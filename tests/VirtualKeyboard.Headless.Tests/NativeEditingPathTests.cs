using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace VirtualKeyboard.Headless.Tests;

/// <summary>
/// K0 evidence for the fixed Avalonia 12.0.3 public editing route. These tests are
/// application routed events; they do not claim operating system IME or touch validation.
/// </summary>
public sealed class NativeEditingPathTests
{
    [AvaloniaFact]
    public void Text_input_inserts_at_caret_and_replaces_selection_without_moving_focus()
    {
        using var fixture = new EditorFixture("abcd");
        var editor = fixture.Editor;
        editor.CaretIndex = 2;
        Text(editor, "XY");
        Assert.Equal("abXYcd", editor.Text);
        Assert.Equal(4, editor.CaretIndex);
        editor.SelectionStart = 1;
        editor.SelectionEnd = 5;
        Text(editor, "中文");
        Assert.Equal("a中文d", editor.Text);
        Assert.Equal(3, editor.CaretIndex);
        Assert.Equal(editor.CaretIndex, editor.SelectionStart);
        Assert.Equal(editor.CaretIndex, editor.SelectionEnd);
        Assert.True(editor.IsFocused);
    }

    [AvaloniaFact]
    public void Text_input_preserves_native_undo_redo_and_two_way_binding()
    {
        var model = new EditorModel { Value = "seed" };
        using var fixture = new EditorFixture();
        var editor = fixture.Editor;
        editor.Bind(TextBox.TextProperty, new Binding(nameof(EditorModel.Value))
        {
            Source = model,
            Mode = BindingMode.TwoWay,
        });
        editor.CaretIndex = 4;
        Text(editor, "abc");
        Drain();
        Assert.Equal("seedabc", model.Value);
        Assert.True(editor.CanUndo);
        editor.Undo();
        Drain();
        Assert.Equal("seed", editor.Text);
        Assert.Equal("seed", model.Value);
        Assert.True(editor.CanRedo);
        editor.Redo();
        Drain();
        Assert.Equal("seedabc", editor.Text);
        Assert.Equal("seedabc", model.Value);
        model.Value = "host";
        Drain();
        Assert.Equal("host", editor.Text);
        editor.CaretIndex = 4;
        Text(editor, "!");
        Drain();
        Assert.Equal("host!", model.Value);
    }

    [AvaloniaFact]
    public void Physical_and_virtual_routes_share_the_editor_and_undo_history()
    {
        using var fixture = new EditorFixture("a");
        fixture.Editor.CaretIndex = 1;
        fixture.Window.KeyTextInput("b");
        Text(fixture.Editor, "c");
        Assert.Equal("abc", fixture.Editor.Text);
        fixture.Editor.Undo();
        Assert.Equal("ab", fixture.Editor.Text);
        fixture.Editor.Redo();
        Assert.Equal("abc", fixture.Editor.Text);
        Key(fixture.Editor, Avalonia.Input.Key.Back);
        Assert.Equal("ab", fixture.Editor.Text);
        fixture.Editor.Undo();
        Assert.Equal("abc", fixture.Editor.Text);
    }

    [AvaloniaFact]
    public void Max_length_counts_selection_replacement_and_truncates_committed_ascii()
    {
        using var fixture = new EditorFixture("abcd");
        var editor = fixture.Editor;
        editor.MaxLength = 5;
        editor.CaretIndex = 4;
        Text(editor, "XY");
        Assert.Equal("abcdX", editor.Text);
        editor.SelectionStart = 1;
        editor.SelectionEnd = 4;
        Text(editor, "12345");
        Assert.Equal("a123X", editor.Text);
        Assert.Equal(4, editor.CaretIndex);
    }

    [AvaloniaFact]
    public void Read_only_text_input_does_not_modify_the_editor()
    {
        using var fixture = new EditorFixture("abc");
        fixture.Editor.IsReadOnly = true;
        fixture.Editor.SelectAll();
        Text(fixture.Editor, "x");
        Key(fixture.Editor, Avalonia.Input.Key.Back);
        Assert.Equal("abc", fixture.Editor.Text);
    }

    [AvaloniaFact]
    public void Password_route_respects_length_mask_and_undo_without_exposing_content_in_assertions()
    {
        using var fixture = new EditorFixture();
        var editor = fixture.Editor;
        editor.PasswordChar = '●';
        editor.MaxLength = 3;
        Text(editor, "aB12");
        Assert.True(editor.Text?.Length == 3);
        var presenter = editor.GetVisualDescendants().OfType<TextPresenter>().Single();
        Assert.Equal('●', presenter.PasswordChar);
        Assert.False(editor.RevealPassword);
        Assert.True(editor.CanUndo);
        editor.Undo();
        Assert.True(string.IsNullOrEmpty(editor.Text));
        editor.Redo();
        Assert.True(editor.Text?.Length == 3);
    }

    [AvaloniaFact]
    public void Enter_and_tab_are_native_editor_actions_only_when_accepted()
    {
        using var fixture = new EditorFixture("a");
        var editor = fixture.Editor;
        editor.CaretIndex = 1;
        var enter = Key(editor, Avalonia.Input.Key.Enter);
        Assert.False(enter.Handled);
        Assert.Equal("a", editor.Text);
        editor.AcceptsReturn = true;
        enter = Key(editor, Avalonia.Input.Key.Enter);
        Assert.True(enter.Handled);
        Assert.Equal("a" + editor.NewLine, editor.Text);
        editor.AcceptsTab = true;
        var tab = Key(editor, Avalonia.Input.Key.Tab);
        Assert.True(tab.Handled);
        Assert.Equal("a" + editor.NewLine + "\t", editor.Text);
    }

    [AvaloniaFact]
    public void Editing_actions_follow_native_selection_and_caret_semantics()
    {
        using var fixture = new EditorFixture("abcd");
        var editor = fixture.Editor;
        editor.CaretIndex = 2;
        Key(editor, Avalonia.Input.Key.Left);
        Assert.Equal(1, editor.CaretIndex);
        Key(editor, Avalonia.Input.Key.Right);
        Assert.Equal(2, editor.CaretIndex);
        Key(editor, Avalonia.Input.Key.Home);
        Assert.Equal(0, editor.CaretIndex);
        Key(editor, Avalonia.Input.Key.End);
        Assert.Equal(4, editor.CaretIndex);
        Key(editor, Avalonia.Input.Key.Back);
        Assert.Equal("abc", editor.Text);
        editor.CaretIndex = 1;
        Key(editor, Avalonia.Input.Key.Delete);
        Assert.Equal("ac", editor.Text);
        editor.SelectAll();
        Assert.Equal(0, Math.Min(editor.SelectionStart, editor.SelectionEnd));
        Assert.Equal(2, Math.Max(editor.SelectionStart, editor.SelectionEnd));
        Key(editor, Avalonia.Input.Key.Back);
        Assert.Equal(string.Empty, editor.Text);
    }

    [AvaloniaFact]
    public void Existing_chinese_and_emoji_use_native_edit_boundaries_without_unpaired_surrogates()
    {
        using var fixture = new EditorFixture("中😀文");
        var editor = fixture.Editor;
        editor.CaretIndex = 1;
        Key(editor, Avalonia.Input.Key.Right);
        Assert.Equal(3, editor.CaretIndex);
        Key(editor, Avalonia.Input.Key.Back);
        Assert.Equal("中文", editor.Text);
        editor.Undo();
        Assert.Equal("中😀文", editor.Text);
        editor.CaretIndex = 1;
        Key(editor, Avalonia.Input.Key.Delete);
        Assert.Equal("中文", editor.Text);
        editor.CaretIndex = 2;
        Key(editor, Avalonia.Input.Key.Back);
        Assert.Equal("中", editor.Text);
    }

    [AvaloniaFact]
    public void Combining_sequence_matches_the_physical_edit_route()
    {
        using var fixture = new EditorFixture("e\u0301x");
        fixture.Editor.CaretIndex = 2;
        Key(fixture.Editor, Avalonia.Input.Key.Back);
        var virtualText = fixture.Editor.Text;
        var virtualCaret = fixture.Editor.CaretIndex;
        using var physical = new EditorFixture("e\u0301x");
        physical.Editor.CaretIndex = 2;
        physical.Window.KeyPress(Avalonia.Input.Key.Back, RawInputModifiers.None, PhysicalKey.Backspace, null);
        physical.Window.KeyRelease(Avalonia.Input.Key.Back, RawInputModifiers.None, PhysicalKey.Backspace, null);
        Assert.Equal(physical.Editor.Text, virtualText);
        Assert.Equal(physical.Editor.CaretIndex, virtualCaret);
    }

    [AvaloniaTheory]
    [InlineData("中😀文")]
    [InlineData("👍🏽")]
    [InlineData("👨‍👩‍👧‍👦")]
    [InlineData("e\u0301")]
    public void Repeated_native_backspace_keeps_existing_complex_text_valid(string text)
    {
        using var fixture = new EditorFixture(text);
        fixture.Editor.CaretIndex = text.Length;
        while (fixture.Editor.Text!.Length > 0)
        {
            var previousLength = fixture.Editor.Text.Length;
            Key(fixture.Editor, Avalonia.Input.Key.Back);
            Assert.True(fixture.Editor.Text.Length < previousLength);
            Assert.True(IsValidUtf16(fixture.Editor.Text));
        }
    }

    [AvaloniaFact]
    public void Preedit_is_only_an_observation_and_key_route_does_not_end_a_platform_composition()
    {
        using var fixture = new EditorFixture("ab");
        fixture.Editor.CaretIndex = 2;
        var presenter = fixture.Editor.GetVisualDescendants().OfType<TextPresenter>().Single();
        presenter.PreeditText = "composition";
        var back = Key(fixture.Editor, Avalonia.Input.Key.Back);
        Assert.False(back.Handled);
        Assert.Equal("ab", fixture.Editor.Text);
        Assert.Equal("composition", presenter.PreeditText);
        presenter.PreeditText = null;
        Key(fixture.Editor, Avalonia.Input.Key.Back);
        Assert.Equal("a", fixture.Editor.Text);
    }

    internal static TextInputEventArgs Text(TextBox editor, string text)
    {
        var args = new TextInputEventArgs
        {
            RoutedEvent = InputElement.TextInputEvent,
            Source = editor,
            Text = text,
        };
        editor.RaiseEvent(args);
        return args;
    }

    internal static KeyEventArgs Key(TextBox editor, Key key)
    {
        var args = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Source = editor,
            Key = key,
        };
        editor.RaiseEvent(args);
        return args;
    }

    internal static void Drain() => Dispatcher.UIThread.RunJobs();

    private static bool IsValidUtf16(string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            if (char.IsHighSurrogate(text[index]))
            {
                if (++index >= text.Length || !char.IsLowSurrogate(text[index])) return false;
            }
            else if (char.IsLowSurrogate(text[index])) return false;
        }
        return true;
    }

    internal sealed class EditorFixture : IDisposable
    {
        public EditorFixture(string? text = null)
        {
            Editor = new TextBox { Text = text, Width = 300 };
            Window = new Window { Width = 420, Height = 200, Content = Editor };
            Window.Show();
            Window.UpdateLayout();
            // The Headless platform posts Show's activation callback to the dispatcher.
            Drain();
            Assert.True(Window.IsActive);
            Assert.True(Editor.Focus());
            Drain();
        }

        public TextBox Editor { get; }
        public Window Window { get; }

        public void Dispose() => Window.Close();
    }

    private sealed class EditorModel : INotifyPropertyChanged
    {
        private string? _value;
        public string? Value
        {
            get => _value;
            set
            {
                if (_value == value) return;
                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
