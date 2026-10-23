namespace NT.AvaloniaControl.Charts;

public static partial class ChartAdvancedComparison
{
    /// <summary>
    /// Builds an inclusive-intersection Venn layout for two or three sets. Singleton masks are required;
    /// omitted intersections mean zero. Negative, duplicate, nonfinite or inconsistent quantities are rejected.
    /// Circle areas preserve singleton ratios. Two circles reproduce their intersection analytically.
    /// Three circles preserve pairwise distances when geometrically possible, with disclosed region errors.
    /// </summary>
    public static VennLayout Venn(IEnumerable<VennItem> source, int setCount = 3,
        CancellationToken cancellationToken = default)
    {
        if (setCount is < 2 or > 3) throw new ArgumentOutOfRangeException(nameof(setCount));
        var items = Snapshot(source, cancellationToken);
        if (items.Length == 0) return new([], [], setCount == 3, 0, 0);
        var limit = 1 << setCount;
        if (items.Length >= limit) throw new ArgumentException("There is at most one quantity per nonempty set mask.", nameof(source));
        var inputIndices = Enumerable.Repeat(-1, limit).ToArray();
        var raw = new double[limit];
        foreach (var (item, index) in items.Select((item, index) => (item, index)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item.Mask <= 0 || item.Mask >= limit) throw new ArgumentOutOfRangeException(nameof(source), "Mask lies outside the configured sets.");
            ValidateFinite(item.Value, nameof(source), true);
            if (inputIndices[item.Mask] >= 0) throw new ArgumentException("Each mask must occur at most once.", nameof(source));
            inputIndices[item.Mask] = index; raw[item.Mask] = item.Value;
        }
        double scale = 0;
        for (var set = 0; set < setCount; set++)
        {
            if (inputIndices[1 << set] < 0) throw new ArgumentException("Every singleton set total is required.", nameof(source));
            scale = Math.Max(scale, raw[1 << set]);
        }
        var inclusive = new double[limit];
        for (var mask = 1; mask < limit; mask++)
        {
            if (scale == 0 && raw[mask] != 0) throw new ArgumentException("An intersection exceeds its empty sets.", nameof(source));
            inclusive[mask] = scale == 0 ? 0 : raw[mask] / scale;
            if (raw[mask] > 0 && inclusive[mask] == 0)
                throw new OverflowException("Set quantities have a relative scale too small to represent in this layout.");
            if (!double.IsFinite(inclusive[mask]) || inclusive[mask] > 1 + 1e-12)
                throw new ArgumentException("An intersection exceeds a containing set.", nameof(source));
        }
        // Compute retained quantities in their original units. Dividing all observations by a large
        // total first can otherwise erase a valid small region or hide an invalid small-set overlap.
        var rawExclusive = ExclusiveValues(raw, true);
        var exclusive = new double[limit];
        for (var mask = 1; mask < limit; mask++) exclusive[mask] = scale == 0 ? 0 : rawExclusive[mask] / scale;
        var radii = new double[setCount];
        for (var set = 0; set < setCount; set++) radii[set] = Math.Sqrt(inclusive[1 << set]) / Math.Sqrt(Math.PI);
        var centers = new ChartPoint[setCount];
        var ab = CircleDistance(radii[0], radii[1], inclusive[3], cancellationToken);
        centers[1] = new(ab, 0);
        if (setCount == 3)
        {
            var ac = CircleDistance(radii[0], radii[2], inclusive[5], cancellationToken);
            var bc = CircleDistance(radii[1], radii[2], inclusive[6], cancellationToken);
            // Inconsistent distance triangles cannot represent arbitrary valid set regions as circles.
            // Retain the deterministic trilateration x and flatten imaginary y; report the resulting error.
            if (ab > 1e-15)
            {
                var x = (ac * ac - bc * bc + ab * ab) / (2 * ab);
                centers[2] = new(x, Math.Sqrt(Math.Max(0, ac * ac - x * x)));
            }
            else centers[2] = new(0, (ac + bc) / 2);
        }
        var representedInclusive = new double[limit];
        for (var set = 0; set < setCount; set++) representedInclusive[1 << set] = inclusive[1 << set];
        representedInclusive[3] = CircleArea(radii[0], radii[1], Distance(centers[0], centers[1]));
        double integrationError = 0;
        if (setCount == 3)
        {
            representedInclusive[5] = CircleArea(radii[0], radii[2], Distance(centers[0], centers[2]));
            representedInclusive[6] = CircleArea(radii[1], radii[2], Distance(centers[1], centers[2]));
            var coarse = TripleArea(centers, radii, 2048, cancellationToken);
            var fine = TripleArea(centers, radii, 4096, cancellationToken);
            representedInclusive[7] = fine; integrationError = Math.Abs(fine - coarse);
        }
        // Analytic and quadrature roundoff must not turn a containing set's finite maximum
        // into an overflowing estimated quantity when multiplied back to physical units.
        for (var mask = 1; mask < limit; mask++)
        {
            double bound = 1;
            for (var set = 0; set < setCount; set++)
                if ((mask & (1 << set)) != 0) bound = Math.Min(bound, inclusive[1 << set]);
            representedInclusive[mask] = Math.Clamp(representedInclusive[mask], 0, bound);
        }
        if (setCount == 3) representedInclusive[7] = Math.Min(representedInclusive[7],
            Math.Min(representedInclusive[3], Math.Min(representedInclusive[5], representedInclusive[6])));
        var representedExclusive = ExclusiveValues(representedInclusive, false);
        var regions = new VennRegion[limit - 1]; double error = 0;
        for (var mask = 1; mask < limit; mask++)
        {
            var dependencies = items.Select((item, index) => (item, index))
                .Where(row => (row.item.Mask & mask) == mask).Select(row => row.index).ToArray();
            regions[mask - 1] = new(mask, rawExclusive[mask], representedExclusive[mask] * scale, dependencies);
            error = Math.Max(error, Math.Abs(exclusive[mask] - representedExclusive[mask]));
        }
        double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
        for (var set = 0; set < setCount; set++)
        {
            minX = Math.Min(minX, centers[set].X - radii[set]); maxX = Math.Max(maxX, centers[set].X + radii[set]);
            minY = Math.Min(minY, centers[set].Y - radii[set]); maxY = Math.Max(maxY, centers[set].Y + radii[set]);
        }
        var span = Math.Max(maxX - minX, maxY - minY);
        var offsetX = (1 - (span == 0 ? 0 : (maxX - minX) / span)) / 2;
        var offsetY = (1 - (span == 0 ? 0 : (maxY - minY) / span)) / 2;
        var circles = new VennCircle[setCount];
        for (var set = 0; set < setCount; set++)
            circles[set] = new(set, span == 0 ? new(.5, .5) :
                new((centers[set].X - minX) / span + offsetX, (centers[set].Y - minY) / span + offsetY),
                span == 0 ? 0 : radii[set] / span, raw[1 << set], inputIndices[1 << set]);
        return new(circles, regions, setCount == 3, error, integrationError * scale);
    }

    private static double[] ExclusiveValues(double[] inclusive, bool validate)
    {
        var exclusive = new double[inclusive.Length];
        for (var mask = inclusive.Length - 1; mask > 0; mask--)
        {
            var value = inclusive[mask];
            for (var superset = mask + 1; superset < inclusive.Length; superset++)
                if ((superset & mask) == mask) value -= exclusive[superset];
            if (validate && value < 0 && (inclusive[mask] == 0 || -value / inclusive[mask] > 1e-12))
                throw new ArgumentException("Inclusive intersections imply a negative exclusive region.", "source");
            exclusive[mask] = Math.Max(0, value);
        }
        return exclusive;
    }

    private static double Distance(ChartPoint a, ChartPoint b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static double CircleDistance(double a, double b, double area, CancellationToken cancellationToken)
    {
        var maximum = Math.PI * Math.Min(a, b) * Math.Min(a, b);
        if (area <= 0) return a + b;
        if (area >= maximum) return Math.Abs(a - b);
        double low = Math.Abs(a - b), high = a + b;
        for (var iteration = 0; iteration < 100; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var middle = (low + high) / 2;
            if (CircleArea(a, b, middle) > area) low = middle; else high = middle;
        }
        return (low + high) / 2;
    }

    private static double CircleArea(double a, double b, double distance)
    {
        if (a == 0 || b == 0 || distance >= a + b) return 0;
        if (distance <= Math.Abs(a - b)) return Math.PI * Math.Min(a, b) * Math.Min(a, b);
        var alpha = Math.Acos(Math.Clamp((distance * distance + a * a - b * b) / (2 * distance * a), -1, 1));
        var beta = Math.Acos(Math.Clamp((distance * distance + b * b - a * a) / (2 * distance * b), -1, 1));
        var product = (-distance + a + b) * (distance + a - b) * (distance - a + b) * (distance + a + b);
        return Math.Max(0, a * a * alpha + b * b * beta - .5 * Math.Sqrt(Math.Max(0, product)));
    }

    private static double TripleArea(ChartPoint[] centers, double[] radii, int slices, CancellationToken cancellationToken)
    {
        double left = double.NegativeInfinity, right = double.PositiveInfinity;
        for (var set = 0; set < 3; set++)
        { left = Math.Max(left, centers[set].X - radii[set]); right = Math.Min(right, centers[set].X + radii[set]); }
        if (right <= left) return 0;
        var step = (right - left) / slices; double sum = 0;
        for (var slice = 0; slice < slices; slice++)
        {
            if ((slice & 127) == 0) cancellationToken.ThrowIfCancellationRequested();
            var x = left + (slice + .5) * step;
            double bottom = double.NegativeInfinity, top = double.PositiveInfinity;
            for (var set = 0; set < 3; set++)
            {
                var dx = x - centers[set].X;
                var halfHeight = Math.Sqrt(Math.Max(0, radii[set] * radii[set] - dx * dx));
                bottom = Math.Max(bottom, centers[set].Y - halfHeight); top = Math.Min(top, centers[set].Y + halfHeight);
            }
            sum += Math.Max(0, top - bottom);
        }
        return sum * step;
    }
}
