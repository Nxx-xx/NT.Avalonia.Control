# NT.AvaloniaControl.Charts.Themes

Optional light and dark chart styling. Add `new ChartsTheme()` after the application theme:

```csharp
app.Styles.Add(new NT.AvaloniaControl.Charts.Themes.ChartsTheme());
```

Or include `avares://NT.AvaloniaControl.Charts.Themes/Themes/Generic.axaml` with `StyleInclude`.
Override `Foreground`, `AxisBrush`, `GridLineBrush`, `Background`, `FontSize` and `Padding` on a chart to customize it.
Tooltip colors follow light/dark mode and can be overridden with `TooltipBackground`, `TooltipForeground` and `TooltipBorderBrush`.

With `ChartsTheme` installed, set `CartesianChart.HighContrastMode` to `Light` or `Dark` for a matching canvas, text, axes, grid, tooltip, selection and six-color palette. `None` restores the ordinary theme. The immutable `ChartContrastPalettes.Light` and `.Dark` colors each meet 7:1 sRGB contrast against the corresponding white or black canvas. Explicit series colors and local chart values/bindings retain precedence; custom colors remain the application's responsibility. Use marker shapes and text as additional series cues.

The preset is opt-in and does not detect the operating system preference. Including `Generic.axaml` alone supplies the ordinary control themes; use `new ChartsTheme()` for the conditional high-contrast styles, or supply equivalent styles in the application. Function-only consumers can use the palettes without referencing this package.
