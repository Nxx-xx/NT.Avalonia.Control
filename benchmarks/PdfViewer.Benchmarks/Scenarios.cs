using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using NT.AvaloniaControl.PdfViewer;
using PdfViewer.Tests;
using Reader = NT.AvaloniaControl.PdfViewer.PdfViewer;

namespace PdfViewer.Benchmarks;

internal sealed class ScenarioHost
{
    public required PerfRecord Record { get; init; }
    public required PdfPerformanceSession Session { get; init; }
    public required byte[] CorpusBytes { get; init; }
    public required CorpusSpec Corpus { get; init; }
    public required CancellationToken Token { get; init; }
    public Window? Window { get; init; }
    public Reader? Viewer { get; init; }
}

/// <summary>
/// Scenario implementations for the performance-plan matrix. Each body measures exactly what
/// its id promises on the monotonic clock, marks harness-side events through the session and
/// leaves unavailable measurements null with a reason instead of inventing values.
/// </summary>
internal static class Scenarios
{
    internal static async Task RunAsync(ScenarioSpec scenario, ScenarioHost host)
    {
        switch (scenario.ScenarioId)
        {
            case "open-first-screen": await OpenFirstScreenAsync(host); break;
            case "jump-page-cached": await JumpPageAsync(host, uncached: false); break;
            case "jump-page-uncached": await JumpPageAsync(host, uncached: true); break;
            case "scroll-continuous": await ScrollContinuousAsync(host); break;
            case "search-first-hit": await SearchAsync(host, fullDocument: false); break;
            case "search-full-document": await SearchAsync(host, fullDocument: true); break;
            case "cancel-queued": await CancelQueuedAsync(host); break;
            case "cancel-native-render": await CancelNativeRenderAsync(host); break;
            case "close-release": await CloseReleaseAsync(host); break;
            case "open-close-cycles": await OpenCloseCyclesAsync(host); break;
            default: throw new ArgumentException("Unknown scenario: " + scenario.ScenarioId);
        }
    }

    private static async Task OpenFirstScreenAsync(ScenarioHost host)
    {
        var viewer = Require(host.Viewer);
        var session = host.Session;
        host.Record.Conditions.Warmup = "none";
        session.SampleResources();
        await viewer.LoadDocumentAsync(host.CorpusBytes, cancellationToken: host.Token);
        await WaitMarksAsync(host, PdfPerformanceSession.FirstPageDecodedMark, PdfPerformanceSession.FirstPageSubmittedMark);
        var marks = session.Marks;
        var loadStart = marks.First(mark => mark.Name == PdfPerformanceSession.LoadStartMark).Timestamp;
        var loadReturn = marks.First(mark => mark.Name == PdfPerformanceSession.LoadReturnMark).Timestamp;
        var decoded = marks.First(mark => mark.Name == PdfPerformanceSession.FirstPageDecodedMark);
        var submitted = marks.First(mark => mark.Name == PdfPerformanceSession.FirstPageSubmittedMark);

        // PERF17: only a present harness records the compositor render, and only when a real
        // compositor answers. Otherwise the metric stays null with an explicit reason.
        var (latency, method, reason) = await PresentProbe.CaptureAsync(host.Window, host.Token);
        long? presentedAt = null;
        if (latency is not null)
        {
            presentedAt = PdfPerformanceSession.Now;
            session.Mark(PdfPerformanceSession.FirstPagePresentedMark, decoded.CorrelationId);
        }
        await viewer.WaitForIdleAsync(host.Token);
        viewer.CaptureResourceSample();

        var record = host.Record;
        record.Metrics["inputReadMs"] = StageSum(session, PdfPerformanceStage.InputRead, to: submitted.Timestamp);
        record.Metrics["openParseMs"] = StageSum(session, PdfPerformanceStage.OpenParse, to: submitted.Timestamp);
        record.Metrics["queueWaitMs"] = StageSum(session, PdfPerformanceStage.QueueWait, to: submitted.Timestamp);
        record.Metrics["queueWaitMaxMs"] = StageMax(session, PdfPerformanceStage.QueueWait, to: submitted.Timestamp);
        record.Metrics["nativeExecMs"] = StageSum(session, PdfPerformanceStage.NativeExec, to: submitted.Timestamp);
        record.Metrics["bitmapUploadMs"] = StageSum(session, PdfPerformanceStage.BitmapUpload, to: submitted.Timestamp);
        record.Metrics["uiSubmitMs"] = StageSum(session, PdfPerformanceStage.UiSubmit, to: submitted.Timestamp);
        record.Metrics["decodeCompleteMs"] = session.Spans
            .Where(span => span.Stage == PdfPerformanceStage.DecodeComplete && span.CorrelationId == decoded.CorrelationId)
            .Select(span => (double?)PdfPerformanceSession.MillisecondsBetween(span.StartTimestamp, span.EndTimestamp))
            .FirstOrDefault();
        record.Metrics["firstPageDecodedMs"] = PdfPerformanceSession.MillisecondsBetween(loadStart, decoded.Timestamp);
        record.Metrics["firstPageSubmittedMs"] = PdfPerformanceSession.MillisecondsBetween(loadStart, submitted.Timestamp);
        var presentedMs = presentedAt is { } present ? PdfPerformanceSession.MillisecondsBetween(loadStart, present) : (double?)null;
        record.Metrics["firstPagePresentedMs"] = presentedMs;
        // First screen is decode → submit → present; without a compositor the submitted
        // visual update is the furthest honest milestone and is named as such.
        record.Metrics["firstScreenMs"] = presentedMs
            ?? PdfPerformanceSession.MillisecondsBetween(loadStart, submitted.Timestamp);
        record.Metrics["loadReturnMs"] = PdfPerformanceSession.MillisecondsBetween(loadStart, loadReturn);
        record.Measurement.PresentMethod = method;
        record.Measurement.PresentUnavailableReason = reason;
        record.Measurement.ScanOutVerified = PresentProbe.ScanOutVerified;
        if (reason is not null)
            record.Notes["presentUnavailableReason"] = reason;
        record.Notes["firstScreenBasis"] = presentedMs is not null
            ? "firstPagePresentedMs (composition-batch-rendered, not scan-out)"
            : "firstPageSubmittedMs (UI submit; compositor present unavailable)";
    }

    private static async Task JumpPageAsync(ScenarioHost host, bool uncached)
    {
        var viewer = Require(host.Viewer);
        host.Record.Conditions.Warmup = uncached ? "open only; target page never rendered" : "target page rendered once before the measured jump";
        await viewer.LoadDocumentAsync(host.CorpusBytes, cancellationToken: host.Token);
        await viewer.WaitForIdleAsync(host.Token);
        var target = uncached ? Math.Max(2, viewer.PageCount - 2) : Math.Min(6, viewer.PageCount);
        if (!uncached)
        {
            // Warm the target into the page cache, then leave it before the measured jump.
            await NavigateAsync(viewer, target, host.Token);
            await NavigateAsync(viewer, 1, host.Token);
        }
        var started = PdfPerformanceSession.Now;
        await NavigateAsync(viewer, target, host.Token);
        host.Record.Metrics["jumpMs"] = PdfPerformanceSession.MillisecondsBetween(started, PdfPerformanceSession.Now);
        host.Record.Notes["targetPage"] = target.ToString();
    }

    private static async Task ScrollContinuousAsync(ScenarioHost host)
    {
        var viewer = Require(host.Viewer);
        host.Record.Conditions.Warmup = "open + idle before the scripted scroll";
        await viewer.LoadDocumentAsync(host.CorpusBytes, cancellationToken: host.Token);
        await viewer.WaitForIdleAsync(host.Token);
        // The scroll host is the presenter's own ScrollViewer — template also contains other
        // scroll parts (e.g. the collapsed sidebar), which would report a 19px viewport.
        var presenter = viewer.GetVisualDescendants().OfType<PdfDocumentPresenter>().Single();
        var scroll = presenter.GetVisualAncestors().OfType<ScrollViewer>()
            .First(candidate => candidate.Name == "PART_ScrollViewer");
        const int burstSteps = 40;
        var offsetStart = scroll.Offset.Y;
        // Phase 1 is a real continuous burst: offsets change on a short interval with no frame
        // waits, so the interaction window stays open and stop-scroll settles the upgrade pass.
        for (var step = 0; step < burstSteps; step++)
        {
            scroll.Offset = new Vector(scroll.Offset.X, scroll.Offset.Y + 120);
            await Task.Delay(8, host.Token);
        }
        host.Record.Notes["burstMovement"] =
            $"offset {offsetStart:F0} → {scroll.Offset.Y:F0} px (requested {burstSteps * 120}px), "
            + $"viewport {scroll.Viewport.Height:F0}px, extent {scroll.Extent.Height:F0}px";
        var settled = PdfPerformanceSession.Now;
        await viewer.WaitForIdleAsync(host.Token);
        host.Record.Metrics["settleMs"] = PdfPerformanceSession.MillisecondsBetween(settled, PdfPerformanceSession.Now);
        // Phase 2 is a paced walk whose per-step frame is captured for the interval series.
        // Each step forces a composition batch commit, so these are forced-commit frame timings.
        const int pacedSteps = 20;
        var intervals = new List<double>(pacedSteps);
        var latencies = new List<double>(pacedSteps);
        long? previousRendered = null;
        for (var step = 0; step < pacedSteps; step++)
        {
            scroll.Offset = new Vector(scroll.Offset.X, scroll.Offset.Y + 120);
            var (latency, renderedAt) = await PresentProbe.CaptureFrameAsync(Require(host.Window), host.Token);
            latencies.Add(latency);
            if (previousRendered is { } previous)
                intervals.Add(PdfPerformanceSession.MillisecondsBetween(previous, renderedAt));
            previousRendered = renderedAt;
        }
        host.Record.Samples["frameIntervalMs"] = intervals.ToArray();
        host.Record.Samples["frameCommitToRenderedMs"] = latencies.ToArray();
        host.Record.Measurement.PresentMethod = PresentProbe.MethodName;
        host.Record.Measurement.ScanOutVerified = PresentProbe.ScanOutVerified;
        host.Record.Notes["settleMethod"] =
            "settleMs = stop-scroll of a continuous 40-step burst (8ms cadence, no frame waits) → WaitForIdleAsync, "
            + "which includes the full-quality upgrade pass.";
        host.Record.Notes["frameMethod"] =
            "frameIntervalMs = intervals between consecutive CompositionBatch.Rendered notifications under a forced commit per paced step; "
            + "frameCommitToRenderedMs = commit → rendered latency. Neither is a scan-out FPS measurement.";
    }

    private static async Task SearchAsync(ScenarioHost host, bool fullDocument)
    {
        host.Record.Conditions.Warmup = "none";
        await using var document = await PdfDocument.LoadAsync(host.CorpusBytes, cancellationToken: host.Token);
        var query = host.Corpus.SearchQuery ?? (fullDocument ? "Page" : "Page 3");
        var started = PdfPerformanceSession.Now;
        host.Session.Mark("searchStart");
        double? firstHitMs = null;
        var matches = 0;
        await foreach (var page in document.SearchPagesAsync(query, cancellationToken: host.Token))
        {
            matches += page.Matches.Count;
            if (firstHitMs is null && page.Matches.Count > 0)
            {
                firstHitMs = PdfPerformanceSession.MillisecondsBetween(started, PdfPerformanceSession.Now);
                host.Session.Mark("firstHit", $"page-{page.PageIndex + 1}");
            }
        }
        var totalMs = PdfPerformanceSession.MillisecondsBetween(started, PdfPerformanceSession.Now);
        if (fullDocument)
        {
            host.Record.Metrics["searchTotalMs"] = totalMs;
            host.Record.Metrics["searchMatchCount"] = matches;
        }
        else
        {
            host.Record.Metrics["searchFirstHitMs"] = firstHitMs;
            host.Record.Metrics["searchTotalMs"] = totalMs;
            if (firstHitMs is null)
                host.Record.Notes["searchFirstHitMs"] = "not-applicable: corpus has no match for the query";
        }
        host.Record.Notes["searchQuery"] = query;
    }

    private static async Task CancelQueuedAsync(ScenarioHost host)
    {
        host.Record.Conditions.Warmup = "none";
        // One worker holds the serial native queue with big raster fills; the doomed renders
        // sit behind it and must drop the moment their token fires.
        await using var document = await PdfDocument.LoadAsync(host.CorpusBytes, cancellationToken: host.Token);
        await using var blockerDocument = await PdfDocument.LoadAsync(PdfFixture.ManyPages(2), cancellationToken: host.Token);
        var blockers = new List<Task<PdfRenderedPage>>();
        for (var index = 0; index < 8; index++)
            blockers.Add(blockerDocument.RenderAsync(0, 5000, 7000, cancellationToken: host.Token));
        await Task.Delay(30, host.Token);
        using var doomedCancellation = CancellationTokenSource.CreateLinkedTokenSource(host.Token);
        var doomed = new List<Task<PdfRenderedPage>>();
        for (var index = 0; index < 8; index++)
            doomed.Add(document.RenderAsync(index % host.Corpus.Pages, 256, 356, cancellationToken: doomedCancellation.Token));
        await Task.Delay(10, host.Token);
        var cancelRequested = PdfPerformanceSession.Now;
        doomedCancellation.Cancel();
        try
        {
            await Task.WhenAll(doomed);
            host.Record.Status = "completed-before-cancel";
            host.Record.Notes["cancelObservedMs"] = "not-applicable: every doomed item completed before the cancel landed";
        }
        catch (OperationCanceledException)
        {
            host.Record.Metrics["cancelObservedMs"] =
                PdfPerformanceSession.MillisecondsBetween(cancelRequested, PdfPerformanceSession.Now);
        }
        foreach (var blocker in blockers)
        {
            try { await blocker; }
            catch (OperationCanceledException) { }
        }
    }

    private static async Task CancelNativeRenderAsync(ScenarioHost host)
    {
        host.Record.Conditions.Warmup = "none";
        await using var document = await PdfDocument.LoadAsync(host.CorpusBytes, cancellationToken: host.Token);
        using var renderCancellation = CancellationTokenSource.CreateLinkedTokenSource(host.Token);
        // A ~57MP raster keeps the progressive native render busy long enough to abort mid-run;
        // PDFium's pause callback observes the token between steps.
        var rendering = document.RenderAsync(0, 6400, 9000, cancellationToken: renderCancellation.Token);
        await Task.Delay(25, host.Token);
        var cancelRequested = PdfPerformanceSession.Now;
        renderCancellation.Cancel();
        try
        {
            await rendering;
            host.Record.Status = "completed-before-cancel";
            host.Record.Notes["cancelObservedMs"] = "not-applicable: the large raster completed before the cancel landed";
        }
        catch (OperationCanceledException)
        {
            host.Record.Metrics["cancelObservedMs"] =
                PdfPerformanceSession.MillisecondsBetween(cancelRequested, PdfPerformanceSession.Now);
        }
    }

    private static async Task CloseReleaseAsync(ScenarioHost host)
    {
        var viewer = Require(host.Viewer);
        host.Record.Conditions.Warmup = "document open and settled before close";
        await viewer.LoadDocumentAsync(host.CorpusBytes, cancellationToken: host.Token);
        await viewer.WaitForIdleAsync(host.Token);
        var marksBefore = host.Session.Marks.Count;
        var started = PdfPerformanceSession.Now;
        var closing = viewer.CloseDocumentAsync();
        // The UI-visible close is synchronous and recorded by the product as a Release span
        // right after the closeRequested mark; awaiting covers the native teardown too.
        var closeMark = host.Session.Marks.Skip(marksBefore)
            .FirstOrDefault(mark => mark.Name == PdfPerformanceSession.CloseRequestedMark);
        var releaseSpan = host.Session.Spans
            .Where(span => span.Stage == PdfPerformanceStage.Release && span.StartTimestamp >= closeMark.Timestamp)
            .OrderBy(span => span.StartTimestamp)
            .FirstOrDefault();
        host.Record.Metrics["closeUiMs"] = releaseSpan.EndTimestamp == 0
            ? null
            : PdfPerformanceSession.MillisecondsBetween(releaseSpan.StartTimestamp, releaseSpan.EndTimestamp);
        await closing;
        host.Record.Metrics["closeReleaseTotalMs"] = PdfPerformanceSession.MillisecondsBetween(started, PdfPerformanceSession.Now);
        if (releaseSpan.EndTimestamp == 0)
            host.Record.Notes["closeUiMs"] = "not-recorded: product Release span missing";
    }

    private static async Task OpenCloseCyclesAsync(ScenarioHost host)
    {
        var viewer = Require(host.Viewer);
        const int cycles = 100;
        const int warmCycles = 20;
        host.Record.Conditions.Warmup = $"cycles 0-{warmCycles - 1} excluded: JIT + page-cache fill to budget; first sample after the cache is at steady state";
        double? firstPrivate = null;
        double? lastPrivate = null;
        long? firstCache = null;
        long? lastCache = null;
        for (var cycle = 0; cycle < cycles; cycle++)
        {
            host.Token.ThrowIfCancellationRequested();
            await viewer.LoadDocumentAsync(host.CorpusBytes, cancellationToken: host.Token);
            if (cycle is warmCycles or cycles - 1)
            {
                // The delta gate measures retained growth, not GC lag or cache fill: settle first,
                // sample after the budgeted caches reached steady state. The sample itself still
                // reads counters without forcing a collection.
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                var sample = viewer.CaptureResourceSample();
                if (cycle == warmCycles)
                {
                    firstPrivate = sample.PrivateBytes;
                    firstCache = sample.Cache?.TrackedBytes;
                }
                else
                {
                    lastPrivate = sample.PrivateBytes;
                    lastCache = sample.Cache?.TrackedBytes;
                }
            }
            await viewer.CloseDocumentAsync();
        }
        host.Record.Metrics["privateBytesFirst"] = firstPrivate;
        host.Record.Metrics["privateBytesLast"] = lastPrivate;
        host.Record.Metrics["privateBytesDeltaBytes"] =
            firstPrivate is { } first && lastPrivate is { } last ? last - first : null;
        host.Record.Metrics["cacheBytesFirst"] = firstCache;
        host.Record.Metrics["cacheBytesLast"] = lastCache;
        host.Record.Notes["cycles"] = cycles.ToString();
        host.Record.Notes["sampling"] =
            "full GC before each sample measures retained private-bytes growth, not garbage lag; "
            + "first sample after 20 warm cycles so budgeted cache fill is not counted as growth";
    }

    private static async Task NavigateAsync(Reader viewer, int page, CancellationToken token)
    {
        viewer.GoToPage(page);
        var deadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency * 60;
        while (viewer.CurrentPage != page)
        {
            if (Stopwatch.GetTimestamp() > deadline)
                throw new TimeoutException($"Navigation to page {page} did not land; CurrentPage={viewer.CurrentPage}.");
            await Task.Delay(5, token);
        }
        await viewer.WaitForIdleAsync(token);
    }

    private static async Task WaitMarksAsync(ScenarioHost host, params string[] names)
    {
        var deadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency * 60;
        while (true)
        {
            var marks = host.Session.Marks;
            if (names.All(name => marks.Any(mark => mark.Name == name))) return;
            if (Stopwatch.GetTimestamp() > deadline)
                throw new TimeoutException("Timed out waiting for first-screen marks: " + string.Join(", ", names));
            await Task.Delay(5, host.Token);
        }
    }

    private static double StageSum(PdfPerformanceSession session, PdfPerformanceStage stage, long? from = null, long? to = null) =>
        session.Spans.Where(span => span.Stage == stage)
            .Where(span => from is not { } start || span.EndTimestamp >= start)
            .Where(span => to is not { } end || span.StartTimestamp <= end)
            .Sum(span => PdfPerformanceSession.MillisecondsBetween(span.StartTimestamp, span.EndTimestamp));

    private static double StageMax(PdfPerformanceSession session, PdfPerformanceStage stage, long? from = null, long? to = null) =>
        session.Spans.Where(span => span.Stage == stage)
            .Where(span => from is not { } start || span.EndTimestamp >= start)
            .Where(span => to is not { } end || span.StartTimestamp <= end)
            .Select(span => PdfPerformanceSession.MillisecondsBetween(span.StartTimestamp, span.EndTimestamp))
            .DefaultIfEmpty(0)
            .Max();

    private static T Require<T>(T? value) => value ?? throw new InvalidOperationException("The scenario needs a UI host for this step.");
}
