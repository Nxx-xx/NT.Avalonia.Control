using System.Globalization;
using System.Diagnostics;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace Charts.PlatformAcceptance;

internal sealed partial class AcceptanceWindow
{
    private sealed record Datum(string Id, double X, double Y);
    private sealed record Stage(string Id, string Label, double Value, string Column, string Row);
    private readonly Datum[] _points = [new("A", 1, 20), new("B", 2, 60), new("C", 3, 80)];
    private readonly Stage[] _stages = [new("a", "中文获客 · acquisition", 20, "产品甲", "线上"), new("b", "转化 😀 · conversion", 50, "产品甲", "门店"),
        new("c", "组合 e\u0301 · مبيعات", 30, "产品乙", "线上"), new("d", "原始聚合成员", 10, "产品甲", "线上")];
    private readonly Dictionary<string, ChartDataLabelContext> _dateLabelContexts = new(StringComparer.Ordinal);

    private void BuildScenes()
    {
        _cartesian = new CartesianChart { Name = "CartesianOriginalData", Title = "多轴原始数据 · 中文 / e\u0301 / 😀 / مبيعات", ShowLegend = true,
            XAxis = new NumericAxis { Key = "primary-x", Minimum = 0, Maximum = 4, Title = "业务时间坐标 / X", LabelFormat = "0.0" },
            YAxis = new NumericAxis { Key = "primary-y", Minimum = 0, Maximum = 100, Title = "原始值 / Y", LabelFormat = "0.0" },
            IsPointBrowsingEnabled = true, IsKeyboardNavigationEnabled = true, IsZoomEnabled = true, IsPanEnabled = true,
            ShowCrosshair = true, ShowCrosshairLabels = true, FlowDirection = CultureInfo.CurrentCulture.TextInfo.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight };
        _cartesian.Series.Add(new ScatterSeries { Title = "原始 A / B / C", ItemsSource = _points, IdPath = nameof(Datum.Id), CategoryPath = nameof(Datum.X), ValuePath = nameof(Datum.Y),
            MarkerSize = 15, Fill = Brushes.DodgerBlue, ShowDataLabels = true, DataLabelFormatter = context => ((Datum)context.Data.Item!).Id });
        _cartesian.Axes.Add(new LogarithmicAxis { Key = "log", Minimum = 1, Maximum = 1000, Position = ChartAxisPosition.Far, Title = "独立对数轴", ShowGridLines = false });
        _cartesian.Series.Add(new LineSeries { Title = "独立 log 系列", YAxisKey = "log", ItemsSource = new ChartPoint[] { new(1, 10), new(3, 1000) }, MarkerSize = 0, Stroke = Brushes.OrangeRed });
        _cartesian.PlotAreaContent = new object();
        _cartesian.PlotAreaContentTemplate = new FuncDataTemplate<ChartPlotAreaContext>((context, _) =>
        { _cartesianContext = context; _cartesianCaption = Caption(_cartesian, "永久业务模板 · 中文 e\u0301 😀"); return PermanentCaption(_cartesianCaption); });
        _cartesian.PropertyChanged += (_, change) =>
        {
            if (!_externalPhase) return;
            if (change.Property == CartesianChart.SelectedPointProperty && _cartesian.SelectedPoint?.StableId?.ToString() is { } selected) _observedSelectedIds.Add(selected);
            if (change.Property == CartesianChart.CurrentPointProperty && _cartesian.CurrentPoint?.StableId?.ToString() is { } current) _observedBrowsingIds.Add(current);
        };
        _cartesian.ViewportChanged += (_, _) => { if (_externalPhase) { _panObserved |= _leftDragActive; RecordEvent("cartesian-viewport-changed", new { leftDragActive = _leftDragActive, viewport = _cartesian.VisibleRange }); } };
        _range = new ChartRangeSelector { Name = "CartesianRangeWindow", Chart = _cartesian, Axis = ChartInteractionAxes.X, MinimumSpan = .05, Height = 36, Margin = new Thickness(10, 0) };
        _range.RangeChanged += (_, args) => { if (_externalPhase) { if (args.IsUserInitiated) _userRangeChanges++; RecordEvent("range-changed", new { oldRange = args.OldRange, newRange = args.NewRange, args.IsUserInitiated }); } };

        var dateAxis = new DateTimeAxis { Minimum = 1_709_251_200_000, Maximum = 1_709_424_000_000, IntervalUnit = DateTimeIntervalUnit.Day, Interval = 1,
            DateFormat = "u", Title = "UTC 日期 · 不受本地时区移动", LabelRotation = -20, LabelMaxWidth = 150 };
        _dates = new CartesianChart { Name = "CultureUtcDates", Title = "UTC 日期 / 文化数字 / RTL 文字", XAxis = dateAxis,
            YAxis = new NumericAxis { Minimum = 0, Maximum = 100, LabelFormat = "0.0", Title = "小数文化" }, ShowLegend = true,
            FlowDirection = _cartesian.FlowDirection };
        _dates.Series.Add(new LineSeries { Title = "2024-03-01 / 02 / 03 UTC", ItemsSource = new Datum[] { new("D0", 1_709_251_200_000, 20), new("D1", 1_709_337_600_000, 62.5), new("D2", 1_709_424_000_000, 80) },
            IdPath = nameof(Datum.Id), CategoryPath = nameof(Datum.X), ValuePath = nameof(Datum.Y), MarkerSize = 12, Stroke = Brushes.Teal,
            ShowDataLabels = true, DataLabelOverlap = ChartDataLabelOverlap.Allow,
            DataLabelFormatter = context =>
            {
                if (context.Data.StableId?.ToString() is { } id) _dateLabelContexts[id] = context;
                return context.FormattedX + " · " + context.FormattedY;
            } });
        _dates.PlotAreaContent = new object();
        _dates.PlotAreaContentTemplate = new FuncDataTemplate<ChartPlotAreaContext>((context, _) => { _dateContext = context; return PermanentCaption(Caption(_dates, "مبيعات 123 · 中文 · e\u0301 · 😀")); });

        _funnel = new FunnelChart { Name = "ProfessionalFunnel", Title = "Funnel · 当前原始来源", ItemsSource = _stages, IdPath = nameof(Stage.Id), CategoryPath = nameof(Stage.Label), ValuePath = nameof(Stage.Value),
            ShowLabels = false, ShowValues = false, PlotAreaContent = new object(), TrackballMode = ChartTrackballMode.Vertical };
        _mosaic = new MosaicChart { Name = "ProfessionalMosaic", Title = "Mosaic · a/d 聚合，保持原始身份", ItemsSource = _stages, IdPath = nameof(Stage.Id), LabelPath = nameof(Stage.Label), ValuePath = nameof(Stage.Value),
            GroupPath = nameof(Stage.Column), SubGroupPath = nameof(Stage.Row), ShowLabels = false, ShowValues = false, PlotAreaContent = new object(), TrackballMode = ChartTrackballMode.Vertical };
        _funnel.PlotAreaContentTemplate = new FuncDataTemplate<ChartDataPlotAreaContext>((context, _) =>
        { _funnelContext = context; _funnelCaption = Caption(_funnel, "永久 Funnel · 中文 e\u0301"); return PermanentCaption(_funnelCaption); });
        _mosaic.PlotAreaContentTemplate = new FuncDataTemplate<ChartDataPlotAreaContext>((context, _) =>
        { _mosaicContext = context; _mosaicCaption = Caption(_mosaic, "永久 Mosaic · 中文 e\u0301"); return PermanentCaption(_mosaicCaption); });
        _funnel.LegendItemTemplate = new FuncDataTemplate<ChartDataLegendItemContext>((context, _) => Caption(_funnel, context!.Text));
        _mosaic.LegendItemTemplate = new FuncDataTemplate<ChartDataLegendItemContext>((context, _) => Caption(_mosaic, context!.Text));
        var contrast = _options.Theme switch { "hc-light" => ChartHighContrastMode.Light, "hc-dark" => ChartHighContrastMode.Dark, _ => ChartHighContrastMode.None };
        foreach (var chart in new ChartBase[] { _cartesian, _dates, _funnel, _mosaic }) chart.HighContrastMode = contrast;
    }

    private static TextBlock Caption(Control chart, string text)
    {
        var caption = new TextBlock { Text = text, FontSize = 14, Margin = new Thickness(10), MaxWidth = 210, MaxHeight = 40,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, TextWrapping = TextWrapping.Wrap,
            IsHitTestVisible = false };
        caption.Bind(TextBlock.ForegroundProperty, new Binding("Foreground") { Source = chart });
        return caption;
    }
    private Border PermanentCaption(TextBlock caption) => new()
    {
        Background = Background, Child = caption, HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false,
    };

    private async Task RunProgrammaticScenesAsync()
    {
        _body.Children.Add(_cartesian);
        await SettleAsync();
        await VerifyCartesianAsync("desktop-main", 1280, 800);
        var initialSize = ClientSize;
        Width = 640; Height = 360; await SettleAsync();
        await VerifyCartesianAsync("desktop-small", 640, 360);
        var smallSize = ClientSize;
        Check("native-resize-small-client-actually-changed", smallSize.Width < initialSize.Width - 10 || smallSize.Height < initialSize.Height - 10);
        Width = 1280; Height = 800; await SettleAsync();
        await VerifyCartesianAsync("desktop-restored", 1280, 800);
        Check("native-resize-restored-client-actually-changed", ClientSize.Width > smallSize.Width + 10 || ClientSize.Height > smallSize.Height + 10);
        await VerifyLogicalDisplayMigrationAsync();
        await VerifyCultureDatesAsync();
        await VerifyProfessionalAsync();
        await VerifyNativeGlyphOrientationAsync();
    }

    private async Task VerifyLogicalDisplayMigrationAsync()
    {
        var screens = Screens.All.ToArray();
        var originalScreen = Screens.ScreenFromWindow(this);
        var originalPosition = Position; var originalWidth = Width; var originalHeight = Height;
        var originalPointToScreen = this.PointToScreen(default);
        var originalClientCenter = this.PointToScreen(new Point(ClientSize.Width / 2, ClientSize.Height / 2));
        var originalClientSize = ClientSize; var originalRenderScaling = RenderScaling;
        var originalScreenBounds = originalScreen is null ? null : new[] { originalScreen.Bounds.X, originalScreen.Bounds.Y, originalScreen.Bounds.Width, originalScreen.Bounds.Height };
        var originalViewport = _cartesian.Viewport; var originalSelection = _cartesian.SelectedPoint;
        var targets = screens.Select((screen, index) => (screen, index)).Where(pair => originalScreen is not null && pair.screen.Bounds != originalScreen.Bounds).ToArray();
        if (targets.Length == 0)
        {
            _suite.Observations.Add(new { kind = "logical-display-migration-capability", status = "not-applicable-single-logical-screen", logicalScreenCount = screens.Length,
                executedTargets = 0, originalPositionPixels = PP(originalPosition), originalPointToScreenOrigin = PP(originalPointToScreen), originalScreenBounds,
                originalClientOriginScreenPixels = PP(originalPointToScreen), originalClientCenterScreenPixels = PP(originalClientCenter),
                originalClientDips = S(originalClientSize), originalRenderScaling,
                restoredPositionPixels = PP(Position), restoredScreenBounds = originalScreenBounds,
                restoredClientOriginScreenPixels = PP(this.PointToScreen(default)), restoredClientCenterScreenPixels = PP(this.PointToScreen(new Point(ClientSize.Width / 2, ClientSize.Height / 2))),
                restoredClientDips = S(ClientSize), restoredRenderScaling = RenderScaling,
                physicalCrossMonitorCertified = false, mixedDpiCertified = false });
            return;
        }
        var executed = 0;
        var placementTrace = new List<object>();
        var placementPhase = "migration-origin";
        PixelPoint? currentRequest = null;
        var calibrationX = 0; var calibrationY = 0; var hasAcceptedCalibration = false;
        void RecordPlacement(string kind, PixelPoint? eventPosition = null)
        {
            if (placementTrace.Count >= 512) return;
            var currentScreen = Screens.ScreenFromWindow(this);
            placementTrace.Add(new { sequence = placementTrace.Count + 1, utc = DateTimeOffset.UtcNow, monotonicTimestamp = Stopwatch.GetTimestamp(),
                kind, phase = placementPhase, requestedPositionPixels = currentRequest is { } request ? PP(request) : null,
                eventPositionPixels = eventPosition is { } point ? PP(point) : null, actualPositionPixels = PP(Position),
                actualPointToScreenOrigin = PP(this.PointToScreen(default)), actualClientDips = S(ClientSize), actualRenderScaling = RenderScaling,
                actualClientCenterScreenPixels = PP(this.PointToScreen(new Point(ClientSize.Width / 2, ClientSize.Height / 2))),
                actualScreenBounds = currentScreen is null ? null : new[] { currentScreen.Bounds.X, currentScreen.Bounds.Y, currentScreen.Bounds.Width, currentScreen.Bounds.Height } });
        }
        async Task WaitForPlacementAsync(string phase, PixelRect? expectedScreen)
        {
            placementPhase = phase; var elapsed = Stopwatch.StartNew();
            var lastPosition = Position; var lastSize = ClientSize; var lastScale = RenderScaling; var stable = 0;
            for (var sample = 0; sample < 16; sample++)
            {
                await SettleAsync();
                var currentPosition = Position; var size = ClientSize; var scale = RenderScaling;
                stable = currentPosition == lastPosition && size == lastSize && scale == lastScale ? stable + 1 : 0;
                RecordPlacement("bounded-native-placement-sample");
                var actualScreen = Screens.ScreenFromWindow(this)?.Bounds;
                if (stable >= 3 && elapsed.ElapsedMilliseconds >= 250 && (expectedScreen is null || actualScreen == expectedScreen)) break;
                lastPosition = currentPosition; lastSize = size; lastScale = scale;
            }
        }
        void NativePositionChanged(object? sender, PixelPointEventArgs args) => RecordPlacement("native-PositionChanged-event", args.Point);
        PositionChanged += NativePositionChanged;
        RecordPlacement("original-placement-observed");
        try
        {
            foreach (var (screen, index) in targets)
            {
                var scaling = screen.Scaling;
                if (!(scaling > 0) || screen.WorkingArea.Width < (MinWidth + 30) * scaling || screen.WorkingArea.Height < (MinHeight + 30) * scaling)
                {
                    _suite.Observations.Add(new { kind = "logical-display-migration-target", targetIndex = index, status = "blocked-target-working-area-too-small",
                        expectedBounds = new[] { screen.Bounds.X, screen.Bounds.Y, screen.Bounds.Width, screen.Bounds.Height }, reportedScreenScaling = scaling,
                        physicalCrossMonitorCertified = false, mixedDpiCertified = false });
                    continue;
                }
                executed++;
                Width = Math.Min(640, screen.WorkingArea.Width / scaling - 40);
                Height = Math.Min(360, screen.WorkingArea.Height / scaling - 40);
                _cartesian.Viewport = new(.5, 3.5, 10, 90);
                await SettleAsync();
                var beforeContext = _cartesianCaption.DataContext as ChartPlotAreaContext;
                var beforePlot = _cartesian.PlotArea;
                var beforePoint = beforeContext?.Project(2, 60) ?? default;
                _cartesian.SelectedPoint = _cartesian.HitTestPoint(new(beforePlot.Left + beforePoint.X, beforePlot.Top + beforePoint.Y), .1);
                var viewportBefore = _cartesian.VisibleRange;
                currentRequest = new PixelPoint(screen.WorkingArea.X + 12, screen.WorkingArea.Y + 12);
                placementPhase = "logical-display-" + index + "-target-request";
                RecordPlacement("position-request-about-to-apply");
                Position = currentRequest.Value;
                await WaitForPlacementAsync("logical-display-" + index + "-target-response", screen.Bounds);
                var observedScreen = Screens.ScreenFromWindow(this);
                if (observedScreen?.Bounds == screen.Bounds)
                {
                    calibrationX = Position.X - currentRequest.Value.X; calibrationY = Position.Y - currentRequest.Value.Y;
                    hasAcceptedCalibration = true;
                    RecordPlacement("accepted-target-getter-minus-request-offset-observed");
                }
                var context = _cartesianCaption.DataContext as ChartPlotAreaContext;
                var plot = _cartesian.PlotArea;
                var point = context?.Project(2, 60) ?? default;
                var hit = _cartesian.HitTestPoint(new(plot.Left + point.X, plot.Top + point.Y), .1);
                var prefix = "logical-display-" + index;
                Check(prefix + "-target-screen-observed", observedScreen?.Bounds == screen.Bounds);
                Check(prefix + "-native-scale-and-client-valid", double.IsFinite(RenderScaling) && RenderScaling > 0 && ClientSize.Width > 0 && ClientSize.Height > 0 && plot.Width > 20 && plot.Height > 20);
                Check(prefix + "-current-original-B-hit-and-selection", hit is { StableId: "B", Index: 1 } && ReferenceEquals(hit.Item, _points[1]) && _cartesian.SelectedPoint is { StableId: "B", Index: 1 });
                Check(prefix + "-existing-viewport-preserved", _cartesian.VisibleRange == viewportBefore);
                CaptureWindowRaster(prefix);
                _suite.Observations.Add(new { kind = "logical-display-migration-target", targetIndex = index, status = "executed", checkPrefix = prefix,
                    logicalScreenCount = screens.Length, expectedBounds = new[] { screen.Bounds.X, screen.Bounds.Y, screen.Bounds.Width, screen.Bounds.Height },
                    expectedWorkingArea = new[] { screen.WorkingArea.X, screen.WorkingArea.Y, screen.WorkingArea.Width, screen.WorkingArea.Height }, reportedScreenScaling = scaling,
                    observedBounds = observedScreen is null ? null : new[] { observedScreen.Bounds.X, observedScreen.Bounds.Y, observedScreen.Bounds.Width, observedScreen.Bounds.Height },
                    actualWindowPositionPixels = PP(Position), actualRenderScaling = RenderScaling, actualClientDips = S(ClientSize), plotDips = R(plot),
                    requestedTargetPositionPixels = PP(currentRequest.Value), getterMinusRequestedOffsetPixels = new[] { Position.X - currentRequest.Value.X, Position.Y - currentRequest.Value.Y },
                    viewportBefore, viewportAfter = _cartesian.VisibleRange, actualHit = Hit(hit), selectedId = _cartesian.SelectedPoint?.StableId?.ToString(),
                    qualification = "Programmatic native-window migration across reported logical display topology, which may include virtual displays. No physical or mixed-DPI certification.",
                    physicalCrossMonitorCertified = false, mixedDpiCertified = false });
            }
        }
        finally
        {
            try
            {
                if (executed > 0)
                {
                    // Native window managers can report decorated frame positions
                    // while accepting a different client/configure request origin.
                    // Restore size first, then use measured getter/request feedback;
                    // the final assertion still requires the exact original getter.
                    placementPhase = "restore-size"; currentRequest = null;
                    Width = originalWidth; Height = originalHeight;
                    _cartesian.Viewport = originalViewport; _cartesian.SelectedPoint = originalSelection;
                    await WaitForPlacementAsync("restore-size-stable", null);
                    var request = new PixelPoint(checked(originalPosition.X - (hasAcceptedCalibration ? calibrationX : 0)),
                        checked(originalPosition.Y - (hasAcceptedCalibration ? calibrationY : 0)));
                    for (var attempt = 0; attempt < 6; attempt++)
                    {
                        currentRequest = request; placementPhase = "restore-position-attempt-" + attempt;
                        RecordPlacement("restore-position-request-about-to-apply");
                        Position = request;
                        await WaitForPlacementAsync("restore-position-response-" + attempt, originalScreen?.Bounds);
                        if (Position == originalPosition && Screens.ScreenFromWindow(this)?.Bounds == originalScreen?.Bounds) break;
                        var errorX = originalPosition.X - Position.X; var errorY = originalPosition.Y - Position.Y;
                        request = new PixelPoint(checked(request.X + errorX), checked(request.Y + errorY));
                        RecordPlacement("exact-restore-feedback-error-observed");
                    }
                    RecordPlacement("restore-bounded-feedback-finished");
                }
            }
            finally { PositionChanged -= NativePositionChanged; }
        }
        if (executed > 0)
        {
            Check("logical-display-original-position-restored", Position == originalPosition && Screens.ScreenFromWindow(this)?.Bounds == originalScreen?.Bounds);
            Check("logical-display-original-view-and-selection-restored", _cartesian.Viewport == originalViewport && Equals(_cartesian.SelectedPoint?.StableId, originalSelection?.StableId));
        }
        var restoredScreen = Screens.ScreenFromWindow(this);
        _suite.Observations.Add(new { kind = "logical-display-migration-capability", status = executed > 0 ? "executed-logical-topology-only" : "blocked-no-target-workarea",
            logicalScreenCount = screens.Length, executedTargets = executed, originalPositionPixels = PP(originalPosition), restoredPositionPixels = PP(Position),
            originalPointToScreenOrigin = PP(originalPointToScreen), restoredPointToScreenOrigin = PP(this.PointToScreen(default)), originalScreenBounds,
            originalClientOriginScreenPixels = PP(originalPointToScreen), originalClientCenterScreenPixels = PP(originalClientCenter), originalClientDips = S(originalClientSize), originalRenderScaling,
            restoredScreenBounds = restoredScreen is null ? null : new[] { restoredScreen.Bounds.X, restoredScreen.Bounds.Y, restoredScreen.Bounds.Width, restoredScreen.Bounds.Height },
            restoredClientOriginScreenPixels = PP(this.PointToScreen(default)), restoredClientCenterScreenPixels = PP(this.PointToScreen(new Point(ClientSize.Width / 2, ClientSize.Height / 2))),
            restoredClientDips = S(ClientSize), restoredRenderScaling = RenderScaling,
            observedGetterMinusRequestCalibrationPixels = hasAcceptedCalibration ? new[] { calibrationX, calibrationY } : null,
            placementTrace, restoreStrategy = "Restore size first; use actual accepted getter/request offset then at most six bounded native-feedback requests. Exact original Position and screen remain mandatory.",
            physicalCrossMonitorCertified = false, mixedDpiCertified = false });
    }

    private async Task VerifyCartesianAsync(string scene, double requestedWidth, double requestedHeight)
    {
        await SettleAsync();
        // Content templates reuse controls: their public DataContext carries the
        // new immutable projection after window/axis/view changes.
        var context = _cartesianCaption.DataContext as ChartPlotAreaContext;
        _cartesianContext = context;
        var plot = _cartesian.PlotArea;
        Check(scene + "-actual-window-client-valid", double.IsFinite(ClientSize.Width) && double.IsFinite(ClientSize.Height) && ClientSize.Width > 0 && ClientSize.Height > 0);
        Check(scene + "-native-scale-finite-positive", double.IsFinite(RenderScaling) && RenderScaling > 0);
        Check(scene + "-plot-positive-contained", Finite(plot) && plot.Width > 20 && plot.Height > 20 && new Rect(_cartesian.Bounds.Size).Contains(plot));
        Check(scene + "-template-current-plot", context is not null && context.PlotArea == plot);
        var projected = context?.Project(2, 60) ?? default;
        var expected = new Point(plot.Width / 2, plot.Height * .4);
        Check(scene + "-primary-projection-independent-coordinate", Near(projected, expected, .02));
        var localPoint = new Point(plot.Left + projected.X, plot.Top + projected.Y);
        var hit = _cartesian.HitTestPoint(localPoint, .1);
        Check(scene + "-original-B-hit", hit is { Index: 1, StableId: "B" } && ReferenceEquals(hit.Item, _points[1]) && hit.Point == new ChartPoint(2, 60));
        var hasLog = context is not null && context.TryGetProjection("primary-x", "log", out var secondary);
        Check(scene + "-named-log-projection", hasLog);
        if (context is not null && context.TryGetProjection("primary-x", "log", out var log))
        {
            Check(scene + "-log-coordinate-independent", Near(log.Project(new(2, 100)), new(plot.Width / 2, plot.Height / 3), .02));
            var back = log.Unproject(log.Project(new(2.5, 25)));
            Check(scene + "-log-roundtrip", Math.Abs(back.X - 2.5) < 1e-8 && Math.Abs(back.Y - 25) < 1e-6);
        }
        else { Check(scene + "-log-coordinate-independent", false); Check(scene + "-log-roundtrip", false); }
        _suite.Observations.Add(new { kind = "cartesian-geometry", scene, inputClassification = "public-api-programmatic-query", requestedWindowDips = new[] { requestedWidth, requestedHeight },
            actualClientDips = S(ClientSize), requestedWindowSizeAchieved = Math.Abs(ClientSize.Width - requestedWidth) <= 2 && Math.Abs(ClientSize.Height - requestedHeight) <= 2,
            windowManagerConstraintQualification = "Actual client dimensions are authoritative; requested dimensions can be constrained by the desktop work area.",
            actualRenderScaling = RenderScaling, actualChartBoundsDips = R(_cartesian.Bounds), plotDips = R(plot),
            projectedPrimaryPlotDips = P(projected), expectedPrimaryPlotDips = P(expected), actualHit = Hit(hit),
            originalSource = _points.Select(p => new { p.Id, p.X, p.Y }).ToArray(), flowDirection = _cartesian.FlowDirection.ToString(),
            compositorPresentValidated = false });
        CaptureWindowRaster(scene);
        await ExportResolutionCasesAsync(_cartesian, scene);
    }

    private async Task VerifyCultureDatesAsync()
    {
        _body.Children.Clear(); _body.Children.Add(_dates); await SettleAsync();
        _dateContext = (_dates.PlotAreaContent is not null ? _dates.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Text == "مبيعات 123 · 中文 · e\u0301 · 😀")?.DataContext : null) as ChartPlotAreaContext ?? _dateContext;
        CaptureWindowRaster("culture-utc-dates");
        var culture = CultureInfo.CurrentCulture;
        var ordered = _dateLabelContexts.Values.OrderBy(context => context.Data.SourceIndex).ToArray();
        var number = _dateLabelContexts.GetValueOrDefault("D1")?.FormattedY ?? string.Empty;
        var originalDomainMilliseconds = ordered.Select(context => context.Data.Point.X).ToArray();
        var labels = ordered.Select(context => context.FormattedX).ToArray();
        Check("culture-process-name", string.Equals(culture.Name, CultureInfo.GetCultureInfo(_options.Culture).Name, StringComparison.OrdinalIgnoreCase));
        Check("culture-decimal-separator", number == "62" + culture.NumberFormat.NumberDecimalSeparator + "5");
        Check("dates-UTC-fixed-original-domain", originalDomainMilliseconds.SequenceEqual(new double[] { 1_709_251_200_000, 1_709_337_600_000, 1_709_424_000_000 }));
        Check("dates-UTC-formatted-data-labels", labels.SequenceEqual(new[] { "2024-03-01 00:00:00Z", "2024-03-02 00:00:00Z", "2024-03-03 00:00:00Z" }));
        var plot = _dates.PlotArea;
        var projected = _dateContext?.Project(1_709_337_600_000, 62.5) ?? default;
        var hit = _dates.HitTestPoint(new Point(plot.Left + projected.X, plot.Top + projected.Y), .1);
        Check("dates-current-frame-original-hit", hit is { Index: 1, StableId: "D1" } && hit.Point.X == 1_709_337_600_000);
        Check("culture-RTL-direction-contract", _dates.FlowDirection == (culture.TextInfo.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight));
        _suite.Observations.Add(new { kind = "culture-and-UTC-dates", culture = culture.Name, calendar = culture.DateTimeFormat.Calendar.GetType().FullName,
            decimalSeparator = culture.NumberFormat.NumberDecimalSeparator, numericLabel = number, originalDomainMilliseconds, labels,
            formattedLabelSource = "actual public ChartDataLabelContext from current chart render; not an internal tick enumerator",
            knownUtcEpochValues = new long[] { 1_709_251_200_000, 1_709_337_600_000, 1_709_424_000_000 }, dateHit = Hit(hit),
            text = "中文 / e\u0301 / 😀 / مبيعات 123", flowDirection = _dates.FlowDirection.ToString(),
            shapingAndImeCertified = false, cultureDifferenceRequiresCrossCulturePixelEquality = false });
        await ExportResolutionCasesAsync(_dates, "culture-utc-dates");
        var unicode = await ExportArtifactAsync(_dates, Path.Combine("中文目录", "图表-é-e\u0301-😀.png"), false, true,
            new ChartExportOptions { PixelSize = new PixelSize(720, 360), Dpi = 144, Background = Background });
        Check("native-unicode-export-path", File.Exists(unicode.Path) && unicode.PixelWidth == 720 && unicode.PixelHeight == 360);
        _suite.Observations.Add(new { kind = "unicode-export-path", path = unicode.Path, codePoints = "中文目录/图表-é-e\u0301-😀.png".EnumerateRunes().Select(r => $"U+{r.Value:X}").ToArray(), normalizationChangedByDriver = false });
    }

    private async Task VerifyProfessionalAsync()
    {
        _body.Children.Clear();
        var group = new Grid { ColumnDefinitions = new("*,*"), ColumnSpacing = 16 };
        group.Children.Add(_funnel); Grid.SetColumn(_mosaic, 1); group.Children.Add(_mosaic); _body.Children.Add(group);
        await SettleAsync();
        var funnelMarks = _funnel.GetMarks(); var mosaicMarks = _mosaic.GetMarks();
        Check("professional-funnel-four-real-originals", funnelMarks.Select(mark => mark.Data.StableId?.ToString()).SequenceEqual(new[] { "a", "b", "c", "d" }));
        Check("professional-mosaic-complete-original-members", mosaicMarks.SelectMany(mark => mark.Members).Select(p => p.StableId?.ToString()).Order().SequenceEqual(new[] { "a", "b", "c", "d" }));
        var aggregate = mosaicMarks.FirstOrDefault(mark => mark.Members.Any(p => Equals(p.StableId, "a")));
        Check("professional-mosaic-real-aggregation", aggregate is not null && aggregate.Members.Select(p => p.StableId?.ToString()).Order().SequenceEqual(new[] { "a", "d" }));
        var funnelHit = funnelMarks.Count > 1 ? _funnel.HitTest(funnelMarks[1].Bounds.Center) : null;
        Check("professional-funnel-original-hit", funnelHit is { StableId: "b", Index: 1 } && ReferenceEquals(funnelHit.Item, _stages[1]));
        var mosaicHit = aggregate is null ? null : _mosaic.HitTest(aggregate.Bounds.Center);
        Check("professional-mosaic-hit-original-members", mosaicHit is not null && mosaicHit.Members.Select(p => p.StableId?.ToString()).Order().SequenceEqual(new[] { "a", "d" }));
        _funnel.SelectedDataKeys = new object[] { "b" }; _mosaic.SelectedDataKeys = new object[] { "a" }; await SettleAsync();
        Check("professional-selected-original-objects", _funnel.GetSelectedData().Count == 1 && ReferenceEquals(_funnel.SelectedData[0].Item, _stages[1]) &&
            _mosaic.GetSelectedData().Count == 1 && ReferenceEquals(_mosaic.SelectedData[0].Item, _stages[0]));
        _funnel.UpdateTrackball(funnelMarks[1].Bounds.Center); _mosaic.UpdateTrackball(aggregate!.Bounds.Center); await SettleAsync();
        Check("professional-trackball-original-members", _funnel.TrackballReadings.SelectMany(r => r.Members).Any(p => ReferenceEquals(p.Item, _stages[1])) &&
            _mosaic.TrackballReadings.SelectMany(r => r.Members).Any(p => ReferenceEquals(p.Item, _stages[0])));
        _funnelContext = _funnelCaption.DataContext as ChartDataPlotAreaContext; _mosaicContext = _mosaicCaption.DataContext as ChartDataPlotAreaContext;
        Check("professional-actual-template-frames", _funnelContext?.AnalysisMarks.Count == 4 && _mosaicContext?.EngineeringMarks.Count == 3);
        Check("professional-layout-projection-native-contract", _funnelContext is not null && _mosaicContext is not null &&
            Near(_funnelContext.ProjectLayout(.25, .75), new(_funnel.PlotArea.Width * .25, _funnel.PlotArea.Height * .75), .01) &&
            Near(_mosaicContext.UnprojectLayout(_mosaicContext.ProjectLayout(.3, .4)), new(.3, .4), 1e-8));
        _suite.Observations.Add(new { kind = "professional-native-frames", funnelPlot = R(_funnel.PlotArea), mosaicPlot = R(_mosaic.PlotArea),
            funnelMarks = funnelMarks.Select(mark => new { id = mark.Data.StableId?.ToString(), sourceIndex = mark.Data.SourceIndex, bounds = R(mark.Bounds), value = mark.Data.Point.Y }).ToArray(),
            mosaicMarks = mosaicMarks.Select(mark => new { members = mark.Members.Select(p => new { id = p.StableId?.ToString(), sourceIndex = p.SourceIndex }).ToArray(), bounds = R(mark.Bounds) }).ToArray(),
            funnelHit = Hit(funnelHit), mosaicHitMembers = mosaicHit?.Members.Select(p => p.StableId?.ToString()).ToArray(),
            inputClassification = "public-api-programmatic-query", physicalInputValidated = false });
        CaptureWindowRaster("professional-selected-trackball");
        await VerifyProfessionalExportsAsync(_funnel, _funnelCaption, "analysis");
        await VerifyProfessionalExportsAsync(_mosaic, _mosaicCaption, "engineering");
    }

    private async Task ShowInteractiveBoardAsync()
    {
        _body.Children.Clear();
        if (_funnel.GetVisualParent() is Panel funnelParent) funnelParent.Children.Remove(_funnel);
        if (_mosaic.GetVisualParent() is Panel mosaicParent) mosaicParent.Children.Remove(_mosaic);
        _cartesian.ResetViewport(); _cartesian.SelectedPoint = null;
        _funnel.TrackballMode = ChartTrackballMode.None; _mosaic.TrackballMode = ChartTrackballMode.None;
        _funnel.ClearDataSelection(); _mosaic.ClearDataSelection();
        var board = new Grid { ColumnDefinitions = new("3*,2*"), ColumnSpacing = 16 };
        var left = new Grid { RowDefinitions = new("*,Auto") }; left.Children.Add(_cartesian); Grid.SetRow(_range, 1); left.Children.Add(_range);
        var right = new Grid { RowDefinitions = new("*,*"), RowSpacing = 12 }; right.Children.Add(_funnel); Grid.SetRow(_mosaic, 1); right.Children.Add(_mosaic);
        board.Children.Add(left); Grid.SetColumn(right, 1); board.Children.Add(right); _body.Children.Add(board);
        await SettleAsync();
        _cartesianContext = _cartesianCaption.DataContext as ChartPlotAreaContext;
        _status.Text = "自动场景已执行。等待独立系统输入；实际鼠标/键盘事件与原始数据状态将分别保存。";
    }
}
