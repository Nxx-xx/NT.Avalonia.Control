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

/// <summary>
/// Theme and narrow-window acceptance: sidebar offset / overlay presentations, sidebar
/// close-reopen work and selection restore, narrow windows, long translated strings, large
/// fonts, status format parameters, and the keyboard focus order. Real screen-reader and IME
/// behavior is not covered here and stays on the device-acceptance list.
/// </summary>
public sealed class ThemeAcceptanceTests
{
    [Fact]
    public Task Sidebar_offset_and_overlay_presentations_layout_as_documented() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await ThemeFixture.Create(PdfFixture.ManyPages(3));
        var reader = fixture.Reader;
        reader.IsSidebarVisible = true;
        reader.SidebarWidth = 160;
        await fixture.Idle();
        var body = fixture.Part<Grid>("PART_BodyGrid");
        var sidebar = fixture.Part<Border>("PART_Sidebar");
        var canvas = fixture.Part<Grid>("PART_CanvasGrid");

        // Offset (default): the sidebar owns its column and the document reflows beside it.
        Assert.Equal(PdfSidebarPresentation.Offset, reader.SidebarPresentation);
        Assert.Equal(160, sidebar.Bounds.Width);
        Assert.Equal(body.Bounds.Width - 160, canvas.Bounds.Width, 1);
        Assert.Equal(0, sidebar.Bounds.X, 1);

        // Overlay: the document keeps the full width and the sidebar floats above it.
        reader.SidebarPresentation = PdfSidebarPresentation.Overlay;
        await fixture.Idle();
        Assert.Equal(body.Bounds.Width, canvas.Bounds.Width, 1);
        Assert.Equal(160, sidebar.Bounds.Width);
        Assert.Equal(0, sidebar.Bounds.X, 1);
        Assert.True(sidebar.ZIndex > canvas.ZIndex);
        Assert.True(reader.HasDocument);

        // Right placement flips the floating side; switching back to offset reflows again.
        reader.SidebarPlacement = PdfSidebarPlacement.Right;
        await fixture.Idle();
        Assert.Equal(body.Bounds.Width - 160, sidebar.Bounds.X, 1);
        reader.SidebarPresentation = PdfSidebarPresentation.Offset;
        await fixture.Idle();
        Assert.Equal(body.Bounds.Width - 160, canvas.Bounds.Width, 1);
        Assert.Equal(body.Bounds.Width - 160, sidebar.Bounds.X, 1);
    });

    [Fact]
    public Task Sidebar_close_releases_work_and_reopen_restores_selection() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await ThemeFixture.Create(PdfFixture.ManyPages(4));
        var reader = fixture.Reader;
        reader.IsSidebarVisible = true;
        await fixture.Idle();
        var thumbnails = fixture.Part<ListBox>("PART_Thumbnails");

        // Selecting a thumbnail navigates and stays the sidebar's selection state.
        thumbnails.SelectedIndex = 2;
        HeadlessTestScope.Drain();
        await fixture.Idle();
        Assert.Equal(3, reader.CurrentPage);
        Assert.Equal(2, thumbnails.SelectedIndex);

        // Closing releases the sidebar's nonessential decode work; reopening restores the state.
        reader.IsSidebarVisible = false;
        HeadlessTestScope.Drain();
        reader.IsSidebarVisible = true;
        await fixture.Idle();
        Assert.Equal(2, thumbnails.SelectedIndex);
        Assert.Equal(3, reader.CurrentPage);
    });

    [Fact]
    public Task Narrow_window_with_a_small_sidebar_stays_usable() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await ThemeFixture.Create(PdfFixture.ManyPages(4), width: 420, height: 640);
        var reader = fixture.Reader;
        reader.SidebarWidth = 120;
        reader.IsSidebarVisible = true;
        await fixture.Idle();
        var body = fixture.Part<Grid>("PART_BodyGrid");
        Assert.True(body.Bounds.Width <= 420, $"body {body.Bounds.Width} must fit the window");
        Assert.True(fixture.Part<Grid>("PART_CanvasGrid").Bounds.Width > 0);

        // Navigation and zoom keep working in the narrow window.
        reader.GoToPage(3);
        await fixture.Idle();
        Assert.Equal(3, reader.CurrentPage);
        Assert.True(fixture.Presenter.GetPageBounds(2).Width > 0);
        var beforeZoom = reader.ZoomLevel;
        reader.ZoomIn();
        Assert.True(reader.ZoomLevel > beforeZoom);

        // Overlay keeps the document width even with the sidebar open — the narrow-window mode.
        reader.SidebarPresentation = PdfSidebarPresentation.Overlay;
        await fixture.Idle();
        Assert.Equal(body.Bounds.Width, fixture.Part<Grid>("PART_CanvasGrid").Bounds.Width, 1);
        reader.GoToPage(2);
        await fixture.Idle();
        Assert.Equal(2, reader.CurrentPage);

        // Tool groups collapse on demand so the remaining controls stay reachable.
        reader.IsSearchToolVisible = false;
        reader.IsDisplayModeToolVisible = false;
        reader.IsBookmarkToolVisible = false;
        await fixture.Idle();
        reader.GoToPage(4);
        await fixture.Idle();
        Assert.Equal(4, reader.CurrentPage);
    });

    [Fact]
    public Task Long_translated_strings_and_large_fonts_still_render() => HeadlessTestScope.Run(async () =>
    {
        var saved = (PdfViewerStrings.OpenText, PdfViewerStrings.OpenTooltip, PdfViewerStrings.EmptyTitle,
            PdfViewerStrings.EmptyMessage, PdfViewerStrings.SearchPlaceholder, PdfViewerStrings.CopyText,
            PdfViewerStrings.StatusPageFormat, PdfViewerStrings.PasswordPromptTitle, PdfViewerStrings.PasswordPromptMessage);
        try
        {
            // Hosts replace the table before creating reader UI; the template reads it at build.
            PdfViewerStrings.OpenText = new string('开', 80);
            PdfViewerStrings.OpenTooltip = new string('提', 120);
            PdfViewerStrings.EmptyTitle = new string('标', 60);
            PdfViewerStrings.EmptyMessage = new string('说', 200);
            PdfViewerStrings.SearchPlaceholder = new string('搜', 100);
            PdfViewerStrings.CopyText = new string('复', 60);
            PdfViewerStrings.PasswordPromptTitle = new string('密', 60);
            PdfViewerStrings.PasswordPromptMessage = new string('文', 200);
            // A composite format may reorder its arguments.
            PdfViewerStrings.StatusPageFormat = "页码 {1}←{0}";
            using var fixture = await ThemeFixture.Create(PdfFixture.ManyPages(3));
            var reader = fixture.Reader;
            fixture.Window.FontSize = 28;
            await fixture.Idle();

            Assert.Equal(new string('开', 80), (string?)fixture.Part<Button>("PART_Open").Content);
            Assert.Equal(new string('搜', 100), fixture.Part<TextBox>("PART_SearchBox").PlaceholderText);
            reader.GoToPage(2);
            await fixture.Idle();
            Assert.Contains("页码 3←2", reader.StatusText);

            // The reader still operates with the long strings and the large inherited font.
            await reader.SearchAsync("Page", TestContext.Current.CancellationToken);
            Assert.Equal(3, reader.SearchResultCount);
            reader.GoToPage(3);
            Assert.Equal(3, reader.CurrentPage);
        }
        finally
        {
            (PdfViewerStrings.OpenText, PdfViewerStrings.OpenTooltip, PdfViewerStrings.EmptyTitle,
                PdfViewerStrings.EmptyMessage, PdfViewerStrings.SearchPlaceholder, PdfViewerStrings.CopyText,
                PdfViewerStrings.StatusPageFormat, PdfViewerStrings.PasswordPromptTitle,
                PdfViewerStrings.PasswordPromptMessage) = saved;
        }
    });

    [Fact]
    public Task Keyboard_focus_order_walks_the_toolbar_with_real_tab_keys() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await ThemeFixture.Create(PdfFixture.ManyPages(3));
        var reader = fixture.Reader;
        await fixture.Idle();
        reader.Focus();
        HeadlessTestScope.Drain();

        // Real Tab presses walk the template's declaration order; every stop stays inside the reader.
        var visited = new List<string>();
        for (var i = 0; i < 30; i++)
        {
            fixture.Press(Key.Tab);
            if (fixture.FocusedElement() is not { } focused) continue;
            Assert.True(focused is Visual visual && (ReferenceEquals(visual, reader) || visual.GetVisualAncestors().Contains(reader)),
                $"focus stop {i} ({focused.GetType().Name}) must stay inside the reader");
            var name = NamedAncestor(focused);
            if (name is not null && !visited.Contains(name)) visited.Add(name);
        }
        Assert.Contains("PART_Open", visited);
        Assert.Contains("PART_PageNumber", visited);
        Assert.Contains("PART_SearchBox", visited);
        Assert.True(visited.IndexOf("PART_Open") < visited.IndexOf("PART_PageNumber"),
            $"toolbar order: [{string.Join(", ", visited)}]");
        Assert.True(visited.IndexOf("PART_PageNumber") < visited.IndexOf("PART_SearchBox"),
            $"search row after navigation: [{string.Join(", ", visited)}]");
    });

    private static string? NamedAncestor(object element)
    {
        // The outermost PART_ below the reader: composite inputs keep their own PART_ names inside.
        string? found = null;
        for (var current = element as Visual; current is not null; current = current.GetVisualParent())
        {
            if (current is Reader) break;
            if (current is Control { Name: { Length: > 0 } name } && name.StartsWith("PART_", StringComparison.Ordinal)) found = name;
        }
        return found;
    }

    private static void ScopedInput(Action input)
    {
        var caller = ExecutionContext.Capture();
        if (caller is null) input();
        else ExecutionContext.Run(caller, _ => input(), null);
    }

    private sealed class ThemeFixture : IDisposable
    {
        internal Reader Reader { get; } = new();
        internal Window Window { get; }
        internal PdfDocumentPresenter Presenter => (PdfDocumentPresenter)Reader.DocumentPresenter!;

        private ThemeFixture(double width, double height)
        {
            Window = new Window { Width = width, Height = height, Content = Reader };
            Window.Show();
            Window.UpdateLayout();
            HeadlessTestScope.Drain();
        }

        internal static async Task<ThemeFixture> Create(byte[] bytes, double width = 1000, double height = 700)
        {
            var fixture = new ThemeFixture(width, height);
            try
            {
                await fixture.Reader.LoadDocumentAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
                await fixture.Idle();
                return fixture;
            }
            catch { fixture.Dispose(); throw; }
        }

        internal T Part<T>(string name) where T : Control =>
            (T)Reader.GetVisualDescendants().OfType<Control>().Single(control => control.Name == name);

        internal object? FocusedElement() => Window.FocusManager?.GetFocusedElement();

        internal void Press(Key key, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            ScopedInput(() =>
            {
                Window.KeyPress(key, modifiers, PhysicalKey.None, null);
                Window.KeyRelease(key, modifiers, PhysicalKey.None, null);
            });
            Window.UpdateLayout();
            HeadlessTestScope.Drain();
        }

        internal async Task Idle()
        {
            Window.UpdateLayout();
            HeadlessTestScope.Drain();
            await Reader.WaitForIdleAsync(TestContext.Current.CancellationToken);
            Window.UpdateLayout();
            HeadlessTestScope.Drain();
        }

        public void Dispose()
        {
            Reader.Dispose();
            Window.Close();
        }
    }
}
