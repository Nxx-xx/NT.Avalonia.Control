using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.Charts;

public sealed partial class CartesianChart
{
    public static readonly StyledProperty<bool> IsPointBrowsingEnabledProperty =
        AvaloniaProperty.Register<CartesianChart, bool>(nameof(IsPointBrowsingEnabled));
    public static readonly DirectProperty<CartesianChart, ChartPointHit?> CurrentPointProperty =
        AvaloniaProperty.RegisterDirect<CartesianChart, ChartPointHit?>(nameof(CurrentPoint), chart => chart.CurrentPoint);
    private ChartPointHit? _currentPoint;
    private long _pointBrowseRevision;
    private readonly HashSet<Visual> _browsingAncestors = [];
    // A key press has a finite search budget even for a long run of invalid/offscreen rows.
    private const int KeyboardPointSearchLimit = 4096;

    /// <summary>Opt in to reading points independently of selection. Requires IsKeyboardNavigationEnabled for routed keys.</summary>
    public bool IsPointBrowsingEnabled { get => GetValue(IsPointBrowsingEnabledProperty); set => SetValue(IsPointBrowsingEnabledProperty, value); }
    /// <summary>The current keyboard/automation browsing point, independent of SelectedPoint. Uses the presented scene.</summary>
    public ChartPointHit? CurrentPoint => _currentPoint;

    /// <summary>Moves the reading cursor to a visible source point without selecting or invoking it. Does not take focus.</summary>
    public bool NavigateToPoint(ChartSeries series, int sourceIndex)
    {
        VerifyAccess(); ArgumentNullException.ThrowIfNull(series);
        if (!_attached || !IsEffectivelyEnabled || !IsEffectivelyVisible || GetScene() is not { } scene ||
            !AccessibilityTryGetPoint(series, sourceIndex, out var hit) || !scene.Plot.Contains(hit.Position)) return false;
        var revision = ++_pointBrowseRevision;
        SetAndRaise<ChartPointHit?>(CurrentPointProperty, ref _currentPoint, hit);
        UpdateBrowsingAncestors();
        if (revision != _pointBrowseRevision || !_attached || !ReferenceEquals(_scene, scene)) return false;
        if (ReadingsEnabled) PublishReadings(Array.AsReadOnly(new[] { hit }), hit.Position, scene);
        if (revision != _pointBrowseRevision || !_attached || !ReferenceEquals(_scene, scene)) return false;
        RequestRender();
        NotifyAutomationStateChanged();
        return revision == _pointBrowseRevision && _currentPoint is { } current && SameIdentity(current, hit);
    }

    private bool KeyboardBrowsingPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == CurrentPointProperty || change.Property == AccessibilitySummaryProperty ||
            change.Property == AccessibilitySummaryTextProperty) return true;
        if (change.Property == IsFocusedProperty || change.Property == IsKeyboardFocusWithinProperty)
        { NotifyAutomationStateChanged(); return true; }
        if (change.Property == IsPointBrowsingEnabledProperty)
        {
            if (IsPointBrowsingEnabled) SetCurrentValue(FocusableProperty, true);
            else ClearCurrentPoint();
            UpdateBrowsingAncestors();
            NotifyAutomationStateChanged();
            return true;
        }
        if (change.Property == IsKeyboardNavigationEnabledProperty && !IsKeyboardNavigationEnabled ||
            change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled ||
            change.Property == IsVisibleProperty && !IsVisible) ClearCurrentPoint();
        return false;
    }

    private void ClearCurrentPoint()
    {
        if (_currentPoint is null) return;
        ++_pointBrowseRevision;
        SetAndRaise(CurrentPointProperty, ref _currentPoint, null);
        UpdateBrowsingAncestors();
        RequestRender();
        NotifyAutomationStateChanged();
    }

    private void ReconcileCurrentPoint()
    {
        if (_currentPoint is not { } current) return;
        if (!_attached || !IsEffectivelyEnabled || !IsEffectivelyVisible ||
            !current.Series.EffectiveVisibility || !ActiveSeries.Contains(current.Series))
        { ClearCurrentPoint(); return; }
        // An invalidated scene is pending; never build or scan a source in this notification.
        if (_scene is not { } scene) return;
        if (!AccessibilityTryResolvePoint(current, out var resolved) || !scene.Plot.Contains(resolved.Position))
        { ClearCurrentPoint(); return; }
        if (current == resolved) return;
        var revision = ++_pointBrowseRevision;
        SetAndRaise<ChartPointHit?>(CurrentPointProperty, ref _currentPoint, resolved);
        if (revision == _pointBrowseRevision) RequestRender();
    }

    private bool HandlePointBrowsingKey(KeyEventArgs args)
    {
        if (!IsPointBrowsingEnabled || !IsKeyboardNavigationEnabled || args.Handled ||
            !IsEffectivelyEnabled || !_attached) return false;
        if (args.Key == Key.Escape) { ClearCurrentPoint(); CancelInput(); args.Handled = true; return true; }
        if (args.Key == Key.Home && (args.KeyModifiers & KeyModifiers.Control) != 0)
        { ResetViewport(); args.Handled = true; return true; }
        if (args.Key is Key.Space or Key.Enter)
        {
            // Configuration changes can invalidate the scene between browsing and
            // activation. Resolve the current point against the newly published
            // scene before invoking or selecting it.
            GetScene();
            ReconcileCurrentPoint();
            if (_currentPoint is not { } focused) { args.Handled = true; return true; }
            if (args.Key == Key.Enter) AccessibilityInvokePoint(focused);
            else
            {
                var additive = SelectionMode == ChartSelectionMode.Multiple && (args.KeyModifiers & KeyModifiers.Control) != 0;
                if ((additive || SelectionMode == ChartSelectionMode.SingleDeselect) && _publishedSelection.Any(point => SameIdentity(point, focused)))
                    AccessibilityRemovePoint(focused);
                else AccessibilitySelectPoint(focused, additive);
            }
            args.Handled = true; return true;
        }
        if (args.Key is not (Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown))
            return false;
        args.Handled = true;
        if (GetScene() is not { } scene || scene.Snapshots.Length == 0) return true;
        ReconcileCurrentPoint();
        var current = _currentPoint;
        var seriesIndex = current is null ? -1 : Array.FindIndex(scene.Snapshots, item => ReferenceEquals(item.Series, current.Series));
        var forward = args.Key is Key.Right or Key.Down or Key.PageDown or Key.Home;
        if (args.Key is Key.Up or Key.Down)
        {
            var target = seriesIndex < 0 ? (forward ? 0 : scene.Snapshots.Length - 1) : seriesIndex + (forward ? 1 : -1);
            var ordinal = seriesIndex < 0 ? 0 : BrowsingOrdinal(scene.Snapshots[seriesIndex].Data, current!.Index);
            for (; target >= 0 && target < scene.Snapshots.Length; target += forward ? 1 : -1)
                if (TryBrowseSnapshot(scene.Snapshots[target], Math.Max(0, ordinal), 1, fallbackReverse: true)) break;
            return true;
        }
        if (seriesIndex < 0) seriesIndex = forward ? 0 : scene.Snapshots.Length - 1;
        var snapshot = scene.Snapshots[seriesIndex];
        var index = current is null ? (forward ? 0 : snapshot.Data.Count - 1) : BrowsingOrdinal(snapshot.Data, current.Index);
        index = args.Key switch
        {
            Key.Home => 0,
            Key.End => snapshot.Data.Count - 1,
            Key.PageUp => Math.Max(0, index - 64),
            Key.PageDown => Math.Min(snapshot.Data.Count - 1, Math.Max(0, index) + 64),
            _ => current is null ? index : index + (forward ? 1 : -1),
        };
        TryBrowseSnapshot(snapshot, index, forward ? 1 : -1);
        return true;
    }

    private void UpdateBrowsingAncestors()
    {
        foreach (var ancestor in _browsingAncestors) ancestor.PropertyChanged -= BrowsingAncestorChanged;
        _browsingAncestors.Clear();
        if (!_attached || !IsPointBrowsingEnabled && _currentPoint is null) return;
        foreach (var ancestor in this.GetVisualAncestors())
        {
            _browsingAncestors.Add(ancestor);
            ancestor.PropertyChanged += BrowsingAncestorChanged;
        }
    }

    private void BrowsingAncestorChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == IsVisibleProperty && (sender is Visual { IsVisible: false } || !IsEffectivelyVisible))
        { ClearCurrentPoint(); ClearHover(); }
    }

    private static int BrowsingOrdinal(ChartDataSnapshot snapshot, int sourceIndex)
    {
        if ((uint)sourceIndex < (uint)snapshot.Count && snapshot.GetValue(sourceIndex).SourceIndex == sourceIndex) return sourceIndex;
        for (var index = 0; index < Math.Min(snapshot.Count, KeyboardPointSearchLimit); index++)
            if (snapshot.GetValue(index).SourceIndex == sourceIndex) return index;
        return -1;
    }

    private bool TryBrowseSnapshot(SeriesSnapshot snapshot, int start, int direction, bool fallbackReverse = false)
    {
        if (snapshot.Data.Count == 0) return false;
        var expectedScene = _scene;
        if (fallbackReverse) start = Math.Clamp(start, 0, snapshot.Data.Count - 1);
        var index = start;
        for (var examined = 0; examined < KeyboardPointSearchLimit && index >= 0 && index < snapshot.Data.Count; examined++, index += direction)
        {
            var point = snapshot.Data.GetValue(index);
            if (point.IsEmpty || !IsValid(point.Point)) continue;
            var revision = _pointBrowseRevision;
            if (NavigateToPoint(snapshot.Series, point.SourceIndex)) return true;
            // A callback may choose another point, replace data or detach. That
            // intent consumes this key; continuing would overwrite the callback.
            if (revision != _pointBrowseRevision || !ReferenceEquals(expectedScene, _scene) || !_attached) return true;
        }
        return fallbackReverse && TryBrowseSnapshot(snapshot, start - 1, -1);
    }

    private void RenderCurrentPoint(DrawingContext drawing, ChartScene scene)
    {
        if (_currentPoint is not { } previous || !AccessibilityTryResolvePoint(previous, out var point) || !scene.Plot.Contains(point.Position)) return;
        using var clip = drawing.PushClip(scene.Plot);
        var brush = SelectionBrush ?? Foreground ?? Brushes.Black;
        var radius = point.MarkerDiameter is null ? 11 : Math.Max(11, DisplayedPointRadius(point.Series, point.MarkerDiameter) + 5);
        drawing.DrawRectangle(null, new Pen(brush, 2, DashStyle.Dash), new Rect(point.Position - new Vector(radius, radius), new Size(radius * 2, radius * 2)));
    }
}
