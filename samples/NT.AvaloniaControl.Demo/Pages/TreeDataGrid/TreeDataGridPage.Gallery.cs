using Avalonia;
using Avalonia.Controls;
using NT.AvaloniaControl.Demo.Navigation;

namespace NT.AvaloniaControl.Demo.Pages.TreeDataGrid;

internal sealed partial class TreeDataGridPage
{
    public IReadOnlyList<DemoNavigationItem> CreateGallery()
    {
        var tabs = _modeTabs.ItemsSource!.Cast<TabItem>().ToArray();
        var normal = (Control)tabs[0].Content!;
        normal.Margin = new Thickness(24, 20);
        if (_mvvmView.Content is Control mvvmContent) mvvmContent.Margin = new Thickness(24, 20);
        foreach (var tab in tabs) tab.Content = null;
        _scenarioPicker.IsVisible = false;
        if (_scenarioPicker.Parent is Panel tools) tools.Children[0].IsVisible = false;

        DemoNavigationItem Scenario(DemoScenarioChoice choice) => new(choice.Label, choice.Description, "table", open: () =>
        {
            _modeTabs.SelectedIndex = 0;
            Content = normal;
            if (!ReferenceEquals(_scenarioPicker.SelectedItem, choice)) _scenarioPicker.SelectedItem = choice;
            return this;
        });
        DemoNavigationItem Group(string title, string description, params string[] ids) =>
            DemoNavigationItem.Group(title, description, "table", ids.Select(id => Scenario(_scenarioChoices.Single(choice => choice.Id == id))).ToArray());

        return
        [
            Group("基础与编辑", "编辑校验、列类型、外观与冻结", "editing", "columns", "visibility", "appearance"),
            Group("树结构与行操作", "层级数据、按需加载与节点移动", "tree", "lazy-tree", "rowmoving"),
            Group("自定义列模板", "按钮、选色盘、时钟与组合控件", "template-buttons", "template-colors", "template-clock", "template-composite"),
            DemoNavigationItem.Group("数据绑定", "MVVM 与嵌套绑定", "table",
                new DemoNavigationItem("MVVM 模式", "声明式列、绑定与 ViewModel 交互", "table", open: () =>
                {
                    _modeTabs.SelectedIndex = 1;
                    Content = _mvvmView;
                    return this;
                }), Scenario(_scenarioChoices.Single(choice => choice.Id == "nestedbindings"))),
            Group("大数据与滚动", "十万行、百列与虚拟化", "flat", "wide"),
        ];
    }
}
