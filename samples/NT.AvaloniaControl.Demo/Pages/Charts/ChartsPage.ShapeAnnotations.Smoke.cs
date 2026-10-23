using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private ShapeSmokeEvidence? _shapeSmokeEvidence;
    private readonly List<ShapePixelEvidence> _shapePixelEvidence = [];

    private async Task RunShapeAnnotationsSmokeAsync(Dictionary<string, bool> checks, string baseImagePath)
    {
        if (_shapeChart is not { } chart || _shapeRectangle is not { } rectangle || _shapeEllipse is not { } ellipse ||
            _shapeArrow is not { } arrow || _shapeExamples is not { } examples)
            throw new InvalidOperationException("形状标注示例尚未创建。");
        string Image(string suffix) => Path.Combine(Path.GetDirectoryName(baseImagePath)!, Path.GetFileNameWithoutExtension(baseImagePath) + suffix + ".png");
        var lightImage = Image("-c2-shapes"); var darkImage = Image("-c2-shapes-dark");
        var lightPixels = Image("-shape-pixels"); var darkPixels = Image("-shape-pixels-dark");
        var lightExport = Image("-shape-export"); var darkExport = Image("-shape-export-dark");
        var exports = new List<ShapeExportEvidence>();
        _shapeSmokeEvidence = new(lightImage, darkImage, lightPixels, darkPixels, lightExport, darkExport, exports, _shapePixelEvidence);
        examples.IsExpanded = true;
        _hostWindow.RequestedThemeVariant = ThemeVariant.Light;
        await SmokeFlushAsync(); examples.BringIntoView(); await SmokeFlushAsync();
        checks["C2三种形状标注已在原生页面布局"] = chart.Bounds.Width > 250 && chart.PlotArea.Height > 100 &&
            chart.Annotations.OfType<ChartShapeAnnotation>().Count() == 3;
        var initial = CaptureShapeHash(chart);
        ellipse.X1 = 2.5; ellipse.X2 = 5.7;
        await SmokeFlushAsync();
        checks["C2移动椭圆关注区域改变实际渲染"] = CaptureShapeHash(chart) != initial;
        ellipse.X1 = 5; ellipse.X2 = 8.2; await SmokeFlushAsync();
        var oneHead = CaptureShapeHash(chart); arrow.ArrowHeads = ChartArrowHeads.Both;
        await SmokeFlushAsync();
        checks["C2双端箭头改变实际渲染"] = CaptureShapeHash(chart) != oneHead;
        arrow.ArrowHeads = ChartArrowHeads.End;
        rectangle.CoordinateUnit = ChartAnnotationCoordinateUnit.Pixel; rectangle.X1 = 20; rectangle.Y1 = 25; rectangle.X2 = 180; rectangle.Y2 = 100;
        chart.Viewport = new ChartViewport(1, 9, 10, 90);
        await SmokeFlushAsync();
        checks["C2固定像素范围框可与数据标注共同缩放"] = rectangle.CoordinateUnit == ChartAnnotationCoordinateUnit.Pixel &&
            ellipse.CoordinateUnit == ChartAnnotationCoordinateUnit.Data && HasVisibleRange(chart, new ChartViewport(1, 9, 10, 90));
        rectangle.CoordinateUnit = ChartAnnotationCoordinateUnit.Data; rectangle.X1 = 1; rectangle.Y1 = 25; rectangle.X2 = 3.4; rectangle.Y2 = 55; chart.ResetViewport();
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var dark = theme == ThemeVariant.Dark;
            _hostWindow.RequestedThemeVariant = theme; await SmokeFlushAsync(); examples.BringIntoView(); await SmokeFlushAsync();
            CaptureSmokeImage(dark ? darkImage : lightImage);
            var export = ExportShapeChart(chart, dark ? darkExport : lightExport, dark);
            exports.Add(export);
            checks[$"C2{(dark ? "暗色" : "亮色")}形状PNG保留矩形椭圆与箭头墨迹"] = export.Exported && export.Width == 960 && export.Height == 540 &&
                export.RectangleInkPixels > 30 && export.EllipseInkPixels > 30 && export.ArrowInkPixels > 30;
            VerifyShapePixels(checks, theme, dark ? darkPixels : lightPixels);
        }
    }

    private static string CaptureShapeHash(CartesianChart chart)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(chart.Bounds.Width), (int)Math.Ceiling(chart.Bounds.Height)), new Vector(96, 96));
        bitmap.Render(chart); using var stream = new MemoryStream(); bitmap.Save(stream);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static ShapeExportEvidence ExportShapeChart(CartesianChart chart, string path, bool dark)
    {
        var exported = chart.TryExportPng(path, new ChartExportOptions { PixelSize = new(960, 540), Background = Brush(dark ? "#182232" : "#FFFFFF") });
        if (!exported) return new(dark ? "dark" : "light", false, 0, 0, 0, 0, 0);
        using var bitmap = new Bitmap(path);
        var pixels = ReadShapePixels(bitmap);
        return new(dark ? "dark" : "light", true, pixels.Width, pixels.Height,
            pixels.Count(new PixelRect(0, 0, pixels.Width, pixels.Height), Color.Parse("#4385F4"), 20),
            pixels.Count(new PixelRect(0, 0, pixels.Width, pixels.Height), Color.Parse("#19A798"), 20),
            pixels.Count(new PixelRect(0, 0, pixels.Width, pixels.Height), Color.Parse("#F2A33A"), 20));
    }

    private void VerifyShapePixels(Dictionary<string, bool> checks, ThemeVariant theme, string imagePath)
    {
        var dark = theme == ThemeVariant.Dark;
        var background = Color.Parse(dark ? "#111722" : "#F8FAFC"); var ink = Color.Parse("#CB11A5");
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), RowDefinitions = new RowDefinitions("*,*"), ColumnSpacing = 12, RowSpacing = 12, Margin = new Thickness(12) };
        var charts = new List<(CartesianChart Chart, int Kind, bool Vertical)>();
        for (var row = 0; row < 2; row++)
            for (var kind = 0; kind < 3; kind++)
            {
                var chart = CreateShapePixelChart(kind, row == 1, background, ink);
                charts.Add((chart, kind, row == 1));
                var panel = new StackPanel { Spacing = 4, Children = { new TextBlock { Text = $"{new[] { "矩形", "椭圆", "双端箭头" }[kind]} · {(row == 1 ? "纵轴" : "横轴")}断轴" }, chart } };
                Grid.SetColumn(panel, kind); Grid.SetRow(panel, row); grid.Children.Add(panel);
            }
        var window = new Window { Title = "Charts · 形状标注原生像素检查", Width = 1200, Height = 660, Content = grid, RequestedThemeVariant = theme };
        try
        {
            window.Show(_hostWindow); window.UpdateLayout();
            var native = window.TryGetPlatformHandle() is { Handle: not 0 };
            foreach (var (chart, kind, vertical) in charts)
            {
                var evidence = ReadShapeBreakPixels(chart, kind, vertical, dark ? "dark" : "light", native, background, ink);
                _shapePixelEvidence.Add(evidence);
                var label = $"C2{(dark ? "暗色" : "亮色")}{evidence.Shape}{evidence.Axis}断轴";
                checks[label + "像素保留两侧且缝内无墨迹"] = native && evidence.BeforeInkPixels >= 5 && evidence.AfterInkPixels >= 5 &&
                    evidence.GapPixels >= 3 && evidence.GapInkPixels == 0 && evidence.GapBackgroundPixels == evidence.GapPixels;
                if (kind == 1) checks[label + "椭圆角落保持背景"] = evidence.ShapeDetailInkPixels == 0 && evidence.ShapeDetailBackgroundPixels >= 9;
                if (kind == 2) checks[label + "箭头两端斜边实际可见"] = evidence.ShapeDetailInkPixels >= 2 && evidence.OtherHeadInkPixels >= 2;
            }
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width), (int)Math.Ceiling(window.ClientSize.Height)), new Vector(96, 96));
            bitmap.Render(window); Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!); bitmap.Save(imagePath);
        }
        finally { window.Close(); }
    }

    private static CartesianChart CreateShapePixelChart(int kind, bool vertical, Color background, Color ink)
    {
        static NumericAxis Axis() => new() { Minimum = 0, Maximum = 10, ShowAxis = false, ShowTitle = false, ShowLabels = false, ShowTicks = false, ShowGridLines = false, ShowMinorGridLines = false };
        var chart = new CartesianChart { Height = 260, Background = new SolidColorBrush(background), XAxis = Axis(), YAxis = Axis(), ShowLegend = false, ShowGridLines = false, IsTooltipEnabled = false };
        (vertical ? chart.YAxis : chart.XAxis).ScaleBreaks.Add(new ChartScaleBreak { Start = 4, End = 6, CompressionRatio = .2 });
        chart.Series.Add(new LineSeries { Stroke = Brushes.Transparent, ShowMarkers = false, ItemsSource = new ChartPoint[] { new(0, 0), new(10, 10) } });
        ChartShapeAnnotation annotation = kind switch
        {
            0 => new RectangleAnnotation { Fill = new SolidColorBrush(ink) },
            1 => new EllipseAnnotation { Fill = new SolidColorBrush(ink) },
            _ => new ArrowAnnotation { Stroke = new SolidColorBrush(ink), Fill = new SolidColorBrush(ink), StrokeThickness = 4, HeadLength = 18, HeadWidth = 18, ArrowHeads = ChartArrowHeads.Both },
        };
        annotation.X1 = vertical ? kind == 2 ? 5 : 3 : 2; annotation.X2 = vertical ? kind == 2 ? 5 : 7 : 8;
        annotation.Y1 = vertical ? 2 : kind == 2 ? 5 : 3; annotation.Y2 = vertical ? 8 : kind == 2 ? 5 : 7;
        chart.Annotations.Add(annotation); return chart;
    }

    private static ShapePixelEvidence ReadShapeBreakPixels(CartesianChart chart, int kind, bool vertical, string theme, bool native, Color background, Color ink)
    {
        var size = new PixelSize((int)Math.Ceiling(chart.Bounds.Width), (int)Math.Ceiling(chart.Bounds.Height));
        using var bitmap = new RenderTargetBitmap(size, new Vector(96, 96)); bitmap.Render(chart);
        var pixels = ReadShapePixels(bitmap); var plot = chart.PlotArea;
        var before = vertical ? new Point(plot.Center.X, plot.Bottom - plot.Height * 3 / 8.4) : new Point(plot.Left + plot.Width * 3 / 8.4, plot.Center.Y);
        var after = vertical ? new Point(plot.Center.X, plot.Bottom - plot.Height * 5.4 / 8.4) : new Point(plot.Left + plot.Width * 5.4 / 8.4, plot.Center.Y);
        var low = (int)Math.Ceiling(vertical ? plot.Bottom - plot.Height * 4.4 / 8.4 : plot.Left + plot.Width * 4 / 8.4) + 2;
        var high = (int)Math.Floor(vertical ? plot.Bottom - plot.Height * 4 / 8.4 : plot.Left + plot.Width * 4.4 / 8.4) - 2;
        var gap = vertical ? new PixelRect((int)Math.Round(plot.Center.X) - 1, low, 3, Math.Max(0, high - low + 1)) : new PixelRect(low, (int)Math.Round(plot.Center.Y) - 1, Math.Max(0, high - low + 1), 3);
        Point Project(double x, double y) => new(plot.Left + plot.Width * (vertical ? x / 10 : x <= 4 ? x / 8.4 : (x - 1.6) / 8.4),
            plot.Bottom - plot.Height * (vertical ? y <= 4 ? y / 8.4 : (y - 1.6) / 8.4 : y / 10));
        var detail = kind == 1 ? Project(vertical ? 3.15 : 2.15, vertical ? 2.15 : 3.15) :
            (vertical ? Project(5, 2) + new Vector(6, -15) : Project(2, 5) + new Vector(15, 6));
        var otherHead = vertical ? Project(5, 8) + new Vector(6, 15) : Project(8, 5) + new Vector(-15, 6);
        return new(theme, new[] { "Rectangle", "Ellipse", "Arrow" }[kind], vertical ? "Y" : "X", native, size.Width, size.Height, plot,
            pixels.Count(Box(before, 3), ink, 24), pixels.Count(Box(after, 3), ink, 24), pixels.Count(gap, ink, 24), pixels.Count(gap, background, 3), gap.Width * gap.Height,
            pixels.Count(Box(detail, 1), ink, 24), pixels.Count(Box(detail, 1), background, 3), pixels.Count(Box(otherHead, 1), ink, 24));
        static PixelRect Box(Point point, int radius) => new((int)Math.Round(point.X) - radius, (int)Math.Round(point.Y) - radius, radius * 2 + 1, radius * 2 + 1);
    }

    private static ShapeRaster ReadShapePixels(Bitmap bitmap)
    {
        using var converted = new WriteableBitmap(bitmap.PixelSize, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var frame = converted.Lock(); bitmap.CopyPixels(frame);
        var pixels = new byte[checked(frame.RowBytes * bitmap.PixelSize.Height)]; Marshal.Copy(frame.Address, pixels, 0, pixels.Length);
        return new(bitmap.PixelSize.Width, bitmap.PixelSize.Height, frame.RowBytes, pixels);
    }

    private sealed record ShapeRaster(int Width, int Height, int Stride, byte[] Pixels)
    {
        internal int Count(PixelRect rectangle, Color color, int tolerance)
        {
            var count = 0;
            for (var y = Math.Max(0, rectangle.Y); y < Math.Min(Height, rectangle.Bottom); y++)
                for (var x = Math.Max(0, rectangle.X); x < Math.Min(Width, rectangle.Right); x++)
                {
                    var offset = y * Stride + x * 4;
                    if (Pixels[offset + 3] >= 250 && Math.Abs(Pixels[offset] - color.B) <= tolerance && Math.Abs(Pixels[offset + 1] - color.G) <= tolerance && Math.Abs(Pixels[offset + 2] - color.R) <= tolerance) count++;
                }
            return count;
        }
    }
    private sealed record ShapeSmokeEvidence(string LightImage, string DarkImage, string LightPixelsImage, string DarkPixelsImage, string LightExportImage, string DarkExportImage,
        List<ShapeExportEvidence> Exports, List<ShapePixelEvidence> NativePixels);
    private sealed record ShapeExportEvidence(string Theme, bool Exported, int Width, int Height, int RectangleInkPixels, int EllipseInkPixels, int ArrowInkPixels);
    private sealed record ShapePixelEvidence(string Theme, string Shape, string Axis, bool NativeWindowCreated, int BitmapWidth, int BitmapHeight, Rect PlotArea,
        int BeforeInkPixels, int AfterInkPixels, int GapInkPixels, int GapBackgroundPixels, int GapPixels, int ShapeDetailInkPixels, int ShapeDetailBackgroundPixels, int OtherHeadInkPixels);
}
