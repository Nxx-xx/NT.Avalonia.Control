using System.Collections.Concurrent;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Markdown;
using Reader = NT.AvaloniaControl.Markdown.Markdown;

namespace Markdown.Headless.Tests;

public sealed class LinkResolutionTests
{
    [Fact]
    public Task MT20_RelativeAndAbsoluteAliasesShareVisitedStateAndBaseChangesRecomputeIt() => HeadlessTestScope.Run(() => MT20_RelativeAndAbsoluteAliasesShareVisitedStateAndBaseChangesRecomputeItCore());

    private async Task MT20_RelativeAndAbsoluteAliasesShareVisitedStateAndBaseChangesRecomputeItCore()
    {
        const string relative = "guide.md#part";
        const string absolute = "https://example.test/one/guide.md#part";
        var reader = new Reader
        {
            BaseUri = new Uri("https://example.test/one/"),
            Text = $"[relative]({relative}) [absolute]({absolute})\n\n" + string.Join("\n\n", Enumerable.Range(0, 80).Select(index => $"paragraph {index:D3}"))
        };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync(); window.UpdateLayout();
            var activated = new List<string>();
            reader.LinkClicked += (_, args) => { activated.Add(args.Url); args.Handled = false; };
            reader.SetSelection(0, 5); var selection = reader.Selection;
            Assert.Equal(2, LinkSpans(reader).Length);
            Assert.All(LinkSpans(reader), span => Assert.DoesNotContain("visited", span.Classes));
            Invoke(reader, "relative");
            Assert.Equal(absolute, Assert.Single(activated));
            Assert.All(LinkSpans(reader), span => Assert.Contains("visited", span.Classes));
            Assert.Equal(selection, reader.Selection);
            reader.BaseUri = new Uri("https://example.test/two/");
            Assert.DoesNotContain("visited", LinkSpans(reader).Single(span => span.Url == relative).Classes);
            Assert.Contains("visited", LinkSpans(reader).Single(span => span.Url == absolute).Classes);
            Invoke(reader, "relative");
            Assert.Equal("https://example.test/two/guide.md#part", activated[1]);
            reader.BaseUri = new Uri("https://example.test/one/");
            Assert.All(LinkSpans(reader), span => Assert.Contains("visited", span.Classes));
            var scroll = reader.GetVisualDescendants().OfType<ScrollViewer>().Single(node => node.Name == "PART_ScrollViewer");
            scroll.ScrollToEnd(); await Flush(window);
            Assert.Empty(LinkSpans(reader));
            scroll.Offset = default; await Flush(window);
            Assert.Equal(2, LinkSpans(reader).Length);
            Assert.All(LinkSpans(reader), span => Assert.Contains("visited", span.Classes));
            using var session = reader.BeginStreaming(); session.Append("\n\nnew tail");
            await session.CompleteAsync(); await Flush(window);
            Assert.Equal(2, LinkSpans(reader).Length);
            Assert.All(LinkSpans(reader), span => Assert.Contains("visited", span.Classes));
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task MT20_HostChangingBaseDuringActivationCannotMarkTheNewTargetAsVisited() => HeadlessTestScope.Run(() => MT20_HostChangingBaseDuringActivationCannotMarkTheNewTargetAsVisitedCore());

    private async Task MT20_HostChangingBaseDuringActivationCannotMarkTheNewTargetAsVisitedCore()
    {
        var reader = new Reader { Text = "[relative](guide.md)", BaseUri = new Uri("https://example.test/old/") };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync();
            string? activated = null;
            reader.LinkClicked += (_, args) => { activated = args.Url; reader.BaseUri = new Uri("https://example.test/new/"); args.Handled = false; };
            Invoke(reader, "relative");
            Assert.Equal("https://example.test/old/guide.md", activated);
            Assert.DoesNotContain("visited", Assert.Single(LinkSpans(reader)).Classes);
            reader.BaseUri = new Uri("https://example.test/old/");
            Assert.Contains("visited", Assert.Single(LinkSpans(reader)).Classes);
        }
        finally { window.Close(); }
    }

    [Theory]
    [InlineData("/root/readme.md#part", "https://example.test/root/readme.md#part")]
    [InlineData("//cdn.example.test/readme.md", "https://cdn.example.test/readme.md")]
    public Task MT20_WebRootAndAuthorityLinksResolveWithoutImplicitFileUris(string url, string expected) => HeadlessTestScope.Run(() => MT20_WebRootAndAuthorityLinksResolveWithoutImplicitFileUrisCore(url, expected));

    private async Task MT20_WebRootAndAuthorityLinksResolveWithoutImplicitFileUrisCore(string url, string expected)
    {
        var reader = new Reader { Text = $"[link]({url})", BaseUri = new Uri("https://example.test/path/") };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync();
            string? activated = null;
            reader.LinkClicked += (_, args) => { activated = args.Url; args.Handled = true; };
            Invoke(reader, "link");
            Assert.Equal(expected, activated);
            Assert.False(new Uri(activated!).IsFile);
        }
        finally { window.Close(); }
    }

    [Theory]
    [InlineData("/root/picture.png", "https://example.test/root/picture.png", "http://next.test/root/picture.png")]
    [InlineData("//cdn.example.test/picture.png", "https://cdn.example.test/picture.png", "http://cdn.example.test/picture.png")]
    public Task MT15_ImageWebDestinationsInheritCurrentBaseAndReloadTheSameNativeNode(string url, string expected, string replacement) => HeadlessTestScope.Run(() => MT15_ImageWebDestinationsInheritCurrentBaseAndReloadTheSameNativeNodeCore(url, expected, replacement));

    private async Task MT15_ImageWebDestinationsInheritCurrentBaseAndReloadTheSameNativeNodeCore(string url, string expected, string replacement)
    {
        var loader = new RecordingLoader();
        var reader = new Reader { Text = $"![image]({url})", BaseUri = new Uri("https://example.test/path/"), ImageLoader = loader };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync(); await Wait(window, () => loader.Uris.Count == 1 && reader.ActiveResourceRequestCount == 0);
            Assert.Equal(expected, loader.Uris.Single().AbsoluteUri);
            Assert.False(loader.Uris.Single().IsFile);
            var image = reader.GetVisualDescendants().OfType<MarkdownImage>().Single();
            reader.SelectAll(); var projection = reader.SelectedText;
            reader.BaseUri = new Uri("http://next.test/other/");
            await Wait(window, () => loader.Uris.Count == 2 && reader.ActiveResourceRequestCount == 0);
            Assert.Equal(replacement, loader.Uris.Last().AbsoluteUri);
            Assert.Same(image, reader.GetVisualDescendants().OfType<MarkdownImage>().Single());
            Assert.Equal(projection, reader.SelectedText);
        }
        finally { window.Close(); }
        Assert.All(loader.Bitmaps, bitmap => Assert.ThrowsAny<Exception>(() => bitmap.Lock()));
    }

    private static void Invoke(Reader reader, string name) => Descendants(ControlAutomationPeer.CreatePeerForElement(reader)!)
        .Single(peer => peer.GetName() == name && peer.GetProvider<IInvokeProvider>() is not null).GetProvider<IInvokeProvider>()!.Invoke();
    private static IEnumerable<AutomationPeer> Descendants(AutomationPeer peer)
    { foreach (var child in peer.GetChildren()) { yield return child; foreach (var nested in Descendants(child)) yield return nested; } }
    private static MarkdownSpan[] LinkSpans(Reader reader) => reader.GetVisualDescendants().OfType<MarkdownTextPresenter>()
        .SelectMany(text => text.GetLogicalDescendants().OfType<MarkdownSpan>()).Where(span => span.Url is not null).ToArray();
    private static Window Show(Reader reader)
    { var window = new Window { Width = 600, Height = 280, Content = reader }; window.Show(); return window; }
    private static async Task Flush(Window window)
    {
        await Task.Delay(20, TestContext.Current.CancellationToken);
        await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout, DispatcherPriority.Loaded);
        await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout, DispatcherPriority.Loaded);
    }
    private static async Task Wait(Window window, Func<bool> condition)
    {
        for (var pass = 0; pass < 100; pass++) { await Flush(window); if (condition()) return; }
        Assert.Fail("URI fixture did not settle within its wait boundary.");
    }
    private sealed class RecordingLoader : IMarkdownImageLoader
    {
        public ConcurrentQueue<Uri> Uris { get; } = new();
        public ConcurrentQueue<WriteableBitmap> Bitmaps { get; } = new();
        public ValueTask<MarkdownImageLease> LoadAsync(Uri uri, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Uris.Enqueue(uri);
            var bitmap = new WriteableBitmap(new PixelSize(4, 4), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            Bitmaps.Enqueue(bitmap); return ValueTask.FromResult(new MarkdownImageLease(bitmap));
        }
    }
}

