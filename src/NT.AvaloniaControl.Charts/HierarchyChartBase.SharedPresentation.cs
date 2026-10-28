using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

public abstract partial class HierarchyChartBase
{
    protected override bool IsSharedLegendVisible => ShowLegend;
    protected override bool HasFamilyLegendArea => true;
    protected override Rect GetSharedLegendArea(Rect plot, Size size) => _legendBounds;

    protected override IEnumerable<ChartDataLegendItemContext> CreateLegendItems(IReadOnlyList<EngineeringMarkInfo> marks)
    {
        if (!ShowLegend) yield break;
        foreach (var item in _legendItems)
        {
            var members = new List<ChartDataPoint>();
            foreach (var node in item.Nodes)
                if (_markIndices.TryGetValue(Identity(node), out var index) && index < marks.Count)
                    members.AddRange(marks[index].Members);
                else
                    members.Add(new ChartDataPoint(node.SourceIndex, node.Id ?? node.Source, node.Source, node.Label,
                        new(node.Index, node.Value), false) { Value = node.Value, Weight = GetHierarchyValue(node) });
            yield return new(Array.AsReadOnly(members.ToArray()), $"{item.Label} {F(item.Value)}", _legendBounds.Center)
            {
                Key = item.Category, Kind = ChartDataLegendKind.Category, Brush = item.Brush,
                Bounds = _legendBounds, Value = item.Value, BusinessDataContext = DataContext,
            };
        }
        if (HasSizeKey)
            yield return new(Array.Empty<ChartDataPoint>(), "Relative area 9 : 4 : 1", _legendBounds.Center)
            {
                Key = (this, "relative-area"), Kind = ChartDataLegendKind.Size,
                Brush = Foreground ?? Brushes.Gray, Bounds = _legendBounds,
                Minimum = 1, Maximum = 9, BusinessDataContext = DataContext,
            };
    }

    protected override IEnumerable<ChartDataLabelItemContext> CreateDataLabels(IReadOnlyList<EngineeringMarkInfo> marks)
    {
        if (!ShowLabels && !ShowValues) yield break;
        for (var index = 0; index < marks.Count; index++)
        {
            var mark = marks[index];
            if (!_displayed.TryGetValue(mark, out var glyph) || glyph.IsExiting ||
                mark.Bounds.Width < FontSize * 1.5 || mark.Bounds.Height < FontSize + 4) continue;
            if (SuppressCoveredAncestorLabels && glyph.Kind != GlyphKind.Sector && _displayParents.Contains(glyph.Entry.Index)) continue;
            var text = (ShowLabels ? glyph.Entry.Label : null) ?? "";
            if (ShowValues) text += $"{(text.Length > 0 ? " " : "")}{F(glyph.Entry.AggregateValue)}";
            yield return LabelItem(mark, index, text) with { GeometryClip = mark.Geometry };
        }
    }
}
