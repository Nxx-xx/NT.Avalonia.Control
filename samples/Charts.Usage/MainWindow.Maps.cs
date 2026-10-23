using Avalonia.Controls;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class MainWindow
{
    internal ShapeMap[] MapCharts => new[] { "UsageMapShape", "UsageMapChoropleth", "UsageMapBubble", "UsageMapHeat", "UsageMapSeat" }.Select(name => this.FindControl<ShapeMap>(name)!).ToArray();
    internal List<object> MapBindingDiagnostics { get; } = [];
    internal void VerifyMapBindings(Dictionary<string, bool> checks)
    {
        var expander = this.FindControl<Expander>("MapExamples")!; expander.IsExpanded = true; FlushBindings();
        var charts = MapCharts; var shape = charts[0]; var bubble = charts[2]; var heat = charts[3]; var seat = charts[4];
        void Check(string name, bool result) => checks["maps-compiled-" + name] = result;
        Check("five-modes-arranged", charts.Length == 5 && charts.All(chart => chart.DataError is null && chart.GetMarks().Count > 0 && chart.PlotArea.Width > 100));
        Check("geometry-source-inherited", charts.All(chart => ReferenceEquals(chart.FeaturesSource, Model.MapFeatures) && chart.Snapshot?.Features.Count == 2));
        Check("layer-datacontext-inheritance", charts.All(chart => chart.Layers.All(layer => ReferenceEquals(layer.DataContext, Model))));
        Check("custom-business-paths", shape.Layers[0].RegionPath == "Region" && shape.Layers[0].ValuePath == "Amount" && bubble.Layers[1].LongitudePath == "East" && bubble.Layers[1].LatitudePath == "North");
        Check("original-region-sources", ReferenceEquals(shape.LayerSnapshots[0].Regions!.Regions[0].Data!.Source, Model.MapRegions[0]));
        Check("unknown-region-retains-original", shape.UnknownRegions.Count == 1 && ReferenceEquals(shape.UnknownRegions[0].Source, Model.MapRegions[2]));
        Check("bubble-original-members", bubble.GetMarks().All(mark => mark.Members.All(member => Model.MapPoints.Any(point => ReferenceEquals(point, member.Item)))));
        Check("heat-members-original", heat.GetMarks().All(mark => mark.Members.All(member => Model.MapPoints.Any(point => ReferenceEquals(point, member.Item)))));
        Check("nongeographic-identity", !seat.IsGeographic && seat.EffectiveProjection == MapProjection.Identity);
        shape.SelectItem(shape.Layers[0], Model.MapRegions[1]); FlushBindings();
        Check("selected-index-to-viewmodel", Model.MapSelectedIndex == shape.SelectedIndex && ReferenceEquals(shape.SelectedPoint?.Members[0].Item, Model.MapRegions[1]));
        Model.MapSelectedIndex = 0; FlushBindings(); Check("selected-index-from-viewmodel", ReferenceEquals(shape.SelectedPoint?.Members[0].Item, Model.MapRegions[0]));
        Model.MapRegions.Move(0, 1); FlushBindings();
        Check("selection-follows-original-region-reorder", ReferenceEquals(shape.SelectedPoint?.Members[0].Item, Model.MapRegions[1]) && shape.SelectedPoint.Members[0].SourceIndex == 1);
        Model.MapRegions.Move(1, 0); FlushBindings(); Model.MapSelectedIndex = -1;
        var before = Model.MapRegions[0].Amount; Model.MapUpdateCommand.Execute(null); FlushBindings();
        Check("region-inpc-refresh", shape.LayerSnapshots[0].Regions!.Regions[0].Value == before + 1);
        Check("point-inpc-refresh", bubble.LayerSnapshots[1].Bubbles!.Points[0].Entry.Value == Model.MapPoints[0].Score);
        Model.MapRegions[0].Amount = double.NaN; FlushBindings(); Check("invalid-region-fails-and-clears", shape.DataError is not null && shape.GetMarks().Count == 0);
        Model.MapRegions[0].Amount = before; FlushBindings(); Check("same-region-recovers", shape.DataError is null && shape.GetMarks().Count == 2);
        var score = Model.MapPoints[0].Score; Model.MapPoints[0].Score = -1; FlushBindings();
        Check("invalid-point-fails-both-layers", bubble.DataError is not null && heat.DataError is not null && bubble.GetMarks().Count == 0 && heat.GetMarks().Count == 0);
        Model.MapPoints[0].Score = score; FlushBindings(); Check("point-source-recovers", bubble.DataError is null && heat.DataError is null);
        Model.MapRegions[0].Region = "unknown-a"; FlushBindings(); Check("changed-region-key-is-unmatched", shape.UnknownRegions.Count == 2 && shape.GetMarks().Count == 1);
        Model.MapRegions[0].Region = "a"; FlushBindings(); Check("repaired-region-key-joins", shape.UnknownRegions.Count == 1 && shape.GetMarks().Count == 2);
        Model.MapProjection = MapProjection.WebMercator; FlushBindings(); Check("projection-bound-through-chart", charts.Take(4).All(chart => chart.EffectiveProjection == MapProjection.WebMercator) && seat.EffectiveProjection == MapProjection.Identity);
        Model.MapProjection = MapProjection.Equirectangular; Model.MapLabels = true; FlushBindings(); Check("labels-bound", charts.All(chart => chart.ShowLabels)); Model.MapLabels = false;
        var extra = new MapBusinessPoint("new", "新增观测", 0, 10, 2); Model.MapPoints.Add(extra); FlushBindings();
        Check("incc-new-point-observed", bubble.GetMarks().Count == 3 && extra.SubscriberCount > 0);
        Model.MapPoints.Remove(extra); FlushBindings(); Check("removed-point-releases", extra.SubscriberCount == 0 && bubble.GetMarks().Count == 2);
        Model.MapSeatCommand.Execute(null); FlushBindings(); Check("seat-business-brush", seat.GetMarks().First(mark => ReferenceEquals(mark.Members[0].Item, Model.MapRegions[0])).Fill?.ToString()?.Contains("99A3AF", StringComparison.OrdinalIgnoreCase) == true);
        var content = Content; Content = null; FlushBindings();
        Check("detach-releases-layer-item-observers", Model.MapPoints.All(point => point.SubscriberCount == 0) && Model.MapRegions.All(region => region.SubscriberCount == 0) && Model.MapPoints.SubscriberCount == 0 && Model.MapRegions.SubscriberCount == 0);
        Content = content; FlushBindings(); foreach (var chart in charts) _ = chart.GetMarks();
        Check("reattach-restores-layers", charts.All(chart => chart.DataError is null && chart.GetMarks().Count > 0) && Model.MapPoints.All(point => point.SubscriberCount > 0));
        MapBindingDiagnostics.Add(new { markCounts = charts.Select(chart => chart.GetMarks().Count).ToArray(), regionIds = Model.MapRegions.Select(region => region.Region).ToArray(),
            originalPointIds = Model.MapPoints.Select(point => point.Id).ToArray(), physicalInputValidated = false, performanceMeasured = false });
        expander.IsExpanded = false; FlushBindings();
    }
}
