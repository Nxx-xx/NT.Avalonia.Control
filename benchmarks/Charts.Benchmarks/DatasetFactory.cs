using System.Buffers.Binary;
using System.Security.Cryptography;
using NT.AvaloniaControl.Charts;

namespace Charts.Benchmarks;

internal sealed record DatasetDefinition(string Id, string Kind, int TotalPoints, int SeriesCount,
    ulong Seed, bool NonMonotonic = false, bool Gaps = false, bool RealtimePoc = false);

internal sealed record DatasetManifest(string Id, string GeneratorVersion, string Distribution,
    string SeedHex, string Sha256, int InputPointCount, int ValidPointCount, int[] SeriesPointCounts,
    string[] SeriesSha256, bool MonotonicX, bool ContainsGaps, bool RealtimePoc, int Revision,
    double MarkerSize, string Downsampling, string[] PendingScope);

internal sealed record BenchmarkDataset(DatasetDefinition Definition, ChartPoint[][] Points, DatasetManifest Manifest);

internal static class DatasetFactory
{
    public const string GeneratorVersion = "charts-c0-fixed-integer-xorshift64star-v1";
    public static readonly IReadOnlyList<DatasetDefinition> Definitions =
    [
        new("S10-L", "line", 10_000, 1, 0x1020304050607081),
        new("S10-S", "scatter", 10_000, 1, 0x1020304050607082),
        new("S10-B", "bar", 10_000, 2, 0x1020304050607083),
        new("S100-L", "line", 100_000, 1, 0x1020304050607084),
        new("S1M-L", "line", 1_000_000, 1, 0x1020304050607085),
        new("S10-L-NONMONOTONIC", "line", 10_000, 1, 0x1020304050607086, NonMonotonic: true),
        new("S10-L-GAP", "line", 10_000, 1, 0x1020304050607087, Gaps: true),
        new("S-RT-POC", "line", 1_000, 4, 0x1020304050607088, RealtimePoc: true),
    ];

    // Integer arithmetic and powers-of-two scaling keep generation independent of Random/Math.Sin implementations.
    // These are C0 candidate fixtures; they do not claim the complete C4 corpus or realtime rate qualification.
    public static BenchmarkDataset Create(DatasetDefinition definition, int revision = 0)
    {
        var perSeries = definition.TotalPoints / definition.SeriesCount + (definition.RealtimePoc ? revision * 25 : 0);
        var arrays = new ChartPoint[definition.SeriesCount][];
        for (var seriesIndex = 0; seriesIndex < arrays.Length; seriesIndex++)
        {
            var state = definition.Seed + (ulong)seriesIndex * 37;
            var points = arrays[seriesIndex] = new ChartPoint[perSeries];
            for (var index = 0; index < points.Length; index++)
            {
                var random = Next(ref state);
                var wave = index % 1024;
                var triangle = wave < 512 ? wave : 1024 - wave;
                var y = (triangle * 32 + (int)(random & 511) - 255) / 256.0 + seriesIndex * 3;
                if (index % 997 == 431) y += 128;
                var x = definition.NonMonotonic ? (index % 2 == 0 ? index / 2 : points.Length - 1 - index / 2) : index;
                if (definition.Kind == "scatter") y = (random >> 20 & 0xFFFFF) / 1024.0;
                if (definition.Kind == "bar" && index % 3 == 0) y = -y - 8;
                if (definition.Gaps && index % 257 == 113) y = double.NaN;
                points[index] = new ChartPoint(x, y);
            }
        }
        var hashes = arrays.Select(HashPoints).ToArray();
        var combined = string.Join('\n', new[] { GeneratorVersion, definition.Id, definition.Seed.ToString("X16"), revision.ToString(System.Globalization.CultureInfo.InvariantCulture) }.Concat(hashes));
        var manifest = new DatasetManifest(definition.Id, GeneratorVersion,
            definition.Kind == "scatter" ? "uniform-fixed-grid-candidate" : "triangle-noise-isolated-spikes-candidate",
            definition.Seed.ToString("X16"), Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(combined))),
            arrays.Sum(points => points.Length), arrays.Sum(points => points.Count(IsValid)), arrays.Select(points => points.Length).ToArray(),
            hashes, !definition.NonMonotonic, definition.Gaps, definition.RealtimePoc, revision,
            definition.Kind == "scatter" ? 4 : 0, "none-current-runtime", definition.RealtimePoc
                ? ["paced-10000-points-per-second", "INCC-streaming-cost", "ring-buffer", "30-minute-soak", "8-hour-soak"]
                : ["complete-C4-corpus", "quality-golden", "physical-input", "native-frame-present", "official-reference"]);
        return new BenchmarkDataset(definition, arrays, manifest);
    }

    public static string HashPoints(IEnumerable<ChartPoint> points)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> bytes = stackalloc byte[16];
        foreach (var point in points)
        {
            BinaryPrimitives.WriteInt64LittleEndian(bytes, BitConverter.DoubleToInt64Bits(point.X));
            BinaryPrimitives.WriteInt64LittleEndian(bytes[8..], BitConverter.DoubleToInt64Bits(point.Y));
            hash.AppendData(bytes);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    public static string Bits(double value) => BitConverter.DoubleToInt64Bits(value).ToString("X16");
    public static bool IsValid(ChartPoint point) => double.IsFinite(point.X) && double.IsFinite(point.Y);
    private static ulong Next(ref ulong state)
    {
        state ^= state >> 12;
        state ^= state << 25;
        state ^= state >> 27;
        return state * 2685821657736338717UL;
    }
}
