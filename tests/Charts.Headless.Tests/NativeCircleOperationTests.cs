using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;
using NT.AvaloniaControl.Charts.Skia;

namespace Charts.Headless.Tests;

public sealed class NativeCircleOperationTests
{
    private static readonly Point[] Centers = [new(11.125, 19.375), new(11.125, 19.375), new(43.75, 57.25)];

    [AvaloniaFact]
    public void Unknown_backend_replays_original_ellipse_order_without_realizing_or_growing_resources()
    {
        var fill = new SolidColorBrush(Color.FromArgb(117, 17, 83, 151), .75);
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(199, 201, 29, 73), .625), 2);
        using var operation = Make(fill, pen, snapshot: false);
        var weight = operation.EstimatedBytes;
        var fastFrames = ChartNativeCircleOperation.FastPaintFrameCount;
        using var expected = new SeriesDrawingLayerCacheTests.NativeBoundary();
        foreach (var center in Centers) expected.Drawing.DrawEllipse(fill, pen, center, 2, 2);
        using var actual = new SeriesDrawingLayerCacheTests.NativeBoundary();
        using var frame = operation.WithPaint(fill, pen, snapshotPaint: false);
        actual.Drawing.Custom(frame);
        Assert.Equal(expected.Recorder.Operations, actual.Recorder.Operations);
        Assert.Same(fill, actual.Recorder.Brushes[0]);
        Assert.Equal(0, operation.RealizedCount);
        Assert.Equal(weight, operation.EstimatedBytes);
        Assert.Equal(fastFrames, ChartNativeCircleOperation.FastPaintFrameCount);
    }

    [AvaloniaFact]
    public void Deferred_frame_captures_paint_and_equal_replays_reuse_scene_identity_without_stale_color()
    {
        var fill = new SolidColorBrush(Colors.Blue, .75);
        var stroke = new SolidColorBrush(Colors.Green, .625);
        var pen = new Pen(stroke, 2);
        using var scene = Make(fill, pen);
        using var same = scene.WithPaint(fill, pen);
        Assert.True(scene.Equals(same));
        fill.Color = Colors.Purple; stroke.Color = Colors.Red;
        using var changed = scene.WithPaint(fill, pen);
        Assert.False(scene.Equals(changed));
        using var before = new SeriesDrawingLayerCacheTests.NativeBoundary();
        using var after = new SeriesDrawingLayerCacheTests.NativeBoundary();
        before.Drawing.Custom(scene); after.Drawing.Custom(changed);
        Assert.Contains(before.Recorder.Brushes, brush => brush is ISolidColorBrush { Color: var color } && color == Colors.Blue);
        Assert.Contains(after.Recorder.Brushes, brush => brush is ISolidColorBrush { Color: var color } && color == Colors.Purple);
        Assert.DoesNotContain(before.Recorder.Brushes, brush => ReferenceEquals(brush, fill));
    }

    [AvaloniaFact]
    public void Disposing_one_frame_does_not_dispose_the_shared_scene_or_a_later_frame()
    {
        var fill = new SolidColorBrush(Colors.Blue);
        var pen = new Pen(Brushes.Green, 2);
        var first = Make(fill, pen);
        using var later = first.WithPaint(fill, pen);
        first.Dispose();
        using var boundary = new SeriesDrawingLayerCacheTests.NativeBoundary();
        boundary.Drawing.Custom(later);
        Assert.Equal(Centers.Length, boundary.Recorder.DrawCount);
        Assert.True(later.EstimatedBytes >= ChartNativeCircleOperation.EstimateManagedBytes(Centers.Length));
        Assert.Equal(0, first.EstimatedBytes);
        Assert.Throws<ObjectDisposedException>(() => first.WithPaint(fill, pen));
    }

    [AvaloniaFact]
    public void Stroke_shape_changes_do_not_reuse_an_incompatible_plan_and_live_sync_paint_keeps_reference_semantics()
    {
        var fill = new SolidColorBrush(Colors.Blue);
        var pen = new Pen(Brushes.Green, 2);
        using var frame = Make(fill, pen);
        using var resized = frame.WithPaint(fill, new Pen(Brushes.Green, 3));
        Assert.False(frame.Equals(resized));
        using var live = Make(fill, pen, snapshot: false);
        using var otherLive = live.WithPaint(fill, pen, snapshotPaint: false);
        Assert.False(live.Equals(otherLive));
        fill.Color = Colors.Purple;
        using var boundary = new SeriesDrawingLayerCacheTests.NativeBoundary();
        using var liveFrame = live.WithPaint(fill, pen, snapshotPaint: false);
        boundary.Drawing.Custom(liveFrame);
        Assert.Same(fill, boundary.Recorder.Brushes[0]);
        Assert.Equal(Colors.Purple, Assert.IsAssignableFrom<ISolidColorBrush>(boundary.Recorder.Brushes[0]).Color);
    }

    [AvaloniaFact]
    public void Series_history_reserves_the_largest_capacity_across_source_replacement_and_small_viewports()
    {
        var series = new ScatterSeries { ItemsSource = new[] { 1d, 2d } };
        EventHandler owner = (_, _) => { };
        series.Changed += owner;
        try
        {
            var fill = new SolidColorBrush(Colors.Blue);
            var pen = new Pen(Brushes.Green, 2);
            using var first = ChartNativeCircleOperation.CreateForSeries(series, new Point[4096], 2, fill, pen);
            Assert.Equal(8192, first.SharedHistoryCapacity);
            Assert.Equal(1024L + 8192L * 576 + 4096L * IntPtr.Size, first.EstimatedBytes);

            series.ItemsSource = new[] { 3d, 4d };
            using var replacement = ChartNativeCircleOperation.CreateForSeries(series, new Point[16384], 2, fill, pen);
            Assert.Equal(32768, replacement.SharedHistoryCapacity);
            using var zoom = ChartNativeCircleOperation.CreateForSeries(series, Centers, 2, fill, pen);
            Assert.Equal(32768, zoom.SharedHistoryCapacity);
            Assert.Equal(1024L + 32768L * 576 + Centers.LongLength * IntPtr.Size, zoom.EstimatedBytes);

            using var boundary = new SeriesDrawingLayerCacheTests.NativeBoundary();
            boundary.Drawing.Custom(zoom);
            Assert.Equal(Centers.Length, boundary.Recorder.DrawCount);
            Assert.Equal(0, zoom.RealizedCount);
            Assert.Equal(0, zoom.SharedHistoryCreatedCount);
        }
        finally
        {
            ChartNativeCircleOperation.ClearHistory(series);
            series.Changed -= owner;
        }
    }

    [AvaloniaFact]
    public void Clearing_or_sharing_a_series_resets_history_without_invalidating_recorded_frame_paint()
    {
        var series = new ScatterSeries();
        EventHandler owner = (_, _) => { };
        EventHandler otherOwner = (_, _) => { };
        series.Changed += owner;
        try
        {
            var fill = new SolidColorBrush(Colors.Blue);
            var pen = new Pen(Brushes.Green, 2);
            using var full = ChartNativeCircleOperation.CreateForSeries(series, new Point[4096], 2, fill, pen);
            using var oldFrame = ChartNativeCircleOperation.CreateForSeries(series, Centers, 2, fill, pen);
            Assert.Equal(8192, oldFrame.SharedHistoryCapacity);

            ChartNativeCircleOperation.ClearHistory(series);
            fill.Color = Colors.Purple;
            using var fresh = ChartNativeCircleOperation.CreateForSeries(series, Centers, 2, fill, pen);
            Assert.Equal(Centers.Length * 2, fresh.SharedHistoryCapacity);
            using var before = new SeriesDrawingLayerCacheTests.NativeBoundary();
            using var after = new SeriesDrawingLayerCacheTests.NativeBoundary();
            before.Drawing.Custom(oldFrame); after.Drawing.Custom(fresh);
            Assert.Contains(before.Recorder.Brushes, brush => brush is ISolidColorBrush { Color: var c } && c == Colors.Blue);
            Assert.DoesNotContain(before.Recorder.Brushes, brush => brush is ISolidColorBrush { Color: var c } && c == Colors.Purple);
            Assert.Contains(after.Recorder.Brushes, brush => brush is ISolidColorBrush { Color: var c } && c == Colors.Purple);

            series.Changed += otherOwner;
            using var shared = ChartNativeCircleOperation.CreateForSeries(series, Centers, 2, fill, pen);
            Assert.Equal(0, shared.SharedHistoryCapacity);
            series.Changed -= otherOwner;
            using var exclusiveAgain = ChartNativeCircleOperation.CreateForSeries(series, Centers, 2, fill, pen);
            Assert.Equal(Centers.Length * 2, exclusiveAgain.SharedHistoryCapacity);
        }
        finally
        {
            series.Changed -= otherOwner;
            ChartNativeCircleOperation.ClearHistory(series);
            series.Changed -= owner;
        }
    }

    [Fact]
    public void Public_paint_bridge_rejects_untrusted_getters_without_invoking_them()
    {
        var unknown = new UnknownPaintProvider();
        Assert.False(ChartSkiaPublicPaintBridge.TryGetRenderOptions(unknown, out _));
        Assert.Null(ChartSkiaPublicPaintBridge.TryGetFillPath(unknown));
        Assert.Equal(0, unknown.Reads);
    }

    [Theory]
    [InlineData(255, 1, 255)]
    [InlineData(131, .75, 98)]
    [InlineData(173, .625, 108)]
    [InlineData(117, .75, 87)]
    [InlineData(53, .375, 19)]
    public void Direct_solid_paint_uses_native_alpha_truncation_instead_of_rounding(byte alpha, double opacity, byte expected)
    {
        var brush = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.FromArgb(alpha, 17, 83, 151), opacity);
        var color = ChartSkiaPublicPaintBridge.SolidColor(brush);
        Assert.Equal(expected, color.Alpha);
        Assert.Equal((byte)17, color.Red); Assert.Equal((byte)83, color.Green); Assert.Equal((byte)151, color.Blue);
    }

    [Theory]
    [InlineData(1, 1, 0, 0, 0, 0, 0, 0, 1, true)]
    [InlineData(1.5, 1.5, 0, 0, 0, 0, 0, 0, 1, false)]
    [InlineData(1, 1, .125, 0, 0, 0, 0, 0, 1, false)]
    [InlineData(1, 1, 0, 0, .25, 0, 0, 0, 1, false)]
    [InlineData(1, 1, 0, 0, 2, -3, 0, 0, 1, false)]
    [InlineData(1, 1, 0, 0, 0, 0, .01, 0, 1, false)]
    public void Device_guard_requires_proven_unit_scale_and_zero_phase(double x, double y, double skewX, double skewY,
        double tx, double ty, double p0, double p1, double p2, bool expected)
        => Assert.Equal(expected, new ChartNativeCircleOperation.DeviceTransform(x, y, skewX, skewY, tx, ty, p0, p1, p2).IsUnitSurface);

    [Theory]
    [InlineData(1, 1, 0, 0, 0, 0, 0, 0, 1, true)]
    [InlineData(1, 1, 0, 0, 2, -3, 0, 0, 1, true)]
    [InlineData(1, 1, 0, 0, 40, 35, 0, 0, 1, true)]
    [InlineData(1, 1, 0, 0, -40, -35, 0, 0, 1, true)]
    [InlineData(1, 1, 0, 0, .25, .75, 0, 0, 1, false)]
    [InlineData(1, 1, 0, 0, 40, 35.25, 0, 0, 1, false)]
    [InlineData(1, 1, 0, 0, double.NaN, 0, 0, 0, 1, false)]
    [InlineData(1, 1, 0, 0, 0, double.PositiveInfinity, 0, 0, 1, false)]
    [InlineData(1.5, 1.5, 0, 0, 40, 35, 0, 0, 1, false)]
    [InlineData(1, 1, .125, 0, 40, 35, 0, 0, 1, false)]
    [InlineData(1, 1, 0, 0, 40, 35, .01, 0, 1, false)]
    public void Raster_guard_accepts_only_finite_integer_translation_at_unit_device_scale(double x, double y, double skewX, double skewY,
        double tx, double ty, double p0, double p1, double p2, bool expected)
        => Assert.Equal(expected, new ChartNativeCircleOperation.DeviceTransform(x, y, skewX, skewY, tx, ty, p0, p1, p2).IsUnitRasterTransform);

    private static ChartNativeCircleOperation Make(IBrush fill, Pen pen, bool snapshot = true) =>
        new(Centers, 2, fill, pen, ChartNativeCircleOperation.EstimateManagedBytes(Centers.Length), snapshot);

    private sealed class UnknownPaintProvider
    {
        internal int Reads;
        public RenderOptions RenderOptions { get { Reads++; throw new InvalidOperationException("Untrusted getter must not execute."); } }
        public SkiaSharp.SKPath? FillPath { get { Reads++; throw new InvalidOperationException("Untrusted getter must not execute."); } }
    }
}
