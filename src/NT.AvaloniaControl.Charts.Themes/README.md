# NT.AvaloniaControl.Charts.Themes

Optional light and dark chart styling. Add `new ChartsTheme()` after the application theme:

```csharp
app.Styles.Add(new NT.AvaloniaControl.Charts.Themes.ChartsTheme());
```

Or include `avares://NT.AvaloniaControl.Charts.Themes/Themes/Generic.axaml` with `StyleInclude`.
Override `Foreground`, `AxisBrush`, `GridLineBrush`, `Background`, `FontSize` and `Padding` on a chart to customize it.
Tooltip colors follow light/dark mode and can be overridden with `TooltipBackground`, `TooltipForeground` and `TooltipBorderBrush`.

These tooltip resources also apply to `EngineeringChartBase` and `AnalysisChartBase` descendants. Their optional `TooltipContentTemplate` creates a passive live control overlay using those three chrome brushes; template text inherits the tooltip foreground, font and resources. `ChartDataTooltipContext` supplies original `Members`, the current mark or point, and a separate `BusinessDataContext`. See the [chart API usage](../NT.AvaloniaControl.Charts/README.md#专业图族-tooltip-模板). Professional chart raster exports exclude transient tooltip content.

Professional `LegendItemTemplate` and `DataLabelTemplate` controls inherit chart resources, fonts, `Foreground` and the selected theme through their real control parents. No separate theme resource is required. Bind templates to `ChartDataLegendItemContext` or `ChartDataLabelItemContext`; `BusinessDataContext` keeps the application view model separate from the reading. Permanent legends and labels are included in PNG/JPEG exports, while transient professional tooltips remain excluded. Template-local colors retain precedence and need their own contrast checks. See [shared presentation usage and bounds](../NT.AvaloniaControl.Charts/README.md#专业图族共享图例与数据标签模板).

With `ChartsTheme` installed, set `ChartBase.HighContrastMode` to `Light` or `Dark` for a matching canvas, text, axes, grid, tooltip, selection and six-color palette. `None` restores the ordinary theme. The immutable `ChartContrastPalettes.Light` and `.Dark` colors each meet 7:1 sRGB contrast against the corresponding white or black canvas. Explicit series colors and local chart values/bindings retain precedence; custom colors remain the application's responsibility. Use marker shapes and text as additional series cues.

The preset is opt-in and does not detect the operating system preference. Including `Generic.axaml` alone supplies the ordinary control themes; use `new ChartsTheme()` for the conditional high-contrast styles, or supply equivalent styles in the application. Function-only consumers can use the palettes without referencing this package.

C7-01 applies the conditional preset to all seven chart style roots and their descendants, including engineering, hierarchy, relations, maps, scheduling and operational charts. Cartesian keeps its existing property entry points. Normal-theme opaque inside labels choose black/white from their actual fill; map labels also compose solid alpha backgrounds. Continuous scales, custom brushes, gradients and overlapping translucent layers require separate contrast verification. These presets do not qualify native accessibility or operating-system preference integration. See the [integration report](../../docs/charts-c7-integration.md).
