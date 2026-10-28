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
        AddContrast(ChartHighContrastMode.Light, Brushes.White, Brushes.Black, ChartContrastPalettes.Light);
        AddContrast(ChartHighContrastMode.Dark, Brushes.Black, Brushes.White, ChartContrastPalettes.Dark);
    }

    private void AddContrast(ChartHighContrastMode mode, IBrush canvas, IBrush foreground, IReadOnlyList<IBrush> palette)
    {
        var cartesian = Contrast<CartesianChart>(mode, canvas, foreground);
        AddSetters(cartesian, [
            new Setter(CartesianChart.AxisBrushProperty, foreground),
            new Setter(CartesianChart.GridLineBrushProperty, Brush.Parse("#767676")),
            new Setter(CartesianChart.ColorPaletteProperty, palette),
            new Setter(CartesianChart.SelectionBrushProperty, foreground),
            new Setter(CartesianChart.CrosshairBrushProperty, foreground),
            new Setter(CartesianChart.FloatingLegendBackgroundProperty, canvas),
            new Setter(CartesianChart.TooltipBackgroundProperty, canvas),
            new Setter(CartesianChart.TooltipForegroundProperty, foreground),
            new Setter(CartesianChart.TooltipBorderBrushProperty, foreground),
        ]);
        Add(cartesian);

        var analysis = Contrast<AnalysisChartBase>(mode, canvas, foreground);
        AddSetters(analysis, [
            new Setter(AnalysisChartBase.PaletteProperty, palette),
            new Setter(AnalysisChartBase.TooltipBackgroundProperty, canvas),
            new Setter(AnalysisChartBase.TooltipForegroundProperty, foreground),
            new Setter(AnalysisChartBase.TooltipBorderBrushProperty, foreground),
        ]);
        Add(analysis);

        var engineering = Contrast<EngineeringChartBase>(mode, canvas, foreground);
        AddSetters(engineering, [
            new Setter(EngineeringChartBase.PaletteProperty, palette),
            new Setter(EngineeringChartBase.GridLineBrushProperty, Brush.Parse("#767676")),
            new Setter(EngineeringChartBase.TooltipBackgroundProperty, canvas),
            new Setter(EngineeringChartBase.TooltipForegroundProperty, foreground),
            new Setter(EngineeringChartBase.TooltipBorderBrushProperty, foreground),
        ]);
        Add(engineering);

        var polar = Contrast<PolarChart>(mode, canvas, foreground);
        AddSetters(polar, [
            new Setter(PolarChart.ColorPaletteProperty, palette),
            new Setter(PolarChart.AxisBrushProperty, foreground),
            new Setter(PolarChart.GridLineBrushProperty, Brush.Parse("#767676")),
        ]);
        Add(polar);

        var circular = Contrast<CircularChart>(mode, canvas, foreground);
        AddSetters(circular, [
            new Setter(CircularChart.ColorPaletteProperty, palette),
            new Setter(CircularChart.LabelConnectorBrushProperty, foreground),
        ]);
        Add(circular);

        var gauge = Contrast<GaugeBase>(mode, canvas, foreground);
        AddSetters(gauge, [
            new Setter(GaugeBase.AxisBrushProperty, foreground),
            new Setter(GaugeBase.TrackBrushProperty, Brush.Parse("#767676")),
            new Setter(GaugeBase.ValueBrushProperty, palette[1]),
            new Setter(GaugeBase.NeedleBrushProperty, foreground),
        ]);
        Add(gauge);

        var sparkline = Contrast<SparklineChart>(mode, canvas, foreground);
        AddSetters(sparkline, [
            new Setter(SparklineChart.LineBrushProperty, palette[0]),
            new Setter(SparklineChart.AreaFillProperty, canvas),
            new Setter(SparklineChart.BarBrushProperty, palette[0]),
            new Setter(SparklineChart.WinBrushProperty, palette[1]),
            new Setter(SparklineChart.LossBrushProperty, palette[3]),
            new Setter(SparklineChart.ZeroBrushProperty, foreground),
            new Setter(SparklineChart.BaselineBrushProperty, foreground),
            new Setter(SparklineChart.MinBrushProperty, palette[1]),
            new Setter(SparklineChart.MaxBrushProperty, palette[3]),
            new Setter(SparklineChart.FirstBrushProperty, palette[4]),
            new Setter(SparklineChart.LastBrushProperty, palette[5]),
        ]);
        Add(sparkline);
    }

    private static void AddSetters(Style style, Setter[] setters)
    {
        foreach (var setter in setters) style.Setters.Add(setter);
    }

    private static Style Contrast<T>(ChartHighContrastMode mode, IBrush canvas, IBrush foreground) where T : ChartBase =>
        new(selector => selector.Is<T>().PropertyEquals(ChartBase.HighContrastModeProperty, mode))
        {
            Setters =
            {
                new Setter(TemplatedControl.BackgroundProperty, canvas),
                new Setter(TemplatedControl.ForegroundProperty, foreground),
            },
        };
}
