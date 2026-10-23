using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.VirtualKeyboard;

/// <summary>Arranges a row by its immutable key width weights, including ItemsControl content containers.</summary>
public class VirtualKeyboardRowPanel : Panel
{
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<VirtualKeyboardRowPanel, double>(nameof(Spacing), validate: value => double.IsFinite(value) && value >= 0);
    static VirtualKeyboardRowPanel() => AffectsMeasure<VirtualKeyboardRowPanel>(SpacingProperty);
    public double Spacing { get => GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }
    protected override Size MeasureOverride(Size availableSize)
    {
        var unit = 0d; var weight = 0d; var height = 0d;
        foreach (var child in Children)
        {
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            var width = Weight(child); weight += width;
            unit = Math.Max(unit, child.DesiredSize.Width / width);
            height = Math.Max(height, child.DesiredSize.Height);
        }
        return new Size(unit * weight + Math.Max(0, Children.Count - 1) * Spacing, height);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        var weight = Children.Sum(Weight);
        var unit = weight == 0 ? 0 : Math.Max(0, finalSize.Width - Math.Max(0, Children.Count - 1) * Spacing) / weight;
        var minimum = Children.Count == 0 ? 0 : Children.Max(child => child.DesiredSize.Width / Weight(child));
        unit = Math.Max(unit, minimum);
        var x = 0d;
        foreach (var child in Children)
        {
            var width = unit * Weight(child);
            child.Arrange(new Rect(x, 0, width, finalSize.Height));
            x += width + Spacing;
        }
        return finalSize;
    }
    private static double Weight(Control child) => child.DataContext is VirtualKeyboardKey model ? model.Width :
        child is VirtualKeyboardKeyButton { Key: { } key } ? key.Width :
        child.GetVisualDescendants().OfType<VirtualKeyboardKeyButton>().FirstOrDefault()?.Key?.Width ?? 1;
}
