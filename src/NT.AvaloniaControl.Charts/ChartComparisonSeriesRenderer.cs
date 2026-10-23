using Avalonia;
using Avalonia.Layout;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

internal enum ChartComparisonKind { Bar, Range, Waterfall, Dot, Lollipop, Variance, Pareto }

internal sealed partial class ChartComparisonSeriesRenderer(ChartComparisonKind kind) : IChartSeriesRenderer
{
    internal static readonly ChartComparisonSeriesRenderer Bar = new(ChartComparisonKind.Bar);
    internal static readonly ChartComparisonSeriesRenderer Range = new(ChartComparisonKind.Range);
    internal static readonly ChartComparisonSeriesRenderer Waterfall = new(ChartComparisonKind.Waterfall);
    internal static readonly ChartComparisonSeriesRenderer Dot = new(ChartComparisonKind.Dot);
    internal static readonly ChartComparisonSeriesRenderer Lollipop = new(ChartComparisonKind.Lollipop);
    internal static readonly ChartComparisonSeriesRenderer Variance = new(ChartComparisonKind.Variance);
    internal static readonly ChartComparisonSeriesRenderer Pareto = new(ChartComparisonKind.Pareto);
    private bool IsRectangle => kind is ChartComparisonKind.Bar or ChartComparisonKind.Range or ChartComparisonKind.Waterfall or ChartComparisonKind.Pareto;
    private bool HasStem => kind is ChartComparisonKind.Lollipop or ChartComparisonKind.Variance;
    public bool IncludesZero => false;
    public bool UsesCategorySlots => true;
    public ChartLegendSymbol LegendSymbol => IsRectangle ? ChartLegendSymbol.Rectangle : kind == ChartComparisonKind.Dot ? ChartLegendSymbol.Circle : ChartLegendSymbol.Line;

    public ChartSeriesBounds? GetBounds(ChartSeries series, ChartDataSnapshot data)
    {
        var bounds = ChartDataExtrema.For(data).Bounds;
        if (bounds is not { } result || series is not IChartComparisonSeries options || kind == ChartComparisonKind.Dot) return bounds;
        var horizontal = options.Orientation == Orientation.Horizontal;
        var minimum = horizontal ? result.XMinimum : result.YMinimum;
        var maximum = horizontal ? result.XMaximum : result.YMaximum;
        var sourceLow = kind is ChartComparisonKind.Range or ChartComparisonKind.Variance;
        if (ChartLowerExtrema.For(data, sourceLow).BoundsWithFallback(sourceLow ? double.NaN : options.Baseline) is { } lower)
        {
            minimum = Math.Min(minimum, lower.Minimum); maximum = Math.Max(maximum, lower.Maximum);
        }
        return horizontal ? result with { XMinimum = minimum, XMaximum = maximum } : result with { YMinimum = minimum, YMaximum = maximum };
    }

    public void Render(DrawingContext drawing, ChartSeriesRenderContext context)
    {
        using (IDisposable? clip = context.PrimaryBreakClip is { } primary ? drawing.PushGeometryClip(primary) : null)
            RenderPrimary(drawing, context);
        if (kind == ChartComparisonKind.Pareto && context.Series is ParetoSeries { ShowCumulativeLine: true, CumulativeStrokeThickness: > 0 } pareto)
        {
            using IDisposable? clip = context.SecondaryBreakClip is { } secondary ? drawing.PushGeometryClip(secondary) : null;
            RenderCumulative(drawing, context, pareto);
        }
    }

    private void RenderPrimary(DrawingContext drawing, ChartSeriesRenderContext context)
    {
        var series = context.Series;
        var radius = ChartMarkerGeometry.Radius(series);
        var markerGeometry = ChartMarkerGeometry.Create(series.MarkerShape, radius);
        var markerPen = new Pen(series.MarkerStroke ?? series.Stroke ?? context.Brush, ChartMarkerGeometry.StrokeThickness(series),
            lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        foreach (var entry in ChartComparisonLayout.For(context, kind).Entries)
        {
            var i = entry.Ordinal; var shape = entry.Shape;
            var fill = Fill(context, i);
            if (IsRectangle && shape.HasBody) drawing.DrawRectangle(fill, null, shape.Bounds);
            if (HasStem && series.StrokeThickness > 0)
                drawing.DrawLine(new Pen(series.Stroke ?? fill, series.StrokeThickness, lineCap: PenLineCap.Round), shape.Start, shape.End);
            if (radius <= 0) continue;
            var markerFill = series.MarkerFill ?? (IsRectangle ? fill : series.Fill ?? fill);
            var markerPlot = context.PlotArea.Inflate(ChartMarkerGeometry.HitRadius(series));
            if (markerPlot.Contains(shape.End)) ChartMarkerGeometry.Draw(drawing, series.MarkerShape, radius, markerFill, markerPen, shape.End, markerGeometry);
            if (kind == ChartComparisonKind.Variance && markerPlot.Contains(shape.Start))
                ChartMarkerGeometry.Draw(drawing, series.MarkerShape, radius, markerFill, markerPen, shape.Start, markerGeometry);
        }
        if (kind == ChartComparisonKind.Waterfall && series is WaterfallSeries { ShowConnectors: true, ConnectorThickness: > 0 } waterfall)
            RenderConnectors(drawing, context, waterfall);
    }

    private static IBrush Fill(ChartSeriesRenderContext context, int ordinal)
    {
        var fallback = context.Series.Fill ?? context.Brush;
        if (context.Series is not WaterfallSeries && context.Series is not VarianceSeries) return fallback;
        var point = context.Data.Points[ordinal];
        if (context.Series is WaterfallSeries waterfall)
            return point.IsTotal ? waterfall.TotalFill ?? fallback : (point.RawPoint ?? point.Point).Y >= 0
                ? waterfall.PositiveFill ?? fallback : waterfall.NegativeFill ?? fallback;
        var variance = (VarianceSeries)context.Series;
        return point.High >= point.Low ? variance.PositiveFill ?? fallback : variance.NegativeFill ?? fallback;
    }

    private static void RenderConnectors(DrawingContext drawing, ChartSeriesRenderContext context, WaterfallSeries series)
    {
        var pen = new Pen(series.ConnectorStroke ?? series.Stroke ?? context.Brush, series.ConnectorThickness, DashStyle.Dash);
        ChartComparisonShape? previous = null;
        var previousOrdinal = -2;
        foreach (var ordinal in ChartComparisonGeometry.VisibleCandidates(context, connectors: true))
        {
            if (ordinal != previousOrdinal + 1) previous = null;
            previousOrdinal = ordinal;
            if (!ChartComparisonGeometry.TryCreate(context, ordinal, out var current)) { previous = null; continue; }
            if (previous is { } before)
            {
                var forward = before.Horizontal ? current.End.Y >= before.End.Y : current.End.X >= before.End.X;
                var a = before.Horizontal ? new Point(before.End.X, forward ? before.Bounds.Bottom : before.Bounds.Top)
                    : new Point(forward ? before.Bounds.Right : before.Bounds.Left, before.End.Y);
                var b = current.Horizontal ? new Point(before.End.X, forward ? current.Bounds.Top : current.Bounds.Bottom)
                    : new Point(forward ? current.Bounds.Left : current.Bounds.Right, before.End.Y);
                drawing.DrawLine(pen, a, b);
            }
            previous = current;
        }
    }

    private static void RenderCumulative(DrawingContext drawing, ChartSeriesRenderContext context, ParetoSeries series)
    {
        if (context.SecondaryData is not { } data || context.SecondaryProjection is not { } projection) return;
        var horizontal = series.Orientation == Orientation.Horizontal;
        var width = double.IsFinite(context.GroupWidth) ? Math.Abs(context.GroupWidth) : 0;
        var categoryOffset = -width / 2 + (context.GroupIndex + .5) * width / Math.Max(1, context.GroupCount);
        var line = new StreamGeometry(); var count = 0; var connected = false;
        using (var path = line.Open())
        {
            foreach (var ordinal in CumulativeCandidates(context, data, projection, horizontal, categoryOffset,
                series.CumulativeStrokeThickness * 5 + 1,
                (series.CumulativeStroke ?? series.Stroke ?? context.Brush) is not ISolidColorBrush))
            {
                var value = data.GetValue(ordinal);
                var position = projection.Project(value.Point);
                ChartVisibilityQuery.RecordProjected(1);
                if (value.IsEmpty || !ChartComparisonGeometry.Valid(position)) { if (connected) path.EndFigure(false); connected = false; continue; }
                position = horizontal ? new Point(position.X, position.Y + categoryOffset) : new Point(position.X + categoryOffset, position.Y);
                if (connected) { path.LineTo(position); count++; }
                else { path.BeginFigure(position, false); connected = true; }
            }
            if (connected) path.EndFigure(false);
        }
        if (count > 0) drawing.DrawGeometry(null, new Pen(series.CumulativeStroke ?? series.Stroke ?? context.Brush, series.CumulativeStrokeThickness), line);
    }

    public ChartSeriesHit? HitTest(ChartSeriesRenderContext context, Point position, double tolerance)
        => ChartComparisonLayout.For(context, kind).HitTest(position, tolerance);
}
