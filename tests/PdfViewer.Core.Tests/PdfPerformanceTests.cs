using NT.AvaloniaControl.PdfViewer;
using NT.AvaloniaControl.PdfViewer.Interop;
using PdfViewer.Tests;

namespace PdfViewer.Core.Tests;

/// <summary>
/// P3-05 metric-contract tests (PERF01/PERF16): the session records monotonic spans and
/// once-only marks, the scheduler splits queue wait from execution under a shared correlation
/// id, load/render/search/release are timed at the stages the performance plan defines, and
/// resource samples are captured together without forcing collections.
/// </summary>
public sealed class PdfPerformanceTests
{
    [Fact]
    public void Session_records_spans_marks_totals_and_resource_samples()
    {
        using var session = PdfPerformanceSession.Begin("contract");
        Assert.Same(session, PdfPerformanceSession.Current);
        Assert.Equal("contract", session.Label);
        var start = PdfPerformanceSession.Now;
        session.Record(session.NextCorrelationId("page"), PdfPerformanceStage.QueueWait, start, PdfPerformanceSession.Now);
        Assert.True(session.Mark("first"));
        Assert.False(session.Mark("first"), "mark names land exactly once per session");
        Assert.True(session.Mark(PdfPerformanceSession.FirstPageDecodedMark, "page-1"));
        var sample = session.SampleResources(new PdfViewerCacheStatistics(1, 2, 0, 0, 0, 0, 0, 0, 0, 0, 2, 2));
        Assert.Same(sample, Assert.Single(session.ResourceSamples));
        Assert.True(sample.ManagedHeapBytes > 0);
        Assert.True(sample.ManagedAllocatedBytes > 0);
        Assert.True(sample.PrivateBytes > 0);
        Assert.True(sample.WorkingSetBytes > 0);
        Assert.Equal(2, sample.Cache!.BudgetedBytes);
        var span = Assert.Single(session.Spans);
        Assert.Equal(PdfPerformanceStage.QueueWait, span.Stage);
        Assert.True(span.EndTimestamp >= span.StartTimestamp);
        var totals = Assert.Single(session.Totals);
        Assert.Equal(1, totals.Count);
        Assert.True(totals.MaxMs >= 0);
    }

    [Fact]
    public void Session_is_single_per_process_and_dispose_clears_current()
    {
        using var first = PdfPerformanceSession.Begin("first");
        Assert.Throws<InvalidOperationException>(() => PdfPerformanceSession.Begin("second"));
        first.Dispose();
        Assert.Null(PdfPerformanceSession.Current);
        first.Dispose(); // idempotent
        using var second = PdfPerformanceSession.Begin("second");
        Assert.Same(second, PdfPerformanceSession.Current);
    }

    [Fact]
    public async Task Scheduler_splits_queue_wait_from_execution_and_records_cancel()
    {
        using var session = PdfPerformanceSession.Begin("scheduler");
        var scheduler = new PdfiumWorkScheduler();
        using var tokenSource = new CancellationTokenSource();
        var entered = new ManualResetEventSlim();
        var release = new ManualResetEventSlim();
        try
        {
            var held = scheduler.Enqueue(() =>
            {
                entered.Set();
                Assert.True(release.Wait(TimeSpan.FromSeconds(30), CancellationToken.None));
                return 1;
            }, TestContext.Current.CancellationToken, PdfiumWorkPriority.Foreground, PdfPerformanceStage.NativeExec, "held-1");
            Assert.True(entered.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
            var doomed = scheduler.Enqueue(() => 2, tokenSource.Token, PdfiumWorkPriority.Search,
                PdfPerformanceStage.Search, "doomed-1");
            tokenSource.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => doomed);
            release.Set();
            Assert.Equal(1, await held);
            var spans = session.Spans;
            var queueWait = Assert.Single(spans, span => span.CorrelationId == "held-1" && span.Stage == PdfPerformanceStage.QueueWait);
            var execution = Assert.Single(spans, span => span.CorrelationId == "held-1" && span.Stage == PdfPerformanceStage.NativeExec);
            Assert.True(execution.StartTimestamp >= queueWait.EndTimestamp, "execution starts when queue wait ends");
            var canceled = Assert.Single(spans, span => span.Stage == PdfPerformanceStage.CancelObserved);
            Assert.Equal("doomed-1", canceled.CorrelationId);
            Assert.True(canceled.StartTimestamp == canceled.EndTimestamp, "a cancel observation is a point event");
        }
        finally
        {
            release.Set();
        }
    }

    [Fact]
    public async Task Load_records_input_read_open_parse_and_the_return_mark()
    {
        using var session = PdfPerformanceSession.Begin("load");
        using var document = await PdfDocument.LoadAsync(PdfFixture.ReadingDocument(),
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, document.Pages.Count);
        var marks = session.Marks;
        var loadStart = Assert.Single(marks, mark => mark.Name == PdfPerformanceSession.LoadStartMark);
        var loadReturn = Assert.Single(marks, mark => mark.Name == PdfPerformanceSession.LoadReturnMark);
        Assert.True(loadReturn.Timestamp >= loadStart.Timestamp);
        Assert.Equal(loadStart.CorrelationId, loadReturn.CorrelationId);
        var spans = session.Spans;
        var inputRead = Assert.Single(spans, span => span.Stage == PdfPerformanceStage.InputRead);
        var openParse = Assert.Single(spans, span => span.Stage == PdfPerformanceStage.OpenParse);
        Assert.Equal(loadStart.CorrelationId, inputRead.CorrelationId);
        Assert.Equal(loadStart.CorrelationId, openParse.CorrelationId);
        Assert.Contains(spans, span => span.Stage == PdfPerformanceStage.QueueWait && span.CorrelationId == loadStart.CorrelationId);
        // A LoadDocumentAsync return is a mark on the open timeline, never a first screen:
        // the product records no present milestone of its own.
        Assert.DoesNotContain(marks, mark => mark.Name == PdfPerformanceSession.FirstPagePresentedMark);
        Assert.DoesNotContain(marks, mark => mark.Name == PdfPerformanceSession.FirstPageDecodedMark);
    }

    [Fact]
    public async Task Render_search_and_release_are_timed_with_correlation_ids()
    {
        using var session = PdfPerformanceSession.Begin("render-search-release");
        var document = await PdfDocument.LoadAsync(PdfFixture.ManyPages(4),
            cancellationToken: TestContext.Current.CancellationToken);
        await document.RenderAsync(0, 120, 160, cancellationToken: TestContext.Current.CancellationToken);
        var searchResults = new List<PdfSearchResult>();
        await foreach (var page in document.SearchPagesAsync("Page 2", cancellationToken: TestContext.Current.CancellationToken))
            searchResults.AddRange(page.Matches);
        Assert.NotEmpty(searchResults);
        await document.DisposeAsync();

        var spans = session.Spans;
        var decode = Assert.Single(spans, span => span.Stage == PdfPerformanceStage.DecodeComplete);
        Assert.Equal("render", decode.CorrelationId.Split('-')[0]);
        Assert.Contains(spans, span => span.CorrelationId == decode.CorrelationId && span.Stage == PdfPerformanceStage.NativeExec);
        Assert.Contains(spans, span => span.CorrelationId == decode.CorrelationId && span.Stage == PdfPerformanceStage.QueueWait);
        var searchSpans = spans.Where(span => span.Stage == PdfPerformanceStage.Search).ToArray();
        Assert.NotEmpty(searchSpans);
        Assert.All(searchSpans, span => Assert.StartsWith("search-", span.CorrelationId));
        Assert.Single(searchSpans.Select(span => span.CorrelationId).Distinct());
        // The asynchronous native teardown is a Release work item on the serial scheduler.
        Assert.Single(spans, span => span.Stage == PdfPerformanceStage.Release);
    }

    [Fact]
    public async Task Resource_samples_stay_read_together_across_document_lifecycle()
    {
        using var session = PdfPerformanceSession.Begin("resources");
        var baseline = session.SampleResources();
        var document = await PdfDocument.LoadAsync(PdfFixture.ManyPages(30),
            cancellationToken: TestContext.Current.CancellationToken);
        await document.RenderAsync(0, 300, 400, cancellationToken: TestContext.Current.CancellationToken);
        var loaded = session.SampleResources();
        await document.DisposeAsync();
        var released = session.SampleResources();
        Assert.Equal(3, session.ResourceSamples.Count);
        Assert.True(released.Timestamp > loaded.Timestamp && loaded.Timestamp > baseline.Timestamp);
        // Bytes are sampled as-is: the audit deliberately does not force a GC to look smaller.
        Assert.True(baseline.ManagedAllocatedBytes <= loaded.ManagedAllocatedBytes);
    }
}
