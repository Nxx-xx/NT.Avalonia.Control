using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>Numeric resource intervals with optimal stable overlap tracks.</summary>
public sealed class SwimlaneChart : ScheduleChartBase
{
    public static readonly StyledProperty<double> LaneHeightProperty = AvaloniaProperty.Register<SwimlaneChart, double>(nameof(LaneHeight), 80, validate: v => double.IsFinite(v) && v >= 4 && v <= 4096);
    public static readonly StyledProperty<double> TaskHeightProperty = AvaloniaProperty.Register<SwimlaneChart, double>(nameof(TaskHeight), 30, validate: v => double.IsFinite(v) && v >= 1 && v <= 4096);
    public static readonly StyledProperty<double> TaskSpacingProperty = AvaloniaProperty.Register<SwimlaneChart, double>(nameof(TaskSpacing), 5, validate: v => double.IsFinite(v) && v >= 0 && v <= 4096);
    public static readonly StyledProperty<double> TaskCornerRadiusProperty = AvaloniaProperty.Register<SwimlaneChart, double>(nameof(TaskCornerRadius), 4, validate: v => double.IsFinite(v) && v >= 0 && v <= 4096);
    public static readonly StyledProperty<double?> MinimumProperty = AvaloniaProperty.Register<SwimlaneChart, double?>(nameof(Minimum), validate: v => v is null || double.IsFinite(v.Value));
    public static readonly StyledProperty<double?> MaximumProperty = AvaloniaProperty.Register<SwimlaneChart, double?>(nameof(Maximum), validate: v => v is null || double.IsFinite(v.Value));
    public static readonly StyledProperty<IBrush?> LaneSeparatorBrushProperty = AvaloniaProperty.Register<SwimlaneChart, IBrush?>(nameof(LaneSeparatorBrush));
    public static readonly StyledProperty<IBrush?> LaneBackgroundBrushProperty = AvaloniaProperty.Register<SwimlaneChart, IBrush?>(nameof(LaneBackgroundBrush));
    private ScheduleSwimlaneLayout? _indexedLayout;
    private readonly Dictionary<int, ScheduleLanePlacement> _placements = [];
    private readonly List<double> _offsets = [];
    private readonly List<(EngineeringMarkInfo Mark, string Label)> _labels = [];
    private double _previousLaneHeight, _previousTaskHeight, _previousSpacing, _minimum, _maximum;
    protected override SchedulingKind Kind => SchedulingKind.Swimlane;
    public double LaneHeight { get => GetValue(LaneHeightProperty); set => SetValue(LaneHeightProperty, value); }
    public double TaskHeight { get => GetValue(TaskHeightProperty); set => SetValue(TaskHeightProperty, value); }
    public double TaskSpacing { get => GetValue(TaskSpacingProperty); set => SetValue(TaskSpacingProperty, value); }
    public double TaskCornerRadius { get => GetValue(TaskCornerRadiusProperty); set => SetValue(TaskCornerRadiusProperty, value); }
    public double? Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double? Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public IBrush? LaneSeparatorBrush { get => GetValue(LaneSeparatorBrushProperty); set => SetValue(LaneSeparatorBrushProperty, value); }
    public IBrush? LaneBackgroundBrush { get => GetValue(LaneBackgroundBrushProperty); set => SetValue(LaneBackgroundBrushProperty, value); }
    public bool ShowTaskLabels { get => ShowLabels; set => ShowLabels = value; }
    public static readonly StyledProperty<bool> ShowTaskLabelsProperty = ShowLabelsProperty;
    protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
    {
        EnsureScheduleData(); _labels.Clear(); if (SpanData is not { } snapshot || LaneData is not { } layout) return [];
        if (!ReferenceEquals(layout, _indexedLayout) || _previousLaneHeight != LaneHeight || _previousTaskHeight != TaskHeight || _previousSpacing != TaskSpacing)
        {
            _indexedLayout = layout; _previousLaneHeight = LaneHeight; _previousTaskHeight = TaskHeight; _previousSpacing = TaskSpacing;
            _placements.Clear(); var counts = new int[layout.Lanes.Count]; foreach (var placement in layout.Placements) { _placements.Add(placement.Entry.Index, placement); counts[placement.LaneIndex] = placement.TrackCount; }
            _offsets.Clear(); _offsets.Add(0); foreach (var count in counts) _offsets.Add(_offsets[^1] + Math.Max(LaneHeight, count * (TaskHeight + TaskSpacing) + TaskSpacing));
        }
        _minimum = Minimum ?? layout.Minimum; _maximum = Maximum ?? layout.Maximum;
        if (_maximum < _minimum) throw new ArgumentException("Maximum must not precede Minimum.");
        if (_maximum == _minimum) { var delta = Math.Max(1, Math.Abs(_minimum) * .01); _minimum = Math.Max(-double.MaxValue, _minimum - delta); _maximum = Math.Min(double.MaxValue, _maximum + delta); }
        var localLeft = RemoveViewport(plot.TopLeft, plot); var localRight = RemoveViewport(plot.TopRight, plot);
        double At(double x) => Interpolate(_minimum, _maximum, Math.Clamp(plot.Width <= 0 ? 0 : (x - plot.Left) / plot.Width, 0, 1));
        var query = snapshot.QueryVisible(At(localLeft.X), At(localRight.X)); LastQueryVisitedNodes = query.VisitedNodes; var marks = new List<EngineeringMarkInfo>();
        foreach (var entry in query.Entries)
        {
            if (LaneFilter is { } lane && !StringComparer.Ordinal.Equals(entry.Span.Lane ?? "", lane)) continue;
            var placement = _placements[entry.Index]; var y = plot.Top + _offsets[placement.LaneIndex] + TaskSpacing + placement.Track * (TaskHeight + TaskSpacing) + TaskHeight / 2;
            var start = ApplyViewport(new(plot.Left + Fraction(entry.Span.Start, _minimum, _maximum) * plot.Width, y), plot); var end = ApplyViewport(new(plot.Left + Fraction(entry.Span.End, _minimum, _maximum) * plot.Width, y), plot);
            var height = TaskHeight * Zoom; var width = Math.Max(.5, Math.Abs(end.X - start.X)); var bounds = new Rect(Math.Min(start.X, end.X), start.Y - height / 2, width, height); if (!bounds.Intersects(plot)) continue;
            var member = Member(entry.SourceIndex, entry.Id, entry.Source, entry.Span.Label, entry.Span.Start, entry.Span.End);
            var mark = new EngineeringMarkInfo(Array.AsReadOnly(new[] { member }), new RectangleGeometry(bounds, TaskCornerRadius * Zoom, TaskCornerRadius * Zoom), ItemBrush(entry.Id, placement.LaneIndex), bounds.Center)
            { Key = entry.Id, Value = entry.Span.End, TooltipText = $"{placement.Lane} / {entry.Span.Label ?? entry.Id.ToString()}: {F(entry.Span.Start)} – {F(entry.Span.End)}; track {placement.Track + 1}" };
            marks.Add(mark); _labels.Add((mark, entry.Span.Label ?? entry.Id.ToString() ?? ""));
        }
        return marks;
    }
    protected override void RenderUnderlay(DrawingContext context, Rect plot)
    {
        if (LaneData is not { } layout) return;
        for (var i = 0; i < layout.Lanes.Count && i + 1 < _offsets.Count; i++)
        {
            var top = ApplyViewport(new(plot.Left, plot.Top + _offsets[i]), plot).Y; var bottom = ApplyViewport(new(plot.Left, plot.Top + _offsets[i + 1]), plot).Y; if (bottom < plot.Top || top > plot.Bottom) continue;
            if (i % 2 == 1 && LaneBackgroundBrush is { } background) context.DrawRectangle(background, null, new(plot.Left, top, plot.Width, bottom - top));
            if (LaneSeparatorBrush is { } separator) context.DrawLine(new Pen(separator, 1), new(plot.Left, top), new(plot.Right, top));
        }
    }
    protected override IReadOnlyList<EngineeringGridLine> BuildGridLines(Rect plot)
    { var lines = new List<EngineeringGridLine>(); for (var i = 0; i <= 4; i++) { var x = ApplyViewport(new(plot.Left + i * plot.Width / 4, plot.Top), plot).X; lines.Add(new(Line([new(x, plot.Top), new(x, plot.Bottom)]), F(Interpolate(_minimum, _maximum, i / 4d)), new(x, plot.Bottom + 12))); } return lines; }
    protected override void RenderAnnotations(DrawingContext context, Rect plot)
    { if (!ShowLabels) return; using var clip = context.PushClip(plot); foreach (var (mark, label) in _labels) DrawLabel(context, label, mark.Position, Math.Max(0, mark.Bounds.Width - 4)); }
    protected override bool ContainsMark(EngineeringMarkInfo mark, Point point)
    {
        var bounds = mark.Bounds; if (!bounds.Contains(point)) return false; var rx = Math.Min(TaskCornerRadius * Zoom, bounds.Width / 2); var ry = Math.Min(TaskCornerRadius * Zoom, bounds.Height / 2);
        if (rx <= 0 || ry <= 0 || point.X >= bounds.Left + rx && point.X <= bounds.Right - rx || point.Y >= bounds.Top + ry && point.Y <= bounds.Bottom - ry) return true;
        var cx = point.X < bounds.Center.X ? bounds.Left + rx : bounds.Right - rx; var cy = point.Y < bounds.Center.Y ? bounds.Top + ry : bounds.Bottom - ry; var x = (point.X - cx) / rx; var y = (point.Y - cy) / ry; return x * x + y * y <= 1;
    }
    protected override void ClearScheduleGeometry() { _placements.Clear(); _offsets.Clear(); _labels.Clear(); _indexedLayout = null; }
}
