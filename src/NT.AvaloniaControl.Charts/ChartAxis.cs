using System.Collections.Specialized;
using Avalonia;

namespace NT.AvaloniaControl.Charts;

/// <summary>Axis-space conversion, before viewport normalization or screen direction is applied.</summary>
public interface IChartAxisTransform
{
    bool IsValidValue(double value);
    double Transform(double value);
    double Inverse(double value);
}

/// <summary>Near is left/bottom; Far is right/top for vertical/horizontal axes respectively.</summary>
public enum ChartAxisPosition { Near, Far }

public enum DateTimeIntervalUnit { Auto, Millisecond, Second, Minute, Hour, Day, Week, Month, Year }

/// <summary>Distinguishes changes to axis values/projection from presentation-only changes.</summary>
public sealed class ChartAxisChangedEventArgs(bool rangeChanged, AvaloniaProperty? property = null) : EventArgs
{
    public bool RangeChanged { get; } = rangeChanged;
    public AvaloniaProperty? Property { get; } = property;
}

/// <summary>A linear numeric axis by default. Derive or use a specialized axis for other scales.</summary>
/// <remarks>As a StyledElement, an owned axis can inherit the chart's DataContext and resources.</remarks>
public partial class ChartAxis : StyledElement, IChartAxisTransform
{
    public static readonly StyledProperty<string?> KeyProperty = AvaloniaProperty.Register<ChartAxis, string?>(nameof(Key));
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<ChartAxis, string?>(nameof(Title));
    public static readonly StyledProperty<double?> MinimumProperty = AvaloniaProperty.Register<ChartAxis, double?>(nameof(Minimum));
    public static readonly StyledProperty<double?> MaximumProperty = AvaloniaProperty.Register<ChartAxis, double?>(nameof(Maximum));
    public static readonly StyledProperty<string?> LabelFormatProperty = AvaloniaProperty.Register<ChartAxis, string?>(nameof(LabelFormat));
    public static readonly StyledProperty<IReadOnlyList<string>?> LabelsProperty = AvaloniaProperty.Register<ChartAxis, IReadOnlyList<string>?>(nameof(Labels));
    public static readonly StyledProperty<int> DesiredTickCountProperty = AvaloniaProperty.Register<ChartAxis, int>(nameof(DesiredTickCount), 6);
    public static readonly StyledProperty<double?> IntervalProperty = AvaloniaProperty.Register<ChartAxis, double?>(nameof(Interval));
    public static readonly StyledProperty<bool> IsInversedProperty = AvaloniaProperty.Register<ChartAxis, bool>(nameof(IsInversed));
    public static readonly StyledProperty<ChartAxisPosition> PositionProperty = AvaloniaProperty.Register<ChartAxis, ChartAxisPosition>(nameof(Position));
    public static readonly StyledProperty<double> LabelRotationProperty = AvaloniaProperty.Register<ChartAxis, double>(nameof(LabelRotation),
        validate: double.IsFinite);
    public static readonly StyledProperty<double> LabelMaxWidthProperty = AvaloniaProperty.Register<ChartAxis, double>(nameof(LabelMaxWidth), 120,
        validate: value => double.IsFinite(value) && value > 0);
    public static readonly StyledProperty<bool> LabelWrapProperty = AvaloniaProperty.Register<ChartAxis, bool>(nameof(LabelWrap));

    private EventHandler<ChartAxisChangedEventArgs>? _changed;
    private INotifyCollectionChanged? _observedLabels;

    public string? Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public double? Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double? Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public string? LabelFormat { get => GetValue(LabelFormatProperty); set => SetValue(LabelFormatProperty, value); }
    /// <summary>Labels at zero-based category coordinates. An automatic map can be supplied by the chart.</summary>
    public IReadOnlyList<string>? Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }
    public int DesiredTickCount { get => GetValue(DesiredTickCountProperty); set => SetValue(DesiredTickCountProperty, value); }
    /// <summary>
    /// Optional major interval. Numeric axes use data units; logarithmic axes use exponent units.
    /// Date axes use milliseconds in Auto mode, otherwise counts of IntervalUnit. Invalid values select automatic ticks.
    /// </summary>
    public double? Interval { get => GetValue(IntervalProperty); set => SetValue(IntervalProperty, value); }
    public bool IsInversed { get => GetValue(IsInversedProperty); set => SetValue(IsInversedProperty, value); }
    public ChartAxisPosition Position { get => GetValue(PositionProperty); set => SetValue(PositionProperty, value); }
    public double LabelRotation { get => GetValue(LabelRotationProperty); set => SetValue(LabelRotationProperty, value); }
    public double LabelMaxWidth { get => GetValue(LabelMaxWidthProperty); set => SetValue(LabelMaxWidthProperty, value); }
    public bool LabelWrap { get => GetValue(LabelWrapProperty); set => SetValue(LabelWrapProperty, value); }

    public virtual bool IsValidValue(double value) => double.IsFinite(value);
    public virtual double Transform(double value) => IsValidValue(value) ? value : double.NaN;
    public virtual double Inverse(double value) => value;

    public virtual bool IsRangeProperty(AvaloniaProperty property) => property == MinimumProperty || property == MaximumProperty ||
        property == IsInversedProperty || property == IntervalProperty || property == KeyProperty || property == LabelsProperty ||
        IsFeatureRangeProperty(property);

    /// <summary>Owners unsubscribe on removal; label collection observation exists only while subscribed.</summary>
    public event EventHandler<ChartAxisChangedEventArgs>? Changed
    {
        add
        {
            var wasEmpty = _changed is null;
            _changed += value;
            if (wasEmpty && _changed is not null) { ObserveLabels(); ObserveFeatures(); }
        }
        remove
        {
            _changed -= value;
            if (_changed is null) { StopObservingLabels(); StopObservingFeatures(); }
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelsProperty && _changed is not null) ObserveLabels();
        FeatureSourcePropertyChanged(change.Property);
        _changed?.Invoke(this, new ChartAxisChangedEventArgs(IsRangeProperty(change.Property), change.Property));
    }

    private void ObserveLabels()
    {
        StopObservingLabels();
        _observedLabels = Labels as INotifyCollectionChanged;
        if (_observedLabels is not null) _observedLabels.CollectionChanged += LabelsChanged;
    }

    private void StopObservingLabels()
    {
        if (_observedLabels is not null) _observedLabels.CollectionChanged -= LabelsChanged;
        _observedLabels = null;
    }

    private void LabelsChanged(object? sender, NotifyCollectionChangedEventArgs args) =>
        _changed?.Invoke(this, new ChartAxisChangedEventArgs(true, LabelsProperty));
}

/// <summary>A continuous linear numerical axis.</summary>
public sealed class NumericAxis : ChartAxis
{
}

/// <summary>A categorical axis whose positions refer to zero-based category slots.</summary>
public sealed class CategoryAxis : ChartAxis
{
    public static readonly StyledProperty<CategoryPlotMode> PlotModeProperty = AvaloniaProperty.Register<CategoryAxis, CategoryPlotMode>(nameof(PlotMode),
        CategoryPlotMode.OnTicks, validate: Enum.IsDefined);
    public static readonly StyledProperty<double> GapProperty = AvaloniaProperty.Register<CategoryAxis, double>(nameof(Gap),
        validate: value => double.IsFinite(value) && value >= 0);
    /// <summary>OnTicks retains data endpoints; BetweenTicks reserves at least half a category slot at either end.</summary>
    public CategoryPlotMode PlotMode { get => GetValue(PlotModeProperty); set => SetValue(PlotModeProperty, value); }
    /// <summary>Additional padding at each end, measured in category slots.</summary>
    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }
    public override bool IsRangeProperty(AvaloniaProperty property) => base.IsRangeProperty(property) || property == PlotModeProperty || property == GapProperty;
}

/// <summary>A UTC date/time axis whose raw data domain is Unix milliseconds.</summary>
/// <remarks>DateTimeKind.Unspecified is interpreted as UTC; Local values are converted to UTC.</remarks>
public sealed class DateTimeAxis : ChartAxis
{
    public const double MinimumUnixMilliseconds = -62_135_596_800_000;
    public const double MaximumUnixMilliseconds = 253_402_300_799_999;
    public static readonly StyledProperty<DateTimeIntervalUnit> IntervalUnitProperty =
        AvaloniaProperty.Register<DateTimeAxis, DateTimeIntervalUnit>(nameof(IntervalUnit));
    public static readonly StyledProperty<string?> DateFormatProperty = AvaloniaProperty.Register<DateTimeAxis, string?>(nameof(DateFormat));
    public DateTimeIntervalUnit IntervalUnit { get => GetValue(IntervalUnitProperty); set => SetValue(IntervalUnitProperty, value); }
    public string? DateFormat { get => GetValue(DateFormatProperty); set => SetValue(DateFormatProperty, value); }
    public override bool IsValidValue(double value) => double.IsFinite(value) && value >= MinimumUnixMilliseconds && value <= MaximumUnixMilliseconds;
    public override bool IsRangeProperty(AvaloniaProperty property) => base.IsRangeProperty(property) || property == IntervalUnitProperty;
    public static double ToUnixMilliseconds(DateTimeOffset value) => value.ToUnixTimeMilliseconds();
    public static double ToUnixMilliseconds(DateTime value) => new DateTimeOffset(value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    }).ToUnixTimeMilliseconds();
    public static DateTimeOffset FromUnixMilliseconds(double value)
    {
        if (!double.IsFinite(value) || value < MinimumUnixMilliseconds || value > MaximumUnixMilliseconds)
            throw new ArgumentOutOfRangeException(nameof(value));
        return DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Floor(value));
    }
}

/// <summary>A positive numerical axis with a configurable logarithm base greater than one.</summary>
public sealed class LogarithmicAxis : ChartAxis
{
    public static readonly StyledProperty<double> LogBaseProperty = AvaloniaProperty.Register<LogarithmicAxis, double>(nameof(LogBase), 10,
        validate: value => double.IsFinite(value) && value > 1);
    public double LogBase { get => GetValue(LogBaseProperty); set => SetValue(LogBaseProperty, value); }
    public override bool IsValidValue(double value) => double.IsFinite(value) && value > 0;
    public override double Transform(double value) => IsValidValue(value) ? Math.Log(value) / Math.Log(LogBase) : double.NaN;
    public override double Inverse(double value) => Math.Pow(LogBase, value);
    public override bool IsRangeProperty(AvaloniaProperty property) => base.IsRangeProperty(property) || property == LogBaseProperty;
}
