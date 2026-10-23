using Avalonia;

namespace NT.AvaloniaControl.Charts;

/// <summary>Optional display reduction. Original data, point readouts and selection are retained.</summary>
public enum ChartSamplingMode { None, MinMax }

/// <summary>A current line display plan; counts refer to vertices, not markers or source items copied into a new series.</summary>
/// <param name="RequestedMode">The series' requested sampling mode.</param>
/// <param name="IsApplied">Whether the plan actually reduced the eligible vertex count.</param>
/// <param name="SourceCount">The complete immutable source snapshot count.</param>
/// <param name="VisibleCount">Finite input vertices in the plan, including required boundary neighbors.</param>
/// <param name="RenderedCount">Finite vertices submitted by the plan. Unsupported plans report the conservative candidate count.</param>
/// <param name="FigureCount">Sampled continuous figures; zero when no sampled plan was produced.</param>
/// <param name="PixelBucketCount">Physical-pixel bucket budget across the plot and its stroke/antialias guard, including its inclusive right edge.</param>
/// <param name="RenderScale">Physical pixels per chart DIP used for this plan.</param>
/// <param name="PixelWidth">Physical pixels in each bucket.</param>
/// <param name="FallbackReason">A stable diagnostic code when reduction is disabled, unsupported or unnecessary.</param>
public sealed record ChartSamplingInfo(ChartSamplingMode RequestedMode, bool IsApplied, int SourceCount,
    int VisibleCount, int RenderedCount, int FigureCount, int PixelBucketCount, double RenderScale,
    int PixelWidth, string? FallbackReason);

public sealed partial class LineSeries
{
    public static readonly StyledProperty<ChartSamplingMode> SamplingModeProperty =
        AvaloniaProperty.Register<LineSeries, ChartSamplingMode>(nameof(SamplingMode), ChartSamplingMode.None, validate: Enum.IsDefined);
    public static readonly StyledProperty<int> SamplingPixelWidthProperty =
        AvaloniaProperty.Register<LineSeries, int>(nameof(SamplingPixelWidth), 1, validate: value => value is >= 1 and <= 64);
    /// <summary>Opt-in first/last/min/max reduction for monotonic, linear, unstacked paths without markers or labels.</summary>
    public ChartSamplingMode SamplingMode { get => GetValue(SamplingModeProperty); set => SetValue(SamplingModeProperty, value); }
    /// <summary>Physical pixels per MinMax bucket, from 1 to 64. Larger values retain a coarser envelope.</summary>
    public int SamplingPixelWidth { get => GetValue(SamplingPixelWidthProperty); set => SetValue(SamplingPixelWidthProperty, value); }
}
