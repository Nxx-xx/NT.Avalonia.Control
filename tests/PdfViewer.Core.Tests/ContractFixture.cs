using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PdfViewer.Tests;

/// <summary>Self-authored permission and information fixtures, independent of the existing reader corpus.</summary>
internal static class ContractFixture
{
    internal static byte[] Create(bool restricted = false, bool signature = false, bool allowAllPermissions = false)
    {
        var content = Encoding.ASCII.GetBytes("BT /F1 14 Tf 20 250 Td (Contract needle text) Tj ET\n");
        var trailer = "";
        var encryption = "";
        if (restricted)
        {
            byte[] padding = [0x28, 0xbf, 0x4e, 0x5e, 0x4e, 0x75, 0x8a, 0x41, 0x64, 0x00, 0x4e, 0x56, 0xff, 0xfa, 0x01, 0x08,
                0x2e, 0x2e, 0x00, 0xb6, 0xd0, 0x68, 0x3e, 0x80, 0x2f, 0x0c, 0xa9, 0xfe, 0x64, 0x53, 0x69, 0x7a];
            byte[] Pad(string value) => Encoding.ASCII.GetBytes(value).Concat(padding).Take(32).ToArray();
            var owner = Rc4(MD5.HashData(Pad("contract-owner"))[..5], Pad("contract-user"));
            var id = MD5.HashData(Encoding.ASCII.GetBytes("Contract.Permission.Fixture"));
            byte[] permissionBytes = [(byte)(allowAllPermissions ? 0xfc : 0xc0), 0xff, 0xff, 0xff];
            var key = MD5.HashData(Pad("contract-user").Concat(owner).Concat(permissionBytes).Concat(id).ToArray())[..5];
            var user = Rc4(key, padding);
            content = Rc4(MD5.HashData(key.Concat(new byte[] { 5, 0, 0, 0, 0 }).ToArray())[..10], content);
            encryption = $"<< /Filter /Standard /V 1 /R 2 /Length 40 /O <{Convert.ToHexString(owner)}> /U <{Convert.ToHexString(user)}> /P {(allowAllPermissions ? -4 : -64)} >>";
            trailer = $" /Encrypt 6 0 R /ID [<{Convert.ToHexString(id)}> <{Convert.ToHexString(id)}>]";
        }
        var title = Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes("契约标题"));
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R" + (signature ? " /AcroForm << /Fields [8 0 R] >>" : "") + " >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 300] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {content.Length} >>\nstream\n{Encoding.Latin1.GetString(content)}\nendstream",
            restricted ? encryption : $"<< /Title <FEFF{title}> /Author (Contract author) /Subject (Metadata contract) /Keywords (reader,contract) /Creator (Self-authored fixture) /Producer (NT tests) /CreationDate (D:20261015083000+08'00') /ModDate (D:20261015) >>"
        };
        if (!restricted) trailer += " /Info 6 0 R";
        if (signature)
        {
            objects.Add("<< /Type /Sig /Filter /Adobe.PPKLite /SubFilter /adbe.pkcs7.detached /ByteRange [0 1 2 3] /Contents <00> >>");
            objects.Add("<< /FT /Sig /T (Fixture signature) /V 7 0 R >>");
        }
        using var output = new MemoryStream();
        void Append(string text) => output.Write(Encoding.Latin1.GetBytes(text));
        Append("%PDF-1.7\n%\u00e2\u00e3\u00cf\u00d3\n");
        var offsets = new List<long> { 0 };
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(output.Position);
            Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = output.Position;
        Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) Append(offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
        Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R{trailer} >>\nstartxref\n{xref}\n%%EOF\n");
        return output.ToArray();
    }

    private static byte[] Rc4(byte[] key, byte[] bytes)
    {
        var state = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
        var j = 0;
        for (var i = 0; i < 256; i++)
        {
            j = (j + state[i] + key[i % key.Length]) & 255;
            (state[i], state[j]) = (state[j], state[i]);
        }
        var x = 0;
        j = 0;
        return bytes.Select(value =>
        {
            x = (x + 1) & 255;
            j = (j + state[x]) & 255;
            (state[x], state[j]) = (state[j], state[x]);
            return (byte)(value ^ state[(state[x] + state[j]) & 255]);
        }).ToArray();
    }
}
