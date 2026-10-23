# VirtualKeyboard 实施状态

日期：2026 年 10 月 13 日。数字/英文首版软件与统一 Demo 已完成集成，88 项专属测试、整解决方案 416 项测试和两种真实 NuGet 消费者全部通过。Windows 与 Ubuntu/WSLg 原生窗口已验收；触屏、系统 IME、DPI 矩阵和麒麟设备仍待验收。开发依据为 [开发计划](virtualkeyboard-development-plan.md)，设备操作见 [平台验收清单](virtualkeyboard-platform-acceptance.md)。

基线：.NET SDK 10.0.401、net10.0、Avalonia 12.0.3；产品版本沿用仓库 0.2.0-preview.2。本文的 416 项为 2026-10-13 当时整仓历史结果，88 项为键盘专属测试；当前全仓功能、测试和提交记录见 [当前状态与下一步](current-status-and-next-steps.md)。

## 统筹与任务分派

| 工作流 | 负责子线程 | 工作范围 | 前置条件与验收 |
| --- | --- | --- | --- |
| 输入原型与适配 | input_prototype | K0 公开输入路由原型；Input 模块；Headless 编辑/绑定回归 | TextInput、功能键、选区、原生撤销与 TwoWay 绑定实际测试通过后才进入后续实现 |
| 核心与交互 | core_control | K1 Core；K2/K3 Controls；Automation；Core 测试 | K0 门禁通过；向主题侧固定键模型、模板及按键契约 |
| 主题与示例 | theme_demo | K2/K4 独立主题、Demo 页面、Usage、Themes 测试、原生探针 | K0 门禁与功能接口固定；兼容现有 TreeDataGrid 探针 |
| 集成与验收 | 主线程 | 项目/解决方案、锁文件、验证脚本、独立 NuGet 消费者、文档与回归 | 汇总三条工作流；记录平台证据与未验证范围 |

三个子线程共享工作区，按目录划分写入范围。K0 期间后两条工作流仅研究和设计；门禁通过后并行推进，主线程负责跨模块问题和最终验收。

## 阶段状态

| 阶段 | 状态 | 当前证据 |
| --- | --- | --- |
| K0 | 公开编辑与原生焦点门禁通过，系统 IME 待验证 | 原型 Headless 11/11；Windows/Ubuntu Usage 各 29/29；实际系统 IME 待设备 |
| K1 | 软件与自动化通过 | Core 25/25；布局、Shift/Caps、可控重复、Input 适配；公共 API 快照 |
| K2 | 软件、自动化与原生预览通过 | 主/按键控件、数字/英文、独立主题、功能包自定义模板、真实 NuGet 消费、Windows/Ubuntu 窗口通过 |
| K3 | 自动化与 Windows 生命周期探针通过 | Headless 52/52；原生生命周期 81/81；指针、取消、重复、窗口/祖先/目标/模板/亮暗切换和严格资源回收 |
| K4 | 主题与统一 Demo 集成通过 | Themes 11/11；亮暗、资源、窄区滚动/命中；统一 Demo 29/29、五轮导航与最小窗口尺寸通过 |
| K5 | 软件、包消费与可用原生平台通过，设备矩阵待验 | 整解决方案 416/416；双包与独立消费者；Windows 固定性能守卫及 Ubuntu/WSLg 探针通过；物理触控、IME、DPI、麒麟与完整辅助工具待验 |

首版数字/英文；中文输入引擎、自动焦点发现与 VirtualKeyboardScope 按原计划后续实施。

## 功能矩阵与证据

| 编号 | 已实现与自动化证据 | 平台边界 |
| --- | --- | --- |
| VK01 | 数字 0–9、小数点/负号选项、编辑与确认；Core / Themes / NuGet 消费者 | 格式、值域由业务校验 |
| VK02 | 美式 QWERTY 所有 printable ASCII 与 Shift/Caps；Core / Headless | 与系统键盘布局独立 |
| VK03 | Numeric/English 与目标变化重置状态、取消旧按下；Core / Headless | 不恢复旧会话 |
| VK04 | 光标、选区、删除、移动、Home/End、全选；NativeEditingPathTests / Headless | 原生 Unicode 编辑边界 |
| VK05 | Enter/Tab 单路径和仅目标身份的宿主请求；Input / Headless | 宿主处理业务确认和下一项焦点 |
| VK06 | 显式 Target、活动同窗口、可见启用聚焦 TextBox；Input / Headless | 不自动发现目标 |
| VK07 | 一次输入、移出取消、捕获丢失、额外指针；Headless 鼠标与合成触摸 | 物理触屏未验收 |
| VK08 | 首次动作、可控时间重复、停止不追加；Core / Headless | 仅 Backspace/Delete/Left/Right |
| VK09 | TwoWay、宿主回写、Undo/Redo、MaxLength、密码；Input / Headless / 包消费者 | 密码断言/探针不输出实际文本 |
| VK10 | 独立主题、亮暗、状态资源、覆盖、共享模板换肤；Themes / 包消费者 | 自定义模板可仅引用功能包 |
| VK11 | Button 角色、可读名称、Invoke、禁用/隐藏门禁；Headless | 完整屏幕阅读器未验收 |
| VK12 | 页面取消、卸载、窗口/祖先失效、新旧窗口隔离、GC、独立双包；Headless / 包消费者 / Windows 生命周期 / Ubuntu Usage | 麒麟及完整设备矩阵未验收 |

实现与测试位置：`src/NT.AvaloniaControl.VirtualKeyboard/`、`src/NT.AvaloniaControl.VirtualKeyboard.Themes/`、`tests/VirtualKeyboard.*.Tests/`。公开契约快照见 [virtualkeyboard-public-api.txt](validation/virtualkeyboard-public-api.txt)，接入说明见 [使用说明](virtualkeyboard-usage.md)。

## 资源释放与审查修复

已修复并回归：祖先禁用通知中重入输入的传播时序、共享相同 Template 对象的 ControlTheme 切换、实际亮暗主题变化取消旧输入、错类型必需部件诊断、自定义按键容器隐藏/禁用、重复计时器 Tick 卸载订阅。DispatcherTimer 提前触发时按剩余延迟重新调度，避免多等一个完整 RepeatDelay。旧 Target 与祖先、启动过重复后卸载的键盘均有严格 WeakReference 回收断言；反复 attach/detach 与目标切换不重复提交。

Avalonia 自身的 Holding 手势会在指针尚未抬起时以 RunOnce 闭包暂时保留输入源。资源测试加入原生 Border 对照，在公开 HoldWaitDuration 到期并推进渲染/Dispatcher 后严格验证对象已回收；未通过反射、伪造抬起或放宽断言掩盖此边界。键盘自身捕获与重复仍在卸载时立即停止，长按行为测试使用可控时间推进。

## 包与验证入口

产品版本为 `0.2.0-preview.2`。两包显式可打包且各带独立 README；主题包仅依赖同版本功能包，功能包仅依赖 Avalonia。实际 nupkg 元数据已检查。

```powershell
./scripts/Verify.ps1
./scripts/Test-PackageConsumer.ps1 -Control VirtualKeyboard
```

专属消费者分别验证 FunctionOnly（自定义 AXAML 模板，不安装主题包）和 DefaultTheme（公开主题 URI）。两者均使用 `artifacts/consumer/<guid>`、空构建/中央版本边界及各自全新 `.packages` 缓存，从 nupkg 加载类型/资源，验证真实 Headless 指针输入、焦点、选区、原生撤销/重做、TwoWay、数字选项与卸载；不引用源码项目。

TreeDataGrid 并行开发期间曾短暂阻断统一构建，现已解除。最终 `Verify.ps1` 锁定还原、整解决方案 Release 构建（0 警告）、六组测试、四产品包打包和独立消费者全部通过。TreeDataGrid 为 127 + 177 + 24 = 328 项，VirtualKeyboard 为 25 + 52 + 11 = 88 项，共 416 项，零失败、零跳过。旧 TreeDataGrid 原生 smoke 36/36 和 MVVM smoke 29/29 也实跑通过。

## 原生平台结果

| 探针 | 检查 / 坐标命中 | 环境与证据 |
| --- | --- | --- |
| 独立 Usage | 29 / 21 | Windows 10.0.26200 / X64，960×530 DIP，[报告](validation/windows-keyboard-usage-smoke.json)、[渲染](validation/windows-keyboard-usage.png) |
| 统一 Demo | 29 / 18 | Windows，[报告](validation/windows-keyboard-demo-smoke.json)、[渲染](validation/windows-keyboard-demo.png) |
| 统一 Demo 生命周期 | 81 / 37 | Windows，第二个原生 Window 失活、祖先/目标失效、亮暗/模板/布局、五轮导航、最小窗口，[报告](validation/windows-keyboard-lifecycle-smoke.json)、[渲染](validation/windows-keyboard-lifecycle.png) |
| 独立 Usage | 29 / 21 | Ubuntu 26.04 / WSL2 / WSLg / Avalonia.X11，[报告](validation/ubuntu-wsl-keyboard-smoke.json)、[渲染](validation/ubuntu-wsl-keyboard-smoke.png) |

以上均实际确认 Window.IsActive、TextBox.IsFocused 和原生窗口句柄；坐标命中等待场景更新，超时判失败。输入由应用内公开路由模拟，不能据此宣称物理设备、系统 IME 或帧呈现已验收。Windows 最小 Demo 窗口实测键宽 52、高 44 DIP、最小间距 5 DIP；间距允许 100% 布局舍入的 1 DIP 误差，尺寸与不重叠约束保持严格。

Ubuntu 使用工作区隔离的 .NET 10.0.12 运行时和 Windows Release net10.0 IL；没有在 Ubuntu 编译，也没有安装系统包或修改用户设置。app-local ICU、缺少的 X11 库和 Noto CJK 字体来源/哈希/签名记录见 [环境记录](validation/ubuntu-wsl-keyboard-environment.json)。首次中文缺字截图保留，加入进程范围字体后中文渲染已检查通过。Ubuntu/WSLg 不替代麒麟或实体 Linux 设备。

汇总、产品包 SHA256、消费者目录及平台边界见 [virtualkeyboard-verification.json](validation/virtualkeyboard-verification.json)。

## 性能基线与回归守卫

独立 `--keyboard-benchmark` 将按键查找、滚动与原生场景命中准备单独计时；普通键从 PointerReleased 路由前到同步 TextBox.TextProperty 变化计时，并要求按下阶段零文本变化、抬起后恰好一次变化与 TwoWay 值一致。每个场景丢弃 20 次预热并记录 100 样本。重复使用真实 DispatcherTimer，分别记录初始动作、第一次重复与后续间隔；取消后等待、发送旧抬起并继续等待，必须零追加输入。

Windows 三轮新进程共 600 样本：字符插入 release-route P95 为 0.1263 ms，选区替换为 0.1403 ms；一次最大值为 5.8731 ms。首次原生命中准备约 616–633 ms，单独统计。固定本机回归阈值为 P95 ≤1 ms、最大值 ≤20 ms、首次命中 ≤2000 ms、CancelInput ≤10 ms、第一次重复迟到 ≤100 ms、后续间隔 P95 ≤配置间隔+40 ms。详见 [基线与原始数据](validation/virtualkeyboard-performance-baseline.json)。额外最终探针 [200 样本](validation/windows-keyboard-performance-producer-check.json) 也通过相同守卫；失焦失败轮次保留并排除，未重写为通过。

```powershell
dotnet run --project samples/VirtualKeyboard.Usage -c Release -- --keyboard-benchmark --benchmark-output artifacts/platform/keyboard-performance.json
./scripts/Test-VirtualKeyboardPerformance.ps1 -ReportPaths artifacts/platform/keyboard-performance.json
./scripts/Tests/Test-VirtualKeyboardPerformance.Tests.ps1
```

守卫严格要求相同 OS、运行时、Avalonia、窗口尺寸、缩放、主题、字体及键尺寸；检查场景/重复配置唯一、字段类型与有限非负值，并从原始样本重算统计。[脚本回归](validation/virtualkeyboard-performance-guard-tests.json) 41/41 通过，其中 39 个负例覆盖缺字段、伪造摘要、真实性能超界、失焦和 fixture 变化；`Verify.ps1` 已纳入离线报告与脚本验证，不在 CI 启动 GUI。上述阈值是固定环境的回归告警；物理投递、显示帧、跨平台发布性能承诺尚未建立。Ubuntu 另外记录 200 样本和重复/取消结果，[报告](validation/ubuntu-wsl-keyboard-benchmark.json) 通过语义验证，但不套用 Windows 阈值。

## 剩余验收

物理鼠标/键盘/触屏交替、多触点、真实系统 IME 提交/取消后的输入、100%/125%/150%/200% 实际 DPI、麒麟目标设备和完整屏幕阅读器仍待验收。设备不足的条目保持未通过，下一步按 [平台验收清单](virtualkeyboard-platform-acceptance.md) 回填，不将当前软件预览标成全计划发布完成。中文引擎与自动 Scope 沿原计划后续开发。
