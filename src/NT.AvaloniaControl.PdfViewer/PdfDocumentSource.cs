namespace NT.AvaloniaControl.PdfViewer;

public enum PdfDocumentSourceKind { File, Bytes, StreamFactory }

/// <summary>
/// Reopenable data source. Bytes are captured on creation; file and stream factories are read for each load.
/// No URI downloading is performed. Every stream returned by a factory is owned and disposed by the loader.
/// </summary>
public sealed class PdfDocumentSource
{
    private readonly byte[]? _bytes;
    private readonly Func<CancellationToken, ValueTask<Stream>>? _streamFactory;

    private PdfDocumentSource(PdfDocumentSourceKind kind, string displayName, string? filePath = null,
        byte[]? bytes = null, Func<CancellationToken, ValueTask<Stream>>? streamFactory = null)
    {
        Kind = kind;
        DisplayName = displayName;
        FilePath = filePath;
        _bytes = bytes;
        _streamFactory = streamFactory;
    }

    public PdfDocumentSourceKind Kind { get; }
    public string DisplayName { get; }
    public string? FilePath { get; }

    public static PdfDocumentSource FromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var absolute = Path.GetFullPath(path);
        return new(PdfDocumentSourceKind.File, Path.GetFileName(absolute), absolute);
    }

    /// <summary>Creates a byte source with the localizable default display name.</summary>
    public static PdfDocumentSource FromBytes(byte[] bytes) => FromBytes(bytes, PdfViewerStrings.DefaultDocumentName);

    public static PdfDocumentSource FromBytes(byte[] bytes, string displayName)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        return new(PdfDocumentSourceKind.Bytes, displayName, bytes: (byte[])bytes.Clone());
    }

    /// <summary>Creates a stream-factory source with the localizable default display name.</summary>
    public static PdfDocumentSource FromStreamFactory(Func<CancellationToken, ValueTask<Stream>> streamFactory) =>
        FromStreamFactory(streamFactory, PdfViewerStrings.DefaultDocumentName);

    public static PdfDocumentSource FromStreamFactory(Func<CancellationToken, ValueTask<Stream>> streamFactory,
        string displayName)
    {
        ArgumentNullException.ThrowIfNull(streamFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        return new(PdfDocumentSourceKind.StreamFactory, displayName, streamFactory: streamFactory);
    }

    /// <summary>
    /// Opens this source with the cheapest strategy for its kind: file paths load with random
    /// access (no full-file copy), captured bytes load from their owned array, and factory
    /// streams are read for this load only (seekable streams get a single exact-size buffer,
    /// non-seekable ones are staged to an owned temp file). Every load re-reads the source, so
    /// password retries and reopen attempts observe the current file contents.
    /// </summary>
    internal Task<PdfDocument> LoadAsync(string? password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (FilePath is not null) return PdfDocument.LoadFromFileAsync(FilePath, password, cancellationToken);
        if (_bytes is not null) return PdfDocument.LoadAsync(_bytes, password, cancellationToken);
        return LoadStreamFactoryAsync(password, cancellationToken);
    }

    private async Task<PdfDocument> LoadStreamFactoryAsync(string? password, CancellationToken cancellationToken)
    {
        // The factory stream is owned and disposed by the loader path (PdfDocument.LoadAsync
        // disposes it unless a failure leaves this method before handing it over).
        var stream = await _streamFactory!(cancellationToken).ConfigureAwait(false)
            ?? throw new IOException(PdfViewerStrings.ErrorStreamFactoryNull);
        if (!stream.CanRead)
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            throw new IOException(PdfViewerStrings.ErrorStreamFactoryUnreadable);
        }
        return await PdfDocument.LoadAsync(stream, password, leaveOpen: false, cancellationToken).ConfigureAwait(false);
    }
}
