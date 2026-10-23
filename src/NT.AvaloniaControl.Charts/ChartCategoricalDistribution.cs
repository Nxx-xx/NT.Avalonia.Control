namespace NT.AvaloniaControl.Charts;

/// <summary>One original categorical observation. Id may be null; categories use value equality and first occurrence order.</summary>
public readonly record struct CategoricalSample(object? Id, object Category, double Value, string? Label = null);
public enum CategoricalDistributionKind { Beeswarm, Strip }

/// <summary>A single original observation in local DIP coordinates, without aggregation or resampling.</summary>
public sealed record CategoricalDistributionPoint(int SourceIndex, object? Id, object Category, int CategoryIndex,
    double Value, string? Label, double X, double Y, bool IsOverflow);
public sealed record CategoricalDistributionCategory(object Category, int CategoryIndex, int Count, double Mean,
    double CenterX, double MeanY);

/// <summary>An owned categorical layout. Every input observation has exactly one point.</summary>
public sealed class CategoricalDistributionLayout
{
    internal CategoricalDistributionLayout(CategoricalDistributionPoint[] points, CategoricalDistributionCategory[] categories,
        double minimum, double maximum, int collisions, int overflow, int work, bool budget)
    { Points = Array.AsReadOnly(points); Categories = Array.AsReadOnly(categories); Minimum = minimum; Maximum = maximum;
      CollisionCount = collisions; OverflowCount = overflow; WorkCount = work; WorkBudgetExceeded = budget; }
    public IReadOnlyList<CategoricalDistributionPoint> Points { get; }
    public IReadOnlyList<CategoricalDistributionCategory> Categories { get; }
    public double Minimum { get; }
    public double Maximum { get; }
    /// <summary>Placements observed to overlap an earlier point; a lower bound when WorkBudgetExceeded is true.</summary>
    public int CollisionCount { get; }
    /// <summary>Points which could not fit their category lane, or were placed after the work budget was exhausted. They remain in Points.</summary>
    public int OverflowCount { get; }
    public int WorkCount { get; }
    public bool WorkBudgetExceeded { get; }
}

/// <summary>Deterministic, bounded categorical point layouts. Values are never altered to avoid collisions.</summary>
public static class ChartCategoricalDistribution
{
    public const int MaximumCount = 20_000;
    public const int MaximumWork = 2_000_000;

    /// <summary>
    /// Values increase upward; width, height and markerRadius are DIPs. Jitter is the fraction of a category width
    /// allowed on either side of its center (0..0.5). Strip uses a seeded index hash. Beeswarm tries the center,
    /// then alternating horizontal diameter steps and checks neighboring spatial cells. A dense lane falls back
    /// to deterministic jitter, keeps the original value and reports overflow. It never silently drops a point.
    /// </summary>
    public static CategoricalDistributionLayout Layout(IEnumerable<CategoricalSample> source,
        CategoricalDistributionKind kind = CategoricalDistributionKind.Beeswarm, double width = 372, double height = 232,
        double markerRadius = 4, double jitter = .36, int seed = 0, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (!double.IsFinite(width) || width <= 0 || width > 10_000_000) throw new ArgumentOutOfRangeException(nameof(width));
        if (!double.IsFinite(height) || height <= 0 || height > 10_000_000) throw new ArgumentOutOfRangeException(nameof(height));
        if (!double.IsFinite(markerRadius) || markerRadius < 0 || markerRadius > 1024) throw new ArgumentOutOfRangeException(nameof(markerRadius));
        if (!double.IsFinite(jitter) || jitter < 0 || jitter > .5) throw new ArgumentOutOfRangeException(nameof(jitter));
        var samples = new List<CategoricalSample>(); var categoryIndices = new Dictionary<object, int>();
        var groups = new List<List<int>>(); var keys = new List<object>();
        double minimum = double.PositiveInfinity, maximum = double.NegativeInfinity;
        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (samples.Count >= MaximumCount) throw new ArgumentOutOfRangeException(nameof(source), "Categorical observation budget exceeded.");
            if (item.Category is null || !double.IsFinite(item.Value)) throw new ArgumentException("Every observation requires a category and a finite value.", nameof(source));
            if (!categoryIndices.TryGetValue(item.Category, out var category))
            { category = groups.Count; categoryIndices.Add(item.Category, category); keys.Add(item.Category); groups.Add([]); }
            groups[category].Add(samples.Count); samples.Add(item);
            minimum = Math.Min(minimum, item.Value); maximum = Math.Max(maximum, item.Value);
        }
        if (samples.Count == 0) return new([], [], 0, 0, 0, 0, 0, false);
        var points = new CategoricalDistributionPoint[samples.Count];
        var summaries = new CategoricalDistributionCategory[groups.Count];
        var diameter = 2 * markerRadius; var cellSize = Math.Max(diameter, .000001);
        var cells = new Dictionary<(long X, long Y), List<int>>();
        var work = 0; var exhausted = false; var collisions = 0; var overflow = 0;
        var lane = width / groups.Count; var verticalInset = Math.Min(markerRadius, height / 2);
        double Y(double value) => verticalInset + (height - 2 * verticalInset) * (1 - Fraction(value, minimum, maximum));
        for (var group = 0; group < groups.Count; group++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var center = lane * (group + .5); var half = Math.Max(0, Math.Min(lane * jitter, lane / 2 - markerRadius));
            var ordered = groups[group].OrderBy(index => samples[index].Value).ThenBy(index => index).ToArray();
            var scale = groups[group].Max(index => Math.Abs(samples[index].Value)); double sum = 0, compensation = 0;
            foreach (var index in groups[group])
            { var term = scale == 0 ? 0 : samples[index].Value / scale; var adjusted = term - compensation; var next = sum + adjusted; compensation = (next - sum) - adjusted; sum = next; }
            var mean = scale == 0 ? 0 : Math.Clamp(sum / groups[group].Count, -1, 1) * scale;
            summaries[group] = new(keys[group], group, ordered.Length, mean, center, Y(mean));
            foreach (var index in ordered)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var item = samples[index]; var y = Y(item.Value); var x = center;
                var crowded = lane < diameter || height < diameter; var overlap = false;
                if (kind == CategoricalDistributionKind.Beeswarm && markerRadius > 0 && !exhausted)
                {
                    var steps = (int)Math.Min(MaximumWork, Math.Floor(half / diameter)); var placed = false;
                    for (var attempt = 0; attempt <= 2 * steps; attempt++)
                    {
                        if (!Spend()) break;
                        var offset = attempt == 0 ? 0 : (attempt % 2 == 1 ? 1 : -1) * ((attempt + 1) / 2) * diameter;
                        var candidate = center + offset;
                        if (!Collides(candidate, y)) { if (exhausted) break; x = candidate; placed = true; break; }
                    }
                    if (!placed) { crowded = true; x = center + SignedHash(index, seed) * half; overlap = !exhausted && Collides(x, y); }
                }
                else
                { x = center + SignedHash(index, seed) * half; overlap = markerRadius > 0 && !exhausted && Collides(x, y); }
                if (exhausted) { crowded = true; overlap = false; }
                if (overlap) collisions++; if (crowded) overflow++;
                points[index] = new(index, item.Id, item.Category, group, item.Value, item.Label, x, y, crowded);
                if (!exhausted && markerRadius > 0)
                { var cell = Cell(x, y); if (!cells.TryGetValue(cell, out var indices)) cells[cell] = indices = []; indices.Add(index); }
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new(points, summaries, minimum, maximum, collisions, overflow, work, exhausted);

        (long X, long Y) Cell(double x, double y) => ((long)Math.Floor(x / cellSize), (long)Math.Floor(y / cellSize));
        bool Spend()
        { cancellationToken.ThrowIfCancellationRequested(); if (work >= MaximumWork) { exhausted = true; return false; } work++; return true; }
        bool Collides(double x, double y)
        {
            var cell = Cell(x, y);
            for (var dx = -1; dx <= 1; dx++) for (var dy = -1; dy <= 1; dy++)
                if (cells.TryGetValue((cell.X + dx, cell.Y + dy), out var indices)) foreach (var previous in indices)
                {
                    if (!Spend()) return true;
                    var point = points[previous]; var px = x - point.X; var py = y - point.Y;
                    if (px * px + py * py < diameter * diameter * (1 - 1e-12)) return true;
                }
            return false;
        }
    }

    private static double Fraction(double value, double minimum, double maximum)
    { if (minimum == maximum) return .5; var scale = Math.Max(Math.Abs(minimum), Math.Abs(maximum)); return Math.Clamp((value / scale - minimum / scale) / (maximum / scale - minimum / scale), 0, 1); }
    private static double SignedHash(int index, int seed)
    {
        unchecked { var value = (uint)index ^ ((uint)seed + 0x9e3779b9u); value ^= value >> 16; value *= 0x7feb352du; value ^= value >> 15; value *= 0x846ca68bu; value ^= value >> 16; return value / (double)uint.MaxValue * 2 - 1; }
    }
}
