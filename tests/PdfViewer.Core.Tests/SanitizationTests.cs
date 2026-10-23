using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using NT.AvaloniaControl.PdfViewer;
using NT.AvaloniaControl.PdfViewer.Editing;

namespace PdfViewer.Core.Tests;

public sealed class SanitizationTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private const PdfHiddenInformationPolicy Policy = PdfHiddenInformationPolicy.RemoveAllNonPageData;
    private static readonly PdfRedactionRegion Target = new(0, new PdfEditRectangle(15, 180, 270, 30));

    [Fact]
    public async Task Whole_text_deletion_commits_one_revision_and_survives_save_and_reopen()
    {
        var input = Fixture();
        await using var session = await PdfEditSession.OpenAsync(input, Token);
        var result = await session.ApplyRedactionsAsync([Target], Policy, Token);
        var output = await session.ExportAsync(Token);
        Assert.Equal(1, result.Revision);
        Assert.Equal(1, result.RedactionRegionCount);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(input)), result.InputSha256);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(output)), result.OutputSha256);
        Assert.True(session.IsDirty);
        using (var document = await PdfDocument.LoadAsync(output, cancellationToken: Token))
        {
            var text = (await document.GetTextAsync(0, Token)).Text;
            Assert.DoesNotContain("TARGET_SECRET", text);
            Assert.Contains("KEEP_TEXT", text);
            Assert.Empty(document.Bookmarks);
        }
        using var saved = new MemoryStream();
        await session.SaveAsync(saved, Token);
        Assert.False(session.IsDirty);
        Assert.Equal(output, saved.ToArray());
        await using var reopened = await PdfEditSession.OpenAsync(saved.ToArray(), Token);
        Assert.Equal(0, reopened.Revision);
        Assert.False(reopened.IsDirty);
    }

    [Fact]
    public async Task Sanitization_removes_stored_and_visible_annotation_instead_of_flattening_it()
    {
        await using var session = await PdfEditSession.OpenAsync(Fixture(), Token);
        var font = new PdfEmbeddedTrueTypeFont(await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "P0Font.ttf"), Token));
        await session.AddFreeTextAsync(0, new PdfEditRectangle(15, 90, 250, 45), "中文批注 ABC", font, 18, Token);
        Assert.Single(await session.GetFreeTextAnnotationsAsync(0, Token));
        var result = await session.SanitizeAsync(Policy, Token);
        Assert.Equal(2, result.Revision);
        Assert.Equal(0, result.RedactionRegionCount);
        Assert.Empty(await session.GetFreeTextAnnotationsAsync(0, Token));
        using var reader = await PdfDocument.LoadAsync(await session.ExportAsync(Token), cancellationToken: Token);
        Assert.Contains("TARGET_SECRET", (await reader.GetTextAsync(0, Token)).Text);
        var image = await reader.RenderAsync(0, 300, 240, cancellationToken: Token);
        // The removed annotation occupied this originally blank area.
        for (var y = 110; y < 140; y++)
        for (var x = 20; x < 250; x++)
        {
            var offset = y * image.Stride + x * 4;
            Assert.Equal(255, image.Pixels[offset]);
            Assert.Equal(255, image.Pixels[offset + 1]);
            Assert.Equal(255, image.Pixels[offset + 2]);
        }
    }

    [Fact]
    public async Task A_later_partial_intersection_does_not_commit_earlier_valid_deletions_or_sanitization()
    {
        await using var session = await PdfEditSession.OpenAsync(Fixture(), Token);
        var before = await session.ExportAsync(Token);
        await Assert.ThrowsAsync<NotSupportedException>(() => session.ApplyRedactionsAsync(
            [Target, new PdfRedactionRegion(0, new PdfEditRectangle(25, 45, 15, 30))], Policy, Token));
        await Unchanged(session, before);
    }

    [Fact]
    public async Task A_region_that_selects_no_object_is_refused_without_committing()
    {
        await using var session = await PdfEditSession.OpenAsync(Fixture(), Token);
        var before = await session.ExportAsync(Token);
        await Assert.ThrowsAnyAsync<Exception>(() => session.ApplyRedactionsAsync(
            [new PdfRedactionRegion(0, new PdfEditRectangle(250, 100, 10, 10))], Policy, Token));
        await Unchanged(session, before);
    }

    [Fact]
    public async Task Invalid_requests_and_missing_explicit_policy_are_rejected()
    {
        await using var session = await PdfEditSession.OpenAsync(Fixture(), Token);
        var before = await session.ExportAsync(Token);
        await Assert.ThrowsAsync<ArgumentException>(() => session.ApplyRedactionsAsync([], Policy, Token));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => session.ApplyRedactionsAsync(
            [new PdfRedactionRegion(1, Target.Bounds)], Policy, Token));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => session.ApplyRedactionsAsync(
            [new PdfRedactionRegion(0, new PdfEditRectangle(double.NaN, 0, 10, 10))], Policy, Token));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => session.SanitizeAsync((PdfHiddenInformationPolicy)0, Token));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => session.ApplyRedactionsAsync([Target], (PdfHiddenInformationPolicy)99, Token));
        await Unchanged(session, before);
    }

    [Fact]
    public async Task Cancelled_cleaning_and_redaction_leave_the_snapshot_and_revision_intact()
    {
        await using var session = await PdfEditSession.OpenAsync(Fixture(), Token);
        var before = await session.ExportAsync(Token);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.SanitizeAsync(Policy, cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.ApplyRedactionsAsync([Target], Policy, cancelled.Token));
        await Unchanged(session, before);
    }

    [Fact]
    public async Task Optional_content_is_refused_instead_of_silently_changing_retained_painting()
    {
        await using var session = await PdfEditSession.OpenAsync(Fixture(optionalContent: true), Token);
        var before = await session.ExportAsync(Token);
        await Assert.ThrowsAsync<NotSupportedException>(() => session.SanitizeAsync(Policy, Token));
        await Unchanged(session, before);
    }

    [Fact]
    public async Task Rotated_page_redaction_is_refused_and_does_not_remove_document_data()
    {
        await using var session = await PdfEditSession.OpenAsync(Fixture(rotation: 90), Token);
        var before = await session.ExportAsync(Token);
        await Assert.ThrowsAsync<NotSupportedException>(() => session.ApplyRedactionsAsync([Target], Policy, Token));
        await Unchanged(session, before);
    }

    [Fact]
    public async Task Standalone_cleaning_preserves_rotated_cropped_page_rendering()
    {
        var source = Fixture(rotation: 90, crop: true);
        using var original = await PdfDocument.LoadAsync(source, cancellationToken: Token);
        var expected = await original.RenderAsync(0, 240, 300, cancellationToken: Token);
        await using var session = await PdfEditSession.OpenAsync(source, Token);
        await session.SanitizeAsync(Policy, Token);
        using var cleaned = await PdfDocument.LoadAsync(await session.ExportAsync(Token), cancellationToken: Token);
        var actual = await cleaned.RenderAsync(0, 240, 300, cancellationToken: Token);
        Assert.Equal(expected.Pixels, actual.Pixels);
        Assert.Equal(original.Pages[0], cleaned.Pages[0]);
    }

    [Fact]
    public async Task Inherited_resources_boxes_and_rotation_survive_standalone_cleaning()
    {
        var input = Fixture(rotation: 90, crop: true, inherited: true);
        using var original = await PdfDocument.LoadAsync(input, cancellationToken: Token);
        var expected = await original.RenderAsync(0, 240, 300, cancellationToken: Token);
        await using var session = await PdfEditSession.OpenAsync(input, Token);
        await session.SanitizeAsync(Policy, Token);
        using var cleaned = await PdfDocument.LoadAsync(await session.ExportAsync(Token), cancellationToken: Token);
        Assert.Contains("KEEP_TEXT", (await cleaned.GetTextAsync(0, Token)).Text);
        Assert.Equal(original.Pages[0], cleaned.Pages[0]);
        Assert.Equal(expected.Pixels, (await cleaned.RenderAsync(0, 240, 300, cancellationToken: Token)).Pixels);
    }

    [Fact]
    public async Task Disposed_session_refuses_cleaning_and_redaction()
    {
        var session = await PdfEditSession.OpenAsync(Fixture(), Token);
        await session.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => session.SanitizeAsync(Policy, Token));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => session.ApplyRedactionsAsync([Target], Policy, Token));
    }

    private static async Task Unchanged(PdfEditSession session, byte[] expected)
    {
        Assert.Equal(0, session.Revision);
        Assert.False(session.IsDirty);
        Assert.Equal(expected, await session.ExportAsync(Token));
    }

    private static byte[] Fixture(int rotation = 0, bool crop = false, bool optionalContent = false, bool inherited = false)
    {
        var content = "BT /F1 16 Tf 20 190 Td (TARGET_SECRET) Tj ET\nBT /F1 16 Tf 20 50 Td (KEEP_TEXT) Tj ET\n";
        if (optionalContent) content = "/OC /Layer BDC\n" + content + "EMC\n";
        var inheritedEntries = $"/MediaBox [0 0 300 240] /Rotate {rotation} {(crop ? "/CropBox [10 10 290 230]" : "")} /Resources << /Font << /F1 4 0 R >> /Properties << /Layer 8 0 R >> >>";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R /Outlines 6 0 R >>",
            $"<< /Type /Pages /Kids [3 0 R] /Count 1 {(inherited ? inheritedEntries : "")} >>",
            $"<< /Type /Page /Parent 2 0 R {(inherited ? "" : inheritedEntries)} /Contents 5 0 R /PrivateData (HIDDEN_PAGE) >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {content.Length} >>\nstream\n{content}endstream",
            "<< /Type /Outlines /First 7 0 R /Last 7 0 R /Count 1 >>",
            "<< /Title (HIDDEN_OUTLINE) /Parent 6 0 R /Dest [3 0 R /Fit] >>",
            "<< /Type /OCG /Name (HIDDEN_LAYER) >>",
            "<< /Author (HIDDEN_AUTHOR) /Subject (HIDDEN_SUBJECT) >>",
        };
        using var output = new MemoryStream();
        void Write(string text) => output.Write(Encoding.ASCII.GetBytes(text));
        Write("%PDF-1.4\n");
        var offsets = new List<long>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(output.Position);
            Write($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = output.Position;
        Write($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets) Write(offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
        Write($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R /Info 9 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return output.ToArray();
    }
}
