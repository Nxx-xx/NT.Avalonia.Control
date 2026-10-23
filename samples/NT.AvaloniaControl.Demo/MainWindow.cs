using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.Input;
using NT.AvaloniaControl.Demo.Navigation;
using NT.AvaloniaControl.Demo.Pages.TreeDataGrid;
using NT.AvaloniaControl.Demo.Pages.VirtualKeyboard;
using NT.AvaloniaControl.Demo.Pages.Markdown;
using NT.AvaloniaControl.Demo.Pages.Charts;
using NT.AvaloniaControl.Demo.Pages.PdfViewer;

namespace NT.AvaloniaControl.Demo;

public sealed class MainWindow : Window
{
    private readonly ContentControl _pageHost = new() { Name = "PageHost" };
    private readonly Button _back = new() { Name = "NavigateBack", Content = "←", MinWidth = 36 };
    private readonly WrapPanel _breadcrumbs = new() { Name = "Breadcrumbs", ItemSpacing = 8, LineSpacing = 4, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _location = new() { Name = "CurrentDirectory", FontSize = 12, Opacity = .65, HorizontalAlignment = HorizontalAlignment.Right, TextWrapping = TextWrapping.Wrap };
    private readonly List<DemoNavigationItem> _path = [];
    private readonly IReadOnlyList<DemoNavigationItem> _rootItems;
    private readonly bool _smoke;
    private readonly IReadOnlyList<DemoPageDefinition> _pageDefinitions;
    private readonly Dictionary<string, IDemoPage> _pages = new(StringComparer.Ordinal);
    private IDemoPage? _activePage;
    private bool _disposed;

    internal string? SelectedPageId { get; private set; }
    internal IReadOnlyList<DemoNavigationItem> CurrentItems => _path.Count == 0 ? _rootItems : _path[^1].Children;
    internal IReadOnlyList<string> NavigationPath => _path.Select(item => item.Title).ToArray();

    public MainWindow(string[] args)
    {
        Title = "NT.AvaloniaControl.Demo · 控件示例";
        Width = 1440;
        Height = 900;
        MinWidth = 800;
        MinHeight = 760;
        var keyboardSmoke = args.Contains("--keyboard-smoke", StringComparer.Ordinal);
        var markdownSmoke = args.Contains("--markdown-smoke", StringComparer.Ordinal);
        var chartsSmoke = args.Contains("--charts-smoke", StringComparer.Ordinal);
        var pdfSmoke = args.Contains("--pdf-smoke", StringComparer.Ordinal);
        var smoke = keyboardSmoke || markdownSmoke || chartsSmoke || pdfSmoke || args.Any(arg => arg is "--smoke" or "--demo-smoke" or "--mvvm-smoke");
        _smoke = smoke;
        var initialPage = keyboardSmoke ? "virtualkeyboard" : markdownSmoke ? "markdown" : chartsSmoke ? "charts" : pdfSmoke ? "pdfviewer" : PageArgument(args) ?? (smoke || args.Contains("--mvvm") ? "treedatagrid" : "overview");
        if (smoke && initialPage == "treedatagrid")
        {
            ShowActivated = false;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = new PixelPoint(-10000, -10000);
        }

        _pageDefinitions = DemoPageCatalog.Create(this, args, SelectPage);
        _rootItems = _pageDefinitions.Where(page => page.Id != "overview")
            .OrderBy(page => page.Id == "charts" ? 0 : page.Id == "treedatagrid" ? 1 : page.Id == "markdown" ? 2 : page.Id == "pdfviewer" ? 3 : 4)
            .Select(page => new DemoNavigationItem(page.Title, page.Description, page.Id switch
            {
                "treedatagrid" => "table", "markdown" => "doc", "pdfviewer" => "pdf", "virtualkeyboard" => "keys", _ => "line",
            }, () => GetPage(page).CreateGallery())).ToArray();
        DemoGallery.AddStyles(Styles);
        this[!BackgroundProperty] = new DynamicResourceExtension("GalleryBackground");
        _back.Click += (_, _) => NavigateBack();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Left && e.KeyModifiers == KeyModifiers.Alt) { NavigateBack(); e.Handled = true; }
        };
        Content = CreateLayout();
        SelectPage(initialPage);
        if (!smoke && initialPage == "treedatagrid" && args.Contains("--mvvm", StringComparer.Ordinal))
        {
            NavigateInto(CurrentItems.Single(item => item.Title == "数据绑定"));
            NavigateInto(CurrentItems.Single(item => item.Title == "MVVM 模式"));
        }
        Closed += (_, _) => DisposePages();

        if (smoke)
            Opened += async (_, _) =>
            {
                switch (_activePage)
                {
                    case PdfViewerPage pdfPage:
                        await pdfPage.RunPdfSmokeAsync(args);
                        break;
                    case ChartsPage chartsPage:
                        await chartsPage.RunChartsSmokeAsync(args);
                        break;
                    case MarkdownPage markdownPage:
                        await markdownPage.RunMarkdownSmokeAsync(args);
                        break;
                    case VirtualKeyboardPage keyboardPage:
                        await keyboardPage.RunKeyboardSmokeAsync(args);
                        break;
                    case TreeDataGridPage treePage:
                        if (args.Contains("--mvvm-smoke", StringComparer.Ordinal)) await treePage.RunMvvmSmokeAsync(args);
                        else if (args.Contains("--demo-smoke", StringComparer.Ordinal)) await treePage.RunDemoSmokeAsync(args);
                        else await treePage.RunSmokeAsync(args);
                        break;
                    default:
                        throw new ArgumentException($"Page {SelectedPageId} has no native smoke probe.");
                }
            };
    }

    private static string? PageArgument(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--page=", StringComparison.Ordinal)) return args[i][7..];
            if (args[i] == "--page")
                return i + 1 < args.Length ? args[i + 1] : throw new ArgumentException("--page requires a page identifier.");
        }
        return null;
    }

    internal void SelectPage(string id)
    {
        if (_disposed) return;
        var definition = _pageDefinitions.FirstOrDefault(page => page.Id == id)
            ?? throw new ArgumentException($"Unknown demo page: {id}", nameof(id));
        _path.Clear();
        if (id == "overview") { ShowDestination(); return; }
        var item = _rootItems.Single(node => node.Title == definition.Title);
        _path.Add(item);
        if (_smoke)
        {
            _activePage?.Deactivate();
            _pageHost.Content = null;
            _activePage = GetPage(definition);
            SelectedPageId = id;
            _pageHost.Content = _activePage.View;
            _activePage.Activate();
            UpdateNavigation();
        }
        else ShowDestination();
    }

    private IDemoPage GetPage(DemoPageDefinition definition)
    {
        if (!_pages.TryGetValue(definition.Id, out var page))
            _pages.Add(definition.Id, page = definition.CreatePage());
        return page;
    }

    internal void NavigateInto(DemoNavigationItem item)
    {
        if (_disposed || !CurrentItems.Contains(item)) return;
        _path.Add(item);
        ShowDestination();
    }

    internal void NavigateBack() => NavigateToDepth(Math.Max(0, _path.Count - 1));

    internal void NavigateToDepth(int depth)
    {
        if (_disposed || depth < 0 || depth >= _path.Count) return;
        _path.RemoveRange(depth, _path.Count - depth);
        ShowDestination();
    }

    private void ShowDestination()
    {
        _activePage?.Deactivate();
        _activePage = null;
        _pageHost.Content = null;
        SelectedPageId = _path.Count == 0 ? "overview" : _pageDefinitions.Single(page => page.Title == _path[0].Title).Id;
        if (_path.Count == 0 || _path[^1].IsDirectory)
        {
            _pageHost.Content = DemoGallery.Create(_path.Count == 0 ? "探索控件" : _path[^1].Title,
                _path.Count == 0 ? "选择一个控件，浏览它的分类与交互示例。" : _path[^1].Description,
                CurrentItems, NavigateInto);
        }
        else
        {
            var page = GetPage(_pageDefinitions.Single(definition => definition.Id == SelectedPageId));
            _pageHost.Content = _path[^1].Open();
            _activePage = page;
            page.Activate();
        }
        UpdateNavigation();
    }

    private void UpdateNavigation()
    {
        _back.IsEnabled = _path.Count > 0;
        _breadcrumbs.Children.Clear();
        var titles = new[] { "控件库" }.Concat(_path.Select(item => item.Title)).ToArray();
        for (var i = 0; i < titles.Length; i++)
        {
            if (i > 0) _breadcrumbs.Children.Add(new TextBlock { Text = "/", Opacity = .4, VerticalAlignment = VerticalAlignment.Center });
            var depth = i;
            var button = new Button { Content = titles[i], Padding = new Thickness(7, 4), Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
            button.Click += (_, _) => NavigateToDepth(depth);
            _breadcrumbs.Children.Add(button);
        }
        _location.Text = string.Join(" / ", titles);
    }

    private Control CreateLayout()
    {
        var title = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock { Text = "AvaloniaControl", FontSize = 20, FontWeight = FontWeight.SemiBold },
                new TextBlock { Text = "控件示例库", FontSize = 12, Opacity = .65 },
            },
        };
        var themeButton = new Button
        {
            Name = "ThemeToggleButton", Content = "切换亮 / 暗主题", VerticalAlignment = VerticalAlignment.Center,
            Command = new RelayCommand(() => RequestedThemeVariant = ActualThemeVariant == ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark),
        };
        ToolTip.SetTip(themeButton, "切换整个示例程序的主题，保留各页面的数据和编辑草稿。");
        var heading = new DockPanel { Margin = new Thickness(24, 16), LastChildFill = true };
        DockPanel.SetDock(themeButton, Dock.Right);
        heading.Children.Add(themeButton);
        heading.Children.Add(title);

        var chrome = new Border { Child = heading, BorderThickness = new Thickness(0, 0, 0, 1) };
        chrome[!Border.BackgroundProperty] = new DynamicResourceExtension("GallerySurface");
        chrome[!Border.BorderBrushProperty] = new DynamicResourceExtension("GalleryLine");
        var navigation = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 16, Margin = new Thickness(24, 14, 24, 0) };
        navigation.Children.Add(_back);
        Grid.SetColumn(_breadcrumbs, 1);
        navigation.Children.Add(_breadcrumbs);
        ToolTip.SetTip(_back, "返回上一级（Alt+←）");
        var footer = new Border { Padding = new Thickness(24, 10), BorderThickness = new Thickness(0, 1, 0, 0), Child = _location };
        footer[!Border.BorderBrushProperty] = new DynamicResourceExtension("GalleryLine");
        var root = new DockPanel();
        DockPanel.SetDock(chrome, Dock.Top);
        DockPanel.SetDock(navigation, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(chrome);
        root.Children.Add(navigation);
        root.Children.Add(footer);
        root.Children.Add(_pageHost);
        return root;
    }

    private void DisposePages()
    {
        if (_disposed) return;
        _disposed = true;
        _activePage?.Deactivate();
        _pageHost.Content = null;
        foreach (var page in _pages.Values) page.Dispose();
        _pages.Clear();
        _activePage = null;
    }
}
