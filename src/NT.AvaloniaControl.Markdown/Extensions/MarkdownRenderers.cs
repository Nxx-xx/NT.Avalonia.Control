using System.Collections.Immutable;
using Avalonia.Controls;
using Avalonia.Controls.Documents;

namespace NT.AvaloniaControl.Markdown;

/// <summary>Immutable, host-owned UI renderer registrations. Keys correspond to converter renderer-key metadata.</summary>
public sealed class MarkdownRendererRegistry
{
    public static MarkdownRendererRegistry Empty { get; } = new();
    public MarkdownRendererRegistry(IEnumerable<MarkdownBlockRendererRegistration>? blocks = null, IEnumerable<MarkdownInlineRendererRegistration>? inlines = null)
    {
        Blocks = (blocks ?? []).ToImmutableDictionary(registration => ValidateKey(registration?.Key), registration => ValidateCallback(registration.Render), StringComparer.Ordinal);
        Inlines = (inlines ?? []).ToImmutableDictionary(registration => ValidateKey(registration?.Key), registration => ValidateCallback(registration.Render), StringComparer.Ordinal);
    }
    public ImmutableDictionary<string, Func<MarkdownBlockRenderContext, Control>> Blocks { get; }
    public ImmutableDictionary<string, Func<MarkdownInlineRenderContext, Inline>> Inlines { get; }
    private static string ValidateKey(string? key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (key != key.Trim()) throw new ArgumentException("Renderer keys must be trimmed.", nameof(key));
        return key;
    }
    private static T ValidateCallback<T>(T callback) where T : Delegate { ArgumentNullException.ThrowIfNull(callback); return callback; }
}

public sealed record MarkdownBlockRendererRegistration(string Key, Func<MarkdownBlockRenderContext, Control> Render);
public sealed record MarkdownInlineRendererRegistration(string Key, Func<MarkdownInlineRenderContext, Inline> Render);

/// <summary>UI-only renderer context. Use native text helpers to preserve global selection geometry.</summary>
public sealed class MarkdownBlockRenderContext
{
    private readonly Func<Control> _default;
    private readonly Func<int, int, MarkdownTextPresenter> _text;
    private readonly Action<IDisposable> _register;
    internal MarkdownBlockRenderContext(Markdown owner, MarkdownDocumentSnapshot document, MarkdownBlock block, Func<Control> createDefault, Func<int, int, MarkdownTextPresenter> createText, Action<IDisposable> register)
    { Owner = owner; Document = document; Block = block; _default = createDefault; _text = createText; _register = register; }
    public Markdown Owner { get; }
    public MarkdownDocumentSnapshot Document { get; }
    public MarkdownBlock Block { get; }
    public Control CreateDefault() { Owner.VerifyAccess(); return _default(); }
    public MarkdownTextPresenter CreateText() { Owner.VerifyAccess(); return _text(Block.TextStart, Block.TextLength); }
    public MarkdownTextPresenter CreateText(int start, int length)
    {
        Owner.VerifyAccess();
        if (start < Block.TextStart || length < 0 || start > Block.TextStart + Block.TextLength - length) throw new ArgumentOutOfRangeException(nameof(start));
        return _text(start, length);
    }
    /// <summary>Register only resources owned by this realization. Host services remain host-owned.</summary>
    public void RegisterDisposable(IDisposable resource) { Owner.VerifyAccess(); ArgumentNullException.ThrowIfNull(resource); _register(resource); }
}

/// <summary>Custom inline renderers must preserve the node's exact logical text. InlineUIContainer is not a text mapping.</summary>
public sealed class MarkdownInlineRenderContext
{
    private readonly Func<Inline> _default;
    internal MarkdownInlineRenderContext(Markdown owner, MarkdownDocumentSnapshot document, MarkdownInline inline, Func<Inline> createDefault)
    { Owner = owner; Document = document; Inline = inline; _default = createDefault; }
    public Markdown Owner { get; }
    public MarkdownDocumentSnapshot Document { get; }
    public MarkdownInline Inline { get; }
    public Inline CreateDefault() { Owner.VerifyAccess(); return _default(); }
    public MarkdownRun CreateText() { Owner.VerifyAccess(); return new(Inline.Text); }
}
