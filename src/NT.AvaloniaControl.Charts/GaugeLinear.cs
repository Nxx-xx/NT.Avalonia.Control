using Avalonia;
using Avalonia.Layout;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>A horizontal or vertical linear track, value fill, threshold intervals and a current/target pointer.</summary>
public sealed class LinearGaugeChart : GaugeBase
{
    public static readonly StyledProperty<Orientation> OrientationProperty = AvaloniaProperty.Register<LinearGaugeChart, Orientation>(nameof(Orientation), Orientation.Horizontal, validate: Enum.IsDefined);
    public static readonly StyledProperty<bool> ShowNeedleProperty = AvaloniaProperty.Register<LinearGaugeChart, bool>(nameof(ShowNeedle), true);
    public static readonly StyledProperty<int> TickCountProperty = AvaloniaProperty.Register<LinearGaugeChart, int>(nameof(TickCount), 5, validate: v => v >= 1 && v <= 100);
    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    public bool ShowNeedle { get => GetValue(ShowNeedleProperty); set => SetValue(ShowNeedleProperty, value); }
    public int TickCount { get => GetValue(TickCountProperty); set => SetValue(TickCountProperty, value); }
    internal Rect TrackBounds
    {
        get
        {
            var rect = GaugeContentBounds;
            return Orientation == Orientation.Horizontal ? new Rect(rect.Left, rect.Center.Y - Math.Min(rect.Height, TrackThickness) / 2, rect.Width, Math.Min(rect.Height, TrackThickness)) :
                new Rect(rect.Center.X - Math.Min(rect.Width, TrackThickness) / 2, rect.Top, Math.Min(rect.Width, TrackThickness), rect.Height);
        }
    }
    internal Point PositionAt(double fraction)
    {
        var rect = TrackBounds; if (!Clockwise) fraction = 1 - fraction;
        return Orientation == Orientation.Horizontal ? new Point(rect.Left + rect.Width * fraction, rect.Center.Y) : new Point(rect.Center.X, rect.Bottom - rect.Height * fraction);
    }
    internal Rect IntervalBounds(double from, double to)
    {
        var first = PositionAt(from); var last = PositionAt(to); var rect = TrackBounds;
        return Orientation == Orientation.Horizontal ? new Rect(Math.Min(first.X, last.X), rect.Top, Math.Abs(last.X - first.X), rect.Height) : new Rect(rect.Left, Math.Min(first.Y, last.Y), rect.Width, Math.Abs(last.Y - first.Y));
    }
    private protected override void RenderGauge(DrawingContext context, GaugeLayout layout)
    {
        var track = TrackBounds; context.DrawRectangle(TrackBrush, null, track);
        if (IsDomainValid)
        {
            foreach (var range in ActiveRanges) if (Normalize(range.Start) is { } from && Normalize(range.End) is { } to && to > from) context.DrawRectangle(range.Brush, null, IntervalBounds(from, to));
            if (NormalizedValue is { } progress)
            {
                context.DrawRectangle(ValueBrush, null, IntervalBounds(0, progress));
                if (ShowNeedle) Pointer(progress, 5);
            }
            if (NormalizedTarget is { } target) Pointer(target, 9);
            if (ShowScale)
                for (var index = 0; index <= TickCount; index++)
                {
                    var fraction = (double)index / TickCount; var at = PositionAt(fraction);
                    if (Orientation == Orientation.Horizontal)
                    {
                        context.DrawLine(new Pen(AxisBrush, 1), new Point(at.X, track.Bottom + 2), new Point(at.X, track.Bottom + 6));
                        DrawText(context, Format(ValueAt(fraction)), new Point(at.X, track.Bottom + FontSize + 6));
                    }
                    else
                    {
                        context.DrawLine(new Pen(AxisBrush, 1), new Point(track.Right + 2, at.Y), new Point(track.Right + 6, at.Y));
                        DrawText(context, Format(ValueAt(fraction)), new Point(track.Right + FontSize * 1.7 + 6, at.Y));
                    }
                }
        }
        if (ShowValue) DrawText(context, CurrentText, Orientation == Orientation.Horizontal ? new Point(track.Center.X, track.Top - FontSize - 8) : new Point(track.Left - FontSize * 2, track.Center.Y), sizeScale: 1.25);
        void Pointer(double fraction, double extension)
        {
            var at = PositionAt(fraction);
            context.DrawLine(new Pen(NeedleBrush, 3), Orientation == Orientation.Horizontal ? new Point(at.X, track.Top - extension) : new Point(track.Left - extension, at.Y), Orientation == Orientation.Horizontal ? new Point(at.X, track.Bottom + extension) : new Point(track.Right + extension, at.Y));
        }
    }
}
