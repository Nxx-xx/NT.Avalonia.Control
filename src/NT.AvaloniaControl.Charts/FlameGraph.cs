using Avalonia;

namespace NT.AvaloniaControl.Charts;

/// <summary>Bottom-up stack cost bands. Inclusive branch values preserve self cost; they must cover child costs. Drill and collapse retain original entries.</summary>
public sealed class FlameGraph : HierarchyChartBase
{
    public static readonly StyledProperty<HierarchyStackValueMode> ValueModeProperty = AvaloniaProperty.Register<FlameGraph, HierarchyStackValueMode>(nameof(ValueMode), HierarchyStackValueMode.Inclusive, validate: Enum.IsDefined);
    private HierarchyRectangleLayout? _layout;
    private Dictionary<int, double> _self = [];
    public FlameGraph() { Gap = 1; }
    public HierarchyStackValueMode ValueMode { get => GetValue(ValueModeProperty); set => SetValue(ValueModeProperty, value); }
    public HierarchyRectangleLayout? Layout { get { _ = GetMarks(); return _layout; } }
    protected override bool SuppressCoveredAncestorLabels => false;
    protected override double GetHierarchyValue(HierarchyEntry entry) => ValueMode == HierarchyStackValueMode.Inclusive ? entry.Value : entry.AggregateValue;
    protected override string GetHierarchyTooltip(HierarchyEntry entry) => $"{entry.Label}: {F(GetHierarchyValue(entry))}; self {(ValueMode == HierarchyStackValueMode.Inclusive ? F(_self.GetValueOrDefault(entry.Index)) : "leaf total")}; depth {entry.Depth}; path {string.Join('/', entry.Path)}";
    protected override IReadOnlyList<Glyph> BuildHierarchy(HierarchySnapshot snapshot, int rootIndex, IReadOnlySet<int> collapsed)
    { _layout = ChartHierarchy.Flame(snapshot, rootIndex, collapsed, ValueMode); _self = snapshot.Nodes.ToDictionary(e => e.Index, e => e.Value - e.Children.Sum(i => snapshot.Nodes[i].Value)); return Array.AsReadOnly(_layout.Rectangles.Select(Rectangle).ToArray()); }
    protected override void ClearProjection(Exception error) { _layout = null; _self.Clear(); base.ClearProjection(error); }
}
