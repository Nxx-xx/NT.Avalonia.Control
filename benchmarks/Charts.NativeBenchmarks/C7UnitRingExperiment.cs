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
using Avalonia.Threading;
using Charts.Benchmarks;
using NT.AvaloniaControl.Charts;
using NT.AvaloniaControl.Charts.Skia;

namespace Charts.NativeBenchmarks;

// Explicit 96-DPI rejection fixture. Unit-ring translation is not production
// enabled; a single unequal pixel rejects that optimization configuration.
internal static class C7UnitRingExperiment
{
    private const int Width = 1200, Height = 650;
    private static string _output = null!;
    private static bool _passed;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    internal static int Run(string[] args)
    {
        if (args.Length != 3 || args[0] != "--c7-unit-ring-experiment" || args[1] != "--output")
            throw new ArgumentException("Expected --c7-unit-ring-experiment --output <new-directory>.");
        _output = Path.GetFullPath(args[2]);
        if (Directory.Exists(_output) || File.Exists(_output)) throw new IOException("Experiment output must be new.");
        Directory.CreateDirectory(_output); ChartNativeTextBinding.Initialize();
        var builder = AppBuilder.Configure<UnitRingApplication>().WithChartsSkiaRendering().UsePlatformDetect();
        if (OperatingSystem.IsWindows()) builder.With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Software] });
        else builder.With(new X11PlatformOptions { RenderingMode = [X11RenderingMode.Software] });
        builder.StartWithClassicDesktopLifetime([], ShutdownMode.OnExplicitShutdown);
        return _passed ? 0 : 1;
    }

    private static void Capture(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var comparisons = new List<object>(); string? error = null; var completed = false; var equivalent = true;
        DatasetManifest? manifest = null; var overlapPointCount = 0;
        try
        {
            var corpus = DatasetFactory.Create(DatasetFactory.Definitions.Single(d => d.Id == "S10-S"));
            manifest = corpus.Manifest;
            var plot = new Rect(77.212890625, 20, 1100.787109375, 581.0390625);
            var source = corpus.Points[0];
            var xmin = source.Min(p => p.X) - 1; var xmax = source.Max(p => p.X) + 1;
            var ymin = Math.Min(0, source.Min(p => p.Y)) - 1; var ymax = source.Max(p => p.Y + .25) + 1;
            var c0 = source.Select(p => new Point(plot.Left + (p.X - xmin) / (xmax - xmin) * plot.Width,
                plot.Bottom - (p.Y - ymin) / (ymax - ymin) * plot.Height)).ToArray();
            var overlaps = Overlaps();
            overlapPointCount = overlaps.Length;
            foreach (var pattern in new[] { "c0-full-10k", "overlap-float-boundaries" })
            foreach (var shape in new[] { (Radius: 2d, Thickness: 2d), (Radius: 2.6875, Thickness: 1.625) })
            foreach (var paint in new[] { "opaque", "alpha" })
            {
                var points = pattern == "c0-full-10k" ? c0 : overlaps;
                var clip = pattern == "c0-full-10k" ? plot : new Rect(8, 8, Width - 16, Height - 16);
                IBrush fill = new SolidColorBrush(paint == "opaque" ? Color.Parse("#155CC1") : Color.FromArgb(131, 17, 83, 151), paint == "opaque" ? 1 : .75);
                IBrush stroke = new SolidColorBrush(paint == "opaque" ? Color.Parse("#155CC1") : Color.FromArgb(173, 201, 29, 73), paint == "opaque" ? 1 : .625);
                var pen = new Pen(stroke, shape.Thickness);
                var allocated = GC.GetAllocatedBytesForCurrentThread(); var started = Stopwatch.GetTimestamp();
                var ellipse = points.Select(p => new EllipseGeometry { Center = p, RadiusX = shape.Radius, RadiusY = shape.Radius }).ToArray();
                var rings = ellipse.Select(e => e.GetWidenedGeometry(pen)).ToArray();
                foreach (var e in ellipse) _ = e.Bounds;
                foreach (var ring in rings) _ = ring.Bounds;
                var perPointPrepareMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                var perPointPrepareAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
                allocated = GC.GetAllocatedBytesForCurrentThread(); started = Stopwatch.GetTimestamp();
                var unit = new EllipseGeometry { Center = default, RadiusX = shape.Radius, RadiusY = shape.Radius };
                var unitRing = unit.GetWidenedGeometry(pen); _ = unit.Bounds; _ = unitRing.Bounds;
                var unitPrepareMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                var unitPrepareAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
                foreach (var edge in new[] { default(EdgeMode), EdgeMode.Aliased })
                foreach (var translation in new[] { new Point(0, 0), new Point(.25, .75), new Point(2, -3) })
                {
                    var id = $"{pattern}-r{shape.Radius.ToString(System.Globalization.CultureInfo.InvariantCulture)}-t{shape.Thickness.ToString(System.Globalization.CultureInfo.InvariantCulture)}-{paint}-{edge}-x{translation.X.ToString(System.Globalization.CultureInfo.InvariantCulture)}-y{translation.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                    var original = Raster(new RingScene(points, ellipse, rings, unit, unitRing, clip, fill, pen, shape.Radius, edge, translation, "ellipse"), id + "-ellipse");
                    foreach (var mode in new[] { "per-point", "unit" })
                    {
                        var candidate = Raster(new RingScene(points, ellipse, rings, unit, unitRing, clip, fill, pen, shape.Radius, edge, translation, mode), id + "-" + mode);
                        var changed = 0; var maxDelta = 0;
                        for (var offset = 0; offset < original.Pixels.Length; offset += 4)
                        {
                            var delta = 0;
                            for (var channel = 0; channel < 4; channel++) delta = Math.Max(delta, Math.Abs(original.Pixels[offset + channel] - candidate.Pixels[offset + channel]));
                            if (delta == 0) continue;
                            changed++; maxDelta = Math.Max(maxDelta, delta);
                        }
                        equivalent &= changed == 0;
                        comparisons.Add(new { id, mode, dpi = 96, pattern, renderedCount = points.Length, radius = shape.Radius, thickness = shape.Thickness,
                            paint, edgeMode = edge.ToString(), translation = new[] { translation.X, translation.Y },
                            perPointPrepareMs, perPointPrepareAllocatedBytes, unitPrepareMs, unitPrepareAllocatedBytes,
                            original = original.Fingerprint, candidate = candidate.Fingerprint,
                            originalSingleRasterMs = original.SingleRasterMs, candidateSingleRasterMs = candidate.SingleRasterMs,
                            changedPixels = changed, maximumChannelDelta = maxDelta, equivalent = changed == 0 });
                        Console.WriteLine($"C7 unit ring {id}/{mode}: changed={changed}; delta={maxDelta}");
                    }
                }
            }
            completed = true; _passed = equivalent;
        }
        catch (Exception exception) { error = exception.ToString(); Console.Error.WriteLine(error); }
        finally
        {
            File.WriteAllText(Path.Combine(_output, "unit-ring-experiment.json"), JsonSerializer.Serialize(new
            {
                schema = "charts-c703-unit-ring-experiment/v1", completed, passed = _passed, equivalent, error,
                scope = "96-DPI exact pixel rejection fixture for EdgeMode/default and Aliased, fractional/integer outer translation, marker 4/2 and 5.375/1.625, C0 10K and coincident/overlap/float-boundary data. Compare original DrawEllipse, per-point widened geometry, and one zero-centered ring translated per point. Separate fill/stroke paint and order preserved. One raster duration per image is descriptive only, never formal performance evidence.",
                canvasDip = new[] { Width, Height }, dpi = 96, corpus = manifest, overlapPointCount,
                os = RuntimeInformation.OSDescription, harness = Fingerprint(typeof(C7UnitRingExperiment).Assembly.Location),
                charts = Fingerprint(typeof(CartesianChart).Assembly.Location), comparisons,
            }, Json));
            desktop.Shutdown(_passed ? 0 : 1);
        }
    }

    private static Point[] Overlaps()
    {
        var values = new List<Point>();
        foreach (var x in new[] { 16d, 32, 64, 128, 256, 512, 1024, 1100 })
        foreach (var y in new[] { 32d, 64, 128, 256, 512 })
        foreach (var phase in new[] { -.00002, 0, .00002, .125, .25, .5, .875 })
        {
            var point = new Point(x + phase, y + phase * .75);
            values.Add(point); values.Add(point);
            values.Add(point + new Vector(-.375, .625)); values.Add(point + new Vector(.75, -.125));
            values.Add(point + new Vector(1.375, .375));
        }
        return values.ToArray();
    }
    private static Captured Raster(Control visual, string id)
    {
        visual.Width = Width; visual.Height = Height; visual.Measure(new Size(Width, Height)); visual.Arrange(new Rect(0, 0, Width, Height));
        var size = new PixelSize(Width, Height);
        using var bitmap = new RenderTargetBitmap(size, new Vector(96, 96));
        var started = Stopwatch.GetTimestamp(); bitmap.Render(visual); var milliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        var relative = id + ".png"; var path = Path.Combine(_output, relative);
        using (var stream = File.Open(path, FileMode.CreateNew)) bitmap.Save(stream);
        using var converted = new WriteableBitmap(size, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var frame = converted.Lock(); bitmap.CopyPixels(frame);
        var raw = new byte[checked(Width * Height * 4)];
        for (var y = 0; y < Height; y++) Marshal.Copy(frame.Address + y * frame.RowBytes, raw, y * Width * 4, Width * 4);
        return new(raw, milliseconds, new { path = relative, bytes = new FileInfo(path).Length,
            sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), pixelSha256 = Convert.ToHexString(SHA256.HashData(raw)) });
    }
    private static object Fingerprint(string path) => new { path, bytes = new FileInfo(path).Length, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) };
    private sealed record Captured(byte[] Pixels, double SingleRasterMs, object Fingerprint);
    private sealed class RingScene(Point[] centers, EllipseGeometry[] ellipses, Geometry[] rings, EllipseGeometry unit, Geometry unitRing,
        Rect clip, IBrush fill, Pen pen, double radius, EdgeMode edge, Point offset, string mode) : Control
    {
        public override void Render(DrawingContext context)
        {
            base.Render(context);
            context.DrawRectangle(Brushes.White, null, new Rect(0, 0, C7UnitRingExperiment.Width, C7UnitRingExperiment.Height));
            for (var x = 0; x < C7UnitRingExperiment.Width; x += 120)
                context.DrawRectangle(x % 240 == 0 ? Brushes.AliceBlue : Brushes.Beige, null, new Rect(x, 0, 120, C7UnitRingExperiment.Height));
            var grid = new Pen(new SolidColorBrush(Color.FromArgb(117, 71, 83, 97)), .75);
            for (var y = 0; y < C7UnitRingExperiment.Height; y += 40) context.DrawLine(grid, new Point(0, y + .375), new Point(C7UnitRingExperiment.Width, y + .375));
            using var options = context.PushRenderOptions(new RenderOptions { EdgeMode = edge });
            using var translation = context.PushTransform(Matrix.CreateTranslation(offset.X, offset.Y));
            using var clipping = context.PushClip(clip);
            for (var i = 0; i < centers.Length; i++)
            {
                if (mode == "ellipse") context.DrawEllipse(fill, pen, centers[i], radius, radius);
                else if (mode == "per-point") { context.DrawGeometry(fill, null, ellipses[i]); context.DrawGeometry(pen.Brush, null, rings[i]); }
                else
                {
                    using var position = context.PushTransform(Matrix.CreateTranslation(centers[i].X, centers[i].Y));
                    context.DrawGeometry(fill, null, unit); context.DrawGeometry(pen.Brush, null, unitRing);
                }
            }
        }
    }
    public sealed class UnitRingApplication : Application
    {
        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var window = new Window { Width = 320, Height = 240, CanResize = false, Title = "C7-03 unit ring rejection experiment", Background = Brushes.White };
                desktop.MainWindow = window; window.Opened += (_, _) => Dispatcher.UIThread.Post(() => Capture(desktop)); window.Show();
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}
#endif
