using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Markdig.Syntax;
using NT.AvaloniaControl.Markdown;
using Reader = NT.AvaloniaControl.Markdown.Markdown;

namespace Markdown.Headless.Tests;

public sealed class ViewportSelectionFallbackTests
{
    private static readonly Color SelectionColor = Color.FromArgb(227, 176, 23, 107);

    [Theory]
    [InlineData("spacing")]
    [InlineData("trimming")]
    [InlineData("max-lines")]
    [InlineData("unknown-inline")]
    [InlineData("custom-renderer")]
    [InlineData("transform")]
    public Task UnsupportedGeometryKeepsTheCompleteNativeSelection(string scenario) => HeadlessTestScope.Run(async () =>
    {
        var reader = new Reader
        {
            Text = string.Concat(Enumerable.Range(0, 190).Select(index => $"record{index:D3} 中文 😀e\u0301 אבג alpha beta ")),
            SelectionBrush = new SolidColorBrush(SelectionColor), FontSize = 16
        };
        if (scenario == "custom-renderer")
            reader.NodeRenderers = new(inlines: [new("unused-host-inline", context => context.CreateText())]);
        var window = new Window { Width = 520, Height = 230, Content = reader };
        window.Show();
        try
        {
            await reader.WhenUpdatedAsync(); await Flush(window);
            var text = Assert.Single(reader.GetVisualDescendants().OfType<MarkdownTextPresenter>());
            Assert.True(text.TextLayout.TextLines.Count > 32);
            switch (scenario)
            {
                case "spacing": text.LineSpacing = 4; break;
                case "trimming": text.TextTrimming = TextTrimming.CharacterEllipsis; break;
                case "max-lines": text.MaxLines = 7; break;
                case "unknown-inline":
                    var inlines = text.Inlines!;
                    var original = inlines.ToArray();
                    inlines.Clear();
                    var unknown = new HostSpan();
                    foreach (var inline in original) unknown.Inlines.Add(inline);
                    inlines.Add(unknown);
                    break;
                case "transform": text.RenderTransform = new ScaleTransform(1, 1); break;
            }
            await Flush(window);
            reader.SelectAll();
            var selected = reader.SelectedText;
            var selection = reader.Selection;
            var layout = text.TextLayout;
            var expected = layout.HitTestTextRange(0, text.TextLength)
                .Select(rect => rect.Translate(new Vector(text.Padding.Left, text.Padding.Top))).ToArray();
            Assert.Contains(expected, rect => rect.Width > 0 && rect.Height > 0);
            Assert.Equal(expected, DrawSelection(text));
            Assert.Same(layout, text.TextLayout);
            Assert.Equal(selection, reader.Selection);
            Assert.Equal(selected, reader.SelectedText);
        }
        finally { window.Close(); }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ExplicitNativeLineHeightPaddingAndBlankLfKeepClippedGeometry(bool useExplicitHeight) =>
        HeadlessTestScope.Run(async () =>
        {
            var lines = "\n\n" + string.Join("\n", Enumerable.Range(0, 160)
                .Select(index => index % 6 == 0 ? "" : $"line{index:D3} 中😀e\u0301 אבג")) + "\n\n";
            var reader = new Reader
            {
                Text = "```text\n" + lines + "\n```", FontSize = 16,
                SelectionBrush = new SolidColorBrush(SelectionColor)
            };
            var window = new Window { Width = 520, Height = 230, Content = reader };
            window.Show();
            try
            {
                await reader.WhenUpdatedAsync(); await Flush(window);
                var text = Assert.Single(reader.GetVisualDescendants().OfType<MarkdownTextPresenter>());
                text.Padding = new Thickness(7, 9, 11, 13);
                if (useExplicitHeight) text.LineHeight = 27;
                await Flush(window);
                Assert.True(text.TextLayout.TextLines.Count > 100);
                Assert.InRange(text.TextLength, 1, reader.Document.Text.Length);
                Assert.Equal(text.Text, reader.Document.Text.Substring(text.TextStart, text.TextLength));
                Assert.Contains(text.TextLayout.TextLines, line => line.Length == 1 &&
                    text.TextStart + line.FirstTextSourceIndex < reader.Document.Text.Length &&
                    reader.Document.Text[text.TextStart + line.FirstTextSourceIndex] == '\n');
                var scroll = Scroll(reader);
                var firstContent = reader.Document.Text.IndexOf("line", text.TextStart, StringComparison.Ordinal) - text.TextStart;
                var contentEnd = reader.Document.Text.IndexOf('\n', text.TextStart + firstContent) - text.TextStart;
                foreach (var (anchor, active) in new[]
                    { (0, text.TextLength), (0, 1), (firstContent, contentEnd), (contentEnd, firstContent) })
                {
                    reader.SetSelection(text.TextStart + anchor, text.TextStart + active);
                    var selection = reader.Selection;
                    foreach (var fraction in new[] { 0d, .48, 1d })
                    {
                        scroll.Offset = new Vector(0, Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height) * fraction);
                        await Flush(window);
                        var layout = text.TextLayout;
                        var viewport = new Rect(scroll.TranslatePoint(new Point(scroll.Padding.Left, scroll.Padding.Top), text)!.Value,
                            scroll.Viewport);
                        var expected = NonemptyNativeLineRectangles(text, reader.Document.Text,
                                selection.Start - text.TextStart, selection.Length)
                            .Select(rect => rect.Translate(new Vector(text.Padding.Left, text.Padding.Top)))
                            .Select(rect => rect.Intersect(viewport)).Where(Positive).ToArray();
                        var commands = DrawSelection(text);
                        Assert.Equal(expected, commands.Select(rect => rect.Intersect(viewport)).Where(Positive).ToArray());
                        Assert.Same(layout, text.TextLayout);
                        Assert.Equal(selection, reader.Selection);
                        Assert.Equal(reader.Document.Text[selection.Start..selection.End], reader.SelectedText);
                    }
                }
            }
            finally { window.Close(); }
        });

    [Fact]
    public Task BlankLfNativeCodeHelperKeepsAutomationBoundsAndGlobalCopy() => HeadlessTestScope.Run(async () =>
    {
        var reader = new Reader { Text = "```text\n\n\nfirst 😀e\u0301\n\nlast אבג\n\n\n```" };
        reader.ConfigureParser(new()
        {
            Converters = [new MarkdownNodeConverterRegistration<FencedCodeBlock>(_ => new("blank-code"))]
        });
        reader.NodeRenderers = new(blocks: [new("blank-code", context =>
        {
            var text = context.CreateText();
            // Match the default code presenter. Native wrapped plain TextBlock
            // already hangs on leading LF under an infinite measure height.
            text.TextWrapping = TextWrapping.NoWrap;
            return text;
        })]);
        var window = new Window { Width = 520, Height = 320, Content = reader };
        window.Show();
        try
        {
            await reader.WhenUpdatedAsync(); await Flush(window);
            var text = Assert.Single(reader.GetVisualDescendants().OfType<MarkdownTextPresenter>());
            Assert.Empty(reader.GetVisualDescendants().OfType<MarkdownBlockPresenter>());
            Assert.InRange(text.TextLength, 1, reader.Document.Text.Length);
            var peer = Assert.IsType<MarkdownAutomationPeer>(ControlAutomationPeer.CreatePeerForElement(reader));
            var logical = Assert.Single(peer.GetChildren());
            var transform = text.TransformToVisual(window)!.Value;
            var expected = default(Rect);
            foreach (var rect in NonemptyNativeLineRectangles(text, reader.Document.Text, 0, text.TextLength))
            {
                var translated = rect.Translate(new Vector(text.Padding.Left, text.Padding.Top)).TransformToAABB(transform);
                expected = expected == default ? translated : expected.Union(translated);
            }
            Assert.True(expected.Width > 0 && expected.Height > 0);
            Assert.Equal(expected, logical.GetBoundingRectangle());
            reader.SelectAll();
            Assert.Equal(reader.Document.Text, reader.SelectedText);
            Assert.Equal(reader.SelectedText, peer.GetSelectionRange().Text);
            string? copied = null;
            reader.CopyingToClipboard += (_, args) => { copied = args.Text; args.Cancel = true; };
            Assert.Equal(MarkdownCopyResult.Cancelled, await reader.CopyAsync());
            Assert.Equal(reader.Document.Text, copied);
            _ = DrawSelection(text);
        }
        finally { window.Close(); }
    });

    private static IEnumerable<Rect> NonemptyNativeLineRectangles(MarkdownTextPresenter text, string document,
        int selectedStart, int selectedLength)
    {
        var y = 0d;
        foreach (var line in text.TextLayout.TextLines)
        {
            var start = Math.Max(selectedStart, line.FirstTextSourceIndex);
            var end = Math.Min(selectedStart + selectedLength, Math.Min(text.TextLength, line.FirstTextSourceIndex + line.Length));
            var length = end - start;
            if (length > 0)
            {
                var emptyLf = length == 1 && document[text.TextStart + start] == '\n';
                if (emptyLf) Assert.Equal(0, line.WidthIncludingTrailingWhitespace);
                else foreach (var bounds in line.GetTextBounds(start, length)) yield return bounds.Rectangle.WithY(y);
            }
            y += line.Height;
        }
    }

    private static bool Positive(Rect rectangle) => rectangle.Width > 0 && rectangle.Height > 0;

    private static ScrollViewer Scroll(Reader reader) => reader.GetVisualDescendants().OfType<ScrollViewer>()
        .Single(scroll => scroll.Name == "PART_ScrollViewer");

    private static async Task Flush(Window window)
    {
        await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout, DispatcherPriority.Loaded);
        await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout, DispatcherPriority.Loaded);
    }

    private static Rect[] DrawSelection(MarkdownTextPresenter text)
    {
        var group = new DrawingGroup();
        using (var context = group.Open()) text.Render(context);
        return Flatten(group).OfType<GeometryDrawing>()
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

    private sealed class HostSpan : Span;
}
