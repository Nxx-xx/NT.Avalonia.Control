using System.Globalization;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class AnnotationShapeCompletionTests
{
    private static readonly Color Ink = Color.Parse("#C911A7");
    private static readonly IBrush InkBrush = new SolidColorBrush(Ink);
    private static readonly Color LabelInk = Colors.Lime;

    [AvaloniaFact]
    public void New_shape_properties_preserve_endpoint_geometry_and_unlabelled_defaults()
    {
        var rectangle = new RectangleAnnotation(); var ellipse = new EllipseAnnotation(); var arrow = new ArrowAnnotation();
        Assert.Equal(0, rectangle.CornerRadius);
        Assert.Equal(ChartEllipseGeometryMode.EndpointBounds, ellipse.GeometryMode);
        Assert.Equal(0, ellipse.X); Assert.Equal(0, ellipse.Y); Assert.Equal(1, ellipse.RadiusX); Assert.Equal(1, ellipse.RadiusY);
        foreach (var shape in new ChartShapeAnnotation[] { rectangle, ellipse, arrow })
        { Assert.Null(shape.Label); Assert.Equal(ChartShapeLabelPosition.Center, shape.LabelPosition); Assert.Equal(default, shape.LabelOffset); }
        ellipse.X1 = 20; ellipse.Y1 = 30; ellipse.X2 = 180; ellipse.Y2 = 130; ellipse.Fill = InkBrush;
        ellipse.X = 700; ellipse.Y = 800; ellipse.RadiusX = 900; ellipse.RadiusY = 1000;
        EqualRect(new Rect(20, 30, 160, 100), Assert.Single(Geometry(Render(ellipse, Context()), Ink)).Geometry!.Bounds);
    }

    [AvaloniaTheory]
    [InlineData(0, 0)] [InlineData(4, 4)] [InlineData(100, 30)] [InlineData(double.MaxValue, 30)]
    public void Rectangle_corner_radius_is_a_clamped_DIP_value(double requested, double expected)
    {
        var rectangle = new RectangleAnnotation { X1 = 20, Y1 = 30, X2 = 120, Y2 = 90, CornerRadius = requested, Fill = InkBrush };
        using var recording = new RectangleRecordingContext();
        rectangle.Render(recording.Drawing, Context());
        var geometry = Assert.Single(recording.Rectangles).Shape;
        EqualRect(new Rect(20, 30, 100, 60), geometry.Rect);
        EqualRadii(expected, geometry);
        Assert.Equal(requested, rectangle.CornerRadius);
    }

    [AvaloniaTheory]
    [InlineData(-1)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(double.NegativeInfinity)]
    public void Radii_and_corner_radii_reject_negative_or_nonfinite_dimensions(double invalid)
    {
        Assert.Throws<ArgumentException>(() => new RectangleAnnotation().CornerRadius = invalid);
        var ellipse = new EllipseAnnotation();
        Assert.Throws<ArgumentException>(() => ellipse.RadiusX = invalid);
        Assert.Throws<ArgumentException>(() => ellipse.RadiusY = invalid);
    }

    [AvaloniaFact]
    public void Shape_label_and_geometry_selectors_reject_invalid_values()
    {
        var rectangle = new RectangleAnnotation();
        Assert.Throws<ArgumentException>(() => rectangle.LabelPosition = (ChartShapeLabelPosition)100);
        Assert.Throws<ArgumentException>(() => rectangle.LabelOffset = new Vector(double.NaN, 0));
        Assert.Throws<ArgumentException>(() => rectangle.LabelOffset = new Vector(0, double.PositiveInfinity));
        Assert.Throws<ArgumentException>(() => new EllipseAnnotation().GeometryMode = (ChartEllipseGeometryMode)100);
    }

    [AvaloniaFact]
    public void Center_radius_uses_projected_extrema_and_ignores_inactive_endpoint_values()
    {
        var ellipse = CenterEllipse(); ellipse.X1 = double.NaN; ellipse.Y1 = double.PositiveInfinity;
        ellipse.X2 = double.NegativeInfinity; ellipse.Y2 = double.NaN;
        EqualRect(new Rect(40, 50, 120, 60), Assert.Single(Geometry(Render(ellipse, Context()), Ink)).Geometry!.Bounds);
        ellipse.GeometryMode = ChartEllipseGeometryMode.EndpointBounds;
        Assert.Empty(Leaves(Render(ellipse, Context())));
    }

    [AvaloniaTheory]
    [InlineData(0, 1)] [InlineData(1, 0)] [InlineData(0, 0)]
    public void Zero_data_radii_skip_geometry_and_label(double radiusX, double radiusY)
    {
        var ellipse = CenterEllipse(); ellipse.RadiusX = radiusX; ellipse.RadiusY = radiusY; ellipse.Label = "not drawn";
        Assert.Empty(Leaves(Render(ellipse, Context())));
    }

    [AvaloniaTheory]
    [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(double.NegativeInfinity)] [InlineData(double.MaxValue)]
    public void Invalid_or_overflowing_data_center_extrema_are_not_drawn(double center)
    {
        var ellipse = CenterEllipse(); ellipse.X = center; ellipse.RadiusX = double.MaxValue; ellipse.Label = "not drawn";
        Assert.Empty(Leaves(Render(ellipse, Context())));
    }

    [AvaloniaTheory]
    [InlineData(ChartShapeLabelPosition.TopLeft, 0, 0)] [InlineData(ChartShapeLabelPosition.Top, .5, 0)]
    [InlineData(ChartShapeLabelPosition.TopRight, 1, 0)] [InlineData(ChartShapeLabelPosition.Left, 0, .5)]
    [InlineData(ChartShapeLabelPosition.Center, .5, .5)] [InlineData(ChartShapeLabelPosition.Right, 1, .5)]
    [InlineData(ChartShapeLabelPosition.BottomLeft, 0, 1)] [InlineData(ChartShapeLabelPosition.Bottom, .5, 1)]
    [InlineData(ChartShapeLabelPosition.BottomRight, 1, 1)]
    public void Rectangle_label_positions_align_measured_text_inside_projected_bounds(ChartShapeLabelPosition position, double horizontal, double vertical)
    {
        var context = Context();
        var shape = new RectangleAnnotation { X1 = 180, Y1 = 130, X2 = 20, Y2 = 30, Label = "Shape", LabelPosition = position,
            LabelOffset = new Vector(7, -3), FontSize = 17, FontFamily = FontFamily.Default, Foreground = Brushes.Lime };
        var text = Format("Shape", context, 17, Brushes.Lime);
        var expected = new Point(20 + (160 - text.WidthIncludingTrailingWhitespace) * horizontal + 7, 30 + (100 - text.Height) * vertical - 3);
        EqualRect(TextBounds(text, expected), GlyphBounds(Render(shape, context), LabelInk));
        Assert.Empty(Geometry(Render(shape, context), Ink)); // A label does not require a shape fill or stroke.
    }

    [AvaloniaTheory]
    [InlineData(false, ChartShapeLabelPosition.Start)] [InlineData(false, ChartShapeLabelPosition.End)]
    [InlineData(true, ChartShapeLabelPosition.Start)] [InlineData(true, ChartShapeLabelPosition.End)]
    public void Arrow_start_and_end_labels_follow_original_endpoints_in_both_directions(bool reverse, ChartShapeLabelPosition position)
    {
        var context = Context();
        var shape = new ArrowAnnotation { X1 = reverse ? 180 : 20, Y1 = 30, X2 = reverse ? 20 : 180, Y2 = 130,
            ArrowHeads = ChartArrowHeads.None, Label = "Endpoint", LabelPosition = position, LabelOffset = new Vector(3, 5),
            Foreground = Brushes.Lime, Stroke = InkBrush, FontSize = 17 };
        var anchor = position == ChartShapeLabelPosition.Start ? new Point(shape.X1, shape.Y1) : new Point(shape.X2, shape.Y2);
        var text = Format("Endpoint", context, 17, Brushes.Lime);
        var expected = anchor + new Vector(3 - text.WidthIncludingTrailingWhitespace / 2, 5 - text.Height / 2);
        EqualRect(TextBounds(text, expected), GlyphBounds(Render(shape, context), LabelInk));
        Assert.Single(Geometry(Render(shape, context), Ink));
    }

    [AvaloniaFact]
    public void Ellipse_center_radius_labels_use_visual_bounds_and_inherited_font_style()
    {
        var ellipse = CenterEllipse(); ellipse.Label = "Ellipse"; ellipse.LabelPosition = ChartShapeLabelPosition.TopRight;
        ellipse.LabelOffset = new Vector(5, 9); ellipse.FontSize = 24; ellipse.Foreground = Brushes.Lime;
        var context = Context(); var text = Format("Ellipse", context, 24, Brushes.Lime);
        EqualRect(TextBounds(text, new Point(160 - text.WidthIncludingTrailingWhitespace + 5, 59)), GlyphBounds(Render(ellipse, context), LabelInk));
        ellipse.Fill = null;
        Assert.Empty(Geometry(Render(ellipse, context), Ink)); Assert.NotEmpty(Leaves(Render(ellipse, context)).OfType<GlyphRunDrawing>());
    }

    [AvaloniaFact]
    public void Center_radius_uses_named_logarithmic_inverted_and_secondary_axes_during_zoom()
    {
        var chart = Chart();
        var log = new LogarithmicAxis { Key = "log", Minimum = 1, Maximum = 1000, IsInversed = true };
        var pressure = new NumericAxis { Key = "pressure", Minimum = 0, Maximum = 1000 };
        chart.Axes.Add(log); chart.Axes.Add(pressure);
        chart.Series.Add(new ScatterSeries { XAxisKey = "log", YAxisKey = "pressure", Fill = Brushes.Transparent, Stroke = Brushes.Transparent,
            ItemsSource = new ChartPoint[] { new(1, 0), new(1000, 1000) } });
        var ellipse = new EllipseAnnotation { GeometryMode = ChartEllipseGeometryMode.CenterRadius, X = 50, Y = 500, RadiusX = 40, RadiusY = 200,
            XAxisKey = "log", YAxisKey = "pressure", Fill = InkBrush };
        chart.Annotations.Add(ellipse);
        using var host = new Host(chart);
        var plot = chart.PlotArea;
        var left = plot.Left + (1 - Math.Log10(90) / 3) * plot.Width;
        var right = plot.Left + (1 - Math.Log10(10) / 3) * plot.Width;
        var original = Assert.Single(Geometry(Render(chart), Ink)).Geometry!.Bounds;
        EqualRect(new Rect(left, plot.Top + plot.Height * .3, right - left, plot.Height * .4), original);
        // The nonlinear data center does not bisect projected center-radius bounds.
        var projectedCenter = plot.Left + (1 - Math.Log10(50) / 3) * plot.Width;
        Assert.True(Math.Abs(original.Center.X - projectedCenter) > 1);
        chart.Viewport = new ChartViewport(2, 8, 2, 8);
        log.ViewMinimum = 10; log.ViewMaximum = 100; pressure.ViewMinimum = 200; pressure.ViewMaximum = 800;
        host.Drain(); plot = chart.PlotArea;
        left = plot.Left + (2 - Math.Log10(90)) * plot.Width;
        EqualRect(new Rect(left, plot.Top + plot.Height / 6, plot.Right - left, plot.Height * 2 / 3),
            Assert.Single(Geometry(Render(chart), Ink)).Geometry!.Bounds);
        ellipse.X = 20; // The lower radius endpoint leaves the log domain.
        Assert.Empty(Geometry(Render(chart), Ink));
    }

    [AvaloniaFact]
    public void Pixel_radius_corner_radius_and_label_offsets_stay_in_DIP_units_across_zoom()
    {
        var chart = Chart();
        var ellipse = CenterEllipse(); ellipse.CoordinateUnit = ChartAnnotationCoordinateUnit.Pixel;
        ellipse.XAxisKey = "ignored pixel key"; ellipse.YAxisKey = "ignored pixel key";
        ellipse.Label = "Pixel"; ellipse.LabelPosition = ChartShapeLabelPosition.TopLeft; ellipse.LabelOffset = new Vector(7, 9); ellipse.Foreground = Brushes.Lime;
        var rectangle = new RectangleAnnotation { CoordinateUnit = ChartAnnotationCoordinateUnit.Pixel, X1 = 20, X2 = 120, Y1 = 30, Y2 = 90,
            CornerRadius = 12, Stroke = Brushes.Blue };
        chart.Annotations.Add(ellipse); chart.Annotations.Add(rectangle);
        using var host = new Host(chart);
        var before = Render(chart); var original = Assert.Single(Geometry(before, Ink)).Geometry!.Bounds;
        var originalLabel = GlyphBounds(before, LabelInk);
        EqualRect(new Rect(chart.PlotArea.Left + 40, chart.PlotArea.Top + 50, 120, 60), original);
        using var beforeRecording = new RectangleRecordingContext(); chart.Render(beforeRecording.Drawing);
        var beforeRounded = Assert.Single(beforeRecording.Rectangles, item => ColorIs(item.Pen?.Brush, Colors.Blue)).Shape;
        EqualRect(new Rect(chart.PlotArea.Left + 20, chart.PlotArea.Top + 30, 100, 60), beforeRounded.Rect);
        EqualRadii(12, beforeRounded);
        chart.Viewport = new ChartViewport(2, 8, 2, 8); host.Drain();
        var after = Render(chart);
        EqualRect(original, Assert.Single(Geometry(after, Ink)).Geometry!.Bounds);
        EqualRect(originalLabel, GlyphBounds(after, LabelInk));
        using var afterRecording = new RectangleRecordingContext(); chart.Render(afterRecording.Drawing);
        var afterRounded = Assert.Single(afterRecording.Rectangles, item => ColorIs(item.Pen?.Brush, Colors.Blue)).Shape;
        EqualRect(beforeRounded.Rect, afterRounded.Rect); EqualRadii(12, afterRounded);
    }

    [AvaloniaFact]
    public void Data_radius_and_labels_use_the_same_geometry_after_offscreen_export_transform()
    {
        var chart = Chart(); var ellipse = new EllipseAnnotation { GeometryMode = ChartEllipseGeometryMode.CenterRadius,
            X = 5, Y = 5, RadiusX = 2, RadiusY = 1, Fill = InkBrush, Label = "Data", LabelPosition = ChartShapeLabelPosition.Center,
            LabelOffset = new Vector(5, -3), Foreground = Brushes.Lime };
        chart.Annotations.Add(ellipse); using var host = new Host(chart);
        var native = Render(chart);
        var nativeGeometry = Assert.Single(Geometry(native, Ink)).Geometry!.Bounds;
        var nativeLabel = GlyphBounds(native, LabelInk);
        var layout = chart.GetExportLayout(new ChartExportOptions { PixelSize = new PixelSize(1200, 900), Dpi = 144 });
        var exported = new DrawingGroup(); using (var drawing = exported.Open()) chart.RenderExport(drawing, layout, Brushes.White);
        var exportGeometry = GeometryEvidence(exported, Matrix.Identity, Ink).Single();
        var exportLabel = GlyphBounds(exported, LabelInk);
        var scale = layout.Scale;
        Assert.Equal(nativeGeometry.Width * scale, exportGeometry.Width, 6); Assert.Equal(nativeGeometry.Height * scale, exportGeometry.Height, 6);
        Assert.Equal(nativeLabel.Width * scale, exportLabel.Width, 6); Assert.Equal(nativeLabel.Height * scale, exportLabel.Height, 6);
        Assert.Equal((nativeLabel.Center.X - nativeGeometry.Center.X) * scale, exportLabel.Center.X - exportGeometry.Center.X, 6);
        Assert.Equal((nativeLabel.Center.Y - nativeGeometry.Center.Y) * scale, exportLabel.Center.Y - exportGeometry.Center.Y, 6);
        EqualRect(nativeGeometry, Assert.Single(Geometry(Render(chart), Ink)).Geometry!.Bounds);
        EqualRect(nativeLabel, GlyphBounds(Render(chart), LabelInk));
    }

    private static EllipseAnnotation CenterEllipse() => new() { GeometryMode = ChartEllipseGeometryMode.CenterRadius, X = 100, Y = 80,
        RadiusX = 60, RadiusY = 30, Fill = InkBrush };
    private static CartesianChart Chart()
    {
        var chart = new CartesianChart { ShowLegend = false, XAxis = new NumericAxis { Minimum = 0, Maximum = 10 }, YAxis = new NumericAxis { Minimum = 0, Maximum = 10 } };
        chart.Series.Add(new LineSeries { Stroke = Brushes.Transparent, MarkerSize = 0, ItemsSource = new ChartPoint[] { new(0, 0), new(10, 10) } });
        return chart;
    }
    private static ChartAnnotationRenderContext Context() => new(new Rect(0, 0, 200, 200), new IdentityProjection(), new Typeface(FontFamily.Default), 12, InkBrush);
    private static DrawingGroup Render(ChartAnnotation annotation, ChartAnnotationRenderContext context)
    { var group = new DrawingGroup(); using (var drawing = group.Open()) annotation.Render(drawing, context); return group; }
    private static DrawingGroup Render(CartesianChart chart)
    { var group = new DrawingGroup(); using (var drawing = group.Open()) chart.Render(drawing); return group; }
    private static FormattedText Format(string text, ChartAnnotationRenderContext context, double size, IBrush foreground) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, context.Typeface, size, foreground);
    private static Rect TextBounds(FormattedText text, Point position)
    { var group = new DrawingGroup(); using (var drawing = group.Open()) drawing.DrawText(text, position); return GlyphBounds(group, LabelInk); }
    private static IEnumerable<Drawing> Leaves(Drawing drawing)
    {
        if (drawing is DrawingGroup group) foreach (var child in group.Children) foreach (var leaf in Leaves(child)) yield return leaf;
        else yield return drawing;
    }
    private static IEnumerable<GeometryDrawing> Geometry(Drawing drawing, Color color) => Leaves(drawing).OfType<GeometryDrawing>()
        .Where(item => ColorIs(item.Brush, color) || ColorIs(item.Pen?.Brush, color));
    private static bool ColorIs(IBrush? brush, Color color) => brush is ISolidColorBrush solid && solid.Color == color;
    private static Rect GlyphBounds(Drawing drawing, Color color)
    {
        var items = GlyphEvidence(drawing, Matrix.Identity, color).ToArray(); Assert.NotEmpty(items);
        return items.Aggregate((left, right) => left.Union(right));
    }
    private static IEnumerable<Rect> GlyphEvidence(Drawing drawing, Matrix toRoot, Color color)
    {
        if (drawing is DrawingGroup group)
        {
            var current = (group.Transform?.Value ?? Matrix.Identity) * toRoot;
            foreach (var child in group.Children) foreach (var bounds in GlyphEvidence(child, current, color)) yield return bounds;
        }
        else if (drawing is GlyphRunDrawing glyph && ColorIs(glyph.Foreground, color)) yield return glyph.GetBounds().TransformToAABB(toRoot);
    }
    private static IEnumerable<Rect> GeometryEvidence(Drawing drawing, Matrix toRoot, Color color)
    {
        if (drawing is DrawingGroup group)
        {
            var current = (group.Transform?.Value ?? Matrix.Identity) * toRoot;
            foreach (var child in group.Children) foreach (var bounds in GeometryEvidence(child, current, color)) yield return bounds;
        }
        else if (drawing is GeometryDrawing geometry && (ColorIs(geometry.Brush, color) || ColorIs(geometry.Pen?.Brush, color)))
            yield return geometry.Geometry!.Bounds.TransformToAABB(toRoot);
    }
    private static void EqualRect(Rect expected, Rect actual)
    { Assert.Equal(expected.X, actual.X, 6); Assert.Equal(expected.Y, actual.Y, 6); Assert.Equal(expected.Width, actual.Width, 6); Assert.Equal(expected.Height, actual.Height, 6); }
    private static void EqualRadii(double expected, RoundedRect actual)
    {
        var radius = new Vector(expected, expected);
        Assert.Equal(radius, actual.RadiiTopLeft); Assert.Equal(radius, actual.RadiiTopRight);
        Assert.Equal(radius, actual.RadiiBottomLeft); Assert.Equal(radius, actual.RadiiBottomRight);
    }

    // Avalonia 12's public DrawingContext cannot be implemented by an external
    // assembly: DrawBitmap is internal abstract. DrawingGroup also converts a
    // RoundedRect into backend PlatformGeometry, losing observable radii in the
    // headless stub. This test-only adapter constructs Avalonia's pinned 12.x
    // platform wrapper and records the runtime IDrawingContextImpl boundary.
    // Its reference assembly intentionally forbids external implementations,
    // so DispatchProxy implements the runtime contract instead of product code.
    // Product code does not use reflection or depend on this test adapter.
    private sealed class RectangleRecordingContext : IDisposable
    {
        private readonly RectangleRecorder _recorder;
        public DrawingContext Drawing { get; }
        public IReadOnlyList<RectangleRecord> Rectangles => _recorder.Rectangles;
        public RectangleRecordingContext()
        {
            var platform = DispatchProxy.Create<IDrawingContextImpl, RectangleRecorder>();
            _recorder = (RectangleRecorder)(object)platform;
            var wrapper = typeof(DrawingContext).Assembly.GetType("Avalonia.Media.PlatformDrawingContext", throwOnError: true)!;
            var constructor = wrapper.GetConstructor([typeof(IDrawingContextImpl), typeof(bool)])
                ?? throw new InvalidOperationException("The pinned Avalonia 12 platform drawing-context adapter changed.");
            Drawing = (DrawingContext)constructor.Invoke([platform, false]);
        }
        public void Dispose() => Drawing.Dispose();
    }
    public sealed record RectangleRecord(IBrush? Brush, IPen? Pen, RoundedRect Shape);
    public class RectangleRecorder : DispatchProxy
    {
        public List<RectangleRecord> Rectangles { get; } = [];
        public Matrix Transform { get; set; } = Matrix.Identity;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case "get_Transform": return Transform;
                case "set_Transform": Transform = (Matrix)args![0]!; return null;
                case "DrawRectangle":
                    Rectangles.Add(new((IBrush?)args![0], (IPen?)args[1], (RoundedRect)args[2]!)); return null;
                case "GetFeature": return null;
                case "Clear": case "DrawBitmap": case "DrawLine": case "DrawGeometry": case "DrawRegion":
                case "DrawEllipse": case "DrawGlyphRun": case "Dispose":
                case "PushClip": case "PopClip": case "PushLayer": case "PopLayer":
                case "PushOpacity": case "PopOpacity": case "PushOpacityMask": case "PopOpacityMask":
                case "PushGeometryClip": case "PopGeometryClip": case "PushRenderOptions": case "PopRenderOptions":
                case "PushTextOptions": case "PopTextOptions": return null;
                default: throw new NotSupportedException($"Unexpected drawing operation in the rectangle recorder: {targetMethod}.");
            }
        }
    }
    private sealed class IdentityProjection : IChartProjection
    {
        public Rect PlotArea => new(0, 0, 200, 200); public double YOrigin => 0;
        public double MapX(double value) => value; public double MapY(double value) => value;
        public Point Project(ChartPoint point) => new(point.X, point.Y); public ChartPoint Unproject(Point point) => new(point.X, point.Y);
    }
    private sealed class Host : IDisposable
    {
        public Window Window { get; }
        public Host(CartesianChart chart) { Window = new Window { Width = 760, Height = 440, Content = chart }; Window.Show(); Drain(); }
        public void Drain() { Window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
        public void Dispose() => Window.Close();
    }
}
