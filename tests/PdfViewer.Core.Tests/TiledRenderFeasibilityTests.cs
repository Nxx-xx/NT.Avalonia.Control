using NT.AvaloniaControl.PdfViewer;

namespace PdfViewer.Tests;

/// <summary>
/// P3-03 tiling feasibility ("分块方案需证明边界、透明度和坐标一致"): the region render
/// primitive must reproduce a full-page render crop one-to-one at shared boundaries, stay
/// fully opaque like the full-page path, and map tile coordinates consistently under
/// rotation. Display tiling is only integrated when deep zoom outgrows the decode caps;
/// these tests are the standing proof that the scheme itself is sound.
/// </summary>
public sealed class TiledRenderFeasibilityTests
{
    [Fact]
    public async Task Region_tiles_stitch_to_the_full_render_at_shared_boundaries()
    {
        using var document = await PdfDocument.LoadAsync(PdfFixture.ManyPages(1, 300, 400),
            cancellationToken: TestContext.Current.CancellationToken);
        var full = await document.RenderAsync(0, 600, 800, cancellationToken: TestContext.Current.CancellationToken);
        // Two vertical strips split at an odd edge (x=287) and a horizontal strip split at
        // y=333: every seam crosses glyph antialiasing, not just white paper.
        var left = await document.RenderPageRegionAsync(0, 600, 800, 0, 0, 287, 800,
            cancellationToken: TestContext.Current.CancellationToken);
        var right = await document.RenderPageRegionAsync(0, 600, 800, 287, 0, 313, 800,
            cancellationToken: TestContext.Current.CancellationToken);
        var top = await document.RenderPageRegionAsync(0, 600, 800, 0, 0, 600, 333,
            cancellationToken: TestContext.Current.CancellationToken);
        var bottom = await document.RenderPageRegionAsync(0, 600, 800, 0, 333, 600, 467,
            cancellationToken: TestContext.Current.CancellationToken);
        AssertTileCrop(full, left, 0, 0);
        AssertTileCrop(full, right, 287, 0);
        AssertTileCrop(full, top, 0, 0);
        AssertTileCrop(full, bottom, 0, 333);
    }

    [Fact]
    public async Task Region_tiles_stay_fully_opaque_like_the_full_render()
    {
        using var document = await PdfDocument.LoadAsync(PdfFixture.ManyPages(1, 300, 400),
            cancellationToken: TestContext.Current.CancellationToken);
        var full = await document.RenderAsync(0, 400, 600, cancellationToken: TestContext.Current.CancellationToken);
        var tile = await document.RenderPageRegionAsync(0, 400, 600, 100, 200, 200, 200,
            cancellationToken: TestContext.Current.CancellationToken);
        for (var index = 3; index < tile.Pixels.Length; index += 4)
            Assert.Equal(255, tile.Pixels[index]);
        for (var index = 3; index < full.Pixels.Length; index += 4)
            Assert.Equal(255, full.Pixels[index]);
    }

    [Fact]
    public async Task Region_tiles_map_coordinates_under_rotation()
    {
        using var document = await PdfDocument.LoadAsync(PdfFixture.ManyPages(1, 300, 400),
            cancellationToken: TestContext.Current.CancellationToken);
        // The rotated display raster of the 300×400 page at 2× is 800×600.
        var full = await document.RenderAsync(0, 800, 600, 90, cancellationToken: TestContext.Current.CancellationToken);
        var quadrant = await document.RenderPageRegionAsync(0, 800, 600, 377, 211, 223, 189, 90,
            cancellationToken: TestContext.Current.CancellationToken);
        AssertTileCrop(full, quadrant, 377, 211);
    }

    [Fact]
    public async Task Region_outside_the_raster_is_rejected()
    {
        using var document = await PdfDocument.LoadAsync(PdfFixture.ManyPages(1),
            cancellationToken: TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => document.RenderPageRegionAsync(0, 600, 800, -1, 0, 10, 10,
            cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => document.RenderPageRegionAsync(0, 600, 800, 0, 0, 601, 10,
            cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => document.RenderPageRegionAsync(0, 600, 800, 0, 0, 0, 10,
            cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => document.RenderPageRegionAsync(0, 600, 800, 500, 700, 100, 200,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>Every pixel of the tile equals the same pixel of the full render's crop.</summary>
    private static void AssertTileCrop(PdfRenderedPage full, PdfRenderedPage tile, int offsetX, int offsetY)
    {
        Assert.Equal(tile.Width * 4, tile.Stride);
        for (var y = 0; y < tile.Height; y++)
        {
            for (var x = 0; x < tile.Width; x++)
            {
                var expected = (offsetY + y) * full.Stride + (offsetX + x) * 4;
                var actual = y * tile.Stride + x * 4;
                for (var channel = 0; channel < 4; channel++)
                    Assert.True(full.Pixels[expected + channel] == tile.Pixels[actual + channel],
                        $"Tile pixel ({x},{y}) channel {channel} is {tile.Pixels[actual + channel]}, " +
                        $"full render crop at ({offsetX + x},{offsetY + y}) is {full.Pixels[expected + channel]}.");
            }
        }
    }
}
