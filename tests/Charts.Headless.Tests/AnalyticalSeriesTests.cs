using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class AnalyticalSeriesTests
{
    [AvaloniaFact]
    public void Existing_public_snapshot_entry_remains_nonvirtual_and_dispatches_calculated_series()
    {
        var existingMethod = typeof(ChartSeries).GetMethod(nameof(ChartSeries.CreateSnapshot))!;
        Assert.False(existingMethod.IsVirtual);
        Assert.Equal(typeof(ChartSeries), typeof(MovingAverageSeries).GetMethod(nameof(ChartSeries.CreateSnapshot))!.DeclaringType);
        ChartSeries series = new MovingAverageSeries { ItemsSource = new[] { 2d, 4d, 8d }, Period = 2 };
        var data = series.CreateSnapshot();
        Assert.True(data.Points[0].IsEmpty); Assert.Equal(6, data.Points[^1].Point.Y);
        Assert.False(data.Points[^1].Analysis!.IsSynthetic);
    }

    [AvaloniaFact]
    public void Moving_average_preserves_warmup_gaps_and_original_endpoint_provenance()
    {
        var rows = new[] { new Row("a", 2), new Row("b", 4), new Row("c", double.NaN), new Row("d", 10), new Row("e", 20) };
        var source = Source(new TrackedSource(rows));
        var average = new MovingAverageSeries { SourceSeries = source, Period = 2 };
        var data = average.CreateSnapshot();
        Assert.Equal(5, data.Points.Count);
        Assert.Equal(new[] { true, false, true, true, false }, data.Points.Select(point => point.IsEmpty));
        Assert.Equal(3, data.Points[1].Point.Y); Assert.Equal(15, data.Points[4].Point.Y);
        Assert.Same(rows[4], data.Points[4].Item); Assert.Equal("e", data.Points[4].StableId); Assert.Equal(4, data.Points[4].SourceIndex);
        Assert.Equal(new ChartPoint(4, 20), data.Points[4].RawPoint);
        var provenance = data.Points[4].Analysis!;
        Assert.False(provenance.IsSynthetic); Assert.Same(source, provenance.SourceSeries); Assert.Same(rows[4], provenance.SourceItem);
        Assert.Equal(new ChartPoint(4, 15), provenance.CalculatedPoint);
    }

    [AvaloniaTheory]
    [InlineData(ChartMovingAverageType.Simple)]
    [InlineData(ChartMovingAverageType.Exponential)]
    [InlineData(ChartMovingAverageType.Weighted)]
    [InlineData(ChartMovingAverageType.Triangular)]
    public void Independent_moving_average_uses_its_own_mapping_and_selected_convention(ChartMovingAverageType type)
    {
        var series = new MovingAverageSeries { ItemsSource = new[] { new Row("a", 1), new Row("b", 2), new Row("c", 4), new Row("d", 10) }, ValuePath = "Value", Period = 3, Type = type };
        // SMA=(2+4+10)/3, EMA seed=7/3 then alpha=.5, WMA=(2+8+30)/6, TMA=(1+2*2+4)/4 then (2+2*4+10)/4.
        var expected = type switch { ChartMovingAverageType.Simple => 16d / 3, ChartMovingAverageType.Exponential => 37d / 6, ChartMovingAverageType.Weighted => 20d / 3, _ => 5 };
        var data = series.CreateSnapshot();
        Assert.Equal(expected, data.Points[^1].Point.Y, 10); Assert.True(data.Points[0].IsEmpty); Assert.True(data.Points[1].IsEmpty);
        Assert.Equal(4, data.Points.Count);
    }

    [AvaloniaFact]
    public void Source_append_item_change_replace_and_parameter_change_recompute_without_mutating_previous_snapshots()
    {
        var rows = new TrackedSource(new Row("a", 2), new Row("b", 4));
        var source = Source(rows);
        var average = new MovingAverageSeries { SourceSeries = source, Period = 2 };
        EventHandler owner = (_, _) => { };
        average.Changed += owner;
        try
        {
            var first = average.CreateSnapshot(); Assert.Equal(3, first.Points[^1].Point.Y);
            rows.Append(new Row("c", 12)); Assert.Equal(8, average.CreateSnapshot().Points[^1].Point.Y);
            rows.Rows[2].Value = 20; Assert.Equal(12, average.CreateSnapshot().Points[^1].Point.Y);
            average.Period = 3; Assert.Equal(26d / 3, average.CreateSnapshot().Points[^1].Point.Y, 10);
            var replacement = new TrackedSource(new Row("x", 30), new Row("y", 60), new Row("z", 90));
            source.ItemsSource = replacement;
            Assert.Equal(0, rows.SubscriberCount); Assert.All(rows.Rows, row => Assert.Equal(0, row.SubscriberCount));
            Assert.Equal(60, average.CreateSnapshot().Points[^1].Point.Y);
            Assert.Equal(3, first.Points[^1].Point.Y);
        }
        finally { average.Changed -= owner; }
    }

    [AvaloniaFact]
    public void Source_replacement_and_final_owner_release_retire_observers_while_hidden_series_keep_history_current()
    {
        var firstRows = new TrackedSource(new Row("a", 1), new Row("b", 3));
        var secondRows = new TrackedSource(new Row("x", 10), new Row("y", 20));
        var average = new EMAIndicator { SourceSeries = Source(firstRows), Period = 2 };
        EventHandler owner = (_, _) => { };
        average.Changed += owner;
        Assert.Equal(1, firstRows.SubscriberCount);
        average.SourceSeries = Source(secondRows);
        Assert.Equal(0, firstRows.SubscriberCount); Assert.All(firstRows.Rows, row => Assert.Equal(0, row.SubscriberCount));
        Assert.Equal(1, secondRows.SubscriberCount);
        var calculations = average.ComputationCount; var version = average.SourceVersion;
        average.IsVisible = false;
        Assert.Equal(1, secondRows.SubscriberCount); Assert.All(secondRows.Rows, row => Assert.Equal(1, row.SubscriberCount));
        secondRows.Append(new Row("z", 40));
        Assert.True(average.SourceVersion > version); Assert.Equal(calculations, average.ComputationCount);
        average.IsVisible = true;
        Assert.Equal(1, secondRows.SubscriberCount); Assert.Equal(95d / 3, average.CreateSnapshot().Points[^1].Point.Y, 10);
        average.Changed -= owner;
        Assert.Equal(0, secondRows.SubscriberCount); Assert.All(secondRows.Rows, row => Assert.Equal(0, row.SubscriberCount));
    }

    [AvaloniaFact]
    public void Hidden_intermediate_average_updates_visible_downstream_and_releases_sources_after_final_removal_or_unload()
    {
        var rows = new TrackedSource(new Row("a", 2), new Row("b", 4), new Row("c", 8), new Row("d", 16));
        var intermediate = new MovingAverageSeries { SourceSeries = Source(rows), Period = 2, IsVisible = false };
        var downstream = new MovingAverageSeries { SourceSeries = intermediate, Period = 2 };
        var chart = new CartesianChart { ShowLegend = false };
        chart.Series.Add(intermediate); chart.Series.Add(downstream); using var host = new TemplateFeaturesHost(chart);
        Assert.Equal(1, rows.SubscriberCount); Assert.Equal(9, downstream.CreateSnapshot().Points[^1].Point.Y);
        var version = downstream.SourceVersion;
        rows.Append(new Row("e", 32)); host.Drain(); _ = chart.PlotArea;
        Assert.True(downstream.SourceVersion > version); Assert.Equal(18, downstream.CreateSnapshot().Points[^1].Point.Y);
        Assert.False(intermediate.IsVisible); Assert.Same(chart, intermediate.SourceSeries!.GetLogicalParent());
        chart.Series.Remove(downstream); host.Drain(); _ = chart.PlotArea;
        var intermediateCalculations = intermediate.ComputationCount;
        rows.Append(new Row("f", 64)); host.Drain(); _ = chart.PlotArea;
        Assert.Equal(1, rows.SubscriberCount); Assert.Equal(intermediateCalculations, intermediate.ComputationCount);
        chart.Series.Remove(intermediate); host.Drain();
        Assert.Equal(0, rows.SubscriberCount); Assert.All(rows.Rows, row => Assert.Equal(0, row.SubscriberCount));
        chart.Series.Add(downstream); host.Drain(); _ = chart.PlotArea;
        Assert.Equal(36, downstream.CreateSnapshot().Points[^1].Point.Y); Assert.Equal(1, rows.SubscriberCount);
        host.Window.Content = null; host.Drain();
        Assert.Equal(0, rows.SubscriberCount); Assert.All(rows.Rows, row => Assert.Equal(0, row.SubscriberCount));
    }

    [AvaloniaFact]
    public void A_source_chain_rejects_self_and_indirect_cycles_before_replacing_the_valid_source()
    {
        var source = new LineSeries { ItemsSource = new[] { 1d, 2d, 3d } };
        var first = new MovingAverageSeries { SourceSeries = source, Period = 1 };
        var second = new MovingAverageSeries { SourceSeries = first, Period = 1 };
        Assert.Throws<InvalidOperationException>(() => first.SourceSeries = first);
        Assert.Same(source, first.SourceSeries);
        Assert.Throws<InvalidOperationException>(() => first.SetValue(ChartAnalyticalSeries.SourceSeriesProperty, second));
        Assert.Same(source, first.SourceSeries);
        Assert.Equal(3, second.CreateSnapshot().Points[^1].Point.Y);
    }

    [AvaloniaFact]
    public void Source_series_precedence_retires_ignored_items_observers_and_restores_them_when_cleared()
    {
        var own = new TrackedSource(new Row("own-a", 1), new Row("own-b", 3));
        var external = new TrackedSource(new Row("external-a", 20), new Row("external-b", 40));
        var average = new MovingAverageSeries { ItemsSource = own, ValuePath = "Value", IdPath = "Id", Period = 2 };
        EventHandler owner = (_, _) => { };
        average.Changed += owner;
        try
        {
            Assert.Equal(1, own.SubscriberCount); Assert.Equal(2, average.CreateSnapshot().Points[^1].Point.Y);
            average.SourceSeries = Source(external);
            Assert.Equal(0, own.SubscriberCount); Assert.All(own.Rows, row => Assert.Equal(0, row.SubscriberCount));
            Assert.Equal(1, external.SubscriberCount); Assert.Equal(30, average.CreateSnapshot().Points[^1].Point.Y);
            var version = average.SourceVersion; var calculations = average.ComputationCount;
            own.Append(new Row("own-c", 11)); own.Rows[0].Value = 5;
            Assert.Equal(version, average.SourceVersion); Assert.Equal(calculations, average.ComputationCount);
            var replacement = new TrackedSource(new Row("new-a", 8), new Row("new-b", 12));
            average.ItemsSource = replacement;
            Assert.Equal(0, replacement.SubscriberCount); Assert.Equal(version, average.SourceVersion);
            average.SourceSeries = null;
            Assert.Equal(0, external.SubscriberCount); Assert.All(external.Rows, row => Assert.Equal(0, row.SubscriberCount));
            Assert.Equal(1, replacement.SubscriberCount); Assert.Equal(10, average.CreateSnapshot().Points[^1].Point.Y);
        }
        finally { average.Changed -= owner; }
        Assert.Equal(0, own.SubscriberCount); Assert.Equal(0, external.SubscriberCount);
        Assert.Equal(0, ((TrackedSource)average.ItemsSource!).SubscriberCount);
    }

    [AvaloniaFact]
    public void Full_history_ema_is_reused_when_viewport_and_export_layout_change()
    {
        var average = new MovingAverageSeries
        {
            SourceSeries = new LineSeries { ItemsSource = new[] { 2d, 4d, 8d, 16d, 32d, 64d } },
            Type = ChartMovingAverageType.Exponential, Period = 3,
        };
        var chart = new CartesianChart { ShowLegend = false };
        chart.Series.Add(average); var window = Show(chart);
        try
        {
            var before = average.CreateSnapshot(); var computations = average.ComputationCount;
            Assert.Equal(511d / 12, before.Points[^1].Point.Y, 10);
            chart.Viewport = new(3, 5, 5, 70); _ = chart.PlotArea;
            using (var recording = new DrawingGroup().Open()) { chart.Render(recording); chart.Render(recording); }
            var export = chart.GetExportLayout(new() { PixelSize = new(1100, 700) });
            using (var recording = new DrawingGroup().Open()) chart.RenderExport(recording, export, Brushes.White);
            Assert.Equal(computations, average.ComputationCount); Assert.Same(before, average.CreateSnapshot());
            Assert.False(before.Points[3].IsEmpty);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Fitting_uses_full_source_category_mapping_before_viewport_clipping()
    {
        var source = new LineSeries { ItemsSource = new[] { new { Category = "A", Value = 1d }, new { Category = "B", Value = 5d }, new { Category = "C", Value = 9d } }, CategoryPath = "Category", ValuePath = "Value" };
        var trend = new ChartTrendlineSeries { SourceSeries = source, SampleCount = 3 };
        var chart = new CartesianChart { XAxis = new CategoryAxis { Labels = new[] { "B", "A", "C" } }, ShowLegend = false };
        chart.Series.Add(trend); var window = Show(chart);
        try
        {
            Assert.Equal(3, trend.FitResult!.Predict(0), 10); Assert.Equal(7, trend.FitResult.Predict(2), 10);
            var count = trend.ComputationCount;
            chart.Viewport = new(.75, 1.25, 0, 10); _ = chart.PlotArea;
            Assert.Equal(count, trend.ComputationCount); Assert.Equal(3, trend.FitResult.Count);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Derived_line_inherits_secondary_axes_and_hits_retain_calculated_and_source_values()
    {
        var rows = new TrackedSource(new Row("a", 40), new Row("b", 80), new Row("c", 100));
        var source = Source(rows); source.YAxisKey = "price";
        var average = new SMAIndicator { SourceSeries = source, Period = 2 };
        var chart = new CartesianChart { XAxis = new NumericAxis { Minimum = 0, Maximum = 2 }, YAxis = new NumericAxis { Minimum = 0, Maximum = 10 }, ShowLegend = false };
        chart.Axes.Add(new NumericAxis { Key = "price", Minimum = 0, Maximum = 100, Position = ChartAxisPosition.Far });
        chart.TechnicalIndicators.Add(average); var window = Show(chart);
        try
        {
            var plot = chart.PlotArea;
            var hit = chart.HitTestPoint(new(plot.Right, plot.Bottom - plot.Height * .9), 1)!;
            Assert.NotNull(hit); Assert.Same(average, hit.Series); Assert.Equal(new ChartPoint(2, 90), hit.Point);
            Assert.Equal(new ChartPoint(2, 100), hit.RawPoint); Assert.Same(rows.Rows[2], hit.Item);
            Assert.False(hit.Analysis!.IsSynthetic); Assert.Equal(hit.Point, hit.Analysis.CalculatedPoint);
            average.YAxisKey = ""; _ = chart.PlotArea;
            Assert.Null(chart.HitTestPoint(new(plot.Right, plot.Bottom - plot.Height * .9), .1));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Attached_trend_add_remove_and_chart_detach_release_observation_and_legend_membership()
    {
        var rows = new TrackedSource(new Row("a", 1), new Row("b", 3), new Row("c", 5));
        var source = Source(rows); source.Title = "source";
        var chart = new CartesianChart(); chart.Series.Add(source); var window = Show(chart);
        var trend = new Trendline { Title = "fit", SampleCount = 3 };
        try
        {
            source.Trendlines.Add(trend);
            Assert.Same(source, trend.SourceSeries); Assert.Contains(chart.LegendItems, item => ReferenceEquals(item.Series, trend));
            Assert.Equal(3, trend.FitResult!.Count);
            source.Trendlines.Remove(trend);
            Assert.Null(trend.SourceSeries); Assert.DoesNotContain(chart.LegendItems, item => ReferenceEquals(item.Series, trend));
            chart.Series.Remove(source);
            Assert.Equal(0, rows.SubscriberCount); Assert.All(rows.Rows, row => Assert.Equal(0, row.SubscriberCount));
            chart.Series.Add(source); Assert.Equal(1, rows.SubscriberCount);
        }
        finally { window.Close(); }
        Assert.Equal(0, rows.SubscriberCount); Assert.All(rows.Rows, row => Assert.Equal(0, row.SubscriberCount));
    }

    [AvaloniaFact]
    public void Fitted_samples_and_hits_are_explicitly_synthetic_and_unsupported_fits_report_issues()
    {
        var trend = new ChartTrendlineSeries { ItemsSource = new ChartPoint[] { new(1, 3), new(2, 5), new(3, 7) }, SampleCount = 3 };
        var data = trend.CreateSnapshot();
        Assert.All(data.Points, point => { Assert.True(point.Analysis!.IsSynthetic); Assert.Null(point.Item); Assert.Null(point.Analysis.SourceIndex); Assert.Null(point.Analysis.SourceItem); Assert.Null(point.RawPoint); });
        Assert.Equal(new[] { 0, 1, 2 }, data.Points.Select(point => point.SourceIndex));
        var tooltip = new Border();
        var chart = new CartesianChart { ShowLegend = false, TooltipContentTemplate = new FuncDataTemplate<ChartTooltipContext>((_, _) => tooltip) };
        chart.Series.Add(trend); var window = Show(chart);
        try
        {
            var plot = chart.PlotArea; var range = chart.VisibleRange!.Value;
            var hit = chart.HitTestPoint(new(plot.Left + (2 - range.XMinimum) / (range.XMaximum - range.XMinimum) * plot.Width,
                plot.Bottom - (5 - range.YMinimum) / (range.YMaximum - range.YMinimum) * plot.Height), 1)!;
            Assert.NotNull(hit); Assert.True(hit.Analysis!.IsSynthetic); Assert.Null(hit.Item);
            SegmentTestHost.RaiseExactMove(chart, hit.Position); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var context = Assert.IsType<ChartTooltipContext>(tooltip.DataContext);
            Assert.Contains("拟合采样", context.Text); Assert.DoesNotContain("原始端点", context.Text);
        }
        finally { window.Close(); }
        var errors = 0; trend.DataError += (_, _) => errors++;
        trend.ItemsSource = new ChartPoint[] { new(1, 1), new(1, 2) };
        Assert.Empty(trend.CreateSnapshot().Points); Assert.Null(trend.FitResult); Assert.Equal(1, errors);
        trend.ItemsSource = new ChartPoint[] { new(1, 3), new(2, 5) };
        Assert.NotEmpty(trend.CreateSnapshot().Points); Assert.NotNull(trend.FitResult);
    }

    [AvaloniaFact]
    public void Bollinger_has_three_independent_lines_and_a_real_filled_band_with_shared_endpoint_provenance()
    {
        var indicator = new BollingerBandsIndicator { ItemsSource = new[] { 1d, 2d, 3d, 4d }, Period = 3, StandardDeviationMultiplier = 2 };
        indicator.BandSeries.Fill = Brushes.Magenta; indicator.BandSeries.ShowInLegend = true;
        indicator.UpperSeries.Stroke = Brushes.Red; indicator.LowerSeries.Stroke = Brushes.Blue;
        var chart = new CartesianChart { SeriesSelectionMode = ChartSelectionMode.Single }; chart.TechnicalIndicators.Add(indicator); var window = Show(chart);
        try
        {
            Assert.Equal(4, chart.LegendItems.Count); Assert.Same(indicator, indicator.MiddleSeries);
            Assert.Equal(3, indicator.CreateSnapshot().Points[^1].Point.Y);
            var upper = indicator.UpperSeries.CreateSnapshot().Points[^1]; var lower = indicator.LowerSeries.CreateSnapshot().Points[^1];
            Assert.Equal(3 + 2 * Math.Sqrt(2d / 3), upper.Point.Y, 10); Assert.Equal(3 - 2 * Math.Sqrt(2d / 3), lower.Point.Y, 10);
            Assert.Equal(upper.StableId, lower.StableId); Assert.Equal(3, upper.Analysis!.SourceIndex);
            var recording = new DrawingGroup(); using (var drawing = recording.Open()) chart.Render(drawing);
            Assert.Contains(Drawings(recording).OfType<GeometryDrawing>(), drawing => ReferenceEquals(drawing.Brush, Brushes.Magenta));
            indicator.UpperSeries.ShowInLegend = false; Assert.Equal(3, chart.LegendItems.Count);
            indicator.IsVisible = false;
            Assert.All(chart.LegendItems, item => Assert.False(item.IsVisible));
            Assert.False(chart.SelectSeries(indicator.LowerSeries));
            indicator.IsVisible = true;
            chart.TechnicalIndicators.Clear(); Assert.Empty(chart.LegendItems);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Indicator_collection_removal_releases_an_external_source_and_gap_recovers_after_append()
    {
        var rows = new TrackedSource(new Row("a", 1), new Row("b", double.NaN), new Row("c", 4));
        var indicator = new WMAIndicator { SourceSeries = Source(rows), Period = 2 };
        var chart = new CartesianChart(); chart.TechnicalIndicators.Add(indicator); var window = Show(chart);
        try
        {
            Assert.All(indicator.CreateSnapshot().Points, point => Assert.True(point.IsEmpty));
            rows.Append(new Row("d", 10)); Assert.Equal(8, indicator.CreateSnapshot().Points[^1].Point.Y);
            chart.TechnicalIndicators.Remove(indicator); Assert.Equal(0, rows.SubscriberCount);
            Assert.All(rows.Rows, row => Assert.Equal(0, row.SubscriberCount));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Analytical_parameters_reject_invalid_values_and_empty_point_policy_cannot_fill_calculated_warmup()
    {
        var average = new MovingAverageSeries { ItemsSource = new[] { 1d, 2d, 3d }, Period = 3, EmptyPointMode = EmptyPointMode.Zero };
        Assert.True(average.CreateSnapshot().Points[1].IsEmpty);
        Assert.Throws<ArgumentException>(() => average.Period = 0);
        Assert.Throws<ArgumentException>(() => new BollingerBandsIndicator { StandardDeviationMultiplier = double.NaN });
        Assert.Throws<ArgumentException>(() => new ChartTrendlineSeries { SampleCount = 1 });
    }

    [AvaloniaFact]
    public void Forecast_extends_samples_without_changing_the_fit_and_explicit_bounds_take_precedence()
    {
        var trend = new ChartTrendlineSeries
        {
            ItemsSource = new ChartPoint[] { new(1, 3), new(2, 5), new(3, 7) }, SampleCount = 3,
            BackwardForecast = 2, ForwardForecast = 4,
        };
        var forecast = trend.CreateSnapshot();
        Assert.Equal(-1, forecast.Points[0].Point.X); Assert.Equal(7, forecast.Points[^1].Point.X);
        Assert.Equal(-1, forecast.Points[0].Point.Y, 9); Assert.Equal(15, forecast.Points[^1].Point.Y, 9);
        Assert.Equal(3, trend.FitResult!.Count); Assert.Equal(1, trend.FitResult.RSquared!.Value, 10);
        trend.Minimum = 0; trend.Maximum = 5;
        var bounded = trend.CreateSnapshot();
        Assert.Equal(0, bounded.Points[0].Point.X); Assert.Equal(5, bounded.Points[^1].Point.X);
        trend.Type = ChartTrendlineType.Logarithmic;
        Assert.Empty(trend.CreateSnapshot().Points); Assert.Null(trend.FitResult);
    }

    [AvaloniaFact]
    public void Financial_close_indicator_keeps_trading_slots_and_original_time_metadata()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var source = new FinancialSeries
        {
            ItemsSource = new ChartFinancialPoint[] { new(start, 1, 3, 1, 2), new(start.AddDays(1), 2, 5, 2, 4), new(start.AddDays(4), 4, 9, 4, 8) },
        };
        var average = new SMAIndicator { SourceSeries = source, Period = 2 };
        var points = average.CreateSnapshot().Points;
        Assert.Equal(new ChartPoint(2, 6), points[^1].Point);
        Assert.Equal(start.AddDays(4).ToUnixTimeMilliseconds(), points[^1].RawPoint!.Value.X);
        Assert.Equal(8, points[^1].RawPoint!.Value.Y); Assert.NotNull(points[^1].Financial);
        Assert.Equal(2, points[^1].Analysis!.SourceIndex);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Financial_indicator_tooltip_and_trackball_contexts_separate_calculated_and_original_endpoint_values(bool trackball)
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var source = new FinancialSeries
        {
            ItemsSource = new ChartFinancialPoint[] { new(start, 1, 3, 1, 2), new(start.AddDays(1), 2, 5, 2, 4), new(start.AddDays(4), 4, 9, 4, 8) },
        };
        var average = new SMAIndicator { SourceSeries = source, Period = 2, Title = "SMA" };
        var chart = new CartesianChart
        {
            XAxis = new NumericAxis { Minimum = 0, Maximum = 3 }, YAxis = new NumericAxis { Minimum = 0, Maximum = 10 }, ShowLegend = false,
        };
        var template = new FuncDataTemplate<ChartTooltipContext>((_, _) => new Border { Name = "AnalyticalReadout" });
        if (trackball) { chart.TrackballMode = ChartTrackballMode.Vertical; chart.TrackballContentTemplate = template; }
        else chart.TooltipContentTemplate = template;
        chart.TechnicalIndicators.Add(average); using var host = new TemplateFeaturesHost(chart);
        var plot = chart.PlotArea;
        SegmentTestHost.RaiseExactMove(chart, new(plot.Left + plot.Width * 2 / 3, plot.Bottom - plot.Height * .6)); host.Drain();
        var card = Assert.Single(chart.GetVisualDescendants().OfType<Border>(), item => item.Name == "AnalyticalReadout");
        var context = Assert.IsType<ChartTooltipContext>(card.DataContext);
        Assert.Same(average, context.Primary.Series); Assert.Equal(6, context.Primary.Point.Y); Assert.Equal(8, context.Primary.RawPoint!.Value.Y);
        Assert.Contains("端点计算值: 6", context.Text); Assert.Contains("原始端点: 8", context.Text); Assert.Contains("源端点索引: 2", context.Text);
        Assert.Contains("C=8", context.Text); Assert.Contains("源记录: 2", context.Text); Assert.Contains("2026-01-05", context.Text);
        Assert.Equal(8, context.Primary.Financial!.SourceEnd.Close);
        Assert.Equal(new ChartPoint(2, 6), context.Primary.Analysis!.CalculatedPoint);
    }

    [AvaloniaFact]
    public void Removed_rendered_source_keeps_inherited_binding_until_its_last_analytical_dependency_is_removed()
    {
        var rows = new TrackedSource(new Row("a", 2), new Row("b", 4));
        var model = new InheritedInput(rows, Array.Empty<object>());
        var source = new LineSeries { ValuePath = "Value", IdPath = "Id", Title = "source" };
        using var binding = source.Bind(ChartSeries.ItemsSourceProperty, new Binding(nameof(InheritedInput.Records)));
        var average = new MovingAverageSeries { SourceSeries = source, Period = 2, Title = "average" };
        var chart = new CartesianChart { DataContext = model };
        chart.Series.Add(source); chart.Series.Add(average);
        using var host = new TemplateFeaturesHost(chart);
        Assert.Same(rows, source.ItemsSource); Assert.Same(model, source.DataContext); Assert.Equal(1, rows.SubscriberCount);
        chart.Series.Remove(source); host.Drain();
        Assert.Same(chart, source.GetLogicalParent()); Assert.Same(model, source.DataContext); Assert.Same(rows, source.ItemsSource);
        Assert.DoesNotContain(chart.LegendItems, item => ReferenceEquals(item.Series, source));
        rows.Append(new Row("c", 8)); host.Drain(); Assert.Equal(6, average.CreateSnapshot().Points[^1].Point.Y);
        chart.Series.Remove(average); host.Drain();
        Assert.Null(source.GetLogicalParent()); Assert.Null(source.DataContext); Assert.Null(source.ItemsSource);
        Assert.Equal(0, rows.SubscriberCount); Assert.All(rows.Rows, row => Assert.Equal(0, row.SubscriberCount));
        chart.Series.Add(average); host.Drain();
        Assert.Same(chart, source.GetLogicalParent()); Assert.Same(rows, source.ItemsSource); Assert.Equal(1, rows.SubscriberCount);
        Assert.Equal(6, average.CreateSnapshot().Points[^1].Point.Y);
        host.Window.Content = null; host.Drain(); Assert.Equal(0, rows.SubscriberCount);
        Assert.All(rows.Rows, row => Assert.Equal(0, row.SubscriberCount));
    }

    [AvaloniaFact]
    public void Replacing_an_intermediate_source_dependency_moves_inherited_context_and_releases_retired_bound_rows()
    {
        var first = new TrackedSource(new Row("a", 2), new Row("b", 4));
        var second = new TrackedSource(new Row("x", 20), new Row("y", 40));
        var model = new InheritedInput(first, second);
        var firstSource = new LineSeries { ValuePath = "Value", IdPath = "Id" };
        var secondSource = new LineSeries { ValuePath = "Value", IdPath = "Id" };
        using var firstBinding = firstSource.Bind(ChartSeries.ItemsSourceProperty, new Binding(nameof(InheritedInput.Records)));
        using var secondBinding = secondSource.Bind(ChartSeries.ItemsSourceProperty, new Binding(nameof(InheritedInput.AlternateRecords)));
        var intermediate = new MovingAverageSeries { SourceSeries = firstSource, Period = 1 };
        var average = new EMAIndicator { SourceSeries = intermediate, Period = 2 };
        var chart = new CartesianChart { DataContext = model, ShowLegend = false };
        chart.TechnicalIndicators.Add(average); using var host = new TemplateFeaturesHost(chart);
        Assert.Same(chart, firstSource.GetLogicalParent()); Assert.Same(chart, intermediate.GetLogicalParent());
        Assert.Equal(3, average.CreateSnapshot().Points[^1].Point.Y); Assert.Equal(1, first.SubscriberCount);
        intermediate.SourceSeries = secondSource; host.Drain();
        Assert.Null(firstSource.GetLogicalParent()); Assert.Null(firstSource.DataContext); Assert.Null(firstSource.ItemsSource);
        Assert.Equal(0, first.SubscriberCount); Assert.All(first.Rows, row => Assert.Equal(0, row.SubscriberCount));
        Assert.Same(chart, secondSource.GetLogicalParent()); Assert.Same(model, secondSource.DataContext); Assert.Same(second, secondSource.ItemsSource);
        Assert.Equal(1, second.SubscriberCount); Assert.Equal(30, average.CreateSnapshot().Points[^1].Point.Y);
        chart.TechnicalIndicators.Clear(); host.Drain();
        Assert.Null(secondSource.GetLogicalParent()); Assert.Null(intermediate.GetLogicalParent()); Assert.Null(secondSource.DataContext);
        Assert.Equal(0, second.SubscriberCount); Assert.All(second.Rows, row => Assert.Equal(0, row.SubscriberCount));
    }

    [AvaloniaFact]
    public void Initially_bound_source_series_receives_context_before_mapping_and_tracks_context_replacement()
    {
        var first = new TrackedSource(new Row("a", 2), new Row("b", 4));
        var second = new TrackedSource(new Row("x", 20), new Row("y", 40));
        var source = new LineSeries { ValuePath = "Value", IdPath = "Id" };
        using var itemsBinding = source.Bind(ChartSeries.ItemsSourceProperty, new Binding(nameof(InheritedInput.Records)));
        var average = new MovingAverageSeries { Period = 2 };
        using var sourceBinding = average.Bind(ChartAnalyticalSeries.SourceSeriesProperty, new Binding(nameof(InheritedInput.BoundSource)));
        var firstModel = new InheritedInput(first, Array.Empty<object>()) { BoundSource = source };
        var chart = new CartesianChart { DataContext = firstModel, ShowLegend = false };
        chart.Series.Add(average); using var host = new TemplateFeaturesHost(chart);
        Assert.Same(source, average.SourceSeries); Assert.Same(chart, source.GetLogicalParent()); Assert.Same(first, source.ItemsSource);
        Assert.Equal(3, average.CreateSnapshot().Points[^1].Point.Y);
        chart.DataContext = new InheritedInput(second, Array.Empty<object>()) { BoundSource = source }; host.Drain();
        Assert.Same(second, source.ItemsSource); Assert.Equal(30, average.CreateSnapshot().Points[^1].Point.Y);
        Assert.Equal(0, first.SubscriberCount); Assert.All(first.Rows, row => Assert.Equal(0, row.SubscriberCount));
        chart.Series.Clear(); host.Drain();
        Assert.Null(source.GetLogicalParent()); Assert.Null(source.DataContext); Assert.Equal(0, second.SubscriberCount);
    }

    [AvaloniaFact]
    public void Derived_selection_inherits_source_id_value_equality_across_source_replacement()
    {
        var first = new[] { new IdCopyRow("a", 1), new IdCopyRow("b", 3) };
        var source = new LineSeries { ItemsSource = first, IdPath = "Id", ValuePath = "Value" };
        var average = new MovingAverageSeries { SourceSeries = source, Period = 1 };
        var chart = new CartesianChart { XAxis = new NumericAxis { Minimum = 0, Maximum = 2 }, YAxis = new NumericAxis { Minimum = 0, Maximum = 10 }, ShowLegend = false };
        chart.Series.Add(average); var window = Show(chart);
        try
        {
            var plot = chart.PlotArea;
            var point = new Point(plot.Left + plot.Width / 2, plot.Bottom - plot.Height * .3);
            chart.SelectRectangle(new Rect(point.X - 1, point.Y - 1, 2, 2));
            Assert.Same(first[1], chart.SelectedPoint!.Item);
            var replacement = new[] { new IdCopyRow("a", 2), new IdCopyRow("b", 7) };
            source.ItemsSource = replacement;
            Assert.NotNull(chart.SelectedPoint); Assert.Same(replacement[1], chart.SelectedPoint!.Item);
            Assert.Equal("b", chart.SelectedPoint.StableId); Assert.Equal(7, chart.SelectedPoint.Point.Y);
        }
        finally { window.Close(); }
    }

    private static LineSeries Source(TrackedSource rows) => new() { ItemsSource = rows, ValuePath = "Value", IdPath = "Id" };
    private static Window Show(CartesianChart chart)
    {
        var window = new Window { Width = 900, Height = 550, Content = chart };
        window.Show(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); _ = chart.PlotArea; return window;
    }
    private static IEnumerable<Drawing> Drawings(Drawing drawing)
    {
        yield return drawing;
        if (drawing is DrawingGroup group)
            foreach (var child in group.Children)
                foreach (var nested in Drawings(child)) yield return nested;
    }
    private sealed class Row(string id, double value) : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? _changed;
        private double _value = value;
        public string Id { get; } = id;
        public double Value { get => _value; set { _value = value; _changed?.Invoke(this, new(nameof(Value))); } }
        public int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;
        public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
    }
    private sealed class IdCopyRow(string id, double value)
    {
        public string Id => new(id.ToCharArray());
        public double Value { get; } = value;
    }
    public sealed class InheritedInput(IEnumerable records, IEnumerable alternateRecords)
    {
        public IEnumerable Records { get; } = records;
        public IEnumerable AlternateRecords { get; } = alternateRecords;
        public ChartSeries? BoundSource { get; init; }
    }
    private sealed class TrackedSource(params Row[] rows) : IEnumerable<Row>, INotifyCollectionChanged
    {
        private NotifyCollectionChangedEventHandler? _changed;
        public List<Row> Rows { get; } = rows.ToList();
        public int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;
        public event NotifyCollectionChangedEventHandler? CollectionChanged { add => _changed += value; remove => _changed -= value; }
        public void Append(Row row) { Rows.Add(row); _changed?.Invoke(this, new(NotifyCollectionChangedAction.Add, row, Rows.Count - 1)); }
        public IEnumerator<Row> GetEnumerator() => Rows.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
