using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class ChartRenderingTests
{
    private static readonly Color SeriesColor = Color.Parse("#D720C5");

    [AvaloniaFact]
    public void Default_axes_belong_to_each_chart_and_null_binding_values_remain_renderable()
    {
        var first = new CartesianChart();
        var second = new CartesianChart();

        Assert.NotSame(first.XAxis, second.XAxis);
        Assert.NotSame(first.YAxis, second.YAxis);
        first.XAxis.Title = "First only";
        Assert.Null(second.XAxis.Title);
        first.SetValue(CartesianChart.XAxisProperty, null!);
        first.SetValue(CartesianChart.YAxisProperty, null!);
        using var fixture = new ChartFixture(first);
        AssertFiniteGeometry(Render(first));
    }

    [AvaloniaFact]
    public void Empty_chart_and_tiny_layout_render_without_nonfinite_geometry()
    {
        var chart = new CartesianChart { Title = "Empty", XAxis = new ChartAxis { Title = "X" }, YAxis = new ChartAxis { Title = "Y" } };
        using var fixture = new ChartFixture(chart);

        AssertFiniteGeometry(Render(chart));
        chart.Width = 24;
        chart.Height = 24;
        fixture.Drain();
        AssertFiniteGeometry(Render(chart), requireNonempty: false);
    }

    [AvaloniaTheory]
    [InlineData("line")]
    [InlineData("bar")]
    [InlineData("scatter")]
    public void All_series_render_finite_shapes_with_negative_constant_and_nonfinite_values(string kind)
    {
        var chart = FixedRangeChart();
        ChartSeries series = kind switch
        {
            "line" => new LineSeries(),
            "bar" => new BarSeries(),
            _ => new ScatterSeries()
        };
        series.Stroke = new SolidColorBrush(SeriesColor);
        series.Fill = new SolidColorBrush(SeriesColor);
        series.ItemsSource = new ChartPoint[] {new(0, -2), new(1, -2), new(2, double.NaN), new(double.PositiveInfinity, 3), new(3, 2)};
        chart.Series.Add(series);
        using var fixture = new ChartFixture(chart);

        var drawings = Render(chart);
        AssertFiniteGeometry(drawings);
        Assert.Equal(kind == "line" ? 4 : 3, SeriesDrawings(drawings).Count);
    }

    [AvaloniaFact]
    public void Observable_items_series_replacement_and_removal_update_rendered_markers()
    {
        var items = new ObservableCollection<ChartPoint> { new(0, 0), new(1, 1) };
        var series = new ScatterSeries { ItemsSource = items, Fill = new SolidColorBrush(SeriesColor), Stroke = new SolidColorBrush(SeriesColor) };
        var chart = FixedRangeChart();
        chart.Series.Add(series);
        using var fixture = new ChartFixture(chart);

        var initial = SeriesDrawings(Render(chart));
        Assert.Equal(2, initial.Count);
        items.Add(new ChartPoint(2, 2));
        fixture.Drain();
        Assert.Equal(3, SeriesDrawings(Render(chart)).Count);

        items[0] = new ChartPoint(3, -2);
        fixture.Drain();
        Assert.NotEqual(initial[0].GetBounds(), SeriesDrawings(Render(chart))[0].GetBounds());
        items.RemoveAt(1);
        fixture.Drain();
        Assert.Equal(2, SeriesDrawings(Render(chart)).Count);

        series.ItemsSource = new ChartPoint[] {new ChartPoint(2, -1)};
        fixture.Drain();
        Assert.Single(SeriesDrawings(Render(chart)));
        items.Clear();
        fixture.Drain();
        Assert.Single(SeriesDrawings(Render(chart)));

        chart.Series[0] = new ScatterSeries { ItemsSource = new ChartPoint[] {new(0, 1), new(1, 2), new(2, 3)}, Fill = new SolidColorBrush(SeriesColor), Stroke = new SolidColorBrush(SeriesColor) };
        fixture.Drain();
        Assert.Equal(3, SeriesDrawings(Render(chart)).Count);
        chart.Series.Clear();
        fixture.Drain();
        Assert.Empty(SeriesDrawings(Render(chart)));
    }

    [AvaloniaFact]
    public void Data_sources_are_unsubscribed_when_replaced_removed_or_detached()
    {
        var original = new CountingPoints([new(0, 0)]);
        var replacement = new CountingPoints([new(1, 1)]);
        var series = new ScatterSeries { ItemsSource = original };
        var chart = FixedRangeChart();
        chart.Series.Add(series);
        using var fixture = new ChartFixture(chart);

        Assert.Equal(1, original.SubscriberCount);
        series.ItemsSource = replacement;
        Assert.Equal(0, original.SubscriberCount);
        Assert.Equal(1, replacement.SubscriberCount);
        chart.Series.Remove(series);
        Assert.Equal(0, replacement.SubscriberCount);
        chart.Series.Add(series);
        Assert.Equal(1, replacement.SubscriberCount);
        fixture.Window.Content = null;
        Assert.Equal(0, replacement.SubscriberCount);
        fixture.Window.Content = chart;
        fixture.Drain();
        Assert.Equal(1, replacement.SubscriberCount);
    }

    [AvaloniaFact]
    public void Points_outside_explicit_axes_are_clipped_instead_of_piled_at_the_plot_edges()
    {
        var chart = FixedRangeChart();
        chart.Series.Add(new ScatterSeries
        {
            Fill = new SolidColorBrush(SeriesColor),
            Stroke = new SolidColorBrush(SeriesColor),
            ItemsSource = new ChartPoint[] {new(0, 0), new(-100, 0), new(100, 0), new(0, -100), new(0, 100)}
        });
        using var fixture = new ChartFixture(chart);

        Assert.Single(SeriesDrawings(Render(chart)));
    }

    [AvaloniaFact]
    public void Light_dark_and_local_brush_overrides_apply_to_the_chart()
    {
        var chart = FixedRangeChart();
        using var fixture = new ChartFixture(chart);
        fixture.Window.RequestedThemeVariant = ThemeVariant.Light;
        fixture.Drain();
        var light = Assert.IsAssignableFrom<ISolidColorBrush>(chart.Foreground).Color;
        fixture.Window.RequestedThemeVariant = ThemeVariant.Dark;
        fixture.Drain();

        Assert.NotEqual(light, Assert.IsAssignableFrom<ISolidColorBrush>(chart.Foreground).Color);
        chart.Resources["ChartForegroundBrush"] = Brushes.HotPink;
        fixture.Drain();
        Assert.Equal(Colors.HotPink, Assert.IsAssignableFrom<ISolidColorBrush>(chart.Foreground).Color);
        AssertFiniteGeometry(Render(chart));
    }

    [AvaloniaFact]
    public void Xaml_consumer_can_create_axes_and_all_three_series_types()
    {
        var view = new XamlConsumerView();
        var chart = view.FindControl<CartesianChart>("Chart")!;
        using var fixture = new ChartFixture(view);

        Assert.Equal("XAML chart", chart.Title);
        Assert.Equal("Sample", chart.XAxis.Title);
        Assert.Equal(4, chart.XAxis.Maximum);
        Assert.Equal("0.0", chart.YAxis.LabelFormat);
        Assert.Collection(chart.Series, series => Assert.IsType<LineSeries>(series), series => Assert.IsType<BarSeries>(series), series => Assert.IsType<ScatterSeries>(series));
        AssertFiniteGeometry(Render(chart));
    }

    private static CartesianChart FixedRangeChart() => new()
    {
        ShowLegend = false,
        XAxis = new ChartAxis { Minimum = -1, Maximum = 4 },
        YAxis = new ChartAxis { Minimum = -4, Maximum = 4 }
    };

    private static List<GeometryDrawing> Render(CartesianChart chart)
    {
        var group = new DrawingGroup();
        using (var context = group.Open())
            chart.Render(context);
        return Flatten(group).OfType<GeometryDrawing>().ToList();
    }

    private static IEnumerable<Drawing> Flatten(Drawing drawing)
    {
        yield return drawing;
        if (drawing is DrawingGroup group)
            foreach (var child in group.Children)
                foreach (var descendant in Flatten(child))
                    yield return descendant;
    }

    private static List<GeometryDrawing> SeriesDrawings(IEnumerable<GeometryDrawing> drawings) => drawings
        .Where(drawing => IsSeriesBrush(drawing.Brush) || IsSeriesBrush(drawing.Pen?.Brush)).ToList();

    private static bool IsSeriesBrush(IBrush? brush) => brush is ISolidColorBrush solid && solid.Color == SeriesColor;

    private static void AssertFiniteGeometry(IEnumerable<GeometryDrawing> drawings, bool requireNonempty = true)
    {
        var materialized = drawings.ToArray();
        if (requireNonempty)
            Assert.NotEmpty(materialized);
        Assert.All(materialized, drawing =>
        {
            var bounds = drawing.GetBounds();
            Assert.True(double.IsFinite(bounds.X));
            Assert.True(double.IsFinite(bounds.Y));
            Assert.True(double.IsFinite(bounds.Width));
            Assert.True(double.IsFinite(bounds.Height));
        });
    }

    private sealed class ChartFixture : IDisposable
    {
        public ChartFixture(Control content)
        {
            Window = new Window { Width = 640, Height = 360, Content = content };
            Window.Show();
            Drain();
        }

        public Window Window { get; }
        public void Drain() { Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); }
        public void Dispose() => Window.Close();
    }

    private sealed class CountingPoints(IEnumerable<ChartPoint> points) : IEnumerable<ChartPoint>, INotifyCollectionChanged
    {
        private NotifyCollectionChangedEventHandler? _changed;
        public int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;
        public event NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add => _changed += value;
            remove => _changed -= value;
        }
        public IEnumerator<ChartPoint> GetEnumerator() => points.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
