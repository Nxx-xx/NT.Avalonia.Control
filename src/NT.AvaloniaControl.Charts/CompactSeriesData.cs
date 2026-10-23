using System.Collections;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using Avalonia;

namespace NT.AvaloniaControl.Charts;

/// <summary>Immutable value buffers. Metadata is expanded only by the public point projection.</summary>
internal abstract class CompactSeriesData
{
    protected readonly CompactIdentityRegistry Identities;
    protected CompactSeriesData(CompactIdentityRegistry identities) => Identities = identities;
    public abstract int Count { get; }
    public virtual ReadOnlyMemory<ChartPoint> Coordinates => ReadOnlyMemory<ChartPoint>.Empty;
    public abstract ChartDataValue GetValue(int index);
    public abstract object? GetItem(int index);
    public abstract object GetCategory(int index);
    protected abstract long GetIdentityId(int index);
    protected abstract bool TryGetIdentityOrdinal(long identity, out int ordinal);
    public abstract CompactSeriesData? Apply(NotifyCollectionChangedEventArgs args);
    public abstract bool MatchesItem(int index, object? item);
    public object GetIdentity(int index) => Identities.GetToken(GetIdentityId(index));

    public ChartDataSnapshot CreateSnapshot(long version, EmptyPointMode mode) =>
        ChartDataSnapshot.FromCompact(version, new SnapshotData(this, mode));

    public static CompactSeriesData? TryCreate(IEnumerable? source) => source switch
    {
        IEnumerable<ChartPoint> values when IsExactArray<ChartPoint>(source) => Create(values, static (value, _) => value, true),
        IEnumerable<Point> values when IsExactArray<Point>(source) => Create(values, static (value, _) => new(value.X, value.Y), true),
        IEnumerable<double> values when IsExactArray<double>(source) => Create(values, static (value, index) => new(index, value)),
        IEnumerable<float> values when IsExactArray<float>(source) => Create(values, static (value, index) => new(index, value)),
        IEnumerable<decimal> values when IsExactArray<decimal>(source) => Create(values, static (value, index) => new(index, (double)value)),
        IEnumerable<int> values when IsExactArray<int>(source) => Create(values, static (value, index) => new(index, value)),
        IEnumerable<long> values when IsExactArray<long>(source) => Create(values, static (value, index) => new(index, value)),
        IEnumerable<short> values when IsExactArray<short>(source) => Create(values, static (value, index) => new(index, value)),
        IEnumerable<byte> values when IsExactArray<byte>(source) => Create(values, static (value, index) => new(index, value)),
        IEnumerable<sbyte> values when IsExactArray<sbyte>(source) => Create(values, static (value, index) => new(index, value)),
        IEnumerable<uint> values when IsExactArray<uint>(source) => Create(values, static (value, index) => new(index, value)),
        IEnumerable<ulong> values when IsExactArray<ulong>(source) => Create(values, static (value, index) => new(index, value)),
        IEnumerable<ushort> values when IsExactArray<ushort>(source) => Create(values, static (value, index) => new(index, value)),
        IEnumerable<double?> values when IsExactArray<double?>(source) => Create(values, static (value, index) => new(index, value ?? double.NaN)),
        IEnumerable<float?> values when IsExactArray<float?>(source) => Create(values, static (value, index) => new(index, value is { } number ? number : double.NaN)),
        IEnumerable<decimal?> values when IsExactArray<decimal?>(source) => Create(values, static (value, index) => new(index, value is { } number ? (double)number : double.NaN)),
        IEnumerable<int?> values when IsExactArray<int?>(source) => Create(values, static (value, index) => new(index, value is { } number ? number : double.NaN)),
        IEnumerable<long?> values when IsExactArray<long?>(source) => Create(values, static (value, index) => new(index, value is { } number ? number : double.NaN)),
        IEnumerable<short?> values when IsExactArray<short?>(source) => Create(values, static (value, index) => new(index, value is { } number ? number : double.NaN)),
        IEnumerable<byte?> values when IsExactArray<byte?>(source) => Create(values, static (value, index) => new(index, value is { } number ? number : double.NaN)),
        IEnumerable<sbyte?> values when IsExactArray<sbyte?>(source) => Create(values, static (value, index) => new(index, value is { } number ? number : double.NaN)),
        IEnumerable<uint?> values when IsExactArray<uint?>(source) => Create(values, static (value, index) => new(index, value is { } number ? number : double.NaN)),
        IEnumerable<ulong?> values when IsExactArray<ulong?>(source) => Create(values, static (value, index) => new(index, value is { } number ? number : double.NaN)),
        IEnumerable<ushort?> values when IsExactArray<ushort?>(source) => Create(values, static (value, index) => new(index, value is { } number ? number : double.NaN)),
        _ => null,
    };

    // CLR primitive arrays can implement the opposite signedness's generic interface.
    // Never infer their original scalar type from that interface cast.
    private static bool IsExactArray<T>(IEnumerable source) => source is not Array || source.GetType() == typeof(T[]);

    private static CompactSeriesData Create<T>(IEnumerable<T> source, Func<T, int, ChartPoint> project, bool coordinates = false)
    {
        var values = CollectOwned(source);
        return new ValueBuffer<T>(values, null, new CompactIdentityRegistry(values.Length), project, coordinates);
    }

    private static T[] CollectOwned<T>(IEnumerable<T> source)
    {
        // ICollection<T> uses an exact allocation and CopyTo. Unknown-length
        // Enumerable.ToArray uses shared pooled segments on .NET 10. Keep our
        // unknown-length staging retention bounded across all supported types.
        if (source is ICollection<T>) return source.ToArray();

        var hasKnownCount = source.TryGetNonEnumeratedCount(out var knownCount) && knownCount > 0;
        if (hasKnownCount) return CollectOwnedCore(source, knownCount, null);

        // Keep this cleanup region outside the hot enumeration method. Its own
        // foreach still disposes the enumerator before these leases are returned.
        var leases = new StagingLeases<T>();
        try { return CollectOwnedCore(source, 0, leases); }
        finally { leases.ReturnAll(); }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T[] CollectOwnedCore<T>(IEnumerable<T> source, int knownCount, StagingLeases<T>? leases)
    {
        const int maximumChunkLength = 4_096;
        T[]? chunk = knownCount > 0 ? new T[knownCount] : null;
        List<T[]>? completed = null;
        var offset = 0;
        var count = 0;
        foreach (var value in source)
        {
            if (chunk is null || offset == chunk.Length)
            {
                // Small live sources retain their original 32..2048 growth.
                var length = chunk is null ? 32 : Math.Clamp((long)chunk.Length * 2, 32, maximumChunkLength);
                if (chunk is not null) (completed ??= []).Add(chunk);
                chunk = length == maximumChunkLength && leases is not null
                    ? leases.RentOrCreate() : new T[(int)length];
                offset = 0;
            }
            chunk[offset++] = value;
            count = checked(count + 1);
        }
        if (count == 0) return Array.Empty<T>();
        // A single chunk is either a small initial allocation or a known-count
        // allocation. A rented 4096-element chunk always has earlier segments.
        if (completed is null && chunk is { } exact && offset == exact.Length) return exact;

        // This final array has a separate lifetime and never enters the pool.
        var owned = new T[count];
        var destination = 0;
        if (completed is not null)
            foreach (var segment in completed)
            {
                Array.Copy(segment, 0, owned, destination, segment.Length);
                destination += segment.Length;
            }
        Array.Copy(chunk!, 0, owned, destination, offset);
        return owned;
    }

    // Per-call ownership only; the retained pool below is shared across all T.
    // Nothing from this object is written on the per-point enumeration path.
    private sealed class StagingLeases<T>
    {
        private readonly int _limit = StagingBufferPool.GetRentLimit<T>();
        private T[][]? _rented;
        private int _count;

        public T[] RentOrCreate()
        {
            if (_count >= _limit) return new T[4_096];
            // Allocate the registry before acquiring a lease. Its next slot is
            // already in range, so successful Rent is followed only by stores.
            var rented = _rented ??= new T[_limit][];
            ref var slot = ref rented[_count];
            var buffer = StagingBufferPool.Rent<T>();
            slot = buffer;
            _count++;
            return buffer;
        }

        public void ReturnAll()
        {
            if (_rented is not { } rented) return;
            var count = _count;
            _rented = null;
            _count = 0;
            for (var index = 0; index < count; index++) StagingBufferPool.Return(rented[index]);
        }
    }

    // This coordinator is deliberately non-generic: the limits cover every T
    // together, not sixteen buffers (or one MiB) per closed generic type.
    private static class StagingBufferPool
    {
        private const int ChunkLength = 4_096;
        private const int MaximumSlots = 16;
        private const int MaximumPayloadBytes = 1_048_576;
        private static readonly object Gate = new();
        private static readonly Array?[] Slots = new Array?[MaximumSlots];
        private static readonly int[] PayloadBytes = new int[MaximumSlots];
        private static int _retainedCount;
        private static int _retainedPayloadBytes;

        internal static bool Supports<T>()
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>()) return false;
            if (typeof(T) == typeof(ChartPoint) || typeof(T) == typeof(Point)) return true;
            var type = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            return type == typeof(double) || type == typeof(float) || type == typeof(decimal) ||
                type == typeof(int) || type == typeof(long) || type == typeof(short) ||
                type == typeof(byte) || type == typeof(sbyte) || type == typeof(uint) ||
                type == typeof(ulong) || type == typeof(ushort);
        }

        internal static int GetRentLimit<T>() => Supports<T>()
            ? Math.Min(MaximumSlots, MaximumPayloadBytes / (Unsafe.SizeOf<T>() * ChunkLength)) : 0;

        internal static T[] Rent<T>()
        {
            if (!Supports<T>()) throw new InvalidOperationException("Unsupported staging buffer element type.");
            lock (Gate)
            {
                for (var index = 0; index < _retainedCount; index++)
                {
                    // CLR signed/unsigned array interface aliases are not enough
                    // to establish the element type or payload accounting.
                    var candidate = Slots[index]!;
                    if (candidate.GetType() != typeof(T[])) continue;
                    RemoveAt(index);
                    return (T[])candidate;
                }
            }
            return new T[ChunkLength];
        }

        internal static void Return<T>(T[] buffer)
        {
            if (buffer.Length != ChunkLength || buffer.GetType() != typeof(T[]) || !Supports<T>()) return;
            var payloadBytes = Unsafe.SizeOf<T>() * buffer.Length;
            if (payloadBytes > MaximumPayloadBytes) return;

            // The caller exclusively owns the lease until this publication.
            // Clearing outside the lock also clears partially filled/error paths.
            Array.Clear(buffer);
            lock (Gate)
            {
                // FIFO eviction lets a different supported T replace old blocks.
                // In-flight buffers and array headers are not this payload budget.
                while (_retainedCount == MaximumSlots || _retainedPayloadBytes + payloadBytes > MaximumPayloadBytes)
                    RemoveAt(0);
                Slots[_retainedCount] = buffer;
                PayloadBytes[_retainedCount] = payloadBytes;
                _retainedCount++;
                _retainedPayloadBytes += payloadBytes;
            }
        }

        private static void RemoveAt(int index)
        {
            _retainedPayloadBytes -= PayloadBytes[index];
            _retainedCount--;
            for (var current = index; current < _retainedCount; current++)
            {
                Slots[current] = Slots[current + 1];
                PayloadBytes[current] = PayloadBytes[current + 1];
            }
            Slots[_retainedCount] = null;
            PayloadBytes[_retainedCount] = 0;
        }
    }

    private sealed class ValueBuffer<T>(T[] values, long[]? ids, CompactIdentityRegistry registry,
        Func<T, int, ChartPoint> project, bool coordinates) : CompactSeriesData(registry)
    {
        private readonly object _ordinalLock = new();
        private Dictionary<long, int>? _identityOrdinals;
        public override int Count => values.Length;
        public override ReadOnlyMemory<ChartPoint> Coordinates => (object)values is ChartPoint[] points ? points : ReadOnlyMemory<ChartPoint>.Empty;
        public override ChartDataValue GetValue(int index)
        {
            var point = project(values[index], index);
            if (!coordinates && !double.IsFinite(point.Y)) point = point with { Y = double.NaN };
            var empty = !double.IsFinite(point.X) || !double.IsFinite(point.Y);
            return new(index, point, empty, empty);
        }
        public override object? GetItem(int index) => values[index];
        public override object GetCategory(int index) => coordinates ? (object)project(values[index], index).X : index;
        protected override long GetIdentityId(int index) => ids is null ? index + 1L : ids[index];
        public override bool MatchesItem(int index, object? item) => item is T candidate ? EqualityComparer<T>.Default.Equals(values[index], candidate) :
            item is null && values[index] is null;

        protected override bool TryGetIdentityOrdinal(long identity, out int ordinal)
        {
            if (ids is null)
            {
                if (identity > 0 && identity <= Count) { ordinal = (int)(identity - 1); return true; }
                ordinal = -1; return false;
            }
            lock (_ordinalLock)
            {
                if (_identityOrdinals is null)
                {
                    var lookup = new Dictionary<long, int>(ids.Length);
                    for (var index = 0; index < ids.Length; index++) lookup.Add(ids[index], index);
                    _identityOrdinals = lookup;
                }
                return _identityOrdinals.TryGetValue(identity, out ordinal);
            }
        }

        public override CompactSeriesData? Apply(NotifyCollectionChangedEventArgs args)
        {
            if (args.Action == NotifyCollectionChangedAction.Reset) return null;
            var nextValues = new List<T>(values);
            var nextIds = new List<long>(Count);
            for (var index = 0; index < Count; index++) nextIds.Add(GetIdentityId(index));
            T[] NewValues() => args.NewItems is null ? [] : args.NewItems.Cast<T>().ToArray();
            long[] NewIds(int count)
            {
                var result = new long[count];
                for (var index = 0; index < count; index++) result[index] = Identities.NextId();
                return result;
            }
            switch (args.Action)
            {
                case NotifyCollectionChangedAction.Add when args.NewStartingIndex >= 0 && args.NewItems is not null:
                    var added = NewValues();
                    nextValues.InsertRange(args.NewStartingIndex, added);
                    nextIds.InsertRange(args.NewStartingIndex, NewIds(added.Length));
                    break;
                case NotifyCollectionChangedAction.Remove when args.OldStartingIndex >= 0 && args.OldItems is not null:
                    nextValues.RemoveRange(args.OldStartingIndex, args.OldItems.Count);
                    nextIds.RemoveRange(args.OldStartingIndex, args.OldItems.Count);
                    break;
                case NotifyCollectionChangedAction.Replace when args.OldStartingIndex >= 0 && args.OldItems is not null && args.NewItems is not null:
                    var replaced = NewValues();
                    nextValues.RemoveRange(args.OldStartingIndex, args.OldItems.Count);
                    nextIds.RemoveRange(args.OldStartingIndex, args.OldItems.Count);
                    nextValues.InsertRange(args.OldStartingIndex, replaced);
                    nextIds.InsertRange(args.OldStartingIndex, NewIds(replaced.Length));
                    break;
                case NotifyCollectionChangedAction.Move when args.OldStartingIndex >= 0 && args.NewStartingIndex >= 0 && args.OldItems is not null:
                    var movedValues = nextValues.GetRange(args.OldStartingIndex, args.OldItems.Count);
                    var movedIds = nextIds.GetRange(args.OldStartingIndex, args.OldItems.Count);
                    nextValues.RemoveRange(args.OldStartingIndex, args.OldItems.Count);
                    nextIds.RemoveRange(args.OldStartingIndex, args.OldItems.Count);
                    nextValues.InsertRange(args.NewStartingIndex, movedValues);
                    nextIds.InsertRange(args.NewStartingIndex, movedIds);
                    break;
                default: return null;
            }
            Identities.PruneAfterCollectionChange();
            return new ValueBuffer<T>(nextValues.ToArray(), nextIds.ToArray(), Identities, project, coordinates);
        }
    }

    private sealed class SnapshotData : IChartSnapshotData
    {
        private readonly CompactSeriesData _source;
        private readonly EmptyPointMode _mode;
        private readonly ChartDataValue[]? _repaired;
        public SnapshotData(CompactSeriesData source, EmptyPointMode mode)
        {
            _source = source;
            _mode = mode;
            if (mode is EmptyPointMode.Average or EmptyPointMode.Interpolate)
            {
                var values = new ChartDataValue[source.Count];
                for (var index = 0; index < values.Length; index++) values[index] = source.GetValue(index);
                RepairRuns(values, mode);
                _repaired = values;
            }
        }
        public int Count => _source.Count;
        public ReadOnlyMemory<ChartPoint> Coordinates => _mode == EmptyPointMode.Gap ? _source.Coordinates : ReadOnlyMemory<ChartPoint>.Empty;
        public bool HasOrdinalSourceIndices => true;
        public ChartDataValue GetValue(int index)
        {
            if (_repaired is { } repaired) return repaired[index];
            var value = _source.GetValue(index);
            return _mode == EmptyPointMode.Zero && value.WasEmpty && double.IsFinite(value.Point.X)
                ? value with { Point = value.Point with { Y = 0 }, IsEmpty = false } : value;
        }
        public ChartDataPoint GetPoint(int index)
        {
            var value = GetValue(index);
            return new(index, _source.GetIdentity(index), _source.GetItem(index), _source.GetCategory(index), value.Point, value.IsEmpty)
                { WasEmpty = value.WasEmpty };
        }
        public bool TryFindIdentity(object identity, bool valueEquality, out int ordinal)
        {
            if (identity is CompactIdentityRegistry.IdentityToken token && ReferenceEquals(token.Epoch, _source.Identities.Epoch))
                return _source.TryGetIdentityOrdinal(token.Id, out ordinal);
            ordinal = -1; return false;
        }

        private static void RepairRuns(ChartDataValue[] values, EmptyPointMode mode)
        {
            for (var index = 0; index < values.Length;)
            {
                if (!values[index].WasEmpty) { index++; continue; }
                var begin = index;
                while (index < values.Length && values[index].WasEmpty) index++;
                if (begin == 0 || index == values.Length) continue;
                var first = values[begin - 1].Point; var last = values[index].Point;
                for (var current = begin; current < index; current++)
                {
                    if (!double.IsFinite(values[current].Point.X)) continue;
                    var span = last.X - first.X;
                    var fraction = span > 0 && double.IsFinite(span) ? (values[current].Point.X - first.X) / span : (current - begin + 1) / (double)(index - begin + 1);
                    var replacement = mode == EmptyPointMode.Average ? first.Y * .5 + last.Y * .5 : first.Y * (1 - fraction) + last.Y * fraction;
                    if (double.IsFinite(replacement)) values[current] = values[current] with { Point = values[current].Point with { Y = replacement }, IsEmpty = false };
                }
            }
        }
    }
}

/// <summary>A token never retains its source buffer or series. Dead weak entries are periodically swept.</summary>
internal sealed class CompactIdentityRegistry(int initialCount)
{
    internal object Epoch { get; } = new();
    private readonly object _gate = new();
    private readonly Dictionary<long, WeakReference<IdentityToken>> _tokens = [];
    private long _nextId = initialCount;
    private int _nextSweep = 1024;
    private int _lastGen2 = GC.CollectionCount(2);
    internal int WeakEntryCount { get { lock (_gate) return _tokens.Count; } }
    public long NextId() { lock (_gate) return checked(++_nextId); }
    public object GetToken(long id)
    {
        lock (_gate)
        {
            if (_tokens.TryGetValue(id, out var weak) && weak.TryGetTarget(out var existing)) return existing;
            if (_tokens.Count >= _nextSweep) Sweep();
            var token = new IdentityToken(this, id);
            _tokens[id] = new WeakReference<IdentityToken>(token);
            return token;
        }
    }
    public void PruneAfterCollectionChange()
    {
        lock (_gate)
        {
            var collection = GC.CollectionCount(2);
            if (collection != _lastGen2 && _tokens.Count > 0) Sweep();
            _lastGen2 = collection;
        }
    }
    private void Sweep()
    {
        var dead = new List<long>();
        foreach (var item in _tokens) if (!item.Value.TryGetTarget(out _)) dead.Add(item.Key);
        foreach (var key in dead) _tokens.Remove(key);
        if (_tokens.Count < _tokens.EnsureCapacity(0) / 4) _tokens.TrimExcess();
        _nextSweep = (int)Math.Min(int.MaxValue, Math.Max(1024L, _tokens.Count * 2L));
    }
    internal sealed class IdentityToken(CompactIdentityRegistry registry, long id)
    {
        public object Epoch { get; } = registry.Epoch;
        public long Id { get; } = id;
    }
}
