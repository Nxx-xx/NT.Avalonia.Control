using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class ProfessionalTrackballContrastTests
{
    [AvaloniaTheory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void Automatic_strokes_have_opposite_tone_halos_across_local_glyph_fills(bool engineering, bool dark)
    {
        var chart = Create(engineering); chart.Foreground = dark ? Brushes.White : Brushes.Black;
        using var host = new Host(chart); var window = host.Window;
        Update(chart); Drain(window);
        var shapes = Draw(chart);
        Assert.Equal(4, shapes.Length);
        var halo = dark ? Colors.Black : Colors.White;
        var core = dark ? Colors.White : Colors.Black;
        Assert.Equal(halo, Assert.IsAssignableFrom<ISolidColorBrush>(shapes[0].Pen!.Brush).Color);
        Assert.Equal(core, Assert.IsAssignableFrom<ISolidColorBrush>(shapes[1].Pen!.Brush).Color);
        Assert.Equal(halo, Assert.IsAssignableFrom<ISolidColorBrush>(shapes[2].Pen!.Brush).Color);
        Assert.Equal(core, Assert.IsAssignableFrom<ISolidColorBrush>(shapes[3].Pen!.Brush).Color);
        Assert.Equal(shapes[1].Pen!.Thickness + 2, shapes[0].Pen!.Thickness);
        Assert.Equal(shapes[3].Pen!.Thickness + 2, shapes[2].Pen!.Thickness);
    }

    [AvaloniaTheory] [InlineData(false)] [InlineData(true)]
    public void Explicit_strokes_and_disabled_zero_width_markers_remain_caller_controlled(bool engineering)
    {
        var chart = Create(engineering); using var host = new Host(chart); var window = host.Window;
        if (chart is EngineeringChartBase e) { e.TrackballLineStroke = Brushes.Magenta; e.TrackballMarkerStroke = Brushes.Lime; }
        else { var a = (AnalysisChartBase)chart; a.TrackballLineStroke = Brushes.Magenta; a.TrackballMarkerStroke = Brushes.Lime; }
        Update(chart); Drain(window); var shapes = Draw(chart);
        Assert.Equal(2, shapes.Length); Assert.Equal(Colors.Magenta, Assert.IsAssignableFrom<ISolidColorBrush>(shapes[0].Pen!.Brush).Color);
        Assert.Equal(Colors.Lime, Assert.IsAssignableFrom<ISolidColorBrush>(shapes[1].Pen!.Brush).Color);
        if (chart is EngineeringChartBase engineeringChart) { engineeringChart.ShowTrackballLine = false; engineeringChart.TrackballMarkerStroke = null; engineeringChart.TrackballMarkerStrokeThickness = 0; }
        else { var a = (AnalysisChartBase)chart; a.ShowTrackballLine = false; a.TrackballMarkerStroke = null; a.TrackballMarkerStrokeThickness = 0; }
        Drain(window); Assert.Empty(Draw(chart));
    }

    private static ChartBase Create(bool engineering) => engineering
        ? new EngineeringProbe { ItemsSource = new[] { new Row("one", 1) }, TrackballMode = ChartTrackballMode.Vertical }
        : new FunnelChart { ItemsSource = new[] { new Row("one", 1) }, IdPath = "Id", ValuePath = "Value", TrackballMode = ChartTrackballMode.Vertical };
    private sealed class Host : IDisposable
    {
        internal Window Window { get; }
        internal Host(ChartBase chart) { Window = new Window { Content = chart, Width = 480, Height = 320 }; Window.Show(); Drain(Window); }
        public void Dispose() { Window.Close(); Dispatcher.UIThread.RunJobs(); }
    }
    private static void Drain(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static void Update(ChartBase chart)
    { if (chart is EngineeringChartBase e) e.UpdateTrackball(e.GetMarks()[0].Position); else { var a = (AnalysisChartBase)chart; a.UpdateTrackball(a.GetMarks()[0].LabelPosition); } }
    private static GeometryDrawing[] Draw(ChartBase chart)
    {
        var lines = chart.GetVisualChildren().OfType<Control>().Single(control => control.Name == "PART_ChartDataTrackballLines");
        var group = new DrawingGroup(); using (var context = group.Open()) lines.Render(context);
        return Shapes(group).ToArray();
    }
    private static IEnumerable<GeometryDrawing> Shapes(Drawing drawing)
    { if (drawing is DrawingGroup group) foreach (var child in group.Children) foreach (var shape in Shapes(child)) yield return shape; else if (drawing is GeometryDrawing geometry) yield return geometry; }
    private sealed record Row(string Id, double Value);
    private sealed class EngineeringProbe : EngineeringChartBase
    {
        internal EngineeringProbe() { IdPath = "Id"; ObservePaths("Value"); }
        protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot) =>
            data.Count == 0 ? [] : [new(data.Points, new RectangleGeometry(plot.Deflate(10)), Brushes.White, plot.Center)];
    }
}
