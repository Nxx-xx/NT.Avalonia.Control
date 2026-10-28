using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

internal sealed partial class CartesianSeriesRenderer
{
    private const int MinimumSharedBarHistoryEntries = 2_048;
    private const int MaximumBarHistoryEntries = MaximumCachedBars * 2;
    private static readonly ConditionalWeakTable<ChartSeries, BarGeometryHistory> BarHistories = new();

    internal static void ReleaseBarGeometryHistory(ChartSeries series)
    {
        if (!BarHistories.TryGetValue(series, out var history)) return;
        BarHistories.Remove(series);
        history.Deactivate();
    }

    private static BarGeometryHistory? RetainBarGeometryHistory(ChartSeries series, RectangleGeometry?[]? rectangles)
    {
        // A public series can change its renderer family. The shared ledger
        // reserves only the currently retained family, so retire the other one.
        ReleaseCircleGeometryHistory(series);
        ChartNativeCircleBackend.Clear(series);
        if (rectangles is null || rectangles.Length == 0 || !series.CanShareRenderingGeometryHistory)
        {
            ReleaseBarGeometryHistory(series);
            return null;
        }
        // A sparse moving view reserves an existing dense history, but neither
        // creates one nor grows it. Its rectangles remain in its own slots.
        if (rectangles.Length < MinimumSharedBarHistoryEntries)
            return BarHistories.TryGetValue(series, out var existing) ? existing : null;
        var history = BarHistories.GetValue(series, static _ => new BarGeometryHistory());
        history.Reserve(rectangles.Length);
        return history;
    }

    private static BarGeometryHistory? RecordingBarGeometryHistory(ChartSeries series, BarGeometryHistory? history, int count)
    {
        if (history is not null && !series.CanShareRenderingGeometryHistory)
        {
            ReleaseBarGeometryHistory(series);
            return null;
        }
        return count >= MinimumSharedBarHistoryEntries ? history : null;
    }

    private readonly record struct BarGeometryKey(long X, long Y, long Width, long Height)
    {
        internal static BarGeometryKey For(Rect bounds) => new(
            BitConverter.DoubleToInt64Bits(bounds.X), BitConverter.DoubleToInt64Bits(bounds.Y),
            BitConverter.DoubleToInt64Bits(bounds.Width), BitConverter.DoubleToInt64Bits(bounds.Height));
    }

    // UI-thread-only, paint-independent input geometry. DrawingGroup's sealed
    // recorder still owns a new mutable public geometry wrapper on every replay.
    private sealed class BarGeometryHistory
    {
        private readonly Dictionary<BarGeometryKey, LinkedListNode<Entry>> _entries = [];
        private readonly LinkedList<Entry> _lru = [];
        internal bool Active { get; private set; } = true;
        internal int Capacity { get; private set; }
        internal int Count => _entries.Count;

        internal void Reserve(int currentCount)
        {
            if (!Active) return;
            var capacity = Math.Min(MaximumBarHistoryEntries, currentCount * 2);
            if (capacity <= Capacity) return;
            _entries.EnsureCapacity(capacity);
            Capacity = capacity;
        }

        internal RectangleGeometry GetOrCreate(Rect bounds)
        {
            // Old retained plans can be replayed after history retirement. Fill
            // their private slots without resurrecting or growing the history.
            if (!Active || Capacity == 0) return new RectangleGeometry(bounds);
            var key = BarGeometryKey.For(bounds);
            if (_entries.TryGetValue(key, out var existing))
            {
                _lru.Remove(existing); _lru.AddLast(existing);
                return existing.Value.Geometry;
            }
            while (_entries.Count >= Capacity && _lru.First is { } oldest)
            {
                _lru.RemoveFirst(); _entries.Remove(oldest.Value.Key);
            }
            var geometry = new RectangleGeometry(bounds);
            var node = _lru.AddLast(new Entry(key, geometry));
            _entries.Add(key, node);
            return geometry;
        }

        internal void Deactivate()
        {
            Active = false;
            _entries.Clear(); _entries.TrimExcess(); _lru.Clear();
            Capacity = 0;
        }

        private sealed record Entry(BarGeometryKey Key, RectangleGeometry Geometry);
    }
}
