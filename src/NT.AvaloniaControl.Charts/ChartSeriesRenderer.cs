using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using System.Runtime.CompilerServices;

namespace NT.AvaloniaControl.Charts;

/// <summary>Data-to-screen projection shared by rendering, hit testing and future overlays.</summary>
public interface IChartProjection
{
    Rect PlotArea { get; }
    Point Project(ChartPoint point);
    ChartPoint Unproject(Point position);
    double MapX(double value);
    double MapY(double value);
    /// <summary>Zero baseline, or the lowest valid axis bound when zero is outside the axis domain.</summary>
    double YOrigin { get; }
}

// Only projections owned by this library promise monotonic, independent X mapping.
internal interface IChartSeparableProjection : IChartProjection { bool HasMonotonicX { get; } }

public readonly record struct ChartSeriesBounds(double XMinimum, double XMaximum, double YMinimum, double YMaximum)
{
    public bool IsValid => double.IsFinite(XMinimum) && double.IsFinite(XMaximum) &&
        double.IsFinite(YMinimum) && double.IsFinite(YMaximum) && XMinimum <= XMaximum && YMinimum <= YMaximum;
}

public enum ChartLegendSymbol { Line, Rectangle, Circle }

/// <summary>A renderer hit refers to a snapshot ordinal before visual filtering; analytical fitted samples carry explicit synthetic provenance.</summary>
public sealed record ChartSeriesHit(int SourceIndex, Point Position, double DistanceSquared);

/// <summary>Series extension point; a new implementation does not require a chart type branch.</summary>
public interface IChartSeriesRenderer
{
    bool IncludesZero { get; }
    bool UsesCategorySlots { get; }
    ChartLegendSymbol LegendSymbol { get; }
    ChartSeriesBounds? GetBounds(ChartSeries series, ChartDataSnapshot data);
    void Render(DrawingContext drawing, ChartSeriesRenderContext context);
    ChartSeriesHit? HitTest(ChartSeriesRenderContext context, Point position, double tolerance);
}

/// <summary>Immutable scene inputs. The source collection remains owned by the application.</summary>
public sealed record ChartSeriesRenderContext(ChartSeries Series, ChartDataSnapshot Data, IChartProjection Projection,
    IBrush Brush, int GroupIndex = 0, int GroupCount = 1, double GroupWidth = 0)
{
    public Rect PlotArea => Projection.PlotArea;
    /// <summary>Optional independent projection and data for a composite series' secondary readout.</summary>
    public IChartProjection? SecondaryProjection { get; init; }
    public ChartDataSnapshot? SecondaryData { get; init; }
    /// <summary>Physical pixels per chart DIP for optional display sampling. Standalone renderers default to 1.</summary>
    public double RenderScaling { get; init; } = 1;
    internal bool SamplingDisabled { get; init; }
    internal CancellationToken SamplingCancellation { get; init; }
    internal Geometry? PrimaryBreakClip { get; init; }
    internal Geometry? SecondaryBreakClip { get; init; }
    internal IReadOnlyList<Rect>? PointHitRegions { get; init; }
}

public abstract partial class ChartSeries
{
    /// <summary>Override to add bounds, geometry, legend and hit behavior for a custom series.</summary>
    public virtual IChartSeriesRenderer Renderer => EmptySeriesRenderer.Instance;
}

public sealed partial class LineSeries
{
    public override IChartSeriesRenderer Renderer => Interpolation == ChartLineInterpolation.Linear
        ? CartesianSeriesRenderer.Line : ChartLineSeriesRenderer.Line;
}

public sealed partial class BarSeries
{
    public override IChartSeriesRenderer Renderer => UsesLegacyBarRenderer ? CartesianSeriesRenderer.Bar : ChartComparisonSeriesRenderer.Bar;
}

public sealed partial class ScatterSeries
{
    public override IChartSeriesRenderer Renderer => UsesLegacyPointRenderer ? CartesianSeriesRenderer.Scatter : ChartPointSeriesRenderer.Instance;
}

internal sealed class EmptySeriesRenderer : IChartSeriesRenderer
{
    public static readonly EmptySeriesRenderer Instance = new();
    public bool IncludesZero => false;
    public bool UsesCategorySlots => false;
    public ChartLegendSymbol LegendSymbol => ChartLegendSymbol.Line;
    public ChartSeriesBounds? GetBounds(ChartSeries series, ChartDataSnapshot data) => null;
    public void Render(DrawingContext drawing, ChartSeriesRenderContext context) { }
    public ChartSeriesHit? HitTest(ChartSeriesRenderContext context, Point position, double tolerance) => null;
}

internal sealed partial class CartesianSeriesRenderer(ChartLegendSymbol symbol, bool connect = false, bool bars = false) : IChartSeriesRenderer
{
    public static readonly CartesianSeriesRenderer Line = new(ChartLegendSymbol.Line, connect: true);
    public static readonly CartesianSeriesRenderer Bar = new(ChartLegendSymbol.Rectangle, bars: true);
    public static readonly CartesianSeriesRenderer Scatter = new(ChartLegendSymbol.Circle);
    private const int MaximumCachedBars = 8_192;
    private static readonly ConditionalWeakTable<ChartSeries, BarGeometryCache> BarGeometries = new();
    private sealed class BarGeometryCache
    {
        public CachedBar[] Entries = [];
    }
    private readonly record struct CachedBar(Rect Bounds, RectangleGeometry? Geometry);
    internal static void ReleaseBarGeometry(ChartSeries series) => BarGeometries.Remove(series);
    private static class DrawingGroupRecording
    {
        // Obtain the concrete recorder through its public factory, without naming
        // or reflecting over an internal Avalonia implementation.
        public static readonly Type ContextType = GetContextType();
        private static Type GetContextType()
        {
            using var context = new DrawingGroup().Open();
            return context.GetType();
        }
    }
    public bool IncludesZero => bars;
    public bool UsesCategorySlots => bars;
    public ChartLegendSymbol LegendSymbol => symbol;

    public ChartSeriesBounds? GetBounds(ChartSeries series, ChartDataSnapshot data)
        => ChartDataExtrema.For(data).Bounds;

    public void Render(DrawingContext drawing, ChartSeriesRenderContext context)
    {
        var coordinates = context.Data.Coordinates.Span;
        if (bars) { RenderBars(drawing, context, coordinates); return; }
        if (connect) RenderLine(drawing, context, coordinates);
        // Marker state must not remain live across the (potentially million-point)
        // line loop, and an unused marker style needs no geometry or paint setup.
        var radius = ChartMarkerGeometry.Radius(context.Series);
        if (radius > 0) RenderMarkers(drawing, context, coordinates, radius);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RenderLine(DrawingContext drawing, ChartSeriesRenderContext context, ReadOnlySpan<ChartPoint> coordinates)
    {
        if (context.Series is LineSeries { SamplingMode: ChartSamplingMode.MinMax } &&
            ChartSamplingPlanner.For(context, context.SamplingCancellation).Sampling is { } sampled)
        {
            RenderSampledLine(drawing, context, coordinates, sampled);
            return;
        }
        // Keep neighboring joins as well as both endpoints of every crossing edge.
        // The native clip still decides coverage; this only omits distant figures.
        // Retain the established short real-time path below its existing hot-loop
        // cutoff. A spatial query is more work there than recording the original
        // compact path, and small fixed-axis revisions keep their full geometry.
        var visible = context.Data.Count < 8_192 ? ChartVisibleOrdinals.All(context.Data.Count)
            : context.Brush is ISolidColorBrush
            ? ChartVisibilityQuery.Segments(context, 5 * Size(context.Series.StrokeThickness) + 1, neighbors: 1)
            : ChartVisibilityQuery.Full(context); // Spatial brushes observe the complete path's paint bounds.
        var geometry = new StreamGeometry();
        int segments;
        using (var path = geometry.Open())
            segments = !visible.IsFull ? RenderVisibleLine(path, context, coordinates, visible)
                : context.Projection is CartesianChart.CartesianProjection projection
                ? projection.TryGetLinearLineProjection(context.Data, out var linear)
                    ? coordinates.Length >= 8_192
                        ? RenderLargeInRangeLinearLine(path, coordinates, linear)
                        : RenderSmallInRangeLinearLine(path, coordinates, linear)
                    : projection.TryGetGappedLinearProjection(context.Data, out var gapped)
                        ? coordinates.Length >= 8_192
                            ? RenderLargeInRangeGappedLine(path, coordinates, gapped)
                            : RenderSmallInRangeGappedLine(path, coordinates, gapped)
                        : context.Data.Count >= 8_192
                            ? RenderLargeCartesianLine(path, context.Data, coordinates, new CartesianProjector(projection))
                            : RenderCartesianLine(path, context.Data, coordinates, new CartesianProjector(projection))
                : RenderCustomLine(path, context.Data, coordinates, new CustomProjector(context.Projection));
        if (visible.IsFull) ChartVisibilityQuery.RecordProjected(ChartDataExtrema.For(context.Data).Count);
        if (segments > 0)
        {
            drawing.DrawGeometry(null, new Pen(context.Brush, Size(context.Series.StrokeThickness)), geometry);
            // Immediate native backends can pass only the path handle to a long
            // unmanaged draw. Keep its owning geometry alive until that draw returns.
            GC.KeepAlive(geometry);
        }
    }

    // Value-type adapters give the built-in projection a direct call without relying on
    // tiered PGO to devirtualize the interface. The custom-projection path stays equivalent.
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int RenderCartesianLine(StreamGeometryContext path, ChartDataSnapshot data,
        ReadOnlySpan<ChartPoint> coordinates, CartesianProjector projection)
        => RenderLineCore(path, data, coordinates, projection);

    // Long loops can collect useful dynamic profiles and enter optimized OSR code;
    // keep that option instead of forcing the non-PGO small-loop compilation policy.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RenderLargeCartesianLine(StreamGeometryContext path, ChartDataSnapshot data,
        ReadOnlySpan<ChartPoint> coordinates, CartesianProjector projection)
        => RenderLineCore(path, data, coordinates, projection);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RenderCustomLine(StreamGeometryContext path, ChartDataSnapshot data,
        ReadOnlySpan<ChartPoint> coordinates, CustomProjector projection)
        => RenderLineCore(path, data, coordinates, projection);

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int RenderSmallInRangeLinearLine(StreamGeometryContext path,
        ReadOnlySpan<ChartPoint> coordinates, InRangeLinearProjection projection)
        => RenderInRangeLinearLineCore(new StreamLineSink(path), coordinates, projection);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RenderLargeInRangeLinearLine(StreamGeometryContext path,
        ReadOnlySpan<ChartPoint> coordinates, InRangeLinearProjection projection)
        => RenderInRangeLinearLineCore(new StreamLineSink(path), coordinates, projection);

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static int RenderSmallInRangeGappedLine(StreamGeometryContext path,
        ReadOnlySpan<ChartPoint> coordinates, InRangeLinearProjection projection)
        => RenderInRangeGappedLineCore(new StreamLineSink(path), coordinates, projection);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RenderLargeInRangeGappedLine(StreamGeometryContext path,
        ReadOnlySpan<ChartPoint> coordinates, InRangeLinearProjection projection)
        => RenderInRangeGappedLineCore(new StreamLineSink(path), coordinates, projection);

    private readonly struct StreamLineSink(StreamGeometryContext path) : IChartLineSink
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Begin(Point position) => path.BeginFigure(position, false);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Line(Point position) => path.LineTo(position);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void End() => path.EndFigure(false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int RenderInRangeLinearLineCore<TSink>(TSink path,
        ReadOnlySpan<ChartPoint> coordinates, InRangeLinearProjection projection) where TSink : struct, IChartLineSink
    {
        // The immutable index proves one finite, bounded figure. Retain every
        // original point and exactly one LineTo per original segment.
        path.Begin(projection.Project(coordinates[0]));
        for (var index = 1; index < coordinates.Length; index++)
            path.Line(projection.Project(coordinates[index]));
        path.End();
        return coordinates.Length - 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int RenderInRangeGappedLineCore<TSink>(TSink path,
        ReadOnlySpan<ChartPoint> coordinates, InRangeLinearProjection projection) where TSink : struct, IChartLineSink
    {
        var segments = 0;
        var started = false;
        foreach (var point in coordinates)
        {
            if (!Valid(point))
            { if (started) path.End(); started = false; continue; }
            var screen = projection.Project(point);
            if (!started) { path.Begin(screen); started = true; }
            else { path.Line(screen); segments++; }
        }
        if (started) path.End();
        return segments;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int RenderLineCore<TProjector>(StreamGeometryContext path, ChartDataSnapshot data,
        ReadOnlySpan<ChartPoint> coordinates, TProjector projection) where TProjector : struct, IPointProjector
    {
        var segments = 0;
        var count = data.Count;
        var started = false;
        for (var index = 0; index < count; index++)
        {
            var point = coordinates.IsEmpty ? data.GetValue(index).Point : coordinates[index];
            var screen = Valid(point) ? projection.Project(point) : new Point(double.NaN, double.NaN);
            if (!Valid(screen))
            { if (started) path.EndFigure(false); started = false; continue; }
            if (!started) { path.BeginFigure(screen, false); started = true; }
            else { path.LineTo(screen); segments++; }
        }
        if (started) path.EndFigure(false);
        return segments;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RenderBars(DrawingContext drawing, ChartSeriesRenderContext context, ReadOnlySpan<ChartPoint> coordinates)
    {
        var visible = VisibleBars(context);
        if (!visible.IsFull) { RenderVisibleBars(drawing, context, coordinates, visible); return; }
        var series = context.Series;
        var radius = ChartMarkerGeometry.Radius(series);
        ChartVisibilityQuery.RecordProjected((long)ChartDataExtrema.For(context.Data).Count * (radius > 0 ? 2 : 1));
        if (radius <= 0 && context.Projection is CartesianChart.CartesianProjection projection)
        {
            RenderCartesianBars(drawing, context, coordinates, projection);
            return;
        }
        BarGeometries.Remove(series);
        var markerShape = series.MarkerShape;
        var markerPen = radius <= 0 ? null : markerShape == ChartMarkerShape.Circle
            ? new Pen(series.MarkerStroke ?? context.Brush, ChartMarkerGeometry.StrokeThickness(series))
            : new Pen(series.MarkerStroke ?? context.Brush, ChartMarkerGeometry.StrokeThickness(series), lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var markerFill = series.MarkerFill ?? series.Fill ?? context.Brush;
        var markerGeometry = radius > 0 ? ChartMarkerGeometry.Create(markerShape, radius) : null;
        var markerExtent = markerShape == ChartMarkerShape.Circle ? radius : ChartMarkerGeometry.HitRadius(series);
        var fill = series.Fill ?? context.Brush;
        var count = context.Data.Count;
        for (var index = 0; index < count; index++)
        {
            var point = coordinates.IsEmpty ? context.Data.GetValue(index).Point : coordinates[index];
            if (Valid(point))
            {
                var rect = BarRectangle(context, point);
                if (double.IsFinite(rect.X) && double.IsFinite(rect.Y) && double.IsFinite(rect.Width) && double.IsFinite(rect.Height) && rect.Height > 0)
                    drawing.DrawRectangle(fill, null, rect);
                if (radius > 0)
                {
                    var end = context.Projection.Project(point);
                    var anchor = new Point(rect.Center.X, end.Y);
                    if (Valid(anchor) && context.PlotArea.Inflate(markerExtent).Contains(anchor))
                        ChartMarkerGeometry.Draw(drawing, markerShape, radius, markerFill, markerPen!, anchor, markerGeometry);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RenderCartesianBars(DrawingContext drawing, ChartSeriesRenderContext context,
        ReadOnlySpan<ChartPoint> coordinates, CartesianChart.CartesianProjection projection)
    {
        if (projection.TryGetLinearProjection(context.Data, out var linear))
        {
            if (coordinates.Length > MaximumCachedBars) RenderLargeInRangeLinearBars(drawing, context, coordinates, projection, linear);
            else RenderSmallInRangeLinearBars(drawing, context, coordinates, projection, linear);
            return;
        }
        BarGeometries.Remove(context.Series);
        var fill = context.Series.Fill ?? context.Brush;
        var data = context.Data;
        var count = data.Count;
        var width = context.GroupWidth / Math.Max(1, context.GroupCount);
        var barWidth = Math.Max(0.5, width * 0.86);
        var halfGroupWidth = context.GroupWidth / 2;
        var groupOffset = context.GroupIndex * width;
        var inset = (width - barWidth) / 2;
        var baseline = projection.YOrigin;
        if (!double.IsFinite(baseline)) baseline = context.PlotArea.Bottom;
        baseline = Math.Clamp(baseline, context.PlotArea.Top, context.PlotArea.Bottom);
        for (var index = 0; index < count; index++)
        {
            var point = coordinates.IsEmpty ? data.GetValue(index).Point : coordinates[index];
            if (!Valid(point)) continue;
            var screen = projection.Project(point);
            // Keep the original left-associative operations; do not combine the offsets.
            var rect = new Rect(screen.X - halfGroupWidth + groupOffset + inset,
                Math.Min(screen.Y, baseline), barWidth, Math.Abs(screen.Y - baseline));
            if (double.IsFinite(rect.X) && double.IsFinite(rect.Y) && double.IsFinite(rect.Width) && double.IsFinite(rect.Height) && rect.Height > 0)
                drawing.DrawRectangle(fill, null, rect);
        }
    }

    // Apply the same short-loop compilation policy as lines to each bar series,
    // while retaining profiling/OSR for larger per-series records.
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void RenderSmallInRangeLinearBars(DrawingContext drawing, ChartSeriesRenderContext context,
        ReadOnlySpan<ChartPoint> coordinates, CartesianChart.CartesianProjection projection, InRangeLinearProjection linear)
    {
        var fill = context.Series.Fill ?? context.Brush;
        if (fill is null) { BarGeometries.Remove(context.Series); return; }
        // DrawingGroup records both operations as a rectangle GeometryDrawing.
        // Other contexts must keep DrawRectangle: native DrawRect and DrawPath
        // can produce different edge pixels, even for the same rectangle.
        // Spatial brushes also retain the original paint-bounds path.
        if (drawing.GetType() != DrawingGroupRecording.ContextType ||
            fill.GetType() != typeof(SolidColorBrush) && fill.GetType() != typeof(ImmutableSolidColorBrush))
        {
            BarGeometries.Remove(context.Series);
            RenderInRangeLinearBarsCore(drawing, context, coordinates, projection, linear, fill, null);
            return;
        }
        var cache = BarGeometries.GetValue(context.Series, static _ => new());
        lock (cache)
        {
            var entries = cache.Entries;
            if (entries.Length != coordinates.Length) cache.Entries = entries = new CachedBar[coordinates.Length];
            // Keep this array local: a reentrant render may replace the cache's
            // current array, but cannot change this record's immutable inputs.
            RenderInRangeLinearBarsCore(drawing, context, coordinates, projection, linear, fill, entries);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RenderLargeInRangeLinearBars(DrawingContext drawing, ChartSeriesRenderContext context,
        ReadOnlySpan<ChartPoint> coordinates, CartesianChart.CartesianProjection projection, InRangeLinearProjection linear)
    {
        BarGeometries.Remove(context.Series);
        RenderInRangeLinearBarsCore(drawing, context, coordinates, projection, linear, context.Series.Fill ?? context.Brush, null);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RenderInRangeLinearBarsCore(DrawingContext drawing, ChartSeriesRenderContext context,
        ReadOnlySpan<ChartPoint> coordinates, CartesianChart.CartesianProjection projection, InRangeLinearProjection linear,
        IBrush fill, CachedBar[]? entries)
    {
        var width = context.GroupWidth / Math.Max(1, context.GroupCount);
        var barWidth = Math.Max(0.5, width * 0.86);
        var halfGroupWidth = context.GroupWidth / 2;
        var groupOffset = context.GroupIndex * width;
        var inset = (width - barWidth) / 2;
        var baseline = projection.YOrigin;
        if (!double.IsFinite(baseline)) baseline = context.PlotArea.Bottom;
        baseline = Math.Clamp(baseline, context.PlotArea.Top, context.PlotArea.Bottom);
        var ordinal = 0;
        foreach (var point in coordinates)
        {
            var screen = linear.Project(point);
            var rect = new Rect(screen.X - halfGroupWidth + groupOffset + inset,
                Math.Min(screen.Y, baseline), barWidth, Math.Abs(screen.Y - baseline));
            if (double.IsFinite(rect.X) && double.IsFinite(rect.Y) && double.IsFinite(rect.Width) && double.IsFinite(rect.Height) && rect.Height > 0)
            {
                if (entries is null) drawing.DrawRectangle(fill, null, rect);
                else
                {
                    ref var entry = ref entries[ordinal];
                    var geometry = entry.Geometry;
                    if (geometry is null || !SameRectBits(entry.Bounds, rect))
                    {
                        geometry = new RectangleGeometry(rect);
                        entry = new(rect, geometry);
                    }
                    // Never mutate a cached geometry: earlier drawing groups may
                    // still reference its platform implementation. Paint is live.
                    drawing.DrawGeometry(fill, null, geometry);
                }
            }
            else if (entries is not null) entries[ordinal] = default;
            ordinal++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool SameRectBits(Rect first, Rect last) =>
        BitConverter.DoubleToInt64Bits(first.X) == BitConverter.DoubleToInt64Bits(last.X) &&
        BitConverter.DoubleToInt64Bits(first.Y) == BitConverter.DoubleToInt64Bits(last.Y) &&
        BitConverter.DoubleToInt64Bits(first.Width) == BitConverter.DoubleToInt64Bits(last.Width) &&
        BitConverter.DoubleToInt64Bits(first.Height) == BitConverter.DoubleToInt64Bits(last.Height);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RenderMarkers(DrawingContext drawing, ChartSeriesRenderContext context, ReadOnlySpan<ChartPoint> coordinates, double radius)
    {
        var visible = ChartVisibilityQuery.Points(context, inflation: MarkerExtent(context.Series, radius) + 1);
        if (!visible.IsFull) { RenderVisibleMarkers(drawing, context, coordinates, radius, visible); return; }
        ChartVisibilityQuery.RecordProjected(ChartDataExtrema.For(context.Data).Count);
        var series = context.Series;
        var markerShape = series.MarkerShape;
        var markerPen = markerShape == ChartMarkerShape.Circle
            ? new Pen(series.MarkerStroke ?? context.Brush, ChartMarkerGeometry.StrokeThickness(series))
            : new Pen(series.MarkerStroke ?? context.Brush, ChartMarkerGeometry.StrokeThickness(series), lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var markerFill = series.MarkerFill ?? series.Fill ?? context.Brush;
        var markerExtent = markerShape == ChartMarkerShape.Circle ? radius : ChartMarkerGeometry.HitRadius(series);
        var plot = context.PlotArea.Inflate(markerExtent);
        var projection = context.Projection;
        var data = context.Data;
        if (markerShape == ChartMarkerShape.Circle && projection is CartesianChart.CartesianProjection cartesian)
        {
            // The pen is private to this render. For the sealed recorder and an
            // already immutable brush, capture it once instead of re-reading
            // mutable styled pen properties for every ellipse. Other contexts
            // retain the original pen and their existing observation behavior.
            if (markerPen.Brush?.GetType() == typeof(ImmutableSolidColorBrush) &&
                drawing.GetType() == DrawingGroupRecording.ContextType)
            {
                RenderImmutableCartesianCircles(drawing, data, coordinates, cartesian, plot, radius, markerFill, markerPen.ToImmutable());
                return;
            }
            RenderCartesianCircles(drawing, data, coordinates, cartesian, plot, radius, markerFill, markerPen);
            return;
        }
        var markerGeometry = ChartMarkerGeometry.Create(markerShape, radius);
        var count = data.Count;
        for (var index = 0; index < count; index++)
        {
            var point = coordinates.IsEmpty ? data.GetValue(index).Point : coordinates[index];
            if (!Valid(point)) continue;
            var screen = projection.Project(point);
            if (Valid(screen) && plot.Contains(screen))
                ChartMarkerGeometry.Draw(drawing, markerShape, radius, markerFill, markerPen, screen, markerGeometry);
        }
    }

    // Keep the built-in circle loop independent from transformed symbol drawing.
    // The same projection and DrawEllipse calls retain their original arithmetic;
    // default tiering can profile and inline this smaller hot body.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RenderCartesianCircles(DrawingContext drawing, ChartDataSnapshot data,
        ReadOnlySpan<ChartPoint> coordinates, CartesianChart.CartesianProjection projection,
        Rect plot, double radius, IBrush fill, Pen pen)
    {
        if (projection.TryGetLinearProjection(data, out var linear))
        { RenderInRangeLinearCircles(drawing, coordinates, linear, plot, radius, fill, pen); return; }
        var count = data.Count;
        for (var index = 0; index < count; index++)
        {
            var point = coordinates.IsEmpty ? data.GetValue(index).Point : coordinates[index];
            if (!Valid(point)) continue;
            var screen = projection.Project(point);
            if (Valid(screen) && plot.Contains(screen))
                drawing.DrawEllipse(fill, pen, screen, radius, radius);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RenderInRangeLinearCircles(DrawingContext drawing, ReadOnlySpan<ChartPoint> coordinates,
        InRangeLinearProjection projection, Rect plot, double radius, IBrush fill, Pen pen)
    {
        foreach (var point in coordinates)
        {
            var screen = projection.Project(point);
            if (plot.Contains(screen)) drawing.DrawEllipse(fill, pen, screen, radius, radius);
        }
    }

    // Preserve the original Pen entry for other contexts while letting this
    // qualified path access concrete immutable pen fields without interface PGO.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RenderImmutableCartesianCircles(DrawingContext drawing, ChartDataSnapshot data,
        ReadOnlySpan<ChartPoint> coordinates, CartesianChart.CartesianProjection projection,
        Rect plot, double radius, IBrush fill, ImmutablePen pen)
    {
        if (projection.TryGetLinearProjection(data, out var linear))
        { RenderInRangeImmutableLinearCircles(drawing, coordinates, linear, plot, radius, fill, pen); return; }
        var count = data.Count;
        for (var index = 0; index < count; index++)
        {
            var point = coordinates.IsEmpty ? data.GetValue(index).Point : coordinates[index];
            if (!Valid(point)) continue;
            var screen = projection.Project(point);
            if (Valid(screen) && plot.Contains(screen))
                drawing.DrawEllipse(fill, pen, screen, radius, radius);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RenderInRangeImmutableLinearCircles(DrawingContext drawing, ReadOnlySpan<ChartPoint> coordinates,
        InRangeLinearProjection projection, Rect plot, double radius, IBrush fill, ImmutablePen pen)
    {
        foreach (var point in coordinates)
        {
            var screen = projection.Project(point);
            if (plot.Contains(screen)) drawing.DrawEllipse(fill, pen, screen, radius, radius);
        }
    }

    public ChartSeriesHit? HitTest(ChartSeriesRenderContext context, Point position, double tolerance)
        => bars ? ChartComparisonLayout.For(context, null).HitTest(position, tolerance)
            : context.Projection is CartesianChart.CartesianProjection projection
            ? bars
                ? HitTestCartesian(context, position, tolerance, new CartesianProjector(projection))
                : HitTestCartesianPoints(context, position, tolerance, projection)
            : HitTestCustom(context, position, tolerance, new CustomProjector(context.Projection));

    // Short point-candidate windows do not reach a long-loop OSR threshold. Keep
    // their real loop in this optimized entry, rather than a tiny optimized wrapper
    // around a large shared bar/point body that still enters unoptimized code.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ChartSeriesHit? HitTestCartesianPoints(ChartSeriesRenderContext context, Point position,
        double tolerance, CartesianChart.CartesianProjection projection)
    {
        var radius = ChartMarkerGeometry.HitRadius(context.Series);
        var hitRadius = Math.Max(tolerance, radius);
        var window = GetHitWindow(context, position, tolerance, hitRadius, false, false);
        var plot = context.PlotArea.Inflate(radius);
        if (window.End - window.Start > 512 && projection.HasStableTransform &&
            double.IsFinite(position.X) && double.IsFinite(position.Y) && double.IsFinite(hitRadius) && hitRadius >= 0)
            return ChartVisibilityIndex.For(context.Data).HitTestPoints(projection, plot, position, hitRadius);
        return window.End - window.Start >= 8_192
            ? HitLargeCartesianPoints(context.Data, projection, position, plot, hitRadius, window)
            : HitSmallCartesianPoints(context.Data, projection, position, plot, hitRadius, window);
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static ChartSeriesHit? HitSmallCartesianPoints(ChartDataSnapshot data,
        CartesianChart.CartesianProjection projection, Point position, Rect plot, double hitRadius, HitWindow window)
        => HitCartesianPointsCore(data, projection, position, plot, hitRadius, window);

    // Dense hit windows, like dense line records, can use dynamic profiles and OSR
    // to inline the projection. The cutoff counts actual candidates, not source size.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ChartSeriesHit? HitLargeCartesianPoints(ChartDataSnapshot data,
        CartesianChart.CartesianProjection projection, Point position, Rect plot, double hitRadius, HitWindow window)
        => HitCartesianPointsCore(data, projection, position, plot, hitRadius, window);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ChartSeriesHit? HitCartesianPointsCore(ChartDataSnapshot data,
        CartesianChart.CartesianProjection projection, Point position, Rect plot, double hitRadius, HitWindow window)
    {
        var coordinates = data.Coordinates.Span;
        var bestOrdinal = -1; var bestIndex = -1;
        var bestDistance = double.PositiveInfinity; var bestPosition = default(Point);
        for (var candidate = window.Start; candidate < window.End; candidate++)
        {
            var ordinal = window.Indexed
                ? window.Index.Ordinal(window.Reverse ? window.Index.Count - 1 - candidate : candidate) : candidate;
            var point = coordinates.IsEmpty ? data.GetValue(ordinal).Point : coordinates[ordinal];
            if (!Valid(point)) continue;
            var screen = projection.Project(point);
            if (!Valid(screen) || !plot.Contains(screen)) continue;
            var delta = screen - position;
            var distance = delta.X * delta.X + delta.Y * delta.Y;
            if (distance > hitRadius * hitRadius) continue;
            if (distance < bestDistance || distance == bestDistance && ordinal > bestOrdinal)
            { bestOrdinal = ordinal; bestIndex = coordinates.IsEmpty ? data.GetValue(ordinal).SourceIndex : ordinal; bestPosition = screen; bestDistance = distance; }
        }
        return bestOrdinal < 0 ? null : new(bestIndex, bestPosition, bestDistance);
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private ChartSeriesHit? HitTestCartesian(ChartSeriesRenderContext context, Point position, double tolerance,
        CartesianProjector projection) => HitTestCore(context, position, tolerance, projection);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private ChartSeriesHit? HitTestCustom(ChartSeriesRenderContext context, Point position, double tolerance,
        CustomProjector projection) => HitTestCore(context, position, tolerance, projection);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ChartSeriesHit? HitTestCore<TProjector>(ChartSeriesRenderContext context, Point position, double tolerance,
        TProjector projection) where TProjector : struct, IPointProjector
    {
        var bestOrdinal = -1; var bestIndex = -1;
        var bestDistance = double.PositiveInfinity; var bestPosition = default(Point);
        var radius = ChartMarkerGeometry.HitRadius(context.Series);
        var hitRadius = Math.Max(tolerance, radius);
        var showMarkers = context.Series.ShowMarkers;
        var barMarkers = bars && showMarkers && ChartMarkerGeometry.Radius(context.Series) > 0;
        var markerPlot = barMarkers ? context.PlotArea.Inflate(radius) : default;
        var coordinates = context.Data.Coordinates.Span;
        var window = GetHitWindow(context, position, tolerance, hitRadius, bars, showMarkers);
        var index = window.Index;
        var (start, end, reverse, indexed) = (window.Start, window.End, window.Reverse, window.Indexed);
        for (var candidate = start; candidate < end; candidate++)
        {
            var ordinal = indexed ? index.Ordinal(reverse ? index.Count - 1 - candidate : candidate) : candidate;
            var point = coordinates.IsEmpty ? context.Data.GetValue(ordinal).Point : coordinates[ordinal];
            if (!Valid(point)) continue;
            var screen = projection.Project(point);
            if (!Valid(screen)) continue;
            double distance;
            if (bars)
            {
                var fullRectangle = BarRectangle(context, point);
                var rectangle = fullRectangle.Intersect(context.PlotArea);
                distance = double.PositiveInfinity;
                if (rectangle.Width > 0 && rectangle.Height > 0)
                {
                    var dx = Math.Max(rectangle.Left - position.X, Math.Max(0, position.X - rectangle.Right));
                    var dy = Math.Max(rectangle.Top - position.Y, Math.Max(0, position.Y - rectangle.Bottom));
                    distance = dx * dx + dy * dy;
                    if (distance > tolerance * tolerance) distance = double.PositiveInfinity;
                }
                var markerWins = false;
                if (barMarkers)
                {
                    var anchor = new Point(fullRectangle.Center.X, screen.Y);
                    var markerDelta = anchor - position;
                    var markerDistance = markerDelta.X * markerDelta.X + markerDelta.Y * markerDelta.Y;
                    if (markerPlot.Contains(anchor) && markerDistance <= hitRadius * hitRadius && markerDistance < distance)
                    { distance = markerDistance; screen = anchor; markerWins = true; }
                }
                if (!markerWins && double.IsFinite(distance)) screen = new Point(rectangle.Center.X, Math.Clamp(screen.Y, rectangle.Top, rectangle.Bottom));
                if (!double.IsFinite(distance)) continue;
            }
            else
            {
                if (!context.PlotArea.Inflate(radius).Contains(screen)) continue;
                var delta = screen - position;
                distance = delta.X * delta.X + delta.Y * delta.Y;
                if (distance > hitRadius * hitRadius) continue;
            }
            if (distance < bestDistance || distance == bestDistance && ordinal > bestOrdinal)
            { bestOrdinal = ordinal; bestIndex = coordinates.IsEmpty ? context.Data.GetValue(ordinal).SourceIndex : ordinal; bestPosition = screen; bestDistance = distance; }
        }
        return bestOrdinal < 0 ? null : new(bestIndex, bestPosition, bestDistance);
    }

    private readonly record struct HitWindow(ChartDataIndex Index, int Start, int End, bool Reverse, bool Indexed);

    private static HitWindow GetHitWindow(ChartSeriesRenderContext context, Point position, double tolerance, double hitRadius,
        bool bars, bool showMarkers)
    {
        var index = ChartDataIndex.For(context.Data);
        var indexed = false; var reverse = false;
        var start = 0; var end = context.Data.Count;
        if (context.Projection is IChartSeparableProjection { HasMonotonicX: true } &&
            double.IsFinite(context.GroupWidth))
        {
            var searchRadius = bars ? Math.Abs(context.GroupWidth) / 2 + Math.Max(tolerance, showMarkers ? hitRadius : 0) + .25 : hitRadius;
            var guard = 1e-9 * Math.Max(1, Math.Max(Math.Abs(position.X), searchRadius));
            var a = position.X - searchRadius - guard;
            var b = position.X + searchRadius + guard;
            // Project saturates extreme screen coordinates. At that plateau the original
            // full-scan distance rule, including very large tolerances, remains authoritative.
            if (a > -1_000_000 && b < 1_000_000)
            {
                (start, end, reverse) = index.ScreenRange(context.Projection, a, b);
                indexed = true;
            }
        }
        return new(index, start, end, reverse, indexed);
    }

    internal static Rect BarRectangle(ChartSeriesRenderContext context, ChartPoint point)
        => ChartComparisonLayout.LegacyRectangle(context, context.Projection.Project(point));

    private static bool Valid(ChartPoint point) => double.IsFinite(point.X) && double.IsFinite(point.Y);
    private static bool Valid(Point point) => double.IsFinite(point.X) && double.IsFinite(point.Y);
    private static double Size(double value) => double.IsFinite(value) && value >= 0 ? Math.Min(100, value) : 0;

    private interface IPointProjector
    {
        Point Project(ChartPoint point);
    }

    private readonly struct CartesianProjector(CartesianChart.CartesianProjection projection) : IPointProjector
    {
        public Point Project(ChartPoint point) => projection.Project(point);
    }

    private readonly struct CustomProjector(IChartProjection projection) : IPointProjector
    {
        public Point Project(ChartPoint point) => projection.Project(point);
    }
}
