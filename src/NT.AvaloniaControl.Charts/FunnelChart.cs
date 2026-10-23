using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>Source-ordered funnel stages with equal height slots and value-scaled boundary widths.</summary>
/// <remarks>Only finite positive values create stages. Width = NeckWidth + (1 - NeckWidth) * value / maximum;
/// the final boundary uses NeckWidth. Segment gaps trim this continuous outline without changing source values.</remarks>
public sealed class FunnelChart : AnalysisChartBase
{
    public static readonly StyledProperty<double> SegmentGapProperty = AvaloniaProperty.Register<FunnelChart, double>(nameof(SegmentGap), 2,
        validate: value => double.IsFinite(value) && value >= 0 && value <= 4096);
    public static readonly StyledProperty<double> NeckWidthProperty = AvaloniaProperty.Register<FunnelChart, double>(nameof(NeckWidth), .3,
        validate: value => double.IsFinite(value) && value >= 0 && value <= 1);

    /// <summary>Maximum empty space between consecutive stages, in DIPs. Short stages clamp their own gap.</summary>
    public double SegmentGap { get => GetValue(SegmentGapProperty); set => SetValue(SegmentGapProperty, value); }
    /// <summary>Minimum boundary width as a fraction of the plot width.</summary>
    public double NeckWidth { get => GetValue(NeckWidthProperty); set => SetValue(NeckWidthProperty, value); }

    protected override IReadOnlyList<AnalysisMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot) =>
        FunnelPyramidGeometry.Funnel(data, plot, NeckWidth, SegmentGap, BrushAt);
}

internal static class FunnelPyramidGeometry
{
    internal static IReadOnlyList<AnalysisMarkInfo> Funnel(ChartDataSnapshot data, Rect plot, double neck, double gap,
        Func<int, IBrush?> brushAt)
    {
        if (!Drawable(plot)) return [];
        var values = Positive(data, out var maximum);
        if (values.Count == 0) return [];
        var result = new List<AnalysisMarkInfo>(values.Count);
        for (var index = 0; index < values.Count; index++)
        {
            var from = plot.Top + plot.Height * ((double)index / values.Count);
            var to = index == values.Count - 1 ? plot.Bottom : plot.Top + plot.Height * ((double)(index + 1) / values.Count);
            if (!(to > from)) continue;
            var widthFrom = plot.Width * (neck + (1 - neck) * (values[index].Point.Y / maximum));
            var widthTo = index + 1 < values.Count ? plot.Width * (neck + (1 - neck) * (values[index + 1].Point.Y / maximum)) : plot.Width * neck;
            var trim = Math.Min(gap, to - from) / 2;
            var top = from + (index > 0 ? trim : 0);
            var bottom = to - (index + 1 < values.Count ? trim : 0);
            if (!(bottom > top)) continue;
            var topFraction = (top - from) / (to - from);
            var bottomFraction = (bottom - from) / (to - from);
            // Weighted interpolation keeps finite widths even when data values approach MaxValue.
            var topWidth = widthFrom * (1 - topFraction) + widthTo * topFraction;
            var bottomWidth = widthFrom * (1 - bottomFraction) + widthTo * bottomFraction;
            Add(result, values[index], plot, top, bottom, topWidth, bottomWidth, brushAt(values[index].SourceIndex));
        }
        return result.AsReadOnly();
    }

    internal static IReadOnlyList<AnalysisMarkInfo> Pyramid(ChartDataSnapshot data, Rect plot, double gap,
        Func<int, IBrush?> brushAt)
    {
        if (!Drawable(plot)) return [];
        var values = Positive(data, out var maximum);
        if (values.Count == 0) return [];
        var total = 0d; var correction = 0d;
        foreach (var point in values) AddCompensated(point.Point.Y / maximum, ref total, ref correction);
        var result = new List<AnalysisMarkInfo>(values.Count);
        var cumulative = 0d; var cumulativeCorrection = 0d; var start = 0d;
        for (var index = 0; index < values.Count; index++)
        {
            AddCompensated(values[index].Point.Y / maximum, ref cumulative, ref cumulativeCorrection);
            var end = index == values.Count - 1 ? 1 : Math.Clamp(Math.Sqrt(cumulative / total), start, 1);
            var rawTop = plot.Top + plot.Height * start;
            var rawBottom = index == values.Count - 1 ? plot.Bottom : plot.Top + plot.Height * end;
            var trim = Math.Min(gap, Math.Max(0, rawBottom - rawTop)) / 2;
            var top = rawTop + (index > 0 ? trim : 0);
            var bottom = rawBottom - (index + 1 < values.Count ? trim : 0);
            // Width grows linearly down the triangle. Its accumulated area therefore grows as y².
            var topWidth = plot.Width * Math.Clamp((top - plot.Top) / plot.Height, 0, 1);
            var bottomWidth = plot.Width * Math.Clamp((bottom - plot.Top) / plot.Height, 0, 1);
            Add(result, values[index], plot, top, bottom, topWidth, bottomWidth, brushAt(values[index].SourceIndex));
            start = end;
        }
        return result.AsReadOnly();
    }

    private static List<ChartDataPoint> Positive(ChartDataSnapshot data, out double maximum)
    {
        var result = new List<ChartDataPoint>(); maximum = 0;
        foreach (var point in data.Points)
            if (!point.IsEmpty && double.IsFinite(point.Point.Y) && point.Point.Y > 0)
            { result.Add(point); maximum = Math.Max(maximum, point.Point.Y); }
        return result;
    }

    private static bool Drawable(Rect plot) => double.IsFinite(plot.X) && double.IsFinite(plot.Y) &&
        double.IsFinite(plot.Width) && double.IsFinite(plot.Height) && plot.Width > 0 && plot.Height > 0 &&
        double.IsFinite(plot.Right) && double.IsFinite(plot.Bottom);

    private static void Add(List<AnalysisMarkInfo> result, ChartDataPoint data, Rect plot,
        double top, double bottom, double topWidth, double bottomWidth, IBrush? fill)
    {
        if (!(bottom > top) || !(Math.Max(topWidth, bottomWidth) > 0)) return;
        var center = plot.Left + plot.Width / 2;
        Point[] vertices = [new(center - topWidth / 2, top), new(center + topWidth / 2, top),
            new(center + bottomWidth / 2, bottom), new(center - bottomWidth / 2, bottom)];
        if (vertices.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y))) return;
        // A positive source value may have no representable paint area. Do not inflate it into a fake stage.
        if (vertices[0] == vertices[1] && vertices[2] == vertices[3]) return;
        var geometry = new PolylineGeometry(vertices, true);
        result.Add(new AnalysisMarkInfo(data, geometry, fill, new Point(center, top + (bottom - top) / 2)));
    }

    private static void AddCompensated(double value, ref double sum, ref double correction)
    {
        var adjusted = value - correction; var next = sum + adjusted;
        correction = (next - sum) - adjusted; sum = next;
    }
}
