using Avalonia;

namespace NT.AvaloniaControl.Charts;

/// <summary>An immutable reading for an analytical or engineering chart tooltip template.</summary>
/// <param name="Members">Every original observation contributing to the displayed mark, with its original Item, StableId and SourceIndex. An aggregate is never represented as an invented observation.</param>
/// <param name="Text">The family's standard tooltip text.</param>
/// <param name="Anchor">The displayed mark anchor in chart-local DIPs.</param>
public sealed record ChartDataTooltipContext(IReadOnlyList<ChartDataPoint> Members, string Text, Point Anchor)
{
    /// <summary>The chart's business DataContext at the time of this reading, distinct from the tooltip's reading DataContext.</summary>
    public object? BusinessDataContext { get; init; }
    /// <summary>Current engineering mark and aggregate metadata; null for analytical charts.</summary>
    public EngineeringPointHit? EngineeringHit { get; init; }
    /// <summary>Current original analytical point; null for engineering charts.</summary>
    public ChartPointHit? PointHit { get; init; }
}
