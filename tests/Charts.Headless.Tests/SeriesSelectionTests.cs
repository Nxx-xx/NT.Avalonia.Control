using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class SeriesSelectionTests
{
    [AvaloniaFact]
    public void Default_none_preserves_point_selection_and_does_not_select_a_series()
    {
        using var fixture = new Fixture(ChartSelectionMode.None);
        fixture.Click(fixture.Point(7, 80));
        Assert.Same(fixture.Scatter, fixture.Chart.SelectedPoint!.Series);
        Assert.Empty(fixture.Chart.SelectedSeries ?? []);
        Assert.False(fixture.Chart.SelectSeries(fixture.Scatter));
    }

    [AvaloniaTheory]
    [InlineData("line")]
    [InlineData("bar")]
    [InlineData("scatter")]
    public void Pointer_selects_the_original_series_and_point_selection_remains_independent(string kind)
    {
        using var fixture = new Fixture();
        var (series, point) = kind switch
        {
            "line" => ((ChartSeries)fixture.Line, new ChartPoint(1, 20)),
            "bar" => (fixture.Bar, new ChartPoint(3, 40)),
            _ => (fixture.Scatter, new ChartPoint(7, 80)),
        };
        ChartSeriesSelectionChangedEventArgs? change = null;
        fixture.Chart.SeriesSelectionChanged += (_, args) => change = args;
        fixture.Click(fixture.Point(point.X, point.Y));
        Assert.Same(series, Assert.Single(fixture.Chart.SelectedSeries!));
        Assert.Same(series, fixture.Chart.SelectedPoint!.Series);
        Assert.Equal(ChartSelectionChangeReason.Pointer, change?.Reason);
        fixture.Chart.ClearSelection();
        Assert.Null(fixture.Chart.SelectedPoint);
        Assert.Same(series, Assert.Single(fixture.Chart.SelectedSeries!));
        fixture.Chart.ClearSeriesSelection();
        Assert.Empty(fixture.Chart.SelectedSeries!);
    }

    [AvaloniaFact]
    public void A_line_segment_can_select_its_series_without_inventing_a_selected_sample()
    {
        using var fixture = new Fixture();
        fixture.Click(fixture.Point(5, 20));
        Assert.Same(fixture.Line, Assert.Single(fixture.Chart.SelectedSeries!));
        Assert.Null(fixture.Chart.SelectedPoint);
        Assert.Empty(fixture.Chart.SelectedPoints!);
    }

    [AvaloniaFact]
    public void SingleDeselect_toggles_clicks_but_explicit_selection_is_idempotent()
    {
        using var fixture = new Fixture(ChartSelectionMode.SingleDeselect);
        fixture.Click(fixture.Point(7, 80));
        Assert.Same(fixture.Scatter, Assert.Single(fixture.Chart.SelectedSeries!));
        fixture.Click(fixture.Point(7, 80));
        Assert.Empty(fixture.Chart.SelectedSeries!);
        Assert.True(fixture.Chart.SelectSeries(fixture.Scatter));
        var state = fixture.Chart.SelectedSeries;
        Assert.True(fixture.Chart.SelectSeries(fixture.Scatter));
        Assert.Same(state, fixture.Chart.SelectedSeries);
    }

    [AvaloniaFact]
    public void Multiple_ctrl_click_toggles_while_plain_click_replaces_and_empty_space_clears()
    {
        using var fixture = new Fixture(ChartSelectionMode.Multiple);
        fixture.Click(fixture.Point(1, 20));
        fixture.Click(fixture.Point(7, 80), RawInputModifiers.Control);
        Assert.Equal(new ChartSeries[] { fixture.Line, fixture.Scatter }, fixture.Chart.SelectedSeries!);
        fixture.Click(fixture.Point(1, 20), RawInputModifiers.Control);
        Assert.Same(fixture.Scatter, Assert.Single(fixture.Chart.SelectedSeries!));
        fixture.Click(fixture.Point(3, 40));
        Assert.Same(fixture.Bar, Assert.Single(fixture.Chart.SelectedSeries!));
        fixture.Click(fixture.Point(9, 95));
        Assert.Empty(fixture.Chart.SelectedSeries!);
    }

    [AvaloniaFact]
    public void Programmatic_additive_selection_is_reference_identified_immutable_and_normalizes_external_values()
    {
        using var fixture = new Fixture(ChartSelectionMode.Multiple);
        var chart = fixture.Chart;
        chart.SelectSeries(fixture.Line);
        var previous = chart.SelectedSeries!;
        chart.SelectSeries(fixture.Bar, additive: true);
        Assert.Same(fixture.Line, Assert.Single(previous));
        Assert.Equal(new ChartSeries[] { fixture.Line, fixture.Bar }, chart.SelectedSeries!);
        Assert.Throws<NotSupportedException>(() => ((IList<ChartSeries>)chart.SelectedSeries!).Clear());
        var external = new ScatterSeries();
        fixture.Scatter.IsVisible = false;
        chart.SelectedSeries = new ChartSeries[] { external, null!, fixture.Line, fixture.Line, fixture.Scatter, fixture.Bar };
        Assert.Equal(new ChartSeries[] { fixture.Line, fixture.Bar }, chart.SelectedSeries!);
        Assert.False(chart.SelectSeries(external));
        Assert.False(chart.SelectSeries(fixture.Scatter));
        Assert.Throws<ArgumentNullException>(() => chart.SelectSeries(null!));
        chart.SeriesSelectionMode = ChartSelectionMode.Single;
        Assert.Same(fixture.Line, Assert.Single(chart.SelectedSeries!));
    }

    [AvaloniaTheory]
    [InlineData("hide")]
    [InlineData("remove")]
    [InlineData("none")]
    [InlineData("single")]
    public void Mandatory_reconciliation_and_mode_changes_cannot_be_canceled_or_resurrect_invalid_series(string action)
    {
        using var fixture = new Fixture(ChartSelectionMode.Multiple);
        var chart = fixture.Chart;
        chart.SelectSeries(fixture.Line); chart.SelectSeries(fixture.Scatter, additive: true);
        var old = chart.SelectedSeries!;
        var changes = new List<ChartSeriesSelectionChangingEventArgs>();
        chart.SeriesSelectionChanging += (_, args) =>
        {
            changes.Add(args); args.Cancel = true;
            chart.SelectedSeries = old;
        };
        switch (action)
        {
            case "hide": fixture.Scatter.IsVisible = false; break;
            case "remove": chart.Series.Remove(fixture.Scatter); break;
            case "none": chart.SeriesSelectionMode = ChartSelectionMode.None; break;
            case "single": chart.SeriesSelectionMode = ChartSelectionMode.Single; break;
        }
        fixture.Drain();
        Assert.NotEmpty(changes);
        Assert.All(changes, value => Assert.False(value.CanCancel));
        Assert.Equal(action is "hide" or "remove" ? ChartSelectionChangeReason.Reconcile : ChartSelectionChangeReason.Configuration, changes[0].Reason);
        if (action == "none") Assert.Empty(chart.SelectedSeries!);
        else Assert.Same(fixture.Line, Assert.Single(chart.SelectedSeries!));
    }

    [AvaloniaFact]
    public void Replacing_sources_keeps_surviving_series_instances_and_detach_does_not_drop_business_selection()
    {
        using var fixture = new Fixture(ChartSelectionMode.Multiple);
        var chart = fixture.Chart;
        chart.SelectSeries(fixture.Line); chart.SelectSeries(fixture.Scatter, additive: true);
        fixture.Line.ItemsSource = new[] { new ChartPoint(2, 25), new ChartPoint(8, 25) };
        chart.SeriesSource = new ChartSeries[] { fixture.Line, fixture.Scatter };
        fixture.Drain();
        Assert.Equal(new ChartSeries[] { fixture.Line, fixture.Scatter }, chart.SelectedSeries!);
        chart.IsEnabled = false; chart.IsVisible = false; fixture.Drain();
        Assert.Equal(2, chart.SelectedSeries!.Count);
        chart.IsEnabled = true; chart.IsVisible = true;
        fixture.Window.Content = null;
        Assert.Equal(2, chart.SelectedSeries!.Count);
        fixture.Window.Content = chart; fixture.Drain();
        chart.SeriesSource = new[] { fixture.Line };
        fixture.Drain();
        Assert.Same(fixture.Line, Assert.Single(chart.SelectedSeries!));
    }

    [AvaloniaFact]
    public void Canceled_pointer_clear_and_external_requests_retain_the_same_canonical_list()
    {
        using var fixture = new Fixture();
        var chart = fixture.Chart; chart.SelectSeries(fixture.Line);
        var previous = chart.SelectedSeries;
        var reasons = new List<ChartSelectionChangeReason>(); var changed = 0;
        chart.SeriesSelectionChanged += (_, _) => changed++;
        chart.SeriesSelectionChanging += (_, args) =>
        {
            Assert.True(args.CanCancel); Assert.Same(previous, args.OldSelection);
            reasons.Add(args.Reason); args.Cancel = true;
        };
        fixture.Click(fixture.Point(7, 80));
        Assert.False(chart.ClearSeriesSelection());
        Assert.False(chart.SelectSeries(fixture.Bar));
        Assert.Equal(new[] { ChartSelectionChangeReason.Pointer, ChartSelectionChangeReason.Clear, ChartSelectionChangeReason.ExternalValue }, reasons);
        Assert.Same(previous, chart.SelectedSeries);
        Assert.Equal(0, changed);
        Assert.Same(fixture.Scatter, chart.SelectedPoint!.Series);
    }

    [AvaloniaFact]
    public void A_nested_selection_supersedes_the_outer_request_without_recursive_changing()
    {
        using var fixture = new Fixture();
        var chart = fixture.Chart; chart.SelectSeries(fixture.Line);
        var changing = 0; var changed = 0;
        chart.SeriesSelectionChanging += (_, args) =>
        {
            changing++; chart.SelectSeries(fixture.Scatter); args.Cancel = true;
        };
        chart.SeriesSelectionChanged += (_, _) => changed++;
        Assert.False(chart.SelectSeries(fixture.Bar));
        Assert.Equal(1, changing); Assert.Equal(1, changed);
        Assert.Same(fixture.Scatter, Assert.Single(chart.SelectedSeries!));
    }

    [AvaloniaFact]
    public void A_callback_removing_the_proposed_series_keeps_the_previous_valid_selection()
    {
        using var fixture = new Fixture();
        var chart = fixture.Chart; chart.SelectSeries(fixture.Line);
        var changed = 0;
        chart.SeriesSelectionChanged += (_, _) => changed++;
        chart.SeriesSelectionChanging += (_, _) => chart.Series.Remove(fixture.Bar);
        Assert.False(chart.SelectSeries(fixture.Bar));
        Assert.Same(fixture.Line, Assert.Single(chart.SelectedSeries!));
        Assert.Equal(0, changed);
    }

    [AvaloniaFact]
    public void Callback_exceptions_restore_state_and_do_not_poison_later_changes()
    {
        using var fixture = new Fixture();
        var chart = fixture.Chart; chart.SelectSeries(fixture.Line);
        void Throw(object? sender, ChartSeriesSelectionChangingEventArgs args) => throw new InvalidOperationException("business callback");
        chart.SeriesSelectionChanging += Throw;
        Assert.Throws<InvalidOperationException>(() => chart.SelectSeries(fixture.Bar));
        Assert.Same(fixture.Line, Assert.Single(chart.SelectedSeries!));
        chart.SeriesSelectionChanging -= Throw;
        Assert.True(chart.SelectSeries(fixture.Scatter));
        Assert.Same(fixture.Scatter, Assert.Single(chart.SelectedSeries!));
    }

    [AvaloniaFact]
    public void Canceled_or_normalized_external_values_update_the_two_way_model_and_preserve_the_binding()
    {
        using var fixture = new Fixture(ChartSelectionMode.Multiple);
        var chart = fixture.Chart;
        var model = new SelectionModel { Series = new[] { fixture.Line } };
        using var binding = chart.Bind(CartesianChart.SelectedSeriesProperty,
            new Binding(nameof(SelectionModel.Series)) { Source = model, Mode = BindingMode.TwoWay });
        fixture.Drain();
        var previous = chart.SelectedSeries;
        void Cancel(object? sender, ChartSeriesSelectionChangingEventArgs args) => args.Cancel = true;
        chart.SeriesSelectionChanging += Cancel;
        model.Series = new[] { fixture.Bar }; fixture.Drain();
        Assert.Same(previous, chart.SelectedSeries); Assert.Same(previous, model.Series);
        chart.SeriesSelectionChanging -= Cancel;
        model.Series = new ChartSeries[] { fixture.Scatter, fixture.Scatter, new LineSeries() }; fixture.Drain();
        Assert.Same(fixture.Scatter, Assert.Single(model.Series!)); Assert.Same(chart.SelectedSeries, model.Series);
        chart.SelectSeries(fixture.Line, additive: true); fixture.Drain();
        Assert.Equal(new ChartSeries[] { fixture.Scatter, fixture.Line }, model.Series!);
        chart.ClearSeriesSelection(); fixture.Drain();
        Assert.Empty(model.Series!);
    }

    [AvaloniaFact]
    public void Compiled_axaml_bindings_round_trip_selection_mode_and_opacity()
    {
        var model = new SeriesSelectionConsumerModel();
        var view = new SeriesSelectionConsumerView { DataContext = model };
        var window = new Window { Width = 760, Height = 460, Content = view }; window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var chart = view.FindControl<CartesianChart>("BoundSeriesChart")!;
            Assert.Equal(ChartSelectionMode.Multiple, chart.SeriesSelectionMode);
            model.Selection = new[] { model.Series[0], model.Series[0] };
            Dispatcher.UIThread.RunJobs();
            Assert.Same(model.Series[0], Assert.Single(chart.SelectedSeries!));
            Assert.Same(chart.SelectedSeries, model.Selection);
            chart.SelectSeries(model.Series[1], additive: true); Dispatcher.UIThread.RunJobs();
            Assert.Equal(model.Series, model.Selection);
            model.Opacity = .12;
            Assert.Equal(.12, chart.UnselectedSeriesOpacity);
            model.Mode = ChartSelectionMode.None; Dispatcher.UIThread.RunJobs();
            Assert.Empty(model.Selection!); Assert.Empty(chart.SelectedSeries!);
            model.Mode = ChartSelectionMode.Single;
            chart.SelectSeries(model.Series[1]); Dispatcher.UIThread.RunJobs();
            Assert.Same(model.Series[1], Assert.Single(model.Selection!));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Reentrant_property_observer_can_publish_a_newer_selection_without_stale_outer_events()
    {
        using var fixture = new Fixture();
        var chart = fixture.Chart;
        var changed = new List<ChartSeries>();
        chart.SeriesSelectionChanged += (_, args) => changed.Add(Assert.Single(args.NewSelection));
        chart.PropertyChanged += (_, args) =>
        {
            if (args.Property == CartesianChart.SelectedSeriesProperty && chart.SelectedSeries?.FirstOrDefault() == fixture.Bar)
                chart.SelectSeries(fixture.Scatter);
        };
        Assert.False(chart.SelectSeries(fixture.Bar));
        Assert.Same(fixture.Scatter, Assert.Single(chart.SelectedSeries!));
        Assert.Equal(new ChartSeries[] { fixture.Scatter }, changed);
    }

    [AvaloniaFact]
    public void A_point_selection_callback_can_choose_a_newer_series_than_the_outer_pointer_request()
    {
        using var fixture = new Fixture();
        var chart = fixture.Chart; chart.SelectSeries(fixture.Line);
        chart.SelectionChanging += (_, _) => chart.SelectSeries(fixture.Scatter);
        fixture.Click(fixture.Point(3, 40));
        Assert.Same(fixture.Bar, chart.SelectedPoint!.Series);
        Assert.Same(fixture.Scatter, Assert.Single(chart.SelectedSeries!));
    }

    [AvaloniaFact]
    public void A_point_selection_callback_can_choose_a_newer_series_than_the_outer_keyboard_request()
    {
        using var fixture = new Fixture();
        var chart = fixture.Chart; chart.IsKeyboardNavigationEnabled = true;
        chart.SelectedPoint = chart.HitTestPoint(fixture.Point(3, 40)); chart.SelectSeries(fixture.Line);
        chart.SelectionChanging += (_, _) => chart.SelectSeries(fixture.Bar);
        chart.Focus();
        fixture.Window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.None, null);
        fixture.Window.KeyRelease(Key.Right, RawInputModifiers.None, PhysicalKey.None, null);
        fixture.Drain();
        Assert.Same(fixture.Scatter, chart.SelectedPoint!.Series);
        Assert.Same(fixture.Bar, Assert.Single(chart.SelectedSeries!));
    }

    [AvaloniaFact]
    public void Keyboard_point_navigation_selects_its_series_with_a_keyboard_reason()
    {
        using var fixture = new Fixture();
        var chart = fixture.Chart;
        chart.IsKeyboardNavigationEnabled = true;
        chart.SelectedPoint = chart.HitTestPoint(fixture.Point(3, 40));
        ChartSelectionChangeReason? reason = null;
        chart.SeriesSelectionChanged += (_, args) => reason = args.Reason;
        chart.Focus();
        fixture.Window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.None, null);
        fixture.Window.KeyRelease(Key.Right, RawInputModifiers.None, PhysicalKey.None, null);
        fixture.Drain();
        Assert.Same(fixture.Scatter, chart.SelectedPoint!.Series);
        Assert.Same(fixture.Scatter, Assert.Single(chart.SelectedSeries!));
        Assert.Equal(ChartSelectionChangeReason.Keyboard, reason);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Keyboard_series_only_navigation_skips_hidden_series_and_can_be_canceled(bool cancel)
    {
        using var fixture = new Fixture();
        var chart = fixture.Chart;
        chart.SelectionMode = ChartSelectionMode.None; chart.IsKeyboardNavigationEnabled = true;
        fixture.Bar.IsVisible = false; chart.SelectSeries(fixture.Line);
        ChartSelectionChangeReason? reason = null;
        chart.SeriesSelectionChanging += (_, args) => { reason = args.Reason; args.Cancel = cancel; };
        chart.Focus();
        fixture.Window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.None, null);
        fixture.Window.KeyRelease(Key.Right, RawInputModifiers.None, PhysicalKey.None, null);
        fixture.Drain();
        Assert.Same(cancel ? fixture.Line : fixture.Scatter, Assert.Single(chart.SelectedSeries!));
        Assert.Equal(ChartSelectionChangeReason.Keyboard, reason);
        Assert.Empty(chart.SelectedPoints ?? []);
        Assert.Null(chart.SelectedPoint);
    }

    [AvaloniaFact]
    public void Series_selection_and_opacity_only_change_rendering_and_never_rebuild_or_enumerate_data()
    {
        var renderer = new CountingRenderer();
        var firstSource = new CountingSource(); var secondSource = new CountingSource();
        var first = new ExternalSeries(renderer) { ItemsSource = firstSource, Stroke = Brushes.Magenta };
        var second = new ExternalSeries(renderer) { ItemsSource = secondSource, Stroke = Brushes.Orange };
        var chart = new CartesianChart
        {
            ShowLegend = false, SeriesSelectionMode = ChartSelectionMode.Multiple,
            XAxis = new NumericAxis { Minimum = 0, Maximum = 10 }, YAxis = new NumericAxis { Minimum = 0, Maximum = 100 },
        };
        chart.Series.Add(first); chart.Series.Add(second);
        var window = new Window { Width = 760, Height = 460, Content = chart };
        window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
        try
        {
            Render(chart);
            var bounds = renderer.BoundsCalls;
            var enumerations = firstSource.Enumerations + secondSource.Enumerations;
            var range = chart.VisibleRange; var firstSnapshot = first.CreateSnapshot(); var secondSnapshot = second.CreateSnapshot();
            chart.SelectSeries(first); chart.UnselectedSeriesOpacity = .17;
            var drawing = Render(chart);
            Assert.Equal(1, Opacities(drawing, Colors.Magenta).Single(), 8);
            Assert.Equal(.17, Opacities(drawing, Colors.Orange).Single(), 8);
            Assert.Equal(range, chart.VisibleRange);
            Assert.Same(firstSnapshot, first.CreateSnapshot()); Assert.Same(secondSnapshot, second.CreateSnapshot());
            Assert.Equal(bounds, renderer.BoundsCalls);
            Assert.Equal(enumerations, firstSource.Enumerations + secondSource.Enumerations);
            chart.ClearSeriesSelection();
            Assert.Equal(1, Opacities(Render(chart), Colors.Orange).Single(), 8);
            Assert.Throws<ArgumentException>(() => chart.UnselectedSeriesOpacity = double.NaN);
            Assert.Throws<ArgumentException>(() => chart.UnselectedSeriesOpacity = -1);
            Assert.Throws<ArgumentException>(() => chart.UnselectedSeriesOpacity = 1.01);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void A_custom_renderer_with_a_line_legend_does_not_gain_an_inferred_line_segment_hit_contract()
    {
        var series = new ExternalSeries(new CountingRenderer()) { ItemsSource = new[] { new ChartPoint(1, 20), new ChartPoint(9, 20) } };
        var chart = new CartesianChart
        {
            ShowLegend = false, SeriesSelectionMode = ChartSelectionMode.Single,
            XAxis = new NumericAxis { Minimum = 0, Maximum = 10 }, YAxis = new NumericAxis { Minimum = 0, Maximum = 100 },
        };
        chart.Series.Add(series);
        var window = new Window { Width = 760, Height = 460, Content = chart };
        window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
        try
        {
            var point = new Point(chart.PlotArea.Left + chart.PlotArea.Width / 2, chart.PlotArea.Bottom - chart.PlotArea.Height * .2);
            var translated = chart.TranslatePoint(point, window)!.Value;
            window.MouseDown(translated, MouseButton.Left); window.MouseUp(translated, MouseButton.Left);
            Assert.Empty(chart.SelectedSeries ?? []);
            Assert.Null(chart.SelectedPoint);
        }
        finally { window.Close(); }
    }

    private static DrawingGroup Render(CartesianChart chart)
    {
        var drawing = new DrawingGroup(); using (var context = drawing.Open()) chart.Render(context); return drawing;
    }

    private static IEnumerable<double> Opacities(Drawing drawing, Color color, double opacity = 1)
    {
        if (drawing is DrawingGroup group)
            foreach (var child in group.Children)
                foreach (var value in Opacities(child, color, opacity * group.Opacity)) yield return value;
        if (drawing is GeometryDrawing { Brush: ISolidColorBrush brush } && brush.Color == color) yield return opacity * brush.Opacity;
    }

    private sealed class Fixture : IDisposable
    {
        public LineSeries Line { get; } = new() { ItemsSource = new[] { new ChartPoint(1, 20), new ChartPoint(9, 20) } };
        public BarSeries Bar { get; } = new() { ItemsSource = new[] { new ChartPoint(3, 40) } };
        public ScatterSeries Scatter { get; } = new() { ItemsSource = new[] { new ChartPoint(7, 80) } };
        public CartesianChart Chart { get; }
        public Window Window { get; }
        public Fixture(ChartSelectionMode mode = ChartSelectionMode.Single)
        {
            Chart = new CartesianChart
            {
                ShowLegend = false, SeriesSelectionMode = mode,
                XAxis = new NumericAxis { Minimum = 0, Maximum = 10 }, YAxis = new NumericAxis { Minimum = 0, Maximum = 100 },
            };
            Chart.Series.Add(Line); Chart.Series.Add(Bar); Chart.Series.Add(Scatter);
            Window = new Window { Width = 760, Height = 460, Content = Chart }; Window.Show(); Drain();
        }
        public Point Point(double x, double y) => new(Chart.PlotArea.Left + x / 10 * Chart.PlotArea.Width,
            Chart.PlotArea.Bottom - y / 100 * Chart.PlotArea.Height);
        public void Drain() { Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); Render(Chart); }
        public void Click(Point point, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            var position = Chart.TranslatePoint(point, Window)!.Value;
            Window.MouseDown(position, MouseButton.Left, modifiers); Window.MouseUp(position, MouseButton.Left, modifiers); Drain();
        }
        public void Dispose() => Window.Close();
    }

    private sealed class SelectionModel : INotifyPropertyChanged
    {
        private IReadOnlyList<ChartSeries>? _series;
        public IReadOnlyList<ChartSeries>? Series { get => _series; set { if (ReferenceEquals(_series, value)) return; _series = value; Changed(); } }
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    }

    private sealed class CountingSource : IEnumerable
    {
        public int Enumerations { get; private set; }
        public IEnumerator GetEnumerator() { Enumerations++; return new[] { new ChartPoint(2, 25), new ChartPoint(8, 75) }.GetEnumerator(); }
    }

    private sealed class ExternalSeries(IChartSeriesRenderer renderer) : ChartSeries
    { public override IChartSeriesRenderer Renderer => renderer; }

    private sealed class CountingRenderer : IChartSeriesRenderer
    {
        public int BoundsCalls { get; private set; }
        public bool IncludesZero => false;
        public bool UsesCategorySlots => false;
        public ChartLegendSymbol LegendSymbol => ChartLegendSymbol.Line;
        public ChartSeriesBounds? GetBounds(ChartSeries series, ChartDataSnapshot data) { BoundsCalls++; return new(0, 10, 0, 100); }
        public void Render(DrawingContext drawing, ChartSeriesRenderContext context) => drawing.DrawRectangle(context.Brush, null,
            new Rect(context.Projection.Project(new ChartPoint(2, 70)), new Size(12, 12)));
        public ChartSeriesHit? HitTest(ChartSeriesRenderContext context, Point position, double tolerance) => null;
    }
}
