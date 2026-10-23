using Avalonia.Headless.XUnit;
using Avalonia.Styling;

namespace VirtualKeyboard.Headless.Tests;

/// <summary>
/// Theme variants also invalidate input in the feature-only custom-template path.
/// They can change a host's dynamic resources without replacing its template.
/// </summary>
public sealed class KeyboardThemeVariantTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Window_theme_variant_switch_cancels_repeat_preserves_state_and_requires_a_new_press(bool startsDark)
    {
        using var fixture = new KeyboardFixture("abcdefgh");
        var initial = startsDark ? ThemeVariant.Dark : ThemeVariant.Light;
        var alternate = startsDark ? ThemeVariant.Light : ThemeVariant.Dark;
        fixture.Window.RequestedThemeVariant = initial;
        fixture.Window.UpdateLayout();
        NativeEditingPathTests.Drain();
        fixture.Tap("CapsLock");
        fixture.Tap("Shift");
        var template = fixture.Keyboard.Template;
        var target = fixture.Keyboard.Target;
        var layout = fixture.Keyboard.Layout;
        var oldButton = fixture.Button("Backspace");
        using var oldPointer = fixture.Press("Backspace");
        var oldTime = fixture.Keyboard.RepeatNow;
        fixture.Keyboard.AdvanceRepeat(oldTime + fixture.Keyboard.RepeatDelay);
        Assert.Equal("abcdef", fixture.Editor.Text);
        Assert.Same(oldButton, oldPointer.Captured);

        fixture.Window.RequestedThemeVariant = alternate;
        Assert.Null(oldPointer.Captured);
        fixture.Window.UpdateLayout();
        NativeEditingPathTests.Drain();
        Assert.Equal(alternate, fixture.Keyboard.ActualThemeVariant);
        Assert.Same(template, fixture.Keyboard.Template);
        Assert.Same(target, fixture.Keyboard.Target);
        Assert.Equal(layout, fixture.Keyboard.Layout);
        Assert.True(fixture.Keyboard.IsShiftActive);
        Assert.True(fixture.Keyboard.IsCapsLockActive);
        Assert.True(fixture.Keyboard.IsInputAvailable);
        Assert.True(fixture.Editor.IsFocused);
        fixture.Keyboard.AdvanceRepeat(oldTime + TimeSpan.FromSeconds(5));
        Assert.Equal("abcdef", fixture.Editor.Text);

        fixture.Window.RequestedThemeVariant = initial;
        fixture.Window.UpdateLayout();
        NativeEditingPathTests.Drain();
        fixture.Keyboard.AdvanceRepeat(oldTime + TimeSpan.FromSeconds(10));
        fixture.Release(oldButton, oldPointer);
        Assert.Equal("abcdef", fixture.Editor.Text);
        Assert.True(fixture.Keyboard.IsShiftActive);
        Assert.True(fixture.Keyboard.IsCapsLockActive);

        using var newPointer = fixture.Press("Backspace");
        var newTime = fixture.Keyboard.RepeatNow;
        Assert.Equal("abcde", fixture.Editor.Text);
        fixture.Keyboard.AdvanceRepeat(newTime + fixture.Keyboard.RepeatDelay);
        Assert.Equal("abcd", fixture.Editor.Text);
        fixture.Release("Backspace", newPointer);
        Assert.Equal("abcd", fixture.Editor.Text);
        fixture.Tap("q");
        Assert.Equal("abcdq", fixture.Editor.Text);
        Assert.False(fixture.Keyboard.IsShiftActive);
        Assert.True(fixture.Keyboard.IsCapsLockActive);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Window_theme_variant_switch_cancels_a_pending_character_without_consuming_shift(bool startsDark)
    {
        using var fixture = new KeyboardFixture("seed");
        var initial = startsDark ? ThemeVariant.Dark : ThemeVariant.Light;
        var alternate = startsDark ? ThemeVariant.Light : ThemeVariant.Dark;
        fixture.Window.RequestedThemeVariant = initial;
        fixture.Window.UpdateLayout();
        NativeEditingPathTests.Drain();
        fixture.Tap("Shift");
        var button = fixture.Button("q");
        using var pointer = fixture.Press("q");
        fixture.Window.RequestedThemeVariant = alternate;
        Assert.Null(pointer.Captured);
        fixture.Window.UpdateLayout();
        NativeEditingPathTests.Drain();
        fixture.Release(button, pointer);
        Assert.Equal("seed", fixture.Editor.Text);
        Assert.True(fixture.Keyboard.IsShiftActive);
        fixture.Tap("q");
        Assert.Equal("seedQ", fixture.Editor.Text);
        Assert.False(fixture.Keyboard.IsShiftActive);
    }
}
