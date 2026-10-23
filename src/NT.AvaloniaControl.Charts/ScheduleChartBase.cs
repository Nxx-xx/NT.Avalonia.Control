using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>Observable scheduling paths, immutable source projection and source-preserving selection.</summary>
public abstract partial class ScheduleChartBase : EngineeringChartBase, IDisposable
{
    public static readonly StyledProperty<string?> StartPathProperty = AvaloniaProperty.Register<ScheduleChartBase, string?>(nameof(StartPath), "Start");
    public static readonly StyledProperty<string?> EndPathProperty = AvaloniaProperty.Register<ScheduleChartBase, string?>(nameof(EndPath), "End");
    public static readonly StyledProperty<string?> ProgressPathProperty = AvaloniaProperty.Register<ScheduleChartBase, string?>(nameof(ProgressPath), "Progress");
    public static readonly StyledProperty<string?> DependenciesPathProperty = AvaloniaProperty.Register<ScheduleChartBase, string?>(nameof(DependenciesPath), "Dependencies");
    public static readonly StyledProperty<string?> LanePathProperty = AvaloniaProperty.Register<ScheduleChartBase, string?>(nameof(LanePath), "Lane");
    public static readonly StyledProperty<string?> DatePathProperty = AvaloniaProperty.Register<ScheduleChartBase, string?>(nameof(DatePath), "Date");
    public static readonly StyledProperty<string?> ValuePathProperty = AvaloniaProperty.Register<ScheduleChartBase, string?>(nameof(ValuePath), "Value");
    public static readonly StyledProperty<string?> DescriptionPathProperty = AvaloniaProperty.Register<ScheduleChartBase, string?>(nameof(DescriptionPath), "Description");
    public static readonly StyledProperty<string?> BrushPathProperty = AvaloniaProperty.Register<ScheduleChartBase, string?>(nameof(BrushPath), "Brush");
    public static readonly StyledProperty<string?> TaskNamePathProperty = LabelPathProperty;
    public static readonly StyledProperty<bool> ShowLabelsProperty = AvaloniaProperty.Register<ScheduleChartBase, bool>(nameof(ShowLabels), true);
    public static readonly StyledProperty<string?> LaneFilterProperty = AvaloniaProperty.Register<ScheduleChartBase, string?>(nameof(LaneFilter));
    public static readonly StyledProperty<TimeZoneInfo> InputTimeZoneProperty = AvaloniaProperty.Register<ScheduleChartBase, TimeZoneInfo>(nameof(InputTimeZone), TimeZoneInfo.Utc, validate: v => v is not null);
    public static readonly StyledProperty<ScheduleAmbiguousTime> AmbiguousTimeProperty = AvaloniaProperty.Register<ScheduleChartBase, ScheduleAmbiguousTime>(nameof(AmbiguousTime), ScheduleAmbiguousTime.Reject, validate: Enum.IsDefined);
    public static readonly StyledProperty<DateTimeOffset?> VisibleStartProperty = AvaloniaProperty.Register<ScheduleChartBase, DateTimeOffset?>(nameof(VisibleStart));
    public static readonly StyledProperty<DateTimeOffset?> VisibleEndProperty = AvaloniaProperty.Register<ScheduleChartBase, DateTimeOffset?>(nameof(VisibleEnd));
    private sealed class ObserverState { public bool PropertyAttached; public bool CollectionAttached; }
    private readonly Dictionary<object, ObserverState> _observed = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<object> _desired = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, IBrush?> _brushes = [];
    private bool _synchronizing, _resynchronize, _attached, _disposed, _dataDirty = true;
    private long _dataRevision;
    private ScheduleSnapshot? _snapshot;
    private ScheduleSpanSnapshot? _spanSnapshot;
    private ScheduleSwimlaneLayout? _swimlaneLayout;
    private IReadOnlyList<ScheduleEventEntry> _events = [];
    protected enum SchedulingKind { Gantt, Swimlane, Event, Spiral }
    protected abstract SchedulingKind Kind { get; }
    protected ScheduleChartBase() { IdPath = "Id"; LabelPath = "Label"; ShowGrid = true; }
    protected override int SourceItemLimit => ChartSchedule.MaximumItemCount;
    public string? StartPath { get => GetValue(StartPathProperty); set => SetValue(StartPathProperty, value); }
    public string? EndPath { get => GetValue(EndPathProperty); set => SetValue(EndPathProperty, value); }
    public string? ProgressPath { get => GetValue(ProgressPathProperty); set => SetValue(ProgressPathProperty, value); }
    public string? DependenciesPath { get => GetValue(DependenciesPathProperty); set => SetValue(DependenciesPathProperty, value); }
    public string? LanePath { get => GetValue(LanePathProperty); set => SetValue(LanePathProperty, value); }
    public string? DatePath { get => GetValue(DatePathProperty); set => SetValue(DatePathProperty, value); }
    public string? ValuePath { get => GetValue(ValuePathProperty); set => SetValue(ValuePathProperty, value); }
    public string? DescriptionPath { get => GetValue(DescriptionPathProperty); set => SetValue(DescriptionPathProperty, value); }
    public string? BrushPath { get => GetValue(BrushPathProperty); set => SetValue(BrushPathProperty, value); }
    /// <summary>Alias of LabelPath for task charts.</summary>
    public string? TaskNamePath { get => LabelPath; set => LabelPath = value; }
    public bool ShowLabels { get => GetValue(ShowLabelsProperty); set => SetValue(ShowLabelsProperty, value); }
    /// <summary>Optional ordinal lane/resource filter for task and span views; source indices and row positions remain unchanged.</summary>
    public string? LaneFilter { get => GetValue(LaneFilterProperty); set => SetValue(LaneFilterProperty, value); }
    /// <summary>Time zone for explicitly unspecified DateTime wall times. DateTimeOffset and UTC DateTime are absolute.</summary>
    public TimeZoneInfo InputTimeZone { get => GetValue(InputTimeZoneProperty); set => SetValue(InputTimeZoneProperty, value); }
    public ScheduleAmbiguousTime AmbiguousTime { get => GetValue(AmbiguousTimeProperty); set => SetValue(AmbiguousTimeProperty, value); }
    public DateTimeOffset? VisibleStart { get => GetValue(VisibleStartProperty); set => SetValue(VisibleStartProperty, value); }
    public DateTimeOffset? VisibleEnd { get => GetValue(VisibleEndProperty); set => SetValue(VisibleEndProperty, value); }
    public ScheduleSnapshot? Snapshot { get { _ = GetMarks(); return _snapshot; } }
    public ScheduleSpanSnapshot? SpanSnapshot { get { _ = GetMarks(); return _spanSnapshot; } }
    public ScheduleSwimlaneLayout? SwimlaneLayout { get { _ = GetMarks(); return _swimlaneLayout; } }
    public IReadOnlyList<ScheduleEventEntry> Events { get { _ = GetMarks(); return _events; } }
    public int LastQueryVisitedNodes { get; protected set; }
    protected ScheduleSnapshot? TaskData => _snapshot;
    protected ScheduleSpanSnapshot? SpanData => _spanSnapshot;
    protected ScheduleSwimlaneLayout? LaneData => _swimlaneLayout;
    protected IReadOnlyList<ScheduleEventEntry> EventData => _events;
    protected override ChartDataSnapshot CreateEngineeringSnapshot() => new(0, Array.Empty<ChartDataPoint>());
    protected override void OnDataInvalidated() { _dataDirty = true; _dataRevision++; }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == ItemsSourceProperty) ReleaseObservers();
        if (change.Property == StartPathProperty || change.Property == EndPathProperty || change.Property == ProgressPathProperty || change.Property == DependenciesPathProperty || change.Property == LanePathProperty || change.Property == DatePathProperty || change.Property == ValuePathProperty || change.Property == DescriptionPathProperty || change.Property == BrushPathProperty || change.Property == InputTimeZoneProperty || change.Property == AmbiguousTimeProperty) OnDataInvalidated();
        base.OnPropertyChanged(change);
    }
    protected void EnsureScheduleData()
    {
        if (_disposed) { _snapshot = null; _spanSnapshot = null; _swimlaneLayout = null; _events = []; return; }
        if (!_dataDirty) return;
        var revision = _dataRevision; var owners = new HashSet<object>(ReferenceEqualityComparer.Instance); var tasks = new List<ScheduleTask>(); var spans = new List<ScheduleSpan>(); var events = new List<ScheduleEvent>(); var brushes = new Dictionary<object, IBrush?>(); var dependencyCount = 0;
        try
        {
            if (ItemsSource is { } source)
            {
                owners.Add(source); var count = 0;
                foreach (var item in source)
                {
                    ChartSchedule.Limit(count++); if (item is null) throw new ArgumentException("Scheduling source rows cannot be null.", nameof(ItemsSource));
                    foreach (var path in Paths()) foreach (var owner in ChartDataAccessor.Owners(item, path)) owners.Add(owner);
                    object? Read(string? path) => !string.IsNullOrWhiteSpace(path) && ChartDataAccessor.TryRead(item, path, out var value) ? value : null;
                    double Numeric(string? path, double fallback) => Read(path) is { } value ? value is not DateTime and not DateTimeOffset && ChartDataAccessor.TryNumber(value, out var number) ? number : throw new ArgumentException($"{path} must be numeric; date scales need an explicit caller conversion.") : fallback;
                    var id = item switch { ScheduleTask task => task.Id, ScheduleSpan span => span.Id, ScheduleEvent ev => ev.Id, _ => Read(IdPath) ?? throw new ArgumentException("Scheduling rows require a unique ID; configure IdPath.") };
                    var label = Read(LabelPath)?.ToString();
                    if (Kind == SchedulingKind.Gantt)
                    {
                        if (item is ScheduleTask typed) tasks.Add(typed);
                        else
                        {
                            var dependencies = new List<ScheduleDependency>(); var raw = Read(DependenciesPath);
                            if (raw is not null && (raw is not IEnumerable || raw is string)) throw new ArgumentException("DependenciesPath must be a sequence of ScheduleDependency.");
                            if (raw is IEnumerable sequence) { owners.Add(raw); foreach (var dependency in sequence) { if (++dependencyCount > ChartSchedule.MaximumDependencyCount) throw new ArgumentOutOfRangeException(nameof(DependenciesPath)); if (dependency is not ScheduleDependency link) throw new ArgumentException("Dependencies must be ScheduleDependency values."); dependencies.Add(link); } }
                            tasks.Add(new(id, Date(Read(StartPath)), Date(Read(EndPath)), Numeric(ProgressPath, 0), Read(LanePath)?.ToString(), label, item, dependencies.AsReadOnly()));
                        }
                    }
                    else if (Kind == SchedulingKind.Swimlane) spans.Add(item is ScheduleSpan typed ? typed : new(id, Numeric(StartPath, double.NaN), Numeric(EndPath, double.NaN), Read(LanePath)?.ToString(), label, item));
                    else events.Add(item is ScheduleEvent typed ? typed : new(id, Date(Read(DatePath)), label, Numeric(ValuePath, 1), item, Read(DescriptionPath)?.ToString()));
                    var rawBrush = Read(BrushPath); try { brushes[id] = rawBrush switch { null => null, IBrush brush => brush, Color color => new SolidColorBrush(color), string text => Brush.Parse(text), _ => throw new ArgumentException("BrushPath must resolve to an IBrush, Color or color string.") }; } catch (FormatException error) { throw new ArgumentException("BrushPath contains an invalid color.", nameof(BrushPath), error); }
                }
            }
            if (revision != _dataRevision) throw new ArgumentException("Scheduling source changed while its snapshot was being read; retry with the current source.");
            _snapshot = Kind == SchedulingKind.Gantt ? ChartSchedule.Snapshot(tasks) : null;
            _spanSnapshot = Kind == SchedulingKind.Swimlane ? ChartSchedule.Spans(spans) : null;
            _swimlaneLayout = _spanSnapshot is null ? null : ChartSchedule.Swimlanes(_spanSnapshot);
            _events = Kind is SchedulingKind.Event or SchedulingKind.Spiral ? ChartSchedule.Events(events) : [];
            _brushes.Clear(); foreach (var pair in brushes) _brushes.Add(pair.Key, pair.Value); _dataDirty = false;
        }
        finally { ObserveOwners(revision == _dataRevision ? owners : new(ReferenceEqualityComparer.Instance)); }
    }
    private IEnumerable<string?> Paths() => [IdPath, LabelPath, StartPath, EndPath, ProgressPath, DependenciesPath, LanePath, DatePath, ValuePath, DescriptionPath, BrushPath];
    private DateTimeOffset Date(object? value) => value switch
    { DateTimeOffset offset => offset.ToUniversalTime(), DateTime date => ChartSchedule.ResolveLocal(date, InputTimeZone, AmbiguousTime), _ => throw new ArgumentException("Date paths must resolve to DateTimeOffset or DateTime; strings and numeric timestamps require an explicit caller conversion.") };
    protected IBrush ItemBrush(object id, int color = 0, IBrush? fallback = null) => _brushes.GetValueOrDefault(id) ?? fallback ?? BrushAt(color);
    protected override void ClearProjection(Exception error) { _dataDirty = true; _snapshot = null; _spanSnapshot = null; _swimlaneLayout = null; _events = []; _brushes.Clear(); LastQueryVisitedNodes = 0; ClearScheduleGeometry(); }
    protected virtual void ClearScheduleGeometry() { }
    public bool SelectItem(object idOrSource)
    {
        VerifyAccess(); var marks = GetMarks();
        for (var i = 0; i < marks.Count; i++) if (Equals(marks[i].Key, idOrSource) || marks[i].Members.Any(m => ReferenceEquals(m.Item, idOrSource))) { SetCurrentValue(SelectedIndexProperty, i); return true; }
        SetCurrentValue(SelectedIndexProperty, -1); return false;
    }
    protected static ChartDataPoint Member(int index, object id, object source, string? label, double x, double value) => new(index, id, source, label, new(x, value), false) { Value = value };
    protected (DateTimeOffset Start, DateTimeOffset End) TimeBounds(DateTimeOffset? minimum, DateTimeOffset? maximum)
    {
        var start = (VisibleStart ?? minimum ?? DateTimeOffset.UnixEpoch).ToUniversalTime(); var end = (VisibleEnd ?? maximum ?? new DateTimeOffset(Math.Min(DateTimeOffset.MaxValue.UtcTicks, start.UtcTicks + TimeSpan.TicksPerDay), TimeSpan.Zero)).ToUniversalTime();
        if (end < start) throw new ArgumentException("VisibleEnd must not precede VisibleStart.");
        if (start == end) { var ticks = TimeSpan.TicksPerDay; start = new(Math.Max(0, start.UtcTicks - ticks), TimeSpan.Zero); end = new(Math.Min(DateTimeOffset.MaxValue.UtcTicks, end.UtcTicks + ticks), TimeSpan.Zero); }
        return (start, end);
    }
    protected static double TimeFraction(DateTimeOffset date, DateTimeOffset start, DateTimeOffset end) => (date.UtcTicks - start.UtcTicks) / (double)(end.UtcTicks - start.UtcTicks);
    protected (DateTimeOffset Start, DateTimeOffset End) VisibleTimeBounds((DateTimeOffset Start, DateTimeOffset End) range, Rect plot)
    {
        var left = RemoveViewport(plot.TopLeft, plot); var right = RemoveViewport(plot.TopRight, plot); var span = range.End.UtcTicks - range.Start.UtcTicks;
        DateTimeOffset At(double fraction) => new((long)Math.Clamp((decimal)range.Start.UtcTicks + (decimal)(Math.Clamp(fraction, -1e6, 1e6) * span), 0, DateTimeOffset.MaxValue.UtcTicks), TimeSpan.Zero);
        return (At(plot.Width <= 0 ? 0 : (left.X - plot.Left) / plot.Width), At(plot.Width <= 0 ? 1 : (right.X - plot.Left) / plot.Width));
    }
    private void ObserveOwners(HashSet<object> owners) { _desired.Clear(); if (_attached && !_disposed) foreach (var owner in owners) if (owner is INotifyPropertyChanged or INotifyCollectionChanged) _desired.Add(owner); SynchronizeObservers(); }
    private void Changed(object? sender, PropertyChangedEventArgs e) => Refresh();
    private void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refresh();
    private void ReleaseObservers() { _desired.Clear(); SynchronizeObservers(); }
    private void SynchronizeObservers()
    {
        _resynchronize = true; if (_synchronizing) return; _synchronizing = true;
        try
        {
            while (_resynchronize)
            {
                _resynchronize = false;
                foreach (var (owner, state) in _observed.ToArray())
                {
                    if (_desired.Contains(owner)) continue;
                    if (state.PropertyAttached && owner is INotifyPropertyChanged property) { state.PropertyAttached = false; property.PropertyChanged -= Changed; }
                    if (_resynchronize) break;
                    if (state.CollectionAttached && owner is INotifyCollectionChanged collection) { state.CollectionAttached = false; collection.CollectionChanged -= CollectionChanged; }
                    if (_resynchronize) break; _observed.Remove(owner);
                }
                if (_resynchronize) continue;
                foreach (var owner in _desired.ToArray())
                {
                    if (!_observed.TryGetValue(owner, out var state)) _observed.Add(owner, state = new());
                    if (!state.PropertyAttached && owner is INotifyPropertyChanged property) { state.PropertyAttached = true; property.PropertyChanged += Changed; }
                    if (_resynchronize) break;
                    if (!state.CollectionAttached && owner is INotifyCollectionChanged collection) { state.CollectionAttached = true; collection.CollectionChanged += CollectionChanged; }
                    if (_resynchronize) break;
                }
            }
        }
        finally { _synchronizing = false; }
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) { _attached = true; _dataDirty = true; base.OnAttachedToVisualTree(e); }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) { _attached = false; ReleaseObservers(); _dataDirty = true; base.OnDetachedFromVisualTree(e); }
    public void Dispose() { VerifyAccess(); if (_disposed) return; _disposed = true; ReleaseObservers(); SetCurrentValue(ItemsSourceProperty, null); _snapshot = null; _spanSnapshot = null; _swimlaneLayout = null; _events = []; _brushes.Clear(); ClearScheduleGeometry(); InvalidateEngineering(); GC.SuppressFinalize(this); }
}
