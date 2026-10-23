using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class ChartInteractionTests
{
    [AvaloniaFact]
    public void Pointer_hover_identifies_the_original_item_and_leaving_or_disabling_hides_the_tooltip()
    {
        var chart = CreateChart();
        var series = new ScatterSeries { ItemsSource = new ChartPoint[] {new(double.NaN, 20), new(5, 50), new(9, 90)} };
        chart.Series.Add(series);
        using var fixture = new InteractionFixture(chart);
        Assert.True(chart.IsTooltipEnabled);

        fixture.Move(fixture.DataPoint(5, 50) + new Vector(3, 2));
        var hit = Assert.IsType<ChartPointHit>(chart.HoveredPoint);
        Assert.Same(series, hit.Series);
        Assert.Equal(new ChartPoint(5, 50), hit.Point);
        Assert.Equal(1, hit.Index);
        Assert.True(chart.PlotArea.Contains(hit.Position));

        fixture.Window.MouseMove(new Point(5, 5));
        fixture.Drain();
        Assert.Null(chart.HoveredPoint);
        fixture.Move(fixture.DataPoint(5, 50));
        Assert.NotNull(chart.HoveredPoint);
        chart.IsTooltipEnabled = false;
        fixture.Drain();
        fixture.Move(fixture.DataPoint(5, 50));
        Assert.Null(chart.HoveredPoint);
    }

    [AvaloniaFact]
    public void Bar_hit_testing_uses_the_entire_bar_and_rejects_positions_outside_the_plot()
    {
        var chart = CreateChart();
        var series = new BarSeries { ItemsSource = new ChartPoint[] {new(5, 80)} };
        chart.Series.Add(series);
        using var fixture = new InteractionFixture(chart);

        var hit = Assert.IsType<ChartPointHit>(chart.HitTestPoint(fixture.DataPoint(5, 20), tolerance: 1));
        Assert.Same(series, hit.Series);
        Assert.Equal(new ChartPoint(5, 80), hit.Point);
        Assert.Null(chart.HitTestPoint(new Point(chart.PlotArea.Left - 10, chart.PlotArea.Center.Y), tolerance: 100));
        Assert.Null(chart.HitTestPoint(fixture.DataPoint(8, 20), tolerance: 1));
    }

    [AvaloniaFact]
    public void Wheel_zoom_is_opt_in_and_only_consumes_wheel_events_inside_the_plot()
    {
        var chart = CreateChart();
        chart.Series.Add(new ScatterSeries { ItemsSource = new ChartPoint[] {new(5, 50)} });
        using var fixture = new InteractionFixture(chart);
        var handled = new List<bool>();
        fixture.Window.AddHandler(InputElement.PointerWheelChangedEvent, (_, args) => handled.Add(args.Handled), RoutingStrategies.Bubble, handledEventsToo: true);

        Assert.False(chart.IsZoomEnabled);
        fixture.Wheel(chart.PlotArea.Center);
        Assert.Null(chart.Viewport);
        Assert.False(handled[^1]);

        chart.IsZoomEnabled = true;
        fixture.Wheel(new Point(chart.PlotArea.Left - 12, chart.PlotArea.Center.Y));
        Assert.Null(chart.Viewport);
        Assert.False(handled[^1]);
        fixture.Wheel(chart.PlotArea.Center);
        Assert.NotNull(chart.Viewport);
        Assert.True(handled[^1]);
    }

    [AvaloniaFact]
    public void Wheel_zoom_keeps_the_pointer_data_coordinate_anchored_and_reset_preserves_axis_settings()
    {
        var chart = CreateChart();
        chart.IsZoomEnabled = true;
        chart.Series.Add(new LineSeries { ItemsSource = new ChartPoint[] {new(0, 0), new(5, 50), new(10, 100)} });
        using var fixture = new InteractionFixture(chart);
        var original = fixture.VisibleRange;
        var notifications = 0;
        chart.ViewportChanged += (_, _) => notifications++;
        const double xFraction = 0.25;
        const double yFraction = 0.25;
        var pointer = new Point(chart.PlotArea.Left + chart.PlotArea.Width * xFraction,
            chart.PlotArea.Bottom - chart.PlotArea.Height * yFraction);

        fixture.Wheel(pointer);
        var zoomed = Assert.IsType<ChartViewport>(chart.Viewport);
        Assert.True(zoomed.XMaximum - zoomed.XMinimum < original.XMaximum - original.XMinimum);
        Assert.True(zoomed.YMaximum - zoomed.YMinimum < original.YMaximum - original.YMinimum);
        Assert.Equal(original.XMinimum + (original.XMaximum - original.XMinimum) * xFraction,
            zoomed.XMinimum + (zoomed.XMaximum - zoomed.XMinimum) * xFraction, 8);
        Assert.Equal(original.YMinimum + (original.YMaximum - original.YMinimum) * yFraction,
            zoomed.YMinimum + (zoomed.YMaximum - zoomed.YMinimum) * yFraction, 8);
        Assert.Equal(1, notifications);

        chart.ResetViewport();
        fixture.Drain();
        Assert.Null(chart.Viewport);
        Assert.Equal(original, fixture.VisibleRange);
        Assert.Equal(0, chart.XAxis.Minimum);
        Assert.Equal(10, chart.XAxis.Maximum);
        Assert.Equal(0, chart.YAxis.Minimum);
        Assert.Equal(100, chart.YAxis.Maximum);
        Assert.Equal(2, notifications);
    }

    [AvaloniaFact]
    public void X_only_interaction_leaves_the_y_range_unchanged_for_zoom_and_pan()
    {
        var chart = CreateChart();
        chart.IsZoomEnabled = true;
        chart.IsPanEnabled = true;
        chart.InteractionAxes = ChartInteractionAxes.X;
        chart.Series.Add(new ScatterSeries { ItemsSource = new ChartPoint[] {new(5, 50)} });
        using var fixture = new InteractionFixture(chart);
        var original = fixture.VisibleRange;

        fixture.Wheel(chart.PlotArea.Center);
        var zoomed = fixture.VisibleRange;
        Assert.True(zoomed.XMaximum - zoomed.XMinimum < original.XMaximum - original.XMinimum);
        Assert.Equal(original.YMinimum, zoomed.YMinimum);
        Assert.Equal(original.YMaximum, zoomed.YMaximum);
        fixture.Press(chart.PlotArea.Center);
        fixture.Move(chart.PlotArea.Center + new Vector(35, 30), RawInputModifiers.LeftMouseButton);
        fixture.Release(chart.PlotArea.Center + new Vector(35, 30));
        var panned = fixture.VisibleRange;
        Assert.NotEqual(zoomed.XMinimum, panned.XMinimum);
        Assert.Equal(original.YMinimum, panned.YMinimum);
        Assert.Equal(original.YMaximum, panned.YMaximum);
    }

    [AvaloniaFact]
    public void Drag_pan_is_opt_in_preserves_spans_and_stops_after_release()
    {
        var chart = CreateChart();
        chart.Series.Add(new ScatterSeries { ItemsSource = new ChartPoint[] {new(5, 50)} });
        using var fixture = new InteractionFixture(chart);
        var origin = chart.PlotArea.Center;
        Assert.False(chart.IsPanEnabled);
        fixture.Press(origin);
        fixture.Move(origin + new Vector(25, 15), RawInputModifiers.LeftMouseButton);
        fixture.Release(origin + new Vector(25, 15));
        Assert.Null(chart.Viewport);

        chart.IsPanEnabled = true;
        chart.Viewport = new ChartViewport(2, 8, 20, 80);
        fixture.Drain();
        origin = chart.PlotArea.Center + new Vector(40, -20);
        var original = fixture.VisibleRange;
        fixture.Press(origin);
        fixture.Move(origin + new Vector(35, 20), RawInputModifiers.LeftMouseButton);
        var panned = fixture.VisibleRange;
        Assert.True(panned.XMinimum < original.XMinimum);
        Assert.True(panned.YMinimum > original.YMinimum);
        Assert.Equal(original.XMaximum - original.XMinimum, panned.XMaximum - panned.XMinimum, 8);
        Assert.Equal(original.YMaximum - original.YMinimum, panned.YMaximum - panned.YMinimum, 8);
        fixture.Release(origin + new Vector(35, 20));
        fixture.Move(origin + new Vector(70, 50));
        Assert.Equal(panned, fixture.VisibleRange);
    }

    [AvaloniaFact]
    public void Capture_loss_ends_pan_and_requires_a_new_press()
    {
        var chart = CreateChart();
        chart.IsPanEnabled = true;
        chart.Viewport = new ChartViewport(2, 8, 20, 80);
        chart.Series.Add(new ScatterSeries { ItemsSource = new ChartPoint[] {new(5, 50)} });
        using var fixture = new InteractionFixture(chart);
        IPointer? pointer = null;
        fixture.Window.AddHandler(InputElement.PointerPressedEvent, (_, args) => pointer = args.Pointer, RoutingStrategies.Bubble, handledEventsToo: true);
        var origin = chart.PlotArea.Center;
        fixture.Press(origin);
        Assert.Same(chart, pointer!.Captured);
        fixture.Move(origin + new Vector(20, 10), RawInputModifiers.LeftMouseButton);
        var beforeLoss = fixture.VisibleRange;

        pointer.Capture(null);
        fixture.Move(origin + new Vector(60, 30), RawInputModifiers.LeftMouseButton);
        Assert.Equal(beforeLoss, fixture.VisibleRange);
        fixture.Release(origin + new Vector(60, 30));
        var nextOrigin = origin + new Vector(40, -20);
        fixture.Press(nextOrigin);
        fixture.Move(nextOrigin + new Vector(15, 0), RawInputModifiers.LeftMouseButton);
        fixture.Release(nextOrigin + new Vector(15, 0));
        Assert.NotEqual(beforeLoss.XMinimum, fixture.VisibleRange.XMinimum);
    }

    [AvaloniaFact]
    public void Hit_testing_and_hover_use_the_zoomed_viewport_and_skip_hidden_points()
    {
        var chart = CreateChart();
        var series = new ScatterSeries { ItemsSource = new ChartPoint[] {new(1, 10), new(3, 35), new(9, 90)} };
        chart.Series.Add(series);
        using var fixture = new InteractionFixture(chart);
        chart.Viewport = new ChartViewport(2, 6, 20, 60);
        fixture.Drain();

        var visiblePosition = fixture.DataPoint(3, 35);
        var hit = Assert.IsType<ChartPointHit>(chart.HitTestPoint(visiblePosition));
        Assert.Equal(new ChartPoint(3, 35), hit.Point);
        Assert.Equal(1, hit.Index);
        fixture.Move(visiblePosition);
        Assert.Equal(new ChartPoint(3, 35), Assert.IsType<ChartPointHit>(chart.HoveredPoint).Point);
        Assert.Null(chart.HitTestPoint(new Point(chart.PlotArea.Left, chart.PlotArea.Bottom), tolerance: 2));
    }

    [AvaloniaFact]
    public void Clearing_data_and_replacing_axes_cannot_leave_stale_hover_or_viewport()
    {
        var chart = CreateChart();
        var items = new ObservableCollection<ChartPoint> { new(5, 50) };
        chart.Series.Add(new ScatterSeries { ItemsSource = items });
        using var fixture = new InteractionFixture(chart);
        fixture.Move(fixture.DataPoint(5, 50));
        Assert.NotNull(chart.HoveredPoint);

        items.Clear();
        fixture.Drain();
        Assert.Null(chart.HoveredPoint);
        items.Add(new ChartPoint(5, 50));
        chart.Viewport = new ChartViewport(2, 8, 20, 80);
        fixture.Drain();
        fixture.Move(fixture.DataPoint(5, 50) + new Vector(1, 1));
        Assert.NotNull(chart.HoveredPoint);
        chart.XAxis = new ChartAxis { Minimum = 0, Maximum = 20 };
        fixture.Drain();
        Assert.Null(chart.HoveredPoint);
        Assert.Null(chart.Viewport);
        Assert.Equal(20, fixture.VisibleRange.XMaximum);
    }

    [AvaloniaFact]
    public void Double_clicking_the_plot_resets_a_requested_viewport()
    {
        var chart = CreateChart();
        chart.IsZoomEnabled = true;
        chart.Viewport = new ChartViewport(2, 8, 20, 80);
        chart.Series.Add(new ScatterSeries { ItemsSource = new ChartPoint[] {new(5, 50)} });
        using var fixture = new InteractionFixture(chart);
        fixture.Window.MouseDown(new Point(5, 5), MouseButton.Left);
        fixture.Window.MouseUp(new Point(5, 5), MouseButton.Left);
        var clickCounts = new List<int>();
        fixture.Window.AddHandler(InputElement.PointerPressedEvent, (_, args) => clickCounts.Add(args.ClickCount), RoutingStrategies.Bubble, handledEventsToo: true);
        var position = chart.PlotArea.Center;

        fixture.Press(position);
        fixture.Release(position);
        Assert.NotNull(chart.Viewport);
        fixture.Press(position);
        fixture.Release(position);

        Assert.Equal([1, 2], clickCounts);
        Assert.Null(chart.Viewport);
        Assert.Equal(new ChartViewport(0, 10, 0, 100), fixture.VisibleRange);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Disabling_pan_or_detaching_during_a_drag_releases_capture_and_stops_viewport_updates(bool detach)
    {
        var chart = CreateChart();
        chart.IsPanEnabled = true;
        chart.Viewport = new ChartViewport(2, 8, 20, 80);
        chart.Series.Add(new ScatterSeries { ItemsSource = new ChartPoint[] {new(5, 50)} });
        using var fixture = new InteractionFixture(chart);
        IPointer? pointer = null;
        fixture.Window.AddHandler(InputElement.PointerPressedEvent, (_, args) => pointer = args.Pointer, RoutingStrategies.Bubble, handledEventsToo: true);
        var origin = chart.PlotArea.Center;
        fixture.Press(origin);
        fixture.Move(origin + new Vector(25, 10), RawInputModifiers.LeftMouseButton);
        Assert.Same(chart, pointer!.Captured);
        var beforeStop = chart.Viewport;
        var nextPosition = chart.TranslatePoint(origin + new Vector(60, 30), fixture.Window)!.Value;

        if (detach)
            fixture.Window.Content = null;
        else
            chart.IsPanEnabled = false;
        Assert.Null(pointer.Captured);
        fixture.Window.MouseMove(nextPosition, RawInputModifiers.LeftMouseButton);
        fixture.Window.MouseUp(nextPosition, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        fixture.Window.UpdateLayout();
        Assert.Equal(beforeStop, chart.Viewport);
    }

    [AvaloniaFact]
    public void Hover_renders_a_tooltip_rectangle_using_the_current_theme_background()
    {
        var chart = CreateChart();
        chart.Series.Add(new ScatterSeries { Title = "Measured", ItemsSource = new ChartPoint[] {new(5, 50)} });
        using var fixture = new InteractionFixture(chart);
        fixture.Window.RequestedThemeVariant = ThemeVariant.Light;
        fixture.Drain();
        var light = Assert.IsAssignableFrom<ISolidColorBrush>(chart.TooltipBackground).Color;
        fixture.Move(fixture.DataPoint(5, 50));
        Assert.NotNull(chart.HoveredPoint);
        Assert.Contains(GeometryDrawings(fixture.LastDrawing), drawing =>
            drawing.Brush is ISolidColorBrush brush && brush.Color == light && drawing.GetBounds().Width >= 90 && drawing.GetBounds().Height > 20);

        fixture.Window.RequestedThemeVariant = ThemeVariant.Dark;
        fixture.Drain();
        var dark = Assert.IsAssignableFrom<ISolidColorBrush>(chart.TooltipBackground).Color;
        Assert.NotEqual(light, dark);
        fixture.Move(fixture.DataPoint(5, 50) + new Vector(1, 1));
        Assert.NotNull(chart.HoveredPoint);
        Assert.Contains(GeometryDrawings(fixture.LastDrawing), drawing =>
            drawing.Brush is ISolidColorBrush brush && brush.Color == dark && drawing.GetBounds().Width >= 90 && drawing.GetBounds().Height > 20);
    }

    [AvaloniaFact]
    public void Large_marker_edges_and_the_visible_part_of_clipped_markers_remain_hittable()
    {
        var chart = CreateChart();
        chart.Series.Add(new ScatterSeries { MarkerSize = 40, StrokeThickness = 4, ItemsSource = new ChartPoint[] {new(-0.1, 50), new(5, 50)} });
        using var fixture = new InteractionFixture(chart);

        var edge = fixture.DataPoint(5, 50) + new Vector(18, 0);
        Assert.Equal(1, Assert.IsType<ChartPointHit>(chart.HitTestPoint(edge, tolerance: 1)).Index);
        var clippedEdge = new Point(chart.PlotArea.Left + 4, fixture.DataPoint(0, 50).Y);
        Assert.Equal(0, Assert.IsType<ChartPointHit>(chart.HitTestPoint(clippedEdge, tolerance: 1)).Index);
    }

    [AvaloniaFact]
    public void Zooming_to_more_precise_tick_labels_keeps_the_plot_rectangle_stable()
    {
        var chart = CreateChart();
        chart.IsZoomEnabled = true;
        chart.Series.Add(new LineSeries { ItemsSource = new ChartPoint[] {new(0, 0), new(10, 100)} });
        using var fixture = new InteractionFixture(chart);
        var original = chart.PlotArea;

        chart.Viewport = new ChartViewport(2, 8, 0.12345, 0.12346);
        fixture.Drain();
        Assert.Equal(original, chart.PlotArea);
        fixture.Wheel(chart.PlotArea.Center);
        Assert.Equal(original, chart.PlotArea);
    }

    [AvaloniaFact]
    public void An_external_axis_view_update_during_drag_releases_capture_and_preserves_the_new_view()
    {
        var chart = CreateChart();
        chart.IsPanEnabled = true;
        chart.Viewport = new ChartViewport(2, 8, 20, 80);
        var extra = new NumericAxis { Key = "extra", Minimum = 0, Maximum = 1000, ViewMinimum = 200, ViewMaximum = 800 };
        chart.Axes.Add(extra);
        chart.Series.Add(new LineSeries { YAxisKey = "extra", ItemsSource = new ChartPoint[] { new(0, 0), new(10, 1000) } });
        using var fixture = new InteractionFixture(chart);
        IPointer? pointer = null;
        fixture.Window.AddHandler(InputElement.PointerPressedEvent, (_, args) => pointer = args.Pointer, RoutingStrategies.Bubble, handledEventsToo: true);
        var origin = chart.PlotArea.Center;
        fixture.Press(origin);
        fixture.Move(origin + new Vector(20, 10), RawInputModifiers.LeftMouseButton);
        Assert.Same(chart, pointer!.Captured);
        extra.ViewMinimum = 400;
        extra.ViewMaximum = 600;
        fixture.Drain();
        Assert.Null(pointer.Captured);
        fixture.Move(origin + new Vector(80, 50), RawInputModifiers.LeftMouseButton);
        Assert.Equal(400, extra.ViewMinimum);
        Assert.Equal(600, extra.ViewMaximum);
        fixture.Release(origin);
    }

    [AvaloniaFact]
    public void Axis_maximum_zoom_above_one_million_is_respected_by_wheel_and_pan()
    {
        var chart = CreateChart();
        chart.IsZoomEnabled = true; chart.IsPanEnabled = true;
        chart.XAxis.MaximumZoom = 100_000_000;
        chart.Viewport = new ChartViewport(5, 5.000001, 20, 80);
        chart.Series.Add(new LineSeries { ItemsSource = new ChartPoint[] { new(0, 0), new(10, 100) } });
        using var fixture = new InteractionFixture(chart);
        var before = fixture.VisibleRange.XMaximum - fixture.VisibleRange.XMinimum;
        fixture.Wheel(chart.PlotArea.Center);
        var zoomed = fixture.VisibleRange.XMaximum - fixture.VisibleRange.XMinimum;
        Assert.True(zoomed < before);
        Assert.True(zoomed < 10d / 1_000_000);
        fixture.Press(chart.PlotArea.Center);
        fixture.Move(chart.PlotArea.Center + new Vector(20, 0), RawInputModifiers.LeftMouseButton);
        Assert.Equal(zoomed, fixture.VisibleRange.XMaximum - fixture.VisibleRange.XMinimum, 10);
        fixture.Release(chart.PlotArea.Center);
    }

    private static IEnumerable<GeometryDrawing> GeometryDrawings(Drawing drawing)
    {
        if (drawing is GeometryDrawing geometry)
            yield return geometry;
        if (drawing is DrawingGroup group)
            foreach (var child in group.Children)
                foreach (var descendant in GeometryDrawings(child))
                    yield return descendant;
    }

    private static CartesianChart CreateChart() => new()
    {
        ShowLegend = false,
        XAxis = new ChartAxis { Minimum = 0, Maximum = 10 },
        YAxis = new ChartAxis { Minimum = 0, Maximum = 100 }
    };

    private sealed class InteractionFixture : IDisposable
    {
        public InteractionFixture(CartesianChart chart)
        {
            Chart = chart;
            Window = new Window { Width = 680, Height = 400, Content = new Border { Padding = new Thickness(20), Child = chart } };
            Window.Show();
            Drain();
        }

        public CartesianChart Chart { get; }
        public Window Window { get; }
        public DrawingGroup LastDrawing { get; private set; } = new();
        public ChartViewport VisibleRange => Assert.IsType<ChartViewport>(Chart.VisibleRange);

        public void Drain()
        {
            Dispatcher.UIThread.RunJobs();
            Window.UpdateLayout();
            LastDrawing = new DrawingGroup();
            using var context = LastDrawing.Open();
            Chart.Render(context);
        }

        public Point DataPoint(double x, double y)
        {
            var range = VisibleRange;
            return new Point(Chart.PlotArea.Left + (x - range.XMinimum) / (range.XMaximum - range.XMinimum) * Chart.PlotArea.Width,
                Chart.PlotArea.Bottom - (y - range.YMinimum) / (range.YMaximum - range.YMinimum) * Chart.PlotArea.Height);
        }

        private Point ToWindow(Point local) => Chart.TranslatePoint(local, Window)!.Value;
        public void Move(Point local, RawInputModifiers modifiers = RawInputModifiers.None) { Window.MouseMove(ToWindow(local), modifiers); Drain(); }
        public void Wheel(Point local) { Window.MouseWheel(ToWindow(local), new Vector(0, 1)); Drain(); }
        public void Press(Point local) { Window.MouseDown(ToWindow(local), MouseButton.Left); Drain(); }
        public void Release(Point local) { Window.MouseUp(ToWindow(local), MouseButton.Left); Drain(); }
        public void Dispose() => Window.Close();
    }
}
