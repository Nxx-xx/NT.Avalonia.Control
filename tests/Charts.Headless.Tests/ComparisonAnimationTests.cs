using Avalonia.Animation.Easings;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class ComparisonAnimationTests
{
    private sealed record Band(double X, double High, double Low);
    private sealed record Row(string Name, double Value);
    private sealed class Clock : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        public void Advance() => _ticks += TimeSpan.FromMilliseconds(500).Ticks;
    }

    private static Clock Start(CartesianChart chart, Action drain)
    {
        var clock = new Clock(); chart.AnimationTimeProvider = clock;
        chart.AnimationDuration = TimeSpan.FromSeconds(1); chart.AnimationEasing = new LinearEasing();
        chart.AnimationTriggers = ChartAnimationTriggers.Entrance;
        chart.ReplayEntranceAnimation(); drain(); clock.Advance(); chart.UpdateAnimationFrame(); drain();
        return clock;
    }

    [AvaloniaFact]
    public void Horizontal_range_animation_moves_both_value_boundaries_and_preserves_category_and_raw_point()
    {
        var series = new RangeBarSeries { ItemsSource = new[] { new Band(1, 80, 40) }, CategoryPath = "X", HighPath = "High", LowPath = "Low", Orientation = Orientation.Horizontal };
        using var host = new SegmentTestHost(new(0, 100), new(0, 4), series);
        _ = Start(host.Chart, host.Drain);
        Assert.True(host.Chart.NavigateToPoint(series, 0));
        var hit = Assert.IsType<ChartPointHit>(host.Chart.CurrentPoint);
        Assert.Equal(new ChartPoint(40, 1), hit.Point); Assert.Equal(20, hit.BaseValue);
        Assert.Equal(new ChartPoint(1, 80), hit.RawPoint);
        host.Chart.CancelAnimations(); host.Drain();
        Assert.True(host.Chart.NavigateToPoint(series, 0));
        Assert.Equal(new ChartPoint(80, 1), host.Chart.CurrentPoint!.Point); Assert.Equal(40, host.Chart.CurrentPoint.BaseValue);
    }

    [AvaloniaFact]
    public void Pareto_animation_exposes_current_frame_cumulative_readout_and_cancellation_restores_exact_target()
    {
        var series = new ParetoSeries { ItemsSource = new[] { new Row("A", 60), new Row("B", 40) }, CategoryPath = "Name", ValuePath = "Value" };
        var chart = new CartesianChart { ShowLegend = false, XAxis = new CategoryAxis(), YAxis = new NumericAxis { Minimum = 0, Maximum = 100 } };
        chart.Series.Add(series); using var host = new TemplateFeaturesHost(chart);
        _ = Start(chart, host.Drain);
        Assert.True(chart.NavigateToPoint(series, 1));
        Assert.Equal(50, chart.CurrentPoint!.CumulativeValue); Assert.Equal(20, chart.CurrentPoint.Point.Y);
        chart.CancelAnimations(); host.Drain();
        Assert.True(chart.NavigateToPoint(series, 1));
        Assert.Equal(100, chart.CurrentPoint!.CumulativeValue); Assert.Equal(40, chart.CurrentPoint.Point.Y);
    }
}
