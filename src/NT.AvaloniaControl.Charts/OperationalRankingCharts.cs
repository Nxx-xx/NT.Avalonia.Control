using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

public abstract class OperationalRankingChartBase : OperationalChartBase
{
    public static readonly StyledProperty<bool> RankFromValuesProperty = AvaloniaProperty.Register<OperationalRankingChartBase, bool>(nameof(RankFromValues), true);
    public static readonly StyledProperty<double> StrokeThicknessProperty = AvaloniaProperty.Register<OperationalRankingChartBase, double>(nameof(StrokeThickness), 2, validate: x => double.IsFinite(x) && x >= 0 && x <= 64);
    public bool RankFromValues { get => GetValue(RankFromValuesProperty); set => SetValue(RankFromValuesProperty, value); }
    public double StrokeThickness { get => GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
    protected abstract bool IsSlope { get; }
    private OperationalRanking? _ranking;
    public OperationalRanking? Ranking { get { _ = GetMarks(); return _ranking; } }
    private int _omittedLabels;
    /// <summary>Endpoint labels omitted because their anchor is outside the plot or the available vertical space cannot hold another line. Every data point is retained.</summary>
    public int OmittedLabelCount { get { _ = GetMarks(); return _omittedLabels; } }
    protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
    {
        BeginScene(); _omittedLabels = 0; var values = data.Points.Select(p => new RankingPoint(Identity(p), Read(p.Item, EntityPath)!, Read(p.Item, PeriodPath)!, Numeric(p, ValuePath), LabelOf(p))).ToArray(); _ranking = ChartOperational.Ranking(values, RankFromValues, IsSlope); var area = new Rect(plot.Left + plot.Width * .12, plot.Top + plot.Height * .06, plot.Width * .76, plot.Height * .88); Point Position(OperationalRank item) => ApplyViewport(new(area.Left + area.Width * item.Position.X, area.Top + area.Height * item.Position.Y), plot); var marks = new List<EngineeringMarkInfo>(); var candidates = new List<(bool Right, string Text, Point Anchor, int Index)>();
        foreach (var group in _ranking.Points.GroupBy(x => x.EntityIndex)) { var points = group.OrderBy(x => x.PeriodIndex).ToArray(); for (var i = 1; i < points.Length; i++) if (points[i].PeriodIndex == points[i - 1].PeriodIndex + 1) Decorations.Add((Line([Position(points[i - 1]), Position(points[i])]), BrushAt(group.Key), StrokeThickness)); foreach (var point in points) { var position = Position(point); var source = data.Points[point.SourceIndex]; marks.Add(Single(source, Disk(position, Math.Max(1, MarkerSize / 2) * Zoom), BrushAt(group.Key), position, Identity(source), point.Source.Value, $"{point.Source.Entity} / {point.Source.Period}: {ValueText(point.Source.Value)}{(IsSlope ? "" : $"; rank {ValueText(point.Rank)}")}")); } if (points.Length > 0) { var first = points[0]; var last = points[^1]; candidates.Add((false, $"{first.Source.Entity} {ValueText(IsSlope ? first.Source.Value : first.Rank)}", Position(first), first.SourceIndex)); if (last.SourceIndex != first.SourceIndex) candidates.Add((true, $"{last.Source.Entity} {ValueText(IsSlope ? last.Source.Value : last.Rank)}", Position(last), last.SourceIndex)); } }
        if (ShowLabels) PlaceEndpointLabels(candidates, plot);
        for (var i = 0; i < _ranking.Periods.Count; i++) AddLabel(_ranking.Periods[i].ToString(), new(area.Left + (i / Math.Max(1d, _ranking.Periods.Count - 1d)) * area.Width, plot.Bottom + 12), Math.Max(1, plot.Width / Math.Max(1, _ranking.Periods.Count))); return marks;
    }
    private void PlaceEndpointLabels(List<(bool Right, string Text, Point Anchor, int Index)> candidates, Rect plot)
    {
        var spacing = Math.Max(1, FontSize * 1.35 + 2); var capacity = Math.Max(0, (int)Math.Floor(plot.Height / spacing)); var width = Math.Max(1, Math.Min(95, plot.Width * .25));
        foreach (var side in candidates.GroupBy(x => x.Right))
        {
            var visible = side.Where(x => plot.Contains(x.Anchor)).OrderBy(x => x.Anchor.Y).ThenBy(x => x.Index).ToArray(); _omittedLabels += side.Count() - visible.Length;
            if (capacity == 0) { _omittedLabels += visible.Length; continue; }
            if (visible.Length > capacity) { _omittedLabels += visible.Length - capacity; visible = Enumerable.Range(0, capacity).Select(i => visible[(int)((long)i * visible.Length / capacity)]).ToArray(); }
            var positions = new double[visible.Length]; var top = plot.Top + spacing / 2; var bottom = plot.Bottom - spacing / 2;
            for (var i = 0; i < visible.Length; i++) positions[i] = Math.Max(Math.Clamp(visible[i].Anchor.Y, top, bottom), i == 0 ? top : positions[i - 1] + spacing);
            var excess = positions.Length == 0 ? 0 : Math.Max(0, positions[^1] - bottom);
            for (var i = 0; i < visible.Length; i++) AddLabel(visible[i].Text, new(side.Key ? plot.Right - width / 2 : plot.Left + width / 2, positions[i] - excess), width);
        }
    }
    protected override void ClearProjection(Exception error) { _ranking = null; _omittedLabels = 0; base.ClearProjection(error); }
}
/// <summary>Period ranks; ties use competition ranking, with missing period pairs left disconnected.</summary>
public sealed class BumpChart : OperationalRankingChartBase { protected override bool IsSlope => false; }
/// <summary>Two-period values on one shared scale; endpoints retain separate original observations.</summary>
public sealed class SlopeChart : OperationalRankingChartBase { protected override bool IsSlope => true; }

/// <summary>Independent, source-bound centered/zero stacked-area host; it does not claim a wiggle baseline.</summary>
public sealed class ThemeRiverChart : OperationalChartBase
{
    public static readonly StyledProperty<OperationalRiverBaseline> BaselineProperty = AvaloniaProperty.Register<ThemeRiverChart, OperationalRiverBaseline>(nameof(Baseline), OperationalRiverBaseline.Centered, validate: Enum.IsDefined);
    public ThemeRiverChart() { RowPath = "Stream"; ColumnPath = "Slot"; }
    public OperationalRiverBaseline Baseline { get => GetValue(BaselineProperty); set => SetValue(BaselineProperty, value); }
    private OperationalRiver? _river;
    public OperationalRiver? River { get { _ = GetMarks(); return _river; } }
    protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
    {
        BeginScene(); var values = data.Points.Select(p => new ThemeRiverPoint(Identity(p), Read(p.Item, RowPath)!, Read(p.Item, ColumnPath)!, Numeric(p, ValuePath), LabelOf(p))).ToArray(); _river = ChartOperational.River(values, Baseline); var marks = new List<EngineeringMarkInfo>(); if (_river.MaximumTotal <= 0) return marks; Point Project(ChartPoint point) => Normalized(_river.Slots.Count < 2 ? .5 : point.X / (_river.Slots.Count - 1), 1 - point.Y / _river.MaximumTotal, plot);
        foreach (var band in _river.Bands)
        {
            if (band.SourceIndices.All(i => values[i].Value == 0)) continue;
            var members = band.SourceIndices.Select(i => Original(data.Points[i], values[i].Value)).ToArray();
            Geometry geometry; Point position;
            if (_river.Slots.Count == 1)
            {
                var lower = band.Lower[0].Y / _river.MaximumTotal; var upper = band.Upper[0].Y / _river.MaximumTotal;
                geometry = Rectangle(0, 1 - upper, 1, upper - lower, plot); position = Normalized(.5, 1 - (lower + upper) / 2, plot);
            }
            else
            {
                var points = band.Upper.Select(Project).Concat(band.Lower.Reverse().Select(Project)).ToArray(); geometry = new PolylineGeometry(points, true);
                // Choose an interior point in the thickest adjacent-slot segment; endpoint boundaries are not a reliable hit location.
                var segment = Enumerable.Range(0, band.Lower.Count - 1).OrderByDescending(i => values[band.SourceIndices[i]].Value / 2 + values[band.SourceIndices[i + 1]].Value / 2).First();
                var centerValue = (band.Lower[segment].Y / 4 + band.Upper[segment].Y / 4) + (band.Lower[segment + 1].Y / 4 + band.Upper[segment + 1].Y / 4);
                position = Project(new(segment + .5, centerValue));
            }
            var total = values.Where(x => Equals(x.Stream, band.Stream)).Sum(x => x.Value); if (!double.IsFinite(total)) throw new OverflowException("River stream summary exceeds finite numeric range.");
            marks.Add(new(Array.AsReadOnly(members), geometry, BrushAt(band.StreamIndex), position) { Key = ("river", band.Stream), Value = total, TooltipText = $"{band.Stream}: {ValueText(total)} ({members.Length} original slots)" }); AddLabel(band.Stream.ToString(), position, plot.Width * .4);
        }
        for (var i = 0; i < _river.Slots.Count; i++) AddLabel(_river.Slots[i].ToString(), new(plot.Left + i / Math.Max(1d, _river.Slots.Count - 1d) * plot.Width, plot.Bottom + 12), Math.Max(1, plot.Width / Math.Max(1, _river.Slots.Count))); return marks;
    }
    protected override void ClearProjection(Exception error) { _river = null; base.ClearProjection(error); }
}
