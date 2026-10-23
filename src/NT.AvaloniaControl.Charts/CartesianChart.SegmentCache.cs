using Avalonia;

namespace NT.AvaloniaControl.Charts;

public sealed partial class CartesianChart
{
    private sealed record SegmentGeometry(Point[]? Vertices, IReadOnlyList<ChartLinePathFigure>? Figures, SegmentGap[] Gaps,
        ChartSegmentIndex? Index = null, Dictionary<int, Point>? ProjectedVertices = null,
        Queue<int>? ProjectionKeys = null, long BaseBytes = 512);
    private const int MaximumSegmentProjectionEntries = 4096;
    private const int SegmentProjectionEntryBytes = 80;
    private ChartScene? _segmentCacheScene;
    private readonly Dictionary<SeriesSnapshot, SegmentGeometry> _segmentCaches = [];
    private long _segmentCacheBuildCount;
    private long _segmentProjectedVertexCount;
    private long _segmentMemoEvictionCount;

    private void ReleaseSegmentCaches()
    {
        ReleaseResourceKind(ResourceCacheKind.SegmentGeometry);
        _segmentCacheScene = null;
        _segmentCaches.Clear();
    }

    private SegmentGeometry GetSegmentGeometry(ChartScene scene, SeriesSnapshot snapshot, bool linear)
    {
        var canRetain = _attached && ResourceCacheBudgetBytes > 0 && snapshot.Context!.Projection is CartesianProjection { HasStableTransform: true };
        if (!ReferenceEquals(_segmentCacheScene, scene))
        {
            ReleaseSegmentCaches();
            if (_attached) _segmentCacheScene = scene;
        }
        if (canRetain && _segmentCaches.TryGetValue(snapshot, out var cached))
        { TouchResource(ResourceCacheKind.SegmentGeometry, snapshot); return cached; }
        var gaps = BreakRectangles(scene.Plot, scene.Scales[snapshot.XAxis], true)
            .Select(rect => new SegmentGap(true, rect.Left, rect.Right))
            .Concat(BreakRectangles(scene.Plot, scene.Scales[snapshot.YAxis], false)
                .Select(rect => new SegmentGap(false, rect.Top, rect.Bottom))).ToArray();
        Point[]? vertices = null;
        IReadOnlyList<ChartLinePathFigure>? figures = null;
        ChartSegmentIndex? index = null;
        Dictionary<int, Point>? projected = null;
        if (linear && snapshot.Context!.Projection is CartesianProjection { HasStableTransform: true })
        {
            index = ChartSegmentIndex.For(snapshot.Data);
            projected = [];
        }
        else if (linear)
        {
            vertices = new Point[snapshot.Data.Count];
            var explicitGaps = snapshot.Series is ScatterLineSeries;
            for (var ordinal = 0; ordinal < vertices.Length; ordinal++)
            {
                var value = snapshot.Data.GetValue(ordinal);
                vertices[ordinal] = (!explicitGaps || !value.IsEmpty) && IsValid(value.Point)
                    ? snapshot.Context!.Projection.Project(value.Point) : new(double.NaN, double.NaN);
            }
        }
        else figures = ChartLineGeometry.Build(snapshot.Context!);
        _segmentCacheBuildCount++;
        var bytes = 512L + gaps.Length * 32L + (vertices?.LongLength ?? 0) * 16L;
        if (figures is not null)
            foreach (var figure in figures) bytes += 128L + (figure.Upper.Count + (long)figure.Lower.Count) * 192;
        var result = new SegmentGeometry(vertices, figures, gaps, index, projected, projected is null ? null : new(), bytes);
        if (canRetain && ReferenceEquals(_scene, scene) &&
            TryRetainResource(ResourceCacheKind.SegmentGeometry, snapshot, bytes, () =>
            {
                _segmentCaches.Remove(snapshot); result.ProjectedVertices?.Clear(); result.ProjectionKeys?.Clear();
            })) _segmentCaches[snapshot] = result;
        return result;
    }

    private Point SegmentVertex(SeriesSnapshot snapshot, SegmentGeometry geometry, int ordinal)
    {
        if (geometry.Vertices is { } full) return full[ordinal];
        if (geometry.ProjectedVertices!.TryGetValue(ordinal, out var cached))
        { TouchResource(ResourceCacheKind.SegmentGeometry, snapshot); return cached; }
        var value = snapshot.Data.GetValue(ordinal);
        var point = (snapshot.Series is not ScatterLineSeries || !value.IsEmpty) && IsValid(value.Point)
            ? snapshot.Context!.Projection.Project(value.Point) : new(double.NaN, double.NaN);
        _segmentProjectedVertexCount++;
        var count = Math.Min(MaximumSegmentProjectionEntries, geometry.ProjectedVertices.Count + 1);
        if (TryResizeResource(ResourceCacheKind.SegmentGeometry, snapshot, geometry.BaseBytes + (long)count * SegmentProjectionEntryBytes))
        {
            if (geometry.ProjectedVertices.Count == MaximumSegmentProjectionEntries)
            { geometry.ProjectedVertices.Remove(geometry.ProjectionKeys!.Dequeue()); _segmentMemoEvictionCount++; }
            geometry.ProjectedVertices.Add(ordinal, point); geometry.ProjectionKeys!.Enqueue(ordinal);
        }
        return point;
    }
}
