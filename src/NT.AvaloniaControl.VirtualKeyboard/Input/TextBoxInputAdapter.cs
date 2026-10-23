using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace NT.AvaloniaControl.VirtualKeyboard.Input;

/// <summary>
/// Routes input through the editor's public event path. The keyboard owns the
/// session and must additionally validate its own visibility and enabled state.
/// </summary>
internal static class TextBoxInputAdapter
{
    internal static bool CanInput(IInputElement? target, TopLevel? keyboardRoot)
    {
        if (target is not TextBox editor || keyboardRoot is not Window window ||
            !window.IsActive || !ReferenceEquals(TopLevel.GetTopLevel(editor), keyboardRoot) ||
            !editor.IsFocused || editor.IsReadOnly || !editor.IsEffectivelyEnabled)
        {
            return false;
        }

        for (Visual? visual = editor; visual is not null; visual = visual.GetVisualParent())
        {
            // Read each raw enabled value as well: its change event can run before
            // the inherited effective state has reached the descendant editor.
            if (!visual.IsVisible || visual is InputElement { IsEnabled: false })
                return false;
        }

        // KeyDown requires the TextBox's native presenter. A host supplied editor
        // template without one cannot satisfy the full editing contract.
        return editor.GetVisualDescendants().OfType<TextPresenter>()
            .Any(presenter => ReferenceEquals(presenter.TemplatedParent, editor));
    }

    internal static VirtualKeyboardInputResult TryCommitText(
        IInputElement? target, TopLevel? keyboardRoot, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!CanInput(target, keyboardRoot) || string.IsNullOrEmpty(text))
            return VirtualKeyboardInputResult.Unavailable;

        var editor = (TextBox)target!;
        var args = new TextInputEventArgs
        {
            RoutedEvent = InputElement.TextInputEvent,
            Source = editor,
            Text = text,
        };
        editor.RaiseEvent(args);
        return args.Handled ? VirtualKeyboardInputResult.Handled : VirtualKeyboardInputResult.Unavailable;
    }

    internal static VirtualKeyboardInputResult TryExecute(
        IInputElement? target, TopLevel? keyboardRoot, VirtualKeyboardKeyKind kind)
    {
        if (!CanInput(target, keyboardRoot))
            return VirtualKeyboardInputResult.Unavailable;

        var editor = (TextBox)target!;
        if (kind == VirtualKeyboardKeyKind.Return && !editor.AcceptsReturn)
            return VirtualKeyboardInputResult.ReturnRequested;
        if (kind == VirtualKeyboardKeyKind.Tab && !editor.AcceptsTab)
            return VirtualKeyboardInputResult.NextRequested;
        if (kind == VirtualKeyboardKeyKind.SelectAll)
        {
            editor.SelectAll();
            return VirtualKeyboardInputResult.Handled;
        }

        var key = kind switch
        {
            VirtualKeyboardKeyKind.Backspace => Key.Back,
            VirtualKeyboardKeyKind.Delete => Key.Delete,
            VirtualKeyboardKeyKind.Left => Key.Left,
            VirtualKeyboardKeyKind.Right => Key.Right,
            VirtualKeyboardKeyKind.Home => Key.Home,
            VirtualKeyboardKeyKind.End => Key.End,
            VirtualKeyboardKeyKind.Return => Key.Enter,
            VirtualKeyboardKeyKind.Tab => Key.Tab,
            _ => Key.None,
        };
        if (key == Key.None)
            return VirtualKeyboardInputResult.Unavailable;

        var args = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Source = editor,
            Key = key,
        };
        editor.RaiseEvent(args);
        // Cursor movement at a text boundary may be a valid no-op with Handled=false.
        // The editor remains the authority for edit semantics and native constraints.
        return VirtualKeyboardInputResult.Handled;
    }
}
