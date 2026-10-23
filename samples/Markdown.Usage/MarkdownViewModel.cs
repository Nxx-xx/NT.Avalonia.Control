using CommunityToolkit.Mvvm.ComponentModel;

namespace Markdown.Usage;

public sealed partial class MarkdownViewModel : ObservableObject
{
    public const string InitialText = "# Markdown Usage\n\n真实 **AXAML** 与 MVVM 绑定。选择跨段落、代码和表格的文本。\n\n```cs\nvar reader = new Markdown();\nreader.SelectAll();\n```\n\n| 能力 | 状态 |\n| --- | --- |\n| 原生阅读 | 示例 |\n| 流式追加 | 可等待 |";
    [ObservableProperty] private string _text = InitialText;
    [ObservableProperty] private string _status = "图片加载器由宿主显式注入；此初始文档无联网图片。";
}
