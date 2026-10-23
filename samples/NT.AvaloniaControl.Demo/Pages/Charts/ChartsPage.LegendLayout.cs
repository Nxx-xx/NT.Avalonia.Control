using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private void AddLegendLayoutActions(WrapPanel actions)
    {
        var floating = new CheckBox { Content = "浮动图例", Margin = new Thickness(0, 0, 14, 6) };
        floating.IsCheckedChanged += (_, _) => { foreach (var chart in _commonCharts) chart.IsLegendFloating = floating.IsChecked == true; };
        var alignment = new ComboBox { ItemsSource = new[] { "图例靠起点", "图例居中", "图例靠终点" }, SelectedIndex = 0, MinWidth = 120, Margin = new Thickness(0, 0, 8, 6) };
        alignment.SelectionChanged += (_, _) =>
        {
            foreach (var chart in _commonCharts)
            {
                chart.LegendHorizontalAlignment = alignment.SelectedIndex switch { 1 => HorizontalAlignment.Center, 2 => HorizontalAlignment.Right, _ => HorizontalAlignment.Left };
                chart.LegendVerticalAlignment = alignment.SelectedIndex switch { 1 => VerticalAlignment.Center, 2 => VerticalAlignment.Bottom, _ => VerticalAlignment.Top };
            }
        };
        actions.Children.Add(floating); actions.Children.Add(alignment);
    }
}
