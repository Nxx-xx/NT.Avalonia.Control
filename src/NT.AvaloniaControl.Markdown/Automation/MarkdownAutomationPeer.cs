using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.Markdown;

/// <summary>
/// Logical read-only document provider independent of realized visuals. Avalonia 12.0.3
/// has no public text/text-range provider; the C# ranges below are not native TextPattern.
/// </summary>
public sealed class MarkdownAutomationPeer : ControlAutomationPeer, IValueProvider
{
    private readonly Markdown _owner;
    private readonly Dictionary<long, MarkdownBlockAutomationPeer> _blocks = [];
    private Dictionary<long, MarkdownBlock>? _blockIndex;
    private MarkdownDocumentSnapshot? _indexedDocument;
    private string _lastValue;
    private string _lastSelectionStatus;

    public MarkdownAutomationPeer(Markdown owner) : base(owner)
    {
        _owner = owner;
        _lastValue = owner.Document.Text;
        _lastSelectionStatus = SelectionStatus();
        SubscribeWeakly(owner, this, document: true);
        SubscribeWeakly(owner, this, document: false);
    }

    public bool IsReadOnly => true;
    public string Value => _owner.Document.Text;
    public void SetValue(string? value) => throw new InvalidOperationException("Markdown is a read-only document.");
    /// <summary>Count of requested logical peers, including inline peers; descendants remain lazy.</summary>
    public int CachedLogicalPeerCount => _blocks.Count + _blocks.Values.Sum(block => block.InlinePeerCount);

    /// <summary>A logical captured range, without advertising native UIA TextPattern.</summary>
    public MarkdownAutomationTextRange GetTextRange(int start, int length)
    {
        _owner.VerifyAccess();
        if (start < 0 || start > Value.Length) throw new ArgumentOutOfRangeException(nameof(start));
        if (length < 0 || length > Value.Length - start) throw new ArgumentOutOfRangeException(nameof(length));
        return new(_owner, _owner.Document, new(start, start + length));
    }
    public MarkdownAutomationTextRange GetSelectionRange()
    {
        _owner.VerifyAccess();
        return new(_owner, _owner.Document, _owner.Selection);
    }
    protected override string GetClassNameCore() => nameof(Markdown);
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Document;
    protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : nameof(Markdown);
    protected override string GetItemStatusCore() => SelectionStatus();
    protected override string? GetHelpTextCore() => base.GetHelpTextCore() is { Length: > 0 } help ? help :
        "Read-only Markdown. Value exposes complete logical text; native TextPattern is unavailable in Avalonia 12.0.3.";
    protected override IReadOnlyList<AutomationPeer> GetChildrenCore() =>
        _owner.Document.Blocks.Select(block => (AutomationPeer)GetBlockPeer(block.Id, this)).ToArray();
    internal Markdown Reader => _owner;

    internal MarkdownBlock? ResolveBlock(long id)
    {
        if (!ReferenceEquals(_indexedDocument, _owner.Document))
        {
            _indexedDocument = _owner.Document;
            _blockIndex = _owner.Document.Descendants().ToDictionary(block => block.Id);
        }
        return _blockIndex!.GetValueOrDefault(id);
    }
    internal MarkdownBlockAutomationPeer GetBlockPeer(long id, AutomationPeer parent)
    {
        if (!_blocks.TryGetValue(id, out var peer)) _blocks.Add(id, peer = new(this, parent, id));
        return peer;
    }
    internal Rect TextBounds(int start, int length)
    {
        if (TopLevel.GetTopLevel(_owner) is not Visual root) return default;
        var bounds = default(Rect);
        foreach (var text in _owner.GetVisualDescendants().OfType<MarkdownTextPresenter>())
        {
            var begin = Math.Max(start, text.TextStart);
            var end = Math.Min(start + length, text.TextStart + text.TextLength);
            if (end < begin || (end == begin && length > 0) || text.TransformToVisual(root) is not { } transform) continue;
            IEnumerable<Rect> rectangles = length == 0 ? [text.TextLayout.HitTestTextPosition(begin - text.TextStart)] :
                MarkdownTextRangeGeometry.GetBounds(text, _owner.Document.Text, begin - text.TextStart, end - begin);
            foreach (var rect in rectangles)
            {
                var translated = rect.Translate(new Vector(text.Padding.Left, text.Padding.Top)).TransformToAABB(transform);
                bounds = bounds == default ? translated : bounds.Union(translated);
            }
        }
        return bounds;
    }
    internal bool IsVisible(Rect bounds) => _owner.IsEffectivelyVisible && _owner.HasPresentationEnvironment &&
        bounds.Width > 0 && bounds.Height > 0 && GetBoundingRectangle().Intersects(bounds);
    private string SelectionStatus() => $"readOnly=true; selectionStart={_owner.Selection.Start}; selectionLength={_owner.Selection.Length}";

    private void DocumentChanged()
    {
        _owner.VerifyAccess();
        _indexedDocument = null;
        _blockIndex = null;
        foreach (var pair in _blocks.ToArray())
        {
            if (ResolveBlock(pair.Key) is null) _blocks.Remove(pair.Key);
            else pair.Value.DocumentChanged();
        }
        InvalidateChildren();
        var current = Value;
        if (_lastValue != current) RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, _lastValue, current);
        _lastValue = current;
        SelectionChanged();
    }
    private void SelectionChanged()
    {
        var status = SelectionStatus();
        if (_lastSelectionStatus != status) RaisePropertyChangedEvent(AutomationElementIdentifiers.ItemStatusProperty, _lastSelectionStatus, status);
        _lastSelectionStatus = status;
    }
    private static void SubscribeWeakly(Markdown owner, MarkdownAutomationPeer peer, bool document)
    {
        var weak = new WeakReference<MarkdownAutomationPeer>(peer);
        EventHandler? handler = null;
        handler = (sender, _) =>
        {
            if (weak.TryGetTarget(out var current))
            {
                if (document) current.DocumentChanged(); else current.SelectionChanged();
            }
            else if (sender is Markdown source)
            {
                if (document) source.Updated -= handler; else source.SelectionChanged -= handler;
            }
        };
        if (document) owner.Updated += handler; else owner.SelectionChanged += handler;
    }
}
