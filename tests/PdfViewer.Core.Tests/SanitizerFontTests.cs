using System.Text;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfViewer.Tests;
using NT.AvaloniaControl.PdfViewer.Editing;
using ReaderDocument = NT.AvaloniaControl.PdfViewer.PdfDocument;

namespace PdfViewer.Core.Tests;

public sealed class SanitizerFontTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Retained_embedded_CJK_CID_font_and_CMap_survive_cleanup_with_identical_text_and_pixels()
    {
        const string retained = "中文批注 ABC 123";
        var bounds = new PdfEditRectangle(15, 90, 250, 45);
        await using var author = await PdfEditSession.OpenAsync(PdfFixture.SinglePage("Original page", 300, 220), Token);
        var font = new PdfEmbeddedTrueTypeFont(await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "P0Font.ttf"), Token));
        await author.AddFreeTextAsync(0, bounds, retained, font, 18, Token);
        var input = PromoteAppearanceToPage(await author.ExportAsync(Token), bounds);
        using var original = await ReaderDocument.LoadAsync(input, cancellationToken: Token);
        var expectedText = (await original.GetTextAsync(0, Token)).Text;
        Assert.Contains(retained, expectedText);
        var expectedPixels = (await original.RenderAsync(0, 300, 220, cancellationToken: Token)).Pixels;

        await using var editor = await PdfEditSession.OpenAsync(input, Token);
        await editor.SanitizeAsync(PdfHiddenInformationPolicy.RemoveAllNonPageData, Token);
        var output = await editor.ExportAsync(Token);
        using var cleaned = await ReaderDocument.LoadAsync(output, cancellationToken: Token);
        Assert.Equal(expectedText, (await cleaned.GetTextAsync(0, Token)).Text);
        Assert.Contains("Original page", expectedText);
        Assert.Equal(expectedPixels, (await cleaned.RenderAsync(0, 300, 220, cancellationToken: Token)).Pixels);

        using var checkStream = new MemoryStream(output, writable: false);
        using var check = PdfReader.Open(checkStream, PdfDocumentOpenMode.Import);
        var form = check.Pages[0].Elements.GetDictionary("/Resources")!.Elements.GetDictionary("/XObject")!
            .Elements.GetDictionary("/RetainedFreeText")!;
        var fonts = form.Elements.GetDictionary("/Resources")!.Elements.GetDictionary("/Font")!;
        var cid = fonts.Elements.Select(entry => entry.Value is PdfSharp.Pdf.Advanced.PdfReference reference ? reference.Value : entry.Value)
            .Cast<PdfDictionary>().Single();
        var cmap = cid.Elements.GetDictionary("/ToUnicode")!;
        Assert.DoesNotContain("HIDDEN_CMAP_COMMENT", Encoding.Latin1.GetString(cmap.Stream.UnfilteredValue));
        var descriptor = cid.Elements.GetArray("/DescendantFonts")!.Elements.GetDictionary(0)!.Elements.GetDictionary("/FontDescriptor")!;
        Assert.NotNull(descriptor.Elements.GetDictionary("/FontFile2")!.Stream);
    }

    private static byte[] PromoteAppearanceToPage(byte[] input, PdfEditRectangle bounds)
    {
        using var source = new MemoryStream(input, writable: false);
        using var document = PdfReader.Open(source, PdfDocumentOpenMode.Modify);
        var page = document.Pages[0];
        var annotation = page.Elements.GetArray("/Annots")!.Elements.GetDictionary(0)!;
        var appearance = annotation.Elements.GetDictionary("/AP")!.Elements.GetDictionary("/N")!;
        var fonts = appearance.Elements.GetDictionary("/Resources")!.Elements.GetDictionary("/Font")!;
        var font = fonts.Elements.Select(entry => entry.Value is PdfSharp.Pdf.Advanced.PdfReference reference ? reference.Value : entry.Value)
            .Cast<PdfDictionary>().Single();
        var cmap = font.Elements.GetDictionary("/ToUnicode")!;
        var cmapBytes = cmap.Stream.UnfilteredValue.Concat("\n% HIDDEN_CMAP_COMMENT\n"u8.ToArray()).ToArray();
        cmap.Stream.Value = cmapBytes;
        cmap.Elements.Remove("/Filter");
        cmap.Elements.Remove("/DecodeParms");
        cmap.Elements.SetInteger("/Length", cmapBytes.Length);
        var xObjects = new PdfDictionary(document);
        xObjects.Elements["/RetainedFreeText"] = appearance.Reference!;
        page.Elements.GetDictionary("/Resources")!.Elements["/XObject"] = xObjects;
        var content = new PdfDictionary(document);
        content.CreateStream(Encoding.ASCII.GetBytes(FormattableString.Invariant($"q 1 0 0 1 {bounds.Left} {bounds.Bottom} cm /RetainedFreeText Do Q\n")));
        document.Internals.AddObject(content);
        var contents = new PdfArray(document);
        if (page.Elements.GetArray("/Contents") is { } existing)
            foreach (var item in existing.Elements) contents.Elements.Add(item);
        else contents.Elements.Add(page.Elements["/Contents"]!);
        contents.Elements.Add(content.Reference!);
        page.Elements["/Contents"] = contents;
        // Paint the appearance exactly once as retained page content; the form/font no longer has
        // an annotation owner. The cleaner also discards its original AcroForm default-font aliases.
        page.Elements.Remove("/Annots");
        using var output = new MemoryStream();
        document.Save(output, closeStream: false);
        return output.ToArray();
    }
}
