using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class C2VisualTests
{
    private static readonly Color Ink = Color.Parse("#D30DCA");
    private static readonly Color LabelInk = Color.Parse("#16DA61");

    [AvaloniaTheory]
    [InlineData(ChartMarkerShape.Circle)]
    [InlineData(ChartMarkerShape.Square)]
    [InlineData(ChartMarkerShape.Rectangle)]
    [InlineData(ChartMarkerShape.Diamond)]
    [InlineData(ChartMarkerShape.Triangle)]
    [InlineData(ChartMarkerShape.InvertedTriangle)]
    [InlineData(ChartMarkerShape.Cross)]
    [InlineData(ChartMarkerShape.Pentagon)]
    [InlineData(ChartMarkerShape.HorizontalLine)]
    [InlineData(ChartMarkerShape.VerticalLine)]
    public void Marker_shapes_render_finite_geometry_and_keep_original_hit_identity(ChartMarkerShape shape)
    {
        var series = new ScatterSeries { MarkerShape = shape, MarkerSize = 30, Stroke = new SolidColorBrush(Ink), ItemsSource = new ChartPoint[] { new(5, 5) } };
        var chart = Chart(series);
        using var host = new Host(chart);
        var drawings = Render(chart);
        Assert.Single(Colored(drawings, Ink));
        var hit = Assert.IsType<ChartPointHit>(chart.HitTestPoint(chart.PlotArea.Center));
        Assert.Equal(0, hit.Index);
        Assert.Same(series.CreateSnapshot().Points[0].StableId, hit.StableId);
    }

    [AvaloniaFact]
    public void Circle_keeps_its_original_hit_radius_while_square_and_cross_include_their_visible_corners()
    {
        var series = new ScatterSeries { MarkerSize = 40, StrokeThickness = 2, ItemsSource = new ChartPoint[] { new(5, 5) } };
        var chart = Chart(series);
        using var host = new Host(chart);
        var center = chart.PlotArea.Center;
        Assert.NotNull(chart.HitTestPoint(center + new Vector(20.9, 0), 0));
        Assert.Null(chart.HitTestPoint(center + new Vector(19, 19), 0));
        series.MarkerShape = ChartMarkerShape.Square;
        Assert.NotNull(chart.HitTestPoint(center + new Vector(19, 19), 0));
        series.MarkerShape = ChartMarkerShape.Cross;
        Assert.NotNull(chart.HitTestPoint(center + new Vector(19, 19), 0));
    }

    [AvaloniaFact]
    public void Bar_markers_are_opt_in_and_their_end_marker_can_be_hit_outside_the_bar()
    {
        var series = new BarSeries { MarkerShape = ChartMarkerShape.Square, MarkerSize = 40, Stroke = new SolidColorBrush(Ink), Fill = new SolidColorBrush(Ink),
            ItemsSource = new ChartPoint[] { new(5, 5) } };
        var chart = Chart(series);
        using var host = new Host(chart);
        Assert.False(series.ShowMarkers);
        Assert.Single(Colored(Render(chart), Ink));
        var markerCorner = chart.PlotArea.Center + new Vector(19, -19);
        Assert.Null(chart.HitTestPoint(markerCorner, 0));
        series.ShowMarkers = true;
        Assert.Equal(2, Colored(Render(chart), Ink).Count);
        var hit = Assert.IsType<ChartPointHit>(chart.HitTestPoint(markerCorner, 0));
        Assert.Equal(new ChartPoint(5, 5), hit.Point);
        series.ShowMarkers = false;
        Assert.Single(Colored(Render(chart), Ink));
    }

    [AvaloniaTheory]
    [InlineData("line")]
    [InlineData("bar")]
    [InlineData("scatter")]
    public void Labels_use_original_items_and_finite_visible_points_in_every_builtin_series(string kind)
    {
        var rows = new[] { new Row("a", 2, 3), new Row("missing", 4, double.NaN), new Row("b", 8, 7), new Row("outside", 20, 5) };
        ChartSeries series = kind switch { "bar" => new BarSeries(), "scatter" => new ScatterSeries(), _ => new LineSeries() };
        series.ItemsSource = rows;
        series.CategoryPath = nameof(Row.X);
        series.ValuePath = nameof(Row.Y);
        series.IdPath = nameof(Row.Id);
        series.ShowDataLabels = true;
        series.DataLabelBackground = new SolidColorBrush(LabelInk);
        var contexts = new List<ChartDataLabelContext>();
        series.DataLabelFormatter = value => { contexts.Add(value); return value.Data.SourceIndex.ToString(); };
        var chart = Chart(series);
        using var host = new Host(chart);
        contexts.Clear();
        Assert.Equal(2, Colored(Render(chart), LabelInk).Count);
        Assert.Equal(new[] { 0, 2 }, contexts.Select(value => value.Data.SourceIndex));
        Assert.Same(rows[0], contexts[0].Data.Item);
        Assert.Equal("a", contexts[0].Data.StableId);
        Assert.Equal("7", contexts[1].FormattedY);
    }

    [AvaloniaFact]
    public void Label_overlap_limit_numeric_format_and_invalid_format_have_observable_output()
    {
        var series = new ScatterSeries { ShowDataLabels = true, DataLabelPosition = ChartDataLabelPosition.Center, DataLabelBackground = new SolidColorBrush(LabelInk),
            DataLabelFormat = "F0", ItemsSource = new ChartPoint[] { new(5, 1.2345), new(5, 1.2345) } };
        var chart = Chart(series);
        using var host = new Host(chart);
        var hiddenOverlap = Colored(Render(chart), LabelInk);
        Assert.Single(hiddenOverlap);
        var narrow = hiddenOverlap[0].Geometry!.Bounds.Width;
        series.DataLabelFormat = "F6";
        Assert.True(Assert.Single(Colored(Render(chart), LabelInk)).Geometry!.Bounds.Width > narrow);
        series.DataLabelOverlap = ChartDataLabelOverlap.Allow;
        Assert.Equal(2, Colored(Render(chart), LabelInk).Count);
        series.DataLabelFormat = "Q";
        Assert.Equal(2, Colored(Render(chart), LabelInk).Count);
        series.ItemsSource = Enumerable.Range(0, 100).Select(index => new ChartPoint(index / 10d, 5)).ToArray();
        series.MaxDataLabels = 7;
        var seen = new List<int>();
        series.DataLabelFormatter = context => { seen.Add(context.Data.SourceIndex); return "X"; };
        Assert.InRange(Colored(Render(chart), LabelInk).Count, 1, 7);
        Assert.InRange(seen.Count, 1, 7);
        Assert.Equal(seen.Count, seen.Distinct().Count());
        series.MaxDataLabels = 0;
        Assert.Empty(Colored(Render(chart), LabelInk));
    }

    [AvaloniaFact]
    public void Auto_bar_labels_follow_the_projected_end_direction_and_stay_within_the_plot()
    {
        var series = new BarSeries { ShowDataLabels = true, DataLabelBackground = new SolidColorBrush(LabelInk), ItemsSource = new ChartPoint[] { new(2, 6), new(8, -6) } };
        var chart = Chart(series);
        chart.YAxis.Minimum = -10;
        using var host = new Host(chart);
        var backgrounds = Colored(Render(chart), LabelInk).Select(value => value.Geometry!.Bounds).OrderBy(rect => rect.X).ToArray();
        Assert.Equal(2, backgrounds.Length);
        var upper = chart.PlotArea.Bottom - .8 * chart.PlotArea.Height;
        var lower = chart.PlotArea.Bottom - .2 * chart.PlotArea.Height;
        Assert.True(backgrounds[0].Bottom < upper);
        Assert.True(backgrounds[1].Top > lower);
        Assert.All(backgrounds, rect => Assert.True(chart.PlotArea.Contains(rect)));
        chart.YAxis.IsInversed = true;
        Assert.Equal(2, Colored(Render(chart), LabelInk).Count);
    }

    [AvaloniaFact]
    public void Annotation_layers_are_ordered_around_series_and_hidden_or_invalid_annotations_are_skipped()
    {
        var series = new ScatterSeries { Stroke = new SolidColorBrush(Ink), ItemsSource = new ChartPoint[] { new(5, 5) } };
        var chart = Chart(series);
        var behind = new BandAnnotation { Orientation = Orientation.Vertical, FromValue = 1, ToValue = 3, Fill = Brushes.Red, Layer = ChartAnnotationLayer.BehindSeries };
        var above = new BandAnnotation { Orientation = Orientation.Vertical, FromValue = 4, ToValue = 6, Fill = Brushes.Blue };
        chart.Annotations.Add(behind);
        chart.Annotations.Add(above);
        chart.Annotations.Add(new LineAnnotation { Value = double.NaN, Stroke = Brushes.Lime });
        chart.Annotations.Add(new TextAnnotation { X = 2, Y = double.PositiveInfinity, Text = "invalid" });
        using var host = new Host(chart);
        var wanted = Flatten(Render(chart)).OfType<GeometryDrawing>().Select(value => (value.Brush as ISolidColorBrush)?.Color)
            .Where(value => value == Colors.Red || value == Ink || value == Colors.Blue).ToArray();
        Assert.Equal(new Color?[] { Colors.Red, Ink, Colors.Blue }, wanted);
        above.IsVisible = false;
        Assert.Empty(Colored(Render(chart), Colors.Blue));
        behind.Opacity = 0;
        Assert.Empty(Colored(Render(chart), Colors.Red));
    }

    [AvaloniaFact]
    public void Data_annotations_use_their_own_axis_and_pixel_annotations_remain_fixed_when_zooming()
    {
        var chart = Chart(new ScatterSeries { YAxisKey = "pressure", ItemsSource = new ChartPoint[] { new(5, 500) } });
        chart.Axes.Add(new NumericAxis { Key = "pressure", Minimum = 0, Maximum = 1000 });
        chart.Axes.Add(new NumericAxis { Key = "unused", Minimum = 0, Maximum = 1000 });
        Point dataPoint = default; Point pixelPoint = default; Point annotationOnlyPoint = default;
        var skipped = 0;
        chart.Annotations.Add(new CustomAnnotation { YAxisKey = "pressure", Renderer = (_, context) => dataPoint = context.Project(5, 500) });
        chart.Annotations.Add(new CustomAnnotation { CoordinateUnit = ChartAnnotationCoordinateUnit.Pixel, Renderer = (_, context) => pixelPoint = context.Project(10, 20) });
        chart.Annotations.Add(new CustomAnnotation { YAxisKey = "unused", Renderer = (_, context) => annotationOnlyPoint = context.Project(5, 500) });
        chart.Annotations.Add(new CustomAnnotation { XAxisKey = "pressure", Renderer = (_, _) => skipped++ });
        using var host = new Host(chart);
        Render(chart);
        Assert.Equal(chart.PlotArea.Center.X, dataPoint.X, 6);
        Assert.Equal(chart.PlotArea.Center.Y, dataPoint.Y, 6);
        Assert.Equal(chart.PlotArea.Center.X, annotationOnlyPoint.X, 6);
        Assert.Equal(chart.PlotArea.Center.Y, annotationOnlyPoint.Y, 6);
        Assert.Equal(chart.PlotArea.TopLeft + new Vector(10, 20), pixelPoint);
        Assert.Equal(0, skipped);
        var oldPixel = pixelPoint;
        chart.Viewport = new ChartViewport(2, 8, 2, 8);
        Render(chart);
        Assert.Equal(oldPixel, pixelPoint);
    }

    [AvaloniaFact]
    public void Annotation_sources_replace_inline_items_and_release_external_collection_subscriptions()
    {
        var chart = Chart(new ScatterSeries { ItemsSource = new ChartPoint[] { new(5, 5) } });
        chart.DataContext = "model";
        var inline = new TextAnnotation { Text = "inline" };
        chart.Annotations.Add(inline);
        var bound = new TextAnnotation { Text = "bound" };
        var source = new AnnotationCollection(bound);
        chart.AnnotationsSource = source;
        Assert.Null(((ILogical)inline).LogicalParent);
        Assert.Same(chart, ((ILogical)bound).LogicalParent);
        Assert.Equal("model", bound.DataContext);
        using var host = new Host(chart);
        Assert.Equal(1, source.Subscribers);
        var added = new LineAnnotation { Value = 4 };
        source.Add(added);
        Assert.Same(chart, ((ILogical)added).LogicalParent);
        source.Remove(added);
        Assert.Null(((ILogical)added).LogicalParent);
        host.Window.Content = null;
        Assert.Equal(0, source.Subscribers);
        var replacement = new TextAnnotation { Text = "replacement" };
        source[0] = replacement;
        host.Window.Content = chart;
        host.Drain();
        Assert.Equal(1, source.Subscribers);
        Assert.Same(chart, ((ILogical)replacement).LogicalParent);
        Assert.Null(((ILogical)bound).LogicalParent);
        chart.AnnotationsSource = Array.Empty<ChartAnnotation>();
        Assert.Equal(0, source.Subscribers);
        Assert.Null(((ILogical)replacement).LogicalParent);
        chart.AnnotationsSource = null;
        Assert.Same(chart, ((ILogical)inline).LogicalParent);
    }

    private static CartesianChart Chart(ChartSeries series)
    {
        var chart = new CartesianChart { ShowLegend = false, XAxis = new NumericAxis { Minimum = 0, Maximum = 10 }, YAxis = new NumericAxis { Minimum = 0, Maximum = 10 } };
        chart.Series.Add(series);
        return chart;
    }
    private static DrawingGroup Render(CartesianChart chart)
    {
        var group = new DrawingGroup();
        using (var drawing = group.Open()) chart.Render(drawing);
        Assert.All(Flatten(group).OfType<GeometryDrawing>(), value =>
        {
            var rect = value.Geometry?.Bounds ?? default;
            Assert.True(double.IsFinite(rect.X) && double.IsFinite(rect.Y) && double.IsFinite(rect.Width) && double.IsFinite(rect.Height));
        });
        return group;
    }
    private static List<GeometryDrawing> Colored(Drawing drawing, Color color) => Flatten(drawing).OfType<GeometryDrawing>()
        .Where(value => (value.Brush as ISolidColorBrush)?.Color == color || (value.Pen?.Brush as ISolidColorBrush)?.Color == color).ToList();
    private static IEnumerable<Drawing> Flatten(Drawing drawing)
    {
        yield return drawing;
        if (drawing is DrawingGroup group)
            foreach (var child in group.Children) foreach (var descendant in Flatten(child)) yield return descendant;
    }
    private sealed record Row(string Id, double X, double Y);
    private sealed class Host : IDisposable
    {
        public Window Window { get; }
        public Host(CartesianChart chart) { Window = new Window { Width = 700, Height = 420, Content = chart }; Window.Show(); Drain(); }
        public void Drain() { Window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
        public void Dispose() => Window.Close();
    }
    private sealed class AnnotationCollection(params ChartAnnotation[] items) : Collection<ChartAnnotation>(items.ToList()), INotifyCollectionChanged
    {
        private NotifyCollectionChangedEventHandler? _changed;
        public int Subscribers => _changed?.GetInvocationList().Length ?? 0;
        public event NotifyCollectionChangedEventHandler? CollectionChanged { add => _changed += value; remove => _changed -= value; }
        protected override void InsertItem(int index, ChartAnnotation item)
        { base.InsertItem(index, item); _changed?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index)); }
        protected override void RemoveItem(int index)
        { var item = this[index]; base.RemoveItem(index); _changed?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index)); }
        protected override void SetItem(int index, ChartAnnotation item)
        { var old = this[index]; base.SetItem(index, item); _changed?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, item, old, index)); }
    }
}
