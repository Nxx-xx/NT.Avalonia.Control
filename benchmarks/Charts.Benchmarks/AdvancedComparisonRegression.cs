using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using NT.AvaloniaControl.Charts;

namespace Charts.Benchmarks;

// Raw product observations. The separate Python oracle recomputes expectations from pinned inputs.
internal static class AdvancedComparisonRegression
{
    public static int Run(string[] args)
    {
        var at = Array.IndexOf(args, "--output");
        var output = Path.GetFullPath(at >= 0 && at + 1 < args.Length ? args[at + 1] : "artifacts/charts-advanced-comparison/raw.json");
        if (File.Exists(output)) { Console.Error.WriteLine("Choose a fresh evidence path."); return 1; }
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var report = new Report();
        try
        {
            foreach (var sort in Enum.GetValues<ComparisonSort>())
            {
                Diverging(report,"diverging-"+sort,[[-6],[3],[9],[3],[0]],sort);
                Paired(report,"paired-"+sort,[[2,8],[9,3],[5,5],[-3,2]],false,sort);
                Paired(report,"mirror-"+sort,[[6,4],[3,8],[9,5],[3,8]],true,sort);
            }
            foreach (var (name,rows) in new (string,double[][])[] { ("empty",[]),("zero",[[0],[0]]),("positive",[[3],[9]]),("negative",[[-3],[-9]]),("large",[[-1e308],[1e308]]) })
                Diverging(report,"diverging-"+name,rows,ComparisonSort.Source);
            foreach(var (name,rows,mirror) in new (string,double[][],bool)[] { ("empty",[],false),("zero",[[0,0]],true),("large",[[-1e308,1e308]],false) })
                Paired(report,"paired-"+name,rows,mirror,ComparisonSort.Source);
            foreach(var (name,rows) in new (string,double[][])[] { ("basic",[[2,2,3],[3,4,1],[5,1,1]]),("zero-width",[[0,1,2],[2,3,0]]),("zero-height",[[1,0,0],[1,1,1]]),("empty",[]),("large",[[1e308,1e308,1e308],[1e308,1e308,0]]) })
                Mekko(report,"mekko-"+name,rows);
            foreach(var (name,rows,count) in new (string,double[][],int)[] {
                ("two-overlap",[[1,60],[2,40],[3,20]],2),("two-disjoint",[[1,60],[2,40]],2),
                ("two-contained",[[1,60],[2,40],[3,40]],2),("two-equal",[[1,40],[2,40],[3,40]],2),
                ("two-zero",[[1,0],[2,0],[3,0]],2),("three-equal",[[1,100],[2,100],[4,100],[3,30],[5,30],[6,30],[7,10]],3),
                ("three-disjoint",[[1,30],[2,20],[4,10]],3) })
                Add(report,"venn-"+name,"venn",rows,new{setCount=count},()=>ChartAdvancedComparison.Venn(VennRows(rows),count));
            Invalid(report,"diverging-nonfinite","ArgumentOutOfRangeException",()=>ChartAdvancedComparison.Diverging([new(double.NaN)]));
            Invalid(report,"diverging-sort","ArgumentOutOfRangeException",()=>ChartAdvancedComparison.Diverging([], (ComparisonSort)99));
            Invalid(report,"paired-negative-mirror","ArgumentOutOfRangeException",()=>ChartAdvancedComparison.Paired([new(-1,2)],true));
            Invalid(report,"paired-nonfinite","ArgumentOutOfRangeException",()=>ChartAdvancedComparison.Paired([new(1,double.PositiveInfinity)]));
            Invalid(report,"mekko-negative-width","ArgumentOutOfRangeException",()=>ChartAdvancedComparison.Mekko([new(-1,[new(1)])]));
            Invalid(report,"mekko-negative-segment","ArgumentOutOfRangeException",()=>ChartAdvancedComparison.Mekko([new(1,[new(-1)])]));
            Invalid(report,"venn-missing-singleton","ArgumentException",()=>ChartAdvancedComparison.Venn([new(1,3)],2));
            Invalid(report,"venn-duplicate-mask","ArgumentException",()=>ChartAdvancedComparison.Venn([new(1,3),new(2,3),new(1,1)],2));
            Invalid(report,"venn-inconsistent","ArgumentException",()=>ChartAdvancedComparison.Venn([new(1,3),new(2,3),new(3,4)],2));
            Invalid(report,"venn-negative","ArgumentOutOfRangeException",()=>ChartAdvancedComparison.Venn([new(1,-1),new(2,3)],2));
            Invalid(report,"venn-count","ArgumentOutOfRangeException",()=>ChartAdvancedComparison.Venn([],4));
            Invalid(report,"venn-mask","ArgumentOutOfRangeException",()=>ChartAdvancedComparison.Venn([new(1,3),new(2,3),new(4,1)],2));
            Invalid(report,"diverging-cancelled","OperationCanceledException",()=>ChartAdvancedComparison.Diverging([],cancellationToken:new(true)));
            Invalid(report,"paired-cancelled","OperationCanceledException",()=>ChartAdvancedComparison.Paired([],cancellationToken:new(true)));
            Invalid(report,"mekko-cancelled","OperationCanceledException",()=>ChartAdvancedComparison.Mekko([],cancellationToken:new(true)));
            Invalid(report,"venn-cancelled","OperationCanceledException",()=>ChartAdvancedComparison.Venn([],cancellationToken:new(true)));
            var largeDiverging=Enumerable.Range(0,100_000).Select(i=>new[]{(double)((i*37)%997)-498}).ToArray();
            var divergingValues=DivergingRows(largeDiverging).ToArray();
            Performance(report,"perf-diverging-100k","diverging",largeDiverging,"Value=((i*37)%997)-498",new{sort="Source"},()=>ChartAdvancedComparison.Diverging(divergingValues));
            var largeMekko=Enumerable.Range(0,100_000).Select(i=>new[]{1d+i%5,1d+i%7,2d+i%11}).ToArray();
            var mekkoValues=MekkoRows(largeMekko).ToArray();
            Performance(report,"perf-mekko-100k","mekko",largeMekko,"Width=1+i%5;Segments=[1+i%7,2+i%11]",new{},()=>ChartAdvancedComparison.Mekko(mekkoValues));
            report.Passed=report.Cases.Count==45 && report.Cases.All(c=>c.Kind=="invalid"?c.ExceptionType is not null:c.ExceptionType is null) && report.PerformanceCases.Count==2 && report.PerformanceCases.All(c=>c.ExceptionType is null);
        }
        catch(Exception e){report.Error=e.ToString();Console.Error.WriteLine(e);}
        report.FinishedUtc=DateTimeOffset.UtcNow;
        using var stream=new FileStream(output,FileMode.CreateNew,FileAccess.Write,FileShare.Read);
        JsonSerializer.Serialize(stream,report,new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true,NumberHandling=JsonNumberHandling.AllowNamedFloatingPointLiterals});
        return report.Passed?0:1;
    }
    private static IEnumerable<DivergingValue> DivergingRows(double[][] rows)=>rows.Select((r,i)=>new DivergingValue(r[0],$"row-{i}"));
    private static IEnumerable<PairedComparisonValue> PairedRows(double[][] rows)=>rows.Select((r,i)=>new PairedComparisonValue(r[0],r[1],$"row-{i}"));
    private static IEnumerable<MekkoColumn> MekkoRows(double[][] rows)=>rows.Select((r,i)=>new MekkoColumn(r[0],r.Skip(1).Select((v,j)=>new MekkoSegment(v,$"segment-{j}")),$"row-{i}"));
    private static IEnumerable<VennItem> VennRows(double[][] rows)=>rows.Select((r,i)=>new VennItem((int)r[0],r[1],$"row-{i}"));
    private static void Diverging(Report report,string id,double[][] rows,ComparisonSort sort)=>Add(report,id,"diverging",rows,new{sort=sort.ToString()},()=>ChartAdvancedComparison.Diverging(DivergingRows(rows),sort));
    private static void Paired(Report report,string id,double[][] rows,bool mirror,ComparisonSort sort)=>Add(report,id,"paired",rows,new{mirror,sort=sort.ToString()},()=>ChartAdvancedComparison.Paired(PairedRows(rows),mirror,sort));
    private static void Mekko(Report report,string id,double[][] rows)=>Add(report,id,"mekko",rows,new{},()=>ChartAdvancedComparison.Mekko(MekkoRows(rows)));
    private static Observation Observe(string id,string kind,double[][] rows,object parameters,Func<object> action)
    {
        var value=new Observation{Id=id,Kind=kind,Input=rows,Parameters=parameters};var started=Stopwatch.GetTimestamp();var allocated=GC.GetAllocatedBytesForCurrentThread();
        try{value.Actual=action();}catch(Exception e){value.ExceptionType=e.GetType().Name;value.ExceptionMessage=e.Message;}
        value.ElapsedTicks=Stopwatch.GetTimestamp()-started;value.ThreadAllocatedBytes=GC.GetAllocatedBytesForCurrentThread()-allocated;return value;
    }
    private static void Add(Report report,string id,string kind,double[][] rows,object parameters,Func<object> action)=>report.Cases.Add(Observe(id,kind,rows,parameters,action));
    private static void Invalid(Report report,string id,string exception,Func<object> action)=>Add(report,"invalid-"+id,"invalid",[],new{expectedException=exception},action);
    private static void Performance(Report report,string id,string kind,double[][] rows,string formula,object parameters,Func<object> action)
    {
        var value=Observe(id,kind,[],parameters,action);value.InputFormula=formula;value.InputCount=rows.Length;
        using var bytes=new MemoryStream();using var writer=new BinaryWriter(bytes);foreach(var row in rows)foreach(var number in row)writer.Write(number);
        value.InputSha256=Convert.ToHexString(SHA256.HashData(bytes.GetBuffer().AsSpan(0,checked((int)bytes.Length))));report.PerformanceCases.Add(value);
    }
    private static object AssemblyRecord(Assembly assembly)=>new{path=assembly.Location,bytes=new FileInfo(assembly.Location).Length,sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))),assembly=assembly.FullName};
    private sealed class Report
    {
        public string SchemaVersion{get;}="charts-c505-advanced-comparison-observations-v1";
        public DateTimeOffset StartedUtc{get;}=DateTimeOffset.UtcNow;public DateTimeOffset? FinishedUtc{get;set;}public bool Passed{get;set;}public string? Error{get;set;}
        public long StopwatchFrequency{get;}=Stopwatch.Frequency;
        public object Environment{get;}=new{os=RuntimeInformation.OSDescription,architecture=RuntimeInformation.ProcessArchitecture.ToString(),runtime=RuntimeInformation.FrameworkDescription,processorCount=System.Environment.ProcessorCount};
        public object[] AssemblyManifest{get;}=[AssemblyRecord(typeof(ChartAdvancedComparison).Assembly),AssemblyRecord(typeof(AdvancedComparisonRegression).Assembly)];
        public bool FormalSlaEvaluated{get;}=false;public List<Observation> Cases{get;}=[];public List<Observation> PerformanceCases{get;}=[];
    }
    private sealed class Observation
    {
        public required string Id{get;init;}public required string Kind{get;init;}public required double[][] Input{get;init;}public required object Parameters{get;init;}
        public object? Actual{get;set;}public string? ExceptionType{get;set;}public string? ExceptionMessage{get;set;}public string? InputFormula{get;set;}public int? InputCount{get;set;}public string? InputSha256{get;set;}
        public long ElapsedTicks{get;set;}public long ThreadAllocatedBytes{get;set;}
    }
}
