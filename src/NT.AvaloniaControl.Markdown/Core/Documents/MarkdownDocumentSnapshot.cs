using System.Collections.Immutable;

namespace NT.AvaloniaControl.Markdown;

/// <summary>Immutable data produced by the parser. Contains no UI resources or parser AST objects.</summary>
public sealed record MarkdownDocumentSnapshot(
    string Source,
    string Text,
    ImmutableArray<MarkdownBlock> Blocks,
    ImmutableArray<MarkdownDiagnostic> Diagnostics)
{
    public static MarkdownDocumentSnapshot Empty { get; } = new(string.Empty, string.Empty, [], []);

    public IEnumerable<MarkdownBlock> Descendants()
    {
        // One iterator and a stack of parent positions preserve preorder without allocating a
        // recursive iterator for every block (or using the call stack for deeply nested data).
        var parents = new Stack<(ImmutableArray<MarkdownBlock> Blocks, int Index)>();
        var siblings = Blocks;
        var index = 0;
        while (true)
        {
            if (index == siblings.Length)
            {
                if (parents.Count == 0) yield break;
                (siblings, index) = parents.Pop();
                continue;
            }
            var block = siblings[index++];
            yield return block;
            if (block.Children.IsEmpty) continue;
            parents.Push((siblings, index));
            siblings = block.Children;
            index = 0;
        }
    }

    public MarkdownBlock? FindBlock(long id) => Descendants().FirstOrDefault(block => block.Id == id);

}

public enum MarkdownBlockKind
{
    Paragraph, Heading, Quote, Alert, List, ListItem, Code, ThematicBreak,
    Table, TableRow, TableCell, FootnoteGroup, Footnote, Html, Unsupported, Custom
}

public enum MarkdownInlineKind
{
    Text, Emphasis, Strong, Strike, Insert, Mark, Subscript, Superscript,
    Code, Link, Image, LineBreak, FootnoteReference, Task, Html, Unsupported, Custom
}

/// <summary>All offsets use UTF-16. Text spans address the document's logical copy projection.</summary>
public sealed record MarkdownBlock(
    long Id,
    MarkdownBlockKind Kind,
    int SourceStart,
    int SourceLength,
    int TextStart,
    int TextLength,
    string Text,
    ImmutableArray<MarkdownBlock> Children,
    ImmutableArray<MarkdownInline> Inlines,
    ImmutableDictionary<string, string> Metadata)
{
    /// <summary>Built-in semantics retained by a custom converter whose Text was null.</summary>
    public MarkdownBlockKind SemanticKind => Kind == MarkdownBlockKind.Custom &&
        Metadata is not null && Metadata.TryGetValue(MarkdownCustomNodeMetadata.NativeKind, out var name) &&
        Enum.TryParse<MarkdownBlockKind>(name, out var native) && Enum.IsDefined(native) &&
        name == native.ToString() && native != MarkdownBlockKind.Custom ? native : Kind;
}

public sealed record MarkdownInline(
    MarkdownInlineKind Kind,
    int SourceStart,
    int SourceLength,
    int TextStart,
    int TextLength,
    string Text,
    ImmutableArray<MarkdownInline> Children,
    ImmutableDictionary<string, string> Metadata)
{
    /// <summary>Built-in semantics retained by a custom converter whose Text was null.</summary>
    public MarkdownInlineKind SemanticKind => Kind == MarkdownInlineKind.Custom &&
        Metadata is not null && Metadata.TryGetValue(MarkdownCustomNodeMetadata.NativeKind, out var name) &&
        Enum.TryParse<MarkdownInlineKind>(name, out var native) && Enum.IsDefined(native) &&
        name == native.ToString() && native != MarkdownInlineKind.Custom ? native : Kind;
}

public sealed record MarkdownDiagnostic(string Code, string Message, int SourceStart, int SourceLength);
