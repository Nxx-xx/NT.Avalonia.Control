using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using PdfViewerSamples;
using NT.AvaloniaControl.PdfViewer;
using PdfControl = NT.AvaloniaControl.PdfViewer.PdfViewer;

namespace NT.AvaloniaControl.Demo.Pages.PdfViewer;

internal sealed partial class PdfViewerPage
{
    internal async Task RunPdfSmokeAsync(string[] args)
    {
        var output = PdfSmokeArgument(args, "--smoke-output") ?? Path.Combine("artifacts", "platform", "pdfviewer.json");
        var image = PdfSmokeArgument(args, "--smoke-image") ?? Path.Combine("artifacts", "platform", "pdfviewer.png");
        var darkImage = Path.Combine(Path.GetDirectoryName(image) ?? ".", Path.GetFileNameWithoutExtension(image) + "-dark.png");
        var checks = new Dictionary<string, bool>();
        var observations = new Dictionary<string, object?>();
        string? error = null;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        try
        {
            await _reader.LoadDocumentAsync(SamplePdf.Create(), cancellationToken: timeout.Token);
            await PdfSmokeIdleAsync(timeout.Token);
            checks["nativeWindow"] = _hostWindow.TryGetPlatformHandle() is { Handle: not 0 };
            checks["pdfDemoSelected"] = _hostWindow is MainWindow main && main.SelectedPageId == "pdfviewer";
            checks["documentLoaded"] = _reader.HasDocument && _reader.PageCount == 3 && _reader.ErrorMessage is null or "";
            checks["outlineLoaded"] = _reader.Outline.Count == 3 && _reader.Outline[1].PageIndex == 1;
            checks["thumbnailsLoaded"] = _reader.Thumbnails.Count == 3 && _reader.Thumbnails.Any(thumbnail => thumbnail.Image is not null);
            checks["pagesRendered"] = _reader.RenderedPageCount > 0;
            checks["scrollViewerAttached"] = _reader.GetVisualDescendants().OfType<ScrollViewer>().Any(scroll => scroll.Name == "PART_ScrollViewer" && scroll.Viewport.Height > 0);

            await _reader.SelectAllAsync().WaitAsync(timeout.Token);
            checks["selectCurrentPageText"] = !string.IsNullOrWhiteSpace(_reader.SelectedText) && _reader.SelectedText.Contains("Avalonia", StringComparison.Ordinal);
            observations["selectedCharacterCount"] = _reader.SelectedText.Length;
            _reader.AllowTextSelection = false;
            checks["selectionDisabledClearsText"] = _reader.SelectedText.Length == 0;
            _reader.AllowTextSelection = true;

            await SearchForPdfSmokeAsync("Avalonia", timeout.Token);
            checks["searchAcrossPages"] = _reader.SearchResultCount >= 5;
            var firstResult = _reader.CurrentSearchResultIndex;
            _reader.FindNext();
            checks["nextSearchResult"] = _reader.CurrentSearchResultIndex != firstResult;
            _reader.FindPrevious();
            checks["previousSearchResult"] = _reader.CurrentSearchResultIndex == firstResult;
            observations["searchResultCount"] = _reader.SearchResultCount;
            await _reader.SearchAsync(null, timeout.Token);

            _reader.GoToPage(2);
            await PdfSmokeIdleAsync(timeout.Token);
            checks["pageNavigation"] = _reader.CurrentPage == 2;
            _reader.RotateRight();
            checks["rotationRight"] = _reader.Rotation == 90;
            _reader.RotateLeft();
            checks["rotationLeft"] = _reader.Rotation == 0;
            _reader.FitWidth();
            await PdfSmokeIdleAsync(timeout.Token);
            checks["fitWidth"] = _reader.ZoomMode == PdfZoomMode.FitWidth && _reader.ZoomLevel > 0;
            var fitZoom = _reader.ZoomLevel;
            _reader.ZoomIn();
            checks["zoomIn"] = _reader.ZoomMode == PdfZoomMode.Manual && _reader.ZoomLevel > fitZoom;
            _reader.ZoomOut();
            checks["zoomOut"] = _reader.ZoomLevel < fitZoom * 1.3;
            _reader.FitPage();
            await PdfSmokeIdleAsync(timeout.Token);
            checks["fitPage"] = _reader.ZoomMode == PdfZoomMode.FitPage && _reader.ZoomLevel > 0;

            // P2 reading extensions: display modes, page labels, user bookmarks, actual size.
            _reader.PageDisplayMode = PdfPageDisplayMode.SinglePage;
            _reader.GoToPage(2);
            await PdfSmokeIdleAsync(timeout.Token);
            checks["singlePageMode"] = _reader.PageDisplayMode == PdfPageDisplayMode.SinglePage && _reader.CurrentPage == 2;
            _reader.PageDisplayMode = PdfPageDisplayMode.TwoPage;
            _reader.ShowCoverPage = true;
            _reader.GoToPage(1);
            await PdfSmokeIdleAsync(timeout.Token);
            checks["twoPageMode"] = _reader.PageDisplayMode == PdfPageDisplayMode.TwoPage && _reader.CurrentPage == 1;
            _reader.NextPage();
            checks["twoPageSpreadStep"] = _reader.CurrentPage == 2;
            _reader.PageDisplayMode = PdfPageDisplayMode.Continuous;
            await PdfSmokeIdleAsync(timeout.Token);
            checks["pageLabelFallback"] = _reader.CurrentPageLabel == _reader.CurrentPage.ToString();
            var smokeBookmark = _reader.AddUserBookmark("冒烟书签", 2);
            checks["userBookmarkAdd"] = _reader.UserBookmarks.Count == 1 && smokeBookmark.PageNumber == 2;
            _reader.RemoveUserBookmark(smokeBookmark.Id);
            checks["userBookmarkRemove"] = _reader.UserBookmarks.Count == 0;
            _reader.ZoomIn();
            _reader.ActualSize();
            await PdfSmokeIdleAsync(timeout.Token);
            checks["actualSize"] = _reader.ZoomMode == PdfZoomMode.Manual && Math.Abs(_reader.ZoomLevel - 1) < .0001;

            // P2-01 completion: FitHeight and the reading-position save / restore interface.
            _reader.GoToPage(2);
            _reader.FitHeight();
            await PdfSmokeIdleAsync(timeout.Token);
            checks["fitHeight"] = _reader.ZoomMode == PdfZoomMode.FitHeight && _reader.ZoomLevel > 0;
            var smokePosition = _reader.CaptureReadingPosition();
            checks["readingPositionCapture"] = smokePosition is not null && smokePosition.PageIndex == 1;
            if (smokePosition is not null)
            {
                _reader.GoToPage(1);
                await PdfSmokeIdleAsync(timeout.Token);
                _reader.RestoreReadingPosition(smokePosition);
                await PdfSmokeIdleAsync(timeout.Token);
                checks["readingPositionRestore"] = _reader.CurrentPage == smokePosition.PageIndex + 1
                    && _reader.ZoomMode == PdfZoomMode.FitHeight;
            }
            // P2-02 completion: /Fit-family destinations honor their view parameters.
            _reader.NavigateToDestination(new PdfLinkDestination(0, null, null, PdfDestinationFit.FitH, Top: 20));
            await PdfSmokeIdleAsync(timeout.Token);
            checks["fitHNavigation"] = _reader.ZoomMode == PdfZoomMode.FitWidth && _reader.CurrentPage == 1;
            _reader.NavigateToDestination(new PdfLinkDestination(1, null, null, PdfDestinationFit.FitV, Left: 30));
            await PdfSmokeIdleAsync(timeout.Token);
            checks["fitVNavigation"] = _reader.ZoomMode == PdfZoomMode.FitHeight && _reader.CurrentPage == 2;
            _reader.NavigateToDestination(new PdfLinkDestination(1, null, null, PdfDestinationFit.FitR,
                Rectangle: new Rect(20, 20, 100, 80)));
            await PdfSmokeIdleAsync(timeout.Token);
            checks["fitRNavigation"] = _reader.ZoomMode == PdfZoomMode.Manual && _reader.CurrentPage == 2
                && _reader.ZoomLevel >= _reader.MinimumZoom && _reader.ZoomLevel <= _reader.MaximumZoom;
            _reader.NavigateToDestination(new PdfLinkDestination(2, null, null, PdfDestinationFit.FitR,
                Rectangle: new Rect(0, 0, 5, 5)));
            await PdfSmokeIdleAsync(timeout.Token);
            checks["fitRZoomClamp"] = Math.Abs(_reader.ZoomLevel - _reader.MaximumZoom) < .0001;

            // P2-03 completion: progressive search state, clear, text extraction, page image export.
            _reader.SearchResultLimit = 3;
            await SearchForPdfSmokeAsync("Avalonia", timeout.Token);
            // Pages 1+2 alone hold at least four matches, so the limit stops the scan before the end.
            checks["searchTruncatedAtLimit"] = _reader.SearchResultsTruncated && _reader.SearchResultCount == 3
                && _reader.SearchProgress > 0 && _reader.SearchProgress < 1;
            checks["searchBindableResults"] = _reader.SearchResults.Count == _reader.SearchResultCount
                && !string.IsNullOrEmpty(_reader.SearchResults[0].Text);
            checks["searchLimitStatus"] = _reader.StatusText.Contains("已达上限", StringComparison.Ordinal);
            _reader.SearchResultLimit = 10000;
            checks["clearSearchCommand"] = _reader.ClearSearchCommand.CanExecute(null);
            _reader.ClearSearch();
            checks["clearSearch"] = string.IsNullOrEmpty(_reader.SearchQuery) && _reader.SearchResultCount == 0
                && _reader.SearchResults.Count == 0 && _reader.SearchProgress == 0 && !_reader.SearchResultsTruncated;
            var smokePageText = await _reader.GetPageTextAsync(0, timeout.Token);
            checks["pageTextExtraction"] = smokePageText.Text.Contains("Avalonia", StringComparison.Ordinal);
            var smokeFullText = await _reader.GetDocumentTextAsync(timeout.Token);
            checks["documentTextExtraction"] = smokeFullText.Count(c => c == '\f') == _reader.PageCount - 1
                && smokeFullText.Contains("Avalonia", StringComparison.Ordinal);
            var smokePng = await _reader.ExportPageImageAsync(0, 96, timeout.Token);
            // Independent decode + pixel check: chunk CRCs, zlib payload, size and every pixel
            // against a fresh reference render of the same sample page (not just the header).
            await using (var smokePngDoc = await PdfDocument.LoadAsync(SamplePdf.Create(), cancellationToken: timeout.Token))
            {
                var smokePngPage = smokePngDoc.Pages[0];
                var smokePngWidth = Math.Max(1, (int)Math.Round(smokePngPage.Width * 96 / 72));
                var smokePngHeight = Math.Max(1, (int)Math.Round(smokePngPage.Height * 96 / 72));
                var smokePngRef = await smokePngDoc.RenderAsync(0, smokePngWidth, smokePngHeight, cancellationToken: timeout.Token);
                var smokePngImage = PdfSmokePng.Decode(smokePng);
                checks["pageImageExport"] = PdfSmokePng.Matches(smokePngImage, smokePngRef);
                observations["exportedPageImageSize"] = $"{smokePngImage.Width}x{smokePngImage.Height}";
            }
            observations["exportedPageImageBytes"] = smokePng.Length;
            observations["extractedPageTextLength"] = smokePageText.Text.Length;

            // P3-01 completion: random-access file loads (including CJK paths), non-seekable
            // staging with owned-temp release, and the external-truncation policy — which must
            // surface a managed error instead of aborting the process at a native CHECK.
            var smokeFile = Path.Combine(Path.GetTempPath(), "随机访问-冒烟-" + Guid.NewGuid().ToString("N") + ".pdf");
            var smokeTruncFile = Path.Combine(Path.GetTempPath(), "nt-pdf-smoke-trunc-" + Guid.NewGuid().ToString("N") + ".pdf");
            try
            {
                await File.WriteAllBytesAsync(smokeFile, SamplePdf.Create(), timeout.Token);
                await using (var smokeFileDoc = await PdfDocument.LoadFromFileAsync(smokeFile, cancellationToken: timeout.Token))
                {
                    var smokeFileText = await smokeFileDoc.GetTextAsync(0, timeout.Token);
                    checks["fileRandomAccessLoad"] = smokeFileDoc.Pages.Count == 3
                        && smokeFileText.Text.Contains("Avalonia", StringComparison.Ordinal);
                }

                var stagedBefore = Directory.GetFiles(Path.GetTempPath(), "nt-pdf-*.tmp").ToHashSet(StringComparer.Ordinal);
                await using (var smokeStagedDoc = await PdfDocument.LoadAsync(new PdfSmokeNonSeekableStream(SamplePdf.Create()),
                    leaveOpen: true, cancellationToken: timeout.Token))
                {
                    var smokeStagedText = await smokeStagedDoc.GetTextAsync(0, timeout.Token);
                    checks["nonSeekableStreamStaging"] = smokeStagedText.Text.Contains("Avalonia", StringComparison.Ordinal);
                }
                checks["stagedTempFileReleased"] = !Directory.GetFiles(Path.GetTempPath(), "nt-pdf-*.tmp")
                    .Any(path => !stagedBefore.Contains(path));

                await File.WriteAllBytesAsync(smokeTruncFile, SamplePdf.Create(), timeout.Token);
                await using (var smokeTruncDoc = await PdfDocument.LoadFromFileAsync(smokeTruncFile, cancellationToken: timeout.Token))
                {
                    using (var truncator = new FileStream(smokeTruncFile, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                        truncator.SetLength(64);
                    try
                    {
                        await smokeTruncDoc.RenderAsync(2, 200, 200, cancellationToken: timeout.Token);
                        checks["externalTruncationError"] = false;
                    }
                    catch (PdfDocumentException exception)
                    {
                        checks["externalTruncationError"] = exception.Kind == PdfErrorKind.FileAccess
                            && exception.Message == PdfViewerStrings.ErrorFileChanged;
                    }
                }
            }
            finally
            {
                File.Delete(smokeFile);
                File.Delete(smokeTruncFile);
            }

            // P3-02 completion: configurable budgets / pre-render depth, thumbnail retention
            // under a small budget, and decode-buffer / bitmap / input accounting.
            checks["cacheBudgetConfiguration"] = _reader.PageCacheBudget == PdfControl.DefaultPageCacheBudget
                && _reader.ThumbnailCacheBudget == PdfControl.DefaultThumbnailCacheBudget
                && _reader.TextCacheBudget == PdfControl.DefaultTextCacheBudget;
            _reader.PrerenderRows = 99;
            checks["prerenderRowsCoercion"] = _reader.PrerenderRows == 8;
            _reader.PrerenderRows = -1;
            checks["prerenderRowsFloor"] = _reader.PrerenderRows == 0;
            _reader.PrerenderRows = 1;
            var smokeStats = _reader.CacheStatistics;
            checks["cacheStatisticsAccounting"] = smokeStats.BudgetedBytes == smokeStats.PageBitmapBytes + smokeStats.ThumbnailBitmapBytes + smokeStats.TextBytes
                && smokeStats.TrackedBytes == smokeStats.BudgetedBytes + smokeStats.InputBytes + smokeStats.DecodeBufferBytes
                && smokeStats.InputBytes == SamplePdf.Create().Length
                && smokeStats.PageBitmapBytes == _reader.CachedBytes
                && smokeStats.DecodeBufferPeakBytes > 0;
            observations["cacheTrackedBytes"] = smokeStats.TrackedBytes;
            observations["decodeBufferPeakBytes"] = smokeStats.DecodeBufferPeakBytes;
            var smokeThumbnailBudget = _reader.ThumbnailCacheBudget;
            _reader.ThumbnailCacheBudget = PdfControl.MinimumCacheBudget;
            await PdfSmokeIdleAsync(timeout.Token);
            checks["thumbnailBudgetEnforced"] = _reader.CacheStatistics.ThumbnailBitmapBytes <= PdfControl.MinimumCacheBudget;
            _reader.ThumbnailCacheBudget = smokeThumbnailBudget;
            await PdfSmokeIdleAsync(timeout.Token);

            // P3-03 quality contract: configurable decode caps with coercion, region tiles
            // that reproduce the full render (tiling feasibility), settle-driven re-raster
            // and the quality-state counters.
            checks["maxRenderConfiguration"] = PdfControl.DefaultMaxRenderDimension == 8192
                && PdfControl.DefaultMaxRenderPixels == 30_000_000
                && PdfControl.DefaultMaxRenderScale == 8
                && _reader.MaxRenderDimension == PdfControl.DefaultMaxRenderDimension
                && _reader.MaxRenderPixels == PdfControl.DefaultMaxRenderPixels
                && _reader.MaxRenderScale == PdfControl.DefaultMaxRenderScale;
            _reader.MaxRenderDimension = 10;
            _reader.MaxRenderPixels = 1;
            _reader.MaxRenderScale = 0;
            checks["maxRenderCoercion"] = _reader.MaxRenderDimension == 256
                && _reader.MaxRenderPixels == 65_536
                && _reader.MaxRenderScale == 1;
            _reader.MaxRenderDimension = PdfControl.DefaultMaxRenderDimension;
            _reader.MaxRenderPixels = PdfControl.DefaultMaxRenderPixels;
            _reader.MaxRenderScale = PdfControl.DefaultMaxRenderScale;
            await PdfSmokeIdleAsync(timeout.Token);
            await using (var smokeRegionDoc = await PdfDocument.LoadAsync(SamplePdf.Create(), cancellationToken: timeout.Token))
            {
                var smokeFull = await smokeRegionDoc.RenderAsync(0, 240, 320, cancellationToken: timeout.Token);
                var smokeTile = await smokeRegionDoc.RenderPageRegionAsync(0, 240, 320, 0, 0, 240, 151, cancellationToken: timeout.Token);
                var smokeTileMatches = smokeTile.Width == 240 && smokeTile.Height == 151;
                for (var y = 0; smokeTileMatches && y < smokeTile.Height; y += 2)
                    for (var x = 0; smokeTileMatches && x < smokeTile.Width; x += 2)
                        for (var channel = 0; channel < 4; channel++)
                            smokeTileMatches &= smokeFull.Pixels[y * smokeFull.Stride + x * 4 + channel]
                                == smokeTile.Pixels[y * smokeTile.Stride + x * 4 + channel];
                checks["renderRegionTilesMatchFullRender"] = smokeTileMatches;
            }
            var smokeRenderedBeforeQuality = _reader.RenderedPageCount;
            _reader.ZoomMode = PdfZoomMode.FitWidth;
            await PdfSmokeIdleAsync(timeout.Token);
            checks["qualitySettleReRasterizes"] = _reader.RenderedPageCount > smokeRenderedBeforeQuality
                && _reader.DegradedPageCount == 0;
            observations["previewPageCount"] = _reader.PreviewPageCount;
            observations["degradedPageCount"] = _reader.DegradedPageCount;

            // P2-04 completion: tool visibility, sidebar width / placement, strings and accessibility names.
            var smokeSidebar = _reader.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "PART_Sidebar");
            var smokeZoomIn = _reader.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_ZoomIn");
            var smokeFitPage = _reader.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_FitPage");
            _reader.IsZoomToolVisible = false;
            await PdfSmokeIdleAsync(timeout.Token);
            checks["toolVisibility"] = !smokeZoomIn.IsVisible && smokeFitPage.IsVisible;
            _reader.IsZoomToolVisible = true;
            _reader.SidebarWidth = 320;
            await PdfSmokeIdleAsync(timeout.Token);
            checks["sidebarWidth"] = Math.Abs(smokeSidebar.Width - 320) < .0001;
            _reader.SidebarWidth = 208;
            _reader.SidebarPlacement = PdfSidebarPlacement.Right;
            await PdfSmokeIdleAsync(timeout.Token);
            checks["sidebarPlacement"] = Grid.GetColumn(smokeSidebar) == 1
                && smokeSidebar.BorderThickness == new Thickness(1, 0, 0, 0);
            _reader.SidebarPlacement = PdfSidebarPlacement.Left;
            await PdfSmokeIdleAsync(timeout.Token);
            checks["automationNames"] = Equals(AutomationProperties.GetName(smokeZoomIn), PdfViewerStrings.ZoomInAccessibleName)
                && Equals(AutomationProperties.GetName(smokeFitPage), PdfViewerStrings.FitPageText);
            _reader.GoToPage(2);
            var smokeDefaultBookmark = _reader.AddUserBookmark();
            checks["stringsSurface"] = smokeDefaultBookmark.Title
                == string.Format(PdfViewerStrings.BookmarkPageTitleFormat, smokeDefaultBookmark.PageNumber);
            _reader.RemoveUserBookmark(smokeDefaultBookmark.Id);

            observations["userBookmarkCount"] = _reader.UserBookmarks.Count;
            observations["pageLabel"] = _reader.CurrentPageLabel;

            _reader.GoToPage(1);
            _reader.FitWidth();
            await SearchForPdfSmokeAsync("Avalonia", timeout.Token);
            checks["searchProgressComplete"] = Math.Abs(_reader.SearchProgress - 1) < .0001;
            _hostWindow.RequestedThemeVariant = ThemeVariant.Light;
            await PdfSmokeIdleAsync(timeout.Token);
            CapturePdfSmokeImage(image);
            checks["lightThemeImage"] = File.Exists(image) && new FileInfo(image).Length > 10000;
            _hostWindow.RequestedThemeVariant = ThemeVariant.Dark;
            await PdfSmokeIdleAsync(timeout.Token);
            CapturePdfSmokeImage(darkImage);
            checks["darkThemeImage"] = _hostWindow.ActualThemeVariant == ThemeVariant.Dark && File.Exists(darkImage) && new FileInfo(darkImage).Length > 10000;
            observations["renderedPageCount"] = _reader.RenderedPageCount;
            observations["cachedPageCount"] = _reader.CachedPageCount;
            observations["cachedBytes"] = _reader.CachedBytes;
            observations["pageCount"] = _reader.PageCount;
            observations["zoomLevel"] = _reader.ZoomLevel;
        }
        catch (Exception exception) { error = exception.ToString(); }
        var passed = error is null && checks.Count > 0 && checks.Values.All(value => value);
        try
        {
            var fullOutput = Path.GetFullPath(output);
            Directory.CreateDirectory(Path.GetDirectoryName(fullOutput)!);
            File.WriteAllText(fullOutput, JsonSerializer.Serialize(new
            {
                Passed = passed, TimestampUtc = DateTimeOffset.UtcNow,
                Platform = RuntimeInformation.OSDescription, Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                Runtime = Environment.Version.ToString(), AvaloniaVersion = typeof(Application).Assembly.GetName().Version?.ToString(),
                WindowWidth = _hostWindow.ClientSize.Width, WindowHeight = _hostWindow.ClientSize.Height,
                RenderScaling = _hostWindow.RenderScaling, Checks = checks, Observations = observations, Error = error,
                Image = Path.GetFullPath(image), DarkImage = Path.GetFullPath(darkImage),
                ImageCaptureKind = "Avalonia RenderTargetBitmap of the native Window visual; not a desktop screenshot.",
                PhysicalInputVerified = false, ClipboardVerified = false, OtherDesktopPlatformsVerified = false,
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.Shutdown(passed ? 0 : 1);
            else _hostWindow.Close();
        }
    }

    private async Task PdfSmokeIdleAsync(CancellationToken cancellation)
    {
        _hostWindow.UpdateLayout();
        await Task.Delay(100, cancellation);
        await _reader.WaitForIdleAsync(cancellation);
        _hostWindow.UpdateLayout();
        await Task.Delay(120, cancellation);
    }

    private async Task SearchForPdfSmokeAsync(string query, CancellationToken cancellation)
    {
        // Let the editable search field and its debounce settle before the explicit, awaited search.
        _reader.SearchQuery = query;
        await Task.Delay(375, cancellation);
        await _reader.SearchAsync(query, cancellation);
    }

    private void CapturePdfSmokeImage(string path)
    {
        var size = _hostWindow.ClientSize;
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height)), new Vector(96, 96));
        bitmap.Render(_hostWindow);
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        bitmap.Save(fullPath);
    }

    private static string? PdfSmokeArgument(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    /// <summary>Forward-only input: the staging path must accept streams without Length or Seek.</summary>
    private sealed class PdfSmokeNonSeekableStream(byte[] payload) : Stream
    {
        private int _position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = Math.Min(count, payload.Length - _position);
            Array.Copy(payload, _position, buffer, offset, read);
            _position += read;
            return read;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = Math.Min(buffer.Length, payload.Length - _position);
            payload.AsSpan(_position, read).CopyTo(buffer.Span);
            _position += read;
            return ValueTask.FromResult(read);
        }
    }
}
