using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class WheelInputTests
{
    [AvaloniaFact]
    public void Wheel_defaults_keep_opt_in_zoom_and_reject_invalid_configuration()
    {
        var chart = Create();
        Assert.Equal(ChartWheelMode.Zoom, chart.WheelMode);
        Assert.Equal(ChartInputRoutingMode.Auto, chart.InputRoutingMode);
        Assert.Equal(KeyModifiers.None, chart.WheelZoomModifiers);
        Assert.Equal(48, chart.WheelPanSensitivity);
        Assert.Equal(1.2, chart.WheelZoomSensitivity);
        foreach (var bad in new[] { 0d, -1, double.NaN, double.PositiveInfinity })
            Assert.Throws<ArgumentException>(() => chart.WheelPanSensitivity = bad);
        foreach (var bad in new[] { 0d, 1, -1, double.NaN, double.PositiveInfinity })
            Assert.Throws<ArgumentException>(() => chart.WheelZoomSensitivity = bad);
        Assert.Throws<ArgumentException>(() => chart.WheelMode = (ChartWheelMode)99);
        Assert.Throws<ArgumentException>(() => chart.InputRoutingMode = (ChartInputRoutingMode)99);
        Assert.Throws<ArgumentException>(() => chart.WheelZoomModifiers = (KeyModifiers)128);
    }

    [AvaloniaFact]
    public void Fractional_two_axis_precision_packets_accumulate_without_rounding_or_losing_a_component()
    {
        using var host = new WheelHost(Create());
        var chart = host.Chart;
        chart.WheelMode = ChartWheelMode.Pan;
        chart.WheelPanSensitivity = 24;
        var start = View(chart);
        var plot = chart.PlotArea;
        for (var i = 0; i < 4; i++) host.Wheel(new(.125, -.0625));
        var result = View(chart);
        Near(20 - .5 * 24 / plot.Width * 60, result.XMinimum);
        Near(20 - .25 * 24 / plot.Height * 60, result.YMinimum);
        Near(60, result.XMaximum - result.XMinimum);
        Near(60, result.YMaximum - result.YMinimum);
        Assert.True(host.ChartHandled);
        chart.Viewport = start; host.Drain();
        host.Wheel(new(.5, -.25));
        AssertView(result, View(chart));
    }

    [AvaloniaTheory]
    [InlineData(ChartInteractionAxes.X, false)]
    [InlineData(ChartInteractionAxes.X, true)]
    [InlineData(ChartInteractionAxes.Y, false)]
    [InlineData(ChartInteractionAxes.Y, true)]
    [InlineData(ChartInteractionAxes.Both, false)]
    [InlineData(ChartInteractionAxes.Both, true)]
    public void Precision_pan_uses_each_axis_direction_and_preserves_locked_dimensions(ChartInteractionAxes axes, bool inverse)
    {
        var chart = Create();
        chart.XAxis.IsInversed = inverse; chart.YAxis.IsInversed = inverse;
        using var host = new WheelHost(chart);
        chart.WheelMode = ChartWheelMode.Pan; chart.InteractionAxes = axes;
        var plot = chart.PlotArea;
        host.Wheel(new(.375, .25));
        var actual = View(chart);
        Near(20 + ((axes & ChartInteractionAxes.X) == 0 ? 0 : (inverse ? 1 : -1) * .375 * 48 / plot.Width * 60), actual.XMinimum);
        Near(20 + ((axes & ChartInteractionAxes.Y) == 0 ? 0 : (inverse ? -1 : 1) * .25 * 48 / plot.Height * 60), actual.YMinimum);
        Near(60, actual.XMaximum - actual.XMinimum); Near(60, actual.YMaximum - actual.YMinimum);
    }

    [AvaloniaFact]
    public void Shift_redirects_single_vertical_pan_but_retains_a_native_diagonal_precision_packet()
    {
        using var host = new WheelHost(Create());
        var chart = host.Chart; chart.WheelMode = ChartWheelMode.Pan;
        host.Wheel(new(0, .25), RawInputModifiers.Shift);
        var horizontal = View(chart);
        Assert.True(horizontal.XMinimum < 20); Near(20, horizontal.YMinimum);
        chart.Viewport = new(20, 80, 20, 80); host.Drain();
        host.Wheel(new(.25, .125), RawInputModifiers.Shift);
        Assert.True(View(chart).XMinimum < 20); Assert.True(View(chart).YMinimum > 20);
    }

    [AvaloniaTheory]
    [InlineData(RawInputModifiers.Control)]
    [InlineData(RawInputModifiers.Meta)]
    public void Automatic_mode_pans_unmodified_and_zooms_with_the_documented_modifier(RawInputModifiers modifier)
    {
        using var host = new WheelHost(Create());
        var chart = host.Chart; chart.WheelMode = ChartWheelMode.Auto;
        host.Wheel(new(.125, .25));
        var panned = View(chart);
        Near(60, panned.XMaximum - panned.XMinimum); Near(60, panned.YMaximum - panned.YMinimum);
        Assert.True(panned.XMinimum < 20); Assert.True(panned.YMinimum > 20);
        host.Wheel(new(.125, .25), modifier);
        var zoomed = View(chart);
        Near(60 / Math.Pow(1.2, .25), zoomed.XMaximum - zoomed.XMinimum);
        Near(60 / Math.Pow(1.2, .25), zoomed.YMaximum - zoomed.YMinimum);
    }

    [AvaloniaFact]
    public void Custom_zoom_modifier_mask_replaces_auto_shortcut_and_requires_all_selected_bits()
    {
        using var host = new WheelHost(Create());
        var chart = host.Chart; chart.WheelZoomModifiers = KeyModifiers.Alt | KeyModifiers.Shift;
        var initial = View(chart);
        host.Wheel(new(0, .5), RawInputModifiers.Alt);
        Assert.Equal(initial, View(chart)); Assert.False(host.ChartHandled);
        chart.WheelZoomSensitivity = 2;
        host.Wheel(new(0, .5), RawInputModifiers.Alt | RawInputModifiers.Shift | RawInputModifiers.Control);
        Near(60 / Math.Sqrt(2), View(chart).XMaximum - View(chart).XMinimum);
        chart.Viewport = initial; chart.WheelMode = ChartWheelMode.Auto; host.Drain();
        host.Wheel(new(0, .5), RawInputModifiers.Control);
        Near(60, View(chart).XMaximum - View(chart).XMinimum);
        Assert.True(View(chart).YMinimum > 20);
        host.Wheel(new(0, .5), RawInputModifiers.Alt | RawInputModifiers.Shift);
        Near(60 / Math.Sqrt(2), View(chart).XMaximum - View(chart).XMinimum);
    }

    [AvaloniaFact]
    public void Extreme_finite_sensitivities_reach_axis_limits_instead_of_dropping_the_packet()
    {
        using var host = new WheelHost(Create());
        var chart = host.Chart; chart.WheelZoomSensitivity = double.MaxValue;
        host.Wheel(new(0, 4));
        Assert.True(host.ChartHandled);
        Assert.True(View(chart).XMaximum - View(chart).XMinimum < .001);
        host.Wheel(new(0, -4));
        Assert.True(host.ChartHandled); AssertView(new(0, 100, 0, 100), View(chart));
        chart.Viewport = new(20, 80, 20, 80); chart.WheelMode = ChartWheelMode.Pan;
        chart.WheelPanSensitivity = double.MaxValue; host.Drain();
        host.Wheel(new(0, 4));
        Assert.True(host.ChartHandled); Near(40, View(chart).YMinimum); Near(100, View(chart).YMaximum);
    }

    [AvaloniaFact]
    public void Automatic_nested_scroll_changes_real_parent_offset_without_changing_chart_or_zoom_binding()
    {
        using var host = new WheelHost(Create(), nested: true);
        host.Chart.WheelMode = ChartWheelMode.Auto;
        var before = View(host.Chart); var offset = host.Scroll!.Offset;
        host.Wheel(new(0, -.5));
        Assert.Equal(before, View(host.Chart)); Assert.False(host.ChartHandled);
        Assert.True(host.Scroll.Offset.Y > offset.Y);
        var scrolled = host.Scroll.Offset;
        host.Wheel(new(0, .5), RawInputModifiers.Control);
        Assert.True(host.ChartHandled); Assert.Equal(scrolled, host.Scroll.Offset);
        Assert.True(View(host.Chart).YMaximum - View(host.Chart).YMinimum < 60);
        Assert.True(host.Chart.IsZoomEnabled);
    }

    [AvaloniaFact]
    public void Automatic_nested_scroll_hands_back_to_chart_at_the_parent_boundary()
    {
        using var host = new WheelHost(Create(), nested: true);
        host.Chart.WheelMode = ChartWheelMode.Auto;
        host.Scroll!.Offset = default; host.Drain();
        host.Wheel(new(0, .5));
        Assert.True(host.ChartHandled); Assert.Equal(default, host.Scroll.Offset);
        Assert.True(View(host.Chart).YMinimum > 20);
    }

    [AvaloniaFact]
    public void Chart_priority_consumes_only_an_actual_change_and_scrolls_parent_at_chart_limits()
    {
        using var host = new WheelHost(Create(), nested: true);
        var chart = host.Chart; chart.WheelMode = ChartWheelMode.Pan; chart.InputRoutingMode = ChartInputRoutingMode.Chart;
        var offset = host.Scroll!.Offset;
        host.Wheel(new(0, -.25));
        Assert.True(host.ChartHandled); Assert.Equal(offset, host.Scroll.Offset);
        chart.Viewport = new(20, 80, 0, 60); host.Drain();
        var before = View(chart); var notifications = 0;
        chart.ViewportChanged += (_, _) => notifications++;
        host.Wheel(new(0, -.25));
        Assert.False(host.ChartHandled); Assert.Equal(before, View(chart)); Assert.Equal(0, notifications);
        Assert.True(host.Scroll.Offset.Y > offset.Y);
    }

    [AvaloniaTheory]
    [InlineData(ChartWheelMode.Zoom)]
    [InlineData(ChartWheelMode.Pan)]
    [InlineData(ChartWheelMode.Auto)]
    public void Scroll_container_policy_defers_all_wheel_navigation_including_modified_zoom(ChartWheelMode mode)
    {
        using var host = new WheelHost(Create(), nested: true);
        host.Chart.WheelMode = mode; host.Chart.InputRoutingMode = ChartInputRoutingMode.ScrollViewer;
        var before = View(host.Chart); var offset = host.Scroll!.Offset;
        host.Wheel(new(0, -.5), RawInputModifiers.Control);
        Assert.False(host.ChartHandled); Assert.Equal(before, View(host.Chart));
        Assert.True(host.Scroll.Offset.Y > offset.Y);
    }

    [AvaloniaFact]
    public void A_vertical_packet_on_an_x_only_chart_bubbles_to_the_real_scroll_container()
    {
        using var host = new WheelHost(Create(), nested: true);
        host.Chart.WheelMode = ChartWheelMode.Pan; host.Chart.InputRoutingMode = ChartInputRoutingMode.Chart;
        host.Chart.InteractionAxes = ChartInteractionAxes.X;
        var before = View(host.Chart); var offset = host.Scroll!.Offset;
        host.Wheel(new(0, -.5));
        Assert.False(host.ChartHandled); Assert.Equal(before, View(host.Chart));
        Assert.True(host.Scroll.Offset.Y > offset.Y);
    }

    [AvaloniaFact]
    public void Default_zoom_retains_chart_priority_in_a_nested_scroll_container()
    {
        using var host = new WheelHost(Create(), nested: true);
        var offset = host.Scroll!.Offset;
        host.Wheel(new(0, .125));
        Assert.True(host.ChartHandled); Assert.Equal(offset, host.Scroll.Offset);
        Near(60 / Math.Pow(1.2, .125), View(host.Chart).XMaximum - View(host.Chart).XMinimum);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Disabled_navigation_or_an_outside_plot_packet_leaves_parent_scrolling_available(bool outsidePlot)
    {
        using var host = new WheelHost(Create(), nested: true);
        if (!outsidePlot) host.Chart.WheelMode = ChartWheelMode.Disabled;
        var before = View(host.Chart); var offset = host.Scroll!.Offset;
        host.Wheel(new(0, -.5), local: outsidePlot ? new Point(2, 2) : null);
        Assert.False(host.ChartHandled); Assert.Equal(before, View(host.Chart));
        Assert.True(host.Scroll.Offset.Y > offset.Y);
    }

    [AvaloniaFact]
    public void Zoom_limit_no_op_keeps_automatic_view_and_allows_parent_scrolling()
    {
        var chart = Create(); chart.Viewport = null;
        using var host = new WheelHost(chart, nested: true);
        var before = View(chart); var offset = host.Scroll!.Offset; var notifications = 0;
        chart.ViewportChanged += (_, _) => notifications++;
        host.Wheel(new(0, -.5));
        Assert.False(host.ChartHandled); Assert.Null(chart.Viewport); Assert.Equal(before, View(chart));
        Assert.Equal(0, notifications); Assert.True(host.Scroll.Offset.Y > offset.Y);
    }

    [AvaloniaFact]
    public void An_explicit_secondary_window_pans_independently_when_primary_axes_are_full_range()
    {
        var chart = Create(); chart.Viewport = null; chart.WheelMode = ChartWheelMode.Pan;
        var extra = new NumericAxis { Key = "second", Minimum = 0, Maximum = 1000, ViewMinimum = 200, ViewMaximum = 800 };
        chart.Axes.Add(extra);
        chart.Series.Add(new LineSeries { YAxisKey = "second", ItemsSource = new ChartPoint[] { new(0, 0), new(100, 1000) } });
        using var host = new WheelHost(chart);
        var plot = chart.PlotArea;
        host.Wheel(new(0, .25));
        Assert.True(host.ChartHandled);
        Near(200 + .25 * 48 / plot.Height * 600, extra.ViewMinimum!.Value);
        Near(800 + .25 * 48 / plot.Height * 600, extra.ViewMaximum!.Value);
        Assert.Equal(new ChartViewport(0, 100, 0, 100), View(chart));
    }

    [AvaloniaFact]
    public void Precision_pan_on_logarithmic_axes_preserves_transformed_spans()
    {
        var chart = Create();
        chart.XAxis = new LogarithmicAxis { Minimum = 1, Maximum = 1_000_000 };
        chart.YAxis = new LogarithmicAxis { Minimum = 1, Maximum = 1_000_000 };
        chart.Viewport = new(100, 10000, 100, 10000); chart.WheelMode = ChartWheelMode.Pan;
        using var host = new WheelHost(chart);
        var plot = chart.PlotArea;
        host.Wheel(new(.25, .125));
        var actual = View(chart);
        Near(2 - .25 * 48 / plot.Width * 2, Math.Log10(actual.XMinimum));
        Near(2 + .125 * 48 / plot.Height * 2, Math.Log10(actual.YMinimum));
        Near(2, Math.Log10(actual.XMaximum / actual.XMinimum)); Near(2, Math.Log10(actual.YMaximum / actual.YMinimum));
    }

    [AvaloniaFact]
    public void External_reentrant_viewport_change_during_wheel_commit_wins_over_the_original_input()
    {
        using var host = new WheelHost(Create());
        var chart = host.Chart; var replacement = new ChartViewport(30, 50, 40, 70); var entered = false;
        chart.ViewportChanged += (_, _) =>
        {
            if (entered) return;
            entered = true; chart.Viewport = replacement;
        };
        host.Wheel(new(0, 1));
        Assert.True(entered); Assert.Equal(replacement, chart.Viewport); Assert.Equal(replacement, View(chart));
        Assert.True(chart.XAxis.ViewMinimum is null || chart.XAxis.ViewMinimum == replacement.XMinimum);
        Assert.True(chart.YAxis.ViewMinimum is null || chart.YAxis.ViewMinimum == replacement.YMinimum);
    }

    [AvaloniaFact]
    public void Wheel_switches_from_a_captured_touch_drag_and_old_touch_updates_cannot_restore_its_baseline()
    {
        using var fixture = new TouchInputFixture();
        using var finger = TouchInputFixture.Finger();
        var chart = fixture.Chart; var origin = chart.PlotArea.Center;
        fixture.Press(finger, origin); fixture.Move(finger, origin + new Vector(30, 15));
        Assert.Same(chart, finger.Captured);
        var before = fixture.Range;
        var window = Assert.IsType<Window>(TopLevel.GetTopLevel(chart));
        window.MouseWheel(chart.TranslatePoint(chart.PlotArea.Center, window)!.Value, new Vector(0, .5)); fixture.Drain();
        Assert.Null(finger.Captured);
        var wheeled = fixture.Range;
        Assert.True(wheeled.XMaximum - wheeled.XMinimum < before.XMaximum - before.XMinimum);
        fixture.Move(finger, origin + new Vector(65, 40)); fixture.Release(finger, origin + new Vector(65, 40));
        Assert.Equal(wheeled, fixture.Range); Assert.Null(chart.SelectedPoint);
    }

    [AvaloniaFact]
    public void Wheel_starts_a_new_native_pinch_baseline_after_the_current_pinch_is_cancelled()
    {
        using var fixture = new TouchInputFixture();
        var chart = fixture.Chart;
        fixture.Pinch(2, chart.PlotArea.Center);
        var window = Assert.IsType<Window>(TopLevel.GetTopLevel(chart));
        window.MouseWheel(chart.TranslatePoint(chart.PlotArea.Center, window)!.Value, new Vector(0, .5)); fixture.Drain();
        var wheeled = fixture.Range;
        fixture.Pinch(2, chart.PlotArea.Center);
        Near((wheeled.XMaximum - wheeled.XMinimum) / 2, fixture.Range.XMaximum - fixture.Range.XMinimum);
        Near((wheeled.YMaximum - wheeled.YMinimum) / 2, fixture.Range.YMaximum - fixture.Range.YMinimum);
    }

    [AvaloniaFact]
    public void A_business_callback_on_capture_release_cancels_the_prepared_wheel_transform()
    {
        using var fixture = new TouchInputFixture();
        using var finger = TouchInputFixture.Finger();
        var chart = fixture.Chart; var origin = chart.PlotArea.Center;
        fixture.Press(finger, origin); fixture.Move(finger, origin + new Vector(30, 15));
        Assert.Same(chart, finger.Captured);
        var replacement = new ChartViewport(10, 40, 30, 70); var released = false;
        chart.PointerCaptureLost += (_, _) => { released = true; chart.Viewport = replacement; };
        var window = Assert.IsType<Window>(TopLevel.GetTopLevel(chart));
        window.MouseWheel(chart.TranslatePoint(chart.PlotArea.Center, window)!.Value, new Vector(0, .5)); fixture.Drain();
        Assert.True(released); Assert.Null(finger.Captured);
        Assert.Equal(replacement, chart.Viewport); Assert.Equal(replacement, fixture.Range);
    }

    private static CartesianChart Create()
    {
        var chart = new CartesianChart
        {
            ShowLegend = false, IsZoomEnabled = true, IsPanEnabled = true,
            XAxis = new NumericAxis { Minimum = 0, Maximum = 100 }, YAxis = new NumericAxis { Minimum = 0, Maximum = 100 },
            Viewport = new(20, 80, 20, 80)
        };
        chart.Series.Add(new LineSeries { ItemsSource = new ChartPoint[] { new(0, 0), new(50, 50), new(100, 100) } });
        return chart;
    }

    private static ChartViewport View(CartesianChart chart) => Assert.IsType<ChartViewport>(chart.VisibleRange);
    private static void Near(double expected, double actual) => Assert.Equal(expected, actual, 8);
    private static void AssertView(ChartViewport expected, ChartViewport actual)
    { Near(expected.XMinimum, actual.XMinimum); Near(expected.XMaximum, actual.XMaximum); Near(expected.YMinimum, actual.YMinimum); Near(expected.YMaximum, actual.YMaximum); }

    private sealed class WheelHost : IDisposable
    {
        public WheelHost(CartesianChart chart, bool nested = false)
        {
            Chart = chart;
            Control content = chart;
            if (nested)
            {
                chart.Height = 340;
                var stack = new StackPanel(); stack.Children.Add(chart); stack.Children.Add(new Border { Height = 900 });
                Scroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = stack };
                content = Scroll;
            }
            Window = new Window { Width = 760, Height = 440, Content = new Border { Padding = new Thickness(19), Child = content } };
            chart.AddHandler(InputElement.PointerWheelChangedEvent, (_, e) => ChartHandled = e.Handled, RoutingStrategies.Bubble, handledEventsToo: true);
            Window.Show(); Drain();
            Assert.True(chart.PlotArea.Width > 100 && chart.PlotArea.Height > 100);
            if (Scroll is not null) Assert.True(Scroll.Extent.Height > Scroll.Viewport.Height);
        }
        public CartesianChart Chart { get; }
        public Window Window { get; }
        public ScrollViewer? Scroll { get; }
        public bool ChartHandled { get; private set; }
        public void Wheel(Vector delta, RawInputModifiers modifiers = RawInputModifiers.None, Point? local = null)
        {
            ChartHandled = false;
            Window.MouseWheel(Chart.TranslatePoint(local ?? Chart.PlotArea.Center, Window)!.Value, delta, modifiers);
            Drain();
        }
        public void Drain()
        { Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); }
        public void Dispose() => Window.Close();
    }
}
