using Avalonia;

namespace NT.AvaloniaControl.Charts;

/// <summary>A professional chart's passive plot-content snapshot. Layout fractions are visual family coordinates,
/// with (0, 0) at the untransformed plot's upper left and (1, 1) at its lower right. They are not data axes.</summary>
public sealed class ChartDataPlotAreaContext
{
    private readonly double _zoom, _panX, _panY;
    internal ChartDataPlotAreaContext(object content, object? dataContext, Rect plotArea, double zoom, double panX, double panY,
        IReadOnlyList<EngineeringMarkInfo> engineeringMarks, IReadOnlyList<AnalysisMarkInfo> analysisMarks)
    {
        Content = content; DataContext = dataContext; PlotArea = plotArea;
        _zoom = zoom; _panX = panX; _panY = panY;
        EngineeringMarks = engineeringMarks; AnalysisMarks = analysisMarks;
        var start = UnprojectLayout(default); var end = UnprojectLayout(new(plotArea.Width, plotArea.Height));
        VisibleLayoutBounds = new(start, end);
    }
    public object Content { get; }
    /// <summary>The chart's inherited application context when this snapshot was captured.</summary>
    public object? DataContext { get; }
    /// <summary>Chart-local plot bounds. Template controls are arranged at this rectangle and use plot-local coordinates.</summary>
    public Rect PlotArea { get; }
    /// <summary>The visible untransformed layout fractions, including the engineering family's actual zoom and pan.</summary>
    public Rect VisibleLayoutBounds { get; }
    /// <summary>The actual immutable engineering frame, including every original aggregate member. Empty for analytical roots.</summary>
    public IReadOnlyList<EngineeringMarkInfo> EngineeringMarks { get; }
    /// <summary>The actual immutable analytical frame and its original data. Empty for engineering roots.</summary>
    public IReadOnlyList<AnalysisMarkInfo> AnalysisMarks { get; }
    /// <summary>Maps untransformed family-layout fractions to plot-local DIPs. Y increases downwards.
    /// This does not map raw numeric, geographic, hierarchy, schedule or category data.</summary>
    public Point ProjectLayout(double xFraction, double yFraction)
    {
        Finite(new(xFraction, yFraction));
        return Result(new(((xFraction - .5) * _zoom + .5 + _panX) * PlotArea.Width,
            ((yFraction - .5) * _zoom + .5 + _panY) * PlotArea.Height));
    }
    /// <summary>Maps plot-local DIPs back to untransformed family-layout fractions.</summary>
    public Point UnprojectLayout(Point plotLocal)
    {
        Finite(plotLocal);
        return Result(new((plotLocal.X / PlotArea.Width - .5 - _panX) / _zoom + .5,
            (plotLocal.Y / PlotArea.Height - .5 - _panY) / _zoom + .5));
    }
    /// <summary>Converts an already projected mark's chart-local anchor to plot-local DIPs.</summary>
    public Point ToLocal(Point chartLocal) { Finite(chartLocal); return Result(new(chartLocal.X - PlotArea.Left, chartLocal.Y - PlotArea.Top)); }
    public Point ToChart(Point plotLocal) { Finite(plotLocal); return Result(new(plotLocal.X + PlotArea.Left, plotLocal.Y + PlotArea.Top)); }
    private static void Finite(Point value)
    { if (!double.IsFinite(value.X) || !double.IsFinite(value.Y)) throw new ArgumentOutOfRangeException(nameof(value), "Coordinates must be finite."); }
    private static Point Result(Point value)
    { if (!double.IsFinite(value.X) || !double.IsFinite(value.Y)) throw new OverflowException("The plot-content projection cannot be represented by finite coordinates."); return value; }
}
