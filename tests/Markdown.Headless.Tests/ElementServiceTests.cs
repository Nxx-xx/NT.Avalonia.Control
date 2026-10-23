using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Markdown;
using Reader = NT.AvaloniaControl.Markdown.Markdown;

namespace Markdown.Headless.Tests;

public sealed class ElementServiceTests
{
    [Fact]
    public Task ImageRuleOverridesInheritanceAndExplicitNullWhileRootChangesOnlyInheritedNodes() => HeadlessTestScope.Run(() => ImageRuleOverridesInheritanceAndExplicitNullWhileRootChangesOnlyInheritedNodesCore());

    private async Task ImageRuleOverridesInheritanceAndExplicitNullWhileRootChangesOnlyInheritedNodesCore()
    {
        var inherited = new Loader(); var local = new Loader(); var replacement = new Loader();
        var reader = new Reader
        {
            Text = "![local](https://example.test/a) ![disabled](https://example.test/b) ![inherited](https://example.test/c)",
            ImageLoader = inherited,
            ElementServices = node => node.Inline?.Text switch
            {
                "local" => new() { OverrideImageLoader = true, ImageLoader = local },
                "disabled" => new() { OverrideImageLoader = true },
                _ => null,
            },
        };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync(); await Wait(window, () => local.Requests.Count == 1 && inherited.Requests.Count == 1);
            var nodes = Images(reader); var disabled = nodes.Single(node => node.AltText == "disabled");
            Assert.Null(disabled.ImageLoader); Assert.False(disabled.IsLoading);
            reader.SetSelection(0, reader.Document.Text.Length); var selection = reader.Selection;
            reader.ImageLoader = replacement;
            await Wait(window, () => replacement.Requests.Count == 1);
            Assert.True(inherited.Requests[0].Token.IsCancellationRequested);
            Assert.False(local.Requests[0].Token.IsCancellationRequested);
            Assert.Single(local.Requests); Assert.Same(nodes[0], Images(reader)[0]); Assert.Equal(selection, reader.Selection);
            var stale = Bitmap(); inherited.Requests[0].Complete(stale);
            local.Requests[0].Complete(Bitmap()); replacement.Requests[0].Complete(Bitmap());
            await Wait(window, () => reader.ActiveResourceRequestCount == 0);
            Assert.ThrowsAny<Exception>(() => stale.Lock());
            Assert.Null(disabled.Child is Image image ? image.Source : throw new InvalidOperationException());
        }
        finally { window.Close(); CompleteRemaining(inherited, local, replacement); }
        Assert.Equal(0, inherited.DisposeCount + local.DisposeCount + replacement.DisposeCount);
    }

    [Fact]
    public Task ImageLocalNullCanBeClearedToRestoreInheritanceAndBaseUriReusesTheSameNode() => HeadlessTestScope.Run(() => ImageLocalNullCanBeClearedToRestoreInheritanceAndBaseUriReusesTheSameNodeCore());

    private async Task ImageLocalNullCanBeClearedToRestoreInheritanceAndBaseUriReusesTheSameNodeCore()
    {
        var loader = new Loader();
        var reader = new Reader { Text = "![relative](assets/picture.png)", ImageLoader = loader, BaseUri = new Uri("https://first.test/path/") };
        reader.ElementPrepared += (_, args) => { if (args.Element is MarkdownImage node) node.ImageLoader = null; };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync(); window.UpdateLayout(); var image = Images(reader).Single();
            Assert.Empty(loader.Requests); Assert.Null(image.ImageLoader);
            image.ClearValue(MarkdownImage.ImageLoaderProperty);
            await Wait(window, () => loader.Requests.Count == 1);
            Assert.Equal("https://first.test/path/assets/picture.png", loader.Requests[0].Uri.AbsoluteUri);
            reader.BaseUri = new Uri("https://second.test/");
            await Wait(window, () => loader.Requests.Count == 2);
            Assert.True(loader.Requests[0].Token.IsCancellationRequested);
            Assert.Equal("https://second.test/assets/picture.png", loader.Requests[1].Uri.AbsoluteUri);
            Assert.Same(image, Images(reader).Single());
        }
        finally { window.Close(); CompleteRemaining(loader); }
    }

    [Fact]
    public Task ImageStylesCanOverrideTheInheritedServiceWithoutChangingLogicalAltText() => HeadlessTestScope.Run(() => ImageStylesCanOverrideTheInheritedServiceWithoutChangingLogicalAltTextCore());

    private async Task ImageStylesCanOverrideTheInheritedServiceWithoutChangingLogicalAltTextCore()
    {
        var root = new Loader(); var styled = new Loader();
        var style = new Style(selector => selector.OfType<MarkdownImage>());
        style.Setters.Add(new Setter(MarkdownImage.ImageLoaderProperty, styled));
        Application.Current!.Styles.Add(style);
        var reader = new Reader { Text = "![中😀 alt](https://example.test/image)", ImageLoader = root };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync(); await Wait(window, () => styled.Requests.Count == 1);
            Assert.Empty(root.Requests); Assert.Same(styled, Images(reader).Single().ImageLoader);
            reader.SelectAll(); Assert.Equal("中😀 alt", reader.SelectedText);
        }
        finally { window.Close(); Application.Current.Styles.Remove(style); CompleteRemaining(root, styled); }
    }

    [Fact]
    public Task ElementRuleIsReappliedAfterVirtualizationAndDetachmentCancelsPendingLoads() => HeadlessTestScope.Run(() => ElementRuleIsReappliedAfterVirtualizationAndDetachmentCancelsPendingLoadsCore());

    private async Task ElementRuleIsReappliedAfterVirtualizationAndDetachmentCancelsPendingLoadsCore()
    {
        var root = new Loader(); var local = new Loader();
        var reader = new Reader
        {
            Text = "![local](https://example.test/image)\n\n" + string.Join("\n\n", Enumerable.Range(0, 100).Select(i => $"Paragraph {i:D3} text")),
            ImageLoader = root,
            ElementServices = node => node.Inline is not null ? new() { OverrideImageLoader = true, ImageLoader = local } : null,
        };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync(); await Wait(window, () => local.Requests.Count == 1);
            var scroll = reader.GetVisualDescendants().OfType<ScrollViewer>().Single(node => node.Name == "PART_ScrollViewer");
            scroll.ScrollToEnd(); await Wait(window, () => Images(reader).Length == 0);
            Assert.True(local.Requests[0].Token.IsCancellationRequested);
            scroll.Offset = default; await Wait(window, () => local.Requests.Count == 2);
            Assert.Same(local, Images(reader).Single().ImageLoader); Assert.Empty(root.Requests);
            var stale = Bitmap(); local.Requests[0].Complete(stale);
            await Wait(window, () => reader.ActiveResourceRequestCount == 1);
            Assert.ThrowsAny<Exception>(() => stale.Lock());
        }
        finally { window.Close(); CompleteRemaining(root, local); }
    }

    [Fact]
    public Task ImageFailureCanRetryAndDoesNotBlockTheDocumentOrSelection() => HeadlessTestScope.Run(() => ImageFailureCanRetryAndDoesNotBlockTheDocumentOrSelectionCore());

    private async Task ImageFailureCanRetryAndDoesNotBlockTheDocumentOrSelectionCore()
    {
        var loader = new Loader(); var errors = new List<string>();
        var reader = new Reader { Text = "before ![alt](https://example.test/image) after", ImageLoader = loader };
        reader.Error += (_, error) => errors.Add(error.Category);
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync(); await Wait(window, () => loader.Requests.Count == 1);
            reader.SelectAll(); var projection = reader.SelectedText;
            loader.Requests[0].Fail(new IOException("local fixture failure"));
            var image = Images(reader).Single(); await Wait(window, () => image.LoadError is not null);
            Assert.False(image.IsLoading); Assert.Contains("Image", errors); Assert.Equal(projection, reader.SelectedText);
            image.Reload(); await Wait(window, () => loader.Requests.Count == 2);
            Assert.Null(image.LoadError); loader.Requests[1].Complete(Bitmap());
            await Wait(window, () => !image.IsLoading);
            Assert.NotNull(((Image)image.Child!).Source); Assert.Equal(projection, reader.SelectedText);
        }
        finally { window.Close(); CompleteRemaining(loader); }
    }

    [Fact]
    public Task CodeRuleSupportsLocalServiceAndNullAndLateFormattingCannotOverwriteReplacement() => HeadlessTestScope.Run(() => CodeRuleSupportsLocalServiceAndNullAndLateFormattingCannotOverwriteReplacementCore());

    private async Task CodeRuleSupportsLocalServiceAndNullAndLateFormattingCannotOverwriteReplacementCore()
    {
        var root = new Highlighter(); var local = new Highlighter(); var next = new Highlighter();
        var reader = new Reader
        {
            Text = "```local\nalpha\n```\n\n```disabled\nbeta\n```\n\n```inherited\ngamma\n```", CodeHighlighter = root,
            ElementServices = node => node.Block.Metadata.GetValueOrDefault("language") switch
            {
                "local" => new() { OverrideCodeHighlighter = true, CodeHighlighter = local },
                "disabled" => new() { OverrideCodeHighlighter = true },
                _ => null,
            },
        };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync(); await Wait(window, () => root.Requests.Count == 1 && local.Requests.Count == 1);
            reader.SelectAll(); var projection = reader.SelectedText;
            reader.CodeHighlighter = next; await Wait(window, () => next.Requests.Count == 1);
            Assert.True(root.Requests[0].Token.IsCancellationRequested); Assert.False(local.Requests[0].Token.IsCancellationRequested);
            Assert.Equal(0, root.Subscribers); Assert.Equal(1, next.Subscribers); Assert.Equal(1, local.Subscribers);
            root.Requests[0].Complete("#ff0000"); next.Requests[0].Complete("#0000ff"); local.Requests[0].Complete("#00ff00");
            await Wait(window, () => reader.ActiveResourceRequestCount == 0);
            Assert.Equal(projection, reader.SelectedText); Assert.Contains("alpha\n", projection);
            var code = reader.GetVisualDescendants().OfType<MarkdownCodeBlock>().Single(node => node.Language == "inherited");
            var text = code.GetVisualDescendants().OfType<MarkdownTextPresenter>().Single();
            Assert.Equal(Colors.Blue, Assert.IsAssignableFrom<ISolidColorBrush>(text.Inlines![0].Foreground).Color);
            Assert.Null(reader.GetVisualDescendants().OfType<MarkdownCodeBlock>().Single(node => node.Language == "disabled").CodeHighlighter);
        }
        finally { window.Close(); CompleteRemaining(root, local, next); }
        Assert.Equal(0, root.Subscribers + local.Subscribers + next.Subscribers);
        Assert.Equal(0, root.DisposeCount + local.DisposeCount + next.DisposeCount);
    }

    [Fact]
    public Task SharedHighlighterInvalidatesBothReadersAndClosingOneDoesNotDisposeOrUnsubscribeTheOther() => HeadlessTestScope.Run(() => SharedHighlighterInvalidatesBothReadersAndClosingOneDoesNotDisposeOrUnsubscribeTheOtherCore());

    private async Task SharedHighlighterInvalidatesBothReadersAndClosingOneDoesNotDisposeOrUnsubscribeTheOtherCore()
    {
        var service = new Highlighter();
        var first = new Reader { Text = "```text\none\n```", CodeHighlighter = service };
        var second = new Reader { Text = "```text\ntwo\n```", CodeHighlighter = service };
        var window1 = Show(first); var window2 = Show(second);
        try
        {
            await first.WhenUpdatedAsync(); await second.WhenUpdatedAsync(); await Wait(window2, () => service.Requests.Count == 2);
            CompleteRemaining(service); await Wait(window2, () => first.ActiveResourceRequestCount + second.ActiveResourceRequestCount == 0);
            Assert.Equal(2, service.Subscribers); service.Invalidate(); await Wait(window2, () => service.Requests.Count == 4);
            CompleteRemaining(service); window1.Close(); Assert.Equal(1, service.Subscribers); Assert.Equal(0, service.DisposeCount);
            service.Invalidate(); await Wait(window2, () => service.Requests.Count == 5);
        }
        finally { window1.Close(); window2.Close(); CompleteRemaining(service); }
        Assert.Equal(0, service.Subscribers); Assert.Equal(0, service.DisposeCount);
    }

    [Fact]
    public Task BorrowedImageLeaseDoesNotDisposeHostBitmapAndOwnedLeaseDisposalIsIdempotent() => HeadlessTestScope.Run(() => BorrowedImageLeaseDoesNotDisposeHostBitmapAndOwnedLeaseDisposalIsIdempotentCore());

    private void BorrowedImageLeaseDoesNotDisposeHostBitmapAndOwnedLeaseDisposalIsIdempotentCore()
    {
        using var borrowed = Bitmap(); var lease = new MarkdownImageLease(borrowed, false); lease.Dispose(); lease.Dispose();
        using (borrowed.Lock()) { }
        var owned = Bitmap(); var ownedLease = new MarkdownImageLease(owned); ownedLease.Dispose(); ownedLease.Dispose();
        Assert.ThrowsAny<Exception>(() => owned.Lock());
        Assert.Throws<ArgumentNullException>(() => new MarkdownImageLease(null!));
    }

    private static MarkdownImage[] Images(Reader reader) => reader.GetVisualDescendants().OfType<MarkdownImage>().ToArray();
    private static Window Show(Control reader) { var window = new Window { Width = 600, Height = 360, Content = reader }; window.Show(); return window; }
    private static WriteableBitmap Bitmap() => new(new PixelSize(8, 8), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
    private static async Task Wait(Window window, Func<bool> condition)
    {
        for (var i = 0; i < 200; i++) { window.UpdateLayout(); if (condition()) return; await Task.Delay(10, TestContext.Current.CancellationToken); await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout); }
        Assert.Fail("Element service state did not settle within the fixture timeout.");
    }
    private static void CompleteRemaining(params Loader[] loaders) { foreach (var loader in loaders) foreach (var request in loader.Requests.Where(request => !request.Result.Task.IsCompleted)) request.Complete(Bitmap()); }
    private static void CompleteRemaining(params Highlighter[] services) { foreach (var service in services) foreach (var request in service.Requests.Where(request => !request.Result.Task.IsCompleted)) request.Complete("#0000ff"); }
    private sealed class Loader : IMarkdownImageLoader, IDisposable
    {
        public List<ImageRequest> Requests { get; } = []; public int DisposeCount { get; private set; }
        public ValueTask<MarkdownImageLease> LoadAsync(Uri uri, CancellationToken token) { var request = new ImageRequest(uri, token); Requests.Add(request); return new(request.Result.Task); }
        public void Dispose() => DisposeCount++;
    }
    private sealed class ImageRequest(Uri uri, CancellationToken token)
    {
        public Uri Uri { get; } = uri; public CancellationToken Token { get; } = token;
        public TaskCompletionSource<MarkdownImageLease> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Complete(WriteableBitmap bitmap) => Result.TrySetResult(new(bitmap)); public void Fail(Exception error) => Result.TrySetException(error);
    }
    private sealed class Highlighter : IMarkdownCodeHighlighter, IDisposable
    {
        private EventHandler? _invalidated; public int Subscribers { get; private set; } public int DisposeCount { get; private set; }
        public event EventHandler? Invalidated { add { _invalidated += value; Subscribers++; } remove { _invalidated -= value; Subscribers--; } }
        public List<HighlightRequest> Requests { get; } = [];
        public ValueTask<IReadOnlyList<MarkdownHighlightSpan>> HighlightAsync(string code, string? language, CancellationToken token) { var request = new HighlightRequest(code, token); Requests.Add(request); return new(request.Result.Task); }
        public void Invalidate() => _invalidated?.Invoke(this, EventArgs.Empty); public void Dispose() => DisposeCount++;
    }
    private sealed class HighlightRequest(string code, CancellationToken token)
    {
        public CancellationToken Token { get; } = token;
        public TaskCompletionSource<IReadOnlyList<MarkdownHighlightSpan>> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Complete(string foreground) => Result.TrySetResult([new(0, code.Length, foreground)]);
    }
}

