using Avalonia.Headless.XUnit;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class IncrementalSummaryTests
{
    [AvaloniaFact]
    public void Coordinate_summary_matches_raw_source_after_window_head_internal_remove_and_replace()
    {
        var source = new ChartDataBuffer<ChartPoint>(1_024);
        source.AppendRange(Enumerable.Range(0, 1_024).Select(index => new ChartPoint(index % 53 - 30, index % 37 == 0 ? double.NaN : index * -2)));
        var series = new LineSeries { ItemsSource = source }; using var owner = new Owner(series);
        Check(series.CreateSnapshot(), source);
        var visits = ChartDataBufferMetrics.SummaryRowVisitCount;
        source.AppendRange(Enumerable.Range(1_024, 17).Select(index => new ChartPoint(index, index)));
        Check(series.CreateSnapshot(), source);
        Assert.InRange(ChartDataBufferMetrics.SummaryRowVisitCount - visits, 17, 17 + 256);
        source.RemoveRange(300, 13); Check(series.CreateSnapshot(), source);
        source.ReplaceRange(511, 21, [new(-99, 99), new(99, -99)]); Check(series.CreateSnapshot(), source);
        source.InsertRange(128, [new(double.NaN, 1), new(2, double.PositiveInfinity)]); Check(series.CreateSnapshot(), source);
        series.EmptyPointMode = EmptyPointMode.Zero;
        Check(series.CreateSnapshot(), source.Select(point => double.IsFinite(point.X) && !double.IsFinite(point.Y) ? point with { Y = 0 } : point));
        source.Clear(); Check(series.CreateSnapshot(), []);
    }

    [AvaloniaFact]
    public void Scalar_and_implicit_object_x_summaries_relocate_with_each_current_ordinal()
    {
        var scalars = new ChartDataBuffer<double?>(700); scalars.AppendRange(Enumerable.Range(0, 700).Select(index => index % 127 == 0 ? (double?)null : index % 31));
        var series = new LineSeries { ItemsSource = scalars }; using var owner = new Owner(series);
        CheckScalars(); scalars.AppendRange([1, null, -20]); CheckScalars(); scalars.RemoveRange(255, 80); CheckScalars();
        scalars.InsertRange(10, [99, -99]); CheckScalars();
        var rows = new ChartDataBuffer<Row>(520); rows.AppendRange(Enumerable.Range(0, 520).Select(index => new Row(index, index % 37)));
        series.ValuePath = nameof(Row.Y); series.ItemsSource = rows;
        CheckRows(); rows.AppendRange([new(900, -90), new(901, 90)]); CheckRows(); rows.RemoveRange(99, 20); CheckRows();
        series.CategoryPath = nameof(Row.X); Check(series.CreateSnapshot(), rows.Select(row => new ChartPoint(row.X, row.Y)));
        void CheckScalars() => Check(series.CreateSnapshot(), scalars.Select((value, index) => new ChartPoint(index, value ?? double.NaN)));
        void CheckRows() => Check(series.CreateSnapshot(), rows.Select((row, index) => new ChartPoint(index, row.Y)));
    }

    [AvaloniaFact]
    public void Mixed_ordinal_constant_x_and_numeric_overlays_preserve_the_exact_full_scan_fallback()
    {
        var source = new ChartDataBuffer<object>(); source.AppendRange([new ChartPoint(20, 10), 3d, new ChartPoint(-20, 5), 7d]);
        var series = new LineSeries { ItemsSource = source }; using var owner = new Owner(series);
        var snapshot = series.CreateSnapshot(); Assert.False(snapshot.TryGetSummary(out _));
        var full = ChartDataExtrema.For(snapshot); Assert.Equal(4, full.Count); Assert.Equal(new ChartSeriesBounds(-20, 20, 3, 10), full.Bounds);
        var coordinates = new ChartDataBuffer<ChartPoint>(); coordinates.AppendRange([new(0, 1), new(1, 2)]);
        series.ItemsSource = coordinates; snapshot = series.CreateSnapshot(); Assert.True(snapshot.TryGetSummary(out _));
        var overlay = snapshot.WithValues([new(0, new ChartPoint(-5, 10), false, false), new(1, new ChartPoint(10, -3), false, false)]);
        Assert.False(overlay.TryGetSummary(out _)); Assert.Equal(new ChartSeriesBounds(-5, 10, -3, 10), ChartDataExtrema.For(overlay).Bounds);
    }

    [AvaloniaFact]
    public void Page_identity_indexes_resolve_first_remaining_duplicate_and_only_index_new_pages()
    {
        var item = new Row(7, 8);
        var source = new ChartDataBuffer<Row>(); source.AppendRange(Enumerable.Repeat(item, 520));
        var series = new LineSeries { ItemsSource = source, CategoryPath = nameof(Row.X), ValuePath = nameof(Row.Y) };
        using var owner = new Owner(series); var snapshot = series.CreateSnapshot();
        Assert.True(snapshot.TryFindIdentity(item, out var first)); Assert.Equal(0, first.SourceIndex);
        var visits = ChartDataBufferMetrics.IdentityRowVisitCount;
        source.RemoveRange(0, 257); var next = series.CreateSnapshot();
        Assert.True(next.TryFindIdentity(item, out first)); Assert.Equal(0, first.SourceIndex);
        Assert.InRange(ChartDataBufferMetrics.IdentityRowVisitCount - visits, 0, 256);
        source.AppendRange([new(900, 7)]); next = series.CreateSnapshot();
        visits = ChartDataBufferMetrics.IdentityRowVisitCount;
        Assert.True(next.TryFindIdentity(item, out first)); Assert.Equal(0, first.SourceIndex);
        Assert.Equal(visits, ChartDataBufferMetrics.IdentityRowVisitCount);
    }

    private static void Check(ChartDataSnapshot data, IEnumerable<ChartPoint> points)
    {
        // Derive this oracle from source values, independently of the summary and GetValue implementation.
        var valid = points.Where(point => double.IsFinite(point.X) && double.IsFinite(point.Y)).ToArray();
        Assert.True(data.TryGetSummary(out var summary)); Assert.Equal(valid.Length, summary.Count);
        ChartSeriesBounds? bounds = valid.Length == 0 ? null : new(valid.Min(point => point.X), valid.Max(point => point.X), valid.Min(point => point.Y), valid.Max(point => point.Y));
        Assert.Equal(bounds, summary.Bounds);
        Assert.Equal(Monotonic(valid, true, true), summary.AscendingX); Assert.Equal(Monotonic(valid, true, false), summary.DescendingX);
        Assert.Equal(Monotonic(valid, false, true), summary.AscendingY); Assert.Equal(Monotonic(valid, false, false), summary.DescendingY);
        if (valid.Length > 0) { Assert.Equal(valid[0], summary.First); Assert.Equal(valid[^1], summary.Last); }
        var extrema = ChartDataExtrema.For(data); Assert.Equal(bounds, extrema.Bounds); Assert.Equal(valid.Length, extrema.Count);
        static bool Monotonic(ChartPoint[] values, bool x, bool ascending)
        {
            for (var i = 1; i < values.Length; i++)
            {
                var a = x ? values[i - 1].X : values[i - 1].Y; var b = x ? values[i].X : values[i].Y;
                if (ascending ? b < a : b > a) return false;
            }
            return true;
        }
    }
    private sealed record Row(double X, double Y);
    private sealed class Owner : IDisposable
    {
        private readonly ChartSeries _series;
        public Owner(ChartSeries series) { _series = series; series.Changed += Changed; }
        private void Changed(object? sender, EventArgs args) { }
        public void Dispose() => _series.Changed -= Changed;
    }
}
