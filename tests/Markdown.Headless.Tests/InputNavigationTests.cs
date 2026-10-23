using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Markdown;
using Reader = NT.AvaloniaControl.Markdown.Markdown;

namespace Markdown.Headless.Tests;

public sealed class InputNavigationTests
{
    [Fact]
    public Task MT05_PageKeysMoveByTheViewportAndShiftPreservesTheAnchorAcrossRecycling() => HeadlessTestScope.Run(() => MT05_PageKeysMoveByTheViewportAndShiftPreservesTheAnchorAcrossRecyclingCore());

    private async Task MT05_PageKeysMoveByTheViewportAndShiftPreservesTheAnchorAcrossRecyclingCore()
    {
        var reader = new Reader { Text = Paragraphs(120) };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync(); await Flush(window); Assert.True(reader.Focus());
            reader.SetSelection(0, 0);
            Press(window, Key.PageDown); await Flush(window);
            var first = reader.Selection.Active;
            Assert.True(first > 0);
            Assert.True(Scroll(reader).Offset.Y > 0);
            Press(window, Key.PageDown, RawInputModifiers.Shift); await Flush(window);
            Assert.Equal(first, reader.Selection.Anchor);
            Assert.True(reader.Selection.Active > first);
            Assert.Equal(reader.Selection, reader.Selection.Normalize(reader.Document.Text));
            var second = reader.Selection.Active;
            Press(window, Key.PageUp, RawInputModifiers.Shift); await Flush(window);
            Assert.Equal(first, reader.Selection.Anchor);
            Assert.True(reader.Selection.Active < second);
            for (var i = 0; i < 8; i++) { Press(window, Key.PageDown); await Flush(window); }
            Assert.True(reader.Selection.Active > second);
            Assert.True(reader.DocumentPresenter!.RealizedBlockCount < reader.Document.Blocks.Length);
            Press(window, Key.Home, RawInputModifiers.Control); await Flush(window);
            Assert.Equal(default(MarkdownSelection), reader.Selection);
            Press(window, Key.End, RawInputModifiers.Control); await Flush(window);
            Assert.Equal(new MarkdownSelection(reader.Document.Text.Length, reader.Document.Text.Length), reader.Selection);
            Press(window, Key.PageUp); await Flush(window);
            Assert.True(reader.Selection.Active < reader.Document.Text.Length);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task MT05_HomeEndSelectLogicalCodeLinesAndCtrlCopyCapturesTheWholeProjection() => HeadlessTestScope.Run(() => MT05_HomeEndSelectLogicalCodeLinesAndCtrlCopyCapturesTheWholeProjectionCore());

    private async Task MT05_HomeEndSelectLogicalCodeLinesAndCtrlCopyCapturesTheWholeProjectionCore()
    {
        var reader = new Reader { Text = "before\n\n```text\nalpha😀\nbeta e\u0301\nlast\n```\n\nafter" };
        var window = Show(reader);
        string? copied = null;
        reader.CopyingToClipboard += (_, args) => { copied = args.Text; args.Cancel = true; };
        try
        {
            await reader.WhenUpdatedAsync(); await Flush(window); Assert.True(reader.Focus());
            var begin = reader.Document.Text.IndexOf("beta", StringComparison.Ordinal);
            reader.SetSelection(begin + 2, begin + 2);
            Press(window, Key.Home);
            Assert.Equal(new MarkdownSelection(begin, begin), reader.Selection);
            Press(window, Key.End, RawInputModifiers.Shift);
            Assert.Equal("beta e\u0301", reader.SelectedText);
            Press(window, Key.A, RawInputModifiers.Control);
            Assert.Equal(reader.Document.Text, reader.SelectedText);
            Press(window, Key.C, RawInputModifiers.Control);
            Assert.Equal(reader.Document.Text, copied);
            Press(window, Key.Escape);
            Assert.Empty(reader.SelectedText);
            Assert.False(reader.CanCopy);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task MT05_OutOfBoundsDragAutoscrollsAndReleaseStopsItsTimer() => HeadlessTestScope.Run(() => MT05_OutOfBoundsDragAutoscrollsAndReleaseStopsItsTimerCore());

    private async Task MT05_OutOfBoundsDragAutoscrollsAndReleaseStopsItsTimerCore()
    {
        var reader = new Reader { Text = Paragraphs(150) };
        var window = Show(reader);
        IPointer? pointer = null;
        reader.AddHandler(InputElement.PointerPressedEvent, (_, args) => pointer = args.Pointer, RoutingStrategies.Tunnel, true);
        try
        {
            await reader.WhenUpdatedAsync(); await Flush(window);
            var start = PointAt(window, reader, 0);
            window.MouseDown(start, MouseButton.Left);
            Assert.NotNull(pointer); Assert.Same(reader, pointer.Captured);
            var outside = new Point(start.X, window.ClientSize.Height + 60);
            window.MouseMove(outside, RawInputModifiers.LeftMouseButton);
            var before = Scroll(reader).Offset.Y;
            await WaitFor(() => Scroll(reader).Offset.Y > before + 20 && reader.Selection.Active > 0, window);
            Assert.Equal(0, reader.Selection.Anchor);
            Assert.True(reader.Selection.Length > 0);
            window.MouseUp(outside, MouseButton.Left);
            Assert.Null(pointer.Captured);
            await Flush(window);
            var releasedOffset = Scroll(reader).Offset;
            var releasedSelection = reader.Selection;
            await Task.Delay(120, TestContext.Current.CancellationToken); window.UpdateLayout();
            Assert.Equal(releasedOffset, Scroll(reader).Offset);
            Assert.Equal(releasedSelection, reader.Selection);
        }
        finally { reader.CancelInteraction(); window.Close(); }
    }

    [Fact]
    public Task MT17_RuntimeTemplateReplacementReleasesOldCaptureAndKeepsLogicalAutomation() => HeadlessTestScope.Run(() => MT17_RuntimeTemplateReplacementReleasesOldCaptureAndKeepsLogicalAutomationCore());

    private async Task MT17_RuntimeTemplateReplacementReleasesOldCaptureAndKeepsLogicalAutomationCore()
    {
        var reader = new Reader { Text = "[logical link](https://example.test/retained)\n\n" + Paragraphs(60) };
        var window = Show(reader);
        IPointer? pointer = null;
        reader.AddHandler(InputElement.PointerPressedEvent, (_, args) => pointer = args.Pointer, RoutingStrategies.Tunnel, true);
        try
        {
            await reader.WhenUpdatedAsync(); await Flush(window);
            var peer = Assert.IsType<MarkdownAutomationPeer>(ControlAutomationPeer.CreatePeerForElement(reader));
            var logicalLink = Assert.Single(peer.GetChildren()[0].GetChildren());
            var oldPresenter = reader.DocumentPresenter!;
            var oldVisualPeer = ControlAutomationPeer.CreatePeerForElement(oldPresenter)!;
            var first = PointAt(window, reader, 0);
            window.MouseDown(first, MouseButton.Left);
            window.MouseMove(new Point(first.X, window.ClientSize.Height + 60), RawInputModifiers.LeftMouseButton);
            Assert.NotNull(pointer); Assert.Same(reader, pointer.Captured);
            reader.Template = NewTemplate();
            await Flush(window);
            Assert.Null(pointer.Captured);
            Assert.NotSame(oldPresenter, reader.DocumentPresenter);
            Assert.Equal(0, oldPresenter.RealizedBlockCount);
            Assert.Equal(default, oldVisualPeer.GetBoundingRectangle());
            Assert.Same(peer, ControlAutomationPeer.CreatePeerForElement(reader));
            Assert.Same(logicalLink, peer.GetChildren()[0].GetChildren()[0]);
            var offset = Scroll(reader).Offset;
            var selection = reader.Selection;
            window.MouseMove(new Point(first.X, window.ClientSize.Height + 80), RawInputModifiers.LeftMouseButton);
            await Task.Delay(100, TestContext.Current.CancellationToken); window.UpdateLayout();
            Assert.Equal(offset, Scroll(reader).Offset);
            Assert.Equal(selection, reader.Selection);
            window.MouseUp(first, MouseButton.Left);
            var newFirst = PointAt(window, reader, 0);
            var last = PointAt(window, reader, 3);
            window.MouseDown(newFirst, MouseButton.Left); window.MouseMove(last, RawInputModifiers.LeftMouseButton); window.MouseUp(last, MouseButton.Left);
            Assert.True(reader.Selection.Length > 0);
        }
        finally { reader.CancelInteraction(); window.Close(); }
    }

    [Fact]
    public Task MT17_DetachReleasesCaptureAndReattachDoesNotResumeTheOldDrag() => HeadlessTestScope.Run(() => MT17_DetachReleasesCaptureAndReattachDoesNotResumeTheOldDragCore());

    private async Task MT17_DetachReleasesCaptureAndReattachDoesNotResumeTheOldDragCore()
    {
        var reader = new Reader { Text = Paragraphs(80) };
        var window = Show(reader);
        IPointer? pointer = null;
        reader.AddHandler(InputElement.PointerPressedEvent, (_, args) => pointer = args.Pointer, RoutingStrategies.Tunnel, true);
        try
        {
            await reader.WhenUpdatedAsync(); await Flush(window);
            var start = PointAt(window, reader, 0);
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(new Point(start.X, window.ClientSize.Height + 60), RawInputModifiers.LeftMouseButton);
            Assert.NotNull(pointer); Assert.Same(reader, pointer.Captured);
            window.Content = null;
            Assert.Null(pointer.Captured);
            window.Content = reader;
            await Flush(window);
            var selection = reader.Selection;
            window.MouseMove(new Point(start.X, window.ClientSize.Height + 100), RawInputModifiers.LeftMouseButton);
            await Task.Delay(100, TestContext.Current.CancellationToken); window.UpdateLayout();
            Assert.Equal(selection, reader.Selection);
            window.MouseUp(start, MouseButton.Left);
            Assert.NotNull(reader.DocumentPresenter);
            Assert.Equal(reader.Document.Text, Assert.IsType<MarkdownAutomationPeer>(ControlAutomationPeer.CreatePeerForElement(reader)).Value);
        }
        finally { reader.CancelInteraction(); window.Close(); }
    }

    [Fact]
    public Task MT04_FootnotePointerActivationScrollsToItsLogicalTargetWithoutExternalNavigation() => HeadlessTestScope.Run(() => MT04_FootnotePointerActivationScrollsToItsLogicalTargetWithoutExternalNavigationCore());

    private async Task MT04_FootnotePointerActivationScrollsToItsLogicalTargetWithoutExternalNavigationCore()
    {
        var reader = new Reader { Text = "intro[^note]\n\n" + Paragraphs(120) + "\n\n[^note]: footnote target" };
        var window = Show(reader);
        var external = 0;
        reader.LinkClicked += (_, _) => external++;
        try
        {
            await reader.WhenUpdatedAsync(); await Flush(window);
            var reference = reader.Document.Descendants().SelectMany(block => block.Inlines).First(inline => inline.SemanticKind == MarkdownInlineKind.FootnoteReference);
            var footnote = reader.Document.Descendants().Single(block => block.SemanticKind == MarkdownBlockKind.Footnote);
            var point = PointAt(window, reader, reference.TextStart + 1);
            window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left);
            await Flush(window);
            Assert.True(reader.Selection.IsEmpty);
            Assert.Equal(footnote.TextStart, reader.Selection.Active);
            Assert.Equal(0, external);
            Assert.True(Scroll(reader).Offset.Y > 0);
            var peer = Assert.IsType<MarkdownAutomationPeer>(ControlAutomationPeer.CreatePeerForElement(reader));
            var target = peer.GetChildren()[^1].GetChildren().Single(child => child.GetAutomationId() == $"Markdown.Block.{footnote.Id}");
            Assert.False(target.IsOffscreen());
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task MT10_FullReplacementClearsOldLinkFocusBeforeEnterCanActivateANewLink() => HeadlessTestScope.Run(() => MT10_FullReplacementClearsOldLinkFocusBeforeEnterCanActivateANewLinkCore());

    private async Task MT10_FullReplacementClearsOldLinkFocusBeforeEnterCanActivateANewLinkCore()
    {
        var reader = new Reader { Text = "[first](https://example.test/first) and [second](https://example.test/second)" };
        var window = Show(reader);
        var activated = new List<string>();
        reader.LinkClicked += (_, args) => { activated.Add(args.Url); args.Handled = true; };
        try
        {
            await reader.WhenUpdatedAsync(); await Flush(window); Assert.True(reader.Focus());
            Press(window, Key.Tab); Press(window, Key.Tab); Press(window, Key.Enter);
            Assert.Equal(["https://example.test/second"], activated);
            // Both old second and new link start at logical UTF-16 position 10.
            reader.Text = "0123456789[new](https://example.test/new)";
            await reader.WhenUpdatedAsync(); await Flush(window);
            Assert.Equal(default(MarkdownSelection), reader.Selection);
            Press(window, Key.Enter);
            Assert.Equal(["https://example.test/second"], activated);
            Press(window, Key.Tab); Press(window, Key.Enter);
            Assert.Equal(["https://example.test/second", "https://example.test/new"], activated);
        }
        finally { window.Close(); }
    }

    private static FuncControlTemplate<Reader> NewTemplate() => new((_, scope) =>
    {
        var presenter = new MarkdownDocumentPresenter { Name = "PART_DocumentPresenter" };
        var scroll = new ScrollViewer { Name = "PART_ScrollViewer", Content = presenter };
        scope.Register("PART_DocumentPresenter", presenter); scope.Register("PART_ScrollViewer", scroll);
        return scroll;
    });
    private static Window Show(Reader reader) { var window = new Window { Width = 600, Height = 280, Content = reader }; window.Show(); return window; }
    private static ScrollViewer Scroll(Reader reader) => reader.GetVisualDescendants().OfType<ScrollViewer>().Single(scroll => scroll.Name == "PART_ScrollViewer");
    private static string Paragraphs(int count) => string.Join("\n\n", Enumerable.Range(0, count).Select(i => $"paragraph {i:D3} 中😀 e\u0301 text"));
    private static Point PointAt(Window window, Reader reader, int position)
    {
        var text = reader.GetVisualDescendants().OfType<MarkdownTextPresenter>().First(text => position >= text.TextStart && position <= text.TextStart + text.TextLength);
        var rect = text.TextLayout.HitTestTextPosition(position - text.TextStart);
        return text.TranslatePoint(new Point(rect.X + text.Padding.Left + .1, rect.Y + rect.Height / 2 + text.Padding.Top), window)!.Value;
    }
    private static void Press(Window window, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    { window.KeyPress(key, modifiers, PhysicalKey.None, null); window.KeyRelease(key, modifiers, PhysicalKey.None, null); window.UpdateLayout(); }
    private static async Task Flush(Window window)
    { await Task.Delay(20, TestContext.Current.CancellationToken); await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout, DispatcherPriority.Loaded); }
    private static async Task WaitFor(Func<bool> predicate, Window window)
    {
        for (var i = 0; i < 80; i++) { window.UpdateLayout(); if (predicate()) return; await Task.Delay(20, TestContext.Current.CancellationToken); }
        Assert.Fail("Expected input/scroll state did not arrive within the bounded wait.");
    }
}

