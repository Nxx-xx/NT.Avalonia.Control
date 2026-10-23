using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;
using NT.AvaloniaControl.Charts.Themes;

namespace Charts.Benchmarks;

// Full collections here are deliberate diagnostic checkpoints, never benchmark timings.
internal static class MemoryRegression
{
    private const long MiB = 1024 * 1024;

    public static int Run(string[] args)
    {
        string Read(string key, string fallback)
        {
            var index = Array.IndexOf(args, key);
            return index < 0 ? fallback : index + 1 < args.Length ? args[index + 1] : throw new ArgumentException($"Missing {key} value.");
        }
        var output = Read("--output", "artifacts/charts-memory/raw.json");
        try
        {
            var cycles = int.Parse(Read("--cycles", "20"), System.Globalization.CultureInfo.InvariantCulture);
            var count = int.Parse(Read("--points", "1000000"), System.Globalization.CultureInfo.InvariantCulture);
            if (cycles is < 5 or > 100 || count is < 1 or > 2_000_000) throw new ArgumentOutOfRangeException(nameof(args));
            AppBuilder.Configure<BenchmarkApplication>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
            var series = new LineSeries { MarkerSize = 0 };
            var chart = new CartesianChart
            {
                Width = 1200, Height = 650, ShowLegend = false,
                XAxis = new NumericAxis { Minimum = 0, Maximum = count },
                YAxis = new NumericAxis { Minimum = -2, Maximum = 2 }
            };
            chart.Series.Add(series);
            var window = new Window { Width = 1200, Height = 650, Content = chart };
            window.Show(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
            // Initialize fonts, range and drawing infrastructure before measuring retained growth.
            Exercise(chart, series, 1000, -1);
            series.ItemsSource = null; Drain(window); Collect();
            var baseline = Capture();
            var receipts = new List<CycleReceipt>();
            var references = new List<(WeakReference Source, WeakReference Snapshot)>();
            for (var index = 0; index < cycles; index++)
            {
                references.Add(Exercise(chart, series, count, index));
                Drain(window); Collect();
                var memory = Capture();
                var obsolete = references.Take(references.Count - 1).ToArray();
                receipts.Add(new(index, memory, obsolete.Count(item => item.Source.IsAlive), obsolete.Count(item => item.Snapshot.IsAlive)));
            }
            // Keep the chart itself alive, proving release follows source clearing rather than chart GC.
            series.ItemsSource = null; Drain(window); Collect();
            var cleared = Capture();
            var sourceSurvivors = references.Count(item => item.Source.IsAlive);
            var snapshotSurvivors = references.Count(item => item.Snapshot.IsAlive);
            window.Content = null; window.Close(); Collect();
            var detached = Capture();
            var plateauGrowth = receipts[^1].Memory.ManagedBytes - receipts[0].Memory.ManagedBytes;
            var residual = cleared.ManagedBytes - baseline.ManagedBytes;
            var passed = receipts.All(item => item.ObsoleteSourcesAlive == 0 && item.ObsoleteSnapshotsAlive == 0) &&
                sourceSurvivors == 0 && snapshotSurvivors == 0 && plateauGrowth <= 16 * MiB && residual <= 16 * MiB;
            Program.WriteReport(output, new
            {
                schemaVersion = "charts-retention-v1", runId = Guid.NewGuid().ToString("N"),
                startedKind = "headless-full-data-retention-diagnostic", cycles, pointCount = count,
                gcPolicy = "forced-full-GC-after-each-cycle; not timing or native present evidence",
                runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                sources = new[] { FileRecord(typeof(CartesianChart).Assembly), FileRecord(typeof(ChartsTheme).Assembly), FileRecord(typeof(MemoryRegression).Assembly) },
                baseline, checkpoints = receipts, cleared, detached, plateauGrowthBytes = plateauGrowth,
                clearedResidualBytes = residual, sourceSurvivors, snapshotSurvivors,
                limits = new { plateauGrowthBytes = 16 * MiB, clearedResidualBytes = 16 * MiB }, passed,
                note = "Process private bytes include reserved runtime/native memory; only post-GC managed retention and obsolete-reference release are gated. No FPS, long-soak or Linux claim."
            });
            Console.WriteLine($"Retention: {(passed ? "passed" : "failed")}; plateau {plateauGrowth / (double)MiB:F2} MiB; residual {residual / (double)MiB:F2} MiB; surviving sources/snapshots {sourceSurvivors}/{snapshotSurvivors}");
            GC.KeepAlive(chart);
            return passed ? 0 : 1;
        }
        catch (Exception exception)
        {
            Program.WriteReport(output, new { schemaVersion = "charts-retention-v1", passed = false, error = exception.ToString() });
            Console.Error.WriteLine(exception); return 1;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Source, WeakReference Snapshot) Exercise(CartesianChart chart, ChartSeries series, int count, int cycle)
    {
        var source = new PointSource(count, cycle);
        series.ItemsSource = source;
        var plot = chart.PlotArea;
        var snapshot = series.CreateSnapshot();
        var index = count / 2;
        var point = snapshot.Points[index];
        if (point.SourceIndex != index || point.Point.X != index) throw new InvalidOperationException("Snapshot/source mapping changed.");
        var drawing = new DrawingGroup();
        using (var context = drawing.Open()) chart.Render(context);
        var range = chart.VisibleRange!.Value;
        var hit = chart.HitTestPoint(new Point(plot.Left + (point.Point.X - range.XMinimum) / (range.XMaximum - range.XMinimum) * plot.Width,
            plot.Bottom - (point.Point.Y - range.YMinimum) / (range.YMaximum - range.YMinimum) * plot.Height));
        if (hit?.Index != index || hit.Point != point.Point) throw new InvalidOperationException("Retention hit oracle failed.");
        return (new WeakReference(source), new WeakReference(snapshot));
    }

    private static void Drain(Window window)
    {
        Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
        // Source clearing invalidates the control, but draining dispatcher work alone does
        // not advance Headless's render clock. Retire the preceding retained drawing too.
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
        Dispatcher.UIThread.RunJobs();
    }
    private static void Collect()
    {
        // Finalizers may marshal work back to the Avalonia dispatcher. Keep it available.
        var collection = Task.Run(() => { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); });
        var started = Stopwatch.GetTimestamp();
        while (!collection.IsCompleted)
        {
            Dispatcher.UIThread.RunJobs();
            if (Stopwatch.GetElapsedTime(started).TotalSeconds > 30) throw new TimeoutException("Full-GC checkpoint exceeded 30 seconds.");
            Thread.Sleep(1);
        }
        collection.GetAwaiter().GetResult();
    }
    private static RetainedMemory Capture()
    {
        using var process = Process.GetCurrentProcess(); process.Refresh();
        var info = GC.GetGCMemoryInfo();
        return new(GC.GetTotalMemory(false), info.HeapSizeBytes, info.FragmentedBytes, info.TotalCommittedBytes, process.PrivateMemorySize64, process.WorkingSet64);
    }
    private static object FileRecord(Assembly assembly) => new { path = assembly.Location, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))) };
    private sealed record RetainedMemory(long ManagedBytes, long HeapBytes, long FragmentedBytes, long CommittedBytes, long PrivateBytes, long WorkingSetBytes);
    private sealed record CycleReceipt(int Cycle, RetainedMemory Memory, int ObsoleteSourcesAlive, int ObsoleteSnapshotsAlive);
    private sealed class PointSource(int count, int revision) : IEnumerable<ChartPoint>
    {
        public IEnumerator<ChartPoint> GetEnumerator()
        {
            for (var index = 0; index < count; index++) yield return new(index, Math.Sin(index * .001 + revision * .01));
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
