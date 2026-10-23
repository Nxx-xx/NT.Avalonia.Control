using Avalonia;
using Avalonia.Controls.Templates;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

public enum ChartTrackballMode { None = 0, Vertical = 1, Horizontal = 2 }
public enum ChartTrackballDisplayMode { FloatAllPoints = 0, GroupAllPoints = 1 }
public enum ChartTrackballSnapMode { NearestPoint = 0, LinearInterpolation = 1 }

/// <summary>
/// A location on one visible built-in line or area path. Start and End retain
/// original item identity. Point is a location on the rendered path inverse-mapped
/// through this series' axes; it is not an application item. Fraction is the cubic
/// parameter, or progression through the explicit step legs, before clipping.
/// Exact endpoints retain their displayed Point. Range bands report the upper edge.
/// </summary>
public sealed record ChartSegmentHit(ChartPointHit Start, ChartPointHit End, double Fraction,
    ChartPoint Point, Point Position, double DistanceSquared)
{
    public ChartSeries Series => Start.Series;
    public bool IsInterpolated => Fraction > 0 && Fraction < 1;
    public ChartPointHit Original => Fraction <= .5 ? Start : End;
    /// <summary>The inverse-projected lower path at this path parameter, for areas, ranges and stacks.</summary>
    public double? BaseValue { get; init; }
}

/// <summary>An original point and, optionally, a separately identified segment estimate.</summary>
public sealed record ChartTrackballReading(ChartPointHit Original, ChartSegmentHit? Segment = null)
{
    public ChartSeries Series => Original.Series;
    public ChartPoint Point => Segment?.Point ?? Original.Point;
    public Point Position => Segment?.Position ?? Original.Position;
    public bool IsInterpolated => Segment?.IsInterpolated ?? false;
    public double? BaseValue => Segment?.BaseValue ?? Original.BaseValue;
}

public sealed partial class CartesianChart
{
    public static readonly StyledProperty<ChartTrackballMode> TrackballModeProperty = AvaloniaProperty.Register<CartesianChart, ChartTrackballMode>(nameof(TrackballMode), validate: Enum.IsDefined);
    public static readonly StyledProperty<ChartTrackballDisplayMode> TrackballDisplayModeProperty = AvaloniaProperty.Register<CartesianChart, ChartTrackballDisplayMode>(nameof(TrackballDisplayMode), ChartTrackballDisplayMode.GroupAllPoints, validate: Enum.IsDefined);
    public static readonly StyledProperty<ChartTrackballSnapMode> TrackballSnapModeProperty = AvaloniaProperty.Register<CartesianChart, ChartTrackballSnapMode>(nameof(TrackballSnapMode), validate: Enum.IsDefined);
    public static readonly StyledProperty<bool> ShowTrackballLineProperty = AvaloniaProperty.Register<CartesianChart, bool>(nameof(ShowTrackballLine), true);
    public static readonly StyledProperty<IBrush?> TrackballLineStrokeProperty = AvaloniaProperty.Register<CartesianChart, IBrush?>(nameof(TrackballLineStroke));
    public static readonly StyledProperty<double> TrackballLineStrokeThicknessProperty = AvaloniaProperty.Register<CartesianChart, double>(nameof(TrackballLineStrokeThickness), 1, validate: value => double.IsFinite(value) && value >= 0);
    public static readonly StyledProperty<bool> ShowTrackballMarkersProperty = AvaloniaProperty.Register<CartesianChart, bool>(nameof(ShowTrackballMarkers), true);
    public static readonly StyledProperty<double> TrackballMarkerSizeProperty = AvaloniaProperty.Register<CartesianChart, double>(nameof(TrackballMarkerSize), 14, validate: value => double.IsFinite(value) && value >= 0);
    public static readonly StyledProperty<IBrush?> TrackballMarkerStrokeProperty = AvaloniaProperty.Register<CartesianChart, IBrush?>(nameof(TrackballMarkerStroke));
    public static readonly StyledProperty<IBrush?> TrackballMarkerFillProperty = AvaloniaProperty.Register<CartesianChart, IBrush?>(nameof(TrackballMarkerFill));
    public static readonly StyledProperty<double> TrackballMarkerStrokeThicknessProperty = AvaloniaProperty.Register<CartesianChart, double>(nameof(TrackballMarkerStrokeThickness), 2, validate: value => double.IsFinite(value) && value >= 0);
    public static readonly StyledProperty<IDataTemplate?> TrackballContentTemplateProperty = AvaloniaProperty.Register<CartesianChart, IDataTemplate?>(nameof(TrackballContentTemplate));
    public static readonly StyledProperty<Func<IReadOnlyList<ChartTrackballReading>, string?>?> TrackballFormatterProperty = AvaloniaProperty.Register<CartesianChart, Func<IReadOnlyList<ChartTrackballReading>, string?>?>(nameof(TrackballFormatter));
    public static readonly DirectProperty<CartesianChart, IReadOnlyList<ChartTrackballReading>> TrackballReadingsProperty = AvaloniaProperty.RegisterDirect<CartesianChart, IReadOnlyList<ChartTrackballReading>>(nameof(TrackballReadings), chart => chart.TrackballReadings);
    public static readonly DirectProperty<CartesianChart, IReadOnlyList<ChartSegmentHit>> HoveredSegmentsProperty = AvaloniaProperty.RegisterDirect<CartesianChart, IReadOnlyList<ChartSegmentHit>>(nameof(HoveredSegments), chart => chart.HoveredSegments);
    public static readonly DirectProperty<CartesianChart, ChartSegmentHit?> PrimarySegmentProperty = AvaloniaProperty.RegisterDirect<CartesianChart, ChartSegmentHit?>(nameof(PrimarySegment), chart => chart.PrimarySegment);

    private IReadOnlyList<ChartTrackballReading> _trackballReadings = Array.Empty<ChartTrackballReading>();
    private IReadOnlyList<ChartSegmentHit> _hoveredSegments = Array.Empty<ChartSegmentHit>();
    private ChartSegmentHit? _primarySegment;
    private Point _readingPosition;

    /// <summary>None preserves TooltipMode. Otherwise Trackball takes precedence and operates independently of IsTooltipEnabled.</summary>
    public ChartTrackballMode TrackballMode { get => GetValue(TrackballModeProperty); set => SetValue(TrackballModeProperty, value); }
    public ChartTrackballDisplayMode TrackballDisplayMode { get => GetValue(TrackballDisplayModeProperty); set => SetValue(TrackballDisplayModeProperty, value); }
    public ChartTrackballSnapMode TrackballSnapMode { get => GetValue(TrackballSnapModeProperty); set => SetValue(TrackballSnapModeProperty, value); }
    public bool ShowTrackballLine { get => GetValue(ShowTrackballLineProperty); set => SetValue(ShowTrackballLineProperty, value); }
    public IBrush? TrackballLineStroke { get => GetValue(TrackballLineStrokeProperty); set => SetValue(TrackballLineStrokeProperty, value); }
    public double TrackballLineStrokeThickness { get => GetValue(TrackballLineStrokeThicknessProperty); set => SetValue(TrackballLineStrokeThicknessProperty, value); }
    public bool ShowTrackballMarkers { get => GetValue(ShowTrackballMarkersProperty); set => SetValue(ShowTrackballMarkersProperty, value); }
    /// <summary>Trackball circle diameter in chart-local DIPs.</summary>
    public double TrackballMarkerSize { get => GetValue(TrackballMarkerSizeProperty); set => SetValue(TrackballMarkerSizeProperty, value); }
    public IBrush? TrackballMarkerStroke { get => GetValue(TrackballMarkerStrokeProperty); set => SetValue(TrackballMarkerStrokeProperty, value); }
    public IBrush? TrackballMarkerFill { get => GetValue(TrackballMarkerFillProperty); set => SetValue(TrackballMarkerFillProperty, value); }
    public double TrackballMarkerStrokeThickness { get => GetValue(TrackballMarkerStrokeThicknessProperty); set => SetValue(TrackballMarkerStrokeThicknessProperty, value); }
    /// <summary>Passive content with ChartTooltipContext. Group mode builds one card; Float mode one per reading. Null Build hides that card.</summary>
    public IDataTemplate? TrackballContentTemplate { get => GetValue(TrackballContentTemplateProperty); set => SetValue(TrackballContentTemplateProperty, value); }
    /// <summary>Formats one group/card. Null hides a default card, while a content template still receives the original readings.</summary>
    public Func<IReadOnlyList<ChartTrackballReading>, string?>? TrackballFormatter { get => GetValue(TrackballFormatterProperty); set => SetValue(TrackballFormatterProperty, value); }
    public IReadOnlyList<ChartTrackballReading> TrackballReadings => _trackballReadings ?? Array.Empty<ChartTrackballReading>();
    public IReadOnlyList<ChartSegmentHit> HoveredSegments => _hoveredSegments ?? Array.Empty<ChartSegmentHit>();
    public ChartSegmentHit? PrimarySegment => _primarySegment;
    private bool IsTrackballActive => TrackballMode != ChartTrackballMode.None;
    private bool ReadingsEnabled => IsTooltipEnabled || IsTrackballActive;
}
