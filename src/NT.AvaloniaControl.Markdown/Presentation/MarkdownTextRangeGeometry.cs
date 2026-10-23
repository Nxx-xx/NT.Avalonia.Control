using Avalonia;
using Avalonia.Media.TextFormatting;

namespace NT.AvaloniaControl.Markdown;

internal static class MarkdownTextRangeGeometry
{
    internal static IEnumerable<Rect> GetBounds(MarkdownTextPresenter text, string document, int start, int length)
    {
        if (length <= 0) return [];
        var layout = text.TextLayout;
        // Avalonia 12.0.3 TextLayout.cs:280-329 advances a range by each run's
        // covered length. Empty LF lines can produce a zero-length next call or
        // incorrect later intersections. The same layout's native per-line API
        // retains all x/height/bidi metrics without that cross-line consumption.
        // Ordinary ranges keep the original complete-layout API unchanged.
        return text.HasEmptyLfLine(layout, document)
            ? GetLineBounds(layout, text, document, start, length)
            : layout.HitTestTextRange(start, length);
    }

    internal static bool ContainsEmptyLfLine(TextLayout layout, MarkdownTextPresenter text, string document)
    {
        if (text.TextStart < 0 || text.TextLength < 0 || text.TextStart > document.Length - text.TextLength) return false;
        foreach (var line in layout.TextLines)
            if (IsEmptyLfLine(line, text, document)) return true;
        return false;
    }

    private static bool IsEmptyLfLine(TextLine line, MarkdownTextPresenter text, string document) =>
        line.Length == 1 && line.FirstTextSourceIndex >= 0 && line.FirstTextSourceIndex < text.TextLength &&
        document[text.TextStart + line.FirstTextSourceIndex] == '\n' && line.WidthIncludingTrailingWhitespace == 0;

    private static IReadOnlyList<Rect> GetLineBounds(TextLayout layout, MarkdownTextPresenter text,
        string document, int start, int length)
    {
        var rectangles = new List<Rect>();
        var end = Math.Min((long)start + length, text.TextLength);
        var y = 0d;
        foreach (var line in layout.TextLines)
        {
            var begin = Math.Max(start, line.FirstTextSourceIndex);
            var finish = Math.Min(end, (long)line.FirstTextSourceIndex + line.Length);
            if (finish > begin && !IsEmptyLfLine(line, text, document))
                foreach (var bounds in line.GetTextBounds(begin, (int)(finish - begin)))
                    rectangles.Add(bounds.Rectangle.WithY(y));
            y += line.Height;
        }
        return rectangles;
    }
}
