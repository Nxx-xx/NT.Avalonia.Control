namespace NT.AvaloniaControl.Charts;

public abstract partial class ChartSeries
{
    // Ordinary Cartesian rows do not need a copy of every professional-series
    // field. Retain their mapped metadata once and expose the same full public
    // point on demand. More specialised encodings keep their established path.
    private bool CanUseMappedColumns() => this is LineSeries or AreaSeries or BarSeries or ScatterSeries &&
        EmptyPointMode == EmptyPointMode.Gap && string.IsNullOrWhiteSpace(HighPath) &&
        string.IsNullOrWhiteSpace(LowPath) && string.IsNullOrWhiteSpace(WeightPath) &&
        (this is not IChartPointSeries encoding || string.IsNullOrWhiteSpace(encoding.SizePath) && string.IsNullOrWhiteSpace(encoding.ColorPath));

    private ChartDataSnapshot CreateMappedColumnsSnapshot(long version)
    {
        // INCC can edit the existing list synchronously inside a mapped getter.
        // Plain sources can only replace it; capturing that list is sufficient.
        IReadOnlyList<Entry> entries = _observedItems is null ? _entries : _entries.ToArray();
        var points = new ChartPoint[entries.Count];
        var metadata = new MappedPointMetadata[entries.Count];
        var issues = new List<ChartDataIssue>();
        var projection = new MappedProjection(this, entries.Count);
        var summary = ChartSnapshotSummary.Empty;
        for (var index = 0; index < entries.Count; index++)
        {
            metadata[index] = projection.Map(entries[index], index, issues, out var point);
            points[index] = point;
            summary = summary.Append(point);
        }
        if (version != SourceVersion) throw new ChartDataMappingException("source changed while mapping; retry");
        return ChartDataSnapshot.FromBuffered(version, new MappedSnapshotData(points, metadata, summary), issues);
    }

    private readonly record struct MappedPointMetadata(object? Identity, object? Item, object? Category, object? Value);

    private sealed class MappedProjection
    {
        private readonly ChartSeries _owner;
        private readonly string? _categoryPath, _valuePath, _idPath;
        private readonly bool _hasCategory, _hasValue, _hasId;
        private readonly ChartDataAccessor.PathReader _category, _value, _id;
        private readonly HashSet<object>? _ids;
        internal MappedProjection(ChartSeries owner, int count)
        {
            _owner = owner;
            _categoryPath = owner.CategoryPath; _valuePath = owner.ValuePath; _idPath = owner.IdPath;
            _hasCategory = !string.IsNullOrWhiteSpace(_categoryPath);
            _hasValue = !string.IsNullOrWhiteSpace(_valuePath);
            _hasId = !string.IsNullOrWhiteSpace(_idPath);
            _category = new(_categoryPath); _value = new(_valuePath); _id = new(_idPath);
            if (_hasId) _ids = new(count);
        }
        internal MappedPointMetadata Map(Entry entry, int index, List<ChartDataIssue> issues, out ChartPoint point)
        {
            var item = entry.Item;
            object? category; object? originalValue = null;
            double x; double y;
            if (!_hasCategory && !_hasValue && item is ChartPoint direct)
            { category = direct.X; x = direct.X; y = direct.Y; }
            else if (!_hasCategory && !_hasValue && item is Avalonia.Point avaloniaPoint)
            { category = avaloniaPoint.X; x = avaloniaPoint.X; y = avaloniaPoint.Y; }
            else
            {
                category = _hasCategory ? Read(item, _category, _categoryPath, index, issues) : index;
                x = ChartDataAccessor.TryNumber(category, out var numericCategory) ? numericCategory : category is null or string ? index : double.NaN;
                var rawValue = Read(item, _value, _valuePath, index, issues);
                if (_hasValue || item is string || item is not null && !item.GetType().IsValueType) originalValue = rawValue;
                if (!ChartDataAccessor.TryNumber(rawValue, out y) || !double.IsFinite(x)) y = double.NaN;
            }
            var id = _hasId ? Read(item, _id, _idPath, index, issues) : item is not null && !item.GetType().IsValueType ? item : entry.Identity;
            if (_ids is not null && (id is null || !_ids.Add(id)))
            {
                var message = $"IdPath '{_idPath}' produced a null or duplicate identity at source index {index}.";
                _owner.ReportIssue(new ChartDataIssue(index, _idPath, message), issues);
                throw new InvalidOperationException(message);
            }
            point = new(x, y);
            return new(id, item, category, originalValue);
        }
        private object? Read(object? item, ChartDataAccessor.PathReader reader, string? path, int index, List<ChartDataIssue> issues)
        {
            if (reader.TryRead(item, out var value)) return value;
            _owner.ReportIssue(new ChartDataIssue(index, path, "Property path is missing or its getter failed."), issues);
            return null;
        }
    }

    private sealed class MappedSnapshotData(ChartPoint[] points, MappedPointMetadata[] metadata, ChartSnapshotSummary summary) : IChartSnapshotData
    {
        private readonly object _identityLock = new();
        private Dictionary<object, int>? _referenceIdentities;
        private Dictionary<object, int>? _valueIdentities;
        public int Count => points.Length;
        public bool HasOrdinalSourceIndices => true;
        public ReadOnlyMemory<ChartPoint> Coordinates => points;
        public ChartDataValue GetValue(int index)
        {
            var point = points[index];
            var empty = !double.IsFinite(point.X) || !double.IsFinite(point.Y);
            return new(index, point, empty, empty);
        }
        public ChartDataPoint GetPoint(int index)
        {
            var value = GetValue(index);
            ref readonly var row = ref metadata[index];
            return new(index, row.Identity, row.Item, row.Category, value.Point, value.IsEmpty)
            { WasEmpty = value.WasEmpty, Value = row.Value };
        }
        public bool TryGetSummary(out ChartSnapshotSummary value) { value = summary; return true; }
        public bool TryFindIdentity(object identity, bool valueEquality, out int ordinal)
        {
            lock (_identityLock)
            {
                var lookup = valueEquality ? _valueIdentities : _referenceIdentities;
                if (lookup is null)
                {
                    lookup = new Dictionary<object, int>(Count, valueEquality ? EqualityComparer<object>.Default : ReferenceEqualityComparer.Instance);
                    for (var index = 0; index < metadata.Length; index++)
                        if (metadata[index].Identity is { } id) lookup.TryAdd(id, index);
                    if (valueEquality) _valueIdentities = lookup; else _referenceIdentities = lookup;
                }
                return lookup.TryGetValue(identity, out ordinal);
            }
        }
        public bool TryFindExistingIdentity(object identity, bool valueEquality, out int ordinal)
        {
            lock (_identityLock)
            {
                var lookup = valueEquality ? _valueIdentities : _referenceIdentities;
                if (lookup is not null) return lookup.TryGetValue(identity, out ordinal);
            }
            ordinal = -1; return false;
        }
    }
}
