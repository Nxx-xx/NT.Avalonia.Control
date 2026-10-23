using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class AnnotationAxesCompletionTests
{
    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Annotation_only_axis_uses_own_domain_and_follows_normalized_zoom(bool inverse)
    {
        var chart = Create();
        var axis = new NumericAxis { Key = "pressure", Minimum = 1000, Maximum = 2000, IsInversed = inverse, Position = ChartAxisPosition.Far };
        chart.Axes.Add(axis);
        Point actual = default;
        chart.Annotations.Add(new CustomAnnotation { YAxisKey = "pressure", Renderer = (_, context) => actual = context.Project(50, 1400) });
        using var host = new TemplateFeaturesHost(chart);
        Record(chart);
        Assert.NotNull(chart.GetAxisLayout(axis));
        Near(chart.PlotArea.Bottom - chart.PlotArea.Height * (inverse ? .6 : .4), actual.Y);
        chart.Viewport = new(20, 80, 20, 80); host.Drain(); Record(chart);
        Near(chart.PlotArea.Center.X, actual.X);
        Near(chart.PlotArea.Bottom - chart.PlotArea.Height * (inverse ? 2d / 3 : 1d / 3), actual.Y);
        axis.ViewMinimum = 1300; axis.ViewMaximum = 1500; host.Drain(); Record(chart);
        Near(chart.PlotArea.Center.Y, actual.Y);
    }

    [AvaloniaFact]
    public void Empty_chart_renders_annotation_only_log_axis_and_its_reference_band()
    {
        var chart = Create(); chart.Series.Clear();
        var axis = new LogarithmicAxis { Key = "log", Minimum = 1, Maximum = 10_000, Position = ChartAxisPosition.Far };
        axis.PlotBands.Add(new ChartPlotBand { Start = 10, End = 100, Fill = Brushes.Magenta });
        chart.Axes.Add(axis);
        Point actual = default;
        chart.Annotations.Add(new CustomAnnotation { YAxisKey = "log", Renderer = (_, context) => actual = context.Project(50, 100) });
        using var host = new TemplateFeaturesHost(chart);
        var drawing = Record(chart);
        Near(chart.PlotArea.Center.Y, actual.Y);
        var band = Assert.Single(TemplateFeaturesHost.Walk(drawing).OfType<GeometryDrawing>(), item => item.Brush == Brushes.Magenta);
        Near(chart.PlotArea.Height / 4, band.Geometry!.Bounds.Height);
        chart.Viewport = new(25, 75, 25, 75); host.Drain(); Record(chart);
        Near(chart.PlotArea.Center.Y, actual.Y);
    }

    [AvaloniaTheory]
    [InlineData("unknown")] [InlineData("duplicate")] [InlineData("wrong")] [InlineData("ambiguous")]
    public void Invalid_named_axes_do_not_fall_back_or_activate_a_dimension(string kind)
    {
        var chart = Create(); var axis = new NumericAxis { Key = "extra", Minimum = 0, Maximum = 1000 }; chart.Axes.Add(axis);
        var count = 0;
        if (kind == "duplicate") chart.Axes.Add(new NumericAxis { Key = "extra" });
        if (kind == "wrong") chart.Series.Add(new ScatterSeries { YAxisKey = "extra", ItemsSource = new[] { new ChartPoint(50, 500) } });
        chart.Annotations.Add(new CustomAnnotation { XAxisKey = kind == "unknown" ? "missing" : "extra", Renderer = (_, _) => count++ });
        if (kind == "ambiguous") chart.Annotations.Add(new CustomAnnotation { YAxisKey = "extra", Renderer = (_, _) => count++ });
        using var host = new TemplateFeaturesHost(chart); Record(chart);
        Assert.Equal(0, count);
        if (kind != "wrong") Assert.Null(chart.GetAxisLayout(axis));
    }

    [AvaloniaFact]
    public void Pixel_visibility_and_source_replacement_recompute_annotation_axis_membership()
    {
        var chart = Create(); var axis = new NumericAxis { Key = "extra", Minimum = 0, Maximum = 1000 }; chart.Axes.Add(axis);
        var annotation = new TextAnnotation { YAxisKey = "extra", X = 5, Y = 5, Text = "fixed", CoordinateUnit = ChartAnnotationCoordinateUnit.Pixel };
        chart.Annotations.Add(annotation);
        using var host = new TemplateFeaturesHost(chart); Record(chart);
        Assert.Null(chart.GetAxisLayout(axis));
        annotation.CoordinateUnit = ChartAnnotationCoordinateUnit.Data; host.Drain(); Record(chart);
        Assert.NotNull(chart.GetAxisLayout(axis));
        annotation.IsVisible = false; host.Drain(); Record(chart); Assert.Null(chart.GetAxisLayout(axis));
        annotation.IsVisible = true; chart.AnnotationsSource = new ObservableCollection<ChartAnnotation>(); host.Drain(); Record(chart);
        Assert.Null(chart.GetAxisLayout(axis));
        chart.AnnotationsSource = null; host.Drain(); Record(chart); Assert.NotNull(chart.GetAxisLayout(axis));
        annotation.YAxisKey = "missing"; host.Drain(); Record(chart); Assert.Null(chart.GetAxisLayout(axis));
    }

    [AvaloniaFact]
    public void Hidden_series_preserves_axis_dimension_and_cannot_be_reinterpreted_by_annotation()
    {
        var chart = Create(); var axis = new NumericAxis { Key = "extra", Minimum = 0, Maximum = 1000 }; chart.Axes.Add(axis);
        chart.Series.Add(new ScatterSeries { IsVisible = false, YAxisKey = "extra", ItemsSource = new[] { new ChartPoint(50, 500) } });
        var count = 0; chart.Annotations.Add(new CustomAnnotation { XAxisKey = "extra", Renderer = (_, _) => count++ });
        using var host = new TemplateFeaturesHost(chart); Record(chart);
        Assert.Equal(0, count); Assert.Null(chart.GetAxisLayout(axis));
    }

    private static CartesianChart Create()
    {
        var chart = new CartesianChart { ShowLegend = false, XAxis = new NumericAxis { Minimum = 0, Maximum = 100 }, YAxis = new NumericAxis { Minimum = 0, Maximum = 100 } };
        chart.Series.Add(new ScatterSeries { ItemsSource = new[] { new ChartPoint(50, 50) } }); return chart;
    }
    private static DrawingGroup Record(CartesianChart chart) => TemplateFeaturesHost.Record(chart);
    private static void Near(double expected, double actual) => Assert.Equal(expected, actual, 7);
}
