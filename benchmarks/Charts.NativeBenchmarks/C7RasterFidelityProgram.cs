using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;
using NT.AvaloniaControl.Charts.Skia;
using NT.AvaloniaControl.Charts.Themes;

namespace Charts.NativeBenchmarks;

// Independent, untimed old/new raster comparison. These fixtures must never be
// pooled with the fixed-load performance protocol or used as timing samples.
internal static class C7RasterFidelityProgram
{
    private static string _output = null!;
    private static bool _passed;
    internal static int Run(string[] args)
    {
        if (args.Length != 3 || args[0] != "--c7-raster-fidelity" || args[1] != "--output")
            throw new ArgumentException("Expected --c7-raster-fidelity --output <new-directory>.");
        _output = Path.GetFullPath(args[2]);
        if (Directory.Exists(_output) || File.Exists(_output)) throw new IOException("Fidelity output must be new.");
        Directory.CreateDirectory(_output);
        ChartNativeTextBinding.Initialize();
        var builder = AppBuilder.Configure<FidelityApplication>().WithChartsSkiaRendering().UsePlatformDetect();
        if (OperatingSystem.IsWindows()) builder.With(new Win32PlatformOptions { RenderingMode = [Win32RenderingMode.Software] });
        else builder.With(new X11PlatformOptions { RenderingMode = [X11RenderingMode.Software] });
        builder.StartWithClassicDesktopLifetime([], ShutdownMode.OnExplicitShutdown);
        return _passed ? 0 : 1;
    }

    private static void Capture(Window window, IClassicDesktopStyleApplicationLifetime desktop)
    {
        var images = new List<object>();
        var translatedFixtures = new List<object>();
        string? error = null;
        try
        {
            foreach (var kind in new[] { "scatter", "bar", "line" })
            foreach (var gradient in new[] { false, true })
            foreach (var markers in new[] { false, true })
            {
                Brush brush = gradient ? new LinearGradientBrush
                {
                    GradientStops = [new(Colors.CornflowerBlue, 0), new(Color.FromArgb(119, 240, 31, 73), .375), new(Colors.DarkGreen, 1)],
                    Opacity = .625,
                } : new SolidColorBrush(Color.FromArgb(137, 17, 83, 151)) { Opacity = .75 };
                var chart = new CartesianChart
                {
                    Width = 640, Height = 360, Background = Brushes.White, Foreground = Brushes.Black,
                    FontFamily = new FontFamily("DejaVu Sans, Segoe UI"), FontSize = 12,
                    ShowLegend = false, ShowGridLines = true, AnimationTriggers = ChartAnimationTriggers.None,
                    XAxis = new NumericAxis { Minimum = 0, Maximum = 8, DesiredTickCount = 5 },
                    YAxis = new NumericAxis { Minimum = -3, Maximum = 9, DesiredTickCount = 5 },
                };
                // Equal X, coincident points, NaN breaks, clipped and fractional
                // edges exercise overdraw, AA, pen order and raw source mapping.
                var rows = new[] { new Row(0, -.125, 5.75), new Row(1, 1.125, 2.375), new Row(2, 1.125, 2.375),
                    new Row(3, 1.125, 8.625), new Row(4, 3.25, double.NaN), new Row(5, 4.8125, -2.375),
                    new Row(6, 7.9875, 4.375), new Row(7, 8.125, 7.625) };
                ChartSeries Make(int offset)
                {
                    ChartSeries series = kind switch { "bar" => new BarSeries(), "scatter" => new ScatterSeries(), _ => new LineSeries { ShowMarkers = markers } };
                    series.ItemsSource = rows.Select(row => row with { Y = row.Y + offset * .125 }).ToArray();
                    series.CategoryPath = "X"; series.ValuePath = "Y"; series.IdPath = "Id";
                    series.Fill = brush; series.Stroke = brush; series.StrokeThickness = 1.75;
                    series.MarkerSize = 7.25; series.MarkerFill = brush; series.MarkerStroke = brush;
                    series.MarkerShape = markers ? ChartMarkerShape.Square : ChartMarkerShape.Circle;
                    if (series is BarSeries bars) bars.ShowMarkers = markers;
                    return series;
                }
                chart.Series.Add(Make(0)); chart.Series.Add(Make(1));
                window.Content = chart; window.UpdateLayout(); _ = chart.PlotArea;
                var id = $"{kind}-{(gradient ? "gradient" : "solid")}-{(markers ? "square" : "circle")}";
                Save(chart, id + "-direct-before");
                var oldRecording = Record(chart);
                SaveDrawing(oldRecording, id + "-record-before");
                // Warm retained replay, then mutate public wrappers and live paint.
                var exported = Record(chart);
                var geometry = Walk(exported).OfType<GeometryDrawing>().FirstOrDefault(d => d.Geometry is not null)?.Geometry;
                if (geometry is not null) geometry.Transform = new TranslateTransform(11.125, -3.375);
                brush.Opacity = .375;
                if (brush is SolidColorBrush solid) solid.Color = Color.FromArgb(97, 201, 29, 73);
                if (brush is LinearGradientBrush linear) linear.GradientStops[1].Color = Colors.Purple;
                SaveDrawing(oldRecording, id + "-record-old-after-paint");
                SaveDrawing(Record(chart), id + "-record-after-paint");
                Save(chart, id + "-direct-after-paint");
                chart.Series[0].ItemsSource = rows.Select(row => row with { Y = row.Y + .25 }).ToArray();
                window.UpdateLayout(); chart.Viewport = new ChartViewport(1, 7, -2, 8);
                window.UpdateLayout();
                Save(chart, id + "-direct-replaced-zoomed");
                SaveDrawing(Record(chart), id + "-record-replaced-zoomed");
                window.Content = null;
            }
            // Exercise the dense native path as well as its DPI/state fallbacks.
            // Keep the same host binary for the previous Core and candidate.
            foreach (var count in new[] { 2048, 10000 })
            foreach (var alpha in new[] { false, true })
            {
                var brush = new SolidColorBrush(Color.FromArgb(alpha ? (byte)121 : (byte)255, 17, 83, 151));
                var rows = Enumerable.Range(0, count).Select(i => new Row(i,
                    i % 17 == 0 ? 4.125 : (i * 37 % 997) / 124.625,
                    i % 17 == 0 ? 5.375 : (i * 113 % 991) / 82.5 - 3)).ToArray();
                var chart = new CartesianChart
                {
                    Width = 640, Height = 360, Background = Brushes.White, Foreground = Brushes.Black,
                    FontFamily = new FontFamily("DejaVu Sans, Segoe UI"), FontSize = 12,
                    ShowLegend = false, AnimationTriggers = ChartAnimationTriggers.None,
                    XAxis = new NumericAxis { Minimum = 0, Maximum = 8 },
                    YAxis = new NumericAxis { Minimum = -3, Maximum = 9 },
                };
                chart.Series.Add(new ScatterSeries { ItemsSource = rows, CategoryPath = "X", ValuePath = "Y", IdPath = "Id",
                    Fill = brush, Stroke = brush, MarkerFill = brush, MarkerStroke = brush, MarkerSize = 4, StrokeThickness = 2 });
                window.Content = chart; window.UpdateLayout(); _ = chart.PlotArea;
                var id = $"dense-{count}-{(alpha ? "alpha" : "opaque")}";
                Save(chart, id + "-direct");
                SaveTranslated(chart, id + "-parent-integer-40-35", new Vector(40, 35));
                SaveTranslated(chart, id + "-parent-fractional-025-075", new Vector(.25, .75));
                var old = Record(chart); SaveDrawing(old, id + "-record");
                chart.Series[0].ItemsSource = rows.Select(row => row with { Y = row.Y + .25 }).ToArray();
                window.UpdateLayout(); Save(chart, id + "-replaced");
                brush.Color = Color.FromArgb(alpha ? (byte)89 : (byte)255, 201, 29, 73);
                SaveDrawing(old, id + "-old-paint"); Save(chart, id + "-paint");
                chart.TrimResourceCaches(); Save(chart, id + "-trimmed");
                window.Content = null;
            }
            _passed = true;
        }
        catch (Exception exception) { error = exception.ToString(); Console.Error.WriteLine(error); }
        finally
        {
            object Fingerprint(string path) => new { path, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) };
            File.WriteAllText(Path.Combine(_output, "fidelity.json"), JsonSerializer.Serialize(new
            {
                schema = "charts-c703-raster-fidelity/v1", passed = _passed, error, images, translatedFixtures,
                os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                charts = Fingerprint(typeof(CartesianChart).Assembly.Location),
                harness = Fingerprint(typeof(C7RasterFidelityProgram).Assembly.Location),
                nativeBackend = Fingerprint(typeof(ChartSkiaRenderingExtensions).Assembly.Location),
                nativeExpandedFrames = typeof(ChartSkiaRenderingExtensions).Assembly
                    .GetType("NT.AvaloniaControl.Charts.Skia.ChartNativeCircleOperation")?
                    .GetProperty("ExpandedFrameCount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?
                    .GetValue(null),
                nativeFastPaintFrames = typeof(ChartSkiaRenderingExtensions).Assembly
                    .GetType("NT.AvaloniaControl.Charts.Skia.ChartNativeCircleOperation")?
                    .GetProperty("FastPaintFrameCount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?
                    .GetValue(null),
                nativeTranslatedExpandedFrames = typeof(ChartSkiaRenderingExtensions).Assembly
                    .GetType("NT.AvaloniaControl.Charts.Skia.ChartNativeCircleOperation")?
                    .GetProperty("TranslatedExpandedFrameCount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?
                    .GetValue(null),
                nativeTranslatedFastPaintFrames = typeof(ChartSkiaRenderingExtensions).Assembly
                    .GetType("NT.AvaloniaControl.Charts.Skia.ChartNativeCircleOperation")?
                    .GetProperty("TranslatedFastPaintFrameCount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?
                    .GetValue(null),
                nativeText = ChartNativeTextBinding.Diagnostics,
                scope = "Untimed software raster; same-OS baseline/candidate exact decoded pixel comparison, not cross-OS or performance qualification.",
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
            desktop.Shutdown(_passed ? 0 : 1);
        }

        void Save(Control control, string id)
        {
            foreach (var dpi in new[] { 96, 144 })
            {
                var scale = dpi / 96d;
                using var bitmap = new RenderTargetBitmap(new PixelSize((int)(640 * scale), (int)(360 * scale)), new Vector(dpi, dpi));
                bitmap.Render(control);
                var imageId = id + "-dpi" + dpi;
                var path = Path.Combine(_output, imageId + ".png"); bitmap.Save(path);
                images.Add(new { id = imageId, path = imageId + ".png", sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) });
            }
        }
        void SaveTranslated(CartesianChart chart, string id, Vector translation)
        {
            var originalTransform = chart.RenderTransform;
            var horizontal = chart.HorizontalAlignment;
            var vertical = chart.VerticalAlignment;
            window.Content = null;
            var parent = new Border
            {
                Width = 640, Height = 360, Background = Brushes.White, ClipToBounds = true,
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
            };
            try
            {
                chart.HorizontalAlignment = HorizontalAlignment.Left;
                chart.VerticalAlignment = VerticalAlignment.Top;
                chart.RenderTransform = new TranslateTransform(translation.X, translation.Y);
                parent.Child = chart;
                window.Content = parent;
                window.UpdateLayout(); _ = chart.PlotArea;
                var matrix = chart.TransformToVisual(parent) ?? throw new InvalidOperationException("Translated fixture has no visual transform.");
                if (matrix.M11 != 1 || matrix.M22 != 1 || matrix.M12 != 0 || matrix.M21 != 0 ||
                    matrix.M31 != translation.X || matrix.M32 != translation.Y)
                    throw new InvalidOperationException($"Translated fixture has unexpected child matrix: {matrix}.");
                // Rendering the parent is essential: RenderTargetBitmap.Render
                // does not apply a root control's own RenderTransform.
                Save(parent, id);
                translatedFixtures.Add(new { id, translationX = translation.X, translationY = translation.Y,
                    childToParent = matrix.ToString(), renderedRoot = "Border", dpi = new[] { 96, 144 } });
            }
            finally
            {
                window.Content = null;
                parent.Child = null;
                chart.RenderTransform = originalTransform;
                chart.HorizontalAlignment = horizontal;
                chart.VerticalAlignment = vertical;
                window.Content = chart;
                window.UpdateLayout(); _ = chart.PlotArea;
            }
        }
        void SaveDrawing(DrawingGroup group, string id)
        {
            var image = new Image { Source = new DrawingImage(group), Width = 640, Height = 360, Stretch = Stretch.None };
            image.Measure(new Size(640, 360)); image.Arrange(new Rect(0, 0, 640, 360));
            Save(image, id);
        }
    }

    private static DrawingGroup Record(CartesianChart chart)
    { var group = new DrawingGroup(); using (var context = group.Open()) chart.Render(context); return group; }
    private static IEnumerable<Drawing> Walk(Drawing drawing)
    { yield return drawing; if (drawing is DrawingGroup group) foreach (var child in group.Children) foreach (var node in Walk(child)) yield return node; }
    private sealed record Row(int Id, double X, double Y);
    public sealed class FidelityApplication : Application
    {
        public override void Initialize()
        { RequestedThemeVariant = ThemeVariant.Light; Styles.Add(new FluentTheme()); Styles.Add(new ChartsTheme()); }
        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var window = new Window { Width = 640, Height = 360, CanResize = false, Title = "C7-03 untimed raster fidelity", Background = Brushes.White };
                desktop.MainWindow = window;
                window.Opened += (_, _) => Dispatcher.UIThread.Post(() => Capture(window, desktop));
                window.Show();
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}
