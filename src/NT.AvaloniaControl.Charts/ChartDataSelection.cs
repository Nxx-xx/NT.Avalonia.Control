using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>Original observation and its native identity domain in a professional chart selection.</summary>
internal readonly record struct ChartDataSelectionMember(ChartDataPoint Data, object Key, bool ExplicitId);
internal readonly record struct ChartDataSelectionGlyph(IEnumerable<ChartDataSelectionMember> Members, Geometry Geometry);

internal static class ChartDataSelection
{
    internal static bool ValidRange(Rect? range) => range is null || range is { } value &&
        double.IsFinite(value.X) && double.IsFinite(value.Y) && double.IsFinite(value.Width) && double.IsFinite(value.Height) &&
        value.X >= 0 && value.Y >= 0 && value.Width > 0 && value.Height > 0 && value.Right <= 1 && value.Bottom <= 1;
    internal static Rect Project(Rect range, Rect plot) => new(plot.Left + range.X * plot.Width,
        plot.Top + range.Y * plot.Height, range.Width * plot.Width, range.Height * plot.Height);
    internal static bool Drawable(Rect bounds) => double.IsFinite(bounds.X) && double.IsFinite(bounds.Y) &&
        double.IsFinite(bounds.Width) && double.IsFinite(bounds.Height) && bounds.Width >= 0 && bounds.Height >= 0 &&
        (bounds.Width > 0 || bounds.Height > 0);
    internal static bool Visible(Rect bounds, Rect plot) => Drawable(bounds) && plot.Width > 0 && plot.Height > 0 && SelectionBounds(bounds).Intersects(plot);
    private static Rect SelectionBounds(Rect bounds) => bounds.Width == 0 || bounds.Height == 0 ? bounds.Inflate(.5) : bounds;
    internal static bool Intersects(Geometry geometry, Rect rectangle, Rect plot)
    {
        if (plot.Width <= 0 || plot.Height <= 0 || !rectangle.Intersects(plot)) return false;
        return SelectionBounds(geometry.Bounds).Intersects(rectangle.Intersect(plot));
    }
    internal sealed class MatchSet(IReadOnlyList<object> keys)
    {
        private readonly HashSet<object> _valueKeys = new(keys);
        private readonly HashSet<object> _referenceKeys = new(keys, ReferenceEqualityComparer.Instance);
        internal bool Contains(ChartDataSelectionMember member) => member.Key is MapGlyphKey or RelationGlyphKey || member.ExplicitId
            ? _valueKeys.Contains(member.Key) : _referenceKeys.Contains(member.Key);
    }
    internal static bool Matches(object requested, ChartDataSelectionMember member) => member.Key switch
    {
        MapGlyphKey map => requested is MapGlyphKey other && map.Equals(other),
        RelationGlyphKey relation => requested is RelationGlyphKey other && relation.Equals(other),
        _ => member.ExplicitId ? Equals(requested, member.Key) : ReferenceEquals(requested, member.Key),
    };
    internal static ChartDataSelectionMember Member(ChartDataPoint point, object? markKey)
    {
        var identity = point.StableId ?? point.Item ?? point.SourceIndex;
        object key = markKey switch
        {
            MapGlyphKey map => new MapGlyphKey(map.Layer, map.Kind, identity,
                map.Kind == MapElementKind.HeatmapCell ? !ReferenceEquals(identity, point.Item) : map.HasExplicitId),
            RelationGlyphKey relation => new RelationGlyphKey(relation.Kind, identity, relation.HasExplicitId),
            _ => identity,
        };
        // Hierarchies can mix explicit IDs and original-reference fallback within one source.
        var explicitId = key switch
        {
            MapGlyphKey map => map.HasExplicitId,
            RelationGlyphKey relation => relation.HasExplicitId,
            _ => point.StableId is not null && !ReferenceEquals(point.StableId, point.Item) || point.StableId is null && point.Item is null,
        };
        return new(point, key, explicitId);
    }
    internal static bool Same(IReadOnlyList<ChartDataPoint> first, IReadOnlyList<ChartDataPoint> second)
    {
        if (first.Count != second.Count) return false;
        for (var index = 0; index < first.Count; index++)
        {
            var a = first[index]; var b = second[index];
            if (!ReferenceEquals(a.Item, b.Item) || a.SourceIndex != b.SourceIndex || !a.Equals(b)) return false;
        }
        return true;
    }
    internal sealed class NativeIdentityComparer : IEqualityComparer<ChartDataSelectionMember>
    {
        internal static readonly NativeIdentityComparer Instance = new();
        public bool Equals(ChartDataSelectionMember x, ChartDataSelectionMember y) =>
            x.ExplicitId == y.ExplicitId && Matches(x.Key, y);
        public int GetHashCode(ChartDataSelectionMember member) => member.Key is MapGlyphKey or RelationGlyphKey || member.ExplicitId
            ? member.Key.GetHashCode() : RuntimeHelpers.GetHashCode(member.Key);
    }
    internal sealed class IdentityComparer : IEqualityComparer<ChartDataSelectionMember>
    {
        internal static readonly IdentityComparer Instance = new();
        public bool Equals(ChartDataSelectionMember x, ChartDataSelectionMember y) => x.Data.SourceIndex == y.Data.SourceIndex &&
            ReferenceEquals(x.Data.Item, y.Data.Item) && NativeIdentityComparer.Instance.Equals(x, y);
        public int GetHashCode(ChartDataSelectionMember member) => HashCode.Combine(NativeIdentityComparer.Instance.GetHashCode(member),
            member.Data.SourceIndex, member.Data.Item is null ? 0 : RuntimeHelpers.GetHashCode(member.Data.Item));
    }
}
