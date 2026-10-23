using System.Collections;
using Avalonia;

namespace NT.AvaloniaControl.Charts;

public abstract partial class ChartSeries
{
    private bool IsStatisticalMappingProperty(AvaloniaProperty property) =>
        property == BoxPlotSeries.SamplesPathProperty || property == BoxPlotSeries.SummaryPathProperty ||
        property == BoxPlotSeries.WhiskerMultiplierProperty || property == ErrorBarSeries.ErrorPathProperty ||
        property == ErrorBarSeries.LowerErrorPathProperty || property == ErrorBarSeries.UpperErrorPathProperty ||
        property == ErrorBarSeries.IntervalPathProperty || property == ErrorBarSeries.ErrorModeProperty || property == HistogramSeries.LowerBoundPathProperty ||
        property == HistogramSeries.UpperBoundPathProperty;

    private ChartDataPoint MapStatistics(ChartDataPoint point, List<ChartDataIssue> issues)
    {
        if (this is DensitySeries)
        {
            if (!point.IsEmpty && point.Point.Y >= 0) return point;
            if (!point.IsEmpty) ReportIssue(new(point.SourceIndex, ValuePath, "Density values must be finite and nonnegative."), issues);
            return point with { IsEmpty = true, WasEmpty = true, Point = point.Point with { Y = double.NaN } };
        }
        if (this is not StatisticalSeries) return point;
        var item = point.Item; var index = point.SourceIndex;
        try
        {
            if (this is BoxPlotSeries box)
            {
                ChartBoxPlotSummary summary;
                if (!string.IsNullOrWhiteSpace(box.SummaryPath))
                    summary = Read(item, box.SummaryPath, index, issues) as ChartBoxPlotSummary
                        ?? throw new ArgumentException("SummaryPath must produce ChartBoxPlotSummary.");
                else if (item is ChartBoxPlotSummary direct) summary = direct;
                else
                {
                    var samples = string.IsNullOrWhiteSpace(box.SamplesPath) ? item : Read(item, box.SamplesPath, index, issues);
                    if (samples is not IEnumerable source || samples is string) throw new ArgumentException("SamplesPath must produce a numeric sequence.");
                    summary = ChartStatistics.BoxPlot(Numbers(source), box.WhiskerMultiplier);
                }
                var valid = double.IsFinite(point.Point.X);
                return point with { BoxPlot = summary, Point = point.Point with { Y = summary.Median },
                    High = summary.Maximum, Low = summary.Minimum, Value = summary, IsEmpty = !valid, WasEmpty = !valid };
            }
            if (this is ErrorBarSeries error)
            {
                ChartErrorInterval interval;
                if (!string.IsNullOrWhiteSpace(error.IntervalPath))
                {
                    if (Read(item, error.IntervalPath, index, issues) is not ChartErrorInterval mapped)
                        throw new ArgumentException("IntervalPath must produce ChartErrorInterval.");
                    interval = mapped;
                }
                else if (item is ChartErrorInterval direct) interval = direct;
                else
                {
                    var common = Optional(item, error.ErrorPath, index, issues);
                    var lower = string.IsNullOrWhiteSpace(error.LowerErrorPath) ? common : Optional(item, error.LowerErrorPath, index, issues);
                    var upper = string.IsNullOrWhiteSpace(error.UpperErrorPath) ? common : Optional(item, error.UpperErrorPath, index, issues);
                    if (error.ErrorMode == ChartErrorMode.Plus) lower = 0;
                    if (error.ErrorMode == ChartErrorMode.Minus) upper = 0;
                    if (lower is null || upper is null) throw new ArgumentException("Error bars require both nonnegative error distances.");
                    interval = ChartStatistics.ErrorInterval(point.Point.Y, lower.Value, upper.Value);
                }
                // Revalidate even a default/custom interval before publishing bounds.
                interval = ChartStatistics.ErrorInterval(interval.Center, interval.LowerError, interval.UpperError);
                var valid = double.IsFinite(point.Point.X);
                return point with { Point = point.Point with { Y = interval.Center }, High = error.ErrorMode == ChartErrorMode.Minus ? interval.Center : interval.Upper,
                    Low = error.ErrorMode == ChartErrorMode.Plus ? interval.Center : interval.Lower, Value = interval, IsEmpty = !valid, WasEmpty = !valid };
            }
            if (this is HistogramSeries histogram)
            {
                double lower, upper, value;
                if (item is ChartHistogramBin bin && string.IsNullOrWhiteSpace(histogram.LowerBoundPath) && string.IsNullOrWhiteSpace(histogram.UpperBoundPath))
                    (lower, upper, value) = (bin.Lower, bin.Upper, string.IsNullOrWhiteSpace(ValuePath) ? bin.Value : point.Point.Y);
                else
                {
                    lower = Optional(item, histogram.LowerBoundPath, index, issues) ?? double.NaN;
                    upper = Optional(item, histogram.UpperBoundPath, index, issues) ?? double.NaN;
                    value = point.Point.Y;
                }
                if (!double.IsFinite(lower) || !double.IsFinite(upper) || lower >= upper || !double.IsFinite(value) || value < 0)
                    throw new ArgumentException("Histogram bins require finite increasing boundaries and nonnegative values.");
                return point with { Point = new(lower * .5 + upper * .5, value), High = upper, Low = lower,
                    IsEmpty = false, WasEmpty = false, Value = value };
            }
        }
        catch (Exception error) when (error is ArgumentException or OverflowException)
        {
            ReportIssue(new(index, this is BoxPlotSeries b ? b.SamplesPath ?? b.SummaryPath : ValuePath, error.Message), issues);
            return point with { Point = point.Point with { Y = double.NaN }, IsEmpty = true, WasEmpty = true };
        }
        return point;
    }

    private static IEnumerable<double> Numbers(IEnumerable source)
    {
        foreach (var sample in source)
            yield return ChartDataAccessor.TryNumber(sample, out var value) ? value : double.NaN;
    }
}
