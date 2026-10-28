using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;

namespace NT.AvaloniaControl.Charts;

/// <summary>Observable data mapping and appearance shared by Cartesian series.</summary>
/// <remarks>Change bound collections/items on the UI thread. Background results use PublishSnapshotAsync.</remarks>
public abstract partial class ChartSeries : StyledElement
{
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<ChartSeries, string?>(nameof(Title));
    public static readonly StyledProperty<IBrush?> StrokeProperty = AvaloniaProperty.Register<ChartSeries, IBrush?>(nameof(Stroke));
    public static readonly StyledProperty<IBrush?> FillProperty = AvaloniaProperty.Register<ChartSeries, IBrush?>(nameof(Fill));
    public static readonly StyledProperty<double> StrokeThicknessProperty = AvaloniaProperty.Register<ChartSeries, double>(nameof(StrokeThickness), 2, validate: value => double.IsFinite(value) && value >= 0);
    public static readonly StyledProperty<double> MarkerSizeProperty = AvaloniaProperty.Register<ChartSeries, double>(nameof(MarkerSize), 7, validate: value => double.IsFinite(value) && value >= 0);
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty = AvaloniaProperty.Register<ChartSeries, IEnumerable?>(nameof(ItemsSource));
    public static readonly StyledProperty<string?> CategoryPathProperty = AvaloniaProperty.Register<ChartSeries, string?>(nameof(CategoryPath));
    public static readonly StyledProperty<string?> ValuePathProperty = AvaloniaProperty.Register<ChartSeries, string?>(nameof(ValuePath));
    public static readonly StyledProperty<string?> IdPathProperty = AvaloniaProperty.Register<ChartSeries, string?>(nameof(IdPath));
    public static readonly StyledProperty<string?> HighPathProperty = AvaloniaProperty.Register<ChartSeries, string?>(nameof(HighPath));
    public static readonly StyledProperty<string?> LowPathProperty = AvaloniaProperty.Register<ChartSeries, string?>(nameof(LowPath));
    public static readonly StyledProperty<string?> WeightPathProperty = AvaloniaProperty.Register<ChartSeries, string?>(nameof(WeightPath));
    public static readonly StyledProperty<string?> XAxisKeyProperty = AvaloniaProperty.Register<ChartSeries, string?>(nameof(XAxisKey));
    public static readonly StyledProperty<string?> YAxisKeyProperty = AvaloniaProperty.Register<ChartSeries, string?>(nameof(YAxisKey));
    public static readonly StyledProperty<EmptyPointMode> EmptyPointModeProperty = AvaloniaProperty.Register<ChartSeries, EmptyPointMode>(nameof(EmptyPointMode), EmptyPointMode.Gap, validate: Enum.IsDefined);
    private EventHandler? _changed;
    private INotifyCollectionChanged? _observedItems;
    private readonly HashSet<INotifyPropertyChanged> _observedProperties = new(ReferenceEqualityComparer.Instance);
    private List<Entry> _entries = [];
    private CompactSeriesData? _compact;
    private bool _sourceReady = true;
    private ChartDataSnapshot? _cachedSnapshot;
    private int _deferDepth;
    private bool _pendingChanged;
    private bool _notifyingChanged;
    private bool _mapping;
    private bool _reportingError;
    private bool _enumeratingSource;
    private long _sourceVersion;

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public IBrush? Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public double StrokeThickness { get => GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
    public double MarkerSize { get => GetValue(MarkerSizeProperty); set => SetValue(MarkerSizeProperty, value); }
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public string? CategoryPath { get => GetValue(CategoryPathProperty); set => SetValue(CategoryPathProperty, value); }
    public string? ValuePath { get => GetValue(ValuePathProperty); set => SetValue(ValuePathProperty, value); }
    public string? IdPath { get => GetValue(IdPathProperty); set => SetValue(IdPathProperty, value); }
    public string? HighPath { get => GetValue(HighPathProperty); set => SetValue(HighPathProperty, value); }
    public string? LowPath { get => GetValue(LowPathProperty); set => SetValue(LowPathProperty, value); }
    public string? WeightPath { get => GetValue(WeightPathProperty); set => SetValue(WeightPathProperty, value); }
    public string? XAxisKey { get => GetValue(XAxisKeyProperty); set => SetValue(XAxisKeyProperty, value); }
    public string? YAxisKey { get => GetValue(YAxisKeyProperty); set => SetValue(YAxisKeyProperty, value); }
    public EmptyPointMode EmptyPointMode { get => GetValue(EmptyPointModeProperty); set => SetValue(EmptyPointModeProperty, value); }
    /// <summary>Source/mapping revision; collection and item changes are tracked while a Changed owner is subscribed.</summary>
    public long SourceVersion => _sourceVersion;
    // Histories may span scene replacements only when one live owner controls
    // their admission and release budget. Multi-chart series keep local plans.
    internal bool CanShareRenderingGeometryHistory => (_changed?.GetInvocationList().Length ?? 0) == 1;
    // Accepted computed coordinates may change without a source/mapping revision.
    internal long PublishedSnapshotRevision { get; private set; }
    public bool IsUpdateSuspended => _deferDepth > 0;
    public event EventHandler<ChartDataErrorEventArgs>? DataError;

    /// <summary>Owners unsubscribe on unload; an unowned series holds no external subscriptions.</summary>
    public event EventHandler? Changed
    {
        add
        {
            VerifyAccess();
            var first = _changed is null;
            _changed += value;
            if (first && _changed is not null) { ObserveSource(); ObservationChanged(true); DataChanged(); }
        }
        remove { VerifyAccess(); _changed -= value; if (_changed is null) { ObservationChanged(false); StopObserving(); _cachedSnapshot = null; } }
    }

    public void Refresh() { VerifyAccess(); InvalidateBufferedMapping(); RebuildEntries(); RebuildPropertyObservers(); DataChanged(); }
    public void SuspendUpdates() { VerifyAccess(); _deferDepth++; }
    public void ResumeUpdates()
    {
        VerifyAccess();
        if (_deferDepth == 0) throw new InvalidOperationException("ResumeUpdates requires a matching SuspendUpdates.");
        if (--_deferDepth == 0 && _pendingChanged) { _pendingChanged = false; NotifyChanged(); }
    }
    public IDisposable DeferUpdates() { SuspendUpdates(); return new UpdateScope(this); }

    /// <summary>Returns an immutable projection; unobserved sources are re-enumerated without keeping subscriptions.</summary>
    public ChartDataSnapshot CreateSnapshot()
    {
        VerifyAccess();
        return CreateSnapshotCore();
    }

    /// <summary>Produces the public snapshot while allowing computed series to transform a mapped input.</summary>
    protected virtual ChartDataSnapshot CreateSnapshotCore() => CreateMappedSnapshot();

    internal ChartDataSnapshot CreateMappedSnapshot()
    {
        VerifyAccess();
        if (_mapping) throw new ChartDataMappingException("Reentrant CreateSnapshot is not supported.");
        if (_sourceReady && _cachedSnapshot is { } cached && cached.Version == SourceVersion && _changed is not null) return cached;
        _mapping = true;
        try
        {
        if (_changed is null)
        {
            RebuildEntries();
            // Caller-owned mutable objects may change without notifications while unobserved.
            if (_bufferSource is { } unowned && !(unowned.SupportsDirectValues && CanUseCompactData())) InvalidateBufferedMapping();
        }
        else if (!_sourceReady) ObserveSource();
        var version = SourceVersion;
        if (this is FinancialSeries) return _cachedSnapshot = CreateFinancialSnapshot(version);
        if (_bufferState is not null)
        {
            try
            {
                var result = CreateBufferedSnapshot(version);
                if (version != SourceVersion) { InvalidateBufferedMapping(); throw new ChartDataMappingException("source changed while mapping; retry"); }
                return _cachedSnapshot = result;
            }
            catch { if (version != SourceVersion) InvalidateBufferedMapping(); throw; }
        }
        if (_compact is { } compact)
        {
            var result = compact.CreateSnapshot(version, EmptyPointMode);
            if (version != SourceVersion) throw new ChartDataMappingException("source changed while mapping; retry");
            return _cachedSnapshot = result;
        }
        if (CanUseMappedColumns()) return _cachedSnapshot = CreateMappedColumnsSnapshot(version);
        var entries = _entries.ToArray();
        var points = new ChartDataPoint[entries.Length];
        var issues = new List<ChartDataIssue>();
        HashSet<object>? ids = string.IsNullOrWhiteSpace(IdPath) ? null : [];
        for (var index = 0; index < entries.Length; index++)
        {
            points[index] = MapEntry(entries[index], index, issues, ids);
        }
        if (version != SourceVersion) throw new ChartDataMappingException("source changed while mapping; retry");
        if (this is RangeAreaSeries or RangeBarSeries or VarianceSeries) ApplyRangeEmptyPoints(points);
        else if (this is not StatisticalSeries and not DensitySeries) ApplyEmptyPoints(points);
        return _cachedSnapshot = ChartDataSnapshot.FromOwned(version, points, issues);
        }
        finally { _mapping = false; }
    }

    private ChartDataPoint MapEntry(Entry entry, int index, List<ChartDataIssue> issues, HashSet<object>? ids)
        => MapItem(entry.Item, entry.Identity, index, issues, ids);

    private ChartDataPoint MapItem(object? item, object? identity, int index, List<ChartDataIssue> issues, HashSet<object>? ids)
    {
            object? category; object? originalValue = null; double x; double y; bool valid;
            if (item is ChartPoint direct && string.IsNullOrWhiteSpace(CategoryPath) && string.IsNullOrWhiteSpace(ValuePath))
            { category = direct.X; x = direct.X; y = direct.Y; valid = double.IsFinite(x) && double.IsFinite(y); }
            else if (item is Point avaloniaPoint && string.IsNullOrWhiteSpace(CategoryPath) && string.IsNullOrWhiteSpace(ValuePath))
            { category = avaloniaPoint.X; x = avaloniaPoint.X; y = avaloniaPoint.Y; valid = double.IsFinite(x) && double.IsFinite(y); }
            else
            {
                category = string.IsNullOrWhiteSpace(CategoryPath) ? index : Read(item, CategoryPath, index, issues);
                x = ChartDataAccessor.TryNumber(category, out var numericCategory) ? numericCategory : category is null or string ? index : double.NaN;
                // Category-based projections and sparklines retain labels/keys independently of X.
                if (this is CircularSeries or SparklineSeries or AnalysisSeries || this is PolarSeries { IsCategorical: true }) x = index;
                else if (this is PolarSeries && !ChartDataAccessor.TryNumber(category, out x)) x = double.NaN;
                var rawValue = Read(item, ValuePath, index, issues);
                if (!string.IsNullOrWhiteSpace(ValuePath) || item is string || item is not null && !item.GetType().IsValueType)
                    originalValue = rawValue;
                valid = ChartDataAccessor.TryNumber(rawValue, out y) && double.IsFinite(x);
                if (!valid) y = double.NaN;
            }
            var id = string.IsNullOrWhiteSpace(IdPath) ? (item is not null && !item.GetType().IsValueType ? item : identity) : Read(item, IdPath, index, issues);
            if (ids is not null && (id is null || !ids.Add(id)))
            {
                var message = $"IdPath '{IdPath}' produced a null or duplicate identity at source index {index}.";
                ReportIssue(new ChartDataIssue(index, IdPath, message), issues);
                throw new InvalidOperationException(message);
            }
            var high = Optional(item, HighPath, index, issues);
            var low = Optional(item, LowPath, index, issues);
            if (this is RangeAreaSeries or RangeBarSeries or VarianceSeries)
            {
                valid = double.IsFinite(x) && high is { } upper && double.IsFinite(upper) && low is { } lower && double.IsFinite(lower);
                y = valid ? this is VarianceSeries ? high!.Value : Math.Max(high!.Value, low!.Value) : double.NaN;
                originalValue = null;
            }
            var weight = Optional(item, WeightPath, index, issues);
            return MapStatistics(new ChartDataPoint(index, id, item, category, new ChartPoint(x, y), !valid,
                high, low, weight) { WasEmpty = !valid, Value = originalValue,
                    IsTotal = this is WaterfallSeries waterfall && !string.IsNullOrWhiteSpace(waterfall.TotalPath) && Read(item, waterfall.TotalPath, index, issues) is true,
                    SizeValue = this is IChartPointSeries encoding && !string.IsNullOrWhiteSpace(encoding.SizePath) ? Optional(item, encoding.SizePath, index, issues) : this is BubbleSeries ? weight : null,
                    ColorValue = this is IChartPointSeries colorEncoding && !string.IsNullOrWhiteSpace(colorEncoding.ColorPath) ? Read(item, colorEncoding.ColorPath, index, issues) :
                        this is CircularSeries circular && !string.IsNullOrWhiteSpace(circular.ColorPath) ? Read(item, circular.ColorPath, index, issues) :
                        this is PolarSeries polar && !string.IsNullOrWhiteSpace(polar.ColorPath) ? Read(item, polar.ColorPath, index, issues) : null }, issues);
    }

    /// <summary>
    /// Publishes a matching version on the UI thread. Requires a live Changed owner (normally an attached chart),
    /// so later source changes remain observable. Unowned, stale, cancelled or malformed results are rejected.
    /// </summary>
    public bool TryPublishSnapshot(ChartDataSnapshot snapshot, long expectedSourceVersion, CancellationToken cancellationToken = default)
    {
        VerifyAccess(); ArgumentNullException.ThrowIfNull(snapshot);
        if (_changed is null || cancellationToken.IsCancellationRequested || expectedSourceVersion != SourceVersion || snapshot.Version != expectedSourceVersion) return false;
        var compactData = _compact;
        var bufferState = _bufferState;
        var entries = compactData is null && bufferState is null ? _entries.ToArray() : Array.Empty<Entry>();
        var idPath = IdPath;
        var count = bufferState?.Count ?? compactData?.Count ?? entries.Length;
        if (!_sourceReady || snapshot.Count != count) return false;
        var sourceIndices = new HashSet<int>();
        var identities = new HashSet<object>(string.IsNullOrWhiteSpace(idPath) ? ReferenceEqualityComparer.Instance : EqualityComparer<object>.Default);
        foreach (var point in snapshot.Points)
        {
            if (point.SourceIndex < 0 || point.SourceIndex >= count || !sourceIndices.Add(point.SourceIndex) ||
                point.StableId is null || !identities.Add(point.StableId) ||
                !(bufferState is not null ? MatchesSourceItem(point.Item, bufferState.GetItem(point.SourceIndex)) : compactData is { } compact ? compact.MatchesItem(point.SourceIndex, point.Item) : MatchesSourceItem(point.Item, entries[point.SourceIndex].Item)) ||
                (!point.IsEmpty && (!double.IsFinite(point.Point.X) || !double.IsFinite(point.Point.Y))) ||
                point.High is { } high && !double.IsFinite(high) || point.Low is { } low && !double.IsFinite(low) || point.Weight is { } weight && !double.IsFinite(weight)) return false;
            object? expectedId;
            if (!string.IsNullOrWhiteSpace(idPath))
            {
                var item = bufferState is not null ? bufferState.GetItem(point.SourceIndex) : entries[point.SourceIndex].Item;
                if (!ChartDataAccessor.TryRead(item, idPath, out expectedId)) return false;
            }
            else if (bufferState is not null)
            {
                var item = bufferState.GetItem(point.SourceIndex);
                expectedId = item is not null && !item.GetType().IsValueType ? item : bufferState.GetIdentity(point.SourceIndex);
            }
            else if (compactData is { } values) expectedId = values.GetIdentity(point.SourceIndex);
            else
            {
                var entry = entries[point.SourceIndex];
                expectedId = entry.Item is not null && !entry.Item.GetType().IsValueType ? entry.Item : entry.Identity;
            }
            if (string.IsNullOrWhiteSpace(idPath) ? !ReferenceEquals(point.StableId, expectedId) : !Equals(point.StableId, expectedId)) return false;
        }
        if (cancellationToken.IsCancellationRequested || expectedSourceVersion != SourceVersion) return false;
        _cachedSnapshot = snapshot; PublishedSnapshotRevision++; NotifyChanged(); return true;
    }
    /// <summary>Dispatches version-checked publication to the UI thread; the live-owner requirement still applies.</summary>
    public Task<bool> PublishSnapshotAsync(ChartDataSnapshot snapshot, long expectedSourceVersion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (cancellationToken.IsCancellationRequested) return Task.FromResult(false);
        if (Dispatcher.UIThread.CheckAccess()) return Task.FromResult(TryPublishSnapshot(snapshot, expectedSourceVersion, cancellationToken));
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = cancellationToken.Register(() => completion.TrySetResult(false));
        Dispatcher.UIThread.Post(() =>
        {
            try { if (!completion.Task.IsCompleted) completion.TrySetResult(TryPublishSnapshot(snapshot, expectedSourceVersion, cancellationToken)); }
            catch (Exception exception) { completion.TrySetException(exception); }
            finally { registration.Dispose(); }
        });
        return completion.Task;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            if (!UsesOwnMappingSource) return;
            SourceDataInvalidated();
            _sourceVersion++; _cachedSnapshot = null; _sourceReady = false;
            if (_changed is not null) ObserveSource(); else RebuildEntries();
            NotifyChanged();
        }
        else if (IsStatisticalMappingProperty(change.Property) || IsFinancialMappingProperty(change.Property) || change.Property == CategoryPathProperty || change.Property == ValuePathProperty || change.Property == IdPathProperty || change.Property == HighPathProperty || change.Property == LowPathProperty || change.Property == WeightPathProperty || change.Property == EmptyPointModeProperty || change.Property == WaterfallSeries.TotalPathProperty || change.Property == PointSeries.SizePathProperty || change.Property == PointSeries.ColorPathProperty || change.Property == CircularSeries.ColorPathProperty || change.Property == PolarSeries.ColorPathProperty)
        { InvalidateBufferedMapping(); if (_compact is not null && !CanUseCompactData()) MaterializeEntries(); RebuildPropertyObservers(); DataChanged(); }
        else NotifyChanged(change);
    }
    private static bool MatchesSourceItem(object? candidate, object? source) => source is null || source.GetType().IsValueType ? Equals(candidate, source) : ReferenceEquals(candidate, source);
    private object? Read(object? item, string? path, int index, List<ChartDataIssue> issues)
    {
        if (ChartDataAccessor.TryRead(item, path, out var value)) return value;
        ReportIssue(new ChartDataIssue(index, path, "Property path is missing or its getter failed."), issues);
        return null;
    }
    private double? Optional(object? item, string? path, int index, List<ChartDataIssue> issues) => string.IsNullOrWhiteSpace(path) ? null : ChartDataAccessor.TryNumber(Read(item, path, index, issues), out var value) ? value : null;
    private void ReportIssue(ChartDataIssue issue, List<ChartDataIssue> issues)
    {
        if (issues.Count >= 100) return;
        issues.Add(issue);
        if (_reportingError) return;
        _reportingError = true;
        try { DataError?.Invoke(this, new ChartDataErrorEventArgs(issue)); }
        finally { _reportingError = false; }
    }
    private void ApplyEmptyPoints(ChartDataPoint[] points)
    {
        if (EmptyPointMode == EmptyPointMode.Gap) return;
        for (var index = 0; index < points.Length; index++)
        {
            if (!points[index].WasEmpty || !double.IsFinite(points[index].Point.X)) continue;
            var replacement = double.NaN;
            if (EmptyPointMode == EmptyPointMode.Zero) replacement = 0;
            else
            {
                var left = index - 1; var right = index + 1;
                while (left >= 0 && points[left].WasEmpty) left--;
                while (right < points.Length && points[right].WasEmpty) right++;
                if (left < 0 || right >= points.Length) continue;
                var first = points[left].Point; var last = points[right].Point;
                if (EmptyPointMode == EmptyPointMode.Average) replacement = first.Y * 0.5 + last.Y * 0.5;
                else
                {
                    var span = last.X - first.X;
                    var fraction = span > 0 && double.IsFinite(span) ? (points[index].Point.X - first.X) / span : (index - left) / (double)(right - left);
                    replacement = first.Y * (1 - fraction) + last.Y * fraction;
                }
            }
            if (double.IsFinite(replacement)) points[index] = points[index] with { Point = points[index].Point with { Y = replacement }, IsEmpty = false };
        }
    }
    private void ObserveSource()
    {
        StopObserving(); RebuildEntries();
        _observedItems = MappingItemsSource as INotifyCollectionChanged;
        if (_observedItems is not null) _observedItems.CollectionChanged += ItemsChanged;
        RebuildPropertyObservers();
    }
    private void RebuildEntries()
    {
        if (_enumeratingSource) throw new ChartDataMappingException("Reentrant source enumeration is not supported.");
        _enumeratingSource = true;
        _sourceReady = false;
        try
        {
            var source = MappingItemsSource;
            var version = SourceVersion;
            if (source is IChartDataBufferSource buffer && this is not FinancialSeries)
            {
                var state = buffer.Capture();
                if (!ReferenceEquals(_bufferSource, buffer) || !ReferenceEquals(_bufferState, state)) InvalidateBufferedMapping();
                _bufferSource = buffer; _bufferState = state;
                _compact = null; _entries = []; _sourceReady = true;
                return;
            }
            _bufferSource = null; _bufferState = null; InvalidateBufferedMapping();
            var compact = CanUseCompactData() ? CompactSeriesData.TryCreate(source) : null;
            var entries = compact is null ? source?.Cast<object?>().Select(item => new Entry(item)).ToList() ?? [] : [];
            if (!ReferenceEquals(source, MappingItemsSource) || version != SourceVersion)
                throw new ChartDataMappingException("source changed while mapping; retry");
            _compact = compact; _entries = entries; _sourceReady = true;
        }
        finally { _enumeratingSource = false; }
    }
    private bool CanUseCompactData() => this is not FinancialSeries and not StatisticalSeries and not DensitySeries and not RangeAreaSeries and not RangeBarSeries and not VarianceSeries &&
        (this is not WaterfallSeries waterfall || string.IsNullOrWhiteSpace(waterfall.TotalPath)) && string.IsNullOrWhiteSpace(CategoryPath) && string.IsNullOrWhiteSpace(ValuePath) &&
        string.IsNullOrWhiteSpace(IdPath) && string.IsNullOrWhiteSpace(HighPath) && string.IsNullOrWhiteSpace(LowPath) && string.IsNullOrWhiteSpace(WeightPath) &&
        (this is not IChartPointSeries pointEncoding || string.IsNullOrWhiteSpace(pointEncoding.SizePath) && string.IsNullOrWhiteSpace(pointEncoding.ColorPath)) &&
        (this is not CircularSeries circular || string.IsNullOrWhiteSpace(circular.ColorPath)) &&
        (this is not PolarSeries polar || string.IsNullOrWhiteSpace(polar.ColorPath));
    private void MaterializeEntries()
    {
        var compact = _compact!;
        var entries = new List<Entry>(compact.Count);
        for (var index = 0; index < compact.Count; index++) entries.Add(new Entry(compact.GetItem(index), compact.GetIdentity(index)));
        _entries = entries; _compact = null;
    }
    private void StopObserving()
    {
        if (_observedItems is not null) _observedItems.CollectionChanged -= ItemsChanged;
        _observedItems = null;
        foreach (var item in _observedProperties) item.PropertyChanged -= ItemChanged;
        _observedProperties.Clear();
        _bufferObserverCounts.Clear();
    }
    private void RebuildPropertyObservers()
    {
        foreach (var item in _observedProperties) item.PropertyChanged -= ItemChanged;
        _observedProperties.Clear();
        _bufferObserverCounts.Clear();
        if (_changed is null || !UsesOwnMappingSource) return;
        if (_bufferState is not null) { ObserveBufferedItems(_bufferState.Slices, true); return; }
        string?[] paths = [CategoryPath, ValuePath, IdPath, HighPath, LowPath, WeightPath, (this as WaterfallSeries)?.TotalPath, (this as IChartPointSeries)?.SizePath, (this as IChartPointSeries)?.ColorPath, (this as CircularSeries)?.ColorPath, (this as PolarSeries)?.ColorPath];
        var allPaths = (this is AnalysisSeries analytical ? paths.Concat(analytical.ExtraPaths) : this is StatisticalSeries statistical ? paths.Concat(statistical.ExtraPaths) : this is FinancialSeries financial ? paths.Concat(financial.ExtraPaths) : paths)
            .Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.Ordinal).ToArray();
        var plans = new Dictionary<Type, string?[]>();
        Type? previousType = null;
        string?[] childPaths = [];
        foreach (var entry in _entries)
        {
            var item = entry.Item;
            if (item is null) continue;
            var type = item.GetType();
            if (item is INotifyPropertyChanged root && _observedProperties.Add(root)) root.PropertyChanged += ItemChanged;
            if (type != previousType)
            {
                if (!plans.TryGetValue(type, out var plannedPaths))
                    plans.Add(type, plannedPaths = allPaths.Where(path => ChartDataAccessor.MayHaveObservableDescendants(type, path!)).ToArray());
                childPaths = plannedPaths;
                previousType = type;
            }
            foreach (var path in childPaths)
                foreach (var owner in ChartDataAccessor.Owners(entry.Item, path))
                    if (owner is INotifyPropertyChanged observable && _observedProperties.Add(observable)) observable.PropertyChanged += ItemChanged;
        }
    }
    private void ItemsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        VerifyAccess();
        if (_bufferSource is { } buffer)
        {
            if (args is ChartBufferChangedEventArgs change)
            {
                if (change.PreviousState is { } previousState) TransferBufferedMappings(previousState, change.State);
                RemoveBufferedIdentities(change.Removed);
                ObserveBufferedItems(change.Removed, false);
                _bufferState = change.State;
                ObserveBufferedItems(change.Added, true);
            }
            else { _bufferState = buffer.Capture(); InvalidateBufferedMapping(); RebuildPropertyObservers(); }
            DataChanged(); return;
        }
        if (_compact is { } compact)
        {
            var updated = compact.Apply(args);
            if (updated is null) RebuildEntries(); else _compact = updated;
            RebuildPropertyObservers(); DataChanged(); return;
        }
        switch (args.Action)
        {
            case NotifyCollectionChangedAction.Add when args.NewStartingIndex >= 0 && args.NewItems is not null:
                _entries.InsertRange(args.NewStartingIndex, args.NewItems.Cast<object?>().Select(item => new Entry(item))); break;
            case NotifyCollectionChangedAction.Remove when args.OldStartingIndex >= 0 && args.OldItems is not null:
                _entries.RemoveRange(args.OldStartingIndex, args.OldItems.Count); break;
            case NotifyCollectionChangedAction.Replace when args.OldStartingIndex >= 0 && args.OldItems is not null && args.NewItems is not null:
                _entries.RemoveRange(args.OldStartingIndex, args.OldItems.Count); _entries.InsertRange(args.OldStartingIndex, args.NewItems.Cast<object?>().Select(item => new Entry(item))); break;
            case NotifyCollectionChangedAction.Move when args.OldStartingIndex >= 0 && args.NewStartingIndex >= 0 && args.OldItems is not null:
                var moved = _entries.GetRange(args.OldStartingIndex, args.OldItems.Count);
                _entries.RemoveRange(args.OldStartingIndex, args.OldItems.Count); _entries.InsertRange(args.NewStartingIndex, moved); break;
            default: RebuildEntries(); break;
        }
        RebuildPropertyObservers(); DataChanged();
    }
    private void ItemChanged(object? sender, PropertyChangedEventArgs args) { VerifyAccess(); InvalidateBufferedMapping(); RebuildPropertyObservers(); DataChanged(); }
    private void DataChanged(EventArgs? args = null) { SourceDataInvalidated(); _sourceVersion++; _cachedSnapshot = null; NotifyChanged(args); }
    /// <summary>Allows computed projections to release a retired history before notifying observers.</summary>
    protected virtual void SourceDataInvalidated() { }
    /// <summary>Computed series can choose an external mapped source instead of observing their own ItemsSource.</summary>
    protected virtual bool UsesOwnMappingSource => true;
    private IEnumerable? MappingItemsSource => UsesOwnMappingSource ? ItemsSource : null;
    /// <summary>Retires or restores ordinary collection/item observation after a computed series changes input mode.</summary>
    protected void RefreshMappedSourceObservation()
    {
        VerifyAccess(); _cachedSnapshot = null; _sourceReady = false;
        if (_changed is not null) ObserveSource(); else { StopObserving(); RebuildEntries(); }
    }
    /// <summary>Whether a chart or another live consumer currently owns change observation.</summary>
    protected bool IsObserved => _changed is not null;
    /// <summary>Allows computed series to retire subscriptions when their final owner releases them.</summary>
    protected virtual void ObservationChanged(bool observed) { }
    /// <summary>Invalidates a computed projection and notifies its owners.</summary>
    protected void InvalidateComputedData() { VerifyAccess(); DataChanged(); }
    /// <summary>Invalidates a computed projection while preserving a source-context change for dependent owners.</summary>
    protected void InvalidateComputedData(EventArgs args) { VerifyAccess(); DataChanged(args); }
    protected void ReportComputedIssue(ChartDataIssue issue)
    {
        if (_reportingError) return;
        _reportingError = true;
        try { DataError?.Invoke(this, new ChartDataErrorEventArgs(issue)); }
        finally { _reportingError = false; }
    }
    // One synchronous follow-up coalesces reentrant mutations; no recursive notification stack.
    private void NotifyChanged(EventArgs? args = null)
    {
        if (_deferDepth > 0 || _notifyingChanged) { _pendingChanged = true; return; }
        _notifyingChanged = true;
        try
        {
            _pendingChanged = false;
            _changed?.Invoke(this, args ?? EventArgs.Empty);
            if (_pendingChanged && _deferDepth == 0) { _pendingChanged = false; _changed?.Invoke(this, EventArgs.Empty); }
        }
        finally { _notifyingChanged = false; }
    }
    private readonly struct Entry(object? item, object? identity = null)
    {
        public object? Item { get; } = item;
        public object Identity { get; } = identity ?? (item is not null && !item.GetType().IsValueType ? item : new());
    }
    private sealed class UpdateScope(ChartSeries owner) : IDisposable
    {
        private ChartSeries? _owner = owner;
        public void Dispose() { if (_owner is { } current) { current.ResumeUpdates(); _owner = null; } }
    }
}

public sealed partial class LineSeries : ChartSeries;
public sealed partial class BarSeries : ChartSeries;
public sealed partial class ScatterSeries : ChartSeries;
