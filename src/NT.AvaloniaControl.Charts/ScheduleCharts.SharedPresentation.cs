using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

public sealed partial class GanttChart
{
    protected override IEnumerable<ChartDataLabelItemContext> CreateDataLabels(IReadOnlyList<EngineeringMarkInfo> marks)
    {
        if (!ShowLabels) yield break;
        for (var index = 0; index < marks.Count; index++)
        {
            var mark = marks[index];
            if (mark.Key is not { } id || !_byId.TryGetValue(id, out var entry) || !_bars.TryGetValue(id, out var bounds)) continue;
            yield return LabelItem(mark, index, entry.Task.Label ?? entry.Id.ToString() ?? "", bounds.Center, bounds) with
            { GeometryClip = new RectangleGeometry(PlotArea) };
        }
    }
}

public sealed partial class EventTimelineChart
{
    protected override IEnumerable<ChartDataLabelItemContext> CreateDataLabels(IReadOnlyList<EngineeringMarkInfo> marks)
    {
        if (!ShowLabels) yield break;
        var indices = new Dictionary<object, int>();
        for (var index = 0; index < marks.Count; index++) if (marks[index].Key is { } key) indices.Add(key, index);
        foreach (var label in _labels)
            if (label.IsVisible && indices.TryGetValue(label.Id, out var index))
                yield return LabelItem(marks[index], index, label.Text, label.Bounds.Center, label.Bounds) with
                { GeometryClip = new RectangleGeometry(PlotArea) };
    }
}

public sealed partial class SpiralTimelineChart
{
    protected override IEnumerable<ChartDataLabelItemContext> CreateDataLabels(IReadOnlyList<EngineeringMarkInfo> marks)
    {
        if (!ShowLabels) yield break;
        for (var index = 0; index < marks.Count && index < _labels.Count; index++)
        {
            var (point, text) = _labels[index];
            var anchor = new Point(point.X, point.Y - MarkerSize - FontSize / 2);
            yield return LabelItem(marks[index], index, text, anchor, new(anchor.X - 48, anchor.Y - FontSize, 96, FontSize * 2)) with
            { GeometryClip = new RectangleGeometry(PlotArea) };
        }
    }
}

public sealed partial class SwimlaneChart
{
    protected override IEnumerable<ChartDataLabelItemContext> CreateDataLabels(IReadOnlyList<EngineeringMarkInfo> marks)
    {
        if (!ShowLabels) yield break;
        var indices = new Dictionary<EngineeringMarkInfo, int>(ReferenceEqualityComparer.Instance);
        for (var index = 0; index < marks.Count; index++) indices.Add(marks[index], index);
        foreach (var (mark, text) in _labels)
            if (indices.TryGetValue(mark, out var index))
                yield return LabelItem(mark, index, text) with { GeometryClip = new RectangleGeometry(PlotArea) };
    }
}
