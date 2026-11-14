using System.Reflection;
using NT.AvaloniaControl.PdfViewer;
using NT.AvaloniaControl.PdfViewer.Interop;

namespace PdfViewer.Core.Tests;

public sealed class SchedulerPerformanceCompletionTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(15);

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Completion_is_not_published_while_execution_span_is_blocked(bool fail, bool throughLifetime)
    {
        using var session = PdfPerformanceSession.Begin("completion-order");
        var scheduler = new PdfiumWorkScheduler();
        using var resume = new ManualResetEventSlim();
        using var leftOperation = new ManualResetEventSlim();
        var entered = new TaskCompletionSource<Thread>(TaskCreationOptions.RunContinuationsAsynchronously);
        var error = new InvalidDataException("operation failure");
        int Operation()
        {
            entered.SetResult(Thread.CurrentThread);
            if (!resume.Wait(Deadline, TestContext.Current.CancellationToken)) throw new TimeoutException();
            leftOperation.Set();
            if (fail) throw error;
            return 42;
        }
        Task observed;
        if (throughLifetime)
        {
            var lifetime = new PdfDocumentLifetime(() => Operation(), action =>
                scheduler.Enqueue(() => { action(); return 42; }, CancellationToken.None,
                    stage: PdfPerformanceStage.Release, correlationId: "release-gated"));
            lifetime.RequestDispose();
            observed = lifetime.Completion;
        }
        else
        {
            observed = scheduler.Enqueue(Operation, CancellationToken.None,
                stage: PdfPerformanceStage.Release, correlationId: "release-gated");
        }
        bool completedBeforeRecord;
        try
        {
            var worker = await entered.Task.WaitAsync(Deadline, TestContext.Current.CancellationToken);
            // Hold the real recorder lock, rather than sleeping and hoping to win the race.
            // QueueWait has already been recorded. The worker's only remaining blocking
            // operation is its Release record. Reflection keeps test hooks out of the product.
            var gate = typeof(PdfPerformanceSession).GetField("_gate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
            lock (gate)
            {
                resume.Set();
                Assert.True(leftOperation.Wait(Deadline, TestContext.Current.CancellationToken));
                Assert.True(SpinWait.SpinUntil(() => (worker.ThreadState & ThreadState.WaitSleepJoin) != 0, Deadline),
                    "worker must reach the blocked recorder before checking completion");
                completedBeforeRecord = observed.IsCompleted;
            }
        }
        finally { resume.Set(); }
        if (fail)
            Assert.Same(error, await Assert.ThrowsAsync<InvalidDataException>(() => observed.WaitAsync(Deadline, TestContext.Current.CancellationToken)));
        else
            await observed.WaitAsync(Deadline, TestContext.Current.CancellationToken);
        // A queue barrier drains a possibly old implementation's finally before disposing
        // the session; it is deliberately after the observed ordering assertion snapshot.
        await scheduler.Enqueue(() => 0, CancellationToken.None).WaitAsync(Deadline, TestContext.Current.CancellationToken);
        Assert.False(completedBeforeRecord, "task completion must not overtake its execution span");
        Assert.Single(session.Spans, s => s.CorrelationId == "release-gated" && s.Stage == PdfPerformanceStage.Release);
    }

    [Fact]
    public async Task Running_cancellation_records_observation_and_execution_before_completion()
    {
        using var session = PdfPerformanceSession.Begin("cancel-order");
        var scheduler = new PdfiumWorkScheduler();
        using var resume = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var work = scheduler.Enqueue(() =>
        {
            entered.SetResult();
            if (!resume.Wait(Deadline, TestContext.Current.CancellationToken)) throw new TimeoutException();
            cancellation.Token.ThrowIfCancellationRequested();
            return 0;
        }, cancellation.Token, stage: PdfPerformanceStage.Search, correlationId: "search-cancel");
        try
        {
            await entered.Task.WaitAsync(Deadline, TestContext.Current.CancellationToken);
            cancellation.Cancel();
            Assert.False(work.IsCompleted);
            resume.Set();
            var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => work.WaitAsync(Deadline, TestContext.Current.CancellationToken));
            var spansAtCompletion = session.Spans;
            await scheduler.Enqueue(() => 0, CancellationToken.None).WaitAsync(Deadline, TestContext.Current.CancellationToken);
            Assert.Equal(cancellation.Token, exception.CancellationToken);
            Assert.Single(spansAtCompletion, s => s.CorrelationId == "search-cancel" && s.Stage == PdfPerformanceStage.CancelObserved);
            Assert.Single(spansAtCompletion, s => s.CorrelationId == "search-cancel" && s.Stage == PdfPerformanceStage.Search);
        }
        finally { resume.Set(); }
    }

    [Fact]
    public async Task Ended_session_does_not_receive_late_spans_or_contaminate_its_successor()
    {
        using var first = PdfPerformanceSession.Begin("first");
        var scheduler = new PdfiumWorkScheduler();
        using var resume = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = scheduler.Enqueue(() =>
        {
            entered.SetResult();
            if (!resume.Wait(Deadline, TestContext.Current.CancellationToken)) throw new TimeoutException();
            return 1;
        }, CancellationToken.None, correlationId: "old-running");
        try
        {
            await entered.Task.WaitAsync(Deadline, TestContext.Current.CancellationToken);
            var queued = scheduler.Enqueue(() => 2, CancellationToken.None, correlationId: "old-queued");
            var canceled = scheduler.Enqueue(() => 3, cancellation.Token, correlationId: "old-canceled");
            first.Dispose();
            var frozen = first.Spans.ToArray();
            using var next = PdfPerformanceSession.Begin("next");
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled.WaitAsync(Deadline, TestContext.Current.CancellationToken));
            resume.Set();
            Assert.Equal(1, await running.WaitAsync(Deadline, TestContext.Current.CancellationToken));
            Assert.Equal(2, await queued.WaitAsync(Deadline, TestContext.Current.CancellationToken));
            Assert.Equal(4, await scheduler.Enqueue(() => 4, CancellationToken.None, correlationId: "new-work").WaitAsync(Deadline, TestContext.Current.CancellationToken));
            var spans = next.Spans;
            Assert.DoesNotContain(spans, s => s.CorrelationId.StartsWith("old-", StringComparison.Ordinal));
            Assert.Contains(spans, s => s.CorrelationId == "new-work" && s.Stage == PdfPerformanceStage.NativeExec);
            Assert.Equal(frozen, first.Spans);
            Assert.Throws<ObjectDisposedException>(() => first.Record("manual", PdfPerformanceStage.NativeExec, 0, 0));
        }
        finally { resume.Set(); await running.WaitAsync(Deadline, CancellationToken.None); }
    }

    [Fact]
    public async Task Work_started_without_a_session_does_not_join_a_later_session()
    {
        var scheduler = new PdfiumWorkScheduler();
        using var resume = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = scheduler.Enqueue(() =>
        {
            entered.SetResult();
            if (!resume.Wait(Deadline, TestContext.Current.CancellationToken)) throw new TimeoutException();
            return 1;
        }, CancellationToken.None, correlationId: "unobserved");
        try
        {
            await entered.Task.WaitAsync(Deadline, TestContext.Current.CancellationToken);
            using var session = PdfPerformanceSession.Begin("late");
            resume.Set();
            Assert.Equal(1, await running.WaitAsync(Deadline, TestContext.Current.CancellationToken));
            await scheduler.Enqueue(() => 0, CancellationToken.None, correlationId: "barrier").WaitAsync(Deadline, TestContext.Current.CancellationToken);
            Assert.DoesNotContain(session.Spans, s => s.CorrelationId == "unobserved");
        }
        finally { resume.Set(); await running.WaitAsync(Deadline, CancellationToken.None); }
    }
}
