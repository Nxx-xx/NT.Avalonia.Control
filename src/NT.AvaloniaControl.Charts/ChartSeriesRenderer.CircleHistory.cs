using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

internal sealed partial class CartesianSeriesRenderer
{
    private const int MaximumCircleHistoryEntries = MaximumCachedCircles * 2;
    private static readonly ConditionalWeakTable<ChartSeries, CircleGeometryHistory> CircleHistories = new();

    // Full resource retirement calls this; ordinary viewport/source scene
    // invalidation deliberately keeps the bounded, paint-independent geometry.
    internal static void ReleaseCircleGeometryHistory(ChartSeries series)
    {
        if (!CircleHistories.TryGetValue(series, out var history)) return;
        CircleHistories.Remove(series);
        history.Deactivate();
    }

    private static EllipseGeometry CreateRecordingCircle(Point center, double radius) =>
        new(new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2));

    private readonly record struct CircleGeometryKey(long X, long Y, long Radius)
    {
        internal static CircleGeometryKey For(Point center, double radius) => new(
            BitConverter.DoubleToInt64Bits(center.X), BitConverter.DoubleToInt64Bits(center.Y), BitConverter.DoubleToInt64Bits(radius));
    }

    // Accessed by the owning chart on the UI thread. Keys retain exact input
    // doubles; geometry is never transformed or edited after construction.
    private sealed class CircleGeometryHistory
    {
        private readonly Dictionary<CircleGeometryKey, LinkedListNode<Entry>> _entries = [];
        private readonly LinkedList<Entry> _lru = [];
        internal bool Active { get; private set; } = true;
        internal int Capacity { get; private set; }
        internal int Count => _entries.Count;

        internal void Reserve(int currentCount)
        {
            if (!Active) return;
            var capacity = Math.Min(MaximumCircleHistoryEntries, currentCount * 2);
            if (capacity <= Capacity) return;
            // Capacity and the logical payload are declared in the retained
            // drawing constructor, before resource-cache admission or replay.
            _entries.EnsureCapacity(capacity);
            Capacity = capacity;
        }

        internal EllipseGeometry GetOrCreate(Point center, double radius)
        {
            // An older retained drawing may outlive Trim/detach or a rejected
            // cache admission. It can still fill its local array, never history.
            if (!Active || Capacity == 0) return CreateRecordingCircle(center, radius);
            var key = CircleGeometryKey.For(center, radius);
            if (_entries.TryGetValue(key, out var existing))
            {
                _lru.Remove(existing); _lru.AddLast(existing);
                return existing.Value.Geometry;
            }
            while (_entries.Count >= Capacity && _lru.First is { } oldest)
            {
                _lru.RemoveFirst(); _entries.Remove(oldest.Value.Key);
            }
            var geometry = CreateRecordingCircle(center, radius);
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

        private sealed record Entry(CircleGeometryKey Key, EllipseGeometry Geometry);
    }
}
