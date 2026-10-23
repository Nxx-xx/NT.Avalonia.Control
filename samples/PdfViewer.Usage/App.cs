using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using NT.AvaloniaControl.PdfViewer.Themes;

namespace PdfViewer.Usage;

public sealed class App : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new PdfViewerTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var model = new ReaderViewModel(Program.Arguments.FirstOrDefault(value => !value.StartsWith("--", StringComparison.Ordinal)));
            var window = new MainWindow { DataContext = model };
            window.Closed += (_, _) => { window.DataContext = null; model.Dispose(); };
            desktop.MainWindow = window;
        }
        base.OnFrameworkInitializationCompleted();
    }
}
