using System.Windows.Input;
using Avalonia;

namespace NT.AvaloniaControl.PdfViewer;

public partial class PdfViewer
{
    public static readonly StyledProperty<PdfDocumentSource?> DocumentSourceProperty = AvaloniaProperty.Register<PdfViewer, PdfDocumentSource?>(nameof(DocumentSource));
    public static readonly StyledProperty<bool> IsReadOnlyProperty = AvaloniaProperty.Register<PdfViewer, bool>(nameof(IsReadOnly), true);
    public static readonly StyledProperty<bool> AllowCopyProperty = AvaloniaProperty.Register<PdfViewer, bool>(nameof(AllowCopy), true);
    public static readonly StyledProperty<bool> AllowSearchProperty = AvaloniaProperty.Register<PdfViewer, bool>(nameof(AllowSearch), true);
    public static readonly StyledProperty<bool> AllowAnnotationsProperty = AvaloniaProperty.Register<PdfViewer, bool>(nameof(AllowAnnotations), true);
    public static readonly StyledProperty<bool> AllowFormsProperty = AvaloniaProperty.Register<PdfViewer, bool>(nameof(AllowForms), true);
    public static readonly StyledProperty<bool> AllowPrintingProperty = AvaloniaProperty.Register<PdfViewer, bool>(nameof(AllowPrinting), true);
    public static readonly DirectProperty<PdfViewer, PdfDocumentMetadata?> MetadataProperty = AvaloniaProperty.RegisterDirect<PdfViewer, PdfDocumentMetadata?>(nameof(Metadata), c => c.Metadata);
    public static readonly DirectProperty<PdfViewer, PdfDocumentPermissions?> PermissionsProperty = AvaloniaProperty.RegisterDirect<PdfViewer, PdfDocumentPermissions?>(nameof(Permissions), c => c.Permissions);
    public static readonly DirectProperty<PdfViewer, PdfDocumentCapabilities> CapabilitiesProperty = AvaloniaProperty.RegisterDirect<PdfViewer, PdfDocumentCapabilities>(nameof(Capabilities), c => c.Capabilities);
    public static readonly DirectProperty<PdfViewer, PdfError?> LastErrorProperty = AvaloniaProperty.RegisterDirect<PdfViewer, PdfError?>(nameof(LastError), c => c.LastError);
    public static readonly DirectProperty<PdfViewer, PdfViewerDocumentState> DocumentStateProperty = AvaloniaProperty.RegisterDirect<PdfViewer, PdfViewerDocumentState>(nameof(DocumentState), c => c.DocumentState);
    public static readonly DirectProperty<PdfViewer, Guid?> DocumentSessionIdProperty = AvaloniaProperty.RegisterDirect<PdfViewer, Guid?>(nameof(DocumentSessionId), c => c.DocumentSessionId);

    private PdfDocumentMetadata? _contractMetadata;
    private PdfDocumentPermissions? _contractPermissions;
    private PdfDocumentCapabilities _contractCapabilities = PdfDocumentCapabilities.Create(null, PdfCapabilityUnavailableReason.NoDocument);
    private PdfError? _contractError;
    private PdfViewerDocumentState _contractState;
    private Guid? _contractSessionId;
    private PdfDocument? _contractDocument;
    private bool _updatingDocumentSource;
    private readonly List<ViewerCommand> _contractCommands = [];

    public PdfDocumentSource? DocumentSource { get => GetValue(DocumentSourceProperty); set => SetValue(DocumentSourceProperty, value); }
    /// <summary>Host editing policy. Reading, search and copying do not depend on this switch.</summary>
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }
    public bool AllowCopy { get => GetValue(AllowCopyProperty); set => SetValue(AllowCopyProperty, value); }
    public bool AllowSearch { get => GetValue(AllowSearchProperty); set => SetValue(AllowSearchProperty, value); }
    public bool AllowAnnotations { get => GetValue(AllowAnnotationsProperty); set => SetValue(AllowAnnotationsProperty, value); }
    public bool AllowForms { get => GetValue(AllowFormsProperty); set => SetValue(AllowFormsProperty, value); }
    public bool AllowPrinting { get => GetValue(AllowPrintingProperty); set => SetValue(AllowPrintingProperty, value); }
    public PdfDocumentMetadata? Metadata => _contractMetadata;
    public PdfDocumentPermissions? Permissions => _contractPermissions;
    public PdfDocumentCapabilities Capabilities => _contractCapabilities;
    public PdfError? LastError => _contractError;
    public PdfViewerDocumentState DocumentState => _contractState;
    /// <summary>A new identifier for each successfully opened document, including reopens of the same source.</summary>
    public Guid? DocumentSessionId => _contractSessionId;
    public event EventHandler<PdfDocumentStateChangedEventArgs>? DocumentStateChanged;
    public event EventHandler<PdfDocumentLoadFailedEventArgs>? DocumentLoadFailed;
    /// <summary>
    /// Raised when a link annotation is activated. The control navigates internal destinations by
    /// default and never opens URIs or files; set Handled to suppress even the internal navigation.
    /// </summary>
    public event EventHandler<PdfLinkActivatedEventArgs>? LinkActivated;

    public ICommand OpenCommand { get; private set; } = null!;
    public ICommand CloseCommand { get; private set; } = null!;
    public ICommand NextPageCommand { get; private set; } = null!;
    public ICommand PreviousPageCommand { get; private set; } = null!;
    public ICommand ZoomInCommand { get; private set; } = null!;
    public ICommand ZoomOutCommand { get; private set; } = null!;
    public ICommand FitWidthCommand { get; private set; } = null!;
    public ICommand FitPageCommand { get; private set; } = null!;
    public ICommand FitHeightCommand { get; private set; } = null!;
    public ICommand RotateLeftCommand { get; private set; } = null!;
    public ICommand RotateRightCommand { get; private set; } = null!;
    public ICommand SearchCommand { get; private set; } = null!;
    public ICommand FindNextCommand { get; private set; } = null!;
    public ICommand FindPreviousCommand { get; private set; } = null!;
    /// <summary>Cancels any running search and clears the query, results and progress state.</summary>
    public ICommand ClearSearchCommand { get; private set; } = null!;
    public ICommand CopyCommand { get; private set; } = null!;
    public ICommand SelectAllCommand { get; private set; } = null!;
    /// <summary>Adds a session bookmark at the current page, or at the parameter's page when it is a number.</summary>
    public ICommand AddUserBookmarkCommand { get; private set; } = null!;
    /// <summary>Clears the active text selection; executable only while <see cref="IsTextSelected"/>.</summary>
    public ICommand ClearSelectionCommand { get; private set; } = null!;
    /// <summary>Removes the user bookmark given as a PdfUserBookmark or Guid parameter.</summary>
    public ICommand RemoveUserBookmarkCommand { get; private set; } = null!;

    private void InitializeContractCommands()
    {
        OpenCommand = Add(_ => Observe(OpenDocumentAsync()), _ => !_disposed && !IsLoading && FileDialogService?.CanOpen != false);
        CloseCommand = Add(_ => CloseDocument(), _ => !_disposed && (HasDocument || IsLoading));
        NextPageCommand = Add(_ => NextPage(), _ => Capabilities.CanRead && CurrentPage < PageCount);
        PreviousPageCommand = Add(_ => PreviousPage(), _ => Capabilities.CanRead && CurrentPage > 1);
        ZoomInCommand = Add(_ => ZoomIn(), _ => Capabilities.CanRead && ZoomLevel < MaximumZoom);
        ZoomOutCommand = Add(_ => ZoomOut(), _ => Capabilities.CanRead && ZoomLevel > MinimumZoom);
        FitWidthCommand = Add(_ => FitWidth(), _ => Capabilities.CanRead);
        FitPageCommand = Add(_ => FitPage(), _ => Capabilities.CanRead);
        FitHeightCommand = Add(_ => FitHeight(), _ => Capabilities.CanRead);
        RotateLeftCommand = Add(_ => RotateLeft(), _ => Capabilities.CanRead);
        RotateRightCommand = Add(_ => RotateRight(), _ => Capabilities.CanRead);
        SearchCommand = Add(parameter => Observe(SearchAsync(parameter as string ?? SearchQuery)),
            parameter => Capabilities.CanSearch && !IsSearching && !string.IsNullOrWhiteSpace(parameter as string ?? SearchQuery));
        FindNextCommand = Add(_ => FindNext(), _ => Capabilities.CanSearch && !string.IsNullOrWhiteSpace(SearchQuery));
        FindPreviousCommand = Add(_ => FindPrevious(), _ => Capabilities.CanSearch && !string.IsNullOrWhiteSpace(SearchQuery));
        ClearSearchCommand = Add(_ => ClearSearch(), _ => !_disposed && (!string.IsNullOrWhiteSpace(SearchQuery) || SearchResultCount > 0 || IsSearching));
        CopyCommand = Add(_ => Observe(CopySelectionToClipboardAsync()), _ => Capabilities.CanCopy && AllowTextSelection);
        SelectAllCommand = Add(_ => Observe(SelectAllAsync()), _ => Capabilities.CanCopy && AllowTextSelection);
        ClearSelectionCommand = Add(_ => ClearSelection(), _ => !_disposed && IsTextSelected);
        AddUserBookmarkCommand = Add(parameter =>
        {
            var page = parameter is int number ? number : (int?)null;
            AddUserBookmark(pageNumber: page);
        }, _ => Capabilities.CanRead);
        RemoveUserBookmarkCommand = Add(parameter =>
        {
            var id = parameter switch
            {
                PdfUserBookmark bookmark => bookmark.Id,
                Guid guid => guid,
                _ => Guid.Empty,
            };
            if (id != Guid.Empty) RemoveUserBookmark(id);
        }, parameter => Capabilities.CanRead && parameter is PdfUserBookmark or Guid);

        ViewerCommand Add(Action<object?> execute, Func<object?, bool> canExecute)
        {
            var command = new ViewerCommand(this, execute, canExecute);
            _contractCommands.Add(command);
            return command;
        }
    }

    /// <summary>Loads a reopenable source and retains it for password changes and bindings.</summary>
    public Task LoadDocumentAsync(PdfDocumentSource source, string? password = null, CancellationToken cancellationToken = default)
    {
        VerifyAccess();
        ArgumentNullException.ThrowIfNull(source);
        SetDocumentSourceInternally(source);
        return LoadCoreAsync(source.LoadAsync, source.DisplayName, source.FilePath, password ?? Password,
            cancellationToken, source);
    }

    private void SetDocumentSourceInternally(PdfDocumentSource? source)
    {
        _updatingDocumentSource = true;
        try { SetCurrentValue(DocumentSourceProperty, source); }
        finally { _updatingDocumentSource = false; }
    }

    // Call from the owner's OnPropertyChanged after the legacy properties have been handled.
    private void HandleContractPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == DocumentSourceProperty && !_updatingDocumentSource)
        {
            if (DocumentSource is { } source) Observe(LoadDocumentAsync(source, Password));
            else CloseDocument();
        }
        if (change.Property == IsReadOnlyProperty || change.Property == AllowCopyProperty || change.Property == AllowSearchProperty ||
            change.Property == AllowAnnotationsProperty || change.Property == AllowFormsProperty || change.Property == AllowPrintingProperty ||
            change.Property == AllowTextSelectionProperty || change.Property == CurrentPageProperty || change.Property == ZoomLevelProperty ||
            change.Property == SearchQueryProperty || change.Property == IsSearchingProperty)
            RefreshContractState();
    }

    // Call after load/clear/loading/dispose state changes. This method performs no native work.
    private void RefreshContractState()
    {
        var sessionChanged = !ReferenceEquals(_contractDocument, _document);
        if (sessionChanged)
        {
            _contractDocument = _document;
            SetAndRaise(DocumentSessionIdProperty, ref _contractSessionId, _document is null ? null : Guid.NewGuid());
        }
        SetAndRaise(MetadataProperty, ref _contractMetadata, _document?.Metadata);
        SetAndRaise(PermissionsProperty, ref _contractPermissions, _document?.Permissions);
        var unavailable = _disposed ? PdfCapabilityUnavailableReason.Disposed : IsLoading ? PdfCapabilityUnavailableReason.Busy
            : _document is null ? PdfCapabilityUnavailableReason.NoDocument : PdfCapabilityUnavailableReason.None;
        SetAndRaise(CapabilitiesProperty, ref _contractCapabilities, PdfDocumentCapabilities.Create(_contractPermissions,
            unavailable, AllowCopy, AllowSearch, IsReadOnly, AllowAnnotations, AllowForms, AllowPrinting));
        var state = _disposed ? PdfViewerDocumentState.Disposed : IsLoading ? PdfViewerDocumentState.Loading
            : _document is not null ? PdfViewerDocumentState.Ready : _contractError is not null ? PdfViewerDocumentState.Error : PdfViewerDocumentState.Empty;
        var stateChanged = state != _contractState;
        SetAndRaise(DocumentStateProperty, ref _contractState, state);
        RaiseContractCommands();
        UpdateCommandParts();
        if (stateChanged || sessionChanged)
            DocumentStateChanged?.Invoke(this, new PdfDocumentStateChangedEventArgs(state, _contractSessionId));
    }

    private void RaiseContractCommands()
    {
        foreach (var command in _contractCommands) command.RaiseCanExecuteChanged();
    }

    private void SetContractError(Exception? error)
    {
        SetAndRaise(LastErrorProperty, ref _contractError, error is null ? null : PdfError.FromException(error));
        RefreshContractState();
    }

    private void RaiseContractLoadFailed(Exception error, PdfDocumentSource? attemptedSource) => DocumentLoadFailed?.Invoke(this,
        new PdfDocumentLoadFailedEventArgs(PdfError.FromException(error), attemptedSource));

    private sealed class ViewerCommand(PdfViewer owner, Action<object?> execute, Func<object?, bool> canExecute) : ICommand
    {
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => canExecute(parameter);
        public void Execute(object? parameter)
        {
            owner.VerifyAccess();
            if (CanExecute(parameter)) execute(parameter);
        }
        internal void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
