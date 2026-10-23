using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class ChartShapeAnnotationTests
{
    private static readonly Color Ink = Color.Parse("#CB11A5");
    private static readonly IBrush InkBrush = new SolidColorBrush(Ink);

    [AvaloniaTheory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public void Rectangle_and_ellipse_normalize_projected_corners(bool ellipse, bool reversed)
    {
        ChartShapeAnnotation shape = ellipse ? new EllipseAnnotation() : new RectangleAnnotation();
        shape.X1 = reversed ? 12 : 2; shape.Y1 = reversed ? 9 : 3;
        shape.X2 = reversed ? 2 : 12; shape.Y2 = reversed ? 3 : 9; shape.Fill = InkBrush;
        var geometry = Assert.Single(InkDrawings(Render(shape, IdentityContext()))).Geometry!;
        EqualRect(new Rect(2, 3, 10, 6), geometry.Bounds);
    }

    [AvaloniaTheory]
    [InlineData(ChartArrowHeads.None, 0)] [InlineData(ChartArrowHeads.Start, 1)]
    [InlineData(ChartArrowHeads.End, 1)] [InlineData(ChartArrowHeads.Both, 2)]
    public void Arrow_heads_use_original_endpoints_and_only_the_stem_is_dashed(ChartArrowHeads heads, int count)
    {
        var arrow = new ArrowAnnotation { X1 = 20, Y1 = 30, X2 = 120, Y2 = 30, Stroke = InkBrush, Fill = InkBrush,
            ArrowHeads = heads, HeadLength = 10, HeadWidth = 6, DashStyle = DashStyle.Dash };
        var drawings = InkDrawings(Render(arrow, IdentityContext()));
        var stem = Assert.Single(drawings, drawing => drawing.Brush is null);
        Assert.NotNull(stem.Pen?.DashStyle);
        EqualRect(new Rect(20, 30, 100, 0), stem.Geometry!.Bounds);
        var triangles = drawings.Where(drawing => drawing.Brush is not null).ToArray();
        Assert.Equal(count, triangles.Length);
        Assert.All(triangles, drawing => Assert.Null(drawing.Pen?.DashStyle));
        if ((heads & ChartArrowHeads.Start) != 0) Assert.Contains(triangles, drawing => Near(drawing.Geometry!.Bounds, new Rect(20, 27, 10, 6)));
        if ((heads & ChartArrowHeads.End) != 0) Assert.Contains(triangles, drawing => Near(drawing.Geometry!.Bounds, new Rect(110, 27, 10, 6)));
    }

    [AvaloniaTheory]
    [InlineData(20, 30, 20, 130, 17, 120, 6, 10)]
    [InlineData(120, 30, 20, 30, 20, 27, 10, 6)]
    [InlineData(20, 20, 120, 120, 110.80761184457488, 110.80761184457488, 9.19238815542512, 9.19238815542512)]
    public void Arrow_head_geometry_follows_vertical_reversed_and_diagonal_directions(double x1, double y1, double x2, double y2, double x, double y, double width, double height)
    {
        var arrow = new ArrowAnnotation { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = InkBrush, HeadLength = 10, HeadWidth = 6 };
        EqualRect(new Rect(x, y, width, height), Assert.Single(InkDrawings(Render(arrow, IdentityContext())), drawing => drawing.Brush is not null).Geometry!.Bounds);
    }

    [AvaloniaFact]
    public void Short_arrows_bound_heads_without_changing_the_requested_sizes()
    {
        var arrow = new ArrowAnnotation { X1 = 20, Y1 = 30, X2 = 24, Y2 = 30, Stroke = InkBrush,
            ArrowHeads = ChartArrowHeads.Both, HeadLength = 100, HeadWidth = 60 };
        var heads = InkDrawings(Render(arrow, IdentityContext())).Where(drawing => drawing.Brush is not null).ToArray();
        Assert.Equal(2, heads.Length);
        Assert.All(heads, head => Assert.True(new Rect(20, 29.4, 4, 1.2).Inflate(.000001).Contains(head.Geometry!.Bounds)));
        Assert.Equal(100, arrow.HeadLength); Assert.Equal(60, arrow.HeadWidth);
        arrow.HeadWidth = 0;
        Assert.Single(InkDrawings(Render(arrow, IdentityContext())));
    }

    [AvaloniaTheory]
    [InlineData(-1)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(10000.1)]
    public void Arrow_head_dimensions_reject_invalid_or_unbounded_sizes(double value)
    {
        var arrow = new ArrowAnnotation();
        Assert.Throws<ArgumentException>(() => arrow.HeadLength = value);
        Assert.Throws<ArgumentException>(() => arrow.HeadWidth = value);
        arrow.HeadLength = 10000; arrow.HeadWidth = 10000;
        Assert.Equal(10000, arrow.HeadLength);
        Assert.Throws<ArgumentException>(() => arrow.ArrowHeads = (ChartArrowHeads)4);
    }

    [AvaloniaTheory]
    [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(double.NegativeInfinity)]
    public void Nonfinite_raw_endpoints_are_not_sent_to_the_drawing_backend(double value)
    {
        foreach (var shape in Shapes()) { shape.X2 = value; Assert.Empty(InkDrawings(Render(shape, IdentityContext()))); }
    }

    [AvaloniaFact]
    public void Degenerate_and_overflowing_external_projection_geometry_is_skipped()
    {
        foreach (var shape in Shapes())
        {
            shape.X2 = shape.X1; shape.Y2 = shape.Y1;
            Assert.Empty(InkDrawings(Render(shape, IdentityContext())));
            shape.X1 = -double.MaxValue; shape.X2 = double.MaxValue; shape.Y1 = -double.MaxValue; shape.Y2 = double.MaxValue;
            Assert.Empty(InkDrawings(Render(shape, IdentityContext())));
            shape.X1 = 1; shape.X2 = 2;
            Assert.Empty(InkDrawings(Render(shape, IdentityContext(new NonfiniteProjection()))));
        }
        Assert.True(EllipseAnnotation.TryGetEllipse(new Rect(8e307, 8e307, 8e307, 8e307), out var center, out var radius));
        Assert.InRange(center.X / 1.2e308, .99999999999999, 1.00000000000001);
        Assert.InRange(center.Y / 1.2e308, .99999999999999, 1.00000000000001);
        Assert.Equal(4e307, radius.X); Assert.Equal(4e307, radius.Y);
    }

    [AvaloniaFact]
    public void Data_shapes_follow_reversed_log_axes_while_pixel_shapes_keep_plot_relative_DIP_bounds()
    {
        var chart = Chart(); chart.XAxis = new LogarithmicAxis { Minimum = 1, Maximum = 1000, IsInversed = true };
        var data = new EllipseAnnotation { X1 = 10, X2 = 100, Y1 = 2, Y2 = 8, Fill = InkBrush };
        var pixel = new RectangleAnnotation { CoordinateUnit = ChartAnnotationCoordinateUnit.Pixel, X1 = 12, Y1 = 15, X2 = 52, Y2 = 45, Fill = Brushes.Lime };
        chart.Annotations.Add(data); chart.Annotations.Add(pixel);
        using var host = new Host(chart);
        var plot = chart.PlotArea;
        var drawings = Render(chart);
        EqualRect(new Rect(plot.Left + plot.Width / 3, plot.Top + plot.Height * .2, plot.Width / 3, plot.Height * .6), Assert.Single(InkDrawings(drawings)).Geometry!.Bounds);
        var original = Assert.Single(ColorDrawings(drawings, Colors.Lime)).Geometry!.Bounds;
        EqualRect(new Rect(plot.Left + 12, plot.Top + 15, 40, 30), original);
        chart.Viewport = new ChartViewport(10, 100, 0, 10);
        var zoomed = Render(chart);
        EqualRect(new Rect(chart.PlotArea.Left + 12, chart.PlotArea.Top + 15, 40, 30), Assert.Single(ColorDrawings(zoomed, Colors.Lime)).Geometry!.Bounds);
        Assert.True(Assert.Single(InkDrawings(zoomed)).Geometry!.Bounds.Width > plot.Width / 3);
        data.X1 = -1;
        Assert.Empty(InkDrawings(Render(chart)));
    }

    [AvaloniaFact]
    public void Shapes_use_the_selected_axis_and_skip_unknown_or_wrong_dimension_keys()
    {
        var chart = Chart();
        chart.Axes.Add(new NumericAxis { Key = "pressure", Minimum = 0, Maximum = 1000 });
        chart.Series.Add(new ScatterSeries { YAxisKey = "pressure", ItemsSource = new ChartPoint[] { new(1, 0), new(9, 1000) }, Stroke = Brushes.Transparent, Fill = Brushes.Transparent });
        var valid = new RectangleAnnotation { X1 = 2, X2 = 8, Y1 = 200, Y2 = 800, YAxisKey = "pressure", Fill = InkBrush };
        chart.Annotations.Add(valid);
        chart.Annotations.Add(new EllipseAnnotation { X1 = 2, X2 = 8, Y1 = 2, Y2 = 8, XAxisKey = "pressure", Fill = InkBrush });
        chart.Annotations.Add(new ArrowAnnotation { X1 = 2, X2 = 8, Y1 = 2, Y2 = 8, YAxisKey = "unknown", Stroke = InkBrush });
        using var host = new Host(chart);
        var plot = chart.PlotArea;
        EqualRect(new Rect(plot.Left + plot.Width * .2, plot.Top + plot.Height * .2, plot.Width * .6, plot.Height * .6), Assert.Single(InkDrawings(Render(chart))).Geometry!.Bounds);
    }

    [AvaloniaTheory]
    [InlineData("rectangle")] [InlineData("ellipse")] [InlineData("arrow")]
    public void Default_data_clipping_removes_both_axis_gaps_but_pixel_coordinates_ignore_data_breaks(string kind)
    {
        var chart = Chart();
        chart.XAxis.ScaleBreaks.Add(new ChartScaleBreak { Start = 4, End = 6, CompressionRatio = .2 });
        chart.YAxis.ScaleBreaks.Add(new ChartScaleBreak { Start = 4, End = 6, CompressionRatio = .2 });
        var shape = Shape(kind); shape.X1 = 2; shape.Y1 = 2; shape.X2 = 8; shape.Y2 = 8;
        chart.Annotations.Add(shape);
        using var host = new Host(chart);
        var plot = chart.PlotArea;
        var clips = InkClips(Render(chart));
        Assert.NotEmpty(clips);
        Assert.All(clips, chain => Assert.Contains(chain, clip => clip is CombinedGeometry));
        var before = new Point(plot.Left + plot.Width * 3 / 8.4, plot.Bottom - plot.Height * 3 / 8.4);
        var after = new Point(plot.Left + plot.Width * 5.4 / 8.4, plot.Bottom - plot.Height * 5.4 / 8.4);
        Assert.All(clips, chain => { Assert.True(chain.All(clip => ClipContains(clip, before))); Assert.True(chain.All(clip => ClipContains(clip, after))); Assert.False(chain.All(clip => ClipContains(clip, plot.Center))); });
        shape.CoordinateUnit = ChartAnnotationCoordinateUnit.Pixel;
        shape.X1 = 10; shape.Y1 = 10; shape.X2 = 80; shape.Y2 = 80;
        var pixelClips = InkClips(Render(chart));
        Assert.NotEmpty(pixelClips);
        Assert.All(pixelClips, chain => Assert.DoesNotContain(chain, clip => clip is CombinedGeometry));
    }

    [AvaloniaFact]
    public void Compiled_shape_bindings_and_resources_update_and_source_replacement_preserves_ownership()
    {
        var view = new ShapeAnnotationsConsumerView(); var model = new ShapeAnnotationModel(); view.DataContext = model;
        var chart = view.FindControl<CartesianChart>("ShapeChart")!;
        using var host = new Host(view);
        var shape = Assert.IsType<RectangleAnnotation>(Assert.Single(chart.Annotations));
        Assert.Equal(2, shape.X1); Assert.Equal(8, shape.X2); Assert.Same(model, shape.DataContext);
        var old = Assert.Single(InkDrawings(Render(chart))).Geometry!.Bounds;
        model.Left = 4; host.Drain();
        Assert.Equal(4, shape.X1); Assert.True(Assert.Single(InkDrawings(Render(chart))).Geometry!.Bounds.Width < old.Width);
        view.Resources["ShapeInk"] = Brushes.Lime; host.Drain();
        Assert.Single(ColorDrawings(Render(chart), Colors.Lime));
        var arrow = new ArrowAnnotation { X1 = 2, Y1 = 2, X2 = 8, Y2 = 8, Stroke = InkBrush };
        model.Items = new ObservableCollection<ChartAnnotation> { arrow }; host.Drain();
        Assert.Null(((ILogical)shape).LogicalParent); Assert.Same(chart, ((ILogical)arrow).LogicalParent); Assert.Same(model, arrow.DataContext);
        model.Items.Clear(); host.Drain(); Assert.Empty(InkDrawings(Render(chart))); Assert.Null(((ILogical)arrow).LogicalParent);
        model.Items = null; host.Drain(); Assert.Same(chart, ((ILogical)shape).LogicalParent);
    }

    private static ChartShapeAnnotation[] Shapes() => [Shape("rectangle"), Shape("ellipse"), Shape("arrow")];
    private static ChartShapeAnnotation Shape(string kind) => kind switch
    {
        "ellipse" => new EllipseAnnotation { X1 = 2, Y1 = 2, X2 = 8, Y2 = 8, Fill = InkBrush },
        "arrow" => new ArrowAnnotation { X1 = 2, Y1 = 2, X2 = 8, Y2 = 8, Stroke = InkBrush },
        _ => new RectangleAnnotation { X1 = 2, Y1 = 2, X2 = 8, Y2 = 8, Fill = InkBrush },
    };
    private static CartesianChart Chart()
    {
        var chart = new CartesianChart { ShowLegend = false, XAxis = new NumericAxis { Minimum = 0, Maximum = 10 }, YAxis = new NumericAxis { Minimum = 0, Maximum = 10 } };
        chart.Series.Add(new LineSeries { Stroke = Brushes.Transparent, MarkerSize = 0, ItemsSource = new ChartPoint[] { new(0, 0), new(10, 10) } }); return chart;
    }
    private static ChartAnnotationRenderContext IdentityContext(IChartProjection? projection = null) => new(new Rect(0, 0, 200, 200), projection ?? new IdentityProjection(), new Typeface(FontFamily.Default), 12, InkBrush);
    private static DrawingGroup Render(ChartAnnotation annotation, ChartAnnotationRenderContext context) { var group = new DrawingGroup(); using (var drawing = group.Open()) annotation.Render(drawing, context); return group; }
    private static DrawingGroup Render(CartesianChart chart) { var group = new DrawingGroup(); using (var drawing = group.Open()) chart.Render(drawing); return group; }
    private static List<GeometryDrawing> InkDrawings(Drawing drawing) => ColorDrawings(drawing, Ink);
    private static List<GeometryDrawing> ColorDrawings(Drawing drawing, Color color) => Flatten(drawing).OfType<GeometryDrawing>().Where(item => IsColor(item.Brush, color) || IsColor(item.Pen?.Brush, color)).ToList();
    private static bool IsColor(IBrush? brush, Color color) => brush is ISolidColorBrush solid && solid.Color == color;
    private static IEnumerable<Drawing> Flatten(Drawing drawing) { yield return drawing; if (drawing is DrawingGroup group) foreach (var child in group.Children) foreach (var item in Flatten(child)) yield return item; }
    private static bool Near(Rect actual, Rect expected) => Math.Abs(actual.X - expected.X) < .000001 && Math.Abs(actual.Y - expected.Y) < .000001 && Math.Abs(actual.Width - expected.Width) < .000001 && Math.Abs(actual.Height - expected.Height) < .000001;
    private static void EqualRect(Rect expected, Rect actual) => Assert.True(Near(actual, expected), $"Expected {expected}, actual {actual}");
    private static List<Geometry[]> InkClips(Drawing drawing)
    {
        var result = new List<Geometry[]>(); Walk(drawing, []); return result;
        void Walk(Drawing item, Geometry[] clips)
        {
            if (item is DrawingGroup group) { var next = group.ClipGeometry is { } clip ? clips.Append(clip).ToArray() : clips; foreach (var child in group.Children) Walk(child, next); }
            else if (item is GeometryDrawing geometry && (IsColor(geometry.Brush, Ink) || IsColor(geometry.Pen?.Brush, Ink))) result.Add(clips);
        }
    }
    private static bool ClipContains(Geometry geometry, Point point)
    {
        if (geometry.Transform is { } transform) point = transform.Value.Invert().Transform(point);
        return geometry switch
        {
            RectangleGeometry rectangle => rectangle.Rect.Contains(point),
            CombinedGeometry combined => combined.GeometryCombineMode switch
            {
                GeometryCombineMode.Exclude => Contains(combined.Geometry1) && !Contains(combined.Geometry2),
                GeometryCombineMode.Intersect => Contains(combined.Geometry1) && Contains(combined.Geometry2),
                GeometryCombineMode.Union => Contains(combined.Geometry1) || Contains(combined.Geometry2),
                _ => throw new InvalidOperationException("Unsupported clip combination in shape fixture."),
            },
            _ => throw new InvalidOperationException($"Unexpected clip {geometry.GetType().Name}."),
        };
        bool Contains(Geometry? child) => child is not null && ClipContains(child, point);
    }
    private class IdentityProjection : IChartProjection
    {
        public Rect PlotArea => new(0, 0, 200, 200); public double YOrigin => 0;
        public virtual double MapX(double value) => value; public double MapY(double value) => value;
        public Point Project(ChartPoint point) => new(MapX(point.X), MapY(point.Y)); public ChartPoint Unproject(Point point) => new(point.X, point.Y);
    }
    private sealed class NonfiniteProjection : IdentityProjection { public override double MapX(double value) => double.NaN; }
    private sealed class Host : IDisposable
    {
        public Window Window { get; }
        public Host(Control content) { Window = new Window { Width = 760, Height = 440, Content = content }; Window.Show(); Drain(); }
        public void Drain() { Window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
        public void Dispose() => Window.Close();
    }
}

public sealed partial class ShapeAnnotationsConsumerView : UserControl { public ShapeAnnotationsConsumerView() => AvaloniaXamlLoader.Load(this); }
public sealed class ShapeAnnotationModel : INotifyPropertyChanged
{
    private double _left = 2; private ObservableCollection<ChartAnnotation>? _items;
    public double Left { get => _left; set { _left = value; PropertyChanged?.Invoke(this, new(nameof(Left))); } }
    public ObservableCollection<ChartAnnotation>? Items { get => _items; set { _items = value; PropertyChanged?.Invoke(this, new(nameof(Items))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}
