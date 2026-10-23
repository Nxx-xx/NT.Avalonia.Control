using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Demo;
using NT.AvaloniaControl.Demo.Navigation;
using Keyboard = NT.AvaloniaControl.VirtualKeyboard.VirtualKeyboard;

namespace Demo.Headless.Tests;

public sealed class GalleryNavigationTests
{
    [AvaloniaFact]
    public void Starts_at_gallery_and_card_clicks_and_breadcrumbs_navigate()
    {
        var window = Open();
        try
        {
            Assert.Equal("overview", window.SelectedPageId);
            Assert.Equal(5, window.CurrentItems.Count);
            Assert.False(Find<Button>(window, "NavigateBack").IsEnabled);
            var card = window.GetVisualDescendants().OfType<Button>().First(button => button.Classes.Contains("gallery-card"));
            card.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(new[] { "Charts" }, window.NavigationPath);
            Enter(window, "趋势与面积");
            Enter(window, "折线与面积");
            Assert.Equal(3, window.NavigationPath.Count);
            Assert.Empty(window.GetVisualDescendants().OfType<GalleryPanel>());
            var breadcrumbs = Find<WrapPanel>(window, "Breadcrumbs");
            breadcrumbs.Children.OfType<Button>().ElementAt(1).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(new[] { "Charts" }, window.NavigationPath);
            Find<Button>(window, "NavigateBack").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Empty(window.NavigationPath);
            Assert.Equal("控件库", Find<TextBlock>(window, "CurrentDirectory").Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Every_control_has_categories_and_every_chart_board_can_be_reopened()
    {
        var window = Open();
        try
        {
            foreach (var component in window.CurrentItems.ToArray())
            {
                window.NavigateInto(component);
                Assert.NotEmpty(window.CurrentItems);
                Assert.All(window.CurrentItems, category => { Assert.True(category.IsDirectory); Assert.NotEmpty(category.Children); });
                window.NavigateToDepth(0);
            }
            Enter(window, "Charts");
            var categories = window.CurrentItems.ToArray();
            var visited = 0;
            foreach (var category in categories)
            {
                window.NavigateInto(category);
                foreach (var example in window.CurrentItems.ToArray())
                {
                    window.NavigateInto(example);
                    window.UpdateLayout();
                    Assert.NotNull(Find<ContentControl>(window, "PageHost").Content);
                    window.NavigateBack();
                    window.NavigateInto(example);
                    window.UpdateLayout();
                    window.NavigateBack();
                    visited++;
                }
                window.NavigateBack();
            }
            Assert.Equal(36, visited);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Tree_scenarios_and_mvvm_can_switch_without_reparenting_failures()
    {
        var window = Open();
        try
        {
            Enter(window, "TreeDataGrid");
            foreach (var category in window.CurrentItems.ToArray())
            {
                window.NavigateInto(category);
                foreach (var item in window.CurrentItems.ToArray())
                {
                    window.NavigateInto(item);
                    window.UpdateLayout();
                    Assert.DoesNotContain(window.GetVisualDescendants().OfType<TabControl>(), tabs => tabs.Name == "DemoModeTabs");
                    if (item.Title == "编辑与校验") Capture(window, "treegrid-editing");
                    if (item.Title == "MVVM 模式") Capture(window, "treegrid-mvvm");
                    window.NavigateBack();
                }
                window.NavigateBack();
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Leaving_keyboard_clears_target_and_reentry_restores_input()
    {
        var window = Open();
        try
        {
            Enter(window, "VirtualKeyboard"); Enter(window, "键盘布局"); Enter(window, "英文键盘");
            Dispatcher.UIThread.RunJobs();
            var keyboard = Find<Keyboard>(window, "DemoKeyboard");
            var editor = Find<TextBox>(window, "FirstEditor");
            editor.Text = "保留草稿";
            Assert.Same(editor, keyboard.Target);
            Capture(window, "keyboard-english");
            window.NavigateBack();
            Assert.Null(keyboard.Target);
            Enter(window, "英文键盘");
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("保留草稿", editor.Text);
            Assert.Same(editor, keyboard.Target);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Markdown_and_pdf_categories_open_the_corresponding_scenarios()
    {
        var window = Open();
        try
        {
            Enter(window, "Markdown"); Enter(window, "阅读与语法"); Enter(window, "中文 / Unicode / RTL");
            var markdown = Assert.Single(window.GetVisualDescendants().OfType<NT.AvaloniaControl.Markdown.Markdown>());
            Assert.Contains("עברית", markdown.Text);
            window.NavigateBack();
            Enter(window, "基础阅读");
            Assert.Contains("常规语法", markdown.Text);
            window.NavigateToDepth(0);
            Enter(window, "PDF Viewer"); Enter(window, "缩放与布局"); Enter(window, "适合页面");
            var pdf = Find<NT.AvaloniaControl.PdfViewer.PdfViewer>(window, "PdfReader");
            Assert.Equal(NT.AvaloniaControl.PdfViewer.PdfZoomMode.FitPage, pdf.ZoomMode);
            window.NavigateBack();
            Enter(window, "双页阅读");
            Assert.Same(pdf, Find<NT.AvaloniaControl.PdfViewer.PdfViewer>(window, "PdfReader"));
            Assert.Equal(NT.AvaloniaControl.PdfViewer.PdfPageDisplayMode.TwoPageContinuous, pdf.PageDisplayMode);
            window.NavigateToDepth(0);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Mvvm_command_line_still_opens_the_demo_and_current_breadcrumb_preserves_it()
    {
        var window = new MainWindow(["--mvvm"]);
        try
        {
            window.Show();
            window.UpdateLayout();
            Assert.Equal(new[] { "TreeDataGrid", "数据绑定", "MVVM 模式" }, window.NavigationPath);
            var view = Find<ContentControl>(window, "PageHost").Content;
            window.NavigateToDepth(3);
            Assert.Same(view, Find<ContentControl>(window, "PageHost").Content);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(1440, false)]
    [InlineData(800, false)]
    [InlineData(1024, true)]
    public void Cards_fit_window_and_follow_theme(int width, bool dark)
    {
        var window = Open(width, dark);
        try
        {
            window.UpdateLayout();
            var panel = Assert.Single(window.GetVisualDescendants().OfType<GalleryPanel>());
            Assert.All(panel.Children, child =>
            {
                Assert.True(child.Bounds.Width > 200);
                Assert.True(child.Bounds.Right <= panel.Bounds.Width + 1);
            });
            Assert.True(window.TryFindResource("GallerySurface", window.ActualThemeVariant, out var surface));
            var card = Assert.IsType<Button>(panel.Children[0]);
            Assert.Equal(surface, card.Background);
            Capture(window, $"home-{width}-{(dark ? "dark" : "light")}");
            Enter(window, "Charts");
            panel = Assert.Single(window.GetVisualDescendants().OfType<GalleryPanel>());
            Assert.All(panel.Children, child => Assert.Equal(panel.Children[0].Bounds.Width, child.Bounds.Width, 1));
            Assert.InRange(window.ClientSize.Width, width - 1, width + 1);
            Capture(window, $"charts-{width}-{(dark ? "dark" : "light")}");
        }
        finally { window.Close(); }
    }

    private static MainWindow Open(int width = 1440, bool dark = false)
    {
        var window = new MainWindow([]) { Width = width, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static void Enter(MainWindow window, string title)
    {
        window.NavigateInto(window.CurrentItems.Single(item => item.Title == title));
        window.UpdateLayout();
    }

    private static T Find<T>(MainWindow window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);

    private static void Capture(MainWindow window, string name)
    {
        if (Environment.GetEnvironmentVariable("DEMO_GALLERY_SCREENSHOTS") is not { Length: > 0 } directory) return;
        Directory.CreateDirectory(directory);
        window.UpdateLayout();
        using var bitmap = new RenderTargetBitmap(PixelSize.FromSize(window.ClientSize, 1), new Vector(96, 96));
        bitmap.Render(window);
        bitmap.Save(Path.Combine(directory, name + ".png"));
    }
}
