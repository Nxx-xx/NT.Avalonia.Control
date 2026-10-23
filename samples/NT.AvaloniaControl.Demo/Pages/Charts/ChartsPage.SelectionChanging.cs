using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private readonly CheckBox _cancelSelectionOption = new()
    {
        Name = "ChartCancelSelectionOption", Content = "拦截选择变更", Margin = new Thickness(0, 0, 14, 6),
    };
    private readonly TextBlock _selectionChangingStatus = new()
    {
        Name = "ChartSelectionChangingStatus", Text = "关闭拦截时可正常选择。", Opacity = .7,
        TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 6),
    };

    private void AddSelectionChangingActions(WrapPanel actions, CartesianChart chart)
    {
        ToolTip.SetTip(_cancelSelectionOption, "启用后保留当前选择。隐藏系列、删除数据或关闭选择仍会清理失效的选择。");
        _cancelSelectionOption.IsCheckedChanged += (_, _) => _selectionChangingStatus.Text = _cancelSelectionOption.IsChecked == true
            ? "拦截已开启：尝试点击、方向键、框选或清除选择。" : "拦截已关闭，可正常选择。";
        chart.SelectionChanging += (_, args) =>
        {
            if (_cancelSelectionOption.IsChecked != true) return;
            if (args.CanCancel)
            {
                args.Cancel = true;
                var action = args.Reason switch
                {
                    ChartSelectionChangeReason.Pointer => "点击", ChartSelectionChangeReason.Keyboard => "方向键",
                    ChartSelectionChangeReason.Rectangle => "框选", ChartSelectionChangeReason.Clear => "清除",
                    _ => "外部赋值",
                };
                _selectionChangingStatus.Text = $"已拦截{action}，保留当前选择。";
            }
            else _selectionChangingStatus.Text = "已清理失效的选择；清理不受拦截开关影响。";
        };
        actions.Children.Add(_cancelSelectionOption);
        actions.Children.Add(_selectionChangingStatus);
    }
}
