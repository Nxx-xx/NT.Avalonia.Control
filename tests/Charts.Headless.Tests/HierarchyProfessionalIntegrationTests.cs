using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class HierarchyProfessionalIntegrationTests
{
    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void Professional_input_templates_and_readonly_notifications_do_not_retarget_a_paused_data_animation(int kind)
    {
        var clock = new Clock(); var first = new Node("a", 4); var second = new Node("b", 8); var chart = Make(kind);
        chart.ItemsSource = new[] { first, second }; chart.IdPath = "Id"; chart.AnimationTimeProvider = clock;
        chart.AnimationDuration = TimeSpan.FromSeconds(1); chart.AnimationEasing = null; chart.AnimationTriggers = ChartAnimationTriggers.Data | ChartAnimationTriggers.Viewport;
        using var host = new Host(chart); first.Value = 20;
        // Organization layout is independent of node weight. Add a real node so every family has a geometric data transition.
        chart.ItemsSource = new[] { first, second, new Node("c", 6) }; _ = chart.GetMarks(); clock.Advance(350); chart.UpdateAnimationFrame(); chart.PauseAnimations(); host.Drain();
        Assert.Equal(ChartAnimationState.Paused, chart.AnimationState); Assert.Equal(.35, chart.AnimationProgress, 8);
        var bounds = chart.GetMarks().Select(mark => mark.Bounds).ToArray(); var keys = chart.GetMarks().Select(mark => mark.Key).ToArray();
        var anchor = chart.GetMarks().First().Position;
        chart.TrackballMode = ChartTrackballMode.Vertical; chart.UpdateTrackball(anchor); host.Drain(); Assert.NotEmpty(chart.TrackballReadings); Unchanged();
        // The private mapping series inherits theme/context notifications too.
        // Its presentation events must not be reported as a new business source.
        var mappingSeries = chart.GetLogicalChildren().OfType<AnalysisSeries>().Single();
        mappingSeries.Title = "Inherited presentation update"; mappingSeries.Stroke = Brushes.Cyan;
        host.Drain(); Unchanged();
        chart.TrackballContentTemplate = new FuncDataTemplate<ChartDataTrackballContext>((_, _) => new TextBlock { Text = "Paused trackball" });
        chart.TrackballDisplayMode = ChartTrackballDisplayMode.FloatAllPoints; chart.MaxTrackballReadings = 1;
        chart.TrackballFormatter = values => $"{values.Count} actual glyphs"; chart.ShowTrackballMarkers = true; chart.TrackballMarkerSize = 12;
        chart.TrackballLineStroke = Brushes.Gold; chart.TrackballMarkerStroke = Brushes.Black; chart.TrackballMarkerFill = Brushes.White;
        chart.UpdateTrackball(anchor + new Vector(1, 0)); host.Drain(); Unchanged();
        chart.SelectedDataKeys = new object[] { "a" }; host.Drain(); Assert.Same(first, Assert.Single(chart.GetSelectedData()).Item); Unchanged();
        chart.SelectDataRange(new(0, 0, 1, 1)); host.Drain(); Assert.Equal(3, chart.GetSelectedData().Count); Unchanged();
        chart.PlotAreaContent = "Overlay";
        chart.PlotAreaContentTemplate = new FuncDataTemplate<ChartDataPlotAreaContext>((_, _) => new TextBlock { Text = "Paused plot content" });
        chart.TooltipContentTemplate = new FuncDataTemplate<ChartDataTooltipContext>((_, _) => new TextBlock { Text = "Paused tooltip" });
        chart.TooltipBackground = Brushes.Navy; chart.TooltipForeground = Brushes.White; chart.TooltipBorderBrush = Brushes.Gold;
        host.Drain(); Unchanged();
        chart.HighContrastMode = ChartHighContrastMode.Dark; host.Theme(ThemeVariant.Dark);
        chart.Foreground = Brushes.White; chart.Background = Brushes.Black; chart.GridLineBrush = Brushes.SteelBlue;
        chart.DataContext = new object(); chart.IsTooltipEnabled = false;
        chart.Palette = new IBrush[] { Brushes.Magenta, Brushes.Cyan, Brushes.Gold }; host.Drain(); Unchanged();
        Assert.Equal(Colors.Magenta, Assert.IsAssignableFrom<ISolidColorBrush>(chart.GetMarks().First(mark => Equals(mark.Key, "a")).Fill).Color);
        clock.Advance(1000); chart.UpdateAnimationFrame(); host.Drain(); Unchanged();
        chart.ResumeAnimations(); clock.Advance(650); chart.UpdateAnimationFrame(); host.Drain(); Assert.Equal(ChartAnimationState.Idle, chart.AnimationState); Assert.Equal(1, chart.AnimationProgress);
        Assert.Equal(20, chart.Snapshot!.Nodes[0].Value);
        void Unchanged()
        {
            Assert.Equal(ChartAnimationState.Paused, chart.AnimationState); Assert.Equal(.35, chart.AnimationProgress, 8);
            Assert.Equal(bounds, chart.GetMarks().Select(mark => mark.Bounds)); Assert.Equal(keys, chart.GetMarks().Select(mark => mark.Key));
            Assert.Same(first, chart.GetMarks().First(mark => Equals(mark.Key, "a")).Members[0].Item);
        }
    }

    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void Unchanged_queries_and_pointer_readings_reuse_selection_and_do_not_enumerate_or_republish_original_members(int kind)
    {
        var rows = new ObservableCollection<Node> { new("a", 4), new("b", 8) }; var source = new CountingRows(rows); var chart = Make(kind);
        chart.ItemsSource = source; chart.IdPath = "Id"; chart.TrackballMode = ChartTrackballMode.Vertical; using var host = new Host(chart);
        chart.SelectedDataKeys = new object[] { "a" }; host.Drain(); var selection = chart.GetSelectedData(); var marks = chart.GetMarks(); var reads = source.Enumerations;
        var notifications = 0; var semanticEvents = 0;
        chart.PropertyChanged += (_, args) => { if (args.Property == EngineeringChartBase.SelectedDataProperty) notifications++; };
        chart.DataSelectionChanged += (_, _) => semanticEvents++;
        for (var iteration = 0; iteration < 12; iteration++)
        {
            Assert.Same(marks, chart.GetMarks()); Assert.Same(selection, chart.GetSelectedData());
            chart.UpdateTrackball(marks[0].Position + new Vector(iteration * .1, 0)); host.Drain();
        }
        Assert.Equal(reads, source.Enumerations); Assert.Equal(0, notifications); Assert.Equal(0, semanticEvents);
    }

    [AvaloniaTheory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void Plot_content_projects_the_actual_intermediate_viewport_instead_of_the_animation_target(int kind)
    {
        var clock = new Clock(); var chart = Make(kind); chart.ItemsSource = new[] { new Node("a", 4), new Node("b", 8) }; chart.IdPath = "Id";
        chart.AnimationTimeProvider = clock; chart.AnimationDuration = TimeSpan.FromSeconds(1); chart.AnimationEasing = null;
        chart.AnimationTriggers = ChartAnimationTriggers.Viewport; chart.PlotAreaContent = "Displayed viewport";
        chart.PlotAreaContentTemplate = new FuncDataTemplate<ChartDataPlotAreaContext>((_, _) => new TextBlock { Name = "ViewportProjectionProbe", Text = "Displayed viewport" });
        using var host = new Host(chart); var before = chart.GetMarks().First(mark => Equals(mark.Key, "a")).Position;
        chart.Zoom = 2; chart.PanX = .1; chart.PanY = -.1; _ = chart.GetMarks(); clock.Advance(350); chart.UpdateAnimationFrame(); chart.PauseAnimations(); host.Drain();
        Assert.Equal(ChartAnimationState.Paused, chart.AnimationState); Assert.Equal(.35, chart.AnimationProgress, 8);
        var control = Assert.Single(chart.GetVisualDescendants().OfType<TextBlock>(), text => text.Name == "ViewportProjectionProbe");
        var context = Assert.IsType<ChartDataPlotAreaContext>(control.DataContext); var plot = chart.PlotArea;
        var projected = context.ProjectLayout(.25, .75); Assert.Equal(.1975 * plot.Width, projected.X, 7); Assert.Equal(.8025 * plot.Height, projected.Y, 7);
        var inverse = context.UnprojectLayout(projected); Assert.Equal(.25, inverse.X, 8); Assert.Equal(.75, inverse.Y, 8);
        var actual = chart.GetMarks().First(mark => Equals(mark.Key, "a")).Position;
        Assert.Equal(plot.Center.X + (before.X - plot.Center.X) * 1.35 + .035 * plot.Width, actual.X, 7);
        Assert.Equal(plot.Center.Y + (before.Y - plot.Center.Y) * 1.35 - .035 * plot.Height, actual.Y, 7);
        var local = context.ToLocal(actual); Assert.Equal(actual, context.ToChart(local));
        Assert.Equal(1 / 1.35, context.VisibleLayoutBounds.Width, 8); Assert.Equal(1 / 1.35, context.VisibleLayoutBounds.Height, 8);
        clock.Advance(1000); chart.UpdateAnimationFrame(); host.Drain(); Assert.Same(context, control.DataContext); Assert.Equal(projected, context.ProjectLayout(.25, .75));
    }

    private static HierarchyChartBase Make(int kind) => kind switch { 0 => new TreemapChart(), 1 => new SunburstChart(), 2 => new OrganizationChart(), 3 => new BubbleCloudChart(), 4 => new PackedBubbleChart(), _ => new CirclePackingChart() };
    private sealed class Node(string id, double value) : INotifyPropertyChanged
    {
        private double _value = value; public string Id { get; } = id; public string Label => Id;
        public double Value { get => _value; set { _value = value; PropertyChanged?.Invoke(this, new(nameof(Value))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    private sealed class Clock : TimeProvider
    { private long _ticks; public override long TimestampFrequency => 1000; public override long GetTimestamp() => _ticks; internal void Advance(int milliseconds) => _ticks += milliseconds; }
    private sealed class CountingRows(ObservableCollection<Node> rows) : IEnumerable, INotifyCollectionChanged
    { public int Enumerations { get; private set; } public IEnumerator GetEnumerator() { Enumerations++; return rows.GetEnumerator(); } public event NotifyCollectionChangedEventHandler? CollectionChanged { add => rows.CollectionChanged += value; remove => rows.CollectionChanged -= value; } }
    private sealed class Host : IDisposable
    {
        private readonly Window _window;
        internal Host(ChartBase chart) { _window = new() { Content = chart, Width = 640, Height = 400 }; _window.Show(); Drain(); }
        internal void Drain() { _window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); _window.UpdateLayout(); }
        internal void Theme(ThemeVariant theme) { _window.RequestedThemeVariant = theme; Drain(); }
        public void Dispose() { _window.Close(); Dispatcher.UIThread.RunJobs(); }
    }
}
