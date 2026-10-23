using NT.AvaloniaControl.PdfViewer;

namespace PdfViewer.Tests;

/// <summary>Locks the default PdfViewerStrings values; tests and templates depend on these literals.</summary>
public sealed class StringsTests
{
    [Fact]
    public void Default_status_and_bookmark_formats_keep_the_locked_reader_literals()
    {
        Assert.Equal("打开 PDF 文件以开始阅读", PdfViewerStrings.StatusNoDocumentText);
        Assert.Equal("正在加载 PDF…", PdfViewerStrings.LoadingText);
        Assert.Equal("正在搜索… {0:P0}", PdfViewerStrings.StatusSearchingFormat);
        Assert.Equal("搜索 {0} / {1}", PdfViewerStrings.StatusSearchResultsFormat);
        Assert.Equal("（已达上限）", PdfViewerStrings.StatusSearchTruncatedSuffix);
        Assert.Equal("第 {0} / {1} 页", PdfViewerStrings.StatusPageFormat);
        Assert.Equal("第 {0} / {1} 页（{2}）", PdfViewerStrings.StatusPageLabeledFormat);
        Assert.Equal("第 {0} 页", PdfViewerStrings.BookmarkPageTitleFormat);
        Assert.Equal("个匹配", PdfViewerStrings.SearchMatchCountSuffix);
    }

    [Fact]
    public void Default_toolbar_dialog_and_accessibility_strings_keep_the_locked_reader_literals()
    {
        Assert.Equal("打开 PDF", PdfViewerStrings.OpenText);
        Assert.Equal("打开 PDF", PdfViewerStrings.FileDialogOpenTitle);
        Assert.Equal("PDF 文档", PdfViewerStrings.FileDialogPdfFileType);
        Assert.Equal("PDF 文档", PdfViewerStrings.DefaultDocumentName);
        Assert.Equal("复制选中文字", PdfViewerStrings.CopySelectionMenuText);
        Assert.Equal("放大", PdfViewerStrings.ZoomInAccessibleName);
        Assert.Equal("缩小", PdfViewerStrings.ZoomOutAccessibleName);
        Assert.Equal("向左旋转 90°", PdfViewerStrings.RotateLeftAccessibleName);
        Assert.Equal("向右旋转 90°", PdfViewerStrings.RotateRightAccessibleName);
        Assert.Equal("页码", PdfViewerStrings.PageNumberAccessibleName);
        Assert.Equal("搜索文档", PdfViewerStrings.SearchAccessibleName);
    }

    [Fact]
    public void Default_password_prompt_strings_keep_the_locked_reader_literals()
    {
        Assert.Equal("需要密码", PdfViewerStrings.PasswordPromptTitle);
        Assert.Equal("此 PDF 文档受密码保护。请输入密码后重试。", PdfViewerStrings.PasswordPromptMessage);
        Assert.Equal("确定", PdfViewerStrings.PasswordPromptSubmitText);
        Assert.Equal("取消", PdfViewerStrings.PasswordPromptCancelText);
        Assert.Equal("文档密码", PdfViewerStrings.PasswordPromptAccessibleName);
    }

    [Fact]
    public void Default_error_strings_keep_the_locked_reader_literals()
    {
        Assert.Equal("The PDF document is empty.", PdfViewerStrings.ErrorEmptyDocument);
        Assert.Equal("The PDF file is too large for random-access loading on this platform.", PdfViewerStrings.ErrorFileTooLargeForRandomAccess);
        Assert.Equal("The PDF file changed on disk while the document was open.", PdfViewerStrings.ErrorFileChanged);
        Assert.Equal("The PDF document has no readable pages.", PdfViewerStrings.ErrorNoReadablePages);
        Assert.Equal("The data is not a valid or supported PDF document.", PdfViewerStrings.ErrorLoadInvalid);
        Assert.Equal("The PDF requires a password, or the supplied password is incorrect.", PdfViewerStrings.ErrorLoadPassword);
        Assert.Equal("This PDF document does not permit copying or extracting its text.", PdfViewerStrings.ErrorCopyDenied);
        Assert.Equal("No PDF document is loaded.", PdfViewerStrings.ErrorNoDocument);
        Assert.Equal("A document must be loaded before adding bookmarks.", PdfViewerStrings.ErrorBookmarkRequiresDocument);
        Assert.Equal("PDF page {0} could not be loaded.", PdfViewerStrings.ErrorPageLoadFormat);
        Assert.Equal("The render region must lie inside the page raster and have a positive size.", PdfViewerStrings.ErrorRenderRegionRange);
    }

    [Fact]
    public async Task Document_errors_expose_the_string_table_values()
    {
        var empty = await Assert.ThrowsAsync<PdfDocumentException>(() =>
            PdfDocument.LoadAsync([], cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(PdfViewerStrings.ErrorEmptyDocument, empty.Message);
        var invalid = await Assert.ThrowsAsync<PdfDocumentException>(() =>
            PdfDocument.LoadAsync("not a PDF"u8.ToArray(), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(PdfViewerStrings.ErrorLoadInvalid, invalid.Message);
    }

    [Fact]
    public void Byte_and_factory_sources_fall_back_to_the_localizable_default_display_name()
    {
        Assert.Equal(PdfViewerStrings.DefaultDocumentName, PdfDocumentSource.FromBytes([1, 2, 3]).DisplayName);
        Assert.Equal(PdfViewerStrings.DefaultDocumentName,
            PdfDocumentSource.FromStreamFactory(_ => ValueTask.FromResult<Stream>(new MemoryStream([1, 2, 3]))).DisplayName);
        Assert.Equal("report.pdf", PdfDocumentSource.FromBytes([1, 2, 3], "report.pdf").DisplayName);
    }
}
