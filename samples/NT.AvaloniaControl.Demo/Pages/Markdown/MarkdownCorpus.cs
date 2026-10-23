namespace NT.AvaloniaControl.Demo.Pages.Markdown;

internal static class MarkdownCorpus
{
    internal static readonly string[] Names = ["基础阅读", "扩展与复杂表格", "中文 / Unicode / RTL", "图片与自定义加载器", "长文（10,000 块）", "单元素服务与自定义节点"];
    internal const string Basic = "# 原生 Markdown 阅读\n\n一个文档，连续选区。拖选可以跨段落、代码和表格；Ctrl+A/C 支持全文选择复制。\n\n## 常规语法\n\n**粗体**、*斜体*、~~删除~~、`行内代码`、[Avalonia](https://avaloniaui.net \"打开官网\")。\n软换行与两空格硬换行：  \n这里开始新行。\n\n3. 保留起始编号\n4. 嵌套列表\n   - 第一项\n   - 第二项\n\n> 引用块中的 **内联样式**。\n\n```csharp title=\"示例\"\nvar reader = new Markdown();\nreader.Text = \"# Hello\";\nreader.SelectAll();\n```\n\n| 能力 | 说明 |\n| :--- | ---: |\n| 逻辑选区 | 独立于视觉回收 |\n| 流式会话 | 合并输入与最终等待 |\n\n---\n\n阅读位置与选区在主题、字体及视口变化后保留。";
    internal const string Extensions = "# 扩展语法\n\n- [x] 已完成任务（只读）\n- [ ] 待完成任务\n\n> [!NOTE]\n> 普通说明。\n\n> [!TIP]\n> 提示与 **强调**。\n\n> [!IMPORTANT]\n> 重要事项。\n\n> [!WARNING]\n> 注意条件。\n\n> [!CAUTION]\n> 谨慎处理。\n\n插入 ++insert++、标记 ==mark==、下标 H~2~O、上标 x^2^、Emoji :smile:。\n\n重复脚注[^a]、再次引用[^a]。\n\n## Grid 表格与合并单元格\n\n+---------+---------+\n| A       | B       |\n+=========+=========+\n| one     | two     |\n|         |         |\n| para    | **bold**|\n+---------+---------+\n| merged            |\n+-------------------+\n\n## 宽表横向滚动\n\n| 一 | 二 | 三 | 四 | 五 | 六 | 七 | 八 |\n| --- | --- | --- | --- | --- | --- | --- | --- |\n| alpha | beta | gamma | delta | epsilon | zeta | eta | theta |\n\n[^a]: 脚注正文及 [链接](https://example.com)。\n\n## HTML 保留原文\n\n<div>HTML 以原文显示，并生成诊断。</div>";
    internal const string Unicode = "# 中文 / Unicode / RTL\n\n中文字体回退由 Avalonia 原生文本系统处理。\n\n代理对：😀；组合字符：e\u0301；Emoji ZWJ：👨‍👩‍👧‍👦。选择与方向键按字素边界处理。\n\nעברית العربية mixed 中文 English。\n\n```text\n\t中文  😀  é  👩‍💻\n保留\t代码空白与最终换行。\n```\n\n| 语言 | 内容 |\n| --- | --- |\n| 中文 | 连续选择 |\n| العربية | نص عربي |";
    internal const string Images = "# 图片与自定义 loader\n\n此页面显式注入 loader。demo:// 图片由宿主生成，并延迟返回以观察阅读锚点。\n\n![自定义加载器的预览图片](demo://preview)\n\n图像完成后，阅读位置应保持；已在底部且开启跟随时继续跟随。\n\n![故意失败的图片](demo://failure)\n\n失败保留 alt 文本，并通过 Error 事件报告。\n\n## 后续段落\n\n向下滚动后观察图片晚到时的阅读位置。";
    internal const string Stream = "\n\n## 受控流式结果\n\n输入每秒 20 个 chunk，未闭合结构允许中间状态变化。\n\n```csharp\nawait session.CompleteAsync();\n```\n\n| 追加 | 完成 |\n| --- | --- |\n| 合并输入 | 等待最终快照 |\n\n迟到引用 [目标][late]。\n\n[late]: https://example.com \"尾部定义\"";
    internal const string Custom = "# 单元素服务与自定义节点\n\nCARD: 这是由宿主注册的原生卡片。**文字样式**、全局选区和复制投影继续使用库的原生文本助手。\n\n![局部加载器](demo://local)\n\n![局部禁用图片](demo://failure)\n\n第二张图显式关闭服务，仅显示alt文字。\n\n```plain\n这块代码局部关闭高亮。\n```\n\n```csharp\nvar inherited = \"保留根高亮器\";\n```\n\n根服务开关仍作用于继承节点，局部规则在回收重新实现时再次应用。";
    internal static string Get(int index) => index switch
    {
        1 => Extensions, 2 => Unicode, 3 => Images,
        4 => "# 长文回收\n\n" + string.Join("\n\n", Enumerable.Range(1, 10000).Select(number => $"段落 {number}。原生文字排版、逻辑选择与视口呈现。")),
        5 => Custom,
        _ => Basic
    };
}
