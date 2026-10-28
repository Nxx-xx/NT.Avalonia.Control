using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.Charts;

/// <summary>A two-handle selection brush for professional family layout. Fractions increase rightward (X) and
/// downward (Y) from the current plot's top left; the brush selects native glyph bounds and never fabricates data axes.
/// Only the attached, effectively visible selector observes its target. Horizontal and vertical selectors preserve the other brush interval.
/// A nondefault Range requested before attachment initializes an empty chart brush; an existing chart brush is authoritative on attachment.</summary>
public sealed class ChartDataRangeSelector : Decorator
{
    public static readonly StyledProperty<ChartRange> RangeProperty = AvaloniaProperty.Register<ChartDataRangeSelector, ChartRange>(nameof(Range), new(0, 1),
        defaultBindingMode: BindingMode.TwoWay, validate: value => double.IsFinite(value.Lower) && double.IsFinite(value.Upper) && value.Lower < value.Upper,
        coerce: (owner, value) => ((ChartDataRangeSelector)owner).Constrain(value));
    public static readonly StyledProperty<ChartInteractionAxes> AxisProperty = AvaloniaProperty.Register<ChartDataRangeSelector, ChartInteractionAxes>(nameof(Axis), ChartInteractionAxes.X, validate: value => value is ChartInteractionAxes.X or ChartInteractionAxes.Y);
    public static readonly StyledProperty<EngineeringChartBase?> EngineeringChartProperty = AvaloniaProperty.Register<ChartDataRangeSelector, EngineeringChartBase?>(nameof(EngineeringChart));
    public static readonly StyledProperty<AnalysisChartBase?> AnalysisChartProperty = AvaloniaProperty.Register<ChartDataRangeSelector, AnalysisChartBase?>(nameof(AnalysisChart));
    public static readonly StyledProperty<double> MinimumSpanProperty = ChartRangeSelector.MinimumSpanProperty.AddOwner<ChartDataRangeSelector>();
    private readonly ChartRangeSelector _selector = new() { Name = "PART_LayoutRangeSelector" };
    private readonly List<Visual> _visibilityAncestors = [];
    private ChartBase? _observed;
    private bool _attached, _synchronizing, _pending, _writeRequested, _receiving, _updatingInner, _applyingInput, _raisingRangeChanged;
    private ChartRange _receivedRange, _publishedRange = new(0, 1);
    private ChartRange? _detachedRangeRequest;
    public ChartDataRangeSelector()
    {
        Child = _selector; _selector.RangeChanged += InnerRangeChanged; UpdateSelector();
    }
    /// <summary>Ascending interval in the current plot layout, not a raw-data or transformed axis domain.</summary>
    public ChartRange Range { get => GetValue(RangeProperty); set => SetValue(RangeProperty, value); }
    public ChartInteractionAxes Axis { get => GetValue(AxisProperty); set => SetValue(AxisProperty, value); }
    /// <summary>Engineering target. Exactly one target property must be set.</summary>
    public EngineeringChartBase? EngineeringChart { get => GetValue(EngineeringChartProperty); set => SetValue(EngineeringChartProperty, value); }
    /// <summary>Analytical target. Exactly one target property must be set.</summary>
    public AnalysisChartBase? AnalysisChart { get => GetValue(AnalysisChartProperty); set => SetValue(AnalysisChartProperty, value); }
    /// <summary>Minimum span of local requests. An externally assigned chart brush remains authoritative.</summary>
    public double MinimumSpan { get => GetValue(MinimumSpanProperty); set => SetValue(MinimumSpanProperty, value); }
    /// <summary>The real pointer/keyboard operated selector, including its two accessible endpoint handles.</summary>
    public ChartRangeSelector Selector => _selector;
    public event EventHandler<ChartRangeChangedEventArgs>? RangeChanged;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RangeProperty)
        {
            UpdateSelector();
            if (!_receiving || Range != _receivedRange) _writeRequested = true;
            _pending = true; SynchronizeRange();
        }
        else if (change.Property == AxisProperty || change.Property == MinimumSpanProperty)
        { CoerceValue(RangeProperty); UpdateSelector(); _pending = true; SynchronizeRange(); }
        else if (change.Property == EngineeringChartProperty || change.Property == AnalysisChartProperty || change.Property == IsVisibleProperty || change.Property == IsEnabledProperty || change.Property == IsEffectivelyEnabledProperty)
            ObserveTarget();
    }
    private ChartRange Constrain(ChartRange value)
    {
        var lower = Math.Clamp(value.Lower, 0, 1); var upper = Math.Clamp(value.Upper, 0, 1);
        if (_receiving && value == _receivedRange) return new(lower, upper);
        if (upper - lower < MinimumSpan)
        {
            upper = Math.Min(1, Math.Max(Math.BitIncrement(lower), lower + MinimumSpan));
            lower = Math.Max(0, Math.Min(Math.BitDecrement(upper), upper - MinimumSpan));
        }
        return new(lower, upper);
    }
    private void UpdateSelector()
    {
        if (_selector is null || _updatingInner) return;
        _updatingInner = true;
        try
        {
            _selector.SetCurrentValue(ChartRangeSelector.MinimumSpanProperty, Math.Min(MinimumSpan, Range.Span));
            _selector.Orientation = Axis == ChartInteractionAxes.X ? Orientation.Horizontal : Orientation.Vertical;
            // The original selector's vertical domain increases upward. Reverse it to match top-left family layout.
            _selector.IsDirectionReversed = Axis == ChartInteractionAxes.Y;
            _selector.SetCurrentValue(ChartRangeSelector.RangeProperty, Range);
        }
        finally { _updatingInner = false; }
    }
    private void InnerRangeChanged(object? sender, ChartRangeChangedEventArgs args)
    {
        if (_updatingInner) return;
        _applyingInput = args.IsUserInitiated;
        try { SetCurrentValue(RangeProperty, args.NewRange); }
        finally { _applyingInput = false; }
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs args)
    {
        base.OnAttachedToVisualTree(args); _attached = true;
        foreach (var ancestor in this.GetVisualAncestors())
        { ancestor.PropertyChanged += AncestorChanged; _visibilityAncestors.Add(ancestor); }
        ObserveTarget();
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs args)
    {
        _attached = false; ObserveTarget();
        foreach (var ancestor in _visibilityAncestors) ancestor.PropertyChanged -= AncestorChanged;
        _visibilityAncestors.Clear(); base.OnDetachedFromVisualTree(args);
    }
    private void AncestorChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    { if (args.Property == IsVisibleProperty || args.Property == IsEnabledProperty || args.Property == IsEffectivelyEnabledProperty) ObserveTarget(); }
    private void ObserveTarget()
    {
        if (_observed is not null) _observed.PropertyChanged -= TargetChanged;
        _observed = _attached && IsEffectivelyVisible && IsEffectivelyEnabled && (EngineeringChart is null || AnalysisChart is null)
            ? (ChartBase?)EngineeringChart ?? AnalysisChart : null;
        if (_observed is not null) _observed.PropertyChanged += TargetChanged;
        _writeRequested = _observed is not null && CurrentRange is null && _detachedRangeRequest is not null;
        if (_observed is not null) _detachedRangeRequest = null;
        _pending = true; SynchronizeRange();
    }
    private void TargetChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Property == EngineeringChartBase.DataSelectionRangeProperty || args.Property == AnalysisChartBase.DataSelectionRangeProperty)
        { _pending = true; SynchronizeRange(); }
    }
    private Rect? CurrentRange => _observed switch { EngineeringChartBase engineering => engineering.DataSelectionRange, AnalysisChartBase analysis => analysis.DataSelectionRange, _ => null };
    private void SynchronizeRange()
    {
        if (_synchronizing) return;
        _synchronizing = true;
        try
        {
            for (var iteration = 0; _pending || _writeRequested; iteration++)
            {
                if (iteration == 64) throw new InvalidOperationException("Professional range-selector callbacks did not converge after 64 updates.");
                var write = _writeRequested; _writeRequested = false; _pending = false;
                if (_observed is not { } target)
                {
                    if (write) _detachedRangeRequest = Range;
                    break;
                }
                var axis = Axis;
                if (write)
                {
                    var current = CurrentRange ?? new Rect(0, 0, 1, 1); var range = Range;
                    var rectangle = axis == ChartInteractionAxes.X ? new Rect(range.Lower, current.Top, range.Span, current.Height) : new Rect(current.Left, range.Lower, current.Width, range.Span);
                    if (target is EngineeringChartBase engineering) engineering.SelectDataRange(rectangle);
                    else ((AnalysisChartBase)target).SelectDataRange(rectangle);
                    if (_writeRequested || !ReferenceEquals(_observed, target) || Axis != axis) continue;
                }
                var actual = CurrentRange ?? new Rect(0, 0, 1, 1);
                _receivedRange = axis == ChartInteractionAxes.X ? new(actual.Left, actual.Right) : new(actual.Top, actual.Bottom);
                _receiving = true;
                try { SetCurrentValue(RangeProperty, _receivedRange); }
                finally { _receiving = false; }
            }
        }
        finally { _synchronizing = false; }
        PublishRange();
    }
    private void PublishRange()
    {
        if (_raisingRangeChanged) return;
        for (var iteration = 0; _publishedRange != Range; iteration++)
        {
            if (iteration == 64) throw new InvalidOperationException("Professional range-selector notifications did not converge after 64 updates.");
            var old = _publishedRange; _publishedRange = Range; var user = _applyingInput && iteration == 0;
            _raisingRangeChanged = true;
            try { RangeChanged?.Invoke(this, new(old, _publishedRange, user)); }
            finally { _raisingRangeChanged = false; }
        }
    }
}
