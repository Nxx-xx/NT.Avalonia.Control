using Avalonia.Markup.Xaml.Styling;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using NT.AvaloniaControl.Charts;

namespace NT.AvaloniaControl.Charts.Themes;

/// <summary>Optional default chart typography and light/dark brushes.</summary>
public sealed class ChartsTheme : Styles
{
    public ChartsTheme()
    {
        Add(new StyleInclude(new Uri("avares://NT.AvaloniaControl.Charts.Themes/"))
        {
            Source = new Uri("avares://NT.AvaloniaControl.Charts.Themes/Themes/Generic.axaml"),
        });
        Add(Contrast(ChartHighContrastMode.Light, Brushes.White, Brushes.Black, ChartContrastPalettes.Light));
        Add(Contrast(ChartHighContrastMode.Dark, Brushes.Black, Brushes.White, ChartContrastPalettes.Dark));
    }

    private static Style Contrast(ChartHighContrastMode mode, IBrush canvas, IBrush foreground, IReadOnlyList<IBrush> palette) =>
        new(selector => selector.OfType<CartesianChart>().PropertyEquals(CartesianChart.HighContrastModeProperty, mode))
        {
            Setters =
            {
                new Setter(TemplatedControl.BackgroundProperty, canvas),
                new Setter(TemplatedControl.ForegroundProperty, foreground),
                new Setter(CartesianChart.AxisBrushProperty, foreground),
                new Setter(CartesianChart.GridLineBrushProperty, Brush.Parse("#767676")),
                new Setter(CartesianChart.ColorPaletteProperty, palette),
                new Setter(CartesianChart.SelectionBrushProperty, foreground),
                new Setter(CartesianChart.CrosshairBrushProperty, foreground),
                new Setter(CartesianChart.FloatingLegendBackgroundProperty, canvas),
                new Setter(CartesianChart.TooltipBackgroundProperty, canvas),
                new Setter(CartesianChart.TooltipForegroundProperty, foreground),
                new Setter(CartesianChart.TooltipBorderBrushProperty, foreground),
            },
        };
}
