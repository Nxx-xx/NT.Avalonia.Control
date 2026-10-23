using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NT.AvaloniaControl.VirtualKeyboard;
using NT.AvaloniaControl.VirtualKeyboard.Themes;
using KeyboardControl = NT.AvaloniaControl.VirtualKeyboard.VirtualKeyboard;

namespace VirtualKeyboard.Themes.Tests;

public sealed class ThemeContractTests
{
    [Fact]
    public void ProductAssembliesHaveOneWayDependency()
    {
        Assert.DoesNotContain(typeof(KeyboardControl).Assembly.GetReferencedAssemblies(), name => name.Name == "NT.AvaloniaControl.VirtualKeyboard.Themes");
        Assert.Contains(typeof(VirtualKeyboardTheme).Assembly.GetReferencedAssemblies(), name => name.Name == "NT.AvaloniaControl.VirtualKeyboard");
    }

    [AvaloniaFact]
    public void PublicResourceUriLoadsFromAnotherAssembly()
    {
        var styles = Application.Current!.Styles;
        var builtIn = styles.OfType<VirtualKeyboardTheme>().Single();
        var fixture = new UriThemeFixture();
        styles.Remove(builtIn);
        styles.Add(fixture);
        var keyboard = new KeyboardControl();
        var editor = new TextBox();
        Window? window = null;
        try
        {
            window = Show(keyboard, editor);
            Assert.Null(keyboard.TemplateDiagnostic);
            Assert.Equal(keyboard.Rows.Sum(row => row.Keys.Count), Buttons(keyboard).Count());
            Tap(window, Find(keyboard, "a"));
            Assert.Equal("a", editor.Text);
            Assert.True(editor.IsFocused);
        }
        finally { window?.Close(); styles.Remove(fixture); styles.Add(builtIn); }
    }

    [AvaloniaFact]
    public void DefaultThemeRendersEnglishAndNumericLayoutsAndOptions()
    {
        var keyboard = new KeyboardControl();
        var window = Show(keyboard, new TextBox());
        try
        {
            Assert.Equal(5, keyboard.Rows.Count);
            Assert.Contains(Buttons(keyboard), button => button.Key!.Id == "Space");
            Assert.False(Find(keyboard, "a").Focusable);
            Assert.True(Find(keyboard, "a").Bounds.Height >= 44);
            Assert.True(Find(keyboard, "Space").Bounds.Width > Find(keyboard, "Left").Bounds.Width * 4);
            keyboard.Layout = VirtualKeyboardLayout.Numeric;
            keyboard.ShowDecimalPoint = false;
            keyboard.ShowNegativeSign = false;
            window.UpdateLayout();
            Assert.Equal(keyboard.Rows.Sum(row => row.Keys.Count), Buttons(keyboard).Count());
            Assert.DoesNotContain(Buttons(keyboard), button => button.Key!.Id is "." or "-");
            Assert.Contains(Buttons(keyboard), button => button.Key!.Id == "0");
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void LightDarkAndResourceOverridesPreserveTargetAndModifierState()
    {
        var keyboard = new KeyboardControl();
        var editor = new TextBox();
        var window = Show(keyboard, editor);
        try
        {
            Tap(window, Find(keyboard, "CapsLock"));
            window.RequestedThemeVariant = ThemeVariant.Light;
            window.UpdateLayout();
            var light = Assert.IsAssignableFrom<ISolidColorBrush>(keyboard.Background).Color;
            window.RequestedThemeVariant = ThemeVariant.Dark;
            window.UpdateLayout();
            var dark = Assert.IsAssignableFrom<ISolidColorBrush>(keyboard.Background).Color;
            Assert.NotEqual(light, dark);
            Assert.Same(editor, keyboard.Target);
            Assert.True(keyboard.IsCapsLockActive);
            Assert.True(Find(keyboard, "CapsLock").IsLocked);
            Assert.Equal("A", Find(keyboard, "a").Label);
            keyboard.Resources["VirtualKeyboardFontSize"] = 23d;
            keyboard.Resources["VirtualKeyboardKeyMinHeight"] = 52d;
            keyboard.Resources["VirtualKeyboardKeyCornerRadius"] = new CornerRadius(12);
            keyboard.Resources["VirtualKeyboardBackgroundBrush"] = Brushes.MediumPurple;
            window.UpdateLayout();
            Assert.Equal(23d, Find(keyboard, "a").FontSize);
            Assert.Equal(52d, Find(keyboard, "a").MinHeight);
            Assert.Equal(new CornerRadius(12), Find(keyboard, "a").CornerRadius);
            Assert.Same(Brushes.MediumPurple, keyboard.Background);
            Assert.Same(editor, keyboard.Target);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void PressedLockedAndUnavailableVisualStatesHaveDistinctResources()
    {
        var keyboard = new KeyboardControl();
        var editor = new TextBox();
        var window = Show(keyboard, editor);
        try
        {
            var caps = Find(keyboard, "CapsLock");
            var ordinary = caps.Background;
            Tap(window, caps);
            Assert.True(caps.IsLocked);
            Assert.NotEqual(ordinary, caps.Background);
            var button = Find(keyboard, "a");
            var before = button.Background;
            var point = Center(window, button);
            window.MouseDown(point, MouseButton.Left);
            Assert.NotEqual(before, button.Background);
            Assert.Null(editor.Text);
            window.MouseUp(point, MouseButton.Left);
            Assert.Equal("A", editor.Text);
            editor.IsReadOnly = true;
            var disabled = button.Background;
            Assert.False(keyboard.IsInputAvailable);
            Assert.NotEqual(before, disabled);
            Tap(window, button);
            Assert.Equal("A", editor.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TemplateReplacementCancelsPendingPressAndPreservesSettings()
    {
        var keyboard = new KeyboardControl();
        var editor = new TextBox();
        var window = Show(keyboard, editor);
        try
        {
            Tap(window, Find(keyboard, "CapsLock"));
            var oldButton = Find(keyboard, "a");
            var point = Center(window, oldButton);
            window.MouseDown(point, MouseButton.Left);
            keyboard.Template = CustomTemplate();
            keyboard.ApplyTemplate();
            window.UpdateLayout();
            window.MouseUp(point, MouseButton.Left);
            Assert.Null(editor.Text);
            Assert.False(oldButton.IsAttachedToVisualTree());
            Assert.Same(editor, keyboard.Target);
            Assert.Equal(VirtualKeyboardLayout.English, keyboard.Layout);
            Assert.True(keyboard.IsCapsLockActive);
            Assert.NotSame(oldButton, Find(keyboard, "a"));
            Tap(window, Find(keyboard, "a"));
            Assert.Equal("A", editor.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ApplicationCanSupplyWorkingTemplateWithoutDefaultKeyboardTheme()
    {
        var styles = Application.Current!.Styles;
        var builtIn = styles.OfType<VirtualKeyboardTheme>().Single();
        styles.Remove(builtIn);
        var keyboard = new KeyboardControl { Template = CustomTemplate() };
        var editor = new TextBox { Text = "ABC" };
        Window? window = null;
        try
        {
            window = Show(keyboard, editor);
            editor.SelectionStart = 1;
            editor.SelectionEnd = 2;
            Assert.Null(keyboard.TemplateDiagnostic);
            Assert.True(keyboard.IsInputAvailable, $"EditorFocus={editor.IsFocused}; WindowActive={window.IsActive}; KeyboardVisible={keyboard.IsEffectivelyVisible}; Enabled={keyboard.IsEffectivelyEnabled}; Template={keyboard.TemplateDiagnostic}");
            Tap(window, Find(keyboard, "a"));
            Assert.Equal("AaC", editor.Text);
            Assert.True(editor.IsFocused);
            Assert.Equal(keyboard.Rows.Sum(row => row.Keys.Count), Buttons(keyboard).Count());
        }
        finally { window?.Close(); styles.Add(builtIn); }
    }

    [AvaloniaFact]
    public void MissingRequiredPartHasDiagnosticAndDisablesInput()
    {
        var keyboard = new KeyboardControl { Template = new FuncControlTemplate<KeyboardControl>((_, _) => new Border()) };
        var window = Show(keyboard, new TextBox());
        try
        {
            Assert.Contains("PART_RowsPresenter", keyboard.TemplateDiagnostic!);
            Assert.Contains(nameof(ItemsControl), keyboard.TemplateDiagnostic!);
            Assert.False(keyboard.IsInputAvailable);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(560)]
    [InlineData(760)]
    [InlineData(960)]
    public void SmallHostsKeepKeysSeparatedAndAllowHorizontalScrolling(int width)
    {
        var keyboard = new KeyboardControl();
        var editor = new TextBox { Text = "AB" };
        var window = Show(keyboard, editor);
        try
        {
            window.Width = width;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            foreach (var panel in keyboard.GetVisualDescendants().OfType<VirtualKeyboardRowPanel>())
            {
                var controls = panel.GetVisualDescendants().OfType<VirtualKeyboardKeyButton>().ToArray();
                for (var i = 1; i < controls.Length; i++)
                {
                    var previous = controls[i - 1].TranslatePoint(default, panel)!.Value.X + controls[i - 1].Bounds.Width;
                    var current = controls[i].TranslatePoint(default, panel)!.Value.X;
                    Assert.True(current >= previous + 4.9, $"Keys overlap at host width {width}: previous={previous}, current={current}");
                }
                Assert.All(controls, button => { Assert.True(button.Bounds.Width >= 32); Assert.True(button.Bounds.Height >= 44); });
            }
            var scroll = keyboard.GetVisualDescendants().OfType<ScrollViewer>().Single(control => control.Name == "PART_KeyboardScrollViewer");
            Assert.True(scroll.Extent.Width >= scroll.Viewport.Width);
            scroll.Offset = new Vector(Math.Max(0, scroll.Extent.Width - scroll.Viewport.Width), 0);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            editor.CaretIndex = 2;
            var buttonPoint = Center(window, Find(keyboard, "Backspace"));
            Assert.True(buttonPoint.X >= 0 && buttonPoint.X <= window.ClientSize.Width, $"point={buttonPoint}; Extent={scroll.Extent}; Viewport={scroll.Viewport}; Offset={scroll.Offset}; Client={window.ClientSize}");
            Assert.True(keyboard.IsInputAvailable, $"focus={editor.IsFocused}; active={window.IsActive}");
            Tap(window, Find(keyboard, "Backspace"));
            Assert.True(editor.Text == "A", $"Text={editor.Text}; point={buttonPoint}; hit={window.InputHitTest(buttonPoint)}; Extent={scroll.Extent}; Viewport={scroll.Viewport}; Offset={scroll.Offset}; Client={window.ClientSize}");
            Assert.True(editor.IsFocused);
        }
        finally { window.Close(); }
    }

    private static FuncControlTemplate<KeyboardControl> CustomTemplate() => new((_, scope) =>
    {
        var rows = new ItemsControl
        {
            ItemTemplate = new FuncDataTemplate<VirtualKeyboardRow>((row, _) => new ItemsControl
            {
                ItemsSource = row!.Keys,
                ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualKeyboardRowPanel { Spacing = 4 }),
                ItemTemplate = new FuncDataTemplate<VirtualKeyboardKey>((key, _) => new VirtualKeyboardKeyButton
                {
                    Key = key, MinHeight = 44, MinWidth = 32,
                    Template = new FuncControlTemplate<VirtualKeyboardKeyButton>((button, _) =>
                    {
                        var label = new TextBlock();
                        label.Bind(TextBlock.TextProperty, new Binding(nameof(VirtualKeyboardKeyButton.Label)) { Source = button });
                        return new Border { Background = Brushes.LightGray, Child = label };
                    }),
                }),
            }),
        };
        scope.Register("PART_RowsPresenter", rows);
        return rows;
    });
    private static Window Show(KeyboardControl keyboard, TextBox editor)
    {
        var window = new Window { Width = 1100, Height = 430, Content = new StackPanel { Children = { editor, keyboard } } };
        window.Show();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        Assert.True(editor.Focus());
        keyboard.Target = editor;
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return window;
    }
    private static IEnumerable<VirtualKeyboardKeyButton> Buttons(KeyboardControl keyboard) => keyboard.GetVisualDescendants().OfType<VirtualKeyboardKeyButton>();
    private static VirtualKeyboardKeyButton Find(KeyboardControl keyboard, string id) => Buttons(keyboard).Single(button => button.Key!.Id == id);
    private static Point Center(Window window, Control control) => control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
    private static void Tap(Window window, Control control)
    {
        var point = Center(window, control);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        window.UpdateLayout();
    }
}
