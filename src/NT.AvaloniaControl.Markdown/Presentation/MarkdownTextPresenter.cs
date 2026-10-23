using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.Markdown;

public class MarkdownTextPresenter : TextBlock
{
    private readonly MarkdownTextRunPropertiesCache _propertiesCache = new();
    private WeakReference<IReadOnlyList<TextRun>>? _lastLayoutRuns;
    private MarkdownSelectionLineIndex? _selectionLineIndex;
    private WeakReference<TextLayout>? _selectionIndexAttempt;
    private int _selectionIndexTextLength;
    private WeakReference<TextLayout>? _rangeGeometryLayout;
    private WeakReference<string>? _rangeGeometryDocument;
    private int _rangeGeometryStart;
    private int _rangeGeometryLength;
    private bool _rangeGeometryHasEmptyLf;
    private Rect _effectiveViewport;
    private bool _hasEffectiveViewport;
    private bool _observingEffectiveViewport;
    private bool _boundedSelectionDrawn;
    private Markdown? _owner;
    internal Markdown? Owner
    {
        get => _owner;
        set
        {
            if (ReferenceEquals(_owner, value)) return;
            StopObservingEffectiveViewport();
            _propertiesCache.Clear();
            ClearSelectionGeometry();
            _hasEffectiveViewport = false;
            _owner = value;
        }
    }
    internal long NodeId { get; set; }
    internal long TopBlockId { get; set; }
    public int TextStart { get; internal set; }
    public int TextLength { get; internal set; }

    protected override TextLayout CreateTextLayout(string? text)
    {
        // Native Measure creates a fresh run list. Arrange deliberately keeps that
        // list and its resolved inline properties, while creating fresh defaults.
        var reusedRuns = _textRuns is { } previousRuns && _lastLayoutRuns is not null &&
            _lastLayoutRuns.TryGetTarget(out var rememberedRuns) && ReferenceEquals(previousRuns, rememberedRuns);
        if (_textRuns is { } runs && MarkdownHardBreakRuns.TryCreate(runs, Inlines, out var adapted))
            _textRuns = adapted;

        try
        {
            if (_textRuns is null && GetType() == typeof(MarkdownTextPresenter) &&
                TextWrapping != TextWrapping.NoWrap && text is { Length: > 0 } plain &&
                plain.AsSpan().IndexOfAny('\r', '\n') >= 0)
            {
                // Native plain TextCharacters containing empty hard lines can
                // stall wrapped shaping. Keep Text/Inlines unchanged and lend
                // this Create call explicit line markers only. Base preserves
                // every native paragraph parameter, including LineSpacing's
                // internal setter. Never keep these fresh properties for Arrange.
                var originalRuns = _textRuns;
                var plainTypeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
                var properties = new GenericTextRunProperties(plainTypeface, FontSize,
                    TextDecorations, Foreground, fontFeatures: FontFeatures);
                try
                {
                    _textRuns = MarkdownHardBreakRuns.CreatePlain(plain, properties);
                    return base.CreateTextLayout(text);
                }
                finally { _textRuns = originalRuns; }
            }
            // Only a fresh list built from these exact native inline types is
            // eligible. Existing lists must retain native Arrange font resolution;
            // unknown inline/property behavior and inaccessible LineSpacing use base.
            if (reusedRuns || _textRuns is null || GetType() != typeof(MarkdownTextPresenter) ||
                LineSpacing != 0 || !KnownInlineTree(Inlines) || !KnownRuns(_textRuns))
                return base.CreateTextLayout(text);

            _propertiesCache.BeginRequest(FontManager.Current, Owner?.Document);
            // Native constructor arguments copied from Avalonia 12.0.3 TextBlock.cs
            // (21e816874efa142bf9903b51fd323d1f35e56ab3), lines 657-693.
            var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
            var defaultProperties = _propertiesCache.GetOrAdd(new GenericTextRunProperties(
                typeface, FontSize, TextDecorations, Foreground, fontFeatures: FontFeatures));
            var paragraphProperties = new GenericTextParagraphProperties(
                FlowDirection, IsMeasureValid ? TextAlignment : TextAlignment.Left, true, false,
                defaultProperties, TextWrapping, LineHeight, 0, LetterSpacing);

            ITextSource textSource;
            if (_textRuns is not null)
            {
                _textRuns = _propertiesCache.ReuseInlineProperties(_textRuns);
                textSource = new InlinesTextSource(_textRuns);
            }
            else
            {
                textSource = new SimpleTextSource(text ?? "", defaultProperties);
            }

            var maxSize = GetMaxSizeFromConstraint();
            return new TextLayout(textSource, paragraphProperties, TextTrimming,
                maxSize.Width, maxSize.Height, MaxLines);
        }
        finally
        {
            _propertiesCache.EndRequest();
            _lastLayoutRuns = _textRuns is { } finalRuns
                ? new WeakReference<IReadOnlyList<TextRun>>(finalRuns) : null;
        }
    }

    private static bool KnownInlineTree(InlineCollection? inlines, int depth = 0)
    {
        if (inlines is null) return true;
        // Deep public trees remain on the native path without growing this guard's stack.
        if (depth > 64) return false;
        foreach (var inline in inlines)
        {
            var type = inline.GetType();
            if (type == typeof(Run) || type == typeof(MarkdownRun) || type == typeof(LineBreak)) continue;
            if (type != typeof(Span) && type != typeof(MarkdownSpan)) return false;
            if (!KnownInlineTree(((Span)inline).Inlines, depth + 1)) return false;
        }
        return true;
    }

    private static bool KnownRuns(IReadOnlyList<TextRun> runs)
    {
        foreach (var run in runs)
        {
            if (run.GetType() == typeof(TextEndOfLine)) continue;
            if (run.GetType() != typeof(TextCharacters) ||
                run.Properties?.GetType() != typeof(GenericTextRunProperties)) return false;
        }
        return true;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var desired = base.MeasureOverride(availableSize);
        // Base has already created its native layout. Short blocks (including
        // table cells) never register as effective-viewport listeners, nor does
        // this ask an empty/short TextBlock to create an additional layout.
        if (TextLength >= 31 && Owner is not null && IsViewportCandidate(TextLayout))
        {
            if (!_observingEffectiveViewport)
            {
                EffectiveViewportChanged += SelectionViewportChanged;
                _observingEffectiveViewport = true;
            }
        }
        else StopObservingEffectiveViewport();
        return desired;
    }

    private bool IsViewportCandidate(TextLayout layout) =>
        layout.TextLines.Count is >= 32 and <= 65_536 &&
        GetType() == typeof(MarkdownTextPresenter) && LineSpacing == 0 && MaxLines == 0 &&
        ReferenceEquals(TextTrimming, Avalonia.Media.TextTrimming.None) &&
        Owner is { } owner && owner.NodeRenderers.Blocks.IsEmpty && owner.NodeRenderers.Inlines.IsEmpty &&
        KnownInlineTree(Inlines) && RenderTransform is null &&
        !this.GetVisualAncestors().Any(ancestor => ancestor.RenderTransform is not null);

    private void SelectionViewportChanged(object? sender, EffectiveViewportChangedEventArgs args)
    {
        if (_hasEffectiveViewport && _effectiveViewport == args.EffectiveViewport) return;
        var firstViewport = !_hasEffectiveViewport;
        _effectiveViewport = args.EffectiveViewport;
        _hasEffectiveViewport = true;
        // A complete native selection already covers every viewport. Only a
        // previously bounded drawing needs repainting when its viewport moves;
        // cold layout with no selection must not create extra visual work.
        if (_boundedSelectionDrawn || firstViewport && Owner is { } owner &&
            owner.Selection.Start < TextStart + TextLength && owner.Selection.End > TextStart)
            InvalidateVisual();
    }

    private void StopObservingEffectiveViewport()
    {
        if (_observingEffectiveViewport) EffectiveViewportChanged -= SelectionViewportChanged;
        _observingEffectiveViewport = false;
        _hasEffectiveViewport = false;
        _boundedSelectionDrawn = false;
    }

    protected override void OnDetachedFromLogicalTree(Avalonia.LogicalTree.LogicalTreeAttachmentEventArgs e)
    {
        _propertiesCache.Clear();
        StopObservingEffectiveViewport();
        ClearSelectionGeometry();
        _hasEffectiveViewport = false;
        base.OnDetachedFromLogicalTree(e);
    }

    protected override void OnMeasureInvalidated()
    {
        ClearSelectionGeometry();
        base.OnMeasureInvalidated();
    }

    protected override void RenderTextLayout(DrawingContext context, Point origin)
    {
        _boundedSelectionDrawn = false;
        if (Owner is { } owner)
        {
            var selection = owner.Selection;
            var start = Math.Clamp(selection.Start - TextStart, 0, TextLength);
            var end = Math.Clamp(selection.End - TextStart, 0, TextLength);
            if (end > start)
            {
                var layout = TextLayout;
                var bounded = TryBoundSelection(layout, origin, ref start, ref end, out var viewport);
                _boundedSelectionDrawn = bounded;
                if (end > start)
                    foreach (var rect in MarkdownTextRangeGeometry.GetBounds(this, owner.Document.Text, start, end - start))
                    {
                        // Preserve complete native boundary-line rectangles. The
                        // existing ancestor clip remains responsible for pixels.
                        if (bounded && (rect.Bottom <= viewport.Top || rect.Top >= viewport.Bottom)) continue;
                        context.FillRectangle(owner.SelectionBrush, rect.Translate(new Vector(origin.X, origin.Y)));
                    }
            }
            if (owner.IsFocused && selection.IsEmpty && selection.Active >= TextStart && selection.Active <= TextStart + TextLength)
            {
                var rect = TextLayout.HitTestTextPosition(selection.Active - TextStart);
                context.FillRectangle(owner.CaretBrush, new Rect(origin.X + rect.X, origin.Y + rect.Y, 1, rect.Height));
            }
        }
        base.RenderTextLayout(context, origin);
    }

    private bool TryBoundSelection(TextLayout layout, Point origin, ref int start, ref int end, out Rect viewport)
    {
        viewport = default;
        // This changes selection geometry generation only, never native shaping,
        // caret/hit testing, or the complete text drawing path. Unknown/custom
        // inline and coordinate behavior retains the original native range.
        if (!_hasEffectiveViewport || !IsMeasureValid || !IsArrangeValid || !IsViewportCandidate(layout) ||
            !double.IsFinite(_effectiveViewport.X) || !double.IsFinite(_effectiveViewport.Y) ||
            !double.IsFinite(_effectiveViewport.Width) || !double.IsFinite(_effectiveViewport.Height) ||
            !double.IsFinite(origin.X) || !double.IsFinite(origin.Y)) return false;

        viewport = _effectiveViewport.Translate(new Vector(-origin.X, -origin.Y));
        // Remember an ineligible layout as well, so repeated selection renders
        // cannot repeatedly allocate an index which then takes the fallback.
        if (_selectionIndexAttempt is null || !_selectionIndexAttempt.TryGetTarget(out var attempted) ||
            !ReferenceEquals(attempted, layout) || _selectionIndexTextLength != TextLength)
        {
            _selectionIndexAttempt = new(layout);
            _selectionIndexTextLength = TextLength;
            _selectionLineIndex = MarkdownSelectionLineIndex.TryCreate(layout, TextLength);
        }
        if (_selectionLineIndex is not { } index) return false;

        index.Intersect(viewport.Top, viewport.Bottom, ref start, ref end);
        return true;
    }

    internal bool HasEmptyLfLine(TextLayout layout, string document)
    {
        if (_rangeGeometryLayout is null || !_rangeGeometryLayout.TryGetTarget(out var previousLayout) ||
            !ReferenceEquals(previousLayout, layout) || _rangeGeometryDocument is null ||
            !_rangeGeometryDocument.TryGetTarget(out var previousDocument) || !ReferenceEquals(previousDocument, document) ||
            _rangeGeometryStart != TextStart || _rangeGeometryLength != TextLength)
        {
            _rangeGeometryHasEmptyLf = MarkdownTextRangeGeometry.ContainsEmptyLfLine(layout, this, document);
            _rangeGeometryLayout = new(layout);
            _rangeGeometryDocument = new(document);
            _rangeGeometryStart = TextStart;
            _rangeGeometryLength = TextLength;
        }
        return _rangeGeometryHasEmptyLf;
    }

    private void ClearSelectionGeometry()
    {
        _selectionLineIndex = null;
        _selectionIndexAttempt = null;
        _rangeGeometryLayout = null;
        _rangeGeometryDocument = null;
    }

    internal int Hit(Point point) => TextStart + Math.Clamp(TextLayout.HitTestPoint(
        new Point(point.X - Padding.Left, point.Y - Padding.Top)).TextPosition, 0, TextLength);
}
