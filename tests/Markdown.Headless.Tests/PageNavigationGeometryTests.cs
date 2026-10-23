using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Markdown;
using Reader = NT.AvaloniaControl.Markdown.Markdown;

namespace Markdown.Headless.Tests;

public sealed class PageNavigationGeometryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task MT05_PageKeysMoveTheCaretAndScrollByOneActualViewport(bool up) => HeadlessTestScope.Run(() => MT05_PageKeysMoveTheCaretAndScrollByOneActualViewportCore(up));

    private async Task MT05_PageKeysMoveTheCaretAndScrollByOneActualViewportCore(bool up)
    {
        var reader = new Reader { Text = Paragraphs() };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync(); await Flush(window);
            await PlaceCaretInViewportMiddle(reader, window);
            var before = Caret(reader); var scroll = Scroll(reader); var offset = scroll.Offset.Y;
            var direction = up ? -1 : 1;
            Press(window, up ? Key.PageUp : Key.PageDown); await Flush(window);
            AssertPage(reader, before, offset, direction);
            Assert.True(reader.Selection.IsEmpty);
            Assert.InRange(reader.DocumentPresenter!.RealizedBlockCount, 1, 50);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task MT05_ShiftPagingPreservesItsAnchorAndViewportCaretAcrossSeveralRecycledPages() => HeadlessTestScope.Run(() => MT05_ShiftPagingPreservesItsAnchorAndViewportCaretAcrossSeveralRecycledPagesCore());

    private async Task MT05_ShiftPagingPreservesItsAnchorAndViewportCaretAcrossSeveralRecycledPagesCore()
    {
        var reader = new Reader { Text = Paragraphs() };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync(); await Flush(window);
            await PlaceCaretInViewportMiddle(reader, window);
            var anchor = reader.Selection.Active;
            foreach (var direction in new[] { 1, 1, 1, -1, -1 })
            {
                var before = Caret(reader); var offset = Scroll(reader).Offset.Y;
                Press(window, direction > 0 ? Key.PageDown : Key.PageUp, RawInputModifiers.Shift); await Flush(window);
                Assert.Equal(anchor, reader.Selection.Anchor);
                AssertPage(reader, before, offset, direction);
                Assert.Equal(reader.Selection, reader.Selection.Normalize(reader.Document.Text));
            }
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task MT13_PagingWinsOverAPreviouslyQueuedFontReadingRestore() => HeadlessTestScope.Run(() => MT13_PagingWinsOverAPreviouslyQueuedFontReadingRestoreCore());

    private async Task MT13_PagingWinsOverAPreviouslyQueuedFontReadingRestoreCore()
    {
        var reader = new Reader { Text = Paragraphs() };
        var window = Show(reader);
        try
        {
            await reader.WhenUpdatedAsync(); await Flush(window);
            await PlaceCaretInViewportMiddle(reader, window);
            reader.FontSize = 20;
            window.UpdateLayout(); // Queue the stable reading restoration, without running Loaded jobs.
            PlaceCaretInViewportMiddle(reader);
            var before = Caret(reader); var offset = Scroll(reader).Offset.Y;
            // Headless KeyPress drains earlier dispatcher jobs before input delivery.
            // Route this input synchronously to exercise navigation before Loaded restore.
            reader.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.PageDown });
            window.UpdateLayout(); await Flush(window);
            AssertPage(reader, before, offset, 1);
            var navigated = Scroll(reader).Offset;
            await Flush(window);
            Assert.Equal(navigated, Scroll(reader).Offset);
        }
        finally { window.Close(); }
    }

    [Fact]
    public Task MT23_DocumentReplacementDuringDestinationRealizationRejectsTheOldPageCaret() => HeadlessTestScope.Run(() => MT23_DocumentReplacementDuringDestinationRealizationRejectsTheOldPageCaretCore());

    private async Task MT23_DocumentReplacementDuringDestinationRealizationRejectsTheOldPageCaretCore()
    {
        var reader = new Reader { Text = Paragraphs() };
        var window = Show(reader);
        var replaced = false; var oldPageCommitted = false;
        try
        {
            await reader.WhenUpdatedAsync(); await Flush(window);
            await PlaceCaretInViewportMiddle(reader, window);
            var original = reader.Selection;
            reader.ElementPrepared += (_, _) =>
            {
                if (replaced) return;
                replaced = true; reader.Text = "host replacement";
            };
            reader.SelectionChanged += (_, _) =>
            { if (replaced && reader.Selection != original && reader.Selection.Active > "host replacement".Length) oldPageCommitted = true; };
            Press(window, Key.PageDown);
            Assert.True(replaced);
            await reader.WhenUpdatedAsync(); await Flush(window);
            Assert.False(oldPageCommitted);
            Assert.Equal("host replacement", reader.Document.Text);
            Assert.Equal(default, reader.Selection);
            Assert.Equal(0, Scroll(reader).Offset.Y);
        }
        finally { window.Close(); }
    }

    private static void AssertPage(Reader reader, (double Y, double Height) before, double offset, int direction)
    {
        var after = Caret(reader); var scroll = Scroll(reader); var page = scroll.Viewport.Height;
        var tolerance = Math.Max(before.Height, after.Height) + .1;
        Assert.InRange((after.Y - before.Y) * direction, page - tolerance, page + tolerance);
        Assert.True(Math.Abs((scroll.Offset.Y - offset) * direction - page) <= .1,
            $"Page={page}, caret={before.Y}->{after.Y}, scroll={offset}->{scroll.Offset.Y}, line={before.Height}->{after.Height}.");
        Assert.InRange(Math.Abs((after.Y - scroll.Offset.Y) - (before.Y - offset)), 0, tolerance);
    }
    private static async Task PlaceCaretInViewportMiddle(Reader reader, Window window)
    {
        Scroll(reader).Offset = new Vector(0, 4000);
        await Flush(window);
        PlaceCaretInViewportMiddle(reader);
        Assert.True(reader.Focus());
    }
    private static void PlaceCaretInViewportMiddle(Reader reader)
    {
        var scroll = Scroll(reader);
        var text = reader.GetVisualDescendants().OfType<MarkdownTextPresenter>()
            .OrderBy(text => Math.Abs(Caret(reader, text.TextStart + 4).Y - scroll.Offset.Y - scroll.Viewport.Height / 2)).First();
        reader.SetSelection(text.TextStart + 4, text.TextStart + 4);
        var point = Caret(reader);
        Assert.InRange(point.Y - scroll.Offset.Y, scroll.Viewport.Height * .35, scroll.Viewport.Height * .65);
    }
    private static (double Y, double Height) Caret(Reader reader) => Caret(reader, reader.Selection.Active);
    private static (double Y, double Height) Caret(Reader reader, int position)
    {
        var text = reader.GetVisualDescendants().OfType<MarkdownTextPresenter>().First(text => position >= text.TextStart && position <= text.TextStart + text.TextLength);
        var rect = text.TextLayout.HitTestTextPosition(position - text.TextStart);
        var point = text.TranslatePoint(new Point(rect.X + text.Padding.Left, rect.Y + rect.Height / 2 + text.Padding.Top), reader.DocumentPresenter!)!.Value;
        return (point.Y, rect.Height);
    }
    private static ScrollViewer Scroll(Reader reader) => reader.GetVisualDescendants().OfType<ScrollViewer>().Single(scroll => scroll.Name == "PART_ScrollViewer");
    private static string Paragraphs() => string.Join("\n\n", Enumerable.Range(0, 1000).Select(i => $"ordinary paragraph {i:D4} 中😀 e\u0301 text"));
    private static Window Show(Reader reader) { var window = new Window { Width = 600, Height = 280, Content = reader }; window.Show(); return window; }
    private static void Press(Window window, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    { window.KeyPress(key, modifiers, PhysicalKey.None, null); window.KeyRelease(key, modifiers, PhysicalKey.None, null); window.UpdateLayout(); }
    private static async Task Flush(Window window)
    { await Task.Delay(20, TestContext.Current.CancellationToken); await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout, DispatcherPriority.Loaded); }
}

