using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using NT.AvaloniaControl.Charts;

namespace Charts.Benchmarks;

// This producer records inputs/results. Acceptance is performed by the independent Python oracle.
internal static class MapsRegression
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true, NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };
    private sealed record Observation(string Id, double X, double Y, double Value, string? Label = null);
    private sealed record ValueInput(string RegionId, double? Value);
    private sealed class Case
    {
        public required string Id { get; init; }
        public required string Kind { get; init; }
        public object? Input { get; init; }
        public object? Configuration { get; init; }
        public object? Actual { get; set; }
        public object? Snapshot { get; set; }
        public object? Queries { get; set; }
        public bool? RepeatedResultIdentical { get; set; }
        public string? ExceptionType { get; set; }
        public string? Error { get; set; }
        public string? ExpectedExceptionType { get; init; }
        public double ElapsedMilliseconds { get; set; }
        public long AllocatedBytes { get; set; }
    }
    private sealed class Report
    {
        public string SchemaVersion { get; } = "charts-c603-map-regression-v1";
        public DateTimeOffset StartedUtc { get; } = DateTimeOffset.UtcNow;
        public DateTimeOffset FinishedUtc { get; set; }
        public string Framework { get; } = RuntimeInformation.FrameworkDescription;
        public string OperatingSystem { get; } = RuntimeInformation.OSDescription;
        public string ProcessArchitecture { get; } = RuntimeInformation.ProcessArchitecture.ToString();
        public string ProductAssembly { get; } = typeof(ChartMaps).Assembly.Location;
        public string ProductAssemblySha256 { get; } = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(ChartMaps).Assembly.Location)));
        public string ProducerAssemblySha256 { get; } = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Assembly.GetExecutingAssembly().Location)));
        public List<Case> Cases { get; } = [];
        public List<Case> Performance { get; } = [];
        public List<Case> Appendices { get; } = [];
        public bool ProducerCompleted { get; set; }
        public bool MathematicalAcceptanceEvaluated { get; } = false;
        public bool FormalSlaEvaluated { get; } = false;
        public string? Error { get; set; }
    }
    private static IEnumerable<MapObservation> Observe(IEnumerable<Observation> input) => input.Select(p => new MapObservation(p.Id, new(p.X, p.Y), p.Value, p, p.Label));
    private static Case Measure(string id, string kind, object? input, object? config, Func<object> action, string? exception = null)
    {
        var result = new Case { Id = id, Kind = kind, Input = input, Configuration = config, ExpectedExceptionType = exception };
        var before = GC.GetAllocatedBytesForCurrentThread(); var start = Stopwatch.GetTimestamp();
        try { result.Actual = action(); }
        catch (Exception e) { result.ExceptionType = e.GetType().Name; result.Error = e.Message; }
        result.ElapsedMilliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        result.AllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        return result;
    }
    private static void Invalid(Report report, string id, string expected, Func<object> action) => report.Cases.Add(Measure(id, "invalid", null, null, action, expected));
    private static MapPolygon Rectangle(double x0, double y0, double x1, double y1) => new([new(x0,y0),new(x1,y0),new(x1,y1),new(x0,y1),new(x0,y0)]);
    public static int Run(string[] args)
    {
        var at = Array.IndexOf(args, "--output");
        var path = Path.GetFullPath(at >= 0 && at + 1 < args.Length ? args[at + 1] : "artifacts/charts-maps/raw.json");
        if (File.Exists(path)) { Console.Error.WriteLine("Choose a fresh evidence path."); return 1; }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!); var report = new Report();
        try
        {
            foreach (var projection in Enum.GetValues<MapProjection>())
                foreach (var fixture in new[] { "native", "simple", "donut", "multipolygon", "dateline", "dateline-cut", "north-pole", "winding-reversed", "collapsed-pole", "empty" })
                {
                    var json = Geometry(fixture); var snapshot = ChartMaps.ReadGeoJson(json);
                    var row = Measure($"project-{projection}-{fixture}", "projection", new { fixture, geoJson = json }, new { projection = projection.ToString() }, () => ChartMaps.Project(snapshot, projection));
                    row.Snapshot = snapshot;
                    if (row.Actual is MapProjectedGeometry projected)
                    {
                        row.RepeatedResultIdentical = JsonSerializer.Serialize(projected, JsonOptions) == JsonSerializer.Serialize(ChartMaps.Project(snapshot, projection), JsonOptions);
                        row.Queries = projected.Features.Select(feature => new { featureId = feature.Entry.Id, values = QueryPoints.Select(p => new { coordinate = p, projected = ChartMaps.ProjectCoordinate(p, projection), contains = feature.Polygons.Any(polygon => ChartMaps.Contains(polygon, ChartMaps.ProjectCoordinate(p, projection))) }).ToArray() }).ToArray();
                    }
                    report.Cases.Add(row);
                }
            foreach (var name in new[] { "joined", "missing", "null-negative", "empty" })
            {
                var values = name switch
                {
                    "joined" => new[] { new ValueInput("a",1), new ValueInput("b",4), new ValueInput("missing",9) },
                    "missing" => [new("b",4)],
                    "null-negative" => [new("a",null),new("b",-4)],
                    _ => Array.Empty<ValueInput>()
                };
                var snapshot = ChartMaps.ReadGeoJson(Geometry("native"));
                report.Cases.Add(Measure("join-" + name,"join",new { geoJson = Geometry("native"), values },null,()=>ChartMaps.Join(snapshot,values.Select(v=>new MapValue(v.RegionId,v.Value,v)))));
            }
            foreach (var projection in Enum.GetValues<MapProjection>())
                foreach (var fixture in new[] { "basic", "zero", "epsilon", "coincident", "extreme" })
                {
                    var input = Points(fixture);
                    var row = Measure($"bubble-{projection}-{fixture}","bubble",input,new { projection=projection.ToString(),minRadius=0d,maxRadius=.08 },()=>ChartMaps.Bubbles(Observe(input),projection,0,.08));
                    if(row.ExceptionType is null) row.RepeatedResultIdentical=JsonSerializer.Serialize(row.Actual,JsonOptions)==JsonSerializer.Serialize(ChartMaps.Bubbles(Observe(input),projection,0,.08),JsonOptions);
                    report.Cases.Add(row);
                }
            foreach (var projection in Enum.GetValues<MapProjection>()) foreach(var radius in new[]{0,1,2})
            {
                var input=Points("basic");var bounds=projection==MapProjection.Identity?new MapBounds(-180,-90,180,90):new MapBounds(0,0,1,1);
                report.Cases.Add(Measure($"heat-{projection}-r{radius}","heatmap",input,new{projection=projection.ToString(),columns=6,rows=4,radius,bounds},()=>ChartMaps.Heatmap(Observe(input),projection,6,4,radius,bounds)));
            }
            var native=ChartMaps.ReadGeoJson(Geometry("native"));
            const string selfCrossJson = """{"type":"FeatureCollection","features":[{"type":"Feature","id":"cross","properties":{"name":"Self cross"},"geometry":{"type":"Polygon","coordinates":[[[0,0],[4,4],[0,3],[3,0],[0,0]]]}}]}""";
            foreach(var projection in Enum.GetValues<MapProjection>())
            {
                var snapshot=ChartMaps.ReadGeoJson(selfCrossJson);
                var row=Measure($"evenodd-{projection}","evenodd",new { geoJson=selfCrossJson },new{projection=projection.ToString(),topologyRule="EvenOdd"},()=>ChartMaps.Project(snapshot,projection,topologyRule:MapTopologyRule.EvenOdd));
                row.Snapshot=snapshot;
                if(row.Actual is MapProjectedGeometry projected) row.Queries=projected.Features.Select(f=>new {featureId=f.Entry.Id,values=EvenOddQueryPoints.Select(p=>new {coordinate=p,projected=ChartMaps.ProjectCoordinate(p,projection),contains=f.Polygons.Any(poly=>ChartMaps.Contains(poly,ChartMaps.ProjectCoordinate(p,projection)))}).ToArray()}).ToArray();
                report.Cases.Add(row);
            }
            Invalid(report,"duplicate-feature","ArgumentException",()=>ChartMaps.Snapshot([new("a",[Rectangle(0,0,1,1)]),new("a",[Rectangle(2,0,3,1)])]));
            Invalid(report,"empty-feature","ArgumentException",()=>ChartMaps.Snapshot([new("a",Array.Empty<MapPolygon>())]));
            Invalid(report,"unclosed-ring","ArgumentException",()=>new MapPolygon([new(0,0),new(1,0),new(1,1),new(0,1)]));
            Invalid(report,"zero-area","ArgumentException",()=>ChartMaps.Project(ChartMaps.Snapshot([new("a",[new([new(0,0),new(1,1),new(2,2),new(0,0)])])]),MapProjection.Identity));
            Invalid(report,"self-intersection","ArgumentException",()=>ChartMaps.Project(ChartMaps.Snapshot([new("a",[new([new(0,0),new(3,3),new(0,3),new(2,0),new(0,0)])])]),MapProjection.Identity));
            Invalid(report,"duplicate-region","ArgumentException",()=>ChartMaps.Join(native,[new("a",1),new("a",2)]));
            Invalid(report,"nonfinite-region","ArgumentOutOfRangeException",()=>ChartMaps.Join(native,[new("a",double.NaN)]));
            Invalid(report,"negative-observation","ArgumentOutOfRangeException",()=>ChartMaps.Bubbles([new("p",new(0,0),-1)],MapProjection.Equirectangular));
            Invalid(report,"nonfinite-observation","ArgumentOutOfRangeException",()=>ChartMaps.Bubbles([new("p",new(0,0),double.PositiveInfinity)],MapProjection.Equirectangular));
            Invalid(report,"duplicate-observation","ArgumentException",()=>ChartMaps.Bubbles([new("p",new(0,0),1),new("p",new(1,1),2)],MapProjection.Equirectangular));
            Invalid(report,"invalid-longitude","ArgumentOutOfRangeException",()=>ChartMaps.ProjectCoordinate(new(181,0),MapProjection.Equirectangular));
            Invalid(report,"invalid-latitude","ArgumentOutOfRangeException",()=>ChartMaps.ProjectCoordinate(new(0,91),MapProjection.WebMercator));
            Invalid(report,"invalid-radius-range","ArgumentOutOfRangeException",()=>ChartMaps.Bubbles(Array.Empty<MapObservation>(),MapProjection.Equirectangular,.2,.1));
            Invalid(report,"invalid-grid","ArgumentOutOfRangeException",()=>ChartMaps.Heatmap(Array.Empty<MapObservation>(),MapProjection.Equirectangular,257));
            Invalid(report,"invalid-kernel","ArgumentOutOfRangeException",()=>ChartMaps.Heatmap(Array.Empty<MapObservation>(),MapProjection.Equirectangular,radius:9));
            Invalid(report,"invalid-bounds","ArgumentOutOfRangeException",()=>ChartMaps.Heatmap(Array.Empty<MapObservation>(),MapProjection.Identity,bounds:new(1,0,0,1)));
            Invalid(report,"outside-bounds","ArgumentOutOfRangeException",()=>ChartMaps.Heatmap([new("p",new(2,2))],MapProjection.Identity,bounds:new(0,0,1,1)));
            Invalid(report,"heat-overflow","ArgumentOutOfRangeException",()=>ChartMaps.Heatmap([new("a",new(0,0),double.MaxValue),new("b",new(0,0),double.MaxValue)],MapProjection.Identity,1,1,0));
            Invalid(report,"feature-budget","ArgumentOutOfRangeException",()=>ChartMaps.Snapshot(Enumerable.Range(0,5001).Select(i=>new MapFeature(i,[Rectangle(i,0,i+1,1)]))));
            Invalid(report,"observation-budget","ArgumentOutOfRangeException",()=>ChartMaps.Bubbles(Enumerable.Range(0,20001).Select(i=>new MapObservation(i,new(0,0))),MapProjection.Equirectangular));
            Invalid(report,"snapshot-cancelled","OperationCanceledException",()=>ChartMaps.Snapshot([],new(true)));
            Invalid(report,"project-cancelled","OperationCanceledException",()=>ChartMaps.Project(native,MapProjection.Equirectangular,new(true)));
            Invalid(report,"join-cancelled","OperationCanceledException",()=>ChartMaps.Join(native,[],new(true)));
            Invalid(report,"bubble-cancelled","OperationCanceledException",()=>ChartMaps.Bubbles([],MapProjection.Equirectangular,cancellationToken:new(true)));
            Invalid(report,"heat-cancelled","OperationCanceledException",()=>ChartMaps.Heatmap([],MapProjection.Equirectangular,cancellationToken:new(true)));
            Invalid(report,"evenodd-zero-signed-area","ArgumentException",()=>ChartMaps.Project(ChartMaps.Snapshot([new("bowtie",[new([new(0,0),new(4,4),new(0,3),new(4,0),new(0,0)])])]),MapProjection.Identity,topologyRule:MapTopologyRule.EvenOdd));
            const string worldRelativePath="samples/NT.AvaloniaControl.Demo/Assets/Maps/natural-earth-110m.geojson";
            var worldJson=File.ReadAllText(worldRelativePath);var worldSha=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(worldRelativePath)));
            Invalid(report,"world-strict-self-intersection","ArgumentException",()=>ChartMaps.Project(ChartMaps.ReadGeoJson(worldJson),MapProjection.Equirectangular));
            foreach(var projection in Enum.GetValues<MapProjection>()) report.Appendices.Add(Measure($"world-{projection}","world-offline",new{geoJson=worldJson,sourceRelativePath=worldRelativePath,sha256=worldSha},new{projection=projection.ToString(),topologyRule="EvenOdd"},()=>ChartMaps.Project(ChartMaps.ReadGeoJson(worldJson),projection,topologyRule:MapTopologyRule.EvenOdd)));
            var geometry=Enumerable.Range(0,2000).Select(i=>new MapFeature("f"+i,[Rectangle(i%50*2,i/50*2,i%50*2+1,i/50*2+1)],source:new {index=i})).ToArray();
            report.Performance.Add(Measure("perf-geometry-2000-10000","scale-geometry",geometry,new{projection="Identity"},()=>ChartMaps.Project(ChartMaps.Snapshot(geometry),MapProjection.Identity)));
            var points=Enumerable.Range(0,10000).Select(i=>new Observation("p"+i,-180+(i%100)*360d/99,-80+(i/100)*160d/99,i%7+1)).ToArray();
            report.Performance.Add(Measure("perf-heat-10000-64x64-r2","scale-heatmap",points,new{projection="WebMercator",columns=64,rows=64,radius=2,bounds=new MapBounds(0,0,1,1)},()=>ChartMaps.Heatmap(Observe(points),MapProjection.WebMercator,64,64,2,new(0,0,1,1))));
            report.ProducerCompleted=report.Cases.All(c=>c.Kind=="invalid"?c.ExceptionType==c.ExpectedExceptionType:c.ExceptionType is null)&&report.Performance.All(c=>c.ExceptionType is null)&&report.Appendices.All(c=>c.ExceptionType is null);
        }
        catch(Exception e){report.Error=e.ToString();Console.Error.WriteLine(e);}
        report.FinishedUtc=DateTimeOffset.UtcNow;
        File.WriteAllText(path,JsonSerializer.Serialize(report,JsonOptions));
        Console.WriteLine($"Maps raw cases={report.Cases.Count}; scales={report.Performance.Count}; producerCompleted={report.ProducerCompleted}; mathematical acceptance is external.");
        return report.ProducerCompleted?0:1;
    }
    private static readonly MapCoordinate[] QueryPoints=[new(-25,0),new(-10,0),new(0,0),new(25,15),new(25,-15),new(175,0),new(-175,0),new(0,85),new(10,10)];
    private static readonly MapCoordinate[] EvenOddQueryPoints=[new(0,0),new(1,.5),new(1,2),new(2,2),new(2,3),new(5,5)];
    private static Observation[] Points(string name)=>name switch
    {
        "basic"=>[new("a",-180,-90,1,"South west"),new("b",0,0,4,"Center"),new("c",180,90,2,"North east"),new("zero",20,20,0)],
        "zero"=>[new("a",-20,0,0),new("b",20,0,0)],
        "epsilon"=>[new("a",-20,0,double.Epsilon),new("b",20,0,double.Epsilon*4)],
        "coincident"=>[new("a",0,0,1),new("b",0,0,4),new("zero",0,0,0)],
        "extreme"=>[new("a",-20,0,double.MaxValue),new("b",20,0,double.MaxValue/4)],
        _=>throw new ArgumentOutOfRangeException(nameof(name))
    };
    private static string Geometry(string name)=>GeometryJson[name];
    private static readonly Dictionary<string,string> GeometryJson=new()
    {
        ["native"] = """{"type":"FeatureCollection","features":[{"type":"Feature","id":"a","properties":{"code":"a","name":"Donut A"},"geometry":{"type":"Polygon","coordinates":[[[-30,-20],[10,-20],[10,20],[-30,20],[-30,-20]],[[-15,-5],[-15,5],[-5,5],[-5,-5],[-15,-5]]]}},{"type":"Feature","id":"b","properties":{"code":"b","name":"Two islands B"},"geometry":{"type":"MultiPolygon","coordinates":[[[[20,-20],[30,-20],[30,-10],[20,-10],[20,-20]]],[[[20,10],[30,10],[30,20],[20,20],[20,10]]]]}}]}""",
        ["empty"] = """{"type":"FeatureCollection","features":[]}""",
        ["simple"] = """{"type":"FeatureCollection","features":[{"type":"Feature","id":"a","properties":{"code":"a","name":"A"},"geometry":{"type":"Polygon","coordinates":[[[-10,-10],[10,-10],[10,10],[-10,10],[-10,-10]]]}}]}""",
        ["donut"] = """{"type":"FeatureCollection","features":[{"type":"Feature","id":"a","properties":{"code":"a","name":"A"},"geometry":{"type":"Polygon","coordinates":[[[-20,-20],[20,-20],[20,20],[-20,20],[-20,-20]],[[-5,-5],[-5,5],[5,5],[5,-5],[-5,-5]]]}}]}""",
        ["multipolygon"] = """{"type":"FeatureCollection","features":[{"type":"Feature","id":"islands","properties":{"code":"islands","name":"ISLANDS"},"geometry":{"type":"MultiPolygon","coordinates":[[[[-30,-10],[-20,-10],[-20,0],[-30,0],[-30,-10]]],[[[20,10],[30,10],[30,20],[20,20],[20,10]]]]}}]}""",
        ["dateline"] = """{"type":"FeatureCollection","features":[{"type":"Feature","id":"cross","properties":{"code":"cross","name":"CROSS"},"geometry":{"type":"Polygon","coordinates":[[[170,-10],[-170,-10],[-170,10],[170,10],[170,-10]]]}}]}""",
        ["dateline-cut"] = """{"type":"FeatureCollection","features":[{"type":"Feature","id":"cut","properties":{"code":"cut","name":"CUT"},"geometry":{"type":"MultiPolygon","coordinates":[[[[170,-10],[180,-10],[180,10],[170,10],[170,-10]]],[[[-180,-10],[-170,-10],[-170,10],[-180,10],[-180,-10]]]]}}]}""",
        ["collapsed-pole"] = """{"type":"FeatureCollection","features":[{"type":"Feature","id":"polar","properties":{"code":"polar","name":"POLAR"},"geometry":{"type":"Polygon","coordinates":[[[-10,86],[10,86],[10,90],[-10,90],[-10,86]]]}}]}""",
        ["north-pole"] = """{"type":"FeatureCollection","features":[{"type":"Feature","id":"polar","properties":{"code":"polar","name":"POLAR"},"geometry":{"type":"Polygon","coordinates":[[[-10,80],[10,80],[10,90],[-10,90],[-10,80]]]}}]}""",
        ["arbitrary"] = """{"type":"FeatureCollection","features":[{"type":"Feature","id":"seat-a","properties":{"code":"seat-a","name":"SEAT-A"},"geometry":{"type":"Polygon","coordinates":[[[1000,2000],[1040,2000],[1040,2040],[1000,2040],[1000,2000]]]}},{"type":"Feature","id":"seat-b","properties":{"code":"seat-b","name":"SEAT-B"},"geometry":{"type":"Polygon","coordinates":[[[1080,2000],[1120,2000],[1120,2040],[1080,2040],[1080,2000]]]}}]}""",
        ["line"] = """{"type":"FeatureCollection","features":[{"type":"Feature","id":"route","properties":{"code":"route","name":"ROUTE"},"geometry":{"type":"LineString","coordinates":[[-40,-10],[0,20],[40,-10]]}}]}""",
        ["point"] = """{"type":"FeatureCollection","features":[{"type":"Feature","id":"site","properties":{"code":"site","name":"SITE"},"geometry":{"type":"Point","coordinates":[12,34]}}]}""",
        ["winding-reversed"] = """{"type":"FeatureCollection","features":[{"type":"Feature","id":"a","properties":{"code":"a","name":"A"},"geometry":{"type":"Polygon","coordinates":[[[-20,-20],[-20,20],[20,20],[20,-20],[-20,-20]],[[-5,-5],[5,-5],[5,5],[-5,5],[-5,-5]]]}}]}""",
    };
}
