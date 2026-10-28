using Avalonia;
using Avalonia.Data;
using Avalonia.VisualTree;
using Avalonia.Media;
using Avalonia.Threading;

namespace NT.AvaloniaControl.Charts;

public abstract partial class AnalysisChartBase
{
    public static readonly StyledProperty<IReadOnlyList<object>?> SelectedDataKeysProperty = EngineeringChartBase.SelectedDataKeysProperty.AddOwner<AnalysisChartBase>();
    public static readonly StyledProperty<Rect?> DataSelectionRangeProperty = EngineeringChartBase.DataSelectionRangeProperty.AddOwner<AnalysisChartBase>();
    public static readonly DirectProperty<AnalysisChartBase, IReadOnlyList<ChartDataPoint>> SelectedDataProperty = AvaloniaProperty.RegisterDirect<AnalysisChartBase, IReadOnlyList<ChartDataPoint>>(nameof(SelectedData), chart => chart.SelectedData);
    /// <summary>Requested native original identities. Unknown keys are retained for future data. Ordinary explicit IDs compare by value; implicit identities compare by reference.
    /// Map observations require a layer-scoped MapGlyphKey and relation observations require a node/edge-scoped RelationGlyphKey. Replace the list to notify changes.</summary>
    public IReadOnlyList<object>? SelectedDataKeys { get => GetValue(SelectedDataKeysProperty); set => SetValue(SelectedDataKeysProperty, value); }
    /// <summary>Optional selection brush in current plot-layout fractions, increasing rightward and downward from the top left.
    /// Visible native glyph bounds intersecting this rectangle contribute every original member. This is not a raw-data axis viewport.</summary>
    public Rect? DataSelectionRange { get => GetValue(DataSelectionRangeProperty); set => SetValue(DataSelectionRangeProperty, value); }
    /// <summary>Current visible original observations selected by native identity, deduplicated within their native domain.</summary>
    public IReadOnlyList<ChartDataPoint> SelectedData => _selectedData;
    public event EventHandler? DataSelectionChanged;

    private bool DataSelectionBusy => _dataSelectionUpdating || _dataSelectionClearing || _dataSelectionInputDepth > 0;
    internal long DataSelectionIntentVersion => _dataSelectionRequestVersion;
    internal bool IsDataSelectionCurrent => !_dirty && _dataSelectionSourceRevision == _revision &&
        _dataSelectionPublishedRequestVersion == _dataSelectionRequestVersion && !_dataSelectionClearing;
    private IReadOnlyList<ChartDataPoint> _selectedData = [];
    private IReadOnlyList<Geometry> _selectedDataGeometries = [];
    private object? _dataSelectionFrame;
    private long _dataSelectionSourceRevision = -1, _dataSelectionVersion, _dataSelectionRequestVersion, _dataSelectionPublishedRequestVersion;
    private int _dataSelectionInputDepth;
    private bool _dataSelectionUpdating, _dataSelectionQueued, _dataSelectionDeferred, _publishingRangeKeys, _dataSelectionClearing;
    private Rect? _dataSelectionPublishedRange;
    private bool _dataSelectionSemanticInvalidated;
    private ChartDataSelectionOverlay? _dataSelectionOverlay;
    private IReadOnlyList<object>? _publishingRangeKeyValue;
    private readonly List<Visual> _dataSelectionVisibilityAncestors = [];

    public IReadOnlyList<ChartDataPoint> GetSelectedData()
    {
        VerifyAccess(); _ = GetMarks(); RefreshDataSelection(); return _selectedData;
    }
    /// <summary>Sets a current-layout range brush. Null clears the brush and its range selection. Selection keys are separate from SelectedIndex and existing single-point selection.</summary>
    public void SelectDataRange(Rect? range)
    {
        VerifyAccess();
        if (!ChartDataSelection.ValidRange(range)) throw new ArgumentOutOfRangeException(nameof(range));
        if (range is not null && !IsSelectionEnabled) return;
        ++_dataSelectionRequestVersion;
        if (range is null) { ClearDataSelection(); return; }
        SetCurrentValue(DataSelectionRangeProperty, range); RefreshDataSelection();
    }
    public void ClearDataSelection()
    {
        VerifyAccess(); ++_dataSelectionRequestVersion; var version = _dataSelectionVersion; var hadRange = DataSelectionRange is not null;
        SetCurrentValue(DataSelectionRangeProperty, null);
        if (_dataSelectionVersion != version + (hadRange ? 1 : 0)) { RefreshDataSelection(); return; }
        SetCurrentValue(SelectedDataKeysProperty, null); RefreshDataSelection();
    }
    private void ApplySingleDataSelection(Action update)
    {
        var request = _dataSelectionRequestVersion; _dataSelectionInputDepth++;
        try
        {
            update();
            if (request == _dataSelectionRequestVersion) AdoptSingleDataSelection();
        }
        finally
        {
            _dataSelectionInputDepth--;
            if (_dataSelectionInputDepth == 0) RefreshDataSelection();
        }
    }
    private void AdoptSingleDataSelection()
    {
        var revision = _revision; var attachment = _attachmentRevision; var request = _dataSelectionRequestVersion;
        var selected = SelectedPoint;
        var mark = selected is null ? null : _marks.FirstOrDefault(candidate => candidate.IsVisible && candidate.Data.SourceIndex == selected.Index);
        var keys = mark is null ? Array.Empty<object>() : new[] { ChartDataSelection.Member(mark.Data, null).Key };
        if (revision != _revision || attachment != _attachmentRevision || request != _dataSelectionRequestVersion) return;
        SetCurrentValue(SelectedDataKeysProperty, Array.AsReadOnly(keys)); RefreshDataSelection();
    }
    private IEnumerable<ChartDataSelectionGlyph> DataSelectionGlyphs(bool includeOffPlot = false)
    {
        foreach (var mark in _marks)
            if (mark.IsVisible && mark.Fill is not null && (includeOffPlot ? ChartDataSelection.Drawable(mark.Bounds) : ChartDataSelection.Visible(mark.Bounds, _plot)))
                yield return new(Array.AsReadOnly(new[] { ChartDataSelection.Member(mark.Data, null) }), mark.Geometry);
    }
    private bool DataSelectionPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == SelectedDataProperty) return true;
        if (change.Property == SelectedDataKeysProperty)
        {
            ++_dataSelectionVersion; _dataSelectionSourceRevision = -1;
            if (!_publishingRangeKeys || !ReferenceEquals(SelectedDataKeys, _publishingRangeKeyValue))
            {
                ++_dataSelectionRequestVersion; SetCurrentValue(DataSelectionRangeProperty, null);
            }
            QueueDataSelection(); InvalidateVisual(); return true;
        }
        if (change.Property == DataSelectionRangeProperty)
        {
            ++_dataSelectionRequestVersion; ++_dataSelectionVersion; _dataSelectionSourceRevision = -1;
            QueueDataSelection(); InvalidateVisual(); return true;
        }
        if (change.Property == IsVisibleProperty || change.Property == IsEnabledProperty || change.Property == IsEffectivelyEnabledProperty)
        { InvalidateDataSelection(); return false; }
        return false;
    }
    private void InvalidateDataSelection()
    {
        ++_dataSelectionVersion; _dataSelectionSourceRevision = -1; _dataSelectionFrame = null; _selectedDataGeometries = [];
        ClearPublishedDataSelection(); QueueDataSelection();
    }
    private void DataSelectionAttached()
    {
        foreach (var ancestor in this.GetVisualAncestors())
        { ancestor.PropertyChanged += DataSelectionAncestorChanged; _dataSelectionVisibilityAncestors.Add(ancestor); }
        QueueDataSelection();
    }
    private void DataSelectionAncestorChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    { if (change.Property == IsVisibleProperty || change.Property == IsEnabledProperty) InvalidateDataSelection(); }
    private void DataSelectionDetached()
    {
        foreach (var ancestor in _dataSelectionVisibilityAncestors) ancestor.PropertyChanged -= DataSelectionAncestorChanged;
        _dataSelectionVisibilityAncestors.Clear();
        ++_dataSelectionVersion; _dataSelectionSourceRevision = -1; _dataSelectionFrame = null; _selectedDataGeometries = [];
        ClearPublishedDataSelection();
    }
    private void ClearPublishedDataSelection()
    {
        if (_dataSelectionClearing) return;
        _dataSelectionClearing = true;
        try
        {
            if (_selectedData.Count > 0 || SelectedDataKeys is { Count: > 0 }) _dataSelectionSemanticInvalidated = true;
            ReleaseDataSelectionOverlay();
            if (_selectedData.Count > 0) SetAndRaise(SelectedDataProperty, ref _selectedData, Array.Empty<ChartDataPoint>());
        }
        finally
        {
            _dataSelectionClearing = false;
            if (_dataSelectionDeferred) QueueDataSelection();
        }
    }
    private void QueueDataSelection()
    {
        _dataSelectionDeferred = true;
        if (_dataSelectionQueued || !_attached || !IsEffectivelyVisible || !IsEffectivelyEnabled) return;
        _dataSelectionQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _dataSelectionQueued = false;
            if (_rendering || _building || _dataSelectionUpdating || _dataSelectionInputDepth > 0) return;
            RefreshDataSelection();
        }, DispatcherPriority.Loaded);
    }
    private void RefreshDataSelection()
    {
        if (_dataSelectionInputDepth > 0) { _dataSelectionDeferred = true; return; }
        if (_dataSelectionUpdating) return;
        if (_dataSelectionClearing) { _dataSelectionDeferred = true; return; }
        if (_rendering || _building) { _dataSelectionDeferred = true; return; }
        _dataSelectionDeferred = false;
        if (!_attached || !IsEffectivelyVisible || !IsEffectivelyEnabled)
        {
            _dataSelectionSourceRevision = -1; _dataSelectionFrame = null; _selectedDataGeometries = [];
            ClearPublishedDataSelection(); return;
        }
        _dataSelectionUpdating = true;
        try
        {
            _dataSelectionDeferred = false;
            var marks = GetMarks(); var revision = _revision; var version = _dataSelectionVersion;
            var request = _dataSelectionRequestVersion; var attachment = _attachmentRevision;
            if (_dirty) { _dataSelectionDeferred = true; return; }
            if (_dataSelectionSourceRevision == revision && ReferenceEquals(_dataSelectionFrame, marks) &&
                _dataSelectionPublishedRequestVersion == request) return;
            var requested = SelectedDataKeys?.ToArray() ?? []; var range = DataSelectionRange;
            if (range is null && requested.Length == 0)
            {
                // The default chart must not traverse or copy every professional glyph's aggregate members.
                if (!Current()) { _dataSelectionDeferred = true; return; }
                var fastChanged = _dataSelectionSemanticInvalidated || _selectedData.Count != 0 || _dataSelectionPublishedRange is not null ||
                    _dataSelectionPublishedRequestVersion != request;
                _selectedDataGeometries = []; _dataSelectionSourceRevision = revision; _dataSelectionFrame = marks;
                if (_selectedData.Count != 0) SetAndRaise(SelectedDataProperty, ref _selectedData, Array.Empty<ChartDataPoint>());
                if (!Current()) { _dataSelectionDeferred = true; return; }
                _dataSelectionPublishedRange = null; _dataSelectionPublishedRequestVersion = request; _dataSelectionSemanticInvalidated = false;
                if (fastChanged) DataSelectionChanged?.Invoke(this, EventArgs.Empty);
                if (!Current()) { _dataSelectionDeferred = true; return; }
                ReleaseDataSelectionOverlay(); return;
            }
            var requestedSet = new ChartDataSelection.MatchSet(requested);
            var selected = new List<ChartDataPoint>(); var selectedKeys = new List<object>(); var geometries = new List<Geometry>();
            var identities = new HashSet<ChartDataSelectionMember>(ChartDataSelection.IdentityComparer.Instance);
            var nativeIdentities = new HashSet<ChartDataSelectionMember>(ChartDataSelection.NativeIdentityComparer.Instance);
            var rectangle = range is { } normalized ? ChartDataSelection.Project(normalized, _plot) : (Rect?)null;
            foreach (var glyph in DataSelectionGlyphs())
            {
                var inRange = rectangle is { } area && ChartDataSelection.Intersects(glyph.Geometry, area, _plot); var highlighted = false;
                foreach (var member in glyph.Members)
                {
                    if (!inRange && (rectangle is not null || !requestedSet.Contains(member))) continue;
                    highlighted = true;
                    if (identities.Add(member)) selected.Add(member.Data);
                    if (nativeIdentities.Add(member)) selectedKeys.Add(member.Key);
                }
                if (highlighted) geometries.Add(glyph.Geometry);
            }
            if (!Current()) { _dataSelectionDeferred = true; return; }
            if (range is not null)
            {
                _publishingRangeKeys = true; _publishingRangeKeyValue = Array.AsReadOnly(selectedKeys.ToArray());
                try { SetCurrentValue(SelectedDataKeysProperty, _publishingRangeKeyValue); }
                finally { _publishingRangeKeys = false; _publishingRangeKeyValue = null; }
                // Publishing our own derived keys advances the selection revision once. Any business override wins.
                if (revision != _revision || attachment != _attachmentRevision || DataSelectionRange != range ||
                    !KeysMatch(SelectedDataKeys, selectedKeys) || !_attached || _dirty)
                { _dataSelectionDeferred = true; return; }
                version = _dataSelectionVersion;
            }
            if (!Current()) { _dataSelectionDeferred = true; return; }
            var result = Array.AsReadOnly(selected.ToArray()); var changed = !ChartDataSelection.Same(_selectedData, result);
            var rangeChanged = _dataSelectionPublishedRange != range;
            if (!Current()) { _dataSelectionDeferred = true; return; }
            _selectedDataGeometries = Array.AsReadOnly(geometries.ToArray());
            _dataSelectionSourceRevision = revision; _dataSelectionFrame = marks;
            if (changed) SetAndRaise(SelectedDataProperty, ref _selectedData, result);
            if (!Current()) { _dataSelectionDeferred = true; return; }
            var semanticChanged = _dataSelectionSemanticInvalidated || changed || rangeChanged || _dataSelectionPublishedRequestVersion != request;
            _dataSelectionPublishedRange = range; _dataSelectionPublishedRequestVersion = request; _dataSelectionSemanticInvalidated = false;
            if (semanticChanged) DataSelectionChanged?.Invoke(this, EventArgs.Empty);
            if (!Current()) { _dataSelectionDeferred = true; return; }
            RefreshDataSelectionOverlay(); InvalidateVisual();
            bool Current() => version == _dataSelectionVersion && request == _dataSelectionRequestVersion && revision == _revision && attachment == _attachmentRevision &&
                _attached && !_dirty && ReferenceEquals(_marks, marks) && IsEffectivelyVisible && IsEffectivelyEnabled;
        }
        finally
        {
            _dataSelectionUpdating = false;
            if (_dataSelectionDeferred) QueueDataSelection();
        }
    }
    private static bool KeysMatch(IReadOnlyList<object>? first, IReadOnlyList<object> second)
    {
        if (first is null || first.Count != second.Count) return false;
        for (var index = 0; index < first.Count; index++) if (!Equals(first[index], second[index])) return false;
        return true;
    }
    /// <summary>Business-link selection across the current retained native glyph frame, including clipped glyphs.
    /// Public SelectedData and range brushes continue to describe only visible glyphs. No raw-source or invented observations are introduced.</summary>
    internal IReadOnlyList<ChartDataPoint>? GetSelectedDataForSynchronization()
    {
        VerifyAccess(); var marks = GetMarks(); var revision = _revision; var version = _dataSelectionVersion;
        var request = _dataSelectionRequestVersion; var attachment = _attachmentRevision;
        var keys = SelectedDataKeys?.ToArray() ?? [];
        if (!Current()) return null;
        if (keys.Length == 0) return Array.Empty<ChartDataPoint>();
        var requested = new ChartDataSelection.MatchSet(keys);
        if (!Current()) return null;
        var selected = new List<ChartDataPoint>();
        var identities = new HashSet<ChartDataSelectionMember>(ChartDataSelection.IdentityComparer.Instance);
        foreach (var glyph in DataSelectionGlyphs(includeOffPlot: true))
            foreach (var member in glyph.Members)
            {
                if (requested.Contains(member) && identities.Add(member)) selected.Add(member.Data);
                if (!Current()) return null;
            }
        return Current() ? Array.AsReadOnly(selected.ToArray()) : null;
        bool Current() => revision == _revision && version == _dataSelectionVersion && request == _dataSelectionRequestVersion &&
            attachment == _attachmentRevision && ReferenceEquals(_marks, marks) && !_dirty;
    }
    internal IReadOnlyList<object>? PrepareSynchronizedSelection(IReadOnlyList<object> keys, Func<ChartDataPoint, object?> keySelector)
    {
        VerifyAccess(); ArgumentNullException.ThrowIfNull(keys); ArgumentNullException.ThrowIfNull(keySelector);
        var marks = GetMarks(); var revision = _revision; var version = _dataSelectionVersion; var request = _dataSelectionRequestVersion; var attachment = _attachmentRevision;
        var businessKeys = new HashSet<object>(keys);
        var native = new List<object>(); var identities = new HashSet<ChartDataSelectionMember>(ChartDataSelection.NativeIdentityComparer.Instance);
        if (!Current()) return null;
        if (businessKeys.Count == 0) return Array.Empty<object>();
        foreach (var glyph in DataSelectionGlyphs(includeOffPlot: true))
            foreach (var member in glyph.Members)
            {
                var key = keySelector(member.Data);
                if (key is not null && businessKeys.Contains(key) && identities.Add(member)) native.Add(member.Key);
                if (!Current()) return null;
            }
        return Current() ? Array.AsReadOnly(native.ToArray()) : null;
        bool Current() => revision == _revision && version == _dataSelectionVersion && request == _dataSelectionRequestVersion &&
            attachment == _attachmentRevision && ReferenceEquals(_marks, marks) && !_dirty;
    }
    internal void ApplySynchronizedSelection(IReadOnlyList<object> keys, Func<ChartDataPoint, object?> keySelector)
    {
        if (PrepareSynchronizedSelection(keys, keySelector) is not { } native) return;
        ApplyPreparedSynchronizedSelection(native);
    }
    internal void ApplyPreparedSynchronizedSelection(IReadOnlyList<object> native)
    {
        SetCurrentValue(SelectedDataKeysProperty, native); RefreshDataSelection();
    }
    private void DataSelectionRenderFinished()
    {
        if (_dataSelectionDeferred || _dataSelectionSourceRevision != _revision) QueueDataSelection();
    }
    private void RefreshDataSelectionOverlay()
    {
        if (!_attached || !IsEffectivelyVisible || !IsEffectivelyEnabled || (_selectedDataGeometries.Count == 0 && DataSelectionRange is null))
        { ReleaseDataSelectionOverlay(); return; }
        if (_dataSelectionOverlay is null)
        {
            var created = new ChartDataSelectionOverlay(context =>
            {
                if (_dataTooltipExportDepth == 0) RenderDataSelection(context);
            });
            created.Initialize(); _dataSelectionOverlay = created;
        }
        var overlay = _dataSelectionOverlay;
        AttachDataTooltipOverlay(overlay, () => ReferenceEquals(_dataSelectionOverlay, overlay));
        overlay.InvalidateVisual(); InvalidateArrange();
    }
    private void ReleaseDataSelectionOverlay()
    {
        var overlay = _dataSelectionOverlay; _dataSelectionOverlay = null;
        if (overlay is not null) DetachDataTooltipOverlay(overlay);
    }
    private void MeasureDataSelection(Size size) => _dataSelectionOverlay?.Measure(size);
    private void ArrangeDataSelection(Size size)
    { if (_dataSelectionOverlay is { } overlay) { overlay.Measure(size); overlay.Arrange(new Rect(size)); } }
    private void ApplyTemplateDataSelection()
    {
        if (_dataSelectionOverlay is { } overlay)
            AttachDataTooltipOverlay(overlay, () => ReferenceEquals(_dataSelectionOverlay, overlay));
    }
    private void PrepareDataSelectionExport()
    {
        // Detached exports need current native geometry while keeping public detached readings empty.
        var marks = GetMarks(); var revision = _revision; var version = _dataSelectionVersion;
        var keys = SelectedDataKeys?.ToArray() ?? [];
        if (keys.Length == 0 && DataSelectionRange is null)
        {
            if (revision == _revision && version == _dataSelectionVersion && ReferenceEquals(marks, _marks) && !_dirty)
            { _selectedDataGeometries = []; _dataSelectionSourceRevision = revision; }
            return;
        }
        var requested = new ChartDataSelection.MatchSet(keys);
        var rectangle = DataSelectionRange is { } range ? ChartDataSelection.Project(range, _plot) : (Rect?)null;
        var geometries = new List<Geometry>();
        foreach (var glyph in DataSelectionGlyphs())
            if (rectangle is { } area ? ChartDataSelection.Intersects(glyph.Geometry, area, _plot) :
                glyph.Members.Any(requested.Contains)) geometries.Add(glyph.Geometry);
        if (revision != _revision || version != _dataSelectionVersion || !ReferenceEquals(marks, _marks) || _dirty) return;
        _selectedDataGeometries = Array.AsReadOnly(geometries.ToArray()); _dataSelectionSourceRevision = revision;
    }
    private void DataSelectionExportFinished()
    {
        if (!_attached) { _selectedDataGeometries = []; _dataSelectionSourceRevision = -1; _dataSelectionFrame = null; }
        else { _dataSelectionSourceRevision = -1; QueueDataSelection(); }
    }
    private void RenderDataSelection(DrawingContext context, bool includeRangeBrush = true)
    {
        if (_dirty || _dataSelectionSourceRevision != _revision || !IsEffectivelyVisible) return;
        using (context.PushClip(_plot))
        {
            foreach (var geometry in _selectedDataGeometries)
            {
                context.DrawGeometry(null, new Pen(Background ?? Brushes.White, 5), geometry);
                context.DrawGeometry(null, new Pen(Foreground ?? Brushes.Black, 2), geometry);
            }
            if (includeRangeBrush && DataSelectionRange is { } range)
                context.DrawRectangle(null, new Pen(Foreground ?? Brushes.Black, 1), ChartDataSelection.Project(range, _plot));
        }
    }
}
