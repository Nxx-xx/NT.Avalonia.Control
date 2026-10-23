using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.PdfViewer;

/// <summary>A PDFium-backed desktop reader. Load its optional Themes package or supply a custom template.</summary>
[TemplatePart("PART_ScrollViewer", typeof(ScrollViewer), IsRequired = true)]
[TemplatePart("PART_DocumentPresenter", typeof(PdfDocumentPresenter), IsRequired = true)]
[PseudoClasses(":loading", ":document", ":error", ":searching")]
public partial class PdfViewer : TemplatedControl, IDisposable, IAsyncDisposable
{
    public static readonly StyledProperty<string?> SourceProperty = AvaloniaProperty.Register<PdfViewer, string?>(nameof(Source));
    public static readonly StyledProperty<string?> PasswordProperty = AvaloniaProperty.Register<PdfViewer, string?>(nameof(Password));
    public static readonly StyledProperty<int> CurrentPageProperty = AvaloniaProperty.Register<PdfViewer, int>(nameof(CurrentPage),
        defaultBindingMode: BindingMode.TwoWay, coerce: (owner, page) => Math.Clamp(page, ((PdfViewer)owner).PageCount > 0 ? 1 : 0, ((PdfViewer)owner).PageCount));
    public static readonly StyledProperty<double> ZoomLevelProperty = AvaloniaProperty.Register<PdfViewer, double>(nameof(ZoomLevel), 1,
        defaultBindingMode: BindingMode.TwoWay, validate: double.IsFinite,
        coerce: (owner, value) => Math.Clamp(value, ((PdfViewer)owner).MinimumZoom, ((PdfViewer)owner).MaximumZoom));
    public static readonly StyledProperty<double> MinimumZoomProperty = AvaloniaProperty.Register<PdfViewer, double>(nameof(MinimumZoom), .1,
        validate: value => double.IsFinite(value) && value > 0);
    public static readonly StyledProperty<double> MaximumZoomProperty = AvaloniaProperty.Register<PdfViewer, double>(nameof(MaximumZoom), 5,
        validate: value => double.IsFinite(value) && value > 0);
    public static readonly StyledProperty<double> ZoomStepProperty = AvaloniaProperty.Register<PdfViewer, double>(nameof(ZoomStep), 1.2,
        validate: value => double.IsFinite(value) && value > 1);
    public static readonly StyledProperty<PdfZoomMode> ZoomModeProperty = AvaloniaProperty.Register<PdfViewer, PdfZoomMode>(nameof(ZoomMode), PdfZoomMode.FitWidth,
        validate: value => Enum.IsDefined(value));
    public static readonly StyledProperty<PdfPageDisplayMode> PageDisplayModeProperty = AvaloniaProperty.Register<PdfViewer, PdfPageDisplayMode>(nameof(PageDisplayMode),
        PdfPageDisplayMode.Continuous, defaultBindingMode: BindingMode.TwoWay, validate: value => Enum.IsDefined(value));
    public static readonly StyledProperty<bool> ShowCoverPageProperty = AvaloniaProperty.Register<PdfViewer, bool>(nameof(ShowCoverPage), true,
        defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<int> RotationProperty = AvaloniaProperty.Register<PdfViewer, int>(nameof(Rotation),
        defaultBindingMode: BindingMode.TwoWay, validate: value => value % 90 == 0, coerce: (_, value) => (value % 360 + 360) % 360);
    public static readonly StyledProperty<bool> IsToolbarVisibleProperty = AvaloniaProperty.Register<PdfViewer, bool>(nameof(IsToolbarVisible), true);
    public static readonly StyledProperty<bool> IsSidebarVisibleProperty = AvaloniaProperty.Register<PdfViewer, bool>(nameof(IsSidebarVisible), true);
    public static readonly StyledProperty<bool> AllowTextSelectionProperty = AvaloniaProperty.Register<PdfViewer, bool>(nameof(AllowTextSelection), true);
    public static readonly StyledProperty<string?> SearchQueryProperty = AvaloniaProperty.Register<PdfViewer, string?>(nameof(SearchQuery), defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<bool> SearchMatchCaseProperty = AvaloniaProperty.Register<PdfViewer, bool>(nameof(SearchMatchCase));
    public static readonly StyledProperty<bool> SearchWholeWordProperty = AvaloniaProperty.Register<PdfViewer, bool>(nameof(SearchWholeWord));
    public static readonly StyledProperty<int> SearchResultLimitProperty = AvaloniaProperty.Register<PdfViewer, int>(nameof(SearchResultLimit),
        10000, coerce: (_, value) => Math.Max(1, value));
    /// <summary>Width of the sidebar area in DIPs. The default theme seeds it from the
    /// PdfViewerSidebarWidth resource (208) so hosts can scale it down for narrow windows.</summary>
    public static readonly StyledProperty<double> SidebarWidthProperty = AvaloniaProperty.Register<PdfViewer, double>(nameof(SidebarWidth),
        208, validate: value => double.IsFinite(value) && value >= 0);
    public static readonly StyledProperty<PdfSidebarPlacement> SidebarPlacementProperty = AvaloniaProperty.Register<PdfViewer, PdfSidebarPlacement>(nameof(SidebarPlacement),
        PdfSidebarPlacement.Left, validate: value => Enum.IsDefined(value));
    /// <summary>Sidebar presentation: Offset (own column, document reflows) or Overlay (floats above the full-width document).</summary>
    public static readonly StyledProperty<PdfSidebarPresentation> SidebarPresentationProperty = AvaloniaProperty.Register<PdfViewer, PdfSidebarPresentation>(nameof(SidebarPresentation),
        PdfSidebarPresentation.Offset, validate: value => Enum.IsDefined(value));
    public static readonly StyledProperty<bool> IsOpenToolVisibleProperty = AvaloniaProperty.Register<PdfViewer, bool>(nameof(IsOpenToolVisible), true);
    public static readonly StyledProperty<bool> IsSidebarToggleVisibleProperty = AvaloniaProperty.Register<PdfViewer, bool>(nameof(IsSidebarToggleVisible), true);
    public static readonly StyledProperty<bool> IsNavigationToolVisibleProperty = AvaloniaProperty.Register<PdfViewer, bool>(nameof(IsNavigationToolVisible), true);
    public static readonly StyledProperty<bool> IsZoomToolVisibleProperty = AvaloniaProperty.Register<PdfViewer, bool>(nameof(IsZoomToolVisible), true);
    public static readonly StyledProperty<bool> IsFitToolVisibleProperty = AvaloniaProperty.Register<PdfViewer, bool>(nameof(IsFitToolVisible), true);
    public static readonly StyledProperty<bool> IsRotateToolVisibleProperty = AvaloniaProperty.Register<PdfViewer, bool>(nameof(IsRotateToolVisible), true);
    public static readonly StyledProperty<bool> IsCopyToolVisibleProperty = AvaloniaProperty.Register<PdfViewer, bool>(nameof(IsCopyToolVisible), true);
    public static readonly StyledProperty<bool> IsCloseToolVisibleProperty = AvaloniaProperty.Register<PdfViewer, bool>(nameof(IsCloseToolVisible), true);
    public static readonly StyledProperty<bool> IsDisplayModeToolVisibleProperty = AvaloniaProperty.Register<PdfViewer, bool>(nameof(IsDisplayModeToolVisible), true);
    public static readonly StyledProperty<bool> IsBookmarkToolVisibleProperty = AvaloniaProperty.Register<PdfViewer, bool>(nameof(IsBookmarkToolVisible), true);
    public static readonly StyledProperty<bool> IsSearchToolVisibleProperty = AvaloniaProperty.Register<PdfViewer, bool>(nameof(IsSearchToolVisible), true);
    /// <summary>When false, every reader keyboard shortcut and the Ctrl/Cmd wheel zoom are ignored (default true).</summary>
    public static readonly StyledProperty<bool> AllowShortcutsProperty = AvaloniaProperty.Register<PdfViewer, bool>(nameof(AllowShortcuts), true);
    /// <summary>When false, a password failure is only reported; the built-in password prompt never shows (default true).</summary>
    public static readonly StyledProperty<bool> IsPasswordPromptEnabledProperty = AvaloniaProperty.Register<PdfViewer, bool>(nameof(IsPasswordPromptEnabled), true);

    /// <summary>Default retained page-bitmap budget: 128 MiB.</summary>
    public const long DefaultPageCacheBudget = 128L * 1024 * 1024;
    /// <summary>Default retained thumbnail budget: 16 MiB.</summary>
    public const long DefaultThumbnailCacheBudget = 16L * 1024 * 1024;
    /// <summary>Default retained text-page budget: 32 MiB.</summary>
    public const long DefaultTextCacheBudget = 32L * 1024 * 1024;
    /// <summary>Default pre-render depth on each side of the viewport, in layout rows.</summary>
    public const int DefaultPrerenderRows = 1;
    /// <summary>Smallest accepted cache budget; budgets coerce up to this floor.</summary>
    public const long MinimumCacheBudget = 64L * 1024;

    /// <summary>Bytes of retained final page bitmaps; at least <see cref="MinimumCacheBudget"/> (default 128 MiB).
    /// Widening it only widens retention: pages already cached are kept until the new budget is crossed.</summary>
    public static readonly StyledProperty<long> PageCacheBudgetProperty = AvaloniaProperty.Register<PdfViewer, long>(nameof(PageCacheBudget),
        DefaultPageCacheBudget, coerce: (_, value) => Math.Max(MinimumCacheBudget, value));
    /// <summary>Bytes of retained sidebar thumbnails; at least <see cref="MinimumCacheBudget"/> (default 16 MiB).</summary>
    public static readonly StyledProperty<long> ThumbnailCacheBudgetProperty = AvaloniaProperty.Register<PdfViewer, long>(nameof(ThumbnailCacheBudget),
        DefaultThumbnailCacheBudget, coerce: (_, value) => Math.Max(MinimumCacheBudget, value));
    /// <summary>Estimated bytes of retained extracted text pages; at least <see cref="MinimumCacheBudget"/> (default 32 MiB).</summary>
    public static readonly StyledProperty<long> TextCacheBudgetProperty = AvaloniaProperty.Register<PdfViewer, long>(nameof(TextCacheBudget),
        DefaultTextCacheBudget, coerce: (_, value) => Math.Max(MinimumCacheBudget, value));
    /// <summary>Whole layout rows decoded beyond the viewport on each side (default 1, clamped 0–8).
    /// A row is one page in single / continuous layouts and two pages in the two-page layouts.</summary>
    public static readonly StyledProperty<int> PrerenderRowsProperty = AvaloniaProperty.Register<PdfViewer, int>(nameof(PrerenderRows),
        DefaultPrerenderRows, coerce: (_, value) => Math.Clamp(value, 0, 8));

    /// <summary>Default largest rendered page edge: 8192 pixels.</summary>
    public const int DefaultMaxRenderDimension = 8192;
    /// <summary>Default largest rendered page raster: 30 megapixels.</summary>
    public const long DefaultMaxRenderPixels = 30_000_000;
    /// <summary>Default display-DPI cap for page rasters: 8 (effectively the true display scale).</summary>
    public const double DefaultMaxRenderScale = 8;

    /// <summary>Largest rendered page edge in pixels (default 8192, clamped 256–16384).
    /// Together with <see cref="MaxRenderPixels"/> and the cache budget this bounds the decode;
    /// defaults cover a reference renderer's 8192/30MP envelope so comparisons never win by
    /// lower resolution.</summary>
    public static readonly StyledProperty<int> MaxRenderDimensionProperty = AvaloniaProperty.Register<PdfViewer, int>(nameof(MaxRenderDimension),
        DefaultMaxRenderDimension, coerce: (_, value) => Math.Clamp(value, 256, 16384));
    /// <summary>Largest rendered page raster in pixels (default 30,000,000, clamped 65,536–67,108,864).
    /// The per-page share of <see cref="PageCacheBudget"/> may still lower the actual raster.</summary>
    public static readonly StyledProperty<long> MaxRenderPixelsProperty = AvaloniaProperty.Register<PdfViewer, long>(nameof(MaxRenderPixels),
        DefaultMaxRenderPixels, coerce: (_, value) => Math.Clamp(value, 65_536, 64L * 1024 * 1024));
    /// <summary>Cap for the display-DPI multiplier used when rasterizing pages (default 8, clamped 1–8).
    /// The effective page raster scale is clamp(RenderScaling, 1, MaxRenderScale); zoom is applied
    /// on top through the layout. Raising it above the display's own RenderScaling has no effect.</summary>
    public static readonly StyledProperty<double> MaxRenderScaleProperty = AvaloniaProperty.Register<PdfViewer, double>(nameof(MaxRenderScale),
        DefaultMaxRenderScale, validate: double.IsFinite, coerce: (_, value) => Math.Clamp(value, 1, 8));

    public static readonly DirectProperty<PdfViewer, int> PageCountProperty = AvaloniaProperty.RegisterDirect<PdfViewer, int>(nameof(PageCount), c => c.PageCount);
    public static readonly DirectProperty<PdfViewer, bool> HasDocumentProperty = AvaloniaProperty.RegisterDirect<PdfViewer, bool>(nameof(HasDocument), c => c.HasDocument);
    public static readonly DirectProperty<PdfViewer, bool> IsLoadingProperty = AvaloniaProperty.RegisterDirect<PdfViewer, bool>(nameof(IsLoading), c => c.IsLoading);
    public static readonly DirectProperty<PdfViewer, bool> IsSearchingProperty = AvaloniaProperty.RegisterDirect<PdfViewer, bool>(nameof(IsSearching), c => c.IsSearching);
    public static readonly DirectProperty<PdfViewer, string?> ErrorMessageProperty = AvaloniaProperty.RegisterDirect<PdfViewer, string?>(nameof(ErrorMessage), c => c.ErrorMessage);
    public static readonly DirectProperty<PdfViewer, string> SelectedTextProperty = AvaloniaProperty.RegisterDirect<PdfViewer, string>(nameof(SelectedText), c => c.SelectedText);
    public static readonly DirectProperty<PdfViewer, bool> IsPasswordPromptVisibleProperty = AvaloniaProperty.RegisterDirect<PdfViewer, bool>(nameof(IsPasswordPromptVisible), c => c.IsPasswordPromptVisible);
    public static readonly DirectProperty<PdfViewer, bool> IsTextSelectedProperty = AvaloniaProperty.RegisterDirect<PdfViewer, bool>(nameof(IsTextSelected), c => c.IsTextSelected);
    public static readonly DirectProperty<PdfViewer, string> DocumentNameProperty = AvaloniaProperty.RegisterDirect<PdfViewer, string>(nameof(DocumentName), c => c.DocumentName);
    public static readonly DirectProperty<PdfViewer, string> ZoomTextProperty = AvaloniaProperty.RegisterDirect<PdfViewer, string>(nameof(ZoomText), c => c.ZoomText);
    public static readonly DirectProperty<PdfViewer, string> StatusTextProperty = AvaloniaProperty.RegisterDirect<PdfViewer, string>(nameof(StatusText), c => c.StatusText);
    public static readonly DirectProperty<PdfViewer, int> SearchResultCountProperty = AvaloniaProperty.RegisterDirect<PdfViewer, int>(nameof(SearchResultCount), c => c.SearchResultCount);
    public static readonly DirectProperty<PdfViewer, int> CurrentSearchResultIndexProperty = AvaloniaProperty.RegisterDirect<PdfViewer, int>(nameof(CurrentSearchResultIndex), c => c.CurrentSearchResultIndex);
    public static readonly DirectProperty<PdfViewer, IReadOnlyList<PdfSearchResult>> SearchResultsProperty = AvaloniaProperty.RegisterDirect<PdfViewer, IReadOnlyList<PdfSearchResult>>(nameof(SearchResults), c => c.SearchResults);
    public static readonly DirectProperty<PdfViewer, double> SearchProgressProperty = AvaloniaProperty.RegisterDirect<PdfViewer, double>(nameof(SearchProgress), c => c.SearchProgress);
    public static readonly DirectProperty<PdfViewer, bool> SearchResultsTruncatedProperty = AvaloniaProperty.RegisterDirect<PdfViewer, bool>(nameof(SearchResultsTruncated), c => c.SearchResultsTruncated);
    public static readonly DirectProperty<PdfViewer, IReadOnlyList<PdfThumbnail>> ThumbnailsProperty = AvaloniaProperty.RegisterDirect<PdfViewer, IReadOnlyList<PdfThumbnail>>(nameof(Thumbnails), c => c.Thumbnails);
    public static readonly DirectProperty<PdfViewer, IReadOnlyList<PdfBookmark>> OutlineProperty = AvaloniaProperty.RegisterDirect<PdfViewer, IReadOnlyList<PdfBookmark>>(nameof(Outline), c => c.Outline);
    public static readonly DirectProperty<PdfViewer, IReadOnlyList<string>> PageLabelsProperty = AvaloniaProperty.RegisterDirect<PdfViewer, IReadOnlyList<string>>(nameof(PageLabels), c => c.PageLabels);
    public static readonly DirectProperty<PdfViewer, string> CurrentPageLabelProperty = AvaloniaProperty.RegisterDirect<PdfViewer, string>(nameof(CurrentPageLabel), c => c.CurrentPageLabel);
    public static readonly DirectProperty<PdfViewer, ObservableCollection<PdfUserBookmark>> UserBookmarksProperty = AvaloniaProperty.RegisterDirect<PdfViewer, ObservableCollection<PdfUserBookmark>>(nameof(UserBookmarks), c => c.UserBookmarks);

    private PdfDocument? _document;
    private PdfDocumentPresenter? _presenter;
    private ScrollViewer? _scroll;
    private ListBox? _thumbnailsList;
    private TreeView? _outlineTree;
    private ListBox? _userBookmarksList;
    private NumericUpDown? _pageNumber;
    private TextBox? _searchBox;
    private TextBox? _passwordBox;
    private ComboBox? _displayModeBox;
    private Grid? _bodyGrid;
    private Border? _sidebar;
    private Grid? _canvasGrid;
    private readonly List<(Button Button, EventHandler<RoutedEventArgs> Handler)> _buttons = [];
    private readonly List<Visual> _visibilityAncestors = [];
    private readonly DispatcherTimer _searchTimer;
    private bool _attached;
    private bool _disposed;
    private bool _updatingSource;
    private bool _updatingCurrent;
    private bool _updatingParts;
    private bool _fitting;
    private bool _settingSearch;
    private bool _updatingPassword;
    private bool _isPasswordPromptVisible;
    private bool _isTextSelected;
    private Size _lastFitViewport;
    private bool _isLoading;
    private bool _isSearching;
    private bool _hasDocument;
    private int _pageCount;
    private int _searchResultCount;
    private int _currentSearchResultIndex = -1;
    private double _searchProgress;
    private bool _searchResultsTruncated;
    private string? _errorMessage;
    private string _selectedText = "";
    private string _documentName = "";
    private string _zoomText = "100%";
    private string _statusText = PdfViewerStrings.StatusNoDocumentText;
    private IReadOnlyList<PdfThumbnail> _thumbnails = [];
    private IReadOnlyList<PdfBookmark> _outline = [];
    private IReadOnlyList<PdfSearchResult> _searchResults = [];
    private IReadOnlyList<string> _pageLabels = [];
    private string _currentPageLabel = "";
    private readonly ObservableCollection<PdfUserBookmark> _userBookmarks = [];

    public PdfViewer()
    {
        Focusable = true;
        _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _searchTimer.Tick += (_, _) => { _searchTimer.Stop(); Observe(SearchAsync(SearchQuery)); };
        InitializeContractCommands();
        PdfCacheGovernor.Register(this);
    }

    public string? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }
    public string? Password { get => GetValue(PasswordProperty); set => SetValue(PasswordProperty, value); }
    public int CurrentPage { get => GetValue(CurrentPageProperty); set => SetValue(CurrentPageProperty, value); }
    /// <summary>100% maps PDF points to 96 DPI. Values are clamped to MinimumZoom–MaximumZoom (default 10–500%).</summary>
    public double ZoomLevel { get => GetValue(ZoomLevelProperty); set => SetValue(ZoomLevelProperty, value); }
    /// <summary>Smallest allowed <see cref="ZoomLevel"/> (default 0.1). Raising it above MaximumZoom raises MaximumZoom too.</summary>
    public double MinimumZoom { get => GetValue(MinimumZoomProperty); set => SetValue(MinimumZoomProperty, value); }
    /// <summary>Largest allowed <see cref="ZoomLevel"/> (default 5). Lowering it below MinimumZoom lowers MinimumZoom too.</summary>
    public double MaximumZoom { get => GetValue(MaximumZoomProperty); set => SetValue(MaximumZoomProperty, value); }
    /// <summary>Multiplier used by ZoomIn / ZoomOut (default 1.2, must be greater than 1).</summary>
    public double ZoomStep { get => GetValue(ZoomStepProperty); set => SetValue(ZoomStepProperty, value); }
    public PdfZoomMode ZoomMode { get => GetValue(ZoomModeProperty); set => SetValue(ZoomModeProperty, value); }
    /// <summary>Page arrangement; see <see cref="PdfPageDisplayMode"/>. Changing it re-centers the current page or spread.</summary>
    public PdfPageDisplayMode PageDisplayMode { get => GetValue(PageDisplayModeProperty); set => SetValue(PageDisplayModeProperty, value); }
    /// <summary>In two-page modes, keeps page 1 alone as the cover instead of pairing it with page 2.</summary>
    public bool ShowCoverPage { get => GetValue(ShowCoverPageProperty); set => SetValue(ShowCoverPageProperty, value); }
    public int Rotation { get => GetValue(RotationProperty); set => SetValue(RotationProperty, value); }
    public bool IsToolbarVisible { get => GetValue(IsToolbarVisibleProperty); set => SetValue(IsToolbarVisibleProperty, value); }
    public bool IsSidebarVisible { get => GetValue(IsSidebarVisibleProperty); set => SetValue(IsSidebarVisibleProperty, value); }
    public bool AllowTextSelection { get => GetValue(AllowTextSelectionProperty); set => SetValue(AllowTextSelectionProperty, value); }
    /// <summary>Wholesale keyboard-shortcut switch: false disables every reader shortcut and the Ctrl/Cmd wheel zoom.</summary>
    public bool AllowShortcuts { get => GetValue(AllowShortcutsProperty); set => SetValue(AllowShortcutsProperty, value); }
    /// <summary>Hosts that provide their own password UI can suppress the built-in prompt with false.</summary>
    public bool IsPasswordPromptEnabled { get => GetValue(IsPasswordPromptEnabledProperty); set => SetValue(IsPasswordPromptEnabledProperty, value); }
    /// <summary>Bytes of retained final page bitmaps (default 128 MiB, minimum 64 KiB). See <see cref="PageCacheBudgetProperty"/>.</summary>
    public long PageCacheBudget { get => GetValue(PageCacheBudgetProperty); set => SetValue(PageCacheBudgetProperty, value); }
    /// <summary>Bytes of retained sidebar thumbnails (default 16 MiB, minimum 64 KiB). See <see cref="ThumbnailCacheBudgetProperty"/>.</summary>
    public long ThumbnailCacheBudget { get => GetValue(ThumbnailCacheBudgetProperty); set => SetValue(ThumbnailCacheBudgetProperty, value); }
    /// <summary>Estimated bytes of retained extracted text pages (default 32 MiB, minimum 64 KiB). See <see cref="TextCacheBudgetProperty"/>.</summary>
    public long TextCacheBudget { get => GetValue(TextCacheBudgetProperty); set => SetValue(TextCacheBudgetProperty, value); }
    /// <summary>Layout rows decoded beyond the viewport on each side (default 1, clamped 0–8). See <see cref="PrerenderRowsProperty"/>.</summary>
    public int PrerenderRows { get => GetValue(PrerenderRowsProperty); set => SetValue(PrerenderRowsProperty, value); }
    /// <summary>Largest rendered page edge in pixels (default 8192, clamped 256–16384). See <see cref="MaxRenderDimensionProperty"/>.</summary>
    public int MaxRenderDimension { get => GetValue(MaxRenderDimensionProperty); set => SetValue(MaxRenderDimensionProperty, value); }
    /// <summary>Largest rendered page raster in pixels (default 30 MP, clamped 65,536–67,108,864). See <see cref="MaxRenderPixelsProperty"/>.</summary>
    public long MaxRenderPixels { get => GetValue(MaxRenderPixelsProperty); set => SetValue(MaxRenderPixelsProperty, value); }
    /// <summary>Cap for the display-DPI multiplier when rasterizing pages (default 8, clamped 1–8). See <see cref="MaxRenderScaleProperty"/>.</summary>
    public double MaxRenderScale { get => GetValue(MaxRenderScaleProperty); set => SetValue(MaxRenderScaleProperty, value); }
    public string? SearchQuery { get => GetValue(SearchQueryProperty); set => SetValue(SearchQueryProperty, value); }
    public bool SearchMatchCase { get => GetValue(SearchMatchCaseProperty); set => SetValue(SearchMatchCaseProperty, value); }
    public bool SearchWholeWord { get => GetValue(SearchWholeWordProperty); set => SetValue(SearchWholeWordProperty, value); }
    public bool SearchMatchWholeWord { get => SearchWholeWord; set => SearchWholeWord = value; }
    /// <summary>Maximum matches a search collects; at least 1. Hitting it sets <see cref="SearchResultsTruncated"/>.</summary>
    public int SearchResultLimit { get => GetValue(SearchResultLimitProperty); set => SetValue(SearchResultLimitProperty, value); }
    /// <summary>Width of the sidebar area in DIPs (default 208). Finite and nonnegative; lower it for narrow windows.</summary>
    public double SidebarWidth { get => GetValue(SidebarWidthProperty); set => SetValue(SidebarWidthProperty, value); }
    /// <summary>Which side of the document the sidebar occupies. Honored by the default template.</summary>
    public PdfSidebarPlacement SidebarPlacement { get => GetValue(SidebarPlacementProperty); set => SetValue(SidebarPlacementProperty, value); }
    /// <summary>Offset gives the sidebar its own column; Overlay floats it over the full-width document. Honored by the default template.</summary>
    public PdfSidebarPresentation SidebarPresentation { get => GetValue(SidebarPresentationProperty); set => SetValue(SidebarPresentationProperty, value); }
    /// <summary>Shows the open-document tool in the default template (default true).</summary>
    public bool IsOpenToolVisible { get => GetValue(IsOpenToolVisibleProperty); set => SetValue(IsOpenToolVisibleProperty, value); }
    /// <summary>Shows the sidebar toggle in the default template (default true).</summary>
    public bool IsSidebarToggleVisible { get => GetValue(IsSidebarToggleVisibleProperty); set => SetValue(IsSidebarToggleVisibleProperty, value); }
    /// <summary>Shows the page navigation group (previous / page number / next) in the default template (default true).</summary>
    public bool IsNavigationToolVisible { get => GetValue(IsNavigationToolVisibleProperty); set => SetValue(IsNavigationToolVisibleProperty, value); }
    /// <summary>Shows the zoom group (zoom out / percentage / zoom in) in the default template (default true).</summary>
    public bool IsZoomToolVisible { get => GetValue(IsZoomToolVisibleProperty); set => SetValue(IsZoomToolVisibleProperty, value); }
    /// <summary>Shows the fit group (width / page / height) in the default template (default true).</summary>
    public bool IsFitToolVisible { get => GetValue(IsFitToolVisibleProperty); set => SetValue(IsFitToolVisibleProperty, value); }
    /// <summary>Shows the rotation group in the default template (default true).</summary>
    public bool IsRotateToolVisible { get => GetValue(IsRotateToolVisibleProperty); set => SetValue(IsRotateToolVisibleProperty, value); }
    /// <summary>Shows the copy tool in the default template (default true).</summary>
    public bool IsCopyToolVisible { get => GetValue(IsCopyToolVisibleProperty); set => SetValue(IsCopyToolVisibleProperty, value); }
    /// <summary>Shows the close tool in the default template (default true).</summary>
    public bool IsCloseToolVisible { get => GetValue(IsCloseToolVisibleProperty); set => SetValue(IsCloseToolVisibleProperty, value); }
    /// <summary>Shows the page-layout picker and cover-page checkbox in the default template (default true).</summary>
    public bool IsDisplayModeToolVisible { get => GetValue(IsDisplayModeToolVisibleProperty); set => SetValue(IsDisplayModeToolVisibleProperty, value); }
    /// <summary>Shows the add-user-bookmark tool in the default template (default true).</summary>
    public bool IsBookmarkToolVisible { get => GetValue(IsBookmarkToolVisibleProperty); set => SetValue(IsBookmarkToolVisibleProperty, value); }
    /// <summary>Shows the search row (search box, find buttons, match options) in the default template (default true).</summary>
    public bool IsSearchToolVisible { get => GetValue(IsSearchToolVisibleProperty); set => SetValue(IsSearchToolVisibleProperty, value); }
    public int PageCount => _pageCount;
    public bool HasDocument => _hasDocument;
    public bool IsLoading => _isLoading;
    public bool IsSearching => _isSearching;
    public string? ErrorMessage => _errorMessage;
    public string SelectedText => _selectedText;
    /// <summary>True while the built-in password prompt is offered for a password load failure.</summary>
    public bool IsPasswordPromptVisible => _isPasswordPromptVisible;
    /// <summary>True while a text selection range is active; cleared by <see cref="ClearSelection"/> or Esc.</summary>
    public bool IsTextSelected => _isTextSelected;
    public string DocumentName => _documentName;
    public string ZoomText => _zoomText;
    public string StatusText => _statusText;
    public int SearchResultCount => _searchResultCount;
    /// <summary>Zero-based active search result, or -1 when there is no active result.</summary>
    public int CurrentSearchResultIndex => _currentSearchResultIndex;
    /// <summary>The accumulated matches of the current search, published page by page while scanning.</summary>
    public IReadOnlyList<PdfSearchResult> SearchResults => _searchResults;
    /// <summary>Fraction of pages scanned by the current search, 0..1; 0 after <see cref="ClearSearch"/>.</summary>
    public double SearchProgress => _searchProgress;
    /// <summary>True when the last search stopped at <see cref="SearchResultLimit"/> and more matches may exist.</summary>
    public bool SearchResultsTruncated => _searchResultsTruncated;
    public IReadOnlyList<PdfThumbnail> Thumbnails => _thumbnails;
    public IReadOnlyList<PdfBookmark> Outline => _outline;
    /// <summary>The document's /PageLabels strings; empty entries mean the page uses its plain number.</summary>
    public IReadOnlyList<string> PageLabels => _pageLabels;
    /// <summary>The active page's label, falling back to the page number when the document defines none.</summary>
    public string CurrentPageLabel => _currentPageLabel;
    /// <summary>Session-scoped user bookmarks. Cleared when the document closes; PDF persistence is a later stage.</summary>
    public ObservableCollection<PdfUserBookmark> UserBookmarks => _userBookmarks;
    public PdfDocumentPresenter? DocumentPresenter => _presenter;
    public int RenderedPageCount => _presenter?.RenderedPageCount ?? 0;
    public int CachedPageCount => _presenter?.CachedPageCount ?? 0;
    public long CachedBytes => _presenter?.CachedBytes ?? 0;
    /// <summary>Cached pages currently holding preview-tier bitmaps (pre-render rows and pages
    /// filled while a scroll / zoom burst was still in progress). They upgrade to full quality
    /// once they are visible and the burst settles.</summary>
    public int PreviewPageCount => _presenter?.PreviewPageCount ?? 0;
    /// <summary>Cached pages whose settled raster sits below its on-screen target because the
    /// decode caps or the page cache budget forced a reduction. High-magnification quality loss
    /// is always recorded here instead of degrading silently.</summary>
    public int DegradedPageCount => _presenter?.DegradedPageCount ?? 0;

    /// <summary>
    /// Accounting snapshot across this viewer's budgeted pools, decode buffers and input.
    /// See <see cref="PdfViewerCacheStatistics"/> for what each byte field covers.
    /// </summary>
    public PdfViewerCacheStatistics CacheStatistics
    {
        get
        {
            var pageBytes = _presenter?.CachedBytes ?? 0;
            var textBytes = _presenter?.TextCacheBytes ?? 0;
            var thumbnailBytes = ThumbnailCacheBytes;
            var decodeBytes = _document?.DecodeBufferBytes ?? 0;
            var budgeted = pageBytes + thumbnailBytes + textBytes;
            return new PdfViewerCacheStatistics(
                _presenter?.CachedPageCount ?? 0,
                pageBytes,
                _thumbnailCache.Count,
                thumbnailBytes,
                _presenter?.TextCacheCount ?? 0,
                textBytes,
                decodeBytes,
                _document?.DecodeBufferPeakBytes ?? 0,
                _document?.InputBytes ?? 0,
                _document?.InputBytesServed ?? 0,
                budgeted,
                budgeted + (_document?.InputBytes ?? 0) + decodeBytes);
        }
    }

    /// <summary>Sum of every live viewer's budgeted pools (page + thumbnail + text); settable once per process.</summary>
    public static long GlobalCacheBudget
    {
        get => PdfCacheGovernor.GlobalCacheBudget;
        set => PdfCacheGovernor.GlobalCacheBudget = value;
    }

    /// <summary>Process-wide bytes currently retained under <see cref="GlobalCacheBudget"/>.</summary>
    public static long GlobalCacheBytes => PdfCacheGovernor.GlobalCacheBytes;

    internal long ThumbnailCacheBytes => _thumbnailCache.Values.Sum(entry => entry.Bytes);
    internal long BudgetedCacheBytes => CachedBytes + ThumbnailCacheBytes + (_presenter?.TextCacheBytes ?? 0);

    /// <summary>
    /// A pool's own budget, scaled down when <see cref="GlobalCacheBudget"/> leaves this viewer
    /// less than its three budgets combined. Pools that fit their share are not squeezed.
    /// </summary>
    internal long EffectivePoolBudget(long poolBudget)
    {
        var ownCombined = PageCacheBudget + ThumbnailCacheBudget + TextCacheBudget;
        var combined = PdfCacheGovernor.EffectiveCombinedBudget(ownCombined, BudgetedCacheBytes);
        return combined >= ownCombined ? poolBudget : (long)((double)poolBudget * combined / ownCombined);
    }

    internal long EffectivePageCacheBudget => EffectivePoolBudget(PageCacheBudget);
    internal long EffectiveThumbnailCacheBudget => EffectivePoolBudget(ThumbnailCacheBudget);
    internal long EffectiveTextCacheBudget => EffectivePoolBudget(TextCacheBudget);

    /// <summary>Sheds retained pages / text / thumbnails past their effective budgets.</summary>
    internal void TrimCachesToBudgets()
    {
        _presenter?.TrimCaches();
        TrimThumbnailCache(-1);
    }

    internal bool IsPresentationActive => _attached && IsEffectivelyVisible && !_disposed;
    public event EventHandler? DocumentLoaded;
    public event EventHandler? LoadError;
    /// <summary>Raised whenever <see cref="CurrentPage"/> changes, from navigation, scrolling or host sets.</summary>
    public event EventHandler? CurrentPageChanged;
    /// <summary>Raised when a search finishes scanning with its final count; never on cancel, supersede or empty queries.</summary>
    public event EventHandler<PdfSearchCompletedEventArgs>? SearchCompleted;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        DisconnectTemplate();
        base.OnApplyTemplate(e);
        if (_disposed) return;
        _scroll = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
        _presenter = e.NameScope.Find<PdfDocumentPresenter>("PART_DocumentPresenter");
        _thumbnailsList = e.NameScope.Find<ListBox>("PART_Thumbnails");
        _outlineTree = e.NameScope.Find<TreeView>("PART_Outline");
        _userBookmarksList = e.NameScope.Find<ListBox>("PART_UserBookmarks");
        _pageNumber = e.NameScope.Find<NumericUpDown>("PART_PageNumber");
        _searchBox = e.NameScope.Find<TextBox>("PART_SearchBox");
        _passwordBox = e.NameScope.Find<TextBox>("PART_PasswordBox");
        _displayModeBox = e.NameScope.Find<ComboBox>("PART_DisplayMode");
        _bodyGrid = e.NameScope.Find<Grid>("PART_BodyGrid");
        _sidebar = e.NameScope.Find<Border>("PART_Sidebar");
        _canvasGrid = e.NameScope.Find<Grid>("PART_CanvasGrid");
        UpdateSidebarLayout();
        if (_presenter is not null) { _presenter.Owner = this; _presenter.SetDocument(_document); }
        if (_scroll is not null)
        {
            _scroll.ScrollChanged += ScrollChanged; _scroll.SizeChanged += ScrollSizeChanged;
            _scroll.LayoutUpdated += ScrollLayoutUpdated;
        }
        if (_thumbnailsList is not null)
        {
            _thumbnailsList.ItemsSource = Thumbnails;
            _thumbnailsList.SelectionChanged += ThumbnailSelected;
            _thumbnailsList.ContainerPrepared += ThumbnailPrepared;
        }
        if (_outlineTree is not null) { _outlineTree.ItemsSource = Outline; _outlineTree.SelectionChanged += OutlineSelected; }
        if (_userBookmarksList is not null)
        {
            _userBookmarksList.ItemsSource = UserBookmarks;
            _userBookmarksList.SelectionChanged += UserBookmarkSelected;
        }
        if (_pageNumber is not null) _pageNumber.ValueChanged += PageNumberChanged;
        if (_searchBox is not null) _searchBox.TextChanged += SearchTextChanged;
        if (_passwordBox is not null) _passwordBox.KeyDown += PasswordBoxKeyDown;
        if (_displayModeBox is not null) _displayModeBox.SelectionChanged += DisplayModeChanged;
        Hook("PART_Open", () => Observe(OpenDocumentAsync()));
        Hook("PART_Close", CloseDocument);
        Hook("PART_PreviousPage", PreviousPage);
        Hook("PART_NextPage", NextPage);
        Hook("PART_ZoomIn", ZoomIn);
        Hook("PART_ZoomOut", ZoomOut);
        Hook("PART_FitWidth", FitWidth);
        Hook("PART_FitPage", FitPage);
        Hook("PART_FitHeight", FitHeight);
        Hook("PART_RotateLeft", RotateLeft);
        Hook("PART_RotateRight", RotateRight);
        Hook("PART_FindNext", FindNext);
        Hook("PART_FindPrevious", FindPrevious);
        Hook("PART_Copy", () => Observe(CopySelectionToClipboardAsync()));
        Hook("PART_AddUserBookmark", () => AddUserBookmark());
        Hook("PART_RemoveUserBookmark", RemoveSelectedUserBookmark);
        Hook("PART_PasswordSubmit", () => SubmitPassword(_passwordBox?.Text));
        Hook("PART_PasswordCancel", CancelPasswordPrompt);
        UpdateParts();
        UpdateCommandParts();
        RefreshViewport();
        Dispatcher.UIThread.Post(() => { if (!_disposed) GoToPage(CurrentPage); }, DispatcherPriority.Loaded);

        void Hook(string name, Action action)
        {
            if (e.NameScope.Find<Button>(name) is not { } button) return;
            EventHandler<RoutedEventArgs> handler = (_, _) => action();
            button.Click += handler;
            _buttons.Add((button, handler));
        }
    }

    private void DisconnectTemplate()
    {
        foreach (var (button, handler) in _buttons) button.Click -= handler;
        _buttons.Clear();
        if (_scroll is not null)
        {
            _scroll.ScrollChanged -= ScrollChanged; _scroll.SizeChanged -= ScrollSizeChanged;
            _scroll.LayoutUpdated -= ScrollLayoutUpdated;
        }
        if (_thumbnailsList is not null)
        {
            _thumbnailsList.SelectionChanged -= ThumbnailSelected;
            _thumbnailsList.ContainerPrepared -= ThumbnailPrepared;
            _thumbnailsList.ItemsSource = null;
        }
        if (_outlineTree is not null) { _outlineTree.SelectionChanged -= OutlineSelected; _outlineTree.ItemsSource = null; }
        if (_userBookmarksList is not null) { _userBookmarksList.SelectionChanged -= UserBookmarkSelected; _userBookmarksList.ItemsSource = null; }
        if (_pageNumber is not null) _pageNumber.ValueChanged -= PageNumberChanged;
        if (_searchBox is not null) _searchBox.TextChanged -= SearchTextChanged;
        if (_passwordBox is not null) _passwordBox.KeyDown -= PasswordBoxKeyDown;
        if (_displayModeBox is not null) _displayModeBox.SelectionChanged -= DisplayModeChanged;
        if (_presenter is not null) { _presenter.SetDocument(null); _presenter.Owner = null; }
        _scroll = null; _presenter = null; _thumbnailsList = null; _outlineTree = null; _userBookmarksList = null;
        _pageNumber = null; _searchBox = null; _passwordBox = null; _displayModeBox = null;
        _bodyGrid = null; _sidebar = null; _canvasGrid = null;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        foreach (var ancestor in this.GetVisualAncestors())
        {
            _visibilityAncestors.Add(ancestor);
            ancestor.PropertyChanged += AncestorVisibilityChanged;
        }
        RefreshViewport();
        RequestVisibleThumbnails();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _attached = false;
        DisconnectVisibilityAncestors();
        _presenter?.Pause();
        _searchTimer.Stop();
        CancelThumbnailWork();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (_disposed) return;
        if (change.Property == SourceProperty && !_updatingSource)
        {
            if (string.IsNullOrWhiteSpace(Source)) CloseDocument();
            else Observe(LoadDocumentAsync(Source, Password));
        }
        else if (change.Property == PasswordProperty && !_updatingSource && !_updatingPassword)
        {
            if (DocumentSource is { } documentSource) Observe(LoadDocumentAsync(documentSource, Password));
            else if (!string.IsNullOrWhiteSpace(Source)) Observe(LoadDocumentAsync(Source, Password));
        }
        else if (change.Property == CurrentPageProperty)
        {
            // Raise for host sets too: SetCurrentPage already updated the value before GoToPage.
            if (!_updatingCurrent) GoToPage(CurrentPage);
            CurrentPageChanged?.Invoke(this, EventArgs.Empty);
        }
        else if (change.Property == ZoomLevelProperty || change.Property == RotationProperty || change.Property == ZoomModeProperty)
        {
            var anchor = !_fitting && change.Property != ZoomModeProperty
                ? _zoomAnchorOverride is { } origin ? CaptureAnchorAt(origin, origin - (_scroll?.Offset ?? default)) : CaptureViewportAnchor()
                : null;
            if (change.Property == ZoomLevelProperty && !_fitting)
            {
                _fitting = true;
                try { SetCurrentValue(ZoomModeProperty, PdfZoomMode.Manual); }
                finally { _fitting = false; }
            }
            SetAndRaise(ZoomTextProperty, ref _zoomText, $"{ZoomLevel:P0}");
            if (!_fitting)
            {
                RecomputeFit();
                _presenter?.RebuildLayout();
                if (anchor is { } position) RestoreViewportAnchor(position);
                else GoToPage(CurrentPage);
            }
            UpdateStatus();
        }
        else if (change.Property == MinimumZoomProperty || change.Property == MaximumZoomProperty)
        {
            // Keep MinimumZoom <= MaximumZoom: the most recently changed limit wins.
            if (MinimumZoom > MaximumZoom)
            {
                if (change.Property == MinimumZoomProperty) SetCurrentValue(MaximumZoomProperty, MinimumZoom);
                else SetCurrentValue(MinimumZoomProperty, MaximumZoom);
            }
            if (ZoomMode == PdfZoomMode.Manual)
            {
                if (ZoomLevel < MinimumZoom || ZoomLevel > MaximumZoom)
                {
                    // Clamp without flipping an active fit mode into manual.
                    _fitting = true;
                    try { SetCurrentValue(ZoomLevelProperty, Math.Clamp(ZoomLevel, MinimumZoom, MaximumZoom)); }
                    finally { _fitting = false; }
                    _presenter?.RebuildLayout();
                    GoToPage(CurrentPage);
                }
            }
            else RecomputeFit();
            UpdateStatus();
        }
        else if (change.Property == PageCacheBudgetProperty || change.Property == ThumbnailCacheBudgetProperty ||
                 change.Property == TextCacheBudgetProperty || change.Property == PrerenderRowsProperty)
        {
            // Shrinking a budget (or the global quota) sheds retention to the low-water mark;
            // PrerenderRows also re-issues the render pass with the wider candidate set.
            TrimCachesToBudgets();
            // Raising a thumbnail budget unblocks admission-controlled sidebar thumbnails.
            RequestVisibleThumbnails();
        }
        else if (change.Property == MaxRenderDimensionProperty || change.Property == MaxRenderPixelsProperty ||
                 change.Property == MaxRenderScaleProperty)
        {
            // Quality caps changed: re-issue the render pass so visible pages re-rasterize
            // (or shed resolution) under the new ceiling immediately.
            _presenter?.RefreshRenderQuality();
        }
        else if (change.Property == PageDisplayModeProperty || change.Property == ShowCoverPageProperty)
        {
            _presenter?.RebuildLayout();
            if (!_updatingParts && _displayModeBox is not null && _displayModeBox.SelectedIndex != (int)PageDisplayMode)
            {
                _updatingParts = true;
                try { _displayModeBox.SelectedIndex = (int)PageDisplayMode; }
                finally { _updatingParts = false; }
            }
            GoToPage(CurrentPage);
            UpdateStatus();
        }
        else if (change.Property == SearchQueryProperty || change.Property == SearchMatchCaseProperty || change.Property == SearchWholeWordProperty)
        {
            if (!_settingSearch)
            {
                _searchCancellation?.Cancel();
                _searchTimer.Stop();
                if (Capabilities.CanSearch) _searchTimer.Start();
            }
        }
        else if (change.Property == AllowTextSelectionProperty && !AllowTextSelection) _presenter?.ClearSelection();
        else if (change.Property == IsVisibleProperty)
        {
            if (IsVisible) { RefreshViewport(); RequestVisibleThumbnails(); }
            else { _presenter?.Pause(); CancelThumbnailWork(); }
        }
        else if (change.Property == IsSidebarVisibleProperty)
        {
            // Closing the sidebar releases its nonessential decode work; reopening restores the
            // selection state and resumes thumbnails.
            if (IsSidebarVisible) { RequestVisibleThumbnails(); UpdateParts(); }
            else CancelThumbnailWork();
        }
        else if (change.Property == SidebarPlacementProperty || change.Property == SidebarPresentationProperty) UpdateSidebarLayout();
        HandleContractPropertyChanged(change);
        if (change.Property == AllowCopyProperty || change.Property == AllowTextSelectionProperty)
        {
            if (!CanSelectText) _presenter?.ClearSelection();
        }
        if (change.Property == AllowSearchProperty && !AllowSearch)
        {
            _searchTimer.Stop();
            _searchCancellation?.Cancel();
            ClearSearchResults();
        }
        if (change.Property == FileDialogServiceProperty || change.Property == PrintServiceProperty || change.Property == ShareServiceProperty)
            RefreshContractState();
        UpdateCommandParts();
    }

    internal bool CanSelectText => AllowTextSelection && Capabilities.CanCopy;

    private void AncestorVisibilityChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == IsVisibleProperty)
            Dispatcher.UIThread.Post(RefreshPresentationVisibility);
    }

    private void RefreshPresentationVisibility()
    {
        if (_disposed) return;
        if (IsPresentationActive) { RefreshViewport(); RequestVisibleThumbnails(); }
        else { _presenter?.Pause(); CancelThumbnailWork(); }
    }

    private void DisconnectVisibilityAncestors()
    {
        foreach (var ancestor in _visibilityAncestors) ancestor.PropertyChanged -= AncestorVisibilityChanged;
        _visibilityAncestors.Clear();
    }

    private void UpdateSidebarLayout()
    {
        // The default template names PART_BodyGrid / PART_Sidebar / PART_CanvasGrid; custom
        // templates without them keep their own sidebar arrangement.
        if (_bodyGrid is null || _sidebar is null) return;
        var right = SidebarPlacement == PdfSidebarPlacement.Right;
        _sidebar.BorderThickness = right ? new Thickness(1, 0, 0, 0) : new Thickness(0, 0, 1, 0);
        if (SidebarPresentation == PdfSidebarPresentation.Overlay)
        {
            // The sidebar floats above the document, which keeps the whole body width.
            _bodyGrid.ColumnDefinitions = new ColumnDefinitions("*");
            Grid.SetColumn(_sidebar, 0);
            if (_canvasGrid is not null) Grid.SetColumn(_canvasGrid, 0);
            _sidebar.HorizontalAlignment = right ? Avalonia.Layout.HorizontalAlignment.Right : Avalonia.Layout.HorizontalAlignment.Left;
            _sidebar.ZIndex = 1;
        }
        else
        {
            _bodyGrid.ColumnDefinitions = right ? new ColumnDefinitions("*,Auto") : new ColumnDefinitions("Auto,*");
            Grid.SetColumn(_sidebar, right ? 1 : 0);
            if (_canvasGrid is not null) Grid.SetColumn(_canvasGrid, right ? 0 : 1);
            _sidebar.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            _sidebar.ZIndex = 0;
        }
    }

    private void UpdateCommandParts()
    {
        if (_searchBox is not null) _searchBox.IsEnabled = Capabilities.CanSearch;
        foreach (var (button, _) in _buttons)
        {
            var command = button.Name switch
            {
                "PART_Open" => OpenCommand, "PART_Close" => CloseCommand,
                "PART_PreviousPage" => PreviousPageCommand, "PART_NextPage" => NextPageCommand,
                "PART_ZoomIn" => ZoomInCommand, "PART_ZoomOut" => ZoomOutCommand,
                "PART_FitWidth" => FitWidthCommand, "PART_FitPage" => FitPageCommand,
                "PART_FitHeight" => FitHeightCommand,
                "PART_RotateLeft" => RotateLeftCommand, "PART_RotateRight" => RotateRightCommand,
                "PART_FindNext" => FindNextCommand, "PART_FindPrevious" => FindPreviousCommand,
                "PART_Copy" => CopyCommand, "PART_AddUserBookmark" => AddUserBookmarkCommand,
                "PART_RemoveUserBookmark" => RemoveUserBookmarkCommand, _ => null,
            };
            if (command is not null) button.IsEnabled = command.CanExecute(null);
        }
    }

    private void ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (!_updatingCurrent && _pendingNavigationPage is null && _navigationOffset is { } expected &&
            _scroll is not null && (_scroll.Offset - expected).Length > 1)
        {
            _navigationPage = null;
            _navigationOffset = null;
        }
        RefreshViewport();
    }
    private void ScrollSizeChanged(object? sender, SizeChangedEventArgs e) => RefreshViewport();
    private void ScrollLayoutUpdated(object? sender, EventArgs e) => TryApplyNavigation();

    private void RefreshViewport()
    {
        if (_disposed || _scroll is null || _presenter is null) return;
        var size = _scroll.Viewport;
        if (size.Width <= 0 || size.Height <= 0) size = _scroll.Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0) return;
        if (size != _lastFitViewport)
        {
            _lastFitViewport = size;
            RecomputeFit();
        }
        _presenter.Refresh(new Rect(_scroll.Offset.X, _scroll.Offset.Y, size.Width, size.Height));
        TryApplyNavigation();
        if (!_updatingCurrent && _pendingNavigationPage is null && _navigationPage is null && HasDocument)
        {
            var page = _presenter.PageAt(_scroll.Offset.Y + Math.Min(30, size.Height / 3));
            if (page >= 0) SetCurrentPage(page + 1);
        }
        UpdateParts();
        RequestThumbnail(Math.Max(0, CurrentPage - 1));
    }

    private void RecomputeFit()
    {
        if (_fitting || _document is null || _scroll is null || ZoomMode == PdfZoomMode.Manual) return;
        var size = _scroll.Viewport;
        if (size.Width <= 60 || size.Height <= 60) size = _scroll.Bounds.Size;
        if (size.Width <= 60 || size.Height <= 60) return;
        var index = Math.Clamp(CurrentPage - 1, 0, PageCount - 1);
        var page = _document.Pages[index];
        var rotated = Rotation is 90 or 270;
        double DisplayWidth(PdfPageInfo info) => rotated ? info.Height : info.Width;
        double DisplayHeight(PdfPageInfo info) => rotated ? info.Width : info.Height;
        double width;
        double height;
        var horizontalMargins = PdfPageLayout.Gap * 2 + 4;
        if (ZoomMode == PdfZoomMode.FitWidth)
        {
            if (PageDisplayMode == PdfPageDisplayMode.SinglePage)
            {
                width = DisplayWidth(page);
            }
            else if (IsTwoPageMode)
            {
                var spread = PdfPageLayout.SpreadPages(index, PageCount, ShowCoverPage);
                width = spread.Sum(i => DisplayWidth(_document.Pages[i]));
                if (spread.Length > 1) horizontalMargins += PdfPageLayout.SpreadGap;
            }
            else
            {
                width = _document.Pages.Max(DisplayWidth);
            }
            height = DisplayHeight(page);
        }
        else
        {
            // FitPage and FitHeight size the current page (or the whole visible spread in two-page modes).
            width = DisplayWidth(page);
            height = DisplayHeight(page);
            if (IsTwoPageMode)
            {
                var spread = PdfPageLayout.SpreadPages(index, PageCount, ShowCoverPage);
                width = spread.Max(i => DisplayWidth(_document.Pages[i])) * (spread.Length > 1 ? 2 : 1);
                height = spread.Max(i => DisplayHeight(_document.Pages[i]));
                if (spread.Length > 1) horizontalMargins += PdfPageLayout.SpreadGap;
            }
        }
        width *= 96d / 72;
        height *= 96d / 72;
        var zoom = ZoomMode switch
        {
            PdfZoomMode.FitWidth => (size.Width - horizontalMargins) / width,
            PdfZoomMode.FitHeight => (size.Height - PdfPageLayout.Gap * 2) / height,
            // FitPage stays inside both dimensions.
            _ => Math.Min((size.Width - horizontalMargins) / width, (size.Height - PdfPageLayout.Gap * 2) / height),
        };
        zoom = Math.Clamp(zoom, MinimumZoom, MaximumZoom);
        if (Math.Abs(zoom - ZoomLevel) < .0001) return;
        _fitting = true;
        try { SetCurrentValue(ZoomLevelProperty, zoom); }
        finally { _fitting = false; }
        _presenter?.RebuildLayout();
    }

    private void SetCurrentPage(int page)
    {
        _updatingCurrent = true;
        try { SetCurrentValue(CurrentPageProperty, page); }
        finally { _updatingCurrent = false; }
        UpdateStatus();
    }

    private void UpdateParts()
    {
        _updatingParts = true;
        try
        {
            if (_pageNumber is not null)
            {
                _pageNumber.Minimum = HasDocument ? 1 : 0;
                _pageNumber.Maximum = PageCount;
                _pageNumber.Value = CurrentPage;
                _pageNumber.IsEnabled = HasDocument;
            }
            if (_thumbnailsList is not null) _thumbnailsList.SelectedIndex = CurrentPage - 1;
            if (_displayModeBox is not null) _displayModeBox.SelectedIndex = (int)PageDisplayMode;
        }
        finally { _updatingParts = false; }
        RaiseContractCommands();
        UpdateCommandParts();
    }

    private void PageNumberChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (!_updatingParts && e.NewValue is { } page) GoToPage((int)page);
    }
    private void ThumbnailSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (!_updatingParts && _thumbnailsList?.SelectedItem is PdfThumbnail thumbnail) GoToPage(thumbnail.PageNumber);
    }
    private void ThumbnailPrepared(object? sender, ContainerPreparedEventArgs e) => RequestThumbnail(e.Index);
    private void OutlineSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_outlineTree?.SelectedItem is PdfBookmark { PageIndex: >= 0 } bookmark)
        {
            if (bookmark.Destination is { } destination) NavigateToDestination(destination);
            else GoToPage(bookmark.PageIndex + 1);
        }
    }
    private void UserBookmarkSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (!_updatingParts && _userBookmarksList?.SelectedItem is PdfUserBookmark bookmark)
            GoToPage(bookmark.PageNumber);
    }
    private void DisplayModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updatingParts || _displayModeBox?.SelectedIndex is not (>= 0 and < 4)) return;
        SetCurrentValue(PageDisplayModeProperty, (PdfPageDisplayMode)_displayModeBox.SelectedIndex);
    }
    private void RemoveSelectedUserBookmark()
    {
        if (_userBookmarksList?.SelectedItem is PdfUserBookmark bookmark) RemoveUserBookmark(bookmark.Id);
    }
    private void SearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_settingSearch) SetCurrentValue(SearchQueryProperty, _searchBox?.Text);
    }

    private void UpdateStatus()
    {
        SetAndRaise(CurrentPageLabelProperty, ref _currentPageLabel, ComputeCurrentPageLabel());
        var status = IsLoading ? PdfViewerStrings.LoadingText
            : !HasDocument ? PdfViewerStrings.StatusNoDocumentText
            : StatusPageText() + $" · {ZoomText}";
        if (IsSearching) status += " · " + string.Format(PdfViewerStrings.StatusSearchingFormat, SearchProgress);
        else if (!string.IsNullOrWhiteSpace(SearchQuery) && HasDocument)
        {
            status += " · " + string.Format(PdfViewerStrings.StatusSearchResultsFormat, Math.Max(0, CurrentSearchResultIndex + 1), SearchResultCount);
            if (SearchResultsTruncated) status += PdfViewerStrings.StatusSearchTruncatedSuffix;
        }
        SetAndRaise(StatusTextProperty, ref _statusText, status);
        RaiseContractCommands();
        UpdateCommandParts();
    }

    private string StatusPageText()
    {
        // Documents with custom page labels show them beside the physical page number.
        var label = _currentPageLabel;
        return string.IsNullOrEmpty(label) || label == CurrentPage.ToString()
            ? string.Format(PdfViewerStrings.StatusPageFormat, CurrentPage, PageCount)
            : string.Format(PdfViewerStrings.StatusPageLabeledFormat, CurrentPage, PageCount, label);
    }

    private string ComputeCurrentPageLabel()
    {
        if (!HasDocument || CurrentPage < 1) return "";
        var raw = CurrentPage <= _pageLabels.Count ? _pageLabels[CurrentPage - 1] : "";
        return string.IsNullOrEmpty(raw) ? CurrentPage.ToString() : raw;
    }

    internal void SetSelectedText(string text) => SetAndRaise(SelectedTextProperty, ref _selectedText, text);

    // Called by the presenter whenever the selection range appears or disappears.
    internal void SetSelectionRangeState(bool selected)
    {
        if (SetAndRaise(IsTextSelectedProperty, ref _isTextSelected, selected)) RaiseContractCommands();
    }
    internal void ReportError(Exception error)
    {
        if (_disposed) return;
        SetContractError(error);
        SetAndRaise(ErrorMessageProperty, ref _errorMessage, error.Message);
        PseudoClasses.Set(":error", true);
    }
    private static async void Observe(Task task)
    {
        try { await task; }
        catch (OperationCanceledException) { }
        catch (Exception) { /* Public operations report failures before propagating to explicit callers. */ }
    }
}
