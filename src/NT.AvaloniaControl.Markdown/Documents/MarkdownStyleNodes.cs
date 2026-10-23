using Avalonia.Controls;
using Avalonia.Controls.Documents;

namespace NT.AvaloniaControl.Markdown;

/// <summary>A real Avalonia logical style node for a semantic inline range.</summary>
public class MarkdownSpan : Span
{
    public int TextStart { get; internal set; }
    public int TextLength { get; internal set; }
    public string? Url { get; internal set; }
}
public class MarkdownRun : Run
{
    public MarkdownRun() { }
    public MarkdownRun(string text) : base(text) { }
}

/// <summary>A realized block style node. Logical document state survives its recycling.</summary>
public class MarkdownBlockPresenter : Border
{
    public long NodeId { get; internal set; }
}
