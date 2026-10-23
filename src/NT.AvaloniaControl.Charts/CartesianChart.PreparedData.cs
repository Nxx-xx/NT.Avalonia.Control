using Avalonia;

namespace NT.AvaloniaControl.Charts;

public sealed partial class CartesianChart
{
    // These entries contain no screen coordinates, view clips or animated paths.
    // A layout/viewport revision creates small view wrappers around these same
    // immutable snapshots rather than remapping/copying every business row.
    private sealed record PreparedChartData(ChartSeries[] AllSeries, SeriesSnapshot[] Snapshots,
        Dictionary<ChartAxis, IReadOnlyList<string>> Categories);

    private PreparedChartData? _preparedData;
    private readonly Dictionary<(ChartAxis Axis, bool Horizontal), ChartScale> _baseScaleCache = [];
    private long _preparedDataBuildCount;
    private long _viewBuildCount;
    private long _baseScaleBuildCount;
    private long _dataPreparationRevision;

    private PreparedChartData GetPreparedData()
    {
        if (_attached && _preparedData is { } cached) return cached;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var revision = _dataPreparationRevision;
            var result = BuildPreparedData();
            // Mapping/error/encoding callbacks may change chart configuration.
            // A retired result must not become the next view's reusable data.
            if (revision != _dataPreparationRevision) continue;
            if (_attached && result.Snapshots.All(item => IsBuiltInAxis(item.XAxis) && IsBuiltInAxis(item.YAxis)))
                _preparedData = result;
            return result;
        }
        throw new ChartDataMappingException("Chart configuration changed repeatedly while preparing data; retry after the update.");
    }

    private PreparedChartData BuildPreparedData()
    {
        var allSeries = ActiveSeries.Where(series => series is not null).ToArray();
        // Palette identities are assigned before visibility filtering.
        foreach (var series in allSeries) GetSeriesBrush(series);
        var visibleSeries = allSeries.Where(AnalyticalVisibility).ToArray();
        var inputs = CreateAnalyticalInputs(visibleSeries);
        var mappedInputs = inputs.Where(input => input.Series is not ChartAnalyticalSeries { SourceSeries: not null }).ToArray();
        var categories = new Dictionary<ChartAxis, IReadOnlyList<string>>();
        foreach (var snapshot in inputs.Where(item => item.Series is FinancialSeries))
        {
            if (snapshot.XAxis is DateTimeAxis)
                throw new InvalidOperationException("Financial series use numeric trading slots. Use FinancialChart for a shared trading-date calendar; DateTimeAxis retains natural elapsed time.");
            if (snapshot.YAxis is CategoryAxis or DateTimeAxis)
                throw new InvalidOperationException("Financial prices and volumes require a numeric value axis.");
        }
        PrepareComparisonSources(mappedInputs);
        PreparePointSources(mappedInputs);
        PrepareSortedCategories(mappedInputs, categories);
        MapCategories(mappedInputs, categories, true);
        MapCategories(mappedInputs, categories, false);
        CalculateAnalyticalInputs(inputs);
        var bySeries = inputs.ToDictionary(input => input.Series);
        var snapshots = visibleSeries.Select((series, index) =>
        {
            var input = bySeries[series];
            return new SeriesSnapshot(series, input.Data, input.Brush, index, input.XAxis, input.YAxis) { SecondaryAxis = input.SecondaryAxis };
        }).ToArray();
        PrepareLineAreaData(snapshots);
        PrepareComparisonData(snapshots);
        FilterAxisDomains(snapshots);
        PreparePointData(snapshots);
        _preparedDataBuildCount++;
        return new PreparedChartData(allSeries, snapshots, categories);
    }

    private ChartScale GetBaseScale(ChartAxis axis, SeriesSnapshot[] snapshots, bool horizontal,
        IReadOnlyList<string>? categories)
    {
        var key = (axis, horizontal);
        if (_attached && _baseScaleCache.TryGetValue(key, out var cached)) return cached;
        var members = snapshots.Where(item => ReferenceEquals(horizontal ? item.XAxis : item.YAxis, axis) ||
            HorizontalComparison(item.Series) == horizontal && ReferenceEquals(item.SecondaryAxis, axis)).ToArray();
        var scale = BuildScale(axis, members, horizontal, categories);
        _baseScaleBuildCount++;
        // Extensions can compute bounds from external state. Keep their contract
        // live rather than imposing an undocumented renderer revision protocol.
        if (_attached && scale.HasStandardTransform && members.All(item => item.Series.Renderer is CartesianSeriesRenderer or ChartLineSeriesRenderer or
                ChartComparisonSeriesRenderer or ChartPointSeriesRenderer or PictorialBarRenderer or ChartStatisticalSeriesRenderer or ChartFinancialSeriesRenderer))
            _baseScaleCache[key] = scale;
        return scale;
    }

    private static bool AxisAffectsPreparedData(ChartAxis axis, EventArgs args)
    {
        // An extension may implement arbitrary domain predicates. Its own change
        // notifications always rebuild domain-filtered coordinates conservatively.
        if (!IsBuiltInAxis(axis))
            return true;
        if (args is not ChartAxisChangedEventArgs { Property: { } property }) return true;
        return property == ChartAxis.KeyProperty || property == ChartAxis.LabelsProperty ||
            property == ChartAxis.ScaleBreaksSourceProperty || property == LogarithmicAxis.LogBaseProperty ||
            property == ChartScaleBreak.StartProperty || property == ChartScaleBreak.EndProperty ||
            property == ChartScaleBreak.CompressionRatioProperty;
    }

    private static bool IsBuiltInAxis(ChartAxis axis) => axis.GetType() == typeof(ChartAxis) ||
        axis is NumericAxis or CategoryAxis or DateTimeAxis or LogarithmicAxis;

    private static bool SeriesAffectsPreparedData(ChartSeries series, EventArgs args)
    {
        if (series.Renderer is not (CartesianSeriesRenderer or ChartLineSeriesRenderer or
            ChartComparisonSeriesRenderer or ChartPointSeriesRenderer or PictorialBarRenderer or ChartStatisticalSeriesRenderer or ChartFinancialSeriesRenderer)) return true;
        if (args is not AvaloniaPropertyChangedEventArgs change) return true;
        var property = change.Property;
        if (property == ChartSeries.TitleProperty || property == ChartSeries.ShowInLegendProperty ||
            property == LineSeries.SamplingModeProperty || property == LineSeries.SamplingPixelWidthProperty ||
            property == ChartSeries.StrokeThicknessProperty || property == ChartSeries.ShowMarkersProperty ||
            property == ChartSeries.MarkerShapeProperty || property == ChartSeries.MarkerStrokeProperty ||
            property == ChartSeries.MarkerStrokeThicknessProperty || property == ChartSeries.ShowDataLabelsProperty ||
            property == ChartSeries.MaxDataLabelsProperty || property.Name.StartsWith("DataLabel", StringComparison.Ordinal))
            return false;
        if (!EncodedPoints(series) && (property == ChartSeries.StrokeProperty || property == ChartSeries.FillProperty ||
            property == ChartSeries.MarkerFillProperty || property == ChartSeries.MarkerSizeProperty)) return false;
        // Font inheritance also raises ChartSeries.Changed. Text/layout changes
        // invalidate the view, while category/stack/style encoding stays reusable.
        return change.Property != FontSizeProperty && change.Property != FontFamilyProperty &&
            change.Property != FontStyleProperty && change.Property != FontWeightProperty &&
            change.Property != ForegroundProperty;
    }
}
