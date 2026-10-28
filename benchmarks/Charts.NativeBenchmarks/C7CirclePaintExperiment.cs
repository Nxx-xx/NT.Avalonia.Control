#if C7_PERFORMANCE_DRIVER
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;
using NT.AvaloniaControl.Charts.Skia;

namespace Charts.NativeBenchmarks;

// Deliberately separate from both production rendering and C7 timings. This
// experiment asks whether two superficially equal opaque circle paints really
// have identical edge coverage. A single differing pixel rejects equivalence.
internal static class C7CirclePaintExperiment
{
    private const int Columns = 32;
    private const int Tile = 32;
    private const int CaseCount = 1024;
    private const int CanvasSize = Columns * Tile;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly Fixture[] Cases = CreateCases();
    private static string _output = null!;
    private static bool _passed;

    internal static int Run(string[] args)
    {
        if (args.Length != 3 || args[0] != "--c7-circle-paint-experiment" || args[1] != "--output")
            throw new ArgumentException("Expected --c7-circle-paint-experiment --output <new-directory>.");
        _output = Path.GetFullPath(args[2]);
        if (Directory.Exists(_output) || File.Exists(_output)) throw new IOException("Experiment output must be new.");
        Directory.CreateDirectory(_output);
        ChartNativeTextBinding.Initialize();
        var builder = AppBuilder.Configure<CirclePaintApplication>().WithChartsSkiaRendering().UsePlatformDetect();
        if (OperatingSystem.IsWindows()) builder.With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Software] });
        else builder.With(new X11PlatformOptions { RenderingMode = [X11RenderingMode.Software] });
        builder.StartWithClassicDesktopLifetime([], ShutdownMode.OnExplicitShutdown);
        return _passed ? 0 : 1;
    }

    private static void Capture(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var comparisons = new List<object>();
        string? error = null;
        var completed = false;
        var equivalent = true;
        try
        {
            foreach (var dpi in new[] { 96, 120, 144 })
            {
                var original = Raster(dpi, merged: false);
                var merged = Raster(dpi, merged: true);
                var tilePixels = Tile * dpi / 96;
                var caseResults = new List<object>();
                long changedPixels = 0;
                var maximumDelta = 0;
                var unequalCases = 0;
                foreach (var fixture in Cases)
                {
                    var left = fixture.Index % Columns * tilePixels;
                    var top = fixture.Index / Columns * tilePixels;
                    var changed = 0;
                    var caseDelta = 0;
                    for (var y = top; y < top + tilePixels; y++)
                    for (var x = left; x < left + tilePixels; x++)
                    {
                        var offset = (y * original.PixelWidth + x) * 4;
                        var delta = 0;
                        for (var channel = 0; channel < 4; channel++)
                            delta = Math.Max(delta, Math.Abs(original.Pixels[offset + channel] - merged.Pixels[offset + channel]));
                        if (delta == 0) continue;
                        changed++;
                        caseDelta = Math.Max(caseDelta, delta);
                    }
                    if (changed > 0) unequalCases++;
                    changedPixels += changed;
                    maximumDelta = Math.Max(maximumDelta, caseDelta);
                    caseResults.Add(new { fixture.Index, changedPixels = changed, maximumChannelDelta = caseDelta, equivalent = changed == 0 });
                }
                equivalent &= changedPixels == 0;
                comparisons.Add(new
                {
                    dpi, original.PixelWidth, original.PixelHeight, pixelFormat = "BGRA8888-premultiplied", tilePixels,
                    original = original.Fingerprint, merged = merged.Fingerprint,
                    changedPixels, maximumChannelDelta = maximumDelta, unequalCases, equivalent = changedPixels == 0, cases = caseResults,
                });
                Console.WriteLine($"C7 circle experiment {dpi} DPI: unequalCases={unequalCases}/{CaseCount}, changedPixels={changedPixels}, maximumChannelDelta={maximumDelta}");
            }
            completed = true;
            _passed = equivalent;
        }
        catch (Exception exception) { error = exception.ToString(); Console.Error.WriteLine(error); }
        finally
        {
            File.WriteAllText(Path.Combine(_output, "circle-paint-experiment.json"), JsonSerializer.Serialize(new
            {
                schema = "charts-c703-circle-paint-experiment/v1", completed, passed = _passed, equivalent, error,
                scope = "Untimed same-process software raster equivalence experiment; no production path or C7 performance protocol is changed. Any unequal decoded pixel rejects the proposed replacement.",
                baseline = "DrawEllipse(opaque #155CC1 fill, same-color Pen(thickness), center, radius, radius)",
                candidate = "DrawEllipse(opaque #155CC1 fill, null pen, center, radius + thickness / 2, radius + thickness / 2)",
                os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(), runtime = Environment.Version.ToString(),
                caseCountPerDpi = CaseCount, totalComparisons = CaseCount * 3, canvasDip = CanvasSize, columns = Columns, tileDip = Tile,
                caseManifestSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(Cases, Json)))),
                harness = Fingerprint(typeof(C7CirclePaintExperiment).Assembly.Location),
                avalonia = Fingerprint(typeof(DrawingContext).Assembly.Location),
                renderAssemblies = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => assembly.GetName().Name is "Avalonia.Skia" or "SkiaSharp")
                    .Select(assembly => Fingerprint(assembly.Location)).ToArray(),
                cases = Cases, comparisons,
            }, Json));
            desktop.Shutdown(_passed ? 0 : 1);
        }
    }

    private static Captured Raster(int dpi, bool merged)
    {
        var pixels = CanvasSize * dpi / 96;
        var size = new PixelSize(pixels, pixels);
        var atlas = new CircleAtlas(merged) { Width = CanvasSize, Height = CanvasSize };
        atlas.Measure(new Size(CanvasSize, CanvasSize)); atlas.Arrange(new Rect(0, 0, CanvasSize, CanvasSize));
        using var bitmap = new RenderTargetBitmap(size, new Vector(dpi, dpi));
        bitmap.Render(atlas);
        var relative = $"circle-{dpi}-{(merged ? "merged-fill" : "original-fill-stroke")}.png";
        var path = Path.Combine(_output, relative);
        using (var stream = File.Open(path, FileMode.CreateNew)) bitmap.Save(stream);
        using var converted = new WriteableBitmap(size, new Vector(dpi, dpi), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var frame = converted.Lock(); bitmap.CopyPixels(frame);
        var raw = new byte[checked(pixels * pixels * 4)];
        for (var y = 0; y < pixels; y++) Marshal.Copy(frame.Address + y * frame.RowBytes, raw, y * pixels * 4, pixels * 4);
        return new(pixels, pixels, raw, new
        {
            path = relative, bytes = new FileInfo(path).Length,
            sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),
            pixelSha256 = Convert.ToHexString(SHA256.HashData(raw)),
        });
    }

    private static Fixture[] CreateCases()
    {
        double[] fractions = [0, .125, .25, .5, .875];
        double[] radii = [1, 2, 2.6875, 3.625, 7.25];
        double[] thicknesses = [.5, 1, 1.625, 2, 3];
        var cases = new List<Fixture>(CaseCount);
        foreach (var x in fractions)
        foreach (var y in fractions)
        foreach (var radius in radii)
        foreach (var thickness in thicknesses)
            cases.Add(new(cases.Count, x, y, radius, thickness, "single"));
        var singles = cases.Count;
        for (var index = 0; cases.Count < CaseCount; index++)
        {
            var basis = cases[index * 137 % singles];
            var pattern = (index % 3) switch { 0 => "coincident", 1 => "triple-overlap", _ => "quad-overlap" };
            // Include the exact formal C7 radius/thickness in every overlap
            // pattern in addition to the complete single-circle parameter grid.
            if (index < 3) basis = basis with { Radius = 2, Thickness = 2 };
            cases.Add(basis with { Index = cases.Count, Pattern = pattern });
        }
        return cases.ToArray();
    }

    private static object Fingerprint(string path) => new
    {
        path, bytes = new FileInfo(path).Length, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),
    };

    private sealed record Fixture(int Index, double FractionX, double FractionY, double Radius, double Thickness, string Pattern);
    private sealed record Captured(int PixelWidth, int PixelHeight, byte[] Pixels, object Fingerprint);

    private sealed class CircleAtlas(bool merged) : Control
    {
        public override void Render(DrawingContext context)
        {
            base.Render(context);
            context.FillRectangle(Brushes.White, new Rect(0, 0, CanvasSize, CanvasSize));
            var brush = new SolidColorBrush(Color.Parse("#155CC1"));
            foreach (var fixture in Cases)
            {
                var center = new Point(fixture.Index % Columns * Tile + Tile / 2d + fixture.FractionX,
                    fixture.Index / Columns * Tile + Tile / 2d + fixture.FractionY);
                var pen = merged ? null : new Pen(brush, fixture.Thickness);
                var radius = merged ? fixture.Radius + fixture.Thickness / 2 : fixture.Radius;
                Draw(center);
                if (fixture.Pattern == "coincident") Draw(center);
                if (fixture.Pattern is "triple-overlap" or "quad-overlap")
                {
                    Draw(center + new Vector(-fixture.Radius * .75, fixture.Radius * .375));
                    Draw(center + new Vector(fixture.Radius * .5, -fixture.Radius * .625));
                }
                if (fixture.Pattern == "quad-overlap") Draw(center + new Vector(-fixture.Radius * .125, -fixture.Radius * .75));
                void Draw(Point position) => context.DrawEllipse(brush, pen, position, radius, radius);
            }
        }
    }

    public sealed class CirclePaintApplication : Application
    {
        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var window = new Window { Width = 320, Height = 240, CanResize = false,
                    Title = "C7-03 opaque circle paint experiment", Background = Brushes.White };
                desktop.MainWindow = window;
                window.Opened += (_, _) => Dispatcher.UIThread.Post(() => Capture(desktop));
                window.Show();
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}
#endif
