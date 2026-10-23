using Avalonia;

namespace NT.AvaloniaControl.Charts;

/// <summary>A financial indicator calculated over every mapped source sample before display filtering.</summary>
public abstract class ChartTechnicalIndicator : ChartAnalyticalSeries
{
    public static readonly StyledProperty<int> PeriodProperty = AvaloniaProperty.Register<ChartTechnicalIndicator, int>(nameof(Period), 14,
        validate: value => value is >= 1 and <= 65_536);
    public int Period { get => GetValue(PeriodProperty); set => SetValue(PeriodProperty, value); }
    protected override bool IsCalculationProperty(AvaloniaProperty property) => property == PeriodProperty;
}

/// <summary>Common implementation for the three independent moving-average indicator objects.</summary>
public abstract class ChartMovingAverageIndicator : ChartTechnicalIndicator
{
    protected abstract ChartMovingAverageType AverageType { get; }
    protected override ChartDataSnapshot Calculate(ChartDataSnapshot input, long version)
    {
        var values = ChartAnalytics.MovingAverage(input.Points.Select(point => point.IsEmpty ? point.Point with { Y = double.NaN } : point.Point), Period, AverageType);
        var points = new ChartDataPoint[input.Points.Count];
        for (var index = 0; index < points.Length; index++) points[index] = ProjectEndpoint(input.Points[index], values[index]);
        return ChartDataSnapshot.FromOwned(version, points, input.Issues);
    }
}

public sealed class SMAIndicator : ChartMovingAverageIndicator
{
    protected override ChartMovingAverageType AverageType => ChartMovingAverageType.Simple;
}
public sealed class EMAIndicator : ChartMovingAverageIndicator
{
    protected override ChartMovingAverageType AverageType => ChartMovingAverageType.Exponential;
}
public sealed class WMAIndicator : ChartMovingAverageIndicator
{
    protected override ChartMovingAverageType AverageType => ChartMovingAverageType.Weighted;
}

/// <summary>A population-standard-deviation envelope with independently styled middle, upper, lower lines and band.</summary>
/// <remarks>This object's line is the middle average. UpperSeries, LowerSeries and BandSeries have independent legend and visibility settings.</remarks>
public sealed class BollingerBandsIndicator : ChartTechnicalIndicator
{
    public static readonly StyledProperty<double> StandardDeviationMultiplierProperty =
        AvaloniaProperty.Register<BollingerBandsIndicator, double>(nameof(StandardDeviationMultiplier), 2,
            validate: value => double.IsFinite(value) && value >= 0);
    public double StandardDeviationMultiplier { get => GetValue(StandardDeviationMultiplierProperty); set => SetValue(StandardDeviationMultiplierProperty, value); }
    /// <summary>The middle SMA line (this indicator).</summary>
    public ChartSeries MiddleSeries => this;
    public ChartSeries UpperSeries { get; }
    public ChartSeries LowerSeries { get; }
    public ChartSeries BandSeries { get; }
    public BollingerBandsIndicator()
    {
        Period = 20;
        Title = "Bollinger middle";
        UpperSeries = new BollingerBoundarySeries(this, true) { Title = "Bollinger upper" };
        LowerSeries = new BollingerBoundarySeries(this, false) { Title = "Bollinger lower" };
        BandSeries = new BollingerBandSeries(this) { Title = "Bollinger band", ShowInLegend = false, StrokeThickness = 0 };
    }
    protected override bool IsCalculationProperty(AvaloniaProperty property) => base.IsCalculationProperty(property) || property == StandardDeviationMultiplierProperty;
    protected override ChartDataSnapshot Calculate(ChartDataSnapshot input, long version)
    {
        var bands = ChartAnalytics.BollingerBands(input.Points.Select(point => point.IsEmpty ? point.Point with { Y = double.NaN } : point.Point), Period, StandardDeviationMultiplier);
        var points = new ChartDataPoint[input.Points.Count];
        for (var index = 0; index < points.Length; index++)
            points[index] = ProjectEndpoint(input.Points[index], new(bands[index].X, bands[index].Middle)) with
            { High = double.IsFinite(bands[index].Upper) ? bands[index].Upper : null, Low = double.IsFinite(bands[index].Lower) ? bands[index].Lower : null };
        return ChartDataSnapshot.FromOwned(version, points, input.Issues);
    }

    private abstract class BollingerComponentSeries : ChartAnalyticalSeries
    {
        protected BollingerComponentSeries(BollingerBandsIndicator owner) { Owner = owner; SourceSeries = owner; }
        protected BollingerBandsIndicator Owner { get; }
        internal override bool IsEffectivelyVisible => IsVisible && Owner.IsVisible;
        protected ChartDataPoint ComponentPoint(ChartDataPoint point, double value) => point with
        { Point = point.Point with { Y = value }, IsEmpty = point.IsEmpty || !double.IsFinite(value),
            Analysis = point.Analysis is { } analysis ? analysis with { CalculatedPoint = point.Point with { Y = value } } : null };
    }
    private sealed class BollingerBoundarySeries(BollingerBandsIndicator owner, bool upper) : BollingerComponentSeries(owner)
    {
        protected override ChartDataSnapshot Calculate(ChartDataSnapshot input, long version) =>
            ChartDataSnapshot.FromOwned(version, input.Points.Select(point => ComponentPoint(point, (upper ? point.High : point.Low) ?? double.NaN)).ToArray(), input.Issues);
    }
    private sealed class BollingerBandSeries(BollingerBandsIndicator owner) : BollingerComponentSeries(owner), IChartRangeSeries
    {
        public override IChartSeriesRenderer Renderer => ChartLineSeriesRenderer.RangeArea;
        protected override ChartDataSnapshot Calculate(ChartDataSnapshot input, long version)
        {
            var points = input.Points.Select(point => ComponentPoint(point, point.High ?? double.NaN)).ToArray();
            var data = ChartDataSnapshot.FromOwned(version, points, input.Issues);
            return data.WithBandValues(points.Select(point => new ChartDataValue(point.SourceIndex, point.Point, point.IsEmpty, point.WasEmpty)).ToArray(),
                points.Select(point => new ChartBandValue(point.Low ?? double.NaN, null, point.RawPoint ?? point.Point)).ToArray());
        }
    }
}

internal interface IChartRangeSeries { }
