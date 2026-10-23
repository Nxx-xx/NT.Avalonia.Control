using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Layout;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class ComparisonDataTests
{
    private sealed record Row(string Id, double X, double Y, bool Total = false);
    private sealed record Band(string Id, double X, double? High, double? Low);
    private sealed record CategoryRow(string Id, string Name, double Amount);

    private static BarSeries Bar(ChartStackMode mode = ChartStackMode.None, params Row[] rows) => new()
    {
        ItemsSource = rows, CategoryPath = "X", ValuePath = "Y", IdPath = "Id", StackMode = mode, WidthRatio = .8,
    };

    private static ChartPointHit Point(CartesianChart chart, ChartSeries series, int sourceIndex)
    {
        Assert.True(chart.NavigateToPoint(series, sourceIndex));
        var point = Assert.IsType<ChartPointHit>(chart.CurrentPoint);
        Assert.Same(series, point.Series); Assert.Equal(sourceIndex, point.Index);
        return point;
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Orientation_swaps_display_coordinates_without_mutating_canonical_source_or_readouts(bool horizontal)
    {
        var row = new Row("business", 1, 35);
        var series = Bar(ChartStackMode.None, row); series.Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical;
        using var host = horizontal ? new SegmentTestHost(new(-100, 100), new(0, 4), series)
            : new SegmentTestHost(new(0, 4), new(-100, 100), series);
        host.Chart.ShowCrosshairLabels = true;
        var point = Point(host.Chart, series, 0);
        Assert.Equal(horizontal ? new ChartPoint(35, 1) : new ChartPoint(1, 35), point.Point);
        Assert.Equal(new ChartPoint(1, 35), point.RawPoint); Assert.Equal(0, point.BaseValue);
        Assert.Same(row, point.Item); Assert.Equal("business", point.StableId);
        var original = series.CreateSnapshot().Points[0];
        Assert.Equal(new ChartPoint(1, 35), original.Point); Assert.Null(original.RawPoint); Assert.Null(original.BaseValue);
        var hit = Assert.IsType<ChartPointHit>(host.Chart.HitTestPoint(point.Position, .01));
        Assert.Same(row, hit.Item); Assert.Equal(point.Point, hit.Point);
        var reading = Assert.Single(host.Chart.AxisReadings, reading => ReferenceEquals(reading.Axis, horizontal ? host.Chart.XAxis : host.Chart.YAxis));
        Assert.Equal(35, reading.Value, 7);
        Assert.Equal(point.RawPoint, Assert.Single(host.Chart.HoveredPoints).RawPoint);
    }

    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Range_bars_normalize_high_low_while_variance_retains_their_direction(bool horizontal, bool variance)
    {
        var rows = new[] { new Band("reversed", 1, 20, 70), new Band("ordered", 3, 80, 30) };
        ComparisonSeries series = variance ? new VarianceSeries() : new RangeBarSeries();
        series.ItemsSource = rows; series.CategoryPath = "X"; series.HighPath = "High"; series.LowPath = "Low"; series.IdPath = "Id";
        series.Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical;
        using var host = horizontal ? new SegmentTestHost(new(0, 100), new(0, 4), series)
            : new SegmentTestHost(new(0, 4), new(0, 100), series);
        var first = Point(host.Chart, series, 0);
        var upper = variance ? 20 : 70; var lower = variance ? 70 : 20;
        Assert.Equal(horizontal ? new ChartPoint(upper, 1) : new ChartPoint(1, upper), first.Point);
        Assert.Equal(lower, first.BaseValue); Assert.Same(rows[0], first.Item); Assert.Equal("reversed", first.StableId);
        var canonical = series.CreateSnapshot().Points[0];
        Assert.False(canonical.IsEmpty); Assert.False(canonical.WasEmpty); Assert.Equal(20, canonical.High); Assert.Equal(70, canonical.Low);
        Assert.Equal(variance ? 20 : 70, canonical.Point.Y); Assert.Null(canonical.BaseValue);
        Assert.Equal(30, Point(host.Chart, series, 1).BaseValue);
    }

    [AvaloniaTheory]
    [InlineData(EmptyPointMode.Gap, double.NaN)]
    [InlineData(EmptyPointMode.Zero, 0d)]
    [InlineData(EmptyPointMode.Average, 40d)]
    [InlineData(EmptyPointMode.Interpolate, 30d)]
    public void Range_bar_missing_bound_policies_keep_original_missing_status(EmptyPointMode mode, double expected)
    {
        var series = new RangeBarSeries
        {
            ItemsSource = new[] { new Band("a", 0, 20, 10), new Band("gap", 1, null, 15), new Band("b", 4, 60, 30) },
            CategoryPath = "X", HighPath = "High", LowPath = "Low", IdPath = "Id", EmptyPointMode = mode,
        };
        var point = series.CreateSnapshot().Points[1];
        Assert.True(point.WasEmpty);
        if (double.IsNaN(expected)) { Assert.True(point.IsEmpty); Assert.Null(point.High); }
        else { Assert.False(point.IsEmpty); Assert.Equal(expected, point.High); Assert.Equal(15, point.Low); }
    }

    [AvaloniaFact]
    public void Variance_missing_endpoint_interpolation_keeps_the_original_high_low_direction()
    {
        var series = new VarianceSeries
        {
            ItemsSource = new[] { new Band("a", 0, 40, 90), new Band("gap", 1, null, 75), new Band("b", 2, 10, 60) },
            CategoryPath = "X", HighPath = "High", LowPath = "Low", IdPath = "Id", EmptyPointMode = EmptyPointMode.Average,
        };
        var point = series.CreateSnapshot().Points[1];
        Assert.True(point.WasEmpty); Assert.False(point.IsEmpty); Assert.Equal(25, point.High); Assert.Equal(75, point.Low); Assert.Equal(25, point.Point.Y);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Waterfall_uses_source_order_deltas_explicit_total_reset_and_gap_without_changing_the_running_value(bool horizontal)
    {
        Row[] rows = [new("start", 3, 40), new("down", 1, -15), new("gap", 2, double.NaN), new("after-gap", 4, 10), new("total", 5, 60, true), new("after-total", 6, -5)];
        var series = new WaterfallSeries
        {
            ItemsSource = rows, CategoryPath = "X", ValuePath = "Y", IdPath = "Id", TotalPath = "Total", Baseline = 5,
            Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical,
        };
        using var host = horizontal ? new SegmentTestHost(new(0, 100), new(0, 7), series)
            : new SegmentTestHost(new(0, 7), new(0, 100), series);
        var expectedLower = new[] { 5d, 45, double.NaN, 30, 5, 60 };
        var expectedUpper = new[] { 45d, 30, double.NaN, 40, 60, 55 };
        for (var i = 0; i < rows.Length; i++)
        {
            if (i == 2) { Assert.False(host.Chart.NavigateToPoint(series, i)); continue; }
            var point = Point(host.Chart, series, i);
            Assert.Equal(expectedLower[i], point.BaseValue); Assert.Equal(expectedUpper[i], horizontal ? point.Point.X : point.Point.Y);
            Assert.Equal(rows[i].Total, point.IsTotal); Assert.Equal(new ChartPoint(rows[i].X, rows[i].Y), point.RawPoint);
            Assert.Same(rows[i], point.Item);
        }
        Assert.True(series.CreateSnapshot().Points[4].IsTotal);
        Assert.Equal(-15, series.CreateSnapshot().Points[1].Point.Y);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Signed_bar_stacks_use_one_category_lane_and_expose_original_contributions(bool horizontal)
    {
        var a = Bar(ChartStackMode.Stacked, new("a", 1, 20), new("b", 3, -10));
        var b = Bar(ChartStackMode.Stacked, new("c", 1, 30), new("d", 3, -40));
        a.Orientation = b.Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical;
        using var host = horizontal ? new SegmentTestHost(new(-100, 100), new(0, 4), a, b)
            : new SegmentTestHost(new(0, 4), new(-100, 100), a, b);
        var positive = Point(host.Chart, b, 0); var negative = Point(host.Chart, b, 1);
        Assert.Equal(50, horizontal ? positive.Point.X : positive.Point.Y); Assert.Equal(20, positive.BaseValue); Assert.Equal(30, positive.StackValue);
        Assert.Equal(-50, horizontal ? negative.Point.X : negative.Point.Y); Assert.Equal(-10, negative.BaseValue); Assert.Equal(-40, negative.StackValue);
        Assert.Equal(new ChartPoint(1, 30), positive.RawPoint); Assert.Equal(new ChartPoint(3, -40), negative.RawPoint);
        var first = Point(host.Chart, a, 0);
        Assert.Equal(horizontal ? first.Position.Y : first.Position.X, horizontal ? positive.Position.Y : positive.Position.X, 8);
        Assert.Equal(30, b.CreateSnapshot().Points[0].Point.Y); Assert.Null(b.CreateSnapshot().Points[0].StackValue);
    }

    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Percent_bars_normalize_each_sign_without_overflow_and_finish_at_exact_signed_hundred(bool horizontal, bool huge)
    {
        var amount = huge ? 1e308 : 1;
        var chart = new CartesianChart { ShowLegend = false };
        if (horizontal) chart.YAxis = new NumericAxis { Minimum = 0, Maximum = 4 };
        else chart.XAxis = new NumericAxis { Minimum = 0, Maximum = 4 };
        for (var i = 0; i < 7; i++)
        {
            var bar = Bar(ChartStackMode.Percent, new("positive", 1, amount), new("negative", 2, -amount), new("zero", 3, 0));
            bar.Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical; chart.Series.Add(bar);
        }
        using var host = new TemplateFeaturesHost(chart);
        var range = chart.VisibleRange!.Value;
        Assert.Equal(-100, horizontal ? range.XMinimum : range.YMinimum); Assert.Equal(100, horizontal ? range.XMaximum : range.YMaximum);
        var last = chart.Series[^1]; var positive = Point(chart, last, 0); var negative = Point(chart, last, 1); var zero = Point(chart, last, 2);
        Assert.Equal(100, horizontal ? positive.Point.X : positive.Point.Y); Assert.Equal(-100, horizontal ? negative.Point.X : negative.Point.Y);
        Assert.Equal(horizontal ? positive.Point.X : positive.Point.Y, positive.BaseValue + positive.StackValue);
        Assert.Equal(horizontal ? negative.Point.X : negative.Point.Y, negative.BaseValue + negative.StackValue);
        Assert.Equal(0, zero.StackValue); Assert.Equal(amount, positive.RawPoint!.Value.Y);
    }

    [AvaloniaFact]
    public void Numeric_duplicate_categories_stack_by_their_source_occurrence()
    {
        var a = Bar(ChartStackMode.Stacked, new("a", 3, 10), new("b", 1, 20), new("c", 1, 30));
        var b = Bar(ChartStackMode.Stacked, new("d", 1, 2), new("e", 3, 4), new("f", 1, 6));
        using var host = new SegmentTestHost(new(0, 4), new(0, 100), a, b);
        Assert.Equal(20, Point(host.Chart, b, 0).BaseValue); Assert.Equal(10, Point(host.Chart, b, 1).BaseValue); Assert.Equal(30, Point(host.Chart, b, 2).BaseValue);
        Assert.Equal(22, Point(host.Chart, b, 0).Point.Y); Assert.Equal(14, Point(host.Chart, b, 1).Point.Y); Assert.Equal(36, Point(host.Chart, b, 2).Point.Y);
    }

    [AvaloniaFact]
    public void Hiding_and_observable_source_updates_reconcile_selected_stack_identity_and_metadata()
    {
        var rows = new ObservableCollection<Row> { new("business-b", 1, 20) };
        var a = Bar(ChartStackMode.Stacked, new Row("business-a", 1, 10)); var b = Bar(ChartStackMode.Stacked); b.ItemsSource = rows;
        using var host = new SegmentTestHost(new(0, 2), new(0, 100), a, b);
        host.Chart.SelectedPoint = Point(host.Chart, b, 0); host.Drain();
        Assert.Equal(30, host.Chart.SelectedPoint!.Point.Y);
        a.IsVisible = false; host.Drain(); _ = host.Chart.PlotArea;
        Assert.Equal(20, host.Chart.SelectedPoint!.Point.Y); Assert.Equal(0, host.Chart.SelectedPoint.BaseValue);
        rows[0] = new("business-b", 1, 40); host.Drain(); _ = host.Chart.PlotArea;
        Assert.Same(rows[0], host.Chart.SelectedPoint!.Item); Assert.Equal(40, host.Chart.SelectedPoint.Point.Y);
        a.IsVisible = true; host.Drain(); _ = host.Chart.PlotArea;
        Assert.Equal(50, host.Chart.SelectedPoint!.Point.Y); Assert.Equal(10, host.Chart.SelectedPoint.BaseValue);
    }

    [AvaloniaFact]
    public void Stable_value_sorting_preserves_original_source_indices_duplicate_items_and_selection_across_updates()
    {
        var rows = new ObservableCollection<CategoryRow> { new("a", "same", 20), new("b", "low", 10), new("c", "same", 80) };
        var series = new BarSeries
        {
            ItemsSource = rows, CategoryPath = "Name", ValuePath = "Amount", IdPath = "Id", SortMode = ChartSeriesSortMode.ValueDescending,
        };
        var chart = new CartesianChart { ShowLegend = false, XAxis = new CategoryAxis(), YAxis = new NumericAxis { Minimum = 0, Maximum = 100 } };
        chart.Series.Add(series); using var host = new TemplateFeaturesHost(chart);
        var a = Point(chart, series, 0); var b = Point(chart, series, 1); var c = Point(chart, series, 2);
        Assert.Equal(1, a.Point.X); Assert.Equal(2, b.Point.X); Assert.Equal(0, c.Point.X);
        Assert.Same(rows[0], a.Item); Assert.Same(rows[2], c.Item); Assert.Equal("a", a.StableId); Assert.Equal("c", c.StableId);
        Assert.Equal(new ChartPoint(0, 20), a.RawPoint); Assert.Equal(new ChartPoint(2, 80), c.RawPoint);
        chart.SelectedPoint = a; host.Drain(); rows[0] = new("a", "same", 90); host.Drain(); _ = chart.PlotArea;
        Assert.Equal(0, chart.SelectedPoint!.Point.X); Assert.Same(rows[0], chart.SelectedPoint.Item); Assert.Equal(90, chart.SelectedPoint.Point.Y);
        Assert.Equal("a", chart.SelectedPoint.StableId); Assert.Equal(0, chart.SelectedPoint.Index); Assert.Null(chart.XAxis.Labels);
    }

    [AvaloniaFact]
    public void Value_sorted_duplicate_stacks_align_peer_series_by_original_source_occurrence()
    {
        var driver = new BarSeries
        {
            ItemsSource = new[] { new CategoryRow("small-first", "same", 20), new CategoryRow("large-second", "same", 80) },
            CategoryPath = "Name", ValuePath = "Amount", IdPath = "Id", SortMode = ChartSeriesSortMode.ValueDescending, StackMode = ChartStackMode.Stacked,
        };
        var peer = new BarSeries
        {
            ItemsSource = new[] { new CategoryRow("peer-first", "same", 10), new CategoryRow("peer-second", "same", 30) },
            CategoryPath = "Name", ValuePath = "Amount", IdPath = "Id", StackMode = ChartStackMode.Stacked,
        };
        var chart = new CartesianChart { ShowLegend = false, XAxis = new CategoryAxis(), YAxis = new NumericAxis { Minimum = 0, Maximum = 150 } };
        chart.Series.Add(driver); chart.Series.Add(peer); using var host = new TemplateFeaturesHost(chart);
        var first = Point(chart, peer, 0); var second = Point(chart, peer, 1);
        Assert.Equal(1, first.Point.X); Assert.Equal(20, first.BaseValue); Assert.Equal(30, first.Point.Y);
        Assert.Equal(0, second.Point.X); Assert.Equal(80, second.BaseValue); Assert.Equal(110, second.Point.Y);
        Assert.Equal("peer-first", first.StableId); Assert.Equal("peer-second", second.StableId);
    }

    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Pareto_sorts_valid_nonnegative_categories_exposes_exact_cumulative_hundred_and_keeps_its_axis_private(bool horizontal, bool huge)
    {
        var unit = huge ? 1e307 : 1;
        CategoryRow[] rows = [new("small", "Small", unit), new("large", "Large", 6 * unit), new("middle", "Middle", 3 * unit), new("negative", "Negative", -unit), new("missing", "Missing", double.NaN)];
        var series = new ParetoSeries
        {
            ItemsSource = rows, CategoryPath = "Name", ValuePath = "Amount", IdPath = "Id", Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical,
        };
        var chart = new CartesianChart { ShowLegend = false };
        if (horizontal) chart.YAxis = new CategoryAxis(); else chart.XAxis = new CategoryAxis();
        chart.Series.Add(series); using var host = new TemplateFeaturesHost(chart);
        var large = Point(chart, series, 1); var middle = Point(chart, series, 2); var small = Point(chart, series, 0);
        Assert.Equal(0, horizontal ? large.Point.Y : large.Point.X); Assert.Equal(1, horizontal ? middle.Point.Y : middle.Point.X);
        Assert.Equal(2, horizontal ? small.Point.Y : small.Point.X);
        Assert.Equal(60, large.CumulativeValue!.Value, 9); Assert.Equal(90, middle.CumulativeValue!.Value, 9); Assert.Equal(100, small.CumulativeValue);
        Assert.Same(rows[0], small.Item); Assert.Equal("small", small.StableId); Assert.Equal(0, small.Index);
        Assert.False(chart.NavigateToPoint(series, 3)); Assert.False(chart.NavigateToPoint(series, 4));
        Assert.Empty(chart.Axes); Assert.Contains(chart.AccessibilitySummary.Axes, axis => axis.Title == "累计 (%)" && axis.Minimum == 0 && axis.Maximum == 100);
        _ = Point(chart, series, 0); Assert.Equal(100, Assert.Single(chart.HoveredPoints).CumulativeValue);
        Assert.Equal(unit, series.CreateSnapshot().Points[0].Point.Y); Assert.Null(series.CreateSnapshot().Points[0].CumulativeValue);
    }

    [AvaloniaFact]
    public void All_zero_Pareto_has_zero_cumulative_values_and_stable_tie_order()
    {
        var series = new ParetoSeries { ItemsSource = new[] { new CategoryRow("a", "A", 0), new CategoryRow("b", "B", 0) }, CategoryPath = "Name", ValuePath = "Amount", IdPath = "Id" };
        var chart = new CartesianChart { ShowLegend = false, XAxis = new CategoryAxis() }; chart.Series.Add(series);
        using var host = new TemplateFeaturesHost(chart);
        Assert.Equal(0, Point(chart, series, 0).CumulativeValue); Assert.Equal(0, Point(chart, series, 0).Point.X);
        Assert.Equal(0, Point(chart, series, 1).CumulativeValue); Assert.Equal(1, Point(chart, series, 1).Point.X);
    }

    [AvaloniaFact]
    public void Pareto_cumulative_axis_break_clip_is_independent_of_the_bar_axis_during_render_animation_and_export()
    {
        var valueAxis = new NumericAxis { Minimum = 0, Maximum = 100 };
        valueAxis.ScaleBreaks.Add(new() { Start = 30, End = 50, CompressionRatio = .2 });
        var cumulativeAxis = new NumericAxis { Key = "percent", Minimum = 0, Maximum = 100, Position = ChartAxisPosition.Far };
        cumulativeAxis.ScaleBreaks.Add(new() { Start = 70, End = 80, CompressionRatio = .3 });
        var series = new ParetoSeries
        {
            ItemsSource = new[] { new CategoryRow("a", "A", 90), new CategoryRow("b", "B", 70), new CategoryRow("c", "C", 10) },
            CategoryPath = "Name", ValuePath = "Amount", IdPath = "Id", CumulativeAxisKey = "percent", Fill = Brushes.Blue, CumulativeStroke = Brushes.Red,
        };
        var chart = new CartesianChart { ShowLegend = false, XAxis = new CategoryAxis(), YAxis = valueAxis };
        chart.Axes.Add(cumulativeAxis); chart.Series.Add(series); using var host = new TemplateFeaturesHost(chart);
        AssertSecondaryClip(TemplateFeaturesHost.Record(chart));
        var exported = new DrawingGroup();
        using (var drawing = exported.Open()) chart.RenderExport(drawing, chart.GetExportLayout(new() { PixelSize = new(960, 540), Dpi = 144 }), Brushes.White);
        AssertSecondaryClip(exported);
        var clock = new Clock(); chart.AnimationTimeProvider = clock; chart.AnimationDuration = TimeSpan.FromSeconds(1);
        chart.AnimationTriggers = ChartAnimationTriggers.Entrance; chart.AnimationEasing = new LinearEasing();
        chart.ReplayEntranceAnimation(); host.Drain(); clock.Advance(TimeSpan.FromMilliseconds(500)); chart.UpdateAnimationFrame(); host.Drain();
        AssertSecondaryClip(TemplateFeaturesHost.Record(chart));

        static void AssertSecondaryClip(Drawing drawing)
        {
            var paths = new List<Geometry[]>();
            Walk(drawing, []);
            var clips = Assert.Single(paths);
            var clip = Assert.Single(clips.OfType<CombinedGeometry>());
            var plot = Assert.IsType<RectangleGeometry>(clip.Geometry1).Rect;
            var excluded = Assert.IsType<RectangleGeometry>(clip.Geometry2).Rect;
            // Independent arithmetic: 10 units compressed to 3 leaves 93 units.
            Assert.Equal(3d / 93, excluded.Height / plot.Height, 10);
            Assert.Equal(1 - 73d / 93, (excluded.Top - plot.Top) / plot.Height, 10);

            void Walk(Drawing item, Geometry[] parents)
            {
                if (item is DrawingGroup group)
                {
                    var next = group.ClipGeometry is { } geometry ? parents.Append(geometry).ToArray() : parents;
                    foreach (var child in group.Children) Walk(child, next);
                }
                else if (item is GeometryDrawing { Pen.Brush: ISolidColorBrush brush } && brush.Color == Colors.Red) paths.Add(parents);
            }
        }
    }

    [AvaloniaTheory]
    [InlineData("value-sort-numeric-category")]
    [InlineData("waterfall-sort")]
    [InlineData("pareto-nondescending")]
    [InlineData("range-category-value")]
    [InlineData("percent-nonzero-baseline")]
    public void Unsupported_comparison_configurations_fail_explicitly(string kind)
    {
        var chart = new CartesianChart { ShowLegend = false };
        ChartSeries series = kind switch
        {
            "value-sort-numeric-category" => new BarSeries { ItemsSource = new ChartPoint[] { new(1, 20) }, SortMode = ChartSeriesSortMode.ValueAscending },
            "waterfall-sort" => new WaterfallSeries { ItemsSource = new ChartPoint[] { new(1, 20) }, SortMode = ChartSeriesSortMode.XAscending },
            "pareto-nondescending" => new ParetoSeries { ItemsSource = new ChartPoint[] { new(1, 20) }, SortMode = ChartSeriesSortMode.Source },
            "range-category-value" => new RangeBarSeries { ItemsSource = new[] { new Band("a", 1, 20, 10) }, CategoryPath = "X", HighPath = "High", LowPath = "Low" },
            _ => new BarSeries { ItemsSource = new ChartPoint[] { new(1, 20) }, StackMode = ChartStackMode.Percent, Baseline = 10 },
        };
        if (kind == "range-category-value") chart.YAxis = new CategoryAxis();
        chart.Series.Add(series);
        Assert.Throws<InvalidOperationException>(() => { chart.Measure(new Size(500, 300)); chart.Arrange(new Rect(0, 0, 500, 300)); _ = chart.PlotArea; });
    }

    private sealed class Clock : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        public void Advance(TimeSpan elapsed) => _ticks += elapsed.Ticks;
    }
}
