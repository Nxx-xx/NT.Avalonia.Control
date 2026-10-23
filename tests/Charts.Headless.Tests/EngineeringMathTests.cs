using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class EngineeringMathTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(50, 0, 0, 0)]
    [InlineData(0, 0, -1, 0)]
    [InlineData(100, 0, 1d / 3, 0)]
    [InlineData(50, 50, .2, .4)]
    [InlineData(50, -50, .2, -.4)]
    public void Smith_impedance_uses_physical_ohms_and_complex_reflection(double r, double x, double real, double imaginary)
    {
        var projected = ChartEngineering.ProjectSmith(new(r, x), 50);
        Equal(real, projected.X); Equal(imaginary, projected.Y);
        var physical = ChartEngineering.UnprojectSmith(projected, 50);
        Equal(r, physical.R); Equal(x, physical.X);
    }

    [Fact]
    public void Smith_admittance_uses_physical_siemens_and_the_admittance_sign()
    {
        var projected = ChartEngineering.ProjectSmith(new(.02, .02), 50, SmithQuantity.Admittance);
        Equal(-.2, projected.X); Equal(-.4, projected.Y);
        var physical = ChartEngineering.UnprojectSmith(projected, 50, SmithQuantity.Admittance);
        Equal(.02, physical.R); Equal(.02, physical.X);
        Assert.Equal(new ChartPoint(1, 0), ChartEngineering.ProjectSmith(new(0, 0), 50, SmithQuantity.Admittance));
        Assert.Equal(new SmithPoint(0, 0), ChartEngineering.UnprojectSmith(new(1, 0), 50, SmithQuantity.Admittance));
    }

    [Fact]
    public void Smith_scaled_arithmetic_handles_extreme_finite_values()
    {
        var projected = ChartEngineering.ProjectSmith(new(1e308, 1e308), 1e308);
        Equal(.2, projected.X); Equal(.4, projected.Y);
        var physical = ChartEngineering.UnprojectSmith(projected, 1e308);
        Assert.InRange(physical.R / 1e308, .999999999999, 1.000000000001);
        Assert.InRange(physical.X / 1e308, .999999999999, 1.000000000001);
        Assert.Equal(new ChartPoint(0, 0), ChartEngineering.ProjectSmith(new(double.Epsilon, 0), double.Epsilon));
        var verySmallReference = ChartEngineering.ProjectSmith(new(1e308, 0), double.Epsilon, SmithQuantity.Admittance);
        Assert.True(double.IsFinite(verySmallReference.X));
        Assert.InRange(verySmallReference.X, .999999999999, 1);
    }

    [Fact]
    public void Smith_rejects_active_nonfinite_singular_and_unrepresentable_values()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartEngineering.ProjectSmith(new(-1, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartEngineering.ProjectSmith(new(1, double.NaN)));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartEngineering.ProjectSmith(new(1, 0), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartEngineering.ProjectSmith(new(1, 0), quantity: (SmithQuantity)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartEngineering.UnprojectSmith(new(1, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartEngineering.UnprojectSmith(new(-1, 0), quantity: SmithQuantity.Admittance));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartEngineering.UnprojectSmith(new(1.01, 0)));
        Assert.Throws<OverflowException>(() => ChartEngineering.UnprojectSmith(new(.9, 0), double.MaxValue));
    }

    [Fact]
    public void Smith_grid_curves_are_finite_passive_immutable_and_bounded()
    {
        var impedance = ChartEngineering.SmithGrid(sampleCount: 37, cancellationToken: Token);
        var admittance = ChartEngineering.SmithGrid(SmithQuantity.Admittance, 37, Token);
        Assert.Equal(17, impedance.Count);
        Assert.All(impedance, path => Assert.Equal(37, path.Points.Count));
        for (var curve = 1; curve < impedance.Count; curve++)
        for (var i = 0; i < 37; i++)
        {
            var point = impedance[curve].Points[i];
            Assert.True(double.IsFinite(point.X) && double.IsFinite(point.Y));
            Assert.InRange(point.X * point.X + point.Y * point.Y, 0, 1 + 1e-12);
            Equal(-point.X, admittance[curve].Points[i].X);
            Equal(-point.Y, admittance[curve].Points[i].Y);
        }
        Assert.Throws<NotSupportedException>(() => ((IList<ChartPoint>)impedance[0].Points)[0] = default);
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartEngineering.SmithGrid(sampleCount: ChartEngineering.MaximumCount, cancellationToken: Token));
    }

    [Theory]
    [InlineData(SmithQuantity.Impedance)]
    [InlineData(SmithQuantity.Admittance)]
    public void Smith_grid_samples_satisfy_independent_constant_component_circle_equations(SmithQuantity quantity)
    {
        var paths = ChartEngineering.SmithGrid(quantity, 73, Token);
        foreach (var path in paths)
        foreach (var displayed in path.Points)
        {
            var point = quantity == SmithQuantity.Impedance ? displayed : new ChartPoint(-displayed.X, -displayed.Y);
            if (path.Label == "unit circle") Equal(1, point.X * point.X + point.Y * point.Y);
            else if (path.Label.StartsWith("real=", StringComparison.Ordinal))
            {
                var real = double.Parse(path.Label[5..], System.Globalization.CultureInfo.InvariantCulture);
                var center = real / (1 + real); var radius = 1 / (1 + real);
                Equal(radius * radius, (point.X - center) * (point.X - center) + point.Y * point.Y);
            }
            else
            {
                var imaginary = double.Parse(path.Label[10..], System.Globalization.CultureInfo.InvariantCulture);
                if (imaginary == 0) { Equal(0, point.Y); continue; }
                var centerY = 1 / imaginary;
                Equal(centerY * centerY, (point.X - 1) * (point.X - 1) + (point.Y - centerY) * (point.Y - centerY));
            }
        }
    }

    [Fact]
    public void Ternary_vertices_and_interior_use_the_documented_orientation()
    {
        Assert.Equal(new ChartPoint(.5, Math.Sqrt(3) / 2), ChartEngineering.ProjectTernary(new(1, 0, 0)));
        Assert.Equal(new ChartPoint(0, 0), ChartEngineering.ProjectTernary(new(0, 1, 0)));
        Assert.Equal(new ChartPoint(1, 0), ChartEngineering.ProjectTernary(new(0, 0, 1)));
        var projected = ChartEngineering.ProjectTernary(new(1, 1, 2));
        Equal(.625, projected.X); Equal(Math.Sqrt(3) / 8, projected.Y);
        var inverse = ChartEngineering.UnprojectTernary(projected);
        Equal(.25, inverse.A); Equal(.25, inverse.B); Equal(.5, inverse.C);
    }

    [Fact]
    public void Ternary_normalization_avoids_large_sum_overflow_and_small_value_underflow()
    {
        var large = ChartEngineering.NormalizeTernary(new(1e308, 1e308, 1e308));
        Equal(1d / 3, large.A); Equal(1d / 3, large.B); Equal(1d / 3, large.C);
        var small = ChartEngineering.NormalizeTernary(new(double.Epsilon, double.Epsilon, double.Epsilon));
        Assert.Equal(large, small);
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartEngineering.NormalizeTernary(new(0, 0, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartEngineering.NormalizeTernary(new(-1, 1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartEngineering.UnprojectTernary(new(-.1, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartEngineering.UnprojectTernary(new(.5, 1)));
    }

    [Fact]
    public void Ternary_roundtrips_deterministic_compositions_without_changing_the_total_contract()
    {
        for (var i = 1; i <= 200; i++)
        {
            var input = new TernaryPoint(i, (i * 37) % 97, (i * 71) % 113);
            var expected = ChartEngineering.NormalizeTernary(input);
            var actual = ChartEngineering.UnprojectTernary(ChartEngineering.ProjectTernary(input));
            Equal(expected.A, actual.A); Equal(expected.B, actual.B); Equal(expected.C, actual.C);
            Equal(1, actual.A + actual.B + actual.C);
        }
    }

    [Theory]
    [InlineData(HexbinAggregation.Count, 2)]
    [InlineData(HexbinAggregation.Sum, 6)]
    [InlineData(HexbinAggregation.Mean, 3)]
    public void Hexbin_retains_members_while_aggregating_values(HexbinAggregation aggregation, double expected)
    {
        HexbinPoint[] source = [new(10, -10, 2), new(double.NaN, 0, 9), new(10.1, -10.1, 4), new(10 + 2 * Math.Sqrt(3), -10, 8)];
        var result = ChartEngineering.Hexbin(source, 2, new(10, -10), aggregation, Token);
        Assert.Equal(3, result.Count); Assert.Equal(1, result.OmittedCount); Assert.Equal(2, result.Bins.Count);
        var center = result.Bins.Single(bin => bin.Q == 0 && bin.R == 0);
        Assert.Equal(new ChartPoint(10, -10), center.Center);
        Assert.Equal(new[] { 0, 2 }, center.SourceIndices); Assert.Equal(expected, center.Value);
        Assert.Equal(6, center.Vertices.Count);
        Assert.All(center.Vertices, vertex => Equal(4, Math.Pow(vertex.X - center.Center.X, 2) + Math.Pow(vertex.Y - center.Center.Y, 2)));
        source[0] = new(99, 99, 99);
        Assert.Equal(new[] { 0, 2 }, center.SourceIndices);
        Assert.Throws<NotSupportedException>(() => ((IList<int>)center.SourceIndices)[0] = 7);
        Assert.Throws<NotSupportedException>(() => ((IList<ChartPoint>)center.Vertices)[0] = default);
    }

    [Fact]
    public void Hexbin_axial_neighbors_have_regular_centers_and_deterministic_order()
    {
        HexbinPoint[] source = [new(Math.Sqrt(3), 0), new(0, 0), new(-Math.Sqrt(3), 0), new(Math.Sqrt(3) / 2, 1.5)];
        var result = ChartEngineering.Hexbin(source, 1, cancellationToken: Token);
        Assert.Equal(new (long, long)[] { (-1, 0), (0, 0), (0, 1), (1, 0) }, result.Bins.Select(bin => (bin.Q, bin.R)));
        Assert.Equal(new[] { 2, 1, 3, 0 }, result.Bins.SelectMany(bin => bin.SourceIndices));
        foreach (var bin in result.Bins)
        {
            Equal(Math.Sqrt(3) * (bin.Q + bin.R / 2d), bin.Center.X);
            Equal(1.5 * bin.R, bin.Center.Y);
        }
    }

    [Theory]
    [InlineData(74, -13, 25)]
    [InlineData(45.3, -7, 15)]
    [InlineData(33.2, -5, 11)]
    [InlineData(21.1, -3, 7)]
    [InlineData(15.7, -2, 5)]
    [InlineData(9.9, -1, 3)]
    public void Hexbin_vertical_symmetry_preserves_the_Q_first_largest_error_tie(double y, long q, long r)
    {
        // At X=0, the fractional Q and cube-Y coordinates are equal. Their rounding errors
        // must tie; correcting Q keeps the rounded R and cube-Y rather than choosing cube-Y.
        var result = ChartEngineering.Hexbin([new(0, y, 7)], 2, cancellationToken: Token);
        var bin = Assert.Single(result.Bins);
        Assert.Equal(q, bin.Q); Assert.Equal(r, bin.R);
        Assert.Equal(new[] { 0 }, bin.SourceIndices);
    }

    [Fact]
    public void Hexbin_tie_membership_is_stable_for_adjacent_ordinates_and_resolves_the_two_sides()
    {
        HexbinPoint[] source = [new(0, Math.BitDecrement(45.3), 2), new(0, 45.3, 4), new(0, Math.BitIncrement(45.3), 6),
            new(-1e-12, 45.3, 8), new(1e-12, 45.3, 10)];
        var result = ChartEngineering.Hexbin(source, 2, aggregation: HexbinAggregation.Mean, cancellationToken: Token);
        Assert.Equal(2, result.Bins.Count);
        var left = result.Bins.Single(bin => bin.Q == -8 && bin.R == 15);
        var right = result.Bins.Single(bin => bin.Q == -7 && bin.R == 15);
        Assert.Equal(new[] { 3 }, left.SourceIndices); Assert.Equal(8, left.Value);
        Assert.Equal(new[] { 0, 1, 2, 4 }, right.SourceIndices); Assert.Equal(5.5, right.Value);
        Assert.Equal(5, result.Count); Assert.Equal(0, result.OmittedCount);
    }

    [Fact]
    public void Hexbin_omits_nonfinite_observations_and_rejects_unrepresentable_lattices_and_sums()
    {
        var result = ChartEngineering.Hexbin([new(0, 0), new(0, 0, double.NaN), new(double.PositiveInfinity, 0)], 1, cancellationToken: Token);
        Assert.Equal(1, result.Count); Assert.Equal(2, result.OmittedCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartEngineering.Hexbin([], 0, cancellationToken: Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartEngineering.Hexbin([new(double.MaxValue, 0)], double.Epsilon, cancellationToken: Token));
        Assert.Throws<OverflowException>(() => ChartEngineering.Hexbin([new(0, 0, double.MaxValue), new(0, 0, double.MaxValue)], 1,
            aggregation: HexbinAggregation.Sum, cancellationToken: Token));
    }

    [Fact]
    public void Hexbin_mean_remains_finite_when_the_sum_or_opposite_sign_delta_would_overflow()
    {
        var repeated = ChartEngineering.Hexbin(Enumerable.Repeat(new HexbinPoint(0, 0, 1e308), 20), 1,
            aggregation: HexbinAggregation.Mean, cancellationToken: Token);
        Assert.Equal(1e308, Assert.Single(repeated.Bins).Value);
        var balanced = ChartEngineering.Hexbin([new(0, 0, 1e308), new(0, 0, -1e308)], 1,
            aggregation: HexbinAggregation.Mean, cancellationToken: Token);
        Assert.Equal(0, Assert.Single(balanced.Bins).Value);
        var mixed = ChartEngineering.Hexbin([new(0, 0, 1e308), new(0, 0, 1e308), new(0, 0, -1e308)], 1,
            aggregation: HexbinAggregation.Mean, cancellationToken: Token);
        var mean = Assert.Single(mixed.Bins).Value;
        Assert.True(double.IsFinite(mean));
        Assert.InRange(mean / 1e308, 1d / 3 - 1e-14, 1d / 3 + 1e-14);
        Assert.Equal(new[] { 0, 1, 2 }, mixed.Bins[0].SourceIndices);
        Assert.Throws<OverflowException>(() => ChartEngineering.Hexbin([new(0, 0, 1e308), new(0, 0, 1e308)], 1,
            aggregation: HexbinAggregation.Sum, cancellationToken: Token));
    }

    [Fact]
    public void Hexbin_online_mean_preserves_small_values_and_ordinary_mixed_value_precision()
    {
        var small = ChartEngineering.Hexbin([new(0, 0, 1e-300), new(0, 0, 2e-300), new(0, 0, 3e-300)], 1,
            aggregation: HexbinAggregation.Mean, cancellationToken: Token);
        Assert.InRange(Assert.Single(small.Bins).Value / 1e-300, 2 - 1e-14, 2 + 1e-14);
        var subnormal = ChartEngineering.Hexbin(Enumerable.Repeat(new HexbinPoint(0, 0, double.Epsilon), 10), 1,
            aggregation: HexbinAggregation.Mean, cancellationToken: Token);
        Assert.Equal(double.Epsilon, Assert.Single(subnormal.Bins).Value);
        var ordinary = ChartEngineering.Hexbin([new(0, 0, -7), new(0, 0, 2), new(0, 0, 11), new(0, 0, -3), new(0, 0, 8)], 1,
            aggregation: HexbinAggregation.Mean, cancellationToken: Token);
        Equal(2.2, Assert.Single(ordinary.Bins).Value);
        var cancelledExtremes = ChartEngineering.Hexbin([new(0, 0, -1e308), new(0, 0, 1e308), new(0, 0, 3)], 1,
            aggregation: HexbinAggregation.Mean, cancellationToken: Token);
        Assert.Equal(1, Assert.Single(cancelledExtremes.Bins).Value);
    }

    [Fact]
    public void Wind_uses_north_centered_half_open_directions_and_speed_boundaries()
    {
        WindObservation[] source = [new(0, 1, 2), new(360, 3, 3), new(348.75, 6, 4), new(11.25, 3, 5), new(Math.BitDecrement(11.25), 1, 6), new(-90, 1, 7)];
        var result = ChartEngineering.WindRose(source, [3, 6], cancellationToken: Token);
        Assert.Equal(6, result.Count); Assert.Equal(0, result.OmittedCount); Assert.Equal(27, result.TotalFrequency);
        Assert.Equal(new[] { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW" }, result.Sectors.Select(sector => sector.Label));
        Assert.Equal(new[] { 0, 4 }, result.Sectors[0].Bins[0].SourceIndices);
        Assert.Equal(new[] { 1 }, result.Sectors[0].Bins[1].SourceIndices);
        Assert.Equal(new[] { 2 }, result.Sectors[0].Bins[2].SourceIndices);
        Assert.Equal(new[] { 3 }, result.Sectors[1].Bins[1].SourceIndices);
        Assert.Equal(new[] { 5 }, result.Sectors[12].Bins[0].SourceIndices);
        Assert.Equal(8, result.Sectors[0].Bins[0].EndFrequency);
        Assert.Equal(8, result.Sectors[0].Bins[1].StartFrequency);
        Assert.Equal(15, result.Sectors[0].Frequency);
        Equal(100, result.Bins.Sum(bin => bin.Percentage));
    }

    [Fact]
    public void Wind_radians_and_compass_parsing_share_degrees_clockwise_from_north()
    {
        var result = ChartEngineering.WindRose([new(Math.PI / 2, 1), new(-Math.PI / 2, 1), new(2 * Math.PI, 1)], [], 4, ChartAngleUnit.Radians, Token);
        Assert.Equal(new[] { "N", "E", "S", "W" }, result.Sectors.Select(sector => sector.Label));
        Assert.Equal(new[] { 2 }, result.Sectors[0].Bins[0].SourceIndices);
        Assert.Equal(new[] { 0 }, result.Sectors[1].Bins[0].SourceIndices);
        Assert.Equal(new[] { 1 }, result.Sectors[3].Bins[0].SourceIndices);
        Assert.Equal(22.5, ChartEngineering.ParseWindDirection("north-northeast"));
        Assert.Equal(247.5, ChartEngineering.ParseWindDirection(" WSW "));
        Equal(90, ChartEngineering.ParseWindDirection("1.5707963267948966rad"));
        Assert.Equal(270, ChartEngineering.ParseWindDirection("-90°"));
        Assert.Equal(90, ChartEngineering.ParseWindDirection("450"));
        Equal(180, ChartEngineering.ParseWindDirection(Math.PI.ToString("R", System.Globalization.CultureInfo.InvariantCulture), ChartAngleUnit.Radians));
        Assert.Throws<FormatException>(() => ChartEngineering.ParseWindDirection("NaN"));
        Assert.Throws<FormatException>(() => ChartEngineering.ParseWindDirection("north-by-unknown"));
    }

    [Fact]
    public void Wind_invalid_rows_are_omitted_and_empty_bands_remain_in_fixed_order()
    {
        WindObservation[] source = [new(0, 1, 2), new(double.NaN, 1), new(45, -1), new(90, 1, 0), new(180, 1, double.PositiveInfinity)];
        double[] bounds = [3, 6];
        var result = ChartEngineering.WindRose(source, bounds, 4, cancellationToken: Token);
        Assert.Equal(1, result.Count); Assert.Equal(4, result.OmittedCount); Assert.Equal(12, result.Bins.Count);
        Assert.Equal(100, result.Bins[0].Percentage); Assert.Equal(0, result.Sectors[2].Frequency);
        bounds[0] = 2; source[0] = new(180, 10);
        Assert.Equal(3, result.SpeedUpperBounds[0]); Assert.Equal(new[] { 0 }, result.Bins[0].SourceIndices);
        Assert.Throws<NotSupportedException>(() => ((IList<double>)result.SpeedUpperBounds)[0] = 9);
        Assert.Throws<ArgumentException>(() => ChartEngineering.WindRose([], [6, 3], cancellationToken: Token));
        Assert.Throws<ArgumentException>(() => ChartEngineering.WindRose([], [0], cancellationToken: Token));
        Assert.Throws<OverflowException>(() => ChartEngineering.WindRose([new(0, 1, double.MaxValue), new(0, 1, double.MaxValue)], [], cancellationToken: Token));
    }

    [Fact]
    public void Carpet_mesh_interpolates_bilinearly_and_preserves_parameter_nodes()
    {
        var source = CarpetSource();
        var grid = ChartEngineering.CarpetGrid(source.Reverse(), cancellationToken: Token);
        Assert.Equal(new[] { 0d, 1d, 2d }, grid.ALevels); Assert.Equal(new[] { 0d, 1d }, grid.BLevels);
        Assert.Equal(5, grid.Paths.Count); Assert.True(grid.IsInvertible);
        Assert.Equal(source, grid.Points);
        Assert.Equal("B=0", grid.Paths[0].Label); Assert.Equal("A=0", grid.Paths[2].Label);
        var interpolated = ChartEngineering.InterpolateCarpet(grid, .5, .25);
        Assert.Equal(new CarpetPoint(.5, .25, 1.875), interpolated);
        Assert.Equal(new ChartPoint(.625, 1.875), ChartEngineering.ProjectCarpet(interpolated));
        source[0] = new(9, 9, 9);
        Assert.Equal(new CarpetPoint(0, 0, 0), grid.Points[0]);
        Assert.Throws<NotSupportedException>(() => ((IList<CarpetPoint>)grid.Points)[0] = default);
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartEngineering.InterpolateCarpet(grid, -1, 0));
    }

    [Theory]
    [InlineData(.5)]
    [InlineData(-3)]
    [InlineData(0)]
    public void Carpet_inverse_roundtrips_nodes_edges_and_interiors(double offset)
    {
        // Y=B supplies a globally injective map for either offset sign, and exercises both elimination branches.
        var grid = ChartEngineering.CarpetGrid(from b in new[] { 0d, 1d, 2d } from a in new[] { 0d, 1d, 2d }
            select new CarpetPoint(a, b, b + .05 * a * b), offset, Token);
        Assert.True(grid.IsInvertible);
        for (var ai = 0; ai <= 8; ai++)
        for (var bi = 0; bi <= 8; bi++)
        {
            var expected = ChartEngineering.InterpolateCarpet(grid, ai / 4d, bi / 4d);
            var actual = ChartEngineering.UnprojectCarpet(grid, ChartEngineering.ProjectCarpet(expected, offset), Token);
            Equal(expected.A, actual.A); Equal(expected.B, actual.B); Equal(expected.Y, actual.Y);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartEngineering.UnprojectCarpet(grid, new(100, 100), Token));
    }

    [Fact]
    public void Carpet_inverse_rejects_folds_singular_meshes_and_incomplete_parameter_grids()
    {
        var folded = ChartEngineering.CarpetGrid([new(0, 0, 0), new(1, 0, 0), new(0, 1, -.5), new(1, 1, .5)], 0, Token);
        Assert.False(folded.IsInvertible);
        Assert.Throws<InvalidOperationException>(() => ChartEngineering.UnprojectCarpet(folded, new(.5, 0), Token));
        var singular = ChartEngineering.CarpetGrid([new(0, 0, 0), new(1, 0, 1), new(0, 1, .5), new(1, 1, 1.5)], cancellationToken: Token);
        Assert.False(singular.IsInvertible);
        Assert.Throws<InvalidOperationException>(() => ChartEngineering.UnprojectCarpet(singular, new(.5, .5), Token));
        Assert.Throws<ArgumentException>(() => ChartEngineering.CarpetGrid([new(0, 0, 0), new(1, 0, 1), new(0, 1, 2)], cancellationToken: Token));
        Assert.Throws<ArgumentException>(() => ChartEngineering.CarpetGrid([..CarpetSource(), new(0, 0, 0)], cancellationToken: Token));
    }

    [Fact]
    public void Engineering_operations_honor_cancellation_before_enumerating_or_generating()
    {
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(Token);
        cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => ChartEngineering.Hexbin(Exploding<HexbinPoint>(), 1, cancellationToken: cancelled.Token));
        Assert.Throws<OperationCanceledException>(() => ChartEngineering.WindRose(Exploding<WindObservation>(), [], cancellationToken: cancelled.Token));
        Assert.Throws<OperationCanceledException>(() => ChartEngineering.CarpetGrid(Exploding<CarpetPoint>(), cancellationToken: cancelled.Token));
        Assert.Throws<OperationCanceledException>(() => ChartEngineering.SmithGrid(cancellationToken: cancelled.Token));
        var grid = ChartEngineering.CarpetGrid(CarpetSource(), cancellationToken: Token);
        Assert.Throws<OperationCanceledException>(() => ChartEngineering.UnprojectCarpet(grid, new(0, 0), cancelled.Token));
    }

    [Fact]
    public void Engineering_observation_and_geometry_budgets_fail_before_unbounded_growth()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartEngineering.Hexbin(
            Enumerable.Repeat(new HexbinPoint(0, 0), ChartEngineering.MaximumCount + 1), 1, cancellationToken: Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartEngineering.WindRose([], Enumerable.Range(1, 3000).Select(i => (double)i),
            360, cancellationToken: Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartEngineering.Hexbin(Enumerable.Range(0, ChartEngineering.MaximumCount / 6 + 1)
            .Select(i => new HexbinPoint(i * 2, 0)), 1, cancellationToken: Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChartEngineering.CarpetGrid(Enumerable.Range(0, ChartEngineering.MaximumCount / 2 + 1)
            .Select(i => new CarpetPoint(i, 0, i)), cancellationToken: Token));
    }

    private static CarpetPoint[] CarpetSource() =>
        (from b in new[] { 0d, 1d } from a in new[] { 0d, 1d, 2d } select new CarpetPoint(a, b, 2 * a + 3 * b + a * b)).ToArray();

    private static IEnumerable<T> Exploding<T>() => Enumerable.Repeat(0, 1).Select<int, T>(
        _ => throw new InvalidOperationException("A cancelled operation must not enumerate its input."));

    private static void Equal(double expected, double actual) => Assert.InRange(actual, expected - 1e-10, expected + 1e-10);
}
