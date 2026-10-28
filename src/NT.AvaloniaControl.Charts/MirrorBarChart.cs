using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>Shared bilateral bars with a common magnitude scale and a nonselectable center gap.</summary>
public abstract class PairedBarChartBase : ComparisonChartBase
{
    public static readonly StyledProperty<string?> LeftValuePathProperty = AvaloniaProperty.Register<PairedBarChartBase, string?>(nameof(LeftValuePath));
    public static readonly StyledProperty<string?> RightValuePathProperty = AvaloniaProperty.Register<PairedBarChartBase, string?>(nameof(RightValuePath));
    public static readonly StyledProperty<double> CenterGapProperty = AvaloniaProperty.Register<PairedBarChartBase, double>(nameof(CenterGap), 12, validate: x => double.IsFinite(x) && x >= 0 && x <= 4096);
    public static readonly StyledProperty<double> BarHeightProperty = AvaloniaProperty.Register<PairedBarChartBase, double>(nameof(BarHeight), .68, validate: x => double.IsFinite(x) && x > 0 && x <= 1);
    public static readonly StyledProperty<string?> LeftTitleProperty = AvaloniaProperty.Register<PairedBarChartBase, string?>(nameof(LeftTitle), "Left");
    public static readonly StyledProperty<string?> RightTitleProperty = AvaloniaProperty.Register<PairedBarChartBase, string?>(nameof(RightTitle), "Right");
    public static readonly StyledProperty<IBrush?> LeftBrushProperty = AvaloniaProperty.Register<PairedBarChartBase, IBrush?>(nameof(LeftBrush));
    public static readonly StyledProperty<IBrush?> RightBrushProperty = AvaloniaProperty.Register<PairedBarChartBase, IBrush?>(nameof(RightBrush));
    private double _maximum;
    protected PairedBarChartBase() => ObservePaths(nameof(PairedComparisonValue.First), nameof(PairedComparisonValue.Second), nameof(PairedComparisonValue.Label));
    public string? LeftValuePath { get => GetValue(LeftValuePathProperty); set => SetValue(LeftValuePathProperty, value); }
    public string? RightValuePath { get => GetValue(RightValuePathProperty); set => SetValue(RightValuePathProperty, value); }
    /// <summary>Unzoomed empty width at the origin, in DIPs; clamped to the available plot width.</summary>
    public double CenterGap { get => GetValue(CenterGapProperty); set => SetValue(CenterGapProperty, value); }
    public double BarHeight { get => GetValue(BarHeightProperty); set => SetValue(BarHeightProperty, value); }
    public string? LeftTitle { get => GetValue(LeftTitleProperty); set => SetValue(LeftTitleProperty, value); }
    public string? RightTitle { get => GetValue(RightTitleProperty); set => SetValue(RightTitleProperty, value); }
    public IBrush? LeftBrush { get => GetValue(LeftBrushProperty); set => SetValue(LeftBrushProperty, value); }
    public IBrush? RightBrush { get => GetValue(RightBrushProperty); set => SetValue(RightBrushProperty, value); }
    protected virtual double CategoryGap => 0;
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    { base.OnPropertyChanged(change); if (change.Property == LeftValuePathProperty || change.Property == RightValuePathProperty) ObservePaths(LeftValuePath ?? nameof(PairedComparisonValue.First), RightValuePath ?? nameof(PairedComparisonValue.Second), nameof(PairedComparisonValue.Label)); }
    protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
    {
        BeginScene(); if (data.Count > ChartAdvancedComparison.MaximumCount / 2) throw new ArgumentOutOfRangeException(nameof(ItemsSource), "Generated bilateral glyph budget exceeded."); var raw = new PairedComparisonValue[data.Count];
        for (var i = 0; i < data.Count; i++)
        {
            var source = data.Points[i];
            raw[i] = source.Item is PairedComparisonValue typed && string.IsNullOrWhiteSpace(LeftValuePath) && string.IsNullOrWhiteSpace(RightValuePath) ? typed : new(Number(source.Item, LeftValuePath, out var left) ? left : double.NaN, Number(source.Item, RightValuePath, out var right) ? right : double.NaN, ReadText(source.Item, LabelPath) ?? source.Category?.ToString());
        }
        var layout = ChartAdvancedComparison.Paired(raw, mirror: true, sort: Sort); _maximum = raw.Length == 0 ? 0 : raw.Max(item => Math.Max(item.First, item.Second)); var marks = new List<EngineeringMarkInfo>();
        var halfGap = plot.Width <= 0 ? .5 : Math.Min(.5, CenterGap / plot.Width / 2); var side = .5 - halfGap;
        foreach (var row in layout.Rows)
        {
            var source = data.Points[row.SourceIndex] with { Point = new(row.First, row.Second), Value = raw[row.SourceIndex], Category = row.Label, Low = row.First, High = row.Second, IsEmpty = false };
            var leftWidth = (.5 - row.FirstPosition) * 2 * side; var rightWidth = (row.SecondPosition - .5) * 2 * side;
            var height = Math.Max(0, row.Height * BarHeight - (plot.Height <= 0 ? row.Height : CategoryGap / plot.Height)); var top = row.Y - height / 2;
            if (height <= 0) continue;
            if (leftWidth > 0)
            {
                var position = Normalized(.5 - halfGap - leftWidth / 2, row.Y, plot);
                marks.Add(Mark(source, Rectangle(.5 - halfGap - leftWidth, top, leftWidth, height, plot), LeftBrush ?? BrushAt(0), position, "left", row.First, $"{row.Label} {LeftTitle}: {ValueText(row.First)}"));
                DataNumberLabel(marks[^1], row.First, position, Math.Max(16, leftWidth * plot.Width - 4));
            }
            if (rightWidth > 0)
            {
                var position = Normalized(.5 + halfGap + rightWidth / 2, row.Y, plot);
                marks.Add(Mark(source, Rectangle(.5 + halfGap, top, rightWidth, height, plot), RightBrush ?? BrushAt(1), position, "right", row.Second, $"{row.Label} {RightTitle}: {ValueText(row.Second)}"));
                DataNumberLabel(marks[^1], row.Second, position, Math.Max(16, rightWidth * plot.Width - 4));
            }
            if (leftWidth > 0 || rightWidth > 0) DataLabel(marks[^1], row.Label, ApplyViewport(new(plot.Center.X, plot.Top + plot.Height * top - 8), plot), Math.Max(40, plot.Width / 3));
        }
        Label(LeftTitle, new(plot.Left + plot.Width / 4, plot.Top - 11), plot.Width / 2); Label(RightTitle, new(plot.Left + plot.Width * .75, plot.Top - 11), plot.Width / 2);
        return marks;
    }
    protected override IReadOnlyList<EngineeringGridLine> BuildGridLines(Rect plot)
    {
        var gap = plot.Width <= 0 ? .5 : Math.Min(.5, CenterGap / plot.Width / 2); var side = .5 - gap;
        var result = new List<EngineeringGridLine> { new(Line([Normalized(.5, 0, plot), Normalized(.5, 1, plot)]), "0", Normalized(.5, 1, plot) + new Vector(0, 12)) };
        foreach (var fraction in new[] { .5, 1d }) foreach (var direction in new[] { -1d, 1d })
        {
            var x = .5 + direction * (gap + side * fraction);
            result.Add(new(Line([Normalized(x, 0, plot), Normalized(x, 1, plot)]), ValueText(_maximum * fraction), Normalized(x, 1, plot) + new Vector(0, 12)));
        }
        return result;
    }
}

/// <summary>Mirror comparison of two nonnegative measurements per original category.</summary>
public sealed class MirrorBarChart : PairedBarChartBase { }
