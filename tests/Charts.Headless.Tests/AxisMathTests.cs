using Avalonia;
using Avalonia.LogicalTree;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class AxisMathTests
{
    [Fact]
    public void Category_axes_generate_each_default_slot_before_the_layout_selects_label_density()
    {
        var labels = Enumerable.Range(0, 8).Select(index => $"Category {index}").ToArray();
        var category = ChartScale.FromValues([0, 7], new CategoryAxis { Labels = labels });
        Assert.Equal(Enumerable.Range(0, 8).Select(index => (double)index), category.Ticks);

        var explicitInterval = ChartScale.FromValues([0, 7], new CategoryAxis { Labels = labels, Interval = 2 });
        Assert.Equal(new double[] { 0, 2, 4, 6 }, explicitInterval.Ticks);

        var legacy = ChartScale.FromValues([0, 7], new ChartAxis { Labels = labels }, paddingFraction: 0);
        Assert.Equal(new double[] { 0, 2, 4, 6 }, legacy.Ticks);
    }

    [Fact]
    public void Minor_count_inserts_only_interior_ticks_without_major_duplicates()
    {
        var axis = new NumericAxis { Interval = 5, MinorTickCount = 4 };
        var scale = ChartScale.CreateForView(axis, 0, 10);

        Assert.Equal(new double[] { 0, 5, 10 }, scale.Ticks);
        Assert.Equal(new double[] { 1, 2, 3, 4, 6, 7, 8, 9 }, scale.MinorTicks);
    }

    [Fact]
    public void Explicit_minor_interval_takes_precedence_and_covers_the_entire_view()
    {
        var scale = ChartScale.CreateForView(new NumericAxis { Interval = 5, MinorTickCount = 1, MinorInterval = 2 }, 0, 10);

        Assert.Equal(new double[] { 2, 4, 6, 8 }, scale.MinorTicks);
        var dense = ChartScale.CreateForView(new NumericAxis { MinorInterval = double.Epsilon }, -double.MaxValue, double.MaxValue);
        Assert.InRange(dense.MinorTicks.Count, 1, 1000);
        Assert.All(dense.MinorTicks, value => Assert.True(double.IsFinite(value)));
        Assert.True(dense.MinorTicks[^1] > double.MaxValue * .8);
    }

    [Fact]
    public void Logarithmic_minor_ticks_use_exponent_spacing()
    {
        var scale = ChartScale.CreateForView(new LogarithmicAxis { Interval = 1, MinorTickCount = 1 }, 1, 100);

        Assert.Equal(2, scale.MinorTicks.Count);
        Assert.Equal(Math.Sqrt(10), scale.MinorTicks[0], 10);
        Assert.Equal(Math.Sqrt(1000), scale.MinorTicks[1], 10);
        Assert.Equal(.25, scale.Normalize(scale.MinorTicks[0]), 10);
        Assert.Equal(.75, scale.Normalize(scale.MinorTicks[1]), 10);
    }

    [Fact]
    public void Date_minor_interval_remains_raw_milliseconds_with_calendar_major_ticks()
    {
        var start = DateTimeAxis.ToUnixMilliseconds(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var end = DateTimeAxis.ToUnixMilliseconds(new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero));
        var axis = new DateTimeAxis { IntervalUnit = DateTimeIntervalUnit.Month, Interval = 1, MinorInterval = TimeSpan.FromDays(1).TotalMilliseconds };
        var scale = ChartScale.CreateForView(axis, start, end);

        Assert.Equal(58, scale.MinorTicks.Count);
        Assert.All(scale.MinorTicks, tick => Assert.Equal(0, DateTimeAxis.FromUnixMilliseconds(tick).Hour));
        Assert.Contains(scale.MinorTicks, tick => DateTimeAxis.FromUnixMilliseconds(tick).Date == new DateTime(2024, 2, 29));
        Assert.DoesNotContain(scale.MinorTicks, tick => DateTimeAxis.FromUnixMilliseconds(tick).Day == 1);
    }

    [Fact]
    public void Breaks_merge_overlaps_and_touching_intervals_with_the_smallest_ratio()
    {
        var axis = new NumericAxis();
        axis.ScaleBreaks.Add(new() { Start = 6, End = 9, CompressionRatio = .2 });
        axis.ScaleBreaks.Add(new() { Start = 2, End = 5, CompressionRatio = .1 });
        axis.ScaleBreaks.Add(new() { Start = 4, End = 6, CompressionRatio = .3 });
        axis.ScaleBreaks.Add(new() { Start = 15, End = 14 });
        var scale = ChartScale.CreateForView(axis, 0, 10);

        var merged = Assert.Single(scale.MergedBreaks);
        Assert.Equal(2, merged.Start);
        Assert.Equal(9, merged.End);
        Assert.Equal(.1, merged.CompressionRatio);
        Assert.False(axis.IsInScaleBreak(2));
        Assert.False(axis.IsInScaleBreak(9));
        Assert.True(axis.IsInScaleBreak(5));
        Assert.False(axis.IsInScaleBreak(double.NaN));
        Assert.True(scale.IsValidValue(2));
        Assert.False(scale.IsValidValue(5));
        Assert.True(axis.IsValidValue(5)); // Viewport bounds still use the mathematical domain.
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Compressed_breaks_preserve_endpoint_projection_and_a_bijective_viewport_transform(bool inversed)
    {
        var axis = new NumericAxis { Interval = 1, MinorTickCount = 1, IsInversed = inversed };
        axis.ScaleBreaks.Add(new() { Start = 4, End = 6, CompressionRatio = .1 });
        var scale = ChartScale.CreateForView(axis, 0, 10);

        Assert.Equal(inversed ? 1 : 0, scale.Normalize(0));
        Assert.Equal(inversed ? 0 : 1, scale.Normalize(10));
        Assert.Equal(inversed ? 1 - 4 / 8.2 : 4 / 8.2, scale.Normalize(4), 10);
        Assert.Equal(.2 / 8.2, Math.Abs(scale.Normalize(6) - scale.Normalize(4)), 10);
        Assert.DoesNotContain(scale.Ticks, tick => tick > 4 && tick < 6);
        Assert.DoesNotContain(scale.MinorTicks, tick => tick > 4 && tick < 6);
        foreach (var value in new[] { -2, 0, 2, 4, 4.5, 5, 6, 8, 10, 12 })
            Assert.Equal(value, scale.Denormalize(scale.Normalize(value)), 8);
        Assert.Equal(5, scale.Denormalize(.5), 8);
    }

    [Fact]
    public void Sliced_views_keep_the_same_piecewise_axis_geometry_even_when_bounds_are_inside_a_break()
    {
        var axis = new NumericAxis();
        axis.ScaleBreaks.Add(new() { Start = 4, End = 6, CompressionRatio = .1 });
        var full = ChartScale.CreateForView(axis, 0, 10);
        var view = ChartScale.CreateForView(axis, 4.5, 9);
        var from = full.Normalize(4.5);
        var to = full.Normalize(9);

        foreach (var raw in new[] { 4.5, 5, 6, 7, 9 })
        {
            Assert.Equal((full.Normalize(raw) - from) / (to - from), view.Normalize(raw), 9);
            Assert.Equal(raw, view.Denormalize(view.Normalize(raw)), 8);
        }
    }

    [Fact]
    public void Log_and_date_breaks_are_compressed_in_their_axis_domains()
    {
        var log = new LogarithmicAxis();
        log.ScaleBreaks.Add(new() { Start = 10, End = 100, CompressionRatio = .1 });
        var logScale = ChartScale.CreateForView(log, 1, 1000);
        Assert.Equal(1 / 2.1, logScale.Normalize(10), 10);
        Assert.Equal(1.1 / 2.1, logScale.Normalize(100), 10);
        Assert.Equal(Math.Sqrt(1000), logScale.Denormalize(.5), 8);

        var date = new DateTimeAxis { IntervalUnit = DateTimeIntervalUnit.Day, Interval = 1 };
        const double day = 86_400_000;
        date.ScaleBreaks.Add(new() { Start = day * 2, End = day * 4, CompressionRatio = .25 });
        var dateScale = ChartScale.CreateForView(date, 0, day * 6);
        Assert.Equal(2 / 4.5, dateScale.Normalize(day * 2), 10);
        Assert.Equal(day * 3, dateScale.Denormalize(.5), 6);
        Assert.DoesNotContain(dateScale.Ticks, tick => tick > day * 2 && tick < day * 4);
    }

    [Theory]
    [InlineData(-double.MaxValue, double.MaxValue)]
    [InlineData(1e300, 1.00000001e300)]
    [InlineData(1e-300, 2e-300)]
    public void Extreme_finite_breaks_remain_monotone_and_roundtrip_visible_values(double minimum, double maximum)
    {
        var unbroken = ChartScale.CreateForView(new NumericAxis(), minimum, maximum);
        var axis = new NumericAxis();
        axis.ScaleBreaks.Add(new() { Start = unbroken.Denormalize(.25), End = unbroken.Denormalize(.75), CompressionRatio = .01 });
        var scale = ChartScale.CreateForView(axis, minimum, maximum);

        var previous = -1d;
        foreach (var fraction in new[] { 0d, .1, .25, .5, .75, .9, 1 })
        {
            var raw = unbroken.Denormalize(fraction);
            var projected = scale.Normalize(raw);
            Assert.True(double.IsFinite(projected));
            Assert.True(projected > previous);
            previous = projected;
            var roundtrip = scale.Denormalize(projected);
            Assert.True(double.IsFinite(roundtrip));
            Assert.InRange(Math.Abs(unbroken.Normalize(roundtrip) - fraction), 0, 1e-6);
        }
    }

    [Fact]
    public void Breaks_covering_a_whole_view_or_extending_outside_it_do_not_degenerate_the_inverse()
    {
        var axis = new NumericAxis();
        axis.ScaleBreaks.Add(new() { Start = -10, End = 20, CompressionRatio = double.Epsilon });
        var scale = ChartScale.CreateForView(axis, 0, 10);

        Assert.Empty(scale.Ticks);
        Assert.False(scale.IsValidValue(5));
        Assert.Equal(.5, scale.Normalize(5), 10);
        Assert.Equal(5, scale.Denormalize(.5), 10);
        var source = ChartScale.FromValues([1, 2, 3], axis);
        Assert.True(double.IsFinite(source.Minimum));
        Assert.True(source.Maximum > source.Minimum);
    }

    [Fact]
    public void Category_padding_is_in_slots_and_not_reapplied_to_views_or_explicit_bounds()
    {
        var axis = new CategoryAxis { PlotMode = CategoryPlotMode.BetweenTicks, Gap = .25 };
        var scale = ChartScale.FromValues([0, 1, 2], axis);
        Assert.Equal(-.75, scale.Minimum);
        Assert.Equal(2.75, scale.Maximum);
        var view = ChartScale.CreateForView(axis, .2, 1.8);
        Assert.Equal(.2, view.Minimum);
        Assert.Equal(1.8, view.Maximum);
        axis.Minimum = 0; axis.Maximum = 2;
        var fixedScale = ChartScale.FromValues([0, 1, 2], axis);
        Assert.Equal(0, fixedScale.Minimum);
        Assert.Equal(2, fixedScale.Maximum);
        var single = ChartScale.FromValues([0], new CategoryAxis { PlotMode = CategoryPlotMode.BetweenTicks });
        Assert.Equal(-.5, single.Minimum);
        Assert.Equal(.5, single.Maximum);
    }

    [Fact]
    public void Feature_changes_keep_ownership_and_distinguish_transform_from_presentation()
    {
        var axis = new NumericAxis { DataContext = "view-model" };
        var band = new ChartPlotBand { Start = 1, End = 2 };
        var scaleBreak = new ChartScaleBreak { Start = 4, End = 6 };
        axis.PlotBands.Add(band); axis.ScaleBreaks.Add(scaleBreak);
        Assert.Same(axis, ((ILogical)band).LogicalParent);
        Assert.Same(axis, ((ILogical)scaleBreak).LogicalParent);
        Assert.Equal("view-model", band.DataContext);
        Assert.Equal("view-model", scaleBreak.DataContext);
        var changes = new List<ChartAxisChangedEventArgs>();
        EventHandler<ChartAxisChangedEventArgs> observer = (_, args) => changes.Add(args);
        axis.Changed += observer;
        axis.ViewMinimum = 0; axis.ViewMaximum = 8;
        axis.ShowTitle = false; axis.ShowTicks = false; axis.LabelBrush = Brushes.Red;
        axis.MinorTickCount = 2; axis.MinorInterval = .5;
        band.Text = "target"; band.Start = 2; band.Fill = Brushes.Blue;
        scaleBreak.DataContext = "another-model";
        Assert.All(changes, change => Assert.False(change.RangeChanged));
        changes.Clear();
        scaleBreak.End = 7; scaleBreak.CompressionRatio = .2;
        axis.MinimumZoom = 2; axis.MaximumZoom = 10;
        Assert.Equal(4, changes.Count);
        Assert.All(changes, change => Assert.True(change.RangeChanged));
        changes.Clear();
        axis.ScaleBreaks.Remove(scaleBreak);
        Assert.Null(((ILogical)scaleBreak).LogicalParent);
        changes.Clear(); scaleBreak.Start = 3;
        Assert.Empty(changes);
        axis.Changed -= observer;
        band.Text = "unobserved";
        Assert.Empty(changes);
        axis.PlotBands.Remove(band);
        Assert.Null(((ILogical)band).LogicalParent);
    }

    [Fact]
    public void Unobserved_break_mutation_and_observed_cache_invalidation_both_take_effect()
    {
        var axis = new NumericAxis();
        var item = new ChartScaleBreak { Start = 2, End = 4 };
        axis.ScaleBreaks.Add(item);
        Assert.True(axis.IsInScaleBreak(3));
        item.End = 2.5;
        Assert.False(axis.IsInScaleBreak(3));
        EventHandler<ChartAxisChangedEventArgs> observer = (_, _) => { };
        axis.Changed += observer;
        Assert.False(axis.IsInScaleBreak(3));
        item.End = 5;
        Assert.True(axis.IsInScaleBreak(3));
        var old = ChartScale.CreateForView(axis, 0, 10);
        item.End = 6;
        Assert.Equal(5, old.MergedBreaks[0].End);
        Assert.Equal(6, ChartScale.CreateForView(axis, 0, 10).MergedBreaks[0].End);
        axis.Changed -= observer;
    }

    [Fact]
    public void Axis_feature_defaults_and_finite_validation_are_explicit()
    {
        var axis = new NumericAxis();
        Assert.True(axis.ShowAxis && axis.ShowTitle && axis.ShowLabels && axis.ShowTicks && axis.ShowGridLines);
        Assert.False(axis.ShowMinorGridLines);
        Assert.Equal(1, axis.MinimumZoom); Assert.Equal(1_000_000, axis.MaximumZoom);
        Assert.Null(axis.LabelBrush); Assert.Null(axis.LabelFontFamily); Assert.Null(axis.TitleFontSize);
        Assert.Equal(4, axis.TickLength); Assert.Equal(2, axis.MinorTickLength);
        Assert.Throws<ArgumentException>(() => axis.MinimumZoom = .5);
        Assert.Throws<ArgumentException>(() => axis.MaximumZoom = double.PositiveInfinity);
        Assert.Throws<ArgumentException>(() => axis.MinorTickCount = -1);
        Assert.Throws<ArgumentException>(() => axis.MinorInterval = 0);
        Assert.Throws<ArgumentException>(() => axis.AxisThickness = double.NaN);
        Assert.Throws<ArgumentException>(() => axis.LabelFontSize = 0);
        Assert.Throws<ArgumentException>(() => axis.ViewMaximum = double.NaN);
        Assert.Throws<ArgumentException>(() => new ChartScaleBreak().CompressionRatio = 0);
        Assert.Throws<ArgumentException>(() => new ChartPlotBand().RepeatCount = 10_001);
        Assert.Equal(ChartPlotBandLayer.BehindSeries, new ChartPlotBand().Layer);
        Assert.Equal(1, new ChartPlotBand().RepeatCount);
    }
}
