using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.LogicalTree;

namespace NT.AvaloniaControl.Charts;

public sealed partial class CartesianChart
{
    /// <summary>Independent indicator objects using SourceSeries or their own ItemsSource.</summary>
    public ObservableCollection<ChartTechnicalIndicator> TechnicalIndicators { get; } = [];

    private IEnumerable<ChartSeries> EnumerateAnalyticalSeries()
    {
        var visited = new HashSet<ChartSeries>(ReferenceEqualityComparer.Instance);
        foreach (var series in (SeriesSource ?? Series).Concat(TechnicalIndicators))
            if (series is not null)
                foreach (var member in Expand(series)) yield return member;

        IEnumerable<ChartSeries> Expand(ChartSeries series)
        {
            if (!visited.Add(series)) yield break;
            // The fill is recorded beneath its three independent lines.
            if (series is BollingerBandsIndicator bollinger)
            {
                if (visited.Add(bollinger.BandSeries)) yield return bollinger.BandSeries;
                yield return series;
                if (visited.Add(bollinger.UpperSeries)) yield return bollinger.UpperSeries;
                if (visited.Add(bollinger.LowerSeries)) yield return bollinger.LowerSeries;
            }
            else yield return series;
            foreach (var trendline in series.ExistingTrendlines)
                foreach (var member in Expand(trendline)) yield return member;
        }
    }

    private static bool AnalyticalVisibility(ChartSeries series) => series.EffectiveVisibility;

    private static HashSet<ChartSeries> AnalyticalSourceDependencies(IEnumerable<ChartSeries> series)
    {
        var dependencies = new HashSet<ChartSeries>(ReferenceEqualityComparer.Instance);
        foreach (var root in series) Add(root);
        void Add(ChartSeries source)
        {
            if (!dependencies.Add(source)) return;
            if (source is ChartAnalyticalSeries { SourceSeries: { } parent }) Add(parent);
        }
        return dependencies;
    }

    private void SynchronizeAnalyticalOwnership(HashSet<ChartSeries> allSeries, HashSet<ChartAxis> allAxes)
    {
        // The first inherited DataContext can itself resolve a bound SourceSeries.
        // Settle that dependency before starting observers or preparing its data.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var children = AnalyticalSourceDependencies(allSeries).Cast<StyledElement>().Concat(allAxes).ToHashSet();
            foreach (var old in _ownedChildren.Except(children).ToArray())
            {
                if (!_attached && old is ChartSeries retiredSeries) CartesianSeriesRenderer.ReleaseBarGeometry(retiredSeries);
                _ownedChildren.Remove(old); LogicalChildren.Remove(old);
            }
            foreach (var item in children.Except(_ownedChildren).ToArray())
            {
                if (((ILogical)item).LogicalParent is { } parent && !ReferenceEquals(parent, this))
                    throw new InvalidOperationException("A chart series or axis can belong to only one chart. Create a separate instance for each chart.");
                _ownedChildren.Add(item);
                try { LogicalChildren.Add(item); }
                catch { _ownedChildren.Remove(item); throw; }
            }
            if (children.SetEquals(AnalyticalSourceDependencies(allSeries).Cast<StyledElement>().Concat(allAxes))) return;
        }
        throw new ChartDataMappingException("Analytical source bindings changed repeatedly while establishing chart inheritance; retry after the update.");
    }

    private SeriesSnapshot[] CreateAnalyticalInputs(ChartSeries[] visibleSeries)
    {
        var inputs = new List<ChartSeries>();
        var visiting = new HashSet<ChartSeries>(ReferenceEqualityComparer.Instance);
        var added = new HashSet<ChartSeries>(ReferenceEqualityComparer.Instance);
        foreach (var series in visibleSeries) Add(series);
        void Add(ChartSeries series)
        {
            if (added.Contains(series)) return;
            if (!visiting.Add(series)) throw new InvalidOperationException("An analytical series contains a cyclic SourceSeries dependency.");
            if (series is ChartAnalyticalSeries { SourceSeries: { } source }) Add(source);
            visiting.Remove(series);
            if (added.Add(series)) inputs.Add(series);
        }
        return inputs.Select((series, index) => new SeriesSnapshot(series,
            series is ChartAnalyticalSeries analytical ? analytical.SourceSeries is null ? analytical.CreateInputSnapshot() : ChartDataSnapshot.FromOwned(series.SourceVersion, []) : series.CreateSnapshot(),
            GetSeriesBrush(series), index,
            ResolveAxis(series is ChartAnalyticalSeries x ? x.EffectiveXAxisKey : series.XAxisKey, XAxis),
            ResolveAxis(series is ChartAnalyticalSeries y ? y.EffectiveYAxisKey : series.YAxisKey, YAxis))).ToArray();
    }

    private static void CalculateAnalyticalInputs(SeriesSnapshot[] inputs)
    {
        var bySeries = inputs.ToDictionary(input => input.Series);
        // CreateAnalyticalInputs orders dependencies before their consumers.
        foreach (var input in inputs)
            if (input.Series is ChartAnalyticalSeries analytical)
            {
                if (input.YAxis is CategoryAxis or DateTimeAxis)
                    throw new InvalidOperationException("Analytical series require a numeric value axis.");
                input.Data = analytical.ComputeSnapshot(analytical.SourceSeries is { } source ? bySeries[source].Data : input.Data);
            }
    }
}
