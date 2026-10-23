using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;
using NT.AvaloniaControl.Charts.Themes;

namespace Charts.Benchmarks;

// A mechanism regression: timings/allocations are diagnostic, never a latency SLA.
internal static class LayerCacheRegression
{
    private const int Warmups = 30;
    private const int Samples = 100;
    private static readonly MethodInfo UpdateReadings = typeof(CartesianChart).GetMethod("UpdateReadings", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly string[] LocalCounters = ["_preparedDataBuildCount", "_viewBuildCount", "_baseScaleBuildCount", "_axisTextBuildCount", "_seriesDrawingBuildCount", "_seriesDrawingReplayCount", "_segmentCacheBuildCount", "_interactionTextBuildCount", "_interactionTextReuseCount"];
    private static readonly string[] SharedCounters = ["ChartDataExtrema", "ChartLowerExtrema", "ChartDataIndex", "ChartReadingIndex", "ChartPointSceneCache.PointOrderBuildCount", "ChartPointSceneCache.HitIndexBuildCount", "ChartScale.LabelCopyCount"];
    private static readonly string[] StableHoverCounters = [.. LocalCounters.Where(key => key != "_seriesDrawingReplayCount" && !key.StartsWith("_interactionText",StringComparison.Ordinal)), .. SharedCounters, "sourceEnumerations", "sourceVisitedItems"];

    public static int Run(string[] args)
    {
        var outputIndex = Array.IndexOf(args, "--output");
        var output = outputIndex >= 0 && outputIndex + 1 < args.Length ? args[outputIndex + 1] : "artifacts/charts-layer-cache/raw.json";
        if (File.Exists(output)) { Console.Error.WriteLine("Layer-cache evidence already exists; use a new output path."); return 1; }
        var report = new LayerReport();
        try
        {
            AppBuilder.Configure<BenchmarkApplication>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
            foreach (var scenario in new[] { "line-10k", "line-1m", "scatter-10k", "shared-y-10k", "encoded-bubble-10k", "interpolate-x-1k", "category-1k", "stacked-2x1k", "range-band-1k" })
            {
                var result = Exercise(scenario);
                report.Scenarios.Add(result);
                Program.WriteReport(output, report);
                Console.WriteLine($"{scenario}: {(result.Passed ? "passed" : "failed")}; hover p95 {result.HoverTiming.P95Milliseconds:F4} ms; {result.HoverAllocation.P95Bytes} B");
            }
            report.Passed = report.Scenarios.Count == 9 && report.Scenarios.All(x => x.Passed);
        }
        catch (Exception exception)
        {
            report.Error = exception.ToString();
            report.Passed = false;
            Console.Error.WriteLine(exception);
        }
        report.FinishedUtc = DateTimeOffset.UtcNow;
        Program.WriteReport(output, report);
        return report.Passed ? 0 : 1;
    }

    private static ScenarioReceipt Exercise(string name)
    {
        var count = name == "line-1m" ? 1_000_000 : name.EndsWith("10k", StringComparison.Ordinal) ? 10_000 : 1_000;
        var chart = new CartesianChart { Width = 1200, Height = 650, ShowLegend = false, IsTooltipEnabled = true, ShowCrosshair = true };
        var sources = new List<ICountedSource>();
        var expected = new List<Func<int, ChartPoint>>();
        var shared = name is "category-1k" or "stacked-2x1k" or "range-band-1k";
        chart.TooltipMode = name == "shared-y-10k" ? ChartTooltipMode.SharedY : name == "interpolate-x-1k" ? ChartTooltipMode.InterpolateX : shared ? ChartTooltipMode.SharedX : ChartTooltipMode.NearestPoint;
        var seriesCount = name == "stacked-2x1k" ? 2 : 1;
        for (var seriesIndex = 0; seriesIndex < seriesCount; seriesIndex++)
        {
            var offset = seriesIndex;
            ChartPoint PointAt(int index) => new(index, name == "shared-y-10k" ? 1 + index / 1000d : 2 + offset + Math.Sin(index * .013 + offset) * .3);
            ChartSeries series = name switch
            {
                "scatter-10k" => new ScatterSeries { MarkerSize = 4 },
                "encoded-bubble-10k" => new BubbleSeries { MinimumSize = .01, MaximumSize = .03, StrokeThickness = 0, MarkerStrokeThickness = 0, DrawOrder = ChartPointDrawOrder.SizeDescending, SizePath = nameof(BubbleRow.Size), ColorPath = nameof(BubbleRow.Color) },
                "stacked-2x1k" => new AreaSeries { StackMode = ChartStackMode.Stacked, MarkerSize = 0 },
                "range-band-1k" => new RangeAreaSeries { MarkerSize = 0 },
                _ => new LineSeries { MarkerSize = 0 }
            };
            if (name == "encoded-bubble-10k")
            {
                var rows = Enumerable.Range(0,count).Select(index => new BubbleRow(index,index,PointAt(index).Y,1+index%97,index%31)).ToArray();
                var source = new CountedSource<BubbleRow>(rows,row => row with { Y = row.Y + .125 });
                series.CategoryPath = nameof(BubbleRow.X); series.ValuePath = nameof(BubbleRow.Y); series.IdPath = nameof(BubbleRow.Id);
                series.ItemsSource = source; sources.Add(source); expected.Add(index => new(rows[index].X,rows[index].Y));
            }
            else if (name is "category-1k" or "range-band-1k")
            {
                var rows = Enumerable.Range(0, count).Select(index => new MappedRow(index, "item-" + index, PointAt(index).Y, PointAt(index).Y - 1, PointAt(index).Y + 1)).ToArray();
                var source = new CountedSource<MappedRow>(rows, row => row with { Value = row.Value + .125, Low = row.Low + .125, High = row.High + .125 });
                series.CategoryPath = name == "category-1k" ? nameof(MappedRow.Category) : nameof(MappedRow.Id);
                series.ValuePath = nameof(MappedRow.Value); series.IdPath = nameof(MappedRow.Id);
                if (name == "range-band-1k") { series.LowPath = nameof(MappedRow.Low); series.HighPath = nameof(MappedRow.High); }
                series.ItemsSource = source; sources.Add(source);
                expected.Add(index => new ChartPoint(index, name == "range-band-1k" ? rows[index].High : rows[index].Value));
            }
            else
            {
                var points = Enumerable.Range(0, count).Select(PointAt).ToArray();
                var source = new CountedSource<ChartPoint>(points, point => new ChartPoint(point.X, point.Y + .125));
                series.ItemsSource = source; sources.Add(source); expected.Add(index => points[index]);
            }
            chart.Series.Add(series);
        }
        if (name == "category-1k") chart.XAxis = new CategoryAxis();
        using var lifetime = new WindowLifetime(new Window { Width = 1200, Height = 650, Content = chart });
        var window = lifetime.Window;
        window.Show(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
        _ = chart.PlotArea;
        Render(chart);
        Dispatcher.UIThread.RunJobs();
        var coldBefore = Counters(chart, sources);
        var cold = Sample(chart, expected, count, 0, shared, name == "shared-y-10k");
        var coldAfter = Counters(chart, sources);
        var warmupSamples = new List<HoverSample>(Warmups);
        for (var index = 0; index < Warmups; index++)
        {
            warmupSamples.Add(Sample(chart, expected, count, index, shared, name == "shared-y-10k"));
            Dispatcher.UIThread.RunJobs();
        }
        var before = Counters(chart, sources);
        var samples = new List<HoverSample>(Samples);
        for (var index = 0; index < Samples; index++)
        {
            samples.Add(Sample(chart, expected, count, index + Warmups, shared, name == "shared-y-10k"));
            Dispatcher.UIThread.RunJobs();
        }
        var after = Counters(chart, sources);
        var checks = new Dictionary<string, bool>(StringComparer.Ordinal);
        var delta = Difference(before, after);
        foreach (var key in StableHoverCounters) checks["warmHover:" + key + ":unchanged"] = delta[key] == 0;
        checks["warmHover:retainedDrawingReplayed"] = delta["_seriesDrawingReplayCount"] >= Samples;
        checks["warmHover:allSourceIdentities"] = samples.All(x => x.IdentityMatched);
        checks["warmup:allSourceIdentities"] = warmupSamples.All(x => x.IdentityMatched);
        checks["coldHover:sourceIdentity"] = cold.IdentityMatched;
        AnchorReceipt? anchor = null;
        if (name == "line-10k")
        {
            _ = Sample(chart,expected,count,0,shared,false);
            Dispatcher.UIThread.RunJobs();
            var anchorBefore = Counters(chart,sources);
            var anchorSamples = new List<HoverSample>(50);
            for (var index = 0; index < 50; index++)
            {
                anchorSamples.Add(Sample(chart,expected,count,0,shared,false,(index%5-2)*.00001));
                Dispatcher.UIThread.RunJobs();
            }
            var anchorAfter = Counters(chart,sources);var anchorDelta = Difference(anchorBefore,anchorAfter);
            foreach(var key in StableHoverCounters)checks["anchor:"+key+":unchanged"]=anchorDelta[key]==0;
            checks["anchor:sameIdentity"]=anchorSamples.All(s=>s.IdentityMatched && s.ExpectedIndex==anchorSamples[0].ExpectedIndex);
            checks["anchor:tooltipTextRetained"]=anchorDelta["_interactionTextBuildCount"]==0 && anchorDelta["_interactionTextReuseCount"]>=50;
            anchor=new(anchorBefore,anchorAfter,anchorDelta,anchorSamples);
        }
        var full = chart.VisibleRange ?? throw new InvalidOperationException("No visible range.");
        var viewBefore = Counters(chart, sources);
        chart.Viewport = new ChartViewport(count * .2, count * .8, full.YMinimum, full.YMaximum);
        _ = chart.PlotArea; Render(chart); Dispatcher.UIThread.RunJobs();
        var viewAfter = Counters(chart, sources);
        var viewDelta = Difference(viewBefore, viewAfter);
        foreach (var key in new[] { "_preparedDataBuildCount", "_baseScaleBuildCount", "ChartDataExtrema", "ChartScale.LabelCopyCount", "sourceEnumerations", "sourceVisitedItems" }) checks["viewport:" + key + ":retained"] = viewDelta[key] == 0;
        checks["viewport:viewRebuilt"] = viewDelta["_viewBuildCount"] > 0;
        checks["viewport:geometryRebuilt"] = viewDelta["_seriesDrawingBuildCount"] > 0;
        chart.Viewport = null; _ = chart.PlotArea; Render(chart); Dispatcher.UIThread.RunJobs();
        var dataBefore = Counters(chart, sources);
        sources[0].ChangeFirst();
        _ = chart.PlotArea; Render(chart); Dispatcher.UIThread.RunJobs();
        var dataAfter = Counters(chart, sources);
        var dataDelta = Difference(dataBefore, dataAfter);
        checks["data:sourceReenumerated"] = dataDelta["sourceEnumerations"] > 0 && dataDelta["sourceVisitedItems"] >= count;
        checks["data:preparationRebuilt"] = dataDelta["_preparedDataBuildCount"] > 0;
        checks["data:viewRebuilt"] = dataDelta["_viewBuildCount"] > 0;
        checks["data:geometryRebuilt"] = dataDelta["_seriesDrawingBuildCount"] > 0;
        return new(name, count, seriesCount, chart.TooltipMode.ToString(), cold, Difference(coldBefore, coldAfter), before, after, delta, viewDelta, dataDelta,
            warmupSamples, samples, anchor, SummarizeTiming(samples), SummarizeAllocation(samples), checks, checks.Values.All(x => x));
    }

    private static HoverSample Sample(CartesianChart chart, IReadOnlyList<Func<int, ChartPoint>> expected, int count, int sequence, bool shared, bool sharedY,double anchorOffset=0)
    {
        var index = (int)((sequence * 7919L + 101) % (count - 2)) + 1;
        var point = expected[0](index);
        var plot = chart.PlotArea; var range = chart.VisibleRange ?? throw new InvalidOperationException("Missing range.");
        var position = new Point(plot.Left + (point.X - range.XMinimum) / (range.XMaximum - range.XMinimum) * plot.Width+anchorOffset,
            plot.Bottom - (point.Y - range.YMinimum) / (range.YMaximum - range.YMinimum) * plot.Height+anchorOffset);
        var allocated = GC.GetAllocatedBytesForCurrentThread(); var start = Stopwatch.GetTimestamp();
        UpdateReadings.Invoke(chart, [position]);
        Render(chart);
        var end = Stopwatch.GetTimestamp(); var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        var hits = chart.HoveredPoints;
        var actual = new List<HitReceipt>(hits.Count);
        var identity = hits.Count == expected.Count;
        foreach (var hit in hits)
        {
            var seriesIndex = chart.Series.IndexOf(hit.Series);
            identity &= seriesIndex >= 0 && hit.Index == index;
            if (seriesIndex >= 0)
            {
                var raw = hit.RawPoint ?? hit.Point;
                identity &= raw == expected[seriesIndex](index);
                if (hit.Item is MappedRow mapped) identity &= mapped.Id == index && Equals(hit.StableId, index);
                if (hit.Item is BubbleRow bubble) identity &= bubble.Id == index && Equals(hit.StableId, index);
                actual.Add(new(seriesIndex,hit.Index,raw.X,raw.Y,hit.StableId?.ToString(),hit.Item is MappedRow row ? row.Id : (hit.Item as BubbleRow)?.Id));
            }
        }
        return new(sequence,index,position.X,position.Y,actual,identity,start,end,bytes,(end-start)*1000d/Stopwatch.Frequency);
    }

    private static void Render(CartesianChart chart)
    {
        var group = new DrawingGroup();
        using (var drawing = group.Open()) chart.Render(drawing);
        if (group.Children.Count == 0) throw new InvalidOperationException("No recorded chart drawing.");
        group.Children.Clear();
    }
    private static Dictionary<string,long> Counters(CartesianChart chart, IEnumerable<ICountedSource> sources)
    {
        var result=new Dictionary<string,long>(StringComparer.Ordinal);
        foreach(var key in LocalCounters)
        {
            var field=typeof(CartesianChart).GetField(key,BindingFlags.Instance|BindingFlags.NonPublic) ?? throw new MissingFieldException(key);
            result.Add(key,Convert.ToInt64(field.GetValue(chart),System.Globalization.CultureInfo.InvariantCulture));
        }
        foreach(var key in SharedCounters)
        {
            var parts=key.Split('.');var typeName=parts[0];var memberName=parts.Length>1?parts[1]:"BuildCount";
            var type=typeof(CartesianChart).Assembly.GetType("NT.AvaloniaControl.Charts."+typeName) ?? throw new TypeLoadException(key);
            var field=type.GetField(memberName,BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
            var property=type.GetProperty(memberName,BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
            result.Add(key,Convert.ToInt64(field is not null?field.GetValue(null):property?.GetValue(null) ?? throw new MissingFieldException(key+".BuildCount"),System.Globalization.CultureInfo.InvariantCulture));
        }
        result.Add("sourceEnumerations",sources.Sum(s=>s.Enumerations));result.Add("sourceVisitedItems",sources.Sum(s=>s.VisitedItems));
        return result;
    }
    private static Dictionary<string,long> Difference(IReadOnlyDictionary<string,long> before,IReadOnlyDictionary<string,long> after)=>before.ToDictionary(x=>x.Key,x=>after[x.Key]-x.Value,StringComparer.Ordinal);
    private static TimingSummary SummarizeTiming(IReadOnlyList<HoverSample> samples)
    {
        var ordered=samples.Select(x=>x.Milliseconds).Order().ToArray();
        return new(ordered.Length,ordered[(int)Math.Ceiling(ordered.Length*.5)-1],ordered[(int)Math.Ceiling(ordered.Length*.95)-1],ordered[^1]);
    }
    private static AllocationSummary SummarizeAllocation(IReadOnlyList<HoverSample> samples)
    {
        var ordered=samples.Select(x=>x.AllocatedBytes).Order().ToArray();
        return new(ordered.Length,ordered[(int)Math.Ceiling(ordered.Length*.5)-1],ordered[(int)Math.Ceiling(ordered.Length*.95)-1],ordered[^1]);
    }
    private static object AssemblyRecord(Assembly assembly)=>new { path=assembly.Location,sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))) };
    private interface ICountedSource { long Enumerations { get; } long VisitedItems { get; } void ChangeFirst(); }
    private sealed class CountedSource<T>(T[] values,Func<T,T> change):IEnumerable<T>,INotifyCollectionChanged,ICountedSource
    {
        public long Enumerations { get; private set; }
        public long VisitedItems { get; private set; }
        public event NotifyCollectionChangedEventHandler? CollectionChanged;
        public IEnumerator<T> GetEnumerator()
        {
            Enumerations++;
            foreach(var item in values){VisitedItems++;yield return item;}
        }
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
        public void ChangeFirst(){values[0]=change(values[0]);CollectionChanged?.Invoke(this,new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));}
    }
    private sealed record MappedRow(int Id,string Category,double Value,double Low,double High);
    private sealed record BubbleRow(int Id,double X,double Y,double Size,double Color);
    private sealed class WindowLifetime(Window window):IDisposable
    {
        public Window Window { get; }=window;
        public void Dispose(){Window.Content=null;Window.Close();Dispatcher.UIThread.RunJobs();}
    }
    private sealed class LayerReport
    {
        public string SchemaVersion { get; }="charts-layer-cache-v1";
        public DateTimeOffset StartedUtc { get; }=DateTimeOffset.UtcNow;
        public DateTimeOffset? FinishedUtc { get; set; }
        public bool Passed { get; set; }
        public string? Error { get; set; }
        public int WarmupsPerScenario { get; }=Warmups;
        public int RecordedHoverSamplesPerScenario { get; }=Samples;
        public long StopwatchFrequency { get; }=Stopwatch.Frequency;
        public string Runtime { get; }=RuntimeInformation.FrameworkDescription;
        public string Os { get; }=RuntimeInformation.OSDescription;
        public object[] Assemblies { get; }=[AssemblyRecord(typeof(CartesianChart).Assembly),AssemblyRecord(typeof(ChartsTheme).Assembly),AssemblyRecord(typeof(LayerCacheRegression).Assembly)];
        public List<ScenarioReceipt> Scenarios { get; }=[];
        public bool PerformanceMeasured { get; }=true;
        public bool FormalSlaEvaluated { get; }=false;
        public bool MemoryLongStabilityQualified { get; }=false;
        public bool ActualPresentMeasured { get; }=false;
        public string Scope { get; }="Attached CartesianChart; programmatic private UpdateReadings plus DrawingGroup Render recording, then dispose context/clear host drawing. Counter gates only. Diagnostic timing includes reflection invocation, tooltip formatting and host drawing recording; retained commands replay remains proportional to draw operations. No physical input, compositor present, FPS or formal p95 qualification.";
    }
    private sealed record HitReceipt(int SeriesIndex,int Index,double RawX,double RawY,string? StableId,int? MappedItemId);
    private sealed record HoverSample(int Sequence,int ExpectedIndex,double X,double Y,List<HitReceipt> ActualHits,bool IdentityMatched,long StartTicks,long EndTicks,long AllocatedBytes,double Milliseconds);
    private sealed record TimingSummary(int Count,double MedianMilliseconds,double P95Milliseconds,double MaximumMilliseconds);
    private sealed record AllocationSummary(int Count,long MedianBytes,long P95Bytes,long MaximumBytes);
    private sealed record AnchorReceipt(Dictionary<string,long> Before,Dictionary<string,long> After,Dictionary<string,long> Delta,List<HoverSample> Samples);
    private sealed record ScenarioReceipt(string Id,int PointCount,int SeriesCount,string TooltipMode,HoverSample ColdHover,Dictionary<string,long> ColdHoverDelta,Dictionary<string,long> BeforeHover,Dictionary<string,long> AfterHover,Dictionary<string,long> HoverDelta,Dictionary<string,long> ViewportDelta,Dictionary<string,long> DataDelta,List<HoverSample> WarmupSamples,List<HoverSample> Samples,AnchorReceipt? Anchor,TimingSummary HoverTiming,AllocationSummary HoverAllocation,Dictionary<string,bool> Checks,bool Passed);
}
