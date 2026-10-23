using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>The original endpoint or endpoints carrying a solid arrowhead.</summary>
[Flags]
public enum ChartArrowHeads { None = 0, Start = 1, End = 2, Both = Start | End }

/// <summary>
/// A label box aligned inside the projected shape bounds. Start and End instead center
/// the label on the original endpoints, preserving their order on inverted axes.
/// CenterRadius ellipses use their visual top-left and bottom-right corners for Start and End.
/// </summary>
public enum ChartShapeLabelPosition { Center, TopLeft, Top, TopRight, Left, Right, BottomLeft, Bottom, BottomRight, Start, End }

/// <summary>Endpoint bounds preserve the original contract; CenterRadius projects the center plus and minus each radius.</summary>
public enum ChartEllipseGeometryMode { EndpointBounds, CenterRadius }

/// <summary>Two data-domain endpoints, or two plot-relative DIP endpoints in Pixel mode.</summary>
public abstract class ChartShapeAnnotation : ChartAnnotation
{
    public static readonly StyledProperty<double> X1Property = AvaloniaProperty.Register<ChartShapeAnnotation, double>(nameof(X1));
    public static readonly StyledProperty<double> Y1Property = AvaloniaProperty.Register<ChartShapeAnnotation, double>(nameof(Y1));
    public static readonly StyledProperty<double> X2Property = AvaloniaProperty.Register<ChartShapeAnnotation, double>(nameof(X2), 1);
    public static readonly StyledProperty<double> Y2Property = AvaloniaProperty.Register<ChartShapeAnnotation, double>(nameof(Y2), 1);
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<ChartShapeAnnotation, string?>(nameof(Label));
    public static readonly StyledProperty<ChartShapeLabelPosition> LabelPositionProperty =
        AvaloniaProperty.Register<ChartShapeAnnotation, ChartShapeLabelPosition>(nameof(LabelPosition), ChartShapeLabelPosition.Center, validate: Enum.IsDefined);
    public static readonly StyledProperty<Vector> LabelOffsetProperty = AvaloniaProperty.Register<ChartShapeAnnotation, Vector>(nameof(LabelOffset),
        validate: value => double.IsFinite(value.X) && double.IsFinite(value.Y));
    public double X1 { get => GetValue(X1Property); set => SetValue(X1Property, value); }
    public double Y1 { get => GetValue(Y1Property); set => SetValue(Y1Property, value); }
    public double X2 { get => GetValue(X2Property); set => SetValue(X2Property, value); }
    public double Y2 { get => GetValue(Y2Property); set => SetValue(Y2Property, value); }
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public ChartShapeLabelPosition LabelPosition { get => GetValue(LabelPositionProperty); set => SetValue(LabelPositionProperty, value); }
    /// <summary>A screen-space translation in DIPs, independent of data axes and zoom.</summary>
    public Vector LabelOffset { get => GetValue(LabelOffsetProperty); set => SetValue(LabelOffsetProperty, value); }

    protected bool TryGetEndpoints(ChartAnnotationRenderContext context, out Point first, out Point last)
    {
        first = last = default;
        if (!double.IsFinite(X1) || !double.IsFinite(Y1) || !double.IsFinite(X2) || !double.IsFinite(Y2)) return false;
        first = context.Project(X1, Y1); last = context.Project(X2, Y2);
        return Finite(first) && Finite(last);
    }

    protected bool TryGetBounds(ChartAnnotationRenderContext context, out Rect bounds)
    {
        bounds = default;
        if (!TryGetEndpoints(context, out var first, out var last)) return false;
        return TryGetBounds(first, last, out bounds);
    }

    protected static bool TryGetBounds(Point first, Point last, out Rect bounds)
    {
        bounds = default;
        if (!Finite(first) || !Finite(last)) return false;
        var left = Math.Min(first.X, last.X); var top = Math.Min(first.Y, last.Y);
        var width = Math.Max(first.X, last.X) - left; var height = Math.Max(first.Y, last.Y) - top;
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0) return false;
        bounds = new Rect(left, top, width, height);
        return double.IsFinite(bounds.Right) && double.IsFinite(bounds.Bottom);
    }

    protected void DrawShapeLabel(DrawingContext drawing, ChartAnnotationRenderContext context, Rect bounds, Point first, Point last)
    {
        if (string.IsNullOrEmpty(Label)) return;
        var formatted = CreateFormattedText(context, Label);
        var width = formatted.WidthIncludingTrailingWhitespace; var height = formatted.Height;
        if (!double.IsFinite(width) || !double.IsFinite(height)) return;
        var x = LabelPosition switch
        {
            ChartShapeLabelPosition.TopLeft or ChartShapeLabelPosition.Left or ChartShapeLabelPosition.BottomLeft => bounds.Left,
            ChartShapeLabelPosition.TopRight or ChartShapeLabelPosition.Right or ChartShapeLabelPosition.BottomRight => bounds.Right - width,
            ChartShapeLabelPosition.Start => first.X - width / 2,
            ChartShapeLabelPosition.End => last.X - width / 2,
            _ => bounds.Left / 2 + bounds.Right / 2 - width / 2,
        };
        var y = LabelPosition switch
        {
            ChartShapeLabelPosition.TopLeft or ChartShapeLabelPosition.Top or ChartShapeLabelPosition.TopRight => bounds.Top,
            ChartShapeLabelPosition.BottomLeft or ChartShapeLabelPosition.Bottom or ChartShapeLabelPosition.BottomRight => bounds.Bottom - height,
            ChartShapeLabelPosition.Start => first.Y - height / 2,
            ChartShapeLabelPosition.End => last.Y - height / 2,
            _ => bounds.Top / 2 + bounds.Bottom / 2 - height / 2,
        };
        DrawText(drawing, context, Label, new Point(x, y) + LabelOffset);
    }
}

/// <summary>A rectangle bounded by the projected endpoints. Specify Fill and/or Stroke to draw it.</summary>
public sealed class RectangleAnnotation : ChartShapeAnnotation
{
    public static readonly StyledProperty<double> CornerRadiusProperty = AvaloniaProperty.Register<RectangleAnnotation, double>(nameof(CornerRadius),
        validate: value => double.IsFinite(value) && value >= 0);
    /// <summary>A circular corner radius in DIPs, capped to half the shorter projected side while rendering.</summary>
    public double CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    public override void Render(DrawingContext drawing, ChartAnnotationRenderContext context)
    {
        if (!TryGetEndpoints(context, out var first, out var last) || !TryGetBounds(first, last, out var bounds)) return;
        if (Fill is not null || Stroke is not null)
        {
            var radius = Math.Min(CornerRadius, Math.Min(bounds.Width, bounds.Height) / 2);
            drawing.DrawRectangle(Fill, Stroke is null ? null : MakePen(context), bounds, radius, radius);
        }
        DrawShapeLabel(drawing, context, bounds, first, last);
    }
}

/// <summary>
/// A screen-space ellipse inscribed in the projected endpoint bounds. Its screen radii are
/// half the projected width/height; on nonlinear axes it is not a data-space circle.
/// CenterRadius mode instead constructs those bounds by projecting (X-RadiusX,Y-RadiusY)
/// and (X+RadiusX,Y+RadiusY). Thus nonlinear projection can place the visual center away
/// from Project(X,Y); the outline remains a screen-space ellipse, not a sampled data circle.
/// Specify Fill and/or Stroke to draw it. Degenerate or nonfinite bounds are skipped.
/// </summary>
public sealed class EllipseAnnotation : ChartShapeAnnotation
{
    public static readonly StyledProperty<ChartEllipseGeometryMode> GeometryModeProperty =
        AvaloniaProperty.Register<EllipseAnnotation, ChartEllipseGeometryMode>(nameof(GeometryMode), validate: Enum.IsDefined);
    public static readonly StyledProperty<double> XProperty = AvaloniaProperty.Register<EllipseAnnotation, double>(nameof(X));
    public static readonly StyledProperty<double> YProperty = AvaloniaProperty.Register<EllipseAnnotation, double>(nameof(Y));
    public static readonly StyledProperty<double> RadiusXProperty = AvaloniaProperty.Register<EllipseAnnotation, double>(nameof(RadiusX), 1,
        validate: value => double.IsFinite(value) && value >= 0);
    public static readonly StyledProperty<double> RadiusYProperty = AvaloniaProperty.Register<EllipseAnnotation, double>(nameof(RadiusY), 1,
        validate: value => double.IsFinite(value) && value >= 0);
    public ChartEllipseGeometryMode GeometryMode { get => GetValue(GeometryModeProperty); set => SetValue(GeometryModeProperty, value); }
    /// <summary>The center X coordinate used only by CenterRadius mode.</summary>
    public double X { get => GetValue(XProperty); set => SetValue(XProperty, value); }
    /// <summary>The center Y coordinate used only by CenterRadius mode.</summary>
    public double Y { get => GetValue(YProperty); set => SetValue(YProperty, value); }
    /// <summary>The nonnegative radius in horizontal axis units, or plot-relative DIPs in Pixel mode.</summary>
    public double RadiusX { get => GetValue(RadiusXProperty); set => SetValue(RadiusXProperty, value); }
    /// <summary>The nonnegative radius in vertical axis units, or plot-relative DIPs in Pixel mode.</summary>
    public double RadiusY { get => GetValue(RadiusYProperty); set => SetValue(RadiusYProperty, value); }

    public override void Render(DrawingContext drawing, ChartAnnotationRenderContext context)
    {
        Point first, last;
        if (GeometryMode == ChartEllipseGeometryMode.CenterRadius)
        {
            var x1 = X - RadiusX; var x2 = X + RadiusX; var y1 = Y - RadiusY; var y2 = Y + RadiusY;
            if (!double.IsFinite(x1) || !double.IsFinite(x2) || !double.IsFinite(y1) || !double.IsFinite(y2)) return;
            first = context.Project(x1, y1); last = context.Project(x2, y2);
        }
        else if (!TryGetEndpoints(context, out first, out last)) return;
        if (!TryGetBounds(first, last, out var bounds) || !TryGetEllipse(bounds, out var center, out var radii)) return;
        if (Fill is not null || Stroke is not null)
            drawing.DrawEllipse(Fill, Stroke is null ? null : MakePen(context), center, radii.X, radii.Y);
        // CenterRadius labels use the visual bounds for Start/End, rather than a
        // nonlinear center projection which need not bisect those bounds.
        DrawShapeLabel(drawing, context, bounds,
            GeometryMode == ChartEllipseGeometryMode.CenterRadius ? bounds.TopLeft : first,
            GeometryMode == ChartEllipseGeometryMode.CenterRadius ? bounds.BottomRight : last);
    }

    internal static bool TryGetEllipse(Rect bounds, out Point center, out Vector radii)
    {
        // Avoid adding two huge finite coordinates when finding the midpoint.
        center = new Point(bounds.Left / 2 + bounds.Right / 2, bounds.Top / 2 + bounds.Bottom / 2);
        radii = new Vector(bounds.Width / 2, bounds.Height / 2);
        return Finite(center) && double.IsFinite(radii.X) && double.IsFinite(radii.Y) && radii.X > 0 && radii.Y > 0 &&
            double.IsFinite(center.X - radii.X) && double.IsFinite(center.X + radii.X) && double.IsFinite(center.Y - radii.Y) && double.IsFinite(center.Y + radii.Y);
    }
}

/// <summary>
/// A projected straight stem with heads at its original endpoints. Head sizes are DIPs,
/// remain fixed while zooming, and must be between 0 and 10,000. DashStyle affects only
/// the stem; head outlines are solid. Default chart clipping also clips the heads and gaps.
/// </summary>
public sealed class ArrowAnnotation : ChartShapeAnnotation
{
    public static readonly StyledProperty<ChartArrowHeads> ArrowHeadsProperty = AvaloniaProperty.Register<ArrowAnnotation, ChartArrowHeads>(nameof(ArrowHeads), ChartArrowHeads.End, validate: Enum.IsDefined);
    public static readonly StyledProperty<double> HeadLengthProperty = AvaloniaProperty.Register<ArrowAnnotation, double>(nameof(HeadLength), 10,
        validate: value => double.IsFinite(value) && value >= 0 && value <= 10_000);
    public static readonly StyledProperty<double> HeadWidthProperty = AvaloniaProperty.Register<ArrowAnnotation, double>(nameof(HeadWidth), 6,
        validate: value => double.IsFinite(value) && value >= 0 && value <= 10_000);
    public ChartArrowHeads ArrowHeads { get => GetValue(ArrowHeadsProperty); set => SetValue(ArrowHeadsProperty, value); }
    public double HeadLength { get => GetValue(HeadLengthProperty); set => SetValue(HeadLengthProperty, value); }
    public double HeadWidth { get => GetValue(HeadWidthProperty); set => SetValue(HeadWidthProperty, value); }

    public override void Render(DrawingContext drawing, ChartAnnotationRenderContext context)
    {
        if (!TryGetEndpoints(context, out var first, out var last)) return;
        var dx = last.X - first.X; var dy = last.Y - first.Y;
        var maximum = Math.Max(Math.Abs(dx), Math.Abs(dy));
        if (!double.IsFinite(maximum) || maximum <= 0) return;
        var nx = dx / maximum; var ny = dy / maximum;
        var norm = Math.Sqrt(nx * nx + ny * ny);
        var length = maximum * norm;
        if (!double.IsFinite(length) || length <= 0) return;
        var direction = new Vector(nx / norm, ny / norm);
        drawing.DrawLine(MakePen(context), first, last);
        if (ArrowHeads != ChartArrowHeads.None && HeadLength != 0 && HeadWidth != 0)
        {
            var headLength = Math.Min(HeadLength, length / (ArrowHeads == ChartArrowHeads.Both ? 2 : 1));
            var halfWidth = HeadWidth / 2 * (headLength / HeadLength);
            var headPen = new Pen(Stroke ?? Foreground ?? context.Foreground, StrokeThickness);
            if ((ArrowHeads & ChartArrowHeads.Start) != 0) DrawHead(drawing, context, first, -direction, headLength, halfWidth, headPen);
            if ((ArrowHeads & ChartArrowHeads.End) != 0) DrawHead(drawing, context, last, direction, headLength, halfWidth, headPen);
        }
        var bounds = new Rect(new Point(Math.Min(first.X, last.X), Math.Min(first.Y, last.Y)),
            new Point(Math.Max(first.X, last.X), Math.Max(first.Y, last.Y)));
        DrawShapeLabel(drawing, context, bounds, first, last);
    }

    private void DrawHead(DrawingContext drawing, ChartAnnotationRenderContext context, Point tip, Vector direction, double length, double halfWidth, Pen pen)
    {
        var center = tip - direction * length;
        var perpendicular = new Vector(-direction.Y, direction.X) * halfWidth;
        var left = center + perpendicular; var right = center - perpendicular;
        if (!Finite(center) || !Finite(left) || !Finite(right)) return;
        var geometry = new StreamGeometry();
        using (var path = geometry.Open()) { path.BeginFigure(tip, true); path.LineTo(left); path.LineTo(right); path.EndFigure(true); }
        drawing.DrawGeometry(Fill ?? Stroke ?? Foreground ?? context.Foreground, pen, geometry);
    }
}
