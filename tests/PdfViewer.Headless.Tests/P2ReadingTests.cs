using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using PdfViewer.Tests;
using NT.AvaloniaControl.PdfViewer;
using Reader = NT.AvaloniaControl.PdfViewer.PdfViewer;

namespace PdfViewer.Headless.Tests;

public sealed class P2ReadingTests
{
    [Fact]
    public Task Single_page_and_two_page_modes_show_only_the_active_page_or_spread() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await P2Fixture.Create(PdfFixture.ManyPages(6));
        var presenter = fixture.Presenter;
        void AssertShown(params int[] indexes)
        {
            for (var i = 0; i < 6; i++)
            {
                var bounds = presenter.GetPageBounds(i);
                if (indexes.Contains(i)) Assert.True(bounds.Width > 0 && bounds.Height > 0, $"page {i + 1} should be shown");
                else Assert.True(bounds.Width == 0 && bounds.Height == 0, $"page {i + 1} should be hidden");
            }
        }

        fixture.Reader.PageDisplayMode = PdfPageDisplayMode.SinglePage;
        fixture.Reader.GoToPage(3);
        await fixture.Idle();
        AssertShown(2);
        fixture.Reader.GoToPage(4);
        await fixture.Idle();
        AssertShown(3);

        fixture.Reader.PageDisplayMode = PdfPageDisplayMode.TwoPage;
        fixture.Reader.ShowCoverPage = true;
        fixture.Reader.GoToPage(2);
        await fixture.Idle();
        AssertShown(1, 2);
        // The spread is laid out side by side.
        Assert.True(presenter.GetPageBounds(1).Right <= presenter.GetPageBounds(2).X);

        fixture.Reader.ShowCoverPage = false;
        fixture.Reader.GoToPage(1);
        await fixture.Idle();
        AssertShown(0, 1);

        fixture.Reader.PageDisplayMode = PdfPageDisplayMode.TwoPageContinuous;
        fixture.Reader.ShowCoverPage = true;
        await fixture.Idle();
        for (var i = 0; i < 6; i++) Assert.True(presenter.GetPageBounds(i).Width > 0);
        // Cover alone, then (2,3) and (4,5) rows.
        Assert.True(presenter.GetPageBounds(0).Bottom <= presenter.GetPageBounds(1).Top);
        Assert.True(presenter.GetPageBounds(1).Right <= presenter.GetPageBounds(2).X);
        Assert.True(presenter.GetPageBounds(3).Right <= presenter.GetPageBounds(4).X);
    });

    [Fact]
    public Task Two_page_navigation_steps_by_spread_and_keys_follow_the_contract() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await P2Fixture.Create(PdfFixture.ManyPages(6));
        fixture.Reader.PageDisplayMode = PdfPageDisplayMode.TwoPage;
        fixture.Reader.ShowCoverPage = true;
        await fixture.Idle();
        fixture.Reader.GoToPage(1);
        Assert.Equal(1, fixture.Reader.CurrentPage);
        fixture.Reader.NextPage();
        Assert.Equal(2, fixture.Reader.CurrentPage);
        fixture.Reader.NextPage();
        Assert.Equal(4, fixture.Reader.CurrentPage);
        fixture.Reader.NextPage();
        Assert.Equal(6, fixture.Reader.CurrentPage);
        fixture.Reader.NextPage();
        Assert.Equal(6, fixture.Reader.CurrentPage);
        fixture.Reader.PreviousPage();
        Assert.Equal(4, fixture.Reader.CurrentPage);

        fixture.RaiseKey(Key.Right);
        Assert.Equal(6, fixture.Reader.CurrentPage);
        fixture.RaiseKey(Key.Left);
        Assert.Equal(4, fixture.Reader.CurrentPage);
        fixture.RaiseKey(Key.Home);
        Assert.Equal(1, fixture.Reader.CurrentPage);
        fixture.RaiseKey(Key.End);
        Assert.Equal(6, fixture.Reader.CurrentPage);
    });

    [Fact]
    public Task Keyboard_zoom_shortcuts_and_text_input_priority_match_the_documented_order() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await P2Fixture.Create(PdfFixture.ManyPages(3));
        fixture.Reader.ZoomLevel = 1;
        fixture.RaiseKey(Key.D2, KeyModifiers.Control);
        Assert.Equal(PdfZoomMode.FitWidth, fixture.Reader.ZoomMode);
        fixture.RaiseKey(Key.D1, KeyModifiers.Control);
        Assert.Equal(PdfZoomMode.FitPage, fixture.Reader.ZoomMode);
        fixture.Reader.ZoomLevel = 2.5;
        fixture.RaiseKey(Key.D0, KeyModifiers.Control);
        Assert.Equal(PdfZoomMode.Manual, fixture.Reader.ZoomMode);
        Assert.Equal(1, fixture.Reader.ZoomLevel);
        fixture.RaiseKey(Key.OemPlus, KeyModifiers.Control);
        Assert.True(fixture.Reader.ZoomLevel > 1);
        fixture.RaiseKey(Key.D3, KeyModifiers.Control);
        Assert.Equal(PdfZoomMode.FitHeight, fixture.Reader.ZoomMode);

        // While focus is inside the search box, page-navigation keys stay with the text input.
        var search = fixture.Reader.GetVisualDescendants().OfType<TextBox>().Single(box => box.Name == "PART_SearchBox");
        fixture.Reader.GoToPage(2);
        var before = fixture.Reader.CurrentPage;
        ScopedInput(() => search.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Right }));
        HeadlessTestScope.Drain();
        Assert.Equal(before, fixture.Reader.CurrentPage);
    });

    [Fact]
    public Task Page_labels_surface_in_properties_status_and_bookmark_titles() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await P2Fixture.Create(PdfFixture.LinksAndLabelsDocument());
        Assert.Equal(new[] { "i", "ii", "A-5", "A-6" }, fixture.Reader.PageLabels);
        Assert.Equal("i", fixture.Reader.CurrentPageLabel);
        fixture.Reader.GoToPage(3);
        await fixture.Idle();
        Assert.Equal("A-5", fixture.Reader.CurrentPageLabel);
        Assert.Contains("A-5", fixture.Reader.StatusText);
        var bookmark = fixture.Reader.AddUserBookmark();
        Assert.Equal("A-5", bookmark.Title);
        await fixture.Reader.LoadDocumentAsync(PdfFixture.ReadingDocument(), cancellationToken: TestContext.Current.CancellationToken);
        await fixture.Idle();
        Assert.Equal(2, fixture.Reader.PageLabels.Count);
        Assert.Equal("1", fixture.Reader.CurrentPageLabel);
        Assert.DoesNotContain("（", fixture.Reader.StatusText);
    });

    [Fact]
    public Task User_bookmarks_are_session_scoped_navigable_and_styled() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await P2Fixture.Create(PdfFixture.ManyPages(6));
        var reader = fixture.Reader;
        var first = reader.AddUserBookmark();
        Assert.Equal("第 1 页", first.Title);
        var second = reader.AddUserBookmark("自定义书签", 4, PdfUserBookmarkFlags.Bold | PdfUserBookmarkFlags.Italic);
        Assert.Equal(3, second.PageIndex);
        Assert.True(second.IsBold);
        Assert.True(second.IsItalic);
        Assert.Equal(2, reader.UserBookmarks.Count);
        Assert.True(reader.RenameUserBookmark(second.Id, "改名后"));
        Assert.Equal("改名后", reader.UserBookmarks[1].Title);
        Assert.True(reader.SetUserBookmarkFlags(second.Id, PdfUserBookmarkFlags.None));
        Assert.False(reader.UserBookmarks[1].IsBold);
        Assert.False(reader.RenameUserBookmark(Guid.NewGuid(), "x"));
        Assert.True(reader.RemoveUserBookmark(first.Id));
        Assert.False(reader.RemoveUserBookmark(first.Id));
        Assert.Single(reader.UserBookmarks);

        // The default template exposes the bookmarks tab, selection navigates, and commands run.
        reader.IsSidebarVisible = true;
        await fixture.Idle();
        var tabs = reader.GetVisualDescendants().OfType<TabControl>().Single();
        tabs.SelectedIndex = 2;
        await fixture.Idle();
        var list = reader.GetVisualDescendants().OfType<ListBox>().Single(control => control.Name == "PART_UserBookmarks");
        list.SelectedItem = reader.UserBookmarks[0];
        await fixture.Idle();
        Assert.Equal(4, reader.CurrentPage);
        Assert.True(reader.AddUserBookmarkCommand.CanExecute(null));
        reader.AddUserBookmarkCommand.Execute(null);
        Assert.Equal(2, reader.UserBookmarks.Count);
        Assert.True(reader.RemoveUserBookmarkCommand.CanExecute(reader.UserBookmarks[0]));
        reader.RemoveUserBookmarkCommand.Execute(reader.UserBookmarks[0]);
        Assert.Single(reader.UserBookmarks);
        var add = reader.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_AddUserBookmark");
        add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(2, reader.UserBookmarks.Count);
        var remove = reader.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_RemoveUserBookmark");
        list.SelectedItem = reader.UserBookmarks[0];
        remove.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Single(reader.UserBookmarks);

        // Bookmarks belong to the session only: closing the document clears them.
        reader.CloseDocument();
        Assert.Empty(reader.UserBookmarks);
        Assert.False(reader.AddUserBookmarkCommand.CanExecute(null));
    });

    [Fact]
    public Task Link_click_navigates_internal_destinations_with_position_and_zoom() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await P2Fixture.Create(PdfFixture.LinksAndLabelsDocument());
        await using var document = await PdfDocument.LoadAsync(PdfFixture.LinksAndLabelsDocument(),
            cancellationToken: TestContext.Current.CancellationToken);
        var reader = fixture.Reader;
        reader.ZoomLevel = 1;
        await fixture.Idle();
        var presenter = fixture.Presenter;
        PdfLinkActivatedEventArgs? activation = null;
        reader.LinkActivated += (_, args) => activation = args;
        var links = await document.GetLinksAsync(0, cancellationToken: TestContext.Current.CancellationToken);
        var xyz = links.Single(link => link.Kind == PdfLinkKind.Internal && link.Destination?.Fit == PdfDestinationFit.Xyz);
        await fixture.ClickLink(xyz);
        Assert.NotNull(activation);
        Assert.Equal(PdfLinkKind.Internal, activation!.Link.Kind);
        Assert.Equal(xyz.Bounds, activation.Link.Bounds);
        Assert.False(activation.Handled);
        Assert.Equal(2, reader.ZoomLevel);
        Assert.Equal(1, reader.CurrentPage);
        Assert.True(presenter.GetPageBounds(0).Width > 0);
        // The /XYZ position scrolls the destination point near the viewport top.
        Assert.True(reader.DocumentPresenter!.GetVisualAncestors().OfType<ScrollViewer>().First().Offset.Y > 0);
    });

    [Fact]
    public Task External_links_raise_the_event_and_never_navigate() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await P2Fixture.Create(PdfFixture.LinksAndLabelsDocument());
        await using var document = await PdfDocument.LoadAsync(PdfFixture.LinksAndLabelsDocument(),
            cancellationToken: TestContext.Current.CancellationToken);
        var reader = fixture.Reader;
        var links = await document.GetLinksAsync(0, cancellationToken: TestContext.Current.CancellationToken);
        var activations = new List<PdfLinkActivatedEventArgs>();
        reader.LinkActivated += (_, args) => activations.Add(args);

        var uri = links.Single(link => link.Kind == PdfLinkKind.Uri);
        await fixture.ClickLink(uri);
        Assert.Equal(1, reader.CurrentPage);
        Assert.Equal(uri.Target, Assert.Single(activations).Link.Target);

        var file = links.Single(link => link.Kind == PdfLinkKind.File);
        await fixture.ClickLink(file);
        Assert.Equal(1, reader.CurrentPage);
        Assert.Equal("report.pdf", activations[1].Link.Target);

        var unsupported = links.Single(link => link.Kind == PdfLinkKind.Unsupported);
        await fixture.ClickLink(unsupported);
        Assert.Equal(1, reader.CurrentPage);
        Assert.Equal(3, activations.Count);

        // A host can suppress even internal navigation through Handled.
        var internalLink = links.Single(link => link.Kind == PdfLinkKind.Internal && link.Destination?.Fit == PdfDestinationFit.Fit);
        reader.LinkActivated += (_, args) => args.Handled = true;
        await fixture.ClickLink(internalLink);
        Assert.Equal(1, reader.CurrentPage);
    });

    [Fact]
    public Task Touch_pinch_zooms_around_the_pinch_origin() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await P2Fixture.Create(PdfFixture.ManyPages(3));
        fixture.Reader.ZoomLevel = 1;
        await fixture.Idle();
        var presenter = fixture.Presenter;
        var origin = presenter.GetPageBounds(0).Center;
        // PinchEventArgs.Scale is cumulative from the gesture start (PinchGestureRecognizer).
        ScopedInput(() => presenter.RaiseEvent(new PinchEventArgs(1.5, origin)));
        HeadlessTestScope.Drain();
        Assert.Equal(1.5, fixture.Reader.ZoomLevel, 3);
        ScopedInput(() => presenter.RaiseEvent(new PinchEventArgs(2, origin)));
        HeadlessTestScope.Drain();
        // Still the same gesture: the total multiplier is applied to the zoom at its start.
        Assert.Equal(2, fixture.Reader.ZoomLevel, 3);
        ScopedInput(() => presenter.RaiseEvent(new PinchEndedEventArgs()));
        HeadlessTestScope.Drain();
        // ZoomLevel is clamped to the documented 10–500% range.
        ScopedInput(() => presenter.RaiseEvent(new PinchEventArgs(10, origin)));
        HeadlessTestScope.Drain();
        Assert.Equal(5, fixture.Reader.ZoomLevel, 3);
    });

    [Fact]
    public Task FitHeight_sizes_the_page_height_to_the_viewport() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await P2Fixture.Create(PdfFixture.ManyPages(3));
        fixture.Reader.FitHeight();
        await fixture.Idle();
        Assert.Equal(PdfZoomMode.FitHeight, fixture.Reader.ZoomMode);
        var scroll = fixture.Presenter.GetVisualAncestors().OfType<ScrollViewer>().First();
        // The page display height equals the viewport minus the layout gap budget, ignoring width.
        Assert.Equal(scroll.Viewport.Height - PdfPageLayout.Gap * 2, fixture.Presenter.GetPageBounds(0).Height, 1);
        Assert.True(fixture.Reader.FitHeightCommand.CanExecute(null));
        // The default template exposes the FitHeight button next to FitWidth / FitPage.
        var button = fixture.Reader.GetVisualDescendants().OfType<Button>().Single(control => control.Name == "PART_FitHeight");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await fixture.Idle();
        Assert.Equal(PdfZoomMode.FitHeight, fixture.Reader.ZoomMode);
    });

    [Fact]
    public Task Zoom_limits_and_step_are_configurable() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await P2Fixture.Create(PdfFixture.ManyPages(3));
        var reader = fixture.Reader;
        reader.ZoomMode = PdfZoomMode.Manual;
        reader.ZoomLevel = 1;
        reader.MinimumZoom = 0.5;
        reader.MaximumZoom = 2;
        reader.ZoomStep = 1.5;
        await fixture.Idle();

        reader.ZoomIn();
        Assert.Equal(1.5, reader.ZoomLevel, 3);
        reader.ZoomIn();
        Assert.Equal(2, reader.ZoomLevel, 3);
        Assert.False(reader.ZoomInCommand.CanExecute(null));
        Assert.True(reader.ZoomOutCommand.CanExecute(null));
        reader.ZoomOut();
        Assert.Equal(2 / 1.5, reader.ZoomLevel, 3);
        reader.ZoomLevel = 3;
        Assert.Equal(2, reader.ZoomLevel, 3);
        reader.ZoomLevel = 0.1;
        Assert.Equal(0.5, reader.ZoomLevel, 3);

        // Limits move ZoomLevel with them; a minimum above the maximum raises the maximum.
        reader.ZoomLevel = 1.8;
        reader.MaximumZoom = 1;
        Assert.Equal(1, reader.ZoomLevel, 3);
        reader.MinimumZoom = 1.6;
        Assert.Equal(1.6, reader.MaximumZoom, 3);
        Assert.Equal(1.6, reader.ZoomLevel, 3);

        // A fresh control keeps the documented 10–500% defaults and 1.2 step.
        using var fresh = new Reader();
        Assert.Equal(0.1, fresh.MinimumZoom, 3);
        Assert.Equal(5, fresh.MaximumZoom, 3);
        Assert.Equal(1.2, fresh.ZoomStep, 3);
    });

    [Fact]
    public Task Fit_destination_navigation_honors_zoom_and_edge_coordinates() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await P2Fixture.Create(PdfFixture.FitDestinationsDocument());
        var reader = fixture.Reader;
        reader.ZoomMode = PdfZoomMode.Manual;
        reader.ZoomLevel = 1;
        await fixture.Idle();
        var scroll = fixture.Presenter.GetVisualAncestors().OfType<ScrollViewer>().First();

        // /FitH fits the width and pins the destination top edge at the window top.
        reader.NavigateToDestination(new PdfLinkDestination(0, null, null, PdfDestinationFit.FitH, Top: 100));
        await fixture.Idle();
        Assert.Equal(PdfZoomMode.FitWidth, reader.ZoomMode);
        Assert.Equal(1, reader.CurrentPage);
        var scale = reader.ZoomLevel * 96 / 72;
        var page = fixture.Presenter.GetPageBounds(0);
        Assert.Equal(page.Y + 100 * scale, scroll.Offset.Y, 1);

        // /FitV fits the height and pins the destination left edge at the window left. Page 3 is
        // landscape, so fit height keeps a horizontal range and the pin is actually scrollable.
        reader.NavigateToDestination(new PdfLinkDestination(2, null, null, PdfDestinationFit.FitV, Left: 50));
        await fixture.Idle();
        Assert.Equal(PdfZoomMode.FitHeight, reader.ZoomMode);
        Assert.Equal(3, reader.CurrentPage);
        scale = reader.ZoomLevel * 96 / 72;
        page = fixture.Presenter.GetPageBounds(2);
        Assert.True(scroll.Extent.Width > scroll.Viewport.Width, "FitV needs horizontal scroll room to pin the left edge.");
        Assert.Equal(page.X + 50 * scale, scroll.Offset.X, 1);

        // /FitB / /FitBH fit the page's content box (page box when the page has no content) at a
        // manual zoom. Page 3's text sits above its page box, so its content box is empty and the
        // /FitB navigation falls back to the landscape page box. The /FitBH target is page 1 so
        // the pin has content below it and is not clamped by the document end.
        var fitBZoom = Math.Min((scroll.Viewport.Width - (PdfPageLayout.Gap * 2 + 4)) / (800 * 96d / 72),
            (scroll.Viewport.Height - PdfPageLayout.Gap * 2) / (300 * 96d / 72));
        fitBZoom = Math.Clamp(fitBZoom, reader.MinimumZoom, reader.MaximumZoom);
        reader.NavigateToDestination(new PdfLinkDestination(2, null, null, PdfDestinationFit.FitB));
        await Until(() => reader.ZoomMode == PdfZoomMode.Manual && Math.Abs(reader.ZoomLevel - fitBZoom) < .001);
        await fixture.Idle();
        Assert.Equal(3, reader.CurrentPage);
        // Page 1's content box is its text run; compute it independently from the same bytes.
        await using var boxDocument = await PdfDocument.LoadAsync(PdfFixture.FitDestinationsDocument(),
            cancellationToken: TestContext.Current.CancellationToken);
        var contentBox = await boxDocument.GetPageContentBoundsAsync(0, cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(contentBox);
        var fitBHZoom = Math.Clamp((scroll.Viewport.Width - (PdfPageLayout.Gap * 2 + 4)) / (contentBox!.Value.Width * 96d / 72),
            reader.MinimumZoom, reader.MaximumZoom);
        reader.NavigateToDestination(new PdfLinkDestination(0, null, null, PdfDestinationFit.FitBH, Top: 100));
        await Until(() => reader.ZoomMode == PdfZoomMode.Manual && Math.Abs(reader.ZoomLevel - fitBHZoom) < .001);
        await fixture.Idle();
        Assert.Equal(1, reader.CurrentPage);
        scale = reader.ZoomLevel * 96 / 72;
        page = fixture.Presenter.GetPageBounds(0);
        Assert.Equal(page.Y + 100 * scale, scroll.Offset.Y, 1);

        // /FitR zooms so the rectangle just fits the window and pins its top-left exactly.
        var rectangle = new Rect(30, 50, 100, 100);
        reader.NavigateToDestination(new PdfLinkDestination(1, null, null, PdfDestinationFit.FitR, Rectangle: rectangle));
        await fixture.Idle();
        Assert.Equal(PdfZoomMode.Manual, reader.ZoomMode);
        Assert.Equal(2, reader.CurrentPage);
        var expectedZoom = Math.Min((scroll.Viewport.Width - (PdfPageLayout.Gap * 2 + 4)) / (100 * 96d / 72),
            (scroll.Viewport.Height - PdfPageLayout.Gap * 2) / (100 * 96d / 72));
        expectedZoom = Math.Clamp(expectedZoom, reader.MinimumZoom, reader.MaximumZoom);
        Assert.Equal(expectedZoom, reader.ZoomLevel, 3);
        scale = reader.ZoomLevel * 96 / 72;
        page = fixture.Presenter.GetPageBounds(1);
        Assert.Equal(page.X + 30 * scale, scroll.Offset.X, 1);
        Assert.Equal(page.Y + 50 * scale, scroll.Offset.Y, 1);

        // A FitR rectangle that would exceed the zoom limits is clamped like every other zoom.
        reader.NavigateToDestination(new PdfLinkDestination(0, null, null, PdfDestinationFit.FitR,
            Rectangle: new Rect(0, 0, 5, 5)));
        await fixture.Idle();
        Assert.Equal(reader.MaximumZoom, reader.ZoomLevel, 3);
    });

    [Fact]
    public Task Reading_position_roundtrips_and_fitv_destinations_map_to_fit_height() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await P2Fixture.Create(PdfFixture.ManyPages(6));
        var reader = fixture.Reader;
        reader.ZoomMode = PdfZoomMode.Manual;
        reader.ZoomLevel = 1;
        await fixture.Idle();
        reader.GoToPage(3);
        await fixture.Idle();
        var scroll = fixture.Presenter.GetVisualAncestors().OfType<ScrollViewer>().First();
        // Right after navigation the viewport top-left sits in the gap above page 3; the
        // position is still attributed to the page being read.
        var atGap = reader.CaptureReadingPosition();
        Assert.NotNull(atGap);
        Assert.Equal(2, atGap!.PageIndex);
        // Scroll into page 3 so the saved anchor is not at the page top.
        scroll.Offset = new Vector(scroll.Offset.X, scroll.Offset.Y + 50);
        await fixture.Idle();
        var offset = scroll.Offset.Y;
        var saved = reader.CaptureReadingPosition();
        Assert.NotNull(saved);
        Assert.Equal(2, saved!.PageIndex);
        Assert.True(saved.PagePosition.Y > 0);
        Assert.Equal(1, saved.ZoomLevel, 3);
        Assert.Equal(PdfZoomMode.Manual, saved.ZoomMode);
        Assert.Equal(0, saved.Rotation);

        // Move away and change the view; restore re-applies zoom and the anchor offset.
        reader.ZoomLevel = 2;
        await fixture.Idle();
        reader.GoToPage(1);
        await fixture.Idle();
        Assert.Equal(1, reader.CurrentPage);
        reader.RestoreReadingPosition(saved);
        await fixture.Idle();
        Assert.Equal(PdfZoomMode.Manual, reader.ZoomMode);
        Assert.Equal(1, reader.ZoomLevel, 3);
        Assert.Equal(3, reader.CurrentPage);
        Assert.Equal(offset, scroll.Offset.Y, 1);

        // /FitV destinations use FitHeight; without a left coordinate the page stays where it is.
        reader.NavigateToDestination(new PdfLinkDestination(1, null, null, PdfDestinationFit.FitV));
        await fixture.Idle();
        Assert.Equal(PdfZoomMode.FitHeight, reader.ZoomMode);
        Assert.Equal(2, reader.CurrentPage);

        reader.CloseDocument();
        Assert.Null(reader.CaptureReadingPosition());
    });

    [Fact]
    public Task Progressive_search_publishes_per_page_and_reports_limits() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await P2Fixture.Create(PdfFixture.ManyPages(6));
        var reader = fixture.Reader;

        // Progressive publishing: every matching page appends its batch as soon as it is scanned.
        var updates = new List<int>();
        EventHandler<AvaloniaPropertyChangedEventArgs> handler = (_, e) =>
        {
            if (e.Property == NT.AvaloniaControl.PdfViewer.PdfViewer.SearchResultsProperty) updates.Add(reader.SearchResults.Count);
        };
        reader.PropertyChanged += handler;
        IReadOnlyList<PdfSearchResult> results;
        try
        {
            results = await reader.SearchAsync("Page", cancellationToken: TestContext.Current.CancellationToken);
        }
        finally { reader.PropertyChanged -= handler; }
        Assert.Equal(6, results.Count);
        Assert.Same(results, reader.SearchResults);
        Assert.Equal(6, reader.SearchResultCount);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, updates.Where(count => count > 0).ToArray());
        Assert.Equal(0, reader.CurrentSearchResultIndex);
        Assert.Equal(1, reader.SearchProgress, 3);
        Assert.False(reader.SearchResultsTruncated);

        // Reaching the limit stops the walk early and marks the result set as incomplete.
        reader.SearchResultLimit = 3;
        await reader.SearchAsync("Page", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(3, reader.SearchResultCount);
        Assert.True(reader.SearchResultsTruncated);
        Assert.True(reader.SearchProgress > 0 && reader.SearchProgress < 1);
        Assert.Contains("已达上限", reader.StatusText);

        // Clearing drops the query and every result state.
        reader.ClearSearch();
        Assert.Null(reader.SearchQuery);
        Assert.Equal(0, reader.SearchResultCount);
        Assert.Empty(reader.SearchResults);
        Assert.Equal(-1, reader.CurrentSearchResultIndex);
        Assert.Equal(0, reader.SearchProgress, 3);
        Assert.False(reader.SearchResultsTruncated);
        Assert.False(reader.IsSearching);
        Assert.DoesNotContain("搜索", reader.StatusText);
    });

    [Fact]
    public Task Page_and_document_text_extraction_and_page_image_export_run_from_the_control() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await P2Fixture.Create(PdfFixture.ReadingDocument());
        var reader = fixture.Reader;
        var page = await reader.GetPageTextAsync(0, TestContext.Current.CancellationToken);
        Assert.Contains("Hello PDF reader", page.Text);
        Assert.NotEmpty(page.Characters);
        var full = await reader.GetDocumentTextAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Hello PDF reader", full);
        Assert.Contains("Second page hello", full);
        Assert.Contains('\f', full);

        // PNG export at 96 DPI renders the displayed page size in pixels (300x400 points).
        // The independent decoder checks chunk CRCs, the zlib payload and the decoded size;
        // PngExportTests additionally compares every pixel against a reference render.
        var png = await reader.ExportPageImageAsync(0, 96, TestContext.Current.CancellationToken);
        var decoded = PngDecode.Decode(png);
        Assert.Equal(400, decoded.Width);  // 300pt * 96/72
        Assert.Equal(533, decoded.Height); // 400pt * 96/72 rounded
        Assert.Equal(400 * 533 * 4, decoded.Rgba.Length);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            reader.ExportPageImageAsync(0, 2000, TestContext.Current.CancellationToken));
    });

    private static void ScopedInput(Action input)
    {
        var caller = ExecutionContext.Capture();
        if (caller is null) input();
        else ExecutionContext.Run(caller, _ => input(), null);
    }

    // Content-box fit navigation completes asynchronously after the content bounds arrive.
    private static async Task Until(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            HeadlessTestScope.Drain();
            await Task.Delay(10, timeout.Token);
        }
    }

    private sealed class P2Fixture : IDisposable
    {
        internal Reader Reader { get; } = new();
        internal Window Window { get; }
        internal PdfDocumentPresenter Presenter => (PdfDocumentPresenter)Reader.DocumentPresenter!;

        private P2Fixture()
        {
            Window = new Window { Width = 1000, Height = 700, Content = Reader };
            Window.Show();
            Window.UpdateLayout();
            HeadlessTestScope.Drain();
        }

        internal static async Task<P2Fixture> Create(byte[] bytes)
        {
            var fixture = new P2Fixture();
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

        internal void RaiseKey(Key key, KeyModifiers modifiers = KeyModifiers.None)
        {
            ScopedInput(() => Reader.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers,
            }));
            HeadlessTestScope.Drain();
        }

        /// <summary>Waits for the presenter's link cache, then clicks the link with real pointer input.</summary>
        internal async Task ClickLink(PdfLink link)
        {
            await Idle();
            var scale = Reader.ZoomLevel * 96 / 72;
            var page = Presenter.GetPageBounds(link.PageIndex);
            var content = new Point(page.X + (link.Bounds.X + link.Bounds.Width / 2) * scale,
                page.Y + (link.Bounds.Y + link.Bounds.Height / 2) * scale);
            // Link prefetch is asynchronous; wait until hit-testing can see the annotation.
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                while (Presenter.LinkAt(content) is null)
                {
                    HeadlessTestScope.Drain();
                    await Task.Delay(10, timeout.Token);
                }
            }
            var center = Presenter.TranslatePoint(content, Window)!.Value;
            ScopedInput(() => Window.MouseDown(center, MouseButton.Left));
            ScopedInput(() => Window.MouseUp(center, MouseButton.Left));
            HeadlessTestScope.Drain();
            await Idle();
        }

        public void Dispose()
        {
            Reader.Dispose();
            Window.Close();
        }
    }
}
