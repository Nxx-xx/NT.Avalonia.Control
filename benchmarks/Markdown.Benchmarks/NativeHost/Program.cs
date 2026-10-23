using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using NT.AvaloniaControl.Markdown.Themes;

namespace MarkdownNativeBenchmarks;

internal static class Program
{
    public static string[] Arguments { get; private set; } = [];
    [STAThread]
    public static int Main(string[] args)
    {
        Arguments = args;
        return AppBuilder.Configure<BenchmarkApp>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
    }
}
public sealed class BenchmarkApp : Application
{
    public override void Initialize() { Styles.Add(new FluentTheme()); Styles.Add(new MarkdownTheme()); }
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Rendering stops while minimized. Constrain the newly created fixture;
            // never restore or retry an already invalidated measured sample.
            var window = new Window { Width = 1200, Height = 700, CanResize = false, CanMinimize = false,
                WindowState = WindowState.Normal, Title = "Markdown 原生性能测量", ShowActivated = false };
            desktop.MainWindow = window;
            window.Opened += async (_, _) =>
            {
                var runner = new NativeBenchmarkRunner(window, Program.Arguments);
                var exitCode = await runner.RunAsync();
                desktop.Shutdown(exitCode);
            };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
