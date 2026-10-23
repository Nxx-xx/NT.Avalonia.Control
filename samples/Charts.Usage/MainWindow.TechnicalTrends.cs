using Avalonia.Controls;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class MainWindow
{
    internal CartesianChart TechnicalAverageChart => this.FindControl<CartesianChart>("UsageTechnicalAverage")!;
    internal CartesianChart TechnicalIndicatorChart => this.FindControl<CartesianChart>("UsageTechnicalIndicators")!;
    internal List<object> TechnicalTrendsBindingDiagnostics { get; } = [];

    private void ConfigureTechnicalTrendsBindings()
    {
        var source = TechnicalAverageChart.Series[0];
        foreach (var analytical in TechnicalAverageChart.Series.OfType<ChartAnalyticalSeries>()) analytical.SourceSeries = source;
        foreach (var indicator in TechnicalIndicatorChart.TechnicalIndicators) indicator.SourceSeries = TechnicalIndicatorChart.Series[0];
    }

    internal void VerifyTechnicalTrendsBindings(Dictionary<string, bool> checks)
    {
        var expander = this.FindControl<Expander>("TechnicalTrendsExamples")!;
        expander.IsExpanded = true; FlushBindings();
        var chart = TechnicalAverageChart; var indicators = TechnicalIndicatorChart;
        var raw = chart.Series[0]; var average = (MovingAverageSeries)chart.Series[1]; var trend = (ChartTrendlineSeries)chart.Series[2];
        var sma = (SMAIndicator)indicators.TechnicalIndicators[0]; var ema = (EMAIndicator)indicators.TechnicalIndicators[1];
        var wma = (WMAIndicator)indicators.TechnicalIndicators[2]; var bands = (BollingerBandsIndicator)indicators.TechnicalIndicators[3];
        void Check(string key, bool value) => checks["technical-trends-compiled-" + key] = value;
        static bool Near(double expected, double actual) => Math.Abs(expected - actual) < 1e-9;
        Check("public-types-share-function-assembly", new[] { typeof(ChartAnalytics), typeof(ChartTrendlineResult), typeof(MovingAverageSeries),
            typeof(ChartTrendlineSeries), typeof(Trendline), typeof(SMAIndicator), typeof(EMAIndicator), typeof(WMAIndicator), typeof(BollingerBandsIndicator) }
            .All(type => type.Assembly == typeof(CartesianChart).Assembly));
        Check("both-charts-have-layout", chart.PlotArea.Width > 100 && chart.PlotArea.Height > 100 && indicators.PlotArea.Width > 100 && indicators.PlotArea.Height > 100);
        Check("business-source-and-paths-use-compiled-bindings", ReferenceEquals(raw.ItemsSource, Model.TechnicalRecords) && raw.CategoryPath == "X" && raw.ValuePath == "Y" && raw.IdPath == "Id" && ReferenceEquals(indicators.Series[0].ItemsSource, Model.TechnicalRecords));
        Check("linked-series-use-complete-mapped-source", ReferenceEquals(average.SourceSeries, raw) && ReferenceEquals(trend.SourceSeries, raw) && indicators.TechnicalIndicators.All(item => ReferenceEquals(item.SourceSeries, indicators.Series[0])));
        var original = average.CreateSnapshot(); var endpoint = original.Points[2];
        Check("average-warmup-gap-and-original-endpoint", original.Points.Count == 12 && original.Points[0].IsEmpty && original.Points[1].IsEmpty &&
            Near(6, endpoint.Point.Y) && endpoint.RawPoint?.Y == 6 && ReferenceEquals(endpoint.Item, Model.TechnicalRecords[2]) && Equals(endpoint.StableId, "technical-3") &&
            original.Points[6].IsEmpty && original.Points[7].IsEmpty && original.Points[8].IsEmpty && !original.Points[9].IsEmpty);
        Check("sma-ema-and-wma-use-distinct-public-conventions", Near(6, sma.CreateSnapshot().Points[2].Point.Y) &&
            Near(6, ema.CreateSnapshot().Points[2].Point.Y) && Near(9, ema.CreateSnapshot().Points[3].Point.Y) && Near(38d / 6, wma.CreateSnapshot().Points[2].Point.Y));
        var bandPoint = bands.CreateSnapshot().Points[2];
        Check("bollinger-middle-and-population-width", Near(6, bandPoint.Point.Y) &&
            Near(6 + 2 * Math.Sqrt(8d / 3), bands.UpperSeries.CreateSnapshot().Points[2].Point.Y) &&
            Near(6 - 2 * Math.Sqrt(8d / 3), bands.LowerSeries.CreateSnapshot().Points[2].Point.Y));
        var fitted = trend.CreateSnapshot();
        Check("fitted-curve-has-explicit-synthetic-identity", fitted.Points.Count == 64 && fitted.Points.All(point => point.Analysis is { IsSynthetic: true, SourceIndex: null } && point.Item is null) && trend.FitResult is { Count: 11, OmittedCount: 1 });
        this.FindControl<ComboBox>("UsageTechnicalPeriod")!.SelectedItem = 4; FlushBindings();
        Check("period-control-updates-model-and-all-calculations", Model.TechnicalPeriod == 4 && average.Period == 4 && indicators.TechnicalIndicators.All(item => item.Period == 4) && average.CreateSnapshot().Points[2].IsEmpty && Near(7.5, average.CreateSnapshot().Points[3].Point.Y));
        this.FindControl<ComboBox>("UsageTechnicalAverageType")!.SelectedItem = ChartMovingAverageType.Weighted; FlushBindings();
        Check("average-type-control-has-two-way-binding", Model.TechnicalAverageType == ChartMovingAverageType.Weighted && average.Type == ChartMovingAverageType.Weighted && Near(8.6, average.CreateSnapshot().Points[3].Point.Y));
        this.FindControl<ComboBox>("UsageTechnicalTrendType")!.SelectedItem = ChartTrendlineType.Polynomial; FlushBindings();
        var polynomial = trend.CreateSnapshot();
        Check("trend-control-refits-a-polynomial", Model.TechnicalTrendType == ChartTrendlineType.Polynomial && trend.Type == ChartTrendlineType.Polynomial &&
            !ReferenceEquals(polynomial, fitted) && polynomial.Points.Count == 64 && polynomial.Points.All(point => double.IsFinite(point.Point.X) && double.IsFinite(point.Point.Y)) &&
            trend.FitResult is { Type: ChartTrendlineType.Polynomial, Count: 11, OmittedCount: 1, RSquared: { } polynomialRSquared } && double.IsFinite(polynomialRSquared) && polynomialRSquared > .9);
        Model.TechnicalPeriod = 3; Model.TechnicalAverageType = ChartMovingAverageType.Simple; Model.TechnicalTrendType = ChartTrendlineType.Linear; FlushBindings();
        Check("model-options-flow-back-to-controls", Equals(this.FindControl<ComboBox>("UsageTechnicalPeriod")!.SelectedItem, 3) && Equals(this.FindControl<ComboBox>("UsageTechnicalAverageType")!.SelectedItem, ChartMovingAverageType.Simple) && average.Type == ChartMovingAverageType.Simple);
        var revision = average.SourceVersion; Model.TechnicalUpdateCommand.Execute(null); FlushBindings();
        Check("inpc-recomputes-derived-values-and-preserves-old-snapshot", average.SourceVersion > revision && Near(6, original.Points[2].Point.Y) && Near(7, average.CreateSnapshot().Points[2].Point.Y) && ReferenceEquals(average.CreateSnapshot().Points[2].Item, endpoint.Item));
        Model.TechnicalAppendCommand.Execute(null); FlushBindings();
        Check("collection-append-adds-an-aligned-endpoint", average.CreateSnapshot().Points.Count == 13 && Equals(average.CreateSnapshot().Points[^1].StableId, "technical-13") && ReferenceEquals(average.CreateSnapshot().Points[^1].Item, Model.TechnicalRecords[^1]) && sma.CreateSnapshot().Points.Count == 13);
        var fullHistory = average.CreateSnapshot(); var computations = average.ComputationCount;
        chart.Viewport = new ChartViewport(8, 12, 0, 40); FlushBindings();
        Check("zoom-reuses-the-full-history-calculation", ReferenceEquals(fullHistory, average.CreateSnapshot()) && average.ComputationCount == computations && average.CreateSnapshot().Points[2].Point.X == 3 && Near(7, average.CreateSnapshot().Points[2].Point.Y));
        chart.Series.Remove(raw); FlushBindings(); var dependencyVersion = average.SourceVersion;
        Model.TechnicalRecords[1].Y += 3; FlushBindings();
        Check("removed-source-remains-an-observed-analysis-dependency", ReferenceEquals(average.SourceSeries, raw) && average.SourceVersion > dependencyVersion && Near(8, average.CreateSnapshot().Points[2].Point.Y));
        chart.Series.Insert(0, raw); chart.Viewport = null; FlushBindings();
        var oldRecords = Model.TechnicalRecords; Model.TechnicalReplaceCommand.Execute(null); FlushBindings();
        Check("source-replacement-releases-old-business-records", oldRecords.All(item => item.SubscriberCount == 0) && Model.TechnicalRecords.All(item => item.SubscriberCount > 0) && ReferenceEquals(raw.ItemsSource, Model.TechnicalRecords) && Near(11, average.CreateSnapshot().Points[2].Point.Y));
        var content = Content; Content = null; FlushBindings();
        Check("unload-releases-analysis-source-and-item-observers", Model.TechnicalRecords.All(item => item.SubscriberCount == 0));
        Content = content; FlushBindings();
        Check("reattach-restores-current-history-observers", Model.TechnicalRecords.All(item => item.SubscriberCount > 0) && Near(11, average.CreateSnapshot().Points[2].Point.Y));
        using (var drawing = new DrawingGroup().Open()) { chart.Render(drawing); indicators.Render(drawing); }
        Check("averages-trend-and-indicator-parts-record-through-rendering", chart.GetResourceCacheInfo().BudgetBytes >= 0 && indicators.GetResourceCacheInfo().BudgetBytes >= 0 && bands.BandSeries.CreateSnapshot().Points.Count == 12);
        TechnicalTrendsBindingDiagnostics.Add(new { originalMean = endpoint.Point.Y, replacedMean = average.CreateSnapshot().Points[2].Point.Y,
            sourceIndex = endpoint.SourceIndex, sourceId = endpoint.StableId, sourceCount = Model.TechnicalRecords.Count,
            fitCount = trend.FitResult?.Count, fitRSquared = trend.FitResult?.RSquared, computations,
            physicalInputValidated = false, nativePixelsValidated = false, performanceMeasured = false });
        Model.TechnicalRecords = ChartUsageViewModel.CreateTechnicalRecords();
        expander.IsExpanded = false; FlushBindings();
    }
}
