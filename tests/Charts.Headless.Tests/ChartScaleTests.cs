using System.Collections.ObjectModel;
using Avalonia;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class ChartScaleTests
{
    [Fact]
    public void Empty_and_nonfinite_data_have_a_usable_default_range()
    {
        var empty = ChartScale.FromValues([]);
        var invalid = ChartScale.FromValues([double.NaN, double.PositiveInfinity, double.NegativeInfinity]);

        Assert.True(empty.Minimum <= 0);
        Assert.True(empty.Maximum >= 1);
        Assert.Equal(empty.Minimum, invalid.Minimum);
        Assert.Equal(empty.Maximum, invalid.Maximum);
        AssertUsable(empty);
        AssertUsable(invalid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    [InlineData(-42)]
    public void Constant_values_expand_to_a_finite_range(double value)
    {
        var scale = ChartScale.FromValues([value, value]);

        Assert.True(scale.Minimum < value);
        Assert.True(scale.Maximum > value);
        Assert.InRange(scale.Normalize(value), 0.01, 0.99);
        AssertUsable(scale);
    }

    [Theory]
    [InlineData(-8, -3)]
    [InlineData(3, 8)]
    [InlineData(-8, 3)]
    public void Bar_ranges_include_zero_and_all_values(double first, double second)
    {
        var scale = ChartScale.FromValues([first, second], includeZero: true);

        Assert.True(scale.Minimum <= Math.Min(0, first));
        Assert.True(scale.Maximum >= Math.Max(0, second));
        Assert.InRange(scale.Normalize(0), 0, 1);
        AssertUsable(scale);
    }

    [Fact]
    public void Nonfinite_values_do_not_poison_a_valid_range()
    {
        var valid = ChartScale.FromValues([2, 7]);
        var mixed = ChartScale.FromValues([double.NaN, 2, double.PositiveInfinity, 7, double.NegativeInfinity]);

        Assert.Equal(valid.Minimum, mixed.Minimum);
        Assert.Equal(valid.Maximum, mixed.Maximum);
        Assert.Equal(valid.Ticks, mixed.Ticks);
        AssertUsable(mixed);
    }

    [Fact]
    public void Explicit_bounds_map_endpoints_and_support_an_inverted_pixel_axis()
    {
        var axis = new ChartAxis { Minimum = -10, Maximum = 30 };
        var scale = ChartScale.FromValues([-100, 100], axis);

        Assert.Equal(-10, scale.Minimum);
        Assert.Equal(30, scale.Maximum);
        Assert.Equal(20, scale.Map(-10, 20, 420), 8);
        Assert.Equal(420, scale.Map(30, 20, 420), 8);
        Assert.Equal(220, scale.Map(10, 20, 420), 8);
        Assert.Equal(300, scale.Map(-10, 300, 40), 8);
        Assert.Equal(40, scale.Map(30, 300, 40), 8);
        Assert.Equal(170, scale.Map(10, 300, 40), 8);
    }

    [Fact]
    public void Explicit_ranges_preserve_the_slope_of_lines_crossing_the_plot_boundary()
    {
        var scale = ChartScale.Create(0, 10, new ChartAxis { Minimum = 0, Maximum = 10 });

        Assert.Equal(-10, scale.Normalize(-100), 8);
        Assert.Equal(11, scale.Normalize(110), 8);
        Assert.Equal(-380, scale.Map(-10, 20, 420), 8);
        Assert.Equal(820, scale.Map(20, 20, 420), 8);
        Assert.Equal(560, scale.Map(-10, 300, 40), 8);
    }

    [Fact]
    public void Category_axes_keep_a_small_margin_and_label_the_first_and_last_points()
    {
        var axis = new ChartAxis { Labels = Enumerable.Range(1, 12).Select(month => $"Month {month}").ToArray() };
        var scale = ChartScale.Create(0, 11, axis, paddingFraction: 0.02);

        Assert.InRange(scale.Minimum, -0.5, 0);
        Assert.InRange(scale.Maximum, 11, 11.5);
        Assert.Equal("Month 1", scale.FormatLabel(0, axis));
        Assert.Equal("Month 12", scale.FormatLabel(11, axis));
        AssertUsable(scale);
    }

    [Theory]
    [InlineData(double.MinValue, double.MaxValue)]
    [InlineData(1e300, 1.00000001e300)]
    [InlineData(-1e300, -9e299)]
    [InlineData(1e-300, 2e-300)]
    public void Extreme_finite_data_generate_finite_coordinates_and_bounded_ticks(double first, double second)
    {
        var scale = ChartScale.FromValues([first, second]);

        AssertUsable(scale);
        Assert.True(double.IsFinite(scale.Map(first, 30, 530)));
        Assert.True(double.IsFinite(scale.Map(second, 30, 530)));
        Assert.True(scale.Normalize(first) < scale.Normalize(second));
    }

    [Theory]
    [InlineData(5, 5)]
    [InlineData(10, -10)]
    [InlineData(double.NaN, double.PositiveInfinity)]
    public void Degenerate_or_invalid_explicit_bounds_cannot_break_scale_generation(double minimum, double maximum)
    {
        var scale = ChartScale.FromValues([2, 4], new ChartAxis { Minimum = minimum, Maximum = maximum });

        AssertUsable(scale);
        Assert.True(double.IsFinite(scale.Map(3, 0, 500)));
    }

    [Fact]
    public void Axis_is_a_styled_element_and_distinguishes_range_changes_from_presentation()
    {
        var axis = new NumericAxis();
        Assert.IsAssignableFrom<StyledElement>(axis);
        var changes = new List<ChartAxisChangedEventArgs>();
        axis.Changed += (_, args) => changes.Add(args);

        axis.Title = "Temperature";
        axis.LabelFormat = "N2";
        axis.LabelRotation = 45;
        axis.LabelWrap = true;
        axis.LabelMaxWidth = 80;
        axis.Position = ChartAxisPosition.Far;
        Assert.All(changes, change => Assert.False(change.RangeChanged));
        changes.Clear();
        axis.Minimum = -20;
        axis.Maximum = 80;
        axis.Interval = 10;
        axis.IsInversed = true;
        axis.Key = "temperature";
        Assert.Equal(5, changes.Count);
        Assert.All(changes, change => Assert.True(change.RangeChanged));
    }

    [Fact]
    public void Observable_axis_labels_are_detached_when_replaced_or_unobserved()
    {
        var oldLabels = new ObservableCollection<string> { "A" };
        var newLabels = new ObservableCollection<string> { "B" };
        var axis = new CategoryAxis { Labels = oldLabels };
        var count = 0;
        EventHandler<ChartAxisChangedEventArgs> handler = (_, _) => count++;
        axis.Changed += handler;
        oldLabels.Add("C");
        Assert.Equal(1, count);
        axis.Labels = newLabels;
        Assert.Equal(2, count);
        oldLabels.Add("D");
        Assert.Equal(2, count);
        newLabels.Add("E");
        Assert.Equal(3, count);
        axis.Changed -= handler;
        newLabels.Add("F");
        Assert.Equal(3, count);
    }

    [Fact]
    public void Fixed_numeric_interval_and_reversal_keep_ticks_in_the_raw_domain()
    {
        var axis = new NumericAxis { Minimum = -10, Maximum = 30, Interval = 10, IsInversed = true };
        var scale = ChartScale.FromValues([-100, 100], axis);

        Assert.Equal(new double[] { -10, 0, 10, 20, 30 }, scale.Ticks);
        Assert.Equal(1, scale.Normalize(-10));
        Assert.Equal(0, scale.Normalize(30));
        Assert.Equal(0.75, scale.Normalize(0), 10);
        Assert.Equal(0, scale.Denormalize(0.75), 10);
        Assert.Equal(200, scale.Map(10, 100, 300), 10);
        AssertUsable(scale);
    }

    [Fact]
    public void A_view_scale_retains_configuration_without_mutating_the_original_axis()
    {
        var axis = new LogarithmicAxis { Minimum = 1, Maximum = 10_000, LogBase = 10,
            IsInversed = true, LabelFormat = "0.0" };

        var view = ChartScale.CreateForView(axis, 10, 1000);

        Assert.Equal(10, view.Minimum);
        Assert.Equal(1000, view.Maximum);
        Assert.Equal(0.5, view.Normalize(100), 10);
        Assert.Equal(100, view.Denormalize(0.5), 8);
        Assert.Equal("100.0", view.FormatLabel(100, axis));
        Assert.Equal(1, axis.Minimum);
        Assert.Equal(10_000, axis.Maximum);
        Assert.True(axis.IsInversed);
    }

    [Fact]
    public void Logarithmic_axis_uses_equal_screen_distance_for_equal_ratios()
    {
        var axis = new LogarithmicAxis { Minimum = 1, Maximum = 1000 };
        var scale = ChartScale.FromValues([-100, 0, double.NaN, 1, 10, 100, 1000], axis);

        Assert.Equal(new double[] { 1, 10, 100, 1000 }, scale.Ticks);
        Assert.Equal(1d / 3, scale.Normalize(10), 10);
        Assert.Equal(2d / 3, scale.Normalize(100), 10);
        Assert.Equal(10, scale.Denormalize(1d / 3), 8);
        Assert.False(axis.IsValidValue(0));
        Assert.False(axis.IsValidValue(-1));
        Assert.False(axis.IsValidValue(double.PositiveInfinity));
        AssertUsable(scale);
    }

    [Fact]
    public void Log_base_is_an_explicit_transform_and_changes_notify_range_invalidation()
    {
        var axis = new LogarithmicAxis { LogBase = 2 };
        ChartAxisChangedEventArgs? latest = null;
        axis.Changed += (_, args) => latest = args;

        Assert.Equal(5, axis.Transform(32), 10);
        Assert.Equal(32, axis.Inverse(5), 10);
        axis.LogBase = 4;
        Assert.NotNull(latest);
        Assert.True(latest.RangeChanged);
        Assert.Throws<ArgumentException>(() => axis.LogBase = 1);
    }

    [Fact]
    public void Logarithmic_scales_handle_the_entire_positive_double_domain_and_adjacent_values()
    {
        var axis = new LogarithmicAxis();
        var scale = ChartScale.CreateForView(axis, double.Epsilon, double.MaxValue);
        AssertUsable(scale);
        Assert.InRange(scale.Normalize(1), 0, 1);
        Assert.True(double.IsFinite(scale.Denormalize(0.5)));
        Assert.True(scale.Denormalize(0.5) > 0);
        var narrow = ChartScale.CreateForView(axis, 1e300, Math.BitIncrement(1e300));
        Assert.Equal(0, narrow.Normalize(narrow.Minimum));
        Assert.Equal(1, narrow.Normalize(narrow.Maximum));
        Assert.True(narrow.Minimum < narrow.Maximum);
        Assert.True(double.IsFinite(narrow.Denormalize(0.5)));
    }

    [Fact]
    public void Temporary_category_labels_are_captured_without_mutating_user_configuration()
    {
        var axis = new CategoryAxis { Labels = ["user", "labels"] };
        var temporary = new[] { "shared A", "shared B", "shared C" };

        var scale = ChartScale.Create(0, 2, axis, categoryLabels: temporary);
        temporary[0] = "changed later";
        var view = ChartScale.CreateForView(axis, 0.5, 2, ["shared A", "shared B", "shared C"]);

        Assert.Equal("shared A", scale.FormatLabel(0, axis));
        Assert.Equal("shared B", view.FormatLabel(1, axis));
        Assert.Equal("user", axis.Labels![0]);
        Assert.Equal(2, axis.Labels.Count);
    }

    [Fact]
    public void Date_inputs_use_unix_milliseconds_and_unspecified_values_mean_utc()
    {
        var utc = new DateTime(2026, 3, 8, 12, 30, 0, DateTimeKind.Utc);
        var unspecified = DateTime.SpecifyKind(utc, DateTimeKind.Unspecified);
        var offset = new DateTimeOffset(2026, 3, 8, 20, 30, 0, TimeSpan.FromHours(8));

        Assert.Equal(DateTimeAxis.ToUnixMilliseconds(utc), DateTimeAxis.ToUnixMilliseconds(unspecified));
        Assert.Equal(DateTimeAxis.ToUnixMilliseconds(utc), DateTimeAxis.ToUnixMilliseconds(offset));
        Assert.Equal(DateTimeOffset.UnixEpoch, DateTimeAxis.FromUnixMilliseconds(0));
        Assert.Equal(new DateTimeOffset(utc), DateTimeAxis.FromUnixMilliseconds(DateTimeAxis.ToUnixMilliseconds(utc)));
        Assert.Throws<ArgumentOutOfRangeException>(() => DateTimeAxis.FromUnixMilliseconds(double.NaN));
    }

    [Fact]
    public void Monthly_date_ticks_follow_the_calendar_across_leap_february()
    {
        var start = DateTimeAxis.ToUnixMilliseconds(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var end = DateTimeAxis.ToUnixMilliseconds(new DateTimeOffset(2024, 4, 1, 0, 0, 0, TimeSpan.Zero));
        var axis = new DateTimeAxis { Minimum = start, Maximum = end, Interval = 1,
            IntervalUnit = DateTimeIntervalUnit.Month, DateFormat = "yyyy-MM-dd" };

        var scale = ChartScale.Create(start, end, axis);

        Assert.Equal(new[] { "2024-01-01", "2024-02-01", "2024-03-01", "2024-04-01" },
            scale.Ticks.Select(tick => scale.FormatLabel(tick, axis)));
        Assert.Equal(TimeSpan.FromDays(29).TotalMilliseconds, scale.Ticks[2] - scale.Ticks[1]);
        AssertUsable(scale);
    }

    [Fact]
    public void Week_ticks_start_on_monday_and_date_views_keep_the_date_formatter()
    {
        var minimum = DateTimeAxis.ToUnixMilliseconds(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));
        var maximum = DateTimeAxis.ToUnixMilliseconds(new DateTimeOffset(2026, 10, 31, 0, 0, 0, TimeSpan.Zero));
        var axis = new DateTimeAxis { IntervalUnit = DateTimeIntervalUnit.Week, Interval = 1, DateFormat = "yyyy-MM-dd" };

        var scale = ChartScale.CreateForView(axis, minimum, maximum);

        Assert.All(scale.Ticks, tick => Assert.Equal(DayOfWeek.Monday, DateTimeAxis.FromUnixMilliseconds(tick).DayOfWeek));
        Assert.StartsWith("2026-10-", scale.FormatLabel(scale.Ticks[0], axis));
        Assert.Equal(minimum, scale.Denormalize(0));
        Assert.Equal(maximum, scale.Denormalize(1));
    }

    [Fact]
    public void A_constant_date_gets_a_local_window_instead_of_epoch_proportional_padding()
    {
        var value = DateTimeAxis.ToUnixMilliseconds(new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero));
        var scale = ChartScale.FromValues([value], new DateTimeAxis());

        Assert.True(scale.Minimum < value);
        Assert.True(scale.Maximum > value);
        Assert.InRange(scale.Maximum - scale.Minimum, TimeSpan.FromDays(2).TotalMilliseconds, TimeSpan.FromDays(4).TotalMilliseconds);
        AssertUsable(scale);
    }

    [Theory]
    [InlineData(double.MinValue, double.MaxValue)]
    [InlineData(1e300, 1.00000001e300)]
    [InlineData(1e-300, 2e-300)]
    public void Inverse_projection_roundtrips_interior_values_without_overflow(double minimum, double maximum)
    {
        var axis = new NumericAxis { Minimum = minimum, Maximum = maximum, IsInversed = true };
        var scale = ChartScale.CreateForView(axis, minimum, maximum);

        Assert.Equal(maximum, scale.Denormalize(0));
        Assert.Equal(minimum, scale.Denormalize(1));
        foreach (var fraction in new[] { 0.25, 0.5, 0.75 })
        {
            var raw = scale.Denormalize(fraction);
            Assert.True(double.IsFinite(raw));
            Assert.InRange(Math.Abs(scale.Normalize(raw) - fraction), 0, 1e-6);
        }
    }

    [Fact]
    public void A_very_small_fixed_interval_is_thinned_across_the_whole_range()
    {
        var scale = ChartScale.CreateForView(new NumericAxis { Interval = 0.001 }, 0, 100);

        Assert.InRange(scale.Ticks.Count, 2, 100);
        Assert.True(scale.Ticks[^1] > 95);
        AssertUsable(scale);
    }

    private static void AssertUsable(ChartScale scale)
    {
        Assert.True(double.IsFinite(scale.Minimum));
        Assert.True(double.IsFinite(scale.Maximum));
        Assert.True(scale.Minimum < scale.Maximum);
        Assert.True(double.IsFinite(scale.TickStep));
        Assert.True(scale.TickStep > 0);
        Assert.InRange(scale.Ticks.Count, 1, 100);
        Assert.All(scale.Ticks, tick =>
        {
            Assert.True(double.IsFinite(tick));
            Assert.InRange(tick, scale.Minimum, scale.Maximum);
            Assert.InRange(scale.Normalize(tick), 0, 1);
        });
        Assert.True(scale.Ticks.Zip(scale.Ticks.Skip(1), (left, right) => left < right).All(x => x));
    }
}
