using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using PdfViewer.Tests;
using NT.AvaloniaControl.PdfViewer;
using Reader = NT.AvaloniaControl.PdfViewer.PdfViewer;

namespace PdfViewer.Headless.Tests;

/// <summary>
/// P3-04 long-document contracts (PERF09/PERF11): selection spans beyond 24 / 100 / 1000
/// pages stay text-correct in both directions, a cleared long selection cancels its pending
/// extract and never lands late, and far navigation plus window resizes keep the layout
/// lookups consistent.
/// </summary>
public sealed class P3LongDocumentTests
{
    [Theory]
    [InlineData(25, false)]
    [InlineData(25, true)]
    [InlineData(120, false)]
    [InlineData(120, true)]
    [InlineData(1005, false)]
    [InlineData(1005, true)]
    public Task Selection_spanning_many_pages_keeps_interval_and_text_correct(int span, bool reverse) =>
        HeadlessTestScope.Run(() => VerifyLongSelectionAsync(span, reverse));

    [Fact]
    public Task Clearing_a_long_selection_cancels_the_pending_text_and_never_lands_late() => HeadlessTestScope.Run(async () =>
    {
        const int total = 400;
        var bytes = PdfFixture.ManyPages(total);
        using var native = await PdfDocument.LoadAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        var startText = await native.GetTextAsync(2, cancellationToken: TestContext.Current.CancellationToken);
        var endText = await native.GetTextAsync(350, cancellationToken: TestContext.Current.CancellationToken);
        var anchor = startText.Characters.First(character => !string.IsNullOrWhiteSpace(character.Text));
        var focus = endText.Characters.Last(character => !string.IsNullOrWhiteSpace(character.Text));
        using var fixture = await LongFixture.Create(bytes);
        fixture.Reader.ZoomLevel = 1;
        await fixture.Idle();
        var presenter = fixture.Presenter;
        var scroll = presenter.GetVisualAncestors().OfType<ScrollViewer>().First();
        scroll.Offset = new Vector(0, Math.Max(0,
            DocumentPoint(presenter, fixture.Reader, 2, anchor).Y - scroll.Viewport.Height / 2));
        await fixture.Idle();
        ScopedInput(() => fixture.Window.MouseDown(WindowPoint(presenter, fixture.Reader, fixture.Window, 2, anchor), MouseButton.Left));
        ScopedInput(() => fixture.Window.MouseDown(WindowPoint(presenter, fixture.Reader, fixture.Window, 2, anchor), MouseButton.Left));
        await Until(() => fixture.Reader.SelectedText == anchor.Text);
        scroll.Offset = new Vector(0, Math.Max(0,
            DocumentPoint(presenter, fixture.Reader, 350, focus).Y - scroll.Viewport.Height / 2));
        await fixture.Idle();
        ScopedInput(() => fixture.Window.MouseMove(WindowPoint(presenter, fixture.Reader, fixture.Window, 350, focus)));
        // The interval is live while the coalesced extract of hundreds of pages is in flight.
        await Until(() => fixture.Reader.IsTextSelected);
        fixture.Reader.ClearSelection();
        Assert.Equal("", fixture.Reader.SelectedText);
        await fixture.Idle();
        // No late landing: the cancelled preview must not overwrite the cleared state.
        Assert.Equal("", fixture.Reader.SelectedText);
        Assert.False(fixture.Reader.IsTextSelected);
        // The machinery still selects correctly afterwards.
        scroll.Offset = new Vector(0, Math.Max(0,
            DocumentPoint(presenter, fixture.Reader, 2, anchor).Y - scroll.Viewport.Height / 2));
        await fixture.Idle();
        var pageTwoEnd = startText.Characters.Last();
        var pageTwo = string.Concat(startText.Characters
            .Where(character => character.Index >= anchor.Index)
            .Select(character => character.Text));
        ScopedInput(() => fixture.Window.MouseDown(WindowPoint(presenter, fixture.Reader, fixture.Window, 2, anchor), MouseButton.Left));
        await Until(() => fixture.Reader.SelectedText == anchor.Text);
        ScopedInput(() => fixture.Window.MouseMove(WindowPoint(presenter, fixture.Reader, fixture.Window, 2, pageTwoEnd)));
        await Until(() => fixture.Reader.SelectedText == pageTwo);
        ScopedInput(() => fixture.Window.MouseUp(WindowPoint(presenter, fixture.Reader, fixture.Window, 2, pageTwoEnd), MouseButton.Left));
        Assert.Equal(pageTwo, fixture.Reader.SelectedText);
        Assert.Null(fixture.Reader.ErrorMessage);
    });

    [Fact]
    public Task Long_document_navigation_and_resize_keep_layout_lookups_consistent() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await LongFixture.Create(PdfFixture.ManyPages(300));
        fixture.Reader.ZoomMode = PdfZoomMode.Manual;
        fixture.Reader.ZoomLevel = 1;
        fixture.Reader.GoToPage(150);
        await fixture.Idle();
        Assert.Equal(150, fixture.Reader.CurrentPage);
        var presenter = fixture.Presenter;
        for (var index = 0; index < 300; index += 37)
        {
            var bounds = presenter.GetPageBounds(index);
            Assert.Equal(index, presenter.PageAt(bounds.Center.Y));
        }
        // A width-only resize recenters rows horizontally without touching vertical geometry.
        var before = presenter.GetPageBounds(149);
        fixture.Window.Width = 640;
        fixture.Window.UpdateLayout();
        HeadlessTestScope.Drain();
        await fixture.Idle();
        Assert.Equal(150, fixture.Reader.CurrentPage);
        var after = presenter.GetPageBounds(149);
        Assert.Equal(before.Y, after.Y, 3);
        Assert.Equal(before.Width, after.Width, 3);
        Assert.NotEqual(before.X, after.X);
        Assert.Equal(149, presenter.PageAt(after.Center.Y));
        Assert.Null(fixture.Reader.ErrorMessage);
    });

    private static async Task VerifyLongSelectionAsync(int span, bool reverse)
    {
        // The selected span sits mid-document with headroom on both sides, so each end needs
        // its own viewport jump instead of the whole range fitting on one screen.
        var total = span + 8;
        var lowPage = 2;
        var highPage = 2 + span - 1;
        var startPage = reverse ? highPage : lowPage;
        var endPage = reverse ? lowPage : highPage;
        var bytes = PdfFixture.ManyPages(total);
        using var native = await PdfDocument.LoadAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        var startText = await native.GetTextAsync(startPage, cancellationToken: TestContext.Current.CancellationToken);
        var endText = await native.GetTextAsync(endPage, cancellationToken: TestContext.Current.CancellationToken);
        var anchorCharacter = reverse ? startText.Characters.Last(character => !string.IsNullOrWhiteSpace(character.Text))
            : startText.Characters.First(character => !string.IsNullOrWhiteSpace(character.Text));
        var focusCharacter = reverse ? endText.Characters.First(character => !string.IsNullOrWhiteSpace(character.Text))
            : endText.Characters.Last(character => !string.IsNullOrWhiteSpace(character.Text));
        var expectedPages = new List<string>(span);
        for (var pageIndex = lowPage; pageIndex <= highPage; pageIndex++)
        {
            var pageText = await native.GetTextAsync(pageIndex, cancellationToken: TestContext.Current.CancellationToken);
            expectedPages.Add(string.Concat(pageText.Characters
                .Where(character => (pageIndex > lowPage || character.Index >= Math.Min(anchorCharacter.Index, focusCharacter.Index)) &&
                    (pageIndex < highPage || character.Index <= Math.Max(anchorCharacter.Index, focusCharacter.Index)))
                .Select(character => character.Text)));
        }
        var expected = string.Join(Environment.NewLine, expectedPages);
        Assert.Contains($"Page {lowPage + 1}", expected);
        Assert.Contains($"Page {highPage + 1}", expected);

        using var fixture = await LongFixture.Create(bytes);
        fixture.Reader.ZoomLevel = 1;
        await fixture.Idle();
        var presenter = fixture.Presenter;
        var scroll = presenter.GetVisualAncestors().OfType<ScrollViewer>().First();
        scroll.Offset = new Vector(0, Math.Max(0,
            DocumentPoint(presenter, fixture.Reader, startPage, anchorCharacter).Y - scroll.Viewport.Height / 2));
        await fixture.Idle();
        ScopedInput(() => fixture.Window.MouseDown(
            WindowPoint(presenter, fixture.Reader, fixture.Window, startPage, anchorCharacter), MouseButton.Left));
        await Until(() => fixture.Reader.SelectedText == anchorCharacter.Text);
        scroll.Offset = new Vector(0, Math.Max(0,
            DocumentPoint(presenter, fixture.Reader, endPage, focusCharacter).Y - scroll.Viewport.Height / 2));
        await fixture.Idle();
        ScopedInput(() => fixture.Window.MouseMove(WindowPoint(presenter, fixture.Reader, fixture.Window, endPage, focusCharacter)));
        await Until(() => fixture.Reader.SelectedText == expected);
        ScopedInput(() => fixture.Window.MouseUp(WindowPoint(presenter, fixture.Reader, fixture.Window, endPage, focusCharacter), MouseButton.Left));
        Assert.Equal(expected, fixture.Reader.SelectedText);
        await fixture.Reader.CopySelectionToClipboardAsync();
        Assert.Equal(expected, await fixture.Window.Clipboard!.TryGetTextAsync());
        Assert.Null(fixture.Reader.ErrorMessage);
    }

    private static Point DocumentPoint(PdfDocumentPresenter presenter, Reader reader, int pageIndex, PdfTextCharacter character)
    {
        var page = presenter.GetPageBounds(pageIndex);
        var scale = reader.ZoomLevel * 96 / 72;
        return new Point(page.X + character.Bounds.Center.X * scale, page.Y + character.Bounds.Center.Y * scale);
    }

    private static Point WindowPoint(PdfDocumentPresenter presenter, Reader reader, Window window, int pageIndex, PdfTextCharacter character)
    {
        var document = DocumentPoint(presenter, reader, pageIndex, character);
        var point = presenter.TranslatePoint(document, window)!.Value;
        Assert.True(new Rect(window.ClientSize).Contains(point),
            $"Page {pageIndex + 1} character at window {point} (document {document}, client {window.ClientSize}, zoom {reader.ZoomLevel}) lies outside the window.");
        return point;
    }

    private static async Task Until(Func<bool> condition)
    {
        // Long extracts walk hundreds of pages through the serial scheduler; allow real time.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        while (!condition())
        {
            HeadlessTestScope.Drain();
            await Task.Delay(10, timeout.Token);
        }
    }

    private static void ScopedInput(Action input)
    {
        var caller = ExecutionContext.Capture();
        if (caller is null) input();
        else ExecutionContext.Run(caller, _ => input(), null);
    }

    private sealed class LongFixture : IDisposable
    {
        internal Reader Reader { get; } = new() { IsSidebarVisible = false };
        internal Window Window { get; }
        internal PdfDocumentPresenter Presenter => (PdfDocumentPresenter)Reader.DocumentPresenter!;

        internal LongFixture()
        {
            Window = new Window { Width = 1000, Height = 700, Content = Reader };
            Window.Show();
            Window.UpdateLayout();
            HeadlessTestScope.Drain();
        }

        internal static async Task<LongFixture> Create(byte[] bytes)
        {
            var fixture = new LongFixture();
            try
            {
                await fixture.Reader.LoadDocumentAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
                await fixture.Idle();
                return fixture;
            }
            catch { fixture.Dispose(); throw; }
        }

        internal async Task Idle()
        {
            Window.UpdateLayout();
            HeadlessTestScope.Drain();
            await Reader.WaitForIdleAsync(TestContext.Current.CancellationToken);
            Window.UpdateLayout();
            HeadlessTestScope.Drain();
        }

        public void Dispose()
        {
            Reader.Dispose();
            Window.Close();
        }
    }
}
