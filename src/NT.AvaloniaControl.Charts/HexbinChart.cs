using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>Pointy-top hexagonal Cartesian aggregation with exact original membership.</summary>
public sealed class HexbinChart : EngineeringChartBase
{
    public static readonly StyledProperty<string?> XPathProperty = AvaloniaProperty.Register<HexbinChart, string?>(nameof(XPath));
    public static readonly StyledProperty<string?> YPathProperty = AvaloniaProperty.Register<HexbinChart, string?>(nameof(YPath));
    public static readonly StyledProperty<string?> ValuePathProperty = AvaloniaProperty.Register<HexbinChart, string?>(nameof(ValuePath));
    public static readonly StyledProperty<double> BinRadiusProperty = AvaloniaProperty.Register<HexbinChart, double>(nameof(BinRadius), 18, validate: x => double.IsFinite(x) && x >= 1 && x <= 4096);
    public static readonly StyledProperty<double> HexRadiusProperty = BinRadiusProperty;
    public static readonly StyledProperty<HexbinAggregation> AggregationProperty = AvaloniaProperty.Register<HexbinChart, HexbinAggregation>(nameof(Aggregation), HexbinAggregation.Count, validate: Enum.IsDefined);
    private ChartSeriesBounds _domain = new(0, 1, 0, 1);
    private HexbinResult? _binning;
    public HexbinChart() => ObservePaths(nameof(HexbinPoint.X), nameof(HexbinPoint.Y), nameof(HexbinPoint.Value));
    public string? XPath { get => GetValue(XPathProperty); set => SetValue(XPathProperty, value); }
    public string? YPath { get => GetValue(YPathProperty); set => SetValue(YPathProperty, value); }
    public string? ValuePath { get => GetValue(ValuePathProperty); set => SetValue(ValuePathProperty, value); }
    /// <summary>Hexagon circumradius in DIPs at Zoom=1. Viewport navigation transforms existing bins.</summary>
    public double BinRadius { get => GetValue(BinRadiusProperty); set => SetValue(BinRadiusProperty, value); }
    public double HexRadius { get => BinRadius; set => BinRadius = value; }
    public HexbinAggregation Aggregation { get => GetValue(AggregationProperty); set => SetValue(AggregationProperty, value); }
    public ChartSeriesBounds DataBounds { get { _ = GetMarks(); return _domain; } }
    /// <summary>Binning in unzoomed plot DIPs. SourceIndices refer to ItemsSource positions, including skipped rows.</summary>
    public HexbinResult Binning { get { _ = GetMarks(); return _binning!; } }
    public IReadOnlyList<HexbinBin> Bins => Binning.Bins;
    public Point Project(ChartPoint value) { var plot = PlotArea; return CartesianToPlot(value, _domain, plot); }
    public ChartPoint Unproject(Point position) { var plot = PlotArea; return PlotToCartesian(position, _domain, plot); }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    { base.OnPropertyChanged(change); if (change.Property == XPathProperty || change.Property == YPathProperty || change.Property == ValuePathProperty) ObservePaths(XPath ?? nameof(HexbinPoint.X), YPath ?? nameof(HexbinPoint.Y), ValuePath ?? nameof(HexbinPoint.Value)); }
    protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
    {
        var raw = new HexbinPoint[data.Count]; var members = new ChartDataPoint[data.Count];
        for (var i = 0; i < data.Count; i++)
        {
            var source = data.Points[i]; var value = new HexbinPoint(double.NaN, double.NaN, double.NaN);
            if (source.Item is HexbinPoint typed && string.IsNullOrWhiteSpace(XPath) && string.IsNullOrWhiteSpace(YPath) && string.IsNullOrWhiteSpace(ValuePath)) value = typed;
            else if (source.Item is ChartPoint direct && string.IsNullOrWhiteSpace(XPath) && string.IsNullOrWhiteSpace(YPath)) value = new(direct.X, direct.Y);
            else if (Number(source.Item, XPath, out var x) && Number(source.Item, YPath, out var y))
                value = new(x, y, string.IsNullOrWhiteSpace(ValuePath) ? 1 : Number(source.Item, ValuePath, out var v) ? v : double.NaN);
            raw[i] = value;
            members[i] = source with { Point = new(value.X, value.Y), Value = value.Value, IsEmpty = !double.IsFinite(value.X) || !double.IsFinite(value.Y) || !double.IsFinite(value.Value) };
        }
        var valid = raw.Where(p => double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Value)).ToArray();
        _domain = valid.Length == 0 ? new(0, 1, 0, 1) : new(valid.Min(p => p.X), valid.Max(p => p.X), valid.Min(p => p.Y), valid.Max(p => p.Y));
        var screen = raw.Select(p =>
        {
            if (!double.IsFinite(p.X) || !double.IsFinite(p.Y) || !double.IsFinite(p.Value)) return new HexbinPoint(double.NaN, double.NaN, double.NaN);
            return new HexbinPoint(plot.Left + Fraction(p.X, _domain.XMinimum, _domain.XMaximum) * plot.Width, plot.Bottom - Fraction(p.Y, _domain.YMinimum, _domain.YMaximum) * plot.Height, p.Value);
        });
        _binning = ChartEngineering.Hexbin(screen, BinRadius, new(plot.Center.X, plot.Center.Y), Aggregation);
        var minimum = _binning.Bins.Count == 0 ? 0 : _binning.Bins.Min(b => b.Value); var maximum = _binning.Bins.Count == 0 ? 0 : _binning.Bins.Max(b => b.Value);
        return _binning.Bins.Select(bin =>
        {
            var points = bin.Vertices.Select(p => ApplyViewport(new(p.X, p.Y), plot)).ToArray();
            var color = (int)Math.Round(Fraction(bin.Value, minimum, maximum) * 3);
            return new EngineeringMarkInfo(Array.AsReadOnly(bin.SourceIndices.Select(index => members[index]).ToArray()), new PolylineGeometry(points, true), BrushAt(color), ApplyViewport(new(bin.Center.X, bin.Center.Y), plot))
            { Key = (bin.Q, bin.R), Value = bin.Value, TooltipText = $"{Aggregation}: {F(bin.Value)}; {bin.Count} observations; sources [{string.Join(", ", bin.SourceIndices.Take(12))}{(bin.Count > 12 ? ", …" : "")}]" };
        }).ToArray();
    }
    protected override IReadOnlyList<EngineeringGridLine> BuildGridLines(Rect plot) => [new(Line([ApplyViewport(plot.TopLeft, plot), ApplyViewport(plot.BottomLeft, plot), ApplyViewport(plot.BottomRight, plot)]))];
    protected override void ClearProjection(Exception error)
    { _domain = new(0, 1, 0, 1); _binning = ChartEngineering.Hexbin(Array.Empty<HexbinPoint>(), BinRadius, aggregation: Aggregation); }
}
