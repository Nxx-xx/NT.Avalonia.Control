using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class FinancialSeriesTests
{
    private static readonly DateTimeOffset Friday = new(2026, 10, 16, 16, 0, 0, TimeSpan.Zero);
    private static ChartFinancialPoint[] Data => [new(Friday, 20, 26, 18, 24, 100), new(Friday.AddDays(3), 24, 25, 16, 18, 140), new(Friday.AddDays(4), 18, 23, 17, 18, 120)];

    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public void Named_series_have_registered_defaults_and_keep_display_and_original_values_separate(int kind)
    {
        var series = Create(kind); series.ItemsSource = Data;
        var snapshot = series.CreateSnapshot(); Assert.NotEmpty(snapshot.Points);
        foreach (var item in snapshot.Points)
        {
            Assert.False(item.IsEmpty); var financial = Assert.IsType<ChartFinancialSample>(item.Financial);
            Assert.Equal(financial.Close, item.Point.Y);
            Assert.Equal(financial.SourceEnd, Assert.IsType<ChartFinancialPoint>(item.Item));
            Assert.Equal(financial.SourceEnd.Close, item.RawPoint!.Value.Y);
            Assert.Equal(financial.SourceEnd.Time.ToUnixTimeMilliseconds(), item.RawPoint.Value.X);
            Assert.Equal(financial.SourceEndIndex - financial.SourceStartIndex + 1, financial.Sources.Count);
        }
        Assert.Equal("DatePath", FinancialSeries.DatePathProperty.Name);
        Assert.Same(FinancialSeries.DatePathProperty, FinancialSeries.TimePathProperty);
    }

    [AvaloniaTheory]
    [InlineData(ChartFinancialStyle.Candlestick)] [InlineData(ChartFinancialStyle.Ohlc)] [InlineData(ChartFinancialStyle.HighLow)]
    public void Wick_and_body_hits_keep_original_row_identity_and_full_high_low_bounds(ChartFinancialStyle style)
    {
        var rows = new[] { new Row("a", Friday, 20, 80, 10, 40, 100), new Row("b", Friday.AddDays(3), 60, 90, 30, 50, 120) };
        var series = Mapped(rows); series.Style = style;
        var snapshot = series.CreateSnapshot(); Assert.Same(rows[0], snapshot.Points[0].Item); Assert.Equal("a", snapshot.Points[0].StableId);
        Assert.Equal(new ChartSeriesBounds(-.5, 1.5, 10, 90), series.Renderer.GetBounds(series, snapshot));
        var chart = Fixed(series); var window = Show(chart);
        try
        {
            var hit = Assert.IsType<ChartPointHit>(chart.HitTestPoint(Project(chart, new(0, 75)), 1));
            Assert.Same(rows[0], hit.Item); Assert.Equal("a", hit.StableId); Assert.Equal(0, hit.Index); Assert.Equal(80, hit.Financial!.High);
            Assert.Equal(20, hit.Financial.SourceEnd.Open); Assert.Equal(40, hit.Financial.SourceEnd.Close);
            Assert.Null(chart.HitTestPoint(Project(chart, new(0, 95)), .1));
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public void Every_price_family_paints_nonempty_geometry_and_hits_only_original_financial_metadata(int kind)
    {
        var series = Create(kind); series.ItemsSource = Data;
        var chart = new CartesianChart { ShowLegend = false, XAxis = new NumericAxis(), YAxis = new NumericAxis() }; chart.Series.Add(series);
        var window = Show(chart);
        try
        {
            var group = new DrawingGroup(); using (var drawing = group.Open()) chart.Render(drawing);
            Assert.NotEmpty(Drawings(group).OfType<GeometryDrawing>());
            var data = series.CreateSnapshot(); var found = false;
            foreach (var item in data.Points)
            {
                // P&F symbols have empty centers, so query throughout their true bounding column.
                for (var step = 0; step <= 20; step++)
                {
                    var price = item.Financial!.Low + (item.Financial.High - item.Financial.Low) * step / 20;
                    if (chart.HitTestPoint(Project(chart, new(item.Point.X, price)), 6) is not { } hit) continue;
                    Assert.Same(series, hit.Series); Assert.NotNull(hit.Financial);
                    Assert.Equal(Data[hit.Financial!.SourceEndIndex], Assert.IsType<ChartFinancialPoint>(hit.Item));
                    Assert.Equal(hit.Financial.SourceEnd.Close, hit.RawPoint!.Value.Y); found = true; break;
                }
                if (found) break;
            }
            Assert.True(found);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Doji_body_remains_visible_and_neutral_and_null_volume_is_a_gap()
    {
        var series = new CandlestickSeries { ItemsSource = new[] { new ChartFinancialPoint(Friday, 30, 40, 20, 30) }, NeutralBrush = Brushes.Orange };
        var chart = Fixed(series); var window = Show(chart);
        try
        {
            var group = new DrawingGroup(); using (var drawing = group.Open()) chart.Render(drawing);
            var body = Drawings(group).OfType<GeometryDrawing>().First(drawing => ReferenceEquals(drawing.Brush, Brushes.Orange));
            Assert.True(body.Geometry!.Bounds.Height >= 1);
            Assert.NotNull(chart.HitTestPoint(Project(chart, new(0, 30)), .1));
            var volume = new FinancialVolumeSeries { ItemsSource = series.ItemsSource }.CreateSnapshot(); Assert.True(Assert.Single(volume.Points).IsEmpty);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Several_renko_bricks_from_one_row_have_unique_glyph_indices_and_true_source_ranges()
    {
        var rows = new[] { new Row("a", Friday, 20, 22, 18, 20, 100), new Row("b", Friday.AddDays(3), 20, 30, 19, 30, 150) };
        var series = Mapped(rows); series.Transform = ChartFinancialTransformKind.Renko; series.BoxSize = 2; series.ReversalBoxes = 2;
        var data = series.CreateSnapshot(); Assert.Equal(5, data.Points.Count);
        Assert.Equal(5, data.Points.Select(item => item.StableId).Distinct().Count());
        for (var ordinal = 0; ordinal < data.Points.Count; ordinal++)
        {
            var item = data.Points[ordinal]; Assert.Equal(ordinal, item.SourceIndex); Assert.Same(rows[1], item.Item);
            Assert.Equal(1, item.Financial!.SourceEndIndex); Assert.Equal(30, item.RawPoint!.Value.Y);
            Assert.Equal(22 + ordinal * 2, item.Point.Y);
        }
        Assert.Equal(0, data.Points[0].Financial!.SourceStartIndex);
        Assert.Equal(1, data.Points[1].Financial!.SourceStartIndex);
        series.MaximumGeneratedSamples = 4;
        Assert.Throws<InvalidOperationException>(() => series.CreateSnapshot());
    }

    [AvaloniaFact]
    public void Precomputed_renko_samples_obey_the_same_admission_budget_without_truncation()
    {
        var source = new[] { new ChartFinancialPoint(Friday, 20, 20, 20, 20), new ChartFinancialPoint(Friday.AddDays(3), 30, 30, 30, 30) };
        var samples = ChartFinancialTransforms.Renko(source, 2);
        var series = new RenkoSeries { ItemsSource = samples, MaximumGeneratedSamples = 5 };
        Assert.Equal(5, series.CreateSnapshot().Points.Count);
        series.MaximumGeneratedSamples = 2;
        Assert.Throws<InvalidOperationException>(() => series.CreateSnapshot());
        Assert.Equal(5, samples.Count);
    }

    [AvaloniaFact]
    public void Precomputed_point_and_figure_admission_counts_letters_instead_of_columns()
    {
        var source = new[] { new ChartFinancialPoint(Friday, 20, 20, 20, 20), new ChartFinancialPoint(Friday.AddDays(3), 30, 30, 30, 30) };
        var samples = ChartFinancialTransforms.PointAndFigure(source, 2);
        Assert.Equal(5, Assert.Single(samples).BoxCount);
        var series = new PointAndFigureSeries { ItemsSource = samples, MaximumGeneratedSamples = 5 };
        Assert.Single(series.CreateSnapshot().Points);
        series.MaximumGeneratedSamples = 4;
        Assert.Throws<InvalidOperationException>(() => series.CreateSnapshot());
        Assert.Single(samples);
    }

    [AvaloniaFact]
    public void Invalid_ohlc_is_a_reported_segment_break_and_empty_policy_does_not_invent_prices()
    {
        var series = new HeikinAshiSeries { EmptyPointMode = EmptyPointMode.Interpolate, ItemsSource = new[] {
            Data[0], new ChartFinancialPoint(Friday.AddDays(1), 10, 8, 12, 11), Data[1] } };
        var errors = new List<ChartDataIssue>(); series.DataError += (_, args) => errors.Add(args.Issue);
        var data = series.CreateSnapshot(); Assert.Equal(2, data.Points.Count); Assert.Single(errors);
        Assert.Equal(2, data.Points[1].SourceIndex); Assert.Equal(2, data.Points[1].Point.X);
        Assert.True(data.Points[1].Financial!.IsSegmentStart);
        Assert.Equal((Data[1].Open + Data[1].Close) / 2, data.Points[1].Financial!.Open);
    }

    [AvaloniaTheory]
    [InlineData(true)] [InlineData(false)]
    public void Duplicate_or_disordered_dates_are_rejected_instead_of_silently_reordered(bool duplicate)
    {
        var second = Data[1] with { Time = duplicate ? Friday : Friday.AddDays(-1) };
        Assert.Throws<ArgumentException>(() => new CandlestickSeries { ItemsSource = new[] { Data[0], second } }.CreateSnapshot());
    }

    [AvaloniaTheory]
    [InlineData(true)] [InlineData(false)]
    public void Natural_time_category_value_axes_are_rejected_with_an_explicit_financial_slot_contract(bool naturalTime)
    {
        var chart = new CartesianChart { XAxis = naturalTime ? new DateTimeAxis() : new NumericAxis(), YAxis = naturalTime ? new NumericAxis() : new CategoryAxis() };
        chart.Series.Add(new CandlestickSeries { ItemsSource = Data });
        Assert.Throws<InvalidOperationException>(() => { chart.Measure(new Size(800, 500)); chart.Arrange(new Rect(0, 0, 800, 500)); _ = chart.PlotArea; });
    }

    [AvaloniaFact]
    public void Mapped_paths_collection_and_item_changes_refresh_the_financial_snapshot()
    {
        var row = new Row("a", Friday, 20, 80, 10, 40, 100); var rows = new ObservableCollection<Row> { row };
        var series = Mapped(rows); EventHandler observer = (_, _) => { }; series.Changed += observer;
        try
        {
            var before = series.CreateSnapshot(); row.Close = 55; var after = series.CreateSnapshot();
            Assert.NotSame(before, after); Assert.Equal(55, after.Points[0].Financial!.Close);
            series.ClosePath = "Open"; Assert.Equal(20, series.CreateSnapshot().Points[0].Financial!.Close);
            rows.Add(new("b", Friday.AddDays(3), 30, 40, 20, 35, 50)); Assert.Equal(2, series.CreateSnapshot().Points.Count);
        }
        finally { series.Changed -= observer; }
    }

    [AvaloniaFact]
    public void Host_uses_equally_spaced_trading_dates_and_exact_calendar_mapping_for_overlays()
    {
        var host = new FinancialChart(); host.SetData(Data);
        Assert.Equal(0, host.MapTime(Friday)); Assert.Equal(1, host.MapTime(Friday.AddDays(3))); Assert.Equal(2, host.MapTime(Friday.AddDays(4)));
        Assert.True(double.IsNaN(host.MapTime(Friday.AddDays(1))));
        var overlay = host.ProjectOverlay(new[] { new ChartPoint(Friday.ToUnixTimeMilliseconds(), 25), new ChartPoint(Friday.AddDays(3).ToUnixTimeMilliseconds(), 21), new ChartPoint(Friday.AddDays(1).ToUnixTimeMilliseconds(), 20) });
        Assert.Equal(new ChartPoint(0, 25), overlay[0]); Assert.Equal(new ChartPoint(1, 21), overlay[1]); Assert.True(double.IsNaN(overlay[2].X));
        host.Series.Add(new LineSeries { ItemsSource = overlay, Title = "共享交易日叠加" });
        var window = Show(host);
        try
        {
            var first = Project(host.Chart, new(0, 20)); var second = Project(host.Chart, new(1, 20)); var third = Project(host.Chart, new(2, 20));
            Assert.Equal(second.X - first.X, third.X - second.X, 8);
            Assert.NotSame(host.PriceAxis, host.VolumeAxis); Assert.Equal("10-19", host.TradingAxis.Labels![1]);
            Assert.True(host.TryGetTime(1, out var time)); Assert.Equal(Friday.AddDays(3), time);
            Assert.True(host.VolumeSeries.IsVisible); Assert.Equal(host.Samples, host.PriceSeries.ItemsSource); Assert.Equal(host.Samples, host.VolumeSeries.ItemsSource);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Host_default_trading_axis_keeps_visible_date_labels_at_their_matching_slots()
    {
        var rows = Data.Append(new ChartFinancialPoint(Friday.AddDays(5), 18, 25, 16, 22, 130)).ToArray();
        var host = new FinancialChart(); host.PriceSeries.MaximumGeneratedSamples = 110000; host.SetData(rows);
        Assert.Equal(110000, host.VolumeSeries.MaximumGeneratedSamples);
        Assert.True(host.TradingAxis.ShowLabels);
        Assert.Equal(ChartAxisLabelMode.Auto, host.TradingAxis.LabelMode);
        Assert.Equal(CategoryPlotMode.OnTicks, host.TradingAxis.PlotMode);
        Assert.Equal(new[] { "10-16", "10-19", "10-20", "10-21" }, host.TradingAxis.Labels);
        for (var index = 0; index < rows.Length; index++)
        {
            Assert.Equal(index, host.MapTime(rows[index].Time));
            Assert.Equal(index, host.Samples[index].Slot);
            Assert.True(host.TryGetTime(index, out var time)); Assert.Equal(rows[index].Time, time);
        }
        var window = Show(host);
        try
        {
            var layout = Assert.IsType<ChartAxisLayout>(host.Chart.GetAxisLayout(host.TradingAxis));
            Assert.True(layout.LabelHeight > 0);
            Assert.True(layout.Bounds.Height > 0);
            var drawing = new DrawingGroup(); using (var context = drawing.Open()) host.Chart.Render(context);
            Assert.NotEmpty(Drawings(drawing).OfType<GlyphRunDrawing>());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Fractional_millisecond_overlay_dates_are_gaps_and_precise_MapTime_retains_submillisecond_identity()
    {
        var host = new FinancialChart(); host.SetData(Data);
        var fractional = host.ProjectOverlay(new[] { new ChartPoint(Friday.ToUnixTimeMilliseconds() + .5, 25) });
        Assert.True(double.IsNaN(Assert.Single(fractional).X));
        var precise = Friday.AddTicks(5000);
        host.SetData(new[] { Data[0] with { Time = precise } });
        Assert.Equal(0, host.MapTime(precise)); Assert.True(double.IsNaN(host.MapTime(Friday)));
        Assert.True(double.IsNaN(Assert.Single(host.ProjectOverlay(new[] { new ChartPoint(Friday.ToUnixTimeMilliseconds(), 25) })).X));
    }

    [AvaloniaTheory]
    [InlineData(ChartFinancialTransformKind.Renko)] [InlineData(ChartFinancialTransformKind.Kagi)] [InlineData(ChartFinancialTransformKind.PointAndFigure)]
    public void Non_time_host_transforms_hide_non_aggregated_volume_and_map_dates_to_final_emitted_slot(ChartFinancialTransformKind kind)
    {
        var host = new FinancialChart(); host.SetData(Data); host.ApplyTransform(kind);
        Assert.False(host.VolumeSeries.IsVisible); Assert.NotEmpty(host.Samples);
        foreach (var group in host.Samples.GroupBy(sample => sample.Time)) Assert.Equal(group.Last().Slot, host.MapTime(group.Key));
        host.ApplyTransform(ChartFinancialTransformKind.Raw); Assert.True(host.VolumeSeries.IsVisible); Assert.Equal(3, host.Samples.Count);
    }

    [AvaloniaFact]
    public void Zoom_selection_and_export_recording_share_the_same_financial_geometry_and_identity()
    {
        var series = new CandlestickSeries { ItemsSource = Data };
        var chart = Fixed(series); chart.SelectionMode = ChartSelectionMode.Multiple;
        var window = Show(chart);
        try
        {
            chart.Viewport = new ChartViewport(-.25, 1.25, 10, 40);
            var selected = chart.SelectRectangle(chart.PlotArea);
            Assert.Equal(2, selected.SelectedCount);
            Assert.All(chart.SelectedPoints!, hit => Assert.NotNull(hit.Financial));
            var drawing = new DrawingGroup(); using (var context = drawing.Open()) chart.Render(context);
            var point = Project(chart, new(1, 18)); Assert.Equal(1, chart.HitTestPoint(point, 1)!.Index);
            var layout = chart.GetExportLayout(new() { PixelSize = new(400, 300), Background = Brushes.White });
            var exported = new DrawingGroup(); using (var context = exported.Open()) chart.RenderExport(context, layout, Brushes.White);
            Assert.NotEmpty(Drawings(exported).OfType<GeometryDrawing>());
            Assert.Equal(new PixelSize(400, 300), layout.Pixels);
            Assert.Equal(new ChartViewport(-.25, 1.25, 10, 40), chart.Viewport);
        }
        finally { window.Close(); }
    }

    private static FinancialSeries Create(int kind) => kind switch { 0 => new CandlestickSeries(), 1 => new OhlcSeries(), 2 => new HiloSeries(), 3 => new HeikinAshiSeries(),
        4 => new RenkoSeries { BoxSize = 2, ReversalBoxes = 2 }, 5 => new KagiSeries { ReversalAmount = 2 }, _ => new PointAndFigureSeries { BoxSize = 2, ReversalBoxes = 2 } };
    private static FinancialSeries Mapped(IEnumerable<Row> source) => new CandlestickSeries { ItemsSource = source, DatePath = "Time", OpenPath = "Open", HighPath = "High", LowPath = "Low", ClosePath = "Close", VolumePath = "Volume", IdPath = "Id" };
    private static CartesianChart Fixed(ChartSeries series)
    {
        var chart = new CartesianChart { ShowLegend = false, XAxis = new NumericAxis { Minimum = -.5, Maximum = 2.5 }, YAxis = new NumericAxis { Minimum = 0, Maximum = 100 } };
        chart.Series.Add(series); return chart;
    }
    private static Window Show(Control control)
    {
        var window = new Window { Width = 900, Height = 550, Content = control };
        window.Show(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); if (control is CartesianChart chart) _ = chart.PlotArea; return window;
    }
    private static Point Project(CartesianChart chart, ChartPoint point)
    {
        var range = chart.VisibleRange!.Value; var plot = chart.PlotArea;
        return new(plot.Left + (point.X - range.XMinimum) / (range.XMaximum - range.XMinimum) * plot.Width,
            plot.Bottom - (point.Y - range.YMinimum) / (range.YMaximum - range.YMinimum) * plot.Height);
    }
    private static IEnumerable<Drawing> Drawings(Drawing drawing)
    {
        yield return drawing;
        if (drawing is DrawingGroup group) foreach (var child in group.Children) foreach (var descendant in Drawings(child)) yield return descendant;
    }
    private sealed class Row(string id, DateTimeOffset time, double open, double high, double low, double close, double volume) : INotifyPropertyChanged
    {
        private double _close = close;
        public string Id => id;
        public DateTimeOffset Time => time;
        public double Open => open;
        public double High => high;
        public double Low => low;
        public double Close { get => _close; set { _close = value; PropertyChanged?.Invoke(this, new(nameof(Close))); } }
        public double Volume => volume;
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
