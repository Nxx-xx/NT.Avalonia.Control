using System.Collections.ObjectModel;
using Avalonia;

namespace NT.AvaloniaControl.Charts;

/// <summary>Describes a calculated point without confusing a fitted sample with an original observation.</summary>
public sealed record ChartAnalysisSample(bool IsSynthetic, ChartSeries? SourceSeries, int? SourceIndex,
    object? SourceItem, object? SourceId)
{
    public ChartPoint CalculatedPoint { get; init; }
}

/// <summary>A line calculated from the full mapped history of another series, or its own ItemsSource.</summary>
/// <remarks>SourceSeries takes precedence over ItemsSource. Source axes are inherited unless an axis key is explicitly supplied.</remarks>
public abstract class ChartAnalyticalSeries : ChartSeries, IChartLineSeries
{
    public static readonly StyledProperty<ChartSeries?> SourceSeriesProperty =
        AvaloniaProperty.Register<ChartAnalyticalSeries, ChartSeries?>(nameof(SourceSeries),
            coerce: (owner, source) => ((ChartAnalyticalSeries)owner).ValidateSource(source));
    private ChartSeries? _subscribedSource;
    private (long Version, long Published) _subscribedRevision;
    private bool _subscribing;
    private ChartDataSnapshot? _computedInput;
    private ChartDataSnapshot? _computedSnapshot;
    private long _computedVersion = -1;
    private bool _calculating;

    protected ChartAnalyticalSeries() => ShowMarkers = false;

    /// <summary>Full-history input. Null uses this series' ordinary data mapping properties.</summary>
    public ChartSeries? SourceSeries { get => GetValue(SourceSeriesProperty); set => SetValue(SourceSeriesProperty, value); }
    /// <summary>Number of completed computations, useful when diagnosing updates and viewport reuse.</summary>
    public long ComputationCount { get; private set; }
    public override IChartSeriesRenderer Renderer => CartesianSeriesRenderer.Line;
    ChartLineInterpolation IChartLineSeries.Interpolation => ChartLineInterpolation.Linear;
    ChartStackMode IChartLineSeries.StackMode => ChartStackMode.None;
    string? IChartLineSeries.StackGroup => null;

    internal string? EffectiveXAxisKey => XAxisKey ?? (SourceSeries is ChartAnalyticalSeries analytical ? analytical.EffectiveXAxisKey : SourceSeries?.XAxisKey);
    internal string? EffectiveYAxisKey => YAxisKey ?? (SourceSeries is ChartAnalyticalSeries analytical ? analytical.EffectiveYAxisKey : SourceSeries?.YAxisKey);
    internal virtual bool IsEffectivelyVisible => IsVisible;
    protected override bool UsesOwnMappingSource => SourceSeries is null;
    internal ChartDataSnapshot CreateInputSnapshot() => CreateMappedSnapshot();
    protected override ChartDataSnapshot CreateSnapshotCore()
    {
        VerifyAccess();
        return ComputeSnapshot(SourceSeries?.CreateSnapshot() ?? CreateMappedSnapshot());
    }

    internal ChartDataSnapshot ComputeSnapshot(ChartDataSnapshot input)
    {
        if (ReferenceEquals(input, _computedInput) && _computedVersion == SourceVersion && _computedSnapshot is { } cached) return cached;
        if (_calculating) throw new ChartDataMappingException("Reentrant analytical calculation is not supported.");
        _calculating = true;
        try
        {
            var version = SourceVersion;
            ChartDataSnapshot result;
            try { result = Calculate(input, version); }
            catch (Exception error) when (error is ArgumentException or OverflowException)
            {
                var issue = new ChartDataIssue(-1, null, error.Message);
                ReportComputedIssue(issue);
                result = ChartDataSnapshot.FromOwned(version, [], input.Issues.Append(issue).ToArray());
            }
            if (version != SourceVersion) throw new ChartDataMappingException("Analysis parameters changed during calculation; retry.");
            _computedInput = input; _computedVersion = version; _computedSnapshot = result;
            ComputationCount++;
            return result;
        }
        finally { _calculating = false; }
    }

    protected abstract ChartDataSnapshot Calculate(ChartDataSnapshot input, long version);

    protected ChartDataPoint ProjectEndpoint(ChartDataPoint endpoint, ChartPoint point) => endpoint with
    {
        Point = point, IsEmpty = !double.IsFinite(point.X) || !double.IsFinite(point.Y),
        WasEmpty = !double.IsFinite(point.X) || !double.IsFinite(point.Y), Value = null,
        RawPoint = endpoint.RawPoint ?? endpoint.Point, BaseValue = null, StackValue = null,
        Analysis = new(false, SourceSeries, endpoint.SourceIndex, endpoint.Item, endpoint.StableId) { CalculatedPoint = point },
    };

    private ChartSeries? ValidateSource(ChartSeries? source)
    {
        var visited = new HashSet<ChartSeries>(ReferenceEqualityComparer.Instance) { this };
        for (var current = source; current is not null; current = (current as ChartAnalyticalSeries)?.SourceSeries)
            if (!visited.Add(current)) throw new InvalidOperationException("An analytical series cannot contain a cyclic SourceSeries dependency.");
        if (this is Trendline { AttachmentOwner: { } owner } && !ReferenceEquals(owner, source))
            throw new InvalidOperationException("An attached trendline uses its containing series as SourceSeries. Remove it before changing the source.");
        return source;
    }

    protected override void ObservationChanged(bool observed)
    {
        base.ObservationChanged(observed);
        SynchronizeSourceSubscription();
        if (!observed) { _computedInput = null; _computedSnapshot = null; }
    }
    protected override void SourceDataInvalidated()
    {
        _computedInput = null; _computedSnapshot = null; _computedVersion = -1;
        base.SourceDataInvalidated();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        // Publish the new calculation revision before base notifies any chart owners.
        if (change.Property == SourceSeriesProperty)
        {
            SynchronizeSourceSubscription();
            RefreshMappedSourceObservation();
            InvalidateComputedData(new ChartAnalyticsMembershipChangedEventArgs());
        }
        else if (IsCalculationProperty(change.Property)) InvalidateComputedData();
        else if (change.Property == IsVisibleProperty) SynchronizeSourceSubscription();
        base.OnPropertyChanged(change);
    }

    protected virtual bool IsCalculationProperty(AvaloniaProperty property) => false;

    private void SynchronizeSourceSubscription()
    {
        var next = IsObserved ? SourceSeries : null;
        if (ReferenceEquals(next, _subscribedSource)) return;
        if (_subscribedSource is { } old) old.Changed -= OnSourceChanged;
        _subscribedSource = next;
        if (next is null) return;
        _subscribing = true;
        try { next.Changed += OnSourceChanged; _subscribedRevision = (next.SourceVersion, next.PublishedSnapshotRevision); }
        finally { _subscribing = false; }
    }
    internal void RefreshSourceObservation() => SynchronizeSourceSubscription();

    private void OnSourceChanged(object? sender, EventArgs args)
    {
        if (_subscribing || _subscribedSource is not { } source) return;
        var revision = (source.SourceVersion, source.PublishedSnapshotRevision);
        if (args is ChartAnalyticsMembershipChangedEventArgs || args is AvaloniaPropertyChangedEventArgs sourceChange && sourceChange.Property == SourceSeriesProperty)
        {
            _subscribedRevision = revision;
            InvalidateComputedData(new ChartAnalyticsMembershipChangedEventArgs());
            return;
        }
        // Axis changes notify the chart so inherited context stays current.
        if (revision != _subscribedRevision || args is AvaloniaPropertyChangedEventArgs { Property: var property } &&
            (property == XAxisKeyProperty || property == YAxisKeyProperty))
        {
            _subscribedRevision = revision;
            InvalidateComputedData();
        }
    }
}

/// <summary>A full-history Simple, EMA, WMA or triangular moving-average line.</summary>
public class MovingAverageSeries : ChartAnalyticalSeries
{
    public static readonly StyledProperty<int> PeriodProperty = AvaloniaProperty.Register<MovingAverageSeries, int>(nameof(Period), 14,
        validate: period => period is >= 1 and <= 65_536);
    public static readonly StyledProperty<ChartMovingAverageType> TypeProperty = AvaloniaProperty.Register<MovingAverageSeries, ChartMovingAverageType>(nameof(Type), validate: Enum.IsDefined);
    public int Period { get => GetValue(PeriodProperty); set => SetValue(PeriodProperty, value); }
    public ChartMovingAverageType Type { get => GetValue(TypeProperty); set => SetValue(TypeProperty, value); }
    protected override bool IsCalculationProperty(AvaloniaProperty property) => property == PeriodProperty || property == TypeProperty;
    protected override ChartDataSnapshot Calculate(ChartDataSnapshot input, long version)
    {
        var calculated = ChartAnalytics.MovingAverage(input.Points.Select(point => point.IsEmpty ? point.Point with { Y = double.NaN } : point.Point), Period, Type);
        var points = new ChartDataPoint[input.Points.Count];
        for (var index = 0; index < points.Length; index++) points[index] = ProjectEndpoint(input.Points[index], calculated[index]);
        return ChartDataSnapshot.FromOwned(version, points, input.Issues);
    }
}

/// <summary>A fitted trend or simple moving-average trend, sampled before viewport clipping.</summary>
public class ChartTrendlineSeries : ChartAnalyticalSeries
{
    public static readonly StyledProperty<ChartTrendlineType> TypeProperty = AvaloniaProperty.Register<ChartTrendlineSeries, ChartTrendlineType>(nameof(Type), validate: Enum.IsDefined);
    public static readonly StyledProperty<int> PolynomialDegreeProperty = AvaloniaProperty.Register<ChartTrendlineSeries, int>(nameof(PolynomialDegree), 2, validate: value => value is >= 1 and <= 10);
    public static readonly StyledProperty<int> PeriodProperty = AvaloniaProperty.Register<ChartTrendlineSeries, int>(nameof(Period), 14, validate: value => value is >= 1 and <= 65_536);
    public static readonly StyledProperty<int> SampleCountProperty = AvaloniaProperty.Register<ChartTrendlineSeries, int>(nameof(SampleCount), 128, validate: value => value is >= 2 and <= 4096);
    public static readonly StyledProperty<double?> MinimumProperty = AvaloniaProperty.Register<ChartTrendlineSeries, double?>(nameof(Minimum), validate: value => value is null || double.IsFinite(value.Value));
    public static readonly StyledProperty<double?> MaximumProperty = AvaloniaProperty.Register<ChartTrendlineSeries, double?>(nameof(Maximum), validate: value => value is null || double.IsFinite(value.Value));
    public static readonly StyledProperty<double> BackwardForecastProperty = AvaloniaProperty.Register<ChartTrendlineSeries, double>(nameof(BackwardForecast), validate: value => double.IsFinite(value) && value >= 0);
    public static readonly StyledProperty<double> ForwardForecastProperty = AvaloniaProperty.Register<ChartTrendlineSeries, double>(nameof(ForwardForecast), validate: value => double.IsFinite(value) && value >= 0);
    public ChartTrendlineType Type { get => GetValue(TypeProperty); set => SetValue(TypeProperty, value); }
    public int PolynomialDegree { get => GetValue(PolynomialDegreeProperty); set => SetValue(PolynomialDegreeProperty, value); }
    public int Period { get => GetValue(PeriodProperty); set => SetValue(PeriodProperty, value); }
    public int SampleCount { get => GetValue(SampleCountProperty); set => SetValue(SampleCountProperty, value); }
    public double? Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double? Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    /// <summary>Extends regression samples below the eligible source minimum by this many mapped X units. Explicit Minimum takes precedence.</summary>
    public double BackwardForecast { get => GetValue(BackwardForecastProperty); set => SetValue(BackwardForecastProperty, value); }
    /// <summary>Extends regression samples above the eligible source maximum by this many mapped X units. Explicit Maximum takes precedence.</summary>
    public double ForwardForecast { get => GetValue(ForwardForecastProperty); set => SetValue(ForwardForecastProperty, value); }
    /// <summary>Most recent successful full-history fit. Insufficient or singular data sets this to null and reports DataError.</summary>
    public ChartTrendlineResult? FitResult { get; private set; }
    protected override bool IsCalculationProperty(AvaloniaProperty property) => property == TypeProperty || property == PolynomialDegreeProperty ||
        property == PeriodProperty || property == SampleCountProperty || property == MinimumProperty || property == MaximumProperty ||
        property == BackwardForecastProperty || property == ForwardForecastProperty;
    protected override ChartDataSnapshot Calculate(ChartDataSnapshot input, long version)
    {
        FitResult = null;
        try
        {
            var coordinates = input.Points.Select(point => point.IsEmpty ? point.Point with { Y = double.NaN } : point.Point).ToArray();
            var minimum = Minimum; var maximum = Maximum;
            if (BackwardForecast > 0 || ForwardForecast > 0)
            {
                if (Type == ChartTrendlineType.MovingAverage) throw new ArgumentException("Moving-average trends retain source alignment and do not support forecasts.");
                var eligible = coordinates.Where(point => double.IsFinite(point.X) && double.IsFinite(point.Y) &&
                    (!(Type is ChartTrendlineType.Exponential or ChartTrendlineType.Power) || point.Y > 0) &&
                    (!(Type is ChartTrendlineType.Logarithmic or ChartTrendlineType.Power) || point.X > 0)).ToArray();
                if (eligible.Length > 0)
                {
                    minimum ??= eligible.Min(point => point.X) - BackwardForecast;
                    maximum ??= eligible.Max(point => point.X) + ForwardForecast;
                }
            }
            var fit = ChartAnalytics.FitTrend(coordinates, Type, PolynomialDegree, Period, SampleCount, minimum, maximum);
            FitResult = fit;
            var points = new ChartDataPoint[fit.Points.Count];
            for (var index = 0; index < points.Length; index++)
                points[index] = Type == ChartTrendlineType.MovingAverage ? ProjectEndpoint(input.Points[index], fit.Points[index]) :
                    new ChartDataPoint(index, new object(), null, fit.Points[index].X, fit.Points[index], false)
                    { Analysis = new(true, SourceSeries, null, null, null) { CalculatedPoint = fit.Points[index] } };
            return ChartDataSnapshot.FromOwned(version, points, input.Issues);
        }
        catch (Exception error) when (error is ArgumentException or OverflowException)
        {
            var issue = new ChartDataIssue(-1, null, error.Message);
            ReportComputedIssue(issue);
            return ChartDataSnapshot.FromOwned(version, [], input.Issues.Append(issue).ToArray());
        }
    }
}

/// <summary>A trend attached to ChartSeries.Trendlines; its containing series supplies data and default axes.</summary>
public sealed class Trendline : ChartTrendlineSeries
{
    internal ChartSeries? AttachmentOwner { get; set; }
    internal override bool IsEffectivelyVisible => IsVisible && (AttachmentOwner?.IsVisible ?? true);
}

internal sealed class ChartAnalyticsMembershipChangedEventArgs : EventArgs { }

public abstract partial class ChartSeries
{
    internal bool EffectiveVisibility => this is ChartAnalyticalSeries analytical ? analytical.IsEffectivelyVisible : IsVisible;
    internal bool UsesValueIdentity => this is ChartAnalyticalSeries { SourceSeries: { } source } ? source.UsesValueIdentity : !string.IsNullOrWhiteSpace(IdPath);
    private ObservableCollection<Trendline>? _trendlines;
    /// <summary>Full-history trend overlays owned by this source series.</summary>
    public ObservableCollection<Trendline> Trendlines => _trendlines ??= new TrendlineCollection(this);
    internal IEnumerable<Trendline> ExistingTrendlines => _trendlines is null ? Enumerable.Empty<Trendline>() : _trendlines;
    private sealed class TrendlineCollection(ChartSeries owner) : ObservableCollection<Trendline>
    {
        private void Attach(Trendline item)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (item.AttachmentOwner is not null || Contains(item)) throw new InvalidOperationException("A trendline may be attached only once.");
            item.SourceSeries = owner;
            item.AttachmentOwner = owner;
        }
        private static void Detach(Trendline item) { item.AttachmentOwner = null; item.SourceSeries = null; }
        protected override void InsertItem(int index, Trendline item)
        {
            owner.VerifyAccess(); if ((uint)index > (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
            CheckReentrancy(); Attach(item); base.InsertItem(index, item); owner.NotifyChanged(new ChartAnalyticsMembershipChangedEventArgs());
        }
        protected override void SetItem(int index, Trendline item)
        {
            owner.VerifyAccess(); if (ReferenceEquals(this[index], item)) return;
            Attach(item); var old = this[index]; base.SetItem(index, item); Detach(old); owner.NotifyChanged(new ChartAnalyticsMembershipChangedEventArgs());
        }
        protected override void RemoveItem(int index) { owner.VerifyAccess(); var old = this[index]; base.RemoveItem(index); Detach(old); owner.NotifyChanged(new ChartAnalyticsMembershipChangedEventArgs()); }
        protected override void ClearItems() { owner.VerifyAccess(); var old = this.ToArray(); base.ClearItems(); foreach (var item in old) Detach(item); owner.NotifyChanged(new ChartAnalyticsMembershipChangedEventArgs()); }
    }
}
