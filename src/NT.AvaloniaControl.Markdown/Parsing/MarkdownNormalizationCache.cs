using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using Markdig.Extensions.Alerts;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Helpers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace NT.AvaloniaControl.Markdown;

// A weak snapshot key gives a previous default parse provenance without changing the public DTO.
// Values contain only immutable normalized nodes and primitive AST shape stamps. They hold neither
// their snapshot key, source buffer, parser, AST, StringSlice, callback nor any UI resource strongly.
internal static class MarkdownNormalizationCache
{
    private static readonly ConditionalWeakTable<MarkdownDocumentSnapshot, Entry> Entries = new();

    internal sealed class Entry
    {
        private readonly WeakReference<MarkdownParser> _origin;
        internal ImmutableArray<Root> Roots { get; }
        internal Entry(MarkdownParser origin, ImmutableArray<Root> roots)
        { _origin = new(origin); Roots = roots; }
        internal bool IsFrom(MarkdownParser parser) => _origin.TryGetTarget(out var origin) && ReferenceEquals(origin, parser);
    }

    internal readonly record struct Root(MarkdownBlock Content, ImmutableArray<Shape> Shapes);
    internal readonly record struct Shape(int Kind, int Start, int End, int A = 0, int B = 0, int C = 0, long Flags = 0);

    internal static Entry? Find(MarkdownParser parser, MarkdownDocumentSnapshot? previous, string source)
    {
        if (previous is null || source.Length <= previous.Source.Length ||
            !source.AsSpan().StartsWith(previous.Source.AsSpan(), StringComparison.Ordinal) ||
            !Entries.TryGetValue(previous, out var entry) || !entry.IsFrom(parser)) return null;
        return entry;
    }

    internal static bool CanCache(MarkdownDocument ast, string source)
    {
        // Definitions can live outside the final presentation tree. This conservative syntax
        // veto also catches escaped labels and nested definitions without rejecting direct links.
        for (var index = source.IndexOf(']'); index >= 0; index = source.IndexOf(']', index + 1))
        {
            var next = index + 1;
            while (next < source.Length && char.IsWhiteSpace(source[next])) next++;
            if (next < source.Length && source[next] == ':') return false;
        }
        var last = ast.Count - 1;
        while (last >= 0 && IsOmitted(ast[last])) last--;
        for (var index = 0; index < ast.Count; index++)
            if (!CanCacheBlock(ast[index], allowPipeTableTail: index == last)) return false;
        return true;
    }

    private static bool CanCacheBlock(Block block, bool allowPipeTableTail)
    {
        if (block is FootnoteGroup { Count: 0 }) return true;
        if (block is not (HeadingBlock or ParagraphBlock or FencedCodeBlock or CodeBlock or
            AlertBlock or QuoteBlock or ListBlock or ListItemBlock or ThematicBreakBlock or Table or TableRow or TableCell)) return false;
        if (block is ContainerBlock container)
            for (var index = 0; index < container.Count; index++)
                if (!CanCacheBlock(container[index], allowPipeTableTail)) return false;
        if (block is LeafBlock { Inline: { } inlines })
            for (var inline = inlines.FirstChild; inline is not null; inline = inline.NextSibling)
                if (!CanCacheInline(inline, allowPipeTableTail)) return false;
        return true;
    }

    private static bool CanCacheInline(Inline inline, bool allowPipeTableTail)
    {
        // Markdig can leave a pipe-table delimiter in an unfinished final paragraph. It has
        // no document-wide dependency, and this entire last root is always converted afresh.
        var pipeTail = allowPipeTableTail && inline is PipeTableDelimiterInline;
        if (inline is LinkInline link && (link.Reference is not null || link.IsShortcut || link.GetDynamicUrl is not null)) return false;
        if (!pipeTail && inline is not (LiteralInline or HtmlEntityInline or CodeInline or EmphasisInline or LinkInline or AutolinkInline or LineBreakInline or TaskList)) return false;
        if (inline is ContainerInline container)
            for (var child = container.FirstChild; child is not null; child = child.NextSibling)
                if (!CanCacheInline(child, allowPipeTableTail)) return false;
        return true;
    }

    internal static bool IsOmitted(Block block) => block is FootnoteGroup { Count: 0 };

    internal static void Remember(MarkdownParser parser, MarkdownDocument ast, MarkdownDocumentSnapshot snapshot,
        Entry? previous, int reusedRoots)
    {
        var completedRoots = snapshot.Blocks.Length - 1;
        if (completedRoots <= 0) return;
        // No diagnostic is reused. Only the known, uncached final pipe-table placeholder can
        // produce one in an otherwise eligible default document; keep all other calls uncached.
        var tail = snapshot.Blocks[^1];
        var last = ast.Count - 1;
        while (last >= 0 && IsOmitted(ast[last])) last--;
        if (last < 0) return;
        foreach (var diagnostic in snapshot.Diagnostics)
            if (diagnostic.Code != "MDNODE" ||
                diagnostic.Message != "No registered converter for PipeTableDelimiterInline; source text is preserved." ||
                diagnostic.SourceStart < tail.SourceStart ||
                (long)diagnostic.SourceStart + diagnostic.SourceLength > (long)tail.SourceStart + tail.SourceLength ||
                !IsPipeDiagnostic(ast[last], diagnostic, snapshot.Source.Length)) return;
        var roots = ImmutableArray.CreateBuilder<Root>(completedRoots);
        for (var index = 0; index < ast.Count; index++)
        {
            if (IsOmitted(ast[index])) continue;
            if (roots.Count == completedRoots) break;
            var shapes = roots.Count < reusedRoots ? previous!.Roots[roots.Count].Shapes : CaptureShapes(ast[index]);
            roots.Add(new(snapshot.Blocks[roots.Count], shapes));
        }
        Entries.Add(snapshot, new(parser, roots.MoveToImmutable()));
    }

    private static bool IsPipeDiagnostic(Block block, MarkdownDiagnostic diagnostic, int sourceLength)
    {
        if (block is ContainerBlock container)
            foreach (var child in container)
                if (IsPipeDiagnostic(child, diagnostic, sourceLength)) return true;
        if (block is LeafBlock { Inline: { } inlines })
            foreach (var inline in inlines)
                if (IsPipeDiagnostic(inline, diagnostic, sourceLength)) return true;
        return false;
    }

    private static bool IsPipeDiagnostic(Inline inline, MarkdownDiagnostic diagnostic, int sourceLength)
    {
        if (inline is PipeTableDelimiterInline && SourceMatches(inline, diagnostic.SourceStart, diagnostic.SourceLength, sourceLength)) return true;
        if (inline is ContainerInline container)
            foreach (var child in container)
                if (IsPipeDiagnostic(child, diagnostic, sourceLength)) return true;
        return false;
    }

    internal static bool Matches(Block ast, Root cached, string source, int textStart) =>
        cached.Content.TextStart == textStart && MatchesShapes(ast, cached.Shapes) && BlockMatches(ast, cached.Content, source);

    internal static MarkdownBlock CloneFreshIds(MarkdownBlock node)
    {
        // Fresh identities preserve the matcher's conservative duplicate-count rules. Sharing an
        // old ID-bearing subtree directly would let its no-match branch retain invalid old IDs.
        var children = node.Children;
        if (!children.IsEmpty)
        {
            var builder = ImmutableArray.CreateBuilder<MarkdownBlock>(children.Length);
            foreach (var child in children) builder.Add(CloneFreshIds(child));
            children = builder.MoveToImmutable();
        }
        return node with { Id = MarkdownDocumentMatcher.NewId(), Children = children };
    }

    private static bool BlockMatches(Block ast, MarkdownBlock cached, string source)
    {
        if (!SourceMatches(ast, cached.SourceStart, cached.SourceLength, source.Length)) return false;
        var metadata = cached.Metadata;
        switch (ast)
        {
            case HeadingBlock heading:
                return cached.Kind == MarkdownBlockKind.Heading && metadata.Count == 1 &&
                    HasNumber(metadata, "level", heading.Level) && cached.Children.IsEmpty && InlinesMatch(heading.Inline, cached.Inlines, source);
            case ParagraphBlock paragraph:
                return cached.Kind == MarkdownBlockKind.Paragraph && metadata.Count == 0 && cached.Children.IsEmpty && InlinesMatch(paragraph.Inline, cached.Inlines, source);
            case FencedCodeBlock fence:
                return cached.Kind == MarkdownBlockKind.Code && metadata.Count == 3 && Has(metadata, "fenced", "true") &&
                    Has(metadata, "language", fence.Info ?? string.Empty) && FenceInfoMatches(fence, metadata["info"], source) &&
                    cached.Children.IsEmpty && cached.Inlines.IsEmpty && CodeTextMatches(fence, cached.Text);
            case CodeBlock code:
                return cached.Kind == MarkdownBlockKind.Code && metadata.Count == 3 && Has(metadata, "fenced", "false") &&
                    Has(metadata, "language", "") && Has(metadata, "info", "") && cached.Children.IsEmpty && cached.Inlines.IsEmpty && CodeTextMatches(code, cached.Text);
            case AlertBlock alert:
                return cached.Kind == MarkdownBlockKind.Alert && metadata.Count == 1 &&
                    Has(metadata, "kind", alert.Kind.ToString().ToUpperInvariant()) && cached.Inlines.IsEmpty && ChildrenMatch(alert, cached.Children, source);
            case QuoteBlock quote:
                return cached.Kind == MarkdownBlockKind.Quote && metadata.Count == 0 && cached.Inlines.IsEmpty && ChildrenMatch(quote, cached.Children, source);
            case ListBlock list:
                if (cached.Kind != MarkdownBlockKind.List || metadata.Count != 3 || !Has(metadata, "ordered", Boolean(list.IsOrdered)) ||
                    !Has(metadata, "loose", Boolean(list.IsLoose)) || !Has(metadata, "start", list.OrderedStart ?? "1") ||
                    !cached.Inlines.IsEmpty || list.Count != cached.Children.Length) return false;
                var number = int.TryParse(list.OrderedStart, CultureInfo.InvariantCulture, out var parsed) ? parsed : 1;
                for (var index = 0; index < list.Count; index++)
                {
                    var marker = list.IsOrdered ? $"{number++}. " : "- ";
                    var item = cached.Children[index];
                    if (list[index] is not ListItemBlock astItem || item.Kind != MarkdownBlockKind.ListItem || item.Metadata.Count != 1 ||
                        !Has(item.Metadata, "marker", marker) || !SourceMatches(astItem, item.SourceStart, item.SourceLength, source.Length) ||
                        !item.Inlines.IsEmpty || !ChildrenMatch(astItem, item.Children, source)) return false;
                }
                return true;
            case ListItemBlock item:
                return cached.Kind == MarkdownBlockKind.ListItem && metadata.Count == 0 && cached.Inlines.IsEmpty && ChildrenMatch(item, cached.Children, source);
            case ThematicBreakBlock:
                return cached.Kind == MarkdownBlockKind.ThematicBreak && metadata.Count == 0 && cached.Text.Length == 0 && cached.Children.IsEmpty && cached.Inlines.IsEmpty;
            case Table table:
                var columns = TableColumns(table);
                return cached.Kind == MarkdownBlockKind.Table && metadata.Count == 2 && HasNumber(metadata, "columns", columns) &&
                    AlignmentsMatch(table, columns, metadata["alignments"]) && cached.Inlines.IsEmpty && ChildrenMatch(table, cached.Children, source);
            case TableRow row:
                if (cached.Kind != MarkdownBlockKind.TableRow || metadata.Count != 1 || !Has(metadata, "header", Boolean(row.IsHeader)) ||
                    !cached.Inlines.IsEmpty || row.Count != cached.Children.Length) return false;
                var column = 0;
                for (var index = 0; index < row.Count; index++)
                {
                    if (row[index] is not TableCell cell) return false;
                    var target = Math.Max(column, cell.ColumnIndex);
                    var definitions = (row.Parent as Table)?.ColumnDefinitions;
                    var alignment = definitions is not null && target < definitions.Count ? Alignment(definitions[target].Alignment) : null;
                    if (!CellMatches(cell, cached.Children[index], source, target, alignment)) return false;
                    column = target + Math.Max(1, cell.ColumnSpan);
                }
                return true;
            case TableCell cell:
                return CellMatches(cell, cached, source, cell.ColumnIndex, null);
            default: return false;
        }
    }

    private static bool CellMatches(TableCell ast, MarkdownBlock cached, string source, int column, string? alignment) =>
        cached.Kind == MarkdownBlockKind.TableCell && SourceMatches(ast, cached.SourceStart, cached.SourceLength, source.Length) &&
        cached.Metadata.Count == (alignment is null ? 3 : 4) && HasNumber(cached.Metadata, "column", column) &&
        HasNumber(cached.Metadata, "column-span", Math.Max(1, ast.ColumnSpan)) && HasNumber(cached.Metadata, "row-span", Math.Max(1, ast.RowSpan)) &&
        (alignment is null || Has(cached.Metadata, "alignment", alignment)) && cached.Inlines.IsEmpty && ChildrenMatch(ast, cached.Children, source);

    private static bool ChildrenMatch(ContainerBlock ast, ImmutableArray<MarkdownBlock> cached, string source)
    {
        var index = 0;
        for (var child = 0; child < ast.Count; child++)
        {
            if (IsOmitted(ast[child])) continue;
            if (index >= cached.Length || !BlockMatches(ast[child], cached[index++], source)) return false;
        }
        return index == cached.Length;
    }

    private static bool InlinesMatch(ContainerInline? ast, ImmutableArray<MarkdownInline> cached, string source)
    {
        var index = 0;
        for (var inline = ast?.FirstChild; inline is not null; inline = inline.NextSibling)
            if (index >= cached.Length || !InlineMatches(inline, cached[index++], source)) return false;
        return index == cached.Length;
    }

    private static bool InlineMatches(Inline ast, MarkdownInline cached, string source)
    {
        if (!SourceMatches(ast, cached.SourceStart, cached.SourceLength, source.Length)) return false;
        var metadata = cached.Metadata;
        switch (ast)
        {
            case LiteralInline literal:
                return cached.Kind == MarkdownInlineKind.Text && metadata.Count == 0 && cached.Children.IsEmpty && SliceMatches(literal.Content, cached.Text);
            case HtmlEntityInline entity:
                return cached.Kind == MarkdownInlineKind.Text && metadata.Count == 0 && cached.Children.IsEmpty && SliceMatches(entity.Transcoded, cached.Text);
            case CodeInline code:
                return cached.Kind == MarkdownInlineKind.Code && metadata.Count == 0 && cached.Children.IsEmpty && code.Content == cached.Text;
            case EmphasisInline emphasis:
                var kind = emphasis.DelimiterChar switch
                {
                    '~' => emphasis.DelimiterCount == 2 ? MarkdownInlineKind.Strike : MarkdownInlineKind.Subscript,
                    '^' => MarkdownInlineKind.Superscript, '+' => MarkdownInlineKind.Insert, '=' => MarkdownInlineKind.Mark,
                    _ => emphasis.DelimiterCount == 2 ? MarkdownInlineKind.Strong : MarkdownInlineKind.Emphasis
                };
                return cached.Kind == kind && metadata.Count == 0 && InlinesMatch(emphasis, cached.Children, source);
            case LinkInline link:
                return cached.Kind == (link.IsImage ? MarkdownInlineKind.Image : MarkdownInlineKind.Link) &&
                    metadata.Count == 2 && Has(metadata, "url", link.Url ?? "") && Has(metadata, "title", link.Title ?? "") && InlinesMatch(link, cached.Children, source);
            case AutolinkInline link:
                return cached.Kind == MarkdownInlineKind.Link && metadata.Count == 1 &&
                    Has(metadata, "url", (link.IsEmail ? "mailto:" : "") + link.Url) && cached.Children.IsEmpty && cached.Text == link.Url;
            case LineBreakInline line:
                return cached.Kind == MarkdownInlineKind.LineBreak && metadata.Count == 1 && Has(metadata, "hard", Boolean(line.IsHard)) &&
                    cached.Children.IsEmpty && cached.Text == (line.IsHard ? "\n" : " ");
            case TaskList task:
                return cached.Kind == MarkdownInlineKind.Task && metadata.Count == 1 && Has(metadata, "checked", Boolean(task.Checked)) &&
                    cached.Children.IsEmpty && cached.Text == (task.Checked ? "[x]" : "[ ]");
            default: return false;
        }
    }

    private static bool SourceMatches(MarkdownObject node, int start, int length, int sourceLength)
    {
        var currentStart = Math.Clamp(node.Span.Start, 0, sourceLength);
        var currentLength = (int)Math.Clamp((long)node.Span.End + 1 - currentStart, 0, sourceLength - currentStart);
        return currentStart == start && currentLength == length;
    }
    private static bool SliceMatches(StringSlice slice, string text) => slice.Text is { } buffer && slice.Length > 0 ?
        buffer.AsSpan(slice.Start, slice.Length).SequenceEqual(text.AsSpan()) : text.Length == 0;
    private static bool CodeTextMatches(CodeBlock ast, string text)
    {
        var position = 0;
        for (var index = 0; index < ast.Lines.Count; index++)
        {
            var line = ast.Lines.Lines[index];
            var length = line.Slice.Text is not null ? Math.Max(0, line.Slice.Length) : 0;
            if (length > text.Length - position || (length > 0 && !line.Slice.Text!.AsSpan(line.Slice.Start, length).SequenceEqual(text.AsSpan(position, length)))) return false;
            position += length;
            if (line.NewLine != NewLine.None)
            {
                if (position >= text.Length || text[position++] != '\n') return false;
            }
        }
        return position == text.Length;
    }

    private static bool FenceInfoMatches(FencedCodeBlock ast, string info, string source)
    {
        for (var parent = ast.Parent; parent is not null; parent = parent.Parent)
            if (parent is TableCell) return info == (string.IsNullOrEmpty(ast.Arguments) ? ast.Info ?? "" : $"{ast.Info} {ast.Arguments}".Trim());
        if (ast.Span.Start < 0 || ast.Span.Start > source.Length) return info == (ast.Info ?? "");
        var opening = source.AsSpan(Math.Clamp(ast.Span.Start, 0, source.Length));
        var newline = opening.IndexOfAny('\r', '\n');
        if (newline >= 0) opening = opening[..newline];
        var fence = opening.IndexOf(ast.FencedChar);
        if (fence < 0) return info == (ast.Info ?? "");
        while (fence < opening.Length && opening[fence] == ast.FencedChar) fence++;
        return opening[fence..].Trim().SequenceEqual(info.AsSpan());
    }
    private static int TableColumns(Table table)
    {
        var count = 0;
        foreach (TableRow row in table)
        {
            var next = 0;
            foreach (TableCell cell in row) next = Math.Max(next, cell.ColumnIndex) + Math.Max(1, cell.ColumnSpan);
            count = Math.Max(count, next);
        }
        return count;
    }
    private static bool AlignmentsMatch(Table table, int columns, string value)
    {
        var rest = value.AsSpan();
        var count = Math.Min(columns, table.ColumnDefinitions.Count);
        for (var index = 0; index < count; index++)
        {
            if (index > 0) { if (rest.Length == 0 || rest[0] != ',') return false; rest = rest[1..]; }
            var alignment = Alignment(table.ColumnDefinitions[index].Alignment);
            if (!rest.StartsWith(alignment.AsSpan(), StringComparison.Ordinal)) return false;
            rest = rest[alignment.Length..];
        }
        return rest.Length == 0;
    }
    private static string Alignment(TableColumnAlign? alignment) => alignment switch
    { null or TableColumnAlign.Left => "left", TableColumnAlign.Center => "center", TableColumnAlign.Right => "right", _ => alignment.ToString()!.ToLowerInvariant() };
    private static string Boolean(bool value) => value ? "true" : "false";
    private static bool Has(ImmutableDictionary<string, string> metadata, string key, string value) => metadata.TryGetValue(key, out var old) && old == value;
    private static bool HasNumber(ImmutableDictionary<string, string> metadata, string key, int value) =>
        metadata.TryGetValue(key, out var text) && text == value.ToString(CultureInfo.InvariantCulture);

    private static ImmutableArray<Shape> CaptureShapes(Block block)
    {
        var counter = new ShapeWalker(ImmutableArray<Shape>.Empty, capture: false, countOnly: true);
        counter.Block(block);
        var walker = new ShapeWalker(ImmutableArray<Shape>.Empty, capture: true, capacity: counter.Count);
        walker.Block(block);
        return walker.Finish();
    }
    private static bool MatchesShapes(Block block, ImmutableArray<Shape> shapes)
    {
        var walker = new ShapeWalker(shapes, capture: false);
        return walker.Block(block) && walker.Complete;
    }

    private struct ShapeWalker
    {
        private readonly ImmutableArray<Shape> _expected;
        private readonly ImmutableArray<Shape>.Builder? _captured;
        private readonly bool _countOnly;
        private int _position;
        internal ShapeWalker(ImmutableArray<Shape> expected, bool capture, int capacity = 0, bool countOnly = false)
        { _expected = expected; _captured = capture ? ImmutableArray.CreateBuilder<Shape>(capacity) : null; _countOnly = countOnly; _position = 0; }
        internal readonly bool Complete => _position == _expected.Length;
        internal readonly int Count => _position;
        internal readonly ImmutableArray<Shape> Finish() => _captured!.MoveToImmutable();
        private bool Add(Shape shape)
        {
            if (_countOnly) { _position++; return true; }
            if (_captured is not null) { _captured.Add(shape); return true; }
            return _position < _expected.Length && _expected[_position++] == shape;
        }
        internal bool Block(Block block)
        {
            var kind = block switch { HeadingBlock => 1, ParagraphBlock => 2, FencedCodeBlock => 3, CodeBlock => 4,
                AlertBlock => 5, QuoteBlock => 6, ListBlock => 7, ListItemBlock => 8, ThematicBreakBlock => 9,
                Table => 10, TableRow => 11, TableCell => 12, FootnoteGroup { Count: 0 } => 13, _ => 0 };
            if (!Add(new(kind, block.Span.Start, block.Span.End, block.Line, block.Column))) return false;
            switch (block)
            {
                case HeadingBlock heading:
                    if (!Add(new(20, 0, 0, heading.Level))) return false;
                    break;
                case FencedCodeBlock fence:
                    if (!Add(new(21, 0, 0, fence.FencedChar, fence.OpeningFencedCharCount, fence.ClosingFencedCharCount, fence.IndentCount)) ||
                        !Add(new(22, 0, 0, (int)fence.InfoNewLine, Flags: (fence.Info is null ? 1 : 0) | (fence.Arguments is null ? 2 : 0)))) return false;
                    break;
                case AlertBlock alert:
                    if (!Add(new(23, alert.Kind.Start, alert.Kind.End, Flags: alert.Kind.Text is null ? 1 : 0))) return false;
                    break;
                case ListBlock list:
                    if (!Add(new(24, 0, 0, list.Count, Flags: (list.IsOrdered ? 1 : 0) | (list.IsLoose ? 2 : 0) | (list.OrderedStart is null ? 4 : 0)))) return false;
                    break;
                case Table table:
                    if (!Add(new(25, 0, 0, table.ColumnDefinitions.Count))) return false;
                    foreach (var column in table.ColumnDefinitions)
                        if (!Add(new(26, 0, 0, column.Alignment is { } alignment ? (int)alignment : -1, Flags: BitConverter.DoubleToInt64Bits(column.Width)))) return false;
                    break;
                case TableRow row:
                    if (!Add(new(27, 0, 0, row.Count, Flags: row.IsHeader ? 1 : 0))) return false;
                    break;
                case TableCell cell:
                    if (!Add(new(28, 0, 0, cell.ColumnIndex, cell.ColumnSpan, cell.RowSpan))) return false;
                    break;
            }
            if (block is CodeBlock code)
            {
                if (!Add(new(29, 0, 0, code.Lines.Count))) return false;
                for (var index = 0; index < code.Lines.Count; index++)
                {
                    var line = code.Lines.Lines[index];
                    if (!Add(new(30, line.Slice.Start, line.Slice.End, line.Position, line.Column, line.Line, (int)line.NewLine))) return false;
                }
            }
            if (block is ContainerBlock container)
            {
                if (!Add(new(31, 0, 0, container.Count))) return false;
                for (var index = 0; index < container.Count; index++) if (!Block(container[index])) return false;
            }
            if (block is LeafBlock leaf && !Inlines(leaf.Inline)) return false;
            return true;
        }
        private bool Inlines(ContainerInline? container)
        {
            var count = 0;
            for (var inline = container?.FirstChild; inline is not null; inline = inline.NextSibling) { if (!Inline(inline)) return false; count++; }
            return Add(new(32, 0, 0, count));
        }
        private bool Inline(Inline inline)
        {
            var kind = inline switch { LiteralInline => 40, HtmlEntityInline => 41, CodeInline => 42, EmphasisInline => 43,
                LinkInline => 44, AutolinkInline => 45, LineBreakInline => 46, TaskList => 47, _ => 0 };
            if (!Add(new(kind, inline.Span.Start, inline.Span.End))) return false;
            switch (inline)
            {
                case LiteralInline literal:
                    if (!Add(new(50, literal.Content.Start, literal.Content.End))) return false;
                    break;
                case HtmlEntityInline entity:
                    if (!Add(new(51, entity.Transcoded.Start, entity.Transcoded.End))) return false;
                    break;
                case EmphasisInline emphasis:
                    if (!Add(new(52, 0, 0, emphasis.DelimiterChar, emphasis.DelimiterCount))) return false;
                    break;
                case LinkInline link:
                    if (!Add(new(53, 0, 0, Flags: (link.IsImage ? 1 : 0) | (link.IsAutoLink ? 2 : 0) | (link.IsShortcut ? 4 : 0) |
                        (link.Url is null ? 8 : 0) | (link.Title is null ? 16 : 0)))) return false;
                    break;
                case AutolinkInline link:
                    if (!Add(new(54, 0, 0, Flags: link.IsEmail ? 1 : 0))) return false;
                    break;
                case LineBreakInline line:
                    if (!Add(new(55, 0, 0, Flags: line.IsHard ? 1 : 0))) return false;
                    break;
                case TaskList task:
                    if (!Add(new(56, 0, 0, Flags: task.Checked ? 1 : 0))) return false;
                    break;
            }
            return inline is not ContainerInline container || Inlines(container);
        }
    }
}
