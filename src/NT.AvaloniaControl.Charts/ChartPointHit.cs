namespace NT.AvaloniaControl.Charts;

/// <summary>A visible data point identified by its source index and chart-local screen position.</summary>
public sealed record ChartPointHit(ChartSeries Series, ChartPoint Point, int Index, Avalonia.Point Position)
{
    public object? Item { get; init; }
    public object? StableId { get; init; }
    /// <summary>Source coordinate before a range/stack display transformation, when applicable.</summary>
    public ChartPoint? RawPoint { get; init; }
    /// <summary>Displayed lower boundary, when applicable.</summary>
    public double? BaseValue { get; init; }
    /// <summary>Signed stack contribution (percentage points for a percent stack), when applicable.</summary>
    public double? StackValue { get; init; }
    public double? CumulativeValue { get; init; }
    public bool IsTotal { get; init; }
    public double? SizeValue { get; init; }
    public object? ColorValue { get; init; }
    public double? MarkerDiameter { get; init; }
    public Avalonia.Media.IBrush? PointFill { get; init; }
    /// <summary>Summary for the original box group, including nested outlier indices.</summary>
    public ChartBoxPlotSummary? BoxPlot { get; init; }
    /// <summary>Displayed financial values and original source range. Index is a glyph ordinal for non-time transforms.</summary>
    public ChartFinancialSample? Financial { get; init; }
    /// <summary>Whether this is a synthetic fitted sample or a derived value at an original endpoint.</summary>
    public ChartAnalysisSample? Analysis { get; init; }
    /// <summary>Original validated error interval, before one-sided display filtering.</summary>
    public ChartErrorInterval? ErrorInterval { get; init; }

    internal static ChartPointHit FromData(ChartSeries series, ChartDataPoint data, Avalonia.Point position) =>
        new(series, data.Point, data.SourceIndex, position)
        {
            Item = data.Item, StableId = data.StableId, RawPoint = data.RawPoint,
            BaseValue = data.BaseValue, StackValue = data.StackValue, CumulativeValue = data.CumulativeValue, IsTotal = data.IsTotal,
            SizeValue = data.SizeValue, ColorValue = data.ColorValue, MarkerDiameter = data.MarkerDiameter, PointFill = data.PointFill,
            BoxPlot = data.BoxPlot, Financial = data.Financial, Analysis = data.Analysis, ErrorInterval = data.Value is ChartErrorInterval interval ? interval : null,
        };
}
