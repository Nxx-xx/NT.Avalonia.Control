using Avalonia;
using Avalonia.Layout;

namespace NT.AvaloniaControl.Charts;

/// <summary>Top-down hierarchy bands. Leaf totals determine width by default; horizontal orientation transposes the normalized layout.</summary>
public sealed class IcicleChart : HierarchyChartBase
{
    public static readonly StyledProperty<Orientation> OrientationProperty = AvaloniaProperty.Register<IcicleChart, Orientation>(nameof(Orientation), Orientation.Vertical, validate: Enum.IsDefined);
    public static readonly StyledProperty<HierarchyStackValueMode> ValueModeProperty = AvaloniaProperty.Register<IcicleChart, HierarchyStackValueMode>(nameof(ValueMode), HierarchyStackValueMode.LeafTotal, validate: Enum.IsDefined);
    private HierarchyRectangleLayout? _layout;
    public IcicleChart() { Gap = 1; }
    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    public HierarchyStackValueMode ValueMode { get => GetValue(ValueModeProperty); set => SetValue(ValueModeProperty, value); }
    public HierarchyRectangleLayout? Layout { get { _ = GetMarks(); return _layout; } }
    protected override bool SuppressCoveredAncestorLabels => false;
    protected override double GetHierarchyValue(HierarchyEntry entry) => ValueMode == HierarchyStackValueMode.Inclusive ? entry.Value : entry.AggregateValue;
    protected override IReadOnlyList<Glyph> BuildHierarchy(HierarchySnapshot snapshot, int rootIndex, IReadOnlySet<int> collapsed)
    {
        _layout = ChartHierarchy.Icicle(snapshot, rootIndex, collapsed, ValueMode);
        return Array.AsReadOnly(_layout.Rectangles.Select(r => Orientation == Orientation.Horizontal ? new Glyph(r.Entry, GlyphKind.Rectangle, r.Y, r.X, r.Height, r.Width) : Rectangle(r)).ToArray());
    }
    protected override void ClearProjection(Exception error) { _layout = null; base.ClearProjection(error); }
}
