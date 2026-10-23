using Avalonia;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private static readonly string[] RelationKinds = ["sankey", "alluvial", "network", "force", "arc", "chord", "flow", "process", "mindmap"];
    private readonly List<object> _relationsProbes = [];
    private readonly List<object> _relationsAsyncEvidence = [];
    private static object RelationRawNode(RelationDemoNode node) => new { id = node.Id, label = node.Label, x = node.X, y = node.Y, width = node.Width, height = node.Height,
        weight = node.Weight, stage = node.Stage, group = node.Group, shape = (int)node.Shape };
    private static object RelationRawEdge(RelationDemoEdge edge) => new { id = edge.Id, sourceId = edge.SourceId, targetId = edge.TargetId, weight = edge.Weight, label = edge.Label };
    private static object? RelationPoint(ChartPoint? point) => point is { } p ? new { x = p.X, y = p.Y } : null;
    private static object RelationSnapshot(GraphSnapshot snapshot) => new
    {
        nodes = snapshot.Nodes.Select(entry => new { index = entry.Index, sourceIndex = entry.SourceIndex, id = entry.Id, label = entry.Label,
            incomingWeight = entry.IncomingWeight, outgoingWeight = entry.OutgoingWeight,
            node = new { id = entry.Id, label = entry.Label, group = entry.Node.Group, shape = (int)entry.Node.Shape, position = RelationPoint(entry.Node.Position),
                x = entry.Node.X, y = entry.Node.Y, width = entry.Node.Width, height = entry.Node.Height, weight = entry.Node.Weight, stage = entry.Node.Stage },
            source = entry.Source is RelationDemoNode node ? RelationRawNode(node) : null }).ToArray(),
        edges = snapshot.Edges.Select(entry => new { index = entry.Index, sourceIndex = entry.SourceIndex, id = entry.Id, label = entry.Label,
            sourceNodeIndex = entry.SourceNodeIndex, targetNodeIndex = entry.TargetNodeIndex, sourceId = entry.Edge.SourceId, targetId = entry.Edge.TargetId, weight = entry.Weight,
            source = entry.Source is RelationDemoEdge edge ? RelationRawEdge(edge) : null }).ToArray(),
    };
    private static object? RelationGraph(RelationGraphLayout? graph) => graph is null ? null : new
    {
        nodes = graph.Nodes.Select(node => new { entryIndex = node.Entry.Index, x = node.X, y = node.Y, width = node.Width, height = node.Height, center = RelationPoint(node.Center) }).ToArray(),
        edges = graph.Edges.Select(edge => new { entryIndex = edge.Entry.Index, points = edge.Points.Select(point => RelationPoint(point)).ToArray(), thickness = edge.Thickness, isBackEdge = edge.IsBackEdge }).ToArray(),
        groups = graph.Groups.Select(group => new { group = group.Group, x = group.X, y = group.Y, width = group.Width, height = group.Height }).ToArray(),
        imbalances = graph.Imbalances.Select(balance => new { nodeIndex = balance.Node.Index, incomingWeight = balance.IncomingWeight, outgoingWeight = balance.OutgoingWeight, delta = balance.Delta }).ToArray(),
        weightScale = graph.WeightScale, isWeightScaleSaturated = graph.IsWeightScaleSaturated, iterations = graph.Iterations, remainingCollisionCount = graph.RemainingCollisionCount, isConverged = graph.IsConverged,
        maximumDisplacement = graph.MaximumDisplacement, fallbackApplied = graph.FallbackApplied,
    };
    private static object? RelationChord(RelationChordLayout? chord) => chord is null ? null : new
    {
        paddingDegrees = chord.PaddingDegrees,
        nodes = chord.Nodes.Select(node => new { entryIndex = node.Entry.Index, startAngle = node.StartAngle, sweepAngle = node.SweepAngle }).ToArray(),
        ribbons = chord.Ribbons.Select(ribbon => new { entryIndex = ribbon.Entry.Index, sourceStartAngle = ribbon.SourceStartAngle, sourceSweepAngle = ribbon.SourceSweepAngle,
            targetStartAngle = ribbon.TargetStartAngle, targetSweepAngle = ribbon.TargetSweepAngle }).ToArray(),
    };
    private static object RelationProbeMark(EngineeringMarkInfo mark, int index) => new
    {
        index, key = mark.Key is RelationGlyphKey key ? new { kind = key.Kind.ToString(), identity = key.Identity, hasExplicitId = key.HasExplicitId } : null,
        value = mark.Value, position = new { x = mark.Position.X, y = mark.Position.Y },
        bounds = new { x = mark.Bounds.X, y = mark.Bounds.Y, width = mark.Bounds.Width, height = mark.Bounds.Height },
        members = mark.Members.Select(member => new { sourceIndex = member.SourceIndex, stableId = member.StableId, category = member.Category,
            point = new { x = member.Point.X, y = member.Point.Y }, value = member.Value, weight = member.Weight,
            item = member.Item switch { RelationDemoNode node => RelationRawNode(node), RelationDemoEdge edge => RelationRawEdge(edge), _ => null } }).ToArray(),
    };
    private static object? RelationProbeHit(EngineeringPointHit? hit) => hit is null ? null : RelationProbeMark(hit.Mark, hit.Index);
    private void AddRelationProbe(string kind, string theme, string phase, RelationsChartBase chart, RelationDemoNode[] nodes, RelationDemoEdge[] edges,
        object? observations = null, string? nativeImage = null, string? exportImage = null, string[]? dpiImages = null)
    {
        var marks = chart.GetMarks(); var plot = chart.PlotArea;
        _relationsProbes.Add(new { kind, theme, phase, widthDip = chart.Bounds.Width, heightDip = chart.Bounds.Height,
            rawNodes = nodes.Select(RelationRawNode).ToArray(), rawEdges = edges.Select(RelationRawEdge).ToArray(),
            config = new { zoom = chart.Zoom, panX = chart.PanX, panY = chart.PanY, layoutState = chart.LayoutState.ToString(), isLayoutRunning = chart.IsLayoutRunning,
                hasPositions = chart.XPath is not null && chart.YPath is not null, nodeWidth = (chart as SankeyChart)?.NodeWidth, nodeGap = (chart as SankeyChart)?.NodeGap,
                seed = (chart as NetworkChart)?.Seed, iterations = (chart as NetworkChart)?.Iterations, innerRadius = (chart as ChordDiagramChart)?.InnerRadius,
                paddingDegrees = (chart as ChordDiagramChart)?.PaddingDegrees, showLabels = chart.ShowLabels, showGroups = chart.ShowGroups },
            plot = new { x = plot.X, y = plot.Y, width = plot.Width, height = plot.Height }, snapshot = chart.Snapshot is { } snapshot ? RelationSnapshot(snapshot) : null,
            graphLayout = RelationGraph(chart.GraphLayout), chordLayout = RelationChord(chart.ChordLayout), marks = marks.Select(RelationProbeMark).ToArray(), observations,
            nativeImage, exportImage, dpiImages = dpiImages ?? [], physicalInputValidated = false, performanceMeasured = false });
    }
    private static (string Id, Point Position)[] RelationIndependentTargets(string kind)
    {
        Point Project(double x, double y) => new(24 + x * 372, 24 + y * 232);
        if (kind == "chord") return [("a", new Point(210 + 116 * .91, 140)), ("b", new Point(210 - 116 * .91, 140))];
        if (kind is "sankey" or "alluvial" or "process") return [("a", Project(.1, .5)), ("b", Project(.9, .5))];
        if (kind == "mindmap") return [("a", Project(.5, .5)), ("b", Project(.9, .5))];
        return [("a", Project(.25, .5)), ("b", Project(.75, .5))];
    }
}
