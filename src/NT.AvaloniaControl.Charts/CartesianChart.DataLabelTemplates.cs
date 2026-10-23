using System.Globalization;
using System.Runtime.ExceptionServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.Charts;

public sealed partial class CartesianChart
{
    private LabelTemplateHost? _labelTemplateHost;
    private readonly Dictionary<LabelTemplateKey, LabelTemplateEntry> _labelTemplateEntries = [];
    private readonly Dictionary<ChartSeries, IDataTemplate> _failedLabelTemplates = [];
    private readonly List<Rect> _templateLabelRectangles = [];
    private bool _labelTemplatesDirty = true;
    private bool _synchronizingLabelTemplates;
    private bool _clearingLabelTemplates;
    private long _labelTemplateRevision;

    private void InvalidateLabelPresentation()
    {
        _labelTemplatesDirty = true;
        _labelTemplateRevision++;
        if (_failedLabelTemplates.Count > 0)
            foreach (var pair in _failedLabelTemplates.ToArray())
                if (!ActiveSeries.Contains(pair.Key) || !ReferenceEquals(pair.Key.DataLabelTemplate, pair.Value))
                    _failedLabelTemplates.Remove(pair.Key);
        if (_labelTemplateHost is not null || _synchronizingLabelTemplates || HasLabelTemplates()) InvalidateMeasure();
    }

    private bool HasLabelTemplates() => ActiveSeries.Any(series => series is not null && series.EffectiveVisibility &&
        series.ShowDataLabels && series.MaxDataLabels > 0 && series.DataLabelTemplate is not null);

    private void MeasureLabelPresentation(Size size)
    {
        if (_labelTemplateHost is null && !HasLabelTemplates()) return;
        if (!_attached && !_exportInProgress || !IsVisible) { ClearLabelPresentation(); return; }
        SynchronizeLabelPresentation(size);
    }

    private void ArrangeLabelPresentation(Size size)
    {
        if (_labelTemplateHost is null && !HasLabelTemplates()) return;
        MeasureLabelPresentation(size);
        if (_labelTemplateHost is not { } host) return;
        EnsureLabelPresentationMembership();
        host.Arrange(new Rect(size));
    }

    private void SynchronizeLabelPresentation(Size size)
    {
        if (_synchronizingLabelTemplates || _clearingLabelTemplates) return;
        if (!_labelTemplatesDirty && _labelTemplateHost is { } existing)
        { EnsureLabelPresentationMembership(); existing.Measure(size); return; }
        _synchronizingLabelTemplates = true;
        var revision = _labelTemplateRevision;
        ChartSeries? buildingSeries = null;
        IDataTemplate? buildingTemplate = null;
        bool Current() => revision == _labelTemplateRevision && !_clearingLabelTemplates && IsVisible && (_attached || _exportInProgress);
        try
        {
            var scene = GetScene();
            if (!Current()) return;
            if (scene is null || !HasLabelTemplates())
            { ReleaseLabelPresentationVisual(); _labelTemplatesDirty = false; return; }
            var host = _labelTemplateHost ??= new LabelTemplateHost
            {
                Name = "PART_ChartDataLabelsOverlay", IsHitTestVisible = false, Focusable = false,
                ClipToBounds = true, ZIndex = 20,
            };
            KeyboardNavigation.SetTabNavigation(host, KeyboardNavigationMode.None);
            EnsureLabelPresentationMembership();
            if (!Current()) return;
            var retained = new HashSet<LabelTemplateKey>();
            var activeLayers = new HashSet<ChartSeries>();
            _templateLabelRectangles.Clear();
            foreach (var snapshot in scene.Snapshots)
            {
                var series = snapshot.Series;
                if (!series.ShowDataLabels || series.MaxDataLabels == 0 || series.DataLabelTemplate is not { } template || snapshot.Data.Count == 0) continue;
                if (_failedLabelTemplates.TryGetValue(series, out var failed) && ReferenceEquals(failed, template)) continue;
                _failedLabelTemplates.Remove(series);
                buildingSeries = series; buildingTemplate = template;
                activeLayers.Add(series);
                var layer = host.GetLayer(series);
                // Break clips use chart-local coordinates and already include the plot rectangle.
                layer.Clip = snapshot.BreakClip ?? new RectangleGeometry(scene.Plot);
                layer.Slots.Clear();
                var projection = snapshot.Context!.Projection;
                var index = ChartDataIndex.For(snapshot.Data);
                var first = 0; var last = index.Count; var reversed = false;
                if (projection is IChartSeparableProjection { HasMonotonicX: true })
                    (first, last, reversed) = index.ScreenRange(projection, scene.Plot.Left, scene.Plot.Right);
                var stride = Math.Max(1, (int)Math.Ceiling((last - first) / (double)series.MaxDataLabels));
                for (var cursor = first; cursor < last; cursor += stride)
                {
                    var ordinal = index.Ordinal(reversed ? index.Count - 1 - cursor : cursor);
                    var value = snapshot.Data.GetValue(ordinal);
                    if (value.IsEmpty || !IsValid(value.Point)) continue;
                    if (!TryGetDataLabelPlacement(snapshot.Context!, ordinal, out var anchor, out var bar, out var horizontal, out var below)) continue;
                    if (!double.IsFinite(anchor.X) || !double.IsFinite(anchor.Y) || !scene.Plot.Contains(anchor) ||
                        !snapshot.Data.TryGetPoint(value.SourceIndex, out var data)) continue;
                    var labelContext = new ChartDataLabelContext(series, data, anchor,
                        scene.Scales[snapshot.XAxis].FormatLabel(data.Point.X, snapshot.XAxis),
                        scene.Scales[snapshot.YAxis].FormatLabel(data.Point.Y, snapshot.YAxis));
                    if (!Current()) return;
                    labelContext = labelContext with { Text = FormatDataLabel(series, labelContext) };
                    if (!Current()) return;
                    var key = new LabelTemplateKey(series, data.SourceIndex);
                    if (_labelTemplateEntries.TryGetValue(key, out var entry) && !ReferenceEquals(entry.Template, template))
                    { ReleaseLabelEntry(key, entry); entry = null; }
                    if (entry is null)
                    {
                        var child = template.Build(labelContext);
                        if (!Current()) return;
                        if (child is not null && (((ILogical)child).LogicalParent is not null || child.GetVisualParent() is not null))
                            throw new InvalidOperationException("A data label template must return an unparented control.");
                        var border = child is null ? null : new Border { IsHitTestVisible = false, Focusable = false, Padding = new Thickness(3, 2), ClipToBounds = true };
                        entry = new LabelTemplateEntry(template, border);
                        _labelTemplateEntries.Add(key, entry);
                        if (border is not null)
                        {
                            border.Child = child;
                            if (!Current()) { DiscardStaleLabelBorder(border); return; }
                            layer.Children.Add(border);
                            if (!Current()) { DiscardStaleLabelBorder(border); return; }
                        }
                    }
                    retained.Add(key);
                    if (entry.Border is not { } container) continue;
                    container.DataContext = labelContext;
                    if (!Current()) return;
                    container.Background = series.DataLabelBackground;
                    TextElement.SetForeground(container, series.DataLabelForeground ?? Foreground ?? Brushes.SlateGray);
                    TextElement.SetFontFamily(container, series.DataLabelFontFamily ?? FontFamily);
                    TextElement.SetFontSize(container, series.DataLabelFontSize ?? FontSize);
                    TextElement.SetFontStyle(container, FontStyle);
                    TextElement.SetFontWeight(container, FontWeight);
                    container.Measure(new Size(Math.Max(0, Math.Min(320, scene.Plot.Width)), scene.Plot.Height));
                    if (!Current()) return;
                    var labelSize = container.DesiredSize;
                    var rect = PlaceTemplateLabel(series, anchor, labelSize, bar, below, scene.Plot, horizontal, DisplayedPointRadius(series, data.MarkerDiameter));
                    container.IsVisible = rect.HasValue;
                    if (rect is { } bounds)
                    { layer.Slots[container] = bounds; _templateLabelRectangles.Add(bounds); }
                }
            }
            buildingSeries = null; buildingTemplate = null;
            foreach (var pair in _labelTemplateEntries.ToArray())
                if (!retained.Contains(pair.Key)) ReleaseLabelEntry(pair.Key, pair.Value);
            if (!Current()) return;
            host.RemoveUnusedLayers(activeLayers);
            foreach (var series in _failedLabelTemplates.Keys.ToArray())
                if (!ActiveSeries.Contains(series) || series.DataLabelTemplate is null) _failedLabelTemplates.Remove(series);
            if (activeLayers.Count == 0) { ReleaseLabelPresentationVisual(); _labelTemplatesDirty = false; return; }
            host.InvalidateMeasure(); host.InvalidateArrange();
            host.Measure(size);
            if (Current()) _labelTemplatesDirty = false;
        }
        catch
        {
            if (Current())
            {
                try { ReleaseLabelPresentationVisual(); }
                finally
                {
                    if (buildingSeries is not null && buildingTemplate is not null)
                        _failedLabelTemplates[buildingSeries] = buildingTemplate;
                }
            }
            throw;
        }
        finally
        {
            _synchronizingLabelTemplates = false;
            if (revision != _labelTemplateRevision)
            { ReleaseLabelPresentationVisual(); InvalidateMeasure(); }
        }
    }

    private static string? FormatDataLabel(ChartSeries series, ChartDataLabelContext context)
    {
        if (series.DataLabelFormatter is { } formatter) return formatter(context);
        var horizontal = series is IChartComparisonSeries { Orientation: Avalonia.Layout.Orientation.Horizontal };
        var formatted = horizontal ? context.FormattedX : context.FormattedY;
        var value = horizontal ? context.Data.Point.X : context.Data.Point.Y;
        try { return series.DataLabelFormat is { } format ? value.ToString(format, CultureInfo.CurrentCulture) : formatted; }
        catch (FormatException) { return formatted; }
    }

    private Rect? PlaceTemplateLabel(ChartSeries series, Point anchor, Size size, Rect bar, bool below, Rect plot, bool horizontal, double markerRadius)
    {
        if (size.Width <= 0 || size.Height <= 0 || !double.IsFinite(size.Width) || !double.IsFinite(size.Height) || size.Height > plot.Height) return null;
        var position = series.DataLabelPosition;
        var alternatives = DataLabelPlacements(position, below, horizontal);
        foreach (var placement in alternatives)
        {
            var rect = LabelRectangle(anchor, size, placement, bar, below, markerRadius + 4, horizontal).Translate(series.DataLabelOffset);
            if (position == ChartDataLabelPosition.Auto)
                rect = new Rect(Math.Clamp(rect.X, plot.Left, Math.Max(plot.Left, plot.Right - rect.Width)),
                    Math.Clamp(rect.Y, plot.Top, Math.Max(plot.Top, plot.Bottom - rect.Height)), rect.Width, rect.Height);
            var visible = rect.Intersect(plot);
            if (visible.Width <= 0 || visible.Height <= 0) continue;
            if (series.DataLabelOverlap == ChartDataLabelOverlap.Hide && _templateLabelRectangles.Any(old => old.Intersects(rect))) continue;
            return rect;
        }
        return null;
    }

    private bool UseLabelTemplate(ChartSeries series) => series.DataLabelTemplate is { } template &&
        (!_failedLabelTemplates.TryGetValue(series, out var failed) || !ReferenceEquals(template, failed));

    private void EnsureLabelPresentationMembership()
    {
        if (_labelTemplateHost is not { } host) return;
        if (((ILogical)host).LogicalParent is null) LogicalChildren.Add(host);
        if (!ReferenceEquals(_labelTemplateHost, host)) return;
        if (host.GetVisualParent() is null) VisualChildren.Add(host);
    }

    private static void DiscardStaleLabelBorder(Border border)
    {
        try { border.Child = null; }
        finally { border.DataContext = null; }
    }

    private void ReleaseLabelEntry(LabelTemplateKey key, LabelTemplateEntry entry)
    {
        _labelTemplateEntries.Remove(key);
        if (entry.Border is not { } border) return;
        try { if (border.GetVisualParent() is Panel parent) parent.Children.Remove(border); }
        finally { DiscardStaleLabelBorder(border); }
    }

    private void ReleaseLabelPresentationVisual()
    {
        var host = _labelTemplateHost;
        _labelTemplateHost = null;
        _templateLabelRectangles.Clear();
        ExceptionDispatchInfo? failure = null;
        void Clean(Action action)
        {
            try { action(); }
            catch (Exception error) { failure ??= ExceptionDispatchInfo.Capture(error); }
        }
        foreach (var pair in _labelTemplateEntries.ToArray()) Clean(() => ReleaseLabelEntry(pair.Key, pair.Value));
        if (host is not null)
        {
            Clean(() => { if (ReferenceEquals(host.GetVisualParent(), this)) VisualChildren.Remove(host); });
            Clean(() => { if (ReferenceEquals(((ILogical)host).LogicalParent, this)) LogicalChildren.Remove(host); });
            Clean(host.Children.Clear); host.Layers.Clear();
        }
        failure?.Throw();
    }

    private void ClearLabelPresentation()
    {
        if (_clearingLabelTemplates) return;
        _clearingLabelTemplates = true;
        try
        {
            _labelTemplateRevision++; _labelTemplatesDirty = true;
            try { ReleaseLabelPresentationVisual(); }
            finally { _failedLabelTemplates.Clear(); }
        }
        finally { _clearingLabelTemplates = false; }
    }

    private void RenderLabelPresentationForExport(DrawingContext context)
    {
        if (_labelTemplateHost is not { } host || host.Bounds.Width <= 0 || host.Bounds.Height <= 0) return;
        context.DrawRectangle(new VisualBrush(host) { Stretch = Stretch.Fill, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top }, null, host.Bounds);
    }

    private readonly record struct LabelTemplateKey(ChartSeries Series, int SourceIndex);
    private sealed record LabelTemplateEntry(IDataTemplate Template, Border? Border);
    private sealed class LabelTemplateHost : Panel
    {
        public Dictionary<ChartSeries, LabelTemplateLayer> Layers { get; } = [];
        public LabelTemplateLayer GetLayer(ChartSeries series)
        {
            if (Layers.TryGetValue(series, out var layer)) return layer;
            layer = new LabelTemplateLayer { IsHitTestVisible = false, Focusable = false };
            Layers.Add(series, layer); Children.Add(layer); return layer;
        }
        public void RemoveUnusedLayers(HashSet<ChartSeries> active)
        {
            foreach (var pair in Layers.ToArray())
                if (!active.Contains(pair.Key)) { Children.Remove(pair.Value); Layers.Remove(pair.Key); }
        }
        protected override Size MeasureOverride(Size availableSize)
        { foreach (var child in Children) child.Measure(availableSize); return default; }
        protected override Size ArrangeOverride(Size finalSize)
        { foreach (var child in Children) child.Arrange(new Rect(finalSize)); return finalSize; }
    }
    private sealed class LabelTemplateLayer : Panel
    {
        public Dictionary<Control, Rect> Slots { get; } = [];
        protected override Size MeasureOverride(Size availableSize) => default;
        protected override Size ArrangeOverride(Size finalSize)
        { foreach (var pair in Slots) pair.Key.Arrange(pair.Value); return finalSize; }
    }
}
