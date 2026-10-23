using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using NT.AvaloniaControl.PdfViewer;
using NT.AvaloniaControl.PdfViewer.Editing;

namespace PdfViewer.P0;

internal static class ProductionWriterProbe
{
    internal static async Task<object> RunAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var checks = new List<object>();
        void Check(string id, bool passed, string detail) { checks.Add(new { Id = id, Passed = passed, Detail = detail }); }
        var source = PreservationFixture();
        var original = (byte[])source.Clone();
        // Preserve the full-font writer regression; the font-subset probe separately exercises the default.
        var font = new PdfEmbeddedTrueTypeFont(File.ReadAllBytes(Path.Combine(Directory.GetParent(directory)!.FullName, "fonts", "P0AnnotationCjk.ttf")), PdfFontEmbeddingMode.Full);
        File.WriteAllBytes(Path.Combine(directory, "input.pdf"), original);
        var encryptedRefused = false;
        try { await using var invalid = await PdfEditSession.OpenAsync(PdfViewer.Tests.PdfFixture.PasswordProtected()); }
        catch (NotSupportedException) { encryptedRefused = true; }
        Check("encrypted-input-refused", encryptedRefused, "Editing does not silently remove input encryption.");
        var signedPath = Path.Combine(Directory.GetParent(directory)!.FullName, "fixtures", "signed-input.pdf");
        if (File.Exists(signedPath))
        {
            var signedRefused = false;
            try { await using var invalid = await PdfEditSession.OpenAsync(File.ReadAllBytes(signedPath)); }
            catch (NotSupportedException) { signedRefused = true; }
            Check("signed-input-refused", signedRefused, "Actual signed fixture refused before structural rewriting.");
        }
        await using var session = await PdfEditSession.OpenAsync(source);
        Array.Fill(source, (byte)0);
        Check("owned-input", (await session.ExportAsync()).SequenceEqual(original), "Caller mutation cannot alter a session.");
        Check("initial-state", !session.IsDirty && session.Revision == 0 && session.PageCount == 1, "Unmodified session starts at revision zero.");
        var first = await session.AddFreeTextAsync(0, new PdfEditRectangle(30, 240, 350, 50), "中文批注 ABC 123", font, 18);
        var firstBytes = await session.ExportAsync();
        File.WriteAllBytes(Path.Combine(directory, "freetext-created.pdf"), firstBytes);
        Check("first-commit", session.IsDirty && session.Revision == 1 && (await session.GetFreeTextAnnotationsAsync(0)).Single().Id == first.Id, "Bytes and revision commit together after validation.");
        await using var reopened = await PdfEditSession.OpenAsync(firstBytes);
        var item = (await reopened.GetFreeTextAnnotationsAsync(0)).Single();
        Check("reopen-id-contents", item.Id == first.Id && item.Text == "中文批注 ABC 123", "Persisted /NM and canonical Contents roundtrip.");
        await reopened.UpdateFreeTextAsync(item.Id, 0, new PdfEditRectangle(40, 230, 350, 60), "中文批注 编辑后 ABC 123", font, 18);
        var edited = await reopened.ExportAsync();
        File.WriteAllBytes(Path.Combine(directory, "freetext-edited.pdf"), edited);
        using (var reader = await NT.AvaloniaControl.PdfViewer.PdfDocument.LoadAsync(edited))
        {
            var raster = await reader.RenderAsync(0, 800, 800);
            Check("native-render", raster.Pixels.Any(x => x < 200), "Updated FreeText and original content render with the existing production reader.");
        }
        var beforeFailure = await reopened.ExportAsync();
        var revision = reopened.Revision;
        var rejected = false;
        try { await reopened.AddFreeTextAsync(0, new PdfEditRectangle(0, 0, 200, 40), "Missing Ω glyph", font); }
        catch (ArgumentException) { rejected = true; }
        var afterFailure = await reopened.ExportAsync();
        Check("missing-glyph-atomic", rejected && reopened.Revision == revision && beforeFailure.SequenceEqual(afterFailure), "Missing glyph is rejected before any state commit.");
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            rejected = false;
            try { await reopened.AddFreeTextAsync(0, new PdfEditRectangle(0, 0, 200, 40), "ABC", font, cancellationToken: cancelled.Token); }
            catch (OperationCanceledException) { rejected = true; }
            var afterCancel = await reopened.ExportAsync();
            Check("cancel-atomic", rejected && reopened.Revision == revision && beforeFailure.SequenceEqual(afterCancel), "Cancelled edit leaves bytes and revision unchanged.");
        }
        var path = Path.Combine(directory, "atomic-saved.pdf");
        File.WriteAllBytes(path, original);
        if (OperatingSystem.IsWindows())
        {
            using var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var failed = false;
            try { await reopened.SaveAsync(path); }
            catch (IOException) { failed = true; }
            Check("locked-target-original-intact", failed && reopened.IsDirty && File.ReadAllBytes(path).SequenceEqual(original), "Failure retains old file and Dirty.");
        }
        await reopened.SaveAsync(path);
        Check("atomic-save-success", !reopened.IsDirty && File.ReadAllBytes(path).SequenceEqual(edited) && !Directory.EnumerateFiles(directory, "*.writing").Any(), "Verified atomic replacement clears only the saved revision; temps removed.");
        var writerAssembly = typeof(PdfSharp.Pdf.PdfDocument).Assembly;
        var pkcs = typeof(System.Security.Cryptography.Pkcs.SignedCms).Assembly;
        return new { Status = "bounded-production-writer", Dependency = "PDFsharp Core 6.2.4 (MIT) + PDFium155.0.8044",
            FunctionalAssemblySHA256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(PdfEditSession).Assembly.Location))),
            PkcsAssembly = new { Version = pkcs.GetName().Version?.ToString(), SHA256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(pkcs.Location))) },
            WriterAssembly = new { Version = writerAssembly.GetName().Version?.ToString(), SHA256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(writerAssembly.Location))) }, Checks = checks,
            Outputs = Directory.EnumerateFiles(directory, "*.pdf").Select(p => new { File = Path.GetFileName(p), SHA256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))) }).ToArray(),
            Limitations = new[] { "This regression explicitly selects Full embedding; AutomaticSubset is verified separately by font-subset. Single-line static TrueType, no shaping/fallback.", "Unsigned and unencrypted structural edit sessions only; no UI, Undo or AutoSave.", "Independent preservation/AP/font/CMS/platform checks are separate; this does not establish arbitrary PDF compatibility." } };
    }

    private static byte[] PreservationFixture()
    {
        static string Stream(string value, string extras = "") => $"<< /Length {Encoding.ASCII.GetByteCount(value)} {extras} >>\nstream\n{value}\nendstream";
        string[] objects = [
            "<< /Type /Catalog /Pages 2 0 R /AcroForm 6 0 R /Outlines 9 0 R /Names << /EmbeddedFiles << /Names [(keep.txt) 12 0 R] >> >> /CustomExtension << /Meaning (KEEP_UNKNOWN_DICTIONARY) /Version 7 >> >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 400 400] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R /Annots [7 0 R] >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            Stream("BT /F1 16 Tf 30 350 Td (KEEP ORIGINAL PAGE CONTENT) Tj ET\n"),
            "<< /Fields [7 0 R] /DR << /Font << /F1 4 0 R >> >> /DA (/F1 12 Tf 0 g) /NeedAppearances false >>",
            "<< /Type /Annot /Subtype /Widget /FT /Tx /T (PreservedField) /V (KEEP_FIELD_VALUE) /DV (KEEP_DEFAULT_VALUE) /Rect [30 50 300 80] /P 3 0 R /AP << /N 8 0 R >> >>",
            Stream("BT /F1 12 Tf 3 10 Td (KEEP_FIELD_VALUE) Tj ET", "/Type /XObject /Subtype /Form /BBox [0 0 270 30] /Resources << /Font << /F1 4 0 R >> >>"),
            "<< /Type /Outlines /First 10 0 R /Last 10 0 R /Count 1 >>",
            "<< /Title (KEEP_OUTLINE) /Parent 9 0 R /Dest [3 0 R /Fit] >>",
            Stream("KEEP_ATTACHMENT_DATA", "/Type /EmbeddedFile"),
            "<< /Type /Filespec /F (keep.txt) /EF << /F 11 0 R >> >>"
        ];
        using var output = new MemoryStream();
        void Append(string value) => output.Write(Encoding.ASCII.GetBytes(value));
        Append("%PDF-1.7\n");
        var offsets = new List<long> { 0 };
        for (var i = 0; i < objects.Length; i++) { offsets.Add(output.Position); Append($"{i+1} 0 obj\n{objects[i]}\nendobj\n"); }
        var xref = output.Position;
        Append($"xref\n0 {objects.Length+1}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) Append(offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
        Append($"trailer\n<< /Size {objects.Length+1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return output.ToArray();
    }
}
