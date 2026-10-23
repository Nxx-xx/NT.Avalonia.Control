# VirtualKeyboard 平台验收

本表用于完成数字/英文首版剩余的设备验收。软件、原生窗口和包消费结果见 [实施状态](virtualkeyboard-status.md)；自动化通过与实际设备输入分别记录。

## 已执行的环境

| 环境 | 已验证 | 未覆盖 |
| --- | --- | --- |
| Windows 10.0.26200 / X64，.NET 10.0.12，Avalonia 12.0.3，100% | Usage、统一 Demo、81 项生命周期检查、实际焦点与坐标命中、三轮路由性能、整解决方案和 NuGet 消费 | 硬件鼠标/键盘/触屏、系统 IME、其他 DPI、屏幕阅读器 |
| Ubuntu 26.04 / WSL2 / WSLg / X64，.NET 10.0.12 | Avalonia.X11 原生 Usage、29 项检查、21 次命中、200 样本性能、中文字体视觉检查 | 麒麟、实体 Linux 桌面、物理触控、IME、DPI 矩阵 |

WSLg 使用真实 Linux Avalonia.X11 后端及窗口焦点，不等同实体麒麟。Ubuntu 验收环境在工作区配置 app-local ICU、缺少的 X11 库和 Noto CJK 字体，没有安装系统包或修改用户设置。来源、版本、哈希与签名见 [环境记录](validation/ubuntu-wsl-keyboard-environment.json)。

## 自动化入口

```powershell
./scripts/Verify.ps1
dotnet run --project samples/NT.AvaloniaControl.Demo -c Release -- --keyboard-smoke --keyboard-lifecycle-smoke --smoke-output artifacts/platform/keyboard-lifecycle.json
dotnet run --project samples/VirtualKeyboard.Usage -c Release -- --keyboard-benchmark --benchmark-output artifacts/platform/keyboard-performance.json
./scripts/Test-VirtualKeyboardPerformance.ps1
```

生命周期探针用第二个实际 Window 产生失活，检查捕获与重复停止，恢复后必须建立新焦点会话。还检查自身和祖先隐藏/禁用、布局/模板/亮暗切换、同模板 ControlTheme、五轮导航和最小尺寸。指针、功能键是应用内路由模拟，不作为物理设备结论。

性能守卫比对固定 Windows fixture，包含 OS/运行时、尺寸、缩放、主题、字体和键尺寸，不比较不同平台，也不把计时器配置值当作输入延迟。原始数据、排除的失焦轮次与固定回归阈值见 [性能基线](validation/virtualkeyboard-performance-baseline.json)。

## 设备操作清单

打开统一 Demo 的 VirtualKeyboard 页面。使用临时测试文本；密码仅记录长度、掩码和是否通过，不保存实际值或明文截图。

| 编号 | 设备操作 | 必须满足的结果 |
| --- | --- | --- |
| M01 | 鼠标设置光标、选区后点击字符；按住字符键拖出抬起 | 保持焦点；一次提交；替换正确；拖出取消 |
| M02 | 物理/虚拟交替，物理 Undo/Redo，切换 A/B | 绑定、光标、撤销正常；旧目标不继续输入 |
| T01 | 单触点点按字符和长按编辑键，拖出或抬起 | 普通键抬起一次提交；重复键按下一次再重复；停止不追加 |
| T02 | 一个触点按住键，第二触点点击另一键 | 第二触点无额外输入；取消后需新按下 |
| L01 | 长按时激活另一个应用，再回 Demo | 原文本停止变化；旧按下不恢复；新按下正常 |
| L02 | 长按时切页、亮暗或布局，再恢复 | 捕获/重复取消；主题保留 Shift/Caps；目标/布局重置状态；无双发 |
| U01 | 编辑已有中文、Emoji/肤色/ZWJ、组合字符 | 与平台 TextBox 原生行为一致；无无效代理对；记录特殊序列行为 |
| I01 | 系统 IME 提交组合后虚拟输入；另一轮 Esc 取消组合后输入 | 组合已结束；ASCII 在正确光标处提交；焦点/绑定正常 |
| I02 | 观察候选/预编辑仍活动时的场景 | 记为排除范围；不以 PreeditText 判断完整会话，不声称自动接管 |
| C01 | 限长、只读、禁用、密码，多行/单行 Enter/Tab | 遵守限制；编辑或宿主请求只走一路；无密码预览/诊断明文 |
| D01 | 实际 100%/125%/150%/200% 桌面操作并缩放窗口 | 命中一致；不重叠；可滚动至所有键；记录实际 RenderScaling |
| A01 | 平台屏幕阅读器/自动化工具定位并调用键 | 名称/角色可读；一次调用一次动作；隐藏/禁用不可调用 |
| K01 | 麒麟目标设备部署并执行适用项 | 记录 OS/架构/后端/.NET/Avalonia/字体/IME；Ubuntu 不替代麒麟 |

系统 IME 活动组合与虚拟输入同时进行仍排除。麒麟、ARM64 和完整设备矩阵不能仅凭编译加入支持列表。

## 记录与发布

每个组合记录日期、操作人、OS/CPU、显示后端、.NET/Avalonia、缩放、设备、字体和 IME 版本。各编号填写通过/失败/不具备环境，附步骤和报告路径。

错目标、重复提交、撤销损坏、崩溃、停用后继续输入属于发布阻断。未具备设备环境的条目保持待验；回填状态文档后再决定支持组合和首版发布状态。
