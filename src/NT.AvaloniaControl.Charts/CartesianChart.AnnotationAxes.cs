namespace NT.AvaloniaControl.Charts;

public sealed partial class CartesianChart
{
    private ChartAxis? UniqueAnnotationAxis(string? key, ChartAxis primary)
    {
        if (string.IsNullOrWhiteSpace(key)) return primary;
        var matches = Axes.Append(XAxis).Append(YAxis).Distinct().Where(axis => axis.Key == key).Take(2).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    // Series declarations establish a dimension even while that series is hidden. An
    // annotation can activate an otherwise unused axis, but cannot swap an existing axis.
    private (ChartAxis[] X, ChartAxis[] Y) AnnotationSceneAxes(SeriesSnapshot[] snapshots, ChartSeries[] series)
    {
        var secondaryX = snapshots.Where(item => HorizontalComparison(item.Series)).Select(item => item.SecondaryAxis).OfType<ChartAxis>();
        var secondaryY = snapshots.Where(item => !HorizontalComparison(item.Series)).Select(item => item.SecondaryAxis).OfType<ChartAxis>();
        var x = snapshots.Select(item => item.XAxis).Concat(secondaryX).Append(XAxis).Distinct().ToList();
        var y = snapshots.Select(item => item.YAxis).Concat(secondaryY).Append(YAxis).Distinct().ToList();
        if (_activeAnnotations.Length == 0) return (x.ToArray(), y.ToArray());
        var knownX = series.Select(item => UniqueAnnotationAxis(item.XAxisKey, XAxis)).OfType<ChartAxis>().Concat(x).ToHashSet();
        var knownY = series.Select(item => UniqueAnnotationAxis(item.YAxisKey, YAxis)).OfType<ChartAxis>().Concat(y).ToHashSet();
        foreach (var pareto in series.OfType<ParetoSeries>().Where(item => !string.IsNullOrWhiteSpace(item.CumulativeAxisKey)))
        {
            var horizontal = HorizontalComparison(pareto);
            if (UniqueAnnotationAxis(pareto.CumulativeAxisKey, horizontal ? XAxis : YAxis) is { } axis)
                (horizontal ? knownX : knownY).Add(axis);
        }
        var requests = new List<(ChartAxis X, ChartAxis Y)>();
        foreach (var annotation in _activeAnnotations)
        {
            if (!annotation.IsVisible || annotation.CoordinateUnit != ChartAnnotationCoordinateUnit.Data) continue;
            var horizontal = UniqueAnnotationAxis(annotation.XAxisKey, XAxis);
            var vertical = UniqueAnnotationAxis(annotation.YAxisKey, YAxis);
            if (horizontal is null || vertical is null || ReferenceEquals(horizontal, vertical) ||
                knownY.Contains(horizontal) || knownX.Contains(vertical)) continue;
            requests.Add((horizontal, vertical));
        }
        var requestedX = requests.Select(item => item.X).ToHashSet();
        var requestedY = requests.Select(item => item.Y).ToHashSet();
        foreach (var request in requests)
        {
            // Conflicting annotation-only dimension declarations are skipped together.
            if (requestedY.Contains(request.X) || requestedX.Contains(request.Y)) continue;
            if (!x.Contains(request.X)) x.Add(request.X);
            if (!y.Contains(request.Y)) y.Add(request.Y);
        }
        return (x.ToArray(), y.ToArray());
    }

    private ChartAxis? AnnotationAxis(string? key, ChartAxis primary, ChartAxis[] additional)
    {
        var axis = UniqueAnnotationAxis(key, primary);
        return axis is not null && (ReferenceEquals(axis, primary) || additional.Contains(axis)) ? axis : null;
    }
}
