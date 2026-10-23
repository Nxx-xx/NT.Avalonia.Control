namespace NT.AvaloniaControl.Charts;

public sealed class MapValue
{
    public MapValue(object regionId, double? value, object? source = null) { RegionId = regionId; Value = value; Source = source; }
    public object RegionId { get; }
    public double? Value { get; }
    public object? Source { get; }
}
public sealed class MapValueEntry
{
    internal MapValueEntry(int index, MapValue data) { Index = index; Data = data; }
    public int Index { get; }
    public int SourceIndex => Index;
    public MapValue Data { get; }
    public object RegionId => Data.RegionId;
    public double? Value => Data.Value;
    public object Source => Data.Source ?? Data;
}
public sealed record MapRegionValue(MapFeatureEntry Feature, MapValueEntry? Data)
{
    public double? Value => Data?.Value;
    public bool IsNoData => Value is null;
}
public sealed class MapJoinResult
{
    internal MapJoinResult(MapRegionValue[] regions, MapValueEntry[] unknown)
    { Regions = Array.AsReadOnly(regions); UnknownValues = Array.AsReadOnly(unknown); }
    public IReadOnlyList<MapRegionValue> Regions { get; }
    public IReadOnlyList<MapValueEntry> UnknownValues { get; }
    public int NoDataCount => Regions.Count(r => r.IsNoData);
}

public sealed class MapObservation
{
    public MapObservation(object id, MapCoordinate position, double value = 1, object? source = null, string? label = null)
    { Id = id; Position = position; Value = value; Source = source; Label = label; }
    public object Id { get; }
    public MapCoordinate Position { get; }
    public double Value { get; }
    public object? Source { get; }
    public string? Label { get; }
}
public sealed class MapObservationEntry
{
    internal MapObservationEntry(int index, MapObservation observation) { Index = index; Observation = observation; }
    public int Index { get; }
    public int SourceIndex => Index;
    public MapObservation Observation { get; }
    public object Id => Observation.Id;
    public object Source => Observation.Source ?? Observation;
    public double Value => Observation.Value;
    public string? Label => Observation.Label;
}
public sealed record MapBubble(MapObservationEntry Entry, ChartPoint Position, double Radius);
public sealed class MapBubbleLayout
{
    internal MapBubbleLayout(MapBubble[] points, MapBounds bounds, int clamped)
    { Points = Array.AsReadOnly(points); Bounds = bounds; ClampedLatitudeCount = clamped; }
    public IReadOnlyList<MapBubble> Points { get; }
    public MapBounds Bounds { get; }
    public int ClampedLatitudeCount { get; }
}
public sealed class MapHeatmapCell
{
    internal MapHeatmapCell(int column, int row, MapBounds bounds, double value, MapObservationEntry[] members)
    { Column = column; Row = row; Bounds = bounds; Value = value; Members = Array.AsReadOnly(members); }
    public int Column { get; }
    public int Row { get; }
    public MapBounds Bounds { get; }
    public double X => Bounds.MinX;
    public double Y => Bounds.MinY;
    public double Width => Bounds.Width;
    public double Height => Bounds.Height;
    public double Value { get; }
    public IReadOnlyList<MapObservationEntry> Members { get; }
}
public sealed class MapHeatmapLayout
{
    internal MapHeatmapLayout(MapHeatmapCell[] cells, MapBounds bounds, int columns, int rows, int clamped)
    { Cells = Array.AsReadOnly(cells); Bounds = bounds; Columns = columns; Rows = rows; ClampedLatitudeCount = clamped; }
    public IReadOnlyList<MapHeatmapCell> Cells { get; }
    public MapBounds Bounds { get; }
    public int Columns { get; }
    public int Rows { get; }
    public int ClampedLatitudeCount { get; }
}

public static partial class ChartMaps
{
    /// <summary>One business row per region. Unknown rows are retained separately; duplicate region keys reject.</summary>
    public static MapJoinResult Join(MapGeometrySnapshot snapshot, IEnumerable<MapValue> values, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot); ArgumentNullException.ThrowIfNull(values); cancellationToken.ThrowIfCancellationRequested();
        var data = new Dictionary<object, MapValueEntry>(); var entries = new List<MapValueEntry>();
        foreach (var value in values)
        {
            cancellationToken.ThrowIfCancellationRequested(); ArgumentNullException.ThrowIfNull(value); ArgumentNullException.ThrowIfNull(value.RegionId);
            if (entries.Count >= MaximumObservationCount) throw new ArgumentOutOfRangeException(nameof(values), "Map observation budget exceeded.");
            if (value.Value is { } number && !double.IsFinite(number)) throw new ArgumentOutOfRangeException(nameof(values), "Map values must be finite or null.");
            var entry = new MapValueEntry(entries.Count, value);
            if (!data.TryAdd(value.RegionId, entry)) throw new ArgumentException("Duplicate business region keys require explicit application aggregation.", nameof(values));
            entries.Add(entry);
        }
        var ids = snapshot.Features.Select(f => f.Id).ToHashSet();
        return new(snapshot.Features.Select(feature => new MapRegionValue(feature, data.GetValueOrDefault(feature.Id))).ToArray(),
            entries.Where(entry => !ids.Contains(entry.RegionId)).ToArray());
    }

    private static (MapObservationEntry[] Entries, ChartPoint[] Positions, int Clamped) Observations(
        IEnumerable<MapObservation> observations, MapProjection projection, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(observations); CheckProjection(projection); token.ThrowIfCancellationRequested();
        var entries = new List<MapObservationEntry>(); var positions = new List<ChartPoint>(); var ids = new HashSet<object>(); var clamped = 0;
        foreach (var observation in observations)
        {
            token.ThrowIfCancellationRequested(); ArgumentNullException.ThrowIfNull(observation); ArgumentNullException.ThrowIfNull(observation.Id);
            if (entries.Count >= MaximumObservationCount) throw new ArgumentOutOfRangeException(nameof(observations), "Map observation budget exceeded.");
            if (!ids.Add(observation.Id)) throw new ArgumentException("Map observation IDs must be unique.", nameof(observations));
            if (!double.IsFinite(observation.Value) || observation.Value < 0) throw new ArgumentOutOfRangeException(nameof(observations), "Bubble and heatmap values must be finite and nonnegative.");
            CheckCoordinate(observation.Position, projection);
            if (projection == MapProjection.WebMercator && Math.Abs(observation.Position.Y) > MercatorLatitudeLimit) clamped++;
            entries.Add(new(entries.Count, observation)); positions.Add(ProjectUnchecked(observation.Position, projection));
        }
        return (entries.ToArray(), positions.ToArray(), clamped);
    }

    /// <summary>For minRadius zero, circle area is proportional to value. Radius is a normalized display radius.
    /// A positive minimum radius intentionally introduces a display floor; zero values always remain zero.</summary>
    public static MapBubbleLayout Bubbles(IEnumerable<MapObservation> observations, MapProjection projection,
        double minRadius = 0, double maxRadius = .06, CancellationToken cancellationToken = default)
    {
        if (!double.IsFinite(minRadius) || !double.IsFinite(maxRadius) || minRadius < 0 || maxRadius < minRadius || maxRadius > 1)
            throw new ArgumentOutOfRangeException(nameof(maxRadius));
        var data = Observations(observations, projection, cancellationToken);
        var maximum = data.Entries.Length == 0 ? 0 : data.Entries.Max(p => p.Value);
        return new(data.Entries.Select((entry, i) => new MapBubble(entry, data.Positions[i], entry.Value == 0 || maximum == 0 ? 0
            : Math.Max(minRadius, maxRadius * (Math.Sqrt(entry.Value) / Math.Sqrt(maximum))))).ToArray(), BoundsOf(data.Positions), data.Clamped);
    }

    /// <summary>Deterministic projected-coordinate bins, optionally spread by a discrete Gaussian kernel.
    /// Each observation's kernel is renormalized at the bounds, preserving total nonnegative mass.
    /// Radius zero means exact binning. Members retain every positive-weight contributing original row.</summary>
    public static MapHeatmapLayout Heatmap(IEnumerable<MapObservation> observations, MapProjection projection,
        int columns = 32, int rows = 32, int radius = 1, MapBounds? bounds = null, CancellationToken cancellationToken = default)
    {
        if (columns < 1 || columns > MaximumGridDimension || rows < 1 || rows > MaximumGridDimension) throw new ArgumentOutOfRangeException(nameof(columns));
        if (radius < 0 || radius > MaximumKernelRadius) throw new ArgumentOutOfRangeException(nameof(radius));
        var data = Observations(observations, projection, cancellationToken);
        if ((long)data.Entries.Length * (2 * radius + 1) * (2 * radius + 1) > MaximumWork)
            throw new ArgumentOutOfRangeException(nameof(observations), "Map heatmap point-cell work budget exceeded.");
        var extent = bounds ?? (projection == MapProjection.Identity ? BoundsOf(data.Positions) : new MapBounds(0, 0, 1, 1));
        ValidateBounds(extent);
        var totals = new double[checked(columns * rows)]; var members = new List<MapObservationEntry>?[totals.Length];
        for (var i = 0; i < data.Entries.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested(); var p = data.Positions[i];
            if (p.X < extent.MinX || p.X > extent.MaxX || p.Y < extent.MinY || p.Y > extent.MaxY)
                throw new ArgumentOutOfRangeException(nameof(bounds), "Heatmap bounds must include every observation.");
            var normalized = extent.Normalize(p);
            var cx = Math.Clamp((int)Math.Floor(normalized.X * columns), 0, columns - 1);
            var cy = Math.Clamp((int)Math.Floor(normalized.Y * rows), 0, rows - 1);
            var left = Math.Max(0, cx - radius); var right = Math.Min(columns - 1, cx + radius);
            var bottom = Math.Max(0, cy - radius); var top = Math.Min(rows - 1, cy + radius);
            double sum = 0;
            for (var y = bottom; y <= top; y++) for (var x = left; x <= right; x++) sum += Kernel(x - cx, y - cy, radius);
            if (data.Entries[i].Value == 0) continue;
            for (var y = bottom; y <= top; y++) for (var x = left; x <= right; x++)
            {
                var index = y * columns + x; var value = data.Entries[i].Value * (Kernel(x - cx, y - cy, radius) / sum);
                var total = totals[index] + value;
                if (!double.IsFinite(total)) throw new ArgumentOutOfRangeException(nameof(observations), "Heatmap cell mass exceeds finite Double range.");
                totals[index] = total; (members[index] ??= []).Add(data.Entries[i]);
            }
        }
        var cells = new List<MapHeatmapCell>();
        for (var y = 0; y < rows; y++) for (var x = 0; x < columns; x++)
        {
            var index = y * columns + x; if (members[index] is not { } raw) continue;
            var min = extent.Interpolate((double)x / columns, (double)y / rows); var max = extent.Interpolate((double)(x + 1) / columns, (double)(y + 1) / rows);
            cells.Add(new(x, y, new(min.X, min.Y, max.X, max.Y), totals[index], raw.ToArray()));
        }
        return new(cells.ToArray(), extent, columns, rows, data.Clamped);
    }
    private static double Kernel(int x, int y, int radius) => radius == 0 ? 1 : Math.Exp(-(x * x + y * y) / (2d * radius * radius));
    private static void ValidateBounds(MapBounds bounds)
    {
        if (!double.IsFinite(bounds.MinX) || !double.IsFinite(bounds.MaxX) || !double.IsFinite(bounds.MinY) || !double.IsFinite(bounds.MaxY)
            || bounds.MinX > bounds.MaxX || bounds.MinY > bounds.MaxY) throw new ArgumentOutOfRangeException(nameof(bounds));
    }
}
