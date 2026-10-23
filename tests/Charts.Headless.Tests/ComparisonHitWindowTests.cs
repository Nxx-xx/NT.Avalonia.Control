using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class ComparisonHitWindowTests
{
    [AvaloniaTheory]
    [InlineData(0, false, false)] [InlineData(0, true, true)]
    [InlineData(1, false, true)] [InlineData(1, true, false)]
    [InlineData(2, false, false)] [InlineData(2, true, true)]
    [InlineData(3, false, true)] [InlineData(3, true, false)]
    [InlineData(4, false, false)] [InlineData(4, true, true)]
    public void Indexed_category_candidates_preserve_exhaustive_hit_results(int kind, bool horizontal, bool inversed)
    {
        ComparisonSeries series = kind switch
        {
            0 => new RangeBarSeries(), 1 => new WaterfallSeries(), 2 => new DotSeries(),
            3 => new LollipopSeries(), _ => new VarianceSeries(),
        };
        series.Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical;
        series.BarWidth = 19; series.MarkerSize = 13;
        var data = new ChartDataSnapshot(1, Enumerable.Range(0, 2000).Select(i =>
            new ChartDataPoint(i, i, i, i / 2, horizontal ? new ChartPoint(2 + i % 12, i / 2) : new ChartPoint(i / 2, 2 + i % 12),
                i % 31 == 0, 2 + i % 12, -2)).ToArray());
        var x = ChartScale.Create(horizontal ? -3 : 0, horizontal ? 15 : 1000,
            new NumericAxis { IsInversed = inversed }, paddingFraction: 0);
        var y = ChartScale.Create(horizontal ? 0 : -3, horizontal ? 1000 : 15,
            new NumericAxis { IsInversed = inversed }, paddingFraction: 0);
        var projection = new CartesianChart.CartesianProjection(new Rect(10, 10, 10000, 6000), x, y);
        var context = new ChartSeriesRenderContext(series, data, projection, Brushes.Blue, 1, 3, 18);
        var reference = context with { Projection = new ExternalProjection(projection) };
        var random = new Random(20261018 + kind);
        for (var index = 0; index < 100; index++)
        {
            var position = new Point(10 + random.NextDouble() * 10000, 10 + random.NextDouble() * 6000);
            Assert.Equal(series.Renderer.HitTest(reference, position, 12), series.Renderer.HitTest(context, position, 12));
            Assert.InRange(ChartComparisonGeometry.HitCandidates(context, position, 12).Count(), 0, 35);
        }
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Pictorial_window_preserves_holes_gaps_and_last_painted_identity(bool horizontal)
    {
        var series = new PictorialBarSeries
        {
            Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical,
            RenderMode = PictorialBarRenderMode.Repeat, SymbolSize = 11, SymbolSpacing = 4, BarWidth = 12,
            Geometry = Geometry.Parse("M0,0 L1,0 L1,1 L0,1 Z M.25,.25 L.25,.75 L.75,.75 L.75,.25 Z"),
        };
        var data = new ChartDataSnapshot(1, Enumerable.Range(0, 100).Select(i => new ChartDataPoint(i, i, i, i / 2,
            horizontal ? new ChartPoint(10, i / 2) : new ChartPoint(i / 2, 10), false)).ToArray());
        var x = ChartScale.Create(0, horizontal ? 10 : 50, new NumericAxis(), paddingFraction: 0);
        var y = ChartScale.Create(0, horizontal ? 50 : 10, new NumericAxis(), paddingFraction: 0);
        var projection = new CartesianChart.CartesianProjection(new Rect(0, 0, 600, 400), x, y);
        var context = new ChartSeriesRenderContext(series, data, projection, Brushes.Blue, 0, 1, 12);
        var reference = context with { Projection = new ExternalProjection(projection) };
        for (var a = 1; a < 600; a += 11)
            for (var b = 1; b < 400; b += 13)
                Assert.Equal(series.Renderer.HitTest(reference, new(a, b), 12), series.Renderer.HitTest(context, new(a, b), 12));
    }

    private sealed class ExternalProjection(IChartProjection inner) : IChartProjection
    {
        public Rect PlotArea => inner.PlotArea;
        public double YOrigin => inner.YOrigin;
        public double MapX(double value) => inner.MapX(value);
        public double MapY(double value) => inner.MapY(value);
        public Point Project(ChartPoint value) => inner.Project(value);
        public ChartPoint Unproject(Point value) => inner.Unproject(value);
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Nonmonotonic_custom_category_axis_preserves_the_exhaustive_fallback(bool horizontal)
    {
        var series = new RangeBarSeries { Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical,
            BarWidth = 8, ShowMarkers = false };
        var data = new ChartDataSnapshot(1, Enumerable.Range(0, 100).Select(i => new ChartDataPoint(i, i, i, i,
            horizontal ? new ChartPoint(10, i) : new ChartPoint(i, 10), false, 10, 0)).ToArray());
        var category = ChartScale.Create(-25, 75, new FoldedAxis(), paddingFraction: 0);
        var value = ChartScale.Create(0, 20, new NumericAxis(), paddingFraction: 0);
        var projection = new CartesianChart.CartesianProjection(new Rect(0, 0, 1000, 1000),
            horizontal ? value : category, horizontal ? category : value);
        var context = new ChartSeriesRenderContext(series, data, projection, Brushes.Blue, 0, 1, 8);
        var reference = context with { Projection = new ExternalProjection(projection) };
        var query = projection.Project(new ChartPoint(10, 10));
        Assert.Equal(100, ChartComparisonGeometry.HitCandidates(context, query, 0).Count());
        Assert.Equal(series.Renderer.HitTest(reference, query, 0), series.Renderer.HitTest(context, query, 0));
        Assert.Equal(90, series.Renderer.HitTest(context, query, 0)!.SourceIndex);
    }

    private sealed class FoldedAxis : ChartAxis
    {
        public override double Transform(double value) => value <= 50 ? value : 100 - value;
        public override double Inverse(double value) => value;
    }
}
