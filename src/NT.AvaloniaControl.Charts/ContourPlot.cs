using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>Observed scalar-grid contours with shared-topology fills, source-preserving hits and engineering navigation/export.</summary>
public sealed class ContourPlot : ComparisonChartBase
{
    public static readonly StyledProperty<string?> XPathProperty = AvaloniaProperty.Register<ContourPlot, string?>(nameof(XPath), "X");
    public static readonly StyledProperty<string?> YPathProperty = AvaloniaProperty.Register<ContourPlot, string?>(nameof(YPath), "Y");
    public static readonly StyledProperty<string?> ValuePathProperty = AvaloniaProperty.Register<ContourPlot, string?>(nameof(ValuePath), "Value");
    public static readonly StyledProperty<IReadOnlyList<double>?> LevelsProperty = AvaloniaProperty.Register<ContourPlot, IReadOnlyList<double>?>(nameof(Levels));
    public static readonly StyledProperty<int> ContourLevelsProperty = AvaloniaProperty.Register<ContourPlot, int>(nameof(ContourLevels), 8, validate: value => value >= 1 && value <= ChartContours.MaximumLevelCount);
    public static readonly StyledProperty<bool> ShowContourLinesProperty = AvaloniaProperty.Register<ContourPlot, bool>(nameof(ShowContourLines), true);
    public static readonly StyledProperty<bool> ShowFilledBandsProperty = AvaloniaProperty.Register<ContourPlot, bool>(nameof(ShowFilledBands), true);
    public static readonly StyledProperty<bool> ShowLinesProperty = ShowContourLinesProperty;
    public static readonly StyledProperty<bool> ShowFillProperty = ShowFilledBandsProperty;
    public static readonly StyledProperty<Color> MinimumColorProperty = AvaloniaProperty.Register<ContourPlot, Color>(nameof(MinimumColor), Color.Parse("#D7E9F5"));
    public static readonly StyledProperty<Color> MaximumColorProperty = AvaloniaProperty.Register<ContourPlot, Color>(nameof(MaximumColor), Color.Parse("#2563EB"));
    public static readonly StyledProperty<IBrush?> LineBrushProperty = AvaloniaProperty.Register<ContourPlot, IBrush?>(nameof(LineBrush));
    public static readonly StyledProperty<double> LineThicknessProperty = AvaloniaProperty.Register<ContourPlot, double>(nameof(LineThickness), 1.5, validate: value => double.IsFinite(value) && value > 0 && value <= 64);
    private ContourResult? _contours;
    private bool _observing;
    private ChartSeriesBounds _bounds = new(0, 1, 0, 1);
    public ContourPlot() { IdPath = "Id"; ShowGrid = false; ObserveContourPaths(); }
    protected override int SourceItemLimit => ChartContours.MaximumPointCount;
    public string? XPath { get => GetValue(XPathProperty); set => SetValue(XPathProperty, value); }
    public string? YPath { get => GetValue(YPathProperty); set => SetValue(YPathProperty, value); }
    public string? ValuePath { get => GetValue(ValuePathProperty); set => SetValue(ValuePathProperty, value); }
    public IReadOnlyList<double>? Levels { get => GetValue(LevelsProperty); set => SetValue(LevelsProperty, value); }
    /// <summary>Number of automatic interior levels when Levels is null; constant/rounded-coincident levels collapse.</summary>
    public int ContourLevels { get => GetValue(ContourLevelsProperty); set => SetValue(ContourLevelsProperty, value); }
    public bool ShowContourLines { get => GetValue(ShowContourLinesProperty); set => SetValue(ShowContourLinesProperty, value); }
    public bool ShowFilledBands { get => GetValue(ShowFilledBandsProperty); set => SetValue(ShowFilledBandsProperty, value); }
    public bool ShowLines { get => ShowContourLines; set => ShowContourLines = value; }
    public bool ShowFill { get => ShowFilledBands; set => ShowFilledBands = value; }
    public Color MinimumColor { get => GetValue(MinimumColorProperty); set => SetValue(MinimumColorProperty, value); }
    public Color MaximumColor { get => GetValue(MaximumColorProperty); set => SetValue(MaximumColorProperty, value); }
    public IBrush? LineBrush { get => GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }
    public double LineThickness { get => GetValue(LineThicknessProperty); set => SetValue(LineThicknessProperty, value); }
    public ContourResult? Contours { get { _ = GetMarks(); return _contours; } }
    public ChartSeriesBounds DataBounds { get { _ = GetMarks(); return _bounds; } }
    public Point Project(double x, double y) { _ = GetMarks(); return CartesianToPlot(new(x, y), _bounds, PlotArea); }
    private void ObserveContourPaths() => ObservePaths(XPath, YPath, ValuePath);
    protected override void OnDataInvalidated() => _contours = null;
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == XPathProperty || change.Property == YPathProperty || change.Property == ValuePathProperty || change.Property == LevelsProperty || change.Property == ContourLevelsProperty) _contours = null;
        base.OnPropertyChanged(change);
        if (change.Property == XPathProperty || change.Property == YPathProperty || change.Property == ValuePathProperty) ObserveContourPaths();
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    { _observing = true; _contours = null; base.OnAttachedToVisualTree(e); }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    { _observing = false; base.OnDetachedFromVisualTree(e); }
    protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
    {
        BeginScene(); ObserveNested(new object?[] { Levels });
        if (!_observing) _contours = null;
        if (_contours is null)
        {
            var input = new List<ScalarGridPoint>(data.Count);
            foreach (var source in data.Points)
            {
                if (!Number(source.Item, XPath, out var x) || !Number(source.Item, YPath, out var y)) throw new ArgumentException("Every scalar observation requires finite X and Y coordinates.");
                var raw = Read(source.Item, ValuePath); double value;
                if (raw is null) value = double.NaN;
                else if (raw is double number) value = number;
                else if (raw is float single) value = single;
                else if (!Number(source.Item, ValuePath, out value)) throw new ArgumentException("Scalar values must be numeric or missing.");
                input.Add(new(source.StableId, x, y, value));
            }
            _contours = Levels is { } explicitLevels ? ChartContours.Build(input, explicitLevels) : ChartContours.Build(input, ContourLevels);
        }
        var contour = _contours;
        if (contour.Grid.Count == 0) { _bounds = new(0, 1, 0, 1); return []; }
        _bounds = new(contour.XCoordinates[0], contour.XCoordinates[^1], contour.YCoordinates[0], contour.YCoordinates[^1]);
        var sources = data.Points.ToArray(); var result = new List<EngineeringMarkInfo>();
        for (var i = 0; i < contour.Grid.Count; i++)
        {
            var point = contour.Grid[i]; var sourceIndex = contour.SourceIndices[i];
            sources[sourceIndex] = sources[sourceIndex] with { Point = new(point.X, point.Y), Value = point.Value, IsEmpty = false, WasEmpty = false };
        }
        var cells = new Dictionary<int, IReadOnlyList<ChartDataPoint>>();
        IReadOnlyList<ChartDataPoint> Members(int cell, IReadOnlyList<int> indices)
        { if (!cells.TryGetValue(cell, out var members)) cells[cell] = members = Array.AsReadOnly(indices.Select(index => sources[index]).ToArray()); return members; }
        if (ShowFilledBands) for (var i = 0; i < contour.Bands.Count; i++)
        {
            var band = contour.Bands[i]; var points = band.Vertices.Select(p => CartesianToPlot(p, _bounds, plot)).ToArray(); var position = new Point(points.Average(p => p.X), points.Average(p => p.Y));
            var color = ColorAt((double)band.BandIndex / contour.Levels.Count);
            result.Add(new(Members(band.CellIndex, band.SourceIndices), new PolylineGeometry(points, true), color, position) { Key = new ContourKey(false, band.CellIndex, band.BandIndex, i), Value = band.BandIndex, TooltipText = $"Cell {band.CellIndex}: {Bound(band.LowerBound, "−∞")} ≤ value < {Bound(band.UpperBound, "+∞")}" });
        }
        if (ShowContourLines) for (var i = 0; i < contour.Lines.Count; i++)
        {
            var line = contour.Lines[i]; var start = CartesianToPlot(line.Start, _bounds, plot); var end = CartesianToPlot(line.End, _bounds, plot); var brush = LineBrush ?? Foreground ?? Brushes.Black;
            var members = line.CellIndices.Count == 1 ? Members(line.CellIndex, line.SourceIndices) : Array.AsReadOnly(line.SourceIndices.Select(index => sources[index]).ToArray());
            result.Add(new(members, new PolylineGeometry(new[] { start, end }, false), brush, new((start.X + end.X) / 2, (start.Y + end.Y) / 2)) { Key = new ContourKey(true, line.CellIndex, 0, i), Value = line.Level, TooltipText = $"Cell {string.Join(",", line.CellIndices)}: value = {F(line.Level)}" });
        }
        return result;
    }
    private static string Bound(double? value, string fallback) => value is { } number ? F(number) : fallback;
    private IBrush ColorAt(double fraction)
    {
        byte Mix(byte a, byte b) => (byte)Math.Clamp(Math.Round((1 - fraction) * a + fraction * b), 0, 255);
        var a = MinimumColor; var b = MaximumColor; return new SolidColorBrush(Color.FromArgb(Mix(a.A, b.A), Mix(a.R, b.R), Mix(a.G, b.G), Mix(a.B, b.B)));
    }
    protected override bool ContainsMark(EngineeringMarkInfo mark, Point point)
    {
        if (mark.Key is not ContourKey { IsLine: true } || mark.Geometry is not PolylineGeometry line) return base.ContainsMark(mark, point);
        var a = line.Points[0]; var b = line.Points[1]; var delta = b - a; var length = delta.X * delta.X + delta.Y * delta.Y;
        var fraction = length == 0 ? 0 : Math.Clamp(((point.X - a.X) * delta.X + (point.Y - a.Y) * delta.Y) / length, 0, 1);
        var nearest = new Point(a.X + fraction * delta.X, a.Y + fraction * delta.Y);
        var dx = nearest.X - point.X; var dy = nearest.Y - point.Y;
        return Math.Sqrt(dx * dx + dy * dy) <= Math.Max(3, LineThickness / 2);
    }
    protected override void RenderEngineeringMarks(DrawingContext context, IReadOnlyList<EngineeringMarkInfo> marks, EngineeringPointHit? selected, EngineeringPointHit? active)
    {
        for (var i = 0; i < marks.Count; i++)
        {
            var mark = marks[i]; var line = mark.Key is ContourKey { IsLine: true }; var highlight = selected?.Index == i || active?.Index == i;
            context.DrawGeometry(line ? null : mark.Fill, line || highlight ? new Pen(highlight ? Foreground ?? Brushes.Gray : mark.Fill, highlight ? Math.Max(2, LineThickness + 1) : LineThickness) : null, mark.Geometry);
        }
    }
    protected override void ClearProjection(Exception error) { base.ClearProjection(error); _contours = null; _bounds = new(0, 1, 0, 1); }
    private sealed record ContourKey(bool IsLine, int Cell, int Band, int Primitive);
}
