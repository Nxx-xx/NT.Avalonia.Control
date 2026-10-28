using System.Reflection;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class RetainedCircleRecordingTests
{
    [AvaloniaFact]
    public void Circle_recordings_reuse_geometry_and_preserve_live_translucent_paint_and_native_order()
    {
        var fill = new SolidColorBrush(Color.FromArgb(131, 17, 83, 151)) { Opacity = .75 };
        var stroke = new SolidColorBrush(Color.FromArgb(173, 201, 29, 73)) { Opacity = .625 };
        var series = new ScatterSeries
        {
            MarkerFill = fill, MarkerStroke = stroke, MarkerSize = 5.375, MarkerStrokeThickness = 1.625,
            ItemsSource = new ChartPoint[] { new(1.125, 2.375), new(1.125, 2.375), new(3.25, double.NaN), new(4.8125, 4.375) },
        };
        var context = new ChartSeriesRenderContext(series, series.CreateSnapshot(), new FractionalProjection(), Brushes.Blue);
        var retained = CartesianSeriesRenderer.Scatter.CreateRetainedDrawing(context);
        var before = Record(retained);
        var geometries = PrivateCircles(retained).ToArray();
        Assert.Equal(3, geometries.Length); Assert.All(geometries, Assert.NotNull);
        var beforeMarks = Marks(before);
        var bounds = beforeMarks.Select(mark => mark.Geometry!.Bounds).ToArray();
        Assert.Equal(bounds[0], bounds[1]);

        fill.Color = Colors.Purple; fill.Opacity = .375;
        stroke.Color = Colors.Green; stroke.Opacity = .25;
        var after = Record(retained);
        Assert.Equal(geometries, PrivateCircles(retained));
        Assert.All(beforeMarks.Concat(Marks(after)), mark =>
        {
            Assert.Same(fill, mark.Brush); Assert.Same(stroke, mark.Pen!.Brush);
            Assert.Equal(1.625, mark.Pen.Thickness);
        });
        Marks(after)[0].Geometry!.Transform = new TranslateTransform(71.25, -19.5);
        Assert.Equal(bounds, Marks(before).Select(mark => mark.Geometry!.Bounds));
        Assert.Equal(bounds, Marks(Record(retained)).Select(mark => mark.Geometry!.Bounds));

        using var expected = new SeriesDrawingLayerCacheTests.NativeBoundary();
        series.Renderer.Render(expected.Drawing, context);
        using var actual = new SeriesDrawingLayerCacheTests.NativeBoundary();
        retained.Draw(actual.Drawing);
        Assert.Equal(expected.Recorder.Operations, actual.Recorder.Operations);
    }

    [AvaloniaFact]
    public void Circle_recording_cache_is_bounded_and_reserved_before_replay()
    {
        var small = Retain(16_384, Brushes.Blue);
        var bytes = small.EstimatedBytes;
        Assert.True(bytes >= 16_384L * (16 + 256));
        Record(small); Assert.Equal(bytes, small.EstimatedBytes);
        Assert.Equal(16_384, PrivateCircles(small).Length);
        var large = Retain(16_385, Brushes.Blue);
        Assert.Null(CircleField(large).GetValue(large));
        Assert.True(large.EstimatedBytes < bytes);
        using var native = new SeriesDrawingLayerCacheTests.NativeBoundary(recordOperations: false);
        large.Draw(native.Drawing); Assert.Equal(16_385, native.Recorder.DrawCount);
    }

    [AvaloniaTheory]
    [InlineData(8_192)]
    [InlineData(10_000)]
    public void Repeated_dense_circle_recordings_reuse_the_reserved_array_and_native_geometry(int count)
    {
        var retained = Retain(count, Brushes.Blue);
        var reserved = PrivateCircles(retained);
        var reservedBytes = retained.EstimatedBytes;
        Assert.Equal(count, reserved.Length);
        Assert.All(reserved, Assert.Null);

        var first = Record(retained);
        Assert.Same(reserved, PrivateCircles(retained));
        var originalWrappers = reserved.ToArray();
        Assert.All(originalWrappers, Assert.NotNull);
        var nativeGeometry = originalWrappers.Select(PlatformGeometry).ToArray();
        var originalDrawings = Marks(first);
        Assert.Equal(count, originalDrawings.Length);

        var second = Record(retained);
        Assert.Same(reserved, PrivateCircles(retained));
        Assert.Equal(reservedBytes, retained.EstimatedBytes);
        var currentDrawings = Marks(second);
        Assert.Equal(count, currentDrawings.Length);
        for (var ordinal = 0; ordinal < count; ordinal++)
        {
            Assert.Same(originalWrappers[ordinal], reserved[ordinal]);
            Assert.Same(nativeGeometry[ordinal], PlatformGeometry(reserved[ordinal]));
            Assert.NotSame(originalDrawings[ordinal], currentDrawings[ordinal]);
            Assert.NotSame(originalDrawings[ordinal].Geometry, currentDrawings[ordinal].Geometry);
            Assert.Equal(originalDrawings[ordinal].Geometry!.Bounds, currentDrawings[ordinal].Geometry!.Bounds);
        }
    }

    [AvaloniaFact]
    public void Spatial_circle_paint_keeps_the_original_recording_path()
    {
        var gradient = new LinearGradientBrush { GradientStops = [new(Colors.Red, 0), new(Colors.Blue, 1)] };
        var retained = Retain(3, gradient);
        Assert.Null(CircleField(retained).GetValue(retained));
        Assert.Equal(3, Marks(Record(retained)).Length);
    }

    private static ChartRetainedSeriesDrawing Retain(int count, IBrush fill)
    {
        var series = new ScatterSeries { MarkerFill = fill, MarkerSize = 5.375,
            ItemsSource = Enumerable.Range(0, count).Select(index => new ChartPoint(index % 5 + .125, index % 9 + .375)).ToArray() };
        return CartesianSeriesRenderer.Scatter.CreateRetainedDrawing(new(series, series.CreateSnapshot(), new FractionalProjection(), Brushes.Blue));
    }

    private static DrawingGroup Record(ChartRetainedSeriesDrawing retained)
    {
        var group = new DrawingGroup(); using (var drawing = group.Open()) retained.Draw(drawing); return group;
    }

    private static GeometryDrawing[] Marks(DrawingGroup group) => TemplateFeaturesHost.Walk(group).OfType<GeometryDrawing>().ToArray();
    private static FieldInfo CircleField(ChartRetainedSeriesDrawing retained) =>
        retained.GetType().GetField("_recordingCircles", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static EllipseGeometry?[] PrivateCircles(ChartRetainedSeriesDrawing retained) =>
        Assert.IsType<EllipseGeometry?[]>(CircleField(retained).GetValue(retained));
    private static object PlatformGeometry(EllipseGeometry? geometry) =>
        typeof(Geometry).GetProperty("PlatformImpl", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(geometry)!;

    private sealed class FractionalProjection : IChartProjection
    {
        public Rect PlotArea => new(12.125, 9.375, 203.625, 151.75);
        public double YOrigin => 148.625;
        public double MapX(double value) => value * 25.375 + 20.125;
        public double MapY(double value) => YOrigin - value * 12.625;
        public Point Project(ChartPoint point) => new(MapX(point.X), MapY(point.Y));
        public ChartPoint Unproject(Point point) => new((point.X - 20.125) / 25.375, (YOrigin - point.Y) / 12.625);
    }
}
