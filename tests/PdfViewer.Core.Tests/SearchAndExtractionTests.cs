using PdfViewer.Tests;
using NT.AvaloniaControl.PdfViewer;

namespace PdfViewer.Core.Tests;

public sealed class SearchAndExtractionTests
{
    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Search_pages_yields_every_scanned_page_in_order_and_flattens_to_the_flat_search()
    {
        await using var document = await PdfDocument.LoadAsync(PdfFixture.ReadingDocument(), cancellationToken: TestToken);
        var pages = new List<PdfSearchPageResult>();
        await foreach (var page in document.SearchPagesAsync("PDF reader", cancellationToken: TestToken))
            pages.Add(page);

        // Both pages are yielded so callers can report scan progress; only page 1 matches.
        Assert.Equal(2, pages.Count);
        Assert.Equal(0, pages[0].PageIndex);
        Assert.Equal(1, pages[1].PageIndex);
        Assert.Equal("PDF reader", Assert.Single(pages[0].Matches).Text);
        Assert.Empty(pages[1].Matches);

        var flat = await document.SearchAsync("PDF reader", cancellationToken: TestToken);
        Assert.Equal(
            flat.Select(result => (result.PageIndex, result.StartIndex, result.Length)),
            pages.SelectMany(page => page.Matches).Select(result => (result.PageIndex, result.StartIndex, result.Length)));
    }

    [Fact]
    public async Task Search_pages_stops_at_the_match_limit_on_the_page_that_reached_it()
    {
        await using var document = await PdfDocument.LoadAsync(PdfFixture.ReadingDocument(), cancellationToken: TestToken);
        var pages = new List<PdfSearchPageResult>();
        await foreach (var page in document.SearchPagesAsync("hello", maxResults: 2, cancellationToken: TestToken))
            pages.Add(page);

        // Page 1 alone holds four matches; the limit cuts it after two and ends the walk.
        var only = Assert.Single(pages);
        Assert.Equal(0, only.PageIndex);
        Assert.Equal(2, only.Matches.Count);
        Assert.Equal(2, (await document.SearchAsync("hello", maxResults: 2, cancellationToken: TestToken)).Count);
    }

    [Fact]
    public async Task Full_document_text_joins_pages_with_a_form_feed()
    {
        await using var document = await PdfDocument.LoadAsync(PdfFixture.ReadingDocument(), cancellationToken: TestToken);
        var first = await document.GetTextAsync(0, cancellationToken: TestToken);
        var second = await document.GetTextAsync(1, cancellationToken: TestToken);
        var full = await document.GetFullTextAsync(TestToken);
        Assert.Equal(first.Text + "\f" + second.Text, full);
        Assert.Contains("Hello PDF reader", full);
        Assert.Contains("Second page hello", full);
    }

    [Fact]
    public async Task Full_document_text_of_a_single_page_has_no_separator()
    {
        await using var document = await PdfDocument.LoadAsync(PdfFixture.SinglePage("Only page"), cancellationToken: TestToken);
        var page = await document.GetTextAsync(0, cancellationToken: TestToken);
        Assert.Equal(page.Text, await document.GetFullTextAsync(TestToken));
        Assert.Contains("Only page", page.Text);
    }
}
