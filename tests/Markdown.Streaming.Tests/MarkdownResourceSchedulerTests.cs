using System.Collections.Concurrent;
using NT.AvaloniaControl.Markdown;

namespace Markdown.Streaming.Tests;

public sealed class MarkdownResourceSchedulerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(0, 256)]
    [InlineData(-1, 256)]
    [InlineData(4, -1)]
    public void InvalidLimitsAreRejected(int concurrency, int queue)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MarkdownResourceScheduler(concurrency, queue));
    }

    [Fact]
    public async Task DefaultLimitAllowsOnlyFourActualCallsAndBoundsWaitingWork()
    {
        using var scheduler = new MarkdownResourceScheduler();
        var release = Signal();
        var fourStarted = Signal();
        var actual = 0;
        var maximum = 0;
        var entered = 0;
        var results = Enumerable.Range(0, 20).Select(index => Run<int>(scheduler, async _ =>
        {
            var concurrent = Interlocked.Increment(ref actual);
            SetMaximum(ref maximum, concurrent);
            if (Interlocked.Increment(ref entered) == 4) fourStarted.TrySetResult();
            try { await release.Task.ConfigureAwait(false); return index; }
            finally { Interlocked.Decrement(ref actual); }
        })).ToArray();
        try
        {
            await fourStarted.Task.WaitAsync(Timeout, TestToken);
            Assert.Equal(4, Volatile.Read(ref actual));
            Assert.Equal(4, scheduler.ActiveCount);
            Assert.Equal(16, scheduler.QueuedCount);
        }
        finally { release.TrySetResult(); }
        Assert.Equal(Enumerable.Range(0, 20), await Task.WhenAll(results).WaitAsync(Timeout, TestToken));
        Assert.InRange(maximum, 1, 4);
        await WaitForIdle(scheduler);
    }

    [Fact]
    public async Task DefaultQueueRejectsTheTwoHundredFiftySeventhWaitingRequest()
    {
        using var scheduler = new MarkdownResourceScheduler();
        var release = Signal();
        var requests = Enumerable.Range(0, 260).Select(index => Run<int>(scheduler, async _ => { await release.Task; return index; })).ToArray();
        try
        {
            Assert.Equal(4, scheduler.ActiveCount);
            Assert.Equal(256, scheduler.QueuedCount);
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Run<int>(scheduler, _ => ValueTask.FromResult(-1)));
            Assert.Contains("queue limit (256)", error.Message);
        }
        finally { release.TrySetResult(); }
        Assert.Equal(260, (await Task.WhenAll(requests).WaitAsync(Timeout, TestToken)).Length);
        await WaitForIdle(scheduler);
    }

    [Fact]
    public async Task SynchronousHostPrefixRunsInTheBackgroundAndCannotBlockRunAsync()
    {
        using var scheduler = new MarkdownResourceScheduler(1);
        using var release = new ManualResetEventSlim();
        var entered = Signal();
        var returned = Signal();
        var invocation = Task.Run(() =>
        {
            var result = Run<int>(scheduler, _ =>
            {
                entered.TrySetResult();
                if (!release.Wait(Timeout, TestToken)) throw new TimeoutException("Synchronous test service was not released.");
                return ValueTask.FromResult(42);
            });
            returned.TrySetResult();
            return result;
        }, TestToken);
        try
        {
            await returned.Task.WaitAsync(Timeout, TestToken);
            await entered.Task.WaitAsync(Timeout, TestToken);
            Assert.False(invocation.IsCompleted);
        }
        finally { release.Set(); }
        Assert.Equal(42, await invocation.WaitAsync(Timeout, TestToken));
        await WaitForIdle(scheduler);
    }

    [Fact]
    public async Task QueueIsFifoAndCancellationImmediatelyFreesItsBoundedCapacity()
    {
        using var scheduler = new MarkdownResourceScheduler(1, 2);
        using var cancellation = new CancellationTokenSource();
        var release = Signal();
        var entered = Signal();
        var order = new ConcurrentQueue<int>();
        var first = Run<int>(scheduler, async _ => { order.Enqueue(0); entered.TrySetResult(); await release.Task; return 0; });
        await entered.Task.WaitAsync(Timeout, TestToken);
        var second = Run<int>(scheduler, _ => { order.Enqueue(1); return ValueTask.FromResult(1); });
        var cancelled = Run<int>(scheduler, _ => { order.Enqueue(2); return ValueTask.FromResult(2); }, cancellation.Token);
        var overflow = Run<int>(scheduler, _ => ValueTask.FromResult(99));
        try
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => overflow);
            Assert.Contains("queue limit (2)", error.Message);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
            Assert.Equal(1, scheduler.QueuedCount);
            var replacement = Run<int>(scheduler, _ => { order.Enqueue(3); return ValueTask.FromResult(3); });
            Assert.Equal(2, scheduler.QueuedCount);
            release.TrySetResult();
            Assert.Equal(new[] { 0, 1, 3 }, await Task.WhenAll(first, second, replacement).WaitAsync(Timeout, TestToken));
            Assert.Equal(new[] { 0, 1, 3 }, order.ToArray());
        }
        finally { release.TrySetResult(); }
        await WaitForIdle(scheduler);
    }

    [Fact]
    public async Task ZeroQueueCapacityStillPermitsAvailableSlotsAndReportsOverflow()
    {
        using var scheduler = new MarkdownResourceScheduler(1, 0);
        var release = Signal();
        var first = Run<int>(scheduler, async _ => { await release.Task; return 1; });
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => Run<int>(scheduler, _ => ValueTask.FromResult(2)));
            Assert.Equal(0, scheduler.QueuedCount);
        }
        finally { release.TrySetResult(); }
        Assert.Equal(1, await first.WaitAsync(Timeout, TestToken));
        await WaitForIdle(scheduler);
        Assert.Equal(3, await Run<int>(scheduler, _ => ValueTask.FromResult(3)).WaitAsync(Timeout, TestToken));
    }

    [Fact]
    public async Task AlreadyCancelledRequestNeverInvokesTheHost()
    {
        using var scheduler = new MarkdownResourceScheduler();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var invoked = false;
        var request = Run<int>(scheduler, _ => { invoked = true; return ValueTask.FromResult(1); }, cancellation.Token);
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.False(invoked);
        Assert.Equal(0, scheduler.ActiveCount);
        Assert.Equal(0, scheduler.QueuedCount);
    }

    [Fact]
    public async Task IgnoredRunningCancellationKeepsItsSlotAndDisposesTheUnacceptedLease()
    {
        using var scheduler = new MarkdownResourceScheduler(1);
        using var cancellation = new CancellationTokenSource();
        var entered = Signal();
        var release = Signal();
        var lease = new DisposableProbe();
        var blockedStarted = false;
        var first = Run<DisposableProbe>(scheduler, async _ => { entered.TrySetResult(); await release.Task; return lease; }, cancellation.Token);
        await entered.Task.WaitAsync(Timeout, TestToken);
        cancellation.Cancel();
        var second = Run<int>(scheduler, _ => { blockedStarted = true; return ValueTask.FromResult(2); });
        try
        {
            Assert.False(first.IsCompleted);
            Assert.False(blockedStarted);
            Assert.Equal(1, scheduler.ActiveCount);
            Assert.Equal(1, scheduler.QueuedCount);
        }
        finally { release.TrySetResult(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.WaitAsync(Timeout, TestToken));
        Assert.Equal(2, await second.WaitAsync(Timeout, TestToken));
        Assert.Equal(1, lease.DisposeCount);
        await WaitForIdle(scheduler);
    }

    [Fact]
    public async Task EpochChangeRejectsOldQueueAndLateResultsWithoutFreeingNonCooperativeSlots()
    {
        using var scheduler = new MarkdownResourceScheduler(1);
        var entered = Signal();
        var release = Signal();
        var lease = new DisposableProbe();
        CancellationToken actualToken = default;
        var queuedInvocations = 0;
        var current = Run<DisposableProbe>(scheduler, async token => { actualToken = token; entered.TrySetResult(); await release.Task; return lease; });
        await entered.Task.WaitAsync(Timeout, TestToken);
        var oldQueued = Run<int>(scheduler, _ => { Interlocked.Increment(ref queuedInvocations); return ValueTask.FromResult(2); });
        scheduler.AdvanceEpoch();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => oldQueued);
        var fresh = Run<int>(scheduler, _ => ValueTask.FromResult(3));
        try
        {
            Assert.False(current.IsCompleted);
            Assert.Equal(1, scheduler.ActiveCount);
            Assert.Equal(1, scheduler.QueuedCount);
            await WaitUntil(() => actualToken.IsCancellationRequested);
            Assert.Equal(0, queuedInvocations);
        }
        finally { release.TrySetResult(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => current.WaitAsync(Timeout, TestToken));
        Assert.Equal(3, await fresh.WaitAsync(Timeout, TestToken));
        Assert.Equal(1, lease.DisposeCount);
        await WaitForIdle(scheduler);
    }

    [Fact]
    public async Task DisposeRejectsPendingAndNewRequestsButCountsRealRunningCallsUntilTheyFinish()
    {
        using var scheduler = new MarkdownResourceScheduler(1);
        var entered = Signal();
        var release = Signal();
        var lease = new DisposableProbe();
        var current = Run<DisposableProbe>(scheduler, async _ => { entered.TrySetResult(); await release.Task; return lease; });
        await entered.Task.WaitAsync(Timeout, TestToken);
        var queuedCalled = false;
        var pending = Run<int>(scheduler, _ => { queuedCalled = true; return ValueTask.FromResult(2); });
        scheduler.Dispose();
        scheduler.Dispose();
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => Run<int>(scheduler, _ => ValueTask.FromResult(3)));
            Assert.Throws<ObjectDisposedException>(scheduler.AdvanceEpoch);
            Assert.False(queuedCalled);
            Assert.False(current.IsCompleted);
            Assert.Equal(1, scheduler.ActiveCount);
            Assert.Equal(0, scheduler.QueuedCount);
        }
        finally { release.TrySetResult(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => current.WaitAsync(Timeout, TestToken));
        Assert.Equal(1, lease.DisposeCount);
        await WaitForIdle(scheduler);
    }

    [Fact]
    public async Task AcceptedLeaseRemainsCallerOwnedAcrossEpochAndDisposal()
    {
        using var scheduler = new MarkdownResourceScheduler();
        var lease = new DisposableProbe();
        Assert.Same(lease, await Run<DisposableProbe>(scheduler, _ => ValueTask.FromResult(lease)).WaitAsync(Timeout, TestToken));
        scheduler.AdvanceEpoch();
        scheduler.Dispose();
        Assert.Equal(0, lease.DisposeCount);
        lease.Dispose();
        Assert.Equal(1, lease.DisposeCount);
        await WaitForIdle(scheduler);
    }

    [Fact]
    public async Task ExplicitDiscardCallbackHandlesAnUnacceptedResultExactlyOnce()
    {
        using var scheduler = new MarkdownResourceScheduler(1);
        using var cancellation = new CancellationTokenSource();
        var entered = Signal();
        var release = Signal();
        var lease = new DisposableProbe();
        var discarded = 0;
        var current = Run<DisposableProbe>(scheduler, async _ => { entered.TrySetResult(); await release.Task; return lease; }, cancellation.Token,
            result => { Interlocked.Increment(ref discarded); result.Dispose(); });
        await entered.Task.WaitAsync(Timeout, TestToken);
        cancellation.Cancel();
        release.TrySetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => current.WaitAsync(Timeout, TestToken));
        Assert.Equal(1, discarded);
        Assert.Equal(1, lease.DisposeCount);
        await WaitForIdle(scheduler);
    }

    [Fact]
    public async Task HostFailurePropagatesAndAllowsTheNextQueuedCallToRun()
    {
        using var scheduler = new MarkdownResourceScheduler(1);
        var entered = Signal();
        var release = Signal();
        var failed = Run<int>(scheduler, async _ => { entered.TrySetResult(); await release.Task; throw new InvalidOperationException("host failure"); });
        await entered.Task.WaitAsync(Timeout, TestToken);
        var next = Run<int>(scheduler, _ => ValueTask.FromResult(42));
        release.TrySetResult();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => failed.WaitAsync(Timeout, TestToken));
        Assert.Equal("host failure", error.Message);
        Assert.Equal(42, await next.WaitAsync(Timeout, TestToken));
        await WaitForIdle(scheduler);
    }

    [Fact]
    public async Task CooperativeCancellationReleasesTheSlotOnlyAfterTheDelegateExits()
    {
        using var scheduler = new MarkdownResourceScheduler(1);
        using var cancellation = new CancellationTokenSource();
        var entered = Signal();
        var exited = 0;
        var current = Run<int>(scheduler, async token =>
        {
            entered.TrySetResult();
            try { await Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, token); return 1; }
            finally { Volatile.Write(ref exited, 1); }
        }, cancellation.Token);
        await entered.Task.WaitAsync(Timeout, TestToken);
        var next = Run<int>(scheduler, _ => ValueTask.FromResult(Volatile.Read(ref exited)));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => current.WaitAsync(Timeout, TestToken));
        Assert.Equal(1, await next.WaitAsync(Timeout, TestToken));
        await WaitForIdle(scheduler);
    }

    [Fact]
    public async Task ThrowingHostCancellationCallbackCannotStrandTheScheduler()
    {
        using var scheduler = new MarkdownResourceScheduler(1);
        var entered = Signal();
        var callbackEntered = Signal();
        var release = Signal();
        var current = Run<int>(scheduler, async token =>
        {
            using var registration = token.Register(() => { callbackEntered.TrySetResult(); throw new InvalidOperationException("callback failure"); });
            entered.TrySetResult();
            await release.Task;
            return 1;
        });
        await entered.Task.WaitAsync(Timeout, TestToken);
        scheduler.AdvanceEpoch();
        var next = Run<int>(scheduler, _ => ValueTask.FromResult(2));
        try { await callbackEntered.Task.WaitAsync(Timeout, TestToken); Assert.Equal(1, scheduler.ActiveCount); }
        finally { release.TrySetResult(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => current.WaitAsync(Timeout, TestToken));
        Assert.Equal(2, await next.WaitAsync(Timeout, TestToken));
        await WaitForIdle(scheduler);
    }

    [Fact]
    public async Task ExternalCancellationDoesNotRunThrowingHostCallbacksOnItsCallingThread()
    {
        using var scheduler = new MarkdownResourceScheduler(1);
        using var cancellation = new CancellationTokenSource();
        var entered = Signal();
        var callbackEntered = Signal();
        var release = Signal();
        var current = Run<int>(scheduler, async token =>
        {
            using var registration = token.Register(() => { callbackEntered.TrySetResult(); throw new InvalidOperationException("host callback failure"); });
            entered.TrySetResult();
            await release.Task;
            return 1;
        }, cancellation.Token);
        await entered.Task.WaitAsync(Timeout, TestToken);
        try
        {
            cancellation.Cancel();
            await callbackEntered.Task.WaitAsync(Timeout, TestToken);
            Assert.Equal(1, scheduler.ActiveCount);
            Assert.False(current.IsCompleted);
        }
        finally { release.TrySetResult(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => current.WaitAsync(Timeout, TestToken));
        await WaitForIdle(scheduler);
    }

    [Fact]
    public async Task ConcurrentCancellationAndEpochChangeNeverInvokeTheRemovedQueueOrExceedTheLimit()
    {
        using var scheduler = new MarkdownResourceScheduler(4, 256);
        var cancellations = Enumerable.Range(0, 200).Select(_ => new CancellationTokenSource()).ToArray();
        var release = Signal();
        var fourEntered = Signal();
        var actual = 0;
        var maximum = 0;
        var entered = 0;
        var requests = cancellations.Select(cancellation => Run<int>(scheduler, async _ =>
        {
            var concurrent = Interlocked.Increment(ref actual);
            SetMaximum(ref maximum, concurrent);
            if (Interlocked.Increment(ref entered) == 4) fourEntered.TrySetResult();
            try { await release.Task; return 1; }
            finally { Interlocked.Decrement(ref actual); }
        }, cancellation.Token)).ToArray();
        try
        {
            await fourEntered.Task.WaitAsync(Timeout, TestToken);
            await Task.WhenAll(cancellations.Select(cancellation => Task.Run(cancellation.Cancel, TestToken)).Append(Task.Run(scheduler.AdvanceEpoch, TestToken))).WaitAsync(Timeout, TestToken);
            Assert.Equal(4, scheduler.ActiveCount);
            Assert.Equal(0, scheduler.QueuedCount);
            Assert.Equal(4, entered);
            release.TrySetResult();
            foreach (var request in requests) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request.WaitAsync(Timeout, TestToken));
            Assert.Equal(4, maximum);
            await WaitForIdle(scheduler);
        }
        finally { release.TrySetResult(); foreach (var cancellation in cancellations) cancellation.Dispose(); }
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static Task<T> Run<T>(MarkdownResourceScheduler scheduler, Func<CancellationToken, ValueTask<T>> action) => scheduler.RunAsync(action, TestToken);
    private static Task<T> Run<T>(MarkdownResourceScheduler scheduler, Func<CancellationToken, ValueTask<T>> action, CancellationToken token) => scheduler.RunAsync(action, token);
    private static Task<T> Run<T>(MarkdownResourceScheduler scheduler, Func<CancellationToken, ValueTask<T>> action, CancellationToken token, Action<T> discard) => scheduler.RunAsync(action, token, discard);
    private static Task WaitForIdle(MarkdownResourceScheduler scheduler) => WaitUntil(() => scheduler.ActiveCount == 0 && scheduler.QueuedCount == 0);
    private static async Task WaitUntil(Func<bool> predicate)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestToken);
        timeout.CancelAfter(Timeout);
        while (!predicate()) await Task.Delay(1, timeout.Token);
    }
    private static void SetMaximum(ref int maximum, int candidate)
    {
        var observed = Volatile.Read(ref maximum);
        while (candidate > observed)
        {
            var previous = Interlocked.CompareExchange(ref maximum, candidate, observed);
            if (previous == observed) return;
            observed = previous;
        }
    }
    private sealed class DisposableProbe : IDisposable
    {
        private int _disposed;
        public int DisposeCount => Volatile.Read(ref _disposed);
        public void Dispose() => Interlocked.Increment(ref _disposed);
    }
}
