using NT.AvaloniaControl.Demo.Navigation;

namespace NT.AvaloniaControl.Demo.Pages.Markdown;

internal sealed partial class MarkdownPage
{
    public IReadOnlyList<DemoNavigationItem> CreateGallery()
    {
        _scenarios.IsVisible = false;
        DemoNavigationItem Corpus(int index, string description) => new(MarkdownCorpus.Names[index], description, "doc", open: () =>
        {
            _scenarios.SelectedIndex = index;
            return this;
        });
        return
        [
            DemoNavigationItem.Group("阅读与语法", "常规语法、复杂表格与多语言内容", "doc",
                Corpus(0, "标题、段落、代码与连续选择"), Corpus(1, "任务列表、提示块与复杂表格"), Corpus(2, "中文、字素与双向文本")),
            DemoNavigationItem.Group("图片与扩展", "图片加载与自定义文档节点", "doc",
                Corpus(3, "异步图片与失败状态"), Corpus(5, "局部服务与自定义节点渲染")),
            DemoNavigationItem.Group("流式与长文", "逐步追加与大量内容阅读", "doc",
                new DemoNavigationItem("流式追加", "进入后点击“流式追加”，观察文档逐步更新", "doc", open: () =>
                {
                    _scenarios.SelectedIndex = 0;
                    return this;
                }), Corpus(4, "10,000 块长文与可见内容回收")),
        ];
    }
}
