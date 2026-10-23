namespace NT.AvaloniaControl.Charts;

/// <summary>Deterministic descending-weight pack of visible terminal nodes; original source order and identity are retained.</summary>
public sealed class PackedBubbleChart : HierarchyChartBase
{
    private HierarchyCircleLayout? _layout;
    public HierarchyCircleLayout? Layout { get { _ = GetMarks(); return _layout; } }
    protected override IReadOnlyList<Glyph> BuildHierarchy(HierarchySnapshot snapshot, int rootIndex, IReadOnlySet<int> collapsed)
    { _layout = ChartHierarchy.PackedBubble(snapshot, rootIndex, collapsed); return Array.AsReadOnly(_layout.Circles.Select(Circle).ToArray()); }
    protected override void ClearProjection(Exception error) { _layout = null; base.ClearProjection(error); }
}
