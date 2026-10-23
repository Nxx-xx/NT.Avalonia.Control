using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Rendering.Composition;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PdfViewer.Tests;
using NT.AvaloniaControl.PdfViewer;
using Reader = NT.AvaloniaControl.PdfViewer.PdfViewer;

namespace PdfViewer.P1;

internal static class Program
{
    private static readonly Dictionary<string, bool> Checks = [];
    private static readonly Dictionary<string, object?> Observations = [];
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static string _output = "";
    private static string? _realPdf;
    private static string? _realSha;
    private static bool _window;
    private static bool _passed;
    private static string? _failure;
    private static readonly DateTimeOffset Started = DateTimeOffset.UtcNow;

    [STAThread]
    internal static int Main(string[] arguments)
    {
        try
        {
            for (var i = 0; i < arguments.Length; i++)
            {
                switch (arguments[i])
                {
                    case "--output": _output = Path.GetFullPath(arguments[++i]); break;
                    case "--real-pdf": _realPdf = Path.GetFullPath(arguments[++i]); break;
                    case "--expected-sha": _realSha = arguments[++i]; break;
                    case "--window": _window = true; break;
                    default: throw new ArgumentException("Unknown argument: " + arguments[i]);
                }
            }
            if (_output.Length == 0) throw new ArgumentException("--output is required.");
            Directory.CreateDirectory(_output);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            RunBackendAsync(timeout.Token).GetAwaiter().GetResult();
            if (_window)
                AppBuilder.Configure<SmokeApplication>().UsePlatformDetect().StartWithClassicDesktopLifetime([], ShutdownMode.OnExplicitShutdown);
            else
                _passed = true;
        }
        catch (Exception error)
        {
            _failure = error.ToString();
            Console.Error.WriteLine(_failure);
        }
        finally
        {
            if (_output.Length > 0)
            {
                if (OperatingSystem.IsLinux() && File.Exists("/proc/self/maps"))
                    Observations["LoadedNativeRuntimeAssets"] = LinuxModulePaths().Where(path => Path.GetFileName(path) is "libpdfium.so" or "libSkiaSharp.so" or "libHarfBuzzSharp.so")
                        .Select(path => new { Path = path, SHA256 = Hash(File.ReadAllBytes(path)), Bytes = new FileInfo(path).Length }).ToArray();
                File.WriteAllText(Path.Combine(_output, "result.json"), JsonSerializer.Serialize(new
                {
                    SchemaVersion = 1, StartedUtc = Started, CompletedUtc = DateTimeOffset.UtcNow,
                    Passed = _passed, Failure = _failure, Checks, Observations,
                    Runtime = RuntimeInformation.FrameworkDescription, OS = RuntimeInformation.OSDescription,
                    RuntimeIdentifier = RuntimeInformation.RuntimeIdentifier,
                    ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                    FunctionalAssemblySHA256 = Hash(File.ReadAllBytes(typeof(PdfDocument).Assembly.Location)),
                    HostAssemblySHA256 = Hash(File.ReadAllBytes(typeof(Program).Assembly.Location)),
                    Display = Environment.GetEnvironmentVariable("DISPLAY"),
                    WaylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"),
                    Wsl = OperatingSystem.IsLinux() && File.Exists("/proc/sys/kernel/osrelease") && File.ReadAllText("/proc/sys/kernel/osrelease").Contains("microsoft", StringComparison.OrdinalIgnoreCase),
                    GitHubRunId = Environment.GetEnvironmentVariable("GITHUB_RUN_ID"),
                    NativeWindowRequested = _window, Headless = false,
                    PhysicalInputVerified = false, ScanOutVerified = false,
                    OfficialPerformanceComparison = false, KylinVerified = false,
                    ImageCaptureKind = "Avalonia RenderTargetBitmap of the native Window visual; not a desktop screenshot or scan-out measurement.",
                }, Json));
            }
        }
        return _passed ? 0 : 1;
    }

    private static async Task RunBackendAsync(CancellationToken cancellation)
    {
        var sample = PdfFixture.ReadingDocument();
        await File.WriteAllBytesAsync(Path.Combine(_output, "owned-fixture.pdf"), sample, cancellation);
        using (var document = await PdfDocument.LoadAsync(sample, cancellationToken: cancellation))
        {
            Check("fixture-pages-and-outline", document.Pages.Count == 2 && document.Bookmarks.Count == 1);
            var text = await document.GetTextAsync(0, cancellation);
            Check("fixture-text", text.Text.Contains("Hello PDF reader", StringComparison.Ordinal));
            var matches = await document.SearchAsync("hello", cancellationToken: cancellation);
            Check("fixture-search", matches.Count >= 5 && matches.Select(match => match.PageIndex).Distinct().Count() == 2);
            var raster = await document.RenderAsync(0, 300, 400, cancellationToken: cancellation);
            Check("fixture-render", HasInk(raster));
            Observations["FixtureRasterSHA256"] = Hash(raster.Pixels);
            Observations["FixtureSearchMatches"] = matches.Count;
        }
        Check("published-pdf-provided", _realPdf is not null && _realSha is { Length: 64 });
        var bytes = await File.ReadAllBytesAsync(_realPdf!, cancellation);
        Check("published-source-sha256", Hash(bytes).Equals(_realSha, StringComparison.OrdinalIgnoreCase));
        using (var document = await PdfDocument.LoadAsync(bytes, cancellationToken: cancellation))
        {
            Check("published-page-count", document.Pages.Count == 229);
            var text = await document.GetTextAsync(0, cancellation);
            Check("published-first-page-text", text.Text.Contains("make", StringComparison.OrdinalIgnoreCase));
            var matches = await document.SearchAsync("make", cancellationToken: cancellation);
            Check("published-search", matches.Count > 0);
            var first = await document.RenderAsync(0, 400, 520, cancellationToken: cancellation);
            var middle = await document.RenderAsync(document.Pages.Count / 2, 400, 520, cancellationToken: cancellation);
            Check("published-two-page-renders", HasInk(first) && HasInk(middle));
            Observations["PublishedSource"] = new { Path = _realPdf, SHA256 = Hash(bytes), Bytes = bytes.Length, Pages = document.Pages.Count, SearchMatches = matches.Count, FirstRasterSHA256 = Hash(first.Pixels), MiddleRasterSHA256 = Hash(middle.Pixels) };
        }
        var loaded = LoadedPdfium();
        Check("actual-pdfium-module-loaded", loaded.Length == 1);
        Observations["LoadedPdfium"] = loaded.Select(path => new { Path = path, SHA256 = Hash(File.ReadAllBytes(path)), Bytes = new FileInfo(path).Length }).ToArray();
        Observations["SyntheticFixtureSHA256"] = Hash(sample);
    }

    private static string[] LoadedPdfium()
    {
        if (OperatingSystem.IsLinux())
            return LinuxModulePaths()
                .Where(path => Path.GetFileName(path).Equals("libpdfium.so", StringComparison.Ordinal)).Distinct(StringComparer.Ordinal).ToArray();
        using var process = Process.GetCurrentProcess();
        return process.Modules.Cast<ProcessModule>().Where(module => Path.GetFileName(module.FileName).Equals("pdfium.dll", StringComparison.OrdinalIgnoreCase))
            .Select(module => module.FileName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IEnumerable<string> LinuxModulePaths() => File.ReadAllLines("/proc/self/maps")
        .Select(line => line.IndexOf('/') is var index && index >= 0 ? line[index..] : "").Where(path => path.Length > 0).Distinct(StringComparer.Ordinal);

    private static bool HasInk(PdfRenderedPage image) => image.Width > 0 && image.Height > 0 && image.Stride == image.Width * 4 && image.Pixels.Length == image.Stride * image.Height && image.Pixels.Where((_, index) => index % 4 != 3).Any(value => value < 200);
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static void Check(string name, bool condition)
    {
        Checks.Add(name, condition);
        if (!condition) throw new InvalidDataException("Native smoke check failed: " + name);
    }

    private sealed class SmokeApplication : Application
    {
        public SmokeApplication() { }
        public override void Initialize()
        {
            Styles.Add(new FluentTheme());
            Styles.Add(new StyleInclude(new Uri("avares://PdfViewer.P1/")) { Source = new Uri("avares://NT.AvaloniaControl.PdfViewer.Themes/Themes/Generic.axaml") });
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
            {
                var reader = new Reader { IsSidebarVisible = false };
                var window = new Window { Title = "PDF Viewer P1 native smoke", Width = 900, Height = 720, Content = reader };
                lifetime.MainWindow = window;
                var watchdog = new DispatcherTimer { Interval = TimeSpan.FromSeconds(45) };
                watchdog.Tick += (_, _) => Finish(new TimeoutException("The native window smoke timed out."));
                window.Opened += async (_, _) =>
                {
                    try
                    {
                        watchdog.Start();
                        Check("native-window-handle", window.TryGetPlatformHandle() is { Handle: not 0 });
                        var cjkFonts = new Dictionary<string, string>();
                        foreach (var codepoint in new[] { 0x4E2D, 0x6587, 0x9875 })
                        {
                            Check("native-cjk-font-" + codepoint.ToString("X"), FontManager.Current.TryMatchCharacter(codepoint, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal, reader.FontFamily, null, out var face));
                            cjkFonts[codepoint.ToString("X")] = face.FontFamily.Name;
                        }
                        Observations["NativeCjkFonts"] = cjkFonts;
                        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                        await reader.LoadDocumentAsync(PdfFixture.ReadingDocument(), cancellationToken: cancellation.Token);
                        Check("native-control-loaded", reader.HasDocument && reader.PageCount == 2 && reader.ErrorMessage is null);
                        await reader.SearchAsync("hello");
                        Check("native-control-search", reader.SearchResultCount >= 5);
                        reader.GoToPage(2);
                        Check("native-control-navigation", reader.CurrentPage == 2);
                        reader.Rotation = 90;
                        reader.ZoomLevel = 1.25;
                        reader.GoToPage(1);
                        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                        Check("native-control-layout", reader.Bounds.Width > 100 && reader.Bounds.Height > 100);
                        var presenter = reader.GetVisualDescendants().OfType<PdfDocumentPresenter>().Single();
                        while (presenter.CachedPageCount == 0)
                            await Task.Delay(20, cancellation.Token);
                        Check("native-control-page-raster-cached", presenter.CachedPageCount > 0 && presenter.CachedBytes > 0);
                        var compositor = ElementComposition.GetElementVisual(window)?.Compositor ?? throw new InvalidOperationException("Native window has no compositor.");
                        var batch = compositor.RequestCompositionBatchCommitAsync();
                        await batch.Rendered.WaitAsync(cancellation.Token);
                        Check("native-composition-rendered", true);
                        using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height), new Vector(96, 96));
                        bitmap.Render(window);
                        var image = Path.Combine(_output, "native-window.png");
                        bitmap.Save(image);
                        Check("native-window-image", new FileInfo(image).Length > 1000);
                        Observations["NativeWindow"] = new { HandleDescriptor = window.TryGetPlatformHandle()?.HandleDescriptor, Size = window.ClientSize.ToString(), RenderScaling = window.RenderScaling, CachedPageCount = presenter.CachedPageCount, CachedBytes = presenter.CachedBytes, ImageSHA256 = Hash(File.ReadAllBytes(image)), CompositionRendered = true };
                        Finish(null);
                    }
                    catch (Exception error) { Finish(error); }
                };
                void Finish(Exception? error)
                {
                    watchdog.Stop();
                    reader.Dispose();
                    _passed = error is null;
                    _failure = error?.ToString();
                    window.Close();
                    lifetime.Shutdown(error is null ? 0 : 1);
                }
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}
