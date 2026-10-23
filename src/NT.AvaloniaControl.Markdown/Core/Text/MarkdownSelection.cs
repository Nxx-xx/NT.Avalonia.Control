using System.Globalization;
using System.Runtime.CompilerServices;

namespace NT.AvaloniaControl.Markdown;

/// <summary>A continuous logical selection, independent of realized visuals.</summary>
public readonly record struct MarkdownSelection(int Anchor, int Active)
{
    public int Start => Math.Min(Anchor, Active);
    public int End => Math.Max(Anchor, Active);
    public int Length => End - Start;
    public bool IsEmpty => Anchor == Active;

    public MarkdownSelection Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (IsEmpty)
        {
            var caret = MarkdownTextBoundary.SnapBackward(text, Anchor);
            return new(caret, caret);
        }
        var start = MarkdownTextBoundary.SnapBackward(text, Start);
        var end = MarkdownTextBoundary.SnapForward(text, End);
        return Anchor <= Active ? new(start, end) : new(end, start);
    }

    public string GetText(MarkdownDocumentSnapshot document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var normalized = Normalize(document.Text);
        return document.Text.Substring(normalized.Start, normalized.Length);
    }
}

/// <summary>Extended grapheme boundaries, including combining marks and emoji ZWJ sequences.</summary>
public static class MarkdownTextBoundary
{
    private static readonly ConditionalWeakTable<string, int[]> Boundaries = new();
    private static int[] GetBoundaries(string text) => Boundaries.GetValue(text, StringInfo.ParseCombiningCharacters);

    public static int Previous(string text, int position)
    {
        position = Math.Clamp(position, 0, text.Length);
        if (position == 0) return 0;
        var starts = GetBoundaries(text);
        var index = Array.BinarySearch(starts, position);
        index = index >= 0 ? index - 1 : ~index - 1;
        return index >= 0 ? starts[index] : 0;
    }

    public static int Next(string text, int position)
    {
        position = Math.Clamp(position, 0, text.Length);
        if (position == text.Length) return position;
        var starts = GetBoundaries(text);
        var index = Array.BinarySearch(starts, position);
        index = index >= 0 ? index + 1 : ~index;
        return index < starts.Length ? starts[index] : text.Length;
    }

    public static int SnapBackward(string text, int position)
    {
        position = Math.Clamp(position, 0, text.Length);
        if (position == 0 || position == text.Length) return position;
        var starts = GetBoundaries(text);
        var index = Array.BinarySearch(starts, position);
        return index >= 0 ? position : starts[Math.Max(0, ~index - 1)];
    }

    public static int SnapForward(string text, int position)
    {
        position = Math.Clamp(position, 0, text.Length);
        if (position == 0 || position == text.Length) return position;
        var starts = GetBoundaries(text);
        var index = Array.BinarySearch(starts, position);
        return index >= 0 ? position : ~index < starts.Length ? starts[~index] : text.Length;
    }
}
