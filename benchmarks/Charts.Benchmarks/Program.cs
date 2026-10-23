using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using NT.AvaloniaControl.Charts.Themes;

namespace Charts.Benchmarks;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--c5-completion-regression", StringComparer.Ordinal)) return C5CompletionRegression.Run(args);
        if (args.Contains("--c6-completion-regression", StringComparer.Ordinal)) return C6CompletionRegression.Run(args);
        if (args.Contains("--maps-regression", StringComparer.Ordinal)) return MapsRegression.Run(args);
        if (args.Contains("--relations-regression", StringComparer.Ordinal)) return RelationsRegression.Run(args);
        if (args.Contains("--hierarchy-regression", StringComparer.Ordinal)) return HierarchyRegression.Run(args);
        if (args.Contains("--advanced-comparison-regression", StringComparer.Ordinal)) return AdvancedComparisonRegression.Run(args);
        if (args.Contains("--engineering-projections-regression", StringComparer.Ordinal)) return EngineeringRegression.Run(args);
        if (args.Contains("--technical-trends-regression", StringComparer.Ordinal)) return TechnicalTrendsRegression.Run(args);
        if (args.Contains("--financial-prices-regression", StringComparer.Ordinal)) return FinancialRegression.Run(args);
        if (args.Contains("--statistics-distributions-regression", StringComparer.Ordinal)) return StatisticsRegression.Run(args);
        if (args.Contains("--multichart-background-layout-regression", StringComparer.Ordinal)) return MultiChartLayoutRegression.Run(args);
        if (args.Contains("--render-resource-budget-regression", StringComparer.Ordinal)) return ResourceBudgetRegression.Run(args);
        if (args.Contains("--incremental-streaming-regression", StringComparer.Ordinal)) return StreamingRegression.Run(args);
        if (args.Contains("--efficient-hit-regression", StringComparer.Ordinal)) return EfficientHitRegression.Run(args);
        if (args.Contains("--level-of-detail-regression", StringComparer.Ordinal)) return LevelOfDetailRegression.Run(args);
        if (args.Contains("--visible-clipping-regression", StringComparer.Ordinal)) return VisibleClippingRegression.Run(args);
        if (args.Contains("--layer-cache-regression", StringComparer.Ordinal)) return LayerCacheRegression.Run(args);
        if (args.Contains("--memory-regression", StringComparer.Ordinal)) return MemoryRegression.Run(args);
        BenchmarkOptions? options = null;
        BenchmarkReport? report = null;
        try
        {
            options = BenchmarkOptions.Parse(args);
            report = new BenchmarkReport(options);
            WriteReport(options.OutputPath, report);
            AppBuilder.Configure<BenchmarkApplication>()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions())
                .SetupWithoutStarting();
            var runner = new BenchmarkRunner(options);
            foreach (var definition in DatasetFactory.Definitions.Where(item => options.Scenarios.Contains(item.Id)))
            {
                var result = runner.Run(definition);
                report.Scenarios.Add(result);
                Console.WriteLine($"{definition.Id}: {result.Status}; {result.Samples.Count} recorded samples");
                WriteReport(options.OutputPath, report);
                if (result.Status != "completed-prototype")
                    break;
            }
            report.ProducerPassed = report.Scenarios.Count == options.Scenarios.Count &&
                report.Scenarios.All(item => item.Status == "completed-prototype");
            report.Status = report.ProducerPassed ? "completed-prototype" : "failed";
            report.FinishedUtc = DateTimeOffset.UtcNow;
            WriteReport(options.OutputPath, report);
            return report.ProducerPassed ? 0 : 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            if (options is not null)
            {
                report ??= new BenchmarkReport(options);
                report.Status = "failed";
                report.ProducerPassed = false;
                report.Error = exception.ToString();
                report.FinishedUtc = DateTimeOffset.UtcNow;
                WriteReport(options.OutputPath, report);
            }
            return 1;
        }
    }

    internal static void WriteReport<T>(string path, T report)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = fullPath + ".partial";
        File.WriteAllText(temporary, JsonSerializer.Serialize(report, JsonOptions));
        File.Move(temporary, fullPath, overwrite: true);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
}

public sealed class BenchmarkApplication : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new ChartsTheme());
    }
}

internal sealed record BenchmarkOptions(string OutputPath, string RunId, string Phase, int Samples,
    int Warmups, int HitQueries, int SceneTimeoutSeconds, bool Quick, HashSet<string> Scenarios)
{
    public static BenchmarkOptions Parse(string[] arguments)
    {
        var quick = arguments.Contains("--quick", StringComparer.Ordinal);
        string? Read(string name)
        {
            var index = Array.IndexOf(arguments, name);
            if (index < 0) return null;
            if (index + 1 >= arguments.Length || arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"{name} requires a value.");
            return arguments[index + 1];
        }
        int ReadCount(string name, int fallback, int minimum, int maximum) =>
            Read(name) is { } value && int.TryParse(value, out var number) && number >= minimum && number <= maximum
                ? number : Read(name) is null ? fallback : throw new ArgumentException($"Invalid {name}.");
        var allowed = new HashSet<string>(["--quick", "--output", "--run-id", "--phase", "--samples", "--warmups", "--hit-queries", "--scene-timeout-seconds", "--scenarios"], StringComparer.Ordinal);
        for (var index = 0; index < arguments.Length; index++)
        {
            if (!allowed.Contains(arguments[index])) throw new ArgumentException($"Unknown option {arguments[index]}.");
            if (arguments[index] != "--quick") index++;
        }
        var scenarios = (Read("--scenarios")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ??
            DatasetFactory.Definitions.Select(item => item.Id).ToArray()).ToHashSet(StringComparer.Ordinal);
        if (scenarios.Count == 0 || scenarios.Any(id => DatasetFactory.Definitions.All(item => item.Id != id)))
            throw new ArgumentException("Unknown or empty scenario list.");
        var runId = Read("--run-id") ?? Guid.NewGuid().ToString("N");
        var output = Read("--output") ?? Path.Combine("artifacts", "charts-performance", runId, "raw.json");
        return new BenchmarkOptions(Path.GetFullPath(output), runId, Read("--phase") ?? "C0-before-C1",
            ReadCount("--samples", quick ? 2 : 10, 1, 1000), ReadCount("--warmups", quick ? 1 : 3, 0, 100),
            ReadCount("--hit-queries", quick ? 5 : 100, 1, 10000),
            ReadCount("--scene-timeout-seconds", quick ? 120 : 600, 1, 3600), quick, scenarios);
    }
}
