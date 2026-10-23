using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class CommonInteractionTests
{
    [AvaloniaFact]
    public void Palette_identity_survives_visibility_reordering_and_removal_without_changing_source_identity()
    {
        var first = new LineSeries { Title = "First", ItemsSource = new[] { new ChartPoint(2, 20) } };
        var second = new ScatterSeries { Title = "Second", ItemsSource = new[] { new ChartPoint(8, 80) } };
        var chart = CreateChart(); chart.Series.Add(first); chart.Series.Add(second);
        using var fixture = new Fixture(chart);
        var brush = chart.GetSeriesBrush(first); var other = chart.GetSeriesBrush(second);
        chart.SelectedPoint = chart.HitTestPoint(fixture.Point(2, 20));
        chart.ToggleSeriesVisibility(first); fixture.Drain();
        Assert.False(first.IsVisible); Assert.Null(chart.SelectedPoint);
        Assert.Null(chart.HitTestPoint(fixture.Point(2, 20)));
        Assert.Equal(2, chart.LegendItems.Count);
        chart.Series.Move(0, 1); first.IsVisible = true; fixture.Drain();
        Assert.Same(brush, chart.GetSeriesBrush(first)); Assert.Same(other, chart.GetSeriesBrush(second));
        chart.Series.Remove(first); chart.Series.Add(first); fixture.Drain();
        Assert.Same(brush, chart.GetSeriesBrush(first));
        chart.ColorPalette = new IBrush[] { Brushes.Red, Brushes.Green };
        Assert.Same(Brushes.Red, chart.GetSeriesBrush(first));
        second.Stroke = Brushes.Purple;
        Assert.Same(Brushes.Purple, chart.GetSeriesBrush(second));
    }

    [AvaloniaTheory]
    [InlineData(ChartLegendPosition.Top)]
    [InlineData(ChartLegendPosition.Bottom)]
    [InlineData(ChartLegendPosition.Left)]
    [InlineData(ChartLegendPosition.Right)]
    public void Legend_reserves_space_and_click_toggles_a_hidden_series_back_on(ChartLegendPosition position)
    {
        var chart = CreateChart(); chart.LegendPosition = position;
        var series = new BarSeries { Title = "Revenue", ItemsSource = new[] { new ChartPoint(5, 50) } };
        chart.Series.Add(series);
        using var fixture = new Fixture(chart);
        var item = Assert.Single(chart.LegendItems);
        Assert.False(item.Bounds.Intersects(chart.PlotArea));
        fixture.Click(item.Bounds.Center); Assert.False(series.IsVisible);
        fixture.Click(Assert.Single(chart.LegendItems).Bounds.Center); Assert.True(series.IsVisible);
        chart.IsLegendInteractive = false; fixture.Click(Assert.Single(chart.LegendItems).Bounds.Center);
        Assert.True(series.IsVisible);
    }

    [AvaloniaFact]
    public void Shared_reading_uses_screen_x_and_each_series_original_value_on_independent_axes()
    {
        var chart = CreateChart(); chart.TooltipMode = ChartTooltipMode.SharedX; chart.ShowCrosshair = true;
        var secondary = new NumericAxis { Key = "secondary", Minimum = 0, Maximum = 1, Position = ChartAxisPosition.Far };
        chart.Axes.Add(secondary);
        var line = new LineSeries { Title = "Load", ItemsSource = new[] { new ChartPoint(5, 50) } };
        var scatter = new ScatterSeries { Title = "Success", YAxisKey = "secondary", ItemsSource = new[] { new ChartPoint(5, .9) } };
        chart.Series.Add(line); chart.Series.Add(scatter);
        using var fixture = new Fixture(chart);
        fixture.Move(fixture.Point(5, 30));
        Assert.Equal(2, chart.HoveredPoints.Count);
        Assert.Equal(50, chart.HoveredPoints.Single(hit => ReferenceEquals(hit.Series, line)).Point.Y);
        var secondaryHit = chart.HoveredPoints.Single(hit => ReferenceEquals(hit.Series, scatter));
        Assert.Equal(.9, secondaryHit.Point.Y); Assert.Equal(0, secondaryHit.Index); Assert.NotNull(secondaryHit.StableId);
        string? formatted = null;
        chart.TooltipFormatter = hits => formatted = string.Join("/", hits.Select(hit => hit.Point.Y));
        fixture.Move(fixture.Point(5, 32)); fixture.Drain(); Assert.NotNull(formatted);
        scatter.IsVisible = false; fixture.Drain(); fixture.Move(fixture.Point(5, 33));
        Assert.Single(chart.HoveredPoints);
        chart.IsTooltipEnabled = false; fixture.Drain(); Assert.Empty(chart.HoveredPoints);
    }

    [AvaloniaFact]
    public void Shared_reading_searches_grouped_bar_centers_at_a_category_midpoint()
    {
        var chart = CreateChart(); chart.TooltipMode = ChartTooltipMode.SharedX;
        var left = new BarSeries { ItemsSource = new[] { new ChartPoint(4, 50), new ChartPoint(6, 50) } };
        var right = new BarSeries { ItemsSource = new[] { new ChartPoint(4, 80), new ChartPoint(6, 80) } };
        chart.Series.Add(left); chart.Series.Add(right);
        using var fixture = new Fixture(chart);
        fixture.Move(fixture.Point(5, 50) + new Vector(10, 0));
        Assert.Equal(1, chart.HoveredPoints.Single(hit => ReferenceEquals(hit.Series, left)).Index);
        Assert.Equal(0, chart.HoveredPoints.Single(hit => ReferenceEquals(hit.Series, right)).Index);
    }

    [AvaloniaFact]
    public void Shared_reading_skips_clipped_nearest_x_and_finds_the_next_visible_point()
    {
        var chart = CreateChart(); chart.TooltipMode = ChartTooltipMode.SharedX;
        chart.Series.Add(new ScatterSeries { ItemsSource = new[] { new ChartPoint(5, 1000), new ChartPoint(6, 50) } });
        using var fixture = new Fixture(chart);
        fixture.Move(fixture.Point(5, 50));
        Assert.Equal(1, Assert.Single(chart.HoveredPoints).Index);
    }

    [AvaloniaFact]
    public void External_selection_is_canonical_even_without_a_semantic_change_and_limits_follow_filtering()
    {
        var chart = CreateChart(); chart.SelectionMode = ChartSelectionMode.Multiple; chart.MaxSelectionCount = 2;
        var series = new ScatterSeries { ItemsSource = new[] { new ChartPoint(2, 20), new ChartPoint(6, 60) } };
        chart.Series.Add(series);
        using var fixture = new Fixture(chart);
        var first = chart.HitTestPoint(fixture.Point(2, 20))!; var second = chart.HitTestPoint(fixture.Point(6, 60))!;
        var foreign = first with { Series = new ScatterSeries() };
        chart.SelectedPoints = new[] { foreign, first, first, second };
        Assert.Equal(new[] { first, second }, chart.SelectedPoints);
        chart.SelectedPoints = new[] { first, first, second };
        Assert.Equal(2, chart.SelectedPoints!.Count);
        chart.SelectionMode = ChartSelectionMode.None;
        chart.SelectedPoints = new[] { first };
        Assert.Empty(chart.SelectedPoints!); Assert.Null(chart.SelectedPoint);
    }

    [AvaloniaFact]
    public void Repeated_reference_selection_keeps_the_hit_occurrence_and_many_legends_leave_a_plot()
    {
        var row = new ValueRow(5, 50);
        var chart = CreateChart(); chart.LegendOrientation = Avalonia.Layout.Orientation.Vertical;
        var series = new ScatterSeries { ItemsSource = new[] { row, row }, CategoryPath = nameof(ValueRow.X), ValuePath = nameof(ValueRow.Y) };
        chart.Series.Add(series);
        for (var index = 0; index < 25; index++) chart.Series.Add(new LineSeries { Title = $"Line {index}" });
        using var fixture = new Fixture(chart);
        Assert.True(chart.PlotArea.Height > 20); Assert.InRange(chart.LegendItems.Count, 1, 25);
        chart.SelectedPoint = chart.HitTestPoint(fixture.Point(5, 50)); fixture.Drain();
        Assert.Equal(1, chart.SelectedPoint?.Index);
    }

    [AvaloniaFact]
    public void Multiple_selection_is_bounded_and_migrates_by_identity_then_clears_when_hidden_or_removed()
    {
        var values = new ObservableCollection<ChartPoint> { new(2, 20), new(4, 40), new(6, 60) };
        var series = new ScatterSeries { ItemsSource = values };
        var chart = CreateChart(); chart.SelectionMode = ChartSelectionMode.Multiple; chart.MaxSelectionCount = 2;
        chart.Series.Add(series);
        using var fixture = new Fixture(chart);
        var result = chart.SelectRectangle(chart.PlotArea);
        Assert.Equal(3, result.MatchedCount); Assert.Equal(2, result.SelectedCount); Assert.True(result.IsTruncated);
        Assert.Equal(2, chart.SelectedPoints!.Count); Assert.Equal(chart.SelectedPoints[^1], chart.SelectedPoint);
        var identity = chart.SelectedPoints[0].StableId;
        values.Move(0, 2); fixture.Drain();
        Assert.Contains(chart.SelectedPoints!, hit => ReferenceEquals(hit.StableId, identity) && hit.Index == 2);
        series.IsVisible = false; fixture.Drain(); Assert.Empty(chart.SelectedPoints!); Assert.Null(chart.SelectedPoint);
        series.IsVisible = true; fixture.Drain(); Assert.Empty(chart.SelectedPoints!);
        chart.SelectedPoint = chart.HitTestPoint(fixture.Point(4, 40));
        Assert.Single(chart.SelectedPoints!);
        chart.Series.Clear(); fixture.Drain(); Assert.Empty(chart.SelectedPoints!);
    }

    [AvaloniaFact]
    public void Keyboard_navigation_and_escape_cancel_rectangle_and_release_capture()
    {
        var chart = CreateChart(); chart.IsKeyboardNavigationEnabled = true; chart.IsPanEnabled = true;
        chart.SelectionMode = ChartSelectionMode.Multiple;
        chart.Series.Add(new ScatterSeries { ItemsSource = new[] { new ChartPoint(2, 20), new ChartPoint(5, 50), new ChartPoint(8, 80) } });
        using var fixture = new Fixture(chart);
        chart.Focus(); fixture.Key(Key.Right); Assert.Equal(0, chart.SelectedPoint?.Index);
        fixture.Key(Key.Right); Assert.Equal(1, chart.SelectedPoint?.Index);
        chart.Viewport = new(2, 8, 20, 80); fixture.Drain(); fixture.Key(Key.Home); Assert.Null(chart.Viewport);
        IPointer? pointer = null;
        fixture.Window.AddHandler(InputElement.PointerPressedEvent, (_, e) => pointer = e.Pointer,
            Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        fixture.Press(fixture.Point(1, 10), RawInputModifiers.Shift);
        fixture.Move(fixture.Point(7, 70), RawInputModifiers.Shift | RawInputModifiers.LeftMouseButton);
        Assert.Same(chart, pointer!.Captured);
        fixture.Key(Key.Escape); Assert.Null(pointer.Captured);
        var before = chart.SelectedPoint;
        fixture.Release(fixture.Point(7, 70)); Assert.Equal(before, chart.SelectedPoint);
    }

    [AvaloniaFact]
    public void Single_deselect_and_cancelable_legend_event_have_explicit_state_rules()
    {
        var chart = CreateChart(); chart.SelectionMode = ChartSelectionMode.SingleDeselect;
        var series = new ScatterSeries { ItemsSource = new[] { new ChartPoint(5, 50) } };
        chart.Series.Add(series);
        using var fixture = new Fixture(chart);
        fixture.Click(fixture.Point(5, 50)); Assert.NotNull(chart.SelectedPoint);
        fixture.Click(fixture.Point(5, 50)); Assert.Null(chart.SelectedPoint);
        chart.LegendItemInvoked += (_, e) => e.Cancel = true;
        fixture.Click(Assert.Single(chart.LegendItems).Bounds.Center);
        Assert.True(series.IsVisible);
    }

    [AvaloniaFact]
    public void Rectangle_release_publishes_selection_and_additive_duplicates_do_not_report_truncation()
    {
        var chart = CreateChart(); chart.SelectionMode = ChartSelectionMode.Multiple; chart.MaxSelectionCount = 2;
        chart.Series.Add(new ScatterSeries { ItemsSource = new[] { new ChartPoint(2, 20), new ChartPoint(6, 60) } });
        using var fixture = new Fixture(chart);
        fixture.Press(fixture.Point(1, 10), RawInputModifiers.Shift);
        fixture.Move(fixture.Point(7, 70), RawInputModifiers.Shift | RawInputModifiers.LeftMouseButton);
        fixture.Release(fixture.Point(7, 70));
        Assert.Equal(2, chart.SelectedPoints!.Count); Assert.Null(chart.Viewport);
        var repeat = chart.SelectRectangle(chart.PlotArea, additive: true);
        Assert.Equal(2, repeat.MatchedCount); Assert.Equal(2, repeat.SelectedCount); Assert.False(repeat.IsTruncated);
    }

    [AvaloniaFact]
    public void Resizing_during_a_pan_releases_capture_and_keeps_the_last_requested_view()
    {
        var chart = CreateChart(); chart.IsPanEnabled = true; chart.Viewport = new(2, 8, 20, 80);
        chart.Series.Add(new ScatterSeries { ItemsSource = new[] { new ChartPoint(5, 50) } });
        using var fixture = new Fixture(chart);
        IPointer? pointer = null;
        fixture.Window.AddHandler(InputElement.PointerPressedEvent, (_, e) => pointer = e.Pointer,
            Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        fixture.Press(chart.PlotArea.Center);
        fixture.Move(chart.PlotArea.Center + new Vector(20, 0), RawInputModifiers.LeftMouseButton);
        var view = chart.Viewport;
        fixture.Window.Width -= 100; fixture.Drain();
        Assert.Null(pointer!.Captured); Assert.Equal(view, chart.Viewport);
        fixture.Move(chart.PlotArea.Center + new Vector(60, 0), RawInputModifiers.LeftMouseButton);
        Assert.Equal(view, chart.Viewport);
    }

    private static CartesianChart CreateChart() => new()
    {
        XAxis = new NumericAxis { Minimum = 0, Maximum = 10 },
        YAxis = new NumericAxis { Minimum = 0, Maximum = 100 },
    };
    private sealed record ValueRow(double X, double Y);

    private sealed class Fixture : IDisposable
    {
        private readonly CartesianChart _chart;
        public Fixture(CartesianChart chart)
        {
            _chart = chart; Window = new Window { Width = 760, Height = 460, Content = chart };
            Window.Show(); Drain();
        }
        public Window Window { get; }
        public void Drain()
        {
            Dispatcher.UIThread.RunJobs(); Window.UpdateLayout();
            using var drawing = new DrawingGroup().Open(); _chart.Render(drawing);
        }
        public Point Point(double x, double y) => new(_chart.PlotArea.Left + x / 10 * _chart.PlotArea.Width,
            _chart.PlotArea.Bottom - y / 100 * _chart.PlotArea.Height);
        private Point ToWindow(Point point) => _chart.TranslatePoint(point, Window)!.Value;
        public void Move(Point point, RawInputModifiers modifiers = RawInputModifiers.None) { Window.MouseMove(ToWindow(point), modifiers); Drain(); }
        public void Click(Point point) { Press(point); Release(point); }
        public void Press(Point point, RawInputModifiers modifiers = RawInputModifiers.None) { Window.MouseDown(ToWindow(point), MouseButton.Left, modifiers); Drain(); }
        public void Release(Point point) { Window.MouseUp(ToWindow(point), MouseButton.Left); Drain(); }
        public void Key(Key key) { Window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null); Window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null); Drain(); }
        public void Dispose() => Window.Close();
    }
}
