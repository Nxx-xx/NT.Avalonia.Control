using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class LineInterpolationTests
{
    [AvaloniaTheory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public void Segment_query_projects_onto_the_rendered_screen_line_and_preserves_real_endpoints(bool inverseX, bool inverseY, bool logarithmic)
    {
        var x = new SegmentOracleAxis(logarithmic ? 1 : 0, logarithmic ? 100 : 10, inverseX, logarithmic);
        var y = new SegmentOracleAxis(logarithmic ? 1 : 0, logarithmic ? 10_000 : 100, inverseY, logarithmic);
        SegmentRow[] rows = logarithmic ? [new("start", 1, 1), new("end", 100, 10_000)] : [new("start", 2, 20), new("end", 8, 80)];
        var line = CreateLine(rows);
        using var host = new SegmentTestHost(x, y, line);
        var a = host.Project(rows[0].Point); var b = host.Project(rows[1].Point);
        var dx = b.X - a.X; var dy = b.Y - a.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        const double fraction = .37;
        var screen = new Point(a.X + dx * fraction, a.Y + dy * fraction);
        var pointer = new Point(screen.X - dy / length * 4, screen.Y + dx / length * 4);
        var segment = Assert.IsType<ChartSegmentHit>(host.Chart.HitTestSegment(pointer, 12));
        Assert.Same(line, segment.Series);
        Assert.Equal(fraction, segment.Fraction, 10);
        Assert.Equal(16, segment.DistanceSquared, 7);
        SegmentTestHost.AssertPosition(screen, segment.Position);
        SegmentTestHost.AssertPoint(host.Unproject(screen), segment.Point);
        Assert.True(segment.IsInterpolated);
        Assert.Same(rows[0], segment.Start.Item); Assert.Same(rows[1], segment.End.Item);
        Assert.Equal("start", segment.Start.StableId); Assert.Equal("end", segment.End.StableId);
        Assert.Equal(0, segment.Start.Index); Assert.Equal(1, segment.End.Index);
        Assert.Equal(rows[0].Point, segment.Start.Point); Assert.Equal(rows[1].Point, segment.End.Point);
        Assert.Same(segment.Start, segment.Original);
        Assert.Null(host.Chart.HitTestSegment(pointer, 3));
        if (logarithmic)
        {
            // A visual midpoint on log/log axes is (10,100), not (50.5,5000.5).
            var middle = new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2);
            var middleHit = Assert.IsType<ChartSegmentHit>(host.Chart.HitTestSegment(middle, 1));
            SegmentTestHost.AssertPoint(new ChartPoint(10, 100), middleHit.Point);
        }
        foreach (var (point, row, endpoint) in new[] { (a, rows[0], 0), (b, rows[1], 1) })
        {
            var exact = Assert.IsType<ChartSegmentHit>(host.Chart.HitTestSegment(point, 1));
            Assert.Equal(endpoint, exact.Fraction, 10);
            Assert.False(exact.IsInterpolated);
            Assert.Equal(row.Point, exact.Point);
            Assert.Same(row, exact.Original.Item);
            Assert.Equal(row.Id, exact.Original.StableId);
        }
    }

    [AvaloniaFact]
    public void Physical_adjacency_and_endpoint_identity_do_not_assume_ordinal_source_indices()
    {
        SegmentRow[] rows = [new("left", 1, 10), new("unused", 3, 70), new("right", 9, 90)];
        var line = CreateLine(rows);
        using var host = new SegmentTestHost(new(0, 10), new(0, 100), line);
        var original = line.CreateSnapshot();
        var replacement = new ChartDataSnapshot(original.Version,
            new[] { original.Points[2], original.Points[0], original.Points[1] });
        Assert.True(line.TryPublishSnapshot(replacement, original.Version));
        host.Drain();
        var segment = Assert.IsType<ChartSegmentHit>(host.Chart.HitTestSegment(host.Project(new(5, 50)), 1));
        Assert.Equal(2, segment.Start.Index); Assert.Equal(0, segment.End.Index);
        Assert.Same(rows[2], segment.Start.Item); Assert.Same(rows[0], segment.End.Item);
        Assert.Equal("right", segment.Start.StableId); Assert.Equal("left", segment.End.StableId);
        Assert.Equal(.5, segment.Fraction, 10);
        SegmentTestHost.AssertPoint(new(5, 50), segment.Point);
        Assert.Same(segment.Start, segment.Original);
    }

    [AvaloniaTheory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Invalid_data_separates_figures_and_is_not_bridged_by_segment_hit_testing(double missing)
    {
        var line = CreateLine([new("a", 1, 10), new("b", 3, 30), new("gap", 5, missing), new("c", 7, 70), new("d", 9, 90)]);
        using var host = new SegmentTestHost(new(0, 10), new(0, 100), line);
        Assert.Null(host.Chart.HitTestSegment(host.Project(new(5, 50)), 1));
        var first = Assert.IsType<ChartSegmentHit>(host.Chart.HitTestSegment(host.Project(new(2, 20)), 1));
        Assert.Equal(0, first.Start.Index); Assert.Equal(1, first.End.Index);
        var second = Assert.IsType<ChartSegmentHit>(host.Chart.HitTestSegment(host.Project(new(8, 80)), 1));
        Assert.Equal(3, second.Start.Index); Assert.Equal(4, second.End.Index);
    }

    [AvaloniaFact]
    public void A_visible_segment_can_have_both_original_endpoints_outside_the_viewport()
    {
        var line = CreateLine([new("before", -10, -100), new("after", 20, 200)]);
        using var host = new SegmentTestHost(new(0, 10), new(0, 100), line);
        var segment = Assert.IsType<ChartSegmentHit>(host.Chart.HitTestSegment(host.Project(new(5, 50)), 1));
        Assert.Equal(new ChartPoint(-10, -100), segment.Start.Point);
        Assert.Equal(new ChartPoint(20, 200), segment.End.Point);
        Assert.Equal(.5, segment.Fraction, 10);
        SegmentTestHost.AssertPoint(new(5, 50), segment.Point);
        Assert.Null(host.Chart.HitTestSegment(new Point(host.Chart.PlotArea.Left - 3, host.Chart.PlotArea.Top), 100));
    }

    [AvaloniaFact]
    public void Break_clip_rejects_the_omitted_band_but_keeps_visible_portions_of_a_crossing_segment()
    {
        var x = new SegmentOracleAxis(0, 10, BreakStart: 4, BreakEnd: 6, Compression: .1);
        var y = new SegmentOracleAxis(0, 100);
        var line = CreateLine([new("before", 1, 10), new("after", 9, 90)]);
        using var host = new SegmentTestHost(x, y, line);
        Assert.Null(host.Chart.HitTestSegment(host.Project(new(5, 50)), 1));
        var a = host.Project(new(1, 10)); var b = host.Project(new(9, 90));
        foreach (var fraction in new[] { .2, .8 })
        {
            var screen = new Point(a.X + (b.X - a.X) * fraction, a.Y + (b.Y - a.Y) * fraction);
            var segment = Assert.IsType<ChartSegmentHit>(host.Chart.HitTestSegment(screen, 1));
            Assert.Equal(fraction, segment.Fraction, 9);
            SegmentTestHost.AssertPoint(host.Unproject(screen), segment.Point);
            Assert.Same(line, segment.Series);
        }
    }

    [AvaloniaFact]
    public void Equal_distance_intersections_choose_the_earliest_physical_line_segment()
    {
        var line = CreateLine([new("a", 1, 10), new("b", 9, 90), new("c", 1, 90), new("d", 9, 10)]);
        using var host = new SegmentTestHost(new(0, 10), new(0, 100), line);
        var segment = Assert.IsType<ChartSegmentHit>(host.Chart.HitTestSegment(host.Project(new(5, 50)), 1));
        Assert.Equal(0, segment.Start.Index); Assert.Equal(1, segment.End.Index);
        Assert.Equal(.5, segment.Fraction, 9);
    }

    [AvaloniaFact]
    public void Bars_scatter_and_custom_series_do_not_fabricate_line_segments()
    {
        ChartPoint[] points = [new(1, 10), new(9, 90)];
        var scatter = new ScatterSeries { ItemsSource = points };
        var bars = new BarSeries { ItemsSource = points };
        var custom = new SegmentCustomSeries { ItemsSource = points };
        using var host = new SegmentTestHost(new(0, 10), new(0, 100), scatter, bars, custom);
        Assert.Null(host.Chart.HitTestSegment(host.Project(new(5, 50)), 1000));
    }

    [AvaloniaFact]
    public void A_zero_length_segment_is_an_original_endpoint_without_fabricated_interpolation()
    {
        SegmentRow[] rows = [new("first", 4, 40), new("second", 4, 40)];
        var line = CreateLine(rows);
        using var host = new SegmentTestHost(new(0, 10), new(0, 100), line);
        var segment = Assert.IsType<ChartSegmentHit>(host.Chart.HitTestSegment(host.Project(new(4, 40)), 1));
        Assert.Equal(0, segment.Fraction);
        Assert.False(segment.IsInterpolated);
        Assert.Same(rows[0], segment.Original.Item);
        Assert.Equal(rows[0].Point, segment.Point);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Axis_interpolation_reports_the_derived_coordinate_separately_from_original_hover_items(bool horizontal)
    {
        SegmentRow[] rows = [new("start", 1, 20), new("end", 5, 60)];
        using var host = new SegmentTestHost(new(0, 10), new(0, 100), CreateLine(rows));
        host.Chart.TooltipMode = horizontal ? ChartTooltipMode.InterpolateY : ChartTooltipMode.InterpolateX;
        host.Move(host.Project(new(3, 50)));
        var segment = Assert.Single(host.Chart.HoveredSegments);
        var expected = horizontal ? new ChartPoint(4, 50) : new ChartPoint(3, 40);
        SegmentTestHost.AssertPoint(expected, segment.Point);
        Assert.Same(rows[0], segment.Start.Item); Assert.Same(rows[1], segment.End.Item);
        var original = Assert.IsType<ChartPointHit>(host.Chart.HoveredPoint);
        Assert.Contains(rows, row => ReferenceEquals(row, original.Item) && row.Point == original.Point);
        Assert.NotEqual(segment.Point, original.Point);
        Assert.Same(segment, host.Chart.PrimarySegment);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Coincident_vertical_or_horizontal_segments_use_the_nearest_visible_orthogonal_position(bool horizontal)
    {
        SegmentRow[] rows = horizontal ? [new("a", 1, 40), new("b", 9, 40)] : [new("a", 4, 10), new("b", 4, 90)];
        using var host = new SegmentTestHost(new(0, 10), new(0, 100), CreateLine(rows));
        host.Chart.TooltipMode = horizontal ? ChartTooltipMode.InterpolateY : ChartTooltipMode.InterpolateX;
        var expected = horizontal ? new ChartPoint(7, 40) : new ChartPoint(4, 70);
        host.Move(host.Project(expected));
        var segment = Assert.Single(host.Chart.HoveredSegments);
        Assert.Equal(.75, segment.Fraction, 9);
        SegmentTestHost.AssertPoint(expected, segment.Point);
        Assert.Same(rows[1], segment.Original.Item);
    }

    internal static LineSeries CreateLine(SegmentRow[] rows) => new()
    {
        ItemsSource = rows, CategoryPath = nameof(SegmentRow.X), ValuePath = nameof(SegmentRow.Y), IdPath = nameof(SegmentRow.Id),
        ShowMarkers = false, Stroke = Brushes.Navy,
    };
    private sealed class SegmentCustomSeries : ChartSeries { public override IChartSeriesRenderer Renderer => new LineSeries().Renderer; }
}

internal sealed record SegmentRow(string Id, double X, double Y) { public ChartPoint Point => new(X, Y); }

// Integrate each independent raw axis; do not call the chart's projection/index/scale for the oracle.
internal sealed record SegmentOracleAxis(double Minimum, double Maximum, bool Inverse = false, bool Log = false,
    double BreakStart = double.NaN, double BreakEnd = double.NaN, double Compression = 1)
{
    private double Transform(double value) => Log ? Math.Log10(value) : value;
    private double Restore(double value) => Log ? Math.Pow(10, value) : value;
    public double Fraction(double value)
    {
        var full = Transform(Maximum) - Transform(Minimum);
        var offset = Transform(value) - Transform(Minimum);
        if (double.IsFinite(BreakStart))
        {
            var removed = Transform(BreakEnd) - Transform(BreakStart);
            offset -= Math.Clamp(Transform(value) - Transform(BreakStart), 0, removed) * (1 - Compression);
            full -= removed * (1 - Compression);
        }
        var fraction = offset / full;
        return Inverse ? 1 - fraction : fraction;
    }
    public double Value(double fraction)
    {
        if (Inverse) fraction = 1 - fraction;
        var full = Transform(Maximum) - Transform(Minimum);
        var start = Transform(Minimum);
        if (!double.IsFinite(BreakStart)) return Restore(start + full * fraction);
        var before = Transform(BreakStart) - start;
        var originalGap = Transform(BreakEnd) - Transform(BreakStart);
        var compressedGap = originalGap * Compression;
        var offset = (full - originalGap + compressedGap) * fraction;
        var value = offset <= before ? offset : offset < before + compressedGap
            ? before + (offset - before) / Compression : offset + originalGap - compressedGap;
        return Restore(start + value);
    }
    public ChartAxis Axis()
    {
        ChartAxis axis = Log ? new LogarithmicAxis() : new NumericAxis();
        axis.Minimum = Minimum; axis.Maximum = Maximum; axis.IsInversed = Inverse;
        if (double.IsFinite(BreakStart)) axis.ScaleBreaks.Add(new() { Start = BreakStart, End = BreakEnd, CompressionRatio = Compression });
        return axis;
    }
}

internal sealed class SegmentTestHost : IDisposable
{
    private readonly TemplateFeaturesHost _host;
    private readonly SegmentOracleAxis _x;
    private readonly SegmentOracleAxis _y;
    public SegmentTestHost(SegmentOracleAxis x, SegmentOracleAxis y, params ChartSeries[] series)
    {
        _x = x; _y = y;
        Chart = new CartesianChart { ShowLegend = false, XAxis = x.Axis(), YAxis = y.Axis() };
        foreach (var item in series) Chart.Series.Add(item);
        _host = new TemplateFeaturesHost(Chart);
    }
    public CartesianChart Chart { get; }
    public Window Window => _host.Window;
    public void Drain() => _host.Drain();
    public Point Project(ChartPoint value) => new(Chart.PlotArea.Left + _x.Fraction(value.X) * Chart.PlotArea.Width,
        Chart.PlotArea.Bottom - _y.Fraction(value.Y) * Chart.PlotArea.Height);
    public ChartPoint Unproject(Point point) => new(_x.Value((point.X - Chart.PlotArea.Left) / Chart.PlotArea.Width),
        _y.Value((Chart.PlotArea.Bottom - point.Y) / Chart.PlotArea.Height));
    public void Move(Point position)
    {
        RaiseExactMove(Chart, position);
        Drain();
    }
    internal static void RaiseExactMove(CartesianChart chart, Point position, ulong timestamp = 1)
    {
        // Avalonia 12's visual root is the TopLevelHost above Window. Passing the
        // Window as root would force GetPosition through integer screen pixels.
        var root = chart.GetVisualAncestors().Last();
        var target = chart.TranslatePoint(position, root)!.Value;
        using var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        var args = new PointerEventArgs(InputElement.PointerMovedEvent, chart, pointer, root, target, timestamp, default, KeyModifiers.None);
        AssertPosition(position, args.GetPosition(chart));
        chart.RaiseEvent(args);
    }
    public static void AssertPosition(Point expected, Point actual)
    { Assert.Equal(expected.X, actual.X, 8); Assert.Equal(expected.Y, actual.Y, 8); }
    public static void AssertPoint(ChartPoint expected, ChartPoint actual)
    { Assert.Equal(expected.X, actual.X, 7); Assert.Equal(expected.Y, actual.Y, 7); }
    public void Dispose() => _host.Dispose();
}
