using System.Collections.ObjectModel;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class RenderPresentationSafetyTests
{
    [AvaloniaFact]
    public void A_dirty_managed_legend_defers_build_and_child_changes_until_layout_instead_of_parent_render()
    {
        var chart = new CartesianChart { IsLegendScrollable = true, LegendMaxHeight = 100 };
        var first = new LineSeries { Title = "original", ItemsSource = new ChartPoint[] { new(1, 10), new(2, 20) } };
        var second = new LineSeries { Title = "new series", ItemsSource = new ChartPoint[] { new(1, 30), new(2, 40) } };
        var source = new ObservableCollection<ChartSeries> { first };
        chart.SeriesSource = source;
        var rendering = typeof(CartesianChart).GetField("_rendering", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var dirty = typeof(CartesianChart).GetField("_legendPresentationDirty", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var builds = 0;
        chart.LegendItemTemplate = new FuncDataTemplate<ChartLegendItem>((_, _) =>
        {
            Assert.False((bool)rendering.GetValue(chart)!);
            builds++;
            return new Border { Name = "RenderSafeLegendCard", Width = 100, Height = 24 };
        });
        using var host = new TemplateFeaturesHost(chart);
        Assert.Contains(chart.LegendItems, item => ReferenceEquals(item.Series, first));
        var before = builds;
        source.Add(second);
        Assert.True((bool)dirty.GetValue(chart)!);
        var childrenBeforeRender = chart.GetVisualDescendants().ToArray();
        _ = TemplateFeaturesHost.Record(chart);
        Assert.Equal(before, builds);
        Assert.Equal(childrenBeforeRender, chart.GetVisualDescendants().ToArray());
        host.Drain();
        Assert.True(builds > before);
        Assert.Equal(2, chart.LegendItems.Count);
        Assert.Contains(chart.LegendItems, item => ReferenceEquals(item.Series, second));
        Assert.Equal(2, chart.GetVisualDescendants().OfType<Border>().Count(control => control.Name == "RenderSafeLegendCard"));
    }
}
