using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class RangeSelectorTests
{
    [AvaloniaFact]
    public void Atomic_range_validation_clamps_bounds_and_enforces_minimum_span()
    {
        var selector = new ChartRangeSelector { MinimumSpan = .2 };
        Assert.True(new ChartRange(0, 1).IsValid);
        Assert.False(new ChartRange(.5, .5).IsValid); Assert.False(new ChartRange(-.1, .5).IsValid);
        Assert.False(new ChartRange(.5, double.NaN).IsValid);
        selector.Range = new(-1, .1); AssertRange(new(0, .2), selector.Range);
        selector.Range = new(.95, 2); AssertRange(new(.8, 1), selector.Range);
        selector.Range = new(.3, .8); selector.MinimumSpan = .7; AssertRange(new(.3, 1), selector.Range);
        Assert.Throws<ArgumentException>(() => selector.Range = new(.7, .2));
        Assert.Throws<ArgumentException>(() => selector.Range = new(double.NaN, 1));
        Assert.Throws<ArgumentException>(() => selector.Axis = ChartInteractionAxes.Both);
    }

    [AvaloniaFact]
    public void Sub_ULP_minimum_span_keeps_keyboard_endpoints_and_window_strictly_ordered()
    {
        using var host = new SelectorHost();
        host.Selector.MinimumSpan = double.Epsilon;
        host.Selector.Range = new(.2, .8); host.Drain();
        host.Part(ChartRangeSelectorPart.Lower).Focus(); host.Key(Key.End);
        Assert.True(host.Selector.Range.IsValid);
        Assert.Equal(Math.BitDecrement(.8), host.Selector.Range.Lower); Assert.Equal(.8, host.Selector.Range.Upper);
        host.Selector.Range = new(.2, .8); host.Part(ChartRangeSelectorPart.Upper).Focus(); host.Key(Key.Home);
        Assert.True(host.Selector.Range.IsValid);
        Assert.Equal(.2, host.Selector.Range.Lower); Assert.Equal(Math.BitIncrement(.2), host.Selector.Range.Upper);
        host.Selector.Range = new(0, double.Epsilon); host.Selector.Focus(); host.Key(Key.Right);
        Assert.True(host.Selector.Range.IsValid); Assert.Equal(.01, host.Selector.Range.Lower);
        Assert.Equal(Math.BitIncrement(.01), host.Selector.Range.Upper);
        host.Selector.Range = new(2, 3);
        Assert.True(host.Selector.Range.IsValid); Assert.Equal(Math.BitDecrement(1), host.Selector.Range.Lower); Assert.Equal(1, host.Selector.Range.Upper);
    }

    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Both_handles_resize_in_physical_direction_with_original_opposite_endpoint(bool vertical, bool reversed)
    {
        using var host = new SelectorHost(vertical, reversed);
        var changes = new List<ChartRangeChangedEventArgs>(); host.Selector.RangeChanged += (_, e) => changes.Add(e);
        host.Drag(host.Handle(ChartRangeSelectorPart.Lower), host.Position(.4));
        AssertRange(new(.4, .8), host.Selector.Range);
        Assert.Equal(ChartRangeSelectorPart.Lower, host.Selector.ActivePart);
        Assert.False(host.Selector.IsDragging); Assert.All(changes, e => Assert.True(e.IsUserInitiated));
        host.Drag(host.Handle(ChartRangeSelectorPart.Upper), host.Position(.7));
        AssertRange(new(.4, .7), host.Selector.Range);
        Assert.Equal(ChartRangeSelectorPart.Upper, host.Selector.ActivePart);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Window_drag_preserves_span_and_clamps_the_whole_window_at_both_edges(bool vertical)
    {
        using var host = new SelectorHost(vertical);
        host.Selector.Range = new(.2, .6); host.Drain();
        host.Drag(host.Position(.4), host.Position(.8));
        AssertRange(new(.6, 1), host.Selector.Range);
        Assert.Equal(ChartRangeSelectorPart.Window, host.Selector.ActivePart);
        host.Drag(host.Position(.8), host.Position(-.2));
        AssertRange(new(0, .4), host.Selector.Range);
    }

    [AvaloniaFact]
    public void Handles_cannot_cross_or_collapse_below_the_minimum_span()
    {
        using var host = new SelectorHost(); host.Selector.MinimumSpan = .15;
        host.Drag(host.Handle(ChartRangeSelectorPart.Lower), host.Position(.99));
        AssertRange(new(.65, .8), host.Selector.Range);
        host.Selector.Range = new(.2, .8); host.Drain();
        host.Drag(host.Handle(ChartRangeSelectorPart.Upper), host.Position(-.1));
        AssertRange(new(.2, .35), host.Selector.Range);
        Assert.True(host.Selector.Range.IsValid);
    }

    [AvaloniaTheory]
    [InlineData("capture")]
    [InlineData("disable")]
    [InlineData("parent-disable")]
    [InlineData("parent-hide")]
    [InlineData("hide")]
    [InlineData("detach")]
    [InlineData("resize")]
    [InlineData("orientation")]
    [InlineData("external")]
    public void Interrupted_drag_releases_capture_and_retains_the_last_committed_range(string interruption)
    {
        using var host = new SelectorHost();
        IPointer? pointer = null;
        host.Window.AddHandler(InputElement.PointerPressedEvent, (_, e) => pointer = e.Pointer, Avalonia.Interactivity.RoutingStrategies.Bubble, true);
        host.Press(host.Position(.5)); host.Move(host.Position(.6), RawInputModifiers.LeftMouseButton);
        Assert.True(host.Selector.IsDragging); Assert.Same(host.Selector, pointer!.Captured);
        switch (interruption)
        {
            case "capture": pointer.Capture(null); break;
            case "disable": host.Selector.IsEnabled = false; break;
            case "parent-disable": host.Container.IsEnabled = false; break;
            case "parent-hide": host.Container.IsVisible = false; break;
            case "hide": host.Selector.IsVisible = false; break;
            case "detach": host.Container.Children.Remove(host.Selector); break;
            case "resize": host.Selector.Width += 80; break;
            case "orientation": host.Selector.Orientation = Orientation.Vertical; break;
            case "external": host.Selector.Range = new(.3, .5); break;
        }
        host.Drain();
        var stopped = host.Selector.Range;
        Assert.False(host.Selector.IsDragging); Assert.Null(pointer.Captured);
        host.Move(host.Position(.95), RawInputModifiers.LeftMouseButton); host.Release(host.Position(.95));
        Assert.Equal(stopped, host.Selector.Range);
    }

    [AvaloniaFact]
    public void Escape_rolls_back_the_entire_pointer_gesture_and_publishes_original_atomic_range()
    {
        using var host = new SelectorHost();
        var original = host.Selector.Range;
        host.Press(host.Handle(ChartRangeSelectorPart.Lower)); host.Move(host.Position(.5), RawInputModifiers.LeftMouseButton);
        Assert.NotEqual(original, host.Selector.Range);
        host.Key(Key.Escape);
        Assert.Equal(original, host.Selector.Range); Assert.False(host.Selector.IsDragging);
        host.Move(host.Position(.65), RawInputModifiers.LeftMouseButton); Assert.Equal(original, host.Selector.Range);
    }

    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Keyboard_focuses_real_endpoints_and_supports_small_page_and_home_end_changes(bool vertical, bool reversed)
    {
        using var host = new SelectorHost(vertical, reversed);
        var selector = host.Selector; selector.SmallChange = .05; selector.LargeChange = .15;
        var lower = host.Part(ChartRangeSelectorPart.Lower); Assert.True(lower.Focus());
        Assert.Equal(ChartRangeSelectorPart.Lower, selector.ActivePart);
        var key = vertical ? reversed ? Key.Down : Key.Up : reversed ? Key.Left : Key.Right;
        host.Key(key); AssertRange(new(.25, .8), selector.Range);
        host.Key(Key.PageUp); AssertRange(new(.4, .8), selector.Range);
        host.Key(Key.Home); AssertRange(new(0, .8), selector.Range);
        Assert.True(host.Part(ChartRangeSelectorPart.Upper).Focus());
        host.Key(Key.End); AssertRange(new(0, 1), selector.Range);
        host.Key(Key.PageDown); AssertRange(new(0, .85), selector.Range);
        selector.Range = new(.2, .6); Assert.True(selector.Focus());
        Assert.Equal(ChartRangeSelectorPart.Window, selector.ActivePart);
        host.Key(Key.End); AssertRange(new(.6, 1), selector.Range);
        host.Key(Key.Home); AssertRange(new(0, .4), selector.Range);
    }

    [AvaloniaFact]
    public void Default_TwoWay_binding_survives_drag_keyboard_and_later_model_replacement()
    {
        var model = new RangeSelectorConsumerModel();
        using var host = new SelectorHost();
        host.Selector.DataContext = model;
        host.Selector.Bind(ChartRangeSelector.RangeProperty, new Binding(nameof(model.Range)));
        host.Drain(); AssertRange(model.Range, host.Selector.Range);
        host.Drag(host.Handle(ChartRangeSelectorPart.Lower), host.Position(.4));
        AssertRange(new(.4, .8), model.Range);
        host.Part(ChartRangeSelectorPart.Upper).Focus(); host.Key(Key.Left);
        AssertRange(new(.4, .79), model.Range);
        model.Range = new(.1, .5); host.Drain(); AssertRange(new(.1, .5), host.Selector.Range);
        Assert.True(host.Selector.IsSet(ChartRangeSelector.RangeProperty));
    }

    [AvaloniaFact]
    public void RangeChanged_reentrant_replacement_wins_without_resurrecting_the_outer_value()
    {
        using var host = new SelectorHost(); var handled = false;
        host.Selector.RangeChanged += (_, e) =>
        {
            if (!e.IsUserInitiated || handled) return;
            handled = true; host.Selector.Range = new(.1, .4);
        };
        host.Drag(host.Handle(ChartRangeSelectorPart.Lower), host.Position(.5));
        Assert.True(handled); AssertRange(new(.1, .4), host.Selector.Range);
    }

    [AvaloniaFact]
    public void Chart_actual_range_is_authoritative_below_selector_minimum_and_window_movement_preserves_it()
    {
        var chart = new CartesianChart { Height = 300, ShowLegend = false,
            XAxis = new NumericAxis { Minimum = 0, Maximum = 100 }, YAxis = new NumericAxis { Minimum = 0, Maximum = 100 } };
        chart.Series.Add(new LineSeries { ItemsSource = new[] { new ChartPoint(0, 0), new ChartPoint(100, 100) } });
        var selector = new ChartRangeSelector { Chart = chart, MinimumSpan = .08, Height = 40 };
        var window = new Window { Width = 760, Height = 400, Content = new StackPanel { Children = { chart, selector } } };
        try
        {
            window.Show(); Drain(window, chart);
            chart.SetVisibleFraction(new(.3, .31)); Drain(window, chart);
            AssertRange(new(.3, .31), selector.Range);
            selector.Focus(); window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.None, null); window.KeyRelease(Key.Right, RawInputModifiers.None, PhysicalKey.None, null);
            Drain(window, chart); AssertRange(new(.31, .32), selector.Range); AssertRange(selector.Range, chart.GetVisibleFraction(ChartInteractionAxes.X)!.Value);
            selector.GetVisualDescendants().OfType<Control>().Single(c => c.Name == "PART_UpperHandle").Focus();
            window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.None, null); window.KeyRelease(Key.Right, RawInputModifiers.None, PhysicalKey.None, null);
            Drain(window, chart); AssertRange(new(.31, .39), selector.Range);
            chart.XAxis.MinimumZoom = 20; Drain(window, chart);
            Assert.Equal(.05, selector.Range.Span, 7);
            selector.Range = new(.4, .9); Drain(window, chart);
            Assert.Equal(.05, selector.Range.Span, 7); AssertRange(selector.Range, chart.GetVisibleFraction(ChartInteractionAxes.X)!.Value);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Chart_callback_replacement_is_settled_before_one_final_RangeChanged_event()
    {
        var chart = new CartesianChart { Height = 300, ShowLegend = false,
            XAxis = new NumericAxis { Minimum = 0, Maximum = 100 }, YAxis = new NumericAxis { Minimum = 0, Maximum = 100 } };
        chart.Series.Add(new LineSeries { ItemsSource = new[] { new ChartPoint(0, 0), new ChartPoint(100, 100) } });
        var selector = new ChartRangeSelector { Chart = chart, Height = 40 };
        var panel = new StackPanel { Children = { chart, selector } };
        var window = new Window { Width = 760, Height = 400, Content = panel };
        try
        {
            window.Show(); Drain(window, chart);
            var events = new List<ChartRangeChangedEventArgs>(); var replaced = false;
            chart.ViewportChanged += (_, _) => { if (!replaced) { replaced = true; selector.Range = new(.15, .45); } };
            selector.RangeChanged += (_, e) => events.Add(e);
            selector.Range = new(.2, .8); Drain(window, chart);
            AssertRange(new(.15, .45), selector.Range); AssertRange(selector.Range, chart.GetVisibleFraction(ChartInteractionAxes.X)!.Value);
            var actual = Assert.Single(events); AssertRange(new(0, 1), actual.OldRange); AssertRange(new(.15, .45), actual.NewRange);
            Assert.False(actual.IsUserInitiated);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Viewport_callback_replacement_during_drag_releases_old_capture_and_future_moves_cannot_restore_it()
    {
        var chart = new CartesianChart { Height = 300, ShowLegend = false,
            XAxis = new NumericAxis { Minimum = 0, Maximum = 100 }, YAxis = new NumericAxis { Minimum = 0, Maximum = 100 } };
        chart.Series.Add(new LineSeries { ItemsSource = new[] { new ChartPoint(0, 0), new ChartPoint(100, 100) } });
        var selector = new ChartRangeSelector { Chart = chart, Height = 40 };
        var window = new Window { Width = 760, Height = 400, Content = new StackPanel { Children = { chart, selector } } };
        try
        {
            window.Show(); Drain(window, chart); chart.SetVisibleFraction(new(.2, .8)); Drain(window, chart);
            var events = new List<ChartRangeChangedEventArgs>(); var replaced = false;
            chart.ViewportChanged += (_, _) => { if (!replaced) { replaced = true; selector.Range = new(.1, .4); } };
            selector.RangeChanged += (_, e) => events.Add(e);
            IPointer? pointer = null;
            window.AddHandler(InputElement.PointerPressedEvent, (_, e) => pointer = e.Pointer, Avalonia.Interactivity.RoutingStrategies.Bubble, true);
            var origin = selector.TranslatePoint(new Point(selector.Bounds.Width / 2, selector.Bounds.Height / 2), window)!.Value;
            window.MouseDown(origin, MouseButton.Left); Assert.True(selector.IsDragging);
            window.MouseMove(origin + new Vector(60, 0), RawInputModifiers.LeftMouseButton); Drain(window, chart);
            AssertRange(new(.1, .4), selector.Range); AssertRange(selector.Range, chart.GetVisibleFraction(ChartInteractionAxes.X)!.Value);
            Assert.False(selector.IsDragging); Assert.Null(pointer!.Captured);
            window.MouseMove(origin + new Vector(120, 0), RawInputModifiers.LeftMouseButton); window.MouseUp(origin + new Vector(120, 0), MouseButton.Left);
            Drain(window, chart); AssertRange(new(.1, .4), selector.Range);
            var actual = Assert.Single(events); AssertRange(new(.2, .8), actual.OldRange); AssertRange(new(.1, .4), actual.NewRange);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Tab_navigation_reaches_both_real_endpoints_then_leaves_the_selector()
    {
        var selector = new ChartRangeSelector { Height = 40 }; var after = new Button { Content = "After selector" };
        var window = new Window { Width = 640, Height = 200, Content = new StackPanel { Children = { selector, after } } };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Assert.True(selector.Focus());
            void Tab(RawInputModifiers modifiers = RawInputModifiers.None)
            {
                window.KeyPress(Key.Tab, modifiers, PhysicalKey.None, null); window.KeyRelease(Key.Tab, modifiers, PhysicalKey.None, null);
                Dispatcher.UIThread.RunJobs();
            }
            Tab(); Assert.True(selector.GetVisualDescendants().OfType<Control>().Single(c => c.Name == "PART_LowerHandle").IsFocused);
            Assert.Equal(ChartRangeSelectorPart.Lower, selector.ActivePart);
            Tab(); Assert.True(selector.GetVisualDescendants().OfType<Control>().Single(c => c.Name == "PART_UpperHandle").IsFocused);
            Assert.Equal(ChartRangeSelectorPart.Upper, selector.ActivePart);
            Tab(); Assert.True(after.IsFocused);
            Tab(RawInputModifiers.Shift); Assert.True(selector.GetVisualDescendants().OfType<Control>().Single(c => c.Name == "PART_UpperHandle").IsFocused);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Normalized_only_extent_change_updates_selector_when_raw_viewport_is_unchanged()
    {
        var chart = new CartesianChart { Height = 300, ShowLegend = false,
            XAxis = new NumericAxis { Minimum = 0 }, YAxis = new NumericAxis { Minimum = 0, Maximum = 100 } };
        var points = new ObservableCollection<ChartPoint> { new(0, 0), new(100, 100) };
        chart.Series.Add(new LineSeries { ItemsSource = points });
        var selector = new ChartRangeSelector { Chart = chart, Height = 40 };
        var window = new Window { Width = 760, Height = 400, Content = new StackPanel { Children = { chart, selector } } };
        try
        {
            window.Show(); Drain(window, chart);
            var full = chart.VisibleRange!.Value;
            chart.Viewport = new(20, 80, 0, 100); Drain(window, chart);
            AssertRange(new(20 / full.XMaximum, 80 / full.XMaximum), selector.Range);
            var initialFraction = selector.Range;
            // Data growth preserves an explicit viewport; axis configuration changes deliberately reset it.
            // Doubling this zero-based source doubles both the automatic maximum and proportional padding.
            points.Add(new(200, 100)); Drain(window, chart);
            Assert.Equal(20, chart.VisibleRange!.Value.XMinimum, 10); Assert.Equal(80, chart.VisibleRange.Value.XMaximum, 10);
            AssertRange(new(initialFraction.Lower / 2, initialFraction.Upper / 2), selector.Range);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Compiled_AXAML_binding_and_dynamic_styles_remain_live()
    {
        var model = new RangeSelectorConsumerModel();
        var view = new RangeSelectorConsumerView { DataContext = model };
        var window = new Window { Width = 640, Height = 180, Content = view };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var selector = view.FindControl<ChartRangeSelector>("BoundRangeSelector")!;
            AssertRange(model.Range, selector.Range); Assert.Equal(.05, selector.MinimumSpan);
            selector.SetCurrentValue(ChartRangeSelector.RangeProperty, new ChartRange(.3, .6));
            AssertRange(new(.3, .6), model.Range);
            model.Range = new(.1, .9); Dispatcher.UIThread.RunJobs(); AssertRange(model.Range, selector.Range);
            Assert.Equal(Colors.SteelBlue, Assert.IsAssignableFrom<ISolidColorBrush>(selector.SelectionBrush).Color);
            view.Resources["RangeAccent"] = Brushes.OrangeRed; Dispatcher.UIThread.RunJobs();
            Assert.Equal(Colors.OrangeRed, Assert.IsAssignableFrom<ISolidColorBrush>(selector.SelectionBrush).Color);
            Assert.Equal(2, selector.GetVisualDescendants().OfType<Control>().Count(c => c.Focusable));
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Chart_coupling_uses_axis_transforms_and_preserves_the_other_axis(bool logarithmic)
    {
        var chart = new CartesianChart { ShowLegend = false,
            XAxis = logarithmic ? new LogarithmicAxis { Minimum = 1, Maximum = 1000 } : new NumericAxis { Minimum = 0, Maximum = 100 },
            YAxis = new NumericAxis { Minimum = 0, Maximum = 100 } };
        chart.Series.Add(new LineSeries { ItemsSource = new[] { new ChartPoint(1, 10), new ChartPoint(1000, 90) } });
        var selector = new ChartRangeSelector { Chart = chart, MinimumSpan = .001, Height = 40 };
        var panel = new StackPanel { Children = { chart, selector } }; chart.Height = 300;
        var window = new Window { Width = 760, Height = 400, Content = panel };
        try
        {
            window.Show(); Drain(window, chart);
            AssertRange(new(0, 1), selector.Range);
            selector.Range = new(1d / 3, 2d / 3); Drain(window, chart);
            var visible = chart.VisibleRange!.Value;
            Assert.Equal(logarithmic ? 10 : 100d / 3, visible.XMinimum, 7);
            Assert.Equal(logarithmic ? 100 : 200d / 3, visible.XMaximum, 7);
            Assert.Equal(0, visible.YMinimum); Assert.Equal(100, visible.YMaximum);
            chart.Viewport = new(logarithmic ? 1 : 10, logarithmic ? 10 : 90, 20, 80); Drain(window, chart);
            AssertRange(logarithmic ? new(0, 1d / 3) : new(.1, .9), selector.Range);
            selector.Axis = ChartInteractionAxes.Y; AssertRange(new(.2, .8), selector.Range);
            selector.Range = new(.3, .7); Drain(window, chart);
            Assert.Equal(30, chart.VisibleRange!.Value.YMinimum, 8); Assert.Equal(70, chart.VisibleRange.Value.YMaximum, 8);
            Assert.Equal(logarithmic ? 1 : 10, chart.VisibleRange.Value.XMinimum, 8);
            chart.ResetViewport(); Drain(window, chart); AssertRange(new(0, 1), selector.Range);
            panel.Children.Remove(selector); var detached = selector.Range;
            chart.Viewport = new(logarithmic ? 1 : 20, logarithmic ? 10 : 80, 40, 60); Drain(window, chart);
            Assert.Equal(detached, selector.Range);
            panel.Children.Add(selector); Drain(window, chart); AssertRange(new(.4, .6), selector.Range);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Range_and_handle_styles_generate_visible_geometry_and_focus_changes_the_outline()
    {
        using var host = new SelectorHost();
        host.Selector.TrackBrush = Brushes.Gray; host.Selector.SelectionBrush = Brushes.Red;
        host.Selector.HandleBrush = Brushes.White; host.Selector.HandleBorderBrush = Brushes.Blue; host.Selector.FocusBrush = Brushes.Orange;
        var before = Record(host.Selector);
        Assert.Contains(before.Children.OfType<GeometryDrawing>(), drawing => ReferenceEquals(drawing.Brush, Brushes.Red));
        var part = host.Part(ChartRangeSelectorPart.Lower); part.Focus(); host.Drain();
        var focused = Record(part);
        Assert.Contains(focused.Children.OfType<GeometryDrawing>(), drawing => ReferenceEquals(drawing.Pen?.Brush, Brushes.Orange));
        Assert.True(part.Bounds.Width > 0 && part.Bounds.Height > 0);
    }

    private static DrawingGroup Record(Control control) { var group = new DrawingGroup(); using (var context = group.Open()) control.Render(context); return group; }
    private static void Drain(Window window, CartesianChart chart)
    {
        Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); using var context = new DrawingGroup().Open(); chart.Render(context); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
    }
    private static void AssertRange(ChartRange expected, ChartRange actual) { Assert.Equal(expected.Lower, actual.Lower, 7); Assert.Equal(expected.Upper, actual.Upper, 7); }

    private sealed class SelectorHost : IDisposable
    {
        private readonly Point _rootOffset;
        public SelectorHost(bool vertical = false, bool reversed = false)
        {
            Selector = new() { Range = new(.2, .8), Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal,
                IsDirectionReversed = reversed, Width = vertical ? 48 : 416, Height = vertical ? 416 : 48 };
            Container = new Canvas(); Container.Children.Add(Selector); Canvas.SetLeft(Selector, 40); Canvas.SetTop(Selector, 40);
            Window = new() { Width = 700, Height = 560, Content = Container }; Window.Show(); Drain();
            _rootOffset = Selector.TranslatePoint(default, Window)!.Value;
        }
        public ChartRangeSelector Selector { get; }
        public Canvas Container { get; }
        public Window Window { get; }
        public void Drain() { Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); using var context = new DrawingGroup().Open(); Selector.Render(context); }
        public Control Part(ChartRangeSelectorPart part) => Selector.GetVisualDescendants().OfType<Control>().Single(control => control.Name == (part == ChartRangeSelectorPart.Lower ? "PART_LowerHandle" : "PART_UpperHandle"));
        public Point Handle(ChartRangeSelectorPart part) => Part(part).Bounds.Center;
        public Point Position(double fraction)
        {
            var physical = Selector.IsDirectionReversed ? 1 - fraction : fraction;
            if (Selector.Orientation == Orientation.Vertical) physical = 1 - physical;
            var length = Selector.Orientation == Orientation.Horizontal ? Selector.Bounds.Width : Selector.Bounds.Height;
            var value = Selector.HandleSize / 2 + physical * (length - Selector.HandleSize);
            return Selector.Orientation == Orientation.Horizontal ? new(value, Selector.Bounds.Height / 2) : new(Selector.Bounds.Width / 2, value);
        }
        private Point ToWindow(Point point) => Selector.TranslatePoint(point, Window) ?? new Point(_rootOffset.X + point.X, _rootOffset.Y + point.Y);
        public void Move(Point point, RawInputModifiers modifiers = RawInputModifiers.None) { Window.MouseMove(ToWindow(point), modifiers); Drain(); }
        public void Press(Point point) { Window.MouseDown(ToWindow(point), MouseButton.Left); Drain(); }
        public void Release(Point point) { Window.MouseUp(ToWindow(point), MouseButton.Left); Drain(); }
        public void Drag(Point from, Point to) { Press(from); Move(to, RawInputModifiers.LeftMouseButton); Release(to); }
        public void Key(Key key) { Window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null); Window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null); Drain(); }
        public void Dispose() => Window.Close();
    }
}
