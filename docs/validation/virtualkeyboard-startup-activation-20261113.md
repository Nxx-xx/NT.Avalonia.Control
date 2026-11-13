# 启动自动聚焦未显示键盘：原因与修复验证

基线：GitHub main `a233d3e44dfc55a8e19e016437979edeb7126e64`。日期：2026-11-13。

## 复现与原因

AutoKeyboardWindow 在 Opened 回调建立 Scope 并调用 TextBox.Focus。原生 Linux/X11 和独立 Headless 启动测试均复现：输入框已有焦点，但键盘不显示；鼠标切换焦点后才显示。

原生诊断只记录布尔状态，不记录输入内容。关键顺序：

```text
Opened-before-scope active=False focus=False fm=False
Opened-after-focus active=False focus=True fm=True keyboard=False target=False
Activated active=False focus=True fm=True keyboard=False target=False
IsActiveChanged active=True focus=True fm=True keyboard=False target=False
Input active=True focus=True fm=True keyboard=False target=False
```

`Activated` 事件发生时 `Window.IsActive` 尚未变为 true。Scope 在该事件中执行 Refresh，正确拒绝非活动窗口，但没监听随后实际 IsActive 状态变化，因此初始焦点没有第二次刷新。不是 Focus 调用失败，不需要人为重复 Focus 或延时计时器。

## 最小修复

Scope 已经通过视觉祖先监听 Window.PropertyChanged；将 `Window.IsActiveProperty` 加入既有生命周期变化过滤条件。实际状态变化后重新评估当前窗口、焦点、显示模式和关闭抑制，不强行打开键盘或改写输入框。

修复后的同一原生启动路径：

```text
Activated active=False focus=True fm=True keyboard=False target=False
IsActiveChanged active=True focus=True fm=True keyboard=False target=False
KeyboardChanged:Target active=True focus=True fm=True keyboard=False target=True
KeyboardChanged:IsVisible active=True focus=True fm=True keyboard=True target=True
Input active=True focus=True fm=True keyboard=True target=True
```

临时 Demo 日志探针已移除，不进入产品或最终提交。

## 验证范围

- 新启动主回归在旧版实际失败（键盘不可见）；修复后新增4项启动/模式/Close回归通过，Headless最终81项全通过（0失败/跳过）。
- Manual/OnTouch不误自动打开、启动期间显式Close不被激活覆盖；原有失活/多窗口与生命周期回归全部保留通过，无新增私有反射依赖。
- Core32项、Themes19项全通过；最终无探针Usage构建0警告/错误。
- 原生云端最终无探针Demo在没有任何点击的情况下自动聚焦并显示键盘；首击q输入成功，隐藏和显式重开正常，运行日志为空。使用Debian13/X11、.NET SDK10.0.402、Avalonia12.0.3。
- 云桌面默认字体回退为 WOFF2，曾独立造成 glyphTypeface 启动错误。本次仅对 Demo 进程使用已经安装的 DejaVu/Noto 字体配置，不修改系统字体。该环境问题与 Scope 激活时序缺陷不同。
- 不声称 Windows、真实触控、DPI、多屏、屏幕阅读器或 IME 已验收。未推送远端。
