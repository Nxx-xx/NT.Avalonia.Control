using Avalonia;

namespace NT.AvaloniaControl.Charts;

public abstract partial class RelationsChartBase
{
    protected override IEnumerable<ChartDataLabelItemContext> CreateDataLabels(IReadOnlyList<EngineeringMarkInfo> marks)
    {
        for (var index = 0; index < marks.Count; index++)
        {
            var mark = marks[index];
            if (!_painted.TryGetValue(mark, out var item) || item.Label is not { } text) continue;
            if (item.Key.Kind == RelationElementKind.Node && ShowLabels)
                yield return LabelItem(mark, index, text) with { GeometryClip = mark.Geometry };
            else if (item.Key.Kind == RelationElementKind.Edge && ShowEdgeLabels)
                yield return LabelItem(mark, index, text, bounds: new(mark.Position.X - 60, mark.Position.Y - FontSize, 120, FontSize * 2));
        }
    }

    protected override IEnumerable<ChartDataLegendItemContext> CreateLegendItems(IReadOnlyList<EngineeringMarkInfo> marks)
    {
        var nodes = _graphLayout?.Nodes.Select(node => node.Entry) ?? _chordLayout?.Nodes.Select(node => node.Entry) ?? [];
        foreach (var group in nodes.GroupBy(node => node.Node.Group ?? node.Node.Label ?? node.Id.ToString() ?? ""))
        {
            var members = new List<ChartDataPoint>();
            EngineeringMarkInfo? first = null;
            foreach (var node in group)
                if (_indices.TryGetValue(new(NodeKey(node)), out var index) && index < marks.Count)
                {
                    first ??= marks[index]; members.AddRange(marks[index].Members);
                }
            if (first is null) continue;
            yield return new(Array.AsReadOnly(members.ToArray()), group.Key, default)
            {
                Key = (this, group.Key), Kind = ChartDataLegendKind.Category,
                Brush = first.Fill, Value = group.Sum(node => node.Node.Weight), BusinessDataContext = DataContext,
            };
        }
    }
}
