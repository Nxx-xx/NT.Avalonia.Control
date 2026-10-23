using System.Globalization;

namespace NT.AvaloniaControl.Charts;

public static partial class ChartEngineering
{
    private static readonly string[] CompassDirections =
        ["N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW"];

    /// <summary>
    /// Parses a case-insensitive 16-point compass label, full English compass name, or invariant numeric
    /// angle. Returns degrees clockwise from north in [0,360). Numeric strings use angleUnit; compass
    /// labels retain their compass meaning. A degree symbol or a 'deg'/'rad' suffix overrides angleUnit.
    /// </summary>
    public static double ParseWindDirection(string direction, ChartAngleUnit angleUnit = ChartAngleUnit.Degrees)
    {
        ArgumentNullException.ThrowIfNull(direction);
        if (!Enum.IsDefined(angleUnit)) throw new ArgumentOutOfRangeException(nameof(angleUnit));
        var text = direction.Trim();
        var compass = text.Replace("-", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
        for (var i = 0; i < CompassDirections.Length; i++)
            if (compass == CompassDirections[i]) return i * 22.5;
        var expanded = compass.Replace("NORTH", "N", StringComparison.Ordinal)
            .Replace("SOUTH", "S", StringComparison.Ordinal).Replace("EAST", "E", StringComparison.Ordinal)
            .Replace("WEST", "W", StringComparison.Ordinal);
        for (var i = 0; i < CompassDirections.Length; i++)
            if (expanded == CompassDirections[i]) return i * 22.5;
        if (text.EndsWith("rad", StringComparison.OrdinalIgnoreCase))
        { angleUnit = ChartAngleUnit.Radians; text = text[..^3].TrimEnd(); }
        else if (text.EndsWith("deg", StringComparison.OrdinalIgnoreCase))
        { angleUnit = ChartAngleUnit.Degrees; text = text[..^3].TrimEnd(); }
        else if (text.EndsWith('°'))
        { angleUnit = ChartAngleUnit.Degrees; text = text[..^1].TrimEnd(); }
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var angle) || !double.IsFinite(angle))
            throw new FormatException("Direction must be a finite invariant angle or a 16-point compass direction.");
        return WindDegrees(angle, angleUnit);
    }

    /// <summary>
    /// Aggregates positive frequency weights into direction/speed bands. Zero degrees is north and
    /// angles increase clockwise. Each sector is centered on its labeled direction, left-closed and
    /// right-open, including the wrapping north sector. Speed bands are [lower,upper), with an implicit
    /// final +infinity upper bound. Percentages sum to 100 across all accepted observations.
    /// </summary>
    public static WindRoseResult WindRose(IEnumerable<WindObservation> source, IEnumerable<double> speedUpperBounds,
        int sectorCount = 16, ChartAngleUnit angleUnit = ChartAngleUnit.Degrees,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(speedUpperBounds);
        if (sectorCount < 1 || sectorCount > 360) throw new ArgumentOutOfRangeException(nameof(sectorCount));
        if (!Enum.IsDefined(angleUnit)) throw new ArgumentOutOfRangeException(nameof(angleUnit));
        cancellationToken.ThrowIfCancellationRequested();
        var boundsList = new List<double>();
        foreach (var bound in speedUpperBounds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!double.IsFinite(bound) || bound <= 0 || boundsList.Count > 0 && bound <= boundsList[^1])
                throw new ArgumentException("Speed upper bounds must be finite, strictly increasing, and positive.", nameof(speedUpperBounds));
            if ((long)(boundsList.Count + 2) * sectorCount > MaximumCount)
                throw new ArgumentOutOfRangeException(nameof(speedUpperBounds), "Wind bands exceed the engineering geometry budget.");
            boundsList.Add(bound);
        }
        var bounds = boundsList.ToArray();
        var bands = bounds.Length + 1;
        var accumulators = new WindAccumulator?[sectorCount * bands];
        var index = 0; var omitted = 0; var total = 0d;
        var width = 360d / sectorCount;
        foreach (var observation in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CountInput(index);
            var originalIndex = index++;
            if (!double.IsFinite(observation.Direction) || !double.IsFinite(observation.Speed) || observation.Speed < 0 ||
                !double.IsFinite(observation.Frequency) || observation.Frequency <= 0)
            { omitted++; continue; }
            var degrees = WindDegrees(observation.Direction, angleUnit);
            // Compare the remainder, rather than adding half a width: addition can round a value
            // immediately below a boundary onto the boundary and move it to the next sector.
            var sector = Math.Min(sectorCount - 1, (int)Math.Floor(degrees / width));
            if (degrees - sector * width >= width / 2) sector++;
            if (sector == sectorCount) sector = 0;
            var band = Array.BinarySearch(bounds, observation.Speed);
            // Exact speed boundaries enter the next band.
            band = band >= 0 ? band + 1 : ~band;
            var slot = sector * bands + band;
            var accumulator = accumulators[slot] ??= new();
            accumulator.Indices.Add(originalIndex);
            accumulator.Frequency = CheckedFinite(accumulator.Frequency + observation.Frequency, "Wind band frequency overflowed.");
            total = CheckedFinite(total + observation.Frequency, "Total wind frequency overflowed.");
        }
        var sectors = new WindRoseSector[sectorCount];
        var bins = new WindRoseBin[accumulators.Length];
        for (var sector = 0; sector < sectorCount; sector++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sectorBins = new WindRoseBin[bands];
            var cumulative = 0d;
            for (var band = 0; band < bands; band++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var slot = sector * bands + band;
                var accumulator = accumulators[slot];
                var frequency = accumulator?.Frequency ?? 0;
                var next = CheckedFinite(cumulative + frequency, "Stacked wind frequency overflowed.");
                var bin = new WindRoseBin(sector, band, band == 0 ? 0 : bounds[band - 1],
                    band == bounds.Length ? double.PositiveInfinity : bounds[band], accumulator?.Indices.ToArray() ?? [],
                    frequency, total == 0 ? 0 : frequency / total * 100, cumulative, next);
                bins[slot] = sectorBins[band] = bin;
                cumulative = next;
            }
            var direction = sector * width;
            var label = 16 % sectorCount == 0 ? CompassDirections[sector * (16 / sectorCount)] :
                direction.ToString("0.###", CultureInfo.InvariantCulture) + "°";
            sectors[sector] = new(sector, direction, width, label, sectorBins);
        }
        return new(sectors, bins, bounds, index - omitted, omitted, total);
    }

    private static double WindDegrees(double angle, ChartAngleUnit unit)
    {
        var degrees = unit == ChartAngleUnit.Degrees ? angle % 360 : angle % (2 * Math.PI) * (180 / Math.PI);
        if (degrees < 0) degrees += 360;
        return degrees >= 360 ? 0 : degrees;
    }

    private sealed class WindAccumulator
    {
        internal List<int> Indices { get; } = [];
        internal double Frequency { get; set; }
    }
}
