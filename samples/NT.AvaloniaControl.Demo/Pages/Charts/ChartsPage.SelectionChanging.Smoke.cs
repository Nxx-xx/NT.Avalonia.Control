using System.Windows.Input;
using Avalonia;
using Avalonia.Input;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private void RunSelectionChangingSmoke(Dictionary<string, bool> checks, CartesianChart chart)
    {
        _cancelSelectionOption.IsChecked = false;
        chart.ClearSelection(); chart.Focus(); RouteCommonKey(chart, Key.Right);
        var original = chart.SelectedPoint ?? throw new InvalidOperationException("选择拦截示例没有可选数据。");
        var originalList = chart.SelectedPoints;
        var oldCommand = chart.PointInvokedCommand;
        var invoked = 0; var changed = 0;
        var changes = new List<ChartSelectionChangingEventArgs>();
        void Changing(object? sender, ChartSelectionChangingEventArgs args) => changes.Add(args);
        void Changed(object? sender, ChartSelectionChangedEventArgs args) => changed++;
        chart.SelectionChanging += Changing; chart.SelectionChanged += Changed;
        chart.PointInvokedCommand = new SelectionSmokeCommand(_ => invoked++);
        var targetSeries = chart.Series[0];
        try
        {
            _cancelSelectionOption.IsChecked = true;
            var range = chart.VisibleRange!.Value; var plot = chart.PlotArea; var point = _commonResponse[5];
            var position = new Point(plot.Left + (point.X - range.XMinimum) / (range.XMaximum - range.XMinimum) * plot.Width,
                plot.Bottom - (point.Y - range.YMinimum) / (range.YMaximum - range.YMinimum) * plot.Height);
            var candidate = chart.HitTestPoint(position) ?? throw new InvalidOperationException("选择拦截示例目标未命中。");
            RouteCommonClick(chart, position);
            RouteCommonKey(chart, Key.Right);
            var rectangle = chart.SelectRectangle(plot);
            chart.ClearSelection();
            chart.SetCurrentValue(CartesianChart.SelectedPointProperty, candidate);
            checks["C2选择拦截开关覆盖点击键盘框选清除和外部赋值"] = ReferenceEquals(chart.SelectedPoints, originalList) &&
                Equals(chart.SelectedPoint, original) && changed == 0 && rectangle.IsCanceled && rectangle.SelectedCount == 1 &&
                changes.Where(args => args.CanCancel).Select(args => args.Reason).SequenceEqual(new[]
                {
                    ChartSelectionChangeReason.Pointer, ChartSelectionChangeReason.Keyboard, ChartSelectionChangeReason.Rectangle,
                    ChartSelectionChangeReason.Clear, ChartSelectionChangeReason.ExternalValue,
                });
            checks["C2取消选择仍执行原始数据点击命令"] = invoked == 1;
            checks["C2选择拦截状态提供可见反馈"] = _selectionChangingStatus.Text?.Contains("已拦截", StringComparison.Ordinal) == true;
            _cancelSelectionOption.IsChecked = false;
            RouteCommonClick(chart, position);
            checks["C2关闭选择拦截后路由点击恢复选择"] = Equals(chart.SelectedPoint?.StableId, candidate.StableId) && !Equals(chart.SelectedPoint, original);
            _cancelSelectionOption.IsChecked = true;
            targetSeries = chart.SelectedPoint!.Series;
            targetSeries.IsVisible = false;
            checks["C2选择拦截不会保留隐藏系列的失效身份"] = CommonSelection(chart).Count == 0 &&
                changes.Any(args => args.Reason == ChartSelectionChangeReason.Reconcile && !args.CanCancel);
        }
        finally
        {
            chart.SelectionChanging -= Changing; chart.SelectionChanged -= Changed;
            _cancelSelectionOption.IsChecked = false;
            targetSeries.IsVisible = true;
            chart.PointInvokedCommand = oldCommand;
            chart.ClearSelection();
        }
    }

    private sealed class SelectionSmokeCommand(Action<object?> execute) : ICommand
    {
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute(parameter);
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}
