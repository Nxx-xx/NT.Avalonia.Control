using Avalonia;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using System.Runtime.CompilerServices;

namespace NT.AvaloniaControl.Charts;

public enum ChartSelectionMode { None, Single, Multiple, SingleDeselect }
public enum ChartSelectionChangeReason { Pointer, Keyboard, Rectangle, Clear, ExternalValue, Reconcile, Configuration }
public sealed record ChartSelectionResult(int MatchedCount, int SelectedCount, bool IsTruncated)
{
    /// <summary>The request was canceled or superseded by a selection made in its callback.</summary>
    public bool IsCanceled { get; init; }
}
public sealed class ChartSelectionChangingEventArgs(IReadOnlyList<ChartPointHit> oldSelection,
    IReadOnlyList<ChartPointHit> newSelection, ChartSelectionChangeReason reason, bool canCancel) : EventArgs
{
    public IReadOnlyList<ChartPointHit> OldSelection { get; } = oldSelection;
    public IReadOnlyList<ChartPointHit> NewSelection { get; } = newSelection;
    public ChartSelectionChangeReason Reason { get; } = reason;
    /// <summary>False for configuration changes and reconciliation of invalid or removed data.</summary>
    public bool CanCancel { get; } = canCancel;
    /// <summary>Ignored when CanCancel is false.</summary>
    public bool Cancel { get; set; }
}
public sealed class ChartSelectionChangedEventArgs(IReadOnlyList<ChartPointHit> oldSelection, IReadOnlyList<ChartPointHit> newSelection) : EventArgs
{
    public IReadOnlyList<ChartPointHit> OldSelection { get; } = oldSelection;
    public IReadOnlyList<ChartPointHit> NewSelection { get; } = newSelection;
}

public sealed partial class CartesianChart
{
    public static readonly StyledProperty<ChartSelectionMode> SelectionModeProperty = AvaloniaProperty.Register<CartesianChart, ChartSelectionMode>(nameof(SelectionMode), ChartSelectionMode.Single, validate: Enum.IsDefined);
    public static readonly StyledProperty<IReadOnlyList<ChartPointHit>?> SelectedPointsProperty = AvaloniaProperty.Register<CartesianChart, IReadOnlyList<ChartPointHit>?>(nameof(SelectedPoints), defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<int> MaxSelectionCountProperty = AvaloniaProperty.Register<CartesianChart, int>(nameof(MaxSelectionCount), 10_000, validate: value => value > 0 && value <= 100_000);
    public static readonly StyledProperty<IBrush?> SelectionBrushProperty = AvaloniaProperty.Register<CartesianChart, IBrush?>(nameof(SelectionBrush));
    public static readonly StyledProperty<bool> IsKeyboardNavigationEnabledProperty = AvaloniaProperty.Register<CartesianChart, bool>(nameof(IsKeyboardNavigationEnabled));
    private bool _synchronizingSelection;
    private bool _raisingSelectionChanging;
    private long _selectionRevision;
    private bool _selectionBindingRollbackPending;
    private IReadOnlyList<ChartPointHit> _publishedSelection = Array.Empty<ChartPointHit>();
    private IPointer? _selectionPointer;
    private Point _selectionOrigin;
    private Point _selectionEnd;
    private bool _additiveRectangle;

    public ChartSelectionMode SelectionMode { get => GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }
    /// <summary>Immutable selection state; replace the list to update a binding. SelectedPoint mirrors its last point.</summary>
    public IReadOnlyList<ChartPointHit>? SelectedPoints { get => GetValue(SelectedPointsProperty); set => SetValue(SelectedPointsProperty, value); }
    public int MaxSelectionCount { get => GetValue(MaxSelectionCountProperty); set => SetValue(MaxSelectionCountProperty, value); }
    public IBrush? SelectionBrush { get => GetValue(SelectionBrushProperty); set => SetValue(SelectionBrushProperty, value); }
    public bool IsKeyboardNavigationEnabled { get => GetValue(IsKeyboardNavigationEnabledProperty); set => SetValue(IsKeyboardNavigationEnabledProperty, value); }
    public event EventHandler<ChartSelectionChangedEventArgs>? SelectionChanged;
    /// <summary>
    /// Raised before a semantic selection change. Callback selections supersede the outer request and
    /// do not raise this event recursively. External property values may already have been observed;
    /// canceling them writes the canonical previous value back without replacing the binding.
    /// PointInvokedCommand remains a click notification even when selection is canceled.
    /// </summary>
    public event EventHandler<ChartSelectionChangingEventArgs>? SelectionChanging;

    public void ClearSelection() { VerifyAccess(); PublishSelection(Array.Empty<ChartPointHit>(), ChartSelectionChangeReason.Clear); }

    /// <summary>Selects visible point centers in chart-local coordinates, including grouped bar ends. Reports any limit truncation.</summary>
    public ChartSelectionResult SelectRectangle(Rect rectangle, bool additive = false)
    {
        VerifyAccess();
        var scene = GetScene();
        if (SelectionMode == ChartSelectionMode.None || scene is null || !double.IsFinite(rectangle.X) || !double.IsFinite(rectangle.Y) || !double.IsFinite(rectangle.Width) || !double.IsFinite(rectangle.Height))
            return new(0, SelectedPoints?.Count ?? 0, false);
        rectangle = rectangle.Intersect(scene.Plot);
        var selected = additive ? _publishedSelection.ToList() : [];
        var selectedIndices = selected.Select(hit => (hit.Series, hit.Index)).ToHashSet();
        var truncated = false;
        var matched = 0; var limit = SelectionMode == ChartSelectionMode.Multiple ? MaxSelectionCount : 1;
        foreach (var snapshot in scene.Snapshots)
        {
            // Grouped comparison series displace their displayed point centers
            // from the raw projection. Shift the query by the opposite amount;
            // the exact displayed-center test below remains authoritative.
            var offset = SelectionOffset(snapshot);
            var queryRectangle = new Rect(rectangle.X - offset.X, rectangle.Y - offset.Y, rectangle.Width, rectangle.Height);
            var candidates = ChartVisibilityQuery.Points(snapshot.Context!, queryRectangle);
            long projected = 0;
            foreach (var index in candidates)
            {
                var value = snapshot.Data.GetValue(index);
                if (!IsValid(value.Point)) continue;
                var screen = SelectionPosition(snapshot, value.Point);
                projected++;
                if (!double.IsFinite(screen.X) || !double.IsFinite(screen.Y) || !rectangle.Contains(screen)) continue;
                matched++;
                if (selectedIndices.Contains((snapshot.Series, value.SourceIndex))) continue;
                if (selected.Count >= limit) { truncated = true; continue; }
                selectedIndices.Add((snapshot.Series, value.SourceIndex));
                var data = snapshot.Data.Points[index];
                var hit = ChartPointHit.FromData(snapshot.Series, data, screen);
                selected.Add(hit);
            }
            ChartVisibilityQuery.RecordProjected(projected);
        }
        var applied = PublishSelection(selected, ChartSelectionChangeReason.Rectangle);
        return new(matched, _publishedSelection.Count, truncated) { IsCanceled = !applied };
    }

    private static Point SelectionPosition(SeriesSnapshot snapshot, ChartPoint point)
    {
        var context = snapshot.Context!;
        return context.Projection.Project(point) + SelectionOffset(snapshot);
    }

    private static Vector SelectionOffset(SeriesSnapshot snapshot)
    {
        var context = snapshot.Context!;
        if (snapshot.Series.Renderer.UsesCategorySlots && context.GroupCount > 0)
        {
            var offset = -context.GroupWidth / 2 + context.GroupWidth / context.GroupCount * (context.GroupIndex + .5);
            return HorizontalComparison(snapshot.Series) ? new(0, offset) : new(offset, 0);
        }
        return default;
    }

    private static bool SameIdentity(ChartPointHit left, ChartPointHit right) => ReferenceEquals(left.Series, right.Series) &&
        (left.Series.UsesValueIdentity ? Equals(left.StableId, right.StableId) : ReferenceEquals(left.StableId, right.StableId));
    private sealed class SelectionIdentityComparer : IEqualityComparer<ChartPointHit>
    {
        public static readonly SelectionIdentityComparer Instance = new();
        public bool Equals(ChartPointHit? left, ChartPointHit? right) => ReferenceEquals(left, right) || left is not null && right is not null && SameIdentity(left, right);
        public int GetHashCode(ChartPointHit hit) => HashCode.Combine(RuntimeHelpers.GetHashCode(hit.Series),
            hit.StableId is null ? 0 : hit.Series.UsesValueIdentity ? hit.StableId.GetHashCode() : RuntimeHelpers.GetHashCode(hit.StableId));
    }

    private List<ChartPointHit> NormalizeSelection(IEnumerable<ChartPointHit> points)
    {
        var limit = SelectionMode == ChartSelectionMode.Multiple ? MaxSelectionCount : 1;
        var distinct = new List<ChartPointHit>();
        var identities = new HashSet<ChartPointHit>(SelectionIdentityComparer.Instance);
        var owned = ActiveSeries.ToHashSet();
        if (SelectionMode != ChartSelectionMode.None)
            foreach (var value in points)
            {
                if (value is null || !value.Series.EffectiveVisibility || !owned.Contains(value.Series) ||
                    !TryResolveSelection(value, _scene, out var resolved) || !identities.Add(resolved)) continue;
                distinct.Add(resolved);
                if (distinct.Count == limit) break;
            }
        return distinct;
    }

    private bool PublishSelection(IEnumerable<ChartPointHit> points, ChartSelectionChangeReason reason)
    {
        if (_synchronizingSelection) return false;
        var revision = reason == ChartSelectionChangeReason.Reconcile ? _selectionRevision : ++_selectionRevision;
        var distinct = NormalizeSelection(points);
        if (_selectionRevision != revision) { SynchronizeSelectionProperties(); return false; }
        var changed = !_publishedSelection.SequenceEqual(distinct);
        if (changed && reason == ChartSelectionChangeReason.Reconcile) revision = ++_selectionRevision;
        if (!changed && SameSelectionMetadata(distinct))
        {
            if (!Equals(SelectedPoint, distinct.LastOrDefault()) || !ReferenceEquals(SelectedPoints, _publishedSelection))
                SynchronizeSelectionProperties();
            return true;
        }
        var previous = _publishedSelection;
        var current = Array.AsReadOnly(distinct.ToArray());
        if (changed && !_raisingSelectionChanging && SelectionChanging is { } changing)
        {
            var canCancel = reason is not (ChartSelectionChangeReason.Reconcile or ChartSelectionChangeReason.Configuration);
            var args = new ChartSelectionChangingEventArgs(previous, current, reason, canCancel);
            _raisingSelectionChanging = true;
            try
            {
                changing(this, args);
                if (_selectionRevision != revision)
                { SynchronizeSelectionProperties(); return false; }
                // Source replacement can also happen while a chart is unattached.
                // Validate again so a callback cannot publish an identity from the old source.
                var stillCurrent = NormalizeSelection(current);
                if (_selectionRevision != revision || !current.SequenceEqual(stillCurrent) || canCancel && args.Cancel)
                {
                    ReconcileSelectionList();
                    SynchronizeSelectionProperties();
                    return false;
                }
            }
            catch
            {
                ReconcileSelectionList();
                SynchronizeSelectionProperties();
                throw;
            }
            finally { _raisingSelectionChanging = false; }
        }
        _publishedSelection = current;
        SynchronizeSelectionProperties();
        if (changed) SelectionChanged?.Invoke(this, new(previous, current));
        NotifyAutomationStateChanged();
        RequestRender();
        return true;
    }

    private bool SameSelectionMetadata(IReadOnlyList<ChartPointHit> points)
    {
        for (var index = 0; index < points.Count; index++)
        {
            var previous = _publishedSelection[index]; var next = points[index];
            if (!SameSourceValue(previous.Item, next.Item) || !SameSourceValue(previous.StableId, next.StableId)) return false;
        }
        return true;
    }

    private static bool SameSourceValue(object? previous, object? next) => ReferenceEquals(previous, next) ||
        next is ValueType && Equals(previous, next);

    private void SynchronizeSelectionProperties()
    {
        _synchronizingSelection = true;
        try
        {
            SetCurrentValue(SelectedPointsProperty, _publishedSelection);
            SetCurrentValue(SelectedPointProperty, _publishedSelection.LastOrDefault());
        }
        finally { _synchronizingSelection = false; }
    }

    private void SelectionPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (_synchronizingSelection) return;
        if (change.Property == SelectedPointProperty)
        {
            if (!PublishSelection(SelectedPoint is { } hit ? new[] { hit } : Array.Empty<ChartPointHit>(), ChartSelectionChangeReason.ExternalValue)) QueueSelectionBindingRollback();
        }
        else if (change.Property == SelectedPointsProperty)
        {
            if (!PublishSelection(SelectedPoints ?? Array.Empty<ChartPointHit>(), ChartSelectionChangeReason.ExternalValue)) QueueSelectionBindingRollback();
        }
        else if (change.Property == SelectionModeProperty || change.Property == MaxSelectionCountProperty)
        { StopRectangle(); PublishSelection(_publishedSelection, ChartSelectionChangeReason.Configuration); }
        else if (change.Property == IsKeyboardNavigationEnabledProperty && IsKeyboardNavigationEnabled) SetCurrentValue(FocusableProperty, true);
    }

    private void QueueSelectionBindingRollback()
    {
        if (_selectionBindingRollbackPending) return;
        _selectionBindingRollbackPending = true;
        // Source-to-target binding transfers can suppress a synchronous reverse transfer.
        // Send the latest canonical state after that transfer, never a captured stale list.
        Dispatcher.UIThread.Post(() =>
        {
            _selectionBindingRollbackPending = false;
            SynchronizeSelectionProperties();
            BindingOperations.GetBindingExpressionBase(this, SelectedPointsProperty)?.UpdateSource();
            BindingOperations.GetBindingExpressionBase(this, SelectedPointProperty)?.UpdateSource();
        });
    }

    private void SelectFromPointer(ChartPointHit? selected, KeyModifiers modifiers)
    {
        if (SelectionMode == ChartSelectionMode.SingleDeselect && selected is not null && SelectedPoint is { } current && SameIdentity(current, selected))
        { PublishSelection(Array.Empty<ChartPointHit>(), ChartSelectionChangeReason.Pointer); return; }
        if (SelectionMode == ChartSelectionMode.Multiple && (modifiers & KeyModifiers.Control) != 0 && selected is not null)
        {
            var values = _publishedSelection.ToList();
            var existing = values.FindIndex(old => SameIdentity(old, selected));
            if (existing >= 0) values.RemoveAt(existing); else values.Add(selected);
            PublishSelection(values, ChartSelectionChangeReason.Pointer);
        }
        else PublishSelection(selected is null ? Array.Empty<ChartPointHit>() : new[] { selected }, ChartSelectionChangeReason.Pointer);
    }

    private bool BeginRectangle(PointerPressedEventArgs e, Point position)
    {
        if (SelectionMode != ChartSelectionMode.Multiple || (e.KeyModifiers & KeyModifiers.Shift) == 0) return false;
        StopPan(); StopRectangle(); ClearHover();
        _selectionOrigin = _selectionEnd = position; _additiveRectangle = (e.KeyModifiers & KeyModifiers.Control) != 0;
        _selectionPointer = e.Pointer; e.Pointer.Capture(this); e.Handled = true; RequestRender(); return true;
    }
    private void StopRectangle()
    {
        var pointer = _selectionPointer; _selectionPointer = null;
        if (pointer is not null && ReferenceEquals(pointer.Captured, this)) pointer.Capture(null);
        if (pointer is not null) RequestRender();
    }
    private Rect SelectionRectangle(ChartScene scene) => new Rect(
        new Point(Math.Min(_selectionOrigin.X, _selectionEnd.X), Math.Min(_selectionOrigin.Y, _selectionEnd.Y)),
        new Point(Math.Max(_selectionOrigin.X, _selectionEnd.X), Math.Max(_selectionOrigin.Y, _selectionEnd.Y))).Intersect(scene.Plot);

    private void RenderSelection(DrawingContext drawing, ChartScene scene)
    {
        if (_publishedSelection.Count == 0 && _selectionPointer is null) return;
        RenderSelectionCore(drawing, scene);
    }

    private void RenderSelectionCore(DrawingContext drawing, ChartScene scene)
    {
        using var clip = drawing.PushClip(scene.Plot);
        var brush = SelectionBrush ?? Foreground ?? Brushes.DodgerBlue;
        foreach (var selected in _publishedSelection)
            if (scene.Plot.Contains(selected.Position))
            {
                var radius = selected.MarkerDiameter is null ? 9 : Math.Max(9, DisplayedPointRadius(selected.Series, selected.MarkerDiameter) + 4);
                drawing.DrawEllipse(null, new Pen(brush, 3), selected.Position, radius, radius);
            }
        if (_selectionPointer is not null)
        {
            var rectangle = SelectionRectangle(scene);
            using (drawing.PushOpacity(.12)) drawing.DrawRectangle(brush, null, rectangle);
            drawing.DrawRectangle(null, new Pen(brush, 1, DashStyle.Dash), rectangle);
        }
    }

    private void ReconcileSelectionList(ChartScene? scene = null)
    {
        // A source/configuration notification has no scene. Merely reading PlotArea
        // republishes an existing scene and must not supersede a pending request.
        if (_raisingSelectionChanging && scene is null) ++_selectionRevision;
        if (_publishedSelection.Count == 0) return;
        var values = new List<ChartPointHit>();
        foreach (var selected in _publishedSelection)
        {
            if (!selected.Series.EffectiveVisibility || !ActiveSeries.Contains(selected.Series)) continue;
            if (TryResolveSelection(selected, scene, out var resolved)) values.Add(resolved);
        }
        PublishSelection(values, ChartSelectionChangeReason.Reconcile);
    }

    private bool TryResolveSelection(ChartPointHit selected, ChartScene? scene, out ChartPointHit resolved)
    {
        var snapshot = scene?.Snapshots.FirstOrDefault(item => ReferenceEquals(item.Series, selected.Series));
        var data = snapshot is not null && snapshot.Data.Version == selected.Series.SourceVersion
            ? snapshot.Data : selected.Series.CreateSnapshot();
        var valueEquality = selected.Series.UsesValueIdentity;
        var exact = data.TryGetPoint(selected.Index, out var item) &&
            (valueEquality ? Equals(item.StableId, selected.StableId) : ReferenceEquals(item.StableId, selected.StableId));
        if (!exact && !data.TryFindIdentity(selected.StableId, out item, valueEquality) || item.IsEmpty || !IsValid(item.Point))
        { resolved = null!; return false; }
        var screen = snapshot is null ? selected.Position : SelectionPosition(snapshot, item.Point);
        if (!double.IsFinite(screen.X) || !double.IsFinite(screen.Y)) { resolved = null!; return false; }
        resolved = selected.Point == item.Point && selected.Index == item.SourceIndex && selected.Position == screen &&
            selected.RawPoint == item.RawPoint && selected.BaseValue == item.BaseValue && selected.StackValue == item.StackValue &&
            selected.CumulativeValue == item.CumulativeValue && selected.IsTotal == item.IsTotal &&
            selected.SizeValue == item.SizeValue && Equals(selected.ColorValue, item.ColorValue) && selected.MarkerDiameter == item.MarkerDiameter && Equals(selected.PointFill, item.PointFill) && selected.Analysis == item.Analysis &&
            ReferenceEquals(selected.StableId, item.StableId) &&
            (ReferenceEquals(selected.Item, item.Item) || item.Item is ValueType && Equals(selected.Item, item.Item))
            ? selected : ChartPointHit.FromData(selected.Series, item, screen);
        return true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape && (IsTouchInputActive || _panPointer is not null || _selectionPointer is not null)) { CancelInput(); e.Handled = true; return; }
        if (!IsKeyboardNavigationEnabled || e.Handled) return;
        if (HandlePointBrowsingKey(e)) return;
        if (HandleSeriesSelectionKey(e)) return;
        if (e.Key == Key.Escape) { CancelInput(); e.Handled = true; return; }
        if (e.Key == Key.Home) { ResetViewport(); e.Handled = true; return; }
        if (e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down) || SelectionMode == ChartSelectionMode.None) return;
        var scene = GetScene(); if (scene is null) return;
        var forward = e.Key is Key.Right or Key.Down;
        var snapshots = forward ? scene.Snapshots : scene.Snapshots.Reverse().ToArray();
        var current = SelectedPoint;
        var passed = current is null;
        foreach (var snapshot in snapshots)
        {
            for (var offset = 0; offset < snapshot.Data.Count; offset++)
            {
                var index = forward ? offset : snapshot.Data.Count - 1 - offset;
                var value = snapshot.Data.GetValue(index);
                if (!passed)
                {
                    if (ReferenceEquals(snapshot.Series, current!.Series) && value.SourceIndex == current.Index) passed = true;
                    continue;
                }
                if (!IsValid(value.Point)) continue;
                var screen = SelectionPosition(snapshot, value.Point);
                if (!scene.Plot.Contains(screen)) continue;
                var point = snapshot.Data.Points[index];
                var hit = ChartPointHit.FromData(snapshot.Series, point, screen);
                var seriesSelectionRevision = _seriesSelectionRevision;
                if (PublishSelection(new[] { hit }, ChartSelectionChangeReason.Keyboard))
                {
                    if (seriesSelectionRevision == _seriesSelectionRevision && ReferenceEquals(_scene, scene)) SelectSeriesFromKeyboard(hit.Series);
                    if (ReadingsEnabled && ReferenceEquals(_scene, scene) && SelectedPoint is { } currentHit && SameIdentity(currentHit, hit))
                        PublishReadings(Array.AsReadOnly(new[] { hit }), screen, scene);
                }
                e.Handled = true; return;
            }
        }
    }

    private void CancelInput() { InvalidateInputTransform(); StopRectangle(); StopPan(); ClearHover(); }
}
