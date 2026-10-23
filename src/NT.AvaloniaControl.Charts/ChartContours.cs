namespace NT.AvaloniaControl.Charts;

/// <summary>A scalar sample at an exact rectangular-grid coordinate. NaN masks every adjacent cell.</summary>
public sealed record ScalarGridPoint(object? Id, double X, double Y, double Value);

/// <summary>A contour segment; indices refer to the original, unsorted source enumeration.</summary>
public sealed class ContourLine
{
    internal ContourLine(double level, ChartPoint start, ChartPoint end, int[] cells, int[] indices)
    { Level = level; Start = start; End = end; CellIndices = Array.AsReadOnly(cells); SourceIndices = Array.AsReadOnly(indices); }
    public double Level { get; }
    public ChartPoint Start { get; }
    public ChartPoint End { get; }
    /// <summary>First owning cell in stable grid order.</summary>
    public int CellIndex => CellIndices[0];
    /// <summary>A threshold-equal shared edge is emitted once and retains both adjacent owning cells.</summary>
    public IReadOnlyList<int> CellIndices { get; }
    public IReadOnlyList<int> SourceIndices { get; }
}

/// <summary>A convex patch in one scalar band. Null lower/upper bounds mean unbounded.</summary>
public sealed class ContourBand
{
    internal ContourBand(int band, double? lower, double? upper, ChartPoint[] vertices, int cell, int[] indices)
    { BandIndex = band; LowerBound = lower; UpperBound = upper; Vertices = Array.AsReadOnly(vertices); CellIndex = cell; SourceIndices = Array.AsReadOnly(indices); }
    public int BandIndex { get; }
    public double? LowerBound { get; }
    public double? UpperBound { get; }
    public IReadOnlyList<ChartPoint> Vertices { get; }
    public int CellIndex { get; }
    public IReadOnlyList<int> SourceIndices { get; }
}

/// <summary>An owned, immutable contour scene. Grid is sorted by Y then X; SourceIndices preserve input identity.</summary>
public sealed class ContourResult
{
    internal ContourResult(ScalarGridPoint[] grid, int[] indices, double[] x, double[] y, double[] levels,
        ContourLine[] lines, ContourBand[] bands, int masked, int saddles)
    { Grid = Array.AsReadOnly(grid); SourceIndices = Array.AsReadOnly(indices); XCoordinates = Array.AsReadOnly(x); YCoordinates = Array.AsReadOnly(y); Levels = Array.AsReadOnly(levels); Lines = Array.AsReadOnly(lines); Bands = Array.AsReadOnly(bands); MaskedCellCount = masked; SaddleCellCount = saddles; }
    public IReadOnlyList<ScalarGridPoint> Grid { get; }
    public IReadOnlyList<int> SourceIndices { get; }
    public IReadOnlyList<double> XCoordinates { get; }
    public IReadOnlyList<double> YCoordinates { get; }
    public IReadOnlyList<double> Levels { get; }
    public IReadOnlyList<ContourLine> Lines { get; }
    public IReadOnlyList<ContourBand> Bands { get; }
    public int MaskedCellCount { get; }
    public int SaddleCellCount { get; }
}

/// <summary>Source-preserving scalar contours using one saddle-aware piecewise-linear surface for both lines and fills.</summary>
public static class ChartContours
{
    public const int MaximumPointCount = 65536;
    public const int MaximumLevelCount = 32;
    public const int MaximumPrimitiveCount = 262144;
    public const int MaximumWork = 2000000;

    /// <summary>Builds evenly spaced interior levels. Constant fields and numerically coincident levels collapse to one unique level.</summary>
    public static ContourResult Build(IEnumerable<ScalarGridPoint> source, int contourLevels = 8, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (contourLevels < 1 || contourLevels > MaximumLevelCount) throw new ArgumentOutOfRangeException(nameof(contourLevels));
        cancellationToken.ThrowIfCancellationRequested(); var input = new List<ScalarGridPoint>();
        foreach (var point in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (input.Count == MaximumPointCount) throw new ArgumentOutOfRangeException(nameof(source), "Scalar grid observation budget exceeded.");
            if (point is null) throw new ArgumentException("Scalar observations cannot be null.", nameof(source)); input.Add(point);
        }
        var finite = input.Where(p => double.IsFinite(p.Value)).Select(p => p.Value).ToArray();
        var minimum = finite.Length == 0 ? 0 : finite.Min(); var maximum = finite.Length == 0 ? 0 : finite.Max();
        var levels = minimum == maximum ? new[] { minimum } : Enumerable.Range(1, contourLevels).Select(i => Lerp(minimum, maximum, (double)i / (contourLevels + 1))).Distinct().ToArray();
        return Build(input, levels, cancellationToken);
    }

    /// <summary>
    /// Accepts a complete rectangular grid, in any source order. Duplicate or missing coordinates are errors.
    /// Each cell with an interior bilinear stationary point is split into four triangles at that exact saddle;
    /// other cells use the bottom-left/top-right diagonal. Lines and filled bands clip the same linear triangles.
    /// This preserves the asymptotic saddle topology, while curved bilinear contours are approximated by straight segments.
    /// Values equal to a level belong to the upper band; a constant plateau has no internal contour.
    /// NaN masks a whole cell, infinity is rejected. Levels must be finite, unique and strictly increasing.
    /// </summary>
    public static ContourResult Build(IEnumerable<ScalarGridPoint> source, IEnumerable<double> levels, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(levels);
        cancellationToken.ThrowIfCancellationRequested();
        var thresholds = new List<double>();
        foreach (var level in levels)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (thresholds.Count == MaximumLevelCount || !double.IsFinite(level) || thresholds.Count > 0 && level <= thresholds[^1])
                throw new ArgumentOutOfRangeException(nameof(levels), "One to 32 finite, strictly increasing levels are required.");
            thresholds.Add(level);
        }
        if (thresholds.Count == 0) throw new ArgumentException("At least one contour level is required.", nameof(levels));
        var input = new List<ScalarGridPoint>(); var locations = new Dictionary<(double X, double Y), int>();
        foreach (var sample in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (input.Count == MaximumPointCount) throw new ArgumentOutOfRangeException(nameof(source), "Scalar grid observation budget exceeded.");
            if (sample is null || !double.IsFinite(sample.X) || !double.IsFinite(sample.Y) || double.IsInfinity(sample.Value))
                throw new ArgumentException("Grid coordinates must be finite and values finite or NaN.", nameof(source));
            if (!locations.TryAdd((sample.X, sample.Y), input.Count)) throw new ArgumentException("A scalar grid cannot contain duplicate coordinates.", nameof(source));
            input.Add(sample);
        }
        var xs = input.Select(p => p.X).Distinct().Order().ToArray(); var ys = input.Select(p => p.Y).Distinct().Order().ToArray();
        if (input.Count == 0) return new([], [], [], [], thresholds.ToArray(), [], [], 0, 0);
        if (xs.Length < 2 || ys.Length < 2 || (long)xs.Length * ys.Length != input.Count)
            throw new ArgumentException("A complete rectangular grid with at least two X and Y coordinates is required.", nameof(source));
        if ((long)(xs.Length - 1) * (ys.Length - 1) * (thresholds.Count + 1) * 4 > MaximumWork)
            throw new ArgumentOutOfRangeException(nameof(source), "Scalar contour cell/level work budget exceeded.");
        var grid = new ScalarGridPoint[input.Count]; var sourceIndices = new int[input.Count];
        for (var y = 0; y < ys.Length; y++) for (var x = 0; x < xs.Length; x++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!locations.TryGetValue((xs[x], ys[y]), out var original)) throw new ArgumentException("The scalar grid contains a missing coordinate.", nameof(source));
            var index = y * xs.Length + x; grid[index] = input[original]; sourceIndices[index] = original;
        }
        var lines = new List<ContourLine>(); var lineKeys = new Dictionary<(double Level, ChartPoint A, ChartPoint B), int>();
        var bands = new List<ContourBand>(); var masked = 0; var saddles = 0;
        for (var y = 0; y < ys.Length - 1; y++) for (var x = 0; x < xs.Length - 1; x++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int[] corners = [y * xs.Length + x, y * xs.Length + x + 1, (y + 1) * xs.Length + x + 1, (y + 1) * xs.Length + x];
            var values = corners.Select(i => grid[i].Value).ToArray();
            if (values.Any(double.IsNaN)) { masked++; continue; }
            var cell = y * (xs.Length - 1) + x; var members = corners.Select(i => sourceIndices[i]).ToArray();
            Vertex[] vertices = [new(0, 0, values[0]), new(1, 0, values[1]), new(1, 1, values[2]), new(0, 1, values[3])];
            var triangles = new List<Vertex[]>(); var minimum = values.Min(); var maximum = values.Max();
            var a = minimum == maximum ? 0 : Fraction(values[0], minimum, maximum); var b = minimum == maximum ? 0 : Fraction(values[1], minimum, maximum);
            var c = minimum == maximum ? 0 : Fraction(values[2], minimum, maximum); var d = minimum == maximum ? 0 : Fraction(values[3], minimum, maximum);
            var cross = a - b + c - d; var u = cross == 0 ? double.NaN : (a - d) / cross; var v = cross == 0 ? double.NaN : (a - b) / cross;
            if (u > 0 && u < 1 && v > 0 && v < 1)
            {
                var saddle = Lerp(Lerp(values[0], values[1], u), Lerp(values[3], values[2], u), v);
                var center = new Vertex(u, v, saddle); saddles++;
                for (var edge = 0; edge < 4; edge++) triangles.Add([vertices[edge], vertices[(edge + 1) % 4], center]);
            }
            else { triangles.Add([vertices[0], vertices[1], vertices[2]]); triangles.Add([vertices[0], vertices[2], vertices[3]]); }
            var seen = new HashSet<(int Level, Vertex A, Vertex B)>();
            ChartPoint Project(Vertex point) => new(Lerp(xs[x], xs[x + 1], point.U), Lerp(ys[y], ys[y + 1], point.V));
            foreach (var triangle in triangles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (var levelIndex = 0; levelIndex < thresholds.Count; levelIndex++)
                {
                    var level = thresholds[levelIndex]; var crossings = new List<Vertex>(3);
                    for (var edge = 0; edge < 3; edge++)
                    {
                        var p = triangle[edge]; var q = triangle[(edge + 1) % 3];
                        if ((p.Value < level) != (q.Value < level)) { var point = Crossing(p, q, level); if (!crossings.Contains(point)) crossings.Add(point); }
                    }
                    if (crossings.Count == 2 && crossings[0] != crossings[1])
                    {
                        var p = crossings[0]; var q = crossings[1];
                        if (p.U > q.U || p.U == q.U && p.V > q.V) (p, q) = (q, p);
                        if (seen.Add((levelIndex, p, q)))
                        {
                            var start = Project(p); var end = Project(q);
                            if (start != end)
                            {
                                var key = (level, start, end);
                                if (lineKeys.TryGetValue(key, out var existing))
                                {
                                    var old = lines[existing];
                                    lines[existing] = new(level, start, end, old.CellIndices.Append(cell).Distinct().ToArray(), old.SourceIndices.Concat(members).Distinct().ToArray());
                                }
                                else { Budget(); lineKeys.Add(key, lines.Count); lines.Add(new(level, start, end, new[] { cell }, members)); }
                            }
                        }
                    }
                }
                for (var band = 0; band <= thresholds.Count; band++)
                {
                    var polygon = triangle.ToList();
                    if (band > 0) polygon = Clip(polygon, thresholds[band - 1], true);
                    if (band < thresholds.Count) polygon = Clip(polygon, thresholds[band], false);
                    if (polygon.Count < 3 || Area(polygon) <= 0) continue;
                    Budget(); bands.Add(new(band, band == 0 ? null : thresholds[band - 1], band == thresholds.Count ? null : thresholds[band], polygon.Select(Project).ToArray(), cell, members));
                }
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new(grid, sourceIndices, xs, ys, thresholds.ToArray(), lines.ToArray(), bands.ToArray(), masked, saddles);
        void Budget() { if (lines.Count + bands.Count >= MaximumPrimitiveCount) throw new ArgumentOutOfRangeException(nameof(source), "Scalar contour primitive budget exceeded."); }
    }
    private readonly record struct Vertex(double U, double V, double Value);
    private static double Lerp(double a, double b, double t)
    {
        if (t == 0) return a; if (t == 1) return b;
        if (Math.Sign(a) == Math.Sign(b) || a == 0 || b == 0)
            return t <= .5 ? a + (b - a) * t : b + (a - b) * (1 - t);
        return (1 - t) * a + t * b;
    }
    private static Vertex Crossing(Vertex a, Vertex b, double level)
    {
        if (a.Value == level) return a; if (b.Value == level) return b;
        var t = Math.Clamp(Fraction(level, a.Value, b.Value), 0, 1);
        return new(Lerp(a.U, b.U, t), Lerp(a.V, b.V, t), level);
    }
    private static double Fraction(double value, double a, double b)
    {
        if (Math.Sign(a) == Math.Sign(b) || a == 0 || b == 0) return (value - a) / (b - a);
        var scale = Math.Max(Math.Abs(a), Math.Abs(b)); return (value / scale - a / scale) / (b / scale - a / scale);
    }
    private static List<Vertex> Clip(List<Vertex> polygon, double level, bool upper)
    {
        var result = new List<Vertex>(6); if (polygon.Count == 0) return result;
        var previous = polygon[^1]; var previousInside = upper ? previous.Value >= level : previous.Value < level;
        foreach (var current in polygon)
        {
            var inside = upper ? current.Value >= level : current.Value < level;
            if (inside != previousInside) Add(Crossing(previous, current, level));
            if (inside) Add(current);
            previous = current; previousInside = inside;
        }
        if (result.Count > 1 && result[0] == result[^1]) result.RemoveAt(result.Count - 1);
        return result;
        void Add(Vertex vertex) { if (result.Count == 0 || result[^1] != vertex) result.Add(vertex); }
    }
    private static double Area(IReadOnlyList<Vertex> vertices)
    { var sum = 0d; for (var i = 0; i < vertices.Count; i++) { var next = vertices[(i + 1) % vertices.Count]; sum += vertices[i].U * next.V - next.U * vertices[i].V; } return Math.Abs(sum) / 2; }
}
