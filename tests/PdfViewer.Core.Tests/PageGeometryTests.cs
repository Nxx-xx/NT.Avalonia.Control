using Avalonia;
using NT.AvaloniaControl.PdfViewer;

namespace PdfViewer.Core.Tests;

public sealed class PageGeometryTests
{
    [Theory]
    [InlineData(0, 10, 20, 30, 40)]
    [InlineData(90, 340, 10, 40, 30)]
    [InlineData(180, 260, 340, 30, 40)]
    [InlineData(270, 20, 260, 40, 30)]
    public void Highlight_rectangles_and_pointer_hit_testing_share_rotation_and_scale(
        int rotation, double x, double y, double width, double height)
    {
        var page = new PdfPageInfo(0, 300, 400);
        var glyph = new Rect(10, 20, 30, 40);
        foreach (var zoom in new[] { .25, 1d, 2.5 })
        {
            var layout = new PdfPageLayout();
            layout.Build([page], zoom, rotation, 1000, PdfPageDisplayMode.Continuous, 0, true);
            var display = layout.ToDisplay(0, glyph, page);
            var origin = layout.Pages[0].TopLeft;
            var scale = zoom * 96 / 72;
            Assert.Equal(origin.X + x * scale, display.X, 5);
            Assert.Equal(origin.Y + y * scale, display.Y, 5);
            Assert.Equal(width * scale, display.Width, 5);
            Assert.Equal(height * scale, display.Height, 5);
            var selected = layout.ToPage(0, display.Center, page);
            Assert.Equal(glyph.Center.X, selected.X, 5);
            Assert.Equal(glyph.Center.Y, selected.Y, 5);
        }
    }

    [Fact]
    public void Variable_page_sizes_remain_centered_and_far_page_lookup_is_correct()
    {
        var pages = Enumerable.Range(0, 1000).Select(index => new PdfPageInfo(index, index % 2 == 0 ? 300 : 600, index % 2 == 0 ? 400 : 200)).ToArray();
        var layout = new PdfPageLayout();
        layout.Build(pages, 1, 0, 1200, PdfPageDisplayMode.Continuous, 0, true);
        Assert.Equal(1200, layout.Extent.Width);
        Assert.Equal(1000, layout.Pages.Count);
        foreach (var index in new[] { 0, 1, 55, 500, 999 })
        {
            var bounds = layout.Pages[index];
            Assert.Equal(600, bounds.Center.X, 5);
            Assert.Equal(index, layout.PageAt(bounds.Center.Y));
            if (index > 0) Assert.Equal(PdfPageLayout.Gap, bounds.Top - layout.Pages[index - 1].Bottom, 5);
        }
        Assert.Equal(0, layout.PageAt(-1000));
        Assert.Equal(999, layout.PageAt(layout.Extent.Height + 1000));
    }
}
