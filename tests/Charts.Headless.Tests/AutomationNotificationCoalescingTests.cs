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

public sealed class AutomationNotificationCoalescingTests
{
    [AvaloniaFact]
    public void Synchronous_viewport_publication_does_not_toggle_realized_points_through_unavailable()
    {
        using var host = new Fixture();
        var points = host.Peer.GetPointPage(host.Series, 0, 64);
        var unavailable = new List<string>();
        foreach (var point in points)
            point.PropertyChanged += (_, args) =>
            {
                if (args.Property == AutomationElementIdentifiers.NameProperty || args.Property == ValuePatternIdentifiers.ValueProperty)
                    if (args.NewValue is string value && value.Contains("unavailable", StringComparison.Ordinal)) unavailable.Add(value);
            };
        var seriesPeer = Assert.Single(host.Peer.GetChildren());
        var childrenChanged = 0;
        seriesPeer.ChildrenChanged += (_, _) => childrenChanged++;
        var rootValues = new List<string>();
        host.Peer.PropertyChanged += (_, args) => { if (args.Property == ValuePatternIdentifiers.ValueProperty) rootValues.Add((string)args.NewValue!); };

        host.Chart.Viewport = new(10, 90, 0, 100);
        // Query semantics remain immediate even when transient event delivery is coalesced.
        Assert.Null(points[0].CurrentPoint);
        Assert.True(host.Chart.AccessibilitySummary.IsLayoutPending);
        _ = host.Chart.PlotArea;
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(points[0].CurrentPoint);
        Assert.True(points[0].IsOffscreen());
        Assert.Empty(unavailable);
        Assert.DoesNotContain(rootValues, value => value.Contains("layout=pending", StringComparison.Ordinal));
        Assert.Equal(0, childrenChanged);
    }

    [AvaloniaFact]
    public void Pending_scene_still_announces_unavailable_if_no_scene_is_published_in_the_dispatcher_turn()
    {
        using var host = new Fixture();
        var point = host.Peer.GetPointPeer(host.Series, 0)!;
        var values = new List<string>();
        point.PropertyChanged += (_, args) => { if (args.Property == ValuePatternIdentifiers.ValueProperty) values.Add((string)args.NewValue!); };
        host.Chart.Viewport = new(10, 90, 0, 100);
        Assert.Empty(values);
        Dispatcher.UIThread.RunJobs();
        Assert.Contains("unavailable", values);
        Assert.Null(point.CurrentPoint);
        _ = host.Chart.PlotArea;
        Assert.NotNull(point.CurrentPoint);
        Assert.Contains(values, value => value.Contains("Y=2.0", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void Real_visibility_unbinding_and_selection_changes_are_immediate()
    {
        using var host = new Fixture();
        var point = host.Peer.GetPointPeer(host.Series, 0)!;
        host.Chart.SelectionMode = ChartSelectionMode.Multiple;
        _ = host.Chart.PlotArea;
        point.Select();
        var selected = new List<bool>(); var values = new List<string>();
        point.PropertyChanged += (_, args) =>
        {
            if (args.Property == SelectionItemPatternIdentifiers.IsSelectedProperty) selected.Add((bool)args.NewValue!);
            if (args.Property == ValuePatternIdentifiers.ValueProperty) values.Add((string)args.NewValue!);
        };
        host.Chart.SelectionMode = ChartSelectionMode.None;
        Assert.Contains(false, selected);
        host.Series.IsVisible = false;
        Assert.Contains("unavailable", values);
        Assert.Null(point.CurrentPoint);
        host.Series.IsVisible = true; _ = host.Chart.PlotArea;
        values.Clear();
        host.Series.ItemsSource = null;
        Assert.Contains("unavailable", values);
        Assert.Null(point.CurrentPoint);
    }

    [AvaloniaFact]
    public void Source_replacement_with_same_identity_announces_only_the_published_value()
    {
        using var host = new Fixture();
        var point = host.Peer.GetPointPeer(host.Series, 0)!;
        var values = new List<string>();
        point.PropertyChanged += (_, args) => { if (args.Property == ValuePatternIdentifiers.ValueProperty) values.Add((string)args.NewValue!); };
        host.Series.ItemsSource = new[] { new Row("0", 0, 9) };
        Assert.Null(point.CurrentPoint);
        _ = host.Chart.PlotArea;
        Dispatcher.UIThread.RunJobs();
        Assert.Same(point, host.Peer.GetPointPeer(host.Series, 0));
        Assert.Single(values);
        Assert.Contains("Y=9.0", values[0]);
    }

    [AvaloniaFact]
    public void Deferred_availability_callback_can_publish_a_scene_and_still_announce_restoration()
    {
        using var host = new Fixture();
        var point = host.Peer.GetPointPeer(host.Series, 0)!;
        var values = new List<string>();
        var published = false;
        point.PropertyChanged += (_, args) =>
        {
            if (args.Property != ValuePatternIdentifiers.ValueProperty) return;
            var value = (string)args.NewValue!;
            values.Add(value);
            if (value != "unavailable" || published) return;
            published = true;
            _ = host.Chart.PlotArea;
        };
        host.Chart.Viewport = new(10, 90, 0, 100);
        Dispatcher.UIThread.RunJobs();
        Assert.True(published);
        Assert.Contains("unavailable", values);
        Assert.Contains("Y=2.0", values[^1]);
        Assert.NotNull(point.CurrentPoint);
        Assert.Equal(values[^1], point.Value);
    }

    [AvaloniaFact]
    public void Cached_scene_readers_do_not_revisit_every_realized_automation_point()
    {
        using var host = new Fixture();
        _ = host.Peer.GetPointPage(host.Series, 0, 128);
        for (var i = 0; i < 32; i++) Read();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++) Read();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 64 * 1024, $"Cached scene readers allocated {allocated} bytes with 128 realized peers.");

        void Read() { _ = host.Chart.PlotArea; _ = host.Chart.VisibleRange; _ = host.Chart.GetSamplingInfo(host.Series); }
    }

    [AvaloniaFact]
    public void Reentrant_viewport_callback_cannot_mark_an_obsolete_scene_as_published()
    {
        using var host = new Fixture();
        var changed = false;
        host.Chart.PropertyChanged += (_, args) =>
        {
            if (args.Property != CartesianChart.VisibleRangeProperty || changed) return;
            changed = true;
            host.Chart.Viewport = new(20, 80, 10, 90);
            host.Chart.Padding = new Thickness(25);
        };
        host.Chart.Viewport = new(10, 90, 0, 100);
        _ = host.Chart.PlotArea;
        var latest = host.Chart.VisibleRange;
        Assert.True(changed);
        Assert.Equal(new ChartViewport(20, 80, 10, 90), latest);
        Assert.True(host.Chart.PlotArea.Left >= 25);
        Assert.Equal(latest, host.Chart.VisibleRange);
    }

    [AvaloniaFact]
    public void Custom_live_axis_keeps_current_point_projection_reconciliation_on_cached_reads()
    {
        using var host = new Fixture();
        var axis = new LiveOffsetAxis { Minimum = 0, Maximum = 150 };
        host.Chart.XAxis = axis;
        _ = host.Chart.PlotArea;
        Assert.True(host.Chart.NavigateToPoint(host.Series, 50));
        var before = host.Chart.CurrentPoint!.Position;
        // Custom renderer/projection paths already support live extension-axis
        // transforms. No source/property change publishes a new scene here.
        axis.Offset = 10;
        _ = host.Chart.PlotArea;
        Assert.True(host.Chart.CurrentPoint!.Position.X > before.X);
        Assert.Equal(before.Y, host.Chart.CurrentPoint.Position.Y);
        Assert.Equal(50, host.Chart.CurrentPoint.Index);
    }

    private sealed record Row(string Id, double X, double Y);
    private sealed class LiveOffsetAxis : ChartAxis
    {
        public double Offset { get; set; }
        public override double Transform(double value) => base.Transform(value) + Offset;
        public override double Inverse(double value) => value - Offset;
    }
    private sealed class Fixture : IDisposable
    {
        public CartesianChart Chart { get; } = new()
        {
            ShowLegend = false, AnimationTriggers = ChartAnimationTriggers.None,
            XAxis = new NumericAxis { Minimum = 0, Maximum = 150, LabelFormat = "0.0" },
            YAxis = new NumericAxis { Minimum = 0, Maximum = 100, LabelFormat = "0.0" },
        };
        public LineSeries Series { get; } = new()
        {
            Title = "Data", CategoryPath = "X", ValuePath = "Y", IdPath = "Id", ShowMarkers = false,
            ItemsSource = Enumerable.Range(0, 150).Select(index => new Row(index.ToString(), index, index % 10 + 2)).ToArray(),
        };
        public ChartAutomationPeer Peer { get; }
        private Window Window { get; }
        public Fixture()
        {
            Chart.Series.Add(Series);
            Window = new Window { Width = 800, Height = 600, Content = Chart };
            Window.Show(); Window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); _ = Chart.PlotArea;
            using (var drawing = new DrawingGroup().Open()) Chart.Render(drawing);
            Dispatcher.UIThread.RunJobs();
            Peer = Assert.IsType<ChartAutomationPeer>(ControlAutomationPeer.CreatePeerForElement(Chart));
        }
        public void Dispose() => Window.Close();
    }
}
