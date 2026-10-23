using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class OperationalMathTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    [Fact]
    public void Bullet_preserves_raw_outliers_and_clips_only_display_positions()
    {
        var result = Assert.Single(ChartOperational.Bullets([new("sales", 120, -5, 0, 100, new[] { 30d, 60, 100 })], cancellationToken: Token)); Assert.Equal(120, result.Source.Value); Assert.Equal(1, result.Position); Assert.Equal(0, result.TargetPosition); Assert.Equal(new[] { .3, .6, 1 }, result.Boundaries);
        Assert.Throws<ArgumentException>(() => ChartOperational.Bullets([new("x", 1, 2, 0, 5, new[] { 3d, 2 })], cancellationToken: Token)); Assert.Throws<ArgumentException>(() => ChartOperational.Bullets([new("x", 1, 2, 4, 4)], cancellationToken: Token));
    }
    [Fact]
    public void Kpi_owns_trend_and_distinguishes_undefined_zero_base_percentage()
    {
        var trend = new[] { 1d, 3, 2 }; var source = new KpiPoint("x", 15, 10, trend); var result = Assert.Single(ChartOperational.Kpis([source], cancellationToken: Token)); Assert.Equal(5, result.Change); Assert.Equal(.5, result.RelativeChange); trend[0] = 99; Assert.Equal(1, result.Trend[0]); Assert.Null(Assert.Single(ChartOperational.Kpis([new("zero", 5, 0)], cancellationToken: Token)).RelativeChange); Assert.Equal(.5, Assert.Single(ChartOperational.Kpis([new("negative", -5, -10)], cancellationToken: Token)).RelativeChange);
    }
    [Fact]
    public void Calendar_has_leap_day_missing_cells_and_explicit_week_start()
    {
        var leap = new DateOnly(2024, 2, 29); var source = new CalendarPoint("leap", leap, 0); var result = ChartOperational.Calendar([source], DayOfWeek.Monday, new(2024, 2, 26), new(2024, 3, 3), cancellationToken: Token); Assert.Equal(1, result.Weeks); Assert.Equal(7, result.Cells.Count); var actual = Assert.Single(result.Cells, x => x.Source is not null); Assert.Same(source, actual.Source); Assert.Equal(3, actual.Day); Assert.Equal(0, actual.SourceIndex); Assert.Equal(6, result.Cells.Count(x => x.SourceIndex is null)); Assert.Equal(0, ChartOperational.Calendar([source], DayOfWeek.Thursday, cancellationToken: Token).Cells[0].Day);
    }
    [Fact]
    public void Calendar_rejects_duplicate_dates_and_unbounded_spans_but_handles_domain_end()
    {
        var date = new DateOnly(2026, 1, 1); Assert.Throws<ArgumentException>(() => ChartOperational.Calendar([new("a", date, 1), new("b", date, 2)], cancellationToken: Token)); Assert.Throws<ArgumentOutOfRangeException>(() => ChartOperational.Calendar([], start: DateOnly.MinValue, end: DateOnly.MaxValue, cancellationToken: Token)); Assert.Equal(DateOnly.MaxValue, ChartOperational.Calendar([new("last", DateOnly.MaxValue, 1)], cancellationToken: Token).Cells[0].Date);
    }
    [Fact]
    public void Matrix_keys_are_typed_and_first_seen_not_stringified()
    {
        MatrixPoint[] points = [new("a", 1, "x", 0), new("b", "1", "x", -3), new("c", 1, "y", 8)]; var result = ChartOperational.Matrix(points, cancellationToken: Token); Assert.Equal(2, result.Rows.Count); Assert.Equal(2, result.Columns.Count); Assert.Equal(0, result.Cells[0].Source.Value); Assert.Equal(1, result.Cells[1].Row); Assert.Same(points[2], result.Cells[2].Source); Assert.Throws<ArgumentException>(() => ChartOperational.Matrix([new("a", 1, 2, 0), new("b", 1, 2, 1)], cancellationToken: Token));
    }
    [Theory]
    [InlineData(100, 34, 33, 33)] [InlineData(2, 1, 1, 0)] [InlineData(1, 1, 0, 0)]
    public void Waffle_largest_remainders_are_stable_and_exact(int units, int a, int b, int c)
    { var result = ChartOperational.Waffle([new("a", 1), new("b", 1), new("c", 1)], units, cancellationToken: Token); Assert.Equal(new[] { a, b, c }, result.Select(x => x.Units)); Assert.Equal(units, result.Sum(x => x.Units)); Assert.All(result, x => Assert.Equal(1d / 3, x.Share)); }
    [Fact]
    public void Waffle_scales_extreme_values_without_overflow_and_zero_allocates_nothing()
    { var result = ChartOperational.Waffle([new("a", double.MaxValue), new("b", double.MaxValue)], 10, cancellationToken: Token); Assert.Equal(new[] { 5, 5 }, result.Select(x => x.Units)); Assert.Equal(0, Assert.Single(ChartOperational.Waffle([new("z", 0)], cancellationToken: Token)).Units); Assert.Throws<ArgumentOutOfRangeException>(() => ChartOperational.Waffle([new("n", -1)], cancellationToken: Token)); }
    [Fact]
    public void Parliament_exact_seats_keep_party_indices_and_nonoverlapping_circles()
    {
        ParliamentParty[] parties = [new("a", 7), new("b", 4), new("z", 0)]; var seats = ChartOperational.Parliament(parties, 3, .3, cancellationToken: Token); Assert.Equal(11, seats.Count); Assert.Equal(7, seats.Count(x => x.SourceIndex == 0)); Assert.Equal(4, seats.Count(x => x.SourceIndex == 1)); Assert.Equal(Enumerable.Range(0, 7), seats.Where(x => x.SourceIndex == 0).Select(x => x.SeatIndex)); foreach (var seat in seats) { Assert.Same(parties[seat.SourceIndex], seat.Source); Assert.InRange(seat.Position.X, 0, 1); Assert.InRange(seat.Position.Y, 0, 1); Assert.True(seat.Radius > 0); }
        for (var i = 0; i < seats.Count; i++) for (var j = i + 1; j < seats.Count; j++) { var a = seats[i]; var b = seats[j]; var dx = a.Position.X - b.Position.X; var dy = (a.Position.Y - b.Position.Y) / 2; Assert.True(Math.Sqrt(dx * dx + dy * dy) + 1e-10 >= a.Radius + b.Radius); }
    }
    [Fact]
    public void Bump_ties_use_competition_ranks_and_period_order_is_explicit_source_order()
    {
        RankingPoint[] source = [new("a1", "a", "later", 9), new("b1", "b", "later", 9), new("c1", "c", "later", 3), new("a0", "a", "earlier", 1), new("b0", "b", "earlier", 5)]; var result = ChartOperational.Ranking(source, cancellationToken: Token); Assert.Equal(new[] { "later", "earlier" }, result.Periods); Assert.Equal(new[] { 1d, 1, 3, 2, 1 }, result.Points.Select(x => x.Rank)); Assert.Equal(0, result.Points[0].Position.X); Assert.Equal(1, result.Points[3].Position.X); Assert.Same(source[0], result.Points[0].Source);
    }
    [Fact]
    public void Direct_ranks_and_two_period_slope_have_distinct_vertical_semantics()
    {
        RankingPoint[] source = [new("a0", "a", 0, 10), new("a1", "a", 1, 20)]; var slope = ChartOperational.Ranking(source, slope: true, cancellationToken: Token); Assert.Equal(1, slope.Points[0].Position.Y); Assert.Equal(0, slope.Points[1].Position.Y); var ranks = ChartOperational.Ranking(source, rankFromValues: false, cancellationToken: Token); Assert.True(ranks.Points[0].Position.Y < ranks.Points[1].Position.Y); Assert.Throws<ArgumentException>(() => ChartOperational.Ranking([new("a", "a", 0, 1)], slope: true, cancellationToken: Token)); Assert.Throws<ArgumentException>(() => ChartOperational.Ranking([new("a", "a", 0, 1), new("b", "a", 0, 2)], cancellationToken: Token));
    }
    [Fact]
    public void Word_shelf_packing_uses_measured_bounds_and_reports_every_omitted_raw_index()
    {
        WordPoint[] source = [new("a", "wide", 4), new("b", "中", 1), new("c", "zero", 0), new("d", "long long long long", 2)]; var result = ChartOperational.Words(source, 100, 60, (s, f) => (s.Length * f / 2, f), 10, 20, cancellationToken: Token); Assert.Contains(2, result.OmittedIndices); Assert.Contains(3, result.OmittedIndices); Assert.Equal(source.Length, result.Words.Count + result.OmittedIndices.Count); Assert.Equal(20, result.Words[0].FontSize); Assert.Equal(15, result.Words[1].FontSize); Assert.Equal(result, ChartOperational.Words(source, 100, 60, (s, f) => (s.Length * f / 2, f), 10, 20, cancellationToken: Token), new WordLayoutComparer());
        foreach (var word in result.Words) { Assert.Same(source[word.SourceIndex], word.Source); Assert.True(word.X >= 0 && word.X + word.Width <= 100); Assert.True(word.Y >= 0 && word.Y + word.Height <= 60); }
    }
    [Theory]
    [InlineData(OperationalRiverBaseline.Centered)] [InlineData(OperationalRiverBaseline.Zero)]
    public void River_aligned_bands_preserve_thickness_and_numeric_total(OperationalRiverBaseline baseline)
    {
        ThemeRiverPoint[] source = [new("a0", "a", 0, 1), new("a1", "a", 1, 3), new("b0", "b", 0, 3), new("b1", "b", 1, 5)]; var river = ChartOperational.River(source, baseline, cancellationToken: Token); Assert.Equal(8, river.MaximumTotal); Assert.Equal(new[] { 0, 1 }, river.Bands[0].SourceIndices); Assert.Equal(baseline == OperationalRiverBaseline.Centered ? 2 : 0, river.Bands[0].Lower[0].Y); for (var stream = 0; stream < river.Bands.Count; stream++) for (var slot = 0; slot < river.Slots.Count; slot++) { var band = river.Bands[stream]; Assert.Equal(source[band.SourceIndices[slot]].Value, band.Upper[slot].Y - band.Lower[slot].Y); if (stream > 0) Assert.Equal(river.Bands[stream - 1].Upper[slot], band.Lower[slot]); }
        Assert.Throws<ArgumentException>(() => ChartOperational.River(source[..^1], cancellationToken: Token));
    }
    [Fact]
    public void Every_family_checks_cancellation_before_enumeration_and_rejects_duplicate_ids()
    {
        using var cancel = new CancellationTokenSource(); cancel.Cancel(); Assert.ThrowsAny<OperationCanceledException>(() => ChartOperational.Waffle(Throwing<WafflePoint>(), cancellationToken: cancel.Token)); Assert.ThrowsAny<OperationCanceledException>(() => ChartOperational.Parliament(Throwing<ParliamentParty>(), cancellationToken: cancel.Token)); Assert.ThrowsAny<OperationCanceledException>(() => ChartOperational.Calendar(Throwing<CalendarPoint>(), cancellationToken: cancel.Token)); Assert.ThrowsAny<OperationCanceledException>(() => ChartOperational.Matrix(Throwing<MatrixPoint>(), cancel.Token)); Assert.ThrowsAny<OperationCanceledException>(() => ChartOperational.Ranking(Throwing<RankingPoint>(), cancellationToken: cancel.Token)); Assert.ThrowsAny<OperationCanceledException>(() => ChartOperational.River(Throwing<ThemeRiverPoint>(), cancellationToken: cancel.Token)); Assert.ThrowsAny<OperationCanceledException>(() => ChartOperational.Bullets(Throwing<BulletPoint>(), cancel.Token)); Assert.ThrowsAny<OperationCanceledException>(() => ChartOperational.Kpis(Throwing<KpiPoint>(), cancel.Token)); Assert.ThrowsAny<OperationCanceledException>(() => ChartOperational.Words(Throwing<WordPoint>(), 10, 10, (_, _) => (1, 1), cancellationToken: cancel.Token)); Assert.Throws<ArgumentException>(() => ChartOperational.Waffle([new("x", 1), new("x", 2)], cancellationToken: Token));
    }
    [Fact]
    public void Nested_numeric_budgets_check_actual_enumeration_even_when_a_caller_lies_about_count()
    {
        var ranges = new UnboundedNumbers(); Assert.Throws<ArgumentOutOfRangeException>(() => ChartOperational.Bullets([new("bullet", 1, 2, 0, double.MaxValue, ranges)], cancellationToken: Token)); Assert.InRange(ranges.Enumerated, 1, 257); var trend = new UnboundedNumbers(); Assert.Throws<ArgumentOutOfRangeException>(() => ChartOperational.Kpis([new("kpi", 1, 0, trend)], cancellationToken: Token)); Assert.InRange(trend.Enumerated, 1, ChartOperational.MaximumCount + 1);
    }
    private static IEnumerable<T> Throwing<T>() => Enumerable.Range(0, 1).Select<int, T>(_ => throw new InvalidOperationException("Must not enumerate."));
    private sealed class WordLayoutComparer : IEqualityComparer<OperationalWords>
    { public bool Equals(OperationalWords? a, OperationalWords? b) => a is not null && b is not null && a.Words.SequenceEqual(b.Words) && a.OmittedIndices.SequenceEqual(b.OmittedIndices); public int GetHashCode(OperationalWords obj) => obj.Words.Count; }
    private sealed class UnboundedNumbers : IReadOnlyList<double>
    { public int Count => 0; public int Enumerated { get; private set; } public double this[int index] => index + 1; public IEnumerator<double> GetEnumerator() { for (var i = 0; ; i++) { Enumerated++; yield return i + 1; } } System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator(); }
}
