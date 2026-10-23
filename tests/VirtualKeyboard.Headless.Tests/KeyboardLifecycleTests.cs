using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.VirtualKeyboard;

namespace VirtualKeyboard.Headless.Tests;

public sealed class KeyboardLifecycleTests
{
    [AvaloniaFact]
    public void Repeat_has_one_initial_action_controlled_ticks_and_no_release_append()
    {
        using var fixture = new KeyboardFixture("abcdefgh");
        using var pointer = fixture.Press("Backspace");
        var now = fixture.Keyboard.RepeatNow;
        Assert.Equal("abcdefg", fixture.Editor.Text);
        fixture.Keyboard.AdvanceRepeat(now + fixture.Keyboard.RepeatDelay);
        Assert.Equal("abcdef", fixture.Editor.Text);
        fixture.Keyboard.AdvanceRepeat(now + fixture.Keyboard.RepeatDelay + fixture.Keyboard.RepeatInterval);
        Assert.Equal("abcde", fixture.Editor.Text);
        fixture.Release("Backspace", pointer);
        Assert.Equal("abcde", fixture.Editor.Text);
        fixture.Keyboard.AdvanceRepeat(now + TimeSpan.FromSeconds(5));
        Assert.Equal("abcde", fixture.Editor.Text);
        Assert.Null(pointer.Captured);
    }

    [AvaloniaFact]
    public void Repeat_stops_on_move_out_and_capture_loss()
    {
        foreach (var loseCapture in new[] { false, true })
        {
            using var fixture = new KeyboardFixture("abcdefgh");
            using var pointer = fixture.Press("Backspace");
            var now = fixture.Keyboard.RepeatNow;
            if (loseCapture) pointer.Capture(null);
            else fixture.MoveOut("Backspace", pointer);
            fixture.Keyboard.AdvanceRepeat(now + TimeSpan.FromSeconds(1));
            fixture.Release("Backspace", pointer);
            Assert.Equal("abcdefg", fixture.Editor.Text);
            Assert.Null(pointer.Captured);
        }
    }

    [AvaloniaFact]
    public void Hiding_or_disabling_a_custom_key_container_cancels_repeat_and_does_not_resume()
    {
        foreach (var hide in new[] { true, false })
        {
            using var fixture = new KeyboardFixture("abcdefgh");
            var button = fixture.Button("Backspace");
            var container = Assert.IsAssignableFrom<Control>(button.GetVisualParent());
            using var pointer = fixture.Press("Backspace");
            var now = fixture.Keyboard.RepeatNow;
            if (hide) container.IsVisible = false;
            else container.IsEnabled = false;
            Assert.Null(pointer.Captured);
            Assert.False(button.IsInvocationAvailable);
            fixture.Keyboard.AdvanceRepeat(now + TimeSpan.FromSeconds(1));
            container.IsVisible = container.IsEnabled = true;
            fixture.Keyboard.AdvanceRepeat(now + TimeSpan.FromSeconds(2));
            fixture.Release(button, pointer);
            Assert.Equal("abcdefg", fixture.Editor.Text);
            fixture.Tap("Backspace");
            Assert.Equal("abcdef", fixture.Editor.Text);
        }
    }

    [AvaloniaFact]
    public void Target_layout_options_and_repeat_settings_invalidate_old_repeat_sessions()
    {
        foreach (var change in new[] { "target", "layout", "decimal", "negative", "delay", "interval", "host" })
        {
            using var fixture = new KeyboardFixture("abcdefgh");
            using var pointer = fixture.Press("Backspace");
            var button = fixture.Button("Backspace");
            var now = fixture.Keyboard.RepeatNow;
            switch (change)
            {
                case "target": fixture.Other.Focus(); fixture.Keyboard.Target = fixture.Other; break;
                case "layout": fixture.Keyboard.Layout = VirtualKeyboardLayout.Numeric; break;
                case "decimal": fixture.Keyboard.ShowDecimalPoint = false; break;
                case "negative": fixture.Keyboard.ShowNegativeSign = false; break;
                case "delay": fixture.Keyboard.RepeatDelay = TimeSpan.FromSeconds(1); break;
                case "interval": fixture.Keyboard.RepeatInterval = TimeSpan.FromMilliseconds(100); break;
                case "host": fixture.Keyboard.CancelInput(); break;
            }
            fixture.Keyboard.AdvanceRepeat(now + TimeSpan.FromSeconds(5));
            fixture.Release(button, pointer);
            Assert.Equal("abcdefg", fixture.Editor.Text);
            Assert.Equal("other", fixture.Other.Text);
            Assert.Null(pointer.Captured);
        }
    }

    [AvaloniaFact]
    public void Hidden_disabled_read_only_and_unloaded_lifetimes_cancel_and_never_restore_an_old_press()
    {
        foreach (var condition in new[]
        {
            "keyboard-hidden", "keyboard-disabled", "keyboard-ancestor-hidden", "keyboard-ancestor-disabled",
            "target-hidden", "target-disabled", "target-ancestor-hidden", "target-ancestor-disabled",
            "target-read-only", "focus", "keyboard-detached", "target-detached", "window-hidden",
        })
        {
            using var fixture = new KeyboardFixture("abcdefgh");
            var button = fixture.Button("Backspace");
            using var pointer = fixture.Press("Backspace");
            var now = fixture.Keyboard.RepeatNow;
            switch (condition)
            {
                case "keyboard-hidden": fixture.Keyboard.IsVisible = false; break;
                case "keyboard-disabled": fixture.Keyboard.IsEnabled = false; break;
                case "keyboard-ancestor-hidden": fixture.KeyboardHost.IsVisible = false; break;
                case "keyboard-ancestor-disabled": fixture.KeyboardHost.IsEnabled = false; break;
                case "target-hidden": fixture.Editor.IsVisible = false; break;
                case "target-disabled": fixture.Editor.IsEnabled = false; break;
                case "target-ancestor-hidden": fixture.EditorHost.IsVisible = false; break;
                case "target-ancestor-disabled": fixture.EditorHost.IsEnabled = false; break;
                case "target-read-only": fixture.Editor.IsReadOnly = true; break;
                case "focus": fixture.Other.Focus(); break;
                case "keyboard-detached": fixture.KeyboardHost.Child = null; break;
                case "target-detached": fixture.EditorHost.Children.Remove(fixture.Editor); break;
                case "window-hidden": fixture.Window.Hide(); NativeEditingPathTests.Drain(); break;
            }
            Assert.False(fixture.Keyboard.IsInputAvailable);
            Assert.Null(pointer.Captured);
            fixture.Keyboard.AdvanceRepeat(now + TimeSpan.FromSeconds(5));
            Assert.Equal("abcdefg", fixture.Editor.Text);

            fixture.Keyboard.IsVisible = fixture.Keyboard.IsEnabled = true;
            fixture.KeyboardHost.IsVisible = fixture.KeyboardHost.IsEnabled = true;
            fixture.Editor.IsVisible = fixture.Editor.IsEnabled = true;
            fixture.EditorHost.IsVisible = fixture.EditorHost.IsEnabled = true;
            fixture.Editor.IsReadOnly = false;
            if (condition == "keyboard-detached") fixture.KeyboardHost.Child = fixture.Keyboard;
            if (condition == "target-detached") fixture.EditorHost.Children.Insert(0, fixture.Editor);
            if (condition == "window-hidden") fixture.Window.Show();
            fixture.Window.UpdateLayout();
            NativeEditingPathTests.Drain();
            fixture.Editor.Focus();
            NativeEditingPathTests.Drain();
            Assert.True(fixture.Keyboard.IsInputAvailable);
            fixture.Keyboard.AdvanceRepeat(now + TimeSpan.FromSeconds(10));
            fixture.Release(button, pointer);
            Assert.Equal("abcdefg", fixture.Editor.Text);
            fixture.Tap("Backspace");
            Assert.Equal("abcdef", fixture.Editor.Text);
        }
    }

    [AvaloniaFact]
    public void Replacing_a_template_keeps_modifier_state_but_old_button_cannot_invoke_or_repeat()
    {
        using var fixture = new KeyboardFixture("abcdefgh");
        fixture.Tap("CapsLock");
        fixture.Tap("Shift");
        var oldButton = fixture.Button("Backspace");
        using var pointer = fixture.Press("Backspace");
        var now = fixture.Keyboard.RepeatNow;
        fixture.Keyboard.Template = KeyboardFixture.CreateTemplate();
        fixture.Window.UpdateLayout();
        Assert.True(fixture.Keyboard.IsCapsLockActive);
        Assert.True(fixture.Keyboard.IsShiftActive);
        Assert.False(oldButton.Invoke());
        fixture.Keyboard.AdvanceRepeat(now + TimeSpan.FromSeconds(1));
        fixture.Release(oldButton, pointer);
        Assert.Equal("abcdefg", fixture.Editor.Text);
        fixture.Tap("q");
        Assert.Equal("abcdefgq", fixture.Editor.Text);
        Assert.False(fixture.Keyboard.IsShiftActive);
    }

    [AvaloniaFact]
    public void Reentrant_target_switch_during_text_change_cancels_the_old_session_and_resets_modifiers()
    {
        using var fixture = new KeyboardFixture("a");
        fixture.Tap("Shift");
        fixture.Editor.PropertyChanged += (_, e) =>
        {
            if (e.Property != TextBox.TextProperty) return;
            fixture.Other.Focus();
            fixture.Keyboard.Target = fixture.Other;
        };
        fixture.Tap("q");
        Assert.Equal("aQ", fixture.Editor.Text);
        Assert.False(fixture.Keyboard.IsShiftActive);
        Assert.Same(fixture.Other, fixture.Keyboard.Target);
        fixture.Other.CaretIndex = fixture.Other.Text!.Length;
        fixture.Tap("w");
        Assert.Equal("otherw", fixture.Other.Text);
    }

    [AvaloniaFact]
    public void Switching_control_themes_with_one_shared_template_cancels_repeat_and_retains_availability()
    {
        using var fixture = new KeyboardFixture("abcdefgh");
        var template = KeyboardFixture.CreateTemplate();
        var first = new ControlTheme(typeof(NT.AvaloniaControl.VirtualKeyboard.VirtualKeyboard))
        {
            Setters = { new Setter(TemplatedControl.TemplateProperty, template) },
        };
        var second = new ControlTheme(typeof(NT.AvaloniaControl.VirtualKeyboard.VirtualKeyboard))
        {
            Setters = { new Setter(TemplatedControl.TemplateProperty, template) },
        };
        fixture.Keyboard.ClearValue(TemplatedControl.TemplateProperty);
        fixture.Keyboard.Theme = first;
        fixture.Window.UpdateLayout();
        Assert.True(fixture.Keyboard.IsInputAvailable);
        fixture.Tap("CapsLock");
        fixture.Tap("Shift");
        var button = fixture.Button("Backspace");
        using var pointer = fixture.Press("Backspace");
        var now = fixture.Keyboard.RepeatNow;
        fixture.Keyboard.Theme = second;
        fixture.Window.UpdateLayout();
        Assert.Null(pointer.Captured);
        Assert.True(fixture.Keyboard.IsInputAvailable);
        Assert.True(fixture.Keyboard.IsCapsLockActive);
        Assert.True(fixture.Keyboard.IsShiftActive);
        fixture.Keyboard.AdvanceRepeat(now + TimeSpan.FromSeconds(1));
        fixture.Release(button, pointer);
        Assert.Equal("abcdefg", fixture.Editor.Text);
        fixture.Tap("q");
        Assert.Equal("abcdefgq", fixture.Editor.Text);
    }
}
