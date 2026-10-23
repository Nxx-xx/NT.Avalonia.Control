using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class ComparisonReadoutTests
{
    private sealed record Row(string Name, double Value, double High, double Low);

    [AvaloniaTheory]
    [InlineData("range", false)]
    [InlineData("variance", true)]
    [InlineData("waterfall", false)]
    [InlineData("percent", true)]
    [InlineData("pareto", false)]
    public void Trackball_template_receives_default_comparison_readout_including_lower_or_cumulative_semantics(string kind, bool horizontal)
    {
        ChartSeries series = kind switch
        {
            "range" => new RangeBarSeries(), "variance" => new VarianceSeries(),
            "waterfall" => new WaterfallSeries(), "percent" => new BarSeries { StackMode = ChartStackMode.Percent },
            _ => new ParetoSeries(),
        };
        if (series is ComparisonSeries comparison) comparison.Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical;
        else ((BarSeries)series).Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical;
        series.ItemsSource = new[] { new Row("A", 60, 70, 20), new Row("B", 40, 80, 30) };
        series.CategoryPath = "Name"; series.ValuePath = "Value"; series.HighPath = "High"; series.LowPath = "Low";
        var chart = new CartesianChart { ShowLegend = false, TrackballMode = horizontal ? ChartTrackballMode.Horizontal : ChartTrackballMode.Vertical,
            TrackballContentTemplate = new FuncDataTemplate<ChartTooltipContext>((_, _) => new Border { Name = "ComparisonReadout" }) };
        if (horizontal) chart.YAxis = new CategoryAxis(); else chart.XAxis = new CategoryAxis();
        chart.Series.Add(series); using var host = new TemplateFeaturesHost(chart);
        Assert.True(chart.NavigateToPoint(series, 1));
        SegmentTestHost.RaiseExactMove(chart, chart.CurrentPoint!.Position); host.Drain();
        var card = Assert.Single(chart.GetVisualDescendants().OfType<Border>(), item => item.Name == "ComparisonReadout");
        var context = Assert.IsType<ChartTooltipContext>(card.DataContext);
        Assert.Contains("下界", context.Text);
        if (kind == "percent") Assert.Contains("100%", context.Text);
        if (kind == "pareto") Assert.Contains("累计: 100%", context.Text);
        if (kind == "waterfall") Assert.Contains("原值: 40", context.Text);
        Assert.Same(series, Assert.Single(context.Readings).Series);
    }
}
