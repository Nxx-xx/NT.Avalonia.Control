using Avalonia.Controls;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class MainWindow
{
    internal TreemapChart HierarchyTreemap => this.FindControl<TreemapChart>("UsageHierarchyTreemap")!;
    internal OrganizationChart HierarchyTree => this.FindControl<OrganizationChart>("UsageHierarchyTree")!;
    internal HierarchyChartBase[] HierarchyCharts => [HierarchyTreemap, this.FindControl<SunburstChart>("UsageHierarchySunburst")!, HierarchyTree,
        this.FindControl<BubbleCloudChart>("UsageHierarchyCloud")!, this.FindControl<PackedBubbleChart>("UsageHierarchyPacked")!, this.FindControl<CirclePackingChart>("UsageHierarchyCircles")!];
    internal List<object> HierarchyBindingDiagnostics { get; } = [];
    private void HierarchyDrill(object? sender, Avalonia.Interactivity.RoutedEventArgs e) { foreach (var chart in HierarchyCharts) chart.DrillDown("north"); }
    private void HierarchyBack(object? sender, Avalonia.Interactivity.RoutedEventArgs e) { foreach (var chart in HierarchyCharts) chart.DrillUp(); }
    private void HierarchyToggle(object? sender, Avalonia.Interactivity.RoutedEventArgs e) { foreach (var chart in HierarchyCharts) chart.ToggleExpanded("north"); }
    internal void VerifyHierarchyBindings(Dictionary<string, bool> checks)
    {
        var expander = this.FindControl<Expander>("HierarchyExamples")!; expander.IsExpanded = true; FlushBindings();
        var charts = HierarchyCharts;
        void Check(string key, bool value) => checks["hierarchy-compiled-" + key] = value;
        IEnumerable<HierarchyBusinessNode> Nodes() => Model.HierarchyRecords.SelectMany(root => new[] { root }.Concat(root.Nodes));
        Check("six-controls-and-paths", charts.All(chart => ReferenceEquals(chart.ItemsSource, Model.HierarchyRecords) && chart.ChildrenPath == "Nodes" && chart.ValuePath == "Weight" && chart.LabelPath == "Name" && chart.IdPath == "Id"));
        Check("valid-arranged-scenes", charts.All(chart => chart.DataError is null && chart.PlotArea.Width > 100) && charts.Select(chart => chart.GetMarks().Count).SequenceEqual(new[] { 6, 6, 6, 4, 4, 6 }));
        Check("branch-value-is-leaf-total", charts.All(chart => chart.Snapshot?.Nodes[0] is { Value: 999, AggregateValue: 9 }));
        Check("source-identities-not-projections", charts.All(chart => chart.GetMarks().All(mark => mark.Members[0].Item is HierarchyBusinessNode row && Equals(row.Id, mark.Key) && Equals(row.Id, mark.Members[0].StableId))));
        Check("bubble-category-and-size-legend-binding", charts.Where(chart => chart is BubbleCloudChart or PackedBubbleChart).All(chart => chart.GroupPath == "Group" && chart.ShowLegend && chart.ShowSizeLegend &&
            chart.LegendItems.Select(item => item.Label).SequenceEqual(new[] { "北区", "南区" }) && chart.LegendItems.Select(item => item.Value).SequenceEqual(new[] { 9d, 3d }) && chart.LegendItems.All(item => item.Nodes.All(node => node.Source is HierarchyBusinessNode))));
        HierarchyTree.SelectedIndex = 1; FlushBindings();
        Check("selection-to-model", Model.HierarchySelectedIndex == 1 && Equals(HierarchyTree.SelectedPoint?.Mark.Key, "a"));
        Model.HierarchySelectedIndex = 2; FlushBindings(); Check("selection-from-model", Equals(HierarchyTree.SelectedPoint?.Mark.Key, "b"));
        var chosen = Model.HierarchyRecords[0].Nodes[1]; Model.HierarchyRecords[0].Nodes.Move(1, 0); FlushBindings();
        Check("selection-follows-node-through-sibling-reorder", ReferenceEquals(HierarchyTree.SelectedPoint?.Members[0].Item, chosen) && HierarchyTree.SelectedPoint?.Members[0].SourceIndex == 0);
        Model.HierarchyRecords[0].Nodes.Move(0, 1); FlushBindings(); _ = HierarchyTree.GetMarks(); Model.HierarchySelectedIndex = -1;
        Model.HierarchyDepth = 0; FlushBindings(); Check("depth-binding-collapses-to-original-roots", charts.All(chart => chart.MaximumVisibleDepth == 0 && chart.GetMarks().Count == 2));
        Model.HierarchyDepth = 64; Model.HierarchyLabels = false; FlushBindings(); Check("label-binding", charts.All(chart => !chart.ShowLabels)); Model.HierarchyLabels = true;
        foreach (var chart in charts)
        {
            Check(chart.GetType().Name + "-drill", chart.DrillDown("north")); _ = chart.GetMarks();
            Check(chart.GetType().Name + "-ancestor-path", chart.DrillPath.Count == 1 && Equals(chart.DrillPath[0].Id, "north"));
            chart.Collapse("north"); Check(chart.GetType().Name + "-collapse", chart.GetMarks().Count == 1 && !chart.IsExpanded("north"));
            chart.Expand("north"); Check(chart.GetType().Name + "-expand", chart.GetMarks().Count >= 2); chart.ResetDrill();
        }
        FlushBindings(); var leaf = Model.HierarchyRecords[0].Nodes[0]; var before = leaf.Weight;
        Model.HierarchyUpdateCommand.Execute(null); FlushBindings(); Check("nested-inpc-updates-all-layouts", charts.All(chart => chart.Snapshot?.Nodes.Single(node => Equals(node.Id, leaf.Id)).Value == before + 2));
        leaf.Weight = -1; FlushBindings(); Check("invalid-nested-value-clears-data", charts.All(chart => chart.GetMarks().Count == 0 && chart.DataError is not null));
        Check("invalid-data-retains-observers-for-recovery", leaf.SubscriberCount > 0 && Model.HierarchyRecords[0].Nodes.SubscriberCount > 0);
        leaf.Weight = before; FlushBindings(); Check("same-source-recovers", charts.All(chart => chart.GetMarks().Count > 0 && chart.DataError is null));
        var added = new HierarchyBusinessNode("extra", "新增服务", 4); Model.HierarchyRecords[0].Nodes.Add(added); FlushBindings();
        Check("nested-incc-and-new-node-observation", charts.All(chart => chart.Snapshot?.Nodes.Any(node => Equals(node.Id, "extra")) == true) && added.SubscriberCount > 0);
        Model.HierarchyRecords[0].Nodes.Remove(added); FlushBindings(); foreach (var chart in charts) _ = chart.GetMarks(); Check("removed-node-releases-observers", added.SubscriberCount == 0);
        var previousChildren = Model.HierarchyRecords[0].Nodes;
        Model.HierarchyRecords[0].Nodes = new([new("replacement", "替换服务", 5)]); FlushBindings();
        Check("replacing-children-detaches-old-collection", charts.All(chart => chart.Snapshot?.Nodes.Any(node => Equals(node.Id, "replacement")) == true) && previousChildren.SubscriberCount == 0 && previousChildren.All(node => node.SubscriberCount == 0));
        previousChildren[0].Weight = -5; FlushBindings(); Check("retired-children-no-longer-invalidate-chart", charts.All(chart => chart.GetMarks().Count > 0 && chart.DataError is null));
        previousChildren[0].Weight = before; Model.HierarchyRecords[0].Nodes = previousChildren; FlushBindings(); foreach (var chart in charts) _ = chart.GetMarks();
        var content = Content; Content = null; FlushBindings(); Check("unload-releases-entire-hierarchy", Nodes().All(node => node.SubscriberCount == 0 && node.Nodes.SubscriberCount == 0) && Model.HierarchyRecords.SubscriberCount == 0);
        Content = content; FlushBindings(); foreach (var chart in charts) _ = chart.GetMarks();
        Check("reattach-restores-scenes-and-nested-observers", charts.All(chart => chart.DataError is null && chart.GetMarks().Count > 0) && Nodes().All(node => node.SubscriberCount > 0) && Model.HierarchyRecords[0].Nodes.SubscriberCount > 0);
        HierarchyBindingDiagnostics.Add(new { kinds = charts.Select(chart => chart.GetType().Name).ToArray(), counts = charts.Select(chart => chart.GetMarks().Count).ToArray(),
            sourceIds = Nodes().Select(node => node.Id).ToArray(), sourcePaths = HierarchyTreemap.Snapshot?.Nodes.Select(node => new { node.Id, node.Index, node.SourceIndex, node.Path, node.Value, node.AggregateValue }).ToArray(),
            physicalInputValidated = false, performanceMeasured = false });
        Model.HierarchySelectedIndex = -1; expander.IsExpanded = false; FlushBindings();
    }
}
