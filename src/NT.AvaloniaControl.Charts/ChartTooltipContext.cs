using Avalonia;

namespace NT.AvaloniaControl.Charts;

/// <summary>
/// One immutable tooltip reading. Coordinates are chart-local DIPs and hits retain
/// their snapshot Item and StableId. Analytical fitted samples are explicitly identified by Analysis.IsSynthetic.
/// </summary>
public sealed record ChartTooltipContext(ChartPointHit Primary, IReadOnlyList<ChartPointHit> Hits,
    string Text, Point Anchor)
{
    /// <summary>Displayed values, including separately identified segment estimates. Original point hits remain unchanged.</summary>
    public IReadOnlyList<ChartTrackballReading> Readings { get; init; } = Array.Empty<ChartTrackballReading>();
    public IReadOnlyList<ChartSegmentHit> Segments { get; init; } = Array.Empty<ChartSegmentHit>();
    public ChartSegmentHit? PrimarySegment { get; init; }
}
