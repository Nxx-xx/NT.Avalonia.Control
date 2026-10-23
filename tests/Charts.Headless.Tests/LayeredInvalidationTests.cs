using System.Reflection;
using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class LayeredInvalidationTests
{
    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void Viewport_and_layout_reuse_prepared_coordinates_without_enumerating_the_source(int kind)
    {
        var source = new Source(Enumerable.Range(0, 64).Select(i => new Row($"C{i}", i + 1)).ToArray());
        var series = MakeSeries(kind);
        series.ItemsSource = source; series.CategoryPath = nameof(Row.Category); series.ValuePath = nameof(Row.Value);
        var chart = new CartesianChart { Width = 720, Height = 360, ShowLegend = false, XAxis = new CategoryAxis() };
        if (kind == 5) { chart.XAxis = new NumericAxis(); chart.YAxis = new CategoryAxis(); }
        chart.Series.Add(series);
        if (kind == 3) chart.Series.Add(new AreaSeries { StackMode = ChartStackMode.Stacked,
            ItemsSource = new[] { new Row("C0", 3), new Row("C1", 5) }, CategoryPath = nameof(Row.Category), ValuePath = nameof(Row.Value) });
        using var host = new Host(chart);
        Draw(chart);
        var prepared = Field(chart, "_preparedData");
        var data = PreparedSnapshots(chart);
        var builds = Counter(chart, "_preparedDataBuildCount");
        var enumerationCount = source.Enumerations;

        var full = chart.VisibleRange!.Value;
        chart.Viewport = full with { XMinimum = 2, XMaximum = 30 };
        Draw(chart);
        chart.Padding = new Thickness(16);
        chart.FontSize = 15;
        Draw(chart);
        chart.XAxis.LabelRotation = 25;
        Draw(chart);

        Assert.Equal(builds, Counter(chart, "_preparedDataBuildCount"));
        Assert.Same(prepared, Field(chart, "_preparedData"));
        Assert.Equal(enumerationCount, source.Enumerations);
        Assert.All(data.Zip(PreparedSnapshots(chart)), pair => Assert.Same(pair.First, pair.Second));
        Assert.Equal(64, series.CreateSnapshot().Points.Count);
    }

    [AvaloniaFact]
    public void Viewport_reuses_full_range_scales_and_unchanged_axis_text()
    {
        var chart = new CartesianChart { Width = 720, Height = 360, ShowLegend = false };
        chart.Series.Add(new LineSeries { MarkerSize = 0, ItemsSource = new[] { new ChartPoint(0, 0), new ChartPoint(100, 100) } });
        using var host = new Host(chart);
        Draw(chart);
        var count = Counter(chart, "_baseScaleBuildCount");
        var texts = (IDictionary)Field(chart, "_axisTexts")!;
        var previous = texts.Values.Cast<object>().ToArray();
        chart.Viewport = new(10, 90, 10, 90);
        Draw(chart);
        Assert.Equal(count, Counter(chart, "_baseScaleBuildCount"));
        Assert.All(previous, text => Assert.Contains(texts.Values.Cast<object>(), current => ReferenceEquals(text, current)));
    }

    [AvaloniaFact]
    public void Collection_mapping_and_published_revisions_replace_prepared_data()
    {
        var source = new Source([new Row("A", 2), new Row("B", 4)]);
        var series = new LineSeries { ItemsSource = source, CategoryPath = nameof(Row.Category), ValuePath = nameof(Row.Value) };
        var chart = new CartesianChart { Width = 720, Height = 360, ShowLegend = false, XAxis = new CategoryAxis() };
        chart.Series.Add(series);
        using var host = new Host(chart);
        Draw(chart);
        var old = Field(chart, "_preparedData");
        source.Replace(0, new Row("A", 20));
        Draw(chart);
        Assert.NotSame(old, Field(chart, "_preparedData"));
        Assert.True(chart.VisibleRange!.Value.YMaximum >= 20);
        old = Field(chart, "_preparedData");
        var snapshot = series.CreateSnapshot();
        Assert.True(series.TryPublishSnapshot(snapshot, series.SourceVersion));
        Draw(chart);
        Assert.NotSame(old, Field(chart, "_preparedData"));
    }

    [AvaloniaFact]
    public void Domain_break_item_changes_cannot_reuse_filtered_coordinates()
    {
        var chart = new CartesianChart { Width = 720, Height = 360, ShowLegend = false };
        chart.Series.Add(new LineSeries { MarkerSize = 0, ItemsSource = new[] { new ChartPoint(0, 1), new ChartPoint(5, 5), new ChartPoint(10, 10) } });
        var gap = new ChartScaleBreak { Start = 4, End = 6 };
        chart.XAxis.ScaleBreaks.Add(gap);
        using var host = new Host(chart);
        Draw(chart);
        var first = PreparedSnapshots(chart)[0];
        Assert.True(first.Points[1].IsEmpty);
        gap.Start = 7; gap.End = 9;
        Draw(chart);
        var second = PreparedSnapshots(chart)[0];
        Assert.NotSame(first, second);
        Assert.False(second.Points[1].IsEmpty);
    }

    [AvaloniaFact]
    public void Palette_and_series_visibility_rebuild_point_styles_and_auto_ranges()
    {
        var chart = new CartesianChart { Width = 720, Height = 360, ShowLegend = false };
        var points = new BubbleSeries { ItemsSource = new[] { new ChartPoint(0, 2), new ChartPoint(1, 3) } };
        var far = new LineSeries { ItemsSource = new[] { new ChartPoint(0, 200), new ChartPoint(1, 300) } };
        chart.Series.Add(points); chart.Series.Add(far);
        using var host = new Host(chart);
        Draw(chart);
        var data = PreparedSnapshots(chart)[0];
        chart.ColorPalette = new[] { Brushes.Lime, Brushes.Purple };
        Draw(chart);
        Assert.NotSame(data, PreparedSnapshots(chart)[0]);
        far.IsVisible = false;
        Draw(chart);
        Assert.True(chart.VisibleRange!.Value.YMaximum < 10);
    }

    [AvaloniaFact]
    public void Detach_releases_data_and_view_caches_and_reobserve_reads_current_source()
    {
        var source = new Source([new Row("A", 2), new Row("B", 4)]);
        var chart = new CartesianChart { Width = 720, Height = 360, ShowLegend = false, XAxis = new CategoryAxis() };
        chart.Series.Add(new LineSeries { ItemsSource = source, CategoryPath = nameof(Row.Category), ValuePath = nameof(Row.Value) });
        using var host = new Host(chart);
        Draw(chart);
        Assert.NotNull(Field(chart, "_preparedData"));
        host.Window.Content = null;
        Assert.Null(Field(chart, "_preparedData"));
        Assert.Empty((IDictionary)Field(chart, "_baseScaleCache")!);
        source.Replace(0, new Row("A", 200));
        host.Window.Content = chart; host.Window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
        Draw(chart);
        Assert.True(chart.VisibleRange!.Value.YMaximum >= 200);
    }

    [AvaloniaFact]
    public void Full_text_cache_replaces_stale_ticks_and_reuses_current_labels_on_hover()
    {
        var chart = new CartesianChart { Width = 720, Height = 360, ShowLegend = false };
        var series = new LineSeries { MarkerSize = 0 };
        chart.Series.Add(series);
        using var host = new Host(chart);
        for (var index = 0; index < 100; index++)
        {
            series.ItemsSource = new[] { new ChartPoint(0, index * 1000 + 1), new ChartPoint(10, index * 1000 + 9) };
            Draw(chart);
        }
        Draw(chart);
        var count = Counter(chart, "_axisTextBuildCount");
        for (var index = 0; index < 8; index++) Draw(chart);
        Assert.Equal(count, Counter(chart, "_axisTextBuildCount"));
        Assert.Equal(256, ((IDictionary)Field(chart, "_axisTexts")!).Count);
    }

    [AvaloniaFact]
    public void Paint_only_changes_keep_stacked_coordinates_but_refresh_the_view_brush()
    {
        var chart = new CartesianChart { Width = 720, Height = 360, ShowLegend = false };
        var series = new AreaSeries { StackMode = ChartStackMode.Stacked, ItemsSource = new[] { new ChartPoint(0, 2), new ChartPoint(10, 9) } };
        chart.Series.Add(series);
        using var host = new Host(chart);
        Draw(chart);
        var old = Field(chart, "_preparedData");
        var count = Counter(chart, "_preparedDataBuildCount");
        series.Stroke = Brushes.Lime;
        series.StrokeThickness = 6;
        series.MarkerShape = ChartMarkerShape.Diamond;
        series.Title = "Changed legend";
        Draw(chart);
        Assert.Equal(count, Counter(chart, "_preparedDataBuildCount"));
        Assert.Same(old, Field(chart, "_preparedData"));
        Assert.Same(Brushes.Lime, chart.GetSeriesBrush(series));
        var scene = Field(chart, "_scene")!;
        var snapshots = (IEnumerable)scene.GetType().GetProperty("Snapshots")!.GetValue(scene)!;
        Assert.All(snapshots.Cast<object>(), snapshot => Assert.Same(Brushes.Lime, snapshot.GetType().GetProperty("Brush")!.GetValue(snapshot)));
    }

    private static ChartSeries MakeSeries(int kind) => kind switch
    {
        0 => new LineSeries(), 1 => new ScatterSeries(), 2 => new AreaSeries(),
        3 => new AreaSeries { StackMode = ChartStackMode.Stacked },
        4 => new BubbleSeries(), _ => new BarSeries { Orientation = Avalonia.Layout.Orientation.Horizontal },
    };
    private sealed record Row(string Category, double Value);
    private sealed class Source(Row[] rows) : IEnumerable, INotifyCollectionChanged
    {
        public int Enumerations;
        public event NotifyCollectionChangedEventHandler? CollectionChanged;
        public IEnumerator GetEnumerator() { Enumerations++; return rows.GetEnumerator(); }
        public void Replace(int index, Row value)
        {
            var previous = rows[index]; rows[index] = value;
            CollectionChanged?.Invoke(this, new(NotifyCollectionChangedAction.Replace, value, previous, index));
        }
    }
    private static object? Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target);
    private static long Counter(object target, string name) => (long)Field(target, name)!;
    private static ChartDataSnapshot[] PreparedSnapshots(CartesianChart chart)
    {
        var value = Field(chart, "_preparedData")!;
        return ((IEnumerable)value.GetType().GetProperty("Snapshots")!.GetValue(value)!).Cast<object>()
            .Select(item => (ChartDataSnapshot)item.GetType().GetProperty("Data")!.GetValue(item)!).ToArray();
    }
    private static void Draw(CartesianChart chart)
    {
        var group = new DrawingGroup();
        using (var context = group.Open()) chart.Render(context);
        Dispatcher.UIThread.RunJobs();
    }
    private sealed class Host : IDisposable
    {
        public Window Window { get; }
        public Host(CartesianChart chart)
        {
            Window = new Window { Width = 720, Height = 360, Content = chart };
            Window.Show(); Window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
        }
        public void Dispose() => Window.Close();
    }
}
