using Avalonia.Controls;
using PdfViewer.Tests;
using NT.AvaloniaControl.PdfViewer;
using Reader = NT.AvaloniaControl.PdfViewer.PdfViewer;

namespace PdfViewer.Headless.Tests;

/// <summary>
/// Validates ExportPageImageAsync end to end with the independent PngDecode reader: chunk CRCs,
/// zlib payload, output size and every decoded pixel against a fresh reference render — not
/// just the file header.
/// </summary>
public sealed class PngExportTests
{
    [Fact]
    public Task Exported_png_decodes_with_valid_crcs_and_exact_pixels() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await PngFixture.Create(PdfFixture.ReadingDocument());
        var png = await fixture.Reader.ExportPageImageAsync(0, 96, TestContext.Current.CancellationToken);
        var image = PngDecode.Decode(png);
        Assert.Equal(400, image.Width);  // 300pt * 96/72
        Assert.Equal(533, image.Height); // 400pt * 96/72 rounded
        await AssertMatchesReferenceRender(image, PdfFixture.ReadingDocument(), 0, 96);
    });

    [Fact]
    public Task Exported_png_keeps_exact_pixels_at_a_higher_dpi() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await PngFixture.Create(PdfFixture.ReadingDocument());
        var png = await fixture.Reader.ExportPageImageAsync(0, 144, TestContext.Current.CancellationToken);
        var image = PngDecode.Decode(png);
        Assert.Equal(600, image.Width);  // 300pt * 144/72
        Assert.Equal(800, image.Height); // 400pt * 144/72
        await AssertMatchesReferenceRender(image, PdfFixture.ReadingDocument(), 0, 144);
    });

    [Fact]
    public Task Exported_png_of_a_rotated_page_matches_its_reference_render() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await PngFixture.Create(PdfFixture.ReadingDocument());
        var png = await fixture.Reader.ExportPageImageAsync(1, 96, TestContext.Current.CancellationToken);
        var image = PngDecode.Decode(png);
        await AssertMatchesReferenceRender(image, PdfFixture.ReadingDocument(), 1, 96);
    });

    [Fact]
    public Task Independent_decode_rejects_corrupted_chunk_crcs_and_image_data() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await PngFixture.Create(PdfFixture.ReadingDocument());
        var png = await fixture.Reader.ExportPageImageAsync(0, 96, TestContext.Current.CancellationToken);

        // A corrupted chunk CRC must be refused before any pixel is produced.
        var badCrc = (byte[])png.Clone();
        badCrc[30] ^= 0xff; // inside the IHDR chunk's stored CRC
        var crcError = Assert.Throws<InvalidDataException>(() => PngDecode.Decode(badCrc));
        Assert.Contains("CRC", crcError.Message);

        // A corrupted IDAT payload must fail the zlib / Adler-32 path.
        var badIdat = (byte[])png.Clone();
        var idat = FindChunk(badIdat, "IDAT"u8);
        badIdat[idat + 40] ^= 0xff;
        Assert.Throws<InvalidDataException>(() => PngDecode.Decode(badIdat));
    });

    private static async Task AssertMatchesReferenceRender(PngDecode.PngImage image, byte[] bytes, int pageIndex, double dpi)
    {
        await using var document = await PdfDocument.LoadAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        var page = document.Pages[pageIndex];
        var width = Math.Max(1, (int)Math.Round(page.Width * dpi / 72));
        var height = Math.Max(1, (int)Math.Round(page.Height * dpi / 72));
        Assert.Equal(width, image.Width);
        Assert.Equal(height, image.Height);
        var rendered = await document.RenderAsync(pageIndex, width, height, cancellationToken: TestContext.Current.CancellationToken);
        var expected = new byte[width * height * 4];
        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                var source = row * rendered.Stride + column * 4;
                var target = (row * width + column) * 4;
                expected[target] = rendered.Pixels[source + 2];     // R
                expected[target + 1] = rendered.Pixels[source + 1]; // G
                expected[target + 2] = rendered.Pixels[source];     // B
                expected[target + 3] = 255;                         // opaque
            }
        }
        if (!expected.AsSpan().SequenceEqual(image.Rgba))
        {
            var index = 0;
            while (index < expected.Length && expected[index] == image.Rgba[index]) index++;
            Assert.Fail($"Decoded pixel mismatch at byte {index} of {expected.Length}.");
        }
    }

    private static int FindChunk(byte[] png, ReadOnlySpan<byte> type)
    {
        var offset = 8;
        while (offset + 12 <= png.Length)
        {
            var length = (png[offset] << 24) | (png[offset + 1] << 16) | (png[offset + 2] << 8) | png[offset + 3];
            if (png.AsSpan(offset + 4, 4).SequenceEqual(type)) return offset;
            offset += 12 + length;
        }
        throw new InvalidOperationException("Chunk not found.");
    }

    private sealed class PngFixture : IDisposable
    {
        internal Reader Reader { get; } = new();
        internal Window Window { get; }

        private PngFixture()
        {
            Window = new Window { Width = 1000, Height = 700, Content = Reader };
            Window.Show();
            Window.UpdateLayout();
            HeadlessTestScope.Drain();
        }

        internal static async Task<PngFixture> Create(byte[] bytes)
        {
            var fixture = new PngFixture();
            try
            {
                await fixture.Reader.LoadDocumentAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
                fixture.Window.UpdateLayout();
                HeadlessTestScope.Drain();
                await fixture.Reader.WaitForIdleAsync(TestContext.Current.CancellationToken);
                return fixture;
            }
            catch { fixture.Dispose(); throw; }
        }

        public void Dispose()
        {
            Reader.Dispose();
            Window.Close();
        }
    }
}
