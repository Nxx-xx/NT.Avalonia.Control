using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

public sealed partial class CartesianChart
{
    private const int MaximumInteractionTexts = 32;
    private readonly Dictionary<InteractionTextCacheKey, FormattedText> _interactionTexts = [];
    private readonly LinkedList<InteractionTextCacheKey> _interactionTextKeys = [];
    private readonly Dictionary<FormattedText, InteractionTextDrawing> _interactionTextDrawings = new(ReferenceEqualityComparer.Instance);
    private long _interactionTextBuildCount;
    private long _interactionTextReuseCount;
    private readonly record struct InteractionTextCacheKey(string Text, string Culture, Typeface Typeface,
        double Size, AxisBrushIdentity Brush);
    private sealed class InteractionTextDrawing
    {
        public required IBrush Brush { get; init; }
        public Point Origin { get; set; }
        public DrawingGroup? Drawing { get; set; }
    }

    private void ReleaseInteractionCaches()
    {
        ReleaseResourceKind(ResourceCacheKind.InteractionText);
        _interactionTexts.Clear(); _interactionTextKeys.Clear(); _interactionTextDrawings.Clear();
    }

    private FormattedText InteractionText(string text, bool heading)
    {
        var culture = CultureInfo.CurrentCulture;
        var typeface = new Typeface(FontFamily, FontStyle, heading ? FontWeight.SemiBold : FontWeight);
        var brush = TooltipForeground ?? Foreground ?? Brushes.Black;
        return CachedInteractionText(text, culture, typeface, FontSize, brush);
    }

    private FormattedText CachedInteractionText(string text, CultureInfo culture, Typeface typeface, double size, IBrush brush)
    {
        var key = new InteractionTextCacheKey(text, culture.Name, typeface, size, new(brush));
        if (_attached && _interactionTexts.TryGetValue(key, out var cached))
        {
            _interactionTextReuseCount++;
            TouchResource(ResourceCacheKind.InteractionText, key);
            return cached;
        }
        _interactionTextBuildCount++;
        var formatted = new FormattedText(text, culture, FlowDirection.LeftToRight, typeface, size, brush);
        if (_attached && TryRetainResource(ResourceCacheKind.InteractionText, key, EstimateTextResource(text), () => RemoveInteractionText(key)))
        {
            while (_interactionTexts.Count >= MaximumInteractionTexts && _interactionTextKeys.First is { } first) EvictResource(ResourceCacheKind.InteractionText, first.Value);
            _interactionTexts.Add(key, formatted); _interactionTextKeys.AddLast(key);
            _interactionTextDrawings.Add(formatted, new() { Brush = brush });
        }
        return formatted;
    }

    private void RemoveInteractionText(InteractionTextCacheKey key)
    {
        if (_interactionTexts.Remove(key, out var text)) _interactionTextDrawings.Remove(text);
        _interactionTextKeys.Remove(key);
    }

    private void DrawInteractionText(DrawingContext drawing, FormattedText formatted, Point origin)
    {
        if (!_interactionTextDrawings.TryGetValue(formatted, out var cached))
        { drawing.DrawText(formatted, origin); return; }
        if (cached.Drawing is null || cached.Origin != origin)
        {
            var group = new DrawingGroup();
            using (var recording = group.Open()) recording.DrawText(formatted, origin);
            // The glyph layout is reusable, while mutable solid/gradient brushes
            // retain their current color, opacity and transform on every replay.
            if (!RestoreGlyphBrushes(group, cached.Brush))
            { drawing.DrawText(formatted, origin); return; }
            cached.Origin = origin; cached.Drawing = group;
        }
        cached.Drawing.Draw(drawing);
    }
}
