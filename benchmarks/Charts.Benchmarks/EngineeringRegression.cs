using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using NT.AvaloniaControl.Charts;

namespace Charts.Benchmarks;

// Product observations only. Independently pinned Python recomputes expectations from the source rows.
internal static class EngineeringRegression
{
    public static int Run(string[] args)
    {
        var at = Array.IndexOf(args, "--output");
        var output = Path.GetFullPath(at >= 0 && at + 1 < args.Length ? args[at + 1] : "artifacts/charts-engineering/raw.json");
        if (File.Exists(output)) { Console.Error.WriteLine("Choose a new evidence path."); return 1; }
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var report = new Report();
        try
        {
            foreach (var (id, r, x, z0, quantity) in new (string, double, double, double, SmithQuantity)[]
            {
                ("smith-short",0,0,50,SmithQuantity.Impedance), ("smith-matched",50,0,50,SmithQuantity.Impedance),
                ("smith-inductive",50,50,50,SmithQuantity.Impedance), ("smith-capacitive",25,-25,50,SmithQuantity.Impedance),
                ("smith-resistance",100,0,50,SmithQuantity.Impedance), ("smith-reference75",75,75,75,SmithQuantity.Impedance),
                ("smith-admittance",.02,.02,50,SmithQuantity.Admittance), ("smith-admittance-zero",0,0,50,SmithQuantity.Admittance)
            }) Add(report,id,"smith",[[r,x]],new { referenceImpedance=z0, quantity=quantity.ToString() },()=>Point(ChartEngineering.ProjectSmith(new(r,x),z0,quantity)));
            foreach (var quantity in Enum.GetValues<SmithQuantity>())
                foreach (var (suffix,x,y) in new[] { ("matched",0d,0d),("complex",.2,.4),("real",1d/3,0d) })
                    Add(report,$"smith-inverse-{quantity}-{suffix}","smith-inverse",[[x,y]],new { referenceImpedance=50d,quantity=quantity.ToString() },()=>Smith(ChartEngineering.UnprojectSmith(new(x,y),50,quantity)));
            foreach (var (id,a,b,c) in new[] { ("ternary-balanced",1d,1d,1d),("ternary-composition",1d,1d,2d),("ternary-vertex",1d,0d,0d),("ternary-large",1e308,1e308,1e308),("ternary-subnormal",1e-300,2e-300,1e-300) })
                Add(report,id,"ternary",[[a,b,c]],new { },()=>new { normalized=Ternary(ChartEngineering.NormalizeTernary(new(a,b,c))),projected=Point(ChartEngineering.ProjectTernary(new(a,b,c))) });
            foreach (var (id,x,y) in new[] { ("ternary-inverse-A",.5,Math.Sqrt(3)/2),("ternary-inverse-B",0d,0d),("ternary-inverse-C",1d,0d),("ternary-inverse-center",.5,Math.Sqrt(3)/6) })
                Add(report,id,"ternary-inverse",[[x,y]],new { },()=>Ternary(ChartEngineering.UnprojectTernary(new(x,y))));
            HexbinPoint[] hex=[new(0,0,2),new(.1,.2,4),new(2,0,8),new(-2,0,3),new(1,2,5),new(double.NaN,0,9),new(0,double.PositiveInfinity,7),new(0,0,double.NaN)];
            foreach (var aggregation in Enum.GetValues<HexbinAggregation>())
                Add(report,"hex-"+aggregation,"hexbin",Rows(hex),new {radius=1d,origin=new[]{0d,0d},aggregation=aggregation.ToString()},()=>Hex(ChartEngineering.Hexbin(hex,1,aggregation:aggregation)));
            Add(report,"hex-offset","hexbin",[[10d,20d,2d],[11d,22d,3d]],new{radius=2d,origin=new[]{10d,20d},aggregation="Count"},()=>Hex(ChartEngineering.Hexbin([new(10,20,2),new(11,22,3)],2,new(10,20))));
            Add(report,"hex-empty","hexbin",[],new{radius=1d,origin=new[]{0d,0d},aggregation="Count"},()=>Hex(ChartEngineering.Hexbin([],1)));
            WindObservation[] winds=[new(0,1,2),new(11.249,2,1),new(11.25,3,4),new(348.75,6,3),new(360,0,1),new(-90,4,2),new(90,6,5),new(double.NaN,2),new(45,-1),new(45,1,0),new(180,2,double.PositiveInfinity)];
            Add(report,"wind-boundaries","wind",Rows(winds),new{speedUpperBounds=new[]{3d,6d},sectorCount=16,angleUnit="Degrees"},()=>Wind(ChartEngineering.WindRose(winds,[3,6])));
            Add(report,"wind-radians","wind",[[0d,1d,2d],[Math.PI/2,4d,1d],[Math.PI,7d,3d],[-Math.PI/2,2d,4d]],new{speedUpperBounds=new[]{3d,6d},sectorCount=4,angleUnit="Radians"},()=>Wind(ChartEngineering.WindRose([new(0,1,2),new(Math.PI/2,4,1),new(Math.PI,7,3),new(-Math.PI/2,2,4)],[3,6],4,ChartAngleUnit.Radians)));
            Add(report,"wind-empty","wind",[],new{speedUpperBounds=new[]{3d},sectorCount=4,angleUnit="Degrees"},()=>Wind(ChartEngineering.WindRose([],[3],4)));
            string[] directions=["N","NNE","NE","ENE","E","ESE","SE","SSE","S","SSW","SW","WSW","W","WNW","NW","NNW"];
            Add(report,"wind-compass","wind-parse",[],new{directions},()=>new{degrees=directions.Select(d=>ChartEngineering.ParseWindDirection(d)).ToArray()});
            var carpet=(from b in new[]{0d,1d,2d} from a in new[]{0d,1d,2d} select new CarpetPoint(a,b,10+a+2*b+.5*a*b)).ToArray();
            Add(report,"carpet-grid","carpet-grid",Rows(carpet),new{offset=.5,invertible=true},()=>Carpet(ChartEngineering.CarpetGrid(carpet)));
            foreach(var (a,b) in new[]{(.25,.75),(1.5,1.25),(0d,0d),(2d,2d)})
            {
                var grid=ChartEngineering.CarpetGrid(carpet);var id=a.ToString(System.Globalization.CultureInfo.InvariantCulture)+"-"+b.ToString(System.Globalization.CultureInfo.InvariantCulture);
                Add(report,"carpet-interpolate-"+id,"carpet-interpolate",Rows(carpet),new{offset=.5,a,b},()=>CarpetPoint(ChartEngineering.InterpolateCarpet(grid,a,b)));
                var projected=new ChartPoint(a+.5*b,10+a+2*b+.5*a*b);
                Add(report,"carpet-inverse-"+id,"carpet-inverse",Rows(carpet),new{offset=.5,point=new[]{projected.X,projected.Y},expectedA=a,expectedB=b},()=>CarpetPoint(ChartEngineering.UnprojectCarpet(grid,projected)));
            }
            CarpetPoint[] folded=[new(0,0,0),new(1,0,1),new(0,1,1),new(1,1,0)];
            Add(report,"carpet-folded-display","carpet-grid",Rows(folded),new{offset=.5,invertible=false},()=>Carpet(ChartEngineering.CarpetGrid(folded)));
            Invalid(report,"invalid-smith-z0","ArgumentOutOfRangeException",()=>ChartEngineering.ProjectSmith(new(50,0),0));
            Invalid(report,"invalid-smith-negative","ArgumentOutOfRangeException",()=>ChartEngineering.ProjectSmith(new(-50,0)));
            Invalid(report,"invalid-smith-infinite","ArgumentOutOfRangeException",()=>ChartEngineering.ProjectSmith(new(50,double.NaN)));
            Invalid(report,"invalid-smith-unit-pole","ArgumentOutOfRangeException",()=>ChartEngineering.UnprojectSmith(new(1,0)));
            Invalid(report,"invalid-smith-outside","ArgumentOutOfRangeException",()=>ChartEngineering.UnprojectSmith(new(1.1,0)));
            Invalid(report,"invalid-ternary-zero","ArgumentOutOfRangeException",()=>ChartEngineering.ProjectTernary(new(0,0,0)));
            Invalid(report,"invalid-ternary-negative","ArgumentOutOfRangeException",()=>ChartEngineering.ProjectTernary(new(1,-1,1)));
            Invalid(report,"invalid-ternary-outside","ArgumentOutOfRangeException",()=>ChartEngineering.UnprojectTernary(new(2,2)));
            Invalid(report,"invalid-hex-radius","ArgumentOutOfRangeException",()=>ChartEngineering.Hexbin(hex,0));
            Invalid(report,"invalid-hex-aggregation","ArgumentOutOfRangeException",()=>ChartEngineering.Hexbin(hex,1,aggregation:(HexbinAggregation)99));
            Invalid(report,"invalid-hex-cancelled","OperationCanceledException",()=>ChartEngineering.Hexbin(hex,1,cancellationToken:new(true)));
            Invalid(report,"invalid-hex-input-budget","ArgumentOutOfRangeException",()=>ChartEngineering.Hexbin(Enumerable.Repeat(new HexbinPoint(0,0),1_000_001),1));
            Invalid(report,"invalid-wind-bounds","ArgumentException",()=>ChartEngineering.WindRose(winds,[6,3]));
            Invalid(report,"invalid-wind-sector","ArgumentOutOfRangeException",()=>ChartEngineering.WindRose(winds,[3],0));
            Invalid(report,"invalid-wind-compass","FormatException",()=>ChartEngineering.ParseWindDirection("north-by-nonsense"));
            Invalid(report,"invalid-wind-cancelled","OperationCanceledException",()=>ChartEngineering.WindRose(winds,[3],cancellationToken:new(true)));
            Invalid(report,"invalid-carpet-incomplete","ArgumentException",()=>ChartEngineering.CarpetGrid([new(0,0,0),new(1,0,1),new(0,1,2)]));
            Invalid(report,"invalid-carpet-duplicate","ArgumentException",()=>ChartEngineering.CarpetGrid([..carpet,carpet[0]]));
            Invalid(report,"invalid-carpet-outside","ArgumentOutOfRangeException",()=>ChartEngineering.InterpolateCarpet(ChartEngineering.CarpetGrid(carpet),3,1));
            Invalid(report,"invalid-carpet-folded-inverse","InvalidOperationException",()=>ChartEngineering.UnprojectCarpet(ChartEngineering.CarpetGrid(folded),new(.5,.5)));
            Invalid(report,"invalid-carpet-cancelled","OperationCanceledException",()=>ChartEngineering.CarpetGrid(carpet,cancellationToken:new(true)));
            var largeHex=Enumerable.Range(0,100_000).Select(i=>new HexbinPoint(((i*37)%997)/10d,((i*71)%991)/10d,1+(i%7)*.25)).ToArray();
            var largeWind=Enumerable.Range(0,100_000).Select(i=>new WindObservation((i*37)%720-180,((i*13)%100)/10d,1+(i%5)*.25)).ToArray();
            Performance(report,"perf-hexbin-100k","hexbin",Rows(largeHex),"X=((i*37)%997)/10;Y=((i*71)%991)/10;Value=1+(i%7)*0.25",new{radius=2d,origin=new[]{0d,0d},aggregation="Mean"},()=>ChartEngineering.Hexbin(largeHex,2,aggregation:HexbinAggregation.Mean),v=>Hex((HexbinResult)v));
            Performance(report,"perf-wind-100k","wind",Rows(largeWind),"Direction=(i*37)%720-180;Speed=((i*13)%100)/10;Frequency=1+(i%5)*0.25",new{speedUpperBounds=new[]{3d,6d},sectorCount=16,angleUnit="Degrees"},()=>ChartEngineering.WindRose(largeWind,[3,6]),v=>Wind((WindRoseResult)v));
            report.Passed=report.Cases.Count>=45 && report.Cases.All(c=>c.Kind=="invalid"?c.ExceptionType is not null:c.ExceptionType is null) && report.PerformanceCases.Count==2 && report.PerformanceCases.All(c=>c.ExceptionType is null);
        }
        catch(Exception e){report.Error=e.ToString();Console.Error.WriteLine(e);}
        report.FinishedUtc=DateTimeOffset.UtcNow;
        using var stream=new FileStream(output,FileMode.CreateNew,FileAccess.Write,FileShare.Read);
        JsonSerializer.Serialize(stream,report,new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true,NumberHandling=JsonNumberHandling.AllowNamedFloatingPointLiterals});
        return report.Passed?0:1;
    }

    private static object Point(ChartPoint p)=>new{x=p.X,y=p.Y};
    private static object Smith(SmithPoint p)=>new{r=p.R,x=p.X};
    private static object Ternary(TernaryPoint p)=>new{a=p.A,b=p.B,c=p.C};
    private static object CarpetPoint(CarpetPoint p)=>new{a=p.A,b=p.B,y=p.Y};
    private static double[][] Rows(IEnumerable<HexbinPoint> values)=>values.Select(p=>new[]{p.X,p.Y,p.Value}).ToArray();
    private static double[][] Rows(IEnumerable<WindObservation> values)=>values.Select(p=>new[]{p.Direction,p.Speed,p.Frequency}).ToArray();
    private static double[][] Rows(IEnumerable<CarpetPoint> values)=>values.Select(p=>new[]{p.A,p.B,p.Y}).ToArray();
    private static object Hex(HexbinResult r)=>new{r.Count,r.OmittedCount,r.Radius,Origin=Point(r.Origin),Aggregation=r.Aggregation.ToString(),Bins=r.Bins.Select(b=>new{b.Q,b.R,Center=Point(b.Center),Vertices=b.Vertices.Select(Point).ToArray(),b.SourceIndices,b.Count,b.Value}).ToArray()};
    private static object WindBin(WindRoseBin b)=>new{b.SectorIndex,b.SpeedBandIndex,b.LowerSpeed,b.UpperSpeed,b.SourceIndices,b.Count,b.Frequency,b.Percentage,b.StartFrequency,b.EndFrequency};
    private static object Wind(WindRoseResult r)=>new{r.Count,r.OmittedCount,r.TotalFrequency,r.SpeedUpperBounds,Bins=r.Bins.Select(WindBin).ToArray(),Sectors=r.Sectors.Select(s=>new{s.Index,s.DirectionDegrees,s.WidthDegrees,s.Label,Bins=s.Bins.Select(WindBin).ToArray(),s.Frequency,s.Count}).ToArray()};
    private static object Carpet(CarpetGridSnapshot r)=>new{Points=r.Points.Select(CarpetPoint).ToArray(),r.ALevels,r.BLevels,Paths=r.Paths.Select(p=>new{p.Label,Points=p.Points.Select(Point).ToArray()}).ToArray(),r.Offset,r.IsInvertible};
    private static Observation Observe(string id,string kind,double[][] input,object parameters,Func<object> calculate,Func<object,object>? projection=null)
    {
        var result=new Observation{Id=id,Kind=kind,Input=input,Parameters=parameters};var t=Stopwatch.GetTimestamp();var bytes=GC.GetAllocatedBytesForCurrentThread();
        try{result.Actual=calculate();}catch(Exception e){result.ExceptionType=e.GetType().Name;result.ExceptionMessage=e.Message;}
        result.ElapsedTicks=Stopwatch.GetTimestamp()-t;result.ThreadAllocatedBytes=GC.GetAllocatedBytesForCurrentThread()-bytes;
        if(result.ExceptionType is null && projection is not null)result.Actual=projection(result.Actual!);return result;
    }
    private static void Add(Report r,string id,string kind,double[][] input,object p,Func<object> f)=>r.Cases.Add(Observe(id,kind,input,p,f));
    private static void Invalid(Report r,string id,string exception,Func<object> f)=>Add(r,id,"invalid",[],new{expectedException=exception},f);
    private static void Performance(Report r,string id,string kind,double[][] input,string formula,object p,Func<object> f,Func<object,object> project)
    {
        var value=Observe(id,kind,[],p,f,project);value.InputFormula=formula;value.InputCount=input.Length;
        using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);foreach(var row in input)foreach(var number in row)writer.Write(number);
        value.InputSha256=Convert.ToHexString(SHA256.HashData(stream.GetBuffer().AsSpan(0,checked((int)stream.Length))));r.PerformanceCases.Add(value);
    }
    private static object AssemblyRecord(Assembly a)=>new{path=a.Location,bytes=new FileInfo(a.Location).Length,sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))),assembly=a.FullName};
    private sealed class Report
    {
        public string SchemaVersion{get;}="charts-c504-engineering-observations-v1";
        public DateTimeOffset StartedUtc{get;}=DateTimeOffset.UtcNow;public DateTimeOffset? FinishedUtc{get;set;}public bool Passed{get;set;}public string? Error{get;set;}
        public long StopwatchFrequency{get;}=Stopwatch.Frequency;
        public object Environment{get;}=new{os=RuntimeInformation.OSDescription,architecture=RuntimeInformation.ProcessArchitecture.ToString(),runtime=RuntimeInformation.FrameworkDescription,processorCount=System.Environment.ProcessorCount};
        public object[] AssemblyManifest{get;}=[AssemblyRecord(typeof(ChartEngineering).Assembly),AssemblyRecord(typeof(EngineeringRegression).Assembly)];
        public bool FormalSlaEvaluated{get;}=false;public List<Observation> Cases{get;}=[];public List<Observation> PerformanceCases{get;}=[];
    }
    private sealed class Observation
    {
        public required string Id{get;init;}public required string Kind{get;init;}public required double[][] Input{get;init;}public required object Parameters{get;init;}
        public object? Actual{get;set;}public string? ExceptionType{get;set;}public string? ExceptionMessage{get;set;}public string? InputFormula{get;set;}public int? InputCount{get;set;}public string? InputSha256{get;set;}
        public long ElapsedTicks{get;set;}public long ThreadAllocatedBytes{get;set;}
    }
}
