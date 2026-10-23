using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class RelationSankeyRenderTests
{
    [AvaloniaTheory]
    [InlineData(420, 280)] [InlineData(280, 520)]
    public void Ordinary_two_node_cycles_keep_every_ribbon_inside_the_plot_and_keep_original_edge_hits(int width, int height)
    {
        var ab = new object(); var ba = new object();
        var chart = new SankeyChart { ItemsSource = new[] { new GraphNode("a"), new GraphNode("b") }, EdgesSource = new[] { new GraphEdge("a", "b", 4, source: ab, id: "ab"), new GraphEdge("b", "a", 4, source: ba, id: "ba") } };
        using var host = new Host(chart, width, height); var plot = chart.PlotArea;
        foreach (var layout in chart.GraphLayout!.Edges)
        {
            var mark = chart.GetMarks().Single(m => m.Key is RelationGlyphKey { Kind: RelationElementKind.Edge } key && Equals(key.Identity, layout.Entry.Id));
            AssertInside(plot, mark.Bounds); Assert.True(chart.SelectEdge(layout.Entry.Id!)); Assert.Same(layout.Entry.Source, chart.SelectedPoint!.Members[0].Item);
            var hit = FindOriginalHit(chart, mark, layout.Entry.Source); Assert.NotNull(hit); Assert.Same(layout.Entry.Source, hit.Members[0].Item);
        }
    }

    [AvaloniaTheory]
    [InlineData(420, 280)] [InlineData(280, 520)]
    public void Sloping_sankey_ports_retain_exact_normalized_weight_width_in_both_aspects(int width, int height)
    {
        var chart = new SankeyChart { ItemsSource = new[] { new GraphNode("a"), new GraphNode("b"), new GraphNode("c") }, EdgesSource = new[] { new GraphEdge("a", "b", 1, id: "ab"), new GraphEdge("a", "c", 3, id: "ac") } };
        using var host = new Host(chart, width, height); var plot = chart.PlotArea;
        foreach (var edge in chart.GraphLayout!.Edges)
        {
            var mark = chart.GetMarks().Single(m => m.Key is RelationGlyphKey { Kind: RelationElementKind.Edge } key && Equals(key.Identity, edge.Entry.Id));
            var polygon = Assert.IsType<PolylineGeometry>(mark.Geometry).Points; var start = edge.Points[0];
            Near(plot.Left + start.X * plot.Width, polygon[0].X); Near(polygon[0].X, polygon[^1].X);
            Near(plot.Top + (start.Y + edge.Thickness / 2) * plot.Height, polygon[0].Y); Near(edge.Thickness * plot.Height, Math.Abs(polygon[0].Y - polygon[^1].Y));
            AssertInside(plot, mark.Bounds);
        }
    }
    [AvaloniaTheory]
    [InlineData(RelationSankeyMode.Sankey, 420, 280)] [InlineData(RelationSankeyMode.Sankey, 280, 520)]
    [InlineData(RelationSankeyMode.Alluvial, 420, 280)] [InlineData(RelationSankeyMode.Alluvial, 280, 520)]
    public void Dense_column_steep_forward_bands_stay_inside_the_plot_and_keep_original_edge_identity(RelationSankeyMode mode, int width, int height)
    {
        const int count = 1000; var originals = Enumerable.Range(0, count).Select(_ => new object()).ToArray();
        var nodes = Enumerable.Range(0, count).Select(i => new GraphNode(i, stage: i)).ToArray();
        var edges = Enumerable.Range(0, count - 1).Select(i => new GraphEdge(i, i + 1, source: originals[i], id: i)).Append(new(0, count - 1, source: originals[^1], id: count - 1)).ToArray();
        var chart = new SankeyChart { Mode = mode, ItemsSource = nodes, EdgesSource = edges };
        using var host = new Host(chart, width, height); Assert.Null(chart.DataError); var plot = chart.PlotArea;
        foreach (var mark in chart.GetMarks()) AssertInside(plot, mark.Bounds);
        var edge = chart.GraphLayout!.Edges[0]; Assert.False(edge.IsBackEdge); Assert.InRange(edge.Points[^1].X - edge.Points[0].X, 0, .001); Assert.True(Math.Abs(edge.Points[^1].Y - edge.Points[0].Y) > .1);
        var ribbon = chart.GetMarks().Single(m => m.Key is RelationGlyphKey { Kind: RelationElementKind.Edge, Identity: 0 }); var points = Assert.IsType<PolylineGeometry>(ribbon.Geometry).Points;
        for (var i = 0; i < edge.Points.Count; i++)
        {
            Near(plot.Left + edge.Points[i].X * plot.Width, points[i].X); Near(points[i].X, points[^(i + 1)].X);
            Near(edge.Thickness * plot.Height, points[i].Y - points[^(i + 1)].Y);
        }
        var middle = edge.Points[edge.Points.Count / 2]; var position = new Point(plot.Left + middle.X * plot.Width, plot.Top + middle.Y * plot.Height);
        var hit = Assert.IsType<EngineeringPointHit>(chart.HitTest(position)); Assert.Equal(0, Assert.IsType<RelationGlyphKey>(hit.Mark.Key).Identity); Assert.Same(originals[0], hit.Members[0].Item); Assert.Equal(0, hit.Members[0].SourceIndex); Assert.Equal(0, hit.Members[0].StableId);
        Assert.True(chart.SelectEdge(0)); Assert.Same(originals[0], chart.SelectedEdge!.Source);
    }
    private static EngineeringPointHit? FindOriginalHit(SankeyChart chart, EngineeringMarkInfo mark, object source)
    {
        var bounds = mark.Bounds;
        for (var y = 1; y < 20; y++) for (var x = 1; x < 20; x++)
        { var point = new Point(bounds.Left + bounds.Width * x / 20, bounds.Top + bounds.Height * y / 20); var hit = chart.HitTest(point); if (hit is not null && ReferenceEquals(hit.Members[0].Item, source)) return hit; }
        return null;
    }
    private static void AssertInside(Rect plot, Rect bounds)
    { Assert.True(bounds.Left >= plot.Left - 1e-7 && bounds.Right <= plot.Right + 1e-7 && bounds.Top >= plot.Top - 1e-7 && bounds.Bottom <= plot.Bottom + 1e-7, $"{bounds} lies outside {plot}."); }
    private static void Near(double expected, double actual) => Assert.InRange(Math.Abs(expected - actual), 0, 1e-8);
    private sealed class Host : IDisposable
    {
        private readonly Window _window;
        public Host(SankeyChart chart, int width, int height) { _window = new Window { Width = width, Height = height, Content = chart }; _window.Show(); _window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); _ = chart.GetMarks(); }
        public void Dispose() { _window.Close(); Dispatcher.UIThread.RunJobs(); }
    }
}
