using System.Runtime.CompilerServices;
using Avalonia;

namespace NT.AvaloniaControl.Charts;

internal sealed partial class ChartComparisonSeriesRenderer
{
    private static readonly ConditionalWeakTable<ChartDataSnapshot, CumulativeOrder> CumulativeOrders = new();
    private sealed class CumulativeOrder
    {
        internal readonly object Sync = new();
        internal int[]? Horizontal, Vertical;
        internal int[] Get(ChartDataSnapshot data, bool horizontal)
        {
            lock (Sync)
            {
                ref var order = ref (horizontal ? ref Horizontal : ref Vertical);
                // Category order is distinct from original item identity. Keep
                // invalid value rows in that order so they continue to split paths.
                return order ??= Enumerable.Range(0, data.Count)
                    .OrderBy(i => ChartComparisonGeometry.Category(data.GetValue(i), horizontal)).ToArray();
            }
        }
    }

    private static IEnumerable<int> CumulativeCandidates(ChartSeriesRenderContext context, ChartDataSnapshot data,
        IChartProjection projection, bool horizontal, double offset, double inflation, bool fullPaintBounds)
    {
        var order = CumulativeOrders.GetValue(data, static _ => new()).Get(data, horizontal);
        var secondary = context with { Data = data, Projection = projection };
        // Account for the grouped category offset before inverse mapping.
        var plot = horizontal ? context.PlotArea.Translate(new Vector(0, -offset)) : context.PlotArea.Translate(new Vector(-offset, 0));
        var first = 0; var last = order.Length;
        if (!fullPaintBounds && ChartVisibilityQuery.TryBounds(secondary, plot, inflation, !horizontal, horizontal, out var bounds))
        {
            var minimum = horizontal ? bounds.MinimumY : bounds.MinimumX;
            var maximum = horizontal ? bounds.MaximumY : bounds.MaximumX;
            first = Math.Max(0, Bound(minimum, false) - 1);
            last = Math.Min(order.Length, Bound(maximum, true) + 1);
        }
        for (var cursor = first; cursor < last; cursor++) yield return order[cursor];

        int Bound(double target, bool upper)
        {
            var low = 0; var high = order.Length;
            while (low < high)
            {
                var middle = low + (high - low) / 2;
                var category = ChartComparisonGeometry.Category(data.GetValue(order[middle]), horizontal);
                // Enumerable.OrderBy places NaNs first; preserve the same order.
                if (double.IsNaN(category) || category < target || upper && category == target) low = middle + 1;
                else high = middle;
            }
            return low;
        }
    }
}
