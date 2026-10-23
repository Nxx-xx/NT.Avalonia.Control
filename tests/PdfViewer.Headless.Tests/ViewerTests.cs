using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using PdfViewer.Tests;
using NT.AvaloniaControl.PdfViewer;
using NT.AvaloniaControl.PdfViewer.Themes;
using Reader = NT.AvaloniaControl.PdfViewer.PdfViewer;

namespace PdfViewer.Headless.Tests;

public sealed class ViewerTests
{
    [Fact]
    public void Functional_assembly_has_no_dependency_on_the_optional_theme()
    {
        Assert.DoesNotContain(typeof(Reader).Assembly.GetReferencedAssemblies(), assembly => assembly.Name == "NT.AvaloniaControl.PdfViewer.Themes");
        Assert.Contains(typeof(PdfViewerTheme).Assembly.GetReferencedAssemblies(), assembly => assembly.Name == "NT.AvaloniaControl.PdfViewer");
    }

    [Fact]
    public Task Real_theme_loads_document_renders_pages_and_toolbar_controls_navigate() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await ViewerFixture.Create(PdfFixture.ReadingDocument());
        Assert.True(fixture.Reader.HasDocument);
        Assert.Equal(2, fixture.Reader.PageCount);
        Assert.Equal(1, fixture.Reader.CurrentPage);
        Assert.Null(fixture.Reader.ErrorMessage);
        Assert.True(fixture.Reader.CachedPageCount > 0);
        Assert.True(fixture.Reader.CachedBytes > 0);
        fixture.Click("PART_NextPage");
        await fixture.Idle();
        Assert.Equal(2, fixture.Reader.CurrentPage);
        fixture.Click("PART_PreviousPage");
        await fixture.Idle();
        Assert.Equal(1, fixture.Reader.CurrentPage);
        Assert.Equal(2, fixture.Reader.Thumbnails.Count);
        var chapter = Assert.Single(fixture.Reader.Outline);
        fixture.Reader.IsSidebarVisible = true;
        await fixture.Idle();
        var tabs = fixture.Reader.GetVisualDescendants().OfType<TabControl>().Single();
        tabs.SelectedIndex = 1;
        await fixture.Idle();
        var tree = fixture.Reader.GetVisualDescendants().OfType<TreeView>().Single(control => control.Name == "PART_Outline");
        var destination = Assert.Single(chapter.Children);
        tree.SelectedItem = destination;
        Assert.Same(destination, tree.SelectedItem);
        Assert.Equal(2, fixture.Reader.CurrentPage);
        await fixture.Idle();
        Assert.Equal(2, fixture.Reader.CurrentPage);
        tabs.SelectedIndex = 0;
        await fixture.Idle();
        var thumbnails = fixture.Reader.GetVisualDescendants().OfType<ListBox>().Single(control => control.Name == "PART_Thumbnails");
        thumbnails.SelectedIndex = 0;
        await fixture.Idle();
        Assert.Equal(1, fixture.Reader.CurrentPage);
        fixture.Reader.IsSidebarVisible = false;
        await fixture.Idle();
        var zoom = fixture.Reader.ZoomLevel;
        fixture.Click("PART_ZoomIn");
        Assert.True(fixture.Reader.ZoomLevel > zoom);
        fixture.Click("PART_RotateRight");
        await fixture.Idle();
        Assert.Equal(90, fixture.Reader.Rotation);
        fixture.Click("PART_RotateLeft");
        Assert.Equal(0, fixture.Reader.Rotation);
        fixture.Click("PART_Close");
        Assert.False(fixture.Reader.HasDocument);
        Assert.Equal(0, fixture.Reader.PageCount);
        Assert.Equal(0, fixture.Reader.CachedBytes);
    });

    [Fact]
    public Task Search_navigates_between_pages_and_replacement_clears_old_results() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await ViewerFixture.Create(PdfFixture.ReadingDocument());
        await fixture.Reader.SearchAsync("hello", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(5, fixture.Reader.SearchResultCount);
        for (var i = 0; i < 4; i++) fixture.Reader.FindNext();
        await fixture.Idle();
        Assert.Equal(2, fixture.Reader.CurrentPage);
        fixture.Reader.FindNext();
        Assert.Equal(1, fixture.Reader.CurrentPage);
        fixture.Reader.FindPrevious();
        Assert.Equal(2, fixture.Reader.CurrentPage);
        await fixture.Reader.LoadDocumentAsync(PdfFixture.SinglePage(), cancellationToken: TestContext.Current.CancellationToken);
        await fixture.Idle();
        Assert.Equal(1, fixture.Reader.PageCount);
        Assert.Equal(1, fixture.Reader.CurrentPage);
        Assert.Equal(0, fixture.Reader.SearchResultCount);
        await fixture.Reader.SearchAsync("Replacement", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, fixture.Reader.SearchResultCount);
        Assert.Contains("搜索", fixture.Reader.StatusText);
        await fixture.Reader.SearchAsync(null, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(0, fixture.Reader.SearchResultCount);
        Assert.Equal(-1, fixture.Reader.CurrentSearchResultIndex);
        Assert.False(fixture.Reader.IsSearching);
        Assert.DoesNotContain("搜索", fixture.Reader.StatusText);
    });

    [Fact]
    public Task Immediate_navigation_after_awaiting_load_survives_the_queued_loaded_callback() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = new ViewerFixture();
        await fixture.Reader.LoadDocumentAsync(PdfFixture.ReadingDocument(), cancellationToken: TestContext.Current.CancellationToken);
        fixture.Reader.GoToPage(2);
        Assert.Equal(2, fixture.Reader.CurrentPage);
        await fixture.Idle();
        Assert.Equal(2, fixture.Reader.CurrentPage);
        Assert.True(fixture.Reader.CachedPageCount > 0);
        Assert.Null(fixture.Reader.ErrorMessage);
    });

    [Fact]
    public Task Invalid_and_password_protected_files_show_error_then_recover() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = new ViewerFixture();
        await Assert.ThrowsAsync<PdfDocumentException>(() => fixture.Reader.LoadDocumentAsync("not a PDF"u8.ToArray(), cancellationToken: TestContext.Current.CancellationToken));
        Assert.False(fixture.Reader.HasDocument);
        Assert.False(fixture.Reader.IsLoading);
        Assert.False(string.IsNullOrWhiteSpace(fixture.Reader.ErrorMessage));
        await Assert.ThrowsAsync<PdfDocumentException>(() => fixture.Reader.LoadDocumentAsync(PdfFixture.PasswordProtected(), cancellationToken: TestContext.Current.CancellationToken));
        Assert.False(fixture.Reader.HasDocument);
        Assert.False(string.IsNullOrWhiteSpace(fixture.Reader.ErrorMessage));
        await fixture.Reader.LoadDocumentAsync(PdfFixture.PasswordProtected(), "reader", cancellationToken: TestContext.Current.CancellationToken);
        await fixture.Idle();
        Assert.True(fixture.Reader.HasDocument);
        Assert.Null(fixture.Reader.ErrorMessage);
        await fixture.Reader.SearchAsync("Protected", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, fixture.Reader.SearchResultCount);
    });

    [Fact]
    public Task Source_path_opens_a_file_and_clearing_it_closes_and_releases_the_document() => HeadlessTestScope.Run(async () =>
    {
        var path = Path.Combine(Path.GetTempPath(), $"pdf-viewer-tests-{Guid.NewGuid():N}.pdf");
        try
        {
            await File.WriteAllBytesAsync(path, PdfFixture.ReadingDocument(), TestContext.Current.CancellationToken);
            using var fixture = new ViewerFixture();
            fixture.Reader.Source = path;
            await Until(() => fixture.Reader.HasDocument || fixture.Reader.ErrorMessage is not null);
            await fixture.Idle();
            Assert.True(fixture.Reader.HasDocument, fixture.Reader.ErrorMessage);
            Assert.Equal(path, fixture.Reader.Source);
            Assert.Equal(Path.GetFileName(path), fixture.Reader.DocumentName);
            Assert.Equal(2, fixture.Reader.PageCount);
            fixture.Reader.Source = null;
            Assert.False(fixture.Reader.HasDocument);
            Assert.Equal(0, fixture.Reader.CachedBytes);
            Assert.Empty(fixture.Reader.Thumbnails);
            Assert.Empty(fixture.Reader.Outline);
        }
        finally { File.Delete(path); }
    });

    [Fact]
    public Task Closing_during_stream_load_prevents_late_document_resurrection() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = new ViewerFixture();
        using var stream = new GatedReadStream(PdfFixture.ReadingDocument());
        var pending = fixture.Reader.LoadDocumentAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
        await stream.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.True(fixture.Reader.IsLoading);
        fixture.Reader.CloseDocument();
        stream.Release.TrySetResult();
        await IgnoreCancellation(pending);
        await fixture.Idle();
        Assert.False(fixture.Reader.HasDocument);
        Assert.False(fixture.Reader.IsLoading);
        Assert.Equal(0, fixture.Reader.PageCount);
        Assert.Null(fixture.Reader.ErrorMessage);
        Assert.Equal(0, fixture.Reader.CachedBytes);
    });

    [Fact]
    public Task Replacement_wins_against_a_slow_previous_stream_load() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = new ViewerFixture();
        using var stream = new GatedReadStream(PdfFixture.ReadingDocument());
        var pending = fixture.Reader.LoadDocumentAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
        await stream.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await fixture.Reader.LoadDocumentAsync(PdfFixture.SinglePage("Newest document"), cancellationToken: TestContext.Current.CancellationToken);
        stream.Release.TrySetResult();
        await IgnoreCancellation(pending);
        await fixture.Idle();
        Assert.True(fixture.Reader.HasDocument);
        Assert.Equal(1, fixture.Reader.PageCount);
        await fixture.Reader.SearchAsync("Newest", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, fixture.Reader.SearchResultCount);
        Assert.Null(fixture.Reader.ErrorMessage);
    });

    [Fact]
    public Task Cancelling_a_replacement_load_clears_the_previous_document_without_an_error() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await ViewerFixture.Create(PdfFixture.ReadingDocument());
        using var cancelled = new CancellationTokenSource();
        using var stream = new GatedReadStream(PdfFixture.SinglePage());
        var pending = fixture.Reader.LoadDocumentAsync(stream, cancellationToken: cancelled.Token);
        await stream.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.False(fixture.Reader.HasDocument);
        Assert.Equal(0, fixture.Reader.PageCount);
        Assert.Equal(0, fixture.Reader.CachedBytes);
        cancelled.Cancel();
        stream.Release.TrySetResult();
        await IgnoreCancellation(pending);
        Assert.False(fixture.Reader.HasDocument);
        Assert.False(fixture.Reader.IsLoading);
        Assert.Null(fixture.Reader.ErrorMessage);
    });

    [Fact]
    public Task Disposing_during_load_cancels_background_work_and_releases_bitmaps() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = new ViewerFixture();
        using var stream = new GatedReadStream(PdfFixture.ReadingDocument());
        var pending = fixture.Reader.LoadDocumentAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
        await stream.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        fixture.Reader.Dispose();
        stream.Release.TrySetResult();
        await IgnoreCancellation(pending);
        Assert.False(fixture.Reader.HasDocument);
        Assert.Equal(0, fixture.Reader.CachedBytes);
        fixture.Reader.Dispose();
    });

    [Fact]
    public Task Large_document_realizes_a_small_viewport_and_navigation_avoids_rendering_every_page() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await ViewerFixture.Create(PdfFixture.ManyPages(120));
        Assert.Equal(120, fixture.Reader.PageCount);
        Assert.InRange(fixture.Reader.CachedPageCount, 1, 15);
        Assert.InRange(fixture.Reader.RenderedPageCount, 1, 15);
        fixture.Reader.GoToPage(100);
        await fixture.Idle();
        Assert.Equal(100, fixture.Reader.CurrentPage);
        Assert.InRange(fixture.Reader.CachedPageCount, 1, 25);
        Assert.InRange(fixture.Reader.RenderedPageCount, 1, 25);
        Assert.InRange(fixture.Reader.CachedBytes, 1, 64L * 1024 * 1024);
        fixture.Reader.CloseDocument();
        Assert.Equal(0, fixture.Reader.CachedPageCount);
        Assert.Equal(0, fixture.Reader.CachedBytes);
    });

    [Fact]
    public Task Very_small_pages_keep_every_visible_page_cached_even_when_more_than_twelve_fit() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await ViewerFixture.Create(PdfFixture.ManyPages(100, width: 100, height: 30));
        fixture.Reader.ZoomLevel = .1;
        await fixture.Idle();
        var presenter = Assert.IsType<PdfDocumentPresenter>(fixture.Reader.DocumentPresenter);
        var scroll = presenter.GetVisualAncestors().OfType<ScrollViewer>().First();
        var viewport = new Rect(scroll.Offset.X, scroll.Offset.Y, scroll.Viewport.Width, scroll.Viewport.Height);
        var visible = presenter.PageBounds.Count(bounds => bounds.Intersects(viewport));
        Assert.True(visible > 12, $"Expected more than twelve visible pages; found {visible}.");
        Assert.InRange(fixture.Reader.CachedPageCount, visible, visible + 4);
        Assert.InRange(fixture.Reader.CachedBytes, 1, 96L * 1024 * 1024);
        var rendered = fixture.Reader.RenderedPageCount;
        await fixture.Idle();
        Assert.Equal(rendered, fixture.Reader.RenderedPageCount);
        fixture.Reader.GoToPage(100);
        await fixture.Idle();
        Assert.Equal(100, fixture.Reader.CurrentPage);
    });

    [Fact]
    public Task Rapid_hide_show_resumes_cancelled_page_and_thumbnail_rendering() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await ViewerFixture.Create(PdfFixture.ManyPages(30));
        fixture.Reader.IsSidebarVisible = true;
        fixture.Reader.ZoomLevel = 2;
        fixture.Reader.IsVisible = false;
        fixture.Reader.IsVisible = true;
        await fixture.Idle();
        await Until(() => fixture.Reader.Thumbnails.Any(thumbnail => thumbnail.Image is not null));
        Assert.True(fixture.Reader.HasDocument);
        Assert.True(fixture.Reader.CachedPageCount > 0);
        Assert.Null(fixture.Reader.ErrorMessage);
        fixture.Reader.GoToPage(20);
        fixture.Reader.IsVisible = false;
        fixture.Reader.IsVisible = true;
        await fixture.Idle();
        Assert.Equal(20, fixture.Reader.CurrentPage);
        Assert.True(fixture.Reader.CachedPageCount > 0);
        Assert.Null(fixture.Reader.ErrorMessage);
    });

    [Fact]
    public Task Theme_switch_and_resource_override_preserve_open_document_and_search() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await ViewerFixture.Create(PdfFixture.ReadingDocument());
        await fixture.Reader.SearchAsync("hello", cancellationToken: TestContext.Current.CancellationToken);
        fixture.Window.RequestedThemeVariant = ThemeVariant.Light;
        fixture.Window.UpdateLayout();
        var light = Assert.IsAssignableFrom<ISolidColorBrush>(fixture.Reader.Background).Color;
        fixture.Window.RequestedThemeVariant = ThemeVariant.Dark;
        fixture.Window.UpdateLayout();
        var dark = Assert.IsAssignableFrom<ISolidColorBrush>(fixture.Reader.Background).Color;
        Assert.NotEqual(light, dark);
        fixture.Reader.Resources["PdfViewerBackgroundBrush"] = Brushes.MediumPurple;
        fixture.Window.UpdateLayout();
        Assert.Same(Brushes.MediumPurple, fixture.Reader.Background);
        Assert.True(fixture.Reader.HasDocument);
        Assert.Equal(5, fixture.Reader.SearchResultCount);
    });

    [Fact]
    public Task Pointer_drag_selects_and_copies_the_same_text_after_every_viewer_rotation() => HeadlessTestScope.Run(async () =>
    {
        var bytes = PdfFixture.ReadingDocument();
        using var native = await PdfDocument.LoadAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        var text = await native.GetTextAsync(0, cancellationToken: TestContext.Current.CancellationToken);
        var start = text.Characters.First(character => character.Index == 0).Bounds.Center;
        var end = text.Characters.First(character => character.Index == 4).Bounds.Center;
        using var fixture = await ViewerFixture.Create(bytes);
        foreach (var rotation in new[] { 0, 90, 180, 270 })
        {
            fixture.Reader.AllowTextSelection = false;
            fixture.Reader.AllowTextSelection = true;
            fixture.Reader.Rotation = rotation;
            fixture.Reader.FitPage();
            await fixture.Idle();
            var presenter = Assert.IsType<PdfDocumentPresenter>(fixture.Reader.DocumentPresenter);
            var page = presenter.GetPageBounds(0);
            var scale = fixture.Reader.ZoomLevel * 96 / 72;
            Point Map(Point original)
            {
                var rotated = rotation switch
                {
                    90 => new Point(400 - original.Y, original.X),
                    180 => new Point(300 - original.X, 400 - original.Y),
                    270 => new Point(original.Y, 300 - original.X),
                    _ => original,
                };
                return presenter.TranslatePoint(new Point(page.X + rotated.X * scale, page.Y + rotated.Y * scale), fixture.Window)!.Value;
            }
            ScopedInput(() => fixture.Window.MouseDown(Map(start), MouseButton.Left));
            await Until(() => fixture.Reader.SelectedText == "H");
            ScopedInput(() => fixture.Window.MouseMove(Map(end)));
            await Until(() => fixture.Reader.SelectedText == "Hello");
            ScopedInput(() => fixture.Window.MouseUp(Map(end), MouseButton.Left));
            Assert.Equal("Hello", fixture.Reader.SelectedText);
            await fixture.Reader.CopySelectionToClipboardAsync();
            Assert.Equal("Hello", await fixture.Window.Clipboard!.TryGetTextAsync());
        }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Pointer_drag_across_pages_selects_and_copies_in_both_directions(bool reverse) => HeadlessTestScope.Run(async () =>
    {
        var bytes = PdfFixture.ReadingDocument();
        using var native = await PdfDocument.LoadAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        var firstText = await native.GetTextAsync(0, cancellationToken: TestContext.Current.CancellationToken);
        var secondText = await native.GetTextAsync(1, cancellationToken: TestContext.Current.CancellationToken);
        var firstCharacter = firstText.Characters.First(character => !string.IsNullOrWhiteSpace(character.Text));
        var lastCharacter = secondText.Characters.Last(character => !string.IsNullOrWhiteSpace(character.Text));
        var expected = string.Concat(firstText.Characters.Where(character => character.Index >= firstCharacter.Index).Select(character => character.Text))
            + Environment.NewLine
            + string.Concat(secondText.Characters.Where(character => character.Index <= lastCharacter.Index).Select(character => character.Text));
        using var fixture = await ViewerFixture.Create(bytes);
        fixture.Reader.IsToolbarVisible = false;
        fixture.Reader.ZoomLevel = 1;
        await fixture.Idle();
        var presenter = Assert.IsType<PdfDocumentPresenter>(fixture.Reader.DocumentPresenter);
        var scroll = presenter.GetVisualAncestors().OfType<ScrollViewer>().First();
        Assert.True(scroll.Extent.Height > scroll.Viewport.Height);

        Point DocumentPoint(int pageIndex, PdfTextCharacter character)
        {
            var page = presenter.GetPageBounds(pageIndex);
            var scale = fixture.Reader.ZoomLevel * 96 / 72;
            return new Point(page.X + character.Bounds.Center.X * scale, page.Y + character.Bounds.Center.Y * scale);
        }

        Point WindowPoint(int pageIndex, PdfTextCharacter character)
        {
            var point = presenter.TranslatePoint(DocumentPoint(pageIndex, character), fixture.Window)!.Value;
            Assert.True(new Rect(fixture.Window.ClientSize).Contains(point));
            return point;
        }

        // Keep the actual pointer captured while the viewport moves between the
        // two pages. The second page also has its own intrinsic PDF rotation.
        var anchorPage = reverse ? 1 : 0;
        var focusPage = reverse ? 0 : 1;
        var anchor = reverse ? lastCharacter : firstCharacter;
        var focus = reverse ? firstCharacter : lastCharacter;
        if (reverse)
            scroll.Offset = new Vector(0, Math.Max(0, DocumentPoint(1, lastCharacter).Y - scroll.Viewport.Height / 2));
        else scroll.Offset = default;
        await fixture.Idle();
        ScopedInput(() => fixture.Window.MouseDown(WindowPoint(anchorPage, anchor), MouseButton.Left));
        await Until(() => fixture.Reader.SelectedText == anchor.Text);
        scroll.Offset = reverse ? default : new Vector(0, Math.Max(0, DocumentPoint(1, lastCharacter).Y - scroll.Viewport.Height / 2));
        await fixture.Idle();
        ScopedInput(() => fixture.Window.MouseMove(WindowPoint(focusPage, focus)));
        await Until(() => fixture.Reader.SelectedText == expected);
        ScopedInput(() => fixture.Window.MouseUp(WindowPoint(focusPage, focus), MouseButton.Left));
        Assert.Contains("Hello PDF reader", fixture.Reader.SelectedText);
        Assert.Contains("hello HELLO helloWorld", fixture.Reader.SelectedText);
        Assert.Contains("Second page hello", fixture.Reader.SelectedText);
        await fixture.Reader.CopySelectionToClipboardAsync();
        Assert.Equal(expected, await fixture.Window.Clipboard!.TryGetTextAsync());
        Assert.Null(fixture.Reader.ErrorMessage);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Complex_text_selection_keeps_cjk_and_surrogate_pairs_intact(bool reverse) => HeadlessTestScope.Run(async () =>
    {
        var bytes = PdfFixture.UnicodeAndCropDocument();
        using var native = await PdfDocument.LoadAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        var text = await native.GetTextAsync(0, cancellationToken: TestContext.Current.CancellationToken);
        var first = text.Characters.First(character => character.Text == "A");
        var last = text.Characters.Last(character => character.Text == "é");
        var expected = string.Concat(text.Characters
            .Where(character => character.Index >= first.Index && character.Index <= last.Index)
            .Select(character => character.Text));
        Assert.Equal("A中文🚀Bé", expected);
        using var fixture = await ViewerFixture.Create(bytes);
        fixture.Reader.ZoomLevel = 1;
        await fixture.Idle();
        var presenter = Assert.IsType<PdfDocumentPresenter>(fixture.Reader.DocumentPresenter);
        var page = presenter.GetPageBounds(0);
        var scale = fixture.Reader.ZoomLevel * 96 / 72;
        Point Map(PdfTextCharacter character) => presenter.TranslatePoint(
            new Point(page.X + character.Bounds.Center.X * scale, page.Y + character.Bounds.Center.Y * scale),
            fixture.Window)!.Value;

        var anchor = reverse ? last : first;
        var focus = reverse ? first : last;
        ScopedInput(() => fixture.Window.MouseDown(Map(anchor), MouseButton.Left));
        await Until(() => fixture.Reader.SelectedText == anchor.Text);
        ScopedInput(() => fixture.Window.MouseMove(Map(focus)));
        await Until(() => fixture.Reader.SelectedText == expected);
        ScopedInput(() => fixture.Window.MouseUp(Map(focus), MouseButton.Left));
        Assert.Equal(expected, fixture.Reader.SelectedText);
        await fixture.Reader.CopySelectionToClipboardAsync();
        Assert.Equal(expected, await fixture.Window.Clipboard!.TryGetTextAsync());
    });

    [Fact]
    public Task Large_range_selection_across_many_pages_copies_the_whole_range() => HeadlessTestScope.Run(async () =>
    {
        const int PageCount = 30;
        var bytes = PdfFixture.ManyPages(PageCount);
        using var native = await PdfDocument.LoadAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
        var firstText = await native.GetTextAsync(0, cancellationToken: TestContext.Current.CancellationToken);
        var lastText = await native.GetTextAsync(PageCount - 1, cancellationToken: TestContext.Current.CancellationToken);
        var anchor = firstText.Characters.First(character => !string.IsNullOrWhiteSpace(character.Text));
        var focus = lastText.Characters.Last(character => !string.IsNullOrWhiteSpace(character.Text));
        var expectedPages = new List<string>(PageCount);
        for (var pageIndex = 0; pageIndex < PageCount; pageIndex++)
        {
            var pageText = await native.GetTextAsync(pageIndex, cancellationToken: TestContext.Current.CancellationToken);
            expectedPages.Add(string.Concat(pageText.Characters
                .Where(character => (pageIndex > 0 || character.Index >= anchor.Index) &&
                    (pageIndex < PageCount - 1 || character.Index <= focus.Index))
                .Select(character => character.Text)));
        }
        var expected = string.Join(Environment.NewLine, expectedPages);

        using var fixture = await ViewerFixture.Create(bytes);
        fixture.Reader.IsToolbarVisible = false;
        fixture.Reader.ZoomLevel = 1;
        await fixture.Idle();
        var presenter = Assert.IsType<PdfDocumentPresenter>(fixture.Reader.DocumentPresenter);
        var scroll = presenter.GetVisualAncestors().OfType<ScrollViewer>().First();

        Point DocumentPoint(int pageIndex, PdfTextCharacter character)
        {
            var page = presenter.GetPageBounds(pageIndex);
            var scale = fixture.Reader.ZoomLevel * 96 / 72;
            return new Point(page.X + character.Bounds.Center.X * scale, page.Y + character.Bounds.Center.Y * scale);
        }

        Point WindowPoint(int pageIndex, PdfTextCharacter character)
        {
            var point = presenter.TranslatePoint(DocumentPoint(pageIndex, character), fixture.Window)!.Value;
            Assert.True(new Rect(fixture.Window.ClientSize).Contains(point));
            return point;
        }

        scroll.Offset = default;
        await fixture.Idle();
        ScopedInput(() => fixture.Window.MouseDown(WindowPoint(0, anchor), MouseButton.Left));
        await Until(() => fixture.Reader.SelectedText == anchor.Text);
        scroll.Offset = new Vector(0, Math.Max(0, DocumentPoint(PageCount - 1, focus).Y - scroll.Viewport.Height / 2));
        await fixture.Idle();
        ScopedInput(() => fixture.Window.MouseMove(WindowPoint(PageCount - 1, focus)));
        await Until(() => fixture.Reader.SelectedText == expected);
        ScopedInput(() => fixture.Window.MouseUp(WindowPoint(PageCount - 1, focus), MouseButton.Left));
        Assert.Equal(expected, fixture.Reader.SelectedText);
        Assert.Contains("Page 1", fixture.Reader.SelectedText);
        Assert.Contains($"Page {PageCount}", fixture.Reader.SelectedText);
        await fixture.Reader.CopySelectionToClipboardAsync();
        Assert.Equal(expected, await fixture.Window.Clipboard!.TryGetTextAsync());
        Assert.Null(fixture.Reader.ErrorMessage);
    });

    [Fact]
    public Task Zoom_and_rotation_preserve_the_PDF_point_at_the_viewport_center() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await ViewerFixture.Create(PdfFixture.SinglePage("A4 reading position", width: 595, height: 842));
        fixture.Reader.ZoomLevel = 2;
        await fixture.Idle();
        var presenter = Assert.IsType<PdfDocumentPresenter>(fixture.Reader.DocumentPresenter);
        var scroll = presenter.GetVisualAncestors().OfType<ScrollViewer>().First();
        var target = presenter.GetAnchorPosition(0, new Point(310, 430));
        scroll.Offset = new Vector(target.X - scroll.Viewport.Width / 2, target.Y - scroll.Viewport.Height / 2);
        await fixture.Idle();

        (int Page, Point Position) CenterAnchor() => presenter.CaptureAnchor(
            new Point(scroll.Offset.X + scroll.Viewport.Width / 2, scroll.Offset.Y + scroll.Viewport.Height / 2))
            ?? throw new InvalidOperationException("The viewport center must be inside the A4 page.");

        var initial = CenterAnchor();
        Assert.Equal(0, initial.Page);
        Assert.InRange(initial.Position.X, 309, 311);
        Assert.InRange(initial.Position.Y, 429, 431);
        Assert.True(scroll.Offset.Y > 100);
        Action[] changes = [fixture.Reader.ZoomIn, fixture.Reader.ZoomIn, fixture.Reader.RotateRight,
            fixture.Reader.RotateRight, fixture.Reader.RotateLeft, fixture.Reader.ZoomOut];
        foreach (var change in changes)
        {
            change();
            await fixture.Idle();
            var anchor = CenterAnchor();
            Assert.Equal(initial.Page, anchor.Page);
            Assert.InRange(anchor.Position.X, initial.Position.X - .75, initial.Position.X + .75);
            Assert.InRange(anchor.Position.Y, initial.Position.Y - .75, initial.Position.Y + .75);
            Assert.True(scroll.Offset.Y > 100);
            Assert.Null(fixture.Reader.ErrorMessage);
        }
    });

    [Fact]
    public Task Replacing_the_template_disconnects_old_buttons_and_releases_its_page_cache() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await ViewerFixture.Create(PdfFixture.ReadingDocument());
        var oldPresenter = Assert.IsType<PdfDocumentPresenter>(fixture.Reader.DocumentPresenter);
        var oldNext = fixture.Reader.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_NextPage");
        fixture.Reader.Template = new FuncControlTemplate<Reader>((_, scope) =>
        {
            var presenter = new PdfDocumentPresenter();
            var scroll = new ScrollViewer { Content = presenter };
            scope.Register("PART_ScrollViewer", scroll);
            scope.Register("PART_DocumentPresenter", presenter);
            return scroll;
        });
        await fixture.Idle();
        Assert.NotSame(oldPresenter, fixture.Reader.DocumentPresenter);
        Assert.Equal(0, oldPresenter.CachedBytes);
        oldNext.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(1, fixture.Reader.CurrentPage);
        fixture.Reader.NextPage();
        Assert.Equal(2, fixture.Reader.CurrentPage);
        await fixture.Idle();
        Assert.Equal(2, fixture.Reader.CurrentPage);
        Assert.True(fixture.Reader.CachedPageCount > 0);
    });

    private static async Task Until(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            HeadlessTestScope.Drain();
            await Task.Delay(10, timeout.Token);
        }
    }

    private static void ScopedInput(Action input)
    {
        var caller = ExecutionContext.Capture();
        if (caller is null) input();
        else ExecutionContext.Run(caller, _ => input(), null);
    }

    private static async Task IgnoreCancellation(Task task)
    {
        try { await task; }
        catch (OperationCanceledException) { }
    }

    private sealed class ViewerFixture : IDisposable
    {
        internal Reader Reader { get; } = new() { IsSidebarVisible = false };
        internal Window Window { get; }

        internal ViewerFixture()
        {
            Window = new Window { Width = 1000, Height = 700, Content = Reader };
            Window.Show();
            Window.UpdateLayout();
            HeadlessTestScope.Drain();
        }

        internal static async Task<ViewerFixture> Create(byte[] bytes)
        {
            var fixture = new ViewerFixture();
            try
            {
                await fixture.Reader.LoadDocumentAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
                await fixture.Idle();
                return fixture;
            }
            catch { fixture.Dispose(); throw; }
        }

        internal async Task Idle()
        {
            Window.UpdateLayout();
            HeadlessTestScope.Drain();
            await Reader.WaitForIdleAsync(TestContext.Current.CancellationToken);
            Window.UpdateLayout();
            HeadlessTestScope.Drain();
        }

        internal void Click(string name)
        {
            var button = Reader.GetVisualDescendants().OfType<Button>().Single(button => button.Name == name);
            Assert.True(button.IsEnabled);
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }

        public void Dispose()
        {
            Reader.Dispose();
            Window.Close();
        }
    }
}
