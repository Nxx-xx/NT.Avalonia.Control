using Avalonia;
using Avalonia.Media;
using System.Runtime.CompilerServices;

namespace NT.AvaloniaControl.Charts;

// The same projected primitives drive native cubic rendering and segment readouts.
// All indices refer to physical snapshot ordinals, never to filtered source indices.
internal readonly record struct ChartLinePathPart(int StartOrdinal, int EndOrdinal, Point Start, Point Control1,
    Point Control2, Point End, bool IsCubic, double StartFraction = 0, double EndFraction = 1)
{
    internal Point At(double t)
    {
        if (t <= 0) return Start;
        if (t >= 1) return End;
        if (!IsCubic) return Lerp(Start, End, t);
        var a = Lerp(Start, Control1, t); var b = Lerp(Control1, Control2, t); var c = Lerp(Control2, End, t);
        return Lerp(Lerp(a, b, t), Lerp(b, c, t), t);
    }
    internal void Append(StreamGeometryContext path, bool reverse = false)
    {
        if (IsCubic) path.CubicBezierTo(reverse ? Control2 : Control1, reverse ? Control1 : Control2, reverse ? Start : End);
        else path.LineTo(reverse ? Start : End);
    }
    internal IEnumerable<(Point Start, Point End, double First, double Last)> Flatten()
    {
        if (!IsCubic) { yield return (Start, End, 0, 1); yield break; }
        foreach (var piece in Subdivide(Start, Control1, Control2, End, 0, 1, 0)) yield return piece;
    }
    private static IEnumerable<(Point Start, Point End, double First, double Last)> Subdivide(Point a, Point b,
        Point c, Point d, double first, double last, int depth)
    {
        // 0.02 DIP control-polygon distance bounds the rendered cubic/chord error.
        if (depth >= 16 || Math.Max(DistanceToChord(b, a, d), DistanceToChord(c, a, d)) <= .02)
        { yield return (a, d, first, last); yield break; }
        var ab = Lerp(a, b, .5); var bc = Lerp(b, c, .5); var cd = Lerp(c, d, .5);
        var abc = Lerp(ab, bc, .5); var bcd = Lerp(bc, cd, .5); var middle = Lerp(abc, bcd, .5);
        var fraction = (first + last) / 2;
        foreach (var piece in Subdivide(a, ab, abc, middle, first, fraction, depth + 1)) yield return piece;
        foreach (var piece in Subdivide(middle, bcd, cd, d, fraction, last, depth + 1)) yield return piece;
    }
    private static double DistanceToChord(Point point, Point a, Point b)
    {
        var scale = Math.Max(Math.Max(Math.Abs(a.X), Math.Abs(a.Y)),
            Math.Max(Math.Max(Math.Abs(b.X), Math.Abs(b.Y)), Math.Max(Math.Abs(point.X), Math.Abs(point.Y))));
        if (scale == 0) return 0;
        var dx = b.X / scale - a.X / scale; var dy = b.Y / scale - a.Y / scale;
        var px = point.X / scale - a.X / scale; var py = point.Y / scale - a.Y / scale;
        var length = Math.Sqrt(dx * dx + dy * dy);
        return (length == 0 ? Math.Sqrt(px * px + py * py) : Math.Abs(px * dy - py * dx) / length) * scale;
    }
    private static Point Lerp(Point a, Point b, double t) => new(a.X * (1 - t) + b.X * t, a.Y * (1 - t) + b.Y * t);
}

internal sealed class ChartLinePathFigure
{
    internal List<ChartLinePathPart> Upper { get; } = [];
    internal List<ChartLinePathPart> Lower { get; } = [];
}

internal static class ChartLineGeometry
{
    // Worker inputs contain numerical values only. Contexts, projections, paints
    // and application objects are deliberately absent from this graph.
    internal readonly record struct NumericVertex(int Ordinal, Point Upper, Point Lower);
    internal sealed record NumericInput(NumericVertex[] Vertices, ChartLineInterpolation Mode, bool IsBand);
    internal sealed record NumericStack(int[] Members, bool FullGeometry, bool[,] SameRuns);
    internal sealed record NumericBatch(NumericInput[] Inputs, NumericStack[] Stacks);
    private static readonly ConditionalWeakTable<ChartSeriesRenderContext, object> Pending = new();
    private static readonly object PendingMarker = new();
    [ThreadStatic] private static int _synchronousScope;
    internal static bool IsSynchronousPreparation => _synchronousScope > 0;
    internal static bool IsBackgroundPending(ChartSeriesRenderContext context) => _synchronousScope == 0 && Pending.TryGetValue(context, out _);
    internal static void MarkBackgroundPending(ChartSeriesRenderContext context)
    { PreparedStacks.Remove(context); Pending.Remove(context); Pending.Add(context, PendingMarker); }
    internal static void ClearBackgroundPending(ChartSeriesRenderContext context) => Pending.Remove(context);
    internal static void PublishBackground(ChartSeriesRenderContext context, IReadOnlyList<ChartLinePathFigure> figures)
    {
        Pending.Remove(context); PreparedStacks.Remove(context);
        PreparedStacks.Add(context, new(figures, CurrentStyle(context), isPreparedStack: true));
    }
    internal static IDisposable BeginSynchronousScope()
    { _synchronousScope++; return new SynchronousScope(); }
    private sealed class SynchronousScope : IDisposable
    { public void Dispose() => _synchronousScope--; }
    private readonly record struct GeometryStyle(ChartLineInterpolation Interpolation, ChartStackMode StackMode, double Baseline,
        bool FullPaintBounds, double StrokeThickness);
    private sealed class PreparedGeometry(IReadOnlyList<ChartLinePathFigure> figures, GeometryStyle style, bool isPreparedStack = false)
    {
        internal IReadOnlyList<ChartLinePathFigure> Figures { get; } = figures;
        internal GeometryStyle Style { get; } = style;
        internal bool IsPreparedStack { get; } = isPreparedStack;
    }
    private static readonly ConditionalWeakTable<ChartSeriesRenderContext, PreparedGeometry> PreparedStacks = new();
    private static long _geometryBuildCount;
    private static long _projectedVertexCount;
    internal static long GeometryBuildCount => Interlocked.Read(ref _geometryBuildCount);
    internal static long ProjectedVertexCount => Interlocked.Read(ref _projectedVertexCount);
    private static GeometryStyle CurrentStyle(ChartSeriesRenderContext context) => new(
        (context.Series as IChartLineSeries)?.Interpolation ?? ChartLineInterpolation.Linear,
        (context.Series as IChartLineSeries)?.StackMode ?? ChartStackMode.None,
        context.Series is AreaSeries area ? area.Baseline : 0, NeedsFullPaintBounds(context),
        ChartMarkerGeometry.Size(context.Series.StrokeThickness));
    private static bool NeedsFullPaintBounds(ChartSeriesRenderContext context) =>
        (context.Series.Stroke ?? context.Brush) is not ISolidColorBrush ||
        context.Series is AreaSeries or IChartRangeSeries && (context.Series.Fill ?? context.Brush) is not ISolidColorBrush;
    internal static bool IsBuiltIn(ChartSeries series) => series switch
    {
        LineSeries => ReferenceEquals(series.Renderer, CartesianSeriesRenderer.Line) || ReferenceEquals(series.Renderer, ChartLineSeriesRenderer.Line),
        AreaSeries => ReferenceEquals(series.Renderer, ChartLineSeriesRenderer.Area),
        IChartRangeSeries => ReferenceEquals(series.Renderer, ChartLineSeriesRenderer.RangeArea),
        ChartAnalyticalSeries => ReferenceEquals(series.Renderer, CartesianSeriesRenderer.Line),
        ScatterLineSeries { ShowLine: true, StrokeThickness: > 0 } => series.Renderer is ChartPointSeriesRenderer,
        _ => false,
    };

    internal static IReadOnlyList<ChartLinePathFigure> Build(ChartSeriesRenderContext context)
    {
        if (IsBackgroundPending(context)) return [];
        var style = CurrentStyle(context);
        var stable = context.Projection is CartesianChart.CartesianProjection { HasStableTransform: true };
        if (PreparedStacks.TryGetValue(context, out var prepared) && prepared.Style == style &&
            (prepared.IsPreparedStack || stable)) return prepared.Figures;
        var figures = BuildCore(context);
        PreserveBandThickness([figures]);
        // Public projections may read mutable caller state. Chart-owned contexts
        // contain a fixed projection and are released with their scene.
        if (stable)
        {
            PreparedStacks.Remove(context);
            PreparedStacks.Add(context, new(figures, style));
        }
        return figures;
    }

    internal static NumericBatch CaptureBackground(IReadOnlyList<ChartSeriesRenderContext> contexts,
        IReadOnlyList<int[]> stackGroups)
    {
        var full = new bool[contexts.Count]; var stacks = new List<NumericStack>();
        foreach (var indices in stackGroups)
        {
            var fullGeometry = indices.Any(index => NeedsFullPaintBounds(contexts[index]));
            var sameRuns = new bool[indices.Length, indices.Length];
            for (var i = 0; i < indices.Length; i++)
                for (var j = i; j < indices.Length; j++)
                {
                    var match = ChartStackTopology.Compare(contexts[indices[i]], contexts[indices[j]]);
                    sameRuns[i, j] = sameRuns[j, i] = match.SameRuns;
                    if (match is { SameRuns: true, SameX: false }) fullGeometry = true;
                }
            if (fullGeometry) foreach (var index in indices) full[index] = true;
            stacks.Add(new(indices, fullGeometry, sameRuns));
        }
        var inputs = new NumericInput[contexts.Count];
        for (var index = 0; index < contexts.Count; index++) inputs[index] = CaptureNumeric(contexts[index], !full[index]);
        return new(inputs, stacks.ToArray());
    }

    private static NumericInput CaptureNumeric(ChartSeriesRenderContext context, bool clip)
    {
        var isBand = context.Series is AreaSeries or IChartRangeSeries ||
            context.Series is IChartLineSeries { StackMode: not ChartStackMode.None };
        var mode = (context.Series as IChartLineSeries)?.Interpolation ?? ChartLineInterpolation.Linear;
        var ordinals = !clip || NeedsFullPaintBounds(context) ? ChartVisibleOrdinals.All(context.Data.Count) :
            ChartVisibilityQuery.Segments(context, ChartMarkerGeometry.Size(context.Series.StrokeThickness) * 5 + 1,
                neighbors: mode == ChartLineInterpolation.Smooth ? 1 : 0, xOnly: isBand);
        var vertices = new NumericVertex[ordinals.Count]; var cursor = 0; long projected = 0;
        foreach (var ordinal in ordinals)
        {
            var value = context.Data.GetValue(ordinal); var upper = new Point(double.NaN, double.NaN);
            if (!value.IsEmpty && Finite(value.Point)) { upper = context.Projection.Project(value.Point); projected++; }
            var lower = upper;
            if (isBand && Finite(upper))
            {
                var baseline = context.Data.GetBandLower(ordinal) ?? (context.Series is AreaSeries area ? area.Baseline : double.NaN);
                lower = context.Projection.Project(new(value.Point.X, baseline)); projected++;
                if (!Finite(lower) && baseline == 0 && double.IsFinite(context.Projection.YOrigin)) lower = new(upper.X, context.Projection.YOrigin);
            }
            vertices[cursor++] = new(ordinal, upper, lower);
        }
        Interlocked.Add(ref _projectedVertexCount, ordinals.Count); ChartVisibilityQuery.RecordProjected(projected);
        return new(vertices, mode, isBand);
    }

    internal static IReadOnlyList<ChartLinePathFigure>[] BuildBackground(NumericBatch batch, CancellationToken cancellation)
    {
        var results = new IReadOnlyList<ChartLinePathFigure>[batch.Inputs.Length];
        for (var index = 0; index < results.Length; index++) results[index] = BuildNumeric(batch.Inputs[index], cancellation);
        var inStack = new bool[results.Length];
        foreach (var stack in batch.Stacks)
        {
            var groups = new List<List<int>>();
            for (var i = 0; i < stack.Members.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested(); var index = stack.Members[i]; inStack[index] = true;
                var group = groups.FirstOrDefault(bucket => (stack.FullGeometry || stack.SameRuns[bucket[0], i]) &&
                    HasSameTopology(results[stack.Members[bucket[0]]], results[index]));
                if (group is null) groups.Add(group = []);
                group.Add(i);
            }
            foreach (var group in groups)
                PreserveBandThickness(group.Select(i => results[stack.Members[i]]).ToArray(), cancellation);
        }
        for (var index = 0; index < results.Length; index++)
            if (!inStack[index]) PreserveBandThickness([results[index]], cancellation);
        cancellation.ThrowIfCancellationRequested(); return results;
    }

    private static IReadOnlyList<ChartLinePathFigure> BuildNumeric(NumericInput input, CancellationToken cancellation)
    {
        Interlocked.Increment(ref _geometryBuildCount);
        var result = new List<ChartLinePathFigure>(); var vertices = new List<(int Ordinal, Point Upper, Point Lower)>();
        var previous = -2; var cursor = 0;
        foreach (var vertex in input.Vertices)
        {
            if ((cursor++ & 63) == 0) cancellation.ThrowIfCancellationRequested();
            if (vertex.Ordinal != previous + 1) Flush(); previous = vertex.Ordinal;
            if (!Finite(vertex.Upper) || !Finite(vertex.Lower)) { Flush(); continue; }
            vertices.Add((vertex.Ordinal, vertex.Upper, vertex.Lower));
        }
        Flush(); return result;
        void Flush()
        {
            if (vertices.Count >= 2)
            {
                var figure = new ChartLinePathFigure();
                for (var i = 0; i + 1 < vertices.Count; i++)
                {
                    if ((i & 63) == 0) cancellation.ThrowIfCancellationRequested();
                    figure.Upper.AddRange(Parts(vertices, i, input.Mode, false));
                    if (input.IsBand) figure.Lower.AddRange(Parts(vertices, i, input.Mode, true));
                }
                result.Add(figure);
            }
            vertices.Clear();
        }
    }

    // Called once after a scene has created the contexts of one resolved stack
    // group. Compatible boundaries must use one common tangent limiter, otherwise
    // independently limiting a band can detach the following band's shared edge.
    internal static void PrepareStackGeometry(IReadOnlyList<ChartSeriesRenderContext> contexts)
    {
        var members = contexts.Where(context => context.Series is IChartLineSeries
            { Interpolation: ChartLineInterpolation.Smooth, StackMode: not ChartStackMode.None }).ToArray();
        // Different raw X values can collapse onto the same saturated screen X.
        // That uncommon topology cannot be proved equivalent from a cropped run;
        // keep the established full-geometry limiter for such a stack group.
        var fullGeometry = members.Any(NeedsFullPaintBounds);
        for (var i = 0; i < members.Length && !fullGeometry; i++)
            for (var j = i + 1; j < members.Length; j++)
                if (ChartStackTopology.Compare(members[i], members[j]) is { SameRuns: true, SameX: false })
                { fullGeometry = true; break; }
        var groups = new List<List<(ChartSeriesRenderContext Context, IReadOnlyList<ChartLinePathFigure> Figures)>>();
        foreach (var context in members)
        {
            var figures = BuildCore(context, clip: !fullGeometry);
            var group = groups.FirstOrDefault(bucket => (fullGeometry || ChartStackTopology.Compare(bucket[0].Context, context).SameRuns)
                && HasSameTopology(bucket[0].Figures, figures));
            if (group is null) groups.Add(group = []);
            group.Add((context, figures));
        }
        foreach (var group in groups)
        {
            PreserveBandThickness(group.Select(entry => entry.Figures).ToArray());
            foreach (var entry in group)
            {
                PreparedStacks.Remove(entry.Context);
                PreparedStacks.Add(entry.Context, new(entry.Figures, CurrentStyle(entry.Context), isPreparedStack: true));
            }
        }
    }

    private static bool HasSameTopology(IReadOnlyList<ChartLinePathFigure> first, IReadOnlyList<ChartLinePathFigure> second)
    {
        if (first.Count != second.Count) return false;
        for (var i = 0; i < first.Count; i++)
        {
            if (first[i].Upper.Count != second[i].Upper.Count) return false;
            for (var j = 0; j < first[i].Upper.Count; j++)
            {
                var a = first[i].Upper[j]; var b = second[i].Upper[j];
                if (a.StartOrdinal != b.StartOrdinal || a.EndOrdinal != b.EndOrdinal ||
                    a.Start.X != b.Start.X || a.End.X != b.End.X || a.IsCubic != b.IsCubic) return false;
            }
        }
        return true;
    }

    private static void PreserveBandThickness(IReadOnlyList<IReadOnlyList<ChartLinePathFigure>> group, CancellationToken cancellation = default)
    {
        var scales = new Dictionary<int, double>();
        foreach (var figures in group)
            foreach (var figure in figures)
                for (var i = 0; i < figure.Upper.Count && i < figure.Lower.Count; i++)
                {
                    if ((i & 63) == 0) cancellation.ThrowIfCancellationRequested();
                    var upper = figure.Upper[i]; var lower = figure.Lower[i];
                    if (!upper.IsCubic || !lower.IsCubic) continue;
                    var startGap = upper.Start.Y - lower.Start.Y; var endGap = upper.End.Y - lower.End.Y;
                    var direction = Math.Sign(startGap); if (direction == 0) direction = Math.Sign(endGap);
                    // A contribution changing sign intentionally crosses its base.
                    if (direction == 0 || startGap * direction < 0 || endGap * direction < 0) continue;
                    Limit(upper.StartOrdinal, startGap * direction,
                        ((upper.Control1.Y - upper.Start.Y) - (lower.Control1.Y - lower.Start.Y)) * direction);
                    Limit(upper.EndOrdinal, endGap * direction,
                        ((upper.Control2.Y - upper.End.Y) - (lower.Control2.Y - lower.End.Y)) * direction);
                }
        foreach (var figures in group)
            foreach (var figure in figures)
            {
                Scale(figure.Upper); Scale(figure.Lower);
            }

        void Limit(int ordinal, double gap, double difference)
        {
            if (difference >= 0) return;
            var scale = !double.IsFinite(gap) || !double.IsFinite(difference) ? 0 : Math.Clamp(gap / -difference, 0, 1);
            // Stay on the safe side of a floating-point equality at zero width.
            if (scale < 1) scale *= 1 - 1e-12;
            scales[ordinal] = Math.Min(scales.GetValueOrDefault(ordinal, 1), scale);
        }
        void Scale(List<ChartLinePathPart> parts)
        {
            for (var i = 0; i < parts.Count; i++)
            {
                if ((i & 63) == 0) cancellation.ThrowIfCancellationRequested();
                var part = parts[i]; if (!part.IsCubic) continue;
                var first = scales.GetValueOrDefault(part.StartOrdinal, 1); var last = scales.GetValueOrDefault(part.EndOrdinal, 1);
                parts[i] = part with
                {
                    Control1 = new(part.Control1.X, Blend(part.Start.Y, part.Control1.Y, first)),
                    Control2 = new(part.Control2.X, Blend(part.End.Y, part.Control2.Y, last)),
                };
            }
        }
        static double Blend(double a, double b, double t) => t == 0 ? a : t == 1 ? b : a * (1 - t) + b * t;
    }

    private static IReadOnlyList<ChartLinePathFigure> BuildCore(ChartSeriesRenderContext context, bool clip = true)
    {
        Interlocked.Increment(ref _geometryBuildCount);
        var result = new List<ChartLinePathFigure>();
        var vertices = new List<(int Ordinal, Point Upper, Point Lower)>();
        var isBand = context.Series is AreaSeries or IChartRangeSeries ||
            context.Series is IChartLineSeries { StackMode: not ChartStackMode.None };
        var mode = (context.Series as IChartLineSeries)?.Interpolation ?? ChartLineInterpolation.Linear;
        // A band may cover the plot even when both its upper and lower boundary
        // are outside Y. Query only X in that case, retaining its closing edges.
        var ordinals = !clip || NeedsFullPaintBounds(context) ? ChartVisibleOrdinals.All(context.Data.Count) : ChartVisibilityQuery.Segments(context,
            inflation: ChartMarkerGeometry.Size(context.Series.StrokeThickness) * 5 + 1,
            neighbors: mode == ChartLineInterpolation.Smooth ? 1 : 0, xOnly: isBand);
        Interlocked.Add(ref _projectedVertexCount, ordinals.Count);
        long projectedCount = 0;
        var previousOrdinal = -2;
        foreach (var ordinal in ordinals)
        {
            // Candidate sets can contain several disjoint runs. Never bridge
            // omitted physical rows (nor a missing sample) into a new segment.
            if (ordinal != previousOrdinal + 1) Flush();
            previousOrdinal = ordinal;
            var value = context.Data.GetValue(ordinal);
            var upper = new Point(double.NaN, double.NaN);
            if (!value.IsEmpty && Finite(value.Point)) { upper = context.Projection.Project(value.Point); projectedCount++; }
            var lower = upper;
            if (isBand && Finite(upper))
            {
                var baseline = context.Data.GetBandLower(ordinal) ?? (context.Series is AreaSeries area ? area.Baseline : double.NaN);
                lower = context.Projection.Project(new(value.Point.X, baseline));
                projectedCount++;
                if (!Finite(lower) && baseline == 0 && double.IsFinite(context.Projection.YOrigin))
                    lower = new(upper.X, context.Projection.YOrigin);
            }
            if (!Finite(upper) || !Finite(lower)) { Flush(); continue; }
            vertices.Add((ordinal, upper, lower));
        }
        Flush();
        ChartVisibilityQuery.RecordProjected(projectedCount);
        return result;

        void Flush()
        {
            if (vertices.Count >= 2)
            {
                var figure = new ChartLinePathFigure();
                for (var i = 0; i + 1 < vertices.Count; i++)
                {
                    var upper = Parts(vertices, i, mode, false);
                    var lower = isBand ? Parts(vertices, i, mode, true) : [];
                    figure.Upper.AddRange(upper); figure.Lower.AddRange(lower);
                }
                result.Add(figure);
            }
            vertices.Clear();
        }
    }

    private static List<ChartLinePathPart> Parts(List<(int Ordinal, Point Upper, Point Lower)> points, int i,
        ChartLineInterpolation mode, bool lower)
    {
        Point Value(int at) => lower ? points[at].Lower : points[at].Upper;
        var a = Value(i); var b = Value(i + 1);
        var first = points[i].Ordinal; var last = points[i + 1].Ordinal;
        ChartLinePathPart Line(Point start, Point end, double low, double high) => new(first, last, start, start, end, end, false, low, high);
        if (mode == ChartLineInterpolation.StepBefore)
        {
            var corner = new Point(a.X, b.Y);
            return [Line(a, corner, 0, .5), Line(corner, b, .5, 1)];
        }
        if (mode == ChartLineInterpolation.StepAfter)
        {
            var corner = new Point(b.X, a.Y);
            return [Line(a, corner, 0, .5), Line(corner, b, .5, 1)];
        }
        if (mode == ChartLineInterpolation.StepCenter)
        {
            var middle = a.X / 2 + b.X / 2;
            var c = new Point(middle, a.Y); var d = new Point(middle, b.Y);
            return [Line(a, c, 0, .25), Line(c, d, .25, .75), Line(d, b, .75, 1)];
        }
        if (mode != ChartLineInterpolation.Smooth || a.X == b.X) return [Line(a, b, 0, 1)];
        var dx = b.X - a.X; var slope = (b.Y - a.Y) / dx;
        var left = slope; var right = slope;
        if (i > 0)
        {
            var previous = Value(i - 1); var width = a.X - previous.X;
            left = width != 0 && Math.Sign(width) == Math.Sign(dx) ? Tangent((a.Y - previous.Y) / width, slope) : 0;
        }
        if (i + 2 < points.Count)
        {
            var next = Value(i + 2); var width = next.X - b.X;
            right = width != 0 && Math.Sign(width) == Math.Sign(dx) ? Tangent(slope, (next.Y - b.Y) / width) : 0;
        }
        var c1 = new Point(a.X + dx / 3, a.Y + left * dx / 3);
        var c2 = new Point(b.X - dx / 3, b.Y - right * dx / 3);
        if (!Finite(c1) || !Finite(c2)) return [Line(a, b, 0, 1)];
        return [new(first, last, a, c1, c2, b, true)];
    }

    // A local, shape-preserving Hermite tangent: no overshoot at extrema and
    // bounded controls for unequal spacing, reversed X and source-order turns.
    private static double Tangent(double previous, double next)
    {
        if (!double.IsFinite(previous) || !double.IsFinite(next) || previous == 0 || next == 0 || Math.Sign(previous) != Math.Sign(next)) return 0;
        var magnitude = Math.Min(Math.Abs(previous), Math.Abs(next));
        return Math.CopySign(magnitude, previous);
    }
    private static bool Finite(Point point) => double.IsFinite(point.X) && double.IsFinite(point.Y);
    private static bool Finite(ChartPoint point) => double.IsFinite(point.X) && double.IsFinite(point.Y);
}
