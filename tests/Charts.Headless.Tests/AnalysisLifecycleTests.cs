using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class AnalysisLifecycleTests(ITestOutputHelper output)
{
    [AvaloniaFact]
    public void Rendering_defers_selection_bindings_and_publishes_followed_identity_after_a_finite_ui_queue()
    {
        var a = new Reading("a", 80); var b = new Reading("b", 50); var c = new Reading("c", 20);
        var rows = new ObservableCollection<Reading> { a, b, c };
        var chart = new FunnelChart { ItemsSource = rows, IdPath = nameof(Reading.Id), ValuePath = nameof(Reading.Value), SelectedIndex = 1 };
        using var host = new AnalysisHost(chart);
        Key(chart, Avalonia.Input.Key.Home); Key(chart, Avalonia.Input.Key.Down);
        Assert.Same(b, chart.SelectedPoint!.Item); Assert.Same(b, chart.ActivePoint!.Item);
        var rendering = false; var duringRender = 0; var selectedEvents = 0; var activeEvents = 0; var indexEvents = 0;
        var status = new TextBlock { Text = "unchanged" };
        chart.PropertyChanged += (_, e) =>
        {
            if (e.Property != AnalysisChartBase.SelectedPointProperty && e.Property != AnalysisChartBase.ActivePointProperty && e.Property != AnalysisChartBase.SelectedIndexProperty) return;
            if (rendering) duringRender++;
            if (e.Property == AnalysisChartBase.SelectedPointProperty) { selectedEvents++; status.Text = $"{chart.SelectedPoint?.StableId}:{chart.SelectedPoint?.Point.Y}"; }
            if (e.Property == AnalysisChartBase.ActivePointProperty) activeEvents++;
            if (e.Property == AnalysisChartBase.SelectedIndexProperty) indexEvents++;
        };
        rows.Move(1, 0); b.Value = 60;
        rendering = true; Record(chart); rendering = false;
        Assert.Equal(0, duringRender); Assert.Equal(0, selectedEvents); Assert.Equal(0, activeEvents); Assert.Equal(0, indexEvents);
        Assert.Equal(1, chart.SelectedIndex); Assert.Equal("unchanged", status.Text);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(0, duringRender); Assert.Equal(1, selectedEvents); Assert.Equal(1, activeEvents); Assert.Equal(1, indexEvents);
        Assert.Equal(0, chart.SelectedIndex); Assert.Same(b, chart.SelectedPoint!.Item); Assert.Equal(60, chart.SelectedPoint.Point.Y);
        Assert.Equal("b:60", status.Text);
        var cached = chart.GetMarks();
        Record(chart); Dispatcher.UIThread.RunJobs(); Dispatcher.UIThread.RunJobs();
        Assert.Same(cached, chart.GetMarks()); Assert.Equal(1, selectedEvents); Assert.Equal(1, activeEvents); Assert.Equal(1, indexEvents);
    }

    [AvaloniaFact]
    public void Coalesced_render_publication_uses_the_latest_replacement_source_and_index()
    {
        var selected = new Reading("b", 50);
        var rows = new ObservableCollection<Reading> { new("a", 80), selected, new("c", 20) };
        var chart = new FunnelChart { ItemsSource = rows, IdPath = nameof(Reading.Id), ValuePath = nameof(Reading.Value), SelectedIndex = 1 };
        using var host = new AnalysisHost(chart);
        Assert.Same(selected, chart.SelectedPoint!.Item);
        var published = new List<(int Index, double Value)>();
        chart.PropertyChanged += (_, e) => { if (e.Property == AnalysisChartBase.SelectedPointProperty && chart.SelectedPoint is { } hit) published.Add((hit.Index, hit.Point.Y)); };
        rows.Move(1, 0); selected.Value = 60; Record(chart);
        var latest = new Reading("b", 95);
        chart.ItemsSource = new ObservableCollection<Reading> { new("c", 30), new("a", 85), latest };
        Record(chart);
        Assert.Empty(published); Assert.Equal(1, chart.SelectedIndex);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(new[] { (2, 95d) }, published); Assert.Equal(2, chart.SelectedIndex); Assert.Same(latest, chart.SelectedPoint!.Item);
        Assert.Equal(0, selected.Observers); Assert.Equal(1, latest.Observers);
        Dispatcher.UIThread.RunJobs(); Assert.Single(published);
    }

    [AvaloniaFact]
    public void External_query_can_publish_pending_selection_synchronously_without_duplicate_queued_notifications()
    {
        var row = new Reading("same", 50);
        var chart = new FunnelChart { ItemsSource = new[] { row }, IdPath = nameof(Reading.Id), ValuePath = nameof(Reading.Value), SelectedIndex = 0 };
        using var host = new AnalysisHost(chart);
        Assert.Same(row, chart.SelectedPoint!.Item);
        var events = 0;
        chart.PropertyChanged += (_, e) => { if (e.Property == AnalysisChartBase.SelectedPointProperty) events++; };
        row.Value = 75; Record(chart);
        Assert.Equal(0, events);
        Assert.Equal(75, chart.SelectedPoint!.Point.Y); Assert.Equal(1, events);
        Dispatcher.UIThread.RunJobs(); Assert.Equal(1, events);
    }

    [AvaloniaFact]
    public void Selection_binding_reentry_can_replace_the_source_during_deferred_publication()
    {
        var row = new Reading("same", 50); var latest = new Reading("same", 90);
        var chart = new FunnelChart { ItemsSource = new[] { row }, IdPath = nameof(Reading.Id), ValuePath = nameof(Reading.Value), SelectedIndex = 0 };
        using var host = new AnalysisHost(chart);
        Assert.Same(row, chart.SelectedPoint!.Item);
        var values = new List<double>();
        chart.PropertyChanged += (_, e) =>
        {
            if (e.Property != AnalysisChartBase.SelectedPointProperty || chart.SelectedPoint is not { } hit) return;
            values.Add(hit.Point.Y);
            if (hit.Point.Y == 60) chart.ItemsSource = new[] { latest };
        };
        row.Value = 60; Record(chart); Assert.Empty(values);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(new[] { 60d, 90d }, values); Assert.Same(latest, chart.SelectedPoint!.Item);
        Assert.Equal(0, row.Observers); Assert.Equal(1, latest.Observers);
        Dispatcher.UIThread.RunJobs(); Assert.Equal(2, values.Count);
    }

    [AvaloniaFact]
    public void Selected_index_default_binding_is_two_way_for_real_source_and_target_updates()
    {
        var model = new SelectionModel();
        var chart = new FunnelChart { ItemsSource = new[] { 60d, 40, 20 } };
        chart.Bind(AnalysisChartBase.SelectedIndexProperty, new Binding(nameof(SelectionModel.Index)) { Source = model });
        using var host = new AnalysisHost(chart);
        Assert.Equal(0, chart.SelectedIndex);
        chart.SetCurrentValue(AnalysisChartBase.SelectedIndexProperty, 1);
        Assert.Equal(1, model.Index); Assert.Equal(1, chart.SelectedPoint!.Index);
        model.Index = 2;
        Assert.Equal(2, chart.SelectedIndex); Assert.Equal(2, chart.SelectedPoint!.Index);
    }

    [AvaloniaFact]
    public void Source_changed_by_a_custom_builder_does_not_publish_intermediate_selected_state()
    {
        var row = new Reading("same", 50); var latest = new Reading("same", 95);
        var chart = new ReentrantBuilder { ItemsSource = new[] { row }, IdPath = nameof(Reading.Id), ValuePath = nameof(Reading.Value), SelectedIndex = 0 };
        using var host = new AnalysisHost(chart);
        Assert.Same(row, chart.SelectedPoint!.Item);
        var published = new List<double>();
        chart.PropertyChanged += (_, e) => { if (e.Property == AnalysisChartBase.SelectedPointProperty && chart.SelectedPoint is { } point) published.Add(point.Point.Y); };
        row.Value = 60;
        chart.AfterBuild = () => chart.ItemsSource = new[] { latest };
        var intermediate = chart.GetMarks();
        Assert.Equal(60, Assert.Single(intermediate).Data.Point.Y);
        Assert.Empty(published);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(new[] { 95d }, published); Assert.Same(latest, chart.SelectedPoint!.Item);
        Assert.Equal(0, row.Observers); Assert.Equal(1, latest.Observers);
        Dispatcher.UIThread.RunJobs(); Assert.Single(published);
    }

    private static void Record(AnalysisChartBase chart)
    { var group = new DrawingGroup(); using var context = group.Open(); chart.Render(context); }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Analytical_extension_marks_hit_the_transformed_shape_and_preserve_unpainted_corners(bool polygon)
    {
        Geometry shape = polygon ? new PolylineGeometry([new(0, 0), new(50, 0), new(0, 50)], true)
            : new RectangleGeometry(new Rect(0, 0, 50, 50));
        shape.Transform = new MatrixTransform(new Matrix(0, 1, -1, 0, 150, 60));
        var chart = new CustomAnalysis(shape) { Width = 200, Height = 200, Padding = new(0), ItemsSource = new[] { 1d } };
        using var host = new AnalysisHost(chart);
        Assert.IsType<ChartPointHit>(chart.HitTest(new Point(140, 70)));
        Assert.Null(chart.HitTest(new Point(10, 10)));
        if (polygon) Assert.Null(chart.HitTest(new Point(110, 100)));
        else Assert.IsType<ChartPointHit>(chart.HitTest(new Point(110, 100)));
        Assert.Equal(new Matrix(0, 1, -1, 0, 150, 60), shape.Transform.Value);
    }

    [AvaloniaFact]
    public void Selected_and_active_business_identity_survive_reorder_replacement_and_removed_rows_clear_selection()
    {
        var a = new Reading("a", 80); var b = new Reading("b", 50); var c = new Reading("c", 20);
        var rows = new ObservableCollection<Reading> { a, b, c };
        var chart = new FunnelChart { ItemsSource = rows, IdPath = nameof(Reading.Id), ValuePath = nameof(Reading.Value), SelectedIndex = 1 };
        using var host = new AnalysisHost(chart);
        Key(chart, Avalonia.Input.Key.Home); Key(chart, Avalonia.Input.Key.Down);
        Assert.Same(b, chart.ActivePoint!.Item); Assert.Same(b, chart.SelectedPoint!.Item);
        rows.Move(1, 0); host.Drain();
        Assert.Equal(0, chart.SelectedIndex); Assert.Same(b, chart.SelectedPoint!.Item); Assert.Same(b, chart.ActivePoint!.Item);
        var replaced = new Reading("b", 65); rows[0] = replaced; host.Drain();
        Assert.Same(replaced, chart.SelectedPoint!.Item); Assert.Same(replaced, chart.ActivePoint!.Item);
        Assert.Equal(65, chart.SelectedPoint.Point.Y); Assert.Equal(0, b.Observers); Assert.Equal(1, replaced.Observers);
        rows.RemoveAt(0); host.Drain();
        Assert.Equal(-1, chart.SelectedIndex); Assert.Null(chart.SelectedPoint); Assert.Null(chart.ActivePoint);
        Assert.Equal(0, replaced.Observers);
    }

    [AvaloniaFact]
    public void Keyboard_skips_unpainted_values_selects_real_rows_and_escape_resets_both_states()
    {
        var chart = new FunnelChart { ItemsSource = new[] { 0d, 30, -1, double.NaN, 80 }, IsSelectionEnabled = true };
        using var host = new AnalysisHost(chart);
        Key(chart, Avalonia.Input.Key.Home); Assert.Equal(1, chart.ActivePoint!.Index);
        Key(chart, Avalonia.Input.Key.Down); Assert.Equal(4, chart.ActivePoint!.Index);
        Key(chart, Avalonia.Input.Key.Enter); Assert.Equal(4, chart.SelectedIndex); Assert.Equal(4, chart.SelectedPoint!.Index);
        Key(chart, Avalonia.Input.Key.Right); Assert.Equal(1, chart.ActivePoint!.Index);
        Key(chart, Avalonia.Input.Key.End); Assert.Equal(4, chart.ActivePoint!.Index);
        Key(chart, Avalonia.Input.Key.Up); Assert.Equal(1, chart.ActivePoint!.Index);
        chart.IsSelectionEnabled = false; Key(chart, Avalonia.Input.Key.Space); Assert.Equal(4, chart.SelectedIndex);
        Key(chart, Avalonia.Input.Key.Escape);
        Assert.Null(chart.ActivePoint); Assert.Null(chart.SelectedPoint); Assert.Equal(-1, chart.SelectedIndex);
    }

    [AvaloniaFact]
    public void Failed_export_reports_original_owner_and_does_not_keep_business_items_observed_after_unload()
    {
        var row = new Reading("export", 60);
        var chart = new FunnelChart { ItemsSource = new ObservableCollection<Reading> { row }, ValuePath = nameof(Reading.Value), IdPath = nameof(Reading.Id) };
        var window = new Window { Width = 320, Height = 180, Content = chart };
        var events = 0; object? sender = null; ChartExportFailedEventArgs? failure = null;
        chart.ExportFailed += (s, e) => { events++; sender = s; failure = e; };
        try
        {
            window.Show(); Drain(window); Assert.Equal(1, row.Observers);
            using var stream = new MemoryStream();
            Assert.False(chart.TryExportPng(stream, new ChartExportOptions { Dpi = 0 }));
            Assert.Equal(1, events); Assert.Same(chart, sender); Assert.NotNull(failure); Assert.Equal(0, stream.Length);
            Assert.Equal(1, row.Observers); Assert.Single(chart.GetMarks());
            window.Content = null; Drain(window);
            Assert.Equal(0, row.Observers); Assert.Null(((ILogical)chart.Series).LogicalParent);
            _ = chart.GetMarks(); Assert.Equal(0, row.Observers);
        }
        finally { window.Close(); Dispatcher.UIThread.RunJobs(); }
    }

    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Inherited_source_binding_can_detach_reattach_and_replace_source_without_retaining_observers(bool reattach, bool replace)
    {
        var original = new Reading("same", 20);
        var replacement = new Reading("same", 80);
        var initialSource = new ObservableCollection<Reading> { original };
        var replacementSource = new ObservableCollection<Reading> { replacement };
        var model = new Model(initialSource);
        var chart = new FunnelChart
        {
            Width = 300, Height = 100, Padding = new(0), DataContext = model,
            ValuePath = nameof(Reading.Value), IdPath = nameof(Reading.Id)
        };
        // This is an inherited DataContext binding on the public Series, rather than
        // an explicit Source binding that could remain alive independently of ownership.
        chart.Series.Bind(ChartSeries.ItemsSourceProperty, new Binding(nameof(Model.Items)));
        var window = new Window { Width = 300, Height = 100 };
        var changed = false;
        var mountingReturned = false;
        var callbackWasSynchronous = false;
        var callbackInheritedContext = false;
        var callbackCompleted = false;
        Exception? callbackFailure = null;
        void State(string stage) => output.WriteLine($"{stage}: content={ReferenceEquals(window.Content, chart)}, visual={chart.IsAttachedToVisualTree()}, logical={ReferenceEquals(((ILogical)chart.Series).LogicalParent, chart)}, sourceInitial={ReferenceEquals(chart.Series.ItemsSource, initialSource)}, sourceReplacement={ReferenceEquals(chart.Series.ItemsSource, replacementSource)}, originalObservers={original.Observers}, replacementObservers={replacement.Observers}, modelObservers={model.Observers}");
        chart.AttachedToVisualTree += (_, _) => State("visual attached");
        chart.DetachedFromVisualTree += (_, _) => State("visual detached");
        chart.Series.PropertyChanged += (_, e) =>
        {
            if (changed || e.Property != ChartSeries.ItemsSourceProperty || !ReferenceEquals(chart.Series.ItemsSource, initialSource)) return;
            changed = true;
            State("binding before mutation");
            callbackWasSynchronous = !mountingReturned;
            callbackInheritedContext = ReferenceEquals(chart.Series.DataContext, model);
            // In Avalonia the inherited binding runs before LogicalParent is assigned.
            // Capture callback errors explicitly: the binding engine otherwise reports
            // them as a binding error, hiding a mutation that never actually completed.
            try
            {
                window.Content = null;
                State("binding after detach");
                if (replace) model.Items = replacementSource;
                if (reattach) window.Content = chart;
                State("binding after final mutation");
                callbackCompleted = true;
            }
            catch (Exception exception) { callbackFailure = exception; }
        };
        try
        {
            window.Content = chart;
            window.Show();
            mountingReturned = true;
            Drain(window);
            State("after mount drain");
            Assert.True(changed); Assert.True(callbackWasSynchronous); Assert.True(callbackInheritedContext);
            Assert.Null(callbackFailure); Assert.True(callbackCompleted);
            var current = replace ? replacement : original;
            if (reattach)
            {
                Assert.Same(chart, window.Content); Assert.True(chart.IsAttachedToVisualTree());
                Assert.Same(chart, ((ILogical)chart.Series).LogicalParent);
                Assert.Equal(1, current.Observers); Assert.Equal(1, model.Observers);
                Assert.Same(current, Assert.Single(chart.GetMarks()).Data.Item);
                Key(chart, Avalonia.Input.Key.Home); Assert.Same(current, chart.ActivePoint?.Item);
                current.Value = 60; Drain(window); Assert.Equal(60, chart.ActivePoint?.Point.Y);
                window.Content = null; Drain(window);
            }
            Assert.Null(window.Content); Assert.False(chart.IsAttachedToVisualTree());
            Assert.Null(((ILogical)chart.Series).LogicalParent); Assert.Null(chart.ActivePoint);
            Assert.Equal(0, original.Observers); Assert.Equal(0, replacement.Observers); Assert.Equal(0, model.Observers);

            // A second ordinary attach/detach also exposes a duplicated Series.Changed
            // subscription: removing one handler would leave the business source observed.
            window.Content = chart; Drain(window);
            Assert.True(chart.IsAttachedToVisualTree()); Assert.Equal(1, current.Observers); Assert.Equal(1, model.Observers);
            Key(chart, Avalonia.Input.Key.Home); Assert.Same(current, chart.ActivePoint?.Item);
            current.Value = 35; Drain(window); Assert.Equal(35, chart.ActivePoint?.Point.Y);
            window.Content = null; Drain(window);
            Assert.Null(chart.ActivePoint); Assert.Null(((ILogical)chart.Series).LogicalParent);
            Assert.Equal(0, original.Observers); Assert.Equal(0, replacement.Observers); Assert.Equal(0, model.Observers);
        }
        finally { window.Close(); Dispatcher.UIThread.RunJobs(); }
    }

    private static void Drain(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static void Key(FunnelChart chart, Key key) => chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });

    private sealed class AnalysisHost : IDisposable
    {
        private readonly Window _window;
        private readonly AnalysisChartBase _chart;
        public AnalysisHost(AnalysisChartBase chart) { _chart = chart; _window = new Window { Width = 400, Height = 280, Content = chart }; _window.Show(); Drain(); }
        public void Drain()
        {
            AnalysisLifecycleTests.Drain(_window);
            // Layout does not execute the Headless renderer. Reproduce a real frame so
            // stable identity reconciliation and SelectedIndex describe painted marks.
            _ = _chart.GetMarks();
            var recording = new DrawingGroup(); using var context = recording.Open(); _chart.Render(context);
        }
        public void Dispose() { _window.Close(); Dispatcher.UIThread.RunJobs(); }
    }

    private sealed class CustomAnalysis(Geometry shape) : AnalysisChartBase
    {
        protected override IReadOnlyList<AnalysisMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot) =>
            data.Points.Select(point => new AnalysisMarkInfo(point, shape, Brushes.Blue, new Point(140, 70))).ToArray();
    }

    private sealed class ReentrantBuilder : AnalysisChartBase
    {
        public Action? AfterBuild { get; set; }
        protected override IReadOnlyList<AnalysisMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
        {
            var result = data.Points.Select(point => new AnalysisMarkInfo(point, new RectangleGeometry(new Rect(0, 0, 50, 50)), Brushes.Blue, new Point(25, 25))).ToArray();
            var action = AfterBuild; AfterBuild = null; action?.Invoke();
            return result;
        }
    }

    private sealed class SelectionModel : INotifyPropertyChanged
    {
        private int _index;
        public int Index { get => _index; set { if (_index == value) return; _index = value; PropertyChanged?.Invoke(this, new(nameof(Index))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private sealed class Model(ObservableCollection<Reading> items) : INotifyPropertyChanged
    {
        private ObservableCollection<Reading> _items = items;
        private PropertyChangedEventHandler? _changed;
        public int Observers { get; private set; }
        public ObservableCollection<Reading> Items { get => _items; set { _items = value; _changed?.Invoke(this, new(nameof(Items))); } }
        public event PropertyChangedEventHandler? PropertyChanged { add { Observers++; _changed += value; } remove { Observers--; _changed -= value; } }
    }

    private sealed class Reading(string id, double value) : INotifyPropertyChanged
    {
        private double _value = value;
        private PropertyChangedEventHandler? _changed;
        public string Id { get; } = id;
        public int Observers { get; private set; }
        public double Value { get => _value; set { _value = value; _changed?.Invoke(this, new(nameof(Value))); } }
        public event PropertyChangedEventHandler? PropertyChanged { add { Observers++; _changed += value; } remove { Observers--; _changed -= value; } }
    }
}
