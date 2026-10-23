using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class MapChartTests
{
    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void Every_catalog_mode_preserves_original_members_selection_keyboard_and_shared_view(int mode)
    {
        using var map = Make(mode); using var host = new Host(map); var layer = map.Layers[^1];
        Assert.Null(map.DataError); Assert.NotEmpty(map.GetMarks());
        foreach (var mark in map.GetMarks()) Assert.All(mark.Members, m => { Assert.NotNull(m.Item); Assert.InRange(m.SourceIndex, 0, 1); });
        var first = map.GetMarks()[0]; Assert.True(map.SelectItem(layer, first.Members[0].StableId!)); var source = map.SelectedPoint!.Members[0].Item;
        map.Zoom = 1.2; map.PanX = .02; map.PanY = -.01; host.Resize(520, 360); Assert.Contains(map.SelectedPoint!.Members, m => ReferenceEquals(source, m.Item));
        map.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.End }); map.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space }); Assert.NotNull(map.SelectedPoint);
        map.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape }); Assert.Equal(1, map.Zoom); Assert.Equal(-1, map.SelectedIndex);
        using var stream = new MemoryStream(); Assert.False(map.TryExportPng(stream, new ChartExportOptions { Dpi = 0 }));
    }

    [AvaloniaFact]
    public void Holes_are_transparent_nonhit_and_multipolygon_has_one_original_business_observation()
    {
        using var map = Make(0); using var host = new Host(map); var a = Screen(map, new(2, 5)); var hole = Screen(map, new(5, 5));
        Assert.Equal("a", map.HitTest(a)!.Members[0].StableId); Assert.Null(map.HitTest(hole));
        Assert.Equal("b", map.HitTest(Screen(map, new(12, 2)))!.Members[0].StableId); Assert.Equal("b", map.HitTest(Screen(map, new(12, 8)))!.Members[0].StableId);
        Assert.Null(map.HitTest(Screen(map, new(12, 5)))); Assert.Equal(2, map.GetMarks().Count);
    }

    [AvaloniaFact]
    public void Missing_and_null_values_are_backgrounds_without_keyboard_or_selection_observations()
    {
        using var map = Make(0); var rows = new[] { new MapValue("a", null), new MapValue("unknown", 9) }; map.Layers[0].ItemsSource = rows; using var host = new Host(map);
        Assert.Empty(map.GetMarks()); Assert.Equal(2, map.LayerSnapshots[0].Regions!.NoDataCount); Assert.Single(map.UnknownRegions); Assert.Same(rows[1], map.UnknownRegions[0].Source); Assert.Null(map.HitTest(Screen(map, new(2, 5)))); Assert.False(map.SelectItem(map.Layers[0], "a"));
        using var drawing = new DrawingGroup().Open(); map.Render(drawing);
    }

    [AvaloniaFact]
    public void Layer_order_visibility_opacity_and_separate_identity_domains_agree_with_hits()
    {
        using var map = Make(0); var upper = new BubbleLayer { ItemsSource = new[] { new MapObservation("a", new(2, 5), 1) }, MaximumRadius = .1 }; map.Layers.Add(upper); using var host = new Host(map);
        var target = Screen(map, new(2, 5)); Assert.Same(upper, ((MapGlyphKey)map.HitTest(target)!.Mark.Key!).Layer); Assert.True(map.SelectItem(map.Layers[0], "a")); Assert.True(map.SelectItem(upper, "a"));
        upper.Opacity = 0; Assert.Same(map.Layers[0], ((MapGlyphKey)map.HitTest(target)!.Mark.Key!).Layer);
        upper.Opacity = 1; upper.IsVisible = false; Assert.Same(map.Layers[0], ((MapGlyphKey)map.HitTest(target)!.Mark.Key!).Layer);
        upper.IsVisible = true; map.Layers.Move(1, 0); Assert.Same(map.Layers[1], ((MapGlyphKey)map.HitTest(target)!.Mark.Key!).Layer);
    }

    [AvaloniaFact]
    public void Later_no_data_layer_interleaves_paint_and_blocks_lower_hits_but_its_holes_pass_through()
    {
        using var map = Make(1); var lower = (BubbleLayer)map.Layers[0]; lower.Fill = Brush.Parse("#2563EB"); lower.Stroke = null;
        lower.ItemsSource = new[] { new MapObservation("covered", new(2, 5), 1), new MapObservation("hole", new(5, 5), 1) };
        var upper = new ShapeLayer { NoDataBrush = Brush.Parse("#F97316"), ItemsSource = Array.Empty<MapValue>(), Stroke = null }; map.Layers.Add(upper); using var host = new Host(map);
        Assert.Equal(2, map.GetMarks().Count); Assert.Null(map.HitTest(Screen(map, new(2, 5)))); Assert.Equal("hole", map.HitTest(Screen(map, new(5, 5)))!.Members[0].StableId);
        var colors = PaintColors(map); Assert.Equal(new[] { "#FF2563EB", "#FF2563EB", "#FFF97316", "#FFF97316" }, colors);
        map.Layers.Move(1, 0); Assert.Equal("covered", map.HitTest(Screen(map, new(2, 5)))!.Members[0].StableId); Assert.Equal(new[] { "#FFF97316", "#FFF97316", "#FF2563EB", "#FF2563EB" }, PaintColors(map));
        map.Layers.Move(0, 1); upper.Opacity = .2; Assert.Null(map.HitTest(Screen(map, new(2, 5)))); upper.Opacity = 0; Assert.Equal("covered", map.HitTest(Screen(map, new(2, 5)))!.Members[0].StableId);
        upper.Opacity = 1; upper.NoDataBrush = Brushes.Transparent; Assert.Equal("covered", map.HitTest(Screen(map, new(2, 5)))!.Members[0].StableId);
        map.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Home }); map.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space }); Assert.Equal("covered", map.SelectedPoint!.Members[0].StableId);
    }

    [AvaloniaFact]
    public void Typed_rows_sharing_one_original_source_keep_separate_ID_values_and_bubble_area()
    {
        using var map = Make(1); var original = new object(); var layer = (BubbleLayer)map.Layers[0]; layer.ItemsSource = new[] { new MapObservation("one", new(2, 5), 1, original), new MapObservation("four", new(12, 8), 4, original) }; using var host = new Host(map);
        var marks = map.GetMarks(); Assert.Equal(1d, marks[0].Value); Assert.Equal(4d, marks[1].Value); Assert.Equal(1d, marks[0].Members[0].Value); Assert.Equal(4d, marks[1].Members[0].Value); Assert.All(marks, m => Assert.Same(original, m.Members[0].Item)); Near(4, marks[1].Bounds.Width * marks[1].Bounds.Width / (marks[0].Bounds.Width * marks[0].Bounds.Width));
    }

    [AvaloniaFact]
    public void Bubble_area_zero_negative_and_size_path_retain_original_value_and_coordinate()
    {
        using var map = Make(1); var layer = (BubbleLayer)map.Layers[0]; var a = new Row("a", "a", 7, 2, 5) { Size = 1 }; var b = new Row("b", "b", 9, 12, 8) { Size = 4 }; var zero = new Row("zero", "a", 2, 3, 3) { Size = 0 };
        layer.ItemsSource = new[] { a, b, zero }; layer.SizePath = "Size"; layer.LongitudePath = "X"; layer.LatitudePath = "Y"; using var host = new Host(map);
        Assert.Null(map.DataError); Assert.Equal(3, map.LayerSnapshots[0].Bubbles!.Points.Count); Assert.Equal(2, map.GetMarks().Count);
        var marks = map.GetMarks(); Near(4, marks[1].Bounds.Width * marks[1].Bounds.Width / (marks[0].Bounds.Width * marks[0].Bounds.Width)); Assert.Equal(7d, marks[0].Members[0].Value); Assert.Equal(new ChartPoint(2, 5), marks[0].Members[0].Point); Assert.Equal(1, marks[0].Members[0].SizeValue);
        b.Size = -1; Assert.NotNull(map.DataError); Assert.Empty(map.GetMarks()); b.Size = 4; Assert.Null(map.DataError); Assert.Equal(2, map.GetMarks().Count);
    }

    [AvaloniaFact]
    public void Heatmap_cells_retain_every_contributor_real_source_indices_and_coordinates()
    {
        using var map = Make(2); var layer = (HeatmapLayer)map.Layers[0]; var first = new MapObservation("p1", new(2, 5), 2); var second = new MapObservation("p2", new(2.01, 5), 3); layer.ItemsSource = new[] { first, second }; layer.Columns = 2; layer.Rows = 2; layer.Radius = 0; using var host = new Host(map);
        var mark = Assert.Single(map.GetMarks()); Assert.Equal(5, mark.Value); Assert.Equal(2, mark.Members.Count); Assert.Equal(new[] { 0, 1 }, mark.Members.Select(m => m.SourceIndex)); Assert.Same(second, mark.Members[1].Item); Assert.Equal(new ChartPoint(2.01, 5), mark.Members[1].Point); Assert.True(map.SelectItem(layer, second));
    }

    [AvaloniaFact]
    public void Heatmap_selection_follows_a_surviving_original_member_and_clears_when_same_cell_has_only_new_rows()
    {
        using var map = Make(2); var layer = (HeatmapLayer)map.Layers[0]; layer.ItemsSource = new[] { new MapObservation("old", new(2, 5), 3) }; using var host = new Host(map); Assert.True(map.SelectItem(layer, "old"));
        layer.ItemsSource = new[] { new MapObservation("old", new(12, 8), 4) }; Assert.Equal("old", map.SelectedPoint!.Members[0].StableId); Assert.Equal(new ChartPoint(12, 8), map.SelectedPoint.Members[0].Point);
        layer.ItemsSource = new[] { new MapObservation("new", new(12, 8), 5) }; Assert.Null(map.SelectedPoint);
    }

    [AvaloniaTheory]
    [InlineData(MapProjection.Equirectangular)] [InlineData(MapProjection.WebMercator)] [InlineData(MapProjection.Identity)]
    public void Heatmap_bounds_are_positive_and_centered_after_northward_projection_zoom_and_pan(MapProjection projection)
    {
        using var map = Make(2); map.IsGeographic = projection != MapProjection.Identity; map.Projection = projection; using var host = new Host(map);
        foreach (var zoom in new[] { 1d, 1.6 })
        {
            map.Zoom = zoom; map.PanX = .02; map.PanY = -.01;
            Assert.Null(map.DataError);
            foreach (var mark in map.GetMarks()) { Assert.True(mark.Bounds.Width > 0); Assert.True(mark.Bounds.Height > 0); Near(mark.Position.X, mark.Bounds.Center.X); Near(mark.Position.Y, mark.Bounds.Center.Y); }
        }
    }

    [AvaloniaFact]
    public void Cross_layer_heatmap_budget_rejects_before_layout_and_hidden_layers_preserve_snapshot_positions()
    {
        using var map = new SeatMap { FeaturesSource = Features(), ShowLegend = false, ShowLabels = false };
        for (var layer = 0; layer < 16; layer++) map.Layers.Add(new HeatmapLayer { Radius = 8, Columns = 6, Rows = 4, ItemsSource = Enumerable.Range(0, 217).Select(i => new MapObservation(i, layer == 15 ? new(10_000, 10_000) : new(2, 5), 0)).ToArray() });
        using var host = new Host(map); Assert.NotNull(map.DataError); Assert.Empty(map.LayerSnapshots); Assert.Empty(map.GetMarks());
        map.Layers[15].IsVisible = false; Assert.Null(map.DataError); Assert.Equal(16, map.LayerSnapshots.Count); Assert.Null(map.LayerSnapshots[15].Heatmap); Assert.All(map.LayerSnapshots.Take(15), s => Assert.NotNull(s.Heatmap)); Assert.Equal(14, map.MapViewport!.Bounds.MaxX);
        map.Layers[15].IsVisible = true; Assert.NotNull(map.DataError); map.Layers[15].Opacity = 0; Assert.Null(map.DataError); Assert.Null(map.LayerSnapshots[15].Heatmap);
    }

    [AvaloniaFact]
    public void Conservative_heatmap_contribution_budget_accepts_exact_boundary_and_recovers_after_excess()
    {
        using var map = new SeatMap { FeaturesSource = Features(), ShowLegend = false, ShowLabels = false };
        map.Layers.Add(new HeatmapLayer { Radius = 8, ItemsSource = Enumerable.Range(0, 3460).Select(i => new MapObservation(i, new(2, 5), 0)).ToArray() }); // 999,940 estimated contributions; zero mass allocates no cell members.
        map.Layers.Add(new HeatmapLayer { Radius = 0, ItemsSource = Enumerable.Range(0, 60).Select(i => new MapObservation(i, new(2, 5), 0)).ToArray() });
        using var host = new Host(map); Assert.Equal(1_000_000, ChartMaps.MaximumRetainedHeatmapContributions); Assert.Null(map.DataError); Assert.All(map.LayerSnapshots, s => Assert.Empty(s.Heatmap!.Cells));
        var extra = new HeatmapLayer { Radius = 0, ItemsSource = new[] { new MapObservation("extra", new(2, 5), 0) } }; map.Layers.Add(extra); Assert.NotNull(map.DataError); Assert.Empty(map.GetMarks());
        extra.ItemsSource = Array.Empty<MapObservation>(); Assert.Null(map.DataError); Assert.Equal(3, map.LayerSnapshots.Count); Assert.All(map.LayerSnapshots, s => Assert.Empty(s.Heatmap!.Cells));
    }

    [AvaloniaTheory]
    [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(double.NegativeInfinity)]
    public void Typed_business_values_must_be_finite_even_when_a_separate_positive_size_is_mapped(double invalidValue)
    {
        using var map = Make(1); var layer = map.Layers[0]; var original = new SizedOriginal(4); layer.SizePath = "Source.Size"; layer.ItemsSource = new[] { new MapObservation("typed", new(2, 5), invalidValue, original) }; using var host = new Host(map); Assert.NotNull(map.DataError); Assert.Empty(map.GetMarks());
        layer.ItemsSource = new[] { new MapObservation("typed", new(2, 5), -7, original) }; Assert.Null(map.DataError); var mark = Assert.Single(map.GetMarks()); Assert.Equal(-7, mark.Value); Assert.Equal(-7d, mark.Members[0].Value); Assert.Equal(4, mark.Members[0].SizeValue); Assert.Same(original, mark.Members[0].Item);
    }

    [AvaloniaFact]
    public void Seat_region_fill_status_and_reorder_follow_original_rows_and_new_source_indices()
    {
        using var map = Make(4); var a = new Row("row-a", "a", 1, 0, 0) { Fill = "#22C55E" }; var b = new Row("row-b", "b", 4, 0, 0) { Fill = "#EF4444" }; var rows = new ObservableCollection<Row> { a, b }; var layer = (ShapeLayer)map.Layers[0]; layer.ItemsSource = rows; layer.RegionPath = "Region"; layer.FillPath = "Fill";
        using var host = new Host(map); Assert.True(a.Observers > 0); Assert.True(map.SelectItem(layer, "b")); Assert.Same(b, map.SelectedPoint!.Members[0].Item); rows.Move(1, 0); Assert.Same(b, map.SelectedPoint!.Members[0].Item); Assert.Equal(0, map.SelectedPoint.Members[0].SourceIndex);
        Assert.Equal(Color.Parse("#EF4444"), ((ISolidColorBrush)map.SelectedPoint.Mark.Fill!).Color); b.Fill = "#3B82F6"; Assert.Equal(Color.Parse("#3B82F6"), ((ISolidColorBrush)map.SelectedPoint!.Mark.Fill!).Color);
        rows.Remove(b); Assert.Null(map.SelectedPoint); Assert.Equal(0, b.Observers);
    }

    [AvaloniaFact]
    public void Nested_property_changes_invalid_recovery_and_source_replacement_keep_observation_alive()
    {
        using var map = Make(0); var payload = new Payload(2); var row = new NestedRow("a", payload); var layer = map.Layers[0]; layer.ValuePath = "Payload.Amount"; layer.ItemsSource = new[] { row }; using var host = new Host(map); Assert.True(payload.Observers > 0);
        payload.Amount = double.NaN; Assert.NotNull(map.DataError); Assert.Empty(map.GetMarks()); Assert.True(payload.Observers > 0); payload.Amount = 8; Assert.Null(map.DataError); Assert.Equal(8, Assert.Single(map.GetMarks()).Value);
        layer.ItemsSource = new[] { new NestedRow("b", new(4)) }; Assert.Single(map.GetMarks()); Assert.Equal(0, payload.Observers);
    }

    [AvaloniaFact]
    public void Detach_dispose_and_layer_removal_release_business_and_nested_observers()
    {
        using var map = Make(0); var row = new Row("id", "a", 2, 0, 0); var layer = map.Layers[0]; layer.RegionPath = "Region"; layer.ItemsSource = new[] { row }; using var host = new Host(map); Assert.True(row.Observers > 0);
        host.Detach(); Assert.Equal(0, row.Observers); host.Attach(); Assert.True(row.Observers > 0); map.Layers.Remove(layer); _ = map.GetMarks(); Assert.Equal(0, row.Observers); map.Layers.Add(layer); Assert.NotEmpty(map.GetMarks()); Assert.True(row.Observers > 0); map.Dispose(); Assert.Equal(0, row.Observers);
    }

    [AvaloniaFact]
    public void Shared_collection_removal_releases_retired_rows_without_rendering_any_panel_and_reads_latest_remaining_value()
    {
        var remaining = new PayloadRow("a", new(2)); var removed = new PayloadRow("b", new(4)); var rows = new ObservableCollection<PayloadRow> { remaining, removed };
        using var first = Make(0); using var second = Make(0); first.Layers[0].ItemsSource = rows; first.Layers[0].ValuePath = "Payload.Amount"; second.Layers[0].ItemsSource = rows; second.Layers[0].ValuePath = "Payload.Amount"; using var firstHost = new Host(first); using var secondHost = new Host(second);
        Assert.True(removed.Payload.Observers > 0); rows.Remove(removed); Assert.Equal(0, removed.Payload.Observers); // Inspect before either map is asked to build its next scene.
        remaining.Payload.Amount = 9; Assert.Equal(9, Assert.Single(first.GetMarks()).Value); Assert.Equal(9, Assert.Single(second.GetMarks()).Value); Assert.True(remaining.Payload.Observers > 0);
        rows.Add(new("b", new(7))); Assert.Equal(2, first.GetMarks().Count); Assert.Equal(2, second.GetMarks().Count);
    }

    [AvaloniaFact]
    public async Task Every_public_layer_mutation_checks_UI_thread_and_disposed_owner_before_modifying_collection()
    {
        using var map = Make(0); map.Layers.Add(new ShapeLayer()); using var host = new Host(map);
        await Task.Run(() => { Assert.Throws<InvalidOperationException>(() => map.Layers.RemoveAt(0)); Assert.Throws<InvalidOperationException>(() => map.Layers.Clear()); Assert.Throws<InvalidOperationException>(() => map.Layers.Move(0, 1)); });
        Assert.Equal(2, map.Layers.Count); map.Dispose(); Assert.Empty(map.Layers); Assert.Throws<ObjectDisposedException>(() => map.Layers.Clear()); Assert.Throws<ObjectDisposedException>(() => map.Layers.Add(new ShapeLayer()));
    }

    [AvaloniaFact]
    public void Layer_ownership_and_DataContext_are_valid_during_collection_notifications()
    {
        using var first = Make(0); using var second = Make(0); var context = new { Rows = new[] { new MapValue("a", 4) } }; first.DataContext = context; var layer = new ShapeLayer(); layer.Bind(MapLayer.ItemsSourceProperty, new Binding("Rows"));
        first.Layers.CollectionChanged += (_, e) => { if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add) { Assert.Same(context, layer.DataContext); Assert.Throws<InvalidOperationException>(() => second.Layers.Add(layer)); } };
        first.Layers.Add(layer); Assert.Same(context.Rows, layer.ItemsSource); first.Layers.Remove(layer); second.Layers.Add(layer); Assert.Same(layer, second.Layers[^1]);
        Assert.Throws<InvalidOperationException>(() => second.Layers.Add(layer)); Assert.Equal(2, second.Layers.Count);
    }

    [AvaloniaFact]
    public void Inherited_DataContext_replacement_updates_layer_binding_and_retires_original_observers()
    {
        using var map = Make(0); map.Layers.Clear(); var old = new Row("a", "a", 2, 0, 0); var latest = new Row("b", "b", 4, 0, 0); var layer = new ShapeLayer { RegionPath = "Region" }; layer.Bind(MapLayer.ItemsSourceProperty, new Binding("Rows")); map.Layers.Add(layer); map.DataContext = new { Rows = new[] { old } }; using var host = new Host(map); Assert.True(old.Observers > 0);
        map.DataContext = new { Rows = new[] { latest } }; Assert.Equal("b", Assert.Single(map.GetMarks()).Members[0].StableId); Assert.Equal(0, old.Observers); Assert.True(latest.Observers > 0);
    }

    [AvaloniaFact]
    public void Replacing_layer_source_inside_a_getter_never_publishes_the_retired_row()
    {
        using var map = Make(0); var layer = map.Layers[0]; var old = new ReentrantRow(); old.OnRead = () => layer.ItemsSource = new[] { new MapValue("b", 9) }; layer.ItemsSource = new[] { old }; using var host = new Host(map); host.Drain(); var mark = Assert.Single(map.GetMarks()); Assert.Equal("b", mark.Members[0].StableId); Assert.Equal(9, mark.Value); Assert.Null(map.DataError);
    }

    [AvaloniaFact]
    public void Collection_remove_can_transfer_layer_to_another_map_and_same_collection_reentry_rejects()
    {
        using var first = Make(0); using var second = Make(0); var layer = first.Layers[0]; first.Layers.CollectionChanged += (_, e) => { if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove) second.Layers.Add(layer); }; first.Layers.Remove(layer); Assert.Same(layer, second.Layers[^1]);
        second.Layers.CollectionChanged += (_, _) => Assert.Throws<InvalidOperationException>(() => second.Layers.Clear()); second.Layers.Add(new ShapeLayer()); Assert.Equal(3, second.Layers.Count);
    }

    [AvaloniaFact]
    public void View_changes_reuse_geojson_geometry_and_original_data_mapping()
    {
        using var map = Make(0); var counted = new CountingRows(); map.Layers[0].ItemsSource = counted; using var host = new Host(map); var snapshot = map.Snapshot; var projected = map.ProjectedGeometry; var enumerations = counted.Enumerations;
        map.Zoom = 2; map.PanX = .04; map.PanY = .02; host.Resize(640, 300); _ = map.GetMarks(); Assert.Same(snapshot, map.Snapshot); Assert.Same(projected, map.ProjectedGeometry); Assert.Equal(enumerations, counted.Enumerations);
        map.Projection = MapProjection.WebMercator; _ = map.GetMarks(); Assert.Same(snapshot, map.Snapshot); // IsGeographic false means identity remains effective.
    }

    [AvaloniaFact]
    public void Feature_collection_update_refreshes_geometry_and_no_stale_selected_shape_survives()
    {
        using var map = Make(0); var features = new ObservableCollection<MapFeature>(Features()); map.FeaturesSource = features; using var host = new Host(map); map.SelectItem(map.Layers[0], "a"); features.RemoveAt(0); Assert.Null(map.SelectedPoint); Assert.Single(map.Snapshot!.Features); Assert.Single(map.UnknownRegions); features.Insert(0, Features()[0]); Assert.Equal(2, map.GetMarks().Count);
    }

    [AvaloniaFact]
    public void Invalid_json_projection_coordinates_duplicate_regions_and_brush_fail_then_recover()
    {
        using var map = Make(0); map.FeaturesSource = null; map.GeoJson = "{"; using var host = new Host(map); Assert.NotNull(map.DataError); Assert.Empty(map.GetMarks()); map.GeoJson = null; map.FeaturesSource = Features(); Assert.Null(map.DataError);
        map.Layers[0].ItemsSource = new[] { new MapValue("a", 1), new MapValue("a", 2) }; Assert.NotNull(map.DataError); map.Layers[0].ItemsSource = new[] { new MapValue("a", 3) }; Assert.Null(map.DataError);
        var row = new Row("a", "a", 2, 0, 0) { Fill = "definitely-not-a-color" }; map.Layers[0].ItemsSource = new[] { row }; map.Layers[0].RegionPath = "Region"; map.Layers[0].FillPath = "Fill"; Assert.NotNull(map.DataError); row.Fill = "#FFFFFF"; Assert.Null(map.DataError);
    }

    [AvaloniaFact]
    public void Source_observation_budget_stops_before_unbounded_enumeration()
    {
        using var map = Make(1); var rows = new TooManyRows(); map.Layers[0].ItemsSource = rows; using var host = new Host(map); Assert.NotNull(map.DataError); Assert.InRange(rows.Reads, 1, ChartMaps.MaximumObservationCount + 1); Assert.Empty(map.GetMarks());
    }

    [AvaloniaFact]
    public void Single_point_without_shapes_and_opposite_finite_extremes_have_finite_shared_fit()
    {
        using var map = new SeatMap { ShowLegend = false, ShowLabels = false }; map.Layers.Add(new BubbleLayer { ItemsSource = new[] { new MapObservation("one", new(0, 0), 1) } }); using var host = new Host(map); Assert.Single(map.GetMarks()); Assert.True(map.GetMarks()[0].Bounds.Width > 0);
        map.Layers[0].ItemsSource = new[] { new MapObservation("left", new(-double.MaxValue, 0), 1), new MapObservation("right", new(double.MaxValue, 1), 1) }; Assert.Null(map.DataError); Assert.All(map.GetMarks(), mark => { Assert.True(double.IsFinite(mark.Position.X)); Assert.True(double.IsFinite(mark.Position.Y)); });
    }

    [AvaloniaFact]
    public async Task Explicit_local_load_supports_failure_recovery_cancellation_latest_wins_and_detach()
    {
        var path = Path.Combine(Path.GetTempPath(), $"charts-map-{Guid.NewGuid():N}.geojson"); await File.WriteAllTextAsync(path, Json);
        try
        {
            using var map = new ShapeMap { SourceUri = new Uri(path), ShowLabels = false, ShowLegend = false }; map.Layers.Add(new ShapeLayer { ItemsSource = new[] { new MapValue("a", 2) } }); using var host = new Host(map);
            Assert.Empty(map.GetMarks()); Assert.Equal(MapSourceState.Idle, map.SourceState); Assert.True(await map.LoadSourceAsync()); Assert.Equal(MapSourceState.Completed, map.SourceState); Assert.Single(map.GetMarks());
            using var canceled = new CancellationTokenSource(); canceled.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => map.LoadSourceAsync(canceled.Token));
            var obsolete = map.LoadSourceAsync(); map.SourceUri = new Uri(path + ".missing"); var missing = map.LoadSourceAsync(); Assert.False(await obsolete); Assert.False(await missing); Assert.Equal(MapSourceState.Failed, map.SourceState); Assert.NotNull(map.SourceError); Assert.Empty(map.GetMarks()); Assert.NotNull(map.DataError);
            map.SourceUri = new Uri(path); Assert.True(await map.LoadSourceAsync()); Assert.Single(map.GetMarks()); Assert.Null(map.DataError); var detached = map.LoadSourceAsync(); host.Detach(); Assert.False(await detached); Assert.Equal(MapSourceState.Canceled, map.SourceState); host.Attach(); Assert.Null(map.DataError);
        }
        finally { File.Delete(path); }
    }

    [AvaloniaTheory]
    [InlineData("https://example.invalid/map.json")] [InlineData("http://localhost/map.json")]
    public async Task Network_source_schemes_are_rejected_without_download(string uri)
    {
        using var map = new ShapeMap { SourceUri = new Uri(uri) }; using var host = new Host(map); Assert.False(await map.LoadSourceAsync()); Assert.IsType<ArgumentException>(map.SourceError); Assert.Empty(map.GetMarks());
    }

    [AvaloniaFact]
    public void Geographic_and_identity_projection_use_the_same_layers_and_equirectangular_physical_aspect()
    {
        using var map = Make(0); map.IsGeographic = true; using var host = new Host(map); var ratio = map.MapViewport!.FittedArea.Width / map.MapViewport.FittedArea.Height; Near(1.4, ratio);
        var north = Screen(map, ChartMaps.ProjectCoordinate(new(2, 8), map.EffectiveProjection)); var south = Screen(map, ChartMaps.ProjectCoordinate(new(2, 2), map.EffectiveProjection)); Assert.True(north.Y < south.Y);
        map.Projection = MapProjection.WebMercator; Assert.Null(map.DataError); Assert.Equal(MapProjection.WebMercator, map.EffectiveProjection); Assert.True(map.MapViewport!.FittedArea.Width > 0);
    }

    [AvaloniaTheory]
    [InlineData(MapProjection.Equirectangular)] [InlineData(MapProjection.WebMercator)]
    public void Bundled_Natural_Earth_geometry_is_usable_with_real_177_feature_topology(MapProjection projection)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory); string? path = null;
        while (root is not null) { var candidate = Path.Combine(root.FullName, "samples", "NT.AvaloniaControl.Demo", "Assets", "Maps", "natural-earth-110m.geojson"); if (File.Exists(candidate)) { path = candidate; break; } root = root.Parent; }
        Assert.NotNull(path); var json = File.ReadAllText(path); var snapshot = ChartMaps.ReadGeoJson(json); Assert.Equal(177, snapshot.Features.Count);
        foreach (var feature in snapshot.Features) try { _ = ChartMaps.Project(ChartMaps.Snapshot(new[] { feature.Feature }), projection, topologyRule: MapTopologyRule.EvenOdd); } catch (ArgumentException error) { throw new ArgumentException($"Natural Earth feature {feature.Id}: {feature.Label}", error); }
        var geometry = ChartMaps.Project(snapshot, projection, topologyRule: MapTopologyRule.EvenOdd); Assert.Equal(177, geometry.Features.Count); Assert.Equal(2, geometry.SelfIntersectionCount); Assert.NotEmpty(geometry.Features.SelectMany(f => f.Polygons));
        using var map = new ShapeMap { GeoJson = json, Projection = projection, TopologyRule = MapTopologyRule.EvenOdd, ShowLabels = false, ShowLegend = false }; map.Layers.Add(new ShapeLayer { ItemsSource = new[] { new MapValue(snapshot.Features[0].Id, 4) } }); using var host = new Host(map); Assert.Null(map.DataError); Assert.Single(map.GetMarks()); Assert.Equal(177, map.Snapshot!.Features.Count); Assert.Equal(2, map.ProjectedGeometry!.SelfIntersectionCount);
    }

    private const string Json = "{\"type\":\"FeatureCollection\",\"features\":[{\"type\":\"Feature\",\"id\":\"a\",\"properties\":{\"name\":\"A\"},\"geometry\":{\"type\":\"Polygon\",\"coordinates\":[[[0,0],[10,0],[10,10],[0,10],[0,0]]]}}]}";
    private static ShapeMap Make(int mode)
    {
        ShapeMap map = mode == 4 ? new SeatMap() : new ShapeMap { IsGeographic = false }; map.FeaturesSource = Features(); map.ShowLegend = false; map.ShowLabels = false;
        if (mode is 0 or 3 or 4) map.Layers.Add(new ShapeLayer { ItemsSource = new[] { new MapValue("a", 1), new MapValue("b", 4) } });
        else if (mode == 1) map.Layers.Add(new BubbleLayer { ItemsSource = new[] { new MapObservation("a", new(2, 5), 1), new MapObservation("b", new(12, 8), 4) } });
        else map.Layers.Add(new HeatmapLayer { ItemsSource = new[] { new MapObservation("a", new(2, 5), 1), new MapObservation("b", new(12, 8), 4) }, Radius = 0, Columns = 6, Rows = 4 });
        return map;
    }
    private static MapFeature[] Features() => [new("a", [new(Ring(0, 0, 10, 10), new[] { Ring(4, 4, 6, 6) })], "A"), new("b", [new(Ring(11, 0, 14, 3)), new(Ring(11, 7, 14, 10))], "B")];
    private static MapCoordinate[] Ring(double x, double y, double right, double top) => [new(x, y), new(right, y), new(right, top), new(x, top), new(x, y)];
    private static Point Screen(ShapeMap map, ChartPoint world) { var view = map.MapViewport!; var unit = view.Bounds.Normalize(world); var raw = new Point(view.FittedArea.Left + unit.X * view.FittedArea.Width, view.FittedArea.Bottom - unit.Y * view.FittedArea.Height); return new(map.PlotArea.Center.X + (raw.X - map.PlotArea.Center.X) * map.Zoom + map.PanX * map.PlotArea.Width, map.PlotArea.Center.Y + (raw.Y - map.PlotArea.Center.Y) * map.Zoom + map.PanY * map.PlotArea.Height); }
    private static void Near(double expected, double actual) => Assert.InRange(actual, expected - 1e-8, expected + 1e-8);
    private static string[] PaintColors(ShapeMap map)
    {
        var group = new DrawingGroup(); using (var context = group.Open()) map.Render(context);
        IEnumerable<string> Colors(Drawing drawing) { if (drawing is DrawingGroup children) { foreach (var child in children.Children) foreach (var color in Colors(child)) yield return color; } else if (drawing is GeometryDrawing { Brush: ISolidColorBrush brush } && brush.Color.A > 0 && brush.Opacity > 0) yield return brush.Color.ToString().ToUpperInvariant(); }
        return Colors(group).ToArray();
    }
    private sealed class Host : IDisposable
    {
        private readonly ShapeMap _map; private readonly Window _window;
        public Host(ShapeMap map) { _map = map; _window = new() { Width = 420, Height = 280, Content = map }; _window.Show(); Resize(420, 280); }
        public void Resize(double width, double height) { _map.Measure(new(width, height)); _map.Arrange(new(0, 0, width, height)); _ = _map.GetMarks(); Drain(); }
        public void Detach() { _window.Content = null; Drain(); }
        public void Attach() { _window.Content = _map; Resize(420, 280); }
        public void Drain() { Dispatcher.UIThread.RunJobs(); _ = _map.GetMarks(); }
        public void Dispose() { _window.Content = null; _window.Close(); Dispatcher.UIThread.RunJobs(); }
    }
    private sealed class Row(string id, string region, double value, double x, double y) : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _changed; private double _size = value; private string? _fill;
        public string Id => id; public string Region => region; public double Value { get; } = value; public double X => x; public double Y => y;
        public double Size { get => _size; set { _size = value; _changed?.Invoke(this, new(nameof(Size))); } }
        public string? Fill { get => _fill; set { _fill = value; _changed?.Invoke(this, new(nameof(Fill))); } }
        public int Observers { get; private set; }
        public event PropertyChangedEventHandler? PropertyChanged { add { _changed += value; Observers++; } remove { _changed -= value; Observers--; } }
    }
    private sealed class Payload(double amount) : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _changed; private double _amount = amount; public int Observers { get; private set; }
        public double Amount { get => _amount; set { _amount = value; _changed?.Invoke(this, new(nameof(Amount))); } }
        public event PropertyChangedEventHandler? PropertyChanged { add { _changed += value; Observers++; } remove { _changed -= value; Observers--; } }
    }
    private sealed record NestedRow(string RegionId, Payload Payload);
    private sealed record PayloadRow(string RegionId, Payload Payload);
    private sealed record SizedOriginal(double Size);
    private sealed class CountingRows : IEnumerable
    { public int Enumerations { get; private set; } public IEnumerator GetEnumerator() { Enumerations++; return new[] { new MapValue("a", 2), new MapValue("b", 4) }.GetEnumerator(); } }
    private sealed class TooManyRows : IEnumerable
    { public int Reads { get; private set; } public IEnumerator GetEnumerator() { for (var i = 0; ; i++) { Reads++; yield return new MapObservation(i, new(2, 5), 1); } } }
    private sealed class ReentrantRow
    { public Action? OnRead { get; set; } public string RegionId { get { var callback = OnRead; OnRead = null; callback?.Invoke(); return "a"; } } public double Value => 2; }
}
