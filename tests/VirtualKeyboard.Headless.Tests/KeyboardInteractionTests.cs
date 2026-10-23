using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using NT.AvaloniaControl.VirtualKeyboard;
using Keyboard = NT.AvaloniaControl.VirtualKeyboard.VirtualKeyboard;

namespace VirtualKeyboard.Headless.Tests;

public sealed class KeyboardInteractionTests
{
    [AvaloniaFact]
    public void Function_package_custom_template_receives_real_headless_mouse_and_preserves_selection_focus()
    {
        using var fixture = new KeyboardFixture("abcd");
        fixture.Editor.SelectionStart = 1;
        fixture.Editor.SelectionEnd = 3;
        var button = fixture.Button("q");
        var point = fixture.Point(button);
        fixture.Window.MouseDown(point, MouseButton.Left);
        Assert.Equal("abcd", fixture.Editor.Text);
        Assert.True(fixture.Editor.IsFocused);
        fixture.Window.MouseUp(point, MouseButton.Left);
        Assert.Equal("aqd", fixture.Editor.Text);
        Assert.True(fixture.Editor.IsFocused);
        Assert.Equal(2, fixture.Editor.CaretIndex);
        fixture.Editor.Undo();
        Assert.Equal("abcd", fixture.Editor.Text);
        fixture.Editor.Redo();
        Assert.Equal("aqd", fixture.Editor.Text);
    }

    [AvaloniaFact]
    public void Touch_click_commits_once_and_an_extra_pointer_is_ignored()
    {
        using var fixture = new KeyboardFixture();
        using var first = fixture.Press("q");
        using var second = fixture.Press("w", primary: false);
        Assert.Null(second.Captured);
        fixture.Release("w", second);
        Assert.True(string.IsNullOrEmpty(fixture.Editor.Text));
        fixture.Release("q", first);
        Assert.Equal("q", fixture.Editor.Text);
        fixture.Release("q", first);
        Assert.Equal("q", fixture.Editor.Text);
        Assert.True(fixture.Editor.IsFocused);
    }

    [AvaloniaFact]
    public void Moving_out_or_losing_capture_cancels_an_uncommitted_character()
    {
        using var fixture = new KeyboardFixture();
        using var pointer = fixture.Press("q");
        fixture.MoveOut("q", pointer);
        Assert.Null(pointer.Captured);
        fixture.Release("q", pointer);
        Assert.True(string.IsNullOrEmpty(fixture.Editor.Text));
        using var captured = fixture.Press("w");
        captured.Capture(null);
        fixture.Release("w", captured);
        Assert.True(string.IsNullOrEmpty(fixture.Editor.Text));
        fixture.Tap("q");
        Assert.Equal("q", fixture.Editor.Text);
    }

    [AvaloniaFact]
    public void Shift_caps_and_ascii_punctuation_apply_once_and_layout_or_target_changes_reset_state()
    {
        using var fixture = new KeyboardFixture();
        fixture.Tap("Shift");
        Assert.True(fixture.Keyboard.IsShiftActive);
        Assert.Equal("Q", fixture.Button("q").Label);
        fixture.Tap("q");
        Assert.False(fixture.Keyboard.IsShiftActive);
        fixture.Tap("CapsLock");
        fixture.Tap("Shift");
        fixture.Tap("q");
        fixture.Tap("1");
        fixture.Tap("Shift");
        fixture.Tap("1");
        Assert.Equal("Qq1!", fixture.Editor.Text);
        fixture.Keyboard.Layout = VirtualKeyboardLayout.Numeric;
        fixture.Window.UpdateLayout();
        Assert.False(fixture.Keyboard.IsCapsLockActive);
        Assert.False(fixture.Keyboard.IsShiftActive);
        fixture.Keyboard.Layout = VirtualKeyboardLayout.English;
        fixture.Window.UpdateLayout();
        fixture.Tap("CapsLock");
        fixture.Other.Focus();
        fixture.Keyboard.Target = fixture.Other;
        Assert.False(fixture.Keyboard.IsCapsLockActive);
    }

    [AvaloniaFact]
    public void Enter_tab_raise_target_identity_or_insert_once_and_do_not_navigate_implicitly()
    {
        using var fixture = new KeyboardFixture("a");
        IInputElement? returnTarget = null;
        IInputElement? nextTarget = null;
        var returns = 0;
        var nexts = 0;
        fixture.Keyboard.ReturnRequested += (_, e) => { returnTarget = e.Target; returns++; };
        fixture.Keyboard.NextRequested += (_, e) => { nextTarget = e.Target; nexts++; };
        fixture.Tap("Return");
        fixture.Tap("Tab");
        Assert.Same(fixture.Editor, returnTarget);
        Assert.Same(fixture.Editor, nextTarget);
        Assert.Equal(1, returns);
        Assert.Equal(1, nexts);
        Assert.Equal("a", fixture.Editor.Text);
        Assert.True(fixture.Editor.IsFocused);
        fixture.Editor.AcceptsReturn = true;
        fixture.Editor.AcceptsTab = true;
        fixture.Tap("Return");
        fixture.Tap("Tab");
        Assert.Equal("a" + fixture.Editor.NewLine + "\t", fixture.Editor.Text);
        Assert.Equal(1, returns);
        Assert.Equal(1, nexts);
    }

    [AvaloniaFact]
    public void Editing_constraints_physical_input_and_virtual_input_share_the_editor()
    {
        using var fixture = new KeyboardFixture("a");
        fixture.Editor.MaxLength = 3;
        fixture.Window.KeyTextInput("b");
        fixture.Tap("q");
        fixture.Tap("w");
        Assert.Equal("abq", fixture.Editor.Text);
        fixture.Tap("Backspace");
        Assert.Equal("ab", fixture.Editor.Text);
        fixture.Editor.Undo();
        Assert.Equal("abq", fixture.Editor.Text);
        fixture.Editor.IsReadOnly = true;
        Assert.False(fixture.Keyboard.IsInputAvailable);
        fixture.Tap("Backspace");
        Assert.Equal("abq", fixture.Editor.Text);
    }

    [AvaloniaFact]
    public void A_pending_character_is_cancelled_by_target_layout_template_and_host_cancellation()
    {
        foreach (var cancellation in new[] { "target", "layout", "template", "host" })
        {
            using var fixture = new KeyboardFixture("seed");
            var button = fixture.Button("q");
            using var pointer = fixture.Press("q");
            switch (cancellation)
            {
                case "target": fixture.Other.Focus(); fixture.Keyboard.Target = fixture.Other; break;
                case "layout": fixture.Keyboard.Layout = VirtualKeyboardLayout.Numeric; break;
                case "template": fixture.Keyboard.Template = KeyboardFixture.CreateTemplate(); fixture.Window.UpdateLayout(); break;
                case "host": fixture.Keyboard.CancelInput(); break;
            }
            fixture.Release(button, pointer);
            Assert.Equal("seed", fixture.Editor.Text);
            Assert.Equal("other", fixture.Other.Text);
            Assert.Null(pointer.Captured);
        }
    }

    [AvaloniaFact]
    public void Missing_required_template_part_is_diagnosed_and_cannot_invoke_a_key()
    {
        using var fixture = new KeyboardFixture("seed");
        fixture.Keyboard.Template = new FuncControlTemplate<Keyboard>((_, _) => new Border());
        fixture.Window.UpdateLayout();
        Assert.False(fixture.Keyboard.IsInputAvailable);
        Assert.Contains("PART_RowsPresenter", fixture.Keyboard.TemplateDiagnostic);
        Assert.False(fixture.Keyboard.InvokeKey(fixture.Keyboard.Rows[1].Keys[1]));
        Assert.Equal("seed", fixture.Editor.Text);
    }

    [AvaloniaFact]
    public void A_wrong_type_required_template_part_has_a_diagnostic_without_throwing()
    {
        using var fixture = new KeyboardFixture("seed");
        fixture.Keyboard.Template = new FuncControlTemplate<Keyboard>((_, scope) =>
        {
            var border = new Border();
            scope.Register("PART_RowsPresenter", border);
            return border;
        });
        fixture.Window.UpdateLayout();
        Assert.False(fixture.Keyboard.IsInputAvailable);
        Assert.Contains("ItemsControl", fixture.Keyboard.TemplateDiagnostic);
        Assert.Equal("seed", fixture.Editor.Text);
    }

    [AvaloniaFact]
    public void Automation_peer_has_button_name_and_invokes_once_with_disabled_gate()
    {
        using var fixture = new KeyboardFixture();
        var button = fixture.Button("q");
        var peer = ControlAutomationPeer.CreatePeerForElement(button)!;
        Assert.Equal(AutomationControlType.Button, peer.GetAutomationControlType());
        Assert.Equal("q", peer.GetName());
        var invoke = Assert.IsAssignableFrom<IInvokeProvider>(peer.GetProvider<IInvokeProvider>());
        invoke.Invoke();
        Assert.Equal("q", fixture.Editor.Text);
        fixture.Editor.IsReadOnly = true;
        Assert.False(peer.IsEnabled());
        Assert.Throws<ElementNotEnabledException>(invoke.Invoke);
        Assert.Equal("q", fixture.Editor.Text);
    }

    [AvaloniaFact]
    public void Automation_editing_keys_have_readable_action_names()
    {
        using var fixture = new KeyboardFixture();
        foreach (var (id, name) in new[]
        {
            ("Left", "Move left"), ("Right", "Move right"),
            ("Home", "Move to beginning"), ("End", "Move to end"),
        })
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(fixture.Button(id))!;
            Assert.Equal(name, peer.GetName());
        }
    }

    [AvaloniaFact]
    public void Automation_cannot_invoke_a_key_in_a_hidden_custom_container()
    {
        using var fixture = new KeyboardFixture();
        var button = fixture.Button("q");
        var container = Assert.IsAssignableFrom<Control>(button.GetVisualParent());
        var peer = ControlAutomationPeer.CreatePeerForElement(button)!;
        var invoke = Assert.IsAssignableFrom<IInvokeProvider>(peer.GetProvider<IInvokeProvider>());
        container.IsVisible = false;
        Assert.False(peer.IsEnabled());
        Assert.Throws<ElementNotEnabledException>(invoke.Invoke);
        Assert.True(string.IsNullOrEmpty(fixture.Editor.Text));
        container.IsVisible = true;
        invoke.Invoke();
        Assert.Equal("q", fixture.Editor.Text);
    }
}

internal sealed class KeyboardFixture : IDisposable
{
    internal KeyboardFixture(string? text = null)
    {
        Editor = new TextBox { Text = text, Width = 320 };
        Other = new TextBox { Text = "other", Width = 320 };
        Keyboard = new Keyboard { Target = Editor, Template = CreateTemplate(), Height = 230 };
        KeyboardHost = new Border { Child = Keyboard };
        EditorHost = new StackPanel { Children = { Editor, Other } };
        Root = new StackPanel { Spacing = 8, Children = { EditorHost, KeyboardHost } };
        Window = new Window { Content = Root, Width = 1040, Height = 400 };
        Window.Show();
        Window.UpdateLayout();
        NativeEditingPathTests.Drain();
        Assert.True(Window.IsActive);
        Assert.True(Editor.Focus());
        Editor.CaretIndex = Editor.Text?.Length ?? 0;
        NativeEditingPathTests.Drain();
        Assert.True(Keyboard.IsInputAvailable);
    }

    internal TextBox Editor { get; }
    internal TextBox Other { get; }
    internal Keyboard Keyboard { get; }
    internal Window Window { get; }
    internal StackPanel Root { get; }
    internal Border KeyboardHost { get; }
    internal StackPanel EditorHost { get; }

    internal VirtualKeyboardKeyButton Button(string id)
    {
        Window.UpdateLayout();
        return Keyboard.GetVisualDescendants().OfType<VirtualKeyboardKeyButton>().Single(button => button.Key?.Id == id);
    }

    internal Point Point(Control control) => control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), Window) ?? default;

    internal Pointer Press(string id, bool primary = true)
    {
        var button = Button(id);
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, primary);
        button.RaiseEvent(new PointerPressedEventArgs(button, pointer, Window, Point(button), 0, default, KeyModifiers.None));
        return pointer;
    }

    internal void Release(string id, Pointer pointer) => Release(Button(id), pointer);
    internal void Release(VirtualKeyboardKeyButton button, Pointer pointer) =>
        button.RaiseEvent(new PointerReleasedEventArgs(button, pointer, Window, Point(button), 1, default, KeyModifiers.None, MouseButton.Left));

    internal void MoveOut(string id, Pointer pointer)
    {
        var button = Button(id);
        var outside = button.TranslatePoint(new Point(-10, -10), Window)!.Value;
        button.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, button, pointer, Window, outside, 1, default, KeyModifiers.None));
    }

    internal void Tap(string id) { using var pointer = Press(id); Release(id, pointer); }
    public void Dispose() { Keyboard.CancelInput(); Window.Close(); }

    internal static FuncControlTemplate<Keyboard> CreateTemplate() => new((_, scope) =>
    {
        var rows = new ItemsControl
        {
            ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel { Spacing = 3 }),
            ItemTemplate = new FuncDataTemplate<VirtualKeyboardRow>((row, _) => new ItemsControl
            {
                ItemsSource = row!.Keys,
                ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 }),
                ItemTemplate = new FuncDataTemplate<VirtualKeyboardKey>((key, _) => new VirtualKeyboardKeyButton
                {
                    Key = key, Width = 60, Height = 38,
                    Template = new FuncControlTemplate<VirtualKeyboardKeyButton>((button, _) => new Border
                    {
                        Background = Brushes.LightGray,
                        Child = new TextBlock { Text = key!.Label },
                    }),
                }),
            }),
        };
        scope.Register("PART_RowsPresenter", rows);
        return rows;
    });
}
