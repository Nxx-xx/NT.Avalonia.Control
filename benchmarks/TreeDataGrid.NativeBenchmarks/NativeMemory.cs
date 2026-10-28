using System.Diagnostics;

namespace TreeDataGridNativeBenchmarks;

internal sealed record GenerationReading(long sizeBeforeBytes, long fragmentationBeforeBytes, long sizeAfterBytes, long fragmentationAfterBytes);
internal sealed record GcReading(long index, int generation, bool concurrent, bool compacted, long heapSizeBytes,
    long fragmentedBytes, long liveBytes, long totalCommittedBytes, GenerationReading[] generations);
internal sealed record ResourceReading(int id, string phase, long ticks, long sampleEndTicks, long rawManagedBytes,
    long? managedBytes, string managedEstimateStatus, GcReading gc, int[] gcCollectionsBefore, int[] gcCollections,
    bool gcOccurredDuringSample, long privateBytes, long workingSetBytes, int handles, long processPeakWorkingSetBytes,
    double cpuMilliseconds, long totalAllocatedBytes, bool crossChecksValid, string[] crossCheckErrors,
    string gcSnapshotKind, GcReading lastAnyGc, string[] lastAnyGcCrossCheckErrors);

internal static class NativeMemory
{
    internal static ResourceReading Capture(int id, string phase, bool blockingGcReference = false)
    {
        var ticks = Stopwatch.GetTimestamp();
        var before = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
        var raw = GC.GetTotalMemory(false);
        var lastAny = ReadGc(GC.GetGCMemoryInfo());
        var gc = blockingGcReference ? ReadGc(GC.GetGCMemoryInfo(GCKind.FullBlocking)) : lastAny;
        using var process = Process.GetCurrentProcess(); process.Refresh();
        var privateBytes = process.PrivateMemorySize64; var workingBytes = process.WorkingSet64; var handles = process.HandleCount;
        var peakBytes = process.PeakWorkingSet64; var cpuMs = process.TotalProcessorTime.TotalMilliseconds;
        var allocated = GC.GetTotalAllocatedBytes(false);
        var after = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
        var end = Stopwatch.GetTimestamp();
        var errors = ValidateGc(gc).ToList();
        if (privateBytes <= 0 || workingBytes <= 0 || handles <= 0 || allocated < 0 || !double.IsFinite(cpuMs) || cpuMs < 0)
            errors.Add("Invalid process resource counters.");
        return new(id, phase, ticks, end, raw, raw >= 0 ? raw : null,
            raw >= 0 ? "available" : "unavailable-negative-runtime-value", gc, before, after, !before.SequenceEqual(after),
            privateBytes, workingBytes, handles, peakBytes, cpuMs, allocated, errors.Count == 0, errors.ToArray(),
            blockingGcReference ? "FullBlocking" : "Any", lastAny, ValidateGc(lastAny));
    }
    private static GcReading ReadGc(GCMemoryInfo info)
    {
        var generations = new List<GenerationReading>();
        foreach (var g in info.GenerationInfo) generations.Add(new(g.SizeBeforeBytes, g.FragmentationBeforeBytes, g.SizeAfterBytes, g.FragmentationAfterBytes));
        return new GcReading(info.Index, info.Generation, info.Concurrent, info.Compacted, info.HeapSizeBytes,
            info.FragmentedBytes, info.HeapSizeBytes - info.FragmentedBytes, info.TotalCommittedBytes, generations.ToArray());
    }
    private static string[] ValidateGc(GcReading gc)
    {
        var errors = new List<string>();
        if (gc.index <= 0 || gc.heapSizeBytes < 0 || gc.fragmentedBytes < 0 || gc.fragmentedBytes > gc.heapSizeBytes || gc.totalCommittedBytes < gc.heapSizeBytes)
            errors.Add("Invalid last-GC heap/fragmentation/commit values.");
        if (gc.generations.Length != 5 || gc.generations.Any(g => g.sizeBeforeBytes < 0 || g.sizeAfterBytes < 0 || g.fragmentationBeforeBytes < 0 || g.fragmentationAfterBytes < 0
            || g.fragmentationBeforeBytes > g.sizeBeforeBytes || g.fragmentationAfterBytes > g.sizeAfterBytes))
            errors.Add("Invalid generation size/fragmentation values.");
        if (gc.generations.Sum(g => g.sizeAfterBytes) != gc.heapSizeBytes || gc.generations.Sum(g => g.fragmentationAfterBytes) != gc.fragmentedBytes)
            errors.Add("Last-GC generation sums differ from the heap snapshot.");
        return errors.ToArray();
    }
}
