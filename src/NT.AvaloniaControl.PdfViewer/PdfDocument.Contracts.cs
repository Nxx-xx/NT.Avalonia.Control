using System.Runtime.InteropServices;
using System.Text;
using NT.AvaloniaControl.PdfViewer.Interop;

namespace NT.AvaloniaControl.PdfViewer;

public sealed partial class PdfDocument
{
    public PdfDocumentMetadata Metadata { get; private set; } = null!;
    public PdfDocumentPermissions Permissions { get; private set; } = null!;
    /// <summary>Implementation and file permissions. Viewer host policy is applied separately.</summary>
    public PdfDocumentCapabilities Capabilities { get; private set; } = null!;

    // Called only inside the native scheduler, while loading owns the handle.
    private void InitializeContracts(IntPtr handle)
    {
        var revision = PdfiumNative.FPDF_GetSecurityHandlerRevision(handle);
        var effective = unchecked((uint)PdfiumNative.FPDF_GetDocPermissions(handle).Value);
        var user = unchecked((uint)PdfiumNative.FPDF_GetDocUserPermissions(handle).Value);
        // The shipped PDFium build normalizes owner permissions to 0xFFFFFFFC, while its public
        // header describes 0xFFFFFFFF. Compare effective/user permissions instead of a magic value.
        // With unrestricted user permissions both APIs return the same flags for either password.
        const uint revision2PermissionMask = (1u << 2) | (1u << 3) | (1u << 4) | (1u << 5);
        const uint laterPermissionMask = revision2PermissionMask | (1u << 8) | (1u << 9) | (1u << 10) | (1u << 11);
        var mask = revision < 3 ? revision2PermissionMask : laterPermissionMask;
        var access = revision < 0 ? PdfPasswordAccess.Unencrypted
            : (effective & mask) != (user & mask) ? PdfPasswordAccess.Owner
            : (user & mask) == mask ? PdfPasswordAccess.UserOrOwner : PdfPasswordAccess.User;
        Permissions = new(effective, user, revision, access);
        var version = PdfiumNative.FPDF_GetFileVersion(handle, out var value) != 0 ? (int?)value : null;
        Metadata = new(ReadMetadata(handle, "Title"), ReadMetadata(handle, "Author"), ReadMetadata(handle, "Subject"),
            ReadMetadata(handle, "Keywords"), ReadMetadata(handle, "Creator"), ReadMetadata(handle, "Producer"),
            ReadMetadata(handle, "CreationDate"), ReadMetadata(handle, "ModDate"), version,
            PdfiumNative.FPDF_GetSignatureCount(handle));
        Capabilities = PdfDocumentCapabilities.Create(Permissions);
    }

    private static string ReadMetadata(IntPtr handle, string key)
    {
        var length = PdfiumNative.FPDF_GetMetaText(handle, key, null, new CULong(0)).Value;
        if (length <= 2) return "";
        if (length > 1024 * 1024 || (length & 1) != 0)
            throw new PdfDocumentException(PdfViewerStrings.ErrorMetadataInvalid, 3);
        var buffer = new byte[(int)length];
        var actual = PdfiumNative.FPDF_GetMetaText(handle, key, buffer, new CULong(length)).Value;
        if (actual != length)
            throw new PdfDocumentException(PdfViewerStrings.ErrorMetadataUnreadable, 3);
        return Encoding.Unicode.GetString(buffer, 0, buffer.Length - 2);
    }

    public static Task<PdfDocument> LoadAsync(PdfDocumentSource source, string? password = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.LoadAsync(password, cancellationToken);
    }
}
