using System.Runtime.CompilerServices;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

internal sealed record ChartSamplingPlan(ChartSamplingInfo Info, ChartLineSampling? Sampling);

internal static class ChartSamplingPlanner
{
    private readonly record struct Style(ChartSamplingMode Mode, int PixelWidth, ChartLineInterpolation Interpolation,
        ChartStackMode Stack, double MarkerRadius, bool Labels, bool Solid, double StrokeThickness, double RenderScale, bool Disabled);
    private sealed class Entry { internal Style? Style; internal ChartSamplingPlan? Plan; }
    private static readonly ConditionalWeakTable<ChartSeriesRenderContext, Entry> Cache = new();
    private static long _planBuildCount, _planReuseCount;
    internal static long PlanBuildCount => Interlocked.Read(ref _planBuildCount);
    internal static long PlanReuseCount => Interlocked.Read(ref _planReuseCount);

    internal static ChartSamplingPlan For(ChartSeriesRenderContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var line = context.Series as LineSeries;
        var style = new Style(line?.SamplingMode ?? ChartSamplingMode.None, line?.SamplingPixelWidth ?? 1,
            line?.Interpolation ?? ChartLineInterpolation.Linear, line?.StackMode ?? ChartStackMode.None,
            ChartMarkerGeometry.Radius(context.Series), context.Series.ShowDataLabels,
            context.Brush is ISolidColorBrush, ChartMarkerGeometry.Size(context.Series.StrokeThickness), context.RenderScaling, context.SamplingDisabled);
        // A caller-owned projection can depend on mutable state without notifications.
        if (context.Projection is not CartesianChart.CartesianProjection { HasStableTransform: true })
            return Build(context, style, cancellationToken);
        var entry = Cache.GetValue(context, static _ => new());
        lock (entry)
        {
            if (entry.Style == style && entry.Plan is { } cached)
            { Interlocked.Increment(ref _planReuseCount); return cached; }
            var plan = Build(context, style, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            entry.Style = style; entry.Plan = plan;
            return plan;
        }
    }

    private static ChartSamplingPlan Build(ChartSeriesRenderContext context, Style style, CancellationToken token)
    {
        Interlocked.Increment(ref _planBuildCount);
        var reason = Reason(context, style);
        if (reason is null)
        {
            var index = ChartSamplingIndex.For(context.Data, token);
            // One physical antialias pixel can occupy more than one DIP when
            // exporting a reduced image. Keep that support outside the plot too.
            var guard = 5 * style.StrokeThickness + Math.Max(1, 1 / context.RenderScaling);
            if (!index.IsMonotonicX) reason = "UnorderedX";
            else if (index.TryQuery(MapXFor(context), context.PlotArea.Left - guard, context.PlotArea.Right + guard,
                context.RenderScaling, style.PixelWidth, out var result, token))
            {
                return new(new(style.Mode, result.IsReduced, context.Data.Count, result.InputCount, result.Count,
                    result.FigureCount, result.BucketBudget, context.RenderScaling, style.PixelWidth,
                    result.IsReduced ? null : "BelowPixelBudget"), result);
            }
            else reason = "UnboundedPixelBudget";
        }
        token.ThrowIfCancellationRequested();
        var candidates = RawCandidates(context);
        var count = candidates.IsFull ? ChartDataExtrema.For(context.Data).Count : CountFinite(context.Data, candidates, token);
        return new(new(style.Mode, false, context.Data.Count, count, count, 0, 0,
            context.RenderScaling, style.PixelWidth, reason), null);
    }

    private static string? Reason(ChartSeriesRenderContext context, Style style)
    {
        if (style.Disabled) return "ExportUsesOriginalData";
        if (style.Mode == ChartSamplingMode.None) return "Disabled";
        if (context.Series is not LineSeries) return "UnsupportedSeries";
        if (style.Interpolation != ChartLineInterpolation.Linear) return "InterpolationRequiresOriginalData";
        if (style.Stack != ChartStackMode.None) return "StackRequiresOriginalData";
        if (style.MarkerRadius > 0) return "MarkersRequireOriginalData";
        if (style.Labels) return "LabelsRequireOriginalData";
        if (!style.Solid) return "SpatialBrushRequiresOriginalData";
        if (context.Projection is not CartesianChart.CartesianProjection { HasStableTransform: true } projection)
            return "UnknownProjection";
        if (projection.XScale.MergedBreaks.Count > 0 || projection.YScale.MergedBreaks.Count > 0)
            return "ScaleBreakRequiresOriginalData";
        if (!double.IsFinite(style.RenderScale) || style.RenderScale <= 0) return "InvalidRenderScale";
        var extrema = ChartDataExtrema.For(context.Data);
        if (extrema.Bounds is not { } bounds) return "NoFiniteData";
        if (!extrema.AscendingX && !extrema.DescendingX) return "UnorderedX";
        if (!projection.XScale.IsValidValue(bounds.XMinimum) || !projection.XScale.IsValidValue(bounds.XMaximum) ||
            !projection.YScale.IsValidValue(bounds.YMinimum) || !projection.YScale.IsValidValue(bounds.YMaximum))
            return "InvalidAxisDomain";
        var x0 = projection.MapX(bounds.XMinimum); var x1 = projection.MapX(bounds.XMaximum);
        var y0 = projection.MapY(bounds.YMinimum); var y1 = projection.MapY(bounds.YMaximum);
        if (!Bounded(x0) || !Bounded(x1) || !Bounded(y0) || !Bounded(y1) ||
            !Bounded(context.PlotArea.Left) || !Bounded(context.PlotArea.Right)) return "SaturatedTransform";
        return null;
    }

    private static bool Bounded(double value) => double.IsFinite(value) && Math.Abs(value) < 999_999;
    // Keep pixel-column classification identical to the actual native line
    // arithmetic, including the pre-existing bounded FMA fast adapter.
    internal static Func<double, double> MapXFor(ChartSeriesRenderContext context)
    {
        if (context.Projection is CartesianChart.CartesianProjection projection &&
            (projection.TryGetLinearLineProjection(context.Data, out var mapping) ||
             projection.TryGetGappedLinearProjection(context.Data, out mapping)))
            return x => mapping.Project(new ChartPoint(x, 0)).X;
        return context.Projection.MapX;
    }
    private static ChartVisibleOrdinals RawCandidates(ChartSeriesRenderContext context) =>
        context.Series is LineSeries { Interpolation: ChartLineInterpolation.Linear } && context.Data.Count < 8_192 ||
        context.Brush is not ISolidColorBrush ? ChartVisibleOrdinals.All(context.Data.Count) :
        ChartVisibilityQuery.Segments(context, 5 * ChartMarkerGeometry.Size(context.Series.StrokeThickness) + 1, neighbors: 1);

    private static int CountFinite(ChartDataSnapshot data, ChartVisibleOrdinals candidates, CancellationToken token)
    {
        var count = 0;
        foreach (var ordinal in candidates)
        {
            if ((count & 255) == 0) token.ThrowIfCancellationRequested();
            var point = data.GetValue(ordinal).Point;
            if (double.IsFinite(point.X) && double.IsFinite(point.Y)) count++;
        }
        return count;
    }
}
