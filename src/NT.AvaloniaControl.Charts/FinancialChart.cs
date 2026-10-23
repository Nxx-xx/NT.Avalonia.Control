using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>A financial composition with one shared trading-slot calendar and independent price/volume axes.</summary>
/// <remarks>SetData takes a defensive typed snapshot. Call SetData again after editing source values; this host does not observe a mutable business source.
/// All built-in interaction, annotation and export APIs are available through Chart. Extra series share this SetData calendar through MapTime/ProjectOverlay.
/// Unknown dates map to NaN instead of inventing a trading period. For non-time transforms multiple glyphs can share a date; overlays map to its final slot.</remarks>
public sealed class FinancialChart : Decorator
{
    private IReadOnlyList<ChartFinancialPoint> _data = Array.Empty<ChartFinancialPoint>();
    private IReadOnlyList<ChartFinancialSample> _samples = Array.Empty<ChartFinancialSample>();
    private Dictionary<DateTimeOffset, double> _slots = [];
    public FinancialChart()
    {
        TradingAxis = new CategoryAxis { Title = "交易日期", PlotMode = CategoryPlotMode.OnTicks, LabelMode = ChartAxisLabelMode.Auto };
        PriceAxis = new NumericAxis { Title = "价格", Key = "financial-price" };
        VolumeAxis = new NumericAxis { Title = "成交量", Key = "financial-volume", Minimum = 0, Position = ChartAxisPosition.Far, ShowGridLines = false, LabelFormat = "0.#" };
        Chart = new CartesianChart { XAxis = TradingAxis, YAxis = PriceAxis, IsZoomEnabled = true, IsPanEnabled = true,
            IsTooltipEnabled = true, ShowCrosshair = true, ShowCrosshairLabels = true, TooltipMode = ChartTooltipMode.SharedX };
        Chart.Axes.Add(VolumeAxis);
        VolumeSeries = new FinancialVolumeSeries { Title = "成交量", YAxisKey = VolumeAxis.Key, StrokeThickness = 0,
            RisingBrush = Brush.Parse("#260A8A6A"), FallingBrush = Brush.Parse("#26CB4458"), NeutralBrush = Brush.Parse("#2665758A") };
        PriceSeries = new FinancialSeries { Title = "价格", YAxisKey = PriceAxis.Key };
        Chart.Series.Add(VolumeSeries); Chart.Series.Add(PriceSeries);
        Child = Chart;
    }
    public CartesianChart Chart { get; }
    public FinancialSeries PriceSeries { get; }
    public FinancialVolumeSeries VolumeSeries { get; }
    public NumericAxis PriceAxis { get; }
    public NumericAxis VolumeAxis { get; }
    public CategoryAxis TradingAxis { get; }
    public ObservableCollection<ChartSeries> Series => Chart.Series;
    public IReadOnlyList<ChartFinancialPoint> Data => _data;
    public IReadOnlyList<ChartFinancialSample> Samples => _samples;
    public string DateFormat { get; set; } = "MM-dd";

    public void SetData(IEnumerable<ChartFinancialPoint> source)
    {
        VerifyAccess(); ArgumentNullException.ThrowIfNull(source);
        var copy = Array.AsReadOnly(source.ToArray());
        var transformed = PriceSeries.TransformPoints(copy);
        Publish(copy, transformed);
    }
    public void ApplyTransform(ChartFinancialTransformKind kind)
    {
        VerifyAccess();
        var previous = PriceSeries.Transform;
        PriceSeries.Transform = kind;
        try { Publish(_data, PriceSeries.TransformPoints(_data)); }
        catch { PriceSeries.Transform = previous; throw; }
    }
    private void Publish(IReadOnlyList<ChartFinancialPoint> data, IReadOnlyList<ChartFinancialSample> samples)
    {
        var labels = samples.Count == 0 ? Array.Empty<string>() : new string[samples.Max(sample => sample.Slot) + 1];
        Array.Fill(labels, string.Empty);
        var slots = new Dictionary<DateTimeOffset, double>();
        foreach (var sample in samples) { labels[sample.Slot] = sample.Time.ToString(DateFormat, CultureInfo.CurrentCulture); slots[sample.Time] = sample.Slot; }
        _data = data; _samples = samples; _slots = slots;
        using (PriceSeries.DeferUpdates()) using (VolumeSeries.DeferUpdates())
        {
            VolumeSeries.MaximumGeneratedSamples = PriceSeries.MaximumGeneratedSamples;
            PriceSeries.ItemsSource = samples; VolumeSeries.ItemsSource = samples;
            // Non-time transforms may emit multiple glyphs from the same source record.
            // Repeating that record's volume would invent turnover, so volume is hidden.
            VolumeSeries.IsVisible = PriceSeries.Transform is ChartFinancialTransformKind.Raw or ChartFinancialTransformKind.HeikinAshi;
            TradingAxis.Labels = Array.AsReadOnly(labels);
        }
        Chart.ResetViewport();
    }
    /// <summary>Exact date lookup; NaN for absent dates. A derived date maps to the final slot emitted on that date.</summary>
    public double MapTime(DateTimeOffset time) => _slots.GetValueOrDefault(time, double.NaN);
    public bool TryGetTime(double slot, out DateTimeOffset time)
    {
        if (double.IsFinite(slot))
            for (var index = _samples.Count - 1; index >= 0; index--)
                if (_samples[index].Slot == Math.Round(slot)) { time = _samples[index].Time; return true; }
        time = default; return false;
    }
    /// <summary>Maps integral Unix-millisecond X values to this host's exact trading calendar. Unknown or fractional dates remain gaps.
    /// Use MapTime directly to retain sub-millisecond timestamp precision.</summary>
    public IReadOnlyList<ChartPoint> ProjectOverlay(IEnumerable<ChartPoint> dateValues)
    {
        ArgumentNullException.ThrowIfNull(dateValues);
        return Array.AsReadOnly(dateValues.Select(point => double.IsFinite(point.X) && point.X == Math.Truncate(point.X) && point.X >= DateTimeAxis.MinimumUnixMilliseconds && point.X <= DateTimeAxis.MaximumUnixMilliseconds
            ? new ChartPoint(MapTime(DateTimeOffset.FromUnixTimeMilliseconds((long)point.X)), point.Y) : new ChartPoint(double.NaN, point.Y)).ToArray());
    }
}
