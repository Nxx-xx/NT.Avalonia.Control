using System.Text;
using Avalonia.Controls.Documents;
using Avalonia.Media.TextFormatting;

namespace NT.AvaloniaControl.Markdown;

// Wrapped native shaping may stall on newline-only plain runs.
// Explicit line markers keep their UTF-16 length; the base TextBlock keeps every layout option.
internal static class MarkdownHardBreakRuns
{
    // Plain public Text retains its original UTF-16 positions. Unlike a mapped
    // inline projection, CRLF must remain a two-position line break here.
    internal static IReadOnlyList<TextRun> CreatePlain(string text, TextRunProperties properties)
    {
        var runs = new List<TextRun>();
        var rest = text.AsMemory();
        while (!rest.IsEmpty)
        {
            var newline = rest.Span.IndexOfAny('\r', '\n');
            if (newline < 0) { runs.Add(new TextCharacters(rest, properties)); break; }
            if (newline > 0) runs.Add(new TextCharacters(rest[..newline], properties));
            var length = rest.Span[newline] == '\r' && newline + 1 < rest.Length && rest.Span[newline + 1] == '\n' ? 2 : 1;
            runs.Add(new TextEndOfLine(length));
            rest = rest[(newline + length)..];
        }
        return runs;
    }

    internal static bool TryCreate(IReadOnlyList<TextRun> runs, InlineCollection? inlines, out IReadOnlyList<TextRun> result)
    {
        result = runs;
        var hasBreak = false;
        var hasCr = false;
        foreach (var run in runs)
        {
            // Includes already-adapted markers on Arrange, embedded controls and future run types.
            if (run is not TextCharacters characters) return false;
            hasBreak |= characters.Text.Span.IndexOfAny('\r', '\n') >= 0;
            hasCr |= characters.Text.Span.IndexOf('\r') >= 0;
        }
        if (!hasBreak) return false;
        IReadOnlyList<TextRun> normalized = runs;
        if (hasCr)
        {
            var converted = new List<TextRun>(runs.Count);
            var text = new StringBuilder();
            foreach (TextCharacters run in runs)
            {
                var memory = run.Text;
                if (memory.Span.IndexOf('\r') >= 0)
                    memory = memory.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').AsMemory();
                converted.Add(new TextCharacters(memory, run.Properties));
                text.Append(memory.Span);
            }
            // LineBreak builds Environment.NewLine, but its logical projection is LF. Raw CR
            // inside a Run retains the native path unless this exact projection remains valid.
            var projection = new StringBuilder();
            if (inlines is null || !AppendProjection(inlines, projection) || projection.ToString() != text.ToString()) return false;
            normalized = converted;
        }
        var adapted = new List<TextRun>(normalized.Count);
        foreach (TextCharacters run in normalized)
        {
            var rest = run.Text;
            while (rest.Length > 0)
            {
                var newline = rest.Span.IndexOf('\n');
                if (newline < 0) { adapted.Add(new TextCharacters(rest, run.Properties)); break; }
                if (newline > 0) adapted.Add(new TextCharacters(rest[..newline], run.Properties));
                adapted.Add(new TextEndOfLine(1));
                rest = rest[(newline + 1)..];
            }
        }
        result = adapted;
        return true;
    }

    private static bool AppendProjection(IEnumerable<Inline> inlines, StringBuilder target)
    {
        foreach (var inline in inlines)
            switch (inline)
            {
                case Run run: target.Append(run.Text); break;
                case Span span: if (!AppendProjection(span.Inlines, target)) return false; break;
                case LineBreak: target.Append('\n'); break;
                default: return false;
            }
        return true;
    }
}
