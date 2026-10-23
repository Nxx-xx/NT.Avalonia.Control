using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.Charts;

/// <summary>Determines whether chart navigation or an enclosing scroll container takes priority.</summary>
public enum ChartInputRoutingMode
{
    /// <summary>The chart takes navigation input when it can change a visible range.</summary>
    Chart = 0,
    /// <summary>Normal scrolling in automatic wheel mode and single-finger touch prefer a scrollable ancestor.
    /// Modified zoom and two-finger chart gestures remain available.</summary>
    Auto = 1,
    /// <summary>Navigation input is left to enclosing controls.</summary>
    ScrollViewer = 2
}

public sealed partial class CartesianChart
{
    public static readonly StyledProperty<ChartInputRoutingMode> InputRoutingModeProperty =
        AvaloniaProperty.Register<CartesianChart, ChartInputRoutingMode>(nameof(InputRoutingMode), ChartInputRoutingMode.Auto,
            validate: value => Enum.IsDefined(value));

    /// <summary>Navigation priority inside a scroll container. Auto retains the default wheel-zoom behavior;
    /// use WheelMode=Auto to scroll the page without a zoom modifier.</summary>
    public ChartInputRoutingMode InputRoutingMode { get => GetValue(InputRoutingModeProperty); set => SetValue(InputRoutingModeProperty, value); }

    // A packet is routed as a whole. Splitting one diagonal gesture between a chart and a parent
    // would move both destinations and produce unstable ownership at the next input sample.
    // contentDelta is a screen/content translation: a positive component decreases that offset.
    private bool CanInputScrollAncestorMove(Vector contentDelta)
    {
        if (!double.IsFinite(contentDelta.X) || !double.IsFinite(contentDelta.Y)) return false;
        foreach (var scroll in this.GetVisualAncestors().OfType<ScrollViewer>())
        {
            if (!scroll.IsEffectivelyEnabled || !scroll.IsVisible) continue;
            if (scroll.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled &&
                CanScrollInputAxis(scroll.Offset.X, scroll.Extent.Width - scroll.Viewport.Width, contentDelta.X)) return true;
            if (scroll.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled &&
                CanScrollInputAxis(scroll.Offset.Y, scroll.Extent.Height - scroll.Viewport.Height, contentDelta.Y)) return true;
        }
        return false;
    }

    private static bool CanScrollInputAxis(double offset, double maximum, double delta) =>
        double.IsFinite(offset) && double.IsFinite(maximum) && maximum > 0 &&
        (delta > 0 ? offset > 0 : delta < 0 && offset < maximum);
}
