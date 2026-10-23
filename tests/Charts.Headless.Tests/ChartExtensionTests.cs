using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System.Windows.Input;
using System.Collections.ObjectModel;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class ChartExtensionTests
{
    [AvaloniaFact]
    public void External_series_controls_bounds_render_legend_projection_and_original_hit_identity()
    {
        var renderer = new DiamondRenderer();
        var series = new ExternalSeries(renderer) { Title = "Custom", ItemsSource = new ChartPoint[] { new(3, 7) } };
        var chart = new CartesianChart();
        chart.Series.Add(series);
        var window = Show(chart);
        try
        {
            using (var drawing = new DrawingGroup().Open()) chart.Render(drawing);
            Assert.True(renderer.Rendered);
            Assert.True(renderer.BoundsRead);
            Assert.NotNull(renderer.Projection);
            var screen = renderer.Projection!.Project(new(3, 7));
            var roundtrip = renderer.Projection.Unproject(screen);
            Assert.Equal(3, roundtrip.X, 8); Assert.Equal(7, roundtrip.Y, 8);
            var hit = Assert.IsType<ChartPointHit>(chart.HitTestPoint(screen));
            Assert.Same(series, hit.Series); Assert.Equal(0, hit.Index);
            Assert.Equal(series.CreateSnapshot().Points[0].StableId, hit.StableId);
            Assert.Equal(new ChartPoint(3, 7), hit.Point);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Secondary_axis_uses_its_own_scale_for_drawing_and_hit_testing()
    {
        var chart = new CartesianChart
        {
            XAxis = new NumericAxis { Minimum = 0, Maximum = 10 },
            YAxis = new NumericAxis { Minimum = 0, Maximum = 10 },
        };
        chart.Axes.Add(new NumericAxis { Key = "pressure", Minimum = 0, Maximum = 1000, Title = "kPa", Position = ChartAxisPosition.Far });
        var series = new ScatterSeries { YAxisKey = "pressure", ItemsSource = new ChartPoint[] { new(5, 500) } };
        chart.Series.Add(series);
        var window = Show(chart);
        try
        {
            var hit = Assert.IsType<ChartPointHit>(chart.HitTestPoint(chart.PlotArea.Center));
            Assert.Equal(500, hit.Point.Y); Assert.Same(series, hit.Series);
            chart.Viewport = new(0, 10, 2.5, 7.5);
            hit = Assert.IsType<ChartPointHit>(chart.HitTestPoint(chart.PlotArea.Center));
            Assert.Equal(500, hit.Point.Y);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Axis_title_format_and_series_color_preserve_requested_viewport()
    {
        var chart = new CartesianChart { XAxis = new NumericAxis { Minimum = 0, Maximum = 10 }, YAxis = new NumericAxis { Minimum = 0, Maximum = 100 } };
        var series = new LineSeries { ItemsSource = new ChartPoint[] { new(0, 0), new(10, 100) } };
        chart.Series.Add(series);
        var window = Show(chart);
        try
        {
            var viewport = new ChartViewport(2, 8, 20, 80);
            chart.Viewport = viewport;
            chart.XAxis.Title = "Time"; chart.YAxis.LabelFormat = "0.00"; series.Stroke = Brushes.Red;
            Assert.Equal(viewport, chart.Viewport);
            chart.XAxis.Maximum = 12;
            Assert.Null(chart.Viewport);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Category_keys_align_across_different_source_orders_without_mutating_axis_labels(bool fixedLabels)
    {
        var chart = new CartesianChart { XAxis = new CategoryAxis { Labels = fixedLabels ? new[] { "A", "B" } : null }, YAxis = new NumericAxis { Minimum = 0, Maximum = 100 } };
        var first = new ScatterSeries { CategoryPath = "Name", ValuePath = "Value", ItemsSource = new[] { new CategoryItem("A", 20), new CategoryItem("B", 30) } };
        var second = new ScatterSeries { CategoryPath = "Name", ValuePath = "Value", ItemsSource = new[] { new CategoryItem("B", 70), new CategoryItem("A", 80) } };
        chart.Series.Add(first); chart.Series.Add(second);
        var window = Show(chart);
        try
        {
            var range = chart.VisibleRange!.Value;
            var x = chart.PlotArea.Left + (0 - range.XMinimum) / (range.XMaximum - range.XMinimum) * chart.PlotArea.Width;
            var y = chart.PlotArea.Bottom - .8 * chart.PlotArea.Height;
            var hit = Assert.IsType<ChartPointHit>(chart.HitTestPoint(new(x, y)));
            Assert.Same(second, hit.Series); Assert.Equal(1, hit.Index); Assert.Equal(0, hit.Point.X);
            if (fixedLabels) Assert.Equal(new[] { "A", "B" }, chart.XAxis.Labels); else Assert.Null(chart.XAxis.Labels);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Custom_bounds_override_the_raw_data_extent_and_rotated_labels_reserve_their_full_width()
    {
        var chart = new CartesianChart { XAxis = new NumericAxis { LabelMaxWidth = 300, LabelRotation = 90 } };
        chart.Series.Add(new ExternalSeries(new DiamondRenderer()) { ItemsSource = new ChartPoint[] { new(9999, 9999) } });
        var window = Show(chart);
        try
        {
            Assert.True(chart.VisibleRange!.Value.XMaximum < 20);
            Assert.True(chart.VisibleRange!.Value.YMaximum < 20);
            Assert.True(chart.Bounds.Height - chart.PlotArea.Bottom >= 300);
            using var context = new DrawingGroup().Open(); chart.Render(context);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Selection_tracks_mapped_category_identity_after_move_range_change_and_resize()
    {
        var rows = new ObservableCollection<CategoryItem> { new("B", 70), new("A", 20) };
        var series = new ScatterSeries { CategoryPath = "Name", ValuePath = "Value", ItemsSource = rows };
        var chart = new CartesianChart { XAxis = new CategoryAxis { Labels = new[] { "A", "B" }, Minimum = -.5, Maximum = 1.5 }, YAxis = new NumericAxis { Minimum = 0, Maximum = 100 } };
        chart.Series.Add(series);
        var window = Show(chart);
        try
        {
            var point = new Point(chart.PlotArea.Left + chart.PlotArea.Width * .75, chart.PlotArea.Bottom - chart.PlotArea.Height * .7);
            chart.SelectedPoint = Assert.IsType<ChartPointHit>(chart.HitTestPoint(point));
            var identity = chart.SelectedPoint.StableId;
            rows.Move(0, 1); _ = chart.PlotArea;
            Assert.Same(identity, chart.SelectedPoint!.StableId); Assert.Equal(1, chart.SelectedPoint.Index); Assert.Equal(1, chart.SelectedPoint.Point.X);
            chart.YAxis.Maximum = 200; _ = chart.PlotArea;
            var moved = chart.SelectedPoint.Position;
            Assert.Equal(chart.PlotArea.Bottom - chart.PlotArea.Height * .35, moved.Y, 6);
            window.Width = 1100; window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); _ = chart.PlotArea;
            Assert.NotEqual(moved.X, chart.SelectedPoint.Position.X); Assert.Same(identity, chart.SelectedPoint.StableId);
            rows.RemoveAt(1); Assert.Null(chart.SelectedPoint);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Reversed_logarithmic_zoom_keeps_cursor_value_stationary()
    {
        var chart = new CartesianChart
        {
            IsZoomEnabled = true, InteractionAxes = ChartInteractionAxes.X,
            XAxis = new LogarithmicAxis { Minimum = 1, Maximum = 1000, IsInversed = true },
            YAxis = new NumericAxis { Minimum = 0, Maximum = 10 },
        };
        chart.Series.Add(new ScatterSeries { ItemsSource = new ChartPoint[] { new(10, 5) } });
        var window = Show(chart);
        try
        {
            var local = new Point(chart.PlotArea.Left + chart.PlotArea.Width * 2 / 3, chart.PlotArea.Center.Y);
            var pointer = chart.TranslatePoint(local, window)!.Value;
            window.MouseWheel(pointer, new Vector(0, 3)); Dispatcher.UIThread.RunJobs();
            var range = chart.VisibleRange!.Value;
            var projected = chart.PlotArea.Left + (1 - (Math.Log10(10) - Math.Log10(range.XMinimum)) /
                (Math.Log10(range.XMaximum) - Math.Log10(range.XMinimum))) * chart.PlotArea.Width;
            Assert.Equal(local.X, projected, 6);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Disabling_zoom_pan_axes_still_allows_point_selection_and_command()
    {
        var chart = new CartesianChart
        {
            InteractionAxes = ChartInteractionAxes.None,
            XAxis = new NumericAxis { Minimum = 0, Maximum = 10 }, YAxis = new NumericAxis { Minimum = 0, Maximum = 10 },
        };
        chart.Series.Add(new ScatterSeries { ItemsSource = new ChartPoint[] { new(5, 5) } });
        ChartPointHit? invoked = null;
        chart.PointInvokedCommand = new DelegateCommand(value => invoked = (ChartPointHit?)value);
        var window = Show(chart);
        try
        {
            var pointer = chart.TranslatePoint(chart.PlotArea.Center, window)!.Value;
            window.MouseDown(pointer, MouseButton.Left); window.MouseUp(pointer, MouseButton.Left); Dispatcher.UIThread.RunJobs();
            Assert.NotNull(chart.SelectedPoint); Assert.Equal(chart.SelectedPoint, invoked); Assert.Null(chart.Viewport);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Reentrant_point_command_cannot_capture_a_detached_or_replaced_scene(bool detach)
    {
        var chart = new CartesianChart
        {
            IsPanEnabled = true,
            XAxis = new NumericAxis { Minimum = 0, Maximum = 10 }, YAxis = new NumericAxis { Minimum = 0, Maximum = 10 },
        };
        chart.Series.Add(new ScatterSeries { ItemsSource = new ChartPoint[] { new(5, 5) } });
        var window = Show(chart);
        IPointer? pointer = null;
        window.AddHandler(InputElement.PointerPressedEvent, (_, args) => pointer = args.Pointer, RoutingStrategies.Bubble, handledEventsToo: true);
        chart.PointInvokedCommand = new DelegateCommand(_ => { if (detach) window.Content = null; else chart.Series.Clear(); });
        try
        {
            var position = chart.TranslatePoint(chart.PlotArea.Center, window)!.Value;
            window.MouseDown(position, MouseButton.Left); Dispatcher.UIThread.RunJobs();
            Assert.NotNull(pointer); Assert.NotSame(chart, pointer.Captured);
            window.MouseMove(position + new Vector(20, 10), RawInputModifiers.LeftMouseButton);
            window.MouseUp(position, MouseButton.Left); Assert.Null(chart.Viewport);
        }
        finally { window.Close(); }
    }

    private static Window Show(CartesianChart chart)
    {
        var window = new Window { Width = 900, Height = 550, Content = chart };
        window.Show(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); return window;
    }

    private sealed record CategoryItem(string Name, double Value);
    private sealed class DelegateCommand(Action<object?> execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute(parameter);
    }
    private sealed class ExternalSeries(IChartSeriesRenderer renderer) : ChartSeries
    { public override IChartSeriesRenderer Renderer => renderer; }
    private sealed class DiamondRenderer : IChartSeriesRenderer
    {
        public bool BoundsRead { get; private set; }
        public bool Rendered { get; private set; }
        public IChartProjection? Projection { get; private set; }
        public bool IncludesZero => false;
        public bool UsesCategorySlots => false;
        public ChartLegendSymbol LegendSymbol => ChartLegendSymbol.Rectangle;
        public ChartSeriesBounds? GetBounds(ChartSeries series, ChartDataSnapshot data)
        { BoundsRead = true; return new(0, 10, 0, 10); }
        public void Render(DrawingContext drawing, ChartSeriesRenderContext context)
        { Rendered = true; Projection = context.Projection; drawing.DrawEllipse(Brushes.Magenta, null, context.Projection.Project(new(3, 7)), 8, 8); }
        public ChartSeriesHit? HitTest(ChartSeriesRenderContext context, Point position, double tolerance)
        {
            var center = context.Projection.Project(new(3, 7)); var delta = position - center;
            var distance = delta.X * delta.X + delta.Y * delta.Y;
            return distance <= tolerance * tolerance ? new(0, center, distance) : null;
        }
    }
}
