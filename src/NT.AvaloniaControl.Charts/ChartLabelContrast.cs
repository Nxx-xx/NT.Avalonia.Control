using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>Contrast for text painted directly on a known opaque solid data fill.</summary>
internal static class ChartLabelContrast
{
    internal static IBrush OnFill(IBrush? fill, IBrush fallback)
    {
        // Gradients, alpha fills and composited overlaps need the complete painted
        // backdrop. Do not invent one from Foreground or change a caller's fallback.
        if (fill is not ISolidColorBrush { Color.A: 255, Opacity: 1 } solid) return fallback;
        static double Linear(byte value)
        {
            var channel = value / 255d;
            return channel <= .04045 ? channel / 12.92 : Math.Pow((channel + .055) / 1.055, 2.4);
        }
        var luminance = .2126 * Linear(solid.Color.R) + .7152 * Linear(solid.Color.G) + .0722 * Linear(solid.Color.B);
        return (luminance + .05) / .05 >= 1.05 / (luminance + .05) ? Brushes.Black : Brushes.White;
    }
}
