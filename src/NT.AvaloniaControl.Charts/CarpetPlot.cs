using Avalonia;

namespace NT.AvaloniaControl.Charts;

/// <summary>A two-parameter carpet mesh, X=A+Offset×B and Y from the observed surface.</summary>
public sealed class CarpetPlot : EngineeringChartBase
{
    public static readonly StyledProperty<string?> APathProperty = AvaloniaProperty.Register<CarpetPlot, string?>(nameof(APath));
    public static readonly StyledProperty<string?> BPathProperty = AvaloniaProperty.Register<CarpetPlot, string?>(nameof(BPath));
    public static readonly StyledProperty<string?> YPathProperty = AvaloniaProperty.Register<CarpetPlot, string?>(nameof(YPath));
    public static readonly StyledProperty<double> OffsetProperty = AvaloniaProperty.Register<CarpetPlot, double>(nameof(Offset), .5, validate: double.IsFinite);
    public static readonly StyledProperty<string?> AAxisPathProperty = APathProperty;
    public static readonly StyledProperty<string?> BAxisPathProperty = BPathProperty;
    public static readonly StyledProperty<string?> YAxisPathProperty = YPathProperty;
    public static readonly StyledProperty<double> CarpetOffsetProperty = OffsetProperty;
    private CarpetGridSnapshot? _grid;
    private ChartSeriesBounds _domain = new(0, 1, 0, 1);
    public CarpetPlot() => ObservePaths(nameof(CarpetPoint.A), nameof(CarpetPoint.B), nameof(CarpetPoint.Y));
    public string? APath { get => GetValue(APathProperty); set => SetValue(APathProperty, value); }
    public string? BPath { get => GetValue(BPathProperty); set => SetValue(BPathProperty, value); }
    public string? YPath { get => GetValue(YPathProperty); set => SetValue(YPathProperty, value); }
    public double Offset { get => GetValue(OffsetProperty); set => SetValue(OffsetProperty, value); }
    public string? AAxisPath { get => APath; set => APath = value; }
    public string? BAxisPath { get => BPath; set => BPath = value; }
    public string? YAxisPath { get => YPath; set => YPath = value; }
    public double CarpetOffset { get => Offset; set => Offset = value; }
    public CarpetGridSnapshot? Grid { get { _ = GetMarks(); return _grid; } }
    public ChartSeriesBounds DataBounds { get { _ = GetMarks(); return _domain; } }
    /// <summary>Explains a duplicate, incomplete or degenerate input mesh without inventing intersections.</summary>
    public string? MeshError { get; private set; }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    { base.OnPropertyChanged(change); if (change.Property == APathProperty || change.Property == BPathProperty || change.Property == YPathProperty) ObservePaths(APath ?? nameof(CarpetPoint.A), BPath ?? nameof(CarpetPoint.B), YPath ?? nameof(CarpetPoint.Y)); }
    public Point Project(CarpetPoint point) { var plot = PlotArea; return CartesianToPlot(ChartEngineering.ProjectCarpet(point, Offset), _domain, plot); }
    public CarpetPoint Interpolate(double a, double b) => ChartEngineering.InterpolateCarpet(Grid ?? throw new InvalidOperationException(MeshError ?? "No carpet mesh is available."), a, b);
    public CarpetPoint Unproject(Point position)
    {
        var plot = PlotArea; var grid = _grid ?? throw new InvalidOperationException(MeshError ?? "No carpet mesh is available.");
        return ChartEngineering.UnprojectCarpet(grid, PlotToCartesian(position, _domain, plot));
    }
    protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
    {
        var values = new List<(ChartDataPoint Source, CarpetPoint Raw, ChartPoint Projected)>();
        foreach (var source in data.Points)
        {
            CarpetPoint raw;
            if (source.Item is CarpetPoint typed && string.IsNullOrWhiteSpace(APath) && string.IsNullOrWhiteSpace(BPath) && string.IsNullOrWhiteSpace(YPath)) raw = typed;
            else if (Number(source.Item, APath, out var a) && Number(source.Item, BPath, out var b) && Number(source.Item, YPath, out var y)) raw = new(a, b, y);
            else continue;
            if (!double.IsFinite(raw.A) || !double.IsFinite(raw.B) || !double.IsFinite(raw.Y)) continue;
            values.Add((source, raw, ChartEngineering.ProjectCarpet(raw, Offset)));
        }
        _domain = values.Count == 0 ? new(0, 1, 0, 1) : new(values.Min(p => p.Projected.X), values.Max(p => p.Projected.X), values.Min(p => p.Projected.Y), values.Max(p => p.Projected.Y));
        MeshError = null; _grid = null;
        try { _grid = ChartEngineering.CarpetGrid(values.Select(v => v.Raw), Offset); }
        catch (ArgumentException exception) when (exception is not ArgumentOutOfRangeException) { MeshError = exception.Message; }
        catch (InvalidOperationException exception) { MeshError = exception.Message; }
        return values.Select(v => Marker(v.Source with { Point = new(v.Raw.A, v.Raw.B), Value = v.Raw, IsEmpty = false }, CartesianToPlot(v.Projected, _domain, plot),
            $"{v.Source.Category}: A={F(v.Raw.A)}, B={F(v.Raw.B)}, Y={F(v.Raw.Y)}; X={F(v.Projected.X)}", v.Raw.Y)).ToArray();
    }
    protected override IReadOnlyList<EngineeringGridLine> BuildGridLines(Rect plot) => _grid?.Paths.Select(path => new EngineeringGridLine(Line(path.Points.Select(p => CartesianToPlot(p, _domain, plot))), path.Label, path.Points.Count == 0 ? null : CartesianToPlot(path.Points[^1], _domain, plot))).ToArray() ?? [];
    protected override void ClearProjection(Exception error) { _grid = null; _domain = new(0, 1, 0, 1); MeshError = error.Message; }
}
