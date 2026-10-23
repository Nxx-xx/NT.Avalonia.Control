using System.Collections;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Animation.Easings;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class SeriesDrawingLayerCacheTests
{
    [AvaloniaTheory]
    [InlineData("line", false)]
    [InlineData("line", true)]
    [InlineData("bar", false)]
    [InlineData("bar", true)]
    [InlineData("bar-with-markers", false)]
    [InlineData("bar-with-markers", true)]
    [InlineData("scatter", false)]
    [InlineData("scatter", true)]
    [InlineData("smooth", false)]
    [InlineData("smooth", true)]
    [InlineData("area", false)]
    [InlineData("area", true)]
    [InlineData("range-bar", false)]
    [InlineData("lollipop", false)]
    [InlineData("waterfall", false)]
    [InlineData("bubble", false)]
    [InlineData("pictorial", false)]
    public void Replay_keeps_primitive_calls_paint_order_and_coordinates_without_projecting_data_again(string kind, bool square)
    {
        var series = MakeSeries(kind);
        series.MarkerShape = square ? ChartMarkerShape.Square : ChartMarkerShape.Circle;
        var projection = new CountingProjection();
        var context = MakeContext(series, projection);
        using var original = new NativeBoundary();
        series.Renderer.Render(original.Drawing, context);
        Assert.NotEmpty(original.Recorder.Operations);
        var retained = Retain(series, context);
        var projected = projection.ProjectCount;
        using var replay = new NativeBoundary();
        retained.Draw(replay.Drawing);
        Assert.Equal(projected, projection.ProjectCount);
        Assert.Equal(original.Recorder.Operations, replay.Recorder.Operations);
    }

    [AvaloniaTheory]
    [InlineData("line", false)]
    [InlineData("line", true)]
    [InlineData("bar", false)]
    [InlineData("bar", true)]
    [InlineData("scatter", false)]
    [InlineData("scatter", true)]
    [InlineData("smooth", false)]
    [InlineData("smooth", true)]
    [InlineData("area", false)]
    [InlineData("area", true)]
    [InlineData("lollipop", false)]
    [InlineData("lollipop", true)]
    [InlineData("bubble", false)]
    [InlineData("bubble", true)]
    [InlineData("pictorial", false)]
    [InlineData("pictorial", true)]
    public void Retained_paint_reads_current_mutable_brush_color_opacity_gradient_and_transform(string kind, bool gradient)
    {
        Brush brush = gradient ? new LinearGradientBrush
        {
            GradientStops = [new GradientStop(Colors.Red, 0), new GradientStop(Colors.Blue, 1)],
        } : new SolidColorBrush(Colors.Red);
        var transform = new MatrixTransform(Matrix.CreateTranslation(2, 3));
        brush.Transform = transform;
        var series = MakeSeries(kind);
        series.Stroke = brush; series.Fill = brush; series.MarkerFill = brush; series.MarkerStroke = brush;
        var context = MakeContext(series, new CountingProjection(), brush);
        var retained = Retain(series, context);
        brush.Opacity = .375;
        transform.Matrix = Matrix.CreateScale(.8, 1.25) * Matrix.CreateTranslation(7, -3);
        if (brush is SolidColorBrush solid) solid.Color = Colors.Green;
        if (brush is LinearGradientBrush linear)
        {
            linear.StartPoint = new RelativePoint(3, 5, RelativeUnit.Absolute);
            linear.EndPoint = new RelativePoint(25, 30, RelativeUnit.Absolute);
            linear.GradientStops[0].Color = Colors.Green;
            linear.GradientStops.Add(new GradientStop(Colors.Purple, .375));
        }
        using var original = new NativeBoundary();
        series.Renderer.Render(original.Drawing, context);
        using var replay = new NativeBoundary();
        retained.Draw(replay.Drawing);
        Assert.Equal(original.Recorder.Operations, replay.Recorder.Operations);
        Assert.Contains(replay.Recorder.Brushes, current => ReferenceEquals(current, brush));
    }

    [AvaloniaTheory]
    [InlineData("line", ChartMarkerShape.Circle)]
    [InlineData("line", ChartMarkerShape.Square)]
    [InlineData("line", ChartMarkerShape.Rectangle)]
    [InlineData("line", ChartMarkerShape.Diamond)]
    [InlineData("line", ChartMarkerShape.Cross)]
    [InlineData("bar", ChartMarkerShape.Circle)]
    [InlineData("bar", ChartMarkerShape.Square)]
    [InlineData("bar-with-markers", ChartMarkerShape.Circle)]
    [InlineData("bar-with-markers", ChartMarkerShape.Square)]
    [InlineData("scatter", ChartMarkerShape.Circle)]
    [InlineData("scatter", ChartMarkerShape.Square)]
    [InlineData("scatter", ChartMarkerShape.Rectangle)]
    [InlineData("scatter", ChartMarkerShape.Diamond)]
    [InlineData("scatter", ChartMarkerShape.Cross)]
    [InlineData("smooth", ChartMarkerShape.Circle)]
    [InlineData("smooth", ChartMarkerShape.Square)]
    [InlineData("area", ChartMarkerShape.Circle)]
    [InlineData("area", ChartMarkerShape.Square)]
    [InlineData("bubble", ChartMarkerShape.Circle)]
    [InlineData("bubble", ChartMarkerShape.Square)]
    public void Owned_cartesian_projection_keeps_exact_native_primitive_coordinates_at_fractional_layout(string kind, ChartMarkerShape shape)
    {
        var series = MakeSeries(kind);
        series.MarkerShape = shape;
        series.ItemsSource = new ChartPoint[] { new(1.125, 2.375), new(2.1875, 8.625), new(4.8125, 4.375) };
        var chart = MakeChart(series);
        chart.Width = 700.375; chart.Height = 400.625; chart.Padding = new Thickness(9.125, 8.375, 7.625, 6.875);
        using var host = new TemplateFeaturesHost(chart);
        TemplateFeaturesHost.Record(chart);
        var scene = typeof(CartesianChart).GetField("_scene", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(chart)!;
        var snapshot = ((Array)scene.GetType().GetProperty("Snapshots")!.GetValue(scene)!).GetValue(0)!;
        var context = (ChartSeriesRenderContext)snapshot.GetType().GetProperty("Context")!.GetValue(snapshot)!;
        using var original = new NativeBoundary();
        series.Renderer.Render(original.Drawing, context);
        var retained = Retain(series, context);
        using var replay = new NativeBoundary();
        retained.Draw(replay.Drawing);
        Assert.NotEmpty(original.Recorder.Operations);
        Assert.Equal(original.Recorder.Operations, replay.Recorder.Operations);
    }

    [AvaloniaFact]
    public void Selection_opacity_reuses_series_geometry_and_data_view_style_revisions_replace_it()
    {
        var first = new LineSeries { MarkerSize = 0, ItemsSource = new ChartPoint[] { new(1, 2), new(4, 8) } };
        var second = new ScatterSeries { ItemsSource = new ChartPoint[] { new(3, 5) } };
        var chart = MakeChart(first, second);
        chart.SeriesSelectionMode = ChartSelectionMode.Multiple;
        using var host = new TemplateFeaturesHost(chart);
        TemplateFeaturesHost.Record(chart);
        var before = CacheValues(chart);
        var built = Counter(chart, "_seriesDrawingBuildCount");
        Assert.Equal(2, before.Length);
        Assert.True(chart.SelectSeries(first));
        chart.UnselectedSeriesOpacity = .125;
        var selected = TemplateFeaturesHost.Record(chart);
        Assert.Equal(built, Counter(chart, "_seriesDrawingBuildCount"));
        Assert.All(before, retained => Assert.Contains(CacheValues(chart), current => ReferenceEquals(retained, current)));
        Assert.Contains(TemplateFeaturesHost.Walk(selected).OfType<DrawingGroup>(), group => group.Opacity == .125);

        first.ItemsSource = new ChartPoint[] { new(1, 7), new(4, 3) };
        TemplateFeaturesHost.Record(chart);
        Assert.True(Counter(chart, "_seriesDrawingBuildCount") > built);
        Assert.DoesNotContain(CacheValues(chart), current => before.Any(previous => ReferenceEquals(previous, current)));
        var afterData = CacheValues(chart);
        chart.Viewport = new ChartViewport(0, 5, 1, 9);
        TemplateFeaturesHost.Record(chart);
        Assert.DoesNotContain(CacheValues(chart), current => afterData.Any(previous => ReferenceEquals(previous, current)));
        var afterView = CacheValues(chart);
        first.StrokeThickness = 5;
        TemplateFeaturesHost.Record(chart);
        Assert.DoesNotContain(CacheValues(chart), current => afterView.Any(previous => ReferenceEquals(previous, current)));
        host.Window.Content = null; host.Drain();
        Assert.Empty(CacheValues(chart));
        Assert.Null(typeof(CartesianChart).GetField("_seriesDrawingScene", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(chart));
    }

    [AvaloniaFact]
    public void Application_renderer_keeps_its_live_render_contract_and_is_never_retained()
    {
        var renderer = new ApplicationRenderer();
        var series = new ApplicationSeries(renderer) { ItemsSource = new ChartPoint[] { new(1, 2), new(4, 8) } };
        var chart = MakeChart(series);
        using var host = new TemplateFeaturesHost(chart);
        TemplateFeaturesHost.Record(chart);
        var before = renderer.RenderCount;
        TemplateFeaturesHost.Record(chart);
        Assert.Equal(before + 1, renderer.RenderCount);
        Assert.Empty(CacheValues(chart));
        Assert.Equal(0, Counter(chart, "_seriesDrawingBuildCount"));
    }

    [AvaloniaFact]
    public void Detached_manual_render_reads_marker_style_without_retaining_an_unobserved_series()
    {
        var series = new ScatterSeries { ItemsSource = new ChartPoint[] { new(2, 4) } };
        var chart = MakeChart(series);
        chart.Measure(new Size(700, 400)); chart.Arrange(new Rect(0, 0, 700, 400));
        using var before = new NativeBoundary();
        chart.Render(before.Drawing);
        Assert.Contains(before.Recorder.Operations, operation => operation.StartsWith("DrawEllipse:"));
        series.ShowMarkers = false;
        using var after = new NativeBoundary();
        chart.Render(after.Drawing);
        Assert.DoesNotContain(after.Recorder.Operations, operation => operation.StartsWith("DrawEllipse:"));
        Assert.Empty(CacheValues(chart));
    }

    [AvaloniaTheory]
    [InlineData("scatter")]
    [InlineData("bar")]
    [InlineData("bubble")]
    public void Owned_projection_with_a_custom_axis_keeps_the_renderers_live_transform_contract(string kind)
    {
        var series = MakeSeries(kind);
        series.ItemsSource = new ChartPoint[] { new(2, 3), new(4, 7) };
        var axis = new FoldedAxis { Minimum = 0, Maximum = 10 };
        var chart = MakeChart(series); chart.XAxis = axis;
        using var host = new TemplateFeaturesHost(chart);
        using var before = new NativeBoundary(); chart.Render(before.Drawing);
        Assert.Empty(CacheValues(chart));
        axis.FoldInterior = true;
        using var after = new NativeBoundary(); chart.Render(after.Drawing);
        Assert.False(before.Recorder.Operations.SequenceEqual(after.Recorder.Operations));
        Assert.Empty(CacheValues(chart));
        Assert.Equal(0, Counter(chart, "_seriesDrawingBuildCount"));
    }

    [AvaloniaFact]
    public void Pareto_secondary_custom_axis_also_keeps_cumulative_path_geometry_live()
    {
        var series = new ParetoSeries { CumulativeAxisKey = "percent",
            ItemsSource = new[] { new { Category = "A", Value = 20d }, new { Category = "B", Value = 80d } },
            CategoryPath = "Category", ValuePath = "Value" };
        var chart = new CartesianChart { ShowLegend = false, XAxis = new CategoryAxis(),
            YAxis = new NumericAxis { Minimum = 0, Maximum = 100 } };
        chart.Series.Add(series);
        chart.Axes.Add(new FoldedAxis { Key = "percent", Minimum = 0, Maximum = 100 });
        using var host = new TemplateFeaturesHost(chart);
        TemplateFeaturesHost.Record(chart);
        Assert.Empty(CacheValues(chart));
        Assert.Equal(0, Counter(chart, "_seriesDrawingBuildCount"));
    }

    [AvaloniaFact]
    public void Animation_frames_replace_geometry_while_a_paused_frame_and_overlay_redraw_keep_it()
    {
        var clock = new ManualClock();
        var chart = MakeChart(new ScatterSeries { ItemsSource = new ChartPoint[] { new(2, 8) } });
        chart.AnimationTimeProvider = clock; chart.AnimationDuration = TimeSpan.FromSeconds(1);
        chart.AnimationEasing = new LinearEasing(); chart.AnimationTriggers = ChartAnimationTriggers.Entrance;
        using var host = new TemplateFeaturesHost(chart);
        chart.ReplayEntranceAnimation(); host.Drain(); TemplateFeaturesHost.Record(chart);
        var initial = CacheValues(chart);
        Assert.NotEmpty(initial);
        clock.Advance(TimeSpan.FromMilliseconds(500)); chart.UpdateAnimationFrame(); host.Drain();
        TemplateFeaturesHost.Record(chart);
        var midpoint = CacheValues(chart);
        Assert.DoesNotContain(midpoint, current => initial.Any(previous => ReferenceEquals(previous, current)));
        chart.IsAnimationPaused = true; host.Drain(); TemplateFeaturesHost.Record(chart);
        var paused = CacheValues(chart);
        var builds = Counter(chart, "_seriesDrawingBuildCount");
        clock.Advance(TimeSpan.FromSeconds(5)); chart.UpdateAnimationFrame();
        chart.ShowCrosshair = true; TemplateFeaturesHost.Record(chart);
        Assert.Equal(builds, Counter(chart, "_seriesDrawingBuildCount"));
        Assert.All(paused, previous => Assert.Contains(CacheValues(chart), current => ReferenceEquals(previous, current)));
        chart.CancelAnimations(); host.Drain(); TemplateFeaturesHost.Record(chart);
        Assert.DoesNotContain(CacheValues(chart), current => paused.Any(previous => ReferenceEquals(previous, current)));
    }

    [AvaloniaFact]
    public void Large_classic_scatter_retains_compact_centers_and_replays_without_projection_or_record_objects()
    {
        var series = new ScatterSeries { MarkerSize = 7,
            ItemsSource = Enumerable.Range(0, 100_000).Select(i => new ChartPoint(i % 10, i % 10)).ToArray() };
        var data = series.CreateSnapshot();
        var projection = new CountingProjection();
        var retained = CartesianSeriesRenderer.Scatter.CreateRetainedDrawing(new(series, data, projection, Brushes.Blue));
        var arrays = retained.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Select(field => field.GetValue(retained)).OfType<Array>().ToArray();
        var centers = Assert.Single(arrays);
        Assert.IsType<Point[]>(centers); Assert.Equal(100_000, centers.Length);
        var projected = projection.ProjectCount;
        using var target = new NativeBoundary(recordOperations: false);
        retained.Draw(target.Drawing);
        Assert.Equal(projected, projection.ProjectCount);
        Assert.Equal(100_000, target.Recorder.DrawCount);
    }

    private static ChartSeries MakeSeries(string kind)
    {
        ChartSeries series = kind switch
        {
            "line" => new LineSeries(),
            "bar" => new BarSeries { MarkerSize = 0 },
            "bar-with-markers" => new BarSeries(),
            "scatter" => new ScatterSeries(),
            "smooth" => new LineSeries { Interpolation = ChartLineInterpolation.Smooth },
            "area" => new AreaSeries(),
            "range-bar" => new RangeBarSeries(),
            "lollipop" => new LollipopSeries(),
            "waterfall" => new WaterfallSeries(),
            "bubble" => new BubbleSeries(),
            "pictorial" => new PictorialBarSeries { Geometry = new RectangleGeometry(new Rect(0, 0, 1, 2)) },
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        series.ItemsSource = new ChartPoint[] { new(1, 2), new(2, 8), new(3, double.NaN), new(4, 4), new(5, 7) };
        if (kind == "range-bar")
        {
            series.CategoryPath = "X"; series.LowPath = "Low"; series.HighPath = "High";
            series.ItemsSource = new[] { new { X = 1d, Low = 0d, High = 2d }, new { X = 2d, Low = 1d, High = 8d } };
        }
        return series;
    }

    private static ChartSeriesRenderContext MakeContext(ChartSeries series, CountingProjection projection, IBrush? brush = null)
    {
        var points = series.CreateSnapshot().Points.Select((point, i) => point with
        {
            Low = i, High = i + 4, Weight = i + 1,
        }).ToArray();
        var data = new ChartDataSnapshot(0, points);
        if (series.Renderer is ChartPointSeriesRenderer)
            data = data.WithPointStyles(ChartPointEncoding.CreateStyles(series, data, brush ?? Brushes.CornflowerBlue));
        return new(series, data, projection, brush ?? Brushes.CornflowerBlue, 1, 2, 28);
    }

    private static ChartRetainedSeriesDrawing Retain(ChartSeries series, ChartSeriesRenderContext context) =>
        series.Renderer is CartesianSeriesRenderer legacy ? legacy.CreateRetainedDrawing(context) : ChartRetainedSeriesDrawing.Record(series.Renderer, context);

    private static CartesianChart MakeChart(params ChartSeries[] series)
    {
        var chart = new CartesianChart
        {
            Width = 700, Height = 400, ShowLegend = false,
            XAxis = new NumericAxis { Minimum = 0, Maximum = 5 },
            YAxis = new NumericAxis { Minimum = 0, Maximum = 10 },
        };
        foreach (var value in series) chart.Series.Add(value);
        return chart;
    }

    private static object[] CacheValues(CartesianChart chart) => ((IDictionary)typeof(CartesianChart)
        .GetField("_seriesDrawings", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(chart)!).Values.Cast<object>().ToArray();
    private static long Counter(CartesianChart chart, string name) => (long)typeof(CartesianChart)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(chart)!;

    private sealed class CountingProjection : IChartProjection
    {
        public int ProjectCount { get; private set; }
        public Rect PlotArea => new(0, 0, 200, 150);
        public double YOrigin => 140;
        public double MapX(double value) => value * 10 + 20;
        public double MapY(double value) => 140 - value * 10;
        public Point Project(ChartPoint point) { ProjectCount++; return new(MapX(point.X), MapY(point.Y)); }
        public ChartPoint Unproject(Point point) => new((point.X - 20) / 10, (140 - point.Y) / 10);
    }

    private sealed class ApplicationSeries(IChartSeriesRenderer renderer) : ChartSeries
    {
        public override IChartSeriesRenderer Renderer => renderer;
    }
    private sealed class ManualClock : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        public void Advance(TimeSpan duration) => _ticks += duration.Ticks;
    }
    private sealed class FoldedAxis : ChartAxis
    {
        // No StyledProperty notification: an extension may use caller state in
        // Transform. Built-in series must not acquire a new revision requirement.
        public bool FoldInterior;
        public override double Transform(double value) => FoldInterior && value > 0 && value < 10 ? 10 - value : value;
    }
    private sealed class ApplicationRenderer : IChartSeriesRenderer
    {
        public int RenderCount { get; private set; }
        public bool IncludesZero => false;
        public bool UsesCategorySlots => false;
        public ChartLegendSymbol LegendSymbol => ChartLegendSymbol.Line;
        public ChartSeriesBounds? GetBounds(ChartSeries series, ChartDataSnapshot data) => new(0, 5, 0, 10);
        public void Render(DrawingContext drawing, ChartSeriesRenderContext context)
        {
            RenderCount++;
            drawing.DrawLine(new Pen(Brushes.Blue, 1), new(20, 20), new(30 + RenderCount, 30));
        }
        public ChartSeriesHit? HitTest(ChartSeriesRenderContext context, Point position, double tolerance) => null;
    }

    // Test-only adapter at Avalonia 12's platform boundary, like the existing
    // annotation rectangle oracle. Product code neither reflects nor implements
    // Avalonia's internal abstract DrawingContext members.
    internal sealed class NativeBoundary : IDisposable
    {
        public DrawingContext Drawing { get; }
        public BoundaryRecorder Recorder { get; }
        public NativeBoundary(bool recordOperations = true)
        {
            var proxy = DispatchProxy.Create<IDrawingContextImpl, BoundaryRecorder>();
            Recorder = (BoundaryRecorder)(object)proxy; Recorder.RecordOperations = recordOperations;
            var wrapper = typeof(DrawingContext).Assembly.GetType("Avalonia.Media.PlatformDrawingContext", true)!;
            Drawing = (DrawingContext)wrapper.GetConstructor([typeof(IDrawingContextImpl), typeof(bool)])!.Invoke([proxy, false]);
        }
        public void Dispose() => Drawing.Dispose();
    }

    public class BoundaryRecorder : DispatchProxy
    {
        public bool RecordOperations { get; set; } = true;
        public List<string> Operations { get; } = [];
        public List<IBrush> Brushes { get; } = [];
        public List<Rect> Rectangles { get; } = [];
        public int DrawCount { get; private set; }
        public Matrix Transform { get; private set; } = Matrix.Identity;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case "get_Transform": return Transform;
                case "set_Transform": Transform = (Matrix)args![0]!; return null;
                case "DrawRectangle": case "DrawEllipse": case "DrawLine": case "DrawGeometry":
                    DrawCount++;
                    if (targetMethod.Name == "DrawRectangle")
                        foreach (var rounded in args!.OfType<RoundedRect>()) Rectangles.Add(rounded.Rect);
                    if (RecordOperations)
                    {
                        foreach (var brush in args!.OfType<IBrush>()) Brushes.Add(brush);
                        Operations.Add(targetMethod.Name + ":" + Transform + ":" + string.Join("|", args!.Select(Describe)));
                    }
                    return null;
                case "GetFeature": return null;
                case "Clear": case "DrawBitmap": case "DrawRegion": case "DrawGlyphRun": case "Dispose":
                case "PushClip": case "PopClip": case "PushLayer": case "PopLayer":
                case "PushOpacity": case "PopOpacity": case "PushOpacityMask": case "PopOpacityMask":
                case "PushGeometryClip": case "PopGeometryClip": case "PushRenderOptions": case "PopRenderOptions":
                case "PushTextOptions": case "PopTextOptions": return null;
                default: throw new NotSupportedException($"Unexpected platform operation: {targetMethod}.");
            }
        }
        private static string Describe(object? value) => value switch
        {
            null => "null",
            ISolidColorBrush solid => $"solid:{solid.Color}:{solid.Opacity}:{solid.Transform?.Value}",
            ILinearGradientBrush gradient => $"gradient:{gradient.Opacity}:{gradient.Transform?.Value}:{gradient.StartPoint}:{gradient.EndPoint}:" +
                string.Join(";", gradient.GradientStops.Select(stop => $"{stop.Offset}:{stop.Color}")),
            IPen pen => $"pen:{pen.Thickness}:{pen.LineCap}:{pen.LineJoin}:{Describe(pen.Brush)}",
            IGeometryImpl => "geometry",
            _ => value.ToString() ?? string.Empty,
        };
    }
}
