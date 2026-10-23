using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.LogicalTree;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

public sealed partial class CartesianChart
{
    public static readonly StyledProperty<IEnumerable<ChartAnnotation>?> AnnotationsSourceProperty =
        AvaloniaProperty.Register<CartesianChart, IEnumerable<ChartAnnotation>?>(nameof(AnnotationsSource));
    public ObservableCollection<ChartAnnotation> Annotations { get; } = [];
    /// <summary>A non-null source replaces the inline annotation collection.</summary>
    public IEnumerable<ChartAnnotation>? AnnotationsSource { get => GetValue(AnnotationsSourceProperty); set => SetValue(AnnotationsSourceProperty, value); }
    private readonly HashSet<ChartAnnotation> _ownedAnnotations = [];
    private readonly HashSet<ChartAnnotation> _observedAnnotations = [];
    private ChartAnnotation[] _activeAnnotations = [];
    private INotifyCollectionChanged? _annotationNotifications;
    private bool _visualInitialized;

    private void InitializeVisualFeatures()
    {
        if (_visualInitialized) return;
        _visualInitialized = true;
        PropertyChanged += VisualFeaturePropertyChanged;
        Annotations.CollectionChanged += AnnotationCollectionChanged;
        SynchronizeVisualFeatures();
    }

    private void VisualFeaturePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property != AnnotationsSourceProperty) return;
        SynchronizeVisualFeatures(); AnnotationAxesChanged();
    }

    private void SynchronizeVisualFeatures()
    {
        if (!_visualInitialized) return;
        var active = (AnnotationsSource ?? Annotations).Where(item => item is not null).ToArray();
        var desired = active.ToHashSet();
        foreach (var item in desired)
            if (((ILogical)item).LogicalParent is { } parent && !ReferenceEquals(parent, this))
                throw new InvalidOperationException("An annotation can belong to only one chart.");
        var notifications = _attached ? AnnotationsSource as INotifyCollectionChanged : null;
        if (!ReferenceEquals(notifications, _annotationNotifications))
        {
            if (_annotationNotifications is not null) _annotationNotifications.CollectionChanged -= AnnotationCollectionChanged;
            _annotationNotifications = notifications;
            if (_annotationNotifications is not null) _annotationNotifications.CollectionChanged += AnnotationCollectionChanged;
        }
        foreach (var old in _ownedAnnotations.Except(desired).ToArray())
        { LogicalChildren.Remove(old); _ownedAnnotations.Remove(old); }
        foreach (var item in desired.Except(_ownedAnnotations))
        { LogicalChildren.Add(item); _ownedAnnotations.Add(item); }
        var observed = _attached ? desired : [];
        foreach (var old in _observedAnnotations.Except(observed).ToArray())
        { old.Changed -= AnnotationChanged; _observedAnnotations.Remove(old); }
        foreach (var item in observed.Except(_observedAnnotations))
        { item.Changed += AnnotationChanged; _observedAnnotations.Add(item); }
        _activeAnnotations = active;
    }

    private void AnnotationCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (ReferenceEquals(sender, Annotations) && AnnotationsSource is not null) return;
        VerifyAccess(); SynchronizeVisualFeatures(); AnnotationAxesChanged();
    }
    private void AnnotationChanged(object? sender, EventArgs args)
    {
        if (args is AvaloniaPropertyChangedEventArgs change &&
            (change.Property == ChartAnnotation.XAxisKeyProperty || change.Property == ChartAnnotation.YAxisKeyProperty ||
             change.Property == ChartAnnotation.CoordinateUnitProperty || change.Property == ChartAnnotation.IsVisibleProperty))
            AnnotationAxesChanged();
        else RequestRender();
    }

    private void AnnotationAxesChanged()
    {
        InvalidateInputTransform(); StopPan(); StopRectangle();
        InvalidateScene(); InvalidateMeasure(); RequestRender();
    }

    private void RenderAnnotations(DrawingContext drawing, ChartScene scene, ChartAnnotationLayer layer)
    {
        if (_activeAnnotations.Length != 0) RenderAnnotationsCore(drawing, scene, layer);
    }

    private void RenderAnnotationsCore(DrawingContext drawing, ChartScene scene, ChartAnnotationLayer layer)
    {
        foreach (var annotation in _activeAnnotations)
        {
            if (!annotation.IsVisible || annotation.Opacity <= 0 || annotation.Layer != layer) continue;
            IChartProjection projection;
            Geometry? breakClip = null;
            if (annotation.CoordinateUnit == ChartAnnotationCoordinateUnit.Pixel) projection = new PixelAnnotationProjection(scene.Plot);
            else
            {
                var x = AnnotationAxis(annotation.XAxisKey, XAxis, scene.ExtraXAxes);
                var y = AnnotationAxis(annotation.YAxisKey, YAxis, scene.ExtraYAxes);
                if (x is null || y is null || !scene.Scales.TryGetValue(x, out var xScale) || !scene.Scales.TryGetValue(y, out var yScale)) continue;
                projection = new CartesianProjection(scene.Plot, xScale, yScale);
                if (annotation.ClipToPlotArea) breakClip = CreateBreakClip(scene.Plot, xScale, yScale);
            }
            var render = new ChartAnnotationRenderContext(scene.Plot, projection, new Typeface(FontFamily, FontStyle, FontWeight),
                FontSize, Foreground ?? Brushes.SlateGray);
            using var opacity = drawing.PushOpacity(annotation.Opacity);
            if (!annotation.ClipToPlotArea) annotation.Render(drawing, render);
            else
            {
                using var clip = drawing.PushClip(scene.Plot);
                if (breakClip is null) annotation.Render(drawing, render);
                else { using var gaps = drawing.PushGeometryClip(breakClip); annotation.Render(drawing, render); }
            }
        }
    }

    private sealed class PixelAnnotationProjection(Rect plot) : IChartProjection
    {
        public Rect PlotArea => plot;
        public double MapX(double value) => Limit(plot.Left + value);
        public double MapY(double value) => Limit(plot.Top + value);
        public double YOrigin => plot.Top;
        public Point Project(ChartPoint point) => new(MapX(point.X), MapY(point.Y));
        public ChartPoint Unproject(Point point) => new(point.X - plot.Left, point.Y - plot.Top);
        private static double Limit(double value) => double.IsFinite(value) ? Math.Clamp(value, -1_000_000, 1_000_000) : double.NaN;
    }

    private void RenderDataLabels(DrawingContext drawing, ChartScene scene)
    {
        foreach (var snapshot in scene.Snapshots)
            if (snapshot.Series.ShowDataLabels && snapshot.Series.MaxDataLabels > 0 && snapshot.Data.Count > 0)
            { RenderDataLabelsCore(drawing, scene); return; }
    }

    private void RenderDataLabelsCore(DrawingContext drawing, ChartScene scene)
    {
        List<Rect>? occupied = _templateLabelRectangles.Count == 0 ? null : new(_templateLabelRectangles);
        foreach (var snapshot in scene.Snapshots)
        {
            var series = snapshot.Series;
            if (!series.ShowDataLabels || series.MaxDataLabels == 0 || snapshot.Data.Count == 0 || UseLabelTemplate(series)) continue;
            var data = snapshot.Data;
            var projection = snapshot.Context!.Projection;
            var index = ChartDataIndex.For(data);
            var first = 0; var last = index.Count; var reversed = false;
            if (projection is IChartSeparableProjection { HasMonotonicX: true })
                (first, last, reversed) = index.ScreenRange(projection, scene.Plot.Left, scene.Plot.Right);
            var stride = Math.Max(1, (int)Math.Ceiling((last - first) / (double)series.MaxDataLabels));
            var position = series.DataLabelPosition;
            using var plotClip = drawing.PushClip(scene.Plot);
            using IDisposable? gapClip = snapshot.BreakClip is { } gaps ? drawing.PushGeometryClip(gaps) : null;
            for (var cursor = first; cursor < last; cursor += stride)
            {
                var ordinal = index.Ordinal(reversed ? index.Count - 1 - cursor : cursor);
                var value = data.GetValue(ordinal);
                if (value.IsEmpty || !IsValid(value.Point)) continue;
                if (!TryGetDataLabelPlacement(snapshot.Context!, ordinal, out var anchor, out var bar, out var horizontal, out var below)) continue;
                if (!double.IsFinite(anchor.X) || !double.IsFinite(anchor.Y) || !scene.Plot.Contains(anchor)) continue;
                if (!data.TryGetPoint(value.SourceIndex, out var item)) continue;
                var xText = scene.Scales[snapshot.XAxis].FormatLabel(item.Point.X, snapshot.XAxis);
                var yText = scene.Scales[snapshot.YAxis].FormatLabel(item.Point.Y, snapshot.YAxis);
                var labelContext = new ChartDataLabelContext(series, item, anchor, xText, yText);
                var label = FormatDataLabel(series, labelContext);
                if (string.IsNullOrEmpty(label)) continue;
                var text = new FormattedText(label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    new Typeface(series.DataLabelFontFamily ?? FontFamily, FontStyle, FontWeight), series.DataLabelFontSize ?? FontSize,
                    series.DataLabelForeground ?? Foreground ?? Brushes.SlateGray)
                { MaxTextWidth = Math.Max(1, Math.Min(320, scene.Plot.Width - 8)), MaxLineCount = 1 };
                var size = new Size(Math.Min(scene.Plot.Width, text.Width + 6), text.Height + 4);
                if (!double.IsFinite(size.Width) || !double.IsFinite(size.Height) || size.Height > scene.Plot.Height) continue;
                var alternatives = DataLabelPlacements(position, below, horizontal);
                foreach (var placement in alternatives)
                {
                    var rect = LabelRectangle(anchor, size, placement, bar, below, DisplayedPointRadius(series, item.MarkerDiameter) + 4, horizontal);
                    rect = rect.Translate(series.DataLabelOffset);
                    if (position == ChartDataLabelPosition.Auto)
                        rect = new Rect(Math.Clamp(rect.X, scene.Plot.Left, Math.Max(scene.Plot.Left, scene.Plot.Right - rect.Width)),
                            Math.Clamp(rect.Y, scene.Plot.Top, Math.Max(scene.Plot.Top, scene.Plot.Bottom - rect.Height)), rect.Width, rect.Height);
                    var visible = rect.Intersect(scene.Plot);
                    if (visible.Width <= 0 || visible.Height <= 0) continue;
                    if (series.DataLabelOverlap == ChartDataLabelOverlap.Hide && occupied?.Any(old => old.Intersects(rect)) == true) continue;
                    occupied ??= []; occupied.Add(rect);
                    if (series.DataLabelBackground is { } background) drawing.DrawRectangle(background, null, rect, 2, 2);
                    using var labelClip = drawing.PushClip(rect);
                    drawing.DrawText(text, rect.TopLeft + new Vector(3, 2));
                    break;
                }
            }
        }
    }

    private static bool TryGetDataLabelPlacement(ChartSeriesRenderContext context, int ordinal, out Point anchor,
        out Rect bar, out bool horizontal, out bool below)
    {
        horizontal = false; below = false; bar = default;
        var value = context.Data.GetValue(ordinal);
        anchor = context.Projection.Project(value.Point);
        if (context.Series is BarSeries { UsesLegacyBarRenderer: true })
        {
            bar = CartesianSeriesRenderer.BarRectangle(context, value.Point);
            anchor = new Point(bar.Center.X, anchor.Y);
            below = anchor.Y > context.Projection.YOrigin;
        }
        else if (context.Series.Renderer is ChartStatisticalSeriesRenderer statistics)
        {
            if (!statistics.TryPlacement(context, ordinal, out anchor, out bar)) return false;
            horizontal = HorizontalComparison(context.Series);
        }
        else if (context.Series.Renderer is ChartFinancialSeriesRenderer financial)
        {
            if (!financial.TryPlacement(context, ordinal, out anchor, out bar)) return false;
        }
        else if (context.Series is IChartComparisonSeries)
        {
            if (!ChartComparisonGeometry.TryCreate(context, ordinal, out var shape)) return false;
            anchor = shape.End; bar = shape.Bounds; horizontal = shape.Horizontal;
            below = horizontal ? shape.End.X < shape.Start.X : shape.End.Y > shape.Start.Y;
        }
        return double.IsFinite(anchor.X) && double.IsFinite(anchor.Y);
    }

    private static ChartDataLabelPosition[] DataLabelPlacements(ChartDataLabelPosition position, bool below, bool horizontal)
    {
        if (position != ChartDataLabelPosition.Auto) return [position];
        if (horizontal) return [below ? ChartDataLabelPosition.Left : ChartDataLabelPosition.Right,
            below ? ChartDataLabelPosition.Right : ChartDataLabelPosition.Left, ChartDataLabelPosition.Above, ChartDataLabelPosition.Below];
        return [below ? ChartDataLabelPosition.Below : ChartDataLabelPosition.Above,
            below ? ChartDataLabelPosition.Above : ChartDataLabelPosition.Below, ChartDataLabelPosition.Right, ChartDataLabelPosition.Left];
    }

    private static Rect LabelRectangle(Point point, Size size, ChartDataLabelPosition position, Rect bar, bool below, double gap, bool horizontal = false) => position switch
    {
        ChartDataLabelPosition.Above => new(point.X - size.Width / 2, point.Y - gap - size.Height, size.Width, size.Height),
        ChartDataLabelPosition.Below => new(point.X - size.Width / 2, point.Y + gap, size.Width, size.Height),
        ChartDataLabelPosition.Left => new(point.X - gap - size.Width, point.Y - size.Height / 2, size.Width, size.Height),
        ChartDataLabelPosition.Right => new(point.X + gap, point.Y - size.Height / 2, size.Width, size.Height),
        ChartDataLabelPosition.InsideEnd when horizontal && bar.Width > 0 => new(below ? point.X + 3 : point.X - size.Width - 3,
            point.Y - size.Height / 2, size.Width, size.Height),
        ChartDataLabelPosition.InsideEnd when bar.Height > 0 => new(point.X - size.Width / 2,
            below ? point.Y - size.Height - 3 : point.Y + 3, size.Width, size.Height),
        _ => new(point.X - size.Width / 2, point.Y - size.Height / 2, size.Width, size.Height),
    };
}
