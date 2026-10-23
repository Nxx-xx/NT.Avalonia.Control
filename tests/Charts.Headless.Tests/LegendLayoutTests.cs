using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class LegendLayoutTests
{
    [AvaloniaTheory]
    [InlineData(ChartLegendPosition.Top)]
    [InlineData(ChartLegendPosition.Bottom)]
    public void Docked_horizontal_alignment_moves_the_legend_without_changing_the_plot(ChartLegendPosition position)
    {
        var chart = CreateChart(); chart.LegendPosition = position;
        using var host = new Host(chart);
        var plot = chart.PlotArea;
        var start = Bounds(chart);
        Assert.Equal(8, start.Left, 6);
        chart.LegendHorizontalAlignment = HorizontalAlignment.Center;
        Assert.Equal(chart.Bounds.Width / 2, Bounds(chart).Center.X, 6);
        Assert.Equal(plot, chart.PlotArea);
        chart.LegendHorizontalAlignment = HorizontalAlignment.Right;
        Assert.Equal(chart.Bounds.Width - 8, Bounds(chart).Right, 6);
        Assert.Equal(plot, chart.PlotArea);
        chart.LegendHorizontalAlignment = HorizontalAlignment.Stretch;
        Assert.Equal(start, Bounds(chart));
    }

    [AvaloniaTheory]
    [InlineData(ChartLegendPosition.Left)]
    [InlineData(ChartLegendPosition.Right)]
    public void Docked_vertical_alignment_uses_the_available_edge_and_keeps_side_legends_vertical(ChartLegendPosition position)
    {
        var chart = CreateChart(); chart.LegendPosition = position;
        using var host = new Host(chart);
        var plot = chart.PlotArea;
        var start = Bounds(chart);
        Assert.Equal(8, start.Top, 6);
        Assert.True(chart.LegendItems[1].Bounds.Top > chart.LegendItems[0].Bounds.Top);
        chart.LegendVerticalAlignment = VerticalAlignment.Center;
        Assert.Equal(chart.Bounds.Height / 2, Bounds(chart).Center.Y, 6);
        Assert.Equal(plot, chart.PlotArea);
        chart.LegendVerticalAlignment = VerticalAlignment.Bottom;
        Assert.Equal(chart.Bounds.Height - 8, Bounds(chart).Bottom, 6);
        Assert.Equal(plot, chart.PlotArea);
        chart.LegendVerticalAlignment = VerticalAlignment.Stretch;
        Assert.Equal(start, Bounds(chart));
    }

    [AvaloniaTheory]
    [InlineData(ChartLegendPosition.Top)]
    [InlineData(ChartLegendPosition.Bottom)]
    [InlineData(ChartLegendPosition.Left)]
    [InlineData(ChartLegendPosition.Right)]
    public void Floating_legend_uses_final_plot_coordinates_and_never_reserves_series_space(ChartLegendPosition position)
    {
        var chart = CreateChart(); chart.ShowLegend = false;
        using var host = new Host(chart);
        var expected = chart.PlotArea;
        chart.IsLegendFloating = true; chart.ShowLegend = true; chart.LegendPosition = position;
        chart.FloatingLegendMargin = new Thickness(11, 13, 17, 19);
        chart.LegendHorizontalAlignment = HorizontalAlignment.Right;
        chart.LegendVerticalAlignment = VerticalAlignment.Bottom;
        Assert.Equal(expected, chart.PlotArea);
        var bounds = Bounds(chart);
        Assert.All(chart.LegendItems, item => Assert.True(expected.Contains(item.Bounds)));
        if (position is ChartLegendPosition.Top or ChartLegendPosition.Bottom)
        {
            Assert.Equal(expected.Right - 17, bounds.Right, 6);
            if (position == ChartLegendPosition.Top) Assert.Equal(expected.Top + 13, bounds.Top, 6);
            else Assert.Equal(expected.Bottom - 19, bounds.Bottom, 6);
        }
        else
        {
            Assert.Equal(expected.Bottom - 19, bounds.Bottom, 6);
            if (position == ChartLegendPosition.Left) Assert.Equal(expected.Left + 11, bounds.Left, 6);
            else Assert.Equal(expected.Right - 17, bounds.Right, 6);
        }
        chart.LegendHorizontalAlignment = HorizontalAlignment.Center;
        chart.LegendVerticalAlignment = VerticalAlignment.Center;
        var inner = new Rect(expected.Left + 11, expected.Top + 13, expected.Width - 28, expected.Height - 32);
        if (position is ChartLegendPosition.Top or ChartLegendPosition.Bottom) Assert.Equal(inner.Center.X, Bounds(chart).Center.X, 6);
        else Assert.Equal(inner.Center.Y, Bounds(chart).Center.Y, 6);
        Assert.Equal(expected, chart.PlotArea);
    }

    [AvaloniaFact]
    public void Floating_overflow_and_extreme_finite_margin_remain_bounded_and_do_not_remove_the_plot()
    {
        var chart = CreateChart(); chart.IsLegendFloating = true; chart.LegendOrientation = Orientation.Vertical;
        for (var i = 0; i < 200; i++) chart.Series.Add(new LineSeries { Title = new string('W', 300) });
        using var host = new Host(chart);
        var plot = chart.PlotArea;
        Assert.InRange(chart.LegendItems.Count, 1, 30);
        Assert.All(chart.LegendItems, item => Assert.True(plot.Contains(item.Bounds)));
        chart.FloatingLegendMargin = new Thickness(double.MaxValue);
        Assert.Empty(chart.LegendItems);
        Assert.Equal(plot, chart.PlotArea);
        Assert.Throws<ArgumentException>(() => chart.FloatingLegendMargin = new Thickness(double.NaN));
        Assert.Throws<ArgumentException>(() => chart.FloatingLegendMargin = new Thickness(-1));
    }

    [AvaloniaFact]
    public void Floating_legend_click_has_priority_and_can_restore_all_hidden_series_or_be_canceled()
    {
        var chart = CreateChart(); chart.IsLegendFloating = true; chart.SelectionMode = ChartSelectionMode.Multiple;
        using var host = new Host(chart);
        var colors = chart.Series.Select(chart.GetSeriesBrush).ToArray();
        foreach (var series in chart.Series)
        {
            host.Click(chart.LegendItems.Single(item => item.Series == series).Bounds.Center);
            Assert.False(series.IsVisible);
        }
        Assert.Empty(chart.SelectedPoints!);
        Assert.Equal(2, chart.LegendItems.Count);
        foreach (var series in chart.Series) host.Click(chart.LegendItems.Single(item => item.Series == series).Bounds.Center);
        Assert.All(chart.Series, series => Assert.True(series.IsVisible));
        Assert.Equal(colors, chart.Series.Select(chart.GetSeriesBrush));
        chart.LegendItemInvoked += (_, args) => args.Cancel = true;
        host.Click(chart.LegendItems[0].Bounds.Center);
        Assert.True(chart.Series[0].IsVisible);
        Assert.Empty(chart.SelectedPoints!);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Floating_panel_blocks_underlying_pointer_selection_hover_and_zoom_in_items_and_empty_background(bool emptyBackground)
    {
        var chart = CreateChart(); chart.IsLegendFloating = true; chart.IsLegendInteractive = false;
        chart.IsZoomEnabled = true; chart.IsPanEnabled = true; chart.SelectionMode = ChartSelectionMode.Multiple;
        if (emptyBackground)
        {
            chart.LegendOrientation = Orientation.Vertical;
            chart.Series[0].Title = "A"; chart.Series[1].Title = new string('W', 16);
        }
        using var host = new Host(chart);
        var center = emptyBackground ? new Point((chart.LegendItems[0].Bounds.Right + chart.LegendItems[1].Bounds.Right) / 2, chart.LegendItems[0].Bounds.Center.Y) : chart.LegendItems[0].Bounds.Center;
        if (emptyBackground) Assert.DoesNotContain(chart.LegendItems, item => item.Bounds.Contains(center));
        var plot = chart.PlotArea;
        var point = new ChartPoint((center.X - plot.Left) / plot.Width * 10, (plot.Bottom - center.Y) / plot.Height * 10);
        chart.Series[0].ItemsSource = new[] { point };
        host.Drain();
        Assert.NotNull(chart.HitTestPoint(center));
        host.Move(center); Assert.Null(chart.HoveredPoint);
        host.Click(center); Assert.Empty(chart.SelectedPoints!); Assert.True(chart.Series[0].IsVisible);
        host.Wheel(center); Assert.Null(chart.Viewport);
        host.Move(center + new Vector(60, 60)); Assert.Null(chart.Viewport);
    }

    [AvaloniaFact]
    public void Floating_background_and_legend_are_drawn_after_series_and_above_annotations()
    {
        var chart = CreateChart(); chart.IsLegendFloating = true; chart.FloatingLegendBackground = Brushes.Yellow;
        chart.Annotations.Add(new CustomAnnotation { Renderer = (context, annotation) => context.DrawRectangle(Brushes.Green, null, annotation.PlotArea) });
        chart.LegendRenderer = (context, item) => context.DrawRectangle(Brushes.Magenta, null, item.Bounds);
        using var host = new Host(chart);
        var group = new DrawingGroup(); using (var context = group.Open()) chart.Render(context);
        var colors = Flatten(group).OfType<GeometryDrawing>().Select(item => (item.Brush as ISolidColorBrush)?.Color).ToArray();
        Assert.Contains(Colors.Green, colors); Assert.Contains(Colors.Yellow, colors); Assert.Contains(Colors.Magenta, colors);
        Assert.True(Array.LastIndexOf(colors, Colors.Green) < Array.IndexOf(colors, Colors.Yellow));
        Assert.True(Array.LastIndexOf(colors, Colors.Yellow) < Array.IndexOf(colors, Colors.Magenta));
    }

    private static CartesianChart CreateChart()
    {
        var chart = new CartesianChart { Padding = new Thickness(8), XAxis = new NumericAxis { Minimum = 0, Maximum = 10 }, YAxis = new NumericAxis { Minimum = 0, Maximum = 10 } };
        chart.Series.Add(new ScatterSeries { Title = "First", ItemsSource = new[] { new ChartPoint(3, 4) } });
        chart.Series.Add(new LineSeries { Title = "Second", ItemsSource = new[] { new ChartPoint(7, 6) } });
        return chart;
    }
    private static Rect Bounds(CartesianChart chart) => chart.LegendItems.Select(item => item.Bounds).Aggregate((left, right) => left.Union(right));
    private static IEnumerable<Drawing> Flatten(Drawing drawing)
    {
        yield return drawing;
        if (drawing is DrawingGroup group) foreach (var child in group.Children) foreach (var item in Flatten(child)) yield return item;
    }
    private sealed class Host : IDisposable
    {
        private readonly CartesianChart _chart;
        public Window Window { get; }
        public Host(CartesianChart chart) { _chart = chart; Window = new Window { Width = 760, Height = 460, Content = chart }; Window.Show(); Drain(); }
        public void Drain() { Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); using var context = new DrawingGroup().Open(); _chart.Render(context); }
        private Point ToWindow(Point point) => _chart.TranslatePoint(point, Window)!.Value;
        public void Click(Point point) { Window.MouseDown(ToWindow(point), MouseButton.Left); Window.MouseUp(ToWindow(point), MouseButton.Left); Drain(); }
        public void Move(Point point) { Window.MouseMove(ToWindow(point)); Drain(); }
        public void Wheel(Point point) { Window.MouseWheel(ToWindow(point), new Vector(0, 1)); Drain(); }
        public void Dispose() => Window.Close();
    }
}
