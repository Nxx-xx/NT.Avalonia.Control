using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>Width-weighted categories containing independently normalized segments.
/// Each mark's Members[0] is the original column; Members[1] is its original segment, whose SourceIndex
/// is the segment index within that column. ColumnGap is visual spacing and reduces painted area.</summary>
public sealed class MekkoChart : ComparisonChartBase
{
    public static readonly StyledProperty<string?> WidthPathProperty = AvaloniaProperty.Register<MekkoChart, string?>(nameof(WidthPath));
    public static readonly StyledProperty<string?> SegmentsPathProperty = AvaloniaProperty.Register<MekkoChart, string?>(nameof(SegmentsPath));
    public static readonly StyledProperty<string?> CategoryPathProperty = LabelPathProperty;
    public static readonly StyledProperty<string?> SegmentValuePathProperty = AvaloniaProperty.Register<MekkoChart, string?>(nameof(SegmentValuePath), "Value");
    public static readonly StyledProperty<string?> SegmentLabelPathProperty = AvaloniaProperty.Register<MekkoChart, string?>(nameof(SegmentLabelPath), "Label");
    public static readonly StyledProperty<string?> SegmentIdPathProperty = AvaloniaProperty.Register<MekkoChart, string?>(nameof(SegmentIdPath));
    public static readonly StyledProperty<double> ColumnGapProperty = AvaloniaProperty.Register<MekkoChart, double>(nameof(ColumnGap), 4, validate: x => double.IsFinite(x) && x >= 0 && x <= 4096);
    public static readonly StyledProperty<bool> ShowPercentagesProperty = AvaloniaProperty.Register<MekkoChart, bool>(nameof(ShowPercentages), true);
    public MekkoChart() => ObservePaths(nameof(MekkoColumn.Width), nameof(MekkoColumn.Segments), nameof(MekkoColumn.Label));
    public string? WidthPath { get => GetValue(WidthPathProperty); set => SetValue(WidthPathProperty, value); }
    public string? SegmentsPath { get => GetValue(SegmentsPathProperty); set => SetValue(SegmentsPathProperty, value); }
    public string? CategoryPath { get => LabelPath; set => LabelPath = value; }
    public string? SegmentValuePath { get => GetValue(SegmentValuePathProperty); set => SetValue(SegmentValuePathProperty, value); }
    public string? SegmentLabelPath { get => GetValue(SegmentLabelPathProperty); set => SetValue(SegmentLabelPathProperty, value); }
    public string? SegmentIdPath { get => GetValue(SegmentIdPathProperty); set => SetValue(SegmentIdPathProperty, value); }
    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }
    public bool ShowPercentages { get => GetValue(ShowPercentagesProperty); set => SetValue(ShowPercentagesProperty, value); }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    { base.OnPropertyChanged(change); if (change.Property == WidthPathProperty || change.Property == SegmentsPathProperty) ObservePaths(WidthPath ?? nameof(MekkoColumn.Width), SegmentsPath ?? nameof(MekkoColumn.Segments), nameof(MekkoColumn.Label)); }
    protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
    {
        BeginScene(); var columns = new MekkoColumn[data.Count]; var children = new IReadOnlyList<object?>[data.Count]; var owners = new List<object?>(); var total = 0;
        try { for (var i = 0; i < data.Count; i++)
        {
            var source = data.Points[i]; var typed = source.Item as MekkoColumn;
            var collection = typed is not null && string.IsNullOrWhiteSpace(SegmentsPath) ? typed.Segments : Read(source.Item, SegmentsPath);
            owners.Add(collection); var segments = Enumerate(collection); children[i] = segments;
            total = checked(total + segments.Count); if (total > ChartAdvancedComparison.MaximumCount) throw new ArgumentOutOfRangeException(nameof(ItemsSource), "Generated segment budget exceeded.");
            var values = new MekkoSegment[segments.Count];
            for (var j = 0; j < segments.Count; j++)
            {
                var child = segments[j]; values[j] = child is MekkoSegment direct ? direct : new(Number(child, SegmentValuePath, out var value) ? value : ChartDataAccessor.TryNumber(child, out var number) ? number : double.NaN, ReadText(child, SegmentLabelPath));
                foreach (var path in new[] { SegmentValuePath, SegmentLabelPath, SegmentIdPath }) owners.AddRange(ChartDataAccessor.Owners(child, path));
            }
            columns[i] = new(typed is not null && string.IsNullOrWhiteSpace(WidthPath) ? typed.Width : Number(source.Item, WidthPath, out var width) ? width : double.NaN, values, ReadText(source.Item, LabelPath) ?? typed?.Label ?? source.Category?.ToString());
        } } finally { ObserveNested(owners); }
        var layout = ChartAdvancedComparison.Mekko(columns); var marks = new List<EngineeringMarkInfo>();
        foreach (var cell in layout.Cells)
        {
            if (cell.Width <= 0 || cell.Height <= 0) continue;
            var gap = plot.Width <= 0 ? cell.Width : Math.Min(cell.Width, ColumnGap / plot.Width); var width = cell.Width - gap; if (width <= 0) continue;
            var column = data.Points[cell.ColumnIndex] with { Point = new(cell.ColumnIndex, columns[cell.ColumnIndex].Width), Value = columns[cell.ColumnIndex].Width, Category = columns[cell.ColumnIndex].Label, IsEmpty = false };
            var item = children[cell.ColumnIndex][cell.SegmentIndex];
            var identity = Read(item, SegmentIdPath) ?? (item is null || item.GetType().IsValueType ? cell.SegmentIndex : item);
            var child = new ChartDataPoint(cell.SegmentIndex, identity, item, cell.Label, new(cell.SegmentIndex, cell.Value), false) { Value = cell.Value, Weight = cell.Height };
            var position = Normalized(cell.X + cell.Width / 2, cell.Y + cell.Height / 2, plot);
            var tooltip = $"{columns[cell.ColumnIndex].Label} / {cell.Label}: {ValueText(cell.Value)} ({cell.Height:P1}); width {ValueText(columns[cell.ColumnIndex].Width)}";
            marks.Add(new(Array.AsReadOnly(new[] { column, child }), Rectangle(cell.X + gap / 2, cell.Y, width, cell.Height, plot), BrushAt(cell.SegmentIndex), position)
            { Key = ("segment", column.StableId, identity), Value = cell.Value, TooltipText = tooltip });
            if (ShowLabels && cell.Height * plot.Height > FontSize + 4) Labels.Add(($"{cell.Label}{(ShowPercentages ? $" {cell.Height:P0}" : ShowValues ? $" {ValueText(cell.Value)}" : "")}", position, Math.Max(1, width * plot.Width - 6)));
        }
        foreach (var group in layout.Cells.GroupBy(c => c.ColumnIndex))
        { var cell = group.First(); Label(columns[cell.ColumnIndex].Label, ApplyViewport(new(plot.Left + plot.Width * (cell.X + cell.Width / 2), plot.Bottom + 12), plot), Math.Max(1, cell.Width * plot.Width - ColumnGap)); }
        return marks;
    }
    protected override IReadOnlyList<EngineeringGridLine> BuildGridLines(Rect plot) => [new(Line([Normalized(0, 0, plot), Normalized(0, 1, plot), Normalized(1, 1, plot)]))];
}
