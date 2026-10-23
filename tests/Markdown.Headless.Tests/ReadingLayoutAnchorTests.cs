using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Markdig.Syntax;
using NT.AvaloniaControl.Markdown;
using Reader = NT.AvaloniaControl.Markdown.Markdown;

namespace Markdown.Headless.Tests;

public sealed class ReadingLayoutAnchorTests
{
    [Fact]
    public Task MT14_LargerFontKeepsTheCapturedMiddleParagraphGraphemeAtItsViewportOffset() => HeadlessTestScope.Run(() => MT14_LargerFontKeepsTheCapturedMiddleParagraphGraphemeAtItsViewportOffsetCore());

    private async Task MT14_LargerFontKeepsTheCapturedMiddleParagraphGraphemeAtItsViewportOffsetCore()
    {
        var reader = new Reader { Text = LongParagraph(), FontSize = 14 };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync();
            var before = await ReadInTheMiddle(reader, window);
            var height = TextFor(reader, before.Position).TextLayout.Height;
            reader.FontSize = 26;
            await Flush(window);
            Assert.True(TextFor(reader, before.Position).TextLayout.Height > height * 1.5);
            AssertReadingGrapheme(reader, before);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task MT14_NarrowerViewportKeepsTheCapturedGraphemeInTheNewFirstVisibleLine() => HeadlessTestScope.Run(() => MT14_NarrowerViewportKeepsTheCapturedGraphemeInTheNewFirstVisibleLineCore());

    private async Task MT14_NarrowerViewportKeepsTheCapturedGraphemeInTheNewFirstVisibleLineCore()
    {
        var reader = new Reader { Text = LongParagraph(), FontSize = 14 };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync();
            var before = await ReadInTheMiddle(reader, window);
            var width = TextFor(reader, before.Position).Bounds.Width;
            window.Width = 350;
            await Flush(window);
            Assert.True(TextFor(reader, before.Position).Bounds.Width < width * .75);
            AssertReadingGrapheme(reader, before);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task MT14_DynamicRootFontResourceKeepsTheCachedGraphemeBeforeInheritanceInvalidatesLayout() => HeadlessTestScope.Run(() => MT14_DynamicRootFontResourceKeepsTheCachedGraphemeBeforeInheritanceInvalidatesLayoutCore());

    private async Task MT14_DynamicRootFontResourceKeepsTheCachedGraphemeBeforeInheritanceInvalidatesLayoutCore()
    {
        var reader = new Reader { Text = LongParagraph() };
        reader.Resources["MarkdownFontSize"] = 14d;
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync();
            var before = await ReadInTheMiddle(reader, window);
            Assert.Equal(14, TextFor(reader, before.Position).FontSize);
            reader.Resources["MarkdownFontSize"] = 24d;
            await Flush(window);
            Assert.Equal(24, TextFor(reader, before.Position).FontSize);
            AssertReadingGrapheme(reader, before);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task MT13_ImageGrowingAboveTextInsideTheSameBlockKeepsTheLogicalGraphemeAndPixelOffset() => HeadlessTestScope.Run(() => MT13_ImageGrowingAboveTextInsideTheSameBlockKeepsTheLogicalGraphemeAndPixelOffsetCore());

    private async Task MT13_ImageGrowingAboveTextInsideTheSameBlockKeepsTheLogicalGraphemeAndPixelOffsetCore()
    {
        var loader = new DelayedLoader();
        var reader = new Reader { Text = "![image](https://example.test/anchor) " + LongParagraph(), FontSize = 14, ImageLoader = loader };
        reader.ConfigureParser(new()
        {
            Converters = [new MarkdownNodeConverterRegistration<ParagraphBlock>(_ => new("image-before-text"))]
        });
        reader.NodeRenderers = new(blocks: [new("image-before-text", context =>
        {
            // A host renderer may put a native image before its mapped text helper.
            // Block identity and an unchanged block-relative pixel offset are insufficient.
            var border = (Border)context.CreateDefault();
            var stack = (StackPanel)border.Child!;
            var image = stack.Children.OfType<MarkdownImage>().Single();
            stack.Children.Remove(image);
            stack.Children.Insert(0, image);
            return border;
        })]);
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync();
            await loader.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            var before = await ReadInTheMiddle(reader, window);
            var image = reader.GetVisualDescendants().OfType<MarkdownImage>().Single();
            var initialHeight = image.Bounds.Height;
            loader.Complete(180);
            for (var pass = 0; pass < 100 && image.IsLoading; pass++) await Flush(window);
            Assert.False(image.IsLoading);
            await Flush(window);
            Assert.True(image.Bounds.Height > initialHeight + 100);
            Assert.Empty(reader.RenderingDiagnostics);
            AssertReadingGrapheme(reader, before);
        }
        finally { window.Close(); loader.CancelPending(); }
    }

    [Fact]
    public Task MT14_RepeatedReflowsBeforeTheQueuedRestoreKeepTheOriginalStableGrapheme() => HeadlessTestScope.Run(() => MT14_RepeatedReflowsBeforeTheQueuedRestoreKeepTheOriginalStableGraphemeCore());

    private async Task MT14_RepeatedReflowsBeforeTheQueuedRestoreKeepTheOriginalStableGraphemeCore()
    {
        var reader = new Reader { Text = LongParagraph(), FontSize = 14 };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync();
            var before = await ReadInTheMiddle(reader, window);
            reader.FontSize = 26; window.UpdateLayout();
            reader.FontSize = 30; window.UpdateLayout();
            reader.FontSize = 18; window.UpdateLayout();
            window.Width = 430; window.UpdateLayout();
            await Flush(window);
            Assert.Equal(18, TextFor(reader, before.Position).FontSize);
            AssertReadingGrapheme(reader, before);
        }
        finally { window.Close(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task MT14_CustomNativeTextHelpersWithoutBlockPresenterAncestorsKeepReadingPosition(bool wrap) => HeadlessTestScope.Run(() => MT14_CustomNativeTextHelpersWithoutBlockPresenterAncestorsKeepReadingPositionCore(wrap));

    private async Task MT14_CustomNativeTextHelpersWithoutBlockPresenterAncestorsKeepReadingPositionCore(bool wrap)
    {
        var reader = new Reader { Text = LongParagraph(), FontSize = 14 };
        reader.ConfigureParser(new()
        { Converters = [new MarkdownNodeConverterRegistration<ParagraphBlock>(_ => new("direct-text"))] });
        reader.NodeRenderers = new(blocks: [new("direct-text", context => wrap
            ? new Border { Padding = new Thickness(11), Child = context.CreateText() }
            : context.CreateText())]);
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync();
            Assert.Empty(reader.GetVisualDescendants().OfType<MarkdownBlockPresenter>());
            var before = await ReadInTheMiddle(reader, window);
            reader.FontSize = 24;
            window.Width = 430;
            await Flush(window);
            Assert.Empty(reader.RenderingDiagnostics);
            AssertReadingGrapheme(reader, before);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task MT13_UserKeyboardNavigationWinsOverAnAlreadyQueuedReflowRestore() => HeadlessTestScope.Run(() => MT13_UserKeyboardNavigationWinsOverAnAlreadyQueuedReflowRestoreCore());

    private async Task MT13_UserKeyboardNavigationWinsOverAnAlreadyQueuedReflowRestoreCore()
    {
        var reader = new Reader { Text = LongParagraph(), FontSize = 14 };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync();
            _ = await ReadInTheMiddle(reader, window);
            reader.FontSize = 26;
            window.UpdateLayout();
            reader.Focus();
            window.KeyPress(Key.Home, RawInputModifiers.Control, PhysicalKey.None, null);
            window.KeyRelease(Key.Home, RawInputModifiers.Control, PhysicalKey.None, null);
            await Flush(window);
            Assert.Equal(0, reader.Selection.Active);
            Assert.InRange(Scroll(reader).Offset.Y, 0, 1);
            Assert.Equal(0, FirstVisible(reader).Position);
        }
        finally { window.Close(); }
    }

    private static string LongParagraph() => string.Concat(Enumerable.Range(0, 180)
        .Select(index => $"record{index:D3} 中文😀e\u0301 👨‍👩‍👧‍👦 reading position "));

    private static Window Show(Reader reader)
    { var window = new Window { Width = 640, Height = 260, Content = reader }; window.Show(); return window; }

    private static ScrollViewer Scroll(Reader reader) => reader.GetVisualDescendants().OfType<ScrollViewer>()
        .Single(scroll => scroll.Name == "PART_ScrollViewer");

    private static MarkdownTextPresenter TextFor(Reader reader, int position) => reader.GetVisualDescendants()
        .OfType<MarkdownTextPresenter>().Single(text => position >= text.TextStart && position < text.TextStart + text.TextLength);

    private static async Task<VisibleGrapheme> ReadInTheMiddle(Reader reader, Window window)
    {
        await Flush(window);
        var text = reader.GetVisualDescendants().OfType<MarkdownTextPresenter>().Single();
        Assert.True(text.TextLayout.TextLines.Count > 40);
        var middleLine = text.TextLayout.TextLines[text.TextLayout.TextLines.Count / 2];
        var position = MarkdownTextBoundary.SnapBackward(reader.Document.Text, text.TextStart + middleLine.FirstTextSourceIndex);
        var rectangle = text.TextLayout.HitTestTextPosition(position - text.TextStart);
        var origin = text.TranslatePoint(new Point(text.Padding.Left, text.Padding.Top), reader.DocumentPresenter!)!.Value;
        // Leave a partially clipped first line, so preserving only its identity is not enough.
        Scroll(reader).Offset = new Vector(0, origin.Y + rectangle.Y + 5.25);
        await Flush(window);
        var before = FirstVisible(reader);
        Assert.Single(reader.Document.Blocks);
        Assert.InRange(before.Position, reader.Document.Text.Length / 4, reader.Document.Text.Length * 3 / 4);
        Assert.InRange(before.Offset, -6, -4);
        Assert.Equal(before.Position, MarkdownTextBoundary.SnapBackward(reader.Document.Text, before.Position));
        return before;
    }

    private static VisibleGrapheme FirstVisible(Reader reader)
    {
        var scroll = Scroll(reader);
        var candidates = new List<VisibleGrapheme>();
        foreach (var text in reader.GetVisualDescendants().OfType<MarkdownTextPresenter>())
        {
            var origin = text.TranslatePoint(new Point(text.Padding.Left, text.Padding.Top), scroll)!.Value;
            var y = origin.Y;
            foreach (var line in text.TextLayout.TextLines)
            {
                if (y + line.Height > scroll.Padding.Top + .01 && y < scroll.Padding.Top + scroll.Viewport.Height)
                {
                    var position = MarkdownTextBoundary.SnapBackward(reader.Document.Text, text.TextStart + line.FirstTextSourceIndex);
                    var rectangle = text.TextLayout.HitTestTextPosition(position - text.TextStart);
                    var end = text.TextStart + Math.Min(text.TextLength, line.FirstTextSourceIndex + line.Length);
                    candidates.Add(new(position, end, origin.Y + rectangle.Y - scroll.Padding.Top,
                        reader.Document.Text[position..MarkdownTextBoundary.Next(reader.Document.Text, position)]));
                    break;
                }
                y += line.Height;
            }
        }
        Assert.NotEmpty(candidates);
        return candidates.OrderBy(candidate => candidate.Offset).First();
    }

    private static void AssertReadingGrapheme(Reader reader, VisibleGrapheme before)
    {
        var after = FirstVisible(reader);
        var text = TextFor(reader, before.Position);
        var rectangle = text.TextLayout.HitTestTextPosition(before.Position - text.TextStart);
        var origin = text.TranslatePoint(new Point(text.Padding.Left, text.Padding.Top), Scroll(reader))!.Value;
        var offset = origin.Y + rectangle.Y - Scroll(reader).Padding.Top;
        Assert.True(Math.Abs(before.Offset - offset) <= 1,
            $"Captured grapheme at {before.Position} moved from viewportY={before.Offset:F3} to {offset:F3}; " +
            $"new first visible line=[{after.Position},{after.End}), scroll={Scroll(reader).Offset.Y:F3}.");
        // Reflow may place the old first grapheme in the middle of the new first line.
        // It must remain in that line and keep its vertical offset; its line prefix may differ.
        Assert.InRange(before.Position, after.Position, Math.Max(after.Position, after.End - 1));
        Assert.Equal(before.Grapheme, reader.Document.Text[before.Position..MarkdownTextBoundary.Next(reader.Document.Text, before.Position)]);
        Assert.Equal(after.Position, MarkdownTextBoundary.SnapBackward(reader.Document.Text, after.Position));
    }

    private static async Task Flush(Window window)
    {
        await Task.Delay(15, TestContext.Current.CancellationToken);
        await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout, DispatcherPriority.Loaded);
        await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout, DispatcherPriority.Loaded);
    }

    private readonly record struct VisibleGrapheme(int Position, int End, double Offset, string Grapheme);

    private sealed class DelayedLoader : IMarkdownImageLoader
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<MarkdownImageLease> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ValueTask<MarkdownImageLease> LoadAsync(Uri uri, CancellationToken cancellationToken)
        { Started.TrySetResult(); return new(_result.Task); }
        public void Complete(int height) => _result.TrySetResult(new(new WriteableBitmap(new PixelSize(80, height),
            new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul)));
        public void CancelPending() => _result.TrySetCanceled();
    }
}

