using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Layout;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class MainWindow
{
    internal CartesianChart BarComparisonChart => this.FindControl<CartesianChart>("UsageBarComparisonChart")!;
    internal CartesianChart BarRangeChart => this.FindControl<CartesianChart>("UsageBarRangeChart")!;
    internal CartesianChart WaterfallChart => this.FindControl<CartesianChart>("UsageWaterfallChart")!;
    internal CartesianChart ParetoChart => this.FindControl<CartesianChart>("UsageParetoChart")!;
    internal List<object> BarComparisonBindingDiagnostics { get; } = [];

    private void ConfigureBarComparisonLabels()
    {
        foreach (var chart in new[] { BarComparisonChart, BarRangeChart, WaterfallChart, ParetoChart })
            foreach (var series in chart.Series)
                series.DataLabelFormatter = context => (context.Data.RawPoint ?? context.Data.Point).Y.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture);
    }

    internal void VerifyBarComparisonBindings(Dictionary<string, bool> checks)
    {
        var expander = this.FindControl<Expander>("BarComparisonExamples")!; expander.IsExpanded = true; FlushBindings();
        var chart = BarComparisonChart; var rangeChart = BarRangeChart; var waterfallChart = WaterfallChart; var paretoChart = ParetoChart;
        foreach (var item in new[] { chart, rangeChart, waterfallChart, paretoChart }) _ = item.PlotArea;
        FlushBindings();
        var first = (BarSeries)chart.Series[0]; var second = (BarSeries)chart.Series[1]; var range = (RangeBarSeries)rangeChart.Series[0]; var waterfall = (WaterfallSeries)waterfallChart.Series[0]; var pareto = (ParetoSeries)paretoChart.Series[0];
        var root = (ChartAutomationPeer)ControlAutomationPeer.CreatePeerForElement(chart)!;
        ChartPointHit? Read(ChartSeries series, int index) { _ = chart.PlotArea; FlushBindings(); return root.GetPointPeer(series, index)?.CurrentPoint; }
        static bool Near(double? value, double expected) => value is { } number && double.IsFinite(number) && Math.Abs(number - expected) <= 1e-9;
        checks["bars-compiled-business-sources-and-new-types"] = ReferenceEquals(first.ItemsSource, Model.BarRecords) && rangeChart.Series[1] is DotSeries && rangeChart.Series[2] is LollipopSeries && rangeChart.Series[3] is VarianceSeries && pareto is ParetoSeries;
        checks["bars-compiled-initial-group-and-category-axis"] = chart.XAxis is CategoryAxis && first.Orientation == Orientation.Vertical && first.StackMode == ChartStackMode.Stacked && first.StackGroup == second.StackGroup && first.SortMode == ChartSeriesSortMode.Source;
        var mapped = first.CreateSnapshot().Points[0];
        checks["bars-compiled-canonical-source-keeps-original-index-and-identity"] = mapped.Point == new ChartPoint(1, 20) && mapped.SourceIndex == 0 && ReferenceEquals(mapped.Item, Model.BarRecords[0]) && Equals(mapped.StableId, "usage-bar-a");
        Model.BarOrientation = Orientation.Horizontal; FlushBindings();
        checks["bars-compiled-model-updates-orientation-and-axis-types"] = first.Orientation == Orientation.Horizontal && second.Orientation == Orientation.Horizontal && chart.XAxis is NumericAxis && chart.YAxis is CategoryAxis;
        this.FindControl<ComboBox>("UsageBarOrientation")!.SelectedItem = Orientation.Vertical; FlushBindings();
        checks["bars-compiled-orientation-selector-is-two-way"] = Model.BarOrientation == Orientation.Vertical && chart.XAxis is CategoryAxis && first.Orientation == Orientation.Vertical;
        Model.BarWidth = 24; FlushBindings();
        checks["bars-compiled-model-updates-fixed-width"] = first.BarWidth == 24 && second.BarWidth == 24;
        this.FindControl<ComboBox>("UsageBarWidth")!.SelectedItem = 12d; FlushBindings();
        checks["bars-compiled-width-selector-is-two-way"] = Model.BarWidth == 12 && first.BarWidth == 12;
        Model.BarSortMode = ChartSeriesSortMode.ValueDescending; FlushBindings();
        checks["bars-compiled-model-updates-value-sort"] = first.SortMode == ChartSeriesSortMode.ValueDescending && second.SortMode == ChartSeriesSortMode.ValueDescending;
        var sorted = Read(first, 0);
        checks["bars-compiled-sorting-moves-slot-without-rewriting-business-row"] = sorted is { Index: 0, Point.X: 1 } && sorted.RawPoint == new ChartPoint(1, 20) && ReferenceEquals(sorted.Item, Model.BarRecords[0]);
        this.FindControl<ComboBox>("UsageBarSortMode")!.SelectedItem = ChartSeriesSortMode.Source; FlushBindings();
        checks["bars-compiled-sort-selector-is-two-way"] = Model.BarSortMode == ChartSeriesSortMode.Source && first.SortMode == ChartSeriesSortMode.Source;
        Model.BarStackMode = ChartStackMode.Percent; FlushBindings();
        var positive = Read(second, 0); var negative = Read(second, 1);
        checks["bars-compiled-percent-positive-keeps-contribution"] = positive is { Index: 0, Point.Y: 100 } && positive.RawPoint == new ChartPoint(1, 30) && Near(positive.BaseValue, 40) && Near(positive.StackValue, 60);
        checks["bars-compiled-percent-negative-keeps-contribution"] = negative is { Index: 1, Point.Y: -100 } && negative.RawPoint == new ChartPoint(3, -30) && Near(negative.BaseValue, -40) && Near(negative.StackValue, -60);
        Model.BarOrientation = Orientation.Horizontal; FlushBindings();
        var horizontal = Read(second, 0);
        checks["bars-compiled-horizontal-display-and-source-coordinates-distinct"] = horizontal is { Index: 0, Point.X: 100, Point.Y: 0 } && horizontal.RawPoint == new ChartPoint(1, 30) && Near(horizontal.BaseValue, 40);
        if (horizontal is not null) chart.SelectedPoints = new[] { horizontal }; FlushBindings();
        checks["bars-compiled-selection-retains-source-and-display-metadata"] = chart.SelectedPoint is { Index: 0, Point.X: 100 } selected && selected.RawPoint == new ChartPoint(1, 30) && Near(selected.StackValue, 60);
        this.FindControl<ComboBox>("UsageBarStackMode")!.SelectedItem = ChartStackMode.None; FlushBindings();
        checks["bars-compiled-stack-selector-is-two-way"] = Model.BarStackMode == ChartStackMode.None && second.StackMode == ChartStackMode.None;
        Model.BarLabels = true; FlushBindings();
        checks["bars-compiled-model-updates-label-toggle"] = first.ShowDataLabels && second.ShowDataLabels && range.ShowDataLabels && waterfall.ShowDataLabels && pareto.ShowDataLabels && first.DataLabelFormatter is not null;
        this.FindControl<CheckBox>("UsageBarLabels")!.IsChecked = false; FlushBindings();
        checks["bars-compiled-label-checkbox-is-two-way"] = !Model.BarLabels && !first.ShowDataLabels && !waterfall.ShowDataLabels;
        var bounds = range.CreateSnapshot().Points[0];
        checks["bars-compiled-range-without-valuepath-has-real-high-low"] = range.ValuePath is null && bounds.High == 80 && bounds.Low == 20 && ReferenceEquals(bounds.Item, Model.BarRangeRecords[0]) && Equals(bounds.StableId, "usage-bar-range");
        Model.BarRangeRecords[0].High = 85; Model.BarRangeRecords[0].Low = 15; _ = rangeChart.PlotArea; FlushBindings();
        checks["bars-compiled-range-inpc-updates-both-bounds"] = range.CreateSnapshot().Points[0] is { High: 85, Low: 15 };
        var waterfallRoot = (ChartAutomationPeer)ControlAutomationPeer.CreatePeerForElement(waterfallChart)!;
        var contribution = waterfallRoot.GetPointPeer(waterfall, 2)?.CurrentPoint;
        checks["bars-compiled-waterfall-cumulative-and-raw-value"] = contribution is { Index: 2, Point.Y: 45 } && contribution.RawPoint == new ChartPoint(3, 20) && Near(contribution.BaseValue, 25);
        Model.WaterfallRecords[2].IsTotal = true; _ = waterfallChart.PlotArea; FlushBindings();
        var changedTotal = waterfallRoot.GetPointPeer(waterfall, 2)?.CurrentPoint;
        checks["bars-compiled-waterfall-totalpath-inpc-resets-baseline"] = changedTotal is { Index: 2, Point.Y: 20 } && Near(changedTotal.BaseValue, 0) && ReferenceEquals(changedTotal.Item, Model.WaterfallRecords[2]);
        var paretoRoot = (ChartAutomationPeer)ControlAutomationPeer.CreatePeerForElement(paretoChart)!; var ranked = paretoRoot.GetPointPeer(pareto, 1)?.CurrentPoint;
        checks["bars-compiled-pareto-ranked-slot-retains-source-index"] = pareto.SortMode == ChartSeriesSortMode.ValueDescending && paretoChart.XAxis is CategoryAxis && ranked is { Index: 1, Point.X: 0, Point.Y: 50 } && ranked.RawPoint == new ChartPoint(2, 50);
        checks["bars-compiled-tooltip-explicitly-provides-raw-coordinate-template"] = chart.TooltipContentTemplate is not null && rangeChart.TooltipContentTemplate is not null && waterfallChart.TooltipContentTemplate is not null;
        var content = Content; Content = null; FlushBindings();
        checks["bars-compiled-detach-releases-business-subscriptions"] = Model.BarRecords.All(item => item.SubscriberCount == 0) && Model.WaterfallRecords.All(item => item.SubscriberCount == 0) && Model.BarRangeRecords.All(item => item.SubscriberCount == 0);
        Content = content; FlushBindings(); foreach (var item in new[] { chart, rangeChart, waterfallChart, paretoChart }) _ = item.PlotArea;
        checks["bars-compiled-reattach-restores-business-subscriptions"] = Model.BarRecords.All(item => item.SubscriberCount > 0) && Model.WaterfallRecords.All(item => item.SubscriberCount > 0) && Model.BarRangeRecords.All(item => item.SubscriberCount > 0);
        BarComparisonBindingDiagnostics.Add(new { canonical = new { mapped.SourceIndex, mapped.StableId, mapped.Point },
            positive = positive is null ? null : new { positive.Point, positive.RawPoint, positive.BaseValue, positive.StackValue }, horizontal = horizontal is null ? null : new { horizontal.Point, horizontal.RawPoint, horizontal.BaseValue, horizontal.StackValue },
            sorted = sorted is null ? null : new { sorted.Point, sorted.RawPoint, sorted.Index }, waterfall = contribution is null ? null : new { contribution.Point, contribution.RawPoint, contribution.BaseValue }, pareto = ranked is null ? null : new { ranked.Point, ranked.RawPoint, ranked.Index },
            derivedMetadataAbsoluteTolerance = 1e-9, rawPointAndIdentityExact = true, physicalInputValidated = false, performanceMeasured = false });
        chart.ClearSelection(); Model.BarOrientation = Orientation.Vertical; Model.BarStackMode = ChartStackMode.Stacked; Model.BarSortMode = ChartSeriesSortMode.Source; Model.BarWidth = null; Model.BarLabels = false;
        Model.BarRangeRecords[0].High = 80; Model.BarRangeRecords[0].Low = 20; Model.WaterfallRecords[2].IsTotal = false; FlushBindings();
        expander.IsExpanded = false; FlushBindings();
    }
}
