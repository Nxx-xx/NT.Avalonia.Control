using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using NT.AvaloniaControl.Charts;

namespace Charts.Benchmarks;

// Evidence recorder only. The independent consumer evaluates mathematical acceptance and performance policies.
internal static class C5CompletionRegression
{
    private sealed class FiniteDoubleConverter : JsonConverter<double>
    {
        public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType == JsonTokenType.Null ? double.NaN : reader.GetDouble();
        public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
        { if (double.IsFinite(value)) writer.WriteNumberValue(value); else writer.WriteNullValue(); }
    }
    private static readonly JsonSerializerOptions JsonOptions = CreateOptions();
    private static JsonSerializerOptions CreateOptions()
    { var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }; options.Converters.Add(new FiniteDoubleConverter()); return options; }
    private sealed class Capture
    {
        public required string Id { get; init; }
        public required string Kind { get; init; }
        public object? Input { get; init; }
        public object? Configuration { get; init; }
        public object? Actual { get; set; }
        public string? ExpectedExceptionType { get; init; }
        public string? ExceptionType { get; set; }
        public string? Error { get; set; }
        public double ElapsedMilliseconds { get; set; }
        public long AllocatedBytes { get; set; }
    }
    private static Capture Measure(string id, string kind, object? input, object? configuration, Func<object> action, string? expected = null)
    {
        var capture = new Capture { Id = id, Kind = kind, Input = input, Configuration = configuration, ExpectedExceptionType = expected };
        var allocated = GC.GetAllocatedBytesForCurrentThread(); var timestamp = Stopwatch.GetTimestamp();
        try { capture.Actual = action(); }
        catch (Exception error) { capture.ExceptionType = error.GetType().Name; capture.Error = error.Message; }
        capture.ElapsedMilliseconds = Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;
        capture.AllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        return capture;
    }
    private static string FileSha(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static object CategoricalConfiguration(CategoricalDistributionKind kind, double width = 372, double height = 232,
        double markerRadius = 4, double jitter = .36, int seed = 17, bool cancelled = false) => new { kind = kind.ToString(), width, height, markerRadius, jitter, seed, cancelled };
    private static object ProfileConfiguration(int sampleCount = 128, double? bandwidth = null, double? minimum = null, double? maximum = null, bool cancelled = false) => new { mode = "samples", sampleCount, bandwidth, minimum, maximum, cancelled };

    public static int Run(string[] args)
    {
        var outputIndex = Array.IndexOf(args, "--output");
        if (outputIndex >= 0 && outputIndex + 1 >= args.Length) { Console.Error.WriteLine("--output requires a fresh file path."); return 1; }
        var output = Path.GetFullPath(outputIndex >= 0 ? args[outputIndex + 1] : "artifacts/charts-c5-completion/raw.json");
        if (File.Exists(output)) { Console.Error.WriteLine("Choose a fresh evidence path; existing output is never overwritten."); return 1; }
        var started = DateTimeOffset.UtcNow; var cases = new List<Capture>(); var performance = new List<Capture>();
        void Add(string id, string kind, object? input, object? configuration, Func<object> action, string? expected = null) => cases.Add(Measure(id, kind, input, configuration, action, expected));
        object Report(bool completed, string? error = null) => new
        {
            schemaVersion = "charts-c5-completion-numeric-v1", startedUtc = started, finishedUtc = DateTimeOffset.UtcNow,
            framework = RuntimeInformation.FrameworkDescription, operatingSystem = RuntimeInformation.OSDescription,
            productAssemblySha256 = FileSha(typeof(ChartDistributionProfiles).Assembly.Location), producerAssemblySha256 = FileSha(Assembly.GetExecutingAssembly().Location),
            missingNumericEncoding = "null represents every original or returned nonfinite double; no named floating-point literals are emitted",
            allocationScope = "GC.GetAllocatedBytesForCurrentThread during the public API call; input construction and JSON serialization excluded",
            cases, performance, producerCompleted = completed, mathematicalAcceptanceEvaluated = false, formalSlaEvaluated = false, error
        };
        void Save(object report)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            using var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            JsonSerializer.Serialize(stream, report, JsonOptions);
        }
        try
        {
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            var categorical = new[] { new CategoricalSample("a", "Alpha", 2, "A"), new CategoricalSample("b", "Beta", -1, "B"), new CategoricalSample("c", "Alpha", 2, "C"), new CategoricalSample("d", "Beta", 8, "D"), new CategoricalSample("e", "Alpha", 4, "E") };
            foreach (var kind in Enum.GetValues<CategoricalDistributionKind>())
            {
                var name = kind == CategoricalDistributionKind.Beeswarm ? "beeswarm" : "strip";
                Add(name + "-basic", name, categorical, CategoricalConfiguration(kind), () => ChartCategoricalDistribution.Layout(categorical, kind, seed: 17));
                Add(name + "-empty", name, Array.Empty<CategoricalSample>(), CategoricalConfiguration(kind), () => ChartCategoricalDistribution.Layout([], kind, seed: 17));
                var constant = Enumerable.Range(0, 9).Select(i => new CategoricalSample("tie-" + i, "One", 5)).ToArray();
                Add(name + "-dense-constant", name, constant, CategoricalConfiguration(kind, 20, 12, 4, .5), () => ChartCategoricalDistribution.Layout(constant, kind, 20, 12, 4, .5, 17));
                Add(name + "-radius-zero", name, categorical, CategoricalConfiguration(kind, markerRadius: 0, jitter: 0), () => ChartCategoricalDistribution.Layout(categorical, kind, markerRadius: 0, jitter: 0, seed: 17));
                var huge = new[] { new CategoricalSample("negative", "Extreme", -double.MaxValue), new CategoricalSample("positive", "Extreme", double.MaxValue) };
                Add(name + "-extreme-finite", name, huge, CategoricalConfiguration(kind), () => ChartCategoricalDistribution.Layout(huge, kind, seed: 17));
                var invalid = new[] { new CategoricalSample("missing", "A", double.NaN) };
                Add(name + "-invalid-value", name, invalid, CategoricalConfiguration(kind), () => ChartCategoricalDistribution.Layout(invalid, kind, seed: 17), "ArgumentException");
                Add(name + "-invalid-jitter", name, categorical, CategoricalConfiguration(kind, jitter: .6), () => ChartCategoricalDistribution.Layout(categorical, kind, jitter: .6, seed: 17), "ArgumentOutOfRangeException");
                Add(name + "-cancel", name, categorical, CategoricalConfiguration(kind, cancelled: true), () => ChartCategoricalDistribution.Layout(categorical, kind, seed: 17, cancellationToken: cancelled.Token), "OperationCanceledException");
            }

            ScalarGridPoint[] Grid(double[,] values)
            {
                var rows = new List<ScalarGridPoint>();
                for (var y = 0; y < values.GetLength(0); y++) for (var x = 0; x < values.GetLength(1); x++) rows.Add(new($"g-{x}-{y}", x, y, values[y, x]));
                return rows.ToArray();
            }
            var grid = Grid(new double[,] { { 0, 1, 2 }, { 1, 2, 3 }, { 2, 3, 4 } }).Reverse().ToArray();
            var levels = new[] { 1.5, 2.5 };
            Add("contour-planar-reordered", "contour", grid, new { levels }, () => ChartContours.Build(grid, levels));
            Add("contour-auto-levels", "contour", grid, new { contourLevels = 3 }, () => ChartContours.Build(grid, 3));
            var saddle = Grid(new double[,] { { 2, -1 }, { -1, 2 } }); var saddleLevels = new[] { 0d, .5, 1 };
            Add("contour-saddle", "contour", saddle, new { levels = saddleLevels }, () => ChartContours.Build(saddle, saddleLevels));
            var masked = Grid(new double[,] { { 0, 1, 2 }, { 1, double.NaN, 3 }, { 2, 3, 4 } });
            Add("contour-masked", "contour", masked, new { levels }, () => ChartContours.Build(masked, levels));
            var plateau = Grid(new double[,] { { 1, 1 }, { 1, 1 } });
            Add("contour-plateau", "contour", plateau, new { levels = new[] { 1d } }, () => ChartContours.Build(plateau, [1d]));
            Add("contour-empty", "contour", Array.Empty<ScalarGridPoint>(), new { levels }, () => ChartContours.Build([], levels));
            var missingGrid = grid.Take(8).ToArray();
            Add("contour-incomplete", "contour", missingGrid, new { levels }, () => ChartContours.Build(missingGrid, levels), "ArgumentException");
            Add("contour-invalid-level-order", "contour", grid, new { levels = new[] { 2d, 1 } }, () => ChartContours.Build(grid, [2d, 1]), "ArgumentOutOfRangeException");
            Add("contour-cancel", "contour", grid, new { levels, cancelled = true }, () => ChartContours.Build(grid, levels, cancelled.Token), "OperationCanceledException");

            var distributions = new[] { new DistributionSamples("a", "First", new[] { 1d, 2, 3, 4, 5 }), new DistributionSamples("b", "Second", new[] { -2d, 0, 2, 6 }) };
            foreach (var name in new[] { "ridgeline", "violin" })
            {
                Add(name + "-shared-explicit", name, distributions, ProfileConfiguration(33, .75, -5, 9), () => ChartDistributionProfiles.Calculate(distributions, 33, .75, -5, 9));
                Add(name + "-automatic", name, distributions, ProfileConfiguration(65), () => ChartDistributionProfiles.Calculate(distributions, 65));
                var boundary = new[] { new DistributionSamples("empty", "Empty", []), new DistributionSamples("missing", "Missing", new[] { double.NaN, double.PositiveInfinity }), new DistributionSamples("singleton", "Singleton", new[] { 2d }), new DistributionSamples("constant", "Constant", new[] { 7d, 7, 7 }) };
                Add(name + "-empty-singleton-constant", name, boundary, ProfileConfiguration(17), () => ChartDistributionProfiles.Calculate(boundary, 17));
                Add(name + "-empty", name, Array.Empty<DistributionSamples>(), ProfileConfiguration(), () => ChartDistributionProfiles.Calculate([]));
                Add(name + "-invalid-bandwidth", name, distributions, ProfileConfiguration(bandwidth: 0), () => ChartDistributionProfiles.Calculate(distributions, bandwidth: 0), "ArgumentOutOfRangeException");
                Add(name + "-invalid-domain", name, distributions, ProfileConfiguration(minimum: 3, maximum: 2), () => ChartDistributionProfiles.Calculate(distributions, minimum: 3, maximum: 2), "ArgumentException");
                Add(name + "-cancel", name, distributions, ProfileConfiguration(cancelled: true), () => ChartDistributionProfiles.Calculate(distributions, cancellationToken: cancelled.Token), "OperationCanceledException");
            }
            var curves = new[] { new DistributionCurve("a", "Computed A", new[] { new ChartPoint(-2, 0), new ChartPoint(0, 4), new ChartPoint(2, 0) }), new DistributionCurve("b", "Computed B", new[] { new ChartPoint(0, 0), new ChartPoint(1, 2), new ChartPoint(4, 0) }) };
            Add("ridgeline-precomputed", "ridgeline", curves, new { mode = "curves", minimum = (double?)null, maximum = (double?)null }, () => ChartDistributionProfiles.FromCurves(curves));
            var zeroCurves = new[] { new DistributionCurve("empty", "Empty", []), new DistributionCurve("zero", "Zero", new[] { new ChartPoint(0, 0), new ChartPoint(2, 0) }) };
            Add("ridgeline-precomputed-zero-empty", "ridgeline", zeroCurves, new { mode = "curves", minimum = (double?)null, maximum = (double?)null }, () => ChartDistributionProfiles.FromCurves(zeroCurves));
            var invalidCurve = new[] { new DistributionCurve("bad", "Bad", new[] { new ChartPoint(0, 0), new ChartPoint(1, -1) }) };
            Add("ridgeline-precomputed-invalid", "ridgeline", invalidCurve, new { mode = "curves", minimum = (double?)null, maximum = (double?)null }, () => ChartDistributionProfiles.FromCurves(invalidCurve), "ArgumentException");
            Add("ridgeline-precomputed-cancel", "ridgeline", curves, new { mode = "curves", minimum = (double?)null, maximum = (double?)null, cancelled = true }, () => ChartDistributionProfiles.FromCurves(curves, cancellationToken: cancelled.Token), "OperationCanceledException");

            var mosaic = new[] { new MosaicObservation("a", "North", "One", 2), new MosaicObservation("b", "South", "Two", 6), new MosaicObservation("c", "North", "Two", 3), new MosaicObservation("d", "North", "One", 1) };
            Add("mosaic-aggregation", "mosaic", mosaic, null, () => ChartMultivariate.Mosaic(mosaic));
            var zeroMosaic = new[] { new MosaicObservation("zero", 1, "A", 0), new MosaicObservation("other", "1", "A", 0) };
            Add("mosaic-zero-typed-keys", "mosaic", zeroMosaic, null, () => ChartMultivariate.Mosaic(zeroMosaic));
            Add("mosaic-empty", "mosaic", Array.Empty<MosaicObservation>(), null, () => ChartMultivariate.Mosaic([]));
            var hugeMosaic = new[] { new MosaicObservation("a", "A", "A", double.MaxValue), new MosaicObservation("b", "B", "B", double.MaxValue) };
            Add("mosaic-scaled-large", "mosaic", hugeMosaic, null, () => ChartMultivariate.Mosaic(hugeMosaic));
            var invalidMosaic = new[] { new MosaicObservation("bad", "A", "A", -1) };
            Add("mosaic-negative", "mosaic", invalidMosaic, null, () => ChartMultivariate.Mosaic(invalidMosaic), "ArgumentException");
            var overflowingMosaic = new[] { new MosaicObservation("a", "A", "A", double.MaxValue), new MosaicObservation("b", "A", "A", double.MaxValue) };
            Add("mosaic-cell-overflow", "mosaic", overflowingMosaic, null, () => ChartMultivariate.Mosaic(overflowingMosaic), "OverflowException");
            Add("mosaic-cancel", "mosaic", mosaic, new { cancelled = true }, () => ChartMultivariate.Mosaic(mosaic, cancelled.Token), "OperationCanceledException");

            var dimensions = new[] { new ParallelDimension("a", "First"), new ParallelDimension("b", "Second", 0, 10, true), new ParallelDimension("c", "Third") };
            var records = new[] { new ParallelRecord("a", new[] { -2d, 20, 7 }, "One"), new ParallelRecord("b", new[] { 2d, 4, 7 }, "Two"), new ParallelRecord("c", new[] { double.NaN, double.NaN, 7 }, "Gaps") };
            Add("parallel-scales-gaps-constant-reversed-clamped", "parallel", new { records, dimensions }, null, () => ChartMultivariate.Parallel(records, dimensions));
            var missingRecords = new[] { new ParallelRecord("empty", new[] { double.NaN, double.NaN }) }; var missingDimensions = new[] { new ParallelDimension("a", "A"), new ParallelDimension("b", "B") };
            Add("parallel-entirely-missing", "parallel", new { records = missingRecords, dimensions = missingDimensions }, null, () => ChartMultivariate.Parallel(missingRecords, missingDimensions));
            Add("parallel-empty", "parallel", new { records = Array.Empty<ParallelRecord>(), dimensions }, null, () => ChartMultivariate.Parallel([], dimensions));
            var hugeRecords = new[] { new ParallelRecord("a", new[] { -double.MaxValue, 1d }), new ParallelRecord("b", new[] { double.MaxValue, 3d }) };
            Add("parallel-extreme-finite", "parallel", new { records = hugeRecords, dimensions = missingDimensions }, null, () => ChartMultivariate.Parallel(hugeRecords, missingDimensions));
            var wrongSize = new[] { new ParallelRecord("wrong", new[] { 1d }) };
            Add("parallel-length-mismatch", "parallel", new { records = wrongSize, dimensions }, null, () => ChartMultivariate.Parallel(wrongSize, dimensions), "ArgumentException");
            var invalidDimensions = new[] { new ParallelDimension("a", "A", 5, 1), new ParallelDimension("b", "B") };
            Add("parallel-invalid-domain", "parallel", new { records = Array.Empty<ParallelRecord>(), dimensions = invalidDimensions }, null, () => ChartMultivariate.Parallel([], invalidDimensions), "ArgumentException");
            Add("parallel-cancel", "parallel", new { records, dimensions }, new { cancelled = true }, () => ChartMultivariate.Parallel(records, dimensions, cancelled.Token), "OperationCanceledException");

            var scaleCategorical = Enumerable.Range(0, 4000).Select(i => new CategoricalSample("p" + i, "c" + i % 8, (i * 37 % 997) / 10d, "p" + i)).ToArray();
            foreach (var kind in Enum.GetValues<CategoricalDistributionKind>())
            { var name = kind == CategoricalDistributionKind.Beeswarm ? "beeswarm" : "strip"; performance.Add(Measure(name + "-4000", name, scaleCategorical, CategoricalConfiguration(kind, 800, 480, 2), () => ChartCategoricalDistribution.Layout(scaleCategorical, kind, 800, 480, 2, .36, 17))); }
            var scaleGrid = Enumerable.Range(0, 64 * 64).Select(i => new ScalarGridPoint("g" + i, i % 64, i / 64, Math.Sin(i % 64 * .1) + Math.Cos(i / 64 * .1))).ToArray();
            var scaleLevels = new[] { -.75, 0d, .75 };
            performance.Add(Measure("contour-64x64", "contour", scaleGrid, new { levels = scaleLevels }, () => ChartContours.Build(scaleGrid, scaleLevels)));
            var scaleSamples = new[] { new DistributionSamples("a", "First", Enumerable.Range(0, 5000).Select(i => Math.Sin(i * .017) + i % 19 * .01).ToArray()), new DistributionSamples("b", "Second", Enumerable.Range(0, 5000).Select(i => Math.Cos(i * .023) * 2).ToArray()) };
            foreach (var name in new[] { "ridgeline", "violin" }) performance.Add(Measure(name + "-10000", name, scaleSamples, ProfileConfiguration(128, .1), () => ChartDistributionProfiles.Calculate(scaleSamples, 128, .1)));
            var scaleMosaic = Enumerable.Range(0, 20000).Select(i => new MosaicObservation("m" + i, "c" + i % 100, "r" + i / 100, i * 17 % 997 + 1)).ToArray();
            performance.Add(Measure("mosaic-20000", "mosaic", scaleMosaic, null, () => ChartMultivariate.Mosaic(scaleMosaic)));
            var scaleDimensions = Enumerable.Range(0, 6).Select(i => new ParallelDimension("d" + i, "Dimension " + i)).ToArray();
            var scaleRecords = Enumerable.Range(0, 20000).Select(i => new ParallelRecord("p" + i, Enumerable.Range(0, 6).Select(d => (i * (d + 3) % 997) / 10d).ToArray())).ToArray();
            performance.Add(Measure("parallel-20000x6", "parallel", new { records = scaleRecords, dimensions = scaleDimensions }, null, () => ChartMultivariate.Parallel(scaleRecords, scaleDimensions)));
            Save(Report(true)); Console.WriteLine($"Recorded {cases.Count} fixed cases and {performance.Count} bounded scale scenarios: {output}"); return 0;
        }
        catch (Exception error)
        {
            if (!File.Exists(output)) { try { Save(Report(false, error.ToString())); } catch (Exception saveError) { Console.Error.WriteLine(saveError); } }
            Console.Error.WriteLine(error); return 1;
        }
    }
}
