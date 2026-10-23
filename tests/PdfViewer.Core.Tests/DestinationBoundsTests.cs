using Avalonia;
using PdfViewer.Tests;
using NT.AvaloniaControl.PdfViewer;

namespace PdfViewer.Core.Tests;

/// <summary>
/// Destination and content-bound edges: /XYZ destinations that define only one coordinate keep
/// that axis (filed under Left / Top in displayed space), and page content bounds are the union
/// of the page's drawable objects with null for content-free pages.
/// </summary>
public sealed class DestinationBoundsTests
{
    [Fact]
    public async Task Partial_xyz_destinations_keep_their_single_axis()
    {
        await using var document = await PdfDocument.LoadAsync(PdfFixture.PartialXyzDocument(),
            cancellationToken: TestContext.Current.CancellationToken);
        var links = await document.GetLinksAsync(0, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(4, links.Count);
        Assert.All(links, link => Assert.Equal(PdfLinkKind.Internal, link.Kind));

        var xOnly = links[0].Destination!;
        Assert.Equal(PdfDestinationFit.Xyz, xOnly.Fit);
        Assert.Equal(0, xOnly.PageIndex);
        Assert.Null(xOnly.Position); Assert.Null(xOnly.Top); Assert.Null(xOnly.Zoom);
        Assert.Equal(50, xOnly.Left!.Value, 3);

        var yOnly = links[1].Destination!;
        Assert.Equal(PdfDestinationFit.Xyz, yOnly.Fit);
        Assert.Null(yOnly.Position); Assert.Null(yOnly.Left); Assert.Null(yOnly.Zoom);
        // User-space y=300 on a 400pt page maps to displayed Y = 400 - 300.
        Assert.Equal(100, yOnly.Top!.Value, 3);

        var zoomOnly = links[2].Destination!;
        Assert.Equal(PdfDestinationFit.Xyz, zoomOnly.Fit);
        Assert.Null(zoomOnly.Position); Assert.Null(zoomOnly.Left); Assert.Null(zoomOnly.Top);
        Assert.Equal(2, zoomOnly.Zoom!.Value, 3);

        // On the /Rotate 90 page the user-space x axis runs along the displayed Y axis, so the
        // preserved coordinate is a displayed Y and is filed under Top, not Left.
        var rotated = links[3].Destination!;
        Assert.Equal(PdfDestinationFit.Xyz, rotated.Fit);
        Assert.Equal(1, rotated.PageIndex);
        Assert.Null(rotated.Position); Assert.Null(rotated.Left); Assert.Null(rotated.Zoom);
        Assert.Equal(80, rotated.Top!.Value, 3);
    }

    [Fact]
    public async Task Page_content_bounds_cover_objects_and_null_for_empty_pages()
    {
        await using var document = await PdfDocument.LoadAsync(PdfFixture.ContentBoxDocument(),
            cancellationToken: TestContext.Current.CancellationToken);
        var box = await document.GetPageContentBoundsAsync(0, cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(box);
        // Two rectangles at (20,100,60x40) and (100,200,50x50): union (20,100)-(150,250) in
        // page space maps to displayed Rect(20, 150, 130, 150) on the 300x400 page.
        Assert.Equal(new Rect(20, 150, 130, 150), box!.Value);
        // The content box is cached per page.
        Assert.Equal(box, await document.GetPageContentBoundsAsync(0, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Null(await document.GetPageContentBoundsAsync(1, cancellationToken: TestContext.Current.CancellationToken));
    }
}
