using Avalonia.Controls;
using NT.AvaloniaControl.Demo.Navigation;

namespace NT.AvaloniaControl.Demo.Pages;

internal sealed class OverviewPage : UserControl, IDemoPage
{
    public OverviewPage(IEnumerable<DemoPageDefinition> controls, Action<string> navigate)
    {
        Name = "OverviewPage";
        var definitions = controls.ToArray();
        var items = definitions.Select(page => new DemoNavigationItem(page.Title, page.Description, page.Id switch
        {
            "treedatagrid" => "table", "markdown" => "doc", "pdfviewer" => "pdf", "virtualkeyboard" => "keys", _ => "line",
        })).ToArray();
        Content = DemoGallery.Create("探索控件", "选择一个控件，浏览它的分类与交互示例。", items,
            item => navigate(definitions[Array.IndexOf(items, item)].Id));
    }

    public Control View => this;
    public IReadOnlyList<DemoNavigationItem> CreateGallery() => [];
    public void Activate() { }
    public void Deactivate() { }
    public void Dispose() { }
}
