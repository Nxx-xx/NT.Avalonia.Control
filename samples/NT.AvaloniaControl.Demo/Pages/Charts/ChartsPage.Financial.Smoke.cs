using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private readonly List<object> _financialEvidence = [];
    private async Task RunFinancialSmokeAsync(Dictionary<string,bool> checks,string baseImage)
    {
        foreach (var theme in new[] { ThemeVariant.Light,ThemeVariant.Dark })
        {
            var label=theme==ThemeVariant.Dark?"dark":"light";
            var background=Brush(label=="dark"?"#111722":"#FFFFFF");
            string Image(string kind,string state)=>Path.Combine(Path.GetDirectoryName(baseImage)!,Path.GetFileNameWithoutExtension(baseImage)+$"-financial-{label}-{kind}-{state}.png");
            void Check(string name,bool value)=>checks[$"FINANCIAL-{label}-{name}"]=value;
            foreach(var kind in new[] { "candle","ohlc","hilo","ha","renko","kagi","pnf","volume" })
            {
                var source=FinancialProbeRows(kind is "renko" or "kagi" or "pnf");
                var series=FinancialProbeSeries(kind); series.ItemsSource=source;
                var snapshot=series.CreateSnapshot();
                var maximum=kind is "renko" or "kagi" or "pnf" ? snapshot.Points.Max(p=>p.Point.X)+.5:3.5;
                var chart=new CartesianChart { Width=420,Height=280,Padding=new Thickness(40,20,20,30),ShowLegend=false,Background=background,
                    AnimationTriggers=ChartAnimationTriggers.None,IsTooltipEnabled=false,SelectionMode=ChartSelectionMode.Single,
                    XAxis=FinancialProbeAxis(-.5,maximum),YAxis=FinancialProbeAxis(0,kind=="volume"?1000:20) };
                chart.Series.Add(series);
                var window=new Window { Title="Financial probe · "+kind,Width=420,Height=280,CanResize=false,RequestedThemeVariant=theme,Background=background,Content=chart };
                try
                {
                    window.Show(_hostWindow); await Task.Delay(40); FlushControlTemplateWindow(window);
                    Check(kind+"-native-window",window.TryGetPlatformHandle() is { Handle:not 0 });
                    Check(kind+"-independent-plot",chart.PlotArea==new Rect(40,24,354,226));
                    var native=Image(kind,"native"); CaptureControlTemplateRaster(chart,native);
                    var exported=Image(kind,"export"); Check(kind+"-export96",chart.TryExportPng(exported,new ChartExportOptions { Dpi=96,PixelSize=new PixelSize(420,280),Background=background }));
                    var first=snapshot.Points[0]; var sample=first.Financial!;
                    var queryY=kind=="volume"?50:kind=="pnf"?12:kind=="kagi"?12.5:(sample.Open+sample.Close)/2;
                    var query=FinancialProbeMap(chart.VisibleRange!.Value,first.Point.X,queryY);
                    var hit=chart.HitTestPoint(query,.01);
                    Check(kind+"-hit-display-index-and-raw-source",hit is not null&&hit.Index==first.SourceIndex&&hit.Financial is { } meta&&meta.SourceEnd==source[meta.SourceEndIndex]&&Equals(hit.Item,source[meta.SourceEndIndex]));
                    Check(kind+"-source-range-and-synthetic-kind",sample.SourceStartIndex>=0&&sample.SourceEndIndex<source.Length&&sample.SourceEndIndex>=sample.SourceStartIndex&&sample.Kind==series.Transform);
                    chart.SelectedPoint=hit;
                    Check(kind+"-selection-preserves-source",hit is not null&&chart.SelectedPoint?.Financial?.SourceEnd==sample.SourceEnd);
                    chart.SelectedPoint=null;
                    var variants=new List<string>();
                    if(kind=="candle")
                    {
                        chart.Viewport=new ChartViewport(-.5,3.5,5,17); FlushControlTemplateWindow(window);
                        var zoom=Image(kind,"zoom"); CaptureControlTemplateRaster(chart,zoom); variants.Add(zoom);
                        Check("candle-zoom-identity",chart.HitTestPoint(FinancialProbeMap(chart.VisibleRange!.Value,0,11),.01)?.Financial?.SourceEnd==source[0]);
                        chart.Viewport=null; series.RisingBrush=Brush("#155CC1"); series.FallingBrush=Brush("#C5394D"); series.NeutralBrush=Brush("#B77700"); FlushControlTemplateWindow(window);
                        var colored=Image(kind,"colored"); CaptureControlTemplateRaster(chart,colored); variants.Add(colored);
                        series.FallingBrush=Brush("#155CC1"); series.NeutralBrush=Brush("#155CC1");
                        chart.YAxis=FinancialProbeAxis(0,100); var price=FinancialProbeAxis(0,20); price.Key="probe-price"; price.Position=ChartAxisPosition.Far; chart.Axes.Add(price); series.YAxisKey=price.Key; FlushControlTemplateWindow(window);
                        var secondary=Image(kind,"secondary"); CaptureControlTemplateRaster(chart,secondary); variants.Add(secondary);
                        Check("candle-secondary-axis-identity",chart.HitTestPoint(FinancialProbeMap(new ChartViewport(-.5,3.5,0,20),0,11),.01)?.Financial?.SourceEnd==source[0]);
                    }
                    else if(kind=="renko")
                    {
                        foreach(var dpi in new[] {144,192})
                        {
                            var path=Image(kind,"dpi"+dpi); variants.Add(path);
                            Check("renko-export"+dpi,chart.TryExportPng(path,new ChartExportOptions { Dpi=dpi,PixelSize=new PixelSize(420*dpi/96,280*dpi/96),Background=background }));
                        }
                    }
                    _financialEvidence.Add(new { theme=label,kind,native,exported,variants,source,display=snapshot.Points.Select(p=>new { p.SourceIndex,p.Point,p.RawPoint,p.Financial }).ToArray(),
                        query,hitIndex=hit?.Index,hitSourceStart=hit?.Financial?.SourceStartIndex,hitSourceEnd=hit?.Financial?.SourceEndIndex,hitRawItem=hit?.Item,hitFinancial=hit?.Financial,hitRawPoint=hit?.RawPoint,observedPlot=chart.PlotArea,
                        physicalInputValidated=false,presentQualified=false,performanceMeasured=false });
                }
                finally { window.Close(); }
            }
            var host=new FinancialChart { Width=420,Height=280 };
            host.Chart.Width=420; host.Chart.Height=280; host.Chart.Padding=new Thickness(40,20,20,30); host.Chart.ShowLegend=false; host.Chart.Background=background;
            host.Chart.AnimationTriggers=ChartAnimationTriggers.None; host.Chart.IsTooltipEnabled=false;
            host.PriceAxis.Minimum=0; host.PriceAxis.Maximum=20; host.PriceAxis.IsVisible=false; host.PriceAxis.ShowGridLines=false; host.PriceAxis.Title=null;
            host.VolumeAxis.Minimum=0; host.VolumeAxis.Maximum=1000; host.VolumeAxis.IsVisible=false; host.VolumeAxis.ShowGridLines=false; host.VolumeAxis.Title=null;
            host.Chart.XAxis=FinancialProbeAxis(-.5,3.5);
            host.PriceSeries.RisingBrush=host.PriceSeries.FallingBrush=host.PriceSeries.NeutralBrush=Brush("#155CC1"); host.PriceSeries.StrokeThickness=3; host.PriceSeries.WidthRatio=.6;
            host.VolumeSeries.RisingBrush=host.VolumeSeries.FallingBrush=host.VolumeSeries.NeutralBrush=Brush("#C5394D"); host.VolumeSeries.WidthRatio=.6;
            var hostRows=FinancialProbeRows(false); host.SetData(hostRows);
            var overlay=new LineSeries { ItemsSource=host.ProjectOverlay(hostRows.Select(p=>new ChartPoint(p.Time.ToUnixTimeMilliseconds(),p.Close/2))),Stroke=Brush("#B77700"),StrokeThickness=3,ShowMarkers=false };
            host.Series.Add(overlay);
            var hostWindow=new Window { Title="Financial date/volume/overlay",Width=420,Height=280,RequestedThemeVariant=theme,Background=background,Content=host };
            try
            {
                hostWindow.Show(_hostWindow); await Task.Delay(40); FlushControlTemplateWindow(hostWindow);
                Check("host-native-and-fixed-plot",hostWindow.TryGetPlatformHandle() is { Handle:not 0 }&&host.Chart.PlotArea==new Rect(40,24,354,226));
                Check("host-weekend-is-one-slot",host.MapTime(hostRows[1].Time)-host.MapTime(hostRows[0].Time)==1&&(hostRows[1].Time-hostRows[0].Time).TotalDays==3);
                Check("host-unknown-date-is-gap",double.IsNaN(host.MapTime(hostRows[0].Time.AddDays(1))));
                Check("host-date-overlay-and-volume-context",host.Samples.Count==4&&host.PriceSeries.CreateSnapshot().Points.Select(p=>p.Point.X).SequenceEqual(host.VolumeSeries.CreateSnapshot().Points.Select(p=>p.Point.X))&&overlay.CreateSnapshot().Points.Select(p=>p.Point.X).SequenceEqual(new double[] {0,1,2,3}));
                var path=Image("host","native"); CaptureControlTemplateRaster(host.Chart,path);
                var unknownTime=hostRows[0].Time.AddDays(1); var unknownSlot=host.MapTime(unknownTime);
                _financialEvidence.Add(new { theme=label,kind="host",native=path,source=hostRows,slots=hostRows.Select(p=>host.MapTime(p.Time)).ToArray(),unknownTime,unknownSlot=double.IsFinite(unknownSlot)?(double?)unknownSlot:null,overlay=overlay.CreateSnapshot().Points.Select(p=>p.Point).ToArray(),observedPlot=host.Chart.PlotArea,physicalInputValidated=false,performanceMeasured=false });
            }
            finally { hostWindow.Close(); }
            var board=CreateFinancialBoard();
            var overview=new Window { Title="Charts · 金融价格",Width=1440,Height=1060,RequestedThemeVariant=theme,Background=background,Content=new Border { Padding=new Thickness(16),Child=board } };
            try
            {
                overview.Show(_hostWindow); await Task.Delay(40); FlushControlTemplateWindow(overview);
                Check("seven-financial-demo-panels",board.GetVisualDescendants().OfType<FinancialChart>().Count()==7);
                var path=Image("board","overview"); using var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(overview.ClientSize.Width),(int)Math.Ceiling(overview.ClientSize.Height)),new Vector(96,96)); bitmap.Render(overview); bitmap.Save(path);
                _financialEvidence.Add(new { theme=label,kind="board",image=path,physicalInputValidated=false,performanceMeasured=false });
            }
            finally { overview.Close(); }
        }
    }

    private static NumericAxis FinancialProbeAxis(double minimum,double maximum)=>new() { Minimum=minimum,Maximum=maximum,IsVisible=false,ShowGridLines=false };
    private static Point FinancialProbeMap(ChartViewport range,double x,double y)=>new(40+(x-range.XMinimum)/(range.XMaximum-range.XMinimum)*354,24+(range.YMaximum-y)/(range.YMaximum-range.YMinimum)*226);
    private static FinancialSeries FinancialProbeSeries(string kind)
    {
        FinancialSeries series=kind=="volume"?new FinancialVolumeSeries():new FinancialSeries();
        series.Transform=kind switch { "ha"=>ChartFinancialTransformKind.HeikinAshi,"renko"=>ChartFinancialTransformKind.Renko,"kagi"=>ChartFinancialTransformKind.Kagi,"pnf"=>ChartFinancialTransformKind.PointAndFigure,_=>ChartFinancialTransformKind.Raw };
        series.Style=kind switch { "ohlc"=>ChartFinancialStyle.Ohlc,"hilo"=>ChartFinancialStyle.HighLow,"renko"=>ChartFinancialStyle.Renko,"kagi"=>ChartFinancialStyle.Kagi,"pnf"=>ChartFinancialStyle.PointAndFigure,_=>ChartFinancialStyle.Candlestick };
        series.BoxSize=2; series.ReversalBoxes=kind=="pnf"?3:2; series.ReversalAmount=3; series.WidthRatio=.6; series.StrokeThickness=3;
        series.RisingBrush=series.FallingBrush=series.NeutralBrush=Brush("#155CC1"); return series;
    }
    private static ChartFinancialPoint[] FinancialProbeRows(bool wave)
    {
        double[] closes=wave?[10,12,15,11,7,10,14]:[12,10,10,15];
        double[] opens=wave?closes.Select(p=>p-1).ToArray():[10,12,10,10];
        double[] highs=wave?closes.Select(p=>p+2).ToArray():[14,15,13,16];
        double[] lows=wave?closes.Select(p=>p-2).ToArray():[8,9,9,9];
        var date=new DateTimeOffset(2026,10,2,9,30,0,TimeSpan.Zero); var rows=new ChartFinancialPoint[closes.Length];
        for(var i=0;i<rows.Length;i++) { while(date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) date=date.AddDays(1); rows[i]=new(date,opens[i],highs[i],lows[i],closes[i],(i+1)*100); date=date.AddDays(1); }
        return rows;
    }
}
