using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

internal static class ChartDataTrackballContrast
{
    // Native glyphs can contain both the theme foreground and background colors.
    // Outline automatic strokes with the opposite tone so the reading stays visible
    // across those local fills. Explicit appearance brushes remain caller controlled.
    internal static IBrush Halo(IBrush stroke) => stroke is ISolidColorBrush solid &&
        .2126 * solid.Color.R + .7152 * solid.Color.G + .0722 * solid.Color.B >= 128
            ? Brushes.Black : Brushes.White;
}
