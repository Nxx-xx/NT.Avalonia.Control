using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Markdown;
using Reader = NT.AvaloniaControl.Markdown.Markdown;

namespace Markdown.Headless.Tests;

public sealed class ReadingAnchorTests
{
    [Fact]
    public Task MT13_ImageAboveViewportPreservesTheFirstVisibleBlockAndItsOffset() => HeadlessTestScope.Run(() => MT13_ImageAboveViewportPreservesTheFirstVisibleBlockAndItsOffsetCore());

    private async Task MT13_ImageAboveViewportPreservesTheFirstVisibleBlockAndItsOffsetCore()
    {
        var loader = new DelayedLoader();
        var reader = new Reader { ImageLoader = loader, Text = "![alt](https://example.test/image)\n\n" + Paragraphs(24) };
        var window = new Window { Width = 520, Height = 240, Content = reader };
        window.Show();
        try
        {
            await reader.WhenUpdatedAsync();
            await loader.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            window.UpdateLayout();
            var scroll = Scroll(reader);
            scroll.Offset = new Vector(0, 180);
            await Flush(window);
            var before = FirstVisible(reader, scroll);
            loader.Finish(260);
            await WaitImage(reader, window);
            await Flush(window);
            var after = FirstVisible(reader, scroll);
            Assert.Equal(before.Id, after.Id);
            Assert.InRange(Math.Abs(before.Offset - after.Offset), 0, 1);
            Assert.True(scroll.Offset.Y > 180, $"offset={scroll.Offset.Y}, extent={scroll.Extent.Height}, image={reader.GetVisualDescendants().OfType<Image>().Single().Bounds}, imageDesired={reader.GetVisualDescendants().OfType<Image>().Single().DesiredSize}, topBlock={reader.DocumentPresenter!.Children[0].Bounds}");
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task MT13_UserScrollUpPausesAppendFollowingAndBottomRestoresIt() => HeadlessTestScope.Run(() => MT13_UserScrollUpPausesAppendFollowingAndBottomRestoresItCore());

    private async Task MT13_UserScrollUpPausesAppendFollowingAndBottomRestoresItCore()
    {
        var reader = new Reader { Text = Paragraphs(32), AutoScrollToEnd = true };
        var window = new Window { Width = 520, Height = 240, Content = reader };
        window.Show();
        try
        {
            await reader.WhenUpdatedAsync();
            reader.ScrollToEnd();
            await Flush(window);
            var scroll = Scroll(reader);
            scroll.Offset = new Vector(0, 200);
            await Flush(window);
            var before = FirstVisible(reader, scroll);
            var reading = reader.BeginStreaming();
            reading.Append("\n\nnew tail one\n\nnew tail two");
            Assert.Equal(MarkdownUpdateStatus.Completed, (await reading.CompleteAsync()).Status);
            await Flush(window);
            var after = FirstVisible(reader, scroll);
            Assert.Equal(before.Id, after.Id);
            Assert.InRange(Math.Abs(before.Offset - after.Offset), 0, 1);
            Assert.True(scroll.Offset.Y < scroll.Extent.Height - scroll.Viewport.Height - 24);
            scroll.ScrollToEnd();
            await Flush(window);
            var following = reader.BeginStreaming();
            following.Append("\n\nfollow the newest tail");
            await following.CompleteAsync();
            await Flush(window);
            Assert.InRange(scroll.Extent.Height - scroll.Viewport.Height - scroll.Offset.Y, 0, 2);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task MT10_UnchangedImageDuringStreamingKeepsItsResourceRequestAndLease() => HeadlessTestScope.Run(() => MT10_UnchangedImageDuringStreamingKeepsItsResourceRequestAndLeaseCore());

    private async Task MT10_UnchangedImageDuringStreamingKeepsItsResourceRequestAndLeaseCore()
    {
        var loader = new DelayedLoader();
        var reader = new Reader { ImageLoader = loader, Text = "![alt](https://example.test/image)\n\nseed paragraph" };
        var window = new Window { Width = 520, Height = 240, Content = reader };
        window.Show();
        try
        {
            await reader.WhenUpdatedAsync();
            await loader.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            var session = reader.BeginStreaming();
            session.Append("\n\nnew paragraph");
            await session.CompleteAsync();
            await Flush(window);
            Assert.Equal(1, loader.CallCount);
            Assert.False(loader.Token.IsCancellationRequested);
            loader.Finish(120);
            await WaitImage(reader, window);
            await Flush(window);
            Assert.Contains(reader.GetVisualDescendants().OfType<Image>(), image => image.Source is not null);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task MT24_FinalSnapshotDoesNotWaitForImageDownload() => HeadlessTestScope.Run(() => MT24_FinalSnapshotDoesNotWaitForImageDownloadCore());

    private async Task MT24_FinalSnapshotDoesNotWaitForImageDownloadCore()
    {
        var loader = new DelayedLoader();
        var reader = new Reader { ImageLoader = loader, Text = "seed" };
        var window = new Window { Width = 520, Height = 240, Content = reader };
        window.Show();
        try
        {
            await reader.WhenUpdatedAsync();
            var session = reader.BeginStreaming();
            session.Append("\n\n![alt](https://example.test/image)");
            Assert.Equal(MarkdownUpdateStatus.Completed,
                (await session.CompleteAsync().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Status);
            await loader.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.DoesNotContain(reader.GetVisualDescendants().OfType<Image>(), image => image.Source is not null);
            loader.Finish(120);
            await WaitImage(reader, window);
            await Flush(window);
            Assert.Contains(reader.GetVisualDescendants().OfType<Image>(), image => image.Source is not null);
        }
        finally { window.Close(); }
    }

    private static string Paragraphs(int count) => string.Join("\n\n", Enumerable.Range(0, count).Select(i => $"Paragraph {i:D2} 中文 marker text."));
    private static ScrollViewer Scroll(Reader reader) => reader.GetVisualDescendants().OfType<ScrollViewer>().Single(scroll => scroll.Name == "PART_ScrollViewer");

    private static (long Id, double Offset) FirstVisible(Reader reader, ScrollViewer scroll)
    {
        var first = reader.DocumentPresenter!.Children.OfType<MarkdownBlockPresenter>()
            .Where(block => block.Bounds.Bottom > scroll.Offset.Y + .1).OrderBy(block => block.Bounds.Y).First();
        return (first.NodeId, first.Bounds.Y - scroll.Offset.Y);
    }

    private static async Task Flush(Window window)
    {
        // ScrollChanged and resource continuations can be posted after the current UI turn.
        await Task.Delay(20, TestContext.Current.CancellationToken);
        await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout, DispatcherPriority.Loaded);
        await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout, DispatcherPriority.Loaded);
    }

    private static async Task WaitImage(Reader reader, Window window)
    {
        for (var i = 0; i < 100; i++)
        {
            window.UpdateLayout();
            if (reader.GetVisualDescendants().OfType<Image>().Any(image => image.Source is not null)) return;
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
        Assert.Fail("The completed local image lease was not applied within the test wait boundary.");
    }

    private sealed class DelayedLoader : IMarkdownImageLoader
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<MarkdownImageLease> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int CallCount { get; private set; }
        public CancellationToken Token { get; private set; }
        public ValueTask<MarkdownImageLease> LoadAsync(Uri uri, CancellationToken cancellationToken)
        {
            CallCount++;
            Token = cancellationToken;
            Started.TrySetResult();
            return new(_result.Task);
        }
        public void Finish(int height) => _result.TrySetResult(new(new WriteableBitmap(new PixelSize(120, height),
            new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul)));
    }
}

