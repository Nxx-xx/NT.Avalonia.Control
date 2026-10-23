using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;

namespace NT.AvaloniaControl.Charts;

/// <summary>A lightweight Cartesian chart for line, grouped bar and scatter series.</summary>
/// <remarks>Update bound collections on the Avalonia UI thread. Invalid points form gaps in lines.</remarks>
public sealed partial class CartesianChart : ChartBase
{
    public static readonly StyledProperty<IEnumerable<ChartSeries>?> SeriesSourceProperty =
        AvaloniaProperty.Register<CartesianChart, IEnumerable<ChartSeries>?>(nameof(SeriesSource));
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<CartesianChart, string?>(nameof(Title));
    public static readonly StyledProperty<ChartAxis> XAxisProperty =
        AvaloniaProperty.Register<CartesianChart, ChartAxis>(nameof(XAxis));
    public static readonly StyledProperty<ChartAxis> YAxisProperty =
        AvaloniaProperty.Register<CartesianChart, ChartAxis>(nameof(YAxis));
    public static readonly StyledProperty<bool> ShowLegendProperty =
        AvaloniaProperty.Register<CartesianChart, bool>(nameof(ShowLegend), true);
    public static readonly StyledProperty<bool> ShowGridLinesProperty =
        AvaloniaProperty.Register<CartesianChart, bool>(nameof(ShowGridLines), true);
    public static readonly StyledProperty<IBrush?> AxisBrushProperty =
        AvaloniaProperty.Register<CartesianChart, IBrush?>(nameof(AxisBrush), Brushes.SlateGray);
    public static readonly StyledProperty<IBrush?> GridLineBrushProperty =
        AvaloniaProperty.Register<CartesianChart, IBrush?>(nameof(GridLineBrush), Brushes.Gainsboro);
    public static readonly StyledProperty<string> EmptyTextProperty =
        AvaloniaProperty.Register<CartesianChart, string>(nameof(EmptyText), "暂无数据");

    private static readonly IBrush[] Palette =
    [
        Brush.Parse("#4385F4"), Brush.Parse("#19A798"), Brush.Parse("#F2A33A"),
        Brush.Parse("#9B72E8"), Brush.Parse("#E36D8F"), Brush.Parse("#44B4C4"),
    ];
    private readonly HashSet<ChartSeries> _observedSeries = [];
    private readonly Dictionary<ChartSeries, (long SourceVersion, long PublishedSnapshotRevision)> _observedVersions = [];
    private readonly HashSet<ChartAxis> _observedAxes = [];
    private readonly HashSet<StyledElement> _ownedChildren = [];
    private INotifyCollectionChanged? _seriesSourceNotifications;
    private readonly ChartAxis _defaultXAxis = new();
    private readonly ChartAxis _defaultYAxis = new();
    private bool _attached;
    private ChartScene? _scene;
    private bool _rendering;
    private bool _sceneStatePending;

    static CartesianChart()
    {
        AffectsRender<CartesianChart>(TitleProperty, XAxisProperty, YAxisProperty,
            ShowLegendProperty, ShowGridLinesProperty, AxisBrushProperty, GridLineBrushProperty,
            EmptyTextProperty, ForegroundProperty, BackgroundProperty, FontSizeProperty,
            FontFamilyProperty, FontStyleProperty, FontWeightProperty, PaddingProperty);
        AffectsRender<CartesianChart>(ColorPaletteProperty, LegendPositionProperty, LegendOrientationProperty,
            IsLegendFloatingProperty, LegendHorizontalAlignmentProperty, LegendVerticalAlignmentProperty,
            FloatingLegendMarginProperty, FloatingLegendBackgroundProperty);
    }

    public CartesianChart()
    {
        SetCurrentValue(XAxisProperty, _defaultXAxis);
        SetCurrentValue(YAxisProperty, _defaultYAxis);
        Series.CollectionChanged += OnSeriesCollectionChanged;
        TechnicalIndicators.CollectionChanged += OnSeriesCollectionChanged;
        Axes.CollectionChanged += OnSeriesCollectionChanged;
        InitializeVisualFeatures();
        InitializeTouchInput();
        LostFocus += (_, _) => { CancelInput(); ClearCurrentPoint(); };
        SynchronizeSubscriptions();
        ClipToBounds = true;
    }

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public ChartAxis XAxis { get => GetValue(XAxisProperty) ?? _defaultXAxis; set => SetValue(XAxisProperty, value); }
    public ChartAxis YAxis { get => GetValue(YAxisProperty) ?? _defaultYAxis; set => SetValue(YAxisProperty, value); }
    public bool ShowLegend { get => GetValue(ShowLegendProperty); set => SetValue(ShowLegendProperty, value); }
    public bool ShowGridLines { get => GetValue(ShowGridLinesProperty); set => SetValue(ShowGridLinesProperty, value); }
    public IBrush? AxisBrush { get => GetValue(AxisBrushProperty); set => SetValue(AxisBrushProperty, value); }
    public IBrush? GridLineBrush { get => GetValue(GridLineBrushProperty); set => SetValue(GridLineBrushProperty, value); }
    public string EmptyText { get => GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }
    public ObservableCollection<ChartSeries> Series { get; } = [];
    /// <summary>Optional bindable series collection. Null uses the XAML-friendly Series collection.</summary>
    public IEnumerable<ChartSeries>? SeriesSource { get => GetValue(SeriesSourceProperty); set => SetValue(SeriesSourceProperty, value); }
    /// <summary>Additional axes addressed by series or annotation XAxisKey and YAxisKey.</summary>
    public ObservableCollection<ChartAxis> Axes { get; } = [];
    private IEnumerable<ChartSeries> ActiveSeries => EnumerateAnalyticalSeries();

    private void OnSeriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        VerifyAccess();
        PrepareAnimationChange(ReferenceEquals(sender, Axes) ? ChartAnimationTriggers.None : ChartAnimationTriggers.Data);
        InvalidateInputTransform();
        StopRectangle(); StopPan(); InvalidateScene(clearData: true); SynchronizeSubscriptions();
        if (_attached && !CanRunScheduledWork) _schedulingReconcilePending = true; else ReconcileSelection();
        RequestRender();
        NotifyViewSynchronizationChanged();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var desired = new Size(
            double.IsFinite(availableSize.Width) ? Math.Min(520, availableSize.Width) : 520,
            double.IsFinite(availableSize.Height) ? Math.Min(300, availableSize.Height) : 300);
        var presentationSize = new Size(
            double.IsFinite(availableSize.Width) ? availableSize.Width : desired.Width,
            double.IsFinite(availableSize.Height) ? availableSize.Height : desired.Height);
        MeasureLegendPresentation(presentationSize);
        MeasureLabelPresentation(presentationSize);
        MeasureForegroundPresentation(presentationSize);
        MeasurePresentation(presentationSize);
        MeasureTrackballPresentation(presentationSize);
        return desired;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var arranged = base.ArrangeOverride(finalSize);
        ArrangeLegendPresentation(finalSize);
        ArrangeLabelPresentation(finalSize);
        ArrangeForegroundPresentation(finalSize);
        ArrangePresentation(finalSize);
        ArrangeTrackballPresentation(finalSize);
        PrepareAnimationForLayout();
        return arranged;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        LegendPresentationTemplateApplied();
        EnsureLabelPresentationMembership();
        EnsureForegroundPresentationMembership();
        PresentationTemplateApplied();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        SchedulingAttachedChanged();
        UpdateBrowsingAncestors();
        AnimationAttached();
        RefreshTouchInputAncestors();
        InvalidateScene(clearData: true);
        SynchronizeSubscriptions();
        LegendPresentationAttached();
        PresentationAttached();
        InvalidateVisual();
        RefreshViewSynchronizationMembership();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _attached = false;
        SchedulingAttachedChanged();
        UpdateBrowsingAncestors();
        ClearCurrentPoint();
        AnimationDetached();
        InvalidateInputTransform();
        RefreshTouchInputAncestors();
        RefreshViewSynchronizationMembership();
        StopRectangle();
        StopPan();
        InvalidateScene(clearData: true);
        SynchronizeSubscriptions();
        LegendPresentationDetached();
        ClearLabelPresentation();
        ClearForegroundPresentation();
        PresentationDetached();
        ReleaseResourceCaches();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        // Pointer containment changes interaction presentation, not the data/view
        // transition. Rebuilding the scene here would settle a running or paused frame.
        if (change.Property == IsPointerOverProperty) { RequestRender(); return; }
        if (change.Property == IsVisibleProperty) UpdateSchedulingActivity();
        if (BackgroundLayoutPropertyChanged(change)) return;
        if (ResourcesPropertyChanged(change)) return;
        if (KeyboardBrowsingPropertyChanged(change)) return;
        if (AnimationPropertyChanged(change)) return;
        if (StreamingPropertyChanged(change)) return;
        ViewSynchronizationPropertyChanged(change);
        if (PlotAreaContentPropertyChanged(change)) return;
        if (TouchInputPropertyChanged(change)) return;
        if (change.Property == WheelModeProperty || change.Property == WheelZoomModifiersProperty ||
            change.Property == WheelPanSensitivityProperty || change.Property == WheelZoomSensitivityProperty || change.Property == InputRoutingModeProperty)
        { InvalidateInputTransform(); StopRectangle(); StopPan(); return; }
        if (change.Property == ViewportProperty && !IsInputTransformPropertyWrite(change) ||
            change.Property == InteractionAxesProperty || change.Property == XAxisProperty || change.Property == YAxisProperty ||
            change.Property == SeriesSourceProperty || change.Property == IsZoomEnabledProperty || change.Property == IsPanEnabledProperty ||
            change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled)
            InvalidateInputTransform();
        if (SeriesSelectionPropertyChanged(change)) return;
        if (change.Property == BoundsProperty || change.Property == IsVisibleProperty && !IsVisible) CancelInput();
        if (change.Property == IsVisibleProperty && !IsVisible)
        {
            ClearLabelPresentation();
            ClearForegroundPresentation();
            foreach (var child in _ownedChildren)
                if (child is ChartSeries series) CartesianSeriesRenderer.ReleaseBarGeometry(series);
        }
        SelectionPropertyChanged(change);
        if (LegendPresentationPropertyChanged(change) || ReadingPropertyChanged(change) || PresentationPropertyChanged(change)) return;
        if (change.Property == VisibleRangeProperty || change.Property == PlotAreaProperty ||
            change.Property == HoveredPointProperty || change.Property == SelectedPointProperty ||
            change.Property == HoveredPointsProperty || change.Property == SelectedPointsProperty ||
            change.Property == PointInvokedCommandProperty)
        { if (change.Property == SelectedPointProperty || change.Property == SelectedPointsProperty) RequestRender(); return; }
        if (change.Property == SelectionBrushProperty || change.Property == CrosshairBrushProperty ||
            change.Property == TooltipFormatterProperty || change.Property == LegendRendererProperty ||
            change.Property == ShowCrosshairProperty)
        { RequestRender(); return; }
        if (change.Property == ViewportProperty)
            PrepareAnimationChange(IsInputTransformPropertyWrite(change) ? ChartAnimationTriggers.None : ChartAnimationTriggers.Viewport);
        else if (change.Property == SeriesSourceProperty)
            PrepareAnimationChange(ChartAnimationTriggers.Data);
        else PrepareAnimationChange(ChartAnimationTriggers.None);
        InvalidateScene(clearAxisText: change.Property != ViewportProperty,
            clearData: change.Property == SeriesSourceProperty || change.Property == XAxisProperty ||
                change.Property == YAxisProperty || change.Property == ColorPaletteProperty);
        if (change.Property == SeriesSourceProperty)
        {
            if (_seriesSourceNotifications is not null) _seriesSourceNotifications.CollectionChanged -= OnSeriesCollectionChanged;
            _seriesSourceNotifications = _attached ? SeriesSource as INotifyCollectionChanged : null;
            if (_seriesSourceNotifications is not null) _seriesSourceNotifications.CollectionChanged += OnSeriesCollectionChanged;
            SynchronizeSubscriptions();
            if (_attached && !CanRunScheduledWork) _schedulingReconcilePending = true; else ReconcileSelection();
        }
        if (change.Property == XAxisProperty || change.Property == YAxisProperty)
        {
            SynchronizeSubscriptions();
            ResetViewportCore(pauseFollowing: false);
        }
        OnInteractionPropertyChanged(change);
        NotifyViewSynchronizationChanged();
    }

    private void SynchronizeSubscriptions()
    {
        SynchronizeVisualFeatures();
        var notifications = _attached ? SeriesSource as INotifyCollectionChanged : null;
        if (!ReferenceEquals(notifications, _seriesSourceNotifications))
        {
            if (_seriesSourceNotifications is not null) _seriesSourceNotifications.CollectionChanged -= OnSeriesCollectionChanged;
            _seriesSourceNotifications = notifications;
            if (_seriesSourceNotifications is not null) _seriesSourceNotifications.CollectionChanged += OnSeriesCollectionChanged;
        }
        var allSeries = ActiveSeries.Where(item => item is not null).ToHashSet();
        var allAxes = Axes.Append(XAxis).Append(YAxis).ToHashSet();
        SynchronizeAnalyticalOwnership(allSeries, allAxes);
        var series = _attached ? allSeries : [];
        foreach (var old in _observedSeries.Except(series).ToArray())
        {
            // A view model may keep the series alive after detach/removal. Retire
            // presentation-only geometry without mutating earlier recordings.
            CartesianSeriesRenderer.ReleaseBarGeometry(old);
            old.Changed -= OnDataChanged;
            _observedSeries.Remove(old);
            _observedVersions.Remove(old);
        }
        foreach (var item in series.Except(_observedSeries))
        {
            _observedVersions[item] = (item.SourceVersion, item.PublishedSnapshotRevision);
            item.Changed += OnDataChanged;
            _observedSeries.Add(item);
        }
        foreach (var item in allSeries.OfType<ChartAnalyticalSeries>()) item.RefreshSourceObservation();

        var axes = _attached ? allAxes : [];
        foreach (var old in _observedAxes.Except(axes).ToArray())
        {
            old.Changed -= OnDataChanged;
            _observedAxes.Remove(old);
        }
        foreach (var item in axes.Except(_observedAxes))
        {
            item.Changed += OnDataChanged;
            _observedAxes.Add(item);
        }
    }

    private void OnDataChanged(object? sender, EventArgs e)
    {
        VerifyAccess();
        if (e is ChartAnalyticsMembershipChangedEventArgs || e is AvaloniaPropertyChangedEventArgs { Property: var membershipProperty } &&
            (membershipProperty == ChartSeries.IsVisibleProperty || membershipProperty == ChartAnalyticalSeries.SourceSeriesProperty))
            SynchronizeSubscriptions();
        if (_attached && !CanRunScheduledWork)
        {
            if (sender is ChartSeries hiddenSeries) _observedVersions[hiddenSeries] = (hiddenSeries.SourceVersion, hiddenSeries.PublishedSnapshotRevision);
            _schedulingReconcilePending = true;
            _schedulingResetViewportPending |= e is ChartAxisChangedEventArgs { RangeChanged: true };
            InvalidateScene(clearData: true);
            return;
        }
        if (sender is not ChartAxis inputAxis || e is not ChartAxisChangedEventArgs inputChange || !IsInputTransformAxisWrite(inputAxis, inputChange))
            InvalidateInputTransform();
        var dataChanged = sender is ChartSeries series && (!_observedVersions.TryGetValue(series, out var previous) ||
            previous != (series.SourceVersion, series.PublishedSnapshotRevision));
        if (dataChanged && sender is ChartSeries deferredSeries && DeferStreamingDataChange(deferredSeries)) return;
        if (!_resettingViewportForDataPolicy && sender is ChartAxis && e is ChartAxisChangedEventArgs { Property: var navigationProperty } &&
            (navigationProperty == ChartAxis.ViewMinimumProperty || navigationProperty == ChartAxis.ViewMaximumProperty))
            PauseFollowingForNavigation();
        if (dataChanged) PrepareAnimationChange(ChartAnimationTriggers.Data);
        else if (sender is ChartAxis animationAxis && e is ChartAxisChangedEventArgs { Property: var animationProperty } &&
            (animationProperty == ChartAxis.ViewMinimumProperty || animationProperty == ChartAxis.ViewMaximumProperty))
            PrepareAnimationChange(IsInputTransformAxisWrite(animationAxis, (ChartAxisChangedEventArgs)e) ? ChartAnimationTriggers.None : ChartAnimationTriggers.Viewport);
        else PrepareAnimationChange(ChartAnimationTriggers.None);
        if (sender is ChartSeries current) _observedVersions[current] = (current.SourceVersion, current.PublishedSnapshotRevision);
        if (dataChanged || e is ChartAxisChangedEventArgs { RangeChanged: true } ||
            !_applyingPan && e is ChartAxisChangedEventArgs { Property: var property } &&
            (property == ChartAxis.ViewMinimumProperty || property == ChartAxis.ViewMaximumProperty)) { StopRectangle(); StopPan(); }
        if (sender is ChartAxis && (e is not ChartAxisChangedEventArgs viewChange ||
            viewChange.Property != ChartAxis.ViewMinimumProperty && viewChange.Property != ChartAxis.ViewMaximumProperty))
            _baseScaleCache.Clear();
        InvalidateScene(clearAxisText: !dataChanged && (e is not ChartAxisChangedEventArgs axisChange ||
                axisChange.Property != ChartAxis.ViewMinimumProperty && axisChange.Property != ChartAxis.ViewMaximumProperty),
            clearData: sender is ChartSeries changedSeries && (dataChanged ||
                SeriesAffectsPreparedData(changedSeries, e)) || sender is ChartAxis axis && AxisAffectsPreparedData(axis, e));
        if (e is ChartAxisChangedEventArgs { RangeChanged: true }) ResetViewportCore(pauseFollowing: false);
        ReconcileSelection();
        RequestRender();
        NotifyViewSynchronizationChanged();
    }

    private void InvalidateScene(bool clearAxisText = true, bool clearData = false)
    {
        BackgroundLayoutInvalidated();
        AnimationSceneInvalidated();
        _scene = null;
        ReleaseSeriesDrawingCache();
        ReleaseSegmentCaches();
        ReleaseInteractionCaches();
        if (clearData) { _dataPreparationRevision++; _preparedData = null; _baseScaleCache.Clear(); }
        InvalidatePlotAreaContent();
        InvalidateLegendPresentation();
        InvalidateLabelPresentation();
        if (clearAxisText) ReleaseAxisTextCaches();
        ClearHover();
        NotifyAutomationStateChanged();
    }
    /// <summary>Requests a coalesced refresh turn in the shared chart queue. Hidden or detached charts resume with their latest state when shown.</summary>
    /// <remarks>May be called from a worker thread. Repeated off-thread calls share one UI handoff; diagnostics count UI-admitted requests and refresh dispatches, not presented frames.</remarks>
    public void RequestRender()
    {
        QueueScheduledRefresh();
    }

    public override void Render(DrawingContext context)
    {
        // The compositor can flush an earlier invalidation after an ancestor hides.
        // Scheduler cancellation alone cannot stop that callback from rebuilding data.
        // Detached direct drawings and explicitly scoped exports remain caller-requested work.
        if (_attached && !CanRunScheduledWork && !_exportInProgress && _samplingExport is null) return;
        using var rendering = new RenderingScope(this);
        base.Render(context);
        if (Bounds.Width <= 0 || Bounds.Height <= 0) return;
        context.DrawRectangle(Background ?? Brushes.Transparent, null, new Rect(Bounds.Size));
        using var chartClip = context.PushClip(new Rect(Bounds.Size));
        var scene = GetScene();
        if (scene is null) return;
        var plot = scene.Plot;
        var xScale = scene.XScale;
        var yScale = scene.YScale;

        if (scene.TitleOrigin is { } titleOrigin)
            DrawLabel(context, Title!, titleOrigin, scene.Right - scene.Left, FontSize + 4, FontWeight.SemiBold);
        RenderLegend(context, scene);
        RenderPlotBands(context, scene, ChartPlotBandLayer.BehindSeries);
        RenderAnnotations(context, scene, ChartAnnotationLayer.BehindSeries);
        RenderAxes(context, scene);

        RenderSeriesLayer(context, scene);
        RenderPlotBands(context, scene, ChartPlotBandLayer.AboveSeries);
        RenderAnnotations(context, scene, ChartAnnotationLayer.AboveSeries);
        RenderDataLabels(context, scene);
        RenderSelection(context, scene);
        RenderCurrentPoint(context, scene);
        RenderReadings(context, scene);

        if (!scene.HasPoints)
        {
            var empty = MakeText(EmptyText);
            DrawLabel(context, EmptyText, new Point(plot.Left, plot.Center.Y - empty.Height / 2), plot.Width,
                alignment: TextAlignment.Center);
        }
        RenderLegend(context, scene, floating: true);
        RenderTooltip(context, scene);
    }

    private ChartScene? GetScene()
    {
        CheckSamplingRenderScale();
        if (UsesLegendPresentation)
        {
            if (IsSynchronizingLegendPresentation) return null;
            // A data/property change can arrive after layout but before the next
            // compositor pass. Rendering must never build, measure or release
            // control-based legend content, even when the cached scene is absent.
            if (_rendering)
            {
                if (LegendPresentationNeedsLayout) QueueLegendPresentationLayout();
                if (_scene is not null) PublishSceneState(_scene);
                return _scene;
            }
            EnsureLegendPresentationForScene();
        }
        if (_scene is not null) { PublishSceneState(_scene); return _scene; }
        return BuildScene();
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private ChartScene? BuildScene()
    {
        var padding = Padding;
        var left = Math.Max(8, padding.Left);
        var right = Bounds.Width - Math.Max(8, padding.Right);
        var top = Math.Max(8, padding.Top);
        if (right - left < 40 || Bounds.Height < 40) return EmptyScene();

        _viewBuildCount++;
        var prepared = GetPreparedData();
        var allSeries = prepared.AllSeries;
        // Contexts, clips and paths belong to this view. Only immutable data and
        // category maps are shared with another viewport or layout revision.
        var snapshots = prepared.Snapshots.Select(item => new SeriesSnapshot(item.Series, item.Data,
            GetSeriesBrush(item.Series), item.Index, item.XAxis, item.YAxis) { SecondaryAxis = item.SecondaryAxis }).ToArray();
        var categories = prepared.Categories;

        Point? titleOrigin = null;
        if (!string.IsNullOrWhiteSpace(Title))
        {
            titleOrigin = new Point(left, top);
            top += FontSize + 17;
        }

        var contentBottom = Bounds.Height - Math.Max(8, padding.Bottom);
        var legend = LayoutLegend(allSeries, ref left, ref right, ref top, ref contentBottom);

        Point? yTitleOrigin = null;
        var (xAxes, yAxes) = AnnotationSceneAxes(snapshots, allSeries);
        var titleAxes = yAxes
            .Where(axis => axis.IsVisible && axis.ShowTitle && !string.IsNullOrWhiteSpace(axis.Title)).ToArray();
        if (titleAxes.Length > 0) top += titleAxes.Max(axis => AxisText(axis.Title!, axis, true).Height) + 8;

        var baseScales = new Dictionary<ChartAxis, ChartScale>();
        foreach (var axis in xAxes) baseScales[axis] = GetBaseScale(axis, snapshots, true, categories.GetValueOrDefault(axis));
        foreach (var axis in yAxes)
        {
            if (xAxes.Contains(axis)) throw new InvalidOperationException("An axis instance cannot be used in both dimensions.");
            baseScales[axis] = GetBaseScale(axis, snapshots, false, categories.GetValueOrDefault(axis));
        }
        var baseX = baseScales[XAxis];
        var baseY = baseScales[YAxis];
        var extent = new ChartViewport(baseX.Minimum, baseX.Maximum, baseY.Minimum, baseY.Maximum);
        var requestedRange = Viewport ?? new ChartViewport(XAxis.ViewMinimum ?? extent.XMinimum, XAxis.ViewMaximum ?? extent.XMaximum,
            YAxis.ViewMinimum ?? extent.YMinimum, YAxis.ViewMaximum ?? extent.YMaximum);
        requestedRange = FollowingRange(requestedRange, extent, snapshots);
        var range = ConstrainViewport(requestedRange, baseX, baseY);
        var xScale = range.XMinimum == baseX.Minimum && range.XMaximum == baseX.Maximum ? baseX :
            CreateViewScale(XAxis, range.XMinimum, range.XMaximum, baseX.CategoryLabels);
        var yScale = range.YMinimum == baseY.Minimum && range.YMaximum == baseY.Maximum ? baseY :
            CreateViewScale(YAxis, range.YMinimum, range.YMaximum, baseY.CategoryLabels);
        // Keep the plot rectangle stable as zoom changes tick precision and digit counts.
        // Reserve the full-range labels plus a typical signed decimal label in every view.
        var extraY = yAxes.Where(axis => !ReferenceEquals(axis, YAxis)).ToArray();
        var extraX = xAxes.Where(axis => !ReferenceEquals(axis, XAxis)).ToArray();
        var layouts = new Dictionary<ChartAxis, ChartAxisLayout>();
        foreach (var axis in yAxes) layouts[axis] = MeasureAxis(axis, baseScales[axis], Math.Max(40, Bounds.Height - top - 70), false);
        var axisWidths = yAxes.ToDictionary(axis => axis, axis => layouts[axis].LabelWidth);
        var leftGutter = yAxes.Where(axis => axis.Position == ChartAxisPosition.Near).Sum(axis => AxisGutter(axis, layouts[axis], false));
        var rightGutter = yAxes.Where(axis => axis.Position == ChartAxisPosition.Far).Sum(axis => AxisGutter(axis, layouts[axis], false));
        foreach (var axis in xAxes) layouts[axis] = MeasureAxis(axis, baseScales[axis], Math.Max(20, right - left - leftGutter - rightGutter - 6), true);
        var bottom = contentBottom - xAxes.Where(axis => axis.Position == ChartAxisPosition.Near).Sum(axis => AxisGutter(axis, layouts[axis], true));
        top += xAxes.Where(axis => axis.Position == ChartAxisPosition.Far).Sum(axis => AxisGutter(axis, layouts[axis], true));
        var plot = new Rect(left + leftGutter, top + 4,
            Math.Max(0, right - left - leftGutter - rightGutter - 6), Math.Max(0, bottom - top - 4));
        if (plot.Width < 20 || plot.Height < 20) return EmptyScene();
        if (ShowLegend && IsLegendFloating) legend = LayoutFloatingLegend(allSeries, plot);
        PositionAxisLayouts(plot, xAxes, yAxes, layouts);
        var viewScales = new Dictionary<ChartAxis, ChartScale>(baseScales) { [XAxis] = xScale, [YAxis] = yScale };
        foreach (var axis in extraX)
            viewScales[axis] = SliceScale(axis, baseScales[axis], baseX, range.XMinimum, range.XMaximum, baseScales[axis].CategoryLabels);
        foreach (var axis in extraY)
            viewScales[axis] = SliceScale(axis, baseScales[axis], baseY, range.YMinimum, range.YMaximum, baseScales[axis].CategoryLabels);
        // Keep full-range gutters, but fit labels to the current view's ticks after zooming.
        foreach (var axis in xAxes.Concat(yAxes))
        {
            var reserved = layouts[axis];
            var horizontal = xAxes.Contains(axis);
            layouts[axis] = MeasureAxis(axis, viewScales[axis], horizontal ? plot.Width : plot.Height, horizontal) with
            { LabelWidth = reserved.LabelWidth, LabelHeight = reserved.LabelHeight, Bounds = reserved.Bounds };
        }
        foreach (var snapshot in snapshots)
        {
            var projection = new CartesianProjection(plot, viewScales[snapshot.XAxis], viewScales[snapshot.YAxis], snapshot.Data);
            snapshot.BreakClip = CreateBreakClip(plot, viewScales[snapshot.XAxis], viewScales[snapshot.YAxis]);
            snapshot.Context = new(snapshot.Series, snapshot.Data, projection, snapshot.Brush, 0, 1, 0)
            {
                PrimaryBreakClip = snapshot.BreakClip,
                RenderScaling = _samplingSceneRenderScale,
                PointHitRegions = EncodedPoints(snapshot.Series) ? CreatePointHitRegions(plot, viewScales[snapshot.XAxis], viewScales[snapshot.YAxis]) : null,
            };
        }
        LayoutCategoryGroups(snapshots, viewScales, plot);
        foreach (var snapshot in snapshots) SetSecondaryContext(snapshot, plot, viewScales);
        PrepareScenePaths(snapshots);
        _scene = new ChartScene(left, right, titleOrigin, yTitleOrigin, legend, plot, xScale, yScale,
            extent, range, snapshots, snapshots.Any(HasData),
            extraX, extraY, viewScales, baseX, baseY, axisWidths, layouts);
        CompleteAnimationScene();
        PublishSceneState(_scene);
        return _scene;
    }

    private ChartScene? EmptyScene()
    {
        PublishSceneState(null);
        return null;
    }

    private static bool HasData(SeriesSnapshot snapshot)
    {
        if (snapshot.Series.Renderer is CartesianSeriesRenderer or ChartLineSeriesRenderer or
            ChartComparisonSeriesRenderer or ChartPointSeriesRenderer or PictorialBarRenderer or ChartStatisticalSeriesRenderer or ChartFinancialSeriesRenderer)
            return ChartDataExtrema.For(snapshot.Data).Bounds is not null;
        // Extensions may deliberately decline automatic bounds while using explicit axes.
        for (var index = 0; index < snapshot.Data.Count; index++)
        {
            var point = snapshot.Data.GetValue(index).Point;
            if (snapshot.XAxis.IsValidValue(point.X) && snapshot.YAxis.IsValidValue(point.Y)) return true;
        }
        return false;
    }

    private void PublishSceneState(ChartScene? scene)
    {
        if (_rendering)
        {
            if (!_sceneStatePending)
            {
                _sceneStatePending = true;
                Dispatcher.UIThread.Post(() => { _sceneStatePending = false; PublishSceneState(_scene); });
            }
            return;
        }
        SetAndRaise(VisibleRangeProperty, ref _visibleRange, scene?.Range);
        SetAndRaise(PlotAreaProperty, ref _plotArea, scene?.Plot ?? default);
        if (scene is not null && ReferenceEquals(_scene, scene)) UpdateSelectionFromScene(scene);
        NotifyAutomationStateChanged();
        NotifyViewSynchronizationChanged();
    }

    private sealed class RenderingScope : IDisposable
    {
        private readonly CartesianChart _chart;
        private readonly bool _previous;
        public RenderingScope(CartesianChart chart) { _chart = chart; _previous = chart._rendering; chart._rendering = true; }
        public void Dispose() => _chart._rendering = _previous;
    }

    private ChartAxis ResolveAxis(string? key, ChartAxis fallback)
    {
        if (string.IsNullOrEmpty(key)) return fallback;
        var matches = Axes.Append(XAxis).Append(YAxis).Distinct().Where(axis => axis.Key == key).ToArray();
        return matches.Length == 1 ? matches[0] : throw new InvalidOperationException($"Axis key '{key}' must identify exactly one axis.");
    }

    private static ChartScale CreateViewScale(ChartAxis axis, double minimum, double maximum, IReadOnlyList<string>? labels = null) =>
        ChartScale.CreateForView(axis, minimum, maximum, labels);

    private static ChartScale SliceScale(ChartAxis axis, ChartScale target, ChartScale primary, double minimum, double maximum,
        IReadOnlyList<string>? labels = null)
    {
        var first = axis.ViewMinimum ?? AscendingValue(target, AscendingFraction(primary, minimum));
        var last = axis.ViewMaximum ?? AscendingValue(target, AscendingFraction(primary, maximum));
        var constrained = ConstrainAxisView(axis, target, first, last);
        return CreateViewScale(axis, constrained.Minimum, constrained.Maximum, labels);
    }

    private static ChartScale BuildScale(ChartAxis axis, SeriesSnapshot[] snapshots, bool horizontal, IReadOnlyList<string>? labels = null)
    {
        var values = new List<double>();
        foreach (var snapshot in snapshots)
        {
            if (ReferenceEquals(snapshot.SecondaryAxis, axis))
            { values.Add(0); values.Add(100); continue; }
            if (snapshot.Series.Renderer.GetBounds(snapshot.Series, snapshot.Data) is { IsValid: true } bounds)
            {
                values.Add(horizontal ? bounds.XMinimum : bounds.YMinimum);
                values.Add(horizontal ? bounds.XMaximum : bounds.YMaximum);
            }
            if (snapshot.Series.Renderer is ChartStatisticalSeriesRenderer)
                values.AddRange(ChartStatisticalSeriesRenderer.AxisValues(snapshot.Series, snapshot.Data, horizontal));
            if (snapshot.Series.Renderer is ChartFinancialSeriesRenderer)
                values.AddRange(ChartFinancialSeriesRenderer.AxisValues(snapshot.Series, snapshot.Data, horizontal));
            if (!horizontal && snapshot.Series is AreaSeries or IChartRangeSeries ||
                snapshot.Series is IChartComparisonSeries && ReferenceEquals(ComparisonValueAxis(snapshot), axis))
            {
                // A domain-invalid baseline (e.g. zero on a log axis) must not
                // erase the smallest positive upper sample from automatic bounds.
                if (ChartDataExtrema.For(snapshot.Data).Bounds is { } upperBounds)
                { values.Add(horizontal ? upperBounds.XMinimum : upperBounds.YMinimum); values.Add(horizontal ? upperBounds.XMaximum : upperBounds.YMaximum); }
                if (ChartLowerExtrema.For(snapshot.Data).ForAxis(axis) is { } lower)
                { values.Add(lower.Minimum); values.Add(lower.Maximum); }
            }
        }
        var valid = values.Where(axis.IsValidValue).ToArray();
        var categorySeries = snapshots.Where(item => item.Series.Renderer.UsesCategorySlots && ReferenceEquals(ComparisonCategoryAxis(item), axis)).ToArray();
        if (axis is CategoryAxis category && valid.Length > 0 &&
            categorySeries.Any(item => (item.Series is BoxPlotSeries or ErrorBarSeries) && ChartDataExtrema.For(item.Data).Bounds is not null))
        {
            // Statistical boxes and fixed-DIP error caps occupy a category slot
            // on both sides of its tick. Supplement OnTicks only as needed;
            // Create still applies Gap/BetweenTicks and honors explicit bounds.
            var suppliedPadding = category.Gap + (category.PlotMode == CategoryPlotMode.BetweenTicks ? .5 : 0);
            var slotPadding = Math.Max(0, .5 - suppliedPadding);
            if (slotPadding > 0)
                return ChartScale.Create(valid.Min() - slotPadding, valid.Max() + slotPadding,
                    axis, paddingFraction: 0, categoryLabels: labels);
        }
        if (axis is not CategoryAxis && categorySeries.Length > 0 && valid.Length > 0)
        {
            var ordered = categorySeries.SelectMany(item => item.Points)
                .Where(point => IsValid(point) && axis.IsValidValue(horizontal ? point.X : point.Y)).Select(point => horizontal ? point.X : point.Y).Distinct().Order().ToArray();
            if (ordered.Length == 0) return ChartScale.FromValues(valid, axis, paddingFraction: 0, categoryLabels: labels);
            var step = ordered.Length > 1 ? ordered.Zip(ordered.Skip(1), (a, b) => b - a).Where(double.IsFinite).DefaultIfEmpty(1).Min() : 1;
            var min = valid.Min() - step * .55; var max = valid.Max() + step * .55;
            return ChartScale.Create(min, max, axis, paddingFraction: 0, categoryLabels: labels);
        }
        var includeZero = axis.IsValidValue(0) && snapshots.Any(item => ReferenceEquals(ComparisonValueAxis(item), axis) &&
            (item.Series.Renderer.IncludesZero || item.Series is IChartLineSeries { StackMode: not ChartStackMode.None }));
        var percentOnly = snapshots.Length > 0 && snapshots.All(item => ReferenceEquals(item.SecondaryAxis, axis) ||
            ReferenceEquals(ComparisonValueAxis(item), axis) && (item.Series is IChartLineSeries { StackMode: ChartStackMode.Percent } or BarSeries { StackMode: ChartStackMode.Percent }));
        return ChartScale.FromValues(valid, axis, includeZero: includeZero,
            paddingFraction: percentOnly ? 0 : horizontal ? .02 : .05, categoryLabels: labels);
    }

    private static double GetBarGroupWidth(SeriesSnapshot[] bars, ChartScale scale, Rect plot)
    {
        var positions = bars.SelectMany(item => item.Points).Where(point => IsValid(point) && scale.IsValidValue(point.X))
            .Select(point => scale.Map(point.X, plot.Left, plot.Right)).Distinct().Order().ToArray();
        var spacing = positions.Length < 2 ? plot.Width :
            positions.Zip(positions.Skip(1), (a, b) => b - a).Where(value => value > 0).DefaultIfEmpty(plot.Width).Min();
        return Math.Max(1, Math.Min(120, spacing * 0.72));
    }

    private static bool IsValid(ChartPoint point) => double.IsFinite(point.X) && double.IsFinite(point.Y);

    private FormattedText MakeText(string text, double? size = null, FontWeight? weight = null) => new(
        text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
        new Typeface(FontFamily, FontStyle, weight ?? FontWeight), size ?? FontSize, Foreground ?? Brushes.SlateGray);

    private void DrawLabel(DrawingContext context, string text, Point origin, double width,
        double? size = null, FontWeight? weight = null, TextAlignment alignment = TextAlignment.Left)
    {
        if (width <= 0) return;
        var formatted = MakeText(text, size, weight);
        var x = alignment switch
        {
            TextAlignment.Right => origin.X + width - formatted.Width,
            TextAlignment.Center => origin.X + (width - formatted.Width) / 2,
            _ => origin.X,
        };
        using var clip = context.PushClip(new Rect(origin.X, origin.Y, width, formatted.Height + 2));
        context.DrawText(formatted, new Point(x, origin.Y));
    }

    private sealed class SeriesSnapshot(ChartSeries series, ChartDataSnapshot data, IBrush brush, int index, ChartAxis xAxis, ChartAxis yAxis)
    {
        public ChartSeries Series { get; } = series;
        public ChartDataSnapshot Data { get; set; } = data;
        public IEnumerable<ChartPoint> Points
        {
            get
            {
                var count = Data.Count;
                for (var index = 0; index < count; index++) yield return Data.GetValue(index).Point;
            }
        }
        public IBrush Brush { get; } = brush;
        public int Index { get; } = index;
        public ChartAxis XAxis { get; } = xAxis;
        public ChartAxis YAxis { get; } = yAxis;
        public ChartAxis? SecondaryAxis { get; set; }
        public ChartSeriesRenderContext? Context { get; set; }
        public Geometry? BreakClip { get; set; }
    }
    internal sealed class CartesianProjection : IChartSeparableProjection
    {
        private readonly Rect plot;
        private readonly ChartScale xScale;
        private readonly ChartScale yScale;
        private readonly ChartDataSnapshot? _linearSource;
        private readonly InRangeLinearProjection? _linearProjection;
        private readonly bool _allPointsFinite;
        private readonly bool _isInRange;

        public CartesianProjection(Rect plot, ChartScale xScale, ChartScale yScale, ChartDataSnapshot? data = null)
        {
            this.plot = plot; this.xScale = xScale; this.yScale = yScale;
            _linearSource = data;
            _linearProjection = CreateLinearProjection(plot, xScale, yScale, data, out _allPointsFinite, out _isInRange);
        }

        internal InRangeLinearProjection? LinearProjection => _allPointsFinite && _isInRange ? _linearProjection : null;
        internal bool HasStableTransform => xScale.HasStandardTransform && yScale.HasStandardTransform;
        internal ChartScale XScale => xScale;
        internal ChartScale YScale => yScale;
        internal bool HasMonotonicY => yScale.HasStandardTransform;

        internal bool TryGetLinearLineProjection(ChartDataSnapshot snapshot, out InRangeLinearProjection projection)
        {
            if (ReferenceEquals(snapshot, _linearSource) && _allPointsFinite && _linearProjection is { } linear)
            { projection = linear; return true; }
            projection = default;
            return false;
        }

        internal bool TryGetLinearProjection(ChartDataSnapshot snapshot, out InRangeLinearProjection projection)
        {
            if (ReferenceEquals(snapshot, _linearSource) && LinearProjection is { } linear)
            { projection = linear; return true; }
            projection = default;
            return false;
        }

        internal bool TryGetGappedLinearProjection(ChartDataSnapshot snapshot, out InRangeLinearProjection projection)
        {
            if (ReferenceEquals(snapshot, _linearSource) && _isInRange && !_allPointsFinite && _linearProjection is { } linear)
            { projection = linear; return true; }
            projection = default;
            return false;
        }

        private static InRangeLinearProjection? CreateLinearProjection(Rect plot, ChartScale xScale, ChartScale yScale,
            ChartDataSnapshot? data, out bool allPointsFinite, out bool inRange)
        {
            allPointsFinite = false;
            inRange = false;
            // Never build an index or scan a snapshot to choose a rendering path.
            // Scene bounds preparation has already indexed built-in series.
            if (data is null || data.Count == 0 || data.Coordinates.Length != data.Count ||
                !ChartDataExtrema.TryGetExisting(data, out var index) || index is null || index.Count == 0 ||
                index.Bounds is not { } bounds)
                return null;
            var xInRange = xScale.TryCreateInRangeLinearMapping(bounds.XMinimum, bounds.XMaximum, plot.Left, plot.Right, out var x);
            var yInRange = yScale.TryCreateInRangeLinearMapping(bounds.YMinimum, bounds.YMaximum, plot.Bottom, plot.Top, out var y);
            inRange = xInRange && yInRange;
            // Every scene uses the same proof entry, including an initial view
            // whose data later grows outside its axes. Keep the strict result
            // separately for marker, bar and gapped-line dispatch.
            if (!xScale.TryCreateBoundedLinearMapping(bounds.XMinimum, bounds.XMaximum, plot.Left, plot.Right, out x) ||
                !yScale.TryCreateBoundedLinearMapping(bounds.YMinimum, bounds.YMaximum, plot.Bottom, plot.Top, out y))
                return null;
            allPointsFinite = index.Count == data.Count;
            return new(x, y);
        }

        public Rect PlotArea => plot;
        public bool HasMonotonicX => xScale.HasStandardTransform;
        public double MapX(double value) => xScale.IsValidValue(value) ? xScale.Map(value, plot.Left, plot.Right) : double.NaN;
        public double MapY(double value) => yScale.IsValidValue(value) ? yScale.Map(value, plot.Bottom, plot.Top) : double.NaN;
        public double YOrigin => yScale.Map(yScale.IsValidValue(0) ? 0 : yScale.Minimum, plot.Bottom, plot.Top);
        public Point Project(ChartPoint point) => new(Saturate(MapX(point.X)), Saturate(MapY(point.Y)));
        public ChartPoint Unproject(Point position) => new(xScale.Denormalize((position.X - plot.Left) / plot.Width),
            yScale.Denormalize((plot.Bottom - position.Y) / plot.Height));
        private static double Saturate(double value) => double.IsFinite(value) ? Math.Clamp(value, -1_000_000, 1_000_000) : value;
    }
    private sealed record ChartScene(double Left, double Right, Point? TitleOrigin, Point? YTitleOrigin,
        IReadOnlyList<ChartLegendItem> Legend, Rect Plot, ChartScale XScale, ChartScale YScale,
        ChartViewport Extent, ChartViewport Range, SeriesSnapshot[] Snapshots, bool HasPoints,
        ChartAxis[] ExtraXAxes, ChartAxis[] ExtraYAxes, IReadOnlyDictionary<ChartAxis, ChartScale> Scales, ChartScale BaseX, ChartScale BaseY,
        IReadOnlyDictionary<ChartAxis, double> AxisWidths, IReadOnlyDictionary<ChartAxis, ChartAxisLayout> Layouts);
}
