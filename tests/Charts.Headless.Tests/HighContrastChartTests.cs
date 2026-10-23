using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using NT.AvaloniaControl.Charts;

namespace Charts.Headless.Tests;

public sealed class HighContrastChartTests
{
    [Fact]
    public void Built_in_palette_colors_meet_declared_contrast_and_are_immutable()
    {
        Assert.Equal(6, ChartContrastPalettes.Light.Count); Assert.Equal(6, ChartContrastPalettes.Dark.Count);
        foreach (var brush in ChartContrastPalettes.Light.Cast<ISolidColorBrush>())
        { Assert.Equal(255, brush.Color.A); Assert.True(Contrast(brush.Color, Colors.White) >= 7); Assert.IsNotType<SolidColorBrush>(brush); }
        foreach (var brush in ChartContrastPalettes.Dark.Cast<ISolidColorBrush>())
        { Assert.Equal(255, brush.Color.A); Assert.True(Contrast(brush.Color, Colors.Black) >= 7); Assert.IsNotType<SolidColorBrush>(brush); }
    }

    [AvaloniaTheory]
    [InlineData(ChartHighContrastMode.Light)]
    [InlineData(ChartHighContrastMode.Dark)]
    public void Optional_theme_applies_complete_contrast_preset_and_restores_normal_mode(ChartHighContrastMode mode)
    {
        var chart = new CartesianChart(); var series = new LineSeries(); chart.Series.Add(series);
        var window = new Window { Content = chart, Width = 600, Height = 400, RequestedThemeVariant = ThemeVariant.Light }; window.Show(); window.UpdateLayout();
        try
        {
            var normal = ((ISolidColorBrush)chart.Foreground!).Color;
            chart.HighContrastMode = mode; window.UpdateLayout();
            var backdrop = ((ISolidColorBrush)chart.Background!).Color; var foreground = ((ISolidColorBrush)chart.Foreground!).Color;
            Assert.Equal(mode == ChartHighContrastMode.Light ? Colors.White : Colors.Black, backdrop);
            Assert.True(Contrast(foreground, backdrop) >= 7);
            Assert.Same(mode == ChartHighContrastMode.Light ? ChartContrastPalettes.Light : ChartContrastPalettes.Dark, chart.ColorPalette);
            Assert.True(Contrast(((ISolidColorBrush)chart.GetSeriesBrush(series)).Color, backdrop) >= 7);
            Assert.Equal(foreground, ((ISolidColorBrush)chart.AxisBrush!).Color);
            Assert.Equal(backdrop, ((ISolidColorBrush)chart.TooltipBackground!).Color);
            chart.HighContrastMode = ChartHighContrastMode.None; window.UpdateLayout(); Assert.Equal(normal, ((ISolidColorBrush)chart.Foreground!).Color);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Host_local_styles_and_series_brushes_keep_precedence_and_palette_identity_survives_reorder()
    {
        var chart = new CartesianChart { Foreground = Brushes.Pink }; var first = new LineSeries(); var second = new BarSeries { Fill = Brushes.Red };
        chart.Series.Add(first); chart.Series.Add(second); var window = new Window { Content = chart, Width = 600, Height = 400 }; window.Show(); window.UpdateLayout();
        try
        {
            _ = chart.GetSeriesBrush(first); _ = chart.GetSeriesBrush(second); chart.HighContrastMode = ChartHighContrastMode.Dark;
            Assert.Same(Brushes.Pink, chart.Foreground); Assert.Same(Brushes.Red, chart.GetSeriesBrush(second));
            var before = chart.GetSeriesBrush(first); chart.Series.Move(0, 1); Assert.Same(before, chart.GetSeriesBrush(first));
        }
        finally { window.Close(); }
    }

    private static double Contrast(Color first, Color second)
    { var a = Luma(first); var b = Luma(second); return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05); }
    private static double Luma(Color color)
    { double Channel(byte value) { var s = value / 255d; return s <= .04045 ? s / 12.92 : Math.Pow((s + .055) / 1.055, 2.4); } return .2126 * Channel(color.R) + .7152 * Channel(color.G) + .0722 * Channel(color.B); }
}
