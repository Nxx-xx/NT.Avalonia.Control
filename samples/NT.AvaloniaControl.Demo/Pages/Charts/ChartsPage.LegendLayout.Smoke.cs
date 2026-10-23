using Avalonia;
using Avalonia.Layout;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Demo.Pages.Charts;

internal sealed partial class ChartsPage
{
    private readonly List<object> _legendLayoutEvidence = [];

    private void RunLegendLayoutSmoke(Dictionary<string, bool> checks, CartesianChart chart, string commonImage)
    {
        try
        {
            chart.ShowLegend = false;
            var unreservedPlot = chart.PlotArea;
            chart.IsLegendFloating = true; chart.ShowLegend = true; chart.LegendPosition = ChartLegendPosition.Top;
            chart.LegendHorizontalAlignment = HorizontalAlignment.Right;
            var legend = chart.LegendItems;
            checks["C2浮动图例不挤占绘图区并按右边对齐"] = chart.PlotArea == unreservedPlot && legend.Count == 3 &&
                legend.All(item => unreservedPlot.Contains(item.Bounds)) && Math.Abs(legend.Max(item => item.Bounds.Right) - (unreservedPlot.Right - 8)) < .001;
            var first = chart.Series[0]; var color = chart.GetSeriesBrush(first);
            RouteCommonClick(chart, chart.LegendItems.Single(item => item.Series == first).Bounds.Center);
            var hidden = !first.IsVisible;
            RouteCommonClick(chart, chart.LegendItems.Single(item => item.Series == first).Bounds.Center);
            checks["C2浮动图例合成点击可恢复系列且保留配色"] = hidden && first.IsVisible && Equals(color, chart.GetSeriesBrush(first));

            var path = Path.Combine(Path.GetDirectoryName(commonImage)!, Path.GetFileNameWithoutExtension(commonImage) + "-floating-legend.png");
            var saved = chart.TryExportPng(path, new() { PixelSize = new(960, 540), Dpi = 144 });
            var ink = new List<int>();
            if (saved)
            {
                var raster = ReadCommonExportRaster(File.ReadAllBytes(path));
                var scale = Math.Min(raster.Width / chart.Bounds.Width, raster.Height / chart.Bounds.Height);
                var offset = new Vector((raster.Width - chart.Bounds.Width * scale) / 2, (raster.Height - chart.Bounds.Height * scale) / 2);
                foreach (var item in chart.LegendItems)
                {
                    var point = item.Bounds.TopLeft + new Vector(6, 7);
                    var pixel = new Point(point.X * scale + offset.X, point.Y * scale + offset.Y);
                    ink.Add(CountCommonExportInk(raster, Around(pixel, Math.Max(3, (int)Math.Ceiling(4 * scale))), item.Brush, 40));
                }
            }
            checks["C2浮动图例PNG包含三组实际符号"] = saved && ink.Count == 3 && ink.All(value => value > 0);
            _legendLayoutEvidence.Add(new { Path = path, Exported = saved, Plot = chart.PlotArea, Items = chart.LegendItems.Select(item => new { item.Text, item.Bounds }).ToArray(), SymbolInkPixels = ink });
        }
        finally
        {
            chart.IsLegendFloating = false; chart.LegendPosition = ChartLegendPosition.Bottom;
            chart.LegendHorizontalAlignment = HorizontalAlignment.Left; chart.LegendVerticalAlignment = VerticalAlignment.Top; chart.ShowLegend = true;
        }
    }
}
