namespace NT.AvaloniaControl.Charts;

/// <summary>Nested circles enclosing child packs. Parent areas include layout whitespace; leaf areas share one scale.</summary>
public sealed class CirclePackingChart : HierarchyChartBase
{
    private HierarchyCircleLayout? _layout;
    public HierarchyCircleLayout? Layout { get { _ = GetMarks(); return _layout; } }
    protected override IReadOnlyList<Glyph> BuildHierarchy(HierarchySnapshot snapshot, int rootIndex, IReadOnlySet<int> collapsed)
    { _layout = ChartHierarchy.CirclePacking(snapshot, rootIndex, collapsed); return Array.AsReadOnly(_layout.Circles.Select(Circle).ToArray()); }
    protected override void ClearProjection(Exception error) { _layout = null; base.ClearProjection(error); }
}
