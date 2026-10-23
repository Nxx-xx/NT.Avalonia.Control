using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.Charts;

/// <summary>An ascending interval in the full axis's normalized, transformed data domain.</summary>
public readonly record struct ChartRange(double Lower, double Upper)
{
    public bool IsValid => double.IsFinite(Lower) && double.IsFinite(Upper) && Lower >= 0 && Upper <= 1 && Lower < Upper;
    public double Span => Upper - Lower;
}

public enum ChartRangeSelectorPart { Window, Lower, Upper }

public sealed class ChartRangeChangedEventArgs(ChartRange oldRange, ChartRange newRange, bool isUserInitiated) : EventArgs
{
    public ChartRange OldRange { get; } = oldRange;
    public ChartRange NewRange { get; } = newRange;
    public bool IsUserInitiated { get; } = isUserInitiated;
}

/// <summary>A keyboard and pointer operated, atomic two-handle normalized range selector.</summary>
/// <remarks>
/// Horizontal ranges increase rightward; vertical ranges increase upward. Direction reversal only
/// changes presentation. Chart coupling uses transformed axis fractions, including logarithmic axes.
/// The chart is authoritative when attached, and subscriptions exist only while in a visual tree.
/// </remarks>
public sealed class ChartRangeSelector : Control
{
    public static readonly StyledProperty<ChartRange> RangeProperty = AvaloniaProperty.Register<ChartRangeSelector, ChartRange>(nameof(Range), new(0, 1),
        defaultBindingMode: BindingMode.TwoWay,
        validate: value => double.IsFinite(value.Lower) && double.IsFinite(value.Upper) && value.Lower < value.Upper,
        coerce: (owner, value) => ((ChartRangeSelector)owner).Constrain(value));
    public static readonly StyledProperty<Orientation> OrientationProperty = AvaloniaProperty.Register<ChartRangeSelector, Orientation>(nameof(Orientation), validate: Enum.IsDefined);
    public static readonly StyledProperty<bool> IsDirectionReversedProperty = AvaloniaProperty.Register<ChartRangeSelector, bool>(nameof(IsDirectionReversed));
    public static readonly StyledProperty<double> MinimumSpanProperty = AvaloniaProperty.Register<ChartRangeSelector, double>(nameof(MinimumSpan), .01,
        validate: value => double.IsFinite(value) && value > 0 && value <= 1);
    public static readonly StyledProperty<double> SmallChangeProperty = AvaloniaProperty.Register<ChartRangeSelector, double>(nameof(SmallChange), .01,
        validate: value => double.IsFinite(value) && value > 0 && value <= 1);
    public static readonly StyledProperty<double> LargeChangeProperty = AvaloniaProperty.Register<ChartRangeSelector, double>(nameof(LargeChange), .1,
        validate: value => double.IsFinite(value) && value > 0 && value <= 1);
    public static readonly StyledProperty<double> HandleSizeProperty = AvaloniaProperty.Register<ChartRangeSelector, double>(nameof(HandleSize), 16,
        validate: value => double.IsFinite(value) && value >= 4 && value <= 128);
    public static readonly StyledProperty<IBrush?> TrackBrushProperty = AvaloniaProperty.Register<ChartRangeSelector, IBrush?>(nameof(TrackBrush), Brushes.LightGray);
    public static readonly StyledProperty<IBrush?> SelectionBrushProperty = AvaloniaProperty.Register<ChartRangeSelector, IBrush?>(nameof(SelectionBrush), Brushes.DodgerBlue);
    public static readonly StyledProperty<IBrush?> HandleBrushProperty = AvaloniaProperty.Register<ChartRangeSelector, IBrush?>(nameof(HandleBrush), Brushes.White);
    public static readonly StyledProperty<IBrush?> HandleBorderBrushProperty = AvaloniaProperty.Register<ChartRangeSelector, IBrush?>(nameof(HandleBorderBrush), Brushes.DodgerBlue);
    public static readonly StyledProperty<IBrush?> FocusBrushProperty = AvaloniaProperty.Register<ChartRangeSelector, IBrush?>(nameof(FocusBrush), Brushes.DarkOrange);
    public static readonly StyledProperty<CartesianChart?> ChartProperty = AvaloniaProperty.Register<ChartRangeSelector, CartesianChart?>(nameof(Chart));
    public static readonly StyledProperty<ChartInteractionAxes> AxisProperty = AvaloniaProperty.Register<ChartRangeSelector, ChartInteractionAxes>(nameof(Axis), ChartInteractionAxes.X,
        validate: value => value is ChartInteractionAxes.X or ChartInteractionAxes.Y);
    public static readonly DirectProperty<ChartRangeSelector, bool> IsDraggingProperty = AvaloniaProperty.RegisterDirect<ChartRangeSelector, bool>(nameof(IsDragging), owner => owner.IsDragging);
    public static readonly DirectProperty<ChartRangeSelector, ChartRangeSelectorPart> ActivePartProperty = AvaloniaProperty.RegisterDirect<ChartRangeSelector, ChartRangeSelectorPart>(nameof(ActivePart), owner => owner.ActivePart);

    private readonly RangeHandle _lowerHandle;
    private readonly RangeHandle _upperHandle;
    private IPointer? _pointer;
    private bool _isDragging;
    private bool _applyingInput;
    private bool _preservingWindow;
    private bool _receivingChart;
    private bool _readingChart;
    private bool _writingChart;
    private bool _settlingRange;
    private bool _raisingRangeChanged;
    private bool _attached;
    private long _rangeRevision;
    private ChartRange _publishedRange = new(0, 1);
    private CartesianChart? _observedChart;
    private ChartRangeSelectorPart _activePart;
    private ChartRange _dragRange;
    private double _dragOrigin;
    private Size _lastSize;
    private readonly List<Visual> _visibilityAncestors = [];

    public ChartRangeSelector()
    {
        Focusable = true;
        AutomationProperties.SetName(this, "Chart range window");
        _lowerHandle = new(this, ChartRangeSelectorPart.Lower) { Name = "PART_LowerHandle" };
        _upperHandle = new(this, ChartRangeSelectorPart.Upper) { Name = "PART_UpperHandle" };
        AutomationProperties.SetName(_lowerHandle, "Lower range endpoint");
        AutomationProperties.SetName(_upperHandle, "Upper range endpoint");
        LogicalChildren.Add(_lowerHandle); LogicalChildren.Add(_upperHandle);
        VisualChildren.Add(_lowerHandle); VisualChildren.Add(_upperHandle);
    }

    public ChartRange Range { get => GetValue(RangeProperty); set => SetValue(RangeProperty, value); }
    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    public bool IsDirectionReversed { get => GetValue(IsDirectionReversedProperty); set => SetValue(IsDirectionReversedProperty, value); }
    /// <summary>Minimum span of local requests. A coupled chart's actual viewport is authoritative and
    /// can be smaller after external zoom or conflicting axis zoom limits. Moving either handle requests
    /// this minimum again; moving the window preserves the chart's actual span.</summary>
    public double MinimumSpan { get => GetValue(MinimumSpanProperty); set => SetValue(MinimumSpanProperty, value); }
    public double SmallChange { get => GetValue(SmallChangeProperty); set => SetValue(SmallChangeProperty, value); }
    public double LargeChange { get => GetValue(LargeChangeProperty); set => SetValue(LargeChangeProperty, value); }
    public double HandleSize { get => GetValue(HandleSizeProperty); set => SetValue(HandleSizeProperty, value); }
    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    public IBrush? SelectionBrush { get => GetValue(SelectionBrushProperty); set => SetValue(SelectionBrushProperty, value); }
    public IBrush? HandleBrush { get => GetValue(HandleBrushProperty); set => SetValue(HandleBrushProperty, value); }
    public IBrush? HandleBorderBrush { get => GetValue(HandleBorderBrushProperty); set => SetValue(HandleBorderBrushProperty, value); }
    public IBrush? FocusBrush { get => GetValue(FocusBrushProperty); set => SetValue(FocusBrushProperty, value); }
    /// <summary>Optional chart whose effective primary-axis viewport is coupled in both directions.</summary>
    public CartesianChart? Chart { get => GetValue(ChartProperty); set => SetValue(ChartProperty, value); }
    /// <summary>The primary axis to couple; does not change the selector's visual orientation.</summary>
    public ChartInteractionAxes Axis { get => GetValue(AxisProperty); set => SetValue(AxisProperty, value); }
    public bool IsDragging => _isDragging;
    public ChartRangeSelectorPart ActivePart => _activePart;
    public event EventHandler<ChartRangeChangedEventArgs>? RangeChanged;

    private ChartRange Constrain(ChartRange value)
    {
        var lower = Math.Clamp(value.Lower, 0, 1);
        var upper = Math.Clamp(value.Upper, 0, 1);
        if (_receivingChart || _preservingWindow && _observedChart is not null) return new(lower, upper);
        if (upper - lower < MinimumSpan)
        {
            upper = Math.Min(1, Math.Max(Math.BitIncrement(lower), lower + MinimumSpan));
            lower = Math.Max(0, Math.Min(Math.BitDecrement(upper), upper - MinimumSpan));
        }
        return new(lower, upper);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RangeProperty)
        {
            ++_rangeRevision;
            var receivingChart = _receivingChart; _receivingChart = false;
            _preservingWindow = false;
            if ((!_applyingInput || _raisingRangeChanged || _settlingRange) && !receivingChart) StopDrag();
            InvalidateArrange(); InvalidateVisual();
            if (_settlingRange) return;
            _settlingRange = true;
            try
            {
                if (!receivingChart) SynchronizeRequestedRange();
            }
            finally { _settlingRange = false; }
            if (_publishedRange == Range) return;
            var old = _publishedRange; _publishedRange = Range;
            var userInitiated = _applyingInput && !_raisingRangeChanged;
            var wasRaising = _raisingRangeChanged; _raisingRangeChanged = true;
            try { RangeChanged?.Invoke(this, new(old, Range, userInitiated)); }
            finally { _raisingRangeChanged = wasRaising; }
        }
        else if (change.Property == MinimumSpanProperty)
        {
            StopDrag(); CoerceValue(RangeProperty);
        }
        else if (change.Property == ChartProperty || change.Property == AxisProperty)
        {
            StopDrag(); ObserveChart();
        }
        else if (change.Property == OrientationProperty || change.Property == IsDirectionReversedProperty || change.Property == HandleSizeProperty)
        {
            StopDrag(); InvalidateMeasure(); InvalidateArrange(); InvalidateVisual();
        }
        else if (change.Property == IsEnabledProperty || change.Property == IsEffectivelyEnabledProperty || change.Property == IsVisibleProperty)
        {
            if (!IsEffectivelyEnabled || !IsVisible) StopDrag();
            InvalidateVisual();
        }
        else if (change.Property == TrackBrushProperty || change.Property == SelectionBrushProperty || change.Property == HandleBrushProperty ||
            change.Property == HandleBorderBrushProperty || change.Property == FocusBrushProperty)
        {
            InvalidateVisual(); _lowerHandle?.InvalidateVisual(); _upperHandle?.InvalidateVisual();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e); _attached = true;
        foreach (var ancestor in this.GetVisualAncestors())
        {
            ancestor.PropertyChanged += AncestorPropertyChanged; _visibilityAncestors.Add(ancestor);
        }
        ObserveChart();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _attached = false; StopDrag(); ObserveChart();
        foreach (var ancestor in _visibilityAncestors) ancestor.PropertyChanged -= AncestorPropertyChanged;
        _visibilityAncestors.Clear(); base.OnDetachedFromVisualTree(e);
    }

    private void AncestorPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsVisibleProperty && !IsEffectivelyVisible) StopDrag();
    }

    private void ObserveChart()
    {
        if (_observedChart is not null)
        {
            _observedChart.ViewportChanged -= ChartRangeChanged;
            _observedChart.PropertyChanged -= ChartPropertyChanged;
            _observedChart.ViewSynchronizationChanged -= ChartRangeChanged;
        }
        _observedChart = _attached ? Chart : null;
        if (_observedChart is null) return;
        _observedChart.ViewportChanged += ChartRangeChanged;
        _observedChart.PropertyChanged += ChartPropertyChanged;
        _observedChart.ViewSynchronizationChanged += ChartRangeChanged;
        ReadChartRange();
    }

    private void ChartRangeChanged(object? sender, EventArgs e) => ReadChartRange();
    private void ChartPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == CartesianChart.VisibleRangeProperty) ReadChartRange();
    }

    private void ReadChartRange()
    {
        if (_writingChart || _readingChart || _observedChart is null) return;
        _readingChart = true;
        try
        {
            if (_observedChart.GetVisibleFraction(Axis) is { IsValid: true } value)
            {
                if (!_applyingInput && value != Range) StopDrag();
                ReceiveChartRange(value);
            }
        }
        finally { _readingChart = false; }
    }

    private void ReceiveChartRange(ChartRange value)
    {
        _receivingChart = true;
        try { SetCurrentValue(RangeProperty, value); }
        finally { _receivingChart = false; }
    }

    private void SynchronizeRequestedRange()
    {
        // Property and viewport callbacks may replace Range, Chart or Axis. Finish the latest
        // request before publishing one semantic event, and never restore an older callback value.
        for (var iteration = 0; iteration < 64 && _observedChart is { } chart; iteration++)
        {
            var revision = _rangeRevision; var axis = Axis;
            _writingChart = true;
            try { chart.SetVisibleFraction(Range, axis); }
            finally { _writingChart = false; }
            if (revision != _rangeRevision || !ReferenceEquals(chart, _observedChart) || axis != Axis) continue;
            if (chart.GetVisibleFraction(axis) is not { IsValid: true } actual) return;
            ReceiveChartRange(actual);
            if (Range == actual) return;
        }
        if (_observedChart is not null) throw new InvalidOperationException("Range selector callbacks did not converge after 64 updates.");
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _lowerHandle.Measure(new(HandleSize, HandleSize)); _upperHandle.Measure(new(HandleSize, HandleSize));
        return Orientation == Orientation.Horizontal ? new(160, Math.Max(32, HandleSize + 8)) : new(Math.Max(32, HandleSize + 8), 160);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_lastSize != finalSize) StopDrag();
        _lastSize = finalSize;
        _lowerHandle.Arrange(HandleRect(Range.Lower, finalSize));
        _upperHandle.Arrange(HandleRect(Range.Upper, finalSize));
        return finalSize;
    }

    private double Length => Orientation == Orientation.Horizontal ? Bounds.Width : Bounds.Height;
    private double UsableLength => Math.Max(0, Length - HandleSize);
    private double PhysicalFraction(double fraction)
    {
        if (IsDirectionReversed) fraction = 1 - fraction;
        return Orientation == Orientation.Vertical ? 1 - fraction : fraction;
    }
    private double Position(double fraction) => HandleSize / 2 + PhysicalFraction(fraction) * UsableLength;
    private double Fraction(Point point)
    {
        var coordinate = Orientation == Orientation.Horizontal ? point.X : point.Y;
        var value = UsableLength > 0 ? (coordinate - HandleSize / 2) / UsableLength : 0;
        if (Orientation == Orientation.Vertical) value = 1 - value;
        return IsDirectionReversed ? 1 - value : value;
    }
    private Rect HandleRect(double fraction, Size size)
    {
        var length = Orientation == Orientation.Horizontal ? size.Width : size.Height;
        var offset = HandleSize / 2 + PhysicalFraction(fraction) * Math.Max(0, length - HandleSize) - HandleSize / 2;
        return Orientation == Orientation.Horizontal ? new(offset, (size.Height - HandleSize) / 2, HandleSize, HandleSize) :
            new((size.Width - HandleSize) / 2, offset, HandleSize, HandleSize);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var bounds = new Rect(Bounds.Size);
        // The entire track is an input surface, including empty space around the two handles.
        context.DrawRectangle(Brushes.Transparent, null, bounds);
        var horizontal = Orientation == Orientation.Horizontal;
        var first = Position(0); var last = Position(1);
        var from = Math.Min(first, last); var to = Math.Max(first, last);
        var track = horizontal ? new Rect(from, bounds.Center.Y - 3, to - from, 6) : new Rect(bounds.Center.X - 3, from, 6, to - from);
        context.DrawRectangle(TrackBrush, null, track, 3, 3);
        from = Math.Min(Position(Range.Lower), Position(Range.Upper)); to = Math.Max(Position(Range.Lower), Position(Range.Upper));
        var selected = horizontal ? new Rect(from, bounds.Center.Y - 5, to - from, 10) : new Rect(bounds.Center.X - 5, from, 10, to - from);
        context.DrawRectangle(SelectionBrush, IsFocused ? new Pen(FocusBrush, 2) : null, selected, 3, 3);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (ReferenceEquals(e.Source, this)) SetAndRaise(ActivePartProperty, ref _activePart, ChartRangeSelectorPart.Window);
        InvalidateVisual();
    }
    protected override void OnLostFocus(FocusChangedEventArgs e) { base.OnLostFocus(e); InvalidateVisual(); }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!IsEffectivelyEnabled || _pointer is not null || UsableLength <= 0 || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var position = e.GetPosition(this);
        var lowerDistance = Math.Abs(Fraction(position) - Range.Lower);
        var upperDistance = Math.Abs(Fraction(position) - Range.Upper);
        var threshold = HandleSize / UsableLength / 2;
        var part = Math.Min(lowerDistance, upperDistance) <= threshold
            ? lowerDistance <= upperDistance ? ChartRangeSelectorPart.Lower : ChartRangeSelectorPart.Upper
            : Fraction(position) > Range.Lower && Fraction(position) < Range.Upper ? ChartRangeSelectorPart.Window
            : lowerDistance <= upperDistance ? ChartRangeSelectorPart.Lower : ChartRangeSelectorPart.Upper;
        SetAndRaise(ActivePartProperty, ref _activePart, part);
        if (part == ChartRangeSelectorPart.Lower) _lowerHandle.Focus();
        else if (part == ChartRangeSelectorPart.Upper) _upperHandle.Focus();
        else Focus();
        _pointer = e.Pointer; _dragRange = Range; _dragOrigin = Fraction(position);
        SetAndRaise(IsDraggingProperty, ref _isDragging, true);
        e.Pointer.Capture(this); e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_pointer != e.Pointer || !IsEffectivelyEnabled) return;
        var delta = Fraction(e.GetPosition(this)) - _dragOrigin;
        ApplyInput(MoveRange(_dragRange, ActivePart, delta), ActivePart == ChartRangeSelectorPart.Window); e.Handled = true;
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_pointer != e.Pointer) return;
        StopDrag(); e.Handled = true;
    }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e) { base.OnPointerCaptureLost(e); StopDrag(); }

    private void StopDrag()
    {
        var pointer = _pointer; _pointer = null;
        SetAndRaise(IsDraggingProperty, ref _isDragging, false);
        if (pointer?.Captured == this) pointer.Capture(null);
    }

    private ChartRange MoveRange(ChartRange range, ChartRangeSelectorPart part, double delta) => part switch
    {
        ChartRangeSelectorPart.Lower => new(Math.Clamp(range.Lower + delta, 0, Math.Min(Math.BitDecrement(range.Upper), Math.Max(0, range.Upper - MinimumSpan))), range.Upper),
        ChartRangeSelectorPart.Upper => new(range.Lower, Math.Clamp(range.Upper + delta, Math.Max(Math.BitIncrement(range.Lower), Math.Min(1, range.Lower + MinimumSpan)), 1)),
        _ => MoveWindow(range, delta),
    };
    private static ChartRange MoveWindow(ChartRange range, double delta)
    {
        var lower = Math.Clamp(range.Lower + delta, 0, 1 - range.Span);
        var upper = Math.Min(1, lower + range.Span);
        if (lower >= upper)
        {
            upper = Math.Min(1, Math.BitIncrement(lower));
            lower = Math.Max(0, Math.Min(lower, Math.BitDecrement(upper)));
        }
        return new(lower, upper);
    }
    private void ApplyInput(ChartRange range, bool preserveWindow = false)
    {
        _applyingInput = true; _preservingWindow = preserveWindow;
        try { SetCurrentValue(RangeProperty, range); }
        finally { _applyingInput = false; _preservingWindow = false; }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !IsEffectivelyEnabled) return;
        if (e.Key == Key.Escape && IsDragging)
        {
            var original = _dragRange; StopDrag(); ApplyInput(original, true); e.Handled = true; return;
        }
        var delta = e.Key switch
        {
            Key.Left when Orientation == Orientation.Horizontal => -SmallChange,
            Key.Right when Orientation == Orientation.Horizontal => SmallChange,
            Key.Down when Orientation == Orientation.Vertical => -SmallChange,
            Key.Up when Orientation == Orientation.Vertical => SmallChange,
            Key.PageDown => -LargeChange,
            Key.PageUp => LargeChange,
            _ => double.NaN,
        };
        if (e.Key is Key.Home or Key.End)
        {
            StopDrag();
            delta = e.Key == Key.Home ? -1 : 1;
        }
        if (!double.IsFinite(delta)) return;
        if (IsDirectionReversed && e.Key is Key.Left or Key.Right or Key.Up or Key.Down) delta = -delta;
        ApplyInput(MoveRange(Range, ActivePart, delta), ActivePart == ChartRangeSelectorPart.Window); e.Handled = true;
    }

    private sealed class RangeHandle : Control
    {
        private readonly ChartRangeSelector _owner;
        private readonly ChartRangeSelectorPart _part;
        public RangeHandle(ChartRangeSelector owner, ChartRangeSelectorPart part) { _owner = owner; _part = part; Focusable = true; }
        protected override void OnGotFocus(FocusChangedEventArgs e)
        {
            base.OnGotFocus(e); _owner.SetAndRaise(ActivePartProperty, ref _owner._activePart, _part); InvalidateVisual();
        }
        protected override void OnLostFocus(FocusChangedEventArgs e) { base.OnLostFocus(e); InvalidateVisual(); }
        public override void Render(DrawingContext context)
        {
            base.Render(context);
            var bounds = new Rect(Bounds.Size).Deflate(1);
            context.DrawRectangle(_owner.HandleBrush, new Pen(IsFocused ? _owner.FocusBrush : _owner.HandleBorderBrush, IsFocused ? 2 : 1), bounds, 3, 3);
            var center = bounds.Center;
            if (_owner.Orientation == Orientation.Horizontal) context.DrawLine(new Pen(_owner.HandleBorderBrush, 1), center + new Vector(0, -3), center + new Vector(0, 3));
            else context.DrawLine(new Pen(_owner.HandleBorderBrush, 1), center + new Vector(-3, 0), center + new Vector(3, 0));
        }
    }
}
