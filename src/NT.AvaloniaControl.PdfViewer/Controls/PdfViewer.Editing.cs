using System.Windows.Input;
using Avalonia;
using Avalonia.Threading;
using NT.AvaloniaControl.PdfViewer.Editing;

namespace NT.AvaloniaControl.PdfViewer;

public partial class PdfViewer
{
    public static readonly StyledProperty<bool> AutoSaveProperty = AvaloniaProperty.Register<PdfViewer, bool>(nameof(AutoSave));
    public static readonly StyledProperty<TimeSpan> AutoSaveIntervalProperty = AvaloniaProperty.Register<PdfViewer, TimeSpan>(nameof(AutoSaveInterval),
        TimeSpan.FromSeconds(2), validate: value => value >= TimeSpan.FromMilliseconds(100) && value <= TimeSpan.FromDays(1));
    public static readonly StyledProperty<IPdfUnsavedChangesService?> UnsavedChangesServiceProperty = AvaloniaProperty.Register<PdfViewer, IPdfUnsavedChangesService?>(nameof(UnsavedChangesService));
    public static readonly DirectProperty<PdfViewer, bool> IsDirtyProperty = AvaloniaProperty.RegisterDirect<PdfViewer, bool>(nameof(IsDirty), c => c.IsDirty);
    public static readonly DirectProperty<PdfViewer, bool> IsSavingProperty = AvaloniaProperty.RegisterDirect<PdfViewer, bool>(nameof(IsSaving), c => c.IsSaving);
    public static readonly DirectProperty<PdfViewer, bool> IsEditingProperty = AvaloniaProperty.RegisterDirect<PdfViewer, bool>(nameof(IsEditing), c => c.IsEditing);
    public static readonly DirectProperty<PdfViewer, bool> CanUndoProperty = AvaloniaProperty.RegisterDirect<PdfViewer, bool>(nameof(CanUndo), c => c.CanUndo);
    public static readonly DirectProperty<PdfViewer, bool> CanRedoProperty = AvaloniaProperty.RegisterDirect<PdfViewer, bool>(nameof(CanRedo), c => c.CanRedo);
    public static readonly DirectProperty<PdfViewer, long> RevisionProperty = AvaloniaProperty.RegisterDirect<PdfViewer, long>(nameof(Revision), c => c.Revision);
    public static readonly DirectProperty<PdfViewer, string?> SavePathProperty = AvaloniaProperty.RegisterDirect<PdfViewer, string?>(nameof(SavePath), c => c.SavePath);
    public static readonly DirectProperty<PdfViewer, bool> IsUnsavedChangesPromptVisibleProperty = AvaloniaProperty.RegisterDirect<PdfViewer, bool>(nameof(IsUnsavedChangesPromptVisible), c => c.IsUnsavedChangesPromptVisible);

    private PdfEditSession? _editSession;
    private Task _editTail = Task.CompletedTask;
    private bool _isDirty, _isSaving, _isEditing, _canUndo, _canRedo, _isUnsavedChangesPromptVisible;
    private long _editRevision;
    private string? _savePath;
    private bool _syncingBookmarks;
    private PdfEditUserBookmark[] _savedViewerBookmarks = [];
    private bool _viewerBookmarksDirty;
    private long _bookmarkVersion, _syncedBookmarkVersion;
    private readonly DispatcherTimer _autoSaveTimer = new();
    private Task? _autoSaveTask;
    private long _changeRequestVersion;
    private CancellationTokenSource? _changeRequestCancellation;
    private TaskCompletionSource<PdfUnsavedChangesDecision>? _unsavedDecision;
    private PdfDocumentSource? _activeDocumentSource;
    private bool _editingUnavailable;
    private PdfFileSnapshot? _saveBaseline;
    private PdfSaveConflictException? _lastSaveConflict;
    private PdfSaveDurabilityException? _lastSaveDurabilityFailure;
    public static readonly DirectProperty<PdfViewer, PdfSaveDurabilityException?> LastSaveDurabilityFailureProperty = AvaloniaProperty.RegisterDirect<PdfViewer, PdfSaveDurabilityException?>(nameof(LastSaveDurabilityFailure), c => c.LastSaveDurabilityFailure);
    public PdfSaveDurabilityException? LastSaveDurabilityFailure => _lastSaveDurabilityFailure;
    public static readonly DirectProperty<PdfViewer, PdfSaveConflictException?> LastSaveConflictProperty = AvaloniaProperty.RegisterDirect<PdfViewer, PdfSaveConflictException?>(nameof(LastSaveConflict), c => c.LastSaveConflict);
    public PdfSaveConflictException? LastSaveConflict => _lastSaveConflict;
    public event EventHandler<PdfSaveConflictEventArgs>? SaveConflict;

    /// <summary>Debounced saves to SavePath. Byte / stream sources require a successful file Save As first.</summary>
    public bool AutoSave { get => GetValue(AutoSaveProperty); set => SetValue(AutoSaveProperty, value); }
    public TimeSpan AutoSaveInterval { get => GetValue(AutoSaveIntervalProperty); set => SetValue(AutoSaveIntervalProperty, value); }
    public IPdfUnsavedChangesService? UnsavedChangesService { get => GetValue(UnsavedChangesServiceProperty); set => SetValue(UnsavedChangesServiceProperty, value); }
    public bool IsDirty => _isDirty;
    public bool IsSaving => _isSaving;
    public bool IsEditing => _isEditing;
    public bool CanUndo => _canUndo;
    public bool CanRedo => _canRedo;
    public long Revision => _editRevision;
    public string? SavePath => _savePath;
    public bool IsUnsavedChangesPromptVisible => _isUnsavedChangesPromptVisible;
    public ICommand SaveCommand { get; private set; } = null!;
    public ICommand SaveAsCommand { get; private set; } = null!;
    public ICommand UndoCommand { get; private set; } = null!;
    public ICommand RedoCommand { get; private set; } = null!;
    public event EventHandler<PdfSaveRequestedEventArgs>? SaveRequested;
    public event EventHandler<PdfDocumentSavedEventArgs>? DocumentSaved;
    public event EventHandler<PdfSaveFailedEventArgs>? SaveFailed;
    public event EventHandler<PdfUnsavedChangesEventArgs>? UnsavedChangesRequested;

    private void InitializeEditing()
    {
        SaveCommand = Add(() => Observe(SaveAsync()), () => Capabilities.CanSave && !IsEditing);
        SaveAsCommand = Add(() => Observe(SaveAsAsync()), () => Capabilities.CanSave && !IsEditing);
        UndoCommand = Add(() => Observe(UndoAsync()), () => CanUndo && CanWriteDocument && !IsEditing);
        RedoCommand = Add(() => Observe(RedoAsync()), () => CanRedo && CanWriteDocument && !IsEditing);
        _autoSaveTimer.Tick += (_, _) =>
        {
            _autoSaveTimer.Stop();
            if (!CanAutoSave || _autoSaveTask is { IsCompleted: false }) return;
            _autoSaveTask = RunAutoSaveAsync();
        };
        _userBookmarks.CollectionChanged += (_, _) => ViewerBookmarksChanged();

        ViewerCommand Add(Action run, Func<bool> enabled)
        {
            var command = new ViewerCommand(this, _ => run(), _ => enabled());
            _contractCommands.Add(command);
            return command;
        }
    }

    private bool CanWriteDocument => !_disposed && !IsLoading && _document is { } doc && !IsReadOnly &&
        !_editingUnavailable && !doc.Permissions.IsEncrypted && doc.Metadata.SignatureCount == 0 && doc.InputBytes <= 256L * 1024 * 1024;

    private PdfCapability ViewerSaveCapability => _disposed ? PdfCapability.Unavailable(PdfCapabilityUnavailableReason.Disposed)
        : IsLoading || IsSaving ? PdfCapability.Unavailable(PdfCapabilityUnavailableReason.Busy)
        : _document is null ? PdfCapability.Unavailable(PdfCapabilityUnavailableReason.NoDocument)
        : IsReadOnly ? PdfCapability.Unavailable(PdfCapabilityUnavailableReason.ReadOnly)
        : !CanWriteDocument ? PdfCapability.Unavailable(PdfCapabilityUnavailableReason.UnsupportedDocument)
        : PdfCapability.Available;

    private void RequireWritable()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!CanWriteDocument) throw new NotSupportedException("Saving / editing requires an unsigned, unencrypted PDF of at most 256 MiB and IsReadOnly=false.");
    }

    private PdfEditUserBookmark[] CaptureViewerBookmarks() => _userBookmarks.Select(b => new PdfEditUserBookmark(b.Id, b.Title, b.PageIndex, b.Flags)).ToArray();

    private void ViewerBookmarksChanged()
    {
        if (_syncingBookmarks || _disposed || !HasDocument) return;
        ++_bookmarkVersion;
        _viewerBookmarksDirty = !CaptureViewerBookmarks().SequenceEqual(_savedViewerBookmarks);
        RefreshEditingState();
        ScheduleAutoSave();
    }

    private void ReplaceViewerBookmarks(IReadOnlyList<PdfEditUserBookmark> bookmarks)
    {
        _syncingBookmarks = true;
        try
        {
            _userBookmarks.Clear();
            foreach (var bookmark in bookmarks)
                _userBookmarks.Add(new PdfUserBookmark(bookmark.Title, bookmark.PageIndex, bookmark.Flags, bookmark.Id));
        }
        finally { _syncingBookmarks = false; }
    }

    /// <summary>Creates the owned edit session lazily, and reads persisted private user bookmarks.</summary>
    public Task EnableEditingAsync(CancellationToken cancellationToken = default) => QueueEditAsync(async token =>
    {
        await EnsureEditSessionAsync(token);
        return true;
    }, cancellationToken);

    private async Task<PdfEditSession> EnsureEditSessionAsync(CancellationToken token)
    {
        RequireWritable();
        if (_editSession is null)
        {
            var document = _document!;
            var bytes = await document.CaptureEditingInputAsync(token);
            if (SavePath is not null) _saveBaseline = PdfFileSnapshot.FromBytes(bytes);
            PdfEditSession session;
            try { session = await PdfEditSession.OpenAsync(bytes, token); }
            catch (Exception error) when (error is NotSupportedException or InvalidDataException)
            { _editingUnavailable = true; RefreshContractState(); throw; }
            if (token.IsCancellationRequested || _disposed) { await session.DisposeAsync(); token.ThrowIfCancellationRequested(); throw new ObjectDisposedException(nameof(PdfViewer)); }
            _editSession = session;
            var persisted = await session.GetUserBookmarksAsync(token);
            token.ThrowIfCancellationRequested();
            if (!_viewerBookmarksDirty)
            {
                ReplaceViewerBookmarks(persisted);
                _savedViewerBookmarks = persisted.ToArray();
            }
        }
        await _editSession.SetUserBookmarksAsync(CaptureViewerBookmarks(), token);
        _syncedBookmarkVersion = _bookmarkVersion;
        _viewerBookmarksDirty = false;
        return _editSession;
    }

    /// <summary>Runs content edits as one undo transaction, then refreshes the rendered snapshot without changing the document session ID.
    /// The callback must not save, dispose, change bookmarks, or start parallel operations against the session.</summary>
    public Task ExecuteEditAsync(Func<PdfEditSession, CancellationToken, Task> edit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(edit);
        return QueueEditAsync(async token =>
        {
            var session = await EnsureEditSessionAsync(token);
            await session.ExecuteTransactionAsync(ct => edit(session, ct), token);
            await RefreshEditedDocumentAsync(session, token);
            return true;
        }, cancellationToken);
    }

    public Task UndoAsync(CancellationToken cancellationToken = default) => HistoryAsync(false, cancellationToken);
    public Task RedoAsync(CancellationToken cancellationToken = default) => HistoryAsync(true, cancellationToken);
    private Task HistoryAsync(bool redo, CancellationToken cancellationToken) => QueueEditAsync(async token =>
    {
        var session = await EnsureEditSessionAsync(token);
        if (redo) await session.RedoAsync(token); else await session.UndoAsync(token);
        await RefreshEditedDocumentAsync(session, token);
        return true;
    }, cancellationToken);

    /// <summary>Returns owned PDF bytes without clearing Dirty or changing SavePath.</summary>
    public Task<byte[]> ExportDocumentAsync(CancellationToken cancellationToken = default) => QueueEditAsync(async token =>
        await (await EnsureEditSessionAsync(token)).ExportAsync(token), cancellationToken);

    /// <summary>Saves at the stream's current position, leaves it open, and marks that snapshot saved. Stream failure may leave partial output.</summary>
    public Task SaveAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return QueueEditAsync(async token =>
        {
            await SaveSessionAsync(null, destination, false, token);
            return true;
        }, cancellationToken);
    }

    /// <summary>Saves to the current local target, or requests a Save As target. False means canceled.</summary>
    public Task<bool> SaveAsync(CancellationToken cancellationToken = default) => SaveFileAsync(null, false, false, cancellationToken);
    public Task<bool> SaveAsAsync(CancellationToken cancellationToken = default) => SaveFileAsync(null, true, false, cancellationToken);
    /// <summary>Atomically saves to a local target and makes it the subsequent Save / AutoSave destination.</summary>
    public Task<bool> SaveAsAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return SaveFileAsync(Path.GetFullPath(path), true, false, cancellationToken);
    }

    private Task<bool> SaveFileAsync(string? path, bool saveAs, bool autoSave, CancellationToken cancellationToken) => QueueEditAsync(async token =>
    {
        RequireWritable();
        if (autoSave && (!AutoSave || !IsDirty || SavePath is null)) return false;
        var request = new PdfSaveRequestedEventArgs(path ?? (saveAs ? null : SavePath), saveAs, autoSave);
        SaveRequested?.Invoke(this, request);
        token.ThrowIfCancellationRequested();
        if (request.Cancel) return false;
        var target = request.Path;
        if (target is null && !autoSave) target = await PickSavePathAsync(token);
        token.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(target)) return false;
        await SaveSessionAsync(Path.GetFullPath(target), null, autoSave, token);
        return true;
    }, cancellationToken);

    private async Task SaveSessionAsync(string? path, Stream? stream, bool autoSave, CancellationToken token)
    {
        var version = _loadVersion;
        SetAndRaise(IsSavingProperty, ref _isSaving, true);
        RefreshContractState();
        try
        {
            token.ThrowIfCancellationRequested();
            var expected = path is null ? null : SavePath is not null && SameSavePath(path, SavePath) ? _saveBaseline : await PdfFileSnapshot.CaptureAsync(path, token);
            var session = await EnsureEditSessionAsync(token);
            // This immutable set describes the committed output; later UI changes stay dirty.
            var savedBookmarks = (await session.GetUserBookmarksAsync(token)).ToArray();
            PdfFileSaveResult? fileResult = null;
            if (stream is not null) await session.SaveAsync(stream, token);
            else fileResult = await session.SaveAsync(path!, new PdfFileSaveOptions { ExpectedTarget = expected ?? _saveBaseline }, token);
            // No cancellable work after the commit: the file / stream is already saved.
            if (!IsCurrent()) return;
            SetContractError(null);
            if (!IsCurrent()) return;
            SetAndRaise(ErrorMessageProperty, ref _errorMessage, null);
            if (!IsCurrent()) return;
            if (path is not null)
            {
                _saveBaseline = fileResult!.Output;
                SetAndRaise(LastSaveDurabilityFailureProperty, ref _lastSaveDurabilityFailure, null);
                SetAndRaise(LastSaveConflictProperty, ref _lastSaveConflict, null);
                if (!IsCurrent()) return;
                SetAndRaise(SavePathProperty, ref _savePath, path);
                if (!IsCurrent()) return;
                SetSourceInternally(path);
                if (!IsCurrent()) return;
                _activeDocumentSource = PdfDocumentSource.FromFile(path);
                SetDocumentSourceInternally(_activeDocumentSource);
                if (!IsCurrent()) return;
                SetAndRaise(DocumentNameProperty, ref _documentName, Path.GetFileName(path));
                if (!IsCurrent()) return;
            }
            _savedViewerBookmarks = savedBookmarks;
            _viewerBookmarksDirty = !CaptureViewerBookmarks().SequenceEqual(_savedViewerBookmarks);
            RefreshEditingState();
            DocumentSaved?.Invoke(this, new PdfDocumentSavedEventArgs(path, session.Revision, autoSave) { FileResult = fileResult });
        }
        catch (Exception error)
        {
            if (error is PdfSaveDurabilityException durability && IsCurrent())
                SetAndRaise(LastSaveDurabilityFailureProperty, ref _lastSaveDurabilityFailure, durability);
            if (error is PdfSaveConflictException conflict && IsCurrent())
            {
                SetAndRaise(LastSaveConflictProperty, ref _lastSaveConflict, conflict);
                if (IsCurrent()) SaveConflict?.Invoke(this, new PdfSaveConflictEventArgs(conflict, autoSave));
            }
            if (error is not OperationCanceledException && IsCurrent()) SaveFailed?.Invoke(this, new PdfSaveFailedEventArgs(PdfError.FromException(error), autoSave));
            throw;
        }
        finally { SetAndRaise(IsSavingProperty, ref _isSaving, false); RefreshContractState(); }

        bool IsCurrent() => version == _loadVersion && !_disposed;
    }

    private static bool SameSavePath(string first, string second) => string.Equals(Path.GetFullPath(first), Path.GetFullPath(second),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private Task<T> QueueEditAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        var previous = _editTail;
        var version = _loadVersion;
        var lifetime = _documentCancellation.Token;
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _editTail = completion.Task;
        Run();
        return completion.Task;

        async void Run()
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime);
            try
            {
                try { await previous; } catch (Exception) { /* Each caller observes its own failure. */ }
                linked.Token.ThrowIfCancellationRequested();
                if (version != _loadVersion || _disposed) throw new OperationCanceledException(linked.Token);
                SetAndRaise(IsEditingProperty, ref _isEditing, true);
                RefreshContractState();
                var result = await operation(linked.Token);
                completion.TrySetResult(result);
            }
            catch (OperationCanceledException error) { completion.TrySetCanceled(error.CancellationToken); }
            catch (Exception error)
            {
                if (version == _loadVersion && !_disposed) ReportError(error);
                completion.TrySetException(error);
            }
            finally
            {
                if (version == _loadVersion && !_disposed)
                {
                    SetAndRaise(IsEditingProperty, ref _isEditing, false);
                    RefreshEditingState();
                    ScheduleAutoSave();
                }
            }
        }
    }

    private void RefreshEditingState(bool refreshContracts = true)
    {
        SetAndRaise(IsDirtyProperty, ref _isDirty, (_editSession?.IsDirty ?? false) || (_viewerBookmarksDirty && (!IsReadOnly || _editSession is not null)));
        SetAndRaise(CanUndoProperty, ref _canUndo, _editSession?.CanUndo ?? false);
        SetAndRaise(CanRedoProperty, ref _canRedo, _editSession?.CanRedo ?? false);
        SetAndRaise(RevisionProperty, ref _editRevision, _editSession?.Revision ?? 0);
        if (refreshContracts) RefreshContractState();
    }

    private bool CanAutoSave => AutoSave && LastSaveConflict is null && LastSaveDurabilityFailure is null && CanWriteDocument && SavePath is not null && IsDirty && !IsSaving && !IsEditing && !IsUnsavedChangesPromptVisible;
    private void ScheduleAutoSave()
    {
        _autoSaveTimer.Stop();
        if (!CanAutoSave) return;
        _autoSaveTimer.Interval = AutoSaveInterval;
        _autoSaveTimer.Start();
    }
    private async Task RunAutoSaveAsync()
    {
        try { if (!await SaveFileAsync(null, false, true, CancellationToken.None)) _autoSaveTimer.Stop(); }
        catch (OperationCanceledException) { }
        catch (Exception) { _autoSaveTimer.Stop(); /* Reported once; a later edit / explicit retry rearms it. */ }
    }

    private async Task RefreshEditedDocumentAsync(PdfEditSession session, CancellationToken token)
    {
        if (_syncedBookmarkVersion != _bookmarkVersion)
        {
            await session.SetUserBookmarksAsync(CaptureViewerBookmarks(), token);
            _syncedBookmarkVersion = _bookmarkVersion;
        }
        var position = CaptureReadingPosition();
        var candidate = await PdfDocument.LoadAsync(await session.ExportAsync(token), cancellationToken: token);
        if (token.IsCancellationRequested || _disposed) { RetireDocument(candidate); token.ThrowIfCancellationRequested(); return; }
        _searchCancellation?.Cancel();
        CancelThumbnailWork();
        foreach (var thumbnail in _thumbnails) thumbnail.Image = null;
        foreach (var cached in _thumbnailCache.Values) cached.Bitmap.Dispose();
        _thumbnailCache.Clear();
        _thumbnailFailures.Clear();
        var old = _document;
        _document = candidate;
        _contractDocument = candidate; // Snapshot refresh is not an open / new logical session.
        _presenter?.SetDocument(candidate);
        RetireDocument(old);
        SetAndRaise(OutlineProperty, ref _outline, candidate.Bookmarks);
        ReplaceViewerBookmarks(await session.GetUserBookmarksAsync(token));
        ClearSelection();
        ClearSearchResults();
        if (position is not null) RestoreReadingPosition(position);
        RefreshViewport();
        RequestVisibleThumbnails();
        if (!string.IsNullOrWhiteSpace(SearchQuery)) Observe(SearchAsync(SearchQuery));
    }

    private void ResetEditing()
    {
        _autoSaveTimer.Stop();
        var old = _editSession;
        _editSession = null;
        if (old is not null) _documentReleases.Add(ReleaseSessionAsync(_editTail, old));
        _syncingBookmarks = true;
        try { _userBookmarks.Clear(); } finally { _syncingBookmarks = false; }
        _savedViewerBookmarks = [];
        _viewerBookmarksDirty = false;
        _activeDocumentSource = null;
        _editingUnavailable = false;
        _saveBaseline = null;
        SetAndRaise(LastSaveDurabilityFailureProperty, ref _lastSaveDurabilityFailure, null);
        SetAndRaise(LastSaveConflictProperty, ref _lastSaveConflict, null);
        _bookmarkVersion = _syncedBookmarkVersion = 0;
        SetAndRaise(SavePathProperty, ref _savePath, null);
        SetAndRaise(IsSavingProperty, ref _isSaving, false);
        SetAndRaise(IsEditingProperty, ref _isEditing, false);
        RefreshEditingState(false);

        static async Task ReleaseSessionAsync(Task pending, PdfEditSession session)
        {
            try { await pending; } catch (Exception) { }
            await session.DisposeAsync();
        }
    }
}
