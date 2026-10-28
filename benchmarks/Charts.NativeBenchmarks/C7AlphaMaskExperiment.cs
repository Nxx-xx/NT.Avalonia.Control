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

// Isolated coverage-mask rejection experiment. No production renderer uses it.
internal static class C7AlphaMaskExperiment
{
    private const int Width = 1200, Height = 650, MaximumMaskPixels = 256;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly ConcurrentDictionary<Type, PropertyInfo> PathGetters = new(), OptionsGetters = new();
    private static readonly PropertyInfo PlatformGeometry = typeof(Geometry).GetProperty("PlatformImpl", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
    private static readonly SKSamplingOptions Nearest = new(SKFilterMode.Nearest, SKMipmapMode.None);
    private static readonly HashSet<string> Formats = [];
    private static string _output = null!;
    private static bool _passed, _saveLayer;
    private static long _maskFrames, _directFrames, _fallbackFrames, _masksCreated, _masksDisposed, _liveMasks, _maximumLiveMasks;

    internal static int Run(string[] args)
    {
        if (args.Length is not (3 or 4) || args[0] != "--c7-alpha-mask-experiment" || args[1] != "--output" ||
            (args.Length == 4 && args[3] != "--opacity-save-layer"))
            throw new ArgumentException("Expected --c7-alpha-mask-experiment --output <new-directory> [--opacity-save-layer].");
        _output = Path.GetFullPath(args[2]); _saveLayer = args.Length == 4;
        if (Directory.Exists(_output) || File.Exists(_output)) throw new IOException("Experiment output must be new.");
        Directory.CreateDirectory(_output); ChartNativeTextBinding.Initialize();
        var builder = AppBuilder.Configure<AlphaMaskApplication>().WithChartsSkiaRendering().UsePlatformDetect()
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
            var sets = new Dictionary<(string Pattern, double Radius), ShapeSet>();
            foreach (var pattern in new[] { "c0-full-10k", "overlap-float-boundaries" })
            foreach (var shape in new[] { (Radius: 2d, Thickness: 2d), (Radius: 2.6875, Thickness: 1.625) })
            {
                var points = pattern == "c0-full-10k" ? full : Overlaps();
                var ellipses = points.Select(p => new EllipseGeometry { Center = p, RadiusX = shape.Radius, RadiusY = shape.Radius }).ToArray();
                var rings = ellipses.Select(e => e.GetWidenedGeometry(new Pen(Brushes.Black, shape.Thickness))).ToArray();
                sets.Add((pattern, shape.Radius), new(points, ellipses, rings, ellipses.Select(PathOf).ToArray(), rings.Select(PathOf).ToArray(), shape.Radius, shape.Thickness));
            }
            foreach (var pattern in new[] { "c0-full-10k", "overlap-float-boundaries" })
            foreach (var radius in new[] { 2d, 2.6875 })
            foreach (var paint in new[] { "opaque", "alpha" })
            foreach (var edge in new[] { default(EdgeMode), EdgeMode.Aliased })
            foreach (var dpi in new[] { 96, 144 })
            foreach (var opacity in new[] { 1d, .375 })
            foreach (var fullOpacity in new[] { false, true })
            {
                var set = sets[(pattern, radius)];
                var fill = new ImmutableSolidColorBrush(paint == "opaque" ? Color.Parse("#155CC1") : Color.FromArgb(131, 17, 83, 151), paint == "opaque" ? 1 : .75);
                var stroke = new ImmutableSolidColorBrush(paint == "opaque" ? Color.Parse("#155CC1") : Color.FromArgb(173, 201, 29, 73), paint == "opaque" ? 1 : .625);
                var pen = new Pen(stroke, set.Thickness);
                var options = new RenderOptions { EdgeMode = edge, RequiresFullOpacityHandling = fullOpacity, BitmapInterpolationMode = BitmapInterpolationMode.None };
                var id = $"{pattern}-r{radius.ToString(System.Globalization.CultureInfo.InvariantCulture)}-{paint}-{edge}-dpi{dpi}-o{opacity.ToString(System.Globalization.CultureInfo.InvariantCulture)}-full{fullOpacity}";
                var diagnostic = pattern == "c0-full-10k" && radius == 2 && paint == "opaque" && edge == default && dpi == 96 && opacity == 1 && !fullOpacity;
                var clip = pattern == "c0-full-10k" ? plot : new Rect(8, 8, Width - 16, Height - 16);
                var original = Raster(new PaintScene(set, clip, fill, pen, options, opacity, paint == "alpha", "ellipse"), dpi, id + "-ellipse", diagnostic);
                var direct = Raster(new PaintScene(set, clip, fill, pen, options, opacity, paint == "alpha", "direct"), dpi, id + "-direct", diagnostic);
                var mask = Raster(new PaintScene(set, clip, fill, pen, options, opacity, paint == "alpha", "mask"), dpi, id + "-mask", diagnostic);
                Compare(original, mask, "ellipse-vs-coverage-mask");
                Compare(direct, mask, "guarded-direct-vs-coverage-mask");
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
                        original = before.Fingerprint, candidate = after.Fingerprint, originalColdRasterMs = before.ColdRasterMs, candidateColdRasterMs = after.ColdRasterMs,
                        originalWarmRasterMs = before.WarmRasterMs, candidateWarmRasterMs = after.WarmRasterMs, candidateMask = after.Mask,
                        changedPixels = changed, maximumChannelDelta = delta, equivalent = changed == 0 });
                    Console.WriteLine($"C7 alpha mask {id}/{mode}: changed={changed}; delta={delta}");
                }
            }
            completed = true;
            _passed = equivalent && comparisons.Count == 256 && _maskFrames > 0 && _directFrames > 0 && _fallbackFrames > 0 && _masksCreated == _masksDisposed && _liveMasks == 0;
        }
        catch (Exception exception) { error = exception.ToString(); Console.Error.WriteLine(error); }
        finally
        {
            File.WriteAllText(Path.Combine(_output, "alpha-mask-experiment.json"), JsonSerializer.Serialize(new
            {
                schema = "charts-c703-alpha-mask-experiment/v1", completed, passed = _passed, equivalent, error,
                scope = "Isolated zero-tolerance coverage-mask rejection experiment, not production or formal performance evidence. Each absolute fill and widened-outline path is separately rasterized in opaque white onto Alpha8 with an integer -floor(bounds)-2 CTM. Masks never contain the series color. Replay uses original solid color and source order, two image commands per point, nearest sampling, integer origin and disabled image-quad antialiasing. Software identity-device-matrix and ambient-opacity-one are required; all other states or a mask exceeding 256 pixels fall back to the original ellipse before painting. 128 fixtures compare ellipse and equivalently guarded direct paths against masks. Only canonical opaque/default 10K has 5 warmup/10 replay samples; mask preparation is separately reported and included in cold raster.",
                useOpacitySaveLayer = _saveLayer, corpus = manifest, canvasDip = new[] { Width, Height }, maximumMaskPixels = MaximumMaskPixels,
                maskFrames = _maskFrames, directFrames = _directFrames, fallbackFrames = _fallbackFrames,
                masksCreated = _masksCreated, masksDisposed = _masksDisposed, liveMasksAtCompletion = _liveMasks, maximumLiveMasks = _maximumLiveMasks,
                actualMaskFormats = Formats.Order().ToArray(),
                ownership = "Only the immutable PaintScene owns its masks. It releases images and bitmaps after RTB rendering, pixel readback and PNG save, including exception paths. Custom-op Dispose does not release shared fixture ownership. Paths remain owned by Media.Geometry fixtures. No static mask cache. Pixel byte counts include actual SKBitmap.ByteCount and any separate SKImage raster backing exposed by PeekPixels; equal pixel addresses are counted once. Uninspectable image backing is counted separately as unknown. Object/allocator overhead is excluded; these are not total native/process memory measurements.",
                fixtureOnlyPrivateAccessor = new { declaringType = PlatformGeometry.DeclaringType?.FullName, PlatformGeometry.Name, getterPublic = PlatformGeometry.GetGetMethod(true)?.IsPublic, productionUse = false },
                publicGetterBridge = new { paths = PathGetters.Values.Select(p => new { declaringType = p.DeclaringType?.FullName, p.Name, getterPublic = p.GetMethod?.IsPublic }).ToArray(),
                    options = OptionsGetters.Values.Select(p => new { declaringType = p.DeclaringType?.FullName, p.Name, getterPublic = p.GetMethod?.IsPublic }).ToArray() },
                os = RuntimeInformation.OSDescription, harness = Fingerprint(typeof(C7AlphaMaskExperiment).Assembly.Location), charts = Fingerprint(typeof(CartesianChart).Assembly.Location),
                skia = Fingerprint(typeof(ISkiaSharpApiLeaseFeature).Assembly.Location), comparisons,
            }, Json));
            desktop.Shutdown(_passed ? 0 : 1);
        }
    }

    private static PropertyInfo PublicGetter(Type type, string name)
    {
        var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
        if (property?.GetMethod?.IsPublic != true || property.GetIndexParameters().Length != 0) throw new NotSupportedException($"No public {name} getter on {type.FullName}.");
        return property;
    }
    private static SKPath PathOf(Geometry geometry)
    {
        var native = PlatformGeometry.GetValue(geometry) ?? throw new InvalidOperationException("Null native fixture geometry.");
        if (native.GetType().Assembly != typeof(ISkiaSharpApiLeaseFeature).Assembly) throw new NotSupportedException("Unexpected geometry backend.");
        var property = PathGetters.GetOrAdd(native.GetType(), static type => PublicGetter(type, "FillPath"));
        if (property.PropertyType != typeof(SKPath)) throw new NotSupportedException("Unexpected FillPath signature.");
        return (SKPath?)property.GetValue(native) ?? throw new InvalidOperationException("Null fill path.");
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
    private static Captured Raster(PaintScene visual, int dpi, string id, bool diagnostic)
    {
        using var owner = visual;
        visual.Width = Width; visual.Height = Height; visual.Measure(new Size(Width, Height)); visual.Arrange(new Rect(0, 0, Width, Height));
        var size = new PixelSize(Width * dpi / 96, Height * dpi / 96);
        double cold; byte[] raw; var times = new double[diagnostic ? 10 : 0];
        var relative = id + ".png"; var path = Path.Combine(_output, relative);
        using (var bitmap = new RenderTargetBitmap(size, new Vector(dpi, dpi)))
        {
            var started = Stopwatch.GetTimestamp(); bitmap.Render(visual); cold = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            if (diagnostic)
            {
                for (var i = 0; i < 5; i++) bitmap.Render(visual);
                for (var i = 0; i < times.Length; i++) { started = Stopwatch.GetTimestamp(); bitmap.Render(visual); times[i] = Stopwatch.GetElapsedTime(started).TotalMilliseconds; }
            }
            using (var stream = File.Open(path, FileMode.CreateNew)) bitmap.Save(stream);
            using var converted = new WriteableBitmap(size, new Vector(dpi, dpi), PixelFormat.Bgra8888, AlphaFormat.Premul);
            using var frame = converted.Lock(); bitmap.CopyPixels(frame);
            raw = new byte[checked(size.Width * size.Height * 4)];
            for (var y = 0; y < size.Height; y++) Marshal.Copy(frame.Address + y * frame.RowBytes, raw, y * size.Width * 4, size.Width * 4);
        }
        visual.Dispose();
        return new(raw, cold, times, visual.Diagnostics, new { path = relative, pixelWidth = size.Width, pixelHeight = size.Height,
            sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), pixelSha256 = Convert.ToHexString(SHA256.HashData(raw)) });
    }
    private static object Fingerprint(string path) => new { path, bytes = new FileInfo(path).Length, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) };
    private sealed record Captured(byte[] Pixels, double ColdRasterMs, double[] WarmRasterMs, object? Mask, object Fingerprint);
    private sealed record ShapeSet(Point[] Points, EllipseGeometry[] Ellipses, Geometry[] Rings, SKPath[] Fills, SKPath[] Outlines, double Radius, double Thickness);

    private sealed class PaintScene(ShapeSet set, Rect clip, IImmutableBrush fill, Pen pen, RenderOptions options, double opacity, bool transparent, string mode) : Control, IDisposable
    {
        private readonly MaskPaint? _operation = mode == "ellipse" ? null : new(set, fill, pen.ToImmutable(), mode == "mask");
        internal object? Diagnostics => _operation?.Diagnostics;
        public void Dispose() => _operation?.ReleaseOwner();
        public override void Render(DrawingContext context)
        {
            base.Render(context);
            context.DrawRectangle(transparent ? Brushes.Transparent : Brushes.White, null, new Rect(0, 0, C7AlphaMaskExperiment.Width, C7AlphaMaskExperiment.Height));
            if (!transparent)
            {
                for (var x = 0; x < C7AlphaMaskExperiment.Width; x += 120) context.DrawRectangle(x % 240 == 0 ? Brushes.AliceBlue : Brushes.Beige, null, new Rect(x, 0, 120, C7AlphaMaskExperiment.Height));
                var grid = new Pen(new ImmutableSolidColorBrush(Color.FromArgb(117, 71, 83, 97)), .75);
                for (var y = 0; y < C7AlphaMaskExperiment.Height; y += 40) context.DrawLine(grid, new Point(0, y + .375), new Point(C7AlphaMaskExperiment.Width, y + .375));
            }
            using var rendering = context.PushRenderOptions(options);
            using var outerOpacity = context.PushOpacity(opacity);
            using var clipping = context.PushClip(clip);
            if (_operation is not null) context.Custom(_operation);
            else foreach (var point in set.Points) context.DrawEllipse(fill, pen, point, set.Radius, set.Radius);
        }
    }

    private sealed class A8Mask(SKBitmap bitmap, SKImage image, float x, float y) : IDisposable
    {
        internal SKBitmap Bitmap { get; } = bitmap;
        internal SKImage Image { get; } = image;
        internal float X { get; } = x;
        internal float Y { get; } = y;
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; Image.Dispose(); Bitmap.Dispose(); _masksDisposed++; _liveMasks--;
        }
    }
    private sealed record MaskPair(A8Mask Fill, A8Mask Outline);

    private sealed class MaskPaint(ShapeSet set, IImmutableBrush fill, ImmutablePen pen, bool masked) : ICustomDrawOperation
    {
        private readonly List<A8Mask> _owned = [];
        private MaskPair[]? _masks;
        private bool _ownerReleased, _attempted, _aa;
        private double? _coldMaskMs;
        private long? _coldManagedBytes;
        private long _pixelBytes;
        private long _separateImagePixelBytes;
        private int _sharedPixelImages, _uninspectableImageBacking;
        private int _largestMaskPixels, _fallbacks, _replays;
        private string? _buildRejection;
        public Rect Bounds => new(0, 0, Width, Height);
        public bool HitTest(Point point) => false;
        public bool Equals(ICustomDrawOperation? other) => ReferenceEquals(this, other);
        public void Dispose() { }
        internal object Diagnostics => new { coldMaskMs = _coldMaskMs, coldMaskManagedBytes = _coldManagedBytes,
            nativePixelBytes = _pixelBytes + _separateImagePixelBytes, bitmapPixelBytes = _pixelBytes,
            separateImagePixelBytes = _separateImagePixelBytes, sharedPixelImages = _sharedPixelImages,
            uninspectableImageBacking = _uninspectableImageBacking, nativePixelBytesExcludeAllocatorAndObjectOverhead = true,
            expectedSeparateMasks = set.Points.Length * 2, largestMaskPixels = _largestMaskPixels,
            replays = _replays, fallbacks = _fallbacks, buildRejection = _buildRejection, ownerReleased = _ownerReleased };
        internal void ReleaseOwner()
        {
            if (_ownerReleased) return;
            _ownerReleased = true;
            foreach (var mask in _owned) mask.Dispose();
            _owned.Clear(); _masks = null;
        }
        public void Render(ImmediateDrawingContext context)
        {
            if (_ownerReleased) throw new ObjectDisposedException(nameof(MaskPaint));
            var native = context.PlatformImpl; var type = native.GetType();
            if (type.Assembly != typeof(ISkiaSharpApiLeaseFeature).Assembly || type.FullName != "Avalonia.Skia.DrawingContextImpl") throw new NotSupportedException("Unexpected native drawing context.");
            var property = OptionsGetters.GetOrAdd(type, static t => PublicGetter(t, "RenderOptions"));
            if (property.PropertyType != typeof(RenderOptions)) throw new NotSupportedException("Unexpected RenderOptions signature.");
            var options = (RenderOptions)property.GetValue(native)!;
            var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>() ?? throw new NotSupportedException("No Skia lease.");
            var fallback = false;
            using (var lease = feature.Lease())
            {
                var m = lease.SkCanvas.TotalMatrix;
                var unit = lease.SkSurface is not null && lease.GrContext is null && m.ScaleX == 1 && m.ScaleY == 1 && m.SkewX == 0 && m.SkewY == 0 &&
                    m.TransX == 0 && m.TransY == 0 && m.Persp0 == 0 && m.Persp1 == 0 && m.Persp2 == 1;
                fallback = !unit || lease.CurrentOpacity != 1;
                if (!fallback && masked) fallback = !PrepareMasks(options.EdgeMode != EdgeMode.Aliased);
                if (!fallback)
                {
                    using var fillPaint = Paint((ISolidColorBrush)fill, masked ? false : options.EdgeMode != EdgeMode.Aliased);
                    using var strokePaint = Paint((ISolidColorBrush)pen.Brush!, masked ? false : options.EdgeMode != EdgeMode.Aliased);
                    for (var index = 0; index < set.Points.Length; index++)
                    {
                        if (masked)
                        {
                            var pair = _masks![index];
                            lease.SkCanvas.DrawImage(pair.Fill.Image, pair.Fill.X, pair.Fill.Y, Nearest, fillPaint);
                            lease.SkCanvas.DrawImage(pair.Outline.Image, pair.Outline.X, pair.Outline.Y, Nearest, strokePaint);
                        }
                        else
                        {
                            lease.SkCanvas.DrawPath(set.Fills[index], fillPaint);
                            lease.SkCanvas.DrawPath(set.Outlines[index], strokePaint);
                        }
                    }
                    _replays++;
                    if (masked) _maskFrames++; else _directFrames++;
                }
            }
            if (!fallback) return;
            _fallbacks++; _fallbackFrames++;
            foreach (var point in set.Points) context.DrawEllipse(fill, pen, point, set.Radius, set.Radius);
        }
        private bool PrepareMasks(bool aa)
        {
            if (_attempted)
            {
                if (_aa != aa) throw new InvalidOperationException("Immutable mask AA state changed.");
                return _masks is not null;
            }
            _attempted = true; _aa = aa;
            var bytes = GC.GetAllocatedBytesForCurrentThread(); var started = Stopwatch.GetTimestamp();
            try
            {
                using var coverage = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill, BlendMode = SKBlendMode.SrcOver, IsAntialias = aa };
                var masks = new MaskPair[set.Points.Length];
                for (var index = 0; index < set.Points.Length; index++)
                {
                    var body = CreateMask(set.Fills[index], coverage);
                    if (body is null) return RejectMasks();
                    var outline = CreateMask(set.Outlines[index], coverage);
                    if (outline is null) return RejectMasks();
                    masks[index] = new(body, outline);
                }
                _masks = masks;
                return true;
            }
            finally
            {
                _coldMaskMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                _coldManagedBytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
            }
        }
        private bool RejectMasks()
        {
            foreach (var mask in _owned) mask.Dispose();
            _owned.Clear();
            return false;
        }
        private A8Mask? CreateMask(SKPath path, SKPaint coverage)
        {
            var bounds = path.TightBounds;
            if (!float.IsFinite(bounds.Left) || !float.IsFinite(bounds.Top) || !float.IsFinite(bounds.Right) || !float.IsFinite(bounds.Bottom))
            { _buildRejection = "nonFiniteMaskBounds"; return null; }
            var left = Math.Floor(bounds.Left) - 2; var top = Math.Floor(bounds.Top) - 2;
            var width = Math.Ceiling(bounds.Right) + 2 - left; var height = Math.Ceiling(bounds.Bottom) + 2 - top;
            if (width <= 0 || height <= 0 || width > MaximumMaskPixels || height > MaximumMaskPixels || width * height > MaximumMaskPixels)
            { _buildRejection = "maskExceeds256Pixels"; return null; }
            var bitmap = new SKBitmap(new SKImageInfo((int)width, (int)height, SKColorType.Alpha8, SKAlphaType.Premul));
            SKImage? image = null;
            try
            {
                using (var canvas = new SKCanvas(bitmap))
                {
                    canvas.Clear(SKColors.Transparent); canvas.Translate((float)-left, (float)-top); canvas.DrawPath(path, coverage); canvas.Flush();
                }
                bitmap.SetImmutable(); image = SKImage.FromBitmap(bitmap);
                Formats.Add($"bitmap:{bitmap.Info.ColorType}/{bitmap.Info.AlphaType};image:{image.ColorType}/{image.AlphaType}");
                if (bitmap.Info.ColorType != SKColorType.Alpha8 || image.ColorType != SKColorType.Alpha8)
                { _buildRejection = "backendDidNotRetainAlpha8"; image.Dispose(); bitmap.Dispose(); return null; }
                using var pixels = image.PeekPixels();
                var sharedPixels = pixels is not null && pixels.GetPixels() == bitmap.GetPixels();
                var extraBytes = pixels is not null && !sharedPixels ? checked((long)pixels.RowBytes * pixels.Height) : 0;
                var result = new A8Mask(bitmap, image, (float)left, (float)top);
                _owned.Add(result); _pixelBytes += bitmap.ByteCount; _largestMaskPixels = Math.Max(_largestMaskPixels, (int)(width * height));
                _masksCreated++; _liveMasks++; _maximumLiveMasks = Math.Max(_maximumLiveMasks, _liveMasks);
                if (pixels is null) _uninspectableImageBacking++;
                else if (sharedPixels) _sharedPixelImages++;
                else _separateImagePixelBytes += extraBytes;
                return result;
            }
            catch { image?.Dispose(); bitmap.Dispose(); throw; }
        }
        private static SKPaint Paint(ISolidColorBrush brush, bool antialias)
        {
            var color = brush.Color;
            return new SKPaint { Style = SKPaintStyle.Fill, BlendMode = SKBlendMode.SrcOver, IsAntialias = antialias,
                Color = new SKColor(color.R, color.G, color.B, (byte)(color.A * brush.Opacity)) };
        }
    }

    public sealed class AlphaMaskApplication : Application
    {
        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var window = new Window { Width = 320, Height = 240, CanResize = false, Title = "C7-03 Alpha8 coverage mask rejection experiment", Background = Brushes.White };
                desktop.MainWindow = window; window.Opened += (_, _) => Dispatcher.UIThread.Post(() => Capture(desktop)); window.Show();
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}
#endif
