using System.Runtime.InteropServices;
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
    private readonly List<BreakPixelEvidence> _breakPixelEvidence = [];

    private void VerifyBreakPixels(Dictionary<string, bool> checks, ThemeVariant theme, string imagePath)
    {
        var dark = theme == ThemeVariant.Dark;
        var background = dark ? Color.Parse("#111722") : Color.Parse("#F8FAFC");
        var ink = Color.Parse("#CB11A5");
        var horizontal = CreateBreakPixelChart(false, background, ink);
        var vertical = CreateBreakPixelChart(true, background, ink);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 16, Margin = new Thickness(12) };
        var first = new StackPanel { Spacing = 8, Children = { new TextBlock { Text = "X 断轴：两侧保留，缝内无折线" }, horizontal } };
        var last = new StackPanel { Spacing = 8, Children = { new TextBlock { Text = "Y 断轴：两侧保留，缝内无折线" }, vertical } };
        Grid.SetColumn(last, 1); grid.Children.Add(first); grid.Children.Add(last);
        var window = new Window { Title = "Charts · 原生断轴像素检查", Width = 1000, Height = 340, Content = grid, RequestedThemeVariant = theme };
        try
        {
            window.Show(_hostWindow); window.UpdateLayout();
            var native = window.TryGetPlatformHandle() is { Handle: not 0 };
            foreach (var item in new[] { (Chart: horizontal, Vertical: false), (Chart: vertical, Vertical: true) })
            {
                var result = ReadBreakPixels(item.Chart, item.Vertical, dark ? "dark" : "light", native, background, ink);
                _breakPixelEvidence.Add(result);
                checks[$"C1{(dark ? "暗色" : "亮色")}{(item.Vertical ? "纵向" : "横向")}断轴实际像素保留两侧且缝内无墨迹"] =
                    result.NativeWindowCreated && result.BeforeInkPixels >= 5 && result.AfterInkPixels >= 5 && result.GapPixels >= 3 &&
                    result.GapInkPixels == 0 && result.GapBackgroundPixels == result.GapPixels;
            }
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width), (int)Math.Ceiling(window.ClientSize.Height)), new Vector(96, 96));
            bitmap.Render(window);
            Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!); bitmap.Save(imagePath);
        }
        finally { window.Close(); }
    }

    private static CartesianChart CreateBreakPixelChart(bool vertical, Color background, Color ink)
    {
        static NumericAxis Axis() => new()
        {
            Minimum = 0, Maximum = 10, ShowAxis = false, ShowTitle = false, ShowLabels = false,
            ShowTicks = false, ShowGridLines = false, ShowMinorGridLines = false,
        };
        var chart = new CartesianChart
        {
            Height = 260, Background = new SolidColorBrush(background), XAxis = Axis(), YAxis = Axis(),
            ShowLegend = false, ShowGridLines = false, IsTooltipEnabled = false,
        };
        (vertical ? chart.YAxis : chart.XAxis).ScaleBreaks.Add(new ChartScaleBreak { Start = 4, End = 6, CompressionRatio = .2 });
        chart.Series.Add(new LineSeries
        {
            Stroke = new SolidColorBrush(ink), MarkerSize = 0, StrokeThickness = 6,
            ItemsSource = vertical ? new ChartPoint[] { new(5, 2), new(5, 8) } : new ChartPoint[] { new(2, 5), new(8, 5) },
        });
        return chart;
    }

    private static BreakPixelEvidence ReadBreakPixels(CartesianChart chart, bool vertical, string theme, bool native, Color background, Color ink)
    {
        var size = new PixelSize((int)Math.Ceiling(chart.Bounds.Width), (int)Math.Ceiling(chart.Bounds.Height));
        using var bitmap = new RenderTargetBitmap(size, new Vector(96, 96)); bitmap.Render(chart);
        using var converted = new WriteableBitmap(size, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var frame = converted.Lock(); bitmap.CopyPixels(frame);
        var pixels = new byte[checked(frame.RowBytes * size.Height)]; Marshal.Copy(frame.Address, pixels, 0, pixels.Length);
        var plot = chart.PlotArea;
        var before = vertical ? new Point(plot.Center.X, plot.Bottom - plot.Height * 3 / 8.4) : new Point(plot.Left + plot.Width * 3 / 8.4, plot.Center.Y);
        var after = vertical ? new Point(plot.Center.X, plot.Bottom - plot.Height * 5.4 / 8.4) : new Point(plot.Left + plot.Width * 5.4 / 8.4, plot.Center.Y);
        var gapLow = vertical ? plot.Bottom - plot.Height * 4.4 / 8.4 : plot.Left + plot.Width * 4 / 8.4;
        var gapHigh = vertical ? plot.Bottom - plot.Height * 4 / 8.4 : plot.Left + plot.Width * 4.4 / 8.4;
        var low = (int)Math.Ceiling(gapLow) + 2; var high = (int)Math.Floor(gapHigh) - 2;
        var gap = vertical ? new PixelRect((int)Math.Round(plot.Center.X) - 1, low, 3, Math.Max(0, high - low + 1)) :
            new PixelRect(low, (int)Math.Round(plot.Center.Y) - 1, Math.Max(0, high - low + 1), 3);
        var beforeBox = new PixelRect((int)Math.Round(before.X) - 3, (int)Math.Round(before.Y) - 3, 7, 7);
        var afterBox = new PixelRect((int)Math.Round(after.X) - 3, (int)Math.Round(after.Y) - 3, 7, 7);
        return new(theme, vertical ? "Y" : "X", native, size.Width, size.Height, plot,
            Count(beforeBox, ink, 24), Count(afterBox, ink, 24), Count(gap, ink, 24), Count(gap, background, 3), gap.Width * gap.Height);

        int Count(PixelRect rectangle, Color color, int tolerance)
        {
            var count = 0;
            for (var y = Math.Max(0, rectangle.Y); y < Math.Min(size.Height, rectangle.Bottom); y++)
                for (var x = Math.Max(0, rectangle.X); x < Math.Min(size.Width, rectangle.Right); x++)
                {
                    var offset = y * frame.RowBytes + x * 4;
                    if (pixels[offset + 3] >= 250 && Math.Abs(pixels[offset] - color.B) <= tolerance &&
                        Math.Abs(pixels[offset + 1] - color.G) <= tolerance && Math.Abs(pixels[offset + 2] - color.R) <= tolerance) count++;
                }
            return count;
        }
    }

    private sealed record BreakPixelEvidence(string Theme, string Axis, bool NativeWindowCreated, int BitmapWidth, int BitmapHeight, Rect PlotArea,
        int BeforeInkPixels, int AfterInkPixels, int GapInkPixels, int GapBackgroundPixels, int GapPixels);
}
