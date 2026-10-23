namespace NT.AvaloniaControl.Charts;

/// <summary>Deterministic source-ordered cloud of area-weighted visible terminal nodes.</summary>
public sealed class BubbleCloudChart : HierarchyChartBase
{
    private HierarchyCircleLayout? _layout;
    public HierarchyCircleLayout? Layout { get { _ = GetMarks(); return _layout; } }
    protected override IReadOnlyList<Glyph> BuildHierarchy(HierarchySnapshot snapshot, int rootIndex, IReadOnlySet<int> collapsed)
    { _layout = ChartHierarchy.BubbleCloud(snapshot, rootIndex, collapsed); return Array.AsReadOnly(_layout.Circles.Select(Circle).ToArray()); }
    protected override void ClearProjection(Exception error) { _layout = null; base.ClearProjection(error); }
}
