using Avalonia;
using Avalonia.Layout;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>Organization tree with selectable original nodes and nonselectable orthogonal connectors.</summary>
public sealed class OrganizationChart : HierarchyChartBase
{
    protected override bool SuppressCoveredAncestorLabels => false;
    public static readonly StyledProperty<Orientation> OrientationProperty = AvaloniaProperty.Register<OrganizationChart, Orientation>(nameof(Orientation), Orientation.Vertical, validate: Enum.IsDefined);
    private HierarchyTreeLayout? _layout;
    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    /// <summary>The normalized top-to-bottom target. Horizontal rendering transposes its node coordinates.</summary>
    public HierarchyTreeLayout? Layout { get { _ = GetMarks(); return _layout; } }
    protected override IReadOnlyList<Glyph> BuildHierarchy(HierarchySnapshot snapshot, int rootIndex, IReadOnlySet<int> collapsed)
    {
        _layout = ChartHierarchy.Tree(snapshot, rootIndex, collapsed);
        return Array.AsReadOnly(_layout.Nodes.Select(n => Orientation == Orientation.Horizontal ? new Glyph(n.Entry, GlyphKind.Rectangle, n.Y, n.X, n.Height, n.Width) : new Glyph(n.Entry, GlyphKind.Rectangle, n.X, n.Y, n.Width, n.Height)).ToArray());
    }
    protected override void RenderAnnotations(DrawingContext context, Rect plot)
    {
        var nodes = DisplayedGlyphs.Where(g => !g.IsExiting).ToDictionary(g => g.Entry.Index);
        using (context.PushClip(plot)) foreach (var node in nodes.Values)
        {
            if (!nodes.TryGetValue(node.Entry.ParentIndex, out var parent)) continue;
            var a = DisplayedRectangle(parent, plot); var b = DisplayedRectangle(node, plot);
            Point start, end, middle1, middle2;
            if (Orientation == Orientation.Horizontal)
            { start = new(a.Right, a.Center.Y); end = new(b.Left, b.Center.Y); var middle = (start.X + end.X) / 2; middle1 = new(middle, start.Y); middle2 = new(middle, end.Y); }
            else { start = new(a.Center.X, a.Bottom); end = new(b.Center.X, b.Top); var middle = (start.Y + end.Y) / 2; middle1 = new(start.X, middle); middle2 = new(end.X, middle); }
            context.DrawGeometry(null, new Pen(GridLineBrush ?? Foreground ?? Brushes.Gray, 1), new PolylineGeometry([start, middle1, middle2, end], false));
        }
        base.RenderAnnotations(context, plot);
    }
    protected override void ClearProjection(Exception error) { _layout = null; base.ClearProjection(error); }
}
