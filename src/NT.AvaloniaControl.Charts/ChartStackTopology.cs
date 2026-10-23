using System.Runtime.CompilerServices;

namespace NT.AvaloniaControl.Charts;

/// <summary>Complete stack topology, independent from viewport and cropped geometry.</summary>
internal static class ChartStackTopology
{
    private const int MaximumProfiles = 8;
    internal readonly record struct Match(bool SameRuns, bool SameX);
    private sealed record Comparison(Match Value);
    private static readonly ConditionalWeakTable<ChartDataSnapshot, Entry> Cache = new();
    private static long _buildCount;
    internal static long BuildCount => Interlocked.Read(ref _buildCount);
    internal static int CachedProfileCount(ChartDataSnapshot data)
    {
        if (!Cache.TryGetValue(data, out var entry)) return 0;
        lock (entry.Values) return entry.Values.Count;
    }
    private sealed class Entry { internal readonly List<Topology> Values = []; }
    private sealed class Topology(ChartDataSnapshot data, double baseline, ChartScale x, ChartScale y)
    {
        internal readonly ChartDataSnapshot Data = data;
        internal readonly double Baseline = baseline;
        internal readonly int XKind = x.DomainKind, YKind = y.DomainKind;
        internal readonly ChartScaleBreakRange[] XBreaks = x.MergedBreaks.ToArray(), YBreaks = y.MergedBreaks.ToArray();
        internal readonly int[] Runs = FindRuns(data, baseline, x, y);
        internal readonly ConditionalWeakTable<Topology, Comparison> Comparisons = new();
        private static int[] FindRuns(ChartDataSnapshot data, double baseline, ChartScale x, ChartScale y)
        {
            Interlocked.Increment(ref _buildCount);
            var runs = new List<int>(); var first = -1;
            for (var i = 0; i < data.Count; i++)
            {
                var value = data.GetValue(i); var lower = data.GetBandLower(i) ?? baseline;
                var valid = !value.IsEmpty && x.IsValidValue(value.Point.X) && y.IsValidValue(value.Point.Y) &&
                    (y.IsValidValue(lower) || lower == 0);
                if (valid) { if (first < 0) first = i; }
                else Flush(i);
            }
            Flush(data.Count); return runs.ToArray();
            void Flush(int end) { if (first >= 0 && end - first >= 2) { runs.Add(first); runs.Add(end); } first = -1; }
        }
    }

    internal static Match Compare(ChartSeriesRenderContext first, ChartSeriesRenderContext last)
    {
        if (first.Projection is not CartesianChart.CartesianProjection { HasStableTransform: true } a ||
            last.Projection is not CartesianChart.CartesianProjection { HasStableTransform: true } b)
            return new(true, true); // Extension projections already retain full geometry.
        var left = Get(first, a); var right = Get(last, b);
        if (ReferenceEquals(left, right)) return new(true, true);
        return left.Comparisons.GetValue(right, value => new(CompareCore(left, value))).Value;
    }

    private static Topology Get(ChartSeriesRenderContext context, CartesianChart.CartesianProjection projection)
    {
        var entry = Cache.GetValue(context.Data, static _ => new());
        var baseline = context.Series is AreaSeries area ? area.Baseline : double.NaN;
        lock (entry.Values)
        {
            var existing = entry.Values.FirstOrDefault(item => item.Baseline.Equals(baseline) &&
                item.XKind == projection.XScale.DomainKind && item.YKind == projection.YScale.DomainKind &&
                SameBreaks(item.XBreaks, projection.XScale.MergedBreaks) &&
                SameBreaks(item.YBreaks, projection.YScale.MergedBreaks));
            if (existing is not null) return existing;
            var created = new Topology(context.Data, baseline, projection.XScale, projection.YScale);
            // A long-lived source may be reused while callers edit baselines or
            // breaks. Retain only a bounded set of complete-domain summaries.
            if (entry.Values.Count == MaximumProfiles) entry.Values.RemoveAt(0);
            entry.Values.Add(created); return created;
        }
    }

    private static bool SameBreaks(ChartScaleBreakRange[] first, IReadOnlyList<ChartScaleBreakRange> last)
    {
        if (first.Length != last.Count) return false;
        for (var i = 0; i < first.Length; i++) if (first[i] != last[i]) return false;
        return true;
    }

    private static Match CompareCore(Topology first, Topology last)
    {
        if (!first.Runs.AsSpan().SequenceEqual(last.Runs)) return new(false, false);
        for (var run = 0; run < first.Runs.Length; run += 2)
            for (var i = first.Runs[run]; i < first.Runs[run + 1]; i++)
                if (first.Data.GetValue(i).Point.X != last.Data.GetValue(i).Point.X) return new(true, false);
        return new(true, true);
    }
}
