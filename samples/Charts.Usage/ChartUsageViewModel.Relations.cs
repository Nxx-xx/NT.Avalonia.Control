using System.Windows.Input;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class ChartUsageViewModel
{
    private int _relationsSelectedIndex = -1, _relationsSeed = 17;
    private bool _relationsLabels = true;
    public int RelationsSelectedIndex { get => _relationsSelectedIndex; set { if (_relationsSelectedIndex == value) return; _relationsSelectedIndex = value; Notify(); } }
    public int RelationsSeed { get => _relationsSeed; set { if (_relationsSeed == value) return; _relationsSeed = value; Notify(); } }
    public bool RelationsLabels { get => _relationsLabels; set { if (_relationsLabels == value) return; _relationsLabels = value; Notify(); } }
    public EngineeringObserverCollection<RelationBusinessNode> RelationNodes { get; } =
    [new("a", "入口", .2, .5, 0, "入口", RelationNodeShape.Ellipse), new("b", "审核", .5, .2, 1, "处理", RelationNodeShape.Diamond),
     new("c", "归档", .8, .5, 2, "处理", RelationNodeShape.RoundedRectangle)];
    public EngineeringObserverCollection<RelationBusinessEdge> RelationEdges { get; } =
    [new("ab", "a", "b", 4), new("bc", "b", "c", 3), new("ac", "a", "c", 2), new("ca", "c", "a", 1), new("parallel", "a", "b", 1), new("loop", "b", "b", 1)];
    public ICommand RelationsUpdateCommand => new ActionCommand(() => RelationEdges[0].Amount += 2);
    public ICommand RelationsSeedCommand => new ActionCommand(() => RelationsSeed++);
}
public sealed class RelationBusinessNode(string id, string name, double x, double y, int stage, string category, RelationNodeShape shape) : AdvancedBusinessRecord(id)
{
    private double _x = x, _y = y;
    public string Name { get; } = name;
    public string Category { get; } = category;
    public RelationNodeShape Shape { get; } = shape;
    public int Stage { get; } = stage;
    public double Width => .12;
    public double Height => .08;
    public double Metric => 4;
    public double X { get => _x; set => Set(ref _x, value); }
    public double Y { get => _y; set => Set(ref _y, value); }
}
public sealed class RelationBusinessEdge(string id, string from, string to, double amount, string? name = null) : AdvancedBusinessRecord(id)
{
    private double _amount = amount;
    private string _to = to;
    public string From { get; } = from;
    public string To { get => _to; set => Set(ref _to, value); }
    public string? Name { get; } = name;
    public double Amount { get => _amount; set => Set(ref _amount, value); }
}
