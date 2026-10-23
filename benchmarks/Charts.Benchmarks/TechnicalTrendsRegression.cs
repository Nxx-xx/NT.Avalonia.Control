using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using NT.AvaloniaControl.Charts;

namespace Charts.Benchmarks;

// Raw observations only: the separately sealed Python oracle computes every expected value.
internal static class TechnicalTrendsRegression
{
    public static int Run(string[] args)
    {
        var at = Array.IndexOf(args, "--output");
        var output = Path.GetFullPath(at >= 0 && at + 1 < args.Length ? args[at + 1] : "artifacts/charts-technical-trends/raw.json");
        if (File.Exists(output)) { Console.Error.WriteLine("Choose a new evidence path."); return 1; }
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var report = new Report();
        try
        {
            var waves = Points(2, 4, 1, 7, 3, 9, 5, 11, 6);
            var gaps = new[] { new ChartPoint(0, 2), new ChartPoint(1, 4), new ChartPoint(2, 6), new ChartPoint(3, double.NaN), new ChartPoint(4, 10), new ChartPoint(5, 12), new ChartPoint(double.NaN, 14), new ChartPoint(7, 16), new ChartPoint(8, 18), new ChartPoint(9, 20), new ChartPoint(10, 22) };
            foreach (var type in Enum.GetValues<ChartMovingAverageType>())
            {
                var name = type.ToString().ToLowerInvariant();
                Add(report, "ma-" + name + "-odd", "ma", waves, new { period = 3, type = type.ToString() }, () => Full(ChartAnalytics.MovingAverage(waves, 3, type)));
                Add(report, "ma-" + name + "-even", "ma", waves, new { period = 4, type = type.ToString() }, () => Full(ChartAnalytics.MovingAverage(waves, 4, type)));
                Add(report, "ma-" + name + "-gap-reset", "ma", gaps, new { period = 3, type = type.ToString() }, () => Full(ChartAnalytics.MovingAverage(gaps, 3, type)));
                Add(report, "ma-" + name + "-period-one", "ma", waves, new { period = 1, type = type.ToString() }, () => Full(ChartAnalytics.MovingAverage(waves, 1, type)));
            }
            Add(report, "ma-empty", "ma", [], new { period = 3, type = "Simple" }, () => Full(ChartAnalytics.MovingAverage([], 3)));
            Add(report, "ma-long-warmup", "ma", waves, new { period = 20, type = "Simple" }, () => Full(ChartAnalytics.MovingAverage(waves, 20)));
            foreach (var (id, input, period, multiplier) in new[] { ("bollinger-waves", waves, 3, 2d), ("bollinger-gap-reset", gaps, 3, 2d), ("bollinger-zero-width", waves, 4, 0d), ("bollinger-population", Points(1, 2, 3, 4), 4, 1d), ("bollinger-period-one", waves, 1, 2d), ("bollinger-empty", Array.Empty<ChartPoint>(), 3, 2d) })
                Add(report, id, "bollinger", input, new { period, multiplier }, () => Bands(ChartAnalytics.BollingerBands(input, period, multiplier)));
            var fits = new (string Id, ChartTrendlineType Type, ChartPoint[] Input, int Degree)[]
            {
                ("linear-exact", ChartTrendlineType.Linear, Sequence(7, i => -2 + 3 * i), 2),
                ("linear-noisy", ChartTrendlineType.Linear, waves, 2),
                ("polynomial-exact", ChartTrendlineType.Polynomial, Sequence(9, i => 2 - 3 * i + .5 * i * i), 2),
                ("polynomial-cubic", ChartTrendlineType.Polynomial, Sequence(9, i => 1 + i - .25 * i * i + .125 * i * i * i), 3),
                ("polynomial-large-offset", ChartTrendlineType.Polynomial, Enumerable.Range(0, 10).Select(i => new ChartPoint(1e9 + i, 7 - 3 * i + i * i)).ToArray(), 2),
                ("exponential-exact", ChartTrendlineType.Exponential, Sequence(7, i => 2 * Math.Exp(.25 * i)), 2),
                ("logarithmic-exact", ChartTrendlineType.Logarithmic, Enumerable.Range(1, 7).Select(i => new ChartPoint(i, 3 + 2 * Math.Log(i))).ToArray(), 2),
                ("power-exact", ChartTrendlineType.Power, Enumerable.Range(1, 7).Select(i => new ChartPoint(i, 2 * Math.Pow(i, 1.5))).ToArray(), 2),
                ("linear-constant", ChartTrendlineType.Linear, Sequence(5, _ => 4), 2),
                ("linear-omission", ChartTrendlineType.Linear, [new(0, 1), new(1, 3), new(2, double.NaN), new(double.PositiveInfinity, 4), new(3, 7)], 2),
                ("exponential-positive-domain", ChartTrendlineType.Exponential, [new(0, 1), new(1, Math.E), new(2, 0), new(3, -1), new(4, Math.Exp(4))], 2),
                ("logarithmic-positive-domain", ChartTrendlineType.Logarithmic, [new(-1, 4), new(0, 2), new(1, 3), new(2, 3 + 2 * Math.Log(2)), new(4, 3 + 2 * Math.Log(4))], 2),
                ("power-positive-domain", ChartTrendlineType.Power, [new(-1, 4), new(0, 2), new(1, 2), new(2, 0), new(3, -1), new(4, 16), new(9, 54)], 2)
            };
            foreach (var fit in fits)
                Add(report, fit.Id, "trend", fit.Input, new { type = fit.Type.ToString(), degree = fit.Degree, period = 3, sampleCount = 9, minimum = (double?)null, maximum = (double?)null }, () => Trend(ChartAnalytics.FitTrend(fit.Input, fit.Type, fit.Degree, 3, 9)));
            Add(report, "linear-explicit-range", "trend", waves, new { type = "Linear", degree = 2, period = 3, sampleCount = 9, minimum = -2d, maximum = 10d }, () => Trend(ChartAnalytics.FitTrend(waves, period: 3, sampleCount: 9, minimum: -2, maximum: 10)));
            Add(report, "trend-moving-average", "trend", gaps, new { type = "MovingAverage", degree = 2, period = 3, sampleCount = 9, minimum = (double?)null, maximum = (double?)null }, () => Trend(ChartAnalytics.FitTrend(gaps, ChartTrendlineType.MovingAverage, period: 3, sampleCount: 9)));
            Invalid(report, "invalid-ma-period-zero", "ArgumentOutOfRangeException", () => ChartAnalytics.MovingAverage(waves, 0));
            Invalid(report, "invalid-ma-period-upper", "ArgumentOutOfRangeException", () => ChartAnalytics.MovingAverage(waves, 65537));
            Invalid(report, "invalid-ma-enum", "ArgumentOutOfRangeException", () => ChartAnalytics.MovingAverage(waves, type: (ChartMovingAverageType)99));
            Invalid(report, "invalid-bollinger-negative", "ArgumentOutOfRangeException", () => ChartAnalytics.BollingerBands(waves, 3, -1));
            Invalid(report, "invalid-bollinger-infinity", "ArgumentOutOfRangeException", () => ChartAnalytics.BollingerBands(waves, 3, double.PositiveInfinity));
            Invalid(report, "invalid-trend-enum", "ArgumentOutOfRangeException", () => ChartAnalytics.FitTrend(waves, (ChartTrendlineType)99));
            Invalid(report, "invalid-degree-zero", "ArgumentOutOfRangeException", () => ChartAnalytics.FitTrend(waves, ChartTrendlineType.Polynomial, 0));
            Invalid(report, "invalid-degree-upper", "ArgumentOutOfRangeException", () => ChartAnalytics.FitTrend(waves, ChartTrendlineType.Polynomial, 11));
            Invalid(report, "invalid-samples-low", "ArgumentOutOfRangeException", () => ChartAnalytics.FitTrend(waves, sampleCount: 1));
            Invalid(report, "invalid-samples-high", "ArgumentOutOfRangeException", () => ChartAnalytics.FitTrend(waves, sampleCount: 4097));
            Invalid(report, "invalid-insufficient", "ArgumentException", () => ChartAnalytics.FitTrend([new(1, 2)], ChartTrendlineType.Linear));
            Invalid(report, "invalid-rank", "ArgumentException", () => ChartAnalytics.FitTrend([new(1, 2), new(1, 3), new(1, 4)], ChartTrendlineType.Polynomial));
            Invalid(report, "invalid-repeated-rank", "ArgumentException", () => ChartAnalytics.FitTrend([new(1, 2), new(1, 3), new(2, 4), new(2, 5)], ChartTrendlineType.Polynomial));
            Invalid(report, "invalid-sampling-order", "ArgumentException", () => ChartAnalytics.FitTrend(waves, minimum: 5, maximum: 1));
            Invalid(report, "invalid-positive-bound", "ArgumentOutOfRangeException", () => ChartAnalytics.FitTrend(fits[6].Input, ChartTrendlineType.Logarithmic, minimum: 0));
            Invalid(report, "invalid-moving-average-bounds", "ArgumentException", () => ChartAnalytics.FitTrend(waves, ChartTrendlineType.MovingAverage, minimum: 0));
            Invalid(report, "invalid-moving-average-predict", "InvalidOperationException", () => ChartAnalytics.FitTrend(waves, ChartTrendlineType.MovingAverage, period: 3).Predict(2));
            Invalid(report, "invalid-predict-nan", "ArgumentOutOfRangeException", () => ChartAnalytics.FitTrend(waves).Predict(double.NaN));
            Invalid(report, "invalid-bollinger-overflow", "OverflowException", () => ChartAnalytics.BollingerBands(Points(-double.MaxValue, double.MaxValue), 2, 2));
            Invalid(report, "invalid-cancelled", "OperationCanceledException", () => ChartAnalytics.MovingAverage(waves, cancellationToken: new CancellationToken(true)));
            Invalid(report, "invalid-input-budget", "ArgumentException", () => ChartAnalytics.MovingAverage(Enumerable.Range(0, 1_000_001).Select(i => new ChartPoint(i, 1))));
            var large = Enumerable.Range(0, 100_000).Select(i => new ChartPoint(i * .001, 100 + ((i * 37) % 257) * .125)).ToArray();
            const string formula = "X=i*0.001;Y=100+((i*37)%257)*0.125;i=0..99999";
            Performance(report, "perf-sma-100k", "ma", large, formula, new { period = 20, type = "Simple" }, () => ChartAnalytics.MovingAverage(large, 20), value => Full((IReadOnlyList<ChartPoint>)value));
            Performance(report, "perf-ema-100k", "ma", large, formula, new { period = 20, type = "Exponential" }, () => ChartAnalytics.MovingAverage(large, 20, ChartMovingAverageType.Exponential), value => Full((IReadOnlyList<ChartPoint>)value));
            Performance(report, "perf-bollinger-100k", "bollinger", large, formula, new { period = 20, multiplier = 2d }, () => ChartAnalytics.BollingerBands(large, 20), value => Bands((IReadOnlyList<ChartBollingerPoint>)value));
            Performance(report, "perf-linear-100k", "trend", large, formula, new { type = "Linear", degree = 2, period = 3, sampleCount = 129, minimum = (double?)null, maximum = (double?)null }, () => ChartAnalytics.FitTrend(large, period: 3, sampleCount: 129), value => Trend((ChartTrendlineResult)value));
            report.Passed = report.Cases.Count == 60 && report.PerformanceCases.Count == 4 && report.Cases.All(c => c.Kind == "invalid" ? c.ExceptionType is not null : c.ExceptionType is null) && report.PerformanceCases.All(c => c.ExceptionType is null);
        }
        catch (Exception exception) { report.Error = exception.ToString(); Console.Error.WriteLine(exception); }
        report.FinishedUtc = DateTimeOffset.UtcNow;
        using var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        JsonSerializer.Serialize(stream, report, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        return report.Passed ? 0 : 1;
    }

    private static ChartPoint[] Points(params double[] values) => values.Select((y, x) => new ChartPoint(x, y)).ToArray();
    private static ChartPoint[] Sequence(int count, Func<double, double> value) => Enumerable.Range(0, count).Select(i => new ChartPoint(i, value(i))).ToArray();
    private static object Encode(double value) => double.IsNaN(value) ? "NaN" : double.IsPositiveInfinity(value) ? "+Infinity" : double.IsNegativeInfinity(value) ? "-Infinity" : value;
    private static object Point(ChartPoint p) => new { x = Encode(p.X), y = Encode(p.Y) };
    private static object[] Full(IReadOnlyList<ChartPoint> value) => value.Select(Point).ToArray();
    private static object[] Bands(IReadOnlyList<ChartBollingerPoint> value) => value.Select(p => (object)new { x = Encode(p.X), middle = Encode(p.Middle), upper = Encode(p.Upper), lower = Encode(p.Lower) }).ToArray();
    private static object Trend(ChartTrendlineResult value) => new { type = value.Type.ToString(), value.Count, value.OmittedCount, value.RSquared, points = Full(value.Points), predictions = value.Type == ChartTrendlineType.MovingAverage ? null : value.Points.Where((_, i) => i == 0 || i == value.Points.Count / 2 || i == value.Points.Count - 1).Select(p => new { x = Encode(p.X), y = Encode(value.Predict(p.X)) }).ToArray() };
    private static Observation Observe(string id, string kind, object[] input, object parameters, Func<object> calculate, Func<object, object>? project = null)
    {
        var value = new Observation { Id = id, Kind = kind, Input = input, Parameters = parameters };
        var started = Stopwatch.GetTimestamp(); var allocated = GC.GetAllocatedBytesForCurrentThread();
        try { value.Actual = calculate(); } catch (Exception e) { value.ExceptionType = e.GetType().Name; value.ExceptionMessage = e.Message; }
        value.ElapsedTicks = Stopwatch.GetTimestamp() - started; value.ThreadAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        if (value.ExceptionType is null && project is not null) value.Actual = project(value.Actual!);
        return value;
    }
    private static void Add(Report report, string id, string kind, ChartPoint[] input, object parameters, Func<object> calculate) => report.Cases.Add(Observe(id, kind, input.Select(Point).ToArray(), parameters, calculate));
    private static void Invalid(Report report, string id, string expectedException, Func<object> calculate) => report.Cases.Add(Observe(id, "invalid", [], new { expectedException }, calculate));
    private static void Performance(Report report, string id, string kind, ChartPoint[] input, string formula, object parameters, Func<object> calculate, Func<object, object> project)
    {
        var value = Observe(id, kind, [], parameters, calculate, project); value.InputFormula = formula; value.InputCount = input.Length;
        using var bytes = new MemoryStream(); using var writer = new BinaryWriter(bytes); foreach (var p in input) { writer.Write(p.X); writer.Write(p.Y); }
        value.InputSha256 = Convert.ToHexString(SHA256.HashData(bytes.GetBuffer().AsSpan(0, checked((int)bytes.Length)))); report.PerformanceCases.Add(value);
    }
    private static object AssemblyRecord(Assembly a) => new { path = a.Location, bytes = new FileInfo(a.Location).Length, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))), assembly = a.FullName };
    private sealed class Report
    {
        public string SchemaVersion { get; } = "charts-c503-technical-trends-observations-v1";
        public DateTimeOffset StartedUtc { get; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? FinishedUtc { get; set; }
        public bool Passed { get; set; }
        public string? Error { get; set; }
        public long StopwatchFrequency { get; } = Stopwatch.Frequency;
        public object Environment { get; } = new { os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(), runtime = RuntimeInformation.FrameworkDescription, processorCount = System.Environment.ProcessorCount };
        public object[] AssemblyManifest { get; } = [AssemblyRecord(typeof(ChartAnalytics).Assembly), AssemblyRecord(typeof(TechnicalTrendsRegression).Assembly)];
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
        public string? InputSha256 { get; set; }
        public long ElapsedTicks { get; set; }
        public long ThreadAllocatedBytes { get; set; }
    }
}
