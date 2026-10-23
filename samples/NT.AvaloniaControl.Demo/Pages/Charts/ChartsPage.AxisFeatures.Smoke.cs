using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private async Task RunAxisFeaturesSmokeAsync(Dictionary<string, bool> checks, string imagePath, string darkImagePath, string pixelsImagePath, string pixelsDarkImagePath)
    {
        if (_axisExamples is null || _independentAxis is null) throw new InvalidOperationException("坐标轴进阶示例尚未创建。");
        _axisExamples.IsExpanded = true;
        try
        {
            await SmokeFlushAsync();
            _axisExamples.BringIntoView();
            await SmokeFlushAsync();
            var labels = _axisShowcaseCharts[0];
            var breaks = _axisShowcaseCharts[1];
            var independent = _axisShowcaseCharts[2];
            checks["C1进阶坐标轴三图已布局"] = _axisShowcaseCharts.Count == 3 && _axisShowcaseCharts.All(chart => chart.PlotArea.Width > 60 && chart.PlotArea.Height > 100);

            var range = labels.VisibleRange;
            var withMinor = AxisGeometry(labels).Count;
            labels.YAxis.ShowMinorGridLines = false;
            var withoutMinor = AxisGeometry(labels).Count;
            checks["C1次网格切换改变绘制且不改数据范围"] = withMinor > withoutMinor && labels.VisibleRange == range;
            labels.YAxis.ShowMinorGridLines = true;
            var styled = AxisGeometry(labels).Count(drawing => HasAxisColor(drawing.Pen?.Brush, Color.Parse("#19A798")));
            labels.YAxis.ShowAxis = false;
            labels.YAxis.ShowLabels = false;
            labels.YAxis.ShowTicks = false;
            var hidden = AxisGeometry(labels).Count;
            labels.YAxis.ShowAxis = true;
            labels.YAxis.ShowLabels = true;
            labels.YAxis.ShowTicks = true;
            checks["C1逐轴颜色与可见性实际改变绘制"] = styled > 0 && AxisGeometry(labels).Count > hidden && labels.VisibleRange == range;
            checks["C1自动标签与类别间隙示例已接入"] = labels.XAxis is CategoryAxis { LabelMode: ChartAxisLabelMode.Auto, PlotMode: CategoryPlotMode.BetweenTicks, Gap: .5 } &&
                labels.GetAxisLayout(labels.XAxis) is { Mode: not ChartAxisLabelMode.Auto } && labels.VisibleRange is { XMinimum: <= -.5, XMaximum: >= 5.5 };

            var bandDrawings = AxisGeometry(breaks);
            checks["C1重复区间与前后图层实际绘制"] = bandDrawings.Count(drawing => HasAxisColor(drawing.Brush, Color.Parse("#304385F4"))) == 3 &&
                bandDrawings.Any(drawing => HasAxisColor(drawing.Brush, Color.Parse("#30F2A33A"))) && breaks.XAxis.PlotBands[0].Text == "巡检" &&
                breaks.YAxis.PlotBands[0].Layer == ChartPlotBandLayer.AboveSeries && breaks.YAxis.PlotBands[0].Opacity == .85;
            checks["C1断轴内部无数据点命中"] = breaks.YAxis.ScaleBreaks.Count == 1 && breaks.HitTestPoint(breaks.PlotArea.Center, tolerance: 0) is null;

            var primaryBefore = independent.VisibleRange;
            _independentAxis.ViewMinimum = .3;
            _independentAxis.ViewMaximum = .7;
            await SmokeFlushAsync();
            var plot = independent.PlotArea;
            var target = new ChartPoint(4, .42);
            var position = new Point(plot.Left + .4 * plot.Width, plot.Bottom - (.42 - .3) / .4 * plot.Height);
            var hit = independent.HitTestPoint(position, tolerance: 2);
            checks["C1独立次轴视图命中保持原始数值"] = independent.VisibleRange == primaryBefore && hit is not null && ReferenceEquals(hit.Series, independent.Series[1]) && hit.Point == target;
            checks["C1多轴NearFar与逐轴缩放限制已接入"] = independent.Axes.Any(axis => axis.Position == ChartAxisPosition.Near) &&
                independent.Axes.Any(axis => axis.Position == ChartAxisPosition.Far) && independent.XAxis.MaximumZoom == 8 && independent.YAxis.MaximumZoom == 4 && _independentAxis.MaximumZoom == 10;

            var windowPoint = independent.TranslatePoint(position, _hostWindow) ?? throw new InvalidOperationException("次轴点无法转换为窗口坐标。");
            using (var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true))
                independent.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, independent, pointer, _hostWindow, windowPoint, 1, default, KeyModifiers.None));
            checks["C1独立次轴路由提示命中"] = independent.HoveredPoint is { } hovered && ReferenceEquals(hovered.Series, independent.Series[1]) && hovered.Point == target;
            CaptureSmokeImage(imagePath);
            VerifyBreakPixels(checks, ThemeVariant.Light, pixelsImagePath);
            _hostWindow.RequestedThemeVariant = ThemeVariant.Dark;
            await SmokeFlushAsync();
            CaptureSmokeImage(darkImagePath);
            VerifyBreakPixels(checks, ThemeVariant.Dark, pixelsDarkImagePath);
            independent.ResetViewport();
            checks["C1重置清除独立次轴范围"] = independent.Viewport is null && _independentAxis.ViewMinimum is null && _independentAxis.ViewMaximum is null;
        }
        finally
        {
            _hostWindow.RequestedThemeVariant = ThemeVariant.Light;
            _axisExamples.IsExpanded = false;
            _scroll.Offset = default;
            await SmokeFlushAsync();
        }
    }

    private static List<GeometryDrawing> AxisGeometry(CartesianChart chart)
    {
        var group = new DrawingGroup();
        using (var drawing = group.Open()) chart.Render(drawing);
        return AxisDrawings(group).OfType<GeometryDrawing>().ToList();
    }

    private static IEnumerable<Drawing> AxisDrawings(Drawing drawing)
    {
        yield return drawing;
        if (drawing is DrawingGroup group)
            foreach (var child in group.Children)
                foreach (var descendant in AxisDrawings(child)) yield return descendant;
    }

    private static bool HasAxisColor(IBrush? brush, Color color) => brush is ISolidColorBrush solid && solid.Color == color;
}
