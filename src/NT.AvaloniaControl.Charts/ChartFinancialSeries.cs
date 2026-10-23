using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

public enum ChartFinancialStyle { Candlestick, Ohlc, HighLow, Renko, Kagi, PointAndFigure }

/// <summary>Prices on equally spaced trading slots, with original OHLC and aggregation ranges retained in Financial hit metadata.</summary>
/// <remarks>Accepts ChartFinancialPoint or ChartFinancialSample values, or maps rows with TimePath/OpenPath/HighPath/LowPath/ClosePath.
/// Timestamps must be strictly increasing; duplicates and disorder are rejected. Invalid prices form explicit segment breaks.
/// A transformed glyph index identifies the displayed glyph; Financial.SourceStartIndex/SourceEndIndex identify its source records.</remarks>
public class FinancialSeries : ChartSeries
{
    public static readonly StyledProperty<string?> TimePathProperty = AvaloniaProperty.Register<FinancialSeries, string?>(nameof(DatePath));
    public static readonly StyledProperty<string?> DatePathProperty = TimePathProperty;
    public static readonly StyledProperty<string?> OpenPathProperty = AvaloniaProperty.Register<FinancialSeries, string?>(nameof(OpenPath));
    public static readonly StyledProperty<string?> ClosePathProperty = AvaloniaProperty.Register<FinancialSeries, string?>(nameof(ClosePath));
    public static readonly StyledProperty<string?> VolumePathProperty = AvaloniaProperty.Register<FinancialSeries, string?>(nameof(VolumePath));
    public static readonly StyledProperty<string?> GapPathProperty = AvaloniaProperty.Register<FinancialSeries, string?>(nameof(GapPath));
    public static readonly StyledProperty<ChartFinancialTransformKind> TransformProperty = AvaloniaProperty.Register<FinancialSeries, ChartFinancialTransformKind>(nameof(Transform), validate: Enum.IsDefined);
    public static readonly StyledProperty<ChartFinancialStyle> StyleProperty = AvaloniaProperty.Register<FinancialSeries, ChartFinancialStyle>(nameof(Style), validate: Enum.IsDefined);
    public static readonly StyledProperty<double> BoxSizeProperty = AvaloniaProperty.Register<FinancialSeries, double>(nameof(BoxSize), 1, validate: value => double.IsFinite(value) && value > 0);
    public static readonly StyledProperty<int> ReversalBoxesProperty = AvaloniaProperty.Register<FinancialSeries, int>(nameof(ReversalBoxes), 3, validate: value => value >= 1 && value <= 10000);
    public static readonly StyledProperty<double> ReversalAmountProperty = AvaloniaProperty.Register<FinancialSeries, double>(nameof(ReversalAmount), 2, validate: value => double.IsFinite(value) && value > 0);
    public static readonly StyledProperty<int> MaximumGeneratedSamplesProperty = AvaloniaProperty.Register<FinancialSeries, int>(nameof(MaximumGeneratedSamples), ChartFinancialTransforms.DefaultMaximumGeneratedSamples,
        validate: value => value >= 1 && value <= ChartFinancialTransforms.MaximumGeneratedSamples);
    public static readonly StyledProperty<double> WidthRatioProperty = AvaloniaProperty.Register<FinancialSeries, double>(nameof(WidthRatio), .72, validate: value => double.IsFinite(value) && value > 0 && value <= 1);
    public static readonly StyledProperty<IBrush?> RisingBrushProperty = AvaloniaProperty.Register<FinancialSeries, IBrush?>(nameof(RisingBrush), Brush.Parse("#0A8A6A"));
    public static readonly StyledProperty<IBrush?> FallingBrushProperty = AvaloniaProperty.Register<FinancialSeries, IBrush?>(nameof(FallingBrush), Brush.Parse("#CB4458"));
    public static readonly StyledProperty<IBrush?> NeutralBrushProperty = AvaloniaProperty.Register<FinancialSeries, IBrush?>(nameof(NeutralBrush), Brush.Parse("#65758A"));
    static FinancialSeries() => ShowMarkersProperty.OverrideDefaultValue<FinancialSeries>(false);
    public string? TimePath { get => GetValue(TimePathProperty); set => SetValue(TimePathProperty, value); }
    /// <summary>Alias for TimePath.</summary>
    public string? DatePath { get => TimePath; set => TimePath = value; }
    public string? OpenPath { get => GetValue(OpenPathProperty); set => SetValue(OpenPathProperty, value); }
    public string? ClosePath { get => GetValue(ClosePathProperty); set => SetValue(ClosePathProperty, value); }
    public string? VolumePath { get => GetValue(VolumePathProperty); set => SetValue(VolumePathProperty, value); }
    public string? GapPath { get => GetValue(GapPathProperty); set => SetValue(GapPathProperty, value); }
    public ChartFinancialTransformKind Transform { get => GetValue(TransformProperty); set => SetValue(TransformProperty, value); }
    public ChartFinancialStyle Style { get => GetValue(StyleProperty); set => SetValue(StyleProperty, value); }
    public double BoxSize { get => GetValue(BoxSizeProperty); set => SetValue(BoxSizeProperty, value); }
    public int ReversalBoxes { get => GetValue(ReversalBoxesProperty); set => SetValue(ReversalBoxesProperty, value); }
    public double ReversalAmount { get => GetValue(ReversalAmountProperty); set => SetValue(ReversalAmountProperty, value); }
    /// <summary>Hard generation and precomputed-sample admission budget; PointAndFigure counts letters, other transforms count glyphs.
    /// No automatic box enlargement or truncation occurs. This is not a bound on input materialization or total process memory.</summary>
    public int MaximumGeneratedSamples { get => GetValue(MaximumGeneratedSamplesProperty); set => SetValue(MaximumGeneratedSamplesProperty, value); }
    public double WidthRatio { get => GetValue(WidthRatioProperty); set => SetValue(WidthRatioProperty, value); }
    public IBrush? RisingBrush { get => GetValue(RisingBrushProperty); set => SetValue(RisingBrushProperty, value); }
    public IBrush? FallingBrush { get => GetValue(FallingBrushProperty); set => SetValue(FallingBrushProperty, value); }
    public IBrush? NeutralBrush { get => GetValue(NeutralBrushProperty); set => SetValue(NeutralBrushProperty, value); }
    public override IChartSeriesRenderer Renderer => ChartFinancialSeriesRenderer.Instance;
    internal IEnumerable<string?> ExtraPaths => [TimePath, OpenPath, ClosePath, VolumePath, GapPath];
    internal IReadOnlyList<ChartFinancialSample> TransformPoints(IEnumerable<ChartFinancialPoint> values) => Transform switch
    {
        ChartFinancialTransformKind.HeikinAshi => ChartFinancialTransforms.HeikinAshi(values, MaximumGeneratedSamples),
        ChartFinancialTransformKind.Renko => ChartFinancialTransforms.Renko(values, BoxSize, ReversalBoxes, MaximumGeneratedSamples),
        ChartFinancialTransformKind.Kagi => ChartFinancialTransforms.Kagi(values, ReversalAmount, MaximumGeneratedSamples),
        ChartFinancialTransformKind.PointAndFigure => ChartFinancialTransforms.PointAndFigure(values, BoxSize, ReversalBoxes, MaximumGeneratedSamples),
        _ => ChartFinancialTransforms.Raw(values, MaximumGeneratedSamples),
    };
}

/// <summary>Volume bars using precisely the same samples and trading slots as a financial price series.</summary>
public sealed class FinancialVolumeSeries : FinancialSeries;

public sealed class CandlestickSeries : FinancialSeries;
public sealed class OhlcSeries : FinancialSeries
{
    static OhlcSeries() => StyleProperty.OverrideDefaultValue<OhlcSeries>(ChartFinancialStyle.Ohlc);
}
public sealed class HiloSeries : FinancialSeries
{
    static HiloSeries() => StyleProperty.OverrideDefaultValue<HiloSeries>(ChartFinancialStyle.HighLow);
}
public sealed class HeikinAshiSeries : FinancialSeries
{
    static HeikinAshiSeries() => TransformProperty.OverrideDefaultValue<HeikinAshiSeries>(ChartFinancialTransformKind.HeikinAshi);
}
public sealed class RenkoSeries : FinancialSeries
{
    static RenkoSeries()
    {
        TransformProperty.OverrideDefaultValue<RenkoSeries>(ChartFinancialTransformKind.Renko);
        ReversalBoxesProperty.OverrideDefaultValue<RenkoSeries>(2);
    }
}
public sealed class KagiSeries : FinancialSeries
{
    static KagiSeries() => TransformProperty.OverrideDefaultValue<KagiSeries>(ChartFinancialTransformKind.Kagi);
}
public sealed class PointAndFigureSeries : FinancialSeries
{
    static PointAndFigureSeries() => TransformProperty.OverrideDefaultValue<PointAndFigureSeries>(ChartFinancialTransformKind.PointAndFigure);
}
