using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class ChartsAxesIntegrationTests
{
    private static readonly Color Ink = Color.Parse("#CB11A5");
    private static readonly IBrush InkBrush = new SolidColorBrush(Ink);
    private static readonly Color MinorInk = Color.Parse("#018C98");

    [AvaloniaTheory]
    [InlineData("numeric")]
    [InlineData("date")]
    [InlineData("log")]
    public void Minor_grid_uses_each_axis_domain_and_visibility_does_not_change_the_view(string kind)
    {
        ChartAxis axis = kind switch
        {
            "date" => new DateTimeAxis { Minimum = 0, Maximum = 86_400_000, Interval = 6, IntervalUnit = DateTimeIntervalUnit.Hour, MinorInterval = 3_600_000 },
            "log" => new LogarithmicAxis { Minimum = 1, Maximum = 1000, Interval = 1, MinorTickCount = 1 },
            _ => new NumericAxis { Minimum = 0, Maximum = 10, Interval = 2, MinorInterval = .5 },
        };
        axis.ShowMinorGridLines = true;
        axis.MinorGridLineBrush = new SolidColorBrush(MinorInk);
        var chart = NewChart(); chart.XAxis = axis;
        using var fixture = new AxesFixture(chart);
        var range = chart.VisibleRange;
        var minors = Flatten(Render(chart)).OfType<GeometryDrawing>().Where(drawing => IsColor(drawing.Pen?.Brush, MinorInk)).ToArray();
        Assert.NotEmpty(minors);
        var first = minors.Min(drawing => drawing.Geometry!.Bounds.Center.X);
        var fraction = kind switch { "date" => 1d / 24, "log" => 1d / 6, _ => .05 };
        Assert.Equal(chart.PlotArea.Left + chart.PlotArea.Width * fraction, first, 5);
        axis.ShowMinorGridLines = false;
        Assert.DoesNotContain(Flatten(Render(chart)).OfType<GeometryDrawing>(), drawing => IsColor(drawing.Pen?.Brush, MinorInk));
        Assert.Equal(range, chart.VisibleRange);
    }

    [AvaloniaFact]
    public void Axis_title_label_baseline_ticks_and_grid_have_independent_styles_and_visibility()
    {
        var chart = NewChart();
        var axis = chart.YAxis;
        axis.Title = "Styled axis"; axis.TitleBrush = Brushes.Red; axis.LabelBrush = Brushes.Green;
        axis.AxisBrush = Brushes.Blue; axis.TickBrush = Brushes.Orange; axis.MinorTickBrush = Brushes.Purple;
        axis.GridLineBrush = Brushes.Brown; axis.MinorGridLineBrush = Brushes.Teal;
        axis.Interval = 2; axis.MinorInterval = 1; axis.ShowMinorGridLines = true;
        axis.AxisThickness = 3; axis.TickThickness = 2; axis.GridLineThickness = 2; axis.MinorGridLineThickness = .5;
        axis.TitleFontSize = 20; axis.LabelFontSize = 15; axis.TickLength = 8; axis.MinorTickLength = 3;
        using var fixture = new AxesFixture(chart);
        var range = chart.VisibleRange;
        var drawing = Render(chart);
        Assert.Contains(Flatten(drawing).OfType<GlyphRunDrawing>(), item => IsColor(item.Foreground, Colors.Red));
        Assert.Contains(Flatten(drawing).OfType<GlyphRunDrawing>(), item => IsColor(item.Foreground, Colors.Green));
        Assert.Contains(Flatten(drawing).OfType<GeometryDrawing>(), item => IsColor(item.Pen?.Brush, Colors.Blue) && item.Pen!.Thickness == 3);
        Assert.Contains(Flatten(drawing).OfType<GeometryDrawing>(), item => IsColor(item.Pen?.Brush, Colors.Orange));
        Assert.Contains(Flatten(drawing).OfType<GeometryDrawing>(), item => IsColor(item.Pen?.Brush, Colors.Purple));
        axis.ShowAxis = false; axis.ShowTitle = false; axis.ShowLabels = false; axis.ShowTicks = false;
        axis.ShowGridLines = false; axis.ShowMinorGridLines = false;
        drawing = Render(chart);
        Assert.DoesNotContain(Flatten(drawing).OfType<GlyphRunDrawing>(), item => IsColor(item.Foreground, Colors.Red) || IsColor(item.Foreground, Colors.Green));
        Assert.DoesNotContain(Flatten(drawing).OfType<GeometryDrawing>(), item => new[] { Colors.Blue, Colors.Orange, Colors.Purple, Colors.Brown, Colors.Teal }.Any(color => IsColor(item.Pen?.Brush, color)));
        Assert.Equal(range, chart.VisibleRange);
    }

    [AvaloniaFact]
    public void Axis_dash_caps_and_join_are_rendered_and_axis_visibility_preserves_projection()
    {
        var chart = NewChart(); var axis = chart.YAxis;
        axis.AxisBrush = Brushes.Red; axis.GridLineBrush = Brushes.Green; axis.MinorGridLineBrush = Brushes.Blue;
        axis.ShowTicks = false; axis.ShowMinorGridLines = true; axis.MinorInterval = 1;
        axis.AxisDashStyle = new DashStyle(new[] { 4d, 2d }, 1); axis.AxisLineCap = PenLineCap.Square; axis.AxisLineJoin = PenLineJoin.Bevel;
        axis.GridLineDashStyle = new DashStyle(new[] { 3d, 2d }, 0); axis.GridLineCap = PenLineCap.Round; axis.GridLineJoin = PenLineJoin.Bevel;
        axis.MinorGridLineDashStyle = new DashStyle(new[] { 1d, 3d }, 0); axis.MinorGridLineCap = PenLineCap.Round; axis.MinorGridLineJoin = PenLineJoin.Round;
        axis.PlotBands.Add(new ChartPlotBand { Start = 2, End = 3, Fill = Brushes.Yellow });
        chart.Series.Add(new ScatterSeries { ItemsSource = new ChartPoint[] { new(5, 5) }, Fill = InkBrush, Stroke = InkBrush });
        using var fixture = new AxesFixture(chart);
        var shapes = Flatten(Render(chart)).OfType<GeometryDrawing>().ToArray();
        Assert.Contains(shapes, shape => PenMatches(shape.Pen, Colors.Red, new[] { 4d, 2d }, PenLineCap.Square, PenLineJoin.Bevel));
        Assert.Contains(shapes, shape => PenMatches(shape.Pen, Colors.Green, new[] { 3d, 2d }, PenLineCap.Round, PenLineJoin.Bevel));
        Assert.Contains(shapes, shape => PenMatches(shape.Pen, Colors.Blue, new[] { 1d, 3d }, PenLineCap.Round, PenLineJoin.Round));
        var range = chart.VisibleRange;
        axis.IsVisible = false;
        shapes = Flatten(Render(chart)).OfType<GeometryDrawing>().ToArray();
        Assert.DoesNotContain(shapes, shape => IsColor(shape.Pen?.Brush, Colors.Red) || IsColor(shape.Pen?.Brush, Colors.Green) || IsColor(shape.Pen?.Brush, Colors.Blue) || IsColor(shape.Brush, Colors.Yellow));
        Assert.Equal(range, chart.VisibleRange);
        Assert.Equal(new ChartPoint(5, 5), Assert.IsType<ChartPointHit>(chart.HitTestPoint(chart.PlotArea.Center)).Point);

        static bool PenMatches(IPen? pen, Color color, double[] dash, PenLineCap cap, PenLineJoin join) =>
            pen is not null && IsColor(pen.Brush, color) && pen.DashStyle is { Dashes: { } dashes } && dashes.SequenceEqual(dash) && pen.LineCap == cap && pen.LineJoin == join;
    }

    [AvaloniaFact]
    public void Null_and_empty_bound_break_sources_have_distinct_precedence_and_observe_collection_changes()
    {
        var chart = NewChart(); var axis = chart.XAxis;
        var inline = new ChartScaleBreak { Start = 4, End = 6 };
        axis.ScaleBreaks.Add(inline);
        var source = new System.Collections.ObjectModel.ObservableCollection<ChartScaleBreak>(); axis.ScaleBreaksSource = source;
        chart.Series.Add(new ScatterSeries { ItemsSource = new ChartPoint[] { new(2, 5), new(8, 5) } });
        using var fixture = new AxesFixture(chart);
        Assert.False(axis.IsInScaleBreak(5)); Assert.Null(inline.Parent);
        var bound = new ChartScaleBreak { Start = 2, End = 3 }; source.Add(bound);
        Assert.True(axis.IsInScaleBreak(2.5)); Assert.Same(axis, bound.Parent);
        bound.End = 3.5; Assert.True(axis.IsInScaleBreak(3.25));
        source.Remove(bound); Assert.False(axis.IsInScaleBreak(2.5)); Assert.Null(bound.Parent);
        fixture.Window.Content = null; source.Add(bound); bound.Start = 1;
        fixture.Window.Content = chart; fixture.Drain(); Assert.True(axis.IsInScaleBreak(1.5));
        axis.ScaleBreaksSource = null;
        Assert.True(axis.IsInScaleBreak(5)); Assert.False(axis.IsInScaleBreak(2.5)); Assert.Same(axis, inline.Parent); Assert.Null(bound.Parent);
    }

    [AvaloniaFact]
    public void Breaks_merge_and_ignore_invalid_ranges_and_refresh_after_item_changes()
    {
        var axis = new NumericAxis();
        var first = new ChartScaleBreak { Start = 2, End = 4 };
        axis.ScaleBreaks.Add(first); axis.ScaleBreaks.Add(new() { Start = 4, End = 6 });
        axis.ScaleBreaks.Add(new() { Start = 8, End = 7 });
        Assert.False(axis.IsInScaleBreak(2)); Assert.True(axis.IsInScaleBreak(4)); Assert.False(axis.IsInScaleBreak(6));
        Assert.False(axis.IsInScaleBreak(7.5));
        var chart = NewChart(); chart.XAxis = axis;
        axis.Minimum = 0; axis.Maximum = 10;
        using var fixture = new AxesFixture(chart);
        first.Start = 1;
        Assert.True(axis.IsInScaleBreak(1.5));
        axis.ScaleBreaks.Remove(first);
        Assert.False(axis.IsInScaleBreak(3));
        fixture.Window.Content = null;
        axis.ScaleBreaks[0].Start = 3;
        fixture.Window.Content = chart; fixture.Drain();
        Assert.True(axis.IsInScaleBreak(3.5));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Crossing_line_is_clipped_out_of_the_compressed_break_and_interior_points_cannot_be_hit(bool vertical)
    {
        var chart = NewChart();
        var axis = vertical ? chart.YAxis : chart.XAxis;
        axis.ScaleBreaks.Add(new ChartScaleBreak { Start = 4, End = 6, CompressionRatio = .1 });
        chart.Series.Add(new LineSeries
        {
            Stroke = InkBrush, MarkerSize = 0, StrokeThickness = 3,
            ItemsSource = vertical ? new ChartPoint[] { new(5, 2), new(5, 8) } : new ChartPoint[] { new(2, 5), new(8, 5) },
        });
        chart.Series.Add(new ScatterSeries { Fill = InkBrush, Stroke = InkBrush, ItemsSource = new ChartPoint[] { new(5, 5) } });
        using var fixture = new AxesFixture(chart);
        var plot = chart.PlotArea;
        var drawing = Render(chart);
        Assert.True(DrawnAt(drawing, plot.Center, Ink, ignoreClips: true), "The crossing series stroke must exist before the break clip is applied.");
        Assert.False(DrawnAt(drawing, plot.Center, Ink));
        // Check segment interiors; the fixture deliberately uses one horizontal or vertical line.
        var first = vertical ? new Point(plot.Center.X, plot.Bottom - plot.Height * 3 / 8.2) : new Point(plot.Left + plot.Width * 3 / 8.2, plot.Center.Y);
        var last = vertical ? new Point(plot.Center.X, plot.Bottom - plot.Height * 5.2 / 8.2) : new Point(plot.Left + plot.Width * 5.2 / 8.2, plot.Center.Y);
        Assert.True(DrawnAt(drawing, first, Ink), $"First segment: {DrawingProbe(drawing, first, Ink)}");
        Assert.True(DrawnAt(drawing, last, Ink), $"Last segment: {DrawingProbe(drawing, last, Ink)}");
        Assert.Null(chart.HitTestPoint(plot.Center, tolerance: 1));
    }

    [AvaloniaFact]
    public void Repeated_plot_bands_have_text_and_render_in_the_requested_series_layer()
    {
        var chart = NewChart();
        var behind = new ChartPlotBand { Start = 1, End = 2, RepeatInterval = 3, RepeatCount = 3, Text = "Window", Fill = Brushes.Red, Foreground = Brushes.DarkRed, Layer = ChartPlotBandLayer.BehindSeries };
        var above = new ChartPlotBand { Start = 4, End = 6, Text = "Threshold", Fill = Brushes.Blue, Foreground = Brushes.DarkBlue, FontSize = 18, Layer = ChartPlotBandLayer.AboveSeries };
        chart.XAxis.PlotBands.Add(behind); chart.YAxis.PlotBands.Add(above);
        chart.Series.Add(new LineSeries { Stroke = InkBrush, MarkerSize = 0, ItemsSource = new ChartPoint[] { new(0, 5), new(10, 5) } });
        using var fixture = new AxesFixture(chart);
        var drawings = Flatten(Render(chart)).ToList();
        var rectangles = drawings.OfType<GeometryDrawing>().Where(item => IsColor(item.Brush, Colors.Red)).ToArray();
        Assert.Equal(3, rectangles.Length);
        Assert.All(rectangles, item => Assert.Equal(chart.PlotArea.Width / 10, item.Geometry!.Bounds.Width, 5));
        var seriesIndex = drawings.FindIndex(item => item is GeometryDrawing shape && IsColor(shape.Pen?.Brush, Ink));
        Assert.True(drawings.FindLastIndex(item => item is GeometryDrawing shape && IsColor(shape.Brush, Colors.Red)) < seriesIndex);
        Assert.True(drawings.FindIndex(item => item is GeometryDrawing shape && IsColor(shape.Brush, Colors.Blue)) > seriesIndex);
        Assert.Contains(drawings.OfType<GlyphRunDrawing>(), item => IsColor(item.Foreground, Colors.DarkRed));
        Assert.Contains(drawings.OfType<GlyphRunDrawing>(), item => IsColor(item.Foreground, Colors.DarkBlue));
        var range = chart.VisibleRange;
        behind.RepeatCount = 0; above.Start = 8; above.End = 7;
        Assert.DoesNotContain(Flatten(Render(chart)).OfType<GeometryDrawing>(), item => IsColor(item.Brush, Colors.Red) || IsColor(item.Brush, Colors.Blue));
        Assert.Equal(range, chart.VisibleRange);
    }

    [AvaloniaTheory]
    [InlineData("category")]
    [InlineData("date")]
    [InlineData("log")]
    public void Plot_band_bounds_use_category_slots_date_milliseconds_or_logarithmic_raw_values(string kind)
    {
        var chart = NewChart();
        double start, end, fraction;
        switch (kind)
        {
            case "date": chart.XAxis = new DateTimeAxis { Minimum = 0, Maximum = 86_400_000 }; start = 21_600_000; end = 43_200_000; fraction = .25; break;
            case "log": chart.XAxis = new LogarithmicAxis { Minimum = 1, Maximum = 1000 }; start = 10; end = 100; fraction = 1d / 3; break;
            default: chart.XAxis = new CategoryAxis { Labels = new[] { "A", "B", "C", "D" }, Minimum = -.5, Maximum = 3.5, PlotMode = CategoryPlotMode.BetweenTicks }; start = .5; end = 1.5; fraction = .25; break;
        }
        chart.XAxis.PlotBands.Add(new ChartPlotBand { Start = start, End = end, Fill = InkBrush });
        using var fixture = new AxesFixture(chart);
        var band = Assert.Single(Flatten(Render(chart)).OfType<GeometryDrawing>(), item => IsColor(item.Brush, Ink));
        Assert.Equal(chart.PlotArea.Width * fraction, band.Geometry!.Bounds.Width, 5);
    }

    [AvaloniaFact]
    public void Plot_band_nan_bounds_opacity_text_alignment_repeat_until_and_visibility_are_rendered()
    {
        var chart = NewChart();
        var band = new ChartPlotBand
        {
            Start = double.NaN, End = double.NaN, Fill = InkBrush, Text = "Zone", Foreground = Brushes.DarkOrange, Opacity = .25,
            HorizontalTextAlignment = ChartBandTextAlignment.Start, VerticalTextAlignment = ChartBandTextAlignment.Start,
        };
        chart.XAxis.PlotBands.Add(band);
        using var fixture = new AxesFixture(chart);
        var drawing = Render(chart); var rectangle = Assert.Single(Flatten(drawing).OfType<GeometryDrawing>(), item => IsColor(item.Brush, Ink));
        Assert.Equal(chart.PlotArea, rectangle.Geometry!.Bounds);
        Assert.Contains(DrawingOpacity(drawing, 1), item => item.Drawing == rectangle && Math.Abs(item.Opacity - .25) < .000001);
        var start = TextBounds(drawing);
        band.HorizontalTextAlignment = ChartBandTextAlignment.End; band.VerticalTextAlignment = ChartBandTextAlignment.End;
        var end = TextBounds(Render(chart));
        Assert.True(end.Left > start.Left + 20 && end.Top > start.Top + 20);
        band.IsVisible = false;
        Assert.DoesNotContain(Flatten(Render(chart)).OfType<GeometryDrawing>(), item => IsColor(item.Brush, Ink));
        band.IsVisible = true; band.Start = 1; band.End = 1.5; band.RepeatInterval = 2; band.RepeatUntil = 6;
        Assert.Equal(3, Flatten(Render(chart)).OfType<GeometryDrawing>().Count(item => IsColor(item.Brush, Ink)));

        static Rect TextBounds(Drawing drawing)
        {
            var bounds = DrawingBounds(drawing, Matrix.Identity).Where(item => item.Drawing is GlyphRunDrawing glyph && IsColor(glyph.Foreground, Colors.DarkOrange)).Select(item => item.Bounds).ToArray();
            Assert.NotEmpty(bounds); return bounds.Aggregate((first, last) => first.Union(last));
        }
    }

    [AvaloniaFact]
    public void Wheel_zoom_with_a_break_keeps_the_original_point_under_the_cursor()
    {
        var chart = NewChart(); chart.IsZoomEnabled = true; chart.InteractionAxes = ChartInteractionAxes.X;
        chart.XAxis.ScaleBreaks.Add(new ChartScaleBreak { Start = 4, End = 6, CompressionRatio = .1 });
        chart.Series.Add(new ScatterSeries { ItemsSource = new ChartPoint[] { new(2, 5) }, MarkerSize = 8 });
        using var fixture = new AxesFixture(chart);
        var position = new Point(chart.PlotArea.Left + chart.PlotArea.Width * 2 / 8.2, chart.PlotArea.Center.Y);
        var windowPoint = chart.TranslatePoint(position, fixture.Window)!.Value;
        fixture.Window.MouseWheel(windowPoint, new Vector(0, 2)); fixture.Drain();
        Assert.Equal(new ChartPoint(2, 5), Assert.IsType<ChartPointHit>(chart.HitTestPoint(position, tolerance: .1)).Point);
    }

    [AvaloniaTheory]
    [InlineData(ChartAxisLabelMode.Auto)]
    [InlineData(ChartAxisLabelMode.Hide)]
    [InlineData(ChartAxisLabelMode.Wrap)]
    [InlineData(ChartAxisLabelMode.Stagger)]
    [InlineData(ChartAxisLabelMode.Rotate)]
    public void Dense_category_labels_keep_finite_layout_and_category_hit_after_resize(ChartAxisLabelMode mode)
    {
        var chart = NewChart();
        chart.XAxis = new CategoryAxis { Title = "Long categories", PlotMode = CategoryPlotMode.BetweenTicks, Gap = .5, LabelMode = mode,
            LabelMaxWidth = 100, Labels = Enumerable.Range(0, 8).Select(index => $"Area {index}: long business label").ToArray() };
        chart.Series.Add(new ScatterSeries { Fill = InkBrush, Stroke = InkBrush, ItemsSource = Enumerable.Range(0, 8).Select(index => new ChartPoint(index, 5)).ToArray() });
        using var fixture = new AxesFixture(chart, 500);
        foreach (var width in new[] { 500d, 1100d, 500d })
        {
            fixture.Window.Width = width; fixture.Drain();
            var plot = chart.PlotArea; var range = chart.VisibleRange!.Value;
            Assert.True(plot.Width > 100 && plot.Height > 100);
            Assert.All(Flatten(Render(chart)), item => { var bounds = item.GetBounds(); Assert.True(double.IsFinite(bounds.X) && double.IsFinite(bounds.Y) && double.IsFinite(bounds.Width) && double.IsFinite(bounds.Height)); });
            var point = new Point(plot.Left + (3 - range.XMinimum) / (range.XMaximum - range.XMinimum) * plot.Width, plot.Center.Y);
            Assert.Equal(new ChartPoint(3, 5), Assert.IsType<ChartPointHit>(chart.HitTestPoint(point)).Point);
        }
    }

    [AvaloniaFact]
    public void Auto_label_layout_selects_a_fit_mode_for_narrow_width_and_returns_to_single_row_when_wide()
    {
        var chart = NewChart();
        chart.XAxis = new CategoryAxis { LabelMode = ChartAxisLabelMode.Auto, LabelMaxWidth = 100,
            Labels = Enumerable.Range(0, 8).Select(index => $"Long area label {index}").ToArray(), PlotMode = CategoryPlotMode.BetweenTicks };
        chart.Series.Add(new ScatterSeries { ItemsSource = Enumerable.Range(0, 8).Select(index => new ChartPoint(index, 5)).ToArray() });
        using var fixture = new AxesFixture(chart, 500);
        var narrow = Assert.IsType<ChartAxisLayout>(chart.GetAxisLayout(chart.XAxis));
        Assert.NotEqual(ChartAxisLabelMode.Auto, narrow.Mode);
        Assert.NotEqual(ChartAxisLabelMode.None, narrow.Mode);
        fixture.Window.Width = 1500; fixture.Drain();
        var wide = Assert.IsType<ChartAxisLayout>(chart.GetAxisLayout(chart.XAxis));
        Assert.Equal(ChartAxisLabelMode.None, wide.Mode); Assert.Equal(1, wide.Rows); Assert.Equal(1, wide.Stride);
        Assert.True(wide.LabelHeight < narrow.LabelHeight);
    }

    [AvaloniaTheory]
    [InlineData(ChartAxisPosition.Near)]
    [InlineData(ChartAxisPosition.Far)]
    public void Hidden_ticks_with_large_lengths_do_not_move_labels_outside_the_chart(ChartAxisPosition position)
    {
        var chart = NewChart();
        foreach (var axis in new[] { chart.XAxis, chart.YAxis })
        {
            axis.ShowTicks = false; axis.TickLength = 100; axis.MinorTickLength = 100;
            axis.Position = position; axis.LabelBrush = InkBrush;
        }
        using var fixture = new AxesFixture(chart);
        var labels = DrawingBounds(Render(chart), Matrix.Identity).Where(item => item.Drawing is GlyphRunDrawing glyph && IsColor(glyph.Foreground, Ink)).ToArray();
        Assert.NotEmpty(labels);
        Assert.All(labels, item =>
        {
            Assert.True(item.Bounds.Left >= -.5 && item.Bounds.Top >= -.5 && item.Bounds.Right <= chart.Bounds.Width + .5 && item.Bounds.Bottom <= chart.Bounds.Height + .5,
                $"{position} label bounds {item.Bounds} escape chart {chart.Bounds.Size} with hidden ticks.");
        });
    }

    [AvaloniaFact]
    public void Date_breaks_outside_the_supported_date_domain_are_clipped_to_the_visible_axis()
    {
        const double day = 86_400_000;
        var start = DateTimeAxis.ToUnixMilliseconds(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var chart = NewChart();
        chart.XAxis = new DateTimeAxis { Minimum = start, Maximum = start + 10 * day };
        chart.XAxis.ScaleBreaks.Add(new ChartScaleBreak { Start = DateTimeAxis.MinimumUnixMilliseconds - day, End = start + 2 * day, CompressionRatio = .1 });
        chart.XAxis.ScaleBreaks.Add(new ChartScaleBreak { Start = start + 8 * day, End = DateTimeAxis.MaximumUnixMilliseconds + day, CompressionRatio = .1 });
        chart.Series.Add(new LineSeries { Stroke = InkBrush, StrokeThickness = 3, MarkerSize = 0, ItemsSource = new ChartPoint[] { new(start + 2 * day, 5), new(start + 8 * day, 5) } });
        using var fixture = new AxesFixture(chart);
        var plot = chart.PlotArea; var drawing = Render(chart);
        var breakClip = Assert.Single(Flatten(drawing).OfType<DrawingGroup>(), item => item.ClipGeometry is CombinedGeometry &&
            Flatten(item).OfType<GeometryDrawing>().Any(shape => IsColor(shape.Pen?.Brush, Ink))).ClipGeometry!;
        Assert.True(StructuralFillContains(breakClip, plot.Center));
        Assert.False(StructuralFillContains(breakClip, new Point(plot.Left + plot.Width * .01, plot.Center.Y)));
        Assert.False(StructuralFillContains(breakClip, new Point(plot.Right - plot.Width * .01, plot.Center.Y)));
        Assert.True(DrawnAt(drawing, plot.Center, Ink));
        Assert.True(double.IsFinite(plot.Width) && plot.Width > 100);
    }

    [AvaloniaFact]
    public void Auto_label_stride_recomputes_for_zoomed_ticks_without_moving_the_plot()
    {
        var chart = NewChart();
        chart.XAxis = new CategoryAxis { LabelMode = ChartAxisLabelMode.Auto, LabelMaxWidth = 100, Interval = 1,
            Labels = Enumerable.Range(0, 30).Select(index => $"Business area {index}").ToArray(), PlotMode = CategoryPlotMode.BetweenTicks };
        chart.Series.Add(new ScatterSeries { ItemsSource = Enumerable.Range(0, 30).Select(index => new ChartPoint(index, 5)).ToArray() });
        using var fixture = new AxesFixture(chart, 500);
        var plot = chart.PlotArea; var full = chart.GetAxisLayout(chart.XAxis)!;
        Assert.True(full.Stride > 1);
        chart.Viewport = new ChartViewport(10, 11, 0, 10);
        var zoomed = chart.GetAxisLayout(chart.XAxis)!;
        Assert.Equal(1, zoomed.Stride); Assert.Equal(ChartAxisLabelMode.None, zoomed.Mode);
        Assert.Equal(plot, chart.PlotArea); Assert.Equal(full.Bounds, zoomed.Bounds);
    }

    [AvaloniaFact]
    public void Independent_far_log_axis_and_near_numeric_axis_preserve_raw_hit_values_when_primary_view_changes()
    {
        var chart = NewChart();
        var far = new LogarithmicAxis { Key = "far", Minimum = 1, Maximum = 1000, ViewMinimum = 10, ViewMaximum = 100, Position = ChartAxisPosition.Far, IsInversed = true };
        var near = new NumericAxis { Key = "near", Minimum = 100, Maximum = 200, Position = ChartAxisPosition.Near };
        chart.Axes.Add(far); chart.Axes.Add(near);
        var series = new ScatterSeries { YAxisKey = "far", ItemsSource = new ChartPoint[] { new(5, 10), new(5, Math.Sqrt(1000)) }, MarkerSize = 8 };
        chart.Series.Add(series); chart.Series.Add(new LineSeries { YAxisKey = "near", ItemsSource = new ChartPoint[] { new(0, 180), new(10, 120) } });
        using var fixture = new AxesFixture(chart);
        chart.Viewport = new ChartViewport(2, 8, 2, 8);
        Assert.True(chart.GetAxisLayout(far)!.Bounds.Left >= chart.PlotArea.Right);
        Assert.True(chart.GetAxisLayout(near)!.Bounds.Right <= chart.GetAxisLayout(chart.YAxis)!.Bounds.Left + .001);
        var hit = Assert.IsType<ChartPointHit>(chart.HitTestPoint(chart.PlotArea.Center, tolerance: 1));
        Assert.Same(series, hit.Series); Assert.Equal(Math.Sqrt(1000), hit.Point.Y, 8);
        Assert.Equal(10, far.ViewMinimum); Assert.Equal(100, far.ViewMaximum);
        far.Position = ChartAxisPosition.Near; near.Position = ChartAxisPosition.Far;
        Assert.Equal(Math.Sqrt(1000), Assert.IsType<ChartPointHit>(chart.HitTestPoint(chart.PlotArea.Center, tolerance: 1)).Point.Y, 8);
        chart.ResetViewportCommand.Execute(null);
        Assert.Null(chart.Viewport); Assert.Null(far.ViewMinimum); Assert.Null(far.ViewMaximum);
    }

    [AvaloniaFact]
    public void Wheel_zoom_respects_each_axis_limit_and_pan_keeps_the_limited_span()
    {
        var chart = NewChart(); chart.IsZoomEnabled = true; chart.IsPanEnabled = true;
        chart.Series.Add(new ScatterSeries { ItemsSource = new ChartPoint[] { new(5, 5) } });
        chart.XAxis.MinimumZoom = 2; chart.XAxis.MaximumZoom = 4;
        chart.YAxis.MinimumZoom = 1; chart.YAxis.MaximumZoom = 2;
        using var fixture = new AxesFixture(chart);
        for (var index = 0; index < 20; index++) { fixture.Wheel(5); }
        var zoomed = chart.VisibleRange!.Value;
        Assert.Equal(2.5, zoomed.XMaximum - zoomed.XMinimum, 6);
        Assert.Equal(5, zoomed.YMaximum - zoomed.YMinimum, 6);
        var from = chart.TranslatePoint(chart.PlotArea.Center, fixture.Window)!.Value;
        fixture.Window.MouseDown(from, Avalonia.Input.MouseButton.Left);
        fixture.Window.MouseMove(from + new Vector(30, 20), Avalonia.Input.RawInputModifiers.LeftMouseButton);
        fixture.Window.MouseUp(from + new Vector(30, 20), Avalonia.Input.MouseButton.Left); fixture.Drain();
        var panned = chart.VisibleRange!.Value;
        Assert.Equal(zoomed.XMaximum - zoomed.XMinimum, panned.XMaximum - panned.XMinimum, 6);
        Assert.Equal(zoomed.YMaximum - zoomed.YMinimum, panned.YMaximum - panned.YMinimum, 6);
        for (var index = 0; index < 20; index++) fixture.Wheel(-5);
        var widened = chart.VisibleRange!.Value;
        Assert.Equal(5, widened.XMaximum - widened.XMinimum, 6);
        Assert.Equal(10, widened.YMaximum - widened.YMinimum, 6);
    }

    [AvaloniaFact]
    public void Independent_reversed_log_axis_zoom_uses_its_own_limits_and_disabled_direction_is_preserved()
    {
        var chart = NewChart(); chart.IsZoomEnabled = true; chart.InteractionAxes = ChartInteractionAxes.X;
        var secondary = new LogarithmicAxis
        {
            Key = "quality", Minimum = 1, Maximum = 1000, ViewMinimum = 10, ViewMaximum = 100,
            Position = ChartAxisPosition.Far, IsInversed = true, MinimumZoom = 1.5, MaximumZoom = 10,
        };
        chart.Axes.Add(secondary);
        chart.Series.Add(new ScatterSeries { YAxisKey = "quality", ItemsSource = new ChartPoint[] { new(5, Math.Sqrt(1000)) } });
        using var fixture = new AxesFixture(chart);
        fixture.Wheel(2);
        Assert.Equal(10, secondary.ViewMinimum); Assert.Equal(100, secondary.ViewMaximum);
        chart.InteractionAxes = ChartInteractionAxes.Y;
        for (var index = 0; index < 20; index++) fixture.Wheel(5);
        Assert.Equal(.3, Math.Log10(secondary.ViewMaximum!.Value / secondary.ViewMinimum!.Value), 6);
        for (var index = 0; index < 20; index++) fixture.Wheel(-5);
        Assert.Equal(2, Math.Log10(secondary.ViewMaximum!.Value / secondary.ViewMinimum!.Value), 6);
    }

    [AvaloniaFact]
    public void Compiled_axis_break_band_and_secondary_view_bindings_inherit_update_and_survive_detach()
    {
        var model = new AxesBindingModel();
        var view = new AxesConsumerView { DataContext = model };
        using var fixture = new AxesFixture(view);
        var chart = view.FindControl<CartesianChart>("Chart")!;
        var secondary = chart.Axes.Single(); var band = chart.XAxis.PlotBands.Single(); var broken = chart.XAxis.ScaleBreaks.Single();
        Assert.Equal(model.Title, chart.XAxis.Title); Assert.Equal(model.Title, band.Text);
        Assert.Equal(model.BandStart, band.Start); Assert.Equal(model.BreakStart, broken.Start);
        Assert.Equal(model.ViewMinimum, secondary.ViewMinimum);
        model.Title = "Changed"; model.BandStart = 1.5; model.BreakStart = 5; model.ShowMinor = false; fixture.Drain();
        Assert.Equal("Changed", band.Text); Assert.Equal(1.5, band.Start); Assert.Equal(5, broken.Start); Assert.False(chart.XAxis.ShowMinorGridLines);
        view.Resources["AxisAccent"] = Brushes.Coral; fixture.Drain();
        Assert.True(IsColor(chart.XAxis.LabelBrush, Colors.Coral)); Assert.True(IsColor(band.Foreground, Colors.Coral));
        secondary.SetCurrentValue(ChartAxis.ViewMinimumProperty, .3d); secondary.SetCurrentValue(ChartAxis.ViewMaximumProperty, .7d); fixture.Drain();
        Assert.Equal(.3, model.ViewMinimum); Assert.Equal(.7, model.ViewMaximum);
        fixture.Window.Content = null; model.Title = "Detached update"; model.BandStart = 2; fixture.Window.Content = view; fixture.Drain();
        Assert.Equal("Detached update", band.Text); Assert.Equal(2, band.Start);
        chart.ResetViewportCommand.Execute(null); fixture.Drain();
        Assert.Null(model.ViewMinimum); Assert.Null(model.ViewMaximum);
        var replacement = new AxesBindingModel { Title = "Replacement", BandStart = .25, BreakStart = 4.5 };
        view.DataContext = replacement; fixture.Drain();
        Assert.Equal("Replacement", band.Text); Assert.Equal(.25, band.Start); Assert.Equal(4.5, broken.Start);
        chart.XAxis.PlotBands.Remove(band); chart.XAxis.ScaleBreaks.Remove(broken);
        Assert.Null(band.Parent); Assert.Null(broken.Parent);
    }

    private static CartesianChart NewChart() => new()
    {
        ShowLegend = false,
        XAxis = new NumericAxis { Minimum = 0, Maximum = 10 },
        YAxis = new NumericAxis { Minimum = 0, Maximum = 10 },
    };
    private static DrawingGroup Render(CartesianChart chart) { var group = new DrawingGroup(); using (var context = group.Open()) chart.Render(context); return group; }
    private static IEnumerable<Drawing> Flatten(Drawing drawing)
    {
        yield return drawing;
        if (drawing is DrawingGroup group) foreach (var child in group.Children) foreach (var item in Flatten(child)) yield return item;
    }
    private static bool IsColor(IBrush? brush, Color color) => brush is ISolidColorBrush solid && solid.Color == color;
    private static bool DrawnAt(Drawing drawing, Point point, Color color, bool ignoreClips = false)
    {
        if (drawing is DrawingGroup group)
        {
            if (group.Transform is { } transform) point = transform.Value.Invert().Transform(point);
            if (!ignoreClips && group.ClipGeometry is { } clip && !StructuralFillContains(clip, point)) return false;
            return group.Children.Any(child => DrawnAt(child, point, color, ignoreClips));
        }
        return drawing is GeometryDrawing geometry && geometry.Geometry is { } shape &&
            (IsColor(geometry.Brush, color) && StructuralFillContains(shape, point) || geometry.Pen is { } pen && IsColor(pen.Brush, color) && StructuralStrokeContains(shape, pen, point));
    }
    private static string DrawingProbe(Drawing drawing, Point point, Color color) =>
        $"point={point}; uncutStroke={DrawnAt(drawing, point, color, ignoreClips: true)}; " +
        string.Join("; ", Flatten(drawing).OfType<DrawingGroup>().Where(group => group.ClipGeometry is not null)
            .Select(group => $"clip={group.ClipGeometry!.GetType().Name}, allows={StructuralFillContains(group.ClipGeometry, point)}"));

    // The Headless platform uses dummy geometry services. This oracle reads the public drawing
    // structure instead of calling platform FillContains/StrokeContains, which do not rasterize.
    private static bool StructuralFillContains(Geometry geometry, Point point)
    {
        if (geometry.Transform is { } transform) point = transform.Value.Invert().Transform(point);
        return geometry switch
        {
            RectangleGeometry rectangle when rectangle.RadiusX == 0 && rectangle.RadiusY == 0 => rectangle.Rect.Contains(point),
            CombinedGeometry combined => combined.GeometryCombineMode switch
            {
                GeometryCombineMode.Union => Contains(combined.Geometry1) || Contains(combined.Geometry2),
                GeometryCombineMode.Intersect => Contains(combined.Geometry1) && Contains(combined.Geometry2),
                GeometryCombineMode.Exclude => Contains(combined.Geometry1) && !Contains(combined.Geometry2),
                GeometryCombineMode.Xor => Contains(combined.Geometry1) != Contains(combined.Geometry2),
                _ => throw new InvalidOperationException("Unexpected clip operation."),
            },
            _ => throw new InvalidOperationException($"The structural oracle does not support {geometry.GetType().Name}."),
        };
        bool Contains(Geometry? child) => child is not null && StructuralFillContains(child, point);
    }

    private static bool StructuralStrokeContains(Geometry geometry, IPen pen, Point point)
    {
        if (geometry.Transform is { } transform) point = transform.Value.Invert().Transform(point);
        Point first, last;
        if (geometry is LineGeometry line) { first = line.StartPoint; last = line.EndPoint; }
        else
        {
            // RecordingDrawingContext may wrap the submitted StreamGeometry as PlatformGeometry.
            // Every ink stroke in this fixture is one axis-aligned segment, so its public bounds
            // retain the complete segment independently of the platform wrapper type.
            var bounds = geometry.Bounds;
            if (bounds.Width > .000001 && bounds.Height > .000001)
                throw new InvalidOperationException($"The crossing-line fixture must remain axis aligned, got {bounds}.");
            first = bounds.TopLeft; last = bounds.BottomRight;
        }
        var segment = last - first; var offset = point - first;
        var lengthSquared = segment.X * segment.X + segment.Y * segment.Y;
        if (lengthSquared <= 0) return false;
        var t = (offset.X * segment.X + offset.Y * segment.Y) / lengthSquared;
        if (t < 0 || t > 1) return false;
        var nearest = first + segment * t; var delta = point - nearest;
        return delta.X * delta.X + delta.Y * delta.Y <= pen.Thickness * pen.Thickness / 4;
    }

    private static IEnumerable<(Drawing Drawing, Rect Bounds)> DrawingBounds(Drawing drawing, Matrix toRoot)
    {
        if (drawing is DrawingGroup group)
        {
            var combined = (group.Transform?.Value ?? Matrix.Identity) * toRoot;
            foreach (var child in group.Children) foreach (var item in DrawingBounds(child, combined)) yield return item;
        }
        else yield return (drawing, drawing.GetBounds().TransformToAABB(toRoot));
    }
    private static IEnumerable<(Drawing Drawing, double Opacity)> DrawingOpacity(Drawing drawing, double opacity)
    {
        if (drawing is DrawingGroup group)
        {
            foreach (var child in group.Children) foreach (var item in DrawingOpacity(child, opacity * group.Opacity)) yield return item;
        }
        else yield return (drawing, opacity);
    }
    private sealed class AxesFixture : IDisposable
    {
        public AxesFixture(Control content, double width = 900) { Window = new Window { Width = width, Height = 600, Content = content }; Window.Show(); Drain(); }
        public Window Window { get; }
        public void Drain() { Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); }
        public void Wheel(double delta) { var chart = Assert.IsType<CartesianChart>(Window.Content); Window.MouseWheel(chart.TranslatePoint(chart.PlotArea.Center, Window)!.Value, new Vector(0, delta)); Drain(); }
        public void Dispose() => Window.Close();
    }
}

public sealed partial class AxesConsumerView : UserControl
{
    public AxesConsumerView() => AvaloniaXamlLoader.Load(this);
}

public sealed class AxesBindingModel : INotifyPropertyChanged
{
    private string _title = "Bound axis";
    private double _bandStart = 1;
    private double _breakStart = 4;
    private bool _showMinor = true;
    private double? _viewMinimum = .2;
    private double? _viewMaximum = .8;
    public string Title { get => _title; set { _title = value; Notify(); } }
    public double BandStart { get => _bandStart; set { _bandStart = value; Notify(); } }
    public double BreakStart { get => _breakStart; set { _breakStart = value; Notify(); } }
    public bool ShowMinor { get => _showMinor; set { _showMinor = value; Notify(); } }
    public double? ViewMinimum { get => _viewMinimum; set { _viewMinimum = value; Notify(); } }
    public double? ViewMaximum { get => _viewMaximum; set { _viewMaximum = value; Notify(); } }
    public ChartPoint[] Points { get; } = [new(2, .5), new(8, .6)];
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
