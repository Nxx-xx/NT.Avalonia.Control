using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>Source-preserving hierarchical data, navigation and raster export shared by the hierarchy chart family.</summary>
public abstract partial class HierarchyChartBase : EngineeringChartBase
{
    public static readonly StyledProperty<string?> ChildrenPathProperty = AvaloniaProperty.Register<HierarchyChartBase, string?>(nameof(ChildrenPath), "Children");
    public static readonly StyledProperty<string?> ValuePathProperty = AvaloniaProperty.Register<HierarchyChartBase, string?>(nameof(ValuePath), "Value");
    public static readonly StyledProperty<bool> ShowLabelsProperty = AvaloniaProperty.Register<HierarchyChartBase, bool>(nameof(ShowLabels), true);
    public static readonly StyledProperty<bool> ShowValuesProperty = AvaloniaProperty.Register<HierarchyChartBase, bool>(nameof(ShowValues));
    public static readonly StyledProperty<double> GapProperty = AvaloniaProperty.Register<HierarchyChartBase, double>(nameof(Gap), 3, validate: x => double.IsFinite(x) && x >= 0 && x <= 4096);
    public static readonly StyledProperty<int> MaximumVisibleDepthProperty = AvaloniaProperty.Register<HierarchyChartBase, int>(nameof(MaximumVisibleDepth), ChartHierarchy.MaximumDepth, validate: x => x >= 0 && x <= ChartHierarchy.MaximumDepth);
    private sealed class ObserverState { public bool PropertyAttached; public bool CollectionAttached; }
    private readonly Dictionary<object, ObserverState> _nested = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<object> _desiredObservers = new(ReferenceEqualityComparer.Instance);
    private bool _syncingObservers, _resyncObservers;
    private readonly HashSet<NodeIdentity> _collapsed = [];
    private readonly Dictionary<EngineeringMarkInfo, Glyph> _displayed = new(ReferenceEqualityComparer.Instance);
    private readonly ConditionalWeakTable<EngineeringMarkInfo, HierarchyEntry> _markEntries = new();
    private readonly Dictionary<NodeIdentity, int> _markIndices = [];
    private readonly List<(Geometry Geometry, IBrush Fill)> _exits = [];
    private HierarchySnapshot? _snapshot;
    private NodeIdentity? _drill;
    private IReadOnlyList<HierarchyEntry> _drillPath = [];
    private bool _hierarchyAttached, _dataDirty = true, _viewDirty = true;
    private IReadOnlyList<Glyph> _frame = [];
    private Rect _framePlot;
    private readonly HashSet<int> _displayParents = [];
    private double _displayZoom = 1, _displayPanX, _displayPanY;
    private long _dataRevision;
    protected HierarchyChartBase() { LabelPath = "Label"; ShowGrid = false; ObservePaths(ValuePath, ChildrenPath); }
    protected override int SourceItemLimit => ChartHierarchy.MaximumCount;
    /// <summary>Unweighted tree families may represent absent numeric values as zero. Weighted families retain strict missing-value rejection.</summary>
    protected virtual bool AllowMissingHierarchyValues => false;
    protected virtual double GetHierarchyValue(HierarchyEntry entry) => entry.AggregateValue;
    protected virtual string GetHierarchyTooltip(HierarchyEntry entry) => $"{entry.Label}: {F(entry.AggregateValue)}; depth {entry.Depth}; path {string.Join('/', entry.Path)}";
    public string? ChildrenPath { get => GetValue(ChildrenPathProperty); set => SetValue(ChildrenPathProperty, value); }
    public string? ValuePath { get => GetValue(ValuePathProperty); set => SetValue(ValuePathProperty, value); }
    public bool ShowLabels { get => GetValue(ShowLabelsProperty); set => SetValue(ShowLabelsProperty, value); }
    public bool ShowValues { get => GetValue(ShowValuesProperty); set => SetValue(ShowValuesProperty, value); }
    /// <summary>Visual spacing in DIPs. Set to zero to retain the numerical layout's full painted areas.</summary>
    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }
    /// <summary>Maximum depth relative to the current drill root; deeper branches remain aggregated and selectable.</summary>
    public int MaximumVisibleDepth { get => GetValue(MaximumVisibleDepthProperty); set => SetValue(MaximumVisibleDepthProperty, value); }
    public HierarchySnapshot? Snapshot { get { _ = GetMarks(); return _snapshot; } }
    public object? DrillRootId { get { _ = GetMarks(); return _drillPath.LastOrDefault() is { } entry ? entry.Id ?? entry.Source : null; } }
    public IReadOnlyList<HierarchyEntry> DrillPath { get { _ = GetMarks(); return _drillPath; } }
    /// <summary>Only explicit IDs compare by value. Sources without IDs retain reference identity across edits and reorders.</summary>
    private sealed class NodeIdentity(object? value, bool byValue) : IEquatable<NodeIdentity>
    {
        public bool Equals(NodeIdentity? other) => other is not null && byValue == other.ByValue && (byValue ? Equals(value, other.Value) : ReferenceEquals(value, other.Value));
        public override bool Equals(object? obj) => obj is NodeIdentity other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(byValue, value is null ? 0 : byValue ? value.GetHashCode() : RuntimeHelpers.GetHashCode(value));
        private object? Value => value;
        private bool ByValue => byValue;
    }
    private static NodeIdentity Identity(HierarchyEntry entry) => new(entry.Id ?? entry.Source, entry.Id is not null);
    private HierarchyEntry? Find(object identity) => _snapshot?.Nodes.FirstOrDefault(entry => entry.Id is not null && Equals(entry.Id, identity) || ReferenceEquals(entry.Source, identity));
    public bool IsExpanded(object idOrSource) { _ = GetMarks(); return Find(idOrSource) is { } entry && !_collapsed.Contains(Identity(entry)); }
    public void Collapse(object idOrSource) => SetExpanded(idOrSource, false);
    public void Expand(object idOrSource) => SetExpanded(idOrSource, true);
    public void ToggleExpanded(object idOrSource) => SetExpanded(idOrSource, !IsExpanded(idOrSource));
    private void SetExpanded(object identity, bool expanded)
    {
        VerifyAccess(); _ = GetMarks(); if (Find(identity) is not { Children.Count: > 0 } entry) return;
        var changed = expanded ? _collapsed.Remove(Identity(entry)) : _collapsed.Add(Identity(entry));
        if (changed) InvalidateHierarchyView();
    }
    /// <summary>Drills to a branch by its explicit ID or original source reference. Leaves are not drill roots.</summary>
    public bool DrillDown(object idOrSource)
    {
        VerifyAccess(); _ = GetMarks(); if (Find(idOrSource) is not { Children.Count: > 0 } entry || Identity(entry).Equals(_drill)) return false;
        if (_drillPath.LastOrDefault() is { } current && !entry.Path.Take(current.Path.Count).SequenceEqual(current.Path)) return false;
        _drill = Identity(entry); InvalidateHierarchyView(); return true;
    }
    public bool DrillUp()
    {
        VerifyAccess(); _ = GetMarks(); if (_drillPath.LastOrDefault() is not { } entry) return false;
        _drill = entry.ParentIndex < 0 ? null : Identity(_snapshot!.Nodes[entry.ParentIndex]); InvalidateHierarchyView(); return true;
    }
    public void ResetDrill() { VerifyAccess(); if (_drill is null) return; _drill = null; InvalidateHierarchyView(); }
    protected void InvalidateHierarchyView() { _viewDirty = true; _pendingTrigger |= ChartAnimationTriggers.Viewport; InvalidateEngineering(); }
    protected override void OnDataInvalidated() { ++_dataRevision; _dataDirty = true; _pendingTrigger |= ChartAnimationTriggers.Data; }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (AnimationPropertyChanged(change)) return;
        if (change.Property == ActivePointProperty || change.Property == SelectedPointProperty || change.Property == DataErrorProperty) { base.OnPropertyChanged(change); return; }
        if (change.Property == SelectedIndexProperty) { base.OnPropertyChanged(change); return; }
        if (change.Property == ItemsSourceProperty) ReleaseObservers();
        if (change.Property == ChildrenPathProperty || change.Property == ValuePathProperty || change.Property == GroupPathProperty)
        { OnDataInvalidated(); ObservePaths(ValuePath, ChildrenPath, GroupPath); }
        // Rebuild family layout only for actual layout inputs. Inherited theme,
        // interaction and presenter properties can change paint without replacing
        // a running or paused geometric transition. A plot-size change is detected
        // separately when the next real frame is built.
        if (change.Property == GapProperty || change.Property == MaximumVisibleDepthProperty || change.Property == ZoomProperty || change.Property == PanXProperty || change.Property == PanYProperty ||
            change.Property.OwnerType != typeof(HierarchyChartBase) && typeof(HierarchyChartBase).IsAssignableFrom(change.Property.OwnerType))
        { _viewDirty = true; _pendingTrigger |= ChartAnimationTriggers.Viewport; }
        base.OnPropertyChanged(change);
    }
    private HierarchySnapshot ReadHierarchy(ChartDataSnapshot data)
    {
        var owners = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var nodes = new List<HierarchyNode>(); var count = 0;
        try
        {
            for (var i = 0; i < data.Count; i++) nodes.Add(ReadNode(data.Points[i].Item, 0));
            var snapshot = ChartHierarchy.Snapshot(nodes);
            var identities = new HashSet<NodeIdentity>();
            foreach (var entry in snapshot.Nodes) if (!identities.Add(Identity(entry))) throw new ArgumentException("Hierarchy node IDs or source instances must be unique.", nameof(ItemsSource));
            return snapshot;
        }
        finally { ObserveNested(owners); }

        HierarchyNode ReadNode(object? item, int depth)
        {
            if (++count > ChartHierarchy.MaximumCount) throw new ArgumentOutOfRangeException(nameof(ItemsSource), "Hierarchy node budget exceeded.");
            if (depth > ChartHierarchy.MaximumDepth) throw new ArgumentOutOfRangeException(nameof(ItemsSource), "Hierarchy depth budget exceeded.");
            if (item is not null && !item.GetType().IsValueType && !seen.Add(item)) throw new ArgumentException("Hierarchy contains a cycle or repeated node source.", nameof(ItemsSource));
            foreach (var path in new[] { ValuePath, ChildrenPath, LabelPath, IdPath, GroupPath }) foreach (var owner in ChartDataAccessor.Owners(item, path)) owners.Add(owner);
            var typed = item as HierarchyNode;
            object? Read(string? path) => !string.IsNullOrWhiteSpace(path) && ChartDataAccessor.TryRead(item, path, out var raw) ? raw : null;
            var childrenSource = typed is not null && ChildrenPath == "Children" ? typed.Children : Read(ChildrenPath);
            if (childrenSource is not null) owners.Add(childrenSource);
            var children = new List<HierarchyNode>();
            if (childrenSource is not null && childrenSource is not IEnumerable || childrenSource is string) throw new ArgumentException("ChildrenPath must resolve to a collection.", nameof(ChildrenPath));
            if (childrenSource is IEnumerable sequence)
                foreach (var child in sequence) children.Add(ReadNode(child, depth + 1));
            var rawValue = typed is not null && ValuePath == "Value" ? typed.Value : Read(ValuePath);
            var value = ChartDataAccessor.TryNumber(rawValue, out var number) ? number : rawValue is null && children.Count > 0 ? 0 : rawValue is null && ChartDataAccessor.TryNumber(item, out number) ? number : rawValue is null && AllowMissingHierarchyValues ? 0 : double.NaN;
            return new(value, children, Read(LabelPath)?.ToString() ?? typed?.Label, typed?.Source ?? item,
                !string.IsNullOrWhiteSpace(IdPath) ? Read(IdPath) : typed?.Id);
        }
    }
    private void ObserveNested(HashSet<object> desired)
    {
        _desiredObservers.Clear(); if (_hierarchyAttached) foreach (var owner in desired) if (owner is INotifyPropertyChanged or INotifyCollectionChanged) _desiredObservers.Add(owner);
        SynchronizeObservers();
    }
    private void NestedChanged(object? sender, PropertyChangedEventArgs e) => Refresh();
    private void NestedCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refresh();
    private void SynchronizeObservers()
    {
        _resyncObservers = true; if (_syncingObservers) return; _syncingObservers = true;
        try
        {
            while (_resyncObservers)
            {
                _resyncObservers = false;
                foreach (var (owner, state) in _nested.ToArray())
                {
                    if (_desiredObservers.Contains(owner)) continue;
                    if (state.PropertyAttached && owner is INotifyPropertyChanged property) { state.PropertyAttached = false; property.PropertyChanged -= NestedChanged; }
                    if (_resyncObservers) break;
                    if (state.CollectionAttached && owner is INotifyCollectionChanged collection) { state.CollectionAttached = false; collection.CollectionChanged -= NestedCollectionChanged; }
                    if (_resyncObservers) break;
                    _nested.Remove(owner);
                }
                if (_resyncObservers) continue;
                foreach (var owner in _desiredObservers.ToArray())
                {
                    if (!_nested.TryGetValue(owner, out var state)) { state = new(); _nested.Add(owner, state); }
                    // Record intent before calling application event accessors. A reentrant source replacement
                    // changes the desired set and the next synchronization pass releases this subscription.
                    if (!state.PropertyAttached && owner is INotifyPropertyChanged property) { state.PropertyAttached = true; property.PropertyChanged += NestedChanged; }
                    if (_resyncObservers) break;
                    if (!state.CollectionAttached && owner is INotifyCollectionChanged collection) { state.CollectionAttached = true; collection.CollectionChanged += NestedCollectionChanged; }
                    if (_resyncObservers) break;
                }
            }
        }
        finally { _syncingObservers = false; }
    }
    private void ReleaseObservers() { _desiredObservers.Clear(); SynchronizeObservers(); }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) { _hierarchyAttached = true; _dataDirty = true; _entrancePending = true; base.OnAttachedToVisualTree(e); ObserveAnimationAncestors(); }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) { _hierarchyAttached = false; CancelAnimations(); ObserveAnimationAncestors(); ReleaseObservers(); _dataDirty = true; base.OnDetachedFromVisualTree(e); }
    protected override void ClearProjection(Exception error) { _snapshot = null; _frame = []; _displayed.Clear(); _markIndices.Clear(); _exits.Clear(); _drillPath = []; ClearLegend(); ReleaseAnimation(); SetAnimationStatus(ChartAnimationState.Idle, 1); }
    private int ResolveRoot()
    {
        if (_snapshot is null) return -1;
        var root = _drill is null ? null : _snapshot.Nodes.FirstOrDefault(entry => Identity(entry).Equals(_drill));
        if (root is null) { _drill = null; _drillPath = []; return -1; }
        var path = new List<HierarchyEntry>(); for (var current = root; current is not null; current = current.ParentIndex < 0 ? null : _snapshot.Nodes[current.ParentIndex]) path.Add(current);
        path.Reverse(); _drillPath = path.AsReadOnly(); return root.Index;
    }
    protected enum GlyphKind { Rectangle, Circle, Sector }
    protected sealed record Glyph(HierarchyEntry Entry, GlyphKind Kind, double X, double Y, double Width, double Height,
        double InnerRadius = 0, double StartAngle = 0, double SweepAngle = 0, bool IsExiting = false);
    protected IReadOnlyList<Glyph> DisplayedGlyphs => _frame;
    internal override (double Zoom, double PanX, double PanY) DisplayedPlotContentViewport => (_displayZoom, _displayPanX, _displayPanY);
    protected Rect DisplayedRectangle(Glyph glyph, Rect plot) => DisplayRect(glyph, plot);
    protected abstract IReadOnlyList<Glyph> BuildHierarchy(HierarchySnapshot snapshot, int rootIndex, IReadOnlySet<int> collapsed);
    protected static Glyph Rectangle(HierarchyRectangle rectangle) => new(rectangle.Entry, GlyphKind.Rectangle, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
    protected static Glyph Circle(HierarchyCircle circle) => new(circle.Entry, GlyphKind.Circle, circle.Center.X, circle.Center.Y, circle.Radius, circle.Radius);
    protected static Glyph Sector(HierarchySector sector) => new(sector.Entry, GlyphKind.Sector, .5, .5, sector.OuterRadius, sector.OuterRadius, sector.InnerRadius, sector.StartAngle, sector.SweepAngle);
    protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
    {
        if (_dataDirty || _snapshot is null)
        {
            var revision = _dataRevision; var candidate = ReadHierarchy(data);
            if (revision != _dataRevision) { _dataDirty = true; _viewDirty = true; InvalidateEngineering(); return []; }
            _snapshot = candidate; _collapsed.IntersectWith(candidate.Nodes.Select(Identity)); _dataDirty = false; _viewDirty = true;
        }
        var root = ResolveRoot();
        if (_viewDirty || _framePlot != plot)
        {
            if (_framePlot != plot) _pendingTrigger |= ChartAnimationTriggers.Viewport;
            var depth = root < 0 ? 0 : _snapshot.Nodes[root].Depth;
            var collapsed = _snapshot.Nodes.Where(entry => _collapsed.Contains(Identity(entry)) || entry.Depth - depth >= MaximumVisibleDepth).Select(entry => entry.Index).ToHashSet();
            var target = BuildHierarchy(_snapshot, root, collapsed); _viewDirty = false; _framePlot = plot;
            BeginTransition(target);
        }
        PrepareLegend();
        _displayed.Clear(); _markIndices.Clear(); _exits.Clear(); _displayParents.Clear();
        var marks = new List<EngineeringMarkInfo>();
        foreach (var glyph in _frame)
        {
            if (glyph.Width <= 0 || glyph.Height <= 0 || glyph.Kind == GlyphKind.Sector && glyph.SweepAngle <= 0) continue;
            // Arc bounds in Headless can contain only their endpoints. Sector drawability and
            // exact hit testing come from the actual annular primitive, never that backend bound.
            if (glyph.Kind == GlyphKind.Sector) { var circle = DisplayCircle(glyph, plot); if (circle.Outer <= circle.Inner) continue; }
            var geometry = GeometryFor(glyph, plot); if (glyph.Kind != GlyphKind.Sector && (geometry.Bounds.Width <= 0 || geometry.Bounds.Height <= 0)) continue;
            var entry = glyph.Entry; var point = PositionFor(glyph, plot);
            var source = new ChartDataPoint(entry.SourceIndex, entry.Id ?? entry.Source, entry.Source, entry.Label, new(entry.Index, entry.Value), false) { Value = entry.Value, Weight = GetHierarchyValue(entry) };
            var fill = EntryBrush(entry);
            if (glyph.IsExiting)
            {
                if (fill is ISolidColorBrush solid) fill = new SolidColorBrush(solid.Color, solid.Opacity * (1 - _animationProgress));
                _exits.Add((geometry, fill)); continue;
            }
            var mark = new EngineeringMarkInfo(Array.AsReadOnly(new[] { source }), geometry, fill, point)
            { Key = entry.Id ?? entry.Source, Value = GetHierarchyValue(entry), TooltipText = GetHierarchyTooltip(entry), Stroke = Background ?? Brushes.White };
            if (!glyph.IsExiting) _markIndices.Add(Identity(entry), marks.Count);
            _displayParents.Add(entry.ParentIndex);
            marks.Add(mark); _displayed.Add(mark, glyph); _markEntries.Add(mark, entry);
        }
        return marks;
    }
    protected override EngineeringPointHit? FindFollowedMark(EngineeringPointHit previous, IReadOnlyList<EngineeringMarkInfo> marks)
    {
        if (previous.Index >= 0 && previous.Index < marks.Count && ReferenceEquals(previous.Mark, marks[previous.Index])) return previous;
        return _markEntries.TryGetValue(previous.Mark, out var entry) && _markIndices.TryGetValue(Identity(entry), out var index) ? new(index, marks[index]) : null;
    }
    private Rect DisplayRect(Glyph glyph, Rect plot)
    {
        var left = DisplayViewport(new(plot.Left + glyph.X * plot.Width, plot.Top + glyph.Y * plot.Height), plot);
        var width = glyph.Width * plot.Width * _displayZoom; var height = glyph.Height * plot.Height * _displayZoom;
        // Increasing nested insets leave each ancestor an exposed border even for a single-child branch.
        var depthSpacing = this is TreemapChart ? Math.Max(1, glyph.Entry.Depth - (_drillPath.LastOrDefault()?.Depth ?? 0) + 1) : 1;
        var gap = Math.Min(Gap * _displayZoom * depthSpacing, Math.Min(width, height));
        return new(left.X + gap / 2, left.Y + gap / 2, Math.Max(0, width - gap), Math.Max(0, height - gap));
    }
    private (Point Center, double Inner, double Outer) DisplayCircle(Glyph glyph, Rect plot)
    {
        var square = Square(plot); var center = DisplayViewport(new(square.Left + glyph.X * square.Width, square.Top + glyph.Y * square.Height), plot);
        var outer = glyph.Width * square.Width * _displayZoom; var rawInner = glyph.InnerRadius * square.Width * _displayZoom;
        var gap = Math.Min(Gap * _displayZoom / 2, outer * .2);
        // Numeric nested packs reserve six percent parent whitespace. Keep the parent inset
        // below that margin so every displayed child remains inside its displayed parent.
        if (this is CirclePackingChart && glyph.Entry.Children.Count > 0) gap = Math.Min(gap, outer * .03);
        if (glyph.Kind == GlyphKind.Sector) gap = Math.Min(gap, Math.Max(0, (outer - rawInner) / 4));
        return (center, rawInner == 0 ? 0 : rawInner + gap, Math.Max(0, outer - gap));
    }
    protected Point DisplayViewport(Point point, Rect plot) => new(plot.Center.X + (point.X - plot.Center.X) * _displayZoom + _displayPanX * plot.Width, plot.Center.Y + (point.Y - plot.Center.Y) * _displayZoom + _displayPanY * plot.Height);
    private Geometry GeometryFor(Glyph glyph, Rect plot)
    {
        if (glyph.Kind == GlyphKind.Rectangle) return new RectangleGeometry(DisplayRect(glyph, plot));
        var (center, inner, outer) = DisplayCircle(glyph, plot);
        if (glyph.Kind == GlyphKind.Circle) return new EllipseGeometry(new Rect(center.X - outer, center.Y - outer, outer * 2, outer * 2));
        if (glyph.SweepAngle >= 360)
        {
            Geometry disk = new EllipseGeometry(new Rect(center.X - outer, center.Y - outer, outer * 2, outer * 2));
            return inner <= 0 ? disk : new CombinedGeometry(GeometryCombineMode.Exclude, disk, new EllipseGeometry(new Rect(center.X - inner, center.Y - inner, inner * 2, inner * 2)));
        }
        var geometry = new StreamGeometry(); using var path = geometry.Open();
        Point At(double angle, double radius) { var radians = angle * Math.PI / 180; return new(center.X + Math.Cos(radians) * radius, center.Y + Math.Sin(radians) * radius); }
        path.BeginFigure(At(glyph.StartAngle, outer), true);
        Arc(glyph.StartAngle, glyph.SweepAngle, outer, SweepDirection.Clockwise);
        path.LineTo(At(glyph.StartAngle + glyph.SweepAngle, inner));
        if (inner > 0) Arc(glyph.StartAngle + glyph.SweepAngle, -glyph.SweepAngle, inner, SweepDirection.CounterClockwise);
        else path.LineTo(center);
        path.EndFigure(true); return geometry;
        void Arc(double start, double sweep, double radius, SweepDirection direction)
        { var parts = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweep) / 180)); for (var i = 1; i <= parts; i++) path.ArcTo(At(start + sweep * i / parts, radius), new Size(radius, radius), 0, false, direction); }
    }
    private Point PositionFor(Glyph glyph, Rect plot)
    {
        if (glyph.Kind == GlyphKind.Rectangle) return DisplayRect(glyph, plot).Center;
        var (center, inner, outer) = DisplayCircle(glyph, plot); if (glyph.Kind == GlyphKind.Circle) return center;
        var angle = (glyph.StartAngle + glyph.SweepAngle / 2) * Math.PI / 180; var radius = (inner + outer) / 2;
        return new(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius);
    }
    protected override bool ContainsMark(EngineeringMarkInfo mark, Point point)
    {
        if (!_displayed.TryGetValue(mark, out var glyph) || glyph.IsExiting) return false;
        if (glyph.Kind == GlyphKind.Rectangle) return DisplayRect(glyph, _framePlot).Contains(point);
        var (center, inner, outer) = DisplayCircle(glyph, _framePlot); var dx = point.X - center.X; var dy = point.Y - center.Y; var radius = Math.Sqrt(dx * dx + dy * dy);
        if (radius > outer || radius < inner) return false;
        if (glyph.Kind == GlyphKind.Circle || glyph.SweepAngle >= 360) return true;
        var angle = Math.Atan2(dy, dx) * 180 / Math.PI; var offset = ((angle - glyph.StartAngle) % 360 + 360) % 360;
        return offset <= glyph.SweepAngle + 1e-10;
    }
    protected override void RenderAnnotations(DrawingContext context, Rect plot)
    {
        using (context.PushClip(plot)) foreach (var (geometry, fill) in _exits) context.DrawGeometry(fill, null, geometry);
        RenderLegend(context);
        if (UsesDataLabelTemplate || !ShowLabels && !ShowValues) return;
        using var clip = context.PushClip(plot);
        foreach (var (mark, glyph) in _displayed)
        {
            if (glyph.IsExiting || mark.Bounds.Width < FontSize * 1.5 || mark.Bounds.Height < FontSize + 4) continue;
            // Parent rectangles/circles share space with their descendants. Their identity remains in Tooltip,
            // drill breadcrumbs and selection; terminal labels avoid painting through child labels.
            if (SuppressCoveredAncestorLabels && glyph.Kind != GlyphKind.Sector && _displayParents.Contains(glyph.Entry.Index)) continue;
            var text = (ShowLabels ? glyph.Entry.Label : null) ?? ""; if (ShowValues) text += $"{(text.Length > 0 ? " " : "")}{F(glyph.Entry.AggregateValue)}";
            using var nodeClip = context.PushGeometryClip(mark.Geometry); DrawLabel(context, text, mark.Position, Math.Max(0, mark.Bounds.Width - 6));
        }
    }
    protected virtual bool SuppressCoveredAncestorLabels => true;
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (e.ClickCount >= 2 && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && HitTest(e.GetPosition(this)) is { } hit && hit.Mark.Key is { } key && DrillDown(key)) { e.Handled = true; return; }
        base.OnPointerPressed(e);
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        var key = (ActivePoint ?? SelectedPoint)?.Mark.Key;
        if (e.Key == Key.Back && DrillUp()) { e.Handled = true; return; }
        if (e.Key == Key.Enter && key is not null && DrillDown(key)) { e.Handled = true; return; }
        if (e.Key is Key.Add or Key.OemPlus && key is not null) { Expand(key); e.Handled = true; return; }
        if (e.Key is Key.Subtract or Key.OemMinus && key is not null) { Collapse(key); e.Handled = true; return; }
        if (e.Key == Key.Escape) ResetDrill(); base.OnKeyDown(e);
    }
}
