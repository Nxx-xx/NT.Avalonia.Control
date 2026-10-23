using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>A linear scale presented as an arc with threshold bands, ticks, a needle and a target.</summary>
public class CircularGaugeChart : GaugeBase
{
    public static readonly StyledProperty<double> StartAngleProperty = AvaloniaProperty.Register<CircularGaugeChart, double>(nameof(StartAngle), -225, validate: double.IsFinite);
    public static readonly StyledProperty<double> SweepAngleProperty = AvaloniaProperty.Register<CircularGaugeChart, double>(nameof(SweepAngle), 270, validate: v => double.IsFinite(v) && v > 0 && v <= 360);
    public static readonly StyledProperty<bool> ShowNeedleProperty = AvaloniaProperty.Register<CircularGaugeChart, bool>(nameof(ShowNeedle), true);
    public static readonly StyledProperty<bool> ShowMajorTicksProperty = AvaloniaProperty.Register<CircularGaugeChart, bool>(nameof(ShowMajorTicks), true);
    public static readonly StyledProperty<int> TickCountProperty = AvaloniaProperty.Register<CircularGaugeChart, int>(nameof(TickCount), 5, validate: v => v >= 1 && v <= 100);
    public double StartAngle { get => GetValue(StartAngleProperty); set => SetValue(StartAngleProperty, value); }
    public double SweepAngle { get => GetValue(SweepAngleProperty); set => SetValue(SweepAngleProperty, value); }
    public bool ShowNeedle { get => GetValue(ShowNeedleProperty); set => SetValue(ShowNeedleProperty, value); }
    public bool ShowMajorTicks { get => GetValue(ShowMajorTicksProperty); set => SetValue(ShowMajorTicksProperty, value); }
    public int TickCount { get => GetValue(TickCountProperty); set => SetValue(TickCountProperty, value); }
    internal double AngleAt(double fraction) => GaugeGeometry.NormalizeAngle(StartAngle) + (Clockwise ? 1 : -1) * SweepAngle * fraction;
    private protected override void RenderGauge(DrawingContext context, GaugeLayout layout)
    {
        var radius = layout.Radius; var inner = Math.Max(0, radius - TrackThickness); var sweep = (Clockwise ? 1 : -1) * SweepAngle;
        context.DrawGeometry(TrackBrush, null, GaugeGeometry.Annulus(layout.Center, radius, inner, StartAngle, sweep));
        if (IsDomainValid)
        {
            foreach (var range in ActiveRanges)
                if (Normalize(range.Start) is { } from && Normalize(range.End) is { } to && to > from)
                    context.DrawGeometry(range.Brush, null, GaugeGeometry.Annulus(layout.Center, radius, inner, AngleAt(from), sweep * (to - from)));
            if (NormalizedValue is { } progress)
            {
                context.DrawGeometry(ValueBrush, null, GaugeGeometry.Annulus(layout.Center, radius, inner, StartAngle, sweep * progress));
                if (ShowNeedle)
                {
                    context.DrawLine(new Pen(NeedleBrush, 3), layout.Center, GaugeGeometry.PointAt(layout.Center, AngleAt(progress), Math.Max(0, inner - 6)));
                    context.DrawEllipse(NeedleBrush, null, layout.Center, 4, 4);
                }
            }
            if (NormalizedTarget is { } target) context.DrawLine(new Pen(NeedleBrush, 3), GaugeGeometry.PointAt(layout.Center, AngleAt(target), Math.Max(0, inner - 5)), GaugeGeometry.PointAt(layout.Center, AngleAt(target), radius + 3));
            for (var index = 0; index <= TickCount; index++)
            {
                if (index == TickCount && SweepAngle == 360) continue;
                var fraction = (double)index / TickCount;
                if (ShowMajorTicks) context.DrawLine(new Pen(AxisBrush, 1), GaugeGeometry.PointAt(layout.Center, AngleAt(fraction), radius + 2), GaugeGeometry.PointAt(layout.Center, AngleAt(fraction), radius + 6));
                if (ShowScale) DrawText(context, Format(ValueAt(fraction)), GaugeGeometry.PointAt(layout.Center, AngleAt(fraction), radius + FontSize));
            }
        }
        if (ShowValue) DrawText(context, CurrentText, ValueTextPosition(layout), sizeScale: 1.35);
    }
    private protected virtual Point ValueTextPosition(GaugeLayout layout) => layout.Center + new Vector(0, layout.Radius * .35);
}

/// <summary>A semicircular gauge with the same business scale and threshold contract as CircularGaugeChart.</summary>
public sealed class GaugeChart : CircularGaugeChart
{
    public GaugeChart() { SetCurrentValue(StartAngleProperty, -180); SetCurrentValue(SweepAngleProperty, 180); }
    internal override GaugeLayout GetGaugeLayout()
    {
        var normalizedStart = (StartAngle % 360 + 360) % 360;
        if (SweepAngle != 180 || Math.Abs(normalizedStart - 180) > 1e-10) return base.GetGaugeLayout();
        var rect = GaugeContentBounds; var reserve = ShowScale ? FontSize * 1.6 : 0;
        return new GaugeLayout(rect, new Point(rect.Center.X, Clockwise ? rect.Bottom - 12 : rect.Top + 12), Math.Max(0, Math.Min(rect.Width / 2, rect.Height - 12) - reserve));
    }
    private protected override Point ValueTextPosition(GaugeLayout layout)
    {
        var normalizedStart = (StartAngle % 360 + 360) % 360;
        return SweepAngle == 180 && Math.Abs(normalizedStart - 180) <= 1e-10 ? layout.Center + new Vector(0, (Clockwise ? -1 : 1) * Math.Max(18, layout.Radius * .22)) : base.ValueTextPosition(layout);
    }
}

/// <summary>A single progress ring with percentage, custom center text and optional scale bounds.</summary>
public sealed class ProgressDonutChart : GaugeBase
{
    public static readonly StyledProperty<double> StartAngleProperty = AvaloniaProperty.Register<ProgressDonutChart, double>(nameof(StartAngle), -90, validate: double.IsFinite);
    public static readonly StyledProperty<double> SweepAngleProperty = AvaloniaProperty.Register<ProgressDonutChart, double>(nameof(SweepAngle), 360, validate: v => double.IsFinite(v) && v > 0 && v <= 360);
    public static readonly StyledProperty<bool> ShowBoundsProperty = AvaloniaProperty.Register<ProgressDonutChart, bool>(nameof(ShowBounds), true);
    public static readonly StyledProperty<bool> ShowPercentageProperty = AvaloniaProperty.Register<ProgressDonutChart, bool>(nameof(ShowPercentage), true);
    public static readonly StyledProperty<string?> CenterLabelProperty = AvaloniaProperty.Register<ProgressDonutChart, string?>(nameof(CenterLabel));
    public double StartAngle { get => GetValue(StartAngleProperty); set => SetValue(StartAngleProperty, value); }
    public double SweepAngle { get => GetValue(SweepAngleProperty); set => SetValue(SweepAngleProperty, value); }
    public bool ShowBounds { get => GetValue(ShowBoundsProperty); set => SetValue(ShowBoundsProperty, value); }
    public bool ShowPercentage { get => GetValue(ShowPercentageProperty); set => SetValue(ShowPercentageProperty, value); }
    public string? CenterLabel { get => GetValue(CenterLabelProperty); set => SetValue(CenterLabelProperty, value); }
    private protected override void RenderGauge(DrawingContext context, GaugeLayout layout)
    {
        var inner = Math.Max(0, layout.Radius - TrackThickness); var sweep = (Clockwise ? 1 : -1) * SweepAngle; var start = GaugeGeometry.NormalizeAngle(StartAngle);
        context.DrawGeometry(TrackBrush, null, GaugeGeometry.Annulus(layout.Center, layout.Radius, inner, start, sweep));
        if (NormalizedValue is { } progress) context.DrawGeometry(ValueBrush, null, GaugeGeometry.Annulus(layout.Center, layout.Radius, inner, start, sweep * progress));
        if (NormalizedTarget is { } target) context.DrawLine(new Pen(NeedleBrush, 3), GaugeGeometry.PointAt(layout.Center, start + sweep * target, Math.Max(0, inner - 4)), GaugeGeometry.PointAt(layout.Center, start + sweep * target, layout.Radius + 3));
        if (ShowValue) DrawText(context, CenterLabel ?? (ShowPercentage && NormalizedValue is { } fraction ? $"{fraction * 100:0.#}%" : CurrentText), layout.Center, sizeScale: 1.5);
        if (ShowBounds && ShowScale && IsDomainValid) DrawText(context, $"{Format(Minimum)} – {Format(Maximum)}", layout.Center + new Vector(0, layout.Radius * .45));
    }
}
