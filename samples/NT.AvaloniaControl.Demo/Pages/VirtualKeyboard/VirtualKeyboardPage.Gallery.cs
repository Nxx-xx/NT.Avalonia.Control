using Avalonia;
using Avalonia.Controls;
using NT.AvaloniaControl.Demo.Navigation;

namespace NT.AvaloniaControl.Demo.Pages.VirtualKeyboard;

internal sealed partial class VirtualKeyboardPage
{
    public IReadOnlyList<DemoNavigationItem> CreateGallery()
    {
        if (Content is Control content) content.Margin = new Thickness(24, 20);
        DemoNavigationItem Target(string title, string description, TextBox editor) => new(title, description, "keys", open: () =>
        {
            _lastEditor = editor;
            return this;
        });
        return
        [
            DemoNavigationItem.Group("键盘布局", "英文与数字输入", "keys",
                Target("英文键盘", "字母、方向键与文本编辑", _first), Target("数字键盘", "数字、小数点与负号", _numeric)),
            DemoNavigationItem.Group("输入约束", "长度、多行与密码输入", "keys",
                Target("限长输入", "最多输入 8 个字符", _limited), Target("多行输入", "Enter 换行与 Tab 插入", _multiline), Target("密码输入", "密码字符与输入预览", _password)),
            DemoNavigationItem.Group("焦点与绑定", "显式输入目标与双向绑定", "keys",
                Target("目标切换与 MVVM", "输入框 A / B 的焦点保持与绑定", _second), Target("只读目标", "可以获得焦点，但不接受输入", _readOnly), Target("禁用目标", "不接受焦点和输入", _disabled)),
            DemoNavigationItem.Group("样式与主题", "按键资源与亮暗主题", "keys",
                new DemoNavigationItem("自定义按键样式", "圆角、字号与按下颜色", "keys", open: () =>
                {
                    if (!_resourceOverride) ToggleResources();
                    _lastEditor = _first;
                    return this;
                })),
        ];
    }
}
