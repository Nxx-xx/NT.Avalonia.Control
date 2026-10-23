using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class ThemeRiverStackTests
{
    [Fact]
    public void Transparent_base_centers_real_contributions_at_every_slot_without_changing_them()
    {
        double[] a = [10, 20, 30], b = [30, 10, 10];
        var baseline = ThemeRiverStack.CreateBaseline([a, b], 50);
        Assert.Equal(new[] { new ChartPoint(0, 30), new ChartPoint(1, 35), new ChartPoint(2, 30) }, baseline);
        for (var i = 0; i < 3; i++) Assert.Equal(50, (baseline[i].Y + baseline[i].Y + a[i] + b[i]) / 2);
        Assert.Equal(new double[] { 10, 20, 30 }, a); Assert.Equal(new double[] { 30, 10, 10 }, b);
    }
    [Fact]
    public void Automatic_center_leaves_nonnegative_spacers_and_zero_streams_remain_zero()
    {
        Assert.Equal(new[] { new ChartPoint(0, 5), new ChartPoint(1, 0), new ChartPoint(2, 10) }, ThemeRiverStack.CreateBaseline([new double[] { 10, 20, 0 }]));
        Assert.Equal(new[] { new ChartPoint(0, 0) }, ThemeRiverStack.CreateBaseline([new double[] { 0 }, new double[] { 0 }]));
        Assert.Empty(ThemeRiverStack.CreateBaseline([]));
    }
    [Theory]
    [InlineData(-1)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public void Invalid_contributions_are_rejected_without_inventing_gaps(double value) => Assert.Throws<ArgumentException>(() => ThemeRiverStack.CreateBaseline([new[] { value }]));
    [Fact]
    public void Alignment_insufficient_center_and_overflow_are_explicit_failures()
    {
        Assert.Throws<ArgumentException>(() => ThemeRiverStack.CreateBaseline([new double[] { 1 }, new double[] { 1, 2 }]));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThemeRiverStack.CreateBaseline([new double[] { 10 }], 4));
        Assert.Throws<OverflowException>(() => ThemeRiverStack.CreateBaseline([new[] { double.MaxValue }, new[] { double.MaxValue }]));
        Assert.Equal(0, Assert.Single(ThemeRiverStack.CreateBaseline([new[] { double.MaxValue }])).Y);
    }
}
