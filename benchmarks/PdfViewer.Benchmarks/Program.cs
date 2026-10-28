using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using NT.AvaloniaControl.PdfViewer;
using PdfViewer.Tests;
using Reader = NT.AvaloniaControl.PdfViewer.PdfViewer;

namespace PdfViewer.Benchmarks;

/// <summary>
/// Performance harness entry point (PERF19). Parent mode generates the deterministic corpus,
/// runs one worker process per corpus × scenario × iteration (honest process-cache
/// "new-process"), aggregates with nearest-rank quantiles and writes the report. Worker mode
/// executes exactly one iteration against a real session and writes one raw record.
/// All results are 自身性能结果; official A/B stays blocked-reference until a legal
/// reference host exists. Nothing here invents missing measurements.
/// </summary>
internal static class Program
{
    private static string _output = "";
    private static string _runId = "";
    private static int _iterationsOverride;
    private static int _windowWidth = 1440;
    private static int _windowHeight = 900;
    private static string? _corpusFilter;
    private static HashSet<string>? _scenarioFilter;
    private static bool _inProcess;
    private static int _timeoutSeconds = 300;
    private static string _renderingMode = "default";

    private static bool _worker;
    private static string _scenarioId = "";
    private static string _corpusPath = "";
    private static string _corpusId = "";
    private static int _iteration;

    // Worker-run state shared with the Avalonia application object.
    private static PerfRecord _record = null!;
    private static PdfPerformanceSession _session = null!;
    private static ScenarioSpec _scenarioSpec = null!;
    private static byte[] _corpusBytes = null!;
    private static CorpusEntry _corpusEntry = null!;
    private static Exception? _scenarioError;
    private static CancellationToken _scenarioToken;

    public static int Main(string[] args)
    {
        try
        {
            ParseArgs(args);
            Directory.CreateDirectory(_output);
            // Avalonia's desktop lifetime must start on the entry thread (P1 pattern), so the
            // worker entry is synchronous end to end; only the parent orchestrator is async.
            return _worker ? RunWorker() : RunParentAsync().GetAwaiter().GetResult();
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static void ParseArgs(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--worker": _worker = true; break;
                case "--output": _output = Path.GetFullPath(args[++i]); break;
                case "--run-id": _runId = args[++i]; break;
                case "--iterations": _iterationsOverride = int.Parse(args[++i]); break;
                case "--window":
                    var parts = args[++i].Split('x', 'X');
                    _windowWidth = int.Parse(parts[0]);
                    _windowHeight = int.Parse(parts[1]);
                    break;
                case "--filter": _corpusFilter = args[++i]; break;
                case "--scenarios":
                    _scenarioFilter = args[++i].Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
                    break;
                case "--in-process": _inProcess = true; break;
                case "--timeout-seconds": _timeoutSeconds = int.Parse(args[++i]); break;
                case "--rendering-mode":
                    _renderingMode = args[++i] switch
                    {
                        "default" or "software" => args[i],
                        var other => throw new ArgumentException("Unknown --rendering-mode: " + other),
                    };
                    break;
                case "--scenario": _scenarioId = args[++i]; break;
                case "--corpus": _corpusPath = Path.GetFullPath(args[++i]); break;
                case "--corpus-id": _corpusId = args[++i]; break;
                case "--iteration": _iteration = int.Parse(args[++i]); break;
                default: throw new ArgumentException("Unknown argument: " + args[i]);
            }
        }
        if (_output.Length == 0) throw new ArgumentException("--output is required.");
        if (_worker)
        {
            if (_scenarioId.Length == 0 || _corpusPath.Length == 0 || _corpusId.Length == 0)
                throw new ArgumentException("Worker mode requires --scenario, --corpus and --corpus-id.");
            if (_runId.Length == 0) _runId = "worker";
        }
        else if (_runId.Length == 0)
        {
            _runId = "pdf-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        }
    }

    // ── parent ────────────────────────────────────────────────────────────────────────

    private static async Task<int> RunParentAsync()
    {
        var repoRoot = FindRepoRoot();
        var matrix = JsonSerializer.Deserialize<ScenarioMatrix>(
            await File.ReadAllTextAsync(Path.Combine(repoRoot, "benchmarks", "pdfviewer", "scenarios", "scenarios.json")),
            PerfJson.Options) ?? throw new InvalidDataException("Scenario matrix is empty.");
        var corpusManifest = JsonSerializer.Deserialize<CorpusManifest>(
            await File.ReadAllTextAsync(Path.Combine(repoRoot, "benchmarks", "pdfviewer", "corpus", "manifest.json")),
            PerfJson.Options) ?? throw new InvalidDataException("Corpus manifest is empty.");

        var (commit, isDirty) = GitIdentity(repoRoot);
        var productSha = PerfJson.HashFile(typeof(PdfDocument).Assembly.Location);
        var nativeSha = FindNativeLibrarySha();

        var corpusDir = Path.Combine(_output, "corpus");
        var rawDir = Path.Combine(_output, "raw");
        Directory.CreateDirectory(corpusDir);
        Directory.CreateDirectory(rawDir);

        // Deterministic corpora: the fixture writer has no dates or randomness, so regenerating
        // yields the same bytes; the manifest still records the real hashes of what we ran.
        var generatedCorpora = new List<CorpusEntry>();
        foreach (var spec in corpusManifest.Corpora)
        {
            var bytes = GenerateFixture(spec.Fixture);
            var path = Path.Combine(corpusDir, spec.CorpusId + ".pdf");
            await File.WriteAllBytesAsync(path, bytes);
            generatedCorpora.Add(new CorpusEntry
            {
                CorpusId = spec.CorpusId,
                Tier = spec.Tier,
                Fixture = spec.Fixture,
                Pages = spec.Pages,
                Labels = spec.Labels,
                SearchQuery = spec.SearchQuery,
                File = Path.GetFileName(path),
                Sha256 = PerfJson.Sha256Hex(bytes),
                ByteLength = bytes.Length,
            });
        }
        var sourceManifestSha = PerfJson.HashFile(Path.Combine(repoRoot, "benchmarks", "pdfviewer", "corpus", "manifest.json"));
        var generatedManifestPath = Path.Combine(corpusDir, "manifest.json");
        await File.WriteAllTextAsync(generatedManifestPath,
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["schemaVersion"] = "1.0",
                ["generator"] = corpusManifest.Generator,
                ["sourceManifestSha256"] = sourceManifestSha,
                ["corpora"] = generatedCorpora,
            }, PerfJson.Options));

        var runEnv = new RunEnv
        {
            RunId = _runId,
            StartedUtc = DateTimeOffset.UtcNow,
            Commit = commit,
            IsDirty = isDirty,
            ProductAssemblySha256 = productSha,
            NativeLibrarySha256 = nativeSha,
            SourceManifestSha256 = sourceManifestSha,
            WindowDip = [_windowWidth, _windowHeight],
            CommandLine = Environment.CommandLine,
            TimeoutSeconds = _timeoutSeconds,
            RenderingMode = _renderingMode,
        };
        await File.WriteAllTextAsync(Path.Combine(_output, "run-env.json"),
            JsonSerializer.Serialize(runEnv, PerfJson.Options));

        var jobs = new List<(ScenarioSpec Scenario, CorpusEntry Corpus, int Iteration)>();
        foreach (var scenario in matrix.Scenarios)
        {
            if (_scenarioFilter is not null && !_scenarioFilter.Contains(scenario.ScenarioId)) continue;
            var iterations = _iterationsOverride > 0 ? _iterationsOverride : scenario.Iterations;
            foreach (var corpusId in scenario.Corpora)
            {
                if (_corpusFilter is not null && !corpusId.Contains(_corpusFilter, StringComparison.OrdinalIgnoreCase)) continue;
                var corpus = generatedCorpora.Single(entry => entry.CorpusId == corpusId);
                for (var iteration = 1; iteration <= iterations; iteration++)
                    jobs.Add((scenario, corpus, iteration));
            }
        }
        if (jobs.Count == 0) throw new InvalidOperationException("No benchmark jobs selected.");
        Console.WriteLine($"[{_runId}] {jobs.Count} worker jobs (process-cache: {(_inProcess ? "in-process DEBUG" : "new-process")}).");

        foreach (var (scenario, corpus, iteration) in jobs)
        {
            var label = $"{corpus.CorpusId} / {scenario.ScenarioId} #{iteration}";
            Console.WriteLine($"[{_runId}] run {label}");
            if (_inProcess)
            {
                await RunJobInProcessAsync(scenario, corpus, iteration, runEnv);
                continue;
            }
            var recordPath = Path.Combine(rawDir, RecordFileName(corpus.CorpusId, scenario.ScenarioId, iteration));
            var started = Stopwatch.GetTimestamp();
            using var worker = StartWorker(scenario, corpus, iteration);
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            worker.OutputDataReceived += (_, eventArgs) => { if (eventArgs.Data is not null) stdout.AppendLine(eventArgs.Data); };
            worker.ErrorDataReceived += (_, eventArgs) => { if (eventArgs.Data is not null) stderr.AppendLine(eventArgs.Data); };
            worker.BeginOutputReadLine();
            worker.BeginErrorReadLine();
            if (!worker.WaitForExit(_timeoutSeconds * 1000))
            {
                try { worker.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                worker.WaitForExit(10_000);
                await WriteStubRecordAsync(recordPath, scenario, corpus, iteration, runEnv, "timeout",
                    $"worker exceeded {_timeoutSeconds}s and was killed; stdout: {Truncate(stdout.ToString(), 400)} stderr: {Truncate(stderr.ToString(), 800)}");
            }
            else if (!File.Exists(recordPath))
            {
                await WriteStubRecordAsync(recordPath, scenario, corpus, iteration, runEnv, "error",
                    $"worker exited {worker.ExitCode} without a record; stdout: {Truncate(stdout.ToString(), 400)} stderr: {Truncate(stderr.ToString(), 800)}");
            }
            var elapsed = PdfPerformanceSession.MillisecondsBetween(started, Stopwatch.GetTimestamp());
            Console.WriteLine($"[{_runId}] done {label} in {elapsed:F0}ms");
        }

        var records = new List<PerfRecord>();
        foreach (var path in Directory.GetFiles(rawDir, "*.json").Order(StringComparer.Ordinal))
        {
            records.Add(JsonSerializer.Deserialize<PerfRecord>(await File.ReadAllTextAsync(path), PerfJson.Options)
                ?? throw new InvalidDataException("Unreadable record: " + path));
        }
        var aggregates = PerfAggregate.Build(records, _runId);
        await File.WriteAllTextAsync(Path.Combine(_output, "aggregates.json"), JsonSerializer.Serialize(aggregates, PerfJson.Options));
        await File.WriteAllTextAsync(Path.Combine(_output, "report.md"), BuildReport(aggregates, runEnv, records.Count));
        Console.WriteLine($"[{_runId}] aggregates + report written to {_output}");
        return records.Any(record => record.Status is "error" or "timeout") ? 1 : 0;
    }

    private static Process StartWorker(ScenarioSpec scenario, CorpusEntry corpus, int iteration)
    {
        var entry = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate the current executable.");
        var startInfo = new ProcessStartInfo
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.FileName = entry;
        if (Path.GetFileNameWithoutExtension(entry).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            startInfo.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        startInfo.ArgumentList.Add("--worker");
        startInfo.ArgumentList.Add("--output"); startInfo.ArgumentList.Add(_output);
        startInfo.ArgumentList.Add("--run-id"); startInfo.ArgumentList.Add(_runId);
        startInfo.ArgumentList.Add("--scenario"); startInfo.ArgumentList.Add(scenario.ScenarioId);
        startInfo.ArgumentList.Add("--corpus"); startInfo.ArgumentList.Add(Path.Combine(_output, "corpus", corpus.File));
        startInfo.ArgumentList.Add("--corpus-id"); startInfo.ArgumentList.Add(corpus.CorpusId);
        startInfo.ArgumentList.Add("--iteration"); startInfo.ArgumentList.Add(iteration.ToString());
        startInfo.ArgumentList.Add("--window"); startInfo.ArgumentList.Add($"{_windowWidth}x{_windowHeight}");
        startInfo.ArgumentList.Add("--timeout-seconds"); startInfo.ArgumentList.Add(_timeoutSeconds.ToString());
        startInfo.ArgumentList.Add("--rendering-mode"); startInfo.ArgumentList.Add(_renderingMode);
        return Process.Start(startInfo) ?? throw new InvalidOperationException("Worker failed to start.");
    }

    private static Task RunJobInProcessAsync(ScenarioSpec scenario, CorpusEntry corpus, int iteration, RunEnv runEnv)
    {
        // Debug path only: one process runs every job, so records say process-cache "in-process".
        // UI jobs still need the entry thread for the Avalonia lifetime — spawn mode is the
        // supported way to run them.
        if (scenario.Ui)
            throw new InvalidOperationException("--in-process supports backend scenarios only; UI scenarios must run in their own process.");
        RunScenario(scenario, corpus, iteration, runEnv, inProcess: true);
        return Task.CompletedTask;
    }

    private static async Task WriteStubRecordAsync(string recordPath, ScenarioSpec scenario, CorpusEntry corpus,
        int iteration, RunEnv runEnv, string status, string detail)
    {
        var stub = NewRecord(scenario, corpus, iteration, runEnv, inProcess: _inProcess);
        stub.Status = status;
        stub.StatusDetail = detail;
        await File.WriteAllTextAsync(recordPath, JsonSerializer.Serialize(stub, PerfJson.Options));
    }

    private static string RecordFileName(string corpusId, string scenarioId, int iteration) =>
        $"{corpusId}__{scenarioId}__{iteration}.json";

    // ── worker ────────────────────────────────────────────────────────────────────────

    private static int RunWorker()
    {
        // Synchronous prep keeps the whole worker — especially the Avalonia lifetime start —
        // on the entry thread; the files involved are tiny.
        var runEnv = JsonSerializer.Deserialize<RunEnv>(
            File.ReadAllText(Path.Combine(_output, "run-env.json")), PerfJson.Options)!;
        var manifest = JsonSerializer.Deserialize<GeneratedManifest>(
            File.ReadAllText(Path.Combine(_output, "corpus", "manifest.json")), PerfJson.Options)!;
        var entry = manifest.Corpora.Single(item => item.CorpusId == _corpusId);
        var matrix = JsonSerializer.Deserialize<ScenarioMatrix>(
            File.ReadAllText(Path.Combine(FindRepoRoot(), "benchmarks", "pdfviewer", "scenarios", "scenarios.json")),
            PerfJson.Options)!;
        var scenario = matrix.Scenarios.Single(item => item.ScenarioId == _scenarioId);
        Console.Error.WriteLine($"[worker] prepared {scenario.ScenarioId} on {entry.CorpusId} #{_iteration}");
        RunScenario(scenario, entry, _iteration, runEnv, inProcess: false);
        return _record.Status is "ok" or "completed-before-cancel" ? 0 : 1;
    }

    private static void RunScenario(ScenarioSpec scenario, CorpusEntry entry, int iteration,
        RunEnv runEnv, bool inProcess)
    {
        _scenarioSpec = scenario;
        _corpusEntry = entry;
        _corpusBytes = File.ReadAllBytes(Path.Combine(_output, "corpus", entry.File));
        _record = NewRecord(scenario, entry, iteration, runEnv, inProcess);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(_timeoutSeconds));
        _scenarioToken = timeout.Token;
        _session = PdfPerformanceSession.Begin($"{scenario.ScenarioId}/{entry.CorpusId}/{iteration}");
        try
        {
            if (scenario.Ui)
            {
                _scenarioError = null;
                Console.Error.WriteLine("[worker] starting Avalonia desktop lifetime");
                var builder = AppBuilder.Configure<BenchApplication>().UsePlatformDetect();
                if (_renderingMode == "software")
                {
                    if (OperatingSystem.IsWindows())
                        builder = builder.With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Software] });
                    else if (OperatingSystem.IsLinux())
                        builder = builder.With(new X11PlatformOptions { RenderingMode = [X11RenderingMode.Software] });
                    else
                        throw new PlatformNotSupportedException("--rendering-mode software is wired for Windows (Win32) and Linux (X11) only.");
                }
                builder.StartWithClassicDesktopLifetime([], ShutdownMode.OnExplicitShutdown);
                Console.Error.WriteLine("[worker] Avalonia desktop lifetime returned");
                if (_scenarioError is not null) throw _scenarioError;
            }
            else
            {
                var host = new ScenarioHost
                {
                    Record = _record,
                    Session = _session,
                    CorpusBytes = _corpusBytes,
                    Corpus = new CorpusSpec
                    {
                        CorpusId = entry.CorpusId,
                        Tier = entry.Tier,
                        Fixture = entry.Fixture,
                        Pages = entry.Pages,
                        Labels = entry.Labels,
                        SearchQuery = entry.SearchQuery,
                    },
                    Token = _scenarioToken,
                };
                Scenarios.RunAsync(scenario, host).GetAwaiter().GetResult();
            }
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            _record.Status = "timeout";
            _record.StatusDetail = $"scenario exceeded {_timeoutSeconds}s";
        }
        catch (Exception error)
        {
            _record.Status = "error";
            _record.StatusDetail = error.ToString();
        }
        finally
        {
            _session.Dispose();
        }
        File.WriteAllText(Path.Combine(_output, "raw", RecordFileName(entry.CorpusId, scenario.ScenarioId, iteration)),
            JsonSerializer.Serialize(_record, PerfJson.Options));
        Console.Error.WriteLine($"[worker] wrote record status={_record.Status}");
    }

    private static PerfRecord NewRecord(ScenarioSpec scenario, CorpusEntry entry, int iteration, RunEnv runEnv, bool inProcess)
    {
        var record = new PerfRecord
        {
            RunId = runEnv.RunId,
            Iteration = iteration,
            Commit = runEnv.Commit,
            IsDirty = runEnv.IsDirty,
            SourceManifestSha256 = runEnv.SourceManifestSha256,
            ProductAssemblySha256 = runEnv.ProductAssemblySha256,
            NativeLibrarySha256 = runEnv.NativeLibrarySha256,
            CorpusId = entry.CorpusId,
            CorpusSha256 = entry.Sha256,
            ScenarioId = scenario.ScenarioId,
        };
        record.Conditions.ProcessCache = inProcess ? "in-process" : "new-process";
        record.Conditions.ViewportDip = [(double)runEnv.WindowDip[0], runEnv.WindowDip[1]];
        if (scenario.Warmup is not null) record.Conditions.Warmup = scenario.Warmup;
        var environment = new PerfEnvironment
        {
            Os = RuntimeInformation.OSDescription,
            DesktopSession = OperatingSystem.IsWindows()
                ? "win32"
                : Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") ?? Environment.GetEnvironmentVariable("DISPLAY"),
            GraphicsBackend = scenario.Ui
                ? (runEnv.RenderingMode == "software"
                    ? "avalonia-compositor (software)"
                    : "avalonia-compositor (platform default)")
                : null,
            CpuCount = Environment.ProcessorCount,
            MemoryBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
            Wsl = OperatingSystem.IsLinux() && File.Exists("/proc/sys/kernel/osrelease")
                && File.ReadAllText("/proc/sys/kernel/osrelease").Contains("microsoft", StringComparison.OrdinalIgnoreCase),
            RuntimeVersions = new Dictionary<string, string?>
            {
                ["framework"] = RuntimeInformation.FrameworkDescription,
                ["runtimeIdentifier"] = RuntimeInformation.RuntimeIdentifier,
                ["avalonia"] = typeof(Application).Assembly.GetName().Version?.ToString(),
                ["pdfViewer"] = typeof(PdfDocument).Assembly.GetName().Version?.ToString(),
            },
        };
        record.Environment = environment;
        record.Evidence.Add(new PerfEvidence { Kind = "corpus", Path = entry.File, Sha256 = entry.Sha256, Detail = entry.Fixture });
        record.Evidence.Add(new PerfEvidence { Kind = "product-assembly", Sha256 = runEnv.ProductAssemblySha256 });
        record.Evidence.Add(new PerfEvidence { Kind = "native-library", Sha256 = runEnv.NativeLibrarySha256 });
        return record;
    }

    /// <summary>UI worker half: real window, real compositor; the scenario body runs on the UI thread.</summary>
    private sealed class BenchApplication : Application
    {
        private Reader? _viewer;
        private Window? _window;
        private bool _finished;

        public override void Initialize()
        {
            Styles.Add(new FluentTheme());
            Styles.Add(new StyleInclude(new Uri("avares://PdfViewer.Benchmarks/"))
            {
                Source = new Uri("avares://NT.AvaloniaControl.PdfViewer.Themes/Themes/Generic.axaml"),
            });
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
            {
                _viewer = new Reader { IsSidebarVisible = false };
                _window = new Window
                {
                    Title = "PdfViewer benchmark — " + _scenarioSpec.ScenarioId,
                    Width = _windowWidth,
                    Height = _windowHeight,
                    Content = _viewer,
                };
                lifetime.MainWindow = _window;
                var watchdog = new DispatcherTimer { Interval = TimeSpan.FromSeconds(_timeoutSeconds) };
                watchdog.Tick += (_, _) => Finish(new TimeoutException($"UI scenario exceeded {_timeoutSeconds}s."));
                _window.Opened += async (_, _) =>
                {
                    Console.Error.WriteLine("[worker] window opened; running scenario");
                    watchdog.Start();
                    try
                    {
                        var host = new ScenarioHost
                        {
                            Record = _record,
                            Session = _session,
                            CorpusBytes = _corpusBytes,
                            Corpus = new CorpusSpec
                            {
                                CorpusId = _corpusEntry.CorpusId,
                                Tier = _corpusEntry.Tier,
                                Fixture = _corpusEntry.Fixture,
                                Pages = _corpusEntry.Pages,
                                Labels = _corpusEntry.Labels,
                                SearchQuery = _corpusEntry.SearchQuery,
                            },
                            Token = _scenarioToken,
                            Window = _window,
                            Viewer = _viewer,
                        };
                        await Scenarios.RunAsync(_scenarioSpec, host);
                        Finish(null);
                    }
                    catch (Exception error) { Finish(error); }
                };
                void Finish(Exception? error)
                {
                    if (_finished) return;
                    _finished = true;
                    Console.Error.WriteLine($"[worker] scenario finished: {(error is null ? "ok" : error.Message)}");
                    watchdog.Stop();
                    _viewer?.Dispose();
                    _scenarioError = error;
                    _window?.Close();
                    lifetime.Shutdown(error is null ? 0 : 1);
                }
            }
            base.OnFrameworkInitializationCompleted();
        }
    }

    // ── shared plumbing ───────────────────────────────────────────────────────────────

    private static byte[] GenerateFixture(string fixture) => fixture switch
    {
        "ReadingDocument" => PdfFixture.ReadingDocument(),
        "ManyPages:20" => PdfFixture.ManyPages(20),
        "ManyPages:1000" => PdfFixture.ManyPages(1000),
        _ => throw new ArgumentException("Unknown fixture: " + fixture),
    };

    private static string FindRepoRoot()
    {
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "NT.AvaloniaControl.slnx")))
                    return directory.FullName;
                directory = directory.Parent;
            }
        }
        throw new DirectoryNotFoundException("Cannot locate the repository root (NT.AvaloniaControl.slnx).");
    }

    private static (string? Commit, bool IsDirty) GitIdentity(string repoRoot)
    {
        try
        {
            var commit = RunGit(repoRoot, "rev-parse HEAD");
            var status = RunGit(repoRoot, "status --porcelain");
            return (commit, status.Length > 0);
        }
        catch (Exception)
        {
            return (null, true);
        }
    }

    private static string RunGit(string repoRoot, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        }) ?? throw new InvalidOperationException("git failed to start.");
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(30_000);
        return output.Trim();
    }

    private static string? FindNativeLibrarySha()
    {
        // Hash the library for the CURRENT runtime only: NuGet leaves every RID's binaries
        // under runtimes/, and a blind AllDirectories search returns a foreign platform's
        // file first (it recorded win-arm64's pdfium.dll on both OSes once).
        var name = OperatingSystem.IsWindows() ? "pdfium.dll" : "libpdfium.so";
        var rid = RuntimeInformation.RuntimeIdentifier;
        foreach (var dir in new[]
                 {
                     Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native"),
                     AppContext.BaseDirectory,
                 })
        {
            var hit = Path.Combine(dir, name);
            if (File.Exists(hit)) return PerfJson.HashFile(hit);
        }
        foreach (var hit in Directory.GetFiles(AppContext.BaseDirectory, name, SearchOption.AllDirectories))
        {
            if (hit.Contains(rid, StringComparison.OrdinalIgnoreCase)) return PerfJson.HashFile(hit);
        }
        return null;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";

    // ── report ────────────────────────────────────────────────────────────────────────

    private static string BuildReport(Dictionary<string, object?> aggregates, RunEnv runEnv, int recordCount)
    {
        var groups = (List<Dictionary<string, object?>>)aggregates["groups"]!;
        var builder = new StringBuilder();
        builder.AppendLine($"# PdfViewer 性能运行报告 `{runEnv.RunId}`");
        builder.AppendLine();
        builder.AppendLine("## 诚实性声明");
        builder.AppendLine();
        builder.AppendLine("- 本报告为 **自身性能结果**。同机官方 A/B 对照需要合法参考宿主，当前不可得，全部对照项 `blocked-reference`。");
        builder.AppendLine("- present 方法为 `composition-batch-rendered`（CompositionBatch.Rendered 通知），`scanOutVerified=false`，不是扫描输出/物理呈现。");
        builder.AppendLine("- 分位数为 nearest-rank（不插值）；样本池 < 20 时 p99 不报告。");
        builder.AppendLine("- 门槛判定来源 `pdfviewer-performance-plan.md §6 工程初始门槛（待校准）`，仅供信息参考，不构成承诺。");
        builder.AppendLine();
        builder.AppendLine("## 运行环境");
        builder.AppendLine();
        builder.AppendLine($"- runId: `{runEnv.RunId}`，开始时间 {runEnv.StartedUtc:u}");
        builder.AppendLine($"- commit: `{runEnv.Commit}`（{(runEnv.IsDirty ? "工作区有未提交修改" : "clean")}）");
        builder.AppendLine($"- productAssemblySha256: `{runEnv.ProductAssemblySha256}`");
        builder.AppendLine($"- nativeLibrarySha256: `{runEnv.NativeLibrarySha256}`");
        builder.AppendLine($"- 视口: {runEnv.WindowDip[0]}×{runEnv.WindowDip[1]} DIPs，process-cache: new-process，rendering-mode: {runEnv.RenderingMode}，raw 记录 {recordCount} 条");
        builder.AppendLine();
        builder.AppendLine("## 分组结果");
        foreach (var group in groups)
        {
            builder.AppendLine();
            builder.AppendLine($"### {group["corpusId"]} / {group["scenarioId"]}");
            builder.AppendLine();
            builder.AppendLine($"- iterations: {group["iterations"]}（ok {group["okCount"]} / error {group["errorCount"]} / timeout {group["timeoutCount"]} / canceled {group["canceledCount"]}）");
            var metrics = (Dictionary<string, object?>)group["metrics"]!;
            if (metrics.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("| metric | count | min | p50 | p95 | p99 | max |");
                builder.AppendLine("|---|---:|---:|---:|---:|---:|---:|");
                foreach (var (name, stat) in metrics)
                {
                    var values = (Dictionary<string, object?>)stat!;
                    builder.AppendLine($"| {name} | {F(values["count"])} | {F(values["min"])} | {F(values["p50"])} | {F(values["p95"])} | {F(values["p99"])} | {F(values["max"])} |");
                }
            }
            var samples = (Dictionary<string, object?>)group["samples"]!;
            if (samples.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("pooled samples:");
                foreach (var (name, stat) in samples)
                {
                    var values = (Dictionary<string, object?>)stat!;
                    builder.AppendLine($"- {name}: n={F(values["count"])} p50={F(values["p50"])} p95={F(values["p95"])} p99={F(values["p99"])}");
                }
            }
            var thresholds = (List<Dictionary<string, object?>>)group["thresholds"]!;
            if (thresholds.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("| metric | quantile | target | value | verdict |");
                builder.AppendLine("|---|---|---|---:|---|");
                foreach (var check in thresholds)
                {
                    var shown = check.GetValueOrDefault("value") ?? check.GetValueOrDefault("p95");
                    builder.AppendLine($"| {check.GetValueOrDefault("metric")} | {check.GetValueOrDefault("quantile")} | {check.GetValueOrDefault("target")} | {F(shown)} | {check.GetValueOrDefault("verdict")} |");
                }
            }
        }
        builder.AppendLine();
        builder.AppendLine("## 未做之事");
        builder.AppendLine();
        builder.AppendLine("- 未与官方控件做同机 A/B（无合法参考宿主）；`compare-baseline.ps1` 仅比较自身历史基线。");
        builder.AppendLine("- 未做 SSIM 金样比对（`qualityStatus=not-run-no-golden`）。");
        builder.AppendLine("- 未验证扫描输出；present 仅到 composition batch rendered。");
        return builder.ToString();

        static string F(object? value) => value switch
        {
            null => "—",
            double number => number.ToString("0.###"),
            _ => value.ToString() ?? "—",
        };
    }

    private sealed class CorpusManifest
    {
        public string SchemaVersion { get; set; } = "1.0";
        public string Generator { get; set; } = "";
        public List<CorpusSpec> Corpora { get; set; } = [];
    }

    private sealed class GeneratedManifest
    {
        public string SchemaVersion { get; set; } = "1.0";
        public string Generator { get; set; } = "";
        public string? SourceManifestSha256 { get; set; }
        public List<CorpusEntry> Corpora { get; set; } = [];
    }

    private sealed class ScenarioMatrix
    {
        public string SchemaVersion { get; set; } = "1.0";
        public string Notes { get; set; } = "";
        public List<ScenarioSpec> Scenarios { get; set; } = [];
    }

    private sealed class CorpusEntry
    {
        public string CorpusId { get; set; } = "";
        public string Tier { get; set; } = "";
        public string Fixture { get; set; } = "";
        public int Pages { get; set; }
        public string[] Labels { get; set; } = [];
        public string? SearchQuery { get; set; }
        public string File { get; set; } = "";
        public string? Sha256 { get; set; }
        public long ByteLength { get; set; }
    }

    private sealed class RunEnv
    {
        public string RunId { get; set; } = "";
        public DateTimeOffset StartedUtc { get; set; }
        public string? Commit { get; set; }
        public bool IsDirty { get; set; }
        public string? ProductAssemblySha256 { get; set; }
        public string? NativeLibrarySha256 { get; set; }
        public string? SourceManifestSha256 { get; set; }
        public int[] WindowDip { get; set; } = [1440, 900];
        public string CommandLine { get; set; } = "";
        public int TimeoutSeconds { get; set; } = 300;
        public string RenderingMode { get; set; } = "default";
    }
}
