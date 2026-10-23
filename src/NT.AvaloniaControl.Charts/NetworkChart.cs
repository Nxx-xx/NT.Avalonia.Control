using Avalonia;

namespace NT.AvaloniaControl.Charts;

/// <summary>Original graph nodes and weighted parallel/loop edges with deterministic circular, force and arc layouts.</summary>
public sealed class NetworkChart : RelationsChartBase
{
    public static readonly StyledProperty<RelationNetworkMode> ModeProperty = AvaloniaProperty.Register<NetworkChart, RelationNetworkMode>(nameof(Mode), RelationNetworkMode.Circular, validate: Enum.IsDefined);
    public static readonly StyledProperty<int> IterationsProperty = AvaloniaProperty.Register<NetworkChart, int>(nameof(Iterations), 80, validate: x => x >= 0 && x <= ChartRelations.MaximumForceIterations);
    public static readonly StyledProperty<int> SeedProperty = AvaloniaProperty.Register<NetworkChart, int>(nameof(Seed), 1);
    public RelationNetworkMode Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }
    public int Iterations { get => GetValue(IterationsProperty); set => SetValue(IterationsProperty, value); }
    public int Seed { get => GetValue(SeedProperty); set => SetValue(SeedProperty, value); }
    protected override object CalculateLayout(GraphSnapshot snapshot, CancellationToken cancellationToken) => ChartRelations.Network(snapshot, Mode, Iterations, Seed, cancellationToken);
    protected override Func<CancellationToken, object> CreateLayoutWorker(GraphSnapshot snapshot)
    { var mode = Mode; var iterations = Iterations; var seed = Seed; return token => ChartRelations.Network(snapshot, mode, iterations, seed, token); }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    { if (change.Property == ModeProperty || change.Property == IterationsProperty || change.Property == SeedProperty) InvalidateRelationLayout(); base.OnPropertyChanged(change); }
}
