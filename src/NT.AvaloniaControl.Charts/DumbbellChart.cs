using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>Two original endpoint values joined by a nonselectable connector.</summary>
public sealed class DumbbellChart : ComparisonChartBase
{
    public static readonly StyledProperty<string?> LowValuePathProperty = AvaloniaProperty.Register<DumbbellChart, string?>(nameof(LowValuePath));
    public static readonly StyledProperty<string?> HighValuePathProperty = AvaloniaProperty.Register<DumbbellChart, string?>(nameof(HighValuePath));
    public static readonly StyledProperty<Orientation> OrientationProperty = AvaloniaProperty.Register<DumbbellChart, Orientation>(nameof(Orientation), Orientation.Horizontal, validate: Enum.IsDefined);
    public static readonly StyledProperty<IBrush?> LowBrushProperty = AvaloniaProperty.Register<DumbbellChart, IBrush?>(nameof(LowBrush));
    public static readonly StyledProperty<IBrush?> HighBrushProperty = AvaloniaProperty.Register<DumbbellChart, IBrush?>(nameof(HighBrush));
    public static readonly StyledProperty<IBrush?> ConnectorBrushProperty = AvaloniaProperty.Register<DumbbellChart, IBrush?>(nameof(ConnectorBrush));
    public static readonly StyledProperty<double> ConnectorThicknessProperty = AvaloniaProperty.Register<DumbbellChart, double>(nameof(ConnectorThickness), 2, validate: x => double.IsFinite(x) && x >= 0 && x <= 64);
    private double _minimum, _maximum;
    public DumbbellChart() => ObservePaths(nameof(PairedComparisonValue.First), nameof(PairedComparisonValue.Second), nameof(PairedComparisonValue.Label));
    public string? LowValuePath { get => GetValue(LowValuePathProperty); set => SetValue(LowValuePathProperty, value); }
    public string? HighValuePath { get => GetValue(HighValuePathProperty); set => SetValue(HighValuePathProperty, value); }
    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    public IBrush? LowBrush { get => GetValue(LowBrushProperty); set => SetValue(LowBrushProperty, value); }
    public IBrush? HighBrush { get => GetValue(HighBrushProperty); set => SetValue(HighBrushProperty, value); }
    public IBrush? ConnectorBrush { get => GetValue(ConnectorBrushProperty); set => SetValue(ConnectorBrushProperty, value); }
    public double ConnectorThickness { get => GetValue(ConnectorThicknessProperty); set => SetValue(ConnectorThicknessProperty, value); }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    { base.OnPropertyChanged(change); if (change.Property == LowValuePathProperty || change.Property == HighValuePathProperty) ObservePaths(LowValuePath ?? nameof(PairedComparisonValue.First), HighValuePath ?? nameof(PairedComparisonValue.Second), nameof(PairedComparisonValue.Label)); }
    protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
    {
        BeginScene(); if (data.Count > ChartAdvancedComparison.MaximumCount / 2) throw new ArgumentOutOfRangeException(nameof(ItemsSource), "Generated endpoint budget exceeded."); var raw = new PairedComparisonValue[data.Count];
        for (var i = 0; i < data.Count; i++)
        {
            var source = data.Points[i];
            raw[i] = source.Item is PairedComparisonValue typed && string.IsNullOrWhiteSpace(LowValuePath) && string.IsNullOrWhiteSpace(HighValuePath) ? typed : new(Number(source.Item, LowValuePath, out var low) ? low : double.NaN, Number(source.Item, HighValuePath, out var high) ? high : double.NaN, ReadText(source.Item, LabelPath) ?? source.Category?.ToString());
        }
        var layout = ChartAdvancedComparison.Paired(raw, sort: Sort); _minimum = raw.Length == 0 ? 0 : Math.Min(0, raw.Min(item => Math.Min(item.First, item.Second))); _maximum = raw.Length == 0 ? 0 : Math.Max(0, raw.Max(item => Math.Max(item.First, item.Second))); var marks = new List<EngineeringMarkInfo>();
        foreach (var row in layout.Rows)
        {
            Point Position(double x) => Orientation == Orientation.Horizontal ? Normalized(x, row.Y, plot) : Normalized(row.Y, 1 - x, plot);
            var a = Position(row.FirstPosition); var b = Position(row.SecondPosition);
            var distance = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y)); var inset = MarkerSize * Zoom / 2;
            if (distance > 2 * inset && ConnectorThickness > 0)
            {
                var vector = (b - a) / distance;
                Decorations.Add((Line([a + vector * inset, b - vector * inset]), ConnectorBrush ?? GridLineBrush, ConnectorThickness));
            }
            var source = data.Points[row.SourceIndex] with { Point = new(row.First, row.Second), Value = raw[row.SourceIndex], Low = row.First, High = row.Second, Category = row.Label, IsEmpty = false };
            foreach (var end in new[] { (a, row.First, "low", LowBrush ?? BrushAt(0)), (b, row.Second, "high", HighBrush ?? BrushAt(1)) })
            {
                if (MarkerSize <= 0) continue;
                var geometry = new EllipseGeometry(new Rect(end.Item1.X - MarkerSize * Zoom / 2, end.Item1.Y - MarkerSize * Zoom / 2, MarkerSize * Zoom, MarkerSize * Zoom));
                marks.Add(Mark(source, geometry, end.Item4, end.Item1, end.Item3, end.Item2, $"{row.Label} {end.Item3}: {ValueText(end.Item2)}; Δ {ValueText(row.Second - row.First)}"));
                NumberLabel(end.Item2, new(end.Item1.X, end.Item1.Y - MarkerSize * Zoom / 2 - 9), 64);
            }
            Label(row.Label, ApplyViewport(Orientation == Orientation.Horizontal ? new(plot.Left + 30, plot.Top + plot.Height * row.Y + 14) : new(plot.Left + plot.Width * row.Y, plot.Bottom + 12), plot), 64);
        }
        return marks;
    }
    protected override IReadOnlyList<EngineeringGridLine> BuildGridLines(Rect plot) => Enumerable.Range(0, 5).Select(index =>
    {
        var fraction = index / 4d;
        return new EngineeringGridLine(Line(Orientation == Orientation.Horizontal ? [Normalized(fraction, 0, plot), Normalized(fraction, 1, plot)] : new[] { Normalized(0, 1 - fraction, plot), Normalized(1, 1 - fraction, plot) }), ValueText(Interpolate(_minimum, _maximum, fraction)), Orientation == Orientation.Horizontal ? Normalized(fraction, 1, plot) + new Vector(0, 12) : Normalized(0, 1 - fraction, plot) + new Vector(15, 0));
    }).ToArray();
}
