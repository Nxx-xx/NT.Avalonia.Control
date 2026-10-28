using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>The meaning of a professional chart's genuine legend entry.</summary>
public enum ChartDataLegendKind { Mark, Category, Continuous, Size }

/// <summary>A professional data-label reading retaining all original contributing observations.</summary>
/// <param name="Members">Original observations. Aggregate marks retain their complete member list.</param>
/// <param name="Text">The family's label text.</param>
/// <param name="Anchor">Current chart-local label anchor in DIPs.</param>
public sealed record ChartDataLabelItemContext(IReadOnlyList<ChartDataPoint> Members, string Text, Point Anchor)
{
    public Rect Bounds { get; init; }
    public IBrush? Brush { get; init; }
    public double Value { get; init; }
    public object? BusinessDataContext { get; init; }
    public EngineeringPointHit? EngineeringHit { get; init; }
    public ChartPointHit? PointHit { get; init; }
    internal Geometry? GeometryClip { get; init; }
}

/// <summary>A real mark, category or scale legend reading; decorative scales have no invented observations.</summary>
public sealed record ChartDataLegendItemContext(IReadOnlyList<ChartDataPoint> Members, string Text, Point Anchor)
{
    public object? Key { get; init; }
    public ChartDataLegendKind Kind { get; init; }
    public Rect Bounds { get; init; }
    public IBrush? Brush { get; init; }
    public double Value { get; init; }
    public double? Minimum { get; init; }
    public double? Maximum { get; init; }
    public object? BusinessDataContext { get; init; }
    public EngineeringPointHit? EngineeringHit { get; init; }
    public ChartPointHit? PointHit { get; init; }
}
