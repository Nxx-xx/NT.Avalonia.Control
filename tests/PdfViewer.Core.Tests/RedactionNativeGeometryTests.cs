using System.Globalization;
using System.Text;
using NT.AvaloniaControl.PdfViewer;
using NT.AvaloniaControl.PdfViewer.Editing;

namespace PdfViewer.Core.Tests;

public sealed class RedactionNativeGeometryTests
{
    private static CancellationToken TestToken => TestContext.Current.CancellationToken;
    private const PdfHiddenInformationPolicy Policy = PdfHiddenInformationPolicy.RemoveAllNonPageData;

    [Fact]
    public async Task Whole_shared_Form_invocation_is_deleted_on_only_the_requested_page()
    {
        var source = Document(
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Resources << /Font << /F1 5 0 R >> /XObject << /Shared 8 0 R >> >> /Contents 6 0 R >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Resources << /Font << /F1 5 0 R >> /XObject << /Shared 8 0 R >> >> /Contents 7 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            Stream("BT /F1 12 Tf 20 250 Td (KEEP_PAGE_ZERO) Tj ET\nq 1 0 0 1 20 100 cm /Shared Do Q\n"),
            Stream("BT /F1 12 Tf 20 250 Td (KEEP_PAGE_ONE) Tj ET\nq 1 0 0 1 20 100 cm /Shared Do Q\n"),
            Stream("BT /F1 12 Tf 0 20 Td (SHARED_FORM_TEXT) Tj ET\n", "/Type /XObject /Subtype /Form /BBox [0 0 260 80] /Resources << /Font << /F1 5 0 R >> >>")
        ]);
        await using var session = await PdfEditSession.OpenAsync(source, TestToken);
        await session.ApplyRedactionsAsync([new PdfRedactionRegion(0, new PdfEditRectangle(5, 90, 280, 90))], Policy, TestToken);
        using var output = await PdfDocument.LoadAsync(await session.ExportAsync(TestToken), cancellationToken: TestToken);
        var first = (await output.GetTextAsync(0, TestToken)).Text;
        var second = (await output.GetTextAsync(1, TestToken)).Text;
        Assert.DoesNotContain("SHARED_FORM_TEXT", first);
        Assert.Contains("KEEP_PAGE_ZERO", first);
        Assert.Contains("SHARED_FORM_TEXT", second);
        Assert.Contains("KEEP_PAGE_ONE", second);
    }

    [Fact]
    public async Task Clipping_text_is_refused_without_committing_any_changes()
    {
        var source = Document(
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            Stream("q BT /F1 30 Tf 7 Tr 20 240 Td (CLIP) Tj ET 0 0 300 300 re f Q\n")
        ]);
        await using var session = await PdfEditSession.OpenAsync(source, TestToken);
        await Assert.ThrowsAsync<NotSupportedException>(() => session.ApplyRedactionsAsync(
            [new PdfRedactionRegion(0, new PdfEditRectangle(0, 0, 300, 300))], Policy, TestToken));
        Assert.Equal(0, session.Revision);
        Assert.False(session.IsDirty);
        Assert.Equal(source, await session.ExportAsync(TestToken));
    }

    [Theory]
    [InlineData("(HIDDEN_SECRET) q\nQ\n")]
    [InlineData("10 20 (HIDDEN_SECRET) m 30 40 l S\n")]
    [InlineData("[(HIDDEN_SECRET)] 0 d\n")]
    [InlineData("/HIDDEN_SECRET ri\n")]
    public async Task Extra_or_wrongly_typed_operator_operands_are_refused(string content)
    {
        var source = SimpleDocument(content, "<< >>");
        await using var session = await PdfEditSession.OpenAsync(source, TestToken);
        await Assert.ThrowsAsync<NotSupportedException>(() => session.SanitizeAsync(Policy, TestToken));
        Assert.Equal(0, session.Revision);
        Assert.False(session.IsDirty);
        Assert.Equal(source, await session.ExportAsync(TestToken));
    }

    [Fact]
    public async Task Implicit_default_color_spaces_are_refused_instead_of_being_silently_dropped()
    {
        var source = SimpleDocument("0 1 0 rg 10 10 40 40 re f\n",
            "<< /ColorSpace << /DefaultRGB [/CalRGB << /WhitePoint [0.9505 1 1.089] /Gamma [2.2 2.2 2.2] >>] >> >>");
        await using var session = await PdfEditSession.OpenAsync(source, TestToken);
        await Assert.ThrowsAsync<NotSupportedException>(() => session.SanitizeAsync(Policy, TestToken));
        Assert.Equal(0, session.Revision);
        Assert.Equal(source, await session.ExportAsync(TestToken));
    }

    private static byte[] SimpleDocument(string content, string resources) => Document(
    [
        "<< /Type /Catalog /Pages 2 0 R >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Resources {resources} /Contents 4 0 R >>",
        Stream(content)
    ]);

    private static string Stream(string content, string dictionary = "") =>
        $"<< {dictionary} /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}endstream";

    private static byte[] Document(IReadOnlyList<string> objects)
    {
        var builder = new StringBuilder("%PDF-1.7\n");
        var offsets = new List<int> { 0 };
        for (var index = 0; index < objects.Count; ++index)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
            builder.Append(CultureInfo.InvariantCulture, $"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }
        var xref = Encoding.ASCII.GetByteCount(builder.ToString());
        builder.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) builder.Append(CultureInfo.InvariantCulture, $"{offset:D10} 00000 n \n");
        builder.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(builder.ToString());
    }
}
