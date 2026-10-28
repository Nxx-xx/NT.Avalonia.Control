using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>A real professional glyph in a vertical or horizontal displayed-coordinate slice.
/// Members retain original source identity; a glyph may contain multiple observations. This is not a Cartesian interpolation.</summary>
public sealed record ChartDataTrackballReading(IReadOnlyList<ChartDataPoint> Members, string Text, Point Position)
{
    public object? BusinessDataContext { get; init; }
    public Rect Bounds { get; init; }
    public IBrush? Brush { get; init; }
    public double Value { get; init; }
    public EngineeringPointHit? EngineeringHit { get; init; }
    public ChartPointHit? PointHit { get; init; }
}

/// <summary>One passive Trackball card. Group mode contains all retained readings; float mode contains one.
/// TotalReadings counts the complete slice before MaxTrackballReadings, in stable family painting order.</summary>
public sealed record ChartDataTrackballContext(IReadOnlyList<ChartDataTrackballReading> Readings, Point Anchor)
{
    public object? BusinessDataContext { get; init; }
    public string? Text { get; init; }
    public int TotalReadings { get; init; }
    public bool IsTruncated { get; init; }
}

internal static class ChartDataTrackballGeometry
{
    internal static bool Visible(Rect bounds, Rect plot) => double.IsFinite(bounds.X) && double.IsFinite(bounds.Y) &&
        double.IsFinite(bounds.Width) && double.IsFinite(bounds.Height) && bounds.Width > 0 && bounds.Height > 0 &&
        bounds.Intersects(plot);

    internal static double Distance(Rect bounds, Point point, ChartTrackballMode mode)
    {
        var coordinate = mode == ChartTrackballMode.Horizontal ? point.Y : point.X;
        var minimum = mode == ChartTrackballMode.Horizontal ? bounds.Top : bounds.Left;
        var maximum = mode == ChartTrackballMode.Horizontal ? bounds.Bottom : bounds.Right;
        return coordinate < minimum ? minimum - coordinate : coordinate > maximum ? coordinate - maximum : 0;
    }
}
