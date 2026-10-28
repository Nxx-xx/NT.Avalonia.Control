using Avalonia.Controls;
using PdfViewer.Tests;
using NT.AvaloniaControl.PdfViewer;
using Reader = NT.AvaloniaControl.PdfViewer.PdfViewer;

namespace PdfViewer.Headless.Tests;

/// <summary>
/// P3-05 viewer-pipeline instrumentation (PERF01/PERF17): with a session active, a headless
/// open records the first-screen stage chain loadStart → loadReturn → firstPageDecoded →
/// firstPageSubmitted on the monotonic clock, upload/submit/layout spans are distinct, the
/// product never claims a compositor present, and resource samples join the cache accounting.
/// </summary>
public sealed class P3PerformanceTests
{
    [Fact]
    public Task First_screen_records_the_stage_chain_without_claiming_a_present() => HeadlessTestScope.Run(async () =>
    {
        using var session = PdfPerformanceSession.Begin("first-screen");
        using var fixture = await PerfFixture.Create(PdfFixture.ManyPages(6));
        await fixture.Idle();
        var marks = session.Marks;
        var loadStart = Assert.Single(marks, mark => mark.Name == PdfPerformanceSession.LoadStartMark);
        var loadReturn = Assert.Single(marks, mark => mark.Name == PdfPerformanceSession.LoadReturnMark);
        var decoded = Assert.Single(marks, mark => mark.Name == PdfPerformanceSession.FirstPageDecodedMark);
        var submitted = Assert.Single(marks, mark => mark.Name == PdfPerformanceSession.FirstPageSubmittedMark);
        Assert.True(loadStart.Timestamp <= loadReturn.Timestamp);
        Assert.True(loadReturn.Timestamp <= decoded.Timestamp);
        Assert.True(decoded.Timestamp <= submitted.Timestamp);
        // The load return is a point on the open timeline, never the first screen itself.
        Assert.NotEqual(decoded.CorrelationId, loadReturn.CorrelationId);
        var spans = session.Spans;
        // Every visible page fill records its own upload/submit pair; the first pair carries
        // the same correlation id as the first-screen marks.
        var uploadSpans = spans.Where(span => span.Stage == PdfPerformanceStage.BitmapUpload).ToArray();
        var submitSpans = spans.Where(span => span.Stage == PdfPerformanceStage.UiSubmit).ToArray();
        Assert.NotEmpty(uploadSpans);
        Assert.NotEmpty(submitSpans);
        Assert.Equal(uploadSpans[0].CorrelationId, submitSpans[0].CorrelationId);
        Assert.Equal(decoded.CorrelationId, uploadSpans[0].CorrelationId);
        Assert.Equal(submitted.CorrelationId, submitSpans[0].CorrelationId);
        Assert.True(uploadSpans[0].EndTimestamp <= submitSpans[0].EndTimestamp, "bitmap upload precedes the UI submit");
        Assert.Contains(spans, span => span.Stage == PdfPerformanceStage.LayoutBuild);
        Assert.Contains(spans, span => span.Stage == PdfPerformanceStage.DecodeComplete
            && span.CorrelationId == decoded.CorrelationId);
        // Present is harness territory (PERF17): the product records decode and submit only.
        Assert.DoesNotContain(marks, mark => mark.Name == PdfPerformanceSession.FirstPagePresentedMark);
        Assert.DoesNotContain(spans, span => span.Stage == PdfPerformanceStage.CompositorRendered);
        // A harness marks the present itself once a compositor batch rendered; headless has
        // no compositor, so this harness honestly records nothing beyond the product chain.
        Assert.Null(fixture.Reader.ErrorMessage);
    });

    [Fact]
    public Task Search_first_hit_is_measurable_against_the_recorded_spans() => HeadlessTestScope.Run(async () =>
    {
        using var session = PdfPerformanceSession.Begin("search-first-hit");
        using var fixture = await PerfFixture.Create(PdfFixture.ManyPages(20));
        await fixture.Idle();
        var searchStarted = PdfPerformanceSession.Now;
        Assert.True(session.Mark("searchStart"));
        var results = await fixture.Reader.SearchAsync("Page 3", TestContext.Current.CancellationToken);
        var firstHitAt = PdfPerformanceSession.Now;
        Assert.True(session.Mark("firstHit"));
        Assert.True(results.Count > 0);
        Assert.True(firstHitAt >= searchStarted);
        var searchSpans = session.Spans.Where(span => span.Stage == PdfPerformanceStage.Search).ToArray();
        Assert.NotEmpty(searchSpans);
        Assert.All(searchSpans, span => Assert.StartsWith("search-", span.CorrelationId));
        Assert.Single(searchSpans.Select(span => span.CorrelationId).Distinct());
        Assert.Null(fixture.Reader.ErrorMessage);
    });

    [Fact]
    public Task Close_records_release_and_resource_samples_join_the_cache() => HeadlessTestScope.Run(async () =>
    {
        using var session = PdfPerformanceSession.Begin("close-resources");
        using var fixture = await PerfFixture.Create(PdfFixture.ManyPages(12));
        await fixture.Idle();
        var loaded = fixture.Reader.CaptureResourceSample();
        Assert.NotNull(loaded.Cache);
        Assert.True(loaded.Cache!.BudgetedBytes > 0);
        Assert.True(loaded.ManagedHeapBytes > 0);
        Assert.Equal(loaded, Assert.Single(session.ResourceSamples));
        await fixture.Reader.CloseDocumentAsync();
        Assert.Contains(session.Marks, mark => mark.Name == PdfPerformanceSession.CloseRequestedMark);
        Assert.Contains(session.Spans, span => span.Stage == PdfPerformanceStage.Release);
        Assert.False(fixture.Reader.HasDocument);
        Assert.Null(fixture.Reader.ErrorMessage);
    });

    private sealed class PerfFixture : IDisposable
    {
        internal Reader Reader { get; } = new() { IsSidebarVisible = false };
        internal Window Window { get; }

        internal PerfFixture()
        {
            Window = new Window { Width = 1000, Height = 700, Content = Reader };
            Window.Show();
            Window.UpdateLayout();
            HeadlessTestScope.Drain();
        }

        internal static async Task<PerfFixture> Create(byte[] bytes)
        {
            var fixture = new PerfFixture();
            try
            {
                await fixture.Reader.LoadDocumentAsync(bytes, cancellationToken: TestContext.Current.CancellationToken);
                await fixture.Idle();
                return fixture;
            }
            catch { fixture.Dispose(); throw; }
        }

        internal async Task Idle()
        {
            Window.UpdateLayout();
            HeadlessTestScope.Drain();
            await Reader.WaitForIdleAsync(TestContext.Current.CancellationToken);
            Window.UpdateLayout();
            HeadlessTestScope.Drain();
        }

        public void Dispose()
        {
            Reader.Dispose();
            Window.Close();
        }
    }
}
