using Avalonia.Controls;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class MainWindow
{
    internal FlowChart RelationFlow => this.FindControl<FlowChart>("UsageRelationFlow")!;
    internal RelationsChartBase[] RelationCharts => new[] { "UsageRelationSankey", "UsageRelationAlluvial", "UsageRelationNetwork", "UsageRelationForce", "UsageRelationArc", "UsageRelationChord", "UsageRelationFlow", "UsageRelationProcess", "UsageRelationMindmap" }
        .Select(name => this.FindControl<RelationsChartBase>(name)!).ToArray();
    internal List<object> RelationBindingDiagnostics { get; } = [];
    private async void RunRelationsLayout(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try { foreach (var chart in RelationCharts) await chart.LayoutAsync(); }
        catch (OperationCanceledException) { }
    }
    private void CancelRelationsLayout(object? sender, Avalonia.Interactivity.RoutedEventArgs e) { foreach (var chart in RelationCharts) chart.CancelLayout(); }
    internal void VerifyRelationsBindings(Dictionary<string, bool> checks)
    {
        var expander = this.FindControl<Expander>("RelationsExamples")!; expander.IsExpanded = true; FlushBindings();
        var charts = RelationCharts;
        void Check(string key, bool value) { checks["relations-compiled-" + key] = value; ProfessionalUsageTrace($"relations: {key}={value}"); }
        Check("nine-modes-arranged", charts.Length == 9 && charts.All(chart => chart.PlotArea.Width > 100 && chart.GetMarks().Count == 9 && chart.DataError is null));
        Check("separate-business-node-edge-sources", charts.All(chart => ReferenceEquals(chart.ItemsSource, Model.RelationNodes) && ReferenceEquals(chart.EdgesSource, Model.RelationEdges)));
        Check("custom-paths", charts.All(chart => chart.IdPath == "Id" && chart.LabelPath == "Name" && chart.SourcePath == "From" && chart.TargetPath == "To" && chart.WeightPath == "Amount" && chart.StagePath == "Stage"));
        Check("shape-position-and-group-paths", RelationFlow.XPath == "X" && RelationFlow.YPath == "Y" && RelationFlow.ShapePath == "Shape" && RelationFlow.GroupPath == "Category" && RelationFlow.WidthPath == "Width" && RelationFlow.HeightPath == "Height");
        Check("all-marks-reference-original-business-items", charts.All(chart => chart.GetMarks().All(mark => mark.Members.Count == 1 && mark.Members[0].Item is RelationBusinessNode or RelationBusinessEdge)));
        Check("parallel-and-loop-edges-have-distinct-original-ids", charts.All(chart => chart.GetMarks().SelectMany(mark => mark.Members).Where(member => member.Item is RelationBusinessEdge).Select(member => ((RelationBusinessEdge)member.Item!).Id).Order().SequenceEqual(Model.RelationEdges.Select(edge => edge.Id).Order())));
        var first = RelationFlow.GetMarks().Select((mark, i) => (mark, i)).Single(pair => ReferenceEquals(pair.mark.Members[0].Item, Model.RelationEdges[0]));
        RelationFlow.SelectedIndex = first.i; FlushBindings();
        Check("edge-selection-flows-to-model", Model.RelationsSelectedIndex == first.i && ReferenceEquals(RelationFlow.SelectedPoint?.Members[0].Item, Model.RelationEdges[0]) && RelationFlow.SelectedEdge is not null);
        var nodeIndex = RelationFlow.GetMarks().Select((mark, i) => (mark, i)).Single(pair => ReferenceEquals(pair.mark.Members[0].Item, Model.RelationNodes[1])).i;
        Model.RelationsSelectedIndex = nodeIndex; FlushBindings(); Check("node-selection-from-model", ReferenceEquals(RelationFlow.SelectedPoint?.Members[0].Item, Model.RelationNodes[1]) && RelationFlow.SelectedNode is not null);
        Model.RelationNodes.Move(1, 0); FlushBindings(); Check("node-selection-follows-source-reorder", Equals(RelationFlow.SelectedNode?.Id, "b") && ReferenceEquals(RelationFlow.SelectedPoint?.Members[0].Item, Model.RelationNodes[0]));
        Model.RelationNodes.Move(0, 1); FlushBindings(); _ = RelationFlow.GetMarks(); Model.RelationsSelectedIndex = -1;
        var original = Model.RelationEdges[0].Amount; Model.RelationsUpdateCommand.Execute(null); FlushBindings();
        Check("edge-inpc-refreshes-all-snapshots", charts.All(chart => chart.Snapshot?.Edges.Single(edge => Equals(edge.Id, "ab")).Weight == original + 2));
        Model.RelationEdges[0].Amount = -1; FlushBindings(); Check("invalid-weight-clears-old-scene", charts.All(chart => chart.GetMarks().Count == 0 && chart.DataError is not null));
        Check("failed-data-retains-edge-observation", Model.RelationEdges[0].SubscriberCount > 0);
        Model.RelationEdges[0].Amount = original; FlushBindings(); Check("same-edge-source-recovers", charts.All(chart => chart.GetMarks().Count == 9 && chart.DataError is null));
        Model.RelationEdges[0].To = "missing"; FlushBindings(); Check("dangling-target-is-data-error", charts.All(chart => chart.GetMarks().Count == 0 && chart.DataError is not null));
        Model.RelationEdges[0].To = "b"; FlushBindings(); Check("repaired-endpoint-recovers", charts.All(chart => chart.GetMarks().Count == 9 && chart.DataError is null));
        var extra = new RelationBusinessEdge("added", "a", "c", 2); Model.RelationEdges.Add(extra); FlushBindings();
        Check("edge-collection-add-observes-new-item", charts.All(chart => chart.Snapshot?.Edges.Count == 7) && extra.SubscriberCount > 0);
        Model.RelationEdges.Remove(extra); FlushBindings(); foreach (var chart in charts) _ = chart.GetMarks(); Check("edge-removal-releases-observer", extra.SubscriberCount == 0);
        var x = Model.RelationNodes[1].X; Model.RelationNodes[1].X = .55; FlushBindings();
        Check("explicit-node-position-change-keeps-source", RelationFlow.GraphLayout?.Nodes.Single(node => Equals(node.Entry.Id, "b")) is { } moved && Math.Abs(moved.Center.X - .55) < 1e-9);
        Model.RelationNodes[1].X = x; Model.RelationsLabels = false; FlushBindings(); Check("labels-bound-across-modes", charts.All(chart => !chart.ShowLabels)); Model.RelationsLabels = true;
        var seed = Model.RelationsSeed; Model.RelationsSeedCommand.Execute(null); FlushBindings(); Check("force-seed-binding", charts.OfType<NetworkChart>().Single(chart => chart.Mode == RelationNetworkMode.ForceDirected).Seed == seed + 1);
        var content = Content; Content = null; FlushBindings();
        Check("detach-releases-node-edge-items-and-collections", Model.RelationNodes.All(node => node.SubscriberCount == 0) && Model.RelationEdges.All(edge => edge.SubscriberCount == 0) && Model.RelationNodes.SubscriberCount == 0 && Model.RelationEdges.SubscriberCount == 0);
        Content = content; FlushBindings(); foreach (var chart in charts) _ = chart.GetMarks();
        Check("reattach-restores-both-source-observers", charts.All(chart => chart.GetMarks().Count == 9 && chart.DataError is null) && Model.RelationEdges.All(edge => edge.SubscriberCount > 0));
        RelationBindingDiagnostics.Add(new { kinds = charts.Select(chart => chart.GetType().Name).ToArray(), markCounts = charts.Select(chart => chart.GetMarks().Count).ToArray(),
            nodes = Model.RelationNodes.Select(node => new { node.Id, node.Name, node.Stage, node.X, node.Y }).ToArray(), edges = Model.RelationEdges.Select(edge => new { edge.Id, edge.From, edge.To, edge.Amount }).ToArray(),
            physicalInputValidated = false, performanceMeasured = false });
        Model.RelationsSelectedIndex = -1; expander.IsExpanded = false; FlushBindings();
    }
}
