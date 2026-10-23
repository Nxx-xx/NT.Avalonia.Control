using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Markdig;
using Markdig.Extensions.Alerts;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Helpers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace NT.AvaloniaControl.Markdown;

/// <summary>Converts Markdig's short-lived AST into immutable data and an explicit copy projection.</summary>
public sealed class MarkdownParser
{
    private readonly MarkdownPipeline _pipeline;
    private readonly ImmutableArray<MarkdownNodeConverterRegistration> _converters;

    public MarkdownParser(Action<MarkdownPipelineBuilder>? configurePipeline = null,
        IEnumerable<MarkdownNodeConverterRegistration>? converters = null)
    {
        _converters = converters is null or ImmutableArray<MarkdownNodeConverterRegistration> { IsDefault: true } ? [] : converters.ToImmutableArray();
        if (_converters.Any(registration => registration is null))
            throw new ArgumentException("Converter registrations cannot contain null.", nameof(converters));
        Options = new() { ConfigurePipeline = configurePipeline, Converters = _converters };
        var builder = new MarkdownPipelineBuilder()
            .UsePreciseSourceLocation()
            .UsePipeTables()
            .UseGridTables()
            .UseTaskLists()
            .UseAlertBlocks()
            .UseFootnotes()
            .UseAutoLinks()
            .UseEmphasisExtras(EmphasisExtraOptions.Default)
            .UseEmojiAndSmiley();
        configurePipeline?.Invoke(builder);
        _pipeline = builder.Build();
    }

    public MarkdownParserOptions Options { get; }

    public static MarkdownParser FromOptions(MarkdownParserOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new(options.ConfigurePipeline, options.Converters.IsDefault ? [] : options.Converters);
    }

    public MarkdownDocumentSnapshot Parse(string? source, MarkdownDocumentSnapshot? previous = null)
    {
        source ??= string.Empty;
        var ast = Markdig.Markdown.Parse(source, _pipeline);
        var cacheable = Options.ConfigurePipeline is null && _converters.IsEmpty && MarkdownNormalizationCache.CanCache(ast, source);
        var cached = cacheable ? MarkdownNormalizationCache.Find(this, previous, source) : null;
        var converter = new Converter(source, _converters, cached);
        var snapshot = converter.Convert(ast);
        var result = previous is null ? snapshot : MarkdownDocumentMatcher.Reconcile(previous, snapshot);
        if (cacheable) MarkdownNormalizationCache.Remember(this, ast, result, cached, converter.ReusedRoots);
        return result;
    }

    private sealed class Converter(string source, ImmutableArray<MarkdownNodeConverterRegistration> registrations, MarkdownNormalizationCache.Entry? cached)
    {
        private readonly StringBuilder _text = new();
        private readonly ImmutableArray<MarkdownDiagnostic>.Builder _diagnostics = ImmutableArray.CreateBuilder<MarkdownDiagnostic>();
        private readonly Dictionary<Table, int> _tableColumnCounts = new();
        private bool _reusePrefix = cached is not null;
        internal int ReusedRoots { get; private set; }

        public MarkdownDocumentSnapshot Convert(MarkdownDocument document)
        {
            var blocks = ConvertChildren(document);
            var text = blocks.Length == 1 && blocks[0].TextStart == 0 && blocks[0].TextLength == _text.Length
                ? blocks[0].Text : _text.ToString();
            return new(source, text, blocks, _diagnostics.ToImmutable());
        }

        private ImmutableArray<MarkdownBlock> ConvertChildren(ContainerBlock container)
        {
            if (container.Count == 0) return [];
            var result = ImmutableArray.CreateBuilder<MarkdownBlock>(container.Count);
            foreach (var block in container)
            {
                // Reference definitions affect links, but have no visible/copy text of their own.
                if (block is LinkReferenceDefinitionGroup or LinkReferenceDefinition) continue;
                // Markdig inserts a paragraph of backlinks when a footnote ends in a non-paragraph
                // block. Those navigation-only nodes must not add an empty copy block or separator.
                if (block is ParagraphBlock { Inline: { FirstChild: not null } footnoteInlines } &&
                    footnoteInlines.All(inline => inline is FootnoteLink { IsBackLink: true })) continue;
                if (block is FootnoteGroup { Count: 0 })
                {
                    // Unreferenced definitions may leave an empty generated group. The default
                    // group has no presentation, while an explicit host converter can still own it.
                    var custom = ConvertCustom(block, out var failed);
                    if (custom is null && !failed) continue;
                    if (result.Count > 0) _text.Append('\n');
                    result.Add(ConvertBlock(block, custom, failed));
                    continue;
                }
                if (result.Count > 0) _text.Append('\n');
                if (container is MarkdownDocument && _reusePrefix && result.Count < cached!.Roots.Length &&
                    MarkdownNormalizationCache.Matches(block, cached.Roots[result.Count], source, _text.Length))
                {
                    var content = cached.Roots[result.Count].Content;
                    _text.Append(content.Text);
                    result.Add(MarkdownNormalizationCache.CloneFreshIds(content));
                    ReusedRoots++;
                }
                else
                {
                    if (container is MarkdownDocument) _reusePrefix = false;
                    result.Add(ConvertBlock(block));
                }
            }
            return Freeze(result);
        }

        private MarkdownBlock ConvertBlock(Block block)
        {
            var custom = ConvertCustom(block, out var failed);
            return ConvertBlock(block, custom, failed);
        }

        private MarkdownBlock ConvertBlock(Block block, MarkdownCustomNode? custom, bool failed)
        {
            // A grid cell's original source is split across columns. On converter failure,
            // its fence must use owned code lines rather than a truncated/contaminated slice.
            if (failed && block is FencedCodeBlock cellFence && IsInsideTableCell(cellFence)) failed = false;
            var start = _text.Length;
            var kind = MarkdownBlockKind.Unsupported;
            var metadata = new NodeMetadata();
            ImmutableArray<MarkdownBlock> children = [];
            ImmutableArray<MarkdownInline> inlines = [];
            if (failed)
            {
                _text.Append(SourceText(block));
                metadata[MarkdownCustomNodeMetadata.AstType] = block.GetType().FullName ?? block.GetType().Name;
            }
            else if (custom?.Text is { } customText)
            {
                kind = MarkdownBlockKind.Custom;
                metadata.AddRange(custom.Metadata);
                _text.Append(NormalizeNewlines(customText));
            }
            else
            {
                // A custom renderer with no replacement text keeps built-in copy/structure rules.
                // Native attributes win over host payload keys, so row spans/markers stay coherent.
                if (custom is not null) metadata.AddRange(custom.Metadata);
                switch (block)
                {
                case HeadingBlock heading:
                    kind = MarkdownBlockKind.Heading;
                    metadata["level"] = Number(heading.Level);
                    inlines = ConvertInlines(heading.Inline);
                    break;
                case ParagraphBlock paragraph:
                    kind = MarkdownBlockKind.Paragraph;
                    inlines = ConvertInlines(paragraph.Inline);
                    break;
                case FencedCodeBlock fenced:
                    kind = MarkdownBlockKind.Code;
                    metadata["language"] = fenced.Info ?? string.Empty;
                    metadata["info"] = FenceInfo(fenced);
                    metadata["fenced"] = "true";
                    AppendCodeLines(fenced);
                    break;
                case CodeBlock code:
                    kind = MarkdownBlockKind.Code;
                    metadata["language"] = string.Empty;
                    metadata["info"] = string.Empty;
                    metadata["fenced"] = "false";
                    AppendCodeLines(code);
                    break;
                case AlertBlock alert:
                    kind = MarkdownBlockKind.Alert;
                    metadata["kind"] = alert.Kind.ToString().ToUpperInvariant();
                    _text.Append(metadata["kind"]).Append('\n');
                    children = ConvertChildren(alert);
                    break;
                case QuoteBlock quote:
                    kind = MarkdownBlockKind.Quote;
                    children = ConvertChildren(quote);
                    break;
                case ListBlock list:
                    kind = MarkdownBlockKind.List;
                    metadata["ordered"] = list.IsOrdered ? "true" : "false";
                    metadata["start"] = list.OrderedStart ?? "1";
                    metadata["loose"] = list.IsLoose ? "true" : "false";
                    var items = ImmutableArray.CreateBuilder<MarkdownBlock>(list.Count);
                    var number = int.TryParse(list.OrderedStart, CultureInfo.InvariantCulture, out var parsed) ? parsed : 1;
                    foreach (var item in list)
                    {
                        if (items.Count > 0) _text.Append('\n');
                        var itemStart = _text.Length;
                        var marker = list.IsOrdered ? $"{number++}. " : "- ";
                        _text.Append(marker);
                        var converted = ConvertBlock(item);
                        items.Add(converted with
                        {
                            TextStart = itemStart,
                            TextLength = _text.Length - itemStart,
                            Text = _text.ToString(itemStart, _text.Length - itemStart),
                            Metadata = converted.Metadata.SetItem("marker", marker)
                        });
                    }
                    children = Freeze(items);
                    break;
                case ListItemBlock item:
                    kind = MarkdownBlockKind.ListItem;
                    children = ConvertChildren(item);
                    break;
                case ThematicBreakBlock:
                    kind = MarkdownBlockKind.ThematicBreak;
                    break;
                case Table table:
                    kind = MarkdownBlockKind.Table;
                    var columnCount = TableColumnCount(table);
                    metadata["columns"] = Number(columnCount);
                    metadata["alignments"] = string.Join(",", table.ColumnDefinitions.Take(columnCount).Select(column => column.Alignment?.ToString().ToLowerInvariant() ?? "left"));
                    children = ConvertChildren(table);
                    break;
                case TableRow row:
                    kind = MarkdownBlockKind.TableRow;
                    metadata["header"] = row.IsHeader ? "true" : "false";
                    var cells = ImmutableArray.CreateBuilder<MarkdownBlock>(row.Count);
                    var columnIndex = 0;
                    foreach (TableCell cell in row)
                    {
                        var targetColumn = Math.Max(columnIndex, cell.ColumnIndex);
                        while (columnIndex < targetColumn) { _text.Append('\t'); columnIndex++; }
                        if (cells.Count > 0 && columnIndex == targetColumn) _text.Append('\t');
                        var converted = ConvertBlock(cell);
                        var definitions = (row.Parent as Table)?.ColumnDefinitions;
                        // Cell content can be replaced, but spans belong to the parent table's
                        // physical structure just like list markers belong to their parent list.
                        var cellMetadata = converted.Metadata.SetItem("column", Number(targetColumn));
                        if (converted.SemanticKind != MarkdownBlockKind.TableCell)
                            cellMetadata = cellMetadata.SetItem("column-span", Number(Math.Max(1, cell.ColumnSpan)))
                                .SetItem("row-span", Number(Math.Max(1, cell.RowSpan)));
                        if (definitions is not null && targetColumn >= 0 && targetColumn < definitions.Count)
                            cellMetadata = cellMetadata.SetItem("alignment", definitions[targetColumn].Alignment?.ToString().ToLowerInvariant() ?? "left");
                        converted = converted with { Metadata = cellMetadata };
                        cells.Add(converted);
                        columnIndex = targetColumn + Math.Max(1, cell.ColumnSpan);
                        for (var span = 1; span < cell.ColumnSpan; span++) _text.Append('\t');
                    }
                    var rowColumns = row.Parent is Table parentTable ? TableColumnCount(parentTable) : columnIndex;
                    // A rowspan's continuation has no TableCell in this row. Preserve all missing
                    // trailing slots too; an entirely empty row contains columns - 1 separators.
                    if (cells.Count == 0 && rowColumns > 0) columnIndex = 1;
                    while (columnIndex < rowColumns) { _text.Append('\t'); columnIndex++; }
                    children = Freeze(cells);
                    break;
                case TableCell cell:
                    kind = MarkdownBlockKind.TableCell;
                    metadata["column"] = Number(cell.ColumnIndex);
                    metadata["column-span"] = Number(Math.Max(1, cell.ColumnSpan));
                    metadata["row-span"] = Number(Math.Max(1, cell.RowSpan));
                    children = ConvertChildren(cell);
                    break;
                case FootnoteGroup group:
                    kind = MarkdownBlockKind.FootnoteGroup;
                    children = ConvertChildren(group);
                    break;
                case Footnote footnote:
                    kind = MarkdownBlockKind.Footnote;
                    metadata["label"] = footnote.Label ?? string.Empty;
                    metadata["number"] = Number(footnote.Order);
                    _text.Append('[').Append(metadata["number"]).Append("] ");
                    children = ConvertChildren(footnote);
                    break;
                case HtmlBlock html:
                    kind = MarkdownBlockKind.Html;
                    _text.Append(NormalizeNewlines(html.Lines.ToString()));
                    Diagnostic("MDHTML", "HTML is preserved as literal text; arbitrary HTML layout is not rendered.", block);
                    break;
                default:
                    if (custom is not null && block is ContainerBlock { Count: > 0 } container) children = ConvertChildren(container);
                    else if (custom is not null && block is LeafBlock { Inline: { FirstChild: not null } inline }) inlines = ConvertInlines(inline);
                    else _text.Append(SourceText(block));
                    if (custom is null)
                    {
                        metadata["ast-type"] = block.GetType().FullName ?? block.GetType().Name;
                        Diagnostic("MDNODE", $"No registered converter for {block.GetType().Name}; source text is preserved.", block);
                    }
                    break;
                }
                if (custom is not null)
                {
                    if (kind != MarkdownBlockKind.Unsupported) metadata[MarkdownCustomNodeMetadata.NativeKind] = kind.ToString();
                    kind = MarkdownBlockKind.Custom;
                }
            }
            return new(MarkdownDocumentMatcher.NewId(), kind, SourceStart(block), SourceLength(block), start,
                _text.Length - start, BlockText(start, children, inlines), children, inlines, metadata.ToImmutable());
        }

        private ImmutableArray<MarkdownInline> ConvertInlines(ContainerInline? container)
        {
            if (container is null) return [];
            var count = 0;
            for (var inline = container.FirstChild; inline is not null; inline = inline.NextSibling)
                if (inline is not FootnoteLink { IsBackLink: true }) count++;
            if (count == 0) return [];
            var result = ImmutableArray.CreateBuilder<MarkdownInline>(count);
            foreach (var inline in container)
            {
                if (inline is FootnoteLink { IsBackLink: true }) continue;
                result.Add(ConvertInline(inline));
            }
            return Freeze(result);
        }

        private MarkdownInline ConvertInline(Inline inline)
        {
            var start = _text.Length;
            var kind = MarkdownInlineKind.Unsupported;
            var metadata = new NodeMetadata();
            ImmutableArray<MarkdownInline> children = [];
            var custom = ConvertCustom(inline, out var failed);
            if (failed)
            {
                _text.Append(SourceText(inline));
                metadata[MarkdownCustomNodeMetadata.AstType] = inline.GetType().FullName ?? inline.GetType().Name;
            }
            else if (custom?.Text is { } customText)
            {
                kind = MarkdownInlineKind.Custom;
                metadata.AddRange(custom.Metadata);
                _text.Append(NormalizeNewlines(customText));
            }
            else
            {
                if (custom is not null) metadata.AddRange(custom.Metadata);
                switch (inline)
                {
                case LiteralInline literal:
                    kind = MarkdownInlineKind.Text;
                    AppendSlice(literal.Content);
                    break;
                case HtmlEntityInline entity:
                    kind = MarkdownInlineKind.Text;
                    AppendSlice(entity.Transcoded);
                    break;
                case CodeInline code:
                    kind = MarkdownInlineKind.Code;
                    _text.Append(code.Content);
                    break;
                case EmphasisInline emphasis:
                    kind = emphasis.DelimiterChar switch
                    {
                        '~' => emphasis.DelimiterCount == 2 ? MarkdownInlineKind.Strike : MarkdownInlineKind.Subscript,
                        '^' => MarkdownInlineKind.Superscript,
                        '+' => MarkdownInlineKind.Insert,
                        '=' => MarkdownInlineKind.Mark,
                        _ => emphasis.DelimiterCount == 2 ? MarkdownInlineKind.Strong : MarkdownInlineKind.Emphasis
                    };
                    children = ConvertInlines(emphasis);
                    break;
                case LinkInline link:
                    kind = link.IsImage ? MarkdownInlineKind.Image : MarkdownInlineKind.Link;
                    metadata["url"] = link.GetDynamicUrl?.Invoke() ?? link.Url ?? string.Empty;
                    metadata["title"] = link.Title ?? string.Empty;
                    children = ConvertInlines(link);
                    break;
                case AutolinkInline link:
                    kind = MarkdownInlineKind.Link;
                    metadata["url"] = (link.IsEmail ? "mailto:" : string.Empty) + link.Url;
                    _text.Append(link.Url);
                    break;
                case LineBreakInline lineBreak:
                    kind = MarkdownInlineKind.LineBreak;
                    metadata["hard"] = lineBreak.IsHard ? "true" : "false";
                    _text.Append(lineBreak.IsHard ? '\n' : ' ');
                    break;
                case FootnoteLink footnoteLink:
                    kind = MarkdownInlineKind.FootnoteReference;
                    metadata["label"] = footnoteLink.Footnote.Label ?? string.Empty;
                    metadata["number"] = Number(footnoteLink.Footnote.Order);
                    if (!footnoteLink.IsBackLink) _text.Append('[').Append(metadata["number"]).Append(']');
                    break;
                case TaskList task:
                    kind = MarkdownInlineKind.Task;
                    metadata["checked"] = task.Checked ? "true" : "false";
                    _text.Append(task.Checked ? "[x]" : "[ ]");
                    break;
                case HtmlInline html:
                    kind = MarkdownInlineKind.Html;
                    _text.Append(html.Tag);
                    Diagnostic("MDHTML", "Inline HTML is preserved as literal text.", inline);
                    break;
                default:
                    if (custom is not null && inline is ContainerInline { FirstChild: not null } container) children = ConvertInlines(container);
                    else _text.Append(SourceText(inline));
                    if (custom is null)
                    {
                        metadata["ast-type"] = inline.GetType().FullName ?? inline.GetType().Name;
                        Diagnostic("MDNODE", $"No registered converter for {inline.GetType().Name}; source text is preserved.", inline);
                    }
                    break;
                }
                if (custom is not null)
                {
                    if (kind != MarkdownInlineKind.Unsupported) metadata[MarkdownCustomNodeMetadata.NativeKind] = kind.ToString();
                    kind = MarkdownInlineKind.Custom;
                }
            }
            return new(kind, SourceStart(inline), SourceLength(inline), start, _text.Length - start,
                InlineText(start, children), children, metadata.ToImmutable());
        }

        private string BlockText(int start, ImmutableArray<MarkdownBlock> children, ImmutableArray<MarkdownInline> inlines)
        {
            var length = _text.Length - start;
            // These immutable values were converted from this exact builder interval.
            // Structural prefixes/separators make the ranges differ and retain their own text.
            if (children.Length == 1 && children[0].TextStart == start && children[0].TextLength == length)
                return children[0].Text;
            if (inlines.Length == 1 && inlines[0].TextStart == start && inlines[0].TextLength == length)
                return inlines[0].Text;
            return _text.ToString(start, length);
        }

        private string InlineText(int start, ImmutableArray<MarkdownInline> children)
        {
            var length = _text.Length - start;
            return children.Length == 1 && children[0].TextStart == start && children[0].TextLength == length
                ? children[0].Text : _text.ToString(start, length);
        }

        private void AppendSlice(StringSlice slice)
        {
            if (slice.Text is { } text && slice.Length > 0) _text.Append(text, slice.Start, slice.Length);
        }

        private void Diagnostic(string code, string message, MarkdownObject node) =>
            _diagnostics.Add(new(code, message, SourceStart(node), SourceLength(node)));

        private static ImmutableArray<T> Freeze<T>(ImmutableArray<T>.Builder builder) =>
            builder.Count == builder.Capacity ? builder.MoveToImmutable() : builder.ToImmutable();

        // Empty metadata is common for paragraphs, literal text and emphasis. Keep the builder
        // reference on the stack and allocate its mutable builder only when a key is actually set.
        private struct NodeMetadata
        {
            private ImmutableDictionary<string, string>.Builder? _builder;
            public string this[string key]
            {
                get => _builder is null ? throw new KeyNotFoundException() : _builder[key];
                set => (_builder ??= ImmutableDictionary.CreateBuilder<string, string>())[key] = value;
            }
            public void AddRange(ImmutableDictionary<string, string> values)
            {
                if (values.Count > 0) (_builder ??= ImmutableDictionary.CreateBuilder<string, string>()).AddRange(values);
            }
            public ImmutableDictionary<string, string> ToImmutable() => _builder?.ToImmutable() ?? ImmutableDictionary<string, string>.Empty;
        }

        private int TableColumnCount(Table table)
        {
            if (_tableColumnCounts.TryGetValue(table, out var count)) return count;
            foreach (TableRow row in table)
            {
                var nextColumn = 0;
                foreach (TableCell cell in row)
                    nextColumn = Math.Max(nextColumn, cell.ColumnIndex) + Math.Max(1, cell.ColumnSpan);
                count = Math.Max(count, nextColumn);
            }
            _tableColumnCounts.Add(table, count);
            return count;
        }
        private MarkdownCustomNode? ConvertCustom(MarkdownObject node, out bool failed)
        {
            failed = false;
            foreach (var registration in registrations)
            {
                if (!registration.Matches(node)) continue;
                try
                {
                    var (sourceStart, sourceLength) = OriginalSourceRange(node);
                    var result = registration.Convert(node, source, sourceStart, sourceLength);
                    if (result is null) continue;
                    return ValidateCustom(result, node);
                }
                catch (Exception error) when (error is not OutOfMemoryException)
                {
                    failed = true;
                    // Do not expose exception messages: a host callback may include source text in them.
                    var preserved = node is FencedCodeBlock fence && IsInsideTableCell(fence) ?
                        "native code text is preserved." : "source text is preserved.";
                    Diagnostic("MDNODECONV", $"Custom converter for {node.GetType().Name} failed ({error.GetType().Name}); {preserved}", node);
                    return null;
                }
            }
            return null;
        }

        private static MarkdownCustomNode ValidateCustom(MarkdownCustomNode node, MarkdownObject ast)
        {
            if (string.IsNullOrWhiteSpace(node.RendererKey) || node.RendererKey.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
                throw new ArgumentException("RendererKey must be a nonempty identifier without whitespace or control characters.");
            if (node.Metadata is null) throw new ArgumentException("Metadata cannot be null.");
            foreach (var pair in node.Metadata)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Key != pair.Key.Trim() || pair.Key.Any(char.IsControl) || pair.Value is null)
                    throw new ArgumentException("Metadata requires nonempty trimmed keys and non-null string values.");
                if (string.Equals(pair.Key, MarkdownCustomNodeMetadata.RendererKey, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(pair.Key, MarkdownCustomNodeMetadata.StyleClasses, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(pair.Key, MarkdownCustomNodeMetadata.AstType, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(pair.Key, MarkdownCustomNodeMetadata.NativeKind, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Metadata cannot set reserved renderer-key, style-classes, ast-type or native-kind entries.");
            }
            var classes = node.StyleClasses.IsDefault ? ImmutableArray<string>.Empty : node.StyleClasses;
            if (classes.Any(value => string.IsNullOrWhiteSpace(value) || value.Any(character => char.IsWhiteSpace(character) || char.IsControl(character))))
                throw new ArgumentException("Style classes must be nonempty identifiers without whitespace or control characters.");
            classes = classes.Distinct(StringComparer.Ordinal).ToImmutableArray();
            var metadata = node.Metadata.WithComparers(StringComparer.Ordinal)
                .Add(MarkdownCustomNodeMetadata.RendererKey, node.RendererKey)
                .Add(MarkdownCustomNodeMetadata.StyleClasses, string.Join(" ", classes))
                .Add(MarkdownCustomNodeMetadata.AstType, ast.GetType().FullName ?? ast.GetType().Name);
            return node with { StyleClasses = classes, Metadata = metadata };
        }

        private int SourceStart(MarkdownObject node) => Math.Clamp(node.Span.Start, 0, source.Length);
        private int SourceLength(MarkdownObject node) => (int)Math.Clamp((long)node.Span.End + 1 - SourceStart(node), 0, source.Length - SourceStart(node));
        private string SourceText(MarkdownObject node)
        {
            var (start, length) = OriginalSourceRange(node);
            return NormalizeNewlines(source.Substring(start, length));
        }

        private (int Start, int Length) OriginalSourceRange(MarkdownObject node)
        {
            var start = SourceStart(node);
            var length = SourceLength(node);
            if (node is not FencedCodeBlock { ClosingFencedCharCount: 0 } block ||
                block.Span.Start < 0 || block.Span.Start >= source.Length || IsInsideTableCell(block))
                return (start, length);

            // Markdig may end an unclosed fence's span inside its opening info string.
            // Context/failure recovery include its physical header and owned code lines;
            // snapshot/diagnostic spans keep the original AST coordinates. Line positions
            // bound recovery so a following quote/list sibling is never consumed.
            var end = Math.Max(start + length, PhysicalLineEnd(start));
            for (var index = 0; index < block.Lines.Count; index++)
            {
                var position = block.Lines.Lines[index].Position;
                if (position >= start && position <= source.Length)
                    end = Math.Max(end, PhysicalLineEnd(position));
            }
            return (start, end - start);
        }

        private static bool IsInsideTableCell(Block block)
        {
            // Grid cells occupy discontiguous slices of physical lines. A single extended
            // source range would include adjacent cells, so keep the AST range there.
            for (var parent = block.Parent; parent is not null; parent = parent.Parent)
                if (parent is TableCell) return true;
            return false;
        }

        private int PhysicalLineEnd(int start)
        {
            var newline = source.AsSpan(start).IndexOfAny('\r', '\n');
            if (newline < 0) return source.Length;
            var end = start + newline + 1;
            if (source[end - 1] == '\r' && end < source.Length && source[end] == '\n') end++;
            return end;
        }

        private void AppendCodeLines(CodeBlock block)
        {
            for (var index = 0; index < block.Lines.Count; index++)
            {
                var line = block.Lines.Lines[index];
                AppendSlice(line.Slice);
                if (line.NewLine != NewLine.None) _text.Append('\n');
            }
        }
        private string FenceInfo(FencedCodeBlock block)
        {
            if (IsInsideTableCell(block))
                return string.IsNullOrEmpty(block.Arguments) ? block.Info ?? string.Empty :
                    $"{block.Info} {block.Arguments}".Trim();
            if (block.Span.Start < 0 || block.Span.Start > source.Length) return block.Info ?? string.Empty;
            // An unclosed fence can have a truncated Markdig Span.End. Read its original
            // opening line without changing the snapshot's AST-derived source span.
            var opening = source.AsSpan(SourceStart(block));
            var newline = opening.IndexOfAny('\r', '\n');
            if (newline >= 0) opening = opening[..newline];
            var fence = opening.IndexOf(block.FencedChar);
            if (fence < 0) return block.Info ?? string.Empty;
            while (fence < opening.Length && opening[fence] == block.FencedChar) fence++;
            return opening[fence..].Trim().ToString();
        }
        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
        private static string NormalizeNewlines(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    }
}
