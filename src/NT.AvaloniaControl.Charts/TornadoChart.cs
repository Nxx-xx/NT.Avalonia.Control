namespace NT.AvaloniaControl.Charts;

/// <summary>Bilateral comparison with the largest category impact shown first; equal impacts keep source order.</summary>
public sealed class TornadoChart : PairedBarChartBase
{
    public TornadoChart() => Sort = ComparisonSort.ValueDescending;
}
