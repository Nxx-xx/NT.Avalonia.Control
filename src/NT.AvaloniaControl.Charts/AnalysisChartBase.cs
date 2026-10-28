using System.Collections;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;

namespace NT.AvaloniaControl.Charts;

/// <summary>Source mapping for a single analytical projection; retains the shared source identity and empty-point policy.</summary>
public sealed class AnalysisSeries : ChartSeries
{
    internal IReadOnlyList<string?> ExtraPaths { get; set; } = [];
}

/// <summary>A painted analytical shape and its original business datum.</summary>
public sealed record AnalysisMarkInfo(ChartDataPoint Data, Geometry Geometry, IBrush? Fill, Point LabelPosition)
{
    public string? Label { get; init; }
    public IBrush? LabelBrush { get; init; }
    public string? TooltipText { get; init; }
    public bool IsVisible { get; init; } = true;
    public Rect Bounds => Geometry.Bounds;
}

/// <summary>Shared data ownership, exact shape interaction and raster export for analytical charts.</summary>
public abstract partial class AnalysisChartBase : ChartBase
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty = AvaloniaProperty.Register<AnalysisChartBase, IEnumerable?>(nameof(ItemsSource));
    public static readonly StyledProperty<string?> CategoryPathProperty = AvaloniaProperty.Register<AnalysisChartBase, string?>(nameof(CategoryPath));
    public static readonly StyledProperty<string?> ValuePathProperty = AvaloniaProperty.Register<AnalysisChartBase, string?>(nameof(ValuePath));
    public static readonly StyledProperty<string?> IdPathProperty = AvaloniaProperty.Register<AnalysisChartBase, string?>(nameof(IdPath));
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<AnalysisChartBase, string?>(nameof(Title));
    public static readonly StyledProperty<string?> ValueFormatProperty = AvaloniaProperty.Register<AnalysisChartBase, string?>(nameof(ValueFormat), "0.#");
    public static readonly StyledProperty<bool> ShowLabelsProperty = AvaloniaProperty.Register<AnalysisChartBase, bool>(nameof(ShowLabels), true);
    public static readonly StyledProperty<bool> ShowValuesProperty = AvaloniaProperty.Register<AnalysisChartBase, bool>(nameof(ShowValues), true);
    public static readonly StyledProperty<bool> IsTooltipEnabledProperty = AvaloniaProperty.Register<AnalysisChartBase, bool>(nameof(IsTooltipEnabled), true);
    public static readonly StyledProperty<bool> IsSelectionEnabledProperty = AvaloniaProperty.Register<AnalysisChartBase, bool>(nameof(IsSelectionEnabled), true);
    public static readonly StyledProperty<int> SelectedIndexProperty = AvaloniaProperty.Register<AnalysisChartBase, int>(nameof(SelectedIndex), -1, defaultBindingMode: BindingMode.TwoWay, validate: x => x >= -1);
    public static readonly StyledProperty<IReadOnlyList<IBrush>?> PaletteProperty = AvaloniaProperty.Register<AnalysisChartBase, IReadOnlyList<IBrush>?>(nameof(Palette));
    public static readonly DirectProperty<AnalysisChartBase, ChartPointHit?> ActivePointProperty = AvaloniaProperty.RegisterDirect<AnalysisChartBase, ChartPointHit?>(nameof(ActivePoint), c => c.ActivePoint);
    public static readonly DirectProperty<AnalysisChartBase, ChartPointHit?> SelectedPointProperty = AvaloniaProperty.RegisterDirect<AnalysisChartBase, ChartPointHit?>(nameof(SelectedPoint), c => c.SelectedPoint);
    private static readonly IBrush[] DefaultPalette = [Brush.Parse("#4385F4"), Brush.Parse("#19A798"), Brush.Parse("#F6A531"), Brush.Parse("#D7256A"), Brush.Parse("#9275D8")];
    private bool _attached, _dirty = true, _building, _rendering, _queued;
    private bool _subscribed, _syncing, _pending;
    private long _attachmentRevision, _revision;
    private Size _size;
    private Rect _plot;
    private IReadOnlyList<AnalysisMarkInfo> _marks = [];
    private ChartPointHit? _active, _selected;
    private bool _followSelected, _updatingSelectedIndex;
    private int _publishingSelectedIndex = -1;
    private bool _stateDirty, _stateQueued, _publishingState, _hasPendingActive, _resetRequested;
    private ChartPointHit? _pendingActive;
    private ChartRasterExporter? _exporter;

    protected AnalysisChartBase() { Focusable = true; ClipToBounds = true; }
    protected override Type StyleKeyOverride => typeof(AnalysisChartBase);
    public AnalysisSeries Series { get; } = new();
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public string? CategoryPath { get => GetValue(CategoryPathProperty); set => SetValue(CategoryPathProperty, value); }
    /// <summary>Alias for CategoryPath.</summary>
    public string? LabelPath { get => CategoryPath; set => CategoryPath = value; }
    public string? ValuePath { get => GetValue(ValuePathProperty); set => SetValue(ValuePathProperty, value); }
    public string? IdPath { get => GetValue(IdPathProperty); set => SetValue(IdPathProperty, value); }
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? ValueFormat { get => GetValue(ValueFormatProperty); set => SetValue(ValueFormatProperty, value); }
    public bool ShowLabels { get => GetValue(ShowLabelsProperty); set => SetValue(ShowLabelsProperty, value); }
    public bool ShowValues { get => GetValue(ShowValuesProperty); set => SetValue(ShowValuesProperty, value); }
    public bool IsTooltipEnabled { get => GetValue(IsTooltipEnabledProperty); set => SetValue(IsTooltipEnabledProperty, value); }
    public bool IsSelectionEnabled { get => GetValue(IsSelectionEnabledProperty); set => SetValue(IsSelectionEnabledProperty, value); }
    public int SelectedIndex { get => GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }
    public IReadOnlyList<IBrush>? Palette { get => GetValue(PaletteProperty); set => SetValue(PaletteProperty, value); }
    public Rect PlotArea { get { _ = GetMarks(); return _plot; } }
    public ChartPointHit? ActivePoint { get { _ = GetMarks(); return _active; } }
    public ChartPointHit? SelectedPoint { get { _ = GetMarks(); return _selected; } }
    public override void ResetViewport()
    {
        ClearProfessionalTrackball(); ClearDataSelection();
        if (_rendering)
        {
            _resetRequested = true; _followSelected = false; SetActive(null); QueueMarkState(); return;
        }
        SetCurrentValue(SelectedIndexProperty, -1); SetActive(null);
    }
    protected abstract IReadOnlyList<AnalysisMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot);
    protected virtual void DrawDecorations(DrawingContext context, IReadOnlyList<AnalysisMarkInfo> marks) { }
    protected virtual Rect GetPlotArea(Size size)
    {
        var left = Math.Clamp(Padding.Left, 0, size.Width);
        var top = Math.Clamp(Padding.Top + (string.IsNullOrEmpty(Title) ? 0 : FontSize * 1.6 + 6), 0, size.Height);
        var right = Math.Clamp(size.Width - Padding.Right, left, size.Width);
        var bottom = Math.Clamp(size.Height - Padding.Bottom, top, size.Height);
        return new(left, top, right - left, bottom - top);
    }
    protected IBrush BrushAt(int index) => Palette is { Count: > 0 } p ? p[Math.Abs(index % p.Count)] : DefaultPalette[Math.Abs(index % DefaultPalette.Length)];
    protected string FormatValue(double value)
    {
        if (!double.IsFinite(value)) return "—";
        try { return value.ToString(ValueFormat ?? "0.#", CultureInfo.CurrentCulture); }
        catch (FormatException) { return value.ToString("G4", CultureInfo.CurrentCulture); }
    }
    protected void DrawLabel(DrawingContext context, string text, Point center, double maxWidth, IBrush? brush = null)
    {
        if (string.IsNullOrEmpty(text) || maxWidth <= 0 || Bounds.Width <= 0 || Bounds.Height <= 0) return;
        var ft = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(FontFamily, FontStyle, FontWeight), FontSize, brush ?? Foreground ?? Brushes.Gray)
        { MaxTextWidth = Math.Max(.01, Math.Min(Bounds.Width, maxWidth)), MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis };
        var x = Math.Clamp(center.X - ft.Width / 2, 0, Math.Max(0, Bounds.Width - ft.Width));
        var y = Math.Clamp(center.Y - ft.Height / 2, 0, Math.Max(0, Bounds.Height - ft.Height));
        context.DrawText(ft, new(x, y));
    }
    public IReadOnlyList<AnalysisMarkInfo> GetMarks()
    {
        VerifyAccess();
        // Queries made by paint callbacks retain this frame's immutable mark array.
        // A subsequent UI job can build the next source revision and publish it safely.
        if (_building || _rendering) return _marks;
        if (!_attached && _permanentExportDepth == 0) _dirty = true;
        EnsureMarks();
        PublishMarkState();
        if (!_dirty) RefreshDataSelection();
        return _marks;
    }
    private void EnsureMarks()
    {
        if (_building || !_dirty) return;
        _building = true;
        try
        {
            var revision = _revision; _dirty = false;
            _plot = ReserveSharedLegendArea(GetPlotArea(_size.Width > 0 && _size.Height > 0 ? _size : Bounds.Size));
            var result = BuildMarks(Series.CreateSnapshot(), _plot).ToArray();
            _marks = Array.AsReadOnly(result);
            _stateDirty = true;
            if (revision != _revision) _dirty = true;
        }
        catch { _dirty = true; throw; }
        finally { _building = false; }
    }
    private void QueueMarkState()
    {
        if (_stateQueued) return;
        _stateQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _stateQueued = false;
            // A callback can explicitly run the dispatcher from inside Render. Do not
            // repost into that same draining loop; Render's finally schedules the job.
            if (_rendering) return;
            _ = GetMarks();
        });
    }
    private ChartPointHit? ResolveSelected() => _followSelected && _selected is { } identity ? FindIdentity(identity) :
        _marks.FirstOrDefault(m => m.IsVisible && m.Data.SourceIndex == SelectedIndex) is { } mark ? Hit(mark) : null;
    private void PublishMarkState()
    {
        if (!_stateDirty || _publishingState) return;
        if (_rendering || _building) { QueueMarkState(); return; }
        if (_dirty) { QueueMarkState(); return; }
        _publishingState = true;
        try
        {
            _stateDirty = false;
            var revision = _revision;
            var previousActive = _active; var previousSelected = _selected;
            if (_resetRequested)
            {
                _resetRequested = false;
                _updatingSelectedIndex = true;
                _publishingSelectedIndex = -1;
                try { SetCurrentValue(SelectedIndexProperty, _publishingSelectedIndex); }
                finally { _updatingSelectedIndex = false; }
            }
            var active = _hasPendingActive ? _pendingActive : _active;
            _hasPendingActive = false; _pendingActive = null;
            var selected = ResolveSelected();
            var followSelected = _followSelected; _followSelected = false;
            SetActive(active is null ? null : FindIdentity(active));
            if (revision != _revision) { _stateDirty = true; QueueMarkState(); return; }
            if (followSelected)
            {
                _updatingSelectedIndex = true;
                _publishingSelectedIndex = selected?.Index ?? -1;
                try { if (SelectedIndex != _publishingSelectedIndex) SetCurrentValue(SelectedIndexProperty, _publishingSelectedIndex); }
                finally { _updatingSelectedIndex = false; }
                if (revision != _revision) { _stateDirty = true; QueueMarkState(); return; }
            }
            SetSelected(selected);
            if (revision != _revision) { _stateDirty = true; QueueMarkState(); }
            if (!Equals(previousActive, _active) || !Equals(previousSelected, _selected)) InvalidateVisual();
        }
        finally { _publishingState = false; }
    }
    private ChartPointHit Hit(AnalysisMarkInfo mark) => ChartPointHit.FromData(Series, mark.Data, mark.LabelPosition);
    private ChartPointHit? FindIdentity(ChartPointHit hit) => _marks.FirstOrDefault(m => m.IsVisible && (string.IsNullOrWhiteSpace(Series.IdPath) ? ReferenceEquals(m.Data.StableId, hit.StableId) : Equals(m.Data.StableId, hit.StableId))) is { } m ? Hit(m) : null;
    public ChartPointHit? HitTest(Point position)
    {
        var marks = GetMarks();
        if (!new Rect(Bounds.Size).Contains(position) || !_plot.Contains(position)) return null;
        return marks.LastOrDefault(m => m.IsVisible && m.Fill is not null && Contains(m.Geometry, position)) is { } mark ? Hit(mark) : null;
    }
    private static bool Contains(Geometry geometry, Point point)
    {
        if (!geometry.Bounds.Contains(point)) return false;
        var local = point;
        if (geometry.Transform is { } transform)
        {
            if (!transform.Value.TryInvert(out var inverse)) return false;
            local = inverse.Transform(point);
        }
        if (geometry is PolylineGeometry polygon)
        {
            point = local;
            var p = polygon.Points; var inside = false;
            for (int i = 0, j = p.Count - 1; i < p.Count; j = i++)
            {
                var a = p[j]; var b = p[i];
                var cross = (point.X - a.X) * (b.Y - a.Y) - (point.Y - a.Y) * (b.X - a.X);
                if (Math.Abs(cross) < 1e-8 && point.X >= Math.Min(a.X, b.X) && point.X <= Math.Max(a.X, b.X) && point.Y >= Math.Min(a.Y, b.Y) && point.Y <= Math.Max(a.Y, b.Y)) return true;
                if ((a.Y > point.Y) != (b.Y > point.Y) && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
            }
            return inside;
        }
        if (geometry is RectangleGeometry rectangle)
        {
            point = local;
            var r = rectangle.Rect; var rx = Math.Min(r.Width / 2, rectangle.RadiusX); var ry = Math.Min(r.Height / 2, rectangle.RadiusY);
            if (rx <= 0 || ry <= 0) return r.Contains(point);
            var x = Math.Max(0, Math.Abs(point.X - r.Center.X) - (r.Width / 2 - rx)) / rx;
            var y = Math.Max(0, Math.Abs(point.Y - r.Center.Y) - (r.Height / 2 - ry)) / ry;
            return x * x + y * y <= 1;
        }
        return geometry.FillContains(point);
    }
    protected override Size MeasureOverride(Size availableSize)
    {
        var desired = new Size(double.IsFinite(availableSize.Width) ? Math.Min(400, availableSize.Width) : 400, double.IsFinite(availableSize.Height) ? Math.Min(280, availableSize.Height) : 280);
        MeasureProfessionalPlotAreaContent(new(double.IsFinite(availableSize.Width) ? availableSize.Width : desired.Width, double.IsFinite(availableSize.Height) ? availableSize.Height : desired.Height));
        _dataTooltip?.Measure(new(double.IsFinite(availableSize.Width) ? availableSize.Width : desired.Width, double.IsFinite(availableSize.Height) ? availableSize.Height : desired.Height));
        MeasurePermanentPresentations(new(double.IsFinite(availableSize.Width) ? availableSize.Width : desired.Width, double.IsFinite(availableSize.Height) ? availableSize.Height : desired.Height));
        MeasureProfessionalTrackball(new(double.IsFinite(availableSize.Width) ? availableSize.Width : desired.Width, double.IsFinite(availableSize.Height) ? availableSize.Height : desired.Height));
        MeasureDataSelection(new(double.IsFinite(availableSize.Width) ? availableSize.Width : desired.Width, double.IsFinite(availableSize.Height) ? availableSize.Height : desired.Height));
        return desired;
    }
    protected override Size ArrangeOverride(Size finalSize)
    { if (_size != finalSize) { _size = finalSize; InvalidateAnalysis(); } var arranged = base.ArrangeOverride(finalSize); ArrangeProfessionalPlotAreaContent(finalSize); _dataTooltip?.Arrange(finalSize); ArrangePermanentPresentations(finalSize); ArrangeProfessionalLegacyTooltip(finalSize); ArrangeProfessionalTrackball(finalSize); ArrangeDataSelection(finalSize); return arranged; }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        DataSynchronizationPropertyChanged(change);
        if (DataSelectionPropertyChanged(change)) return;
        if (change.Property == ItemsSourceProperty || change.Property == IdPathProperty || change.Property == CategoryPathProperty || change.Property == ValuePathProperty)
            InvalidateDataSelection();
        // Input containment/focus changes do not alter the family data or view.
        // Any actual style change raises its own property notification.
        if (change.Property == IsPointerOverProperty || change.Property == IsFocusedProperty || change.Property == IsKeyboardFocusWithinProperty)
        { InvalidateVisual(); return; }
        if (TrackballPropertyChanged(change))
        {
            if (change.Property == TrackballModeProperty) { RefreshDataTooltip(); RefreshProfessionalLegacyTooltip(); }
            return;
        }
        if (PlotAreaContentPropertyChanged(change)) { RefreshProfessionalLegacyTooltip(); return; }
        RefreshProfessionalLegacyTooltip();
        if (PermanentPresentationPropertyChanged(change)) return;
        if (DataTooltipPropertyChanged(change)) return;
        if (change.Property == ActivePointProperty || change.Property == SelectedPointProperty) return;
        if (change.Property == ItemsSourceProperty) Series.ItemsSource = ItemsSource;
        else if (change.Property == CategoryPathProperty) Series.CategoryPath = CategoryPath;
        else if (change.Property == ValuePathProperty) Series.ValuePath = ValuePath;
        else if (change.Property == IdPathProperty) Series.IdPath = IdPath;
        else if (change.Property == SelectedIndexProperty)
        {
            // Publishing a followed identity changes its public index, not its shape.
            // Rebuilding geometry here would schedule an endless publish/render cycle.
            if (_updatingSelectedIndex && SelectedIndex == _publishingSelectedIndex) return;
            _followSelected = false;
        }
        InvalidateAnalysis();
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        var revision = ++_attachmentRevision; _attached = true; RefreshDataSynchronizationMembership(); InvalidateAnalysis(); base.OnAttachedToVisualTree(e);
        if (_attached && revision == _attachmentRevision) { _dataTooltip?.Attached(); _dataLegend?.Attached(); _dataLabels?.Attached(); ProfessionalPlotAreaContentAttached(); AttachedProfessionalTrackball(); }
        if (_attached && revision == _attachmentRevision) { DataSelectionAttached(); SynchronizeSeries(); }
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    { ++_attachmentRevision; _attached = false; RefreshDataSynchronizationMembership(); DataSelectionDetached(); ProfessionalPlotAreaContentDetached(); DetachedProfessionalTrackball(); _dataTooltip?.Detached(); _dataLegend?.Detached(); _dataLabels?.Detached(); ReleaseProfessionalLegacyTooltip(); SetActive(null); SynchronizeSeries(); base.OnDetachedFromVisualTree(e); InvalidateAnalysis(); }
    private void SynchronizeSeries()
    {
        _pending = true; if (_syncing) return; _syncing = true;
        try
        {
            while (_pending)
            {
                _pending = false;
                if (_attached)
                {
                    if (!_subscribed) { _subscribed = true; Series.Changed += SeriesChanged; }
                    if (_pending || !_attached) continue;
                    if (((ILogical)Series).LogicalParent is null)
                    {
                        ((ISetInheritanceParent)Series).SetParent(this);
                        if (_pending || !_attached) continue;
                        LogicalChildren.Add(Series);
                    }
                }
                else
                {
                    if (_subscribed) { _subscribed = false; Series.Changed -= SeriesChanged; }
                    if (_pending || _attached) continue;
                    if (ReferenceEquals(((ILogical)Series).LogicalParent, this)) LogicalChildren.Remove(Series);
                    else if (((ILogical)Series).LogicalParent is null) ((ISetInheritanceParent)Series).SetParent(null);
                }
            }
        }
        finally { _syncing = false; }
    }
    private void SeriesChanged(object? sender, EventArgs e) { _followSelected = _selected is not null; InvalidateAnalysis(); }
    protected void InvalidateAnalysis()
    {
        _dirty = true; _revision++; InvalidateDataSelection(); _dataTooltip?.InvalidateReading(); InvalidatePermanentPresentations(); InvalidateProfessionalPlotAreaContent(); InvalidateProfessionalTrackball();
        if (!_rendering) { InvalidateVisual(); return; }
        if (_queued) return; _queued = true;
        Dispatcher.UIThread.Post(() => { _queued = false; InvalidateVisual(); });
    }
    private void SetActive(ChartPointHit? hit)
    {
        if (_rendering) { _pendingActive = hit; _hasPendingActive = true; _stateDirty = true; QueueMarkState(); return; }
        SetAndRaise(ActivePointProperty, ref _active, hit);
    }
    private void SetSelected(ChartPointHit? hit) => SetAndRaise(SelectedPointProperty, ref _selected, hit);
    protected override void OnPointerMoved(PointerEventArgs e) { base.OnPointerMoved(e); var position = e.GetPosition(this); SetActive(HitTest(position)); UpdateProfessionalTrackball(position); InvalidateVisual(); }
    protected override void OnPointerExited(PointerEventArgs e) { base.OnPointerExited(e); ClearProfessionalTrackball(); SetActive(null); InvalidateVisual(); }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e); if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var hit = HitTest(e.GetPosition(this));
        var revision = _revision; var attachmentRevision = _attachmentRevision;
        SetActive(hit); Focus();
        if (revision != _revision || attachmentRevision != _attachmentRevision) { _ = GetMarks(); return; }
        if (IsSelectionEnabled) ApplySingleDataSelection(() => SetCurrentValue(SelectedIndexProperty, hit?.Index == SelectedIndex ? -1 : hit?.Index ?? -1));
        UpdateProfessionalTrackball(e.GetPosition(this)); InvalidateVisual();
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape) { ResetViewport(); e.Handled = true; return; }
        if (e.Key is Key.Enter or Key.Space && IsSelectionEnabled) { _ = GetMarks(); ApplySingleDataSelection(() => SetCurrentValue(SelectedIndexProperty, _active?.Index ?? -1)); e.Handled = true; return; }
        if (e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End)) return;
        var marks = GetMarks().Where(m => m.IsVisible && m.Fill is not null).ToArray(); if (marks.Length == 0) return;
        var index = Array.FindIndex(marks, m => m.Data.SourceIndex == _active?.Index);
        index = e.Key switch { Key.Home => 0, Key.End => marks.Length - 1, Key.Left or Key.Up => index < 0 ? marks.Length - 1 : (index + marks.Length - 1) % marks.Length, _ => (index + 1) % marks.Length };
        SetActive(Hit(marks[index])); if (_active is { } keyboardHit) UpdateProfessionalTrackball(keyboardHit.Position); e.Handled = true; InvalidateVisual();
    }
    public sealed override void Render(DrawingContext context)
    {
        if (_rendering) return;
        _rendering = true;
        try
        {
            if (!_attached && _permanentExportDepth == 0) _dirty = true;
            EnsureMarks();
            if (_stateDirty) QueueMarkState();
            var marks = _marks;
            var active = _active is null ? null : FindIdentity(_active);
            var selected = ResolveSelected();
            base.Render(context);
            using var clip = context.PushClip(new Rect(Bounds.Size));
            if (Background is { } bg) context.DrawRectangle(bg, null, new Rect(Bounds.Size));
            using (context.PushClip(_plot))
                foreach (var mark in marks.Where(m => m.IsVisible))
                {
                    var emphasized = mark.Data.SourceIndex == selected?.Index || mark.Data.SourceIndex == active?.Index;
                    context.DrawGeometry(mark.Fill, emphasized ? new Pen(Foreground ?? Brushes.Gray, 2) : null, mark.Geometry);
                }
            if (!string.IsNullOrEmpty(Title)) DrawLabel(context, Title, new(Bounds.Width / 2, Padding.Top + 8 + FontSize * .8), Math.Max(0, Bounds.Width - Padding.Left - Padding.Right - 16));
            foreach (var mark in marks.Where(m => m.IsVisible))
            {
                if (UsesDataLabelTemplate || !ShowLabels && !ShowValues) continue;
                var label = mark.Label ?? string.Join(" · ", new[] { ShowLabels ? Convert.ToString(mark.Data.Category, CultureInfo.CurrentCulture) : null, ShowValues ? FormatValue(mark.Data.Point.Y) : null }.Where(s => !string.IsNullOrEmpty(s)));
                if (mark.Bounds.Height >= FontSize * 1.2) DrawLabel(context, label, mark.LabelPosition, Math.Max(0, mark.Bounds.Width - 4),
                    mark.LabelBrush ?? ChartLabelContrast.OnFill(mark.Fill, Foreground ?? Brushes.Gray));
            }
            DrawDecorations(context, marks);
            if (!SuppressTransientTooltip && IsTooltipEnabled && active is { } hit)
            {
                var mark = marks.FirstOrDefault(m => m.Data.SourceIndex == hit.Index);
                DrawLabel(context, mark?.TooltipText ?? $"{mark?.Data.Category}: {FormatValue(hit.Point.Y)}", new(Bounds.Width / 2, Math.Max(FontSize, Bounds.Height - FontSize)), Math.Max(0, Bounds.Width - 16));
            }
        }
        finally { _rendering = false; if (_stateDirty || _dirty) QueueMarkState(); DataTooltipRenderFinished(); PermanentPresentationRenderFinished(); ProfessionalPlotAreaContentRenderFinished(); ProfessionalTrackballRenderFinished(); DataSelectionRenderFinished(); }
    }
}
