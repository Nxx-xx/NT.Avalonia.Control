using Avalonia;

namespace NT.AvaloniaControl.Charts;

/// <summary>Automatic process and mind map layouts or explicit centered node coordinates, shapes, groups and directed connectors.</summary>
public sealed class FlowChart : RelationsChartBase
{
    public static readonly StyledProperty<RelationFlowMode> ModeProperty = AvaloniaProperty.Register<FlowChart, RelationFlowMode>(nameof(Mode), RelationFlowMode.Layered, validate: Enum.IsDefined);
    public static readonly StyledProperty<bool> ShowArrowsProperty = AvaloniaProperty.Register<FlowChart, bool>(nameof(ShowArrows), true);
    public RelationFlowMode Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }
    public bool ShowArrows { get => GetValue(ShowArrowsProperty); set => SetValue(ShowArrowsProperty, value); }
    protected override object CalculateLayout(GraphSnapshot snapshot, CancellationToken cancellationToken) => ChartRelations.Flow(snapshot, Mode, cancellationToken);
    protected override Func<CancellationToken, object> CreateLayoutWorker(GraphSnapshot snapshot)
    { var mode = Mode; return token => ChartRelations.Flow(snapshot, mode, token); }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    { if (change.Property == ModeProperty) InvalidateRelationLayout(); base.OnPropertyChanged(change); }
}
