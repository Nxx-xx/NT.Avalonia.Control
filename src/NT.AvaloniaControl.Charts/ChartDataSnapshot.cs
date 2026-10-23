using System.Collections;
using System.Collections.ObjectModel;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>How missing numeric values are projected without modifying the original data.</summary>
public enum EmptyPointMode { Gap, Zero, Average, Interpolate }

/// <summary>A projected value retaining its original item, index and category.</summary>
public readonly record struct ChartDataPoint(int SourceIndex, object? StableId, object? Item,
    object? Category, ChartPoint Point, bool IsEmpty, double? High = null, double? Low = null, double? Weight = null)
{
    /// <summary>Whether the original value was missing before the empty-point policy.</summary>
    public bool WasEmpty { get; init; }
    /// <summary>The original mapped value, retained for categorical value axes. Direct point and numeric sources leave this null.</summary>
    public object? Value { get; init; }
    /// <summary>Mapped source coordinate before a range/stack display transformation; null for ordinary points.</summary>
    public ChartPoint? RawPoint { get; init; }
    /// <summary>Displayed lower boundary of an area/range/stack, when supplied.</summary>
    public double? BaseValue { get; init; }
    /// <summary>Original stack contribution, or its signed percentage share. Null for unstacked points.</summary>
    public double? StackValue { get; init; }
    /// <summary>Cumulative percentage at this Pareto category, when supplied.</summary>
    public double? CumulativeValue { get; init; }
    /// <summary>Whether a waterfall row is an explicit total that resets the running value.</summary>
    public bool IsTotal { get; init; }
    /// <summary>Mapped size input before normalization. Missing or invalid values do not invent a source item.</summary>
    public double? SizeValue { get; init; }
    /// <summary>Mapped numeric color input or direct color/brush value.</summary>
    public object? ColorValue { get; init; }
    /// <summary>Current displayed marker diameter in DIPs, when point encoding is active.</summary>
    public double? MarkerDiameter { get; init; }
    /// <summary>Current displayed point fill, when point encoding is active.</summary>
    public IBrush? PointFill { get; init; }
    /// <summary>Immutable box summary of this original group; outlier entries retain their nested observation indices.</summary>
    public ChartBoxPlotSummary? BoxPlot { get; init; }
    /// <summary>Displayed OHLC with the true original source range; transformed glyphs are not raw observations.</summary>
    public ChartFinancialSample? Financial { get; init; }
    /// <summary>Calculation provenance. Synthetic fitted samples have no original source item or index.</summary>
    public ChartAnalysisSample? Analysis { get; init; }
}

public sealed record ChartDataIssue(int SourceIndex, string? Path, string Message);
public sealed class ChartDataErrorEventArgs(ChartDataIssue issue) : EventArgs
{
    public ChartDataIssue Issue { get; } = issue;
}

internal sealed class ChartDataMappingException(string message) : InvalidOperationException(message);

internal readonly record struct ChartDataValue(int SourceIndex, ChartPoint Point, bool IsEmpty, bool WasEmpty);
internal readonly record struct ChartBandValue(double Lower, double? Contribution, ChartPoint RawPoint, double? CumulativeValue = null);
internal readonly record struct ChartPointStyle(double Diameter, IBrush? Fill);

internal readonly record struct ChartSnapshotSummary(int Count, ChartSeriesBounds? Bounds, ChartPoint First, ChartPoint Last,
    bool AscendingX, bool DescendingX, bool AscendingY, bool DescendingY)
{
    public static ChartSnapshotSummary Empty => new(0, null, default, default, true, true, true, true);
    public ChartSnapshotSummary Append(ChartPoint point)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y)) return this;
        var bounds = Bounds is { } old ? new ChartSeriesBounds(Math.Min(old.XMinimum, point.X), Math.Max(old.XMaximum, point.X),
            Math.Min(old.YMinimum, point.Y), Math.Max(old.YMaximum, point.Y)) : new(point.X, point.X, point.Y, point.Y);
        return new(Count + 1, bounds, Count == 0 ? point : First, point,
            AscendingX && (Count == 0 || point.X >= Last.X), DescendingX && (Count == 0 || point.X <= Last.X),
            AscendingY && (Count == 0 || point.Y >= Last.Y), DescendingY && (Count == 0 || point.Y <= Last.Y));
    }
    public ChartSnapshotSummary Merge(ChartSnapshotSummary next)
    {
        if (Count == 0) return next;
        if (next.Count == 0) return this;
        var a = Bounds!.Value; var b = next.Bounds!.Value;
        return new(checked(Count + next.Count), new(Math.Min(a.XMinimum, b.XMinimum), Math.Max(a.XMaximum, b.XMaximum),
            Math.Min(a.YMinimum, b.YMinimum), Math.Max(a.YMaximum, b.YMaximum)), First, next.Last,
            AscendingX && next.AscendingX && next.First.X >= Last.X, DescendingX && next.DescendingX && next.First.X <= Last.X,
            AscendingY && next.AscendingY && next.First.Y >= Last.Y, DescendingY && next.DescendingY && next.First.Y <= Last.Y);
    }
    public ChartSnapshotSummary ShiftX(int offset) => Count == 0 ? this : this with
    {
        Bounds = new ChartSeriesBounds(Bounds!.Value.XMinimum + offset, Bounds.Value.XMaximum + offset, Bounds.Value.YMinimum, Bounds.Value.YMaximum),
        First = First with { X = First.X + offset }, Last = Last with { X = Last.X + offset }
    };
}

internal interface IChartSnapshotData
{
    int Count { get; }
    bool HasOrdinalSourceIndices { get; }
    ReadOnlyMemory<ChartPoint> Coordinates { get; }
    ChartDataValue GetValue(int index);
    ChartDataPoint GetPoint(int index);
    bool TryFindIdentity(object identity, bool valueEquality, out int ordinal);
    bool TryGetSummary(out ChartSnapshotSummary summary) { summary = default; return false; }
}

/// <summary>An immutable projection of one source version. Items remain caller-owned.</summary>
public sealed record ChartDataSnapshot
{
    private readonly IChartSnapshotData _data;
    private readonly ChartDataSnapshot? _parent;
    private readonly ChartDataValue[]? _values;
    private readonly ChartBandValue[]? _bands;
    private readonly ChartPointStyle[]? _pointStyles;
    private readonly ChartDataSnapshot? _coordinateCacheOwner;
    private readonly ChartDataSnapshot? _rawPointsSource;
    private readonly object _indexLock = new();
    private Dictionary<int, int>? _sourceOrdinals;

    public ChartDataSnapshot(long version, IReadOnlyList<ChartDataPoint> points, IReadOnlyList<ChartDataIssue>? issues = null)
        : this(version, new ArraySnapshotData(CopyPoints(points)), CopyIssues(issues)) { }

    private ChartDataSnapshot(long version, IChartSnapshotData data, IReadOnlyList<ChartDataIssue> issues,
        ChartDataSnapshot? parent = null, ChartDataValue[]? values = null, ChartBandValue[]? bands = null, ChartPointStyle[]? pointStyles = null,
        ChartDataSnapshot? coordinateCacheOwner = null, ChartDataSnapshot? rawPointsSource = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(version);
        Version = version;
        _data = data;
        _parent = parent;
        _values = values;
        _bands = bands;
        _pointStyles = pointStyles;
        _coordinateCacheOwner = coordinateCacheOwner;
        _rawPointsSource = rawPointsSource;
        Points = new PointList(this);
        Issues = issues;
    }
    public long Version { get; }
    public IReadOnlyList<ChartDataPoint> Points { get; }
    public IReadOnlyList<ChartDataIssue> Issues { get; }
    internal int Count => _data.Count;
    internal ReadOnlyMemory<ChartPoint> Coordinates => _values is null ? _data.Coordinates : ReadOnlyMemory<ChartPoint>.Empty;
    // Marker paint/size overlays do not alter coordinates, gaps, source ordinals or
    // bands. They reuse numeric caches without retaining any transformed point copy.
    internal ChartDataSnapshot CoordinateCacheOwner => _coordinateCacheOwner ?? this;

    internal bool TryGetSummary(out ChartSnapshotSummary summary)
    {
        if (_values is null) return _data.TryGetSummary(out summary);
        summary = default; return false;
    }

    internal ChartDataValue GetValue(int index) => _values is { } values ? values[index] : _data.GetValue(index);
    internal double? GetBandLower(int index) => _bands is { } bands ? bands[index].Lower : null;
    internal ChartBandValue? GetBandValue(int index) => _bands is { } bands ? bands[index] : null;
    internal ChartPointStyle? GetPointStyle(int index) => _pointStyles is { } styles ? styles[index] : null;

    /// <summary>Takes exclusive array ownership; callers must never mutate the array again.</summary>
    internal static ChartDataSnapshot FromOwned(long version, ChartDataPoint[] ownedPoints, IReadOnlyList<ChartDataIssue>? issues = null)
    {
        ArgumentNullException.ThrowIfNull(ownedPoints);
        return new(version, new ArraySnapshotData(ownedPoints), CopyIssues(issues));
    }

    internal static ChartDataSnapshot FromCompact(long version, IChartSnapshotData data) => new(version, data, Array.Empty<ChartDataIssue>());

    internal static ChartDataSnapshot FromBuffered(long version, IChartSnapshotData data, IReadOnlyList<ChartDataIssue> issues) => new(version, data, CopyIssues(issues));

    /// <summary>Takes exclusive ownership of an equally ordered coordinate overlay.</summary>
    internal ChartDataSnapshot WithValues(ChartDataValue[] ownedValues)
    {
        ValidateValues(ownedValues);
        return new(Version, _data, Issues, _parent ?? this, ownedValues, _bands, _pointStyles, rawPointsSource: _rawPointsSource);
    }

    private void ValidateValues(ChartDataValue[] ownedValues)
    {
        ArgumentNullException.ThrowIfNull(ownedValues);
        var count = Count;
        if (ownedValues.Length != count) throw new ArgumentException("An overlay must retain the snapshot count.", nameof(ownedValues));
        var ordinalSourceIndices = _data.HasOrdinalSourceIndices;
        for (var index = 0; index < count; index++)
            if (ownedValues[index].SourceIndex != (ordinalSourceIndices ? index : GetValue(index).SourceIndex))
                throw new ArgumentException("An overlay must retain the snapshot's source-index order.", nameof(ownedValues));
    }

    /// <summary>Takes ownership of a display overlay, retaining original items and source ordering.</summary>
    internal ChartDataSnapshot WithBandValues(ChartDataValue[] ownedValues, ChartBandValue[] ownedBands)
    {
        ArgumentNullException.ThrowIfNull(ownedBands);
        if (ownedBands.Length != Count) throw new ArgumentException("A band must retain the snapshot count.", nameof(ownedBands));
        ValidateValues(ownedValues);
        return new(Version, _data, Issues, _parent ?? this, ownedValues, ownedBands, _pointStyles);
    }

    internal ChartDataSnapshot WithPointStyles(ChartPointStyle[] ownedStyles)
    {
        ArgumentNullException.ThrowIfNull(ownedStyles);
        if (ownedStyles.Length != Count) throw new ArgumentException("Point styles must retain the snapshot count.", nameof(ownedStyles));
        return new(Version, _data, Issues, _parent ?? this, _values, _bands, ownedStyles, CoordinateCacheOwner, _rawPointsSource);
    }

    // Capture source coordinates lazily as metadata, before categorical/display
    // transforms. No source items, point records or coordinate arrays are copied.
    internal ChartDataSnapshot WithRawPoints() => new(Version, _data, Issues, _parent ?? this,
        _values, _bands, _pointStyles, CoordinateCacheOwner, this);

    internal bool TryGetPoint(int sourceIndex, out ChartDataPoint point)
    {
        if (TryGetOrdinal(sourceIndex, out var ordinal)) { point = GetPoint(ordinal); return true; }
        point = default;
        return false;
    }

    internal bool TryFindIdentity(object? identity, out ChartDataPoint point, bool valueEquality = false)
    {
        if (identity is not null && _data.TryFindIdentity(identity, valueEquality, out var ordinal))
        { point = GetPoint(ordinal); return true; }
        point = default;
        return false;
    }

    // Accessibility may reuse an index built by selection, but must never create an
    // N-entry identity/source-index dictionary merely to resolve a virtual child.
    internal bool TryFindExistingIdentity(object? identity, out ChartDataPoint point, bool valueEquality = false)
    {
        if (identity is not null && _data is ArraySnapshotData array && array.TryFindExistingIdentity(identity, valueEquality, out var ordinal))
        { point = GetPoint(ordinal); return true; }
        point = default;
        return false;
    }

    internal bool TryGetPointWithoutIndex(int sourceIndex, out ChartDataPoint point, int searchLimit = 256)
    {
        if (sourceIndex < 0) { point = default; return false; }
        if (_data.HasOrdinalSourceIndices)
        {
            if ((uint)sourceIndex < (uint)Count) { point = GetPoint(sourceIndex); return true; }
            point = default; return false;
        }
        if (TryGetExistingOrdinal(sourceIndex, out var ordinal)) { point = GetPoint(ordinal); return true; }
        var start = Math.Clamp(sourceIndex - searchLimit / 2, 0, Math.Max(0, Count - searchLimit));
        var end = (int)Math.Min(Count, (long)start + searchLimit);
        for (var index = start; index < end; index++)
            if (GetValue(index).SourceIndex == sourceIndex) { point = GetPoint(index); return true; }
        point = default; return false;
    }

    private bool TryGetExistingOrdinal(int sourceIndex, out int ordinal)
    {
        if (_data.HasOrdinalSourceIndices)
        { ordinal = sourceIndex; return (uint)sourceIndex < (uint)Count; }
        if (_parent is not null) return _parent.TryGetExistingOrdinal(sourceIndex, out ordinal);
        lock (_indexLock)
        {
            if (_sourceOrdinals is not null) return _sourceOrdinals.TryGetValue(sourceIndex, out ordinal);
        }
        ordinal = -1; return false;
    }

    private bool TryGetOrdinal(int sourceIndex, out int ordinal)
    {
        if (sourceIndex < 0) { ordinal = -1; return false; }
        if (_data.HasOrdinalSourceIndices)
        { ordinal = sourceIndex; return (uint)sourceIndex < (uint)Count; }
        if (_parent is not null) return _parent.TryGetOrdinal(sourceIndex, out ordinal);
        lock (_indexLock)
        {
            if (_sourceOrdinals is null)
            {
                var lookup = new Dictionary<int, int>(Count);
                for (var index = 0; index < Count; index++) lookup.TryAdd(GetValue(index).SourceIndex, index);
                _sourceOrdinals = lookup;
            }
            return _sourceOrdinals.TryGetValue(sourceIndex, out ordinal);
        }
    }

    private ChartDataPoint GetPoint(int index)
    {
        var point = _data.GetPoint(index);
        if (_values is { } values)
        {
            var value = values[index];
            point = point with { Point = value.Point, IsEmpty = value.IsEmpty, WasEmpty = value.WasEmpty };
        }
        if (_bands is { } bands)
        {
            var band = bands[index];
            point = point with { RawPoint = band.RawPoint, BaseValue = band.Lower, StackValue = band.Contribution, CumulativeValue = band.CumulativeValue };
        }
        if (_rawPointsSource is { } rawSource) point = point with { RawPoint = rawSource.GetValue(index).Point };
        if (_pointStyles is { } styles)
        {
            var style = styles[index];
            point = point with { MarkerDiameter = style.Diameter, PointFill = style.Fill, RawPoint = point.RawPoint ?? _data.GetPoint(index).Point };
        }
        return point;
    }

    private static ChartDataPoint[] CopyPoints(IReadOnlyList<ChartDataPoint> points)
    { ArgumentNullException.ThrowIfNull(points); return points.ToArray(); }

    private static IReadOnlyList<ChartDataIssue> CopyIssues(IReadOnlyList<ChartDataIssue>? issues) =>
        issues is null || issues.Count == 0 ? Array.Empty<ChartDataIssue>() : new ReadOnlyCollection<ChartDataIssue>(issues.ToArray());

    private sealed class PointList(ChartDataSnapshot owner) : IReadOnlyList<ChartDataPoint>
    {
        public int Count => owner.Count;
        public ChartDataPoint this[int index] => owner.GetPoint(index);
        public IEnumerator<ChartDataPoint> GetEnumerator()
        { for (var index = 0; index < Count; index++) yield return owner.GetPoint(index); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class ArraySnapshotData(ChartDataPoint[] points) : IChartSnapshotData
    {
        private readonly object _identityLock = new();
        private Dictionary<object, int>? _referenceIdentities;
        private Dictionary<object, int>? _valueIdentities;
        public int Count => points.Length;
        public ReadOnlyMemory<ChartPoint> Coordinates => ReadOnlyMemory<ChartPoint>.Empty;
        public bool HasOrdinalSourceIndices { get; } = IsOrdered(points);
        public ChartDataPoint GetPoint(int index) => points[index];
        public ChartDataValue GetValue(int index)
        { ref readonly var point = ref points[index]; return new(point.SourceIndex, point.Point, point.IsEmpty, point.WasEmpty); }
        public bool TryFindIdentity(object identity, bool valueEquality, out int ordinal)
        {
            lock (_identityLock)
            {
                var lookup = valueEquality ? _valueIdentities : _referenceIdentities;
                if (lookup is null)
                {
                    lookup = new Dictionary<object, int>(valueEquality ? EqualityComparer<object>.Default : ReferenceEqualityComparer.Instance);
                    for (var index = 0; index < points.Length; index++)
                        if (points[index].StableId is { } id) lookup.TryAdd(id, index);
                    if (valueEquality) _valueIdentities = lookup; else _referenceIdentities = lookup;
                }
                return lookup.TryGetValue(identity, out ordinal);
            }
        }
        internal bool TryFindExistingIdentity(object identity, bool valueEquality, out int ordinal)
        {
            lock (_identityLock)
            {
                var lookup = valueEquality ? _valueIdentities : _referenceIdentities;
                if (lookup is not null) return lookup.TryGetValue(identity, out ordinal);
            }
            ordinal = -1; return false;
        }
        private static bool IsOrdered(ChartDataPoint[] values)
        {
            for (var index = 0; index < values.Length; index++) if (values[index].SourceIndex != index) return false;
            return true;
        }
    }
}
