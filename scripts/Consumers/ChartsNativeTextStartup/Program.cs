using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.NativeTextStartupConsumer;

internal static class Program
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true, Converters = { new JsonStringEnumConverter() },
    };
    internal static Options Options = null!;
    internal static readonly Dictionary<string, bool> Checks = new(StringComparer.Ordinal);
    internal static readonly List<object> Artifacts = [];
    internal static readonly List<nint> RetainedHandles = [];
    internal static object? SystemPreload, BeforeBinding, AfterBinding, AfterWindow;
    internal static object? NativeWindow;
    internal static string? Error;
    internal static bool Passed;
    private static readonly DateTimeOffset Started = DateTimeOffset.UtcNow;

    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            Options = global::Charts.NativeTextStartupConsumer.Options.Parse(args);
            Directory.CreateDirectory(Options.Output);
            Write("raw.initial.json", Snapshot("starting"));
            if (Options.SystemLibrary is not null)
            {
                SystemPreload = NativeProbe.Open(Options.SystemLibrary, 2 | 256, "system-global-control");
                Write("raw.system-preloaded.json", Snapshot("system-global-preloaded"));
            }
            BeforeBinding = ChartNativeTextBinding.Diagnostics;
            if (Options.Mode is "missing" or "resolver-conflict" or "late" or "native-loaded")
            {
                VerifyFailurePolicy();
                Passed = Checks.Count > 0 && Checks.Values.All(value => value);
                return Passed ? 0 : 1;
            }
            var builder = AppBuilder.Configure<ConsumerApplication>();
            if (Options.Mode != "baseline")
            {
                Checks["extension-returns-original-builder"] = ReferenceEquals(builder, builder.WithChartsNativeTextIsolation());
                AfterBinding = ChartNativeTextBinding.Diagnostics;
                if (Options.Mode == "idempotent")
                {
                    var first = ChartNativeTextBinding.Diagnostics;
                    var again = ChartNativeTextBinding.Initialize();
                    builder.WithChartsNativeTextIsolation();
                    var last = ChartNativeTextBinding.Diagnostics;
                    Checks["repeated-initialization-stable"] = first.Status == again.Status && first.Status == last.Status &&
                        first.AssetPath == last.AssetPath && first.AssetSha256 == last.AssetSha256 &&
                        first.SymbolProviderPath == last.SymbolProviderPath && first.ErrorCode == last.ErrorCode;
                }
            }
            else AfterBinding = ChartNativeTextBinding.Diagnostics;
            Write("raw.binding-before-platform.json", Snapshot("binding-before-platform"));
            builder.UsePlatformDetect().StartWithClassicDesktopLifetime([], ShutdownMode.OnExplicitShutdown);
            return Passed ? 0 : 1;
        }
        catch (Exception error) { Error = error.ToString(); Console.Error.WriteLine(error); return 1; }
        finally
        {
            if (Options is not null)
                Write("raw.json", Snapshot(Passed ? "completed" : "failed"));
        }
    }

    private static void VerifyFailurePolicy()
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Fault-policy modes require Linux.");
        string? hidden = null, original = null;
        try
        {
            if (Options.Mode == "missing")
            {
                original = NativeProbe.PackageAsset();
                hidden = original + ".owned-fixture-hidden";
                if (File.Exists(hidden)) throw new IOException("Owned missing-asset fixture destination already exists.");
                File.Move(original, hidden);
            }
            if (Options.Mode == "resolver-conflict")
            {
                var assembly = Assembly.Load(new AssemblyName("HarfBuzzSharp"));
                NativeLibrary.SetDllImportResolver(assembly, (_, _, _) => 0);
                Checks["external-resolver-installed-before-API"] = true;
            }
            if (Options.Mode == "native-loaded")
                _ = NativeProbe.Open(NativeProbe.PackageAsset(), 2, "package-local-before-API");
            if (Options.Mode == "late")
            {
                AppBuilder.Configure<ConsumerApplication>().UsePlatformDetect().SetupWithoutStarting();
                Checks["Avalonia-already-initialized"] = Application.Current is not null;
            }
            var expected = Options.Mode == "missing" ? "MissingNativeAsset" : Options.Mode == "resolver-conflict" ? "ResolverConflict" : "TooLate";
            try { ChartNativeTextBinding.Initialize(); Checks["first-call-failed-closed"] = false; }
            catch (InvalidOperationException)
            {
                var state = ChartNativeTextBinding.Diagnostics;
                Checks["first-call-failed-closed"] = state.Status == ChartNativeTextBindingStatus.Failed && state.ErrorCode == expected;
                AfterBinding = state;
            }
            if (hidden is not null && original is not null) { File.Move(hidden, original); hidden = null; }
            try { ChartNativeTextBinding.Initialize(); Checks["failure-is-sticky"] = false; }
            catch (InvalidOperationException)
            {
                var state = ChartNativeTextBinding.Diagnostics;
                Checks["failure-is-sticky"] = state.Status == ChartNativeTextBindingStatus.Failed && state.ErrorCode == expected;
            }
        }
        finally { if (hidden is not null && original is not null && File.Exists(hidden)) File.Move(hidden, original); }
    }

    internal static async Task VerifyWindowAsync(ConsumerWindow window, IClassicDesktopStyleApplicationLifetime lifetime)
    {
        try
        {
            await Settle(window);
            var handle = window.TryGetPlatformHandle();
            NativeWindow = new { handle = handle?.Handle.ToInt64(), descriptor = handle?.HandleDescriptor,
                backendType = window.PlatformImpl?.GetType().FullName, clientSize = new[] { window.ClientSize.Width, window.ClientSize.Height },
                actualRenderScaling = window.RenderScaling, visible = window.IsVisible };
            Checks["native-visible-window"] = window.IsVisible && handle is { Handle: not 0 };
            Checks["actual-native-platform"] = OperatingSystem.IsWindows() ? handle?.HandleDescriptor == "HWND" :
                handle?.HandleDescriptor == "XID" && window.PlatformImpl?.GetType().FullName?.Contains("X11", StringComparison.Ordinal) == true;
            var plot = window.Chart.PlotArea;
            var hit = window.Chart.HitTestPoint(new Point(plot.X + plot.Width * .25, plot.Y + plot.Height * .5), .1);
            Checks["original-data-identity"] = hit is { StableId: "original-A", Index: 0 } && ReferenceEquals(hit.Item, window.Rows[0]);
            var glyph = window.Chart.TranslatePoint(new Point(plot.X + plot.Width * .25, plot.Y + plot.Height * .5), window) ?? throw new InvalidOperationException("Chart is detached.");
            var reference = window.Reference.TranslatePoint(default, window) ?? throw new InvalidOperationException("Reference is detached.");
            var scale = window.RenderScaling;
            var regions = new { chartGlyph = new[] { (glyph.X - 85) * scale, (glyph.Y - 90) * scale, 170 * scale, 180 * scale },
                referenceGlyph = new[] { reference.X * scale, reference.Y * scale, window.Reference.Bounds.Width * scale, window.Reference.Bounds.Height * scale } };
            Capture(window, "glyph-baseline.png");
            window.Series.ShowDataLabels = false; window.Reference.Opacity = 0;
            await Settle(window); Capture(window, "glyph-hidden.png");
            window.Series.ShowDataLabels = true; window.Reference.Opacity = 1;
            await Settle(window); Capture(window, "glyph-restored.png");
            Write("glyph-regions.json", regions);
            AfterWindow = ChartNativeTextBinding.Diagnostics;
            var state = ChartNativeTextBinding.Diagnostics;
            if (Options.Mode != "baseline")
            {
                Checks["platform-binding-state"] = OperatingSystem.IsLinux() ? state.Status == ChartNativeTextBindingStatus.Initialized : state.Status == ChartNativeTextBindingStatus.NotApplicable;
                Checks["actual-resolver-route-observed"] = !OperatingSystem.IsLinux() || state.ResolverObserved && state.ResolverInvocationCount > 0 && state.ResolvedLibraryName == "libHarfBuzzSharp";
                Checks["private-asset-symbol-provider"] = !OperatingSystem.IsLinux() || state.DeepBindingEnabled && state.AssetPath == state.SymbolProviderPath && state.NativeVersion == "8.3.1" &&
                    NativeProbe.Modules().Any(row => row.Path == state.AssetPath && row.Sha256 == state.AssetSha256);
            }
            Checks["glyph-artifact-protocol-complete"] = Artifacts.Count == 3;
            Passed = Checks.Count > 0 && Checks.Values.All(value => value);
        }
        catch (Exception error) { Error = error.ToString(); }
        finally { lifetime.Shutdown(Passed ? 0 : 1); }
    }

    private static async Task Settle(Window window)
    {
        window.UpdateLayout();
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        await Task.Delay(100); window.UpdateLayout();
    }

    private static void Capture(Window window, string name)
    {
        var scale = window.RenderScaling; var source = new Rect(window.ClientSize);
        var destination = new Rect(new Size(source.Width * scale, source.Height * scale));
        var pixels = new PixelSize((int)Math.Ceiling(destination.Width), (int)Math.Ceiling(destination.Height));
        var path = Path.Combine(Options.Output, name);
        using (var bitmap = new RenderTargetBitmap(pixels, new Vector(96, 96)))
        {
            using (var context = bitmap.CreateDrawingContext())
                context.DrawRectangle(new VisualBrush(window) { SourceRect = new RelativeRect(source, RelativeUnit.Absolute),
                    DestinationRect = new RelativeRect(destination, RelativeUnit.Absolute), Stretch = Stretch.Fill }, null, destination);
            using var stream = File.Open(path, FileMode.CreateNew); bitmap.Save(stream);
        }
        Artifacts.Add(new { path = name, sha256 = NativeProbe.Hash(path), bytes = new FileInfo(path).Length,
            pixelWidth = pixels.Width, pixelHeight = pixels.Height, actualRenderScaling = scale,
            capture = "Neutral 96-DPI offscreen readback of the complete live Window visual; not a compositor screenshot." });
    }

    private static object Snapshot(string status) => new
    {
        schema = "charts-native-text-package-consumer/v1", startedUtc = Started, utc = DateTimeOffset.UtcNow,
        processId = Environment.ProcessId, options = Options, status, passed = Passed, error = Error,
        osDescription = RuntimeInformation.OSDescription, operatingSystem = OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsLinux() ? "linux" : "other",
        architecture = RuntimeInformation.ProcessArchitecture.ToString(),
        apiRequested = Options.Mode != "baseline", systemPreload = SystemPreload, beforeBinding = BeforeBinding,
        afterBinding = AfterBinding, afterWindow = AfterWindow, currentBinding = ChartNativeTextBinding.Diagnostics,
        checks = Checks, artifacts = Artifacts, nativeWindow = NativeWindow, nativeModules = NativeProbe.Modules(),
        loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location) &&
            (a.GetName().Name?.StartsWith("Avalonia", StringComparison.Ordinal) == true || a.GetName().Name is "NT.AvaloniaControl.Charts" or "HarfBuzzSharp"))
            .Select(a => new { name = a.GetName().Name, path = a.Location, sha256 = NativeProbe.Hash(a.Location) }).ToArray(),
        environment = new { xdgCacheHome = Environment.GetEnvironmentVariable("XDG_CACHE_HOME"),
            xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME"),
            fontconfigFile = Environment.GetEnvironmentVariable("FONTCONFIG_FILE"), tmpdir = Environment.GetEnvironmentVariable("TMPDIR"),
            fontconfigSha256 = Environment.GetEnvironmentVariable("FONTCONFIG_FILE") is { } fontFile && File.Exists(fontFile) ? NativeProbe.Hash(fontFile) : null,
            privateFontCache = Environment.GetEnvironmentVariable("CHARTS_STARTUP_FONT_CACHE"),
            ldPreload = Environment.GetEnvironmentVariable("LD_PRELOAD"), globalScale = Environment.GetEnvironmentVariable("AVALONIA_GLOBAL_SCALE_FACTOR") },
        qualification = new { packageOnlyConsumer = true, hostIsolationFlagUsed = false, physicalInputValidated = false,
            physicalDisplayCertified = false, performanceMeasured = false,
            scope = "Independent ordinary Desktop package consumer with an explicit production AppBuilder API. Fault modes are policy tests; baseline is an unmodified diagnostic control, never an inferred pass." },
    };

    private static void Write(string name, object data) => File.WriteAllText(Path.Combine(Options.Output, name), JsonSerializer.Serialize(data, Json));
}

internal sealed record Options(string Output, string Mode, string? SystemLibrary)
{
    internal static Options Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i++)
        {
            var name = args[i];
            if (name is not ("--output" or "--mode" or "--preload-system-harfbuzz") || ++i >= args.Length || !values.TryAdd(name, args[i])) throw new ArgumentException("Invalid/duplicate fixture argument.");
        }
        var output = Path.GetFullPath(values["--output"]);
        if (Directory.Exists(output) || File.Exists(output)) throw new IOException("Use a fresh evidence directory.");
        var mode = values.GetValueOrDefault("--mode", "normal");
        if (mode is not ("normal" or "baseline" or "idempotent" or "missing" or "late" or "native-loaded" or "resolver-conflict")) throw new ArgumentException("Unsupported mode.");
        var system = values.GetValueOrDefault("--preload-system-harfbuzz");
        if (system is not null && (!OperatingSystem.IsLinux() || !Path.IsPathFullyQualified(system) || !File.Exists(system))) throw new ArgumentException("System control requires an existing absolute Linux library path.");
        return new(output, mode, system);
    }
}

internal sealed class ConsumerApplication : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
        {
            var window = new ConsumerWindow(); lifetime.MainWindow = window;
            window.Opened += (_, _) => Dispatcher.UIThread.Post(async () => await Program.VerifyWindowAsync(window, lifetime));
            window.Show();
        }
        base.OnFrameworkInitializationCompleted();
    }
}

internal sealed class ConsumerWindow : Window
{
    internal sealed record Datum(string Id, double X, double Y);
    internal Datum[] Rows { get; } = [new("original-A", .5, 1), new("original-C", 1.5, 1)];
    internal CartesianChart Chart { get; }
    internal ScatterSeries Series { get; }
    internal TextBlock Reference { get; }
    internal ConsumerWindow()
    {
        Width = 1000; Height = 620; Title = "Charts package native-text startup consumer"; Background = Brushes.White;
        Series = new ScatterSeries { ItemsSource = Rows, IdPath = nameof(Datum.Id), CategoryPath = nameof(Datum.X), ValuePath = nameof(Datum.Y),
            MarkerSize = 4, Fill = Brushes.Red, MarkerFill = Brushes.Red, ShowDataLabels = true, DataLabelFontSize = 64,
            DataLabelForeground = Brushes.Black, DataLabelPosition = ChartDataLabelPosition.Center, DataLabelOverlap = ChartDataLabelOverlap.Allow,
            DataLabelFormatter = context => Equals(context.Data.StableId, "original-A") ? "F" : null };
        Chart = new CartesianChart { ShowLegend = false, ShowGridLines = false, Padding = new Thickness(0), Background = Brushes.White,
            XAxis = new NumericAxis { Minimum = 0, Maximum = 2, IsVisible = false }, YAxis = new NumericAxis { Minimum = 0, Maximum = 2, IsVisible = false },
            IsTooltipEnabled = false, SelectionMode = ChartSelectionMode.None };
        Chart.Series.Add(Series);
        Reference = new TextBlock { Text = "F", FontSize = 64, FontFamily = Chart.FontFamily, Foreground = Brushes.Black,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var glyphs = new Grid { ColumnDefinitions = new("*,*"), Background = Brushes.White };
        glyphs.Children.Add(Chart); Grid.SetColumn(Reference, 1); glyphs.Children.Add(Reference);
        var root = new Grid { RowDefinitions = new("Auto,*"), Margin = new Thickness(16) };
        root.Children.Add(new TextBlock { Text = "Package-only native text: 中文 / e\u0301 / 😀 / مبيعات", FontSize = 28, Foreground = Brushes.Black });
        Grid.SetRow(glyphs, 1); root.Children.Add(glyphs); Content = root;
    }
}

internal static class NativeProbe
{
    internal sealed record Module(string Path, string Sha256);
    internal static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
    internal static string PackageAsset()
    {
        var rid = RuntimeInformation.ProcessArchitecture == Architecture.X64 ? "linux-x64" : "linux-arm64";
        foreach (var path in new[] { Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native", "libHarfBuzzSharp.so"), Path.Combine(AppContext.BaseDirectory, "libHarfBuzzSharp.so") })
            if (File.Exists(path)) return Path.GetFullPath(path);
        throw new FileNotFoundException("Fixture cannot locate its owned package asset.");
    }
    internal static object Open(string path, int flags, string purpose)
    {
        path = Path.GetFullPath(path);
        var handle = Dlopen(path, flags);
        if (handle == 0) throw new InvalidOperationException("Controlled dlopen failed: " + Marshal.PtrToStringUTF8(Dlerror()));
        Program.RetainedHandles.Add(handle);
        var symbol = NativeLibrary.GetExport(handle, "hb_version_string");
        var version = Marshal.PtrToStringUTF8(Marshal.GetDelegateForFunctionPointer<VersionString>(symbol)());
        if (Dladdr(symbol, out var info) == 0 || Marshal.PtrToStringUTF8(info.FileName) is not { } provider)
            throw new InvalidOperationException("Controlled system symbol provider could not be observed.");
        provider = Path.GetFullPath(provider);
        return new { purpose, path, sha256 = Hash(path), bytes = new FileInfo(path).Length, flags, handle = $"0x{handle.ToInt64():X}",
            actualVersion = version, symbol = "hb_version_string", symbolAddress = $"0x{symbol.ToInt64():X}",
            symbolProviderPath = provider, symbolProviderSha256 = Hash(provider),
            qualification = "Controlled process-local test preloading; no LD_PRELOAD or system setting is changed." };
    }
    internal static Module[] Modules()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return process.Modules.Cast<ProcessModule>().Select(module => module.FileName).Where(path => path.Contains("harfbuzz", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
                .Distinct(StringComparer.Ordinal).Select(path => new Module(path, Hash(path))).ToArray();
        }
        catch (Exception error) { throw new InvalidOperationException("Actual native module observation failed.", error); }
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nint VersionString();
    [StructLayout(LayoutKind.Sequential)] private struct DlInfo { public nint FileName, BaseAddress, SymbolName, SymbolAddress; }
    [DllImport("libdl.so.2", EntryPoint = "dlopen", CallingConvention = CallingConvention.Cdecl)] private static extern nint Dlopen([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);
    [DllImport("libdl.so.2", EntryPoint = "dlerror", CallingConvention = CallingConvention.Cdecl)] private static extern nint Dlerror();
    [DllImport("libdl.so.2", EntryPoint = "dladdr", CallingConvention = CallingConvention.Cdecl)] private static extern int Dladdr(nint symbol, out DlInfo info);
}
