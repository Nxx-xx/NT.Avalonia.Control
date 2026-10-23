using System.ComponentModel;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class ScatterBubbleDataTests
{
    private sealed record Row(string Id, double X, double Y, double? Size, object? Tone, double? Weight = null);
    private sealed class MutableRow : INotifyPropertyChanged
    {
        public string Id => "stable";
        public double X => 5;
        public double Y => 50;
        private double _size = 25;
        private double _tone;
        public double Size { get => _size; set { _size = value; PropertyChanged?.Invoke(this, new(nameof(Size))); } }
        public double Tone { get => _tone; set { _tone = value; PropertyChanged?.Invoke(this, new(nameof(Tone))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    private sealed class Clock : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        public void Advance(double ms) => _ticks += TimeSpan.FromMilliseconds(ms).Ticks;
    }
    private static BubbleSeries Bubble(params Row[] rows) => new()
    {
        ItemsSource = rows, CategoryPath = "X", ValuePath = "Y", IdPath = "Id", SizePath = "Size", ColorPath = "Tone",
        SizeMaximum = 100, ColorMinimum = 0, ColorMaximum = 100, MinimumSize = 0, MaximumSize = 40,
        MinimumColor = Colors.Blue, MaximumColor = Colors.Red, MarkerStrokeThickness = 0,
    };
    private static ChartPointHit Current(CartesianChart chart, ChartSeries series, int index = 0)
    {
        Assert.True(chart.NavigateToPoint(series, index)); return Assert.IsType<ChartPointHit>(chart.CurrentPoint);
    }

    [AvaloniaFact]
    public void Bubble_scene_retains_raw_mapping_and_source_identity_while_exposing_derived_size_and_color()
    {
        var row = new Row("business", 5, 50, 25, 0d);
        var series = Bubble(row);
        using var host = new SegmentTestHost(new(0, 10), new(0, 100), series);
        var hit = Current(host.Chart, series);
        Assert.Equal(25, hit.SizeValue); Assert.Equal(0d, hit.ColorValue); Assert.Equal(20, hit.MarkerDiameter);
        Assert.Equal(Colors.Blue, Assert.IsAssignableFrom<ISolidColorBrush>(hit.PointFill).Color);
        Assert.Same(row, hit.Item); Assert.Equal("business", hit.StableId); Assert.Equal(0, hit.Index);
        Assert.Equal(new ChartPoint(5, 50), hit.RawPoint); Assert.Equal(hit.Point, hit.RawPoint);
        var source = series.CreateSnapshot().Points[0];
        Assert.Equal(25, source.SizeValue); Assert.Equal(0d, source.ColorValue);
        Assert.Null(source.MarkerDiameter); Assert.Null(source.PointFill); Assert.Null(source.RawPoint);
        Assert.Equal(0, host.Chart.HitTestPoint(hit.Position + new Vector(9, 0), 0)!.Index);
    }

    [AvaloniaTheory]
    [InlineData(ChartSizeScale.Area, 20d)]
    [InlineData(ChartSizeScale.Diameter, 10d)]
    public void Auto_domain_uses_all_valid_target_samples_and_stays_stable_across_viewport_changes(ChartSizeScale scale, double expected)
    {
        var series = Bubble(new("a", 2, 50, 25, 0d), new("b", 8, 50, 100, 100d));
        series.SizeMaximum = null; series.SizeScale = scale;
        using var host = new SegmentTestHost(new(0, 10), new(0, 100), series);
        Assert.Equal(expected, Current(host.Chart, series).MarkerDiameter!.Value, 8);
        host.Chart.Viewport = new(0, 4, 0, 100); host.Drain();
        Assert.Equal(expected, Current(host.Chart, series).MarkerDiameter!.Value, 8);
        host.Chart.ResetViewport(); host.Drain();
        Assert.Equal(expected, Current(host.Chart, series).MarkerDiameter!.Value, 8);
    }

    [AvaloniaFact]
    public void Weight_path_is_Bubble_fallback_and_explicit_SizePath_takes_precedence_without_overwriting_weight()
    {
        var series = Bubble(new Row("weight", 5, 50, 25, 0d, 100));
        series.SizePath = null; series.WeightPath = "Weight";
        using var host = new SegmentTestHost(new(0, 10), new(0, 100), series);
        Assert.Equal(100, Current(host.Chart, series).SizeValue); Assert.Equal(40, Current(host.Chart, series).MarkerDiameter);
        var revision = series.SourceVersion;
        series.SizePath = "Size"; host.Drain();
        Assert.True(series.SourceVersion > revision); Assert.Equal(25, Current(host.Chart, series).SizeValue);
        Assert.Equal(20, Current(host.Chart, series).MarkerDiameter); Assert.Equal(100, series.CreateSnapshot().Points[0].Weight);
    }

    [AvaloniaFact]
    public void Size_and_color_item_notifications_refresh_selected_metadata_and_derived_style()
    {
        var row = new MutableRow();
        var series = Bubble(); series.ItemsSource = new[] { row };
        using var host = new SegmentTestHost(new(0, 10), new(0, 100), series);
        host.Chart.SelectedPoint = Current(host.Chart, series); host.Drain();
        var revision = series.SourceVersion;
        row.Size = 100; row.Tone = 100; host.Drain(); _ = host.Chart.PlotArea;
        Assert.True(series.SourceVersion > revision); Assert.Same(row, host.Chart.SelectedPoint!.Item);
        Assert.Equal(40, host.Chart.SelectedPoint.MarkerDiameter); Assert.Equal(100, host.Chart.SelectedPoint.SizeValue);
        Assert.Equal(Colors.Red, Assert.IsAssignableFrom<ISolidColorBrush>(host.Chart.SelectedPoint.PointFill).Color);
    }

    [AvaloniaTheory]
    [InlineData(null)]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(0d)]
    public void Missing_negative_and_nonfinite_sizes_hide_glyphs_but_keep_canonical_XY_available(double? size)
    {
        var series = Bubble(new Row("zero-or-invalid", 5, 50, size, 0d));
        using var host = new SegmentTestHost(new(0, 10), new(0, 100), series);
        var point = Current(host.Chart, series);
        Assert.Equal(0, point.MarkerDiameter); Assert.False(series.CreateSnapshot().Points[0].IsEmpty);
        Assert.Null(host.Chart.HitTestPoint(host.Project(new(5, 50)), 0));
        Assert.Equal(new ChartPoint(5, 50), point.Point);
    }

    [AvaloniaFact]
    public void Color_path_accepts_direct_Color_brush_and_string_with_invalid_value_falling_back()
    {
        var brush = new SolidColorBrush(Colors.Purple);
        var series = Bubble(new("a", 1, 50, 100, Colors.Green), new("b", 3, 50, 100, brush),
            new("c", 5, 50, 100, "#FFFFA500"), new("d", 7, 50, 100, "invalid-color"));
        series.MarkerFill = Brushes.Teal;
        using var host = new SegmentTestHost(new(0, 8), new(0, 100), series);
        Assert.Equal(Colors.Green, Assert.IsAssignableFrom<ISolidColorBrush>(Current(host.Chart, series).PointFill).Color);
        Assert.Same(brush, Current(host.Chart, series, 1).PointFill);
        Assert.Equal(Colors.Orange, Assert.IsAssignableFrom<ISolidColorBrush>(Current(host.Chart, series, 2).PointFill).Color);
        Assert.Same(Brushes.Teal, Current(host.Chart, series, 3).PointFill);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ScatterLine_connects_physical_source_order_with_real_endpoint_metadata_and_an_independent_line_switch(bool markerless)
    {
        var series = new ScatterLineSeries
        {
            ItemsSource = new[] { new Row("first", 1, 20, 10, 0d), new Row("second", 8, 80, 20, 50d), new Row("third", 3, 50, 30, 100d) },
            CategoryPath = "X", ValuePath = "Y", IdPath = "Id", SizePath = "Size", ColorPath = "Tone", ShowMarkers = !markerless,
        };
        using var host = new SegmentTestHost(new(0, 10), new(0, 100), series);
        var segment = Assert.IsType<ChartSegmentHit>(host.Chart.HitTestSegment(host.Project(new(5.5, 65)), .01));
        Assert.Equal(1, segment.Start.Index); Assert.Equal(2, segment.End.Index); Assert.Equal(20, segment.Start.SizeValue);
        Assert.Equal(30, segment.End.SizeValue); Assert.Equal(.5, segment.Fraction, 7);
        host.Chart.TrackballMode = ChartTrackballMode.Vertical; host.Chart.TrackballSnapMode = ChartTrackballSnapMode.LinearInterpolation;
        host.Move(host.Project(new(5.5, 65))); Assert.NotEmpty(host.Chart.TrackballReadings);
        Assert.All(host.Chart.TrackballReadings, reading => Assert.Same(series, reading.Series));
        series.ShowLine = false; host.Drain(); Assert.Null(host.Chart.HitTestSegment(host.Project(new(5.5, 65)), .01));
    }

    [AvaloniaFact]
    public void XY_gaps_break_ScatterLine_but_missing_size_only_hides_that_marker()
    {
        var series = new ScatterLineSeries { ItemsSource = new[] { new Row("a", 1, 10, 1, 0d), new Row("b", 3, 30, null, 0d),
            new Row("gap", 5, double.NaN, 1, 0d), new Row("c", 7, 70, 1, 0d), new Row("d", 9, 90, 1, 0d) },
            CategoryPath = "X", ValuePath = "Y", SizePath = "Size" };
        using var host = new SegmentTestHost(new(0, 10), new(0, 100), series);
        Assert.NotNull(host.Chart.HitTestSegment(host.Project(new(2, 20)), .01));
        Assert.Null(host.Chart.HitTestSegment(host.Project(new(5, 50)), .01));
        Assert.NotNull(host.Chart.HitTestSegment(host.Project(new(8, 80)), .01));
    }

    [AvaloniaFact]
    public void Point_style_animation_uses_the_current_diameter_and_color_while_raw_encoding_stays_target_data()
    {
        var series = Bubble(new Row("stable", 5, 50, 25, 0d));
        using var host = new SegmentTestHost(new(0, 10), new(0, 100), series);
        var clock = new Clock(); host.Chart.AnimationTimeProvider = clock; host.Chart.AnimationDuration = TimeSpan.FromSeconds(1);
        host.Chart.AnimationEasing = new LinearEasing(); host.Chart.AnimationTriggers = ChartAnimationTriggers.Data;
        series.ItemsSource = new[] { new Row("stable", 5, 50, 100, 100d) }; host.Drain();
        clock.Advance(500); host.Chart.UpdateAnimationFrame(); host.Drain();
        var hit = Current(host.Chart, series);
        Assert.Equal(30, hit.MarkerDiameter!.Value, 7); Assert.Equal(100, hit.SizeValue); Assert.Equal(100d, hit.ColorValue);
        Assert.Equal(Color.FromRgb(128, 0, 128), Assert.IsAssignableFrom<ISolidColorBrush>(hit.PointFill).Color);
        Assert.Equal(100, series.CreateSnapshot().Points[0].SizeValue); Assert.Null(series.CreateSnapshot().Points[0].MarkerDiameter);
        host.Chart.CancelAnimations(); host.Drain(); Assert.Equal(40, Current(host.Chart, series).MarkerDiameter);
    }

    [AvaloniaFact]
    public void Label_templates_and_default_Trackball_text_receive_same_current_encoding_as_the_plot()
    {
        var series = Bubble(new Row("label", 5, 50, 100, 75d)); series.MaximumSize = 120; series.ShowDataLabels = true;
        series.DataLabelTemplate = new FuncDataTemplate<ChartDataLabelContext>((_, _) => new TextBlock { Name = "EncodedLabel", Text = "encoded" });
        using var host = new SegmentTestHost(new(0, 10), new(0, 100), series);
        var point = Current(host.Chart, series);
        var label = Assert.Single(host.Chart.GetVisualDescendants().OfType<TextBlock>(), text => text.Name == "EncodedLabel");
        var context = Assert.IsType<ChartDataLabelContext>(label.DataContext); Assert.Equal(120, context.Data.MarkerDiameter);
        var root = label.TranslatePoint(new Point(), host.Chart)!.Value;
        Assert.True(root.Y + label.Bounds.Height < point.Position.Y - 60);
        host.Chart.TrackballMode = ChartTrackballMode.Vertical;
        host.Chart.TrackballContentTemplate = new FuncDataTemplate<ChartTooltipContext>((_, _) => new Border { Name = "EncodedReadout" });
        host.Move(point.Position);
        var card = Assert.Single(host.Chart.GetVisualDescendants().OfType<Border>(), border => border.Name == "EncodedReadout");
        var readout = Assert.IsType<ChartTooltipContext>(card.DataContext);
        Assert.Contains("大小值: 100", readout.Text); Assert.Contains("颜色值: 75", readout.Text);
        Assert.Equal(120, Assert.Single(readout.Hits).MarkerDiameter);
    }

    [AvaloniaFact]
    public void Published_finite_empty_sample_keeps_a_real_line_gap_and_cannot_reappear_in_shared_readouts()
    {
        var series = new ScatterLineSeries { ItemsSource = new[] { new Row("a", 1, 10, 1, 0d), new Row("gap", 5, 50, 1, 0d), new Row("b", 9, 90, 1, 0d) },
            CategoryPath = "X", ValuePath = "Y", IdPath = "Id" };
        using var host = new SegmentTestHost(new(0, 10), new(0, 100), series);
        var source = series.CreateSnapshot();
        Assert.True(series.TryPublishSnapshot(new(source.Version, source.Points.Select(point => point.SourceIndex == 1 ? point with { IsEmpty = true } : point).ToArray()), source.Version));
        host.Drain();
        Assert.Null(host.Chart.HitTestSegment(host.Project(new(5, 50)), .01));
        host.Chart.TooltipMode = ChartTooltipMode.SharedX; host.Move(host.Project(new(5, 50)));
        Assert.DoesNotContain(host.Chart.HoveredPoints, point => point.Index == 1);
        Assert.False(host.Chart.NavigateToPoint(series, 1));
        Assert.True(series.CreateSnapshot().Points[1].IsEmpty); Assert.Equal(new ChartPoint(5, 50), series.CreateSnapshot().Points[1].Point);
    }

    [AvaloniaFact]
    public void Zero_line_thickness_suppresses_ScatterLine_segment_queries_but_keeps_point_markers()
    {
        var series = new ScatterLineSeries { ItemsSource = new ChartPoint[] { new(2, 20), new(8, 80) }, StrokeThickness = 0 };
        using var host = new SegmentTestHost(new(0, 10), new(0, 100), series);
        Assert.Null(host.Chart.HitTestSegment(host.Project(new(5, 50)), .01));
        Assert.NotNull(host.Chart.HitTestPoint(host.Project(new(2, 20)), 0));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Bubble_diameter_stays_in_DIP_on_logarithmic_and_inversed_axes(bool inversed)
    {
        var series = Bubble(new Row("log", 10, 100, 100, 0d));
        using var host = new SegmentTestHost(new(1, 100, Log: true, Inverse: inversed), new(1, 10000, Log: true, Inverse: inversed), series);
        var point = Current(host.Chart, series);
        SegmentTestHost.AssertPosition(host.Project(new(10, 100)), point.Position);
        Assert.Equal(40, point.MarkerDiameter);
        Assert.NotNull(host.Chart.HitTestPoint(point.Position + new Vector(19, 0), 0));
        Assert.Null(host.Chart.HitTestPoint(point.Position + new Vector(21, 0), 0));
    }

    [AvaloniaFact]
    public void Bubble_entrance_grows_current_geometry_and_reduce_motion_finishes_at_the_target()
    {
        var series = Bubble(new Row("entrance", 5, 50, 100, 0d));
        using var host = new SegmentTestHost(new(0, 10), new(0, 100), series);
        var clock = new Clock(); host.Chart.AnimationTimeProvider = clock; host.Chart.AnimationDuration = TimeSpan.FromSeconds(1);
        host.Chart.AnimationEasing = new LinearEasing(); host.Chart.AnimationTriggers = ChartAnimationTriggers.Entrance;
        host.Chart.ReplayEntranceAnimation(); host.Drain(); clock.Advance(500); host.Chart.UpdateAnimationFrame(); host.Drain();
        var point = Current(host.Chart, series); Assert.Equal(20, point.MarkerDiameter!.Value, 7); Assert.Equal(25, point.Point.Y, 7);
        Assert.Equal(new ChartPoint(5, 50), point.RawPoint); Assert.Equal(100, point.SizeValue);
        host.Chart.ReduceMotion = true; host.Drain();
        point = Current(host.Chart, series); Assert.Equal(40, point.MarkerDiameter); Assert.Equal(50, point.Point.Y);
        Assert.Equal(ChartAnimationState.Idle, host.Chart.AnimationState);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Thick_square_stroke_hit_uses_only_the_visible_plot_intersection(bool partiallyVisible)
    {
        var series = new BubbleSeries { MarkerShape = ChartMarkerShape.Square, MarkerSize = 10, MarkerStrokeThickness = 12 };
        using var host = new SegmentTestHost(new(0, 10), new(0, 100), series);
        var plot = host.Chart.PlotArea;
        var center = new Point(plot.Left - 10, plot.Top + (partiallyVisible ? 10 : -10));
        series.ItemsSource = new[] { host.Unproject(center) }; host.Drain();
        Assert.Equal(plot, host.Chart.PlotArea);
        var query = new Point(plot.Left + 2, plot.Top);
        if (partiallyVisible)
        {
            Assert.Null(host.Chart.HitTestPoint(query, 2.61));
            Assert.Equal(0, host.Chart.HitTestPoint(query, 2.62)!.Index);
        }
        else Assert.Null(host.Chart.HitTestPoint(query, 10_000));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Bubble_hit_tolerance_excludes_footprint_removed_by_horizontal_or_vertical_axis_breaks(bool horizontal)
    {
        var broken = new SegmentOracleAxis(0, 100, BreakStart: 40, BreakEnd: 60, Compression: 1);
        var ordinary = new SegmentOracleAxis(0, 100);
        var series = new BubbleSeries { MarkerSize = 30, MarkerStrokeThickness = 0 };
        using var host = new SegmentTestHost(horizontal ? broken : ordinary, horizontal ? ordinary : broken, series);
        var first = host.Project(new(40, 40)); var last = host.Project(new(60, 60));
        var low = horizontal ? Math.Min(first.X, last.X) : Math.Min(first.Y, last.Y);
        var high = horizontal ? Math.Max(first.X, last.X) : Math.Max(first.Y, last.Y);
        var plot = host.Chart.PlotArea;
        var center = horizontal ? new Point(low - 10, plot.Center.Y) : new Point(plot.Center.X, low - 10);
        series.ItemsSource = new[] { host.Unproject(center) }; host.Drain();
        var query = horizontal ? new Point(high + 1, center.Y) : new Point(center.X, high + 1);
        Assert.Null(host.Chart.HitTestPoint(query, high - low - 2));
        Assert.Equal(0, host.Chart.HitTestPoint(query, high - low + 1.01)!.Index);
    }
}
