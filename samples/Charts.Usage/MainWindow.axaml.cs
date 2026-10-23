using System.Text.Json;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class MainWindow : Window
{
    private readonly ChartSynchronizationGroup _rangeSynchronization = new();
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        Model.ConfigureReadingTemplate((IDataTemplate)Resources["ReadingTooltipTemplate"]!);
        Model.ConfigureControlTemplates((IDataTemplate)Resources["ControlLegendTemplate"]!, (IDataTemplate)Resources["ControlDataLabelTemplate"]!);
        Model.ConfigureTrackballTemplate((IDataTemplate)Resources["TrackballBusinessTemplate"]!);
        ActualThemeVariantChanged += (_, _) => UpdateReadingTooltipResources();
        UpdateReadingTooltipResources();
        DataContext = Model;
        ConfigureBarComparisonLabels();
        ConfigureScatterBubbleLabels();
        ConfigureRenderResourceBindings();
        ConfigureFinancialBindings();
        ConfigureTechnicalTrendsBindings();
        _rangeSynchronization.Add(SelectionPrimaryChart); _rangeSynchronization.Add(SelectionSecondaryChart);
        Closed += (_, _) => _rangeSynchronization.Dispose();
        Closed += (_, _) => Model.DisposeStreaming();
        Closed += (_, _) => { foreach (var chart in RelationCharts) chart.Dispose(); };
        Closed += (_, _) => { foreach (var chart in MapCharts) chart.Dispose(); };
        Closed += (_, _) => DisposeC6Completion();
        Closed += (_, _) => { _exportClosed = true; _exportCancellation?.Cancel(); };
        for (var i = 4; i <= 16; i++)
            TemplateChart.Series.Add(new LineSeries { Title = $"候选渠道 {i}", IsVisible = false, Stroke = i % 2 == 0 ? Brushes.Teal : Brushes.DarkOrange,
                ItemsSource = new ChartPoint[] { new(1, 10 + i), new(3, 15 + i), new(5, 12 + i) } });
        if (Program.Arguments.Contains("--smoke", StringComparer.Ordinal)) Opened += async (_, _) => await RunSmokeAsync();
    }

    internal ChartUsageViewModel Model { get; } = new();
    internal CartesianChart BoundChart => this.FindControl<CartesianChart>("BoundSeriesChart")!;
    internal CartesianChart InheritedChart => this.FindControl<CartesianChart>("InheritedSeriesChart")!;
    internal CartesianChart ReadingChart => this.FindControl<CartesianChart>("TooltipReadingChart")!;
    internal CartesianChart TemplateChart => this.FindControl<CartesianChart>("ControlTemplateChart")!;
    internal CartesianChart TrackballChart => this.FindControl<CartesianChart>("UsageTrackballChart")!;
    internal CartesianChart SelectionPrimaryChart => this.FindControl<CartesianChart>("UsageSelectionPrimary")!;
    internal CartesianChart SelectionSecondaryChart => this.FindControl<CartesianChart>("UsageSelectionSecondary")!;
    internal CartesianChart AnnotationCompletionChart => this.FindControl<CartesianChart>("UsageAnnotationCompletion")!;
    internal CartesianChart AnimationChart => this.FindControl<CartesianChart>("UsageAnimationChart")!;
    internal CartesianChart ExportChart => this.FindControl<CartesianChart>("UsageExportChart")!;
    internal CartesianChart AccessibilityChart => this.FindControl<CartesianChart>("UsageAccessibilityChart")!;
    internal CartesianChart LineAreaChart => this.FindControl<CartesianChart>("UsageLineAreaChart")!;
    internal CartesianChart RangeAreaChart => this.FindControl<CartesianChart>("UsageRangeAreaChart")!;
    internal List<object> LineAreaBindingDiagnostics { get; } = [];
    internal List<object> AccessibilityBindingDiagnostics { get; } = [];
    internal List<object> ExportBindingDiagnostics { get; } = [];
    internal List<object> TrackballBindingDiagnostics { get; } = [];
    private CancellationTokenSource? _exportCancellation;
    private bool _exportClosed, _exportPickerActive;
    internal string? AnnotationExportBackendDiagnostic { get; private set; }
    internal ChartRangeSelector SelectionRangeControl => this.FindControl<ChartRangeSelector>("UsageRangeSelector")!;
    private void UpdateReadingTooltipResources()
    {
        var dark = ActualThemeVariant == ThemeVariant.Dark;
        Resources["UsageTooltipBackground"] = new SolidColorBrush(Color.Parse(dark ? "#202B40" : "#FFFFFF"));
        Resources["UsageTooltipForeground"] = new SolidColorBrush(Color.Parse(dark ? "#F2F5FC" : "#172238"));
        Resources["UsageControlTemplateForeground"] = new SolidColorBrush(Color.Parse(dark ? "#F2F5FC" : "#172238"));
        Resources["UsageControlTemplateAccent"] = new SolidColorBrush(Color.Parse(dark ? "#FFB8E7" : "#9C247A"));
    }
    private void ToggleTheme(object? sender, RoutedEventArgs args) => RequestedThemeVariant = ActualThemeVariant == ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark;
    private void ReplayAnimation(object? sender, RoutedEventArgs args) => AnimationChart.ReplayEntranceAnimation();
    private void CancelAnimation(object? sender, RoutedEventArgs args) => AnimationChart.CancelAnimations();
    private void CancelExport(object? sender, RoutedEventArgs args) => _exportCancellation?.Cancel();
    private void FocusAccessibilityChart(object? sender, RoutedEventArgs args) => AccessibilityChart.Focus();
    private void UpdateAccessibilityValue(object? sender, RoutedEventArgs args) => Model.AccessibilityRecords[2].Y = Model.AccessibilityRecords[2].Y == 65 ? 80 : 65;
    private async void SaveExport(object? sender, RoutedEventArgs args)
    {
        if (_exportClosed || _exportPickerActive || _exportCancellation is not null) return;
        _exportPickerActive = true;
        try
        {
            var jpeg = Model.ExportFormat == UsageExportFormat.Jpeg;
            using var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            { Title = "导出图表", SuggestedFileName = jpeg ? "chart-usage.jpg" : "chart-usage.png", DefaultExtension = jpeg ? "jpg" : "png" });
            if (file is null || _exportClosed) return;
            var options = Model.CreateExportOptions();
            if (file.TryGetLocalPath() is { } path)
                await RunExportOperationAsync(token => jpeg ? ExportChart.TryExportJpegAsync(path, options, token) : ExportChart.TryExportPngAsync(path, options, token));
            else
            {
                await using var stream = await file.OpenWriteAsync();
                if (_exportClosed) return;
                await RunExportOperationAsync(token => jpeg ? ExportChart.TryExportJpegAsync(stream, options, token) : ExportChart.TryExportPngAsync(stream, options, token));
            }
        }
        catch (Exception exception) { Model.ExportStatus = "无法保存：" + exception.Message; }
        finally { _exportPickerActive = false; }
    }

    internal Task<bool> ExportToStreamAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var options = Model.CreateExportOptions(); var jpeg = Model.ExportFormat == UsageExportFormat.Jpeg;
        return RunExportOperationAsync(token => jpeg ? ExportChart.TryExportJpegAsync(stream, options, token) : ExportChart.TryExportPngAsync(stream, options, token), cancellationToken);
    }

    private async Task<bool> RunExportOperationAsync(Func<CancellationToken, Task<bool>> operation, CancellationToken cancellationToken = default)
    {
        if (_exportClosed || _exportCancellation is not null) return false;
        _exportCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Model.ExportIsBusy = true; Model.ExportStatus = "正在捕获、编码和写入…";
        Exception? failure = null;
        void Failed(object? sender, ChartExportFailedEventArgs args) => failure = args.Exception;
        ExportChart.ExportFailed += Failed;
        try
        {
            var success = await operation(_exportCancellation.Token);
            Model.ExportStatus = success ? "导出完成。" : "导出失败：" + (failure?.Message ?? "当前后端未生成图像。");
            return success;
        }
        catch (OperationCanceledException) when (_exportCancellation.IsCancellationRequested) { Model.ExportStatus = "导出已取消。"; return false; }
        catch (Exception exception) { Model.ExportStatus = "导出失败：" + exception.Message; return false; }
        finally
        {
            ExportChart.ExportFailed -= Failed; _exportCancellation.Dispose(); _exportCancellation = null; Model.ExportIsBusy = false;
        }
    }

    internal async Task<Dictionary<string, bool>> VerifyExportBindingsAsync()
    {
        var checks = new Dictionary<string, bool>();
        this.FindControl<Expander>("ExportExamples")!.IsExpanded = true; FlushBindings();
        var chart = ExportChart; var model = Model;
        var format = this.FindControl<ComboBox>("UsageExportFormatSelector")!; var width = this.FindControl<NumericUpDown>("UsageExportWidth")!;
        var height = this.FindControl<NumericUpDown>("UsageExportHeight")!; var dpi = this.FindControl<ComboBox>("UsageExportDpi")!;
        var background = this.FindControl<ComboBox>("UsageExportBackgroundSelector")!; var quality = this.FindControl<ComboBox>("UsageExportQuality")!;
        var inferred = this.FindControl<CheckBox>("UsageExportInferred")!;
        checks["export-compiled-selector-sources-and-defaults"] = ReferenceEquals(format.ItemsSource, model.ExportFormats) && Equals(format.SelectedItem, UsageExportFormat.Png) &&
            ReferenceEquals(dpi.ItemsSource, model.ExportDpis) && ReferenceEquals(quality.ItemsSource, model.ExportQualities) && Equals(background.SelectedItem, UsageExportBackground.White);
        var options = model.CreateExportOptions();
        checks["export-compiled-default-dimensions-background-dpi-quality"] = width.Value == 1200 && height.Value == 700 && options.PixelSize == new PixelSize(1200, 700) &&
            options.Dpi == 96 && options.JpegQuality == 90 && options.Background is ISolidColorBrush { Color: var initialColor } && initialColor == Colors.White;
        model.ExportPixelWidth = 800; model.ExportPixelHeight = 600; model.ExportDpi = 144; model.ExportQuality = 77; model.ExportFormat = UsageExportFormat.Jpeg;
        model.ExportBackground = UsageExportBackground.LightBlue; FlushBindings(); options = model.CreateExportOptions();
        checks["export-compiled-model-updates-options-and-panel"] = width.Value == 800 && height.Value == 600 && Equals(dpi.SelectedItem, 144d) && Equals(quality.SelectedItem, 77) &&
            Equals(format.SelectedItem, UsageExportFormat.Jpeg) && options.PixelSize == new PixelSize(800, 600) && options.Background is ISolidColorBrush { Color: var changedColor } && changedColor == Color.Parse("#E1F1FF");
        format.SetCurrentValue(ComboBox.SelectedItemProperty, UsageExportFormat.Png); inferred.SetCurrentValue(CheckBox.IsCheckedProperty, true); FlushBindings();
        checks["export-compiled-two-way-format-and-derived-size"] = model.ExportFormat == UsageExportFormat.Png && model.ExportInferredSize && model.CreateExportOptions().PixelSize is null;
        model.ExportInferredSize = false; model.ExportPixelWidth = 320; model.ExportPixelHeight = 200; model.ExportDpi = 96; model.ExportQuality = 90; FlushBindings();
        foreach (var jpeg in new[] { false, true })
        {
            using var stream = new MemoryStream(); Exception? failure = null; var events = 0; var onUi = true;
            void Failed(object? sender, ChartExportFailedEventArgs args) { failure = args.Exception; events++; onUi &= Dispatcher.UIThread.CheckAccess(); }
            chart.ExportFailed += Failed;
            var success = jpeg ? await chart.TryExportJpegAsync(stream, model.CreateExportOptions()) : await chart.TryExportPngAsync(stream, model.CreateExportOptions());
            var formatMatches = success && ExportSignatureMatches(stream.ToArray(), jpeg);
            checks[$"export-async-{(jpeg ? "jpeg" : "png")}-result-or-explicit-backend-contract"] = stream.CanWrite && onUi &&
                (success ? events == 0 && formatMatches : events == 1 && failure is NotSupportedException && stream.Length == 0);
            ExportBindingDiagnostics.Add(new { scenario = jpeg ? "async-jpeg" : "async-png", success, outputBytes = stream.Length, events, eventOnUiThread = onUi,
                failureType = failure?.GetType().Name, failureMessage = failure?.Message, formatSignatureMatches = formatMatches, nativePixelProof = false });
            chart.ExportFailed -= Failed;
        }
        using (var stream = new MemoryStream())
        {
            Exception? failure = null; var events = 0; void Failed(object? sender, ChartExportFailedEventArgs args) { failure = args.Exception; events++; }
            chart.ExportFailed += Failed; var success = chart.TryExportJpeg(stream, model.CreateExportOptions()); chart.ExportFailed -= Failed;
            checks["export-sync-jpeg-result-or-explicit-backend-contract"] = stream.CanWrite && (success ? events == 0 && ExportSignatureMatches(stream.ToArray(), true) : events == 1 && failure is NotSupportedException && stream.Length == 0);
            ExportBindingDiagnostics.Add(new { scenario = "sync-jpeg", success, outputBytes = stream.Length, events, failureType = failure?.GetType().Name, failureMessage = failure?.Message, nativePixelProof = false });
        }
        foreach (var jpeg in new[] { false, true })
        {
            using var stream = new MemoryStream(); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            var events = 0; var canceled = false; void Failed(object? sender, ChartExportFailedEventArgs args) => events++;
            chart.ExportFailed += Failed;
            try { if (jpeg) await chart.TryExportJpegAsync(stream, model.CreateExportOptions(), cancellation.Token); else await chart.TryExportPngAsync(stream, model.CreateExportOptions(), cancellation.Token); }
            catch (OperationCanceledException) { canceled = true; }
            chart.ExportFailed -= Failed;
            checks[$"export-pre-canceled-{(jpeg ? "jpeg" : "png")}-throws-without-failure-or-output"] = canceled && events == 0 && stream.Length == 0 && stream.CanWrite;
            Exception? failure = null; var onUi = true; events = 0;
            void InvalidFailed(object? sender, ChartExportFailedEventArgs args) { failure = args.Exception; events++; onUi &= Dispatcher.UIThread.CheckAccess(); }
            chart.ExportFailed += InvalidFailed;
            var success = jpeg ? await chart.TryExportJpegAsync(stream, model.CreateExportOptions() with { JpegQuality = 0 }) : await chart.TryExportPngAsync(stream, model.CreateExportOptions() with { Dpi = 0 });
            chart.ExportFailed -= InvalidFailed;
            checks[$"export-invalid-{(jpeg ? "jpeg-quality" : "png-dpi")}-false-and-one-ui-event"] = !success && failure is ArgumentOutOfRangeException && events == 1 && onUi && stream.Length == 0 && stream.CanWrite;
            using var readOnly = new MemoryStream([], writable: false); failure = null; events = 0; chart.ExportFailed += InvalidFailed;
            success = jpeg ? await chart.TryExportJpegAsync(readOnly, model.CreateExportOptions()) : await chart.TryExportPngAsync(readOnly, model.CreateExportOptions());
            chart.ExportFailed -= InvalidFailed;
            checks[$"export-readonly-{(jpeg ? "jpeg" : "png")}-stream-fails-before-render"] = !success && failure is ArgumentException && events == 1 && onUi && readOnly.CanRead;
        }
        var busyStates = new List<bool>();
        void Changed(object? sender, System.ComponentModel.PropertyChangedEventArgs args) { if (args.PropertyName == nameof(model.ExportIsBusy)) busyStates.Add(model.ExportIsBusy); }
        model.PropertyChanged += Changed;
        using (var canceled = new CancellationTokenSource())
        using (var stream = new MemoryStream())
        {
            canceled.Cancel(); var success = await ExportToStreamAsync(stream, canceled.Token); FlushBindings();
            checks["export-compiled-cancel-status-and-busy-buttons"] = !success && busyStates.SequenceEqual(new[] { true, false }) && model.ExportStatus.Contains("取消", StringComparison.Ordinal) &&
                this.FindControl<TextBlock>("UsageExportStatus")!.Text == model.ExportStatus && this.FindControl<Button>("UsageExportSave")!.IsEnabled && !this.FindControl<Button>("UsageExportCancel")!.IsEnabled;
        }
        using (var stream = new MemoryStream())
        {
            model.ExportDpi = 0; var success = await ExportToStreamAsync(stream); FlushBindings();
            checks["export-compiled-failure-status-restores-busy-state"] = !success && !model.ExportIsBusy && model.ExportStatus.StartsWith("导出失败", StringComparison.Ordinal) && stream.Length == 0;
        }
        model.PropertyChanged -= Changed;
        model.ExportFormat = UsageExportFormat.Png; model.ExportPixelWidth = 1200; model.ExportPixelHeight = 700; model.ExportDpi = 96; model.ExportQuality = 90;
        model.ExportBackground = UsageExportBackground.White; model.ExportInferredSize = false; model.ExportStatus = "导出绑定检查已完成。"; FlushBindings();
        checks["export-compiled-settings-restored"] = model.CreateExportOptions().PixelSize == new PixelSize(1200, 700) && model.ExportDpi == 96 && !model.ExportIsBusy;
        return checks;
    }

    internal static bool ExportSignatureMatches(byte[] bytes, bool jpeg) => jpeg ? bytes.Length > 4 && bytes[0] == 255 && bytes[1] == 216 && bytes[^2] == 255 && bytes[^1] == 217 :
        bytes.Length > 32 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
    internal void FlushBindings() { Dispatcher.UIThread.RunJobs(); UpdateLayout(); }

    internal Dictionary<string, bool> VerifyBindings()
    {
        FlushBindings();
        var checks = new Dictionary<string, bool>
        {
            ["compiled-series-source"] = ReferenceEquals(BoundChart.SeriesSource, Model.SeriesSource),
            ["inherited-series-items"] = ReferenceEquals(InheritedChart.Series.Single().ItemsSource, Model.Records),
            ["inherited-series-title"] = InheritedChart.Series.Single().Title == Model.SeriesTitle,
            ["inherited-axis-title"] = InheritedChart.YAxis.Title == Model.AxisTitle && BoundChart.YAxis.Title == Model.AxisTitle,
        };
        var before = InheritedChart.VisibleRange;
        Model.Records[0].Value += 500;
        FlushBindings();
        checks["item-property-refreshes-range"] = before is { } old && InheritedChart.VisibleRange is { } changed && changed.YMaximum > old.YMaximum;
        Model.AxisTitle = "更新后的坐标标题";
        Model.SeriesTitle = "更新后的系列";
        FlushBindings();
        checks["compiled-inherited-property-updates"] = InheritedChart.YAxis.Title == Model.AxisTitle && InheritedChart.Series.Single().Title == Model.SeriesTitle;
        var previous = Model.SeriesSource;
        Model.ReplaceSeriesCommand.Execute(null);
        FlushBindings();
        checks["series-source-replacement"] = !ReferenceEquals(previous, Model.SeriesSource) && ReferenceEquals(BoundChart.SeriesSource, Model.SeriesSource);
        BoundChart.SetCurrentValue(CartesianChart.ViewportProperty, new ChartViewport(.5, 2.5, 20, 80));
        FlushBindings();
        checks["viewport-two-way"] = Model.Viewport == BoundChart.Viewport && Model.Viewport == InheritedChart.Viewport && Model.Viewport is not null;
        Model.ResetViewportCommand.Execute(null);
        FlushBindings();
        checks["viewmodel-reset-command"] = BoundChart.Viewport is null && InheritedChart.Viewport is null;
        Resources["UsageSeriesBrush"] = Brushes.HotPink;
        FlushBindings();
        checks["series-inherits-dynamic-resource"] = InheritedChart.Series.Single().Stroke is ISolidColorBrush brush && brush.Color == Colors.HotPink;
        checks["item-subscriptions-active"] = Model.Records.All(record => record.SubscriberCount > 0);
        var content = Content;
        Content = null;
        FlushBindings();
        checks["detach-releases-item-subscriptions"] = Model.Records.All(record => record.SubscriberCount == 0);
        Content = content;
        FlushBindings();
        checks["reattach-restores-bindings"] = Model.Records.All(record => record.SubscriberCount > 0) && ReferenceEquals(InheritedChart.Series.Single().ItemsSource, Model.Records);
        VerifyAxisBindings(checks);
        VerifyCommonBindings(checks);
        VerifyCommonFollowupBindings(checks);
        VerifyReadingBindings(checks);
        VerifyControlTemplateBindings(checks);
        VerifyTrackballBindings(checks);
        VerifySelectionRangeBindings(checks);
        VerifyInputBindings(checks);
        VerifyAnnotationCompletionBindings(checks);
        VerifyAnimationBindings(checks);
        VerifyAccessibilityBindings(checks);
        VerifyLineAreaBindings(checks);
        VerifyBarComparisonBindings(checks);
        VerifyScatterBubbleBindings(checks);
        VerifyCircularBindings(checks);
        VerifyPolarBindings(checks);
        VerifyGaugeMicroBindings(checks);
        VerifyAnalysisBindings(checks);
        VerifyStatisticsBindings(checks);
        VerifyFinancialBindings(checks);
        VerifyTechnicalTrendsBindings(checks);
        VerifyEngineeringBindings(checks);
        VerifyAdvancedComparisonBindings(checks);
        VerifyHierarchyBindings(checks);
        VerifyRelationsBindings(checks);
        VerifyMapBindings(checks);
        VerifyC6CompletionBindings(checks);
        VerifyC5CompletionBindings(checks);
        VerifyLevelOfDetailBindings(checks);
        VerifyEfficientHitBindings(checks);
        VerifyIncrementalStreamingBindings(checks);
        VerifyRenderResourceBindings(checks);
        return checks;
    }

    internal void VerifyLineAreaBindings(Dictionary<string, bool> checks)
    {
        var expander = this.FindControl<Expander>("LineAreaExamples")!;
        expander.IsExpanded = true; FlushBindings();
        var chart = LineAreaChart; var rangeChart = RangeAreaChart;
        _ = chart.PlotArea; _ = rangeChart.PlotArea; FlushBindings();
        var first = (AreaSeries)chart.Series[0]; var second = (AreaSeries)chart.Series[1]; var line = (LineSeries)chart.Series[2]; var range = (RangeAreaSeries)rangeChart.Series[0];
        checks["line-area-compiled-new-series-types-and-sources"] = chart.Series.Count == 3 && ReferenceEquals(first.ItemsSource, Model.LineAreaPrimaryPoints) && ReferenceEquals(range.ItemsSource, Model.LineAreaRangeRecords);
        checks["line-area-compiled-initial-interpolation-and-group-isolation"] = first.Interpolation == ChartLineInterpolation.Smooth && second.Interpolation == ChartLineInterpolation.Smooth && line.Interpolation == ChartLineInterpolation.Smooth &&
            first.StackMode == ChartStackMode.Stacked && first.StackGroup == second.StackGroup && first.StackGroup != line.StackGroup;
        checks["line-area-compiled-missing-contribution-preserved"] = second.CreateSnapshot().Points[2].IsEmpty;
        var mapped = range.CreateSnapshot().Points[1];
        checks["line-area-compiled-high-low-without-valuepath"] = range.ValuePath is null && mapped.Low == 20 && mapped.High == 70 && ReferenceEquals(mapped.Item, Model.LineAreaRangeRecords[1]) && Equals(mapped.StableId, "usage-range-b");
        Model.LineAreaInterpolation = ChartLineInterpolation.StepBefore; FlushBindings();
        checks["line-area-compiled-model-updates-all-interpolations"] = first.Interpolation == ChartLineInterpolation.StepBefore && second.Interpolation == ChartLineInterpolation.StepBefore && line.Interpolation == ChartLineInterpolation.StepBefore && range.Interpolation == ChartLineInterpolation.StepBefore;
        this.FindControl<ComboBox>("UsageLineAreaInterpolation")!.SelectedItem = ChartLineInterpolation.StepCenter; FlushBindings();
        checks["line-area-compiled-interpolation-selector-is-two-way"] = Model.LineAreaInterpolation == ChartLineInterpolation.StepCenter && range.Interpolation == ChartLineInterpolation.StepCenter;
        Model.LineAreaStackMode = ChartStackMode.Percent; FlushBindings();
        checks["line-area-compiled-model-updates-stack-mode"] = first.StackMode == ChartStackMode.Percent && second.StackMode == ChartStackMode.Percent && line.StackMode == ChartStackMode.Percent;
        this.FindControl<ComboBox>("UsageLineAreaStackMode")!.SelectedItem = ChartStackMode.None; FlushBindings();
        checks["line-area-compiled-stack-selector-is-two-way"] = Model.LineAreaStackMode == ChartStackMode.None && second.StackMode == ChartStackMode.None;
        Model.LineAreaBaseline = 10; FlushBindings();
        checks["line-area-compiled-model-updates-baseline"] = first.Baseline == 10 && second.Baseline == 10;
        this.FindControl<ComboBox>("UsageLineAreaBaseline")!.SelectedItem = -20d; FlushBindings();
        checks["line-area-compiled-baseline-selector-is-two-way"] = Model.LineAreaBaseline == -20 && first.Baseline == -20;
        Model.LineAreaGap = false; FlushBindings();
        checks["line-area-compiled-data-switch-replaces-missing-value"] = second.CreateSnapshot().Points[2] is { IsEmpty: false, Point.Y: 70 };
        this.FindControl<CheckBox>("UsageLineAreaGap")!.IsChecked = true; FlushBindings();
        checks["line-area-compiled-gap-checkbox-is-two-way"] = Model.LineAreaGap && second.CreateSnapshot().Points[2].IsEmpty;
        Model.LineAreaInterpolation = ChartLineInterpolation.Linear; Model.LineAreaStackMode = ChartStackMode.Percent; line.IsVisible = false; FlushBindings(); _ = chart.PlotArea;
        ChartPointHit? Point(double x, double y)
        {
            var bounds = chart.VisibleRange!.Value; var plot = chart.PlotArea;
            return chart.HitTestPoint(new Point(plot.Left + (x - bounds.XMinimum) / (bounds.XMaximum - bounds.XMinimum) * plot.Width,
                plot.Bottom - (y - bounds.YMinimum) / (bounds.YMaximum - bounds.YMinimum) * plot.Height), 8);
        }
        var positive = Point(1, 100); var negative = Point(3, -100);
        static bool SameMetadata(double? actual, double expected) => actual is { } number && double.IsFinite(number) && Math.Abs(number - expected) <= 1e-9;
        checks["line-area-compiled-percent-positive-retains-contribution"] = positive is { Index: 0, Point.Y: 100 } && SameMetadata(positive.BaseValue, 40) && SameMetadata(positive.StackValue, 60) && ReferenceEquals(positive.Series, second) && positive.RawPoint == new ChartPoint(1, 30);
        checks["line-area-compiled-percent-negative-retains-contribution"] = negative is { Index: 1, Point.Y: -100 } && SameMetadata(negative.BaseValue, -40) && SameMetadata(negative.StackValue, -60) && ReferenceEquals(negative.Series, second) && negative.RawPoint == new ChartPoint(3, -30);
        if (positive is not null) chart.SelectedPoints = new[] { positive }; FlushBindings();
        checks["line-area-compiled-selection-retains-derived-and-source-values"] = chart.SelectedPoint is { } selected && SameMetadata(selected.BaseValue, 40) && SameMetadata(selected.StackValue, 60) && selected.RawPoint == new ChartPoint(1, 30);
        Model.LineAreaRangeRecords[1].High = 82; Model.LineAreaRangeRecords[1].Low = 12; _ = rangeChart.PlotArea; FlushBindings();
        var updated = range.CreateSnapshot().Points[1];
        checks["line-area-compiled-range-inpc-updates-both-bounds"] = updated.High == 82 && updated.Low == 12 && ReferenceEquals(updated.Item, Model.LineAreaRangeRecords[1]);
        var content = Content; Content = null; FlushBindings();
        checks["line-area-compiled-detach-releases-range-subscriptions"] = Model.LineAreaRangeRecords.All(record => record.SubscriberCount == 0);
        Content = content; FlushBindings(); _ = rangeChart.PlotArea;
        checks["line-area-compiled-reattach-restores-range-subscriptions"] = Model.LineAreaRangeRecords.All(record => record.SubscriberCount > 0) && ReferenceEquals(range.ItemsSource, Model.LineAreaRangeRecords);
        LineAreaBindingDiagnostics.Add(new { interpolation = Model.LineAreaInterpolation.ToString(), stackMode = Model.LineAreaStackMode.ToString(), Model.LineAreaBaseline, Model.LineAreaGap,
            derivedMetadataAbsoluteTolerance = 1e-9, rawPointAndIdentityExact = true,
            positive = positive is null ? null : new { positive.Point, positive.RawPoint, positive.BaseValue, positive.StackValue, positive.Index },
            negative = negative is null ? null : new { negative.Point, negative.RawPoint, negative.BaseValue, negative.StackValue, negative.Index },
            range = new { updated.Low, updated.High, updated.SourceIndex, updated.StableId }, physicalInputValidated = false, nativeBridgeValidated = false });
        chart.ClearSelection(); line.IsVisible = true; Model.LineAreaRangeRecords[1].High = 70; Model.LineAreaRangeRecords[1].Low = 20;
        Model.LineAreaBaseline = 0; Model.LineAreaStackMode = ChartStackMode.Stacked; Model.LineAreaInterpolation = ChartLineInterpolation.Smooth; FlushBindings();
        expander.IsExpanded = false; FlushBindings();
    }

    internal void VerifyAccessibilityBindings(Dictionary<string, bool> checks)
    {
        this.FindControl<Expander>("AccessibilityExamples")!.IsExpanded = true; FlushBindings();
        var chart = AccessibilityChart; var line = chart.Series[0];
        _ = chart.PlotArea; FlushBindings();
        checks["accessibility-compiled-business-records-and-multi-axis"] = ReferenceEquals(line.ItemsSource, Model.AccessibilityRecords) &&
            line.IdPath == nameof(TrackballUsageRecord.Id) && chart.Axes.Single().Key == "usage-accessibility-pressure" && chart.Series.Count == 3;
        checks["accessibility-compiled-browsing-enabled"] = chart.IsKeyboardNavigationEnabled && chart.IsPointBrowsingEnabled;
        Model.AccessibilityKeyboardEnabled = false; Model.AccessibilityPointBrowsing = false; FlushBindings();
        checks["accessibility-viewmodel-disables-keyboard-and-point-browsing"] = !chart.IsKeyboardNavigationEnabled && !chart.IsPointBrowsingEnabled;
        this.FindControl<CheckBox>("UsageAccessibilityKeyboard")!.IsChecked = true;
        this.FindControl<CheckBox>("UsageAccessibilityPointBrowsing")!.IsChecked = true; FlushBindings();
        checks["accessibility-compiled-two-way-browsing-options"] = Model.AccessibilityKeyboardEnabled && Model.AccessibilityPointBrowsing && chart.IsKeyboardNavigationEnabled && chart.IsPointBrowsingEnabled;
        _ = chart.PlotArea; var summary = chart.AccessibilitySummary;
        checks["accessibility-compiled-summary-counts-missing-record"] = summary.TotalCount == 10 && summary.ValidCount == 9 && summary.MissingCount == 1;
        checks["accessibility-summary-reports-independent-secondary-axis"] = summary.Axes.Any(axis => axis.Key == "usage-accessibility-pressure" && !axis.IsHorizontal && axis.Minimum == 1000 && axis.Maximum == 2000);
        checks["accessibility-compiled-summary-text-binding"] = this.FindControl<TextBlock>("UsageAccessibilitySummary")!.Text == chart.AccessibilitySummaryText && chart.AccessibilitySummaryText.Length > 30;
        Model.AccessibilityContrast = ChartHighContrastMode.Dark; FlushBindings();
        checks["accessibility-compiled-high-contrast-viewmodel-to-chart"] = chart.HighContrastMode == ChartHighContrastMode.Dark &&
            this.FindControl<ComboBox>("UsageAccessibilityContrast")!.SelectedItem is ChartHighContrastMode.Dark;
        this.FindControl<ComboBox>("UsageAccessibilityContrast")!.SelectedItem = ChartHighContrastMode.Light; FlushBindings();
        checks["accessibility-compiled-high-contrast-two-way-selector"] = Model.AccessibilityContrast == ChartHighContrastMode.Light && chart.HighContrastMode == ChartHighContrastMode.Light;
        line.Stroke = Brushes.HotPink; Model.AccessibilityContrast = ChartHighContrastMode.Dark; FlushBindings();
        checks["accessibility-high-contrast-preserves-explicit-series-color"] = chart.GetSeriesBrush(line) is ISolidColorBrush custom && custom.Color == Colors.HotPink;
        line.ClearValue(ChartSeries.StrokeProperty);
        chart.SelectionMode = ChartSelectionMode.None; chart.Focus();
        var key = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Source = chart, Key = Key.Right }; chart.RaiseEvent(key); FlushBindings();
        checks["accessibility-routed-browsing-independent-of-selection"] = key.Handled && chart.CurrentPoint is { Index: 0 } current && ReferenceEquals(current.Item, Model.AccessibilityRecords[0]) && chart.SelectedPoint is null;
        chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Source = chart, Key = Key.Right }); FlushBindings();
        checks["accessibility-routed-browsing-skips-missing-source-index"] = chart.CurrentPoint is { Index: 2 } second && Equals(second.StableId, "access-b") && ReferenceEquals(second.Item, Model.AccessibilityRecords[2]);
        var peer = (ChartAutomationPeer)ControlAutomationPeer.CreatePeerForElement(chart)!;
        var point = peer.GetPointPeer(line, 2);
        checks["accessibility-point-provider-original-item-and-read-only-value"] = point is IValueProvider { IsReadOnly: true } value && value.Value is { Length: > 0 } &&
            point.CurrentPoint is { Index: 2 } data && ReferenceEquals(data.Item, Model.AccessibilityRecords[2]) && Equals(data.StableId, "access-b");
        Model.AccessibilityRecords[2].Y = 78; _ = chart.PlotArea; FlushBindings();
        checks["accessibility-current-point-and-summary-refresh-after-item-change"] = chart.CurrentPoint is { Point.Y: 78 } &&
            point?.CurrentPoint is { Point.Y: 78 } && this.FindControl<TextBlock>("UsageAccessibilityCurrent")!.Text?.Contains("78", StringComparison.Ordinal) == true;
        chart.SelectionMode = ChartSelectionMode.Multiple;
        chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Source = chart, Key = Key.Space }); FlushBindings();
        checks["accessibility-space-selects-current-point"] = chart.SelectedPoint is { Index: 2 } && point is ISelectionItemProvider { IsSelected: true };
        chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Source = chart, Key = Key.Escape }); FlushBindings();
        checks["accessibility-escape-clears-current-keeps-selection"] = chart.CurrentPoint is null && chart.SelectedPoint is { Index: 2 };
        AccessibilityBindingDiagnostics.Add(new { scenario = "compiled-accessibility", summary = chart.AccessibilitySummaryText, chart.HighContrastMode,
            keyboardEnabled = chart.IsKeyboardNavigationEnabled, browsingEnabled = chart.IsPointBrowsingEnabled, pointName = point?.GetName(), pointStatus = point?.GetItemStatus(),
            originalId = point?.CurrentPoint?.StableId?.ToString(), sourceIndex = point?.CurrentPoint?.Index, nativeBridgeValidated = false });
        chart.ClearSelection(); Model.AccessibilityRecords[2].Y = 65; Model.AccessibilityContrast = ChartHighContrastMode.None; chart.SelectionMode = ChartSelectionMode.Multiple; FlushBindings();
    }

    private void VerifyAnimationBindings(Dictionary<string, bool> checks)
    {
        this.FindControl<Expander>("AnimationExamples")!.IsExpanded = true; FlushBindings();
        var chart = AnimationChart;
        checks["compiled-animation-disabled-default"] = chart.AnimationTriggers == ChartAnimationTriggers.None;
        checks["compiled-animation-timing-and-easing"] = chart.AnimationDuration == TimeSpan.FromMilliseconds(800) && chart.AnimationDelay == TimeSpan.FromMilliseconds(150) && ReferenceEquals(chart.AnimationEasing, Model.AnimationEasing);
        checks["compiled-animation-three-series-sources"] = ReferenceEquals(chart.Series.OfType<LineSeries>().Single().ItemsSource, Model.AnimationLinePoints) && chart.Series.Count == 3;
        Model.AnimationDuration = TimeSpan.FromSeconds(30); Model.AnimationDelay = TimeSpan.Zero; Model.AnimationPaused = true; Model.AnimationEnabled = true; FlushBindings();
        checks["compiled-animation-enable-duration-pause"] = chart.AnimationTriggers == ChartAnimationTriggers.All && chart.AnimationDuration == TimeSpan.FromSeconds(30) && chart.IsAnimationPaused;
        chart.ReplayEntranceAnimation(); FlushBindings();
        checks["compiled-animation-replay-paused"] = chart.AnimationState == ChartAnimationState.Paused && chart.AnimationProgress == 0;
        chart.CancelAnimations(); FlushBindings();
        checks["compiled-animation-cancel-final"] = chart.AnimationState == ChartAnimationState.Idle && chart.AnimationProgress == 1;
        Model.AnimationUpdateCommand.Execute(null); FlushBindings();
        checks["compiled-animation-data-replacement-bound"] = ReferenceEquals(chart.Series.OfType<LineSeries>().Single().ItemsSource, Model.AnimationLinePoints) && chart.AnimationState == ChartAnimationState.Paused;
        Model.AnimationReduceMotion = true; FlushBindings();
        checks["compiled-animation-reduce-motion-snaps-target"] = chart.ReduceMotion && chart.AnimationState == ChartAnimationState.Idle && chart.AnimationProgress == 1;
        Model.AnimationViewCommand.Execute(null); FlushBindings();
        checks["compiled-animation-view-two-way-with-reduced-motion"] = chart.Viewport == Model.AnimationViewport && chart.VisibleRange == Model.AnimationViewport;
        Model.AnimationReduceMotion = false; Model.AnimationPointLimit = 1; Model.AnimationUpdateCommand.Execute(null); FlushBindings();
        checks["compiled-animation-budget-skips-transition"] = chart.AnimationPointLimit == 1 && chart.AnimationState == ChartAnimationState.Idle;
        chart.SetCurrentValue(CartesianChart.IsAnimationPausedProperty, false); FlushBindings();
        checks["compiled-animation-pause-two-way"] = !Model.AnimationPaused;
        Model.AnimationEnabled = false; Model.AnimationPointLimit = 10_000; Model.AnimationViewport = null;
        Model.AnimationDuration = TimeSpan.FromMilliseconds(800); Model.AnimationDelay = TimeSpan.FromMilliseconds(150); FlushBindings();
        checks["compiled-animation-restores-binding-settings"] = chart.AnimationTriggers == ChartAnimationTriggers.None && chart.VisibleRange == new ChartViewport(0, 10, 0, 100);
    }

    private void VerifyAnnotationCompletionBindings(Dictionary<string, bool> checks)
    {
        var expander = this.FindControl<Expander>("AnnotationCompletionExamples")!;
        expander.IsExpanded = true; FlushBindings(); expander.BringIntoView(); AnnotationCompletionChart.BringIntoView(); FlushBindings();
        var chart = AnnotationCompletionChart; var axis = chart.Axes.Single();
        var rectangle = chart.Annotations.OfType<RectangleAnnotation>().Single();
        var ellipse = chart.Annotations.OfType<EllipseAnnotation>().Single();
        Border? Card() => chart.GetVisualDescendants().OfType<Border>().SingleOrDefault(item => item.Name == "UsageAnnotationPlotCard");
        ChartPlotAreaContext? Context() => Card()?.DataContext as ChartPlotAreaContext;
        checks["compiled-annotation-only-axis-without-dummy-series"] = chart.Series.All(item => item.YAxisKey != "reference") && chart.GetAxisLayout(axis) is not null;
        checks["compiled-rounded-rectangle-label-and-inherited-model"] = rectangle.CornerRadius == 12 && rectangle.Label == Model.AnnotationShapeLabel && ReferenceEquals(rectangle.DataContext, Model);
        checks["compiled-ellipse-center-radius"] = ellipse.GeometryMode == ChartEllipseGeometryMode.CenterRadius && ellipse.RadiusX == 10 && ellipse.RadiusY == 120;
        checks["compiled-plot-content-real-control-and-context"] = Card() is { Bounds.Width: > 20, Bounds.Height: > 10 } && Context() is { } initial && Equals(initial.Content, Model.AnnotationOverlayMessage);
        Model.AnnotationCornerRadius = 20; Model.AnnotationShapeLabel = "更新后的区间"; Model.AnnotationRadiusX = 16; Model.AnnotationRadiusY = 90; FlushBindings();
        checks["compiled-shape-live-model-updates"] = rectangle.CornerRadius == 20 && rectangle.Label == "更新后的区间" && ellipse.RadiusX == 16 && ellipse.RadiusY == 90;
        var oldCard = Card(); Model.AnnotationOverlayMessage = "参考窗口已更新"; FlushBindings();
        checks["compiled-plot-content-source-replacement"] = Context() is { } replaced && Equals(replaced.Content, "参考窗口已更新") &&
            oldCard is not null && !oldCard.GetVisualAncestors().Contains(chart) && oldCard.DataContext is null;
        var context = Context();
        checks["compiled-plot-context-named-axis-projection"] = context is not null && context.TryGetProjection(null, "reference", out var projection) &&
            Math.Abs(projection.MapY(1500) - chart.PlotArea.Height / 2) < 1e-8;
        chart.Viewport = new(20, 80, 20, 80); FlushBindings(); context = Context();
        checks["compiled-annotation-axis-and-template-zoom-window"] = context is not null && context.TryGetVisibleRange(null, "reference", out var range) &&
            Math.Abs(range.YMinimum - 1200) < 1e-8 && Math.Abs(range.YMaximum - 1800) < 1e-8;
        Resources["UsageControlTemplateForeground"] = Brushes.LimeGreen; FlushBindings();
        checks["compiled-plot-content-dynamic-resource"] = chart.GetVisualDescendants().OfType<ContentControl>().Any(item => item.Name == "UsageAnnotationPlotText" && item.Foreground is ISolidColorBrush brush && brush.Color == Colors.LimeGreen);
        using var stream = new MemoryStream();
        Exception? exportFailure = null;
        void Failed(object? sender, ChartExportFailedEventArgs args) => exportFailure = args.Exception;
        chart.ExportFailed += Failed;
        bool exported;
        try { exported = chart.TryExportPng(stream, new ChartExportOptions { PixelSize = new PixelSize(920, 400), Dpi = 144 }); }
        finally { chart.ExportFailed -= Failed; }
        AnnotationExportBackendDiagnostic = exportFailure?.ToString();
        checks["compiled-annotation-plot-export-result-contract"] = exported ? stream.Length > 1000 && exportFailure is null :
            exportFailure is NotSupportedException && stream.Length == 0;
        chart.ResetViewport(); Model.AnnotationCornerRadius = 12; Model.AnnotationShapeLabel = "观察区间"; Model.AnnotationRadiusX = 10; Model.AnnotationRadiusY = 120; Model.AnnotationOverlayMessage = "多轴参考 · 可缩放查看";
        UpdateReadingTooltipResources(); FlushBindings();
        checks["compiled-annotation-bindings-remain-active-after-reset"] = rectangle.CornerRadius == 12 && rectangle.Label == "观察区间" && ellipse.RadiusX == 10 && Context() is { } final && Equals(final.Content, Model.AnnotationOverlayMessage);
    }

    private void VerifyInputBindings(Dictionary<string, bool> checks)
    {
        var examples = this.FindControl<Expander>("SelectionRangeExamples")!;
        examples.IsExpanded = true; FlushBindings(); examples.BringIntoView(); SelectionPrimaryChart.BringIntoView(); FlushBindings();
        var chart = SelectionPrimaryChart;
        checks["compiled-input-initial-mode-and-routing"] = chart.WheelMode == ChartWheelMode.Auto && chart.InputRoutingMode == ChartInputRoutingMode.Auto && chart.IsTouchEnabled;
        Model.InputTouchEnabled = false; Model.InputTouchThreshold = 12; Model.InputPanSensitivity = 30; Model.InputZoomSensitivity = 1.5; FlushBindings();
        checks["compiled-input-enabled-threshold-sensitivity-updates"] = !chart.IsTouchEnabled && chart.TouchPanThreshold == 12 && chart.WheelPanSensitivity == 30 && chart.WheelZoomSensitivity == 1.5;
        Model.InputTouchEnabled = true; Model.InputTouchThreshold = 0; Model.InputWheelMode = ChartWheelMode.Pan; Model.InputRoutingMode = ChartInputRoutingMode.Chart;
        chart.SetNormalizedViewport(new(.2, .8, .2, .8)); FlushBindings();
        var before = chart.VisibleRange!.Value; var width = chart.PlotArea.Width;
        var root = chart.GetVisualAncestors().LastOrDefault() ?? chart;
        using var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        var center = chart.TranslatePoint(chart.PlotArea.Center, root)!.Value;
        var wheel = new PointerWheelEventArgs(chart, pointer, root, center, 1, default, KeyModifiers.None, new Vector(-.25, .125)) { RoutedEvent = InputElement.PointerWheelChangedEvent };
        chart.RaiseEvent(wheel); FlushBindings();
        var after = chart.VisibleRange!.Value;
        checks["compiled-input-fractional-pan-math-and-axis-lock"] = wheel.Handled && Math.Abs(after.XMinimum - (before.XMinimum + (before.XMaximum - before.XMinimum) * 7.5 / width)) < 1e-8 && after.YMinimum == before.YMinimum && after.YMaximum == before.YMaximum;
        checks["compiled-input-pan-updates-range-selector-and-model"] = Math.Abs(Model.SelectionRange.Lower - after.XMinimum / 100) < 1e-8 && SelectionRangeControl.Range == Model.SelectionRange;
        checks["compiled-input-pan-propagates-multichart"] = SelectionSecondaryChart.GetVisibleFraction(ChartInteractionAxes.X) is { } second && Math.Abs(second.Lower - after.XMinimum / 100) < 1e-8;
        Model.InputWheelMode = ChartWheelMode.Disabled; FlushBindings(); var disabledRange = chart.VisibleRange;
        var disabled = new PointerWheelEventArgs(chart, pointer, root, center, 2, default, KeyModifiers.Control, new Vector(0, 1)) { RoutedEvent = InputElement.PointerWheelChangedEvent };
        chart.RaiseEvent(disabled); FlushBindings();
        checks["compiled-input-disabled-wheel-preserves-chart"] = chart.VisibleRange == disabledRange;
        Model.InputWheelMode = ChartWheelMode.Zoom; chart.WheelZoomModifiers = KeyModifiers.Control; FlushBindings();
        var zoomBefore = chart.VisibleRange!.Value;
        var zoom = new PointerWheelEventArgs(chart, pointer, root, chart.TranslatePoint(chart.PlotArea.Center, root)!.Value, 3, default, KeyModifiers.Control, new Vector(0, 1)) { RoutedEvent = InputElement.PointerWheelChangedEvent };
        chart.RaiseEvent(zoom); FlushBindings(); var zoomAfter = chart.VisibleRange!.Value;
        checks["compiled-input-custom-modifier-and-zoom-sensitivity"] = zoom.Handled && Math.Abs((zoomAfter.XMaximum - zoomAfter.XMinimum) - (zoomBefore.XMaximum - zoomBefore.XMinimum) / 1.5) < 1e-8;
        Model.InputTouchEnabled = false; FlushBindings(); var touchBefore = chart.VisibleRange;
        chart.RaiseEvent(new PinchEventArgs(2, chart.PlotArea.Center) { RoutedEvent = InputElement.PinchEvent, Source = chart }); FlushBindings();
        checks["compiled-input-touch-switch-disables-native-pinch"] = chart.VisibleRange == touchBefore;
        Model.InputTouchEnabled = true; chart.WheelZoomModifiers = KeyModifiers.None; Model.InputWheelMode = ChartWheelMode.Auto; Model.InputRoutingMode = ChartInputRoutingMode.Auto;
        Model.InputTouchThreshold = 8; Model.InputPanSensitivity = 48; Model.InputZoomSensitivity = 1.2; chart.ResetViewport(); FlushBindings();
        checks["compiled-input-bindings-remain-active-after-transitions"] = chart.IsTouchEnabled && chart.WheelMode == ChartWheelMode.Auto && chart.InputRoutingMode == ChartInputRoutingMode.Auto && chart.TouchPanThreshold == 8 && chart.WheelPanSensitivity == 48 && chart.WheelZoomSensitivity == 1.2;
        checks["compiled-input-reset-clears-range-selector-and-follower"] = Model.SelectionRange == new ChartRange(0, 1) && SelectionSecondaryChart.GetVisibleFraction(ChartInteractionAxes.X) == new ChartRange(0, 1);
        examples.IsExpanded = false; FlushBindings();
    }

    private void VerifySelectionRangeBindings(Dictionary<string, bool> checks)
    {
        var examples = this.FindControl<Expander>("SelectionRangeExamples")!;
        examples.IsExpanded = true; FlushBindings(); examples.BringIntoView(); FlushBindings();
        var chart = SelectionPrimaryChart; var other = SelectionSecondaryChart; var selector = SelectionRangeControl;
        static bool Near(double a, double b) => Math.Abs(a - b) < 1e-7;
        checks["compiled-range-selector-chart-reference"] = ReferenceEquals(selector.Chart, chart) && selector.Axis == ChartInteractionAxes.X;
        Model.SeriesSelectionMode = ChartSelectionMode.Multiple; Model.SelectedSeries = new[] { chart.Series[0], chart.Series[1] }; FlushBindings();
        checks["compiled-series-selection-model-to-chart"] = chart.SeriesSelectionMode == ChartSelectionMode.Multiple && chart.SelectedSeries is { Count: 2 } && ReferenceEquals(chart.SelectedSeries[1], chart.Series[1]);
        chart.ClearSeriesSelection(); FlushBindings();
        checks["compiled-series-selection-chart-to-model"] = Model.SelectedSeries.Count == 0;
        chart.SelectSeries(chart.Series[0]); chart.SelectSeries(chart.Series[1], true); FlushBindings();
        checks["compiled-series-selection-multiple-reference-identity"] = Model.SelectedSeries.Count == 2 && ReferenceEquals(Model.SelectedSeries[0], chart.Series[0]);
        void Cancel(object? sender, ChartSeriesSelectionChangingEventArgs args) { if (args.CanCancel) args.Cancel = true; }
        chart.SeriesSelectionChanging += Cancel; Model.SelectedSeries = new[] { chart.Series[1] }; FlushBindings();
        checks["compiled-series-selection-cancel-restores-model-binding"] = chart.SelectedSeries is { Count: 2 } && ReferenceEquals(Model.SelectedSeries, chart.SelectedSeries);
        chart.SeriesSelectionChanging -= Cancel; Model.SelectedSeries = new[] { chart.Series[1] }; FlushBindings();
        checks["compiled-series-selection-canceled-binding-remains-active"] = chart.SelectedSeries is { Count: 1 } && ReferenceEquals(chart.SelectedSeries[0], chart.Series[1]);
        chart.Series[1].IsVisible = false; FlushBindings();
        checks["compiled-series-selection-hidden-forced-cleanup"] = Model.SelectedSeries.Count == 0;
        chart.Series[1].IsVisible = true;
        Model.SelectionRange = new(.2, .65); FlushBindings();
        checks["compiled-range-two-way-model-to-chart"] = chart.VisibleRange is { } a && Near(a.XMinimum, 20) && Near(a.XMaximum, 65) && selector.Range == Model.SelectionRange;
        checks["compiled-multichart-normalized-x-different-extents"] = other.VisibleRange is { } b && Near(b.XMinimum, 200) && Near(b.XMaximum, 650) && Near(b.YMinimum, 0) && Near(b.YMaximum, 100);
        chart.SetVisibleFraction(new(.3, .8), ChartInteractionAxes.X); FlushBindings();
        checks["compiled-range-two-way-chart-to-model"] = Near(Model.SelectionRange.Lower, .3) && Near(Model.SelectionRange.Upper, .8);
        other.SetVisibleFraction(new(.1, .4), ChartInteractionAxes.X); FlushBindings();
        checks["compiled-multichart-follower-to-leader-and-selector"] = Near(Model.SelectionRange.Lower, .1) && Near(Model.SelectionRange.Upper, .4) && chart.VisibleRange is { } c && Near(c.XMinimum, 10) && Near(c.XMaximum, 40);
        var lower = selector.GetVisualDescendants().OfType<Control>().Single(control => control.Name == "PART_LowerHandle");
        lower.Focus(); lower.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Right }); FlushBindings();
        checks["compiled-range-keyboard-updates-two-way-model"] = Near(Model.SelectionRange.Lower, .12) && Near(Model.SelectionRange.Upper, .4);
        chart.SelectSeries(chart.Series[0]); FlushBindings();
        Model.SeriesSelectionMode = ChartSelectionMode.None; FlushBindings();
        checks["compiled-series-selection-mode-binding"] = chart.SeriesSelectionMode == ChartSelectionMode.None && Model.SelectedSeries.Count == 0;
        Model.SeriesSelectionMode = ChartSelectionMode.Multiple; chart.ResetViewport(); FlushBindings();
        checks["compiled-range-reset-both-charts-and-selector"] = selector.Range == new ChartRange(0, 1) && Model.SelectionRange == new ChartRange(0, 1) && other.GetVisibleFraction(ChartInteractionAxes.X) == new ChartRange(0, 1);
        examples.IsExpanded = false; FlushBindings();
    }

    private void VerifyTrackballBindings(Dictionary<string, bool> checks)
    {
        var examples = this.FindControl<Expander>("TrackballExamples")!;
        examples.IsExpanded = true; FlushBindings(); examples.BringIntoView(); TrackballChart.BringIntoView(); FlushBindings();
        var chart = TrackballChart;
        Point Project(double x, double y) => new(chart.PlotArea.Left + chart.PlotArea.Width * x / 10, chart.PlotArea.Bottom - chart.PlotArea.Height * y / 100);
        Point Hover(double x, double y)
        {
            using var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
            var point = chart.TranslatePoint(Project(x, y), this) ?? throw new InvalidOperationException("Usage Trackball is not attached to the window.");
            var args = new PointerEventArgs(InputElement.PointerMovedEvent, chart, pointer, this, point, 1, default, KeyModifiers.None);
            chart.RaiseEvent(args); FlushBindings(); return args.GetPosition(chart);
        }
        Border[] Cards() => chart.GetVisualDescendants().OfType<Border>().Where(control => control.Name == "UsageTrackballCard").ToArray();
        ChartTrackballReading? FirstReading() => chart.TrackballReadings.FirstOrDefault(reading => ReferenceEquals(reading.Original.Series, chart.Series[0]));
        bool Endpoints(ChartTrackballReading? reading) => reading?.Segment is { } segment && ReferenceEquals(segment.Start.Item, Model.TrackballPrimaryRecords[0]) &&
            ReferenceEquals(segment.End.Item, Model.TrackballPrimaryRecords[1]) && Equals(segment.Start.StableId, "track-a0") && Equals(segment.End.StableId, "track-a1") && segment.Start.Index == 0 && segment.End.Index == 1;
        var routed = Hover(3, 40);
        var rawX = (routed.X - chart.PlotArea.Left) * 10 / chart.PlotArea.Width;
        checks["compiled-trackball-options-and-template"] = chart.TrackballMode == Model.TrackballMode && chart.TrackballDisplayMode == Model.TrackballDisplayMode &&
            chart.TrackballSnapMode == Model.TrackballSnapMode && ReferenceEquals(chart.TrackballContentTemplate, Model.ActiveTrackballTemplate);
        checks["compiled-trackball-independent-of-disabled-ordinary-tooltip"] = !chart.IsTooltipEnabled && chart.TrackballReadings.Count == 2 && Cards() is [{ Bounds.Width: > 30, DataContext: ChartTooltipContext { Readings.Count: 2 } }];
        var primary = FirstReading();
        checks["compiled-trackball-linear-value-and-original-endpoint-identity"] = Endpoints(primary) && primary is { IsInterpolated: true } &&
            Math.Abs(primary.Point.X - rawX) < 1e-8 && Math.Abs(primary.Point.Y - (20 + (rawX - 1) * 10)) < 1e-8 && primary.Original.Point != primary.Point;
        checks["compiled-trackball-template-real-endpoint-business-controls"] = Cards().SelectMany(card => card.GetVisualDescendants().OfType<TextBlock>()).Any(text => text.Name == "UsageTrackballLeftId" && text.Text == "track-a0") &&
            Cards().SelectMany(card => card.GetVisualDescendants().OfType<TextBlock>()).Any(text => text.Name == "UsageTrackballRightId" && text.Text == "track-a1") &&
            Cards().SelectMany(card => card.GetVisualDescendants().OfType<TextBlock>()).Any(text => text.Name == "UsageTrackballValueY" && text.Text == $"Y {primary!.Point.Y:0.00}");
        Model.TrackballMode = ChartTrackballMode.Horizontal; FlushBindings(); routed = Hover(3, 40);
        var rawY = (chart.PlotArea.Bottom - routed.Y) * 100 / chart.PlotArea.Height;
        var secondaryExpected = rawY >= 40 && rawY <= 80;
        checks["compiled-trackball-horizontal-real-routed-boundary-has-no-extrapolation"] = chart.TrackballReadings.Count == (secondaryExpected ? 2 : 1) &&
            chart.TrackballReadings.Any(reading => ReferenceEquals(reading.Original.Series, chart.Series[1])) == secondaryExpected &&
            chart.TrackballReadings.Where(reading => reading.IsInterpolated).All(reading => Math.Abs(reading.Point.Y - rawY) < 1e-8);
        TrackballBindingDiagnostics.Add(new { scenario = "horizontal-boundary", chart.Bounds, chart.PlotArea, chart.VisibleRange, routed, rawY, secondaryMinimum = 40, secondaryMaximum = 80, secondaryExpected, readings = chart.TrackballReadings.Select(reading => new { reading.Point, reading.IsInterpolated, reading.Original.Index, reading.Original.StableId, title = reading.Original.Series.Title }) });
        routed = Hover(3, 45); rawY = (chart.PlotArea.Bottom - routed.Y) * 100 / chart.PlotArea.Height;
        checks["compiled-trackball-horizontal-binding-and-math"] = chart.TrackballMode == ChartTrackballMode.Horizontal && chart.TrackballReadings.Count == 2 && Endpoints(FirstReading()) &&
            Math.Abs(FirstReading()!.Point.Y - rawY) < 1e-8 && Math.Abs(FirstReading()!.Point.X - (1 + (rawY - 20) / 10)) < 1e-8;
        TrackballBindingDiagnostics.Add(new { scenario = "horizontal", chart.Bounds, chart.PlotArea, chart.VisibleRange, routed, rawY, chart.TrackballMode, chart.TrackballDisplayMode, readings = chart.TrackballReadings.Select(reading => new { reading.Point, reading.IsInterpolated, reading.Original.Index, reading.Original.StableId, title = reading.Original.Series.Title, startIndex = reading.Segment?.Start.Index, endIndex = reading.Segment?.End.Index }), cards = Cards().Select(card => new { card.Bounds, readings = (card.DataContext as ChartTooltipContext)?.Readings.Count }) });
        Model.TrackballDisplayMode = ChartTrackballDisplayMode.FloatAllPoints; FlushBindings(); Hover(3, 45);
        checks["compiled-trackball-floating-real-card-per-series"] = chart.TrackballDisplayMode == ChartTrackballDisplayMode.FloatAllPoints && Cards().Length == 2 &&
            Cards().All(card => card.Bounds.Width > 30 && card.DataContext is ChartTooltipContext { Readings.Count: 1 });
        TrackballBindingDiagnostics.Add(new { scenario = "floating", chart.Bounds, chart.PlotArea, chart.VisibleRange, chart.TrackballMode, chart.TrackballDisplayMode, readings = chart.TrackballReadings.Select(reading => new { reading.Point, reading.IsInterpolated, reading.Original.Index, reading.Original.StableId, title = reading.Original.Series.Title }), cards = Cards().Select(card => new { card.Bounds, readings = (card.DataContext as ChartTooltipContext)?.Readings.Count }) });
        Model.TrackballMode = ChartTrackballMode.Vertical; Model.TrackballSnapMode = ChartTrackballSnapMode.NearestPoint; FlushBindings(); Hover(2.5, 38);
        checks["compiled-trackball-nearest-preserves-real-point-and-item"] = chart.TrackballReadings.Count == 2 && chart.TrackballReadings.All(reading => !reading.IsInterpolated && reading.Segment is null && reading.Point == reading.Original.Point) &&
            ReferenceEquals(FirstReading()?.Original.Item, Model.TrackballPrimaryRecords[0]);
        Model.TrackballMode = ChartTrackballMode.None; Model.TrackballTooltipMode = ChartTooltipMode.NearestPoint; FlushBindings(); Hover(1, 20);
        checks["compiled-trackball-none-restores-ordinary-point-tooltip"] = chart.IsTooltipEnabled && chart.TrackballMode == ChartTrackballMode.None && ReferenceEquals(chart.HoveredPoint?.Item, Model.TrackballPrimaryRecords[0]) &&
            !chart.GetVisualDescendants().OfType<Control>().Any(control => control.Name == "PART_ChartTrackballOverlay");
        Model.TrackballTooltipMode = ChartTooltipMode.NearestSegment; FlushBindings(); routed = Hover(3, 40);
        var segmentHit = chart.HitTestSegment(Project(3, 40), 8);
        checks["compiled-nearest-segment-and-public-hit-query"] = chart.PrimarySegment is { } nearest && ReferenceEquals(nearest.Start.Item, Model.TrackballPrimaryRecords[0]) &&
            ReferenceEquals(nearest.End.Item, Model.TrackballPrimaryRecords[1]) && segmentHit is { } segment && Math.Abs(segment.Fraction - .5) < 1e-8 && Math.Abs(segment.Point.X - 3) < 1e-8 && Math.Abs(segment.Point.Y - 40) < 1e-8;
        foreach (var mode in new[] { ChartTooltipMode.InterpolateX, ChartTooltipMode.InterpolateY })
        {
            Model.TrackballTooltipMode = mode; FlushBindings(); routed = Hover(3, 40);
            var actual = chart.HoveredSegments.FirstOrDefault(item => ReferenceEquals(item.Series, chart.Series[0]));
            var value = mode == ChartTooltipMode.InterpolateX ? (routed.X - chart.PlotArea.Left) * 10 / chart.PlotArea.Width : (chart.PlotArea.Bottom - routed.Y) * 100 / chart.PlotArea.Height;
            var fraction = mode == ChartTooltipMode.InterpolateX ? (value - 1) / 4 : (value - 20) / 40;
            checks[$"compiled-tooltip-{mode}-binding-and-raw-endpoints"] = chart.TooltipMode == mode && actual is not null && ReferenceEquals(actual.Start.Item, Model.TrackballPrimaryRecords[0]) &&
                ReferenceEquals(actual.End.Item, Model.TrackballPrimaryRecords[1]) && Math.Abs(actual.Fraction - fraction) < 1e-8 && Math.Abs(actual.Point.X - (1 + 4 * fraction)) < 1e-8 && Math.Abs(actual.Point.Y - (20 + 40 * fraction)) < 1e-8;
        }
        foreach (var mode in new[] { ChartTooltipMode.SharedX, ChartTooltipMode.SharedY })
        {
            Model.TrackballTooltipMode = mode; FlushBindings(); Hover(3, 40);
            checks[$"compiled-trackball-none-{mode}-retains-real-samples"] = chart.TrackballMode == ChartTrackballMode.None && chart.HoveredSegments.Count == 0 && chart.HoveredPoints.Count == 2 &&
                !chart.GetVisualDescendants().OfType<Control>().Any(control => control.Name == "PART_ChartTrackballOverlay") &&
                chart.HoveredPoints.All(hit => hit.Item is TrackballUsageRecord record && hit.Point == new ChartPoint(record.X, record.Y));
        }
        Model.TrackballMode = ChartTrackballMode.Vertical; Model.TrackballDisplayMode = ChartTrackballDisplayMode.GroupAllPoints; Model.TrackballSnapMode = ChartTrackballSnapMode.LinearInterpolation; FlushBindings();
        Model.TrackballUpdateCommand.Execute(null); routed = Hover(3, 40); rawX = (routed.X - chart.PlotArea.Left) * 10 / chart.PlotArea.Width;
        checks["compiled-trackball-endpoint-inpc-updates-estimate-with-original-items"] = Endpoints(FirstReading()) && FirstReading()!.Segment!.End.Point.Y == 80 &&
            Math.Abs(FirstReading()!.Point.Y - (20 + (rawX - 1) * 15)) < 1e-8;
        chart.TrackballFormatter = _ => null; FlushBindings(); Hover(3, 40);
        checks["compiled-trackball-null-formatter-keeps-business-template"] = chart.TrackballReadings.Count == 2 && Cards() is [{ Bounds.Width: > 30 }] &&
            Cards().SelectMany(card => card.GetVisualDescendants().OfType<TextBlock>()).Any(text => text.Text == "track-a0");
        chart.TrackballFormatter = null; Model.UseBusinessTrackball = false; FlushBindings(); Hover(3, 40); var removed = Cards().Length == 0 && chart.TrackballReadings.Count == 2;
        Model.UseBusinessTrackball = true; FlushBindings(); Hover(3, 40);
        checks["compiled-trackball-template-binding-removes-and-restores-real-card"] = removed && ReferenceEquals(chart.TrackballContentTemplate, Model.ActiveTrackballTemplate) && Cards() is [{ Bounds.Width: > 30 }];
        var content = Content; Content = null; FlushBindings();
        var detached = Model.TrackballPrimaryRecords.Concat(Model.TrackballSecondaryRecords).All(record => record.SubscriberCount == 0) && chart.TrackballReadings.Count == 0 && Cards().Length == 0;
        Content = content; FlushBindings(); Hover(3, 40);
        checks["compiled-trackball-detach-and-reattach-cleans-and-restores-context"] = detached && Model.TrackballPrimaryRecords.Concat(Model.TrackballSecondaryRecords).All(record => record.SubscriberCount > 0) && chart.TrackballReadings.Count == 2 && Cards().Length == 1;
        using var exitPointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        chart.RaiseEvent(new PointerEventArgs(InputElement.PointerExitedEvent, chart, exitPointer, this, new Point(-1, -1), 1, default, KeyModifiers.None)); FlushBindings();
        checks["compiled-trackball-pointer-exit-clears-presentation-and-readings"] = chart.TrackballReadings.Count == 0 && chart.HoveredSegments.Count == 0 && chart.PrimarySegment is null && Cards().Length == 0;
        Model.TrackballPrimaryRecords[1].Y = 60; Model.TrackballTooltipMode = ChartTooltipMode.NearestPoint; FlushBindings();
    }

    private void VerifyControlTemplateBindings(Dictionary<string, bool> checks)
    {
        this.FindControl<Expander>("ControlTemplateExamples")!.IsExpanded = true; FlushBindings();
        var chart = TemplateChart; var series = chart.Series[0];
        Border? PrimaryCard() => chart.GetVisualDescendants().OfType<Border>().FirstOrDefault(control => control.Name == "UsageDataLabelCard" &&
            control.DataContext is ChartDataLabelContext context && ReferenceEquals(context.Data.Item, Model.TemplateRecords[0]));
        Border? LegendCard(ChartSeries item) => chart.GetVisualDescendants().OfType<Border>().FirstOrDefault(control => control.Name == "UsageLegendCard" &&
            control.DataContext is ChartLegendItem context && ReferenceEquals(context.Series, item));
        var card = PrimaryCard(); var context = card?.DataContext as ChartDataLabelContext;
        checks["compiled-control-template-properties"] = ReferenceEquals(chart.LegendItemTemplate, Model.ActiveLegendTemplate) && ReferenceEquals(series.DataLabelTemplate, Model.ActiveDataLabelTemplate) && chart.IsLegendScrollable;
        checks["compiled-data-label-original-business-context"] = card is { Bounds.Width: > 20, Bounds.Height: > 15 } && context is not null &&
            ReferenceEquals(context.Series, series) && ReferenceEquals(context.Data.Item, Model.TemplateRecords[0]) && context.Data.SourceIndex == 0 &&
            Equals(context.Data.StableId, "template-primary") && context.Data.Point == new ChartPoint(1, 37) && context.Text == "37";
        checks["compiled-data-label-nested-business-template"] = card is not null && card.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Name == "UsageDataLabelRecordId" && text.Text == "template-primary") &&
            card.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Name == "UsageDataLabelRecordName" && text.Text == "直营订单");
        Model.TemplateSeriesTitle = "直营订单 · 已更新"; FlushBindings();
        checks["compiled-legend-context-title-inpc"] = series.Title == Model.TemplateSeriesTitle && LegendCard(series)?.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Name == "UsageLegendTitle" && text.Text == Model.TemplateSeriesTitle) == true;
        Model.UpdateTemplateValueCommand.Execute(null); FlushBindings(); card = PrimaryCard();
        checks["compiled-data-label-inpc-updates-point-and-real-text"] = card is not null && card.DataContext is ChartDataLabelContext updated && ReferenceEquals(updated.Data.Item, Model.TemplateRecords[0]) && updated.Data.Point == new ChartPoint(1, 54) &&
            card.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Name == "UsageDataLabelValue" && text.Text == "54 单");
        Resources["UsageControlTemplateAccent"] = Brushes.MediumVioletRed; FlushBindings();
        checks["compiled-data-label-inherits-dynamic-resource"] = PrimaryCard()?.BorderBrush is ISolidColorBrush accent && accent.Color == Colors.MediumVioletRed;
        series.DataLabelFormatter = _ => null; FlushBindings();
        checks["compiled-data-label-template-builds-with-empty-formatted-text"] = PrimaryCard() is { Bounds.Width: > 20 } && PrimaryCard()!.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "template-primary");
        series.DataLabelFormatter = null; FlushBindings();
        var scroll = chart.GetVisualDescendants().OfType<ScrollViewer>().SingleOrDefault(control => control.Name == "PART_ChartLegendScrollViewer");
        checks["compiled-legend-real-scroll-viewport"] = scroll is { Viewport.Height: > 0 } && scroll.Extent.Height > scroll.Viewport.Height && scroll.Viewport.Height <= 205.01;
        if (scroll is not null) { scroll.Offset = new Vector(0, Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height)); FlushBindings(); }
        var last = chart.Series[^1]; var tail = LegendCard(last);
        checks["compiled-legend-scroll-reaches-real-tail"] = scroll is { Offset.Y: > 0 } && tail is { Bounds.Width: > 20 } &&
            tail.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Name == "UsageLegendTitle" && text.Text == "候选渠道 16") &&
            chart.LegendItems.Any(item => ReferenceEquals(item.Series, last) && item.Bounds.Width > 0 && item.Bounds.Height > 0);
        chart.ToggleSeriesVisibility(last); FlushBindings();
        checks["compiled-legend-tail-toggle-updates-nested-binding"] = last.IsVisible && LegendCard(last)?.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Name == "UsageLegendVisible" && text.IsVisible) == true;
        chart.ToggleSeriesVisibility(last); FlushBindings();
        var oldLabel = PrimaryCard(); Model.UseDataLabelControlTemplate = false; FlushBindings();
        checks["compiled-data-label-null-template-unloads-control"] = series.DataLabelTemplate is null && PrimaryCard() is null && oldLabel is not null && oldLabel.GetVisualParent() is null;
        var oldLegend = LegendCard(last); Model.UseLegendControlTemplate = false; FlushBindings();
        checks["compiled-legend-null-template-unloads-control"] = chart.LegendItemTemplate is null && !chart.GetVisualDescendants().OfType<Border>().Any(control => control.Name == "UsageLegendCard") && oldLegend is not null && oldLegend.GetVisualParent() is null;
        Model.UseDataLabelControlTemplate = true; Model.UseLegendControlTemplate = true; FlushBindings();
        checks["compiled-control-templates-restore-real-content"] = PrimaryCard() is { Bounds.Width: > 20 } && chart.GetVisualDescendants().OfType<Border>().Any(control => control.Name == "UsageLegendCard" && control.Bounds.Width > 20);
        var content = Content; Content = null; FlushBindings();
        checks["compiled-control-template-detach-releases-business-source"] = Model.TemplateRecords.All(record => record.SubscriberCount == 0) && !chart.GetVisualDescendants().OfType<Border>().Any(control => control.Name == "UsageDataLabelCard");
        Content = content; FlushBindings();
        checks["compiled-control-template-reattach-restores-original-items"] = Model.TemplateRecords.All(record => record.SubscriberCount > 0) && PrimaryCard()?.DataContext is ChartDataLabelContext attached && ReferenceEquals(attached.Data.Item, Model.TemplateRecords[0]);
        Model.TemplateRecords[0].Value = 37; Model.TemplateSeriesTitle = "直营订单"; FlushBindings();
    }

    private void VerifyReadingBindings(Dictionary<string, bool> checks)
    {
        var examples = this.FindControl<Expander>("TooltipReadingExamples")!;
        examples.IsExpanded = true; FlushBindings();
        var chart = ReadingChart;
        Point Project(double x, double y, bool log = false) => new(chart.PlotArea.Left + chart.PlotArea.Width * x / 10,
            chart.PlotArea.Bottom - chart.PlotArea.Height * (log ? Math.Log10(y) / 4 : y / 100));
        Border? Card() => chart.GetVisualDescendants().OfType<Border>().SingleOrDefault(control => control.Name == "UsageTooltipCard");
        void Hover(Point point)
        {
            using var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
            var target = chart.TranslatePoint(point, this) ?? throw new InvalidOperationException("Usage tooltip chart is not in the window.");
            chart.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, chart, pointer, this, target, 1, default, KeyModifiers.None));
            FlushBindings();
        }
        var anchor = Project(2, 37);
        Hover(anchor);
        var card = Card(); var context = card?.DataContext as ChartTooltipContext;
        checks["compiled-tooltip-template-real-controls-and-rows"] = card is { Bounds.Width: > 50, Bounds.Height: > 30 } && context is { Hits.Count: 2 } &&
            card.GetVisualDescendants().OfType<TextBlock>().Count(text => text.Name == "UsageTooltipRecordId" && !string.IsNullOrEmpty(text.Text)) == 2;
        checks["compiled-shared-y-retains-original-records"] = context is not null && ReferenceEquals(context.Primary.Item, Model.ReadingRecords[1]) &&
            context.Hits.Any(hit => ReferenceEquals(hit.Item, Model.ReadingVolumeRecords[1]) && hit.Point == new ChartPoint(7, Math.Pow(10, 1.48)) && Equals(hit.StableId, "volume-b"));
        checks["compiled-shared-y-independent-math-oracle"] = chart.HoveredPoints.Count == 2 && chart.Series.All(series =>
        {
            var source = ReferenceEquals(series, chart.Series[0]) ? Model.ReadingRecords : Model.ReadingVolumeRecords;
            var expected = source.Select((record, ordinal) => new { Record = record, Ordinal = ordinal, Screen = Project(record.Position, record.Value, !ReferenceEquals(series, chart.Series[0])) })
                .OrderBy(item => Math.Abs(item.Screen.Y - anchor.Y)).ThenBy(item => Math.Abs(item.Screen.X - anchor.X)).ThenByDescending(item => item.Ordinal).First();
            return chart.HoveredPoints.Any(hit => ReferenceEquals(hit.Series, series) && ReferenceEquals(hit.Item, expected.Record) && hit.Index == expected.Ordinal);
        });
        Model.ReadingTooltipMode = ChartTooltipMode.NearestPoint; FlushBindings(); Hover(anchor);
        var nearest = chart.HoveredPoints.Count == 1 && ReferenceEquals(chart.HoveredPoints[0].Item, Model.ReadingRecords[1]);
        Model.ReadingTooltipMode = ChartTooltipMode.SharedX; FlushBindings(); Hover(anchor);
        var sharedX = chart.HoveredPoints.Count == 2 && chart.HoveredPoints.Any(hit => ReferenceEquals(hit.Item, Model.ReadingVolumeRecords[0]));
        Model.ReadingTooltipMode = ChartTooltipMode.SharedY; FlushBindings(); Hover(anchor);
        checks["compiled-tooltip-mode-binding-selects-real-x-and-y-neighbours"] = nearest && sharedX && chart.TooltipMode == ChartTooltipMode.SharedY && chart.HoveredPoints.Any(hit => ReferenceEquals(hit.Item, Model.ReadingVolumeRecords[1]));
        Model.ReadingCrosshairDirection = ChartCrosshairDirection.Horizontal; Model.ShowReadingLabels = false; Model.ShowReadingCrosshair = false; FlushBindings();
        checks["compiled-crosshair-direction-label-and-line-bindings"] = chart.CrosshairDirection == ChartCrosshairDirection.Horizontal && !chart.ShowCrosshairLabels && !chart.ShowCrosshair;
        checks["compiled-hidden-labels-keep-independent-axis-readings"] = chart.AxisReadings.Count == 3 &&
            chart.AxisReadings.Any(row => ReferenceEquals(row.Axis, chart.XAxis) && row.IsHorizontal && Math.Abs(row.Value - 2) < 1e-8) &&
            chart.AxisReadings.Any(row => ReferenceEquals(row.Axis, chart.YAxis) && !row.IsHorizontal && Math.Abs(row.Value - 37) < 1e-8) &&
            chart.AxisReadings.Any(row => ReferenceEquals(row.Axis, chart.Axes[0]) && !row.IsHorizontal && Math.Abs(row.Value - Math.Pow(10, 1.48)) < 1e-7);
        Model.ReadingCrosshairDirection = ChartCrosshairDirection.Vertical; Model.ShowReadingLabels = true; Model.ShowReadingCrosshair = true; FlushBindings();
        checks["compiled-crosshair-options-restore-after-binding-update"] = chart.CrosshairDirection == ChartCrosshairDirection.Vertical && chart.ShowCrosshairLabels && chart.ShowCrosshair;
        Resources["UsageTooltipBadgeBrush"] = Brushes.LimeGreen; FlushBindings();
        checks["compiled-tooltip-inherits-live-dynamic-resource"] = Card()?.GetVisualDescendants().OfType<Border>().Any(control => control.Name == "UsageTooltipBadge" && control.Background is ISolidColorBrush brush && brush.Color == Colors.LimeGreen) == true;
        var oldTheme = RequestedThemeVariant;
        RequestedThemeVariant = ThemeVariant.Light; FlushBindings(); Hover(anchor);
        var light = (Card()?.Background as ISolidColorBrush)?.Color;
        RequestedThemeVariant = ThemeVariant.Dark; FlushBindings(); Hover(anchor);
        checks["compiled-tooltip-template-follows-light-dark-resources"] = light is not null && Card()?.Background is ISolidColorBrush dark && dark.Color != light;
        RequestedThemeVariant = oldTheme; FlushBindings(); Hover(anchor);
        var before = Model.ReadingRecords[1].Value;
        Model.ReadingRecords[1].Value = 46; FlushBindings(); Hover(Project(2, 46));
        checks["compiled-tooltip-business-property-updates-text-and-hit"] = Card()?.DataContext is ChartTooltipContext updated && updated.Hits.Any(hit => ReferenceEquals(hit.Item, Model.ReadingRecords[1]) && hit.Point.Y == 46) &&
            Card()!.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == 46d.ToString("0.00"));
        Model.ReadingRecords[1].Value = before; FlushBindings(); Hover(anchor);
        var previousCard = Card();
        Model.UseRichReadingTooltip = false; FlushBindings();
        checks["compiled-tooltip-template-switch-unloads-real-content"] = chart.TooltipContentTemplate is null && Card() is null && previousCard is not null && previousCard.GetVisualParent() is null && chart.HoveredPoints.Count == 2;
        Model.UseRichReadingTooltip = true; FlushBindings(); Hover(anchor);
        checks["compiled-tooltip-template-switch-restores-nonempty-content"] = ReferenceEquals(chart.TooltipContentTemplate, Model.ReadingTooltipTemplate) && Card() is { Bounds.Width: > 50 } && chart.HoveredPoints.Count == 2;
        checks["compiled-tooltip-record-subscriptions-active"] = Model.ReadingRecords.All(record => record.SubscriberCount > 0) && Model.ReadingVolumeRecords.All(record => record.SubscriberCount > 0);
        var content = Content; Content = null; FlushBindings();
        checks["compiled-tooltip-detach-releases-items-and-presentation"] = Model.ReadingRecords.All(record => record.SubscriberCount == 0) && Model.ReadingVolumeRecords.All(record => record.SubscriberCount == 0) &&
            Card() is null && chart.AxisReadings.Count == 0 && chart.HoveredPoints.Count == 0;
        Content = content; FlushBindings(); Hover(Project(2, 37));
        checks["compiled-tooltip-reattach-restores-context-and-bindings"] = Model.ReadingRecords.All(record => record.SubscriberCount > 0) && Card()?.DataContext is ChartTooltipContext { Hits.Count: 2 };
        chart.IsTooltipEnabled = false; FlushBindings();
        checks["compiled-tooltip-disabled-clears-visual-and-readings"] = Card() is null && chart.AxisReadings.Count == 0 && chart.HoveredPoints.Count == 0;
        chart.IsTooltipEnabled = true; Model.ReadingCrosshairDirection = ChartCrosshairDirection.Both; FlushBindings();
    }

    private void VerifyCommonFollowupBindings(Dictionary<string, bool> checks)
    {
        InheritedChart.ShowLegend = false;
        var expected = InheritedChart.PlotArea;
        Model.IsLegendFloating = true; InheritedChart.ShowLegend = true; FlushBindings();
        checks["compiled-floating-legend-binding-and-layout"] = InheritedChart.IsLegendFloating && InheritedChart.PlotArea == expected &&
            InheritedChart.LegendItems.Count == 1 && expected.Contains(InheritedChart.LegendItems[0].Bounds) &&
            Math.Abs(InheritedChart.LegendItems[0].Bounds.Center.X - expected.Center.X) < .001;
        Model.IsLegendFloating = false; FlushBindings();
        var rectangle = InheritedChart.Annotations.OfType<RectangleAnnotation>().Single();
        var ellipse = InheritedChart.Annotations.OfType<EllipseAnnotation>().Single();
        var arrow = InheritedChart.Annotations.OfType<ArrowAnnotation>().Single();
        checks["compiled-shape-annotation-properties"] = rectangle.X1 == 8 && rectangle.X2 == 52 && ellipse.Y1 == 32 && ellipse.Y2 == 54 &&
            arrow.X1 == 122 && arrow.X2 == 175 && arrow.CoordinateUnit == ChartAnnotationCoordinateUnit.Pixel;
        checks["compiled-shape-annotation-resource-inheritance"] = new ChartAnnotation[] { rectangle, ellipse, arrow }
            .All(annotation => annotation.Stroke is ISolidColorBrush brush && brush.Color == Colors.HotPink);
        var series = InheritedChart.Series.Single(); var record = series.CreateSnapshot().Points[1];
        var range = InheritedChart.VisibleRange!.Value; var plot = InheritedChart.PlotArea;
        var position = new Point(plot.Left + (record.Point.X - range.XMinimum) / (range.XMaximum - range.XMinimum) * plot.Width,
            plot.Bottom - (record.Point.Y - range.YMinimum) / (range.YMaximum - range.YMinimum) * plot.Height);
        var hit = new ChartPointHit(series, record.Point, record.SourceIndex, position) { Item = record.Item, StableId = record.StableId };
        var received = false;
        void Cancel(object? sender, ChartSelectionChangingEventArgs args)
        { if (args.Reason == ChartSelectionChangeReason.ExternalValue && args.CanCancel) { received = true; args.Cancel = true; } }
        InheritedChart.SelectionChanging += Cancel;
        try
        {
            Model.SelectedPoints = new[] { hit }; FlushBindings();
            checks["selection-changing-cancels-two-way-assignment"] = received && Model.SelectedPoints.Count == 0 && InheritedChart.SelectedPoints!.Count == 0;
        }
        finally { InheritedChart.SelectionChanging -= Cancel; }
        Model.SelectedPoints = new[] { hit }; FlushBindings();
        checks["selection-changing-rollback-preserves-binding"] = InheritedChart.SelectedPoints!.Count == 1 && Equals(InheritedChart.SelectedPoints[0].StableId, record.StableId);
        InheritedChart.ClearSelection(); FlushBindings();
    }

    private void VerifyCommonBindings(Dictionary<string, bool> checks)
    {
        var series = InheritedChart.Series.Single();
        checks["compiled-c2-visibility-and-crosshair"] = series.IsVisible == Model.IsStoreVisible && InheritedChart.ShowCrosshair == Model.ShowCrosshair;
        Model.IsStoreVisible = false; FlushBindings(); var hidden = !series.IsVisible;
        series.SetCurrentValue(ChartSeries.IsVisibleProperty, true); FlushBindings();
        checks["compiled-c2-visibility-two-way"] = hidden && Model.IsStoreVisible && series.IsVisible;
        Model.SelectionMode = ChartSelectionMode.Multiple; Model.ShowCrosshair = false; Model.ShowDataLabels = true; FlushBindings();
        checks["compiled-c2-selection-crosshair-label-updates"] = InheritedChart.SelectionMode == ChartSelectionMode.Multiple && !InheritedChart.ShowCrosshair && series.ShowDataLabels && series.MarkerShape == ChartMarkerShape.Diamond;
        var record = series.CreateSnapshot().Points[1]; var range = InheritedChart.VisibleRange!.Value; var plot = InheritedChart.PlotArea;
        var point = new Point(plot.Left + (1 - range.XMinimum) / (range.XMaximum - range.XMinimum) * plot.Width,
            plot.Bottom - (record.Point.Y - range.YMinimum) / (range.YMaximum - range.YMinimum) * plot.Height);
        var hit = InheritedChart.HitTestPoint(point) ?? throw new InvalidOperationException("C2 binding probe did not hit the original record.");
        InheritedChart.SetCurrentValue(CartesianChart.SelectedPointsProperty, new[] { hit }); FlushBindings();
        checks["compiled-c2-selected-points-two-way"] = Model.SelectedPoints.Count == 1 && Equals(Model.SelectedPoints[0].StableId, record.StableId);
        InheritedChart.ClearSelection(); FlushBindings();
        checks["compiled-c2-clear-selection-updates-viewmodel"] = Model.SelectedPoints.Count == 0;
        checks["compiled-c2-annotation-inheritance"] = InheritedChart.Annotations.OfType<TextAnnotation>().Single().Text == Model.AxisTitle &&
            InheritedChart.Annotations.OfType<TextAnnotation>().Single().Foreground is ISolidColorBrush annotationBrush && annotationBrush.Color == Colors.HotPink;
        Model.ShowCrosshair = true; Model.ShowDataLabels = false; Model.SelectionMode = ChartSelectionMode.Single; FlushBindings();
    }

    private void VerifyAxisBindings(Dictionary<string, bool> checks)
    {
        var examples = this.FindControl<Expander>("AxisExamples")!;
        examples.IsExpanded = true;
        FlushBindings();
        var chart = this.FindControl<CartesianChart>("AxisFeaturesChart")!;
        var secondary = chart.Axes.Single();
        checks["compiled-axis-minor-grid-binding"] = chart.XAxis.ShowMinorGridLines == Model.ShowMinorGrid && chart.XAxis.MinorTickCount == 3 && chart.YAxis.MinorInterval == 5;
        checks["compiled-break-and-band-bindings"] = chart.YAxis.ScaleBreaks.Single().Start == Model.BreakStart && chart.XAxis.PlotBands.Single().Start == Model.BandStart;
        Model.ShowMinorGrid = false;
        Model.BandStart = .5;
        Model.BreakStart = 40;
        FlushBindings();
        checks["nested-axis-property-updates"] = !chart.XAxis.ShowMinorGridLines && chart.XAxis.PlotBands.Single().Start == .5 && chart.YAxis.ScaleBreaks.Single().Start == 40;
        Resources["UsageAxisBrush"] = Brushes.MediumVioletRed;
        FlushBindings();
        checks["nested-axis-dynamic-resource"] = secondary.LabelBrush is ISolidColorBrush label && label.Color == Colors.MediumVioletRed &&
            chart.XAxis.PlotBands.Single().Foreground is ISolidColorBrush band && band.Color == Colors.MediumVioletRed;
        Model.FocusSecondaryCommand.Execute(null);
        FlushBindings();
        checks["secondary-axis-viewmodel-command"] = secondary.ViewMinimum == .3 && secondary.ViewMaximum == .7 && chart.Viewport is null;
        secondary.SetCurrentValue(ChartAxis.ViewMinimumProperty, .25d);
        secondary.SetCurrentValue(ChartAxis.ViewMaximumProperty, .75d);
        FlushBindings();
        checks["secondary-axis-viewport-two-way"] = Model.SecondaryMinimum == .25 && Model.SecondaryMaximum == .75;
        chart.ResetViewportCommand.Execute(null);
        FlushBindings();
        checks["reset-command-clears-secondary-two-way"] = Model.SecondaryMinimum is null && Model.SecondaryMaximum is null && secondary.ViewMinimum is null && secondary.ViewMaximum is null;
        checks["compiled-axis-layout-and-zoom-limits"] = chart.PlotArea.Width > 100 && chart.PlotArea.Height > 80 && chart.XAxis.MaximumZoom == 8 && chart.YAxis.MaximumZoom == 4 && secondary.MaximumZoom == 10;
        checks["compiled-break-source-binding"] = ReferenceEquals(chart.XAxis.ScaleBreaksSource, Model.AxisBreaks) && chart.XAxis.IsInScaleBreak(2.5);
        var addedBreak = new ChartScaleBreak { Start = 3.2, End = 3.5 };
        Model.AxisBreaks.Add(addedBreak); FlushBindings();
        var added = chart.XAxis.IsInScaleBreak(3.3);
        addedBreak.End = 3.7; FlushBindings();
        var updated = chart.XAxis.IsInScaleBreak(3.6);
        Model.AxisBreaks.Remove(addedBreak); FlushBindings();
        checks["compiled-break-source-collection-updates"] = added && updated && !chart.XAxis.IsInScaleBreak(3.3);
    }

    private async Task RunSmokeAsync()
    {
        var index = Array.IndexOf(Program.Arguments, "--smoke-output");
        var output = index >= 0 && index + 1 < Program.Arguments.Length ? Program.Arguments[index + 1] : "artifacts/platform/charts-usage.json";
        Dictionary<string, bool> checks = [];
        string? error = null;
        try { await Task.Delay(80); checks = VerifyBindings(); foreach (var check in await VerifyExportBindingsAsync()) checks[check.Key] = check.Value; foreach (var check in await VerifyIncrementalStreamingSessionAsync()) checks[check.Key] = check.Value; foreach (var check in await VerifyMultiChartBindingsAsync()) checks[check.Key] = check.Value; checks["native-window-created"] = TryGetPlatformHandle() is { Handle: not 0 }; }
        catch (Exception exception) { error = exception.ToString(); }
        var passed = error is null && checks.Count > 0 && checks.Values.All(value => value);
        var path = Path.GetFullPath(output);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(new { passed, timestampUtc = DateTimeOffset.UtcNow, executionKind = "native-window-programmatic-binding-checks", physicalInputValidated = false, performanceMeasured = false, checks, exportDiagnostics = ExportBindingDiagnostics, trackballDiagnostics = TrackballBindingDiagnostics, accessibilityDiagnostics = AccessibilityBindingDiagnostics, lineAreaDiagnostics = LineAreaBindingDiagnostics, barComparisonDiagnostics = BarComparisonBindingDiagnostics, scatterBubbleDiagnostics = ScatterBubbleBindingDiagnostics, circularDiagnostics = CircularBindingDiagnostics, polarDiagnostics = PolarBindingDiagnostics, gaugeMicroDiagnostics = GaugeMicroBindingDiagnostics, analysisDiagnostics = AnalysisBindingDiagnostics, statisticsDiagnostics = StatisticsBindingDiagnostics, financialDiagnostics = FinancialBindingDiagnostics, technicalTrendsDiagnostics = TechnicalTrendsBindingDiagnostics, engineeringDiagnostics = EngineeringBindingDiagnostics, advancedComparisonDiagnostics = AdvancedComparisonBindingDiagnostics, hierarchyDiagnostics = HierarchyBindingDiagnostics, relationDiagnostics = RelationBindingDiagnostics, mapDiagnostics = MapBindingDiagnostics, c6CompletionDiagnostics = C6CompletionDiagnostics, c5CompletionDiagnostics = C5CompletionDiagnostics, levelOfDetailDiagnostics = LevelOfDetailBindingDiagnostics, efficientHitDiagnostics = EfficientHitBindingDiagnostics, incrementalStreamingDiagnostics = IncrementalStreamingBindingDiagnostics, renderResourceDiagnostics = RenderResourceBindingDiagnostics, multiChartDiagnostics = MultiChartBindingDiagnostics, error }, new JsonSerializerOptions { WriteIndented = true }));
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.Shutdown(passed ? 0 : 1);
        else Close();
    }
}

