using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Headless.XUnit;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class LineAreaDataTests
{
    private sealed record Row(string Id, double X, double Y);
    private sealed record Band(string Id, double X, double? High, double? Low);

    private static AreaSeries Area(string name, ChartStackMode mode, params Row[] rows) => new()
    {
        Title = name, ItemsSource = rows, CategoryPath = "X", ValuePath = "Y", IdPath = "Id",
        StackMode = mode, MarkerSize = 0,
    };
    private static RangeAreaSeries Range(EmptyPointMode mode, params Band[] rows) => new()
    {
        ItemsSource = rows, CategoryPath = "X", HighPath = "High", LowPath = "Low", IdPath = "Id", EmptyPointMode = mode, MarkerSize = 0,
    };
    private static ChartPointHit Hit(SegmentTestHost host, ChartSeries series, double x, double y)
    {
        var hit = Assert.IsType<ChartPointHit>(host.Chart.HitTestPoint(host.Project(new(x, y)), .1));
        Assert.Same(series, hit.Series); Assert.Equal(y, hit.Point.Y, 8); return hit;
    }

    [AvaloniaTheory]
    [InlineData(ChartLineInterpolation.Linear)]
    [InlineData(ChartLineInterpolation.Smooth)]
    [InlineData(ChartLineInterpolation.StepBefore)]
    [InlineData(ChartLineInterpolation.StepAfter)]
    [InlineData(ChartLineInterpolation.StepCenter)]
    public void Signed_stacks_retain_source_identity_and_contribution_for_every_path(ChartLineInterpolation interpolation)
    {
        Row[] first = [new("a", 1, 20), new("b", 3, -10)];
        Row[] second = [new("c", 1, 30), new("d", 3, -40)];
        var a = Area("a", ChartStackMode.Stacked, first); var b = Area("b", ChartStackMode.Stacked, second);
        a.Interpolation = b.Interpolation = interpolation;
        using var host = new SegmentTestHost(new(0, 4), new(-100, 100), a, b);
        var positive = Hit(host, b, 1, 50); var negative = Hit(host, b, 3, -50);
        Assert.Equal(20, positive.BaseValue); Assert.Equal(30, positive.StackValue); Assert.Equal(new ChartPoint(1, 30), positive.RawPoint);
        Assert.Equal(-10, negative.BaseValue); Assert.Equal(-40, negative.StackValue); Assert.Equal(new ChartPoint(3, -40), negative.RawPoint);
        Assert.Same(second[0], positive.Item); Assert.Equal("c", positive.StableId); Assert.Equal(0, positive.Index);
        Assert.Equal(30, b.CreateSnapshot().Points[0].Point.Y); Assert.Null(b.CreateSnapshot().Points[0].BaseValue);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Signed_percent_stacks_normalize_each_sign_without_overflow(bool huge)
    {
        var amount = huge ? 1e308 : 30;
        var a = Area("a", ChartStackMode.Percent, new("a", 1, amount), new("b", 2, -amount), new("zero-a", 3, 0));
        var b = Area("b", ChartStackMode.Percent, new("c", 1, amount), new("d", 2, -amount), new("zero-b", 3, 0));
        using var host = new SegmentTestHost(new(0, 4), new(-110, 110), a, b);
        var positive = Hit(host, b, 1, 100); var negative = Hit(host, b, 2, -100); var zero = Hit(host, b, 3, 0);
        Assert.Equal(50, positive.BaseValue); Assert.Equal(50, positive.StackValue); Assert.Equal(amount, positive.RawPoint!.Value.Y);
        Assert.Equal(-50, negative.BaseValue); Assert.Equal(-50, negative.StackValue); Assert.Equal(0, zero.StackValue);
    }

    [AvaloniaFact]
    public void Percent_line_auto_axis_includes_zero_and_exact_one_hundred()
    {
        var a = new LineSeries { ItemsSource = new ChartPoint[] { new(1, 2), new(2, 6) }, StackMode = ChartStackMode.Percent, MarkerSize = 0 };
        var b = new LineSeries { ItemsSource = new ChartPoint[] { new(1, 8), new(2, 4) }, StackMode = ChartStackMode.Percent, MarkerSize = 0 };
        var chart = new CartesianChart { ShowLegend = false }; chart.Series.Add(a); chart.Series.Add(b);
        using var host = new TemplateFeaturesHost(chart);
        Assert.Equal(0, chart.VisibleRange!.Value.YMinimum); Assert.Equal(100, chart.VisibleRange.Value.YMaximum);
        Assert.Equal(2, a.CreateSnapshot().Points[0].Point.Y);
    }

    [AvaloniaTheory]
    [InlineData(3)]
    [InlineData(7)]
    public void Percent_nondyadic_shares_finish_at_exact_signed_hundred_without_expanding_auto_ticks(int seriesCount)
    {
        var chart = new CartesianChart { ShowLegend = false };
        for (var index = 0; index < seriesCount; index++)
            chart.Series.Add(Area(index.ToString(), ChartStackMode.Percent,
                new("positive", 1, seriesCount == 3 ? new[] { 20d, 30d, 10d }[index] : 1),
                new("negative", 2, seriesCount == 3 ? -new[] { 20d, 30d, 10d }[index] : -1)));
        using var host = new TemplateFeaturesHost(chart);
        Assert.Equal(-100, chart.VisibleRange!.Value.YMinimum); Assert.Equal(100, chart.VisibleRange.Value.YMaximum);
        var plot = chart.PlotArea; var range = chart.VisibleRange.Value;
        var x = plot.Left + (1 - range.XMinimum) / (range.XMaximum - range.XMinimum) * plot.Width;
        var hit = Assert.IsType<ChartPointHit>(chart.HitTestPoint(new(x, plot.Top), .1));
        Assert.Same(chart.Series[^1], hit.Series); Assert.Equal(100, hit.Point.Y);
        Assert.Equal(hit.Point.Y, hit.BaseValue + hit.StackValue);
    }

    [AvaloniaFact]
    public void Stacks_align_by_x_and_duplicate_occurrence_without_sorting_or_fabricating_items()
    {
        var a = Area("a", ChartStackMode.Stacked, new("a", 3, 10), new("b", 1, 20), new("c", 1, 30));
        var b = Area("b", ChartStackMode.Stacked, new("d", 1, 2), new("e", 3, 4), new("f", 1, 6));
        using var host = new SegmentTestHost(new(0, 4), new(0, 100), a, b);
        var first = Hit(host, b, 1, 22); var second = Hit(host, b, 3, 14); var duplicate = Hit(host, b, 1, 36);
        Assert.Equal(0, first.Index); Assert.Equal(1, second.Index); Assert.Equal(2, duplicate.Index);
        Assert.Equal(20, first.BaseValue); Assert.Equal(10, second.BaseValue); Assert.Equal(30, duplicate.BaseValue);
    }

    [AvaloniaFact]
    public void Groups_and_modes_are_isolated_while_line_and_area_share_a_stack()
    {
        var a = Area("a", ChartStackMode.Stacked, new Row("a", 1, 10)); a.StackGroup = "shared";
        var b = new LineSeries { ItemsSource = new ChartPoint[] { new(1, 20) }, StackMode = ChartStackMode.Stacked, StackGroup = "shared", MarkerSize = 0 };
        var c = Area("c", ChartStackMode.Stacked, new Row("c", 2, 40)); c.StackGroup = "other";
        var d = Area("d", ChartStackMode.Percent, new Row("d", 3, 50)); d.StackGroup = "shared";
        using var host = new SegmentTestHost(new(0, 4), new(0, 110), a, b, c, d);
        Assert.Equal(10, Hit(host, b, 1, 30).BaseValue);
        Assert.Equal(0, Hit(host, c, 2, 40).BaseValue);
        Assert.Equal(100, Hit(host, d, 3, 100).StackValue);
    }

    [AvaloniaFact]
    public void Different_resolved_axes_do_not_share_stack_totals()
    {
        var a = Area("a", ChartStackMode.Stacked, new Row("a", 1, 30));
        var b = Area("b", ChartStackMode.Stacked, new Row("b", 2, 50)); b.YAxisKey = "right";
        using var host = new SegmentTestHost(new(0, 4), new(0, 100), a);
        host.Chart.Axes.Add(new NumericAxis { Key = "right", Minimum = 0, Maximum = 100 }); host.Chart.Series.Add(b); host.Drain();
        Assert.Equal(0, Hit(host, b, 2, 50).BaseValue);
    }

    [AvaloniaFact]
    public void Hidden_series_and_observable_updates_recompute_remaining_stack_metadata_and_selection()
    {
        var rows = new ObservableCollection<Row> { new("b", 1, 20) };
        var a = Area("a", ChartStackMode.Stacked, new Row("a", 1, 10));
        var b = Area("b", ChartStackMode.Stacked); b.ItemsSource = rows;
        using var host = new SegmentTestHost(new(0, 4), new(0, 100), a, b);
        host.Chart.SelectionMode = ChartSelectionMode.Multiple;
        host.Chart.SelectedPoint = Hit(host, b, 1, 30); host.Drain();
        a.IsVisible = false; host.Drain(); _ = host.Chart.PlotArea;
        Assert.Equal(20, host.Chart.SelectedPoint!.Point.Y); Assert.Equal(0, host.Chart.SelectedPoint.BaseValue);
        rows[0] = new("b", 1, 40); host.Drain(); _ = host.Chart.PlotArea;
        Assert.Equal(40, host.Chart.SelectedPoint!.Point.Y); Assert.Same(rows[0], host.Chart.SelectedPoint.Item);
        a.IsVisible = true; host.Drain(); _ = host.Chart.PlotArea; Assert.Equal(50, host.Chart.SelectedPoint!.Point.Y);
    }

    [AvaloniaFact]
    public void A_missing_contribution_is_a_gap_and_does_not_poison_other_series()
    {
        var a = Area("a", ChartStackMode.Stacked, new("a", 1, 10), new("missing", 2, double.NaN), new("c", 3, 30));
        var b = Area("b", ChartStackMode.Stacked, new("d", 1, 20), new("e", 2, 40), new("f", 3, 50));
        using var host = new SegmentTestHost(new(0, 4), new(0, 100), a, b);
        Assert.Equal(0, Hit(host, b, 2, 40).BaseValue);
        var hit = host.Chart.HitTestSegment(host.Project(new(2, 20)), .1);
        Assert.True(hit is null || !ReferenceEquals(hit.Series, a));
    }

    [AvaloniaFact]
    public void Unrepresentable_raw_stack_sum_is_a_gap_but_source_stays_finite()
    {
        var a = Area("a", ChartStackMode.Stacked, new Row("a", 1, 1e308));
        var b = Area("b", ChartStackMode.Stacked, new Row("b", 1, 1e308));
        var chart = new CartesianChart { ShowLegend = false }; chart.Series.Add(a); chart.Series.Add(b);
        using var host = new TemplateFeaturesHost(chart);
        Assert.NotNull(chart.VisibleRange); Assert.True(double.IsFinite(chart.VisibleRange!.Value.YMaximum));
        Assert.Equal(1e308, b.CreateSnapshot().Points[0].Point.Y);
    }

    [AvaloniaFact]
    public void Range_uses_high_low_without_value_path_and_normalizes_reversed_bounds()
    {
        Band[] rows = [new("a", 1, 10, 30), new("b", 3, 60, 40)];
        var series = Range(EmptyPointMode.Gap, rows);
        using var host = new SegmentTestHost(new(0, 4), new(0, 100), series);
        var hit = Hit(host, series, 1, 30);
        Assert.Equal(10, hit.BaseValue); Assert.Same(rows[0], hit.Item); Assert.Equal("a", hit.StableId);
        var source = series.CreateSnapshot().Points[0]; Assert.False(source.IsEmpty); Assert.False(source.WasEmpty);
        Assert.Equal(10, source.High); Assert.Equal(30, source.Low);
        Assert.Equal(30, source.Point.Y); Assert.Null(source.BaseValue);
        Assert.Equal(40, Hit(host, series, 3, 60).BaseValue);
    }

    [AvaloniaTheory]
    [InlineData(EmptyPointMode.Gap, double.NaN)]
    [InlineData(EmptyPointMode.Zero, 0)]
    [InlineData(EmptyPointMode.Average, 40)]
    [InlineData(EmptyPointMode.Interpolate, 30)]
    public void Range_empty_policy_fills_only_missing_bounds_and_preserves_missing_status(EmptyPointMode mode, double expectedHigh)
    {
        var series = Range(mode, new("a", 0, 20, 10), new("gap", 1, null, 15), new("c", 4, 60, 30));
        var snapshot = series.CreateSnapshot(); var item = snapshot.Points[1];
        Assert.True(item.WasEmpty);
        if (double.IsNaN(expectedHigh)) { Assert.True(item.IsEmpty); Assert.Null(item.High); }
        else { Assert.False(item.IsEmpty); Assert.Equal(expectedHigh, item.High); Assert.Equal(15, item.Low); Assert.Equal(Math.Max(expectedHigh, 15), item.Point.Y); }
    }

    [AvaloniaFact]
    public void Range_auto_extent_and_area_nonzero_baseline_include_their_lower_bound()
    {
        var chart = new CartesianChart { ShowLegend = false };
        chart.Series.Add(Range(EmptyPointMode.Gap, new("a", 1, 70, -20), new("b", 2, 80, 10)));
        using var host = new TemplateFeaturesHost(chart);
        Assert.True(chart.VisibleRange!.Value.YMinimum <= -20); Assert.True(chart.VisibleRange.Value.YMaximum >= 80);
        chart.Series.Clear(); var area = Area("baseline", ChartStackMode.None, new("a", 1, 20), new("b", 2, 30)); area.Baseline = 10;
        chart.Series.Add(area); host.Drain();
        Assert.True(chart.VisibleRange!.Value.YMinimum > 0); Assert.True(chart.VisibleRange.Value.YMinimum <= 10);
    }

    [AvaloniaFact]
    public void An_explicitly_empty_published_range_sample_is_never_revived_by_finite_bounds()
    {
        var series = Range(EmptyPointMode.Gap, new("a", 1, 60, 20), new("b", 2, 60, 20), new("c", 3, 60, 20));
        using var host = new SegmentTestHost(new(0, 4), new(0, 100), series);
        var source = series.CreateSnapshot(); var points = source.Points.ToArray(); points[1] = points[1] with { IsEmpty = true };
        Assert.True(series.TryPublishSnapshot(new(source.Version, points), source.Version)); host.Drain();
        Assert.Null(host.Chart.HitTestPoint(host.Project(new(2, 60)), .1));
        Assert.Null(host.Chart.HitTestSegment(host.Project(new(2, 60)), .1));
        Assert.Equal(60, series.CreateSnapshot().Points[1].High);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Area_log_auto_range_keeps_positive_upper_minimum_despite_zero_baseline(bool stacked)
    {
        var chart = new CartesianChart { ShowLegend = false, YAxis = new LogarithmicAxis() };
        var series = Area("a", stacked ? ChartStackMode.Stacked : ChartStackMode.None, new("a", 1, 1), new("b", 2, 1e6));
        chart.Series.Add(series); using var host = new TemplateFeaturesHost(chart);
        Assert.True(chart.VisibleRange!.Value.YMinimum <= 1); Assert.True(chart.VisibleRange.Value.YMaximum >= 1e6);
    }

    [AvaloniaFact]
    public void Log_range_extent_includes_small_positive_lower_values_even_when_another_row_is_rejected()
    {
        var chart = new CartesianChart { ShowLegend = false, YAxis = new LogarithmicAxis() };
        chart.Series.Add(Range(EmptyPointMode.Gap, new("invalid", 1, 10, -1), new("valid", 2, 20, .1)));
        using var host = new TemplateFeaturesHost(chart);
        Assert.True(chart.VisibleRange!.Value.YMinimum <= .1); Assert.True(chart.VisibleRange.Value.YMaximum >= 20);
    }

    [AvaloniaFact]
    public void Stack_rejects_category_value_axis_with_an_explicit_error()
    {
        var chart = new CartesianChart { YAxis = new CategoryAxis() };
        chart.Series.Add(Area("a", ChartStackMode.Stacked, new Row("a", 1, 2)));
        Assert.Throws<InvalidOperationException>(() =>
        { chart.Measure(new Size(500, 300)); chart.Arrange(new Rect(0, 0, 500, 300)); _ = chart.PlotArea; });
    }

    [AvaloniaFact]
    public void Range_rejects_category_value_axis_instead_of_mixing_raw_bounds_with_category_slots()
    {
        var chart = new CartesianChart { YAxis = new CategoryAxis() };
        chart.Series.Add(Range(EmptyPointMode.Gap, new("a", 1, 30, 10), new("b", 2, 40, 20)));
        Assert.Throws<InvalidOperationException>(() =>
        { chart.Measure(new Size(500, 300)); chart.Arrange(new Rect(0, 0, 500, 300)); _ = chart.PlotArea; });
    }

    [AvaloniaFact]
    public void Entrance_animation_moves_both_range_boundaries_and_keeps_raw_identity()
    {
        var series = Range(EmptyPointMode.Gap, new("a", 1, 80, 40), new("b", 3, 60, 20));
        using var host = new SegmentTestHost(new(0, 4), new(0, 100), series);
        var clock = new TestClock(); host.Chart.AnimationTimeProvider = clock; host.Chart.AnimationDuration = TimeSpan.FromSeconds(1);
        host.Chart.AnimationEasing = new LinearEasing(); host.Chart.AnimationTriggers = ChartAnimationTriggers.Entrance;
        host.Chart.ReplayEntranceAnimation(); host.Drain(); clock.Advance(TimeSpan.FromMilliseconds(500)); host.Chart.UpdateAnimationFrame(); host.Drain();
        var hit = Hit(host, series, 1, 40); Assert.Equal(20, hit.BaseValue); Assert.Equal(new ChartPoint(1, 80), hit.RawPoint);
        Assert.Equal(80, series.CreateSnapshot().Points[0].Point.Y);
        host.Chart.CancelAnimations(); host.Drain();
        Assert.Equal(40, Hit(host, series, 1, 80).BaseValue);
    }

    private sealed class TestClock : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        public void Advance(TimeSpan elapsed) => _ticks += elapsed.Ticks;
    }
}
