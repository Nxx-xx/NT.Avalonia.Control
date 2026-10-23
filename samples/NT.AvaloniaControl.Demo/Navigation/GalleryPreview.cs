using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace NT.AvaloniaControl.Demo.Navigation;

/// <summary>Lightweight thumbnails that do not run sample charts.</summary>
internal sealed class GalleryPreview : Control
{
    private readonly string _kind;

    internal GalleryPreview(string kind)
    {
        _kind = kind;
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var ink = this.TryFindResource("GalleryAccent", ActualThemeVariant, out var resource) && resource is IBrush brush
            ? brush : Brushes.SlateBlue;
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0 || height <= 0) return;
        var pen = new Pen(ink, 2.5);
        Point P(double x, double y) => new(x * width, y * height);
        void Line(double x1, double y1, double x2, double y2) => context.DrawLine(pen, P(x1, y1), P(x2, y2));
        void Box(double x, double y, double w, double h, bool fill = false) => context.DrawRectangle(fill ? ink : null, fill ? null : pen, new Rect(x * width, y * height, w * width, h * height), 3, 3);

        switch (_kind)
        {
            case "table":
                Box(.08, .08, .84, .84);
                for (var i = 1; i <= 3; i++) Line(.08, .08 + i * .21, .92, .08 + i * .21);
                Line(.5, .08, .5, .92); Line(.74, .08, .74, .92);
                Box(.13, .15, .22, .06, true); Box(.2, .38, .18, .05, true); Box(.2, .59, .12, .05, true);
                break;
            case "keys":
                for (var row = 0; row < 3; row++)
                    for (var col = 0; col < 7 - row; col++) Box(.08 + row * .035 + col * .115, .05 + row * .25, .09, .18);
                Box(.3, .8, .4, .15);
                break;
            case "doc":
            case "pdf":
                if (_kind == "pdf") Box(.28, .01, .44, .98);
                var left = _kind == "pdf" ? .34 : .12;
                var right = _kind == "pdf" ? .66 : .88;
                Box(left, .15, (right - left) * .6, .09, true);
                for (var i = 0; i < 4; i++) Line(left, .38 + i * .14, right - (i % 2) * .08, .38 + i * .14);
                break;
            case "bar":
                double[] heights = [.3, .65, .45, .9, .6, .8];
                for (var i = 0; i < heights.Length; i++)
                    using (context.PushOpacity(.4 + i * .1)) Box(.08 + i * .145, 1 - heights[i], .1, heights[i], true);
                break;
            case "ring":
                var radius = Math.Min(width, height) * .38;
                context.DrawEllipse(null, new Pen(ink, 12), P(.5, .5), radius, radius);
                break;
            case "scatter":
                for (var i = 0; i < 12; i++)
                    context.DrawEllipse(ink, null, P(.07 + i * .077, .8 - i * .05 + (i % 3 - 1) * .16), 3 + i % 3, 3 + i % 3);
                break;
            default:
                double[] values = [.78, .56, .66, .28, .41, .1, .22];
                for (var i = 1; i < values.Length; i++) Line(.04 + (i - 1) * .15, values[i - 1], .04 + i * .15, values[i]);
                using (context.PushOpacity(.3))
                    for (var i = 1; i < values.Length; i++) Line(.04 + (i - 1) * .15, values[i - 1] * .5 + .45, .04 + i * .15, values[i] * .5 + .45);
                break;
        }
    }
}
