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

namespace Charts.NativeBenchmarks;

internal static class C7PerformanceProgram
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    internal static C7Options Options { get; private set; } = null!;
    internal static C7Report Report { get; private set; } = null!;
    private static StreamWriter? _requests;
    private static int _checkpoint;

    internal static int Run(string[] args)
    {
        try
        {
            Options = C7Options.Parse(args);
            Directory.CreateDirectory(Options.Output);
            Report = new() { Options = Options };
            Report.Sources = new
            {
                driver = Fingerprint(typeof(C7PerformanceProgram).Assembly.Location),
                charts = Fingerprint(typeof(CartesianChart).Assembly.Location),
                themes = Fingerprint(typeof(ChartsTheme).Assembly.Location),
                nativeBackend = Fingerprint(typeof(ChartSkiaRenderingExtensions).Assembly.Location),
                generator = Fingerprint(Path.Combine(Options.SourceRoot, "benchmarks/Charts.Benchmarks/DatasetFactory.cs")),
                runtimeConfig = Fingerprint(Path.ChangeExtension(typeof(C7PerformanceProgram).Assembly.Location, ".runtimeconfig.json")),
            };
            _requests = new StreamWriter(File.Open(Path.Combine(Options.Output, "requests.jsonl"), FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
            Checkpoint();
            try { ChartNativeTextBinding.Initialize(); }
            finally { Report.NativeTextBeforeUi = ChartNativeTextBinding.Diagnostics; }
            var builder = AppBuilder.Configure<C7PerformanceApplication>().WithChartsSkiaRendering().UsePlatformDetect();
            if (OperatingSystem.IsWindows()) builder.With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Software] });
            else if (OperatingSystem.IsLinux()) builder.With(new X11PlatformOptions { RenderingMode = [X11RenderingMode.Software] });
            else throw new PlatformNotSupportedException("C7 currently measures Windows Win32 or Linux X11 software paths.");
            builder.StartWithClassicDesktopLifetime([], ShutdownMode.OnExplicitShutdown);
            return Report.Passed ? 0 : 1;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            if (Report is not null) { Report.Error = error.ToString(); Report.Status = "failed"; Report.Passed = false; }
            return 1;
        }
        finally
        {
            if (Report is not null)
            {
                Report.FinishedUtc = DateTimeOffset.UtcNow;
                Report.NativeTextAfterRealText = ChartNativeTextBinding.Diagnostics;
                var backend = typeof(ChartSkiaRenderingExtensions).Assembly.GetType("NT.AvaloniaControl.Charts.Skia.ChartNativeCircleOperation");
                Report.NativeBackendDiagnostics = new[] { "ExpandedFrameCount", "FastPaintFrameCount", "TranslatedExpandedFrameCount", "TranslatedFastPaintFrameCount", "FirstRejectedDeviceTransform", "FirstTranslatedOptimizedDeviceTransform" }
                    .ToDictionary(name => name, name => backend?.GetProperty(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.GetValue(null));
                Report.Assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location)).Select(a => Fingerprint(a.Location)).ToArray();
                if (Report.Status is "starting" or "running") { Report.Status = "incomplete"; Report.Passed = false; }
                WriteNew("raw.json", Report);
            }
            _requests?.Dispose();
        }
    }

    internal static async Task RunWindowAsync(Window window, IClassicDesktopStyleApplicationLifetime lifetime)
    {
        try
        {
            Report.Status = "running";
            var surface = new C7NativeSurfaceProbe { Width = 8, Height = 8 };
            window.Content = surface; window.UpdateLayout();
            var compositor = ElementComposition.GetElementVisual(surface)?.Compositor ?? throw new InvalidOperationException("Missing native compositor.");
            surface.InvalidateVisual();
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
            var batch = compositor.RequestCompositionBatchCommitAsync();
            await batch.Rendered.WaitAsync(TimeSpan.FromSeconds(15));
            var renderer = await surface.Observed.Task.WaitAsync(TimeSpan.FromSeconds(15));
            if (renderer.HasGrContext != false || renderer.Error is not null) throw new InvalidOperationException("Requested software renderer was not observed on the native draw surface.");
            var handle = window.TryGetPlatformHandle();
            if (!window.IsVisible || handle is not { Handle: not 0 }) throw new InvalidOperationException("A visible native window is required.");
            var tieredPresent = AppContext.TryGetSwitch("System.Runtime.TieredCompilation", out var tiered);
            var dotnetTiered = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation");
            var comPlusTiered = Environment.GetEnvironmentVariable("COMPlus_TieredCompilation");
            if (!tieredPresent || tiered || dotnetTiered is not (null or "0") || comPlusTiered is not (null or "0"))
                throw new InvalidOperationException("C7 requires the inherited false tiered-compilation switch and no enabled or unknown environment override.");
            Report.NativeEnvironment = new
            {
                os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(), runtime = RuntimeInformation.FrameworkDescription,
                cpuCount = Environment.ProcessorCount, gcServer = System.Runtime.GCSettings.IsServerGC,
                gcLatencyMode = System.Runtime.GCSettings.LatencyMode.ToString(), stopwatchHighResolution = Stopwatch.IsHighResolution,
                culture = CultureInfo.CurrentCulture.Name,
                backend = window.PlatformImpl?.GetType().FullName, nativeHandle = handle.Handle.ToInt64(), nativeHandleDescriptor = handle.HandleDescriptor,
                clientDips = new[] { window.ClientSize.Width, window.ClientSize.Height }, renderScaling = window.RenderScaling,
                requestedRenderer = "software", actualRenderer = "software-raster", nativeSurface = renderer,
                tieredCompilationSwitchPresent = tieredPresent, tieredCompilationSwitchValue = tiered,
                dotnetTieredCompilation = dotnetTiered, comPlusTieredCompilation = comPlusTiered,
                display = Environment.GetEnvironmentVariable("DISPLAY"), waylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"),
                fontconfigFile = Environment.GetEnvironmentVariable("FONTCONFIG_FILE"), xdgCacheHome = Environment.GetEnvironmentVariable("XDG_CACHE_HOME"),
                defaultFont = FontManager.Current.DefaultFontFamily.ToString(), fontProbes = FontProbes(), memoryBeforeCorpus = Memory(),
            };
            window.Content = null;
            foreach (var track in Options.Scenarios)
            {
                try { await RunSceneAsync(window, track); }
                catch (Exception error)
                {
                    Report.SceneErrors[track] = error.ToString();
                    if (Report.Scenes.LastOrDefault() is { } scene && scene.Id == track) { scene.Error = error.ToString(); scene.Passed = false; }
                    window.Content = null;
                    Checkpoint();
                }
            }
            Report.Passed = Report.Scenes.Count == Options.Scenarios.Length && Report.Scenes.All(scene => scene.Passed);
            Report.Status = Report.Passed ? "completed" : "failed";
            if (!Report.Passed) Report.Error = string.Join(Environment.NewLine, Report.Scenes.Where(scene => !scene.Passed).Select(scene => scene.Id + ": " + scene.Error).Concat(Report.SceneErrors.Select(pair => pair.Key + ": " + pair.Value)));
        }
        catch (Exception error) { Report.Error = error.ToString(); Report.Status = error is TimeoutException ? "timeout" : "failed"; Report.Passed = false; }
        finally { lifetime.Shutdown(Report.Passed ? 0 : 1); }
    }

    private static async Task RunSceneAsync(Window window, string track)
    {
        var minmax = track.EndsWith("-minmax", StringComparison.Ordinal);
        var corpusId = track[..track.LastIndexOf('-')];
        var definition = DatasetFactory.Definitions.Single(item => item.Id == corpusId);
        var generatedAt = Stopwatch.GetTimestamp(); var allocated = GC.GetAllocatedBytesForCurrentThread();
        var dataset = DatasetFactory.Create(definition);
        var rows = dataset.Points.Select(points => points.Select((point, index) => new C7Row(index, point.X, point.Y)).ToArray()).ToArray();
        var alternate = Options.FirstUseOnly ? rows : dataset.Points.Select(points => points.Select((point, index) => new C7Row(index, point.X, point.Y + .25)).ToArray()).ToArray();
        var xmin = double.PositiveInfinity; var xmax = double.NegativeInfinity; var ymin = 0d; var ymax = 0d;
        foreach (var points in dataset.Points) foreach (var p in points) { xmin = Math.Min(xmin, p.X); xmax = Math.Max(xmax, p.X); ymin = Math.Min(ymin, p.Y); ymax = Math.Max(ymax, p.Y + .25); }
        var generation = new C7Timing(generatedAt, Stopwatch.GetTimestamp(), GC.GetAllocatedBytesForCurrentThread() - allocated);
        var series = rows.Select((_, i) => definition.Kind switch
        {
            "scatter" => (ChartSeries)new ScatterSeries { MarkerSize = 4 },
            "bar" => new BarSeries(),
            _ => new LineSeries { MarkerSize = 0, ShowMarkers = false, SamplingMode = minmax ? ChartSamplingMode.MinMax : ChartSamplingMode.None },
        }).ToArray();
        foreach (var item in series)
        {
            item.CategoryPath = nameof(C7Row.X); item.ValuePath = nameof(C7Row.Y); item.IdPath = nameof(C7Row.Id);
            item.Stroke = new SolidColorBrush(Color.Parse("#155CC1")); item.Fill = new SolidColorBrush(Color.Parse("#155CC1")); item.StrokeThickness = 2; item.ShowDataLabels = false;
        }
        var result = new C7SceneReport
        {
            Id = track, DatasetManifest = dataset.Manifest, DatasetGeneration = generation,
            RuntimeQuality = new
            {
                samplingMode = minmax ? "MinMax" : "None", strokeThicknessDip = 2, scatterMarkerSizeDip = definition.Kind == "scatter" ? 4 : 0,
                showLegend = false, showGridLines = true, showDataLabels = false, animations = "None", fontSizeDip = 12, fontFamily = "DejaVu Sans, Segoe UI",
                chartDips = new[] { 1200, 650 }, rasterPixels = new[] { 1200, 650 }, rasterDpi = new[] { 96, 96 },
                dataAdapter = "object rows with numeric X/Y and integer original Id", sourceReplacement = "alternate original Y and Y+0.25; X/Id/order/count unchanged; arrays prepared outside measured operations",
                barOptions = series.OfType<BarSeries>().Select(bar => new
                {
                    orientation = bar.Orientation.ToString(), stackMode = bar.StackMode.ToString(), baseline = bar.Baseline,
                    barWidth = bar.BarWidth, widthRatio = bar.WidthRatio, showMarkers = bar.ShowMarkers,
                }).ToArray(),
                corpusManifestDownsamplingIsRuntimeQuality = false,
            },
        };
        Report.Scenes.Add(result);
        for (var i = 0; i < rows.Length; i++)
        {
            var relative = $"datasets/{track}-series-{i}.xy64le";
            var path = Path.Combine(Options.Output, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using (var writer = new BinaryWriter(File.Open(path, FileMode.CreateNew))) foreach (var row in rows[i]) { writer.Write(row.X); writer.Write(row.Y); }
            result.DatasetFiles.Add(new { path = relative, bytes = new FileInfo(path).Length, sha256 = Hash(path), seriesIndex = i, pointCount = rows[i].Length, encoding = "little-endian IEEE754 double X,Y; original index equals ordinal" });
        }
        var chart = new CartesianChart
        {
            Width = 1200, Height = 650, Background = Brushes.White, Foreground = Brushes.Black, FontSize = 12, FontFamily = new FontFamily("DejaVu Sans, Segoe UI"),
            ShowLegend = false, ShowGridLines = true, AnimationTriggers = ChartAnimationTriggers.None,
            XAxis = new NumericAxis { Minimum = xmin - 1, Maximum = xmax + 1, DesiredTickCount = 6 },
            YAxis = new NumericAxis { Minimum = ymin - 1, Maximum = ymax + 1, DesiredTickCount = 6 },
        };
        foreach (var item in series)
        {
            chart.Series.Add(item);
        }
        var scene = new C7Scene(chart, series, rows, alternate, dataset.Manifest.InputPointCount, xmin - 1, xmax + 1, ymin - 1, ymax + 1, definition.Kind);
        result.MemoryEndpoints.Add(new { phase = "after-prepared-corpus-before-source-binding", observation = Memory() });
        Checkpoint();
        var first = new C7Operation { Index = 0, Phase = "first-use", Action = "new-chart-source-bind" }; result.FirstUse = first;
        first.Ticks["start"] = Stopwatch.GetTimestamp();
        var firstAllocated = GC.GetAllocatedBytesForCurrentThread();
        Stage(first, "sourceApply", () => { for (var i = 0; i < series.Length; i++) series[i].ItemsSource = rows[i]; });
        Stage(first, "prepare", () => { window.Content = chart; window.UpdateLayout(); _ = chart.PlotArea; _ = chart.VisibleRange; });
        using var bitmap = new RenderTargetBitmap(new PixelSize(1200, 650), new Vector(96, 96));
        Stage(first, "controlledRaster", () => bitmap.Render(chart));
        first.Hit = Query(scene, 0, "first-use");
        first.Ticks["hitStart"] = first.Hit.Timing.StartTicks; first.Ticks["hitEnd"] = first.Hit.Timing.EndTicks;
        first.Ticks["end"] = Stopwatch.GetTimestamp(); first.Allocations["firstUsableToControlledRaster"] = GC.GetAllocatedBytesForCurrentThread() - firstAllocated;
        result.FirstUseCompletedUtc = DateTimeOffset.UtcNow;
        first.Counts = Counts(scene, Record(chart)); first.MemoryAfter = Memory();
        first.Passed = first.Hit.Passed && first.Counts.InputCount == dataset.Manifest.InputPointCount && first.Counts.RenderedCount > 0;
        Emit(new { track, firstUse = first }); Checkpoint();
        if (!first.Passed) throw new InvalidOperationException("First-use original data or raster-count check failed.");
        SaveRaster(result, bitmap, track + "-first.png", "first-use");
        if (!Options.FirstUseOnly)
        {
            await TraceGuardedAsync(window, scene, result, bitmap, "warmup", Options.WarmupSeconds);
            Checkpoint();
            await TraceGuardedAsync(window, scene, result, bitmap, "measured", Options.MeasureSeconds);
            Checkpoint();
            try
            {
                ResetView(scene); Prepare(window, scene);
                for (var i = 0; i < 100 + 1000; i++)
                {
                    C7Timing? maintenance = null;
                    if (i % 10 == 0)
                    {
                        var start = Stopwatch.GetTimestamp(); var bytes = GC.GetAllocatedBytesForCurrentThread();
                        Replace(scene); Prepare(window, scene);
                        maintenance = new(start, Stopwatch.GetTimestamp(), GC.GetAllocatedBytesForCurrentThread() - bytes);
                    }
                    var query = Query(scene, i, i < 100 ? "warmup" : "measured", maintenance);
                    if (i < 100) result.HitWarmups.Add(query); else result.HitQueries.Add(query);
                    Emit(new { track, query });
                    if (!query.Passed) throw new InvalidOperationException("Independent hit-batch original identity failed.");
                    if (i % 50 == 0) await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                }
            }
            catch (Exception error) { result.SectionErrors["hit-batch"] = error.ToString(); Checkpoint(); }
            try { bitmap.Render(chart); SaveRaster(result, bitmap, track + "-last.png", "after-hit-batch"); }
            catch (Exception error) { result.SectionErrors["terminal-quality-raster"] = error.ToString(); }
            result.Checks["hit-warmup-count"] = result.HitWarmups.Count == 100;
            result.Checks["hit-measured-count"] = result.HitQueries.Count == 1000;
            result.Checks["hit-source-maintenance-at-least-ten-percent"] = result.HitQueries.Count(query => query.Maintenance is not null) >= 100;
            result.MemoryEndpoints.Add(new { phase = "after-interaction-and-hit-batch", observation = Memory() });
        }
        result.Checks["first-use-original-hit"] = first.Passed;
        result.Checks["native-window-still-visible"] = window.IsVisible && window.TryGetPlatformHandle() is { Handle: not 0 };
        result.Checks["all-planned-trace-slots-accounted"] = result.Schedules.All(schedule => schedule.Passed && schedule.Pending == 0);
        result.Checks["trace-raw-point-identities"] = result.TraceSamples.All(sample => sample.Passed);
        result.Checks["all-sections-completed"] = result.SectionErrors.Count == 0;
        result.Passed = result.Checks.Values.All(value => value);
        if (result.SectionErrors.Count > 0) result.Error = string.Join(Environment.NewLine, result.SectionErrors.Select(pair => pair.Key + ": " + pair.Value));
        window.Content = null; chart.Series.Clear();
        Checkpoint();
        Console.WriteLine($"C7 {track}: passed={result.Passed}; traces={result.TraceSamples.Count}; hits={result.HitQueries.Count}");
    }

    private static async Task TraceGuardedAsync(Window window, C7Scene scene, C7SceneReport report, RenderTargetBitmap bitmap, string phase, double duration)
    {
        try { await TraceAsync(window, scene, report, bitmap, phase, duration); }
        catch (TimeoutException error) when (error.Message.StartsWith("C7 fixed-slot phase", StringComparison.Ordinal))
        {
            // A cadence deadline invalidates that phase; it does not erase the
            // independent hit batch or prevent the next track being measured.
            report.SectionErrors[phase + "-trace"] = error.ToString();
            report.MemoryEndpoints.Add(new { phase = "after-" + phase, observation = Memory() });
            Checkpoint();
        }
    }

    private static async Task TraceAsync(Window window, C7Scene scene, C7SceneReport report, RenderTargetBitmap bitmap, string phase, double duration)
    {
        var start = Stopwatch.GetTimestamp(); var frequency = Stopwatch.Frequency;
        var planned = (int)Math.Round(duration * C7Options.RequestsPerSecond, MidpointRounding.AwayFromZero);
        var schedule = new C7Schedule { Phase = phase, Planned = planned, StartTicks = start, PlannedEndTicks = start + (long)(duration * frequency) }; report.Schedules.Add(schedule);
        try
        {
            for (var index = 0; index < planned; index++)
            {
                var scheduled = start + (long)(index * frequency / (double)C7Options.RequestsPerSecond);
                await WaitUntilAsync(scheduled);
                if (Stopwatch.GetTimestamp() > schedule.PlannedEndTicks + C7Options.MaximumDrainSeconds * frequency) throw new TimeoutException("C7 fixed-slot phase exceeded its bounded drain; pending requests are retained in the schedule.");
                var sample = new C7Operation { Index = index, Phase = phase, Action = index % 10 == 0 ? "sourceReplace" : "viewport" };
                sample.Ticks["scheduled"] = scheduled; sample.Ticks["request"] = Stopwatch.GetTimestamp();
                report.TraceSamples.Add(sample); schedule.Attempted++;
                try
                {
                    Stage(sample, "sourceApply", () =>
                    {
                        if (sample.Action == "sourceReplace") Replace(scene);
                        else
                        {
                            var factor = index % 240 < 120 ? .1 : 1d;
                            var span = (scene.XMaximum - scene.XMinimum) * factor;
                            var offset = ((index % 120) / 119d) * (scene.XMaximum - scene.XMinimum - span);
                            scene.Chart.Viewport = new(scene.XMinimum + offset, scene.XMinimum + offset + span, scene.YMinimum, scene.YMaximum);
                        }
                    });
                    sample.Revision = scene.Revision;
                    Stage(sample, "prepare", () => Prepare(window, scene));
                    var primitives = 0; Stage(sample, "record", () => primitives = Record(scene.Chart));
                    Stage(sample, "controlledRaster", () => bitmap.Render(scene.Chart));
                    sample.Hit = Query(scene, index, phase); sample.Ticks["hitStart"] = sample.Hit.Timing.StartTicks; sample.Ticks["hitEnd"] = sample.Hit.Timing.EndTicks;
                    sample.Counts = Counts(scene, primitives);
                    sample.Passed = sample.Hit.Passed && sample.Counts.InputCount == scene.InputCount && sample.Counts.RenderedCount > 0;
                    sample.Ticks["end"] = Stopwatch.GetTimestamp();
                    if (!sample.Passed) throw new InvalidOperationException("C7 trace point/count guard failed.");
                    schedule.Completed++;
                }
                catch (Exception error) { sample.Passed = false; sample.Error = error.ToString(); schedule.Failed++; throw; }
                finally { Emit(new { track = report.Id, sample }); }
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            }
            await WaitUntilAsync(schedule.PlannedEndTicks);
            schedule.Passed = schedule.Completed == planned && schedule.Failed == 0;
        }
        finally { schedule.ActualEndTicks = Stopwatch.GetTimestamp(); }
        report.MemoryEndpoints.Add(new { phase = "after-" + phase, observation = Memory() });
    }

    private static async Task WaitUntilAsync(long scheduled)
    {
        // Task.Delay may complete slightly before its requested fractional
        // duration. Never admit a synthetic input before its scheduled slot.
        while (Stopwatch.GetTimestamp() < scheduled)
        {
            var remaining = (scheduled - Stopwatch.GetTimestamp()) * 1000d / Stopwatch.Frequency;
            if (remaining > 1) await Task.Delay(TimeSpan.FromMilliseconds(remaining));
            else Thread.SpinWait(32);
        }
    }

    private static void Replace(C7Scene scene)
    {
        scene.Revision++;
        for (var i = 0; i < scene.Series.Length; i++) scene.Series[i].ItemsSource = scene.CurrentRows[i];
    }
    private static void ResetView(C7Scene scene) => scene.Chart.Viewport = new(scene.XMinimum, scene.XMaximum, scene.YMinimum, scene.YMaximum);
    private static void Prepare(Window window, C7Scene scene) { window.UpdateLayout(); _ = scene.Chart.PlotArea; _ = scene.Chart.VisibleRange; }
    private static C7Query Query(C7Scene scene, int ordinal, string phase, C7Timing? maintenance = null)
    {
        var chart = scene.Chart; var plot = chart.PlotArea; var range = chart.VisibleRange!.Value;
        var seriesIndex = ordinal % scene.Series.Length; var rows = scene.CurrentRows[seriesIndex];
        var lo = Math.Clamp((int)Math.Ceiling(range.XMinimum), 0, rows.Length - 1); var hi = Math.Clamp((int)Math.Floor(range.XMaximum), lo, rows.Length - 1);
        var index = lo + (int)((ordinal * 104729L + 37) % (hi - lo + 1));
        var probe = rows[index];
        var x = plot.Left + (probe.X - range.XMinimum) / (range.XMaximum - range.XMinimum) * plot.Width;
        var y = plot.Bottom - (probe.Y - range.YMinimum) / (range.YMaximum - range.YMinimum) * plot.Height;
        var start = Stopwatch.GetTimestamp(); var bytes = GC.GetAllocatedBytesForCurrentThread(); var hit = chart.HitTestPoint(new Point(x, y), 1);
        var timing = new C7Timing(start, Stopwatch.GetTimestamp(), GC.GetAllocatedBytesForCurrentThread() - bytes);
        var actualSeries = hit is null ? -1 : Array.IndexOf(scene.Series, hit.Series);
        var valid = hit is not null && actualSeries >= 0 && hit.Index >= 0 && hit.Index < scene.CurrentRows[actualSeries].Length;
        var original = valid ? scene.CurrentRows[actualSeries][hit!.Index] : null;
        var reference = valid && ReferenceEquals(hit!.Item, original);
        var identity = reference && Equals(hit!.StableId, original!.Id) && hit.Point.X == original.X && hit.Point.Y == original.Y;
        // Dense columns can overlap; the original returned row, rather than the
        // arbitrarily probed column, is the declared identity contract there.
        var passed = identity && (scene.Kind == "bar" || actualSeries == seriesIndex && hit!.Index == index);
        return new(ordinal, phase, scene.Revision, seriesIndex, index, x, y, valid ? actualSeries : null, valid ? hit!.Index : null,
            valid && hit!.StableId is int id ? id : null, valid ? DatasetFactory.Bits(hit!.Point.X) : null, valid ? DatasetFactory.Bits(hit!.Point.Y) : null,
            original is null ? null : DatasetFactory.Bits(original.X), original is null ? null : DatasetFactory.Bits(original.Y), reference, passed, timing, maintenance,
            [plot.X, plot.Y, plot.Width, plot.Height], [range.XMinimum, range.XMaximum, range.YMinimum, range.YMaximum]);
    }
    private static C7Counts Counts(C7Scene scene, int primitives)
    {
        var info = scene.Series.Select(series => scene.Chart.GetSamplingInfo(series)).ToArray();
        return new(scene.InputCount, scene.InputCount, info.Sum(item => item.VisibleCount), info.Sum(item => item.RenderedCount), primitives, info);
    }
    private static int Record(CartesianChart chart)
    {
        var group = new DrawingGroup(); using (var context = group.Open()) chart.Render(context);
        return Leaves(group);
    }
    private static int Leaves(Drawing drawing) => drawing is DrawingGroup group ? group.Children.Sum(Leaves) : 1;
    private static void Stage(C7Operation operation, string name, Action action)
    {
        operation.Ticks[name + "Start"] = Stopwatch.GetTimestamp(); var bytes = GC.GetAllocatedBytesForCurrentThread();
        action(); operation.Ticks[name + "End"] = Stopwatch.GetTimestamp(); operation.Allocations[name] = GC.GetAllocatedBytesForCurrentThread() - bytes;
    }
    private static object Memory()
    {
        var start = Stopwatch.GetTimestamp(); using var process = Process.GetCurrentProcess(); process.Refresh();
        var workingSet = process.WorkingSet64; var privateBytes = process.PrivateMemorySize64; var peakWorkingSet = process.PeakWorkingSet64;
        var heap = GC.GetTotalMemory(false); var allocated = GC.GetTotalAllocatedBytes(false); var cpu = process.TotalProcessorTime.Ticks;
        return new { startTicks = start, endTicks = Stopwatch.GetTimestamp(), workingSetBytes = workingSet, privateBytes,
            peakWorkingSetBytes = peakWorkingSet, managedHeapBytes = heap, allocatedBytes = allocated, processCpuTicks = cpu,
            privateMeasurementKind = OperatingSystem.IsWindows() ? "private-commit" : "Process.PrivateMemorySize64-platform-definition", peakPrivateBytes = (long?)null, nativeBytes = (long?)null, gpuBytes = (long?)null };
    }
    private static object[] FontProbes()
    {
        var probes = new List<object>(); var manager = FontManager.Current;
        foreach (var scalar in new[] { 0x30, 0x46 })
        {
            var matched = manager.TryMatchCharacter(scalar, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal,
                new FontFamily("DejaVu Sans, Segoe UI"), CultureInfo.CurrentCulture, out var face);
            string? hash = null; long? bytes = null;
            if (matched && face.GlyphTypeface.PlatformTypeface.TryGetStream(out var stream))
            {
                using (stream) { if (stream.CanSeek) { bytes = stream.Length; stream.Position = 0; } hash = Convert.ToHexString(SHA256.HashData(stream)); }
            }
            probes.Add(new { codepoint = $"U+{scalar:X}", matched, family = matched ? face.GlyphTypeface.FamilyName : null,
                glyphId = matched ? face.GlyphTypeface.CharacterToGlyphMap.GetGlyph(scalar) : (ushort?)null, fontSha256 = hash, fontBytes = bytes });
        }
        return probes.ToArray();
    }
    private static void SaveRaster(C7SceneReport scene, RenderTargetBitmap bitmap, string name, string phase)
    {
        var relative = "png/" + name; var path = Path.Combine(Options.Output, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using (var stream = File.Open(path, FileMode.CreateNew)) bitmap.Save(stream);
        scene.Artifacts.Add(new { path = relative, phase, bytes = new FileInfo(path).Length, sha256 = Hash(path), pixelWidth = bitmap.PixelSize.Width, pixelHeight = bitmap.PixelSize.Height });
    }
    private static object Fingerprint(string path) => new { path, bytes = new FileInfo(path).Length, sha256 = Hash(path) };
    private static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
    private static void Emit(object entry) => _requests!.WriteLine(JsonSerializer.Serialize(entry, new JsonSerializerOptions(Json) { WriteIndented = false }));
    private static void Checkpoint() => WriteNew($"checkpoints/{_checkpoint++:D5}.json", Report);
    private static void WriteNew(string relative, object data)
    {
        var path = Path.Combine(Options.Output, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Open(path, FileMode.CreateNew); JsonSerializer.Serialize(stream, data, Json);
    }
    private sealed record C7Row(int Id, double X, double Y);
    private sealed class C7Scene(CartesianChart chart, ChartSeries[] series, C7Row[][] rows, C7Row[][] alternate, int count, double xmin, double xmax, double ymin, double ymax, string kind)
    {
        internal CartesianChart Chart { get; } = chart; internal ChartSeries[] Series { get; } = series;
        internal C7Row[][] CurrentRows => Revision % 2 == 0 ? rows : alternate;
        internal int Revision { get; set; }
        internal int InputCount { get; } = count;
        internal double XMinimum { get; } = xmin; internal double XMaximum { get; } = xmax;
        internal double YMinimum { get; } = ymin; internal double YMaximum { get; } = ymax;
        internal string Kind { get; } = kind;
    }
}

public sealed class C7PerformanceApplication : Application
{
    public override void Initialize() { RequestedThemeVariant = ThemeVariant.Light; Styles.Add(new FluentTheme()); Styles.Add(new ChartsTheme()); }
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new Window { Width = 1200, Height = 650, CanResize = false, Background = Brushes.White, Title = "C7 controlled chart raster performance" };
            desktop.MainWindow = window;
            window.Opened += (_, _) => Dispatcher.UIThread.Post(async () => await C7PerformanceProgram.RunWindowAsync(window, desktop));
        }
        base.OnFrameworkInitializationCompleted();
    }
}

internal sealed record C7NativeSurface(bool? HasGrContext, string? ContextType, string? Error);
internal sealed class C7NativeSurfaceProbe : Control
{
    internal TaskCompletionSource<C7NativeSurface> Observed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public override void Render(DrawingContext context) => context.Custom(new Probe(this, new Rect(Bounds.Size)));
    private sealed class Probe(C7NativeSurfaceProbe owner, Rect bounds) : ICustomDrawOperation
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
