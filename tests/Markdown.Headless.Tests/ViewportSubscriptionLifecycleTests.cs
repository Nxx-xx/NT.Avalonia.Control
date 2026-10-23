using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Markdown;
using Reader = NT.AvaloniaControl.Markdown.Markdown;

namespace Markdown.Headless.Tests;

public sealed class ViewportSubscriptionLifecycleTests
{
    private static readonly Color SelectionColor = Color.FromArgb(219, 123, 41, 167);

    [Theory]
    [InlineData("wrap")]
    [InlineData("spacing")]
    [InlineData("max-lines")]
    public Task MovingBetweenEligibleAndFallbackLayoutsKeepsSelectionAndResumesViewportDrawing(string change) =>
        HeadlessTestScope.Run(async () =>
        {
            var reader = new Reader { Text = Source(), SelectionBrush = new SolidColorBrush(SelectionColor) };
            var window = new Window { Width = 520, Height = 230, Content = reader };
            window.Show();
            try
            {
                await reader.WhenUpdatedAsync(); await Flush(window);
                reader.SelectAll();
                var text = Text(reader);
                var selection = reader.Selection;
                var selected = reader.SelectedText;
                await ScrollMiddle(reader, window);
                AssertGeometry(reader, text, bounded: true);

                switch (change)
                {
                    case "wrap": text.TextWrapping = TextWrapping.NoWrap; break;
                    case "spacing": text.LineSpacing = 4; break;
                    case "max-lines": text.MaxLines = 7; break;
                }
                await Flush(window);
                AssertGeometry(reader, text, bounded: false);
                text.TextWrapping = TextWrapping.Wrap;
                text.LineSpacing = 0;
                text.MaxLines = 0;
                await Flush(window);
                await ScrollMiddle(reader, window);
                Assert.True(text.TextLayout.TextLines.Count >= 32);
                AssertGeometry(reader, text, bounded: true);
                Assert.Equal(selection, reader.Selection);
                Assert.Equal(selected, reader.SelectedText);
            }
            finally { window.Close(); }
        });

    [Fact]
    public Task LongShortLongReplacementAndDetachReattachKeepTheCurrentViewport() => HeadlessTestScope.Run(async () =>
    {
        var reader = new Reader { Text = Source(), SelectionBrush = new SolidColorBrush(SelectionColor) };
        var window = new Window { Width = 520, Height = 230, Content = reader };
        window.Show();
        try
        {
            await reader.WhenUpdatedAsync(); await Flush(window);
            reader.SelectAll(); await ScrollMiddle(reader, window);
            AssertGeometry(reader, Text(reader), bounded: true);
            reader.Text = "short";
            await reader.WhenUpdatedAsync(); await Flush(window);
            reader.SelectAll();
            Assert.Single(Text(reader).TextLayout.TextLines);
            AssertGeometry(reader, Text(reader), bounded: false);
            reader.Text = Source();
            await reader.WhenUpdatedAsync(); await Flush(window);
            reader.SelectAll(); await ScrollMiddle(reader, window);
            AssertGeometry(reader, Text(reader), bounded: true);
            window.Content = null; window.UpdateLayout();
            window.Content = reader;
            await reader.WhenUpdatedAsync(); await Flush(window);
            reader.SelectAll(); await ScrollMiddle(reader, window);
            AssertGeometry(reader, Text(reader), bounded: true);
            Assert.Equal(reader.Document.Text, reader.SelectedText);
        }
        finally { window.Close(); }
    });

    private static string Source() => string.Concat(Enumerable.Range(0, 230)
        .Select(index => $"record{index:D3} 中文 😀e\u0301 alpha beta gamma "));

    private static MarkdownTextPresenter Text(Reader reader) =>
        Assert.Single(reader.GetVisualDescendants().OfType<MarkdownTextPresenter>());

    private static ScrollViewer Scroll(Reader reader) => reader.GetVisualDescendants().OfType<ScrollViewer>()
        .Single(view => view.Name == "PART_ScrollViewer");

    private static async Task ScrollMiddle(Reader reader, Window window)
    {
        var scroll = Scroll(reader);
        scroll.Offset = new Vector(0, Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height) * .43);
        await Flush(window);
    }

    private static async Task Flush(Window window)
    {
        await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout, DispatcherPriority.Loaded);
        await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout, DispatcherPriority.Loaded);
    }

    private static void AssertGeometry(Reader reader, MarkdownTextPresenter text, bool bounded)
    {
        var layout = text.TextLayout;
        var expected = layout.HitTestTextRange(0, text.TextLength)
            .Select(rect => rect.Translate(new Vector(text.Padding.Left, text.Padding.Top))).ToArray();
        var group = new DrawingGroup();
        using (var context = group.Open()) text.Render(context);
        var actual = Flatten(group).OfType<GeometryDrawing>()
            .Where(drawing => drawing.Brush is ISolidColorBrush brush && brush.Color == SelectionColor)
            .Select(drawing => drawing.Geometry!.Bounds).ToArray();
        var scroll = Scroll(reader);
        var viewport = new Rect(scroll.TranslatePoint(new Point(scroll.Padding.Left, scroll.Padding.Top), text)!.Value,
            scroll.Viewport);
        if (bounded)
        {
            Assert.Equal(expected.Select(rect => rect.Intersect(viewport)).Where(Positive),
                actual.Select(rect => rect.Intersect(viewport)).Where(Positive));
            Assert.All(actual.Where(Positive), rect => Assert.True(rect.Bottom > viewport.Top && rect.Top < viewport.Bottom));
        }
        else Assert.Equal(expected, actual);
        Assert.Same(layout, text.TextLayout);
    }

    private static bool Positive(Rect rectangle) => rectangle.Width > 0 && rectangle.Height > 0;
    private static IEnumerable<Drawing> Flatten(Drawing drawing)
    {
        yield return drawing;
        if (drawing is DrawingGroup group)
            foreach (var child in group.Children)
                foreach (var item in Flatten(child)) yield return item;
    }
}
