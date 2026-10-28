using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

public partial class ShapeMap
{
    private sealed record PaintedPolygon(Point[] Outer, Point[][] Holes);
    private sealed record PaintedSector(Point Center, double Radius, double StartAngle, double SweepAngle);
    private sealed record PaintedMapMark(PaintedPolygon[] Polygons, Rect? Circle, (Point Start, Point End)[]? Segments = null, double LineRadius = 0, PaintedSector? Sector = null, Point[]? Endpoints = null, double EndpointRadius = 0);
    private sealed record MapPaintCommand(Geometry Geometry, IBrush? Fill, IBrush? Stroke, EngineeringMarkInfo? Mark, PaintedMapMark Primitive);
    protected override Rect GetPlotArea(Size size)
    {
        var plot = base.GetPlotArea(size);
        return ShowLegend ? new(plot.X, plot.Y, plot.Width, Math.Max(0, plot.Height - (UsesLegendItemTemplate && MaxLegendItems > 0 ? 64 : 30))) : plot;
    }
    private Point ProjectPoint(ChartPoint point, Rect plot)
    {
        var normalized = _worldBounds!.Value.Normalize(point); var fit = _viewport!.FittedArea;
        return ApplyViewport(new(fit.Left + normalized.X * fit.Width, fit.Bottom - normalized.Y * fit.Height), plot);
    }
    private Rect FitWorld(Rect plot)
    {
        var aspect = _worldBounds!.Value.AspectRatio;
        if (EffectiveProjection == MapProjection.Equirectangular) aspect = Math.Min(double.MaxValue, aspect * 2);
        if (aspect <= 0) return new(plot.Center.X, plot.Top, 0, plot.Height);
        var available = plot.Height == 0 ? double.MaxValue : plot.Width / plot.Height;
        var width = aspect > available ? plot.Width : plot.Height * aspect;
        var height = aspect > available ? plot.Width / aspect : plot.Height;
        return new(plot.Center.X - width / 2, plot.Center.Y - height / 2, width, height);
    }
    protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
    {
        _painted.Clear(); _indices.Clear(); _commands.Clear(); _commandIndices.Clear(); _labels.Clear(); _legends.Clear(); _categoryLegends.Clear(); _categoryLegendKeys.Clear();
        if (_disposed) return [];
        EnsureData(); if (_worldBounds is null || _projected is null || _dataDirty) return [];
        var categoryKeys = _inputs.SelectMany(i => i.Categories.Values.Select(c => (i.Layer as ShapeLayer, c))).ToHashSet();
        foreach (var key in _categoryIndices.Keys.ToArray()) if (!categoryKeys.Contains((key.Layer, key.Category))) _categoryIndices.Remove(key);
        _viewport = new(_worldBounds.Value, FitWorld(plot), Zoom, PanX, PanY);
        var marks = new List<EngineeringMarkInfo>();
        void Add(MapGlyphKey key, IReadOnlyList<ChartDataPoint> members, Geometry geometry, Point position, double value, IBrush? fill, IBrush? stroke, string text, PaintedMapMark painted)
        {
            if (fill is null) return;
            var mark = new EngineeringMarkInfo(members, geometry, fill, position) { Key = key, Value = value, TooltipText = text, Stroke = stroke };
            _indices.TryAdd(key, marks.Count); marks.Add(mark); _painted.Add(mark, painted);
            _commandIndices.Add(mark, _commands.Count); _commands.Add(new(geometry, fill, stroke, mark, painted));
            if (ShowLabels && !string.IsNullOrWhiteSpace(text)) _labels.Add((text, position, fill, mark));
        }
        foreach (var state in _layerSnapshots)
        {
            var layer = state.Layer; if (!layer.IsVisible || layer.Opacity <= 0) continue;
            var input = _inputs.First(i => ReferenceEquals(i.Layer, layer)); var original = input.Original;
            if (layer is ShapeLayer shape && state.Regions is { } join)
            {
                var finite = join.Regions.Where(r => r.Value is not null).Select(r => r.Value!.Value).ToArray();
                var low = finite.Length == 0 ? 0 : finite.Min(); var high = finite.Length == 0 ? 0 : finite.Max();
                if (finite.Length > 0 && shape.ColorMode == MapColorMode.Continuous) _legends.Add((layer, shape.MinimumColor, shape.MaximumColor, low, high));
                foreach (var region in join.Regions)
                {
                    var feature = _projected.Features[region.Feature.Index];
                    if (feature.Polygons.Count == 0) continue;
                    var polygons = feature.Polygons.Select(p => new PaintedPolygon(p.Outer.Select(v => ProjectPoint(v, plot)).ToArray(), p.Holes.Select(h => h.Select(v => ProjectPoint(v, plot)).ToArray()).ToArray())).ToArray();
                    var geometry = PolygonGeometry(polygons); var center = LabelPoint(polygons);
                    if (region.IsNoData || region.Data is null) { var noDataFill = WithOpacity(shape.NoDataBrush, layer.Opacity); _commands.Add(new(geometry, noDataFill, WithOpacity(layer.Stroke, layer.Opacity), null, new(polygons, null))); if (ShowLabels && feature.Entry.Label is { } label) _labels.Add((label, center, noDataFill, null)); continue; }
                    var row = region.Data; var raw = original.GetValueOrDefault(row.RegionId);
                    var category = input.Categories.GetValueOrDefault(row.RegionId);
                    IBrush mapped = Gradient(shape.MinimumColor, shape.MaximumColor, low == high ? .5 : Fraction(region.Value!.Value, low, high));
                    if (shape.ColorMode == MapColorMode.Categorical && category is not null)
                    {
                        var categoryKey = (shape, category); if (!_categoryIndices.TryGetValue(categoryKey, out var ordinal)) { ordinal = _categoryIndices.Count; _categoryIndices.Add(categoryKey, ordinal); }
                        mapped = shape.CategoryColors?.GetValueOrDefault(category) ?? BrushAt(ordinal);
                        if (_categoryLegendKeys.Add((shape, category))) _categoryLegends.Add((shape, category, WithOpacity(raw.Fill ?? mapped, layer.Opacity)!));
                    }
                    var fill = WithOpacity(raw.Fill ?? mapped, layer.Opacity);
                    var key = new MapGlyphKey(layer, MapElementKind.Region, row.RegionId, true);
                    var businessValue = shape.ColorMode == MapColorMode.Categorical ? ChartDataAccessor.TryNumber(Read(row.Source, shape.ValuePath), out var numeric) ? numeric : (double?)null : row.Value;
                    var member = new ChartDataPoint(row.SourceIndex, row.RegionId, row.Source, region.Feature.Label ?? row.RegionId.ToString(), default, false) { Value = businessValue, Weight = businessValue, PointFill = fill };
                    Add(key, Array.AsReadOnly(new[] { member }), geometry, center, region.Value!.Value, fill, WithOpacity(layer.Stroke, layer.Opacity), $"{region.Feature.Label ?? row.RegionId.ToString()}: {(shape.ColorMode == MapColorMode.Categorical ? category?.ToString() : F(region.Value.Value))}", new(polygons, null));
                }
            }
            if (layer is BubbleLayer && state.Bubbles is { } bubbles)
            {
                foreach (var bubble in bubbles.Points)
                {
                    if (bubble.Radius <= 0) continue;
                    var entry = bubble.Entry; var raw = original.GetValueOrDefault(entry.Id); var position = ProjectPoint(bubble.Position, plot);
                    var radius = bubble.Radius * Math.Min(_viewport.FittedArea.Width, _viewport.FittedArea.Height) * Zoom;
                    var bounds = new Rect(position.X - radius, position.Y - radius, radius * 2, radius * 2); var fill = WithOpacity(raw.Fill ?? layer.Fill, layer.Opacity);
                    var hasId = entry.Observation.Id is not null && !ReferenceEquals(entry.Observation.Id, entry.Source);
                    var key = new MapGlyphKey(layer, MapElementKind.Bubble, entry.Id, hasId);
                    var member = new ChartDataPoint(entry.SourceIndex, entry.Id, entry.Source, entry.Label, new(entry.Observation.Position.X, entry.Observation.Position.Y), false) { Value = raw.Value, Weight = entry.Value, SizeValue = entry.Value, MarkerDiameter = radius * 2, PointFill = fill };
                    Add(key, Array.AsReadOnly(new[] { member }), new EllipseGeometry(bounds), position, raw.Value, fill, WithOpacity(layer.Stroke, layer.Opacity), $"{entry.Label ?? entry.Id.ToString()}: {F(raw.Value)}", new([], bounds));
                }
            }
            if (layer is HeatmapLayer heat && state.Heatmap is { } grid)
            {
                var low = grid.Cells.Count == 0 ? 0 : grid.Cells.Min(c => c.Value); var high = grid.Cells.Count == 0 ? 0 : grid.Cells.Max(c => c.Value);
                if (grid.Cells.Count > 0) _legends.Add((layer, heat.MinimumColor, heat.MaximumColor, low, high));
                foreach (var cell in grid.Cells)
                {
                    var a = ProjectPoint(new(cell.Bounds.MinX, cell.Bounds.MinY), plot); var b = ProjectPoint(new(cell.Bounds.MaxX, cell.Bounds.MaxY), plot); var bounds = new Rect(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)); var polygon = new PaintedPolygon([bounds.TopLeft, bounds.TopRight, bounds.BottomRight, bounds.BottomLeft], []);
                    var fill = WithOpacity(Gradient(heat.MinimumColor, heat.MaximumColor, low == high ? .5 : Fraction(cell.Value, low, high)), layer.Opacity);
                    var members = cell.Members.Select(entry => new ChartDataPoint(entry.SourceIndex, entry.Id, entry.Source, entry.Label, new(entry.Observation.Position.X, entry.Observation.Position.Y), false) { Value = original.GetValueOrDefault(entry.Id).Value, Weight = entry.Value }).ToArray();
                    Add(new(layer, MapElementKind.HeatmapCell, (cell.Column, cell.Row), true), Array.AsReadOnly(members), new RectangleGeometry(bounds), bounds.Center, cell.Value, fill, WithOpacity(layer.Stroke, layer.Opacity), $"{cell.Members.Count} observations: {F(cell.Value)}", new([polygon], null));
                }
            }
            BuildOverlayMarks(state, plot, original, Add);
        }
        ReconcileMapSelection(marks);
        return marks.AsReadOnly();
    }
    private static IBrush Gradient(Color min, Color max, double fraction)
    {
        var t = Math.Clamp(fraction, 0, 1); byte C(byte a, byte b) => (byte)Math.Round(a + (b - a) * t);
        return new SolidColorBrush(Color.FromArgb(C(min.A, max.A), C(min.R, max.R), C(min.G, max.G), C(min.B, max.B)));
    }
    private static IBrush? WithOpacity(IBrush? brush, double opacity) => brush switch
    {
        ISolidColorBrush solid => new SolidColorBrush(solid.Color, solid.Opacity * opacity),
        null => null,
        _ when opacity == 1 => brush,
        _ => throw new ArgumentException("Layer opacity below one requires a solid color brush."),
    };
    private static Geometry PolygonGeometry(PaintedPolygon[] polygons)
    {
        var geometry = new StreamGeometry();
        using var path = geometry.Open(); path.SetFillRule(FillRule.EvenOdd);
        foreach (var polygon in polygons) foreach (var ring in new[] { polygon.Outer }.Concat(polygon.Holes))
        { if (ring.Length == 0) continue; path.BeginFigure(ring[0], true); for (var i = 1; i < ring.Length; i++) path.LineTo(ring[i]); path.EndFigure(true); }
        return geometry;
    }
    private static Point LabelPoint(PaintedPolygon[] polygons)
    {
        foreach (var polygon in polygons)
        {
            if (polygon.Outer.Length == 0) continue;
            var bounds = new Rect(polygon.Outer.Min(p => p.X), polygon.Outer.Min(p => p.Y), polygon.Outer.Max(p => p.X) - polygon.Outer.Min(p => p.X), polygon.Outer.Max(p => p.Y) - polygon.Outer.Min(p => p.Y));
            if (Inside(polygon, bounds.Center)) return bounds.Center;
            for (var y = 1; y < 8; y++) for (var x = 1; x < 8; x++) { var p = new Point(bounds.Left + bounds.Width * x / 8, bounds.Top + bounds.Height * y / 8); if (Inside(polygon, p)) return p; }
            return polygon.Outer[0];
        }
        return default;
    }
    private static bool RingContains(Point[] vertices, Point point)
    {
        var inside = false;
        for (int i = 0, j = vertices.Length - 1; i < vertices.Length; j = i++)
        {
            var a = vertices[j]; var b = vertices[i]; var cross = (point.X - a.X) * (b.Y - a.Y) - (point.Y - a.Y) * (b.X - a.X);
            if (Math.Abs(cross) < 1e-8 && point.X >= Math.Min(a.X, b.X) && point.X <= Math.Max(a.X, b.X) && point.Y >= Math.Min(a.Y, b.Y) && point.Y <= Math.Max(a.Y, b.Y)) return true;
            if ((a.Y > point.Y) != (b.Y > point.Y) && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
        }
        return inside;
    }
    private static bool Inside(PaintedPolygon polygon, Point point) => RingContains(polygon.Outer, point) && !polygon.Holes.Any(h => RingContains(h, point));
    protected override bool ContainsMark(EngineeringMarkInfo mark, Point point)
    {
        if (!_painted.TryGetValue(mark, out var painted) || !PrimitiveContains(painted, point)) return false;
        if (_commandIndices.TryGetValue(mark, out var index))
            for (var i = index + 1; i < _commands.Count; i++)
            {
                var command = _commands[i];
                // No-data areas are not observations, but a visible later layer still
                // occludes pointer interaction beneath it. Zero alpha/opacity passes through.
                var visible = command.Fill is ISolidColorBrush solid ? solid.Color.A > 0 && solid.Opacity > 0 : command.Fill is { Opacity: > 0 };
                if (command.Mark is null && visible && PrimitiveContains(command.Primitive, point)) return false;
            }
        return true;
    }
    private static bool PrimitiveContains(PaintedMapMark painted, Point point)
    {
        if (painted.Sector is { } sector)
        {
            var dx = point.X - sector.Center.X; var dy = point.Y - sector.Center.Y;
            if (dx * dx + dy * dy > sector.Radius * sector.Radius) return false;
            var offset = ((Math.Atan2(dy, dx) * 180 / Math.PI - sector.StartAngle) % 360 + 360) % 360;
            return offset <= sector.SweepAngle + 1e-10;
        }
        if (painted.Segments is { } segments)
            return segments.Any(s => SegmentDistance(point, s.Start, s.End) <= painted.LineRadius) || painted.Endpoints?.Any(p => SegmentDistance(point, p, p) <= painted.EndpointRadius) == true;
        if (painted.Circle is { } circle) { if (circle.Width <= 0 || circle.Height <= 0) return false; var x = (point.X - circle.Center.X) / (circle.Width / 2); var y = (point.Y - circle.Center.Y) / (circle.Height / 2); return x * x + y * y <= 1; }
        return painted.Polygons.Any(p => Inside(p, point));
    }
    protected override void RenderEngineeringMarks(DrawingContext context, IReadOnlyList<EngineeringMarkInfo> marks, EngineeringPointHit? selected, EngineeringPointHit? active)
    {
        foreach (var command in _commands)
        {
            if (command.Mark is { } mark)
            {
                var highlighted = ReferenceEquals(mark, selected?.Mark) || ReferenceEquals(mark, active?.Mark) || IsMapMarkSelected(mark);
                var filledLine = mark.Key is MapGlyphKey { Kind: MapElementKind.Connection or MapElementKind.Vector };
                context.DrawGeometry(mark.Fill, !highlighted && filledLine && mark.Stroke is null ? null : new Pen(highlighted ? Foreground ?? Brushes.Gray : mark.Stroke ?? mark.Fill ?? Brushes.Gray, highlighted ? 2 : 1), mark.Geometry);
            }
            else context.DrawGeometry(command.Fill, command.Stroke is null ? null : new Pen(command.Stroke, 1), command.Geometry);
        }
    }
    protected override void RenderAnnotations(DrawingContext context, Rect plot)
    {
        if (ShowLabels)
        {
            var canvas = LabelCanvasColor();
            using (context.PushClip(plot))
                foreach (var label in _labels)
                    if (!UsesDataLabelTemplate || label.Mark is null) DrawMapLabel(context, label.Text, label.Position, Math.Min(160, plot.Width), label.Fill, canvas);
        }
        if (UsesLegendItemTemplate || !ShowLegend || _legends.Count + _categoryLegends.Count == 0) return;
        var segment = plot.Width / (_legends.Count + _categoryLegends.Count);
        for (var i = 0; i < _legends.Count; i++)
        {
            var legend = _legends[i]; var width = Math.Max(0, segment - 12); var x = plot.Left + i * segment + 6; var y = plot.Bottom + 8;
            for (var step = 0; step < 32; step++) context.DrawRectangle(Gradient(legend.Minimum, legend.Maximum, step / 31d), null, new(x + width * step / 32, y, width / 32 + .1, 7));
            DrawLabel(context, $"{F(legend.Low)} — {F(legend.High)}", new(x + width / 2, y + 19), width);
        }
        for (var i = 0; i < _categoryLegends.Count; i++)
        {
            var legend = _categoryLegends[i]; var x = plot.Left + (i + _legends.Count) * segment + 4; var y = plot.Bottom + 8;
            context.DrawRectangle(legend.Brush, null, new(x, y, Math.Min(10, segment), 8));
            if (segment >= 24) DrawLabel(context, legend.Category.ToString() ?? "", new(x + segment / 2, y + 19), Math.Max(0, segment - 4));
        }
    }
    protected virtual bool IsMapMarkSelected(EngineeringMarkInfo mark) => false;
    protected virtual void ReconcileMapSelection(IReadOnlyList<EngineeringMarkInfo> marks) { }
}
