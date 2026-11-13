using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using NT.AvaloniaControl.VirtualKeyboard;
using Keyboard = NT.AvaloniaControl.VirtualKeyboard.VirtualKeyboard;

namespace VirtualKeyboard.Headless.Tests;

public sealed class KeyboardScopeTests
{
    private static VirtualKeyboardScope Attach(KeyboardFixture f, VirtualKeyboardDisplayMode mode = VirtualKeyboardDisplayMode.OnFocus)
    {
        f.Keyboard.Target = null;
        return new VirtualKeyboardScope(f.Window, f.Keyboard, mode);
    }
    [AvaloniaFact]
    public void Focus_scope_follows_editors_and_preserves_native_input()
    {
        using var f = new KeyboardFixture("abc"); using var scope = Attach(f);
        Assert.Same(f.Editor, f.Keyboard.Target); f.Tap("q"); Assert.Equal("abcq", f.Editor.Text);
        f.Other.Focus(); NativeEditingPathTests.Drain();
        Assert.Same(f.Other, f.Keyboard.Target); f.Other.CaretIndex = 5; f.Tap("q");
        Assert.Equal("otherq", f.Other.Text); Assert.Equal("abcq", f.Editor.Text);
    }
    [AvaloniaFact]
    public void Manual_requires_explicit_open_each_focus_session()
    {
        using var f = new KeyboardFixture(); using var scope = Attach(f, VirtualKeyboardDisplayMode.Manual);
        Assert.False(f.Keyboard.IsVisible); Touch(f, f.Editor); Assert.False(f.Keyboard.IsVisible);
        Assert.True(scope.Open()); f.Other.Focus(); NativeEditingPathTests.Drain();
        Assert.False(f.Keyboard.IsVisible); Assert.True(scope.Open());
    }
    [AvaloniaFact]
    public void Close_and_external_hide_suppress_until_focus_change_or_new_touch()
    {
        using var f = new KeyboardFixture(); using var scope = Attach(f);
        scope.Close(); VirtualKeyboardScope.SetInputPurpose(f.Editor, VirtualKeyboardInputPurpose.Integer);
        NativeEditingPathTests.Drain(); Assert.False(f.Keyboard.IsVisible);
        Touch(f, f.Editor); Assert.True(f.Keyboard.IsVisible);
        f.Keyboard.IsVisible = false; NativeEditingPathTests.Drain(); Assert.Null(f.Keyboard.Target);
        f.Other.Focus(); NativeEditingPathTests.Drain(); Assert.True(f.Keyboard.IsVisible);
    }
    [AvaloniaFact]
    public void Touch_mode_does_not_open_for_native_mouse_or_programmatic_focus()
    {
        using var f = new KeyboardFixture(); using var scope = Attach(f, VirtualKeyboardDisplayMode.OnTouch);
        f.Other.Focus(); NativeEditingPathTests.Drain(); Assert.False(f.Keyboard.IsVisible);
        f.Window.UpdateLayout(); var p = f.Point(f.Editor);
        f.Window.MouseDown(p, MouseButton.Left); f.Window.MouseUp(p, MouseButton.Left);
        NativeEditingPathTests.Drain(); Assert.True(f.Editor.IsFocused); Assert.False(f.Keyboard.IsVisible);
        Touch(f, f.Editor); Assert.True(f.Keyboard.IsVisible);
    }
    [AvaloniaFact]
    public void Touch_on_new_editor_opens_but_old_touch_cannot_authorize_refocus()
    {
        using var f = new KeyboardFixture(); using var scope = Attach(f, VirtualKeyboardDisplayMode.OnTouch);
        Touch(f, f.Other, drain: false); Assert.True(f.Keyboard.IsVisible);
        f.Editor.Focus(); f.Other.Focus(); NativeEditingPathTests.Drain(); Assert.False(f.Keyboard.IsVisible);
        Touch(f, f.Editor); Assert.Same(f.Editor, f.Keyboard.Target);
    }
    [AvaloniaFact]
    public void Secondary_touch_does_not_open()
    {
        using var f = new KeyboardFixture(); using var scope = Attach(f, VirtualKeyboardDisplayMode.OnTouch);
        Touch(f, f.Editor, primary: false); Assert.False(f.Keyboard.IsVisible);
    }
    [AvaloniaFact]
    public void Focused_readonly_and_optout_editors_recover_without_refocus()
    {
        using var f = new KeyboardFixture(); using var scope = Attach(f);
        f.Editor.IsReadOnly = true; Assert.Null(f.Keyboard.Target);
        f.Editor.IsReadOnly = false; Assert.Same(f.Editor, f.Keyboard.Target);
        VirtualKeyboardScope.SetIsEnabled(f.Editor, false); Assert.Null(f.Keyboard.Target);
        VirtualKeyboardScope.SetIsEnabled(f.Editor, true); Assert.Same(f.Editor, f.Keyboard.Target);
    }
    [AvaloniaFact]
    public void Invalid_ancestors_and_keyboard_disable_cancel_target()
    {
        using var f = new KeyboardFixture(); using var scope = Attach(f);
        f.EditorHost.IsVisible = false; Assert.Null(f.Keyboard.Target);
        f.EditorHost.IsVisible = true; f.Editor.Focus(); NativeEditingPathTests.Drain();
        f.Keyboard.IsEnabled = false; Assert.Null(f.Keyboard.Target);
        f.Keyboard.IsEnabled = true; Assert.Same(f.Editor, f.Keyboard.Target);
    }
    [AvaloniaFact]
    public void Purpose_maps_layout_without_reading_or_modifying_password()
    {
        using var f = new KeyboardFixture("secret"); f.Editor.PasswordChar = '*'; using var scope = Attach(f);
        foreach (var (purpose, layout) in new[] {
            (VirtualKeyboardInputPurpose.Text, VirtualKeyboardLayout.English),
            (VirtualKeyboardInputPurpose.Integer, VirtualKeyboardLayout.Numeric),
            (VirtualKeyboardInputPurpose.Decimal, VirtualKeyboardLayout.Numeric),
            (VirtualKeyboardInputPurpose.Email, VirtualKeyboardLayout.Email),
            (VirtualKeyboardInputPurpose.Url, VirtualKeyboardLayout.Url),
            (VirtualKeyboardInputPurpose.Phone, VirtualKeyboardLayout.Phone) })
        {
            VirtualKeyboardScope.SetInputPurpose(f.Editor, purpose);
            Assert.Equal(layout, f.Keyboard.Layout); Assert.Equal("secret", f.Editor.Text); Assert.Equal('*', f.Editor.PasswordChar);
            if (purpose == VirtualKeyboardInputPurpose.Integer) Assert.False(f.Keyboard.ShowDecimalPoint);
            if (purpose == VirtualKeyboardInputPurpose.Decimal) Assert.True(f.Keyboard.ShowDecimalPoint);
        }
        VirtualKeyboardScope.SetInputPurpose(f.Editor, VirtualKeyboardInputPurpose.Default);
        Assert.Equal(VirtualKeyboardLayout.English, f.Keyboard.Layout);
    }
    [AvaloniaFact]
    public void Explicit_target_and_duplicate_scope_are_rejected_and_host_takeover_wins()
    {
        using var f = new KeyboardFixture();
        Assert.Throws<InvalidOperationException>(() => new VirtualKeyboardScope(f.Window, f.Keyboard));
        using var scope = Attach(f);
        Assert.Throws<InvalidOperationException>(() => new VirtualKeyboardScope(f.Window, f.Keyboard));
        f.Keyboard.Target = f.Other; Assert.Same(f.Other, f.Keyboard.Target);
        Assert.Throws<ObjectDisposedException>(() => scope.Open());
        f.Keyboard.Target = null; using var next = new VirtualKeyboardScope(f.Window, f.Keyboard);
    }
    [AvaloniaFact]
    public void Target_switch_cancels_old_pointer_release()
    {
        using var f = new KeyboardFixture("abc"); using var scope = Attach(f);
        var button = f.Button("q"); using var pointer = f.Press("q"); f.Other.Focus();
        f.Release(button, pointer); NativeEditingPathTests.Drain();
        Assert.Equal("abc", f.Editor.Text); Assert.Equal("other", f.Other.Text);
    }
    [AvaloniaFact]
    public void Noneditor_focus_hides_and_unload_disposes_pending_work()
    {
        using var f = new KeyboardFixture(); using var scope = Attach(f);
        var button = new Button { Content = "Other" }; f.Root.Children.Add(button); button.Focus();
        NativeEditingPathTests.Drain(); Assert.False(scope.Open()); Assert.Null(f.Keyboard.Target);
        Touch(f, f.Editor, drain: false); f.KeyboardHost.Child = null; NativeEditingPathTests.Drain();
        Assert.Null(f.Keyboard.Target); Assert.Throws<ObjectDisposedException>(() => scope.Open());
    }
    [AvaloniaFact]
    public void Independent_windows_do_not_share_targets()
    {
        using var a = new KeyboardFixture("a"); using var sa = Attach(a);
        using var b = new KeyboardFixture("b"); using var sb = Attach(b);
        NativeEditingPathTests.Drain(); Assert.Null(a.Keyboard.Target); Assert.Same(b.Editor, b.Keyboard.Target);
        b.Tap("q"); Assert.Equal("a", a.Editor.Text); Assert.Equal("bq", b.Editor.Text);
        a.Window.Activate(); a.Editor.Focus(); NativeEditingPathTests.Drain(); Assert.Null(b.Keyboard.Target);
    }
    [AvaloniaFact]
    public void Reentrant_focus_change_is_reconciled()
    {
        using var f = new KeyboardFixture(); using var scope = Attach(f, VirtualKeyboardDisplayMode.Manual);
        VirtualKeyboardScope.SetInputPurpose(f.Other, VirtualKeyboardInputPurpose.Integer);
        var once = false;
        f.Keyboard.PropertyChanged += (_, e) => { if (e.Property == Keyboard.TargetProperty && f.Keyboard.Target == f.Editor && !once) { once = true; f.Other.Focus(); } };
        scope.DisplayMode = VirtualKeyboardDisplayMode.OnFocus; NativeEditingPathTests.Drain();
        Assert.Same(f.Other, f.Keyboard.Target); Assert.Equal(VirtualKeyboardLayout.Numeric, f.Keyboard.Layout);
    }
    [AvaloniaFact]
    public void Reentrant_target_takeover_is_preserved()
    {
        using var f = new KeyboardFixture(); using var scope = Attach(f, VirtualKeyboardDisplayMode.Manual);
        f.Keyboard.PropertyChanged += (_, e) => { if (e.Property == Keyboard.TargetProperty && f.Keyboard.Target == f.Editor) f.Keyboard.Target = f.Other; };
        scope.Open(); Assert.Same(f.Other, f.Keyboard.Target); Assert.Throws<ObjectDisposedException>(() => scope.Open());
    }
    [AvaloniaFact]
    public void Reentrant_disposal_during_target_update_does_not_reopen()
    {
        using var f = new KeyboardFixture(); using var scope = Attach(f, VirtualKeyboardDisplayMode.Manual);
        f.Keyboard.PropertyChanged += (_, e) => { if (e.Property == Keyboard.TargetProperty && f.Keyboard.Target == f.Editor) scope.Dispose(); };
        Assert.False(scope.Open()); NativeEditingPathTests.Drain(); Assert.Null(f.Keyboard.Target); Assert.False(f.Keyboard.IsVisible);
    }
    [AvaloniaFact]
    public void Reentrant_hide_or_disable_is_reconciled()
    {
        foreach (var hide in new[] { true, false })
        {
            using var f = new KeyboardFixture(); using var scope = Attach(f, VirtualKeyboardDisplayMode.Manual); var once = false;
            f.Keyboard.PropertyChanged += (_, e) => { if (e.Property == Visual.IsVisibleProperty && f.Keyboard.IsVisible && !once) { once = true; if (hide) f.Keyboard.IsVisible = false; else f.Keyboard.IsEnabled = false; } };
            scope.Open(); NativeEditingPathTests.Drain(); Assert.False(f.Keyboard.IsVisible); Assert.Null(f.Keyboard.Target);
        }
    }
    [AvaloniaFact]
    public void Dispose_is_idempotent_and_releases_ownership()
    {
        using var f = new KeyboardFixture(); f.Keyboard.Target = null;
        for (var i = 0; i < 3; i++)
        {
            var scope = new VirtualKeyboardScope(f.Window, f.Keyboard); Assert.True(scope.Open());
            scope.Dispose(); scope.Dispose(); NativeEditingPathTests.Drain(); Assert.Null(f.Keyboard.Target);
        }
    }
    // Synthetic routed touches test the scope policy, not an OS touchscreen driver.
    private static void Touch(KeyboardFixture f, TextBox editor, bool primary = true, bool drain = true)
    {
        f.Window.UpdateLayout(); using var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, primary);
        editor.RaiseEvent(new PointerPressedEventArgs(editor, pointer, f.Window, f.Point(editor), 0, default, KeyModifiers.None));
        editor.Focus(); if (drain) NativeEditingPathTests.Drain();
    }
}
