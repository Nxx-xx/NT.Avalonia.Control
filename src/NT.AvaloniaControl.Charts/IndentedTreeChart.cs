using Avalonia;
using Avalonia.Input;

namespace NT.AvaloniaControl.Charts;

/// <summary>A source-ordered expandable tree list. Rows fit the current plot; IndentSize is a DIP preference capped at ninety percent of its width.</summary>
public sealed class IndentedTreeChart : HierarchyChartBase
{
    public static readonly StyledProperty<double> IndentSizeProperty = AvaloniaProperty.Register<IndentedTreeChart, double>(nameof(IndentSize), 20, validate: x => double.IsFinite(x) && x >= 0 && x <= 4096);
    private HierarchyRectangleLayout? _layout;
    public IndentedTreeChart() { Gap = 1; }
    public double IndentSize { get => GetValue(IndentSizeProperty); set => SetValue(IndentSizeProperty, value); }
    public HierarchyRectangleLayout? Layout { get { _ = GetMarks(); return _layout; } }
    protected override bool AllowMissingHierarchyValues => true;
    protected override bool SuppressCoveredAncestorLabels => false;
    protected override IReadOnlyList<Glyph> BuildHierarchy(HierarchySnapshot snapshot, int rootIndex, IReadOnlySet<int> collapsed)
    {
        var plot = GetPlotArea(Bounds.Size); _layout = ChartHierarchy.Indented(snapshot, rootIndex, collapsed, Math.Min(1, IndentSize / Math.Max(1, plot.Width)));
        return Array.AsReadOnly(_layout.Rectangles.Select(Rectangle).ToArray());
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        var key = (ActivePoint ?? SelectedPoint)?.Mark.Key;
        if (e.Key == Key.Right && key is not null) { Expand(key); e.Handled = true; return; }
        if (e.Key == Key.Left && key is not null) { Collapse(key); e.Handled = true; return; }
        base.OnKeyDown(e);
    }
    protected override void ClearProjection(Exception error) { _layout = null; base.ClearProjection(error); }
}
