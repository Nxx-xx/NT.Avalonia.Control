using Avalonia;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private static readonly string[] MapKinds = ["shape", "choropleth", "bubble", "heatmap", "seat"];
    private readonly List<object> _mapProbes = [];
    private readonly List<object> _mapAsyncEvidence = [];
    private static object MapRawValue(MapDemoValue value) => new { id = value.Id, regionId = value.RegionId, label = value.Label, value = value.Value, fill = value.Fill };
    private static object MapRawObservation(MapDemoObservation value) => new { id = value.Id, longitude = value.Longitude, latitude = value.Latitude, label = value.Label, value = value.Value };
    private static object? MapRawSource(object? source) => source switch { MapDemoValue value => MapRawValue(value), MapDemoObservation value => MapRawObservation(value), _ => null };
    private static object MapBoundsProbe(MapBounds bounds) => new { minX = bounds.MinX, minY = bounds.MinY, maxX = bounds.MaxX, maxY = bounds.MaxY };
    private static object MapPointProbe(ChartPoint point) => new { x = point.X, y = point.Y };
    private static object MapRectProbe(Rect bounds) => new { x = bounds.X, y = bounds.Y, width = bounds.Width, height = bounds.Height };
    private static object MapSnapshotProbe(MapGeometrySnapshot snapshot) => new
    {
        vertexCount = snapshot.VertexCount,
        features = snapshot.Features.Select(feature => new { index = feature.Index, sourceIndex = feature.SourceIndex, id = feature.Id, label = feature.Label,
            polygons = feature.Feature.Polygons.Select(polygon => new { outer = polygon.Outer.Select(point => new { x = point.X, y = point.Y }).ToArray(),
                holes = polygon.Holes.Select(ring => ring.Select(point => new { x = point.X, y = point.Y }).ToArray()).ToArray() }).ToArray() }).ToArray(),
    };
    private static object MapProjectedProbe(MapProjectedGeometry geometry) => new
    {
        bounds = MapBoundsProbe(geometry.Bounds), clampedLatitudeCount = geometry.ClampedLatitudeCount, collapsedFeatureCount = geometry.CollapsedFeatureCount, selfIntersectionCount = geometry.SelfIntersectionCount,
        features = geometry.Features.Select(feature => new { entryIndex = feature.Entry.Index, id = feature.Entry.Id, bounds = MapBoundsProbe(feature.Bounds),
            polygons = feature.Polygons.Select(polygon => new { outer = polygon.Outer.Select(MapPointProbe).ToArray(), holes = polygon.Holes.Select(ring => ring.Select(MapPointProbe).ToArray()).ToArray() }).ToArray() }).ToArray(),
    };
    private static object MapValueProbe(MapValueEntry entry) => new { index = entry.Index, sourceIndex = entry.SourceIndex, regionId = entry.RegionId, value = entry.Value, source = MapRawSource(entry.Source) };
    private static object MapObservationProbe(MapObservationEntry entry) => new { index = entry.Index, sourceIndex = entry.SourceIndex, id = entry.Id, value = entry.Value, label = entry.Label,
        position = new { x = entry.Observation.Position.X, y = entry.Observation.Position.Y }, source = MapRawSource(entry.Source) };
    private static object MapLayerProbe(MapLayerSnapshot snapshot, int index) => new
    {
        index, kind = snapshot.Layer.GetType().Name,
        config = new { isVisible = snapshot.Layer.IsVisible, opacity = snapshot.Layer.Opacity, fill = snapshot.Layer.Fill?.ToString(), stroke = snapshot.Layer.Stroke?.ToString(),
            minimumRadius = (snapshot.Layer as BubbleLayer)?.MinimumRadius, maximumRadius = (snapshot.Layer as BubbleLayer)?.MaximumRadius,
            columns = (snapshot.Layer as HeatmapLayer)?.Columns, rows = (snapshot.Layer as HeatmapLayer)?.Rows, radius = (snapshot.Layer as HeatmapLayer)?.Radius,
            minimumColor = snapshot.Layer is ShapeLayer shape ? shape.MinimumColor.ToString() : (snapshot.Layer as HeatmapLayer)?.MinimumColor.ToString(),
            maximumColor = snapshot.Layer is ShapeLayer shape2 ? shape2.MaximumColor.ToString() : (snapshot.Layer as HeatmapLayer)?.MaximumColor.ToString(),
            noDataBrush = (snapshot.Layer as ShapeLayer)?.NoDataBrush?.ToString() },
        regions = snapshot.Regions is { } join ? new { noDataCount = join.NoDataCount, unknownValues = join.UnknownValues.Select(MapValueProbe).ToArray(),
            regions = join.Regions.Select(region => new { featureIndex = region.Feature.Index, featureId = region.Feature.Id, value = region.Value, isNoData = region.IsNoData,
                data = region.Data is { } value ? MapValueProbe(value) : null }).ToArray() } : null,
        bubbles = snapshot.Bubbles is { } bubbles ? new { bounds = MapBoundsProbe(bubbles.Bounds), clampedLatitudeCount = bubbles.ClampedLatitudeCount,
            points = bubbles.Points.Select(point => new { entry = MapObservationProbe(point.Entry), position = MapPointProbe(point.Position), radius = point.Radius }).ToArray() } : null,
        heatmap = snapshot.Heatmap is { } heat ? new { bounds = MapBoundsProbe(heat.Bounds), columns = heat.Columns, rows = heat.Rows, clampedLatitudeCount = heat.ClampedLatitudeCount,
            cells = heat.Cells.Select(cell => new { column = cell.Column, row = cell.Row, bounds = MapBoundsProbe(cell.Bounds), value = cell.Value, members = cell.Members.Select(MapObservationProbe).ToArray() }).ToArray() } : null,
    };
    private static object MapMarkProbe(ShapeMap chart, EngineeringMarkInfo mark, int index) => new
    {
        index, key = mark.Key is MapGlyphKey key ? new { kind = key.Kind.ToString(), layerIndex = chart.Layers.IndexOf(key.Layer), identity = key.Identity.ToString(), hasExplicitId = key.HasExplicitId } : null,
        value = mark.Value, position = new { x = mark.Position.X, y = mark.Position.Y }, bounds = MapRectProbe(mark.Bounds),
        members = mark.Members.Select(member => new { sourceIndex = member.SourceIndex, stableId = member.StableId, value = member.Value, weight = member.Weight,
            point = new { x = member.Point.X, y = member.Point.Y }, item = MapRawSource(member.Item) }).ToArray(),
    };
    private static object? MapHitProbe(ShapeMap chart, EngineeringPointHit? hit) => hit is null ? null : MapMarkProbe(chart, hit.Mark, hit.Index);
    private void AddMapProbe(string kind, string theme, string phase, ShapeMap chart, MapDemoValue[] values, MapDemoObservation[] observations,
        object? results = null, string? nativeImage = null, string? exportImage = null, string[]? dpiImages = null)
    {
        var marks = chart.GetMarks(); var viewport = chart.MapViewport;
        _mapProbes.Add(new { kind, theme, phase, widthDip = chart.Bounds.Width, heightDip = chart.Bounds.Height, rawGeoJson = MapFixtureJson,
            rawValues = values.Select(MapRawValue).ToArray(), rawObservations = observations.Select(MapRawObservation).ToArray(),
            config = new { projection = chart.EffectiveProjection.ToString(), topologyRule = chart.TopologyRule.ToString(), isGeographic = chart.IsGeographic, zoom = chart.Zoom, panX = chart.PanX, panY = chart.PanY, showLabels = chart.ShowLabels, showLegend = chart.ShowLegend },
            plot = MapRectProbe(chart.PlotArea), snapshot = chart.Snapshot is { } snapshot ? MapSnapshotProbe(snapshot) : null,
            projected = chart.ProjectedGeometry is { } projected ? MapProjectedProbe(projected) : null,
            viewport = viewport is null ? null : new { bounds = MapBoundsProbe(viewport.Bounds), fittedArea = MapRectProbe(viewport.FittedArea), zoom = viewport.Zoom, panX = viewport.PanX, panY = viewport.PanY },
            layers = chart.LayerSnapshots.Select(MapLayerProbe).ToArray(), marks = marks.Select((mark, index) => MapMarkProbe(chart, mark, index)).ToArray(),
            results, nativeImage, exportImage, dpiImages = dpiImages ?? [], physicalInputValidated = false, performanceMeasured = false });
    }
}
