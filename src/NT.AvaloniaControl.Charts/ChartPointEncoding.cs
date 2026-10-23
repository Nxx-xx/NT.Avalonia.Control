using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace NT.AvaloniaControl.Charts;

internal static class ChartPointEncoding
{
    internal static ChartPointStyle[] CreateStyles(ChartSeries series, ChartDataSnapshot data, IBrush fallback)
    {
        if (series is not IChartPointSeries options) throw new ArgumentException("A point series is required.", nameof(series));
        if (options.MinimumSize > options.MaximumSize)
            throw new InvalidOperationException("MinimumSize must not exceed MaximumSize.");
        var hasSize = !string.IsNullOrWhiteSpace(options.SizePath) || series is BubbleSeries && !string.IsNullOrWhiteSpace(series.WeightPath);
        var hasColor = !string.IsNullOrWhiteSpace(options.ColorPath);
        var sizeMinimum = options.SizeMinimum ?? 0;
        var sizeMaximum = 0d;
        var colorMinimum = double.PositiveInfinity;
        var colorMaximum = double.NegativeInfinity;
        for (var ordinal = 0; ordinal < data.Count; ordinal++)
        {
            var point = data.Points[ordinal];
            if (point.IsEmpty || !double.IsFinite(point.Point.X) || !double.IsFinite(point.Point.Y)) continue;
            if (hasSize && point.SizeValue is { } size && double.IsFinite(size) && size >= 0) sizeMaximum = Math.Max(sizeMaximum, size);
            if (hasColor && NumericColor(point.ColorValue, out var color))
            { colorMinimum = Math.Min(colorMinimum, color); colorMaximum = Math.Max(colorMaximum, color); }
        }
        sizeMaximum = options.SizeMaximum ?? sizeMaximum;
        if ((options.SizeMinimum.HasValue || options.SizeMaximum.HasValue) && sizeMaximum <= sizeMinimum)
            throw new InvalidOperationException("The resolved SizeMaximum must be greater than SizeMinimum.");
        colorMinimum = options.ColorMinimum ?? (double.IsFinite(colorMinimum) ? colorMinimum : 0);
        colorMaximum = options.ColorMaximum ?? (double.IsFinite(colorMaximum) ? colorMaximum : colorMinimum);
        if ((options.ColorMinimum.HasValue || options.ColorMaximum.HasValue) && colorMaximum <= colorMinimum)
            throw new InvalidOperationException("The resolved ColorMaximum must be greater than ColorMinimum.");
        var fill = series.MarkerFill ?? series.Fill ?? fallback;
        var result = new ChartPointStyle[data.Count];
        // Colors have byte channels, so many distinct numeric values share a brush.
        // This scene-local cache is bounded and holds no application items or source arrays.
        var colors = new Dictionary<Color, IBrush>();
        for (var ordinal = 0; ordinal < result.Length; ordinal++)
        {
            var point = data.Points[ordinal];
            var diameter = double.IsFinite(series.MarkerSize) && series.MarkerSize > 0 ? Math.Min(1024, series.MarkerSize) : 0;
            if (hasSize)
            {
                diameter = 0;
                if (point.SizeValue is { } size && double.IsFinite(size) && size >= 0)
                {
                    var fraction = Unit(size, sizeMinimum, sizeMaximum);
                    diameter = options.SizeScale == ChartSizeScale.Diameter
                        ? options.MinimumSize * (1 - fraction) + options.MaximumSize * fraction
                        : Math.Sqrt(options.MinimumSize * options.MinimumSize * (1 - fraction) + options.MaximumSize * options.MaximumSize * fraction);
                }
            }
            var pointFill = fill;
            if (hasColor)
            {
                if (point.ColorValue is IBrush directBrush) pointFill = directBrush;
                else if (point.ColorValue is Color directColor) pointFill = Brush(directColor);
                else if (point.ColorValue is string text && Color.TryParse(text, out var parsed)) pointFill = Brush(parsed);
                else if (NumericColor(point.ColorValue, out var color))
                    pointFill = Brush(Interpolate(options.MinimumColor, options.MaximumColor, Unit(color, colorMinimum, colorMaximum)));
            }
            result[ordinal] = new(diameter, pointFill);
        }
        return result;

        IBrush Brush(Color color)
        {
            if (colors.TryGetValue(color, out var brush)) return brush;
            brush = new ImmutableSolidColorBrush(color);
            if (colors.Count < 4096) colors.Add(color, brush);
            return brush;
        }
    }

    internal static bool NumericColor(object? value, out double number)
    {
        if (value is not null && value is not Color && value is not IBrush && ChartDataAccessor.TryNumber(value, out number) && double.IsFinite(number)) return true;
        number = default; return false;
    }

    private static double Unit(double value, double minimum, double maximum)
    {
        if (value <= minimum || maximum <= minimum) return 0;
        if (value >= maximum) return 1;
        var range = maximum - minimum;
        // Half-scaled subtraction also handles a color domain spanning -MaxValue..MaxValue;
        // ordinary subtraction retains subnormal, nonnegative size domains.
        return Math.Clamp(double.IsFinite(range) ? (value - minimum) / range :
            (value / 2 - minimum / 2) / (maximum / 2 - minimum / 2), 0, 1);
    }

    private static Color Interpolate(Color minimum, Color maximum, double fraction) => Color.FromArgb(
        Channel(minimum.A, maximum.A, fraction), Channel(minimum.R, maximum.R, fraction), Channel(minimum.G, maximum.G, fraction), Channel(minimum.B, maximum.B, fraction));

    private static byte Channel(byte minimum, byte maximum, double fraction) => (byte)Math.Clamp(Math.Round(minimum * (1 - fraction) + maximum * fraction,
        MidpointRounding.AwayFromZero), 0, 255);
}
