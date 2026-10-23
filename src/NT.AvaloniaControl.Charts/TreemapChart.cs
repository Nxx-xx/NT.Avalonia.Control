using Avalonia;

namespace NT.AvaloniaControl.Charts;

/// <summary>Nested area rectangles; leaf weights aggregate into their ancestors. Source order is deterministic.</summary>
public sealed class TreemapChart : HierarchyChartBase
{
    public static readonly StyledProperty<TreemapLayoutMode> LayoutModeProperty = AvaloniaProperty.Register<TreemapChart, TreemapLayoutMode>(nameof(LayoutMode), TreemapLayoutMode.Squarify, validate: Enum.IsDefined);
    private HierarchyRectangleLayout? _layout;
    public TreemapLayoutMode LayoutMode { get => GetValue(LayoutModeProperty); set => SetValue(LayoutModeProperty, value); }
    public HierarchyRectangleLayout? Layout { get { _ = GetMarks(); return _layout; } }
    protected override IReadOnlyList<Glyph> BuildHierarchy(HierarchySnapshot snapshot, int rootIndex, IReadOnlySet<int> collapsed)
    { _layout = ChartHierarchy.Treemap(snapshot, rootIndex, collapsed, LayoutMode); return Array.AsReadOnly(_layout.Rectangles.Select(Rectangle).ToArray()); }
    protected override void ClearProjection(Exception error) { _layout = null; base.ClearProjection(error); }
}
