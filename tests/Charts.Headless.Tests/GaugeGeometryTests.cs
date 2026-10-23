using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class GaugeGeometryTests
{
    [AvaloniaTheory]
    [InlineData(90, SweepDirection.Clockwise)] [InlineData(-90, SweepDirection.CounterClockwise)]
    public void Quarter_ring_uses_two_opposing_exact_arcs_with_a_real_hole(double sweep, SweepDirection direction)
    {
        var sink = new Sink(); GaugeGeometry.WriteAnnulus(sink, new Point(10, 20), 100, 80, -90, sweep);
        Assert.Equal(FillRule.NonZero, sink.Rule); Assert.Single(sink.Begins); Near(new Point(10, -80), sink.Begins[0]);
        Assert.Equal(2, sink.Arcs.Count); Assert.Equal(direction, sink.Arcs[0].Direction); Assert.Equal(direction == SweepDirection.Clockwise ? SweepDirection.CounterClockwise : SweepDirection.Clockwise, sink.Arcs[1].Direction);
        Near(new Point(sweep > 0 ? 110 : -90, 20), sink.Arcs[0].End); Near(new Point(10, -60), sink.Arcs[1].End);
        Assert.Equal(new Size(100, 100), sink.Arcs[0].Radii); Assert.Equal(new Size(80, 80), sink.Arcs[1].Radii); Assert.All(sink.Closed, closed => Assert.True(closed));
    }

    [AvaloniaTheory]
    [InlineData(360)] [InlineData(-360)]
    public void Full_ring_splits_each_circle_into_two_half_arcs_and_reverses_inner_winding(double sweep)
    {
        var sink = new Sink(); GaugeGeometry.WriteAnnulus(sink, default, 100, 80, 0, sweep);
        Assert.Equal(2, sink.Begins.Count); Assert.Equal(4, sink.Arcs.Count); Assert.Equal(2, sink.Closed.Count); Assert.Empty(sink.Lines);
        Near(new Point(-100, 0), sink.Arcs[0].End); Near(new Point(100, 0), sink.Arcs[1].End); Near(new Point(-80, 0), sink.Arcs[2].End); Near(new Point(80, 0), sink.Arcs[3].End);
        Assert.NotEqual(sink.Arcs[0].Direction, sink.Arcs[2].Direction); Assert.All(sink.Arcs, arc => Assert.False(arc.Large));
    }

    [AvaloniaFact]
    public void Large_partial_arc_preserves_native_large_arc_flag_and_connects_inner_endpoint()
    {
        var sink = new Sink(); GaugeGeometry.WriteAnnulus(sink, default, 10, 5, 0, 270);
        Assert.All(sink.Arcs, arc => Assert.True(arc.Large)); Near(new Point(0, -5), Assert.Single(sink.Lines));
        Assert.Single(sink.Begins); Assert.Single(sink.Closed);
    }

    [AvaloniaTheory]
    [InlineData(100, 100, 90)] [InlineData(0, 0, 90)] [InlineData(100, 80, 0)] [InlineData(double.NaN, 20, 90)]
    public void Empty_or_unrepresentable_bands_emit_no_fill_figure(double outer, double inner, double sweep)
    {
        var sink = new Sink(); GaugeGeometry.WriteAnnulus(sink, default, outer, inner, 0, sweep); Assert.Empty(sink.Begins); Assert.Empty(sink.Arcs);
    }

    private static void Near(Point expected, Point actual) { Assert.Equal(expected.X, actual.X, 9); Assert.Equal(expected.Y, actual.Y, 9); }
    private sealed class Sink : ICircularGeometrySink
    {
        public FillRule Rule; public List<Point> Begins { get; } = []; public List<Point> Lines { get; } = []; public List<bool> Closed { get; } = [];
        public List<(Point End, Size Radii, bool Large, SweepDirection Direction)> Arcs { get; } = [];
        public void SetFillRule(FillRule rule) => Rule = rule;
        public void BeginFigure(Point start, bool isFilled) { Assert.True(isFilled); Begins.Add(start); }
        public void ArcTo(Point end, Size radii, double rotation, bool largeArc, SweepDirection direction) => Arcs.Add((end, radii, largeArc, direction));
        public void LineTo(Point end) => Lines.Add(end);
        public void EndFigure(bool close) => Closed.Add(close);
    }
}
