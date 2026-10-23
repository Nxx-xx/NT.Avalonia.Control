using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Charts;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Charts.Benchmarks;

namespace Charts.Reference;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    internal static BenchmarkDataset Dataset { get; private set; } = null!;
    internal static string OutputPath { get; private set; } = string.Empty;
    internal static string RunId { get; private set; } = string.Empty;

    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            string? Read(string option)
            {
                var index = Array.IndexOf(args, option);
                return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
            }
            RunId = Guid.NewGuid().ToString("N");
            OutputPath = Path.GetFullPath(Read("--output") ?? Path.Combine("evidence", RunId, "reference.json"));
            if (File.Exists(OutputPath)) throw new InvalidOperationException("Reference evidence already exists.");
            var scenario = Read("--scenario") ?? "S10-L";
            var definition = DatasetFactory.Definitions.Single(item => item.Id == scenario);
            Dataset = DatasetFactory.Create(definition);
            if (args.Contains("--dataset-only", StringComparer.Ordinal))
            {
                WriteResult("dataset-ready", "Shared fixture generated; no licensed chart was initialized.");
                return 0;
            }

            // Presence only. Never store, print, serialize or pass the license value as an argument.
            if (Environment.GetEnvironmentVariable("AVALONIA_LICENSE_KEY") is null)
            {
                WriteResult("blocked-reference", "license-environment-not-present; chart initialization was not attempted");
                return 2;
            }
            WriteResult("pending-reference", "Entitlement and rendering are not yet verified.");
            if (args.Contains("--headless", StringComparer.Ordinal))
            {
                AppBuilder.Configure<ReferenceApplication>()
                    .UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
                var window = new Window { Width = 1200, Height = 650, Content = CreateChart() };
                try { window.Show(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
                finally { window.Close(); }
                WriteResult("initialized-headless-reference", "Layout completed; no native presentation timing or comparative qualification.");
                return 0;
            }
            return AppBuilder.Configure<ReferenceApplication>().UsePlatformDetect()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception exception)
        {
            // License/runtime exceptions are summarized by type, never by potentially sensitive text.
            if (OutputPath.Length > 0)
                WriteResult("blocked-reference", "initialization-or-package-error:" + exception.GetType().FullName);
            return 2;
        }
    }

    internal static CartesianChart CreateChart()
    {
        var valid = Dataset.Points.SelectMany(points => points).Where(DatasetFactory.IsValid).ToArray();
        var chart = new CartesianChart
        {
            Width = 1200, Height = 650, FontSize = 12, Padding = new Thickness(16),
            IsAnimationEnabled = false, ShowLegend = false, IsTooltipEnabled = false,
            IsZoomEnabled = false, IsPanEnabled = false, ShowRangeSelector = false,
            HorizontalAxis = new NumericalAxis { Minimum = valid.Min(point => point.X) - 1,
                Maximum = valid.Max(point => point.X) + 1, ShowGridLines = true },
            VerticalAxis = new NumericalAxis { Minimum = Math.Min(0, valid.Min(point => point.Y)) - 1,
                Maximum = valid.Max(point => point.Y) + 1, ShowGridLines = true },
            DataContext = new ReferenceViewModel(Dataset.Points),
        };
        for (var index = 0; index < Dataset.Points.Length; index++)
        {
            CartesianSeries series = Dataset.Definition.Kind switch
            {
                "bar" => new BarSeries(),
                "scatter" => new ScatterSeries(),
                _ => new LineSeries(),
            };
            series.Title = "Series " + (index + 1).ToString(CultureInfo.InvariantCulture);
            series.CategoryPath = "X";
            series.ValuePath = "Y";
            series.StrokeThickness = 2;
            series.ShowMarkers = Dataset.Definition.Kind == "scatter";
            series.MarkerSize = Dataset.Definition.Kind == "scatter" ? 4 : 0;
            series.EmptyPointMode = EmptyPointMode.Gap;
            series.Bind(ChartSeries.ItemsSourceProperty, new Binding($"Series[{index}]"));
            chart.Series.Add(series);
        }
        return chart;
    }

    internal static void WriteResult(string status, string reason)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath)!);
        var assemblyPath = typeof(CartesianChart).Assembly.Location;
        var result = new
        {
            schemaVersion = 1, runId = RunId, capturedUtc = DateTimeOffset.UtcNow, status, reason,
            packageId = "Avalonia.Controls.Charts", pinnedVersion = "12.3.0",
            referenceAssemblyIdentity = typeof(CartesianChart).Assembly.FullName,
            referenceAssemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assemblyPath))),
            fixture = Dataset.Manifest,
            quality = new { width = 1200, height = 650, fontSize = 12, strokeThickness = 2,
                markers = Dataset.Definition.Kind == "scatter", markerSize = Dataset.Manifest.MarkerSize,
                animation = false, legend = false, grid = true, emptyPointMode = "Gap",
                xLayout = "official-series-compatibility-rules", downsampling = "none-requested" },
            comparisonEligible = false,
            pending = new[] { "official-entitlement", "bound-point-count-verification", "visible-quality-golden",
                "bar-horizontal-axis-layout-equivalence", "shared-sample-loop", "native-present-timing",
                "windows-linux-reference-comparison", "realtime-fixed-extent-equivalence" },
        };
        File.WriteAllText(OutputPath, JsonSerializer.Serialize(result, JsonOptions));
        Console.WriteLine(status);
    }
}

internal sealed record ReferenceViewModel(NT.AvaloniaControl.Charts.ChartPoint[][] Series);

public sealed class ReferenceApplication : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new StyleInclude(new Uri("avares://Avalonia.Controls.Charts/"))
        {
            Source = new Uri("avares://Avalonia.Controls.Charts/Themes/Default.axaml"),
        });
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new Window { Width = 1200, Height = 650, Title = "Charts official reference 12.3.0",
                Content = Program.CreateChart() };
            window.Opened += (_, _) =>
                Program.WriteResult("initialized-native-reference", "Window opened; no native presentation timing or comparative qualification.");
            desktop.MainWindow = window;
        }
        base.OnFrameworkInitializationCompleted();
    }
}
