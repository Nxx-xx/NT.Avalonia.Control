using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Charts.Benchmarks;
using NT.AvaloniaControl.Charts;
using NT.AvaloniaControl.Charts.Skia;
using NT.AvaloniaControl.Charts.Themes;

namespace Charts.MemoryProbe;

// A separate diagnostic track. No original C7 timing, budget or entry is changed.
internal static class Program
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    private static readonly List<object> Phases = [];
    private static readonly List<object> Managed = [];
    private static string _mode = "", _output = "", _control = "", _token = "";
    private static DateTime _processStartedUtc;
    private static long? _linuxStartTicks;
    private static string? _linuxBootId;
    private static int _exitCode = 1;

    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var index = 0; index < args.Length; index += 2)
                if (index + 1 >= args.Length || args[index] is not ("--mode" or "--output" or "--control-dir" or "--run-token") ||
                    !values.TryAdd(args[index], args[index + 1])) throw new ArgumentException("Expected distinct known --name value pairs.");
            _mode = values["--mode"];
            if (_mode is not ("empty" or "full")) throw new ArgumentException("Mode must be empty or full.");
            _token = values["--run-token"];
            if (!Guid.TryParseExact(_token, "N", out _)) throw new ArgumentException("Run token must be a GUID in N format.");
            _output = Path.GetFullPath(values["--output"]); _control = Path.GetFullPath(values["--control-dir"]);
            if (Directory.Exists(_output) || File.Exists(_output)) throw new IOException("Host output must be new.");
            Directory.CreateDirectory(_output); Directory.CreateDirectory(_control);
            using (var process = Process.GetCurrentProcess()) _processStartedUtc = process.StartTime.ToUniversalTime();
            if (OperatingSystem.IsLinux())
            {
                var stat = File.ReadAllText($"/proc/{Environment.ProcessId}/stat");
                _linuxStartTicks = long.Parse(stat[(stat.LastIndexOf(')') + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries)[19], CultureInfo.InvariantCulture);
                _linuxBootId = File.ReadAllText("/proc/sys/kernel/random/boot_id").Trim();
            }
            ChartNativeTextBinding.Initialize();
            var builder = AppBuilder.Configure<MemoryApplication>().WithChartsSkiaRendering().UsePlatformDetect();
            if (OperatingSystem.IsWindows()) builder.With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Software] });
            else if (OperatingSystem.IsLinux()) builder.With(new X11PlatformOptions { RenderingMode = [X11RenderingMode.Software] });
            else throw new PlatformNotSupportedException("Only Windows Win32 and Linux X11 software rendering are supported.");
            builder.StartWithClassicDesktopLifetime([], ShutdownMode.OnExplicitShutdown);
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            if (Directory.Exists(_output) && !File.Exists(Path.Combine(_output, "raw.json")))
                Write(_output, "raw.json", new { status = "failed", error = error.ToString(), phases = Phases, managed = Managed });
        }
        return _exitCode;
    }

    internal static async Task RunWindowAsync(Window window, IClassicDesktopStyleApplicationLifetime lifetime)
    {
        var status = "failed"; string? error = null;
        BenchmarkDataset? dataset = null; MemoryRow[] rows = []; DrawingGroup? recorded = null;
        try
        {
            var native = new NativeProbe { Width = 8, Height = 8 };
            window.Content = native; window.UpdateLayout();
            var compositor = ElementComposition.GetElementVisual(native)?.Compositor ?? throw new InvalidOperationException("Native compositor unavailable.");
            native.InvalidateVisual();
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
            var batch = compositor.RequestCompositionBatchCommitAsync();
            await batch.Rendered.WaitAsync(TimeSpan.FromSeconds(15));
            var surface = await native.Observed.Task.WaitAsync(TimeSpan.FromSeconds(15));
            if (surface.HasGrContext != false || surface.Error is not null) throw new InvalidOperationException("Software Skia lease was not observed.");
            if (!window.IsVisible || window.TryGetPlatformHandle() is not { Handle: not 0 } || window.RenderScaling != 1)
                throw new InvalidOperationException("A visible 96-DPI native window is required.");
            if (!AppContext.TryGetSwitch("System.Runtime.TieredCompilation", out var tiered) || tiered ||
                Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") is not (null or "0") ||
                Environment.GetEnvironmentVariable("COMPlus_TieredCompilation") is not (null or "0"))
                throw new InvalidOperationException("The frozen false tiered-compilation configuration is required.");

            var series = new LineSeries
            {
                MarkerSize = 0, ShowMarkers = false, SamplingMode = ChartSamplingMode.MinMax,
                CategoryPath = nameof(MemoryRow.X), ValuePath = nameof(MemoryRow.Y), IdPath = nameof(MemoryRow.Id),
                Stroke = new SolidColorBrush(Color.Parse("#155CC1")), Fill = new SolidColorBrush(Color.Parse("#155CC1")),
                StrokeThickness = 2, ShowDataLabels = false,
            };
            var chart = new CartesianChart
            {
                Width = 1200, Height = 650, Background = Brushes.White, Foreground = Brushes.Black,
                FontFamily = new FontFamily("DejaVu Sans, Segoe UI"), FontSize = 12,
                ShowLegend = false, ShowGridLines = true, AnimationTriggers = ChartAnimationTriggers.None,
                XAxis = new NumericAxis { Minimum = -1, Maximum = 1_000_000, DesiredTickCount = 6 },
                YAxis = new NumericAxis { Minimum = -2, Maximum = 194, DesiredTickCount = 6 },
            };
            chart.Series.Add(series); window.Content = chart; window.UpdateLayout(); _ = chart.PlotArea; _ = chart.VisibleRange;
            using var bitmap = new RenderTargetBitmap(new PixelSize(1200, 650), new Vector(96, 96));
            bitmap.Render(chart); // Matching empty chart/font/RTB startup in both modes.
            var environment = new
            {
                os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                runtime = RuntimeInformation.FrameworkDescription, backend = window.PlatformImpl?.GetType().FullName,
                windowDips = new[] { window.ClientSize.Width, window.ClientSize.Height }, renderScaling = window.RenderScaling,
                renderer = "software", surface, culture = CultureInfo.CurrentCulture.Name, tieredCompilation = false,
                gcServer = System.Runtime.GCSettings.IsServerGC, gcLatency = System.Runtime.GCSettings.LatencyMode.ToString(),
                fontFamily = "DejaVu Sans, Segoe UI", fontSize = 12, fontProbes = FontProbes(),
                chartDips = new[] { 1200, 650 }, rasterPixels = new[] { 1200, 650 }, dpi = new[] { 96, 96 },
                fixedXAxis = new[] { -1d, 1_000_000d }, fixedYAxis = new[] { -2d, 194d },
                samplingMode = "MinMax", strokeThickness = 2, markerSize = 0, showMarkers = false,
                showLegend = false, showGridLines = true, showDataLabels = false, animationTriggers = "None", seriesCount = 1,
                display = Environment.GetEnvironmentVariable("DISPLAY"), waylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"),
                fontconfigFile = Environment.GetEnvironmentVariable("FONTCONFIG_FILE"),
                sources = new[] { Fingerprint(typeof(Program).Assembly.Location), Fingerprint(typeof(CartesianChart).Assembly.Location),
                    Fingerprint(typeof(ChartSkiaRenderingExtensions).Assembly.Location), Fingerprint(typeof(ChartsTheme).Assembly.Location),
                    Fingerprint(Path.ChangeExtension(typeof(Program).Assembly.Location, ".runtimeconfig.json")),
                    Fingerprint(Path.ChangeExtension(typeof(Program).Assembly.Location, ".deps.json")),
                    Fingerprint(typeof(AvaloniaObject).Assembly.Location), Fingerprint(typeof(ISkiaSharpApiLeaseFeature).Assembly.Location),
                    Fingerprint(typeof(SkiaSharp.SKCanvas).Assembly.Location), Fingerprint(typeof(HarfBuzzSharp.Font).Assembly.Location) },
            };
            Write(_output, "ready.json", new { readyQpc = Stopwatch.GetTimestamp(), readyUtc = DateTimeOffset.UtcNow, environment });
            await AwaitControl("sampler-ready.json");
            var samplerReadyObserved = Stopwatch.GetTimestamp();
            var workStart = Stopwatch.GetTimestamp(); var workStartUtc = DateTimeOffset.UtcNow;
            Managed.Add(CaptureManaged("before-generation"));
            Stage("generation", () =>
            {
                if (_mode != "full") return;
                dataset = DatasetFactory.Create(DatasetFactory.Definitions.Single(definition => definition.Id == "S1M-L"));
                foreach (var point in dataset.Points[0])
                    if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) || point.X < -1 || point.X > 1_000_000 || point.Y < -2 || point.Y > 194)
                        throw new InvalidOperationException("The original finite corpus must fit the shared fixed axes without clipping or source filtering.");
                rows = dataset.Points[0].Select((point, index) => new MemoryRow(index, point.X, point.Y)).ToArray();
            });
            Stage("sourceApply", () => series.ItemsSource = rows);
            Stage("prepare", () => { window.UpdateLayout(); _ = chart.PlotArea; _ = chart.VisibleRange; });
            var leaves = 0;
            Stage("record", () =>
            {
                recorded = new DrawingGroup(); using (var context = recorded.Open()) chart.Render(context);
                leaves = Leaves(recorded);
            });
            Stage("controlledRaster", () => bitmap.Render(chart));
            var hitPassed = _mode == "empty";
            Stage("hit", () =>
            {
                if (_mode != "full") return;
                var expected = rows[rows.Length / 2]; var plot = chart.PlotArea; var range = chart.VisibleRange!.Value;
                var position = new Point(plot.Left + (expected.X - range.XMinimum) / (range.XMaximum - range.XMinimum) * plot.Width,
                    plot.Bottom - (expected.Y - range.YMinimum) / (range.YMaximum - range.YMinimum) * plot.Height);
                var hit = chart.HitTestPoint(position, .01);
                hitPassed = hit is not null && ReferenceEquals(hit.Series, series) && hit.Index == expected.Id &&
                    Equals(hit.StableId, expected.Id) && hit.Point.X == expected.X && hit.Point.Y == expected.Y;
            });
            Managed.Add(CaptureManaged("after-work"));
            var workEnd = Stopwatch.GetTimestamp(); var workEndUtc = DateTimeOffset.UtcNow;
            var sampling = chart.GetSamplingInfo(series);
            if (!hitPassed || leaves <= 0 || (_mode == "full" && (rows.Length != 1_000_000 || !sampling.IsApplied || sampling.RenderedCount <= 0)))
                throw new InvalidOperationException("Source, sampling, drawing or original-point guard failed.");
            status = "completed";
            Write(_output, "work-complete.json", new
            {
                status, samplerReadyObservedQpc = samplerReadyObserved, workStartQpc = workStart, workEndQpc = workEnd, workStartUtc, workEndUtc,
                phases = Phases, managed = Managed, inputCount = rows.Length, primitiveCount = leaves, sampling, hitPassed,
                datasetManifest = dataset?.Manifest,
                rawInputRetained = _mode == "full", originalPointValueBytes = rows.LongLength * 16,
                allFinitePointsInsideAxes = true, sourceGeneratedAfterSamplerReady = true,
                emptySourceNoGeneration = _mode == "empty",
                nativeBytes = (long?)null, gpuBytes = (long?)null,
                qualification = "Matched observed process-memory diagnostic; original input remains included in full process measurements. Not instantaneous peak, native/GPU accounting, present, original C7 timing or physical-hardware qualification.",
            });
            await AwaitControl("sampler-complete.json");
            await AwaitControl("release.json");
            GC.KeepAlive(dataset); GC.KeepAlive(rows); GC.KeepAlive(recorded); GC.KeepAlive(chart);
            _exitCode = 0;
        }
        catch (Exception failure) { status = "failed"; error = failure.ToString(); Console.Error.WriteLine(error); }
        finally
        {
            Write(_output, "raw.json", new { status, error, phases = Phases, managed = Managed, finishedQpc = Stopwatch.GetTimestamp(), finishedUtc = DateTimeOffset.UtcNow });
            lifetime.Shutdown(_exitCode);
        }
    }

    private static int Leaves(Drawing drawing) => drawing is DrawingGroup group ? group.Children.Sum(Leaves) : 1;
    private static void Stage(string name, Action action)
    {
        var start = Stopwatch.GetTimestamp(); var utc = DateTimeOffset.UtcNow; var allocated = GC.GetAllocatedBytesForCurrentThread();
        action();
        var end = Stopwatch.GetTimestamp(); var endUtc = DateTimeOffset.UtcNow; var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Phases.Add(new { name, startQpc = start, endQpc = end, startUtc = utc, endUtc, uiThreadAllocatedBytes = bytes });
        Managed.Add(CaptureManaged("after-" + name));
    }
    private static object CaptureManaged(string phase)
    {
        var start = Stopwatch.GetTimestamp(); var utc = DateTimeOffset.UtcNow; var info = GC.GetGCMemoryInfo();
        var managed = GC.GetTotalMemory(false); var allocated = GC.GetTotalAllocatedBytes(false);
        int[] collections = [GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2)];
        return new { phase, startQpc = start, endQpc = Stopwatch.GetTimestamp(), utc,
            estimatedManagedBytes = managed, totalAllocatedBytes = allocated,
            lastGcIndex = info.Index, lastGcGeneration = info.Generation, lastGcHeapSizeBytes = info.HeapSizeBytes,
            lastGcFragmentedBytes = info.FragmentedBytes, lastGcTotalCommittedBytes = info.TotalCommittedBytes,
            collections,
            qualification = "GetTotalMemory(false) estimate and last-GC information; no forced collection, exact live heap or native subtraction." };
    }
    private static async Task AwaitControl(string name)
    {
        var start = Stopwatch.GetTimestamp(); var path = Path.Combine(_control, name);
        while (!File.Exists(path))
        {
            if (Stopwatch.GetElapsedTime(start).TotalSeconds > 90) throw new TimeoutException("Missing control handshake: " + name);
            await Task.Delay(20);
        }
        using var file = File.OpenRead(path); using var document = JsonDocument.Parse(file); var value = document.RootElement;
        if (value.GetProperty("runToken").GetString() != _token || value.GetProperty("processId").GetInt32() != Environment.ProcessId ||
            value.GetProperty("processStartedUtc").GetString() != _processStartedUtc.ToString("O"))
            throw new InvalidDataException("Control handshake identity mismatch: " + name);
        if (value.TryGetProperty("status", out var status) && status.GetString() is not ("ready" or "completed" or "release"))
            throw new InvalidDataException("Control handshake reported failure: " + name);
    }
    private static void Write(string directory, string name, object payload)
    {
        var value = new Dictionary<string, object?>
        {
            ["schema"] = "charts-memory-probe/v1", ["runToken"] = _token, ["mode"] = _mode,
            ["processId"] = Environment.ProcessId, ["processStartedUtc"] = _processStartedUtc.ToString("O"),
            ["windowsCreationFileTimeUtc"] = OperatingSystem.IsWindows() ? _processStartedUtc.ToFileTimeUtc().ToString(CultureInfo.InvariantCulture) : null,
            ["linuxProcStartTicks"] = _linuxStartTicks, ["linuxBootId"] = _linuxBootId, ["stopwatchFrequency"] = Stopwatch.Frequency,
            ["payload"] = payload,
        };
        var path = Path.Combine(directory, name); var temporary = path + ".tmp";
        using (var stream = File.Open(temporary, FileMode.CreateNew)) JsonSerializer.Serialize(stream, value, Json);
        File.Move(temporary, path);
    }
    private static object[] FontProbes()
    {
        var result = new List<object>();
        foreach (var scalar in new[] { 0x30, 0x46 })
        {
            var matched = FontManager.Current.TryMatchCharacter(scalar, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal,
                new FontFamily("DejaVu Sans, Segoe UI"), CultureInfo.CurrentCulture, out var face);
            string? hash = null;
            if (matched && face.GlyphTypeface.PlatformTypeface.TryGetStream(out var stream))
                using (stream) hash = Convert.ToHexString(SHA256.HashData(stream));
            result.Add(new { scalar, matched, font = matched ? face.GlyphTypeface.FamilyName : null, glyph = matched ? face.GlyphTypeface.CharacterToGlyphMap.GetGlyph(scalar) : (ushort?)null, sha256 = hash });
        }
        return result.ToArray();
    }
    private static object Fingerprint(string path)
    {
        using var stream = File.OpenRead(path);
        return new { name = Path.GetFileName(path), bytes = stream.Length, sha256 = Convert.ToHexString(SHA256.HashData(stream)) };
    }
    private sealed record MemoryRow(int Id, double X, double Y);
}

public sealed class MemoryApplication : Application
{
    public override void Initialize() { RequestedThemeVariant = ThemeVariant.Light; Styles.Add(new FluentTheme()); Styles.Add(new ChartsTheme()); }
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new Window { Width = 1200, Height = 650, CanResize = false, Background = Brushes.White, Title = "Charts matched memory diagnostic" };
            desktop.MainWindow = window;
            window.Opened += (_, _) => Dispatcher.UIThread.Post(async () => await Program.RunWindowAsync(window, desktop));
        }
        base.OnFrameworkInitializationCompleted();
    }
}

internal sealed record SurfaceObservation(bool? HasGrContext, string? ContextType, string? Error);
internal sealed class NativeProbe : Control
{
    internal TaskCompletionSource<SurfaceObservation> Observed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public override void Render(DrawingContext context) => context.Custom(new Probe(this, new Rect(Bounds.Size)));
    private sealed class Probe(NativeProbe owner, Rect bounds) : ICustomDrawOperation
    {
        public Rect Bounds { get; } = bounds;
        public bool HitTest(Point p) => false;
        public bool Equals(ICustomDrawOperation? other) => false;
        public void Dispose() { }
        public void Render(ImmediateDrawingContext context)
        {
            try
            {
                var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
                if (feature is null) { owner.Observed.TrySetResult(new(null, null, "Skia lease unavailable")); return; }
                using var lease = feature.Lease(); owner.Observed.TrySetResult(new(lease.GrContext is not null, lease.GrContext?.GetType().FullName, null));
            }
            catch (Exception error) { owner.Observed.TrySetResult(new(null, null, error.ToString())); }
        }
    }
}
