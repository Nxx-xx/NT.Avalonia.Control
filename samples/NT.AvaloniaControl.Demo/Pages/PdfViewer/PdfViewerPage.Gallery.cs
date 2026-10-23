using NT.AvaloniaControl.Demo.Navigation;
using NT.AvaloniaControl.PdfViewer;

namespace NT.AvaloniaControl.Demo.Pages.PdfViewer;

internal sealed partial class PdfViewerPage
{
    public IReadOnlyList<DemoNavigationItem> CreateGallery()
    {
        DemoNavigationItem Example(string title, string description, Action configure) => new(title, description, "pdf", open: () =>
        {
            configure();
            return this;
        });
        return
        [
            DemoNavigationItem.Group("文档浏览", "内置示例与本地 PDF 阅读", "pdf",
                Example("连续阅读", "连续滚动、缩略图与页面导航", () => _reader.PageDisplayMode = PdfPageDisplayMode.Continuous),
                Example("打开本地文档", "使用“打开本地 PDF”加载文档，支持密码", () => { })),
            DemoNavigationItem.Group("缩放与布局", "适合页面、宽度与双页显示", "pdf",
                Example("适合页面", "完整显示当前页面", () => { _reader.PageDisplayMode = PdfPageDisplayMode.SinglePage; _reader.ZoomMode = PdfZoomMode.FitPage; }),
                Example("适合宽度", "按照阅读区域宽度缩放", () => { _reader.PageDisplayMode = PdfPageDisplayMode.Continuous; _reader.ZoomMode = PdfZoomMode.FitWidth; }),
                Example("双页阅读", "双页排列与连续浏览", () => _reader.PageDisplayMode = PdfPageDisplayMode.TwoPageContinuous)),
            DemoNavigationItem.Group("阅读交互", "搜索、目录与文字选择", "pdf",
                Example("搜索与复制", "使用工具栏搜索，拖动选择文字后复制", () => { }),
                Example("缩略图与目录", "使用侧栏定位页面与文档书签", () => _reader.IsSidebarVisible = true)),
        ];
    }
}
