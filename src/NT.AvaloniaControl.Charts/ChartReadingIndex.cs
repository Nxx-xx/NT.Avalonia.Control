using System.Runtime.CompilerServices;

namespace NT.AvaloniaControl.Charts;

/// <summary>A lazy snapshot-local Y index, built only when SharedY is requested.</summary>
internal sealed class ChartReadingIndex
{
    private static readonly ConditionalWeakTable<ChartDataSnapshot, ChartReadingIndex> Cache = new();
    private readonly ChartDataSnapshot _data;
    private readonly int[]? _ordinals;
    private readonly bool _descending;
    private static long _buildCount;
    internal static long BuildCount => Interlocked.Read(ref _buildCount);
    public int Count { get; }
    public static ChartReadingIndex For(ChartDataSnapshot data) => Cache.GetValue(data.CoordinateCacheOwner, static item => new(item));
    internal static bool TryGetExisting(ChartDataSnapshot data, out ChartReadingIndex? index) => Cache.TryGetValue(data.CoordinateCacheOwner, out index);

    private ChartReadingIndex(ChartDataSnapshot data)
    {
        _data = data;
        Interlocked.Increment(ref _buildCount);
        var extrema = ChartDataExtrema.For(data);
        var ascending = extrema.AscendingY; var descending = extrema.DescendingY; var count = extrema.Count;
        var sourceCount = extrema.SourceCount;
        var coordinates = data.Coordinates.Span;
        Count = count;
        if (count == sourceCount && (ascending || descending)) { _descending = !ascending; return; }
        _ordinals = new int[count];
        var keys = ascending ? null : new double[count];
        for (int ordinal = 0, index = 0; ordinal < sourceCount; ordinal++)
        {
            var point = coordinates.IsEmpty ? data.GetValue(ordinal).Point : coordinates[ordinal];
            if (!double.IsFinite(point.X) || !double.IsFinite(point.Y)) continue;
            _ordinals[index] = ordinal;
            if (keys is not null) keys[index] = point.Y;
            index++;
        }
        if (keys is null) return;
        keys.AsSpan().Sort(_ordinals.AsSpan());
        // Keyed sort is unstable; preserve physical order for equal Y, including +/-0.
        for (var start = 0; start < count;)
        {
            var end = start + 1;
            while (end < count && keys[end] == keys[start]) end++;
            if (end - start > 1) _ordinals.AsSpan(start, end - start).Sort();
            start = end;
        }
    }

    public int Ordinal(int position) => _ordinals is not null ? _ordinals[position] : _descending ? Count - 1 - position : position;

    public (int Start, int End, bool Reverse) ScreenRange(IChartProjection projection, double minimum, double maximum)
    {
        var reverse = Count > 1 && projection.MapY(_data.GetValue(Ordinal(0)).Point.Y) >
            projection.MapY(_data.GetValue(Ordinal(Count - 1)).Point.Y);
        int Bound(double value, bool upper)
        {
            var low = 0; var high = Count;
            while (low < high)
            {
                var middle = low + (high - low) / 2;
                var mapped = projection.MapY(_data.GetValue(Ordinal(reverse ? Count - 1 - middle : middle)).Point.Y);
                if (mapped < value || upper && mapped == value) low = middle + 1; else high = middle;
            }
            return low;
        }
        return (Bound(minimum, false), Bound(maximum, true), reverse);
    }
}
