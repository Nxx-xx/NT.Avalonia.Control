using System.Collections;

namespace NT.AvaloniaControl.Charts;

/// <summary>
/// Display-only source ordinals. An intentional sampling skip remains within its
/// original figure; a source gap always starts a new figure. The source snapshot
/// and all interaction metadata remain untouched.
/// </summary>
internal sealed class ChartLineSampling : IReadOnlyList<int>
{
    private readonly int[] _ordinals;
    private readonly bool[] _starts;

    internal ChartLineSampling(long version, int sourceCount, int[] ordinals, bool[] starts,
        int rawVisibleCount, int inputCount, int buckets, int bucketBudget, int figures)
    {
        SourceVersion = version; SourceCount = sourceCount;
        _ordinals = ordinals; _starts = starts;
        RawVisibleCount = rawVisibleCount; InputCount = inputCount;
        BucketCount = buckets; BucketBudget = bucketBudget; FigureCount = figures;
    }

    public int Count => _ordinals.Length;
    public int this[int index] => _ordinals[index];
    internal long SourceVersion { get; }
    internal int SourceCount { get; }
    internal int RawVisibleCount { get; }
    internal int InputCount { get; }
    internal int BucketCount { get; }
    internal int BucketBudget { get; }
    internal int FigureCount { get; }
    internal bool IsReduced => Count < InputCount;
    internal bool StartsFigure(int index) => _starts[index];
    public IEnumerator<int> GetEnumerator() => ((IEnumerable<int>)_ordinals).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
