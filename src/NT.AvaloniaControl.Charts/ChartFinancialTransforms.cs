namespace NT.AvaloniaControl.Charts;

/// <summary>
/// Deterministic close-only financial transforms. These are display calculations, not execution prices.
/// Every gap resets the calculation. Results and their input snapshots are immutable.
/// </summary>
public static partial class ChartFinancialTransforms
{
    public const int MaximumInputPoints = 1_000_000;
    public const int MaximumGeneratedSamples = 1_000_000;
    public const int DefaultMaximumGeneratedSamples = 100_000;

    /// <summary>Copies and validates strict chronological order, the OHLC envelope, and optional nonnegative volume.
    /// Times are compared as UTC instants; negative prices are supported. Gap prices and volume are ignored.</summary>
    public static IReadOnlyList<ChartFinancialPoint> Validate(IEnumerable<ChartFinancialPoint> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        var result = new List<ChartFinancialPoint>();
        foreach (var point in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (result.Count == MaximumInputPoints)
                throw new ArgumentOutOfRangeException(nameof(source), "Financial input exceeds the one-million observation budget.");
            if (result.Count > 0 && point.Time <= result[^1].Time)
                throw new ArgumentException("Financial timestamps must be strictly increasing UTC instants.", nameof(source));
            if (!point.IsGap && (!double.IsFinite(point.Open) || !double.IsFinite(point.High) ||
                !double.IsFinite(point.Low) || !double.IsFinite(point.Close) ||
                point.Low > point.High || point.Low > Math.Min(point.Open, point.Close) ||
                point.High < Math.Max(point.Open, point.Close) ||
                point.Volume is { } volume && (!double.IsFinite(volume) || volume < 0)))
                throw new ArgumentException("OHLC must be finite within its low/high envelope and volume must be finite and nonnegative.", nameof(source));
            result.Add(point);
        }
        return Array.AsReadOnly(result.ToArray());
    }

    /// <summary>Original prices. Gap slots remain empty; Slot is the original input index.</summary>
    public static IReadOnlyList<ChartFinancialSample> Raw(IEnumerable<ChartFinancialPoint> source,
        int maxGenerated = DefaultMaximumGeneratedSamples, CancellationToken cancellationToken = default)
    {
        CheckBudget(maxGenerated);
        var input = Validate(source, cancellationToken);
        var output = new List<ChartFinancialSample>();
        var segmentStart = true;
        for (var i = 0; i < input.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var p = input[i];
            if (p.IsGap) { segmentStart = true; continue; }
            EnsureBudget(output.Count, 1, maxGenerated);
            output.Add(new(i, p.Open, p.High, p.Low, p.Close, input, i, i,
                ChartFinancialTransformKind.Raw, Math.Sign(p.Close.CompareTo(p.Open)), segmentStart));
            segmentStart = false;
        }
        return Array.AsReadOnly(output.ToArray());
    }

    /// <summary>HA close is the current OHLC mean, HA open the preceding HA open/close midpoint.
    /// The first HA open of each segment is the original open/close midpoint.</summary>
    public static IReadOnlyList<ChartFinancialSample> HeikinAshi(IEnumerable<ChartFinancialPoint> source,
        int maxGenerated = DefaultMaximumGeneratedSamples, CancellationToken cancellationToken = default)
    {
        CheckBudget(maxGenerated);
        var input = Validate(source, cancellationToken);
        var output = new List<ChartFinancialSample>();
        var segmentStart = true;
        double previousOpen = 0, previousClose = 0;
        for (var i = 0; i < input.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var p = input[i];
            if (p.IsGap) { segmentStart = true; continue; }
            EnsureBudget(output.Count, 1, maxGenerated);
            var close = Mean4(p.Open, p.High, p.Low, p.Close);
            var open = segmentStart ? Mean2(p.Open, p.Close) : Mean2(previousOpen, previousClose);
            output.Add(new(i, open, Math.Max(p.High, Math.Max(open, close)),
                Math.Min(p.Low, Math.Min(open, close)), close, input, i, i,
                ChartFinancialTransformKind.HeikinAshi, Math.Sign(close.CompareTo(open)), segmentStart));
            previousOpen = open; previousClose = close; segmentStart = false;
        }
        return Array.AsReadOnly(output.ToArray());
    }

    /// <summary>Fixed-box close-only Renko, anchored at each segment's first close.
    /// Reversal requires reversalBoxes boxes; its first brick skips reversalBoxes-1 price boxes.
    /// Each generated brick counts against maxGenerated; no rounding or automatic box enlargement occurs.</summary>
    public static IReadOnlyList<ChartFinancialSample> Renko(IEnumerable<ChartFinancialPoint> source,
        double boxSize, int reversalBoxes = 2, int maxGenerated = DefaultMaximumGeneratedSamples,
        CancellationToken cancellationToken = default)
    {
        CheckBoxes(boxSize, reversalBoxes); CheckBudget(maxGenerated);
        var input = Validate(source, cancellationToken);
        var output = new List<ChartFinancialSample>();
        var seeded = false; var direction = 0; var previousSource = 0; var segmentStart = true;
        double last = 0;
        for (var i = 0; i < input.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var p = input[i];
            if (p.IsGap) { seeded = false; direction = 0; segmentStart = true; continue; }
            if (!seeded) { last = p.Close; previousSource = i; seeded = true; continue; }
            var delta = Difference(p.Close, last);
            var nextDirection = Math.Sign(delta);
            if (nextDirection == 0) continue;
            var steps = WholeBoxes(Math.Abs(delta), boxSize);
            var reversing = direction != 0 && direction != nextDirection;
            var skipped = reversing ? reversalBoxes - 1 : 0;
            if (steps <= skipped) continue;
            var generated = steps - skipped;
            EnsureBudget(output.Count, generated, maxGenerated);
            var open = skipped == 0 ? last : Step(last, nextDirection, skipped, boxSize);
            for (var k = 0; k < (int)generated; k++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var close = Step(open, nextDirection, 1, boxSize);
                output.Add(new(output.Count, open, Math.Max(open, close), Math.Min(open, close), close,
                    input, previousSource, i, ChartFinancialTransformKind.Renko, nextDirection,
                    segmentStart, boxSize: boxSize, boxCount: 1));
                open = close; last = close; previousSource = i; segmentStart = false;
            }
            direction = nextDirection;
        }
        return Array.AsReadOnly(output.ToArray());
    }

    /// <summary>Fixed-box close-only X/O columns. Open is the preceding extreme; letters are at
    /// Open + Direction * (1..BoxCount) * BoxSize, ending at Close. maxGenerated limits total letters.</summary>
    public static IReadOnlyList<ChartFinancialSample> PointAndFigure(IEnumerable<ChartFinancialPoint> source,
        double boxSize, int reversalBoxes = 3, int maxGenerated = DefaultMaximumGeneratedSamples,
        CancellationToken cancellationToken = default)
    {
        CheckBoxes(boxSize, reversalBoxes); CheckBudget(maxGenerated);
        var input = Validate(source, cancellationToken);
        var columns = new List<FinancialColumn>();
        var seeded = false; var direction = 0; var previousSource = 0; var segmentStart = true;
        var letters = 0; double last = 0;
        for (var i = 0; i < input.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var p = input[i];
            if (p.IsGap) { seeded = false; direction = 0; segmentStart = true; continue; }
            if (!seeded) { last = p.Close; previousSource = i; seeded = true; continue; }
            var delta = Difference(p.Close, last);
            var nextDirection = Math.Sign(delta);
            if (nextDirection == 0) continue;
            var steps = WholeBoxes(Math.Abs(delta), boxSize);
            var reversing = direction != 0 && direction != nextDirection;
            if (steps < (reversing ? reversalBoxes : 1)) continue;
            EnsureBudget(letters, steps, maxGenerated);
            var close = Step(last, nextDirection, (int)steps, boxSize);
            // A batch advance can be finite even when individual X/O boxes collapse at this
            // price magnitude. Check both ends and the coarsest spacing on the interval.
            _ = Step(last, nextDirection, 1, boxSize);
            _ = Step(close, -nextDirection, 1, boxSize);
            var coarseEnd = Math.Abs(last) >= Math.Abs(close) ? last : close;
            var towardsInterior = coarseEnd == last ? nextDirection : -nextDirection;
            var adjacent = towardsInterior > 0 ? Math.BitIncrement(coarseEnd) : Math.BitDecrement(coarseEnd);
            if (boxSize < Math.Abs(adjacent - coarseEnd))
                throw new ArithmeticException("Box size is smaller than the representable price spacing of the column.");
            if (direction == nextDirection)
            {
                var column = columns[^1];
                column.Close = close; column.End = i; column.Boxes += (int)steps;
            }
            else columns.Add(new(columns.Count, last, close, previousSource, i, nextDirection,
                segmentStart, false, (int)steps));
            letters += (int)steps; last = close; direction = nextDirection;
            previousSource = i; segmentStart = false;
        }
        return Array.AsReadOnly(columns.Select(c => new ChartFinancialSample(c.Slot, c.Open,
            Math.Max(c.Open, c.Close), Math.Min(c.Open, c.Close), c.Close, input, c.Start, c.End,
            ChartFinancialTransformKind.PointAndFigure, c.Direction, c.SegmentStart,
            boxSize: boxSize, boxCount: c.Boxes)).ToArray());
    }

    private static void CheckBudget(int maxGenerated)
    {
        if (maxGenerated is < 1 or > MaximumGeneratedSamples)
            throw new ArgumentOutOfRangeException(nameof(maxGenerated), "Output budget must be between one and one million.");
    }
    private static void CheckBoxes(double boxSize, int reversalBoxes)
    {
        if (!double.IsFinite(boxSize) || boxSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(boxSize), "Box size must be finite and positive.");
        if (reversalBoxes is < 1 or > MaximumGeneratedSamples || !double.IsFinite(boxSize * reversalBoxes))
            throw new ArgumentOutOfRangeException(nameof(reversalBoxes), "Reversal distance must be finite and use 1..1000000 boxes.");
    }
    private static void EnsureBudget(int count, double additional, int maximum)
    {
        if (!double.IsFinite(additional) || additional > maximum - count)
            throw new InvalidOperationException("Financial transform exceeds the configured generated-glyph budget.");
    }
    private static double Difference(double a, double b)
    {
        var difference = a - b;
        return double.IsFinite(difference) ? difference : throw new ArithmeticException("Price difference exceeds finite precision.");
    }
    private static double WholeBoxes(double distance, double boxSize) => Math.Floor(distance / boxSize);
    private static double Step(double price, int direction, int count, double boxSize)
    {
        var result = price + direction * (count * boxSize);
        if (!double.IsFinite(result) || result == price)
            throw new ArithmeticException("Box size cannot produce a finite, distinct price at this magnitude.");
        return result;
    }
    // Use the ordinary sum when possible to preserve subnormal prices; split only an overflowing sum.
    private static double Mean2(double a, double b)
    {
        var sum = a + b;
        return double.IsFinite(sum) ? sum / 2 : a / 2 + b / 2;
    }
    private static double Mean4(double a, double b, double c, double d) =>
        a == b && b == c && c == d ? a :
            NearUnderflow(a) || NearUnderflow(b) || NearUnderflow(c) || NearUnderflow(d)
                ? Mean2(Mean2(a, b), Mean2(c, d)) : a / 4 + b / 4 + c / 4 + d / 4;
    private static bool NearUnderflow(double value) => value != 0 && Math.Abs(value) < 8.900295434028806e-308;

    private sealed class FinancialColumn(int slot, double open, double close, int start, int end,
        int direction, bool segmentStart, bool thick, int boxes = 0)
    {
        public int Slot = slot;
        public double Open = open;
        public double Close = close;
        public int Start = start;
        public int End = end;
        public int Direction = direction;
        public bool SegmentStart = segmentStart;
        public bool Thick = thick;
        public int Boxes = boxes;
    }
}
