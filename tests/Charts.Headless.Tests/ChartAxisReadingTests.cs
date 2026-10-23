using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class ChartAxisReadingTests
{
    [AvaloniaFact]
    public void Shared_y_finds_visible_points_and_breaks_ties_by_screen_x_then_last_physical_item()
    {
        var chart = CreateChart(); chart.TooltipMode = ChartTooltipMode.SharedY;
        var rows = new[] { new Row("outside", 100, 50), new Row("right", 6, 60), new Row("first", 4, 60), new Row("last", 4, 60) };
        var series = new ScatterSeries { ItemsSource = rows, CategoryPath = nameof(Row.X), ValuePath = nameof(Row.Y), IdPath = nameof(Row.Id) };
        chart.Series.Add(series);
        using var host = new Host(chart);
        host.Move(host.Point(4, 50));
        var hit = Assert.Single(chart.HoveredPoints);
        Assert.Equal(3, hit.Index); Assert.Same(rows[3], hit.Item); Assert.Equal("last", hit.StableId);
        var expected = host.Point(4, 60);
        Assert.InRange(Math.Sqrt(Math.Pow(expected.X - hit.Position.X, 2) + Math.Pow(expected.Y - hit.Position.Y, 2)), 0, 1e-8);
    }

    [AvaloniaFact]
    public void Shared_y_uses_the_coupled_custom_projection_full_scan_without_calling_map_y()
    {
        var chart = CreateChart(); chart.TooltipMode = ChartTooltipMode.SharedY;
        chart.Series.Add(new ScatterSeries { ItemsSource = new ChartPoint[] { new(1, 20), new(9, 19) } });
        using var host = new Host(chart);
        // Initial pointer entry changes IsPointerOver and invalidates the chart scene.
        // Enter first, then replace this stable scene's projection for the fallback oracle.
        host.Move(new Point(1, 1));
        var scene = Scene(chart);
        var snapshots = (Array)scene.GetType().GetProperty("Snapshots")!.GetValue(scene)!;
        var snapshot = snapshots.GetValue(0)!;
        var property = snapshot.GetType().GetProperty("Context")!;
        var context = (ChartSeriesRenderContext)property.GetValue(snapshot)!;
        property.SetValue(snapshot, context with { Projection = new CoupledProjection(context.Projection) });
        host.Move(host.Point(5, 60));
        Assert.Same(scene, Scene(chart));
        var hit = Assert.Single(chart.HoveredPoints);
        Assert.Equal(1, hit.Index);
        var expected = host.Point(9, 64);
        Assert.InRange(Math.Sqrt(Math.Pow(expected.X - hit.Position.X, 2) + Math.Pow(expected.Y - hit.Position.Y, 2)), 0, 1e-8);
    }

    [AvaloniaFact]
    public void Axis_readings_inverse_project_the_primary_position_for_every_axis_independent_of_direction_flags()
    {
        var chart = CreateChart();
        var log = new LogarithmicAxis { Key = "log", Minimum = 1, Maximum = 1000, Position = ChartAxisPosition.Far, LabelFormat = "0.000" };
        chart.Axes.Add(log);
        chart.Series.Add(new ScatterSeries { ItemsSource = new ChartPoint[] { new(3.7, 37) } });
        chart.Series.Add(new ScatterSeries { YAxisKey = "log", ItemsSource = new ChartPoint[] { new(8, 10) } });
        using var host = new Host(chart);
        host.Move(host.Point(3.7, 37));
        Assert.Equal(3, chart.AxisReadings.Count);
        var main = Assert.Single(chart.AxisReadings, row => ReferenceEquals(row.Axis, chart.YAxis));
        var secondary = Assert.Single(chart.AxisReadings, row => ReferenceEquals(row.Axis, log));
        Assert.Equal(37, main.Value, 9);
        Assert.Equal(Math.Pow(1000, .37), secondary.Value, 9);
        Assert.Equal(secondary.Value.ToString("0.000", System.Globalization.CultureInfo.CurrentCulture), secondary.Text);
        chart.CrosshairDirection = ChartCrosshairDirection.Vertical; chart.ShowCrosshair = false; chart.ShowCrosshairLabels = true;
        Assert.Equal(3, chart.AxisReadings.Count);
        Assert.Single(chart.AxisReadings, row => row.IsHorizontal);
        chart.IsTooltipEnabled = false;
        Assert.Empty(chart.AxisReadings); Assert.Empty(chart.HoveredPoints); Assert.Null(chart.HoveredPoint);
    }

    [AvaloniaTheory]
    [InlineData(ChartCrosshairDirection.Both, 2, 2)]
    [InlineData(ChartCrosshairDirection.Horizontal, 1, 1)]
    [InlineData(ChartCrosshairDirection.Vertical, 1, 1)]
    public void Crosshair_direction_controls_lines_and_axis_labels_independently(ChartCrosshairDirection direction, int lineCount, int labelCount)
    {
        var chart = CreateChart(); chart.ShowCrosshair = true; chart.CrosshairDirection = direction;
        chart.Series.Add(new ScatterSeries { ItemsSource = new ChartPoint[] { new(5, 50) } });
        using var host = new Host(chart); host.Move(host.Point(5, 50));
        var lines = Leaves(DrawOverlay(chart)).OfType<GeometryDrawing>().Where(drawing =>
            drawing.Geometry!.Bounds.Width == 0 || drawing.Geometry.Bounds.Height == 0).ToArray();
        Assert.Equal(lineCount, lines.Length);
        if (direction == ChartCrosshairDirection.Horizontal) Assert.All(lines, line => Assert.Equal(0, line.Geometry!.Bounds.Height));
        if (direction == ChartCrosshairDirection.Vertical) Assert.All(lines, line => Assert.Equal(0, line.Geometry!.Bounds.Width));
        chart.ShowCrosshair = false; chart.ShowCrosshairLabels = true;
        var drawings = Leaves(DrawOverlay(chart)).ToArray();
        var boxes = drawings.OfType<GeometryDrawing>().Where(drawing => drawing.Brush is not null).ToArray();
        Assert.Equal(labelCount, boxes.Length);
        Assert.NotEmpty(drawings.OfType<GlyphRunDrawing>());
        Assert.Equal(2, chart.AxisReadings.Count);
    }

    [AvaloniaFact]
    public void Default_layout_and_nearest_hover_do_not_construct_the_lazy_y_index()
    {
        var chart = CreateChart();
        var series = new ScatterSeries { ItemsSource = new ChartPoint[] { new(2, 20), new(8, 80) } }; chart.Series.Add(series);
        using var host = new Host(chart);
        var snapshot = series.CreateSnapshot();
        var cache = (ConditionalWeakTable<ChartDataSnapshot, ChartReadingIndex>)typeof(ChartReadingIndex)
            .GetField("Cache", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        Assert.False(cache.TryGetValue(snapshot, out _));
        host.Move(host.Point(2, 20));
        Assert.False(cache.TryGetValue(snapshot, out _));
        chart.TooltipMode = ChartTooltipMode.SharedY; host.Move(host.Point(2, 21));
        Assert.True(cache.TryGetValue(snapshot, out _));
    }

    [AvaloniaFact]
    public void Hover_property_source_reentrancy_does_not_restore_old_hits_or_readings()
    {
        var chart = CreateChart(); var series = new ScatterSeries { ItemsSource = new ChartPoint[] { new(5, 50) } };
        chart.Series.Add(series); using var host = new Host(chart);
        var changed = false;
        chart.PropertyChanged += (_, args) =>
        {
            if (args.Property != CartesianChart.HoveredPointsProperty || changed || chart.HoveredPoints.Count == 0) return;
            changed = true; series.ItemsSource = new ChartPoint[] { new(8, 80) };
        };
        host.Move(host.Point(5, 50));
        Assert.True(changed); Assert.Empty(chart.HoveredPoints); Assert.Null(chart.HoveredPoint); Assert.Empty(chart.AxisReadings);
    }

    [AvaloniaFact]
    public void A_nested_clear_finishes_empty_hover_notifications_without_restoring_old_state()
    {
        var chart = CreateChart(); var series = new ScatterSeries { ItemsSource = new ChartPoint[] { new(5, 50) } };
        chart.Series.Add(series); using var host = new Host(chart);
        host.Move(host.Point(5, 50));
        ChartPointHit? lastNotification = chart.HoveredPoint;
        var changed = false;
        chart.PropertyChanged += (_, args) =>
        {
            if (args.Property == CartesianChart.HoveredPointProperty) lastNotification = chart.HoveredPoint;
            if (args.Property != CartesianChart.AxisReadingsProperty || chart.AxisReadings.Count > 0 || changed) return;
            changed = true; series.ItemsSource = new ChartPoint[] { new(8, 80) };
        };
        chart.IsTooltipEnabled = false;
        Assert.True(changed); Assert.Null(lastNotification); Assert.Null(chart.HoveredPoint); Assert.Empty(chart.HoveredPoints);
    }

    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Shared_y_matches_an_independent_screen_space_full_scan_with_log_reverse_and_breaks(bool inverse, bool logarithmic)
    {
        var chart = CreateChart(); chart.TooltipMode = ChartTooltipMode.SharedY;
        var axis = logarithmic ? (ChartAxis)new LogarithmicAxis() : new NumericAxis();
        axis.Minimum = 1; axis.Maximum = 1000; axis.IsInversed = inverse;
        axis.ScaleBreaks.Add(new ChartScaleBreak { Start = 200, End = 300 }); chart.YAxis = axis;
        ChartPoint[] values = [new(1, 100), new(3, 1), new(4, 10), new(4, 10), new(12, 50),
            new(8, 1000), new(5, 250), new(double.NaN, 30), new(2, 30), new(9, double.NaN)];
        chart.Series.Add(new ScatterSeries { ItemsSource = values });
        using var host = new Host(chart);
        var scene = Scene(chart); var snapshots = (Array)scene.GetType().GetProperty("Snapshots")!.GetValue(scene)!;
        var context = (ChartSeriesRenderContext)snapshots.GetValue(0)!.GetType().GetProperty("Context")!.GetValue(snapshots.GetValue(0))!;
        foreach (var y in new[] { 1d, 10d, 30d, 50d, 100d, 1000d })
        {
            var position = context.Projection.Project(new ChartPoint(5, y));
            var expected = values.Select((point, ordinal) => (point, ordinal, screen: context.Projection.Project(point)))
                .Where(item => double.IsFinite(item.screen.X) && double.IsFinite(item.screen.Y) && context.PlotArea.Contains(item.screen))
                .OrderBy(item => Math.Abs(item.screen.Y - position.Y)).ThenBy(item => Math.Abs(item.screen.X - position.X))
                .ThenByDescending(item => item.ordinal).First();
            host.Move(position);
            var actual = Assert.Single(chart.HoveredPoints);
            Assert.Equal(expected.ordinal, actual.Index); Assert.Equal(expected.point, actual.Point);
        }
    }

    private static CartesianChart CreateChart() => new()
    {
        Width = 760, Height = 440, ShowLegend = false,
        XAxis = new NumericAxis { Minimum = 0, Maximum = 10 },
        YAxis = new NumericAxis { Minimum = 0, Maximum = 100 },
    };
    private static object Scene(CartesianChart chart) => typeof(CartesianChart).GetMethod("GetScene", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(chart, null)!;
    private static DrawingGroup DrawOverlay(CartesianChart chart)
    {
        var group = new DrawingGroup();
        using (var context = group.Open()) typeof(CartesianChart).GetMethod("RenderReadings", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(chart, [context, Scene(chart)]);
        return group;
    }
    private static IEnumerable<Drawing> Leaves(Drawing drawing)
    {
        if (drawing is DrawingGroup group) foreach (var child in group.Children) foreach (var leaf in Leaves(child)) yield return leaf;
        else yield return drawing;
    }
    private sealed record Row(string Id, double X, double Y);
    private sealed class CoupledProjection(IChartProjection inner) : IChartProjection
    {
        public Rect PlotArea => inner.PlotArea;
        public double YOrigin => inner.YOrigin;
        public Point Project(ChartPoint point) => new(inner.MapX(point.X), inner.MapY(point.Y + point.X * 5));
        public ChartPoint Unproject(Point position) => inner.Unproject(position);
        public double MapX(double value) => throw new InvalidOperationException("Coupled projection cannot use a separable X index.");
        public double MapY(double value) => throw new InvalidOperationException("Coupled projection cannot use a separable Y index.");
    }
    private sealed class Host : IDisposable
    {
        private readonly CartesianChart _chart;
        private readonly Window _window;
        public Host(CartesianChart chart)
        {
            _chart = chart; _window = new Window { Width = chart.Width, Height = chart.Height, Content = chart };
            _window.Show(); _window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
        }
        public Point Point(double x, double y) => new(_chart.PlotArea.Left + x / 10 * _chart.PlotArea.Width,
            _chart.PlotArea.Bottom - y / 100 * _chart.PlotArea.Height);
        public void Move(Point point) => _window.MouseMove(point);
        public void Dispose() => _window.Close();
    }
}
