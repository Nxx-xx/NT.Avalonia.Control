using Avalonia;
using NT.AvaloniaControl.Charts;
using NT.AvaloniaControl.Charts.Skia;

namespace NT.AvaloniaControl.Demo;

internal static class Program
{
    internal static string[] Arguments { get; private set; } = [];
    internal static object? NativeTextBeforeUi { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        Arguments = args;
        try { return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args); }
        catch (Exception error)
        {
            if (!args.Any(value => value is "--charts-smoke" or "--smoke" or "--demo-smoke")) throw;
            FontStartupScene.WriteStartupFailure(args, error);
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>().WithChartsNativeTextIsolation().WithChartsSkiaRendering().UsePlatformDetect();
        NativeTextBeforeUi = FontStartupScene.BindingSnapshot();
        return builder;
    }
}
