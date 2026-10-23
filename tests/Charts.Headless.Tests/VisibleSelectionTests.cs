using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

[Collection("Visibility index diagnostics")]
public sealed class VisibleSelectionTests
{
    [AvaloniaTheory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void Rectangle_candidates_match_independent_projection_math_and_original_reordered_metadata(bool logarithmic, bool inversed)
    {
        var random = new Random(611);
        var rows = Enumerable.Range(0, 8193).Select(i => new Row($"row:{i}", random.NextDouble() * 1000,
            logarithmic ? Math.Pow(10, random.NextDouble() * 3) : random.NextDouble() * 1000)).ToArray();
        rows[11] = rows[11] with { X = double.NaN };
        rows[23] = rows[23] with { Y = double.NaN };
        var series = Points(rows);
        var chart = CreateChart(1000, logarithmic ? 1 : 0, 1000, inversed, logarithmic);
        chart.Series.Add(series);
        using var host = new Host(chart);
        var original = series.CreateSnapshot();
        var reordered = new ChartDataSnapshot(original.Version, original.Points.Reverse()
            .Select(point => point with { RawPoint = new(point.SourceIndex + .25, point.SourceIndex + .5) }).ToArray());
        Assert.True(series.TryPublishSnapshot(reordered, original.Version));
        chart.Viewport = logarithmic ? new(150, 850, 2, 500) : new(150, 850, 100, 900);
        host.Drain();

        var plot = chart.PlotArea;
        var rectangle = new Rect(plot.Left + plot.Width * .21, plot.Top + plot.Height * .29, plot.Width * .37, plot.Height * .31);
        var expected = reordered.Points.Select(point => (Point: point,
            Position: Project(point.Point, plot, chart.VisibleRange!.Value, logarithmic, inversed)))
            .Where(item => Finite(item.Position) && rectangle.Contains(item.Position)).ToArray();
        Assert.NotEmpty(expected);
        var result = chart.SelectRectangle(rectangle);

        Assert.Equal(expected.Length, result.MatchedCount);
        Assert.Equal(expected.Length, result.SelectedCount);
        Assert.False(result.IsTruncated); Assert.False(result.IsCanceled);
        Assert.Equal(expected.Select(item => item.Point.SourceIndex), chart.SelectedPoints!.Select(hit => hit.Index));
        foreach (var (expectedPoint, actual) in expected.Zip(chart.SelectedPoints!))
        {
            Assert.Same(series, actual.Series);
            Assert.Same(rows[expectedPoint.Point.SourceIndex], actual.Item);
            Assert.Equal(expectedPoint.Point.StableId, actual.StableId);
            Assert.Equal(expectedPoint.Point.Point, actual.Point);
            Assert.Equal(expectedPoint.Point.RawPoint, actual.RawPoint);
            Assert.InRange(Math.Abs(expectedPoint.Position.X - actual.Position.X), 0, 1e-8);
            Assert.InRange(Math.Abs(expectedPoint.Position.Y - actual.Position.Y), 0, 1e-8);
        }
        Assert.Equal(reordered.Points, series.CreateSnapshot().Points);
    }

    [AvaloniaTheory]
    [InlineData(0, false)] [InlineData(0, true)]
    [InlineData(1, false)] [InlineData(1, true)]
    [InlineData(2, false)] [InlineData(2, true)]
    public void Narrow_grouped_bar_selection_shifts_the_broad_query_to_displayed_centers(int kind, bool inversed)
    {
        var horizontal = kind == 2;
        var firstRows = Enumerable.Range(0, 11).Select(i => new Row($"left:{i}", i * 10, 60)).ToArray();
        var secondRows = Enumerable.Range(0, 11).Select(i => new Row($"right:{i}", i * 10, 80)).ToArray();
        var first = Bars(firstRows, horizontal, kind > 0);
        var second = Bars(secondRows, horizontal, kind > 0);
        var chart = CreateChart(100, 0, 100, inversed);
        chart.Series.Add(first); chart.Series.Add(second);
        using var host = new Host(chart);
        // Unit-spaced numeric categories use the 72% group width, split between
        // two series. This derives the displacement without any scene helper.
        var width = Math.Max(1, Math.Min(120, (horizontal ? chart.PlotArea.Height : chart.PlotArea.Width) * .1 * .72));
        foreach (var group in new[] { 0, 1 })
        {
            var raw = horizontal ? new ChartPoint(group == 0 ? 60 : 80, 50) : new ChartPoint(50, group == 0 ? 60 : 80);
            var offset = (group == 0 ? -1 : 1) * width / 4;
            var center = Project(raw, chart.PlotArea, new(0, 100, 0, 100), false, inversed) +
                (horizontal ? new Vector(0, offset) : new Vector(offset, 0));
            var result = chart.SelectRectangle(new Rect(center.X - .25, center.Y - .25, .5, .5));
            Assert.Equal(1, result.MatchedCount); Assert.Equal(1, result.SelectedCount); Assert.False(result.IsTruncated);
            var actual = Assert.Single(chart.SelectedPoints!);
            Assert.Same(group == 0 ? first : second, actual.Series);
            Assert.Same((group == 0 ? firstRows : secondRows)[5], actual.Item);
            Assert.Equal(5, actual.Index);
            Assert.Equal(raw, actual.Point);
            if (kind > 0) Assert.Equal(new ChartPoint(50, group == 0 ? 60 : 80), actual.RawPoint);
            Assert.InRange(Math.Abs(center.X - actual.Position.X), 0, 1e-8);
            Assert.InRange(Math.Abs(center.Y - actual.Position.Y), 0, 1e-8);
            // Use the canonical selection position to isolate inverse query
            // rounding from the different arithmetic order of bar hit geometry.
            var exact = actual.Position;
            var boundary = horizontal ? new Rect(exact.X - .25, exact.Y, .5, .5)
                : new Rect(exact.X, exact.Y - .25, .5, .5);
            Assert.True(boundary.Contains(exact));
            Assert.True(boundary.Intersect(chart.PlotArea).Contains(exact));
            var repeated = chart.SelectRectangle(boundary);
            Assert.Equal(1, repeated.MatchedCount); Assert.Equal(1, repeated.SelectedCount);
            Assert.Equal(actual.Index, Assert.Single(chart.SelectedPoints!).Index);
        }
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Asymmetric_numeric_domains_keep_zero_at_an_exact_positive_area_selection_boundary(bool inversed)
    {
        var rows = new[] { new Row("negative", -.075, .1), new Row("zero", 0, .1), new Row("positive", .08, .1) };
        var series = Points(rows);
        var chart = CreateChart(.217, -.141, .329, inversed);
        chart.XAxis.Minimum = -.113;
        chart.Series.Add(series);
        using var host = new Host(chart);
        var point = Project(new(0, .1), chart.PlotArea, new(-.113, .217, -.141, .329), false, inversed);
        var broad = chart.SelectRectangle(new Rect(point.X - .25, point.Y - .25, .5, .5));
        Assert.Equal(1, broad.MatchedCount);
        var selected = Assert.Single(chart.SelectedPoints!);
        Assert.Equal(1, selected.Index);
        var boundary = new Rect(selected.Position.X, selected.Position.Y - .25, .5, .5);
        Assert.True(boundary.Contains(selected.Position));
        Assert.True(boundary.Intersect(chart.PlotArea).Contains(selected.Position));
        var result = chart.SelectRectangle(boundary);
        Assert.Equal(1, result.MatchedCount); Assert.Equal(1, result.SelectedCount);
        Assert.Equal(1, Assert.Single(chart.SelectedPoints!).Index);
        // Avalonia intersects a zero-area rectangle to Rect.Empty. Keep the
        // pre-existing SelectRectangle contract after its plot intersection.
        var zeroArea = new Rect(selected.Position.X, selected.Position.Y - .25, 0, .5);
        Assert.True(zeroArea.Contains(selected.Position));
        Assert.False(zeroArea.Intersect(chart.PlotArea).Contains(selected.Position));
        var empty = chart.SelectRectangle(zeroArea);
        Assert.Equal(0, empty.MatchedCount); Assert.Equal(0, empty.SelectedCount);
    }

    [AvaloniaFact]
    public void Limited_and_additive_queries_keep_physical_order_full_matched_counts_and_callback_cancellation()
    {
        var rows = Enumerable.Range(0, 100).Select(i => new Row($"row:{i}", i, 50)).ToArray();
        var series = Points(rows);
        var chart = CreateChart(100, 0, 100); chart.MaxSelectionCount = 4;
        chart.Series.Add(series);
        using var host = new Host(chart);
        var original = series.CreateSnapshot();
        Assert.True(series.TryPublishSnapshot(new(original.Version, original.Points.Reverse().ToArray()), original.Version));
        host.Drain();
        Rect Between(double first, double last)
        {
            var a = Project(new(first, 49), chart.PlotArea, new(0, 100, 0, 100));
            var b = Project(new(last, 51), chart.PlotArea, new(0, 100, 0, 100));
            return new(new Point(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y)), new Point(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y)));
        }
        var result = chart.SelectRectangle(Between(10.1, 19.9));
        Assert.Equal(9, result.MatchedCount); Assert.Equal(4, result.SelectedCount); Assert.True(result.IsTruncated);
        Assert.Equal(new[] { 19, 18, 17, 16 }, chart.SelectedPoints!.Select(hit => hit.Index));
        result = chart.SelectRectangle(Between(15.9, 19.1), additive: true);
        Assert.Equal(4, result.MatchedCount); Assert.Equal(4, result.SelectedCount); Assert.False(result.IsTruncated);
        result = chart.SelectRectangle(Between(9.9, 14.1), additive: true);
        Assert.Equal(5, result.MatchedCount); Assert.Equal(4, result.SelectedCount); Assert.True(result.IsTruncated);
        var previous = chart.SelectedPoints;
        chart.SelectionChanging += (_, args) => { Assert.Equal(ChartSelectionChangeReason.Rectangle, args.Reason); args.Cancel = true; };
        result = chart.SelectRectangle(Between(79.9, 84.1));
        Assert.Equal(5, result.MatchedCount); Assert.Equal(4, result.SelectedCount); Assert.True(result.IsTruncated); Assert.True(result.IsCanceled);
        Assert.Same(previous, chart.SelectedPoints);
    }

    [AvaloniaFact]
    public void Sparse_two_dimensional_rectangle_reuses_index_and_does_not_project_the_full_source()
    {
        var points = Enumerable.Range(0, 256 * 256).Select(i => new ChartPoint(i / 256, i % 256)).ToArray();
        new Random(551).Shuffle(points);
        var series = new ScatterSeries { ItemsSource = points, ShowMarkers = false };
        var chart = CreateChart(256, 0, 256); chart.Series.Add(series);
        using var host = new Host(chart);
        var first = Project(new(99.5, 99.5), chart.PlotArea, new(0, 256, 0, 256));
        var last = Project(new(103.5, 103.5), chart.PlotArea, new(0, 256, 0, 256));
        var rectangle = new Rect(new Point(first.X, last.Y), new Point(last.X, first.Y));
        Assert.Equal(16, chart.SelectRectangle(rectangle).MatchedCount);
        var builds = ChartVisibilityIndex.PointBuildCount;
        var visited = ChartVisibilityIndex.VisitedCount;
        var projected = ChartVisibilityQuery.ProjectedCount;
        var result = chart.SelectRectangle(rectangle);
        Assert.Equal(16, result.MatchedCount); Assert.Equal(16, result.SelectedCount);
        Assert.Equal(builds, ChartVisibilityIndex.PointBuildCount);
        Assert.InRange(ChartVisibilityIndex.VisitedCount - visited, 1, 4096);
        Assert.InRange(ChartVisibilityQuery.ProjectedCount - projected, 16, 128);
        Assert.Equal(points.Select((point, index) => (point, index)).Where(item => item.point.X >= 100 && item.point.X <= 103 && item.point.Y >= 100 && item.point.Y <= 103)
            .Select(item => item.index), chart.SelectedPoints!.Select(hit => hit.Index));
    }

    [AvaloniaFact]
    public void Custom_folded_axis_keeps_exhaustive_selection_semantics()
    {
        var rows = new[] { new Row("first", 20, 50), new Row("folded", 80, 50) };
        var series = Points(rows);
        var chart = CreateChart(100, 0, 100);
        chart.XAxis = new FoldedAxis { Minimum = 0, Maximum = 100 };
        chart.Series.Add(series);
        using var host = new Host(chart);
        var center = Project(new(20, 50), chart.PlotArea, new(0, 100, 0, 100));
        var fallback = ChartVisibilityQuery.FallbackCount;
        var result = chart.SelectRectangle(new Rect(center.X - .25, center.Y - .25, .5, .5));
        Assert.Equal(1, result.MatchedCount);
        Assert.True(ChartVisibilityQuery.FallbackCount > fallback);
        var hit = Assert.Single(chart.SelectedPoints!);
        Assert.Equal(1, hit.Index); Assert.Same(rows[1], hit.Item); Assert.Equal("folded", hit.StableId);
    }

    private sealed record Row(string Id, double X, double Y);
    private sealed class FoldedAxis : ChartAxis
    {
        public override double Transform(double value) => value > 0 && value < 100 ? 100 - value : value;
        public override double Inverse(double value) => value;
    }
    private static ScatterSeries Points(Row[] rows) => new()
    { ItemsSource = rows, CategoryPath = nameof(Row.X), ValuePath = nameof(Row.Y), IdPath = nameof(Row.Id), ShowMarkers = false };
    private static BarSeries Bars(Row[] rows, bool horizontal, bool advanced) => new()
    { ItemsSource = rows, CategoryPath = nameof(Row.X), ValuePath = nameof(Row.Y), IdPath = nameof(Row.Id),
        Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical, Baseline = advanced ? 2 : 0, ShowMarkers = false };
    private static CartesianChart CreateChart(double maximumX, double minimumY, double maximumY, bool inversed = false, bool logarithmic = false) => new()
    {
        Width = 760, Height = 440, ShowLegend = false, SelectionMode = ChartSelectionMode.Multiple,
        XAxis = new NumericAxis { Minimum = 0, Maximum = maximumX, IsInversed = inversed },
        YAxis = logarithmic ? new LogarithmicAxis { Minimum = minimumY, Maximum = maximumY, IsInversed = inversed }
            : new NumericAxis { Minimum = minimumY, Maximum = maximumY, IsInversed = inversed },
    };
    private static Point Project(ChartPoint value, Rect plot, ChartViewport range, bool logarithmic = false, bool inversed = false)
    {
        var x = (value.X - range.XMinimum) / (range.XMaximum - range.XMinimum);
        var y = logarithmic ? (Math.Log10(value.Y) - Math.Log10(range.YMinimum)) / (Math.Log10(range.YMaximum) - Math.Log10(range.YMinimum))
            : (value.Y - range.YMinimum) / (range.YMaximum - range.YMinimum);
        if (inversed) { x = 1 - x; y = 1 - y; }
        return new(plot.Left + x * plot.Width, plot.Bottom - y * plot.Height);
    }
    private static bool Finite(Point point) => double.IsFinite(point.X) && double.IsFinite(point.Y);
    private sealed class Host : IDisposable
    {
        private readonly CartesianChart _chart;
        private readonly Window _window;
        internal Host(CartesianChart chart)
        { _chart = chart; _window = new Window { Width = 820, Height = 500, Content = chart }; _window.Show(); Drain(); }
        internal void Drain()
        {
            Dispatcher.UIThread.RunJobs(); _window.UpdateLayout();
            using var drawing = new DrawingGroup().Open(); _chart.Render(drawing);
            Dispatcher.UIThread.RunJobs();
        }
        public void Dispose() => _window.Close();
    }
}
