using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>A displayed label and its optional outer-edge leader line in chart-local DIPs.</summary>
public sealed record CircularLabelInfo(CircularSliceInfo Slice, string Text, Rect Bounds, IReadOnlyList<Point> Connector);

public sealed partial class CircularChart
{
    private string SliceText(CircularSliceInfo slice) => LabelFormatter?.Invoke(slice) ??
        $"{Convert.ToString(slice.Data.Category, CultureInfo.CurrentCulture)} {slice.Fraction:P0}";
    /// <summary>Current-frame labels. Labels that cannot fit are omitted without merging or changing source values.</summary>
    public IReadOnlyList<CircularLabelInfo> GetLabels()
    {
        if (!ShowDataLabels || LabelPosition == CircularLabelPosition.None) return Array.Empty<CircularLabelInfo>();
        var slices = GetSlices().Where(s => s.IsVisible && !s.IsExiting && s.SweepAngle != 0).ToArray();
        var labels = new List<CircularLabelInfo>();
        var canvas = new Rect(Padding.Left + 4, Padding.Top + 4 + (string.IsNullOrEmpty(Title) ? 0 : FontSize * 1.6 + 8),
            Math.Max(0, Bounds.Width - Padding.Left - Padding.Right - 8),
            Math.Max(0, Bounds.Height - Padding.Top - Padding.Bottom - 8 - (string.IsNullOrEmpty(Title) ? 0 : FontSize * 1.6 + 8) - (ShowLegend ? FontSize * 2 + 10 : 0)));
        if (LabelPosition == CircularLabelPosition.Inside)
        {
            foreach (var slice in slices)
            {
                var text = SliceText(slice);
                if (string.IsNullOrEmpty(text)) continue;
                var measure = Text(text);
                var room = Math.Abs(slice.SweepAngle) * Math.PI / 180 * ((slice.InnerRadius + slice.OuterRadius) / 2);
                if (room < measure.Width + 4 || slice.OuterRadius - slice.InnerRadius < measure.Height + 4) continue;
                var rect = new Rect(slice.Hit.Position - new Vector(measure.Width / 2, measure.Height / 2), new Size(measure.Width, measure.Height));
                if (!canvas.Contains(rect) || labels.Any(l => l.Bounds.Intersects(rect))) continue;
                labels.Add(new(slice, text, rect, Array.Empty<Point>()));
            }
        }
        else
        {
            foreach (var right in new[] { false, true })
            {
                var width = right ? canvas.Right - _plotArea.Right - 12 : _plotArea.Left - canvas.Left - 12;
                if (width <= 4) continue;
                var candidates = slices.Where(s => (Math.Cos((s.StartAngle + s.SweepAngle / 2) * Math.PI / 180) >= 0) == right)
                    .Select(s => (Slice: s, Text: SliceText(s).ReplaceLineEndings(" "), Anchor: CircularChartGeometry.PointAt(s, s.StartAngle + s.SweepAngle / 2, s.OuterRadius))).Where(c => !string.IsNullOrEmpty(c.Text)).ToList();
                var lineHeight = Text("Ag").Height + 4;
                var capacity = Math.Max(0, (int)(canvas.Height / lineHeight));
                var retained = candidates.OrderByDescending(c => c.Slice.Fraction).ThenBy(c => c.Slice.Data.SourceIndex).Take(capacity).OrderBy(c => c.Anchor.Y).ToArray();
                var previousBottom = canvas.Top - 4;
                for (var index = 0; index < retained.Length; index++)
                {
                    var item = retained[index];
                    var measure = Text(item.Text); measure.MaxTextWidth = width; measure.MaxLineCount = 1; measure.Trimming = TextTrimming.CharacterEllipsis;
                    var y = Math.Clamp(item.Anchor.Y - measure.Height / 2, previousBottom + 4,
                        Math.Max(previousBottom + 4, canvas.Bottom - (retained.Length - index) * lineHeight));
                    var x = right ? _plotArea.Right + 12 : _plotArea.Left - 12 - Math.Min(measure.Width, width);
                    var rect = new Rect(x, y, Math.Min(measure.Width, width), measure.Height);
                    previousBottom = rect.Bottom;
                    var elbow = CircularChartGeometry.PointAt(item.Slice, item.Slice.StartAngle + item.Slice.SweepAngle / 2, item.Slice.OuterRadius + 7);
                    var end = new Point(right ? rect.Left - 3 : rect.Right + 3, rect.Center.Y);
                    labels.Add(new(item.Slice, item.Text, rect, Array.AsReadOnly(new[] { item.Anchor, elbow, end })));
                }
            }
        }
        return labels.AsReadOnly();
    }
    public override void Render(DrawingContext context)
    {
        if (_rendering) return;
        if (!_attached) _dirty = true;
        _rendering = true;
        try { RenderCore(context); }
        finally { _renderScene = null; _rendering = false; }
    }
    private void RenderCore(DrawingContext context)
    {
        base.Render(context);
        using var clip = context.PushClip(new Rect(Bounds.Size));
        if (Background is { } background) context.DrawRectangle(background, null, new Rect(Bounds.Size));
        var scene = GetSlices();
        _renderScene = scene;
        if (!string.IsNullOrEmpty(Title)) context.DrawText(Text(Title, FontSize * 1.2), new Point(Padding.Left + 8, Padding.Top + 5));
        foreach (var slice in scene)
        {
            if (!slice.IsVisible || slice.SweepAngle == 0) continue;
            var geometry = CircularChartGeometry.CreateGeometry(slice);
            var stroke = slice.Series.Stroke is { } brush && slice.Series.StrokeThickness > 0 ? new Pen(brush, slice.Series.StrokeThickness) : null;
            context.DrawGeometry(slice.Fill, stroke, geometry);
            if (_selection.Any(p => SameIdentity(p, slice.Hit)) || SameIdentity(_keyboardSlice?.Hit, slice.Hit))
                context.DrawGeometry(null, new Pen(Foreground ?? Brushes.Black, 2), geometry);
        }
        if (!scene.Any(s => s.IsVisible))
        { var empty = Text(EmptyText); context.DrawText(empty, _plotArea.Center - new Vector(empty.Width / 2, empty.Height / 2)); }
        foreach (var label in GetLabels())
        {
            if (LabelConnectorBrush is { } brush && LabelConnectorThickness > 0)
                for (var index = 1; index < label.Connector.Count; index++) context.DrawLine(new Pen(brush, LabelConnectorThickness), label.Connector[index - 1], label.Connector[index]);
            var text = Text(label.Text); text.MaxTextWidth = Math.Max(1, label.Bounds.Width); text.MaxLineCount = 1; text.Trimming = TextTrimming.CharacterEllipsis;
            context.DrawText(text, label.Bounds.TopLeft);
        }
        var hole = scene.Where(s => s.IsVisible && !s.IsExiting).Select(s => s.InnerRadius).DefaultIfEmpty(0).Min();
        if (_centerChild is null && CenterText is { Length: > 0 } center && hole > 0)
        {
            var text = Text(center); text.MaxTextWidth = Math.Max(1, hole * 1.3); text.TextAlignment = TextAlignment.Center;
            var centerY = Math.Clamp(_circleCenter.Y, _plotArea.Top + text.Height / 2, Math.Max(_plotArea.Top + text.Height / 2, _plotArea.Bottom - text.Height / 2));
            context.DrawText(text, new Point(_circleCenter.X - text.Width / 2, centerY - text.Height / 2));
        }
        if (ShowLegend) DrawLegend(context, scene);
        var hover = _hovered is { } hovered ? scene.LastOrDefault(s => !s.IsExiting && s.IsVisible && SameIdentity(s.Hit, hovered.Hit)) : null;
        if (IsTooltipEnabled && hover is not null) DrawTooltip(context, hover);
    }
    private void DrawLegend(DrawingContext context, IReadOnlyList<CircularSliceInfo> scene)
    {
        var x = Padding.Left + 8; var y = Bounds.Height - Padding.Bottom - FontSize * 2 - 2;
        foreach (var slice in scene.Where(s => !s.IsExiting))
        {
            var label = Convert.ToString(slice.Data.Category, CultureInfo.CurrentCulture) ?? slice.Data.SourceIndex.ToString(CultureInfo.CurrentCulture);
            if (ActiveSeries.Count() > 1) label = $"{slice.Series.Title} {label}";
            var text = Text(label, FontSize * .9);
            if (x + text.Width + 24 > Bounds.Width - Padding.Right - 8) break;
            context.DrawEllipse(slice.Fill, null, new Point(x + 4, y + text.Height / 2), 4, 4);
            context.DrawText(text, new Point(x + 13, y)); x += text.Width + 27;
        }
    }
    private void DrawTooltip(DrawingContext context, CircularSliceInfo hover)
    {
        var value = TooltipFormatter?.Invoke(hover) ?? $"{hover.Data.Category}: {hover.Data.Point.Y:G6} ({hover.Fraction:P1})";
        if (string.IsNullOrEmpty(value)) return;
        var text = Text(value); text.MaxTextWidth = Math.Max(1, Bounds.Width - 20); text.Trimming = TextTrimming.CharacterEllipsis;
        var width = Math.Min(Bounds.Width, text.Width + 12); var height = Math.Min(Bounds.Height, text.Height + 8);
        var rect = new Rect(Math.Clamp(hover.Hit.Position.X + 8, 0, Math.Max(0, Bounds.Width - width)), Math.Clamp(hover.Hit.Position.Y + 8, 0, Math.Max(0, Bounds.Height - height)), width, height);
        context.DrawRectangle(Background ?? Brushes.White, new Pen(LabelConnectorBrush ?? Brushes.Gray, 1), rect, 3, 3);
        context.DrawText(text, rect.TopLeft + new Vector(6, 4));
    }
}
