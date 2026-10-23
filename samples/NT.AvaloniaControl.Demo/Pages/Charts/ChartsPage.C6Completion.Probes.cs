using System.Collections;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private readonly List<object> _c6Probes = [];
    private static readonly JsonSerializerOptions C6ProbeJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new JsonStringEnumConverter() } };
    private static object? C6Json(object? value) => value is null ? null : JsonSerializer.SerializeToElement(value, value.GetType(), C6ProbeJson);
    private static object? C6Raw(object? value)
    {
        if (value is null) return null;
        if (value is EngineeringChartBase control) return new { type = control.GetType().Name, scalarControlSource = true };
        var element = JsonSerializer.SerializeToElement(value, value.GetType(), C6ProbeJson);
        if (element.ValueKind != JsonValueKind.Object) return element;
        var result = element.EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value.Clone()); result["type"] = value.GetType().Name;
        switch (value)
        {
            case ScheduleTask task: result["startUtcTicks"] = task.Start.UtcTicks; result["endUtcTicks"] = task.End.UtcTicks; result["dependencies"] = task.Dependencies?.Select(d => new { predecessorId = d.PredecessorId.ToString(), type = d.Type.ToString(), lagTicks = d.Lag.Ticks }).ToArray(); break;
            case ScheduleEvent ev: result["dateUtcTicks"] = ev.Date.UtcTicks; break;
            case HierarchyNode node: result["children"] = node.Children.Select(C6Raw).ToArray(); break;
            case MapPie pie: result["slices"] = pie.Slices.Select((slice, ordinal) => new { sliceIndex = ordinal, value = slice.Value, label = slice.Label, source = C6Raw(slice.Source) }).ToArray(); break;
        }
        return result;
    }
    private static object C6Point(Point point) => new { x = point.X, y = point.Y };
    private static object C6Point(ChartPoint point) => new { x = point.X, y = point.Y };
    private static object C6Rect(Rect rect) => new { x = rect.X, y = rect.Y, width = rect.Width, height = rect.Height };
    private static uint? C6Argb(IBrush? brush) => brush is ISolidColorBrush solid ? ((uint)solid.Color.A << 24) | ((uint)solid.Color.R << 16) | ((uint)solid.Color.G << 8) | solid.Color.B : null;
    private static double? C6StrokeWidth(EngineeringChartBase chart, EngineeringMarkInfo mark)
    {
        if (mark.Key is MapGlyphKey { Layer: VectorLayer vector }) return mark.Members[0].Item is MapPolygonVector or MapCircleVector ? 0 : vector.StrokeThickness * chart.Zoom;
        if (mark.Key is not MapGlyphKey { Layer: LineLayer line } || chart is not ShapeMap map) return null;
        var maximum = map.LayerSnapshots.First(s => ReferenceEquals(s.Layer, line)).Connections!.Connections.Max(c => c.Entry.Connection.Value);
        return (maximum == 0 ? line.MinimumThickness : line.MinimumThickness + (line.MaximumThickness - line.MinimumThickness) * (mark.Value / maximum)) * chart.Zoom;
    }
    private static object[] C6Rows(IEnumerable? rows) => rows is null ? [] : rows.Cast<object?>().Select(C6Raw).Cast<object>().ToArray();
    private static IReadOnlyList<object> C6OriginalItems(EngineeringChartBase chart)
    {
        var rows = new List<object>();
        if (chart is ShapeMap map)
        {
            foreach (var layer in map.Layers) if (layer.ItemsSource is { } source) foreach (var item in source) if (item is not null) rows.Add(item);
        }
        else if (chart.ItemsSource is { } source)
        {
            foreach (var item in source) if (item is not null) Add(item);
        }
        return rows;
        void Add(object item) { rows.Add(item); if (item is HierarchyNode node) foreach (var child in node.Children) Add(child); }
    }
    private static object C6Mark(EngineeringChartBase chart, EngineeringMarkInfo mark, int index)
    {
        var original = C6OriginalItems(chart);
        return new
        {
            index, kind = mark.Key is MapGlyphKey mapGlyph ? mapGlyph.Kind.ToString() : chart.GetType().Name, key = mark.Key?.ToString(), keyType = mark.Key?.GetType().Name,
            mapKey = mark.Key is MapGlyphKey key && chart is ShapeMap map ? new { kind = key.Kind.ToString(), layerIndex = map.Layers.IndexOf(key.Layer), layerId = "layer-" + map.Layers.IndexOf(key.Layer), identity = key.Identity.ToString(), hasExplicitId = key.HasExplicitId,
                rowId = key.Identity is ValueTuple<object, int> tuple ? tuple.Item1.ToString() : key.Identity.ToString(), sliceIndex = key.Identity is ValueTuple<object, int> sliceKey ? sliceKey.Item2 : (int?)null } : null,
            value = mark.Value, position = C6Point(mark.Position), bounds = C6Rect(mark.Bounds), fill = mark.Fill?.ToString(), fillArgb = C6Argb(mark.Fill), fillOpacity = mark.Fill?.Opacity, stroke = mark.Stroke?.ToString(), strokeArgb = C6Argb(mark.Stroke), strokeOpacity = mark.Stroke?.Opacity, tooltip = mark.TooltipText,
            width = C6StrokeWidth(chart, mark),
            members = mark.Members.Select(member => new { sourceIndex = member.SourceIndex, stableId = member.StableId?.ToString(), stableIdType = member.StableId?.GetType().Name,
                value = C6Json(member.Value), weight = member.Weight, sizeValue = member.SizeValue, markerDiameter = member.MarkerDiameter, point = C6Point(member.Point),
                rawId = member.StableId?.ToString(), rawValue = C6Json(member.Item?.GetType().GetProperty("Value")?.GetValue(member.Item) ?? member.Item?.GetType().GetProperty("Weight")?.GetValue(member.Item)),
                originalSourceReference = original.Any(item => ReferenceEquals(item, member.Item)), itemReferenceIsRawSource = original.Any(item => ReferenceEquals(item, member.Item)), item = C6Raw(member.Item) }).ToArray(),
        };
    }
    private static object? C6Hit(EngineeringChartBase chart, EngineeringPointHit? hit) => hit is null ? null : C6Mark(chart, hit.Mark, hit.Index);
    private static object C6MapLayer(MapLayerSnapshot layer, int index)
    {
        var value = layer.Layer;
        return new
        {
            index, kind = value.GetType().Name,
            rawItems = C6Rows(value.ItemsSource),
            config = new { isVisible = value.IsVisible, opacity = value.Opacity, idPath = value.IdPath, regionPath = value.RegionPath, valuePath = value.ValuePath, longitudePath = value.LongitudePath,
                latitudePath = value.LatitudePath, labelPath = value.LabelPath, fillPath = value.FillPath, fill = value.Fill?.ToString(), stroke = value.Stroke?.ToString(),
                markerSize = (value as MarkerLayer)?.MarkerSize, markerType = (value as MarkerLayer)?.MarkerType.ToString(),
                minimumThickness = (value as LineLayer)?.MinimumThickness, maximumThickness = (value as LineLayer)?.MaximumThickness, isCurved = (value as LineLayer)?.IsCurved, showEndpoints = (value as LineLayer)?.ShowEndpoints,
                fromLongitudePath = (value as LineLayer)?.FromLongitudePath, fromLatitudePath = (value as LineLayer)?.FromLatitudePath, toLongitudePath = (value as LineLayer)?.ToLongitudePath, toLatitudePath = (value as LineLayer)?.ToLatitudePath,
                strokeThickness = (value as VectorLayer)?.StrokeThickness, radius = (value as PieLayer)?.Radius, slicesPath = (value as PieLayer)?.SlicesPath, sliceValuePath = (value as PieLayer)?.SliceValuePath, sliceLabelPath = (value as PieLayer)?.SliceLabelPath,
                colorMode = (value as ShapeLayer)?.ColorMode.ToString(), categoryPath = (value as ShapeLayer)?.CategoryPath,
                categoryColors = (value as ShapeLayer)?.CategoryColors?.Select(pair => new { category = pair.Key.ToString(), brush = pair.Value.ToString() }).ToArray(),
                minimumColor = (value as ShapeLayer)?.MinimumColor.ToString(), maximumColor = (value as ShapeLayer)?.MaximumColor.ToString(), noDataBrush = (value as ShapeLayer)?.NoDataBrush?.ToString() },
            regions = layer.Regions is { } regions ? new { noDataCount = regions.NoDataCount, regions = regions.Regions.Select(region => new { featureId = region.Feature.Id.ToString(), featureIndex = region.Feature.Index, value = region.Value, isNoData = region.IsNoData, sourceIndex = region.Data?.SourceIndex, source = C6Json(region.Data?.Source) }).ToArray(), unknownValues = regions.UnknownValues.Select(entry => new { sourceIndex = entry.SourceIndex, regionId = entry.RegionId.ToString(), value = entry.Value, source = C6Json(entry.Source) }).ToArray() } : null,
            points = layer.Points?.Points.Select(point => new { sourceIndex = point.Entry.SourceIndex, id = point.Entry.Id.ToString(), source = C6Json(point.Entry.Source), value = point.Entry.Value, position = C6Point(point.Position) }).ToArray(),
            connections = layer.Connections?.Connections.Select(connection => new { sourceIndex = connection.Entry.SourceIndex, id = connection.Entry.Id.ToString(), source = C6Json(connection.Entry.Source), value = connection.Entry.Connection.Value, segments = connection.Segments.Select(segment => new { start = C6Point(segment.Start), end = C6Point(segment.End) }).ToArray() }).ToArray(),
            vectors = layer.Vectors?.Vectors.Select(vector => new { sourceIndex = vector.Entry.SourceIndex, id = vector.Entry.Id.ToString(), kind = vector.Entry.Vector.GetType().Name, rawVector = C6Json(vector.Entry.Vector), source = C6Json(vector.Entry.Source),
                segments = vector.Segments.Select(segment => new { start = C6Point(segment.Start), end = C6Point(segment.End) }).ToArray(), polygons = vector.Polygons.Select(polygon => new { outer = polygon.Outer.Select(C6Point).ToArray(), holes = polygon.Holes.Select(hole => hole.Select(C6Point).ToArray()).ToArray() }).ToArray(), center = vector.Center is { } center ? C6Point(center) : null }).ToArray(),
            pies = layer.Pies?.Slices.Select(slice => new { sourceIndex = slice.Entry.SourceIndex, id = slice.Entry.Id.ToString(), source = C6Json(slice.Entry.Source), sliceIndex = slice.SliceIndex, slice = C6Json(slice.Slice), position = C6Point(slice.Position), startAngle = slice.StartAngle, sweepAngle = slice.SweepAngle }).ToArray(),
        };
    }
    private static object C6Config(EngineeringChartBase chart)
    {
        var config = new Dictionary<string, object?> { ["zoom"] = chart.Zoom, ["panX"] = chart.PanX, ["panY"] = chart.PanY, ["showGrid"] = chart.ShowGrid, ["title"] = chart.Title,
            ["markerSize"] = chart.MarkerSize, ["background"] = chart.Background?.ToString(), ["foreground"] = chart.Foreground?.ToString(), ["fontFamily"] = chart.FontFamily.ToString(), ["fontSize"] = chart.FontSize,
            ["fontWeight"] = chart.FontWeight.ToString(), ["fontStyle"] = chart.FontStyle.ToString(), ["flowDirection"] = chart.FlowDirection.ToString(), ["culture"] = CultureInfo.CurrentCulture.Name, ["uiCulture"] = CultureInfo.CurrentUICulture.Name,
            ["palette"] = chart.Palette?.Select(brush => brush.ToString()).ToArray(), ["idPath"] = chart.IdPath, ["labelPath"] = chart.LabelPath, ["tooltipEnabled"] = chart.IsTooltipEnabled };
        if (chart is ScheduleChartBase schedule)
        {
            config["showLabels"] = schedule.ShowLabels; config["visibleStart"] = schedule.VisibleStart; config["visibleEnd"] = schedule.VisibleEnd; config["inputTimeZone"] = schedule.InputTimeZone.Id;
            config["laneFilter"] = schedule.LaneFilter; config["startPath"] = schedule.StartPath; config["endPath"] = schedule.EndPath; config["valuePath"] = schedule.ValuePath;
        }
        if (chart is GanttChart gantt) { config["barHeight"] = gantt.BarHeight; config["rowHeight"] = gantt.RowHeight; config["barBrush"] = gantt.BarBrush?.ToString(); config["progressBrush"] = gantt.ProgressBrush?.ToString(); config["showDependencies"] = gantt.ShowDependencies; }
        if (chart is SwimlaneChart swim) { config["laneHeight"] = swim.LaneHeight; config["taskHeight"] = swim.TaskHeight; config["taskSpacing"] = swim.TaskSpacing; config["taskCornerRadius"] = swim.TaskCornerRadius; config["minimum"] = swim.Minimum; config["maximum"] = swim.Maximum; }
        if (chart is EventTimelineChart timeline) { config["orientation"] = timeline.Orientation.ToString(); config["strokeThickness"] = timeline.StrokeThickness; }
        if (chart is SpiralTimelineChart spiral) { config["turns"] = spiral.Turns; config["innerRadius"] = spiral.InnerRadius; config["periodTicks"] = spiral.Period?.Ticks; }
        if (chart is OperationalChartBase operational)
        { config["showLabels"] = operational.ShowLabels; config["showValues"] = operational.ShowValues; config["cellGap"] = operational.CellGap; config["minimumColor"] = operational.MinimumColor.ToString(); config["maximumColor"] = operational.MaximumColor.ToString(); config["valuePath"] = operational.ValuePath; config["rowPath"] = operational.RowPath; config["columnPath"] = operational.ColumnPath; }
        if (chart is KpiCardChart kpi) { config["deltaType"] = kpi.DeltaType.ToString(); config["showSparkline"] = kpi.ShowSparkline; }
        if (chart is CalendarHeatmapChart calendar) { config["startDate"] = calendar.StartDate?.ToString("yyyy-MM-dd"); config["endDate"] = calendar.EndDate?.ToString("yyyy-MM-dd"); config["firstDayOfWeek"] = calendar.FirstDayOfWeek.ToString(); config["emptyCellBrush"] = calendar.EmptyCellBrush?.ToString(); }
        if (chart is WaffleChart waffle) { config["rows"] = waffle.Rows; config["columns"] = waffle.Columns; }
        if (chart is WordCloudChart word) { config["minFontSize"] = word.MinFontSize; config["maxFontSize"] = word.MaxFontSize; }
        if (chart is OperationalRankingChartBase ranking) { config["rankFromValues"] = ranking.RankFromValues; config["strokeThickness"] = ranking.StrokeThickness; }
        if (chart is ParliamentChart parliament) { config["rows"] = parliament.Rows; config["innerRadiusFactor"] = parliament.InnerRadiusFactor; }
        if (chart is ThemeRiverChart river) config["baseline"] = river.Baseline.ToString();
        if (chart is HierarchyChartBase hierarchy)
        {
            config["showLabels"] = hierarchy.ShowLabels; config["showValues"] = hierarchy.ShowValues; config["showLegend"] = hierarchy.ShowLegend; config["gap"] = hierarchy.Gap;
            config["childrenPath"] = hierarchy.ChildrenPath; config["valuePath"] = hierarchy.ValuePath; config["maximumVisibleDepth"] = hierarchy.MaximumVisibleDepth;
            config["rootIndex"] = hierarchy.DrillPath.LastOrDefault()?.Index ?? -1; config["collapsedIndices"] = hierarchy.Snapshot?.Nodes.Where(entry => !hierarchy.IsExpanded(entry.Id ?? entry.Source!)).Select(entry => entry.Index).ToArray();
            config["animationTriggers"] = hierarchy.AnimationTriggers.ToString(); config["reduceMotion"] = hierarchy.ReduceMotion;
        }
        if (chart is FlameGraph flame) config["valueMode"] = flame.ValueMode.ToString();
        if (chart is IcicleChart icicle) { config["valueMode"] = icicle.ValueMode.ToString(); config["orientation"] = icicle.Orientation.ToString(); }
        if (chart is DendrogramChart dendrogram) { config["distancePath"] = dendrogram.DistancePath; config["orientation"] = dendrogram.Orientation.ToString(); config["linkStyle"] = dendrogram.LinkStyle.ToString(); config["nodeSize"] = dendrogram.NodeSize; }
        if (chart is IndentedTreeChart indented) config["indentSize"] = indented.IndentSize;
        if (chart is RadialTreeChart radial) { config["startAngle"] = radial.StartAngle; config["sweepAngle"] = radial.SweepAngle; config["nodeSize"] = radial.NodeSize; }
        if (chart is ShapeMap map) { config["showLabels"] = map.ShowLabels; config["showLegend"] = map.ShowLegend; config["projection"] = map.EffectiveProjection.ToString(); config["topologyRule"] = map.TopologyRule.ToString(); config["isGeographic"] = map.IsGeographic; }
        if (chart is SeatMap seat) config["isMultiSelectionEnabled"] = seat.IsMultiSelectionEnabled;
        return config;
    }
    private static object? C6Layout(EngineeringChartBase chart) => chart switch
    {
        GanttChart gantt => new { snapshot = C6Json(gantt.Snapshot), dependencies = gantt.DependencyPaths.Select(path => new { predecessorId = path.PredecessorId.ToString(), taskId = path.TaskId.ToString(), type = path.Type.ToString(), points = path.Points.Select(C6Point).ToArray() }).ToArray(), visitedNodes = gantt.LastQueryVisitedNodes },
        SwimlaneChart swim => new { snapshot = C6Json(swim.SpanSnapshot), swimlanes = C6Json(swim.SwimlaneLayout), visitedNodes = swim.LastQueryVisitedNodes },
        EventTimelineChart timeline => new { events = C6Json(timeline.Events), labels = timeline.LabelPlacements.Select(label => new { id = label.Id, text = label.Text, bounds = C6Rect(label.Bounds), anchor = C6Point(label.Anchor), isVisible = label.IsVisible }).ToArray(), visitedNodes = timeline.LastQueryVisitedNodes },
        SpiralTimelineChart spiral => C6Json(spiral.SpiralLayout),
        BulletChart bullet => C6Json(bullet.Layout), KpiCardChart kpi => C6Json(kpi.Layout), CalendarHeatmapChart calendar => C6Json(calendar.Calendar),
        OperationalMatrixChartBase matrix => new { matrix = C6Json(matrix.Matrix), tableCells = chart is TableChart table ? C6Json(table.Cells) : null },
        WaffleChart waffle => C6Json(waffle.Allocation), WordCloudChart word => C6Json(word.Words), OperationalRankingChartBase ranking => C6Json(ranking.Ranking),
        ParliamentChart parliament => C6Json(parliament.Seats), ThemeRiverChart river => C6Json(river.River),
        FlameGraph flame => C6Json(flame.Layout), IcicleChart icicle => C6Json(icicle.Layout), DendrogramChart dendrogram => C6Json(dendrogram.Layout), IndentedTreeChart indented => C6Json(indented.Layout), RadialTreeChart radial => C6Json(radial.Layout),
        ShapeMap map => new { snapshot = map.Snapshot is { } snapshot ? MapSnapshotProbe(snapshot) : null, projected = map.ProjectedGeometry is { } projected ? MapProjectedProbe(projected) : null,
            viewport = map.MapViewport is { } viewport ? new { bounds = MapBoundsProbe(viewport.Bounds), fittedArea = C6Rect(viewport.FittedArea), zoom = viewport.Zoom, panX = viewport.PanX, panY = viewport.PanY } : null,
            layers = map.LayerSnapshots.Select(C6MapLayer).ToArray() }, _ => null,
    };
    private void AddC6Probe(string kind, string theme, string phase, EngineeringChartBase chart, object? results = null, string? nativeImage = null, string? exportImage = null, string[]? dpiImages = null)
    {
        var marks = chart.GetMarks();
        _c6Probes.Add(new { schema = "c6-main-native/v1", kind, theme, phase, widthDip = chart.Bounds.Width, heightDip = chart.Bounds.Height,
            rawSource = C6Rows(chart.ItemsSource), rawGeoJson = (chart as ShapeMap)?.GeoJson, config = C6Config(chart), plot = C6Rect(chart.PlotArea), layout = C6Layout(chart),
            marks = marks.Select((mark, index) => C6Mark(chart, mark, index)).ToArray(), selectedItems = chart is SeatMap seat ? seat.SelectedItems.Select(C6Json).ToArray() : [],
            results, nativeImage, exportImage, dpiImages = dpiImages ?? [], physicalInputValidated = false, performanceMeasured = false });
    }
}
