using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace NT.AvaloniaControl.PdfViewer.Editing;

/// <summary>
/// Assigns a separate CID to each requested Unicode scalar, including scalar aliases sharing one
/// glyph. Explicit UTF-16BE ToUnicode mappings avoid lossy surrogate or glyph-to-Unicode inference.
/// </summary>
internal sealed record PdfFreeTextCidFont(string ToUnicode, byte[] CidToGid, uint[] Charcodes)
{
    internal static PdfFreeTextCidFont Create(string text, byte[] bytes, CancellationToken token)
    {
        var font = new PdfTrueTypeFont(bytes);
        var scalars = text.EnumerateRunes().Select(rune => rune.Value).Distinct().Order().ToArray();
        var cids = new Dictionary<int, uint>(scalars.Length);
        var glyphMap = new byte[(scalars.Length + 1) * 2];
        var cmap = new StringBuilder("/CIDInit /ProcSet findresource begin\n12 dict begin\nbegincmap\n/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def\n/CMapName /NTFreeTextUnicode def\n/CMapType 2 def\n1 begincodespacerange\n<0000> <FFFF>\nendcodespacerange\n");
        for (var start = 0; start < scalars.Length; start += 100)
        {
            var count = Math.Min(100, scalars.Length - start);
            cmap.Append(count.ToString(CultureInfo.InvariantCulture)).Append(" beginbfchar\n");
            for (var index = start; index < start + count; index++)
            {
                token.ThrowIfCancellationRequested();
                var cid = checked((ushort)(index + 1));
                var glyph = font.GetGlyphIndex(scalars[index]);
                if (glyph == 0) throw new ArgumentException($"The supplied font has no glyph for U+{scalars[index]:X4}.", nameof(bytes));
                BinaryPrimitives.WriteUInt16BigEndian(glyphMap.AsSpan(cid * 2, 2), checked((ushort)glyph));
                cids.Add(scalars[index], cid);
                cmap.Append('<').Append(cid.ToString("X4", CultureInfo.InvariantCulture)).Append("> <")
                    .Append(Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(char.ConvertFromUtf32(scalars[index])))).Append(">\n");
            }
            cmap.Append("endbfchar\n");
        }
        cmap.Append("endcmap\nCMapName currentdict /CMap defineresource pop\nend\nend\n");
        var charcodes = text.EnumerateRunes().Select(rune => cids[rune.Value]).ToArray();
        token.ThrowIfCancellationRequested();
        return new(cmap.ToString(), glyphMap, charcodes);
    }
}
