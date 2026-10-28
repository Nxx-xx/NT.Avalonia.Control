using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

public sealed partial class CartesianChart
{
    private ChartScene? _seriesDrawingScene;
    private readonly Dictionary<SeriesSnapshot, ChartRetainedSeriesDrawing> _seriesDrawings = [];
    // Cumulative work counters distinguish geometry construction from overlay redraws.
    private long _seriesDrawingBuildCount;
    private long _seriesDrawingReplayCount;

    private void ReleaseSeriesDrawingCache()
    {
        ReleaseResourceKind(ResourceCacheKind.SeriesDrawing);
        _seriesDrawingScene = null;
        _seriesDrawings.Clear();
    }

    private void RenderSeriesLayer(DrawingContext drawing, ChartScene scene)
    {
        if (!_attached) ReleaseSeriesDrawingCache();
        else if (!ReferenceEquals(_seriesDrawingScene, scene))
        {
            ReleaseSeriesDrawingCache();
            _seriesDrawingScene = scene;
        }
        using var plotClip = drawing.PushClip(scene.Plot);
        foreach (var series in _observedSeries)
            if (!scene.Snapshots.Any(snapshot => ReferenceEquals(snapshot.Series, series))) ClearCircleHistory(series);
        foreach (var snapshot in scene.Snapshots)
        {
            var renderer = snapshot.Series.Renderer;
            if (renderer is not (CartesianSeriesRenderer or ChartLineSeriesRenderer) || !snapshot.Series.CanShareRenderingGeometryHistory)
                ClearCircleHistory(snapshot.Series);
            var opacity = GetSeriesSelectionOpacity(snapshot.Series);
            using var selectionOpacity = opacity < 1 ? (IDisposable)drawing.PushOpacity(opacity) : null;
            using var geometryClip = renderer is not ChartComparisonSeriesRenderer && snapshot.BreakClip is { } clip
                ? (IDisposable)drawing.PushGeometryClip(clip) : null;

            var renderContext = SamplingRenderContext(snapshot.Context!);
            // Export has its own physical-pixel budget, or explicitly requests
            // original geometry. Do not replace the screen's retained plan.
            if (!ReferenceEquals(renderContext, snapshot.Context))
            {
                ClearCircleHistory(snapshot.Series);
                renderer.Render(drawing, renderContext);
                continue;
            }

            if (ResourceCacheBudgetBytes == 0)
            {
                ClearCircleHistory(snapshot.Series);
                _resourceCacheMisses++; _resourceBypasses++;
                renderer.Render(drawing, snapshot.Context!);
                continue;
            }

            // An application renderer can depend on external state or draw custom
            // operations. Its existing per-render contract is deliberately preserved.
            if (!_attached || snapshot.Context!.Projection is CartesianProjection { HasStableTransform: false } ||
                snapshot.Context.SecondaryProjection is CartesianProjection { HasStableTransform: false } ||
                renderer is not (CartesianSeriesRenderer or ChartLineSeriesRenderer or
                ChartComparisonSeriesRenderer or ChartPointSeriesRenderer or PictorialBarRenderer or ChartStatisticalSeriesRenderer or ChartFinancialSeriesRenderer))
            {
                ClearCircleHistory(snapshot.Series);
                renderer.Render(drawing, snapshot.Context!);
                continue;
            }
            if (_seriesDrawings.TryGetValue(snapshot, out var previous) &&
                !HasCircleHistoryReservation(snapshot.Series, previous))
                ClearCircleHistory(snapshot.Series);
            if (!_seriesDrawings.TryGetValue(snapshot, out var retained))
            {
                _seriesDrawingBuildCount++;
                retained = renderer is CartesianSeriesRenderer legacy
                    ? legacy.CreateRetainedDrawing(snapshot.Context!)
                    : ChartRetainedSeriesDrawing.Record(renderer, snapshot.Context!);
                // A renderer or paint callback can invalidate a scene reentrantly.
                // The local drawing remains valid, but must not repopulate retired caches.
                if (ReferenceEquals(_seriesDrawingScene, scene) && ReferenceEquals(_scene, scene) &&
                    TryRetainCircleHistory(snapshot.Series, retained) &&
                    TryRetainResource(ResourceCacheKind.SeriesDrawing, snapshot,
                        retained.EstimatedBytes - retained.SharedCircleHistoryBytes,
                        () => _seriesDrawings.Remove(snapshot)) &&
                    HasCircleHistoryReservation(snapshot.Series, retained))
                    _seriesDrawings[snapshot] = retained;
                else
                {
                    // Admission can evict the just-reserved history through
                    // either the local or process-wide budget. Never retain a
                    // plan whose shared payload is no longer charged.
                    if (_resourceEntries.TryGetValue(new(ResourceCacheKind.SeriesDrawing, snapshot), out var entry))
                        RemoveResourceEntry(entry, false);
                    ClearCircleHistory(snapshot.Series);
                }
            }
            else
            {
                TouchResource(ResourceCacheKind.SeriesDrawing, snapshot);
                if (retained.SharedCircleHistoryBytes > 0) TouchResource(ResourceCacheKind.CircleHistory, snapshot.Series);
            }
            _seriesDrawingReplayCount++;
            retained.Draw(drawing);
        }
    }

}

internal abstract class ChartRetainedSeriesDrawing
{
    // Logical model-1 payload weights. Native geometry/font/GPU allocation is
    // opaque and is not represented as a measured process-memory bound.
    internal abstract long EstimatedBytes { get; }
    // Shared histories have an independent, stable per-series budget entry.
    // EstimatedBytes remains the complete payload weight for direct callers.
    internal virtual long SharedCircleHistoryBytes => 0;
    internal virtual bool SharedCircleHistoryActive => true;
    public abstract void Draw(DrawingContext drawing);

    internal static long EstimateLinearGeometry(ChartSeriesRenderContext context)
    {
        var count = context.Data.Count;
        if (context.Series is LineSeries { SamplingMode: ChartSamplingMode.MinMax } &&
            ChartSamplingPlanner.For(context, context.SamplingCancellation).Sampling is { } sampled) count = sampled.Count;
        else if (count >= 8192 && context.Brush is ISolidColorBrush)
            count = ChartVisibilityQuery.Segments(context, 5 * ChartMarkerGeometry.Size(context.Series.StrokeThickness) + 1, neighbors: 1).Count;
        return 512L + count * 32L;
    }

    internal static long EstimateShapedGeometry(ChartSeriesRenderContext context)
    {
        var bytes = 512L;
        foreach (var figure in ChartLineGeometry.Build(context)) bytes += 128L + (figure.Upper.Count + (long)figure.Lower.Count) * 192;
        return bytes;
    }

    public static ChartRetainedSeriesDrawing Record(IChartSeriesRenderer renderer, ChartSeriesRenderContext context)
    {
        return renderer switch
        {
            ChartLineSeriesRenderer line => line.CreateRetainedDrawing(context),
            ChartComparisonSeriesRenderer comparison => comparison.CreateRetainedDrawing(context),
            ChartPointSeriesRenderer point => point.CreateRetainedDrawing(context),
            PictorialBarRenderer pictorial => pictorial.CreateRetainedDrawing(context),
            ChartStatisticalSeriesRenderer statistics => statistics.CreateRetainedDrawing(context),
            ChartFinancialSeriesRenderer financial => financial.CreateRetainedDrawing(context),
            _ => throw new InvalidOperationException("Only built-in series have retained drawing contracts."),
        };
    }

    // These groups contain only explicit DrawGeometry commands, preserving the
    // native operation and live paint references on every backend.
    internal sealed class Recorded(DrawingGroup group, long geometryBytes) : ChartRetainedSeriesDrawing
    {
        internal override long EstimatedBytes => 256L + geometryBytes;
        public override void Draw(DrawingContext drawing) => group.Draw(drawing);
    }

    internal sealed class Composite(ChartRetainedSeriesDrawing first, ChartRetainedSeriesDrawing second) : ChartRetainedSeriesDrawing
    {
        internal override long EstimatedBytes => 128L + first.EstimatedBytes + second.EstimatedBytes;
        internal override long SharedCircleHistoryBytes => first.SharedCircleHistoryBytes + second.SharedCircleHistoryBytes;
        internal override bool SharedCircleHistoryActive => first.SharedCircleHistoryActive && second.SharedCircleHistoryActive;
        public override void Draw(DrawingContext drawing) { first.Draw(drawing); second.Draw(drawing); }
    }
}
