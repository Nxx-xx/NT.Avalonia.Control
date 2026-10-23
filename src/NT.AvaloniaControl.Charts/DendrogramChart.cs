using Avalonia;
using Avalonia.Layout;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>Renders a supplied cluster tree, without computing clusters. DistancePath uses finite nonnegative merge heights; null uses structural height.</summary>
public sealed class DendrogramChart : HierarchyChartBase
{
    public static readonly StyledProperty<string?> DistancePathProperty = AvaloniaProperty.Register<DendrogramChart, string?>(nameof(DistancePath));
    public static readonly StyledProperty<Orientation> OrientationProperty = AvaloniaProperty.Register<DendrogramChart, Orientation>(nameof(Orientation), Orientation.Horizontal, validate: Enum.IsDefined);
    public static readonly StyledProperty<HierarchyLinkStyle> LinkStyleProperty = AvaloniaProperty.Register<DendrogramChart, HierarchyLinkStyle>(nameof(LinkStyle), HierarchyLinkStyle.Elbow, validate: Enum.IsDefined);
    public static readonly StyledProperty<double> NodeSizeProperty = AvaloniaProperty.Register<DendrogramChart, double>(nameof(NodeSize), 5, validate: x => double.IsFinite(x) && x > 0 && x <= 128);
    private HierarchyDendrogramLayout? _layout;
    private Dictionary<int, double> _heights = [];
    public DendrogramChart() { Gap = 0; ValuePath = null; }
    public string? DistancePath { get => GetValue(DistancePathProperty); set => SetValue(DistancePathProperty, value); }
    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    public HierarchyLinkStyle LinkStyle { get => GetValue(LinkStyleProperty); set => SetValue(LinkStyleProperty, value); }
    /// <summary>Preferred node diameter in DIPs, clipped to the chart plot.</summary>
    public double NodeSize { get => GetValue(NodeSizeProperty); set => SetValue(NodeSizeProperty, value); }
    public HierarchyDendrogramLayout? Layout { get { _ = GetMarks(); return _layout; } }
    protected override bool AllowMissingHierarchyValues => true;
    protected override bool SuppressCoveredAncestorLabels => false;
    protected override double GetHierarchyValue(HierarchyEntry entry) => _heights.GetValueOrDefault(entry.Index);
    protected override string GetHierarchyTooltip(HierarchyEntry entry) => $"{entry.Label}: merge height {F(GetHierarchyValue(entry))}; depth {entry.Depth}; path {string.Join('/', entry.Path)}";
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    { if (change.Property == DistancePathProperty) SetCurrentValue(ValuePathProperty, DistancePath); base.OnPropertyChanged(change); }
    protected override IReadOnlyList<Glyph> BuildHierarchy(HierarchySnapshot snapshot, int rootIndex, IReadOnlySet<int> collapsed)
    {
        _layout = ChartHierarchy.Dendrogram(snapshot, rootIndex, collapsed, !string.IsNullOrWhiteSpace(DistancePath));
        _heights = _layout.Nodes.ToDictionary(n => n.Entry.Index, n => n.MergeHeight);
        var plot = GetPlotArea(Bounds.Size); var w = NodeSize / Math.Max(1, plot.Width); var h = NodeSize / Math.Max(1, plot.Height);
        return Array.AsReadOnly(_layout.Nodes.Select(n =>
        { var p = Orientation == Orientation.Horizontal ? new ChartPoint(n.Position.Y, n.Position.X) : n.Position; return new Glyph(n.Entry, GlyphKind.Rectangle, p.X - w / 2, p.Y - h / 2, w, h); }).ToArray());
    }
    protected override void RenderUnderlay(DrawingContext context, Rect plot)
    {
        var nodes = DisplayedGlyphs.Where(g => !g.IsExiting).ToDictionary(g => g.Entry.Index); var pen = new Pen(GridLineBrush ?? Foreground ?? Brushes.Gray, 1);
        foreach (var node in nodes.Values)
        {
            if (!nodes.TryGetValue(node.Entry.ParentIndex, out var parent)) continue;
            var a = DisplayedRectangle(parent, plot).Center; var b = DisplayedRectangle(node, plot).Center;
            if (LinkStyle == HierarchyLinkStyle.Straight) context.DrawLine(pen, a, b);
            else context.DrawGeometry(null, pen, new PolylineGeometry([a, Orientation == Orientation.Horizontal ? new Point(a.X, b.Y) : new Point(b.X, a.Y), b], false));
        }
        base.RenderUnderlay(context, plot);
    }
    protected override void RenderAnnotations(DrawingContext context, Rect plot)
    {
        base.RenderAnnotations(context, plot); if (!ShowLabels) return; using var clip = context.PushClip(plot);
        foreach (var glyph in DisplayedGlyphs.Where(g => !g.IsExiting))
        { var box = DisplayedRectangle(glyph, plot); var x = box.Center.X > plot.Center.X ? box.Left - 34 : box.Right + 34; DrawLabel(context, glyph.Entry.Label ?? "", new Point(x, box.Center.Y), 64); }
    }
    protected override void ClearProjection(Exception error) { _layout = null; _heights.Clear(); base.ClearProjection(error); }
}
