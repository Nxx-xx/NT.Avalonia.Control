using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace NT.AvaloniaControl.PdfViewer;

public partial class PdfViewer
{
    private long _loadVersion;
    private CancellationTokenSource? _loadCancellation;
    private CancellationTokenSource _documentCancellation = new();
    private CancellationTokenSource? _thumbnailCancellation;
    private readonly Dictionary<int, (WriteableBitmap Bitmap, long Bytes, long Stamp)> _thumbnailCache = [];
    private readonly HashSet<int> _thumbnailPending = [];
    private long _thumbnailStamp;
    private long _thumbnailGeneration;
    private readonly HashSet<int> _thumbnailFailures = [];
    // Largest thumbnail decoded so far; admission control reserves this much budget headroom
    // before scheduling another one, so a small budget stops scheduling instead of evicting
    // and immediately re-requesting the same sidebar thumbnails in a loop.
    private long _thumbnailReservation;
    private readonly HashSet<Task> _pendingLoads = [];
    private readonly HashSet<Task> _documentReleases = [];
    private Task? _disposeCompletion;
    private PdfDocument.StreamSource? _streamSource;
    // The failed load a password prompt can retry: same open channel, name and source, with a
    // new password. The channel re-opens the source on every attempt (files observe current
    // contents), which is what "same read" means for reopenable sources.
    private (Func<string?, CancellationToken, Task<PdfDocument>> Open, string Name, string? Source, PdfDocumentSource? Attempted)? _passwordRetry;

    /// <summary>Loads a local file with random access (no full-file copy). A new load closes the previous document; failures are reported and rethrown.</summary>
    public Task LoadDocumentAsync(string path, string? password = null, CancellationToken cancellationToken = default)
    {
        VerifyAccess();
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        SetDocumentSourceInternally(null);
        return LoadCoreAsync((tokenPassword, token) => PdfDocument.LoadFromFileAsync(path, tokenPassword, token),
            Path.GetFileName(path), path, password ?? Password, cancellationToken);
    }

    /// <summary>Captures from the current position once, including password retries. The caller retains ownership of the stream.</summary>
    public Task LoadDocumentAsync(Stream stream, string? password = null, CancellationToken cancellationToken = default)
    {
        VerifyAccess();
        ArgumentNullException.ThrowIfNull(stream);
        SetDocumentSourceInternally(null);
        var source = new PdfDocument.StreamSource(stream);
        return LoadCoreAsync(source.OpenAsync,
            PdfViewerStrings.DefaultDocumentName, null, password ?? Password, cancellationToken);
    }

    public Task LoadDocumentAsync(byte[] bytes, string? password = null, CancellationToken cancellationToken = default)
    {
        VerifyAccess();
        ArgumentNullException.ThrowIfNull(bytes);
        SetDocumentSourceInternally(null);
        return LoadCoreAsync((tokenPassword, token) => PdfDocument.LoadAsync(bytes, tokenPassword, token),
            PdfViewerStrings.DefaultDocumentName, null, password ?? Password, cancellationToken);
    }

    private Task LoadCoreAsync(Func<string?, CancellationToken, Task<PdfDocument>> open, string name, string? source,
        string? password, CancellationToken cancellationToken, PdfDocumentSource? attemptedDocumentSource = null)
    {
        // Register before any bindable state/event is published: a handler can dispose us synchronously.
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingLoads.Add(completion.Task);
        var change = BeginDocumentChange(cancellationToken);
        CompleteRegisteredLoadAsync(completion, open, name, source, password, change.Token, attemptedDocumentSource, change.Version);
        return completion.Task;
    }

    private async void CompleteRegisteredLoadAsync(TaskCompletionSource completion,
        Func<string?, CancellationToken, Task<PdfDocument>> open, string name, string? source, string? password, CancellationToken token,
        PdfDocumentSource? attemptedDocumentSource, long changeRequest)
    {
        try
        {
            if (!await ConfirmDocumentChangeAsync(PdfDocumentChangeReason.Replace, changeRequest, token))
            {
                if (changeRequest == _changeRequestVersion)
                {
                    SetSourceInternally(_savePath);
                    SetDocumentSourceInternally(_activeDocumentSource);
                }
                (open.Target as PdfDocument.StreamSource)?.Dispose();
                throw new OperationCanceledException(new CancellationToken(true));
            }
            await LoadCoreImplementationAsync(open, name, source, password, token, attemptedDocumentSource);
            completion.TrySetResult();
        }
        catch (OperationCanceledException error) { completion.TrySetCanceled(error.CancellationToken); }
        catch (Exception error) { completion.TrySetException(error); }
        finally
        {
            if (open.Target is PdfDocument.StreamSource owned && !ReferenceEquals(owned, _streamSource)) owned.Dispose();
            _pendingLoads.Remove(completion.Task);
        }
    }

    private async Task LoadCoreImplementationAsync(Func<string?, CancellationToken, Task<PdfDocument>> open, string name, string? source,
        string? password, CancellationToken cancellationToken, PdfDocumentSource? attemptedDocumentSource)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        // Any new load supersedes an offered password prompt and its retry data.
        ResetPasswordPrompt();
        // The attempted source is an argument, independent of bindable state or legacy
        // error handlers which can synchronously change the viewer's current source.
        var version = ++_loadVersion;
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _loadCancellation.Token;
        var streamSource = open.Target as PdfDocument.StreamSource;
        if (!ReferenceEquals(_streamSource, streamSource))
        {
            _streamSource?.Dispose();
            _streamSource = streamSource;
        }
        PdfDocument? loaded = null;
        try
        {
            ClearDocument();
            EnsureCurrentLoad();
            SetSourceInternally(source);
            EnsureCurrentLoad();
            SetAndRaise(IsLoadingProperty, ref _isLoading, true);
            EnsureCurrentLoad();
            SetContractError(null);
            RefreshContractState();
            EnsureCurrentLoad();
            SetAndRaise(ErrorMessageProperty, ref _errorMessage, null);
            PseudoClasses.Set(":loading", true);
            PseudoClasses.Set(":error", false);
            UpdateStatus();
            EnsureCurrentLoad();
            loaded = await open(password, token);
            token.ThrowIfCancellationRequested();
            token.ThrowIfCancellationRequested();
            if (version != _loadVersion || _disposed) throw new OperationCanceledException(token);
            _document = loaded;
            var document = loaded;
            loaded = null;
            _activeDocumentSource = attemptedDocumentSource;
            SetAndRaise(SavePathProperty, ref _savePath, source is null ? null : Path.GetFullPath(source));
            EnsureCurrentLoad();
            SetAndRaise(PageCountProperty, ref _pageCount, _document.Pages.Count);
            EnsureCurrentLoad();
            CoerceValue(CurrentPageProperty);
            SetCurrentPage(1);
            SetAndRaise(HasDocumentProperty, ref _hasDocument, true);
            EnsureCurrentLoad();
            SetAndRaise(DocumentNameProperty, ref _documentName, name);
            EnsureCurrentLoad();
            SetAndRaise(ThumbnailsProperty, ref _thumbnails,
                Enumerable.Range(1, PageCount).Select(page => new PdfThumbnail(page)).ToArray());
            EnsureCurrentLoad();
            SetAndRaise(OutlineProperty, ref _outline, document.Bookmarks);
            EnsureCurrentLoad();
            SetAndRaise(PageLabelsProperty, ref _pageLabels, document.PageLabels);
            EnsureCurrentLoad();
            UpdateStatus();
            if (_thumbnailsList is not null) _thumbnailsList.ItemsSource = Thumbnails;
            if (_outlineTree is not null) _outlineTree.ItemsSource = Outline;
            _presenter?.SetDocument(_document);
            PseudoClasses.Set(":document", true);
            GoToPage(1);
            RefreshViewport();
            Dispatcher.UIThread.Post(() =>
            {
                if (version != _loadVersion || _disposed) return;
                RefreshViewport();
                RequestVisibleThumbnails();
            }, DispatcherPriority.Loaded);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error)
        {
            if (version == _loadVersion && !_disposed)
            {
                ReportError(error);
                LoadError?.Invoke(this, EventArgs.Empty);
                RaiseContractLoadFailed(error, attemptedDocumentSource);
                // Password failures keep the load inputs so the built-in prompt can retry them.
                if (version == _loadVersion && !_disposed &&
                    error is PdfDocumentException { Kind: PdfErrorKind.PasswordRequiredOrIncorrect } && IsPasswordPromptEnabled)
                {
                    _passwordRetry = (open, name, source, attemptedDocumentSource);
                    SetAndRaise(IsPasswordPromptVisibleProperty, ref _isPasswordPromptVisible, true);
                    PseudoClasses.Set(":password", true);
                }
            }
            throw;
        }
        finally
        {
            RetireDocument(loaded);
            if (version == _loadVersion && !_disposed)
            {
                SetAndRaise(IsLoadingProperty, ref _isLoading, false);
                if (version == _loadVersion && !_disposed)
                {
                    PseudoClasses.Set(":loading", false);
                    UpdateStatus();
                    RefreshContractState();
                }
                if (version == _loadVersion && _passwordRetry is null) ReleaseStreamSource();
            }
        }
        if (version == _loadVersion && !_disposed)
        {
            ResetPasswordPrompt();
            if (!IsReadOnly && CanWriteDocument)
            {
                try { await EnableEditingAsync(token); }
                catch (Exception error) when (error is NotSupportedException or InvalidDataException) { /* Reading remains available for unsupported editing structures. */ }
            }
            EnsureCurrentLoad();
            DocumentLoaded?.Invoke(this, EventArgs.Empty);
            if (!string.IsNullOrWhiteSpace(SearchQuery)) Observe(SearchAsync(SearchQuery));
        }

        void EnsureCurrentLoad()
        {
            token.ThrowIfCancellationRequested();
            if (version != _loadVersion || _disposed) throw new OperationCanceledException(new CancellationToken(true));
        }
    }

    /// <summary>Opens the desktop file picker. The PDF is fully copied before the storage stream closes.</summary>
    public Task OpenDocumentAsync() => OpenDocumentAsync(CancellationToken.None);

    /// <summary>Opens a host-supplied or Avalonia picker; a newer load or close invalidates its result.</summary>
    public async Task OpenDocumentAsync(CancellationToken cancellationToken)
    {
        VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        var version = _loadVersion;
        try
        {
            var source = await PickDocumentAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (source is null || _disposed || version != _loadVersion) return;
            await LoadDocumentAsync(source, Password, cancellationToken);
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { ReportError(error); }
    }

    /// <summary>
    /// Submits a password for the failed load behind the password prompt, updates
    /// <see cref="Password"/> and reopens the same source. A wrong password reports the error
    /// again and re-offers the prompt while <see cref="IsPasswordPromptEnabled"/> is on. A host
    /// can also call this after <see cref="CancelPasswordPrompt"/> to retry on its own.
    /// </summary>
    public void SubmitPassword(string? password)
    {
        VerifyAccess();
        if (_disposed) return;
        if (_passwordRetry is not { } retry)
        {
            ResetPasswordPrompt();
            return;
        }
        _passwordRetry = null;
        HidePasswordPrompt();
        // The explicit retry below owns the reload; the property only records the value.
        _updatingPassword = true;
        try { SetCurrentValue(PasswordProperty, password); }
        finally { _updatingPassword = false; }
        Observe(LoadCoreAsync(retry.Open, retry.Name, retry.Source, password, CancellationToken.None, retry.Attempted));
    }

    /// <summary>
    /// Hides the password prompt without retrying; the load error stays reported and the retry
    /// data is kept so <see cref="SubmitPassword"/> can still reopen the document later.
    /// </summary>
    public void CancelPasswordPrompt()
    {
        VerifyAccess();
        HidePasswordPrompt();
    }

    private void HidePasswordPrompt()
    {
        SetAndRaise(IsPasswordPromptVisibleProperty, ref _isPasswordPromptVisible, false);
        PseudoClasses.Set(":password", false);
    }

    private void ResetPasswordPrompt()
    {
        _passwordRetry = null;
        HidePasswordPrompt();
    }

    private void ReleaseStreamSource()
    {
        _streamSource?.Dispose();
        _streamSource = null;
    }

    private void PasswordBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { SubmitPassword(_passwordBox?.Text); e.Handled = true; }
        else if (e.Key == Key.Escape) { CancelPasswordPrompt(); e.Handled = true; }
    }

    public void CloseDocument()
    {
        VerifyAccess();
        if (_disposed) return;
        if (IsDirty || !_editTail.IsCompleted) Observe(RequestCloseDocumentAsync());
        else
        {
            BeginDocumentChange(CancellationToken.None);
            CloseDocumentCore();
        }
    }

    private void CloseDocumentCore()
    {
        VerifyAccess();
        if (_disposed) return;
        var version = ++_loadVersion;
        _loadCancellation?.Cancel();
        ReleaseStreamSource();
        ResetPasswordPrompt();
        ClearDocument();
        if (version != _loadVersion || _disposed) return;
        SetSourceInternally(null);
        if (version != _loadVersion || _disposed) return;
        SetDocumentSourceInternally(null);
        if (version != _loadVersion || _disposed) return;
        SetAndRaise(IsLoadingProperty, ref _isLoading, false);
        if (version != _loadVersion || _disposed) return;
        SetAndRaise(ErrorMessageProperty, ref _errorMessage, null);
        SetContractError(null);
        if (version != _loadVersion || _disposed) return;
        PseudoClasses.Set(":loading", false);
        PseudoClasses.Set(":error", false);
        UpdateStatus();
        RefreshContractState();
    }

    /// <summary>Clears the current UI immediately, then waits for its canceled loads and native releases.</summary>
    public async Task CloseDocumentAsync()
    {
        VerifyAccess();
        // PERF01: the Release span is the UI-visible close latency; awaiting the returned task
        // additionally covers the asynchronous native teardown (see the Release work items).
        if (PdfPerformanceSession.Current is { } perf)
        {
            perf.Mark(PdfPerformanceSession.CloseRequestedMark);
            var closeStarted = PdfPerformanceSession.Now;
            await RequestCloseDocumentAsync();
            perf.Record(perf.NextCorrelationId("close"), PdfPerformanceStage.Release, closeStarted, PdfPerformanceSession.Now);
        }
        else
        {
            await RequestCloseDocumentAsync();
        }
        await AwaitDocumentReleasesAsync(_pendingLoads.ToArray());
    }

    private void RetireDocument(PdfDocument? document)
    {
        if (document is null) return;
        document.Dispose();
        _documentReleases.RemoveWhere(task => task.IsCompletedSuccessfully);
        _documentReleases.Add(document.WhenDisposed);
    }

    private async Task AwaitDocumentReleasesAsync(Task[] loads)
    {
        // Loading failures are already observable through their public tasks/events.
        // Every load's finally retires any abandoned native document before completing.
        try { await Task.WhenAll(loads); }
        catch (Exception) { }
        await Task.WhenAll(_documentReleases.ToArray());
        _documentReleases.RemoveWhere(task => task.IsCompletedSuccessfully);
    }

    private void ClearDocument()
    {
        ++_navigationVersion;
        PendingContentFit = Task.CompletedTask;
        var version = _loadVersion;
        _searchTimer.Stop();
        _searchCancellation?.Cancel();
        _documentCancellation.Cancel();
        _documentCancellation.Dispose();
        _documentCancellation = new CancellationTokenSource();
        CancelThumbnailWork();
        _presenter?.SetDocument(null);
        var old = _document;
        _document = null;
        RetireDocument(old);
        ResetEditing();
        if (version != _loadVersion || _disposed) return;
        foreach (var thumbnail in _thumbnails) thumbnail.Image = null;
        foreach (var cached in _thumbnailCache.Values) cached.Bitmap.Dispose();
        _thumbnailCache.Clear();
        _thumbnailPending.Clear();
        _thumbnailFailures.Clear();
        _navigationPage = _pendingNavigationPage = null;
        _navigationOffset = null;
        _pendingSearchTarget = null;
        _pendingViewportAnchor = null;
        _pendingLinkTarget = null;
        SetAndRaise(HasDocumentProperty, ref _hasDocument, false);
        if (version != _loadVersion || _disposed) return;
        SetAndRaise(PageCountProperty, ref _pageCount, 0);
        if (version != _loadVersion || _disposed) return;
        CoerceValue(CurrentPageProperty);
        SetCurrentPage(0);
        SetAndRaise(ThumbnailsProperty, ref _thumbnails, []);
        if (version != _loadVersion || _disposed) return;
        SetAndRaise(OutlineProperty, ref _outline, []);
        if (version != _loadVersion || _disposed) return;
        SetAndRaise(PageLabelsProperty, ref _pageLabels, []);
        if (version != _loadVersion || _disposed) return;
        SetAndRaise(CurrentPageLabelProperty, ref _currentPageLabel, "");
        ClearUserBookmarks();
        if (version != _loadVersion || _disposed) return;
        SetAndRaise(DocumentNameProperty, ref _documentName, "");
        if (version != _loadVersion || _disposed) return;
        SetSelectedText("");
        SetSelectionRangeState(false);
        if (version != _loadVersion || _disposed) return;
        ClearSearchResults();
        if (_thumbnailsList is not null) _thumbnailsList.ItemsSource = Thumbnails;
        if (_outlineTree is not null) _outlineTree.ItemsSource = Outline;
        if (_scroll is not null) _scroll.Offset = default;
        PseudoClasses.Set(":document", false);
        UpdateParts();
        RefreshContractState();
    }

    private void SetSourceInternally(string? source)
    {
        _updatingSource = true;
        try { SetCurrentValue(SourceProperty, source); }
        finally { _updatingSource = false; }
    }

    private void CancelThumbnailWork()
    {
        ++_thumbnailGeneration;
        _thumbnailPending.Clear();
        _thumbnailCancellation?.Cancel();
        _thumbnailCancellation?.Dispose();
        _thumbnailCancellation = null;
    }

    private void RequestVisibleThumbnails()
    {
        if (_thumbnailsList is null) return;
        foreach (var container in _thumbnailsList.GetRealizedContainers())
            RequestThumbnail(_thumbnailsList.IndexFromContainer(container));
    }

    private void RequestThumbnail(int page)
    {
        if (!IsPresentationActive || !IsSidebarVisible || _document is null || page < 0 || page >= _thumbnails.Count) return;
        if (_thumbnailCache.TryGetValue(page, out var cached))
        {
            _thumbnailCache[page] = (cached.Bitmap, cached.Bytes, ++_thumbnailStamp);
            return;
        }
        if (_thumbnailFailures.Contains(page) || _thumbnailPending.Contains(page) || _thumbnailPending.Count >= 12) return;
        if (_thumbnailCache.Count > 0 && ThumbnailCacheBytes + _thumbnailReservation > EffectiveThumbnailCacheBudget) return;
        _thumbnailCancellation ??= CancellationTokenSource.CreateLinkedTokenSource(_documentCancellation.Token);
        _thumbnailPending.Add(page);
        Observe(RenderThumbnailAsync(_document, page, _thumbnailGeneration, _thumbnailCancellation.Token));
    }

    private async Task RenderThumbnailAsync(PdfDocument document, int pageIndex, long generation, CancellationToken token)
    {
        try
        {
            var page = document.Pages[pageIndex];
            var scale = 128 / Math.Max(page.Width, page.Height);
            var decoded = await document.RenderThumbnailAsync(pageIndex, Math.Max(1, (int)(page.Width * scale)),
                Math.Max(1, (int)(page.Height * scale)), cancellationToken: token);
            token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(document, _document) || generation != _thumbnailGeneration || _disposed) return;
            var bitmap = PdfDocumentPresenter.CreateBitmap(decoded);
            var bytes = (long)decoded.Width * decoded.Height * 4;
            if (bytes > _thumbnailReservation) _thumbnailReservation = bytes;
            _thumbnailCache[pageIndex] = (bitmap, bytes, ++_thumbnailStamp);
            _thumbnails[pageIndex].Image = bitmap;
            TrimThumbnailCache(pageIndex);
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) when (token.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (!token.IsCancellationRequested && ReferenceEquals(document, _document))
            {
                _thumbnailFailures.Add(pageIndex);
                ReportError(error);
            }
        }
        finally
        {
            if (ReferenceEquals(document, _document) && generation == _thumbnailGeneration)
            {
                _thumbnailPending.Remove(pageIndex);
                if (!token.IsCancellationRequested) RequestVisibleThumbnails();
            }
        }
    }

    // Thumbnail retention hysteresis, matching the page cache: shed to the low-water mark only
    // after crossing the effective budget, plus an entry-count safety valve for tiny pages.
    private void TrimThumbnailCache(int protectPage)
    {
        const int maxCachedThumbnails = 1024;
        if (ThumbnailCacheBytes <= EffectiveThumbnailCacheBudget && _thumbnailCache.Count <= maxCachedThumbnails) return;
        var lowWater = (long)(EffectiveThumbnailCacheBudget * PdfCacheGovernor.TrimWaterRatio);
        while (ThumbnailCacheBytes > lowWater || _thumbnailCache.Count > maxCachedThumbnails)
        {
            var victim = _thumbnailCache.Where(pair => pair.Key != protectPage).MinBy(pair => pair.Value.Stamp);
            if (victim.Value.Bitmap is null) break;
            _thumbnails[victim.Key].Image = null;
            victim.Value.Bitmap.Dispose();
            _thumbnailCache.Remove(victim.Key);
        }
    }

    /// <summary>Waits for the current visible-page render pass; the viewer must be attached and laid out.</summary>
    public Task WaitForIdleAsync(CancellationToken cancellationToken = default)
    {
        VerifyAccess();
        return _presenter?.WaitForIdleAsync(cancellationToken) ?? Task.CompletedTask;
    }

    public void Dispose()
    {
        VerifyAccess();
        if (_disposed) return;
        var disposeStarted = PdfPerformanceSession.Now;
        if (PdfPerformanceSession.Current is { } perf)
            perf.Mark(PdfPerformanceSession.CloseRequestedMark);
        BeginDocumentChange(CancellationToken.None);
        CloseDocumentCore();
        _disposed = true;
        PdfCacheGovernor.Unregister(this);
        _loadCancellation?.Cancel();
        SetAndRaise(IsLoadingProperty, ref _isLoading, false);
        RefreshContractState();
        DisconnectTemplate();
        DisconnectVisibilityAncestors();
        _searchTimer.Stop();
        _autoSaveTimer.Stop();
        _changeRequestCancellation?.Cancel();
        _changeRequestCancellation?.Dispose();
        _loadCancellation?.Dispose();
        _searchCancellation?.Dispose();
        _documentCancellation.Cancel();
        _documentCancellation.Dispose();
        GC.SuppressFinalize(this);
        // PERF01: UI-side dispose latency; native teardown continues asynchronously and is
        // recorded through the Release work items of the scheduler.
        if (PdfPerformanceSession.Current is { } releasePerf)
            releasePerf.Record(releasePerf.NextCorrelationId("close"), PdfPerformanceStage.Release,
                disposeStarted, PdfPerformanceSession.Now);
    }

    /// <summary>Begins disposal on the UI thread and asynchronously waits for all owned native documents.</summary>
    public ValueTask DisposeAsync()
    {
        VerifyAccess();
        Dispose();
        return new ValueTask(_disposeCompletion ??= AwaitDocumentReleasesAsync(_pendingLoads.ToArray()));
    }
}
