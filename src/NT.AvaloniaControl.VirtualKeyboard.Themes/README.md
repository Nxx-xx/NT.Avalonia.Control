# NT.AvaloniaControl.VirtualKeyboard.Themes

Optional light and dark templates for `NT.AvaloniaControl.VirtualKeyboard`, targeting Avalonia 12.0.3 and .NET 10. This package depends on the matching function package; the function package does not depend on this theme.

Load Avalonia Fluent styles followed by the keyboard theme:

```csharp
Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
Styles.Add(new NT.AvaloniaControl.VirtualKeyboard.Themes.VirtualKeyboardTheme());
```

The public XAML style URI is `avares://NT.AvaloniaControl.VirtualKeyboard.Themes/Themes/Generic.axaml`. Place `NT.AvaloniaControl.VirtualKeyboard.VirtualKeyboard` in the host view and set `Target` to an editable TextBox in the same active Window after establishing editing focus.

Override the `VirtualKeyboardBackgroundBrush`, `VirtualKeyboardForegroundBrush`, `VirtualKeyboardKeyPressedBrush`, `VirtualKeyboardKeyLockedBrush`, `VirtualKeyboardFontSize`, `VirtualKeyboardKeyMinWidth`, `VirtualKeyboardKeyMinHeight` and `VirtualKeyboardKeySpacing` resources as needed. Keys default to at least 32 by 44 logical pixels with 6-pixel spacing. The default template allows horizontal scrolling when the available area is smaller than the measured natural key width.

The theme only controls presentation. Layout, text mapping, cancellation and repeat behavior remain in the function package. Call `CancelInput()` when a host page becomes inactive. Finish or cancel system IME composition before using virtual keys. Complete device testing separately for physical touch, system IME, DPI and Linux/Kylin combinations.
