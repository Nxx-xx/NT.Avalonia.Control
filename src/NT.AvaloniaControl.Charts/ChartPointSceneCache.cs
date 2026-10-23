using System.Runtime.CompilerServices;
using Avalonia;

namespace NT.AvaloniaControl.Charts;

/// <summary>Derived point paint order and a lazy screen-space hit index for one immutable view.</summary>
internal static partial class ChartPointSceneCache
{
    private static readonly ConditionalWeakTable<ChartSeriesRenderContext, Entry> Cache = new();
    private static readonly ConditionalWeakTable<ChartDataSnapshot, MaximumDiameter> Diameters = new();
    private static long _pointOrderBuildCount;
    private static long _hitIndexBuildCount;
    internal static long PointOrderBuildCount => Interlocked.Read(ref _pointOrderBuildCount);
    internal static long HitIndexBuildCount => Interlocked.Read(ref _hitIndexBuildCount);
    private sealed class MaximumDiameter(ChartDataSnapshot data)
    {
        internal readonly double Value = FindMaximum(data);
        private static double FindMaximum(ChartDataSnapshot data)
        {
            var result = 0d;
            for (var i = 0; i < data.Count; i++)
            {
                var diameter = data.GetPointStyle(i)?.Diameter ?? data.Points[i].MarkerDiameter ?? 0;
                if (double.IsFinite(diameter) && diameter > result) result = diameter;
            }
            return result;
        }
    }

    private readonly record struct Style(ChartPointDrawOrder Order, double MarkerSize, bool ShowMarkers,
        double StrokeThickness, ChartMarkerShape Shape);
    private sealed class Entry
    {
        public readonly object Sync = new();
        public Style? Style;
        public IEnumerable<int>? Order;
        public int Count;
        public HitIndex? HitIndex;
    }

    private static Style CurrentStyle(ChartSeries series) => new(series is IChartPointSeries options ? options.DrawOrder : ChartPointDrawOrder.Source,
        series.MarkerSize, series.ShowMarkers, ChartMarkerGeometry.StrokeThickness(series), series.MarkerShape);

    internal static IEnumerable<int> PointOrder(ChartSeriesRenderContext context)
    {
        var entry = Cache.GetValue(context, static _ => new());
        lock (entry.Sync) return EnsureOrder(context, entry);
    }

    internal static int VisiblePointCount(ChartSeriesRenderContext context)
    {
        var entry = Cache.GetValue(context, static _ => new());
        lock (entry.Sync) { EnsureOrder(context, entry); return entry.Count; }
    }

    private static IEnumerable<int> EnsureOrder(ChartSeriesRenderContext context, Entry entry)
    {
        var style = CurrentStyle(context.Series);
        if (entry.Style != style)
        {
            // Standalone renderer consumers can keep a context while replacing marker
            // options. Chart-owned views normally receive a fresh context instead.
            entry.Order = null;
            entry.HitIndex = null; entry.Style = style;
        }
        if (entry.Order is not null) return entry.Order;
        Interlocked.Increment(ref _pointOrderBuildCount);
        var source = ChartVisibilityQuery.Points(context);
        if (source.Count != context.Data.Count)
        {
            // Encoded radii belong to the immutable styled snapshot. Retain only
            // their maximum, rather than an N-item copy for every viewport.
            var diameter = Math.Max(style.MarkerSize, Diameters.GetValue(context.Data, static data => new(data)).Value);
            var extent = new ChartPointShape(default, diameter, style.StrokeThickness, style.Shape).Extent;
            source = ChartVisibilityQuery.Points(context, inflation: extent + 1);
        }
        entry.Count = source.Count;
        return entry.Order = style.Order switch
        {
            ChartPointDrawOrder.SizeDescending => source.OrderByDescending(index => context.Data.Points[index].MarkerDiameter ?? style.MarkerSize).ToArray(),
            ChartPointDrawOrder.SizeAscending => source.OrderBy(index => context.Data.Points[index].MarkerDiameter ?? style.MarkerSize).ToArray(),
            _ => source,
        };
    }

    internal static HitIndex ForHitTest(ChartSeriesRenderContext context)
    {
        var entry = Cache.GetValue(context, static _ => new());
        lock (entry.Sync)
        {
            var order = EnsureOrder(context, entry);
            // Public projections may read caller-owned mutable state without a
            // revision contract. Only the chart's immutable projection is retained.
            if (context.Projection is not CartesianChart.CartesianProjection { HasStableTransform: true }) return new(context, order);
            return entry.HitIndex ??= new(context, order);
        }
    }

    internal readonly record struct HitEntry(ChartPointShape Shape, int SourceIndex, int PaintOrder);

    internal sealed partial class HitIndex
    {
        private readonly HitEntry[] _entries;
        private readonly double _maximumExtent;
        internal int Count { get; }

        internal HitIndex(ChartSeriesRenderContext context, IEnumerable<int> order)
        {
            Interlocked.Increment(ref _hitIndexBuildCount);
            _entries = new HitEntry[order.TryGetNonEnumeratedCount(out var length) ? length : order.Count()];
            var count = 0; var paintOrder = 0; var maximumExtent = 0d;
            foreach (var ordinal in order)
            {
                var point = context.Data.Points[ordinal];
                if (ChartPointGeometry.TryCreate(context, point, out var shape) && shape.Bounds.Intersects(context.PlotArea))
                {
                    _entries[count++] = new(shape, point.SourceIndex, paintOrder);
                    maximumExtent = Math.Max(maximumExtent, shape.Extent);
                }
                paintOrder++;
            }
            Count = count; _maximumExtent = maximumExtent;
            Array.Sort(_entries, 0, count, Comparer<HitEntry>.Create(static (first, last) => first.Shape.Center.X.CompareTo(last.Shape.Center.X)));
        }

        internal HitEntry this[int index] => _entries[index];

        internal (int Start, int End) CandidateRange(Point position, double tolerance)
        {
            var extent = _maximumExtent + tolerance;
            // Match the original exact footprint arithmetic at boundaries; this
            // guard affects candidates only and never changes final hit distance.
            var guard = 1e-9 * Math.Max(1, Math.Max(Math.Abs(position.X), extent));
            return (Bound(position.X - extent - guard, false), Bound(position.X + extent + guard, true));
        }

        private int Bound(double value, bool upper)
        {
            var low = 0; var high = Count;
            while (low < high)
            {
                var middle = low + (high - low) / 2;
                var x = _entries[middle].Shape.Center.X;
                if (x < value || upper && x == value) low = middle + 1; else high = middle;
            }
            return low;
        }
    }
}
