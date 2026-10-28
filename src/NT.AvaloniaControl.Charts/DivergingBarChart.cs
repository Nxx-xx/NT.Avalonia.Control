using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>Bars extending to either side of a shared numeric baseline.</summary>
public sealed class DivergingBarChart : ComparisonChartBase
{
    public static readonly StyledProperty<string?> ValuePathProperty = AvaloniaProperty.Register<DivergingBarChart, string?>(nameof(ValuePath));
    public static readonly StyledProperty<double> BaselineProperty = AvaloniaProperty.Register<DivergingBarChart, double>(nameof(Baseline), 0, validate: double.IsFinite);
    public static readonly StyledProperty<Orientation> OrientationProperty = AvaloniaProperty.Register<DivergingBarChart, Orientation>(nameof(Orientation), Orientation.Horizontal, validate: Enum.IsDefined);
    public static readonly StyledProperty<double> BarHeightProperty = AvaloniaProperty.Register<DivergingBarChart, double>(nameof(BarHeight), .68, validate: x => double.IsFinite(x) && x > 0 && x <= 1);
    public static readonly StyledProperty<IBrush?> NegativeBrushProperty = AvaloniaProperty.Register<DivergingBarChart, IBrush?>(nameof(NegativeBrush));
    public static readonly StyledProperty<IBrush?> PositiveBrushProperty = AvaloniaProperty.Register<DivergingBarChart, IBrush?>(nameof(PositiveBrush));
    private double _zero;
    private double _extent;
    public DivergingBarChart() => ObservePaths(nameof(DivergingValue.Value), nameof(DivergingValue.Label));
    public string? ValuePath { get => GetValue(ValuePathProperty); set => SetValue(ValuePathProperty, value); }
    public double Baseline { get => GetValue(BaselineProperty); set => SetValue(BaselineProperty, value); }
    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    /// <summary>Fraction of each category slot occupied by its bar, in (0,1].</summary>
    public double BarHeight { get => GetValue(BarHeightProperty); set => SetValue(BarHeightProperty, value); }
    public IBrush? NegativeBrush { get => GetValue(NegativeBrushProperty); set => SetValue(NegativeBrushProperty, value); }
    public IBrush? PositiveBrush { get => GetValue(PositiveBrushProperty); set => SetValue(PositiveBrushProperty, value); }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    { base.OnPropertyChanged(change); if (change.Property == ValuePathProperty) ObservePaths(ValuePath ?? nameof(DivergingValue.Value), nameof(DivergingValue.Label)); }
    protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
    {
        BeginScene(); var raw = new DivergingValue[data.Count]; var originalValues = new double[data.Count];
        for (var i = 0; i < data.Count; i++)
        {
            var source = data.Points[i]; var value = source.Item is DivergingValue typed && string.IsNullOrWhiteSpace(ValuePath) ? typed.Value : Number(source.Item, ValuePath, out var mapped) ? mapped : source.Item is IConvertible && ChartDataAccessor.TryNumber(source.Item, out var direct) ? direct : double.NaN;
            var delta = value - Baseline; if (double.IsFinite(value) && !double.IsFinite(delta)) throw new OverflowException("The baseline difference cannot be represented.");
            originalValues[i] = value;
            raw[i] = new(delta, ReadText(source.Item, LabelPath) ?? (source.Item is DivergingValue item ? item.Label : source.Category?.ToString()));
        }
        var layout = ChartAdvancedComparison.Diverging(raw, Sort); _zero = layout.Zero; _extent = raw.Length == 0 ? 0 : raw.Max(item => Math.Abs(item.Value)); var marks = new List<EngineeringMarkInfo>();
        foreach (var row in layout.Rows)
        {
            if (row.Start == row.End) continue; var source = data.Points[row.SourceIndex]; var value = originalValues[row.SourceIndex];
            source = source with { Point = new(row.SourceIndex, value), Value = value, Category = row.Label, IsEmpty = false };
            var low = Math.Min(row.Start, row.End); var span = Math.Abs(row.End - row.Start); var top = row.Y - row.Height * BarHeight / 2; var height = row.Height * BarHeight;
            var geometry = Orientation == Orientation.Horizontal ? Rectangle(low, top, span, height, plot) : Rectangle(top, 1 - low - span, height, span, plot);
            var position = Orientation == Orientation.Horizontal ? Normalized((row.Start + row.End) / 2, row.Y, plot) : Normalized(row.Y, 1 - (row.Start + row.End) / 2, plot);
            marks.Add(Mark(source, geometry, row.Value < 0 ? NegativeBrush ?? BrushAt(1) : PositiveBrush ?? BrushAt(0), position, "bar", value, $"{row.Label}: {ValueText(value)} (baseline {ValueText(Baseline)})"));
            DataLabel(marks[^1], row.Label, ApplyViewport(Orientation == Orientation.Horizontal ? new(plot.Left + 34, plot.Top + plot.Height * row.Y) : new(plot.Left + plot.Width * row.Y, plot.Bottom + 12), plot), 64);
            DataNumberLabel(marks[^1], value, position, Math.Max(16, Orientation == Orientation.Horizontal ? geometry.Bounds.Width - 4 : 60));
        }
        return marks;
    }
    protected override IReadOnlyList<EngineeringGridLine> BuildGridLines(Rect plot)
    {
        return Enumerable.Range(0, 5).Select(index =>
        {
            var fraction = index / 4d; var value = Baseline + (fraction - _zero) * 2 * _extent;
            return new EngineeringGridLine(Line(Orientation == Orientation.Horizontal ? [Normalized(fraction, 0, plot), Normalized(fraction, 1, plot)] : new[] { Normalized(0, 1 - fraction, plot), Normalized(1, 1 - fraction, plot) }), double.IsFinite(value) ? ValueText(value) : null, Orientation == Orientation.Horizontal ? Normalized(fraction, 1, plot) + new Vector(0, 12) : Normalized(0, 1 - fraction, plot) + new Vector(15, 0));
        }).ToArray();
    }
}
