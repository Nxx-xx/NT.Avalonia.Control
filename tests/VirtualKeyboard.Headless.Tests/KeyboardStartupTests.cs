using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using NT.AvaloniaControl.VirtualKeyboard;
using Keyboard = NT.AvaloniaControl.VirtualKeyboard.VirtualKeyboard;

namespace VirtualKeyboard.Headless.Tests;

public sealed class KeyboardStartupTests(ITestOutputHelper output)
{
    [AvaloniaFact]
    public void Scope_installed_in_opened_reconciles_initial_focus_without_second_click()
    {
        var editor = new TextBox { Width = 300 };
        var keyboard = new Keyboard { Template = KeyboardFixture.CreateTemplate(), Height = 230 };
        var window = new Window { Width = 1040, Height = 400,
            Content = new StackPanel { Children = { editor, keyboard } } };
        VirtualKeyboardScope? scope = null;
        var trace = new List<string>();
        void Record(string stage) => trace.Add($"{stage}: active={window.IsActive}, editorFocused={editor.IsFocused}, managerIsEditor={ReferenceEquals(window.FocusManager?.GetFocusedElement(), editor)}, visible={keyboard.IsVisible}, targetIsEditor={ReferenceEquals(keyboard.Target, editor)}");
        window.Activated += (_, _) => Record("Activated");
        window.PropertyChanged += (_, e) => { if (e.Property == Window.IsActiveProperty) Record("IsActive changed"); };
        window.AddHandler(InputElement.GotFocusEvent, (_, _) => Record("GotFocus"), RoutingStrategies.Bubble, true);
        window.Opened += (_, _) =>
        {
            Record("Opened before scope");
            scope = new VirtualKeyboardScope(window, keyboard);
            Record("Opened after scope");
            editor.Focus();
            Record("Opened after Focus");
        };
        try
        {
            window.Show(); Record("Show returned");
            window.UpdateLayout(); NativeEditingPathTests.Drain(); Record("Drained");
            output.WriteLine(string.Join(Environment.NewLine, trace));
            Assert.True(window.IsActive);
            Assert.True(editor.IsFocused);
            Assert.Same(editor, window.FocusManager?.GetFocusedElement());
            Assert.True(keyboard.IsVisible, string.Join(Environment.NewLine, trace));
            Assert.Same(editor, keyboard.Target);
        }
        finally { scope?.Dispose(); window.Close(); }
    }
    [AvaloniaTheory]
    [InlineData(VirtualKeyboardDisplayMode.Manual, false)]
    [InlineData(VirtualKeyboardDisplayMode.OnTouch, false)]
    [InlineData(VirtualKeyboardDisplayMode.OnFocus, true)]
    public void Activation_respects_mode_and_close_during_initial_focus(
        VirtualKeyboardDisplayMode mode, bool closeBeforeActivation)
    {
        var editor = new TextBox();
        var keyboard = new Keyboard { Template = KeyboardFixture.CreateTemplate(), Height = 230 };
        var window = new Window { Width = 1040, Height = 400,
            Content = new StackPanel { Children = { editor, keyboard } } };
        VirtualKeyboardScope? scope = null;
        window.Opened += (_, _) =>
        {
            scope = new VirtualKeyboardScope(window, keyboard, mode);
            editor.Focus();
            if (closeBeforeActivation) scope.Close();
        };
        try
        {
            window.Show(); window.UpdateLayout(); NativeEditingPathTests.Drain();
            Assert.True(window.IsActive); Assert.True(editor.IsFocused);
            Assert.False(keyboard.IsVisible); Assert.Null(keyboard.Target);
            Assert.True(scope!.Open()); Assert.Same(editor, keyboard.Target);
        }
        finally { scope?.Dispose(); window.Close(); }
    }

}
