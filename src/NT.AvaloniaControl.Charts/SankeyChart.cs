using Avalonia;

namespace NT.AvaloniaControl.Charts;

/// <summary>Weighted flow ribbons. Alluvial mode retains supplied stages; return edges and self loops use exterior lanes.</summary>
public sealed class SankeyChart : RelationsChartBase
{
    public static readonly StyledProperty<RelationSankeyMode> ModeProperty = AvaloniaProperty.Register<SankeyChart, RelationSankeyMode>(nameof(Mode), RelationSankeyMode.Sankey, validate: Enum.IsDefined);
    public static readonly StyledProperty<double> NodeWidthProperty = AvaloniaProperty.Register<SankeyChart, double>(nameof(NodeWidth), .045, validate: x => double.IsFinite(x) && x > 0 && x <= .4);
    public static readonly StyledProperty<double> NodeGapProperty = AvaloniaProperty.Register<SankeyChart, double>(nameof(NodeGap), .025, validate: x => double.IsFinite(x) && x >= 0 && x <= .4);
    public static readonly StyledProperty<bool> StrictConservationProperty = AvaloniaProperty.Register<SankeyChart, bool>(nameof(StrictConservation));
    public static readonly StyledProperty<int> CrossingIterationsProperty = AvaloniaProperty.Register<SankeyChart, int>(nameof(CrossingIterations), 6, validate: x => x >= 0 && x <= ChartRelations.MaximumCrossingIterations);
    public RelationSankeyMode Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }
    public double NodeWidth { get => GetValue(NodeWidthProperty); set => SetValue(NodeWidthProperty, value); }
    public double NodeGap { get => GetValue(NodeGapProperty); set => SetValue(NodeGapProperty, value); }
    public bool StrictConservation { get => GetValue(StrictConservationProperty); set => SetValue(StrictConservationProperty, value); }
    public int CrossingIterations { get => GetValue(CrossingIterationsProperty); set => SetValue(CrossingIterationsProperty, value); }
    protected override object CalculateLayout(GraphSnapshot snapshot, CancellationToken cancellationToken) => ChartRelations.Sankey(snapshot, NodeWidth, NodeGap, StrictConservation, CrossingIterations, Mode, cancellationToken);
    protected override Func<CancellationToken, object> CreateLayoutWorker(GraphSnapshot snapshot)
    { var width = NodeWidth; var gap = NodeGap; var strict = StrictConservation; var iterations = CrossingIterations; var mode = Mode; return token => ChartRelations.Sankey(snapshot, width, gap, strict, iterations, mode, token); }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    { if (change.Property == ModeProperty || change.Property == NodeWidthProperty || change.Property == NodeGapProperty || change.Property == StrictConservationProperty || change.Property == CrossingIterationsProperty) InvalidateRelationLayout(); base.OnPropertyChanged(change); }
}
