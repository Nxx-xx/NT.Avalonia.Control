using Avalonia;

namespace NT.AvaloniaControl.PdfViewer;

/// <summary>
/// Y-band buckets over one page's characters so pointer hit tests probe a few neighboring
/// bands instead of every character. Result parity with a full MinBy(Distance) scan is part
/// of the contract: the lowest list position wins among equally distant characters.
/// </summary>
internal sealed class PdfCharacterHitIndex
{
    /// <summary>Band height in unrotated page points; a character registers in every band it touches.</summary>
    private const double BandHeight = 12;

    private readonly PdfTextCharacter[] _characters;
    private readonly Dictionary<int, int[]> _bands = [];
    private readonly int _minBand;
    private readonly int _maxBand;

    internal PdfCharacterHitIndex(IReadOnlyList<PdfTextCharacter> characters)
    {
        _characters = characters as PdfTextCharacter[] ?? [.. characters];
        var lists = new Dictionary<int, List<int>>();
        int? min = null;
        int? max = null;
        for (var i = 0; i < _characters.Length; i++)
        {
            var bounds = _characters[i].Bounds;
            // Zero-size bounds can never win the nearest race; skip them like the linear scan.
            if (bounds.Width <= 0 || bounds.Height <= 0) continue;
            // Registering in every touched band keeps straddling characters findable from either side.
            var first = (int)Math.Floor(bounds.Top / BandHeight);
            var last = (int)Math.Floor(bounds.Bottom / BandHeight);
            for (var band = first; band <= last; band++)
            {
                if (!lists.TryGetValue(band, out var list)) lists[band] = list = [];
                list.Add(i);
            }
            min = min is null ? first : Math.Min(min.Value, first);
            max = max is null ? last : Math.Max(max.Value, last);
        }
        foreach (var (band, list) in lists) _bands[band] = list.ToArray();
        _minBand = min ?? 0;
        _maxBand = max ?? -1;
    }

    /// <summary>Text index of the character nearest the point in page space, or -1 when none qualifies.</summary>
    internal int FindNearest(Point point)
    {
        if (_bands.Count == 0) return -1;
        var band = (int)Math.Floor(point.Y / BandHeight);
        var bestIndex = -1;
        var bestDistance = double.PositiveInfinity;
        var maxOffset = Math.Max(band - _minBand, _maxBand - band) + 1;
        for (var offset = 0; offset <= maxOffset; offset++)
        {
            if (offset > 0)
            {
                // Unvisited characters live only in bands at least (offset - 1) bands away from
                // the point, so once that gap exceeds the best distance nothing can win or tie.
                var gap = (offset - 1) * BandHeight;
                if (gap * gap > bestDistance) break;
                Scan(band + offset, point, ref bestIndex, ref bestDistance);
                Scan(band - offset, point, ref bestIndex, ref bestDistance);
            }
            else Scan(band, point, ref bestIndex, ref bestDistance);
        }
        return bestIndex < 0 ? -1 : _characters[bestIndex].Index;
    }

    private void Scan(int band, Point point, ref int bestIndex, ref double bestDistance)
    {
        if (!_bands.TryGetValue(band, out var entries)) return;
        foreach (var index in entries)
        {
            var distance = Distance(_characters[index].Bounds, point);
            // Strictly nearer, or the same distance at a lower list position (MinBy parity).
            if (distance < bestDistance || (distance == bestDistance && index < bestIndex))
            {
                bestDistance = distance;
                bestIndex = index;
            }
        }
    }

    private static double Distance(Rect rect, Point point)
    {
        var x = Math.Max(0, Math.Max(rect.Left - point.X, point.X - rect.Right));
        var y = Math.Max(0, Math.Max(rect.Top - point.Y, point.Y - rect.Bottom));
        return x * x + y * y;
    }
}
