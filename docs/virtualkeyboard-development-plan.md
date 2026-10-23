# VirtualKeyboard 开发计划

基线日期：2026 年 10 月 13 日。状态：数字/英文首版软件已实施，平台与发布验收进行中，实际结果见 [实施状态](virtualkeyboard-status.md)。首版开发数字与英文屏幕虚拟键盘，支持直接向应用内的 `TextBox` 输入，并正确处理光标、选区、焦点和撤销。中文输入放入后续阶段，首版预留输入方法与目标适配边界。

沿用现有控件系列结构：新增 `NT.AvaloniaControl.VirtualKeyboard` 功能项目和 `NT.AvaloniaControl.VirtualKeyboard.Themes` 默认主题项目，示例接入统一 `NT.AvaloniaControl.Demo`，测试按控件独立划分。参考官方公开功能设计自己的 API，不要求与商业控件二进制兼容。

**技术基线和交付范围**

- 沿用仓库当前 .NET SDK `10.0.401`、`net10.0`、Avalonia `12.0.3`、中央包版本和锁定还原；实施开始时重新读取这些配置，避免使用已经变更的版本。
- 首版产品只依赖 Avalonia 基础能力，不依赖商业虚拟键盘、Windows 按键注入或中文输入引擎。输入只作用于当前应用内的有效目标。
- 首个可用版本提供手动放置的 `VirtualKeyboard` 与显式 `Target`。内置适配器支持 Avalonia `TextBox`，包括其多行和 `PasswordChar` 场景。自定义编辑器、第三方输入控件和 TreeDataGrid 编辑器自动发现留待后续适配。
- 目标平台沿用 Windows 与 Linux，Linux 重点验证麒麟。发布支持列表按实际测试组合填写；目标设备未到位时，可以完成 Windows 与自动化验证，麒麟状态保持待验证。
- 交付物包括两个可独立消费的 NuGet 包、统一 Demo 页面、最小接入样例、使用/API 文档、专属测试及平台验收记录。

首版采用显式目标以控制范围。表单切换输入框时，由应用更新 `Target`；Demo 展示这种接入。自动跟随焦点、自动显隐与内容避让由后续 `VirtualKeyboardScope` 扩展，不成为数字/英文首版的完成条件。

**首版功能矩阵**

| 编号 | 功能 | 首版行为与完成标准 | 阶段 |
| --- | --- | --- | --- |
| VK01 | 数字布局 | 提供 `0–9`、小数点、负号及编辑/确认键；小数点和负号通过布局选项显示或隐藏。键盘负责提交字符，数字格式与值域由业务校验处理 | K1、K2 |
| VK02 | 英文布局 | 美式 QWERTY、数字行、空格、常用 ASCII 标点和 Shift 层；大小写与 Shift/CapsLock 行为固定 | K1、K2 |
| VK03 | 布局切换 | 应用可设置 Numeric/English；切换布局或目标时停止重复输入，重置一次性 Shift 与 CapsLock | K1、K2 |
| VK04 | 编辑操作 | 在光标处插入、替换选区、Backspace、Delete、Left/Right、Home/End、全选；复用目标的原生编辑语义 | K0、K1、K3 |
| VK05 | Enter 与 Tab | 多行且 `AcceptsReturn` 时换行；其他情况请求宿主确认。`AcceptsTab` 时插入 Tab；其他情况请求宿主移动至下一输入框 | K0、K1、K3 |
| VK06 | 输入目标 | `Target` 显式绑定；无目标、不支持的目标、只读、禁用、隐藏、卸载及窗口失活时不继续输入 | K0、K1、K3 |
| VK07 | 鼠标与触摸 | 点按产生一次输入；按键不夺走目标焦点；拖出、取消及指针捕获丢失有一致行为 | K0、K2、K3 |
| VK08 | 长按重复 | 仅重复 Backspace、Delete、Left/Right；延迟和间隔可配置；抬起、拖出、目标改变、布局改变及卸载立即停止 | K1、K3 |
| VK09 | 编辑约束与绑定 | 尊重 `MaxLength`、只读、禁用和密码显示；输入更新现有 TwoWay 绑定，保持原生撤销/重做路径 | K0、K1、K3 |
| VK10 | 独立主题 | 默认亮暗主题、可覆盖资源、按下/锁定/禁用状态；只引用功能包并提供自定义模板也能工作 | K1、K2、K4 |
| VK11 | 可访问性 | 按键有可读名称、角色与调用动作；视觉尺寸和焦点策略明确，不将完整屏幕阅读器支持视为自动满足 | K2、K4、K5 |
| VK12 | 生命周期与部署 | 页面停用、窗口失活、卸载以及键盘自身/祖先隐藏或禁用时停止输入；双包在独立消费者中验证 XAML、主题与输入 | K3、K5 |

中文拼音、词库、候选窗、联想、滑行输入、多语言布局、系统级键盘、跨进程输入、自动输入框发现和完整剪贴板工具栏不进入首版。数字/英文首版也要正确编辑目标中已有的中文、代理对与组合字符；输入范围和已有文本的编辑正确性分别验收。

**输入行为契约**

1. 字符键通过目标适配器提交文本，功能键通过适配器执行编辑动作。优先验证 Avalonia 公开的 `TextInput` 与按键处理路径，使目标继续负责选区替换、长度限制、绑定和撤销。禁止通过反射访问私有字段或逐字符重写整个 `Text` 绕过编辑流程。
2. 文本直接提交 ASCII 字符，不依赖当前操作系统键盘布局。功能键事件属于应用内输入路由，不等同于真实操作系统键盘事件；平台输入法行为必须单独验证。
3. Shift 为一次性状态，处理一次字符键后复位；CapsLock 持续到再次切换。字母大小写为 Shift 与 CapsLock 的异或结果，数字与标点只受 Shift 影响。修改键状态不改变目标文本。
4. 普通字符键在同一按键内抬起时提交一次；移出后抬起取消。重复键按下时执行一次，再按计时器重复；拖出后停止，已执行的动作保留，抬起不能再由 Click 追加一次。首版只接受一个活动指针，额外触点不产生输入。
5. 每次输入重新验证目标、目标所属窗口、可编辑状态及键盘自身/祖先的有效可见与启用状态。目标切换使旧输入会话失效，旧计时器不能向新旧目标继续发送动作。窗口重新激活或键盘重新显示/启用后，仅接受新的按下，不恢复此前的长按。
6. 按键指针交互不夺走 `TextBox` 的焦点和选区。首版要求 `Target` 为宿主当前聚焦的编辑目标；指定其他输入框时，由宿主先建立编辑焦点再更新 `Target`。目标失去编辑焦点时暂停输入，不通过每次按键重新调用 `Focus()` 修补焦点问题。
7. Enter/Tab 每次只走一种处理路径。换行/制表符由目标处理；单行确认或下一项通过 `ReturnRequested`/`NextRequested` 请求宿主处理，键盘本身不提交业务表单，也不查找并切换任意窗口的焦点。
8. 密码场景不显示输入预览，不在事件、日志和探针结果中输出实际密码。`PasswordChar` 不改变字符提交方式，长度与撤销行为沿用目标本身并实测。
9. 支持物理键盘与虚拟键盘交替输入，首版排除“系统 IME 候选或组合尚未结束时同时使用虚拟键盘”。宿主应先结束或取消系统组合，再使用虚拟键；控件不强制切换系统 IME，不承诺自动提交、取消或可靠阻断组合。`TextPresenter.PreeditText` 可用于原型观察，但不能代表完整平台输入法会话。K0 记录实际组合情景及结果，后续自动接管策略另行验证。

K0 的通过条件包括公开编辑路径可以保持原生 Undo/Redo 与 TwoWay 绑定。若 `TextInput` 或功能键事件原型不能达到要求，先调整适配器方案并更新契约，再推进布局和交互；不能用显示正确但破坏撤销的实现替代此验收。

撤销分组沿用 `TextBox` 原生规则，首版不要求每次虚拟按键都形成独立撤销单元。字符提交以 `string` 表示，输入适配器不限制为单个 `char`，为后续中文选词后的多字符提交保留能力。

**功能项目与主题项目分工**

| 项目 | 职责 | 依赖与约束 |
| --- | --- | --- |
| `NT.AvaloniaControl.VirtualKeyboard` | 布局与键定义、Shift/CapsLock 状态、输入会话、目标适配、长按控制、主控件、按键控件、事件和模板契约 | 依赖 Avalonia，不引用默认主题项目；不硬编码默认主题资源地址 |
| `NT.AvaloniaControl.VirtualKeyboard.Themes` | `VirtualKeyboardTheme`、AXAML 模板、亮暗颜色、字体、按键尺寸/间距、边框及状态外观 | 单向引用功能项目；不保存目标、修改文本或决定长按行为 |

输入链路为：`按键交互 → 键盘状态/输入会话 → 目标适配器 → TextBox 公共编辑路径`。主题只消费功能侧的键模型、命令、属性与伪类。布局定义属于功能侧，按键排列和视觉模板由主题呈现；更换模板不能更改字母映射和编辑语义。

`Core/` 是功能项目内的模块，不生成第三个产品程序集。布局描述、大小写规则和重复状态机不依赖视觉控件；目标适配和 Avalonia 路由代码放入 `Input/`。先使用内部适配接口分离实现，避免在原型验证前发布不稳定的扩展 API。

拟定目录如下，实施时加入现有解决方案：

```text
src/
  NT.AvaloniaControl.VirtualKeyboard/
    Core/                         键定义、布局、大小写及重复状态
    Controls/                     VirtualKeyboard 与按键控件
    Input/                        TextBox 适配、输入会话和焦点处理
    Automation/                   按键名称与调用动作
  NT.AvaloniaControl.VirtualKeyboard.Themes/
    VirtualKeyboardTheme.cs
    Themes/Generic.axaml
    Resources/Colors.axaml
samples/
  NT.AvaloniaControl.Demo/
    Pages/VirtualKeyboard/        数字/英文及输入约束示例
  VirtualKeyboard.Usage/          双包的最小 XAML/MVVM 接入示例
tests/
  VirtualKeyboard.Core.Tests/
  VirtualKeyboard.Headless.Tests/
  VirtualKeyboard.Themes.Tests/
docs/
  virtualkeyboard-development-plan.md
  virtualkeyboard-usage.md
  virtualkeyboard-status.md
```

默认主题入口为 `NT.AvaloniaControl.VirtualKeyboard.Themes.VirtualKeyboardTheme`，资源 URI 为 `avares://NT.AvaloniaControl.VirtualKeyboard.Themes/Themes/Generic.axaml`。功能包与主题包使用兼容的版本，首版沿用仓库发布版本策略。两个产品项目显式设置 `IsPackable=true`，配置各自 `PackageId`、`AssemblyName`、`RootNamespace`、说明和包内 README；主题包声明对兼容功能包的依赖。示例与测试保持不可打包。

**首版公开 API 与模板契约**

下表 API 已在 K0/K1 实施后固定，完整类型/成员见 [公开 API 快照](validation/virtualkeyboard-public-api.txt)，接入和默认值见 [使用说明](virtualkeyboard-usage.md)。

| API | 语义和默认值 |
| --- | --- |
| `VirtualKeyboard : TemplatedControl` | 手动放置的键盘控件，应用通过布局控制可见性与位置 |
| `Target : IInputElement?` | 默认 null；首版仅内置 `TextBox` 适配；其他目标明确处于不可输入状态 |
| `Layout : VirtualKeyboardLayout` | Numeric / English；默认 English |
| `ShowDecimalPoint` / `ShowNegativeSign` | 默认 true；选项改变时更新布局并停止正在进行的重复动作 |
| `IsShiftActive` / `IsCapsLockActive` | 只读状态，默认 false，供主题绑定 |
| `IsInputAvailable` | 只读状态，反映当前目标和会话是否能接收输入；不承诺判断业务校验是否通过 |
| `RepeatDelay` / `RepeatInterval` | 默认 500 ms / 70 ms；有效范围分别为 0–10 s / 10 ms–10 s；Delay 为 0 时第一次重复在一个 Interval 后进行 |
| `ReturnRequested` / `NextRequested` | 带目标身份的宿主请求；不附带目标实际文本 |

模板契约明确主键盘呈现部件、按键命令接入和状态资源。主题可覆盖背景、前景、按下/锁定/禁用颜色、字号、圆角、最小按键尺寸和间距。缺少必需部件时禁用对应交互并提供清晰诊断；可选装饰缺失时正常工作。

主题或模板替换应保持目标与布局设置，取消活动指针和长按以防止旧按钮继续输入。Shift/CapsLock 在单纯换肤时保留；目标或布局改变时按输入契约重置。键盘卸载时清理会话，重新加载后从新的输入开始。

**阶段计划与工作量**

以下为一名熟悉 C# 与 Avalonia 自定义控件的开发者全职投入的粗估，合计约 20–27 个工作日，即 4–6 人周，包含测试、文档和修复。范围不含中文输入、未知架构的运行时移植和等待目标设备；K0 结束后依据原型结果重估。

| 阶段 | 工作量 | 主要任务 | 完成条件 |
| --- | --- | --- | --- |
| K0 基线与输入原型 | 2–3 天 | 固定功能矩阵；建立两个项目、最小消费者和测试入口；验证焦点、选区、TextInput、功能键、Undo/Redo、MVVM、系统 IME 边界 | 公开编辑路径有可重复证据；输入契约和未支持边界固定；确定原生平台探针 |
| K1 模型与目标适配 | 3–4 天 | 键/行/布局模型、Shift/CapsLock、会话失效规则、重复状态机、TextBox 适配和公开 API/模板契约 | 模型测试通过；目标编辑符合 K0 契约；主题侧可以依稳定接口开始工作 |
| K2 控件与基本布局 | 4–5 天 | 主键盘/按键控件、数字与英文布局、基础默认主题、显式 Target 接入、最小样例 | 形成 v0.1 功能预览；真实窗口可操作；只有功能包时可用应用自定义模板 |
| K3 交互与生命周期 | 4–5 天 | 点击/触摸取消、长按、目标与窗口切换、只读/禁用/密码、Enter/Tab、绑定与撤销组合回归 | 形成 v0.2 交互预览；没有重复输入、错目标输入或失控计时器 |
| K4 主题与统一 Demo | 3–4 天 | 完善亮暗主题、资源覆盖、模板替换、按键无障碍信息；注册 Demo 页面并补齐使用/API 文档 | 形成发布候选；导航与页面停用契约正确；主题和示例验收通过 |
| K5 平台与双包验收 | 4–6 天 | Windows/可用 Linux 真机回归、触摸与 DPI、独立 NuGet 消费、资源释放、现有 TreeDataGrid 回归和状态记录 | 已验证平台的首版可发布；功能矩阵全部有结果，待验证平台明确列出 |

这些是控件里程碑，不直接修改 TreeDataGrid 的版本含义。最终包版本由仓库发布策略统一确定。目标是数字/英文范围内可复用的首版，不以官方完整虚拟键盘功能对齐作为发布条件。

K1 的状态和模板契约固定后，核心/适配与主题/样例可并行推进；基础主题随 K2 一起提供，K4 做完整回归。K0 的输入原型是共同前置条件，尚未验证焦点和撤销之前，不先扩大布局种类或加入中文引擎。

**统一 Demo 与自动化接入**

- 在 `samples/NT.AvaloniaControl.Demo/Pages/VirtualKeyboard/` 实现 `IDemoPage`，在 `DemoPageCatalog` 注册唯一标识 `virtualkeyboard`；Demo 项目引用两个新项目，并在 `App.cs` 加载默认主题。
- 页面展示数字、英文、单行/多行、选区替换、两个输入框切换、长度限制、只读/禁用、密码与主题覆盖。`Deactivate()` 停止重复和捕获，`Dispose()` 解除页面及键盘拥有的订阅；重新激活不恢复旧长按。
- 增加按页面选择和派发的原生探针，例如 `--page virtualkeyboard` 和专属 keyboard smoke；现有 TreeDataGrid 的 smoke/MVVM 命令继续兼容，避免对所有页面强转 `TreeDataGridPage`。
- 键盘原生焦点探针使用可激活的真实窗口并确认实际编辑焦点。现有 smoke 的 `ShowActivated=false` 与屏幕外布局不能直接作为键盘焦点验收配置；无激活探针只能证明对应路由/状态，不作为原生焦点通过证据。
- 将三组 VirtualKeyboard 测试加入 `scripts/Verify.ps1` 的显式测试清单。解决方案打包覆盖两个新产品项目，不将 Demo/测试打包为产品。
- 扩展 `scripts/Test-PackageConsumer.ps1` 的按控件消费者夹具，验证真实包的 XAML 类型、主题 URI、指针输入、绑定和自定义模板。沿用 `artifacts/consumer/<guid>`、独立 `Directory.Build.props` / `Directory.Packages.props` 与全新包缓存隔断仓库配置继承；消费者不依赖源码项目引用或开发目录资源。
- 沿用 Windows/Ubuntu CI 验证路径；原生触摸、真实系统 IME 与麒麟验收单独记录。自动化环境不具备这些能力时，状态保持待人工/真机验证。

**测试与发布验收**

| 场景 | 必须满足的结果 | 证据类型 |
| --- | --- | --- |
| 空文本、文本中间和选区替换 | 字符插入位置正确，选择折叠位置正确，单次动作只修改一次 | 模型/Headless 与原生样例 |
| Shift 与 CapsLock 组合 | 字母映射正确；一次性 Shift 清除规则和标点 Shift 层一致 | Core 与 Headless |
| 已有中文、Emoji、组合字符 | 删除和光标移动沿用目标编辑边界，不产生无效代理对；特殊序列行为记录清楚 | Headless 与原生样例 |
| Undo/Redo 与 TwoWay Text 绑定 | 目标撤销栈工作，ViewModel 正确更新；在业务绑定回写时不会反复输入 | Headless 与真实 AXAML 消费者 |
| MaxLength、只读、禁用及密码 | 遵守原生限制；密码只显示掩码，诊断数据不包含密码 | Headless 与原生样例 |
| 普通按键点击、移出和取消 | 点按只输入一次，移出抬起取消，额外触点没有额外输入 | Headless 与鼠标/触屏真机 |
| 长按、捕获丢失和目标切换 | 首次动作与重复次数可测；停止后不追加动作，不向错误目标输入 | 可控计时测试与原生样例 |
| Window 失活、页面停用和卸载 | 捕获、计时器和订阅停止；重入与重新显示不恢复失效会话 | Headless 与原生窗口 |
| 键盘或祖先隐藏/禁用但仍在视觉树中 | 立即停止捕获与重复，保持宿主文本；重新显示/启用不恢复旧按下 | Headless 与原生样例 |
| Enter/Tab 与不同目标设置 | 单次处理，不同时换行和提交；宿主请求目标身份正确 | Headless 与原生样例 |
| 系统 IME 活动组合 | 验证先提交/取消组合再使用虚拟键的交替输入；并发场景明确排除，不将观察到的预编辑文本当成可靠组合检测 | 实际系统输入法 |
| 亮暗切换、资源覆盖和模板替换 | 视觉更新、目标和状态遵循契约、旧按键不继续输入 | Themes 与视觉检查 |
| 100%/125%/150%/200% DPI 与窗口缩放 | 命中与显示一致，按键不重叠；约定最小尺寸下仍可操作 | Windows/Linux 原生视觉检查 |
| 功能包独立消费、主题包消费 | 自定义模板与默认主题均可工作，主题只单向依赖功能包 | 独立 NuGet 消费者 |
| 反复目标切换和页面导航 | 无重复订阅，无卸载后输入，输入控件不被过期会话持续引用 | Headless/资源释放回归 |
| 按键无障碍调用 | 名称/角色正确，调用一次产生一次动作，禁用状态生效 | 自动化接口与真机辅助工具 |

长按测试使用可控时间推进，避免仅依赖实际等待造成随机失败。Headless 负责路由和状态回归，真实系统负责触屏、IME、窗口焦点和 DPI；两类结果分别记录。

K0/K2 在固定窗口、键盘尺寸、构建模式和平台建立首次呈现、按键到文本变化、重复调度与资源释放基线；K5 复测。阈值基于原型实测固定，不能把计时器间隔直接当作响应延迟承诺。首版键数有限，不引入虚拟化。

发布前 VK01–VK12 全部有实现、样例和验收结果；对应测试、包消费及现有 TreeDataGrid 回归通过。存在错目标写入、重复输入、破坏撤销、崩溃或卸载后仍输入的问题时不得发布。使用文档列明支持平台、输入目标和系统 IME 并发边界。

**后续中文输入扩展**

首版将“按键布局”“输入方法”“输入目标”分开：英文和数字直接产生提交文本，输入目标适配器负责送入编辑器。中文阶段加入独立的输入方法会话，在确定字符前维护预编辑文本与候选项，选词后再提交，不把每次拼音按键当作已提交文本。

中文扩展采用独立可选项目，例如 `NT.AvaloniaControl.VirtualKeyboard.Ime.Rime`；默认数字/英文包不因此依赖原生 IME、词库或用户数据目录。首版只保留内部边界，输入方法的公开插件 API 在中文原型完成后单独固定。

自带中文引擎与系统 IME 同一时间只能有一方拥有组合会话。中文原型验证接管、退出与恢复策略；输入目标变化、物理输入、宿主修改 Text/光标/选区或焦点改变时，必须按固定规则结束或取消自带组合，禁止旧会话向已经变化的目标继续提交。

| 后续阶段 | 任务与验收重点 |
| --- | --- |
| C0 中文技术原型 | 选择并核实引擎、许可、原生平台/CPU、方案数据和用户目录；验证预编辑/候选/提交/取消、系统 IME 互斥与恢复；验证多字符提交遇到 MaxLength 时的截断和代理对边界 |
| C1 输入方法会话 | 定义候选项、分页、提交、取消、模式切换、目标失效与外部编辑契约；确认预编辑是否能通过目标公开接口显示，或由键盘自有区域显示 |
| C2 插件与候选界面 | 引擎放入可选包；候选模板仍属于主题层，候选状态和选词逻辑属于功能/输入方法层 |
| C3 中文发布验收 | Windows/麒麟真实输入、目标切换、窗口失活、密码场景与词库部署；原数字/英文行为回归 |

自动 `VirtualKeyboardScope` 可在中文阶段前后单独实现，复用已验证的键盘与适配器。它负责焦点发现、显隐及内容避让，不把这些功能混入中文引擎。

**启动任务清单**

- [x] 创建两个同级功能/主题项目，加入解决方案与锁定还原，确定自己的包名及命名空间。
- [ ] 制作可点击的最小原生键盘原型，验证目标焦点、光标、选区、撤销、MVVM 与 IME 并发边界。（Windows 原生窗口/焦点/编辑已通过；实际系统 IME 待真机）
- [x] 固定 VK01–VK12、公开 API 草案、必需模板部件、资源名称及测试标识。
- [x] 实现 Numeric/English 布局、Shift/CapsLock、输入会话失效和重复状态机。
- [x] 实现 TextBox 适配与基础模板，形成可运行的首个功能预览。
- [ ] 完成触摸/长按、输入约束、目标切换及生命周期组合回归。（Headless 与 Windows 原生路由探针通过；物理触屏未验收）
- [x] 加入统一 Demo、最小消费者、专属测试与验证脚本。（整解决方案 416 项测试、统一 Demo 与双包独立消费通过）
- [ ] 完成平台和双包验收，补齐使用/API 文档及真实状态记录。（双包、Windows 与 Ubuntu/WSLg、性能回归通过；触屏、系统 IME、DPI、麒麟和完整辅助工具待验，见平台验收清单）

**参考与关联文档**

- [现有控件库说明](../README.md)与 [TreeDataGrid 开发计划](treedatagrid-development-plan.md)：系列项目结构、主题分离、Demo 与平台基线。
- [官方 VirtualKeyboard 参考](https://docs.avaloniaui.net/controls/input/text-input/virtualkeyboard/virtualkeyboard-control)：显式目标、输入方法和后续 RIME 扩展参考。
- [官方 VirtualKeyboardScope](https://docs.avaloniaui.net/controls/input/text-input/virtualkeyboard/virtualkeyboardscope)：后续自动焦点、显隐和内容避让参考。
- [官方 Text Input and IME](https://docs.avaloniaui.net/docs/input-interaction/text-input)：输入事件与平台输入法边界。
- [Avalonia 12.0.3 TextBox 源码](https://github.com/AvaloniaUI/Avalonia/blob/12.0.3/src/Avalonia.Controls/TextBox.cs)与 [TextPresenter 源码](https://github.com/AvaloniaUI/Avalonia/blob/12.0.3/src/Avalonia.Controls/Presenters/TextPresenter.cs)：固定版本的编辑路径和预编辑边界参考。

实施状态统一更新到 `virtualkeyboard-status.md`；只有实际完成并有对应证据的任务才标记完成。
