using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NT.AvaloniaControl.PdfViewer;

namespace PdfViewer.Benchmarks;

/// <summary>
/// One raw measurement record (schema 1.0): one corpus × one scenario × one iteration.
/// Field groups follow docs/pdfviewer-performance-plan.md section 9. Missing values are null
/// with a reason in <see cref="Notes"/>; nothing is filled with simulated numbers.
/// </summary>
internal sealed class PerfRecord
{
    public string SchemaVersion { get; set; } = "1.0";
    public string RunId { get; set; } = "";
    public int Iteration { get; set; }
    public string Implementation { get; set; } = "rt";
    public string? Commit { get; set; }
    public bool IsDirty { get; set; }
    public string? SourceManifestSha256 { get; set; }
    public string? ProductAssemblySha256 { get; set; }
    public string? NativeLibrarySha256 { get; set; }
    public string CorpusId { get; set; } = "";
    public string? CorpusSha256 { get; set; }
    public string ScenarioId { get; set; } = "";
    public string Status { get; set; } = "ok";
    public string? StatusDetail { get; set; }
    public string QualityStatus { get; set; } = "not-run-no-golden";
    public string ReferenceAvailability { get; set; } = "blocked-reference";
    public PerfEnvironment Environment { get; set; } = new();
    public PerfConditions Conditions { get; set; } = new();
    public Dictionary<string, double?> Metrics { get; set; } = new();
    public Dictionary<string, double[]> Samples { get; set; } = new();
    public PerfMeasurement Measurement { get; set; } = new();
    public PerfQuality Quality { get; set; } = new();
    public List<PerfEvidence> Evidence { get; set; } = [];
    public Dictionary<string, string?> Notes { get; set; } = new();
}

internal sealed class PerfEnvironment
{
    public string Os { get; set; } = "";
    public string? DesktopSession { get; set; }
    public string? GraphicsBackend { get; set; }
    public string? MachineProfileId { get; set; }
    public int CpuCount { get; set; }
    public long MemoryBytes { get; set; }
    public bool Wsl { get; set; }
    public Dictionary<string, string?> RuntimeVersions { get; set; } = new();
}

internal sealed class PerfConditions
{
    public string ProcessCache { get; set; } = "new-process";
    public string FileCache { get; set; } = "uncontrolled";
    public int ViewerCount { get; set; } = 1;
    public double[] ViewportDip { get; set; } = [1440, 900];
    public double RenderScale { get; set; } = 1;
    public int[]? TargetRasterPx { get; set; }
    public string? QualityProfileId { get; set; }
    public string? Warmup { get; set; }
}

internal sealed class PerfMeasurement
{
    public string Clock { get; set; } = "monotonic-stopwatch";
    public string? PresentMethod { get; set; }
    public string? PresentUnavailableReason { get; set; }
    public bool ScanOutVerified { get; set; }
}

internal sealed class PerfQuality
{
    public int[]? ActualRasterPx { get; set; }
    public string? DegradationReason { get; set; }
    public string SsimStatus { get; set; } = "not-run-no-golden";
    public List<string> RoiResults { get; set; } = [];
}

internal sealed class PerfEvidence
{
    public string Kind { get; set; } = "";
    public string? Path { get; set; }
    public string? Sha256 { get; set; }
    public string? Detail { get; set; }
}

internal sealed class CorpusSpec
{
    public string CorpusId { get; set; } = "";
    public string Tier { get; set; } = "";
    public string Fixture { get; set; } = "";
    public int Pages { get; set; }
    public string[] Labels { get; set; } = [];
    /// <summary>The query the search scenarios run against this corpus (has a deterministic hit).</summary>
    public string? SearchQuery { get; set; }
}

internal sealed class ScenarioSpec
{
    public string ScenarioId { get; set; } = "";
    public string[] Corpora { get; set; } = [];
    public bool Ui { get; set; }
    public string PrimaryMetric { get; set; } = "";
    public int Iterations { get; set; } = 30;
    public string? Warmup { get; set; }
}

internal static class PerfJson
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    internal static string Sha256Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    internal static string? HashFile(string path) =>
        File.Exists(path) ? Sha256Hex(File.ReadAllBytes(path)) : null;
}

/// <summary>Nearest-rank aggregation over raw records; quantiles are never interpolated.</summary>
internal static class PerfAggregate
{
    internal static double Quantile(double[] sortedAscending, double quantile)
    {
        if (sortedAscending.Length == 0) return double.NaN;
        var rank = (int)Math.Ceiling(quantile * sortedAscending.Length);
        return sortedAscending[Math.Clamp(rank, 1, sortedAscending.Length) - 1];
    }

    internal static Dictionary<string, object?> Build(IReadOnlyList<PerfRecord> records, string runId)
    {
        var groups = new List<Dictionary<string, object?>>();
        foreach (var group in records
                     .OrderBy(record => record.CorpusId, StringComparer.Ordinal)
                     .ThenBy(record => record.ScenarioId, StringComparer.Ordinal)
                     .GroupBy(record => (record.CorpusId, record.ScenarioId)))
        {
            var ordered = group.ToArray();
            var metrics = new Dictionary<string, object?>();
            foreach (var name in ordered.SelectMany(record => record.Metrics.Keys).Distinct().Order())
            {
                var values = ordered.Select(record => record.Metrics.GetValueOrDefault(name))
                    .Where(value => value is not null).Select(value => value!.Value).Order().ToArray();
                if (values.Length == 0) continue;
                metrics[name] = Stat(values);
            }
            var samples = new Dictionary<string, object?>();
            foreach (var name in ordered.SelectMany(record => record.Samples.Keys).Distinct().Order())
            {
                var values = ordered.SelectMany(record => record.Samples.GetValueOrDefault(name) ?? [])
                    .Order().ToArray();
                if (values.Length == 0) continue;
                samples[name] = Stat(values);
            }
            groups.Add(new Dictionary<string, object?>
            {
                ["corpusId"] = group.Key.CorpusId,
                ["scenarioId"] = group.Key.ScenarioId,
                ["iterations"] = ordered.Length,
                ["okCount"] = ordered.Count(record => record.Status == "ok"),
                ["errorCount"] = ordered.Count(record => record.Status == "error"),
                ["timeoutCount"] = ordered.Count(record => record.Status == "timeout"),
                ["canceledCount"] = ordered.Count(record => record.Status is "canceled" or "completed-before-cancel"),
                ["metrics"] = metrics,
                ["samples"] = samples,
                ["thresholds"] = Thresholds.Evaluate(group.Key.ScenarioId, metrics, samples),
                ["rawRecords"] = ordered.Select(record => $"{record.CorpusId}__{record.ScenarioId}__{record.Iteration}.json").ToArray(),
            });
        }
        return new Dictionary<string, object?>
        {
            ["schemaVersion"] = "1.0",
            ["runId"] = runId,
            ["generatedUtc"] = DateTimeOffset.UtcNow,
            ["referenceAvailability"] = "blocked-reference",
            ["notes"] = "自身性能结果。同机官方 A/B 对照缺合法参考宿主，全部对照项 blocked-reference；"
                + "quantiles are nearest-rank; p99 is reported only with >= 20 samples in the pooled set.",
            ["groups"] = groups,
        };
    }

    private static Dictionary<string, object?> Stat(double[] sorted)
    {
        return new Dictionary<string, object?>
        {
            ["count"] = sorted.Length,
            ["min"] = sorted[0],
            ["max"] = sorted[^1],
            ["mean"] = sorted.Average(),
            ["p50"] = Quantile(sorted, 0.50),
            ["p95"] = Quantile(sorted, 0.95),
            ["p99"] = sorted.Length >= 20 ? Quantile(sorted, 0.99) : null,
        };
    }
}

/// <summary>
/// The engineering initial thresholds from docs/pdfviewer-performance-plan.md section 6.
/// These are calibrated-target placeholders, not official promises; verdicts are informational
/// and the source table stays the authority.
/// </summary>
internal static class Thresholds
{
    private const string Source = "pdfviewer-performance-plan.md §6 工程初始门槛（待校准）";

    internal static object Evaluate(string scenarioId, Dictionary<string, object?> metrics,
        Dictionary<string, object?> samples)
    {
        var checks = new List<Dictionary<string, object?>>();
        void Check(string metric, string targetText, Func<double, bool> pass, string? scope = null, string quantile = "p95")
        {
            if (!(scope == "samples" ? samples : metrics).TryGetValue(metric, out var stat) ||
                stat is not Dictionary<string, object?> values || values[quantile] is not double value)
                return;
            checks.Add(new Dictionary<string, object?>
            {
                ["metric"] = metric,
                ["scope"] = scope ?? "per-iteration",
                ["quantile"] = quantile,
                ["target"] = targetText,
                ["value"] = value,
                ["verdict"] = pass(value) ? "met" : "not-met",
                ["source"] = Source,
            });
        }

        switch (scenarioId)
        {
            case "open-first-screen":
                Check("firstScreenMs", "p50 <= 600ms", p50 => p50 <= 600, quantile: "p50");
                Check("firstScreenMs", "p95 <= 1500ms", p95 => p95 <= 1500);
                break;
            case "jump-page-cached":
                Check("jumpMs", "p95 <= 100ms", p95 => p95 <= 100);
                break;
            case "jump-page-uncached":
                Check("jumpMs", "p95 <= 600ms", p95 => p95 <= 600);
                break;
            case "scroll-continuous":
                Check("frameIntervalMs", "p95 <= 20ms", p95 => p95 <= 20, "samples");
                Check("frameIntervalMs", "p99 <= 33.3ms", p99 => p99 <= 33.3, "samples", quantile: "p99");
                Check("settleMs", "p95 <= 500ms", p95 => p95 <= 500);
                break;
            case "search-first-hit":
                Check("searchFirstHitMs", "p95 <= 500ms", p95 => p95 <= 500);
                break;
            case "search-full-document":
                Check("searchTotalMs", "p95 <= 10000ms", p95 => p95 <= 10000);
                break;
            case "cancel-queued":
                Check("cancelObservedMs", "p95 <= 50ms", p95 => p95 <= 50);
                break;
            case "cancel-native-render":
                Check("cancelObservedMs", "p95 <= 250ms (1000ms 复杂页)", p95 => p95 <= 250);
                break;
            case "close-release":
                Check("closeUiMs", "p95 <= 100ms", p95 => p95 <= 100);
                break;
            case "open-close-cycles":
                // The 100-cycle gate is a delta gate, evaluated once per group from the cycle metrics.
                if (metrics.TryGetValue("privateBytesDeltaBytes", out var delta) && delta is Dictionary<string, object?> d
                    && metrics.TryGetValue("privateBytesFirst", out var first) && first is Dictionary<string, object?> f
                    && f["max"] is double start && start > 0 && d["max"] is double growth)
                {
                    var limit = Math.Max(20 * 1024 * 1024, start * 0.05);
                    checks.Add(new Dictionary<string, object?>
                    {
                        ["metric"] = "privateBytesDeltaBytes",
                        ["scope"] = "per-iteration",
                        ["quantile"] = "delta",
                        ["target"] = "<= max(20MiB, 5% of first sample)",
                        ["value"] = growth,
                        ["verdict"] = growth <= limit ? "met" : "not-met",
                        ["source"] = Source,
                    });
                }
                break;
        }
        return checks;
    }
}
