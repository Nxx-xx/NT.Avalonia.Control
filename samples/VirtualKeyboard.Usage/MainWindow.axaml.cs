using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using KeyboardControl = NT.AvaloniaControl.VirtualKeyboard.VirtualKeyboard;

namespace VirtualKeyboard.Usage;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += async (_, _) =>
        {
            var editor = this.FindControl<TextBox>("Editor")!;
            if (editor.Focus()) this.FindControl<KeyboardControl>("Keyboard")!.Target = editor;
            if (Program.Arguments.Contains("--keyboard-benchmark", StringComparer.Ordinal))
                await RunKeyboardBenchmarkAsync(Program.Arguments);
            else if (Program.Arguments.Contains("--keyboard-smoke", StringComparer.Ordinal))
                await RunKeyboardSmokeAsync(Program.Arguments);
        };
        Closed += (_, _) =>
        {
            var keyboard = this.FindControl<KeyboardControl>("Keyboard")!;
            keyboard.CancelInput();
            keyboard.Target = null;
            _smokeWindowClosed = true;
        };
    }

    private void EditorGotFocus(object? sender, RoutedEventArgs args)
    {
        if (sender is TextBox { IsFocused: true } editor && this.FindControl<KeyboardControl>("Keyboard") is { } keyboard)
            keyboard.Target = editor;
    }
}
