using System.Collections;
using System.Globalization;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class InteractionTextCacheTests
{
    private static readonly MethodInfo Readings = typeof(CartesianChart).GetMethod("UpdateReadings", BindingFlags.Instance | BindingFlags.NonPublic)!;

    [AvaloniaFact]
    public void Repeated_hit_and_anchor_movement_reuse_tooltip_text_and_keep_current_position()
    {
        using var host = new Host(); var chart = host.Chart;
        host.Hover(); var first = Draw(chart);
        var before = CachedTexts(chart); var builds = Counter(chart, "_interactionTextBuildCount");
        Assert.NotEmpty(before);
        var firstOrigin = TooltipGlyphBounds(first, host.Brush, Matrix.Identity).First().TopLeft;
        host.Hover(new Vector(.25, .5)); var moved = Draw(chart);
        var movedOrigin = TooltipGlyphBounds(moved, host.Brush, Matrix.Identity).First().TopLeft;
        Assert.Equal(builds, Counter(chart, "_interactionTextBuildCount"));
        Assert.True(Counter(chart, "_interactionTextReuseCount") > 0);
        Assert.All(before, text => Assert.Contains(CachedTexts(chart), current => ReferenceEquals(text, current)));
        Assert.Equal(firstOrigin.X + .25, movedOrigin.X, 8); Assert.Equal(firstOrigin.Y + .5, movedOrigin.Y, 8);
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public void Retained_tooltip_glyphs_keep_live_mutable_brushes_and_match_a_fresh_text_layout(bool gradient)
    {
        Brush brush = gradient ? new LinearGradientBrush
        {
            GradientStops = [new GradientStop(Colors.Red, 0), new GradientStop(Colors.Blue, 1)],
        } : new SolidColorBrush(Colors.Red);
        using var host = new Host(brush); var chart = host.Chart;
        host.Hover(); Draw(chart);
        var before = CachedTexts(chart); var builds = Counter(chart, "_interactionTextBuildCount");
        brush.Opacity = .35; brush.Transform = new MatrixTransform(Matrix.CreateTranslation(2.25, -3.5));
        if (brush is SolidColorBrush solid) solid.Color = Colors.Green;
        if (brush is LinearGradientBrush linear)
        { linear.GradientStops[0].Color = Colors.Green; linear.StartPoint = new RelativePoint(.25, .5, RelativeUnit.Relative); }
        var retained = Draw(chart);
        Assert.Equal(builds, Counter(chart, "_interactionTextBuildCount"));
        Assert.All(before, text => Assert.Contains(CachedTexts(chart), current => ReferenceEquals(text, current)));
        var retainedGlyphs = TooltipGlyphs(retained, brush).ToArray(); Assert.NotEmpty(retainedGlyphs);
        Assert.All(retainedGlyphs, glyph => Assert.Same(brush, glyph.Foreground));
        Assert.All(retainedGlyphs, glyph => Assert.Equal(.35, glyph.Foreground!.Opacity));
        Release(chart);
        var freshGlyphs = TooltipGlyphs(Draw(chart), brush).ToArray();
        Assert.Equal(retainedGlyphs.Length, freshGlyphs.Length);
        foreach (var (old, fresh) in retainedGlyphs.Zip(freshGlyphs))
        {
            Assert.Equal(old.GlyphRun!.BaselineOrigin, fresh.GlyphRun!.BaselineOrigin);
            Assert.Equal(old.GlyphRun.GlyphInfos, fresh.GlyphRun.GlyphInfos);
            Assert.Equal(old.GetBounds(), fresh.GetBounds());
        }
    }

    [AvaloniaFact]
    public void Formatter_remains_live_and_text_and_drawing_caches_remain_bounded()
    {
        using var host = new Host(); var chart = host.Chart;
        var calls = 0; chart.TooltipFormatter = _ => "Current formatter " + ++calls;
        host.Hover(); Draw(chart); var before = calls;
        Draw(chart); Draw(chart);
        Assert.Equal(before + 2, calls);
        for (var index = 0; index < 80; index++) Draw(chart);
        Assert.Equal(32, Cache(chart, "_interactionTexts").Count);
        Assert.Equal(32, Cache(chart, "_interactionTextDrawings").Count);
        Assert.Contains(Cache(chart, "_interactionTexts").Keys.Cast<object>(),
            key => Equals(key.GetType().GetProperty("Text")!.GetValue(key), "Current formatter " + calls));
    }

    [AvaloniaFact]
    public void Font_culture_and_brush_replacement_receive_current_layout_and_style()
    {
        using var host = new Host(); var chart = host.Chart;
        chart.TooltipFormatter = _ => "1234 ثابت";
        host.Hover(); Draw(chart); var before = CachedTexts(chart);
        chart.FontSize = 23; chart.FontStyle = FontStyle.Italic;
        var green = new SolidColorBrush(Colors.Green); chart.TooltipForeground = green;
        host.Hover(); var updated = Draw(chart);
        Assert.DoesNotContain(CachedTexts(chart), text => before.Any(old => ReferenceEquals(old, text)));
        Assert.NotEmpty(TooltipGlyphs(updated, green));
        var builds = Counter(chart, "_interactionTextBuildCount");
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = previousCulture.Name == "ar-SA" ? new("en-US") : new("ar-SA");
            Draw(chart);
            Assert.True(Counter(chart, "_interactionTextBuildCount") > builds);
        }
        finally { CultureInfo.CurrentCulture = previousCulture; }
    }

    [AvaloniaFact]
    public void View_invalidation_and_detach_release_bounded_interaction_caches()
    {
        using var host = new Host(); var chart = host.Chart;
        host.Hover(); Draw(chart); Assert.NotEmpty(CachedTexts(chart));
        chart.Viewport = new(1, 9, 10, 90);
        Assert.Empty(CachedTexts(chart)); Assert.Empty(Cache(chart, "_interactionTextDrawings"));
        host.Hover(); Draw(chart); Assert.NotEmpty(CachedTexts(chart));
        host.Window.Content = null;
        Assert.Empty(CachedTexts(chart)); Assert.Empty(Cache(chart, "_interactionTextDrawings"));
    }

    private static IDictionary Cache(CartesianChart chart, string name) => (IDictionary)typeof(CartesianChart).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(chart)!;
    private static FormattedText[] CachedTexts(CartesianChart chart) => Cache(chart, "_interactionTexts").Values.Cast<FormattedText>().ToArray();
    private static long Counter(CartesianChart chart, string name) => (long)typeof(CartesianChart).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(chart)!;
    private static void Release(CartesianChart chart) => typeof(CartesianChart).GetMethod("ReleaseInteractionCaches", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(chart, null);
    private static DrawingGroup Draw(CartesianChart chart)
    {
        var group = new DrawingGroup(); using (var drawing = group.Open()) chart.Render(drawing); return group;
    }
    private static IEnumerable<GlyphRunDrawing> TooltipGlyphs(Drawing drawing, IBrush brush)
    {
        if (drawing is GlyphRunDrawing glyph && ReferenceEquals(glyph.Foreground, brush)) yield return glyph;
        if (drawing is DrawingGroup group)
            foreach (var child in group.Children)
                foreach (var nested in TooltipGlyphs(child, brush)) yield return nested;
    }
    private static IEnumerable<Rect> TooltipGlyphBounds(Drawing drawing, IBrush brush, Matrix transform)
    {
        if (drawing is GlyphRunDrawing glyph && ReferenceEquals(glyph.Foreground, brush))
            yield return glyph.GetBounds().TransformToAABB(transform);
        if (drawing is DrawingGroup group)
        {
            var combined = (group.Transform?.Value ?? Matrix.Identity) * transform;
            foreach (var child in group.Children)
                foreach (var bounds in TooltipGlyphBounds(child, brush, combined)) yield return bounds;
        }
    }
    private sealed class Host : IDisposable
    {
        public CartesianChart Chart { get; }
        public Window Window { get; }
        public IBrush Brush { get; }
        public Host(IBrush? brush = null)
        {
            Brush = brush ?? new SolidColorBrush(Colors.Red);
            Chart = new CartesianChart
            {
                Width = 720, Height = 400, ShowLegend = false, TooltipForeground = Brush,
                XAxis = new NumericAxis { Minimum = 0, Maximum = 10 }, YAxis = new NumericAxis { Minimum = 0, Maximum = 100 },
            };
            Chart.Series.Add(new ScatterSeries { Title = "Series", ItemsSource = new ChartPoint[] { new(5, 50) } });
            Window = new Window { Width = 720, Height = 400, Content = Chart };
            Window.Show(); Window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
        }
        public void Hover(Vector delta = default)
        {
            var plot = Chart.PlotArea; var range = Chart.VisibleRange!.Value;
            var point = new Point(plot.Left + (5 - range.XMinimum) / (range.XMaximum - range.XMinimum) * plot.Width,
                plot.Bottom - (50 - range.YMinimum) / (range.YMaximum - range.YMinimum) * plot.Height) + delta;
            Readings.Invoke(Chart, [point]);
        }
        public void Dispose() => Window.Close();
    }
}
