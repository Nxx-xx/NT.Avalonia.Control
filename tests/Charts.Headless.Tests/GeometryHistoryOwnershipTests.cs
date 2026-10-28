using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class GeometryHistoryOwnershipTests
{
    [AvaloniaFact]
    public void A_history_requires_one_owner_and_full_trim_releases_it_without_affecting_old_frames()
    {
        var series = MakeSeries();
        Assert.False(series.CanShareRenderingGeometryHistory);
        EventHandler observer = (_, _) => { };
        series.Changed += observer;
        Assert.True(series.CanShareRenderingGeometryHistory);
        series.Changed -= observer;
        using var registration = new ClearRegistration();
        using var fixture = new Fixture(series);
        Assert.True(series.CanShareRenderingGeometryHistory);
        series.Changed += observer;
        Assert.False(series.CanShareRenderingGeometryHistory);
        series.Changed -= observer;
        Assert.True(series.CanShareRenderingGeometryHistory);

        var oldFrame = fixture.Record();
        var bounds = oldFrame.GetBounds();
        registration.Cleared.Clear();
        series.ItemsSource = Enumerable.Range(0, 16).Select(index => new ChartPoint(index, index % 7 + .25)).ToArray();
        fixture.Record();
        Assert.DoesNotContain(series, registration.Cleared);
        fixture.Chart.TrimResourceCaches();
        Assert.Contains(series, registration.Cleared);
        Assert.Equal(bounds, oldFrame.GetBounds());
        Assert.Equal(0, fixture.Chart.GetResourceCacheInfo().EstimatedBytes);
    }

    [AvaloniaFact]
    public void Budget_zero_eviction_visibility_removal_and_detach_clear_native_history()
    {
        var series = MakeSeries();
        using var registration = new ClearRegistration();
        using var fixture = new Fixture(series);
        fixture.Record(); registration.Cleared.Clear();
        fixture.Chart.ResourceCacheBudgetBytes = 0;
        Assert.Contains(series, registration.Cleared);

        fixture.Chart.ResourceCacheBudgetBytes = 128L * 1024 * 1024;
        fixture.Record(); registration.Cleared.Clear();
        ChartWorkScheduler.TrimResourceCaches();
        Assert.Contains(series, registration.Cleared);

        fixture.Record(); registration.Cleared.Clear();
        fixture.Chart.IsVisible = false;
        Assert.Contains(series, registration.Cleared);
        fixture.Chart.IsVisible = true; fixture.Record(); registration.Cleared.Clear();
        fixture.Chart.Series.Remove(series);
        Assert.Contains(series, registration.Cleared);
        Assert.False(series.CanShareRenderingGeometryHistory);

        fixture.Chart.Series.Add(series); fixture.Record(); registration.Cleared.Clear();
        fixture.Window.Content = null;
        Assert.Contains(series, registration.Cleared);
        Assert.False(series.CanShareRenderingGeometryHistory);
    }

    private static ScatterSeries MakeSeries() => new()
    { MarkerSize = 4, ItemsSource = Enumerable.Range(0, 16).Select(index => new ChartPoint(index, index % 7)).ToArray() };
    private sealed class ClearRegistration : IDisposable
    {
        private readonly FieldInfo _field = typeof(ChartNativeCircleBackend).GetField("_provider", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? _previous;
        public List<ChartSeries> Cleared { get; } = [];
        public ClearRegistration()
        {
            _previous = _field.GetValue(null);
            ChartNativeCircleBackend.Register((_, _, _, _, _) => null, series => Cleared.Add(series));
        }
        public void Dispose() => _field.SetValue(null, _previous);
    }
    private sealed class Fixture : IDisposable
    {
        public CartesianChart Chart { get; } = new() { ShowLegend = false, AnimationTriggers = ChartAnimationTriggers.None };
        public Window Window { get; }
        public Fixture(ChartSeries series)
        {
            Chart.Series.Add(series);
            Window = new Window { Width = 800, Height = 600, Content = Chart };
            Window.Show(); Window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); _ = Chart.PlotArea;
        }
        public DrawingGroup Record()
        {
            Window.UpdateLayout(); _ = Chart.PlotArea;
            var group = new DrawingGroup(); using (var context = group.Open()) Chart.Render(context); return group;
        }
        public void Dispose() => Window.Close();
    }
}
