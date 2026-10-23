using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>North-up clockwise direction sectors, stacked speed bands and frequency-weighted percentages.</summary>
public sealed class WindRoseChart : EngineeringChartBase
{
    public static readonly StyledProperty<string?> DirectionPathProperty = AvaloniaProperty.Register<WindRoseChart, string?>(nameof(DirectionPath));
    public static readonly StyledProperty<string?> SpeedPathProperty = AvaloniaProperty.Register<WindRoseChart, string?>(nameof(SpeedPath));
    public static readonly StyledProperty<string?> FrequencyPathProperty = AvaloniaProperty.Register<WindRoseChart, string?>(nameof(FrequencyPath));
    public static readonly StyledProperty<string?> ValuePathProperty = FrequencyPathProperty;
    public static readonly StyledProperty<int> SectorCountProperty = AvaloniaProperty.Register<WindRoseChart, int>(nameof(SectorCount), 16, validate: x => x >= 1 && x <= 360);
    public static readonly StyledProperty<IReadOnlyList<double>> SpeedUpperBoundsProperty = AvaloniaProperty.Register<WindRoseChart, IReadOnlyList<double>>(nameof(SpeedUpperBounds), Array.AsReadOnly(new double[] { 3, 6 }), validate: ValidSpeedBounds);
    public static readonly StyledProperty<ChartAngleUnit> AngleUnitProperty = AvaloniaProperty.Register<WindRoseChart, ChartAngleUnit>(nameof(AngleUnit), ChartAngleUnit.Degrees, validate: Enum.IsDefined);
    private WindRoseResult? _rose;
    private double _maximumFrequency;
    public WindRoseChart() => ObservePaths(nameof(WindObservation.Direction), nameof(WindObservation.Speed), nameof(WindObservation.Frequency));
    public string? DirectionPath { get => GetValue(DirectionPathProperty); set => SetValue(DirectionPathProperty, value); }
    public string? SpeedPath { get => GetValue(SpeedPathProperty); set => SetValue(SpeedPathProperty, value); }
    public string? FrequencyPath { get => GetValue(FrequencyPathProperty); set => SetValue(FrequencyPathProperty, value); }
    public string? ValuePath { get => FrequencyPath; set => FrequencyPath = value; }
    public int SectorCount { get => GetValue(SectorCountProperty); set => SetValue(SectorCountProperty, value); }
    /// <summary>Strictly ascending positive speed cutoffs. A final infinite band is added automatically.</summary>
    public IReadOnlyList<double> SpeedUpperBounds { get => GetValue(SpeedUpperBoundsProperty); set => SetValue(SpeedUpperBoundsProperty, value); }
    public ChartAngleUnit AngleUnit { get => GetValue(AngleUnitProperty); set => SetValue(AngleUnitProperty, value); }
    public WindRoseResult Rose { get { _ = GetMarks(); return _rose!; } }
    public double MaximumFrequency { get { _ = GetMarks(); return _maximumFrequency; } }
    private static bool ValidSpeedBounds(IReadOnlyList<double> bounds)
    {
        if (bounds is null || bounds.Count > ChartEngineering.MaximumCount / 360 - 1) return false;
        for (var i = 0; i < bounds.Count; i++) if (!double.IsFinite(bounds[i]) || bounds[i] <= 0 || i > 0 && bounds[i] <= bounds[i - 1]) return false;
        return true;
    }
    protected override Rect GetPlotArea(Size size) => Square(base.GetPlotArea(size));
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    { base.OnPropertyChanged(change); if (change.Property == DirectionPathProperty || change.Property == SpeedPathProperty || change.Property == FrequencyPathProperty) ObservePaths(DirectionPath ?? nameof(WindObservation.Direction), SpeedPath ?? nameof(WindObservation.Speed), FrequencyPath ?? nameof(WindObservation.Frequency)); }
    /// <summary>Projects a direction (AngleUnit) and radial frequency to chart-local DIPs.</summary>
    public Point Project(double direction, double frequency)
    {
        if (!double.IsFinite(direction) || !double.IsFinite(frequency) || frequency < 0) throw new ArgumentOutOfRangeException(nameof(direction), "Direction and nonnegative frequency must be finite.");
        var plot = PlotArea;
        var degrees = AngleUnit == ChartAngleUnit.Degrees ? direction % 360 : direction % (2 * Math.PI) * (180 / Math.PI);
        return ProjectPolar(degrees, frequency, plot);
    }
    /// <summary>Returns direction in AngleUnit and radial frequency, rather than an invented observation.</summary>
    public ChartPoint Unproject(Point position)
    {
        var plot = PlotArea; if (plot.Width <= 0 || plot.Height <= 0) throw new InvalidOperationException("The chart must be arranged.");
        if (!double.IsFinite(position.X) || !double.IsFinite(position.Y)) throw new ArgumentOutOfRangeException(nameof(position), "Coordinates must be finite.");
        var point = RemoveViewport(position, plot); var dx = point.X - plot.Center.X; var dy = plot.Center.Y - point.Y;
        var direction = (Math.Atan2(dx, dy) * 180 / Math.PI + 360) % 360;
        var scale = Math.Max(Math.Abs(dx), Math.Abs(dy));
        var radialFraction = scale == 0 ? 0 : scale / (plot.Width / 2) * Math.Sqrt((dx / scale) * (dx / scale) + (dy / scale) * (dy / scale));
        var frequency = radialFraction * _maximumFrequency;
        if (!double.IsFinite(frequency)) throw new OverflowException("The radial frequency cannot be represented.");
        return new(AngleUnit == ChartAngleUnit.Degrees ? direction : direction * Math.PI / 180, frequency);
    }
    private Point ProjectPolar(double direction, double frequency, Rect plot)
        => ProjectPolarFraction(direction, _maximumFrequency <= 0 ? 0 : frequency / _maximumFrequency, plot);
    private Point ProjectPolarFraction(double direction, double fraction, Rect plot)
    { var radians = direction * Math.PI / 180; var radius = fraction * plot.Width / 2; return ApplyViewport(new(plot.Center.X + Math.Sin(radians) * radius, plot.Center.Y - Math.Cos(radians) * radius), plot); }
    private bool TryDirection(object? item, out double direction)
    {
        direction = double.NaN;
        if (string.IsNullOrWhiteSpace(DirectionPath) || !ChartDataAccessor.TryRead(item, DirectionPath, out var raw)) return false;
        if (ChartDataAccessor.TryNumber(raw, out direction)) return true;
        if (raw is not string compass) return false;
        try { direction = ChartEngineering.ParseWindDirection(compass, AngleUnit); if (AngleUnit == ChartAngleUnit.Radians) direction *= Math.PI / 180; return true; }
        catch (FormatException) { return false; }
        catch (ArgumentException) { return false; }
    }
    protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
    {
        var input = new WindObservation[data.Count]; var members = new ChartDataPoint[data.Count];
        for (var i = 0; i < data.Count; i++)
        {
            var source = data.Points[i]; var raw = new WindObservation(double.NaN, double.NaN, double.NaN);
            if (source.Item is WindObservation typed && string.IsNullOrWhiteSpace(DirectionPath) && string.IsNullOrWhiteSpace(SpeedPath) && string.IsNullOrWhiteSpace(FrequencyPath)) raw = typed;
            else if (TryDirection(source.Item, out var direction) && Number(source.Item, SpeedPath, out var speed))
                raw = new(direction, speed, string.IsNullOrWhiteSpace(FrequencyPath) ? 1 : Number(source.Item, FrequencyPath, out var f) ? f : double.NaN);
            input[i] = raw; members[i] = source with { Point = new(raw.Direction, raw.Speed), Value = raw, IsEmpty = !double.IsFinite(raw.Direction) || !double.IsFinite(raw.Speed) || !double.IsFinite(raw.Frequency) || raw.Speed < 0 || raw.Frequency <= 0 };
        }
        _rose = ChartEngineering.WindRose(input, SpeedUpperBounds, SectorCount, AngleUnit);
        _maximumFrequency = _rose.Sectors.Count == 0 ? 0 : _rose.Sectors.Max(s => s.Frequency);
        var marks = new List<EngineeringMarkInfo>();
        foreach (var bin in _rose.Bins.Where(b => b.Count > 0))
        {
            var sector = _rose.Sectors[bin.SectorIndex]; var start = sector.DirectionDegrees - sector.WidthDegrees / 2; var end = sector.DirectionDegrees + sector.WidthDegrees / 2;
            var segments = Math.Max(2, (int)Math.Ceiling(sector.WidthDegrees / 4)); var points = new List<Point>();
            for (var i = 0; i <= segments; i++) points.Add(ProjectPolar(start + (end - start) * i / segments, bin.EndFrequency, plot));
            for (var i = segments; i >= 0; i--) points.Add(ProjectPolar(start + (end - start) * i / segments, bin.StartFrequency, plot));
            var position = ProjectPolar(sector.DirectionDegrees, bin.StartFrequency / 2 + bin.EndFrequency / 2, plot);
            marks.Add(new(Array.AsReadOnly(bin.SourceIndices.Select(index => members[index]).ToArray()), new PolylineGeometry(points, true), BrushAt(bin.SpeedBandIndex), position)
            { Key = (bin.SectorIndex, bin.SpeedBandIndex), Value = bin.Frequency, TooltipText = $"{sector.Label}: speed [{F(bin.LowerSpeed)}, {(double.IsPositiveInfinity(bin.UpperSpeed) ? "∞" : F(bin.UpperSpeed))}); frequency {F(bin.Frequency)} ({F(bin.Percentage)}%); {bin.Count} observations" });
        }
        return marks;
    }
    protected override IReadOnlyList<EngineeringGridLine> BuildGridLines(Rect plot)
    {
        var lines = new List<EngineeringGridLine>();
        for (var ring = 1; ring <= 4; ring++) lines.Add(new(Line(Enumerable.Range(0, 73).Select(i => ProjectPolarFraction(i * 5, ring / 4d, plot)))));
        foreach (var (angle, label) in new[] { (0d, "N"), (90d, "E"), (180d, "S"), (270d, "W") })
            lines.Add(new(Line([ApplyViewport(plot.Center, plot), ProjectPolarFraction(angle, 1, plot)]), label, ProjectPolarFraction(angle, 1.1, plot)));
        return lines;
    }
    protected override void ClearProjection(Exception error)
    { _maximumFrequency = 0; _rose = ChartEngineering.WindRose(Array.Empty<WindObservation>(), Array.Empty<double>(), SectorCount, AngleUnit); }
}
