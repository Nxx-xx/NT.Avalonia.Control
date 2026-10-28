using Avalonia;
using NT.AvaloniaControl.Charts;
using NT.AvaloniaControl.Charts.Skia;

namespace Charts.Usage;

internal static class Program
{
    internal static string[] Arguments { get; private set; } = [];
    internal static object? NativeTextBeforeUi { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        Arguments = args;
        try
        {
            var builder = AppBuilder.Configure<App>().WithChartsNativeTextIsolation().WithChartsSkiaRendering().UsePlatformDetect();
            NativeTextBeforeUi = FontStartupScene.BindingSnapshot();
            return builder.StartWithClassicDesktopLifetime(args);
        }
        catch (Exception error)
        {
            if (!args.Contains("--smoke", StringComparer.Ordinal)) throw;
            FontStartupScene.WriteStartupFailure(args, error);
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}
