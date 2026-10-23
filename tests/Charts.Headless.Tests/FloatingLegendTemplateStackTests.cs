using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class FloatingLegendTemplateStackTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Floating_legend_stays_above_label_controls_and_exports_each_layer_once(bool templatedLegend)
    {
        var chart = new CartesianChart
        {
            Padding = new Thickness(8), IsLegendFloating = true,
            LegendPosition = ChartLegendPosition.Right,
            XAxis = new NumericAxis { Minimum = 0, Maximum = 10 },
            YAxis = new NumericAxis { Minimum = 0, Maximum = 100 },
        };
        var labeledSeries = new ScatterSeries
        {
            Title = "original label series", ShowDataLabels = true, Stroke = Brushes.Navy, Fill = Brushes.Navy,
            ItemsSource = new ChartPoint[] { new(8, 80) },
            DataLabelTemplate = new FuncDataTemplate<ChartDataLabelContext>((_, _) => new Border
            {
                Name = "StackedLabelContent", Width = 96, Height = 26,
                Background = Brushes.OrangeRed,
            }),
        };
        var secondSeries = new LineSeries
        {
            Title = "original line series", Stroke = Brushes.Navy, Fill = Brushes.Navy,
            ItemsSource = new ChartPoint[] { new(2, 20), new(8, 80) },
        };
        chart.Series.Add(labeledSeries);
        chart.Series.Add(secondSeries);
        if (templatedLegend)
            chart.LegendItemTemplate = new FuncDataTemplate<ChartLegendItem>((_, _) => new Border
            {
                Name = "StackedLegendContent", Width = 126, Height = 28,
                Background = Brushes.DarkCyan,
            });
        var drawnLegendCalls = 0;
        chart.LegendRenderer = (context, item) =>
        {
            drawnLegendCalls++;
            context.DrawRectangle(Brushes.Magenta, null, item.Bounds);
        };

        using var host = new TemplateFeaturesHost(chart);
        var labelsHost = Assert.Single(chart.GetVisualDescendants().OfType<Control>(), control => control.Name == "PART_ChartDataLabelsOverlay");
        var legendHost = Assert.Single(chart.GetVisualDescendants().OfType<Control>(), control => control.Name == "PART_ChartLegend");
        Assert.Same(chart, labelsHost.GetVisualParent());
        Assert.Same(chart, legendHost.GetVisualParent());
        Assert.True(legendHost.ZIndex > labelsHost.ZIndex);
        Assert.False(chart.IsLegendScrollable);
        Assert.Null(chart.LegendMaxWidth);
        Assert.Null(chart.LegendMaxHeight);

        var label = Assert.Single(labelsHost.GetVisualDescendants().OfType<Border>(), control => control.Name == "StackedLabelContent");
        Assert.Same(labeledSeries, Assert.IsType<ChartDataLabelContext>(label.DataContext).Series);
        var legendControls = legendHost.GetVisualDescendants().OfType<Control>().Where(control => templatedLegend
            ? control.Name == "StackedLegendContent"
            : control.DataContext is ChartLegendItem && !control.GetVisualChildren().Any()).ToArray();
        Assert.Equal(2, legendControls.Length);
        foreach (var series in chart.Series)
        {
            var control = Assert.Single(legendControls, control => control.DataContext is ChartLegendItem item && ReferenceEquals(item.Series, series));
            Assert.True(control.Bounds.Width > 0 && control.Bounds.Height > 0);
            Assert.Equal(series.Title, Assert.IsType<ChartLegendItem>(control.DataContext).Text);
        }

        // A chart's own recording must not also draw either child overlay.
        var before = drawnLegendCalls;
        var ownRecording = TemplateFeaturesHost.Record(chart);
        Assert.Equal(before, drawnLegendCalls);
        Assert.DoesNotContain(TemplateFeaturesHost.Walk(ownRecording).OfType<GeometryDrawing>(), drawing => drawing.Brush is VisualBrush);
        Assert.DoesNotContain(TemplateFeaturesHost.Walk(ownRecording).OfType<GeometryDrawing>(), drawing =>
            drawing.Brush is ISolidColorBrush brush && (brush.Color == Colors.Magenta || brush.Color == Colors.OrangeRed || brush.Color == Colors.DarkCyan));

        var export = new DrawingGroup();
        using (var drawing = export.Open())
            chart.RenderExport(drawing, chart.GetExportLayout(new() { PixelSize = new(960, 540), Dpi = 144 }), Brushes.White);
        var overlayBrushes = TemplateFeaturesHost.Walk(export).OfType<GeometryDrawing>()
            .Select(drawing => drawing.Brush).OfType<VisualBrush>().ToArray();
        Assert.Equal(2, overlayBrushes.Length);
        Assert.Same(labelsHost, overlayBrushes[0].Visual);
        Assert.Same(legendHost, overlayBrushes[1].Visual);
        Assert.Equal(before, drawnLegendCalls);

        // With both template features removed there is no reason to retain the
        // implicit control legend; the existing floating drawing path resumes.
        labeledSeries.DataLabelTemplate = null;
        chart.LegendItemTemplate = null;
        host.Drain();
        Assert.DoesNotContain(chart.GetVisualDescendants().OfType<Control>(), control =>
            control.Name is "PART_ChartDataLabelsOverlay" or "PART_ChartLegend");
        TemplateFeaturesHost.AssertReleased(label);
        var beforeRaw = drawnLegendCalls;
        _ = TemplateFeaturesHost.Record(chart);
        Assert.Equal(beforeRaw + 2, drawnLegendCalls);
    }
}
