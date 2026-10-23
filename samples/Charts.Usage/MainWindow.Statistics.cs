using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class MainWindow
{
    internal CartesianChart StatisticsBoxChart => this.FindControl<CartesianChart>("UsageStatisticsBox")!;
    internal CartesianChart StatisticsErrorChart => this.FindControl<CartesianChart>("UsageStatisticsError")!;
    internal CartesianChart StatisticsDistributionChart => this.FindControl<CartesianChart>("UsageStatisticsDistribution")!;
    internal List<object> StatisticsBindingDiagnostics { get; } = [];

    internal void VerifyStatisticsBindings(Dictionary<string, bool> checks)
    {
        var expander = this.FindControl<Expander>("StatisticsExamples")!;
        expander.IsExpanded = true; FlushBindings();
        var boxChart = StatisticsBoxChart; var errorChart = StatisticsErrorChart; var distribution = StatisticsDistributionChart;
        var box = (BoxPlotSeries)boxChart.Series[0]; var errors = (ErrorBarSeries)errorChart.Series[0];
        var histogram = (HistogramSeries)distribution.Series[0]; var density = (DensitySeries)distribution.Series[1];
        void Check(string name, bool value) => checks["statistics-compiled-" + name] = value;
        static bool Near(double left, double right) => Math.Abs(left - right) <= 1e-10;
        Check("four-series-have-public-function-assembly-types", new[] { typeof(BoxPlotSeries), typeof(ErrorBarSeries), typeof(HistogramSeries), typeof(DensitySeries), typeof(ChartStatistics), typeof(ChartBoxPlotSummary) }.All(type => type.Assembly == typeof(CartesianChart).Assembly));
        Check("all-three-charts-have-layout", new[] { boxChart, errorChart, distribution }.All(chart => chart.PlotArea.Width > 100 && chart.PlotArea.Height > 100));
        Check("compiled-sources-and-business-paths", ReferenceEquals(box.ItemsSource, Model.StatisticsGroups) && box.SamplesPath == "Samples" && box.CategoryPath == "Name" && box.IdPath == "Id" && ReferenceEquals(errors.ItemsSource, Model.StatisticsErrors) && errors.ValuePath == "Center" && errors.LowerErrorPath == "Lower" && errors.UpperErrorPath == "Upper");
        Check("distribution-uses-the-public-immutable-results", ReferenceEquals(histogram.ItemsSource, Model.StatisticsBins) && ReferenceEquals(density.ItemsSource, Model.StatisticsDensity));
        var original = box.CreateSnapshot(); var first = original.Points[0];
        Check("quartile-outlier-identity-and-constant-group", first.BoxPlot is { Median: 12.5, LowerQuartile: 11.25, UpperQuartile: 13.75 } summary && summary.Outliers.Count == 1 && summary.Outliers[0] == new ChartStatisticalObservation(5, 95) && ReferenceEquals(first.Item, Model.StatisticsGroups[0]) && box.CreateSnapshot().Points[2].BoxPlot is { LowerQuartile: 45, UpperQuartile: 45 });
        var range = boxChart.VisibleRange!.Value; var plot = boxChart.PlotArea;
        var position = new Point(plot.Left + (first.Point.X - range.XMinimum) / (range.XMaximum - range.XMinimum) * plot.Width, plot.Bottom - (first.Point.Y - range.YMinimum) / (range.YMaximum - range.YMinimum) * plot.Height);
        var hit = boxChart.HitTestPoint(position, 1);
        Check("hit-retains-the-original-group", hit is { Index: 0 } && ReferenceEquals(hit.Item, Model.StatisticsGroups[0]) && Equals(hit.StableId, "stats-group-a"));
        if (hit is not null) boxChart.SetCurrentValue(CartesianChart.SelectedPointProperty, hit);
        FlushBindings();
        Check("selected-point-is-two-way-business-identity", Model.StatisticsSelectedPoint is { Index: 0 } selected && ReferenceEquals(selected.Item, Model.StatisticsGroups[0]) && Model.StatisticsSelectionText.Contains("stats-group-a", StringComparison.Ordinal));
        var initialError = errors.CreateSnapshot().Points[0];
        Check("asymmetric-error-paths-map-data-endpoints", initialError.Point.Y == 20 && initialError.Low == 18 && initialError.High == 24);
        var binPoint = histogram.CreateSnapshot().Points[2]; var densityPoint = density.CreateSnapshot().Points[20];
        Check("error-bin-and-density-preserve-their-actual-source-items", ReferenceEquals(initialError.Item, Model.StatisticsErrors[0]) && Equals(initialError.StableId, "stats-error-a") && binPoint.SourceIndex == 2 && binPoint.Item is ChartHistogramBin sourceBin && sourceBin == Model.StatisticsBins[2] && densityPoint.SourceIndex == 20 && densityPoint.Item is ChartPoint sourceDensity && sourceDensity == Model.StatisticsDensity[20]);
        Model.StatisticsShowOutliers = false; Model.StatisticsErrorMode = ChartErrorMode.Plus; FlushBindings();
        Check("model-options-update-series", !box.ShowOutliers && errors.ErrorMode == ChartErrorMode.Plus && errors.CreateSnapshot().Points[0].Low == 20);
        this.FindControl<CheckBox>("UsageStatisticsOutliers")!.IsChecked = true;
        this.FindControl<ComboBox>("UsageStatisticsErrorMode")!.SelectedItem = ChartErrorMode.Minus;
        this.FindControl<ComboBox>("UsageStatisticsBandwidth")!.SelectedItem = 1.5;
        FlushBindings();
        Check("controls-have-two-way-bindings", Model.StatisticsShowOutliers && Model.StatisticsErrorMode == ChartErrorMode.Minus && Model.StatisticsBandwidth == 1.5 && box.ShowOutliers && errors.CreateSnapshot().Points[0].High == 20);
        Check("density-normalized-histogram-area-is-one", Near(Model.StatisticsBins.Sum(bin => bin.Value * (bin.Upper - bin.Lower)), 1));
        var oldDensity = Model.StatisticsDensity;
        Model.StatisticsNormalization = ChartHistogramNormalization.Probability; FlushBindings();
        Check("probability-normalization-is-a-bound-result", Near(Model.StatisticsBins.Sum(bin => bin.Value), 1) && ReferenceEquals(histogram.ItemsSource, Model.StatisticsBins));
        var version = box.SourceVersion; Model.StatisticsUpdateCommand.Execute(null); FlushBindings();
        var changed = box.CreateSnapshot().Points[0];
        Check("inpc-data-update-keeps-old-snapshot-and-current-identity", box.SourceVersion > version && first.BoxPlot!.Median == 12.5 && changed.BoxPlot?.Median == 22.5 && ReferenceEquals(changed.Item, first.Item) && Equals(changed.StableId, first.StableId));
        Check("selection-tracks-the-updated-original-group", Model.StatisticsSelectedPoint is { Point.Y: 22.5 } changedSelection && ReferenceEquals(changedSelection.Item, Model.StatisticsGroups[0]));
        Check("error-and-distribution-data-refresh", errors.CreateSnapshot().Points[0].Point.Y == 30 && !ReferenceEquals(oldDensity, Model.StatisticsDensity) && ReferenceEquals(density.ItemsSource, Model.StatisticsDensity) && histogram.CreateSnapshot().Points.Count == 9 && density.CreateSnapshot().Points.Count == 129);
        using (var drawing = new DrawingGroup().Open()) foreach (var chart in new[] { boxChart, errorChart, distribution }) chart.Render(drawing);
        Check("all-statistical-series-record-through-public-rendering", new[] { boxChart, errorChart, distribution }.All(chart => chart.GetResourceCacheInfo().BudgetBytes >= 0));
        StatisticsBindingDiagnostics.Add(new { originalMedian = first.BoxPlot!.Median, changedMedian = changed.BoxPlot!.Median, changed.SourceIndex, changed.StableId, originalOutliers = first.BoxPlot.Outliers, histogramArea = Model.StatisticsBins.Sum(bin => bin.Value * (bin.Upper - bin.Lower)), densitySampleCount = Model.StatisticsDensity.Count, nativePixelsValidated = false, physicalInputValidated = false, performanceMeasured = false });
        Model.StatisticsUpdateCommand.Execute(null); Model.StatisticsNormalization = ChartHistogramNormalization.Density; Model.StatisticsBandwidth = .75; Model.StatisticsErrorMode = ChartErrorMode.Both; Model.StatisticsShowOutliers = true;
        boxChart.ClearSelection(); expander.IsExpanded = false; FlushBindings();
    }
}
