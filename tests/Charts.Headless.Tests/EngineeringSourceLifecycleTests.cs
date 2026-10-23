using System.Collections;
using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class EngineeringSourceLifecycleTests
{
    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void Root_subscription_replacement_releases_accessor_that_finishes_after_reentrant_remove(int family)
    {
        var (chart, oldItem, latestItem) = Make(family); var old = new ReentrantCollection(oldItem); var latest = new ReentrantCollection(latestItem);
        old.OnSubscribe = () => chart.ItemsSource = latest; chart.ItemsSource = old;
        using var host = new Host(chart); host.Drain(); Assert.Equal(0, old.Observers); Assert.True(latest.Observers > 0); Assert.Same(latestItem, Assert.Single(chart.GetMarks()).Members[0].Item);
        var newest = Make(family).Latest; latest.Reset(newest); host.Drain(); Assert.Same(newest, Assert.Single(chart.GetMarks()).Members[0].Item);
        host.Detach(); Assert.Equal(0, latest.Observers); Assert.Equal(0, old.Observers); host.Attach(); Assert.True(latest.Observers > 0);
    }

    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void Root_unsubscription_replacement_does_not_leave_superseded_source_or_skip_latest_notifications(int family)
    {
        var (chart, oldItem, latestItem) = Make(family); var old = new ReentrantCollection(oldItem); var intermediate = new ReentrantCollection(oldItem); var latest = new ReentrantCollection(latestItem);
        chart.ItemsSource = old; using var host = new Host(chart); Assert.True(old.Observers > 0);
        old.OnUnsubscribe = () => chart.ItemsSource = latest; chart.ItemsSource = intermediate; host.Drain(); Assert.Equal(0, old.Observers); Assert.Equal(0, intermediate.Observers); Assert.True(latest.Observers > 0); Assert.Same(latestItem, Assert.Single(chart.GetMarks()).Members[0].Item);
        host.Detach(); Assert.Equal(0, latest.Observers); Assert.Equal(0, old.Observers); Assert.Equal(0, intermediate.Observers);
    }

    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void Root_unsubscription_replacement_while_detaching_stays_unobserved_until_reattached(int family)
    {
        var (chart, oldItem, latestItem) = Make(family); var old = new ReentrantCollection(oldItem); var latest = new ReentrantCollection(latestItem);
        chart.ItemsSource = old; using var host = new Host(chart); old.OnUnsubscribe = () => chart.ItemsSource = latest;
        host.Detach(); Assert.Equal(0, old.Observers); Assert.Equal(0, latest.Observers); Assert.Same(latest, chart.ItemsSource);
        host.Attach(); Assert.True(latest.Observers > 0); Assert.Same(latestItem, Assert.Single(chart.GetMarks()).Members[0].Item);
    }

    private static (EngineeringChartBase Chart, object Old, object Latest) Make(int kind) => kind switch
    {
        0 => (new HexbinChart(), new HexbinPoint(1, 2), new HexbinPoint(3, 4)),
        1 => (new TreemapChart(), new HierarchyNode(2), new HierarchyNode(4)),
        _ => (new NetworkChart(), new GraphNode("old"), new GraphNode("latest")),
    };
    private sealed class Host : IDisposable
    {
        private readonly Window _window; private readonly EngineeringChartBase _chart;
        public Host(EngineeringChartBase chart) { _chart = chart; _window = new Window { Width = 420, Height = 280, Content = chart }; _window.Show(); Drain(); }
        public void Drain() { _window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); _window.UpdateLayout(); _ = _chart.GetMarks(); }
        public void Detach() { _window.Content = null; Drain(); }
        public void Attach() { _window.Content = _chart; Drain(); }
        public void Dispose() { _window.Close(); Dispatcher.UIThread.RunJobs(); }
    }
    private sealed class ReentrantCollection(object item) : IEnumerable, INotifyCollectionChanged
    {
        private object _item = item; private NotifyCollectionChangedEventHandler? _changed;
        public Action? OnSubscribe { get; set; } public Action? OnUnsubscribe { get; set; } public int Observers => _changed?.GetInvocationList().Length ?? 0;
        public event NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add { var callback = OnSubscribe; OnSubscribe = null; callback?.Invoke(); _changed += value; }
            remove { var callback = OnUnsubscribe; OnUnsubscribe = null; callback?.Invoke(); _changed -= value; }
        }
        public void Reset(object value) { _item = value; _changed?.Invoke(this, new(NotifyCollectionChangedAction.Reset)); }
        public IEnumerator GetEnumerator() { yield return _item; }
    }
}
