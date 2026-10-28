using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;

namespace NT.AvaloniaControl.Charts;

/// <summary>A painted engineering glyph with every original contributing observation.</summary>
public sealed record EngineeringMarkInfo(IReadOnlyList<ChartDataPoint> Members, Geometry Geometry, IBrush? Fill, Point Position)
{
    public object? Key { get; init; }
    public double Value { get; init; }
    public string? TooltipText { get; init; }
    public IBrush? Stroke { get; init; }
    public Rect Bounds => Geometry.Bounds;
}

/// <summary>An engineering glyph hit. Index is a glyph ordinal; Members contains the real source indices.</summary>
public sealed record EngineeringPointHit(int Index, EngineeringMarkInfo Mark)
{
    public Point Position => Mark.Position;
    public IReadOnlyList<ChartDataPoint> Members => Mark.Members;
}

/// <summary>One projected engineering grid curve; curves are never selectable observations.</summary>
public sealed record EngineeringGridLine(Geometry Geometry, string? Label = null, Point? LabelPosition = null);

/// <summary>Observable engineering data, source-preserving interaction, navigation and raster export.</summary>
public abstract partial class EngineeringChartBase : ChartBase
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty = AvaloniaProperty.Register<EngineeringChartBase, IEnumerable?>(nameof(ItemsSource));
    public static readonly StyledProperty<string?> IdPathProperty = AvaloniaProperty.Register<EngineeringChartBase, string?>(nameof(IdPath));
    public static readonly StyledProperty<string?> LabelPathProperty = AvaloniaProperty.Register<EngineeringChartBase, string?>(nameof(LabelPath));
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<EngineeringChartBase, string?>(nameof(Title));
    public static readonly StyledProperty<bool> ShowGridProperty = AvaloniaProperty.Register<EngineeringChartBase, bool>(nameof(ShowGrid), true);
    public static readonly StyledProperty<IBrush?> GridLineBrushProperty = AvaloniaProperty.Register<EngineeringChartBase, IBrush?>(nameof(GridLineBrush));
    public static readonly StyledProperty<bool> IsTooltipEnabledProperty = AvaloniaProperty.Register<EngineeringChartBase, bool>(nameof(IsTooltipEnabled), true);
    public static readonly StyledProperty<bool> IsSelectionEnabledProperty = AvaloniaProperty.Register<EngineeringChartBase, bool>(nameof(IsSelectionEnabled), true);
    public static readonly StyledProperty<int> SelectedIndexProperty = AvaloniaProperty.Register<EngineeringChartBase, int>(nameof(SelectedIndex), -1, defaultBindingMode: BindingMode.TwoWay, validate: x => x >= -1);
    public static readonly StyledProperty<double> MarkerSizeProperty = AvaloniaProperty.Register<EngineeringChartBase, double>(nameof(MarkerSize), 8, validate: x => double.IsFinite(x) && x >= 0 && x <= 1024);
    public static readonly StyledProperty<double> ZoomProperty = AvaloniaProperty.Register<EngineeringChartBase, double>(nameof(Zoom), 1, validate: x => double.IsFinite(x) && x >= 1 && x <= 32);
    public static readonly StyledProperty<double> PanXProperty = AvaloniaProperty.Register<EngineeringChartBase, double>(nameof(PanX), 0, validate: x => double.IsFinite(x) && Math.Abs(x) <= 32);
    public static readonly StyledProperty<double> PanYProperty = AvaloniaProperty.Register<EngineeringChartBase, double>(nameof(PanY), 0, validate: x => double.IsFinite(x) && Math.Abs(x) <= 32);
    public static readonly StyledProperty<IReadOnlyList<IBrush>?> PaletteProperty = AvaloniaProperty.Register<EngineeringChartBase, IReadOnlyList<IBrush>?>(nameof(Palette));
    public static readonly DirectProperty<EngineeringChartBase, EngineeringPointHit?> ActivePointProperty = AvaloniaProperty.RegisterDirect<EngineeringChartBase, EngineeringPointHit?>(nameof(ActivePoint), c => c.ActivePoint);
    public static readonly DirectProperty<EngineeringChartBase, EngineeringPointHit?> SelectedPointProperty = AvaloniaProperty.RegisterDirect<EngineeringChartBase, EngineeringPointHit?>(nameof(SelectedPoint), c => c.SelectedPoint);
    public static readonly DirectProperty<EngineeringChartBase, Exception?> DataErrorProperty = AvaloniaProperty.RegisterDirect<EngineeringChartBase, Exception?>(nameof(DataError), c => c.DataError);
    private static readonly IBrush[] DefaultPalette = [Brush.Parse("#1598B6"), Brush.Parse("#D25573"), Brush.Parse("#55B47A"), Brush.Parse("#E4AB47")];
    private readonly AnalysisSeries _series = new();
    private bool _attached, _subscribed, _dirty = true, _building, _rendering, _queued, _publishing, _settingIndex, _explicitSelectionPending;
    private bool _syncing, _pending, _resetRequested, _hasPendingActive, _synchronizingSource, _sourcePending;
    private long _revision, _attachmentRevision;
    private Size _size;
    private Rect _plot;
    private IReadOnlyList<EngineeringMarkInfo> _marks = [];
    private IReadOnlyList<EngineeringGridLine> _grid = [];
    private EngineeringPointHit? _active, _selected, _pendingActive;
    private Exception? _dataError, _pendingDataError;
    private BudgetedSource? _source;
    private Dictionary<object, int> _identityMarks = new(ReferenceEqualityComparer.Instance);
    private int _publishingIndex = -1;
    private Point? _drag;
    private ChartRasterExporter? _exporter;

    protected EngineeringChartBase() { Focusable = true; ClipToBounds = true; }
    protected override Type StyleKeyOverride => typeof(EngineeringChartBase);
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public string? IdPath { get => GetValue(IdPathProperty); set => SetValue(IdPathProperty, value); }
    public string? LabelPath { get => GetValue(LabelPathProperty); set => SetValue(LabelPathProperty, value); }
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public bool ShowGrid { get => GetValue(ShowGridProperty); set => SetValue(ShowGridProperty, value); }
    public IBrush? GridLineBrush { get => GetValue(GridLineBrushProperty); set => SetValue(GridLineBrushProperty, value); }
    public bool IsTooltipEnabled { get => GetValue(IsTooltipEnabledProperty); set => SetValue(IsTooltipEnabledProperty, value); }
    public bool IsSelectionEnabled { get => GetValue(IsSelectionEnabledProperty); set => SetValue(IsSelectionEnabledProperty, value); }
    public int SelectedIndex { get => GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }
    public double MarkerSize { get => GetValue(MarkerSizeProperty); set => SetValue(MarkerSizeProperty, value); }
    public double Zoom { get => GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }
    /// <summary>Horizontal translation as a fraction of the plot width.</summary>
    public double PanX { get => GetValue(PanXProperty); set => SetValue(PanXProperty, value); }
    /// <summary>Vertical translation as a fraction of the plot height.</summary>
    public double PanY { get => GetValue(PanYProperty); set => SetValue(PanYProperty, value); }
    public IReadOnlyList<IBrush>? Palette { get => GetValue(PaletteProperty); set => SetValue(PaletteProperty, value); }
    public Rect PlotArea { get { _ = GetMarks(); return _plot; } }
    public EngineeringPointHit? ActivePoint { get { _ = GetMarks(); return _active; } }
    /// <summary>Automatic rebuilds follow the first former member still present, in former member order.
    /// A split chooses that member's current bin; removing every member clears the selection.</summary>
    public EngineeringPointHit? SelectedPoint { get { _ = GetMarks(); return _selected; } }
    /// <summary>Last source, projection-budget or numeric data error. Notifications publish outside Render.
    /// Failed frames have no marks or grid; a later valid source clears this error.</summary>
    public Exception? DataError { get { _ = GetMarks(); return _dataError; } }
    /// <summary>Refreshes mutable sources that do not implement change notification.</summary>
    public void Refresh() { VerifyAccess(); OnDataInvalidated(); _series.Refresh(); InvalidateEngineering(); }
    /// <summary>Invalidates a family's immutable data projection independently of its displayed geometry.</summary>
    protected virtual void OnDataInvalidated() { }
    /// <summary>Maximum root observations enumerated before a family's numeric projection.</summary>
    protected virtual int SourceItemLimit => ChartEngineering.MaximumCount;
    protected void ObservePaths(params string?[] paths) { _series.ExtraPaths = paths; _series.ValuePath = paths.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p)); _series.Refresh(); }
    protected abstract IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot);
    /// <summary>Allows families with their own identity validation to capture original root items before numeric mapping.</summary>
    protected virtual ChartDataSnapshot CreateEngineeringSnapshot() => _series.CreateSnapshot();
    protected virtual IReadOnlyList<EngineeringGridLine> BuildGridLines(Rect plot) => [];
    /// <summary>Allows compound glyph families to retain a side or region when following a source member.</summary>
    protected virtual EngineeringPointHit? ResolveFollowedMark(EngineeringPointHit previous, IReadOnlyList<EngineeringMarkInfo> marks, EngineeringPointHit? proposed) => proposed;
    /// <summary>Paints nonselectable family labels and decorations outside the selectable geometry collection.</summary>
    protected virtual void RenderAnnotations(DrawingContext context, Rect plot) { }
    /// <summary>Paints nonselectable family backgrounds inside the plot, beneath data marks.</summary>
    protected virtual void RenderUnderlay(DrawingContext context, Rect plot) { }
    /// <summary>Paints family data marks. Compound families may interleave nonselectable decorations while retaining this interaction collection.</summary>
    protected virtual void RenderEngineeringMarks(DrawingContext context, IReadOnlyList<EngineeringMarkInfo> marks, EngineeringPointHit? selected, EngineeringPointHit? active)
    {
        for (var i = 0; i < marks.Count; i++)
        { var mark = marks[i]; context.DrawGeometry(mark.Fill, new Pen(i == selected?.Index || i == active?.Index ? Foreground ?? Brushes.Gray : mark.Stroke ?? mark.Fill ?? Brushes.Gray, i == selected?.Index || i == active?.Index ? 2 : 1), mark.Geometry); }
    }
    protected virtual void ClearProjection(Exception error) { }
    protected virtual Rect GetPlotArea(Size size)
    {
        var left = Math.Clamp(Padding.Left + 24, 0, size.Width);
        var top = Math.Clamp(Padding.Top + 24 + (string.IsNullOrEmpty(Title) ? 0 : FontSize * 1.6 + 6), 0, size.Height);
        var right = Math.Clamp(size.Width - Padding.Right - 24, left, size.Width);
        var bottom = Math.Clamp(size.Height - Padding.Bottom - 24, top, size.Height);
        return new(left, top, right - left, bottom - top);
    }
    protected static Rect Square(Rect plot)
    { var side = Math.Min(plot.Width, plot.Height); return new(plot.Center.X - side / 2, plot.Center.Y - side / 2, side, side); }
    protected IBrush BrushAt(int index) => Palette is { Count: > 0 } p ? p[Math.Abs(index % p.Count)] : DefaultPalette[Math.Abs(index % DefaultPalette.Length)];
    protected Point ApplyViewport(Point point, Rect plot)
    {
        var result = new Point(plot.Center.X + (point.X - plot.Center.X) * Zoom + PanX * plot.Width, plot.Center.Y + (point.Y - plot.Center.Y) * Zoom + PanY * plot.Height);
        if (!double.IsFinite(result.X) || !double.IsFinite(result.Y)) throw new OverflowException("The screen projection cannot be represented by finite DIPs.");
        return result;
    }
    protected Point RemoveViewport(Point point, Rect plot) => new(plot.Center.X + (point.X - plot.Center.X - PanX * plot.Width) / Zoom, plot.Center.Y + (point.Y - plot.Center.Y - PanY * plot.Height) / Zoom);
    protected static bool Number(object? item, string? path, out double value) { value = double.NaN; return !string.IsNullOrWhiteSpace(path) && ChartDataAccessor.TryRead(item, path, out var raw) && ChartDataAccessor.TryNumber(raw, out value); }
    protected static string F(double value) => double.IsFinite(value) ? value.ToString("G5", CultureInfo.InvariantCulture) : "—";
    protected static double Fraction(double value, double minimum, double maximum)
    {
        if (minimum == maximum) return .5;
        var scale = Math.Max(Math.Abs(minimum), Math.Abs(maximum));
        return (value / scale - minimum / scale) / (maximum / scale - minimum / scale);
    }
    protected static double Interpolate(double minimum, double maximum, double fraction) => (1 - fraction) * minimum + fraction * maximum;
    protected Point CartesianToPlot(ChartPoint point, ChartSeriesBounds bounds, Rect plot)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y)) throw new ArgumentOutOfRangeException(nameof(point), "Coordinates must be finite.");
        return ApplyViewport(new(plot.Left + Fraction(point.X, bounds.XMinimum, bounds.XMaximum) * plot.Width, plot.Bottom - Fraction(point.Y, bounds.YMinimum, bounds.YMaximum) * plot.Height), plot);
    }
    protected ChartPoint PlotToCartesian(Point point, ChartSeriesBounds bounds, Rect plot)
    {
        if (plot.Width <= 0 || plot.Height <= 0) throw new InvalidOperationException("The chart must have a nonempty arranged plot.");
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y)) throw new ArgumentOutOfRangeException(nameof(point), "Coordinates must be finite.");
        var local = RemoveViewport(point, plot);
        var result = new ChartPoint(Interpolate(bounds.XMinimum, bounds.XMaximum, (local.X - plot.Left) / plot.Width), Interpolate(bounds.YMinimum, bounds.YMaximum, (plot.Bottom - local.Y) / plot.Height));
        if (!double.IsFinite(result.X) || !double.IsFinite(result.Y)) throw new OverflowException("The inverse screen projection cannot be represented.");
        return result;
    }
    protected static Geometry Line(IEnumerable<Point> points) => new PolylineGeometry(points.ToArray(), false);
    protected EngineeringMarkInfo Marker(ChartDataPoint source, Point position, string tooltip, double value, int color = 0) =>
        new(Array.AsReadOnly(new[] { source }), new EllipseGeometry(new Rect(position.X - MarkerSize / 2, position.Y - MarkerSize / 2, MarkerSize, MarkerSize)), BrushAt(color), position)
        { Key = source.StableId, Value = value, TooltipText = tooltip };
    public IReadOnlyList<EngineeringMarkInfo> GetMarks()
    {
        VerifyAccess();
        if (_building || _rendering) return _marks;
        if (!_attached && _permanentExportDepth == 0) _dirty = true;
        EnsureMarks(); PublishState(); if (!_dirty) RefreshDataSelection(); return _marks;
    }
    public IReadOnlyList<EngineeringGridLine> GetGridLines() { _ = GetMarks(); return _grid; }
    private void EnsureMarks()
    {
        if (!_dirty || _building) return;
        _building = true;
        try
        {
            var revision = _revision; _dirty = false;
            _plot = ReserveSharedLegendArea(GetPlotArea(_size.Width > 0 && _size.Height > 0 ? _size : Bounds.Size));
            var data = CreateEngineeringSnapshot();
            if (_source?.Error is { } sourceError) throw sourceError;
            if (data.Count > SourceItemLimit) throw new ArgumentOutOfRangeException(nameof(ItemsSource), "Source exceeds the chart observation budget.");
            _marks = Array.AsReadOnly(BuildMarks(data, _plot).ToArray());
            _grid = Array.AsReadOnly(BuildGridLines(_plot).ToArray());
            _pendingDataError = null;
            BuildIdentityIndex();
            if (revision != _revision) _dirty = true;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException)
        {
            _pendingDataError = error; _marks = []; _grid = []; _identityMarks.Clear();
            _hasPendingActive = true; _pendingActive = null; ClearProjection(error);
        }
        catch { _dirty = true; throw; }
        finally { _building = false; }
    }
    private EngineeringPointHit? At(int index) => index >= 0 && index < _marks.Count ? new(index, _marks[index]) : null;
    private void BuildIdentityIndex()
    {
        _identityMarks = string.IsNullOrWhiteSpace(IdPath) ? new(ReferenceEqualityComparer.Instance) : new();
        for (var index = 0; index < _marks.Count; index++)
            foreach (var member in _marks[index].Members)
                if (member.StableId is { } identity) _identityMarks.TryAdd(identity, index);
    }
    private EngineeringPointHit? Follow(EngineeringPointHit? hit)
    {
        if (hit is null) return null;
        return FindFollowedMark(hit, _marks);
    }
    /// <summary>Finds an original source in the current frame. Hierarchies can distinguish explicit IDs from source references.</summary>
    protected virtual EngineeringPointHit? FindFollowedMark(EngineeringPointHit hit, IReadOnlyList<EngineeringMarkInfo> marks)
    {
        foreach (var member in hit.Members)
                if (member.StableId is { } identity && _identityMarks.TryGetValue(identity, out var index)) return ResolveFollowedMark(hit, marks, new(index, marks[index]));
        return null;
    }
    private EngineeringPointHit? ResolveSelected() => _explicitSelectionPending || _selected is null ? At(SelectedIndex) : Follow(_selected);
    private void PublishState()
    {
        if (_rendering || _building || _publishing || _dirty) { QueueState(); return; }
        _publishing = true;
        try
        {
            if (_resetRequested) { _resetRequested = false; ResetViewport(); QueueState(); return; }
            var revision = _revision;
            var selected = ResolveSelected();
            var active = Follow(_hasPendingActive ? _pendingActive : _active);
            _hasPendingActive = false; _pendingActive = null;
            if (!Same(_active, active)) SetActive(active);
            if (revision != _revision) { QueueState(); return; }
            _settingIndex = true;
            _publishingIndex = selected?.Index ?? -1;
            try { SetCurrentValue(SelectedIndexProperty, _publishingIndex); }
            finally { _settingIndex = false; }
            if (revision != _revision) { QueueState(); return; }
            if (!Same(_selected, selected)) SetAndRaise(SelectedPointProperty, ref _selected, selected);
            if (revision != _revision) { QueueState(); return; }
            if (!SameError(_dataError, _pendingDataError)) SetAndRaise(DataErrorProperty, ref _dataError, _pendingDataError);
            if (revision != _revision) QueueState(); else _explicitSelectionPending = false;
        }
        finally { _publishing = false; }
    }
    private static bool Same(EngineeringPointHit? a, EngineeringPointHit? b) => a?.Index == b?.Index && ReferenceEquals(a?.Mark, b?.Mark);
    private static bool SameError(Exception? a, Exception? b) => a?.GetType() == b?.GetType() && a?.Message == b?.Message;
    private void SetActive(EngineeringPointHit? hit)
    {
        if (_rendering || _building) { _hasPendingActive = true; _pendingActive = hit; QueueState(); return; }
        SetAndRaise(ActivePointProperty, ref _active, hit);
    }
    private void QueueState()
    {
        if (_queued) return; _queued = true;
        Dispatcher.UIThread.Post(() => { _queued = false; if (!_rendering && !_building) _ = GetMarks(); });
    }
    public EngineeringPointHit? HitTest(Point position)
    {
        var marks = GetMarks();
        if (!new Rect(Bounds.Size).Contains(position) || !_plot.Contains(position)) return null;
        for (var i = marks.Count - 1; i >= 0; i--)
            if (marks[i].Fill is not null && ContainsMark(marks[i], position)) return new(i, marks[i]);
        return null;
    }
    /// <summary>Tests a displayed family mark against its exact primitive shape.</summary>
    protected virtual bool ContainsMark(EngineeringMarkInfo mark, Point point) => Contains(mark.Geometry, point);
    private static bool Contains(Geometry geometry, Point point)
    {
        // Evaluate compound regions from their primitives. Headless backends can expose
        // only the combined bounds through FillContains, which would select overlapping
        // Venn atoms as if every atom were the enclosing circle.
        if (geometry is CombinedGeometry combined)
        {
            bool Child(Geometry? child) => child is not null && Contains(child, point);
            return combined.GeometryCombineMode switch
            {
                GeometryCombineMode.Union => Child(combined.Geometry1) || Child(combined.Geometry2),
                GeometryCombineMode.Intersect => Child(combined.Geometry1) && Child(combined.Geometry2),
                GeometryCombineMode.Exclude => Child(combined.Geometry1) && !Child(combined.Geometry2),
                GeometryCombineMode.Xor => Child(combined.Geometry1) != Child(combined.Geometry2),
                _ => false,
            };
        }
        if (!geometry.Bounds.Contains(point)) return false;
        if (geometry is EllipseGeometry ellipse)
        {
            var rect = ellipse.Rect; if (rect.Width <= 0 || rect.Height <= 0) return false;
            var x = (point.X - rect.Center.X) / (rect.Width / 2); var y = (point.Y - rect.Center.Y) / (rect.Height / 2);
            return x * x + y * y <= 1;
        }
        if (geometry is PolylineGeometry polygon)
        {
            var vertices = polygon.Points; var inside = false;
            for (int i = 0, j = vertices.Count - 1; i < vertices.Count; j = i++)
            {
                var a = vertices[j]; var b = vertices[i];
                var cross = (point.X - a.X) * (b.Y - a.Y) - (point.Y - a.Y) * (b.X - a.X);
                if (Math.Abs(cross) < 1e-8 && point.X >= Math.Min(a.X, b.X) && point.X <= Math.Max(a.X, b.X) && point.Y >= Math.Min(a.Y, b.Y) && point.Y <= Math.Max(a.Y, b.Y)) return true;
                if ((a.Y > point.Y) != (b.Y > point.Y) && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
            }
            return inside;
        }
        return geometry.FillContains(point);
    }
    public override void ResetViewport()
    {
        ClearProfessionalTrackball(); ClearDataSelection();
        if (_rendering || _building) { _resetRequested = true; QueueState(); return; }
        SetCurrentValue(ZoomProperty, 1); SetCurrentValue(PanXProperty, 0); SetCurrentValue(PanYProperty, 0);
        _explicitSelectionPending = true; SetCurrentValue(SelectedIndexProperty, -1);
        SetActive(null);
    }
    protected void InvalidateEngineering() { _dirty = true; _revision++; InvalidateDataSelection(); _dataTooltip?.InvalidateReading(); InvalidatePermanentPresentations(); InvalidateProfessionalPlotAreaContent(); InvalidateProfessionalTrackball(); if (_rendering) QueueState(); else InvalidateVisual(); }
    protected override Size MeasureOverride(Size availableSize)
    {
        var desired = new Size(double.IsFinite(availableSize.Width) ? Math.Min(420, availableSize.Width) : 420, double.IsFinite(availableSize.Height) ? Math.Min(280, availableSize.Height) : 280);
        MeasureProfessionalPlotAreaContent(new(double.IsFinite(availableSize.Width) ? availableSize.Width : desired.Width, double.IsFinite(availableSize.Height) ? availableSize.Height : desired.Height));
        _dataTooltip?.Measure(new(double.IsFinite(availableSize.Width) ? availableSize.Width : desired.Width, double.IsFinite(availableSize.Height) ? availableSize.Height : desired.Height));
        MeasurePermanentPresentations(new(double.IsFinite(availableSize.Width) ? availableSize.Width : desired.Width, double.IsFinite(availableSize.Height) ? availableSize.Height : desired.Height));
        MeasureProfessionalTrackball(new(double.IsFinite(availableSize.Width) ? availableSize.Width : desired.Width, double.IsFinite(availableSize.Height) ? availableSize.Height : desired.Height));
        MeasureDataSelection(new(double.IsFinite(availableSize.Width) ? availableSize.Width : desired.Width, double.IsFinite(availableSize.Height) ? availableSize.Height : desired.Height));
        return desired;
    }
    protected override Size ArrangeOverride(Size finalSize)
    { if (_size != finalSize) { _size = finalSize; InvalidateEngineering(); } var arranged = base.ArrangeOverride(finalSize); ArrangeProfessionalPlotAreaContent(finalSize); _dataTooltip?.Arrange(finalSize); ArrangePermanentPresentations(finalSize); ArrangeProfessionalLegacyTooltip(finalSize); ArrangeProfessionalTrackball(finalSize); ArrangeDataSelection(finalSize); return arranged; }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        DataSynchronizationPropertyChanged(change);
        if (DataSelectionPropertyChanged(change)) return;
        // Family status notifications describe completed work. Rebuilding the
        // source for a new error object would schedule another error notification
        // and keep the selection refresh dispatcher busy indefinitely.
        if (change.Property.IsDirect && change.Property.IsReadOnly && change.Property.OwnerType.IsSubclassOf(typeof(EngineeringChartBase))) return;
        if (change.Property == ItemsSourceProperty || change.Property == IdPathProperty || change.Property == LabelPathProperty)
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
        if (change.Property == ActivePointProperty || change.Property == SelectedPointProperty || change.Property == DataErrorProperty) return;
        if (change.Property == ItemsSourceProperty) { OnDataInvalidated(); SynchronizeSource(); }
        else if (change.Property == IdPathProperty) { OnDataInvalidated(); _series.IdPath = IdPath; }
        else if (change.Property == LabelPathProperty) { OnDataInvalidated(); _series.CategoryPath = LabelPath; }
        else if (change.Property == SelectedIndexProperty)
        {
            if (_settingIndex && SelectedIndex == _publishingIndex) return;
            _explicitSelectionPending = true;
            InvalidateEngineering();
            // Capture the chosen scene's original members before a subsequent resize/rebin.
            // Initializers have no arranged scene yet; paint/build/publication callbacks
            // retain the deferred path so binding reentry cannot recurse into publication.
            if (!_rendering && !_building && !_publishing && _size.Width > 0 && _size.Height > 0) _ = GetMarks();
            else if (_size.Width > 0 && _size.Height > 0) QueueState();
            return;
        }
        InvalidateEngineering();
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        var revision = ++_attachmentRevision; _attached = true; RefreshDataSynchronizationMembership(); InvalidateEngineering(); base.OnAttachedToVisualTree(e);
        if (_attached && revision == _attachmentRevision) { _dataTooltip?.Attached(); _dataLegend?.Attached(); _dataLabels?.Attached(); ProfessionalPlotAreaContentAttached(); AttachedProfessionalTrackball(); }
        if (_attached && revision == _attachmentRevision) { DataSelectionAttached(); SynchronizeSeries(); }
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ++_attachmentRevision; _attached = false; RefreshDataSynchronizationMembership(); DataSelectionDetached(); ProfessionalPlotAreaContentDetached(); DetachedProfessionalTrackball(); _dataTooltip?.Detached(); _dataLegend?.Detached(); _dataLabels?.Detached(); ReleaseProfessionalLegacyTooltip(); SetActive(null); SynchronizeSeries(); base.OnDetachedFromVisualTree(e); InvalidateEngineering();
    }
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
                    if (!_subscribed) { _subscribed = true; _series.Changed += SeriesChanged; }
                    if (_pending || !_attached) continue;
                    if (((ILogical)_series).LogicalParent is null)
                    { ((ISetInheritanceParent)_series).SetParent(this); if (_pending || !_attached) continue; LogicalChildren.Add(_series); }
                }
                else
                {
                    if (_subscribed) { _subscribed = false; _series.Changed -= SeriesChanged; }
                    if (_pending || _attached) continue;
                    if (ReferenceEquals(((ILogical)_series).LogicalParent, this)) LogicalChildren.Remove(_series);
                    else if (((ILogical)_series).LogicalParent is null) ((ISetInheritanceParent)_series).SetParent(null);
                }
            }
        }
        finally { _syncing = false; if (_sourcePending) SynchronizeSource(); }
    }
    private void SynchronizeSource()
    {
        _sourcePending = true;
        // An external collection event accessor can change ItemsSource during series observation.
        // Let that accessor and the current series operation finish before replacing its source.
        if (_synchronizingSource || _syncing) return;
        _synchronizingSource = true;
        try
        {
            while (_sourcePending)
            {
                _sourcePending = false;
                _source = ItemsSource is { } source ? new BudgetedSource(source, SourceItemLimit) : null;
                _series.ItemsSource = _source;
            }
        }
        finally { _synchronizingSource = false; }
    }
    private void SeriesChanged(object? sender, EventArgs e)
    {
        // Mapping and source changes are published as data notifications. A
        // property notification here describes the private series' appearance
        // or inherited context; it must not turn a theme change into new data.
        if (e is not AvaloniaPropertyChangedEventArgs) OnDataInvalidated();
        InvalidateEngineering();
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e); var p = e.GetPosition(this);
        if (_drag is { } start && e.GetCurrentPoint(this).Properties.IsMiddleButtonPressed)
        { if (_plot.Width > 0 && _plot.Height > 0) { SetCurrentValue(PanXProperty, Math.Clamp(PanX + (p.X - start.X) / _plot.Width, -32, 32)); SetCurrentValue(PanYProperty, Math.Clamp(PanY + (p.Y - start.Y) / _plot.Height, -32, 32)); } _drag = p; return; }
        SetActive(HitTest(p)); UpdateProfessionalTrackball(p); InvalidateVisual();
    }
    protected override void OnPointerExited(PointerEventArgs e) { base.OnPointerExited(e); ClearProfessionalTrackball(); SetActive(null); InvalidateVisual(); }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e); var p = e.GetPosition(this); var properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsMiddleButtonPressed) { ClearProfessionalTrackball(); _drag = p; e.Pointer.Capture(this); return; }
        if (!properties.IsLeftButtonPressed) return;
        var hit = HitTest(p); var revision = _revision; var attachment = _attachmentRevision;
        SetActive(hit); Focus();
        if (revision != _revision || attachment != _attachmentRevision) { _ = GetMarks(); return; }
        if (IsSelectionEnabled) ApplySingleDataSelection(() => SetCurrentValue(SelectedIndexProperty, hit?.Index == SelectedIndex ? -1 : hit?.Index ?? -1)); UpdateProfessionalTrackball(p); InvalidateVisual();
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e) { base.OnPointerReleased(e); _drag = null; e.Pointer.Capture(null); }
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e); if (!PlotArea.Contains(e.GetPosition(this))) return;
        SetCurrentValue(ZoomProperty, Math.Clamp(Zoom * Math.Pow(1.15, e.Delta.Y), 1, 32)); e.Handled = true;
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape) { ResetViewport(); e.Handled = true; return; }
        if (e.Key is Key.Enter or Key.Space && IsSelectionEnabled) { _ = GetMarks(); ApplySingleDataSelection(() => SetCurrentValue(SelectedIndexProperty, _active?.Index ?? -1)); e.Handled = true; return; }
        if (e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End)) return;
        var marks = GetMarks(); if (marks.Count == 0) return;
        var i = _active?.Index ?? -1;
        i = e.Key switch { Key.Home => 0, Key.End => marks.Count - 1, Key.Left or Key.Up => i < 0 ? marks.Count - 1 : (i + marks.Count - 1) % marks.Count, _ => (i + 1) % marks.Count };
        SetActive(At(i)); if (_active is { } keyboardHit) UpdateProfessionalTrackball(keyboardHit.Position); e.Handled = true; InvalidateVisual();
    }
    protected void DrawLabel(DrawingContext context, string text, Point center, double maxWidth)
        => DrawLabel(context, text, center, maxWidth, Foreground ?? Brushes.Gray);
    protected void DrawLabel(DrawingContext context, string text, Point center, double maxWidth, IBrush brush)
    {
        if (string.IsNullOrEmpty(text) || maxWidth <= 0 || Bounds.Width <= 0 || Bounds.Height <= 0) return;
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(FontFamily, FontStyle, FontWeight), FontSize, brush)
        { MaxTextWidth = Math.Max(.01, maxWidth), MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis };
        context.DrawText(formatted, new(Math.Clamp(center.X - formatted.Width / 2, 0, Math.Max(0, Bounds.Width - formatted.Width)), Math.Clamp(center.Y - formatted.Height / 2, 0, Math.Max(0, Bounds.Height - formatted.Height))));
    }
    public sealed override void Render(DrawingContext context)
    {
        if (_rendering) return; _rendering = true;
        try
        {
            if (!_attached && _permanentExportDepth == 0) _dirty = true; EnsureMarks(); base.Render(context);
            using var clip = context.PushClip(new Rect(Bounds.Size));
            if (Background is { } background) context.DrawRectangle(background, null, new Rect(Bounds.Size));
            using (context.PushClip(_plot))
            {
                RenderUnderlay(context, _plot);
                if (ShowGrid) foreach (var grid in _grid) context.DrawGeometry(null, new Pen(GridLineBrush ?? Brush.Parse("#70909CA9"), 1), grid.Geometry);
                var selected = ResolveSelected(); var active = Follow(_active);
                RenderEngineeringMarks(context, _marks, selected, active);
            }
            if (!string.IsNullOrEmpty(Title)) DrawLabel(context, Title, new(Bounds.Width / 2, Padding.Top + FontSize), Bounds.Width - 16);
            if (_pendingDataError is { } error) DrawLabel(context, error.Message, new(Bounds.Width / 2, Bounds.Height / 2), Math.Max(0, Bounds.Width - 24));
            if (ShowGrid) foreach (var line in _grid) if (line.Label is { } label && line.LabelPosition is { } p) DrawLabel(context, label, p, 64);
            RenderAnnotations(context, _plot);
            if (!SuppressTransientTooltip && IsTooltipEnabled && Follow(_active) is { } hit) DrawLabel(context, hit.Mark.TooltipText ?? $"{hit.Members.Count} observations: {F(hit.Mark.Value)}", new(Bounds.Width / 2, Bounds.Height - FontSize), Bounds.Width - 16);
        }
        finally { _rendering = false; QueueState(); DataTooltipRenderFinished(); PermanentPresentationRenderFinished(); ProfessionalPlotAreaContentRenderFinished(); ProfessionalTrackballRenderFinished(); DataSelectionRenderFinished(); }
    }

    private sealed class BudgetedSource(IEnumerable source, int limit) : IEnumerable, INotifyCollectionChanged
    {
        private NotifyCollectionChangedEventHandler? _changed;
        private bool _truncated, _sourceSubscribed, _synchronizingSubscription, _subscriptionPending;
        public Exception? Error { get; private set; }
        public event NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add { _changed += value; SynchronizeSubscription(); }
            remove { _changed -= value; SynchronizeSubscription(); }
        }
        private void SynchronizeSubscription()
        {
            _subscriptionPending = true;
            if (_synchronizingSubscription || source is not INotifyCollectionChanged observable) return;
            _synchronizingSubscription = true;
            try
            {
                while (_subscriptionPending)
                {
                    _subscriptionPending = false;
                    // Application event accessors may replace/detach the chart before returning.
                    // Record intent first, and reconcile again after the accessor finishes so an
                    // add that completes after a reentrant remove cannot retain an old source.
                    if (_changed is not null && !_sourceSubscribed)
                    { _sourceSubscribed = true; observable.CollectionChanged += SourceChanged; _subscriptionPending = true; }
                    else if (_changed is null && _sourceSubscribed)
                    { _sourceSubscribed = false; observable.CollectionChanged -= SourceChanged; _subscriptionPending = true; }
                }
            }
            finally { _synchronizingSubscription = false; }
        }
        private void SourceChanged(object? sender, NotifyCollectionChangedEventArgs args)
        { var reset = _truncated || Error is not null || limit < ChartEngineering.MaximumCount; Error = null; _changed?.Invoke(this, reset ? new(NotifyCollectionChangedAction.Reset) : args); }
        public IEnumerator GetEnumerator()
        {
            Error = null; _truncated = false; var count = 0; IEnumerator? iterator = null;
            try { iterator = source.GetEnumerator(); }
            catch (Exception error) when (error is ArgumentException or OverflowException) { Error = error; }
            if (iterator is null) yield break;
            try
            {
                while (true)
                {
                    var moved = false; object? item = null;
                    try { moved = iterator.MoveNext(); if (moved) item = iterator.Current; }
                    catch (Exception error) when (error is ArgumentException or OverflowException) { Error = error; }
                    if (Error is not null || !moved) yield break;
                    if (count++ >= limit)
                    { _truncated = true; Error = new ArgumentOutOfRangeException(nameof(ItemsSource), "Source exceeds the engineering observation budget."); yield break; }
                    yield return item;
                }
            }
            finally { (iterator as IDisposable)?.Dispose(); }
        }
    }
}
