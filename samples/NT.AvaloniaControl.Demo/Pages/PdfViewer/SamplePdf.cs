using System.Globalization;
using System.Text;

namespace PdfViewerSamples;

/// <summary>A deterministic, searchable PDF fixture, generated without a PDF authoring dependency.</summary>
internal static class SamplePdf
{
    internal static byte[] Create()
    {
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R /Outlines 10 0 R /PageMode /UseOutlines >>",
            "<< /Type /Pages /Kids [3 0 R 5 0 R 7 0 R] /Count 3 >>",
            Page(595, 842, 4),
            Stream(PageOne),
            Page(842, 595, 6),
            Stream(PageTwo),
            Page(420, 595, 8),
            Stream(PageThree),
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            "<< /Type /Outlines /First 11 0 R /Last 13 0 R /Count 3 >>",
            "<< /Title (1. Welcome) /Parent 10 0 R /Next 12 0 R /Dest [3 0 R /Fit] >>",
            "<< /Title (2. Landscape and search) /Parent 10 0 R /Prev 11 0 R /Next 13 0 R /Dest [5 0 R /Fit] >>",
            "<< /Title (3. Reading checklist) /Parent 10 0 R /Prev 12 0 R /Dest [7 0 R /Fit] >>",
        };
        using var output = new MemoryStream();
        void Write(string value) => output.Write(Encoding.ASCII.GetBytes(value));
        Write("%PDF-1.7\n% deterministic local demo\n");
        var offsets = new List<long> { 0 };
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(output.Position);
            Write($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = output.Position;
        Write($"xref\n0 {offsets.Count}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
            Write(offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
        Write($"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return output.ToArray();
    }

    private static string Page(int width, int height, int content) =>
        $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {width} {height}] /Resources << /Font << /F1 9 0 R >> >> /Contents {content} 0 R >>";
    private static string Stream(string content) => $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream";

    private const string PageOne = """
        q
        0.08 0.20 0.34 rg 0 674 595 168 re f
        0.20 0.73 0.83 rg 40 650 70 5 re f
        BT /F1 13 Tf 1 1 1 rg 48 794 Td (NT / AVALONIA CONTROLS) Tj ET
        BT /F1 34 Tf 1 1 1 rg 48 734 Td (A better reading surface.) Tj ET
        BT /F1 17 Tf 0.12 0.19 0.28 rg 48 608 Td (PDF Viewer / Desktop reading edition) Tj ET
        BT /F1 12 Tf 0.23 0.29 0.37 rg 48 568 Td
        (Welcome to the Avalonia PDF Viewer sample.) Tj 0 -26 Td
        (This document has three pages, searchable text, and bookmarks.) Tj 0 -26 Td
        (Try selecting this sentence and copying it to your clipboard.) Tj 0 -26 Td
        (Search for Avalonia to move between matches on different pages.) Tj 0 -26 Td
        (Use thumbnails or the outline to navigate the document.) Tj ET
        0.91 0.95 0.98 rg 48 288 499 126 re f
        BT /F1 16 Tf 0.08 0.20 0.34 rg 66 380 Td (Built for everyday documents) Tj ET
        BT /F1 12 Tf 0.23 0.29 0.37 rg 66 349 Td
        (Continuous scrolling. Crisp zoom. Native text selection.) Tj 0 -23 Td
        (A separate themes package, ready for your application.) Tj ET
        BT /F1 10 Tf 0.40 0.47 0.55 rg 48 38 Td (LOCAL SAMPLE / A4 PORTRAIT) Tj 440 0 Td (01) Tj ET
        Q
        """;

    private const string PageTwo = """
        q
        0.08 0.20 0.34 rg 0 466 842 129 re f
        BT /F1 12 Tf 1 1 1 rg 42 555 Td (02 / EXPLORE THE DOCUMENT) Tj ET
        BT /F1 30 Tf 1 1 1 rg 42 502 Td (Landscape, search and navigation.) Tj ET
        BT /F1 16 Tf 0.12 0.19 0.28 rg 42 420 Td (This page uses a different size and orientation.) Tj ET
        BT /F1 13 Tf 0.23 0.29 0.37 rg 42 382 Td
        (Avalonia can present portrait and landscape pages in one continuous document.) Tj 0 -28 Td
        (Change the zoom mode: fit width for reading, or fit page for an overview.) Tj 0 -28 Td
        (Rotate the document by 90 degrees and verify that text selection follows.) Tj 0 -28 Td
        (Search terms: Avalonia, PDF, reading, document. Try case-sensitive search.) Tj ET
        0.13 0.53 0.67 rg 42 142 220 104 re f
        0.27 0.66 0.73 rg 282 142 220 104 re f
        0.55 0.77 0.81 rg 522 142 278 104 re f
        BT /F1 18 Tf 1 1 1 rg 62 196 Td (01 / Navigate) Tj ET
        BT /F1 18 Tf 1 1 1 rg 302 196 Td (02 / Find) Tj ET
        BT /F1 18 Tf 0.08 0.20 0.34 rg 542 196 Td (03 / Read) Tj ET
        BT /F1 10 Tf 0.40 0.47 0.55 rg 42 30 Td (LOCAL SAMPLE / A4 LANDSCAPE) Tj 730 0 Td (02) Tj ET
        Q
        """;

    private const string PageThree = """
        q
        0.08 0.20 0.34 rg 0 475 420 120 re f
        BT /F1 12 Tf 1 1 1 rg 34 557 Td (03 / READING CHECKLIST) Tj ET
        BT /F1 28 Tf 1 1 1 rg 34 510 Td (Make it your own.) Tj ET
        BT /F1 13 Tf 0.12 0.19 0.28 rg 34 433 Td
        (Avalonia PDF Viewer) Tj 0 -35 Td
        ([1] Open your own PDF file.) Tj 0 -29 Td
        ([2] Jump to a page using its thumbnail.) Tj 0 -29 Td
        ([3] Follow a bookmark in the outline.) Tj 0 -29 Td
        ([4] Search and cycle through results.) Tj 0 -29 Td
        ([5] Select and copy a passage of text.) Tj 0 -29 Td
        ([6] Switch between light and dark themes.) Tj 0 -42 Td
        (PDF. Reading. Avalonia. Ready to explore.) Tj ET
        BT /F1 10 Tf 0.40 0.47 0.55 rg 34 30 Td (LOCAL SAMPLE / COMPACT PORTRAIT) Tj 320 0 Td (03) Tj ET
        Q
        """;
}
