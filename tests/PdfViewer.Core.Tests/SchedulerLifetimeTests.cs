using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using PdfViewer.Tests;
using NT.AvaloniaControl.PdfViewer;
using NT.AvaloniaControl.PdfViewer.Interop;

namespace PdfViewer.Core.Tests;

public sealed class SchedulerLifetimeTests
{
    // Cleanup work deliberately uses CancellationToken.None: even an aborted test must await resource release.
    // Separate per-case cancellation sources below are the tokens whose semantics the tests exercise.
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task Scheduler_serializes_independent_callers_and_continues_after_failure()
    {
        var scheduler = new PdfiumWorkScheduler();
        var active = 0;
        var overlap = 0;
        var seen = new ConcurrentDictionary<int, byte>();
        var tasks = Enumerable.Range(0, 100).Select(index => scheduler.Enqueue(() =>
        {
            var current = Interlocked.Increment(ref active);
            if (current != 1)
                Interlocked.Exchange(ref overlap, 1);
            try
            {
                Assert.True(seen.TryAdd(index, 0));
                if (index == 42)
                    throw new InvalidDataException("A failing document must not stop the shared queue.");
                return index;
            }
            finally { Interlocked.Decrement(ref active); }
        }, TestContext.Current.CancellationToken)).ToArray();
        await Assert.ThrowsAsync<InvalidDataException>(() => Task.WhenAll(tasks).WaitAsync(Deadline, TestContext.Current.CancellationToken));
        Assert.Equal(0, overlap);
        Assert.Equal(100, seen.Count);
        Assert.Equal(123, await scheduler.Enqueue(() => 123, TestContext.Current.CancellationToken).WaitAsync(Deadline, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Weighted_priority_serves_foreground_search_and_thumbnails_without_starvation()
    {
        var scheduler = new PdfiumWorkScheduler();
        using var resume = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var held = scheduler.Enqueue(() =>
        {
            entered.SetResult();
            if (!resume.Wait(Deadline, TestContext.Current.CancellationToken)) throw new TimeoutException();
            return true;
        }, TestContext.Current.CancellationToken);
        try
        {
            await entered.Task.WaitAsync(Deadline, TestContext.Current.CancellationToken);
            var order = new ConcurrentQueue<PdfiumWorkPriority>();
            var work = new List<Task<int>>();
            foreach (var priority in new[] { PdfiumWorkPriority.Thumbnail, PdfiumWorkPriority.Search, PdfiumWorkPriority.Foreground })
            {
                for (var index = 0; index < 14; index++)
                    work.Add(scheduler.Enqueue(() => { order.Enqueue(priority); return 1; }, cancellationToken: TestContext.Current.CancellationToken, priority: priority));
            }
            resume.Set();
            await Task.WhenAll(work).WaitAsync(Deadline, TestContext.Current.CancellationToken);
            var firstCycle = order.Take(7).ToArray();
            Assert.Equal(4, firstCycle.Count(item => item == PdfiumWorkPriority.Foreground));
            Assert.Equal(2, firstCycle.Count(item => item == PdfiumWorkPriority.Search));
            Assert.Single(firstCycle, item => item == PdfiumWorkPriority.Thumbnail);
            Assert.Equal(42, order.Count);
        }
        finally { resume.Set(); await held.WaitAsync(Deadline, CancellationToken.None); }
    }

    [Fact]
    public async Task Queued_cancellation_completes_before_a_busy_worker_and_never_invokes_the_operation()
    {
        var scheduler = new PdfiumWorkScheduler();
        using var resume = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var held = scheduler.Enqueue(() =>
        {
            entered.SetResult();
            if (!resume.Wait(Deadline, TestContext.Current.CancellationToken)) throw new TimeoutException();
            return true;
        }, TestContext.Current.CancellationToken);
        try
        {
            await entered.Task.WaitAsync(Deadline, TestContext.Current.CancellationToken);
            using var cancellation = new CancellationTokenSource();
            var invoked = false;
            var queued = scheduler.Enqueue(() => { invoked = true; return true; }, cancellation.Token);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued.WaitAsync(Deadline, TestContext.Current.CancellationToken));
            Assert.False(held.IsCompleted);
            Assert.False(invoked);
            resume.Set();
            await scheduler.Enqueue(() => true, TestContext.Current.CancellationToken).WaitAsync(Deadline, TestContext.Current.CancellationToken);
            Assert.False(invoked);
        }
        finally { resume.Set(); await held.WaitAsync(Deadline, CancellationToken.None); }
    }

    [Fact]
    public async Task Cancellation_of_running_work_does_not_complete_or_release_it_until_native_return()
    {
        var scheduler = new PdfiumWorkScheduler();
        using var resume = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var work = scheduler.Enqueue(() =>
        {
            entered.SetResult();
            if (!resume.Wait(Deadline, TestContext.Current.CancellationToken)) throw new TimeoutException();
            cancellation.Token.ThrowIfCancellationRequested();
            return true;
        }, cancellation.Token);
        try
        {
            await entered.Task.WaitAsync(Deadline, TestContext.Current.CancellationToken);
            cancellation.Cancel();
            Assert.False(work.IsCompleted);
            resume.Set();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => work.WaitAsync(Deadline, TestContext.Current.CancellationToken));
            Assert.Equal(7, await scheduler.Enqueue(() => 7, TestContext.Current.CancellationToken).WaitAsync(Deadline, TestContext.Current.CancellationToken));
        }
        finally { resume.Set(); }
    }

    [Fact]
    public async Task A_canceled_queued_load_releases_its_large_input_before_the_busy_worker_returns()
    {
        var scheduler = new PdfiumWorkScheduler();
        using var resume = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var held = scheduler.Enqueue(() =>
        {
            entered.SetResult();
            if (!resume.Wait(Deadline, TestContext.Current.CancellationToken)) throw new TimeoutException();
            return true;
        }, TestContext.Current.CancellationToken);
        try
        {
            await entered.Task.WaitAsync(Deadline, TestContext.Current.CancellationToken);
            var pending = QueueLargeInput(scheduler, cancellation.Token);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.Task.WaitAsync(Deadline, TestContext.Current.CancellationToken));
            Assert.False(held.IsCompleted);
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            Assert.False(pending.Input.IsAlive);
        }
        finally { resume.Set(); await held.WaitAsync(Deadline, CancellationToken.None); }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (Task<int> Task, WeakReference Input) QueueLargeInput(PdfiumWorkScheduler scheduler, CancellationToken token)
    {
        var input = new byte[4 * 1024 * 1024];
        return (scheduler.Enqueue(() => input.Length, token), new WeakReference(input));
    }

    [Fact]
    public async Task Concurrent_cancellation_during_admission_leaves_no_runnable_work_and_the_queue_recovers()
    {
        var scheduler = new PdfiumWorkScheduler();
        using var resume = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var held = scheduler.Enqueue(() =>
        {
            entered.SetResult();
            if (!resume.Wait(Deadline, TestContext.Current.CancellationToken)) throw new TimeoutException();
            return true;
        }, TestContext.Current.CancellationToken);
        var sources = Enumerable.Range(0, 256).Select(_ => new CancellationTokenSource()).ToArray();
        try
        {
            await entered.Task.WaitAsync(Deadline, TestContext.Current.CancellationToken);
            var cancellations = new List<Task>();
            var work = new List<Task<int>>();
            var invoked = 0;
            foreach (var source in sources)
            {
                cancellations.Add(Task.Run(() => source.Cancel(), TestContext.Current.CancellationToken));
                work.Add(scheduler.Enqueue(() => Interlocked.Increment(ref invoked), source.Token));
            }
            await Task.WhenAll(cancellations).WaitAsync(Deadline, TestContext.Current.CancellationToken);
            foreach (var pending in work)
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(Deadline, TestContext.Current.CancellationToken));
            Assert.False(held.IsCompleted);
            resume.Set();
            Assert.Equal(123, await scheduler.Enqueue(() => 123, TestContext.Current.CancellationToken)
                .WaitAsync(Deadline, TestContext.Current.CancellationToken));
            Assert.Equal(0, invoked);
        }
        finally
        {
            resume.Set();
            await held.WaitAsync(Deadline, CancellationToken.None);
            foreach (var source in sources) source.Dispose();
        }
    }

    [Fact]
    public async Task Disposal_rejects_admission_immediately_but_awaits_all_leases_and_native_release()
    {
        var scheduler = new PdfiumWorkScheduler();
        using var resume = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var held = scheduler.Enqueue(() =>
        {
            entered.SetResult();
            if (!resume.Wait(Deadline, TestContext.Current.CancellationToken)) throw new TimeoutException();
            return true;
        }, TestContext.Current.CancellationToken);
        try
        {
            await entered.Task.WaitAsync(Deadline, TestContext.Current.CancellationToken);
            var released = 0;
            var scheduled = 0;
            var lifetime = new PdfDocumentLifetime(() => Interlocked.Increment(ref released), action =>
            {
                Interlocked.Increment(ref scheduled);
                return scheduler.Enqueue(() => { action(); return true; }, CancellationToken.None);
            });
            var first = lifetime.Acquire();
            var second = lifetime.Acquire();
            lifetime.RequestDispose();
            lifetime.RequestDispose();
            Assert.True(lifetime.DisposeToken.IsCancellationRequested);
            Assert.Throws<ObjectDisposedException>(() => lifetime.Acquire());
            first.Dispose();
            first.Dispose();
            Assert.Equal(0, scheduled);
            Assert.False(lifetime.Completion.IsCompleted);
            second.Dispose();
            Assert.Equal(1, scheduled);
            Assert.Equal(0, released);
            Assert.False(lifetime.Completion.IsCompleted);
            resume.Set();
            await lifetime.Completion.WaitAsync(Deadline, TestContext.Current.CancellationToken);
            Assert.Equal(1, released);
            lifetime.RequestDispose();
            Assert.Equal(1, scheduled);
            Assert.True(lifetime.DisposeToken.IsCancellationRequested);
            Assert.Throws<ObjectDisposedException>(() => lifetime.Acquire());
        }
        finally { resume.Set(); await held.WaitAsync(Deadline, CancellationToken.None); }
    }

    [Fact]
    public async Task Disposal_cancels_a_queued_lease_without_waiting_for_another_documents_busy_worker()
    {
        var scheduler = new PdfiumWorkScheduler();
        using var resume = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var held = scheduler.Enqueue(() =>
        {
            entered.SetResult();
            if (!resume.Wait(Deadline, TestContext.Current.CancellationToken)) throw new TimeoutException();
            return true;
        }, TestContext.Current.CancellationToken);
        try
        {
            await entered.Task.WaitAsync(Deadline, TestContext.Current.CancellationToken);
            var releaseScheduled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var lifetime = new PdfDocumentLifetime(() => { }, action =>
            {
                releaseScheduled.SetResult();
                return scheduler.Enqueue(() => { action(); return true; }, CancellationToken.None);
            });
            var invoked = false;
            async Task UseDocumentAsync()
            {
                using var lease = lifetime.Acquire();
                await scheduler.Enqueue(() => { invoked = true; return true; }, lifetime.DisposeToken);
            }
            var work = UseDocumentAsync();
            lifetime.RequestDispose();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => work.WaitAsync(Deadline, TestContext.Current.CancellationToken));
            await releaseScheduled.Task.WaitAsync(Deadline, TestContext.Current.CancellationToken);
            Assert.False(held.IsCompleted);
            Assert.False(lifetime.Completion.IsCompleted);
            Assert.False(invoked);
            resume.Set();
            await lifetime.Completion.WaitAsync(Deadline, TestContext.Current.CancellationToken);
            Assert.False(invoked);
        }
        finally { resume.Set(); await held.WaitAsync(Deadline, CancellationToken.None); }
    }

    [Fact]
    public async Task Release_failure_is_observable_to_the_async_disposer()
    {
        var scheduler = new PdfiumWorkScheduler();
        var lifetime = new PdfDocumentLifetime(() => throw new InvalidDataException("Native release failed."),
            action => scheduler.Enqueue(() => { action(); return true; }, CancellationToken.None));
        lifetime.RequestDispose();
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => lifetime.Completion.WaitAsync(Deadline, TestContext.Current.CancellationToken));
        Assert.Equal("Native release failed.", exception.Message);
    }

    [Fact]
    public async Task Real_document_async_release_is_idempotent_and_preserves_other_documents()
    {
        var document = await PdfDocument.LoadAsync(PdfFixture.ManyPages(8), cancellationToken: TestContext.Current.CancellationToken);
        await using var other = await PdfDocument.LoadAsync(PdfFixture.SinglePage("Still alive"), cancellationToken: TestContext.Current.CancellationToken);
        var render = document.RenderThumbnailAsync(0, 120, 160, cancellationToken: TestContext.Current.CancellationToken);
        var text = document.GetTextAsync(1, TestContext.Current.CancellationToken);
        var search = document.SearchAsync("Page", cancellationToken: TestContext.Current.CancellationToken);
        document.Dispose();
        foreach (var operation in new Task[] { render, text, search })
        {
            try { await operation.WaitAsync(Deadline, TestContext.Current.CancellationToken); }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
        }
        await document.DisposeAsync();
        await document.DisposeAsync();
        Assert.True(document.WhenDisposed.IsCompletedSuccessfully);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => document.GetTextAsync(0, TestContext.Current.CancellationToken));
        Assert.Contains("Still alive", (await other.GetTextAsync(0, TestContext.Current.CancellationToken)).Text);
    }
}
