using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class StatisticsMathTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(.1, 3)]
    [InlineData(.25, 7.5)]
    [InlineData(.5, 15)]
    [InlineData(.75, 22.5)]
    [InlineData(1, 30)]
    public void Quantiles_use_type_seven_linear_ranks(double probability, double expected)
    {
        Assert.Equal(expected, ChartStatistics.Quantile([30, 0, 20, 10], probability), 12);
    }

    [Fact]
    public void Quantile_filters_invalid_observations_without_mutating_the_input()
    {
        double[] source = [3, double.NaN, 1, double.PositiveInfinity, 2, double.NegativeInfinity];
        var snapshot = source.ToArray();
        Assert.Equal(2, ChartStatistics.Quantile(source, .5));
        Assert.Equal(snapshot, source);
    }

    [Fact]
    public void Box_uses_type_seven_quartiles_and_observed_tukey_whiskers()
    {
        var box = ChartStatistics.BoxPlot([1, 2, 3, 4, 5, 6, 7, 8, 9, 30]);
        Assert.Equal(1, box.Minimum);
        Assert.Equal(3.25, box.LowerQuartile);
        Assert.Equal(5.5, box.Median);
        Assert.Equal(7.75, box.UpperQuartile);
        Assert.Equal(30, box.Maximum);
        Assert.Equal(1, box.LowerWhisker);
        Assert.Equal(9, box.UpperWhisker);
        Assert.Equal(7.5, box.Mean, 12);
        Assert.Equal(10, box.Count);
        Assert.Equal(0, box.OmittedCount);
        Assert.False(box.IsPreaggregated);
        Assert.Equal(new ChartStatisticalObservation(9, 30), Assert.Single(box.Outliers));
    }

    [Fact]
    public void Outliers_preserve_original_unfiltered_indices_and_input_order()
    {
        double[] source = [100, double.NaN, 1, 2, 3, 4, 5, 6, 7, 8, 9, -100, double.PositiveInfinity];
        var box = ChartStatistics.BoxPlot(source);
        Assert.Equal(11, box.Count);
        Assert.Equal(2, box.OmittedCount);
        Assert.Equal(new[] { new ChartStatisticalObservation(0, 100), new ChartStatisticalObservation(11, -100) }, box.Outliers);
        source[0] = 0;
        Assert.Equal(100, box.Outliers[0].Value);
        Assert.Throws<NotSupportedException>(() => ((IList<ChartStatisticalObservation>)box.Outliers)[0] = default);
    }

    [Fact]
    public void Values_exactly_on_the_tukey_fence_are_not_outliers()
    {
        // Type-7 Q1 = 0, Q3 = 4; fences are -6 and 10, each represented exactly.
        var box = ChartStatistics.BoxPlot([-6, 0, 0, 0, 4, 4, 4, 10]);
        Assert.Equal(-6, box.LowerWhisker);
        Assert.Equal(10, box.UpperWhisker);
        Assert.Empty(box.Outliers);
    }

    [Fact]
    public void Zero_fence_multiplier_does_not_place_whiskers_inside_the_box()
    {
        var box = ChartStatistics.BoxPlot([0, 10], 0);
        Assert.Equal(2.5, box.LowerQuartile);
        Assert.Equal(2.5, box.LowerWhisker);
        Assert.Equal(7.5, box.UpperQuartile);
        Assert.Equal(7.5, box.UpperWhisker);
        Assert.Equal(2, box.Outliers.Count);
    }

    [Fact]
    public void Singleton_and_constant_boxes_have_zero_spread()
    {
        foreach (var source in new[] { new double[] { 4 }, [4, 4, 4, 4] })
        {
            var box = ChartStatistics.BoxPlot(source);
            Assert.Equal(4, box.Minimum);
            Assert.Equal(4, box.LowerQuartile);
            Assert.Equal(4, box.Median);
            Assert.Equal(4, box.UpperQuartile);
            Assert.Equal(4, box.Maximum);
            Assert.Equal(4, box.Mean);
            Assert.Empty(box.Outliers);
        }
    }

    [Fact]
    public void Preaggregated_box_copies_its_outliers_and_marks_the_external_origin()
    {
        var outliers = new[] { new ChartStatisticalObservation(2, 20) };
        var box = ChartBoxPlotSummary.CreatePreaggregated(0, 2, 3, 4, 20, 0, 8, 5, 10, outliers);
        outliers[0] = new(0, 0);
        Assert.True(box.IsPreaggregated);
        Assert.Equal(20, box.Outliers[0].Value);
        Assert.Throws<NotSupportedException>(() => ((IList<ChartStatisticalObservation>)box.Outliers).Clear());
    }

    [Fact]
    public void Preaggregated_box_rejects_inverted_nonfinite_or_inconsistent_summary()
    {
        Assert.Throws<ArgumentException>(() => ChartBoxPlotSummary.CreatePreaggregated(0, 4, 3, 5, 9, 0, 9, 4, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartBoxPlotSummary.CreatePreaggregated(0, 2, 3, 4, double.NaN, 0, 9, 4, 10));
        Assert.Throws<ArgumentException>(() => ChartBoxPlotSummary.CreatePreaggregated(0, 2, 3, 4, 9, 0, 9, 10, 10));
        Assert.Throws<ArgumentException>(() => ChartBoxPlotSummary.CreatePreaggregated(0, 2, 3, 4, 9, 0, 9, 4, 0));
        Assert.Throws<ArgumentException>(() => ChartBoxPlotSummary.CreatePreaggregated(0, 2, 3, 4, 9, 0, 9, 4, 1));
        Assert.Throws<ArgumentException>(() => ChartBoxPlotSummary.CreatePreaggregated(0, 2, 3, 4, 9, 0, 9, 4, 10, [new(1, 5)]));
        Assert.Throws<ArgumentException>(() => ChartBoxPlotSummary.CreatePreaggregated(0, 2, 3, 4, 20, 0, 9, 4, 10, [new(1, 20), new(1, 20)]));
    }

    [Fact]
    public void Descriptive_summary_uses_n_minus_one_and_undefined_singleton_variance()
    {
        var summary = ChartStatistics.Summarize([1, 2, 3, 4, double.NaN]);
        Assert.Equal(4, summary.Count);
        Assert.Equal(1, summary.OmittedCount);
        Assert.Equal(2.5, summary.Mean, 12);
        Assert.Equal(5d / 3, summary.SampleVariance!.Value, 12);
        Assert.Equal(Math.Sqrt(5d / 3), summary.StandardDeviation!.Value, 12);
        var singleton = ChartStatistics.Summarize([7]);
        Assert.Null(singleton.SampleVariance);
        Assert.Null(singleton.StandardDeviation);
        Assert.Equal(0, ChartStatistics.Summarize([7, 7]).SampleVariance);
    }

    [Fact]
    public void Descriptive_summary_keeps_small_variation_at_a_large_offset()
    {
        var summary = ChartStatistics.Summarize([1e12, 1e12 + 1, 1e12 + 2]);
        Assert.Equal(1e12 + 1, summary.Mean);
        Assert.Equal(1, summary.SampleVariance!.Value, 12);
        Assert.Equal(1, summary.StandardDeviation!.Value, 12);
    }

    [Fact]
    public void Symmetric_and_asymmetric_error_magnitudes_create_data_endpoints()
    {
        var symmetric = ChartStatistics.SymmetricError(8, 2);
        Assert.Equal(8, symmetric.Center);
        Assert.Equal(6, symmetric.Lower);
        Assert.Equal(10, symmetric.Upper);
        var asymmetric = ChartStatistics.ErrorInterval(-2, 3, 7);
        Assert.Equal(-5, asymmetric.Lower);
        Assert.Equal(5, asymmetric.Upper);
        Assert.Equal(3, asymmetric.LowerError);
        Assert.Equal(7, asymmetric.UpperError);
    }

    [Fact]
    public void Mean_confidence_interval_uses_normal_critical_value_and_sample_standard_error()
    {
        // [1,2,3] has mean 2, sample SD 1; the 95% normal critical value is 1.959963984540054.
        var interval = ChartStatistics.MeanConfidenceInterval([1, 2, 3]);
        var halfWidth = 1.959963984540054 / Math.Sqrt(3);
        Assert.Equal(2, interval.Center, 12);
        Assert.InRange(Math.Abs(interval.Lower - (2 - halfWidth)), 0, 1e-8);
        Assert.InRange(Math.Abs(interval.Upper - (2 + halfWidth)), 0, 1e-8);
        Assert.Equal(5, ChartStatistics.MeanConfidenceInterval([5, 5]).Lower);
        Assert.Throws<ArgumentException>(() => ChartStatistics.MeanConfidenceInterval([5]));
    }

    [Theory]
    [InlineData(.5, .6744897501960817)]
    [InlineData(.9, 1.6448536269514722)]
    [InlineData(.99, 2.5758293035489004)]
    [InlineData(.999999, 4.891638475698591)]
    public void Confidence_intervals_cover_central_and_tail_normal_quantiles(double confidence, double critical)
    {
        // [-1,0,1] has sample SD 1 and mean zero.
        var result = ChartStatistics.MeanConfidenceInterval([-1, 0, 1], confidence);
        Assert.InRange(Math.Abs(result.Upper - critical / Math.Sqrt(3)), 0, 1e-8);
    }

    [Fact]
    public void Histogram_count_includes_left_edges_and_the_final_right_edge()
    {
        var histogram = ChartStatistics.Histogram([0, 1, 2, 3, 4], 2);
        Assert.Equal(new ChartHistogramBin(0, 2, 2, 2), histogram.Bins[0]);
        Assert.Equal(new ChartHistogramBin(2, 4, 3, 3), histogram.Bins[1]);
        Assert.Equal(5, histogram.IncludedCount);
        Assert.Equal(0, histogram.ExcludedCount);
        Assert.Throws<NotSupportedException>(() => ((IList<ChartHistogramBin>)histogram.Bins).Clear());
    }

    [Fact]
    public void Unequal_histogram_bins_normalize_by_included_count_and_individual_width()
    {
        double[] samples = [-1, 0, 1, 2, 3, 4, 99, double.NaN];
        var probability = ChartStatistics.HistogramByBoundaries(samples, [0, 2, 5], ChartHistogramNormalization.Probability);
        Assert.Equal(7, probability.Count);
        Assert.Equal(1, probability.OmittedCount);
        Assert.Equal(5, probability.IncludedCount);
        Assert.Equal(2, probability.ExcludedCount);
        Assert.Equal(.4, probability.Bins[0].Value, 12);
        Assert.Equal(.6, probability.Bins[1].Value, 12);
        var density = ChartStatistics.HistogramByBoundaries(samples, [0, 2, 5], ChartHistogramNormalization.Density);
        Assert.Equal(.2, density.Bins[0].Value, 12);
        Assert.Equal(.2, density.Bins[1].Value, 12);
        Assert.Equal(1, density.Bins.Sum(bin => bin.Value * (bin.Upper - bin.Lower)), 12);
    }

    [Fact]
    public void Fixed_width_histogram_keeps_the_requested_width_then_shortens_the_last_bin()
    {
        var result = ChartStatistics.HistogramByWidth([0, 1, 2, 3, 4, 5], 2, ChartHistogramNormalization.Density);
        Assert.Equal(new double[] { 0, 2, 4 }, result.Bins.Select(bin => bin.Lower));
        Assert.Equal(new double[] { 2, 4, 5 }, result.Bins.Select(bin => bin.Upper));
        Assert.Equal(new[] { 2, 2, 2 }, result.Bins.Select(bin => bin.Count));
        Assert.Equal(1d / 6, result.Bins[0].Value, 12);
        Assert.Equal(1d / 3, result.Bins[2].Value, 12);
    }

    [Fact]
    public void Histograms_expand_implicit_constant_domain_but_do_not_rewrite_explicit_boundaries()
    {
        var result = ChartStatistics.Histogram([5, 5, 5], 2);
        Assert.Equal(4.5, result.Bins[0].Lower);
        Assert.Equal(5.5, result.Bins[^1].Upper);
        Assert.Equal(3, result.Bins[1].Count);
        Assert.Throws<ArgumentException>(() => ChartStatistics.Histogram([5, 5], 2, minimum: 5, maximum: 5));
    }

    [Fact]
    public void Explicit_histogram_domain_with_no_observations_has_zero_height()
    {
        var result = ChartStatistics.Histogram([1, 2], 2, ChartHistogramNormalization.Density, 10, 20);
        Assert.Equal(0, result.IncludedCount);
        Assert.Equal(2, result.ExcludedCount);
        Assert.All(result.Bins, bin => Assert.Equal(0, bin.Value));
    }

    [Fact]
    public void Density_is_a_gaussian_sum_and_does_not_renormalize_a_truncated_domain()
    {
        var result = ChartStatistics.KernelDensity([0], 3, 1, -1, 1);
        Assert.Equal(new double[] { -1, 0, 1 }, result.Points.Select(point => point.X));
        Assert.Equal(.24197072451914337, result.Points[0].Y, 12);
        Assert.Equal(.3989422804014327, result.Points[1].Y, 12);
        Assert.Equal(.24197072451914337, result.Points[2].Y, 12);
        Assert.Equal(.6409130049205761, result.SampledArea, 12);
        Assert.Equal(1, result.Bandwidth);
        Assert.Equal(1, result.Count);
        Assert.Equal(3, result.SampleCount);
        Assert.Throws<NotSupportedException>(() => ((IList<ChartPoint>)result.Points).Clear());
    }

    [Fact]
    public void Density_mixture_has_the_average_of_each_normalized_kernel()
    {
        // At x=0, the mean of Gaussian kernels centered at -1 and +1 is phi(1).
        var result = ChartStatistics.KernelDensity([-1, 1, double.NaN], 3, 1, -1, 1);
        Assert.Equal(.24197072451914337, result.Points[1].Y, 12);
        Assert.Equal(1, result.OmittedCount);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Density_automatic_bandwidth_uses_sample_sd_and_type_seven_iqr()
    {
        // [0,1,2] has sample SD 1, IQR 1, n=3.
        var result = ChartStatistics.KernelDensity([0, 1, 2], 5);
        var expected = .9 / 1.34 * Math.Pow(3, -.2);
        Assert.Equal(expected, result.Bandwidth, 12);
        Assert.Equal(-3 * expected, result.Points[0].X, 12);
        Assert.Equal(2 + 3 * expected, result.Points[^1].X, 12);
    }

    [Fact]
    public void Density_automatic_bandwidth_has_documented_constant_and_singleton_fallbacks()
    {
        Assert.Equal(.9, ChartStatistics.KernelDensity([5], 3).Bandwidth, 12);
        Assert.Equal(.9 * Math.Pow(2, -.2), ChartStatistics.KernelDensity([5, 5], 3).Bandwidth, 12);
        Assert.Equal(9, ChartStatistics.KernelDensity([1000], 3).Bandwidth, 12);
        var manyTies = ChartStatistics.KernelDensity([0, 0, 0, 0, 1], 3);
        Assert.Equal(.9 * Math.Sqrt(.2) * Math.Pow(5, -.2), manyTies.Bandwidth, 12);
    }

    [Fact]
    public void Wide_density_domain_integrates_the_standard_kernel_without_artificial_rescaling()
    {
        var result = ChartStatistics.KernelDensity([0], 1001, 1, -6, 6);
        Assert.InRange(result.SampledArea, .999999997, 1);
    }

    [Fact]
    public void Ecdf_uses_right_continuous_tied_jumps_and_sorted_unique_knots()
    {
        var result = ChartStatistics.EmpiricalCumulativeDistribution([3, 1, 1, 2, double.NaN]);
        Assert.Equal(new[] { new ChartPoint(1, .5), new ChartPoint(2, .75), new ChartPoint(3, 1) }, result);
        Assert.Throws<NotSupportedException>(() => ((IList<ChartPoint>)result).Clear());
    }

    [Fact]
    public void Pearson_filters_whole_pairs_and_handles_positive_negative_and_zero_correlation()
    {
        Assert.Equal(1, ChartStatistics.PearsonCorrelation([new(1, 2), new(2, 4), new(3, 6), new(double.NaN, 99)]), 12);
        Assert.Equal(-1, ChartStatistics.PearsonCorrelation([new(1, 6), new(2, 4), new(3, 2)]), 12);
        Assert.Equal(0, ChartStatistics.PearsonCorrelation([new(-1, 1), new(0, -2), new(1, 1)]), 12);
    }

    [Fact]
    public void Pearson_keeps_small_variation_on_large_offsets_and_avoids_extreme_product_overflow()
    {
        Assert.Equal(1, ChartStatistics.PearsonCorrelation([new(1e12, -1e12), new(1e12 + 1, -1e12 + 2), new(1e12 + 2, -1e12 + 4)]), 12);
        Assert.Equal(-1, ChartStatistics.PearsonCorrelation([new(-1e308, 1e308), new(0, 0), new(1e308, -1e308)]), 12);
        Assert.Throws<ArgumentException>(() => ChartStatistics.PearsonCorrelation([new(1, 1), new(1, 2)]));
        Assert.Throws<ArgumentException>(() => ChartStatistics.PearsonCorrelation([new(1, 1), new(double.PositiveInfinity, 2)]));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-.1)]
    [InlineData(1.1)]
    public void Quantile_rejects_invalid_probabilities(double value) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartStatistics.Quantile([1, 2], value));

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-1)]
    public void Nonnegative_numeric_parameters_reject_invalid_values(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartStatistics.BoxPlot([1, 2], value));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartStatistics.SymmetricError(1, value));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartStatistics.ErrorInterval(1, value, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartStatistics.ErrorInterval(1, 2, value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Confidence_level_is_finite_and_strictly_between_zero_and_one(double value) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartStatistics.MeanConfidenceInterval([1, 2], value));

    [Fact]
    public void Histograms_reject_invalid_bounds_counts_widths_and_normalization()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartStatistics.Histogram([1], 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartStatistics.Histogram([1], 65537));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartStatistics.Histogram([1], minimum: double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartStatistics.Histogram([1], normalization: (ChartHistogramNormalization)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartStatistics.HistogramByWidth([1], 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartStatistics.HistogramByWidth([1], double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartStatistics.HistogramByWidth([0, 1], double.Epsilon));
        Assert.Throws<ArgumentException>(() => ChartStatistics.HistogramByBoundaries([1], [0]));
        Assert.Throws<ArgumentException>(() => ChartStatistics.HistogramByBoundaries([1], [0, 1, 1]));
        Assert.Throws<ArgumentException>(() => ChartStatistics.HistogramByBoundaries([1], [1, 0]));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartStatistics.HistogramByBoundaries([1], [0, double.NaN]));
    }

    [Fact]
    public void Density_rejects_invalid_bandwidth_sample_count_and_domain()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartStatistics.KernelDensity([0], 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartStatistics.KernelDensity([0], 4097));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartStatistics.KernelDensity([0], bandwidth: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartStatistics.KernelDensity([0], bandwidth: double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartStatistics.KernelDensity([0], bandwidth: double.PositiveInfinity));
        Assert.Throws<ArgumentException>(() => ChartStatistics.KernelDensity([0], minimum: 1, maximum: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartStatistics.KernelDensity([0], minimum: double.NaN));
        Assert.Throws<ArgumentException>(() => ChartStatistics.KernelDensity([1e16], 4096, 1, 1e16, 1e16 + 2));
    }

    [Fact]
    public void Empty_or_entirely_nonfinite_inputs_cannot_silently_produce_statistical_data()
    {
        foreach (var values in new[] { Array.Empty<double>(), [double.NaN, double.PositiveInfinity] })
        {
            Assert.Throws<ArgumentException>(() => ChartStatistics.Quantile(values, .5));
            Assert.Throws<ArgumentException>(() => ChartStatistics.Summarize(values));
            Assert.Throws<ArgumentException>(() => ChartStatistics.BoxPlot(values));
            Assert.Throws<ArgumentException>(() => ChartStatistics.Histogram(values));
            Assert.Throws<ArgumentException>(() => ChartStatistics.KernelDensity(values));
            Assert.Throws<ArgumentException>(() => ChartStatistics.EmpiricalCumulativeDistribution(values));
        }
        Assert.Throws<ArgumentNullException>(() => ChartStatistics.BoxPlot(null!));
        Assert.Throws<ArgumentNullException>(() => ChartStatistics.HistogramByBoundaries([1], null!));
    }

    [Fact]
    public void Unrepresentable_results_throw_instead_of_returning_nonfinite_coordinates()
    {
        Assert.Throws<OverflowException>(() => ChartStatistics.SymmetricError(double.MaxValue, double.MaxValue));
        Assert.Throws<OverflowException>(() => ChartStatistics.Summarize([-1e200, 1e200]));
        Assert.Throws<OverflowException>(() => ChartStatistics.BoxPlot([-double.MaxValue, double.MaxValue]));
        Assert.Throws<OverflowException>(() => ChartStatistics.Histogram([-double.MaxValue, double.MaxValue]));
        Assert.Throws<OverflowException>(() => ChartStatistics.KernelDensity([0], bandwidth: double.Epsilon));
        Assert.Throws<OverflowException>(() => ChartStatistics.KernelDensity([double.MaxValue], bandwidth: double.MaxValue));
        Assert.Equal(0, ChartStatistics.Quantile([-double.MaxValue, double.MaxValue], .5));
        Assert.Equal(double.MaxValue, ChartStatistics.Summarize([double.MaxValue, double.MaxValue]).Mean);
    }
}
