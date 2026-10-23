using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>Age or population category pairs sharing a symmetric, common magnitude scale.</summary>
public sealed class PopulationPyramidChart : PairedBarChartBase
{
    public static readonly StyledProperty<string?> AgeLabelPathProperty = LabelPathProperty;
    public static readonly StyledProperty<string?> MaleValuePathProperty = LeftValuePathProperty;
    public static readonly StyledProperty<string?> FemaleValuePathProperty = RightValuePathProperty;
    public static readonly StyledProperty<IBrush?> MaleBrushProperty = LeftBrushProperty;
    public static readonly StyledProperty<IBrush?> FemaleBrushProperty = RightBrushProperty;
    public static readonly StyledProperty<double> BarGapProperty = AvaloniaProperty.Register<PopulationPyramidChart, double>(nameof(BarGap), 2, validate: x => double.IsFinite(x) && x >= 0 && x <= 4096);
    public PopulationPyramidChart() { LeftTitle = "First population"; RightTitle = "Second population"; }
    public string? AgeLabelPath { get => LabelPath; set => LabelPath = value; }
    public string? MaleValuePath { get => LeftValuePath; set => LeftValuePath = value; }
    public string? FemaleValuePath { get => RightValuePath; set => RightValuePath = value; }
    public IBrush? MaleBrush { get => LeftBrush; set => LeftBrush = value; }
    public IBrush? FemaleBrush { get => RightBrush; set => RightBrush = value; }
    /// <summary>Additional empty height between adjacent age-category bars, in unzoomed DIPs.</summary>
    public double BarGap { get => GetValue(BarGapProperty); set => SetValue(BarGapProperty, value); }
    protected override double CategoryGap => BarGap;
}
