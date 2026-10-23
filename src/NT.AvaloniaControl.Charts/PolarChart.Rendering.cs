using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

public partial class PolarChart
{
    public override void Render(DrawingContext context)
    {
        if (_rendering) return;
        if (!_attached) _dirty = true;
        _rendering = true;
        try
        {
            base.Render(context);
            using var bounds = context.PushClip(new Rect(Bounds.Size));
            if (Background is { } background) context.DrawRectangle(background, null, new Rect(Bounds.Size));
            var scene = GetScene(); _renderScene = scene;
            if (!string.IsNullOrEmpty(Title)) context.DrawText(Text(Title, FontSize * 1.2), new Point(Padding.Left + 8, Padding.Top + 5));
            if (scene.Radius > 0)
            {
                DrawGrid(context, scene);
                using (context.PushGeometryClip(new EllipseGeometry(new Rect(scene.Center - new Vector(scene.Radius, scene.Radius), new Size(scene.Radius * 2, scene.Radius * 2)))))
                    foreach (var group in scene.Series) DrawSeries(context, scene, group);
            }
            if (!CurrentHits(scene).Any()) { var text = Text(EmptyText); context.DrawText(text, scene.Center - new Vector(text.Width / 2, text.Height / 2)); }
            if (ShowDataLabels) DrawDataLabels(context, scene);
            if (ShowLegend) DrawLegend(context, scene);
            if (IsTooltipEnabled && _hovered is { } hovered) DrawTooltip(context, hovered);
        }
        finally { _renderScene = null; _rendering = false; }
    }
    private void DrawSeries(DrawingContext context, PolarScene scene, PolarSeriesScene group)
    {
        var series = group.Series;
        var color = group.Points.Select(p => p.Fill).FirstOrDefault(b => b is not null) ?? group.Sectors.Select(p => p.Fill).FirstOrDefault(b => b is not null) ?? DefaultPalette[0];
        var stroke = series.Stroke ?? color;
        var pen = series.StrokeThickness > 0 ? new Pen(stroke, series.StrokeThickness) : null;
        if (series is PolarFilledSeries or RadarSeries)
        {
            using var opacity = context.PushOpacity(series.FillOpacity);
            context.DrawGeometry(series.Fill ?? color, null, PolarChartGeometry.CreateFillGeometry(group, scene.Center));
        }
        if (series is not (PolarScatterSeries or PolarAreaSeries or NightingaleRoseSeries or RadialBarSeries) && pen is not null)
            context.DrawGeometry(null, pen, PolarChartGeometry.CreateLineGeometry(group));
        foreach (var sector in group.Sectors.Where(s => s.IsVisible))
        {
            var geometry = PolarChartGeometry.CreateGeometry(sector);
            using (context.PushOpacity(series.FillOpacity)) context.DrawGeometry(sector.Fill, null, geometry);
            if (series.Stroke is not null && pen is not null) context.DrawGeometry(null, pen, geometry);
            if (IsEmphasized(sector.Hit)) context.DrawGeometry(null, new Pen(Foreground ?? Brushes.Black, 2), geometry);
        }
        foreach (var point in group.Points.Where(p => p.IsVisible))
        {
            if (series.ShowMarkers && series.MarkerSize > 0) context.DrawEllipse(point.Fill, null, point.Position, series.MarkerSize / 2, series.MarkerSize / 2);
            if (IsEmphasized(point.Hit)) context.DrawEllipse(null, new Pen(Foreground ?? Brushes.Black, 2), point.Position, Math.Max(4, series.MarkerSize / 2 + 3), Math.Max(4, series.MarkerSize / 2 + 3));
        }
    }
    private bool IsEmphasized(ChartPointHit point) => _selection.Any(p => SameIdentity(p, point)) || SameIdentity(_keyboardPoint, point);
    private void DrawDataLabels(DrawingContext context, PolarScene scene)
    {
        var used = new List<Rect>();
        foreach (var hit in CurrentHits(scene))
        {
            var value = LabelFormatter?.Invoke(hit) ?? hit.Point.Y.ToString("G4", CultureInfo.CurrentCulture);
            if (string.IsNullOrEmpty(value)) continue;
            var text = Text(value); text.MaxTextWidth = 100; text.MaxLineCount = 1; text.Trimming = TextTrimming.CharacterEllipsis;
            var rect = new Rect(hit.Position + new Vector(6, -text.Height - 4), new Size(text.Width, text.Height));
            if (!new Rect(Bounds.Size).Contains(rect) || used.Any(r => r.Intersects(rect))) continue;
            used.Add(rect); context.DrawText(text, rect.TopLeft);
        }
    }
    private void DrawLegend(DrawingContext context, PolarScene scene)
    {
        var x = Padding.Left + 8; var y = Bounds.Height - Padding.Bottom - FontSize * 2;
        foreach (var group in scene.Series)
        {
            var text = Text(group.Series.Title ?? group.Series.GetType().Name, FontSize * .9);
            if (x + text.Width + 24 > Bounds.Width - Padding.Right - 8) break;
            var brush = group.Series.Stroke ?? group.Series.Fill ?? group.Points.FirstOrDefault()?.Fill ?? group.Sectors.FirstOrDefault()?.Fill;
            context.DrawEllipse(brush, null, new Point(x + 4, y + text.Height / 2), 4, 4); context.DrawText(text, new Point(x + 13, y)); x += text.Width + 27;
        }
    }
    private void DrawTooltip(DrawingContext context, ChartPointHit hit)
    {
        var value = TooltipFormatter?.Invoke(hit) ?? $"{hit.Series.Title} {hit.Point.X:G4}: {hit.Point.Y:G4}";
        if (string.IsNullOrEmpty(value)) return;
        var text = Text(value); text.MaxTextWidth = Math.Max(1, Bounds.Width - 20); text.MaxLineCount = 1; text.Trimming = TextTrimming.CharacterEllipsis;
        var size = new Size(Math.Min(Bounds.Width, text.Width + 12), Math.Min(Bounds.Height, text.Height + 8));
        var rect = new Rect(Math.Clamp(hit.Position.X + 8, 0, Math.Max(0, Bounds.Width - size.Width)), Math.Clamp(hit.Position.Y + 8, 0, Math.Max(0, Bounds.Height - size.Height)), size.Width, size.Height);
        context.DrawRectangle(Background ?? Brushes.White, new Pen(AxisBrush ?? Brushes.Gray, 1), rect, 3, 3); context.DrawText(text, rect.TopLeft + new Vector(6, 4));
    }
}
