using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class GaugeExtremeAngleTests
{
    [AvaloniaTheory]
    [InlineData(1e20, 360)] [InlineData(-1e20, -360)] [InlineData(double.MaxValue, 360)]
    [InlineData(-double.MaxValue, 270)] [InlineData(1e20, 90)] [InlineData(double.MaxValue, -270)]
    public void Production_annular_path_commands_match_the_equivalent_modulo_angle_before_sweep_addition(double start, double sweep)
    {
        var actual = new Sink(); var expected = new Sink();
        GaugeGeometry.WriteAnnulus(actual, new Point(10, 20), 100, 80, start, sweep);
        GaugeGeometry.WriteAnnulus(expected, new Point(10, 20), 100, 80, start % 360, sweep);
        Assert.Equal(expected.Begins.Count, actual.Begins.Count); Assert.Equal(expected.Arcs.Count, actual.Arcs.Count); Assert.Equal(expected.Lines.Count, actual.Lines.Count); Assert.Equal(expected.Closed, actual.Closed);
        for (var index = 0; index < actual.Begins.Count; index++) Near(expected.Begins[index], actual.Begins[index]);
        for (var index = 0; index < actual.Lines.Count; index++) Near(expected.Lines[index], actual.Lines[index]);
        for (var index = 0; index < actual.Arcs.Count; index++)
        {
            Near(expected.Arcs[index].End, actual.Arcs[index].End); Assert.Equal(expected.Arcs[index].Radii, actual.Arcs[index].Radii);
            Assert.Equal(expected.Arcs[index].Large, actual.Arcs[index].Large); Assert.Equal(expected.Arcs[index].Direction, actual.Arcs[index].Direction);
        }
        if (Math.Abs(sweep) == 360)
        {
            var vector = actual.Arcs[0].End - actual.Begins[0];
            Assert.Equal(200, Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y), 9);
        }
    }

    [AvaloniaTheory]
    [InlineData(0, 1e20)] [InlineData(0, double.MaxValue)] [InlineData(1, 1e20)] [InlineData(1, double.MaxValue)]
    [InlineData(2, 1e20)] [InlineData(2, double.MaxValue)] [InlineData(3, 1e20)] [InlineData(3, double.MaxValue)]
    public void Actual_needle_target_and_tick_drawings_match_the_equivalent_normalized_start_without_rewriting_public_input(int family, double start)
    {
        var huge = Create(family, start); var normalized = Create(family, start % 360);
        using var hugeHost = new Host(huge); using var normalizedHost = new Host(normalized);
        var actual = Lines(huge); var expected = Lines(normalized); Assert.Equal(family switch { 1 => 6, 3 => 2, _ => 1 }, actual.Length); Assert.Equal(expected.Length, actual.Length);
        for (var index = 0; index < actual.Length; index++)
        {
            Assert.Equal(expected[index].Color, actual[index].Color); Assert.Equal(expected[index].Thickness, actual[index].Thickness);
            RectNear(expected[index].Bounds, actual[index].Bounds);
        }
        Assert.Equal(start, huge switch { CircularGaugeChart c => c.StartAngle, ProgressDonutChart p => p.StartAngle, GradientRingChart r => r.StartAngle, _ => 0 });
    }

    [AvaloniaFact]
    public void An_animated_huge_wave_phase_reduces_before_adding_elapsed_time()
    {
        var chart = new LiquidFillGaugeChart { Width = 420, Height = 280, Value = 50, IsWaveAnimationEnabled = true };
        using var host = new Host(chart); chart.WavePhase = 1e20;
        var expected = (1e20 % (Math.PI * 2) + Math.PI / 2) % (Math.PI * 2);
        chart.AdvanceWave(TimeSpan.FromSeconds(.5)); chart.IsPaused = true;
        Assert.Equal(expected, chart.WavePhase, 9); Assert.False(chart.IsWaveTimerRunning);
    }

    private static GaugeBase Create(int family, double start)
    {
        GaugeBase chart = family switch
        {
            0 => new CircularGaugeChart { StartAngle = start, SweepAngle = 360, ShowNeedle = true, ShowMajorTicks = false },
            1 => new CircularGaugeChart { StartAngle = start, SweepAngle = 360, ShowNeedle = false, ShowMajorTicks = true, TargetValue = 75 },
            2 => new ProgressDonutChart { StartAngle = start, TargetValue = 75, ShowBounds = false },
            _ => new GradientRingChart { StartAngle = start, TargetValue = 75, ShowLabels = false }
        };
        chart.Width = 420; chart.Height = 280; chart.Padding = new Thickness(0); chart.Value = 25; chart.ShowValue = chart.ShowScale = false;
        chart.NeedleBrush = Brushes.Magenta; chart.AxisBrush = Brushes.Lime; chart.ValueBrush = chart.TrackBrush = Brushes.Transparent;
        if (chart is GradientRingChart rings) { rings.Values.Add(new GaugeValue { Value = 25 }); rings.Values.Add(new GaugeValue { Value = 75 }); }
        return chart;
    }
    private static (Color Color, Rect Bounds, double Thickness)[] Lines(GaugeBase chart)
    {
        var group = new DrawingGroup(); using (var context = group.Open()) chart.Render(context);
        return Leaves(group).OfType<GeometryDrawing>().Where(g => g.Pen?.Brush is ISolidColorBrush b && (b.Color == Colors.Magenta || b.Color == Colors.Lime))
            .Select(g => (((ISolidColorBrush)g.Pen!.Brush!).Color, g.Geometry!.Bounds, g.Pen.Thickness)).ToArray();
    }
    private static IEnumerable<Drawing> Leaves(Drawing drawing)
    {
        if (drawing is DrawingGroup group) foreach (var child in group.Children) foreach (var leaf in Leaves(child)) yield return leaf;
        else yield return drawing;
    }
    private static void Near(Point expected, Point actual) { Assert.Equal(expected.X, actual.X, 9); Assert.Equal(expected.Y, actual.Y, 9); }
    private static void RectNear(Rect expected, Rect actual) { Assert.Equal(expected.X, actual.X, 9); Assert.Equal(expected.Y, actual.Y, 9); Assert.Equal(expected.Width, actual.Width, 9); Assert.Equal(expected.Height, actual.Height, 9); }
    private sealed class Host : IDisposable
    {
        private readonly Window _window;
        public Host(Control control) { _window = new Window { Width = 500, Height = 400, Content = control }; _window.Show(); _window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
        public void Dispose() { _window.Close(); Dispatcher.UIThread.RunJobs(); }
    }
    private sealed class Sink : ICircularGeometrySink
    {
        public List<Point> Begins { get; } = []; public List<Point> Lines { get; } = []; public List<bool> Closed { get; } = [];
        public List<(Point End, Size Radii, bool Large, SweepDirection Direction)> Arcs { get; } = [];
        public void SetFillRule(FillRule rule) => Assert.Equal(FillRule.NonZero, rule);
        public void BeginFigure(Point start, bool isFilled) { Assert.True(isFilled); Begins.Add(start); }
        public void ArcTo(Point end, Size radii, double rotation, bool largeArc, SweepDirection direction) => Arcs.Add((end, radii, largeArc, direction));
        public void LineTo(Point end) => Lines.Add(end);
        public void EndFigure(bool close) => Closed.Add(close);
    }
}
