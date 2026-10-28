using NT.AvaloniaControl.PdfViewer.Interop;

namespace NT.AvaloniaControl.PdfViewer.Editing;

/// <summary>
/// A bounded structural writer for unsigned, unencrypted PDFs. Changes use owned
/// immutable byte snapshots and commit only after a native reopen succeeds.
/// Content edits (FreeText, sanitization, redaction) participate in bounded undo/redo
/// history; user bookmarks follow separate rules: they persist into the saved / exported
/// PDF, read back on open, and never enter or roll back with the content history.
/// Export and save reuse one injected output per state, so repeated outputs of the same
/// state are byte-identical. PDFsharp 6.2.4 regenerates XMP document IDs on every
/// structural save, so byte-for-byte reproducibility across separate sessions is not
/// guaranteed. This backend does not change a viewer's currently loaded read-only snapshot.
/// </summary>
public sealed partial class PdfEditSession : IDisposable, IAsyncDisposable
{
    /// <summary>The default retained entry count per undo / redo stack.</summary>
    public const int DefaultUndoLimit = 100;
    /// <summary>The default retained history budget in bytes.</summary>
    public const long DefaultHistoryByteLimit = 64L * 1024 * 1024;
    /// <summary>The smallest history byte budget a session accepts.</summary>
    public const long MinimumHistoryByteLimit = 64 * 1024;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly AsyncLocal<PdfEditSession?> TransactionScope = new();

    private byte[] _bytes;
    private int _disposed;
    private long _revision;
    private long _stateId = 1;
    // History restores _stateId; allocation must never rewind with it or a new
    // branch can accidentally acquire the identity of an earlier saved state.
    private long _nextStateId = 1;
    private long _savedStateId = 1;
    private readonly List<HistoryEntry> _undo = [];
    private readonly List<HistoryEntry> _redo = [];
    private long _redoBytes;
    private long _historyBytes;
    private int _undoLimit = DefaultUndoLimit;
    private long _historyByteLimit = DefaultHistoryByteLimit;
    private IReadOnlyList<PdfEditUserBookmark> _bookmarks;
    private IReadOnlyList<PdfEditUserBookmark> _savedBookmarks;
    private bool _bookmarkDirty;
    // Cached bookmark-injected output: PDFsharp regenerates XMP document IDs on every structural
    // save (its PrepareForSave replaces /Metadata unconditionally), so repeat outputs of the same
    // state can only be byte-identical by reusing one injected byte array per state.
    private byte[]? _injectedOutput;

    private PdfEditSession(byte[] bytes, int pageCount, IReadOnlyList<PdfEditUserBookmark> bookmarks)
    {
        _bytes = bytes;
        PageCount = pageCount;
        _bookmarks = bookmarks.ToArray();
        _savedBookmarks = _bookmarks;
    }

    public int PageCount { get; }
    /// <summary>The total number of successful mutations, including undo / redo and bookmark updates.</summary>
    public long Revision => Interlocked.Read(ref _revision);
    /// <summary>True while content bytes or the bookmark set differ from the last saved output.</summary>
    public bool IsDirty => Interlocked.Read(ref _stateId) != Interlocked.Read(ref _savedStateId) || Volatile.Read(ref _bookmarkDirty);

    /// <summary>The retained entry count per undo / redo stack; values below 1 clamp to 1 and trim immediately.</summary>
    public int UndoLimit
    {
        get => Volatile.Read(ref _undoLimit);
        set
        {
            ThrowIfInTransaction();
            _gate.Wait();
            try { _undoLimit = Math.Max(1, value); TrimHistory(); }
            finally { _gate.Release(); }
        }
    }

    /// <summary>
    /// The retained history byte budget across both stacks; values below <see cref="MinimumHistoryByteLimit"/>
    /// clamp to it and trim immediately. The newest undo entry is always retained, so the recorded bytes can
    /// exceed the budget by that single entry.
    /// </summary>
    public long HistoryByteLimit
    {
        get => Interlocked.Read(ref _historyByteLimit);
        set
        {
            ThrowIfInTransaction();
            _gate.Wait();
            try { _historyByteLimit = Math.Max(MinimumHistoryByteLimit, value); TrimHistory(); }
            finally { _gate.Release(); }
        }
    }

    /// <summary>The number of retained undo entries.</summary>
    public int UndoCount => _undo.Count;
    /// <summary>The number of retained redo entries.</summary>
    public int RedoCount => _redo.Count;
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    /// <summary>The retained history bytes across both stacks.</summary>
    public long HistoryBytes => Interlocked.Read(ref _historyBytes);

    /// <summary>
    /// Opens an independent copy. Encrypted and signed documents are rejected. Persisted user
    /// bookmarks are read back and stripped from the content snapshots. The catalog is parsed
    /// structurally for the private bookmark block, so documents that fail structural parsing
    /// are rejected at open instead of failing on the first edit.
    /// </summary>
    public static async Task<PdfEditSession> OpenAsync(byte[] bytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length is 0 or > 256 * 1024 * 1024) throw new ArgumentException("Editing input must be between 1 byte and 256 MiB.", nameof(bytes));
        cancellationToken.ThrowIfCancellationRequested();
        var owned = (byte[])bytes.Clone();
        var count = await PdfWriteValidation.ValidateAsync(owned, cancellationToken).ConfigureAwait(false);
        var (content, bookmarks) = await Task.Run(() => PdfUserBookmarkStore.Strip(owned, cancellationToken), cancellationToken).ConfigureAwait(false);
        if (!ReferenceEquals(content, owned) && await PdfWriteValidation.ValidateAsync(content, cancellationToken).ConfigureAwait(false) != count)
            throw new InvalidDataException("Bookmark normalization changed the page count.");
        foreach (var bookmark in bookmarks)
            if ((uint)bookmark.PageIndex >= (uint)count)
                throw new InvalidDataException("The persisted NT user bookmark block contains an out-of-range page.");
        return new PdfEditSession(content, count, bookmarks);
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
        var inTransaction = TransactionScope.Value == this;
        if (!inTransaction) await _gate.WaitAsync(token).ConfigureAwait(false);
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
            CommitContent(candidate);
            return new PdfFreeTextAnnotation(id, pageIndex, bounds, text);
        }
        finally { if (!inTransaction) _gate.Release(); }
    }

    /// <summary>Undoes the most recent content edit. Bookmarks are never rolled back.</summary>
    public async Task UndoAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfInTransaction();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureUsable();
            if (_undo.Count == 0) throw new InvalidOperationException("There is no edit to undo.");
            _redo.Add(new HistoryEntry(_bytes, _stateId));
            _redoBytes += _bytes.Length;
            _historyBytes += _bytes.Length;
            var entry = _undo[^1];
            _undo.RemoveAt(_undo.Count - 1);
            _historyBytes -= entry.Bytes.Length;
            _bytes = entry.Bytes;
            _injectedOutput = null;
            Interlocked.Exchange(ref _stateId, entry.StateId);
            Interlocked.Increment(ref _revision);
            TrimHistory();
        }
        finally { _gate.Release(); }
    }

    /// <summary>Re-applies the most recently undone content edit. Bookmarks are never affected.</summary>
    public async Task RedoAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfInTransaction();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureUsable();
            if (_redo.Count == 0) throw new InvalidOperationException("There is no edit to redo.");
            _undo.Add(new HistoryEntry(_bytes, _stateId));
            _historyBytes += _bytes.Length;
            var entry = _redo[^1];
            _redo.RemoveAt(_redo.Count - 1);
            _redoBytes -= entry.Bytes.Length;
            _historyBytes -= entry.Bytes.Length;
            _bytes = entry.Bytes;
            _injectedOutput = null;
            Interlocked.Exchange(ref _stateId, entry.StateId);
            Interlocked.Increment(ref _revision);
            TrimHistory();
        }
        finally { _gate.Release(); }
    }

    /// <summary>
    /// Runs <paramref name="body"/> as one atomic edit: every content edit inside is grouped into a
    /// single undo entry, and any failure or cancellation rolls the session back to its previous
    /// bytes, revision and dirty state. Undo / redo, saves and bookmark changes are not allowed
    /// inside the body; transactions cannot nest. The body must not start parallel work that calls
    /// back into this session — the transaction owns the session gate for its whole lifetime.
    /// </summary>
    public async Task ExecuteTransactionAsync(Func<CancellationToken, Task> body, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (TransactionScope.Value is not null) throw new InvalidOperationException("Transactions cannot nest.");
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureUsable();
            cancellationToken.ThrowIfCancellationRequested();
            var originalBytes = _bytes;
            var originalStateId = Interlocked.Read(ref _stateId);
            var originalRevision = Interlocked.Read(ref _revision);
            TransactionScope.Value = this;
            try
            {
                await body(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                EnsureUsable();
                if (!ReferenceEquals(_bytes, originalBytes)) RecordHistory(originalBytes, originalStateId);
            }
            catch
            {
                _bytes = originalBytes;
                _injectedOutput = null;
                Interlocked.Exchange(ref _stateId, originalStateId);
                Interlocked.Exchange(ref _revision, originalRevision);
                throw;
            }
            finally { TransactionScope.Value = null; }
        }
        finally { _gate.Release(); }
    }

    /// <summary>Returns the session's user bookmark set, including bookmarks persisted in the opened PDF.</summary>
    public async Task<IReadOnlyList<PdfEditUserBookmark>> GetUserBookmarksAsync(CancellationToken cancellationToken = default)
    {
        var inTransaction = TransactionScope.Value == this;
        if (!inTransaction) await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureUsable();
            cancellationToken.ThrowIfCancellationRequested();
            return _bookmarks.ToArray();
        }
        finally { if (!inTransaction) _gate.Release(); }
    }

    /// <summary>
    /// Replaces the session's user bookmark set. Bookmarks follow rules separate from content undo
    /// history: they never enter it and are never rolled back, an identical set is a no-op, and the
    /// persisted block is injected at the export / save boundary.
    /// </summary>
    public async Task SetUserBookmarksAsync(IReadOnlyList<PdfEditUserBookmark> bookmarks, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bookmarks);
        ThrowIfInTransaction();
        var owned = bookmarks.ToArray();
        var ids = new HashSet<Guid>();
        foreach (var bookmark in owned)
        {
            ArgumentNullException.ThrowIfNull(bookmark);
            if (bookmark.Id == Guid.Empty) throw new ArgumentException("A bookmark ID cannot be empty.", nameof(bookmarks));
            if (!ids.Add(bookmark.Id)) throw new ArgumentException("Bookmark IDs must be unique.", nameof(bookmarks));
            if (string.IsNullOrWhiteSpace(bookmark.Title) || bookmark.Title.Length > PdfUserBookmarkStore.MaximumTitleLength || bookmark.Title.Contains('\0'))
                throw new ArgumentException("A bookmark title must contain 1 to 512 UTF-16 code units and no null characters.", nameof(bookmarks));
            if (bookmark.Flags != PdfUserBookmarkFlags.None && bookmark.Flags != PdfUserBookmarkFlags.Bold
                && bookmark.Flags != PdfUserBookmarkFlags.Italic && bookmark.Flags != (PdfUserBookmarkFlags.Bold | PdfUserBookmarkFlags.Italic))
                throw new ArgumentOutOfRangeException(nameof(bookmarks), "Only None, Bold and Italic bookmark flags are supported.");
            ValidatePage(bookmark.PageIndex);
        }
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureUsable();
            cancellationToken.ThrowIfCancellationRequested();
            if (_bookmarks.SequenceEqual(owned)) return;
            _bookmarks = owned;
            _injectedOutput = null;
            UpdateBookmarkDirty();
            Interlocked.Increment(ref _revision);
        }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<PdfFreeTextAnnotation>> GetFreeTextAnnotationsAsync(int pageIndex, CancellationToken cancellationToken = default)
    {
        ValidatePage(pageIndex);
        var inTransaction = TransactionScope.Value == this;
        if (!inTransaction) await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
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
        finally { if (!inTransaction) _gate.Release(); }
    }

    /// <summary>
    /// Returns an owned copy of the current snapshot with the bookmark set injected; exporting does
    /// not clear IsDirty.
    /// </summary>
    public async Task<byte[]> ExportAsync(CancellationToken cancellationToken = default)
    {
        var inTransaction = TransactionScope.Value == this;
        if (!inTransaction) await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureUsable();
            cancellationToken.ThrowIfCancellationRequested();
            var output = await BuildOutputAsync(_bytes, cancellationToken).ConfigureAwait(false);
            return (byte[])output.Clone();
        }
        finally { if (!inTransaction) _gate.Release(); }
    }

    /// <summary>
    /// Writes at the stream's current position, leaving it open. Failure can leave
    /// partial stream output; session bytes and dirty state are retained.
    /// </summary>
    public async Task SaveAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite) throw new ArgumentException("A writable stream is required.", nameof(destination));
        ThrowIfInTransaction();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureUsable();
            var output = await BuildOutputAsync(_bytes, cancellationToken).ConfigureAwait(false);
            await destination.WriteAsync(output, cancellationToken).ConfigureAwait(false);
            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            EnsureUsable();
            MarkSaved();
        }
        finally { _gate.Release(); }
    }

    /// <summary>Writes through a unique same-directory temp file and atomically replaces an existing target.</summary>
    public Task SaveAsync(string path, CancellationToken cancellationToken = default) => SaveAsync(path, new PdfFileSaveOptions(), cancellationToken);

    /// <summary>Conflict-aware atomic file save, with an optional original-target identity and crash-recovery journal.</summary>
    public async Task<PdfFileSaveResult> SaveAsync(string path, PdfFileSaveOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(options);
        var target = Path.GetFullPath(path);
        ThrowIfInTransaction();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureUsable();
            var expected = options.ExpectedTarget ?? await PdfFileSnapshot.CaptureAsync(target, cancellationToken).ConfigureAwait(false);
            var output = await BuildOutputAsync(_bytes, cancellationToken).ConfigureAwait(false);
            var result = await Saving.PdfAtomicFileSave.SaveAsync(target, output, PageCount,
                options with { ExpectedTarget = expected }, cancellationToken, EnsureUsable).ConfigureAwait(false);
            MarkSaved();
            return result;
        }
        finally { _gate.Release(); }
    }

    /// <summary>Injects the current bookmark set and verifies the result before it leaves the session. One injected array is cached per state so repeated exports and saves of the same state are byte-identical.</summary>
    private async Task<byte[]> BuildOutputAsync(byte[] source, CancellationToken token)
    {
        if (_bookmarks.Count == 0) return source;
        if (_injectedOutput is not null) return _injectedOutput;
        var injected = await Task.Run(() => PdfUserBookmarkStore.Inject(source, _bookmarks, token), token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        if (await PdfWriteValidation.ValidateAsync(injected, token).ConfigureAwait(false) != PageCount)
            throw new InvalidDataException("The bookmark-injected output could not be verified.");
        _injectedOutput = injected;
        return injected;
    }

    /// <summary>Commits validated content bytes: records one history entry outside transactions and bumps state identity and revision.</summary>
    private void CommitContent(byte[] candidate)
    {
        EnsureUsable();
        if (TransactionScope.Value != this) RecordHistory(_bytes, _stateId);
        _bytes = candidate;
        _injectedOutput = null;
        Interlocked.Exchange(ref _stateId, Interlocked.Increment(ref _nextStateId));
        Interlocked.Increment(ref _revision);
    }

    private void RecordHistory(byte[] preBytes, long preStateId)
    {
        _undo.Add(new HistoryEntry(preBytes, preStateId));
        _historyBytes += preBytes.Length;
        if (_redo.Count != 0)
        {
            _historyBytes -= _redoBytes;
            _redoBytes = 0;
            _redo.Clear();
        }
        TrimHistory();
    }

    /// <summary>Evicts oldest undo entries first, then oldest redo entries; the newest entry per stack is always retained.</summary>
    private void TrimHistory()
    {
        while (_undo.Count > 1 && (_undo.Count > _undoLimit || _historyBytes > _historyByteLimit))
        {
            _historyBytes -= _undo[0].Bytes.Length;
            _undo.RemoveAt(0);
        }
        while (_redo.Count > 1 && (_redo.Count > _undoLimit || _historyBytes > _historyByteLimit))
        {
            _historyBytes -= _redo[0].Bytes.Length;
            _redoBytes -= _redo[0].Bytes.Length;
            _redo.RemoveAt(0);
        }
    }

    private void MarkSaved()
    {
        Interlocked.Exchange(ref _savedStateId, Interlocked.Read(ref _stateId));
        // Both lists are private immutable snapshots. Subsequent changes replace
        // _bookmarks, so saving needs no allocation after the output is committed.
        _savedBookmarks = _bookmarks;
        Volatile.Write(ref _bookmarkDirty, false);
    }

    private void UpdateBookmarkDirty() =>
        Volatile.Write(ref _bookmarkDirty, !_bookmarks.SequenceEqual(_savedBookmarks));

    private void ThrowIfInTransaction()
    {
        if (TransactionScope.Value == this) throw new InvalidOperationException("This operation is not allowed inside an edit transaction.");
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
        try
        {
            _bytes = [];
            _undo.Clear();
            _redo.Clear();
            _bookmarks = [];
            _savedBookmarks = [];
            _injectedOutput = null;
            Interlocked.Exchange(ref _historyBytes, 0);
            Interlocked.Exchange(ref _redoBytes, 0);
        }
        finally { _gate.Release(); }
    }

    private readonly record struct HistoryEntry(byte[] Bytes, long StateId);
}
