using Avalonia;
using Avalonia.Layout;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

public sealed record ScheduleEventLabel(object Id, string Text, Rect Bounds, Point Anchor, bool IsVisible);
/// <summary>Chronological UTC events with horizontal/vertical orientation and bounded collision-free labels.</summary>
public sealed partial class EventTimelineChart : ScheduleChartBase
{
    public static readonly StyledProperty<Orientation> OrientationProperty = AvaloniaProperty.Register<EventTimelineChart, Orientation>(nameof(Orientation), Orientation.Horizontal, validate: Enum.IsDefined);
    public static readonly StyledProperty<double> StrokeThicknessProperty = AvaloniaProperty.Register<EventTimelineChart, double>(nameof(StrokeThickness), 2, validate: v => double.IsFinite(v) && v >= 0 && v <= 1024);
    private IReadOnlyList<ScheduleEventLabel> _labels = [];
    private (DateTimeOffset Start, DateTimeOffset End) _range;
    protected override SchedulingKind Kind => SchedulingKind.Event;
    public EventTimelineChart() { MarkerSize = 12; ShowGrid = false; }
    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    public double StrokeThickness { get => GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
    public IReadOnlyList<ScheduleEventLabel> LabelPlacements { get { _ = GetMarks(); return _labels; } }
    public int HiddenLabelCount { get { _ = GetMarks(); return _labels.Count(l => !l.IsVisible); } }
    protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
    {
        EnsureScheduleData(); _labels = []; _range = TimeBounds(EventData.FirstOrDefault()?.Date, EventData.LastOrDefault()?.Date);
        var localStart = RemoveViewport(plot.TopLeft, plot); var localEnd = RemoveViewport(plot.BottomRight, plot); var vertical = Orientation == Orientation.Vertical;
        var f0 = vertical ? plot.Height <= 0 ? 0 : (localStart.Y - plot.Top) / plot.Height : plot.Width <= 0 ? 0 : (localStart.X - plot.Left) / plot.Width;
        var f1 = vertical ? plot.Height <= 0 ? 1 : (localEnd.Y - plot.Top) / plot.Height : plot.Width <= 0 ? 1 : (localEnd.X - plot.Left) / plot.Width;
        DateTimeOffset At(double fraction) => new((long)Math.Clamp((decimal)_range.Start.UtcTicks + (decimal)(fraction * (_range.End.UtcTicks - _range.Start.UtcTicks)), 0, DateTimeOffset.MaxValue.UtcTicks), TimeSpan.Zero);
        var first = At(f0); var last = At(f1); var lo = 0; var hi = EventData.Count; var visited = 0;
        while (lo < hi) { visited++; var mid = lo + (hi - lo) / 2; if (EventData[mid].Date < first) lo = mid + 1; else hi = mid; }
        var marks = new List<EngineeringMarkInfo>(); var labels = new List<ScheduleEventLabel>(); var occupied = new LabelGrid(Math.Max(4, FontSize * 1.5 + 6));
        for (var i = lo; i < EventData.Count && EventData[i].Date <= last; i++)
        {
            visited++; var entry = EventData[i]; var fraction = TimeFraction(entry.Date, _range.Start, _range.End); var p = ApplyViewport(vertical ? new(plot.Center.X, plot.Top + fraction * plot.Height) : new(plot.Left + fraction * plot.Width, plot.Center.Y), plot);
            var bounds = new Rect(p.X - MarkerSize / 2, p.Y - MarkerSize / 2, MarkerSize, MarkerSize); if (!bounds.Intersects(plot)) continue;
            var member = Member(entry.SourceIndex, entry.Id, entry.Source, entry.Event.Label, entry.Date.ToUnixTimeMilliseconds(), entry.Event.Value);
            marks.Add(new(Array.AsReadOnly(new[] { member }), new EllipseGeometry(bounds), ItemBrush(entry.Id, entry.Index), p) { Key = entry.Id, Value = entry.Event.Value, TooltipText = $"{entry.Date:u}: {entry.Event.Label ?? entry.Id.ToString()} {entry.Event.Description}" });
            var text = entry.Event.Label ?? entry.Id.ToString() ?? ""; var width = Math.Min(vertical ? Math.Max(0, plot.Width / 2 - MarkerSize - 8) : Math.Min(140, plot.Width), Math.Max(24, text.Length * FontSize * .65)); var height = FontSize * 1.5 + 2; var placed = false; var candidate = default(Rect);
            // At most sixteen alternatives per label: bounded work even for coincident events.
            for (var slot = 0; slot < 16 && !placed && ShowLabels; slot++)
            {
                if (vertical)
                { var direction = slot % 2 == 0 ? -1 : 1; var shift = ((slot / 2 + 1) / 2) * height * (slot / 2 % 2 == 0 ? 1 : -1); candidate = new(direction < 0 ? p.X - MarkerSize - width : p.X + MarkerSize, Math.Clamp(p.Y + shift - height / 2, plot.Top, Math.Max(plot.Top, plot.Bottom - height)), width, height); }
                else
                { var row = slot / 2 + 1; var y = slot % 2 == 0 ? p.Y - MarkerSize - row * height : p.Y + MarkerSize + (row - 1) * height; candidate = new(Math.Clamp(p.X - width / 2, plot.Left, Math.Max(plot.Left, plot.Right - width)), y, width, height); }
                if (width > 0 && plot.Contains(candidate.TopLeft) && plot.Contains(candidate.BottomRight) && occupied.TryAdd(candidate)) placed = true;
            }
            labels.Add(new(entry.Id, text, candidate, p, placed));
        }
        LastQueryVisitedNodes = visited; _labels = labels.AsReadOnly(); return marks;
    }
    protected override void RenderUnderlay(DrawingContext context, Rect plot)
    { var pen = new Pen(GridLineBrush ?? Foreground ?? Brushes.Gray, StrokeThickness); if (Orientation == Orientation.Horizontal) { var y = ApplyViewport(plot.Center, plot).Y; context.DrawLine(pen, new(plot.Left, y), new(plot.Right, y)); } else { var x = ApplyViewport(plot.Center, plot).X; context.DrawLine(pen, new(x, plot.Top), new(x, plot.Bottom)); } }
    protected override void RenderAnnotations(DrawingContext context, Rect plot)
    { if (!ShowLabels) return; using var clip = context.PushClip(plot); foreach (var label in _labels.Where(l => l.IsVisible)) { context.DrawLine(new Pen(Foreground ?? Brushes.Gray, .7), label.Anchor, label.Bounds.Center); if (!UsesDataLabelTemplate) DrawLabel(context, label.Text, label.Bounds.Center, label.Bounds.Width); } }
    protected override void ClearScheduleGeometry() { _labels = []; }
    private sealed class LabelGrid(double rowHeight)
    {
        private readonly Dictionary<(int X, int Y), List<Rect>> _cells = [];
        public bool TryAdd(Rect bounds)
        {
            var padded = bounds.Inflate(2); var left = (int)Math.Floor(padded.Left / 128); var right = (int)Math.Floor(padded.Right / 128); var top = (int)Math.Floor(padded.Top / rowHeight); var bottom = (int)Math.Floor(padded.Bottom / rowHeight);
            for (var x = left; x <= right; x++) for (var y = top; y <= bottom; y++) if (_cells.TryGetValue((x, y), out var candidates) && candidates.Any(r => r.Intersects(padded))) return false;
            for (var x = left; x <= right; x++) for (var y = top; y <= bottom; y++) { if (!_cells.TryGetValue((x, y), out var candidates)) _cells.Add((x, y), candidates = []); candidates.Add(padded); }
            return true;
        }
    }
}
