using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using NT.AvaloniaControl.Demo.Navigation;
using NT.AvaloniaControl.VirtualKeyboard;
using KeyboardControl = NT.AvaloniaControl.VirtualKeyboard.VirtualKeyboard;

namespace NT.AvaloniaControl.Demo.Pages.VirtualKeyboard;

internal sealed partial class VirtualKeyboardPage : UserControl, IDemoPage
{
    private readonly Window _hostWindow;
    private readonly KeyboardControl _keyboard = new() { Name = "DemoKeyboard" };
    private readonly Border _keyboardHost = new() { Name = "KeyboardHost" };
    private readonly TextBox _first = new() { Name = "FirstEditor" };
    private readonly TextBox _second = new() { Name = "SecondEditor" };
    private readonly TextBox _numeric = new() { Name = "NumericEditor", Text = "12.5" };
    private readonly TextBox _limited = new() { Name = "LimitedEditor", MaxLength = 8, Text = "Max8" };
    private readonly TextBox _multiline = new() { Name = "MultilineEditor", AcceptsReturn = true, AcceptsTab = true, Height = 66, Text = "多行文本\nEnter 换行，Tab 插入制表符" };
    private readonly TextBox _password = new() { Name = "PasswordEditor", PasswordChar = '●' };
    private readonly TextBox _readOnly = new() { Name = "ReadOnlyEditor", IsReadOnly = true, Text = "只读输入框" };
    private readonly TextBox _disabled = new() { Name = "DisabledEditor", IsEnabled = false, Text = "禁用输入框" };
    private readonly TextBlock _status = new() { Name = "KeyboardStatus", Text = "点击输入框以建立焦点。", TextWrapping = TextWrapping.Wrap };
    private readonly KeyboardDemoViewModel _viewModel = new();
    private TextBox? _lastEditor;
    private bool _active;
    private bool _disposed;
    private bool _resourceOverride;
    private int _returnRequests;
    private int _nextRequests;

    internal VirtualKeyboardPage(Window hostWindow)
    {
        _hostWindow = hostWindow;
        _keyboardHost.Child = _keyboard;
        DataContext = _viewModel;
        _first.Bind(TextBox.TextProperty, new Binding(nameof(KeyboardDemoViewModel.FirstText)) { Mode = BindingMode.TwoWay });
        _second.Bind(TextBox.TextProperty, new Binding(nameof(KeyboardDemoViewModel.SecondText)) { Mode = BindingMode.TwoWay });
        foreach (var editor in Editors) editor.GotFocus += EditorGotFocus;
        _keyboard.ReturnRequested += ReturnRequested;
        _keyboard.NextRequested += NextRequested;
        Content = CreateContent();
    }

    public Control View => this;
    private TextBox[] Editors => [_first, _second, _numeric, _limited, _multiline, _password, _readOnly, _disabled];

    public void Activate()
    {
        if (_disposed) return;
        _active = true;
        Dispatcher.UIThread.Post(() =>
        {
            if (_active && !_disposed) SelectTarget(_lastEditor ?? _first);
        }, DispatcherPriority.Loaded);
    }

    public void Deactivate()
    {
        _active = false;
        _keyboard.CancelInput();
        _keyboard.Target = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        Deactivate();
        _disposed = true;
        foreach (var editor in Editors) editor.GotFocus -= EditorGotFocus;
        _keyboard.ReturnRequested -= ReturnRequested;
        _keyboard.NextRequested -= NextRequested;
        _keyboard.DataContext = null;
        DataContext = null;
    }

    private void EditorGotFocus(object? sender, RoutedEventArgs args)
    {
        if (_active && sender is TextBox { IsFocused: true } editor) SetFocusedTarget(editor);
    }

    private void SelectTarget(TextBox editor)
    {
        if (!_active || _disposed) return;
        if (editor.Focus()) SetFocusedTarget(editor);
        else
        {
            _keyboard.CancelInput();
            _keyboard.Target = null;
            _status.Text = "该输入框不能建立编辑焦点，键盘暂停输入。";
        }
    }

    private void SetFocusedTarget(TextBox editor)
    {
        _lastEditor = editor;
        _keyboard.Layout = ReferenceEquals(editor, _numeric) ? VirtualKeyboardLayout.Numeric : VirtualKeyboardLayout.English;
        _keyboard.Target = editor;
        _status.Text = $"目标：{EditorName(editor)}。按键保持此输入框的编辑焦点；只读或禁用目标不接收输入。";
    }

    private void ReturnRequested(object? sender, VirtualKeyboardTargetEventArgs args)
    {
        _returnRequests++;
        _status.Text = $"收到 {EditorName(args.Target)} 的确认请求（第 {_returnRequests} 次）。由宿主决定业务操作。";
    }

    private void NextRequested(object? sender, VirtualKeyboardTargetEventArgs args)
    {
        _nextRequests++;
        var next = ReferenceEquals(args.Target, _first) ? _second : _first;
        SelectTarget(next);
        _status.Text = $"收到下一项请求（第 {_nextRequests} 次），宿主已将目标切换至 {EditorName(next)}。";
    }

    private static string EditorName(IInputElement? editor) => (editor as Control)?.Name switch
    {
        "FirstEditor" => "输入框 A", "SecondEditor" => "输入框 B", "NumericEditor" => "数字",
        "LimitedEditor" => "限长 8", "MultilineEditor" => "多行", "PasswordEditor" => "密码",
        "ReadOnlyEditor" => "只读", "DisabledEditor" => "禁用", _ => "未选择",
    };

    private Control CreateContent()
    {
        var heading = new StackPanel { Spacing = 5, Children =
        {
            new TextBlock { Text = "VirtualKeyboard", FontSize = 25, FontWeight = FontWeight.SemiBold },
            new TextBlock { Text = "数字 / 英文 · 显式 Target · 原生 TextBox 编辑路径", Opacity = .75 },
            new TextBlock { Text = "可选中现有文本再输入；Backspace / Delete / 方向键支持长按。系统 IME 组合结束后再使用虚拟键。", TextWrapping = TextWrapping.Wrap, Opacity = .75 },
        } };
        var form = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"), ColumnSpacing = 16, RowSpacing = 10 };
        AddField(form, "输入框 A · TwoWay 绑定", _first, 0, 0);
        AddField(form, "输入框 B · TwoWay 绑定", _second, 0, 1);
        AddField(form, "数字 · 格式与值域由业务校验", _numeric, 1, 0);
        AddField(form, "MaxLength = 8", _limited, 1, 1);
        AddField(form, "多行 · AcceptsReturn / AcceptsTab", _multiline, 2, 0);
        AddField(form, "密码 · 不显示输入预览", _password, 2, 1);
        AddField(form, "只读 · 可以获得焦点但不接受输入", _readOnly, 3, 0);
        AddField(form, "禁用 · 不接受焦点和输入", _disabled, 3, 1);
        var actions = new WrapPanel { Orientation = Orientation.Horizontal };
        AddAction(actions, "编辑 A", () => SelectTarget(_first));
        AddAction(actions, "编辑 B", () => SelectTarget(_second));
        AddAction(actions, "选中 A 的文本", () => { SelectTarget(_first); _first.SelectAll(); });
        AddAction(actions, "英文布局", () => _keyboard.Layout = VirtualKeyboardLayout.English);
        AddAction(actions, "数字布局", () => _keyboard.Layout = VirtualKeyboardLayout.Numeric);
        AddAction(actions, "覆盖主题资源", ToggleResources);
        var decimalOption = new CheckBox { Content = "小数点", IsChecked = true, Focusable = false, Margin = new Thickness(6, 4) };
        decimalOption.IsCheckedChanged += (_, _) => _keyboard.ShowDecimalPoint = decimalOption.IsChecked == true;
        var negativeOption = new CheckBox { Content = "负号", IsChecked = true, Focusable = false, Margin = new Thickness(6, 4) };
        negativeOption.IsCheckedChanged += (_, _) => _keyboard.ShowNegativeSign = negativeOption.IsChecked == true;
        actions.Children.Add(decimalOption);
        actions.Children.Add(negativeOption);
        var boundText = new TextBlock { Name = "BoundModelText", TextWrapping = TextWrapping.Wrap, Opacity = .75 };
        boundText.Bind(TextBlock.TextProperty, new Binding(nameof(KeyboardDemoViewModel.FirstText)) { StringFormat = "ViewModel.A: {0}" });
        return new ScrollViewer { Content = new StackPanel { Spacing = 12, Children = { heading, form, actions, _keyboardHost, _status, boundText } } };
    }

    private static void AddField(Grid form, string label, TextBox editor, int row, int column)
    {
        var field = new StackPanel { Spacing = 4, Children = { new TextBlock { Text = label, Opacity = .75 }, editor } };
        Grid.SetRow(field, row);
        Grid.SetColumn(field, column);
        form.Children.Add(field);
    }

    private static void AddAction(Panel panel, string label, Action action)
    {
        var button = new Button { Content = label, Focusable = false, Margin = new Thickness(0, 0, 6, 4) };
        button.Click += (_, _) => action();
        panel.Children.Add(button);
    }

    private void ToggleResources()
    {
        _resourceOverride = !_resourceOverride;
        if (_resourceOverride)
        {
            _keyboard.Resources["VirtualKeyboardKeyPressedBrush"] = new SolidColorBrush(Color.Parse("#BDE8D9"));
            _keyboard.Resources["VirtualKeyboardKeyCornerRadius"] = new CornerRadius(12);
            _keyboard.Resources["VirtualKeyboardFontSize"] = 20d;
        }
        else
        {
            _keyboard.Resources.Remove("VirtualKeyboardKeyPressedBrush");
            _keyboard.Resources.Remove("VirtualKeyboardKeyCornerRadius");
            _keyboard.Resources.Remove("VirtualKeyboardFontSize");
        }
        _status.Text = _resourceOverride ? "已覆盖按下颜色、圆角和字号；输入目标继续由宿主管理。" : "已恢复默认主题资源。";
    }
}

internal sealed class KeyboardDemoViewModel : ObservableObject
{
    private string? _firstText = "Hello 中文 😀";
    private string? _secondText = "Second input";
    public string? FirstText { get => _firstText; set => SetProperty(ref _firstText, value); }
    public string? SecondText { get => _secondText; set => SetProperty(ref _secondText, value); }
}
