using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>Observable concentric progress rings on one shared domain. Brushes may be gradients.</summary>
public sealed class GradientRingChart : GaugeBase
{
    public static readonly StyledProperty<IEnumerable<GaugeValue>?> ValuesSourceProperty = AvaloniaProperty.Register<GradientRingChart, IEnumerable<GaugeValue>?>(nameof(ValuesSource));
    public static readonly StyledProperty<double> RingSpacingProperty = AvaloniaProperty.Register<GradientRingChart, double>(nameof(RingSpacing), 6, validate: v => double.IsFinite(v) && v >= 0 && v <= 4096);
    public static readonly StyledProperty<double> StartAngleProperty = AvaloniaProperty.Register<GradientRingChart, double>(nameof(StartAngle), -90, validate: double.IsFinite);
    public static readonly StyledProperty<bool> ShowLabelsProperty = AvaloniaProperty.Register<GradientRingChart, bool>(nameof(ShowLabels), true);
    public GradientRingChart() => Values.CollectionChanged += (_, _) => ItemsChanged();
    public ObservableCollection<GaugeValue> Values { get; } = [];
    public IEnumerable<GaugeValue>? ValuesSource { get => GetValue(ValuesSourceProperty); set => SetValue(ValuesSourceProperty, value); }
    public double RingSpacing { get => GetValue(RingSpacingProperty); set => SetValue(RingSpacingProperty, value); }
    public double StartAngle { get => GetValue(StartAngleProperty); set => SetValue(StartAngleProperty, value); }
    public bool ShowLabels { get => GetValue(ShowLabelsProperty); set => SetValue(ShowLabelsProperty, value); }
    private IEnumerable<GaugeValue> ActiveValues => (ValuesSource ?? Values).Where(v => v is not null);
    protected override IEnumerable<StyledElement> SourceElements => base.SourceElements.Concat(ActiveValues);
    protected override IEnumerable<INotifyCollectionChanged> SourceCollections => ValuesSource is INotifyCollectionChanged collection ? base.SourceCollections.Append(collection) : base.SourceCollections;
    internal IReadOnlyList<GaugeRingLayout> GetRings()
    {
        var layout = GetGaugeLayout(); var result = new List<GaugeRingLayout>(); var index = 0;
        foreach (var value in ActiveValues)
        {
            var radius = layout.Radius - index * (TrackThickness + RingSpacing); if (radius <= 0) break;
            result.Add(new GaugeRingLayout(value, layout.Center, radius, Math.Max(0, radius - TrackThickness), Normalize(value.Value))); index++;
        }
        return result;
    }
    private protected override void RenderGauge(DrawingContext context, GaugeLayout layout)
    {
        var rings = GetRings(); var direction = Clockwise ? 1 : -1; var start = GaugeGeometry.NormalizeAngle(StartAngle);
        foreach (var ring in rings)
        {
            context.DrawGeometry(TrackBrush, null, GaugeGeometry.Annulus(ring.Center, ring.OuterRadius, ring.InnerRadius, start, 360 * direction));
            if (ring.Fraction is { } fraction) context.DrawGeometry(ring.Item.Brush ?? ValueBrush, null, GaugeGeometry.Annulus(ring.Center, ring.OuterRadius, ring.InnerRadius, start, 360 * direction * fraction));
            if (NormalizedTarget is { } target)
                context.DrawLine(new Pen(NeedleBrush, 3), GaugeGeometry.PointAt(ring.Center, start + 360 * direction * target, ring.InnerRadius), GaugeGeometry.PointAt(ring.Center, start + 360 * direction * target, ring.OuterRadius));
        }
        if (ShowValue)
        {
            var valid = rings.Where(r => r.Fraction is not null).ToArray();
            DrawText(context, valid.Length == 0 ? "—" : $"{valid.Average(r => r.Fraction!.Value) * 100:0.#}%", layout.Center, sizeScale: 1.3);
        }
        if (ShowLabels)
        {
            var text = string.Join("  ·  ", rings.Select(r => $"{r.Item.Label ?? "值"} {(r.Fraction is null ? "—" : Format(r.Item.Value))}"));
            DrawText(context, text, new Point(layout.Bounds.Center.X, layout.Bounds.Bottom - FontSize / 2));
        }
    }
}

internal readonly record struct GaugeRingLayout(GaugeValue Item, Point Center, double OuterRadius, double InnerRadius, double? Fraction);
