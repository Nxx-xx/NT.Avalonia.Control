using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class MapOverlayTests
{
    private static readonly MapCoordinate[] Outer = [new(0, 0), new(10, 0), new(10, 10), new(0, 10), new(0, 0)];
    private static readonly MapCoordinate[] Hole = [new(4, 4), new(4, 6), new(6, 6), new(6, 4), new(4, 4)];

    [Fact]
    public void Marker_projection_keeps_negative_business_values_and_rejects_duplicate_ids()
    {
        var source = new object(); var row = new MapObservation("a", new(30, 45), -7, source); var layout = ChartMaps.Markers([row], MapProjection.Equirectangular, TestContext.Current.CancellationToken);
        var point = Assert.Single(layout.Points); Assert.Same(source, point.Entry.Source); Assert.Equal(-7, point.Entry.Value); Near(7d / 12, point.Position.X); Near(.75, point.Position.Y);
        Assert.Throws<ArgumentException>(() => ChartMaps.Markers([row, row], MapProjection.Identity, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartMaps.Markers([], (MapProjection)99, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Connections_split_date_line_without_fabricating_original_rows_and_keep_identity_plane_unsplit()
    {
        var source = new object(); var row = new MapConnection("route", new(170, 10), new(-170, 20), 9, source);
        var projected = ChartMaps.Connections([row], MapProjection.Equirectangular, TestContext.Current.CancellationToken); var route = Assert.Single(projected.Connections);
        Assert.Same(source, route.Entry.Source); Assert.Equal(0, route.Entry.SourceIndex); Assert.Equal(2, route.Segments.Count); Near(1, route.Segments[0].End.X); Near(0, route.Segments[1].Start.X); Near(7d / 12, route.Segments[0].End.Y);
        Assert.Single(ChartMaps.Connections([row], MapProjection.Identity, TestContext.Current.CancellationToken).Connections[0].Segments);
        var same = ChartMaps.Connections([new("meridian", new(180, -5), new(-180, 5))], MapProjection.Equirectangular, TestContext.Current.CancellationToken); Assert.All(same.Connections[0].Segments, s => Assert.True(double.IsFinite(s.Start.X + s.Start.Y + s.End.X + s.End.Y)));
        Assert.Throws<ArgumentException>(() => ChartMaps.Connections([row with { Value = -1 }], MapProjection.Identity, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Pie_locations_normalize_independently_preserve_slice_ordinal_and_reject_negative_values()
    {
        var source = new object(); var row = new MapPie("p", new(2, 3), [new(1, "a"), new(0, "zero"), new(3, "b")], source);
        var layout = ChartMaps.Pies([row], MapProjection.Identity, TestContext.Current.CancellationToken); Assert.Equal(2, layout.Slices.Count);
        Near(-90, layout.Slices[0].StartAngle); Near(90, layout.Slices[0].SweepAngle); Near(0, layout.Slices[1].StartAngle); Near(270, layout.Slices[1].SweepAngle); Assert.Equal(2, layout.Slices[1].SliceIndex); Assert.Same(source, layout.Slices[0].Entry.Source);
        Assert.Empty(ChartMaps.Pies([row with { Slices = [new(0)] }], MapProjection.Identity, TestContext.Current.CancellationToken).Slices);
        Assert.Throws<ArgumentException>(() => ChartMaps.Pies([row with { Slices = [new(-1)] }], MapProjection.Identity, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartMaps.Pies([row with { Slices = Enumerable.Repeat(new MapPieSlice(1), ChartMaps.MaximumPieSlices + 1).ToArray() }], MapProjection.Identity, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Five_vector_kinds_retain_original_source_and_polygon_holes_with_strict_validation()
    {
        var source = new object(); MapVector[] rows = [new MapLineVector("line", new(1, 1), new(2, 2), source), new MapPolylineVector("polyline", [new(2, 1), new(3, 1), new(4, 2)]),
            new MapPolygonVector("polygon", new(Outer, [Hole])), new MapCircleVector("circle", new(8, 8), 10), new MapArcVector("arc", new(2, 8), 12, 0, 90)];
        var layout = ChartMaps.Vectors(rows, MapProjection.Identity, cancellationToken: TestContext.Current.CancellationToken); Assert.Equal(5, layout.Vectors.Count); Assert.Same(source, layout.Vectors[0].Entry.Source);
        Assert.Equal(2, layout.Vectors[1].Segments.Count); var polygon = Assert.Single(layout.Vectors[2].Polygons); Assert.True(ChartMaps.Contains(polygon, new(2, 2))); Assert.False(ChartMaps.Contains(polygon, new(5, 5)));
        Assert.Equal(new ChartPoint(8, 8), layout.Vectors[3].Center); Assert.Equal(new ChartPoint(2, 8), layout.Vectors[4].Center);
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartMaps.Vectors([new MapCircleVector("bad", new(0, 0), 0)], MapProjection.Identity, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => ChartMaps.Vectors([new MapArcVector("bad", new(0, 0), 10, 0, 0)], MapProjection.Identity, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Overlay_algorithms_cancel_before_enumeration_and_apply_typed_vector_budget()
    {
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.Throws<OperationCanceledException>(() => ChartMaps.Markers([], MapProjection.Identity, cancel.Token));
        Assert.Throws<OperationCanceledException>(() => ChartMaps.Connections([], MapProjection.Identity, cancel.Token));
        Assert.Throws<OperationCanceledException>(() => ChartMaps.Pies([], MapProjection.Identity, cancel.Token));
        Assert.Throws<OperationCanceledException>(() => ChartMaps.Vectors([], MapProjection.Identity, cancellationToken: cancel.Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartMaps.Vectors(Enumerable.Range(0, ChartMaps.MaximumVectorCount + 1).Select(i => new MapLineVector(i, new(0, 0), new(1, 1))), MapProjection.Identity, cancellationToken: TestContext.Current.CancellationToken));
    }

    [AvaloniaTheory]
    [InlineData(MapMarkerType.Circle)] [InlineData(MapMarkerType.Diamond)] [InlineData(MapMarkerType.Triangle)] [InlineData(MapMarkerType.Rectangle)] [InlineData(MapMarkerType.Pin)]
    public void Every_marker_shape_has_original_business_hits_and_inherits_data_context(MapMarkerType type)
    {
        var point = new PointRow("p", 3, 3, -5); var layer = new MarkerLayer { ItemsSource = new[] { point }, MarkerType = type, MarkerSize = 20, Fill = Brushes.Red };
        using var map = NewMap(); map.DataContext = point; map.Layers.Add(layer); using var host = new Host(map);
        Assert.Same(point, layer.DataContext); var mark = Assert.Single(map.GetMarks()); Assert.Equal(MapElementKind.Marker, ((MapGlyphKey)mark.Key!).Kind); Assert.Same(point, mark.Members[0].Item); Assert.Equal(-5, mark.Value); Assert.Same(point, map.HitTest(mark.Position)!.Members[0].Item);
        Assert.True(map.SelectItem(layer, "p")); map.Zoom = 1.25; _ = map.GetMarks(); Assert.Same(point, map.SelectedPoint!.Members[0].Item); Assert.NotEmpty(Record(map).Children);
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Straight_and_curved_connection_paths_return_original_routes_and_leave_blank_space_unhittable(bool curved)
    {
        var source = new object(); var row = new MapConnection("r", new(2, 2), new(8, 8), 4, source); var layer = new LineLayer { ItemsSource = new[] { row }, IsCurved = curved, ShowEndpoints = false, MinimumThickness = 4, MaximumThickness = 4 };
        using var map = NewMap(); map.Layers.Add(layer); using var host = new Host(map); var mark = Assert.Single(map.GetMarks()); Assert.Same(source, mark.Members[0].Item); Assert.Same(source, map.HitTest(mark.Position)!.Members[0].Item);
        Assert.Null(map.HitTest(new(30, 30))); Assert.True(map.SelectItem(layer, "r")); Assert.NotEmpty(Record(map).Children);
        layer.MinimumThickness = 10; Assert.NotNull(map.DataError); layer.MaximumThickness = 12; Assert.Null(map.DataError);
    }

    [AvaloniaFact]
    public void Vector_layer_draws_and_hits_five_original_shapes_including_a_polygon_hole()
    {
        var rows = VectorRows(); var layer = new VectorLayer { ItemsSource = rows, Fill = Brushes.Green, Stroke = Brushes.Blue }; using var map = NewMap(); map.Layers.Add(layer); using var host = new Host(map);
        Assert.Equal(5, map.GetMarks().Count); Assert.Null(map.DataError);
        foreach (var mark in map.GetMarks()) { Assert.Equal(MapElementKind.Vector, ((MapGlyphKey)mark.Key!).Kind); Assert.Same(mark.Members[0].Item, map.HitTest(mark.Position)!.Members[0].Item); }
        Assert.Null(map.HitTest(Project(map, new(6, 2)))); Assert.NotEmpty(Record(map).Children); Assert.Equal(5, map.LayerSnapshots[0].Vectors!.Vectors.Count);
    }

    [AvaloniaFact]
    public void Pie_layer_returns_original_locations_for_each_nonzero_slice_and_nested_collection_updates()
    {
        var slices = new ObservableCollection<MapPieSlice> { new(1, "a"), new(3, "b") }; var row = new PieRow("p", 5, 5, slices); var layer = new PieLayer { ItemsSource = new[] { row }, Radius = 30 };
        using var map = NewMap(); map.Layers.Add(layer); using var host = new Host(map); Assert.Equal(2, map.GetMarks().Count);
        foreach (var mark in map.GetMarks()) { Assert.Same(row, mark.Members[0].Item); Assert.Equal("p", mark.Members[0].StableId); Assert.Same(row, map.HitTest(mark.Position)!.Members[0].Item); }
        Assert.True(map.SelectItem(layer, "p")); slices.Add(new(2, "c")); host.Drain(); Assert.Equal(3, map.GetMarks().Count); Assert.Same(row, map.SelectedPoint!.Members[0].Item);
        slices[0] = new(-1); host.Drain(); Assert.NotNull(map.DataError); Assert.Empty(map.GetMarks()); slices[0] = new(1); host.Drain(); Assert.Null(map.DataError); Assert.Equal(3, map.GetMarks().Count);
    }

    [AvaloniaFact]
    public void Categorical_regions_keep_real_labels_no_fabricated_member_metric_and_explicit_business_colors()
    {
        var row = new RegionRow("a", "Available"); var layer = new ShapeLayer { ItemsSource = new[] { row }, RegionPath = "Id", ColorMode = MapColorMode.Categorical, CategoryPath = "State", CategoryColors = new Dictionary<object, IBrush> { ["Available"] = Brushes.Green, ["Booked"] = Brushes.Red } };
        using var map = NewMap(); map.Layers.Add(layer); using var host = new Host(map); var mark = Assert.Single(map.GetMarks()); Assert.Same(row, mark.Members[0].Item); Assert.Null(mark.Members[0].Value); Assert.Contains("Available", mark.TooltipText!); Assert.Equal(Colors.Green, ((ISolidColorBrush)mark.Fill!).Color);
        row.State = "Booked"; host.Drain(); var changed = Assert.Single(map.GetMarks()); Assert.Contains("Booked", changed.TooltipText!); Assert.Equal(Colors.Red, ((ISolidColorBrush)changed.Fill!).Color); Assert.Null(changed.Members[0].Value);
        map.ShowLegend = true; Assert.NotEmpty(Record(map).Children);
    }

    [AvaloniaTheory]
    [InlineData(double.Epsilon)] [InlineData(-double.Epsilon)] [InlineData(double.MaxValue)] [InlineData(-double.MaxValue)]
    public void Single_extreme_planar_point_has_real_fit_span_visible_size_and_original_hit(double coordinate)
    {
        var row = new MapObservation("tiny", new(coordinate, coordinate), 1); using var map = new SeatMap { ShowLabels = false, ShowLegend = false }; map.Layers.Add(new BubbleLayer { ItemsSource = new[] { row }, MaximumRadius = .1 }); using var host = new Host(map);
        var mark = Assert.Single(map.GetMarks()); Assert.Null(map.DataError); Assert.True(map.MapViewport!.Bounds.MinX < map.MapViewport.Bounds.MaxX); Assert.True(map.MapViewport.FittedArea.Width > 0); Assert.True(mark.Bounds.Width > 0); Assert.Same(row, map.HitTest(mark.Position)!.Members[0].Item);
    }

    [AvaloniaFact]
    public void Source_replacement_and_layer_removal_release_retired_subscriptions_before_another_capture()
    {
        var old = new PointRow("old", 2, 2, 1); var next = new PointRow("next", 3, 3, 2); var layer = new MarkerLayer { ItemsSource = new[] { old } };
        using var map = NewMap(); map.Layers.Add(layer); using var host = new Host(map); Assert.True(old.Subscribers > 0);
        layer.ItemsSource = new[] { next }; Assert.Equal(0, old.Subscribers); host.Drain(); Assert.True(next.Subscribers > 0);
        map.Layers.Remove(layer); Assert.Equal(0, next.Subscribers);
    }

    [AvaloniaFact]
    public void Seat_multiple_selection_supports_pointer_keyboard_external_lists_and_original_replacements()
    {
        var a = new RegionRow("a", "Available"); var b = new RegionRow("b", "Available"); var rows = new ObservableCollection<RegionRow> { a, b };
        using var map = NewMap(); map.IsMultiSelectionEnabled = true; map.Layers.Add(new ShapeLayer { ItemsSource = rows, RegionPath = "Id", ColorMode = MapColorMode.Categorical, CategoryPath = "State" }); using var host = new Host(map);
        Assert.True(map.ToggleSeat("a")); Assert.True(map.ToggleSeat(b)); Assert.Equal(2, map.SelectedItems.Count); Assert.Equal(-1, map.SelectedIndex);
        Assert.True(map.ToggleSeat("a")); Assert.Same(b, Assert.Single(map.SelectedItems)); map.SelectedItems = new object[] { a }; _ = map.GetMarks(); Assert.Same(a, Assert.Single(map.SelectedItems));
        var replacement = new RegionRow("a", "Booked"); rows[0] = replacement; host.Drain(); Assert.Same(replacement, Assert.Single(map.SelectedItems)); rows.Move(0, 1); host.Drain(); Assert.Same(replacement, Assert.Single(map.SelectedItems));
        map.ClearSeatSelection(); var mark = map.GetMarks().Single(m => ReferenceEquals(m.Members[0].Item, b)); host.Click(mark.Position); Assert.Same(b, Assert.Single(map.SelectedItems));
        map.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.End }); map.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space }); Assert.Empty(map.SelectedItems);
        map.ToggleSeat("a"); rows.Remove(replacement); host.Drain(); Assert.Empty(map.SelectedItems);
    }

    [AvaloniaFact]
    public async Task New_map_layers_keep_export_failure_and_cancellation_contracts()
    {
        using var map = NewMap(); map.Layers.Add(new MarkerLayer { ItemsSource = new[] { new MapObservation("a", new(3, 3)) } }); map.Layers.Add(new PieLayer { ItemsSource = new[] { new MapPie("p", new(6, 6), [new(1), new(2)]) } }); using var host = new Host(map);
        using var stream = new MemoryStream(); var errors = 0; map.ExportFailed += (_, _) => errors++; Assert.False(map.TryExportPng(stream, new ChartExportOptions { Dpi = 0 })); Assert.Equal(1, errors);
        using var cancel = new CancellationTokenSource(); cancel.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => map.TryExportPngAsync(stream, cancellationToken: cancel.Token)); Assert.Equal(1, errors);
    }

    private static MapVector[] VectorRows() => [new MapLineVector("line", new(1, 1), new(2, 1)), new MapPolylineVector("polyline", [new(1, 3), new(2, 4), new(3, 3)]),
        new MapPolygonVector("polygon", new([new(5, 1), new(7, 1), new(7, 3), new(5, 3), new(5, 1)], [[new(5.5, 1.5), new(5.5, 2.5), new(6.5, 2.5), new(6.5, 1.5), new(5.5, 1.5)]])),
        new MapCircleVector("circle", new(2, 8), 10), new MapArcVector("arc", new(7, 7), 15, 0, 90)];
    private static SeatMap NewMap() => new() { ShowLegend = false, ShowLabels = false, FeaturesSource = [new MapFeature("a", [new MapPolygon([new(0, 0), new(5, 0), new(5, 10), new(0, 10), new(0, 0)])]), new MapFeature("b", [new MapPolygon([new(5, 0), new(10, 0), new(10, 10), new(5, 10), new(5, 0)])])] };
    private static Point Project(ShapeMap map, MapCoordinate point) { var viewport = map.MapViewport!; var p = viewport.Bounds.Normalize(new(point.X, point.Y)); return new(viewport.FittedArea.Left + p.X * viewport.FittedArea.Width, viewport.FittedArea.Bottom - p.Y * viewport.FittedArea.Height); }
    private static DrawingGroup Record(EngineeringChartBase chart) { var drawing = new DrawingGroup(); using var context = drawing.Open(); chart.Render(context); return drawing; }
    private static void Near(double expected, double actual) => Assert.True(Math.Abs(expected - actual) < 1e-9, $"{expected:R} != {actual:R}");
    private sealed record PieRow(string Id, double Longitude, double Latitude, ObservableCollection<MapPieSlice> Slices);
    private sealed class RegionRow(string id, string state) : INotifyPropertyChanged
    { private string _state = state; public string Id { get; } = id; public string State { get => _state; set { _state = value; PropertyChanged?.Invoke(this, new(nameof(State))); } } public event PropertyChangedEventHandler? PropertyChanged; }
    private sealed class PointRow(string id, double longitude, double latitude, double value) : INotifyPropertyChanged
    { private PropertyChangedEventHandler? _changed; public string Id { get; } = id; public double Longitude { get; } = longitude; public double Latitude { get; } = latitude; public double Value { get; } = value; public int Subscribers { get; private set; } public event PropertyChangedEventHandler? PropertyChanged { add { Subscribers++; _changed += value; } remove { Subscribers--; _changed -= value; } } }
    private sealed class Host : IDisposable
    {
        private readonly Window _window; private readonly EngineeringChartBase _chart; public Host(EngineeringChartBase chart) { _chart = chart; _window = new Window { Width = 420, Height = 280, Content = chart }; _window.Show(); Drain(); }
        public void Drain() { _window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); _window.UpdateLayout(); _ = _chart.GetMarks(); }
        public void Click(Point p) { _window.MouseDown(p, MouseButton.Left); _window.MouseUp(p, MouseButton.Left); Drain(); }
        public void Dispose() { _window.Close(); Dispatcher.UIThread.RunJobs(); }
    }
}
