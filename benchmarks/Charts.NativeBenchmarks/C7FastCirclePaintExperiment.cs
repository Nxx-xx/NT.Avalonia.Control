#if C7_PERFORMANCE_DRIVER
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using Charts.Benchmarks;
using NT.AvaloniaControl.Charts;
using NT.AvaloniaControl.Charts.Skia;
using SkiaSharp;

namespace Charts.NativeBenchmarks;

// An isolated rejection experiment. Production rendering does not use this
// public-getter bridge or the direct SKCanvas command path.
internal static class C7FastCirclePaintExperiment
{
    private const int Width = 1200, Height = 650;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly ConcurrentDictionary<Type, PropertyInfo> FillPathGetters = new();
    private static readonly ConcurrentDictionary<Type, PropertyInfo> RenderOptionsGetters = new();
    private static readonly PropertyInfo PlatformGeometry = FixtureOnlyPlatformGetter();
    private static string _output = null!;
    private static bool _passed, _saveLayer;
    private static long _directFrames, _guardedDirectFrames, _guardedFallbackFrames;

    internal static int Run(string[] args)
    {
        if (args.Length is not (3 or 4) || args[0] != "--c7-fast-circle-paint-experiment" || args[1] != "--output" ||
            (args.Length == 4 && args[3] != "--opacity-save-layer"))
            throw new ArgumentException("Expected --c7-fast-circle-paint-experiment --output <new-directory> [--opacity-save-layer].");
        _output = Path.GetFullPath(args[2]); _saveLayer = args.Length == 4;
        if (Directory.Exists(_output) || File.Exists(_output)) throw new IOException("Experiment output must be new.");
        Directory.CreateDirectory(_output); ChartNativeTextBinding.Initialize();
        var builder = AppBuilder.Configure<FastPaintApplication>().WithChartsSkiaRendering().UsePlatformDetect()
            .With(new SkiaOptions { UseOpacitySaveLayer = _saveLayer });
        if (OperatingSystem.IsWindows()) builder.With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Software] });
        else builder.With(new X11PlatformOptions { RenderingMode = [X11RenderingMode.Software] });
        builder.StartWithClassicDesktopLifetime([], ShutdownMode.OnExplicitShutdown);
        return _passed ? 0 : 1;
    }

    private static void Capture(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var comparisons = new List<object>(); string? error = null; var completed = false; var equivalent = true;
        DatasetManifest? manifest = null;
        try
        {
            var corpus = DatasetFactory.Create(DatasetFactory.Definitions.Single(d => d.Id == "S10-S")); manifest = corpus.Manifest;
            var plot = new Rect(77.212890625, 20, 1100.787109375, 581.0390625);
            var source = corpus.Points[0];
            var xmin = source.Min(p => p.X) - 1; var xmax = source.Max(p => p.X) + 1;
            var ymin = Math.Min(0, source.Min(p => p.Y)) - 1; var ymax = source.Max(p => p.Y + .25) + 1;
            var full = source.Select(p => new Point(plot.Left + (p.X - xmin) / (xmax - xmin) * plot.Width,
                plot.Bottom - (p.Y - ymin) / (ymax - ymin) * plot.Height)).ToArray();
            var shapes = new Dictionary<(string Pattern, double Radius), ShapeSet>();
            foreach (var pattern in new[] { "c0-full-10k", "overlap-float-boundaries" })
            foreach (var shape in new[] { (Radius: 2d, Thickness: 2d), (Radius: 2.6875, Thickness: 1.625) })
            {
                var points = pattern == "c0-full-10k" ? full : Overlaps();
                var ellipses = points.Select(p => new EllipseGeometry { Center = p, RadiusX = shape.Radius, RadiusY = shape.Radius }).ToArray();
                var rings = ellipses.Select(e => e.GetWidenedGeometry(new Pen(Brushes.Black, shape.Thickness))).ToArray();
                shapes.Add((pattern, shape.Radius), new(points, ellipses, rings, ellipses.Select(PathOf).ToArray(),
                    rings.Select(PathOf).ToArray(), shape.Radius, shape.Thickness));
            }
            foreach (var pattern in new[] { "c0-full-10k", "overlap-float-boundaries" })
            foreach (var radius in new[] { 2d, 2.6875 })
            foreach (var paint in new[] { "opaque", "alpha" })
            foreach (var edge in new[] { default(EdgeMode), EdgeMode.Aliased })
            foreach (var dpi in new[] { 96, 144 })
            foreach (var opacity in new[] { 1d, .375 })
            foreach (var fullOpacity in new[] { false, true })
            {
                var set = shapes[(pattern, radius)];
                var fill = new ImmutableSolidColorBrush(paint == "opaque" ? Color.Parse("#155CC1") : Color.FromArgb(131, 17, 83, 151), paint == "opaque" ? 1 : .75);
                var stroke = new ImmutableSolidColorBrush(paint == "opaque" ? Color.Parse("#155CC1") : Color.FromArgb(173, 201, 29, 73), paint == "opaque" ? 1 : .625);
                var pen = new Pen(stroke, set.Thickness);
                var options = new RenderOptions { EdgeMode = edge, RequiresFullOpacityHandling = fullOpacity, BitmapInterpolationMode = BitmapInterpolationMode.None };
                var id = $"{pattern}-r{radius.ToString(System.Globalization.CultureInfo.InvariantCulture)}-{paint}-{edge}-dpi{dpi}-o{opacity.ToString(System.Globalization.CultureInfo.InvariantCulture)}-full{fullOpacity}";
                var diagnostic = pattern == "c0-full-10k" && radius == 2 && paint == "opaque" && edge == default && dpi == 96 && opacity == 1 && !fullOpacity;
                var clip = pattern == "c0-full-10k" ? plot : new Rect(8, 8, Width - 16, Height - 16);
                var original = Raster(new PaintScene(set, clip, fill, pen, options, opacity, paint == "alpha", "ellipse"), dpi, id + "-ellipse", diagnostic);
                var native = Raster(new PaintScene(set, clip, fill, pen, options, opacity, paint == "alpha", "native-pair"), dpi, id + "-native-pair", diagnostic);
                var direct = Raster(new PaintScene(set, clip, fill, pen, options, opacity, paint == "alpha", "direct"), dpi, id + "-direct", diagnostic);
                var guarded = Raster(new PaintScene(set, clip, fill, pen, options, opacity, paint == "alpha", "guarded"), dpi, id + "-guarded", diagnostic);
                Compare(native, direct, "native-pair-vs-direct");
                Compare(original, guarded, "ellipse-vs-guarded");
                void Compare(Captured before, Captured after, string mode)
                {
                    var changed = 0; var delta = 0;
                    for (var index = 0; index < before.Pixels.Length; index += 4)
                    {
                        var pixelDelta = 0;
                        for (var c = 0; c < 4; c++) pixelDelta = Math.Max(pixelDelta, Math.Abs(before.Pixels[index + c] - after.Pixels[index + c]));
                        if (pixelDelta == 0) continue;
                        changed++; delta = Math.Max(delta, pixelDelta);
                    }
                    equivalent &= changed == 0;
                    comparisons.Add(new { id, mode, pattern, radius, thickness = set.Thickness, paint, edgeMode = edge.ToString(), dpi, opacity,
                        requiresFullOpacityHandling = fullOpacity, transparentBackdrop = paint == "alpha", renderedCount = set.Points.Length,
                        original = before.Fingerprint, candidate = after.Fingerprint, originalRasterMs = before.RasterMs, candidateRasterMs = after.RasterMs,
                        changedPixels = changed, maximumChannelDelta = delta, equivalent = changed == 0 });
                    Console.WriteLine($"C7 fast circle {id}/{mode}: changed={changed}; delta={delta}");
                }
            }
            completed = true; _passed = equivalent && _guardedDirectFrames > 0 && _guardedFallbackFrames > 0;
        }
        catch (Exception exception) { error = exception.ToString(); Console.Error.WriteLine(error); }
        finally
        {
            File.WriteAllText(Path.Combine(_output, "fast-circle-paint-experiment.json"), JsonSerializer.Serialize(new
            {
                schema = "charts-c703-fast-circle-paint-experiment/v1", completed, passed = _passed, equivalent, error,
                scope = "Untimed exact-pixel rejection experiment only. Fixture preparation reads the nonpublic Media.Geometry.PlatformImpl getter to recover the already-owned native fixture geometry. This fixture-only accessor is excluded from the proposed production bridge, which starts with factory-owned IGeometryImpl and reads only public FillPath/RenderOptions getters (some declared by internal pinned Skia types). No production path, workload or quality threshold changes. Native-pair/direct compares two source-ordered filled paths under the actual current state. Guarded-direct/original-ellipse additionally rejects non-unit raster state and ambient opacity != 1. Separate 5-warmup/10-sample diagnostic timing exists only for the canonical opaque/default 10K fixture.",
                useOpacitySaveLayer = _saveLayer, corpus = manifest, canvasDip = new[] { Width, Height },
                directFrames = _directFrames, guardedDirectFrames = _guardedDirectFrames, guardedFallbackFrames = _guardedFallbackFrames,
                fixtureOnlyPrivateAccessor = new { declaringType = PlatformGeometry.DeclaringType?.FullName, PlatformGeometry.Name,
                    getterPublic = PlatformGeometry.GetGetMethod(nonPublic: true)?.IsPublic, productionUse = false },
                publicGetterBridge = new {
                    paths = FillPathGetters.Values.Select(p => new { declaringType = p.DeclaringType?.FullName, p.Name, getterPublic = p.GetMethod?.IsPublic }).ToArray(),
                    options = RenderOptionsGetters.Values.Select(p => new { declaringType = p.DeclaringType?.FullName, p.Name, getterPublic = p.GetMethod?.IsPublic }).ToArray() },
                os = RuntimeInformation.OSDescription, harness = Fingerprint(typeof(C7FastCirclePaintExperiment).Assembly.Location),
                charts = Fingerprint(typeof(CartesianChart).Assembly.Location), skia = Fingerprint(typeof(ISkiaSharpApiLeaseFeature).Assembly.Location), comparisons,
            }, Json));
            desktop.Shutdown(_passed ? 0 : 1);
        }
    }

    private static PropertyInfo PublicGetter(Type type, string name)
    {
        var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
        if (property?.GetMethod?.IsPublic != true || property.GetIndexParameters().Length != 0)
            throw new NotSupportedException($"No public {name} getter on {type.FullName}.");
        return property;
    }

    // The experiment's public Media.Geometry fixtures own these objects. This
    // accessor is never used by production, whose factory already returns the
    // native IGeometryImpl and therefore needs no Media.Geometry private access.
    private static PropertyInfo FixtureOnlyPlatformGetter()
    {
        var property = typeof(Geometry).GetProperty("PlatformImpl", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property?.GetGetMethod(nonPublic: true) is null || property.GetIndexParameters().Length != 0)
            throw new NotSupportedException("No fixture-only Geometry.PlatformImpl accessor.");
        return property;
    }

    private static SKPath PathOf(Geometry geometry)
    {
        var native = PlatformGeometry.GetValue(geometry) ?? throw new InvalidOperationException("Null native geometry.");
        if (native.GetType().Assembly != typeof(ISkiaSharpApiLeaseFeature).Assembly) throw new NotSupportedException("Unexpected geometry backend.");
        var getter = FillPathGetters.GetOrAdd(native.GetType(), static type => PublicGetter(type, "FillPath"));
        if (getter.PropertyType != typeof(SKPath)) throw new NotSupportedException("Unexpected FillPath signature.");
        return (SKPath?)getter.GetValue(native) ?? throw new InvalidOperationException("Null fill path.");
    }

    private static Point[] Overlaps()
    {
        var values = new List<Point>();
        foreach (var x in new[] { 16d, 32, 64, 128, 256, 512, 1024, 1100 })
        foreach (var y in new[] { 32d, 64, 128, 256, 512 })
        foreach (var phase in new[] { -.00002, 0, .00002, .125, .25, .5, .875 })
        {
            var point = new Point(x + phase, y + phase * .75);
            values.Add(point); values.Add(point); values.Add(point + new Vector(-.375, .625));
            values.Add(point + new Vector(.75, -.125)); values.Add(point + new Vector(1.375, .375));
        }
        return values.ToArray();
    }

    private static Captured Raster(Control visual, int dpi, string id, bool diagnostic)
    {
        visual.Width = Width; visual.Height = Height; visual.Measure(new Size(Width, Height)); visual.Arrange(new Rect(0, 0, Width, Height));
        var size = new PixelSize(Width * dpi / 96, Height * dpi / 96);
        using var bitmap = new RenderTargetBitmap(size, new Vector(dpi, dpi));
        if (diagnostic) for (var i = 0; i < 5; i++) bitmap.Render(visual);
        var times = new double[diagnostic ? 10 : 1];
        for (var i = 0; i < times.Length; i++) { var started = Stopwatch.GetTimestamp(); bitmap.Render(visual); times[i] = Stopwatch.GetElapsedTime(started).TotalMilliseconds; }
        var relative = id + ".png"; var path = Path.Combine(_output, relative);
        using (var stream = File.Open(path, FileMode.CreateNew)) bitmap.Save(stream);
        using var converted = new WriteableBitmap(size, new Vector(dpi, dpi), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var frame = converted.Lock(); bitmap.CopyPixels(frame);
        var raw = new byte[checked(size.Width * size.Height * 4)];
        for (var y = 0; y < size.Height; y++) Marshal.Copy(frame.Address + y * frame.RowBytes, raw, y * size.Width * 4, size.Width * 4);
        return new(raw, times, new { path = relative, pixelWidth = size.Width, pixelHeight = size.Height,
            sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), pixelSha256 = Convert.ToHexString(SHA256.HashData(raw)) });
    }

    private static object Fingerprint(string path) => new { path, bytes = new FileInfo(path).Length, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) };
    private sealed record Captured(byte[] Pixels, double[] RasterMs, object Fingerprint);
    private sealed record ShapeSet(Point[] Points, EllipseGeometry[] Ellipses, Geometry[] Rings, SKPath[] Fills, SKPath[] Outlines, double Radius, double Thickness);

    private sealed class PaintScene(ShapeSet set, Rect clip, IImmutableBrush fill, Pen pen, RenderOptions options,
        double opacity, bool transparent, string mode) : Control
    {
        public override void Render(DrawingContext context)
        {
            base.Render(context);
            context.DrawRectangle(transparent ? Brushes.Transparent : Brushes.White, null, new Rect(0, 0, C7FastCirclePaintExperiment.Width, C7FastCirclePaintExperiment.Height));
            if (!transparent)
            {
                for (var x = 0; x < C7FastCirclePaintExperiment.Width; x += 120)
                    context.DrawRectangle(x % 240 == 0 ? Brushes.AliceBlue : Brushes.Beige, null, new Rect(x, 0, 120, C7FastCirclePaintExperiment.Height));
                var grid = new Pen(new ImmutableSolidColorBrush(Color.FromArgb(117, 71, 83, 97)), .75);
                for (var y = 0; y < C7FastCirclePaintExperiment.Height; y += 40)
                    context.DrawLine(grid, new Point(0, y + .375), new Point(C7FastCirclePaintExperiment.Width, y + .375));
            }
            using var renderOptions = context.PushRenderOptions(options);
            using var outerOpacity = context.PushOpacity(opacity);
            using var clipping = context.PushClip(clip);
            if (mode is "direct" or "guarded") { context.Custom(new DirectPaint(set, fill, pen.ToImmutable(), mode == "guarded")); return; }
            for (var i = 0; i < set.Points.Length; i++)
                if (mode == "ellipse") context.DrawEllipse(fill, pen, set.Points[i], set.Radius, set.Radius);
                else { context.DrawGeometry(fill, null, set.Ellipses[i]); context.DrawGeometry(pen.Brush, null, set.Rings[i]); }
        }
    }

    private sealed class DirectPaint(ShapeSet set, IImmutableBrush fill, ImmutablePen pen, bool guarded) : ICustomDrawOperation
    {
        public Rect Bounds => new(0, 0, Width, Height);
        public bool HitTest(Point point) => false;
        public bool Equals(ICustomDrawOperation? other) => ReferenceEquals(this, other);
        public void Dispose() { }
        public void Render(ImmediateDrawingContext context)
        {
            var native = context.PlatformImpl;
            var type = native.GetType();
            if (type.Assembly != typeof(ISkiaSharpApiLeaseFeature).Assembly || type.FullName != "Avalonia.Skia.DrawingContextImpl")
                throw new NotSupportedException("Unexpected native drawing context.");
            var property = RenderOptionsGetters.GetOrAdd(type, static t => PublicGetter(t, "RenderOptions"));
            if (property.PropertyType != typeof(RenderOptions)) throw new NotSupportedException("Unexpected RenderOptions signature.");
            var options = (RenderOptions)property.GetValue(native)!;
            var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>() ?? throw new NotSupportedException("No Skia lease.");
            var fallback = false;
            using (var lease = feature.Lease())
            {
                var m = lease.SkCanvas.TotalMatrix;
                var unit = lease.SkSurface is not null && lease.GrContext is null &&
                    m.ScaleX == 1 && m.ScaleY == 1 && m.SkewX == 0 && m.SkewY == 0 &&
                    m.TransX == 0 && m.TransY == 0 && m.Persp0 == 0 && m.Persp1 == 0 && m.Persp2 == 1;
                fallback = guarded && (!unit || lease.CurrentOpacity != 1);
                if (!fallback)
                {
                    using var fillPaint = Paint((ISolidColorBrush)fill, options, lease.CurrentOpacity);
                    using var strokePaint = Paint((ISolidColorBrush)pen.Brush!, options, lease.CurrentOpacity);
                    for (var index = 0; index < set.Points.Length; index++)
                    {
                        lease.SkCanvas.DrawPath(set.Fills[index], fillPaint);
                        lease.SkCanvas.DrawPath(set.Outlines[index], strokePaint);
                    }
                    if (guarded) _guardedDirectFrames++; else _directFrames++;
                }
            }
            if (!fallback) return;
            _guardedFallbackFrames++;
            foreach (var point in set.Points)
                context.DrawEllipse(fill, pen, point, set.Radius, set.Radius);
        }
        private static SKPaint Paint(ISolidColorBrush brush, RenderOptions options, double opacity)
        {
            var color = brush.Color;
            return new SKPaint { Style = SKPaintStyle.Fill, BlendMode = SKBlendMode.SrcOver,
                IsAntialias = options.EdgeMode != EdgeMode.Aliased,
                Color = new SKColor(color.R, color.G, color.B, (byte)(color.A * (brush.Opacity * opacity))) };
        }
    }

    public sealed class FastPaintApplication : Application
    {
        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var window = new Window { Width = 320, Height = 240, CanResize = false, Title = "C7-03 reusable Skia paint rejection experiment", Background = Brushes.White };
                desktop.MainWindow = window; window.Opened += (_, _) => Dispatcher.UIThread.Post(() => Capture(desktop)); window.Show();
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}
#endif
