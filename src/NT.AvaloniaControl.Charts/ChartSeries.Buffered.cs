using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NT.AvaloniaControl.Charts;

public abstract partial class ChartSeries
{
    private IChartDataBufferSource? _bufferSource;
    private ChartBufferState? _bufferState;
    private ConditionalWeakTable<IChartBufferPage, BufferedPageProjection> _bufferMappedPages = new();
    private readonly Dictionary<object, int> _bufferIdCounts = [];
    private int _bufferNullIdCount;
    private int _bufferDuplicateIdCount;
    private readonly Dictionary<INotifyPropertyChanged, int> _bufferObserverCounts = new(ReferenceEqualityComparer.Instance);
    internal static long BufferedMappedRowCount;

    // Storage packing changes pages, not business rows. Transfer only records already mapped
    // by this series, without calling a caller-owned getter or changing the active-ID ledger.
    private void TransferBufferedMappings(ChartBufferState previous, ChartBufferState next)
    {
        if (_bufferSource?.SupportsDirectValues == true && CanUseCompactData()) return;
        var sourceSliceIndex = 0; var sourceLocal = 0;
        foreach (var target in next.Slices)
        {
            BufferedPageProjection? targetProjection = null;
            var destination = 0;
            while (destination < target.Count)
            {
                var source = previous.Slices[sourceSliceIndex];
                var take = Math.Min(target.Count - destination, source.Count - sourceLocal);
                if (_bufferMappedPages.TryGetValue(source.Page, out var sourceProjection))
                    for (var local = 0; local < take; local++)
                    {
                        var sourceSlot = source.Offset + sourceLocal + local;
                        if (!sourceProjection.Ready[sourceSlot]) continue;
                        targetProjection ??= _bufferMappedPages.GetValue(target.Page, static key => new BufferedPageProjection(key.Count));
                        var targetSlot = target.Offset + destination + local;
                        targetProjection.Rows[targetSlot] = sourceProjection.Rows[sourceSlot];
                        targetProjection.Ready[targetSlot] = true;
                        targetProjection.HasIssues |= sourceProjection.Rows[sourceSlot].Issues.Length > 0;
                        Interlocked.Increment(ref ChartDataBufferMetrics.MappedCompactionCopyCount);
                    }
                destination += take; sourceLocal += take;
                if (sourceLocal == source.Count) { sourceSliceIndex++; sourceLocal = 0; }
            }
            // The copied ready slots may have holes when several source batches were never
            // snapshotted. Leave the range cold so the next snapshot checks each slot once.
        }
    }

    private void InvalidateBufferedMapping()
    {
        _bufferMappedPages = new(); _bufferIdCounts.Clear(); _bufferNullIdCount = 0; _bufferDuplicateIdCount = 0;
    }

    private void UpdateBufferedIdentity(object? identity, bool add)
    {
        if (string.IsNullOrWhiteSpace(IdPath)) return;
        if (identity is null) { _bufferNullIdCount += add ? 1 : -1; return; }
        _bufferIdCounts.TryGetValue(identity, out var count);
        if (add)
        {
            if (count == 1) _bufferDuplicateIdCount++;
            _bufferIdCounts[identity] = count + 1;
        }
        else
        {
            if (count == 2) _bufferDuplicateIdCount--;
            if (count <= 1) _bufferIdCounts.Remove(identity); else _bufferIdCounts[identity] = count - 1;
        }
    }

    private void RemoveBufferedIdentities(IEnumerable<ChartBufferSlice> slices)
    {
        if (string.IsNullOrWhiteSpace(IdPath)) return;
        foreach (var slice in slices)
            if (_bufferMappedPages.TryGetValue(slice.Page, out var page))
                for (var local = 0; local < slice.Count; local++)
                {
                    var slot = slice.Offset + local;
                    if (page.Ready[slot]) UpdateBufferedIdentity(page.Rows[slot].Point.StableId, false);
                }
    }

    private ChartDataSnapshot CreateBufferedSnapshot(long version)
    {
        var state = _bufferState!;
        var mappingPages = _bufferMappedPages;
        var direct = _bufferSource!.SupportsDirectValues && CanUseCompactData();
        var projections = direct ? null : new BufferedPageProjection[state.Slices.Length];
        var issues = new List<ChartDataIssue>();
        List<ChartDataIssue>? mappingIssues = null;
        var ordinal = 0;
        for (var sliceIndex = 0; sliceIndex < state.Slices.Length; sliceIndex++)
        {
            var slice = state.Slices[sliceIndex];
            if (!direct)
            {
                var page = mappingPages.GetValue(slice.Page, static key => new BufferedPageProjection(key.Count));
                projections![sliceIndex] = page;
                var mapNeeded = slice.Offset < page.ReadyStart || slice.Offset + slice.Count > page.ReadyEnd;
                if (mapNeeded || page.HasIssues)
                for (var local = 0; local < slice.Count; local++)
                {
                    var slot = slice.Offset + local;
                    if (!page.Ready[slot])
                    {
                        mappingIssues ??= [];
                        var issueStart = mappingIssues.Count;
                        var item = slice.Page.GetItem(slot);
                        var identity = string.IsNullOrWhiteSpace(IdPath) && (item is null || item.GetType().IsValueType) ? slice.Page.GetIdentity(slot) : null;
                        var point = MapItem(item, identity, ordinal + local, mappingIssues, null);
                        // A getter or DataError handler can change the source or mapping synchronously.
                        // Never insert the old row into a replacement mapping epoch's ID registry.
                        if (version != SourceVersion || !ReferenceEquals(mappingPages, _bufferMappedPages) || !ReferenceEquals(state, _bufferState))
                        { InvalidateBufferedMapping(); throw new ChartDataMappingException("source changed while mapping; retry"); }
                        var rowIssues = mappingIssues.Count == issueStart ? Array.Empty<ChartDataIssue>() : mappingIssues.GetRange(issueStart, mappingIssues.Count - issueStart).ToArray();
                        var coordinateItem = item is ChartPoint or Avalonia.Point && string.IsNullOrWhiteSpace(CategoryPath) && string.IsNullOrWhiteSpace(ValuePath);
                        var ordinalCategory = !coordinateItem && string.IsNullOrWhiteSpace(CategoryPath);
                        var ordinalX = !coordinateItem && (ordinalCategory ||
                            !ChartDataAccessor.TryNumber(point.Category, out _) && point.Category is null or string ||
                            this is CircularSeries or SparklineSeries or AnalysisSeries || this is PolarSeries { IsCategorical: true });
                        if (this is PolarSeries { IsCategorical: false } && !ordinalCategory) ordinalX = false;
                        if (this is HistogramSeries) ordinalX = false;
                        page.Rows[slot] = new(point, ordinalCategory, ordinalX, rowIssues);
                        page.InvalidateIdentityLookup();
                        UpdateBufferedIdentity(point.StableId, true);
                        page.HasIssues |= rowIssues.Length > 0;
                        page.Ready[slot] = true;
                        Interlocked.Increment(ref BufferedMappedRowCount);
                    }
                    var row = page.Rows[slot];
                    foreach (var issue in row.Issues)
                        if (issues.Count < 100) issues.Add(issue with { SourceIndex = ordinal + local });
                }
                page.ReadyStart = Math.Min(page.ReadyStart, slice.Offset);
                page.ReadyEnd = Math.Max(page.ReadyEnd, slice.Offset + slice.Count);
            }
            ordinal += slice.Count;
        }
        if (_bufferNullIdCount != 0 || _bufferDuplicateIdCount != 0)
        {
            var message = $"IdPath '{IdPath}' produced a null or duplicate identity in the current buffer window.";
            ReportIssue(new ChartDataIssue(-1, IdPath, message), issues);
            throw new InvalidOperationException(message);
        }
        var data = new BufferedSnapshotData(state, projections, direct, this is StatisticalSeries or DensitySeries ? EmptyPointMode.Gap : EmptyPointMode);
        if (this is not StatisticalSeries and not DensitySeries &&
            (EmptyPointMode is EmptyPointMode.Average or EmptyPointMode.Interpolate ||
            EmptyPointMode == EmptyPointMode.Zero && this is RangeAreaSeries or RangeBarSeries or VarianceSeries))
        {
            // Repair depends on neighbours beyond a page. Keep the established complete-run policy.
            var points = new ChartDataPoint[state.Count];
            for (var index = 0; index < points.Length; index++) points[index] = data.GetPoint(index);
            if (this is RangeAreaSeries or RangeBarSeries or VarianceSeries) ApplyRangeEmptyPoints(points);
            else if (this is not StatisticalSeries and not DensitySeries) ApplyEmptyPoints(points);
            return ChartDataSnapshot.FromOwned(version, points, issues);
        }
        return ChartDataSnapshot.FromBuffered(version, data, issues);
    }

    private void ObserveBufferedItems(IEnumerable<ChartBufferSlice> slices, bool add)
    {
        if (_changed is null || _bufferSource?.SupportsDirectValues == true && CanUseCompactData()) return;
        string?[] paths = [CategoryPath, ValuePath, IdPath, HighPath, LowPath, WeightPath, (this as WaterfallSeries)?.TotalPath,
            (this as IChartPointSeries)?.SizePath, (this as IChartPointSeries)?.ColorPath, (this as CircularSeries)?.ColorPath, (this as PolarSeries)?.ColorPath];
        foreach (var slice in slices)
            for (var local = 0; local < slice.Count; local++)
            {
                var item = slice.Page.GetItem(slice.Offset + local);
                if (item is null || item.GetType().IsValueType) continue;
                var owners = new HashSet<INotifyPropertyChanged>(ReferenceEqualityComparer.Instance);
                foreach (var path in this is AnalysisSeries analytical ? paths.Concat(analytical.ExtraPaths) : this is StatisticalSeries statistical ? paths.Concat(statistical.ExtraPaths) : paths)
                    foreach (var candidate in ChartDataAccessor.Owners(item, path))
                        if (candidate is INotifyPropertyChanged owner) owners.Add(owner);
                foreach (var owner in owners)
                {
                    _bufferObserverCounts.TryGetValue(owner, out var count);
                    if (add)
                    {
                        _bufferObserverCounts[owner] = count + 1;
                        if (count == 0) { _observedProperties.Add(owner); owner.PropertyChanged += ItemChanged; }
                    }
                    else if (count <= 1)
                    {
                        _bufferObserverCounts.Remove(owner); _observedProperties.Remove(owner); owner.PropertyChanged -= ItemChanged;
                    }
                    else _bufferObserverCounts[owner] = count - 1;
                }
            }
    }

    private sealed class BufferedPageProjection(int count)
    {
        private readonly object _summaryLock = new();
        private (ChartSnapshotSummary Summary, bool OrdinalX)? _gapSummary;
        private (ChartSnapshotSummary Summary, bool OrdinalX)? _zeroSummary;
        private readonly object _identityLock = new();
        private Dictionary<object, List<int>>? _referenceIds;
        private Dictionary<object, List<int>>? _valueIds;
        public readonly BufferedMappedRow[] Rows = new BufferedMappedRow[count];
        public readonly bool[] Ready = new bool[count];
        public int ReadyStart = count;
        public int ReadyEnd;
        public bool HasIssues;
        public void InvalidateIdentityLookup() { lock (_identityLock) { _referenceIds = null; _valueIds = null; } }
        public bool TryFindIdentity(object identity, bool valueEquality, int offset, int length, out int slot)
        {
            lock (_identityLock)
            {
                var lookup = valueEquality ? _valueIds : _referenceIds;
                if (lookup is null)
                {
                    lookup = new(valueEquality ? EqualityComparer<object>.Default : ReferenceEqualityComparer.Instance);
                    for (var index = 0; index < Rows.Length; index++)
                        if (Ready[index] && Rows[index].Point.StableId is { } id)
                        {
                            Interlocked.Increment(ref ChartDataBufferMetrics.IdentityRowVisitCount);
                            if (!lookup.TryGetValue(id, out var ordinals)) lookup.Add(id, ordinals = []);
                            ordinals.Add(index);
                        }
                    if (valueEquality) _valueIds = lookup; else _referenceIds = lookup;
                }
                if (lookup.TryGetValue(identity, out var candidates))
                {
                    var found = candidates.BinarySearch(offset);
                    if (found < 0) found = ~found;
                    if (found < candidates.Count && candidates[found] < offset + length) { slot = candidates[found]; return true; }
                }
                slot = -1; return false;
            }
        }
        public bool TryGetSummary(int offset, int length, bool zero, out ChartSnapshotSummary summary, out bool ordinalX)
        {
            lock (_summaryLock)
            {
                var cached = zero ? _zeroSummary : _gapSummary;
                if (offset == 0 && length == Rows.Length && cached is { } ready)
                { summary = ready.Summary; ordinalX = ready.OrdinalX; return true; }
                var result = ChartSnapshotSummary.Empty;
                bool? ordinal = null;
                for (var index = offset; index < offset + length; index++)
                {
                    var row = Rows[index];
                    var point = row.At(index).Point;
                    if (zero && row.Point.WasEmpty && double.IsFinite(point.X)) point = point with { Y = 0 };
                    if (!double.IsFinite(point.X) || !double.IsFinite(point.Y)) continue;
                    if (ordinal is { } previous && previous != row.OrdinalX)
                    {
                        Interlocked.Add(ref ChartDataBufferMetrics.SummaryRowVisitCount, index - offset + 1);
                        summary = default; ordinalX = false; return false;
                    }
                    ordinal = row.OrdinalX;
                    result = result.Append(point);
                }
                Interlocked.Add(ref ChartDataBufferMetrics.SummaryRowVisitCount, length);
                summary = result; ordinalX = ordinal ?? false;
                if (offset == 0 && length == Rows.Length)
                { if (zero) _zeroSummary = (result, ordinalX); else _gapSummary = (result, ordinalX); }
                return true;
            }
        }
    }
    private readonly record struct BufferedMappedRow(ChartDataPoint Point, bool OrdinalCategory, bool OrdinalX, ChartDataIssue[] Issues)
    {
        public ChartDataPoint At(int index) => Point with { SourceIndex = index,
            Category = OrdinalCategory ? index : Point.Category,
            Point = OrdinalX ? Point.Point with { X = index } : Point.Point };
    }

    private sealed class BufferedSnapshotData(ChartBufferState state, BufferedPageProjection[]? projections, bool direct, EmptyPointMode mode) : IChartSnapshotData
    {
        private readonly object _snapshotSummaryLock = new();
        private ChartSnapshotSummary _snapshotSummary;
        private bool _summaryAttempted, _hasSummary;
        public int Count => state.Count;
        public bool HasOrdinalSourceIndices => true;
        public ReadOnlyMemory<ChartPoint> Coordinates => ReadOnlyMemory<ChartPoint>.Empty;
        private BufferedMappedRow Row(int index)
        {
            var (sliceIndex, local) = state.LocateIndex(index);
            return projections![sliceIndex].Rows[state.Slices[sliceIndex].Offset + local];
        }
        public ChartDataValue GetValue(int index)
        {
            ChartPoint point; bool empty; bool wasEmpty;
            if (direct)
            {
                var (slice, local) = state.Locate(index);
                point = slice.Page.GetDirectPoint(slice.Offset + local, index);
                empty = wasEmpty = !double.IsFinite(point.X) || !double.IsFinite(point.Y);
            }
            else
            {
                var row = Row(index); point = row.OrdinalX ? row.Point.Point with { X = index } : row.Point.Point;
                empty = row.Point.IsEmpty; wasEmpty = row.Point.WasEmpty;
            }
            if (mode == EmptyPointMode.Zero && wasEmpty && double.IsFinite(point.X)) { point = point with { Y = 0 }; empty = false; }
            return new(index, point, empty, wasEmpty);
        }
        public ChartDataPoint GetPoint(int index)
        {
            ChartDataPoint point;
            if (direct)
            {
                var (slice, local) = state.Locate(index);
                var slot = slice.Offset + local;
                var value = GetValue(index);
                point = new(index, slice.Page.GetIdentity(slot), slice.Page.GetItem(slot),
                    slice.Page.Coordinates ? (object)value.Point.X : index, value.Point, value.IsEmpty) { WasEmpty = value.WasEmpty };
            }
            else
            {
                point = Row(index).At(index);
                if (mode == EmptyPointMode.Zero && point.WasEmpty && double.IsFinite(point.Point.X))
                    point = point with { Point = point.Point with { Y = 0 }, IsEmpty = false };
            }
            return point;
        }
        public bool TryFindIdentity(object identity, bool valueEquality, out int ordinal)
        {
            var start = 0;
            for (var sliceIndex = 0; sliceIndex < state.Slices.Length; sliceIndex++)
            {
                var slice = state.Slices[sliceIndex];
                int slot;
                var found = direct ? slice.Page.TryFindIdentity(identity, slice.Offset, slice.Count, out slot) :
                    projections![sliceIndex].TryFindIdentity(identity, valueEquality, slice.Offset, slice.Count, out slot);
                if (found) { ordinal = start + slot - slice.Offset; return true; }
                start += slice.Count;
            }
            ordinal = -1; return false;
        }
        public bool TryGetSummary(out ChartSnapshotSummary summary)
        {
            lock (_snapshotSummaryLock)
            {
                if (!_summaryAttempted)
                {
                    _hasSummary = BuildSummary(out _snapshotSummary);
                    _summaryAttempted = true;
                }
                summary = _snapshotSummary; return _hasSummary;
            }
        }
        private bool BuildSummary(out ChartSnapshotSummary summary)
        {
            if (mode is EmptyPointMode.Average or EmptyPointMode.Interpolate) { summary = default; return false; }
            summary = ChartSnapshotSummary.Empty;
            var ordinal = 0;
            for (var sliceIndex = 0; sliceIndex < state.Slices.Length; sliceIndex++)
            {
                var slice = state.Slices[sliceIndex];
                ChartSnapshotSummary pageSummary;
                bool ordinalX;
                if (direct)
                {
                    pageSummary = slice.Page.GetDirectSummary(slice.Offset, slice.Count, mode == EmptyPointMode.Zero);
                    ordinalX = !slice.Page.Coordinates;
                }
                else if (!projections![sliceIndex].TryGetSummary(slice.Offset, slice.Count, mode == EmptyPointMode.Zero, out pageSummary, out ordinalX))
                { summary = default; return false; }
                if (ordinalX) pageSummary = pageSummary.ShiftX(ordinal - slice.Offset);
                summary = summary.Merge(pageSummary);
                ordinal += slice.Count;
            }
            return true;
        }
    }
}
