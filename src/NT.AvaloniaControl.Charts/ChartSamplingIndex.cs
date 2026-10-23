using System.Runtime.CompilerServices;

namespace NT.AvaloniaControl.Charts;

/// <summary>
/// Snapshot-local, display-only min/max hierarchy. Leaves summarize 32 physical
/// source ordinals; no coordinates or source metadata are copied. Queries retain
/// the first, last and both extrema of each occupied physical pixel column.
/// </summary>
internal sealed class ChartSamplingIndex
{
    private const int LeafSize = 32;
    private const int MaximumBuckets = 1_000_000;
    private sealed class Entry { internal readonly object Sync = new(); internal ChartSamplingIndex? Index; }
    private readonly record struct Run(int Start, int End);
    private static readonly ConditionalWeakTable<ChartDataSnapshot, Entry> Cache = new();
    private static long _buildCount, _queryCount, _visitedCount, _outputCount;
    private readonly ChartDataSnapshot _data;
    private readonly Run[] _runs;
    private readonly int[] _minimum, _maximum;
    private readonly int _leafBase;

    internal static long BuildCount => Interlocked.Read(ref _buildCount);
    internal static long QueryCount => Interlocked.Read(ref _queryCount);
    internal static long VisitedCount => Interlocked.Read(ref _visitedCount);
    internal static long OutputCount => Interlocked.Read(ref _outputCount);
    internal bool IsMonotonicX { get; }
    internal int RunCount => _runs.Length;
    // Owned array payload only; source storage, object headers and the weak-cache
    // entry are reported separately by the process/heap diagnostics.
    internal long EstimatedBytes => ((long)_minimum.Length + _maximum.Length) * sizeof(int) + (long)_runs.Length * 2 * sizeof(int);

    internal static ChartSamplingIndex For(ChartDataSnapshot data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        cancellationToken.ThrowIfCancellationRequested();
        var owner = data.CoordinateCacheOwner;
        var entry = Cache.GetValue(owner, static _ => new());
        lock (entry.Sync)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Construct locally: cancellation or a failed construction never
            // publishes a partially initialized hierarchy to a later revision.
            if (entry.Index is null) entry.Index = new(owner, cancellationToken);
            return entry.Index;
        }
    }

    private ChartSamplingIndex(ChartDataSnapshot data, CancellationToken cancellationToken)
    {
        _data = data;
        var leaves = Math.Max(1, (int)(((long)data.Count + LeafSize - 1) / LeafSize));
        var leafBase = 1;
        while (leafBase < leaves) leafBase = checked(leafBase * 2);
        _leafBase = leafBase;
        _minimum = new int[checked(leafBase * 2)]; _maximum = new int[_minimum.Length];
        Array.Fill(_minimum, -1); Array.Fill(_maximum, -1);
        var runs = new List<Run>();
        var runStart = -1;
        var previousX = 0d; var havePrevious = false;
        var ascending = true; var descending = true;
        for (var ordinal = 0; ordinal < data.Count; ordinal++)
        {
            if ((ordinal & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
            var point = data.GetValue(ordinal).Point;
            if (!Finite(point))
            {
                if (runStart >= 0) runs.Add(new(runStart, ordinal));
                runStart = -1; continue;
            }
            if (runStart < 0) runStart = ordinal;
            if (havePrevious) { ascending &= point.X >= previousX; descending &= point.X <= previousX; }
            previousX = point.X; havePrevious = true;
            var leaf = leafBase + ordinal / LeafSize;
            _minimum[leaf] = Minimum(_minimum[leaf], ordinal);
            _maximum[leaf] = Maximum(_maximum[leaf], ordinal);
        }
        if (runStart >= 0) runs.Add(new(runStart, data.Count));
        for (var node = leafBase - 1; node > 0; node--)
        {
            if ((node & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
            _minimum[node] = Minimum(_minimum[node * 2], _minimum[node * 2 + 1]);
            _maximum[node] = Maximum(_maximum[node * 2], _maximum[node * 2 + 1]);
        }
        _runs = runs.ToArray(); IsMonotonicX = ascending || descending;
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _buildCount);
    }

    /// <summary>
    /// The caller proves a continuous monotonic forward mapping and applicable
    /// line semantics. Screen bounds are inclusive, in DIPs. Pixel bins are
    /// floor((MapX(X)-left)*renderScale/pixelWidth), relative to this plot view.
    /// </summary>
    internal bool TryQuery(Func<double, double> mapX, double left, double right, double renderScale,
        int pixelWidth, out ChartLineSampling sampling, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mapX);
        cancellationToken.ThrowIfCancellationRequested();
        sampling = null!;
        if (!IsMonotonicX || !double.IsFinite(left) || !double.IsFinite(right) || right < left ||
            !double.IsFinite(renderScale) || renderScale <= 0 || pixelWidth is < 1 or > 64) return false;
        var budget = Math.Floor((right - left) * renderScale / pixelWidth) + 1;
        if (!double.IsFinite(budget) || budget > MaximumBuckets) return false;
        var work = new QueryWork(mapX, left, renderScale, pixelWidth, cancellationToken);
        var ordinals = new List<int>(); var starts = new List<bool>();
        var rawVisible = 0; var input = 0; var buckets = 0; var figures = 0;
        Span<int> kept = stackalloc int[4];
        try
        {
            // Runs remain in original physical order even on descending axes.
            foreach (var run in _runs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var first = Map(run.Start, ref work); var last = Map(run.End - 1, ref work);
                if (Math.Max(first, last) < left || Math.Min(first, last) > right) continue;
                var increasing = first <= last;
                var low = Bound(run, left, false, increasing, ref work);
                var high = Bound(run, right, true, increasing, ref work);
                var start = increasing ? run.Start + low : run.End - high;
                var end = increasing ? run.Start + high : run.End - low;
                var inputStart = Math.Max(run.Start, start - 1);
                var inputEnd = Math.Min(run.End, end + 1);
                if (inputStart == inputEnd) continue;
                rawVisible += end - start; input += inputEnd - inputStart;
                var firstOutput = ordinals.Count;
                if (inputStart < start) Add(inputStart);
                var cursor = start;
                while (cursor < end)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var bucket = Bucket(cursor, ref work);
                    var bucketEnd = EndOfBucket(cursor, end, bucket, ref work);
                    var (minimum, maximum) = Extrema(cursor, bucketEnd, ref work);
                    kept[0] = cursor; kept[1] = bucketEnd - 1; kept[2] = minimum; kept[3] = maximum;
                    kept.Sort();
                    for (var i = 0; i < kept.Length; i++)
                        if (i == 0 || kept[i] != kept[i - 1]) Add(kept[i]);
                    buckets++; cursor = bucketEnd;
                }
                if (inputEnd > end) Add(inputEnd - 1);
                if (ordinals.Count > firstOutput) { starts[firstOutput] = true; figures++; }
            }
        }
        catch (InvalidMappingException) { return false; }
        cancellationToken.ThrowIfCancellationRequested();
        sampling = new(_data.Version, _data.Count, ordinals.ToArray(), starts.ToArray(), rawVisible, input,
            buckets, (int)budget, figures);
        Interlocked.Increment(ref _queryCount); Interlocked.Add(ref _visitedCount, work.Visited);
        Interlocked.Add(ref _outputCount, sampling.Count);
        return true;

        void Add(int ordinal)
        {
            // Adjacent bins can share neither source rows nor metadata. Boundary
            // neighbors are added once, and no point is synthesized or averaged.
            if (ordinals.Count > 0 && ordinals[^1] == ordinal) return;
            ordinals.Add(ordinal); starts.Add(false);
        }
    }

    private struct QueryWork(Func<double, double> mapX, double left, double renderScale, int pixelWidth, CancellationToken token)
    {
        internal readonly Func<double, double> MapX = mapX;
        internal readonly double Left = left, RenderScale = renderScale;
        internal readonly int PixelWidth = pixelWidth;
        internal readonly CancellationToken Token = token;
        internal long Visited;
    }
    private sealed class InvalidMappingException : Exception;
    private double Map(int ordinal, ref QueryWork work)
    {
        if ((work.Visited++ & 255) == 0) work.Token.ThrowIfCancellationRequested();
        var value = work.MapX(_data.GetValue(ordinal).Point.X);
        if (!double.IsFinite(value)) throw new InvalidMappingException();
        return value;
    }
    private double Bucket(int ordinal, ref QueryWork work)
    {
        var value = Math.Floor((Map(ordinal, ref work) - work.Left) * work.RenderScale / work.PixelWidth);
        if (!double.IsFinite(value)) throw new InvalidMappingException();
        return value;
    }
    private int Bound(Run run, double value, bool upper, bool increasing, ref QueryWork work)
    {
        var low = 0; var high = run.End - run.Start;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            var mapped = Map(increasing ? run.Start + middle : run.End - 1 - middle, ref work);
            if (mapped < value || upper && mapped == value) low = middle + 1; else high = middle;
        }
        return low;
    }
    private int EndOfBucket(int start, int end, double bucket, ref QueryWork work)
    {
        var low = start + 1; var high = end;
        // Source rows in one pixel column form a contiguous range in either
        // monotonic direction. Find its local bracket before binary searching;
        // searching the entire remaining source for every column wastes work
        // when a full view contains many small columns. A small initial stride
        // also avoids redundant probes at offsets 1, 2 and 4 in dense columns.
        long stride = 8;
        while ((long)start + stride < end)
        {
            var probe = (int)(start + stride);
            if (Bucket(probe, ref work) != bucket) { high = probe; break; }
            low = probe + 1; stride *= 2;
        }
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (Bucket(middle, ref work) == bucket) low = middle + 1; else high = middle;
        }
        return low;
    }
    private (int Minimum, int Maximum) Extrema(int start, int end, ref QueryWork work)
    {
        var minimum = -1; var maximum = -1;
        while (start < end && start % LeafSize != 0)
        {
            work.Visited++; minimum = Minimum(minimum, start); maximum = Maximum(maximum, start); start++;
        }
        while (start < end && end % LeafSize != 0)
        {
            end--; work.Visited++; minimum = Minimum(minimum, end); maximum = Maximum(maximum, end);
        }
        var left = _leafBase + start / LeafSize; var right = _leafBase + end / LeafSize;
        while (left < right)
        {
            if ((left & 1) != 0)
            {
                work.Visited++; minimum = Minimum(minimum, _minimum[left]); maximum = Maximum(maximum, _maximum[left]); left++;
            }
            if ((right & 1) != 0)
            {
                right--; work.Visited++; minimum = Minimum(minimum, _minimum[right]); maximum = Maximum(maximum, _maximum[right]);
            }
            left /= 2; right /= 2;
        }
        return (minimum, maximum);
    }
    private int Minimum(int first, int second)
    {
        if (first < 0) return second; if (second < 0) return first;
        var comparison = _data.GetValue(second).Point.Y.CompareTo(_data.GetValue(first).Point.Y);
        return comparison < 0 ? second : comparison > 0 ? first : Math.Min(first, second);
    }
    private int Maximum(int first, int second)
    {
        if (first < 0) return second; if (second < 0) return first;
        var comparison = _data.GetValue(second).Point.Y.CompareTo(_data.GetValue(first).Point.Y);
        return comparison > 0 ? second : comparison < 0 ? first : Math.Min(first, second);
    }
    private static bool Finite(ChartPoint point) => double.IsFinite(point.X) && double.IsFinite(point.Y);
}
