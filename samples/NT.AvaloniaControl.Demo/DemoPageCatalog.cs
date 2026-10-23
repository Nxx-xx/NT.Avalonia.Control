using Avalonia.Controls;
using NT.AvaloniaControl.Demo.Navigation;
using NT.AvaloniaControl.Demo.Pages;
using NT.AvaloniaControl.Demo.Pages.TreeDataGrid;
using NT.AvaloniaControl.Demo.Pages.VirtualKeyboard;
using NT.AvaloniaControl.Demo.Pages.Markdown;
using NT.AvaloniaControl.Demo.Pages.Charts;
using NT.AvaloniaControl.Demo.Pages.PdfViewer;

namespace NT.AvaloniaControl.Demo;

internal static class DemoPageCatalog
{
    internal static IReadOnlyList<DemoPageDefinition> Create(Window hostWindow, string[] args, Action<string> navigate)
    {
        var pages = new List<DemoPageDefinition>();
        pages.Add(new("overview", "概览", "浏览可用控件及其示例。",
            () => new OverviewPage(pages.Where(page => page.Id != "overview"), navigate)));
        pages.Add(new("treedatagrid", "TreeDataGrid", "平面与树形表格、编辑校验、MVVM 绑定及自定义列模板。",
            () => new TreeDataGridPage(hostWindow, args)));
        pages.Add(new("virtualkeyboard", "VirtualKeyboard", "数字与英文键盘、显式输入目标、输入约束与焦点保持。",
            () => new VirtualKeyboardPage(hostWindow)));
        pages.Add(new("markdown", "Markdown", "原生文档阅读、跨块选择复制、语义样式与可等待的流式会话。",
            () => new MarkdownPage(hostWindow)));
        pages.Add(new("charts", "Charts", "趋势、比较、分布、关系与地图，探索图表和交互示例。",
            () => new ChartsPage(hostWindow, galleryMode: !args.Any(arg => arg.Contains("smoke", StringComparison.Ordinal)))));
        pages.Add(new("pdfviewer", "PDF Viewer", "PDF 连续阅读、缩略图、目录、搜索、文字选择与复制。",
            () => new PdfViewerPage(hostWindow)));
        return pages;
    }
}
