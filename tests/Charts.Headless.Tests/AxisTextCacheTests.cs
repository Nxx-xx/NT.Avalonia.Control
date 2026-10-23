using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class AxisTextCacheTests
{
    [AvaloniaTheory]
    [InlineData(0d, false)]
    [InlineData(37d, false)]
    [InlineData(0d, true)]
    [InlineData(-23d, true)]
    public void Retained_label_drawings_match_original_glyphs_transforms_clips_and_alpha(double rotation, bool wrap)
    {
        var chart = MakeChart(720.375);
        chart.Height = 360.625; chart.Padding = new Thickness(9.125, 8.375, 7.625, 6.875);
        chart.XAxis.LabelFontSize = 13.25;
        chart.XAxis.LabelBrush = new ImmutableSolidColorBrush(Color.FromArgb(109, 33, 90, 170));
        chart.XAxis.LabelMode = wrap ? ChartAxisLabelMode.Wrap : ChartAxisLabelMode.Rotate;
        chart.XAxis.LabelWrap = wrap; chart.XAxis.LabelRotation = rotation; chart.XAxis.LabelMaxWidth = 53.75;
        using var host = new Host(chart);
        Render(chart);
        var cached = GlyphEvidence(Render(chart, .625), Matrix.Identity, 1, []).ToArray();
        var cache = (System.Collections.IDictionary)typeof(CartesianChart)
            .GetField("_axisTextDrawings", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(chart)!;
        Assert.NotEmpty(cache);
        // Keep the same FormattedText/metrics, disabling only retained drawing reuse.
        cache.Clear();
        var original = GlyphEvidence(Render(chart, .625), Matrix.Identity, 1, []).ToArray();

        AssertGlyphsEqual(original, cached);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Retained_glyphs_read_current_mutable_brush_color_opacity_and_gradient_coordinates(bool gradient)
    {
        Brush brush = gradient
            ? new LinearGradientBrush
            {
                StartPoint = new RelativePoint(.15, .25, RelativeUnit.Relative),
                EndPoint = new RelativePoint(.85, .9, RelativeUnit.Relative),
                SpreadMethod = GradientSpreadMethod.Reflect,
                GradientStops = [new GradientStop(Colors.Red, .1), new GradientStop(Colors.Blue, .9)],
            }
            : new SolidColorBrush(Colors.Red);
        var transform = new MatrixTransform(Matrix.CreateScale(.85, 1.1) * Matrix.CreateTranslation(1.125, -2.375));
        brush.Opacity = .73; brush.Transform = transform;
        brush.TransformOrigin = new RelativePoint(.2, .8, RelativeUnit.Relative);
        var chart = MakeChart(720.375);
        chart.XAxis.LabelBrush = brush; chart.XAxis.LabelFontSize = 13.25;
        chart.XAxis.LabelMode = ChartAxisLabelMode.Rotate; chart.XAxis.LabelRotation = -23;
        using var host = new Host(chart);
        Render(chart); Render(chart);
        var cachedTexts = CachedText(chart);

        // Mutate the same brush and transform objects, without replacing the style.
        brush.Opacity = .39;
        transform.Matrix = Matrix.CreateRotation(.17) * Matrix.CreateTranslation(-3.625, 2.875);
        brush.TransformOrigin = new RelativePoint(3.25, -1.75, RelativeUnit.Absolute);
        if (brush is SolidColorBrush solid) solid.Color = Color.FromArgb(151, 27, 189, 73);
        if (brush is LinearGradientBrush linear)
        {
            linear.StartPoint = new RelativePoint(2.125, 3.75, RelativeUnit.Absolute);
            linear.EndPoint = new RelativePoint(31.875, 18.25, RelativeUnit.Absolute);
            linear.SpreadMethod = GradientSpreadMethod.Repeat;
            linear.GradientStops[0].Color = Colors.Purple; linear.GradientStops[0].Offset = .2;
            linear.GradientStops.Add(new GradientStop(Color.FromArgb(87, 40, 190, 80), .55));
        }
        chart.Series[0].ItemsSource = new ChartPoint[] { new(0, 2), new(7, 8) };
        var retained = Render(chart, .625);
        Assert.Contains(Leaves(retained).OfType<GlyphRunDrawing>(), glyph => ReferenceEquals(glyph.Foreground, brush));
        Assert.All(cachedTexts, text => Assert.Contains(CachedText(chart), current => ReferenceEquals(text, current)));
        var actual = GlyphEvidence(retained, Matrix.Identity, 1, []).ToArray();

        var cache = (System.Collections.IDictionary)typeof(CartesianChart)
            .GetField("_axisTextDrawings", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(chart)!;
        Assert.NotEmpty(cache);
        cache.Clear();
        var original = GlyphEvidence(Render(chart, .625), Matrix.Identity, 1, []).ToArray();
        AssertGlyphsEqual(original, actual);
        Assert.Contains(actual, glyph => glyph.Brush is { Opacity: .39 });
    }

    [AvaloniaFact]
    public void Mutable_brush_keeps_its_identity_and_current_color_across_data_refresh()
    {
        var chart = MakeChart(720);
        var brush = new SolidColorBrush(Colors.Red);
        chart.XAxis.LabelBrush = brush;
        using var host = new Host(chart);
        Render(chart);
        var before = CachedText(chart);

        brush.Color = Colors.Green;
        chart.Series[0].ItemsSource = new ChartPoint[] { new(0, 2), new(7, 8) };
        var drawing = Render(chart);

        Assert.True(HasGlyphColor(drawing, Colors.Green));
        Assert.False(HasGlyphColor(drawing, Colors.Red));
        Assert.All(before, text => Assert.Contains(CachedText(chart), current => ReferenceEquals(text, current)));
    }

    [AvaloniaFact]
    public void Stable_axis_text_survives_data_revisions_but_style_changes_replace_it()
    {
        var chart = MakeChart(720);
        chart.XAxis.LabelBrush = Brushes.Red;
        using var host = new Host(chart);
        Render(chart);
        var before = CachedText(chart);
        Assert.NotEmpty(before);

        chart.Series[0].ItemsSource = new ChartPoint[] { new(0, 2), new(7, 8) };
        Render(chart);
        var afterData = CachedText(chart);
        Assert.All(before, text => Assert.Contains(afterData, current => ReferenceEquals(text, current)));

        chart.XAxis.LabelBrush = Brushes.Green;
        chart.XAxis.LabelFontSize = 24;
        var updated = Render(chart);
        Assert.True(HasGlyphColor(updated, Colors.Green));
        Assert.False(HasGlyphColor(updated, Colors.Red));
        Assert.DoesNotContain(CachedText(chart), text => before.Any(previous => ReferenceEquals(text, previous)));
    }

    [AvaloniaFact]
    public void Changing_automatic_ticks_retains_a_bounded_cache_and_axis_removal_releases_its_entries()
    {
        var chart = MakeChart(720);
        chart.YAxis.Minimum = null; chart.YAxis.Maximum = null;
        using var host = new Host(chart);
        for (var index = 0; index < 80; index++)
        {
            chart.Series[0].ItemsSource = new ChartPoint[] { new(0, index * 10), new(7, index * 10 + 1) };
            _ = chart.PlotArea;
        }
        Assert.Equal(256, CachedText(chart).Length);
        chart.YAxis = new NumericAxis { Minimum = 0, Maximum = 10 };
        Assert.InRange(CachedText(chart).Length, 0, 80);
    }

    [AvaloniaFact]
    public void Detached_arranged_chart_reads_new_axis_style_on_every_render()
    {
        var chart = MakeChart(720);
        chart.XAxis.LabelBrush = Brushes.Red;
        chart.XAxis.LabelFontSize = 12;
        chart.Measure(new Size(720, 360)); chart.Arrange(new Rect(0, 0, 720, 360));
        var first = Leaves(Render(chart)).OfType<GlyphRunDrawing>()
            .Where(glyph => glyph.Foreground is ISolidColorBrush brush && brush.Color == Colors.Red).ToArray();
        Assert.NotEmpty(first);
        chart.XAxis.LabelBrush = Brushes.Green;
        chart.XAxis.LabelFontSize = 24;
        var updated = Render(chart);
        var green = Leaves(updated).OfType<GlyphRunDrawing>()
            .Where(glyph => glyph.Foreground is ISolidColorBrush brush && brush.Color == Colors.Green).ToArray();
        Assert.NotEmpty(green);
        Assert.False(HasGlyphColor(updated, Colors.Red));
        Assert.True(green.Max(glyph => glyph.GetBounds().Height) > first.Max(glyph => glyph.GetBounds().Height));
    }

    [AvaloniaFact]
    public void Wrapped_labels_keep_fresh_layout_and_brushes_after_redraw_binding_and_resize()
    {
        var chart = MakeChart(720);
        chart.XAxis.Bind(ChartAxis.LabelFontSizeProperty, new Binding(nameof(FontModel.Size)));
        chart.XAxis.Bind(ChartAxis.LabelBrushProperty, new Binding(nameof(FontModel.Brush)));
        chart.DataContext = new FontModel(12, Brushes.Red);
        using var host = new Host(chart);
        var originalPlot = chart.PlotArea;
        var originalLayout = chart.GetAxisLayout(chart.XAxis);
        Assert.True(HasGlyphColor(Render(chart), Colors.Red));
        Render(chart);
        Assert.Equal(originalPlot, chart.PlotArea);
        Assert.Equal(originalLayout, chart.GetAxisLayout(chart.XAxis));

        chart.DataContext = new FontModel(24, Brushes.Green);
        chart.Width = 460; host.Window.Width = 460;
        host.Window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
        var updated = Render(chart);
        Assert.True(HasGlyphColor(updated, Colors.Green));
        Assert.False(HasGlyphColor(updated, Colors.Red));
        var fresh = MakeChart(460);
        fresh.XAxis.LabelFontSize = 24; fresh.XAxis.LabelBrush = Brushes.Green;
        using var freshHost = new Host(fresh);
        Render(fresh);
        Assert.Equal(fresh.PlotArea, chart.PlotArea);
        Assert.Equal(fresh.GetAxisLayout(fresh.XAxis), chart.GetAxisLayout(chart.XAxis));
    }

    [AvaloniaFact]
    public void Wrapped_draw_does_not_constrain_later_unwrapped_measurement()
    {
        var chart = MakeChart(460);
        using var host = new Host(chart);
        Render(chart); Render(chart);
        chart.XAxis.LabelMode = ChartAxisLabelMode.None;
        chart.XAxis.LabelWrap = false;
        chart.XAxis.LabelMaxWidth = 320;
        var afterWrap = Render(chart);
        var fresh = MakeChart(460);
        fresh.XAxis.LabelMode = ChartAxisLabelMode.None;
        fresh.XAxis.LabelWrap = false;
        fresh.XAxis.LabelMaxWidth = 320;
        using var freshHost = new Host(fresh);
        var freshDrawing = Render(fresh);
        Assert.Equal(fresh.PlotArea, chart.PlotArea);
        Assert.Equal(fresh.GetAxisLayout(fresh.XAxis), chart.GetAxisLayout(chart.XAxis));
        Assert.Equal(Leaves(freshDrawing).OfType<GlyphRunDrawing>().Count(), Leaves(afterWrap).OfType<GlyphRunDrawing>().Count());
        // The ordinary benchmark host has no automation client. Confirm the framework
        // itself did not eagerly create the peer during show/layout/render.
        var field = typeof(CartesianChart).GetField("_chartAutomationPeer", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Null(field.GetValue(chart));
    }

    private static CartesianChart MakeChart(double width)
    {
        var chart = new CartesianChart
        {
            Width = width, Height = 360, ShowLegend = false,
            XAxis = new CategoryAxis
            {
                Labels = Enumerable.Range(0, 8).Select(index => $"Label {index} has several long words").ToArray(),
                LabelMode = ChartAxisLabelMode.Wrap, LabelWrap = true, LabelMaxWidth = 80,
            },
            YAxis = new NumericAxis { Minimum = 0, Maximum = 10 },
        };
        chart.Series.Add(new LineSeries { MarkerSize = 0, ItemsSource = new ChartPoint[] { new(0, 1), new(7, 9) } });
        return chart;
    }
    private static DrawingGroup Render(CartesianChart chart, double opacity = 1)
    {
        var drawing = new DrawingGroup();
        using (var context = drawing.Open())
        {
            using var alpha = context.PushOpacity(opacity);
            chart.Render(context);
        }
        return drawing;
    }
    private sealed record GradientStopRecord(double Offset, Color Color);
    private sealed record BrushRecord(double Opacity, Matrix Transform, RelativePoint Origin, Color? Color,
        RelativePoint? Start, RelativePoint? End, GradientSpreadMethod? Spread);
    private sealed record GlyphRecord(Matrix Transform, Rect Bounds, Point Origin, double Opacity, BrushRecord? Brush,
        GradientStopRecord[] Stops, Rect[] Clips, GlyphInfo[] Glyphs);
    private static void AssertGlyphsEqual(GlyphRecord[] expectedRecords, GlyphRecord[] actualRecords)
    {
        Assert.NotEmpty(actualRecords);
        Assert.Equal(expectedRecords.Length, actualRecords.Length);
        foreach (var (expected, actual) in expectedRecords.Zip(actualRecords))
        {
            Assert.Equal(expected.Transform, actual.Transform); Assert.Equal(expected.Bounds, actual.Bounds);
            Assert.Equal(expected.Origin, actual.Origin); Assert.Equal(expected.Opacity, actual.Opacity);
            Assert.Equal(expected.Brush, actual.Brush); Assert.Equal(expected.Stops, actual.Stops);
            Assert.Equal(expected.Clips, actual.Clips); Assert.Equal(expected.Glyphs, actual.Glyphs);
        }
    }
    private static IEnumerable<GlyphRecord> GlyphEvidence(Drawing drawing, Matrix toRoot, double opacity, Rect[] clips)
    {
        if (drawing is DrawingGroup group)
        {
            var combined = (group.Transform?.Value ?? Matrix.Identity) * toRoot;
            var currentClips = group.ClipGeometry is { } clip ? clips.Append(clip.Bounds.TransformToAABB(combined)).ToArray() : clips;
            foreach (var child in group.Children)
                foreach (var record in GlyphEvidence(child, combined, opacity * group.Opacity, currentClips)) yield return record;
        }
        else if (drawing is GlyphRunDrawing glyph)
        {
            var run = Assert.IsType<GlyphRun>(glyph.GlyphRun);
            var brush = glyph.Foreground;
            var gradient = brush as ILinearGradientBrush;
            yield return new(toRoot, glyph.GetBounds().TransformToAABB(toRoot), run.BaselineOrigin, opacity,
                brush is null ? null : new(brush.Opacity, brush.Transform?.Value ?? Matrix.Identity, brush.TransformOrigin,
                    (brush as ISolidColorBrush)?.Color, gradient?.StartPoint, gradient?.EndPoint, (brush as IGradientBrush)?.SpreadMethod),
                (brush as IGradientBrush)?.GradientStops.Select(stop => new GradientStopRecord(stop.Offset, stop.Color)).ToArray() ?? [],
                clips, run.GlyphInfos.ToArray());
        }
    }
    private static bool HasGlyphColor(Drawing drawing, Color color) => Leaves(drawing).OfType<GlyphRunDrawing>()
        .Any(glyph => glyph.Foreground is ISolidColorBrush brush && brush.Color == color);
    private static FormattedText[] CachedText(CartesianChart chart)
    {
        var cache = (System.Collections.IDictionary)typeof(CartesianChart)
            .GetField("_axisTexts", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(chart)!;
        return cache.Values.Cast<FormattedText>().ToArray();
    }
    private static IEnumerable<Drawing> Leaves(Drawing drawing)
    {
        if (drawing is DrawingGroup group)
            foreach (var child in group.Children)
                foreach (var leaf in Leaves(child)) yield return leaf;
        else yield return drawing;
    }
    private sealed record FontModel(double Size, IBrush Brush);
    private sealed class Host : IDisposable
    {
        public Window Window { get; }
        public Host(CartesianChart chart)
        {
            Window = new Window { Width = chart.Width, Height = chart.Height, Content = chart };
            Window.Show(); Window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
        }
        public void Dispose() => Window.Close();
    }
}
