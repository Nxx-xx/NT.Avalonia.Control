using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

public sealed record ScheduleDependencyPath(object PredecessorId, object TaskId, ScheduleDependencyType Type, IReadOnlyList<Point> Points);
/// <summary>UTC task bars, progress, milestones and nonselectable dependency connectors.</summary>
public sealed class GanttChart : ScheduleChartBase
{
    public static readonly StyledProperty<double> BarHeightProperty = AvaloniaProperty.Register<GanttChart, double>(nameof(BarHeight), .6, validate: v => double.IsFinite(v) && v > 0 && v <= 1);
    public static readonly StyledProperty<double> RowHeightProperty = AvaloniaProperty.Register<GanttChart, double>(nameof(RowHeight), 40, validate: v => double.IsFinite(v) && v >= 4 && v <= 4096);
    public static readonly StyledProperty<IBrush?> BarBrushProperty = AvaloniaProperty.Register<GanttChart, IBrush?>(nameof(BarBrush), Brush.Parse("#93C5FD"));
    public static readonly StyledProperty<IBrush?> ProgressBrushProperty = AvaloniaProperty.Register<GanttChart, IBrush?>(nameof(ProgressBrush), Brush.Parse("#2563EB"));
    public static readonly StyledProperty<bool> ShowDependenciesProperty = AvaloniaProperty.Register<GanttChart, bool>(nameof(ShowDependencies), true);
    private readonly Dictionary<object, Rect> _bars = [];
    private readonly Dictionary<object, ScheduleTaskEntry> _byId = [];
    private ScheduleSnapshot? _indexedSnapshot;
    private IReadOnlyList<ScheduleDependencyPath> _dependencies = [];
    private (DateTimeOffset Start, DateTimeOffset End) _range;
    protected override SchedulingKind Kind => SchedulingKind.Gantt;
    public double BarHeight { get => GetValue(BarHeightProperty); set => SetValue(BarHeightProperty, value); }
    public double RowHeight { get => GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }
    public IBrush? BarBrush { get => GetValue(BarBrushProperty); set => SetValue(BarBrushProperty, value); }
    public IBrush? ProgressBrush { get => GetValue(ProgressBrushProperty); set => SetValue(ProgressBrushProperty, value); }
    public bool ShowDependencies { get => GetValue(ShowDependenciesProperty); set => SetValue(ShowDependenciesProperty, value); }
    public IReadOnlyList<ScheduleDependencyPath> DependencyPaths { get { _ = GetMarks(); return _dependencies; } }
    protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
    {
        EnsureScheduleData(); _bars.Clear(); _dependencies = []; var snapshot = TaskData; if (snapshot is null) return [];
        if (!ReferenceEquals(snapshot, _indexedSnapshot)) { _indexedSnapshot = snapshot; _byId.Clear(); foreach (var task in snapshot.Tasks) _byId.Add(task.Id, task); }
        _range = TimeBounds(snapshot.Minimum, snapshot.Maximum); var visible = VisibleTimeBounds(_range, plot); var query = snapshot.QueryVisible(visible.Start, visible.End); LastQueryVisitedNodes = query.VisitedNodes;
        var result = new List<EngineeringMarkInfo>(); var dependencies = new List<ScheduleDependencyPath>();
        foreach (var entry in query.Entries)
        {
            if (LaneFilter is { } lane && !StringComparer.Ordinal.Equals(entry.Task.Lane ?? "", lane)) continue;
            var start = Position(entry.Start, entry.Index, plot); var end = Position(entry.End, entry.Index, plot); var height = RowHeight * BarHeight * Zoom;
            var bounds = new Rect(Math.Min(start.X, end.X), start.Y - height / 2, Math.Max(entry.IsMilestone ? height : .5, Math.Abs(end.X - start.X)), height);
            if (entry.IsMilestone) bounds = new(start.X - height / 2, start.Y - height / 2, height, height);
            if (!bounds.Intersects(plot)) continue;
            Geometry geometry = entry.IsMilestone ? Polygon([new(start.X, bounds.Top), new(bounds.Right, start.Y), new(start.X, bounds.Bottom), new(bounds.Left, start.Y)]) : new RectangleGeometry(bounds);
            var center = bounds.Center; var member = Member(entry.SourceIndex, entry.Id, entry.Source, entry.Task.Label, entry.Start.ToUnixTimeMilliseconds(), entry.Task.Progress);
            result.Add(new(Array.AsReadOnly(new[] { member }), geometry, ItemBrush(entry.Id, entry.Index, BarBrush), center)
            { Key = entry.Id, Value = entry.Task.Progress, TooltipText = $"{entry.Task.Label ?? entry.Id.ToString()}: {entry.Start:u} – {entry.End:u}; {F(entry.Task.Progress)}%" });
            _bars[entry.Id] = bounds;
            if (ShowDependencies) foreach (var dependency in entry.Task.Dependencies!)
            {
                if (!_byId.TryGetValue(dependency.PredecessorId, out var predecessor) || Equals(entry.Id, predecessor.Id)) continue;
                var fromEnd = dependency.Type is ScheduleDependencyType.FinishStart or ScheduleDependencyType.FinishFinish;
                var toEnd = dependency.Type is ScheduleDependencyType.FinishFinish or ScheduleDependencyType.StartFinish;
                var from = Position(fromEnd ? predecessor.End : predecessor.Start, predecessor.Index, plot); var to = Position(toEnd ? entry.End : entry.Start, entry.Index, plot); var bend = from.X + (from.X <= to.X ? 6 : -6);
                dependencies.Add(new(predecessor.Id, entry.Id, dependency.Type, Array.AsReadOnly(new[] { from, new Point(bend, from.Y), new Point(bend, to.Y), to })));
            }
        }
        _dependencies = dependencies.AsReadOnly(); return result;
    }
    private Point Position(DateTimeOffset date, int row, Rect plot) => ApplyViewport(new(plot.Left + TimeFraction(date, _range.Start, _range.End) * plot.Width, plot.Top + (row + .5) * RowHeight), plot);
    private static Geometry Polygon(Point[] points) => new PolylineGeometry(points, true);
    protected override IReadOnlyList<EngineeringGridLine> BuildGridLines(Rect plot)
    {
        var lines = new List<EngineeringGridLine>(); for (var i = 0; i <= 4; i++) { var x = ApplyViewport(new(plot.Left + i * plot.Width / 4, plot.Top), plot).X; var ticks = (long)((decimal)_range.Start.UtcTicks + ((decimal)_range.End.UtcTicks - _range.Start.UtcTicks) * i / 4); lines.Add(new(Line([new(x, plot.Top), new(x, plot.Bottom)]), new DateTimeOffset(ticks, TimeSpan.Zero).ToString("MM-dd"), new(x, plot.Bottom + 12))); } return lines;
    }
    protected override void RenderUnderlay(DrawingContext context, Rect plot)
    { foreach (var dependency in _dependencies) { var pen = new Pen(Foreground ?? Brushes.Gray, 1); context.DrawGeometry(null, pen, Line(dependency.Points)); var to = dependency.Points[^1]; var from = dependency.Points[^2]; var direction = to.X >= from.X ? 1 : -1; context.DrawLine(pen, new(to.X - direction * 4, to.Y - 3), to); context.DrawLine(pen, new(to.X - direction * 4, to.Y + 3), to); } }
    protected override void RenderEngineeringMarks(DrawingContext context, IReadOnlyList<EngineeringMarkInfo> marks, EngineeringPointHit? selected, EngineeringPointHit? active)
    {
        base.RenderEngineeringMarks(context, marks, selected, active);
        if (ProgressBrush is null) return;
        foreach (var mark in marks) if (mark.Key is { } id && _byId.TryGetValue(id, out var entry) && !entry.IsMilestone && entry.Task.Progress > 0 && _bars.TryGetValue(id, out var bar)) context.DrawRectangle(ProgressBrush, null, new(bar.X, bar.Y, bar.Width * entry.Task.Progress / 100, bar.Height));
    }
    protected override void RenderAnnotations(DrawingContext context, Rect plot)
    { if (!ShowLabels) return; using var clip = context.PushClip(plot); foreach (var pair in _bars) if (_byId.TryGetValue(pair.Key, out var entry)) DrawLabel(context, entry.Task.Label ?? entry.Id.ToString() ?? "", pair.Value.Center, Math.Max(0, pair.Value.Width - 4)); }
    protected override void ClearScheduleGeometry() { _bars.Clear(); _dependencies = []; _byId.Clear(); _indexedSnapshot = null; }
}
