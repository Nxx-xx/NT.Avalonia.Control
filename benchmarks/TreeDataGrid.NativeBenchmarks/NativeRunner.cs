using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGridNativeBenchmarks;

internal sealed class NativeRunner
{
    private readonly Window _window;
    private readonly NativeReport _report;
    private readonly NativeOptions _options;
    private readonly Grid _root = new();
    private readonly FrameWitness _witness = new() { IsHitTestVisible = false, ZIndex = 1000 };
    private NativeFixture? _fixture;
    private NativeAutomation? _automation;
    private string _fixtureId = "";
    private int _fixtureSequence;
    private Size _clientSize;
    private double _scaling;
    private int _sequence;
    private int _sourceRefreshCycles;
    private double _cyclePeakRowHeight;
    private readonly List<(long Ticks, long Private, long Working, int Handles)> _memory = [];
    private readonly List<ResourceReading> _resourceReadings = [];

    internal NativeRunner(Window window, string[] args)
    {
        _window = window; _options = NativeOptions.Parse(args); _report = new(_options);
        _root.Children.Add(_witness); _window.Content = _root;
    }

    internal async Task<int> RunAsync()
    {
        try
        {
            NativeFixture.Require(OperatingSystem.IsWindows(), "This protocol requires the Windows native desktop.");
#if DEBUG
            throw new InvalidOperationException("Native measurements require Release.");
#endif
            NativeFixture.Require(!Debugger.IsAttached, "Detach the debugger for native measurements.");
            await Dispatcher.UIThread.InvokeAsync(_window.UpdateLayout, DispatcherPriority.Loaded);
            _clientSize = _window.ClientSize; _scaling = _window.RenderScaling;
            AssertWindow();
            _report.Checks["nativeWindowOnScreen"] = true;
            var functionPath = typeof(GridControl).Assembly.Location; var themePath = typeof(NT.AvaloniaControl.TreeDataGrid.Themes.TreeDataGridTheme).Assembly.Location;
            _report.Environment["runtime"] = RuntimeInformation.FrameworkDescription; _report.Environment["os"] = RuntimeInformation.OSDescription;
            _report.Environment["architecture"] = RuntimeInformation.ProcessArchitecture.ToString(); _report.Environment["processors"] = System.Environment.ProcessorCount;
            _report.Environment["clientSize"] = _clientSize; _report.Environment["renderScaling"] = _scaling;
            _report.Environment["position"] = _window.Position; _report.Environment["screens"] = _window.Screens.All.Select(screen => new { screen.Bounds, screen.WorkingArea, screen.Scaling }).ToArray();
            _report.Environment["avalonia"] = typeof(Application).Assembly.GetName().Version?.ToString();
            _report.Environment["functionDll"] = new { path = functionPath, sha256 = NativeReport.Hash(functionPath) };
            _report.Environment["themeDll"] = new { path = themePath, sha256 = NativeReport.Hash(themePath) };
            _report.Environment["hostDll"] = new { path = typeof(NativeRunner).Assembly.Location, sha256 = NativeReport.Hash(typeof(NativeRunner).Assembly.Location) };
            _report.Environment["font"] = Avalonia.Media.FontManager.Current.DefaultFontFamily.Name;
            _report.Environment["containerMetricsVersion"] = 1;
            _report.Environment["automationQueryVersion"] = 1;
            _report.Environment["hostPid"] = Environment.ProcessId;
            _report.Environment["automationClientMode"] = _options.AutomationClient is null ? "none" : "external-windows-uia-v1";
            _report.Environment["automationWorkloadVersion"] = 1;
            _report.Environment["automationWorkload"] = _options.AutomationClient is null ? "none" : _options.AutomationWorkload;
            if (_options.AutomationClient is not null && _options.AutomationWorkload != "query") _report.Environment["automationSubscriptionVersion"] = 1;
            if (_options.ShutdownAutomationProviders) _report.Environment["automationShutdownVersion"] = 1;
            _report.Event("environment", _report.Environment);
            _report.Environment["automationClientBackendVersion"] = 1;
            _report.Environment["automationClientBackend"] = _options.AutomationClient is null ? "none" : _options.AutomationClientBackend;
            if (_options.AutomationClient is { } client) _automation = new NativeAutomation(client, _report, _options.AutomationWorkload, _options.AutomationClientBackend);
            if (_options.Mode == "performance") await RunPerformanceAsync(); else await RunSoakAsync();
            _report.Checks["allNativeRequestsCompleted"] = _report.Requests > 0 && _report.Requests == _report.Completed;
            using var process = Process.GetCurrentProcess();
            _report.Environment["loadedNativeModules"] = process.Modules.Cast<ProcessModule>().Where(module => new[] { "skia", "harfbuzz", "d3d", "dxgi", "libegl", "libgles" }.Any(name => module.ModuleName.Contains(name, StringComparison.OrdinalIgnoreCase)))
                .Select(module => new { name = module.ModuleName, path = module.FileName, version = module.FileVersionInfo.FileVersion }).ToArray();
        }
        catch (Exception error) { _report.Error = error.ToString(); Console.Error.WriteLine(error); _report.Event("failure", new { error = error.ToString() }); }
        finally
        {
            _report.Checks["allNativeRequestsCompleted"] = _report.Requests > 0 && _report.Requests == _report.Completed;
            try { await DetachAsync(); } catch (Exception error) { _report.Error = (_report.Error ?? "") + "\nCleanup: " + error; }
            try { if (_automation is { } cleanupClient) await cleanupClient.DisposeAsync(); } catch (Exception error) { _report.Error = (_report.Error ?? "") + "\nClient cleanup: " + error; }
            _report.Save(); _report.Dispose();
        }
        return _report.Error is null && _report.Checks.Values.All(value => value) ? 0 : 1;
    }

    private void AssertWindow()
    {
        NativeFixture.Require(_window.TryGetPlatformHandle() is { Handle: not 0 } && _window.IsVisible && _window.WindowState == WindowState.Normal, "Native benchmark window unavailable/minimized.");
        NativeFixture.Require(_window.ClientSize == _clientSize && _window.RenderScaling == _scaling && _clientSize.Width >= 600 && _clientSize.Height >= 400, "Viewport/DPI changed during native measurement.");
        var area = new PixelRect(_window.Position, PixelSize.FromSize(_clientSize, _scaling));
        NativeFixture.Require(_window.Screens.All.Any(screen => screen.WorkingArea.Intersects(area)), "Native benchmark window is off-screen.");
    }

    private async Task AttachAsync(NativeFixture fixture)
    {
        NativeFixture.Require(_fixture is null, "Old native fixture is still attached."); _fixture = fixture;
        _fixtureId = "NativeFixture." + ++_fixtureSequence;
        Avalonia.Automation.AutomationProperties.SetAutomationId(fixture.Grid, _fixtureId);
        _root.Children.Insert(0, fixture.Grid); _window.UpdateLayout(); fixture.Snapshot();
        if (_automation is { } client) await client.ProbeAsync(_window, _fixtureId, fixture, _sequence, "attach");
        _report.Event("fixture-automation-state", new { fixtureId = _fixtureId, lastRequestId = _sequence,
            peerPresent = Avalonia.Automation.Peers.ControlAutomationPeer.FromElement(fixture.Grid) is not null });
    }
    private async Task DetachAsync()
    {
        var hadFixture = _fixture is not null;
        Detach();
        if (hadFixture && _automation is { } client) await client.ProbeAsync(_window, _fixtureId, null, _sequence, "retired");
    }
    private void Detach()
    {
        var old = _fixture; _fixture = null;
        if (old is not null) { _root.Children.Remove(old.Grid); old.Dispose(); }
        if (old is not null)
        {
            // Avalonia invalidates a parent's automation children lazily. Consume that
            // public query outside measurements so this long-lived harness panel does
            // not itself keep a retired fixture alive through its old peer list.
            var peer = Avalonia.Automation.Peers.ControlAutomationPeer.FromElement(_root);
            var children = peer?.GetChildren();
            var retained = children?.OfType<Avalonia.Automation.Peers.ControlAutomationPeer>().Any(p => ReferenceEquals(p.Owner, old.Grid)) == true;
            _report.Event("host-automation-detached", new { lastRequestId = _sequence, automationPeerPresent = peer is not null, retiredGridStillChild = retained });
            NativeFixture.Require(!retained, "Native host automation cache still contains its retired fixture.");
        }
        _window.UpdateLayout();
    }

    private sealed record FrameResult(int Id, string Scenario, int Round, int Step, bool Warmup, long StartTicks, long LayoutEndTicks,
        long UiRenderTicks, long BatchRequestedTicks, long ProcessedTicks, long RenderedTicks, double UiMs, double RenderCompletionMs,
        long UiAllocatedBytes, long ProcessAllocatedBytes, SceneSnapshot Snapshot);

    private async Task<FrameResult> MeasureAsync(string scenario, int round, int step, bool warmup, Action<NativeFixture> mutation)
    {
        AssertWindow(); var fixture = _fixture ?? throw new InvalidOperationException("No native scene.");
        var id = ++_sequence; _report.Requests++; _report.Event("request", new { id, scenario, round, step, warmup });
        var allocation = GC.GetAllocatedBytesForCurrentThread(); var processAllocation = GC.GetTotalAllocatedBytes(false); var start = Stopwatch.GetTimestamp();
        mutation(fixture); _window.UpdateLayout(); var layoutEnd = Stopwatch.GetTimestamp(); var uiBytes = GC.GetAllocatedBytesForCurrentThread() - allocation;
        var expectedRevision = fixture.Revision; var expectedVersion = fixture.Source!.Version;
        var applied = new { id, expectedRevision, expectedVersion, start, layoutEnd, projectionRows = fixture.Source.Rows.Count, columns = fixture.Grid.Columns.Count };
        var receipt = await _witness.ObserveAsync(() => fixture.Snapshot());
        NativeFixture.Require(receipt.Snapshot is { } && receipt.Snapshot.Revision == expectedRevision && receipt.Snapshot.SourceVersion == expectedVersion, "Native frame does not correspond to the requested revision/source.");
        NativeFixture.Require(receipt.UiRenderTicks >= layoutEnd && receipt.BatchRequestedTicks >= receipt.UiRenderTicks && receipt.ProcessedTicks >= receipt.BatchRequestedTicks && receipt.RenderedTicks >= receipt.ProcessedTicks, "Native render timestamps out of order.");
        AssertWindow();
        if (scenario == "soak") _cyclePeakRowHeight = Math.Max(_cyclePeakRowHeight, receipt.Snapshot!.MaxRowHeight);
        var result = new FrameResult(id, scenario, round, step, warmup, start, layoutEnd, receipt.UiRenderTicks, receipt.BatchRequestedTicks, receipt.ProcessedTicks, receipt.RenderedTicks,
            Stopwatch.GetElapsedTime(start, layoutEnd).TotalMilliseconds, Stopwatch.GetElapsedTime(start, receipt.RenderedTicks).TotalMilliseconds,
            uiBytes, GC.GetTotalAllocatedBytes(false) - processAllocation, receipt.Snapshot!);
        _report.Completed++; _report.Event("applied", applied); _report.Event("completed", result);
        if (_automation is { } client) await client.ProbeAsync(_window, _fixtureId, fixture, id, "frame");
        return result;
    }

    private async Task RunPerformanceAsync()
    {
        foreach (var scenario in new[] { "observer", "flat-100k", "auto-default", "auto-factory", "wide-tree", "navigation-edit" })
        {
            var all = new List<FrameResult>(); var rounds = new List<object>();
            for (var round = 0; round < _options.Samples; round++)
            {
                Console.WriteLine($"native performance {scenario} round {round + 1}/{_options.Samples}");
                await AttachAsync(new NativeFixture(scenario));
                try
                {
                    for (var step = 0; step < _options.Warmup; step++) await Frame(step, true);
                    var measured = new List<FrameResult>();
                    var first = Stopwatch.GetTimestamp();
                    for (var step = 0; step < _options.Frames; step++) measured.Add(await Frame(step + _options.Warmup, false));
                    var wallSeconds = Stopwatch.GetElapsedTime(first).TotalSeconds;
                    all.AddRange(measured);
                    rounds.Add(new { round, count = measured.Count, uiMs = NativeReport.Distribution(measured.Select(frame => frame.UiMs)), renderCompletionMs = NativeReport.Distribution(measured.Select(frame => frame.RenderCompletionMs)),
                        observationThroughputHz = measured.Count / wallSeconds, uiAllocatedBytes = measured.Sum(frame => frame.UiAllocatedBytes), processAllocatedBytes = measured.Sum(frame => frame.ProcessAllocatedBytes),
                        peakRetainedRows = measured.Max(frame => frame.Snapshot.RetainedRows), peakRealizedCells = measured.Max(frame => frame.Snapshot.RealizedCells) });
                    if (round == _options.Samples - 1) Capture(scenario + ".png");
                }
                finally { await DetachAsync(); }
                await _witness.ObserveAsync(() => null);
                async Task<FrameResult> Frame(int step, bool warmup) => await MeasureAsync(scenario, round, step, warmup, fixture =>
                {
                    if (scenario == "observer") fixture.Revision++;
                    else if (scenario == "navigation-edit") fixture.NavigateAndEdit(step);
                    else fixture.Scroll(step);
                });
            }
            _report.Scenarios.Add(new { scenario, samples = _options.Samples, framesPerSample = _options.Frames, observations = all.Count,
                uiMs = NativeReport.Distribution(all.Select(frame => frame.UiMs)), renderCompletionMs = NativeReport.Distribution(all.Select(frame => frame.RenderCompletionMs)), rounds });
        }
        _report.Checks["sixScenariosAndCorrelatedRenderSamples"] = _report.Scenarios.Count == 6;
    }

    private async Task RunSoakAsync()
    {
        var tracker = new RetiredTracker();
        Console.WriteLine("native soak warmup");
        for (var cycle = 0; cycle < (_options.Pilot ? 1 : 10); cycle++) await CycleAsync(cycle, null, true);
        await _witness.ObserveAsync(() => null); Collect("pre-soak-baseline");
        _report.BaselineMemory = ResourceSnapshot("baseline-after-gc");
        _report.Event("soak-start", new { seconds = _options.DurationSeconds, cycleSeconds = _options.CycleSeconds });
        var run = await ExerciseSoakAsync(tracker);
        _report.Event("soak-end", new { run.ElapsedSeconds, run.Cycles });
        _report.MeasuredSoakSeconds = run.ElapsedSeconds; _report.Cycles = run.Cycles;
        if (_automation is { } client)
        {
            _report.Event("automation-connected-retention", new { boundary = "Before client exit and without a forced host GC; not the final collection boundary.", retired = tracker.Snapshot() });
            _automation = null; await client.DisposeAsync();
        }
        await _witness.ObserveAsync(() => null);
        await Task.Delay(_options.Pilot && _options.AutomationClient is null ? 100 : 12000); // Allows native composition/UIA transport/dispose pools to age outside the measured soak.
        await _witness.ObserveAsync(() => null); Collect("post-soak-drain");
        if (_options.ShutdownAutomationProviders)
        {
            var beforeShutdown = tracker.Snapshot();
            _report.Event("automation-provider-retention-before-shutdown", beforeShutdown);
            // Public Windows process-shutdown API, confined to this benchmark
            // process after all client activity and measured work has ended.
            var inSendMessage = InSendMessageEx(0);
            NativeFixture.Require(inSendMessage == 0, "Provider shutdown cannot run inside SendMessage.");
            var started = Stopwatch.GetTimestamp();
            var hresult = UiaDisconnectAllProviders();
            _report.Event("automation-providers-shutdown", new { hostPid = Environment.ProcessId, started, hresult, inSendMessage });
            Marshal.ThrowExceptionForHR(hresult);
            await _witness.ObserveAsync(() => null);
            Collect("post-automation-provider-shutdown");
            _report.AutomationRetirement = new { beforeProviderShutdown = beforeShutdown, boundary = "Final retention is after explicit Windows UIA provider shutdown; pre-shutdown objects are preserved in raw evidence, not certified zero while the bridge remains connected." };
        }
        _report.Retention = tracker.Snapshot(); _report.Checks["retiredNativeObjectsCollected"] = tracker.Outstanding == 0;
        var postGc = ResourceSnapshot("after-final-gc");
        _report.MemoryTelemetry = new { schemaVersion = 1, snapshots = _resourceReadings.Count,
            unavailableCurrentEstimates = _resourceReadings.Count(r => r.managedBytes is null),
            requiredGcSnapshotKind = _options.BlockingGcReference ? "FullBlocking" : "Any",
            inconsistentLastAnySnapshots = _resourceReadings.Count(r => r.lastAnyGcCrossCheckErrors.Length != 0),
            allCrossChecksValid = _resourceReadings.All(r => r.crossChecksValid),
            boundary = "Optional current estimate may be unavailable, never replaced by last-GC live bytes. Required last-GC/process cross-checks are independently validated from raw resource-snapshot events." };
        _report.Checks["memoryTelemetryCrossChecks"] = _resourceReadings.All(r => r.crossChecksValid);
        var steady = _memory.Skip(Math.Min(2, _memory.Count / 4)).ToArray();
        var first = steady.Take(3).ToArray(); var last = steady.TakeLast(3).ToArray();
        double Median(IEnumerable<long> values) { var ordered = values.Order().ToArray(); return ordered[ordered.Length / 2]; }
        var privateDelta = Median(last.Select(value => value.Private)) - Median(first.Select(value => value.Private));
        var handleDelta = Median(last.Select(value => (long)value.Handles)) - Median(first.Select(value => (long)value.Handles));
        var guardPassed = privateDelta <= 256L * 1024 * 1024 && handleDelta <= 128;
        _report.Drift = new { privateBytesMedianDelta = privateDelta, handlesMedianDelta = handleDelta, privateBytesInvestigationThreshold = 256L * 1024 * 1024, handleInvestigationThreshold = 128,
            investigationRequired = !guardPassed, checkpointCount = _memory.Count, finalAfterGc = postGc, meaning = "First/last three steady checkpoints; investigation guards, not a product budget. Managed/working-set/private/handle traces include harness and native renderer caches." };
        _report.Checks["nativeSoakInvestigationGuards"] = guardPassed;
        _report.Checks["requestedSoakDuration"] = run.ElapsedSeconds >= _options.DurationSeconds && run.Cycles > 0;
        _report.Checks["defaultAndCustomFactoryScrollCycles"] = _options.Pilot || run.Cycles >= 2;
        if (_options.RefreshSourceDuringSoak)
        {
            _report.Checks["sameSourceRefreshEveryCycle"] = _sourceRefreshCycles == run.Cycles + (_options.Pilot ? 1 : 10);
            NativeFixture.Require(_report.Checks["sameSourceRefreshEveryCycle"], "Native soak missed a configured same-source refresh.");
        }
        NativeFixture.Require(guardPassed, "Native soak drift crossed a predeclared investigation guard.");
        NativeFixture.Require(_report.Checks["memoryTelemetryCrossChecks"], "Native memory cross-check failed; preserve raw telemetry for investigation.");
        NativeFixture.Require(tracker.Outstanding == 0, "Retired native objects survived final renderer drain/settle/full GC.");
        Capture("soak-final.png");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private async Task<(double ElapsedSeconds, int Cycles)> ExerciseSoakAsync(RetiredTracker tracker)
    {
        var clock = Stopwatch.StartNew(); var cycle = 0; var nextCheckpoint = 0d;
        while (clock.Elapsed.TotalSeconds < _options.DurationSeconds)
        {
            var cycleStart = clock.Elapsed.TotalSeconds;
            await CycleAsync(cycle, tracker, false); cycle++;
            if (clock.Elapsed.TotalSeconds >= nextCheckpoint)
            {
                var resources = ResourceSnapshot("steady"); var retired = tracker.Snapshot();
                _report.Memory.Add(new { elapsedSeconds = clock.Elapsed.TotalSeconds, cycle, resources, retired });
                Console.WriteLine($"native soak {clock.Elapsed.TotalSeconds:F0}/{_options.DurationSeconds}s; cycles={cycle}; outstanding weak observations={tracker.Outstanding}");
                nextCheckpoint = clock.Elapsed.TotalSeconds + (_options.Pilot ? 2 : 15);
            }
            var delay = Math.Min(_options.DurationSeconds - clock.Elapsed.TotalSeconds, _options.CycleSeconds - (clock.Elapsed.TotalSeconds - cycleStart));
            if (delay > 0) await Task.Delay(TimeSpan.FromSeconds(delay));
        }
        _report.Memory.Add(new { elapsedSeconds = clock.Elapsed.TotalSeconds, cycle, resources = ResourceSnapshot("steady-final"), retired = tracker.Snapshot() });
        return (clock.Elapsed.TotalSeconds, cycle);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private async Task CycleAsync(int cycle, RetiredTracker? tracker, bool warmup)
    {
        Action<string, object> track = tracker is null ? (_, _) => { } : tracker.Track;
        _cyclePeakRowHeight = 0;
        await AttachAsync(new NativeFixture(_options.AutoHeightDuringSoak ? "auto-soak" : "soak", true, track));
        try
        {
            await MeasureAsync("soak", cycle, 0, warmup, fixture =>
            {
                var grid = fixture.Grid; var source = fixture.Source!; grid.SelectionUnit = TreeDataGridSelectionUnit.Row;
                var first = source.Rows[0].Id; grid.State!.Select(first); grid.State.Select(source.Rows[1].Id, true);
                var address = new CellAddress(first, grid.Columns[0].Id); var model = (ProbeModel)source.Rows[0].Model;
                NativeFixture.Require(grid.BeginEdit(address), "Soak draft start failed."); grid.EditSession!.Value = "cancel"; grid.CancelEdit();
                NativeFixture.Require(model.Writes == 0 && grid.BeginEdit(address), "Cancelled soak draft wrote a value.");
                var edit = grid.EditSession!; edit.Value = "cycle " + cycle;
                fixture.TrackRetired(track); fixture.ReplaceFactory(track);
                grid.ShowColumnHeaders = false; fixture.RebuildTemplate();
                _window.RequestedThemeVariant = cycle % 2 == 0 ? ThemeVariant.Dark : ThemeVariant.Light; _window.UpdateLayout();
                NativeFixture.Require(ReferenceEquals(edit, grid.EditSession) && model.Writes == 0, "Soak factory/template/theme switch lost or committed draft.");
            });
            await MeasureAsync("soak", cycle, 1, warmup, fixture =>
            {
                var source = fixture.Source!; var first = source.Rows[0].Id; var second = source.Rows[1].Id; var grid = fixture.Grid;
                NativeFixture.Require(grid.CommitEdit() && fixture.Models[0].Writes == 1 && fixture.Commits == 1, "Soak draft must commit once.");
                NativeFixture.Require(grid.TryMoveRows([second, first], source.Rows[^1].Id, TreeDataGridRowDropPosition.After).IsCommitted, "Soak batch move failed.");
                NativeFixture.Require(grid.State!.SelectedNodes.Count == 2 && grid.CurrentCell!.Value.Node == first, "Soak batch lost selection/current identity.");
                grid.ShowColumnHeaders = true; grid.Columns[^1].IsVisible = false; grid.Columns[^1].IsVisible = true;
                NativeFixture.Require(grid.TryReorderColumn(grid.Columns[5].Id, 3), "Soak column reorder failed."); fixture.Revision++;
            });
            await MeasureAsync("soak", cycle, 2, warmup, fixture => fixture.ReplaceSource(track));
            for (var step = 0; step < 5; step++) await MeasureAsync("soak", cycle, step + 3, warmup, fixture =>
            {
                if (step == 0 && cycle % 2 == 0) fixture.UseDefaultFactory(track);
                if (step == 1 && _options.RefreshSourceDuringSoak)
                {
                    fixture.TrackRetired(track);
                    var source = fixture.Source!; var before = source.Version;
                    source.Refresh();
                    NativeFixture.Require(ReferenceEquals(fixture.Grid.Source, source) && source.Version > before, "Soak refresh replaced its source or did not publish.");
                    _sourceRefreshCycles++;
                    _report.Event("source-refresh", new { requestId = _sequence, cycle, warmup, versionBefore = before, versionAfter = source.Version, defaultFactory = fixture.Factory is null });
                }
                fixture.Scroll(cycle * 5 + step);
            });
            if(warmup && cycle == 0) Capture("soak-warmup.png");
            _fixture!.TrackRetired(track);
            _report.Event("cycle-completed", new { cycle, warmup, nativeFrames = 8, commits = _fixture.Commits, defaultFactoryScroll = cycle % 2 == 0, sameSourceRefresh = _options.RefreshSourceDuringSoak,
                autoRowHeight = _fixture.Grid.AutoRowHeight, maxRowHeight = _cyclePeakRowHeight });
            if (_options.AutoHeightDuringSoak) NativeFixture.Require(_fixture.Grid.AutoRowHeight && _cyclePeakRowHeight > _fixture.Grid.RowHeight, "Automatic-height soak never measured a wrapped tall row.");
        }
        finally { await DetachAsync(); }
        await _witness.ObserveAsync(() => null);
    }

    private ResourceReading ResourceSnapshot(string phase)
    {
        var reading = NativeMemory.Capture(_resourceReadings.Count + 1, phase, _options.BlockingGcReference);
        _resourceReadings.Add(reading);
        if (phase is "steady" or "steady-final") _memory.Add((reading.ticks, reading.privateBytes, reading.workingSetBytes, reading.handles));
        _report.Event("resource-snapshot", reading);
        return reading;
    }
    private void Capture(string name)
    {
        _window.UpdateLayout(); using var bitmap = new RenderTargetBitmap(PixelSize.FromSize(_window.ClientSize, _window.RenderScaling), new Vector(96 * _window.RenderScaling, 96 * _window.RenderScaling));
        bitmap.Render(_window); bitmap.Save(Path.Combine(_options.Output, name));
        _report.Event("offscreen-image-outside-measurement", new { name, boundary = "RenderTargetBitmap of native window visual; not desktop scan-out." });
    }
    private void Collect(string phase) { _report.Event("forced-gc", new { phase }); GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
    [DllImport("UIAutomationCore.dll", ExactSpelling = true)]
    private static extern int UiaDisconnectAllProviders();
    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern uint InSendMessageEx(nint reserved);
}
