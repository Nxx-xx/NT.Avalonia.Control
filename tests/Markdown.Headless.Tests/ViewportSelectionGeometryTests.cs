using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Markdown;
using Reader = NT.AvaloniaControl.Markdown.Markdown;

namespace Markdown.Headless.Tests;

// Preparation only: the bounded-command assertion is expected to fail against
// the existing full-range selection renderer. No product optimization is present.
public sealed class ViewportSelectionGeometryTests
{
    private static readonly Color SelectionColor = Color.FromArgb(231, 201, 17, 92);
    private readonly ITestOutputHelper _output;

    public ViewportSelectionGeometryTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task FullSelectionKeepsNativeVisibleGeometryWhileScrolling(bool rightToLeft) =>
        HeadlessTestScope.Run(async () =>
        {
            var reader = NewReader(LongParagraph(rightToLeft));
            if (rightToLeft) reader.FlowDirection = FlowDirection.RightToLeft;
            var window = Show(reader);
            try
            {
                await Updated(reader, window);
                var text = Text(reader);
                Assert.True(text.TextLayout.TextLines.Count > 80);
                reader.SelectAll();
                var selection = reader.Selection;
                var selected = reader.SelectedText;
                foreach (var fraction in new[] { .0, .31, .76, 1.0 })
                {
                    ScrollTo(reader, window, fraction);
                    AssertNativeVisibleSelection(reader, text);
                    Assert.Equal(selection, reader.Selection);
                    Assert.Equal(selected, reader.SelectedText);
                }
            }
            finally { window.Close(); }
        });

    [Fact]
    public Task PartialReversedAndOutsideSelectionsKeepNativeClusterAndClipGeometry() =>
        HeadlessTestScope.Run(async () =>
        {
            var reader = NewReader(LongParagraph(false));
            var window = Show(reader);
            try
            {
                await Updated(reader, window);
                var text = Text(reader);
                ScrollTo(reader, window, .48);
                var middle = text.TextLayout.TextLines.Count / 2;
                var localStart = text.TextLayout.TextLines[middle - 8].FirstTextSourceIndex;
                var localEnd = text.TextLayout.TextLines[middle + 8].FirstTextSourceIndex;
                reader.SetSelection(text.TextStart + localEnd, text.TextStart + localStart);
                var selection = reader.Selection;
                Assert.False(selection.IsEmpty);
                AssertNativeVisibleSelection(reader, text);
                Assert.Equal(selection, reader.Selection);
                Assert.Equal(reader.Document.Text[selection.Start..selection.End], reader.SelectedText);

                reader.SetSelection(0, "record000".Length);
                AssertNativeVisibleSelection(reader, text);

                var paragraph = reader.Document.Text;
                var emoji = paragraph.IndexOf("😀", StringComparison.Ordinal);
                reader.SetSelection(emoji, emoji + "😀e\u0301".Length);
                ScrollTo(reader, window, 0);
                Assert.Equal("😀e\u0301", reader.SelectedText);
                AssertNativeVisibleSelection(reader, text);
                foreach (var position in new[] { emoji, emoji + 2, emoji + 4 })
                {
                    var local = position - text.TextStart;
                    var rect = text.TextLayout.HitTestTextPosition(local);
                    var hit = text.TextLayout.HitTestPoint(new Point(rect.X + .01, rect.Y + rect.Height / 2));
                    Assert.Equal(local, hit.TextPosition);
                }
            }
            finally { window.Close(); }
        });

    [Fact]
    public Task LongSelectionEmitsOnlyViewportIntersectingCommands() =>
        HeadlessTestScope.Run(async () =>
        {
            var reader = NewReader(LongParagraph(false));
            var window = Show(reader);
            try
            {
                await Updated(reader, window);
                var text = Text(reader);
                reader.SelectAll();
                ScrollTo(reader, window, .48);
                AssertNativeVisibleSelection(reader, text);
                var viewport = Viewport(reader, text);
                _ = SelectionRectangles(text); // Warm the drawing path; this is not a budget sample.
                var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                var commands = SelectionRectangles(text);
                var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                _output.WriteLine($"SELECTION_DRAW_DIAGNOSTIC commands={commands.Length}; currentThreadAllocatedBytes={allocated}; " +
                    $"nativeLines={text.TextLayout.TextLines.Count}; includesCompleteNativeTextDrawing=true; warmupDraws=1; measuredDraws=1");
                Assert.All(commands.Where(rect => rect.Width > 0 && rect.Height > 0), rect =>
                    Assert.True(rect.Bottom > viewport.Top && rect.Top < viewport.Bottom,
                        $"Selection command outside viewport: rect={rect}, viewport={viewport}."));
                reader.SetSelection(0, "record000".Length);
                AssertNativeVisibleSelection(reader, text);
                Assert.DoesNotContain(SelectionRectangles(text), rect => rect.Width > 0 && rect.Height > 0);
            }
            finally { window.Close(); }
        });

    [Fact]
    public Task HardLfRichStylesReflowAndReaderReuseReplaceOnlyLayoutDerivedState() =>
        HeadlessTestScope.Run(async () =>
        {
            var reader = NewReader(LongHardBreaks());
            var window = Show(reader);
            try
            {
                await Updated(reader, window);
                var document = reader.Document;
                var text = Text(reader);
                var strong = text.GetLogicalDescendants().OfType<MarkdownSpan>()
                    .First(span => span.Classes.Contains("strong"));
                reader.SelectAll();
                var selected = reader.SelectedText;
                var initial = text.TextLayout;
                ScrollTo(reader, window, .43);
                AssertNativeVisibleSelection(reader, text);

                window.Width = 330;
                strong.FontSize = 29;
                await Flush(window);
                Assert.NotSame(initial, text.TextLayout);
                Assert.Same(document, reader.Document);
                Assert.Equal(selected, reader.SelectedText);
                Assert.Contains(text.TextLayout.TextLines.SelectMany(line => line.TextRuns),
                    run => run.Properties?.FontRenderingEmSize == 29);
                AssertNativeVisibleSelection(reader, text);

                strong.ClearValue(TextElement.FontSizeProperty);
                window.Width = 580;
                await Flush(window);
                AssertNativeVisibleSelection(reader, text);
                Assert.Equal(selected, reader.SelectedText);

                reader.Text = "replacement 😀e\u0301 אבג";
                await Updated(reader, window);
                reader.SelectAll();
                Assert.Equal(reader.Document.Text, reader.SelectedText);
                AssertNativeVisibleSelection(reader, Text(reader));
                Assert.Empty(reader.RenderingDiagnostics);
            }
            finally { window.Close(); }
        });

    private static Reader NewReader(string source) => new()
    {
        Text = source, FontSize = 16, SelectionBrush = new SolidColorBrush(SelectionColor)
    };

    private static string LongParagraph(bool rightToLeft) => string.Concat(Enumerable.Range(0, 240)
        .Select(index => rightToLeft
            ? $"record{index:D3} אבג العربية 😀e\u0301 👩‍💻 סוף "
            : $"record{index:D3} 中文 😀e\u0301 👩‍💻 alpha beta gamma "));

    private static string LongHardBreaks() => string.Join("  \n", Enumerable.Range(0, 130)
        .Select(index => index % 7 == 0 ? $"**rich{index:D3}** 😀e\u0301 אבג" : $"row{index:D3} 中文 👩‍💻 alpha"));

    private static Window Show(Reader reader)
    {
        var window = new Window { Width = 580, Height = 230, Content = reader };
        window.Show(); return window;
    }

    private static MarkdownTextPresenter Text(Reader reader) =>
        Assert.Single(reader.GetVisualDescendants().OfType<MarkdownTextPresenter>());

    private static ScrollViewer Scroll(Reader reader) => reader.GetVisualDescendants().OfType<ScrollViewer>()
        .Single(scroll => scroll.Name == "PART_ScrollViewer");

    private static void ScrollTo(Reader reader, Window window, double fraction)
    {
        var scroll = Scroll(reader);
        scroll.Offset = new Vector(0, Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height) * fraction);
        window.UpdateLayout();
        ScopedHeadlessInput.RunJobs();
        window.UpdateLayout();
    }

    private static async Task Updated(Reader reader, Window window)
    {
        await reader.WhenUpdatedAsync().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await Flush(window);
    }

    private static async Task Flush(Window window)
    {
        await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout, DispatcherPriority.Loaded);
        await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout, DispatcherPriority.Loaded);
    }

    private static void AssertNativeVisibleSelection(Reader reader, MarkdownTextPresenter text)
    {
        Assert.True(text.IsMeasureValid && text.IsArrangeValid);
        var layout = text.TextLayout;
        var selected = reader.Selection;
        var start = Math.Clamp(selected.Start - text.TextStart, 0, text.TextLength);
        var end = Math.Clamp(selected.End - text.TextStart, 0, text.TextLength);
        var viewport = Viewport(reader, text);
        var expected = end > start ? layout.HitTestTextRange(start, end - start)
            .Select(rect => rect.Translate(new Vector(text.Padding.Left, text.Padding.Top))).ToArray() : [];
        var actual = SelectionRectangles(text);
        Assert.Contains(expected, rect => rect.Width > 0 && rect.Height > 0);

        // Compare the existing complete native range after external clipping. The
        // oracle does not reproduce a line index or the proposed range limiter.
        var clippedExpected = expected.Select(rect => rect.Intersect(viewport))
            .Where(rect => rect.Width > 0 && rect.Height > 0).ToArray();
        var clippedActual = actual.Select(rect => rect.Intersect(viewport))
            .Where(rect => rect.Width > 0 && rect.Height > 0).ToArray();
        Assert.Equal(clippedExpected, clippedActual);

        Assert.Same(layout, text.TextLayout);
        Assert.Equal(selected, reader.Selection);
    }

    private static Rect Viewport(Reader reader, MarkdownTextPresenter text)
    {
        var scroll = Scroll(reader);
        var topLeft = scroll.TranslatePoint(new Point(scroll.Padding.Left, scroll.Padding.Top), text)!.Value;
        return new Rect(topLeft, scroll.Viewport);
    }

    private static Rect[] SelectionRectangles(MarkdownTextPresenter text)
    {
        var drawings = new DrawingGroup();
        using (var context = drawings.Open()) text.Render(context);
        return Flatten(drawings).OfType<GeometryDrawing>()
            .Where(drawing => drawing.Brush is ISolidColorBrush brush && brush.Color == SelectionColor)
            .Select(drawing => drawing.Geometry!.Bounds).ToArray();
    }

    private static IEnumerable<Drawing> Flatten(Drawing drawing)
    {
        yield return drawing;
        if (drawing is DrawingGroup group)
            foreach (var child in group.Children)
                foreach (var item in Flatten(child)) yield return item;
    }
}
