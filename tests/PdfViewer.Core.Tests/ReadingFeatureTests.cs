using Avalonia;
using PdfViewer.Tests;
using NT.AvaloniaControl.PdfViewer;

namespace PdfViewer.Core.Tests;

public sealed class ReadingFeatureTests
{
    [Fact]
    public async Task Page_labels_follow_the_document_number_tree_with_prefix_and_style()
    {
        await using var document = await PdfDocument.LoadAsync(PdfFixture.LinksAndLabelsDocument(),
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "i", "ii", "A-5", "A-6" }, document.PageLabels);
    }

    [Fact]
    public async Task Page_labels_are_empty_when_the_document_has_no_number_tree()
    {
        await using var document = await PdfDocument.LoadAsync(PdfFixture.ReadingDocument(),
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, document.PageLabels.Count);
        Assert.All(document.PageLabels, label => Assert.Equal("", label));
    }

    [Fact]
    public async Task Link_extraction_reports_targets_bounds_and_xyz_details()
    {
        await using var document = await PdfDocument.LoadAsync(PdfFixture.LinksAndLabelsDocument(),
            cancellationToken: TestContext.Current.CancellationToken);
        var links = await document.GetLinksAsync(0, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(5, links.Count);

        var xyz = links.Single(link => link.Kind == PdfLinkKind.Internal && link.Destination?.Fit == PdfDestinationFit.Xyz);
        Assert.Equal(0, xyz.PageIndex);
        Assert.Equal(0, xyz.Destination!.PageIndex);
        Assert.Equal(PdfDestinationFit.Xyz, xyz.Destination.Fit);
        Assert.Equal(2, xyz.Destination.Zoom);
        Assert.NotNull(xyz.Destination.Position);
        Assert.Equal(50, xyz.Destination.Position!.Value.X, 3);
        // /XYZ top 300 on a 400pt page is 100 points from the displayed top-left origin.
        Assert.Equal(100, xyz.Destination.Position.Value.Y, 3);
        // /Rect [10 350 110 380] becomes a top-left origin box: x=10, y=20, 100x30 points.
        Assert.Equal(10, xyz.Bounds.X, 3);
        Assert.Equal(20, xyz.Bounds.Y, 3);
        Assert.Equal(100, xyz.Bounds.Width, 3);
        Assert.Equal(30, xyz.Bounds.Height, 3);

        var uri = links.Single(link => link.Kind == PdfLinkKind.Uri);
        Assert.Equal("https://example.com/doc", uri.Target);
        Assert.Null(uri.Destination);

        var file = links.Single(link => link.Kind == PdfLinkKind.File);
        Assert.Equal("report.pdf", file.Target);

        var unsupported = links.Single(link => link.Kind == PdfLinkKind.Unsupported && link.Target is null);
        Assert.Equal(0, unsupported.PageIndex);

        // The named destination resolves through the catalog name tree to page 2 (/Fit).
        var named = links.Single(link => link.Kind == PdfLinkKind.Internal && link.Destination?.Fit == PdfDestinationFit.Fit);
        Assert.Equal(1, named.Destination!.PageIndex);
        Assert.Null(named.Destination.Position);
        Assert.Null(named.Destination.Zoom);
    }

    [Fact]
    public async Task Link_extraction_on_a_page_without_links_returns_empty()
    {
        await using var document = await PdfDocument.LoadAsync(PdfFixture.LinksAndLabelsDocument(),
            cancellationToken: TestContext.Current.CancellationToken);
        var links = await document.GetLinksAsync(1, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Empty(links);
    }

    [Fact]
    public async Task Outline_entries_carry_full_destinations()
    {
        await using var document = await PdfDocument.LoadAsync(PdfFixture.ReadingDocument(),
            cancellationToken: TestContext.Current.CancellationToken);
        var chapter = Assert.Single(document.Bookmarks);
        Assert.Equal(0, chapter.PageIndex);
        Assert.NotNull(chapter.Destination);
        Assert.Equal(0, chapter.Destination!.PageIndex);
        Assert.Equal(PdfDestinationFit.Fit, chapter.Destination.Fit);
        Assert.Null(chapter.Destination.Zoom);
        var child = Assert.Single(chapter.Children);
        Assert.Equal(1, child.PageIndex);
        Assert.Equal(1, child.Destination!.PageIndex);
        Assert.Equal(PdfDestinationFit.Fit, child.Destination.Fit);
    }

    [Fact]
    public async Task Unsupported_and_external_link_kinds_never_carry_a_destination()
    {
        await using var document = await PdfDocument.LoadAsync(PdfFixture.LinksAndLabelsDocument(),
            cancellationToken: TestContext.Current.CancellationToken);
        var links = await document.GetLinksAsync(0, cancellationToken: TestContext.Current.CancellationToken);
        foreach (var link in links.Where(link => link.Kind is PdfLinkKind.Uri or PdfLinkKind.File or PdfLinkKind.Unsupported))
            Assert.Null(link.Destination);
    }

    [Fact]
    public async Task Fit_family_destinations_expose_their_view_parameters()
    {
        await using var document = await PdfDocument.LoadAsync(PdfFixture.FitDestinationsDocument(),
            cancellationToken: TestContext.Current.CancellationToken);
        var links = await document.GetLinksAsync(0, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(6, links.Count);
        Assert.All(links, link => Assert.Equal(PdfLinkKind.Internal, link.Kind));

        // /FitR 30 350 130 250 on a 400pt page: display x=30, y=50, 100x100 points.
        var fitR = links.Single(link => link.Destination?.Fit == PdfDestinationFit.FitR).Destination!;
        Assert.Equal(1, fitR.PageIndex);
        Assert.Equal(new Rect(30, 50, 100, 100), fitR.Rectangle!.Value);
        Assert.Null(fitR.Left); Assert.Null(fitR.Top);
        Assert.Null(fitR.Position); Assert.Null(fitR.Zoom);

        var fitH = links.Single(link => link.Destination?.Fit == PdfDestinationFit.FitH).Destination!;
        Assert.Equal(0, fitH.PageIndex);
        Assert.Equal(100, fitH.Top!.Value, 3);
        Assert.Null(fitH.Left); Assert.Null(fitH.Rectangle);

        var fitV = links.Single(link => link.Destination?.Fit == PdfDestinationFit.FitV).Destination!;
        Assert.Equal(1, fitV.PageIndex);
        Assert.Equal(50, fitV.Left!.Value, 3);
        Assert.Null(fitV.Top); Assert.Null(fitV.Rectangle);

        var fitB = links.Single(link => link.Destination?.Fit == PdfDestinationFit.FitB).Destination!;
        Assert.Equal(2, fitB.PageIndex);
        Assert.Null(fitB.Rectangle); Assert.Null(fitB.Left); Assert.Null(fitB.Top);

        var fitBH = links.Single(link => link.Destination?.Fit == PdfDestinationFit.FitBH).Destination!;
        Assert.Equal(2, fitBH.PageIndex);
        // /FitBH top 200 on a 300pt page: display Y = 300 - 200 = 100.
        Assert.Equal(100, fitBH.Top!.Value, 3);
        Assert.Null(fitBH.Left);

        var fitBV = links.Single(link => link.Destination?.Fit == PdfDestinationFit.FitBV).Destination!;
        Assert.Equal(2, fitBV.PageIndex);
        Assert.Equal(25, fitBV.Left!.Value, 3);
        Assert.Null(fitBV.Top);
    }
}
