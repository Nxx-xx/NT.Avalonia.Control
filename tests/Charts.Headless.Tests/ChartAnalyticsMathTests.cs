using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class ChartAnalyticsMathTests
{
    [Theory]
    [InlineData(ChartMovingAverageType.Simple, 11d / 3, 6, 14d / 3)]
    [InlineData(ChartMovingAverageType.Exponential, 11d / 3, 19d / 3, 31d / 6)]
    [InlineData(ChartMovingAverageType.Weighted, 21d / 6, 37d / 6, 31d / 6)]
    [InlineData(ChartMovingAverageType.Triangular, 19d / 4, 19d / 4, 23d / 4)]
    public void Moving_averages_have_independent_hand_calculated_oracles(ChartMovingAverageType type, double first, double second, double third)
    {
        var input = Points(2, 8, 1, 9, 4);
        var result = Average(input, 3, type);
        Assert.Equal(input.Length, result.Count);
        Assert.Equal(input.Select(point => point.X), result.Select(point => point.X));
        Assert.True(double.IsNaN(result[0].Y));
        Assert.True(double.IsNaN(result[1].Y));
        Near(first, result[2].Y);
        Near(second, result[3].Y);
        Near(third, result[4].Y);
        Assert.Equal(new[] { 2d, 8, 1, 9, 4 }, input.Select(point => point.Y));
    }

    [Theory]
    [InlineData(ChartMovingAverageType.Simple)]
    [InlineData(ChartMovingAverageType.Exponential)]
    [InlineData(ChartMovingAverageType.Weighted)]
    [InlineData(ChartMovingAverageType.Triangular)]
    public void Missing_values_restart_the_complete_warmup_and_preserve_source_indices(ChartMovingAverageType type)
    {
        ChartPoint[] input = [new(5, 2), new(9, 4), new(10, double.NaN), new(13, 10), new(17, 20), new(double.PositiveInfinity, 30), new(28, 40), new(29, 60)];
        var result = Average(input, 2, type);
        Assert.Equal(input.Select(point => point.X), result.Select(point => point.X));
        foreach (var index in new[] { 0, 2, 3, 5, 6 }) Assert.True(double.IsNaN(result[index].Y));
        Assert.True(double.IsFinite(result[1].Y));
        Assert.True(double.IsFinite(result[4].Y));
        Assert.True(double.IsFinite(result[7].Y));
        Near(type == ChartMovingAverageType.Weighted ? 160d / 3 : 50, result[7].Y);
    }

    [Fact]
    public void Triangular_even_period_uses_a_four_observation_symmetric_kernel()
    {
        var result = Average(Points(2, 8, 1, 9, 4), 4, ChartMovingAverageType.Triangular);
        Assert.All(result.Take(3), point => Assert.True(double.IsNaN(point.Y)));
        Near(29d / 6, result[3].Y); // [1,2,2,1] / 6
        Near(32d / 6, result[4].Y);
    }

    [Theory]
    [InlineData(ChartMovingAverageType.Simple)]
    [InlineData(ChartMovingAverageType.Exponential)]
    [InlineData(ChartMovingAverageType.Weighted)]
    [InlineData(ChartMovingAverageType.Triangular)]
    public void One_observation_period_is_the_identity_even_after_extreme_values(ChartMovingAverageType type)
    {
        var input = Points(1e308, 1, -1e308, -1);
        Assert.Equal(input, Average(input, 1, type));
    }

    [Theory]
    [InlineData(ChartMovingAverageType.Simple)]
    [InlineData(ChartMovingAverageType.Exponential)]
    [InlineData(ChartMovingAverageType.Weighted)]
    [InlineData(ChartMovingAverageType.Triangular)]
    public void Finite_extremes_do_not_overflow_a_representable_average(ChartMovingAverageType type)
    {
        var constant = Average(Points(1e308, 1e308, 1e308, 1e308), 2, type);
        Assert.All(constant.Skip(1), point => Assert.Equal(1e308, point.Y));
        var opposing = Average(Points(1e308, -1e308, 1e308, -1e308), 2, type);
        Assert.All(opposing.Skip(1), point => Assert.True(double.IsFinite(point.Y)));
    }

    [Theory]
    [InlineData(ChartMovingAverageType.Simple)]
    [InlineData(ChartMovingAverageType.Weighted)]
    [InlineData(ChartMovingAverageType.Triangular)]
    public void Removing_a_huge_outlier_does_not_destroy_later_small_values(ChartMovingAverageType type)
    {
        var result = Average(Points(1e308, 1, 1), 2, type);
        Assert.Equal(1, result[2].Y);
    }

    [Fact]
    public void Bollinger_bands_use_population_standard_deviation_and_restart_after_gaps()
    {
        var result = Bands(Points(1, 2, 3, double.NaN, 4, 5, 6), 3, 2);
        foreach (var index in new[] { 0, 1, 3, 4, 5 })
        { Assert.True(double.IsNaN(result[index].Middle)); Assert.True(double.IsNaN(result[index].Upper)); Assert.True(double.IsNaN(result[index].Lower)); }
        Near(2, result[2].Middle);
        Near(2 + 2 * Math.Sqrt(2d / 3), result[2].Upper);
        Near(2 - 2 * Math.Sqrt(2d / 3), result[2].Lower);
        Near(5, result[6].Middle);
        Near(5 + 2 * Math.Sqrt(2d / 3), result[6].Upper);
        Assert.Equal(Enumerable.Range(0, 7).Select(value => (double)value), result.Select(point => point.X));
    }

    [Fact]
    public void Bollinger_extremes_are_scaled_and_unrepresentable_bands_fail_explicitly()
    {
        var constant = Bands(Points(1e308, 1e308), 2);
        Assert.Equal(new ChartBollingerPoint(1, 1e308, 1e308, 1e308), constant[1]);
        var opposing = Bands(Points(1e308, -1e308), 2, 1);
        Assert.Equal(new ChartBollingerPoint(1, 0, 1e308, -1e308), opposing[1]);
        Assert.Throws<OverflowException>(() => Bands(Points(1e308, -1e308), 2, 2));
        var recovered = Bands(Points(1e308, 1, 1), 2);
        Assert.Equal(new ChartBollingerPoint(2, 1, 1, 1), recovered[2]);
    }

    [Fact]
    public void Fit_polynomials_uses_centered_coordinates_at_a_large_X_offset()
    {
        var input = Enumerable.Range(-5, 11).Select(value => new ChartPoint(1e12 + value, 3 + 2d * value + .5 * value * value)).ToArray();
        var fit = Fit(input, ChartTrendlineType.Polynomial, polynomialDegree: 2, sampleCount: 11,
            minimum: 1e12 - 6, maximum: 1e12 + 6);
        Assert.Equal(ChartTrendlineType.Polynomial, fit.Type);
        Assert.Equal(11, fit.Count);
        Assert.Equal(0, fit.OmittedCount);
        Assert.Equal(11, fit.Points.Count);
        Near(9, fit.Predict(1e12 + 2));
        Near(9, fit.Predict(1e12 - 6));
        Near(33, fit.Predict(1e12 + 6));
        Near(1, fit.RSquared!.Value);
    }

    [Fact]
    public void Linear_fit_accepts_opposite_finite_extremes_without_overflowing_the_span()
    {
        var fit = Fit(new[] { new ChartPoint(-1e308, -1e308), new ChartPoint(0, 0), new ChartPoint(1e308, 1e308) }, sampleCount: 3);
        Near(1e308, fit.Predict(1e308), 2e-14);
        Near(-1e308, fit.Predict(-1e308), 2e-14);
        Near(1, fit.RSquared!.Value);
        Assert.All(fit.Points, point => Assert.True(double.IsFinite(point.X) && double.IsFinite(point.Y)));
    }

    [Theory]
    [InlineData(ChartTrendlineType.Linear)]
    [InlineData(ChartTrendlineType.Exponential)]
    [InlineData(ChartTrendlineType.Logarithmic)]
    [InlineData(ChartTrendlineType.Power)]
    public void Regression_models_recover_exact_functions_and_predict_outside_observed_X(ChartTrendlineType type)
    {
        double Model(double x) => type switch
        {
            ChartTrendlineType.Linear => 4 + 3 * x,
            ChartTrendlineType.Exponential => 2 * Math.Exp(.3 * x),
            ChartTrendlineType.Logarithmic => 4 + 3 * Math.Log(x),
            _ => 2 * Math.Pow(x, 1.5)
        };
        var input = Enumerable.Range(1, 8).Select(value => new ChartPoint(value, Model(value))).ToArray();
        var result = Fit(input, type, sampleCount: 17, minimum: .5, maximum: 10);
        Near(Model(9), result.Predict(9));
        Near(Model(.5), result.Points[0].Y);
        Near(Model(10), result.Points[^1].Y);
        Near(1, result.RSquared!.Value);
        Assert.Equal(.5, result.Points[0].X);
        Assert.Equal(10, result.Points[^1].X);
    }

    [Theory]
    [InlineData(ChartTrendlineType.Exponential, 4, 3)]
    [InlineData(ChartTrendlineType.Logarithmic, 4, 3)]
    [InlineData(ChartTrendlineType.Power, 2, 5)]
    public void Transformed_fits_omit_invalid_domains_and_report_observation_counts(ChartTrendlineType type, int count, int omitted)
    {
        ChartPoint[] input = [new(-1, 2), new(0, 3), new(1, 4), new(2, 8), new(3, 0), new(4, -1), new(double.NaN, 9)];
        var result = Fit(input, type);
        Assert.Equal(count, result.Count);
        Assert.Equal(omitted, result.OmittedCount);
        if (type is ChartTrendlineType.Logarithmic or ChartTrendlineType.Power)
            Assert.Throws<ArgumentOutOfRangeException>(() => result.Predict(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => result.Predict(double.NaN));
    }

    [Fact]
    public void Exponential_R_squared_is_measured_against_original_Y_residuals()
    {
        var input = Points(1, 3, 8, 30, 100);
        var fit = Fit(input, ChartTrendlineType.Exponential);
        var mean = input.Average(point => point.Y);
        var expected = 1 - input.Sum(point => Math.Pow(point.Y - fit.Predict(point.X), 2)) / input.Sum(point => Math.Pow(point.Y - mean, 2));
        Near(expected, fit.RSquared!.Value);
    }

    [Fact]
    public void Moving_average_trend_retains_alignment_and_does_not_claim_a_regression_model()
    {
        var input = Points(1, 2, double.NaN, 4, 5);
        var result = Fit(input, ChartTrendlineType.MovingAverage, period: 2);
        Assert.Equal(Average(input, 2), result.Points);
        Assert.Equal(4, result.Count);
        Assert.Equal(1, result.OmittedCount);
        Assert.Null(result.RSquared);
        Assert.Throws<InvalidOperationException>(() => result.Predict(3));
        Assert.Throws<ArgumentException>(() => Fit(input, ChartTrendlineType.MovingAverage, minimum: 0));
    }

    [Fact]
    public void Empty_average_and_constant_Y_fit_have_explicit_contracts()
    {
        Assert.Empty(Average(Array.Empty<ChartPoint>()));
        Assert.Empty(Bands(Array.Empty<ChartPoint>()));
        Assert.Empty(Fit(Array.Empty<ChartPoint>(), ChartTrendlineType.MovingAverage).Points);
        var fit = Fit(Points(1e308, 1e308, 1e308));
        Assert.Equal(1e308, fit.Predict(100));
        Assert.Null(fit.RSquared);
    }

    [Fact]
    public void Rank_deficiency_insufficient_positive_domains_and_excessive_parameters_fail_boundedly()
    {
        Assert.Throws<ArgumentException>(() => Fit(Array.Empty<ChartPoint>()));
        Assert.Throws<ArgumentException>(() => Fit(new[] { new ChartPoint(1, 1), new ChartPoint(1, 2) }));
        Assert.Throws<ArgumentException>(() => Fit(new[] { new ChartPoint(1, 1), new ChartPoint(2, 2), new ChartPoint(1, 3), new ChartPoint(2, 4) }, ChartTrendlineType.Polynomial, polynomialDegree: 2));
        Assert.Throws<ArgumentException>(() => Fit(Points(-1, 0, -3), ChartTrendlineType.Exponential));
        Assert.Throws<ArgumentOutOfRangeException>(() => Fit(Points(1, 2, 3), ChartTrendlineType.Polynomial, polynomialDegree: 11));
        Assert.Throws<ArgumentOutOfRangeException>(() => Fit(Points(1, 2), sampleCount: 4097));
        Assert.Throws<ArgumentOutOfRangeException>(() => Fit(new[] { new ChartPoint(1, 1), new ChartPoint(2, 2) }, ChartTrendlineType.Power, minimum: 0));
        Assert.Throws<ArgumentException>(() => Fit(Points(1, 2), minimum: 2, maximum: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Average(Points(1), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Average(Points(1), 65537));
        Assert.Throws<ArgumentOutOfRangeException>(() => Average(Points(1), type: (ChartMovingAverageType)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => Fit(Points(1, 2), (ChartTrendlineType)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => Bands(Points(1), standardDeviationMultiplier: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Bands(Points(1), standardDeviationMultiplier: double.PositiveInfinity));
        Assert.Throws<ArgumentNullException>(() => Average(null!));
        Assert.Throws<ArgumentException>(() => Average(Enumerable.Repeat(new ChartPoint(0, 1), 1_000_001)));
    }

    [Fact]
    public void Nonrepresentable_predictions_fail_instead_of_emitting_infinite_geometry()
    {
        var fit = Fit(Points(1, Math.Exp(1), Math.Exp(2)), ChartTrendlineType.Exponential);
        Assert.Throws<OverflowException>(() => fit.Predict(1000));
        Assert.Throws<OverflowException>(() => fit.Predict(-1000));
        Assert.Throws<OverflowException>(() => Fit(Points(1, Math.Exp(1), Math.Exp(2)), ChartTrendlineType.Exponential, maximum: 1000));
    }

    [Fact]
    public void Source_is_enumerated_once_results_are_read_only_and_cancellation_is_observed_during_reading()
    {
        var enumerations = 0;
        IEnumerable<ChartPoint> Once()
        { enumerations++; if (enumerations > 1) throw new InvalidOperationException(); yield return new(1, 2); yield return new(2, 4); }
        var result = Average(Once(), 1);
        Assert.Equal(1, enumerations);
        Assert.Throws<NotSupportedException>(() => ((IList<ChartPoint>)result)[0] = new(0, 0));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        IEnumerable<ChartPoint> CancelDuringRead()
        { yield return new(1, 1); cancellation.Cancel(); yield return new(2, 2); }
        Assert.Throws<OperationCanceledException>(() => ChartAnalytics.MovingAverage(CancelDuringRead(), cancellationToken: cancellation.Token));
        Assert.Throws<OperationCanceledException>(() => ChartAnalytics.BollingerBands(Points(1), cancellationToken: cancellation.Token));
        Assert.Throws<OperationCanceledException>(() => ChartAnalytics.FitTrend(Points(1, 2), cancellationToken: cancellation.Token));
    }

    private static IReadOnlyList<ChartPoint> Average(IEnumerable<ChartPoint> points, int period = 14,
        ChartMovingAverageType type = ChartMovingAverageType.Simple) =>
        ChartAnalytics.MovingAverage(points, period, type, TestContext.Current.CancellationToken);
    private static IReadOnlyList<ChartBollingerPoint> Bands(IEnumerable<ChartPoint> points, int period = 20,
        double standardDeviationMultiplier = 2) =>
        ChartAnalytics.BollingerBands(points, period, standardDeviationMultiplier, TestContext.Current.CancellationToken);
    private static ChartTrendlineResult Fit(IEnumerable<ChartPoint> points, ChartTrendlineType type = ChartTrendlineType.Linear,
        int polynomialDegree = 2, int period = 14, int sampleCount = 128, double? minimum = null, double? maximum = null) =>
        ChartAnalytics.FitTrend(points, type, polynomialDegree, period, sampleCount, minimum, maximum, TestContext.Current.CancellationToken);

    private static ChartPoint[] Points(params double[] values) => values.Select((value, index) => new ChartPoint(index, value)).ToArray();
    private static void Near(double expected, double actual, double relativeTolerance = 1e-10)
    {
        Assert.True(double.IsFinite(actual), $"Expected finite {expected}, actual {actual}.");
        var scale = Math.Max(1, Math.Abs(expected));
        Assert.True(Math.Abs(expected / scale - actual / scale) <= relativeTolerance, $"Expected {expected:R}, actual {actual:R}.");
    }
}
