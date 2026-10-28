using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.Charts;

public partial class ShapeMap
{
    // Inspect the ancestor chain once per paint, not once per label. Solid alpha
    // backgrounds compose from the outermost container through the chart itself.
    // An unknown painted background retains the foreground-derived canvas fallback.
    private (double R, double G, double B) LabelCanvasColor()
    {
        var lightForeground = Foreground is ISolidColorBrush foreground &&
            .2126 * foreground.Color.R + .7152 * foreground.Color.G + .0722 * foreground.Color.B > 128;
        var canvas = lightForeground ? (17 / 255d, 23 / 255d, 34 / 255d) : (1d, 1d, 1d);
        foreach (var ancestor in this.GetVisualAncestors().Reverse())
        {
            var background = ancestor switch
            {
                Border border => border.Background,
                Panel panel => panel.Background,
                TemplatedControl control => control.Background,
                _ => null,
            };
            if (background is ISolidColorBrush solid) canvas = CompositeLabelColor(canvas, solid);
        }
        return Background is ISolidColorBrush own ? CompositeLabelColor(canvas, own) : canvas;
    }

    private static (double R, double G, double B) CompositeLabelColor((double R, double G, double B) canvas, ISolidColorBrush fill)
    {
        var alpha = Math.Clamp(fill.Color.A / 255d * fill.Opacity, 0, 1);
        return (fill.Color.R / 255d * alpha + canvas.R * (1 - alpha),
            fill.Color.G / 255d * alpha + canvas.G * (1 - alpha),
            fill.Color.B / 255d * alpha + canvas.B * (1 - alpha));
    }

    private void DrawMapLabel(DrawingContext context, string text, Point center, double maxWidth, IBrush? fill, (double R, double G, double B) canvas)
    {
        if (string.IsNullOrEmpty(text) || maxWidth <= 0 || Bounds.Width <= 0 || Bounds.Height <= 0) return;
        var brush = Foreground ?? Brushes.Gray;
        if (fill is null || fill is ISolidColorBrush)
        {
            var color = fill is ISolidColorBrush solid ? CompositeLabelColor(canvas, solid) : canvas;
            static double Linear(double channel) => channel <= .04045 ? channel / 12.92 : Math.Pow((channel + .055) / 1.055, 2.4);
            var luminance = .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
            brush = (luminance + .05) / .05 >= 1.05 / (luminance + .05) ? Brushes.Black : Brushes.White;
        }
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily, FontStyle, FontWeight), FontSize, brush)
        { MaxTextWidth = Math.Max(.01, maxWidth), MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis };
        context.DrawText(formatted, new(Math.Clamp(center.X - formatted.Width / 2, 0, Math.Max(0, Bounds.Width - formatted.Width)),
            Math.Clamp(center.Y - formatted.Height / 2, 0, Math.Max(0, Bounds.Height - formatted.Height))));
    }
}
