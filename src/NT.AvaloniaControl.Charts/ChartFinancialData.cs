namespace NT.AvaloniaControl.Charts;

/// <summary>One original trading observation. A gap carries a timestamp but no prices or volume.</summary>
public readonly record struct ChartFinancialPoint(DateTimeOffset Time, double Open, double High, double Low,
    double Close, double? Volume = null, bool IsGap = false);

/// <summary>The calculation used to produce a financial glyph.</summary>
public enum ChartFinancialTransformKind { Raw, HeikinAshi, Renko, PointAndFigure, Kagi }

/// <summary>
/// An immutable display glyph and its associated, unmodified input identities. Display prices of transformed
/// glyphs are synthetic; SourceEnd and Sources contain actual observations. For Heikin-Ashi the source
/// range is its corresponding trading row, not the entire recursive calculation dependency history.
/// </summary>
public sealed class ChartFinancialSample
{
    internal ChartFinancialSample(int slot, double open, double high, double low, double close,
        IReadOnlyList<ChartFinancialPoint> source, int start, int end, ChartFinancialTransformKind kind,
        int direction, bool segmentStart, bool thick = false, double? boxSize = null, int boxCount = 0)
    {
        Slot = slot; Open = open; High = high; Low = low; Close = close; SourceStartIndex = start;
        SourceEndIndex = end; SourceStart = source[start]; SourceEnd = source[end];
        Time = SourceEnd.Time; Volume = SourceEnd.Volume; Kind = kind; Direction = direction;
        IsSegmentStart = segmentStart; IsThick = thick; BoxSize = boxSize; BoxCount = boxCount;
        Sources = new SourceSlice(source, start, end - start + 1);
    }
    public int Slot { get; }
    public DateTimeOffset Time { get; }
    public double Open { get; }
    public double High { get; }
    public double Low { get; }
    public double Close { get; }
    /// <summary>The last associated observation's volume; it is not aggregated or synthetic volume.</summary>
    public double? Volume { get; }
    public int SourceStartIndex { get; }
    public int SourceEndIndex { get; }
    public ChartFinancialPoint SourceStart { get; }
    public ChartFinancialPoint SourceEnd { get; }
    public IReadOnlyList<ChartFinancialPoint> Sources { get; }
    public ChartFinancialTransformKind Kind { get; }
    public int Direction { get; }
    public bool IsThick { get; }
    public bool IsSegmentStart { get; }
    public double? BoxSize { get; }
    public int BoxCount { get; }

    private sealed class SourceSlice(IReadOnlyList<ChartFinancialPoint> source, int start, int count)
        : IReadOnlyList<ChartFinancialPoint>
    {
        public int Count => count;
        public ChartFinancialPoint this[int index] => index >= 0 && index < count
            ? source[start + index] : throw new ArgumentOutOfRangeException(nameof(index));
        public IEnumerator<ChartFinancialPoint> GetEnumerator()
        { for (var i = 0; i < count; i++) yield return source[start + i]; }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
