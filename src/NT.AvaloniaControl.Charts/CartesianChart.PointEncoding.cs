using System.Globalization;
using System.Collections;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace NT.AvaloniaControl.Charts;

public sealed partial class CartesianChart
{
    private static bool EncodedPoints(ChartSeries series) => series.Renderer is ChartPointSeriesRenderer;

    private static void PreparePointSources(SeriesSnapshot[] snapshots)
    {
        foreach (var snapshot in snapshots.Where(item => EncodedPoints(item.Series)))
            snapshot.Data = snapshot.Data.WithRawPoints();
    }

    private static void PreparePointData(SeriesSnapshot[] snapshots)
    {
        foreach (var snapshot in snapshots.Where(item => EncodedPoints(item.Series)))
        {
            // Generic indexes use nonfinite coordinates as their gap sentinel.
            // Keep a published finite IsEmpty sample in the source snapshot, but
            // prevent center-based readouts from reviving it in the display scene.
            ChartDataValue[]? gaps = null;
            for (var index = 0; index < snapshot.Data.Count; index++)
            {
                var value = snapshot.Data.GetValue(index);
                if (value.IsEmpty && IsValid(value.Point))
                {
                    gaps ??= Enumerable.Range(0, snapshot.Data.Count).Select(snapshot.Data.GetValue).ToArray();
                    gaps[index] = value with { Point = value.Point with { Y = double.NaN } };
                }
            }
            if (gaps is not null) snapshot.Data = snapshot.Data.WithValues(gaps);
            snapshot.Data = snapshot.Data.WithPointStyles(ChartPointEncoding.CreateStyles(snapshot.Series, snapshot.Data, snapshot.Brush));
        }
    }

    private static double DisplayedPointRadius(ChartSeries series, double? diameter)
    {
        if (diameter is null) return ChartMarkerGeometry.Radius(series);
        if (!series.ShowMarkers || !double.IsFinite(diameter.Value) || diameter <= 0) return 0;
        var radius = diameter.Value / 2;
        var extent = series.MarkerShape switch
        {
            ChartMarkerShape.Square or ChartMarkerShape.Cross => radius * Math.Sqrt(2),
            ChartMarkerShape.Rectangle => radius * Math.Sqrt(1.36),
            _ => radius,
        };
        return extent + ChartMarkerGeometry.StrokeThickness(series) / 2;
    }

    private static string PointReadingSuffix(ChartPointHit point)
    {
        var analysisSuffix = string.Empty;
        if (point.Analysis is { } analysis)
        {
            var calculated = analysis.CalculatedPoint.Y.ToString("G6", CultureInfo.CurrentCulture);
            if (analysis.IsSynthetic) analysisSuffix = $" (拟合采样; 计算值: {calculated})";
            else
            {
                analysisSuffix = $" (端点计算值: {calculated}";
                if (point.RawPoint is { } raw) analysisSuffix += $"; 原始端点: {raw.Y.ToString("G6", CultureInfo.CurrentCulture)}";
                if (analysis.SourceIndex is { } index) analysisSuffix += $"; 源端点索引: {index}";
                analysisSuffix += ")";
            }
        }
        if (point.Financial is { } financial)
        {
            string Price(double value) => value.ToString("G6", CultureInfo.CurrentCulture);
            var source = financial.Kind == ChartFinancialTransformKind.Raw ? $"源记录: {financial.SourceEndIndex}" :
                $"{financial.Kind}, 源范围: {financial.SourceStartIndex}–{financial.SourceEndIndex}, 原始收盘: {Price(financial.SourceEnd.Close)}";
            return analysisSuffix + $" ({financial.Time:yyyy-MM-dd HH:mm zzz}; O={Price(financial.Open)}, H={Price(financial.High)}, L={Price(financial.Low)}, C={Price(financial.Close)}; {source})";
        }
        if (point.MarkerDiameter is null) return analysisSuffix;
        var parts = new List<string>();
        if (point.SizeValue is { } size) parts.Add($"大小值: {size.ToString("G6", CultureInfo.CurrentCulture)}");
        if (point.ColorValue is { } color) parts.Add($"颜色值: {Convert.ToString(color, CultureInfo.CurrentCulture)}");
        return analysisSuffix + (parts.Count == 0 ? string.Empty : $" ({string.Join(", ", parts)})");
    }

    private static IBrush? InterpolatePointFill(IBrush? from, IBrush? to, double progress)
    {
        if (progress <= 0) return from;
        if (progress >= 1) return to;
        if (from is not ISolidColorBrush first || to is not ISolidColorBrush last) return to;
        byte Channel(byte a, byte b) => (byte)Math.Clamp(Math.Round(a * (1 - progress) + b * progress, MidpointRounding.AwayFromZero), 0, 255);
        var color = Color.FromArgb(Channel(first.Color.A, last.Color.A), Channel(first.Color.R, last.Color.R),
            Channel(first.Color.G, last.Color.G), Channel(first.Color.B, last.Color.B));
        return new ImmutableSolidColorBrush(color, first.Opacity * (1 - progress) + last.Opacity * progress);
    }

    private static IReadOnlyList<Rect>? CreatePointHitRegions(Rect plot, ChartScale x, ChartScale y)
    {
        var xGaps = BreakRectangles(plot, x, true).ToArray();
        var yGaps = BreakRectangles(plot, y, false).ToArray();
        if (xGaps.Length == 0 && yGaps.Length == 0) return null;
        static (double Start, double End)[] Intervals(double minimum, double maximum, IEnumerable<(double Start, double End)> gaps)
        {
            var result = new List<(double Start, double End)>();
            var cursor = minimum;
            foreach (var gap in gaps.OrderBy(item => item.Start))
            {
                if (gap.Start > cursor) result.Add((cursor, gap.Start));
                cursor = Math.Max(cursor, gap.End);
            }
            if (cursor < maximum) result.Add((cursor, maximum));
            return result.ToArray();
        }
        return new PointHitRegionGrid(Intervals(plot.Left, plot.Right, xGaps.Select(gap => (gap.Left, gap.Right))),
            Intervals(plot.Top, plot.Bottom, yGaps.Select(gap => (gap.Top, gap.Bottom))));
    }

    // Axis intervals are stored once; their product is enumerated without allocating
    // one rectangle per cell, including charts with breaks in both dimensions.
    private sealed class PointHitRegionGrid((double Start, double End)[] x, (double Start, double End)[] y) : IReadOnlyList<Rect>
    {
        public int Count => checked(x.Length * y.Length);
        public Rect this[int index]
        {
            get
            {
                if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
                var horizontal = x[index / y.Length]; var vertical = y[index % y.Length];
                return new(horizontal.Start, vertical.Start, horizontal.End - horizontal.Start, vertical.End - vertical.Start);
            }
        }
        public IEnumerator<Rect> GetEnumerator()
        {
            foreach (var horizontal in x) foreach (var vertical in y)
                yield return new(horizontal.Start, vertical.Start, horizontal.End - horizontal.Start, vertical.End - vertical.Start);
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
