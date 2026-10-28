#if C7_PERFORMANCE_DRIVER
using System.Diagnostics;
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

// Untimed quality plus short diagnostic-only raster timings. These samples
// never enter the fixed-load C7 protocol or relax its original pixel guards.
internal static class C7NativeGeometryExperiment
{
    private const int CanvasWidth = 1200, CanvasHeight = 650;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static string _output = null!;
    private static bool _passed;
    internal static int Run(string[] args)
    {
        if (args.Length != 3 || args[0] != "--c7-native-geometry-experiment" || args[1] != "--output")
            throw new ArgumentException("Expected --c7-native-geometry-experiment --output <new-directory>.");
        _output = Path.GetFullPath(args[2]);
        if (Directory.Exists(_output) || File.Exists(_output)) throw new IOException("Experiment output must be new.");
        Directory.CreateDirectory(_output); ChartNativeTextBinding.Initialize();
        var builder = AppBuilder.Configure<GeometryExperimentApplication>().WithChartsSkiaRendering().UsePlatformDetect();
        if (OperatingSystem.IsWindows()) builder.With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Software] });
        else builder.With(new X11PlatformOptions { RenderingMode = [X11RenderingMode.Software] });
        builder.StartWithClassicDesktopLifetime([], ShutdownMode.OnExplicitShutdown);
        return _passed ? 0 : 1;
    }
    private static void Capture(Window window, IClassicDesktopStyleApplicationLifetime desktop)
    {
        var comparisons = new List<object>(); string? error = null; var completed = false; var equivalent = true;
        try
        {
            var corpus = DatasetFactory.Create(DatasetFactory.Definitions.Single(d => d.Id == "S10-S"));
            var points = corpus.Points[0];
            var xmin = points.Min(p => p.X) - 1; var xmax = points.Max(p => p.X) + 1;
            var ymin = Math.Min(0, points.Min(p => p.Y)) - 1; var ymax = Math.Max(0, points.Max(p => p.Y + .25)) + 1;
            foreach (var viewport in new[] { "full", "zoom" })
            foreach (var paint in new[] { "opaque", "alpha", "gradient" })
            {
                Brush fill = paint switch
                {
                    "alpha" => new SolidColorBrush(Color.FromArgb(137, 21, 92, 193)) { Opacity = .75 },
                    "gradient" => new LinearGradientBrush { GradientStops = [new(Colors.CornflowerBlue, 0), new(Color.FromArgb(119, 240, 31, 73), .375), new(Colors.DarkGreen, 1)], Opacity = .625 },
                    _ => new SolidColorBrush(Color.Parse("#155CC1")),
                };
                var pen = new Pen(fill, 2);
                var chart = new CartesianChart { Width = CanvasWidth, Height = CanvasHeight, Background = Brushes.White,
                    FontFamily = new FontFamily("DejaVu Sans, Segoe UI"), FontSize = 12, ShowLegend = false, AnimationTriggers = ChartAnimationTriggers.None,
                    XAxis = new NumericAxis { Minimum = xmin, Maximum = xmax, DesiredTickCount = 6 },
                    YAxis = new NumericAxis { Minimum = ymin, Maximum = ymax, DesiredTickCount = 6 } };
                chart.Series.Add(new ScatterSeries { ItemsSource = points, Fill = fill, Stroke = fill, MarkerSize = 4, StrokeThickness = 2 });
                if (viewport == "zoom") chart.Viewport = new(xmin + (xmax - xmin) * .25, xmin + (xmax - xmin) * .35, ymin, ymax);
                window.Content = chart; window.UpdateLayout(); var plot = chart.PlotArea; var range = chart.VisibleRange!.Value;
                var centers = points.Select(p => new Point(plot.Left + (p.X - range.XMinimum) / (range.XMaximum - range.XMinimum) * plot.Width,
                    plot.Bottom - (p.Y - range.YMinimum) / (range.YMaximum - range.YMinimum) * plot.Height)).Where(p => plot.Inflate(2).Contains(p)).ToArray();
                var allocated = GC.GetAllocatedBytesForCurrentThread(); var prepareStart = Stopwatch.GetTimestamp();
                // Center/Radius assignment avoids the Rect->Center round trip.
                var ellipses = centers.Select(center => new EllipseGeometry { Center = center, RadiusX = 2, RadiusY = 2 }).ToArray();
                foreach (var ellipse in ellipses) _ = ellipse.Bounds;
                var geometryPrepareMs = Stopwatch.GetElapsedTime(prepareStart).TotalMilliseconds;
                var geometryPrepareBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
                allocated = GC.GetAllocatedBytesForCurrentThread(); prepareStart = Stopwatch.GetTimestamp();
                var outlines = ellipses.Select(ellipse => ellipse.GetWidenedGeometry(pen)).ToArray();
                foreach (var outline in outlines) _ = outline.Bounds;
                var outlinePrepareMs = Stopwatch.GetElapsedTime(prepareStart).TotalMilliseconds;
                var outlinePrepareBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
                foreach (var dpi in new[] { 96, 144 })
                {
                    var id = $"scatter-{viewport}-{paint}-dpi{dpi}";
                    var original = Raster(new PointPaint(centers, ellipses, outlines, plot, fill, pen, "ellipse"), dpi, id + "-ellipse");
                    foreach (var mode in new[] { "geometry", "widened-outline" })
                    {
                        var candidate = Raster(new PointPaint(centers, ellipses, outlines, plot, fill, pen, mode), dpi, id + "-" + mode);
                        var changed = 0; var maxDelta = 0;
                        for (var index = 0; index < original.Pixels.Length; index += 4)
                        {
                            var delta = 0;
                            for (var channel = 0; channel < 4; channel++) delta = Math.Max(delta, Math.Abs(original.Pixels[index + channel] - candidate.Pixels[index + channel]));
                            if (delta == 0) continue;
                            changed++; maxDelta = Math.Max(maxDelta, delta);
                        }
                        equivalent &= changed == 0;
                        comparisons.Add(new { id, mode, viewport, paint, dpi, inputCount = points.Length, renderedCount = centers.Length,
                            geometryPrepareMs, geometryPrepareBytes, outlinePrepareMs, outlinePrepareBytes,
                            original = original.Fingerprint, candidate = candidate.Fingerprint,
                            originalRasterMs = original.RasterMs, candidateRasterMs = candidate.RasterMs,
                            originalRasterP50Ms = Quantile(original.RasterMs, .5), candidateRasterP50Ms = Quantile(candidate.RasterMs, .5),
                            originalRasterP95Ms = Quantile(original.RasterMs, .95), candidateRasterP95Ms = Quantile(candidate.RasterMs, .95),
                            changedPixels = changed, maximumChannelDelta = maxDelta, equivalent = changed == 0 });
                        Console.WriteLine($"C7 native geometry {id}/{mode}: changed={changed}; delta={maxDelta}; rasterP50={Quantile(original.RasterMs,.5):F4} -> {Quantile(candidate.RasterMs,.5):F4}ms");
                    }
                }
                window.Content = null;
            }
            completed = true; _passed = equivalent;
        }
        catch (Exception exception) { error = exception.ToString(); Console.Error.WriteLine(error); }
        finally
        {
            File.WriteAllText(Path.Combine(_output, "native-geometry-experiment.json"), JsonSerializer.Serialize(new
            {
                schema = "charts-c703-native-geometry-experiment/v1", completed, passed = _passed, equivalent, error,
                scope = "Exact BGRA comparison of ordered DrawEllipse fill+stroke against direct-center EllipseGeometry fill+stroke and separate widened stroke fill. Same colors, center coordinates, clipping and backing grid; no merged fill/stroke or data sampling. Short 5 warmup/10 raster samples are diagnostic-only; construction costs recorded separately, not a formal throughput or tail SLA.",
                canvasDip = new[] { CanvasWidth, CanvasHeight }, diagnosticWarmupCount = 5, diagnosticSamplesPerMode = 10,
                os = RuntimeInformation.OSDescription, runtime = Environment.Version.ToString(), stopwatchFrequency = Stopwatch.Frequency,
                harness = Fingerprint(typeof(C7NativeGeometryExperiment).Assembly.Location), charts = Fingerprint(typeof(CartesianChart).Assembly.Location),
                avalonia = Fingerprint(typeof(DrawingContext).Assembly.Location), comparisons,
            }, Json));
            desktop.Shutdown(_passed ? 0 : 1);
        }
    }
    private static double Quantile(double[] values, double fraction) { var ordered = values.Order().ToArray(); return ordered[(int)Math.Ceiling(ordered.Length * fraction) - 1]; }
    private static Captured Raster(Control visual, int dpi, string id)
    {
        visual.Width = CanvasWidth; visual.Height = CanvasHeight;
        visual.Measure(new Size(CanvasWidth, CanvasHeight)); visual.Arrange(new Rect(0, 0, CanvasWidth, CanvasHeight));
        var size = new PixelSize(CanvasWidth * dpi / 96, CanvasHeight * dpi / 96);
        using var bitmap = new RenderTargetBitmap(size, new Vector(dpi, dpi));
        for (var i = 0; i < 5; i++) bitmap.Render(visual);
        var times = new double[10];
        for (var i = 0; i < times.Length; i++) { var start = Stopwatch.GetTimestamp(); bitmap.Render(visual); times[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds; }
        var relative = id + ".png"; var path = Path.Combine(_output, relative);
        using (var stream = File.Open(path, FileMode.CreateNew)) bitmap.Save(stream);
        using var converted = new WriteableBitmap(size, new Vector(dpi, dpi), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var frame = converted.Lock(); bitmap.CopyPixels(frame);
        var raw = new byte[checked(size.Width * size.Height * 4)];
        for (var y = 0; y < size.Height; y++) Marshal.Copy(frame.Address + y * frame.RowBytes, raw, y * size.Width * 4, size.Width * 4);
        return new(raw, times, new { path = relative, pixelWidth = size.Width, pixelHeight = size.Height, bytes = new FileInfo(path).Length,
            sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), pixelSha256 = Convert.ToHexString(SHA256.HashData(raw)) });
    }
    private static object Fingerprint(string path) => new { path, bytes = new FileInfo(path).Length, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) };
    private sealed record Captured(byte[] Pixels, double[] RasterMs, object Fingerprint);
    private sealed class PointPaint(Point[] centers, EllipseGeometry[] geometry, Geometry[] outlines, Rect plot, IBrush fill, Pen pen, string mode) : Control
    {
        public override void Render(DrawingContext context)
        {
            base.Render(context);
            context.DrawRectangle(Brushes.White, null, new Rect(0, 0, CanvasWidth, CanvasHeight));
            for (var x = 0; x < CanvasWidth; x += 120)
                context.DrawRectangle(x % 240 == 0 ? Brushes.AliceBlue : Brushes.Beige, null, new Rect(x, 0, 120, CanvasHeight));
            var grid = new Pen(new SolidColorBrush(Color.FromArgb(117, 71, 83, 97)), .75);
            for (var y = 0; y < CanvasHeight; y += 40) context.DrawLine(grid, new Point(0, y + .375), new Point(CanvasWidth, y + .375));
            using var clip = context.PushClip(plot);
            for (var i = 0; i < centers.Length; i++)
            {
                if (mode == "ellipse") context.DrawEllipse(fill, pen, centers[i], 2, 2);
                else if (mode == "geometry") context.DrawGeometry(fill, pen, geometry[i]);
                else { context.DrawGeometry(fill, null, geometry[i]); context.DrawGeometry(pen.Brush, null, outlines[i]); }
            }
        }
    }
    public sealed class GeometryExperimentApplication : Application
    {
        public override void Initialize() { RequestedThemeVariant = ThemeVariant.Light; Styles.Add(new FluentTheme()); Styles.Add(new ChartsTheme()); }
        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var window = new Window { Width = CanvasWidth, Height = CanvasHeight, CanResize = false, Title = "C7-03 native geometry experiment", Background = Brushes.White };
                desktop.MainWindow = window; window.Opened += (_, _) => Dispatcher.UIThread.Post(() => Capture(window, desktop)); window.Show();
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}
#endif
