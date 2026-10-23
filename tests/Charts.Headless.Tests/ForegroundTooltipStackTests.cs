using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class ForegroundTooltipStackTests
{
    [AvaloniaFact]
    public void Ordinary_tooltip_has_one_foreground_layer_and_yields_to_a_tooltip_template()
    {
        var chart = new CartesianChart
        {
            Padding = new Thickness(8), IsLegendFloating = true, LegendPosition = ChartLegendPosition.Right,
            XAxis = new NumericAxis { Minimum = 0, Maximum = 10 },
            YAxis = new NumericAxis { Minimum = 0, Maximum = 100 },
            TooltipBackground = Brushes.HotPink,
            LegendItemTemplate = new FuncDataTemplate<ChartLegendItem>((_, _) => new Border
            { Width = 100, Height = 28, Background = Brushes.DarkCyan }),
        };
        var series = new ScatterSeries
        {
            Title = "original series", Stroke = Brushes.Navy, Fill = Brushes.Navy,
            ItemsSource = new ChartPoint[] { new(2, 20), new(8, 80) }, ShowDataLabels = true,
            DataLabelTemplate = new FuncDataTemplate<ChartDataLabelContext>((_, _) => new Border
            { Width = 70, Height = 24, Background = Brushes.OrangeRed }),
        };
        chart.Series.Add(series);
        var formatterCalls = 0;
        chart.TooltipFormatter = _ => { formatterCalls++; return "one foreground tooltip"; };
        using var fixture = new TemplateFeaturesHost(chart);
        var labels = Find(chart, "PART_ChartDataLabelsOverlay");
        var legend = Find(chart, "PART_ChartLegend");
        var foreground = Find(chart, "PART_ChartForegroundTooltip");
        Assert.True(foreground.ZIndex > legend.ZIndex && foreground.ZIndex > labels.ZIndex);
        Assert.False(foreground.IsHitTestVisible);
        Assert.False(foreground.Focusable);
        Assert.Equal(new Rect(chart.Bounds.Size), foreground.Bounds);

        var position = new Point(chart.PlotArea.Left + chart.PlotArea.Width * .2, chart.PlotArea.Bottom - chart.PlotArea.Height * .2);
        var windowPosition = chart.TranslatePoint(position, fixture.Window)!.Value;
        using (var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true))
            chart.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, chart, pointer, fixture.Window,
                windowPosition, 1, default, KeyModifiers.None));
        fixture.Drain();
        Assert.Same(series, Assert.IsType<ChartPointHit>(chart.HoveredPoint).Series);

        var before = formatterCalls;
        Assert.False(HasTooltipFill(TemplateFeaturesHost.Record(chart)));
        Assert.Equal(before, formatterCalls);
        Assert.True(HasTooltipFill(Record(foreground)));
        Assert.Equal(before + 1, formatterCalls);

        before = formatterCalls;
        var exportedLayers = ExportBrushes(chart);
        Assert.Equal(3, exportedLayers.Length);
        Assert.Same(labels, exportedLayers[0].Visual);
        Assert.Same(legend, exportedLayers[1].Visual);
        Assert.Same(foreground, exportedLayers[2].Visual);
        Assert.Equal(before, formatterCalls);

        var tooltipContent = new TextBlock { Text = "control tooltip", Width = 110, Height = 25 };
        chart.TooltipContentTemplate = new FuncDataTemplate<ChartTooltipContext>((_, _) => tooltipContent);
        fixture.Drain();
        var templateTooltip = Find(chart, "PART_ChartTooltipOverlay");
        Assert.True(templateTooltip.ZIndex > foreground.ZIndex);
        Assert.Same(series, Assert.IsType<ChartTooltipContext>(tooltipContent.DataContext).Primary.Series);
        before = formatterCalls;
        Assert.False(HasTooltipFill(Record(foreground)));
        Assert.False(HasTooltipFill(TemplateFeaturesHost.Record(chart)));
        Assert.Equal(before, formatterCalls);
        exportedLayers = ExportBrushes(chart);
        Assert.Equal(3, exportedLayers.Length);
        Assert.Same(labels, exportedLayers[0].Visual);
        Assert.Same(legend, exportedLayers[1].Visual);
        Assert.Same(templateTooltip, exportedLayers[2].Visual);
        Assert.DoesNotContain(exportedLayers, brush => ReferenceEquals(brush.Visual, foreground));
        Assert.Equal(before, formatterCalls);

        fixture.Window.Content = null;
        fixture.Drain();
        TemplateFeaturesHost.AssertReleased(foreground);
        TemplateFeaturesHost.AssertReleased(tooltipContent);
        Assert.DoesNotContain(chart.GetVisualDescendants().OfType<Control>(), control =>
            control.Name is "PART_ChartForegroundTooltip" or "PART_ChartTooltipOverlay");
        before = formatterCalls;
        Assert.False(HasTooltipFill(Record(foreground)));
        Assert.Equal(before, formatterCalls);
    }

    private static Control Find(CartesianChart chart, string name) =>
        Assert.Single(chart.GetVisualDescendants().OfType<Control>(), control => control.Name == name);

    private static DrawingGroup Record(Control control)
    {
        var recording = new DrawingGroup();
        using (var drawing = recording.Open()) control.Render(drawing);
        return recording;
    }

    private static bool HasTooltipFill(Drawing drawing) => TemplateFeaturesHost.Walk(drawing)
        .OfType<GeometryDrawing>().Any(item => item.Brush is ISolidColorBrush brush && brush.Color == Colors.HotPink);

    private static VisualBrush[] ExportBrushes(CartesianChart chart)
    {
        var recording = new DrawingGroup();
        using (var drawing = recording.Open())
            chart.RenderExport(drawing, chart.GetExportLayout(new() { PixelSize = new(960, 540), Dpi = 144 }), Brushes.White);
        return TemplateFeaturesHost.Walk(recording).OfType<GeometryDrawing>()
            .Select(item => item.Brush).OfType<VisualBrush>().ToArray();
    }
}
