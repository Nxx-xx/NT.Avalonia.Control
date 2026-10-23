using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private readonly List<object> _statisticsEvidence = [];
    private async Task RunStatisticsSmokeAsync(Dictionary<string, bool> checks, string baseImage)
    {
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var label = theme == ThemeVariant.Dark ? "dark" : "light";
            string Image(string kind, string state) => Path.Combine(Path.GetDirectoryName(baseImage)!, Path.GetFileNameWithoutExtension(baseImage) + $"-statistics-{label}-{kind}-{state}.png");
            void Check(string name, bool value) => checks[$"STATISTICS-{label}-{name}"] = value;
            var background = Brush(label == "dark" ? "#111722" : "#FFFFFF");
            foreach (var kind in new[] { "box", "error", "hist", "density" })
            {
                var chart = new CartesianChart
                {
                    Width = 420, Height = 280, Padding = new Thickness(40,20,20,30), ShowLegend = false,
                    Background = background, AnimationTriggers = ChartAnimationTriggers.None,
                    IsTooltipEnabled = false, SelectionMode = ChartSelectionMode.Single,
                    XAxis = Axis(kind == "hist" ? 0 : kind == "density" ? -2 : 0, kind == "hist" ? 4 : 2),
                    YAxis = Axis(0, kind == "box" ? 32 : kind == "error" ? 10 : .5),
                };
                var source = new StatisticsProbeGroup("probe-box", 1, [1,2,3,4,5,6,7,8,9,30]);
                var errorRow = new StatisticsProbeError("probe-error", 1, 5, 3, 3);
                var histogram = ChartStatistics.HistogramByBoundaries([0,1,1,2,3,4], [0,1,3,4], ChartHistogramNormalization.Density);
                var density = ChartStatistics.KernelDensity([-1,0,1], 5, 1, -2, 2);
                ChartSeries series = kind switch
                {
                    "box" => new BoxPlotSeries { ItemsSource = new[] { source }, CategoryPath = "X", SamplesPath = "Samples", IdPath = "Id", BarWidth = 48, ShowOutliers = true, ShowMean = false },
                    "error" => new ErrorBarSeries { ItemsSource = new[] { errorRow }, CategoryPath = "X", ValuePath = "Value", LowErrorPath = "Lower", HighErrorPath = "Upper", IdPath = "Id", CapWidth = 24, ShowMarkers = true, MarkerSize = 8 },
                    "hist" => new HistogramSeries { ItemsSource = histogram.Bins, BinGapRatio = 0, Fill = Brush("#155CC1") },
                    _ => new DensitySeries { ItemsSource = density.Points, FillUnderCurve = false },
                };
                series.Stroke = Brush("#155CC1"); series.StrokeThickness = 3; chart.Series.Add(series);
                var window = new Window { Title = "Statistics probe · " + kind, Width = 420, Height = 280, CanResize = false, RequestedThemeVariant = theme, Background = background, Content = chart };
                try
                {
                    window.Show(_hostWindow); await Task.Delay(40); FlushControlTemplateWindow(window);
                    Check(kind + "-native-window", window.TryGetPlatformHandle() is { Handle: not 0 });
                    Check(kind + "-fixed-independent-plot", chart.PlotArea == new Rect(40,24,354,226));
                    var native = Image(kind, "native"); CaptureControlTemplateRaster(chart, native);
                    var exported = Image(kind, "export"); Check(kind + "-export-96", chart.TryExportPng(exported, new ChartExportOptions { Dpi = 96, PixelSize = new PixelSize(420,280), Background = background }));
                    var snapshot = series.CreateSnapshot();
                    var y = kind == "box" ? 5.5 : kind == "error" ? 5 : kind == "hist" ? .15 : density.Points[2].Y;
                    var x = kind is "box" or "error" ? 1 : kind == "hist" ? 2 : 0;
                    var query = Map(chart.VisibleRange!.Value, x, y); var hit = chart.HitTestPoint(query, .01);
                    Check(kind + "-hit-original-source", hit is not null && (kind switch
                    {
                        "box" => hit.Index == 0 && ReferenceEquals(hit.Item, source) && Equals(hit.StableId, source.Id) && hit.BoxPlot is { Median: 5.5, LowerWhisker: 1, UpperWhisker: 9, Outliers.Count: 1 },
                        "error" => hit.Index == 0 && ReferenceEquals(hit.Item, errorRow) && hit.ErrorInterval is { Center: 5, Lower: 2, Upper: 8 },
                        "hist" => hit.Index == 1 && Equals(hit.Item, histogram.Bins[1]),
                        _ => hit.Index == 2 && Equals(hit.Item, density.Points[2]),
                    }));
                    chart.SelectedPoint = hit; Check(kind + "-selection-keeps-original", hit is not null && ReferenceEquals(chart.SelectedPoint?.Item, hit.Item));
                    chart.SelectedPoint = null;
                    var variants = new List<string>();
                    if (kind == "box")
                    {
                        chart.Viewport = new ChartViewport(0,2,0,16); FlushControlTemplateWindow(window);
                        var zoom = Image(kind,"zoom"); CaptureControlTemplateRaster(chart,zoom); variants.Add(zoom);
                        var zoomHit = chart.HitTestPoint(Map(chart.VisibleRange!.Value,1,5.5),.01);
                        Check("box-zoom-original-group", zoomHit is not null && ReferenceEquals(zoomHit.Item,source));
                        chart.Viewport = null;
                        ((BoxPlotSeries)series).SamplesPath = null; ((BoxPlotSeries)series).SummaryPath = "Summary";
                        var pre = new StatisticsSummaryRow(source.Id, "one", ChartStatistics.BoxPlot(source.Samples));
                        series.CategoryPath = null; series.ItemsSource = new[] { pre };
                        Check("box-preaggregate-metadata", series.CreateSnapshot().Points[0].BoxPlot?.Median == 5.5);
                    }
                    else if (kind == "error")
                    {
                        ((ErrorBarSeries)series).Orientation = Orientation.Horizontal;
                        chart.XAxis = Axis(0,10); chart.YAxis = Axis(0,2); FlushControlTemplateWindow(window);
                        var horizontal = Image(kind,"horizontal"); CaptureControlTemplateRaster(chart,horizontal); variants.Add(horizontal);
                        var horizontalHit = chart.HitTestPoint(Map(chart.VisibleRange!.Value,5,1),.01);
                        Check("error-horizontal-original", horizontalHit is not null && ReferenceEquals(horizontalHit.Item,errorRow));
                        ((ErrorBarSeries)series).Orientation = Orientation.Vertical; chart.XAxis = Axis(0,2); chart.YAxis = Axis(0,100);
                        var secondary = Axis(0,10); secondary.Key = "stat"; secondary.Position = ChartAxisPosition.Far; chart.Axes.Add(secondary); series.YAxisKey = "stat"; FlushControlTemplateWindow(window);
                        var multiple = Image(kind,"multiaxis"); CaptureControlTemplateRaster(chart,multiple); variants.Add(multiple);
                        var axisHit = chart.HitTestPoint(Map(new ChartViewport(0,2,0,10),1,5),.01);
                        Check("error-secondary-axis-original", axisHit is not null && ReferenceEquals(axisHit.Item,errorRow));
                    }
                    else if (kind == "density")
                    {
                        foreach (var dpi in new[] { 144,192 })
                        {
                            var path = Image(kind,"dpi"+dpi); variants.Add(path);
                            Check("density-export-"+dpi, chart.TryExportPng(path,new ChartExportOptions { Dpi=dpi,PixelSize=new PixelSize(420*dpi/96,280*dpi/96),Background=background }));
                        }
                        var next = ChartStatistics.KernelDensity([-1,0,1],5,2,-2,2); series.ItemsSource=next.Points; FlushControlTemplateWindow(window);
                        var path2=Image(kind,"bandwidth"); CaptureControlTemplateRaster(chart,path2); variants.Add(path2);
                        Check("density-bandwidth-replaces-samples", !ReferenceEquals(snapshot,series.CreateSnapshot()) && series.CreateSnapshot().Points[2].Point.Y==next.Points[2].Y);
                    }
                    _statisticsEvidence.Add(new { theme=label,kind,native,exported,variants,observedPlot=chart.PlotArea,rawSamples=source.Samples,errorRow,histogram,density,
                        query,hitIndex=hit?.Index,hitId=hit?.StableId,snapshotCount=snapshot.Points.Count,physicalInputValidated=false,presentQualified=false,performanceMeasured=false });
                }
                finally { window.Close(); }
            }
            var board=CreateStatisticsBoard();
            var overview=new Window { Title="Charts · 统计与分布",Width=1180,Height=950,RequestedThemeVariant=theme,Background=background,Content=new Border { Padding=new Thickness(16),Child=board } };
            try
            {
                overview.Show(_hostWindow); await Task.Delay(40); FlushControlTemplateWindow(overview);
                Check("four-chart-demo-visible", board.GetVisualDescendants().OfType<CartesianChart>().Count()==4);
                var path=Image("board","overview");
                using var bitmap=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(overview.ClientSize.Width),(int)Math.Ceiling(overview.ClientSize.Height)),new Vector(96,96)); bitmap.Render(overview); bitmap.Save(path);
                _statisticsEvidence.Add(new { theme=label,kind="board",image=path,physicalInputValidated=false,performanceMeasured=false });
            }
            finally { overview.Close(); }
        }
        static NumericAxis Axis(double minimum,double maximum) => new() { Minimum=minimum,Maximum=maximum,IsVisible=false,ShowGridLines=false };
        static Point Map(ChartViewport range,double x,double y) => new(40+(x-range.XMinimum)/(range.XMaximum-range.XMinimum)*354,24+(range.YMaximum-y)/(range.YMaximum-range.YMinimum)*226);
    }
    private sealed record StatisticsProbeGroup(string Id,double X,double[] Samples);
    private sealed record StatisticsProbeError(string Id,double X,double Value,double Lower,double Upper);
}
