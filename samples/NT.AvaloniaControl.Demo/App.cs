using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using NT.AvaloniaControl.TreeDataGrid.Themes;
using NT.AvaloniaControl.VirtualKeyboard.Themes;
using NT.AvaloniaControl.Markdown.Themes;
using NT.AvaloniaControl.Charts.Themes;
using NT.AvaloniaControl.PdfViewer.Themes;

namespace NT.AvaloniaControl.Demo;

public sealed class App : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new TreeDataGridTheme());
        Styles.Add(new VirtualKeyboardTheme());
        Styles.Add(new MarkdownTheme());
        Styles.Add(new ChartsTheme());
        Styles.Add(new PdfViewerTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow(Program.Arguments);
        base.OnFrameworkInitializationCompleted();
    }
}
