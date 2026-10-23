using Avalonia;

namespace NT.AvaloniaControl.Charts;

/// <summary>Impedance or admittance on a Smith reflection plane, retaining physical source quantities.</summary>
public sealed class SmithChart : EngineeringChartBase
{
    public static readonly StyledProperty<string?> ResistancePathProperty = AvaloniaProperty.Register<SmithChart, string?>(nameof(ResistancePath));
    public static readonly StyledProperty<string?> ReactancePathProperty = AvaloniaProperty.Register<SmithChart, string?>(nameof(ReactancePath));
    public static readonly StyledProperty<double> ReferenceImpedanceProperty = AvaloniaProperty.Register<SmithChart, double>(nameof(ReferenceImpedance), 50, validate: x => double.IsFinite(x) && x > 0);
    public static readonly StyledProperty<SmithQuantity> QuantityProperty = AvaloniaProperty.Register<SmithChart, SmithQuantity>(nameof(Quantity), SmithQuantity.Impedance, validate: Enum.IsDefined);
    private static readonly ChartSeriesBounds ReflectionBounds = new(-1, 1, -1, 1);
    public SmithChart() => ObservePaths(nameof(SmithPoint.R), nameof(SmithPoint.X));
    public string? ResistancePath { get => GetValue(ResistancePathProperty); set => SetValue(ResistancePathProperty, value); }
    public string? ReactancePath { get => GetValue(ReactancePathProperty); set => SetValue(ReactancePathProperty, value); }
    public double ReferenceImpedance { get => GetValue(ReferenceImpedanceProperty); set => SetValue(ReferenceImpedanceProperty, value); }
    public SmithQuantity Quantity { get => GetValue(QuantityProperty); set => SetValue(QuantityProperty, value); }
    protected override Rect GetPlotArea(Size size) => Square(base.GetPlotArea(size));
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    { base.OnPropertyChanged(change); if (change.Property == ResistancePathProperty || change.Property == ReactancePathProperty) ObservePaths(ResistancePath ?? nameof(SmithPoint.R), ReactancePath ?? nameof(SmithPoint.X)); }
    /// <summary>Projects ohms (impedance) or siemens (admittance) to chart-local DIPs.</summary>
    public Point Project(SmithPoint value) => CartesianToPlot(ChartEngineering.ProjectSmith(value, ReferenceImpedance, Quantity), ReflectionBounds, PlotArea);
    public SmithPoint Unproject(Point position) => ChartEngineering.UnprojectSmith(PlotToCartesian(position, ReflectionBounds, PlotArea), ReferenceImpedance, Quantity);
    protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
    {
        var marks = new List<EngineeringMarkInfo>();
        foreach (var source in data.Points)
        {
            SmithPoint raw;
            if (source.Item is SmithPoint typed && string.IsNullOrWhiteSpace(ResistancePath) && string.IsNullOrWhiteSpace(ReactancePath)) raw = typed;
            else if (Number(source.Item, ResistancePath, out var r) && Number(source.Item, ReactancePath, out var x)) raw = new(r, x);
            else continue;
            if (!double.IsFinite(raw.R) || raw.R < 0 || !double.IsFinite(raw.X)) continue;
            var projected = ChartEngineering.ProjectSmith(raw, ReferenceImpedance, Quantity);
            var position = CartesianToPlot(projected, ReflectionBounds, plot);
            var unit = Quantity == SmithQuantity.Impedance ? "Ω" : "S";
            marks.Add(Marker(source with { Point = new(raw.R, raw.X), IsEmpty = false }, position,
                $"{source.Category}: {F(raw.R)} + j{F(raw.X)} {unit}; Γ=({F(projected.X)}, {F(projected.Y)}); Z₀={F(ReferenceImpedance)} Ω", raw.R));
        }
        return marks;
    }
    protected override IReadOnlyList<EngineeringGridLine> BuildGridLines(Rect plot) =>
        ChartEngineering.SmithGrid(Quantity).Select(path => new EngineeringGridLine(Line(path.Points.Select(p => CartesianToPlot(p, ReflectionBounds, plot))))).ToArray();
}
