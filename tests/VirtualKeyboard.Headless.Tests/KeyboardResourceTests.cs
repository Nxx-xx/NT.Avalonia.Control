using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using NT.AvaloniaControl.VirtualKeyboard;
using Keyboard = NT.AvaloniaControl.VirtualKeyboard.VirtualKeyboard;

namespace VirtualKeyboard.Headless.Tests;

public sealed class KeyboardResourceTests
{
    [AvaloniaFact]
    public void Switching_target_does_not_retain_the_old_editor_or_its_ancestor()
    {
        using var fixture = new KeyboardFixture();
        var references = ReplaceAndRemoveOldTarget(fixture);
        Collect();
        Assert.False(references.Editor.IsAlive);
        Assert.False(references.Ancestor.IsAlive);
        fixture.Tap("q");
        Assert.Equal("q", fixture.Editor.Text);
    }

    [AvaloniaFact]
    public void Removing_a_native_editor_without_a_keyboard_target_is_collectable()
    {
        using var fixture = new KeyboardFixture();
        var references = ReplaceAndRemoveOldTarget(fixture, useKeyboard: false);
        Collect();
        Assert.False(references.Editor.IsAlive);
        Assert.False(references.Ancestor.IsAlive);
    }

    [AvaloniaFact]
    public async Task An_unloaded_keyboard_can_be_collected_while_its_form_and_editor_remain_alive()
    {
        using var fixture = new KeyboardFixture("seed");
        var reference = CreateAndUnloadKeyboard(fixture);
        // Avalonia's global Gestures.PointerPressed RunOnce closure keeps the
        // press source through HoldWaitDuration, including for a removed native
        // Border (the separate baseline below). This is not our repeat scheduler.
        await DrainNativeHoldingTimer();
        Collect();
        Assert.False(reference.IsAlive);
        Assert.True(fixture.Editor.IsFocused);
        fixture.Editor.CaretIndex = fixture.Editor.Text!.Length;
        fixture.Tap("q");
        Assert.Equal("seedq", fixture.Editor.Text);
    }

    [AvaloniaFact]
    public async Task Native_pointer_down_also_retains_a_detached_source_until_its_holding_timer_expires()
    {
        using var fixture = new KeyboardFixture();
        var reference = CreateAndRemoveNativePointerSource(fixture);
        Collect();
        Assert.True(reference.IsAlive);
        await DrainNativeHoldingTimer();
        Collect();
        Assert.False(reference.IsAlive);
    }

    [AvaloniaFact]
    public void Repeated_target_switches_and_attach_detach_do_not_duplicate_host_requests_or_text()
    {
        using var fixture = new KeyboardFixture();
        var returns = 0;
        fixture.Keyboard.ReturnRequested += (_, _) => returns++;
        for (var iteration = 0; iteration < 8; iteration++)
        {
            fixture.Other.Focus();
            fixture.Keyboard.Target = fixture.Other;
            fixture.Editor.Focus();
            fixture.Keyboard.Target = fixture.Editor;
            fixture.KeyboardHost.Child = null;
            fixture.KeyboardHost.Child = fixture.Keyboard;
            fixture.Window.UpdateLayout();
            fixture.Editor.Focus();
            fixture.Tap("Return");
            fixture.Tap("q");
        }
        Assert.Equal(8, returns);
        Assert.Equal("qqqqqqqq", fixture.Editor.Text);
    }

    [AvaloniaFact]
    public void Moving_keyboard_to_a_new_window_disconnects_the_old_window_lifetime()
    {
        using var fixture = new KeyboardFixture("old");
        fixture.Root.Children.Remove(fixture.KeyboardHost);
        var editor = new TextBox { Text = "abcdefgh" };
        var second = new Window
        {
            Width = 1040, Height = 400,
            Content = new StackPanel { Children = { editor, fixture.KeyboardHost } },
        };
        try
        {
            second.Show();
            second.UpdateLayout();
            NativeEditingPathTests.Drain();
            editor.Focus();
            editor.CaretIndex = 8;
            fixture.Keyboard.Target = editor;
            Assert.True(fixture.Keyboard.IsInputAvailable);
            var button = fixture.Button("Backspace");
            using var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
            var point = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), second)!.Value;
            button.RaiseEvent(new PointerPressedEventArgs(button, pointer, second, point, 0, default, KeyModifiers.None));
            var now = fixture.Keyboard.RepeatNow;
            Assert.Equal("abcdefg", editor.Text);
            fixture.Window.Hide();
            NativeEditingPathTests.Drain();
            fixture.Keyboard.AdvanceRepeat(now + TimeSpan.FromSeconds(1));
            Assert.Equal("abcdef", editor.Text);
            Assert.Equal("old", fixture.Editor.Text);
            button.RaiseEvent(new PointerReleasedEventArgs(button, pointer, second, point, 1, default, KeyModifiers.None, MouseButton.Left));
            Assert.Equal("abcdef", editor.Text);
        }
        finally { second.Close(); }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Editor, WeakReference Ancestor) ReplaceAndRemoveOldTarget(KeyboardFixture fixture, bool useKeyboard = true)
    {
        var editor = new TextBox { Text = "old" };
        var ancestor = new Border { Child = editor };
        fixture.EditorHost.Children.Add(ancestor);
        fixture.Window.UpdateLayout();
        editor.Focus();
        if (useKeyboard)
        {
            fixture.Keyboard.Target = editor;
            Assert.True(fixture.Keyboard.IsInputAvailable);
            using var pointer = fixture.Press("Backspace");
            fixture.Keyboard.Target = fixture.Editor;
        }
        fixture.Editor.Focus();
        fixture.EditorHost.Children.Remove(ancestor);
        fixture.Window.UpdateLayout();
        NativeEditingPathTests.Drain();
        // The compositor's previous scene can retain removed native visuals until
        // the next rendering tick. Drain it before testing keyboard subscription ownership.
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        NativeEditingPathTests.Drain();
        return (new WeakReference(editor), new WeakReference(ancestor));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateAndUnloadKeyboard(KeyboardFixture fixture)
    {
        var keyboard = new Keyboard { Target = fixture.Editor, Template = KeyboardFixture.CreateTemplate() };
        var host = new Border { Child = keyboard };
        fixture.Root.Children.Add(host);
        fixture.Window.UpdateLayout();
        fixture.Editor.Focus();
        Assert.True(keyboard.IsInputAvailable);
        var button = keyboard.GetVisualDescendants().OfType<VirtualKeyboardKeyButton>().Single(key => key.Key?.Id == "Left");
        using var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        var point = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), fixture.Window)!.Value;
        button.RaiseEvent(new PointerPressedEventArgs(button, pointer, fixture.Window, point, 0, default, KeyModifiers.None));
        host.Child = null;
        fixture.Root.Children.Remove(host);
        fixture.Window.UpdateLayout();
        NativeEditingPathTests.Drain();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        NativeEditingPathTests.Drain();
        Assert.False(keyboard.IsInputAvailable);
        return new WeakReference(keyboard);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateAndRemoveNativePointerSource(KeyboardFixture fixture)
    {
        var source = new Border { Width = 50, Height = 20 };
        fixture.Root.Children.Add(source);
        fixture.Window.UpdateLayout();
        using var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        var point = source.TranslatePoint(new Point(10, 10), fixture.Window)!.Value;
        source.RaiseEvent(new PointerPressedEventArgs(source, pointer, fixture.Window, point, 0, default, KeyModifiers.None));
        fixture.Root.Children.Remove(source);
        fixture.Window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        return new WeakReference(source);
    }

    private static async Task DrainNativeHoldingTimer()
    {
        await Task.Delay(Application.Current!.PlatformSettings!.HoldWaitDuration + TimeSpan.FromMilliseconds(50));
        NativeEditingPathTests.Drain();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        NativeEditingPathTests.Drain();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Collect()
    {
        for (var count = 0; count < 3; count++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
