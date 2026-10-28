using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using NT.AvaloniaControl.TreeDataGrid.Themes;

namespace TreeDataGridNativeBenchmarks;

internal static class Program
{
    internal static string[] Arguments { get; private set; } = [];
    [STAThread]
    public static int Main(string[] args)
    {
        Arguments = args;
        return AppBuilder.Configure<BenchmarkApp>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
    }
}

public sealed class BenchmarkApp : Application
{
    public override void Initialize() { Styles.Add(new FluentTheme()); Styles.Add(new TreeDataGridTheme()); }
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new Window { Title = "TreeDataGrid · Windows 原生性能与耐久测量", Width = 1000, Height = 650,
                CanResize = false, CanMinimize = false, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.CenterScreen };
            desktop.MainWindow = window;
            window.Opened += async (_, _) =>
            {
                if (Program.Arguments is ["--geometry", var output])
                {
                    desktop.Shutdown(await NativeGeometry.RunAsync(window, output));
                    return;
                }
                var runner = new NativeRunner(window, Program.Arguments);
                desktop.Shutdown(await runner.RunAsync());
            };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
