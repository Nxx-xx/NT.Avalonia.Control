using System.Reflection;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class CircleGeometryHistoryTests
{
    [AvaloniaFact]
    public void Dense_source_replacements_reuse_geometry_keep_live_paint_and_skip_small_viewport_pollution()
    {
        var fill = new SolidColorBrush(Colors.Blue, .75);
        var series = Make(2_048, 0, fill);
        using var owner = new Owner(series);
        var first = Retain(series); var before = Record(first); var original = Circles(first).ToArray();
        var history = History(series)!;
        Assert.Equal(4_096, Value<int>(history, "Capacity"));
        Assert.Equal(2_048, Value<int>(history, "Count"));

        series.ItemsSource = Points(2_048, .25);
        var second = Retain(series); Record(second);
        var secondGeometry = Circles(second).ToArray();
        Assert.Equal(4_096, Value<int>(history, "Count"));
        Assert.NotSame(original[0], secondGeometry[0]);

        // These moving small scenes still reserve the full history, but their
        // point geometry must stay local instead of evicting the dense states.
        for (var revision = 1; revision <= 5; revision++)
        {
            series.ItemsSource = Points(100, revision);
            var small = Retain(series);
            Assert.True(small.EstimatedBytes >= 4_096L * 256 + 100L * 256);
            var reserved = small.EstimatedBytes;
            Record(small); Assert.Equal(reserved, small.EstimatedBytes);
            Assert.Equal(4_096, Value<int>(history, "Count"));
        }
        series.ItemsSource = Points(2_048, 0);
        var restored = Retain(series); var after = Record(restored);
        Assert.NotSame(Circles(first), Circles(restored));
        for (var i = 0; i < original.Length; i++) Assert.Same(original[i], Circles(restored)[i]);
        Assert.NotSame(Marks(before)[0].Geometry, Marks(after)[0].Geometry);
        Marks(after)[0].Geometry!.Transform = new TranslateTransform(19, -7);
        Assert.Equal(Marks(before)[0].Geometry!.Bounds, Marks(Record(restored))[0].Geometry!.Bounds);
        fill.Color = Colors.Purple; fill.Opacity = .375;
        Assert.All(Marks(before).Concat(Marks(after)), drawing => Assert.Same(fill, drawing.Brush));
        Assert.Equal(Colors.Purple, Assert.IsType<SolidColorBrush>(Marks(before)[0].Brush).Color);
    }

    [AvaloniaFact]
    public void Capacity_grows_only_in_construction_is_bounded_and_small_scenes_reserve_previous_dense_history()
    {
        var series = Make(2_048, 0, Brushes.Blue);
        using var owner = new Owner(series);
        _ = Retain(series);
        var history = History(series)!;
        Assert.Equal(4_096, Value<int>(history, "Capacity"));
        series.ItemsSource = Points(10_000, 0);
        var full = Retain(series);
        Assert.Equal(20_000, Value<int>(history, "Capacity"));
        Assert.True(full.EstimatedBytes >= 20_000L * 256 + 10_000L * IntPtr.Size);
        var zoomed = Retain(series, new Projection(100));
        Assert.InRange(Circles(zoomed).Length, 1, 200);
        Assert.True(zoomed.EstimatedBytes >= 20_000L * 256 + Circles(zoomed).Length * 256L);
        var reserved = zoomed.EstimatedBytes;
        Record(zoomed); Assert.Equal(reserved, zoomed.EstimatedBytes);
        Assert.Equal(0, Value<int>(history, "Count"));
        series.ItemsSource = Points(16_384, 0);
        _ = Retain(series);
        Assert.Equal(32_768, Value<int>(history, "Capacity"));
        series.ItemsSource = Points(16_385, 0);
        var overflow = Retain(series);
        Assert.Null(CircleField(overflow).GetValue(overflow));
        Assert.Null(History(series)); Assert.False(Value<bool>(history, "Active"));
        Assert.Equal(0, Value<int>(history, "Count"));
    }

    [AvaloniaFact]
    public void Dense_history_evicts_oldest_state_and_retirement_does_not_break_old_or_unfilled_scene_arrays()
    {
        var series = Make(2_048, 0, Brushes.Blue);
        using var owner = new Owner(series);
        var first = Retain(series); var originalDrawing = Record(first); var original = Circles(first).ToArray();
        var history = History(series)!;
        series.ItemsSource = Points(2_048, .25); Record(Retain(series));
        series.ItemsSource = Points(2_048, .5); var third = Retain(series); Record(third);
        Assert.Equal(4_096, Value<int>(history, "Count"));
        series.ItemsSource = Points(2_048, 0); var returned = Retain(series); Record(returned);
        Assert.NotSame(original[0], Circles(returned)[0]);
        Assert.Equal(4_096, Value<int>(history, "Count"));

        series.ItemsSource = Points(2_048, .75);
        var pending = Retain(series); var pendingBytes = pending.EstimatedBytes;
        Assert.All(Circles(pending), Assert.Null);
        CartesianSeriesRenderer.ReleaseCircleGeometryHistory(series);
        CartesianSeriesRenderer.ReleaseCircleGeometryHistory(series);
        Assert.Null(History(series)); Assert.False(Value<bool>(history, "Active"));
        Assert.Equal(0, Value<int>(history, "Count"));
        Record(pending); Record(first);
        Assert.Equal(pendingBytes, pending.EstimatedBytes);
        Assert.Equal(0, Value<int>(history, "Count")); Assert.Null(History(series));
        Assert.Equal(original, Circles(first));
        Assert.Equal(Marks(originalDrawing).Select(d => d.Geometry!.Bounds), Marks(Record(first)).Select(d => d.Geometry!.Bounds));
    }

    [AvaloniaFact]
    public void Multiple_owners_and_unsupported_paint_retire_sharing_and_keep_scene_local_caches()
    {
        var series = Make(2_048, 0, Brushes.Blue);
        using var owner = new Owner(series);
        var first = Retain(series); Record(first); var history = History(series)!;
        EventHandler other = (_, _) => { };
        series.Changed += other;
        try
        {
            var a = Retain(series); var b = Retain(series); Record(a); Record(b);
            Assert.Null(History(series)); Assert.False(Value<bool>(history, "Active"));
            Assert.NotSame(Circles(a)[0], Circles(b)[0]);
            Assert.Equal(0, Value<int>(history, "Count"));
        }
        finally { series.Changed -= other; }
        Record(Retain(series));
        Assert.NotNull(History(series));
        series.MarkerFill = new LinearGradientBrush { GradientStops = [new(Colors.Red, 0), new(Colors.Blue, 1)] };
        var gradient = Retain(series);
        Assert.Null(CircleField(gradient).GetValue(gradient)); Assert.Null(History(series));
    }

    private static ScatterSeries Make(int count, double revision, IBrush fill) => new()
    { ItemsSource = Points(count, revision), MarkerFill = fill, MarkerSize = 4 };
    private static ChartPoint[] Points(int count, double revision) => Enumerable.Range(0, count).Select(i => new ChartPoint(i + .125, revision + .375)).ToArray();
    private static ChartRetainedSeriesDrawing Retain(ScatterSeries series, IChartProjection? projection = null) =>
        CartesianSeriesRenderer.Scatter.CreateRetainedDrawing(new(series, series.CreateSnapshot(), projection ?? new Projection(), Brushes.Blue));
    private static DrawingGroup Record(ChartRetainedSeriesDrawing retained)
    { var group = new DrawingGroup(); using (var drawing = group.Open()) retained.Draw(drawing); return group; }
    private static GeometryDrawing[] Marks(DrawingGroup group) => TemplateFeaturesHost.Walk(group).OfType<GeometryDrawing>().ToArray();
    private static FieldInfo CircleField(ChartRetainedSeriesDrawing retained) => retained.GetType().GetField("_recordingCircles", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static EllipseGeometry?[] Circles(ChartRetainedSeriesDrawing retained) => Assert.IsType<EllipseGeometry?[]>(CircleField(retained).GetValue(retained));
    private static T Value<T>(object target, string property) => (T)target.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(target)!;
    private static object? History(ChartSeries series)
    {
        var table = typeof(CartesianSeriesRenderer).GetField("CircleHistories", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        object?[] args = [series, null];
        return (bool)table.GetType().GetMethod("TryGetValue")!.Invoke(table, args)! ? args[1] : null;
    }
    private sealed class Owner : IDisposable
    {
        private readonly ChartSeries _series;
        private readonly EventHandler _changed = (_, _) => { };
        internal Owner(ChartSeries series) { _series = series; series.Changed += _changed; }
        public void Dispose() { CartesianSeriesRenderer.ReleaseCircleGeometryHistory(_series); ChartNativeCircleBackend.Clear(_series); _series.Changed -= _changed; }
    }
    private sealed class Projection(double width = 40_000) : IChartProjection
    {
        public Rect PlotArea => new(0, 0, width, 1_000);
        public double MapX(double x) => x + 10.125;
        public double MapY(double y) => 900 - y;
        public double YOrigin => 900;
        public Point Project(ChartPoint point) => new(MapX(point.X), MapY(point.Y));
        public ChartPoint Unproject(Point point) => new(point.X - 10.125, 900 - point.Y);
    }
}
