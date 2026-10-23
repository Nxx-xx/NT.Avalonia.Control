using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>Resolved label fit and reserved chart-local bounds for one axis.</summary>
public sealed record ChartAxisLayout(ChartAxisLabelMode Mode, double Rotation, int Rows, int Stride,
    double LabelWidth, double LabelHeight, double TextWidth, double RowHeight)
{
    public Rect Bounds { get; init; }
}

public sealed partial class CartesianChart
{
    /// <summary>Returns the effective Auto/explicit label layout after the current chart layout.</summary>
    public ChartAxisLayout? GetAxisLayout(ChartAxis axis)
    {
        VerifyAccess(); ArgumentNullException.ThrowIfNull(axis);
        return GetScene()?.Layouts.GetValueOrDefault(axis);
    }

    private static void MapCategories(SeriesSnapshot[] snapshots, Dictionary<ChartAxis, IReadOnlyList<string>> categories, bool horizontal)
    {
        foreach (var axis in snapshots.Select(item => horizontal ? item.XAxis : item.YAxis).Distinct().OfType<CategoryAxis>())
        {
            if (categories.ContainsKey(axis)) continue;
            var labels = axis.Labels?.ToList() ?? [];
            var indexes = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var index = 0; index < labels.Count; index++) indexes.TryAdd(labels[index] ?? string.Empty, index);
            foreach (var snapshot in snapshots.Where(item => ReferenceEquals(horizontal ? item.XAxis : item.YAxis, axis)))
            {
                var mapped = snapshot.Data.Points.Select(item =>
                {
                    var categoryDimension = horizontal != HorizontalComparison(snapshot.Series);
                    var value = categoryDimension ? item.Category : item.Value ?? (horizontal ? item.Point.X : item.Point.Y);
                    var path = categoryDimension ? snapshot.Series.CategoryPath : snapshot.Series.ValuePath;
                    if (axis.Labels is not null && string.IsNullOrWhiteSpace(path) && value is not string) return item;
                    if (!categoryDimension && value is double number && !double.IsFinite(number)) return item;
                    var label = Convert.ToString(value, CultureInfo.CurrentCulture) ?? item.SourceIndex.ToString(CultureInfo.CurrentCulture);
                    double coordinate;
                    if (indexes.TryGetValue(label, out var slot)) coordinate = slot;
                    else if (axis.Labels is not null) coordinate = double.NaN;
                    else { coordinate = labels.Count; indexes.Add(label, labels.Count); labels.Add(label); }
                    var point = horizontal ? item.Point with { X = coordinate } : item.Point with { Y = coordinate };
                    return item with { Point = point, IsEmpty = !IsValid(point) || item.IsEmpty && IsValid(item.Point) };
                }).ToArray();
                snapshot.Data = ChartDataSnapshot.FromOwned(snapshot.Data.Version, mapped, snapshot.Data.Issues);
            }
            categories[axis] = labels;
        }
    }

    private static void FilterAxisDomains(SeriesSnapshot[] snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            var xBreaks = snapshot.XAxis.GetMergedScaleBreaks(); var yBreaks = snapshot.YAxis.GetMergedScaleBreaks();
            static bool Linear(ChartAxis axis) => axis.GetType() == typeof(ChartAxis) || axis is NumericAxis or CategoryAxis;
            if (xBreaks.Count == 0 && yBreaks.Count == 0 && Linear(snapshot.XAxis) && Linear(snapshot.YAxis)) continue;
            ChartDataValue[]? filtered = null;
            for (var index = 0; index < snapshot.Data.Count; index++)
            {
                var item = snapshot.Data.GetValue(index);
                var valid = snapshot.XAxis.IsValidValue(item.Point.X) && snapshot.YAxis.IsValidValue(item.Point.Y) &&
                    !ChartAxis.ContainsScaleBreak(xBreaks, item.Point.X) && !ChartAxis.ContainsScaleBreak(yBreaks, item.Point.Y);
                if (snapshot.Series is IChartRangeSeries && snapshot.Data.GetBandLower(index) is { } lower)
                    valid &= snapshot.YAxis.IsValidValue(lower) && !ChartAxis.ContainsScaleBreak(yBreaks, lower);
                if (snapshot.Series is RangeBarSeries or VarianceSeries && snapshot.Data.GetBandLower(index) is { } bound)
                    valid &= ComparisonValueAxis(snapshot).IsValidValue(bound) && !ComparisonValueAxis(snapshot).IsInScaleBreak(bound);
                if (!valid && !(item.IsEmpty && double.IsNaN(item.Point.Y)))
                {
                    if (filtered is null)
                    {
                        filtered = new ChartDataValue[snapshot.Data.Count];
                        for (var previous = 0; previous < index; previous++) filtered[previous] = snapshot.Data.GetValue(previous);
                    }
                    item = item with { Point = HorizontalComparison(snapshot.Series) ? item.Point with { X = double.NaN } : item.Point with { Y = double.NaN }, IsEmpty = true };
                }
                if (filtered is not null) filtered[index] = item;
            }
            if (filtered is not null) snapshot.Data = snapshot.Data.WithValues(filtered);
        }
    }

    private double AxisFontSize(ChartAxis axis) => axis.LabelFontSize ?? FontSize;
    private readonly Dictionary<AxisTextCacheKey, FormattedText> _axisTexts = [];
    private readonly LinkedList<AxisTextCacheKey> _axisTextKeys = [];
    private long _axisTextBuildCount;
    private readonly Dictionary<FormattedText, AxisTextDrawing> _axisTextDrawings = new(ReferenceEqualityComparer.Instance);
    private sealed class AxisTextDrawing
    {
        public required IBrush Brush { get; init; }
        public Point Origin { get; set; }
        public DrawingGroup? Drawing { get; set; }
    }
    private readonly record struct AxisBrushIdentity(IBrush Brush)
    {
        public bool Equals(AxisBrushIdentity other) => ReferenceEquals(Brush, other.Brush);
        public override int GetHashCode() => RuntimeHelpers.GetHashCode(Brush);
    }
    private readonly record struct AxisTextCacheKey(ChartAxis Axis, string Text, bool Title, string Culture,
        Typeface Typeface, double Size, AxisBrushIdentity Brush, double? Width, int? Lines);

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private FormattedText AxisText(string text, ChartAxis axis, bool title = false, double? maxWidth = null, int? maxLines = null)
    {
        var culture = CultureInfo.CurrentCulture;
        var typeface = new Typeface((title ? axis.TitleFontFamily : axis.LabelFontFamily) ?? FontFamily, FontStyle, FontWeight);
        var size = (title ? axis.TitleFontSize : axis.LabelFontSize) ?? FontSize;
        var brush = (title ? axis.TitleBrush : axis.LabelBrush) ?? Foreground ?? Brushes.SlateGray;
        var key = new AxisTextCacheKey(axis, text, title, culture.Name, typeface, size, new(brush), maxWidth, maxLines);
        // Detached arranged charts are also valid render/export inputs, but do not
        // subscribe to axis style changes. Keep their per-draw style reads fresh.
        if (_attached && _axisTexts.TryGetValue(key, out var cached))
        { TouchResource(ResourceCacheKind.AxisText, key); return cached; }
        _axisTextBuildCount++;
        var formatted = new FormattedText(text, culture, FlowDirection.LeftToRight, typeface, size, brush);
        if (maxWidth is { } width) formatted.MaxTextWidth = width;
        if (maxLines is { } lines) formatted.MaxLineCount = lines;
        // Pure data revisions can reuse unchanged labels. Style/layout/axis invalidations
        // still clear the bounded cache, and every resolved text style is also in its key.
        // Wrapped drawing never mutates the instance used by unwrapped measurement.
        if (_attached && TryRetainResource(ResourceCacheKind.AxisText, key, EstimateTextResource(text), () => RemoveAxisText(key)))
        {
            while (_axisTexts.Count >= 256 && _axisTextKeys.First is { } first) EvictResource(ResourceCacheKind.AxisText, first.Value);
            _axisTexts.Add(key, formatted);
            _axisTextKeys.AddLast(key);
            _axisTextDrawings.Add(formatted, new() { Brush = brush });
        }
        return formatted;
    }

    private void RemoveAxisText(AxisTextCacheKey key)
    {
        if (_axisTexts.Remove(key, out var text)) _axisTextDrawings.Remove(text);
        _axisTextKeys.Remove(key);
    }

    private void ReleaseAxisTextCaches()
    {
        ReleaseResourceKind(ResourceCacheKind.AxisText);
        _axisTexts.Clear(); _axisTextKeys.Clear(); _axisTextDrawings.Clear();
        ReleaseAxisPenCache();
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private void DrawAxisText(DrawingContext drawing, FormattedText formatted, Point origin)
    {
        if (!_axisTextDrawings.TryGetValue(formatted, out var cached))
        {
            drawing.DrawText(formatted, origin);
            return;
        }
        if (cached.Drawing is null || cached.Origin != origin)
        {
            var group = new DrawingGroup();
            using (var recording = group.Open()) recording.DrawText(formatted, origin);
            // Retain only glyphs and their original local transforms/clips. Replaying
            // them reads the current brush reference, just like FormattedText.Draw;
            // a mutable color, opacity, or gradient transform is never snapshotted.
            if (!RestoreGlyphBrushes(group, cached.Brush))
            {
                _axisTextDrawings.Remove(formatted);
                drawing.DrawText(formatted, origin);
                return;
            }
            cached.Origin = origin;
            cached.Drawing = group;
        }
        cached.Drawing.Draw(drawing);
    }

    private static bool RestoreGlyphBrushes(Drawing drawing, IBrush brush)
    {
        if (drawing is GlyphRunDrawing glyph)
        {
            glyph.Foreground = brush;
            return true;
        }
        if (drawing is not DrawingGroup group) return false;
        foreach (var child in group.Children)
            if (!RestoreGlyphBrushes(child, brush)) return false;
        return true;
    }

    // Short interactive layouts cannot rely on a long-loop OSR transition.
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private ChartAxisLayout MeasureAxis(ChartAxis axis, ChartScale scale, double length, bool horizontal)
    {
        if (!axis.IsVisible || !axis.ShowLabels || axis.LabelMode == ChartAxisLabelMode.Hide) return new(ChartAxisLabelMode.Hide, 0, 0, 1, 0, 0, 0, 0);
        var texts = scale.Ticks.Select(value => AxisText(scale.FormatLabel(value, axis), axis)).ToArray();
        var size = AxisFontSize(axis);
        var maxWidth = Math.Max(horizontal ? 1 : AxisText("-0000.00", axis).Width, texts.Select(text => text.Width).DefaultIfEmpty(size).Max());
        var maxText = Math.Min(axis.LabelMaxWidth, Math.Max(1, Bounds.Width));
        var textWidth = Math.Min(maxText, maxWidth);
        var textHeight = texts.Select(text => text.Height + 2).DefaultIfEmpty(size * 1.5).Max();
        var angle = axis.LabelRotation % 360;
        var rows = 1;
        var mode = axis.LabelMode;
        var spacing = length / Math.Max(1, scale.Ticks.Count - 1);
        var wrap = axis.LabelWrap || mode == ChartAxisLabelMode.Wrap;
        if (mode == ChartAxisLabelMode.Auto)
        {
            if (wrap) mode = ChartAxisLabelMode.Wrap;
            else if (angle != 0) mode = ChartAxisLabelMode.Rotate;
            else if (horizontal && textWidth + 12 > spacing)
            {
                if (textWidth + 12 <= spacing * 2) { mode = ChartAxisLabelMode.Stagger; rows = 2; }
                else if (textWidth <= spacing * 3 && spacing >= size * 2) { mode = ChartAxisLabelMode.Wrap; wrap = true; }
                else { mode = ChartAxisLabelMode.Rotate; angle = 45; }
            }
            else if (!horizontal && textHeight + 6 > spacing) { mode = ChartAxisLabelMode.Stagger; rows = 2; }
            else mode = ChartAxisLabelMode.None;
        }
        if (mode == ChartAxisLabelMode.Stagger) rows = 2;
        if (mode == ChartAxisLabelMode.Rotate && angle == 0) angle = 45;
        if (mode == ChartAxisLabelMode.Wrap || wrap)
        {
            wrap = true; mode = ChartAxisLabelMode.Wrap;
            textWidth = Math.Max(1, Math.Min(textWidth, horizontal ? Math.Max(size * 2, spacing - 8) : maxText));
            textHeight = size * 4.5;
        }
        else if (angle != 0) textWidth = maxText; // explicit maximum also reserves enough space for clipped rotated labels
        var radians = angle * Math.PI / 180;
        var rotatedWidth = Math.Abs(Math.Cos(radians)) * textWidth + Math.Abs(Math.Sin(radians)) * textHeight;
        var rotatedHeight = Math.Abs(Math.Sin(radians)) * textWidth + Math.Abs(Math.Cos(radians)) * textHeight;
        var capacity = horizontal ? Math.Max(1, length / Math.Max(1, rotatedWidth + 10) * rows) :
            Math.Max(1, length / Math.Max(1, rotatedHeight + 6) * rows);
        var stride = mode == ChartAxisLabelMode.None ? 1 : Math.Max(1, (int)Math.Ceiling(scale.Ticks.Count / capacity));
        return new(mode, angle, rows, stride, rotatedWidth * (horizontal ? 1 : rows), rotatedHeight * (horizontal ? rows : 1), textWidth, rotatedHeight);
    }

    private double AxisGutter(ChartAxis axis, ChartAxisLayout layout, bool horizontal) => !axis.IsVisible ? 0 :
        (layout.Mode == ChartAxisLabelMode.Hide ? 0 : (horizontal ? layout.LabelHeight : layout.LabelWidth) + 8) +
        TickOffset(axis) + (axis.ShowAxis ? axis.AxisThickness : 0) +
        (horizontal && axis.ShowTitle && !string.IsNullOrWhiteSpace(axis.Title) ? (axis.TitleFontSize ?? FontSize) * 1.5 + 8 : 0) + 2;

    private static double TickOffset(ChartAxis axis) => axis.ShowTicks ? Math.Max(axis.TickLength, axis.MinorTickLength) : 0;

    private void PositionAxisLayouts(Rect plot, ChartAxis[] xAxes, ChartAxis[] yAxes, Dictionary<ChartAxis, ChartAxisLayout> layouts)
    {
        var near = 0.0; var far = 0.0;
        foreach (var axis in yAxes.OrderBy(axis => ReferenceEquals(axis, YAxis) ? 0 : 1))
        {
            var width = AxisGutter(axis, layouts[axis], false);
            var isNear = axis.Position == ChartAxisPosition.Near;
            layouts[axis] = layouts[axis] with { Bounds = new Rect(isNear ? plot.Left - near - width : plot.Right + far, plot.Top, width, plot.Height) };
            if (isNear) near += width; else far += width;
        }
        near = far = 0;
        foreach (var axis in xAxes.OrderBy(axis => ReferenceEquals(axis, XAxis) ? 0 : 1))
        {
            var height = AxisGutter(axis, layouts[axis], true);
            var isNear = axis.Position == ChartAxisPosition.Near;
            layouts[axis] = layouts[axis] with { Bounds = new Rect(plot.Left, isNear ? plot.Bottom + near : plot.Top - far - height, plot.Width, height) };
            if (isNear) near += height; else far += height;
        }
    }

    // Axis records are short UI work; they do not reach a long-loop OSR entry.
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private void RenderAxes(DrawingContext drawing, ChartScene scene)
    {
        var plot = scene.Plot;
        var near = 0.0; var far = 0.0;
        foreach (var axis in new[] { YAxis }.Concat(scene.ExtraYAxes))
        {
            if (!axis.IsVisible) continue;
            var layout = scene.Layouts[axis]; var scale = scene.Scales[axis];
            var isNear = axis.Position == ChartAxisPosition.Near;
            var x = isNear ? plot.Left - near : plot.Right + far;
            RenderGrid(drawing, scene, axis, false);
            DrawTicks(drawing, axis, scale, plot, false, x);
            var columnWidth = layout.LabelWidth / Math.Max(1, layout.Rows);
            var labelLayout = columnWidth == layout.LabelWidth ? layout : layout with { LabelWidth = columnWidth };
            foreach (var pair in scale.Ticks.Select((tick, index) => (tick, index)))
            {
                if (layout.Mode == ChartAxisLabelMode.Hide || pair.index % layout.Stride != 0) continue;
                var y = scale.Map(pair.tick, plot.Bottom, plot.Top);
                if (y < plot.Top - .5 || y > plot.Bottom + .5) continue;
                var row = pair.index / layout.Stride % Math.Max(1, layout.Rows);
                var origin = new Point(isNear ? x - (row + 1) * columnWidth - TickOffset(axis) - 4 : x + TickOffset(axis) + 4 + row * columnWidth, y - layout.RowHeight / 2);
                DrawAxisLabel(drawing, scale.FormatLabel(pair.tick, axis), origin, labelLayout, axis);
            }
            if (axis.ShowAxis) drawing.DrawLine(AxisPen(axis), new Point(x, plot.Top), new Point(x, plot.Bottom));
            if (axis.ShowTitle && !string.IsNullOrWhiteSpace(axis.Title))
            {
                var text = AxisText(axis.Title, axis, true);
                var titleX = isNear ? Math.Max(scene.Left, x - Math.Max(layout.LabelWidth, text.Width)) : x + 4;
                DrawAxisText(drawing, text, new Point(titleX, plot.Top - text.Height - 8));
            }
            DrawBreakMarks(drawing, axis, scale, plot, false, x);
            var gutter = AxisGutter(axis, layout, false);
            if (isNear) near += gutter; else far += gutter;
        }
        near = far = 0;
        foreach (var axis in new[] { XAxis }.Concat(scene.ExtraXAxes))
        {
            if (!axis.IsVisible) continue;
            var layout = scene.Layouts[axis]; var scale = scene.Scales[axis];
            var isNear = axis.Position == ChartAxisPosition.Near;
            var y = isNear ? plot.Bottom + near : plot.Top - far;
            RenderGrid(drawing, scene, axis, true);
            DrawTicks(drawing, axis, scale, plot, true, y);
            foreach (var pair in scale.Ticks.Select((tick, index) => (tick, index)))
            {
                if (layout.Mode == ChartAxisLabelMode.Hide || pair.index % layout.Stride != 0) continue;
                var x = scale.Map(pair.tick, plot.Left, plot.Right);
                if (x < plot.Left - .5 || x > plot.Right + .5) continue;
                var row = pair.index / layout.Stride % Math.Max(1, layout.Rows);
                var labelY = isNear ? y + TickOffset(axis) + 4 + row * layout.RowHeight : y - TickOffset(axis) - 4 - layout.LabelHeight + row * layout.RowHeight;
                var labelX = Math.Clamp(x - layout.LabelWidth / 2, scene.Left, Math.Max(scene.Left, scene.Right - layout.LabelWidth));
                DrawAxisLabel(drawing, scale.FormatLabel(pair.tick, axis), new Point(labelX, labelY), layout, axis);
            }
            if (axis.ShowAxis) drawing.DrawLine(AxisPen(axis), new Point(plot.Left, y), new Point(plot.Right, y));
            if (axis.ShowTitle && !string.IsNullOrWhiteSpace(axis.Title))
            {
                var text = AxisText(axis.Title, axis, true);
                DrawAxisText(drawing, text, new Point(plot.Center.X - text.Width / 2, isNear ? y + AxisGutter(axis, layout, true) - text.Height : y - AxisGutter(axis, layout, true)));
            }
            DrawBreakMarks(drawing, axis, scale, plot, true, y);
            var gutter = AxisGutter(axis, layout, true);
            if (isNear) near += gutter; else far += gutter;
        }
    }

    private Pen AxisPen(ChartAxis axis) => CachedAxisPen(axis.AxisBrush ?? AxisBrush, axis.AxisThickness,
        axis.AxisDashStyle, axis.AxisLineCap, axis.AxisLineJoin);

    private void DrawTicks(DrawingContext drawing, ChartAxis axis, ChartScale scale, Rect plot, bool horizontal, double edge)
    {
        if (!axis.ShowTicks) return;
        var direction = axis.Position == ChartAxisPosition.Near ? (horizontal ? 1 : -1) : (horizontal ? -1 : 1);
        void Draw(IEnumerable<double> ticks, double length, IBrush? brush)
        {
            var pen = CachedAxisPen(brush ?? axis.AxisBrush ?? AxisBrush, axis.TickThickness);
            foreach (var tick in ticks)
            {
                var value = horizontal ? scale.Map(tick, plot.Left, plot.Right) : scale.Map(tick, plot.Bottom, plot.Top);
                if (value < (horizontal ? plot.Left : plot.Top) - .5 || value > (horizontal ? plot.Right : plot.Bottom) + .5) continue;
                drawing.DrawLine(pen, horizontal ? new Point(value, edge) : new Point(edge, value),
                    horizontal ? new Point(value, edge + direction * length) : new Point(edge + direction * length, value));
            }
        }
        Draw(scale.Ticks, axis.TickLength, axis.TickBrush);
        Draw(scale.MinorTicks, axis.MinorTickLength, axis.MinorTickBrush);
    }

    private void RenderGrid(DrawingContext drawing, ChartScene scene, ChartAxis axis, bool horizontal)
    {
        if (!ShowGridLines) return;
        var scale = scene.Scales[axis]; var plot = scene.Plot;
        void Draw(IEnumerable<double> ticks, IBrush? brush, double thickness, IDashStyle? dash, PenLineCap cap, PenLineJoin join)
        {
            var pen = CachedAxisPen(brush, thickness, dash, cap, join);
            foreach (var tick in ticks)
            {
                var value = horizontal ? scale.Map(tick, plot.Left, plot.Right) : scale.Map(tick, plot.Bottom, plot.Top);
                if (value < (horizontal ? plot.Left : plot.Top) - .5 || value > (horizontal ? plot.Right : plot.Bottom) + .5) continue;
                drawing.DrawLine(pen, horizontal ? new Point(value, plot.Top) : new Point(plot.Left, value),
                    horizontal ? new Point(value, plot.Bottom) : new Point(plot.Right, value));
            }
        }
        var geometry = CreateBreakClip(plot, scene.XScale, scene.YScale);
        if (geometry is not null)
        {
            using var clip = drawing.PushGeometryClip(geometry);
            if (axis.ShowGridLines) Draw(scale.Ticks, axis.GridLineBrush ?? GridLineBrush, axis.GridLineThickness, axis.GridLineDashStyle, axis.GridLineCap, axis.GridLineJoin);
            if (axis.ShowMinorGridLines) Draw(scale.MinorTicks, axis.MinorGridLineBrush ?? axis.GridLineBrush ?? GridLineBrush, axis.MinorGridLineThickness, axis.MinorGridLineDashStyle, axis.MinorGridLineCap, axis.MinorGridLineJoin);
        }
        else
        {
            if (axis.ShowGridLines) Draw(scale.Ticks, axis.GridLineBrush ?? GridLineBrush, axis.GridLineThickness, axis.GridLineDashStyle, axis.GridLineCap, axis.GridLineJoin);
            if (axis.ShowMinorGridLines) Draw(scale.MinorTicks, axis.MinorGridLineBrush ?? axis.GridLineBrush ?? GridLineBrush, axis.MinorGridLineThickness, axis.MinorGridLineDashStyle, axis.MinorGridLineCap, axis.MinorGridLineJoin);
        }
    }

    private void DrawAxisLabel(DrawingContext drawing, string text, Point origin, ChartAxisLayout layout, ChartAxis axis)
    {
        var wrap = layout.Mode == ChartAxisLabelMode.Wrap || axis.LabelWrap;
        var formatted = AxisText(text, axis, maxWidth: wrap ? layout.TextWidth : null, maxLines: wrap ? 3 : null);
        var height = Math.Min(formatted.Height + 2, AxisFontSize(axis) * (layout.Mode == ChartAxisLabelMode.Wrap ? 4.5 : 2));
        var rotation = Matrix.CreateRotation(layout.Rotation * Math.PI / 180);
        var bounds = new Rect(0, 0, layout.TextWidth, height).TransformToAABB(rotation);
        using var transform = drawing.PushTransform(rotation * Matrix.CreateTranslation(origin.X + (layout.LabelWidth - bounds.Width) / 2 - bounds.Left, origin.Y - bounds.Top));
        using var clip = drawing.PushClip(new Rect(0, 0, layout.TextWidth, height));
        DrawAxisText(drawing, formatted, new Point((layout.TextWidth - formatted.Width) / 2, 0));
    }

    private static Geometry? CreateBreakClip(Rect plot, ChartScale x, ChartScale y)
    {
        if (x.MergedBreaks.Count == 0 && y.MergedBreaks.Count == 0) return null;
        Geometry? result = null;
        foreach (var rectangle in BreakRectangles(plot, x, true).Concat(BreakRectangles(plot, y, false)))
            result = new CombinedGeometry(GeometryCombineMode.Exclude, result ?? new RectangleGeometry(plot), new RectangleGeometry(rectangle));
        return result;
    }

    private static IEnumerable<Rect> BreakRectangles(Rect plot, ChartScale scale, bool horizontal, ChartScaleBreakRange? only = null)
    {
        foreach (var gap in only is { } single ? new[] { single } : scale.MergedBreaks)
        {
            var start = Math.Max(gap.Start, scale.Minimum);
            var end = Math.Min(gap.End, scale.Maximum);
            if (start >= end) continue;
            var first = horizontal ? scale.Map(start, plot.Left, plot.Right) : scale.Map(start, plot.Bottom, plot.Top);
            var last = horizontal ? scale.Map(end, plot.Left, plot.Right) : scale.Map(end, plot.Bottom, plot.Top);
            var low = Math.Max(horizontal ? plot.Left : plot.Top, Math.Min(first, last));
            var high = Math.Min(horizontal ? plot.Right : plot.Bottom, Math.Max(first, last));
            if (high <= low) continue;
            yield return horizontal ? new Rect(low, plot.Top, high - low, plot.Height) : new Rect(plot.Left, low, plot.Width, high - low);
        }
    }

    private void DrawBreakMarks(DrawingContext drawing, ChartAxis axis, ChartScale scale, Rect plot, bool horizontal, double edge)
    {
        if (!axis.ShowAxis) return;
        foreach (var gap in scale.MergedBreaks)
        {
            var matching = axis.ActiveScaleBreaks.FirstOrDefault(item => item.Start < gap.End && item.End > gap.Start);
            var pen = CachedAxisPen(matching?.Stroke ?? axis.AxisBrush ?? AxisBrush, matching?.StrokeThickness ?? Math.Max(1, axis.AxisThickness));
            var rect = BreakRectangles(plot, scale, horizontal, gap).FirstOrDefault();
            if (rect.Width <= 0 || rect.Height <= 0) continue;
            var value = horizontal ? rect.Center.X : rect.Center.Y;
            drawing.DrawLine(pen, horizontal ? new Point(value - 4, edge + 3) : new Point(edge - 3, value - 4),
                horizontal ? new Point(value, edge - 3) : new Point(edge + 3, value));
            drawing.DrawLine(pen, horizontal ? new Point(value, edge + 3) : new Point(edge - 3, value),
                horizontal ? new Point(value + 4, edge - 3) : new Point(edge + 3, value + 4));
        }
    }

    private void RenderPlotBands(DrawingContext drawing, ChartScene scene, ChartPlotBandLayer layer)
    {
        using var plotClip = drawing.PushClip(scene.Plot);
        foreach (var entry in scene.Scales)
        {
            var axis = entry.Key; var scale = entry.Value;
            var horizontal = ReferenceEquals(axis, XAxis) || scene.ExtraXAxes.Contains(axis);
            if (!axis.IsVisible) continue;
            foreach (var band in axis.ActivePlotBands.Where(band => band.IsVisible && band.Layer == layer))
            {
                var start = double.IsNaN(band.Start) ? scale.Minimum : band.Start;
                var end = double.IsNaN(band.End) ? scale.Maximum : band.End;
                if (!double.IsFinite(start) || !double.IsFinite(end) || start >= end) continue;
                var count = Math.Clamp(band.RepeatCount, 0, 10_000);
                var interval = band.RepeatInterval is { } repeat && double.IsFinite(repeat) && repeat > 0 ? repeat : 0;
                if (interval == 0) count = Math.Min(count, 1);
                else if (count > 0 && band.RepeatUntil is { } until) count = (int)Math.Clamp(Math.Ceiling((until - start) / interval), 0, 10_000);
                for (var index = 0; index < count; index++)
                {
                    var first = Math.Max(start + interval * index, scale.Minimum);
                    var last = Math.Min(end + interval * index, Math.Min(band.RepeatUntil ?? scale.Maximum, scale.Maximum));
                    if (first >= last || !axis.IsValidValue(first) || !axis.IsValidValue(last)) continue;
                    var a = horizontal ? scale.Map(first, scene.Plot.Left, scene.Plot.Right) : scale.Map(first, scene.Plot.Bottom, scene.Plot.Top);
                    var b = horizontal ? scale.Map(last, scene.Plot.Left, scene.Plot.Right) : scale.Map(last, scene.Plot.Bottom, scene.Plot.Top);
                    var low = Math.Max(horizontal ? scene.Plot.Left : scene.Plot.Top, Math.Min(a, b));
                    var high = Math.Min(horizontal ? scene.Plot.Right : scene.Plot.Bottom, Math.Max(a, b));
                    if (high <= low) continue;
                    var rectangle = horizontal ? new Rect(low, scene.Plot.Top, high - low, scene.Plot.Height) : new Rect(scene.Plot.Left, low, scene.Plot.Width, high - low);
                    using var opacity = drawing.PushOpacity(band.Opacity);
                    drawing.DrawRectangle(band.Fill, band.Stroke is null ? null : CachedAxisPen(band.Stroke, band.StrokeThickness), rectangle);
                    if (string.IsNullOrEmpty(band.Text)) continue;
                    var text = CachedInteractionText(band.Text, CultureInfo.CurrentCulture,
                        new Typeface(FontFamily), band.FontSize ?? FontSize, band.Foreground ?? axis.LabelBrush ?? Foreground ?? Brushes.SlateGray);
                    using var labelClip = drawing.PushClip(rectangle);
                    var textX = band.HorizontalTextAlignment switch
                    { ChartBandTextAlignment.Start => rectangle.Left + 5, ChartBandTextAlignment.End => rectangle.Right - text.Width - 5, _ => rectangle.Center.X - text.Width / 2 };
                    var textY = band.VerticalTextAlignment switch
                    { ChartBandTextAlignment.Start => rectangle.Top + 5, ChartBandTextAlignment.End => rectangle.Bottom - text.Height - 5, _ => rectangle.Center.Y - text.Height / 2 };
                    DrawInteractionText(drawing, text, new Point(textX, textY));
                }
            }
        }
    }
}
