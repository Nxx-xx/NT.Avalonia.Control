using Avalonia;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.Charts;

public sealed partial class CartesianChart
{
    private readonly List<WeakReference<ChartSynchronizationGroup>> _synchronizationGroups = [];
    private ChartSynchronizationSnapshot? _publishedSynchronization;
    private bool _publishingSynchronization;
    private int _applyingSynchronization;
    private long _synchronizationOperation;
    private bool _synchronizationNotificationPending;
    private readonly List<Visual> _synchronizationVisibilityAncestors = [];

    internal event EventHandler? ViewSynchronizationChanged;
    internal bool CanSynchronizeView => _attached && IsVisible && this.GetVisualAncestors().All(visual => visual.IsVisible);

    /// <summary>The current primary-axis windows as fractions of their complete transformed extents.
    /// Fractions increase with data values even for inversed axes. Returns null before usable layout.</summary>
    public ChartViewport? GetNormalizedViewport()
    {
        VerifyAccess();
        var scene = GetScene();
        return scene is null ? null : NormalizeViewport(scene.Range, scene.BaseX, scene.BaseY);
    }

    /// <summary>Sets primary-axis windows in ascending transformed fractions, preserving unselected axes.
    /// Each target axis applies its own logarithmic/date/break transform and minimum/maximum zoom.</summary>
    public void SetNormalizedViewport(ChartViewport viewport, ChartInteractionAxes axes = ChartInteractionAxes.Both)
    {
        VerifyAccess();
        ValidateSynchronizationAxes(axes);
        if (!IsNormalizedViewport(viewport)) throw new ArgumentOutOfRangeException(nameof(viewport));
        if (axes == ChartInteractionAxes.None || GetScene() is not { } scene) return;
        var raw = DenormalizeViewport(viewport, scene.BaseX, scene.BaseY);
        ApplyPreparedSynchronization(PrepareSynchronization(raw, axes, scene), axes, scene);
    }

    /// <summary>Gets one primary-axis window in ascending transformed fractions.</summary>
    public ChartRange? GetVisibleFraction(ChartInteractionAxes axis)
    {
        ValidateSingleSynchronizationAxis(axis);
        var view = GetNormalizedViewport();
        return view is null ? null : axis == ChartInteractionAxes.X
            ? new ChartRange(view.Value.XMinimum, view.Value.XMaximum)
            : new ChartRange(view.Value.YMinimum, view.Value.YMaximum);
    }

    /// <summary>Sets one primary-axis window in ascending transformed fractions.</summary>
    public void SetVisibleFraction(ChartRange range, ChartInteractionAxes axis = ChartInteractionAxes.X)
    {
        VerifyAccess();
        ValidateSingleSynchronizationAxis(axis);
        if (!range.IsValid) throw new ArgumentOutOfRangeException(nameof(range));
        if (GetNormalizedViewport() is not { } view) return;
        SetNormalizedViewport(axis == ChartInteractionAxes.X
            ? view with { XMinimum = range.Lower, XMaximum = range.Upper }
            : view with { YMinimum = range.Lower, YMaximum = range.Upper }, axis);
    }

    private static bool IsNormalizedViewport(ChartViewport value) => value.IsValid &&
        value.XMinimum >= 0 && value.XMaximum <= 1 && value.YMinimum >= 0 && value.YMaximum <= 1;

    internal static void ValidateSynchronizationAxes(ChartInteractionAxes axes)
    {
        if ((axes & ~ChartInteractionAxes.Both) != 0) throw new ArgumentOutOfRangeException(nameof(axes));
    }

    private static void ValidateSingleSynchronizationAxis(ChartInteractionAxes axis)
    {
        if (axis is not (ChartInteractionAxes.X or ChartInteractionAxes.Y))
            throw new ArgumentOutOfRangeException(nameof(axis));
    }

    private ChartViewport PrepareSynchronization(ChartViewport requested, ChartInteractionAxes axes, ChartScene scene)
    {
        var merged = new ChartViewport(
            (axes & ChartInteractionAxes.X) != 0 ? requested.XMinimum : scene.Range.XMinimum,
            (axes & ChartInteractionAxes.X) != 0 ? requested.XMaximum : scene.Range.XMaximum,
            (axes & ChartInteractionAxes.Y) != 0 ? requested.YMinimum : scene.Range.YMinimum,
            (axes & ChartInteractionAxes.Y) != 0 ? requested.YMaximum : scene.Range.YMaximum);
        var constrained = ConstrainViewport(merged, scene.BaseX, scene.BaseY);
        // An untouched axis must remain byte-for-byte unchanged, including very large offsets.
        return new ChartViewport(
            (axes & ChartInteractionAxes.X) != 0 ? constrained.XMinimum : scene.Range.XMinimum,
            (axes & ChartInteractionAxes.X) != 0 ? constrained.XMaximum : scene.Range.XMaximum,
            (axes & ChartInteractionAxes.Y) != 0 ? constrained.YMinimum : scene.Range.YMinimum,
            (axes & ChartInteractionAxes.Y) != 0 ? constrained.YMaximum : scene.Range.YMaximum);
    }

    internal ChartSynchronizationSnapshot? CaptureSynchronization()
    {
        VerifyAccess();
        var presented = GetScene();
        // Linked charts exchange viewport intent once, rather than feeding
        // intermediate animation frames back into each other's timelines.
        var scene = _animationTarget ?? presented;
        return scene is null ? null : new(scene.Range, NormalizeViewport(scene.Range, scene.BaseX, scene.BaseY));
    }

    internal ChartViewport? PrepareSynchronizedViewport(ChartSynchronizationSnapshot source,
        ChartInteractionAxes axes, ChartSynchronizationCoordinateMode mode)
    {
        if (GetScene() is not { } scene) return null;
        var raw = mode == ChartSynchronizationCoordinateMode.Normalized
            ? DenormalizeViewport(source.Normalized, scene.BaseX, scene.BaseY) : source.Data;
        return PrepareSynchronization(raw, axes, scene);
    }

    internal void ApplySynchronizedViewport(ChartViewport prepared, ChartInteractionAxes axes)
    {
        if (GetScene() is { } scene) ApplyPreparedSynchronization(prepared, axes, scene);
    }

    private void ApplyPreparedSynchronization(ChartViewport prepared, ChartInteractionAxes axes, ChartScene scene)
    {
        var operation = ++_synchronizationOperation;
        _applyingSynchronization++;
        try
        {
            var normalized = NormalizeViewport(prepared, scene.BaseX, scene.BaseY);
            // Explicit extra-axis windows otherwise override SliceScale. Move only the selected
            // dimension, using each extra axis's own full extent and transform.
            foreach (var axis in scene.ExtraXAxes.Concat(scene.ExtraYAxes))
            {
                var horizontal = scene.ExtraXAxes.Contains(axis);
                if ((axes & (horizontal ? ChartInteractionAxes.X : ChartInteractionAxes.Y)) == 0 ||
                    axis.ViewMinimum is null && axis.ViewMaximum is null) continue;
                var full = BuildScale(axis, scene.Snapshots.Where(item =>
                    ReferenceEquals(horizontal ? item.XAxis : item.YAxis, axis)).ToArray(), horizontal,
                    scene.Scales[axis].CategoryLabels);
                var lower = horizontal ? normalized.XMinimum : normalized.YMinimum;
                var upper = horizontal ? normalized.XMaximum : normalized.YMaximum;
                var bounds = ConstrainAxisView(axis, full, AscendingValue(full, lower), AscendingValue(full, upper));
                axis.SetCurrentValue(ChartAxis.ViewMinimumProperty, bounds.Minimum);
                if (operation != _synchronizationOperation) return;
                axis.SetCurrentValue(ChartAxis.ViewMaximumProperty, bounds.Maximum);
                if (operation != _synchronizationOperation) return;
            }
            // Do not turn an untouched automatic chart into a fixed full-range request.
            // When all dimensions have returned to their complete extents, retain automatic
            // data-growth behavior as long as no explicit primary-axis view is being overridden.
            if (Viewport is null && prepared == scene.Range) return;
            var automatic = prepared == scene.Extent && XAxis.ViewMinimum is null && XAxis.ViewMaximum is null &&
                YAxis.ViewMinimum is null && YAxis.ViewMaximum is null;
            SetCurrentValue(ViewportProperty, automatic ? null : prepared);
        }
        finally
        {
            _applyingSynchronization--;
            NotifyViewSynchronizationChanged();
        }
    }

    internal void RegisterSynchronizationGroup(ChartSynchronizationGroup group)
    {
        _synchronizationGroups.RemoveAll(reference => !reference.TryGetTarget(out _));
        if (!_synchronizationGroups.Any(reference => reference.TryGetTarget(out var value) && ReferenceEquals(value, group)))
            _synchronizationGroups.Add(new(group));
        RefreshViewSynchronizationMembership();
    }

    internal void UnregisterSynchronizationGroup(ChartSynchronizationGroup group)
    {
        _synchronizationGroups.RemoveAll(reference => !reference.TryGetTarget(out var value) || ReferenceEquals(value, group));
        if (_synchronizationGroups.Count == 0) _publishedSynchronization = null;
        RefreshViewSynchronizationMembership();
    }

    private void OnSynchronizationAncestorPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == IsVisibleProperty) RefreshViewSynchronizationMembership();
    }

    private void RefreshViewSynchronizationMembership()
    {
        _synchronizationGroups.RemoveAll(reference => !reference.TryGetTarget(out _));
        var ancestors = _attached && _synchronizationGroups.Count != 0 ? this.GetVisualAncestors().ToArray() : [];
        foreach (var old in _synchronizationVisibilityAncestors.Except(ancestors).ToArray())
        { old.PropertyChanged -= OnSynchronizationAncestorPropertyChanged; _synchronizationVisibilityAncestors.Remove(old); }
        foreach (var added in ancestors.Except(_synchronizationVisibilityAncestors))
        { added.PropertyChanged += OnSynchronizationAncestorPropertyChanged; _synchronizationVisibilityAncestors.Add(added); }
        foreach (var reference in _synchronizationGroups.ToArray())
            if (reference.TryGetTarget(out var group)) group.RefreshMembership(this);
    }

    internal void ResetSynchronizationBaseline()
    {
        var snapshot = CaptureSynchronization();
        // Adding a second observer while an earlier notification is publishing must not
        // acknowledge a newer, reentrant viewport on behalf of the existing observers.
        if (ViewSynchronizationChanged is null) _publishedSynchronization = snapshot;
    }

    private void ViewSynchronizationPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == ViewportProperty) _synchronizationOperation++;
        if (change.Property == IsVisibleProperty) RefreshViewSynchronizationMembership();
    }

    private void NotifyViewSynchronizationChanged()
    {
        if (ViewSynchronizationChanged is null || _applyingSynchronization != 0 || _rendering) return;
        if (_publishingSynchronization) { _synchronizationNotificationPending = true; return; }
        _publishingSynchronization = true;
        try
        {
            var iterations = 0;
            do
            {
                _synchronizationNotificationPending = false;
                var presented = _scene ?? GetScene();
                var scene = _animationTarget ?? presented;
                ChartSynchronizationSnapshot? current = scene is null ? null :
                    new(scene.Range, NormalizeViewport(scene.Range, scene.BaseX, scene.BaseY));
                if (current == _publishedSynchronization) break;
                _publishedSynchronization = current;
                ViewSynchronizationChanged?.Invoke(this, EventArgs.Empty);
                if (++iterations >= 64 && _synchronizationNotificationPending)
                {
                    _synchronizationNotificationPending = false;
                    throw new InvalidOperationException("Chart viewport notifications did not settle after 64 changes. Check contradictory viewport bindings.");
                }
            } while (_synchronizationNotificationPending);
        }
        finally { _publishingSynchronization = false; }
    }
}

internal readonly record struct ChartSynchronizationSnapshot(ChartViewport Data, ChartViewport Normalized);
