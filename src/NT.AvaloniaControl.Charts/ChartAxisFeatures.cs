using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.LogicalTree;

namespace NT.AvaloniaControl.Charts;

public enum ChartAxisLabelMode { Auto, Hide, Wrap, Stagger, Rotate, None }
public enum CategoryPlotMode { OnTicks, BetweenTicks }
public enum ChartPlotBandLayer { BehindSeries, AboveSeries }
public enum ChartBandTextAlignment { Start, Center, End }

/// <summary>An open raw-data interval omitted from data, retaining a compressed invertible screen band.</summary>
public sealed class ChartScaleBreak : StyledElement
{
    public static readonly StyledProperty<double> StartProperty = AvaloniaProperty.Register<ChartScaleBreak, double>(nameof(Start), validate: double.IsFinite);
    public static readonly StyledProperty<double> EndProperty = AvaloniaProperty.Register<ChartScaleBreak, double>(nameof(End), validate: double.IsFinite);
    public static readonly StyledProperty<double> CompressionRatioProperty = AvaloniaProperty.Register<ChartScaleBreak, double>(nameof(CompressionRatio), .05,
        validate: value => double.IsFinite(value) && value > 0 && value <= 1);
    public static readonly StyledProperty<IBrush?> StrokeProperty = AvaloniaProperty.Register<ChartScaleBreak, IBrush?>(nameof(Stroke));
    public static readonly StyledProperty<double> StrokeThicknessProperty = AvaloniaProperty.Register<ChartScaleBreak, double>(nameof(StrokeThickness), 1,
        validate: value => double.IsFinite(value) && value >= 0);
    public double Start { get => GetValue(StartProperty); set => SetValue(StartProperty, value); }
    public double End { get => GetValue(EndProperty); set => SetValue(EndProperty, value); }
    public double CompressionRatio { get => GetValue(CompressionRatioProperty); set => SetValue(CompressionRatioProperty, value); }
    public IBrush? Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public double StrokeThickness { get => GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
    internal long Revision { get; private set; }
    internal event EventHandler<ChartAxisChangedEventArgs>? Changed;
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var rangeChanged = change.Property == StartProperty || change.Property == EndProperty || change.Property == CompressionRatioProperty;
        if (!rangeChanged && change.Property != StrokeProperty && change.Property != StrokeThicknessProperty) return;
        if (rangeChanged) Revision++;
        Changed?.Invoke(this, new ChartAxisChangedEventArgs(rangeChanged, change.Property));
    }
}

/// <summary>A raw-data band. Repetition advances both ends by RepeatInterval in raw units, including Unix milliseconds.</summary>
public sealed class ChartPlotBand : StyledElement
{
    public static readonly StyledProperty<double> StartProperty = AvaloniaProperty.Register<ChartPlotBand, double>(nameof(Start), double.NaN,
        validate: value => double.IsFinite(value) || double.IsNaN(value));
    public static readonly StyledProperty<double> EndProperty = AvaloniaProperty.Register<ChartPlotBand, double>(nameof(End), double.NaN,
        validate: value => double.IsFinite(value) || double.IsNaN(value));
    public static readonly StyledProperty<double?> RepeatIntervalProperty = AvaloniaProperty.Register<ChartPlotBand, double?>(nameof(RepeatInterval),
        validate: value => value is null || double.IsFinite(value.Value) && value > 0);
    public static readonly StyledProperty<int> RepeatCountProperty = AvaloniaProperty.Register<ChartPlotBand, int>(nameof(RepeatCount), 1,
        validate: value => value >= 0 && value <= 10_000);
    public static readonly StyledProperty<double?> RepeatUntilProperty = AvaloniaProperty.Register<ChartPlotBand, double?>(nameof(RepeatUntil),
        validate: value => value is null || double.IsFinite(value.Value));
    public static readonly StyledProperty<bool> IsVisibleProperty = AvaloniaProperty.Register<ChartPlotBand, bool>(nameof(IsVisible), true);
    public static readonly StyledProperty<double> OpacityProperty = AvaloniaProperty.Register<ChartPlotBand, double>(nameof(Opacity), 1,
        validate: value => double.IsFinite(value) && value >= 0 && value <= 1);
    public static readonly StyledProperty<ChartBandTextAlignment> HorizontalTextAlignmentProperty =
        AvaloniaProperty.Register<ChartPlotBand, ChartBandTextAlignment>(nameof(HorizontalTextAlignment), ChartBandTextAlignment.Center, validate: Enum.IsDefined);
    public static readonly StyledProperty<ChartBandTextAlignment> VerticalTextAlignmentProperty =
        AvaloniaProperty.Register<ChartPlotBand, ChartBandTextAlignment>(nameof(VerticalTextAlignment), ChartBandTextAlignment.Center, validate: Enum.IsDefined);
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<ChartPlotBand, string?>(nameof(Text));
    public static readonly StyledProperty<IBrush?> FillProperty = AvaloniaProperty.Register<ChartPlotBand, IBrush?>(nameof(Fill));
    public static readonly StyledProperty<IBrush?> StrokeProperty = AvaloniaProperty.Register<ChartPlotBand, IBrush?>(nameof(Stroke));
    public static readonly StyledProperty<IBrush?> ForegroundProperty = AvaloniaProperty.Register<ChartPlotBand, IBrush?>(nameof(Foreground));
    public static readonly StyledProperty<double> StrokeThicknessProperty = AvaloniaProperty.Register<ChartPlotBand, double>(nameof(StrokeThickness), 1,
        validate: value => double.IsFinite(value) && value >= 0);
    public static readonly StyledProperty<double?> FontSizeProperty = AvaloniaProperty.Register<ChartPlotBand, double?>(nameof(FontSize),
        validate: value => value is null || double.IsFinite(value.Value) && value > 0);
    public static readonly StyledProperty<ChartPlotBandLayer> LayerProperty = AvaloniaProperty.Register<ChartPlotBand, ChartPlotBandLayer>(nameof(Layer),
        validate: Enum.IsDefined);
    /// <summary>Raw start; NaN extends to the visible axis minimum.</summary>
    public double Start { get => GetValue(StartProperty); set => SetValue(StartProperty, value); }
    /// <summary>Raw end; NaN extends to the visible axis maximum.</summary>
    public double End { get => GetValue(EndProperty); set => SetValue(EndProperty, value); }
    public double? RepeatInterval { get => GetValue(RepeatIntervalProperty); set => SetValue(RepeatIntervalProperty, value); }
    public int RepeatCount { get => GetValue(RepeatCountProperty); set => SetValue(RepeatCountProperty, value); }
    /// <summary>With a positive RepeatInterval, overrides RepeatCount and repeats while start is below this raw bound (at most 10,000 bands).</summary>
    public double? RepeatUntil { get => GetValue(RepeatUntilProperty); set => SetValue(RepeatUntilProperty, value); }
    public bool IsVisible { get => GetValue(IsVisibleProperty); set => SetValue(IsVisibleProperty, value); }
    public double Opacity { get => GetValue(OpacityProperty); set => SetValue(OpacityProperty, value); }
    public ChartBandTextAlignment HorizontalTextAlignment { get => GetValue(HorizontalTextAlignmentProperty); set => SetValue(HorizontalTextAlignmentProperty, value); }
    public ChartBandTextAlignment VerticalTextAlignment { get => GetValue(VerticalTextAlignmentProperty); set => SetValue(VerticalTextAlignmentProperty, value); }
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public IBrush? Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public double StrokeThickness { get => GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
    public double? FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public ChartPlotBandLayer Layer { get => GetValue(LayerProperty); set => SetValue(LayerProperty, value); }
    internal event EventHandler? Changed;
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    { base.OnPropertyChanged(change); Changed?.Invoke(this, EventArgs.Empty); }
}

internal readonly record struct ChartScaleBreakRange(double Start, double End, double CompressionRatio);

public partial class ChartAxis
{
    public static readonly StyledProperty<double?> MinorIntervalProperty = AvaloniaProperty.Register<ChartAxis, double?>(nameof(MinorInterval),
        validate: value => value is null || double.IsFinite(value.Value) && value > 0);
    public static readonly StyledProperty<int> MinorTickCountProperty = AvaloniaProperty.Register<ChartAxis, int>(nameof(MinorTickCount),
        validate: value => value >= 0 && value <= 100);
    public static readonly StyledProperty<bool> ShowAxisProperty = AvaloniaProperty.Register<ChartAxis, bool>(nameof(ShowAxis), true);
    public static readonly StyledProperty<bool> IsVisibleProperty = AvaloniaProperty.Register<ChartAxis, bool>(nameof(IsVisible), true);
    public static readonly StyledProperty<bool> ShowTitleProperty = AvaloniaProperty.Register<ChartAxis, bool>(nameof(ShowTitle), true);
    public static readonly StyledProperty<bool> ShowLabelsProperty = AvaloniaProperty.Register<ChartAxis, bool>(nameof(ShowLabels), true);
    public static readonly StyledProperty<bool> ShowTicksProperty = AvaloniaProperty.Register<ChartAxis, bool>(nameof(ShowTicks), true);
    public static readonly StyledProperty<bool> ShowGridLinesProperty = AvaloniaProperty.Register<ChartAxis, bool>(nameof(ShowGridLines), true);
    public static readonly StyledProperty<bool> ShowMinorGridLinesProperty = AvaloniaProperty.Register<ChartAxis, bool>(nameof(ShowMinorGridLines));
    public static readonly StyledProperty<IBrush?> TitleBrushProperty = AvaloniaProperty.Register<ChartAxis, IBrush?>(nameof(TitleBrush));
    public static readonly StyledProperty<IBrush?> LabelBrushProperty = AvaloniaProperty.Register<ChartAxis, IBrush?>(nameof(LabelBrush));
    public static readonly StyledProperty<IBrush?> AxisBrushProperty = AvaloniaProperty.Register<ChartAxis, IBrush?>(nameof(AxisBrush));
    public static readonly StyledProperty<IBrush?> GridLineBrushProperty = AvaloniaProperty.Register<ChartAxis, IBrush?>(nameof(GridLineBrush));
    public static readonly StyledProperty<IBrush?> MinorGridLineBrushProperty = AvaloniaProperty.Register<ChartAxis, IBrush?>(nameof(MinorGridLineBrush));
    public static readonly StyledProperty<IBrush?> TickBrushProperty = AvaloniaProperty.Register<ChartAxis, IBrush?>(nameof(TickBrush));
    public static readonly StyledProperty<IBrush?> MinorTickBrushProperty = AvaloniaProperty.Register<ChartAxis, IBrush?>(nameof(MinorTickBrush));
    public static readonly StyledProperty<IDashStyle?> AxisDashStyleProperty = AvaloniaProperty.Register<ChartAxis, IDashStyle?>(nameof(AxisDashStyle));
    public static readonly StyledProperty<IDashStyle?> GridLineDashStyleProperty = AvaloniaProperty.Register<ChartAxis, IDashStyle?>(nameof(GridLineDashStyle));
    public static readonly StyledProperty<IDashStyle?> MinorGridLineDashStyleProperty = AvaloniaProperty.Register<ChartAxis, IDashStyle?>(nameof(MinorGridLineDashStyle));
    public static readonly StyledProperty<PenLineCap> AxisLineCapProperty = AvaloniaProperty.Register<ChartAxis, PenLineCap>(nameof(AxisLineCap), PenLineCap.Flat, validate: Enum.IsDefined);
    public static readonly StyledProperty<PenLineCap> GridLineCapProperty = AvaloniaProperty.Register<ChartAxis, PenLineCap>(nameof(GridLineCap), PenLineCap.Flat, validate: Enum.IsDefined);
    public static readonly StyledProperty<PenLineCap> MinorGridLineCapProperty = AvaloniaProperty.Register<ChartAxis, PenLineCap>(nameof(MinorGridLineCap), PenLineCap.Flat, validate: Enum.IsDefined);
    public static readonly StyledProperty<PenLineJoin> AxisLineJoinProperty = AvaloniaProperty.Register<ChartAxis, PenLineJoin>(nameof(AxisLineJoin), PenLineJoin.Miter, validate: Enum.IsDefined);
    public static readonly StyledProperty<PenLineJoin> GridLineJoinProperty = AvaloniaProperty.Register<ChartAxis, PenLineJoin>(nameof(GridLineJoin), PenLineJoin.Miter, validate: Enum.IsDefined);
    public static readonly StyledProperty<PenLineJoin> MinorGridLineJoinProperty = AvaloniaProperty.Register<ChartAxis, PenLineJoin>(nameof(MinorGridLineJoin), PenLineJoin.Miter, validate: Enum.IsDefined);
    public static readonly StyledProperty<IEnumerable<ChartScaleBreak>?> ScaleBreaksSourceProperty =
        AvaloniaProperty.Register<ChartAxis, IEnumerable<ChartScaleBreak>?>(nameof(ScaleBreaksSource));
    public static readonly StyledProperty<IEnumerable<ChartPlotBand>?> PlotBandsSourceProperty =
        AvaloniaProperty.Register<ChartAxis, IEnumerable<ChartPlotBand>?>(nameof(PlotBandsSource));
    public static readonly StyledProperty<double> AxisThicknessProperty = AvaloniaProperty.Register<ChartAxis, double>(nameof(AxisThickness), 1,
        validate: value => double.IsFinite(value) && value >= 0);
    public static readonly StyledProperty<double> TickThicknessProperty = AvaloniaProperty.Register<ChartAxis, double>(nameof(TickThickness), 1,
        validate: value => double.IsFinite(value) && value >= 0);
    public static readonly StyledProperty<double> GridLineThicknessProperty = AvaloniaProperty.Register<ChartAxis, double>(nameof(GridLineThickness), 1,
        validate: value => double.IsFinite(value) && value >= 0);
    public static readonly StyledProperty<double> MinorGridLineThicknessProperty = AvaloniaProperty.Register<ChartAxis, double>(nameof(MinorGridLineThickness), 1,
        validate: value => double.IsFinite(value) && value >= 0);
    public static readonly StyledProperty<double> TickLengthProperty = AvaloniaProperty.Register<ChartAxis, double>(nameof(TickLength), 4,
        validate: value => double.IsFinite(value) && value >= 0);
    public static readonly StyledProperty<double> MinorTickLengthProperty = AvaloniaProperty.Register<ChartAxis, double>(nameof(MinorTickLength), 2,
        validate: value => double.IsFinite(value) && value >= 0);
    public static readonly StyledProperty<FontFamily?> LabelFontFamilyProperty = AvaloniaProperty.Register<ChartAxis, FontFamily?>(nameof(LabelFontFamily));
    public static readonly StyledProperty<FontFamily?> TitleFontFamilyProperty = AvaloniaProperty.Register<ChartAxis, FontFamily?>(nameof(TitleFontFamily));
    public static readonly StyledProperty<double?> TitleFontSizeProperty = AvaloniaProperty.Register<ChartAxis, double?>(nameof(TitleFontSize),
        validate: value => value is null || double.IsFinite(value.Value) && value > 0);
    public static readonly StyledProperty<double?> LabelFontSizeProperty = AvaloniaProperty.Register<ChartAxis, double?>(nameof(LabelFontSize),
        validate: value => value is null || double.IsFinite(value.Value) && value > 0);
    public static readonly StyledProperty<ChartAxisLabelMode> LabelModeProperty = AvaloniaProperty.Register<ChartAxis, ChartAxisLabelMode>(nameof(LabelMode),
        validate: Enum.IsDefined);
    public static readonly StyledProperty<double> MinimumZoomProperty = AvaloniaProperty.Register<ChartAxis, double>(nameof(MinimumZoom), 1,
        validate: value => double.IsFinite(value) && value >= 1);
    public static readonly StyledProperty<double> MaximumZoomProperty = AvaloniaProperty.Register<ChartAxis, double>(nameof(MaximumZoom), 1_000_000,
        validate: value => double.IsFinite(value) && value >= 1);
    public static readonly StyledProperty<double?> ViewMinimumProperty = AvaloniaProperty.Register<ChartAxis, double?>(nameof(ViewMinimum),
        defaultBindingMode: BindingMode.TwoWay, validate: value => value is null || double.IsFinite(value.Value));
    public static readonly StyledProperty<double?> ViewMaximumProperty = AvaloniaProperty.Register<ChartAxis, double?>(nameof(ViewMaximum),
        defaultBindingMode: BindingMode.TwoWay, validate: value => value is null || double.IsFinite(value.Value));

    private readonly HashSet<ChartScaleBreak> _observedBreaks = [];
    private readonly HashSet<ChartPlotBand> _observedBands = [];
    private readonly HashSet<StyledElement> _ownedFeatures = [];
    private IReadOnlyList<ChartScaleBreakRange>? _breakRanges;
    private long[] _breakRevisions = [];
    private IReadOnlyList<ChartScaleBreak> _sourceBreaks = [];
    private IReadOnlyList<ChartPlotBand> _sourceBands = [];
    private INotifyCollectionChanged? _breakSourceNotifications;
    private INotifyCollectionChanged? _bandSourceNotifications;

    public ChartAxis()
    {
        ScaleBreaks.CollectionChanged += BreaksChanged;
        PlotBands.CollectionChanged += BandsChanged;
    }

    /// <summary>Positive minor step: raw units for numeric/date axes, exponent units for logarithmic axes.</summary>
    public double? MinorInterval { get => GetValue(MinorIntervalProperty); set => SetValue(MinorIntervalProperty, value); }
    /// <summary>Interior minor ticks per adjacent major pair; ignored when MinorInterval is set.</summary>
    public int MinorTickCount { get => GetValue(MinorTickCountProperty); set => SetValue(MinorTickCountProperty, value); }
    public bool ShowAxis { get => GetValue(ShowAxisProperty); set => SetValue(ShowAxisProperty, value); }
    /// <summary>Hides axis decorations, grid lines and bands while retaining the data projection.</summary>
    public bool IsVisible { get => GetValue(IsVisibleProperty); set => SetValue(IsVisibleProperty, value); }
    public bool ShowTitle { get => GetValue(ShowTitleProperty); set => SetValue(ShowTitleProperty, value); }
    public bool ShowLabels { get => GetValue(ShowLabelsProperty); set => SetValue(ShowLabelsProperty, value); }
    public bool ShowTicks { get => GetValue(ShowTicksProperty); set => SetValue(ShowTicksProperty, value); }
    public bool ShowGridLines { get => GetValue(ShowGridLinesProperty); set => SetValue(ShowGridLinesProperty, value); }
    public bool ShowMinorGridLines { get => GetValue(ShowMinorGridLinesProperty); set => SetValue(ShowMinorGridLinesProperty, value); }
    public IBrush? TitleBrush { get => GetValue(TitleBrushProperty); set => SetValue(TitleBrushProperty, value); }
    public IBrush? LabelBrush { get => GetValue(LabelBrushProperty); set => SetValue(LabelBrushProperty, value); }
    public IBrush? AxisBrush { get => GetValue(AxisBrushProperty); set => SetValue(AxisBrushProperty, value); }
    public IBrush? GridLineBrush { get => GetValue(GridLineBrushProperty); set => SetValue(GridLineBrushProperty, value); }
    public IBrush? MinorGridLineBrush { get => GetValue(MinorGridLineBrushProperty); set => SetValue(MinorGridLineBrushProperty, value); }
    public IBrush? TickBrush { get => GetValue(TickBrushProperty); set => SetValue(TickBrushProperty, value); }
    public IBrush? MinorTickBrush { get => GetValue(MinorTickBrushProperty); set => SetValue(MinorTickBrushProperty, value); }
    public IDashStyle? AxisDashStyle { get => GetValue(AxisDashStyleProperty); set => SetValue(AxisDashStyleProperty, value); }
    public IDashStyle? GridLineDashStyle { get => GetValue(GridLineDashStyleProperty); set => SetValue(GridLineDashStyleProperty, value); }
    public IDashStyle? MinorGridLineDashStyle { get => GetValue(MinorGridLineDashStyleProperty); set => SetValue(MinorGridLineDashStyleProperty, value); }
    public PenLineCap AxisLineCap { get => GetValue(AxisLineCapProperty); set => SetValue(AxisLineCapProperty, value); }
    public PenLineCap GridLineCap { get => GetValue(GridLineCapProperty); set => SetValue(GridLineCapProperty, value); }
    public PenLineCap MinorGridLineCap { get => GetValue(MinorGridLineCapProperty); set => SetValue(MinorGridLineCapProperty, value); }
    public PenLineJoin AxisLineJoin { get => GetValue(AxisLineJoinProperty); set => SetValue(AxisLineJoinProperty, value); }
    public PenLineJoin GridLineJoin { get => GetValue(GridLineJoinProperty); set => SetValue(GridLineJoinProperty, value); }
    public PenLineJoin MinorGridLineJoin { get => GetValue(MinorGridLineJoinProperty); set => SetValue(MinorGridLineJoinProperty, value); }
    public double AxisThickness { get => GetValue(AxisThicknessProperty); set => SetValue(AxisThicknessProperty, value); }
    public double TickThickness { get => GetValue(TickThicknessProperty); set => SetValue(TickThicknessProperty, value); }
    public double GridLineThickness { get => GetValue(GridLineThicknessProperty); set => SetValue(GridLineThicknessProperty, value); }
    public double MinorGridLineThickness { get => GetValue(MinorGridLineThicknessProperty); set => SetValue(MinorGridLineThicknessProperty, value); }
    public double TickLength { get => GetValue(TickLengthProperty); set => SetValue(TickLengthProperty, value); }
    public double MinorTickLength { get => GetValue(MinorTickLengthProperty); set => SetValue(MinorTickLengthProperty, value); }
    public FontFamily? LabelFontFamily { get => GetValue(LabelFontFamilyProperty); set => SetValue(LabelFontFamilyProperty, value); }
    public FontFamily? TitleFontFamily { get => GetValue(TitleFontFamilyProperty); set => SetValue(TitleFontFamilyProperty, value); }
    public double? TitleFontSize { get => GetValue(TitleFontSizeProperty); set => SetValue(TitleFontSizeProperty, value); }
    public double? LabelFontSize { get => GetValue(LabelFontSizeProperty); set => SetValue(LabelFontSizeProperty, value); }
    public ChartAxisLabelMode LabelMode { get => GetValue(LabelModeProperty); set => SetValue(LabelModeProperty, value); }
    /// <summary>Minimum zoom factor relative to the full transformed range. Effective maximum is at least MinimumZoom.</summary>
    public double MinimumZoom { get => GetValue(MinimumZoomProperty); set => SetValue(MinimumZoomProperty, value); }
    public double MaximumZoom { get => GetValue(MaximumZoomProperty); set => SetValue(MaximumZoomProperty, value); }
    /// <summary>Optional independent raw-domain view bounds, normally used by additional axes.</summary>
    public double? ViewMinimum { get => GetValue(ViewMinimumProperty); set => SetValue(ViewMinimumProperty, value); }
    public double? ViewMaximum { get => GetValue(ViewMaximumProperty); set => SetValue(ViewMaximumProperty, value); }
    public ObservableCollection<ChartScaleBreak> ScaleBreaks { get; } = [];
    public ObservableCollection<ChartPlotBand> PlotBands { get; } = [];
    /// <summary>Optional bound breaks; a non-null source takes precedence over the inline collection.</summary>
    public IEnumerable<ChartScaleBreak>? ScaleBreaksSource { get => GetValue(ScaleBreaksSourceProperty); set => SetValue(ScaleBreaksSourceProperty, value); }
    /// <summary>Optional bound bands; a non-null source takes precedence over the inline collection.</summary>
    public IEnumerable<ChartPlotBand>? PlotBandsSource { get => GetValue(PlotBandsSourceProperty); set => SetValue(PlotBandsSourceProperty, value); }
    internal IReadOnlyList<ChartScaleBreak> ActiveScaleBreaks => ScaleBreaksSource is not { } source ? ScaleBreaks :
        _changed is not null ? _sourceBreaks : source as IReadOnlyList<ChartScaleBreak> ?? source.ToArray();
    internal IReadOnlyList<ChartPlotBand> ActivePlotBands => PlotBandsSource is not { } source ? PlotBands :
        _changed is not null ? _sourceBands : source as IReadOnlyList<ChartPlotBand> ?? source.ToArray();

    /// <summary>Tests strict interiors of merged breaks. Endpoints remain valid data coordinates.</summary>
    public bool IsInScaleBreak(double value)
    {
        if (ActiveScaleBreaks.Count == 0 || !double.IsFinite(value)) return false;
        return ContainsScaleBreak(GetMergedScaleBreaks(), value);
    }

    internal static bool ContainsScaleBreak(IReadOnlyList<ChartScaleBreakRange> ranges, double value)
    {
        for (var index = 0; index < ranges.Count; index++)
        {
            var range = ranges[index];
            if (value <= range.Start) return false;
            if (value < range.End) return true;
        }
        return false;
    }

    internal IReadOnlyList<ChartScaleBreakRange> GetMergedScaleBreaks()
    {
        var breaks = ActiveScaleBreaks;
        if (breaks.Count == 0) return [];
        if (_breakRanges is not null && _breakRevisions.Length == breaks.Count && (_changed is not null || ScaleBreaksSource is null))
        {
            var current = true;
            for (var index = 0; index < _breakRevisions.Length; index++)
                if (_breakRevisions[index] != breaks[index].Revision) { current = false; break; }
            if (current) return _breakRanges;
        }
        var result = new List<ChartScaleBreakRange>();
        foreach (var item in breaks.Where(item => item.Start < item.End).OrderBy(item => item.Start))
        {
            if (result.Count > 0 && item.Start <= result[^1].End)
            {
                var previous = result[^1];
                result[^1] = previous with { End = Math.Max(previous.End, item.End), CompressionRatio = Math.Min(previous.CompressionRatio, item.CompressionRatio) };
            }
            else result.Add(new(item.Start, item.End, item.CompressionRatio));
        }
        _breakRevisions = breaks.Select(item => item.Revision).ToArray();
        return _breakRanges = result.ToArray();
    }

    private static bool IsFeatureRangeProperty(AvaloniaProperty property) => property == MinimumZoomProperty || property == MaximumZoomProperty ||
        property == ScaleBreaksSourceProperty;

    private void FeatureSourcePropertyChanged(AvaloniaProperty property)
    {
        if (property != ScaleBreaksSourceProperty && property != PlotBandsSourceProperty) return;
        RefreshFeatureSources();
        SynchronizeFeatureChildren();
        if (_changed is not null) ObserveFeatures();
        _breakRanges = null;
    }

    private void RefreshFeatureSources()
    {
        _sourceBreaks = ScaleBreaksSource?.ToArray() ?? [];
        _sourceBands = PlotBandsSource?.ToArray() ?? [];
    }

    private void SynchronizeFeatureChildren()
    {
        var children = ActiveScaleBreaks.Cast<StyledElement>().Concat(ActivePlotBands).ToHashSet();
        foreach (var item in children)
            if (((ILogical)item).LogicalParent is { } parent && !ReferenceEquals(parent, this))
                throw new InvalidOperationException("An axis break or plot band can belong to only one axis.");
        foreach (var old in _ownedFeatures.Except(children).ToArray())
        { LogicalChildren.Remove(old); _ownedFeatures.Remove(old); }
        foreach (var item in children.Except(_ownedFeatures))
        {
            LogicalChildren.Add(item); _ownedFeatures.Add(item);
        }
    }

    private void ObserveFeatures()
    {
        StopObservingFeatures();
        RefreshFeatureSources();
        SynchronizeFeatureChildren();
        _breakRanges = null;
        _breakSourceNotifications = ScaleBreaksSource as INotifyCollectionChanged;
        _bandSourceNotifications = PlotBandsSource as INotifyCollectionChanged;
        if (_breakSourceNotifications is not null) _breakSourceNotifications.CollectionChanged += BreakSourceChanged;
        if (_bandSourceNotifications is not null) _bandSourceNotifications.CollectionChanged += BandSourceChanged;
        foreach (var item in ActiveScaleBreaks.Distinct()) { _observedBreaks.Add(item); item.Changed += BreakItemChanged; }
        foreach (var item in ActivePlotBands.Distinct()) { _observedBands.Add(item); item.Changed += BandItemChanged; }
    }
    private void StopObservingFeatures()
    {
        if (_breakSourceNotifications is not null) _breakSourceNotifications.CollectionChanged -= BreakSourceChanged;
        if (_bandSourceNotifications is not null) _bandSourceNotifications.CollectionChanged -= BandSourceChanged;
        _breakSourceNotifications = null; _bandSourceNotifications = null;
        foreach (var item in _observedBreaks) item.Changed -= BreakItemChanged;
        foreach (var item in _observedBands) item.Changed -= BandItemChanged;
        _observedBreaks.Clear(); _observedBands.Clear(); _breakRanges = null;
    }
    private void BreaksChanged(object? sender, NotifyCollectionChangedEventArgs args)
    { if (ScaleBreaksSource is not null) return; SynchronizeFeatureChildren(); _breakRanges = null; if (_changed is not null) ObserveFeatures(); _changed?.Invoke(this, new ChartAxisChangedEventArgs(true)); }
    private void BandsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    { if (PlotBandsSource is not null) return; SynchronizeFeatureChildren(); if (_changed is not null) ObserveFeatures(); _changed?.Invoke(this, new ChartAxisChangedEventArgs(false)); }
    private void BreakSourceChanged(object? sender, NotifyCollectionChangedEventArgs args)
    { ObserveFeatures(); _changed?.Invoke(this, new ChartAxisChangedEventArgs(true, ScaleBreaksSourceProperty)); }
    private void BandSourceChanged(object? sender, NotifyCollectionChangedEventArgs args)
    { ObserveFeatures(); _changed?.Invoke(this, new ChartAxisChangedEventArgs(false, PlotBandsSourceProperty)); }
    private void BreakItemChanged(object? sender, ChartAxisChangedEventArgs args)
    { if (args.RangeChanged) _breakRanges = null; _changed?.Invoke(this, args); }
    private void BandItemChanged(object? sender, EventArgs args) => _changed?.Invoke(this, new ChartAxisChangedEventArgs(false));
}
