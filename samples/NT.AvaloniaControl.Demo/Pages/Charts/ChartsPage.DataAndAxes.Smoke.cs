using Avalonia;
using Avalonia.Controls;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private async Task RunDataAndAxesSmokeAsync(Dictionary<string, bool> checks, string imagePath)
    {
        if (_advancedExamples is null) throw new InvalidOperationException("业务与坐标轴示例未创建。");
        _advancedExamples.IsExpanded = true;
        try
        {
            await SmokeFlushAsync();
            _advancedExamples.BringIntoView();
            await SmokeFlushAsync();
            checks["C1业务图表均已布局"] = _advancedCharts.Count >= 3 && _advancedCharts.Take(3).All(chart => chart.Bounds.Width > 250 && chart.PlotArea.Width > 80 && chart.PlotArea.Height > 100);
            var dates = _advancedCharts[0];
            var multi = _advancedCharts[1];
            var logarithmic = _advancedCharts[2];
            checks["C1日期与对数轴已使用"] = dates.XAxis is DateTimeAxis && logarithmic.XAxis is LogarithmicAxis && logarithmic.YAxis is LogarithmicAxis;
            var dateBefore = dates.Series[0].CreateSnapshot();
            var oldMaximum = dates.VisibleRange?.YMaximum;
            _dailyRequests[3].Value += 200;
            _channelResults[1].Secondary = 11;
            await SmokeFlushAsync();
            var dateAfter = dates.Series[0].CreateSnapshot();
            checks["C1业务属性通知更新图表"] = dateAfter.Version > dateBefore.Version && oldMaximum is { } old && dates.VisibleRange is { } updated && updated.YMaximum > old &&
                dateAfter.Points[3].Point.Y == _dailyRequests[3].Value && ReferenceEquals(dateAfter.Points[3].Item, _dailyRequests[3]);
            checks["C1日期原始值保持Unix毫秒"] = dateAfter.Points[3].Point.X == _dailyRequests[3].X &&
                DateTimeAxis.FromUnixMilliseconds(dateAfter.Points[3].Point.X).Day == 4;

            var conversionAxis = multi.Axes.Single(axis => axis.Key == "conversion");
            var multiRange = multi.VisibleRange ?? throw new InvalidOperationException("多坐标轴图表尚无视图。");
            var conversion = _channelResults[1];
            var multiPosition = new Point(multi.PlotArea.Left + (1 - multiRange.XMinimum) / (multiRange.XMaximum - multiRange.XMinimum) * multi.PlotArea.Width,
                multi.PlotArea.Bottom - conversion.Secondary / 12 * multi.PlotArea.Height);
            var multiHit = multi.HitTestPoint(multiPosition);
            checks["C1次轴命中返回原始业务项"] = conversionAxis.Position == ChartAxisPosition.Far && multi.Series[1].YAxisKey == "conversion" &&
                multiHit is not null && ReferenceEquals(multiHit.Series, multi.Series[1]) && ReferenceEquals(multiHit.Item, conversion) && Equals(multiHit.StableId, conversion.Id) && multiHit.Point.Y == conversion.Secondary;

            var record = _processingTimes[4];
            var plot = logarithmic.PlotArea;
            var logPosition = new Point(plot.Left + (Math.Log10(record.X) - 2) / 4 * plot.Width,
                plot.Bottom - Math.Log10(record.Value) / 4 * plot.Height);
            var logHit = logarithmic.HitTestPoint(logPosition);
            checks["C1对数命中返回原始数值"] = logHit is not null && ReferenceEquals(logHit.Series, logarithmic.Series[0]) && ReferenceEquals(logHit.Item, record) &&
                logHit.Point.X == record.X && logHit.Point.Y == record.Value && Equals(logHit.StableId, record.Id);
            CaptureSmokeImage(imagePath);
        }
        finally
        {
            UpdateBusinessData();
            _advancedExamples.IsExpanded = false;
            _scroll.Offset = default;
            await SmokeFlushAsync();
        }
    }
}
