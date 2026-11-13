# VirtualKeyboard 应用内触摸键盘后续开发计划

> 重建说明：原云端工作区不可用后，用户要求重做 M1/M2。本次从远端可靠基线重新实现；下方为历史规划，不将旧测试计入本次。接入见 [Scope说明](virtualkeyboard-scope.md)，结果见 [本次验证](validation/virtualkeyboard-rebuild-20261113.md)。

规划日期：2026-11-13。源码基线：`9e626da290d7299a03bbc31976de4e111d50b003`。

> 本文是后续开发规划，不是功能完成声明。本次仅创建文档，未修改产品代码、执行构建或运行测试。现有能力来自源码、测试和 Demo 静态阅读；仓库历史验证记录不代表本次重新验证。所有 M0–M4 任务均待实施或重新验收。

## 1. 定位与现有文档

目标：在现有数字/英文键盘基础上，形成可用于 Windows/Linux Avalonia 桌面应用的触摸输入组件，优先解决自动焦点、显隐、输入用途、密码安全与多窗口生命周期。

本文补充而不覆盖以下文档，也不追溯改变首版完成条件：

- [首版开发计划](virtualkeyboard-development-plan.md)：历史 K0–K5 范围。
- [实施状态](virtualkeyboard-status.md)：历史软件及打包验证记录。
- [使用说明](virtualkeyboard-usage.md)：当前显式 Target 接入。
- [平台验收](virtualkeyboard-platform-acceptance.md)：设备矩阵、现有探针和发布阻断项。

实施前重读 `global.json`、`Directory.Build.props`、`Directory.Packages.props`。本次源码基线为 SDK 10.0.402、net10.0、Avalonia 12.0.3；旧计划中的 SDK 版本不是后续实施依据。

## 2. 范围与非目标

### 本轮范围

- 同一应用、同一活动 Window 内的原生 Avalonia TextBox。
- 保留显式 Target 接入，增加可选的窗口级自动目标与显隐。
- 英文、整数、小数优先，按业务需要扩展邮箱、URL、电话便利布局。
- 底部停靠、触控尺寸、主题、有限内容避让、密码安全、多窗口隔离。
- 自动化回归与 Windows/Linux 实际设备验收分开记录。

### 明确不做

- 系统级键盘、SendInput/uinput、跨进程或其他应用输入。
- 中文拼音引擎、候选窗、词库、联想、学习和滑行输入。
- 默认支持任意第三方编辑器、TreeDataGrid 编辑器自动发现或自定义 TextBox 模板。
- 将输入用途布局当作业务数字格式、精度或值域校验。
- 因为编译通过或 Headless 通过而宣布全部平台、触屏、IME 与无障碍已验证。

## 3. 当前基础与差距

| 方面 | 静态确认的现有能力 | 本轮新增或加固 |
| --- | --- | --- |
| 输入 | TextInput/KeyDown 原生编辑路径；选区、MaxLength、绑定、撤销语义有测试 | 保持路线，不直接拼接或重写整个 Text |
| 布局 | English/Numeric；小数点、负号开关 | 输入用途映射、便利布局、受控布局扩展 |
| 编辑键 | Shift/Caps、退格/Delete、左右/Home/End、全选、Enter/Tab | Enter 意图标签与宿主动作契约 |
| 焦点 | 显式 Target；同窗口、已聚焦、可编辑才输入 | 窗口级自动跟踪，消除页面重复 GotFocus 代码 |
| 显隐 | 已有可用状态与失效保护 | 自动显示策略、手动关闭抑制、目标失效处理 |
| 长按 | 编辑键重复与取消；单活动指针 | 真实触摸、捕获丢失、快速目标切换压力验证 |
| 密码 | 使用目标遮罩，无键盘文本预览和文本诊断 | 敏感状态策略、泄露回归测试 |
| 多窗口 | 拒绝跨窗口输入，失活取消 | 每窗口独立 scope、模态窗口、实例仲裁 |
| 中文 | 沿用原生编辑路径操作已有 Unicode 文本 | 明确与中文输入及 IME 并发无关 |

数字布局目前不会保证小数点唯一或负号位置正确。现有源码中的原生输入适配器还要求 TextBox 模板具有所属 TextPresenter，自定义模板必须单独验证。

## 4. 架构与拟议 API

本节名称均为建议，不代表现有公开 API。

保留 Core（布局、修饰状态、重复）、Input（原生 TextBox 适配）、Controls/Themes（交互与呈现）三层，增加窗口级 `VirtualKeyboardScope` 或等价 Controller：

1. 一个窗口内注册一个 scope 并绑定键盘，不建立全局当前编辑器。
2. 默认保持显式模式；自动模式显式启用，不能与手动 Target 争夺控制权。
3. 订阅窗口焦点与生命周期，页面卸载、窗口关闭时取消输入并解除订阅。
4. 提供普通 XAML/C# 接入，不强制引入 DI 框架。
5. 功能包不依赖主题包；主题不得决定目标、修改文本或控制重复逻辑。

建议输入框附加配置：

| 配置 | 意图与边界 |
| --- | --- |
| IsEnabled | 是否允许 scope 接管该编辑器；不是替代控件自身启用状态 |
| InputPurpose | Text/Integer/Decimal/Email/Url/Phone 等，只决定便利布局 |
| EnterAction | Default/Next/Done/Search 等，仅表达意图；业务提交仍由宿主决定 |
| 显示策略 | Manual/OnFocus/OnTouch；具体属性名和所属对象在 M0 固定 |
| 敏感状态 | 与布局用途分离；PasswordChar 必须视为敏感，不能被普通用途覆盖 |

内容避让先采用同窗口底部停靠与有限 BringIntoView/ScrollViewer 协作，不强行重排应用，不首先引入独立键盘窗口。公开布局扩展需不可变定义、唯一 ID 和尺寸校验；当前键/行构造器为 internal，自定义模板不等于自定义布局 API。

## 5. 里程碑

### M0：契约与回归基线

- [ ] 固定显式/自动模式优先级、显示触发、隐藏、Enter/Tab、敏感目标行为矩阵。
- [ ] 运行 Core、Headless、Themes 测试，记录环境和结果；检查公开 API 快照。
- [ ] 确认原生编辑、撤销、绑定与模板限制，避免逐字符修改 Text 的替代实现。
- [ ] 修正 Demo 密码案例“输入预览”文案歧义，保持无密码预览契约。

**交付与验收**：可复现测试基线、行为矩阵、拟议 API 清单；原有 Target + Layout 示例无须改写。没有基线证据不宣称新增功能回归通过。

### M1：自动目标与显隐（首个可交付版本）

- [ ] 实现窗口级 scope，跟随有效 TextBox 焦点并配置布局。
- [ ] 实现 Manual/OnFocus/OnTouch；OnTouch 结合近期触摸来源，不把程序聚焦当作触摸。
- [ ] 键盘不抢编辑焦点；目标切换取消旧按压，旧动作不能进入新目标。
- [ ] 手动隐藏后同一焦点会话不反复弹出；再次触摸目标或显式打开才能重开。
- [ ] 对非编辑区域、只读/禁用/隐藏目标、页面离开、窗口失活定义并实现隐藏或停用。
- [ ] Enter/Tab 保持单一路径；可提供遵循窗口焦点导航的 Next，不能固定在 Demo A/B 之间切换。
- [ ] 解除订阅、重复 attach/detach、目标引用释放均有测试。

**交付与验收**：宿主配置一次即可跟随目标；无显隐循环、焦点抢夺、误输入或旧长按恢复；自动模式不覆盖显式模式；多行 Enter/Tab 原生行为不变。

### M2：触摸呈现与输入用途

- [ ] 优先完善英文、整数、小数；邮箱、URL、电话作为第二批便利布局。
- [ ] 配置键高、触控面积、间距、字号、窄窗口适应、亮暗与高对比主题。
- [ ] 增加底部停靠及有限目标滚动到可见协作，不破坏宿主布局。
- [ ] 需要自定义布局时，先固定不可变模型与非法定义校验，再公开 API。
- [ ] Demo 展示业务数字校验与键盘布局的职责分离。

**交付与验收**：约定窗口尺寸/缩放矩阵无截断或不可达按键，编辑器不会被完全遮挡；切布局不改变已有文本；非法布局定义有可理解的错误；不对业务格式作隐式强制转换。

### M3：密码安全与多窗口加固

安全约束从 M0 开始，不等待 M3 才执行。

- [ ] 密码不进入状态文案、日志、工具提示、探针、截图产物或键盘缓存。
- [ ] 不增加密码回显、输入历史、按键文本遥测或密码复制/剪切能力。
- [ ] 隐藏、卸载、切目标和关窗清理引用与按压会话，但不擅自清空 TextBox 内容。
- [ ] 每窗口独立管理；模态窗口打开和失活立即停止旧窗口输入。
- [ ] 同一 scope 只有一个活动控制者，同窗口多个键盘的仲裁行为明确。
- [ ] 对敏感输入禁用未来可能出现的学习/联想，普通布局不能覆盖敏感判定。

**交付与验收**：A 窗口键盘不能修改 B 窗口；模态弹窗、快速激活切换、关闭和长按组合无串字；诊断不出现测试密码；生命周期回收与无重复订阅测试通过。

**安全边界**：密码仍由应用 TextBox 和业务代码持有。本组件不是安全隔离环境，不能宣称防系统键盘记录、无法读取密码或保证清除进程内所有密码副本。

### M4：平台、IME 与发布验收

- [ ] Windows 真机触摸，物理/虚拟键盘交替、捕获丢失、多指干扰、长按与窗口切换。
- [ ] Linux 按实际发行版、桌面会话与 Avalonia 后端验证；X11 与 Wayland 不互相替代。
- [ ] 麒麟为独立设备项，Ubuntu/WSLg 结果不能代替麒麟实体机。
- [ ] 验证 DPI、多屏、屏幕阅读器、按钮名称/状态、高对比度和敏感信息保护。
- [ ] 做 IME 公共 API 技术验证；不能可靠识别完整组合状态时继续保留排除范围。
- [ ] 更新 Usage、统一 Demo、接入说明和平台矩阵，记录通过/失败/未验证。
- [ ] 完成包消费与项目规定的发布前验证，支持列表只包含有证据的组合。

**交付与验收**：自动化和真实设备有分开的结果记录。错目标、重复提交、撤销损坏、崩溃、停用后继续输入、密码泄露是发布阻断项。缺设备的项目保持未验证，不伪造全面兼容结论。

## 6. IME 与中文边界

- ASCII TextInput 提交不等于给系统 IME 发送真实物理按键；编辑已有中文不等于支持中文输入。
- 系统候选/预编辑未结束时同时使用虚拟键盘不在当前支持契约内，宿主先完成或取消组合。
- 不强制切换系统 IME，不承诺自动提交、取消或可靠阻断组合。
- TextPresenter.PreeditText 只能作为原型观察，不能代表完整跨平台 IME 会话。
- 只通过可支持的公共 API 做技术验证，不使用反射或平台私有接口掩盖限制。
- 中文拼音、候选、词库、学习应单独立项，评估词库许可、引擎依赖、隐私和各平台行为；本计划不授权引入该范围。

## 7. 验证矩阵与执行入口

| 层级 | 重点 |
| --- | --- |
| Core | 布局、Shift/Caps、用途映射、显隐状态机、长按时间与取消 |
| Headless | 原生编辑、选区/MaxLength/绑定/撤销、自动目标、生命周期、多窗口、敏感数据诊断 |
| Themes | 模板契约、缺失部件、尺寸资源、亮暗、高对比、布局溢出 |
| 真机 | 触摸命中/捕获/多指、系统 IME、DPI、多屏、激活、屏幕阅读器 |

继续保留单活动指针策略。第二触点不得产生额外输入或导致长按失控。已有原生窗口探针仍可能使用应用内路由模拟，不能作为物理设备输入证明。

以下为后续执行入口，本次没有执行：

```sh
dotnet test tests/VirtualKeyboard.Core.Tests/VirtualKeyboard.Core.Tests.csproj -c Release
dotnet test tests/VirtualKeyboard.Headless.Tests/VirtualKeyboard.Headless.Tests.csproj -c Release
dotnet test tests/VirtualKeyboard.Themes.Tests/VirtualKeyboard.Themes.Tests.csproj -c Release
```

```powershell
./scripts/Verify.ps1
./scripts/Test-PackageConsumer.ps1 -Control VirtualKeyboard
./scripts/Test-VirtualKeyboardPerformance.ps1
dotnet run --project samples/NT.AvaloniaControl.Demo -c Release -- --keyboard-smoke --keyboard-lifecycle-smoke --smoke-output artifacts/platform/keyboard-lifecycle.json
```

执行前检查 SDK/包还原/窗口及 PowerShell 条件，缺少条件记录阻碍而不标记通过。性能只比较相同固定 fixture，不把 RepeatDelay/RepeatInterval 当作实测输入延迟。

## 8. 开放决策及建议默认值

| 决策 | 建议，尚待实施时确认 |
| --- | --- |
| 默认接入 | 保留显式模式；触摸 Demo 用 OnTouch；纯触摸终端可选 OnFocus |
| 首批布局 | 英文、整数、小数；邮箱/URL/电话后续加入 |
| 小数文化规则 | 当前固定 `.`；先确定固定还是文化相关，不隐式改变存储格式 |
| Linux 目标 | 明确实际发行版、架构、会话/后端、触屏与 IME 版本 |
| 自定义布局 | 有业务需要再稳定公开 API，不为扩展而过度设计 |
| 中文 | 本轮不做，另立计划和依赖评估 |
| 同窗口多键盘 | 明确唯一活动控制者和冲突处理，禁止隐式争抢 Target |

以上不要求现在逐项阻塞规划；实施时在对应里程碑固定契约和验收环境，不把建议当作已承诺功能。

## 9. 源码落点与依赖风险

- `src/NT.AvaloniaControl.VirtualKeyboard/Controls/VirtualKeyboard.cs`：目标、按压/重复、可用状态与生命周期。
- `src/NT.AvaloniaControl.VirtualKeyboard/Core/VirtualKeyboardModels.cs`：布局、键与行定义。
- `src/NT.AvaloniaControl.VirtualKeyboard/Core/KeyboardState.cs`、`RepeatController.cs`：修饰状态和重复。
- `src/NT.AvaloniaControl.VirtualKeyboard/Input/TextBoxInputAdapter.cs`：原生事件输入与窗口/焦点校验。
- `src/NT.AvaloniaControl.VirtualKeyboard/Automation/VirtualKeyboardKeyAutomationPeer.cs`：自动化。
- `src/NT.AvaloniaControl.VirtualKeyboard.Themes/`：主题及视觉资源。
- `tests/VirtualKeyboard.Headless.Tests/NativeEditingPathTests.cs`、`KeyboardLifecycleTests.cs`、`KeyboardInteractionTests.cs`：回归基础。
- `samples/NT.AvaloniaControl.Demo/Pages/VirtualKeyboard/VirtualKeyboardPage.cs`：现有宿主焦点、布局和 Next 示例。
- `samples/VirtualKeyboard.Usage/`：最小接入与平台探针。

主要风险是 Avalonia 焦点时序、原生 TextBox 模板契约、触摸捕获和平台 IME，而不是缺少第三方库。保持功能/主题分离，不为自动显隐引入额外 DI、系统注入或中文引擎依赖。新依赖和公开 API 若确有必要，应在引入前记录用途、许可证、平台支持及回退策略。

## 10. 推荐交付顺序

M0 → M1 → M2 → M3 → M4，安全与生命周期测试贯穿全部阶段。M1 是首个可验收交付：宿主配置一次即可可靠跟随输入框并显隐；随后完善布局和触摸呈现，再完成安全、多窗口与真实设备发布门槛。本文不承诺未评估的工期，也不将文档落盘视为实现完成。
