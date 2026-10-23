using Avalonia;

namespace NT.AvaloniaControl.Charts;

/// <summary>Directed chord ribbons with separate source/target angular reservations and exact original edge identity.</summary>
public sealed class ChordDiagramChart : RelationsChartBase
{
    public static readonly StyledProperty<double> PaddingDegreesProperty = AvaloniaProperty.Register<ChordDiagramChart, double>(nameof(PaddingDegrees), 3, validate: x => double.IsFinite(x) && x >= 0 && x < 180);
    public static readonly StyledProperty<double> InnerRadiusProperty = AvaloniaProperty.Register<ChordDiagramChart, double>(nameof(InnerRadius), .82, validate: x => double.IsFinite(x) && x > 0 && x < 1);
    public double PaddingDegrees { get => GetValue(PaddingDegreesProperty); set => SetValue(PaddingDegreesProperty, value); }
    public double InnerRadius { get => GetValue(InnerRadiusProperty); set => SetValue(InnerRadiusProperty, value); }
    protected override object CalculateLayout(GraphSnapshot snapshot, CancellationToken cancellationToken) => ChartRelations.Chord(snapshot, PaddingDegrees, cancellationToken);
    protected override Func<CancellationToken, object> CreateLayoutWorker(GraphSnapshot snapshot)
    { var padding = PaddingDegrees; return token => ChartRelations.Chord(snapshot, padding, token); }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    { if (change.Property == PaddingDegreesProperty) InvalidateRelationLayout(); base.OnPropertyChanged(change); }
}
