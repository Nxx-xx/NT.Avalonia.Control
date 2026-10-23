using System.Collections;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Benchmarks;

internal sealed class BenchmarkRunner(BenchmarkOptions options)
{
    public ScenarioReport Run(DatasetDefinition definition)
    {
        var report = new ScenarioReport(definition);
        var scenarioStart = Stopwatch.GetTimestamp();
        Window? window = null;
        try
        {
            var creation = Measure(() => DatasetFactory.Create(definition), out BenchmarkDataset dataset);
            report.Datasets.Add(dataset.Manifest);
            report.DatasetPreparation.Add(new(0, creation));
            var allValid = dataset.Points.SelectMany(points => points).Where(DatasetFactory.IsValid).ToArray();
            var chart = new CartesianChart
            {
                Width = 1200, Height = 650, ShowLegend = false, ShowGridLines = true, FontSize = 12,
                XAxis = new ChartAxis { Minimum = allValid.Min(point => point.X) - 1,
                    Maximum = allValid.Max(point => point.X) + (definition.RealtimePoc ? (options.Samples + options.Warmups) * 25 + 1 : 1), DesiredTickCount = 6 },
                YAxis = new ChartAxis { Minimum = Math.Min(0, allValid.Min(point => point.Y)) - 1,
                    Maximum = allValid.Max(point => point.Y) + 1, DesiredTickCount = 6 },
            };
            for (var index = 0; index < definition.SeriesCount; index++)
            {
                ChartSeries series = definition.Kind switch
                {
                    "scatter" => new ScatterSeries(),
                    "bar" => new BarSeries(),
                    _ => new LineSeries(),
                };
                series.MarkerSize = definition.Kind == "scatter" ? 4 : 0;
                series.StrokeThickness = 2;
                chart.Series.Add(series);
            }
            window = new Window { Width = 1200, Height = 650, Content = chart };
            window.Show();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            for (var index = 0; index < options.Warmups + options.Samples; index++)
            {
                CheckDeadline(scenarioStart);
                if (definition.RealtimePoc && index > 0)
                {
                    creation = Measure(() => DatasetFactory.Create(definition, index), out dataset);
                    report.Datasets.Add(dataset.Manifest);
                    report.DatasetPreparation.Add(new(index, creation));
                }
                report.Samples.Add(RunSample(chart, dataset, index, index < options.Warmups));
                CheckDeadline(scenarioStart);
            }
            report.Summary = Summarize(report.Samples.Where(sample => !sample.IsWarmup).ToArray());
            report.Status = "completed-prototype";
        }
        catch (Exception exception)
        {
            report.Status = exception is TimeoutException ? "timed-out" : "failed";
            report.Error = exception.ToString();
        }
        finally { window?.Close(); }
        return report;
    }

    private BenchmarkSample RunSample(CartesianChart chart, BenchmarkDataset dataset, int sampleIndex, bool warmup)
    {
        var memoryBefore = MemorySample.Capture();
        var firstAllocated = GC.GetAllocatedBytesForCurrentThread();
        var firstStart = Stopwatch.GetTimestamp();
        // Explicitly a host materialization stage; runtime internal snapshotting belongs to scenePrepare.
        var snapshotTiming = Measure(() => dataset.Points.Select(points => points.ToArray()).ToArray(), out ChartPoint[][] snapshots);
        var sources = snapshots.Select(points => new InstrumentedSource(points)).ToArray();
        var applyTiming = Measure(() =>
        {
            for (var index = 0; index < sources.Length; index++) chart.Series[index].ItemsSource = sources[index];
        });
        Rect plot = default;
        ChartViewport visible = default;
        var prepareTiming = Measure(() =>
        {
            plot = chart.PlotArea;
            visible = chart.VisibleRange ?? throw new InvalidOperationException("No visible range after layout.");
            if (!visible.IsValid || plot.Width <= 0 || plot.Height <= 0) throw new InvalidOperationException("Invalid prepared scene.");
        });
        var cachedTiming = Measure(() => { _ = chart.PlotArea; _ = chart.VisibleRange; });
        var group = new DrawingGroup();
        var recordTiming = Measure(() =>
        {
            using var context = group.Open();
            chart.Render(context);
        });
        var queries = new QueryReceipt[options.HitQueries];
        StageTiming? firstUsable = null;
        for (var queryIndex = 0; queryIndex < queries.Length; queryIndex++)
        {
            var probeSeriesIndex = queryIndex % snapshots.Length;
            var points = snapshots[probeSeriesIndex];
            var probeRawIndex = Math.Clamp((int)((queryIndex + 1L) * points.Length / (queries.Length + 1)), 0, points.Length - 1);
            while (!DatasetFactory.IsValid(points[probeRawIndex])) probeRawIndex = (probeRawIndex + 1) % points.Length;
            var probe = points[probeRawIndex];
            var position = new Point(plot.Left + (probe.X - visible.XMinimum) / (visible.XMaximum - visible.XMinimum) * plot.Width,
                plot.Bottom - (probe.Y - visible.YMinimum) / (visible.YMaximum - visible.YMinimum) * plot.Height);
            ChartPointHit? hit = null;
            var timing = Measure(() => hit = chart.HitTestPoint(position));
            if (queryIndex == 0) firstUsable = new StageTiming(firstStart, Stopwatch.GetTimestamp(), GC.GetAllocatedBytesForCurrentThread() - firstAllocated);
            var hitSeriesIndex = hit is { } found ? chart.Series.IndexOf(found.Series) : -1;
            var validHit = hit is { } actual && hitSeriesIndex >= 0 && actual.Index >= 0 && actual.Index < snapshots[hitSeriesIndex].Length;
            var oracle = validHit ? snapshots[hitSeriesIndex][hit!.Index] : (ChartPoint?)null;
            var mappingMatches = validHit && oracle == hit!.Point;
            // Dense grouped columns overlap; a source-index oracle is valid without pretending every chosen column wins that overlap.
            var strictNearest = dataset.Definition.Kind != "bar";
            var matched = mappingMatches && (!strictNearest || (hitSeriesIndex == probeSeriesIndex && hit!.Index == probeRawIndex));
            queries[queryIndex] = new QueryReceipt(queryIndex, position.X, position.Y, probeSeriesIndex, probeRawIndex,
                DatasetFactory.Bits(probe.X), DatasetFactory.Bits(probe.Y), strictNearest ? "exact-probe-nearest-and-source-index" : "source-index-consistency-with-overlapping-columns",
                strictNearest ? probeSeriesIndex : null, strictNearest ? probeRawIndex : null,
                validHit ? hitSeriesIndex : null, validHit ? hit!.Index : null,
                validHit ? DatasetFactory.Bits(hit!.Point.X) : null, validHit ? DatasetFactory.Bits(hit!.Point.Y) : null,
                oracle is { } value ? DatasetFactory.Bits(value.X) : null, oracle is { } second ? DatasetFactory.Bits(second.Y) : null, matched, timing);
            if (!matched) throw new InvalidOperationException($"Hit oracle failed for {dataset.Definition.Id}, probe {probeSeriesIndex}:{probeRawIndex}, hit {hitSeriesIndex}:{hit?.Index}.");
        }
        var primitives = Flatten(group).Where(drawing => drawing is not DrawingGroup).ToArray();
        var finite = primitives.OfType<GeometryDrawing>().All(drawing =>
        {
            var bounds = drawing.GetBounds();
            return double.IsFinite(bounds.X) && double.IsFinite(bounds.Y) && double.IsFinite(bounds.Width) && double.IsFinite(bounds.Height);
        });
        if (!finite || primitives.Length == 0) throw new InvalidOperationException("Recorded geometry is missing or nonfinite.");
        return new BenchmarkSample(sampleIndex, warmup, dataset.Manifest.Revision, dataset.Manifest.Sha256, dataset.Manifest.InputPointCount,
            snapshotTiming, applyTiming, prepareTiming, cachedTiming, recordTiming, firstUsable!,
            sources.Select((source, index) => new SourceEnumeration(index, source.EnumerationCount, source.CompletedCount, source.VisitedPointCount, source.ElapsedTicks)).ToArray(),
            primitives.Length, finite, [plot.X, plot.Y, plot.Width, plot.Height],
            [visible.XMinimum, visible.XMaximum, visible.YMinimum, visible.YMaximum], memoryBefore, MemorySample.Capture(), queries);
    }

    private void CheckDeadline(long start)
    {
        if ((Stopwatch.GetTimestamp() - start) / (double)Stopwatch.Frequency > options.SceneTimeoutSeconds)
            throw new TimeoutException($"Scenario exceeded cooperative limit {options.SceneTimeoutSeconds}s. A process supervisor is required for blocking operations.");
    }

    private static Dictionary<string, MetricSummary> Summarize(BenchmarkSample[] samples)
    {
        Dictionary<string, double[]> values = new()
        {
            ["harnessSourceSnapshot"] = samples.Select(sample => sample.HarnessSourceSnapshot.ElapsedMilliseconds).ToArray(),
            ["sourceApply"] = samples.Select(sample => sample.SourceApply.ElapsedMilliseconds).ToArray(),
            ["scenePrepare"] = samples.Select(sample => sample.ScenePrepare.ElapsedMilliseconds).ToArray(),
            ["cachedSceneGetter"] = samples.Select(sample => sample.CachedSceneGetter.ElapsedMilliseconds).ToArray(),
            ["renderRecord"] = samples.Select(sample => sample.RenderRecord.ElapsedMilliseconds).ToArray(),
            ["firstUsableToRecord"] = samples.Select(sample => sample.FirstUsableToRecord.ElapsedMilliseconds).ToArray(),
            ["hitQuery"] = samples.SelectMany(sample => sample.Queries).Select(query => query.Timing.ElapsedMilliseconds).ToArray(),
        };
        return values.ToDictionary(pair => pair.Key, pair =>
        {
            var ordered = pair.Value.Order().ToArray();
            double Percentile(double fraction) => ordered[Math.Max(0, (int)Math.Ceiling(ordered.Length * fraction) - 1)];
            return new MetricSummary(ordered.Length, Percentile(0.5), Percentile(0.95), ordered[^1]);
        });
    }

    private static StageTiming Measure(Action action)
    {
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var start = Stopwatch.GetTimestamp();
        action();
        var end = Stopwatch.GetTimestamp();
        return new StageTiming(start, end, GC.GetAllocatedBytesForCurrentThread() - allocated);
    }
    private static StageTiming Measure<T>(Func<T> action, out T value)
    {
        T? result = default;
        var timing = Measure(() => { result = action(); });
        value = result!;
        return timing;
    }
    private static IEnumerable<Drawing> Flatten(Drawing drawing)
    {
        yield return drawing;
        if (drawing is DrawingGroup group)
            foreach (var child in group.Children)
                foreach (var descendant in Flatten(child)) yield return descendant;
    }

    private sealed class InstrumentedSource(ChartPoint[] points) : IEnumerable<ChartPoint>
    {
        public int EnumerationCount { get; private set; }
        public int CompletedCount { get; private set; }
        public long VisitedPointCount { get; private set; }
        public long ElapsedTicks { get; private set; }
        public IEnumerator<ChartPoint> GetEnumerator()
        {
            EnumerationCount++;
            return new Enumerator(this, points);
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        private sealed class Enumerator(InstrumentedSource owner, ChartPoint[] source) : IEnumerator<ChartPoint>
        {
            private readonly long _start = Stopwatch.GetTimestamp();
            private int _index = -1;
            private bool _completed;
            public ChartPoint Current => source[_index];
            object IEnumerator.Current => Current;
            public bool MoveNext()
            {
                if (++_index < source.Length) { owner.VisitedPointCount++; return true; }
                if (!_completed)
                {
                    owner.CompletedCount++;
                    owner.ElapsedTicks += Stopwatch.GetTimestamp() - _start;
                    _completed = true;
                }
                return false;
            }
            public void Reset() => throw new NotSupportedException();
            public void Dispose() { }
        }
    }
}
