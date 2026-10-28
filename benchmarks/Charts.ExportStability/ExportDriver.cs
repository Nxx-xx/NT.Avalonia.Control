using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.ExportStability;

internal sealed class ExportDriver : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly JsonSerializerOptions LineJson = new(JsonSerializerDefaults.Web);
    private readonly Options _options;
    private readonly FileStream _events;
    private readonly CancellationTokenSource _cancel = new();
    private readonly CancellationTokenSource _gcStop = new();
    private readonly List<object> _attempts = [];
    private readonly List<string> _errors = [];
    private readonly DateTimeOffset _started = DateTimeOffset.UtcNow;
    private Task? _gcWorker;
    private long _collections, _collectionsStartingDuringCapture;
    private int _captureActive, _attemptId;
    private bool _disposed;
    private bool _runFinished;
    internal bool Passed { get; private set; }

    internal ExportDriver(Options options)
    {
        _options = options;
        Directory.CreateDirectory(options.Output);
        Directory.CreateDirectory(Path.Combine(options.Output, "images"));
        Directory.CreateDirectory(Path.Combine(options.Output, "attempts"));
        _events = new FileStream(Path.Combine(options.Output, "events.jsonl"), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        WriteNew("protocol.json", new
        {
            schema = "charts-native-exact-export-stability/v1", options, startedUtc = _started,
            fixedAttemptCountPerRound = 17,
            fixture = new
            {
                count = options.Points, x = "i", y = "14*sin(i*.0123)+3*sin(i*.173)",
                peakIndex = options.Points / 3 + 1, peak = 95, valleyIndex = options.Points * 2 / 3 + 1, valley = -95,
                nanFirst = options.Points / 2 - 2, nanLast = options.Points / 2 + 2,
                finiteVertices = options.Points - 5, continuousFigures = 2, exactOverviewLineSegments = options.Points - 7,
                chartDips = new[] { 1120, 390 }, stroke = "#4385F4", strokeDip = 3,
                markers = false, interpolation = "Linear", screenSampling = "MinMax", exactExportUsesSampling = false,
            },
            quality = new
            {
                opaqueColorTolerance = 24, opaqueAlphaMinimum = 250, opaqueMinimumExtremumInk = 4,
                transparentColorTolerance = 24, transparentAlphaMinimum = 16, transparentMinimumExtremumInk = 1,
                maximumGapInk = 0, transparentCornerAlpha = 0,
                independentPngOracleCanUseSavedImagesAndAttemptPixelRegions = true,
            },
            order = new[]
            {
                "three fresh-chart cold exact overview PNG exports: half-transparent,1x-opaque,2x-opaque",
                "one warmed chart: unsampled screen capture; gap exact/sampled2x; overview exact/sampled1x/2x",
                "warmed overview half-transparent exact/sampled; cropped peak/gap half-transparent exact",
                "async exact overview PNG half-transparent; async exact overview JPEG1x; pre-canceled async PNG",
            },
            defaultJitPolicy = "No runtime tiered-compilation override in this project; record actual configuration/environment.",
            scope = "Visible native window plus RenderTargetBitmap/export; synthetic software stress, no physical input or actual compositor-present proof.",
            forcedGcOverlapLimit = "Worker records collections beginning during managed capture interval; exact overlap with the native sk_canvas_draw_path call is not asserted.",
            assemblyManifest = Assemblies(), sourceManifest = Sources(), runtime = EnvironmentInfo(),
        });
        Event(new { phase = "driver-created", utc = DateTimeOffset.UtcNow });
    }

    internal void Cancel() => _cancel.Cancel();
    internal void Fail(Exception error)
    {
        Passed = false; _errors.Add(error.ToString());
        if (!_disposed) Event(new { phase = "failure", utc = DateTimeOffset.UtcNow, error = error.ToString() });
    }

    internal async Task RunAsync(Window window)
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            CheckWindow(window);
            if (_options.ForceGc)
            {
                var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _gcWorker = Task.Run(() => CollectUntilCanceled(ready));
                await ready.Task.WaitAsync(TimeSpan.FromSeconds(10), _cancel.Token);
            }
            for (var round = 0; round < _options.Rounds; round++)
            {
                foreach (var (scale, transparent) in new[] { (.5, true), (1d, false), (2d, false) })
                {
                    using var cold = await AttachAsync(window);
                    await AttemptAsync(window, cold, round, $"cold-overview-{scale:0.0}x-{(transparent ? "transparent" : "opaque")}", "overview", scale, transparent);
                    Detach(window, cold);
                }
                using var warm = await AttachAsync(window);
                // This forces the retained original path before the exact-export
                // sequence; it is separately receipted because it can itself fail.
                await AttemptAsync(window, warm, round, "history-screen-original", "overview", 1, false, screen: true);
                await AttemptAsync(window, warm, round, "history-gap-exact-2x", "gap", 2, false);
                await AttemptAsync(window, warm, round, "history-gap-sampled-2x", "gap", 2, false, sampled: true);
                foreach (var scale in new[] { 1d, 2d })
                    foreach (var sampled in new[] { false, true })
                        await AttemptAsync(window, warm, round, $"history-overview-{(sampled ? "sampled" : "exact")}-{scale:0.0}x", "overview", scale, false, sampled);
                await AttemptAsync(window, warm, round, "warm-overview-exact-half-transparent", "overview", .5, true);
                await AttemptAsync(window, warm, round, "warm-overview-sampled-half-transparent", "overview", .5, true, sampled: true);
                await AttemptAsync(window, warm, round, "cropped-peak-exact-half-transparent", "peak", .5, true);
                await AttemptAsync(window, warm, round, "cropped-gap-exact-half-transparent", "gap", .5, true);
                await AttemptAsync(window, warm, round, "async-overview-exact-half-transparent-png", "overview", .5, true, asynchronous: true);
                await AttemptAsync(window, warm, round, "async-overview-exact-opaque-jpeg", "overview", 1, false, asynchronous: true, jpeg: true);
                await AttemptAsync(window, warm, round, "precanceled-async-png", "overview", 1, false, asynchronous: true, precanceled: true);
                Detach(window, warm);
                Event(new { phase = "round-end", round, utc = DateTimeOffset.UtcNow, attempts = _attempts.Count });
            }
            _runFinished = true;
        }
        catch (Exception error) { Fail(error); }
        finally
        {
            _gcStop.Cancel();
            if (_gcWorker is not null)
                try { await _gcWorker.WaitAsync(TimeSpan.FromSeconds(15)); }
                catch (Exception error) { Fail(error); }
            window.Content = null; window.UpdateLayout();
            Passed = _runFinished && _errors.Count == 0 && _attempts.Count == _options.Rounds * 17;
            WriteNew("results.json", new
            {
                schema = "charts-native-exact-export-stability-results/v1", passed = Passed, startedUtc = _started,
                finishedUtc = DateTimeOffset.UtcNow, options = _options,
                expectedAttempts = _options.Rounds * 17, actualAttempts = _attempts.Count, attempts = _attempts, errors = _errors,
                forcedCollections = Interlocked.Read(ref _collections), collectionsBeginningDuringManagedCapture = Interlocked.Read(ref _collectionsStartingDuringCapture),
                forcedGcWorkerStopped = _gcWorker is null || _gcWorker.IsCompleted,
                assembliesAfter = Assemblies(), nativeModulesAfter = NativeModules(),
                physicalInputValidated = false, compositorPresentValidated = false, performanceMeasured = false,
                fullDemoRegressionRun = false, officialRuntimeCompared = false, eightHourSoakCompleted = false,
                samplingWasNotImplicitlyEnabledForExactExports = true,
            });
            Event(new { phase = "driver-end", passed = Passed, utc = DateTimeOffset.UtcNow });
        }
    }

    private async Task<Fixture> AttachAsync(Window window)
    {
        _cancel.Token.ThrowIfCancellationRequested();
        var fixture = new Fixture(_options.Points);
        window.Content = fixture.Chart; window.UpdateLayout();
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        window.UpdateLayout(); CheckWindow(window);
        if (Math.Abs(fixture.Chart.Bounds.Width - 1120) > .01 || Math.Abs(fixture.Chart.Bounds.Height - 390) > .01 || fixture.Chart.PlotArea.Width < 700)
            throw new InvalidOperationException("The real chart must be arranged at 1120x390 DIPs with a usable plot.");
        return fixture;
    }

    private static void Detach(Window window, Fixture fixture)
    {
        window.Content = null; window.UpdateLayout(); fixture.Series.ItemsSource = null; fixture.Chart.Series.Clear();
    }

    private async Task AttemptAsync(Window window, Fixture fixture, int round, string name, string view, double scale,
        bool transparent, bool sampled = false, bool asynchronous = false, bool jpeg = false, bool precanceled = false, bool screen = false)
    {
        _cancel.Token.ThrowIfCancellationRequested(); CheckWindow(window);
        var chart = fixture.Chart; var series = fixture.Series;
        chart.Background = transparent ? Brushes.Transparent : Brushes.White;
        if (view == "overview") chart.ResetViewport();
        else
        {
            var center = view == "peak" ? _options.Points / 3 + 1 : _options.Points / 2;
            chart.Viewport = new ChartViewport(center - 24, center + 24, -110, 110);
        }
        window.UpdateLayout();
        series.SamplingMode = screen ? ChartSamplingMode.None : ChartSamplingMode.MinMax;
        chart.ClearSelection();
        var selectedIndex = view == "peak" ? _options.Points / 3 - 7 : view == "gap" ? _options.Points / 2 - 8 : Math.Min(23_457, _options.Points / 4);
        var hit = chart.HitTestPoint(Project(chart, selectedIndex, fixture.Rows[selectedIndex].Y), .1);
        if (hit is null || hit.Index != selectedIndex || !ReferenceEquals(hit.Item, fixture.Rows[selectedIndex]))
            throw new InvalidOperationException("Original raw data point hit was lost before export.");
        chart.SelectedPoints = [hit];
        var before = chart.GetSamplingInfo(series); var pixels = new PixelSize((int)(1120 * scale), (int)(390 * scale));
        var id = _attemptId++;
        var stem = $"{id:D5}-r{round:D3}-{name}";
        var imagePath = Path.Combine(_options.Output, "images", stem + (jpeg ? ".jpg" : ".png"));
        var collectionsBefore = Interlocked.Read(ref _collections);
        var overlapBefore = Interlocked.Read(ref _collectionsStartingDuringCapture);
        var started = DateTimeOffset.UtcNow;
        // Flush to stable storage before entering any renderer/native encoder.
        // A fatal native exception can leave only this start record.
        Event(new
        {
            phase = "attempt-start", id, round, name, view, scale, transparent, sampled, asynchronous, jpeg, precanceled, screen,
            utc = started, points = fixture.Rows.Length, image = imagePath, chartBounds = chart.Bounds,
            plot = chart.PlotArea, visibleRange = chart.VisibleRange, screenPlan = before, outputPixels = pixels,
            forcedGcWorkerActive = _gcWorker is { IsCompleted: false }, forcedCollections = collectionsBefore,
        });
        fixture.LastExportError = null;
        var exportFailureEventsBefore = fixture.ExportFailureEvents;
        using var stream = new MemoryStream();
        using var alreadyCanceled = new CancellationTokenSource();
        if (precanceled) alreadyCanceled.Cancel();
        var canceled = false; var success = false;
        var watch = Stopwatch.StartNew();
        try
        {
            Interlocked.Exchange(ref _captureActive, 1);
            var options = new ChartExportOptions { PixelSize = pixels, Background = transparent ? null : Brushes.White, UseSampling = sampled, JpegQuality = 95 };
            if (screen)
            {
                using var bitmap = new RenderTargetBitmap(pixels, new Vector(96, 96));
                bitmap.Render(chart); bitmap.Save(stream); success = true;
            }
            else if (asynchronous)
            {
                var token = precanceled ? alreadyCanceled.Token : _cancel.Token;
                var task = jpeg ? chart.TryExportJpegAsync(stream, options, token) : chart.TryExportPngAsync(stream, options, token);
                // UI capture ran synchronously before the async encoder task
                // was returned. Do not label its subsequent encode as capture.
                Interlocked.Exchange(ref _captureActive, 0);
                success = await task;
            }
            else success = chart.TryExportPng(stream, options);
        }
        catch (OperationCanceledException) when (precanceled && alreadyCanceled.IsCancellationRequested) { canceled = true; }
        finally { Interlocked.Exchange(ref _captureActive, 0); watch.Stop(); }

        var sourceAfter = series.CreateSnapshot();
        var guards = new Dictionary<string, bool>
        {
            ["native-window-visible"] = window.IsVisible && window.TryGetPlatformHandle() is { Handle: not 0 },
            ["original-source-count"] = sourceAfter.Points.Count == fixture.Rows.Length,
            ["original-selection-identity"] = chart.SelectedPoint is { } selected && selected.Index == selectedIndex && ReferenceEquals(selected.Item, fixture.Rows[selectedIndex]) && Equals(selected.StableId, fixture.Rows[selectedIndex].Id),
            ["screen-plan-preserved-by-export"] = chart.GetSamplingInfo(series) == before,
            ["no-unexpected-export-failure-event"] = fixture.ExportFailureEvents == exportFailureEventsBefore,
            ["linear-no-marker-fixture-contract"] = !series.ShowMarkers && series.MarkerSize == 0 && series.Interpolation == ChartLineInterpolation.Linear && series.StackMode == ChartStackMode.None,
            ["source-extrema-and-gap-original-identities"] = new[] { 0, fixture.Rows.Length / 3 + 1, fixture.Rows.Length / 2, fixture.Rows.Length * 2 / 3 + 1, fixture.Rows.Length - 1 }
                .All(index => ReferenceEquals(sourceAfter.Points[index].Item, fixture.Rows[index]) && sourceAfter.Points[index].SourceIndex == index && Equals(sourceAfter.Points[index].StableId, fixture.Rows[index].Id)) &&
                sourceAfter.Points[fixture.Rows.Length / 3 + 1].Point.Y == 95 && sourceAfter.Points[fixture.Rows.Length * 2 / 3 + 1].Point.Y == -95 && sourceAfter.Points[fixture.Rows.Length / 2].IsEmpty,
        };
        object? rasterProof = null;
        if (precanceled)
        {
            guards["pre-cancel-throws-without-output"] = canceled && !success && stream.Length == 0 && stream.CanWrite;
        }
        else
        {
            guards["export-returned-true"] = success && !canceled && stream.Length > 0 && stream.CanWrite;
            var encoded = stream.ToArray();
            using (var image = new FileStream(imagePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) image.Write(encoded);
            guards["format-signature"] = jpeg ? encoded.Length > 3 && encoded[0] == 255 && encoded[1] == 216 && encoded[^2] == 255 && encoded[^1] == 217
                : encoded.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            stream.Position = 0;
            using var bitmap = new Bitmap(stream);
            guards["decoded-pixel-size"] = bitmap.PixelSize == pixels;
            var raster = Raster.Read(bitmap);
            rasterProof = PixelProof(chart, fixture, view, scale, transparent, jpeg, raster, guards);
            rasterProof = new { raster = rasterProof, imageSha256 = Hash(encoded), encodedBytes = encoded.Length };
        }
        var collectionsAfter = Interlocked.Read(ref _collections);
        var overlapAfter = Interlocked.Read(ref _collectionsStartingDuringCapture);
        var attempt = new
        {
            id, round, name, view, scale, transparent, sampled, asynchronous, jpeg, precanceled, screen, startedUtc = started,
            completedUtc = DateTimeOffset.UtcNow, managedOperationElapsedMilliseconds = watch.Elapsed.TotalMilliseconds,
            elapsedIsStressDiagnosticNotPerformanceQualification = true, outputPixels = pixels, image = precanceled ? null : imagePath,
            plot = chart.PlotArea, range = chart.VisibleRange, beforeScreenPlan = before, afterScreenPlan = chart.GetSamplingInfo(series),
            expectedSelectedIndex = selectedIndex, expectedStableId = fixture.Rows[selectedIndex].Id, guards, rasterProof,
            forcedCollectionsBefore = collectionsBefore, forcedCollectionsAfter = collectionsAfter,
            collectionsBeginningDuringManagedCapture = overlapAfter - overlapBefore,
            exactOverlapWithNativeDrawCallAsserted = false, exportError = fixture.LastExportError,
            passed = guards.Values.All(value => value),
        };
        WriteNew(Path.Combine("attempts", stem + ".json"), attempt);
        _attempts.Add(attempt);
        Event(new { phase = "attempt-end", id, round, name, passed = guards.Values.All(value => value), utc = DateTimeOffset.UtcNow });
        if (guards.Any(pair => !pair.Value)) throw new InvalidOperationException($"Attempt {name} failed: {string.Join(", ", guards.Where(pair => !pair.Value).Select(pair => pair.Key))}");
        if (screen) series.SamplingMode = ChartSamplingMode.MinMax;

    }

    private static object PixelProof(CartesianChart chart, Fixture fixture, string view, double scale, bool transparent, bool jpeg,
        Raster raster, Dictionary<string, bool> guards)
    {
        var peak = fixture.Rows.Length / 3 + 1; var valley = fixture.Rows.Length * 2 / 3 + 1;
        Rect Region(int index, double y)
        {
            var point = Project(chart, index, y);
            return new((point.X - 3) * scale, (point.Y - 3) * scale, 6 * scale, 6 * scale);
        }
        var peakRegion = Region(peak, 95); var valleyRegion = Region(valley, -95);
        var gapX = Project(chart, fixture.Rows.Length / 2, 0).X;
        var gap = new Rect((gapX - 5) * scale, (chart.PlotArea.Top + 4) * scale, 10 * scale, (chart.PlotArea.Height - 8) * scale);
        var alphaMinimum = transparent && !jpeg ? 16 : 250;
        // JPEG is a codec success/dimension/identity test. Lossy pixels have
        // their own independently inspectable output; PNG thresholds do not
        // silently become looser JPEG acceptance thresholds.
        var peakInk = raster.CountBlue(peakRegion, alphaMinimum);
        var valleyInk = raster.CountBlue(valleyRegion, alphaMinimum);
        var gapInk = raster.CountBlue(gap, alphaMinimum);
        if (!jpeg)
        {
            if (view is "overview" or "peak") guards["peak-extremum-ink"] = peakInk >= (transparent ? 1 : 4);
            if (view == "overview") guards["valley-extremum-ink"] = valleyInk >= (transparent ? 1 : 4);
            if (view == "gap") guards["gap-no-invented-bridge"] = gapInk == 0;
            if (transparent) guards["transparent-corner-alpha-zero"] = raster.Pixels[3] == 0;
        }
        return new
        {
            width = raster.Width, height = raster.Height, pixelSha256 = Hash(raster.Pixels),
            peakPixelRegion = peakRegion, valleyPixelRegion = valleyRegion, gapPixelRegion = gap,
            peakInk, valleyInk, gapInk, cornerAlpha = raster.Pixels[3], jpegPixelQualityQualified = false,
            predicate = new { channelTolerance = 24, alphaMinimum, unpremultiplyBeforeColorTest = true, color = "#4385F4" },
        };
    }

    private static Point Project(CartesianChart chart, int x, double y)
    {
        var range = chart.VisibleRange ?? throw new InvalidOperationException("No visible chart range.");
        var plot = chart.PlotArea;
        return new(plot.Left + (x - range.XMinimum) / (range.XMaximum - range.XMinimum) * plot.Width,
            plot.Bottom - (y - range.YMinimum) / (range.YMaximum - range.YMinimum) * plot.Height);
    }

    private void CollectUntilCanceled(TaskCompletionSource ready)
    {
        ready.TrySetResult();
        while (!_gcStop.IsCancellationRequested)
        {
            var startedDuringCapture = Volatile.Read(ref _captureActive) != 0;
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: false);
            Interlocked.Increment(ref _collections);
            if (startedDuringCapture) Interlocked.Increment(ref _collectionsStartingDuringCapture);
            GC.WaitForPendingFinalizers();
            if (_gcStop.Token.WaitHandle.WaitOne(1)) break;
        }
    }

    private static void CheckWindow(Window window)
    {
        if (!window.IsVisible || window.TryGetPlatformHandle() is not { Handle: not 0 })
            throw new InvalidOperationException("A real, visible native window is required.");
        if (AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name?.Contains("Headless", StringComparison.OrdinalIgnoreCase) == true))
            throw new InvalidOperationException("Headless is forbidden in this native stress producer.");
    }

    private void Event(object value)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, LineJson) + "\n");
        _events.Write(bytes); _events.Flush(flushToDisk: true);
    }

    private void WriteNew(string name, object value)
    {
        using var file = new FileStream(Path.Combine(_options.Output, name), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        JsonSerializer.Serialize(file, value, Json); file.Flush(flushToDisk: true);
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static object Assemblies() => AppDomain.CurrentDomain.GetAssemblies()
        .Where(assembly => !assembly.IsDynamic && (assembly.GetName().Name?.StartsWith("Avalonia", StringComparison.Ordinal) == true ||
            assembly.GetName().Name?.Contains("Charts", StringComparison.Ordinal) == true || assembly.GetName().Name is "SkiaSharp" or "HarfBuzzSharp"))
        .OrderBy(assembly => assembly.GetName().Name).Select(assembly => new
        {
            name = assembly.GetName().Name, version = assembly.GetName().Version?.ToString(), path = assembly.Location,
            sha256 = File.Exists(assembly.Location) ? Hash(File.ReadAllBytes(assembly.Location)) : null,
        }).ToArray();

    private static object NativeModules()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return process.Modules.Cast<ProcessModule>().Where(module => module.ModuleName.Contains("skia", StringComparison.OrdinalIgnoreCase) || module.ModuleName.Contains("harfbuzz", StringComparison.OrdinalIgnoreCase))
                .Select(module => new { module.ModuleName, module.FileName, sha256 = Hash(File.ReadAllBytes(module.FileName)) }).ToArray();
        }
        catch (Exception error) { return new { error = error.ToString(), nativeModuleInventoryComplete = false }; }
    }

    private static object EnvironmentInfo() => new
    {
        os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(), framework = RuntimeInformation.FrameworkDescription,
        runtime = Environment.Version.ToString(), serverGc = GCSettings.IsServerGC, gcLatencyMode = GCSettings.LatencyMode.ToString(),
        tieredCompilationEnvironment = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),
        comPlusTieredCompilationEnvironment = Environment.GetEnvironmentVariable("COMPlus_TieredCompilation"),
        runtimeConfigPath = Path.Combine(AppContext.BaseDirectory, "Charts.ExportStability.runtimeconfig.json"),
        runtimeConfig = File.Exists(Path.Combine(AppContext.BaseDirectory, "Charts.ExportStability.runtimeconfig.json"))
            ? File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Charts.ExportStability.runtimeconfig.json")) : null,
    };

    private static object Sources()
    {
        DirectoryInfo? root = new(Environment.CurrentDirectory);
        while (root is not null && (!File.Exists(Path.Combine(root.FullName, "Directory.Build.props")) ||
            !Directory.Exists(Path.Combine(root.FullName, "src/NT.AvaloniaControl.Charts")))) root = root.Parent;
        if (root is null) return new { sourceRoot = (string?)null, status = "unavailable; run from the frozen repository root for source receipts" };
        var sourceRoot = root.FullName;
        var paths = new[] { "src/NT.AvaloniaControl.Charts", "src/NT.AvaloniaControl.Charts.Themes", "benchmarks/Charts.ExportStability" }
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(sourceRoot, folder), "*", SearchOption.AllDirectories))
            .Where(path => !Path.GetRelativePath(sourceRoot, path).Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
            .Concat(new[] { "Directory.Build.props", "Directory.Packages.props", "global.json", "NuGet.Config" }.Select(name => Path.Combine(sourceRoot, name))).Order(StringComparer.Ordinal);
        return new
        {
            sourceRoot,
            files = paths.Select(path => new { path = Path.GetRelativePath(sourceRoot, path).Replace('\\', '/'), bytes = new FileInfo(path).Length, sha256 = Hash(File.ReadAllBytes(path)) }).ToArray(),
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _gcStop.Cancel(); _cancel.Cancel(); _events.Dispose();
        if (_gcWorker is null || _gcWorker.IsCompleted) _gcStop.Dispose();
        _cancel.Dispose(); _disposed = true;
    }

    private sealed class Fixture : IDisposable
    {
        internal Row[] Rows { get; }
        internal LineSeries Series { get; }
        internal CartesianChart Chart { get; }
        internal string? LastExportError;
        internal int ExportFailureEvents;

        internal Fixture(int count)
        {
            Rows = Enumerable.Range(0, count).Select(index =>
            {
                var y = Math.Sin(index * .0123) * 14 + Math.Sin(index * .173) * 3;
                if (index == count / 3 + 1) y = 95;
                if (index == count * 2 / 3 + 1) y = -95;
                if (index >= count / 2 - 2 && index <= count / 2 + 2) y = double.NaN;
                return new Row(index, y);
            }).ToArray();
            Series = new LineSeries
            {
                Title = "Exact original signal", ItemsSource = Rows, CategoryPath = nameof(Row.X), ValuePath = nameof(Row.Y), IdPath = nameof(Row.Id),
                Stroke = new SolidColorBrush(Color.Parse("#4385F4")), StrokeThickness = 3, ShowMarkers = false, MarkerSize = 0,
                SamplingMode = ChartSamplingMode.MinMax,
            };
            Chart = new CartesianChart
            {
                Width = 1120, Height = 390, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                ShowLegend = false, IsTooltipEnabled = false, SelectionMode = ChartSelectionMode.Single,
                AnimationTriggers = ChartAnimationTriggers.None, Background = Brushes.White, Foreground = Brushes.Black,
                XAxis = new NumericAxis { Minimum = 0, Maximum = count - 1, Title = "Source index", LabelFormat = "0" },
                YAxis = new NumericAxis { Minimum = -110, Maximum = 110, Title = "Signal", LabelFormat = "0" },
            };
            Chart.Series.Add(Series);
            Chart.ExportFailed += (_, args) => { ExportFailureEvents++; LastExportError = args.Exception.ToString(); };
        }
        public void Dispose() { Series.ItemsSource = null; Chart.Series.Clear(); }
    }

    private sealed record Row(int Index, double Y)
    {
        public double X => Index;
        public string Id => "sample-" + Index;
    }
}
