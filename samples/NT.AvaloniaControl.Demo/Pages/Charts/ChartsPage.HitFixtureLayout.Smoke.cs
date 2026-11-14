using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private static async Task AwaitHitFixtureLayoutAsync(Window window, params CartesianChart[] charts)
    {
        // Summary text, native client-size coercion and wrapping can all enqueue
        // another layout after Show/Refresh. Bounds changes intentionally cancel
        // chart input, so send the synthetic move only in the settled geometry.
        // A delay alone is not evidence that the coordinates are ready.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        Rect[]? previous = null;
        var stablePasses = 0;
        while (!timeout.IsCancellationRequested)
        {
            // Cached validity alone is insufficient: rendering an attached subtree
            // or native size coercion can leave a valid but stale ancestor layout.
            // Exercise the same measure invalidation that installing tooltip content
            // will cause, before projecting/routing any pointer coordinates.
            foreach (var layout in charts.SelectMany(chart => chart.GetVisualAncestors()
                .OfType<Layoutable>().Prepend(chart)).Distinct())
            {
                layout.InvalidateMeasure();
                layout.InvalidateArrange();
            }
            FlushControlTemplateWindow(window);
            var geometry = charts.SelectMany(chart => new[] { chart.Bounds, chart.PlotArea }).ToArray();
            var ready = window.IsMeasureValid && window.IsArrangeValid && charts.All(chart =>
                chart.IsMeasureValid && chart.IsArrangeValid && chart.PlotArea.Width > 0 &&
                chart.PlotArea.Height > 0 && !chart.GetWorkSchedulingInfo().RefreshPending);
            stablePasses = ready && previous is not null && geometry.SequenceEqual(previous) ? stablePasses + 1 : 0;
            if (stablePasses >= 2) return;
            previous = geometry;
            // Let the native event loop deliver pending size/layout work, then
            // verify geometry again. Never treat elapsed time as success.
            try { await Task.Delay(1, timeout.Token); }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested) { break; }
        }
        throw new TimeoutException("Original-data hit-test fixture layout did not settle: " +
            string.Join("; ", charts.Select(chart => $"{chart.Name}: bounds={chart.Bounds}, plot={chart.PlotArea}")));
    }
}
