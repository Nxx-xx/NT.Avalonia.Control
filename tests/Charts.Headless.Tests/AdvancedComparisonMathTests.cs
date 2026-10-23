using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class AdvancedComparisonMathTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void Diverging_keeps_sign_and_uses_one_zero_origin()
    {
        var result = ChartAdvancedComparison.Diverging([new(-20, "loss"), new(40, "gain"), new(0)], cancellationToken: Token);
        Equal(.5, result.Zero);
        Equal(.25, result.Rows[0].End); Equal(1, result.Rows[1].End);
        Assert.All(result.Rows, row => Equal(result.Zero, row.Start));
        Assert.Equal(-20, result.Rows[0].Value); Assert.Equal("loss", result.Rows[0].Label);
        Equal(1d / 6, result.Rows[0].Y); Equal(1d / 3, result.Rows[0].Height);
    }

    [Fact]
    public void Diverging_sort_is_stable_and_retains_original_indices()
    {
        var source = new[] { new DivergingValue(2), new DivergingValue(-1), new DivergingValue(2), new DivergingValue(0) };
        var ascending = ChartAdvancedComparison.Diverging(source, ComparisonSort.ValueAscending, Token);
        var descending = ChartAdvancedComparison.Diverging(source, ComparisonSort.ValueDescending, Token);
        Assert.Equal([1, 3, 0, 2], ascending.Rows.Select(row => row.SourceIndex));
        Assert.Equal([0, 2, 3, 1], descending.Rows.Select(row => row.SourceIndex));
        Assert.Equal(2, source[0].Value);
    }

    [Fact]
    public void Diverging_extreme_domains_do_not_overflow_or_lose_origin()
    {
        var result = ChartAdvancedComparison.Diverging([new(-double.MaxValue), new(double.MaxValue)], cancellationToken: Token);
        Equal(.5, result.Zero); Equal(0, result.Rows[0].End); Equal(1, result.Rows[1].End);
        var subnormal = ChartAdvancedComparison.Diverging([new(-double.Epsilon), new(double.Epsilon)], cancellationToken: Token);
        Equal(.5, subnormal.Zero); Equal(0, subnormal.Rows[0].End); Equal(1, subnormal.Rows[1].End);
    }

    [Fact]
    public void Empty_and_zero_layouts_have_finite_neutral_geometry()
    {
        Assert.Empty(ChartAdvancedComparison.Diverging([], cancellationToken: Token).Rows);
        Equal(.5, ChartAdvancedComparison.Diverging([new(0)], cancellationToken: Token).Zero);
        var pair = ChartAdvancedComparison.Paired([new(0, 0)], cancellationToken: Token);
        Equal(.5, pair.Rows[0].FirstPosition); Equal(.5, pair.Rows[0].SecondPosition);
        Assert.Empty(ChartAdvancedComparison.Mekko([], Token).Cells);
        Assert.Empty(ChartAdvancedComparison.Venn([], 2, Token).Circles);
    }

    [Fact]
    public void Paired_regular_values_share_a_scale_including_negative_endpoints()
    {
        var result = ChartAdvancedComparison.Paired([new(-10, 30), new(10, 20)], cancellationToken: Token);
        Equal(.25, result.Zero); Equal(0, result.Rows[0].FirstPosition); Equal(1, result.Rows[0].SecondPosition);
        Equal(.5, result.Rows[1].FirstPosition); Equal(.75, result.Rows[1].SecondPosition);
        Assert.False(result.IsMirrored);
    }

    [Fact]
    public void Mirrored_pairs_use_magnitudes_and_one_shared_scale()
    {
        var result = ChartAdvancedComparison.Paired([new(20, 40), new(40, 10)], true, cancellationToken: Token);
        Equal(.5, result.Zero); Equal(.25, result.Rows[0].FirstPosition); Equal(1, result.Rows[0].SecondPosition);
        Equal(0, result.Rows[1].FirstPosition); Equal(.625, result.Rows[1].SecondPosition);
        Assert.True(result.IsMirrored); Assert.Equal(40, result.Rows[0].Second);
    }

    [Fact]
    public void Paired_sort_uses_delta_or_mirrored_total_span_without_overflow()
    {
        var regular = ChartAdvancedComparison.Paired([new(double.MaxValue, -double.MaxValue), new(-double.MaxValue, double.MaxValue), new(0, 0)],
            false, ComparisonSort.ValueDescending, Token);
        Assert.Equal([1, 2, 0], regular.Rows.Select(row => row.SourceIndex));
        var mirrored = ChartAdvancedComparison.Paired([new(1, 5), new(4, 2), new(4, 5)], true, ComparisonSort.ValueDescending, Token);
        Assert.Equal([2, 0, 1], mirrored.Rows.Select(row => row.SourceIndex));
    }

    [Fact]
    public void Mekko_widths_heights_and_areas_follow_independent_normalization()
    {
        var result = ChartAdvancedComparison.Mekko([
            new(2, [new(1, "a"), new(3, "b")], "first"),
            new(6, [new(3), new(1)], "second")], Token);
        Assert.Equal(4, result.Cells.Count);
        var a = result.Cells[0]; var b = result.Cells[1]; var c = result.Cells[2];
        Equal(0, a.X); Equal(.25, a.Width); Equal(.75, a.Y); Equal(.25, a.Height);
        Equal(0, b.Y); Equal(.75, b.Height); Equal(.25, c.X); Equal(.75, c.Width);
        Equal(1d / 16, a.Width * a.Height); Equal(9d / 16, c.Width * c.Height);
        Assert.Equal(1, c.ColumnIndex); Assert.Equal(0, c.SegmentIndex); Assert.Equal("a", a.Label);
    }

    [Fact]
    public void Mekko_zero_and_extreme_weights_are_finite_and_source_preserving()
    {
        var result = ChartAdvancedComparison.Mekko([
            new(double.MaxValue, [new(double.MaxValue), new(double.MaxValue)]),
            new(double.MaxValue, [new(0)]), new(0, [new(1)])], Token);
        Equal(.5, result.Cells[0].Width); Equal(.5, result.Cells[0].Height);
        Equal(0, result.Cells[2].Height); Equal(0, result.Cells[3].Width);
        Assert.Equal(2, result.Cells[3].ColumnIndex);
        Assert.All(result.Cells, cell => Assert.True(double.IsFinite(cell.X + cell.Y + cell.Width + cell.Height)));
        var zeros = ChartAdvancedComparison.Mekko([new(0, [new(0)])], Token);
        Equal(0, zeros.Cells[0].Width); Equal(0, zeros.Cells[0].Height);
    }

    [Fact]
    public void Layout_collections_are_owned_snapshots()
    {
        var segments = new List<MekkoSegment> { new(2) }; var column = new MekkoColumn(1, segments);
        segments[0] = new(999); Assert.Equal(2, column.Segments[0].Value);
        Assert.Throws<NotSupportedException>(() => ((IList<MekkoSegment>)column.Segments)[0] = new(1));
        var source = new[] { new DivergingValue(1) };
        var layout = ChartAdvancedComparison.Diverging(source, cancellationToken: Token);
        source[0] = new(5); Assert.Equal(1, layout.Rows[0].Value);
        Assert.Throws<NotSupportedException>(() => ((IList<DivergingRow>)layout.Rows)[0] = layout.Rows[0]);
    }

    [Fact]
    public void Invalid_values_and_sort_modes_fail_explicitly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartAdvancedComparison.Diverging([new(double.NaN)], cancellationToken: Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartAdvancedComparison.Diverging([], (ComparisonSort)99, Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartAdvancedComparison.Paired([new(-1, 2)], true, cancellationToken: Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartAdvancedComparison.Paired([new(1, double.PositiveInfinity)], cancellationToken: Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartAdvancedComparison.Mekko([new(-1, [new(1)])], Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartAdvancedComparison.Mekko([new(1, [new(-1)])], Token));
    }

    [Fact]
    public void Cancellation_is_observed_before_enumeration_and_during_enumeration()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => ChartAdvancedComparison.Diverging([], cancellationToken: cancellation.Token));
        Assert.Throws<OperationCanceledException>(() => ChartAdvancedComparison.Paired([], cancellationToken: cancellation.Token));
        Assert.Throws<OperationCanceledException>(() => ChartAdvancedComparison.Mekko([], cancellation.Token));
        Assert.Throws<OperationCanceledException>(() => ChartAdvancedComparison.Venn([], 2, cancellation.Token));
        using var during = new CancellationTokenSource();
        IEnumerable<DivergingValue> Source() { yield return new(1); during.Cancel(); yield return new(2); }
        Assert.Throws<OperationCanceledException>(() => ChartAdvancedComparison.Diverging(Source(), cancellationToken: during.Token));
    }

    [Fact]
    public void Venn_inclusive_inputs_produce_exclusive_quantities_and_summary_dependencies()
    {
        var result = ChartAdvancedComparison.Venn([new(3, 20), new(1, 60), new(2, 40)], 2, Token);
        Assert.False(result.IsApproximate);
        Assert.Equal([40d, 20d, 20d], result.Regions.Select(region => Math.Round(region.Value, 10)));
        Assert.Equal([0, 1], result.Regions[0].SourceIndices);
        Assert.Equal([0, 2], result.Regions[1].SourceIndices);
        Assert.Equal([0], result.Regions[2].SourceIndices);
        Assert.Equal(1, result.Circles[0].SourceIndex); Assert.Equal(2, result.Circles[1].SourceIndex);
        Assert.InRange(result.MaximumRelativeAreaError, 0, 1e-12);
    }

    [Fact]
    public void Venn_equal_circle_lens_matches_closed_form_independently()
    {
        var intersection = 2 * Math.PI / 3 - Math.Sqrt(3) / 2;
        var result = ChartAdvancedComparison.Venn([new(1, Math.PI), new(2, Math.PI), new(3, intersection)], 2, Token);
        var first = result.Circles[0]; var second = result.Circles[1];
        Equal(1d / 3, first.Radius); Equal(first.Radius, second.Radius);
        Equal(1d / 3, second.Center.X - first.Center.X); Equal(first.Center.Y, second.Center.Y);
        Equal(intersection, result.Regions[2].EstimatedValue, 1e-11);
    }

    [Theory]
    [InlineData(100, 25, 0)]
    [InlineData(100, 25, 25)]
    [InlineData(100, 100, 100)]
    [InlineData(0, 0, 0)]
    public void Venn_disjoint_contained_coincident_and_zero_sets_remain_exact(double a, double b, double overlap)
    {
        var result = ChartAdvancedComparison.Venn([new(1, a), new(2, b), new(3, overlap)], 2, Token);
        Assert.All(result.Regions, region => Equal(region.Value, region.EstimatedValue, 1e-10));
        Assert.All(result.Circles, circle => {
            Assert.InRange(circle.Center.X - circle.Radius, -1e-12, 1 + 1e-12);
            Assert.InRange(circle.Center.Y + circle.Radius, -1e-12, 1 + 1e-12);
        });
    }

    [Fact]
    public void Venn_three_set_mobius_values_and_approximation_are_explicit()
    {
        var result = ChartAdvancedComparison.Venn([new(1, 100), new(2, 100), new(4, 100),
            new(3, 30), new(5, 30), new(6, 30), new(7, 10)], 3, Token);
        Assert.True(result.IsApproximate);
        Assert.Equal([50d, 50d, 20d, 50d, 20d, 20d, 10d], result.Regions.Select(region => Math.Round(region.Value, 10)));
        var maxError = result.Regions.Max(region => Math.Abs(region.Value - region.EstimatedValue)) / 100;
        Equal(maxError, result.MaximumRelativeAreaError);
        Assert.InRange(result.TripleIntersectionErrorEstimate, 0, .01);
        Assert.True(result.MaximumRelativeAreaError > .0001);
        // An independent vertical Riemann integral over normalized circles checks represented triple area.
        var tripleArea = ReferenceTripleArea(result.Circles, 32_768);
        var areaScale = Math.PI * result.Circles[0].Radius * result.Circles[0].Radius / 100;
        Equal(tripleArea / areaScale, result.Regions[6].EstimatedValue, .002);
    }

    [Fact]
    public void Venn_impossible_circle_triangle_is_finite_and_discloses_error()
    {
        // A=B as sets, but C intersects A and not B: valid exclusive quantities require A/B noncoincident
        // despite their large overlap; the circle-distance triangle cannot honor all pair intersections.
        var result = ChartAdvancedComparison.Venn([new(1, 100), new(2, 100), new(4, 20),
            new(3, 80), new(5, 20), new(6, 0), new(7, 0)], 3, Token);
        Assert.True(result.IsApproximate); Assert.True(result.MaximumRelativeAreaError > .01);
        Assert.All(result.Circles, circle => Assert.True(double.IsFinite(circle.Center.X + circle.Center.Y + circle.Radius)));
    }

    [Fact]
    public void Venn_extreme_physical_totals_are_scaled_without_overflow()
    {
        var result = ChartAdvancedComparison.Venn([new(1, double.MaxValue), new(2, double.MaxValue), new(3, double.MaxValue / 2)], 2, Token);
        Assert.All(result.Regions, region => Assert.True(double.IsFinite(region.Value) && double.IsFinite(region.EstimatedValue)));
        Assert.InRange(result.MaximumRelativeAreaError, 0, 1e-12);
        var imbalanced = ChartAdvancedComparison.Venn([new(1, 1e300), new(2, 1e-10), new(3, 1e-10)], 2, Token);
        Assert.Equal(1e-10, imbalanced.Regions[2].Value);
        Assert.Equal(0, imbalanced.Regions[1].Value);
        Assert.Throws<OverflowException>(() => ChartAdvancedComparison.Venn([new(1, double.MaxValue), new(2, double.Epsilon)], 2, Token));
    }

    [Fact]
    public void Venn_invalid_or_inconsistent_intersections_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartAdvancedComparison.Venn([], 1, Token));
        Assert.Throws<ArgumentException>(() => ChartAdvancedComparison.Venn([new(1, 10)], 2, Token));
        Assert.Throws<ArgumentException>(() => ChartAdvancedComparison.Venn([new(1, 10), new(1, 10), new(2, 10)], 2, Token));
        Assert.Throws<ArgumentException>(() => ChartAdvancedComparison.Venn([new(1, 10), new(2, 10), new(3, 11)], 2, Token));
        Assert.Throws<ArgumentException>(() => ChartAdvancedComparison.Venn([new(1, 1e300), new(2, 1e-10), new(3, 2e-10)], 2, Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartAdvancedComparison.Venn([new(1, -1), new(2, 10)], 2, Token));
        Assert.Throws<ArgumentException>(() => ChartAdvancedComparison.Venn([new(1, 10), new(2, 10), new(4, 10),
            new(3, 8), new(5, 8), new(6, 0)], 3, Token));
    }

    private static double ReferenceTripleArea(IReadOnlyList<VennCircle> circles, int slices)
    {
        var left = circles.Max(circle => circle.Center.X - circle.Radius);
        var right = circles.Min(circle => circle.Center.X + circle.Radius);
        var step = (right - left) / slices; double area = 0;
        for (var i = 0; i < slices; i++)
        {
            var x = left + (i + .5) * step;
            var intervals = circles.Select(circle => {
                var dx = x - circle.Center.X;
                var h = Math.Sqrt(Math.Max(0, circle.Radius * circle.Radius - dx * dx));
                return (lower: circle.Center.Y - h, upper: circle.Center.Y + h);
            }).ToArray();
            area += Math.Max(0, intervals.Min(interval => interval.upper) - intervals.Max(interval => interval.lower)) * step;
        }
        return area;
    }

    private static void Equal(double expected, double actual, double tolerance = 1e-12) => Assert.InRange(actual, expected - tolerance, expected + tolerance);
}
