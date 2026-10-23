using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class StatisticsSeriesTests
{
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Automatic_category_slots_keep_first_and_last_box_bodies_and_error_caps_inside_plot(bool errorBars, bool horizontal)
    {
        var brush = new SolidColorBrush(Color.FromRgb(19, 88, 177));
        StatisticalSeries series = errorBars ? new ErrorBarSeries
        {
            ItemsSource = new[] { new { Name = "A", Value = 50d, Error = 15d }, new { Name = "B", Value = 50d, Error = 15d }, new { Name = "C", Value = 50d, Error = 15d } },
            CategoryPath = "Name", ValuePath = "Value", ErrorPath = "Error", CapWidth = 20,
        } : new BoxPlotSeries
        {
            ItemsSource = new[] { new Group("a", "A", [20, 30, 50, 70, 80]), new Group("b", "B", [20, 30, 50, 70, 80]), new Group("c", "C", [20, 30, 50, 70, 80]) },
            CategoryPath = "Name", SamplesPath = "Samples",
        };
        series.Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical;
        series.Stroke = brush; series.Fill = brush;
        var category = new CategoryAxis();
        var value = new NumericAxis { Minimum = 0, Maximum = 100 };
        var chart = new CartesianChart { XAxis = horizontal ? value : category, YAxis = horizontal ? category : value, ShowLegend = false };
        chart.Series.Add(series); var window = Show(chart);
        try
        {
            var range = chart.VisibleRange!.Value; var plot = chart.PlotArea;
            Assert.Equal(-.5, horizontal ? range.YMinimum : range.XMinimum);
            Assert.Equal(2.5, horizontal ? range.YMaximum : range.XMaximum);
            var group = new DrawingGroup();
            using (var drawing = group.Open()) chart.Render(drawing);
            var glyphs = Drawings(group).OfType<GeometryDrawing>()
                .Where(drawing => ReferenceEquals(drawing.Brush, brush) || ReferenceEquals(drawing.Pen?.Brush, brush)).ToArray();
            Assert.NotEmpty(glyphs);
            foreach (var glyph in glyphs)
            {
                var bounds = glyph.GetBounds();
                Assert.True(horizontal ? bounds.Top >= plot.Top && bounds.Bottom <= plot.Bottom :
                    bounds.Left >= plot.Left && bounds.Right <= plot.Right, $"Statistical geometry {bounds} exceeds plot {plot}.");
            }
            foreach (var index in new[] { 0, 2 })
            {
                var center = Project(chart, horizontal ? new(50, index) : new(index, 50));
                Assert.Equal(index, chart.HitTestPoint(center, .1)!.Index);
                if (errorBars)
                {
                    var cap = Project(chart, horizontal ? new(65, index) : new(index, 65));
                    foreach (var sign in new[] { -1, 1 })
                        Assert.Equal(index, chart.HitTestPoint(cap + (horizontal ? new Vector(0, sign * 9) : new Vector(sign * 9, 0)), .1)!.Index);
                }
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(CategoryPlotMode.OnTicks, .25, -.5, 2.5)]
    [InlineData(CategoryPlotMode.BetweenTicks, 0, -.5, 2.5)]
    [InlineData(CategoryPlotMode.OnTicks, .75, -.75, 2.75)]
    public void Statistical_category_padding_supplements_existing_plot_mode_and_gap_once(CategoryPlotMode mode, double gap, double expectedMinimum, double expectedMaximum)
    {
        var series = new ErrorBarSeries { ItemsSource = new[] { ChartStatistics.SymmetricError(50, 10), ChartStatistics.SymmetricError(50, 10), ChartStatistics.SymmetricError(50, 10) } };
        var chart = new CartesianChart { XAxis = new CategoryAxis { PlotMode = mode, Gap = gap }, YAxis = new NumericAxis { Minimum = 0, Maximum = 100 } };
        chart.Series.Add(series); var window = Show(chart);
        try { Assert.Equal(expectedMinimum, chart.VisibleRange!.Value.XMinimum); Assert.Equal(expectedMaximum, chart.VisibleRange!.Value.XMaximum); }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Statistical_category_slot_padding_preserves_explicit_category_and_numeric_ranges(bool categorical)
    {
        ChartAxis axis = categorical ? new CategoryAxis { Minimum = 0, Maximum = 2 } : new NumericAxis { Minimum = 0, Maximum = 2 };
        var chart = new CartesianChart { XAxis = axis, YAxis = new NumericAxis { Minimum = 0, Maximum = 100 } };
        chart.Series.Add(new BoxPlotSeries { ItemsSource = new[] { new double[] { 20, 50, 80 }, new double[] { 20, 50, 80 }, new double[] { 20, 50, 80 } } });
        var window = Show(chart);
        try { Assert.Equal(0, chart.VisibleRange!.Value.XMinimum); Assert.Equal(2, chart.VisibleRange!.Value.XMaximum); }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Empty_statistical_series_does_not_expand_a_peer_line_category_range()
    {
        var chart = new CartesianChart { XAxis = new CategoryAxis(), YAxis = new NumericAxis { Minimum = 0, Maximum = 100 } };
        chart.Series.Add(new LineSeries { ItemsSource = new ChartPoint[] { new(0, 20), new(1, 50), new(2, 80) } });
        chart.Series.Add(new ErrorBarSeries());
        var window = Show(chart);
        try { Assert.Equal(0, chart.VisibleRange!.Value.XMinimum); Assert.Equal(2, chart.VisibleRange!.Value.XMaximum); }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Box_samples_and_preaggregated_rows_keep_group_identity_and_full_outlier_bounds()
    {
        var row = new Group("batch-a", "A", [1, 2, 3, 4, 100]);
        var series = new BoxPlotSeries { ItemsSource = new[] { row }, CategoryPath = "Name", IdPath = "Id", SamplesPath = "Samples" };
        var data = series.CreateSnapshot();
        var item = Assert.Single(data.Points);
        Assert.Same(row, item.Item); Assert.Equal("batch-a", item.StableId); Assert.Equal(0, item.SourceIndex);
        Assert.Equal(3, item.Point.Y); Assert.Equal(4, Assert.Single(item.BoxPlot!.Outliers).OriginalIndex);
        Assert.Equal(new ChartSeriesBounds(0, 0, 1, 100), series.Renderer.GetBounds(series, data));
        var summary = item.BoxPlot;
        var aggregated = new BoxPlotSeries { ItemsSource = new[] { summary } }.CreateSnapshot().Points[0];
        Assert.Same(summary, aggregated.Item); Assert.Same(summary, aggregated.BoxPlot); Assert.False(aggregated.IsEmpty);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Box_whiskers_outliers_and_zero_spread_hit_the_original_group_in_either_orientation(bool horizontal)
    {
        var row = new Group("a", "A", [1, 2, 3, 4, 100]);
        var series = new BoxPlotSeries { ItemsSource = new[] { row }, SamplesPath = "Samples", IdPath = "Id", Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical, ShowMean = true };
        var chart = Fixed(series, horizontal, 0, 110);
        var window = Show(chart);
        try
        {
            var point = horizontal ? new ChartPoint(100, 0) : new ChartPoint(0, 100);
            var hit = Assert.IsType<ChartPointHit>(chart.HitTestPoint(Project(chart, point), 1));
            Assert.Same(row, hit.Item); Assert.Equal("a", hit.StableId); Assert.Equal(0, hit.Index);
            row.Samples = [7, 7, 7]; series.Refresh();
            point = horizontal ? new(7, 0) : new(0, 7);
            Assert.NotNull(chart.HitTestPoint(Project(chart, point), 1));
            using var drawing = new DrawingGroup().Open(); chart.Render(drawing);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(false, ChartErrorMode.Both, 40, 80)]
    [InlineData(true, ChartErrorMode.Both, 40, 80)]
    [InlineData(false, ChartErrorMode.Plus, 50, 80)]
    [InlineData(true, ChartErrorMode.Minus, 40, 50)]
    public void Asymmetric_error_caps_modes_and_orientation_retain_original_identity(bool horizontal, ChartErrorMode mode, double low, double high)
    {
        var row = new ErrorRow("err-a", 50, 10, 30);
        var series = new ErrorBarSeries { ItemsSource = new[] { row }, ValuePath = "Center", IdPath = "Id",
            LowErrorPath = "Low", HighErrorPath = "High", ErrorMode = mode, CapWidth = 20,
            Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical };
        Assert.False(series.ShowMarkers); Assert.Equal(series.LowerErrorPath, series.LowErrorPath); Assert.Equal(20, series.CapSize);
        var mapped = Assert.Single(series.CreateSnapshot().Points);
        Assert.Equal(low, mapped.Low); Assert.Equal(high, mapped.High);
        var chart = Fixed(series, horizontal, 0, 100); var window = Show(chart);
        try
        {
            var cap = Project(chart, horizontal ? new(high, 0) : new(0, high));
            cap += horizontal ? new Vector(0, 8) : new Vector(8, 0);
            var hit = Assert.IsType<ChartPointHit>(chart.HitTestPoint(cap, .1));
            Assert.Same(row, hit.Item); Assert.Equal("err-a", hit.StableId);
            Assert.Equal(0, hit.Index);
            if (mode == ChartErrorMode.Plus)
                Assert.Null(chart.HitTestPoint(Project(chart, horizontal ? new(40, 0) : new(0, 40)), .1));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Symmetric_and_direct_interval_mapping_agree_and_aliases_share_one_property()
    {
        var series = new ErrorBarSeries { ItemsSource = new[] { new { Value = 12d, Error = 3d } }, ValuePath = "Value", ErrorPath = "Error" };
        var symmetric = series.CreateSnapshot().Points[0];
        var direct = new ErrorBarSeries { ItemsSource = new[] { ChartStatistics.SymmetricError(12, 3) } }.CreateSnapshot().Points[0];
        Assert.Equal(symmetric.Point, direct.Point); Assert.Equal(9, direct.Low); Assert.Equal(15, direct.High);
        series.LowErrorPath = "A"; Assert.Equal("A", series.LowerErrorPath);
        series.UpperErrorPath = "B"; Assert.Equal("B", series.HighErrorPath);
        series.CapWidth = 27; Assert.Equal(27, series.CapSize);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Unequal_histogram_bins_use_their_numeric_edges_and_return_original_bin_items(bool horizontal)
    {
        var bins = ChartStatistics.HistogramByBoundaries([.5, 1.5, 2.5, 3.5], [0, 1, 4], ChartHistogramNormalization.Count).Bins;
        var series = new HistogramSeries { ItemsSource = bins, BinGapRatio = 0, Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical };
        var chart = new CartesianChart
        {
            XAxis = new NumericAxis { Minimum = 0, Maximum = horizontal ? 5 : 4 },
            YAxis = new NumericAxis { Minimum = 0, Maximum = horizontal ? 4 : 5 },
        };
        chart.Series.Add(series); var window = Show(chart);
        try
        {
            var hit = Assert.IsType<ChartPointHit>(chart.HitTestPoint(Project(chart, horizontal ? new(2, 3.5) : new(3.5, 2)), .1));
            Assert.Equal(1, hit.Index); Assert.Equal(bins[1], Assert.IsType<ChartHistogramBin>(hit.Item));
            chart.Viewport = horizontal ? new(0, 5, 2, 4) : new(2, 4, 0, 5);
            Assert.Equal(1, chart.HitTestPoint(Project(chart, horizontal ? new(2, 3.5) : new(3.5, 2)), .1)!.Index);
            using var drawing = new DrawingGroup().Open(); chart.Render(drawing);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Histogram_bounds_include_edges_and_density_rendering_does_not_replace_source_samples()
    {
        var histogram = new HistogramSeries { ItemsSource = new[] { new ChartHistogramBin(1, 8, 4, .5) } };
        Assert.Equal(new ChartSeriesBounds(1, 8, 0, .5), histogram.Renderer.GetBounds(histogram, histogram.CreateSnapshot()));
        var rows = new[] { new DensityRow("a", 1, .2), new DensityRow("b", 3, .6) };
        var density = new DensitySeries { ItemsSource = rows, CategoryPath = "X", ValuePath = "Y", IdPath = "Id" };
        var data = density.CreateSnapshot();
        var projection = new TestProjection();
        var context = new ChartSeriesRenderContext(density, data, projection, Brushes.Blue);
        using var drawing = new DrawingGroup().Open(); density.Renderer.Render(drawing, context);
        var between = projection.Project(new(2.5, .5));
        var hit = density.Renderer.HitTest(context, between, .01);
        Assert.NotNull(hit); Assert.Equal(1, hit.SourceIndex);
        Assert.Same(rows[1], data.Points[hit.SourceIndex].Item); Assert.Equal("b", data.Points[hit.SourceIndex].StableId);
        Assert.Equal(new ChartSeriesBounds(1, 3, 0, .6), density.Renderer.GetBounds(density, data));
    }

    [AvaloniaFact]
    public void Direct_histogram_bins_respect_explicit_value_path_and_custom_boundaries_map_original_rows()
    {
        var bin = new ChartHistogramBin(0, 1, 4, .2);
        var direct = new HistogramSeries { ItemsSource = new[] { bin }, ValuePath = "Count" }.CreateSnapshot().Points[0];
        Assert.Equal(4, direct.Point.Y); Assert.Equal(0, direct.Low); Assert.Equal(1, direct.High);
        var row = new { Id = "bin-a", Left = 2d, Right = 5d, Height = .25 };
        var custom = new HistogramSeries { ItemsSource = new[] { row }, IdPath = "Id", LowerBoundPath = "Left", UpperBoundPath = "Right", ValuePath = "Height" }.CreateSnapshot().Points[0];
        Assert.Equal(new ChartPoint(3.5, .25), custom.Point); Assert.Equal("bin-a", custom.StableId); Assert.Same(row, custom.Item);
    }

    [AvaloniaFact]
    public void Density_hit_clips_huge_finite_crossing_endpoints_before_distance_arithmetic()
    {
        var series = new DensitySeries { ItemsSource = new ChartPoint[] { new(-1e200, .5), new(1e200, .5) } };
        var context = new ChartSeriesRenderContext(series, series.CreateSnapshot(), new TestProjection(), Brushes.Blue);
        var hit = series.Renderer.HitTest(context, new(200, 150), .01);
        Assert.NotNull(hit); Assert.Equal(new Point(200, 150), hit.Position); Assert.Equal(0, hit.DistanceSquared);
        Assert.InRange(hit.SourceIndex, 0, 1);
    }

    [AvaloniaFact]
    public void Buffered_histogram_centers_are_numeric_edges_and_invalid_statistical_values_remain_gaps()
    {
        var buffer = new ChartDataBuffer<ChartHistogramBin>(2);
        buffer.AppendRange([new(10, 20, 1, 1), new(20, 40, 2, 2)]);
        var series = new HistogramSeries { ItemsSource = buffer, EmptyPointMode = EmptyPointMode.Zero };
        EventHandler changed = (_, _) => { }; series.Changed += changed;
        try
        {
            var previous = series.CreateSnapshot(); var identity = previous.Points[1].StableId;
            Assert.Equal(15, previous.Points[0].Point.X); Assert.Equal(30, previous.Points[1].Point.X);
            buffer.AppendRange([new(40, 40, 1, 1)]);
            var current = series.CreateSnapshot();
            Assert.Equal(30, current.Points[0].Point.X); Assert.Same(identity, current.Points[0].StableId);
            Assert.Equal(0, current.Points[0].SourceIndex); Assert.True(current.Points[1].IsEmpty);
        }
        finally { series.Changed -= changed; }
    }

    [AvaloniaTheory]
    [InlineData("box")]
    [InlineData("error")]
    [InlineData("histogram")]
    [InlineData("density")]
    public void Invalid_statistics_report_a_gap_without_empty_point_imputation(string kind)
    {
        ChartSeries series = kind switch
        {
            "box" => new BoxPlotSeries { ItemsSource = new[] { Array.Empty<double>() } },
            "error" => new ErrorBarSeries { ItemsSource = new[] { new { Value = 10d, Error = -2d } }, ValuePath = "Value", ErrorPath = "Error" },
            "histogram" => new HistogramSeries { ItemsSource = new[] { new ChartHistogramBin(2, 1, 3, 3) } },
            _ => new DensitySeries { ItemsSource = new ChartPoint[] { new(0, -1) } },
        };
        series.EmptyPointMode = EmptyPointMode.Zero;
        var errors = new List<ChartDataIssue>(); series.DataError += (_, error) => errors.Add(error.Issue);
        var snapshot = series.CreateSnapshot();
        Assert.True(Assert.Single(snapshot.Points).IsEmpty); Assert.NotEmpty(snapshot.Issues); Assert.NotEmpty(errors);
        Assert.Null(series.Renderer.GetBounds(series, snapshot));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Unrepresentable_finite_statistics_report_data_errors_without_crashing_layout(bool errorBar)
    {
        ChartSeries series = errorBar ? new ErrorBarSeries
        {
            ItemsSource = new[] { new { Value = double.MaxValue, Error = double.MaxValue } },
            ValuePath = "Value", ErrorPath = "Error",
        } : new BoxPlotSeries { ItemsSource = new[] { new[] { -double.MaxValue, double.MaxValue } } };
        var errors = new List<ChartDataIssue>(); series.DataError += (_, error) => errors.Add(error.Issue);
        var snapshot = series.CreateSnapshot();
        Assert.True(snapshot.Points[0].IsEmpty); Assert.NotEmpty(snapshot.Issues); Assert.NotEmpty(errors);
        var chart = Fixed(series, false, 0, 100); var window = Show(chart);
        try { using var drawing = new DrawingGroup().Open(); chart.Render(drawing); }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Changing_sample_mapping_and_observable_group_rebuilds_statistics_and_selection_identity()
    {
        var row = new Group("a", "A", [1, 2, 3]);
        var rows = new ObservableCollection<Group> { row };
        var series = new BoxPlotSeries { ItemsSource = rows, SamplesPath = "Samples", IdPath = "Id" };
        var chart = Fixed(series, false, 0, 100); var window = Show(chart);
        try
        {
            chart.SelectedPoint = Assert.IsType<ChartPointHit>(chart.HitTestPoint(Project(chart, new(0, 2)), 1));
            row.Samples = [20, 30, 40];
            _ = chart.PlotArea;
            Assert.Equal(30, series.CreateSnapshot().Points[0].BoxPlot!.Median);
            Assert.Equal("a", chart.SelectedPoint!.StableId); Assert.Equal(30, chart.SelectedPoint.Point.Y);
            var before = series.SourceVersion; series.SamplesPath = "Alternate";
            Assert.True(series.SourceVersion > before);
            Assert.Equal(8, series.CreateSnapshot().Points[0].BoxPlot!.Median);
            rows.Clear(); _ = chart.PlotArea; Assert.Null(chart.SelectedPoint);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData("box")]
    [InlineData("error")]
    [InlineData("histogram")]
    [InlineData("density")]
    public void Secondary_axes_zoom_retained_replay_and_export_preserve_original_data(string kind)
    {
        ChartSeries series = kind switch
        {
            "box" => new BoxPlotSeries { ItemsSource = new[] { new double[] { 20, 40, 60, 80, 100 } } },
            "error" => new ErrorBarSeries { ItemsSource = new[] { ChartStatistics.ErrorInterval(60, 20, 30) } },
            "histogram" => new HistogramSeries { ItemsSource = new[] { new ChartHistogramBin(-.25, .25, 80, 80) } },
            _ => new DensitySeries { ItemsSource = new ChartPoint[] { new(-.25, 40), new(0, 80), new(.25, 40) } },
        };
        series.YAxisKey = "detail";
        var chart = new CartesianChart { XAxis = new NumericAxis { Minimum = -.5, Maximum = .5 }, YAxis = new NumericAxis { Minimum = 0, Maximum = 10 } };
        chart.Axes.Add(new NumericAxis { Key = "detail", Minimum = 0, Maximum = 100, Position = ChartAxisPosition.Far });
        chart.Series.Add(series); var window = Show(chart);
        try
        {
            var original = series.CreateSnapshot().Points.Select(point => (point.SourceIndex, point.StableId, point.Item)).ToArray();
            using (var drawing = new DrawingGroup().Open()) { chart.Render(drawing); chart.Render(drawing); }
            Assert.NotNull(chart.HitTestPoint(new(chart.PlotArea.Center.X, chart.PlotArea.Bottom - chart.PlotArea.Height * .8), 3));
            chart.Viewport = new(-.25, .25, 2, 9);
            var viewport = chart.Viewport; var plot = chart.PlotArea;
            var layout = chart.GetExportLayout(new() { PixelSize = new(1200, 800), Dpi = 144 });
            using (var drawing = new DrawingGroup().Open()) chart.RenderExport(drawing, layout, Brushes.White);
            Assert.Equal(viewport, chart.Viewport); Assert.Equal(plot, chart.PlotArea);
            Assert.Equal(original, series.CreateSnapshot().Points.Select(point => (point.SourceIndex, point.StableId, point.Item)).ToArray());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Histogram_rejects_categorical_bin_boundaries()
    {
        var chart = new CartesianChart { XAxis = new CategoryAxis() };
        chart.Series.Add(new HistogramSeries { ItemsSource = new[] { new ChartHistogramBin(0, 1, 1, 1) } });
        Assert.Throws<InvalidOperationException>(() =>
        {
            chart.Measure(new(800, 500)); chart.Arrange(new Rect(0, 0, 800, 500)); _ = chart.PlotArea;
        });
    }

    private static CartesianChart Fixed(ChartSeries series, bool horizontal, double minimum, double maximum)
    {
        var chart = new CartesianChart
        {
            XAxis = new NumericAxis { Minimum = horizontal ? minimum : -.5, Maximum = horizontal ? maximum : .5 },
            YAxis = new NumericAxis { Minimum = horizontal ? -.5 : minimum, Maximum = horizontal ? .5 : maximum },
        };
        chart.Series.Add(series); return chart;
    }
    private static Point Project(CartesianChart chart, ChartPoint point)
    {
        var range = chart.VisibleRange!.Value; var plot = chart.PlotArea;
        return new(plot.Left + (point.X - range.XMinimum) / (range.XMaximum - range.XMinimum) * plot.Width,
            plot.Bottom - (point.Y - range.YMinimum) / (range.YMaximum - range.YMinimum) * plot.Height);
    }
    private static Window Show(CartesianChart chart)
    {
        var window = new Window { Width = 900, Height = 550, Content = chart };
        window.Show(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); _ = chart.PlotArea; return window;
    }
    private sealed record ErrorRow(string Id, double Center, double Low, double High);
    private sealed record DensityRow(string Id, double X, double Y);
    private sealed class Group(string id, string name, double[] samples) : INotifyPropertyChanged
    {
        private double[] _samples = samples;
        public string Id => id;
        public string Name => name;
        public double[] Samples { get => _samples; set { _samples = value; PropertyChanged?.Invoke(this, new(nameof(Samples))); } }
        public double[] Alternate => [7, 8, 9];
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    private sealed class TestProjection : IChartProjection
    {
        public Rect PlotArea => new(0, 0, 400, 300);
        public double YOrigin => 300;
        public Point Project(ChartPoint point) => new(MapX(point.X), MapY(point.Y));
        public ChartPoint Unproject(Point position) => new(position.X / 100, (300 - position.Y) / 300);
        public double MapX(double value) => value * 100;
        public double MapY(double value) => 300 - value * 300;
    }
    private static IEnumerable<Drawing> Drawings(Drawing drawing)
    {
        yield return drawing;
        if (drawing is DrawingGroup group)
            foreach (var child in group.Children)
                foreach (var nested in Drawings(child)) yield return nested;
    }
}
