using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.Markdown;

internal sealed record MarkdownReadingLayoutAnchor(
    MarkdownDocumentSnapshot Document,
    long BlockId,
    long TopBlockId,
    int Position,
    double GlyphViewportOffset,
    double BlockOffset,
    double ScrollOffset,
    long ReadingRevision,
    WeakReference<MarkdownTextPresenter> TextPresenter,
    WeakReference<TextLayout> TextLayout,
    double TextWidth,
    double FontSize);

internal sealed record MarkdownFollowingDocumentCommit(
    MarkdownDocumentPresenter Presenter,
    ScrollViewer Scroll,
    long Generation,
    long ReadingRevision);

internal enum MarkdownFollowingDocumentCommitResult
{
    NotFollowing,
    Applied,
    Superseded,
    Pending
}

public partial class Markdown
{
    private sealed record ReadingLayoutRestore(MarkdownReadingLayoutAnchor Anchor,
        MarkdownDocumentSnapshot Document, WeakReference<MarkdownDocumentPresenter> Presenter,
        long Generation, long Revision, long Ticket);
    private sealed record CoarseReadingRestore(MarkdownReadingAnchor? Anchor,
        MarkdownDocumentSnapshot Document, WeakReference<MarkdownDocumentPresenter> Presenter,
        long Generation, long Revision, long Ticket);

    private MarkdownReadingLayoutAnchor? _stableReadingLayoutAnchor;
    private ReadingLayoutRestore? _pendingReadingLayoutRestore;
    private bool _readingLayoutTracking;
    private bool _readingLayoutQueued;
    private bool _readingLayoutPending;
    private bool _readingLayoutRestoring;
    private long _readingLayoutTicket;
    private CoarseReadingRestore? _pendingCoarseReadingRestore;
    private bool _coarseReadingQueued;
    private long _coarseReadingTicket;
    private Func<Vector, Vector, bool>? _followingDocumentOffsetDetector;

    internal bool IsFollowingDocumentOffsetAdjustment(AvaloniaPropertyChangedEventArgs change) =>
        change.Property == ScrollViewer.OffsetProperty &&
        _followingDocumentOffsetDetector?.Invoke(change.GetOldValue<Vector>(), change.GetNewValue<Vector>()) == true;

    internal MarkdownFollowingDocumentCommit? CaptureFollowingDocumentCommit()
    {
        VerifyAccess();
        return AutoScrollToEnd && _following && HasPresentationEnvironment && IsEffectivelyVisible &&
            _presenter is { } presenter && _scroll is { } scroll
            ? new(presenter, scroll, _updates.DocumentGeneration, _readingRevision) : null;
    }

    // The host captures this before changing Document, then includes this whole
    // synchronous operation in its layout metric. No successful follow needs a
    // later Loaded job to realize or scroll to the newly committed tail.
    internal MarkdownFollowingDocumentCommitResult TryApplyFollowingDocumentCommit(
        MarkdownFollowingDocumentCommit? capture, Func<bool> stillCurrent)
    {
        VerifyAccess();
        if (capture is null) return MarkdownFollowingDocumentCommitResult.NotFollowing;
        var document = Document;
        if (!IsCurrent()) return MarkdownFollowingDocumentCommitResult.Superseded;
        ResetReadingLayoutTracking();
        var scroll = capture.Scroll;
        var presenter = capture.Presenter;
        Vector? ownOffset = null;
        var performingLayout = false;
        _followingDocumentOffsetDetector = IsInternalOffset;
        scroll.PropertyChanged += OffsetChanged;
        try
        {
            presenter.BeginFollowingViewport(scroll.Viewport.Height, IsCurrent, () => scroll.Offset.Y);
            if (!Layout() || !IsCurrent()) return MarkdownFollowingDocumentCommitResult.Superseded;
            for (var pass = 0; pass < 4; pass++)
            {
                if (!IsCurrent()) return MarkdownFollowingDocumentCommitResult.Superseded;
                ownOffset = new Vector(scroll.Offset.X, Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height));
                var restoring = _restoring;
                _restoring = true;
                try { scroll.ScrollToEnd(); }
                finally { _restoring = restoring; ownOffset = null; }
                if (!IsCurrent()) return MarkdownFollowingDocumentCommitResult.Superseded;
                presenter.SetViewport(scroll.Offset.Y, scroll.Viewport.Height);
                if (!Layout() || !IsCurrent()) return MarkdownFollowingDocumentCommitResult.Superseded;
                if (!HasRealizedTail()) continue;
                ResetReadingLayoutTracking();
                _stableReadingLayoutAnchor = CaptureNativeReadingLayoutAnchor();
                return MarkdownFollowingDocumentCommitResult.Applied;
            }

            // A custom renderer can change its desired size during successive
            // layouts. Retain the original priority token for the bounded fallback;
            // never label unverified geometry as a completed synchronous follow.
            if (!IsCurrent()) return MarkdownFollowingDocumentCommitResult.Superseded;
            _pendingCoarseReadingRestore = new(null, document, new(presenter), capture.Generation,
                capture.ReadingRevision, ++_coarseReadingTicket);
            QueueCoarseReadingRestore();
            return MarkdownFollowingDocumentCommitResult.Pending;
        }
        finally
        {
            scroll.PropertyChanged -= OffsetChanged;
            _followingDocumentOffsetDetector = null;
            presenter.EndFollowingViewport(scroll.Offset.Y, scroll.Viewport.Height);
        }

        bool IsCurrent() => stillCurrent() && AutoScrollToEnd && _following && HasPresentationEnvironment &&
            IsEffectivelyVisible && ReferenceEquals(Document, document) &&
            ReferenceEquals(_presenter, capture.Presenter) && ReferenceEquals(_scroll, capture.Scroll) &&
            _updates.DocumentGeneration == capture.Generation && _readingRevision == capture.ReadingRevision;

        bool Layout()
        {
            if (!IsCurrent()) return false;
            performingLayout = true;
            try { UpdateLayout(); }
            finally { performingLayout = false; }
            return IsCurrent();
        }

        void OffsetChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
        {
            if (change.Property != ScrollViewer.OffsetProperty) return;
            var previous = change.GetOldValue<Vector>();
            var next = change.GetNewValue<Vector>();
            if (previous == next) return;
            if (IsInternalOffset(previous, next)) return;
            presenter.EndFollowingViewport(scroll.Offset.Y, scroll.Viewport.Height);
            if (_readingRevision == capture.ReadingRevision) _readingRevision++;
            _following = scroll.Extent.Height - scroll.Viewport.Height - scroll.Offset.Y <= 24;
            ResetReadingLayoutTracking();
        }

        bool IsInternalOffset(Vector previous, Vector next)
        {
            if (ownOffset is { } expected && Math.Abs(expected.X - next.X) < .01 && Math.Abs(expected.Y - next.Y) < .01) return true;
            // Native scroll anchoring can move either way as the measured extent
            // changes. Reaching that actual new end is the same follow destination;
            // a host choosing an interior reading offset must still supersede it.
            return performingLayout && previous.X == next.X &&
                Math.Abs(next.Y - Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height)) < .01;
        }

        bool HasRealizedTail()
        {
            if (!scroll.IsMeasureValid || !scroll.IsArrangeValid || !presenter.IsMeasureValid || !presenter.IsArrangeValid ||
                Math.Abs(scroll.Offset.Y - Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height)) > .5) return false;
            if (document.Blocks.IsEmpty) return true;
            // Bounds include the actual final arrangement; the estimated height
            // array or a ScrollViewer at its numerical end alone cannot prove a
            // tail realization. Margins belong to the document extent, not Bounds.
            var child = presenter.GetRealizedBlockView(document.Blocks[^1].Id);
            if (child is null || !child.IsEffectivelyVisible || child.Bounds.Height <= 0 ||
                Math.Abs(child.Bounds.Bottom + child.Margin.Bottom - presenter.Bounds.Height) > .5) return false;
            var origin = child.TranslatePoint(new Point(0, 0), scroll);
            return origin is { } point && point.Y + child.Bounds.Height > scroll.Padding.Top + .01 &&
                point.Y < scroll.Padding.Top + scroll.Viewport.Height - .01;
        }
    }

    // The host partial connects these hooks. Capture must precede layout invalidation;
    // LayoutUpdated observes the previous stable anchor rather than recapturing new text.
    private void InitializeReadingLayoutTracking()
    {
        VerifyAccess();
        if (_readingLayoutTracking) return;
        _readingLayoutTracking = true;
        LayoutUpdated += ReadingLayoutUpdated;
    }

    private void ClearReadingLayoutTracking()
    {
        VerifyAccess();
        if (_readingLayoutTracking) LayoutUpdated -= ReadingLayoutUpdated;
        _readingLayoutTracking = false;
        ResetReadingLayoutTracking();
    }

    private void ResetReadingLayoutTracking()
    {
        VerifyAccess();
        _stableReadingLayoutAnchor = null;
        _pendingReadingLayoutRestore = null;
        _readingLayoutPending = false;
        _readingLayoutTicket++;
        ResetCoarseReadingAnchor();
    }

    private void ResetCoarseReadingAnchor()
    {
        _pendingCoarseReadingRestore = null;
        _coarseReadingTicket++;
    }

    private void RestoreCoarseReadingAnchor(MarkdownReadingAnchor? anchor)
    {
        VerifyAccess();
        if (!HasPresentationEnvironment || _presenter is null) return;
        _pendingCoarseReadingRestore = new(anchor, Document, new(_presenter), _updates.DocumentGeneration,
            _readingRevision, ++_coarseReadingTicket);
        QueueCoarseReadingRestore();
    }

    private void QueueCoarseReadingRestore()
    {
        if (_coarseReadingQueued) return;
        _coarseReadingQueued = true;
        var owner = new WeakReference<Markdown>(this);
        PostRestore(owner);

        static void PostRestore(WeakReference<Markdown> owner) => MarkdownUiDispatcher.Post(() =>
        { if (owner.TryGetTarget(out var reader)) reader.ApplyPendingCoarseReadingRestore(); }, DispatcherPriority.Loaded);
    }

    private void ApplyPendingCoarseReadingRestore()
    {
        VerifyAccess();
        var request = _pendingCoarseReadingRestore;
        _pendingCoarseReadingRestore = null;
        try
        {
            if (request is null || !IsCurrent(request)) return;
            UpdateLayout();
            if (!IsCurrent(request)) return;
            var restoring = _restoring;
            _restoring = true;
            try
            {
                if (AutoScrollToEnd && _following) _scroll!.ScrollToEnd();
                else if (request.Anchor is { } anchor && _presenter!.AnchorOffset(anchor.Id, anchor.Offset) is { } offset)
                    _scroll!.Offset = new Vector(_scroll.Offset.X, offset);
            }
            finally { _restoring = restoring; }
        }
        finally
        {
            _coarseReadingQueued = false;
            if (_pendingCoarseReadingRestore is not null) QueueCoarseReadingRestore();
        }

        bool IsCurrent(CoarseReadingRestore value) => value.Ticket == _coarseReadingTicket && HasPresentationEnvironment &&
            ReferenceEquals(value.Document, Document) && value.Generation == _updates.DocumentGeneration &&
            value.Presenter.TryGetTarget(out var presenter) && ReferenceEquals(presenter, _presenter) && value.Revision == _readingRevision;
    }

    private void ReadingLayoutScrolled()
    {
        VerifyAccess();
        if (_readingLayoutPending || _readingLayoutRestoring || _restoring) return;
        _stableReadingLayoutAnchor = CaptureNativeReadingLayoutAnchor();
    }

    internal MarkdownReadingLayoutAnchor? CaptureReadingLayoutAnchor()
    {
        VerifyAccess();
        // Inherited resource changes can invalidate TextLayout before the owner sees a
        // property notification. A pending/reflow capture therefore uses stable geometry.
        if (_stableReadingLayoutAnchor is { } stable && ReferenceEquals(stable.Document, Document) &&
            stable.ReadingRevision == _readingRevision && _scroll is { } scroll &&
            Math.Abs(stable.ScrollOffset - scroll.Offset.Y) < .1) return stable;
        return CaptureNativeReadingLayoutAnchor();
    }

    internal void RestoreReadingLayoutAnchor(MarkdownReadingLayoutAnchor? anchor)
    {
        VerifyAccess();
        if (anchor is null || !HasPresentationEnvironment) return;
        _pendingReadingLayoutRestore = new(anchor, Document, new(_presenter!), _updates.DocumentGeneration,
            _readingRevision, ++_readingLayoutTicket);
        _readingLayoutPending = true;
        QueueReadingLayoutRestore();
    }

    private void QueueReadingLayoutRestore()
    {
        if (_readingLayoutQueued) return;
        _readingLayoutQueued = true;
        var owner = new WeakReference<Markdown>(this);
        // The dispatcher holds one weak owner only; repeated resource/document changes
        // replace one pending record and cannot queue closures retaining old snapshots.
        // Layout housekeeping can outlive a caller's async scope. Carrying its AsyncLocal
        // context into another UI turn would retain a disposed request/test context.
        PostRestore(owner);

        static void PostRestore(WeakReference<Markdown> owner) => MarkdownUiDispatcher.Post(() =>
        { if (owner.TryGetTarget(out var reader)) reader.ApplyPendingReadingLayoutRestore(); }, DispatcherPriority.Loaded);
    }

    private void ApplyPendingReadingLayoutRestore()
    {
        VerifyAccess();
        var request = _pendingReadingLayoutRestore;
        _pendingReadingLayoutRestore = null;
        if (request is null) { _readingLayoutQueued = false; _readingLayoutPending = false; return; }
        var anchor = request.Anchor;
        try
        {
            if (!IsCurrent()) return;
            _readingLayoutRestoring = true;
            var position = ResolveReadingLayoutPosition(anchor, request.Document);
            for (var pass = 0; pass < 4 && IsCurrent(); pass++)
            {
                _presenter!.SetViewport(_scroll!.Offset.Y, _scroll.Viewport.Height);
                UpdateLayout();
                if (!IsCurrent()) return;
                if (AutoScrollToEnd && _following)
                {
                    _restoring = true;
                    try { _scroll.ScrollToEnd(); }
                    finally { _restoring = false; }
                    break;
                }
                if (position is not { } logical) break;
                var text = ReadingTextAt(logical);
                if (text is null)
                {
                    if (_presenter.AnchorOffset(anchor.TopBlockId, anchor.BlockOffset) is not { } estimated) break;
                    SetReadingLayoutOffset(estimated);
                    continue;
                }
                var rectangle = text.TextLayout.HitTestTextPosition(logical - text.TextStart);
                var origin = text.TranslatePoint(new Point(text.Padding.Left, text.Padding.Top), _presenter);
                if (origin is null) break;
                var desired = Math.Max(0, origin.Value.Y + rectangle.Y - anchor.GlyphViewportOffset);
                if (Math.Abs(desired - _scroll.Offset.Y) < .1) break;
                var previous = _scroll.Offset.Y;
                SetReadingLayoutOffset(desired);
                if (Math.Abs(previous - _scroll.Offset.Y) < .1) break;
            }
            if (IsCurrent())
            {
                _presenter!.SetViewport(_scroll!.Offset.Y, _scroll.Viewport.Height);
                UpdateLayout();
            }
        }
        finally
        {
            _readingLayoutRestoring = false;
            _readingLayoutQueued = false;
            if (_pendingReadingLayoutRestore is not null)
            {
                _readingLayoutPending = true;
                QueueReadingLayoutRestore();
            }
            else if (request.Ticket == _readingLayoutTicket)
            {
                _readingLayoutPending = false;
                _stableReadingLayoutAnchor = IsCurrent() ? CaptureNativeReadingLayoutAnchor() : null;
            }
        }

        bool IsCurrent() => request.Ticket == _readingLayoutTicket && HasPresentationEnvironment &&
            ReferenceEquals(request.Document, Document) && request.Generation == _updates.DocumentGeneration &&
            request.Presenter.TryGetTarget(out var presenter) && ReferenceEquals(presenter, _presenter) &&
            request.Revision == _readingRevision;
    }

    private void SetReadingLayoutOffset(double offset)
    {
        _restoring = true;
        try { _scroll!.Offset = new Vector(_scroll.Offset.X, offset); }
        finally { _restoring = false; }
    }

    private void ReadingLayoutUpdated(object? sender, EventArgs args)
    {
        if (!HasPresentationEnvironment || _readingLayoutPending || _readingLayoutRestoring) return;
        if (_stableReadingLayoutAnchor is not { } stable || !ReferenceEquals(stable.Document, Document) ||
            stable.ReadingRevision != _readingRevision || Math.Abs(stable.ScrollOffset - _scroll!.Offset.Y) > .1)
        {
            _stableReadingLayoutAnchor = CaptureNativeReadingLayoutAnchor();
            return;
        }
        var text = ReadingTextAt(stable.Position);
        if (text is null)
        {
            // Reflow can move the retained logical anchor outside the currently
            // realized range. The restore first uses its top block estimate to
            // realize that text, then restores the captured glyph offset.
            RestoreReadingLayoutAnchor(stable);
            return;
        }
        if (!stable.TextPresenter.TryGetTarget(out var oldText) || !ReferenceEquals(oldText, text) ||
            !stable.TextLayout.TryGetTarget(out var oldLayout) || !ReferenceEquals(oldLayout, text.TextLayout) ||
            Math.Abs(stable.TextWidth - text.Bounds.Width) > .1 || Math.Abs(stable.FontSize - text.FontSize) > .01)
        {
            RestoreReadingLayoutAnchor(stable);
            return;
        }
        _stableReadingLayoutAnchor = CaptureNativeReadingLayoutAnchor();
    }

    private MarkdownReadingLayoutAnchor? CaptureNativeReadingLayoutAnchor()
    {
        if (!HasPresentationEnvironment || _scroll is null || _presenter is null || Document.Text.Length == 0) return null;
        var viewportTop = _scroll.Offset.Y;
        MarkdownReadingLayoutAnchor? nearest = null;
        // This walks realized helpers only. Native CharacterHit supplies a shaped cluster
        // start; no whole-document StringInfo index or descendant DTO scan is built here.
        foreach (var text in _presenter.GetVisualDescendants().OfType<MarkdownTextPresenter>())
        {
            if (!text.IsMeasureValid || !text.IsArrangeValid || text.TextLength == 0) continue;
            var origin = text.TranslatePoint(new Point(text.Padding.Left, text.Padding.Top), _presenter);
            if (origin is null || origin.Value.Y + text.TextLayout.Height <= viewportTop + .01 ||
                origin.Value.Y >= viewportTop + _scroll.Viewport.Height) continue;
            var hit = text.TextLayout.HitTestPoint(new Point(0, Math.Max(0, viewportTop - origin.Value.Y) + .01));
            var local = Math.Clamp(hit.CharacterHit.FirstCharacterIndex, 0, text.TextLength - 1);
            var position = text.TextStart + local;
            if (position >= Document.Text.Length) continue;
            if (position > 0 && char.IsLowSurrogate(Document.Text[position]) && char.IsHighSurrogate(Document.Text[position - 1])) position--;
            var rectangle = text.TextLayout.HitTestTextPosition(position - text.TextStart);
            var offset = origin.Value.Y + rectangle.Y - viewportTop;
            var parents = text.GetVisualAncestors().OfType<MarkdownBlockPresenter>().ToArray();
            var blockId = text.NodeId != 0 ? text.NodeId : parents.FirstOrDefault()?.NodeId ?? 0;
            var topId = text.TopBlockId != 0 ? text.TopBlockId : parents.LastOrDefault()?.NodeId ?? 0;
            var top = text.GetVisualAncestors().Prepend(text).OfType<Control>()
                .FirstOrDefault(control => ReferenceEquals(control.GetVisualParent(), _presenter));
            if (top is null || blockId == 0 || topId == 0) continue;
            var topOrigin = top.TranslatePoint(new Point(0, -top.Margin.Top), _presenter);
            if (topOrigin is null) continue;
            var candidate = new MarkdownReadingLayoutAnchor(Document, blockId, topId, position,
                offset, viewportTop - topOrigin.Value.Y, viewportTop, _readingRevision,
                new(text), new(text.TextLayout), text.Bounds.Width, text.FontSize);
            if (nearest is null || offset < nearest.GlyphViewportOffset) nearest = candidate;
        }
        return nearest;
    }

    private MarkdownTextPresenter? ReadingTextAt(int position) => _presenter?.GetVisualDescendants()
        .OfType<MarkdownTextPresenter>().FirstOrDefault(text => position >= text.TextStart && position < text.TextStart + text.TextLength);

    private Rect? ReadingPositionBounds(int position)
    {
        VerifyAccess();
        if (_presenter is null) return null;
        var text = ReadingTextAt(position) ?? _presenter.GetVisualDescendants().OfType<MarkdownTextPresenter>()
            .LastOrDefault(text => position == text.TextStart + text.TextLength);
        if (text is null) return null;
        var rectangle = text.TextLayout.HitTestTextPosition(Math.Clamp(position - text.TextStart, 0, text.TextLength));
        var origin = text.TranslatePoint(new Point(text.Padding.Left, text.Padding.Top), _presenter);
        return origin is { } point ? rectangle.Translate(new Vector(point.X, point.Y)) : null;
    }

    private static int? ResolveReadingLayoutPosition(MarkdownReadingLayoutAnchor anchor, MarkdownDocumentSnapshot document)
    {
        if (ReferenceEquals(anchor.Document, document)) return anchor.Position;
        // A converter/configuration may rebuild node identities while retaining the
        // exact copy projection. The captured logical position is then still unambiguous.
        if (anchor.Document.Text == document.Text) return anchor.Position;
        // Identity lookups occur only for a changed snapshot, never on every stable layout.
        var previous = anchor.Document.FindBlock(anchor.BlockId);
        var current = document.FindBlock(anchor.BlockId);
        if (previous is null || current is null || current.TextLength == 0) return null;
        var local = anchor.Position - previous.TextStart;
        var prefix = 0;
        while (prefix < Math.Min(previous.Text.Length, current.Text.Length) && previous.Text[prefix] == current.Text[prefix]) prefix++;
        if (local > prefix)
        {
            var suffix = 0;
            while (suffix < Math.Min(previous.Text.Length, current.Text.Length) - prefix && previous.Text[^(suffix + 1)] == current.Text[^(suffix + 1)]) suffix++;
            local = local >= previous.Text.Length - suffix ? local + current.Text.Length - previous.Text.Length : prefix;
        }
        // Normalize only this changed leaf, not the full document and not every layout.
        return current.TextStart + MarkdownTextBoundary.SnapBackward(current.Text, Math.Clamp(local, 0, current.TextLength - 1));
    }
}
