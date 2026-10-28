using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Logging;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts.Themes;

namespace Charts.PlatformAcceptance;

internal static class Program
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    internal static Options CurrentOptions { get; private set; } = null!;
    internal static AcceptanceReport Report { get; private set; } = null!;
    private static PlatformLogSink? _log;

    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            CurrentOptions = Options.Parse(args);
            Directory.CreateDirectory(CurrentOptions.Output);
            var culture = CultureInfo.GetCultureInfo(CurrentOptions.Culture);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            Report = new() { Options = CurrentOptions };
            WriteNewJson(Path.Combine(CurrentOptions.Output, "raw.initial.json"), Report);
            _log = new PlatformLogSink(Path.Combine(CurrentOptions.Output, "avalonia-platform.jsonl"));
            Logger.Sink = _log;
            PlatformDiagnostics.Initialize(CurrentOptions);
            NativeTextBindingBootstrap.Apply(CurrentOptions.IsolateNativeText);
            var builder = AppBuilder.Configure<AcceptanceApplication>().UsePlatformDetect();
            if (OperatingSystem.IsWindows())
                builder.With(new Win32PlatformOptions { RenderingMode = CurrentOptions.Renderer switch
                {
                    "software" => [Win32RenderingMode.Software],
                    "gpu" => [Win32RenderingMode.AngleEgl],
                    _ => [Win32RenderingMode.AngleEgl, Win32RenderingMode.Software],
                } });
            else if (OperatingSystem.IsLinux())
                builder.With(new X11PlatformOptions { RenderingMode = CurrentOptions.Renderer switch
                {
                    "software" => [X11RenderingMode.Software],
                    "gpu" => [X11RenderingMode.Glx],
                    _ => [X11RenderingMode.Glx, X11RenderingMode.Software],
                } });
            else throw new PlatformNotSupportedException("This acceptance host currently targets Win32 and Linux X11.");
            builder.StartWithClassicDesktopLifetime([], ShutdownMode.OnExplicitShutdown);
            return Report.Passed ? 0 : 1;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            if (Report is not null) Fail(error, "startup-or-dispatch-failed");
            return 1;
        }
        finally
        {
            if (Report is not null)
            {
                Report.FinishedUtc ??= DateTimeOffset.UtcNow;
                Report.Assemblies = PlatformDiagnostics.Assemblies();
                Report.NativeModules = PlatformDiagnostics.NativeModules();
                if (Report.Status is "starting" or "running")
                {
                    Report.Status = "incomplete";
                    Report.Passed = false;
                }
                WriteNewJson(Path.Combine(CurrentOptions.Output, "raw.json"), Report);
            }
            _log?.Dispose();
        }
    }

    internal static async Task RunAsync(AcceptanceWindow window, IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            Report.Status = "running";
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            window.UpdateLayout();
            await PlatformDiagnostics.ObserveNativeFrameAsync(window, "before-suite");
            Report.EnvironmentBefore = PlatformDiagnostics.Capture(window);
            Report.Suite = await window.RunAsync();
            if (window.IsVisible && window.TryGetPlatformHandle() is { Handle: not 0 })
            {
                await PlatformDiagnostics.ObserveNativeFrameAsync(window, "after-suite");
                Report.EnvironmentAfter = PlatformDiagnostics.Capture(window);
            }
            Report.Checks["native-visible-window"] = window.IsVisible && window.TryGetPlatformHandle() is { Handle: not 0 };
            Report.Checks["observed-native-backend"] = window.PlatformImpl is not null && PlatformDiagnostics.ActualBackend(window) != "unknown";
            Report.Checks["native-render-surface-observed"] = PlatformDiagnostics.ObservedRenderer is "software-raster" or "gpu-context";
            Report.Checks["software-request-observed-on-native-surface"] = CurrentOptions.Renderer != "software" || PlatformDiagnostics.ObservedRenderer == "software-raster";
            Report.Checks["gpu-context-request-observed-on-native-surface"] = CurrentOptions.Renderer != "gpu" || PlatformDiagnostics.ObservedRenderer == "gpu-context";
            Report.Checks["requested-native-scale-observed"] = CurrentOptions.RequestedScale is not { } scale || Math.Abs(window.RenderScaling - scale) <= .02;
            Report.Checks["current-culture-matches-request"] = CultureInfo.CurrentCulture.Name == CultureInfo.GetCultureInfo(CurrentOptions.Culture).Name;
            Report.Checks["suite-has-positive-coverage"] = Report.Suite.Checks.Count > 0;
            Report.Passed = Report.Checks.Values.All(value => value) && Report.Suite.Checks.Values.All(value => value) && Report.Suite.Faults.Count == 0;
            Report.Status = Report.Passed ? "completed" : "failed";
        }
        catch (Exception error) { Fail(error, error is TimeoutException ? "timeout" : "failed"); }
        finally
        {
            Report.FinishedUtc = DateTimeOffset.UtcNow;
            desktop.Shutdown(Report.Passed ? 0 : 1);
        }
    }

    private static void Fail(Exception error, string status)
    {
        Report.Passed = false;
        Report.Status = status;
        Report.Error = error.ToString();
    }

    internal static void WriteNewJson(string path, object value)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        JsonSerializer.Serialize(stream, value, JsonOptions);
    }
}

public sealed class AcceptanceApplication : Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = Program.CurrentOptions.Theme.EndsWith("dark", StringComparison.Ordinal) ? ThemeVariant.Dark : ThemeVariant.Light;
        Styles.Add(new FluentTheme());
        Styles.Add(new ChartsTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new AcceptanceWindow(Program.CurrentOptions);
            desktop.MainWindow = window;
            window.Opened += (_, _) => Dispatcher.UIThread.Post(async () => await Program.RunAsync(window, desktop));
            window.Show();
        }
        base.OnFrameworkInitializationCompleted();
    }
}

internal sealed record Options(string Output, string Renderer, string Culture, string Theme, string Scenario, double? RequestedScale, bool Interactive, bool IsolateNativeText)
{
    internal static Options Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var interactive = false;
        var isolateNativeText = false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i++)
        {
            var key = args[i];
            if (!seen.Add(key)) throw new ArgumentException($"Duplicate option: {key}");
            if (key == "--interactive") { interactive = true; continue; }
            if (key == "--isolate-native-text") { isolateNativeText = true; continue; }
            if (key is not ("--output" or "--renderer" or "--culture" or "--theme" or "--scenario" or "--scale") ||
                ++i >= args.Length || args[i].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Unknown option or missing value: {key}");
            values.Add(key, args[i]);
        }
        if (!values.TryGetValue("--output", out var path) || string.IsNullOrWhiteSpace(path)) throw new ArgumentException("--output is required.");
        var output = Path.GetFullPath(path);
        var allowed = new[] { "process.json", "stdout.log", "stderr.log" };
        if (File.Exists(output) || Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any(entry => !allowed.Contains(Path.GetFileName(entry), StringComparer.Ordinal)))
            throw new IOException("Output must be new or contain only this invocation's supervisor receipts; existing evidence is never overwritten.");
        var renderer = values.GetValueOrDefault("--renderer", "default");
        if (renderer is not ("default" or "software" or "gpu")) throw new ArgumentException("--renderer must be default, software or gpu.");
        var culture = CultureInfo.GetCultureInfo(values.GetValueOrDefault("--culture", "en-US")).Name;
        var theme = values.GetValueOrDefault("--theme", "light");
        if (theme is not ("light" or "dark" or "hc-light" or "hc-dark")) throw new ArgumentException("Invalid --theme.");
        var scenario = values.GetValueOrDefault("--scenario", "desktop");
        if (scenario != "desktop") throw new ArgumentException("Only the desktop acceptance scenario is currently defined.");
        double? scale = null;
        if (values.TryGetValue("--scale", out var text))
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number) || number is < .5 or > 4)
                throw new ArgumentException("--scale must be a finite expected native scale between 0.5 and 4.");
            scale = number;
        }
        return new(output, renderer, culture, theme, scenario, scale, interactive, isolateNativeText);
    }
}

internal sealed class AcceptanceReport
{
    public string Schema { get; init; } = "charts-platform-acceptance/v1";
    public DateTimeOffset StartedUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedUtc { get; set; }
    public Options Options { get; init; } = null!;
    public string Status { get; set; } = "starting";
    public bool Passed { get; set; }
    public string? Error { get; set; }
    public Dictionary<string, bool> Checks { get; } = [];
    public Dictionary<string, object?>? EnvironmentBefore { get; set; }
    public Dictionary<string, object?>? EnvironmentAfter { get; set; }
    public AcceptanceSuiteResult? Suite { get; set; }
    public object[] Assemblies { get; set; } = [];
    public object[] NativeModules { get; set; } = [];
    public object NativeTextIsolation => NativeTextBindingBootstrap.Capture(Options?.IsolateNativeText ?? false);
    public object Qualification { get; } = new
    {
        physicalInputValidated = false,
        compositorPresentValidated = false,
        hardwareGpuCertified = false,
        targetLinuxDistributionCertified = false,
        nativeSelectedItemBridgeCertified = false,
        performanceMeasured = false,
        scope = "A native-window software acceptance host. Requested renderer/scale, observed surface, native events, OS input provenance, compositor screenshots and physical equipment are separate evidence.",
    };
}
