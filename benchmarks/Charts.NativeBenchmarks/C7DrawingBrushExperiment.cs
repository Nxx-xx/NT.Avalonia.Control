#if C7_PERFORMANCE_DRIVER
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Charts.Benchmarks;
using NT.AvaloniaControl.Charts;
using NT.AvaloniaControl.Charts.Skia;
using NT.AvaloniaControl.Charts.Themes;

namespace Charts.NativeBenchmarks;

// Untimed experiment only. No production renderer or performance protocol uses
// this path. An unequal pixel disqualifies that scene/content configuration.
internal static class C7DrawingBrushExperiment
{
    private const int Width = 1200, Height = 650;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static string _output = null!;
    private static bool _passed;

    internal static int Run(string[] args)
    {
        if (args.Length != 3 || args[0] != "--c7-drawing-brush-experiment" || args[1] != "--output")
            throw new ArgumentException("Expected --c7-drawing-brush-experiment --output <new-directory>.");
        _output = Path.GetFullPath(args[2]);
        if (Directory.Exists(_output) || File.Exists(_output)) throw new IOException("Experiment output must be new.");
        Directory.CreateDirectory(_output);
        ChartNativeTextBinding.Initialize();
        var builder = AppBuilder.Configure<BrushExperimentApplication>().WithChartsSkiaRendering().UsePlatformDetect();
        if (OperatingSystem.IsWindows()) builder.With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Software] });
        else builder.With(new X11PlatformOptions { RenderingMode = [X11RenderingMode.Software] });
        builder.StartWithClassicDesktopLifetime([], ShutdownMode.OnExplicitShutdown);
        return _passed ? 0 : 1;
    }

    private static void Capture(Window window, IClassicDesktopStyleApplicationLifetime desktop)
    {
        var comparisons = new List<object>();
        var completed = false;
        var equivalent = true;
        string? error = null;
        try
        {
            foreach (var kind in new[] { "scatter", "bar" })
            foreach (var viewport in new[] { "full", "zoom" })
            foreach (var paint in new[] { "opaque", "alpha", "gradient" })
            {
                var corpus = DatasetFactory.Create(DatasetFactory.Definitions.Single(d => d.Id == (kind == "scatter" ? "S10-S" : "S10-B")));
                var xmin = corpus.Points.Min(a => a.Min(p => p.X)) - 1;
                var xmax = corpus.Points.Max(a => a.Max(p => p.X)) + 1;
                var ymin = Math.Min(0, corpus.Points.Min(a => a.Min(p => p.Y))) - 1;
                var ymax = Math.Max(0, corpus.Points.Max(a => a.Max(p => p.Y + .25))) + 1;
                Brush brush = paint switch
                {
                    "alpha" => new SolidColorBrush(Color.FromArgb(137, 21, 92, 193)) { Opacity = .75 },
                    "gradient" => new LinearGradientBrush { GradientStops = [new(Colors.CornflowerBlue, 0), new(Color.FromArgb(119, 240, 31, 73), .375), new(Colors.DarkGreen, 1)], Opacity = .625 },
                    _ => new SolidColorBrush(Color.Parse("#155CC1")),
                };
                var chart = new CartesianChart
                {
                    Width = Width, Height = Height, Background = Brushes.White, Foreground = Brushes.Black,
                    FontFamily = new FontFamily("DejaVu Sans, Segoe UI"), FontSize = 12,
                    ShowLegend = false, ShowGridLines = true, AnimationTriggers = ChartAnimationTriggers.None,
                    XAxis = new NumericAxis { Minimum = xmin, Maximum = xmax, DesiredTickCount = 6 },
                    YAxis = new NumericAxis { Minimum = ymin, Maximum = ymax, DesiredTickCount = 6 },
                };
                foreach (var points in corpus.Points)
                {
                    ChartSeries series = kind == "scatter" ? new ScatterSeries { MarkerSize = 4 } : new BarSeries();
                    series.Stroke = brush; series.Fill = brush; series.StrokeThickness = 2; series.ShowDataLabels = false;
                    series.CategoryPath = "X"; series.ValuePath = "Y"; series.IdPath = "Id";
                    series.ItemsSource = points.Select((point, index) => new Row(index, point.X, point.Y)).ToArray();
                    chart.Series.Add(series);
                }
                if (viewport == "zoom") chart.Viewport = new(xmin + (xmax - xmin) * .25, xmin + (xmax - xmin) * .35, ymin, ymax);
                window.Content = chart; window.UpdateLayout(); _ = chart.PlotArea;
                foreach (var content in new[] { "opaque-chart", "transparent-series" })
                {
                    var group = new DrawingGroup();
                    using (var context = group.Open())
                    {
                        if (content == "opaque-chart") chart.Render(context);
                        else
                        {
                            // A zero-alpha rectangle fixes brush coordinates to
                            // the canvas while retaining a transparent surface.
                            context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, Width, Height));
                            var range = chart.VisibleRange!.Value;
                            var projection = new Projection(chart.PlotArea, range);
                            using var clip = context.PushClip(chart.PlotArea);
                            for (var index = 0; index < chart.Series.Count; index++)
                            {
                                var series = chart.Series[index];
                                var spacing = Math.Clamp(chart.PlotArea.Width / corpus.Points[index].Length * .72, 1, 120);
                                series.Renderer.Render(context, new(series, series.CreateSnapshot(), projection, brush, index, chart.Series.Count, spacing));
                            }
                        }
                    }
                    foreach (var dpi in new[] { 96, 144 })
                    {
                        var id = $"{kind}-{viewport}-{paint}-{content}-dpi{dpi}";
                        var transparent = content == "transparent-series";
                        var original = Raster(new Replay(group, false, backdrop: transparent), dpi, id + "-group");
                        foreach (var visualBrush in new[] { false, true })
                        {
                        var variant = visualBrush ? "visual-brush" : "drawing-brush";
                        var wrapped = Raster(new Replay(group, true, visualBrush, transparent), dpi, id + "-" + variant);
                        var changed = 0; var maximum = 0;
                        for (var pixel = 0; pixel < original.Pixels.Length; pixel += 4)
                        {
                            var delta = 0;
                            for (var channel = 0; channel < 4; channel++)
                                delta = Math.Max(delta, Math.Abs(original.Pixels[pixel + channel] - wrapped.Pixels[pixel + channel]));
                            if (delta == 0) continue;
                            changed++; maximum = Math.Max(maximum, delta);
                        }
                        var recorded = new DrawingGroup();
                        using (var context = recorded.Open()) new Replay(group, true, visualBrush).Render(context);
                        equivalent &= changed == 0;
                        comparisons.Add(new { id, variant, kind, viewport, paint, content, dpi, inputCount = corpus.Manifest.InputPointCount,
                            corpus.Manifest.SeriesPointCounts, sourceBounds = Bounds(group.GetBounds()),
                            original = original.Fingerprint, wrapped = wrapped.Fingerprint,
                            originalLeaves = Leaves(group), wrappedLeaves = Leaves(recorded),
                            changedPixels = changed, maximumChannelDelta = maximum, equivalent = changed == 0 });
                        Console.WriteLine($"C7 drawing brush {id}/{variant}: changedPixels={changed}; maxDelta={maximum}; leaves={Leaves(group)} -> {Leaves(recorded)}");
                        }
                    }
                }
                window.Content = null;
            }
            completed = true; _passed = equivalent;
        }
        catch (Exception exception) { error = exception.ToString(); Console.Error.WriteLine(error); }
        finally
        {
            File.WriteAllText(Path.Combine(_output, "drawing-brush-experiment.json"), JsonSerializer.Serialize(new
            {
                schema = "charts-c703-drawing-brush-experiment/v1", completed, passed = _passed, equivalent, error,
                scope = "Untimed exact BGRA comparison: recorded vector group replay versus DrawingBrush-wrapped vector group, with full opaque chart and transparent series variants. Does not enable a production path, establish native-direct parity or change the C7 performance protocol.",
                canvasDip = new[] { Width, Height }, caseCount = comparisons.Count,
                os = RuntimeInformation.OSDescription, runtime = Environment.Version.ToString(),
                harness = Fingerprint(typeof(C7DrawingBrushExperiment).Assembly.Location), charts = Fingerprint(typeof(CartesianChart).Assembly.Location),
                avalonia = Fingerprint(typeof(DrawingContext).Assembly.Location), comparisons,
            }, Json));
            desktop.Shutdown(_passed ? 0 : 1);
        }
    }

    private static double[] Bounds(Rect rect) => [rect.X, rect.Y, rect.Width, rect.Height];
    private static int Leaves(Drawing drawing) => drawing is DrawingGroup group ? group.Children.Sum(Leaves) : 1;
    private static Captured Raster(Control visual, int dpi, string id)
    {
        visual.Width = Width; visual.Height = Height;
        visual.Measure(new Size(Width, Height)); visual.Arrange(new Rect(0, 0, Width, Height));
        var size = new PixelSize(Width * dpi / 96, Height * dpi / 96);
        using var bitmap = new RenderTargetBitmap(size, new Vector(dpi, dpi)); bitmap.Render(visual);
        var relative = id + ".png"; var path = Path.Combine(_output, relative);
        using (var stream = File.Open(path, FileMode.CreateNew)) bitmap.Save(stream);
        using var converted = new WriteableBitmap(size, new Vector(dpi, dpi), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var frame = converted.Lock(); bitmap.CopyPixels(frame);
        var raw = new byte[checked(size.Width * size.Height * 4)];
        for (var y = 0; y < size.Height; y++) Marshal.Copy(frame.Address + y * frame.RowBytes, raw, y * size.Width * 4, size.Width * 4);
        return new(raw, new { path = relative, pixelWidth = size.Width, pixelHeight = size.Height, bytes = new FileInfo(path).Length,
            sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), pixelSha256 = Convert.ToHexString(SHA256.HashData(raw)) });
    }
    private static object Fingerprint(string path) => new { path, bytes = new FileInfo(path).Length, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) };
    private sealed record Captured(byte[] Pixels, object Fingerprint);
    private sealed record Row(int Id, double X, double Y);

    private sealed class Replay(DrawingGroup group, bool wrapped, bool visualBrush = false, bool backdrop = false) : Control
    {
        public override void Render(DrawingContext context)
        {
            base.Render(context);
            var rectangle = new Rect(0, 0, C7DrawingBrushExperiment.Width, C7DrawingBrushExperiment.Height);
            if (backdrop)
            {
                context.DrawRectangle(Brushes.White, null, rectangle);
                for (var x = 0; x < C7DrawingBrushExperiment.Width; x += 120)
                    context.DrawRectangle(x % 240 == 0 ? Brushes.AliceBlue : Brushes.Beige, null, new Rect(x, 0, 120, C7DrawingBrushExperiment.Height));
                var grid = new Pen(new SolidColorBrush(Color.FromArgb(117, 71, 83, 97)), .75);
                for (var y = 0; y < C7DrawingBrushExperiment.Height; y += 40)
                    context.DrawLine(grid, new Point(0, y + .375), new Point(C7DrawingBrushExperiment.Width, y + .375));
            }
            if (!wrapped) { group.Draw(context); return; }
            using var sampling = backdrop ? context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }) : default;
            TileBrush brush;
            if (visualBrush)
            {
                var visual = new PrivateReplay(group) { Width = C7DrawingBrushExperiment.Width, Height = C7DrawingBrushExperiment.Height };
                visual.Measure(rectangle.Size); visual.Arrange(rectangle);
                brush = new VisualBrush(visual);
            }
            else brush = new DrawingBrush(group);
            brush.Stretch = Stretch.None; brush.TileMode = TileMode.None;
            brush.AlignmentX = AlignmentX.Left; brush.AlignmentY = AlignmentY.Top;
            brush.SourceRect = new RelativeRect(rectangle, RelativeUnit.Absolute);
            brush.DestinationRect = new RelativeRect(rectangle, RelativeUnit.Absolute);
            context.DrawRectangle(brush, null, rectangle);
        }
    }
    private sealed class PrivateReplay(DrawingGroup group) : Control
    {
        public override void Render(DrawingContext context) { base.Render(context); group.Draw(context); }
    }
    private sealed class Projection(Rect plot, ChartViewport range) : IChartProjection
    {
        public Rect PlotArea => plot;
        public double MapX(double x) => plot.Left + (x - range.XMinimum) / (range.XMaximum - range.XMinimum) * plot.Width;
        public double MapY(double y) => plot.Bottom - (y - range.YMinimum) / (range.YMaximum - range.YMinimum) * plot.Height;
        public double YOrigin => MapY(0);
        public Point Project(ChartPoint point) => new(MapX(point.X), MapY(point.Y));
        public ChartPoint Unproject(Point position) => new(range.XMinimum + (position.X - plot.Left) / plot.Width * (range.XMaximum - range.XMinimum), range.YMinimum + (plot.Bottom - position.Y) / plot.Height * (range.YMaximum - range.YMinimum));
    }
    public sealed class BrushExperimentApplication : Application
    {
        public override void Initialize() { RequestedThemeVariant = ThemeVariant.Light; Styles.Add(new FluentTheme()); Styles.Add(new ChartsTheme()); }
        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var window = new Window { Width = Width, Height = Height, CanResize = false, Title = "C7-03 vector drawing-brush experiment", Background = Brushes.White };
                desktop.MainWindow = window; window.Opened += (_, _) => Dispatcher.UIThread.Post(() => Capture(window, desktop)); window.Show();
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}
#endif
