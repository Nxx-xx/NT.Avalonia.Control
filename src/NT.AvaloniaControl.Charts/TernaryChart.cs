using Avalonia;

namespace NT.AvaloniaControl.Charts;

/// <summary>Normalized three-component mixtures on an equilateral simplex; raw components stay in Members.</summary>
public sealed class TernaryChart : EngineeringChartBase
{
    public static readonly StyledProperty<string?> APathProperty = AvaloniaProperty.Register<TernaryChart, string?>(nameof(APath));
    public static readonly StyledProperty<string?> BPathProperty = AvaloniaProperty.Register<TernaryChart, string?>(nameof(BPath));
    public static readonly StyledProperty<string?> CPathProperty = AvaloniaProperty.Register<TernaryChart, string?>(nameof(CPath));
    public static readonly StyledProperty<string> ALabelProperty = AvaloniaProperty.Register<TernaryChart, string>(nameof(ALabel), "A");
    public static readonly StyledProperty<string> BLabelProperty = AvaloniaProperty.Register<TernaryChart, string>(nameof(BLabel), "B");
    public static readonly StyledProperty<string> CLabelProperty = AvaloniaProperty.Register<TernaryChart, string>(nameof(CLabel), "C");
    private static readonly ChartSeriesBounds SimplexBounds = new(0, 1, 0, Math.Sqrt(3) / 2);
    public TernaryChart() => ObservePaths(nameof(TernaryPoint.A), nameof(TernaryPoint.B), nameof(TernaryPoint.C));
    public string? APath { get => GetValue(APathProperty); set => SetValue(APathProperty, value); }
    public string? BPath { get => GetValue(BPathProperty); set => SetValue(BPathProperty, value); }
    public string? CPath { get => GetValue(CPathProperty); set => SetValue(CPathProperty, value); }
    public string ALabel { get => GetValue(ALabelProperty); set => SetValue(ALabelProperty, value); }
    public string BLabel { get => GetValue(BLabelProperty); set => SetValue(BLabelProperty, value); }
    public string CLabel { get => GetValue(CLabelProperty); set => SetValue(CLabelProperty, value); }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    { base.OnPropertyChanged(change); if (change.Property == APathProperty || change.Property == BPathProperty || change.Property == CPathProperty) ObservePaths(APath ?? nameof(TernaryPoint.A), BPath ?? nameof(TernaryPoint.B), CPath ?? nameof(TernaryPoint.C)); }
    protected override Rect GetPlotArea(Size size)
    {
        var plot = base.GetPlotArea(size); var width = Math.Min(plot.Width, plot.Height * 2 / Math.Sqrt(3)); var height = width * Math.Sqrt(3) / 2;
        return new(plot.Center.X - width / 2, plot.Center.Y - height / 2, width, height);
    }
    /// <summary>Normalizes nonnegative components and projects them to chart-local DIPs.</summary>
    public Point Project(TernaryPoint value) => CartesianToPlot(ChartEngineering.ProjectTernary(value), SimplexBounds, PlotArea);
    /// <summary>Returns normalized components. The source total is not inferable from a screen point.</summary>
    public TernaryPoint Unproject(Point position) => ChartEngineering.UnprojectTernary(PlotToCartesian(position, SimplexBounds, PlotArea));
    protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
    {
        var marks = new List<EngineeringMarkInfo>();
        foreach (var source in data.Points)
        {
            TernaryPoint raw;
            if (source.Item is TernaryPoint typed && string.IsNullOrWhiteSpace(APath) && string.IsNullOrWhiteSpace(BPath) && string.IsNullOrWhiteSpace(CPath)) raw = typed;
            else if (Number(source.Item, APath, out var a) && Number(source.Item, BPath, out var b) && Number(source.Item, CPath, out var c)) raw = new(a, b, c);
            else continue;
            if (!double.IsFinite(raw.A) || !double.IsFinite(raw.B) || !double.IsFinite(raw.C) || raw.A < 0 || raw.B < 0 || raw.C < 0 || Math.Max(raw.A, Math.Max(raw.B, raw.C)) <= 0) continue;
            var normalized = ChartEngineering.NormalizeTernary(raw);
            marks.Add(Marker(source with { Point = new(raw.A, raw.B), Value = raw, IsEmpty = false }, CartesianToPlot(ChartEngineering.ProjectTernary(raw), SimplexBounds, plot),
                $"{source.Category}: {ALabel}={F(raw.A)}, {BLabel}={F(raw.B)}, {CLabel}={F(raw.C)}; ({F(normalized.A * 100)}%, {F(normalized.B * 100)}%, {F(normalized.C * 100)}%)", normalized.A));
        }
        return marks;
    }
    protected override IReadOnlyList<EngineeringGridLine> BuildGridLines(Rect plot)
    {
        Point P(TernaryPoint p) => CartesianToPlot(ChartEngineering.ProjectTernary(p), SimplexBounds, plot);
        var a = P(new(1, 0, 0)); var b = P(new(0, 1, 0)); var c = P(new(0, 0, 1));
        var lines = new List<EngineeringGridLine> { new(Line([a, b, c, a])), new(Line([a]), ALabel, new(a.X, a.Y - 12)), new(Line([b]), BLabel, new(b.X - 12, b.Y + 8)), new(Line([c]), CLabel, new(c.X + 12, c.Y + 8)) };
        for (var i = 1; i < 10; i++)
        {
            var t = i / 10d;
            lines.Add(new(Line([P(new(t, 1 - t, 0)), P(new(t, 0, 1 - t))])));
            lines.Add(new(Line([P(new(1 - t, t, 0)), P(new(0, t, 1 - t))])));
            lines.Add(new(Line([P(new(1 - t, 0, t)), P(new(0, 1 - t, t))])));
        }
        return lines;
    }
}
