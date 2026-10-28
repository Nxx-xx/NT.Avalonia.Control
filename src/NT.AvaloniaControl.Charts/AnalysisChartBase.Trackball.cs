using Avalonia;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Avalonia.Threading;

namespace NT.AvaloniaControl.Charts;

public abstract partial class AnalysisChartBase
{
    public static readonly StyledProperty<ChartTrackballMode> TrackballModeProperty = CartesianChart.TrackballModeProperty.AddOwner<AnalysisChartBase>();
    public static readonly StyledProperty<ChartTrackballDisplayMode> TrackballDisplayModeProperty = CartesianChart.TrackballDisplayModeProperty.AddOwner<AnalysisChartBase>();
    public static readonly StyledProperty<IDataTemplate?> TrackballContentTemplateProperty = CartesianChart.TrackballContentTemplateProperty.AddOwner<AnalysisChartBase>();
    public static readonly StyledProperty<bool> ShowTrackballLineProperty = CartesianChart.ShowTrackballLineProperty.AddOwner<AnalysisChartBase>();
    public static readonly StyledProperty<IBrush?> TrackballLineStrokeProperty = CartesianChart.TrackballLineStrokeProperty.AddOwner<AnalysisChartBase>();
    public static readonly StyledProperty<double> TrackballLineStrokeThicknessProperty = CartesianChart.TrackballLineStrokeThicknessProperty.AddOwner<AnalysisChartBase>();
    public static readonly StyledProperty<bool> ShowTrackballMarkersProperty = CartesianChart.ShowTrackballMarkersProperty.AddOwner<AnalysisChartBase>();
    public static readonly StyledProperty<double> TrackballMarkerSizeProperty = CartesianChart.TrackballMarkerSizeProperty.AddOwner<AnalysisChartBase>();
    public static readonly StyledProperty<IBrush?> TrackballMarkerStrokeProperty = CartesianChart.TrackballMarkerStrokeProperty.AddOwner<AnalysisChartBase>();
    public static readonly StyledProperty<IBrush?> TrackballMarkerFillProperty = CartesianChart.TrackballMarkerFillProperty.AddOwner<AnalysisChartBase>();
    public static readonly StyledProperty<double> TrackballMarkerStrokeThicknessProperty = CartesianChart.TrackballMarkerStrokeThicknessProperty.AddOwner<AnalysisChartBase>();
    public static readonly StyledProperty<int> MaxTrackballReadingsProperty = EngineeringChartBase.MaxTrackballReadingsProperty.AddOwner<AnalysisChartBase>();
    public static readonly StyledProperty<Func<IReadOnlyList<ChartDataTrackballReading>, string?>?> TrackballFormatterProperty = EngineeringChartBase.TrackballFormatterProperty.AddOwner<AnalysisChartBase>();
    public static readonly DirectProperty<AnalysisChartBase, IReadOnlyList<ChartDataTrackballReading>> TrackballReadingsProperty = AvaloniaProperty.RegisterDirect<AnalysisChartBase, IReadOnlyList<ChartDataTrackballReading>>(nameof(TrackballReadings), chart => chart.TrackballReadings);
    public static readonly DirectProperty<AnalysisChartBase, int> TrackballTotalReadingsProperty = AvaloniaProperty.RegisterDirect<AnalysisChartBase, int>(nameof(TrackballTotalReadings), chart => chart.TrackballTotalReadings);

    public ChartTrackballMode TrackballMode { get => GetValue(TrackballModeProperty); set => SetValue(TrackballModeProperty, value); }
    public ChartTrackballDisplayMode TrackballDisplayMode { get => GetValue(TrackballDisplayModeProperty); set => SetValue(TrackballDisplayModeProperty, value); }
    public IDataTemplate? TrackballContentTemplate { get => GetValue(TrackballContentTemplateProperty); set => SetValue(TrackballContentTemplateProperty, value); }
    public bool ShowTrackballLine { get => GetValue(ShowTrackballLineProperty); set => SetValue(ShowTrackballLineProperty, value); }
    public IBrush? TrackballLineStroke { get => GetValue(TrackballLineStrokeProperty); set => SetValue(TrackballLineStrokeProperty, value); }
    public double TrackballLineStrokeThickness { get => GetValue(TrackballLineStrokeThicknessProperty); set => SetValue(TrackballLineStrokeThicknessProperty, value); }
    public bool ShowTrackballMarkers { get => GetValue(ShowTrackballMarkersProperty); set => SetValue(ShowTrackballMarkersProperty, value); }
    public double TrackballMarkerSize { get => GetValue(TrackballMarkerSizeProperty); set => SetValue(TrackballMarkerSizeProperty, value); }
    public IBrush? TrackballMarkerStroke { get => GetValue(TrackballMarkerStrokeProperty); set => SetValue(TrackballMarkerStrokeProperty, value); }
    public IBrush? TrackballMarkerFill { get => GetValue(TrackballMarkerFillProperty); set => SetValue(TrackballMarkerFillProperty, value); }
    public double TrackballMarkerStrokeThickness { get => GetValue(TrackballMarkerStrokeThicknessProperty); set => SetValue(TrackballMarkerStrokeThicknessProperty, value); }
    public int MaxTrackballReadings { get => GetValue(MaxTrackballReadingsProperty); set => SetValue(MaxTrackballReadingsProperty, value); }
    public Func<IReadOnlyList<ChartDataTrackballReading>, string?>? TrackballFormatter { get => GetValue(TrackballFormatterProperty); set => SetValue(TrackballFormatterProperty, value); }
    public IReadOnlyList<ChartDataTrackballReading> TrackballReadings => _professionalTrackballReadings;
    public int TrackballTotalReadings => _professionalTrackballTotal;

    private IReadOnlyList<ChartDataTrackballReading> _professionalTrackballReadings = [];
    private IReadOnlyList<ChartDataTrackballPresentationReading> _professionalTrackballCards = [];
    private ChartDataTrackballPresenter? _professionalTrackball;
    private Point? _professionalTrackballPosition;
    private object? _professionalTrackballFrame, _professionalTrackballBusiness;
    private long _professionalTrackballSourceRevision = -1, _professionalTrackballVersion;
    private bool _professionalTrackballUpdating, _professionalTrackballDeferred, _professionalTrackballQueued, _professionalTrackballClearing;
    private int _professionalTrackballTotal;
    private Func<IReadOnlyList<ChartDataTrackballReading>, string?>? _failedProfessionalTrackballFormatter;
    private ChartDataTrackballPresenter ProfessionalTrackball
    {
        get
        {
            if (_professionalTrackball is { } existing) return existing;
            var presenter = new ChartDataTrackballPresenter(this, () => TrackballContentTemplate,
                () => _professionalTrackballCards, RefreshProfessionalTrackball, () => (TooltipBackground, TooltipForeground, TooltipBorderBrush), RenderProfessionalTrackball, () => _professionalTrackballReadings.Count > 0);
            _professionalTrackball = presenter; if (_attached) presenter.Attached(); return presenter;
        }
    }
    private bool ProfessionalTrackballBusy => _professionalTrackballUpdating || _professionalTrackballClearing || _professionalTrackball?.IsBusy == true;
    private bool HasProfessionalTrackball => TrackballMode != ChartTrackballMode.None;

    /// <summary>Reads visible native glyph bounds at this chart-local position. Every closest vertical/horizontal
    /// slice glyph is retained in family painting order, up to MaxTrackballReadings. No Cartesian interpolation is fabricated.
    /// Invalid positions, hidden/disabled owners, and None mode clear the reading.</summary>
    public void UpdateTrackball(Point position)
    {
        VerifyAccess();
        if (!double.IsFinite(position.X) || !double.IsFinite(position.Y) || !HasProfessionalTrackball ||
            !IsEffectivelyVisible || !IsEffectivelyEnabled) { ClearTrackball(); return; }
        if (_professionalTrackballPosition != position) _professionalTrackballSourceRevision = -1;
        _professionalTrackballPosition = position;
        RefreshProfessionalTrackball();
    }
    public void ClearTrackball()
    {
        VerifyAccess(); _professionalTrackballPosition = null; InvalidateProfessionalTrackball(queue: false); InvalidateVisual();
    }
    private void UpdateProfessionalTrackball(Point position) => UpdateTrackball(position);
    private void ClearProfessionalTrackball() => ClearTrackball();

    private void InvalidateProfessionalTrackball(bool queue = true)
    {
        var ownsClear = !_professionalTrackballClearing;
        if (ownsClear) _professionalTrackballClearing = true;
        try
        {
            ++_professionalTrackballVersion; _professionalTrackballSourceRevision = -1;
            _professionalTrackballCards = []; _professionalTrackballFrame = null; _professionalTrackballBusiness = null;
            try { _professionalTrackball?.InvalidateReading(); }
            finally
            {
                // Both visual-context teardown and public notifications can call application code.
                // The outermost clear owns the empty-state publication; nested clears only invalidate it.
                if (ownsClear)
                {
                    try
                    {
                        if (_professionalTrackballReadings.Count != 0) SetAndRaise(TrackballReadingsProperty, ref _professionalTrackballReadings, Array.Empty<ChartDataTrackballReading>());
                    }
                    finally
                    {
                        if (_professionalTrackballTotal != 0) SetAndRaise(TrackballTotalReadingsProperty, ref _professionalTrackballTotal, 0);
                    }
                }
            }
            _professionalTrackball?.InvalidateVisual();
        }
        finally
        {
            if (ownsClear)
            {
                _professionalTrackballClearing = false;
                // A child-context or reading-cleared notification may request a new pointer/mode/source.
                // Publish it only after the whole teardown and empty reading/total have both settled.
                if (_professionalTrackballDeferred && _professionalTrackballPosition is not null) QueueProfessionalTrackball();
            }
        }
        if (queue && _professionalTrackballPosition is not null) QueueProfessionalTrackball();
    }

    private void RefreshProfessionalTrackball()
    {
        if (_professionalTrackballUpdating || _professionalTrackballClearing) { _professionalTrackballDeferred = true; return; }
        if (_rendering || _building || _presentationContextsBuilding) { _professionalTrackballDeferred = true; return; }
        if (!_attached || !HasProfessionalTrackball || !IsEffectivelyVisible || _professionalTrackball?.IsOwnerVisible == false || !IsEffectivelyEnabled || _professionalTrackballPosition is not { } position)
        { InvalidateProfessionalTrackball(queue: false); return; }
        if (_failedProfessionalTrackballFormatter is { } failed && ReferenceEquals(failed, TrackballFormatter))
        { InvalidateProfessionalTrackball(queue: false); return; }
        var marks = GetMarks();
        if (!_plot.Contains(position)) { ClearTrackball(); return; }
        var business = DataContext; var revision = _revision; var attachment = _attachmentRevision;
        if (_professionalTrackballSourceRevision == revision && ReferenceEquals(_professionalTrackballFrame, marks) &&
            ReferenceEquals(_professionalTrackballBusiness, business))
        { _professionalTrackball?.Synchronize(); return; }
        var version = ++_professionalTrackballVersion;
        _professionalTrackballUpdating = true;
        try
        {
            var mode = TrackballMode;
            var candidates = new List<int>(Math.Min(marks.Count, MaxTrackballReadings)); var candidateTotal = 0; var minimum = double.PositiveInfinity;
            for (var index = 0; index < marks.Count; index++)
            {
                var mark = marks[index];
                if (!(mark.IsVisible && mark.Fill is not null) || !ChartDataTrackballGeometry.Visible(mark.Bounds, _plot)) continue;
                var distance = ChartDataTrackballGeometry.Distance(mark.Bounds.Intersect(_plot), position, mode);
                if (distance + 1e-9 < minimum)
                { minimum = distance; candidateTotal = 1; candidates.Clear(); if (MaxTrackballReadings > 0) candidates.Add(index); }
                else if (Math.Abs(distance - minimum) <= 1e-9)
                { candidateTotal++; if (candidates.Count < MaxTrackballReadings) candidates.Add(index); }
            }
            var retained = new List<ChartDataTrackballReading>(candidates.Count);
            foreach (var index in candidates)
            {
                var mark = marks[index];
                var hit = Hit(mark);
                var reading = new ChartDataTrackballReading(Array.AsReadOnly(new[] { mark.Data }), mark.TooltipText ?? $"{mark.Data.Category}: {FormatValue(mark.Data.Point.Y)}", hit.Position)
                { PointHit = hit, Bounds = mark.Bounds, Brush = mark.Fill, Value = mark.Data.Point.Y, BusinessDataContext = business };
                retained.Add(reading);
            }
            if (!Current()) { _professionalTrackballDeferred = true; return; }
            var readings = Array.AsReadOnly(retained.ToArray());
            var cards = new List<ChartDataTrackballPresentationReading>();
            if (readings.Count > 0)
            {
                if (TrackballDisplayMode == ChartTrackballDisplayMode.GroupAllPoints) AddCard(readings, position);
                else foreach (var reading in readings) AddCard(Array.AsReadOnly(new[] { reading }), reading.Position);
            }
            if (!Current()) { _professionalTrackballDeferred = true; return; }
            _professionalTrackballSourceRevision = revision; _professionalTrackballFrame = marks; _professionalTrackballBusiness = business;
            _professionalTrackballCards = Array.AsReadOnly(cards.ToArray());
            SetAndRaise(TrackballTotalReadingsProperty, ref _professionalTrackballTotal, candidateTotal);
            if (!Current()) return;
            SetAndRaise(TrackballReadingsProperty, ref _professionalTrackballReadings, readings);
            if (!Current()) return;
            ProfessionalTrackball.Synchronize(); _professionalTrackball?.InvalidateVisual(); InvalidateVisual();

            bool Current() => version == _professionalTrackballVersion && revision == _revision && attachment == _attachmentRevision &&
                _attached && !_dirty && HasProfessionalTrackball && _professionalTrackballPosition == position && ReferenceEquals(DataContext, business) &&
                IsEffectivelyVisible && _professionalTrackball?.IsOwnerVisible != false && IsEffectivelyEnabled;
            void AddCard(IReadOnlyList<ChartDataTrackballReading> cardReadings, Point anchor)
            {
                string? text;
                if (TrackballFormatter is { } formatter)
                {
                    try { text = formatter(cardReadings); }
                    catch { if (ReferenceEquals(formatter, TrackballFormatter)) _failedProfessionalTrackballFormatter = formatter; throw; }
                }
                else text = string.Join(Environment.NewLine, cardReadings.Select(item => item.Text));
                if (text is null && TrackballContentTemplate is null) return;
                var context = new ChartDataTrackballContext(cardReadings, anchor)
                { Text = text, BusinessDataContext = business, TotalReadings = candidateTotal, IsTruncated = candidateTotal > readings.Count };
                cards.Add(new(context, context, version, Current));
            }
        }
        catch
        {
            InvalidateProfessionalTrackball(queue: false); throw;
        }
        finally { _professionalTrackballUpdating = false; if (_professionalTrackballDeferred) QueueProfessionalTrackball(); }
    }

    private void QueueProfessionalTrackball()
    {
        if (_professionalTrackballQueued || !_attached) return;
        _professionalTrackballDeferred = false; _professionalTrackballQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _professionalTrackballQueued = false;
            if (_rendering || _building || _presentationContextsBuilding) { _professionalTrackballDeferred = true; return; }
            RefreshProfessionalTrackball();
        }, DispatcherPriority.Loaded);
    }

    private bool TrackballPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        var property = change.Property;
        if (property == TrackballReadingsProperty || property == TrackballTotalReadingsProperty) return true;
        if (property == ItemsSourceProperty || property == IdPathProperty || property == CategoryPathProperty || property == ValuePathProperty)
            InvalidateProfessionalTrackball();
        if (property == TrackballContentTemplateProperty)
        { InvalidateProfessionalTrackball(); _professionalTrackball?.TemplateChanged(); return true; }
        if (property == TrackballModeProperty || property == TrackballDisplayModeProperty || property == MaxTrackballReadingsProperty || property == TrackballFormatterProperty)
        { if (property == TrackballFormatterProperty) _failedProfessionalTrackballFormatter = null; InvalidateProfessionalTrackball(); QueueProfessionalTrackball(); InvalidateVisual(); return true; }
        if (property == ShowTrackballLineProperty || property == TrackballLineStrokeProperty || property == TrackballLineStrokeThicknessProperty ||
            property == ShowTrackballMarkersProperty || property == TrackballMarkerSizeProperty || property == TrackballMarkerStrokeProperty ||
            property == TrackballMarkerFillProperty || property == TrackballMarkerStrokeThicknessProperty)
        { _professionalTrackball?.InvalidateVisual(); InvalidateVisual(); return true; }
        if (property == DataContextProperty || property == IsEffectivelyEnabledProperty || property == IsVisibleProperty)
        { InvalidateProfessionalTrackball(); QueueProfessionalTrackball(); }
        if (property == TooltipBackgroundProperty || property == TooltipForegroundProperty || property == TooltipBorderBrushProperty ||
            property == ForegroundProperty || property == FontFamilyProperty || property == FontSizeProperty || property == FontStyleProperty ||
            property == FontWeightProperty || property == HighContrastModeProperty)
            _professionalTrackball?.UpdateAppearance();
        return false;
    }
    private void MeasureProfessionalTrackball(Size size) => _professionalTrackball?.Measure(size);
    private void ArrangeProfessionalTrackball(Size size) => _professionalTrackball?.Arrange(size);
    private void AttachedProfessionalTrackball() { _professionalTrackball?.Attached(); QueueProfessionalTrackball(); }
    private void DetachedProfessionalTrackball() { _professionalTrackballPosition = null; InvalidateProfessionalTrackball(queue: false); _professionalTrackball?.Detached(); }
    private void EnsureProfessionalTrackballMembership() => _professionalTrackball?.EnsureMembership();
    private void ProfessionalTrackballRenderFinished() { if (_professionalTrackballDeferred || _professionalTrackballPosition is not null) QueueProfessionalTrackball(); }
    private void RenderProfessionalTrackball(DrawingContext drawing)
    {
        if (_dataTooltipExportDepth != 0 || _professionalTrackballPosition is not { } position || !HasProfessionalTrackball ||
            !IsEffectivelyVisible || !IsEffectivelyEnabled || _professionalTrackballReadings.Count == 0) return;
        using (drawing.PushClip(_plot))
        {
            if (ShowTrackballLine && TrackballLineStrokeThickness > 0)
            {
                var stroke = TrackballLineStroke ?? Foreground ?? Brushes.Gray;
                var start = TrackballMode == ChartTrackballMode.Horizontal ? new Point(_plot.Left, position.Y) : new Point(position.X, _plot.Top);
                var end = TrackballMode == ChartTrackballMode.Horizontal ? new Point(_plot.Right, position.Y) : new Point(position.X, _plot.Bottom);
                if (TrackballLineStroke is null)
                    drawing.DrawLine(new Pen(ChartDataTrackballContrast.Halo(stroke), TrackballLineStrokeThickness + 2), start, end);
                drawing.DrawLine(new Pen(stroke, TrackballLineStrokeThickness), start, end);
            }
            if (ShowTrackballMarkers && TrackballMarkerSize > 0)
                foreach (var reading in _professionalTrackballReadings)
                {
                    var stroke = TrackballMarkerStroke ?? Foreground ?? reading.Brush ?? Brushes.Gray;
                    if (TrackballMarkerStroke is null && TrackballMarkerStrokeThickness > 0)
                        drawing.DrawEllipse(null, new Pen(ChartDataTrackballContrast.Halo(stroke), TrackballMarkerStrokeThickness + 2),
                            reading.Position, TrackballMarkerSize / 2, TrackballMarkerSize / 2);
                    drawing.DrawEllipse(TrackballMarkerFill, new Pen(stroke, TrackballMarkerStrokeThickness),
                        reading.Position, TrackballMarkerSize / 2, TrackballMarkerSize / 2);
                }
        }
    }
}
