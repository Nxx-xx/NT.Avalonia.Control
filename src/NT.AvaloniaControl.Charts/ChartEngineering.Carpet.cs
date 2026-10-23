using System.Globalization;

namespace NT.AvaloniaControl.Charts;

public static partial class ChartEngineering
{
    /// <summary>Uses the explicit nonorthogonal convention X=A+offset*B, Y=point.Y.</summary>
    public static ChartPoint ProjectCarpet(CarpetPoint point, double offset = .5)
    {
        if (!double.IsFinite(point.A) || !double.IsFinite(point.B) || !double.IsFinite(point.Y) || !double.IsFinite(offset))
            throw new ArgumentOutOfRangeException(nameof(point), "Carpet coordinates and offset must be finite.");
        var x = point.A + offset * point.B;
        if (!double.IsFinite(x)) throw new OverflowException("The carpet projection cannot be represented.");
        return new(x, point.Y);
    }

    /// <summary>
    /// Copies a complete rectangular A/B mesh, requiring at least two distinct levels per parameter and
    /// exactly one ordinate per pair. Mesh paths include every original node. Folded/singular meshes can
    /// be displayed, but IsInvertible is false and UnprojectCarpet rejects them.
    /// </summary>
    public static CarpetGridSnapshot CarpetGrid(IEnumerable<CarpetPoint> source, double offset = .5,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!double.IsFinite(offset)) throw new ArgumentOutOfRangeException(nameof(offset));
        cancellationToken.ThrowIfCancellationRequested();
        var byParameter = new Dictionary<(double A, double B), CarpetPoint>();
        var aValues = new SortedSet<double>(); var bValues = new SortedSet<double>();
        foreach (var point in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (byParameter.Count >= MaximumCount / 2)
                throw new ArgumentOutOfRangeException(nameof(source), "Carpet mesh paths exceed the engineering geometry budget.");
            _ = ProjectCarpet(point, offset);
            if (!byParameter.TryAdd((point.A, point.B), point))
                throw new ArgumentException("Each A/B pair must occur exactly once.", nameof(source));
            aValues.Add(point.A); bValues.Add(point.B);
        }
        if (aValues.Count < 2 || bValues.Count < 2 || (long)aValues.Count * bValues.Count != byParameter.Count)
            throw new ArgumentException("Carpet interpolation requires a complete rectangular mesh with at least two A and B levels.", nameof(source));
        var aLevels = aValues.ToArray(); var bLevels = bValues.ToArray();
        if (!double.IsFinite(aLevels[^1] - aLevels[0]) || !double.IsFinite(bLevels[^1] - bLevels[0]))
            throw new ArgumentOutOfRangeException(nameof(source), "Parameter spans must be representable.");
        var points = new CarpetPoint[byParameter.Count];
        var paths = new List<EngineeringPath>(aLevels.Length + bLevels.Length);
        for (var b = 0; b < bLevels.Length; b++)
        {
            var row = new ChartPoint[aLevels.Length];
            for (var a = 0; a < aLevels.Length; a++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var point = byParameter[(aLevels[a], bLevels[b])];
                points[b * aLevels.Length + a] = point;
                row[a] = ProjectCarpet(point, offset);
            }
            paths.Add(new($"B={bLevels[b].ToString(CultureInfo.InvariantCulture)}", row));
        }
        for (var a = 0; a < aLevels.Length; a++)
        {
            var column = new ChartPoint[bLevels.Length];
            for (var b = 0; b < bLevels.Length; b++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                column[b] = ProjectCarpet(points[b * aLevels.Length + a], offset);
            }
            paths.Add(new($"A={aLevels[a].ToString(CultureInfo.InvariantCulture)}", column));
        }
        var orientation = 0;
        var invertible = true;
        for (var b = 0; b < bLevels.Length - 1; b++)
        for (var a = 0; a < aLevels.Length - 1; a++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cell = CreateCarpetCell(points, aLevels.Length, a, b, offset);
            var localOrientation = CarpetOrientation(cell);
            if (localOrientation == 0 || orientation != 0 && localOrientation != orientation) invertible = false;
            if (localOrientation != 0) orientation = localOrientation;
        }
        return new(points, aLevels, bLevels, paths.ToArray(), offset, invertible);
    }

    /// <summary>Interpolates the ordinate bilinearly inside the mesh; extrapolation is rejected.</summary>
    public static CarpetPoint InterpolateCarpet(CarpetGridSnapshot grid, double a, double b)
    {
        ArgumentNullException.ThrowIfNull(grid);
        if (!double.IsFinite(a) || !double.IsFinite(b) || a < grid.ALevels[0] || a > grid.ALevels[^1] ||
            b < grid.BLevels[0] || b > grid.BLevels[^1])
            throw new ArgumentOutOfRangeException(nameof(a), "Parameters must be inside the carpet mesh.");
        var ai = CarpetInterval(grid.ALevels, a); var bi = CarpetInterval(grid.BLevels, b);
        var u = (a - grid.ALevels[ai]) / (grid.ALevels[ai + 1] - grid.ALevels[ai]);
        var v = (b - grid.BLevels[bi]) / (grid.BLevels[bi + 1] - grid.BLevels[bi]);
        var cell = CreateCarpetCell(grid.Points, grid.ALevels.Count, ai, bi, grid.Offset);
        var y = Lerp(Lerp(cell.P00.Y, cell.P10.Y, u), Lerp(cell.P01.Y, cell.P11.Y, u), v);
        return new(a, b, CheckedFinite(y, "Interpolated carpet ordinate overflowed."));
    }

    /// <summary>
    /// Finds A/B for a projected point by solving each candidate bilinear cell along its constant-X line.
    /// A scaled Jacobian check rejects folds and singular cells. Outside points and distinct multiple
    /// roots are rejected; shared-edge duplicates resolve to the same parameter pair.
    /// </summary>
    public static CarpetPoint UnprojectCarpet(CarpetGridSnapshot grid, ChartPoint point,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grid); ValidatePoint(point, nameof(point));
        cancellationToken.ThrowIfCancellationRequested();
        if (!grid.IsInvertible) throw new InvalidOperationException("A folded or singular carpet mesh has no unambiguous inverse.");
        CarpetPoint? result = null;
        for (var b = 0; b < grid.BLevels.Count - 1; b++)
        for (var a = 0; a < grid.ALevels.Count - 1; a++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cell = CreateCarpetCell(grid.Points, grid.ALevels.Count, a, b, grid.Offset);
            if (!TryInvertCarpetCell(cell, point, out var candidate)) continue;
            if (result is { } previous)
            {
                var aTolerance = EngineeringEpsilon * (grid.ALevels[^1] - grid.ALevels[0]);
                var bTolerance = EngineeringEpsilon * (grid.BLevels[^1] - grid.BLevels[0]);
                if (Math.Abs(previous.A - candidate.A) > aTolerance || Math.Abs(previous.B - candidate.B) > bTolerance)
                    throw new InvalidOperationException("The carpet inverse has multiple parameter solutions.");
            }
            else result = candidate;
        }
        return result ?? throw new ArgumentOutOfRangeException(nameof(point), "Point is outside the carpet mesh.");
    }

    private static int CarpetInterval(IReadOnlyList<double> levels, double value)
    {
        var lower = 0; var upper = levels.Count - 1;
        while (upper - lower > 1)
        {
            var middle = lower + (upper - lower) / 2;
            if (value < levels[middle]) upper = middle; else lower = middle;
        }
        return Math.Min(lower, levels.Count - 2);
    }

    private static CarpetCell CreateCarpetCell(IReadOnlyList<CarpetPoint> points, int width, int a, int b, double offset)
    {
        var p00 = points[b * width + a]; var p10 = points[b * width + a + 1];
        var p01 = points[(b + 1) * width + a]; var p11 = points[(b + 1) * width + a + 1];
        var dxA = p10.A - p00.A; var dxB = offset * (p01.B - p00.B);
        if (!double.IsFinite(dxA) || !double.IsFinite(dxB) || !double.IsFinite(Math.Abs(dxA) + Math.Abs(dxB)))
            throw new ArgumentOutOfRangeException(nameof(points), "Carpet cell spans cannot be represented.");
        return new(p00, p10, p01, p11, p00.A + offset * p00.B, dxA, dxB);
    }

    private static int CarpetOrientation(CarpetCell cell)
    {
        var xScale = Math.Max(Math.Abs(cell.DxA), Math.Abs(cell.DxB));
        var yScale = Math.Max(Math.Max(Math.Abs(cell.P00.Y), Math.Abs(cell.P10.Y)),
            Math.Max(Math.Abs(cell.P01.Y), Math.Abs(cell.P11.Y)));
        if (xScale == 0 || yScale == 0) return 0;
        var xa = cell.DxA / xScale; var xb = cell.DxB / xScale;
        var y00 = cell.P00.Y / yScale; var y10 = cell.P10.Y / yScale;
        var y01 = cell.P01.Y / yScale; var y11 = cell.P11.Y / yScale;
        var yu = y10 - y00; var yv = y01 - y00; var cross = (y11 - y01) - yu;
        var tolerance = EngineeringEpsilon * (Math.Abs(xa) * (Math.Abs(yv) + Math.Abs(cross)) +
            Math.Abs(xb) * (Math.Abs(yu) + Math.Abs(cross)));
        var orientation = 0;
        for (var u = 0; u <= 1; u++)
        for (var v = 0; v <= 1; v++)
        {
            var determinant = xa * (yv + cross * u) - xb * (yu + cross * v);
            if (Math.Abs(determinant) <= tolerance) return 0;
            var sign = Math.Sign(determinant);
            if (orientation != 0 && orientation != sign) return 0;
            orientation = sign;
        }
        return orientation;
    }

    private static bool TryInvertCarpetCell(CarpetCell cell, ChartPoint point, out CarpetPoint result)
    {
        result = default;
        var x10 = cell.X00 + cell.DxA; var x01 = cell.X00 + cell.DxB;
        var x11 = x10 + cell.DxB;
        var minX = Math.Min(Math.Min(cell.X00, x10), Math.Min(x01, x11));
        var maxX = Math.Max(Math.Max(cell.X00, x10), Math.Max(x01, x11));
        var minY = Math.Min(Math.Min(cell.P00.Y, cell.P10.Y), Math.Min(cell.P01.Y, cell.P11.Y));
        var maxY = Math.Max(Math.Max(cell.P00.Y, cell.P10.Y), Math.Max(cell.P01.Y, cell.P11.Y));
        if (point.X < minX || point.X > maxX || point.Y < minY || point.Y > maxY) return false;
        var xScale = Math.Max(Math.Abs(cell.DxA), Math.Abs(cell.DxB));
        var xa = cell.DxA / xScale; var xb = cell.DxB / xScale;
        var x = (point.X - cell.X00) / xScale;
        // Eliminate the larger X coefficient, ensuring that this division is well conditioned.
        var eliminateU = Math.Abs(xa) >= Math.Abs(xb);
        var p = x / (eliminateU ? xa : xb);
        var q = (eliminateU ? xb : xa) / (eliminateU ? xa : xb);
        var lower = 0d; var upper = 1d;
        if (q > 0) { lower = Math.Max(lower, (p - 1) / q); upper = Math.Min(upper, p / q); }
        else if (q < 0) { lower = Math.Max(lower, p / q); upper = Math.Min(upper, (p - 1) / q); }
        else if (p < -EngineeringEpsilon || p > 1 + EngineeringEpsilon) return false;
        if (lower > upper + EngineeringEpsilon || upper < 0 || lower > 1) return false;
        lower = Math.Clamp(lower, 0, 1); upper = Math.Clamp(upper, lower, 1);
        var yScale = Math.Max(Math.Abs(minY), Math.Abs(maxY));
        var y00 = cell.P00.Y / yScale; var y10 = cell.P10.Y / yScale;
        var y01 = cell.P01.Y / yScale; var y11 = cell.P11.Y / yScale;
        var target = point.Y / yScale;
        var tolerance = EngineeringEpsilon * Math.Max(double.Epsilon, maxY / yScale - minY / yScale);
        double Value(double t)
        {
            var eliminated = Math.Clamp(p - q * t, 0, 1);
            var u = eliminateU ? eliminated : t; var v = eliminateU ? t : eliminated;
            return Lerp(Lerp(y00, y10, u), Lerp(y01, y11, u), v) - target;
        }
        var lowValue = Value(lower); var highValue = Value(upper);
        double root;
        if (Math.Abs(lowValue) <= tolerance) root = lower;
        else if (Math.Abs(highValue) <= tolerance) root = upper;
        else
        {
            if (Math.Sign(lowValue) == Math.Sign(highValue)) return false;
            for (var iteration = 0; iteration < 64; iteration++)
            {
                var middle = (lower + upper) / 2;
                var middleValue = Value(middle);
                if (middleValue == 0) { lower = upper = middle; break; }
                if (Math.Sign(middleValue) == Math.Sign(lowValue)) { lower = middle; lowValue = middleValue; }
                else upper = middle;
            }
            root = (lower + upper) / 2;
        }
        var coordinate = Math.Clamp(p - q * root, 0, 1);
        var finalU = eliminateU ? coordinate : root; var finalV = eliminateU ? root : coordinate;
        if (Math.Abs(Value(root)) > tolerance * 4) return false;
        result = new(Lerp(cell.P00.A, cell.P10.A, finalU), Lerp(cell.P00.B, cell.P01.B, finalV), point.Y);
        return true;
    }

    private readonly record struct CarpetCell(CarpetPoint P00, CarpetPoint P10, CarpetPoint P01, CarpetPoint P11,
        double X00, double DxA, double DxB);
}
