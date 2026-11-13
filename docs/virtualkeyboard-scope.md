# 可选窗口级 VirtualKeyboardScope

这是显式启用的窗口协调器；旧的手动 `Target` 接入不需要更改。

```csharp
// 窗口已显示、键盘位于该窗口后建立。不要同时绑定 Target。
var scope = new VirtualKeyboardScope(window, keyboard, VirtualKeyboardDisplayMode.OnFocus);
VirtualKeyboardScope.SetInputPurpose(amountBox, VirtualKeyboardInputPurpose.Decimal);
VirtualKeyboardScope.SetIsEnabled(excludedBox, false);
// 页面离开时释放；键盘卸载或窗口关闭也释放。
scope.Dispose();
```

## 行为矩阵

| 模式 | 聚焦可编辑输入框 | 主触点点击输入框 | 显式 Open |
| --- | --- | --- | --- |
| Manual | 不自动打开 | 不自动打开 | 当前有效焦点打开 |
| OnFocus | 自动打开 | 随焦点打开 | 当前有效焦点打开 |
| OnTouch | 鼠标/程序聚焦不打开 | 打开 | 当前有效焦点打开 |

- 一个窗口和键盘只允许一个 scope；重复安装报错。不设置全局当前编辑器。
- `Close()` 取消待提交和长按，隐藏并抑制同一焦点会话自动重开；改变焦点、再次触摸或显式 Open 可重开。
- 键盘不主动夺取编辑焦点。非编辑器焦点、只读/禁用/隐藏/排除、失活、卸载会停止旧输入。点击空白未改变焦点时需要宿主主动 Close。
- 不接受跨窗口目标；不读取目标文本用于预览、日志或缓存。敏感目标仍由 TextBox/宿主持有，不是进程安全隔离。
- 显式外部 Target 接管时 scope 让出所有权；不要在 scope 存续时同时绑定 Target。释放后如需自动模式须创建新 scope。
- Enter/Tab 沿用原生编辑和既有 ReturnRequested/NextRequested 事件，业务提交与导航由宿主决定，不自动提交表单。

## 用途、主题与布局

`Default` 保留宿主初始布局配置；Text/Integer/Decimal 分别映射英文、无小数点数字、有小数点数字。Email/Url/Phone 为便利布局，不验证、规范化或重写原文本。小数点固定 `.`，金额格式、范围、精度是业务职责。密码用途不改变无预览契约。

主题默认最小触点 44×44 DIP，间距/字号/最小宽高可通过现有资源覆盖。窄窗保留横向滚动，不压缩触点。可选高对比使用 `Resources/HighContrast.axaml` ResourceInclude，接入见主题 README；不自动跟随 OS 模式。

示例 `--keyboard-auto` 使用底部 DockPanel、上方 ScrollViewer，在焦点或视口尺寸变化后 BringIntoView 当前编辑器。只滚动宿主区域，不重挂控件。极矮窗口仍可能没有足够编辑区域，宿主需调整尺寸或收起键盘。示例只给金额输入附加验证，不把 TextChanged 预览用于密码。

```sh
dotnet run --project samples/VirtualKeyboard.Usage -c Release -- --keyboard-auto
```

## 边界

只支持同一活动 Window 中的原生 TextBox，仍要求其模板具有所属 TextPresenter。没有系统级输入注入、自定义布局公共模型、输入历史、密码复制/剪切、中文拼音或学习。

系统 IME 组合期间并用虚拟键盘仍不在支持范围内；Headless Preedit 观察不证明完整平台组合管理。自动化测试与真实 Windows/Linux 触控、DPI、多屏、屏幕阅读器验收分开记录。
