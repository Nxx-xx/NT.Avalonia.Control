using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class MainWindow
{
    internal CartesianChart FinancialRawChart => this.FindControl<CartesianChart>("UsageFinancialRaw")!;
    internal CartesianChart FinancialHeikinChart => this.FindControl<CartesianChart>("UsageFinancialHeikin")!;
    internal CartesianChart FinancialPointFigureChart => this.FindControl<CartesianChart>("UsageFinancialPointFigure")!;
    internal FinancialChart FinancialHost => this.FindControl<FinancialChart>("UsageFinancialHost")!;
    internal List<object> FinancialBindingDiagnostics { get; } = [];

    private void ConfigureFinancialBindings() => FinancialHost.SetData(Model.FinancialHostPoints);
    private void RefreshFinancialHost(object? sender, RoutedEventArgs args) => ConfigureFinancialBindings();

    internal void VerifyFinancialBindings(Dictionary<string, bool> checks)
    {
        var expander = this.FindControl<Expander>("FinancialExamples")!;
        expander.IsExpanded = true; FlushBindings();
        var rawChart = FinancialRawChart; var heikinChart = FinancialHeikinChart; var pfChart = FinancialPointFigureChart;
        var candle = (CandlestickSeries)rawChart.Series[0]; var ohlc = (OhlcSeries)rawChart.Series[1];
        var ha = (HeikinAshiSeries)heikinChart.Series[0]; var pf = (PointAndFigureSeries)pfChart.Series[0];
        var host = FinancialHost;
        void Check(string key, bool value) => checks["financial-compiled-" + key] = value;
        Check("types-share-the-public-function-assembly", new[] { typeof(ChartFinancialPoint), typeof(ChartFinancialSample),
            typeof(ChartFinancialTransforms), typeof(FinancialSeries), typeof(FinancialChart), typeof(CandlestickSeries),
            typeof(OhlcSeries), typeof(HiloSeries), typeof(HeikinAshiSeries), typeof(RenkoSeries), typeof(KagiSeries),
            typeof(PointAndFigureSeries), typeof(FinancialVolumeSeries) }.All(type => type.Assembly == typeof(CartesianChart).Assembly));
        Check("all-financial-charts-have-layout", new[] { rawChart, heikinChart, pfChart, host.Chart }.All(chart => chart.PlotArea.Width > 100 && chart.PlotArea.Height > 100));
        Check("compiled-sources-and-business-paths", new FinancialSeries[] { candle, ohlc, ha, pf }.All(series =>
            ReferenceEquals(series.ItemsSource, Model.FinancialRecords) && series.TimePath == "Time" && series.OpenPath == "Open" &&
            series.HighPath == "High" && series.LowPath == "Low" && series.ClosePath == "Close" && series.VolumePath == "Volume" && series.IdPath == "Id"));
        var original = candle.CreateSnapshot(); var first = original.Points[1];
        var originalHa = ha.CreateSnapshot(); var haFirst = originalHa.Points[0];
        Check("raw-glyph-retains-unmodified-business-record", first.Financial is { Open: 11, High: 14, Low: 10, Close: 12,
            SourceStartIndex: 1, SourceEndIndex: 1 } && ReferenceEquals(first.Item, Model.FinancialRecords[1]) && Equals(first.StableId, "financial-1"));
        Check("ohlc-and-heikin-default-transform-contracts", ohlc.Style == ChartFinancialStyle.Ohlc &&
            ha.Transform == ChartFinancialTransformKind.HeikinAshi && haFirst.Financial is { Open: 9.5, Close: 9.75 } &&
            haFirst.Financial.SourceEnd.Close == 10 && ReferenceEquals(haFirst.Item, Model.FinancialRecords[0]));
        Check("point-figure-column-and-original-range", pf.CreateSnapshot().Points is { Count: 3 } columns &&
            columns[1].Financial is { SourceStartIndex: 2, SourceEndIndex: 4, Open: 14, Close: 8, BoxCount: 3 } &&
            ReferenceEquals(columns[1].Item, Model.FinancialRecords[4]));
        var range = rawChart.VisibleRange!.Value; var plot = rawChart.PlotArea;
        var position = new Point(plot.Left + (first.Point.X - range.XMinimum) / (range.XMaximum - range.XMinimum) * plot.Width,
            plot.Bottom - (first.Point.Y - range.YMinimum) / (range.YMaximum - range.YMinimum) * plot.Height);
        var hit = rawChart.HitTestPoint(position, 1);
        Check("financial-hit-exposes-real-business-ohlc", hit?.Financial?.SourceEnd.Close == 12 && ReferenceEquals(hit?.Item, Model.FinancialRecords[1]) && Equals(hit?.StableId, "financial-1"));
        if (hit is not null) rawChart.SetCurrentValue(CartesianChart.SelectedPointProperty, hit);
        FlushBindings();
        Check("selected-point-is-two-way-original-identity", ReferenceEquals(Model.FinancialSelectedPoint?.Item, Model.FinancialRecords[1]) && Model.FinancialSelectionText.Contains("financial-1", StringComparison.Ordinal));
        var oldVersion = candle.SourceVersion; Model.FinancialUpdateCommand.Execute(null); FlushBindings();
        Check("inpc-prices-refresh-and-retain-old-snapshot", candle.SourceVersion > oldVersion && original.Points[1].Financial?.Close == 12 &&
            candle.CreateSnapshot().Points[1].Financial?.Close == 13 && ha.CreateSnapshot().Points[1].Financial?.Close == 12 && originalHa.Points[1].Financial?.Close == 11.75);
        Check("host-keeps-a-defensive-explicit-snapshot", host.Data[1].Close == 12);
        ConfigureFinancialBindings(); FlushBindings();
        Check("host-refresh-publishes-new-data-and-axis-context", host.Data[1].Close == 13 && host.PriceSeries.YAxisKey == host.PriceAxis.Key &&
            host.VolumeSeries.YAxisKey == host.VolumeAxis.Key && !ReferenceEquals(host.PriceAxis, host.VolumeAxis) && host.VolumeSeries.IsVisible);
        Check("weekend-compresses-with-real-dates-retained", host.MapTime(Model.FinancialRecords[0].Time) == 0 && host.MapTime(Model.FinancialRecords[1].Time) == 1 &&
            host.TryGetTime(1, out var tradingTime) && tradingTime == Model.FinancialRecords[1].Time &&
            double.IsNaN(host.MapTime(Model.FinancialRecords[0].Time.AddDays(1))));
        this.FindControl<ComboBox>("UsageFinancialBoxSize")!.SelectedItem = 4.0; FlushBindings();
        Check("box-control-two-way-binding-rebuilds-columns", Model.FinancialBoxSize == 4 && pf.BoxSize == 4 &&
            pf.CreateSnapshot().Points.Count == 1 && pf.CreateSnapshot().Points[0].Financial?.BoxSize == 4);
        Model.FinancialBoxSize = 1; FlushBindings();
        Check("model-box-change-refreshes-compiled-series", pf.BoxSize == 1 && pf.CreateSnapshot().Points.Count == 3);
        host.PriceSeries.BoxSize = 2; host.PriceSeries.ReversalBoxes = 2; host.ApplyTransform(ChartFinancialTransformKind.Renko); FlushBindings();
        Check("host-transform-hides-volume-to-avoid-double-counting", host.Samples.Count > 0 && host.Samples.All(sample => sample.Kind == ChartFinancialTransformKind.Renko) && !host.VolumeSeries.IsVisible);
        using (var drawing = new DrawingGroup().Open()) foreach (var chart in new[] { rawChart, heikinChart, pfChart, host.Chart }) chart.Render(drawing);
        Check("financial-series-record-through-public-rendering", new[] { rawChart, heikinChart, pfChart, host.Chart }.All(chart => chart.GetResourceCacheInfo().BudgetBytes >= 0));
        FinancialBindingDiagnostics.Add(new { originalClose = first.Financial!.Close, changedClose = candle.CreateSnapshot().Points[1].Financial!.Close,
            originalHeikinClose = originalHa.Points[1].Financial!.Close, changedHeikinClose = ha.CreateSnapshot().Points[1].Financial!.Close,
            sourceId = first.StableId, sourceIndex = first.SourceIndex, hostSourceCount = host.Data.Count,
            derivedGlyphCount = host.Samples.Count, hostVolumeVisible = host.VolumeSeries.IsVisible,
            physicalInputValidated = false, nativePixelsValidated = false, performanceMeasured = false });
        Model.FinancialUpdateCommand.Execute(null); Model.FinancialBoxSize = 2;
        host.ApplyTransform(ChartFinancialTransformKind.Raw); ConfigureFinancialBindings(); rawChart.ClearSelection();
        expander.IsExpanded = false; FlushBindings();
    }
}
