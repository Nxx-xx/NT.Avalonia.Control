using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using NT.AvaloniaControl.VirtualKeyboard;
using NT.AvaloniaControl.VirtualKeyboard.Input;

namespace VirtualKeyboard.Headless.Tests;

public sealed class TextBoxInputAdapterTests
{
    [AvaloniaFact]
    public void Adapter_commits_a_string_through_the_native_edit_path()
    {
        using var fixture = new NativeEditingPathTests.EditorFixture("abcd");
        fixture.Editor.SelectionStart = 1;
        fixture.Editor.SelectionEnd = 3;
        Assert.True(TextBoxInputAdapter.CanInput(fixture.Editor, fixture.Window));
        Assert.Equal(VirtualKeyboardInputResult.Handled,
            TextBoxInputAdapter.TryCommitText(fixture.Editor, fixture.Window, "XYZ"));
        Assert.Equal("aXYZd", fixture.Editor.Text);
        fixture.Editor.Undo();
        Assert.Equal("abcd", fixture.Editor.Text);
        fixture.Editor.Redo();
        Assert.Equal("aXYZd", fixture.Editor.Text);
    }

    [AvaloniaFact]
    public void Adapter_routes_enter_and_tab_to_exactly_one_native_or_host_path()
    {
        using var fixture = new NativeEditingPathTests.EditorFixture("a");
        fixture.Editor.CaretIndex = 1;
        Assert.Equal(VirtualKeyboardInputResult.ReturnRequested, Execute(fixture, VirtualKeyboardKeyKind.Return));
        Assert.Equal(VirtualKeyboardInputResult.NextRequested, Execute(fixture, VirtualKeyboardKeyKind.Tab));
        Assert.Equal("a", fixture.Editor.Text);
        fixture.Editor.AcceptsReturn = true;
        fixture.Editor.AcceptsTab = true;
        Assert.Equal(VirtualKeyboardInputResult.Handled, Execute(fixture, VirtualKeyboardKeyKind.Return));
        Assert.Equal(VirtualKeyboardInputResult.Handled, Execute(fixture, VirtualKeyboardKeyKind.Tab));
        Assert.Equal("a" + fixture.Editor.NewLine + "\t", fixture.Editor.Text);
        Assert.True(fixture.Editor.IsFocused);
    }

    [AvaloniaFact]
    public void Adapter_rejects_unsupported_unfocused_read_only_disabled_and_hidden_targets()
    {
        using var fixture = new NativeEditingPathTests.EditorFixture("seed");
        Assert.False(TextBoxInputAdapter.CanInput(null, fixture.Window));
        Assert.False(TextBoxInputAdapter.CanInput(new Button(), fixture.Window));
        fixture.Editor.IsReadOnly = true;
        Reject(fixture);
        fixture.Editor.IsReadOnly = false;
        fixture.Editor.IsEnabled = false;
        Reject(fixture);
        fixture.Editor.IsEnabled = true;
        fixture.Editor.Focus();
        fixture.Editor.IsVisible = false;
        Reject(fixture);
        fixture.Editor.IsVisible = true;
        fixture.Editor.Focus();
        var other = new TextBox();
        fixture.Window.Content = null;
        fixture.Window.UpdateLayout();
        fixture.Window.Content = new StackPanel { Children = { fixture.Editor, other } };
        fixture.Window.UpdateLayout();
        other.Focus();
        Reject(fixture);
        Assert.Equal("seed", fixture.Editor.Text);
    }

    [AvaloniaFact]
    public void Adapter_revalidates_target_ancestor_state_and_attachment()
    {
        using var fixture = new NativeEditingPathTests.EditorFixture("seed");
        fixture.Window.Content = null;
        fixture.Window.UpdateLayout();
        var ancestor = new StackPanel { Children = { fixture.Editor } };
        fixture.Window.Content = ancestor;
        fixture.Window.UpdateLayout();
        fixture.Editor.Focus();
        Assert.True(TextBoxInputAdapter.CanInput(fixture.Editor, fixture.Window));
        ancestor.IsVisible = false;
        Reject(fixture);
        ancestor.IsVisible = true;
        ancestor.IsEnabled = false;
        Reject(fixture);
        ancestor.IsEnabled = true;
        fixture.Editor.Focus();
        ancestor.Children.Clear();
        Reject(fixture);
        Assert.Equal("seed", fixture.Editor.Text);
    }

    [AvaloniaFact]
    public void Adapter_rejects_another_window_and_an_inactive_window()
    {
        using var fixture = new NativeEditingPathTests.EditorFixture("seed");
        var other = new Window();
        try
        {
            Assert.False(TextBoxInputAdapter.CanInput(fixture.Editor, other));
            Assert.False(TextBoxInputAdapter.CanInput(fixture.Editor, null));
            fixture.Window.Hide();
            NativeEditingPathTests.Drain();
            Assert.False(fixture.Window.IsActive);
            Reject(fixture);
        }
        finally { other.Close(); }
        Assert.Equal("seed", fixture.Editor.Text);
    }

    [AvaloniaFact]
    public void Ancestor_disabling_reentrant_input_is_rejected_before_inherited_state_propagation()
    {
        using var fixture = new NativeEditingPathTests.EditorFixture("seed");
        fixture.Window.Content = null;
        fixture.Window.UpdateLayout();
        var ancestor = new StackPanel { Children = { fixture.Editor } };
        fixture.Window.Content = ancestor;
        fixture.Window.UpdateLayout();
        fixture.Editor.Focus();
        var result = VirtualKeyboardInputResult.Handled;
        ancestor.PropertyChanged += (_, e) =>
        {
            if (e.Property == InputElement.IsEnabledProperty && !ancestor.IsEnabled)
                result = TextBoxInputAdapter.TryCommitText(fixture.Editor, fixture.Window, "!");
        };
        ancestor.IsEnabled = false;
        Assert.Equal(VirtualKeyboardInputResult.Unavailable, result);
        Assert.Equal("seed", fixture.Editor.Text);
    }

    [AvaloniaFact]
    public void Adapter_preserves_password_editor_constraints_without_content_events()
    {
        using var fixture = new NativeEditingPathTests.EditorFixture();
        fixture.Editor.PasswordChar = '●';
        fixture.Editor.MaxLength = 2;
        Assert.Equal(VirtualKeyboardInputResult.Handled,
            TextBoxInputAdapter.TryCommitText(fixture.Editor, fixture.Window, "abc"));
        Assert.True(fixture.Editor.Text?.Length == 2);
        Assert.Equal(VirtualKeyboardInputResult.Handled, Execute(fixture, VirtualKeyboardKeyKind.Backspace));
        Assert.True(fixture.Editor.Text?.Length == 1);
        fixture.Editor.Undo();
        Assert.True(fixture.Editor.Text?.Length == 2);
    }


    private static VirtualKeyboardInputResult Execute(
        NativeEditingPathTests.EditorFixture fixture, VirtualKeyboardKeyKind kind) =>
        TextBoxInputAdapter.TryExecute(fixture.Editor, fixture.Window, kind);

    private static void Reject(NativeEditingPathTests.EditorFixture fixture)
    {
        Assert.False(TextBoxInputAdapter.CanInput(fixture.Editor, fixture.Window));
        Assert.Equal(VirtualKeyboardInputResult.Unavailable,
            TextBoxInputAdapter.TryCommitText(fixture.Editor, fixture.Window, "!"));
        Assert.Equal(VirtualKeyboardInputResult.Unavailable, Execute(fixture, VirtualKeyboardKeyKind.Backspace));
    }
}
