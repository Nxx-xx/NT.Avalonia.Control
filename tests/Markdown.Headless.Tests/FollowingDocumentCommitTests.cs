using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Markdig.Syntax;
using NT.AvaloniaControl.Markdown;
using Reader = NT.AvaloniaControl.Markdown.Markdown;

namespace Markdown.Headless.Tests;

public sealed class FollowingDocumentCommitTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task TheUpdatedEventAlreadyHasTheActualNewTailInItsViewport(bool streaming) =>
        HeadlessTestScope.Run(() => TheUpdatedEventAlreadyHasTheActualNewTailInItsViewportCore(streaming));

    private static async Task TheUpdatedEventAlreadyHasTheActualNewTailInItsViewportCore(bool streaming)
    {
        var reader = new Reader { Text = Paragraphs(40), AutoScrollToEnd = true };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync();
            await Settle(window);
            reader.ScrollToEnd();
            await Settle(window);
            var scroll = Scroll(reader);
            AssertTailVisible(reader, scroll);
            if (streaming) reader.SetSelection(10, 22);
            var previousSelection = reader.SelectedText;
            var nextSource = reader.Text + "\n\n" + Paragraphs(30, 40) + "\n\nTHE NEW FINAL TAIL";
            TailGeometry? atUpdated = null;
            reader.Updated += (_, _) => atUpdated = CaptureTail(reader, scroll);

            MarkdownUpdateResult result;
            if (streaming)
            {
                using var session = reader.BeginStreaming();
                session.Append(nextSource.AsMemory(reader.Text!.Length));
                result = await session.CompleteAsync();
            }
            else
            {
                reader.Text = nextSource;
                result = await reader.WhenUpdatedAsync();
            }

            Assert.Equal(MarkdownUpdateStatus.Completed, result.Status);
            Assert.Equal(nextSource, reader.Document.Source);
            Assert.True(atUpdated.HasValue);
            // This geometry was captured synchronously inside Updated, before
            // any later Loaded restoration or test drain can make it correct.
            AssertTailGeometry(atUpdated.GetValueOrDefault());
            AssertTailVisible(reader, scroll);
            if (streaming) Assert.Equal(previousSelection, reader.SelectedText);

            // A completed following scope must relinquish the viewport immediately.
            // One subsequent manual jump must realize its own range without a drain.
            scroll.Offset = default;
            window.UpdateLayout();
            var firstId = reader.Document.Blocks[0].Id;
            Assert.Contains(reader.DocumentPresenter!.Children.OfType<MarkdownBlockPresenter>(),
                block => block.NodeId == firstId && block.Bounds.Bottom > scroll.Offset.Y &&
                    block.Bounds.Top < scroll.Offset.Y + scroll.Viewport.Height);
        }
        finally { window.Close(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task LayoutReentryKeepsAHostOffsetOrCtrlHomeAheadOfFollowing(bool keyboard) =>
        HeadlessTestScope.Run(() => LayoutReentryKeepsAHostOffsetOrCtrlHomeAheadOfFollowingCore(keyboard));

    private static async Task LayoutReentryKeepsAHostOffsetOrCtrlHomeAheadOfFollowingCore(bool keyboard)
    {
        var reader = new Reader { Text = Paragraphs(40), AutoScrollToEnd = true };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync();
            await Settle(window);
            reader.ScrollToEnd();
            await Settle(window);
            var scroll = Scroll(reader);
            reader.Focus();
            var tail = "\n\n" + Paragraphs(30, 40) + "\n\nLAYOUT REENTRY FINAL TAIL";
            var nextSource = reader.Text + tail;
            var reentered = false;
            reader.LayoutUpdated += (_, _) =>
            {
                if (reentered || reader.Document.Source != nextSource) return;
                reentered = true;
                if (keyboard)
                {
                    window.KeyPress(Key.Home, RawInputModifiers.Control, PhysicalKey.None, null);
                    window.KeyRelease(Key.Home, RawInputModifiers.Control, PhysicalKey.None, null);
                }
                else scroll.Offset = new Vector(0, 180);
            };

            using var session = reader.BeginStreaming();
            session.Append(tail);
            Assert.Equal(MarkdownUpdateStatus.Completed, (await session.CompleteAsync()).Status);
            await Settle(window);
            Assert.True(reentered);
            Assert.InRange(Math.Abs(scroll.Offset.Y - (keyboard ? 0 : 180)), 0, 1);
            Assert.True(scroll.Offset.Y < scroll.Extent.Height - scroll.Viewport.Height - 24);
            if (keyboard) Assert.Equal(0, reader.Selection.Active);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task AHostOffsetReenteredDuringTheOwnScrollWriteIsNotOverwrittenByAnotherFollowPass() =>
        HeadlessTestScope.Run(AHostOffsetReenteredDuringTheOwnScrollWriteIsNotOverwrittenByAnotherFollowPassCore);

    private static async Task AHostOffsetReenteredDuringTheOwnScrollWriteIsNotOverwrittenByAnotherFollowPassCore()
    {
        var reader = new Reader { Text = Paragraphs(40), AutoScrollToEnd = true };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync();
            await Settle(window);
            reader.ScrollToEnd();
            await Settle(window);
            var scroll = Scroll(reader);
            var tail = "\n\n" + Paragraphs(30, 40) + "\n\nOFFSET REENTRY FINAL TAIL";
            var nextSource = reader.Text + tail;
            var reentered = false;
            scroll.PropertyChanged += (_, change) =>
            {
                if (reentered || change.Property != ScrollViewer.OffsetProperty ||
                    reader.Document.Source != nextSource ||
                    change.GetNewValue<Vector>().Y <= change.GetOldValue<Vector>().Y) return;
                reentered = true;
                scroll.Offset = new Vector(0, 180);
            };

            using var session = reader.BeginStreaming();
            session.Append(tail);
            Assert.Equal(MarkdownUpdateStatus.Completed, (await session.CompleteAsync()).Status);
            await Settle(window);
            Assert.True(reentered);
            Assert.InRange(Math.Abs(scroll.Offset.Y - 180), 0, 1);
            Assert.True(scroll.Offset.Y < scroll.Extent.Height - scroll.Viewport.Height - 24);
        }
        finally { window.Close(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ACustomNativeTextRootOrWrapperHasASettledVisibleTailAtUpdate(bool wrap) =>
        HeadlessTestScope.Run(() => ACustomNativeTextRootOrWrapperHasASettledVisibleTailAtUpdateCore(wrap));

    private static async Task ACustomNativeTextRootOrWrapperHasASettledVisibleTailAtUpdateCore(bool wrap)
    {
        var reader = new Reader { Text = Paragraphs(40), AutoScrollToEnd = true };
        reader.ConfigureParser(new()
        { Converters = [new MarkdownNodeConverterRegistration<ParagraphBlock>(_ => new("following-native"))] });
        reader.NodeRenderers = new(blocks: [new("following-native", context => wrap
            ? new Border { Padding = new Thickness(11), Child = context.CreateText() }
            : context.CreateText())]);
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync();
            await Settle(window);
            reader.ScrollToEnd();
            await Settle(window);
            var scroll = Scroll(reader);
            TailGeometry? atUpdated = null;
            reader.Updated += (_, _) => atUpdated = CaptureTail(reader, scroll);
            using var session = reader.BeginStreaming();
            session.Append("\n\n" + Paragraphs(30, 40) + "\n\nCUSTOM FINAL TAIL");
            var result = await session.CompleteAsync();
            Assert.Equal(MarkdownUpdateStatus.Completed, result.Status);
            Assert.False(result.LayoutPending);
            Assert.True(atUpdated.HasValue);
            AssertTailGeometry(atUpdated.GetValueOrDefault());
            Assert.Empty(reader.RenderingDiagnostics);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task AZeroHeightCustomTailCannotBeReportedAsAConfirmedVisibleFollowingLayout() =>
        HeadlessTestScope.Run(AZeroHeightCustomTailCannotBeReportedAsAConfirmedVisibleFollowingLayoutCore);

    private static async Task AZeroHeightCustomTailCannotBeReportedAsAConfirmedVisibleFollowingLayoutCore()
    {
        var reader = new Reader { Text = Paragraphs(40), AutoScrollToEnd = true };
        reader.ConfigureParser(new()
        {
            Converters = [new MarkdownNodeConverterRegistration<ParagraphBlock>(context =>
                new("following-zero", context.SourceText.Trim() == "ZERO FINAL TAIL" ? string.Empty : null))]
        });
        reader.NodeRenderers = new(blocks: [new("following-zero", context => context.Block.Text.Length == 0
            ? new Border { Height = 0 }
            : context.CreateText())]);
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync();
            await Settle(window);
            reader.ScrollToEnd();
            await Settle(window);
            var zeroScroll = Scroll(reader);
            zeroScroll.PropertyChanged += (_, change) =>
            {
                if (change.Property == ScrollViewer.OffsetProperty)
                    TestContext.Current.TestOutputHelper?.WriteLine($"zero offset {change.GetOldValue<Vector>()}->{change.GetNewValue<Vector>()}; extent={zeroScroll.Extent}; viewport={zeroScroll.Viewport}; pValid={reader.DocumentPresenter!.IsMeasureValid}/{reader.DocumentPresenter.IsArrangeValid}");
            };
            using var session = reader.BeginStreaming();
            session.Append("\n\nZERO FINAL TAIL");
            var result = await session.CompleteAsync();
            var last = reader.Document.Blocks[^1];
            TestContext.Current.TestOutputHelper?.WriteLine($"zero-tail result={result.Status}, pending={result.LayoutPending}, text=<{last.Text}>, roots=" +
                string.Join(";", reader.DocumentPresenter!.Children.Select(child => $"{child.GetType().Name}:{child.Bounds}:desired={child.DesiredSize}")));
            TestContext.Current.TestOutputHelper?.WriteLine($"zero pBounds={reader.DocumentPresenter!.Bounds}; extent={zeroScroll.Extent}; viewport={zeroScroll.Viewport}; offset={zeroScroll.Offset}");
            Assert.Equal(MarkdownUpdateStatus.Completed, result.Status);
            Assert.True(result.LayoutPending);
            Assert.EndsWith("ZERO FINAL TAIL", reader.Document.Source);
            Assert.Empty(reader.RenderingDiagnostics);
            reader.SelectAll();
            Assert.Equal(reader.Document.Text, reader.SelectedText);
        }
        finally { window.Close(); }
    }

    private static Window Show(Reader reader)
    {
        var window = new Window { Width = 600, Height = 280, Content = reader };
        window.Show();
        return window;
    }

    private static ScrollViewer Scroll(Reader reader) => reader.GetVisualDescendants()
        .OfType<ScrollViewer>().Single(scroll => scroll.Name == "PART_ScrollViewer");

    private static string Paragraphs(int count, int start = 0) => string.Join("\n\n",
        Enumerable.Range(start, count).Select(index => $"paragraph {index:D3} 中文 😀 and visible reading text"));

    private static TailGeometry CaptureTail(Reader reader, ScrollViewer scroll)
    {
        var lastId = reader.Document.Blocks[^1].Id;
        var last = reader.DocumentPresenter!.Children.OfType<MarkdownBlockPresenter>()
            .SingleOrDefault(block => block.NodeId == lastId);
        Control? lastRoot = last;
        if (lastRoot is null)
        {
            var text = reader.DocumentPresenter.GetVisualDescendants().OfType<MarkdownTextPresenter>()
                .LastOrDefault(text => text.TextStart + text.TextLength == reader.Document.Text.Length);
            if (text is not null)
                lastRoot = text.GetVisualAncestors().OfType<Control>().Prepend(text)
                    .FirstOrDefault(control => ReferenceEquals(control.GetVisualParent(), reader.DocumentPresenter));
        }
        var origin = lastRoot?.TranslatePoint(default, scroll);
        return new(scroll.Extent.Height - scroll.Viewport.Height - scroll.Offset.Y,
            origin is { } point && point.Y + lastRoot!.Bounds.Height > scroll.Padding.Top + .01 &&
                point.Y < scroll.Padding.Top + scroll.Viewport.Height - .01,
            reader.DocumentPresenter.IsMeasureValid && reader.DocumentPresenter.IsArrangeValid,
            reader.DocumentPresenter.RealizedBlockCount,
            reader.DocumentPresenter.Children.Count(child => child.Bounds.Bottom > scroll.Offset.Y &&
                child.Bounds.Top < scroll.Offset.Y + scroll.Viewport.Height),
            $"presenterMeasure={reader.DocumentPresenter.IsMeasureValid}, presenterArrange={reader.DocumentPresenter.IsArrangeValid}, " +
                $"readerMeasure={reader.IsMeasureValid}, readerArrange={reader.IsArrangeValid}, scrollMeasure={scroll.IsMeasureValid}, scrollArrange={scroll.IsArrangeValid}");
    }

    private static void AssertTailVisible(Reader reader, ScrollViewer scroll) => AssertTailGeometry(CaptureTail(reader, scroll));

    private static void AssertTailGeometry(TailGeometry tail)
    {
        Assert.InRange(Math.Abs(tail.BottomDistance), 0, .5);
        Assert.True(tail.LastBlockVisible, $"Tail was not visible; bottom distance={tail.BottomDistance}, realized={tail.Realized}.");
        Assert.True(tail.LayoutValid, tail.ValidityDetails);
        Assert.InRange(tail.Realized, tail.Visible, tail.Visible + 16);
    }

    private static async Task Settle(Window window)
    {
        // Preparation alone may drain an earlier glyph/coarse restoration. The
        // tested commit is observed inside Updated and never uses this drain.
        await Task.Delay(20, TestContext.Current.CancellationToken);
        for (var pass = 0; pass < 8; pass++)
        {
            ScopedHeadlessInput.RunJobs();
            window.UpdateLayout();
            if (window.Content is Reader reader && reader.DocumentPresenter is { IsMeasureValid: true, IsArrangeValid: true }) return;
            await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout, DispatcherPriority.Loaded);
        }
    }

    private readonly record struct TailGeometry(double BottomDistance, bool LastBlockVisible,
        bool LayoutValid, int Realized, int Visible, string ValidityDetails);
}
