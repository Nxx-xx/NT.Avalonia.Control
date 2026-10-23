using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class SharedYOracleTests
{
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Dense_unordered_shared_y_matches_raw_math_full_scan_and_original_metadata(bool logarithmic, bool reversed)
    {
        var x = new OracleAxis(-4, 10, reversed, false, -1, .75, .2);
        var y = logarithmic
            ? new OracleAxis(1, 1000, reversed, true, 20, 50, .15)
            : new OracleAxis(-60, 80, reversed, false, -12, 9, .15);
        var chart = CreateChart(x, y);
        var rows = DenseRows(logarithmic);
        var line = new LineSeries { ItemsSource = rows, CategoryPath = nameof(Row.X), ValuePath = nameof(Row.Y), ShowMarkers = false };
        var scatter = new ScatterSeries { ItemsSource = rows, CategoryPath = nameof(Row.X), ValuePath = nameof(Row.Y), ShowMarkers = false };
        chart.Series.Add(line); chart.Series.Add(scatter);
        using var host = new Host(chart, x, y);

        var first = line.CreateSnapshot();
        var original = scatter.CreateSnapshot();
        var reversedStorage = new ChartDataSnapshot(original.Version, original.Points.Reverse().ToArray());
        Assert.True(scatter.TryPublishSnapshot(reversedStorage, original.Version));
        var sources = new[] { new OracleSeries(line, first), new OracleSeries(scatter, reversedStorage) };
        Assert.NotEqual(0, reversedStorage.Points[0].SourceIndex);

        var rawYs = logarithmic ? new[] { 1.7, 7.25, 13.7, 73.1, 173.2, 650.1 }
            : new[] { -53.1, -37.25, -19.1, 17.3, 49.1, 72.3 };
        foreach (var rawX in new[] { -3.537, 2.013, 5.257, 9.137 })
            foreach (var rawY in rawYs)
                AssertAgainstFullScan(host, sources, new ChartPoint(rawX, rawY));

        // Exact duplicate coordinates use physical snapshot order, not source index or Y-sort order.
        host.Move(host.Project(new ChartPoint(2, logarithmic ? 7.25 : -37.25)));
        var reversedHit = Assert.Single(chart.HoveredPoints, hit => ReferenceEquals(hit.Series, scatter));
        Assert.Equal(43, reversedHit.Index);
        Assert.Same(rows[43], reversedHit.Item);
        Assert.Same(rows[43], reversedHit.StableId);

        // A pointer in either compressed omitted band must not expose a point behind the band.
        AssertAgainstFullScan(host, sources, new ChartPoint((x.BreakStart + x.BreakEnd) / 2, rawYs[0]));
        AssertAgainstFullScan(host, sources, new ChartPoint(2, (y.BreakStart + y.BreakEnd) / 2));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Grouped_negative_bars_use_visible_slot_centres_and_reordered_source_metadata(bool reversed)
    {
        var x = new OracleAxis(-2, 6, reversed);
        var y = new OracleAxis(-70, 90, reversed, false, -5, 5, .1);
        var chart = CreateChart(x, y);
        var rows = Enumerable.Range(0, 512)
            .Select(i => new Row(i, i % 13 - 4, -80 + i * 37 % 185 + .37)).ToArray();
        rows[0] = new Row(0, 1, -20);
        rows[1] = new Row(1, 3, -20);
        rows[2] = new Row(2, 1, -20);
        rows[3] = new Row(3, 30, -20); // Closer Y, but no visible bar endpoint.
        rows[4] = new Row(4, double.NaN, -20);
        rows[5] = new Row(5, 2, double.NaN);
        var left = new BarSeries { ItemsSource = rows, CategoryPath = nameof(Row.X), ValuePath = nameof(Row.Y) };
        var right = new BarSeries { ItemsSource = rows, CategoryPath = nameof(Row.X), ValuePath = nameof(Row.Y) };
        chart.Series.Add(left); chart.Series.Add(right);
        using var host = new Host(chart, x, y);
        var originalLeft = left.CreateSnapshot();
        var originalRight = right.CreateSnapshot();
        var leftData = new ChartDataSnapshot(originalLeft.Version, originalLeft.Points.Reverse().ToArray());
        var rightData = new ChartDataSnapshot(originalRight.Version, originalRight.Points.Skip(1).Concat(originalRight.Points.Take(1)).ToArray());
        Assert.True(left.TryPublishSnapshot(leftData, originalLeft.Version));
        Assert.True(right.TryPublishSnapshot(rightData, originalRight.Version));

        // These explicit unit-spaced numeric categories use the built-in 72% group slot.
        // Derive centres directly from data units and plot width, without using scene/index helpers.
        var groupWidth = Math.Max(1, Math.Min(120, host.Chart.PlotArea.Width / 8 * .72));
        var sources = new[]
        {
            new OracleSeries(left, leftData, -groupWidth / 4),
            new OracleSeries(right, rightData, groupWidth / 4),
        };
        foreach (var rawX in new[] { -1.1, .4, 2d, 4.6, 5.8 })
            foreach (var rawY in new[] { -61.3, -34.1, -20d, 19.1, 61.7, 83.3 })
                AssertAgainstFullScan(host, sources, new ChartPoint(rawX, rawY));

        host.Move(host.Project(new ChartPoint(2, -20)));
        var first = Assert.Single(chart.HoveredPoints, hit => ReferenceEquals(hit.Series, left));
        var second = Assert.Single(chart.HoveredPoints, hit => ReferenceEquals(hit.Series, right));
        Assert.Equal(reversed ? 0 : 1, first.Index);
        Assert.Equal(reversed ? 1 : 0, second.Index);
        Assert.NotEqual(first.Position.X, second.Position.X);
        Assert.Same(rows[first.Index], first.Item); Assert.Same(rows[second.Index], second.Item);
        AssertAgainstFullScan(host, sources, new ChartPoint(2, 0));
    }

    private static void AssertAgainstFullScan(Host host, IReadOnlyList<OracleSeries> sources, ChartPoint pointer)
    {
        var position = host.Project(pointer);
        Assert.True(host.Chart.PlotArea.Contains(position));
        host.Move(position);
        var expected = new List<(OracleSeries Series, ChartDataPoint Point, Point Screen)>();
        if (host.X.IsValid(pointer.X) && host.Y.IsValid(pointer.Y))
            foreach (var source in sources)
            {
                // Intentionally enumerate every raw item. The oracle has no sorted/indexed candidate window.
                var candidates = source.Data.Points.Select((point, ordinal) =>
                    (point, ordinal, screen: host.Project(point.Point) + new Vector(source.OffsetX, 0)))
                    .Where(candidate => !candidate.point.IsEmpty && host.X.IsValid(candidate.point.Point.X) &&
                        host.Y.IsValid(candidate.point.Point.Y) && host.Chart.PlotArea.Contains(candidate.screen))
                    .OrderBy(candidate => Math.Abs(candidate.screen.Y - position.Y))
                    .ThenBy(candidate => Math.Abs(candidate.screen.X - position.X))
                    .ThenByDescending(candidate => candidate.ordinal).ToArray();
                if (candidates.Length > 0) expected.Add((source, candidates[0].point, candidates[0].screen));
            }

        Assert.Equal(expected.Count, host.Chart.HoveredPoints.Count);
        foreach (var item in expected)
        {
            var actual = Assert.Single(host.Chart.HoveredPoints, hit => ReferenceEquals(hit.Series, item.Series.Series));
            Assert.Equal(item.Point.SourceIndex, actual.Index);
            Assert.Equal(item.Point.Point, actual.Point);
            Assert.Same(item.Point.Item, actual.Item);
            Assert.Same(item.Point.StableId, actual.StableId);
            Assert.InRange(Math.Abs(item.Screen.X - actual.Position.X), 0, 1e-7);
            Assert.InRange(Math.Abs(item.Screen.Y - actual.Position.Y), 0, 1e-7);
        }
    }

    private static Row[] DenseRows(bool logarithmic)
    {
        var random = new Random(734123);
        var rows = Enumerable.Range(0, 2048).Select(i => new Row(i, (random.Next(181) - 60) / 10d,
            logarithmic ? Math.Pow(10, random.Next(-15, 71) / 20d) : random.Next(-90, 111) + .125 * (i % 3))).ToArray();
        var tieY = logarithmic ? 7.25 : -37.25;
        foreach (var index in new[] { 43, 551, 1741 }) rows[index] = new Row(index, 2, tieY);
        rows[11] = new Row(11, 25, tieY);
        rows[27] = new Row(27, double.NaN, tieY);
        rows[61] = new Row(61, 2, double.NaN);
        rows[127] = new Row(127, double.PositiveInfinity, tieY);
        rows[313] = new Row(313, 2, double.NegativeInfinity);
        if (logarithmic) rows[500] = new Row(500, 2, -5);
        return rows;
    }

    private static CartesianChart CreateChart(OracleAxis x, OracleAxis y) => new()
    {
        Width = 760, Height = 440, ShowLegend = false, TooltipMode = ChartTooltipMode.SharedY,
        XAxis = x.Create(), YAxis = y.Create(),
    };

    private sealed record Row(int Id, double X, double Y);
    private sealed record OracleSeries(ChartSeries Series, ChartDataSnapshot Data, double OffsetX = 0);

    // Integrate the transformed domain's retained length, rather than calling ChartScale,
    // IChartProjection, SelectionPosition, or either production lookup index.
    private sealed record OracleAxis(double Minimum, double Maximum, bool Reversed, bool Logarithmic = false,
        double BreakStart = double.NaN, double BreakEnd = double.NaN, double Compression = 1)
    {
        public bool IsValid(double value) => double.IsFinite(value) && (!Logarithmic || value > 0) &&
            !(value > BreakStart && value < BreakEnd);

        public double Fraction(double value)
        {
            if (!double.IsFinite(value) || Logarithmic && value <= 0) return double.NaN;
            double Transform(double number) => Logarithmic ? Math.Log10(number) : number;
            var valueLength = Transform(value) - Transform(Minimum);
            var fullLength = Transform(Maximum) - Transform(Minimum);
            if (double.IsFinite(BreakStart))
            {
                var omitted = Transform(BreakEnd) - Transform(BreakStart);
                var traversed = Math.Clamp(Transform(value) - Transform(BreakStart), 0, omitted);
                valueLength -= traversed * (1 - Compression);
                fullLength -= omitted * (1 - Compression);
            }
            var fraction = valueLength / fullLength;
            return Reversed ? 1 - fraction : fraction;
        }

        public ChartAxis Create()
        {
            ChartAxis axis = Logarithmic ? new LogarithmicAxis() : new NumericAxis();
            axis.Minimum = Minimum; axis.Maximum = Maximum; axis.IsInversed = Reversed;
            if (double.IsFinite(BreakStart)) axis.ScaleBreaks.Add(new ChartScaleBreak
                { Start = BreakStart, End = BreakEnd, CompressionRatio = Compression });
            return axis;
        }
    }

    private sealed class Host : IDisposable
    {
        private readonly Window _window;
        public CartesianChart Chart { get; }
        public OracleAxis X { get; }
        public OracleAxis Y { get; }
        public Host(CartesianChart chart, OracleAxis x, OracleAxis y)
        {
            Chart = chart; X = x; Y = y;
            _window = new Window { Width = 820, Height = 500,
                Content = new Border { Padding = new Thickness(17, 13, 19, 23), Child = chart } };
            _window.Show(); _window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
            Assert.True(chart.PlotArea.Width > 0 && chart.PlotArea.Height > 0);
        }
        public Point Project(ChartPoint point)
        {
            var plot = Chart.PlotArea;
            return new Point(plot.Left + X.Fraction(point.X) * plot.Width, plot.Bottom - Y.Fraction(point.Y) * plot.Height);
        }
        public void Move(Point point)
        {
            _window.MouseMove(Chart.TranslatePoint(point, _window)!.Value);
            Dispatcher.UIThread.RunJobs();
        }
        public void Dispose() => _window.Close();
    }
}
