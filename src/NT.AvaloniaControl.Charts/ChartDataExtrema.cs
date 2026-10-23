using System.Runtime.CompilerServices;

namespace NT.AvaloniaControl.Charts;

/// <summary>Constant-size numeric summary, independent from lazily built hit indexes.</summary>
internal sealed class ChartDataExtrema
{
    private static readonly ConditionalWeakTable<ChartDataSnapshot, ChartDataExtrema> Cache = new();
    private static long _buildCount;
    internal static long BuildCount => Interlocked.Read(ref _buildCount);
    public ChartSeriesBounds? Bounds { get; }
    public int SourceCount { get; }
    public int Count { get; }
    public bool AscendingX { get; }
    public bool DescendingX { get; }
    public bool AscendingY { get; }
    public bool DescendingY { get; }

    public static ChartDataExtrema For(ChartDataSnapshot data) => Cache.GetValue(data.CoordinateCacheOwner, static value => new(value));
    internal static bool TryGetExisting(ChartDataSnapshot data, out ChartDataExtrema? extrema) =>
        Cache.TryGetValue(data.CoordinateCacheOwner, out extrema);

    private ChartDataExtrema(ChartDataSnapshot data)
    {
        Interlocked.Increment(ref _buildCount);
        SourceCount = data.Count;
        if (data.TryGetSummary(out var summary))
        {
            Count = summary.Count; Bounds = summary.Bounds;
            AscendingX = summary.AscendingX; DescendingX = summary.DescendingX;
            AscendingY = summary.AscendingY; DescendingY = summary.DescendingY;
            return;
        }
        var minX = double.PositiveInfinity; var maxX = double.NegativeInfinity;
        var minY = double.PositiveInfinity; var maxY = double.NegativeInfinity;
        var ascendingX = true; var descendingX = true;
        var ascendingY = true; var descendingY = true;
        var previous = default(ChartPoint); var count = 0;
        var coordinates = data.Coordinates.Span;
        for (var ordinal = 0; ordinal < SourceCount; ordinal++)
        {
            var point = coordinates.IsEmpty ? data.GetValue(ordinal).Point : coordinates[ordinal];
            if (!double.IsFinite(point.X) || !double.IsFinite(point.Y)) continue;
            if (count > 0)
            {
                ascendingX &= point.X >= previous.X; descendingX &= point.X <= previous.X;
                ascendingY &= point.Y >= previous.Y; descendingY &= point.Y <= previous.Y;
            }
            previous = point; count++;
            minX = Math.Min(minX, point.X); maxX = Math.Max(maxX, point.X);
            minY = Math.Min(minY, point.Y); maxY = Math.Max(maxY, point.Y);
        }
        Count = count;
        AscendingX = ascendingX; DescendingX = descendingX;
        AscendingY = ascendingY; DescendingY = descendingY;
        Bounds = count > 0 ? new(minX, maxX, minY, maxY) : null;
    }
}

internal readonly record struct ChartValueBounds(double Minimum, double Maximum);

/// <summary>Snapshot-local lower-band extrema, without rebuilding them for each axis/view.</summary>
internal sealed class ChartLowerExtrema
{
    private sealed class Entry
    {
        public readonly object Sync = new();
        public ChartLowerExtrema? Band;
        public ChartLowerExtrema? SourceLow;
    }
    private static readonly ConditionalWeakTable<ChartDataSnapshot, Entry> Cache = new();
    private static long _buildCount;
    private readonly ChartDataSnapshot _data;
    private readonly bool _useSourceLow;
    private readonly ChartValueBounds? _positive;
    private readonly ChartValueBounds? _dateTime;
    private readonly bool _hasMissingLower;
    internal static long BuildCount => Interlocked.Read(ref _buildCount);
    public ChartValueBounds? Bounds { get; }

    public static ChartLowerExtrema For(ChartDataSnapshot data, bool useSourceLow = false)
    {
        data = data.CoordinateCacheOwner;
        var entry = Cache.GetValue(data, static _ => new());
        lock (entry.Sync)
        {
            if (useSourceLow) return entry.SourceLow ??= new(data, true);
            return entry.Band ??= new(data, false);
        }
    }

    private ChartLowerExtrema(ChartDataSnapshot data, bool useSourceLow)
    {
        Interlocked.Increment(ref _buildCount);
        _data = data; _useSourceLow = useSourceLow;
        var minimum = double.PositiveInfinity; var maximum = double.NegativeInfinity;
        var positiveMinimum = double.PositiveInfinity; var positiveMaximum = double.NegativeInfinity;
        var dateMinimum = double.PositiveInfinity; var dateMaximum = double.NegativeInfinity;
        var count = data.Count;
        for (var ordinal = 0; ordinal < count; ordinal++)
        {
            if (data.GetValue(ordinal).IsEmpty) continue;
            var lower = Lower(ordinal);
            if (lower is null) { _hasMissingLower = true; continue; }
            var value = lower.Value;
            if (!double.IsFinite(value)) continue;
            minimum = Math.Min(minimum, value); maximum = Math.Max(maximum, value);
            if (value > 0) { positiveMinimum = Math.Min(positiveMinimum, value); positiveMaximum = Math.Max(positiveMaximum, value); }
            if (value >= DateTimeAxis.MinimumUnixMilliseconds && value <= DateTimeAxis.MaximumUnixMilliseconds)
            { dateMinimum = Math.Min(dateMinimum, value); dateMaximum = Math.Max(dateMaximum, value); }
        }
        Bounds = Make(minimum, maximum);
        _positive = Make(positiveMinimum, positiveMaximum);
        _dateTime = Make(dateMinimum, dateMaximum);
    }

    private double? Lower(int ordinal) => _data.GetBandLower(ordinal) ?? (_useSourceLow ? _data.Points[ordinal].Low : null);
    private static ChartValueBounds? Make(double minimum, double maximum) => double.IsFinite(minimum) ? new(minimum, maximum) : null;

    public ChartValueBounds? BoundsWithFallback(double fallback)
    {
        if (!_hasMissingLower || !double.IsFinite(fallback)) return Bounds;
        return Bounds is { } bounds ? new(Math.Min(bounds.Minimum, fallback), Math.Max(bounds.Maximum, fallback)) : new(fallback, fallback);
    }

    public ChartValueBounds? ForAxis(ChartAxis axis)
    {
        var type = axis.GetType();
        if (type == typeof(ChartAxis) || type == typeof(NumericAxis) || type == typeof(CategoryAxis)) return Bounds;
        if (type == typeof(LogarithmicAxis)) return _positive;
        if (type == typeof(DateTimeAxis)) return _dateTime;
        // A custom validity predicate may change with any caller-owned state.
        // Preserve its exact behavior instead of reusing a built-in domain summary.
        var minimum = double.PositiveInfinity; var maximum = double.NegativeInfinity;
        var count = _data.Count;
        for (var ordinal = 0; ordinal < count; ordinal++)
            if (!_data.GetValue(ordinal).IsEmpty && Lower(ordinal) is { } value && axis.IsValidValue(value))
            { minimum = Math.Min(minimum, value); maximum = Math.Max(maximum, value); }
        return Make(minimum, maximum);
    }
}
