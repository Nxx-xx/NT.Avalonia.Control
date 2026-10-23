using System.Reflection;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class ParetoVisibilityCacheTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cumulative_source_and_category_order_survive_view_style_and_secondary_axis_changes(bool horizontal)
    {
        var rows = Enumerable.Range(0, 20).Select(i => new Row($"id-{i}", $"Category {i}", 100 - i)).ToArray();
        var series = new ParetoSeries { Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical,
            ItemsSource = rows, CategoryPath = "Category", ValuePath = "Value", IdPath = "Id", CumulativeAxisKey = "percent" };
        var chart = new CartesianChart { Width = 700, Height = 400, ShowLegend = false,
            XAxis = horizontal ? new NumericAxis { Minimum = 0, Maximum = 120 } : new CategoryAxis(),
            YAxis = horizontal ? new CategoryAxis() : new NumericAxis { Minimum = 0, Maximum = 120 } };
        var percent = new NumericAxis { Key = "percent", Minimum = 0, Maximum = 100 };
        chart.Axes.Add(percent); chart.Series.Add(series);
        using var host = new TemplateFeaturesHost(chart);
        var first = ReadContext();
        Assert.NotNull(first.SecondaryData);
        chart.Viewport = horizontal ? new(0, 120, 5, 10) : new(5, 10, 0, 120);
        var zoomed = ReadContext();
        Assert.Same(first.Data, zoomed.Data);
        Assert.Same(first.SecondaryData, zoomed.SecondaryData);
        series.Fill = Brushes.OrangeRed; percent.Maximum = 200;
        var restyled = ReadContext();
        Assert.Same(first.SecondaryData, restyled.SecondaryData);
        var item = restyled.SecondaryData!.Points[0];
        Assert.Same(rows[item.SourceIndex], item.Item);
        Assert.Equal(rows[item.SourceIndex].Id, item.StableId);
        series.Refresh();
        Assert.NotSame(first.SecondaryData, ReadContext().SecondaryData);

        ChartSeriesRenderContext ReadContext()
        {
            host.Drain(); TemplateFeaturesHost.Record(chart);
            var scene = typeof(CartesianChart).GetField("_scene", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(chart)!;
            var snapshot = ((Array)scene.GetType().GetProperty("Snapshots")!.GetValue(scene)!).GetValue(0)!;
            return (ChartSeriesRenderContext)snapshot.GetType().GetProperty("Context")!.GetValue(snapshot)!;
        }
    }
    private sealed record Row(string Id, string Category, double Value);
}
