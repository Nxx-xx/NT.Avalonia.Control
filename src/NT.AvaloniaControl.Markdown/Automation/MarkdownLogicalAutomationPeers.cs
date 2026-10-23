using System.Collections.Immutable;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.Markdown;

internal abstract class MarkdownLogicalAutomationPeer : UnrealizedElementAutomationPeer, IValueProvider
{
    private readonly AutomationPeer _parent;
    protected MarkdownLogicalAutomationPeer(MarkdownAutomationPeer root, AutomationPeer parent)
    { Root = root; _parent = parent; SetParent(parent); }
    protected MarkdownAutomationPeer Root { get; }
    protected abstract bool Exists { get; }
    protected abstract int Position { get; }
    protected abstract int Length { get; }
    public bool IsReadOnly => true;
    public abstract string Value { get; }
    public void SetValue(string? value) => throw new InvalidOperationException("Markdown is a read-only document.");
    protected override string? GetAcceleratorKeyCore() => null;
    protected override string? GetAccessKeyCore() => null;
    protected override AutomationPeer? GetLabeledByCore() => null;
    protected override AutomationPeer GetParentCore() => _parent;
    protected override string GetNameCore() => Value;
    protected override string GetItemStatusCore() => Exists ? "readOnly=true" : "removed=true";
    protected override Rect GetBoundingRectangleCore() => Exists ? Root.TextBounds(Position, Length) : default;
    protected override bool IsOffscreenCore() => !Exists || !Root.IsVisible(GetBoundingRectangleCore());
    protected override bool IsEnabledCore() => Exists && Root.Reader.IsEffectivelyEnabled;
    protected override bool IsKeyboardFocusableCore() => false;
    protected override bool HasKeyboardFocusCore() => false;
    protected override void SetFocusCore() => throw new InvalidOperationException("This logical reading node is not keyboard focusable.");
    protected override void BringIntoViewCore()
    { Root.Reader.VerifyAccess(); EnsureEnabled(); Root.Reader.BringAutomationPositionIntoView(Position); }
    protected override IReadOnlyList<AutomationPeer> GetOrCreateChildrenCore() => [];
}

internal sealed class MarkdownBlockAutomationPeer(MarkdownAutomationPeer root, AutomationPeer parent, long blockId)
    : MarkdownLogicalAutomationPeer(root, parent)
{
    private readonly Dictionary<string, MarkdownInlineAutomationPeer> _inlines = [];
    private IReadOnlyList<AutomationPeer>? _children;
    private string _lastValue = root.ResolveBlock(blockId)?.Text ?? string.Empty;
    private MarkdownBlock? Block => Root.ResolveBlock(blockId);
    protected override bool Exists => Block is not null;
    protected override int Position => Block?.TextStart ?? 0;
    protected override int Length => Block?.TextLength ?? 0;
    public override string Value => Block?.Text ?? string.Empty;
    internal int InlinePeerCount => _inlines.Count;
    protected override string GetAutomationIdCore() => $"Markdown.Block.{blockId}";
    protected override string GetClassNameCore() => "Markdown" + (Block?.SemanticKind.ToString() ?? "RemovedBlock");
    protected override AutomationControlType GetAutomationControlTypeCore() => Block?.SemanticKind switch
    {
        MarkdownBlockKind.List => AutomationControlType.List,
        MarkdownBlockKind.ListItem => AutomationControlType.ListItem,
        MarkdownBlockKind.Table => AutomationControlType.Table,
        MarkdownBlockKind.ThematicBreak => AutomationControlType.Separator,
        MarkdownBlockKind.Quote or MarkdownBlockKind.Alert or MarkdownBlockKind.TableRow or MarkdownBlockKind.FootnoteGroup => AutomationControlType.Group,
        _ => AutomationControlType.Text
    };
    protected override int GetHeadingLevelCore() => Block is { SemanticKind: MarkdownBlockKind.Heading } block &&
        int.TryParse(block.Metadata.GetValueOrDefault("level"), out var level) ? level : 0;
    protected override Rect GetBoundingRectangleCore()
    {
        var view = Root.Reader.GetVisualDescendants().OfType<MarkdownBlockPresenter>().FirstOrDefault(view => view.NodeId == blockId);
        return view is not null ? ControlAutomationPeer.CreatePeerForElement(view)!.GetBoundingRectangle() : base.GetBoundingRectangleCore();
    }
    protected override IReadOnlyList<AutomationPeer> GetOrCreateChildrenCore()
    {
        if (_children is not null) return _children;
        if (Block is not { } block) return [];
        var children = new List<(int Position, AutomationPeer Peer)>();
        foreach (var child in block.Children) children.Add((child.TextStart, Root.GetBlockPeer(child.Id, this)));
        foreach (var target in InlineTarget.Enumerate(blockId, block.Inlines))
        {
            if (!_inlines.TryGetValue(target.PathKey, out var peer) || !peer.Matches(target))
                _inlines[target.PathKey] = peer = target.Kind is MarkdownInlineKind.Link or MarkdownInlineKind.FootnoteReference
                    ? new MarkdownLinkAutomationPeer(Root, this, target) : new MarkdownInlineAutomationPeer(Root, this, target);
            children.Add((target.TextStart, peer));
        }
        return _children = children.OrderBy(child => child.Position).Select(child => child.Peer).ToArray();
    }
    internal void DocumentChanged()
    {
        _children = null;
        foreach (var pair in _inlines.ToArray()) if (!pair.Value.IsCurrent) _inlines.Remove(pair.Key);
        var value = Value;
        if (_lastValue != value)
        {
            RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, _lastValue, value);
            RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, _lastValue, value);
        }
        _lastValue = value;
        RaiseChildrenChangedEvent();
    }
}

internal class MarkdownInlineAutomationPeer(MarkdownAutomationPeer root, AutomationPeer parent, InlineTarget target)
    : MarkdownLogicalAutomationPeer(root, parent)
{
    protected InlineTarget Target { get; } = target;
    protected MarkdownInline? Inline => Target.Resolve(Root);
    internal bool IsCurrent => Inline is not null;
    internal bool Matches(InlineTarget other) => Target.SameIdentity(other);
    protected override bool Exists => IsCurrent;
    protected override int Position => Inline?.TextStart ?? 0;
    protected override int Length => Inline?.TextLength ?? 0;
    public override string Value => Inline is { } inline ? inline.SemanticKind == MarkdownInlineKind.Task
        ? inline.Metadata.GetValueOrDefault("checked", "false") : inline.Text : string.Empty;
    protected override string GetAutomationIdCore() => $"Markdown.Inline.{Target.BlockId}.{Target.PathKey}";
    protected override string GetClassNameCore() => "Markdown" + Target.Kind;
    protected override AutomationControlType GetAutomationControlTypeCore() => Target.Kind switch
    { MarkdownInlineKind.Task => AutomationControlType.CheckBox, MarkdownInlineKind.Image => AutomationControlType.Image, _ => AutomationControlType.Hyperlink };
    protected override string GetNameCore() => Target.Kind == MarkdownInlineKind.Task && Root.ResolveBlock(Target.BlockId) is { } block
        ? TaskName(block.Text) : Inline?.Text ?? string.Empty;
    protected override string GetItemStatusCore() => !IsCurrent ? "removed=true" : Target.Kind == MarkdownInlineKind.Task
        ? $"readOnly=true; checked={Value}" : "readOnly=true";
    protected override string GetHelpTextCore() => Target.Kind == MarkdownInlineKind.Task
        ? "Read-only task state; no Toggle provider is exposed." : Inline?.Metadata.GetValueOrDefault("url", "") ?? string.Empty;
    private static string TaskName(string text)
    {
        text = text.TrimStart();
        return text.StartsWith("[x]", StringComparison.OrdinalIgnoreCase) || text.StartsWith("[ ]", StringComparison.Ordinal)
            ? text[3..].TrimStart() : text;
    }
}

internal sealed class MarkdownLinkAutomationPeer(MarkdownAutomationPeer root, AutomationPeer parent, InlineTarget target)
    : MarkdownInlineAutomationPeer(root, parent, target), IInvokeProvider
{
    protected override bool IsKeyboardFocusableCore() => IsEnabledCore();
    protected override bool HasKeyboardFocusCore() => IsCurrent && Root.Reader.IsFocused && Root.Reader.Selection.IsEmpty && Root.Reader.Selection.Active == Position;
    protected override void SetFocusCore() { Root.Reader.VerifyAccess(); EnsureEnabled(); Root.Reader.FocusAutomationLink(Position); }
    public void Invoke()
    {
        Root.Reader.VerifyAccess();
        EnsureEnabled();
        if (!IsCurrent) throw new InvalidOperationException("The link no longer belongs to this document.");
        Root.Reader.InvokeAutomationLink(Position);
    }
}

internal sealed record InlineTarget(long BlockId, ImmutableArray<int> Path, MarkdownInlineKind Kind,
    string Text, ImmutableDictionary<string, string> Metadata, int TextStart)
{
    public string PathKey => string.Join(".", Path);
    public bool SameIdentity(InlineTarget other) => BlockId == other.BlockId && Path.SequenceEqual(other.Path) &&
        Kind == other.Kind && Text == other.Text && SameMetadata(Metadata, other.Metadata);
    public MarkdownInline? Resolve(MarkdownAutomationPeer root)
    {
        var children = root.ResolveBlock(BlockId)?.Inlines ?? [];
        MarkdownInline? inline = null;
        foreach (var index in Path)
        {
            if (index < 0 || index >= children.Length) return null;
            inline = children[index]; children = inline.Children;
        }
        return inline is not null && inline.SemanticKind == Kind && inline.Text == Text && SameMetadata(Metadata, inline.Metadata) ? inline : null;
    }
    public static IEnumerable<InlineTarget> Enumerate(long blockId, ImmutableArray<MarkdownInline> inlines) => Walk(blockId, inlines, []);
    private static IEnumerable<InlineTarget> Walk(long blockId, ImmutableArray<MarkdownInline> inlines, ImmutableArray<int> path)
    {
        for (var i = 0; i < inlines.Length; i++)
        {
            var inline = inlines[i]; var next = path.Add(i);
            if (inline.SemanticKind is MarkdownInlineKind.Link or MarkdownInlineKind.FootnoteReference or MarkdownInlineKind.Task or MarkdownInlineKind.Image)
                yield return new(blockId, next, inline.SemanticKind, inline.Text, inline.Metadata, inline.TextStart);
            foreach (var nested in Walk(blockId, inline.Children, next)) yield return nested;
        }
    }
    private static bool SameMetadata(ImmutableDictionary<string, string> first, ImmutableDictionary<string, string> second) =>
        first.Count == second.Count && first.All(pair => second.TryGetValue(pair.Key, out var value) && pair.Value == value);
}
