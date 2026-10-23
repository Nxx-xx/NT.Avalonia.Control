namespace NT.AvaloniaControl.PdfViewer;

/// <summary>
/// Process-wide cache budgeting shared by every live <see cref="PdfViewer"/>. Each viewer
/// keeps its own page / thumbnail / text budgets; when a global quota is set, a viewer's
/// combined budget is additionally clamped to the quota minus what the other viewers retain.
/// Pools trim their own least-recently-used entries, so one viewer never evicts another's
/// pages — the quota only tightens each viewer's own retention. UI-thread affinity matches
/// the controls whose caches this class accounts.
/// </summary>
internal static class PdfCacheGovernor
{
    /// <summary>
    /// Retention hysteresis: pools trim only after crossing their budget (the high-water
    /// mark) and then shed down to this fraction of it (the low-water mark), so a page that
    /// just fell out of the viewport is not evicted only to be decoded again immediately.
    /// </summary>
    internal const double TrimWaterRatio = 0.85;

    private static readonly object s_gate = new();
    private static readonly List<WeakReference<PdfViewer>> s_viewers = [];
    private static long s_globalBudget;

    /// <summary>Bytes all live viewers currently retain in their budgeted pools.</summary>
    internal static long GlobalCacheBytes
    {
        get
        {
            lock (s_gate)
            {
                Compact();
                long total = 0;
                foreach (var weak in s_viewers)
                    if (weak.TryGetTarget(out var viewer)) total += viewer.BudgetedCacheBytes;
                return total;
            }
        }
    }

    /// <summary>0 disables the quota; otherwise the sum of every viewer's budgeted pools.</summary>
    internal static long GlobalCacheBudget
    {
        get => Interlocked.Read(ref s_globalBudget);
        set
        {
            Volatile.Write(ref s_globalBudget, Math.Max(0, value));
            TrimAll();
        }
    }

    /// <summary>
    /// The combined ceiling for one viewer: its own three budgets, clamped by the global
    /// quota after every other viewer's retained bytes. Pools that are not over their share
    /// keep their individual budgets; see <see cref="PdfViewer.EffectivePoolBudget"/>.
    /// </summary>
    internal static long EffectiveCombinedBudget(long ownCombinedBudget, long ownRetainedBytes)
    {
        var quota = GlobalCacheBudget;
        if (quota <= 0) return ownCombinedBudget;
        var others = Math.Max(0, GlobalCacheBytes - ownRetainedBytes);
        return Math.Min(ownCombinedBudget, Math.Max(0, quota - others));
    }

    internal static void Register(PdfViewer viewer)
    {
        lock (s_gate)
        {
            Compact();
            s_viewers.Add(new WeakReference<PdfViewer>(viewer));
        }
    }

    internal static void Unregister(PdfViewer viewer)
    {
        lock (s_gate)
        {
            s_viewers.RemoveAll(weak => !weak.TryGetTarget(out var target) || ReferenceEquals(target, viewer));
        }
    }

    private static void TrimAll()
    {
        PdfViewer[] targets;
        lock (s_gate)
        {
            Compact();
            targets = s_viewers.Select(weak => weak.TryGetTarget(out var viewer) ? viewer : null)
                .OfType<PdfViewer>().ToArray();
        }
        foreach (var viewer in targets) viewer.TrimCachesToBudgets();
    }

    private static void Compact() => s_viewers.RemoveAll(weak => !weak.TryGetTarget(out _));

    /// <summary>Estimated retained size of an extracted text page: per-character records plus strings.</summary>
    internal static long EstimateTextBytes(PdfTextPage page)
    {
        long bytes = 96 + page.Text.Length * 2L;
        foreach (var character in page.Characters) bytes += 96 + character.Text.Length * 2L;
        return bytes;
    }
}
