using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using NT.AvaloniaControl.PdfViewer.Editing;

namespace PdfViewer.P0;

/// <summary>Authored geometry corpus exercised exclusively through the public edit session.</summary>
internal static class ProductionRedactionProbe
{
    private const string TextSecret = "PIPE_TARGET_TEXT_82FA";
    private const string SharedSecret = "PIPE_SHARED_FORM_3C9D";
    private static readonly PdfRedactionRegion[] Regions =
    [
        new(0, new PdfEditRectangle(145, 330, 250, 30)),
        new(0, new PdfEditRectangle(35, 205, 70, 50)),
        new(0, new PdfEditRectangle(125, 125, 245, 40)),
        new(0, new PdfEditRectangle(35, 75, 90, 40))
    ];

    internal static async Task<object> RunAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var checks = new List<object>();
        void Check(string id, bool passed, string detail) => checks.Add(new { Id = id, Passed = passed, Detail = detail });
        var source = Fixture();
        File.WriteAllBytes(Path.Combine(directory, "input.pdf"), source);
        await using (var session = await PdfEditSession.OpenAsync(source))
        {
            var result = await session.SanitizeAsync(PdfHiddenInformationPolicy.RemoveAllNonPageData);
            var sanitized = await session.ExportAsync();
            File.WriteAllBytes(Path.Combine(directory, "sanitized.pdf"), sanitized);
            Check("sanitize-public-session-commit", session.Revision == 1 && session.IsDirty,
                "Explicit destructive cleanup policy commits a separately validated snapshot.");
            Check("sanitize-result-binds-snapshot", result.Revision == 1 && result.PageCount == 2 && result.RedactionRegionCount == 0
                && result.InputSha256 == Hash(source) && result.OutputSha256 == Hash(sanitized), "Result hashes bind exact original/candidate bytes and transaction revision.");
            var refused = false;
            try { await session.ApplyRedactionsAsync([new PdfRedactionRegion(0, new PdfEditRectangle(40, 210, 20, 40))], PdfHiddenInformationPolicy.RemoveAllNonPageData); }
            catch (NotSupportedException) { refused = true; }
            Check("already-dirty-session-rollback", refused && session.IsDirty && session.Revision == 1 && (await session.ExportAsync()).SequenceEqual(sanitized),
                "Failure preserves an earlier successful unsaved transaction and its dirty/revision state.");
        }
        await using (var session = await PdfEditSession.OpenAsync(source))
        {
            var result = await session.ApplyRedactionsAsync(Regions, PdfHiddenInformationPolicy.RemoveAllNonPageData);
            var output = await session.ExportAsync();
            File.WriteAllBytes(Path.Combine(directory, "redacted.pdf"), output);
            Check("pipeline-public-session-commit", session.Revision == 1 && session.IsDirty,
                "Four geometric regions run content removal, explicit cleanup, full rewrite and validation before one commit.");
            Check("pipeline-result-binds-snapshot", result.Revision == 1 && result.PageCount == 2 && result.RedactionRegionCount == 4
                && result.InputSha256 == Hash(source) && result.OutputSha256 == Hash(output), "Result hashes bind the exact input, final snapshot and all four requested regions.");
            using var reader = await NT.AvaloniaControl.PdfViewer.PdfDocument.LoadAsync(output);
            var text0 = (await reader.GetTextAsync(0)).Text;
            var text1 = (await reader.GetTextAsync(1)).Text;
            Check("target-text-and-form-instance-removed", !text0.Contains(TextSecret, StringComparison.Ordinal)
                && !text0.Contains(SharedSecret, StringComparison.Ordinal), "The selected whole text and page0 Form invocation disappear after reopening.");
            Check("retained-text-and-shared-page-preserved", text0.Contains("KEEP_LEFT", StringComparison.Ordinal)
                && text0.Contains("KEEP_RIGHT", StringComparison.Ordinal) && text1.Contains(SharedSecret, StringComparison.Ordinal)
                && text1.Contains("KEEP_SECOND_PAGE", StringComparison.Ordinal), "The same Form remains on the unselected second page.");
            await session.SaveAsync(Path.Combine(directory, "atomic-saved.pdf"));
            Check("pipeline-atomic-file-save", !session.IsDirty && File.ReadAllBytes(Path.Combine(directory, "atomic-saved.pdf")).SequenceEqual(output),
                "File save persists exactly the validated sanitized snapshot.");
        }
        foreach (var item in new[]
        {
            ("partial-text", new PdfEditRectangle(150, 340, 15, 15)),
            ("partial-image", new PdfEditRectangle(40, 210, 20, 40)),
            ("partial-form", new PdfEditRectangle(130, 130, 20, 30)),
            ("partial-path", new PdfEditRectangle(40, 80, 20, 30))
        })
        {
            await using var session = await PdfEditSession.OpenAsync(source);
            var refused = false;
            try { await session.ApplyRedactionsAsync([new PdfRedactionRegion(0, item.Item2)], PdfHiddenInformationPolicy.RemoveAllNonPageData); }
            catch (NotSupportedException) { refused = true; }
            var unchanged = (await session.ExportAsync()).SequenceEqual(source);
            Check(item.Item1 + "-refused-before-commit", refused && unchanged && session.Revision == 0 && !session.IsDirty,
                "Partial geometry is unsupported and cannot silently remove a whole object or commit cleanup side effects.");
        }
        await using (var session = await PdfEditSession.OpenAsync(source))
        {
            var refused = false;
            try { await session.ApplyRedactionsAsync([Regions[0], new PdfRedactionRegion(0, new PdfEditRectangle(40, 210, 20, 40))], PdfHiddenInformationPolicy.RemoveAllNonPageData); }
            catch (NotSupportedException) { refused = true; }
            Check("later-region-failure-atomic", refused && (await session.ExportAsync()).SequenceEqual(source) && session.Revision == 0,
                "A valid earlier target is not committed when a later region is unsupported.");
        }
        await using (var session = await PdfEditSession.OpenAsync(source))
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var refused = false;
            try { await session.ApplyRedactionsAsync(Regions, PdfHiddenInformationPolicy.RemoveAllNonPageData, cancellation.Token); }
            catch (OperationCanceledException) { refused = true; }
            Check("pre-cancelled-pipeline-atomic", refused && (await session.ExportAsync()).SequenceEqual(source) && session.Revision == 0,
                "Cancellation cannot publish a partial content-removal/cleanup snapshot.");
        }
        await using (var session = await PdfEditSession.OpenAsync(source))
        {
            var refused = false;
            try { await session.SanitizeAsync((PdfHiddenInformationPolicy)999); }
            catch (ArgumentOutOfRangeException) { refused = true; }
            catch (ArgumentException) { refused = true; }
            Check("unknown-policy-refused", refused && (await session.ExportAsync()).SequenceEqual(source) && session.Revision == 0,
                "Unknown enum values do not silently select a destructive cleanup policy.");
        }
        return new
        {
            Status = "bounded-production-redaction-pipeline", Policy = "RemoveAllNonPageData", Checks = checks,
            FunctionalAssemblySHA256 = Hash(File.ReadAllBytes(typeof(PdfEditSession).Assembly.Location)),
            Regions = Regions.Select(r => new { r.PageIndex, r.Bounds.Left, r.Bounds.Bottom, r.Bounds.Width, r.Bounds.Height }).ToArray(),
            Outputs = Directory.EnumerateFiles(directory, "*.pdf").Select(p => new { File = Path.GetFileName(p), SHA256 = Hash(File.ReadAllBytes(p)) }).ToArray(),
            Limitations = new[]
            {
                "One authored unsigned/unencrypted two-page corpus; supported whole-object rectangles only.",
                "The explicit strict policy removes annotations, forms, outline/attachments and custom non-page data, including visible annotation appearances.",
                "The shared Form secret intentionally remains on unselected page1; global token absence is the wrong page-scoped oracle.",
                "No partial characters, image pixels, vector splitting, nested Form editing, arbitrary-PDF compatibility or Linux/performance parity claim."
            }
        };
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static byte[] Fixture()
    {
        static string Stream(string content, string extras = "") => $"<< /Length {Encoding.ASCII.GetByteCount(content)} {extras} >>\nstream\n{content}\nendstream";
        static string Image(string marker) => Stream(marker.PadRight(24, '_'), "/Type /XObject /Subtype /Image /Width 8 /Height 1 /ColorSpace /DeviceRGB /BitsPerComponent 8");
        var page0 = "BT /F1 14 Tf 30 365 Td (KEEP_LEFT) Tj ET\n"
            + $"BT /F1 14 Tf 150 340 Td ({TextSecret}) Tj ET\n"
            + "BT /F1 14 Tf 30 295 Td (KEEP_RIGHT) Tj ET\n"
            + "q 60 0 0 40 40 210 cm /TargetImage Do Q\nq 1 0 0 1 130 130 cm /Shared Do Q\n"
            + "0.97 0.04 0.02 rg 40 80 80 30 re f\n0.02 0.08 0.95 rg 300 80 50 30 re f\n";
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R /Metadata 7 0 R /AcroForm 12 0 R /Outlines 15 0 R /Names << /EmbeddedFiles << /Names [(PIPE_HIDDEN_ATTACHMENT_NAME) 18 0 R] >> >> /StructTreeRoot 20 0 R /OpenAction 23 0 R /CustomExtension << /Secret (PIPE_HIDDEN_CUSTOM_CATALOG) >> >>",
            "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 400 400] /Resources << /Font << /F1 5 0 R /UnusedFont 30 0 R >> /XObject << /TargetImage 9 0 R /Shared 10 0 R /UnusedImage 24 0 R /UnusedForm 25 0 R >> /Pattern << /UnusedPattern 29 0 R >> >> /Contents 6 0 R /Annots [13 0 R 14 0 R] /Thumb 11 0 R /Metadata 7 0 R /AA << /O 23 0 R >> /PieceInfo 28 0 R /CustomPageSecret (PIPE_HIDDEN_CUSTOM_PAGE) >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 400 400] /Resources << /Font << /F1 5 0 R >> /XObject << /Shared 10 0 R >> >> /Contents 8 0 R /Thumb 11 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            Stream(page0),
            Stream("<?xml version=\"1.0\"?><x:xmpmeta xmlns:x=\"adobe:ns:meta/\">PIPE_HIDDEN_XMP</x:xmpmeta>", "/Type /Metadata /Subtype /XML"),
            Stream($"BT /F1 14 Tf 30 365 Td (KEEP_SECOND_PAGE) Tj ET\nq 1 0 0 1 30 250 cm /Shared Do Q\n"),
            Image("PIPE_TARGET_IMAGE_64E1"),
            Stream($"BT /F1 14 Tf 0 5 Td ({SharedSecret}) Tj ET", "/Type /XObject /Subtype /Form /BBox [0 0 230 30] /Resources << /Font << /F1 5 0 R >> /XObject << /UnusedNested 24 0 R >> >> /CustomFormSecret (PIPE_HIDDEN_CUSTOM_FORM)"),
            Image("PIPE_HIDDEN_THUMB"),
            "<< /Fields [13 0 R] /XFA 26 0 R /DR << /Font << /F1 5 0 R >> >> /DA (/F1 12 Tf 0 g) /NeedAppearances false >>",
            "<< /Type /Annot /Subtype /Widget /FT /Tx /T (PIPE_HIDDEN_FIELD_NAME) /V (PIPE_HIDDEN_FIELD_VALUE) /DV (PIPE_HIDDEN_FIELD_DEFAULT) /Rect [-100 -100 -90 -90] /P 3 0 R /AP << /N 27 0 R >> >>",
            "<< /Type /Annot /Subtype /Text /Contents (PIPE_HIDDEN_ANNOT_CONTENTS) /Rect [-100 -100 -90 -90] /F 2 /P 3 0 R /AP << /N 27 0 R >> /AA << /E 23 0 R >> >>",
            "<< /Type /Outlines /First 16 0 R /Last 16 0 R /Count 1 >>",
            "<< /Title (PIPE_HIDDEN_OUTLINE_TITLE) /Parent 15 0 R /Dest [3 0 R /Fit] >>",
            Stream("PIPE_HIDDEN_ATTACHMENT_BYTES", "/Type /EmbeddedFile"),
            "<< /Type /Filespec /F (hidden.txt) /EF << /F 17 0 R >> >>",
            "<< /Title (PIPE_HIDDEN_INFO_TITLE) /Subject (PIPE_HIDDEN_OLD_REVISION) /CustomInfo (PIPE_HIDDEN_INFO_CUSTOM) >>",
            "<< /Type /StructTreeRoot /K [21 0 R] >>",
            "<< /Type /StructElem /S /Span /P 20 0 R /Pg 3 0 R /ActualText (PIPE_HIDDEN_ACTUAL_TEXT) /Alt (PIPE_HIDDEN_ALT) >>",
            Stream("PIPE_HIDDEN_UNREACHABLE_STREAM"),
            "<< /S /JavaScript /JS (PIPE_HIDDEN_JAVASCRIPT) >>",
            Image("PIPE_HIDDEN_UNUSED_IMAGE"),
            Stream("BT /F1 12 Tf 0 0 Td (PIPE_HIDDEN_UNUSED_FORM) Tj ET", "/Type /XObject /Subtype /Form /BBox [0 0 200 20] /Resources << /Font << /F1 5 0 R >> >>"),
            Stream("<xfa>PIPE_HIDDEN_XFA</xfa>"),
            Stream("BT /F1 12 Tf 0 0 Td (PIPE_HIDDEN_ANNOT_AP) Tj ET", "/Type /XObject /Subtype /Form /BBox [0 0 200 20] /Resources << /Font << /F1 5 0 R >> >>"),
            "<< /Application << /Private (PIPE_HIDDEN_PIECE_INFO) >> >>",
            Stream("% PIPE_HIDDEN_UNUSED_PATTERN\n0 0 1 rg 0 0 2 2 re f", "/Type /Pattern /PatternType 1 /PaintType 1 /TilingType 1 /BBox [0 0 2 2] /XStep 2 /YStep 2 /Resources << >>"),
            "<< /Type /Font /Subtype /Type1 /BaseFont /Times-Roman /CustomSecret (PIPE_HIDDEN_UNUSED_FONT) >>"
        ];
        using var output = new MemoryStream();
        void Append(string text) => output.Write(Encoding.ASCII.GetBytes(text));
        Append("%PDF-1.7\n");
        var offsets = new List<long> { 0 };
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(output.Position);
            Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = output.Position;
        Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) Append(offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
        Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R /Info 19 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        // The current Info no longer references the prior subject. A full rewrite
        // must remove both old revision bytes and the entirely unreachable object22.
        var changed = output.Position;
        Append("19 0 obj\n<< /Title (PIPE_HIDDEN_INFO_TITLE) /Subject (PIPE_HIDDEN_CURRENT_REVISION) /CustomInfo (PIPE_HIDDEN_INFO_CUSTOM) >>\nendobj\n");
        var latest = output.Position;
        Append($"xref\n19 1\n{changed.ToString("D10", CultureInfo.InvariantCulture)} 00000 n \ntrailer\n<< /Size {objects.Length + 1} /Root 1 0 R /Info 19 0 R /Prev {xref} >>\nstartxref\n{latest}\n%%EOF\n");
        return output.ToArray();
    }
}
