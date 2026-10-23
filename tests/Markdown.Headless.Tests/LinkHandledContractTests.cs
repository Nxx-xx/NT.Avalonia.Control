using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Markdown;
using Reader = NT.AvaloniaControl.Markdown.Markdown;

namespace Markdown.Headless.Tests;

public sealed class LinkHandledContractTests
{
    private const string Target = "https://example.test/guide.md";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task HandledControlsDefaultVisitedMarkingWithoutChangingEventOrSelection(bool handled) => HeadlessTestScope.Run(async () =>
    {
        var reader = new Reader
        {
            BaseUri = new Uri("https://example.test/"),
            Text = $"[relative](guide.md \"Guide\") [absolute]({Target})"
        };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync(); window.UpdateLayout();
            reader.SetSelection(1, 5);
            var selection = reader.Selection;
            var calls = 0;
            reader.LinkClicked += (_, args) =>
            {
                Assert.False(args.Handled);
                Assert.Equal(Target, args.Url);
                Assert.Equal("Guide", args.Title);
                calls++;
                args.Handled = handled;
            };

            Invoke(reader, "relative");

            Assert.Equal(1, calls);
            Assert.Equal(selection, reader.Selection);
            var links = LinkSpans(reader);
            Assert.Equal(2, links.Length);
            Assert.All(links, span => Assert.Equal(!handled, span.Classes.Contains("visited")));
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task NoHandlerRetainsDefaultVisitedMarking() => HeadlessTestScope.Run(async () =>
    {
        var reader = new Reader { Text = $"[link]({Target})" };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync(); window.UpdateLayout();
            Assert.DoesNotContain("visited", Assert.Single(LinkSpans(reader)).Classes);

            Invoke(reader, "link");

            Assert.Contains("visited", Assert.Single(LinkSpans(reader)).Classes);
        }
        finally { window.Close(); }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task AllSubscribersRunBeforeTheFinalHandledValueControlsVisited(bool finalHandled) => HeadlessTestScope.Run(async () =>
    {
        var reader = new Reader { Text = $"[link]({Target})" };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync(); window.UpdateLayout();
            MarkdownLinkEventArgs? captured = null;
            var calls = new List<int>();
            reader.LinkClicked += (_, args) => { captured = args; args.Handled = true; calls.Add(1); };
            reader.LinkClicked += (_, args) =>
            {
                Assert.Same(captured, args);
                Assert.True(args.Handled);
                args.Handled = finalHandled;
                calls.Add(2);
            };

            Invoke(reader, "link");

            Assert.Equal(new[] { 1, 2 }, calls);
            Assert.Equal(!finalHandled, Assert.Single(LinkSpans(reader)).Classes.Contains("visited"));
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task HandledDoesNotClearAnEarlierVisit() => HeadlessTestScope.Run(async () =>
    {
        var reader = new Reader { Text = $"[link]({Target})" };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync(); window.UpdateLayout();
            var calls = 0;
            reader.LinkClicked += (_, args) => args.Handled = ++calls > 1;
            Invoke(reader, "link");
            Assert.Contains("visited", Assert.Single(LinkSpans(reader)).Classes);

            Invoke(reader, "link");

            Assert.Equal(2, calls);
            Assert.Contains("visited", Assert.Single(LinkSpans(reader)).Classes);
        }
        finally { window.Close(); }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ReentrantReplacementOrConfigurationCannotVisitTheNextDocument(bool configure) => HeadlessTestScope.Run(async () =>
    {
        var reader = new Reader { Text = $"[link]({Target})" };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync(); window.UpdateLayout();
            var original = reader.Document;
            var originalSpan = Assert.Single(LinkSpans(reader));
            var calls = 0;
            reader.LinkClicked += (_, args) =>
            {
                Assert.False(args.Handled);
                Assert.Equal(Target, args.Url);
                calls++;
                if (configure) reader.ConfigurePipeline(null);
                else reader.Text = $"replacement [link]({Target})";
                // Invalidation is immediate even before a new snapshot can commit.
                Assert.Same(original, reader.Document);
            };

            Invoke(reader, "link");

            Assert.DoesNotContain("visited", originalSpan.Classes);
            Assert.Equal(MarkdownUpdateStatus.Completed, (await reader.WhenUpdatedAsync()).Status);
            window.UpdateLayout();
            Assert.NotSame(original, reader.Document);
            Assert.Equal(1, calls);
            Assert.DoesNotContain("visited", Assert.Single(LinkSpans(reader)).Classes);
            Assert.Equal(configure ? $"[link]({Target})" : $"replacement [link]({Target})", reader.Document.Source);
        }
        finally { window.Close(); }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ReentrantDetachCannotVisitARemountedReader(bool reattachInsideHandler) => HeadlessTestScope.Run(async () =>
    {
        var reader = new Reader { Text = $"[link]({Target})" };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync(); window.UpdateLayout();
            EventHandler<MarkdownLinkEventArgs> detach = (_, args) =>
            {
                Assert.False(args.Handled);
                window.Content = null;
                if (reattachInsideHandler) { window.Content = reader; window.UpdateLayout(); }
            };
            reader.LinkClicked += detach;

            Invoke(reader, "link");

            reader.LinkClicked -= detach;
            if (!reattachInsideHandler) window.Content = reader;
            await reader.WhenUpdatedAsync();
            await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout, DispatcherPriority.Loaded);
            Assert.True(reader.HasPresentationEnvironment);
            Assert.DoesNotContain("visited", Assert.Single(LinkSpans(reader)).Classes);

            // A subsequent ordinary activation still has its default behavior.
            Invoke(reader, "link");
            Assert.Contains("visited", Assert.Single(LinkSpans(reader)).Classes);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task HandlerExceptionPropagatesWithoutDefaultVisitedSideEffects() => HeadlessTestScope.Run(async () =>
    {
        var reader = new Reader { Text = $"[link]({Target})" };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync(); window.UpdateLayout();
            reader.SetSelection(0, 2);
            var selection = reader.Selection;
            var expected = new InvalidOperationException("host navigation failed");
            EventHandler<MarkdownLinkEventArgs> fail = (_, _) => throw expected;
            reader.LinkClicked += fail;

            Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => Invoke(reader, "link")));

            Assert.DoesNotContain("visited", Assert.Single(LinkSpans(reader)).Classes);
            Assert.Equal(selection, reader.Selection);
            reader.LinkClicked -= fail;
            Invoke(reader, "link");
            Assert.Contains("visited", Assert.Single(LinkSpans(reader)).Classes);
        }
        finally { window.Close(); }
    });

    private static void Invoke(Reader reader, string name) => Descendants(ControlAutomationPeer.CreatePeerForElement(reader)!)
        .Single(peer => peer.GetName() == name && peer.GetProvider<IInvokeProvider>() is not null)
        .GetProvider<IInvokeProvider>()!.Invoke();

    private static IEnumerable<AutomationPeer> Descendants(AutomationPeer peer)
    {
        foreach (var child in peer.GetChildren())
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static MarkdownSpan[] LinkSpans(Reader reader) => reader.GetVisualDescendants().OfType<MarkdownTextPresenter>()
        .SelectMany(text => text.GetLogicalDescendants().OfType<MarkdownSpan>())
        .Where(span => span.Url is not null).ToArray();

    private static Window Show(Reader reader)
    {
        var window = new Window { Width = 600, Height = 280, Content = reader };
        window.Show();
        return window;
    }
}
