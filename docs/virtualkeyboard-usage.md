# VirtualKeyboard 使用说明

数字与英文 VirtualKeyboard 面向应用内的 Avalonia `TextBox`。首版手动放置键盘，宿主显式设置输入目标。实际验收结果见 [实施状态](virtualkeyboard-status.md)，开发依据见 [开发计划](virtualkeyboard-development-plan.md)。

功能包 `NT.AvaloniaControl.VirtualKeyboard` 与默认主题包 `NT.AvaloniaControl.VirtualKeyboard.Themes` 单向依赖，版本沿用仓库发布策略。只向应用内显式指定且已聚焦的 TextBox 输入。

## 最小接入

安装同版本的两个包，先加载 Avalonia Fluent 主题，再加载默认键盘主题：

```csharp
using Avalonia.Themes.Fluent;
using NT.AvaloniaControl.VirtualKeyboard.Themes;

Styles.Add(new FluentTheme());
Styles.Add(new VirtualKeyboardTheme());
```

也可用 `StyleInclude` 加载 `avares://NT.AvaloniaControl.VirtualKeyboard.Themes/Themes/Generic.axaml`。

```xml
<StackPanel xmlns="https://github.com/avaloniaui"
            xmlns:vk="clr-namespace:NT.AvaloniaControl.VirtualKeyboard;assembly=NT.AvaloniaControl.VirtualKeyboard"
            Spacing="12">
  <TextBox x:Name="Editor" Text="{Binding Value, Mode=TwoWay}" />
  <vk:VirtualKeyboard x:Name="Keyboard" Target="{Binding #Editor}" Layout="English" />
</StackPanel>
```

上述片段放在已有 `xmlns:x` 声明的视图中。窗口显示后由宿主先让 `Editor` 获得焦点；键盘只向当前已聚焦且可编辑的目标输入。表单有多个输入框时，在输入框 `GotFocus` 中更新 `Keyboard.Target`。按键不会逐次调用 `Focus()`。`Target` 可以先绑定到输入框，焦点建立前键盘保持不可输入。

```csharp
first.GotFocus += (_, _) => keyboard.Target = first;
second.GotFocus += (_, _) => keyboard.Target = second;
// 页面停用时取消所有旧按下、捕获和重复。
keyboard.CancelInput();
keyboard.Target = null;
```

完整 AXAML/MVVM 示例为 `samples/VirtualKeyboard.Usage`；统一 Demo 的页面标识为 `virtualkeyboard`。

```powershell
dotnet run --project samples/VirtualKeyboard.Usage -c Release
dotnet run --project samples/VirtualKeyboard.Usage -c Release -- --keyboard-smoke --smoke-output artifacts/platform/keyboard-usage.json
dotnet run --project samples/NT.AvaloniaControl.Demo -c Release -- --page virtualkeyboard
```

## API 与输入规则

命名空间为 `NT.AvaloniaControl.VirtualKeyboard`。

| 成员 | 类型与默认值 | 行为 |
| --- | --- | --- |
| `VirtualKeyboard.Target` | `IInputElement?`，null | 首版内置 `TextBox`；需与键盘位于同一活动 Window，并已聚焦 |
| `Layout` | `VirtualKeyboardLayout`，English | Numeric / English；改变时取消旧输入并重置 Shift/CapsLock |
| `ShowDecimalPoint` / `ShowNegativeSign` | bool，true | 数字布局显示小数点/负号；字符有效性和数字值域由业务校验 |
| `IsShiftActive` / `IsCapsLockActive` | 只读 bool，false | 字母大小写为 Shift 与 CapsLock 异或；数字/标点只受 Shift 影响 |
| `IsInputAvailable` | 只读 bool，false | 当前窗口、目标、键盘、模板与有效祖先状态均允许输入 |
| `RepeatDelay` | TimeSpan，500 ms | 范围 0–10 s；0 表示第一次重复在一个 RepeatInterval 后进行 |
| `RepeatInterval` | TimeSpan，70 ms | 范围 10 ms–10 s；越界拒绝，避免失控重复 |
| `Rows` | 只读行集合 | `VirtualKeyboardRow.Keys` 提供固定功能布局，供模板呈现 |
| `TemplateDiagnostic` | 只读 string? | 必需模板部件缺失时提供诊断，不含目标文本 |
| `CancelInput()` | 方法 | 取消指针/重复；页面停用与宿主主动中断时调用 |
| `ReturnRequested` / `NextRequested` | 事件 | 只带目标身份，请宿主确认或切换下一项；不带目标文本 |

字符通过 `TextInput` 公开路由提交 string；编辑键通过 `KeyDown` 或公开 `SelectAll()` 执行。光标、选区、长度限制、TwoWay 更新、撤销与重做沿用目标原生实现。首版不要求每次虚拟按键都形成单独撤销单元。

Shift 在处理一次字符键后清除，CapsLock 持续到再次切换。只有 Backspace、Delete、Left、Right 可以长按重复。普通键在同一按键内抬起时提交；移出后抬起取消。重复键按下立即执行一次，抬起不追加动作；移出、捕获丢失、目标/布局变化、窗口失活、页面停用、隐藏、禁用、卸载或模板替换均取消旧输入。首版一次只接受一个活动指针。

多行目标 `AcceptsReturn=true` 时 Enter 由目标换行，否则触发 `ReturnRequested`。`AcceptsTab=true` 时 Tab 由目标插入，否则触发 `NextRequested`。每次只走一种路径，业务提交及下一项焦点由宿主处理。

无目标、不支持的控件、只读、禁用、隐藏、卸载、目标未聚焦、不同窗口、非活动窗口及缺少原生 TextPresenter 的自定义 TextBox 模板不会继续输入。恢复可见、启用或窗口激活后，只接受新的按下。

## 主题与自定义模板

功能包不引用默认主题包，也不硬编码主题资源地址。仅消费功能包时，应用提供主键盘及按键控件模板；必需 `PART_RowsPresenter` 类型为 `ItemsControl`，功能侧将其 `ItemsSource` 设置为 `Rows`。每一行呈现 `Keys`，每一个 `VirtualKeyboardKeyButton.Key` 绑定对应键模型。按钮自动关联其键盘视觉祖先。

`VirtualKeyboardKeyButton` 提供只读 `Label` 和 `IsLocked`；默认模板消费 `:pressed`、`:locked`、`:unavailable` 与 Avalonia `:disabled` 伪类。`VirtualKeyboardRowPanel.Spacing` 控制横向间隔，按键模型 `Width` 是布局权重。默认键按钮不接受焦点，普通指针输入保留编辑目标焦点；按键自动化 peer 提供可读名称、Button 角色和 Invoke 动作。完整屏幕阅读器支持仍需平台辅助工具验收。

资源可在应用、窗口或键盘的 `Resources` 中覆盖：

| 类别 | 资源名称 |
| --- | --- |
| 键盘颜色 | `VirtualKeyboardBackgroundBrush`、`VirtualKeyboardForegroundBrush`、`VirtualKeyboardBorderBrush` |
| 按键颜色 | `VirtualKeyboardKeyBackgroundBrush`、`VirtualKeyboardKeyHoverBrush`、`VirtualKeyboardKeyPressedBrush`、`VirtualKeyboardKeyLockedBrush`、`VirtualKeyboardKeyLockedForegroundBrush`、`VirtualKeyboardKeyDisabledBrush`、`VirtualKeyboardKeyDisabledForegroundBrush` |
| 尺寸与字体 | `VirtualKeyboardFontSize`、`VirtualKeyboardKeyMinWidth`、`VirtualKeyboardKeyMinHeight`、`VirtualKeyboardKeySpacing` |
| 圆角与内距 | `VirtualKeyboardCornerRadius`、`VirtualKeyboardKeyCornerRadius`、`VirtualKeyboardPadding`、`VirtualKeyboardKeyPadding` |

默认按键最小宽 32、高 44（Avalonia 逻辑像素），默认间距 6。默认主题内置可选 `PART_KeyboardScrollViewer`，当区域小于按键的自然宽度时横向滚动，保持各键的尺寸和间距。自然宽度依实际字体、平台与资源覆盖测量，Headless 字体测量不代表真机最小宽。自定义模板需要自行处理空间不足。模板、ControlTheme 或实际亮暗主题变化保留 Target、Layout、Shift/CapsLock，并取消旧指针与重复。

## 平台与边界

只输入 ASCII 数字、英文字母和标点，不依赖系统键盘布局。编辑目标中已有中文、代理对和组合字符时复用原生编辑边界，不引入自有 Unicode 分割规则。

首版不支持系统 IME 未结束组合时与虚拟键盘同时输入；宿主应先提交或取消系统组合。控件不强制切换输入法，不承诺自动接管组合。`TextPresenter.PreeditText` 只能用于原型观察，不能代表完整系统 IME 会话。密码只显示目标自身掩码，不输出输入预览、实际密码事件或文本诊断。

Windows、Ubuntu/WSLg 原生探针已通过，环境和边界记录在实施状态中。物理触摸、系统 IME、DPI、麒麟与完整辅助工具仍待 [平台验收](virtualkeyboard-platform-acceptance.md)。Ubuntu 运行时、ICU、X11 库和中文字体为工作区隔离配置，来源见环境记录；正式宿主仍需按其部署环境提供运行时依赖和字体。中文输入方法、自动发现目标和 `VirtualKeyboardScope` 另行开发。

`VirtualKeyboard.Usage --keyboard-smoke` 提供 Windows 及已配置 Linux 环境的独立原生窗口验收入口，可附 `--smoke-output <json>` 与 `--smoke-image <png>`。它检查实际窗口激活、编辑焦点、渲染后的坐标命中、路由编辑及关闭后停止输入。指针和功能键使用应用内模拟路由，结果不会声称物理触摸或系统 IME 已通过；截图是 Avalonia 对可见原生窗口的 RenderTargetBitmap 渲染。

统一 Demo 同时支持 `--keyboard-smoke --keyboard-lifecycle-smoke`，额外验证原生窗口失活、目标/键盘祖先失效、模板/主题/布局、五轮导航和最小窗口。Usage 的 `--keyboard-benchmark`（可附 `--benchmark-output <json>`、`--benchmark-image <png>`）分开记录准备、抬起到文本、真实重复和取消。每个输入场景为 20 次丢弃预热及 100 个有效样本；性能守卫比对固定 Windows 环境，不能直接用于 Linux。

```powershell
dotnet run --project samples/NT.AvaloniaControl.Demo -c Release -- --keyboard-smoke --keyboard-lifecycle-smoke --smoke-output artifacts/platform/keyboard-lifecycle.json
dotnet run --project samples/VirtualKeyboard.Usage -c Release -- --keyboard-benchmark --benchmark-output artifacts/platform/keyboard-performance.json
./scripts/Test-VirtualKeyboardPerformance.ps1 -ReportPaths artifacts/platform/keyboard-performance.json
```

## 验证与双包消费

```powershell
./scripts/Verify.ps1
./scripts/Test-PackageConsumer.ps1 -Control VirtualKeyboard
./scripts/Tests/Test-VirtualKeyboardPerformance.Tests.ps1
```

验证脚本包含三组 VirtualKeyboard 专属测试及现有 TreeDataGrid 回归。包消费者在 `artifacts/consumer/<guid>` 建立独立项目、独立构建/中央版本配置和全新包缓存；分别验证功能包加自定义模板、主题包加公开 URI，不使用源码项目引用。
