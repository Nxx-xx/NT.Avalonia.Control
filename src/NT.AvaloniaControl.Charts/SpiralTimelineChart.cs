using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>Archimedean chronological spiral with optional explicit period and original event identity.</summary>
public sealed partial class SpiralTimelineChart : ScheduleChartBase
{
    public static readonly StyledProperty<double> TurnsProperty = AvaloniaProperty.Register<SpiralTimelineChart, double>(nameof(Turns), 3, validate: v => double.IsFinite(v) && v > 0 && v <= 64);
    public static readonly StyledProperty<double> InnerRadiusProperty = AvaloniaProperty.Register<SpiralTimelineChart, double>(nameof(InnerRadius), 30, validate: v => double.IsFinite(v) && v >= 0 && v <= 4096);
    public static readonly StyledProperty<TimeSpan?> PeriodProperty = AvaloniaProperty.Register<SpiralTimelineChart, TimeSpan?>(nameof(Period), validate: v => v is null || v.Value.Ticks > 0);
    private ScheduleSpiralLayout? _layout;
    private Geometry? _spiral;
    private readonly List<(Point Position, string Label)> _labels = [];
    protected override SchedulingKind Kind => SchedulingKind.Spiral;
    public SpiralTimelineChart() { ShowGrid = false; }
    public double Turns { get => GetValue(TurnsProperty); set => SetValue(TurnsProperty, value); }
    /// <summary>Unscaled inner radius in DIPs; it must be smaller than the fitted outer radius.</summary>
    public double InnerRadius { get => GetValue(InnerRadiusProperty); set => SetValue(InnerRadiusProperty, value); }
    public TimeSpan? Period { get => GetValue(PeriodProperty); set => SetValue(PeriodProperty, value); }
    public ScheduleSpiralLayout? SpiralLayout { get { _ = GetMarks(); return _layout; } }
    protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
    {
        EnsureScheduleData(); _labels.Clear(); _spiral = null;
        var radius = Math.Max(0, Math.Min(plot.Width, plot.Height) / 2 - MarkerSize / 2); if (radius <= 0) { _layout = null; return []; }
        if (InnerRadius >= radius) throw new ArgumentException("InnerRadius must be smaller than the fitted spiral radius.");
        var range = TimeBounds(EventData.FirstOrDefault()?.Date, EventData.LastOrDefault()?.Date);
        _layout = ChartSchedule.Spiral(EventData.Select(e => e.Event), Turns, InnerRadius / radius, Period, range.Start, range.End);
        // Keep the original pre-sort source indices rather than the sorted projection's temporary enumeration index.
        var originals = EventData.ToDictionary(e => e.Id); var points = _layout.Points.Select(p => p with { Entry = originals[p.Entry.Id] }).ToArray(); _layout = _layout with { Points = Array.AsReadOnly(points) };
        var curve = new List<Point>(); var samples = Math.Max(32, (int)Math.Ceiling(_layout.Turns * 96));
        for (var i = 0; i <= samples; i++) { var fraction = i / (double)samples; var angle = -Math.PI / 2 + fraction * _layout.Turns * Math.Tau; var distance = InnerRadius + (radius - InnerRadius) * fraction; curve.Add(ApplyViewport(new(plot.Center.X + distance * Math.Cos(angle), plot.Center.Y + distance * Math.Sin(angle)), plot)); }
        _spiral = Line(curve); var marks = new List<EngineeringMarkInfo>(); var maxValue = Math.Max(1, EventData.Count == 0 ? 1 : EventData.Max(e => e.Event.Value));
        foreach (var point in _layout.Points)
        {
            var entry = point.Entry; var p = ApplyViewport(new(plot.Center.X + point.Position.X * radius, plot.Center.Y + point.Position.Y * radius), plot); var diameter = MarkerSize * (.5 + .5 * Math.Sqrt(entry.Event.Value / maxValue)); var bounds = new Rect(p.X - diameter / 2, p.Y - diameter / 2, diameter, diameter); if (!bounds.Intersects(plot)) continue;
            var member = Member(entry.SourceIndex, entry.Id, entry.Source, entry.Event.Label, entry.Date.ToUnixTimeMilliseconds(), entry.Event.Value);
            marks.Add(new(Array.AsReadOnly(new[] { member }), new EllipseGeometry(bounds), ItemBrush(entry.Id, entry.Index), p) { Key = entry.Id, Value = entry.Event.Value, TooltipText = $"{entry.Date:u}: {entry.Event.Label ?? entry.Id.ToString()}; {F(entry.Event.Value)}" }); _labels.Add((p, entry.Event.Label ?? entry.Id.ToString() ?? ""));
        }
        LastQueryVisitedNodes = _layout.Points.Count; return marks;
    }
    protected override void RenderUnderlay(DrawingContext context, Rect plot) { if (_spiral is { } spiral) context.DrawGeometry(null, new Pen(GridLineBrush ?? Foreground ?? Brushes.Gray, 1), spiral); }
    protected override void RenderAnnotations(DrawingContext context, Rect plot) { if (!ShowLabels || UsesDataLabelTemplate) return; using var clip = context.PushClip(plot); foreach (var (p, label) in _labels) DrawLabel(context, label, new(p.X, p.Y - MarkerSize - FontSize / 2), 96); }
    protected override void ClearScheduleGeometry() { _layout = null; _spiral = null; _labels.Clear(); }
}
