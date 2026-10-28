namespace NT.AvaloniaControl.PdfViewer;

public sealed partial class PdfDocument
{
    // Capture the handle actually opened, not its path (which may have been replaced).
    // The lifetime lease and native queue keep input disposal from racing this copy.
    internal Task<byte[]> CaptureEditingInputAsync(CancellationToken token) => RunDocumentAsync(cancellation =>
    {
        cancellation.ThrowIfCancellationRequested();
        if (_inputBytes > 256L * 1024 * 1024) throw new NotSupportedException("Editing input exceeds 256 MiB.");
        return _fileInput is { } file ? file.ReadSnapshot(cancellation) : (byte[])((byte[])_dataPin.Target!).Clone();
    }, token);
}
