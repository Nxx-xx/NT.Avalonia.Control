using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Styling;
using Avalonia.VisualTree;
using EmphasisInline = Markdig.Syntax.Inlines.EmphasisInline;
using NT.AvaloniaControl.Markdown;
using Reader = NT.AvaloniaControl.Markdown.Markdown;

namespace Markdown.Headless.Tests;

public sealed class MarkdownHardLineBreakLayoutTests
{
    [Fact]
    public Task LeadingConsecutiveAndTrailingLfKeepNativeLineRangesWithInfiniteHeight() =>
        HeadlessTestScope.Run(LeadingConsecutiveAndTrailingLfKeepNativeLineRangesWithInfiniteHeightCore);

    private void LeadingConsecutiveAndTrailingLfKeepNativeLineRangesWithInfiniteHeightCore()
    {
        foreach (var source in new[] { "\nfirst", "first\n\nlast", "first\n", "\nfirst\n\nlast\n", "\n", "\n\n" })
        foreach (var (height, spacing) in new[] { (double.NaN, 0d), (27d, 5d) })
        {
            var text = InlineText(source);
            text.LineHeight = height;
            text.LineSpacing = spacing;
            Measure(text, 800);

            Assert.Equal(source, Projection(text));
            AssertNativeLineRanges(source, text, 800);
            Assert.Equal(new[] { 0 }.Concat(source.Select((character, index) => (character, index))
                    .Where(value => value.character == '\n').Select(value => value.index + 1)),
                text.TextLayout.TextLines.Select(line => line.FirstTextSourceIndex));
            AssertFiniteLayout(text);
        }
    }

    [Fact]
    public Task HardBreaksAndSoftWrapReflowBackToTheOriginalNativeRanges() =>
        HeadlessTestScope.Run(HardBreaksAndSoftWrapReflowBackToTheOriginalNativeRangesCore);

    private void HardBreaksAndSoftWrapReflowBackToTheOriginalNativeRangesCore()
    {
        const string source = "alpha beta gamma delta epsilon zeta eta theta\n\nlast paragraph has enough words to wrap at a narrow width\n";
        var text = InlineText(source);
        Measure(text, 800);
        AssertNativeLineRanges(source, text, 800);
        var original = Ranges(text);
        var originalHeight = text.TextLayout.Height;

        Measure(text, 140);
        AssertNativeLineRanges(source, text, 140);
        Assert.True(text.TextLayout.TextLines.Count > original.Length);
        Assert.True(text.TextLayout.Height > originalHeight);
        AssertFiniteLayout(text);

        Measure(text, 800);
        AssertNativeLineRanges(source, text, 800);
        Assert.Equal(original, Ranges(text));
        Assert.Equal(originalHeight, text.TextLayout.Height, 4);
        Assert.Equal(source, Projection(text));
    }

    [Fact]
    public Task NestedSpanStyleChangesAcrossHardBreaksUpdateActualRunsAndKeepTheDocument() =>
        HeadlessTestScope.Run(NestedSpanStyleChangesAcrossHardBreaksUpdateActualRunsAndKeepTheDocumentCore);

    private async Task NestedSpanStyleChangesAcrossHardBreaksUpdateActualRunsAndKeepTheDocumentCore()
    {
        var reader = new Reader { Text = "**first *大😀e\u0301  \nsecond* end**  \ntail" };
        var window = Show(reader);
        try
        {
            await Updated(reader, window);
            reader.SelectAll();
            const string expected = "first 大😀e\u0301\nsecond end\ntail";
            Assert.Equal(expected, reader.SelectedText);
            var document = reader.Document;
            var selection = reader.Selection;
            var text = Assert.Single(reader.GetVisualDescendants().OfType<MarkdownTextPresenter>());
            var strong = text.Inlines!.OfType<MarkdownSpan>().Single(span => span.Classes.Contains("strong"));
            var nested = strong.Inlines.OfType<MarkdownSpan>().Single(span => span.Classes.Contains("emphasis"));
            var originalSize = nested.FontSize;
            var originalHeight = text.TextLayout.Height;
            var originalRanges = Ranges(text);
            Assert.Equal(3, originalRanges.Length);
            var tailHeight = text.TextLayout.TextLines[2].Height;
            var style = new Style(selector => selector.OfType<MarkdownSpan>().Class("strong")
                .Descendant().OfType<MarkdownSpan>().Class("emphasis"));
            style.Setters.Add(new Setter(TextElement.FontSizeProperty, 43d));
            style.Setters.Add(new Setter(TextElement.ForegroundProperty, Brushes.HotPink));

            reader.Styles.Add(style); window.UpdateLayout();
            Assert.Equal(43d, nested.FontSize);
            Assert.True(text.TextLayout.Height > originalHeight);
            Assert.Contains(ShapedRuns(text), run => run.Properties.FontRenderingEmSize == 43d &&
                run.Properties.ForegroundBrush is ISolidColorBrush brush && brush.Color == Colors.HotPink);
            Assert.Equal(originalRanges, Ranges(text));
            Assert.Equal(tailHeight, text.TextLayout.TextLines[2].Height, 4);

            nested.FontSize = 55d; window.UpdateLayout();
            Assert.Contains(ShapedRuns(text), run => run.Properties.FontRenderingEmSize == 55d);
            nested.ClearValue(TextElement.FontSizeProperty);
            reader.Styles.Remove(style); window.UpdateLayout();
            Assert.Equal(originalSize, nested.FontSize);
            Assert.Equal(originalHeight, text.TextLayout.Height, 4);
            Assert.Equal(originalRanges, Ranges(text));
            Assert.Same(document, reader.Document);
            Assert.Equal(selection, reader.Selection);
            Assert.Equal(expected, Projection(text));
            Assert.Same(strong, text.Inlines!.OfType<MarkdownSpan>().Single(span => span.Classes.Contains("strong")));
            Assert.Same(nested, strong.Inlines.OfType<MarkdownSpan>().Single(span => span.Classes.Contains("emphasis")));
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task UnicodeAfterHardBreakKeepsGlobalPointerKeyboardCopyAndLinkPositions() =>
        HeadlessTestScope.Run(UnicodeAfterHardBreakKeepsGlobalPointerKeyboardCopyAndLinkPositionsCore);

    private async Task UnicodeAfterHardBreakKeepsGlobalPointerKeyboardCopyAndLinkPositionsCore()
    {
        var reader = new Reader { Text = "prefix\n\n中😀e\u0301  \n[👩‍💻](https://example.test/hardline) end\n\nlast" };
        var window = Show(reader);
        string? copied = null;
        reader.CopyingToClipboard += (_, args) => { copied = args.Text; args.Cancel = true; };
        var activated = new List<string>();
        reader.LinkClicked += (_, args) => { activated.Add(args.Url); args.Handled = true; };
        try
        {
            await Updated(reader, window);
            const string expected = "prefix\n中😀e\u0301\n👩‍💻 end\nlast";
            Assert.Equal(expected, reader.Document.Text);
            var text = reader.GetVisualDescendants().OfType<MarkdownTextPresenter>()
                .Single(view => view.TextStart == "prefix\n".Length);
            Assert.Equal(new[] { 0, "中😀e\u0301\n".Length },
                text.TextLayout.TextLines.Select(line => line.FirstTextSourceIndex));
            var emoji = expected.IndexOf("😀", StringComparison.Ordinal);
            var linkedEmoji = expected.IndexOf("👩‍💻", StringComparison.Ordinal);
            foreach (var position in new[] { emoji, emoji + "😀".Length, linkedEmoji, linkedEmoji + "👩‍💻 ".Length })
            {
                var point = PointAt(text, window, position - text.TextStart);
                window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left);
                Assert.Equal(new MarkdownSelection(position, position), reader.Selection);
            }

            Assert.True(reader.Focus()); reader.SetSelection(emoji, emoji);
            Press(window, Key.Right, RawInputModifiers.Shift);
            Assert.Equal("😀", reader.SelectedText);
            Press(window, Key.Right, RawInputModifiers.Shift);
            Assert.Equal("😀e\u0301", reader.SelectedText);
            Press(window, Key.Right, RawInputModifiers.Shift);
            Assert.Equal("😀e\u0301\n", reader.SelectedText);
            Press(window, Key.Right, RawInputModifiers.Shift);
            Assert.Equal("😀e\u0301\n👩‍💻", reader.SelectedText);
            Assert.Equal(reader.Selection, reader.Selection.Normalize(expected));

            var start = PointAt(text, window, emoji - text.TextStart);
            var end = PointAt(text, window, linkedEmoji + "👩‍💻".Length - text.TextStart);
            window.MouseDown(start, MouseButton.Left); window.MouseMove(end, RawInputModifiers.LeftMouseButton);
            window.MouseUp(end, MouseButton.Left);
            Assert.Equal("😀e\u0301\n👩‍💻", reader.SelectedText);
            reader.SelectAll();
            Assert.Equal(expected, reader.SelectedText);
            Assert.Equal(MarkdownCopyResult.Cancelled, await reader.CopyAsync());
            Assert.Equal(expected, copied);

            activated.Clear();
            reader.SelectionEnabled = false;
            var linkPoint = PointAt(text, window, linkedEmoji - text.TextStart);
            window.MouseDown(linkPoint, MouseButton.Left); window.MouseUp(linkPoint, MouseButton.Left);
            Assert.Equal(new[] { "https://example.test/hardline" }, activated);
            Assert.Empty(reader.SelectedText);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task CustomCrLfProjectionAndNativeLineBreaksKeepOneLogicalPositionAcrossReaderReuse() =>
        HeadlessTestScope.Run(CustomCrLfProjectionAndNativeLineBreaksKeepOneLogicalPositionAcrossReaderReuseCore);

    private async Task CustomCrLfProjectionAndNativeLineBreaksKeepOneLogicalPositionAcrossReaderReuseCore()
    {
        const string replacement = "left\r\n\r\n😀e\u0301\r\n";
        const string normalized = "left\n\n😀e\u0301\n";
        var reader = new Reader();
        reader.ConfigureParser(new() { Converters = [new MarkdownNodeConverterRegistration<EmphasisInline>(_ => new("host-lines", replacement))] });
        reader.NodeRenderers = new(inlines: [new("host-lines", _ => new Span
        {
            Inlines = { new Run("left"), new LineBreak(), new LineBreak(), new Run("😀e\u0301"), new LineBreak() }
        })]);
        var window = Show(reader);
        var doubleTap = Application.Current!.PlatformSettings!.GetDoubleTapSize(PointerType.Mouse);
        var step = Math.Max(32d, doubleTap.Height + 1);
        Point? previousPoint = null;
        try
        {
            for (var cycle = 0; cycle < 4; cycle++)
            {
                var prefix = $"before {new string('x', cycle * 8)} {cycle} ";
                var suffix = $" after {cycle}";
                reader.Margin = new Thickness(0, cycle * step, 0, 0);
                reader.Text = prefix + "*custom*" + suffix;
                await Updated(reader, window);
                var expected = prefix + normalized + suffix;
                Assert.Equal(expected, reader.Document.Text);
                Assert.DoesNotContain('\r', reader.Document.Text);
                Assert.Empty(reader.RenderingDiagnostics);
                var text = Assert.Single(reader.GetVisualDescendants().OfType<MarkdownTextPresenter>());
                var mapped = text.Inlines!.OfType<MarkdownSpan>().Single(span => span.Classes.Contains("custom"));
                Assert.Equal(prefix.Length, mapped.TextStart);
                Assert.Equal(normalized.Length, mapped.TextLength);
                var rendered = Assert.IsType<Span>(Assert.Single(mapped.Inlines));
                Assert.Equal(3, rendered.Inlines.OfType<LineBreak>().Count());
                Assert.Equal(new[] { 0, prefix.Length + 5, prefix.Length + 6, prefix.Length + normalized.Length },
                    text.TextLayout.TextLines.Select(line => line.FirstTextSourceIndex));
                var emoji = expected.IndexOf("😀", StringComparison.Ordinal);
                var point = PointAt(text, window, emoji);
                if (previousPoint is { } previous)
                    Assert.True(Math.Abs(point.Y - previous.Y) > doubleTap.Height);
                previousPoint = point;
                window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left);
                Assert.Equal(new MarkdownSelection(emoji, emoji), reader.Selection);
                Assert.True(reader.Focus());
                Press(window, Key.Right, RawInputModifiers.Shift);
                Assert.Equal("😀", reader.SelectedText);
                reader.SelectAll(); Assert.Equal(expected, reader.SelectedText);

                reader.Text = string.Empty; await Updated(reader, window);
                Assert.Empty(reader.Document.Text);
                Assert.Empty(reader.GetVisualDescendants().OfType<MarkdownTextPresenter>());
                Assert.Equal(0, reader.DocumentPresenter!.RealizedBlockCount);
            }
        }
        finally { window.Close(); }
    }

    private static MarkdownTextPresenter InlineText(string source)
    {
        var text = new MarkdownTextPresenter { TextWrapping = TextWrapping.Wrap, FontSize = 16 };
        var outer = new MarkdownSpan();
        outer.Inlines.Add(new MarkdownRun(source)); text.Inlines!.Add(outer);
        return text;
    }

    private static TextBlock PlainText(string source) => new() { Text = source, TextWrapping = TextWrapping.Wrap, FontSize = 16 };
    private static void Measure(TextBlock text, double width)
    {
        text.Measure(new Size(width, double.PositiveInfinity));
        text.Arrange(new Rect(0, 0, width, text.DesiredSize.Height));
    }
    private static (int Start, int Length)[] Ranges(TextBlock text) => text.TextLayout.TextLines
        .Select(line => (line.FirstTextSourceIndex, line.Length)).ToArray();
    private static void AssertNativeLineRanges(string source, TextBlock text, double width)
    {
        // Native TextBlock also stalls on leading/newline-only input in this version.
        // Each newline-free segment is an independent native wrapping/metrics oracle;
        // its terminal EOP has the same one-unit source length as the intervening LF.
        var ranges = new List<(int Start, int Length)>();
        var lineHeights = new List<double>();
        var position = 0;
        foreach (var piece in source.Split('\n', StringSplitOptions.None))
        {
            var reference = PlainText(piece);
            reference.LineHeight = text.LineHeight;
            reference.LineSpacing = text.LineSpacing;
            Measure(reference, width);
            ranges.AddRange(reference.TextLayout.TextLines.Select(line =>
                (position + line.FirstTextSourceIndex, line.Length)));
            lineHeights.AddRange(reference.TextLayout.TextLines.Select(line => line.Height));
            position += piece.Length + 1;
        }
        Assert.Equal(ranges, Ranges(text));
        Assert.Equal(lineHeights.Count, text.TextLayout.TextLines.Count);
        for (var index = 0; index < lineHeights.Count; index++)
            Assert.Equal(lineHeights[index], text.TextLayout.TextLines[index].Height, 4);
        Assert.Equal(lineHeights.Sum(), text.TextLayout.Height, 4);
    }
    private static void AssertFiniteLayout(TextBlock text)
    {
        Assert.True(double.IsFinite(text.DesiredSize.Width) && double.IsFinite(text.DesiredSize.Height));
        Assert.True(text.TextLayout.Height > 0 && double.IsFinite(text.TextLayout.Height));
        Assert.All(text.TextLayout.TextLines, line => Assert.True(line.Height > 0 && double.IsFinite(line.Height)));
    }
    private static IEnumerable<ShapedTextRun> ShapedRuns(TextBlock text) =>
        text.TextLayout.TextLines.SelectMany(line => line.TextRuns).OfType<ShapedTextRun>();
    private static string Projection(TextBlock text) => text.Inlines is { Count: > 0 } inlines
        ? string.Concat(inlines.Select(Projection)) : text.Text ?? string.Empty;
    private static string Projection(Inline inline) => inline switch
    {
        Run run => run.Text ?? string.Empty,
        Span span => string.Concat(span.Inlines.Select(Projection)),
        LineBreak => "\n",
        _ => throw new InvalidOperationException("Unexpected inline in the fixed text fixture.")
    };
    private static Window Show(Control content)
    {
        var window = new Window { Width = 850, Height = 650, Content = content };
        window.Show(); return window;
    }
    private static async Task Updated(Reader reader, Window window)
    {
        await reader.WhenUpdatedAsync().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        window.UpdateLayout();
    }
    private static Point PointAt(MarkdownTextPresenter text, Window window, int localPosition)
    {
        var rectangle = text.TextLayout.HitTestTextPosition(localPosition);
        return text.TranslatePoint(new Point(rectangle.X + text.Padding.Left + .1,
            rectangle.Y + rectangle.Height / 2 + text.Padding.Top), window)!.Value;
    }
    private static void Press(Window window, Key key, RawInputModifiers modifiers)
    {
        window.KeyPress(key, modifiers, PhysicalKey.None, null);
        window.KeyRelease(key, modifiers, PhysicalKey.None, null); window.UpdateLayout();
    }
}
