using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private static CommonExportPixelsEvidence VerifyCommonExportPixels(CartesianChart chart, byte[] primaryPng, bool dark)
    {
        var primary = ReadCommonExportRaster(primaryPng);
        var background = Brush(dark ? "#182232" : "#FFFFFF");
        var resolutions = new List<CommonExportDpiEvidence>();
        var invariant = true;
        foreach (var dpi in new[] { 96d, 144d, 192d })
        {
            using var stream = new MemoryStream();
            if (!chart.TryExportPng(stream, new() { PixelSize = new(960, 540), Dpi = dpi, Background = background }))
                throw new InvalidOperationException($"C2 PNG {dpi} DPI 导出失败。");
            var bytes = stream.ToArray();
            var raster = ReadCommonExportRaster(bytes);
            var matches = raster.Width == primary.Width && raster.Height == primary.Height && raster.Stride == primary.Stride && raster.Pixels.AsSpan().SequenceEqual(primary.Pixels);
            var density = TryReadCommonPngDensity(bytes, out var xDensity, out var yDensity, out var unit);
            var expectedDensity = (uint)Math.Round(dpi / .0254, MidpointRounding.AwayFromZero);
            var densityMatches = density && unit == 1 && xDensity == expectedDensity && yDensity == expectedDensity;
            invariant &= matches && densityMatches;
            resolutions.Add(new(dpi, Convert.ToHexString(SHA256.HashData(raster.Pixels)), matches, densityMatches, xDensity, yDensity));
        }

        // This oracle uses the arranged chart's public bounds/range, not the export
        // implementation's layout helper or its drawing-group geometry bounds.
        var scale = Math.Min(primary.Width / chart.Bounds.Width, primary.Height / chart.Bounds.Height);
        var offset = new Vector((primary.Width - chart.Bounds.Width * scale) / 2, (primary.Height - chart.Bounds.Height * scale) / 2);
        Point Map(Point point) => new(point.X * scale + offset.X, point.Y * scale + offset.Y);
        Rect MapRect(Rect rectangle) => new(Map(rectangle.TopLeft), Map(rectangle.BottomRight));
        var plot = chart.PlotArea;
        var targetPlot = MapRect(plot);
        var plotFits = targetPlot.Left >= 0 && targetPlot.Top >= 0 && targetPlot.Right <= primary.Width && targetPlot.Bottom <= primary.Height;
        var bottomInk = chart.XAxis.AxisBrush ?? chart.AxisBrush;
        var leftInk = chart.YAxis.AxisBrush ?? chart.AxisBrush;
        var bottomAxis = CountCommonExportInk(primary, Around(Map(new Point(plot.Center.X, plot.Bottom)), 4), bottomInk, 56);
        // In this fixture X=0 is also a grid line. X grids are painted after the Y
        // axis and cover its one-pixel center. Verify that visible boundary in its
        // final grid color, then verify the Y=80 tick outside the plot in axis ink.
        // The public near-axis layout gives the axis edge independently of export.
        var leftLayout = chart.GetAxisLayout(chart.YAxis) ?? throw new InvalidOperationException("C2 PNG 缺少纵轴布局。");
        var axisEdge = leftLayout.Bounds.Right;
        var tick = Map(new Point(axisEdge - chart.YAxis.TickLength * .6, plot.Bottom - plot.Height * .8));
        var usesGrid = chart.ShowGridLines && chart.XAxis.ShowGridLines;
        var boundaryBrush = usesGrid ? chart.XAxis.GridLineBrush ?? chart.GridLineBrush : leftInk;
        // Sample above the translucent band. The overlapping antialiased grid/axis
        // edge differs slightly from either solid brush, but this tolerance still
        // excludes both theme backgrounds in the chosen white/solid backdrop region.
        var boundary = Map(new Point(axisEdge, plot.Top + plot.Height * .1));
        var leftBoundary = CountCommonExportInk(primary, Around(boundary, 4), boundaryBrush, usesGrid ? 14 : 32);
        var leftAxis = CountCommonExportInk(primary, Around(tick, 2), chart.YAxis.TickBrush ?? leftInk, 32);
        var range = chart.VisibleRange ?? throw new InvalidOperationException("C2 PNG 缺少原始可见范围。");
        var endpoints = new List<CommonExportEndpointEvidence>();
        foreach (var series in chart.Series.Where(series => series.IsVisible))
        {
            var points = series.CreateSnapshot().Points.Where(point => double.IsFinite(point.Point.X) && double.IsFinite(point.Point.Y)).ToArray();
            foreach (var point in new[] { points[0], points[^1] })
            {
                var raw = point.Point;
                var position = Map(new Point(plot.Left + (raw.X - range.XMinimum) / (range.XMaximum - range.XMinimum) * plot.Width,
                    plot.Bottom - (raw.Y - range.YMinimum) / (range.YMaximum - range.YMinimum) * plot.Height));
                var ink = CountCommonExportInk(primary, Around(position, Math.Max(4, (int)Math.Ceiling(series.MarkerSize * scale / 2))), chart.GetSeriesBrush(series), 40);
                endpoints.Add(new(series.Title ?? "Series", point.SourceIndex, raw.X, raw.Y, position, ink));
            }
        }
        var legendItems = new List<CommonExportLegendEvidence>();
        foreach (var item in chart.LegendItems)
        {
            var symbol = Map(item.Bounds.TopLeft + new Vector(6, 7));
            var symbolInk = CountCommonExportInk(primary, Around(symbol, Math.Max(3, (int)Math.Ceiling(3 * scale))), item.Brush, 40);
            var label = MapRect(new Rect(item.Bounds.X + 20, item.Bounds.Y, Math.Max(1, item.Bounds.Width - 23), item.Bounds.Height));
            var textInk = CountCommonExportInk(primary, new PixelRect((int)Math.Floor(label.X), (int)Math.Floor(label.Y), (int)Math.Ceiling(label.Width), (int)Math.Ceiling(label.Height)), chart.Foreground, 64);
            legendItems.Add(new(item.Text, symbol, symbolInk, textInk));
        }
        return new(invariant, resolutions, targetPlot, plotFits, bottomAxis, leftAxis, leftBoundary, tick, endpoints, legendItems);
    }

    private static PixelRect Around(Point point, int radius) => new((int)Math.Round(point.X) - radius, (int)Math.Round(point.Y) - radius, radius * 2 + 1, radius * 2 + 1);

    private static CommonExportRaster ReadCommonExportRaster(byte[] png)
    {
        using var stream = new MemoryStream(png, writable: false);
        using var bitmap = new Bitmap(stream);
        using var converted = new WriteableBitmap(bitmap.PixelSize, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var frame = converted.Lock();
        bitmap.CopyPixels(frame);
        var pixels = new byte[checked(frame.RowBytes * bitmap.PixelSize.Height)];
        Marshal.Copy(frame.Address, pixels, 0, pixels.Length);
        return new(bitmap.PixelSize.Width, bitmap.PixelSize.Height, frame.RowBytes, pixels);
    }

    private static int CountCommonExportInk(CommonExportRaster raster, PixelRect rectangle, IBrush? brush, int tolerance)
    {
        if (brush is not ISolidColorBrush solid) return 0;
        var color = solid.Color;
        var count = 0;
        for (var y = Math.Max(0, rectangle.Y); y < Math.Min(raster.Height, rectangle.Bottom); y++)
            for (var x = Math.Max(0, rectangle.X); x < Math.Min(raster.Width, rectangle.Right); x++)
            {
                var offset = y * raster.Stride + x * 4;
                if (raster.Pixels[offset + 3] >= 250 && Math.Abs(raster.Pixels[offset] - color.B) <= tolerance &&
                    Math.Abs(raster.Pixels[offset + 1] - color.G) <= tolerance && Math.Abs(raster.Pixels[offset + 2] - color.R) <= tolerance) count++;
            }
        return count;
    }

    private sealed record CommonExportRaster(int Width, int Height, int Stride, byte[] Pixels);
    private sealed record CommonExportDpiEvidence(double Dpi, string DecodedPixelSha256, bool MatchesPrimaryPixels, bool DensityMatches, uint PixelsPerMeterX, uint PixelsPerMeterY);
    private sealed record CommonExportEndpointEvidence(string Series, int SourceIndex, double X, double Y, Point ExpectedPixel, int InkPixels);
    private sealed record CommonExportLegendEvidence(string Text, Point ExpectedSymbolPixel, int SymbolInkPixels, int TextInkPixels);
    private sealed record CommonExportPixelsEvidence(bool DpiInvariant, IReadOnlyList<CommonExportDpiEvidence> Resolutions, Rect ExpectedPlotPixels,
        bool PlotFitsCanvas, int BottomAxisInkPixels, int LeftAxisInkPixels, int LeftBoundaryInkPixels, Point ExpectedLeftTickPixel,
        IReadOnlyList<CommonExportEndpointEvidence> SeriesEndpoints, IReadOnlyList<CommonExportLegendEvidence> LegendItems);
}
