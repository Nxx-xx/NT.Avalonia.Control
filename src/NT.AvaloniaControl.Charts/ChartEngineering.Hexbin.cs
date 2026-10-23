namespace NT.AvaloniaControl.Charts;

public static partial class ChartEngineering
{
    /// <summary>
    /// Bins Cartesian coordinates into pointy-top regular hexagons, using the same units for radius, origin,
    /// and observations. For a pixel-sized lattice, project all observations into DIP coordinates first.
    /// Nonfinite coordinates/values are omitted. Cube rounding uses AwayFromZero, resolving largest-error
    /// ties in Q, cube-Y, then R order. Membership indices always refer to the original enumeration.
    /// </summary>
    public static HexbinResult Hexbin(IEnumerable<HexbinPoint> source, double radius, ChartPoint origin = default,
        HexbinAggregation aggregation = HexbinAggregation.Count, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!double.IsFinite(radius) || radius <= 0) throw new ArgumentOutOfRangeException(nameof(radius));
        ValidatePoint(origin, nameof(origin));
        if (!Enum.IsDefined(aggregation)) throw new ArgumentOutOfRangeException(nameof(aggregation));
        cancellationToken.ThrowIfCancellationRequested();
        var members = new Dictionary<(long Q, long R), HexAccumulator>();
        var index = 0; var omitted = 0;
        foreach (var point in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CountInput(index);
            var sourceIndex = index++;
            if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) || !double.IsFinite(point.Value))
            { omitted++; continue; }
            var x = (point.X - origin.X) / radius;
            var y = (point.Y - origin.Y) / radius;
            // Share the third: independently evaluating (2/3)*y can break q == cubeY at
            // x == 0 by a few ulps and change the documented largest-error tie decision.
            var yThird = y / 3;
            var q = Math.Sqrt(3) / 3 * x - yThird;
            var r = 2 * yThird;
            if (!double.IsFinite(q) || !double.IsFinite(r) || Math.Abs(q) > (1L << 50) || Math.Abs(r) > (1L << 50) ||
                Math.Abs(q + r) > (1L << 50))
                throw new ArgumentOutOfRangeException(nameof(source), "Coordinates exceed the representable axial lattice for this radius and origin.");
            var key = RoundHex(q, r);
            if (!members.TryGetValue(key, out var bin))
            {
                if (members.Count >= MaximumCount / 6)
                    throw new ArgumentOutOfRangeException(nameof(source), "Hexagonal vertices exceed the engineering geometry budget.");
                bin = new(); members.Add(key, bin);
            }
            bin.Indices.Add(sourceIndex);
            if (aggregation == HexbinAggregation.Sum)
                bin.Sum = CheckedFinite(bin.Sum + point.Value, "Hexbin value aggregation overflowed.");
            else if (aggregation == HexbinAggregation.Mean)
            {
                var count = bin.Indices.Count;
                if (count == 1) bin.Mean = point.Value;
                else
                {
                    // Same-sign subtraction cannot overflow. Opposite-sign weighting avoids the
                    // potentially overflowing (value - mean), without ever forming a running sum.
                    var sameSign = bin.Mean >= 0 && point.Value >= 0 || bin.Mean <= 0 && point.Value <= 0;
                    bin.Mean = CheckedFinite(sameSign ? bin.Mean + (point.Value - bin.Mean) / count :
                        bin.Mean * ((count - 1d) / count) + point.Value / count, "Hexbin mean aggregation overflowed.");
                }
            }
        }
        var bins = new HexbinBin[members.Count];
        var output = 0;
        foreach (var (key, accumulator) in members.OrderBy(pair => pair.Key.Q).ThenBy(pair => pair.Key.R))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var center = new ChartPoint(origin.X + radius * Math.Sqrt(3) * (key.Q + key.R / 2d),
                origin.Y + radius * 1.5 * key.R);
            if (!IsFinite(center)) throw new OverflowException("Hexbin center overflowed.");
            var vertices = new ChartPoint[6];
            for (var corner = 0; corner < 6; corner++)
            {
                var angle = (30 + 60 * corner) * Math.PI / 180;
                vertices[corner] = new(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle));
                if (!IsFinite(vertices[corner])) throw new OverflowException("Hexbin vertex overflowed.");
            }
            var value = aggregation switch
            {
                HexbinAggregation.Count => accumulator.Indices.Count,
                HexbinAggregation.Sum => accumulator.Sum,
                _ => accumulator.Mean,
            };
            bins[output++] = new(key.Q, key.R, center, vertices, accumulator.Indices.ToArray(), value);
        }
        return new(bins, index - omitted, omitted, radius, origin, aggregation);
    }

    private static (long Q, long R) RoundHex(double q, double r)
    {
        var cubeY = -q - r;
        var roundedQ = Math.Round(q, MidpointRounding.AwayFromZero);
        var roundedY = Math.Round(cubeY, MidpointRounding.AwayFromZero);
        var roundedR = Math.Round(r, MidpointRounding.AwayFromZero);
        var qError = Math.Abs(roundedQ - q); var yError = Math.Abs(roundedY - cubeY); var rError = Math.Abs(roundedR - r);
        if (qError >= yError && qError >= rError) roundedQ = -roundedY - roundedR;
        else if (yError >= rError) roundedY = -roundedQ - roundedR;
        else roundedR = -roundedQ - roundedY;
        return ((long)roundedQ, (long)roundedR);
    }

    private sealed class HexAccumulator
    {
        internal List<int> Indices { get; } = [];
        internal double Sum { get; set; }
        internal double Mean { get; set; }
    }
}
