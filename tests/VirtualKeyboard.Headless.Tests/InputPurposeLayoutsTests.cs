using Avalonia.Headless.XUnit;
using NT.AvaloniaControl.VirtualKeyboard;

namespace VirtualKeyboard.Headless.Tests;

public sealed class InputPurposeLayoutsTests
{
    [AvaloniaFact]
    public void Convenience_purposes_change_layout_without_normalizing_existing_text()
    {
        using var fixture = new KeyboardFixture("not a valid address / +()");
        fixture.Keyboard.Target = null;
        using var scope = new VirtualKeyboardScope(fixture.Window, fixture.Keyboard);
        var original = fixture.Editor.Text;
        foreach (var (purpose, layout) in new[]
        {
            (VirtualKeyboardInputPurpose.Email, VirtualKeyboardLayout.Email),
            (VirtualKeyboardInputPurpose.Url, VirtualKeyboardLayout.Url),
            (VirtualKeyboardInputPurpose.Phone, VirtualKeyboardLayout.Phone),
            (VirtualKeyboardInputPurpose.Text, VirtualKeyboardLayout.English),
        })
        {
            VirtualKeyboardScope.SetInputPurpose(fixture.Editor, purpose);
            Assert.Equal(layout, fixture.Keyboard.Layout);
            Assert.Equal(original, fixture.Editor.Text);
            Assert.True(fixture.Editor.IsFocused);
        }
    }

    [AvaloniaFact]
    public void Convenience_strings_use_native_selection_maxlength_and_undo()
    {
        using var fixture = new KeyboardFixture("abcd");
        fixture.Keyboard.Target = null;
        using var scope = new VirtualKeyboardScope(fixture.Window, fixture.Keyboard);
        VirtualKeyboardScope.SetInputPurpose(fixture.Editor, VirtualKeyboardInputPurpose.Url);
        fixture.Editor.SelectionStart = 1;
        fixture.Editor.SelectionEnd = 3;
        fixture.Tap("UrlScheme");
        Assert.Equal("ahttps://d", fixture.Editor.Text);
        fixture.Editor.Undo();
        Assert.Equal("abcd", fixture.Editor.Text);
        fixture.Editor.MaxLength = 5;
        fixture.Editor.SelectionStart = fixture.Editor.SelectionEnd = fixture.Editor.CaretIndex = 4;
        fixture.Tap("UrlScheme");
        Assert.Equal("abcdh", fixture.Editor.Text);
    }

    [AvaloniaFact]
    public void Switching_convenience_layout_cancels_old_press_and_preserves_password_mask()
    {
        using var fixture = new KeyboardFixture("seed");
        fixture.Editor.PasswordChar = '*';
        fixture.Keyboard.Target = null;
        using var scope = new VirtualKeyboardScope(fixture.Window, fixture.Keyboard);
        VirtualKeyboardScope.SetInputPurpose(fixture.Editor, VirtualKeyboardInputPurpose.Email);
        var oldButton = fixture.Button("EmailAt");
        using var pointer = fixture.Press("EmailAt");
        VirtualKeyboardScope.SetInputPurpose(fixture.Editor, VirtualKeyboardInputPurpose.Phone);
        fixture.Release(oldButton, pointer);
        Assert.Equal("seed", fixture.Editor.Text);
        Assert.Equal('*', fixture.Editor.PasswordChar);
        fixture.Editor.CaretIndex = fixture.Editor.Text!.Length;
        fixture.Tap("+");
        Assert.Equal("seed+", fixture.Editor.Text);
    }
}
