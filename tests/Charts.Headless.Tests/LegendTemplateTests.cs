using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class LegendTemplateTests
{
    [AvaloniaFact]
    public void Defaults_keep_the_drawn_legend_and_create_no_template_or_scroll_controls()
    {
        var chart = CreateChart(2);
        var draws = 0;
        chart.LegendRenderer = (drawing, item) => { draws++; drawing.DrawRectangle(Brushes.Magenta, null, item.Bounds); };
        using var host = new TemplateFeaturesHost(chart);
        var before = draws;
        _ = TemplateFeaturesHost.Record(chart);
        Assert.Equal(before + 2, draws);
        Assert.DoesNotContain(chart.GetVisualDescendants().OfType<Control>(), control => control.DataContext is ChartLegendItem);
        Assert.Empty(chart.GetVisualDescendants().OfType<ScrollViewer>());
    }

    [AvaloniaFact]
    public void A_real_template_takes_priority_and_reuses_controls_when_text_and_visibility_change()
    {
        var chart = CreateChart(2);
        var builds = 0;
        var customDraws = 0;
        chart.LegendRenderer = (_, _) => customDraws++;
        chart.LegendItemTemplate = new FuncDataTemplate<ChartLegendItem>((_, _) => { builds++; return Card(); });
        using var host = new TemplateFeaturesHost(chart);
        var cards = Cards(chart);
        Assert.Equal(2, cards.Length);
        Assert.All(cards, card => Assert.True(card.Bounds.Width > 0 && card.Bounds.Height > 0));
        foreach (var series in chart.Series)
        {
            var context = Assert.IsType<ChartLegendItem>(Assert.Single(cards, card => ((ChartLegendItem)card.DataContext!).Series == series).DataContext);
            Assert.Equal(series.Title, context.Text);
            Assert.Same(chart.GetSeriesBrush(series), context.Brush);
        }
        var before = builds;
        chart.Series[0].Title = "changed business title";
        chart.Series[0].IsVisible = false;
        host.Drain();
        _ = TemplateFeaturesHost.Record(chart);
        _ = TemplateFeaturesHost.Record(chart);
        Assert.Equal(before, builds);
        Assert.Equal(0, customDraws);
        Assert.All(cards, card => Assert.Contains(Cards(chart), current => ReferenceEquals(current, card)));
        var updated = Assert.IsType<ChartLegendItem>(Assert.Single(Cards(chart), card => ((ChartLegendItem)card.DataContext!).Series == chart.Series[0]).DataContext);
        Assert.Equal("changed business title", updated.Text);
        Assert.False(updated.IsVisible);
    }

    [AvaloniaFact]
    public void Null_template_results_hide_legend_items_without_falling_back_to_a_renderer()
    {
        var chart = CreateChart(2);
        var builds = 0;
        var draws = 0;
        chart.LegendRenderer = (_, _) => draws++;
        chart.LegendItemTemplate = new FuncDataTemplate<ChartLegendItem>((_, _) => { builds++; return null; });
        using var host = new TemplateFeaturesHost(chart);
        Assert.Empty(chart.LegendItems);
        Assert.Empty(Cards(chart));
        var before = builds;
        _ = TemplateFeaturesHost.Record(chart);
        _ = TemplateFeaturesHost.Record(chart);
        Assert.Equal(before, builds);
        Assert.Equal(0, draws);
        Assert.True(chart.PlotArea.Width > 100 && chart.PlotArea.Height > 100);
    }

    [AvaloniaFact]
    public void A_height_limit_alone_applies_to_a_real_default_legend_without_enabling_scroll_or_templates()
    {
        var chart = CreateChart(20);
        chart.LegendPosition = ChartLegendPosition.Top;
        chart.LegendMaxHeight = 40;
        using var host = new TemplateFeaturesHost(chart);
        Assert.Null(chart.LegendItemTemplate);
        Assert.False(chart.IsLegendScrollable);
        Assert.NotEmpty(chart.LegendItems);
        var bounds = chart.LegendItems.Select(item => item.Bounds).Aggregate((first, next) => first.Union(next));
        Assert.InRange(bounds.Height, double.Epsilon, 40);
        var viewer = Viewer(chart);
        Assert.InRange(viewer.Bounds.Height, double.Epsilon, 40);
        Assert.Equal(0, viewer.Offset.Y);
        Assert.True(chart.PlotArea.Height > 100);
    }

    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Wheel_reaches_the_last_series_and_visible_clipped_items_keep_click_and_cancel_identity(bool floating, bool templated)
    {
        var chart = CreateChart(20);
        chart.IsLegendFloating = floating;
        chart.IsLegendScrollable = true;
        chart.LegendMaxWidth = 156;
        chart.LegendMaxHeight = 100;
        chart.LegendPosition = ChartLegendPosition.Right;
        chart.IsLegendInteractive = true;
        chart.IsZoomEnabled = true;
        chart.IsPanEnabled = true;
        chart.SelectionMode = ChartSelectionMode.Multiple;
        if (templated) chart.LegendItemTemplate = new FuncDataTemplate<ChartLegendItem>((_, _) => Card());
        using var host = new TemplateFeaturesHost(chart);
        var scroll = Viewer(chart);
        Assert.True(scroll.Extent.Height > scroll.Viewport.Height);
        var viewport = TemplateFeaturesHost.BoundsInChart(scroll, chart);
        Assert.All(chart.LegendItems, item => Assert.True(viewport.Contains(item.Bounds)));
        Assert.Contains(chart.LegendItems, item => ReferenceEquals(item.Series, chart.Series[0]));
        var handled = new List<bool>();
        host.Window.AddHandler(InputElement.PointerWheelChangedEvent, (_, args) => handled.Add(args.Handled), RoutingStrategies.Bubble, true);
        host.Wheel(viewport.Center, -100);
        Assert.True(scroll.Offset.Y > 0);
        Assert.All(chart.LegendItems, item => Assert.True(viewport.Contains(item.Bounds)));
        var finalSeries = chart.Series[^1];
        var finalItem = Assert.Single(chart.LegendItems, item => ReferenceEquals(item.Series, finalSeries));
        var cancel = true;
        ChartSeries? invoked = null;
        chart.LegendItemInvoked += (_, args) => { invoked = args.Series; args.Cancel = cancel; };
        host.Click(finalItem.Bounds.Center);
        Assert.Same(finalSeries, invoked);
        Assert.True(finalSeries.IsVisible);
        Assert.Empty(chart.SelectedPoints!);
        cancel = false;
        host.Click(Assert.Single(chart.LegendItems, item => ReferenceEquals(item.Series, finalSeries)).Bounds.Center);
        Assert.False(finalSeries.IsVisible);
        Assert.Null(chart.Viewport);
        Assert.Null(chart.HoveredPoint);
        // Wheel at both extremes is still consumed by the legend; it must not zoom
        // the plot or leak to an enclosing scroll surface.
        host.Wheel(viewport.Center, -100);
        host.Wheel(viewport.Center, 100);
        host.Wheel(viewport.Center, 100);
        Assert.Equal(0, scroll.Offset.Y, 6);
        Assert.NotEmpty(handled);
        Assert.All(handled, value => Assert.True(value));
        Assert.Null(chart.Viewport);
        Assert.Contains(chart.LegendItems, item => ReferenceEquals(item.Series, chart.Series[0]));
    }

    [AvaloniaFact]
    public void Removing_scrolled_items_clamps_the_offset_and_releases_their_controls()
    {
        var chart = CreateChart(20);
        chart.IsLegendScrollable = true;
        chart.LegendPosition = ChartLegendPosition.Right;
        chart.LegendMaxHeight = 90;
        chart.LegendItemTemplate = new FuncDataTemplate<ChartLegendItem>((_, _) => Card());
        using var host = new TemplateFeaturesHost(chart);
        var viewer = Viewer(chart);
        host.Wheel(TemplateFeaturesHost.BoundsInChart(viewer, chart).Center, -100);
        var removedSeries = chart.Series[^1];
        var removedCard = Assert.Single(Cards(chart), card => ((ChartLegendItem)card.DataContext!).Series == removedSeries);
        while (chart.Series.Count > 2) chart.Series.RemoveAt(chart.Series.Count - 1);
        host.Drain();
        Assert.True(!chart.GetVisualDescendants().Contains(viewer) || viewer.Offset.Y == 0);
        Assert.DoesNotContain(chart.LegendItems, item => ReferenceEquals(item.Series, removedSeries));
        TemplateFeaturesHost.AssertReleased(removedCard);
        Assert.All(chart.LegendItems, item => Assert.Contains(chart.Series, series => ReferenceEquals(series, item.Series)));
    }

    [AvaloniaTheory]
    [InlineData("template")]
    [InlineData("detach")]
    public void Template_replacement_and_detach_release_the_old_legend_subtree(string action)
    {
        var chart = CreateChart(1);
        var original = Card();
        chart.LegendItemTemplate = new FuncDataTemplate<ChartLegendItem>((_, _) => original);
        using var host = new TemplateFeaturesHost(chart);
        Assert.Same(original, Assert.Single(Cards(chart)));
        if (action == "template") chart.LegendItemTemplate = null;
        else host.Window.Content = null;
        host.Drain();
        TemplateFeaturesHost.AssertReleased(original);
    }

    [AvaloniaFact]
    public void A_template_that_removes_its_series_cannot_commit_an_obsolete_legend_child()
    {
        var chart = CreateChart(1);
        var original = chart.Series[0];
        var obsolete = Card();
        chart.LegendItemTemplate = new FuncDataTemplate<ChartLegendItem>((_, _) =>
        {
            chart.Series.Remove(original);
            return obsolete;
        });
        using var host = new TemplateFeaturesHost(chart);
        Assert.Empty(chart.LegendItems);
        Assert.Empty(Cards(chart));
        TemplateFeaturesHost.AssertReleased(obsolete);
    }

    [AvaloniaFact]
    public void Export_records_the_scrolled_legend_subtree_once_and_preserves_the_view()
    {
        var chart = CreateChart(20);
        chart.IsLegendScrollable = true;
        chart.IsLegendFloating = true;
        chart.LegendPosition = ChartLegendPosition.Right;
        chart.LegendMaxHeight = 90;
        var builds = 0;
        chart.LegendItemTemplate = new FuncDataTemplate<ChartLegendItem>((_, _) => { builds++; return Card(); });
        using var host = new TemplateFeaturesHost(chart);
        var viewer = Viewer(chart);
        host.Wheel(TemplateFeaturesHost.BoundsInChart(viewer, chart).Center, -100);
        var offset = viewer.Offset;
        var bounds = chart.Bounds;
        var plot = chart.PlotArea;
        var visibleSeries = chart.LegendItems.Select(item => item.Series).ToArray();
        var before = builds;
        var recording = new DrawingGroup();
        using (var drawing = recording.Open()) chart.RenderExport(drawing, chart.GetExportLayout(new() { PixelSize = new(960, 540), Dpi = 144 }), Brushes.White);
        var brushes = TemplateFeaturesHost.Walk(recording).OfType<GeometryDrawing>().Select(drawing => drawing.Brush).OfType<VisualBrush>();
        Assert.Single(brushes, brush => brush.Visual?.GetVisualDescendants().OfType<Border>().Any(control => control.Name == "LegendTemplateRoot") == true);
        Assert.Equal(before, builds);
        Assert.Equal(offset, viewer.Offset);
        Assert.Equal(bounds, chart.Bounds);
        Assert.Equal(plot, chart.PlotArea);
        Assert.Equal(visibleSeries, chart.LegendItems.Select(item => item.Series));
        Assert.Null(chart.Viewport);
    }

    private static Border Card() => new() { Name = "LegendTemplateRoot", Width = 126, Height = 28, Background = Brushes.DarkCyan, Child = new TextBlock { Text = "business legend" } };
    private static Border[] Cards(CartesianChart chart) => chart.GetVisualDescendants().OfType<Border>().Where(control => control.Name == "LegendTemplateRoot").ToArray();
    private static ScrollViewer Viewer(CartesianChart chart) => Assert.Single(chart.GetVisualDescendants().OfType<ScrollViewer>(), control => control.Name == "PART_ChartLegendScrollViewer");
    private static CartesianChart CreateChart(int seriesCount)
    {
        var chart = new CartesianChart { Padding = new Thickness(8), XAxis = new NumericAxis { Minimum = 0, Maximum = 10 }, YAxis = new NumericAxis { Minimum = 0, Maximum = 100 } };
        for (var i = 0; i < seriesCount; i++) chart.Series.Add(new LineSeries { Title = $"business series {i + 1}", ItemsSource = new ChartPoint[] { new(2, 20), new(8, 80) } });
        return chart;
    }
}

internal sealed class TemplateFeaturesHost : IDisposable
{
    public TemplateFeaturesHost(CartesianChart chart, Control? content = null)
    {
        Chart = chart;
        Window = new Window { Width = 760, Height = 460, Content = new Border { Padding = new Thickness(23, 17, 29, 19), Child = content ?? chart } };
        Window.Show();
        Drain();
    }
    public CartesianChart Chart { get; }
    public Window Window { get; }
    public void Drain()
    {
        Dispatcher.UIThread.RunJobs();
        Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        Window.UpdateLayout();
    }
    public void Wheel(Point point, double y)
    {
        Window.MouseWheel(Chart.TranslatePoint(point, Window)!.Value, new Vector(0, y));
        Drain();
    }
    public void Click(Point point)
    {
        var target = Chart.TranslatePoint(point, Window)!.Value;
        Window.MouseDown(target, MouseButton.Left);
        Window.MouseUp(target, MouseButton.Left);
        Drain();
    }
    public static Rect BoundsInChart(Control control, CartesianChart chart) => new(control.TranslatePoint(default, chart)!.Value, control.Bounds.Size);
    public static void AssertReleased(Control control)
    {
        Assert.Null(control.GetVisualParent());
        Assert.Null(((ILogical)control).LogicalParent);
        Assert.Null(control.DataContext);
    }
    public static DrawingGroup Record(CartesianChart chart)
    {
        var result = new DrawingGroup();
        using (var drawing = result.Open()) chart.Render(drawing);
        return result;
    }
    public static IEnumerable<Drawing> Walk(Drawing drawing)
    {
        yield return drawing;
        if (drawing is DrawingGroup group)
            foreach (var child in group.Children)
                foreach (var item in Walk(child)) yield return item;
    }
    public void Dispose() => Window.Close();
}
