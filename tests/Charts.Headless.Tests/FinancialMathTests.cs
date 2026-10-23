using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class FinancialMathTests
{
    private static ChartFinancialPoint[] Prices(params double[] closes) => closes.Select((c, i) =>
        new ChartFinancialPoint(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero).AddDays(i),
            c - 1, c + 2, c - 2, c, (i + 1) * 100)).ToArray();

    [Fact]
    public void Raw_is_an_immutable_copy_with_true_original_identity_and_unaggregated_volume()
    {
        var source = Prices(10, 12, 15);
        var result = Raw(source);
        var original = source[1]; source[1] = source[1] with { Close = 0 };
        Assert.Equal(original, result[1].SourceEnd);
        Assert.Equal(original, Assert.Single(result[1].Sources));
        Assert.Equal(200, result[1].Volume);
        Assert.Equal(1, result[1].Slot);
        Assert.Equal(1, result[1].SourceStartIndex);
        Assert.Equal(1, result[1].SourceEndIndex);
        Assert.Equal(ChartFinancialTransformKind.Raw, result[1].Kind);
        Assert.True(result[0].IsSegmentStart);
        Assert.False(result[1].IsSegmentStart);
        Assert.Throws<NotSupportedException>(() => ((IList<ChartFinancialSample>)result)[0] = result[1]);
        Assert.False(result[1].Sources is IList<ChartFinancialPoint>);
        Assert.Throws<ArgumentOutOfRangeException>(() => result[1].Sources[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => result[1].Sources[1]);
    }

    [Fact]
    public void Heikin_ashi_uses_the_explicit_seed_and_preceding_synthetic_values()
    {
        var input = Prices(10, 12, 15);
        var r = HeikinAshi(input);
        Assert.Equal(new[] { 9.5, 9.625, 10.6875 }, r.Select(s => s.Open));
        Assert.Equal(new[] { 9.75, 11.75, 14.75 }, r.Select(s => s.Close));
        Assert.Equal(new[] { 12.0, 14, 17 }, r.Select(s => s.High));
        Assert.Equal(new[] { 8.0, 9.625, 10.6875 }, r.Select(s => s.Low));
        Assert.Equal(input[2], r[2].SourceEnd);
        Assert.Equal(14, r[2].SourceEnd.Open);
        Assert.Equal(2, r[2].SourceStartIndex);
        Assert.Equal(2, r[2].SourceEndIndex);
        Assert.Equal(ChartFinancialTransformKind.HeikinAshi, r[2].Kind);
    }

    [Fact]
    public void Renko_fixed_two_box_reversal_skips_the_overlap_and_retains_each_trigger()
    {
        var source = Prices(10, 12, 15, 11, 7, 10, 14);
        var r = Renko(source, 2);
        Assert.Equal(new[] { 10.0, 12, 12, 10, 10, 12 }, r.Select(s => s.Open));
        Assert.Equal(new[] { 12.0, 14, 10, 8, 12, 14 }, r.Select(s => s.Close));
        Assert.Equal(new[] { 1, 1, -1, -1, 1, 1 }, r.Select(s => s.Direction));
        Assert.Equal(new[] { 0, 1, 2, 4, 4, 6 }, r.Select(s => s.SourceStartIndex));
        Assert.Equal(new[] { 1, 2, 4, 4, 6, 6 }, r.Select(s => s.SourceEndIndex));
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, r.Select(s => s.Slot));
        Assert.All(r, s => { Assert.Equal(2, s.BoxSize); Assert.Equal(1, s.BoxCount);
            Assert.Equal(source[s.SourceEndIndex], s.SourceEnd); });
    }

    [Fact]
    public void Renko_exact_thresholds_and_one_box_reversal_are_deterministic()
    {
        var two = Renko(Prices(10, 12, 10, 8), 2);
        Assert.Equal(new[] { 12.0, 8 }, two.Select(s => s.Close));
        var one = Renko(Prices(10, 12, 10, 8), 2, 1);
        Assert.Equal(new[] { 12.0, 10, 8 }, one.Select(s => s.Close));
        var three = Renko(Prices(10, 12, 8, 6), 2, 3);
        Assert.Equal(new[] { 12.0, 6 }, three.Select(s => s.Close));
        Assert.Equal(8, three[1].Open);
    }

    [Fact]
    public void Point_and_figure_extends_one_column_then_alternates_on_three_boxes()
    {
        var source = Prices(10, 12, 15, 11, 7, 10, 14);
        var r = PointAndFigure(source, 2);
        Assert.Equal(new[] { 10.0, 14, 8 }, r.Select(s => s.Open));
        Assert.Equal(new[] { 14.0, 8, 14 }, r.Select(s => s.Close));
        Assert.Equal(new[] { 2, 3, 3 }, r.Select(s => s.BoxCount));
        Assert.Equal(new[] { 1, -1, 1 }, r.Select(s => s.Direction));
        Assert.Equal(new[] { 0, 2, 4 }, r.Select(s => s.SourceStartIndex));
        Assert.Equal(new[] { 2, 4, 6 }, r.Select(s => s.SourceEndIndex));
        Assert.Equal(new[] { 0, 1, 2 }, r.Select(s => s.Slot));
        Assert.Equal(new[] { 12.0, 14 }, Enumerable.Range(1, r[0].BoxCount)
            .Select(i => r[0].Open + r[0].Direction * i * r[0].BoxSize!.Value));
    }

    [Fact]
    public void Point_and_figure_budget_counts_symbols_even_when_the_column_count_is_one()
    {
        Assert.Throws<InvalidOperationException>(() => PointAndFigure(Prices(10, 20), 1, 3, 9));
        Assert.Equal(10, Assert.Single(PointAndFigure(Prices(10, 20), 1, 3, 10)).BoxCount);
    }

    [Fact]
    public void Kagi_ignores_small_retracements_and_preserves_actual_extreme_source_rows()
    {
        var r = Kagi(Prices(10, 12, 15, 11, 7, 10, 14), 3);
        Assert.Equal(new[] { 10.0, 15, 7 }, r.Select(s => s.Open));
        Assert.Equal(new[] { 15.0, 7, 14 }, r.Select(s => s.Close));
        Assert.Equal(new[] { 0, 2, 4 }, r.Select(s => s.SourceStartIndex));
        Assert.Equal(new[] { 2, 4, 6 }, r.Select(s => s.SourceEndIndex));
        Assert.All(r, s => Assert.False(s.IsThick));
        var small = Kagi(Prices(10, 15, 13, 14), 3);
        Assert.Equal(1, Assert.Single(small).SourceEndIndex);
        Assert.Equal(15, small[0].Close);
    }

    [Fact]
    public void Kagi_splits_at_previous_shoulders_and_waists_without_faking_a_source_price()
    {
        var input = Prices(10, 15, 10, 16, 9);
        var r = Kagi(input, 3);
        Assert.Equal(new[] { 10.0, 15, 10, 15, 16, 10 }, r.Select(s => s.Open));
        Assert.Equal(new[] { 15.0, 10, 15, 16, 10, 9 }, r.Select(s => s.Close));
        Assert.Equal(new[] { false, false, false, true, true, false }, r.Select(s => s.IsThick));
        Assert.Equal(new[] { 0, 1, 2, 2, 3, 3 }, r.Select(s => s.Slot));
        Assert.Equal(new[] { 0, 1, 2, 2, 3, 3 }, r.Select(s => s.SourceStartIndex));
        Assert.Equal(new[] { 1, 2, 3, 3, 4, 4 }, r.Select(s => s.SourceEndIndex));
        Assert.Equal(input[3], r[2].SourceEnd);
        Assert.NotEqual(r[2].Close, r[2].SourceEnd.Close);
        Assert.All(r.Skip(1), s => Assert.False(s.IsSegmentStart));
    }

    [Fact]
    public void Kagi_exact_shoulder_touch_remains_thin_and_exact_reversal_starts_a_new_slot()
    {
        var r = Kagi(Prices(10, 15, 12, 15), 3);
        Assert.Equal(3, r.Count);
        Assert.All(r, s => Assert.False(s.IsThick));
        Assert.Equal(new[] { 0, 1, 2 }, r.Select(s => s.Slot));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void Gaps_reset_all_state_and_preserve_original_indices(int kind)
    {
        var input = Prices(10, 14, 999, 20, 24);
        input[2] = input[2] with { IsGap = true, Open = double.NaN, High = double.NaN,
            Low = double.NaN, Close = double.NaN, Volume = double.NaN };
        var r = Transform(input, kind);
        Assert.DoesNotContain(r, s => s.SourceStartIndex == 2 || s.SourceEndIndex == 2);
        var after = r.First(s => s.SourceStartIndex >= 3);
        Assert.True(after.IsSegmentStart);
        Assert.All(after.Sources, s => Assert.False(s.IsGap));
        if (kind < 2) Assert.Equal(3, after.Slot);
        if (kind == 1) Assert.Equal(19.5, after.Open);
        if (kind == 2) Assert.Equal(20, after.Open);
        if (kind is 3 or 4) Assert.Equal(20, after.Open);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void Empty_singleton_and_flat_series_have_explicit_behavior(int kind)
    {
        Assert.Empty(Transform([], kind));
        Assert.Equal(kind < 2 ? 1 : 0, Transform(Prices(5), kind).Count);
        var constant = Prices(5, 5, 5);
        if (kind < 2) Assert.Equal(3, Transform(constant, kind).Count);
        else Assert.Empty(Transform(constant, kind));
    }

    [Theory]
    [InlineData(double.NaN, 10, 8, 9)] [InlineData(9, double.PositiveInfinity, 8, 10)]
    [InlineData(9, 10, double.NegativeInfinity, 10)] [InlineData(9, 10, 8, double.NaN)]
    [InlineData(7, 10, 8, 9)] [InlineData(9, 10, 8, 11)] [InlineData(9, 8, 10, 9)]
    public void Invalid_ohlc_is_rejected_without_silent_sorting_or_clamping(double open, double high, double low, double close)
    {
        var p = Prices(10); p[0] = p[0] with { Open = open, High = high, Low = low, Close = close };
        Assert.Throws<ArgumentException>(() => Validate(p));
    }

    [Theory]
    [InlineData(-1)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public void Invalid_volume_is_rejected(double volume)
    {
        var input = Prices(10); input[0] = input[0] with { Volume = volume };
        Assert.Throws<ArgumentException>(() => Validate(input));
    }

    [Fact]
    public void Missing_volume_negative_prices_and_offsets_are_supported_but_duplicate_instants_are_not()
    {
        var input = Prices(-20, -10); input[0] = input[0] with { Volume = null };
        input[1] = input[1] with { Time = input[1].Time.ToOffset(TimeSpan.FromHours(8)) };
        Assert.Null(Validate(input)[0].Volume);
        Assert.NotEmpty(Renko(input, 2));
        input[1] = input[1] with { Time = input[0].Time.ToOffset(TimeSpan.FromHours(8)) };
        Assert.Throws<ArgumentException>(() => Validate(input));
        Assert.Throws<ArgumentException>(() => Validate(Prices(10, 12).Reverse()));
    }

    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public void Invalid_box_sizes_and_reversal_amounts_are_rejected(double size)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Renko([], size));
        Assert.Throws<ArgumentOutOfRangeException>(() => PointAndFigure([], size));
        Assert.Throws<ArgumentOutOfRangeException>(() => Kagi([], size));
    }

    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(1000001)]
    public void Invalid_output_or_reversal_budgets_are_rejected(int count)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Raw([], count));
        Assert.Throws<ArgumentOutOfRangeException>(() => HeikinAshi([], count));
        Assert.Throws<ArgumentOutOfRangeException>(() => Renko([], 1, count));
        Assert.Throws<ArgumentOutOfRangeException>(() => PointAndFigure([], 1, count));
        Assert.Throws<ArgumentOutOfRangeException>(() => Kagi([], 1, count));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void Output_limits_and_cancellation_fail_instead_of_returning_partial_data(int kind)
    {
        Assert.Throws<InvalidOperationException>(() => Transform(Prices(10, 15, 10, 16, 9), kind, 1));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => TransformCore(Prices(10, 15), kind, ChartFinancialTransforms.DefaultMaximumGeneratedSamples, canceled.Token));
    }

    [Fact]
    public void Tiny_boxes_and_extreme_price_differences_are_bounded()
    {
        Assert.Throws<InvalidOperationException>(() => Renko(Prices(10, 11), double.Epsilon));
        Assert.Throws<InvalidOperationException>(() => PointAndFigure(Prices(10, 11), double.Epsilon));
        var input = Prices(10, 11);
        input[0] = input[0] with { Open = -double.MaxValue, High = -double.MaxValue,
            Low = -double.MaxValue, Close = -double.MaxValue };
        input[1] = input[1] with { Open = double.MaxValue, High = double.MaxValue,
            Low = double.MaxValue, Close = double.MaxValue };
        Assert.Throws<ArithmeticException>(() => Renko(input, 1));
        Assert.Throws<ArithmeticException>(() => Kagi(input, 1));
        Assert.All(HeikinAshi(input), s =>
        { Assert.True(double.IsFinite(s.Open)); Assert.True(double.IsFinite(s.Close)); });
    }

    [Fact]
    public void Heikin_ashi_does_not_underflow_constant_subnormal_prices_outside_the_envelope()
    {
        foreach (var price in new[] { double.Epsilon, 2 * double.Epsilon, -double.Epsilon,
            Math.BitIncrement(Math.ScaleB(1, -1022)), -Math.BitIncrement(Math.ScaleB(1, -1022)) })
        {
            var source = Prices(10, 10).Select(p => p with { Open = price, High = price, Low = price, Close = price });
            Assert.All(HeikinAshi(source), sample =>
            { Assert.Equal(price, sample.Open); Assert.Equal(price, sample.Close);
                Assert.Equal(price, sample.Low); Assert.Equal(price, sample.High); });
        }
    }

    [Fact]
    public void Point_and_figure_rejects_boxes_that_collapse_at_the_head_of_a_column()
    {
        var input = Prices(10, 11);
        input[0] = input[0] with { Open = 1e20, High = 1e20, Low = 1e20, Close = 1e20 };
        input[1] = input[1] with { Open = 1e20 + 32768, High = 1e20 + 32768,
            Low = 1e20 + 32768, Close = 1e20 + 32768 };
        Assert.Throws<ArithmeticException>(() => PointAndFigure(input, 1));
    }

    [Fact]
    public void Point_and_figure_rejects_boxes_that_collapse_after_crossing_a_price_precision_boundary()
    {
        var boundary = Math.ScaleB(1, 53);
        var input = Prices(10, 11);
        input[0] = input[0] with { Open = boundary - 1, High = boundary - 1,
            Low = boundary - 1, Close = boundary - 1 };
        input[1] = input[1] with { Open = boundary + 4, High = boundary + 4,
            Low = boundary + 4, Close = boundary + 4 };
        Assert.Throws<ArithmeticException>(() => PointAndFigure(input, 1));
        // The last reverse step can itself round to a distinct value while an interior
        // one-box step would still collapse, so check the column's price resolution too.
        input[1] = input[1] with { Open = boundary + 2, High = boundary + 2,
            Low = boundary + 2, Close = boundary + 2 };
        Assert.Throws<ArithmeticException>(() => PointAndFigure(input, 1));
    }

    [Fact]
    public void Heikin_ashi_mixed_prices_near_the_normal_boundary_remain_inside_the_original_envelope()
    {
        var a = Math.BitIncrement(Math.ScaleB(1, -1022));
        var b = Math.BitIncrement(a);
        var p = Prices(10)[0];
        var positive = HeikinAshi([p with { Open = a, High = b, Low = a, Close = a }]);
        Assert.InRange(Assert.Single(positive).Close, a, b);
        var negative = HeikinAshi([p with { Open = -a, High = -a, Low = -b, Close = -a }]);
        Assert.InRange(Assert.Single(negative).Close, -b, -a);
    }

    [Fact]
    public void Input_limit_is_enforced_during_enumeration_and_null_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => Validate(null!));
        var start = Prices(10)[0];
        Assert.Throws<ArgumentOutOfRangeException>(() => Validate(
            Enumerable.Range(0, ChartFinancialTransforms.MaximumInputPoints + 1).Select(i =>
                start with { Time = start.Time.AddSeconds(i) })));
    }

    private static IReadOnlyList<ChartFinancialPoint> Validate(IEnumerable<ChartFinancialPoint> source) =>
        ChartFinancialTransforms.Validate(source, TestContext.Current.CancellationToken);
    private static IReadOnlyList<ChartFinancialSample> Raw(IEnumerable<ChartFinancialPoint> source,
        int maximum = ChartFinancialTransforms.DefaultMaximumGeneratedSamples) =>
        ChartFinancialTransforms.Raw(source, maximum, TestContext.Current.CancellationToken);
    private static IReadOnlyList<ChartFinancialSample> HeikinAshi(IEnumerable<ChartFinancialPoint> source,
        int maximum = ChartFinancialTransforms.DefaultMaximumGeneratedSamples) =>
        ChartFinancialTransforms.HeikinAshi(source, maximum, TestContext.Current.CancellationToken);
    private static IReadOnlyList<ChartFinancialSample> Renko(IEnumerable<ChartFinancialPoint> source, double size,
        int reversal = 2, int maximum = ChartFinancialTransforms.DefaultMaximumGeneratedSamples) =>
        ChartFinancialTransforms.Renko(source, size, reversal, maximum, TestContext.Current.CancellationToken);
    private static IReadOnlyList<ChartFinancialSample> PointAndFigure(IEnumerable<ChartFinancialPoint> source, double size,
        int reversal = 3, int maximum = ChartFinancialTransforms.DefaultMaximumGeneratedSamples) =>
        ChartFinancialTransforms.PointAndFigure(source, size, reversal, maximum, TestContext.Current.CancellationToken);
    private static IReadOnlyList<ChartFinancialSample> Kagi(IEnumerable<ChartFinancialPoint> source, double size,
        int maximum = ChartFinancialTransforms.DefaultMaximumGeneratedSamples) =>
        ChartFinancialTransforms.Kagi(source, size, maximum, TestContext.Current.CancellationToken);
    private static IReadOnlyList<ChartFinancialSample> Transform(IEnumerable<ChartFinancialPoint> source,
        int kind, int maximum = ChartFinancialTransforms.DefaultMaximumGeneratedSamples) =>
        TransformCore(source, kind, maximum, TestContext.Current.CancellationToken);
    private static IReadOnlyList<ChartFinancialSample> TransformCore(IEnumerable<ChartFinancialPoint> source,
        int kind, int maximum, CancellationToken token) => kind switch
    {
        0 => ChartFinancialTransforms.Raw(source, maximum, token),
        1 => ChartFinancialTransforms.HeikinAshi(source, maximum, token),
        2 => ChartFinancialTransforms.Renko(source, 2, 2, maximum, token),
        3 => ChartFinancialTransforms.PointAndFigure(source, 2, 3, maximum, token),
        _ => ChartFinancialTransforms.Kagi(source, 3, maximum, token)
    };
}
