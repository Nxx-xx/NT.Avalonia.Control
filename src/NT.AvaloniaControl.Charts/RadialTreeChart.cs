using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>A source-ordered radial tree with a central root and selectable original nodes. Depth determines radius; no clustering or force layout is inferred.</summary>
public sealed class RadialTreeChart : HierarchyChartBase
{
    public static readonly StyledProperty<double> StartAngleProperty = AvaloniaProperty.Register<RadialTreeChart, double>(nameof(StartAngle), -90, validate: double.IsFinite);
    public static readonly StyledProperty<double> SweepAngleProperty = AvaloniaProperty.Register<RadialTreeChart, double>(nameof(SweepAngle), 360, validate: x => double.IsFinite(x) && x > 0 && x <= 360);
    public static readonly StyledProperty<double> NodeSizeProperty = AvaloniaProperty.Register<RadialTreeChart, double>(nameof(NodeSize), 6, validate: x => double.IsFinite(x) && x > 0 && x <= 128);
    private HierarchyRadialLayout? _layout;
    public RadialTreeChart() { Gap = 0; }
    public double StartAngle { get => GetValue(StartAngleProperty); set => SetValue(StartAngleProperty, value); }
    public double SweepAngle { get => GetValue(SweepAngleProperty); set => SetValue(SweepAngleProperty, value); }
    /// <summary>Preferred node radius in DIPs before zoom.</summary>
    public double NodeSize { get => GetValue(NodeSizeProperty); set => SetValue(NodeSizeProperty, value); }
    public HierarchyRadialLayout? Layout { get { _ = GetMarks(); return _layout; } }
    protected override bool AllowMissingHierarchyValues => true;
    protected override IReadOnlyList<Glyph> BuildHierarchy(HierarchySnapshot snapshot, int rootIndex, IReadOnlySet<int> collapsed)
    {
        _layout = ChartHierarchy.RadialTree(snapshot, rootIndex, collapsed, StartAngle, SweepAngle);
        var square = Square(GetPlotArea(Bounds.Size)); var radius = NodeSize / Math.Max(1, square.Width);
        return Array.AsReadOnly(_layout.Nodes.Select(n => new Glyph(n.Entry, GlyphKind.Circle, n.Position.X, n.Position.Y, radius, radius)).ToArray());
    }
    private Point Center(Glyph glyph, Rect plot)
    { var square = Square(plot); return DisplayViewport(new(square.Left + glyph.X * square.Width, square.Top + glyph.Y * square.Height), plot); }
    protected override void RenderUnderlay(DrawingContext context, Rect plot)
    {
        var nodes = DisplayedGlyphs.Where(g => !g.IsExiting).ToDictionary(g => g.Entry.Index); var pen = new Pen(GridLineBrush ?? Foreground ?? Brushes.Gray, 1);
        foreach (var node in nodes.Values) if (nodes.TryGetValue(node.Entry.ParentIndex, out var parent)) context.DrawLine(pen, Center(parent, plot), Center(node, plot));
        base.RenderUnderlay(context, plot);
    }
    protected override void RenderAnnotations(DrawingContext context, Rect plot)
    {
        base.RenderAnnotations(context, plot); if (!ShowLabels) return; using var clip = context.PushClip(plot);
        foreach (var glyph in DisplayedGlyphs.Where(g => !g.IsExiting))
        { var p = Center(glyph, plot); var y = p.Y > plot.Center.Y ? p.Y - NodeSize - FontSize : p.Y + NodeSize + FontSize; DrawLabel(context, glyph.Entry.Label ?? "", new Point(p.X, y), 90); }
    }
    protected override void ClearProjection(Exception error) { _layout = null; base.ClearProjection(error); }
}
