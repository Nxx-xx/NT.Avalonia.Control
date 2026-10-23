using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>Snapshot of this chart's local resource-cache accounting. Values are estimates, not measured managed, native or GPU allocations.</summary>
/// <remarks>Model 1 counts retained drawing payload weights, text UTF-16/glyph weights and fixed cache-entry/pen weights.
/// Shared source/context caches (including prepared shaped paths and point/comparison indices), caller objects,
/// font-manager caches, native geometry and GPU allocations are excluded. This is not a total-memory budget.</remarks>
public sealed record ChartResourceCacheInfo(long BudgetBytes, long EstimatedBytes,
    long SeriesDrawingBytes, long AxisTextBytes, long InteractionTextBytes, long AxisPenBytes, long SegmentGeometryBytes,
    int SeriesDrawingCount, int AxisTextCount, int InteractionTextCount, int AxisPenCount, int SegmentGeometryCount, int SegmentProjectionCount,
    long CacheHits, long CacheMisses, long EvictionCount, long BypassCount, long TrimCount)
{
    public int ModelVersion => 1;
    public bool NativeMemoryIncluded => false;
    public bool SharedIndexCachesIncluded => false;
}

public sealed partial class CartesianChart
{
    public static readonly StyledProperty<long> ResourceCacheBudgetBytesProperty =
        AvaloniaProperty.Register<CartesianChart, long>(nameof(ResourceCacheBudgetBytes), 128L * 1024 * 1024,
            validate: value => value >= 0);

    /// <summary>Maximum local cache estimate under accounting model 1. Zero disables local drawing, text, axis-pen and segment-geometry retention.</summary>
    /// <remarks>This is cache-admission accounting, not a bound on process memory, native/GPU allocations, shared source/context caches or transient drawing work.</remarks>
    public long ResourceCacheBudgetBytes { get => GetValue(ResourceCacheBudgetBytesProperty); set => SetValue(ResourceCacheBudgetBytesProperty, value); }

    private enum ResourceCacheKind { SeriesDrawing, AxisText, InteractionText, AxisPen, SegmentGeometry }
    private readonly record struct ResourceCacheKey(ResourceCacheKind Kind, object Key);
    private sealed record ResourceCacheEntry(ResourceCacheKey Key, long Bytes, Action Release, ChartWorkScheduler.ProcessResource ProcessEntry);
    private readonly Dictionary<ResourceCacheKey, LinkedListNode<ResourceCacheEntry>> _resourceEntries = [];
    private readonly LinkedList<ResourceCacheEntry> _resourceLru = [];
    private long _resourceEstimatedBytes, _resourceCacheHits, _resourceCacheMisses, _resourceEvictions, _resourceBypasses, _resourceTrims;

    /// <summary>Returns UI-thread local-cache estimates and cumulative admission/reuse counters without enumerating data.</summary>
    public ChartResourceCacheInfo GetResourceCacheInfo()
    {
        VerifyAccess();
        var bytes = new long[5]; var counts = new int[5];
        foreach (var entry in _resourceLru) { bytes[(int)entry.Key.Kind] += entry.Bytes; counts[(int)entry.Key.Kind]++; }
        return new(ResourceCacheBudgetBytes, _resourceEstimatedBytes, bytes[0], bytes[1], bytes[2], bytes[3], bytes[4],
            counts[0], counts[1], counts[2], counts[3], counts[4], _segmentCaches.Values.Sum(value => value.ProjectedVertices?.Count ?? 0),
            _resourceCacheHits, _resourceCacheMisses, _resourceEvictions, _resourceBypasses, _resourceTrims);
    }

    /// <summary>Releases local retained drawings, axis/interaction text, axis pens and segment geometry. Data, view, selection and shared indices remain available.</summary>
    public void TrimResourceCaches()
    {
        VerifyAccess(); _resourceTrims++;
        ReleaseResourceCaches(); RequestRender();
    }

    private void ReleaseResourceCaches()
    {
        while (_resourceLru.First is { } first) RemoveResourceEntry(first, false);
        // Scene identity is kept separate from resources; the next draw can
        // rebuild its resources without preparing or copying the source again.
        ReleaseSeriesDrawingCache(); ReleaseAxisTextCaches(); ReleaseInteractionCaches(); ReleaseAxisPenCache(); ReleaseSegmentCaches();
    }

    private bool ResourcesPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property != ResourceCacheBudgetBytesProperty) return false;
        while (_resourceEstimatedBytes > ResourceCacheBudgetBytes && _resourceLru.First is { } first) RemoveResourceEntry(first, true);
        RequestRender();
        return true;
    }

    private bool TryRetainResource(ResourceCacheKind kind, object key, long bytes, Action release)
    {
        _resourceCacheMisses++;
        bytes = Math.Max(1, bytes);
        if (!CanRunScheduledWork || bytes > ResourceCacheBudgetBytes)
        { _resourceBypasses++; return false; }
        var resourceKey = new ResourceCacheKey(kind, key);
        if (_resourceEntries.TryGetValue(resourceKey, out var previous)) RemoveResourceEntry(previous, false);
        while (bytes > ResourceCacheBudgetBytes - _resourceEstimatedBytes && _resourceLru.First is { } first) RemoveResourceEntry(first, true);
        var processEntry = ChartWorkScheduler.RetainResource(bytes, () =>
        {
            if (_resourceEntries.TryGetValue(resourceKey, out var owned)) RemoveResourceEntry(owned, true);
        });
        if (processEntry is null) { _resourceBypasses++; return false; }
        var node = _resourceLru.AddLast(new ResourceCacheEntry(resourceKey, bytes, release, processEntry));
        _resourceEntries.Add(resourceKey, node); _resourceEstimatedBytes += bytes;
        return true;
    }

    private void TouchResource(ResourceCacheKind kind, object key)
    {
        if (!_resourceEntries.TryGetValue(new(kind, key), out var node)) return;
        _resourceCacheHits++;
        _resourceLru.Remove(node); _resourceLru.AddLast(node);
        ChartWorkScheduler.TouchResource(node.Value.ProcessEntry);
    }

    private bool TryResizeResource(ResourceCacheKind kind, object key, long bytes)
    {
        if (!_resourceEntries.TryGetValue(new(kind, key), out var node)) return false;
        if (bytes > ResourceCacheBudgetBytes)
        { RemoveResourceEntry(node, true); return false; }
        if (!ChartWorkScheduler.ResizeResource(node.Value.ProcessEntry, bytes))
        { RemoveResourceEntry(node, true); return false; }
        var previous = node.Value.Bytes;
        _resourceEstimatedBytes -= previous;
        node.Value = node.Value with { Bytes = bytes };
        _resourceLru.Remove(node); _resourceLru.AddLast(node);
        while (bytes > ResourceCacheBudgetBytes - _resourceEstimatedBytes && _resourceLru.First is { } first && !ReferenceEquals(first, node))
            RemoveResourceEntry(first, true);
        _resourceEstimatedBytes += bytes;
        return true;
    }

    private void ReleaseResourceKind(ResourceCacheKind kind)
    {
        var node = _resourceLru.First;
        while (node is not null)
        {
            var next = node.Next;
            if (node.Value.Key.Kind == kind) RemoveResourceEntry(node, false);
            node = next;
        }
    }

    private void EvictResource(ResourceCacheKind kind, object key)
    {
        if (_resourceEntries.TryGetValue(new(kind, key), out var node)) RemoveResourceEntry(node, true);
    }

    private void RemoveResourceEntry(LinkedListNode<ResourceCacheEntry> node, bool eviction)
    {
        _resourceLru.Remove(node); _resourceEntries.Remove(node.Value.Key); _resourceEstimatedBytes -= node.Value.Bytes;
        ChartWorkScheduler.ReleaseResource(node.Value.ProcessEntry);
        if (eviction) _resourceEvictions++;
        node.Value.Release();
    }

    // Include UTF-16 and a fixed per-character glyph/layout allowance. Opaque
    // platform layouts/fonts are deliberately not represented as exact bytes.
    private static long EstimateTextResource(string text) => 512L + text.Length * 66L;

    private readonly record struct PaintReference(object? Value)
    {
        public bool Equals(PaintReference other) => ReferenceEquals(Value, other.Value);
        public override int GetHashCode() => Value is null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Value);
    }
    private readonly record struct AxisPenCacheKey(PaintReference Brush, double Thickness, PaintReference Dash,
        PenLineCap Cap, PenLineJoin Join);
    private readonly Dictionary<AxisPenCacheKey, Pen> _axisPens = [];
    private readonly LinkedList<AxisPenCacheKey> _axisPenKeys = [];
    private long _axisPenBuildCount, _axisPenReuseCount;

    private Pen CachedAxisPen(IBrush? brush, double thickness, IDashStyle? dash = null,
        PenLineCap cap = PenLineCap.Flat, PenLineJoin join = PenLineJoin.Miter)
    {
        var key = new AxisPenCacheKey(new(brush), thickness, new(dash), cap, join);
        if (_attached && _axisPens.TryGetValue(key, out var pen))
        { _axisPenReuseCount++; TouchResource(ResourceCacheKind.AxisPen, key); return pen; }
        _axisPenBuildCount++;
        pen = new Pen(brush, thickness, dash, cap, join);
        if (_attached && TryRetainResource(ResourceCacheKind.AxisPen, key, 512, () => { _axisPens.Remove(key); _axisPenKeys.Remove(key); }))
        {
            while (_axisPens.Count >= 64 && _axisPenKeys.First is { } first) EvictResource(ResourceCacheKind.AxisPen, first.Value);
            _axisPens.Add(key, pen); _axisPenKeys.AddLast(key);
        }
        return pen;
    }

    private void ReleaseAxisPenCache()
    {
        ReleaseResourceKind(ResourceCacheKind.AxisPen); _axisPens.Clear(); _axisPenKeys.Clear();
    }
}
