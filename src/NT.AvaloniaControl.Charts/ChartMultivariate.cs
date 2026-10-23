namespace NT.AvaloniaControl.Charts;

public sealed record MosaicObservation(object? Id, object Column, object Row, double Value, string? Label = null);
public sealed record MosaicCell(object Column, object Row, int ColumnIndex, int RowIndex, IReadOnlyList<int> SourceIndices, double Value, double Share, double X, double Y, double Width, double Height);
public sealed record MosaicLayout(IReadOnlyList<object> Columns, IReadOnlyList<object> Rows, IReadOnlyList<MosaicCell> Cells, int SourceCount);
public sealed record ParallelDimension(object Id, string Header, double? Minimum = null, double? Maximum = null, bool IsReversed = false);
public sealed record ParallelRecord(object? Id, IReadOnlyList<double> Values, string? Label = null);
public sealed record ParallelDimensionScale(object Id, string Header, double Minimum, double Maximum, bool IsReversed);
public sealed record ParallelCoordinate(int DimensionIndex, double Value, ChartPoint Position);
public sealed record ParallelRecordLayout(int SourceIndex, ParallelRecord Source, IReadOnlyList<ParallelCoordinate> Coordinates);
public sealed record ParallelCoordinatesLayout(IReadOnlyList<ParallelDimensionScale> Dimensions, IReadOnlyList<ParallelRecordLayout> Records, int OmittedValues);

/// <summary>Deterministic contingency proportions and independently scaled multivariate records.
/// Typed category keys compare by value, in first-seen order. Inputs and results are owned snapshots.</summary>
public static class ChartMultivariate
{
    public const int MaximumCount = 20_000;
    public const int MaximumDimensions = 64;
    public const int MaximumValues = 200_000;
    public static MosaicLayout Mosaic(IEnumerable<MosaicObservation> source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source); cancellationToken.ThrowIfCancellationRequested();
        var data = new List<MosaicObservation>(); var columns = new List<object>(); var rows = new List<object>();
        var columnIds = new Dictionary<object, int>(); var rowIds = new Dictionary<object, int>();
        var groups = new Dictionary<(int Column, int Row), List<int>>(); double scale = 0;
        foreach (var value in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (data.Count == MaximumCount) throw new ArgumentOutOfRangeException(nameof(source), "Mosaic source budget exceeded.");
            if (value is null || value.Column is null || value.Row is null || !double.IsFinite(value.Value) || value.Value < 0) throw new ArgumentException("Mosaic requires non-null categories and finite nonnegative weights.", nameof(source));
            if (!columnIds.TryGetValue(value.Column, out var c)) { c = columns.Count; columns.Add(value.Column); columnIds.Add(value.Column, c); }
            if (!rowIds.TryGetValue(value.Row, out var r)) { r = rows.Count; rows.Add(value.Row); rowIds.Add(value.Row, r); }
            if (!groups.TryGetValue((c, r), out var members)) groups.Add((c, r), members = []);
            members.Add(data.Count); data.Add(value); scale = Math.Max(scale, value.Value);
        }
        var columnWeights = new double[columns.Count]; var groupWeights = new Dictionary<(int, int), double>(); double total = 0;
        foreach (var group in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var weight = scale == 0 ? 0 : group.Value.Sum(i => data[i].Value / scale);
            groupWeights.Add(group.Key, weight); columnWeights[group.Key.Column] += weight; total += weight;
        }
        var byColumn=groups.GroupBy(g=>g.Key.Column).ToDictionary(g=>g.Key,g=>g.OrderBy(v=>v.Key.Row).ToArray());
        var cells = new List<MosaicCell>(); double x = 0;
        for (var c = 0; c < columns.Count; c++)
        {
            var width = total == 0 ? 0 : columnWeights[c] / total; double y = 0;
            foreach (var group in byColumn[c])
            {
                cancellationToken.ThrowIfCancellationRequested();
                var weight = groupWeights[group.Key]; var height = columnWeights[c] == 0 ? 0 : weight / columnWeights[c];
                // A group's displayed total must remain representable; global normalization itself is scaled.
                var value = group.Value.Sum(i => data[i].Value); if (!double.IsFinite(value)) throw new OverflowException("A mosaic cell total is not representable.");
                cells.Add(new(columns[c], rows[group.Key.Row], c, group.Key.Row, Array.AsReadOnly(group.Value.ToArray()), value, total == 0 ? 0 : weight / total, x, y, width, height)); y += height;
            }
            x += width;
        }
        return new(columns.AsReadOnly(), rows.AsReadOnly(), cells.AsReadOnly(), data.Count);
    }

    public static ParallelCoordinatesLayout Parallel(IEnumerable<ParallelRecord> source, IEnumerable<ParallelDimension> dimensions, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(dimensions); cancellationToken.ThrowIfCancellationRequested();
        var axes = new List<ParallelDimension>(); var ids = new HashSet<object>();
        foreach (var dimension in dimensions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (axes.Count == MaximumDimensions) throw new ArgumentOutOfRangeException(nameof(dimensions));
            if (dimension is null || dimension.Id is null || !ids.Add(dimension.Id) || dimension.Header is null || dimension.Header.Length > 512 || dimension.Minimum is { } min && !double.IsFinite(min) || dimension.Maximum is { } max && !double.IsFinite(max)) throw new ArgumentException("Parallel axes require unique IDs, finite bounds and bounded titles.", nameof(dimensions));
            axes.Add(dimension);
        }
        if (axes.Count < 2) throw new ArgumentException("At least two parallel dimensions are required.", nameof(dimensions));
        var records = new List<ParallelRecord>(); var minima = Enumerable.Repeat(double.PositiveInfinity, axes.Count).ToArray(); var maxima = Enumerable.Repeat(double.NegativeInfinity, axes.Count).ToArray(); var omitted = 0;
        foreach (var record in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (records.Count == MaximumCount || (long)(records.Count + 1) * axes.Count > MaximumValues) throw new ArgumentOutOfRangeException(nameof(source), "Parallel value budget exceeded.");
            if (record is null || record.Values is null || record.Values.Count != axes.Count) throw new ArgumentException("Every record must have exactly one value per dimension.", nameof(source));
            var values = new double[axes.Count];
            for (var i = 0; i < axes.Count; i++) { var v = record.Values[i]; if (double.IsInfinity(v)) throw new ArgumentException("Parallel values may be finite or NaN gaps.", nameof(source)); values[i] = v; if (double.IsNaN(v)) omitted++; else { minima[i] = Math.Min(minima[i], v); maxima[i] = Math.Max(maxima[i], v); } }
            records.Add(record with { Values = Array.AsReadOnly(values) });
        }
        var scales = axes.Select((a, i) => new ParallelDimensionScale(a.Id, a.Header, a.Minimum ?? (double.IsFinite(minima[i]) ? minima[i] : 0), a.Maximum ?? (double.IsFinite(maxima[i]) ? maxima[i] : 1), a.IsReversed)).ToArray();
        if (scales.Any(a => a.Minimum > a.Maximum)) throw new ArgumentException("Axis minimum must not exceed maximum.", nameof(dimensions));
        var layouts = new List<ParallelRecordLayout>();
        for (var r = 0; r < records.Count; r++)
        {
            cancellationToken.ThrowIfCancellationRequested(); var points = new List<ParallelCoordinate>(); var record = records[r];
            for (var i = 0; i < axes.Count; i++)
            {
                var value = record.Values[i]; if (double.IsNaN(value)) continue; var axis = scales[i];
                var scale = Math.Max(Math.Abs(axis.Minimum), Math.Abs(axis.Maximum));
                var fraction = axis.Minimum == axis.Maximum ? .5 : Math.Clamp((value / scale - axis.Minimum / scale) / (axis.Maximum / scale - axis.Minimum / scale), 0, 1);
                if (!double.IsFinite(fraction)) throw new OverflowException("Parallel axis projection is not representable.");
                points.Add(new(i, value, new(i / (double)(axes.Count - 1), axis.IsReversed ? fraction : 1 - fraction)));
            }
            layouts.Add(new(r, record, points.AsReadOnly()));
        }
        return new(Array.AsReadOnly(scales), layouts.AsReadOnly(), omitted);
    }
}
