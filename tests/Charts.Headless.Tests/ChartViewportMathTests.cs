using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class ChartViewportMathTests
{
    private static readonly ChartViewport Extent = new(0, 100, 0, 200);

    [Fact]
    public void Viewport_requires_finite_ordered_bounds_but_allows_an_overflowing_span()
    {
        Assert.True(new ChartViewport(-double.MaxValue, double.MaxValue, 0, 1).IsValid);
        Assert.False(new ChartViewport(1, 1, 0, 1).IsValid);
        Assert.False(new ChartViewport(2, 1, 0, 1).IsValid);
        Assert.False(new ChartViewport(0, 1, double.NaN, 1).IsValid);
        Assert.False(new ChartViewport(0, double.PositiveInfinity, 0, 1).IsValid);
    }

    [Fact]
    public void Zoom_preserves_the_data_position_beneath_an_asymmetric_anchor()
    {
        var zoomed = ChartViewportMath.Zoom(Extent, Extent, 0.2, 0.75, 0.5, ChartInteractionAxes.Both);

        Assert.Equal(10, zoomed.XMinimum, 10);
        Assert.Equal(60, zoomed.XMaximum, 10);
        Assert.Equal(75, zoomed.YMinimum, 10);
        Assert.Equal(175, zoomed.YMaximum, 10);
        Assert.Equal(20, zoomed.XMinimum + (zoomed.XMaximum - zoomed.XMinimum) * 0.2, 10);
        Assert.Equal(150, zoomed.YMinimum + (zoomed.YMaximum - zoomed.YMinimum) * 0.75, 10);
    }

    [Fact]
    public void A_disabled_axis_is_preserved_exactly()
    {
        var current = new ChartViewport(10, 90, 33.3, 125.7);

        var zoomed = ChartViewportMath.Zoom(current, Extent, 0.5, 0.5, 0.5, ChartInteractionAxes.X);
        var panned = ChartViewportMath.Pan(current, Extent, 0.25, 0.75, ChartInteractionAxes.X);

        Assert.Equal(current.YMinimum, zoomed.YMinimum);
        Assert.Equal(current.YMaximum, zoomed.YMaximum);
        Assert.Equal(current.YMinimum, panned.YMinimum);
        Assert.Equal(current.YMaximum, panned.YMaximum);
        Assert.Equal(current, ChartViewportMath.Zoom(current, Extent, 0.5, 0.5, 0.5, ChartInteractionAxes.None));
        Assert.Equal(current, ChartViewportMath.Pan(current, Extent, 1, 1, ChartInteractionAxes.None));
    }

    [Fact]
    public void Pan_moves_by_a_fraction_of_each_current_span_in_data_coordinate_direction()
    {
        var current = new ChartViewport(20, 40, 60, 100);

        var panned = ChartViewportMath.Pan(current, Extent, 0.5, -0.25, ChartInteractionAxes.Both);

        Assert.Equal(30, panned.XMinimum, 10);
        Assert.Equal(50, panned.XMaximum, 10);
        Assert.Equal(50, panned.YMinimum, 10);
        Assert.Equal(90, panned.YMaximum, 10);
    }

    [Fact]
    public void Pan_stops_at_the_extent_without_shrinking_the_visible_window()
    {
        var current = new ChartViewport(20, 40, 60, 100);

        var panned = ChartViewportMath.Pan(current, Extent, double.MaxValue, -double.MaxValue, ChartInteractionAxes.Both);

        Assert.Equal(80, panned.XMinimum, 10);
        Assert.Equal(100, panned.XMaximum, 10);
        Assert.Equal(0, panned.YMinimum, 10);
        Assert.Equal(40, panned.YMaximum, 10);
        AssertWithinExtent(panned, Extent);
    }

    [Fact]
    public void Zooming_out_beyond_the_data_restores_the_complete_extent()
    {
        var current = new ChartViewport(20, 40, 60, 100);

        var zoomed = ChartViewportMath.Zoom(current, Extent, 0.3, 0.8, double.MaxValue, ChartInteractionAxes.Both);

        Assert.Equal(Extent, zoomed);
    }

    [Fact]
    public void Repeated_zoom_has_a_finite_minimum_span_and_remains_pannable()
    {
        var current = Extent;
        for (var index = 0; index < 100; index++)
            current = ChartViewportMath.Zoom(current, Extent, 0.25, 0.75, 0.1, ChartInteractionAxes.Both);

        Assert.InRange(current.XMaximum - current.XMinimum, 0.000099999, 0.000100001);
        Assert.InRange(current.YMaximum - current.YMinimum, 0.000199999, 0.000200001);
        AssertWithinExtent(current, Extent);
        var panned = ChartViewportMath.Pan(current, Extent, 1, 1, ChartInteractionAxes.Both);
        Assert.True(panned.XMinimum > current.XMinimum);
        Assert.True(panned.YMinimum > current.YMinimum);
        AssertWithinExtent(panned, Extent);
    }

    [Fact]
    public void Overflowing_extents_and_tiny_extents_keep_their_correct_zoom_center()
    {
        var extent = new ChartViewport(-double.MaxValue, double.MaxValue, 1e-300, 2e-300);

        var zoomed = ChartViewportMath.Zoom(extent, extent, 0.5, 0.5, 0.5, ChartInteractionAxes.Both);

        AssertWithinExtent(zoomed, extent);
        Assert.InRange(zoomed.XMinimum / double.MaxValue, -0.500000000001, -0.499999999999);
        Assert.InRange(zoomed.XMaximum / double.MaxValue, 0.499999999999, 0.500000000001);
        Assert.InRange(zoomed.YMinimum / 1e-300, 1.24999999999, 1.25000000001);
        Assert.InRange(zoomed.YMaximum / 1e-300, 1.74999999999, 1.75000000001);
        AssertWithinExtent(ChartViewportMath.Pan(zoomed, extent, 0.1, -0.2, ChartInteractionAxes.Both), extent);
    }

    [Fact]
    public void An_extent_of_adjacent_doubles_does_not_collapse_when_zoomed()
    {
        var extent = new ChartViewport(1e300, Math.BitIncrement(1e300), 0, double.Epsilon * 8);

        var current = extent;
        for (var index = 0; index < 30; index++)
        {
            current = ChartViewportMath.Zoom(current, extent, 0.5, 0.5, 0.01, ChartInteractionAxes.Both);
            current = ChartViewportMath.Pan(current, extent, 0.1, -0.2, ChartInteractionAxes.Both);
            AssertWithinExtent(current, extent);
        }

        Assert.Equal(extent.XMinimum, current.XMinimum);
        Assert.Equal(extent.XMaximum, current.XMaximum);
    }

    [Fact]
    public void Invalid_inputs_leave_a_valid_view_or_fall_back_to_the_extent()
    {
        var current = new ChartViewport(20, 40, 60, 100);

        Assert.Equal(current, ChartViewportMath.Zoom(current, Extent, 0.5, 0.5, double.NaN, ChartInteractionAxes.Both));
        Assert.Equal(current, ChartViewportMath.Zoom(current, Extent, double.NaN, 0.5, 0.5, ChartInteractionAxes.Both));
        Assert.Equal(current, ChartViewportMath.Zoom(current, Extent, 0.5, 0.5, 0, ChartInteractionAxes.Both));
        Assert.Equal(current, ChartViewportMath.Pan(current, Extent, double.PositiveInfinity, 0, ChartInteractionAxes.Both));
        Assert.Equal(current, ChartViewportMath.Pan(current, default, 1, 1, ChartInteractionAxes.Both));
        Assert.Equal(Extent, ChartViewportMath.Zoom(default, Extent, 0.5, 0.5, 1, ChartInteractionAxes.Both));
        Assert.Equal(Extent, ChartViewportMath.Zoom(default, Extent, 0.5, 0.5, 0.5, ChartInteractionAxes.Both));
        Assert.Equal(Extent, ChartViewportMath.Pan(default, Extent, 1, 1, ChartInteractionAxes.Both));
        AssertWithinExtent(ChartViewportMath.Pan(new ChartViewport(-20, 10, 190, 220), Extent,
            0, 0, ChartInteractionAxes.Both), Extent);
    }

    private static void AssertWithinExtent(ChartViewport viewport, ChartViewport extent)
    {
        Assert.True(viewport.IsValid);
        Assert.InRange(viewport.XMinimum, extent.XMinimum, extent.XMaximum);
        Assert.InRange(viewport.XMaximum, extent.XMinimum, extent.XMaximum);
        Assert.InRange(viewport.YMinimum, extent.YMinimum, extent.YMaximum);
        Assert.InRange(viewport.YMaximum, extent.YMinimum, extent.YMaximum);
    }
}
