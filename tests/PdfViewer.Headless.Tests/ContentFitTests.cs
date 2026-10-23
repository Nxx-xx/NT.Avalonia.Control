using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using PdfViewer.Tests;
using NT.AvaloniaControl.PdfViewer;
using Reader = NT.AvaloniaControl.PdfViewer.PdfViewer;

namespace PdfViewer.Headless.Tests;

/// <summary>
/// Content-bound fit semantics: /FitB, /FitBH and /FitBV fit the page's content box (page box on
/// an empty page), /FitR honors viewer rotation for non-square rectangles, and bookmark flag
/// updates notify the computed Weight / Style properties the theme binds to.
/// </summary>
public sealed class ContentFitTests
{
    [Fact]
    public Task FitB_family_fits_the_content_box_not_the_page_box() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await ContentFitFixture.Create(PdfFixture.ContentBoxDocument());
        var reader = fixture.Reader;
        var scroll = fixture.Presenter.GetVisualAncestors().OfType<ScrollViewer>().First();
        // Content box of page 1: displayed Rect(20, 150, 130, 150) on the 300x400 page.
        var box = new Rect(20, 150, 130, 150);
        var center = box.Center;

        // /FitB fits and centers the content box at a manual zoom.
        var expected = FitZoom(reader, scroll, box.Width, box.Height);
        await NavigateAndSettle(fixture, new PdfLinkDestination(0, null, null, PdfDestinationFit.FitB), expected);
        Assert.Equal(PdfZoomMode.Manual, reader.ZoomMode);
        Assert.Equal(expected, reader.ZoomLevel, 3);
        var scale = reader.ZoomLevel * 96 / 72;
        var page = fixture.Presenter.GetPageBounds(0);
        Assert.Equal(OffsetX(scroll, page.X + center.X * scale - scroll.Viewport.Width / 2), scroll.Offset.X, 1);
        Assert.Equal(OffsetY(scroll, page.Y + center.Y * scale - scroll.Viewport.Height / 2), scroll.Offset.Y, 1);

        // /FitBV fits the content box height, pins Left at the window left and centers the other axis.
        expected = FitZoom(reader, scroll, null, box.Height);
        await NavigateAndSettle(fixture, new PdfLinkDestination(0, null, null, PdfDestinationFit.FitBV, Left: 25), expected);
        Assert.Equal(expected, reader.ZoomLevel, 3);
        scale = reader.ZoomLevel * 96 / 72;
        page = fixture.Presenter.GetPageBounds(0);
        Assert.Equal(OffsetX(scroll, page.X + 25 * scale), scroll.Offset.X, 1);
        Assert.Equal(OffsetY(scroll, page.Y + center.Y * scale - scroll.Viewport.Height / 2), scroll.Offset.Y, 1);

        // /FitBH fits the content box width, pins the Top parameter at the window top and centers X.
        expected = FitZoom(reader, scroll, box.Width, null);
        await NavigateAndSettle(fixture, new PdfLinkDestination(0, null, null, PdfDestinationFit.FitBH, Top: 100), expected);
        Assert.Equal(expected, reader.ZoomLevel, 3);
        scale = reader.ZoomLevel * 96 / 72;
        page = fixture.Presenter.GetPageBounds(0);
        Assert.Equal(OffsetX(scroll, page.X + center.X * scale - scroll.Viewport.Width / 2), scroll.Offset.X, 1);
        Assert.Equal(OffsetY(scroll, page.Y + 100 * scale), scroll.Offset.Y, 1);

        // /FitBH without a Top parameter pins the content box's own top edge.
        await NavigateAndSettle(fixture, new PdfLinkDestination(0, null, null, PdfDestinationFit.FitBH), expected);
        scale = reader.ZoomLevel * 96 / 72;
        page = fixture.Presenter.GetPageBounds(0);
        Assert.Equal(OffsetY(scroll, page.Y + box.Y * scale), scroll.Offset.Y, 1);

        // Page 2 has no content objects: the content box falls back to the page box. Its center
        // sits past the end of the document, so the offset clamps at the extent like any scroll.
        expected = FitZoom(reader, scroll, 500, 300);
        await NavigateAndSettle(fixture, new PdfLinkDestination(1, null, null, PdfDestinationFit.FitB), expected);
        Assert.Equal(PdfZoomMode.Manual, reader.ZoomMode);
        Assert.Equal(expected, reader.ZoomLevel, 3);
        scale = reader.ZoomLevel * 96 / 72;
        page = fixture.Presenter.GetPageBounds(1);
        Assert.Equal(OffsetX(scroll, page.X + 250 * scale - scroll.Viewport.Width / 2), scroll.Offset.X, 1);
        Assert.Equal(OffsetY(scroll, page.Y + 150 * scale - scroll.Viewport.Height / 2), scroll.Offset.Y, 1);
    });

    [Fact]
    public Task FitR_under_viewer_rotation_fits_the_swapped_rectangle() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await ContentFitFixture.Create(PdfFixture.ContentBoxDocument());
        var reader = fixture.Reader;
        reader.Rotation = 90;
        await fixture.Idle();
        var scroll = fixture.Presenter.GetVisualAncestors().OfType<ScrollViewer>().First();
        // Non-square destination rectangle in page-display points. Tall enough that its rotated
        // top-left pin can actually reach the window's left edge without clamping at the extent.
        var rectangle = new Rect(30, 50, 100, 120);

        // Under 90 / 270 viewer rotation the rectangle's on-screen width and height swap, so the
        // fit zoom uses the swapped dimensions and the pin corner is the rectangle's rotated top-left.
        var expected = FitZoom(reader, scroll, rectangle.Height, rectangle.Width);
        await NavigateAndSettle(fixture, new PdfLinkDestination(0, null, null, PdfDestinationFit.FitR, Rectangle: rectangle), expected);
        Assert.Equal(PdfZoomMode.Manual, reader.ZoomMode);
        Assert.Equal(expected, reader.ZoomLevel, 3);
        var scale = reader.ZoomLevel * 96 / 72;
        var page = fixture.Presenter.GetPageBounds(0);
        // Rotation 90 pins page-display (rectangle.X, rectangle.Bottom); displayed (x, y) maps to
        // content (page.Height - y, x) inside the rotated page box.
        Assert.Equal(OffsetX(scroll, page.X + (400 - rectangle.Bottom) * scale), scroll.Offset.X, 1);
        Assert.Equal(OffsetY(scroll, page.Y + rectangle.X * scale), scroll.Offset.Y, 1);

        reader.Rotation = 270;
        await fixture.Idle();
        await NavigateAndSettle(fixture, new PdfLinkDestination(0, null, null, PdfDestinationFit.FitR, Rectangle: rectangle), expected);
        Assert.Equal(expected, reader.ZoomLevel, 3);
        scale = reader.ZoomLevel * 96 / 72;
        page = fixture.Presenter.GetPageBounds(0);
        // Rotation 270 pins page-display (rectangle.Right, rectangle.Y); displayed (x, y) maps to
        // content (y, page.Width - x).
        Assert.Equal(OffsetX(scroll, page.X + rectangle.Y * scale), scroll.Offset.X, 1);
        Assert.Equal(OffsetY(scroll, page.Y + (300 - rectangle.Right) * scale), scroll.Offset.Y, 1);
    });

    [Fact]
    public Task Bookmark_flag_updates_notify_weight_and_style() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await ContentFitFixture.Create(PdfFixture.ManyPages(3));
        var bookmark = fixture.Reader.AddUserBookmark("标题", 2);
        var notified = new List<string?>();
        bookmark.PropertyChanged += (_, e) => notified.Add(e.PropertyName);
        Assert.True(fixture.Reader.SetUserBookmarkFlags(bookmark.Id, PdfUserBookmarkFlags.Bold | PdfUserBookmarkFlags.Italic));
        Assert.Contains(nameof(PdfUserBookmark.Flags), notified);
        Assert.Contains(nameof(PdfUserBookmark.IsBold), notified);
        Assert.Contains(nameof(PdfUserBookmark.IsItalic), notified);
        // The theme binds FontWeight / FontStyle through the computed Weight / Style properties.
        Assert.Contains(nameof(PdfUserBookmark.Weight), notified);
        Assert.Contains(nameof(PdfUserBookmark.Style), notified);
        Assert.Equal(FontWeight.Bold, bookmark.Weight);
        Assert.Equal(FontStyle.Italic, bookmark.Style);
        notified.Clear();
        Assert.True(fixture.Reader.SetUserBookmarkFlags(bookmark.Id, PdfUserBookmarkFlags.None));
        Assert.Contains(nameof(PdfUserBookmark.Weight), notified);
        Assert.Contains(nameof(PdfUserBookmark.Style), notified);
        Assert.Equal(FontWeight.Normal, bookmark.Weight);
        Assert.Equal(FontStyle.Normal, bookmark.Style);
    });

    // Navigates from a neutral view (manual zoom 100% on the last page) so the settle wait cannot
    // match the previous navigation's zoom, then waits for the destination zoom to land.
    private static async Task NavigateAndSettle(ContentFitFixture fixture, PdfLinkDestination destination, double expectedZoom)
    {
        var reader = fixture.Reader;
        reader.ZoomMode = PdfZoomMode.Manual;
        reader.ZoomLevel = 1;
        reader.GoToPage(reader.PageCount);
        await fixture.Idle();
        reader.NavigateToDestination(destination);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (Math.Abs(reader.ZoomLevel - expectedZoom) > .001 || reader.ZoomMode != PdfZoomMode.Manual)
        {
            HeadlessTestScope.Drain();
            await Task.Delay(10, timeout.Token);
        }
        await fixture.Idle();
    }

    // ScrollViewer coerces offsets into the extent: a pin near the end of the document cannot
    // scroll past the last content, so expectations clamp into the scrollable range the same way.
    private static double OffsetX(ScrollViewer scroll, double value) =>
        Math.Clamp(value, 0, Math.Max(0, scroll.Extent.Width - scroll.Viewport.Width));

    private static double OffsetY(ScrollViewer scroll, double value) =>
        Math.Clamp(value, 0, Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height));

    // The /Fit-family zooms: fit the given on-screen rectangle dimensions into the viewport with
    // the layout's fit margins, clamped like every other zoom. Null fits only the other axis.
    private static double FitZoom(Reader reader, ScrollViewer scroll, double? widthPoints, double? heightPoints)
    {
        var size = scroll.Viewport;
        if (size.Width <= 60 || size.Height <= 60) size = scroll.Bounds.Size;
        var zoom = double.PositiveInfinity;
        if (widthPoints is { } width) zoom = Math.Min(zoom, (size.Width - (PdfPageLayout.Gap * 2 + 4)) / (width * 96 / 72));
        if (heightPoints is { } height) zoom = Math.Min(zoom, (size.Height - PdfPageLayout.Gap * 2) / (height * 96 / 72));
        return Math.Clamp(zoom, reader.MinimumZoom, reader.MaximumZoom);
    }

    private sealed class ContentFitFixture : IDisposable
    {
        internal Reader Reader { get; } = new();
        internal Window Window { get; }
        internal PdfDocumentPresenter Presenter => (PdfDocumentPresenter)Reader.DocumentPresenter!;

        private ContentFitFixture()
        {
            Window = new Window { Width = 1000, Height = 700, Content = Reader };
            Window.Show();
            Window.UpdateLayout();
            HeadlessTestScope.Drain();
        }

        internal static async Task<ContentFitFixture> Create(byte[] bytes)
        {
            var fixture = new ContentFitFixture();
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
