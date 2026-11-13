using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using System.Globalization;
using NT.AvaloniaControl.VirtualKeyboard;
using Keyboard = NT.AvaloniaControl.VirtualKeyboard.VirtualKeyboard;

namespace VirtualKeyboard.Usage;

/// <summary>Optional window scope example; no password preview or content telemetry.</summary>
public sealed class AutoKeyboardWindow : Window
{
    private readonly Keyboard _keyboard = new();
    private VirtualKeyboardScope? _scope;

    public AutoKeyboardWindow()
    {
        Title = "VirtualKeyboard · 自动目标与显隐";
        Width = 1000;
        Height = 700;
        TextBox Editor(string hint, VirtualKeyboardInputPurpose purpose)
        {
            var editor = new TextBox { PlaceholderText = hint };
            VirtualKeyboardScope.SetInputPurpose(editor, purpose);
            return editor;
        }
        var text = Editor("英文文本", VirtualKeyboardInputPurpose.Text);
        var integer = Editor("整数（格式由业务校验）", VirtualKeyboardInputPurpose.Integer);
        var amount = Editor("金额：0–10000，最多两位小数", VirtualKeyboardInputPurpose.Decimal);
        var validation = new TextBlock { Text = "布局不替代业务校验。" };
        amount.TextChanged += (_, _) =>
        {
            var valid = decimal.TryParse(amount.Text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number)
                && number is >= 0 and <= 10000 && decimal.Round(number, 2) == number;
            validation.Text = string.IsNullOrEmpty(amount.Text) ? "金额为空。" : valid ? "金额有效；原输入未重写。" : "金额无效：0–10000，最多两位小数。";
        };
        var email = Editor("邮箱（不验证地址）", VirtualKeyboardInputPurpose.Email);
        var url = Editor("URL（不验证地址）", VirtualKeyboardInputPurpose.Url);
        var phone = Editor("电话（不验证号码）", VirtualKeyboardInputPurpose.Phone);
        var password = Editor("密码，无预览", VirtualKeyboardInputPurpose.Text);
        password.PasswordChar = '●';
        var multiline = Editor("多行原生 Enter / Tab", VirtualKeyboardInputPurpose.Text);
        multiline.AcceptsReturn = true;
        multiline.AcceptsTab = true;
        multiline.Height = 65;
        var modes = new ComboBox { ItemsSource = Enum.GetValues<VirtualKeyboardDisplayMode>(), SelectedItem = VirtualKeyboardDisplayMode.OnFocus };
        modes.SelectionChanged += (_, _) => { if (_scope is not null && modes.SelectedItem is VirtualKeyboardDisplayMode mode) _scope.DisplayMode = mode; };
        var open = new Button { Content = "显示键盘", Focusable = false };
        var close = new Button { Content = "隐藏键盘", Focusable = false };
        open.Click += (_, _) => _scope?.Open();
        close.Click += (_, _) => _scope?.Close();
        var viewport = new ScrollViewer { Content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = "OnTouch 仅由触摸触发，不把鼠标或程序聚焦当作触摸。" },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { modes, open, close } },
                text, integer, amount, validation, email, url, phone, password, multiline,
                new TextBox { Text = "只读目标不打开键盘", IsReadOnly = true },
            },
        }};
        var panel = new DockPanel { Margin = new Thickness(20) };
        DockPanel.SetDock(_keyboard, Dock.Bottom);
        panel.Children.Add(_keyboard);
        panel.Children.Add(viewport);
        Content = panel;
        void Reveal() => Dispatcher.UIThread.Post(() =>
        {
            if (_scope is not null && _keyboard.IsVisible && FocusManager?.GetFocusedElement() is TextBox editor)
                editor.BringIntoView();
        }, DispatcherPriority.Loaded);
        AddHandler(InputElement.GotFocusEvent, (_, _) => Reveal(), RoutingStrategies.Bubble);
        viewport.SizeChanged += (_, _) => Reveal();
        Opened += (_, _) => { _scope = new VirtualKeyboardScope(this, _keyboard); text.Focus(); };
        Closed += (_, _) => { _scope?.Dispose(); _scope = null; };
    }
}
