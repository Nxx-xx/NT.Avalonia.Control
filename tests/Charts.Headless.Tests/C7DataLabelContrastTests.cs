using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using System.Runtime.CompilerServices;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class C7DataLabelContrastTests
{
    [AvaloniaTheory]
    [InlineData(0, ChartHighContrastMode.Light)] [InlineData(0, ChartHighContrastMode.Dark)]
    [InlineData(1, ChartHighContrastMode.Light)] [InlineData(1, ChartHighContrastMode.Dark)]
    [InlineData(2, ChartHighContrastMode.Light)] [InlineData(2, ChartHighContrastMode.Dark)]
    [InlineData(3, ChartHighContrastMode.Light)] [InlineData(3, ChartHighContrastMode.Dark)]
    public void Inside_labels_use_the_actual_six_color_palette_and_outside_text_keeps_local_foreground(int family, ChartHighContrastMode mode)
    {
        var rows = Enumerable.Range(0, 6).Select(index => new LabelRow($"id{index}", $"Inside{index}", 6 - index, $"Inside{index}", "OutsideColumn")).ToArray();
        ChartBase chart = family switch
        {
            0 => new FunnelChart { ItemsSource = rows, CategoryPath = nameof(LabelRow.Label), ValuePath = nameof(LabelRow.Value), IdPath = nameof(LabelRow.Id), ShowValues = false, Title = "OutsideTitle" },
            1 => new PyramidChart { ItemsSource = rows, CategoryPath = nameof(LabelRow.Label), ValuePath = nameof(LabelRow.Value), IdPath = nameof(LabelRow.Id), ShowValues = false, Title = "OutsideTitle" },
            // Equal sectors guarantee every palette color has room for its full label.
            2 => Circular(rows.Select(row => row with { Value = 1 }).ToArray()),
            _ => new MosaicChart { ItemsSource = rows, Title = "OutsideTitle" },
        };
        chart.HighContrastMode = mode; chart.Foreground = Brushes.Red;
        using var host = new Host(chart);
        var glyphs = Glyphs(Record(chart)).ToArray();
        Assert.Contains(glyphs, glyph => Characters(glyph).Contains("OutsideTitle", StringComparison.Ordinal) && IsColor(glyph.Foreground, Colors.Red));
        for (var index = 0; index < rows.Length; index++)
        {
            var prefix = $"Inside{index}"; var actualLabels = glyphs.Where(glyph => Characters(glyph).StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            Assert.NotEmpty(actualLabels);
            var fill = chart switch
            {
                AnalysisChartBase analysis => analysis.GetMarks().Single(mark => mark.Data.SourceIndex == index).Fill,
                CircularChart circular => circular.GetSlices().Single(slice => slice.Data.SourceIndex == index).Fill,
                EngineeringChartBase engineering => engineering.GetMarks().Single(mark => mark.Members[0].SourceIndex == index).Fill,
                _ => throw new InvalidOperationException(),
            };
            var background = Assert.IsAssignableFrom<ISolidColorBrush>(fill).Color;
            Assert.All(actualLabels, glyph =>
            {
                var foreground = Assert.IsAssignableFrom<ISolidColorBrush>(glyph.Foreground).Color;
                Assert.True(Contrast(foreground, background) >= 4.5, $"{chart.GetType().Name}: {prefix} {foreground} on {background}");
                Assert.Equal(mode == ChartHighContrastMode.Dark ? Colors.Black : Colors.White, foreground);
            });
        }
        if (chart is MosaicChart)
            Assert.Contains(glyphs, glyph => Characters(glyph).Contains("OutsideColumn", StringComparison.Ordinal) && IsColor(glyph.Foreground, Colors.Red));
    }

    [AvaloniaFact]
    public void Circular_outside_labels_and_center_text_retain_explicit_foreground()
    {
        var chart = Circular([new("one", "Outer", 1, "R", "C")]);
        chart.LabelPosition = CircularLabelPosition.Outside; chart.CenterText = "Center"; chart.Foreground = Brushes.Purple;
        chart.HighContrastMode = ChartHighContrastMode.Dark;
        using var host = new Host(chart);
        var glyphs = Glyphs(Record(chart)).ToArray();
        Assert.Contains(glyphs, glyph => Characters(glyph) == "Outer");
        Assert.Contains(glyphs, glyph => Characters(glyph).Contains("Center", StringComparison.Ordinal));
        Assert.All(glyphs, glyph => Assert.True(IsColor(glyph.Foreground, Colors.Purple)));
    }

    [AvaloniaFact]
    public void Explicit_analysis_label_brush_has_precedence_over_automatic_contrast()
    {
        var chart = new LabelChart { Fill = Brushes.Yellow, ExplicitLabelBrush = Brushes.Purple, Foreground = Brushes.Red, HighContrastMode = ChartHighContrastMode.Dark };
        using var host = new Host(chart);
        Assert.All(InsideGlyphs(chart), glyph => Assert.True(IsColor(glyph.Foreground, Colors.Purple)));
    }

    [AvaloniaFact]
    public void Current_mutable_fill_colors_are_resolved_at_paint_without_stale_label_cache()
    {
        var fill = new SolidColorBrush(Colors.White); var chart = new LabelChart { Fill = fill, Foreground = Brushes.Red };
        using var host = new Host(chart);
        Assert.All(InsideGlyphs(chart), glyph => Assert.True(IsColor(glyph.Foreground, Colors.Black)));
        fill.Color = Colors.Black;
        Assert.All(InsideGlyphs(chart), glyph => Assert.True(IsColor(glyph.Foreground, Colors.White)));
    }

    [AvaloniaFact]
    public void Unknown_alpha_and_gradient_backdrops_keep_the_requested_fallback()
    {
        var fills = new IBrush[] { new SolidColorBrush(Colors.White, .5), new SolidColorBrush(Color.FromArgb(127, 255, 255, 255)), new LinearGradientBrush() };
        foreach (var fill in fills)
        {
            var chart = new LabelChart { Fill = fill, Foreground = Brushes.Red };
            using var host = new Host(chart);
            Assert.All(InsideGlyphs(chart), glyph => Assert.True(IsColor(glyph.Foreground, Colors.Red)));
        }
    }

    [AvaloniaFact]
    public void Matrix_and_table_keep_their_existing_composited_fill_contrast()
    {
        foreach (var table in new[] { false, true })
        {
            OperationalMatrixChartBase chart = table ? new TableChart() : new MatrixChart();
            chart.ItemsSource = new[] { new LabelRow("a", "A", 1, "R", "C") }; chart.Foreground = Brushes.Red; chart.Background = Brushes.Black;
            chart.MinimumColor = chart.MaximumColor = Colors.White; chart.HighContrastMode = ChartHighContrastMode.Dark;
            using var host = new Host(chart);
            var glyphs = Glyphs(Record(chart)).ToArray();
            Assert.Contains(glyphs, glyph => Characters(glyph) == "1" && IsColor(glyph.Foreground, Colors.Black));
            Assert.Contains(glyphs, glyph => Characters(glyph) == "R" && IsColor(glyph.Foreground, Colors.Red));
        }
    }

    [AvaloniaFact]
    public void Replacing_a_comparison_source_releases_the_previous_label_frame_without_another_paint()
    {
        var chart = new MosaicChart(); using var host = new Host(chart);
        var retired = PaintAndReplaceSource(chart);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.False(retired.IsAlive);
        GC.KeepAlive(chart);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference PaintAndReplaceSource(MosaicChart chart)
    {
        var old = new LabelRow("old", "Old", 1, "OldRow", "OldColumn");
        chart.ItemsSource = new[] { old }; _ = chart.GetMarks(); _ = Record(chart);
        var retired = new WeakReference(old);
        chart.ItemsSource = new[] { new LabelRow("new", "New", 2, "NewRow", "NewColumn") };
        _ = chart.GetMarks();
        // There is intentionally no second Render: the hidden/replaced frame must
        // release original business items before the control paints again.
        return retired;
    }

    private static CircularChart Circular(LabelRow[] rows)
    {
        var chart = new CircularChart { LabelPosition = CircularLabelPosition.Inside, ShowLegend = false, LabelFormatter = slice => slice.Data.Category?.ToString() ?? "", Title = "OutsideTitle" };
        chart.Series.Add(new DonutSeries { ItemsSource = rows, CategoryPath = nameof(LabelRow.Label), ValuePath = nameof(LabelRow.Value), IdPath = nameof(LabelRow.Id) });
        return chart;
    }
    private static GlyphRunDrawing[] InsideGlyphs(ChartBase chart)
    {
        var glyphs = Glyphs(Record(chart)).Where(glyph => Characters(glyph) == "Inside").ToArray(); Assert.NotEmpty(glyphs); return glyphs;
    }
    private static double Contrast(Color a, Color b)
    {
        static double Channel(byte value) { var channel = value / 255d; return channel <= .04045 ? channel / 12.92 : Math.Pow((channel + .055) / 1.055, 2.4); }
        static double Luma(Color color) => .2126 * Channel(color.R) + .7152 * Channel(color.G) + .0722 * Channel(color.B);
        var first = Luma(a); var second = Luma(b); return (Math.Max(first, second) + .05) / (Math.Min(first, second) + .05);
    }
    private static string Characters(GlyphRunDrawing glyph) => glyph.GlyphRun is { } run ? new string(run.Characters.Span) : "";
    private static bool IsColor(IBrush? brush, Color color) => brush is ISolidColorBrush solid && solid.Color == color && solid.Opacity == 1;
    private static DrawingGroup Record(ChartBase chart) { var result = new DrawingGroup(); using var context = result.Open(); chart.Render(context); return result; }
    private static IEnumerable<GlyphRunDrawing> Glyphs(Drawing drawing)
    { if (drawing is DrawingGroup group) foreach (var child in group.Children) foreach (var glyph in Glyphs(child)) yield return glyph; else if (drawing is GlyphRunDrawing glyph) yield return glyph; }
    private sealed record LabelRow(string Id, string Label, double Value, string Row, string Column);
    private sealed class LabelChart : AnalysisChartBase
    {
        public IBrush? Fill { get; init; } public IBrush? ExplicitLabelBrush { get; init; }
        protected override IReadOnlyList<AnalysisMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot) =>
            [new(new ChartDataPoint(0, "one", this, "Inside", new(0, 1), false), new RectangleGeometry(plot), Fill, plot.Center) { Label = "Inside", LabelBrush = ExplicitLabelBrush }];
    }
    private sealed class Host : IDisposable
    {
        private readonly Window _window;
        public Host(ChartBase chart) { _window = new Window { Content = chart, Width = 900, Height = 700 }; _window.Show(); _window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); _window.UpdateLayout(); }
        public void Dispose() { _window.Close(); Dispatcher.UIThread.RunJobs(); }
    }
}
