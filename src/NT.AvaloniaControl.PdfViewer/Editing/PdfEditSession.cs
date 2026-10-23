using NT.AvaloniaControl.PdfViewer.Interop;

namespace NT.AvaloniaControl.PdfViewer.Editing;

/// <summary>
/// A bounded structural writer for unsigned, unencrypted PDFs. Changes use owned
/// immutable byte snapshots and commit only after a native reopen succeeds.
/// This backend does not change a viewer's currently loaded read-only snapshot.
/// </summary>
public sealed partial class PdfEditSession : IDisposable, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private byte[] _bytes;
    private int _disposed;
    private long _revision;
    private long _savedRevision;
    private PdfEditSession(byte[] bytes, int pageCount) { _bytes = bytes; PageCount = pageCount; }
    public int PageCount { get; }
    public long Revision => Interlocked.Read(ref _revision);
    public bool IsDirty => Revision != Interlocked.Read(ref _savedRevision);

    /// <summary>Opens an independent copy. Encrypted and signed documents are rejected.</summary>
    public static async Task<PdfEditSession> OpenAsync(byte[] bytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length is 0 or > 256 * 1024 * 1024) throw new ArgumentException("Editing input must be between 1 byte and 256 MiB.", nameof(bytes));
        cancellationToken.ThrowIfCancellationRequested();
        var owned = (byte[])bytes.Clone();
        var count = await PdfWriteValidation.ValidateAsync(owned, cancellationToken).ConfigureAwait(false);
        return new PdfEditSession(owned, count);
    }

    /// <summary>Creates a single-line FreeText with an embedded, explicitly supplied font and an AP stream.</summary>
    public Task<PdfFreeTextAnnotation> AddFreeTextAsync(int pageIndex, PdfEditRectangle bounds, string text,
        PdfEmbeddedTrueTypeFont font, float fontSize = 12, CancellationToken cancellationToken = default) =>
        WriteFreeTextAsync(Guid.NewGuid(), pageIndex, bounds, text, font, fontSize, false, cancellationToken);

    /// <summary>Updates a session-owned persisted FreeText; unsupported external annotations are not rewritten.</summary>
    public Task<PdfFreeTextAnnotation> UpdateFreeTextAsync(Guid id, int pageIndex, PdfEditRectangle bounds, string text,
        PdfEmbeddedTrueTypeFont font, float fontSize = 12, CancellationToken cancellationToken = default) =>
        WriteFreeTextAsync(id, pageIndex, bounds, text, font, fontSize, true, cancellationToken);

    private async Task<PdfFreeTextAnnotation> WriteFreeTextAsync(Guid id, int pageIndex, PdfEditRectangle bounds, string text,
        PdfEmbeddedTrueTypeFont font, float fontSize, bool update, CancellationToken token)
    {
        ValidatePage(pageIndex);
        bounds.Validate();
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(font);
        if (id == Guid.Empty) throw new ArgumentException("An annotation ID cannot be empty.", nameof(id));
        if (!float.IsFinite(fontSize) || fontSize is < 1 or > 512) throw new ArgumentOutOfRangeException(nameof(fontSize));
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            EnsureUsable();
            var source = _bytes;
            var preparedFont = await Task.Run(() =>
            {
                PdfAnnotationAppearance.ValidateText(text, font.Data, token);
                return font.EmbeddingMode == PdfFontEmbeddingMode.Full ? font.Data :
                    PdfTrueTypeSubsetter.Create(font.Data, text, token).FontBytes;
            }, token).ConfigureAwait(false);
            var appearance = await PdfiumRuntime.RunAsync(() => PdfAnnotationAppearance.Create(text, preparedFont,
                bounds.Width, bounds.Height, fontSize, token), token).ConfigureAwait(false);
            var candidate = await Task.Run(() => PdfStructureWriter.ApplyFreeText(source, pageIndex, bounds.Left, bounds.Bottom,
                bounds.Width, bounds.Height, text, appearance, id, update, token), token).ConfigureAwait(false);
            if (await PdfWriteValidation.ValidateAsync(candidate, token).ConfigureAwait(false) != PageCount)
                throw new InvalidDataException("Structural writing changed the page count.");
            token.ThrowIfCancellationRequested();
            EnsureUsable();
            _bytes = candidate;
            Interlocked.Increment(ref _revision);
            return new PdfFreeTextAnnotation(id, pageIndex, bounds, text);
        }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<PdfFreeTextAnnotation>> GetFreeTextAnnotationsAsync(int pageIndex, CancellationToken cancellationToken = default)
    {
        ValidatePage(pageIndex);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureUsable();
            var bytes = _bytes;
            var records = await Task.Run(() => PdfStructureWriter.ReadFreeText(bytes, pageIndex), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            EnsureUsable();
            return records.Select(item => new PdfFreeTextAnnotation(item.Id, pageIndex,
                new PdfEditRectangle(item.Left, item.Bottom, item.Width, item.Height), item.Text)).ToArray();
        }
        finally { _gate.Release(); }
    }

    /// <summary>Returns an owned copy of the current snapshot; exporting does not clear IsDirty.</summary>
    public async Task<byte[]> ExportAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { EnsureUsable(); cancellationToken.ThrowIfCancellationRequested(); return (byte[])_bytes.Clone(); }
        finally { _gate.Release(); }
    }

    /// <summary>
    /// Writes at the stream's current position, leaving it open. Failure can leave
    /// partial stream output; session bytes and dirty state are retained.
    /// </summary>
    public async Task SaveAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite) throw new ArgumentException("A writable stream is required.", nameof(destination));
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureUsable();
            await destination.WriteAsync(_bytes, cancellationToken).ConfigureAwait(false);
            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            EnsureUsable();
            Interlocked.Exchange(ref _savedRevision, Revision);
        }
        finally { _gate.Release(); }
    }

    /// <summary>Writes through a unique same-directory temp file and atomically replaces an existing target.</summary>
    public async Task SaveAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var target = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(target)!;
        var temp = Path.Combine(directory, "." + Path.GetFileName(target) + "." + Guid.NewGuid().ToString("N") + ".writing");
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var ownsTemp = false;
        try
        {
            EnsureUsable();
            await using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous))
            {
                ownsTemp = true;
                await output.WriteAsync(_bytes, cancellationToken).ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                output.Flush(true);
            }
            var candidate = await File.ReadAllBytesAsync(temp, cancellationToken).ConfigureAwait(false);
            if (await PdfWriteValidation.ValidateAsync(candidate, cancellationToken).ConfigureAwait(false) != PageCount)
                throw new InvalidDataException("The saved candidate could not be verified.");
            cancellationToken.ThrowIfCancellationRequested();
            EnsureUsable();
            if (File.Exists(target)) File.Replace(temp, target, null);
            else File.Move(temp, target);
            Interlocked.Exchange(ref _savedRevision, Revision);
        }
        finally
        {
            try { if (ownsTemp && File.Exists(temp)) File.Delete(temp); }
            finally { _gate.Release(); }
        }
    }

    private void ValidatePage(int index)
    {
        EnsureUsable();
        if ((uint)index >= (uint)PageCount) throw new ArgumentOutOfRangeException(nameof(index));
    }
    private void EnsureUsable() => ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
    public void Dispose() => Interlocked.Exchange(ref _disposed, 1);
    public async ValueTask DisposeAsync()
    {
        Dispose();
        await _gate.WaitAsync().ConfigureAwait(false);
        try { _bytes = []; }
        finally { _gate.Release(); }
    }
}
