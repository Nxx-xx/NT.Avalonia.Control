using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;
using Pointer = Avalonia.Input.Pointer;

namespace Charts.Headless.Tests;

public sealed class TouchInputTests
{
    [AvaloniaFact]
    public void Touch_tap_selects_only_on_release_and_invokes_the_actual_original_record()
    {
        using var fixture = new TouchInputFixture();
        var invokes = new List<object?>();
        fixture.Chart.PointInvokedCommand = new TouchCommand(invokes.Add);
        using var finger = TouchInputFixture.Finger();
        var point = fixture.DataPoint(50, 50);
        var press = fixture.Press(finger, point);
        Assert.False(press.Handled); Assert.Null(finger.Captured);
        Assert.Null(fixture.Chart.SelectedPoint); Assert.Empty(invokes);
        fixture.Move(finger, point + new Vector(2, 1));
        Assert.Null(fixture.Chart.SelectedPoint);
        Assert.True(fixture.Release(finger, point).Handled);
        var hit = Assert.IsType<ChartPointHit>(fixture.Chart.SelectedPoint);
        Assert.Equal(new ChartPoint(50, 50), hit.Point);
        Assert.Same(fixture.Series, hit.Series);
        var invoked = Assert.IsType<ChartPointHit>(Assert.Single(invokes));
        Assert.Same(hit.Series, invoked.Series); Assert.Equal(hit.Index, invoked.Index);
        Assert.Equal(hit.Point, invoked.Point); Assert.Same(hit.Item, invoked.Item);
    }

    [AvaloniaFact]
    public void The_first_touch_can_focus_a_keyboard_enabled_chart_and_still_begin_its_pan()
    {
        var chart = TouchInputFixture.CreateChart(); chart.IsKeyboardNavigationEnabled = true; chart.Focusable = true;
        using var fixture = new TouchInputFixture(chart);
        using var finger = TouchInputFixture.Finger();
        Assert.False(chart.IsFocused);
        var origin = chart.PlotArea.Center;
        fixture.Press(finger, origin);
        Assert.True(chart.IsFocused); Assert.Null(chart.SelectedPoint);
        fixture.Move(finger, origin + new Vector(35, 0));
        Assert.Same(chart, finger.Captured); Assert.True(fixture.Range.XMinimum < 20);
    }

    [AvaloniaFact]
    public void Moving_keyboard_focus_to_another_control_cancels_touch_capture()
    {
        var chart = TouchInputFixture.CreateChart(); chart.Focusable = true; chart.Height = 260;
        var text = new TextBox();
        using var fixture = new TouchInputFixture(chart, new StackPanel { Children = { chart, text } });
        chart.Focus();
        using var finger = TouchInputFixture.Finger();
        var origin = chart.PlotArea.Center;
        fixture.Press(finger, origin); fixture.Move(finger, origin + new Vector(35, 0));
        Assert.Same(chart, finger.Captured);
        Assert.True(text.Focus()); fixture.Drain();
        Assert.Null(finger.Captured);
        var range = fixture.Range;
        fixture.Move(finger, origin + new Vector(75, 0));
        Assert.Equal(range, fixture.Range);
    }

    [AvaloniaFact]
    public void Focus_that_translates_the_chart_rebases_the_touch_origin_in_the_new_local_coordinates()
    {
        var chart = TouchInputFixture.CreateChart(); chart.IsKeyboardNavigationEnabled = true; chart.Focusable = true; chart.Height = 260;
        var text = new TextBox();
        using var fixture = new TouchInputFixture(chart, new StackPanel { Children = { chart, text } });
        Assert.True(text.Focus());
        chart.GotFocus += (_, _) => chart.RenderTransform = new Avalonia.Media.TranslateTransform(20, 0);
        using var finger = TouchInputFixture.Finger();
        var origin = chart.PlotArea.Center;
        fixture.Press(finger, origin);
        Assert.True(chart.IsFocused);
        // The root/screen coordinate has not moved: the new local coordinate is
        // 20 DIP smaller after GotFocus translated the chart to the right.
        fixture.Move(finger, origin + new Vector(-20, 0));
        Assert.Null(finger.Captured);
        TouchInputFixture.AssertRange(new(20, 80, 20, 80), fixture.Range);
    }

    [AvaloniaFact]
    public void A_drag_crossing_the_threshold_does_not_select_or_invoke_even_when_navigation_is_disabled()
    {
        using var fixture = new TouchInputFixture();
        fixture.Chart.IsPanEnabled = false; fixture.Chart.IsZoomEnabled = false;
        var invokes = 0; fixture.Chart.PointInvokedCommand = new TouchCommand(_ => invokes++);
        using var finger = TouchInputFixture.Finger();
        var origin = fixture.DataPoint(50, 50);
        fixture.Press(finger, origin);
        fixture.Move(finger, origin + new Vector(35, 0));
        fixture.Move(finger, origin);
        fixture.Release(finger, origin);
        Assert.Null(fixture.Chart.SelectedPoint); Assert.Equal(0, invokes); Assert.Null(finger.Captured);
    }

    [AvaloniaFact]
    public void Touch_tap_command_can_execute_may_remove_itself_without_invoking_a_stale_instance()
    {
        using var fixture = new TouchInputFixture();
        var invokes = 0;
        fixture.Chart.PointInvokedCommand = new TouchCommand(_ => invokes++, _ =>
        {
            fixture.Chart.PointInvokedCommand = null;
            return true;
        });
        using var finger = TouchInputFixture.Finger();
        var point = fixture.DataPoint(50, 50);
        fixture.Press(finger, point); fixture.Release(finger, point);
        Assert.NotNull(fixture.Chart.SelectedPoint); Assert.Null(fixture.Chart.PointInvokedCommand); Assert.Equal(0, invokes);
    }

    [AvaloniaFact]
    public void Mouse_command_can_execute_may_remove_itself_without_invoking_a_stale_instance()
    {
        using var fixture = new TouchInputFixture();
        var chart = fixture.Chart; var invokes = 0;
        chart.PointInvokedCommand = new TouchCommand(_ => invokes++, _ => { chart.PointInvokedCommand = null; return true; });
        var point = chart.TranslatePoint(fixture.DataPoint(50, 50), fixture.Window)!.Value;
        fixture.Window.MouseDown(point, MouseButton.Left); fixture.Window.MouseUp(point, MouseButton.Left);
        Assert.NotNull(chart.SelectedPoint); Assert.Null(chart.PointInvokedCommand); Assert.Equal(0, invokes);
    }

    [AvaloniaFact]
    public void Mouse_focus_that_translates_the_chart_rebases_its_pan_origin_before_capture()
    {
        var chart = TouchInputFixture.CreateChart(); chart.IsKeyboardNavigationEnabled = true; chart.Focusable = true; chart.Height = 260;
        var text = new TextBox();
        using var fixture = new TouchInputFixture(chart, new StackPanel { Children = { chart, text } });
        Assert.True(text.Focus());
        chart.GotFocus += (_, _) => chart.RenderTransform = new Avalonia.Media.TranslateTransform(20, 0);
        var point = chart.TranslatePoint(chart.PlotArea.Center, fixture.Window)!.Value;
        fixture.Window.MouseDown(point, MouseButton.Left);
        Assert.True(chart.IsFocused);
        fixture.Window.MouseMove(point, RawInputModifiers.LeftMouseButton);
        TouchInputFixture.AssertRange(new(20, 80, 20, 80), fixture.Range);
        fixture.Window.MouseUp(point, MouseButton.Left);
    }

    [AvaloniaFact]
    public void Mouse_command_callback_changing_input_routing_cannot_start_an_old_pan_after_it_returns()
    {
        using var fixture = new TouchInputFixture();
        var chart = fixture.Chart;
        IPointer? pointer = null;
        chart.AddHandler(InputElement.PointerPressedEvent, (_, args) => pointer = args.Pointer, RoutingStrategies.Bubble, handledEventsToo: true);
        chart.PointInvokedCommand = new TouchCommand(_ => { }, _ => { chart.InputRoutingMode = ChartInputRoutingMode.ScrollViewer; return true; });
        var point = chart.TranslatePoint(fixture.DataPoint(50, 50), fixture.Window)!.Value;
        fixture.Window.MouseDown(point, MouseButton.Left);
        Assert.NotNull(pointer); Assert.Null(pointer.Captured);
        fixture.Window.MouseMove(point + new Vector(30, 0), RawInputModifiers.LeftMouseButton);
        TouchInputFixture.AssertRange(new(20, 80, 20, 80), fixture.Range);
        fixture.Window.MouseUp(point + new Vector(30, 0), MouseButton.Left);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Single_finger_pan_uses_the_original_view_and_inverse_axis_directions_without_accumulating_drift(bool inverse)
    {
        using var fixture = new TouchInputFixture(inverse: inverse);
        using var finger = TouchInputFixture.Finger();
        var origin = fixture.Chart.PlotArea.Center;
        var originalPlot = fixture.Chart.PlotArea;
        fixture.Press(finger, origin);
        fixture.Move(finger, origin + new Vector(4, 2));
        Assert.Null(finger.Captured);
        TouchInputFixture.AssertRange(new(20, 80, 20, 80), fixture.Range);
        fixture.Move(finger, origin + new Vector(25, 18));
        Assert.Same(fixture.Chart, finger.Captured);
        fixture.Move(finger, origin + new Vector(50, 30));
        var x = 20 + (inverse ? 1 : -1) * 60 * 50 / originalPlot.Width;
        var y = 20 + (inverse ? -1 : 1) * 60 * 30 / originalPlot.Height;
        TouchInputFixture.AssertRange(new(x, x + 60, y, y + 60), fixture.Range);
        fixture.Release(finger, origin + new Vector(50, 30));
        Assert.Null(finger.Captured); Assert.Null(fixture.Chart.SelectedPoint);
        var final = fixture.Range;
        fixture.Move(finger, origin + new Vector(70, 45));
        Assert.Equal(final, fixture.Range);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Two_finger_pinch_preserves_the_initial_data_anchor_at_the_moving_centroid(bool inverse)
    {
        using var fixture = new TouchInputFixture(inverse: inverse);
        using var first = TouchInputFixture.Finger(); using var second = TouchInputFixture.Finger(false);
        var center = fixture.Chart.PlotArea.Center;
        var plot = fixture.Chart.PlotArea;
        fixture.Press(first, center + new Vector(-40, 0));
        fixture.Press(second, center + new Vector(40, 0));
        Assert.Null(first.Captured); Assert.Null(second.Captured);
        fixture.Move(first, center + new Vector(-70, 20));
        fixture.Move(second, center + new Vector(90, 20));
        Assert.Same(fixture.Chart, first.Captured); Assert.Same(fixture.Chart, second.Captured);
        // Initial distance 80, final distance 160; final centroid is (+10,+20).
        var xAnchor = .5 + (inverse ? -10 : 10) / plot.Width;
        var yAnchor = .5 + (inverse ? 20 : -20) / plot.Height;
        var x = 50 - 30 * xAnchor; var y = 50 - 30 * yAnchor;
        TouchInputFixture.AssertRange(new(x, x + 30, y, y + 30), fixture.Range);
        fixture.Release(second, center + new Vector(90, 20));
        fixture.Release(first, center + new Vector(-70, 20));
        Assert.Null(fixture.Chart.SelectedPoint); Assert.Null(first.Captured); Assert.Null(second.Captured);
    }

    [AvaloniaFact]
    public void Pinch_to_single_finger_rebases_without_jumping_and_never_turns_into_a_click()
    {
        using var fixture = new TouchInputFixture();
        using var first = TouchInputFixture.Finger(); using var second = TouchInputFixture.Finger(false);
        var center = fixture.Chart.PlotArea.Center;
        fixture.Press(first, center + new Vector(-40, 0)); fixture.Press(second, center + new Vector(40, 0));
        fixture.Move(first, center + new Vector(-80, 0)); fixture.Move(second, center + new Vector(80, 0));
        var pinched = fixture.Range;
        fixture.Release(second, center + new Vector(80, 0));
        fixture.Move(first, center + new Vector(-80, 0));
        TouchInputFixture.AssertRange(pinched, fixture.Range);
        var plot = fixture.Chart.PlotArea;
        fixture.Move(first, center + new Vector(-60, 0));
        var shift = -(pinched.XMaximum - pinched.XMinimum) * 20 / plot.Width;
        TouchInputFixture.AssertRange(pinched with { XMinimum = pinched.XMinimum + shift, XMaximum = pinched.XMaximum + shift }, fixture.Range);
        fixture.Release(first, center + new Vector(-60, 0));
        Assert.Null(fixture.Chart.SelectedPoint);
    }

    [AvaloniaFact]
    public void A_multitouch_tap_never_selects_even_without_crossing_the_pan_threshold()
    {
        using var fixture = new TouchInputFixture();
        using var first = TouchInputFixture.Finger(); using var second = TouchInputFixture.Finger(false);
        var center = fixture.DataPoint(50, 50);
        fixture.Press(first, center); fixture.Press(second, center + new Vector(1, 1));
        fixture.Release(second, center + new Vector(1, 1)); fixture.Release(first, center);
        Assert.Null(fixture.Chart.SelectedPoint); Assert.Null(first.Captured); Assert.Null(second.Captured);
    }

    [AvaloniaFact]
    public void Touch_axis_restrictions_and_zoom_switch_leave_unselected_windows_exactly_unchanged()
    {
        using var fixture = new TouchInputFixture();
        fixture.Chart.InteractionAxes = ChartInteractionAxes.X; fixture.Chart.IsPanEnabled = false;
        var lockedYMinimum = fixture.Range.YMinimum; var lockedYMaximum = fixture.Range.YMaximum;
        using var first = TouchInputFixture.Finger(); using var second = TouchInputFixture.Finger(false);
        var center = fixture.Chart.PlotArea.Center;
        fixture.Press(first, center + new Vector(-40, 0)); fixture.Press(second, center + new Vector(40, 0));
        fixture.Move(first, center + new Vector(-80, 30)); fixture.Move(second, center + new Vector(80, 30));
        TouchInputFixture.AssertRange(new(35, 65, 20, 80), fixture.Range);
        Assert.Equal(lockedYMinimum, fixture.Range.YMinimum); Assert.Equal(lockedYMaximum, fixture.Range.YMaximum);
        fixture.Release(first, center + new Vector(-80, 30)); fixture.Release(second, center + new Vector(80, 30));
        fixture.Chart.IsZoomEnabled = false; fixture.Chart.IsPanEnabled = true;
        fixture.Press(first, center + new Vector(-40, 0)); fixture.Press(second, center + new Vector(40, 0));
        fixture.Move(first, center + new Vector(-80, 0)); fixture.Move(second, center + new Vector(80, 0));
        Assert.Equal(30, fixture.Range.XMaximum - fixture.Range.XMinimum, 7);
        Assert.Equal(lockedYMinimum, fixture.Range.YMinimum); Assert.Equal(lockedYMaximum, fixture.Range.YMaximum);
    }

    [AvaloniaFact]
    public void Pinch_on_log_axes_uses_geometric_windows_instead_of_arithmetic_data_ranges()
    {
        var chart = TouchInputFixture.CreateChart();
        chart.XAxis = new LogarithmicAxis { Minimum = 1, Maximum = 10_000 };
        chart.YAxis = new LogarithmicAxis { Minimum = 1, Maximum = 10_000 };
        chart.Viewport = new(10, 1000, 10, 1000);
        using var fixture = new TouchInputFixture(chart);
        using var first = TouchInputFixture.Finger(); using var second = TouchInputFixture.Finger(false);
        var center = chart.PlotArea.Center;
        fixture.Press(first, center + new Vector(-40, 0)); fixture.Press(second, center + new Vector(40, 0));
        fixture.Move(first, center + new Vector(-80, 0)); fixture.Move(second, center + new Vector(80, 0));
        var expectedMinimum = Math.Pow(10, 1.5); var expectedMaximum = Math.Pow(10, 2.5);
        TouchInputFixture.AssertRange(new(expectedMinimum, expectedMaximum, expectedMinimum, expectedMaximum), fixture.Range);
    }

    [AvaloniaFact]
    public void Pinch_uses_compressed_break_distance_and_preserves_the_transformed_center()
    {
        var chart = TouchInputFixture.CreateChart();
        chart.XAxis.ScaleBreaks.Add(new() { Start = 40, End = 60, CompressionRatio = .2 });
        using var fixture = new TouchInputFixture(chart);
        using var first = TouchInputFixture.Finger(); using var second = TouchInputFixture.Finger(false);
        var center = chart.PlotArea.Center;
        fixture.Press(first, center + new Vector(-40, 0)); fixture.Press(second, center + new Vector(40, 0));
        fixture.Move(first, center + new Vector(-80, 0)); fixture.Move(second, center + new Vector(80, 0));
        // Removing 80% of [40,60] leaves transformed window [20,64]. Half its
        // span around transformed 42 is [31,53], whose data bounds are [31,69].
        TouchInputFixture.AssertRange(new(31, 69, 35, 65), fixture.Range);
    }

    [AvaloniaFact]
    public void Pinch_preserves_each_explicit_extra_axis_window_in_its_own_transform()
    {
        var chart = TouchInputFixture.CreateChart();
        var extra = new NumericAxis { Key = "extra", Minimum = 0, Maximum = 1000, ViewMinimum = 100, ViewMaximum = 900 };
        chart.Axes.Add(extra);
        chart.Series.Add(new ScatterSeries { YAxisKey = "extra", ItemsSource = new[] { new ChartPoint(50, 500) } });
        using var fixture = new TouchInputFixture(chart);
        using var first = TouchInputFixture.Finger(); using var second = TouchInputFixture.Finger(false);
        var center = chart.PlotArea.Center;
        fixture.Press(first, center + new Vector(-40, 0)); fixture.Press(second, center + new Vector(40, 0));
        fixture.Move(first, center + new Vector(-80, 0)); fixture.Move(second, center + new Vector(80, 0));
        TouchInputFixture.AssertRange(new(35, 65, 35, 65), fixture.Range);
        Assert.Equal(300, extra.ViewMinimum!.Value, 7); Assert.Equal(700, extra.ViewMaximum!.Value, 7);
    }

    [AvaloniaTheory]
    [InlineData("capture")]
    [InlineData("escape")]
    [InlineData("viewport")]
    [InlineData("disable")]
    [InlineData("touch-disable")]
    [InlineData("hide")]
    [InlineData("ancestor-hide")]
    [InlineData("ancestor-disable")]
    [InlineData("resize")]
    [InlineData("source")]
    [InlineData("axis")]
    [InlineData("detach")]
    public void Cancellation_releases_all_contacts_and_stale_moves_cannot_restore_the_old_view(string reason)
    {
        using var fixture = new TouchInputFixture();
        using var first = TouchInputFixture.Finger(); using var second = TouchInputFixture.Finger(false);
        var center = fixture.Chart.PlotArea.Center;
        fixture.Press(first, center + new Vector(-40, 0)); fixture.Press(second, center + new Vector(40, 0));
        fixture.Move(first, center + new Vector(-60, 0)); fixture.Move(second, center + new Vector(60, 0));
        Assert.Same(fixture.Chart, first.Captured); Assert.Same(fixture.Chart, second.Captured);
        switch (reason)
        {
            case "capture": first.Capture(null); break;
            case "escape": fixture.Chart.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape }); break;
            case "viewport": fixture.Chart.Viewport = new(10, 40, 10, 40); break;
            case "disable": fixture.Chart.IsEnabled = false; break;
            case "touch-disable": fixture.Chart.IsTouchEnabled = false; break;
            case "hide": fixture.Chart.IsVisible = false; break;
            case "ancestor-hide": fixture.Container.IsVisible = false; break;
            case "ancestor-disable": fixture.Container.IsEnabled = false; break;
            case "resize": fixture.Chart.Width = 510; fixture.Drain(); break;
            case "source": fixture.Series.ItemsSource = new[] { new ChartPoint(20, 20), new ChartPoint(80, 80) }; break;
            case "axis": fixture.Chart.XAxis.Maximum = 200; break;
            case "detach": fixture.Container.Child = null; break;
        }
        Assert.Null(first.Captured); Assert.Null(second.Captured);
        var requested = fixture.Chart.Viewport;
        fixture.Move(first, center + new Vector(-90, 20)); fixture.Move(second, center + new Vector(90, 20));
        fixture.Release(first, center + new Vector(-90, 20)); fixture.Release(second, center + new Vector(90, 20));
        Assert.Equal(requested, fixture.Chart.Viewport); Assert.Null(fixture.Chart.SelectedPoint);
    }

    [AvaloniaFact]
    public void Reentrant_viewport_binding_supersedes_the_active_pinch_and_releases_both_captures()
    {
        using var fixture = new TouchInputFixture();
        using var first = TouchInputFixture.Finger(); using var second = TouchInputFixture.Finger(false);
        var center = fixture.Chart.PlotArea.Center;
        fixture.Press(first, center + new Vector(-40, 0)); fixture.Press(second, center + new Vector(40, 0));
        var replaced = false;
        fixture.Chart.ViewportChanged += (_, _) =>
        {
            if (replaced) return;
            replaced = true;
            fixture.Chart.Viewport = new(5, 25, 10, 30);
        };
        fixture.Move(first, center + new Vector(-80, 0));
        Assert.True(replaced);
        TouchInputFixture.AssertRange(new(5, 25, 10, 30), fixture.Range);
        Assert.Null(first.Captured); Assert.Null(second.Captured);
        fixture.Move(second, center + new Vector(80, 0));
        TouchInputFixture.AssertRange(new(5, 25, 10, 30), fixture.Range);
    }

    [AvaloniaFact]
    public void Native_pinch_scales_are_cumulative_and_ended_starts_a_fresh_baseline()
    {
        using var fixture = new TouchInputFixture();
        var center = fixture.Chart.PlotArea.Center;
        Assert.True(fixture.Pinch(1.5, center).Handled);
        TouchInputFixture.AssertRange(new(30, 70, 30, 70), fixture.Range);
        fixture.Pinch(2, center);
        TouchInputFixture.AssertRange(new(35, 65, 35, 65), fixture.Range);
        fixture.Chart.RaiseEvent(new PinchEndedEventArgs { RoutedEvent = InputElement.PinchEndedEvent });
        fixture.Pinch(2, fixture.Chart.PlotArea.Center);
        TouchInputFixture.AssertRange(new(42.5, 57.5, 42.5, 57.5), fixture.Range);
    }

    [AvaloniaTheory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Invalid_native_pinch_scale_is_not_consumed_and_does_not_change_the_view(double scale)
    {
        using var fixture = new TouchInputFixture();
        Assert.False(fixture.Pinch(scale, fixture.Chart.PlotArea.Center).Handled);
        TouchInputFixture.AssertRange(new(20, 80, 20, 80), fixture.Range);
    }

    [AvaloniaFact]
    public void Native_pinch_does_not_duplicate_an_active_touch_stream_and_scrollviewer_routing_leaves_it_unhandled()
    {
        using var fixture = new TouchInputFixture();
        using var finger = TouchInputFixture.Finger();
        var center = fixture.Chart.PlotArea.Center;
        fixture.Press(finger, center);
        Assert.False(fixture.Pinch(2, center).Handled);
        TouchInputFixture.AssertRange(new(20, 80, 20, 80), fixture.Range);
        fixture.Release(finger, center);
        fixture.Chart.InputRoutingMode = ChartInputRoutingMode.ScrollViewer;
        Assert.False(fixture.Pinch(2, center).Handled);
        TouchInputFixture.AssertRange(new(20, 80, 20, 80), fixture.Range);
    }

    [AvaloniaFact]
    public void Native_pinch_respects_a_business_change_while_releasing_the_previous_mouse_capture()
    {
        using var fixture = new TouchInputFixture();
        var chart = fixture.Chart;
        var center = chart.PlotArea.Center;
        fixture.Window.MouseDown(chart.TranslatePoint(center, fixture.Window)!.Value, MouseButton.Left);
        var captured = false;
        chart.AddHandler(InputElement.PointerCaptureLostEvent, (_, _) =>
        {
            captured = true;
            chart.InputRoutingMode = ChartInputRoutingMode.ScrollViewer;
        });
        var before = fixture.Range;
        Assert.False(fixture.Pinch(2, center).Handled);
        Assert.True(captured);
        Assert.Equal(before, fixture.Range);
        fixture.Window.MouseUp(chart.TranslatePoint(center, fixture.Window)!.Value, MouseButton.Left);
    }

    [AvaloniaFact]
    public void Raw_touch_inside_a_real_scrollviewer_defers_vertical_scrolling_but_claims_horizontal_chart_pan()
    {
        var chart = TouchInputFixture.CreateChart(); chart.InputRoutingMode = ChartInputRoutingMode.Auto;
        var scroll = new ScrollViewer
        {
            Height = 340, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, IsScrollInertiaEnabled = false,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new StackPanel { Children = { new Border { Height = 180 }, chart, new Border { Height = 650 } } },
        };
        chart.Height = 260;
        using var fixture = new TouchInputFixture(chart, scroll);
        scroll.Offset = new Vector(0, 170); fixture.Drain();
        using var device = new RawTouchTestInjector();
        var center = chart.PlotArea.Center;
        var initial = fixture.Range;
        var offset = scroll.Offset;
        fixture.Raw(device, 1, RawPointerEventType.TouchBegin, center);
        fixture.Raw(device, 1, RawPointerEventType.TouchUpdate, center + new Vector(0, -45));
        fixture.Raw(device, 1, RawPointerEventType.TouchUpdate, center + new Vector(0, -80));
        fixture.Raw(device, 1, RawPointerEventType.TouchEnd, center + new Vector(0, -80));
        Assert.True(scroll.Offset.Y > offset.Y, $"Parent did not scroll: {offset} -> {scroll.Offset}\n{fixture.RawTrace}");
        TouchInputFixture.AssertRange(initial, fixture.Range);
        Assert.Null(chart.SelectedPoint);
        scroll.Offset = new Vector(0, 170); fixture.Drain();
        center = chart.PlotArea.Center; offset = scroll.Offset;
        fixture.Raw(device, 2, RawPointerEventType.TouchBegin, center);
        fixture.Raw(device, 2, RawPointerEventType.TouchUpdate, center + new Vector(45, 0));
        fixture.Raw(device, 2, RawPointerEventType.TouchUpdate, center + new Vector(75, 0));
        fixture.Raw(device, 2, RawPointerEventType.TouchEnd, center + new Vector(75, 0));
        Assert.True(fixture.Range.XMinimum < initial.XMinimum);
        Assert.Equal(offset, scroll.Offset); Assert.Null(chart.SelectedPoint);
    }

    [AvaloniaFact]
    public void Scrollviewer_navigation_routing_still_allows_a_real_touch_tap_to_select()
    {
        var chart = TouchInputFixture.CreateChart(); chart.InputRoutingMode = ChartInputRoutingMode.ScrollViewer; chart.Height = 260;
        var scroll = new ScrollViewer
        {
            Height = 340, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, IsScrollInertiaEnabled = false,
            Content = new StackPanel { Children = { chart, new Border { Height = 650 } } },
        };
        using var fixture = new TouchInputFixture(chart, scroll);
        using var device = new RawTouchTestInjector();
        var center = fixture.DataPoint(50, 50);
        fixture.Raw(device, 1, RawPointerEventType.TouchBegin, center);
        Assert.Null(chart.SelectedPoint);
        fixture.Raw(device, 1, RawPointerEventType.TouchEnd, center);
        Assert.True(chart.SelectedPoint is not null, fixture.RawTrace);
        Assert.Equal(new ChartPoint(50, 50), Assert.IsType<ChartPointHit>(chart.SelectedPoint).Point);
        var offset = scroll.Offset;
        fixture.Raw(device, 2, RawPointerEventType.TouchBegin, center);
        fixture.Raw(device, 2, RawPointerEventType.TouchUpdate, center + new Vector(0, -45));
        fixture.Raw(device, 2, RawPointerEventType.TouchUpdate, center + new Vector(0, -75));
        fixture.Raw(device, 2, RawPointerEventType.TouchEnd, center + new Vector(0, -75));
        Assert.True(scroll.Offset.Y > offset.Y);
        TouchInputFixture.AssertRange(new(20, 80, 20, 80), fixture.Range);
    }

    [AvaloniaFact]
    public void Raw_two_finger_pinch_and_centroid_pan_win_over_the_parent_scroll_recognizer()
    {
        var chart = TouchInputFixture.CreateChart(); chart.InputRoutingMode = ChartInputRoutingMode.Auto; chart.Height = 260;
        var scroll = new ScrollViewer
        {
            Height = 340, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, IsScrollInertiaEnabled = false,
            Content = new StackPanel { Children = { new Border { Height = 180 }, chart, new Border { Height = 650 } } },
        };
        using var fixture = new TouchInputFixture(chart, scroll);
        scroll.Offset = new Vector(0, 170); fixture.Drain();
        using var device = new RawTouchTestInjector();
        var center = chart.PlotArea.Center; var plot = chart.PlotArea; var offset = scroll.Offset;
        fixture.Raw(device, 1, RawPointerEventType.TouchBegin, center + new Vector(-40, 0));
        fixture.Raw(device, 2, RawPointerEventType.TouchBegin, center + new Vector(40, 0));
        fixture.Raw(device, 1, RawPointerEventType.TouchUpdate, center + new Vector(-80, 20));
        fixture.Raw(device, 2, RawPointerEventType.TouchUpdate, center + new Vector(80, 20));
        var y = 35 + 30 * 20 / plot.Height;
        Assert.True(Math.Abs(fixture.Range.XMinimum - 35) < 1e-7, fixture.RawTrace);
        TouchInputFixture.AssertRange(new(35, 65, y, y + 30), fixture.Range);
        Assert.Equal(offset, scroll.Offset);
        fixture.Raw(device, 2, RawPointerEventType.TouchEnd, center + new Vector(80, 20));
        fixture.Raw(device, 1, RawPointerEventType.TouchEnd, center + new Vector(-80, 20));
        Assert.Null(chart.SelectedPoint);
    }

    private sealed class TouchCommand(Action<object?> invoke, Func<object?, bool>? canExecute = null) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => invoke(parameter);
    }
}

internal sealed class TouchInputFixture : IDisposable
{
    private readonly TemplateFeaturesHost _host;
    private readonly List<string> _rawTrace = [];
    private ulong _timestamp;
    public TouchInputFixture(CartesianChart? chart = null, Control? content = null, bool inverse = false)
    {
        Chart = chart ?? CreateChart();
        Chart.XAxis.IsInversed = inverse; Chart.YAxis.IsInversed = inverse;
        if (Chart.Series.Count == 0) Chart.Series.Add(new ScatterSeries { ItemsSource = new[] { new ChartPoint(50, 50), new ChartPoint(30, 30) } });
        Series = Chart.Series[0];
        Container = new Border { Child = content ?? Chart };
        _host = new TemplateFeaturesHost(Chart, Container);
        Chart.AddHandler(InputElement.PointerPressedEvent, TracePointer, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        Chart.AddHandler(InputElement.PointerMovedEvent, TracePointer, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        Chart.AddHandler(InputElement.PointerReleasedEvent, TracePointer, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        Window.AddHandler(InputElement.PointerPressedEvent, TracePointer, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        Window.AddHandler(InputElement.PointerMovedEvent, TracePointer, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        Window.AddHandler(InputElement.PointerReleasedEvent, TracePointer, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
    }
    public static CartesianChart CreateChart() => new()
    {
        ShowLegend = false, IsTouchEnabled = true, IsPanEnabled = true, IsZoomEnabled = true,
        IsKeyboardNavigationEnabled = false, InputRoutingMode = ChartInputRoutingMode.Chart,
        XAxis = new NumericAxis { Minimum = 0, Maximum = 100 }, YAxis = new NumericAxis { Minimum = 0, Maximum = 100 },
        Viewport = new(20, 80, 20, 80),
    };
    public CartesianChart Chart { get; }
    public Window Window => _host.Window;
    public ChartSeries Series { get; }
    public Border Container { get; }
    public ChartViewport Range => Chart.VisibleRange!.Value;
    public string RawTrace => string.Join(Environment.NewLine, _rawTrace);
    private void TracePointer(object? sender, PointerEventArgs args) => _rawTrace.Add(
        $"{sender?.GetType().Name} {args.RoutedEvent?.Name} {args.Route} pointer={args.Pointer.Id}:{args.Pointer.Type} source={args.Source?.GetType().Name} captured={args.Pointer.Captured?.GetType().Name} handled={args.Handled} local={args.GetPosition(Chart)}");
    public static Pointer Finger(bool primary = true) => new(Pointer.GetNextFreeId(), PointerType.Touch, primary);
    public Point DataPoint(double x, double y) => new(Chart.PlotArea.Left + (x - Range.XMinimum) / (Range.XMaximum - Range.XMinimum) * Chart.PlotArea.Width,
        Chart.PlotArea.Bottom - (y - Range.YMinimum) / (Range.YMaximum - Range.YMinimum) * Chart.PlotArea.Height);
    private (Visual Root, Point Point) Position(Point local)
    {
        var root = Chart.GetVisualAncestors().LastOrDefault() ?? Chart;
        return (root, Chart.TranslatePoint(local, root)!.Value);
    }
    public PointerPressedEventArgs Press(IPointer pointer, Point local)
    {
        var (root, point) = Position(local);
        var args = new PointerPressedEventArgs(Chart, pointer, root, point, ++_timestamp, default, KeyModifiers.None)
        { RoutedEvent = InputElement.PointerPressedEvent };
        SegmentTestHost.AssertPosition(local, args.GetPosition(Chart));
        Chart.RaiseEvent(args); Drain(); return args;
    }
    public PointerEventArgs Move(IPointer pointer, Point local)
    {
        var (root, point) = Position(local);
        var args = new PointerEventArgs(InputElement.PointerMovedEvent, Chart, pointer, root, point, ++_timestamp, default, KeyModifiers.None);
        SegmentTestHost.AssertPosition(local, args.GetPosition(Chart));
        Chart.RaiseEvent(args); Drain(); return args;
    }
    public PointerReleasedEventArgs Release(IPointer pointer, Point local)
    {
        var (root, point) = Position(local);
        var args = new PointerReleasedEventArgs(Chart, pointer, root, point, ++_timestamp, default, KeyModifiers.None, MouseButton.Left)
        { RoutedEvent = InputElement.PointerReleasedEvent };
        Chart.RaiseEvent(args); Drain(); return args;
    }
    public PinchEventArgs Pinch(double scale, Point origin)
    {
        var args = new PinchEventArgs(scale, origin) { RoutedEvent = InputElement.PinchEvent, Source = Chart };
        Chart.RaiseEvent(args); Drain(); return args;
    }
    public void Raw(RawTouchTestInjector device, long id, RawPointerEventType type, Point local)
    {
        // Match HeadlessWindowExtensions' real input helpers: commit the scene's
        // hit-test data before delivering the platform packet, and render after it.
        Window.MouseMove(new Point(5, 5));
        Drain(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(2); Drain();
        var root = RawTouchTestInjector.InputRoot(_host.Window);
        var rootVisual = RawTouchTestInjector.RootVisual(root);
        var point = Chart.TranslatePoint(local, rootVisual)!.Value;
        var packet = device.Process(_host.Window, ++_timestamp, root, type, point, id);
        _rawTrace.Add(packet.Trace);
        if (type == RawPointerEventType.TouchBegin)
            Assert.True(packet.Hit is Visual visual && (ReferenceEquals(visual, Chart) || visual.GetVisualAncestors().Contains(Chart)), RawTrace);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(2); Drain();
    }
    public void Drain() => _host.Drain();
    public static void AssertRange(ChartViewport expected, ChartViewport actual)
    {
        Assert.Equal(expected.XMinimum, actual.XMinimum, 7); Assert.Equal(expected.XMaximum, actual.XMaximum, 7);
        Assert.Equal(expected.YMinimum, actual.YMinimum, 7); Assert.Equal(expected.YMaximum, actual.YMaximum, 7);
    }
    public void Dispose() => _host.Dispose();
}

// Avalonia 12's runtime contains TouchDevice's injection constructors/method, but
// deliberately omits them from its reference assembly. Reflection is confined to
// this test adapter: packets enter the actual platform Input callback, then the
// TopLevel/InputManager hit test, TouchDevice, pointer routing and the parent's
// ScrollGestureRecognizer. This is not public API proof
// or a claim that a physical touchscreen has been exercised.
internal sealed class RawTouchTestInjector : IDisposable
{
    private const BindingFlags RuntimeInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private readonly object _device = Activator.CreateInstance(typeof(TouchDevice), nonPublic: true)!;
    private readonly ConstructorInfo _constructor = typeof(RawTouchEventArgs).GetConstructors(RuntimeInstance)
        .Single(constructor => constructor.GetParameters() is var parameters && parameters.Length == 7 && parameters[4].ParameterType == typeof(Point));
    public static IInputRoot InputRoot(TopLevel window) => Assert.IsAssignableFrom<IInputRoot>(
        typeof(TopLevel).GetProperty("InputRoot", RuntimeInstance)!.GetValue(window));
    public static Visual RootVisual(IInputRoot root) => Assert.IsAssignableFrom<Visual>(
        typeof(IInputRoot).GetProperty("RootElement", RuntimeInstance)!.GetValue(root));
    public (string Trace, IInputElement? Hit) Process(TopLevel window, ulong timestamp, IInputRoot root, RawPointerEventType type, Point point, long id)
    {
        var packet = _constructor.Invoke([_device, timestamp, root, type, point, RawInputModifiers.None, id]);
        var platform = typeof(TopLevel).GetProperty("PlatformImpl", RuntimeInstance)!.GetValue(window);
        var input = Assert.IsType<Action<RawInputEventArgs>>(typeof(ITopLevelImpl).GetProperty("Input", RuntimeInstance)!.GetValue(platform));
        input((RawInputEventArgs)packet);
        var hit = typeof(RawPointerEventArgs).GetProperty("InputHitTestResult", RuntimeInstance)!.GetValue(packet);
        var handled = typeof(RawInputEventArgs).GetProperty("Handled", RuntimeInstance)!.GetValue(packet);
        return ($"RAW {type} id={id} position={point} hit={hit} handled={handled}",
            (IInputElement?)hit!.GetType().GetField("Item2")!.GetValue(hit));
    }
    public void Dispose() => ((IDisposable)_device).Dispose();
}
