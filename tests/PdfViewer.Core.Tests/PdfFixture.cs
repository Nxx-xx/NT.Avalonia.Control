using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PdfViewer.Tests;

/// <summary>Small, self-contained PDF files with real xref tables; no fixture generator dependency.</summary>
internal static class PdfFixture
{
    internal static byte[] ReadingDocument()
    {
        var first = Stream("0 0 1 rg 20 20 40 30 re f\n0 g BT /F1 18 Tf 40 320 Td (Hello PDF reader) Tj 0 -30 Td (hello HELLO helloWorld) Tj ET\n");
        var second = Stream("BT /F1 16 Tf 30 330 Td (Second page hello) Tj ET\n");
        return Write(
        [
            "<< /Type /Catalog /Pages 2 0 R /Outlines 8 0 R >>",
            "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 400] /Resources << /Font << /F1 5 0 R >> >> /Contents 6 0 R >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 250 400] /Rotate 90 /Resources << /Font << /F1 5 0 R >> >> /Contents 7 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            first,
            second,
            "<< /Type /Outlines /First 9 0 R /Last 9 0 R /Count 2 >>",
            "<< /Title (Chapter one) /Parent 8 0 R /Dest [3 0 R /Fit] /First 10 0 R /Last 10 0 R /Count 1 >>",
            "<< /Title (Second page) /Parent 9 0 R /Dest [4 0 R /Fit] >>",
        ]);
    }

    /// <summary>
    /// Four pages with /PageLabels (roman, then decimal with a prefix) and one page of links:
    /// an /XYZ internal destination, a URI action, a Launch action, a JavaScript action and a
    /// named destination resolved through the catalog name tree.
    /// </summary>
    internal static byte[] LinksAndLabelsDocument()
    {
        var content = Stream("BT /F1 14 Tf 20 360 Td (Link page) Tj ET\n");
        return Write(
        [
            "<< /Type /Catalog /Pages 2 0 R /Names << /Dests << /Names [(targetName) 11 0 R] >> >>" +
            " /PageLabels << /Nums [ 0 << /S /r >> 2 << /S /D /St 5 /P (A-) >> ] >> >>",
            "<< /Type /Pages /Kids [3 0 R 4 0 R 5 0 R 6 0 R] /Count 4 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 400] /Resources << /Font << /F1 13 0 R >> >> /Contents 14 0 R" +
            " /Annots [7 0 R 8 0 R 9 0 R 10 0 R 12 0 R] >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 250 400] /Resources << /Font << /F1 13 0 R >> >> /Contents 15 0 R >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 250 400] /Resources << /Font << /F1 13 0 R >> >> /Contents 16 0 R >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 250 400] /Resources << /Font << /F1 13 0 R >> >> /Contents 17 0 R >>",
            // Internal /XYZ destination to page 1 at (50, 300) with zoom 2.
            "<< /Type /Annot /Subtype /Link /Rect [10 350 110 380] /Border [0 0 0] /Dest [3 0 R /XYZ 50 300 2] >>",
            "<< /Type /Annot /Subtype /Link /Rect [10 300 110 330] /Border [0 0 0] /A << /S /URI /URI (https://example.com/doc) >> >>",
            "<< /Type /Annot /Subtype /Link /Rect [10 250 110 280] /Border [0 0 0] /A << /S /Launch /F (report.pdf) >> >>",
            "<< /Type /Annot /Subtype /Link /Rect [10 200 110 230] /Border [0 0 0] /A << /S /JavaScript /JS (app.alert(1)) >> >>",
            "<< /D [4 0 R /Fit] >>",
            "<< /Type /Annot /Subtype /Link /Rect [10 150 110 180] /Border [0 0 0] /Dest (targetName) >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            content,
            Stream("BT /F1 14 Tf 20 360 Td (Page two) Tj ET\n"),
            Stream("BT /F1 14 Tf 20 360 Td (Page three) Tj ET\n"),
            Stream("BT /F1 14 Tf 20 360 Td (Page four) Tj ET\n"),
        ]);
    }

    /// <summary>
    /// Three pages with one link per /Fit-family destination on page 1: /FitR to page 2, /FitH 300
    /// back to page 1, /FitV 50 to page 2, and /FitB, /FitBH 200, /FitBV 25 to page 3. Page 3 is
    /// landscape so fit-height navigation keeps a horizontal scroll range. Pages are unrotated, so
    /// a raw /XYZ-style y maps to (pageHeight - y) in display space.
    /// </summary>
    internal static byte[] FitDestinationsDocument()
    {
        var content = Stream("BT /F1 14 Tf 20 360 Td (Fit page) Tj ET\n");
        return Write(
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R 4 0 R 5 0 R] /Count 3 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 400] /Resources << /Font << /F1 12 0 R >> >> /Contents 13 0 R" +
            " /Annots [6 0 R 7 0 R 8 0 R 9 0 R 10 0 R 11 0 R] >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 250 400] /Resources << /Font << /F1 12 0 R >> >> /Contents 13 0 R >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 800 300] /Resources << /Font << /F1 12 0 R >> >> /Contents 13 0 R >>",
            // /FitR left top right bottom on page 2: display rect x=30, y=50, 100x100 points.
            "<< /Type /Annot /Subtype /Link /Rect [10 350 110 380] /Border [0 0 0] /Dest [4 0 R /FitR 30 350 130 250] >>",
            // /FitH top 300 on page 1: display Y = 400 - 300 = 100.
            "<< /Type /Annot /Subtype /Link /Rect [10 300 110 330] /Border [0 0 0] /Dest [3 0 R /FitH 300] >>",
            // /FitV left 50 on page 2: display X = 50.
            "<< /Type /Annot /Subtype /Link /Rect [10 250 110 280] /Border [0 0 0] /Dest [4 0 R /FitV 50] >>",
            "<< /Type /Annot /Subtype /Link /Rect [10 200 110 230] /Border [0 0 0] /Dest [5 0 R /FitB] >>",
            // /FitBH top 200 on page 3: display Y = 300 - 200 = 100.
            "<< /Type /Annot /Subtype /Link /Rect [10 150 110 180] /Border [0 0 0] /Dest [5 0 R /FitBH 200] >>",
            // /FitBV left 25 on page 3: display X = 25.
            "<< /Type /Annot /Subtype /Link /Rect [10 100 110 130] /Border [0 0 0] /Dest [5 0 R /FitBV 25] >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            content,
        ]);
    }

    /// <summary>
    /// Two pages of link-only /XYZ destinations with one missing coordinate each: x-only, y-only
    /// and zoom-only on the unrotated page 1, plus x-only into the /Rotate 90 page 2 (250x400 user
    /// space, 400x250 displayed) where the user-space x axis runs along the displayed Y axis.
    /// </summary>
    internal static byte[] PartialXyzDocument() => Write(
    [
        "<< /Type /Catalog /Pages 2 0 R >>",
        "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 400] /Contents 5 0 R /Annots [6 0 R 7 0 R 8 0 R 9 0 R] >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 250 400] /Rotate 90 /Contents 10 0 R >>",
        Stream("20 100 60 40 re f\n"),
        "<< /Type /Annot /Subtype /Link /Rect [10 350 110 380] /Border [0 0 0] /Dest [3 0 R /XYZ 50 null null] >>",
        "<< /Type /Annot /Subtype /Link /Rect [10 300 110 330] /Border [0 0 0] /Dest [3 0 R /XYZ null 300 null] >>",
        "<< /Type /Annot /Subtype /Link /Rect [10 250 110 280] /Border [0 0 0] /Dest [3 0 R /XYZ null null 2] >>",
        "<< /Type /Annot /Subtype /Link /Rect [10 200 110 230] /Border [0 0 0] /Dest [4 0 R /XYZ 80 null null] >>",
        Stream("20 100 60 40 re f\n"),
    ]);

    /// <summary>
    /// Two pages whose content boxes are known exactly: page 1 draws two filled rectangles whose
    /// union is page-space (20,100)-(150,250), i.e. displayed Rect(20, 150, 130, 150) on the
    /// 300x400 page; page 2 (500x300) has an empty content stream and no content box.
    /// </summary>
    internal static byte[] ContentBoxDocument() => Write(
    [
        "<< /Type /Catalog /Pages 2 0 R >>",
        "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 400] /Contents 5 0 R >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 500 300] /Contents 6 0 R >>",
        Stream("20 100 60 40 re f\n100 200 50 50 re f\n"),
        Stream(""),
    ]);

    internal static byte[] SinglePage(string text = "Replacement document", int width = 200, int height = 300) => Write(
    [
        "<< /Type /Catalog /Pages 2 0 R >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {width} {height}] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        Stream($"BT /F1 14 Tf 20 {height - 40} Td ({Escape(text)}) Tj ET\n"),
    ]);

    /// <summary>One small page plus a large unreferenced stream object, for random-access read assertions.</summary>
    internal static byte[] PaddedDocument(int paddingBytes)
    {
        var padding = new string('x', paddingBytes);
        return Write(
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            Stream("BT /F1 14 Tf 20 260 Td (Padded) Tj ET\n"),
            Stream(padding),
        ]);
    }

    internal static byte[] ManyPages(int count, int width = 300, int height = 400)
    {
        var references = string.Join(" ", Enumerable.Range(0, count).Select(i => $"{4 + i * 2} 0 R"));
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Kids [{references}] /Count {count} >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        };
        for (var i = 0; i < count; i++)
        {
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {width} {height}] /Resources << /Font << /F1 3 0 R >> >> /Contents {5 + i * 2} 0 R >>");
            objects.Add(Stream($"BT /F1 18 Tf 10 {Math.Max(10, height - 40)} Td (Page {i + 1}) Tj ET\n"));
        }
        return Write(objects.ToArray());
    }

    internal static byte[] UnicodeAndCropDocument() => Write(
    [
        "<< /Type /Catalog /Pages 2 0 R >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 600 800] /CropBox [50 100 550 700] /Resources << /Font << /F1 4 0 R >> >> /Contents 8 0 R >>",
        "<< /Type /Font /Subtype /Type0 /BaseFont /FixtureUnicode /Encoding /Identity-H /DescendantFonts [5 0 R] /ToUnicode 6 0 R >>",
        "<< /Type /Font /Subtype /CIDFontType2 /BaseFont /FixtureUnicode /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> /FontDescriptor 7 0 R /DW 1000 /CIDToGIDMap /Identity >>",
        Stream("/CIDInit /ProcSet findresource begin\n12 dict begin\nbegincmap\n/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def\n/CMapName /FixtureUnicode def\n/CMapType 2 def\n1 begincodespacerange\n<0000> <FFFF>\nendcodespacerange\n6 beginbfchar\n<0001> <0041>\n<0002> <4E2D>\n<0003> <6587>\n<0004> <D83DDE80>\n<0005> <0042>\n<0006> <00E9>\nendbfchar\nendcmap\nCMapName currentdict /CMap defineresource pop\nend\nend\n"),
        "<< /Type /FontDescriptor /FontName /FixtureUnicode /Flags 4 /FontBBox [-200 -200 1200 1200] /ItalicAngle 0 /Ascent 1000 /Descent -200 /CapHeight 700 /StemV 80 >>",
        Stream("BT /F1 20 Tf 70 650 Td <000100020003000400050006> Tj ET\n"),
    ]);

    internal static byte[] PasswordProtected(string password = "reader")
    {
        // PDF standard security, revision 2 (40-bit RC4). Encryption applies to
        // the contents stream; the encryption dictionary's O/U are raw bytes.
        byte[] padding = [0x28, 0xbf, 0x4e, 0x5e, 0x4e, 0x75, 0x8a, 0x41, 0x64, 0x00, 0x4e, 0x56, 0xff, 0xfa, 0x01, 0x08, 0x2e, 0x2e, 0x00, 0xb6, 0xd0, 0x68, 0x3e, 0x80, 0x2f, 0x0c, 0xa9, 0xfe, 0x64, 0x53, 0x69, 0x7a];
        byte[] Pad(string value) => Encoding.ASCII.GetBytes(value).Concat(padding).Take(32).ToArray();
        var ownerKey = MD5.HashData(Pad("fixture-owner"))[..5];
        var owner = Rc4(ownerKey, Pad(password));
        var id = MD5.HashData(Encoding.ASCII.GetBytes("NT.AvaloniaControl.PdfViewer.PasswordFixture"));
        byte[] permissions = [0xfc, 0xff, 0xff, 0xff]; // P = -4, all permissions.
        var key = MD5.HashData(Pad(password).Concat(owner).Concat(permissions).Concat(id).ToArray())[..5];
        var user = Rc4(key, padding);
        byte[] objectNumber = [5, 0, 0, 0, 0];
        var objectKey = MD5.HashData(key.Concat(objectNumber).ToArray())[..10];
        var encrypted = Rc4(objectKey, Encoding.ASCII.GetBytes("BT /F1 14 Tf 20 250 Td (Protected text) Tj ET\n"));
        return Write(
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            Stream(encrypted),
            $"<< /Filter /Standard /V 1 /R 2 /Length 40 /O <{Convert.ToHexString(owner)}> /U <{Convert.ToHexString(user)}> /P -4 >>",
        ], $" /Encrypt 6 0 R /ID [<{Convert.ToHexString(id)}> <{Convert.ToHexString(id)}>]");
    }

    private static byte[] Rc4(byte[] key, byte[] data)
    {
        var state = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
        var j = 0;
        for (var i = 0; i < state.Length; i++)
        {
            j = (j + state[i] + key[i % key.Length]) & 255;
            (state[i], state[j]) = (state[j], state[i]);
        }
        var result = new byte[data.Length];
        var x = 0;
        j = 0;
        for (var i = 0; i < data.Length; i++)
        {
            x = (x + 1) & 255;
            j = (j + state[x]) & 255;
            (state[x], state[j]) = (state[j], state[x]);
            result[i] = (byte)(data[i] ^ state[(state[x] + state[j]) & 255]);
        }
        return result;
    }

    private static string Stream(string contents) => Stream(Encoding.ASCII.GetBytes(contents));
    private static string Stream(byte[] contents) => $"<< /Length {contents.Length} >>\nstream\n{Encoding.Latin1.GetString(contents)}\nendstream";

    private static byte[] Write(string[] objects, string extraTrailer = "")
    {
        using var output = new MemoryStream();
        void Append(string value) => output.Write(Encoding.Latin1.GetBytes(value));
        Append("%PDF-1.4\n%\u00e2\u00e3\u00cf\u00d3\n");
        var offsets = new List<long> { 0 };
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(output.Position);
            Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = output.Position;
        Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) Append(offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
        Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R{extraTrailer} >>\nstartxref\n{xref}\n%%EOF\n");
        return output.ToArray();
    }

    private static string Escape(string text) => text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
}
