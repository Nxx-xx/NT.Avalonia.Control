using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts.Themes;

namespace Charts.ExportStability;

internal static class Program
{
    internal static ExportDriver? Driver { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            var driver = new ExportDriver(Options.Parse(args)); Driver = driver;
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; driver.Cancel(); };
            AppBuilder.Configure<ExportApplication>().UsePlatformDetect().LogToTrace()
                .StartWithClassicDesktopLifetime([], ShutdownMode.OnExplicitShutdown);
            return driver.Passed ? 0 : 1;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            Driver?.Fail(error);
            return 1;
        }
        finally { Driver?.Dispose(); }
    }
}

public sealed class ExportApplication : Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Light;
        Styles.Add(new FluentTheme()); Styles.Add(new ChartsTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new Window
            {
                Width = 1160, Height = 430, CanResize = false, Title = "Charts exact export stability · native synthetic",
                Background = Brushes.White,
            };
            desktop.MainWindow = window;
            window.Opened += (_, _) => Dispatcher.UIThread.Post(async () =>
            {
                var driver = Program.Driver ?? throw new InvalidOperationException("No initialized export driver.");
                await driver.RunAsync(window);
                desktop.Shutdown(driver.Passed ? 0 : 1);
            });
            window.Show();
        }
        base.OnFrameworkInitializationCompleted();
    }
}

internal sealed record Options(string Output, int Rounds, int Points, bool ForceGc)
{
    internal static Options Parse(string[] args)
    {
        string? output = null; var rounds = 16; var points = 100_000; var forced = false; var enabled = false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i++)
        {
            var key = args[i];
            if (!seen.Add(key)) throw new ArgumentException($"Duplicate option: {key}");
            if (key == "--export-stability") { enabled = true; continue; }
            if (key == "--force-gc") { forced = true; continue; }
            if (key is not ("--output" or "--rounds" or "--points") || ++i >= args.Length)
                throw new ArgumentException($"Unknown option or missing value: {key}");
            var value = args[i];
            if (key == "--output") output = Path.GetFullPath(value);
            else if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var count))
                throw new ArgumentException($"Invalid integer: {key}");
            else if (key == "--rounds" && count is >= 1 and <= 128) rounds = count;
            else if (key == "--points" && count is >= 8_191 and <= 1_000_000) points = count;
            else throw new ArgumentOutOfRangeException(key);
        }
        if (!enabled || output is null) throw new ArgumentException("--export-stability and --output are required.");
        var supervisorFiles = new[] { "process.json", "stdout.log", "stderr.log" };
        if (File.Exists(output) || Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output)
                .Any(path => !supervisorFiles.Contains(Path.GetFileName(path), StringComparer.Ordinal)))
            throw new IOException("Use a fresh output directory; previous producer evidence is never overwritten.");
        return new(output, rounds, points, forced);
    }
}
