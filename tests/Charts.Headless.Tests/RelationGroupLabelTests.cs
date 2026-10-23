using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class RelationGroupLabelTests
{
    [AvaloniaFact]
    public void Group_touching_top_edge_paints_its_complete_text_line_inside_the_plot()
    {
        var chart = Chart(new GraphNode("a", group: "Processing", position: new(.5, .04), width: .4, height: .08));
        using var host = new Host(chart); var labels = GlyphBounds(Record(chart), Matrix.Identity).ToArray();
        Assert.NotEmpty(labels); foreach (var label in labels) AssertInside(chart.PlotArea, label);
    }

    [AvaloniaFact]
    public void Zoom_and_pan_keep_visible_group_headers_inside_both_horizontal_and_vertical_plot_edges()
    {
        var chart = Chart(new GraphNode("a", group: "Processing group with a deliberately long title for trimming", position: new(.5, .5), width: .5, height: .3));
        using var host = new Host(chart); chart.Zoom = 2; chart.PanX = .2; chart.PanY = -.2;
        var labels = GlyphBounds(Record(chart), Matrix.Identity).ToArray(); Assert.NotEmpty(labels); foreach (var label in labels) AssertInside(chart.PlotArea, label);
        chart.PanX = -.2; labels = GlyphBounds(Record(chart), Matrix.Identity).ToArray(); Assert.NotEmpty(labels); foreach (var label in labels) AssertInside(chart.PlotArea, label);
    }

    [AvaloniaTheory]
    [InlineData(2, 0)] [InlineData(-2, 0)] [InlineData(0, 2)] [InlineData(0, -2)]
    public void Entirely_offscreen_groups_do_not_emit_floating_header_glyphs(double panX, double panY)
    {
        var chart = Chart(new GraphNode("a", group: "Processing", position: new(.5, .5), width: .2, height: .2));
        using var host = new Host(chart); Assert.NotEmpty(GlyphBounds(Record(chart), Matrix.Identity));
        chart.PanX = panX; chart.PanY = panY; Assert.Empty(GlyphBounds(Record(chart), Matrix.Identity));
    }

    [AvaloniaFact]
    public void Tiny_visible_fragments_omit_headers_instead_of_painting_partially_clipped_text()
    {
        var chart = Chart(new GraphNode("a", group: "Processing", position: new(.5, .5), width: .2, height: .2));
        using var host = new Host(chart); chart.PanY = -.6; Assert.Empty(GlyphBounds(Record(chart), Matrix.Identity));
        chart.PanY = 0; chart.PanX = -.6; Assert.Empty(GlyphBounds(Record(chart), Matrix.Identity));
    }
    private static FlowChart Chart(GraphNode node) => new() { Mode = RelationFlowMode.Explicit, ItemsSource = new[] { node }, ShowLabels = false, ShowEdgeLabels = false, ShowGroups = true, IsTooltipEnabled = false };
    private static DrawingGroup Record(EngineeringChartBase chart) { var drawing = new DrawingGroup(); using var context = drawing.Open(); chart.Render(context); return drawing; }
    private static IEnumerable<Rect> GlyphBounds(Drawing drawing, Matrix transform)
    {
        if (drawing is DrawingGroup group)
        {
            var next = (group.Transform?.Value ?? Matrix.Identity) * transform;
            foreach (var child in group.Children) foreach (var bounds in GlyphBounds(child, next)) yield return bounds;
        }
        else if (drawing is GlyphRunDrawing glyph) yield return glyph.GetBounds().TransformToAABB(transform);
    }
    private static void AssertInside(Rect plot, Rect bounds) => Assert.True(bounds.Width > 0 && bounds.Height > 0 && bounds.Left >= plot.Left - 1e-6 && bounds.Top >= plot.Top - 1e-6 && bounds.Right <= plot.Right + 1e-6 && bounds.Bottom <= plot.Bottom + 1e-6, $"Unclipped label bounds {bounds} extend outside plot {plot}.");
    private sealed class Host : IDisposable
    {
        private readonly Window _window;
        public Host(FlowChart chart) { _window = new Window { Width = 420, Height = 280, Content = chart }; _window.Show(); _window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); _ = chart.GetMarks(); }
        public void Dispose() { _window.Close(); Dispatcher.UIThread.RunJobs(); }
    }
}
