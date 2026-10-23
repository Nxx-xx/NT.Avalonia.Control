using System.Runtime.CompilerServices;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using NT.AvaloniaControl.Markdown;
using Reader = NT.AvaloniaControl.Markdown.Markdown;

namespace Markdown.Headless.Tests;

public sealed class AutomationTests
{
    [Fact]
    public Task MT20_FullReadOnlyValueIncludesUnrealizedTextAndHonorsHostName() => HeadlessTestScope.Run(() => MT20_FullReadOnlyValueIncludesUnrealizedTextAndHonorsHostNameCore());

    private async Task MT20_FullReadOnlyValueIncludesUnrealizedTextAndHonorsHostNameCore()
    {
        var reader = new Reader { Text = "## heading\n\nfirst\n\n```cs\n  code();\n```\n\n|A|B|\n|-|-|\n|x|y|\n\n" + Paragraphs(400) };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync();
            var peer = Peer(reader);
            AutomationProperties.SetName(reader, "帮助文档");
            Assert.Equal("帮助文档", peer.GetName());
            Assert.Equal(AutomationControlType.Document, peer.GetAutomationControlType());
            var value = Assert.IsAssignableFrom<IValueProvider>(peer.GetProvider<IValueProvider>());
            Assert.True(value.IsReadOnly);
            Assert.Equal(reader.Document.Text, value.Value);
            Assert.EndsWith("paragraph 399", value.Value);
            Assert.Contains("A\tB\nx\ty", value.Value);
            Assert.Contains("  code();", value.Value);
            Assert.Throws<InvalidOperationException>(() => value.SetValue("write"));
            Assert.Null(peer.GetProvider<ISelectionProvider>());
            Assert.Null(typeof(IValueProvider).Assembly.GetType("Avalonia.Automation.Provider.ITextProvider"));
            Assert.Null(typeof(IValueProvider).Assembly.GetType("Avalonia.Automation.Provider.ITextRangeProvider"));
            Assert.True(reader.DocumentPresenter!.RealizedBlockCount < reader.Document.Blocks.Length);
            var children = peer.GetChildren();
            Assert.Equal(reader.Document.Blocks.Length, children.Count);
            Assert.Equal(2, children[0].GetHeadingLevel());
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task MT20_LazyHierarchyDoesNotConstructAllDescendantPeers() => HeadlessTestScope.Run(() => MT20_LazyHierarchyDoesNotConstructAllDescendantPeersCore());

    private async Task MT20_LazyHierarchyDoesNotConstructAllDescendantPeersCore()
    {
        var reader = new Reader { Text = string.Join("\n", Enumerable.Range(0, 300).Select(i => $"- item {i} [link](https://example.test/{i})")) };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync();
            var peer = Peer(reader);
            Assert.Equal(0, peer.CachedLogicalPeerCount);
            var list = Assert.Single(peer.GetChildren());
            Assert.Equal(AutomationControlType.List, list.GetAutomationControlType());
            Assert.Equal(1, peer.CachedLogicalPeerCount);
            var items = list.GetChildren();
            Assert.Equal(300, items.Count);
            Assert.Equal(301, peer.CachedLogicalPeerCount);
            Assert.All(items, item => Assert.Same(list, item.GetParent()));
            Assert.True(reader.Document.Descendants().Count() > peer.CachedLogicalPeerCount);
            var paragraph = Assert.Single(items[^1].GetChildren());
            Assert.Contains(paragraph.GetChildren(), child => child.GetAutomationControlType() == AutomationControlType.Hyperlink);
            Assert.Equal(303, peer.CachedLogicalPeerCount);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task MT20_OffscreenLinkCanBeReadInvokedAndBroughtIntoViewWithoutSelection() => HeadlessTestScope.Run(() => MT20_OffscreenLinkCanBeReadInvokedAndBroughtIntoViewWithoutSelectionCore());

    private async Task MT20_OffscreenLinkCanBeReadInvokedAndBroughtIntoViewWithoutSelectionCore()
    {
        var reader = new Reader { Text = Paragraphs(300) + "\n\n[far link](https://example.test/far \"title\")" };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync();
            var peer = Peer(reader);
            var last = peer.GetChildren()[^1];
            var link = Assert.Single(last.GetChildren());
            Assert.True(link.IsOffscreen());
            Assert.Equal("far link", link.GetName());
            Assert.Equal("https://example.test/far", link.GetHelpText());
            Assert.Same(last, link.GetParent());
            string? invoked = null;
            reader.LinkClicked += (_, args) => { invoked = args.Url; args.Handled = true; };
            var selection = reader.Selection;
            link.GetProvider<IInvokeProvider>()!.Invoke();
            Assert.Equal("https://example.test/far", invoked);
            link.BringIntoView();
            await Flush(window);
            Assert.False(link.IsOffscreen(), $"Logical link bounds={link.GetBoundingRectangle()}; document bounds={peer.GetBoundingRectangle()}; realized={reader.DocumentPresenter!.RealizedBlockCount}");
            Assert.Equal(selection, reader.Selection);
            link.SetFocus();
            Assert.True(link.HasKeyboardFocus());
            Assert.True(reader.Selection.IsEmpty);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task MT20_LogicalReadOnlyTaskStateHasNoToggleOrInvoke() => HeadlessTestScope.Run(() => MT20_LogicalReadOnlyTaskStateHasNoToggleOrInvokeCore());

    private async Task MT20_LogicalReadOnlyTaskStateHasNoToggleOrInvokeCore()
    {
        var reader = new Reader { Text = "- [x] xylophone done\n- [ ] unfinished" };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync();
            var tasks = Descendants(Peer(reader)).Where(peer => peer.GetAutomationControlType() == AutomationControlType.CheckBox).ToArray();
            Assert.Equal(2, tasks.Length);
            Assert.Equal("xylophone done", tasks[0].GetName());
            Assert.Equal("true", tasks[0].GetProvider<IValueProvider>()!.Value);
            Assert.Equal("false", tasks[1].GetProvider<IValueProvider>()!.Value);
            Assert.All(tasks, task =>
            {
                Assert.True(task.GetProvider<IValueProvider>()!.IsReadOnly);
                Assert.Null(task.GetProvider<IToggleProvider>());
                Assert.Null(task.GetProvider<IInvokeProvider>());
                Assert.False(task.IsKeyboardFocusable());
                Assert.Throws<InvalidOperationException>(() => task.GetProvider<IValueProvider>()!.SetValue("false"));
                Assert.Contains("readOnly=true", task.GetItemStatus());
            });
            Assert.Equal("- [x] xylophone done\n- [ ] unfinished", reader.Text);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task MT20_TextRangesNormalizeUnicodeCaptureSelectionAndRejectStaleSnapshots() => HeadlessTestScope.Run(() => MT20_TextRangesNormalizeUnicodeCaptureSelectionAndRejectStaleSnapshotsCore());

    private async Task MT20_TextRangesNormalizeUnicodeCaptureSelectionAndRejectStaleSnapshotsCore()
    {
        var reader = new Reader { Text = "a😀b e\u0301 👩‍💻" };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync();
            var peer = Peer(reader);
            var range = peer.GetTextRange(2, 1);
            Assert.Equal("😀", range.Text);
            Assert.Equal(1, range.Start);
            Assert.Equal(2, range.Length);
            range.Select();
            Assert.Equal("😀", reader.SelectedText);
            var selection = peer.GetSelectionRange();
            Assert.Equal(reader.Selection, selection.Selection);
            Assert.Equal("😀", selection.Text);
            Assert.Contains("selectionStart=1", peer.GetItemStatus());
            Assert.Throws<ArgumentOutOfRangeException>(() => peer.GetTextRange(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => peer.GetTextRange(0, int.MaxValue));
            reader.Text = "new document";
            // Invalidate immediately when replacement begins, before its background parse commits.
            Assert.False(range.IsCurrent);
            Assert.Throws<InvalidOperationException>(range.Select);
            await reader.WhenUpdatedAsync();
            Assert.False(range.IsCurrent);
            Assert.Equal("😀", range.Text);
            Assert.Throws<InvalidOperationException>(range.Select);
            Assert.Throws<InvalidOperationException>(range.BringIntoView);
            Assert.True(reader.Selection.IsEmpty);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task MT20_ReadOnlyRangeRespectsDisabledSelectionAndValueChangesNotifyOnce() => HeadlessTestScope.Run(() => MT20_ReadOnlyRangeRespectsDisabledSelectionAndValueChangesNotifyOnceCore());

    private async Task MT20_ReadOnlyRangeRespectsDisabledSelectionAndValueChangesNotifyOnceCore()
    {
        var reader = new Reader { Text = "initial" };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync();
            var peer = Peer(reader);
            var values = new List<AutomationPropertyChangedEventArgs>();
            var selectionChanges = 0;
            var blockValues = new List<AutomationPropertyChangedEventArgs>();
            var block = Assert.Single(peer.GetChildren());
            block.PropertyChanged += (_, args) => { if (args.Property == ValuePatternIdentifiers.ValueProperty) blockValues.Add(args); };
            peer.PropertyChanged += (_, args) =>
            {
                if (args.Property == ValuePatternIdentifiers.ValueProperty) values.Add(args);
                if (args.Property == AutomationElementIdentifiers.ItemStatusProperty) selectionChanges++;
            };
            var range = peer.GetTextRange(0, 3);
            range.Select();
            Assert.Equal(1, selectionChanges);
            reader.SelectionEnabled = false;
            Assert.Throws<InvalidOperationException>(range.Select);
            var stream = reader.BeginStreaming(); stream.Append(" appended");
            await stream.CompleteAsync();
            var change = Assert.Single(values);
            Assert.Equal("initial", change.OldValue);
            Assert.Equal("initial appended", change.NewValue);
            Assert.Single(blockValues);
            Assert.Equal("initial appended", block.GetProvider<IValueProvider>()!.Value);
            Assert.Equal(reader.Document.Text, peer.Value);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task MT10_LinksSurviveUnchangedAppendButRemovedPeersCannotInvokeAnotherLink() => HeadlessTestScope.Run(() => MT10_LinksSurviveUnchangedAppendButRemovedPeersCannotInvokeAnotherLinkCore());

    private async Task MT10_LinksSurviveUnchangedAppendButRemovedPeersCannotInvokeAnotherLinkCore()
    {
        var reader = new Reader { Text = "[first](https://example.test/first)" };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync();
            var peer = Peer(reader);
            var link = Assert.Single(Assert.Single(peer.GetChildren()).GetChildren());
            var identity = link.GetAutomationId();
            var session = reader.BeginStreaming(); session.Append("\n\nnew paragraph");
            await session.CompleteAsync();
            Assert.Same(link, peer.GetChildren()[0].GetChildren()[0]);
            Assert.Equal(identity, link.GetAutomationId());
            var invocations = 0;
            reader.LinkClicked += (_, _) => invocations++;
            link.GetProvider<IInvokeProvider>()!.Invoke();
            Assert.Equal(1, invocations);
            reader.Text = "[replacement](https://example.test/replacement)";
            await reader.WhenUpdatedAsync();
            Assert.False(link.IsEnabled());
            Assert.True(link.IsOffscreen());
            Assert.Throws<ElementNotEnabledException>(() => link.GetProvider<IInvokeProvider>()!.Invoke());
            Assert.Equal(1, invocations);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task MT21_ReplacementPrunesOldLogicalPeersAndTheyCanBeCollected() => HeadlessTestScope.Run(() => MT21_ReplacementPrunesOldLogicalPeersAndTheyCanBeCollectedCore());

    private async Task MT21_ReplacementPrunesOldLogicalPeersAndTheyCanBeCollectedCore()
    {
        var reader = new Reader { Text = "[old link](https://example.test/old)" };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync();
            var peer = Peer(reader);
            var old = CaptureOldLink(peer);
            for (var i = 0; i < 20; i++)
            {
                reader.Text = $"[new link {i}](https://example.test/{i})";
                await reader.WhenUpdatedAsync();
                _ = Assert.Single(peer.GetChildren()).GetChildren();
                Assert.Equal(2, peer.CachedLogicalPeerCount);
            }
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            Assert.False(old.TryGetTarget(out _));
        }
        finally { window.Close(); }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<AutomationPeer> CaptureOldLink(MarkdownAutomationPeer peer) =>
        new(Assert.Single(Assert.Single(peer.GetChildren()).GetChildren()));

    [Fact]
    public Task MT20_RangesKeepWholeCombiningZwjAndRtlClustersAndRejectNewGenerationImmediately() => HeadlessTestScope.Run(() => MT20_RangesKeepWholeCombiningZwjAndRtlClustersAndRejectNewGenerationImmediatelyCore());

    private async Task MT20_RangesKeepWholeCombiningZwjAndRtlClustersAndRejectNewGenerationImmediatelyCore()
    {
        const string source = "A e\u0301 👩‍💻 בִּ שלום";
        var reader = new Reader { Text = source };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync();
            var peer = Peer(reader);
            Assert.Equal("e\u0301", peer.GetTextRange(3, 1).Text);
            var emoji = peer.GetTextRange(source.IndexOf("👩", StringComparison.Ordinal) + 2, 1);
            Assert.Equal("👩‍💻", emoji.Text);
            var rtl = peer.GetTextRange(source.IndexOf("ב", StringComparison.Ordinal) + 1, 1);
            Assert.Equal("בִּ", rtl.Text);
            emoji.Select();
            Assert.Equal("👩‍💻", reader.SelectedText);
            var document = reader.Document;
            var session = reader.BeginStreaming();
            Assert.Same(document, reader.Document);
            Assert.False(emoji.IsCurrent);
            Assert.False(rtl.IsCurrent);
            Assert.Throws<InvalidOperationException>(emoji.Select);
            Assert.Throws<InvalidOperationException>(rtl.BringIntoView);
            Assert.Equal(MarkdownUpdateStatus.Completed, (await session.CompleteAsync()).Status);
            var current = peer.GetSelectionRange();
            Assert.True(current.IsCurrent);
            window.Content = null;
            Assert.False(current.IsCurrent);
            Assert.Throws<InvalidOperationException>(current.Select);
            Assert.Equal(source, peer.Value);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task MT21_ReattachmentKeepsOnePeerAndOneValueNotificationPerCommit() => HeadlessTestScope.Run(() => MT21_ReattachmentKeepsOnePeerAndOneValueNotificationPerCommitCore());

    private async Task MT21_ReattachmentKeepsOnePeerAndOneValueNotificationPerCommitCore()
    {
        var reader = new Reader { Text = "initial" };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync();
            var peer = Peer(reader);
            var changes = 0;
            peer.PropertyChanged += (_, args) => { if (args.Property == ValuePatternIdentifiers.ValueProperty) changes++; };
            for (var i = 0; i < 12; i++)
            {
                window.Content = null;
                window.Content = reader;
                Assert.Same(peer, Peer(reader));
                reader.Text = $"replacement {i}";
                await reader.WhenUpdatedAsync();
                Assert.Equal(i + 1, changes);
            }
        }
        finally { window.Content = null; window.Close(); }
    }

    [Fact]
    public Task MT20_CustomConvertersWithNativeTextKeepHeadingLinkAndTaskSemantics() => HeadlessTestScope.Run(() => MT20_CustomConvertersWithNativeTextKeepHeadingLinkAndTaskSemanticsCore());

    private async Task MT20_CustomConvertersWithNativeTextKeepHeadingLinkAndTaskSemanticsCore()
    {
        var reader = new Reader { Text = "## heading\n\n[custom link](https://example.test/custom)\n\n- [x] native task" };
        reader.ConfigureParser(new()
        {
            Converters =
            [
                new MarkdownNodeConverterRegistration<HeadingBlock>(_ => new("host-heading")),
                new MarkdownNodeConverterRegistration<LinkInline>(_ => new("host-link")),
                new MarkdownNodeConverterRegistration<TaskList>(_ => new("host-task"))
            ]
        });
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync();
            var peer = Peer(reader);
            Assert.Equal(2, peer.GetChildren()[0].GetHeadingLevel());
            var children = Descendants(peer).ToArray();
            var link = Assert.Single(children, child => child.GetAutomationControlType() == AutomationControlType.Hyperlink);
            var task = Assert.Single(children, child => child.GetAutomationControlType() == AutomationControlType.CheckBox);
            Assert.Equal("native task", task.GetName());
            Assert.Equal("true", task.GetProvider<IValueProvider>()!.Value);
            Assert.Null(task.GetProvider<IToggleProvider>());
            string? activated = null;
            reader.LinkClicked += (_, args) => { activated = args.Url; args.Handled = true; };
            link.GetProvider<IInvokeProvider>()!.Invoke();
            Assert.Equal("https://example.test/custom", activated);
            Assert.Contains(reader.Document.Descendants(), block => block.Kind == MarkdownBlockKind.Custom && block.SemanticKind == MarkdownBlockKind.Heading);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task MT21_DetachedReaderAndAutomationPeerAreCollectableAfterPendingUiWorkSettles() => HeadlessTestScope.Run(() => MT21_DetachedReaderAndAutomationPeerAreCollectableAfterPendingUiWorkSettlesCore());

    private async Task MT21_DetachedReaderAndAutomationPeerAreCollectableAfterPendingUiWorkSettlesCore()
    {
        var references = await CreateDetachedReferences();
        for (var i = 0; i < 10; i++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            if (!IsAlive(references.Reader) && !IsAlive(references.Peer)) return;
        }
        Assert.False(IsAlive(references.Reader), "Detached reader remained rooted after local parse and UI queues settled.");
        Assert.False(IsAlive(references.Peer), "Automation peer or its subscriptions retained a detached reader.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<(WeakReference<Reader> Reader, WeakReference<MarkdownAutomationPeer> Peer)> CreateDetachedReferences()
    {
        var reader = new Reader { Text = "[collectable](https://example.test/collectable)\n\n" + Paragraphs(80) };
        var window = Show(reader);
        await reader.WhenUpdatedAsync();
        var peer = Peer(reader);
        _ = peer.GetChildren()[0].GetChildren();
        reader.SetSelection(0, 5);
        var references = (new WeakReference<Reader>(reader), new WeakReference<MarkdownAutomationPeer>(peer));
        window.Content = null;
        window.Close();
        return references;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool IsAlive<T>(WeakReference<T> reference) where T : class => reference.TryGetTarget(out _);

    private static string Paragraphs(int count) => string.Join("\n\n", Enumerable.Range(0, count).Select(i => $"paragraph {i}"));
    private static Window Show(Reader reader) { var window = new Window { Width = 600, Height = 300, Content = reader }; window.Show(); return window; }
    private static MarkdownAutomationPeer Peer(Reader reader) => Assert.IsType<MarkdownAutomationPeer>(ControlAutomationPeer.CreatePeerForElement(reader));
    private static IEnumerable<AutomationPeer> Descendants(AutomationPeer peer)
    {
        foreach (var child in peer.GetChildren())
        { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
    private static async Task Flush(Window window)
    {
        await Task.Delay(20, TestContext.Current.CancellationToken);
        await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout, DispatcherPriority.Loaded);
    }
}

