using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using NT.AvaloniaControl.Charts;

namespace Charts.Benchmarks;

// Measurements are observations. The separately pinned Python oracle reconstructs
// the input and financial algorithms without loading this executable or Charts.
internal static class FinancialRegression
{
    private static readonly DateTimeOffset Epoch = new(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);

    public static int Run(string[] args)
    {
        var at = Array.IndexOf(args, "--output");
        var output = Path.GetFullPath(at >= 0 && at + 1 < args.Length ? args[at + 1] : "artifacts/charts-financial-prices/raw.json");
        if (File.Exists(output)) { Console.Error.WriteLine("Choose a new evidence path."); return 1; }
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var report = new Report();
        try
        {
            var canonical = new ChartFinancialPoint[] { P(0, 10, 14, 8, 12, 100), P(1, 12, 15, 9, 10, 120), P(2, 10, 13, 9, 10, null), P(3, 10, 16, 9, 15, 80) };
            var waves = Closes(10, 13, 12, 10, 7, 9, 12, 15, 11, 8, 12);
            var gap = new ChartFinancialPoint[] { P(0, 10, 14, 8, 12, 100), P(1, 12, 15, 9, 10, 120), P(2, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, true), P(3, 20, 24, 18, 22, 80), P(4, 22, 25, 19, 20, 90) };
            Add(report, "validate-canonical", "validate", canonical, new { }, () => ChartFinancialTransforms.Validate(canonical).Select(PointRecord).ToArray());
            Add(report, "validate-empty", "validate", [], new { }, () => ChartFinancialTransforms.Validate([]).Select(PointRecord).ToArray());
            Add(report, "raw-canonical", "raw", canonical, new { maxGenerated = 100_000 }, () => Full(ChartFinancialTransforms.Raw(canonical)));
            var negative = new[] { P(0, -5, -1, -8, -3, 0) };
            Add(report, "raw-negative-price", "raw", negative, new { maxGenerated = 100_000 }, () => Full(ChartFinancialTransforms.Raw(negative)));
            Add(report, "raw-gap", "raw", gap, new { maxGenerated = 100_000 }, () => Full(ChartFinancialTransforms.Raw(gap)));
            Add(report, "ha-canonical", "ha", canonical, new { maxGenerated = 100_000 }, () => Full(ChartFinancialTransforms.HeikinAshi(canonical)));
            Add(report, "ha-gap-reset", "ha", gap, new { maxGenerated = 100_000 }, () => Full(ChartFinancialTransforms.HeikinAshi(gap)));
            Add(report, "ha-empty", "ha", [], new { maxGenerated = 100_000 }, () => Full(ChartFinancialTransforms.HeikinAshi([])));
            foreach (var reversal in new[] { 1, 2, 3 })
                Add(report, "renko-waves-r" + reversal, "renko", waves, new { boxSize = 1d, reversalBoxes = reversal, maxGenerated = 100_000 }, () => Full(ChartFinancialTransforms.Renko(waves, 1, reversal)));
            var fractional = Closes(-2, -1.5, -1.25, -2.5, -2, -.5);
            Add(report, "renko-fractional-negative", "renko", fractional, new { boxSize = .5, reversalBoxes = 2, maxGenerated = 100_000 }, () => Full(ChartFinancialTransforms.Renko(fractional, .5)));
            var threshold = Closes(10, 10.5, 11, 10, 9, 10, 11);
            Add(report, "renko-exact-threshold", "renko", threshold, new { boxSize = 1d, reversalBoxes = 2, maxGenerated = 100_000 }, () => Full(ChartFinancialTransforms.Renko(threshold, 1)));
            var derivedGap = new[] { P(0, 10, 10, 10, 10, 10), P(1, 13, 13, 13, 13, 20), P(2, double.NaN, double.NaN, double.NaN, double.NaN, null, true), P(3, 30, 30, 30, 30, 30), P(4, 33, 33, 33, 33, 40) };
            Add(report, "renko-gap-reset", "renko", derivedGap, new { boxSize = 1d, reversalBoxes = 2, maxGenerated = 100_000 }, () => Full(ChartFinancialTransforms.Renko(derivedGap, 1)));
            foreach (var reversal in new[] { 1, 3 })
                Add(report, "pf-waves-r" + reversal, "pf", waves, new { boxSize = 1d, reversalBoxes = reversal, maxGenerated = 100_000 }, () => Full(ChartFinancialTransforms.PointAndFigure(waves, 1, reversal)));
            Add(report, "pf-gap-reset", "pf", derivedGap, new { boxSize = 1d, reversalBoxes = 3, maxGenerated = 100_000 }, () => Full(ChartFinancialTransforms.PointAndFigure(derivedGap, 1)));
            Add(report, "pf-flat", "pf", Closes(10, 10, 10), new { boxSize = 1d, reversalBoxes = 3, maxGenerated = 100_000 }, () => Full(ChartFinancialTransforms.PointAndFigure(Closes(10, 10, 10), 1)));
            Add(report, "kagi-waves", "kagi", waves, new { reversalAmount = 2d, maxGenerated = 100_000 }, () => Full(ChartFinancialTransforms.Kagi(waves, 2)));
            Add(report, "kagi-gap-reset", "kagi", derivedGap, new { reversalAmount = 2d, maxGenerated = 100_000 }, () => Full(ChartFinancialTransforms.Kagi(derivedGap, 2)));
            Add(report, "kagi-exact-threshold", "kagi", threshold, new { reversalAmount = 1d, maxGenerated = 100_000 }, () => Full(ChartFinancialTransforms.Kagi(threshold, 1)));
            Add(report, "invalid-low", "invalid", [P(0, 10, 14, 11, 12)], new { operation = "raw", expectedException = "ArgumentException" }, () => Full(ChartFinancialTransforms.Raw([P(0, 10, 14, 11, 12)])));
            Add(report, "invalid-high", "invalid", [P(0, 10, 11, 8, 12)], new { operation = "raw", expectedException = "ArgumentException" }, () => Full(ChartFinancialTransforms.Raw([P(0, 10, 11, 8, 12)])));
            Add(report, "invalid-nonfinite", "invalid", [P(0, double.NaN, 14, 8, 12)], new { operation = "validate", expectedException = "ArgumentException" }, () => ChartFinancialTransforms.Validate([P(0, double.NaN, 14, 8, 12)]));
            Add(report, "invalid-volume", "invalid", [P(0, 10, 14, 8, 12, -1)], new { operation = "validate", expectedException = "ArgumentException" }, () => ChartFinancialTransforms.Validate([P(0, 10, 14, 8, 12, -1)]));
            var duplicate = new[] { P(0, 10, 10, 10, 10), P(0, 11, 11, 11, 11) };
            Add(report, "invalid-duplicate-time", "invalid", duplicate, new { operation = "validate", expectedException = "ArgumentException" }, () => ChartFinancialTransforms.Validate(duplicate));
            var unordered = new[] { P(1, 10, 10, 10, 10), P(0, 11, 11, 11, 11) };
            Add(report, "invalid-unordered-time", "invalid", unordered, new { operation = "validate", expectedException = "ArgumentException" }, () => ChartFinancialTransforms.Validate(unordered));
            var zoned = new[] { new ChartFinancialPoint(Epoch.ToOffset(TimeSpan.FromHours(8)), 10, 10, 10, 10) };
            Add(report, "validate-zoned-time", "validate", zoned, new { }, () => ChartFinancialTransforms.Validate(zoned).Select(PointRecord).ToArray());
            Add(report, "invalid-box-size", "invalid", waves, new { operation = "renko", expectedException = "ArgumentOutOfRangeException" }, () => Full(ChartFinancialTransforms.Renko(waves, 0)));
            Add(report, "invalid-reversal-boxes", "invalid", waves, new { operation = "pf", expectedException = "ArgumentOutOfRangeException" }, () => Full(ChartFinancialTransforms.PointAndFigure(waves, 1, 0)));
            Add(report, "invalid-reversal-amount", "invalid", waves, new { operation = "kagi", expectedException = "ArgumentOutOfRangeException" }, () => Full(ChartFinancialTransforms.Kagi(waves, 0)));
            Add(report, "invalid-max-generated", "invalid", waves, new { operation = "raw", expectedException = "ArgumentOutOfRangeException" }, () => Full(ChartFinancialTransforms.Raw(waves, 0)));
            Add(report, "invalid-max-generated-upper", "invalid", waves, new { operation = "raw", expectedException = "ArgumentOutOfRangeException" }, () => Full(ChartFinancialTransforms.Raw(waves, 1_000_001)));
            Add(report, "invalid-raw-resource-budget", "invalid", waves, new { operation = "raw", maxGenerated = 2, expectedException = "InvalidOperationException" }, () => Full(ChartFinancialTransforms.Raw(waves, 2)));
            Add(report, "invalid-renko-resource-budget", "invalid", Closes(0, 100), new { operation = "renko", maxGenerated = 5, expectedException = "InvalidOperationException" }, () => Full(ChartFinancialTransforms.Renko(Closes(0, 100), 1, 2, 5)));
            Add(report, "invalid-pf-resource-budget", "invalid", Closes(0, 100), new { operation = "pf", maxGenerated = 5, expectedException = "InvalidOperationException" }, () => Full(ChartFinancialTransforms.PointAndFigure(Closes(0, 100), 1, 3, 5)));
            Add(report, "invalid-kagi-resource-budget", "invalid", waves, new { operation = "kagi", maxGenerated = 1, expectedException = "InvalidOperationException" }, () => Full(ChartFinancialTransforms.Kagi(waves, 2, 1)));
            Add(report, "invalid-input-resource-budget", "invalid", [], new { operation = "validate", inputCount = 1_000_001, inputFormula = "OHLC=1;UTC=2026-01-02+i minutes;i=0..1000000", expectedException = "ArgumentOutOfRangeException" }, () => ChartFinancialTransforms.Validate(Enumerable.Range(0, 1_000_001).Select(i => new ChartFinancialPoint(Epoch.AddMinutes(i), 1, 1, 1, 1))));
            Add(report, "invalid-price-difference-overflow", "invalid", Closes(-double.MaxValue, double.MaxValue), new { operation = "renko", expectedException = "ArithmeticException" }, () => Full(ChartFinancialTransforms.Renko(Closes(-double.MaxValue, double.MaxValue), 1)));
            Add(report, "invalid-price-no-progress", "invalid", Closes(1e20, 1e20 + 32768), new { operation = "renko", expectedException = "ArithmeticException" }, () => Full(ChartFinancialTransforms.Renko(Closes(1e20, 1e20 + 32768), 1)));
            var cancelled = new CancellationToken(true);
            Add(report, "invalid-cancelled", "invalid", canonical, new { operation = "ha", expectedException = "OperationCanceledException" }, () => Full(ChartFinancialTransforms.HeikinAshi(canonical, cancellationToken: cancelled)));
            var large = Enumerable.Range(0, 100_000).Select(i => { var o = 100 + ((i * 37) % 257) * .125; var c = o + ((i % 5) - 2) * .25; return P(i, o, Math.Max(o, c) + .5, Math.Min(o, c) - .5, c, i % 1000); }).ToArray();
            const string formula = "O=100+((i*37)%257)*0.125;C=O+(i%5-2)*0.25;H=max(O,C)+0.5;L=min(O,C)-0.5;V=i%1000;UTC=2026-01-02+i days;i=0..99999";
            AddPerformance(report, "perf-validate-100k", "validate-summary", large, formula, new { }, () => ChartFinancialTransforms.Validate(large), PointSummary);
            AddPerformance(report, "perf-ha-100k", "ha-summary", large, formula, new { maxGenerated = 100_000 }, () => ChartFinancialTransforms.HeikinAshi(large), SampleSummary);
            var medium = Enumerable.Range(0, 2048).Select(i => { var c = 100 + ((i * 13) % 41) * .25; return P(i, c, c, c, c, i % 100); }).ToArray();
            const string mediumFormula = "O=H=L=C=100+((i*13)%41)*0.25;V=i%100;UTC=2026-01-02+i days;i=0..2047";
            AddPerformance(report, "perf-renko-2048", "renko", medium, mediumFormula, new { boxSize = 1d, reversalBoxes = 2, maxGenerated = 100_000 }, () => ChartFinancialTransforms.Renko(medium, 1), Full);
            AddPerformance(report, "perf-pf-2048", "pf", medium, mediumFormula, new { boxSize = 1d, reversalBoxes = 3, maxGenerated = 100_000 }, () => ChartFinancialTransforms.PointAndFigure(medium, 1), Full);
            AddPerformance(report, "perf-kagi-2048", "kagi", medium, mediumFormula, new { reversalAmount = 2d, maxGenerated = 100_000 }, () => ChartFinancialTransforms.Kagi(medium, 2), Full);
            report.Passed = report.Cases.Count == 41 && report.PerformanceCases.Count == 5 && report.Cases.All(item => item.Kind == "invalid" ? item.ExceptionType is not null : item.ExceptionType is null) && report.PerformanceCases.All(item => item.ExceptionType is null);
        }
        catch (Exception exception) { report.Error = exception.ToString(); Console.Error.WriteLine(exception); }
        report.FinishedUtc = DateTimeOffset.UtcNow;
        using var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        JsonSerializer.Serialize(stream, report, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        return report.Passed ? 0 : 1;
    }

    private static ChartFinancialPoint P(int day, double open, double high, double low, double close, double? volume = null, bool gap = false) => new(Epoch.AddDays(day), open, high, low, close, volume, gap);
    private static ChartFinancialPoint[] Closes(params double[] values) => values.Select((value, i) => P(i, value, value, value, value, i + 1)).ToArray();
    private static object Encode(double value) => double.IsNaN(value) ? "NaN" : double.IsPositiveInfinity(value) ? "+Infinity" : double.IsNegativeInfinity(value) ? "-Infinity" : value;
    private static object PointRecord(ChartFinancialPoint point) => new { timeUtcTicks = point.Time.UtcTicks, offsetMinutes = point.Time.Offset.TotalMinutes, open = Encode(point.Open), high = Encode(point.High), low = Encode(point.Low), close = Encode(point.Close), volume = point.Volume is { } value ? Encode(value) : null, isGap = point.IsGap };
    private static object SampleRecord(ChartFinancialSample sample) => new { sample.Slot, timeUtcTicks = sample.Time.UtcTicks, open = Encode(sample.Open), high = Encode(sample.High), low = Encode(sample.Low), close = Encode(sample.Close), sample.Volume, sample.SourceStartIndex, sample.SourceEndIndex, sourceStart = PointRecord(sample.SourceStart), sourceEnd = PointRecord(sample.SourceEnd), sources = sample.Sources.Select(PointRecord).ToArray(), kind = sample.Kind.ToString(), sample.Direction, sample.IsThick, sample.IsSegmentStart, sample.BoxSize, sample.BoxCount };
    private static object[] Full(IReadOnlyList<ChartFinancialSample> samples) => samples.Select(SampleRecord).ToArray();
    private static void WritePoint(BinaryWriter writer, ChartFinancialPoint point)
    {
        writer.Write(point.Time.UtcTicks); writer.Write(point.Time.Offset.Ticks); writer.Write(point.Open); writer.Write(point.High); writer.Write(point.Low); writer.Write(point.Close); writer.Write(point.Volume.HasValue); writer.Write(point.Volume ?? 0); writer.Write(point.IsGap);
    }
    private static string PointSha(IReadOnlyList<ChartFinancialPoint> points)
    {
        using var bytes = new MemoryStream(); using var writer = new BinaryWriter(bytes);
        foreach (var point in points) WritePoint(writer, point);
        return Convert.ToHexString(SHA256.HashData(bytes.GetBuffer().AsSpan(0, checked((int)bytes.Length))));
    }
    private static object PointSummary(IReadOnlyList<ChartFinancialPoint> points) => new { count = points.Count, first = points.Take(4).Select(PointRecord).ToArray(), last = points.TakeLast(4).Select(PointRecord).ToArray(), outputEncoding = "point-i64UTC-i64offset-f64OHLC-boolVolume-f64Volume-boolGap-little-endian", outputSha256 = PointSha(points) };
    private static object SampleSummary(IReadOnlyList<ChartFinancialSample> samples)
    {
        using var bytes = new MemoryStream(); using var writer = new BinaryWriter(bytes);
        foreach (var sample in samples)
        {
            writer.Write((double)sample.Slot); writer.Write(sample.Time.UtcTicks); writer.Write(sample.Open); writer.Write(sample.High); writer.Write(sample.Low); writer.Write(sample.Close); writer.Write(sample.Volume.HasValue); writer.Write(sample.Volume ?? 0); writer.Write(sample.SourceStartIndex); writer.Write(sample.SourceEndIndex); writer.Write(sample.Direction); writer.Write(sample.IsThick); writer.Write(sample.IsSegmentStart); writer.Write(sample.BoxSize.HasValue); writer.Write(sample.BoxSize ?? 0); writer.Write(sample.BoxCount);
        }
        return new { count = samples.Count, first = samples.Take(4).Select(SampleRecord).ToArray(), last = samples.TakeLast(4).Select(SampleRecord).ToArray(), outputEncoding = "sample-f64Slot-i64UTC-f64OHLC-boolVolume-f64Volume-i32StartEndDirection-boolThickStartBox-f64Box-i32BoxCount-little-endian", outputSha256 = Convert.ToHexString(SHA256.HashData(bytes.GetBuffer().AsSpan(0, checked((int)bytes.Length)))) };
    }
    private static Observation Observe(string id, string kind, object[] input, object parameters, Func<object> calculate)
    {
        var item = new Observation { Id = id, Kind = kind, Input = input, Parameters = parameters };
        var started = Stopwatch.GetTimestamp(); var allocated = GC.GetAllocatedBytesForCurrentThread();
        try { item.Actual = calculate(); } catch (Exception exception) { item.ExceptionType = exception.GetType().Name; item.ExceptionMessage = exception.Message; }
        item.ElapsedTicks = Stopwatch.GetTimestamp() - started; item.ThreadAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        return item;
    }
    private static void Add(Report report, string id, string kind, ChartFinancialPoint[] input, object parameters, Func<object> calculate) => report.Cases.Add(Observe(id, kind, input.Select(PointRecord).ToArray(), parameters, calculate));
    private static void AddPerformance<T>(Report report, string id, string kind, ChartFinancialPoint[] input, string formula, object parameters, Func<IReadOnlyList<T>> calculate, Func<IReadOnlyList<T>, object> project)
    {
        var item = Observe(id, kind, [], parameters, () => calculate()); if (item.ExceptionType is null) item.Actual = project((IReadOnlyList<T>)item.Actual!); item.InputFormula = formula; item.InputCount = input.Length; item.InputEncoding = "point-i64UTC-i64offset-f64OHLC-boolVolume-f64Volume-boolGap-little-endian"; item.InputSha256 = PointSha(input); report.PerformanceCases.Add(item);
    }
    private static object AssemblyRecord(Assembly assembly) => new { path = assembly.Location, bytes = new FileInfo(assembly.Location).Length, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))), assembly = assembly.FullName };
    private sealed class Report
    {
        public string SchemaVersion { get; } = "charts-c502-financial-observations-v1";
        public DateTimeOffset StartedUtc { get; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? FinishedUtc { get; set; }
        public bool Passed { get; set; }
        public string? Error { get; set; }
        public long StopwatchFrequency { get; } = Stopwatch.Frequency;
        public object Environment { get; } = new { os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(), runtime = RuntimeInformation.FrameworkDescription, processorCount = System.Environment.ProcessorCount };
        public object[] AssemblyManifest { get; } = [AssemblyRecord(typeof(ChartFinancialTransforms).Assembly), AssemblyRecord(typeof(FinancialRegression).Assembly)];
        public bool FormalSlaEvaluated { get; } = false;
        public List<Observation> Cases { get; } = [];
        public List<Observation> PerformanceCases { get; } = [];
    }
    private sealed class Observation
    {
        public required string Id { get; init; }
        public required string Kind { get; init; }
        public required object[] Input { get; init; }
        public required object Parameters { get; init; }
        public object? Actual { get; set; }
        public string? ExceptionType { get; set; }
        public string? ExceptionMessage { get; set; }
        public string? InputFormula { get; set; }
        public int? InputCount { get; set; }
        public string? InputEncoding { get; set; }
        public string? InputSha256 { get; set; }
        public long ElapsedTicks { get; set; }
        public long ThreadAllocatedBytes { get; set; }
    }
}
