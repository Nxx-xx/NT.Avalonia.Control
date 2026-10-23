using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace NT.AvaloniaControl.Markdown;

// Share equal native properties only within one CreateTextLayout call. Public
// font collections can change between any two calls without changing FontManager
// identity, document identity or declared style, so no resolved face crosses calls.
internal sealed class MarkdownTextRunPropertiesCache
{
    private const int Capacity = 32;
    private readonly List<GenericTextRunProperties> _entries = new(Capacity);

    internal void BeginRequest(FontManager fontManager, object? document)
    {
        // Identity is insufficient to detect public AddFontCollection/append.
        // Keep the internal call signature while making every request fresh.
        Clear();
    }

    internal GenericTextRunProperties GetOrAdd(GenericTextRunProperties properties)
    {
        foreach (var entry in _entries)
        {
            if (SameStyle(entry, properties)) return entry;
        }

        if (_entries.Count == Capacity)
            _entries.RemoveAt(0);

        // Do not let an input object's previous lazy glyph cache enter this scope.
        // Presenter eligibility also excludes surviving native Arrange run lists.
        var retained = new GenericTextRunProperties(
            properties.Typeface,
            properties.FontRenderingEmSize,
            properties.TextDecorations,
            properties.ForegroundBrush,
            properties.BackgroundBrush,
            properties.BaselineAlignment,
            properties.CultureInfo,
            properties.FontFeatures);
        _entries.Add(retained);
        return retained;
    }

    internal IReadOnlyList<TextRun> ReuseInlineProperties(IReadOnlyList<TextRun> runs)
    {
        TextRun[]? rewritten = null;
        for (var index = 0; index < runs.Count; ++index)
        {
            var run = runs[index];
            // Preserve custom run/property behavior, embedded controls, and hard-break markers.
            if (run.GetType() != typeof(TextCharacters) ||
                run.Properties is not GenericTextRunProperties properties ||
                properties.GetType() != typeof(GenericTextRunProperties))
                continue;

            var cached = GetOrAdd(properties);
            if (ReferenceEquals(cached, properties))
                continue;

            if (rewritten is null)
            {
                rewritten = new TextRun[runs.Count];
                for (var copyIndex = 0; copyIndex < runs.Count; ++copyIndex)
                    rewritten[copyIndex] = runs[copyIndex];
            }

            rewritten[index] = new TextCharacters(run.Text, cached);
        }

        return rewritten ?? runs;
    }

    internal void EndRequest()
    {
        Clear();
    }

    internal void Clear()
    {
        _entries.Clear();
    }

    private static bool SameStyle(GenericTextRunProperties left, GenericTextRunProperties right)
    {
        // Family reference identity also preserves composite families' full source identity.
        // Mutable brushes/collections are deliberately compared by identity, never value:
        // the retained properties continue to reference their current live values.
        return ReferenceEquals(left.Typeface.FontFamily, right.Typeface.FontFamily) &&
            left.Typeface.Equals(right.Typeface) &&
            left.FontRenderingEmSize.Equals(right.FontRenderingEmSize) &&
            left.BaselineAlignment == right.BaselineAlignment &&
            ReferenceEquals(left.ForegroundBrush, right.ForegroundBrush) &&
            ReferenceEquals(left.BackgroundBrush, right.BackgroundBrush) &&
            ReferenceEquals(left.TextDecorations, right.TextDecorations) &&
            ReferenceEquals(left.FontFeatures, right.FontFeatures) &&
            ReferenceEquals(left.CultureInfo, right.CultureInfo);
    }
}
