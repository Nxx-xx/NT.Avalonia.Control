using System.Reflection;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class RetainedBarRecordingTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Repeated_recordings_reuse_private_rectangles_and_keep_live_paint_native_order_and_old_frames(bool markers)
    {
        var fill = new SolidColorBrush(Color.FromArgb(131, 17, 83, 151)) { Opacity = .75 };
        var series = new BarSeries
        {
            Fill = fill, ShowMarkers = markers, MarkerSize = 5,
            ItemsSource = new ChartPoint[] { new(1.125, 2.375), new(1.125, 8.625), new(3.25, double.NaN), new(4.8125, 4.375) },
        };
        var context = new ChartSeriesRenderContext(series, series.CreateSnapshot(), new FractionalProjection(), Brushes.Blue, 1, 2, 35.75);
        var retained = CartesianSeriesRenderer.Bar.CreateRetainedDrawing(context);
        var before = Record(retained);
        var geometries = PrivateRectangles(retained);
        var originals = geometries.ToArray();
        Assert.Contains(originals, geometry => geometry is not null);
        var oldBounds = Geometries(before).Select(geometry => geometry.Bounds).ToArray();

        fill.Color = Color.FromArgb(97, 201, 29, 73); fill.Opacity = .375;
        var after = Record(retained);
        Assert.Equal(originals, PrivateRectangles(retained));
        Assert.Contains(TemplateFeaturesHost.Walk(after).OfType<GeometryDrawing>(), drawing => ReferenceEquals(drawing.Brush, fill));

        // A caller can alter an exported DrawingGroup, including its geometry
        // wrappers. That must not alter the retained source rectangle or a
        // previous frame that is still waiting for native playback.
        var exported = Geometries(after).First();
        Assert.DoesNotContain(originals, geometry => ReferenceEquals(geometry, exported));
        exported.Transform = new TranslateTransform(71.25, -19.5);
        Assert.Equal(oldBounds, Geometries(before).Select(geometry => geometry.Bounds));
        var current = Record(retained);
        Assert.Equal(oldBounds, Geometries(current).Select(geometry => geometry.Bounds));

        // Recording reuse must not turn immediate native DrawRectangle into
        // DrawGeometry or move a marker ahead of the following translucent bar.
        using var expected = new SeriesDrawingLayerCacheTests.NativeBoundary();
        series.Renderer.Render(expected.Drawing, context);
        using var actual = new SeriesDrawingLayerCacheTests.NativeBoundary();
        retained.Draw(actual.Drawing);
        Assert.Equal(expected.Recorder.Operations, actual.Recorder.Operations);
    }

    [AvaloniaFact]
    public void Rectangle_recording_payload_is_bounded_and_reserved_before_first_replay()
    {
        var small = Retain(8_192);
        var bytes = small.EstimatedBytes;
        Assert.True(bytes >= 8_192L * (128 + 256));
        Record(small);
        Assert.Equal(bytes, small.EstimatedBytes);
        Assert.Equal(8_192, PrivateRectangles(small).Length);
        var large = Retain(8_193);
        Assert.Null(RectangleField(large).GetValue(large));
        Assert.True(large.EstimatedBytes < bytes);
        using var immediate = new SeriesDrawingLayerCacheTests.NativeBoundary(recordOperations: false);
        large.Draw(immediate.Drawing);
        Assert.Equal(8_193, immediate.Recorder.DrawCount);
    }

    private static ChartRetainedSeriesDrawing Retain(int count)
    {
        var series = new BarSeries { Fill = Brushes.Blue, ShowMarkers = false,
            ItemsSource = Enumerable.Range(0, count).Select(index => new ChartPoint(index % 5 + .125, index % 9 + .375)).ToArray() };
        return CartesianSeriesRenderer.Bar.CreateRetainedDrawing(new(series, series.CreateSnapshot(), new FractionalProjection(), Brushes.Blue));
    }

    private static DrawingGroup Record(ChartRetainedSeriesDrawing retained)
    {
        var group = new DrawingGroup();
        using (var drawing = group.Open()) retained.Draw(drawing);
        return group;
    }

    private static IEnumerable<Geometry> Geometries(DrawingGroup group) =>
        TemplateFeaturesHost.Walk(group).OfType<GeometryDrawing>().Select(drawing => drawing.Geometry!).Where(geometry => geometry is not null);

    private static FieldInfo RectangleField(ChartRetainedSeriesDrawing retained) =>
        retained.GetType().GetField("_recordingRectangles", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static RectangleGeometry?[] PrivateRectangles(ChartRetainedSeriesDrawing retained) =>
        Assert.IsType<RectangleGeometry?[]>(RectangleField(retained).GetValue(retained));

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
