using System.Text.Json;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class MapMathTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static MapCoordinate[] Box(double x0 = 0, double y0 = 0, double x1 = 10, double y1 = 10) =>
        [new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1), new(x0, y0)];
    private static MapFeature Feature(object? source = null) => new("a", [new(Box(), [Box(2, 2, 4, 4)])], "Alpha", source);
    private static MapGeometrySnapshot Snapshot(params MapFeature[] features) => ChartMaps.Snapshot(features, Token);
    private static void Near(double expected, double actual, double tolerance = 1e-10) => Assert.InRange(actual,
        expected - tolerance * Math.Max(1, Math.Abs(expected)), expected + tolerance * Math.Max(1, Math.Abs(expected)));
    private static double Area(IReadOnlyList<ChartPoint> ring)
    { double sum = 0; for (var i = 0; i < ring.Count - 1; i++) sum += ring[i].X * ring[i + 1].Y - ring[i + 1].X * ring[i].Y; return Math.Abs(sum) / 2; }

    [Fact]
    public void Snapshot_owns_collections_and_retains_original_feature_identity_index_and_metadata()
    {
        var source = new object(); var ring = Box(); var holes = new List<MapCoordinate[]> { Box(2, 2, 4, 4) };
        var polygon = new MapPolygon(ring, holes); var polygons = new List<MapPolygon> { polygon };
        var feature = new MapFeature("a", polygons, "Alpha", source); var features = new List<MapFeature> { feature };
        var snapshot = ChartMaps.Snapshot(features, Token); features.Clear(); polygons.Clear(); holes.Clear(); ring[0] = new(999, 999);
        var entry = Assert.Single(snapshot.Features); Assert.Same(feature, entry.Feature); Assert.Same(source, entry.Source);
        Assert.Equal("a", entry.Id); Assert.Equal("Alpha", entry.Label); Assert.Equal(0, entry.SourceIndex); Assert.Equal(10, snapshot.VertexCount);
        Assert.Equal(new MapCoordinate(0, 0), polygon.Outer[0]); Assert.Single(polygon.Holes);
        Assert.Throws<NotSupportedException>(() => ((IList<MapFeatureEntry>)snapshot.Features).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<MapCoordinate>)polygon.Outer).Clear());
    }

    [Fact]
    public void Snapshot_rejects_duplicate_null_ids_and_empty_features()
    {
        Assert.Throws<ArgumentException>(() => Snapshot(Feature(), Feature()));
        Assert.Throws<ArgumentNullException>(() => Snapshot(new MapFeature(null!, [new(Box())])));
        Assert.Throws<ArgumentException>(() => Snapshot(new MapFeature("a", [])));
    }

    [Fact]
    public void Ring_constructor_rejects_unclosed_short_and_nonfinite_coordinates()
    {
        Assert.Throws<ArgumentException>(() => new MapPolygon([new(0, 0), new(1, 0), new(0, 1)]));
        Assert.Throws<ArgumentException>(() => new MapPolygon([new(0, 0), new(1, 0), new(0, 0)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MapPolygon([new(0, 0), new(double.NaN, 0), new(0, 1), new(0, 0)]));
    }

    [Fact]
    public void Ring_feature_and_observation_budgets_stop_before_unbounded_materialization()
    {
        var reads = 0;
        IEnumerable<MapCoordinate> Coordinates()
        { while (true) { reads++; yield return new(reads, 0); } }
        Assert.Throws<ArgumentOutOfRangeException>(() => new MapPolygon(Coordinates()));
        Assert.Equal(ChartMaps.MaximumRingVertexCount + 1, reads);
        reads = 0;
        IEnumerable<MapFeature> Features()
        { while (true) { reads++; yield return new(reads, [new(Box())]); } }
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartMaps.Snapshot(Features(), Token));
        Assert.Equal(ChartMaps.MaximumFeatureCount + 1, reads);
        reads = 0;
        IEnumerable<MapObservation> Observations()
        { while (true) { reads++; yield return new(reads, new(0, 0)); } }
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartMaps.Bubbles(Observations(), MapProjection.Identity, cancellationToken: Token));
        Assert.Equal(ChartMaps.MaximumObservationCount + 1, reads);
    }

    [Theory]
    [InlineData(-180, -90, 0, 0)]
    [InlineData(0, 0, .5, .5)]
    [InlineData(180, 90, 1, 1)]
    [InlineData(90, 45, .75, .75)]
    public void Equirectangular_projection_has_explicit_north_positive_world_coordinates(double x, double y, double expectedX, double expectedY)
    {
        var projected = ChartMaps.ProjectCoordinate(new(x, y), MapProjection.Equirectangular);
        Near(expectedX, projected.X); Near(expectedY, projected.Y);
    }

    [Fact]
    public void Mercator_clamps_legal_poles_with_diagnostic_count_and_keeps_equator()
    {
        var geometry = Snapshot(new MapFeature("poles", [new(Box(-20, -90, 20, 90))]));
        var result = ChartMaps.Project(geometry, MapProjection.WebMercator, Token);
        Assert.Equal(5, result.ClampedLatitudeCount); Near(0, result.Bounds.MinY); Near(1, result.Bounds.MaxY);
        var equator = ChartMaps.ProjectCoordinate(new(0, 0), MapProjection.WebMercator); Near(.5, equator.X); Near(.5, equator.Y);
        Near(1, ChartMaps.ProjectCoordinate(new(180, ChartMaps.MercatorLatitudeLimit), MapProjection.WebMercator).Y);
        Near(0, ChartMaps.ProjectCoordinate(new(-180, -ChartMaps.MercatorLatitudeLimit), MapProjection.WebMercator).Y);
    }

    [Theory]
    [InlineData(181, 0)]
    [InlineData(-181, 0)]
    [InlineData(0, 91)]
    [InlineData(0, -91)]
    public void Geographic_out_of_range_values_reject_instead_of_silent_wrap_or_clamp(double x, double y)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartMaps.ProjectCoordinate(new(x, y), MapProjection.Equirectangular));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartMaps.ProjectCoordinate(new(x, y), MapProjection.WebMercator));
        Assert.Equal(new ChartPoint(x, y), ChartMaps.ProjectCoordinate(new(x, y), MapProjection.Identity));
    }

    [Fact]
    public void Identity_projection_donut_hole_and_original_source_are_preserved()
    {
        var original = new object(); var geometry = Snapshot(Feature(original)); var result = ChartMaps.Project(geometry, MapProjection.Identity, Token);
        var feature = Assert.Single(result.Features); var polygon = Assert.Single(feature.Polygons);
        Assert.Same(original, feature.Entry.Source); Assert.Equal(new MapBounds(0, 0, 10, 10), result.Bounds);
        Assert.True(ChartMaps.Contains(polygon, new(1, 1))); Assert.False(ChartMaps.Contains(polygon, new(3, 3)));
        Assert.False(ChartMaps.Contains(polygon, new(2, 3))); Assert.True(ChartMaps.Contains(polygon, new(0, 3)));
        Assert.False(ChartMaps.Contains(polygon, new(12, 3))); Near(96, Area(polygon.Outer) - polygon.Holes.Sum(Area));
        Assert.Throws<NotSupportedException>(() => ((IList<ChartPoint>)polygon.Outer).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<MapProjectedFeature>)result.Features).Clear());
    }

    [Fact]
    public void Projection_rejects_zero_area_self_intersection_and_invalid_hole_topology()
    {
        Assert.Throws<ArgumentException>(() => ChartMaps.Project(Snapshot(new MapFeature("line", [new([new(0, 0), new(1, 0), new(2, 0), new(0, 0)])])), MapProjection.Identity, Token));
        Assert.Throws<ArgumentException>(() => ChartMaps.Project(Snapshot(new MapFeature("cross", [new([new(0, 0), new(4, 4), new(0, 3), new(4, 0), new(0, 0)])])), MapProjection.Identity, Token));
        foreach (var hole in new[] { Box(9, 9, 12, 12), Box(0, 1, 2, 2), Box(20, 20, 30, 30) })
            Assert.Throws<ArgumentException>(() => ChartMaps.Project(Snapshot(new MapFeature("a", [new(Box(), [hole])])), MapProjection.Identity, Token));
        Assert.Throws<ArgumentException>(() => ChartMaps.Project(Snapshot(new MapFeature("a", [new(Box(), [Box(1, 1, 6, 6), Box(2, 2, 4, 4)])])), MapProjection.Identity, Token));
        Assert.Throws<ArgumentException>(() => ChartMaps.Project(Snapshot(new MapFeature("a", [new(Box(), [Box(1, 1, 6, 6), Box(4, 4, 8, 8)])])), MapProjection.Identity, Token));
    }

    [Fact]
    public void EvenOdd_requires_explicit_opt_in_retains_crossing_geometry_and_reports_source_intersections()
    {
        var geometry = Snapshot(new MapFeature("cross", [new([new(0, 0), new(5, 4), new(0, 3), new(4, 0), new(0, 0)])]));
        Assert.Throws<ArgumentException>(() => ChartMaps.Project(geometry, MapProjection.Identity, Token));
        var result = ChartMaps.Project(geometry, MapProjection.Identity, Token, MapTopologyRule.EvenOdd);
        Assert.Equal(1, result.SelfIntersectionCount); Assert.Equal(geometry.Features[0], result.Features[0].Entry);
        Assert.Single(result.Features[0].Polygons); Assert.Equal(5, result.Features[0].Polygons[0].Outer.Count);
        Assert.Throws<ArgumentException>(() => ChartMaps.Project(Snapshot(new MapFeature("balanced", [new([new(0, 0), new(4, 4), new(0, 4), new(4, 0), new(0, 0)])])), MapProjection.Identity, Token, MapTopologyRule.EvenOdd));
        Assert.Throws<ArgumentException>(() => ChartMaps.Project(Snapshot(new MapFeature("hole", [new(Box(), [Box(20, 20, 30, 30)])])), MapProjection.Identity, Token, MapTopologyRule.EvenOdd));
    }

    [Theory]
    [InlineData(MapProjection.Equirectangular)]
    [InlineData(MapProjection.Identity)]
    public void Contains_preserves_inclusive_projected_boundaries_without_accepting_nearby_outside_points(MapProjection projection)
    {
        var source = Snapshot(new MapFeature("cross", [new([new(0, 0), new(4, 4), new(0, 3), new(3, 0), new(0, 0)])]));
        var projected = ChartMaps.Project(source, projection, Token, MapTopologyRule.EvenOdd);
        var polygon = Assert.Single(projected.Features[0].Polygons);
        foreach (var point in new[] { new MapCoordinate(1, 2), new MapCoordinate(2, 2) })
            Assert.True(ChartMaps.Contains(polygon, ChartMaps.ProjectCoordinate(point, projection)));
        Assert.False(ChartMaps.Contains(polygon, ChartMaps.ProjectCoordinate(new(2 + 1e-8, 2), projection)));
        Assert.False(ChartMaps.Contains(polygon, ChartMaps.ProjectCoordinate(new(1 - 1e-8, 2), projection)));
    }

    [Fact]
    public void Mercator_hit_testing_uses_projected_edges_rather_than_unprojected_geographic_edges()
    {
        var source = Snapshot(new MapFeature("cross", [new([new(0, 0), new(4, 4), new(0, 3), new(3, 0), new(0, 0)])]));
        var projected = ChartMaps.Project(source, MapProjection.WebMercator, Token, MapTopologyRule.EvenOdd);
        var polygon = Assert.Single(projected.Features[0].Polygons);
        Assert.False(ChartMaps.Contains(polygon, ChartMaps.ProjectCoordinate(new(1, 2), MapProjection.WebMercator)));
        Assert.False(ChartMaps.Contains(polygon, ChartMaps.ProjectCoordinate(new(2, 2), MapProjection.WebMercator)));
        var a = polygon.Outer[0]; var b = polygon.Outer[1];
        Assert.True(ChartMaps.Contains(polygon, new((a.X + b.X) / 2, (a.Y + b.Y) / 2)));
    }

    [Theory]
    [InlineData(MapTopologyRule.StrictSimple)]
    [InlineData(MapTopologyRule.EvenOdd)]
    public void Rounded_public_hit_boundaries_do_not_relax_strict_hole_topology_validation(MapTopologyRule rule)
    {
        var source = Snapshot(new MapFeature("outside-hole", [new(Box(), [Box(-1e-14, 2, -5e-15, 4)])]));
        Assert.Throws<ArgumentException>(() => ChartMaps.Project(source, MapProjection.Identity, Token, rule));
    }

    [Fact]
    public void Longitude_hole_alignment_uses_extents_and_is_independent_of_outer_vertex_density()
    {
        var outer = new List<MapCoordinate> { new(-150, -20), new(0, -20), new(150, -20), new(150, 20), new(0, 20), new(-150, 20) };
        for (var y = 19; y > -20; y--) outer.Add(new(-150, y)); outer.Add(outer[0]);
        var projected = ChartMaps.Project(Snapshot(new MapFeature("wide", [new(outer, [Box(130, -5, 140, 5)])])), MapProjection.Equirectangular, Token);
        Assert.Single(projected.Features[0].Polygons[0].Holes);
        Assert.False(ChartMaps.Contains(projected.Features[0].Polygons[0], ChartMaps.ProjectCoordinate(new(135, 0), MapProjection.Equirectangular)));
    }

    [Fact]
    public void Dateline_polygon_splits_narrow_parts_and_keeps_clipped_holes_without_world_strip()
    {
        var feature = new MapFeature("seam", [new(Box(170, -10, -170, 10), [Box(175, -5, -175, 5)])]);
        var result = ChartMaps.Project(Snapshot(feature), MapProjection.Equirectangular, Token);
        var parts = Assert.Single(result.Features).Polygons; Assert.Equal(2, parts.Count);
        foreach (var part in parts)
        {
            Assert.True(part.Outer.Max(p => p.X) - part.Outer.Min(p => p.X) < .06);
            Assert.Single(part.Holes); Assert.All(part.Outer, p => Assert.InRange(p.X, 0, 1));
        }
        Near((20d * 20 - 10d * 10) / (360 * 180), parts.Sum(p => Area(p.Outer) - p.Holes.Sum(Area)));
        Assert.Contains(parts, p => ChartMaps.Contains(p, ChartMaps.ProjectCoordinate(new(172, 0), MapProjection.Equirectangular)));
        Assert.Contains(parts, p => ChartMaps.Contains(p, ChartMaps.ProjectCoordinate(new(-172, 0), MapProjection.Equirectangular)));
        Assert.DoesNotContain(parts, p => ChartMaps.Contains(p, new(.5, .5)));
        Assert.DoesNotContain(parts, p => ChartMaps.Contains(p, ChartMaps.ProjectCoordinate(new(178, 0), MapProjection.Equirectangular)));
    }

    [Fact]
    public void Dateline_ring_direction_does_not_change_area_or_hole_alignment()
    {
        var ring = Box(170, -10, -170, 10); var hole = Box(-175, -5, 175, 5);
        var a = ChartMaps.Project(Snapshot(new MapFeature("a", [new(ring, [hole])])), MapProjection.WebMercator, Token);
        var b = ChartMaps.Project(Snapshot(new MapFeature("a", [new(ring.Reverse(), [hole.Reverse()])])), MapProjection.WebMercator, Token);
        Near(a.Features[0].Polygons.Sum(p => Area(p.Outer) - p.Holes.Sum(Area)), b.Features[0].Polygons.Sum(p => Area(p.Outer) - p.Holes.Sum(Area)));
    }

    [Fact]
    public void Geographic_polar_closure_preserves_full_world_span_and_mercator_clamp_does_not_reject_valid_cap()
    {
        MapCoordinate[] cap = [new(180, -80), new(180, -90), new(-180, -90), new(-180, -80), new(-90, -75), new(0, -78), new(90, -75), new(180, -80)];
        foreach (var projection in new[] { MapProjection.Equirectangular, MapProjection.WebMercator })
        {
            var result = ChartMaps.Project(Snapshot(new MapFeature("polar", [new(cap)])), projection, Token);
            Near(0, result.Bounds.MinX); Near(1, result.Bounds.MaxX); Near(0, result.Bounds.MinY);
            Assert.True(result.Features[0].Polygons.Sum(p => Area(p.Outer)) > 0);
            Assert.True(ChartMaps.Contains(result.Features[0].Polygons[0], new(.5, .001)));
            if (projection == MapProjection.WebMercator) Assert.Equal(2, result.ClampedLatitudeCount);
        }
    }

    [Fact]
    public void Redundant_adjacent_coordinates_are_retained_in_original_source_and_canonicalized_only_in_projected_ring()
    {
        MapCoordinate[] coordinates = [new(0, 0), new(1, 0), new(1, 0), new(1, 1), new(0, 1), new(0, 0), new(0, 0)];
        var source = new MapPolygon(coordinates); var snapshot = Snapshot(new MapFeature("a", [source]));
        Assert.Equal(7, snapshot.VertexCount); Assert.Equal(7, source.Outer.Count);
        var projected = ChartMaps.Project(snapshot, MapProjection.Identity, Token); Assert.Equal(5, projected.Features[0].Polygons[0].Outer.Count);
    }

    [Fact]
    public void Fully_clamped_polar_feature_is_retained_with_explicit_collapsed_diagnostic()
    {
        var original = new object(); var projected = ChartMaps.Project(Snapshot(new MapFeature("pole", [new(Box(20, 86, 30, 89))], source: original)), MapProjection.WebMercator, Token);
        Assert.Equal(1, projected.CollapsedFeatureCount); Assert.Equal(5, projected.ClampedLatitudeCount);
        Assert.Same(original, Assert.Single(projected.Features).Entry.Source); Assert.Empty(projected.Features[0].Polygons);
    }

    [Fact]
    public void Intersection_validation_has_an_explicit_work_budget_before_quadratic_work()
    {
        var ring = Enumerable.Range(0, 4095).Select(i => new MapCoordinate(Math.Cos(i * Math.PI * 2 / 4095), Math.Sin(i * Math.PI * 2 / 4095))).ToList(); ring.Add(ring[0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartMaps.Project(Snapshot(new MapFeature("large", [new(ring)])), MapProjection.Identity, Token));
    }

    [Fact]
    public void Multi_polygon_retains_components_and_shared_bounds()
    {
        var result = ChartMaps.Project(Snapshot(new MapFeature("a", [new(Box(0, 0, 10, 10)), new(Box(30, 20, 40, 25))])), MapProjection.Identity, Token);
        Assert.Equal(2, result.Features[0].Polygons.Count); Assert.Equal(new MapBounds(0, 0, 40, 25), result.Bounds);
    }

    [Fact]
    public void Bounds_normalization_interpolation_and_aspect_are_safe_for_opposite_finite_extrema()
    {
        var bounds = new MapBounds(-double.MaxValue, -double.MaxValue, double.MaxValue, double.MaxValue);
        Assert.Equal(new ChartPoint(.5, .5), bounds.Normalize(new(0, 0))); Assert.Equal(new ChartPoint(0, 0), bounds.Normalize(new(-double.MaxValue, -double.MaxValue)));
        Assert.Equal(new ChartPoint(1, 1), bounds.Normalize(new(double.MaxValue, double.MaxValue)));
        Assert.Equal(new ChartPoint(0, 0), bounds.Interpolate(.5, .5)); Near(1, bounds.AspectRatio); Assert.Equal(double.MaxValue, bounds.Width);
        var projected = ChartMaps.Project(Snapshot(new MapFeature("giant", [new(Box(-double.MaxValue, -double.MaxValue, double.MaxValue, double.MaxValue))])), MapProjection.Identity, Token);
        Assert.Equal(bounds, projected.Bounds);
    }

    [Fact]
    public void Join_preserves_source_rows_indices_null_no_data_unknown_keys_and_negative_finite_values()
    {
        var a = new object(); var unknown = new object();
        var result = ChartMaps.Join(Snapshot(Feature(), new("b", [new(Box(20, 0, 30, 10))]), new("c", [new(Box(40, 0, 50, 10))])),
            [new("x", 10, unknown), new("a", -2, a), new("b", null)], Token);
        Assert.Same(a, result.Regions[0].Data!.Source); Assert.Equal(1, result.Regions[0].Data!.SourceIndex); Assert.Equal(-2, result.Regions[0].Value);
        Assert.True(result.Regions[1].IsNoData); Assert.NotNull(result.Regions[1].Data); Assert.Null(result.Regions[2].Data); Assert.Equal(2, result.NoDataCount);
        Assert.Same(unknown, Assert.Single(result.UnknownValues).Source); Assert.Equal(0, result.UnknownValues[0].SourceIndex);
    }

    [Fact]
    public void Join_rejects_duplicate_null_region_keys_and_nonfinite_values()
    {
        Assert.Throws<ArgumentException>(() => ChartMaps.Join(Snapshot(Feature()), [new("a", 1), new("a", 2)], Token));
        Assert.Throws<ArgumentNullException>(() => ChartMaps.Join(Snapshot(Feature()), [new(null!, 1)], Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartMaps.Join(Snapshot(Feature()), [new("a", double.NaN)], Token));
    }

    [Fact]
    public void Bubbles_scale_area_by_value_and_keep_zero_invisible_original_identity_and_equal_coordinate_rows()
    {
        var source = new object(); var result = ChartMaps.Bubbles([new("a", new(0, 0), 1, source, "A"), new("b", new(0, 0), 4), new("zero", new(0, 0), 0)],
            MapProjection.Identity, maxRadius: .2, cancellationToken: Token);
        Near(.1, result.Points[0].Radius); Near(.2, result.Points[1].Radius); Near(0, result.Points[2].Radius);
        Assert.Same(source, result.Points[0].Entry.Source); Assert.Equal(0, result.Points[0].Entry.SourceIndex); Assert.Equal("A", result.Points[0].Entry.Label);
        Assert.Equal(3, result.Points.Count);
    }

    [Fact]
    public void Bubble_floor_is_explicit_and_subnormal_to_maximum_weights_remain_finite()
    {
        var result = ChartMaps.Bubbles([new("a", new(0, 0), double.Epsilon), new("b", new(1, 1), double.MaxValue), new("c", new(0, 1), 0)],
            MapProjection.Identity, .01, .1, Token);
        Near(.01, result.Points[0].Radius); Near(.1, result.Points[1].Radius); Near(0, result.Points[2].Radius);
        var unfloored = ChartMaps.Bubbles([new("a", new(0, 0), double.Epsilon), new("b", new(1, 1), double.MaxValue)], MapProjection.Identity, cancellationToken: Token);
        Assert.True(unfloored.Points[0].Radius > 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartMaps.Bubbles([], MapProjection.Identity, .2, .1, Token));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Spatial_layers_reject_invalid_weights(double value)
    {
        MapObservation[] points = [new("a", new(0, 0), value)];
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartMaps.Bubbles(points, MapProjection.Identity, cancellationToken: Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartMaps.Heatmap(points, MapProjection.Identity, cancellationToken: Token));
    }

    [Fact]
    public void Spatial_layers_reject_duplicate_observation_ids_but_retain_overlap()
    {
        Assert.Throws<ArgumentException>(() => ChartMaps.Bubbles([new("a", new(0, 0)), new("a", new(1, 1))], MapProjection.Identity, cancellationToken: Token));
        Assert.Equal(2, ChartMaps.Bubbles([new("a", new(0, 0)), new("b", new(0, 0))], MapProjection.Identity, cancellationToken: Token).Points.Count);
    }

    [Fact]
    public void Heatmap_exact_bins_use_projected_coordinates_shared_bounds_and_original_members()
    {
        var source = new object(); var result = ChartMaps.Heatmap([new("a", new(-90, -45), 2, source), new("b", new(90, 45), 3), new("c", new(90, 45), 4), new("z", new(0, 0), 0)],
            MapProjection.Equirectangular, 2, 2, 0, cancellationToken: Token);
        Assert.Equal(2, result.Cells.Count); var low = result.Cells[0]; var high = result.Cells[1];
        Assert.Equal((0, 0), (low.Column, low.Row)); Assert.Equal((1, 1), (high.Column, high.Row)); Near(2, low.Value); Near(7, high.Value);
        Assert.Equal(new MapBounds(0, 0, .5, .5), low.Bounds); Assert.Same(source, Assert.Single(low.Members).Source);
        Assert.Equal([1, 2], high.Members.Select(m => m.SourceIndex)); Near(9, result.Cells.Sum(c => c.Value));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(.5, .5)]
    [InlineData(1, 1)]
    public void Heatmap_gaussian_kernel_renormalizes_at_boundaries_and_preserves_mass(double x, double y)
    {
        var result = ChartMaps.Heatmap([new("a", new(x, y), 10)], MapProjection.Identity, 3, 3, 1, new(0, 0, 1, 1), Token);
        Near(10, result.Cells.Sum(c => c.Value)); Assert.All(result.Cells, cell => Assert.Equal("a", Assert.Single(cell.Members).Id));
        var origin = result.Cells.Single(c => c.Column == Math.Min(2, (int)(x * 3)) && c.Row == Math.Min(2, (int)(y * 3)));
        Assert.True(result.Cells.All(c => c.Value <= origin.Value));
    }

    [Fact]
    public void Heatmap_is_deterministic_preserves_input_order_and_uses_exact_nonunit_bounds()
    {
        MapObservation[] points = [new("b", new(15, 15), 2), new("a", new(15, 15), 3)];
        var a = ChartMaps.Heatmap(points, MapProjection.Identity, 2, 2, 1, new(10, 10, 30, 30), Token);
        var b = ChartMaps.Heatmap(points, MapProjection.Identity, 2, 2, 1, new(10, 10, 30, 30), Token);
        Assert.Equal(a.Cells.Select(c => c.Value), b.Cells.Select(c => c.Value));
        Assert.All(a.Cells, cell => Assert.Equal(["b", "a"], cell.Members.Select(m => m.Id)));
        Assert.Equal(new MapBounds(10, 10, 20, 20), a.Cells[0].Bounds); Near(5, a.Cells.Sum(c => c.Value));
    }

    [Fact]
    public void Heatmap_work_bounds_and_overflow_fail_explicitly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartMaps.Heatmap([], MapProjection.Identity, 257, cancellationToken: Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartMaps.Heatmap([], MapProjection.Identity, radius: 9, cancellationToken: Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartMaps.Heatmap([new("a", new(2, 2))], MapProjection.Identity, bounds: new(0, 0, 1, 1), cancellationToken: Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartMaps.Heatmap([new("a", new(0, 0), double.MaxValue), new("b", new(0, 0), double.MaxValue)], MapProjection.Identity, 1, 1, 0, cancellationToken: Token));
    }

    [Fact]
    public void Heatmap_degenerate_identity_extent_and_extreme_bounds_have_finite_cell_endpoints()
    {
        var flat = ChartMaps.Heatmap([new("a", new(5, 5))], MapProjection.Identity, 2, 2, 0, cancellationToken: Token);
        Assert.Equal(new MapBounds(5, 5, 5, 5), Assert.Single(flat.Cells).Bounds);
        var giant = ChartMaps.Heatmap([new("a", new(0, 0))], MapProjection.Identity, 2, 2, 0,
            new(-double.MaxValue, -double.MaxValue, double.MaxValue, double.MaxValue), Token);
        Assert.Equal(new MapBounds(0, 0, double.MaxValue, double.MaxValue), Assert.Single(giant.Cells).Bounds);
    }

    [Fact]
    public void GeoJson_reads_features_multipolygons_properties_ids_labels_holes_and_owned_original_source()
    {
        const string json = """
        {"type":"FeatureCollection","features":[{"type":"Feature","id":"a","properties":{"name":"Alpha"},"geometry":{"type":"Polygon","coordinates":[[[0,0],[10,0],[10,10],[0,10],[0,0]],[[2,2],[4,2],[4,4],[2,4],[2,2]]]}},{"type":"Feature","id":12,"properties":{"name":"Beta"},"geometry":{"type":"MultiPolygon","coordinates":[[[[20,0],[25,0],[25,5],[20,5],[20,0]]],[[[30,0],[35,0],[35,5],[30,5],[30,0]]]]}}]}
        """;
        var snapshot = ChartMaps.ReadGeoJson(json, cancellationToken: Token); Assert.Equal(2, snapshot.Features.Count);
        Assert.Equal("a", snapshot.Features[0].Id); Assert.Equal(12L, snapshot.Features[1].Id); Assert.Equal("Alpha", snapshot.Features[0].Label);
        Assert.Single(snapshot.Features[0].Feature.Polygons[0].Holes); Assert.Equal(2, snapshot.Features[1].Feature.Polygons.Count);
        var original = Assert.IsType<JsonElement>(snapshot.Features[0].Source); Assert.Equal("a", original.GetProperty("id").GetString());
        Assert.Equal(2, ChartMaps.Project(snapshot, MapProjection.Identity, Token).Features.Count);
    }

    [Fact]
    public void GeoJson_configured_business_id_property_takes_precedence_and_missing_ids_have_stable_indices()
    {
        const string feature = """{"type":"Feature","id":"ignored","properties":{"code":"business","title":"Seat"},"geometry":{"type":"Polygon","coordinates":[[[0,0],[1,0],[1,1],[0,1],[0,0]]]}}""";
        var snapshot = ChartMaps.ReadGeoJson(feature, "code", "title", Token); Assert.Equal("business", snapshot.Features[0].Id); Assert.Equal("Seat", snapshot.Features[0].Label);
        Assert.Throws<ArgumentException>(() => ChartMaps.ReadGeoJson(feature, "missing", cancellationToken: Token));
        var bare = ChartMaps.ReadGeoJson("""{"type":"Polygon","coordinates":[[[0,0],[1,0],[1,1],[0,1],[0,0]]]}""", cancellationToken: Token);
        Assert.Equal(0, bare.Features[0].Id);
    }

    [Theory]
    [InlineData("Point", "[0,0]")]
    [InlineData("LineString", "[[0,0],[1,1]]")]
    [InlineData("GeometryCollection", "[]")]
    public void GeoJson_unsupported_geometry_rejects_explicitly(string kind, string coordinates)
    {
        var json = "{\"type\":\"" + kind + "\",\"coordinates\":" + coordinates + "}";
        Assert.Throws<NotSupportedException>(() => ChartMaps.ReadGeoJson(json, cancellationToken: Token));
    }

    [Theory]
    [InlineData("{\"type\":\"Polygon\",\"coordinates\":[]}")]
    [InlineData("{\"type\":\"Polygon\",\"coordinates\":[[[0,0,1],[1,0],[1,1],[0,0]]]}")]
    [InlineData("{\"type\":\"Polygon\",\"coordinates\":[[[0,0],[1,0],[1,1],[0,1]]]}")]
    [InlineData("{\"type\":\"Feature\",\"id\":null,\"geometry\":{\"type\":\"Polygon\",\"coordinates\":[]}}")]
    [InlineData("{\"type\":\"Feature\",\"geometry\":null}")]
    public void GeoJson_malformed_unsupported_dimension_null_geometry_and_null_id_reject(string json)
    { Assert.ThrowsAny<ArgumentException>(() => ChartMaps.ReadGeoJson(json, cancellationToken: Token)); }

    [Fact]
    public void GeoJson_byte_and_depth_budgets_are_enforced()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartMaps.ReadGeoJson(new string(' ', ChartMaps.MaximumJsonBytes + 1), cancellationToken: Token));
        Assert.ThrowsAny<JsonException>(() => ChartMaps.ReadGeoJson(new string('[', 33) + "0" + new string(']', 33), cancellationToken: Token));
    }

    [Fact]
    public void Every_expensive_entry_point_observes_cancellation_before_source_enumeration()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); var token = cancellation.Token; var reads = 0;
        IEnumerable<MapFeature> Features() { reads++; yield return Feature(); }
        IEnumerable<MapObservation> Points() { reads++; yield return new("a", new(0, 0)); }
        IEnumerable<MapValue> Values() { reads++; yield return new("a", 1); }
        Assert.Throws<OperationCanceledException>(() => ChartMaps.Snapshot(Features(), token));
        Assert.Throws<OperationCanceledException>(() => ChartMaps.Project(Snapshot(Feature()), MapProjection.Identity, token));
        Assert.Throws<OperationCanceledException>(() => ChartMaps.Join(Snapshot(Feature()), Values(), token));
        Assert.Throws<OperationCanceledException>(() => ChartMaps.Bubbles(Points(), MapProjection.Identity, cancellationToken: token));
        Assert.Throws<OperationCanceledException>(() => ChartMaps.Heatmap(Points(), MapProjection.Identity, cancellationToken: token));
        Assert.Throws<OperationCanceledException>(() => ChartMaps.ReadGeoJson("not-json", cancellationToken: token));
        Assert.Equal(0, reads);
    }
}

