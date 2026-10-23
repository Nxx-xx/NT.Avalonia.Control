using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class PolarControlTests
{
    [AvaloniaFact]
    public void Layout_numeric_axis_inverse_and_radians_use_the_public_plot_contract()
    {
        var chart = Chart(new PolarScatterSeries { ItemsSource = new[] { new ChartPoint(0, 100), new(90, 50), new(360, 0) } });
        using var host = new Host(chart);
        Assert.Equal(new Rect(8, 8, 684, 504), chart.PlotArea); Assert.Equal(new Point(350, 260), chart.PolarCenter); Assert.Equal(252, chart.PolarRadius);
        var points = chart.GetPoints(); Near(new(350, 8), points[0].Position); Near(new(476, 260), points[1].Position); Near(chart.PolarCenter, points[2].Position);
        chart.AngularAxis.IsInversed = true; chart.RadialAxis.IsInversed = true;
        Near(chart.PolarCenter, chart.GetPoints()[0].Position); Near(new(224, 260), chart.GetPoints()[1].Position);
        chart.AngularAxis.IsInversed = chart.RadialAxis.IsInversed = false;
        chart.AngleUnit = ChartAngleUnit.Radians;
        chart.Series[0].ItemsSource = new[] { new ChartPoint(Math.PI / 2, 50) }; Near(new(476, 260), Assert.Single(chart.GetPoints()).Position);
    }

    [AvaloniaFact]
    public void Public_points_and_sectors_publish_the_same_resolved_brush_and_identity_used_by_selection()
    {
        var rows = new[] { new Row("a", "A", 0, 100, "#E51791"), new("b", "B", 90, 50, null) };
        var series = Numeric(rows); var chart = Chart(series); chart.ColorPalette = new[] { Brushes.Lime };
        using var host = new Host(chart);
        var points = chart.GetPoints(); Assert.Equal(Color.Parse("#E51791"), ColorOf(points[0].Fill)); Assert.Equal(Colors.Lime, ColorOf(points[1].Fill));
        Assert.True(chart.SelectPoint(points[0].Hit)); Assert.Same(rows[0], chart.SelectedPoint?.Item); Assert.Equal("a", chart.SelectedPoint?.StableId);
        Assert.Equal(points[0].Position, chart.SelectedPoint?.Position);
        var area = new PolarAreaSeries { ItemsSource = rows, CategoryPath = nameof(Row.Category), ValuePath = nameof(Row.Value), IdPath = nameof(Row.Id), ColorPath = nameof(Row.Color), SectorGap = 0 };
        chart.Series.Clear(); chart.Series.Add(area);
        Assert.Empty(chart.GetPoints()); var sector = chart.GetSectors()[0]; Assert.Equal(Color.Parse("#E51791"), ColorOf(sector.Fill));
        Assert.True(chart.SelectPoint(sector.Hit)); Assert.Equal(sector.Anchor, chart.SelectedPoint?.Position);
    }

    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void Sector_zero_holes_and_gaps_do_not_fall_back_to_marker_hits(int kind)
    {
        PolarSeries series = kind switch { 0 => new PolarAreaSeries(), 1 => new NightingaleRoseSeries(), _ => new RadialBarSeries() };
        series.ItemsSource = new[] { 0d, 100d }; series.InnerRadius = .4; series.SectorGap = 10;
        var chart = Chart(series); using var host = new Host(chart);
        Assert.Empty(chart.GetPoints()); Assert.False(chart.GetSectors()[0].IsVisible);
        Assert.Null(chart.HitTest(chart.PolarCenter)); Assert.False(chart.SelectPoint(chart.GetSectors()[0].Hit));
        var visible = chart.GetSectors()[1]; Assert.NotNull(chart.HitTest(visible.Anchor, 0));
        Assert.True(chart.SelectPoint(visible.Hit)); Assert.Equal(visible.Anchor, chart.SelectedPoint?.Position);
        chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Home });
        chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        Assert.Null(chart.SelectedPoint); // Home visits the single visible sector once, Space toggles it off.
    }

    [AvaloniaFact]
    public void Shared_categories_reorder_radar_sources_without_rewriting_original_indices()
    {
        var a = new RadarSeries { ItemsSource = new[] { new Row("a", "North", 0, 100, null), new("b", "East", 0, 50, null), new("c", "South", 0, 25, null), new("d", "West", 0, 75, null) }, CategoryPath = nameof(Row.Category), ValuePath = nameof(Row.Value), IdPath = nameof(Row.Id) };
        var b = new RadarSeries { ItemsSource = new[] { new Row("x", "South", 0, 50, null), new("y", "North", 0, 25, null), new("z", "West", 0, 75, null), new("w", "East", 0, 100, null) }, CategoryPath = nameof(Row.Category), ValuePath = nameof(Row.Value), IdPath = nameof(Row.Id) };
        var chart = Chart(a); chart.Series.Add(b); using var host = new Host(chart);
        var points = chart.GetPoints().Where(p => ReferenceEquals(p.Series, b)).ToArray();
        Assert.Equal(new[] { 0, 1, 2, 3 }, points.Select(p => p.Data.SourceIndex));
        Near(new(350, 386), points[0].Position); Near(new(350, 197), points[1].Position); Near(new(161, 260), points[2].Position); Near(new(602, 260), points[3].Position);
    }

    [AvaloniaFact]
    public void Series_and_axis_notifications_refresh_live_geometry_and_release_item_observers_on_detach()
    {
        var row = new MutableRow { Angle = 0, Value = 50 }; var source = new ObservableCollection<MutableRow> { row };
        var series = new PolarLineSeries { ItemsSource = source, AnglePath = nameof(MutableRow.Angle), RadiusPath = nameof(MutableRow.Value), IdPath = nameof(MutableRow.Id) };
        var chart = Chart(series); using var host = new Host(chart);
        var first = Assert.Single(chart.GetPoints()); chart.SelectPoint(first.Hit);
        row.Angle = 90; row.Value = 75; host.Drain(); var next = Assert.Single(chart.GetPoints()); Near(new(539, 260), next.Position);
        Assert.Equal(next.Position, chart.SelectedPoint?.Position); Assert.Same(row, chart.SelectedPoint?.Item);
        chart.RadialAxis.Maximum = 150; host.Drain(); Near(new(476, 260), Assert.Single(chart.GetPoints()).Position);
        Assert.True(row.Observers > 0); host.Window.Content = null; host.Drain(); Assert.Equal(0, row.Observers);
        host.Window.Content = chart; host.Drain(); Assert.True(row.Observers > 0);
        source.Clear(); host.Drain(); Assert.Empty(chart.GetPoints()); Assert.Null(chart.SelectedPoint);
    }

    [AvaloniaFact]
    public void Series_source_binding_observes_collection_and_releases_replaced_series()
    {
        var first = new PolarScatterSeries { ItemsSource = new[] { new ChartPoint(0, 50) } };
        var second = new PolarLineSeries { ItemsSource = new[] { new ChartPoint(90, 75) } };
        var source = new ObservableCollection<PolarSeries> { first }; var chart = Chart(); chart.SeriesSource = source;
        using var host = new Host(chart); Assert.Same(first, Assert.Single(chart.GetPoints()).Series);
        source.Clear(); source.Add(second); host.Drain(); Assert.Same(second, Assert.Single(chart.GetPoints()).Series);
        chart.SeriesSource = null; host.Drain(); Assert.Empty(chart.GetPoints());
    }

    [AvaloniaFact]
    public void Initial_selection_and_canceled_reentrant_selection_preserve_the_latest_business_identity()
    {
        var rows = new[] { new Row("a", "A", 0, 100, null), new("b", "B", 90, 100, null) };
        var series = Numeric(rows); var chart = Chart(series);
        chart.SelectedPoint = ChartPointHit.FromData(series, series.CreateSnapshot().Points[1], default);
        using var host = new Host(chart); Assert.Equal("b", chart.SelectedPoint?.StableId);
        chart.SelectionChanging += (_, e) => { if (e.CanCancel && Equals(e.NewSelection.FirstOrDefault()?.StableId, "a")) e.Cancel = true; };
        Assert.False(chart.SelectPoint(chart.GetPoints()[0].Hit)); Assert.Equal("b", chart.SelectedPoint?.StableId);
        chart.ClearSelection(); Assert.Empty(chart.SelectedPoints);
    }

    [AvaloniaFact]
    public void Replacing_items_by_stable_id_updates_selection_metadata_and_removal_clears_it()
    {
        var series = Numeric(new[] { new Row("a", "A", 0, 50, null) }); var chart = Chart(series); using var host = new Host(chart);
        chart.SelectPoint(chart.GetPoints()[0].Hit); var next = new Row("a", "A", 180, 75, null); series.ItemsSource = new[] { next }; host.Drain();
        Assert.Same(next, chart.SelectedPoint?.Item); Assert.Equal(180, chart.SelectedPoint?.Point.X); Assert.Equal(75, chart.SelectedPoint?.Point.Y);
        series.ItemsSource = Array.Empty<Row>(); host.Drain(); Assert.Null(chart.SelectedPoint);
    }

    [AvaloniaFact]
    public void Keyboard_browses_unique_source_items_and_escape_clears_selection()
    {
        var chart = Chart(new PolarScatterSeries { ItemsSource = new[] { new ChartPoint(0, 50), new(90, 75), new(180, 100) } }); using var host = new Host(chart);
        void Key(Key key) => chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });
        Key(Avalonia.Input.Key.Home); Key(Avalonia.Input.Key.Space); Assert.Equal(0, chart.SelectedPoint?.Index);
        Key(Avalonia.Input.Key.Right); Key(Avalonia.Input.Key.Enter); Assert.Equal(1, chart.SelectedPoint?.Index);
        Key(Avalonia.Input.Key.End); Key(Avalonia.Input.Key.Space); Assert.Equal(2, chart.SelectedPoint?.Index);
        Key(Avalonia.Input.Key.Escape); Assert.Empty(chart.SelectedPoints);
    }

    [AvaloniaFact]
    public void Formatter_selection_is_deferred_until_after_rendering()
    {
        var chart = Chart(new PolarScatterSeries { ItemsSource = new[] { new ChartPoint(90, 50) } }); using var host = new Host(chart);
        var rendering = false; var callbackDuringRender = false; chart.ShowDataLabels = true;
        chart.SelectionChanged += (_, _) => callbackDuringRender |= rendering;
        chart.LabelFormatter = hit => { chart.SelectPoint(hit); return "source"; };
        var group = new DrawingGroup(); rendering = true; using (var context = group.Open()) chart.Render(context); rendering = false; host.Drain();
        Assert.False(callbackDuringRender); Assert.NotNull(chart.SelectedPoint);
    }

    [AvaloniaFact]
    public void Hit_testing_obeys_closed_line_missing_gaps_and_topmost_series()
    {
        var line = new PolarLineSeries { ItemsSource = new[] { new ChartPoint(0, 100), new(90, 100), new(180, 100) }, ShowMarkers = false, IsClosed = true };
        var chart = Chart(line); using var host = new Host(chart);
        Assert.NotNull(chart.HitTest(new Point(350, 260), 0)); // Closing chord from south to north.
        line.IsClosed = false; Assert.Null(chart.HitTest(new Point(350, 260), 0));
        line.ItemsSource = new[] { new ChartPoint(0, 100), new(90, double.NaN), new(180, 100) }; line.IsClosed = true;
        Assert.Null(chart.HitTest(new Point(350, 260), 0));
        var upper = new PolarScatterSeries { ItemsSource = new[] { new ChartPoint(0, 100) } }; chart.Series.Add(upper);
        Assert.Same(upper, chart.HitTest(new Point(350, 8), 0)?.Series);
    }

    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void Fill_opacity_is_recorded_for_polygon_and_sector_families(int kind)
    {
        PolarSeries series = kind switch { 0 => new PolarFilledSeries { IsClosed = true }, 1 => new RadarSeries(), _ => new PolarAreaSeries() };
        series.ItemsSource = new[] { 100d, 75d, 50d, 25d }; series.Fill = Brushes.Magenta; series.StrokeThickness = 0; series.ShowMarkers = false; series.FillOpacity = .37;
        if (kind == 0) series.ItemsSource = new[] { new ChartPoint(0, 100), new(90, 75), new(180, 50), new(270, 25) };
        var chart = Chart(series); using var host = new Host(chart); var drawing = new DrawingGroup(); using (var context = drawing.Open()) chart.Render(context);
        var values = Paints(drawing).Where(p => p.Drawing is GeometryDrawing g && ColorOf(g.Brush) == Colors.Magenta).ToArray();
        Assert.NotEmpty(values); Assert.All(values, p => Assert.Equal(.37, p.Opacity, 10));
    }

    [AvaloniaTheory]
    [InlineData(96)] [InlineData(144)] [InlineData(192)]
    public void Fixed_pixel_export_layout_and_vector_geometry_preserve_source_coordinates(int dpi)
    {
        var chart = Chart(new PolarLineSeries { ItemsSource = new[] { new ChartPoint(0, 100), new(90, 50) }, Stroke = Brushes.Magenta, ShowMarkers = false }); using var host = new Host(chart);
        var before = chart.GetPoints().Select(p => p.Position).ToArray(); var layout = chart.GetExportLayout(new() { PixelSize = new(350, 260), Dpi = dpi });
        Assert.Equal(new PixelSize(350, 260), layout.Pixels); Assert.Equal(.5, layout.Scale); Assert.Equal(dpi, layout.Dpi);
        var group = new DrawingGroup(); using (var context = group.Open()) chart.RenderExport(context, layout, null);
        Assert.DoesNotContain(Flatten(group), d => d is ImageDrawing || d is GeometryDrawing { Brush: VisualBrush });
        Assert.Contains(Flatten(group), d => d is GeometryDrawing { Pen.Brush: ISolidColorBrush b } && b.Color == Colors.Magenta);
        Assert.Equal(before, chart.GetPoints().Select(p => p.Position));
    }

    [AvaloniaFact]
    public async Task Async_export_cancellation_and_option_failures_use_the_chart_sender_and_shared_pipeline()
    {
        var chart = Chart(new PolarScatterSeries { ItemsSource = new[] { new ChartPoint(0, 50) } }); using var host = new Host(chart);
        using var stream = new MemoryStream(); using var canceled = new CancellationTokenSource(); canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => chart.TryExportPngAsync(stream, cancellationToken: canceled.Token));
        ChartExportFailedEventArgs? failure = null; object? sender = null; chart.ExportFailed += (s, e) => { sender = s; failure = e; };
        Assert.False(await chart.TryExportJpegAsync(stream, new() { PixelSize = new(0, 1) })); Assert.Same(chart, sender); Assert.NotNull(failure); Assert.True(stream.CanWrite);
    }

    [AvaloniaFact]
    public void Axis_defaults_are_per_chart_and_null_binding_values_coerce_to_a_usable_axis()
    {
        var a = Chart(); var b = Chart();
        Assert.NotSame(a.AngularAxis, b.AngularAxis); Assert.NotSame(a.RadialAxis, b.RadialAxis);
        a.SetValue(PolarChart.AngularAxisProperty, null!); a.SetValue(PolarChart.RadialAxisProperty, null!);
        Assert.IsType<NumericAxis>(a.AngularAxis); Assert.IsType<NumericAxis>(a.RadialAxis);
        a.RadialAxis.Maximum = 250; Assert.Equal(100, b.RadialAxis.Maximum);
    }

    [AvaloniaFact]
    public void Extreme_finite_angular_domains_render_finite_axis_label_values()
    {
        var chart = Chart(new PolarLineSeries { ItemsSource = new[] { new ChartPoint(0, 50) } });
        chart.ShowAxisLabels = true; chart.AngularAxis.Minimum = -double.MaxValue; chart.AngularAxis.Maximum = double.MaxValue;
        using var host = new Host(chart); var drawing = new DrawingGroup(); using (var context = drawing.Open()) chart.Render(context);
        var point = Assert.Single(chart.GetPoints()); Assert.True(double.IsFinite(point.Position.X)); Assert.True(double.IsFinite(point.Position.Y));
        // The two opposite finite extrema must not turn their intermediate ticks into Infinity/NaN glyphs.
        var glyphs = Flatten(drawing).OfType<GlyphRunDrawing>().ToArray(); Assert.NotEmpty(glyphs);
        Assert.DoesNotContain(glyphs, g => g.GlyphRun is { } glyph && (glyph.Characters.Span.Contains('∞') || new string(glyph.Characters.Span).Contains("NaN", StringComparison.Ordinal)));
    }

    [AvaloniaFact]
    public void Unsupported_axis_transforms_and_breaks_fail_explicitly_instead_of_drawing_linear_approximations()
    {
        var chart = Chart(new PolarLineSeries { ItemsSource = new[] { new ChartPoint(0, 50) } });
        chart.RadialAxis = new LogarithmicAxis(); Assert.Throws<NotSupportedException>(() => chart.GetPoints());
        chart.RadialAxis = new NumericAxis { Minimum = 100, Maximum = 50 }; Assert.Throws<ArgumentOutOfRangeException>(() => chart.GetPoints());
    }

    private static PolarChart Chart(params PolarSeries[] series)
    {
        var chart = new PolarChart { Width = 700, Height = 520, Padding = new(0), ShowLegend = false, ShowAxisLabels = false, ShowGridLines = false, AxisBrush = null, RadialAxis = new NumericAxis { Minimum = 0, Maximum = 100 } };
        foreach (var item in series) chart.Series.Add(item); return chart;
    }
    private static PolarLineSeries Numeric(IEnumerable<Row> rows) => new() { ItemsSource = rows, AnglePath = nameof(Row.Angle), RadiusPath = nameof(Row.Value), IdPath = nameof(Row.Id), ColorPath = nameof(Row.Color) };
    private static Color? ColorOf(IBrush? brush) => (brush as ISolidColorBrush)?.Color;
    private static void Near(Point expected, Point actual) { Assert.Equal(expected.X, actual.X, 8); Assert.Equal(expected.Y, actual.Y, 8); }
    private static IEnumerable<Drawing> Flatten(Drawing drawing) { yield return drawing; if (drawing is DrawingGroup group) foreach (var child in group.Children) foreach (var item in Flatten(child)) yield return item; }
    private static IEnumerable<(Drawing Drawing, double Opacity)> Paints(Drawing drawing, double opacity = 1)
    {
        if (drawing is DrawingGroup group) { foreach (var child in group.Children) foreach (var item in Paints(child, opacity * group.Opacity)) yield return item; }
        else yield return (drawing, opacity);
    }
    private sealed record Row(string Id, string Category, double Angle, double Value, object? Color);
    private sealed class MutableRow : INotifyPropertyChanged
    {
        private double _angle, _value; private PropertyChangedEventHandler? _changed;
        public int Observers { get; private set; } public string Id => "stable";
        public double Angle { get => _angle; set { _angle = value; _changed?.Invoke(this, new(nameof(Angle))); } }
        public double Value { get => _value; set { _value = value; _changed?.Invoke(this, new(nameof(Value))); } }
        public event PropertyChangedEventHandler? PropertyChanged { add { Observers++; _changed += value; } remove { Observers--; _changed -= value; } }
    }
    private sealed class Host : IDisposable
    {
        public Window Window { get; }
        public Host(PolarChart chart) { Window = new Window { Width = 700, Height = 520, Content = chart }; Window.Show(); Drain(); }
        public void Drain() { Window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); }
        public void Dispose() { Window.Close(); Dispatcher.UIThread.RunJobs(); }
    }
}
