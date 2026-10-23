using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

public sealed partial class SparklineChart
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
            if (scene.IsDomainValid && _plotArea.Width > 0 && _plotArea.Height > 0)
            {
                using var plot = context.PushClip(_plotArea.Inflate(Math.Max(MarkerSize / 2, StrokeThickness / 2)));
                if (ShowBaseline) context.DrawLine(new Pen(BaselineBrush ?? Foreground ?? Brushes.Gray, 1), new(_plotArea.Left, scene.Baseline), new(_plotArea.Right, scene.Baseline));
                if (SparklineType is SparklineType.Line or SparklineType.Area)
                {
                    if (SparklineType == SparklineType.Area) context.DrawGeometry(AreaFill ?? DefaultArea, null, CreatePath(scene, true));
                    if (StrokeThickness > 0) context.DrawGeometry(null, new Pen(LineBrush ?? DefaultLine, StrokeThickness), CreatePath(scene, false));
                    foreach (var point in scene.Points.Where(p => p.IsVisible))
                    {
                        var emphasized = ShowMinMax && (point.IsMinimum || point.IsMaximum) || ShowFirstLast && (point.IsFirst || point.IsLast);
                        if ((ShowMarkers || emphasized) && MarkerSize > 0) context.DrawEllipse(PointBrush(point), null, point.Position, MarkerSize / 2, MarkerSize / 2);
                    }
                }
                else
                    foreach (var point in scene.Points.Where(p => p.IsVisible))
                    {
                        context.DrawRectangle(PointBrush(point), null, point.BarBounds);
                        if (ShowMinMax && (point.IsMinimum || point.IsMaximum) || ShowFirstLast && (point.IsFirst || point.IsLast))
                            context.DrawRectangle(null, new Pen(EmphasisBrush(point), Math.Min(1, point.BarBounds.Width)), point.BarBounds);
                    }
                if (_activePoint is { } active)
                    context.DrawEllipse(null, new Pen(Foreground ?? Brushes.Gray, 1), active.Position, Math.Max(3, MarkerSize / 2 + 2), Math.Max(3, MarkerSize / 2 + 2));
            }
            if (IsTooltipEnabled && _activePoint is { } hit) DrawTooltip(context, hit);
        }
        finally { _renderScene = null; _rendering = false; }
    }

    private StreamGeometry CreatePath(SparklineScene scene, bool area)
    {
        var geometry = new StreamGeometry(); using var context = geometry.Open();
        WritePathGeometry(scene.Points, scene.Baseline, area, new NativeGeometrySink(context));
        return geometry;
    }

    internal static void WritePathGeometry(IReadOnlyList<SparklinePointInfo> points, double baseline, bool area, ICircularGeometrySink context)
    {
        var segment = new List<Point>();
        void Commit()
        {
            if (segment.Count == 0) return;
            context.BeginFigure(segment[0], area);
            foreach (var point in segment.Skip(1)) context.LineTo(point);
            if (area) { context.LineTo(new(segment[^1].X, baseline)); context.LineTo(new(segment[0].X, baseline)); }
            context.EndFigure(area); segment.Clear();
        }
        foreach (var point in points) { if (point.IsVisible) segment.Add(point.Position); else Commit(); }
        Commit();
    }

    private IBrush PointBrush(SparklinePointInfo point)
    {
        if (SparklineType == SparklineType.WinLoss)
            return point.Data.Point.Y > Baseline ? WinBrush ?? DefaultWin : point.Data.Point.Y < Baseline ? LossBrush ?? DefaultLoss : ZeroBrush ?? Foreground ?? Brushes.Gray;
        if (SparklineType == SparklineType.Bar)
            return point.Data.Point.Y == Baseline ? ZeroBrush ?? BarBrush ?? LineBrush ?? DefaultLine : BarBrush ?? LineBrush ?? DefaultLine;
        return EmphasisBrush(point);
    }

    private IBrush EmphasisBrush(SparklinePointInfo point)
    {
        if (ShowFirstLast && point.IsLast) return LastBrush ?? LineBrush ?? DefaultLine;
        if (ShowFirstLast && point.IsFirst) return FirstBrush ?? LineBrush ?? DefaultLine;
        if (ShowMinMax && point.IsMaximum) return MaxBrush ?? LineBrush ?? DefaultLine;
        if (ShowMinMax && point.IsMinimum) return MinBrush ?? LineBrush ?? DefaultLine;
        return LineBrush ?? DefaultLine;
    }

    private void DrawTooltip(DrawingContext context, ChartPointHit hit)
    {
        var value = TooltipFormatter?.Invoke(hit) ?? $"{GetScene().Points.FirstOrDefault(p => p.Data.SourceIndex == hit.Index)?.Data.Category}: {hit.Point.Y.ToString("G4", CultureInfo.CurrentCulture)}";
        if (string.IsNullOrEmpty(value) || Bounds.Width <= 0 || Bounds.Height <= 0) return;
        var text = Text(value); text.MaxTextWidth = Math.Max(1, Bounds.Width - 12); text.MaxLineCount = 1; text.Trimming = TextTrimming.CharacterEllipsis;
        var width = Math.Min(Bounds.Width, text.Width + 8); var height = Math.Min(Bounds.Height, text.Height + 4);
        var rect = new Rect(Math.Clamp(hit.Position.X + 5, 0, Bounds.Width - width), Math.Clamp(hit.Position.Y - height - 3, 0, Bounds.Height - height), width, height);
        context.DrawRectangle(Background ?? Brushes.White, new Pen(Foreground ?? Brushes.Gray, 1), rect, 2, 2);
        context.DrawText(text, rect.TopLeft + new Vector(4, 2));
    }

    private sealed class NativeGeometrySink(StreamGeometryContext context) : ICircularGeometrySink
    {
        public void SetFillRule(FillRule rule) => context.SetFillRule(rule);
        public void BeginFigure(Point start, bool isFilled) => context.BeginFigure(start, isFilled);
        public void ArcTo(Point end, Size radii, double rotation, bool largeArc, SweepDirection direction) => context.ArcTo(end, radii, rotation, largeArc, direction);
        public void LineTo(Point end) => context.LineTo(end);
        public void EndFigure(bool close) => context.EndFigure(close);
    }
}
