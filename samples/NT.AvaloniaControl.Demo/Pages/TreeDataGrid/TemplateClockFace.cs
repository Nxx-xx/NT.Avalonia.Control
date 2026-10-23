using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace NT.AvaloniaControl.Demo.Pages.TreeDataGrid;

/// <summary>The window supplies a single shared clock tick; this control only draws its bound time.</summary>
internal sealed class TemplateClockFace : Control
{
    public static readonly StyledProperty<TimeSpan> TimeProperty =
        AvaloniaProperty.Register<TemplateClockFace, TimeSpan>(nameof(Time));

    private static readonly IBrush FaceBrush = new SolidColorBrush(Color.Parse("#1E293B"));
    private static readonly Pen RimPen = new(new SolidColorBrush(Color.Parse("#64748B")), 1);
    private static readonly Pen TickPen = new(new SolidColorBrush(Color.Parse("#94A3B8")), 1);
    private static readonly Pen HourPen = new(Brushes.White, 2.2);
    private static readonly Pen MinutePen = new(Brushes.White, 1.6);
    private static readonly Pen SecondPen = new(new SolidColorBrush(Color.Parse("#38BDF8")), 1);

    static TemplateClockFace() => AffectsRender<TemplateClockFace>(TimeProperty);
    public TemplateClockFace() { Width = 48; Height = 48; }
    public TimeSpan Time { get => GetValue(TimeProperty); set => SetValue(TimeProperty, value); }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var radius = Math.Max(0, Math.Min(Bounds.Width, Bounds.Height) / 2 - 1);
        if (radius == 0) return;
        context.DrawEllipse(FaceBrush, RimPen, center, radius, radius);
        for (var mark = 0; mark < 12; mark++)
            context.DrawLine(TickPen, At(center, mark * 30, radius * .79), At(center, mark * 30, radius * .92));
        context.DrawLine(HourPen, center, At(center, Time.TotalHours % 12 * 30, radius * .47));
        context.DrawLine(MinutePen, center, At(center, Time.TotalMinutes % 60 * 6, radius * .67));
        context.DrawLine(SecondPen, center, At(center, Time.Seconds * 6, radius * .77));
        context.DrawEllipse(Brushes.White, null, center, 2, 2);
    }

    private static Point At(Point center, double degrees, double length)
    {
        var radians = (degrees - 90) * Math.PI / 180;
        return new Point(center.X + Math.Cos(radians) * length, center.Y + Math.Sin(radians) * length);
    }
}
