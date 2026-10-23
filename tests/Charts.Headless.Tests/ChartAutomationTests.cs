using System.Collections;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class ChartAutomationTests
{
    [AvaloniaFact]
    public void Summary_is_read_only_honors_host_name_and_series_children_are_lazy()
    {
        var source = new CountingSource(100_000);
        var series = new LineSeries { Title = "Samples", ItemsSource = source };
        var chart = new CartesianChart { Title = "Telemetry" };
        chart.Series.Add(series);
        var enumerations = source.Enumerations;
        var peer = Assert.IsType<ChartAutomationPeer>(ControlAutomationPeer.CreatePeerForElement(chart));
        AutomationProperties.SetName(chart, "Engine telemetry");
        Assert.Equal("Engine telemetry", peer.GetName());
        Assert.Equal(AutomationControlType.Group, peer.GetAutomationControlType());
        var value = Assert.IsAssignableFrom<IValueProvider>(peer.GetProvider<IValueProvider>());
        Assert.True(value.IsReadOnly);
        Assert.Contains("Telemetry", value.Value);
        Assert.Contains("series=1", value.Value);
        Assert.Contains("layout=pending", value.Value);
        Assert.Throws<InvalidOperationException>(() => value.SetValue("write"));
        Assert.Equal(0, peer.CachedSeriesPeerCount);
        var child = Assert.Single(peer.GetChildren());
        Assert.Equal(1, peer.CachedSeriesPeerCount);
        Assert.Empty(child.GetChildren());
        Assert.Same(peer, child.GetParent());
        Assert.Equal(enumerations, source.Enumerations);
    }

    [AvaloniaFact]
    public void Series_toggle_uses_visibility_state_and_cancellable_legend_command()
    {
        var series = new LineSeries { Title = "Revenue", ItemsSource = new ChartPoint[] { new(0, 0), new(1, 1), new(2, 2) } };
        using var fixture = new Fixture(series);
        var peer = fixture.Peer;
        Assert.Contains("rows=3", peer.Value);
        var child = Assert.Single(peer.GetChildren());
        Assert.Equal(AutomationControlType.CheckBox, child.GetAutomationControlType());
        Assert.Equal("Revenue", child.GetName());
        var toggle = Assert.IsAssignableFrom<IToggleProvider>(child.GetProvider<IToggleProvider>());
        var stateChanges = 0;
        var valueChanges = new List<string>();
        child.PropertyChanged += (_, args) => { if (args.Property == TogglePatternIdentifiers.ToggleStateProperty) stateChanges++; };
        peer.PropertyChanged += (_, args) => { if (args.Property == ValuePatternIdentifiers.ValueProperty) valueChanges.Add((string)args.NewValue!); };
        Assert.Equal(ToggleState.On, toggle.ToggleState);
        toggle.Toggle();
        Assert.False(series.IsVisible);
        Assert.Equal(ToggleState.Off, toggle.ToggleState);
        Assert.Contains("visible=0", peer.Value);
        Assert.Equal(1, stateChanges);
        Assert.Contains(valueChanges, value => value.Contains("visible=0", StringComparison.Ordinal));
        _ = fixture.Chart.PlotArea;
        Assert.False(child.IsOffscreen()); // The dimmed legend checkbox remains on screen.
        fixture.Chart.LegendItemInvoked += (_, args) => args.Cancel = true;
        toggle.Toggle();
        Assert.False(series.IsVisible);
    }

    [AvaloniaFact]
    public void Series_identity_survives_reordering_and_removed_peer_cannot_toggle()
    {
        var first = new LineSeries { Title = "First" };
        var last = new ScatterSeries { Title = "Last" };
        using var fixture = new Fixture(first, last);
        var children = fixture.Peer.GetChildren();
        var firstPeer = children[0]; var lastPeer = children[1];
        fixture.Chart.Series.Move(0, 1);
        children = fixture.Peer.GetChildren();
        Assert.Same(lastPeer, children[0]); Assert.Same(firstPeer, children[1]);
        fixture.Chart.Series.Remove(first);
        Assert.Single(fixture.Peer.GetChildren());
        Assert.Equal(1, fixture.Peer.CachedSeriesPeerCount);
        Assert.False(firstPeer.IsEnabled());
        Assert.ThrowsAny<Exception>(() => firstPeer.GetProvider<IToggleProvider>()!.Toggle());
    }

    [AvaloniaFact]
    public void Bound_series_collection_replacement_refreshes_accessible_children()
    {
        using var fixture = new Fixture();
        var first = new LineSeries { Title = "First" };
        var items = new ObservableCollection<ChartSeries> { first };
        fixture.Chart.SeriesSource = items;
        var removed = Assert.Single(fixture.Peer.GetChildren());
        items[0] = new ScatterSeries { Title = "Replacement" };
        Assert.Equal("Replacement", Assert.Single(fixture.Peer.GetChildren()).GetName());
        Assert.False(removed.IsEnabled());
    }

    [AvaloniaFact]
    public void Disabled_chart_or_legend_interaction_rejects_series_toggle()
    {
        var series = new LineSeries { Title = "Series" };
        using var fixture = new Fixture(series);
        var child = Assert.Single(fixture.Peer.GetChildren());
        var toggle = child.GetProvider<IToggleProvider>()!;
        fixture.Chart.IsLegendInteractive = false;
        Assert.False(child.IsEnabled());
        Assert.ThrowsAny<Exception>(toggle.Toggle);
        fixture.Chart.IsLegendInteractive = true;
        fixture.Chart.IsEnabled = false;
        Assert.False(child.IsEnabled());
        Assert.ThrowsAny<Exception>(toggle.Toggle);
        Assert.True(series.IsVisible);
    }

    private sealed class CountingSource(int count) : IEnumerable<ChartPoint>
    {
        public int Enumerations { get; private set; }
        public IEnumerator<ChartPoint> GetEnumerator()
        {
            Enumerations++;
            for (var index = 0; index < count; index++) yield return new(index, index);
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class Fixture : IDisposable
    {
        public CartesianChart Chart { get; } = new();
        private Window Window { get; }
        public ChartAutomationPeer Peer { get; }
        public Fixture(params ChartSeries[] series)
        {
            foreach (var item in series) Chart.Series.Add(item);
            Window = new Window { Width = 800, Height = 600, Content = Chart };
            Window.Show(); Window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
            using (var drawing = new DrawingGroup().Open()) Chart.Render(drawing);
            _ = Chart.PlotArea;
            Peer = Assert.IsType<ChartAutomationPeer>(ControlAutomationPeer.CreatePeerForElement(Chart));
        }
        public void Dispose() => Window.Close();
    }
}
