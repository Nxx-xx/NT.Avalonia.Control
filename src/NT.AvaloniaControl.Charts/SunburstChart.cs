using Avalonia;

namespace NT.AvaloniaControl.Charts;

/// <summary>Source-preserving hierarchy sectors with aggregate branch angles and concentric depth rings.</summary>
public sealed class SunburstChart : HierarchyChartBase
{
    public static readonly StyledProperty<double> InnerRadiusProperty = AvaloniaProperty.Register<SunburstChart, double>(nameof(InnerRadius), 0, validate: x => double.IsFinite(x) && x >= 0 && x < .5);
    public static readonly StyledProperty<double> StartAngleProperty = AvaloniaProperty.Register<SunburstChart, double>(nameof(StartAngle), -90, validate: double.IsFinite);
    public static readonly StyledProperty<double> SweepAngleProperty = AvaloniaProperty.Register<SunburstChart, double>(nameof(SweepAngle), 360, validate: x => double.IsFinite(x) && x > 0 && x <= 360);
    private HierarchySectorLayout? _layout;
    public double InnerRadius { get => GetValue(InnerRadiusProperty); set => SetValue(InnerRadiusProperty, value); }
    public double StartAngle { get => GetValue(StartAngleProperty); set => SetValue(StartAngleProperty, value); }
    public double SweepAngle { get => GetValue(SweepAngleProperty); set => SetValue(SweepAngleProperty, value); }
    public HierarchySectorLayout? Layout { get { _ = GetMarks(); return _layout; } }
    protected override IReadOnlyList<Glyph> BuildHierarchy(HierarchySnapshot snapshot, int rootIndex, IReadOnlySet<int> collapsed)
    { _layout = ChartHierarchy.Sunburst(snapshot, rootIndex, collapsed, InnerRadius, StartAngle, SweepAngle); return Array.AsReadOnly(_layout.Sectors.Select(Sector).ToArray()); }
    protected override void ClearProjection(Exception error) { _layout = null; base.ClearProjection(error); }
}
