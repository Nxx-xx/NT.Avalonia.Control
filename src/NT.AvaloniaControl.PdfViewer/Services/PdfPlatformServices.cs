namespace NT.AvaloniaControl.PdfViewer;

/// <summary>Host file dialogs. A canceled dialog returns null; operational failures are propagated.</summary>
public interface IPdfFileDialogService
{
    bool CanOpen { get; }
    bool CanSave { get; }
    Task<PdfDocumentSource?> OpenAsync(CancellationToken cancellationToken = default);
    Task<string?> GetSavePathAsync(string suggestedFileName, CancellationToken cancellationToken = default);
}

/// <summary>
/// An owned document snapshot for a host service. It contains no password and does not borrow a native handle.
/// Hosts must apply the document's permissions before requesting printing or sharing.
/// </summary>
public sealed class PdfPlatformDocument
{
    private readonly byte[] _bytes;

    public PdfPlatformDocument(byte[] bytes, string displayName)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (bytes.Length == 0) throw new ArgumentException("A document snapshot cannot be empty.", nameof(bytes));
        _bytes = (byte[])bytes.Clone();
        DisplayName = displayName;
    }

    public string DisplayName { get; }
    public long Length => _bytes.Length;
    /// <summary>Returns a new nonwritable stream, owned by the caller. Its backing array is not exposed.</summary>
    public Stream OpenRead() => new MemoryStream(_bytes, 0, _bytes.Length, writable: false, publiclyVisible: false);
}

/// <summary>Optional host print adapter. Registering an adapter does not implement the viewer's P8 print workflow.</summary>
public interface IPdfPrintService
{
    bool IsAvailable { get; }
    Task PrintAsync(PdfPlatformDocument document, CancellationToken cancellationToken = default);
}

/// <summary>Optional host share adapter. No system integration is loaded merely by opening a PDF.</summary>
public interface IPdfShareService
{
    bool IsAvailable { get; }
    Task ShareAsync(PdfPlatformDocument document, CancellationToken cancellationToken = default);
}
