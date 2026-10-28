using Avalonia;
using NT.AvaloniaControl.PdfViewer;

namespace PdfViewer.Core.Tests;

/// <summary>
/// P3-04 character hit contracts: the band index must agree with the old full MinBy(Distance)
/// scan for every query, including equal-distance ties (lowest list position wins), straddling
/// bounds, zero-sized bounds, overlapping characters and sparse far text.
/// </summary>
public sealed class P3CharacterHitIndexTests
{
    [Fact]
    public void Empty_and_zero_sized_pages_have_no_hit()
    {
        Assert.Equal(-1, new PdfCharacterHitIndex([]).FindNearest(new Point(10, 10)));
        var onlyTiny = new[]
        {
            new PdfTextCharacter(0, "x", new Rect(50, 50, 0, 10)),
            new PdfTextCharacter(1, "y", new Rect(60, 60, 10, 0)),
        };
        var index = new PdfCharacterHitIndex(onlyTiny);
        Assert.Equal(-1, index.FindNearest(new Point(55, 55)));
        // The zero-sized bounds can never steal a hit from a real character.
        var mixed = new[]
        {
            new PdfTextCharacter(0, "x", new Rect(50, 50, 0, 10)),
            new PdfTextCharacter(1, "y", new Rect(60, 60, 10, 0)),
            new PdfTextCharacter(2, "z", new Rect(100, 100, 10, 10)),
        };
        Assert.Equal(2, new PdfCharacterHitIndex(mixed).FindNearest(new Point(55, 55)));
    }

    [Fact]
    public void Equal_distance_ties_prefer_the_lowest_list_position()
    {
        // Two characters the same distance from the query; the later list entry carries the
        // lower text index, which must not win the tie (MinBy keeps list order).
        var characters = new[]
        {
            new PdfTextCharacter(10, "a", new Rect(0, 0, 10, 10)),
            new PdfTextCharacter(3, "b", new Rect(30, 0, 10, 10)),
        };
        var index = new PdfCharacterHitIndex(characters);
        Assert.Equal(10, index.FindNearest(new Point(20, 5)));
        // The brute scan the index replaced agrees.
        Assert.Equal(10, BruteNearest(characters, new Point(20, 5)));
    }

    [Fact]
    public void Overlapping_characters_prefer_the_lowest_list_position()
    {
        var characters = new[]
        {
            new PdfTextCharacter(7, "a", new Rect(0, 0, 20, 20)),
            new PdfTextCharacter(3, "b", new Rect(5, 5, 20, 20)),
        };
        var index = new PdfCharacterHitIndex(characters);
        foreach (var point in new[] { new Point(10, 10), new Point(6, 6), new Point(22, 22) })
        {
            Assert.Equal(BruteNearest(characters, point), index.FindNearest(point));
        }
    }

    [Fact]
    public void Band_straddling_bounds_stay_findable_from_both_sides()
    {
        // 12-point bands: this character covers bands 0, 1 and 2 and must win from far above
        // and far below even though its registered bands are walked late.
        var characters = new[]
        {
            new PdfTextCharacter(0, "tall", new Rect(50, 2, 10, 30)),
            new PdfTextCharacter(1, "up", new Rect(50, -18, 10, 10)),
            new PdfTextCharacter(2, "down", new Rect(50, 62, 10, 10)),
        };
        var index = new PdfCharacterHitIndex(characters);
        foreach (var point in new[] { new Point(55, 30), new Point(55, 0), new Point(55, 20), new Point(55, 33), new Point(55, -15) })
            Assert.Equal(BruteNearest(characters, point), index.FindNearest(point));
    }

    [Fact]
    public void Sparse_far_text_still_resolves()
    {
        var characters = new[] { new PdfTextCharacter(0, "x", new Rect(1000, 1234, 12, 10)) };
        var index = new PdfCharacterHitIndex(characters);
        Assert.Equal(0, index.FindNearest(new Point(0, 0)));
        Assert.Equal(0, index.FindNearest(new Point(2000, 2500)));
    }

    [Fact]
    public void FindNearest_matches_the_full_scan_over_a_dense_cloud()
    {
        var random = new Random(20261023);
        var characters = Enumerable.Range(0, 400).Select(i => new PdfTextCharacter(i, "x",
            new Rect(random.NextDouble() * 300, random.NextDouble() * 400,
                4 + random.NextDouble() * 20, 6 + random.NextDouble() * 8))).ToArray();
        var index = new PdfCharacterHitIndex(characters);
        for (var probe = 0; probe < 2000; probe++)
        {
            // Mix uniform probes with band-boundary and corner probes where early-out matters.
            var point = (probe % 4) switch
            {
                0 => new Point(random.NextDouble() * 320 - 10, random.NextDouble() * 420 - 10),
                1 => new Point(random.NextDouble() * 320 - 10, (probe / 4 % 40) * 12),
                2 => new Point(0, 0),
                _ => new Point(310, 410),
            };
            var expected = BruteNearest(characters, point);
            var actual = index.FindNearest(point);
            Assert.Equal(expected, actual);
        }
    }

    /// <summary>The exact scan the index replaced: first minimum over list order wins.</summary>
    private static int BruteNearest(IReadOnlyList<PdfTextCharacter> characters, Point point)
    {
        var best = characters.Where(c => c.Bounds.Width > 0 && c.Bounds.Height > 0)
            .MinBy(c => Distance(c.Bounds, point));
        return best?.Index ?? -1;
    }

    private static double Distance(Rect rect, Point point)
    {
        var x = Math.Max(0, Math.Max(rect.Left - point.X, point.X - rect.Right));
        var y = Math.Max(0, Math.Max(rect.Top - point.Y, point.Y - rect.Bottom));
        return x * x + y * y;
    }
}
