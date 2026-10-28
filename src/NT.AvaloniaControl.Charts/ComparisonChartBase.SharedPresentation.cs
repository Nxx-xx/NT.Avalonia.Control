using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

public abstract partial class ComparisonChartBase
{
    private readonly Dictionary<int, (EngineeringMarkInfo Mark, IBrush? Brush)> _dataLabelMarks = [];

    protected void DataLabel(EngineeringMarkInfo mark, string? text, Point position, double width)
    { if (ShowLabels && !string.IsNullOrEmpty(text)) AddDataLabel(mark, text, position, width); }
    protected void DataNumberLabel(EngineeringMarkInfo mark, double value, Point position, double width)
    { if (ShowValues) AddDataLabel(mark, ValueText(value), position, width); }
    protected void AddDataLabel(EngineeringMarkInfo mark, string text, Point position, double width, IBrush? brush = null)
    {
        _dataLabelMarks.Add(Labels.Count, (mark, brush));
        Labels.Add((text, position, width));
    }
    protected bool IsTemplatedDataLabel(int index) => UsesDataLabelTemplate && _dataLabelMarks.ContainsKey(index);

    protected override IEnumerable<ChartDataLabelItemContext> CreateDataLabels(IReadOnlyList<EngineeringMarkInfo> marks)
    {
        if (!ShowLabels && !ShowValues) yield break;
        if (_dataLabelMarks.Count == 0)
        {
            if (this is not (CategoricalDistributionChartBase or CalendarHeatmapChart or ParallelCoordinatesChart)) yield break;
            foreach (var item in base.CreateDataLabels(marks))
            {
                var data = item.Members[0];
                var label = this is CalendarHeatmapChart calendar ? Read(data.Item, calendar.DatePath)?.ToString() : data.Category?.ToString();
                var value = data.Value is IReadOnlyList<double> values ? string.Join(", ", values.Select(ValueText)) : ValueText(item.Value);
                yield return item with { Text = string.Join(" ", new[] { ShowLabels ? label : null, ShowValues ? value : null }.Where(text => !string.IsNullOrEmpty(text))) };
            }
            yield break;
        }
        var indices = new Dictionary<EngineeringMarkInfo, int>(ReferenceEqualityComparer.Instance);
        for (var index = 0; index < marks.Count; index++) indices[marks[index]] = index;
        for (var ordinal = 0; ordinal < Labels.Count; ordinal++)
        {
            if (!_dataLabelMarks.TryGetValue(ordinal, out var entry) || !indices.TryGetValue(entry.Mark, out var index)) continue;
            var label = Labels[ordinal];
            var item = LabelItem(entry.Mark, index, label.Text, label.Position,
                new Rect(label.Position.X - Math.Max(0, label.Width) / 2, label.Position.Y - FontSize, Math.Max(0, label.Width), FontSize * 2));
            yield return entry.Brush is null ? item : item with { Brush = entry.Brush };
        }
    }

}
