using Avalonia;

namespace NT.AvaloniaControl.Charts;

/// <summary>Source-ordered layers of an upright triangle, with positive area proportional to each value.</summary>
/// <remarks>With zero gap, a boundary's height fraction is the square root of its cumulative value share.
/// Negative, zero, empty and nonfinite values do not create layers; gaps remove paint without changing source values.</remarks>
public sealed class PyramidChart : AnalysisChartBase
{
    public static readonly StyledProperty<double> SegmentGapProperty = AvaloniaProperty.Register<PyramidChart, double>(nameof(SegmentGap), 2,
        validate: value => double.IsFinite(value) && value >= 0 && value <= 4096);

    /// <summary>Maximum empty space between consecutive layers, in DIPs. Short layers clamp their own gap.</summary>
    public double SegmentGap { get => GetValue(SegmentGapProperty); set => SetValue(SegmentGapProperty, value); }

    protected override IReadOnlyList<AnalysisMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot) =>
        FunnelPyramidGeometry.Pyramid(data, plot, SegmentGap, BrushAt);
}
