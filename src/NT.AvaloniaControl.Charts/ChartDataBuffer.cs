using System.Collections;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Threading;

namespace NT.AvaloniaControl.Charts;

/// <summary>A UI-thread data source whose immutable pages are shared by chart snapshots.</summary>
/// <remarks>When a mutation exceeds Capacity, the oldest rows are evicted, including newly inserted rows
/// before the retained window. EvictedCount reports this explicitly. Each nonempty mutation raises one Reset.</remarks>
public sealed class ChartDataBuffer<T> : IReadOnlyList<T>, INotifyCollectionChanged, IChartDataBufferSource
{
    private const int PageSize = 256;
    private readonly CompactIdentityRegistry _identities = new(0);
    private ChartBufferState _state = new([], 0);
    private bool _changing;
    public ChartDataBuffer(int capacity = int.MaxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        Capacity = capacity;
    }
    public int Capacity { get; }
    public int Count => _state.Count;
    /// <summary>Distinct backing pages retained by the current buffer. Every page allocates 256 T slots and 256 identity slots.</summary>
    /// <remarks>Earlier snapshots can retain additional pages. This is an element-slot measure, not a bound on arbitrary T object graphs.</remarks>
    public int StoragePageCount => _state.StoragePageCount;
    public T this[int index] { get { var location = _state.Locate(index); return ((Page)location.Slice.Page).Values[location.Slice.Offset + location.Local]; } }
    public long Version { get; private set; }
    public long EvictedCount { get; private set; }
    public int LastMutationEvictedCount { get; private set; }
    public long RemovedCount { get; private set; }
    public int LastMutationRemovedCount { get; private set; }
    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    ChartBufferState IChartDataBufferSource.Capture() => _state;
    bool IChartDataBufferSource.SupportsDirectValues => DirectValues.Supported;

    public void AppendRange(IEnumerable<T> items) => ReplaceCore(Count, 0, items);
    public void InsertRange(int index, IEnumerable<T> items) => ReplaceCore(index, 0, items);
    public void RemoveRange(int index, int count) => ReplaceCore(index, count, Array.Empty<T>());
    public void ReplaceRange(int index, int count, IEnumerable<T> items) => ReplaceCore(index, count, items);
    public void Clear() => ReplaceCore(0, Count, Array.Empty<T>());

    /// <summary>Packs retained rows into the minimum number of pages without changing their items, identities or order.</summary>
    /// <remarks>This explicit UI-thread operation copies Count retained rows. It raises one Reset and increments Version when
    /// storage or its slice directory needs packing. Older snapshots remain immutable and may retain the original pages.</remarks>
    public bool Compact()
    {
        Dispatcher.UIThread.VerifyAccess();
        if (_changing) throw new InvalidOperationException("Reentrant chart buffer mutation is not supported.");
        var previous = _state;
        var pageCount = (int)(((long)Count + PageSize - 1) / PageSize);
        var packed = previous.Slices.Length == pageCount;
        if (packed)
            for (var index = 0; index < previous.Slices.Length; index++)
            {
                var slice = previous.Slices[index];
                var expected = Math.Min(PageSize, Count - index * PageSize);
                if (slice.Offset != 0 || slice.Count != expected || slice.Page.Count != expected) { packed = false; break; }
            }
        if (packed) return false;
        var version = checked(Version + 1);
        _changing = true;
        try
        {
            var pages = new ChartBufferSlice[pageCount];
            var sourceSlice = 0; var sourceLocal = 0;
            for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
            {
                var values = new T[PageSize]; var ids = new long[PageSize];
                var length = Math.Min(PageSize, Count - pageIndex * PageSize);
                var destination = 0;
                while (destination < length)
                {
                    var slice = previous.Slices[sourceSlice];
                    var sourcePage = (Page)slice.Page;
                    var take = Math.Min(length - destination, slice.Count - sourceLocal);
                    Array.Copy(sourcePage.Values, slice.Offset + sourceLocal, values, destination, take);
                    Array.Copy(sourcePage.IdentityIds, slice.Offset + sourceLocal, ids, destination, take);
                    destination += take; sourceLocal += take;
                    if (sourceLocal == slice.Count) { sourceSlice++; sourceLocal = 0; }
                }
                var orderedIdentities = true;
                for (var index = 1; index < length; index++) orderedIdentities &= ids[index] > ids[index - 1];
                pages[pageIndex] = new(new Page(values, ids, length, _identities, orderedIdentities), 0, length);
            }
            var next = new ChartBufferState(pages, Count);
            _state = next; Version = version;
            LastMutationEvictedCount = 0; LastMutationRemovedCount = 0;
            Interlocked.Add(ref ChartDataBufferMetrics.CopiedItemCount, Count);
            Interlocked.Add(ref ChartDataBufferMetrics.CreatedPageCount, pageCount);
            Interlocked.Add(ref ChartDataBufferMetrics.CompactedItemCount, Count);
            Interlocked.Increment(ref ChartDataBufferMetrics.CompactionCount);
            CollectionChanged?.Invoke(this, new ChartBufferChangedEventArgs(next, [], [], previous));
            return true;
        }
        finally { _changing = false; }
    }

    private void ReplaceCore(int index, int count, IEnumerable<T> items)
    {
        Dispatcher.UIThread.VerifyAccess();
        ArgumentNullException.ThrowIfNull(items);
        if (_changing) throw new InvalidOperationException("Reentrant chart buffer mutation is not supported.");
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (index > Count || count > Count - index) throw new ArgumentOutOfRangeException(nameof(count));
        _changing = true;
        try
        {
            // Enumerate caller data fully before publication. A failed iterator leaves the source unchanged.
            var added = new List<ChartBufferSlice>();
            var insertedCount = 0;
            using (var iterator = items.GetEnumerator())
            {
                while (iterator.MoveNext())
                {
                    var values = new T[PageSize];
                    var ids = new long[PageSize];
                    var length = 0;
                    do
                    {
                        values[length] = iterator.Current;
                        ids[length++] = _identities.NextId();
                        insertedCount = checked(insertedCount + 1);
                    } while (length < PageSize && iterator.MoveNext());
                    added.Add(new(new Page(values, ids, length, _identities), 0, length));
                    Interlocked.Add(ref ChartDataBufferMetrics.CopiedItemCount, length);
                    Interlocked.Increment(ref ChartDataBufferMetrics.CreatedPageCount);
                }
            }
            if (count == 0 && insertedCount == 0) return;
            var logicalCount = checked(Count - count + insertedCount);
            var logical = new List<ChartBufferSlice>(_state.Slices.Length + added.Count + 2);
            _state.AppendRange(logical, 0, index);
            logical.AddRange(added);
            _state.AppendRange(logical, index + count, Count - index - count);
            var excess = Math.Max(0, logicalCount - Capacity);
            var retained = SliceRange(logical, excess, logicalCount - excess);
            var removed = new List<ChartBufferSlice>();
            _state.AppendRange(removed, index, count);
            var newPages = new HashSet<IChartBufferPage>(added.Select(slice => slice.Page), ReferenceEqualityComparer.Instance);
            removed.AddRange(SliceRange(logical, 0, excess).Where(slice => !newPages.Contains(slice.Page)));
            var retainedAdded = retained.Where(slice => newPages.Contains(slice.Page)).ToArray();
            var nextVersion = checked(Version + 1);
            var nextEvicted = checked(EvictedCount + excess);
            var nextRemoved = checked(RemovedCount + count);
            var next = new ChartBufferState(retained, logicalCount - excess);
            _state = next; Version = nextVersion;
            EvictedCount = nextEvicted; LastMutationEvictedCount = excess;
            RemovedCount = nextRemoved; LastMutationRemovedCount = count;
            _identities.PruneAfterCollectionChange();
            CollectionChanged?.Invoke(this, new ChartBufferChangedEventArgs(next, retainedAdded, removed.ToArray()));
        }
        finally { _changing = false; }
    }

    private static ChartBufferSlice[] SliceRange(IReadOnlyList<ChartBufferSlice> source, int start, int count)
    {
        if (count == 0) return [];
        var result = new List<ChartBufferSlice>();
        foreach (var slice in source)
        {
            if (start >= slice.Count) { start -= slice.Count; continue; }
            var take = Math.Min(count, slice.Count - start);
            result.Add(slice with { Offset = slice.Offset + start, Count = take });
            count -= take; start = 0;
            if (count == 0) break;
        }
        return result.ToArray();
    }

    public IEnumerator<T> GetEnumerator()
    {
        var state = _state;
        foreach (var slice in state.Slices)
        {
            var values = ((Page)slice.Page).Values;
            for (var index = 0; index < slice.Count; index++) yield return values[slice.Offset + index];
        }
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private sealed class Page(T[] values, long[] identities, int count, CompactIdentityRegistry registry, bool orderedIdentities = true) : IChartBufferPage
    {
        private readonly (long Minimum, long Maximum) _identityBounds = IdentityBounds(identities, count);
        private readonly object _summaryLock = new();
        private ChartSnapshotSummary? _gapSummary;
        private ChartSnapshotSummary? _zeroSummary;
        public T[] Values { get; } = values;
        public long[] IdentityIds { get; } = identities;
        public int Count { get; } = count;
        public object? GetItem(int index) => Values[index];
        public object GetIdentity(int index) => registry.GetToken(IdentityIds[index]);
        public bool IsIdentity(object candidate, int index) => candidate is CompactIdentityRegistry.IdentityToken token &&
            ReferenceEquals(token.Epoch, registry.Epoch) && token.Id == IdentityIds[index];
        public bool TryFindIdentity(object candidate, int offset, int length, out int slot)
        {
            if (candidate is CompactIdentityRegistry.IdentityToken token && ReferenceEquals(token.Epoch, registry.Epoch))
            {
                if (token.Id < _identityBounds.Minimum || token.Id > _identityBounds.Maximum) { slot = -1; return false; }
                if (orderedIdentities)
                {
                    slot = Array.BinarySearch(IdentityIds, offset, length, token.Id);
                    return slot >= 0;
                }
                // A compacted page can combine slices whose identities are not globally ordered
                // after an insertion. Keep the exact original row identity even in that layout.
                for (var index = offset; index < offset + length; index++)
                    if (IdentityIds[index] == token.Id) { slot = index; return true; }
                slot = -1;
                return false;
            }
            slot = -1; return false;
        }
        private static (long Minimum, long Maximum) IdentityBounds(long[] values, int length)
        {
            var minimum = long.MaxValue; var maximum = long.MinValue;
            for (var index = 0; index < length; index++) { minimum = Math.Min(minimum, values[index]); maximum = Math.Max(maximum, values[index]); }
            return (minimum, maximum);
        }
        public ChartPoint GetDirectPoint(int index, int ordinal) => DirectValues.Project(Values[index], ordinal);
        public bool Coordinates => DirectValues.Coordinates;
        public ChartSnapshotSummary GetDirectSummary(int offset, int length, bool zero)
        {
            lock (_summaryLock)
            {
                var cached = zero ? _zeroSummary : _gapSummary;
                if (offset == 0 && length == Count && cached is { } ready) return ready;
                var result = ChartSnapshotSummary.Empty;
                for (var index = offset; index < offset + length; index++)
                {
                    var point = DirectValues.Project(Values[index], index);
                    if (zero && !double.IsFinite(point.Y) && double.IsFinite(point.X)) point = point with { Y = 0 };
                    result = result.Append(point);
                }
                Interlocked.Add(ref ChartDataBufferMetrics.SummaryRowVisitCount, length);
                if (offset == 0 && length == Count) { if (zero) _zeroSummary = result; else _gapSummary = result; }
                return result;
            }
        }
    }

    private static class DirectValues
    {
        private static readonly Type ValueType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        public static bool Coordinates { get; } = typeof(T) == typeof(ChartPoint) || typeof(T) == typeof(Point);
        public static bool Supported { get; } = Coordinates || ValueType == typeof(double) || ValueType == typeof(float) ||
            ValueType == typeof(decimal) || ValueType == typeof(int) || ValueType == typeof(long) || ValueType == typeof(short) ||
            ValueType == typeof(byte) || ValueType == typeof(sbyte) || ValueType == typeof(uint) || ValueType == typeof(ulong) || ValueType == typeof(ushort);
        public static ChartPoint Project(T value, int ordinal)
        {
            if (typeof(T) == typeof(ChartPoint)) return Unsafe.As<T, ChartPoint>(ref value);
            if (typeof(T) == typeof(Point)) { var point = Unsafe.As<T, Point>(ref value); return new(point.X, point.Y); }
            double number;
            if (typeof(T) == typeof(double)) number = Unsafe.As<T, double>(ref value);
            else if (typeof(T) == typeof(float)) number = Unsafe.As<T, float>(ref value);
            else if (typeof(T) == typeof(decimal)) number = (double)Unsafe.As<T, decimal>(ref value);
            else if (typeof(T) == typeof(int)) number = Unsafe.As<T, int>(ref value);
            else if (typeof(T) == typeof(long)) number = Unsafe.As<T, long>(ref value);
            else if (typeof(T) == typeof(short)) number = Unsafe.As<T, short>(ref value);
            else if (typeof(T) == typeof(byte)) number = Unsafe.As<T, byte>(ref value);
            else if (typeof(T) == typeof(sbyte)) number = Unsafe.As<T, sbyte>(ref value);
            else if (typeof(T) == typeof(uint)) number = Unsafe.As<T, uint>(ref value);
            else if (typeof(T) == typeof(ulong)) number = Unsafe.As<T, ulong>(ref value);
            else if (typeof(T) == typeof(ushort)) number = Unsafe.As<T, ushort>(ref value);
            else if (typeof(T) == typeof(double?)) number = Unsafe.As<T, double?>(ref value) ?? double.NaN;
            else if (typeof(T) == typeof(float?)) number = Unsafe.As<T, float?>(ref value) is { } nFloat ? nFloat : double.NaN;
            else if (typeof(T) == typeof(decimal?)) number = Unsafe.As<T, decimal?>(ref value) is { } nDecimal ? (double)nDecimal : double.NaN;
            else if (typeof(T) == typeof(int?)) number = Unsafe.As<T, int?>(ref value) is { } nInt ? nInt : double.NaN;
            else if (typeof(T) == typeof(long?)) number = Unsafe.As<T, long?>(ref value) is { } nLong ? nLong : double.NaN;
            else if (typeof(T) == typeof(short?)) number = Unsafe.As<T, short?>(ref value) is { } nShort ? nShort : double.NaN;
            else if (typeof(T) == typeof(byte?)) number = Unsafe.As<T, byte?>(ref value) is { } nByte ? nByte : double.NaN;
            else if (typeof(T) == typeof(sbyte?)) number = Unsafe.As<T, sbyte?>(ref value) is { } nSbyte ? nSbyte : double.NaN;
            else if (typeof(T) == typeof(uint?)) number = Unsafe.As<T, uint?>(ref value) is { } nUint ? nUint : double.NaN;
            else if (typeof(T) == typeof(ulong?)) number = Unsafe.As<T, ulong?>(ref value) is { } nUlong ? nUlong : double.NaN;
            else if (typeof(T) == typeof(ushort?)) number = Unsafe.As<T, ushort?>(ref value) is { } nUshort ? nUshort : double.NaN;
            else number = ChartDataAccessor.TryNumber(value, out var nullable) ? nullable : double.NaN;
            return new(ordinal, double.IsFinite(number) ? number : double.NaN);
        }
    }
}

internal interface IChartDataBufferSource
{
    ChartBufferState Capture();
    bool SupportsDirectValues { get; }
}

internal interface IChartBufferPage
{
    int Count { get; }
    object? GetItem(int index);
    object GetIdentity(int index);
    bool IsIdentity(object candidate, int index);
    bool TryFindIdentity(object candidate, int offset, int length, out int slot);
    ChartPoint GetDirectPoint(int index, int ordinal);
    bool Coordinates { get; }
    ChartSnapshotSummary GetDirectSummary(int offset, int length, bool zero);
}

internal readonly record struct ChartBufferSlice(IChartBufferPage Page, int Offset, int Count);

internal sealed class ChartBufferState
{
    private readonly int[] _starts;
    private int _storagePageCount = -1;
    public ChartBufferState(ChartBufferSlice[] slices, int count)
    {
        Slices = slices; Count = count;
        _starts = new int[slices.Length];
        var start = 0;
        for (var index = 0; index < slices.Length; index++) { _starts[index] = start; start += slices[index].Count; }
        Interlocked.Add(ref ChartDataBufferMetrics.DirectoryEntryCount, slices.Length);
    }
    public ChartBufferSlice[] Slices { get; }
    public int Count { get; }
    public int StoragePageCount
    {
        get
        {
            var cached = Volatile.Read(ref _storagePageCount);
            if (cached >= 0) return cached;
            var pages = new HashSet<IChartBufferPage>(ReferenceEqualityComparer.Instance);
            foreach (var slice in Slices) pages.Add(slice.Page);
            cached = pages.Count;
            Interlocked.CompareExchange(ref _storagePageCount, cached, -1);
            return cached;
        }
    }
    public (ChartBufferSlice Slice, int Local) Locate(int ordinal)
    {
        var (index, local) = LocateIndex(ordinal);
        return (Slices[index], local);
    }
    public (int SliceIndex, int Local) LocateIndex(int ordinal)
    {
        if ((uint)ordinal >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(ordinal));
        var index = Array.BinarySearch(_starts, ordinal);
        if (index < 0) index = ~index - 1;
        return (index, ordinal - _starts[index]);
    }
    public object? GetItem(int ordinal) { var (slice, local) = Locate(ordinal); return slice.Page.GetItem(slice.Offset + local); }
    public object GetIdentity(int ordinal) { var (slice, local) = Locate(ordinal); return slice.Page.GetIdentity(slice.Offset + local); }
    public void AppendRange(List<ChartBufferSlice> destination, int start, int count)
    {
        foreach (var slice in Slices)
        {
            if (count == 0) break;
            if (start >= slice.Count) { start -= slice.Count; continue; }
            var take = Math.Min(count, slice.Count - start);
            destination.Add(slice with { Offset = slice.Offset + start, Count = take });
            count -= take; start = 0;
        }
    }
}

internal sealed class ChartBufferChangedEventArgs(ChartBufferState state, ChartBufferSlice[] added, ChartBufferSlice[] removed,
    ChartBufferState? previousState = null)
    : NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)
{
    public ChartBufferState State { get; } = state;
    public ChartBufferSlice[] Added { get; } = added;
    public ChartBufferSlice[] Removed { get; } = removed;
    public ChartBufferState? PreviousState { get; } = previousState;
}

// Append/edit payload copies count new rows; explicit compaction also counts copied retained rows.
// The immutable slice directory costs O(page count) per mutation and is a separate measure.
internal static class ChartDataBufferMetrics
{
    internal static long CopiedItemCount;
    internal static long CreatedPageCount;
    internal static long DirectoryEntryCount;
    internal static long SummaryRowVisitCount;
    internal static long IdentityRowVisitCount;
    internal static long CompactionCount;
    internal static long CompactedItemCount;
    internal static long MappedCompactionCopyCount;
}
