using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class MainWindow
{
    internal CartesianChart EncodedScatterChart => this.FindControl<CartesianChart>("UsageEncodedScatterChart")!;
    internal CartesianChart BubbleChart => this.FindControl<CartesianChart>("UsageBubbleChart")!;
    internal CartesianChart ScatterLineChart => this.FindControl<CartesianChart>("UsageScatterLineChart")!;
    internal List<object> ScatterBubbleBindingDiagnostics { get; } = [];

    private void ConfigureScatterBubbleLabels()
    {
        foreach (var chart in new[] { EncodedScatterChart, BubbleChart, ScatterLineChart })
            foreach (var series in chart.Series) series.DataLabelFormatter = context => (context.Data.SizeValue ?? context.Data.Point.Y).ToString("0.##", System.Globalization.CultureInfo.CurrentCulture);
    }

    internal void VerifyScatterBubbleBindings(Dictionary<string, bool> checks)
    {
        var expander = this.FindControl<Expander>("ScatterBubbleExamples")!; expander.IsExpanded = true; FlushBindings();
        var chart = EncodedScatterChart; var bubbleChart = BubbleChart; var lineChart = ScatterLineChart;
        foreach (var item in new[] { chart, bubbleChart, lineChart }) _ = item.PlotArea; FlushBindings();
        var scatter = (ScatterSeries)chart.Series[0]; var bubble = (BubbleSeries)bubbleChart.Series[0]; var line = (ScatterLineSeries)lineChart.Series[0];
        var root = (ChartAutomationPeer)ControlAutomationPeer.CreatePeerForElement(chart)!;
        var bubbleRoot = (ChartAutomationPeer)ControlAutomationPeer.CreatePeerForElement(bubbleChart)!;
        ChartPointHit? Read() { _ = chart.PlotArea; FlushBindings(); return root.GetPointPeer(scatter, 1)?.CurrentPoint; }
        static bool Near(double? value, double expected) => value is { } number && double.IsFinite(number) && Math.Abs(number - expected) <= 1e-9;
        checks["scatter-bubble-compiled-types-and-real-business-sources"] = bubble is PointSeries && line is PointSeries && ReferenceEquals(scatter.ItemsSource, Model.ScatterBubbleRecords) && ReferenceEquals(bubble.ItemsSource, Model.ScatterBubbleRecords) && ReferenceEquals(line.ItemsSource, Model.ScatterLinePoints);
        checks["scatter-bubble-compiled-paths-domain-and-colors"] = scatter.SizePath == "Weight" && scatter.ColorPath == "Color" && scatter.MinimumSize == 20 && scatter.MaximumSize == 60 && scatter.SizeMinimum == 0 && scatter.SizeMaximum == 100 && scatter.MinimumColor == Colors.Blue && scatter.MaximumColor == Colors.Red;
        var original = Read();
        checks["scatter-bubble-compiled-area-scale-has-expected-diameter"] = original is { Index: 1, SizeValue: 50, Point.X: 5, Point.Y: 55 } && Near(original.MarkerDiameter, Math.Sqrt(2000));
        checks["scatter-bubble-compiled-point-preserves-raw-color-and-identity"] = original is not null && Equals(original.ColorValue, 50d) && ReferenceEquals(original.Item, Model.ScatterBubbleRecords[1]) && Equals(original.StableId, "usage-bubble-b") && original.PointFill is ISolidColorBrush;
        Model.ScatterBubbleSizeScale = ChartSizeScale.Diameter; FlushBindings();
        checks["scatter-bubble-compiled-model-updates-both-size-scales"] = scatter.SizeScale == ChartSizeScale.Diameter && bubble.SizeScale == ChartSizeScale.Diameter && Near(Read()?.MarkerDiameter, 40);
        this.FindControl<ComboBox>("UsageScatterBubbleSizeScale")!.SelectedItem = ChartSizeScale.Area; FlushBindings();
        checks["scatter-bubble-compiled-size-scale-selector-is-two-way"] = Model.ScatterBubbleSizeScale == ChartSizeScale.Area && Near(Read()?.MarkerDiameter, Math.Sqrt(2000));
        Model.ScatterBubbleDrawOrder = ChartPointDrawOrder.SizeDescending; FlushBindings();
        checks["scatter-bubble-compiled-model-updates-draw-order"] = scatter.DrawOrder == ChartPointDrawOrder.SizeDescending && bubble.DrawOrder == ChartPointDrawOrder.SizeDescending;
        this.FindControl<ComboBox>("UsageScatterBubbleDrawOrder")!.SelectedItem = ChartPointDrawOrder.SizeAscending; FlushBindings();
        checks["scatter-bubble-compiled-draw-order-selector-is-two-way"] = Model.ScatterBubbleDrawOrder == ChartPointDrawOrder.SizeAscending && scatter.DrawOrder == ChartPointDrawOrder.SizeAscending && scatter.CreateSnapshot().Points[1].SourceIndex == 1;
        Model.ScatterBubbleLabels = true; FlushBindings();
        checks["scatter-bubble-compiled-label-toggle-and-size-formatter"] = scatter.ShowDataLabels && bubble.ShowDataLabels && line.ShowDataLabels && bubble.DataLabelFormatter is not null;
        this.FindControl<CheckBox>("UsageScatterBubbleLabels")!.IsChecked = false; FlushBindings();
        checks["scatter-bubble-compiled-label-checkbox-is-two-way"] = !Model.ScatterBubbleLabels && !scatter.ShowDataLabels && !bubble.ShowDataLabels;
        Model.ScatterBubbleShowLine = false; FlushBindings();
        checks["scatter-bubble-compiled-model-hides-line-without-replacing-source"] = !line.ShowLine && ReferenceEquals(line.ItemsSource, Model.ScatterLinePoints);
        this.FindControl<CheckBox>("UsageScatterBubbleShowLine")!.IsChecked = true; FlushBindings();
        checks["scatter-bubble-compiled-line-checkbox-is-two-way"] = Model.ScatterBubbleShowLine && line.ShowLine;
        var segment = lineChart.HitTestSegment(ScatterBubbleProject(lineChart, 4, 50), 3);
        checks["scatter-bubble-compiled-scatter-line-uses-nonmonotonic-source-order"] = segment is { Start.Index: 0, End.Index: 1 } && Near(segment.Point.X, 4) && Near(segment.Point.Y, 50);
        checks["scatter-bubble-compiled-scatter-line-keeps-gap"] = line.CreateSnapshot().Points[3].IsEmpty && lineChart.HitTestSegment(ScatterBubbleProject(lineChart, 5, 40), 3) is null;
        checks["scatter-bubble-compiled-weightpath-fallback-preserves-raw-weight"] = bubble.SizePath is null && bubble.WeightPath == "Weight" && bubble.CreateSnapshot().Points[1].Weight == 50 && Near(bubbleRoot.GetPointPeer(bubble, 1)?.CurrentPoint?.MarkerDiameter, Math.Sqrt(2000));
        var version = scatter.SourceVersion; Model.ScatterBubbleRecords[1].Weight = 75; _ = chart.PlotArea; _ = bubbleChart.PlotArea; FlushBindings();
        var changedSize = Read();
        checks["scatter-bubble-compiled-inpc-size-updates-scatter-and-bubble"] = scatter.SourceVersion > version && changedSize is { SizeValue: 75 } && Near(changedSize.MarkerDiameter, Math.Sqrt(2800)) && Near(bubbleRoot.GetPointPeer(bubble, 1)?.CurrentPoint?.MarkerDiameter, Math.Sqrt(2800));
        Model.ScatterBubbleRecords[1].Color = 25; _ = chart.PlotArea; FlushBindings();
        checks["scatter-bubble-compiled-inpc-color-updates-raw-color"] = Equals(Read()?.ColorValue, 25d);
        bubble.SizePath = "Color"; _ = bubbleChart.PlotArea; FlushBindings();
        checks["scatter-bubble-compiled-sizepath-takes-precedence-over-weightpath"] = bubbleRoot.GetPointPeer(bubble, 1)?.CurrentPoint is { SizeValue: 25 } precedence && Near(precedence.MarkerDiameter, Math.Sqrt(1200));
        bubble.SizePath = null; Model.ScatterBubbleRecords[1].Weight = -1; _ = bubbleChart.PlotArea; FlushBindings();
        checks["scatter-bubble-compiled-negative-size-hides-marker-keeps-data-point"] = bubbleRoot.GetPointPeer(bubble, 1)?.CurrentPoint is { Point.X: 5, Point.Y: 55, SizeValue: -1 } && bubbleChart.HitTestPoint(ScatterBubbleProject(bubbleChart, 5, 55), 0) is null;
        Model.ScatterBubbleRecords[1].Weight = 50; Model.ScatterBubbleRecords[1].Color = 50; _ = chart.PlotArea; _ = bubbleChart.PlotArea; FlushBindings();
        var current = Read(); if (current is not null) chart.SelectedPoints = new[] { current }; FlushBindings();
        checks["scatter-bubble-compiled-selection-keeps-current-encoding-and-business-item"] = chart.SelectedPoint is { SizeValue: 50, Index: 1 } selected && ReferenceEquals(selected.Item, Model.ScatterBubbleRecords[1]) && Near(selected.MarkerDiameter, Math.Sqrt(2000));
        checks["scatter-bubble-compiled-tooltip-template-and-datacontext-inheritance"] = chart.TooltipContentTemplate is not null && ReferenceEquals(chart.TooltipContentTemplate, bubbleChart.TooltipContentTemplate) && ReferenceEquals(scatter.DataContext, Model) && ReferenceEquals(bubble.DataContext, Model);
        var content = Content; Content = null; FlushBindings();
        checks["scatter-bubble-compiled-detach-releases-business-observers"] = Model.ScatterBubbleRecords.All(item => item.SubscriberCount == 0);
        Content = content; FlushBindings(); _ = chart.PlotArea; _ = bubbleChart.PlotArea;
        checks["scatter-bubble-compiled-reattach-restores-business-observers"] = Model.ScatterBubbleRecords.All(item => item.SubscriberCount > 0);
        ScatterBubbleBindingDiagnostics.Add(new { original = original is null ? null : new { original.Point, original.Index, original.StableId, original.SizeValue, original.ColorValue, original.MarkerDiameter },
            changedSize = changedSize is null ? null : new { changedSize.Point, changedSize.SizeValue, changedSize.MarkerDiameter }, physicalInputValidated = false, performanceMeasured = false, derivedMetadataAbsoluteTolerance = 1e-9 });
        chart.ClearSelection(); Model.ScatterBubbleSizeScale = ChartSizeScale.Area; Model.ScatterBubbleDrawOrder = ChartPointDrawOrder.Source; Model.ScatterBubbleLabels = false; Model.ScatterBubbleShowLine = true;
        expander.IsExpanded = false; FlushBindings();
    }

    internal static Point ScatterBubbleProject(CartesianChart chart, double x, double y)
    {
        var plot = chart.PlotArea; var range = chart.VisibleRange!.Value;
        return new(plot.Left + (x - range.XMinimum) / (range.XMaximum - range.XMinimum) * plot.Width, plot.Bottom - (y - range.YMinimum) / (range.YMaximum - range.YMinimum) * plot.Height);
    }
}
