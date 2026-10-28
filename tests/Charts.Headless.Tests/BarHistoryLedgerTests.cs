using System.Collections;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class BarHistoryLedgerTests
{
    [AvaloniaFact]
    public void Source_and_view_invalidation_keep_the_full_history_charged_without_double_counting_the_scene()
    {
        using var registration = new NoNativeRegistration();
        var series = MakeSeries();
        using var fixture = new Fixture(series);
        fixture.Record();
        var history = History(series)!;
        var retained = Retained(fixture.Chart);
        var reserved = retained.SharedCircleHistoryBytes;
        Assert.True(reserved >= 4096L * 256);
        Assert.Equal(retained.EstimatedBytes, fixture.Chart.GetResourceCacheInfo().SeriesDrawingBytes);
        Assert.Equal(reserved, HistoryBytes(fixture.Chart));

        series.ItemsSource = Points(2048, .25);
        var invalidated = fixture.Chart.GetResourceCacheInfo();
        Assert.Equal(0, invalidated.SeriesDrawingCount);
        Assert.Equal(reserved, invalidated.SeriesDrawingBytes);
        Assert.Same(history, History(series));
        fixture.Record();
        Assert.Equal(Retained(fixture.Chart).EstimatedBytes, fixture.Chart.GetResourceCacheInfo().SeriesDrawingBytes);
        Assert.Equal(reserved, HistoryBytes(fixture.Chart));

        fixture.Chart.XAxis.Minimum = 500;
        fixture.Chart.XAxis.Maximum = 550;
        Assert.Equal(reserved, fixture.Chart.GetResourceCacheInfo().SeriesDrawingBytes);
        fixture.Record();
        Assert.Equal(reserved, HistoryBytes(fixture.Chart));
        Assert.Same(history, History(series));
        Assert.Equal(Retained(fixture.Chart).EstimatedBytes, fixture.Chart.GetResourceCacheInfo().SeriesDrawingBytes);
        var before = fixture.Chart.GetResourceCacheInfo().EstimatedBytes;
        fixture.Record();
        Assert.Equal(before, fixture.Chart.GetResourceCacheInfo().EstimatedBytes);
    }

    [AvaloniaFact]
    public void A_nonzero_budget_reduction_after_invalidation_evicts_history_and_preserves_older_frames()
    {
        using var registration = new NoNativeRegistration();
        var series = MakeSeries();
        using var fixture = new Fixture(series);
        var oldFrame = fixture.Record();
        var oldBounds = oldFrame.GetBounds();
        var history = History(series)!;
        series.ItemsSource = Points(2048, .5);
        Assert.Equal(0, fixture.Chart.GetResourceCacheInfo().SeriesDrawingCount);
        Assert.True(fixture.Chart.GetResourceCacheInfo().SeriesDrawingBytes > 1_000_000);
        registration.Cleared.Clear();
        fixture.Chart.ResourceCacheBudgetBytes = 1;
        Assert.Null(History(series));
        Assert.False(Active(history));
        Assert.Contains(series, registration.Cleared);
        Assert.InRange(fixture.Chart.GetResourceCacheInfo().EstimatedBytes, 0, 1);
        Assert.Equal(0, HistoryBytes(fixture.Chart));
        Assert.Equal(oldBounds, oldFrame.GetBounds());
        fixture.Record();
        Assert.Null(History(series));
        Assert.Equal(0, fixture.Chart.GetResourceCacheInfo().SeriesDrawingCount);
        Assert.InRange(fixture.Chart.GetResourceCacheInfo().EstimatedBytes, 0, 1);
    }

    [AvaloniaFact]
    public void History_growth_removal_and_multi_owner_retirement_keep_the_ledger_consistent()
    {
        using var registration = new NoNativeRegistration();
        var series = MakeSeries();
        using var fixture = new Fixture(series);
        fixture.Record();
        var initial = HistoryBytes(fixture.Chart);
        series.ItemsSource = Points(4096, .25);
        fixture.Record();
        var grown = HistoryBytes(fixture.Chart);
        Assert.True(grown > initial);
        Assert.Equal(Retained(fixture.Chart).EstimatedBytes, fixture.Chart.GetResourceCacheInfo().SeriesDrawingBytes);
        Assert.InRange(fixture.Chart.GetResourceCacheInfo().EstimatedBytes, 0, fixture.Chart.ResourceCacheBudgetBytes);

        EventHandler observer = (_, _) => { };
        series.Changed += observer;
        try
        {
            fixture.Record();
            Assert.Null(History(series));
            Assert.Equal(0, HistoryBytes(fixture.Chart));
            Assert.Equal(0, Retained(fixture.Chart).SharedCircleHistoryBytes);
            Assert.Equal(Retained(fixture.Chart).EstimatedBytes, fixture.Chart.GetResourceCacheInfo().SeriesDrawingBytes);
        }
        finally { series.Changed -= observer; }
        series.ItemsSource = Points(2048, 0);
        fixture.Record();
        Assert.NotNull(History(series));
        fixture.Chart.Series.Remove(series);
        Assert.Null(History(series));
        Assert.Equal(0, HistoryBytes(fixture.Chart));
        Assert.Equal(0, fixture.Chart.GetResourceCacheInfo().SeriesDrawingBytes);
    }

    [AvaloniaFact]
    public void Process_budget_eviction_retires_both_the_reservation_and_its_dependent_scene()
    {
        using var registration = new NoNativeRegistration();
        var originalBudget = ChartWorkScheduler.ResourceCacheBudgetBytes;
        var series = MakeSeries();
        using var fixture = new Fixture(series);
        fixture.Record();
        var history = History(series)!;
        try
        {
            ChartWorkScheduler.ResourceCacheBudgetBytes = 1;
            Assert.Null(History(series));
            Assert.False(Active(history));
            Assert.Equal(0, HistoryBytes(fixture.Chart));
            Assert.Equal(0, fixture.Chart.GetResourceCacheInfo().SeriesDrawingCount);
            Assert.InRange(fixture.Chart.GetResourceCacheInfo().EstimatedBytes, 0, 1);
        }
        finally { ChartWorkScheduler.ResourceCacheBudgetBytes = originalBudget; }
    }

    [AvaloniaFact]
    public void Dense_clipped_zero_budget_and_detached_rendering_do_not_create_uncharged_history()
    {
        var series = MakeSeries();
        series.ItemsSource = Points(4096, 0);
        using (var fixture = new Fixture(series))
        {
            fixture.Record();
            Assert.NotNull(History(series));
            fixture.Chart.XAxis.Minimum = 500;
            fixture.Chart.XAxis.Maximum = 3500;
            fixture.Chart.ResourceCacheBudgetBytes = 0;
            fixture.Record();
            Assert.Null(History(series));
            Assert.Equal(0, fixture.Chart.GetResourceCacheInfo().EstimatedBytes);
        }
        series = MakeSeries();
        series.ItemsSource = Points(4096, 0);
        var detached = new CartesianChart { ShowLegend = false, AnimationTriggers = ChartAnimationTriggers.None };
        detached.Series.Add(series);
        detached.XAxis.Minimum = 500; detached.XAxis.Maximum = 3500;
        detached.Measure(new Size(800, 600)); detached.Arrange(new Rect(0, 0, 800, 600));
        var group = new DrawingGroup();
        using (var context = group.Open()) detached.Render(context);
        Assert.Null(History(series));
        Assert.Equal(0, HistoryBytes(detached));
        detached.Series.Clear();
    }

    private static BarSeries MakeSeries() => new()
    { ShowMarkers = false, Fill = Brushes.Blue, ItemsSource = Points(2048, 0) };
    private static ChartPoint[] Points(int count, double revision) => Enumerable.Range(0, count)
        .Select(index => new ChartPoint(index + .125, index % 37 + 5.25 + revision)).ToArray();
    private static ChartRetainedSeriesDrawing Retained(CartesianChart chart) => Assert.Single(
        ((IDictionary)typeof(CartesianChart).GetField("_seriesDrawings", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(chart)!).Values.Cast<ChartRetainedSeriesDrawing>());
    private static object? History(ChartSeries series)
    {
        var table = typeof(CartesianSeriesRenderer).GetField("BarHistories", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        object?[] args = [series, null];
        return (bool)table.GetType().GetMethod("TryGetValue")!.Invoke(table, args)! ? args[1] : null;
    }
    private static bool Active(object history) => (bool)history.GetType()
        .GetProperty("Active", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(history)!;
    private static long HistoryBytes(CartesianChart chart)
    {
        var entries = (IEnumerable)typeof(CartesianChart).GetField("_resourceLru", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(chart)!;
        var total = 0L;
        foreach (var entry in entries)
        {
            var type = entry.GetType();
            var key = type.GetProperty("Key")!.GetValue(entry)!;
            if (key.GetType().GetProperty("Kind")!.GetValue(key)!.ToString() == "CircleHistory")
                total += (long)type.GetProperty("Bytes")!.GetValue(entry)!;
        }
        return total;
    }
    private sealed class NoNativeRegistration : IDisposable
    {
        private readonly FieldInfo _field = typeof(ChartNativeCircleBackend).GetField("_provider", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? _previous;
        internal List<ChartSeries> Cleared { get; } = [];
        internal NoNativeRegistration()
        {
            _previous = _field.GetValue(null);
            ChartNativeCircleBackend.Register((_, _, _, _, _) => null, series => Cleared.Add(series));
        }
        public void Dispose() => _field.SetValue(null, _previous);
    }
    private sealed class Fixture : IDisposable
    {
        internal CartesianChart Chart { get; } = new() { ShowLegend = false, AnimationTriggers = ChartAnimationTriggers.None };
        private Window Window { get; }
        internal Fixture(ChartSeries series)
        {
            Chart.Series.Add(series);
            Window = new Window { Width = 800, Height = 600, Content = Chart };
            Window.Show(); Window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); _ = Chart.PlotArea;
        }
        internal DrawingGroup Record()
        {
            Window.UpdateLayout(); _ = Chart.PlotArea;
            var group = new DrawingGroup(); using (var context = group.Open()) Chart.Render(context); return group;
        }
        public void Dispose() => Window.Close();
    }
}
