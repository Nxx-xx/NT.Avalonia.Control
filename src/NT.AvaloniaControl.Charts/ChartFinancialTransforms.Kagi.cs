namespace NT.AvaloniaControl.Charts;

public static partial class ChartFinancialTransforms
{
    /// <summary>
    /// Close-only Kagi with an absolute reversal amount. A first nonzero move starts a thin line.
    /// Reaching the reversal distance starts another slot. Passing the previous shoulder changes thin
    /// to thick, and passing the previous waist changes thick to thin; exact touches retain the style.
    /// A style crossing splits one slot into two connected, source-traceable display segments.
    /// </summary>
    public static IReadOnlyList<ChartFinancialSample> Kagi(IEnumerable<ChartFinancialPoint> source,
        double reversalAmount, int maxGenerated = DefaultMaximumGeneratedSamples,
        CancellationToken cancellationToken = default)
    {
        if (!double.IsFinite(reversalAmount) || reversalAmount <= 0)
            throw new ArgumentOutOfRangeException(nameof(reversalAmount), "Reversal amount must be finite and positive.");
        CheckBudget(maxGenerated);
        var input = Validate(source, cancellationToken);
        var segments = new List<FinancialColumn>();
        var seeded = false; var direction = 0; var previousSource = 0;
        var slot = -1; var segmentStart = true; var thick = false;
        double last = 0; double? shoulder = null, waist = null;
        for (var i = 0; i < input.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var p = input[i];
            if (p.IsGap)
            {
                seeded = false; direction = 0; segmentStart = true; thick = false;
                shoulder = null; waist = null; continue;
            }
            if (!seeded) { last = p.Close; previousSource = i; seeded = true; continue; }
            var delta = Difference(p.Close, last);
            var nextDirection = Math.Sign(delta);
            if (nextDirection == 0) continue;
            if (direction != 0 && nextDirection != direction && Math.Abs(delta) < reversalAmount) continue;
            var extending = direction != 0 && nextDirection == direction;
            if (!extending)
            {
                if (direction > 0) shoulder = last;
                if (direction < 0) waist = last;
                slot++;
            }
            var crossing = !thick && nextDirection > 0 && shoulder is { } high && p.Close > high
                ? shoulder : thick && nextDirection < 0 && waist is { } low && p.Close < low ? waist : null;
            if (crossing is { } threshold)
            {
                if (last != threshold)
                {
                    if (extending) { segments[^1].Close = threshold; segments[^1].End = i; }
                    else Add(last, threshold, previousSource, i, segmentStart, thick);
                    segmentStart = false;
                }
                thick = !thick;
                Add(threshold, p.Close, previousSource, i, segmentStart, thick);
            }
            else if (extending) { segments[^1].Close = p.Close; segments[^1].End = i; }
            else Add(last, p.Close, previousSource, i, segmentStart, thick);
            direction = nextDirection; last = p.Close; previousSource = i; segmentStart = false;
        }
        return Array.AsReadOnly(segments.Select(s => new ChartFinancialSample(s.Slot, s.Open,
            Math.Max(s.Open, s.Close), Math.Min(s.Open, s.Close), s.Close, input, s.Start, s.End,
            ChartFinancialTransformKind.Kagi, s.Direction, s.SegmentStart, s.Thick)).ToArray());

        void Add(double open, double close, int start, int end, bool starts, bool isThick)
        {
            EnsureBudget(segments.Count, 1, maxGenerated);
            segments.Add(new(slot, open, close, start, end, Math.Sign(close.CompareTo(open)), starts, isThick));
        }
    }
}
