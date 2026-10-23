using System.Windows.Input;

namespace Charts.Usage;

public sealed partial class ChartUsageViewModel
{
    private int _hierarchySelectedIndex = -1, _hierarchyDepth = 64;
    private bool _hierarchyLabels = true, _hierarchyReduceMotion = true;
    public int HierarchySelectedIndex { get => _hierarchySelectedIndex; set { if (_hierarchySelectedIndex == value) return; _hierarchySelectedIndex = value; Notify(); } }
    public int HierarchyDepth { get => _hierarchyDepth; set { if (_hierarchyDepth == value) return; _hierarchyDepth = value; Notify(); } }
    public bool HierarchyLabels { get => _hierarchyLabels; set { if (_hierarchyLabels == value) return; _hierarchyLabels = value; Notify(); } }
    public bool HierarchyReduceMotion { get => _hierarchyReduceMotion; set { if (_hierarchyReduceMotion == value) return; _hierarchyReduceMotion = value; Notify(); } }
    public int[] HierarchyDepthOptions { get; } = [0, 1, 2, 64];
    public EngineeringObserverCollection<HierarchyBusinessNode> HierarchyRecords { get; } =
    [new("north", "北区", 999, [new("a", "服务 A", 6), new("b", "服务 B", 3)]),
     new("south", "南区", 222, [new("c", "服务 C", 2), new("d", "服务 D", 1)])];
    public ICommand HierarchyUpdateCommand => new ActionCommand(() => HierarchyRecords[0].Nodes[0].Weight += 2);
}

public sealed class HierarchyBusinessNode(string id, string name, double weight, IEnumerable<HierarchyBusinessNode>? nodes = null) : AdvancedBusinessRecord(id)
{
    private double _weight = weight;
    private EngineeringObserverCollection<HierarchyBusinessNode> _nodes = new(nodes ?? []);
    public string Name { get; } = name;
    public string Group => Id is "north" or "a" or "b" or "extra" ? "北区" : "南区";
    public double Weight { get => _weight; set => Set(ref _weight, value); }
    public EngineeringObserverCollection<HierarchyBusinessNode> Nodes { get => _nodes; set => Set(ref _nodes, value); }
}
