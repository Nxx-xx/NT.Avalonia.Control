using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class CartesianHiddenWorkAdmissionTests(ITestOutputHelper output)
{
    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Pending_render_after_ancestor_hides_never_prepares_deferred_or_replaced_source(bool compositorFrame)
    {
        var getterCalls = 0; var capture = false; var traces = new List<string>();
        GetterRow Row(double x, double y) => new(x, () => { getterCalls++; if (capture && traces.Count < 4) traces.Add(Environment.StackTrace); return y; });
        var first = Row(0, 10); var buffer = new ChartDataBuffer<GetterRow>(); buffer.AppendRange([first, Row(1, 20)]);
        var series = new LineSeries { ItemsSource = buffer, CategoryPath = nameof(GetterRow.X), ValuePath = nameof(GetterRow.Y), MarkerSize = 0 };
        var chart = new CartesianChart { Width = 620, Height = 300, ShowLegend = false }; chart.Series.Add(series);
        var parent = new Border { Child = chart }; using var host = new TemplateFeaturesHost(chart, parent);
        TemplateFeaturesHost.Record(chart); chart.SelectedPoint = ChartPointHit.FromData(series, series.CreateSnapshot().Points[0], default);
        // Leave a compositor invalidation pending while the chart is still visible.
        chart.InvalidateVisual(); var update = chart.DeferDataUpdates(); buffer.AppendRange([Row(2, 30)]); parent.IsVisible = false; update.Dispose(); buffer.AppendRange([Row(3, 40)]);
        var callsBefore = getterCalls; var preparedBefore = Counter(chart); var mappedBefore = ChartSeries.BufferedMappedRowCount; capture = true;
        chart.SeriesSource = new[] { series };
        if (compositorFrame) { AvaloniaHeadlessPlatform.ForceRenderTimerTick(2); host.Drain(); }
        else TemplateFeaturesHost.Record(chart);
        output.WriteLine($"compositorFrame={compositorFrame}; active={chart.GetWorkSchedulingInfo().IsActive}; getterCalls={callsBefore}->{getterCalls}; prepared={preparedBefore}->{Counter(chart)}");
        foreach (var trace in traces) output.WriteLine(trace);
        Assert.Equal(callsBefore, getterCalls); Assert.Equal(preparedBefore, Counter(chart)); Assert.Equal(mappedBefore, ChartSeries.BufferedMappedRowCount);
        capture = false; parent.IsVisible = true; host.Drain(); TemplateFeaturesHost.Record(chart);
        Assert.True(chart.GetWorkSchedulingInfo().IsActive); Assert.Equal(4, series.CreateSnapshot().Count); Assert.True(chart.VisibleRange!.Value.YMaximum >= 40);
        Assert.Same(first, chart.SelectedPoint!.Item); Assert.Equal(0, chart.SelectedPoint.Index); Assert.Equal(10, chart.SelectedPoint.Point.Y);
    }
    [AvaloniaFact]
    public void Explicit_hidden_export_draws_latest_data_without_reenabling_pending_compositor_work()
    {
        var getterCalls = 0; GetterRow Row(double x, double y) => new(x, () => { getterCalls++; return y; });
        var buffer = new ChartDataBuffer<GetterRow>(); buffer.AppendRange([Row(0, 10), Row(1, 20)]);
        var series = new LineSeries { ItemsSource = buffer, CategoryPath = nameof(GetterRow.X), ValuePath = nameof(GetterRow.Y), MarkerSize = 0, Stroke = Brushes.Magenta };
        var chart = new CartesianChart { Width = 620, Height = 300, ShowLegend = false }; chart.Series.Add(series);
        var parent = new Border { Child = chart }; using var host = new TemplateFeaturesHost(chart, parent); TemplateFeaturesHost.Record(chart);
        parent.IsVisible = false; buffer.AppendRange([Row(2, 55)]); var beforeExport = getterCalls;
        var exported = new DrawingGroup(); using (var context = exported.Open()) chart.RenderExport(context, chart.GetExportLayout(null), null);
        Assert.True(getterCalls > beforeExport); AssertSeriesPath(exported, series); Assert.True(chart.VisibleRange!.Value.YMaximum >= 55); Assert.False(chart.GetWorkSchedulingInfo().IsActive);
        buffer.AppendRange([Row(3, 80)]); var beforeFrame = getterCalls; var prepared = Counter(chart);
        chart.InvalidateVisual(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(2); host.Drain();
        Assert.Equal(beforeFrame, getterCalls); Assert.Equal(prepared, Counter(chart));
        parent.IsVisible = true; host.Drain(); TemplateFeaturesHost.Record(chart); Assert.True(chart.VisibleRange!.Value.YMaximum >= 80);
    }
    [AvaloniaFact]
    public void Explicit_detached_drawing_still_prepares_and_draws_current_source()
    {
        var rows = new ChartDataBuffer<ChartPoint>(); rows.AppendRange([new(0, 10), new(1, 20)]);
        var series = new LineSeries { ItemsSource = rows, MarkerSize = 0, Stroke = Brushes.Magenta };
        var chart = new CartesianChart { Width = 620, Height = 300, ShowLegend = false }; chart.Series.Add(series);
        using var host = new TemplateFeaturesHost(chart); TemplateFeaturesHost.Record(chart); host.Window.Content = null;
        rows.AppendRange([new(2, 55)]); chart.Arrange(new Rect(0, 0, 620, 300));
        var before = Counter(chart); var drawn = TemplateFeaturesHost.Record(chart);
        Assert.False(chart.GetWorkSchedulingInfo().IsRegistered); Assert.True(Counter(chart) > before); AssertSeriesPath(drawn, series); Assert.True(chart.VisibleRange!.Value.YMaximum >= 55);
    }
    private static void AssertSeriesPath(DrawingGroup drawing, LineSeries series) => Assert.Contains(TemplateFeaturesHost.Walk(drawing).OfType<GeometryDrawing>(), geometry => ReferenceEquals(geometry.Pen?.Brush, series.Stroke) && geometry.Geometry?.Bounds.Width > 0);
    private static long Counter(CartesianChart chart) => (long)typeof(CartesianChart).GetField("_preparedDataBuildCount", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(chart)!;
    private sealed class GetterRow(double x, Func<double> getter) { public double X => x; public double Y => getter(); }
}
