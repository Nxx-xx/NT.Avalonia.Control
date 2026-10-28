using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;
using NT.AvaloniaControl.Charts.Themes;

namespace Charts.NativeBenchmarks;

// A real desktop-window synthetic driver. A raster readback is not compositor
// presentation, a native frame latency, or an estimate of GPU execution time.
internal static class Program
{
    internal static Driver? Current { get; private set; }
    [STAThread]
    public static int Main(string[] args)
    {
#if C7_PERFORMANCE_DRIVER
        if (args.Contains("--c7-alpha-mask-experiment", StringComparer.Ordinal)) return C7AlphaMaskExperiment.Run(args);
        if (args.Contains("--c7-picture-paint-experiment", StringComparer.Ordinal)) return C7PicturePaintExperiment.Run(args);
        if (args.Contains("--c7-fast-circle-paint-experiment", StringComparer.Ordinal)) return C7FastCirclePaintExperiment.Run(args);
        if (args.Contains("--c7-unit-ring-experiment", StringComparer.Ordinal)) return C7UnitRingExperiment.Run(args);
        if (args.Contains("--c7-native-geometry-experiment", StringComparer.Ordinal)) return C7NativeGeometryExperiment.Run(args);
        if (args.Contains("--c7-drawing-brush-experiment", StringComparer.Ordinal)) return C7DrawingBrushExperiment.Run(args);
        if (args.Contains("--c7-circle-paint-experiment", StringComparer.Ordinal)) return C7CirclePaintExperiment.Run(args);
        if (args.Contains("--c7-raster-fidelity", StringComparer.Ordinal)) return C7RasterFidelityProgram.Run(args);
        if (args.Contains("--c7-performance", StringComparer.Ordinal)) return C7PerformanceProgram.Run(args);
#endif
        Driver? driver = null;
        try
        {
            var options = Options.Parse(args);
            driver = Current = new Driver(options);
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; driver.Cancel(); };
            AppBuilder.Configure<NativeApplication>().UsePlatformDetect().LogToTrace()
                .StartWithClassicDesktopLifetime([], ShutdownMode.OnExplicitShutdown);
            return driver.Report.Passed ? 0 : 1;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            driver?.Fail(error, "startup-or-dispatch-failed");
            return 1;
        }
        finally
        {
            if (driver is not null)
                try { driver.CompleteFiles(); } finally { driver.Dispose(); }
        }
    }
}

public sealed class NativeApplication : Application
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
            var window = new Window { Width = 960, Height = 620, CanResize = false,
                Title = "C4-08 native synthetic chart driver", Background = Brushes.White };
            desktop.MainWindow = window;
            window.Opened += (_, _) => Dispatcher.UIThread.Post(async () =>
            {
                await Program.Current!.Run(window);
                desktop.Shutdown(Program.Current.Report.Passed ? 0 : 1);
            });
            window.Show();
        }
        base.OnFrameworkInitializationCompleted();
    }
}

internal sealed record Options(string Output, string Mode, string SourceRoot, int Samples, int Warmups, string[] Scenarios)
{
    internal static readonly string[] AllScenarios = ["line-10k", "scatter-10k", "bar-10k", "multi-4x512"];
    internal static Options Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var keys = new[] { "--output", "--mode", "--source-root", "--samples", "--warmups", "--scenarios" };
        for (var i = 0; i < args.Length; i += 2)
            if (!keys.Contains(args[i], StringComparer.Ordinal) || i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal) || !values.TryAdd(args[i], args[i + 1]))
                throw new ArgumentException("Unknown, duplicate, or missing option value.");
        string Required(string key) => values.TryGetValue(key, out var value) ? value : throw new ArgumentException($"{key} is required.");
        int Count(string key, int fallback, int min, int max) => !values.TryGetValue(key, out var v) ? fallback :
            int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= min && n <= max ? n : throw new ArgumentException($"Invalid {key}.");
        var output = Path.GetFullPath(Required("--output"));
        var supervisorFiles = new[] { "process.json", "stdout.log", "stderr.log" };
        if (File.Exists(output) || Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any(p => !supervisorFiles.Contains(Path.GetFileName(p), StringComparer.Ordinal)))
            throw new IOException("Output must be new or contain only supervisor process/stdout/stderr receipts; old producer evidence is never overwritten.");
        var mode = Required("--mode");
        if (mode is not ("DesktopSynthetic" or "NativeSynthetic" or "XvfbSynthetic")) throw new ArgumentException("Unknown native display mode.");
        if (OperatingSystem.IsWindows() && mode != "DesktopSynthetic" || OperatingSystem.IsLinux() && mode == "DesktopSynthetic")
            throw new ArgumentException("Display mode does not match the operating system.");
        var source = Path.GetFullPath(Required("--source-root"));
        if (!File.Exists(Path.Combine(source, "Directory.Build.props")) || !File.Exists(Path.Combine(source, "benchmarks", "Charts.NativeBenchmarks", "Program.cs")))
            throw new ArgumentException("Source root does not contain the native driver.");
        var scenarios = values.TryGetValue("--scenarios", out var selected) ? selected.Split(',', StringSplitOptions.TrimEntries) : AllScenarios;
        if (scenarios.Length == 0 || scenarios.Distinct().Count() != scenarios.Length || scenarios.Any(s => !AllScenarios.Contains(s, StringComparer.Ordinal)))
            throw new ArgumentException("Unknown, duplicate, or empty scenario.");
        return new(output, mode, source, Count("--samples", 12, 12, 120), Count("--warmups", 2, 2, 20), scenarios);
    }
}

internal sealed class Driver : IDisposable
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly Options _options;
    private readonly StreamWriter _events;
    private readonly StreamWriter _log;
    private readonly Stopwatch _lifetime = Stopwatch.StartNew();
    private volatile bool _canceled;
    private int _eventIndex;
    private int _snapshotIndex;
    internal Report Report { get; }

    internal Driver(Options options)
    {
        _options = options; Directory.CreateDirectory(options.Output);
        Directory.CreateDirectory(Path.Combine(options.Output, "png")); Directory.CreateDirectory(Path.Combine(options.Output, "datasets"));
        Directory.CreateDirectory(Path.Combine(options.Output, "snapshots"));
        _events = new StreamWriter(File.Open(Path.Combine(options.Output, "events.jsonl"), FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
        _log = new StreamWriter(File.Open(Path.Combine(options.Output, "driver.log"), FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
        var manifest = SourceManifest(options.SourceRoot);
        var driverManifest = manifest.Where(m => m.Path.StartsWith("benchmarks/", StringComparison.Ordinal)).ToArray();
        Report = new Report { Options = options, SourceManifest = manifest.Where(m => !m.Path.StartsWith("benchmarks/", StringComparison.Ordinal)).ToArray(),
            DriverSourceManifest = driverManifest, DriverSourceSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                string.Join('\n', driverManifest.Select(m => $"{m.Path}|{m.Bytes}|{m.Sha256}"))))) };
        WriteNewJson(Path.Combine(_options.Output, "raw.initial.json")); Write(); Event("driver-created", null, null);
    }

    internal void Cancel() { _canceled = true; }
    private void CheckDeadline()
    {
        if (_canceled) throw new OperationCanceledException("Console cancellation requested.");
        if (_lifetime.Elapsed.TotalSeconds > 120) throw new TimeoutException("120 second cooperative driver deadline exceeded.");
    }

    internal async Task Run(Window window)
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            window.UpdateLayout(); CheckWindow(window);
            Report.RuntimeConfiguration = ReadRuntimeConfiguration();
            Report.Environment = Environment(window, Report.RuntimeConfiguration);
            ValidateRuntimeConfiguration(Report.RuntimeConfiguration);
            foreach (var scenarioId in _options.Scenarios)
            {
                CheckDeadline();
                var scene = CreateScenario(scenarioId); window.Content = scene.Host;
                window.UpdateLayout(); await AnimationCallback(window); CheckWindow(window);
                var scenario = new Scenario { Id = scenarioId, ChartCount = scene.Charts.Length, PointCountPerChart = scene.Count,
                    DatasetPath = $"datasets/{scenarioId}.json", DatasetSha256 = WriteDataset(scenarioId, scene.Count, scene.Charts.Length) };
                Report.Scenarios.Add(scenario); Write(); Event("scenario-started", scenarioId, null);
                for (var index = 0; index < _options.Warmups + _options.Samples; index++)
                {
                    CheckDeadline();
                    var sample = new Sample { Index = index, Phase = index < _options.Warmups ? "warmup" : "measured",
                        CorrelationId = $"{scenarioId}-{index:D3}", RequestedSceneRevision = index + 1,
                        Action = (index % 3) switch { 0 => "viewport", 1 => "data", _ => "routed-key-reset" } };
                    scenario.Samples.Add(sample); Write(); Event("request-started", scenarioId, sample.CorrelationId);
                    try
                    {
                        await Sample(window, scene, scenario, sample);
                        sample.Outcome = _lifetime.Elapsed.TotalSeconds > 120 ? "late" : "completed";
                        sample.Passed = sample.Outcome == "completed" && sample.Checks.Values.All(v => v);
                        if (!sample.Passed) throw new InvalidOperationException("Native sample quality or lifecycle check failed.");
                    }
                    catch (Exception error)
                    {
                        sample.Outcome = error is TimeoutException ? "timeout" : error is OperationCanceledException ? "canceled" : sample.Outcome == "late" ? "late" : "failed";
                        sample.Error = error.ToString(); sample.Passed = false;
                        sample.Ticks["terminal"] = Stopwatch.GetTimestamp(); Write(); Event("request-terminal", scenarioId, sample.CorrelationId);
                        throw;
                    }
                    sample.Ticks["terminal"] = Stopwatch.GetTimestamp(); Write(); Event("request-completed", scenarioId, sample.CorrelationId);
                }
                scenario.Passed = scenario.Samples.Count == _options.Samples + _options.Warmups && scenario.Samples.All(s => s.Passed);
                scene.Host.Children.Clear(); window.Content = null; Write();
            }
            Report.Passed = Report.Scenarios.Count == _options.Scenarios.Length && Report.Scenarios.All(s => s.Passed);
            Report.Status = Report.Passed ? "completed" : "failed";
        }
        catch (Exception error) { Fail(error, error is TimeoutException ? "timeout" : error is OperationCanceledException ? "canceled" : "failed"); }
        finally
        {
            Report.FinishedUtc = DateTimeOffset.UtcNow; Report.AssemblyManifest = AssemblyManifest();
            Report.NativeModules = NativeModules(); Write(); Event("driver-terminal", null, null);
        }
    }

    private async Task Sample(Window window, Scene scene, Scenario scenario, Sample sample)
    {
        // No await between allocation counter readings: this is the apply/layout
        // interval on the UI thread, not all render-thread/process allocations.
        CheckWindow(window); sample.NativeWindowVisible = true;
        sample.SourceVersionsBefore = scene.Series.Select(s => s.SourceVersion).ToArray();
        sample.DataRevisionBefore = scene.DataRevision;
        sample.Ticks["request"] = Stopwatch.GetTimestamp();
        var before = GC.GetAllocatedBytesForCurrentThread();
        if (sample.Action == "viewport")
        {
            scene.ViewMinimum = scene.Count * .2; scene.ViewMaximum = scene.Count * .8;
            foreach (var chart in scene.Charts) chart.Viewport = new ChartViewport(scene.ViewMinimum, scene.ViewMaximum, 0, 120);
        }
        else if (sample.Action == "data")
        {
            scene.DataRevision++;
            foreach (var series in scene.Series) series.ItemsSource = Rows(scene.Count, scene.DataRevision);
        }
        else
        {
            sample.InputHandled = true;
            foreach (var chart in scene.Charts)
            {
                var input = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Source = chart, Key = Key.Home, KeyModifiers = KeyModifiers.Control };
                chart.RaiseEvent(input); sample.InputHandled &= input.Handled;
            }
            scene.ViewMinimum = 0; scene.ViewMaximum = scene.Count - 1;
        }
        sample.Ticks["applyComplete"] = Stopwatch.GetTimestamp(); window.UpdateLayout();
        foreach (var chart in scene.Charts) _ = chart.PlotArea;
        sample.Ticks["layoutComplete"] = Stopwatch.GetTimestamp();
        sample.UiApplyLayoutAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        sample.SourceVersionsAfter = scene.Series.Select(s => s.SourceVersion).ToArray();
        sample.DataRevisionAfter = scene.DataRevision;
        sample.Ticks["animationCallback"] = await AnimationCallback(window); CheckDeadline(); CheckWindow(window);
        sample.CapturePath = $"png/{scenario.Id}-{sample.Index:D3}.png";
        before = GC.GetAllocatedBytesForCurrentThread(); sample.Ticks["captureStart"] = Stopwatch.GetTimestamp();
        var raster = Capture(window, Path.Combine(_options.Output, sample.CapturePath));
        sample.Ticks["captureComplete"] = Stopwatch.GetTimestamp();
        sample.CaptureReadbackAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        sample.CaptureSha256 = FileHash(Path.Combine(_options.Output, sample.CapturePath));
        sample.PixelSha256 = raster.Sha256; sample.PixelWidth = raster.Width; sample.PixelHeight = raster.Height;
        sample.NonWhitePixels = raster.NonWhitePixels; sample.BusinessInkPixels = raster.BusinessInkPixels;
        foreach (var chart in scene.Charts)
        {
            var plot = chart.PlotArea; var origin = chart.TranslatePoint(plot.TopLeft, window) ?? throw new InvalidOperationException("Chart plot is detached from native window.");
            var roi = new Rect(origin, plot.Size); sample.PlotPixelRects.Add([roi.X, roi.Y, roi.Width, roi.Height]);
            sample.PlotInkPixels.Add(CountBusinessInk(raster.Pixels, raster.Width, raster.Height, roi));
        }
        sample.Ticks["hitStart"] = Stopwatch.GetTimestamp(); before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < scene.Charts.Length; i++)
        {
            var chart = scene.Charts[i]; var ordinal = scene.Count / 2 + sample.Index;
            // Ten thousand minimum-width bars overlap in screen space. Query a
            // deterministic maximum-height bar so no later shorter bar covers
            // its top edge; retain exact original-index/identity assertions.
            if (scenario.Id == "bar-10k")
            {
                ordinal = scene.Count / 2;
                while (ordinal * 37 % 71 != 70) ordinal++;
                ordinal += sample.Index * 71;
            }
            var expected = RowAt(ordinal, scene.DataRevision); var plot = chart.PlotArea; var range = chart.VisibleRange!.Value;
            var p = new Point(plot.Left + (expected.X - range.XMinimum) / (range.XMaximum - range.XMinimum) * plot.Width,
                plot.Bottom - expected.Y / 120 * plot.Height);
            var hit = chart.HitTestPoint(p, .01);
            sample.Hits.Add(new Hit { ChartIndex = i, ExpectedIndex = ordinal, ExpectedStableId = expected.Id, ExpectedX = expected.X, ExpectedY = expected.Y,
                ActualIndex = hit?.Index, ActualStableId = hit?.StableId?.ToString(), ActualX = hit?.Point.X, ActualY = hit?.Point.Y,
                QueryX = p.X, QueryY = p.Y, Plot = new[] { plot.X, plot.Y, plot.Width, plot.Height },
                Range = new[] { range.XMinimum, range.XMaximum, range.YMinimum, range.YMaximum },
                Passed = hit is not null && ReferenceEquals(hit.Series, scene.Series[i]) && hit.Index == ordinal && Equals(hit.StableId, expected.Id) && hit.Point.X == expected.X && hit.Point.Y == expected.Y });
        }
        sample.HitAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before; sample.Ticks["hitComplete"] = Stopwatch.GetTimestamp();
        // Reconstruct the same deterministic data/view in a fresh control tree.
        // This cold-tree raster control uses the same stable public chart APIs as
        // an older base revision; it is outside all measured intervals.
        var canonicalScene = CreateScenario(scenario.Id, scene.DataRevision);
        for (var i = 0; i < scene.Charts.Length; i++) canonicalScene.Charts[i].Viewport = scene.Charts[i].Viewport;
        window.Content = canonicalScene.Host; window.UpdateLayout();
        foreach (var chart in canonicalScene.Charts) _ = chart.PlotArea;
        sample.CanonicalPath = $"png/{scenario.Id}-{sample.Index:D3}-canonical.png";
        var canonical = Capture(window, Path.Combine(_options.Output, sample.CanonicalPath));
        sample.CanonicalPixelSha256 = canonical.Sha256; sample.CanonicalSha256 = FileHash(Path.Combine(_options.Output, sample.CanonicalPath));
        window.Content = scene.Host; window.UpdateLayout(); canonicalScene.Host.Children.Clear();
        sample.Checks = new()
        {
            ["visible-native-window"] = sample.NativeWindowVisible,
            ["nonempty-raster"] = raster.NonWhitePixels >= 1000 && raster.Width >= 800 && raster.Height >= 400,
            ["business-ink-in-every-plot"] = sample.PlotInkPixels.Count == scene.Charts.Length && sample.PlotInkPixels.All(n => n >= 10),
            ["retained-cold-tree-pixels-equal"] = raster.Sha256 == canonical.Sha256,
            ["raw-original-point-hits"] = sample.Hits.Count == scene.Charts.Length && sample.Hits.All(h => h.Passed),
            ["routed-key-handled"] = sample.Action != "routed-key-reset" || sample.InputHandled == true,
            ["data-revision"] = sample.DataRevisionAfter == sample.DataRevisionBefore + (sample.Action == "data" ? 1 : 0),
            ["product-source-version"] = sample.SourceVersionsAfter.Zip(sample.SourceVersionsBefore).All(p => sample.Action == "data" ? p.First > p.Second : p.First == p.Second),
            ["native-window-still-visible"] = window.IsVisible && window.TryGetPlatformHandle() is { Handle: not 0 },
        };
    }

    private static Scene CreateScenario(string id, int revision = 0)
    {
        var count = id == "multi-4x512" ? 512 : 10_000; var chartCount = id == "multi-4x512" ? 4 : 1;
        var grid = new Grid { Background = Brushes.White, Margin = new Thickness(8), RowDefinitions = new RowDefinitions(chartCount == 4 ? "*,*" : "*"), ColumnDefinitions = new ColumnDefinitions(chartCount == 4 ? "*,*" : "*") };
        var charts = new CartesianChart[chartCount]; var allSeries = new ChartSeries[chartCount];
        for (var i = 0; i < chartCount; i++)
        {
            ChartSeries series = id.StartsWith("scatter", StringComparison.Ordinal) ? new ScatterSeries { MarkerSize = 3 } :
                id.StartsWith("bar", StringComparison.Ordinal) ? new BarSeries() : new LineSeries { MarkerSize = 0, SamplingMode = ChartSamplingMode.None };
            series.ItemsSource = Rows(count, revision); series.CategoryPath = nameof(Row.X); series.ValuePath = nameof(Row.Y); series.IdPath = nameof(Row.Id);
            series.Stroke = new SolidColorBrush(Color.Parse("#155CC1")); series.Fill = new SolidColorBrush(Color.Parse("#155CC1"));
            var chart = new CartesianChart { ShowLegend = false, Background = Brushes.White, Foreground = Brushes.Black,
                FontFamily = new FontFamily("DejaVu Sans, Segoe UI"), FontSize = 12, AnimationTriggers = ChartAnimationTriggers.None,
                XAxis = new NumericAxis { Minimum = 0, Maximum = count - 1 }, YAxis = new NumericAxis { Minimum = 0, Maximum = 120 },
                IsKeyboardNavigationEnabled = true, IsPointBrowsingEnabled = true, Title = $"{id} / {i}" };
            chart.Series.Add(series); Grid.SetRow(chart, i / 2); Grid.SetColumn(chart, i % 2); grid.Children.Add(chart);
            charts[i] = chart; allSeries[i] = series;
        }
        return new Scene(grid, charts, allSeries, count);
    }

    private static Row RowAt(int index, int revision) => new($"row-{index}", index, 20 + index * 37 % 71 + revision * .25);
    private static Row[] Rows(int count, int revision) => Enumerable.Range(0, count).Select(i => RowAt(i, revision)).ToArray();
    private string WriteDataset(string id, int count, int charts)
    {
        var path = Path.Combine(_options.Output, "datasets", id + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(new { Schema = "charts-native-dataset-v1", Scenario = id, ChartCount = charts, Count = count,
            Formula = "Id=row-{i};X=i;Y=20+(i*37%71)+revision*0.25", Rows = Rows(count, 0) }, Json), new UTF8Encoding(false));
        return FileHash(path);
    }

    private static void CheckWindow(Window window)
    {
        if (!window.IsVisible || window.TryGetPlatformHandle() is not { Handle: not 0 } || window.ClientSize.Width < 800 || window.ClientSize.Height < 400)
            throw new InvalidOperationException("A visible, sized, real desktop native window is required.");
        if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name?.Contains("Headless", StringComparison.OrdinalIgnoreCase) == true))
            throw new InvalidOperationException("Headless is forbidden in this native producer.");
    }

    private async Task<long> AnimationCallback(Window window)
    {
        var source = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        window.RequestAnimationFrame(_ => source.TrySetResult(Stopwatch.GetTimestamp()));
        var winner = await Task.WhenAny(source.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        if (winner != source.Task) throw new TimeoutException("Native animation callback did not arrive within five seconds; this callback does not prove presentation.");
        return await source.Task;
    }

    private static Raster Capture(Window window, string path)
    {
        var size = new PixelSize((int)Math.Ceiling(window.ClientSize.Width), (int)Math.Ceiling(window.ClientSize.Height));
        using var bitmap = new RenderTargetBitmap(size, new Vector(96, 96)); bitmap.Render(window); bitmap.Save(path);
        using var converted = new WriteableBitmap(size, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var frame = converted.Lock(); bitmap.CopyPixels(frame);
        var raw = new byte[checked(size.Width * size.Height * 4)]; var line = new byte[frame.RowBytes]; var nonWhite = 0;
        for (var y = 0; y < size.Height; y++)
        {
            Marshal.Copy(frame.Address + y * frame.RowBytes, line, 0, line.Length);
            Array.Copy(line, 0, raw, y * size.Width * 4, size.Width * 4);
            for (var x = 0; x < size.Width; x++) if (line[x * 4 + 3] >= 250 && (line[x * 4] < 240 || line[x * 4 + 1] < 240 || line[x * 4 + 2] < 240)) nonWhite++;
        }
        return new(size.Width, size.Height, Convert.ToHexString(SHA256.HashData(raw)), nonWhite,
            CountBusinessInk(raw, size.Width, size.Height, new Rect(new Size(size.Width, size.Height))), raw);
    }

    private static int CountBusinessInk(byte[] pixels, int width, int height, Rect bounds)
    {
        var count = 0;
        for (var y = Math.Max(0, (int)Math.Ceiling(bounds.Top)); y < Math.Min(height, (int)Math.Floor(bounds.Bottom)); y++)
            for (var x = Math.Max(0, (int)Math.Ceiling(bounds.Left)); x < Math.Min(width, (int)Math.Floor(bounds.Right)); x++)
            {
                var offset = (y * width + x) * 4;
                if (pixels[offset + 3] >= 250 && Math.Abs(pixels[offset] - 193) <= 24 && Math.Abs(pixels[offset + 1] - 92) <= 24 && Math.Abs(pixels[offset + 2] - 21) <= 24) count++;
            }
        return count;
    }

    private static RuntimeConfigurationEvidence ReadRuntimeConfiguration()
    {
        var path = Path.Combine(AppContext.BaseDirectory, typeof(Driver).Assembly.GetName().Name + ".runtimeconfig.json");
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        bool? declared = null;
        if (document.RootElement.TryGetProperty("runtimeOptions", out var runtime) && runtime.TryGetProperty("configProperties", out var properties) &&
            properties.TryGetProperty("System.Runtime.TieredCompilation", out var setting) && setting.ValueKind is JsonValueKind.True or JsonValueKind.False)
            declared = setting.GetBoolean();
        var present = AppContext.TryGetSwitch("System.Runtime.TieredCompilation", out var value);
        return new(path, new FileInfo(path).Length, FileHash(path), declared, present, present ? value : null,
            System.Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"), System.Environment.GetEnvironmentVariable("COMPlus_TieredCompilation"));
    }

    private static void ValidateRuntimeConfiguration(RuntimeConfigurationEvidence evidence)
    {
        if (evidence.DeclaredTieredCompilation is not false || !evidence.AppContextSwitchPresent || evidence.AppContextSwitchValue is not false)
            throw new InvalidOperationException("Protocol requires System.Runtime.TieredCompilation=false in the hashed runtimeconfig and a present false AppContext switch; unavailable switch evidence cannot qualify.");
        if (evidence.DotnetOverride is not (null or "0") || evidence.ComPlusOverride is not (null or "0"))
            throw new InvalidOperationException("Protocol requires DOTNET_TieredCompilation and COMPlus_TieredCompilation to be unset or literal 0; an unknown or enabled override cannot qualify.");
    }

    private static object Environment(Window window, RuntimeConfigurationEvidence configuration) => new
    {
        Os = RuntimeInformation.OSDescription, OsArchitecture = RuntimeInformation.OSArchitecture.ToString(), ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
        Runtime = RuntimeInformation.FrameworkDescription, RuntimeVersion = System.Environment.Version.ToString(), CpuCount = System.Environment.ProcessorCount,
        Display = System.Environment.GetEnvironmentVariable("DISPLAY"), WaylandDisplay = System.Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"),
        NativeHandleDescriptor = window.TryGetPlatformHandle()?.HandleDescriptor, RenderScaling = window.RenderScaling,
        RasterDpi = new[] { 96, 96 }, PixelFormat = "Bgra8888/Premul", Theme = "Light", Culture = CultureInfo.CurrentCulture.Name,
        DefaultFont = FontManager.Current.DefaultFontFamily.ToString(), FontFamilies = FontManager.Current.SystemFonts.Select(f => f.Name).Order(StringComparer.Ordinal).ToArray(),
        Backend = "Avalonia.Desktop UsePlatformDetect (loaded assembly/module manifest identifies selected backend)",
        StopwatchFrequency = Stopwatch.Frequency, StopwatchHighResolution = Stopwatch.IsHighResolution,
        TieredCompilation = configuration.AppContextSwitchValue,
        TieredCompilationSwitchPresent = configuration.AppContextSwitchPresent,
        TieredCompilationSwitchValue = configuration.AppContextSwitchValue,
        DotnetTieredCompilationOverride = configuration.DotnetOverride,
        ComPlusTieredCompilationOverride = configuration.ComPlusOverride,
    };

    private static ManifestEntry[] SourceManifest(string root)
    {
        var dirs = new[] { "benchmarks/Charts.NativeBenchmarks", "src/NT.AvaloniaControl.Charts", "src/NT.AvaloniaControl.Charts.Themes" };
        var files = dirs.SelectMany(d => Directory.EnumerateFiles(Path.Combine(root, d), "*", SearchOption.AllDirectories))
            .Where(f => !Path.GetRelativePath(root, f).Split(Path.DirectorySeparatorChar).Any(p => p is "bin" or "obj" or ".avalonia-build-tasks"))
            .Where(f => new[] { ".cs", ".csproj", ".axaml", ".json" }.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .Append(Path.Combine(root, "Directory.Build.props")).Append(Path.Combine(root, "Directory.Packages.props"));
        return files.Order(StringComparer.Ordinal).Select(f => new ManifestEntry(Path.GetRelativePath(root, f).Replace('\\', '/'), new FileInfo(f).Length, FileHash(f))).ToArray();
    }
    private static ManifestEntry[] AssemblyManifest() => AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
        .Select(a => a.Location).Distinct().Order(StringComparer.Ordinal).Select(f => new ManifestEntry(f, new FileInfo(f).Length, FileHash(f))).ToArray();
    private static object NativeModules()
    {
        try { using var process = Process.GetCurrentProcess(); return new { Available = true, Error = (string?)null, Files = process.Modules.Cast<ProcessModule>().Select(m => m.FileName).Order(StringComparer.Ordinal).ToArray() }; }
        catch (Exception error) { return new { Available = false, Error = error.ToString(), Files = Array.Empty<string>() }; }
    }
    private static string FileHash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
    internal void Fail(Exception error, string status) { Report.Passed = false; Report.Status = status; Report.Error = error.ToString(); Report.FinishedUtc = DateTimeOffset.UtcNow; Write(); Event("driver-failed", null, null); }
    private void Write()
    {
        // Append immutable reports. On Windows an unrelated reader/indexer may
        // hold a JSON file without FILE_SHARE_DELETE, so atomically replacing a
        // frequently updated raw.json can fail despite a writable directory.
        // No report is overwritten or retried; the terminal file is created once.
        WriteNewJson(Path.Combine(_options.Output, "snapshots", $"report-{_snapshotIndex++:D6}.json"));
    }
    internal void CompleteFiles() => WriteNewJson(Path.Combine(_options.Output, "raw.json"));
    private void WriteNewJson(string path)
    {
        using var stream = File.Open(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        JsonSerializer.Serialize(stream, Report, Json);
    }
    private void Event(string kind, string? scenario, string? correlation)
    {
        var entry = new { Index = _eventIndex++, Kind = kind, Scenario = scenario, CorrelationId = correlation, Ticks = Stopwatch.GetTimestamp(), Utc = DateTimeOffset.UtcNow };
        _events.WriteLine(JsonSerializer.Serialize(entry, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var line = $"{entry.Index} {entry.Utc:O} {kind} {scenario} {correlation}"; _log.WriteLine(line); Console.WriteLine(line);
    }
    public void Dispose() { _events.Dispose(); _log.Dispose(); }
    private sealed record Raster(int Width, int Height, string Sha256, int NonWhitePixels, int BusinessInkPixels, byte[] Pixels);
    private sealed class Scene(Grid host, CartesianChart[] charts, ChartSeries[] series, int count)
    {
        internal Grid Host { get; } = host; internal CartesianChart[] Charts { get; } = charts; internal ChartSeries[] Series { get; } = series;
        internal int Count { get; } = count; internal int DataRevision; internal double ViewMinimum; internal double ViewMaximum = count - 1;
    }
}

internal sealed record Row(string Id, double X, double Y);
internal sealed record ManifestEntry(string Path, long Bytes, string Sha256);
internal sealed record RuntimeConfigurationEvidence(string Path, long Bytes, string Sha256, bool? DeclaredTieredCompilation,
    bool AppContextSwitchPresent, bool? AppContextSwitchValue, string? DotnetOverride, string? ComPlusOverride);
internal sealed class Report
{
    public string Schema { get; } = "charts-native-window-v1";
    public string RunId { get; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset StartedUtc { get; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedUtc { get; set; }
    public bool Passed { get; set; }
    public string Status { get; set; } = "running";
    public string? Error { get; set; }
    public required Options Options { get; init; }
    public string Measurement { get; } = "request-to-controlled-native-raster-readback; UI-apply/layout and UI capture allocations; synthetic routed keyboard and public viewport/data requests";
    public bool ActualNativeWindowRequired { get; } = true;
    public bool PhysicalInputMeasured { get; } = false;
    public bool PresentMeasured { get; } = false;
    public bool GpuMeasured { get; } = false;
    public double? NativePresentTicks { get; } = null;
    public double? GpuTicks { get; } = null;
    public string UnavailableReason { get; } = "RenderTargetBitmap copies raster pixels; RequestAnimationFrame is a scheduling callback, neither is a compositor-present or GPU fence.";
    public object? Environment { get; set; }
    public RuntimeConfigurationEvidence? RuntimeConfiguration { get; set; }
    public required ManifestEntry[] SourceManifest { get; init; }
    public required ManifestEntry[] DriverSourceManifest { get; init; }
    public required string DriverSourceSha256 { get; init; }
    public ManifestEntry[] AssemblyManifest { get; set; } = [];
    public object? NativeModules { get; set; }
    public List<Scenario> Scenarios { get; } = [];
}
internal sealed class Scenario
{
    public required string Id { get; init; }
    public int ChartCount { get; init; }
    public int PointCountPerChart { get; init; }
    public required string DatasetPath { get; init; }
    public required string DatasetSha256 { get; init; }
    public List<Sample> Samples { get; } = [];
    public bool Passed { get; set; }
}
internal sealed class Sample
{
    public required string CorrelationId { get; init; }
    public required string Phase { get; init; }
    public required string Action { get; init; }
    public int Index { get; init; }
    public long RequestedSceneRevision { get; init; }
    public string Outcome { get; set; } = "started";
    public bool Passed { get; set; }
    public string? Error { get; set; }
    public bool NativeWindowVisible { get; set; }
    public bool? InputHandled { get; set; }
    public int DataRevisionBefore { get; set; }
    public int DataRevisionAfter { get; set; }
    public long[] SourceVersionsBefore { get; set; } = [];
    public long[] SourceVersionsAfter { get; set; } = [];
    public Dictionary<string, long> Ticks { get; } = [];
    public long UiApplyLayoutAllocatedBytes { get; set; }
    public long CaptureReadbackAllocatedBytes { get; set; }
    public long HitAllocatedBytes { get; set; }
    public string? CapturePath { get; set; }
    public string? CaptureSha256 { get; set; }
    public string? PixelSha256 { get; set; }
    public string? CanonicalPath { get; set; }
    public string? CanonicalSha256 { get; set; }
    public string? CanonicalPixelSha256 { get; set; }
    public int PixelWidth { get; set; }
    public int PixelHeight { get; set; }
    public int NonWhitePixels { get; set; }
    public int BusinessInkPixels { get; set; }
    public List<int> PlotInkPixels { get; } = [];
    public List<double[]> PlotPixelRects { get; } = [];
    public List<Hit> Hits { get; } = [];
    public Dictionary<string, bool> Checks { get; set; } = [];
}
internal sealed class Hit
{
    public int ChartIndex { get; init; }
    public int ExpectedIndex { get; init; }
    public string? ExpectedStableId { get; init; }
    public double ExpectedX { get; init; }
    public double ExpectedY { get; init; }
    public int? ActualIndex { get; init; }
    public string? ActualStableId { get; init; }
    public double? ActualX { get; init; }
    public double? ActualY { get; init; }
    public double QueryX { get; init; }
    public double QueryY { get; init; }
    public double[] Plot { get; init; } = [];
    public double[] Range { get; init; } = [];
    public bool Passed { get; init; }
}
