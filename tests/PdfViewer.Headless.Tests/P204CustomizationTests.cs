using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PdfViewer.Tests;
using NT.AvaloniaControl.PdfViewer;
using NT.AvaloniaControl.PdfViewer.Themes;
using Reader = NT.AvaloniaControl.PdfViewer.PdfViewer;

namespace PdfViewer.Headless.Tests;

public sealed class P204CustomizationTests
{
    [Fact]
    public Task Tool_visibility_switches_drive_the_default_template() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await P204Fixture.Create(PdfFixture.ManyPages(2));
        var reader = fixture.Reader;
        Button Part(string name) => reader.GetVisualDescendants().OfType<Button>().Single(b => b.Name == name);

        // Default: every group is present.
        Assert.True(Part("PART_Open").IsVisible);
        Assert.True(Part("PART_NextPage").IsVisible);
        Assert.True(Part("PART_ZoomIn").IsVisible);

        reader.IsOpenToolVisible = false;
        Assert.False(Part("PART_Open").IsVisible);
        Assert.True(Part("PART_Close").IsVisible);

        reader.IsNavigationToolVisible = false;
        Assert.False(Part("PART_NextPage").IsVisible);
        Assert.False(reader.GetVisualDescendants().OfType<NumericUpDown>().Single(n => n.Name == "PART_PageNumber").IsVisible);
        Assert.True(Part("PART_ZoomIn").IsVisible);

        reader.IsZoomToolVisible = false;
        Assert.False(Part("PART_ZoomIn").IsVisible);
        reader.IsFitToolVisible = false;
        Assert.False(Part("PART_FitPage").IsVisible);
        reader.IsRotateToolVisible = false;
        Assert.False(Part("PART_RotateRight").IsVisible);
        reader.IsCopyToolVisible = false;
        Assert.False(Part("PART_Copy").IsVisible);
        reader.IsCloseToolVisible = false;
        Assert.False(Part("PART_Close").IsVisible);
        reader.IsDisplayModeToolVisible = false;
        Assert.False(reader.GetVisualDescendants().OfType<ComboBox>().Single(c => c.Name == "PART_DisplayMode").IsVisible);
        reader.IsBookmarkToolVisible = false;
        Assert.False(Part("PART_AddUserBookmark").IsVisible);
        reader.IsSidebarToggleVisible = false;
        Assert.False(reader.GetVisualDescendants().OfType<ToggleButton>()
            .Single(t => Equals(t.Content, PdfViewerStrings.SidebarToggleText)).IsVisible);
        reader.IsSearchToolVisible = false;
        var search = reader.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "PART_SearchBox");
        Assert.False(search.GetVisualAncestors().OfType<WrapPanel>().First().IsVisible);

        // The coarse toolbar switch still hides everything at once.
        reader.IsSearchToolVisible = true;
        reader.IsToolbarVisible = false;
        await fixture.Idle();
        Assert.False(reader.GetVisualDescendants().OfType<WrapPanel>().First().GetVisualAncestors().OfType<Border>().First().IsVisible);
    });

    [Fact]
    public Task Sidebar_width_and_placement_configure_the_default_layout() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await P204Fixture.Create(PdfFixture.ManyPages(2));
        var reader = fixture.Reader;
        var sidebar = reader.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PART_Sidebar");
        var body = reader.GetVisualDescendants().OfType<Grid>().Single(g => g.Name == "PART_BodyGrid");
        var canvas = reader.GetVisualDescendants().OfType<Grid>().Single(g => g.Name == "PART_CanvasGrid");

        Assert.Equal(208, sidebar.Width);
        reader.SidebarWidth = 320;
        Assert.Equal(320, sidebar.Width);
        Assert.Throws<ArgumentException>(() => reader.SidebarWidth = -1);
        Assert.Throws<ArgumentException>(() => reader.SidebarWidth = double.NaN);

        Assert.Equal(0, Grid.GetColumn(sidebar));
        Assert.Equal(new Thickness(0, 0, 1, 0), sidebar.BorderThickness);
        reader.SidebarPlacement = PdfSidebarPlacement.Right;
        Assert.Equal(2, body.ColumnDefinitions.Count);
        Assert.True(body.ColumnDefinitions[0].Width.IsStar);
        Assert.Equal(1, Grid.GetColumn(sidebar));
        Assert.Equal(0, Grid.GetColumn(canvas));
        Assert.Equal(new Thickness(1, 0, 0, 0), sidebar.BorderThickness);
        reader.SidebarPlacement = PdfSidebarPlacement.Left;
        Assert.Equal(0, Grid.GetColumn(sidebar));
        Assert.Equal(new Thickness(0, 0, 1, 0), sidebar.BorderThickness);
        Assert.True(body.ColumnDefinitions[0].Width.IsAuto);
    });

    [Fact]
    public Task Localized_strings_replace_status_bookmarks_and_accessibility_names() => HeadlessTestScope.Run(async () =>
    {
        var saved = (PdfViewerStrings.StatusNoDocumentText, PdfViewerStrings.BookmarkPageTitleFormat,
            PdfViewerStrings.ZoomInAccessibleName, PdfViewerStrings.SearchMatchCountSuffix);
        try
        {
            PdfViewerStrings.StatusNoDocumentText = "No document open";
            PdfViewerStrings.BookmarkPageTitleFormat = "Page {0}";
            PdfViewerStrings.ZoomInAccessibleName = "Zoom in";
            PdfViewerStrings.SearchMatchCountSuffix = "matches";
            using var fixture = await P204Fixture.Create(PdfFixture.ManyPages(3), load: false);
            Assert.Equal("No document open", fixture.Reader.StatusText);
            using var loaded = await P204Fixture.Create(PdfFixture.ManyPages(3));
            loaded.Reader.GoToPage(3);
            loaded.Reader.AddUserBookmark();
            Assert.Equal("Page 3", loaded.Reader.UserBookmarks.Single().Title);
            var zoomIn = loaded.Reader.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "PART_ZoomIn");
            Assert.Equal("Zoom in", AutomationProperties.GetName(zoomIn));
            var suffix = loaded.Reader.GetVisualDescendants().OfType<TextBlock>()
                .Single(t => t.Text == "matches");
            Assert.NotNull(suffix);
        }
        finally
        {
            PdfViewerStrings.StatusNoDocumentText = saved.StatusNoDocumentText;
            PdfViewerStrings.BookmarkPageTitleFormat = saved.BookmarkPageTitleFormat;
            PdfViewerStrings.ZoomInAccessibleName = saved.ZoomInAccessibleName;
            PdfViewerStrings.SearchMatchCountSuffix = saved.SearchMatchCountSuffix;
        }
    });

    [Fact]
    public Task Default_template_exposes_the_default_accessibility_names() => HeadlessTestScope.Run(async () =>
    {
        using var fixture = await P204Fixture.Create(PdfFixture.ManyPages(2));
        var reader = fixture.Reader;
        string Name(string part) => AutomationProperties.GetName(
            reader.GetVisualDescendants().OfType<Button>().Single(b => b.Name == part))!;
        Assert.Equal(PdfViewerStrings.OpenText, Name("PART_Open"));
        Assert.Equal(PdfViewerStrings.ZoomInAccessibleName, Name("PART_ZoomIn"));
        Assert.Equal(PdfViewerStrings.ZoomOutAccessibleName, Name("PART_ZoomOut"));
        Assert.Equal(PdfViewerStrings.RotateLeftAccessibleName, Name("PART_RotateLeft"));
        Assert.Equal(PdfViewerStrings.FitWidthText, Name("PART_FitWidth"));
        var search = reader.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "PART_SearchBox");
        Assert.Equal(PdfViewerStrings.SearchAccessibleName, AutomationProperties.GetName(search));
    });

    [Fact]
    public Task Fluent_resource_mapping_follows_the_host_theme_tokens() => HeadlessTestScope.Run(async () =>
    {
        var window = new Window { Width = 800, Height = 600 };
        window.Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://NT.AvaloniaControl.PdfViewer.Themes/"))
        {
            Source = new Uri("avares://NT.AvaloniaControl.PdfViewer.Themes/Resources/Fluent.axaml"),
        });
        var reader = new Reader();
        window.Content = reader;
        window.Show();
        window.UpdateLayout();
        HeadlessTestScope.Drain();
        AssertBrushMatchesToken(window, reader, "PdfViewerBackgroundBrush", "SystemAltHighColor");
        AssertBrushMatchesToken(window, reader, "PdfViewerForegroundBrush", "SystemBaseHighColor");
        AssertBrushMatchesToken(window, reader, "PdfViewerErrorBrush", "SystemErrorTextColor");
        reader.Dispose();
        window.Close();
    });

    [Fact]
    public Task Fluent_theme_option_overrides_the_standalone_palette() => HeadlessTestScope.Run(async () =>
    {
        var window = new Window { Width = 800, Height = 600 };
        window.Styles.Add(new PdfViewerTheme(useFluentResources: true));
        var reader = new Reader();
        window.Content = reader;
        window.Show();
        window.UpdateLayout();
        HeadlessTestScope.Drain();
        AssertBrushMatchesToken(window, reader, "PdfViewerBackgroundBrush", "SystemAltHighColor");
        AssertBrushMatchesToken(window, reader, "PdfViewerToolbarBrush", "SystemChromeMediumLowColor");
        reader.Dispose();
        window.Close();
    });

    private static void AssertBrushMatchesToken(Window window, Reader reader, string brushKey, string tokenKey)
    {
        var token = (Color)window.FindResource(tokenKey)!;
        var brush = (SolidColorBrush)reader.FindResource(brushKey)!;
        Assert.Equal(token, brush.Color);
        var applied = reader.Background as SolidColorBrush;
        Assert.NotNull(applied);
    }

    private sealed class P204Fixture : IDisposable
    {
        internal Reader Reader { get; } = new();
        internal Window Window { get; }

        private P204Fixture()
        {
            Window = new Window { Width = 1000, Height = 700, Content = Reader };
            Window.Show();
            Window.UpdateLayout();
            HeadlessTestScope.Drain();
        }

        internal static async Task<P204Fixture> Create(byte[] bytes, bool load = true)
        {
            var fixture = new P204Fixture();
            try
            {
                if (load)
                {
                    await fixture.Reader.LoadDocumentAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
                    await fixture.Idle();
                }
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

        public void Dispose()
        {
            Reader.Dispose();
            Window.Close();
        }
    }
}
