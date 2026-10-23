namespace NT.AvaloniaControl.Charts;

public sealed record MapProjectedPoint(MapObservationEntry Entry, ChartPoint Position);
public sealed class MapPointLayout
{
    internal MapPointLayout(MapProjectedPoint[] points) => Points = Array.AsReadOnly(points);
    public IReadOnlyList<MapProjectedPoint> Points { get; }
}
public sealed record MapConnection(object Id, MapCoordinate Start, MapCoordinate End, double Value = 1, object? Source = null, string? Label = null);
public sealed class MapConnectionEntry
{
    internal MapConnectionEntry(int index, MapConnection connection) { SourceIndex = index; Connection = connection; }
    public int SourceIndex { get; }
    public MapConnection Connection { get; }
    public object Id => Connection.Id;
    public object Source => Connection.Source ?? Connection;
}
public sealed record MapLineSegment(ChartPoint Start, ChartPoint End);
public sealed record MapProjectedConnection(MapConnectionEntry Entry, IReadOnlyList<MapLineSegment> Segments);
public sealed class MapConnectionLayout
{
    internal MapConnectionLayout(MapProjectedConnection[] connections) => Connections = Array.AsReadOnly(connections);
    public IReadOnlyList<MapProjectedConnection> Connections { get; }
}

/// <summary>A typed vector overlay. Radius and stroke widths are screen DIPs, not geographic or geodesic distances.</summary>
public abstract record MapVector(object Id, object? Source = null, string? Label = null);
public sealed record MapLineVector(object Id, MapCoordinate Start, MapCoordinate End, object? Source = null, string? Label = null) : MapVector(Id, Source, Label);
public sealed record MapPolylineVector(object Id, IReadOnlyList<MapCoordinate> Points, object? Source = null, string? Label = null) : MapVector(Id, Source, Label);
public sealed record MapPolygonVector(object Id, MapPolygon Polygon, object? Source = null, string? Label = null) : MapVector(Id, Source, Label);
public sealed record MapCircleVector(object Id, MapCoordinate Center, double Radius, object? Source = null, string? Label = null) : MapVector(Id, Source, Label);
public sealed record MapArcVector(object Id, MapCoordinate Center, double Radius, double StartAngle, double SweepAngle, object? Source = null, string? Label = null) : MapVector(Id, Source, Label);
public sealed class MapVectorEntry
{
    internal MapVectorEntry(int index, MapVector vector) { SourceIndex = index; Vector = vector; }
    public int SourceIndex { get; }
    public MapVector Vector { get; }
    public object Id => Vector.Id;
    public object Source => Vector.Source ?? Vector;
}
public sealed record MapProjectedVector(MapVectorEntry Entry, IReadOnlyList<MapLineSegment> Segments,
    IReadOnlyList<MapProjectedPolygon> Polygons, ChartPoint? Center);
public sealed class MapVectorLayout
{
    internal MapVectorLayout(MapProjectedVector[] vectors) => Vectors = Array.AsReadOnly(vectors);
    public IReadOnlyList<MapProjectedVector> Vectors { get; }
}
public sealed record MapPieSlice(double Value, string? Label = null, object? Source = null);
public sealed record MapPie(object Id, MapCoordinate Position, IReadOnlyList<MapPieSlice> Slices, object? Source = null, string? Label = null);
public sealed class MapPieEntry
{
    internal MapPieEntry(int index, MapPie pie) { SourceIndex = index; Pie = pie; }
    public int SourceIndex { get; }
    public MapPie Pie { get; }
    public object Id => Pie.Id;
    public object Source => Pie.Source ?? Pie;
}
public sealed record MapProjectedPieSlice(MapPieEntry Entry, int SliceIndex, MapPieSlice Slice, ChartPoint Position, double StartAngle, double SweepAngle);
public sealed class MapPieLayout
{
    internal MapPieLayout(MapProjectedPieSlice[] slices) => Slices = Array.AsReadOnly(slices);
    public IReadOnlyList<MapProjectedPieSlice> Slices { get; }
}

public static partial class ChartMaps
{
    public const int MaximumVectorCount = 2000;
    public const int MaximumPieSlices = 20_000;
    /// <summary>Map-wide conservative logical primitive admission units; not a byte estimate or performance guarantee.</summary>
    public const int MaximumOverlayPrimitives = 200_000;

    /// <summary>Projects fixed-size markers. Business values may be any finite number and never determine marker size.</summary>
    public static MapPointLayout Markers(IEnumerable<MapObservation> observations, MapProjection projection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observations); CheckProjection(projection); cancellationToken.ThrowIfCancellationRequested(); var ids = new HashSet<object>(); var result = new List<MapProjectedPoint>();
        foreach (var point in observations)
        {
            cancellationToken.ThrowIfCancellationRequested(); ArgumentNullException.ThrowIfNull(point); ArgumentNullException.ThrowIfNull(point.Id);
            if (result.Count >= MaximumObservationCount) throw new ArgumentOutOfRangeException(nameof(observations));
            if (!ids.Add(point.Id)) throw new ArgumentException("Map marker IDs must be unique.", nameof(observations));
            if (!double.IsFinite(point.Value)) throw new ArgumentException("Marker business values must be finite.", nameof(observations));
            result.Add(new(new(result.Count, point), ProjectCoordinate(point.Position, projection)));
        }
        return new(result.ToArray());
    }

    /// <summary>Projects straight connections, splitting shortest longitude arcs at the date line without creating additional observations.</summary>
    public static MapConnectionLayout Connections(IEnumerable<MapConnection> connections, MapProjection projection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connections); CheckProjection(projection); cancellationToken.ThrowIfCancellationRequested(); var ids = new HashSet<object>(); var result = new List<MapProjectedConnection>();
        foreach (var connection in connections)
        {
            cancellationToken.ThrowIfCancellationRequested(); ArgumentNullException.ThrowIfNull(connection); ArgumentNullException.ThrowIfNull(connection.Id);
            if (result.Count >= MaximumObservationCount) throw new ArgumentOutOfRangeException(nameof(connections));
            if (!ids.Add(connection.Id)) throw new ArgumentException("Map connection IDs must be unique.", nameof(connections));
            if (!double.IsFinite(connection.Value) || connection.Value < 0) throw new ArgumentException("Connection widths require finite nonnegative values.", nameof(connections));
            result.Add(new(new(result.Count, connection), Array.AsReadOnly(SplitConnection(connection.Start, connection.End, projection))));
        }
        return new(result.ToArray());
    }

    private static MapLineSegment[] SplitConnection(MapCoordinate start, MapCoordinate end, MapProjection projection)
    {
        var a = ProjectCoordinate(start, projection); var b = ProjectCoordinate(end, projection);
        if (projection == MapProjection.Identity || Math.Abs(b.X - a.X) <= .5) return [new(a, b)];
        var unwrapped = b.X + (b.X < a.X ? 1 : -1); if (unwrapped == a.X) return [new(a, new(a.X, b.Y))]; var boundary = b.X < a.X ? 1d : 0d;
        var fraction = (boundary - a.X) / (unwrapped - a.X); var y = a.Y * (1 - fraction) + b.Y * fraction;
        return [new(a, new(boundary, y)), new(new(boundary == 1 ? 0 : 1, y), b)];
    }

    /// <summary>Projects five explicitly typed vector shapes. Circle/arc radii remain screen DIPs. Polygon validation uses the same strict map topology.</summary>
    public static MapVectorLayout Vectors(IEnumerable<MapVector> vectors, MapProjection projection,
        MapTopologyRule topologyRule = MapTopologyRule.StrictSimple, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vectors); CheckProjection(projection); if (!Enum.IsDefined(topologyRule)) throw new ArgumentOutOfRangeException(nameof(topologyRule)); cancellationToken.ThrowIfCancellationRequested(); var ids = new HashSet<object>(); var result = new List<MapProjectedVector>(); long vertices = 0;
        foreach (var vector in vectors)
        {
            cancellationToken.ThrowIfCancellationRequested(); ArgumentNullException.ThrowIfNull(vector); ArgumentNullException.ThrowIfNull(vector.Id);
            if (result.Count >= MaximumVectorCount) throw new ArgumentOutOfRangeException(nameof(vectors));
            if (!ids.Add(vector.Id)) throw new ArgumentException("Vector IDs must be unique.", nameof(vectors));
            var entry = new MapVectorEntry(result.Count, vector); var segments = new List<MapLineSegment>(); IReadOnlyList<MapProjectedPolygon> polygons = []; ChartPoint? center = null;
            switch (vector)
            {
                case MapLineVector line: vertices += 2; segments.AddRange(SplitConnection(line.Start, line.End, projection)); break;
                case MapPolylineVector polyline:
                    ArgumentNullException.ThrowIfNull(polyline.Points); if (polyline.Points.Count < 2) throw new ArgumentException("A polyline needs two points.", nameof(vectors));
                    var ownedPoints = new List<MapCoordinate>();
                    foreach (var point in polyline.Points) { cancellationToken.ThrowIfCancellationRequested(); if (++vertices > MaximumVertexCount) throw new ArgumentOutOfRangeException(nameof(vectors)); ownedPoints.Add(point); }
                    if (ownedPoints.Count < 2) throw new ArgumentException("A polyline needs two points.", nameof(vectors));
                    for (var i = 1; i < ownedPoints.Count; i++) { cancellationToken.ThrowIfCancellationRequested(); segments.AddRange(SplitConnection(ownedPoints[i - 1], ownedPoints[i], projection)); }
                    break;
                case MapPolygonVector polygon:
                    ArgumentNullException.ThrowIfNull(polygon.Polygon); vertices += polygon.Polygon.Outer.Count + polygon.Polygon.Holes.Sum(h => h.Count);
                    if (vertices > MaximumVertexCount) throw new ArgumentOutOfRangeException(nameof(vectors));
                    polygons = Project(Snapshot([new MapFeature(vector.Id, [polygon.Polygon])], cancellationToken), projection, cancellationToken, topologyRule).Features[0].Polygons; break;
                case MapCircleVector circle:
                    CheckRadius(circle.Radius); center = ProjectCoordinate(circle.Center, projection); vertices++; break;
                case MapArcVector arc:
                    CheckRadius(arc.Radius); if (!double.IsFinite(arc.StartAngle) || !double.IsFinite(arc.SweepAngle) || arc.SweepAngle == 0 || Math.Abs(arc.SweepAngle) > 360) throw new ArgumentException("Arc angles must be finite and its sweep nonzero and at most 360 degrees.", nameof(vectors));
                    center = ProjectCoordinate(arc.Center, projection); vertices += 129; break;
                default: throw new ArgumentException("Unsupported vector shape.", nameof(vectors));
            }
            if (vertices > MaximumVertexCount) throw new ArgumentOutOfRangeException(nameof(vectors), "Vector vertex budget exceeded.");
            result.Add(new(entry, Array.AsReadOnly(segments.ToArray()), polygons, center));
        }
        return new(result.ToArray());
        static void CheckRadius(double radius) { if (!double.IsFinite(radius) || radius <= 0 || radius > 4096) throw new ArgumentOutOfRangeException(nameof(radius)); }
    }

    /// <summary>Each location independently normalizes its nonnegative slices. Zero slices remain absent from drawing and do not create source observations.</summary>
    public static MapPieLayout Pies(IEnumerable<MapPie> pies, MapProjection projection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pies); CheckProjection(projection); cancellationToken.ThrowIfCancellationRequested(); var ids = new HashSet<object>(); var result = new List<MapProjectedPieSlice>(); var count = 0; var sliceCount = 0;
        foreach (var pie in pies)
        {
            cancellationToken.ThrowIfCancellationRequested(); ArgumentNullException.ThrowIfNull(pie); ArgumentNullException.ThrowIfNull(pie.Id); ArgumentNullException.ThrowIfNull(pie.Slices);
            if (count >= MaximumObservationCount || pie.Slices.Count > MaximumPieSlices - sliceCount) throw new ArgumentOutOfRangeException(nameof(pies));
            var ownedSlices = new List<MapPieSlice>(); foreach (var slice in pie.Slices) { cancellationToken.ThrowIfCancellationRequested(); if (++sliceCount > MaximumPieSlices) throw new ArgumentOutOfRangeException(nameof(pies)); ownedSlices.Add(slice); }
            if (!ids.Add(pie.Id)) throw new ArgumentException("Map pie IDs must be unique.", nameof(pies));
            var position = ProjectCoordinate(pie.Position, projection); var entry = new MapPieEntry(count++, pie); double total = 0;
            foreach (var slice in ownedSlices) { cancellationToken.ThrowIfCancellationRequested(); ArgumentNullException.ThrowIfNull(slice); if (!double.IsFinite(slice.Value) || slice.Value < 0) throw new ArgumentException("Pie slice values must be finite and nonnegative.", nameof(pies)); total += slice.Value; if (!double.IsFinite(total)) throw new ArgumentOutOfRangeException(nameof(pies)); }
            var angle = -90d; var lastPositive = -1; for (var i = 0; i < ownedSlices.Count; i++) if (ownedSlices[i].Value > 0) lastPositive = i;
            for (var i = 0; i < ownedSlices.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested(); var slice = ownedSlices[i]; if (slice.Value == 0) continue;
                var sweep = i == lastPositive ? 270 - angle : 360 * (slice.Value / total); if (sweep <= 0) throw new ArgumentOutOfRangeException(nameof(pies), "Positive pie slice angle is not representable.");
                result.Add(new(entry, i, slice, position, angle, sweep)); angle += sweep;
            }
        }
        return new(result.ToArray());
    }
}
