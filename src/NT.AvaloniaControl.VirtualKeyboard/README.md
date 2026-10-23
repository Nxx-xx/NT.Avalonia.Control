# NT.AvaloniaControl.VirtualKeyboard

An application-local numeric and English virtual keyboard for Avalonia 12.0.3, targeting .NET 10. It submits text and editing actions through the public TextBox input route, preserving native selection, MaxLength, TwoWay binding and undo/redo behavior.

The function package has no dependency on its optional default theme package. Install the matching `NT.AvaloniaControl.VirtualKeyboard.Themes` package, or supply application templates for `VirtualKeyboard` and `VirtualKeyboardKeyButton`. The required keyboard template part is `PART_RowsPresenter` of type `ItemsControl`; render its `Rows` and each row's `Keys`, binding `VirtualKeyboardKeyButton.Key` to the key model.

```csharp
using Avalonia.Controls;
using NT.AvaloniaControl.VirtualKeyboard;

var editor = new TextBox();
var keyboard = new VirtualKeyboard { Target = editor, Layout = VirtualKeyboardLayout.English };
// Mount both in the same Window and let the host focus editor after showing it.
// A page becoming inactive must call keyboard.CancelInput().
```

Only the focused, editable TextBox in the same active Window accepts input. Target changes, layout changes, lost focus, disabled/hidden ancestors, window deactivation, template changes and unload invalidate an old press/repeat session. Enter and Tab either use the TextBox's accepted native action or raise target-only `ReturnRequested` / `NextRequested` host requests.

The first version inputs ASCII. Existing Unicode text is edited using native TextBox boundaries. Finish or cancel system IME composition before using virtual keys; simultaneous IME composition is outside the supported contract. Passwords use the target's own masking without a keyboard preview or text diagnostics. Physical touch, platform IME, DPI and Linux/Kylin acceptance require separate device testing.
