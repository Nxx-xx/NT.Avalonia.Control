using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using NT.AvaloniaControl.VirtualKeyboard;
using Keyboard = NT.AvaloniaControl.VirtualKeyboard.VirtualKeyboard;

namespace VirtualKeyboard.Headless.Tests;

public sealed class KeyboardSecurityTests
{
    [AvaloniaFact]
    public void Password_never_appears_in_keyboard_labels_automation_or_template_diagnostics()
    {
        const string secret = "private-password-sentinel-7341";
        using var fixture = new KeyboardFixture(secret);
        fixture.Editor.PasswordChar = '●';
        fixture.Keyboard.Target = null;
        using var scope = new VirtualKeyboardScope(fixture.Window, fixture.Keyboard);
        foreach (var purpose in Enum.GetValues<VirtualKeyboardInputPurpose>())
        {
            VirtualKeyboardScope.SetInputPurpose(fixture.Editor, purpose);
            fixture.Window.UpdateLayout();
            Assert.Equal(secret, fixture.Editor.Text);
            foreach (var button in fixture.Keyboard.GetVisualDescendants().OfType<VirtualKeyboardKeyButton>())
            {
                Assert.DoesNotContain(secret, button.Label ?? "");
                Assert.DoesNotContain(secret, ControlAutomationPeer.CreatePeerForElement(button)!.GetName() ?? "");
            }
            Assert.DoesNotContain(secret, fixture.Keyboard.TemplateDiagnostic ?? "");
        }
        fixture.Keyboard.Template = new FuncControlTemplate<Keyboard>((_, _) => new Border());
        fixture.Window.UpdateLayout();
        Assert.Contains("PART_RowsPresenter", fixture.Keyboard.TemplateDiagnostic);
        Assert.DoesNotContain(secret, fixture.Keyboard.TemplateDiagnostic!);
        Assert.Equal(secret, fixture.Editor.Text);
    }

    [AvaloniaFact]
    public void Closing_scope_releases_password_target_and_pending_character_without_erasing_editor()
    {
        const string secret = "private-password-sentinel-7341";
        using var fixture = new KeyboardFixture(secret);
        fixture.Editor.PasswordChar = '*';
        fixture.Keyboard.Target = null;
        using var scope = new VirtualKeyboardScope(fixture.Window, fixture.Keyboard);
        var button = fixture.Button("q");
        using var pointer = fixture.Press("q");
        scope.Dispose();
        Assert.Null(pointer.Captured);
        fixture.Release(button, pointer);
        fixture.Keyboard.AdvanceRepeat(fixture.Keyboard.RepeatNow + TimeSpan.FromSeconds(5));
        Assert.Null(fixture.Keyboard.Target);
        Assert.Equal(secret, fixture.Editor.Text);
        Assert.False(fixture.Keyboard.IsInputAvailable);
    }

    [AvaloniaFact]
    public void Window_close_cancels_password_repeat_and_releases_target()
    {
        using var fixture = new KeyboardFixture("abcdef");
        fixture.Editor.PasswordChar = '*';
        fixture.Keyboard.Target = null;
        using var scope = new VirtualKeyboardScope(fixture.Window, fixture.Keyboard);
        var button = fixture.Button("Backspace");
        using var pointer = fixture.Press("Backspace");
        Assert.Equal("abcde", fixture.Editor.Text);
        var now = fixture.Keyboard.RepeatNow;
        fixture.Window.Close();
        NativeEditingPathTests.Drain();
        fixture.Keyboard.AdvanceRepeat(now + TimeSpan.FromSeconds(5));
        fixture.Release(button, pointer);
        Assert.Null(pointer.Captured);
        Assert.Null(fixture.Keyboard.Target);
        Assert.Equal("abcde", fixture.Editor.Text);
        Assert.Throws<ObjectDisposedException>(() => scope.Open());
    }

    [AvaloniaFact]
    public void Modal_dialog_stops_owner_repeat_and_old_press_never_resumes()
    {
        using var fixture = new KeyboardFixture("abcdef");
        fixture.Keyboard.Target = null;
        using var scope = new VirtualKeyboardScope(fixture.Window, fixture.Keyboard);
        var button = fixture.Button("Backspace");
        using var pointer = fixture.Press("Backspace");
        var now = fixture.Keyboard.RepeatNow;
        var editor = new TextBox { Text = "dialog" };
        var dialog = new Window { Width = 300, Height = 200, Content = editor };
        try
        {
            var completion = dialog.ShowDialog(fixture.Window);
            dialog.UpdateLayout();
            editor.Focus();
            NativeEditingPathTests.Drain();
            Assert.False(fixture.Keyboard.IsInputAvailable);
            Assert.Null(pointer.Captured);
            fixture.Keyboard.AdvanceRepeat(now + TimeSpan.FromSeconds(5));
            Assert.Equal("abcde", fixture.Editor.Text);
            Assert.Equal("dialog", editor.Text);
            dialog.Close();
            NativeEditingPathTests.Drain();
            Assert.True(completion.IsCompleted);
            fixture.Window.Activate();
            fixture.Editor.Focus();
            NativeEditingPathTests.Drain();
            fixture.Keyboard.AdvanceRepeat(now + TimeSpan.FromSeconds(10));
            fixture.Release(button, pointer);
            Assert.Equal("abcde", fixture.Editor.Text);
            Assert.Equal("dialog", editor.Text);
        }
        finally { dialog.Close(); }
    }
}
