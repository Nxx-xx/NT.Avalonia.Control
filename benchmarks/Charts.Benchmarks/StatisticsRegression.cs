using System.Diagnostics;
using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using NT.AvaloniaControl.Charts;

namespace Charts.Benchmarks;

// This producer records observations. Independent mathematical expectations live in
// the separately pinned validation oracle, not in this executable or its geometry.
internal static class StatisticsRegression
{
    public static int Run(string[] args)
    {
        var at = Array.IndexOf(args, "--output");
        var output = Path.GetFullPath(at >= 0 && at + 1 < args.Length ? args[at + 1] : "artifacts/charts-statistics/raw.json");
        if (File.Exists(output)) { Console.Error.WriteLine("Choose a new evidence path."); return 1; }
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var report = new Report();
        try
        {
            var canonical = new double[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 30 };
            foreach (var probability in new[] { 0d, .1, .25, .5, .75, 1d })
                Add(report, "quantile-" + probability.ToString(System.Globalization.CultureInfo.InvariantCulture), "quantile", canonical,
                    new { probability }, () => new { value = ChartStatistics.Quantile(canonical, probability) });
            Add(report, "box-canonical", "box", canonical, new { whiskerMultiplier = 1.5 }, () => ChartStatistics.BoxPlot(canonical));
            var repeated = new double[] { -20, -20, 1, 2, 3, 4, 5, 6, 7, 8, 40, 40 };
            Add(report, "box-duplicate-outliers", "box", repeated, new { whiskerMultiplier = .5 }, () => ChartStatistics.BoxPlot(repeated, .5));
            var invalid = new[] { double.NaN, 1d, 2d, double.PositiveInfinity, 3d, 4d, double.NegativeInfinity };
            Add(report, "box-nonfinite", "box", invalid, new { whiskerMultiplier = 1.5 }, () => ChartStatistics.BoxPlot(invalid));
            Add(report, "box-single", "box", [5], new { whiskerMultiplier = 1.5 }, () => ChartStatistics.BoxPlot([5]));
            Add(report, "error-asymmetric", "error", [], new { center = 5d, lowerError = 3d, upperError = 4d }, () => ChartStatistics.ErrorInterval(5, 3, 4));
            Add(report, "error-symmetric", "error", [], new { center = 10d, lowerError = 2d, upperError = 2d }, () => ChartStatistics.SymmetricError(10, 2));
            var confidence = new double[] { 1, 2, 3, 4, 5 };
            Add(report, "mean-confidence", "confidence", confidence, new { confidenceLevel = .95 }, () => ChartStatistics.MeanConfidenceInterval(confidence));
            var histogram = new double[] { 0, .5, 1, 1.5, 2, 2.5, 3, 4, 5 };
            foreach (var normalization in Enum.GetValues<ChartHistogramNormalization>())
            {
                var normalizationName = normalization.ToString();
                Add(report, "histogram-" + normalizationName.ToLowerInvariant(), "histogram", histogram,
                    new { binCount = 4, minimum = 0d, maximum = 4d, normalization = normalizationName },
                    () => ChartStatistics.Histogram(histogram, 4, normalization, 0, 4));
            }
            var boundaries = new double[] { 0, 1, 3, 6 };
            var nonuniform = new[] { -1d, 0d, .5, 1d, 2d, 3d, 5d, 6d, 7d, double.NaN };
            Add(report, "histogram-nonuniform-density", "histogram-boundaries", nonuniform,
                new { boundaries, normalization = "Density" }, () => ChartStatistics.HistogramByBoundaries(nonuniform, boundaries, ChartHistogramNormalization.Density));
            Add(report, "kde-unit-gaussian", "density", [0], new { sampleCount = 3, bandwidth = 1d, minimum = -1d, maximum = 1d },
                () => ChartStatistics.KernelDensity([0], 3, 1, -1, 1));
            var density = new double[] { -2, -1, 0, 1, 2, 10 };
            Add(report, "kde-auto-bandwidth", "density", density, new { sampleCount = 17, bandwidth = (double?)null, minimum = (double?)null, maximum = (double?)null },
                () => ChartStatistics.KernelDensity(density, 17));
            Add(report, "kde-constant", "density", [5, 5], new { sampleCount = 9, bandwidth = (double?)null, minimum = (double?)null, maximum = (double?)null },
                () => ChartStatistics.KernelDensity([5, 5], 9));
            var finiteDensity = new[] { double.NaN, -1d, 0d, double.PositiveInfinity, 1d };
            Add(report, "kde-nonfinite", "density", finiteDensity, new { sampleCount = 7, bandwidth = .5, minimum = -2d, maximum = 2d },
                () => ChartStatistics.KernelDensity(finiteDensity, 7, .5, -2, 2));
            Add(report, "invalid-empty-box", "invalid", [], new { operation = "box", expectedException = "ArgumentException" }, () => ChartStatistics.BoxPlot([]));
            Add(report, "invalid-nonfinite-box", "invalid", [double.NaN, double.PositiveInfinity], new { operation = "box", expectedException = "ArgumentException" }, () => ChartStatistics.BoxPlot([double.NaN, double.PositiveInfinity]));
            Add(report, "invalid-quantile", "invalid", [1, 2], new { operation = "quantile", expectedException = "ArgumentOutOfRangeException" }, () => ChartStatistics.Quantile([1, 2], 2));
            Add(report, "invalid-negative-error", "invalid", [], new { operation = "error", expectedException = "ArgumentOutOfRangeException" }, () => ChartStatistics.ErrorInterval(5, -1, 2));
            Add(report, "invalid-bandwidth", "invalid", [0, 1], new { operation = "density", expectedException = "ArgumentOutOfRangeException" }, () => ChartStatistics.KernelDensity([0, 1], 3, 0));
            Add(report, "invalid-confidence", "invalid", [1, 2], new { operation = "confidence", expectedException = "ArgumentOutOfRangeException" }, () => ChartStatistics.MeanConfidenceInterval([1, 2], 1));
            Add(report, "invalid-bin-boundaries", "invalid", [1, 2], new { operation = "histogram", expectedException = "ArgumentException" }, () => ChartStatistics.HistogramByBoundaries([1, 2], [0, 1, 1]));
            Add(report, "summary-canonical", "summary", canonical, new { }, () => ChartStatistics.Summarize(canonical));
            Add(report, "summary-single-filtered", "summary", [double.NaN, 5], new { }, () => ChartStatistics.Summarize([double.NaN, 5]));
            var empirical = new[] { 3d, 1d, 1d, double.NaN, 2d, double.PositiveInfinity, 3d };
            Add(report, "empirical-ties-filtered", "empirical", empirical, new { }, () => ChartStatistics.EmpiricalCumulativeDistribution(empirical));
            var correlationX = new double[] { 1, 2, 3, 4 }; var correlationY = new double[] { 3, 7, 11, 15 };
            Add(report, "correlation-linear", "correlation", correlationY, new { x = correlationX },
                () => new { value = ChartStatistics.PearsonCorrelation(correlationX.Zip(correlationY, (x, y) => new ChartPoint(x, y))) });
            var nonlinearX = new double[] { -1, 0, 1, 2 }; var nonlinearY = new double[] { 2, 0, 1, 5 };
            Add(report, "correlation-nonlinear", "correlation", nonlinearY, new { x = nonlinearX },
                () => new { value = ChartStatistics.PearsonCorrelation(nonlinearX.Zip(nonlinearY, (x, y) => new ChartPoint(x, y))) });
            Add(report, "histogram-fixed-width", "histogram-width", histogram,
                new { binWidth = 2d, minimum = 0d, maximum = 5d, normalization = "Density" },
                () => ChartStatistics.HistogramByWidth(histogram, 2, ChartHistogramNormalization.Density, 0, 5));
            var large = Enumerable.Range(0, 100_000).Select(i => ((i * 37) % 997 - 498) * .125 + (i % 251 == 0 ? 128d : 0d)).ToArray();
            const string largeFormula = "((i*37)%997-498)*0.125+(i%251==0?128:0); i=0..99999";
            AddPerformance(report, "perf-box-100k", "box", large, largeFormula, new { whiskerMultiplier = 1.5 }, () => ChartStatistics.BoxPlot(large));
            AddPerformance(report, "perf-histogram-100k", "histogram", large, largeFormula,
                new { binCount = 64, minimum = -64d, maximum = 192d, normalization = "Density" },
                () => ChartStatistics.Histogram(large, 64, ChartHistogramNormalization.Density, -64, 192));
            var medium = Enumerable.Range(0, 2048).Select(i => ((i * 29) % 257 - 128) * .03125 + (i % 13 == 0 ? 4d : 0d)).ToArray();
            AddPerformance(report, "perf-density-2048", "density", medium,
                "((i*29)%257-128)*0.03125+(i%13==0?4:0); i=0..2047",
                new { sampleCount = 65, bandwidth = .5, minimum = -5d, maximum = 9d },
                () => ChartStatistics.KernelDensity(medium, 65, .5, -5, 9));
            report.Passed = report.Cases.Count == 34 && report.PerformanceCases.Count == 3 && report.Cases.All(item => item.Kind == "invalid" ? item.ExceptionType is not null : item.ExceptionType is null) && report.PerformanceCases.All(item => item.ExceptionType is null);
        }
        catch (Exception exception) { report.Error = exception.ToString(); Console.Error.WriteLine(exception); }
        report.FinishedUtc = DateTimeOffset.UtcNow;
        using var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        JsonSerializer.Serialize(stream, report, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        return report.Passed ? 0 : 1;
    }

    private static void Add(Report report, string id, string kind, double[] values, object parameters, Func<object> calculate)
    {
        report.Cases.Add(Observe(id, kind, values.Select(Encode).ToArray(), parameters, calculate));
    }

    private static Observation Observe(string id, string kind, object[] input, object parameters, Func<object> calculate)
    {
        var item = new Observation { Id = id, Kind = kind, Input = input, Parameters = parameters };
        var started = Stopwatch.GetTimestamp(); var allocated = GC.GetAllocatedBytesForCurrentThread();
        try { item.Actual = calculate(); }
        catch (Exception exception) { item.ExceptionType = exception.GetType().Name; item.ExceptionMessage = exception.Message; }
        item.ElapsedTicks = Stopwatch.GetTimestamp() - started;
        item.ThreadAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        return item;
    }

    private static void AddPerformance(Report report, string id, string kind, double[] values, string formula, object parameters, Func<object> calculate)
    {
        var item = Observe(id, kind, [], parameters, calculate);
        var bytes = new byte[values.Length * sizeof(double)];
        for (var i = 0; i < values.Length; i++) BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(i * sizeof(double)), BitConverter.DoubleToInt64Bits(values[i]));
        item.InputFormula = formula; item.InputCount = values.Length;
        item.InputEncoding = "IEEE754-float64-little-endian-concatenation";
        item.InputSha256 = Convert.ToHexString(SHA256.HashData(bytes));
        report.PerformanceCases.Add(item);
    }

    private static object Encode(double value) => double.IsNaN(value) ? "NaN" : double.IsPositiveInfinity(value) ? "+Infinity" : double.IsNegativeInfinity(value) ? "-Infinity" : value;
    private static object AssemblyRecord(Assembly assembly)
    {
        var path = assembly.Location;
        return new { path, bytes = new FileInfo(path).Length, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), assembly = assembly.FullName };
    }

    private sealed class Report
    {
        public string SchemaVersion { get; } = "charts-c501-statistics-observations-v1";
        public DateTimeOffset StartedUtc { get; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? FinishedUtc { get; set; }
        public bool Passed { get; set; }
        public string? Error { get; set; }
        public long StopwatchFrequency { get; } = Stopwatch.Frequency;
        public object Environment { get; } = new { os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(), runtime = RuntimeInformation.FrameworkDescription, processorCount = System.Environment.ProcessorCount };
        public object[] AssemblyManifest { get; } = [AssemblyRecord(typeof(ChartStatistics).Assembly), AssemblyRecord(typeof(StatisticsRegression).Assembly)];
        public bool FormalSlaEvaluated { get; } = false;
        public List<Observation> Cases { get; } = [];
        public List<Observation> PerformanceCases { get; } = [];
    }

    private sealed class Observation
    {
        public required string Id { get; init; }
        public required string Kind { get; init; }
        public required object[] Input { get; init; }
        public string? InputFormula { get; set; }
        public int? InputCount { get; set; }
        public string? InputEncoding { get; set; }
        public string? InputSha256 { get; set; }
        public required object Parameters { get; init; }
        public object? Actual { get; set; }
        public string? ExceptionType { get; set; }
        public string? ExceptionMessage { get; set; }
        public long ElapsedTicks { get; set; }
        public long ThreadAllocatedBytes { get; set; }
    }
}
