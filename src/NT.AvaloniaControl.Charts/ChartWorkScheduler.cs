using Avalonia.Threading;

namespace NT.AvaloniaControl.Charts;

/// <summary>Process-wide logical accounting for chart-local resources and admitted numeric background payloads.</summary>
/// <remarks>These estimates exclude shared source/index caches, caller objects, transient drawing, native memory and GPU allocations.</remarks>
public sealed record ChartWorkBudgetInfo(long ResourceCacheBudgetBytes, long EstimatedResourceCacheBytes, int ResourceEntryCount,
    long BackgroundPayloadBudgetBytes, long EstimatedBackgroundPayloadBytes, int MaximumBackgroundJobs,
    int ReservedBackgroundJobs, int QueuedBackgroundJobs, int RunningBackgroundJobs, int PendingRefreshCount,
    long RefreshRequests, long RefreshDispatches, long CoalescedRefreshes, long RejectedBackgroundJobs,
    long CompletedBackgroundJobs, long CancelledBackgroundJobs, long FailedBackgroundJobs, long ResourceEvictions)
{
    public int ModelVersion => 1;
    public int MaximumBackgroundWorkers => ChartWorkScheduler.MaximumBackgroundWorkers;
    public bool NativeMemoryIncluded => false;
    public bool SharedIndexCachesIncluded => false;
}

/// <summary>Shares chart refresh turns, local cache estimates and bounded numeric background work across charts on the Avalonia UI thread.</summary>
/// <remarks>Configuration and diagnostics require UI-thread access. Budgets are admission weights, not measured process-memory limits.
/// Worker delegates are internal and accept only captured numeric data; rendering and publication remain on the UI thread.</remarks>
public static class ChartWorkScheduler
{
    public const int MaximumBackgroundWorkers = 2;
    private static long _resourceBudget = 512L * 1024 * 1024, _payloadBudget = 64L * 1024 * 1024;
    private static int _maximumJobs = 16;
    private static readonly LinkedList<ProcessResource> Resources = [];
    private static long _resourceBytes, _payloadBytes, _resourceEvictions;
    private static readonly LinkedList<CartesianChart> RefreshQueue = [];
    private static readonly Dictionary<CartesianChart, LinkedListNode<CartesianChart>> RefreshNodes = [];
    private static bool _refreshPosted;
    private static long _refreshRequests, _refreshDispatches, _coalescedRefreshes;
    private static readonly HashSet<ChartWorkLease> Leases = [];
    private static readonly LinkedList<ChartWorkLease> WorkQueue = [];
    private static int _running;
    private static long _rejected, _completed, _cancelled, _failed;

    /// <summary>Maximum sum of retained chart-local five-layer resource estimates. Lowering this value immediately evicts least-recently-used entries.</summary>
    public static long ResourceCacheBudgetBytes
    {
        get { Verify(); return _resourceBudget; }
        set { Verify(); ArgumentOutOfRangeException.ThrowIfNegative(value); _resourceBudget = value; EvictResources(0); }
    }
    /// <summary>Maximum admitted numeric input/output estimates, including reservations, queued work and completed results awaiting publication/release.</summary>
    public static long BackgroundPayloadBudgetBytes
    {
        get { Verify(); return _payloadBudget; }
        set { Verify(); ArgumentOutOfRangeException.ThrowIfNegative(value); _payloadBudget = value; TrimWorkAdmission(); }
    }
    /// <summary>Maximum admitted background reservations. At most two execute concurrently; zero disables admission.</summary>
    public static int MaximumBackgroundJobs
    {
        get { Verify(); return _maximumJobs; }
        set { Verify(); ArgumentOutOfRangeException.ThrowIfNegative(value); _maximumJobs = value; TrimWorkAdmission(); }
    }

    /// <summary>Returns scalar logical-accounting counters without traversing any chart data.</summary>
    public static ChartWorkBudgetInfo GetBudgetInfo()
    {
        Verify();
        return new(_resourceBudget, _resourceBytes, Resources.Count, _payloadBudget, _payloadBytes, _maximumJobs,
            Leases.Count, WorkQueue.Count, _running, RefreshQueue.Count, _refreshRequests, _refreshDispatches,
            _coalescedRefreshes, _rejected, _completed, _cancelled, _failed, _resourceEvictions);
    }

    /// <summary>Releases all registered chart-local cache entries. Current scenes, source data, viewports and selection remain intact.</summary>
    public static void TrimResourceCaches()
    {
        Verify();
        while (Resources.First is { } first) Evict(first);
    }

    private static void Verify() => Dispatcher.UIThread.VerifyAccess();

    internal sealed class ProcessResource(long bytes, Action evict)
    {
        internal long Bytes = bytes;
        internal Action? Evict = evict;
        internal LinkedListNode<ProcessResource>? Node;
    }

    internal static ProcessResource? RetainResource(long bytes, Action evict)
    {
        Verify();
        if (bytes > _resourceBudget) return null;
        EvictResources(bytes);
        var entry = new ProcessResource(bytes, evict);
        entry.Node = Resources.AddLast(entry); _resourceBytes += bytes;
        return entry;
    }

    internal static bool ResizeResource(ProcessResource entry, long bytes)
    {
        Verify();
        if (entry.Node is null || bytes > _resourceBudget) return false;
        // Keep the resized entry most recent while retiring older entries.
        _resourceBytes -= entry.Bytes; entry.Bytes = bytes;
        Resources.Remove(entry.Node); Resources.AddLast(entry.Node);
        EvictResources(bytes);
        _resourceBytes += bytes;
        return true;
    }

    internal static void TouchResource(ProcessResource entry)
    {
        if (entry.Node is not { } node) return;
        Resources.Remove(node); Resources.AddLast(node);
    }

    internal static void ReleaseResource(ProcessResource? entry)
    {
        if (entry?.Node is not { } node) return;
        Resources.Remove(node); _resourceBytes -= entry.Bytes; entry.Node = null; entry.Evict = null;
    }

    private static void EvictResources(long incoming)
    {
        while (_resourceBytes > _resourceBudget - incoming && Resources.First is { } first) Evict(first);
    }

    private static void Evict(LinkedListNode<ProcessResource> node)
    {
        var release = node.Value.Evict; ReleaseResource(node.Value); _resourceEvictions++; release?.Invoke();
    }

    internal static void RequestRefresh(CartesianChart owner)
    {
        Verify(); _refreshRequests++;
        if (RefreshNodes.ContainsKey(owner)) { _coalescedRefreshes++; owner.SchedulingRefreshCoalesced(); return; }
        if (!owner.CanRunScheduledWork) return;
        RefreshNodes.Add(owner, RefreshQueue.AddLast(owner));
        if (_refreshPosted) return;
        _refreshPosted = true; Dispatcher.UIThread.Post(DispatchRefresh, DispatcherPriority.Render);
    }

    internal static bool IsRefreshPending(CartesianChart owner) => RefreshNodes.ContainsKey(owner);

    private static void DispatchRefresh()
    {
        _refreshPosted = false;
        if (RefreshQueue.First is not { } first) return;
        var owner = first.Value; RefreshQueue.RemoveFirst(); RefreshNodes.Remove(owner);
        if (owner.CanRunScheduledWork) { _refreshDispatches++; owner.DispatchScheduledRefresh(); }
        if (RefreshQueue.Count == 0 || _refreshPosted) return;
        _refreshPosted = true; Dispatcher.UIThread.Post(DispatchRefresh, DispatcherPriority.Render);
    }

    internal static bool TryReserve(CartesianChart owner, long estimatedBytes, out ChartWorkLease? lease)
    {
        Verify(); ArgumentOutOfRangeException.ThrowIfNegative(estimatedBytes);
        // Reservation precedes numeric capture, so rejected work never allocates its input payload.
        if (!owner.CanRunScheduledWork || estimatedBytes > _payloadBudget - _payloadBytes || Leases.Count >= _maximumJobs ||
            Leases.Any(existing => !existing.IsDisposed && ReferenceEquals(existing.Owner, owner)))
        { _rejected++; lease = null; return false; }
        lease = new ChartWorkLease(owner, estimatedBytes); Leases.Add(lease); _payloadBytes += estimatedBytes;
        return true;
    }

    internal static void Enqueue(ChartWorkLease lease)
    {
        Verify();
        lease.QueueNode = WorkQueue.AddLast(lease); StartWorkers();
    }

    private static void StartWorkers()
    {
        while (_running < MaximumBackgroundWorkers && WorkQueue.First is { } first)
        {
            var lease = first.Value; WorkQueue.RemoveFirst(); lease.QueueNode = null;
            if (lease.IsDisposed) { ReleaseLease(lease); continue; }
            lease.IsRunning = true; _running++;
            var work = lease.TakeWork();
            // Exactly one Task per running slot. Waiting reservations do not start tasks or copy payloads.
            _ = Task.Run(() =>
            {
                object? result = null; Exception? error = null;
                try { lease.Token.ThrowIfCancellationRequested(); result = work(lease.Token); }
                catch (Exception exception) { error = exception; }
                Dispatcher.UIThread.Post(() => Complete(lease, result, error));
            });
        }
    }

    private static void Complete(ChartWorkLease lease, object? result, Exception? error)
    {
        lease.IsRunning = false; _running--;
        if (lease.IsDisposed || lease.Token.IsCancellationRequested || error is OperationCanceledException)
        { lease.CompleteCancelled(); if (!lease.IsDisposed) lease.Dispose(); }
        else if (error is not null) { _failed++; lease.CompleteFaulted(error); }
        else { _completed++; lease.CompleteResult(result); }
        // Successful/failed results retain their reservation until the UI caller publishes/rejects and disposes it.
        if (lease.IsDisposed) ReleaseLease(lease);
        StartWorkers();
    }

    internal static void DisposeLease(ChartWorkLease lease)
    {
        Verify(); _cancelled += lease.WasCompleted ? 0 : 1;
        if (lease.QueueNode is { } node) { WorkQueue.Remove(node); lease.QueueNode = null; }
        lease.CompleteCancelled();
        if (!lease.IsRunning) ReleaseLease(lease);
        StartWorkers();
    }

    private static void ReleaseLease(ChartWorkLease lease)
    {
        if (!Leases.Remove(lease)) return;
        _payloadBytes -= lease.EstimatedBytes; lease.Release();
    }

    private static void TrimWorkAdmission()
    {
        // Running payloads remain accounted until cooperative cancellation finishes; admission stays blocked while over budget.
        foreach (var lease in Leases.Reverse().ToArray())
        {
            if (_payloadBytes <= _payloadBudget && Leases.Count <= _maximumJobs) break;
            lease.Dispose();
        }
    }

    internal static void Cancel(CartesianChart owner)
    {
        Verify();
        if (RefreshNodes.Remove(owner, out var node)) RefreshQueue.Remove(node);
        foreach (var lease in Leases.Where(lease => ReferenceEquals(lease.Owner, owner)).ToArray()) lease.Dispose();
    }
}

/// <summary>Internal admission handle. Caller captures numeric data only after obtaining it and disposes after UI publication/rejection.</summary>
internal sealed class ChartWorkLease(CartesianChart owner, long estimatedBytes) : IDisposable
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly TaskCompletionSource<object?> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationTokenRegistration _registration;
    private Func<CancellationToken, object?>? _work;
    internal CartesianChart? Owner { get; private set; } = owner;
    internal long EstimatedBytes { get; } = estimatedBytes;
    internal bool IsDisposed { get; private set; }
    internal bool IsRunning { get; set; }
    internal bool WasCompleted { get; private set; }
    internal LinkedListNode<ChartWorkLease>? QueueNode;
    internal CancellationToken Token => _cancellation.Token;

    internal Task<T> RunAsync<T>(Func<CancellationToken, T> work, CancellationToken cancellationToken = default)
    {
        Dispatcher.UIThread.VerifyAccess(); ArgumentNullException.ThrowIfNull(work);
        if (IsDisposed || _work is not null || QueueNode is not null || IsRunning || WasCompleted)
            throw new InvalidOperationException("A background reservation can be started once before disposal.");
        _work = token => work(token);
        if (cancellationToken.CanBeCanceled)
            _registration = cancellationToken.Register(() => Dispatcher.UIThread.Post(Dispose));
        if (cancellationToken.IsCancellationRequested) { Dispose(); return Task.FromCanceled<T>(cancellationToken); }
        ChartWorkScheduler.Enqueue(this);
        return ConvertResult<T>(_completion.Task);
    }

    private static async Task<T> ConvertResult<T>(Task<object?> task) => (T)(await task.ConfigureAwait(false))!;
    internal Func<CancellationToken, object?> TakeWork() { var work = _work!; _work = null; return work; }
    internal void CompleteResult(object? result) { WasCompleted = true; _completion.TrySetResult(result); }
    internal void CompleteFaulted(Exception error) { WasCompleted = true; _completion.TrySetException(error); }
    internal void CompleteCancelled() => _completion.TrySetCanceled();
    internal void Release() { _work = null; Owner = null; _registration.Dispose(); _cancellation.Dispose(); }
    public void Dispose()
    {
        Dispatcher.UIThread.VerifyAccess();
        if (IsDisposed) return;
        IsDisposed = true; Owner = null; _cancellation.Cancel(); _work = null;
        ChartWorkScheduler.DisposeLease(this);
    }
}
