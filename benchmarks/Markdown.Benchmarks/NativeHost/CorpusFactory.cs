using System.Text;

namespace MarkdownNativeBenchmarks;

internal static class CorpusFactory
{
    public static string Mixed(int bytes)
    {
        var text = new StringBuilder("# 原生固定混合语料\n\n");
        for (var index = 0; index < 100; index++) text.Append($"```csharp title=block-{index:D3}\nvar value = {index}; // 中文\n```\n\n");
        const string unit = "## 标题\n\n普通 **粗体** *斜体* ~~删除~~ `code` 和 [链接](https://example.test/path)。\n\n- 项目甲\n- 项目乙\n\n> 引用文字\n\n| A | B |\n|---|:--:|\n| 中文 | data |\n\n";
        var used = Encoding.UTF8.GetByteCount(text.ToString());
        var unitBytes = Encoding.UTF8.GetByteCount(unit);
        while (used + unitBytes <= bytes) { text.Append(unit); used += unitBytes; }
        text.Append('x', bytes - used);
        return text.ToString();
    }
    public static string Code(int bytes) => "```text\n" + new string('x', bytes - 13) + "\n```\n";
    public static string Paragraph(int bytes) => string.Concat(Enumerable.Repeat("long paragraph ", bytes / 15)) + new string('x', bytes % 15);
    public static string Blocks() => string.Concat(Enumerable.Range(0, 10_000).Select(index => $"段落 {index:D5} 普通 **强调** 和逻辑选择。\n\n"));
    public static string WideTable() => "|" + string.Join("|", Enumerable.Range(0, 30).Select(index => "列" + index)) + "|\n|" + string.Join("|", Enumerable.Repeat("---", 30)) + "|\n" +
        string.Concat(Enumerable.Range(0, 30).Select(row => "|" + string.Join("|", Enumerable.Range(0, 30).Select(column => $"单元格{row}-{column} 中文内容")) + "|\n"));
}
