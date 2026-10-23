using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.Charts;

/// <summary>A colored interval on a gauge's shared, linear business scale.</summary>
public sealed class GaugeRange : StyledElement
{
    public static readonly StyledProperty<double> StartProperty = AvaloniaProperty.Register<GaugeRange, double>(nameof(Start));
    public static readonly StyledProperty<double> EndProperty = AvaloniaProperty.Register<GaugeRange, double>(nameof(End), 100);
    public static readonly StyledProperty<IBrush?> BrushProperty = AvaloniaProperty.Register<GaugeRange, IBrush?>(nameof(Brush));
    public double Start { get => GetValue(StartProperty); set => SetValue(StartProperty, value); }
    public double End { get => GetValue(EndProperty); set => SetValue(EndProperty, value); }
    public IBrush? Brush { get => GetValue(BrushProperty); set => SetValue(BrushProperty, value); }
    internal event EventHandler? Changed;
    internal int ChangedObserverCount => Changed?.GetInvocationList().Length ?? 0;
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) { base.OnPropertyChanged(change); Changed?.Invoke(this, EventArgs.Empty); }
}

/// <summary>An observable item for a concentric progress ring. Values share their host's domain.</summary>
public sealed class GaugeValue : StyledElement
{
    public static readonly StyledProperty<double> ValueProperty = AvaloniaProperty.Register<GaugeValue, double>(nameof(Value));
    public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<GaugeValue, string?>(nameof(Label));
    public static readonly StyledProperty<IBrush?> BrushProperty = AvaloniaProperty.Register<GaugeValue, IBrush?>(nameof(Brush));
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public IBrush? Brush { get => GetValue(BrushProperty); set => SetValue(BrushProperty, value); }
    internal event EventHandler? Changed;
    internal int ChangedObserverCount => Changed?.GetInvocationList().Length ?? 0;
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) { base.OnPropertyChanged(change); Changed?.Invoke(this, EventArgs.Empty); }
}

/// <summary>Shared linear scale, observable thresholds and raster export for compact gauges.</summary>
public abstract partial class GaugeBase : ChartBase
{
    public static readonly StyledProperty<double> MinimumProperty = AvaloniaProperty.Register<GaugeBase, double>(nameof(Minimum));
    public static readonly StyledProperty<double> MaximumProperty = AvaloniaProperty.Register<GaugeBase, double>(nameof(Maximum), 100);
    public static readonly StyledProperty<double> ValueProperty = AvaloniaProperty.Register<GaugeBase, double>(nameof(Value));
    public static readonly StyledProperty<double?> TargetValueProperty = AvaloniaProperty.Register<GaugeBase, double?>(nameof(TargetValue));
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<GaugeBase, string?>(nameof(Title));
    public static readonly StyledProperty<bool> ShowValueProperty = AvaloniaProperty.Register<GaugeBase, bool>(nameof(ShowValue), true);
    public static readonly StyledProperty<bool> ShowScaleProperty = AvaloniaProperty.Register<GaugeBase, bool>(nameof(ShowScale), true);
    public static readonly StyledProperty<string> ValueFormatProperty = AvaloniaProperty.Register<GaugeBase, string>(nameof(ValueFormat), "0.#");
    public static readonly StyledProperty<IBrush?> TrackBrushProperty = AvaloniaProperty.Register<GaugeBase, IBrush?>(nameof(TrackBrush), Brushes.LightGray);
    public static readonly StyledProperty<IBrush?> ValueBrushProperty = AvaloniaProperty.Register<GaugeBase, IBrush?>(nameof(ValueBrush), Brushes.DodgerBlue);
    public static readonly StyledProperty<IBrush?> NeedleBrushProperty = AvaloniaProperty.Register<GaugeBase, IBrush?>(nameof(NeedleBrush), Brushes.OrangeRed);
    public static readonly StyledProperty<IBrush?> AxisBrushProperty = AvaloniaProperty.Register<GaugeBase, IBrush?>(nameof(AxisBrush), Brushes.SlateGray);
    public static readonly StyledProperty<double> TrackThicknessProperty = AvaloniaProperty.Register<GaugeBase, double>(nameof(TrackThickness), 18, validate: v => double.IsFinite(v) && v >= 0 && v <= 4096);
    public static readonly StyledProperty<bool> ClockwiseProperty = AvaloniaProperty.Register<GaugeBase, bool>(nameof(Clockwise), true);
    public static readonly StyledProperty<IEnumerable<GaugeRange>?> RangesSourceProperty = AvaloniaProperty.Register<GaugeBase, IEnumerable<GaugeRange>?>(nameof(RangesSource));
    public static readonly DirectProperty<GaugeBase, bool> IsDomainValidProperty = AvaloniaProperty.RegisterDirect<GaugeBase, bool>(nameof(IsDomainValid), c => c.IsDomainValid);
    public static readonly DirectProperty<GaugeBase, double?> NormalizedValueProperty = AvaloniaProperty.RegisterDirect<GaugeBase, double?>(nameof(NormalizedValue), c => c.NormalizedValue);
    public static readonly DirectProperty<GaugeBase, double?> NormalizedTargetProperty = AvaloniaProperty.RegisterDirect<GaugeBase, double?>(nameof(NormalizedTarget), c => c.NormalizedTarget);
    private readonly HashSet<StyledElement> _observed = [];
    private readonly HashSet<INotifyCollectionChanged> _collections = [];
    private bool _attached;
    private bool _synchronizingItems, _itemsSyncPending;
    private long _attachmentRevision;
    private bool _publishedDomain = true;
    private double? _publishedValue = 0, _publishedTarget;
    private long _scaleRevision;
    protected GaugeBase() { ClipToBounds = true; Ranges.CollectionChanged += CollectionChanged; }
    protected override Type StyleKeyOverride => typeof(GaugeBase);
    public double Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double? TargetValue { get => GetValue(TargetValueProperty); set => SetValue(TargetValueProperty, value); }
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public bool ShowValue { get => GetValue(ShowValueProperty); set => SetValue(ShowValueProperty, value); }
    public bool ShowScale { get => GetValue(ShowScaleProperty); set => SetValue(ShowScaleProperty, value); }
    public string ValueFormat { get => GetValue(ValueFormatProperty); set => SetValue(ValueFormatProperty, value); }
    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    public IBrush? ValueBrush { get => GetValue(ValueBrushProperty); set => SetValue(ValueBrushProperty, value); }
    public IBrush? NeedleBrush { get => GetValue(NeedleBrushProperty); set => SetValue(NeedleBrushProperty, value); }
    public IBrush? AxisBrush { get => GetValue(AxisBrushProperty); set => SetValue(AxisBrushProperty, value); }
    public double TrackThickness { get => GetValue(TrackThicknessProperty); set => SetValue(TrackThicknessProperty, value); }
    public bool Clockwise { get => GetValue(ClockwiseProperty); set => SetValue(ClockwiseProperty, value); }
    public ObservableCollection<GaugeRange> Ranges { get; } = [];
    public IEnumerable<GaugeRange>? RangesSource { get => GetValue(RangesSourceProperty); set => SetValue(RangesSourceProperty, value); }
    public bool IsDomainValid => double.IsFinite(Minimum) && double.IsFinite(Maximum) && Maximum > Minimum;
    /// <summary>Clamped fraction; null denotes an invalid domain or a nonfinite current value. Raw Value is preserved.</summary>
    public double? NormalizedValue => Normalize(Value);
    public double? NormalizedTarget => TargetValue is { } target ? Normalize(target) : null;
    protected IEnumerable<GaugeRange> ActiveRanges => (RangesSource ?? Ranges).Where(r => r is not null);
    internal double? Normalize(double value)
    {
        if (!IsDomainValid || !double.IsFinite(value)) return null;
        if (value <= Minimum) return 0; if (value >= Maximum) return 1;
        var scale = Math.Max(Math.Abs(Minimum), Math.Abs(Maximum));
        return scale == 0 ? 0 : Math.Clamp((value / scale - Minimum / scale) / (Maximum / scale - Minimum / scale), 0, 1);
    }
    internal double ValueAt(double fraction) => Minimum * (1 - fraction) + Maximum * fraction;
    public override void ResetViewport() { VerifyAccess(); }
    protected override Size MeasureOverride(Size availableSize) => new(double.IsFinite(availableSize.Width) ? Math.Min(240, availableSize.Width) : 240, double.IsFinite(availableSize.Height) ? Math.Min(200, availableSize.Height) : 200);
    internal Rect GaugeContentBounds
    {
        get
        {
            var top = Padding.Top + 8 + (string.IsNullOrEmpty(Title) ? 0 : FontSize * 1.6 + 6);
            return new Rect(Padding.Left + 8, top, Math.Max(0, Bounds.Width - Padding.Left - Padding.Right - 16), Math.Max(0, Bounds.Height - top - Padding.Bottom - 8));
        }
    }
    internal virtual GaugeLayout GetGaugeLayout()
    {
        var rect = GaugeContentBounds; var reserve = ShowScale ? FontSize * 1.6 : 0;
        return new GaugeLayout(rect, rect.Center, Math.Max(0, Math.Min(rect.Width, rect.Height) / 2 - reserve));
    }
    public sealed override void Render(DrawingContext context)
    {
        base.Render(context);
        using var clip = context.PushClip(new Rect(Bounds.Size));
        if (Background is { } background) context.DrawRectangle(background, null, new Rect(Bounds.Size));
        if (!string.IsNullOrEmpty(Title)) DrawText(context, Title, new Point(Padding.Left + 8, Padding.Top + 4), false);
        if (GaugeContentBounds.Width > 0 && GaugeContentBounds.Height > 0) RenderGauge(context, GetGaugeLayout());
    }
    private protected abstract void RenderGauge(DrawingContext context, GaugeLayout layout);
    protected void DrawText(DrawingContext context, string value, Point position, bool centered = true, double sizeScale = 1)
    {
        var text = new FormattedText(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(FontFamily, FontStyle, FontWeight), FontSize * sizeScale, Foreground ?? Brushes.Black);
        text.MaxLineCount = 1; text.MaxTextWidth = Math.Max(1, GaugeContentBounds.Width); text.Trimming = TextTrimming.CharacterEllipsis;
        var location = centered ? position - new Vector(text.Width / 2, text.Height / 2) : position;
        location = new Point(Math.Clamp(location.X, 0, Math.Max(0, Bounds.Width - text.Width)), Math.Clamp(location.Y, 0, Math.Max(0, Bounds.Height - text.Height)));
        context.DrawText(text, location);
    }
    protected string Format(double value)
    {
        var format = ValueFormat ?? "0.#";
        try { return format.Contains('{') ? string.Format(CultureInfo.CurrentCulture, format, value) : value.ToString(format, CultureInfo.CurrentCulture); }
        catch (FormatException) { return value.ToString("G4", CultureInfo.CurrentCulture); }
    }
    protected string CurrentText => NormalizedValue is not null ? Format(Value) : "—";
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsDomainValidProperty || change.Property == NormalizedValueProperty || change.Property == NormalizedTargetProperty) return;
        if (change.Property == MinimumProperty || change.Property == MaximumProperty || change.Property == ValueProperty || change.Property == TargetValueProperty)
        {
            var revision = ++_scaleRevision;
            var domain = IsDomainValid; var value = NormalizedValue; var target = NormalizedTarget;
            var oldDomain = _publishedDomain; var oldValue = _publishedValue; var oldTarget = _publishedTarget;
            _publishedDomain = domain; _publishedValue = value; _publishedTarget = target;
            if (oldDomain != domain) RaisePropertyChanged(IsDomainValidProperty, oldDomain, domain);
            if (revision == _scaleRevision && oldValue != value) RaisePropertyChanged(NormalizedValueProperty, oldValue, value);
            if (revision == _scaleRevision && oldTarget != target) RaisePropertyChanged(NormalizedTargetProperty, oldTarget, target);
        }
        SynchronizeItems(); InvalidateVisual();
    }
    protected virtual IEnumerable<StyledElement> SourceElements => ActiveRanges;
    protected virtual IEnumerable<INotifyCollectionChanged> SourceCollections => RangesSource is INotifyCollectionChanged collection ? [collection] : [];
    protected void ItemsChanged() { VerifyAccess(); SynchronizeItems(); InvalidateVisual(); }
    private void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => ItemsChanged();
    private void ItemChanged(object? sender, EventArgs e) { VerifyAccess(); InvalidateVisual(); }
    private void SynchronizeItems()
    {
        _itemsSyncPending = true;
        if (_synchronizingItems) return;
        _synchronizingItems = true;
        try
        {
            while (_itemsSyncPending)
            {
                _itemsSyncPending = false;
                // Observe collection mutations before logical inheritance can synchronously
                // evaluate a binding which changes that same source collection.
                var collections = _attached ? SourceCollections.Distinct().ToHashSet() : [];
                foreach (var collection in _collections.ToArray()) if (!collections.Contains(collection))
                {
                    _collections.Remove(collection); collection.CollectionChanged -= CollectionChanged;
                    if (_itemsSyncPending) break;
                }
                if (_itemsSyncPending) continue;
                foreach (var collection in collections) if (_collections.Add(collection))
                {
                    collection.CollectionChanged += CollectionChanged;
                    if (_itemsSyncPending) break;
                }
                if (_itemsSyncPending) continue;
                var desired = _attached ? SourceElements.Distinct().ToHashSet() : [];
                foreach (var item in _observed.ToArray()) if (!desired.Contains(item))
                {
                    _observed.Remove(item);
                    if (item is GaugeRange range) range.Changed -= ItemChanged; else if (item is GaugeValue value) value.Changed -= ItemChanged;
                    if (ReferenceEquals(((ILogical)item).LogicalParent, this)) LogicalChildren.Remove(item);
                    if (_itemsSyncPending) break;
                }
                if (_itemsSyncPending) continue;
                foreach (var item in desired) if (_observed.Add(item))
                {
                    // Register the observer before publishing the logical child. A nested
                    // source replacement/detachment then removes both in the next pass.
                    if (item is GaugeRange range) range.Changed += ItemChanged; else if (item is GaugeValue value) value.Changed += ItemChanged;
                    if (((ILogical)item).LogicalParent is null) LogicalChildren.Add(item);
                    if (_itemsSyncPending) break;
                }
            }
        }
        finally { _synchronizingItems = false; }
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        var revision = ++_attachmentRevision;
        base.OnAttachedToVisualTree(e);
        if (revision != _attachmentRevision) return;
        _attached = this.IsAttachedToVisualTree(); SynchronizeItems();
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    { ++_attachmentRevision; _attached = false; SynchronizeItems(); base.OnDetachedFromVisualTree(e); }
}

internal readonly record struct GaugeLayout(Rect Bounds, Point Center, double Radius);
