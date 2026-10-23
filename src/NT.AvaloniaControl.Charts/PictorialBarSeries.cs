using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>How a pictorial bar places its silhouette between its baseline and value.</summary>
public enum PictorialBarRenderMode { Stretch, Clip, Repeat }

/// <summary>A Cartesian category bar painted with a reusable geometry silhouette.</summary>
/// <remarks>Stretch fits one silhouette to the numeric bar. Clip places one SymbolSize-DIP silhouette
/// at the baseline. Repeat places equally sized silhouettes from the baseline, including a clipped
/// final silhouette. All modes clip to the numeric bar and the plot; zero values have no body.
/// Geometry bounds, including its transform, are normalized without modifying the supplied geometry.
/// Marker properties are not used; normal Cartesian labels, selection and source identities apply.</remarks>
public sealed class PictorialBarSeries : ComparisonSeries
{
    public static readonly StyledProperty<Geometry?> GeometryProperty =
        AvaloniaProperty.Register<PictorialBarSeries, Geometry?>(nameof(Geometry));
    public static readonly StyledProperty<PictorialBarRenderMode> RenderModeProperty =
        AvaloniaProperty.Register<PictorialBarSeries, PictorialBarRenderMode>(nameof(RenderMode), validate: Enum.IsDefined);
    public static readonly StyledProperty<double> SymbolSizeProperty =
        AvaloniaProperty.Register<PictorialBarSeries, double>(nameof(SymbolSize), 20,
            validate: value => double.IsFinite(value) && value >= 1 && value <= 4096);
    public static readonly StyledProperty<double> SymbolSpacingProperty =
        AvaloniaProperty.Register<PictorialBarSeries, double>(nameof(SymbolSpacing), 4,
            validate: value => double.IsFinite(value) && value >= 0 && value <= 4096);

    static PictorialBarSeries() => ShowMarkersProperty.OverrideDefaultValue<PictorialBarSeries>(false);

    /// <summary>Filled silhouette. Null uses a unit rectangle. Empty or nonfinite bounds paint no body.
    /// Replace Geometry or call Refresh after changing a supplied mutable geometry in place.</summary>
    public Geometry? Geometry { get => GetValue(GeometryProperty); set => SetValue(GeometryProperty, value); }
    public PictorialBarRenderMode RenderMode { get => GetValue(RenderModeProperty); set => SetValue(RenderModeProperty, value); }
    /// <summary>Symbol extent along the value axis in DIPs for Clip and Repeat; independent of zoom.</summary>
    public double SymbolSize { get => GetValue(SymbolSizeProperty); set => SetValue(SymbolSizeProperty, value); }
    /// <summary>Unpainted DIP gap between repeated symbols. The final partial symbol is clipped.</summary>
    public double SymbolSpacing { get => GetValue(SymbolSpacingProperty); set => SetValue(SymbolSpacingProperty, value); }
    public override IChartSeriesRenderer Renderer => PictorialBarRenderer.Instance;
}
