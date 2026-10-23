using Avalonia.Media.TextFormatting;

namespace NT.AvaloniaControl.Markdown;

// One numeric index per presenter; the presenter keeps its weak native-layout key.
// No text, runs, font objects, lines, owner or document are retained here.
internal sealed class MarkdownSelectionLineIndex
{
    private const int MinimumLines = 32;
    private const int MaximumLines = 65_536;
    private readonly double[] _bottoms;
    private readonly int[] _starts;

    private MarkdownSelectionLineIndex(double[] bottoms, int[] starts)
    {
        _bottoms = bottoms;
        _starts = starts;
    }

    internal static MarkdownSelectionLineIndex? TryCreate(TextLayout layout, int textLength)
    {
        var lines = layout.TextLines;
        if (lines.Count < MinimumLines || lines.Count > MaximumLines || textLength <= 0) return null;
        var bottoms = new double[lines.Count];
        var starts = new int[lines.Count + 1];
        var bottom = 0d;
        var previousEnd = 0;
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (line.HasCollapsed || !double.IsFinite(line.Height) || line.Height <= 0 ||
                line.FirstTextSourceIndex != previousEnd || line.Length <= 0 ||
                line.Length > int.MaxValue - previousEnd) return null;
            starts[index] = Math.Min(textLength, line.FirstTextSourceIndex);
            previousEnd += line.Length;
            bottom += line.Height;
            if (!double.IsFinite(bottom)) return null;
            bottoms[index] = bottom;
        }
        // A native terminal EOP may account for one extra source position; it
        // must not enter the document's selection range. Incomplete coverage or
        // non-native vertical positioning remains on the unmodified path.
        if (previousEnd < textLength || (long)previousEnd > (long)textLength + 1 ||
            !double.IsFinite(layout.Height) || Math.Abs(bottom - layout.Height) > .0001) return null;
        starts[^1] = textLength;
        return new(bottoms, starts);
    }

    internal void Intersect(double top, double bottom, ref int start, ref int end)
    {
        if (bottom <= top || bottom <= 0 || top >= _bottoms[^1]) { end = start; return; }
        // The first line has Bottom > viewport.Top; the exclusive last line
        // has Top >= viewport.Bottom. Heights are strictly positive and were
        // accumulated in the same order as native line drawing.
        var first = FirstBottomGreaterThan(top);
        var afterLast = Math.Min(_bottoms.Length, FirstBottomGreaterThanOrEqual(bottom) + 1);
        start = Math.Max(start, _starts[first]);
        end = Math.Min(end, _starts[afterLast]);
        if (end < start) end = start;
    }

    private int FirstBottomGreaterThan(double value)
    {
        var low = 0;
        var high = _bottoms.Length;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (_bottoms[middle] <= value) low = middle + 1;
            else high = middle;
        }
        return low;
    }

    private int FirstBottomGreaterThanOrEqual(double value)
    {
        var low = 0;
        var high = _bottoms.Length;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (_bottoms[middle] < value) low = middle + 1;
            else high = middle;
        }
        return low;
    }
}
