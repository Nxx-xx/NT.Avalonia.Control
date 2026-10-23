using System.Runtime.CompilerServices;

namespace NT.AvaloniaControl.Charts;

/// <summary>A lazy exact X search index over immutable coordinates. No data is decimated.</summary>
internal sealed class ChartDataIndex
{
    private static readonly ConditionalWeakTable<ChartDataSnapshot, ChartDataIndex> Cache = new();
    private readonly ChartDataSnapshot _data;
    private readonly int[]? _ordinals;
    private readonly bool _descending;
    private static long _buildCount;
    internal static long BuildCount => Interlocked.Read(ref _buildCount);
    public ChartSeriesBounds? Bounds { get; }
    public int Count { get; }
    public static ChartDataIndex For(ChartDataSnapshot data) => Cache.GetValue(data.CoordinateCacheOwner, static value => new(value));
    internal static bool TryGetExisting(ChartDataSnapshot data, out ChartDataIndex? index) => Cache.TryGetValue(data.CoordinateCacheOwner, out index);

    private ChartDataIndex(ChartDataSnapshot data)
    {
        _data = data;
        Interlocked.Increment(ref _buildCount);
        var extrema = ChartDataExtrema.For(data);
        var count = extrema.SourceCount;
        var finiteX = extrema.Count;
        var ascending = extrema.AscendingX;
        var descending = extrema.DescendingX;
        var coordinates = data.Coordinates.Span;
        Bounds = extrema.Bounds;
        Count = finiteX;
        if (finiteX == count && (ascending || descending)) { _descending = !ascending; return; }
        _ordinals = new int[finiteX];
        // Compact snapshots sort their ordinal indices directly. Mapped/overlaid snapshots
        // cache X keys once so sorting never projects a metadata value again.
        var keys = !ascending && coordinates.IsEmpty ? new double[finiteX] : null;
        for (int ordinal = 0, index = 0; ordinal < count; ordinal++)
        {
            var point = coordinates.IsEmpty ? data.GetValue(ordinal).Point : coordinates[ordinal];
            if (!double.IsFinite(point.X) || !double.IsFinite(point.Y)) continue;
            _ordinals[index] = ordinal;
            if (keys is not null) keys[index] = point.X;
            index++;
        }
        if (!ascending)
        {
            if (keys is null)
                SortCoordinates(_ordinals.AsSpan(), coordinates);
            else
            {
                keys.AsSpan().Sort(_ordinals.AsSpan());
                // Keyed sort is unstable. Restore physical order within equal-X runs,
                // including -0/+0, without changing either the raw data or identities.
                for (var start = 0; start < keys.Length;)
                {
                    var end = start + 1;
                    while (end < keys.Length && keys[end] == keys[start]) end++;
                    if (end - start > 1) _ordinals.AsSpan(start, end - start).Sort();
                    start = end;
                }
            }
        }
    }

    private static void SortCoordinates(Span<int> ordinals, ReadOnlySpan<ChartPoint> coordinates)
    {
        // Stable radix passes preserve physical order for equal X without indirect
        // O(N log N) snapshot reads. Only these temporary ordinal/bucket arrays are released;
        // the cached index retains its original int[finite-count] and no coordinate copy.
        var scratch = new int[ordinals.Length];
        var digitBits = ordinals.Length < 65_536 ? 8 : 16;
        Span<int> counts = digitBits == 8 ? stackalloc int[256] : new int[65_536];
        var mask = (ulong)(counts.Length - 1);
        Span<int> source = ordinals;
        Span<int> destination = scratch;
        var inScratch = false;
        for (var shift = 0; shift < 64; shift += digitBits)
        {
            counts.Clear();
            foreach (var ordinal in source) counts[(int)(SortableKey(coordinates[ordinal].X) >> shift & mask)]++;
            var offset = 0;
            var constantDigit = false;
            for (var digit = 0; digit < counts.Length; digit++)
            {
                var count = counts[digit];
                if (count == source.Length) { constantDigit = true; break; }
                counts[digit] = offset;
                offset += count;
            }
            if (constantDigit) continue;
            foreach (var ordinal in source)
                destination[counts[(int)(SortableKey(coordinates[ordinal].X) >> shift & mask)]++] = ordinal;
            var previous = source; source = destination; destination = previous;
            inScratch = !inScratch;
        }
        if (inScratch) source.CopyTo(ordinals);
    }

    private static ulong SortableKey(double value)
    {
        // The index excludes nonfinite X/Y. Canonicalize signed zero because Double.CompareTo
        // treats -0 and +0 as equal; all other finite values keep their exact 64-bit order.
        var bits = value == 0 ? 0UL : BitConverter.DoubleToUInt64Bits(value);
        return (bits & 0x8000_0000_0000_0000UL) == 0 ? bits ^ 0x8000_0000_0000_0000UL : ~bits;
    }

    public int Ordinal(int position) => _ordinals is not null ? _ordinals[position] : _descending ? Count - 1 - position : position;

    public (int Start, int End, bool Reverse) ScreenRange(IChartProjection projection, double minimum, double maximum)
    {
        // Compare the same forward mapping used by hit distances. Inverse logarithmic mapping
        // can differ by hundreds of raw-value ULPs over a wide domain and omit edge hits.
        var reverse = Count > 1 && projection.MapX(_data.GetValue(Ordinal(0)).Point.X) >
            projection.MapX(_data.GetValue(Ordinal(Count - 1)).Point.X);
        int Bound(double value, bool upper)
        {
            var low = 0; var high = Count;
            while (low < high)
            {
                var mid = low + (high - low) / 2;
                var x = projection.MapX(_data.GetValue(Ordinal(reverse ? Count - 1 - mid : mid)).Point.X);
                if (x < value || upper && x == value) low = mid + 1; else high = mid;
            }
            return low;
        }
        return (Bound(minimum, false), Bound(maximum, true), reverse);
    }
}
