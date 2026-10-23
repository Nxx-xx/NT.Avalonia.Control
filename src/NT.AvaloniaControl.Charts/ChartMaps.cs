namespace NT.AvaloniaControl.Charts;

public enum MapProjection { Equirectangular, WebMercator, Identity }
/// <summary>StrictSimple rejects self crossings/touches. EvenOdd retains them and exposes their count;
/// it still rejects zero signed-area rings, including balanced bow ties, and invalid hole relationships.</summary>
public enum MapTopologyRule { StrictSimple, EvenOdd }

/// <summary>Longitude/latitude in degrees for geographic projections; planar X/Y for Identity.</summary>
public readonly record struct MapCoordinate(double X, double Y);

/// <summary>Finite endpoint bounds. Normalize and Interpolate also support opposite Double extrema.</summary>
public readonly record struct MapBounds(double MinX, double MinY, double MaxX, double MaxY)
{
    public double MinimumX => MinX;
    public double MinimumY => MinY;
    public double MaximumX => MaxX;
    public double MaximumY => MaxY;
    /// <summary>Diagnostic span, saturated if the mathematical span exceeds Double.MaxValue.</summary>
    public double Width => Span(MinX, MaxX);
    public double Height => Span(MinY, MaxY);
    public double AspectRatio
    {
        get
        {
            var scale = Math.Max(Math.Max(Math.Abs(MinX), Math.Abs(MaxX)), Math.Max(Math.Abs(MinY), Math.Abs(MaxY)));
            if (scale == 0) return 1;
            var x = MaxX / scale - MinX / scale; var y = MaxY / scale - MinY / scale;
            return x == 0 ? 0 : y == 0 ? double.MaxValue : Math.Min(double.MaxValue, x / y);
        }
    }
    public ChartPoint Normalize(ChartPoint point) => new(NormalizeAxis(point.X, MinX, MaxX), NormalizeAxis(point.Y, MinY, MaxY));
    public ChartPoint Interpolate(double x, double y) => new(Blend(MinX, MaxX, x), Blend(MinY, MaxY, y));
    private static double Span(double min, double max) => double.IsPositiveInfinity(max - min) ? double.MaxValue : max - min;
    private static double NormalizeAxis(double value, double min, double max)
    {
        if (min == max) return .5;
        var scale = Math.Max(Math.Abs(min), Math.Abs(max));
        var result = (value / scale - min / scale) / (max / scale - min / scale);
        return result;
    }
    private static double Blend(double min, double max, double t)
    {
        if (!double.IsFinite(t) || t < 0 || t > 1) throw new ArgumentOutOfRangeException(nameof(t));
        if (t == 0) return min; if (t == 1) return max;
        return min * (1 - t) + max * t;
    }
}

/// <summary>An owned closed outer ring with zero or more owned closed holes. Snapshot validates input budgets;
/// Project validates topology after applying the projection's longitude convention.</summary>
public sealed class MapPolygon
{
    public MapPolygon(IEnumerable<MapCoordinate> outer, IEnumerable<IEnumerable<MapCoordinate>>? holes = null)
    {
        ArgumentNullException.ThrowIfNull(outer);
        Outer = Array.AsReadOnly(ChartMaps.OwnRing(outer));
        var rings = new List<IReadOnlyList<MapCoordinate>>(); var vertices = Outer.Count;
        if (holes is not null) foreach (var hole in holes)
        {
            if (rings.Count >= ChartMaps.MaximumRingCount) throw new ArgumentOutOfRangeException(nameof(holes), "Map ring budget exceeded.");
            var owned = ChartMaps.OwnRing(hole); vertices = checked(vertices + owned.Length);
            if (vertices > ChartMaps.MaximumVertexCount) throw new ArgumentOutOfRangeException(nameof(holes), "Map vertex budget exceeded.");
            rings.Add(Array.AsReadOnly(owned));
        }
        Holes = rings.AsReadOnly();
    }
    public IReadOnlyList<MapCoordinate> Outer { get; }
    public IReadOnlyList<IReadOnlyList<MapCoordinate>> Holes { get; }
}

public sealed class MapFeature
{
    public MapFeature(object id, IEnumerable<MapPolygon> polygons, string? label = null, object? source = null)
    {
        ArgumentNullException.ThrowIfNull(polygons);
        var owned = new List<MapPolygon>(); var count = 0;
        foreach (var polygon in polygons)
        {
            ArgumentNullException.ThrowIfNull(polygon);
            if (owned.Count >= ChartMaps.MaximumRingCount) throw new ArgumentOutOfRangeException(nameof(polygons));
            count = checked(count + polygon.Outer.Count + polygon.Holes.Sum(r => r.Count));
            if (count > ChartMaps.MaximumVertexCount) throw new ArgumentOutOfRangeException(nameof(polygons), "Map vertex budget exceeded.");
            owned.Add(polygon);
        }
        Id = id; Polygons = owned.AsReadOnly(); Label = label; Source = source;
    }
    public object Id { get; }
    public IReadOnlyList<MapPolygon> Polygons { get; }
    public string? Label { get; }
    public object? Source { get; }
}

public sealed class MapFeatureEntry
{
    internal MapFeatureEntry(int index, MapFeature feature) { Index = index; Feature = feature; }
    public int Index { get; }
    public int SourceIndex => Index;
    public MapFeature Feature { get; }
    public object Id => Feature.Id;
    public string? Label => Feature.Label;
    public object Source => Feature.Source ?? Feature;
}

public sealed class MapGeometrySnapshot
{
    internal MapGeometrySnapshot(MapFeatureEntry[] features, int vertices) { Features = Array.AsReadOnly(features); VertexCount = vertices; }
    public IReadOnlyList<MapFeatureEntry> Features { get; }
    public int VertexCount { get; }
}

public sealed class MapProjectedPolygon
{
    internal MapProjectedPolygon(ChartPoint[] outer, IReadOnlyList<ChartPoint>[] holes)
    { Outer = Array.AsReadOnly(outer); Holes = Array.AsReadOnly(holes); }
    public IReadOnlyList<ChartPoint> Outer { get; }
    public IReadOnlyList<IReadOnlyList<ChartPoint>> Holes { get; }
}

public sealed class MapProjectedFeature
{
    internal MapProjectedFeature(MapFeatureEntry entry, MapProjectedPolygon[] polygons, MapBounds bounds)
    { Entry = entry; Polygons = Array.AsReadOnly(polygons); Bounds = bounds; }
    public MapFeatureEntry Entry { get; }
    public IReadOnlyList<MapProjectedPolygon> Polygons { get; }
    public MapBounds Bounds { get; }
}

public sealed class MapProjectedGeometry
{
    internal MapProjectedGeometry(MapProjectedFeature[] features, MapBounds bounds, int clamped, int selfIntersections = 0)
    { Features = Array.AsReadOnly(features); Bounds = bounds; ClampedLatitudeCount = clamped; SelfIntersectionCount = selfIntersections; }
    public IReadOnlyList<MapProjectedFeature> Features { get; }
    public MapBounds Bounds { get; }
    public int ClampedLatitudeCount { get; }
    /// <summary>Number of nonadjacent segment intersections in source rings after longitude unwrapping,
    /// before Mercator clamping. Nonzero values are accepted only with the explicit EvenOdd rule.</summary>
    public int SelfIntersectionCount { get; }
    /// <summary>Retained original features whose legal polar geometry collapses entirely at the Mercator latitude limit.</summary>
    public int CollapsedFeatureCount => Features.Count(feature => feature.Polygons.Count == 0);
}

/// <summary>Bounded geographic algorithms. Projected geographic Y increases northward.
/// Geographic ring edges use the shortest longitude arc, including dateline clipping; use pre-cut
/// geometry to represent intentional edges spanning more than 180 degrees. Identity has no wrapping.</summary>
public static partial class ChartMaps
{
    public const int MaximumFeatureCount = 5_000;
    public const int MaximumVertexCount = 200_000;
    public const int MaximumRingCount = 20_000;
    public const int MaximumRingVertexCount = 4_096;
    public const int MaximumJsonBytes = 16 * 1_024 * 1_024;
    public const int MaximumJsonDepth = 32;
    public const int MaximumObservationCount = 20_000;
    public const int MaximumGridDimension = 256;
    public const int MaximumKernelRadius = 8;
    public const long MaximumWork = 8_000_000;
    /// <summary>Conservative sum of point-kernel contributions retained by all visible heatmap layers in one map. Zero weights are included in the estimate.</summary>
    public const long MaximumRetainedHeatmapContributions = 1_000_000;
    public const double MercatorLatitudeLimit = 85.0511287798066;

    internal static MapCoordinate[] OwnRing(IEnumerable<MapCoordinate> ring)
    {
        ArgumentNullException.ThrowIfNull(ring); var points = new List<MapCoordinate>();
        foreach (var point in ring)
        {
            if (points.Count >= MaximumRingVertexCount) throw new ArgumentOutOfRangeException(nameof(ring), "Map ring vertex budget exceeded.");
            if (!double.IsFinite(point.X) || !double.IsFinite(point.Y)) throw new ArgumentOutOfRangeException(nameof(ring), "Map coordinates must be finite.");
            points.Add(point);
        }
        if (points.Count < 4 || points[0] != points[^1]) throw new ArgumentException("Map rings require at least four coordinates and exact closure.", nameof(ring));
        return points.ToArray();
    }

    public static MapGeometrySnapshot Snapshot(IEnumerable<MapFeature> features, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(features); cancellationToken.ThrowIfCancellationRequested();
        var owned = new List<MapFeatureEntry>(); var ids = new HashSet<object>(); var vertices = 0; var rings = 0;
        foreach (var feature in features)
        {
            cancellationToken.ThrowIfCancellationRequested(); ArgumentNullException.ThrowIfNull(feature);
            if (owned.Count >= MaximumFeatureCount) throw new ArgumentOutOfRangeException(nameof(features), "Map feature budget exceeded.");
            ArgumentNullException.ThrowIfNull(feature.Id);
            if (!ids.Add(feature.Id)) throw new ArgumentException("Map feature IDs must be unique.", nameof(features));
            if (feature.Polygons.Count == 0) throw new ArgumentException("Map features require at least one polygon.", nameof(features));
            foreach (var polygon in feature.Polygons)
            {
                foreach (var ring in new[] { polygon.Outer }.Concat(polygon.Holes))
                {
                    vertices = checked(vertices + ring.Count); rings++;
                    if (vertices > MaximumVertexCount || rings > MaximumRingCount) throw new ArgumentOutOfRangeException(nameof(features), "Map geometry budget exceeded.");
                    // Topology depends on the projection's explicit longitude edge convention.
                    // Validate it after unwrapping in Project, rather than rejecting valid dateline rings here.
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            owned.Add(new(owned.Count, feature));
        }
        return new(owned.ToArray(), vertices);
    }

    private static void CheckProjection(MapProjection projection)
    { if (!Enum.IsDefined(projection)) throw new ArgumentOutOfRangeException(nameof(projection)); }
    private static void CheckCoordinate(MapCoordinate point, MapProjection projection)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y)) throw new ArgumentOutOfRangeException(nameof(point));
        if (projection != MapProjection.Identity && (point.X < -180 || point.X > 180 || point.Y < -90 || point.Y > 90))
            throw new ArgumentOutOfRangeException(nameof(point), "Longitude must be in [-180,180] and latitude in [-90,90].");
    }
    public static ChartPoint ProjectCoordinate(MapCoordinate point, MapProjection projection)
    {
        CheckProjection(projection); CheckCoordinate(point, projection);
        return ProjectUnchecked(point, projection);
    }
    private static ChartPoint ProjectUnchecked(MapCoordinate point, MapProjection projection)
    {
        if (projection == MapProjection.Identity) return new(point.X, point.Y);
        if (projection == MapProjection.Equirectangular) return new((point.X + 180) / 360, (point.Y + 90) / 180);
        var latitude = Math.Clamp(point.Y, -MercatorLatitudeLimit, MercatorLatitudeLimit) * Math.PI / 180;
        return new((point.X + 180) / 360, Math.Clamp(.5 + Math.Log(Math.Tan(Math.PI / 4 + latitude / 2)) / (2 * Math.PI), 0, 1));
    }
    internal static MapBounds BoundsOf(IEnumerable<ChartPoint> points)
    {
        using var cursor = points.GetEnumerator(); if (!cursor.MoveNext()) return new(0, 0, 1, 1);
        var x0 = cursor.Current.X; var x1 = x0; var y0 = cursor.Current.Y; var y1 = y0;
        while (cursor.MoveNext()) { x0 = Math.Min(x0, cursor.Current.X); x1 = Math.Max(x1, cursor.Current.X); y0 = Math.Min(y0, cursor.Current.Y); y1 = Math.Max(y1, cursor.Current.Y); }
        return new(x0, y0, x1, y1);
    }
}
