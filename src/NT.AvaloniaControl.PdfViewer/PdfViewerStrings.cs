namespace NT.AvaloniaControl.PdfViewer;

/// <summary>
/// Localizable UI, status, dialog, menu, accessibility and error strings used by the reader and
/// the reading document layer. Hosts replace values at startup, before creating viewer UI; the
/// default theme captures them when a template instance is built, so later changes apply to newly
/// created viewers. Defaults stay byte-identical to the previous hardcoded literals.
/// </summary>
public static class PdfViewerStrings
{
    public static string ErrorSaveConflict { get; set; } = "保存目标已被外部修改或删除。请重新加载，或另存到其他文件。";
    public static string ErrorPublishedSaveConflict { get; set; } = "提交时检测到并发修改，已保留保存快照和被替换版本。请查看恢复记录并选择需要的版本。";
    public static string ErrorSaveDurabilityUncertain { get; set; } = "保存的提交或耐久确认未完成，已保留恢复快照。请恢复存储连接后检查恢复记录。";
    public static string SaveText { get; set; } = "保存";
    public static string SaveAsText { get; set; } = "另存为";
    public static string UndoText { get; set; } = "撤销";
    public static string RedoText { get; set; } = "重做";
    public static string UnsavedChangesTitle { get; set; } = "文档有未保存的修改";
    public static string UnsavedChangesMessage { get; set; } = "保存修改后继续，或放弃本次修改。取消将保留当前文档。";
    public static string DiscardChangesText { get; set; } = "放弃修改";
    public static string CancelChangesText { get; set; } = "取消";
    public static string DirtyText { get; set; } = "未保存";
    // Toolbar, menus, tooltips and accessibility names.
    /// <summary>Open button label and accessibility name.</summary>
    public static string OpenText { get; set; } = "打开 PDF";
    /// <summary>Open button tooltip.</summary>
    public static string OpenTooltip { get; set; } = "打开本地 PDF · Ctrl+O";
    /// <summary>Sidebar toggle label and accessibility name.</summary>
    public static string SidebarToggleText { get; set; } = "侧栏";
    /// <summary>Previous-page button label, tooltip and accessibility name.</summary>
    public static string PreviousPageText { get; set; } = "上一页";
    /// <summary>Next-page button label, tooltip and accessibility name.</summary>
    public static string NextPageText { get; set; } = "下一页";
    /// <summary>Page-number box tooltip.</summary>
    public static string PageNumberTooltip { get; set; } = "跳转到页码";
    /// <summary>Page-number box accessibility name.</summary>
    public static string PageNumberAccessibleName { get; set; } = "页码";
    /// <summary>Zoom-out button tooltip.</summary>
    public static string ZoomOutTooltip { get; set; } = "缩小 · Ctrl+减号";
    /// <summary>Zoom-out button accessibility name (the button glyph carries no text).</summary>
    public static string ZoomOutAccessibleName { get; set; } = "缩小";
    /// <summary>Zoom-in button tooltip.</summary>
    public static string ZoomInTooltip { get; set; } = "放大 · Ctrl+加号";
    /// <summary>Zoom-in button accessibility name (the button glyph carries no text).</summary>
    public static string ZoomInAccessibleName { get; set; } = "放大";
    /// <summary>Fit-width button label and accessibility name.</summary>
    public static string FitWidthText { get; set; } = "适合宽度";
    /// <summary>Fit-page button label and accessibility name.</summary>
    public static string FitPageText { get; set; } = "适合整页";
    /// <summary>Fit-height button label and accessibility name.</summary>
    public static string FitHeightText { get; set; } = "适合高度";
    /// <summary>Rotate-left button tooltip.</summary>
    public static string RotateLeftTooltip { get; set; } = "向左旋转 90°";
    /// <summary>Rotate-left button accessibility name (the button glyph carries no text).</summary>
    public static string RotateLeftAccessibleName { get; set; } = "向左旋转 90°";
    /// <summary>Rotate-right button tooltip.</summary>
    public static string RotateRightTooltip { get; set; } = "向右旋转 90°";
    /// <summary>Rotate-right button accessibility name (the button glyph carries no text).</summary>
    public static string RotateRightAccessibleName { get; set; } = "向右旋转 90°";
    /// <summary>Copy button label and accessibility name.</summary>
    public static string CopyText { get; set; } = "复制";
    /// <summary>Copy button tooltip.</summary>
    public static string CopyTooltip { get; set; } = "复制选中文字 · Ctrl+C";
    /// <summary>Close button label and accessibility name.</summary>
    public static string CloseText { get; set; } = "关闭";
    /// <summary>Page-display-mode picker tooltip.</summary>
    public static string DisplayModeTooltip { get; set; } = "页面布局";
    /// <summary>Continuous layout item.</summary>
    public static string DisplayModeContinuousText { get; set; } = "连续";
    /// <summary>Single-page layout item.</summary>
    public static string DisplayModeSinglePageText { get; set; } = "单页";
    /// <summary>Two-page continuous layout item.</summary>
    public static string DisplayModeTwoPageContinuousText { get; set; } = "双页连续";
    /// <summary>Two-page layout item.</summary>
    public static string DisplayModeTwoPageText { get; set; } = "双页";
    /// <summary>“Show cover page” checkbox label.</summary>
    public static string ShowCoverPageText { get; set; } = "封面单独显示";
    /// <summary>“Show cover page” checkbox tooltip.</summary>
    public static string ShowCoverPageTooltip { get; set; } = "双页模式下第 1 页单独成页；关闭则按奇数页起配对";
    /// <summary>Add-user-bookmark button label.</summary>
    public static string AddBookmarkText { get; set; } = "＋书签";
    /// <summary>Add-user-bookmark button tooltip.</summary>
    public static string AddBookmarkTooltip { get; set; } = "在当前页添加书签";
    /// <summary>Search box placeholder text.</summary>
    public static string SearchPlaceholder { get; set; } = "搜索文档 · Ctrl+F";
    /// <summary>Search box accessibility name.</summary>
    public static string SearchAccessibleName { get; set; } = "搜索文档";
    /// <summary>Find-previous button label.</summary>
    public static string FindPreviousText { get; set; } = "上一个";
    /// <summary>Find-previous button tooltip.</summary>
    public static string FindPreviousTooltip { get; set; } = "上一个搜索结果 · Shift+F3";
    /// <summary>Find-next button label.</summary>
    public static string FindNextText { get; set; } = "下一个";
    /// <summary>Find-next button tooltip.</summary>
    public static string FindNextTooltip { get; set; } = "下一个搜索结果 · F3";
    /// <summary>“Match case” checkbox label.</summary>
    public static string SearchMatchCaseText { get; set; } = "区分大小写";
    /// <summary>“Whole word” checkbox label.</summary>
    public static string SearchWholeWordText { get; set; } = "全字匹配";
    /// <summary>Search match-count suffix shown after the number.</summary>
    public static string SearchMatchCountSuffix { get; set; } = "个匹配";
    /// <summary>Thumbnails tab header.</summary>
    public static string ThumbnailsTabText { get; set; } = "缩略图";
    /// <summary>Document outline tab header.</summary>
    public static string OutlineTabText { get; set; } = "目录";
    /// <summary>User bookmarks tab header.</summary>
    public static string BookmarksTabText { get; set; } = "书签";
    /// <summary>Remove-user-bookmark button label.</summary>
    public static string RemoveBookmarkText { get; set; } = "删除书签";
    /// <summary>Composite format for a bookmark's default page title ("第 {0} 页").</summary>
    public static string BookmarkPageTitleFormat { get; set; } = "第 {0} 页";
    /// <summary>Empty-state title.</summary>
    public static string EmptyTitle { get; set; } = "PDF 阅读器";
    /// <summary>Empty-state message.</summary>
    public static string EmptyMessage { get; set; } = "打开 PDF，即可连续阅读、搜索和选择文字。";
    /// <summary>Empty-state hint.</summary>
    public static string EmptyHint { get; set; } = "使用上方“打开 PDF”，或将本地路径绑定到 Source。";
    /// <summary>Loading overlay text.</summary>
    public static string LoadingText { get; set; } = "正在加载 PDF…";
    /// <summary>Password prompt card title.</summary>
    public static string PasswordPromptTitle { get; set; } = "需要密码";
    /// <summary>Password prompt explanation.</summary>
    public static string PasswordPromptMessage { get; set; } = "此 PDF 文档受密码保护。请输入密码后重试。";
    /// <summary>Password prompt confirm button.</summary>
    public static string PasswordPromptSubmitText { get; set; } = "确定";
    /// <summary>Password prompt cancel button.</summary>
    public static string PasswordPromptCancelText { get; set; } = "取消";
    /// <summary>Password prompt input's accessible name and watermark.</summary>
    public static string PasswordPromptAccessibleName { get; set; } = "文档密码";

    // Status bar.
    /// <summary>Status when no document is loaded.</summary>
    public static string StatusNoDocumentText { get; set; } = "打开 PDF 文件以开始阅读";
    /// <summary>Composite format for the in-progress search hint ("正在搜索… {0:P0}").</summary>
    public static string StatusSearchingFormat { get; set; } = "正在搜索… {0:P0}";
    /// <summary>Composite format for the search result counter ("搜索 {0} / {1}").</summary>
    public static string StatusSearchResultsFormat { get; set; } = "搜索 {0} / {1}";
    /// <summary>Suffix appended when a search stopped at its result limit.</summary>
    public static string StatusSearchTruncatedSuffix { get; set; } = "（已达上限）";
    /// <summary>Composite format for the page counter ("第 {0} / {1} 页").</summary>
    public static string StatusPageFormat { get; set; } = "第 {0} / {1} 页";
    /// <summary>Composite format for the page counter with a differing page label ("第 {0} / {1} 页（{2}）").</summary>
    public static string StatusPageLabeledFormat { get; set; } = "第 {0} / {1} 页（{2}）";

    // Dialogs and default names.
    /// <summary>Open-file dialog title.</summary>
    public static string FileDialogOpenTitle { get; set; } = "打开 PDF";
    /// <summary>Open-file dialog PDF file-type name.</summary>
    public static string FileDialogPdfFileType { get; set; } = "PDF 文档";
    /// <summary>Default display name for byte / stream / factory sources without an explicit name.</summary>
    public static string DefaultDocumentName { get; set; } = "PDF 文档";

    // Context menu.
    /// <summary>Presenter context-menu item that copies the selection.</summary>
    public static string CopySelectionMenuText { get; set; } = "复制选中文字";

    // Reading-surface errors. The structural editing backend keeps its own developer messages.
    /// <summary>Adding a user bookmark without a loaded document.</summary>
    public static string ErrorBookmarkRequiresDocument { get; set; } = "A document must be loaded before adding bookmarks.";
    /// <summary>A stream factory returned null.</summary>
    public static string ErrorStreamFactoryNull { get; set; } = "The PDF stream factory returned null.";
    /// <summary>A stream factory returned an unreadable stream.</summary>
    public static string ErrorStreamFactoryUnreadable { get; set; } = "The PDF stream factory returned an unreadable stream.";
    /// <summary>Input bytes are empty.</summary>
    public static string ErrorEmptyDocument { get; set; } = "The PDF document is empty.";

    /// <summary>Document error when a file exceeds the random-access addressable length.</summary>
    public static string ErrorFileTooLargeForRandomAccess { get; set; } = "The PDF file is too large for random-access loading on this platform.";
    /// <summary>Document error when the file on disk no longer matches the snapshot taken at open.</summary>
    public static string ErrorFileChanged { get; set; } = "The PDF file changed on disk while the document was open.";
    /// <summary>The document defines no readable pages.</summary>
    public static string ErrorNoReadablePages { get; set; } = "The PDF document has no readable pages.";
    /// <summary>Composite format: a page's size could not be read ("The size of PDF page {0} could not be read.").</summary>
    public static string ErrorPageSizeUnreadableFormat { get; set; } = "The size of PDF page {0} could not be read.";
    /// <summary>Composite format: a page bitmap could not be created ("A bitmap for PDF page {0} could not be created.").</summary>
    public static string ErrorPageBitmapCreateFormat { get; set; } = "A bitmap for PDF page {0} could not be created.";
    /// <summary>Composite format: a page could not be rendered ("PDF page {0} could not be rendered.").</summary>
    public static string ErrorPageRenderFormat { get; set; } = "PDF page {0} could not be rendered.";
    /// <summary>The document forbids text copying / extraction.</summary>
    public static string ErrorCopyDenied { get; set; } = "This PDF document does not permit copying or extracting its text.";
    /// <summary>Composite format: a page's text could not be read ("The text of PDF page {0} could not be read.").</summary>
    public static string ErrorPageTextUnreadableFormat { get; set; } = "The text of PDF page {0} could not be read.";
    /// <summary>Composite format: a page search could not start ("Text search on PDF page {0} could not be started.").</summary>
    public static string ErrorPageSearchStartFormat { get; set; } = "Text search on PDF page {0} could not be started.";
    /// <summary>Composite format: a page could not be loaded ("PDF page {0} could not be loaded.").</summary>
    public static string ErrorPageLoadFormat { get; set; } = "PDF page {0} could not be loaded.";
    /// <summary>Composite format: a page's text page could not be loaded ("The text of PDF page {0} could not be loaded.").</summary>
    public static string ErrorPageTextLoadFormat { get; set; } = "The text of PDF page {0} could not be loaded.";
    /// <summary>PDFium error code 2: the document could not be read.</summary>
    public static string ErrorLoadUnreadable { get; set; } = "The PDF document could not be read.";
    /// <summary>PDFium error code 3: invalid or unsupported data.</summary>
    public static string ErrorLoadInvalid { get; set; } = "The data is not a valid or supported PDF document.";
    /// <summary>PDFium error code 4: password required or incorrect.</summary>
    public static string ErrorLoadPassword { get; set; } = "The PDF requires a password, or the supplied password is incorrect.";
    /// <summary>PDFium error code 5: unsupported security handler.</summary>
    public static string ErrorLoadSecurity { get; set; } = "The PDF uses an unsupported security handler.";
    /// <summary>PDFium error code 6: unreadable page data.</summary>
    public static string ErrorLoadPageData { get; set; } = "The PDF contains unreadable page data.";
    /// <summary>PDFium unknown error code: the document could not be opened.</summary>
    public static string ErrorLoadUnknown { get; set; } = "The PDF document could not be opened.";
    /// <summary>A document information value exceeds the size limit or has invalid encoding.</summary>
    public static string ErrorMetadataInvalid { get; set; } = "A PDF document information value exceeds the supported size or has invalid encoding.";
    /// <summary>A document information value could not be read consistently.</summary>
    public static string ErrorMetadataUnreadable { get; set; } = "A PDF document information value could not be read consistently.";
    /// <summary>A page label exceeds the size limit or has invalid encoding.</summary>
    public static string ErrorPageLabelInvalid { get; set; } = "A PDF page label exceeds the supported size or has invalid encoding.";
    /// <summary>Control API called without a loaded document.</summary>
    public static string ErrorNoDocument { get; set; } = "No PDF document is loaded.";
    /// <summary>Control API called on a document without a readable page tree.</summary>
    public static string ErrorDocumentUnreadable { get; set; } = "This PDF document cannot be read.";
    /// <summary>Export DPI outside the supported range.</summary>
    public static string ErrorExportDpiRange { get; set; } = "Export DPI must be between 36 and 1200.";
    /// <summary>Render width outside the supported range.</summary>
    public static string ErrorRenderWidthRange { get; set; } = "Render width must be between 1 and 16384 pixels.";
    /// <summary>Render height outside the supported range.</summary>
    public static string ErrorRenderHeightRange { get; set; } = "Render height must be between 1 and 16384 pixels.";
    /// <summary>A single render exceeds the pixel budget.</summary>
    public static string ErrorRenderMegapixels { get; set; } = "A single render cannot exceed 64 megapixels.";
    /// <summary>A page raster region lies outside the raster or has a non-positive size.</summary>
    public static string ErrorRenderRegionRange { get; set; } = "The render region must lie inside the page raster and have a positive size.";
    /// <summary>Rotation is not a multiple of 90 degrees.</summary>
    public static string ErrorRotationRange { get; set; } = "Rotation must be 0, 90, 180, or 270 degrees.";
    /// <summary>Search query contains a NUL character.</summary>
    public static string ErrorSearchQueryNul { get; set; } = "The search query cannot contain a NUL character.";
    /// <summary>The PDFium native library could not be loaded.</summary>
    public static string ErrorPdfiumLibraryLoad { get; set; } =
        "The PDFium native library could not be loaded. Publish for a supported Windows, Linux, or macOS runtime and include the package's native runtime assets.";
    /// <summary>The PDFium native library does not match the process architecture.</summary>
    public static string ErrorPdfiumArchitecture { get; set; } = "The PDFium native library does not match this process's architecture.";
}
