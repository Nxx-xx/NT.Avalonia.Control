using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

public partial class ShapeMap
{
    protected override bool IsSharedLegendVisible => ShowLegend;
    protected override bool HasFamilyLegendArea => true;
    protected override Rect GetSharedLegendArea(Rect plot, Size size) => new(plot.Left, plot.Bottom + 4, plot.Width, Math.Min(60, Math.Max(0, size.Height - Padding.Bottom - 24 - plot.Bottom - 4)));

    protected override IEnumerable<ChartDataLabelItemContext> CreateDataLabels(IReadOnlyList<EngineeringMarkInfo> marks)
    {
        if (!ShowLabels) yield break;
        var indices = new Dictionary<EngineeringMarkInfo, int>(ReferenceEqualityComparer.Instance);
        for (var index = 0; index < marks.Count; index++) indices[marks[index]] = index;
        foreach (var label in _labels)
            if (label.Mark is { } mark && indices.TryGetValue(mark, out var index))
                yield return LabelItem(mark, index, label.Text, label.Position,
                    new Rect(label.Position.X - Math.Min(160, PlotArea.Width) / 2, label.Position.Y - FontSize, Math.Min(160, PlotArea.Width), FontSize * 2));
    }

    protected override IEnumerable<ChartDataLegendItemContext> CreateLegendItems(IReadOnlyList<EngineeringMarkInfo> marks)
    {
        if (!ShowLegend) yield break;
        foreach (var legend in _legends)
        {
            var members = LayerMembers(marks, legend.Layer, null);
            var brush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, .5, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, .5, RelativeUnit.Relative),
                Opacity = legend.Layer.Opacity,
                GradientStops = [new GradientStop(legend.Minimum, 0), new GradientStop(legend.Maximum, 1)],
            };
            yield return new(members, $"{F(legend.Low)} — {F(legend.High)}", default)
            {
                Key = (legend.Layer, "continuous"), Kind = ChartDataLegendKind.Continuous,
                Brush = brush, Value = legend.High, Minimum = legend.Low, Maximum = legend.High,
                BusinessDataContext = DataContext,
            };
        }
        foreach (var legend in _categoryLegends)
            yield return new(LayerMembers(marks, legend.Layer, legend.Category), legend.Category.ToString() ?? "", default)
            {
                Key = (legend.Layer, legend.Category), Kind = ChartDataLegendKind.Category,
                Brush = legend.Brush, BusinessDataContext = DataContext,
            };
    }

    private IReadOnlyList<ChartDataPoint> LayerMembers(IReadOnlyList<EngineeringMarkInfo> marks, MapLayer layer, object? category)
    {
        var input = _inputs.FirstOrDefault(input => ReferenceEquals(input.Layer, layer));
        var members = new List<ChartDataPoint>();
        var seen = new HashSet<(int Index, object? Id)>();
        var regions = _layerSnapshots.FirstOrDefault(state => ReferenceEquals(state.Layer, layer))?.Regions;
        if (layer is ShapeLayer shape && regions is not null)
        {
            var painted = new Dictionary<object, ChartDataPoint>();
            foreach (var mark in marks)
                if (mark.Key is MapGlyphKey { Kind: MapElementKind.Region } key && ReferenceEquals(key.Layer, layer))
                    foreach (var member in mark.Members) if (member.StableId is { } id) painted.TryAdd(id, member);
            // The color domain includes valid joined observations whose geographic
            // projection collapses. Keep those real observations in the scale key.
            foreach (var region in regions.Regions)
            {
                if (region.Value is null || region.Data is not { } row) continue;
                if (category is not null && (input is null || !Equals(input.Categories.GetValueOrDefault(row.RegionId), category))) continue;
                if (painted.TryGetValue(row.RegionId, out var member)) members.Add(member);
                else
                {
                    var value = shape.ColorMode == MapColorMode.Categorical
                        ? ChartDataAccessor.TryNumber(Read(row.Source, shape.ValuePath), out var numeric) ? numeric : (double?)null
                        : row.Value;
                    members.Add(new ChartDataPoint(row.SourceIndex, row.RegionId, row.Source, region.Feature.Label ?? row.RegionId.ToString(), default, false)
                    { Value = value, Weight = value });
                }
            }
            return Array.AsReadOnly(members.ToArray());
        }
        foreach (var mark in marks)
        {
            if (mark.Key is not MapGlyphKey key || !ReferenceEquals(key.Layer, layer)) continue;
            foreach (var member in mark.Members)
            {
                if (category is not null && (input is null || member.StableId is null ||
                    !Equals(input.Categories.GetValueOrDefault(member.StableId), category))) continue;
                if (seen.Add((member.SourceIndex, member.StableId))) members.Add(member);
            }
        }
        return Array.AsReadOnly(members.ToArray());
    }
}
