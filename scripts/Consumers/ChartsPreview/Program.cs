using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;
#if CHARTS_PREVIEW_THEME
using NT.AvaloniaControl.Charts.Themes;
#endif
#if CHARTS_PREVIEW_SKIA
using NT.AvaloniaControl.Charts.Skia;
#endif

namespace Charts.PreviewConsumer;

internal static class Program
{
    internal static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web)
        {WriteIndented=true,Converters={new JsonStringEnumConverter()}};
    private static readonly Dictionary<string,object?> Result=new()
        { ["schema"]="charts-preview-native-consumer/v1",["passed"]=false,["status"]="starting",
          ["formalPerformanceQualified"]=false,["physicalHardwareQualified"]=false };
    private static string Output="",Mode="smoke";
    private static int Seconds=10,Cycles=1,ExitCode=1;
    private static long Produced,Applied,RenderChecks,HitChecks,ExportChecks,CompletedCycles;
    private static ChartStreamingSession<Sample>? Session;
    private static StreamWriter? Samples;
    private static readonly Stopwatch Clock=new();
    private static object? BeforeText;
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            var options=new Dictionary<string,string>();
            for(var i=0;i<args.Length;i+=2)if(i+1>=args.Length||!options.TryAdd(args[i],args[i+1]))throw new ArgumentException("Distinct --name value pairs required");
            if(options.Keys.Except(new[]{"--output","--mode","--seconds","--cycles"}).Any())throw new ArgumentException("Unknown option");
            Output=Path.GetFullPath(options["--output"]);Mode=options.GetValueOrDefault("--mode","smoke");
            Seconds=int.Parse(options.GetValueOrDefault("--seconds","10"),CultureInfo.InvariantCulture);
            Cycles=int.Parse(options.GetValueOrDefault("--cycles","1"),CultureInfo.InvariantCulture);
            if(Mode is not("smoke" or "lifecycle" or "soak")||Seconds is <1 or >30000||Cycles is <1 or >1000)throw new ArgumentException("Invalid bounded workload");
            if(Directory.Exists(Output))throw new IOException("Fresh output required");Directory.CreateDirectory(Output);
            var builder=AppBuilder.Configure<PreviewApplication>().WithChartsNativeTextIsolation();
            BeforeText=ChartNativeTextBinding.Diagnostics;
#if CHARTS_PREVIEW_SKIA
            builder.WithChartsSkiaRendering();
#endif
            builder.UsePlatformDetect();
            if(OperatingSystem.IsWindows())builder.With(new Win32PlatformOptions{RenderingMode=[Win32RenderingMode.Software]});
            else builder.With(new X11PlatformOptions{RenderingMode=[X11RenderingMode.Software]});
            Result["options"]=new{mode=Mode,seconds=Seconds,cycles=Cycles,streamSeries=4,samplesPerBatch=50,batchMilliseconds=50,retainedPerSeries=16384,renderIntervalMilliseconds=200};
            Result["assemblies"]=new[]{typeof(Program).Assembly,typeof(CartesianChart).Assembly}.Select(a=>FileIdentity(a.Location)).ToArray();
            Result["environment"]=new{os=RuntimeInformation.OSDescription,runtime=RuntimeInformation.FrameworkDescription,architecture=RuntimeInformation.ProcessArchitecture.ToString(),processId=Environment.ProcessId,processStartedUtc=Process.GetCurrentProcess().StartTime.ToUniversalTime().ToString("O"),frequency=Stopwatch.Frequency};
            Save();builder.StartWithClassicDesktopLifetime([],ShutdownMode.OnExplicitShutdown);return ExitCode;
        }
        catch(Exception error){Result["error"]=error.ToString();if(Directory.Exists(Output))Save();Console.Error.WriteLine(error);return 1;}
    }
    private static async Task Execute(IClassicDesktopStyleApplicationLifetime lifetime)
    {
        Window? window=null;Scene? scene=null;
        try
        {
            Result["status"]="lifecycle";Save();
            for(var cycle=0;cycle<Cycles;cycle++)
            {
                scene=new Scene();window=CreateWindow(scene);lifetime.MainWindow=window;window.Show();window.UpdateLayout();CheckWindow(window);
                await Dispatcher.UIThread.InvokeAsync(()=>{},DispatcherPriority.Background);
                await Verify(scene,cycle==0?"first":null);
                window.Content=null;scene.Retire();window.Close();scene=null;window=null;CompletedCycles++;Save();
                await Dispatcher.UIThread.InvokeAsync(()=>{},DispatcherPriority.Background);
            }
            scene=new Scene();window=CreateWindow(scene);lifetime.MainWindow=window;window.Show();window.UpdateLayout();CheckWindow(window);
            await Dispatcher.UIThread.InvokeAsync(()=>{},DispatcherPriority.Background);
            await Verify(scene,"stream-start");
            Samples=new StreamWriter(new FileStream(Path.Combine(Output,"samples.jsonl"),FileMode.CreateNew,FileAccess.Write,FileShare.Read));
            Session=new ChartStreamingSession<Sample>(batch=>scene.Apply(batch),maximumBatchSize:50,maximumPendingBatches:2);
            Clock.Start();Result["status"]="streaming";Save();
            var producer=Task.Run(Produce);
            long nextSample=0,nextVerify=0,nextExport=30000,nextTheme=10000;
            while(!producer.IsCompleted)
            {
                var elapsed=Clock.ElapsedMilliseconds;
                if(elapsed>(long)Seconds*1000+300000)throw new TimeoutException("Preview streaming exceeded bounded drain");
                if(elapsed>=nextVerify){CheckWindow(window);window.UpdateLayout();CheckHits(scene);Render(scene);nextVerify=elapsed+200;}
                if(elapsed>=nextSample){SampleResources(scene);nextSample=elapsed+1000;}
                if(elapsed>=nextTheme){Application.Current!.RequestedThemeVariant=Application.Current.ActualThemeVariant==ThemeVariant.Dark?ThemeVariant.Light:ThemeVariant.Dark;scene.ReplaceSmallSources();nextTheme=elapsed+10000;}
                if(elapsed>=nextExport){await CheckExports(scene.Charts[0],null);nextExport=elapsed+30000;}
                await Task.Delay(20);
            }
            await producer;await Session.CompleteAsync();SampleResources(scene);
            await Verify(scene,"last");Clock.Stop();
            var stats=Session.Statistics;
            if(Produced!=Applied||stats.AcceptedSamples!=Produced||stats.CommittedSamples!=Produced||stats.PendingSamples!=0||stats.FaultedSamples!=0||stats.CanceledSamples!=0||stats.RejectedSamples!=0||stats.MaximumObservedPendingBatches>2)throw new InvalidOperationException("Accepted/committed accounting failed");
            if(Clock.Elapsed.TotalSeconds<Seconds)throw new InvalidOperationException("Measured monotonic duration shorter than requested");
            Result["streamStatistics"]=stats;Result["elapsedSeconds"]=Clock.Elapsed.TotalSeconds;Result["status"]="completed";Result["passed"]=true;ExitCode=0;
        }
        catch(Exception error){Result["status"]="failed";Result["error"]=error.ToString();Console.Error.WriteLine(error);}
        finally
        {
            Session?.Dispose();Samples?.Dispose();
            if(window!=null){window.Content=null;scene?.Retire();window.Close();}
            Result["nativeTextBeforeUi"]=BeforeText;Result["nativeTextAfterUi"]=ChartNativeTextBinding.Diagnostics;
#if CHARTS_PREVIEW_SKIA
            var backend=typeof(ChartSkiaRenderingExtensions).Assembly.GetType("NT.AvaloniaControl.Charts.Skia.ChartNativeCircleOperation");
            Result["optionalSkiaFastFrameCount"]=backend?.GetProperty("FastPaintFrameCount",BindingFlags.NonPublic|BindingFlags.Static)?.GetValue(null);
#endif
            Result["completedCycles"]=CompletedCycles;Result["producedSamples"]=Produced;Result["appliedSamples"]=Applied;
            Result["renderChecks"]=RenderChecks;Result["hitChecks"]=HitChecks;Result["exportChecks"]=ExportChecks;
            Result["finishedUtc"]=DateTimeOffset.UtcNow;Save();lifetime.Shutdown(ExitCode);
        }
    }
    private static Window CreateWindow(Scene scene)=>new(){Title="Charts preview · automated package validation",Width=1140,Height=760,Content=scene.Root};
    private static void CheckWindow(Window window)
    {
        var handle=window.TryGetPlatformHandle();
        if(!window.IsVisible||handle is null||handle.Handle==0)throw new InvalidOperationException("No visible native window handle");
        Result["nativeWindow"]=new{handleDescriptor=handle.HandleDescriptor,nonzeroHandle=true,window.ClientSize.Width,window.ClientSize.Height,window.RenderScaling,requestedRenderer="software"};
    }
    private static async Task Produce()
    {
        var totalBatches=checked(Seconds*20);
        for(var batch=0;batch<totalBatches;batch++)
        {
            var scheduled=batch*50L;var delay=scheduled-Clock.ElapsedMilliseconds;if(delay>0)await Task.Delay((int)delay).ConfigureAwait(false);
            var items=new Sample[50];
            for(var i=0;i<items.Length;i++){var ordinal=batch*50L+i;var series=(int)(ordinal%4);var x=1024L+ordinal/4;items[i]=new(series,new Row(x*4+series,x,(series%2==0?-.75:.75)+Math.Sin(x*.025)*.2));}
            Interlocked.Add(ref Produced,items.Length);await Session!.AppendAsync(items).ConfigureAwait(false);
        }
        var remainder=(long)Seconds*1000-Clock.ElapsedMilliseconds;if(remainder>0)await Task.Delay((int)remainder).ConfigureAwait(false);
    }
    private static async Task Verify(Scene scene,string? id)
    {
        if(TopLevel.GetTopLevel(scene.Root) is null)throw new InvalidOperationException("Charts are not attached to a native window");
        CheckHits(scene);Render(scene);await CheckExports(scene.Charts[0],id);
        using var canceled=new CancellationTokenSource();canceled.Cancel();var didCancel=false;
        try{using var destination=new MemoryStream();await scene.Charts[0].TryExportPngAsync(destination,cancellationToken:canceled.Token);}catch(OperationCanceledException){didCancel=true;}
        if(!didCancel)throw new InvalidOperationException("Pre-canceled export was not canceled");
        using var readOnly=new MemoryStream(new byte[8],writable:false);
        if(scene.Charts[0].TryExportJpeg(readOnly))throw new InvalidOperationException("Readonly export destination unexpectedly succeeded");
        if(ChartWorkScheduler.GetBudgetInfo().EstimatedResourceCacheBytes>ChartWorkScheduler.ResourceCacheBudgetBytes)throw new InvalidOperationException("Logical process cache budget exceeded");
    }
    private static void CheckHits(Scene scene)
    {
        foreach(var (chart,series) in scene.Charts.Select(c=>(c,c.Series[0])))
        {
            var items=(IReadOnlyList<Row>)series.ItemsSource!;var row=items[items.Count-3];var range=chart.VisibleRange!.Value;var p=chart.PlotArea;
            var point=new Point(p.Left+(row.X-range.XMinimum)/(range.XMaximum-range.XMinimum)*p.Width,p.Bottom-(row.Y-range.YMinimum)/(range.YMaximum-range.YMinimum)*p.Height);
            var hit=chart.HitTestPoint(point,.01);
            if(hit is null||!ReferenceEquals(hit.Series,series)||!ReferenceEquals(hit.Item,row)||!Equals(hit.StableId,row.Id)||hit.Index!=items.Count-3)throw new InvalidOperationException("Original item/index/id hit mismatch");
            HitChecks++;
        }
    }
    private static void Render(Scene scene)
    {
        foreach(var chart in scene.Charts){using var bitmap=new RenderTargetBitmap(new PixelSize(520,320),new Vector(96,96));bitmap.Render(chart);RenderChecks++;var cache=chart.GetResourceCacheInfo();if(cache.EstimatedBytes>cache.BudgetBytes)throw new InvalidOperationException("Chart cache exceeds logical budget");}
    }
    private static async Task CheckExports(CartesianChart chart,string? id)
    {
        foreach(var jpeg in new[]{false,true})
        {
            using var stream=new MemoryStream();var options=new ChartExportOptions{PixelSize=new PixelSize(520,320),UseSampling=true};
            var ok=jpeg?await chart.TryExportJpegAsync(stream,options):await chart.TryExportPngAsync(stream,options);
            var bytes=stream.ToArray();if(!ok||bytes.Length<100||(jpeg?bytes[0]!=255||bytes[1]!=216:bytes[0]!=137||bytes[1]!=80))throw new InvalidOperationException("PNG/JPEG export failed");
            if(id!=null)File.WriteAllBytes(Path.Combine(Output,id+(jpeg?".jpg":".png")),bytes);ExportChecks++;
        }
    }
    private static void SampleResources(Scene scene)
    {
        using var process=Process.GetCurrentProcess();process.Refresh();
        var info=new{elapsedSeconds=Clock.Elapsed.TotalSeconds,utc=DateTimeOffset.UtcNow,workingSetBytes=process.WorkingSet64,privateBytes=process.PrivateMemorySize64,managedBytes=GC.GetTotalMemory(false),handles=process.HandleCount,threads=process.Threads.Count,
            produced=Interlocked.Read(ref Produced),applied=Applied,statistics=Session!.Statistics,buffers=scene.Buffers.Select(b=>new{b.Count,b.Capacity,b.EvictedCount}).ToArray(),cache=ChartWorkScheduler.GetBudgetInfo()};
        Samples!.WriteLine(JsonSerializer.Serialize(info,new JsonSerializerOptions(Json){WriteIndented=false}));Samples.Flush();Result["latestSample"]=info;Save();
    }
    private static object FileIdentity(string path)=>new{path,bytes=new FileInfo(path).Length,sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))};
    private static void Save()
    {
        var tmp=Path.Combine(Output,"result.tmp");File.WriteAllText(tmp,JsonSerializer.Serialize(Result,Json));File.Move(tmp,Path.Combine(Output,"result.json"),true);
    }
    private sealed record Row(long Id,double X,double Y);
    private sealed record Sample(int Series,Row Row);
    private sealed class Scene
    {
        internal readonly ChartDataBuffer<Row>[] Buffers=Enumerable.Range(0,4).Select(_=>new ChartDataBuffer<Row>(16384)).ToArray();
        internal readonly CartesianChart[] Charts;
        internal readonly Grid Root;
        private int revision;
        internal Scene()
        {
            Charts=Enumerable.Range(0,4).Select(_=>new CartesianChart{Width=520,Height=320,Background=Brushes.White,Foreground=Brushes.Black,FontFamily=new FontFamily("DejaVu Sans, Segoe UI"),AnimationTriggers=ChartAnimationTriggers.None,ShowLegend=false}).ToArray();
            for(var s=0;s<4;s++)
            {
                Buffers[s].AppendRange(Enumerable.Range(0,1024).Select(i=>new Row(i*4+s,i,(s%2==0?-.75:.75)+Math.Sin(i*.025)*.2)));
                Charts[s/2].Series.Add(new LineSeries{ItemsSource=Buffers[s],CategoryPath="X",ValuePath="Y",IdPath="Id",ShowMarkers=false,SamplingMode=ChartSamplingMode.MinMax});
            }
            foreach(var c in Charts.Take(2)){c.FollowLatestData=true;c.FollowLatestWindow=512;c.YAxis=new NumericAxis{Minimum=-2,Maximum=2};}
            Charts[2].Series.Add(new BarSeries{CategoryPath="X",ValuePath="Y",IdPath="Id"});
            Charts[3].Series.Add(new ScatterSeries{CategoryPath="X",ValuePath="Y",IdPath="Id",MarkerSize=4});
            ReplaceSmallSources();Root=new Grid{RowDefinitions=new RowDefinitions("Auto,*,*"),ColumnDefinitions=new ColumnDefinitions("*,*")};
            var text=new TextBlock{Text="Chart 预览 · é 😀 العربية",FontSize=18};Grid.SetColumnSpan(text,2);Root.Children.Add(text);
            for(var i=0;i<4;i++){Grid.SetRow(Charts[i],i/2+1);Grid.SetColumn(Charts[i],i%2);Root.Children.Add(Charts[i]);}
        }
        internal void Apply(IReadOnlyList<Sample> batch)
        {
            using(Charts[0].DeferDataUpdates())using(Charts[1].DeferDataUpdates())
                for(var s=0;s<4;s++){var local=s;Buffers[s].AppendRange(batch.Where(x=>x.Series==local).Select(x=>x.Row));}
            Applied+=batch.Count;
        }
        internal void ReplaceSmallSources()
        {
            revision++;Charts[2].Series[0].ItemsSource=Enumerable.Range(0,32).Select(i=>new Row(revision*1000+i,i,2+i%11)).ToArray();
            var scatterCount=256;
#if CHARTS_PREVIEW_SKIA
            scatterCount=4096;
#endif
            Charts[3].Series[0].ItemsSource=Enumerable.Range(0,scatterCount).Select(i=>new Row(revision*10000+i,i,2+Math.Sin(i*.04))).ToArray();
        }
        internal void Retire(){foreach(var chart in Charts){foreach(var series in chart.Series)series.ItemsSource=null;chart.Series.Clear();}Root.Children.Clear();}
    }
    private sealed class PreviewApplication:Application
    {
        public override void Initialize(){Styles.Add(new FluentTheme());
#if CHARTS_PREVIEW_THEME
            Styles.Add(new ChartsTheme());
#endif
        }
        public override void OnFrameworkInitializationCompleted(){if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)Dispatcher.UIThread.Post(async()=>await Execute(lifetime));base.OnFrameworkInitializationCompleted();}
    }
}
