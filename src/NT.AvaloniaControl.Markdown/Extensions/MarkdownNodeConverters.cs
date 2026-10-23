using System.Collections.Immutable;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace NT.AvaloniaControl.Markdown;

/// <summary>
/// Immutable registration list captured when a parser is created. Callbacks must be deterministic
/// and safe to invoke on a background thread; a callback's captured mutable state is host-owned.
/// </summary>
public sealed record MarkdownParserOptions
{
    public Action<MarkdownPipelineBuilder>? ConfigurePipeline { get; init; }
    public ImmutableArray<MarkdownNodeConverterRegistration> Converters { get; init; } = [];
}

/// <summary>
/// Pure data returned by a converter. A non-null Text replaces this node's content copy projection
/// and makes it a leaf. Its parent may still add structural text, such as a list marker or table
/// separator. Null Text retains a built-in node's native copy and structure rules, or
/// converts an unknown container's children and preserves source for an unknown childless node.
/// Built-in semantic metadata takes precedence over host payload keys. All resulting offsets,
/// source spans and identities are assigned by the library.
/// CRLF/CR in Text become LF. Empty Text is valid and contributes no selectable text. StyleClasses
/// are individual nonempty names without whitespace; duplicate names are collapsed in order.
/// Metadata allows empty values but rejects null, blank/untrimmed keys and reserved keys.
/// </summary>
public sealed record MarkdownCustomNode(string RendererKey, string? Text = null)
{
    public ImmutableArray<string> StyleClasses { get; init; } = [];
    public ImmutableDictionary<string, string> Metadata { get; init; } = ImmutableDictionary<string, string>.Empty;
}

/// <summary>Reserved snapshot metadata used to find a native renderer and apply semantic classes.</summary>
public static class MarkdownCustomNodeMetadata
{
    public const string RendererKey = "renderer-key";
    public const string StyleClasses = "style-classes";
    public const string AstType = "ast-type";
    /// <summary>Original block/inline enum name when null Text retained built-in semantics.</summary>
    public const string NativeKind = "native-kind";
}

/// <summary>
/// Short-lived input to a converter. Node belongs to the current Markdig parse and must not be
/// retained or mutated. SourceText is exact source, before logical LF normalization, and its
/// length equals SourceLength. For an unclosed fenced code block, SourceStart/SourceLength cover
/// the opening physical line and its code lines even when Markdig truncates the AST span, except
/// inside table cells whose source can be discontiguous. Snapshot
/// and diagnostic source ranges retain that original AST span. No context or AST object is stored
/// in the resulting document snapshot.
/// </summary>
public sealed class MarkdownNodeConversionContext<TNode> where TNode : MarkdownObject
{
    internal MarkdownNodeConversionContext(TNode node, string source, int sourceStart, int sourceLength)
    {
        Node = node; Source = source; SourceStart = sourceStart; SourceLength = sourceLength;
    }
    public TNode Node { get; }
    public string Source { get; }
    public int SourceStart { get; }
    public int SourceLength { get; }
    public string SourceText => Source.Substring(SourceStart, SourceLength);
}

/// <summary>
/// A registration can match a block or inline AST type. Registrations are tried in supplied order;
/// a null result declines the node so the next registration or built-in converter can handle it.
/// Reference definitions and footnote backlinks remain non-presentational and are not converted.
/// A callback exception or invalid output stops matching this node and preserves its original
/// source with an MDNODECONV diagnostic; no partially converted copy text is published.
/// Fenced code blocks inside table cells instead preserve their native code-line projection,
/// because the original cell source may occupy discontiguous portions of physical lines.
/// </summary>
public abstract class MarkdownNodeConverterRegistration
{
    internal MarkdownNodeConverterRegistration(Type nodeType)
    {
        if ((!typeof(Block).IsAssignableFrom(nodeType) && !typeof(Inline).IsAssignableFrom(nodeType)) || nodeType == typeof(MarkdownDocument))
            throw new ArgumentException("Converter node type must be a block or inline, excluding the document root.", nameof(nodeType));
        NodeType = nodeType;
    }
    public Type NodeType { get; }
    internal bool Matches(MarkdownObject node) => NodeType.IsInstanceOfType(node);
    internal abstract MarkdownCustomNode? Convert(MarkdownObject node, string source, int sourceStart, int sourceLength);
}

/// <summary>Type-safe AST registration. Returning UI objects is impossible in its output contract.</summary>
public sealed class MarkdownNodeConverterRegistration<TNode> : MarkdownNodeConverterRegistration where TNode : MarkdownObject
{
    private readonly Func<MarkdownNodeConversionContext<TNode>, MarkdownCustomNode?> _convert;
    public MarkdownNodeConverterRegistration(Func<MarkdownNodeConversionContext<TNode>, MarkdownCustomNode?> convert) : base(typeof(TNode))
    {
        ArgumentNullException.ThrowIfNull(convert);
        _convert = convert;
    }
    internal override MarkdownCustomNode? Convert(MarkdownObject node, string source, int sourceStart, int sourceLength) =>
        _convert(new((TNode)node, source, sourceStart, sourceLength));
}
