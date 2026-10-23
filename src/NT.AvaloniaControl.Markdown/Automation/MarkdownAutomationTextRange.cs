using Avalonia;
using Avalonia.Automation.Peers;

namespace NT.AvaloniaControl.Markdown;

/// <summary>
/// A snapshot-bound logical UTF-16 range normalized to grapheme boundaries. This is a C#
/// adapter, not native UIA TextPattern: Avalonia 12.0.3 exports no ITextRangeProvider.
/// </summary>
public sealed class MarkdownAutomationTextRange
{
    private readonly Markdown _owner;
    private readonly MarkdownDocumentSnapshot _document;
    private readonly long _generation;
    internal MarkdownAutomationTextRange(Markdown owner, MarkdownDocumentSnapshot document, MarkdownSelection selection)
    {
        _owner = owner;
        _document = document;
        _generation = owner.AutomationDocumentGeneration;
        Selection = selection.Normalize(document.Text);
    }
    public MarkdownSelection Selection { get; }
    public int Start => Selection.Start;
    public int Length => Selection.Length;
    public string Text => Selection.GetText(_document);
    public bool IsCurrent => _generation == _owner.AutomationDocumentGeneration && ReferenceEquals(_owner.Document, _document);
    public void Select()
    {
        VerifyCurrent();
        if (!_owner.IsEffectivelyEnabled || !_owner.SelectionEnabled) throw new InvalidOperationException("Selection is unavailable for this document.");
        _owner.SetSelection(Selection.Anchor, Selection.Active);
    }
    public void BringIntoView() { VerifyCurrent(); _owner.BringAutomationPositionIntoView(Start); }
    private void VerifyCurrent()
    {
        _owner.VerifyAccess();
        if (!IsCurrent) throw new InvalidOperationException("The range belongs to an older document snapshot.");
    }
}

public partial class Markdown
{
    internal long AutomationDocumentGeneration => _updates.DocumentGeneration;
    internal void BringAutomationPositionIntoView(int position)
    {
        VerifyAccess();
        _readingRevision++;
        var document = Document;
        var generation = AutomationDocumentGeneration;
        BringPositionIntoView(position);
        // Estimated heights may change when the target and its overscan become realized.
        // Correct from actual native bounds, in the same coordinate system as the viewport.
        for (var pass = 0; pass < 6 && _scroll is not null && _presenter is not null; pass++)
        {
            if (!ReferenceEquals(document, Document) || generation != AutomationDocumentGeneration)
                throw new InvalidOperationException("The document changed during automation navigation.");
            _presenter.SetViewport(_scroll.Offset.Y, _scroll.Viewport.Height);
            UpdateLayout();
            if (!ReferenceEquals(document, Document) || generation != AutomationDocumentGeneration)
                throw new InvalidOperationException("The document changed during automation navigation.");
            if (ControlAutomationPeer.FromElement(this) is not MarkdownAutomationPeer peer) break;
            var bounds = peer.TextBounds(position, position < Document.Text.Length ? 1 : 0);
            if (bounds.Height <= 0) { BringPositionIntoView(position); continue; }
            var viewport = ControlAutomationPeer.CreatePeerForElement(_scroll)!.GetBoundingRectangle().Deflate(_scroll.Padding);
            var delta = bounds.Top < viewport.Top ? bounds.Top - viewport.Top - 2 :
                bounds.Bottom > viewport.Bottom ? bounds.Bottom - viewport.Bottom + 2 : 0;
            if (Math.Abs(delta) < .1) break;
            _scroll.Offset = new Vector(_scroll.Offset.X, Math.Max(0, _scroll.Offset.Y + delta));
        }
        if (_scroll is not null && _presenter is not null) _presenter.SetViewport(_scroll.Offset.Y, _scroll.Viewport.Height);
        UpdateLayout();
    }
    internal void FocusAutomationLink(int position)
    {
        VerifyAccess();
        _linkFocusPosition = position;
        SetSelection(position, position);
        Focus();
        BringAutomationPositionIntoView(position);
    }
    internal void InvokeAutomationLink(int position) { VerifyAccess(); ActivateLink(position); }
}
