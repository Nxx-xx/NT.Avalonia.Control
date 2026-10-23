# Markdown 控件开发计划

当前M7可执行软件范围已通过，见[本次M7收束记录](markdown-m7-completion-20261015.md)及[新M7执行记录](../artifacts/markdown-m7-completion/c63fab7542ba4b22b9a63d9cbb736f1b/m7-exit.json)。Windows43项与Linux私有Xvfb/X11新43项真实复制、读回及恢复通过，实际producer/Main/外层均退出0；原WSLg共享显示的安全Pending保持历史记录，两个环境分别记账。

Windows50ms、Linux51ms及身份、引用、脚注语义不变；本次增补按同一冻结源码和产物SHA复用五轮、544×2、416×2、包/API及85/134工具合同，不声称再次执行。远端原生CI由用户暂缓（DeferredByUser）；麒麟、实体Linux/原生Wayland、完整DPI/多屏、物理输入和真实辅助工具继续待验证。未发布、未改版本。

## 原始规划与阶段目标

规划基线日期：2026 年 10 月 13 日。本文保留原始阶段、API、目录和指标的拟定目标；实施进度与当前验收证据见 [实施状态](markdown-status.md)。2026 年 10 月 14 日已建立功能、主题、高亮适配、示例和测试，并持续推进 M1–M7；文中注明日期的后续预算用于补齐原计划的长单块及长会话测量范围。

目标是开发独立的 Avalonia 原生 Markdown 阅读控件，覆盖官方 Markdown 的主要展示、文本交互、样式扩展与流式更新能力，适用于帮助文档、技术说明和 AI 对话。沿用本仓库的控件系列结构，交付功能包、默认主题包、可选高亮适配包、统一 Demo 页面、最小接入样例及测试。首版平台为 Windows 与 Linux，Linux 重点验证麒麟；具体发行版、显示后端、CPU 和字体环境在 M0 核验。

**技术基线与对标边界**

- 沿用当前 .NET SDK `10.0.401`、目标 `net10.0`、C# 14、Avalonia `12.0.3`、`CommunityToolkit.Mvvm` `8.4.2`、中央包版本与锁定还原。实施前重新读取 `global.json`、`Directory.Build.props` 和 `Directory.Packages.props`，版本变化需更新本计划。
- 固定官方 `Avalonia.Controls.Markdown` **12.3.0** 作为首轮公开功能参考，不将动态文档后续新增成员自动纳入当前范围。该版本依赖 Avalonia ≥ `12.0.3`；这里固定的是对照版本，不宣称它始终是最新版本。[官方 12.3.0 包与发行说明][official-package]
- 独立包名和命名空间为 `NT.AvaloniaControl.Markdown`。运行时依赖 Avalonia 基础框架与开源 Markdig，不引用官方商业 Markdown、Documents 或 RichTextEditor 包。Markdig 具体版本、目标框架和所用扩展在 M0 固定到中央版本及锁文件。[Markdig][markdig]
- 对标公开能力和可迁移的使用语义，使用自己的模型、实现和样式类型。功能对齐、API 源码迁移、视觉效果和性能分别验收；不承诺二进制替换、逐像素复刻或完整复用官方文档节点 API。
- 首个正式版本覆盖“只读 Markdown 文档 + 全文选择复制 + 流式更新 + 可定制样式与服务”。Markdown 写回、所见即所得编辑、撤销、分页、打印和 PDF/Word 导出属于后续文档能力，不能把简单保存原始 `Text` 当作文档模型序列化。
- 以公开文档、API 和固定包的发行说明建立对照矩阵。没有可运行的官方参考环境时，记录“按公开文档实现，行为未对照”；文档说明与实际 API 不一致时保留证据，不自行认定完全兼容。
- 无同机、同语料和同配置的官方性能测量时，只报告本库数据和自身回归，不宣称达到或超过官方速度。

官方主文档描述 Markdig → 文档快照 → 活文档 → 原生文本呈现，并列出选择和流式 API；较新的专项文档、API 和 12.3.0 发行说明补充了控件级 `ImageLoader` / `CodeHighlighter`、`CopyAsync()` 和底部跟随细节。M0 应结合这些资料固定基线，不把主页面的简短 API 表当作完整清单。[官方总览][official-markdown]、[控件 API][official-api]、[图片加载][official-images]、[12.3.0 发行说明][official-package]

**首版功能矩阵**

下表中的完成标准是本库拟定的验收要求。性能、虚拟化、诊断和生命周期等质量设计单独标注，避免误写成已核实的官方承诺。每个编号在实施时补充实现状态、样例、测试编号、对照结果与差异。

| 编号 | 能力与依据 | 本库实现及验收重点 | 阶段 |
| --- | --- | --- | --- |
| MD01 | 文本、AXAML 内嵌与 MVVM 绑定；官方总览 | `Text` 绑定、空/null、完整替换、保留换行的内嵌文本；最小 AXAML 消费者可用 | M1、M2 |
| MD02 | 标题、段落、分隔线、行内强调；官方总览 | h1–h6、粗体/斜体、删除线、行内代码、软/硬换行、转义及实体；渲染与纯文本投影有固定语料 | M2 |
| MD03 | 有序/无序列表与引用；官方总览 | 起始编号、嵌套、松散/紧凑列表、列表内代码及嵌套引用；缩进、换行和复制次序一致 | M2 |
| MD04 | fenced/缩进代码；官方总览及高亮说明 | 保留代码空白、语言标识和 fence info；未知语言或未安装高亮器时正常显示原文 | M2、M3 |
| MD05 | 链接与自动链接；官方总览 | 链接文本、URL、title、hover/visited、键盘激活；宿主可处理/取消导航；脚注定位与外部导航分开 | M2、M3、M4 |
| MD06 | pipe/grid 表格；官方总览 | 列对齐、单元格内联格式、网格表格的结构与跨单元格关系；窄视口宽表横向滚动、选择及复制 | M2、M3、M4 |
| MD07 | 任务列表与五类提示块；官方总览及样式说明 | 任务状态只读；NOTE/TIP/IMPORTANT/WARNING/CAUTION 的图文、嵌套与样式；不回写源码 | M3 |
| MD08 | 脚注、Emoji、上下标等扩展；官方总览 | 脚注编号、重复引用、点击定位；Emoji/插入/标记/上下标的解析、呈现与复制；逐项验证支持节点 | M3、M4 |
| MD09 | 图片及自定义 loader；图片加载说明 | 默认、控件级和单元素覆盖；异步加载、占位/失败、尺寸变化、替换服务后重载、取消旧结果 | M2、M3、M5 |
| MD10 | 可插拔代码高亮；官方高亮说明 | 定义独立接口；至少交付一种可选 TextMate 适配；高亮器切换和主题失效后重绘，选择不变 | M3、M6 |
| MD11 | 全文选择、复制、全选与清除；控件 API | 跨段落、代码、列表、引用、表格和离屏块的统一选区；鼠标/键盘、`SelectedText`、`CanCopy` 及事件 | M0、M1、M4 |
| MD12 | 流式追加及显式会话；官方总览与会话 API | `AppendText`、`BeginStreaming`、跨线程会话追加、结束等待；后台解析和差异更新；最后一批不会丢失 | M0、M1、M5 |
| MD13 | 自动滚动到底部；控件 API | 已在底部时跟随，用户上滚暂停、回到底部恢复；图片和字体改变高度后保持同一规则 | M5、M6 |
| MD14 | 语义节点样式与命名资源；官方样式说明 | 自有块/行内样式节点与类、层级选择器、字体/颜色继承、资源覆盖和亮暗主题；样式影响布局时正确失效 | M0、M1、M3、M6 |
| MD15 | 配置 Markdown pipeline；官方总览 | 配置 Markdig 扩展并注册对应节点呈现器；有 parser 无 renderer 的情况给出约定降级及诊断 | M1、M3、M6 |
| MD16 | 长文布局与视口回收；本库质量设计 | 块高度索引、可见块按需呈现、布局缓存、超长单块处理；选择和滚动不依赖视觉对象存活 | M0、M2、M6 |
| MD17 | 生命周期、线程与错误恢复；本库质量设计 | 替换文本/模板、卸载、页面停用时旧结果不提交；任务有界、资源归属明确、失败后可继续更新 | M1–M6 |
| MD18 | 中文、Unicode、DPI 与无障碍；本库质量设计 | 中文字体回退、Emoji/组合字符、RTL、键盘和屏幕阅读器、不同 DPI/视口；按真实平台记录 | M0、M4、M6、M7 |
| MD19 | 独立包、统一 Demo 和最小消费者；仓库约定 | 两个基础包与可选高亮包独立消费；只引用功能包也可提供自己的模板；现有控件回归通过 | M0、M2、M6、M7 |

MD02–MD08 的常规语法和默认扩展参见[总览][official-markdown]；fence info、链接信息保留及脚注重复引用/定位参见[12.3.0 发行说明][official-package]；插入/标记及链接 hover/visited 样式参见[样式说明][official-styling]。MD09 为[图片加载][official-images]，MD10 为[高亮说明][official-highlighting]，MD11–MD13 为[控件 API][official-api]及[会话 API][official-streaming]，MD14 为[样式说明][official-styling]。

官方提到的内置 symbol 扩展缺少足够公开的语法边界，M0 单列核验；未确认前不声称完整复刻。Markdig 能解析数学、图表或其它扩展，也不代表本库已经具备相应的原生渲染器。

**首版范围与后续能力**

首版默认启用已实现呈现器的常规语法及上述主要扩展。基础渲染、语法配置、样式、选择复制、图片/代码服务、流式更新和质量要求共同构成正式版本完成条件，不把“能显示一份 README”当作主要功能对齐。

以下能力分别安排，避免阶段范围不断扩张：

| 能力 | 安排及明确边界 |
| --- | --- |
| ColorCode 高亮适配 | 预留同一高亮接口；TextMate 首版通过后补充第二种适配，不是核心包强制依赖 |
| SVG、认证图片及缓存 | 通过 loader 扩展接入；首版提供实现接口及至少一个自定义样例，完整 SVG 适配可后续独立发布 |
| 相对路径、`BaseUri`、代码复制按钮 | 本库产品设计；首版支持并专门测试，不将其当作已确认的官方默认行为 |
| HTML | 本库首版明确差异：默认作为文本保留；`<br>`、`<sub>` 等常用标签可在 M0 固定白名单后原生呈现，其余节点可诊断地降级。任意 HTML/CSS 布局不进入首版；官方 12.3.0 已有 HTML 专用节点和原文保留，本库此方案不代表 HTML 完全对齐 |
| 数学公式、Mermaid、视频等 | 后续可选节点呈现器；必须同时验证解析与呈现，不能只打开 Markdig 扩展就宣布支持 |
| Markdown 写回 | 后续独立任务，明确语义等价与源码无损是不同标准；保留 source span、原文和 fence info 为扩展基础 |
| RichTextEditor、撤销、分页、打印、PDF/Word 导出 | 后续文档引擎工程；首版不发布尚未验证的通用可编辑 `FlowDocument` 接口 |
| 浏览器、移动平台 | 保留 Avalonia 可移植结构；首版支持声明以已验收 Windows/Linux 组合为准 |

官方 12.3.0 已有 Markdown serializer 和更完整的文档集成；将这些列为后续差异，不据此声明首版覆盖整个官方文档产品体系。[官方发行说明][official-package]

**核心架构与数据边界**

```text
Text / AppendText / MarkdownStreamingSession
        ↓
源文本缓冲、文档代次、配置版本和调度器
        ↓                         后台：纯数据
Markdig AST → 归一化不可变文档快照 → 文档差异
        ↓                         UI：校验代次后提交
自有文档状态 / 样式节点 → 块高度索引 / 视口呈现
        ↓
Avalonia TextLayout、图像与块 Presenter

逻辑文本投影、选区和阅读锚点贯穿各层，不依赖视觉控件存活
```

| 模块 | 主要职责 | 必须保持的约束 |
| --- | --- | --- |
| Parsing | 固定 Markdig pipeline、源码 span、扩展 AST 转换 | parser 与 renderer 能力清单对应；不直接生成视觉控件 |
| Core/Documents | 不可变块/行内快照、结构及源位置、语义元数据 | 不包含 `AvaloniaObject`、控件、可变 Brush、`TextLayout` 或图像资源；结构一经发布不可修改 |
| Core/Text | 渲染文本投影、全局位置、源码映射、选区 | 源 Markdown 坐标与用户看见的文本坐标分开；表格/列表/图片有统一复制规则 |
| Core/Updates | 归一化比较、稳定身份、文档版本和变更描述 | 内容相同不意味着同一节点；重复段落、链接定义和插件配置变化都参与比较 |
| Streaming | 输入合并、解析调度、会话与完成/取消 | 最多一个活动会话；解析并发有界；旧代次永远不能提交 UI |
| Documents/Styling | UI 线程上的可样式化节点、类与属性继承 | 快照与样式对象分开；节点不是每字符一个 Control；离屏节点不要求存在对应视觉元素 |
| Presentation | 段落/表格/代码/图片布局、视口、缓存及命中 | 排版复用 Avalonia；字体、宽度、主题和服务版本纳入缓存失效条件 |
| Input/Selection | 指针捕获、拖选、键盘移动、剪贴板和链接 | 文档统一选区是权威状态；回收 Presenter 不改变选区 |
| Services | 图片、高亮、链接和自定义节点扩展 | 控件与单节点覆盖规则明确；结果绑定文档/节点/服务版本；不释放宿主拥有的服务 |
| Automation | 文档、链接、只读任务状态和文本选择适配 | 无障碍不能只暴露当前可见的几个块；支持级别与真机证据分别记录 |

不重新实现字体塑形、双向文本和字体回退。优先使用 Avalonia `TextLayout` / 文本格式化公开 API 完成段落布局、命中测试与选区几何；自行绘制时仍需实现键盘、链接命中和无障碍适配。[TextLayout API][textlayout]

首轮采用“后台完整解析 + 归一化快照差异 + 复用未变更的呈现”。这是增量呈现策略，不承诺真正的增量解析或每次追加恒定成本。M6 基准若暴露长会话的解析瓶颈，再设计安全的解析失效范围与引用依赖，保留完整解析作为正确性对照。

文档身份由匹配器分配并结合结构、上下文和已有身份迁移；行号、数组下标、AST 对象引用、文本 hash 都不能单独作为稳定身份。无法可靠匹配时重建受影响子树，宁可失去局部缓存也不能复用错误的链接、选区或图像。

**文本、选区与复制契约**

1. 同时维护原始源文本和按文档阅读顺序生成的逻辑文本投影。强调标记、链接 URL、转义、实体、代码缩进和布局换行不能直接通过源码 span 推算可见文本位置。
2. 公共范围和内部字符串偏移使用 UTF-16；键盘移动、命中边界和选区归一化按字素边界处理。代理对、组合字符、Emoji ZWJ 和 RTL 使用专门用例验证，不能逐 `char` 移动造成非法文本。
3. 复制投影使用明确的分隔策略：块间换行、列表项顺序、表格单元格 Tab/行换行、代码原始空白；图片复制为 alt 文本。任务状态、列表编号、提示标题和脚注标记是否进入投影在 M0 固定并写入黄金用例。软换行、硬换行和视觉自动折行分别处理。
4. 一个全局选区可以跨段落、代码块、表格和未呈现区域。全选与 `SelectedText` 从文档模型提取，不遍历当前视觉树；仅展示可见块不能减少复制内容。
5. 支持鼠标拖选、越界自动滚动、双击按词选择、Shift 扩展、方向/Home/End/PageUp/PageDown、Ctrl+A/C 及相应平台键位。普通方向导航不产生文档编辑，链接焦点与选区焦点的规则在 M0 固定。
6. `Text` 完整替换产生新文档代次并清除旧选区。追加/差异更新保留未受影响的位置，重写区域映射到合法边界；锚点删除时回退到最近存活的位置。单纯换肤、字体、宽度及 Presenter 回收保留逻辑选区。
7. 拖选中发生文档更新时，先迁移选区和命中位置，再更新选区几何。模板替换或控件停用取消指针捕获，不恢复旧拖动；选择变化事件按一次提交批量通知，避免每个视觉块重复触发。
8. 复制在 UI 线程捕获选区文本并触发可取消事件，再异步调用剪贴板。无剪贴板或复制失败返回明确结果并提供诊断；不阻塞 UI，也不把异步调用发出当作复制成功。

全文复制、复杂表格复制以及多种 Unicode 的行为，必须在 M0/M1 建立契约，不能到发布前再根据当前实现补定义。

**流式会话、线程与滚动契约**

| 操作/状态 | 拟定行为 |
| --- | --- |
| `Text` 完整替换 | UI 线程；立即使旧会话与旧任务失效，按新完整文本解析；不与旧追加拼接 |
| `AppendText(string, TimeSpan?)` | UI 线程的便利入口，复用隐式会话；显式会话活动时禁止混用；合并间隔与结束方式固定 |
| `BeginStreaming()` | UI 线程创建显式会话；已经有活动会话时明确报错，不静默替换 |
| `session.Append(...)` | 可由任意线程调用；按进入输入锁的顺序接收，调用方负责业务顺序；已结束/取消的会话不再接收 |
| `ReadOnlyMemory<char>` 追加 | 如提供重载，在返回前复制到会话拥有的缓冲，不长期引用调用方可复用的内存 |
| `CompleteAsync()` / 控件 `CompleteStreamingAsync()` | 冻结接收并等待最终快照提交；可见且有效模板时等待最终布局，隐藏状态按下述完成边界处理；正常完成后 `Text` 与完整接收内容一致 |
| `Dispose()` | 非阻塞地请求结束；不能承诺同步等待最终解析，也不能在 Dispatcher 上等待后台任务；异常通过已定义结果/事件呈现 |
| `DisposeAsync()` | 等待正常结束；文本替换、取消或卸载导致的失效按取消结果处理 |
| 明确取消 | 停止接收和待提交工作，不再触发更新事件；保留已接收源文本的规则在 M1 固定；外部完整替换始终优先 |
| UI 状态与事件 | `Text`、文档、选区及 `Updated`/错误事件统一在 UI 线程批量同步；内部流式同步保留绑定，不按外部完整替换重新开代次或清除选区；解析不读取可变视觉属性 |

跨线程会话追加、最终更新等待、底部跟随是当前官方公开能力；本库的线程检查、取消结果和便利方法混用规则需写明自己的行为差异。[官方流式会话][official-streaming]、[控件 API][official-api]

调度器最多执行一个解析任务，并保留一个合并后的最新待处理请求，不能为每个 token 创建独立 Task。Markdig 一次解析若不能中途取消，仍通过文档代次拒绝结果；取消 token 不能替代并发上限。M0 测量合并间隔，M1 固定默认值与范围；缓冲超出配置限额时返回明确错误，不静默截断。

每次工作捕获源版本、pipeline/renderer 配置版本、文档代次和会话身份。UI 提交时再次校验文档代次、配置和会话。**同一有效会话允许已完成的前缀快照单调提交**：源版本高于最后应用版本即可应用，后续追加本身不使该前缀失效；低于或等于已应用版本的结果拒绝。完整替换、配置变化、会话取消或卸载才使相应代次失效。不能要求每个解析结果等于最新接收版本，否则解析慢于输入时可能始终无法显示进度。图片/高亮还校验当前节点身份、URL/代码及服务版本；同一节点内容未变时，不因其它块追加而反复拒绝资源结果。

完成等待不包含远程图片下载或异步高亮的全部完成。已挂载但隐藏时，最终文档提交即正常结束，布局标为待显示时应用；无有效模板或尚未挂载时返回明确的无呈现环境错误，不能无限等布局。已因卸载/替换/取消失效的会话返回取消；解析或呈现失败返回错误并结束等待。普通 `Dispose()` 的非阻塞请求同样必须有可观察结果，不能遗留永不完成的任务。

流式语料必须覆盖未闭合代码围栏、表格头分隔线、列表续行、脚注和在文末补充的引用定义。后续定义可能改变前文，不能只更新最后一个段落。对最终输入，完整赋值与任意分块追加所得归一化文档、链接目标和复制投影必须等价。

`AutoScrollToEnd=false` 为本库拟定默认值。设为 true 后，只有更新前接近底部或用户明确执行 `ScrollToEnd()` 时跟随；用户主动上滚暂停，回到底部恢复。底部阈值与触发规则在 M0 固定。处于阅读状态时用“首个可见逻辑位置 + 块内偏移”保持锚点；图片异步增高、宽度/字体变化和前文插入不应把阅读位置跳到末尾。

先保证正确更新和阅读保持，再实施块虚拟化。单个超长代码块或段落必须有独立测量，块数量很少并不意味着成本很低；必要时增加行级布局缓存，不将块虚拟化当作所有长文本问题的解法。

**功能包、主题包与拟定目录**

| 项目 | 内容与依赖方向 |
| --- | --- |
| `NT.AvaloniaControl.Markdown` | Markdig 适配、内部 Core、文档状态、样式节点、呈现与交互、图片/高亮接口、模板契约及无障碍；依赖 Avalonia/Markdig，不引用默认主题包 |
| `NT.AvaloniaControl.Markdown.Themes` | `MarkdownTheme`、AXAML 模板、节点样式、字体/颜色/间距、亮暗资源；单向引用功能包，不决定解析、选择或更新逻辑 |
| `NT.AvaloniaControl.Markdown.TextMate` | 首版可选高亮适配和语法/主题资源；单向引用功能包，不反向成为基础包依赖；版本和许可在 M0/M3 核验 |
| 后续 `.ColorCode` / 图像适配 | 按需独立发布，复用功能侧接口；基础包不因此强制带上全部依赖 |

两个基础包必须能单独消费，可选适配包不改变功能/主题的分工。应用只引用功能包时可以提供自己的模板。`Core/` 是功能项目内的模块，不额外生成通用文档产品程序集；可以借鉴 TreeDataGrid 的高度索引、锚点与回收经验，但不直接引用其内部实现。

```text
src/
  NT.AvaloniaControl.Markdown/
    Controls/                       Markdown 主控件、属性与事件
    Core/Documents/                 不可变快照、身份与差异
    Core/Text/                      逻辑文本、位置和选区
    Parsing/                        Markdig pipeline 与 AST 适配
    Documents/                      UI 文档/样式节点
    Presentation/                   块呈现、布局缓存与视口
    Input/                          指针、键盘、链接及剪贴板
    Streaming/                      缓冲、调度、会话和版本
    Services/                       图片、高亮及节点扩展接口
    Automation/                     无障碍适配
    README.md
  NT.AvaloniaControl.Markdown.Themes/
    MarkdownTheme.cs
    Themes/Generic.axaml
    Resources/                      颜色、字体、间距和装饰
    README.md
  NT.AvaloniaControl.Markdown.TextMate/
    README.md
samples/
  NT.AvaloniaControl.Demo/Pages/Markdown/
  Markdown.Usage/                   最小 AXAML/MVVM 接入
tests/
  Markdown.Core.Tests/
  Markdown.Headless.Tests/
  Markdown.Themes.Tests/
  Markdown.TextMate.Tests/
  Markdown.Core.Tests/Fixtures/      固定语料与期望结构/文本
benchmarks/
  Markdown.Benchmarks/
docs/
  markdown-development-plan.md      本计划
  markdown-usage.md                 实施后创建
  markdown-status.md                实施后创建
  markdown-official-alignment.md    逐项对照及差异
  markdown-performance.md           方法、预算与结果
  markdown-platform-acceptance.md   真机与待验证项目
  validation/markdown-public-api.txt
```

基础主题入口拟定为 `NT.AvaloniaControl.Markdown.Themes.MarkdownTheme`，资源 URI 为 `avares://NT.AvaloniaControl.Markdown.Themes/Themes/Generic.axaml`。功能包不硬编码该 URI。产品项目显式配置 `IsPackable=true`、包名、程序集、命名空间和 README；示例、测试及基准项目不可打包。版本遵循当前仓库策略，新增控件的 M0–M7 是开发里程碑，不直接等同 NuGet 版本。

**公开 API 与扩展契约草案**

以下名称用于规划，M0/M1 原型后固定类型、签名、默认值和 API 快照；签名差异写入迁移文档。

| API | 语义和拟定默认行为 |
| --- | --- |
| `Markdown : TemplatedControl` | 原生只读阅读控件，具备内置滚动呈现和统一选择 |
| `Text : string?` | 默认 null，与空文本同样显示空文档；支持绑定和 AXAML 内容；完整赋值开启新代次 |
| `Document` | 只读的本库文档状态入口；不开放任意可变编辑 API，不暴露后台 Markdig AST 为长期共享状态 |
| `SelectionEnabled` | 本库扩展，默认 true；关闭时清除选区、停止选择输入，不影响链接 |
| `SelectedText` / `CanCopy` | 只读；从逻辑文本提取。无有效选区时为空/false；实际剪贴板失败由复制结果报告 |
| `SelectionBrush` / `CaretBrush` | 可样式化；键盘导航的只读插入位置也使用统一选区模型 |
| `Copy()` / `CopyAsync()` | 便利命令与可等待的复制结果；异步失败被观察和报告，不 fire-and-forget 丢失异常 |
| `SelectAll()` / `ClearSelection()` | 覆盖完整逻辑文档，清除时统一通知 |
| `SelectionChanged` / `CopyingToClipboard` | UI 线程事件；复制前可取消；事件参数与实际文本快照对应 |
| `AppendText(...)` / `BeginStreaming()` | 便利追加和显式会话；线程、重复会话及混用规则见流式契约 |
| `CompleteStreamingAsync()` / 会话 `CompleteAsync()` | 等待最终文档提交；可见时等待布局，隐藏/未挂载/失败按明确契约结束，不只确认入队 |
| `AutoScrollToEnd` / `ScrollToEnd()` | 默认 false；开启后使用底部跟随，显式滚到底部可恢复跟随 |
| `ImageLoader` / `CodeHighlighter` | 控件级可配置服务；单元素明确覆盖时优先，缺省时继承控件服务 |
| `BaseUri` | 本库扩展，默认 null；解析相对链接/图片路径；基址变化使相关资源重新解析 |
| `ConfigurePipeline` / 配置选项 | 默认仅启用有呈现器的功能；配置固定成不可变版本，变化触发重新解析 |
| 自定义节点呈现器注册 | 按 AST/语义节点类型注册适配；稳定接口在内置扩展验证后发布 |
| 更新/错误事件 | 区分解析、图片、高亮和剪贴板失败；含版本及错误类别，不在诊断中默认输出整份文档 |

AXAML 内嵌文本应采用明确的 Content 属性转换，样例使用 `xml:space="preserve"`；不得把子控件和 Markdown 文本混为两个隐式输入源。绑定默认按只读展示设置；流式输入由控件批量同步 `Text`，需要宿主观察时采用已记录的绑定模式/更新事件。内部同步必须保留绑定，并区分 TwoWay 的同值回写与宿主的新完整文本；同值回写不取消自身会话，新完整文本立即优先替换，不能形成反馈循环。

图片服务首版提供显式可配置的 HTTP/HTTPS、文件及 Avalonia 资源加载器；主控件默认 `ImageLoader=null`，未配置时显示 alt/占位而不自动联网。Demo 和默认接入样例显式注入加载器。相对地址由 `BaseUri` 解析；缓存、认证和 SVG 通过扩展实现。加载器处理超时、取消、失败与占用限额，图片完成后重新布局并保持阅读锚点。

高亮接口返回与代码文本对应的样式范围及语言信息，不能修改代码原文。无服务、未知语言或高亮失败时退回等宽纯文本；应用整体切换主题或高亮器发布失效通知时，相关缓存必须更新。代码复制按钮复制完整原始代码，不复制语言标题或选择装饰。

宿主注入的 loader、高亮器和 HTTP 客户端默认由宿主拥有；控件只释放自己创建的任务、订阅、布局、缓存句柄及资源租约。共享服务同时被多个控件使用时，卸载一个控件不能使其它控件失效。

链接首版通过可处理/取消的事件交给宿主，Demo 明确接入平台打开器。脚注引用在本控件内定位；其它 fragment 链接交给宿主决定。任务复选状态只读，点击不会偷偷改写 Markdown 源文。

**样式和模板设计**

块与行内样式能力是 MD14 的验收目标。M0 验证自有 `StyledElement` 文档节点或等价样式适配层的样式附着、逻辑父子关系、属性继承、嵌套选择器和动态资源。普通快照 DTO 不具备 Avalonia 样式能力，不能仅增加 `Classes` 字符串就宣称任意 AXAML 样式已生效。

M1 固定自有节点/Presenter 类型和语义类，至少覆盖 h1–h6、quote、五类 alert、code、table/header/cell、list/item、inline code、strike/insert/mark 和链接 hover/visited。类型名使用本库命名空间，迁移表明确与官方选择器的对应关系。若采用 Presenter 适配，必须说明块和行内选择器的支持边界；MD14 未实现的部分留作差异，不能隐去。

主题资源使用 `Markdown` 前缀，覆盖字体、正文/弱化色、块间距、标题、代码背景/边框、引用、提示、表格、链接、选区、图片占位及焦点。字体、行高、内边距等改变触发布局；单纯颜色变化尽量只重绘。默认主题、应用覆盖及运行时换肤均要保持源文本和逻辑选区。

主模板部件拟定包括滚动宿主、文档 Presenter、选择/焦点装饰和占位区域。M1 固定必需部件、可选部件和缺失诊断；使用自定义模板必须仍能进行选择、流式更新和命中。替换模板释放旧呈现器的捕获、订阅和缓存引用，再按新模板恢复当前文档，不恢复旧会话的已取消操作。

**阶段计划、依赖与工作量**

以下是一名熟悉 C# 与 Avalonia 自定义控件的开发者全职投入的初估，合计 **13–21 人周**，包含测试、文档和修复，不包含后续编辑/导出体系、目标设备等待和未知 CPU 的运行时移植。M0/M1 后根据选择、样式及复杂表格原型重估；完成标准以验收证据为准，不能因到达预计日期就标为完成。

| 阶段 | 初估 | 主要任务和交付物 | 依赖与退出条件 |
| --- | --- | --- | --- |
| M0 基线与关键原型 | 1–2 人周 | 固定官方版本/功能清单；核验依赖；跨段落→代码→表格选区、样式节点、图片锚点与不完整流式原型；性能方法 | 关键架构有可重复证据；未决行为、平台清单、首次预算和失败降级明确 |
| M1 文档模型与公共契约 | 1–2 人周 | 两个基础项目、快照/逻辑文本/身份/差异、统一选区、调度状态、API 与模板快照、基本测试 | 依赖 M0；后台/UI 边界、Unicode/复制、取消和服务所有权契约通过 |
| M2 基础原生呈现 | 2–3 人周 | 常规语法、列表/引用/代码、pipe 表格、图片与链接基础、基本主题、最小 Usage 和 Demo 页面 | 依赖 M1；真实 AXAML/MVVM 消费可运行；形成基础预览，但不宣称功能对齐 |
| M3 扩展语法与服务 | 2–3 人周 | grid 表格、任务/提示/脚注/Emoji等扩展、语义样式、图片覆盖、自定义节点接口、可选 TextMate 适配 | 依赖 M1/M2；支持矩阵逐项有语料；高亮及未知节点降级有结果 |
| M4 完整文本交互 | 2–3 人周 | 跨块选择、键盘/指针/自动滚动、全文/代码复制、脚注定位、链接与焦点、无障碍接口原型 | 依赖 M2/M3；离屏、表格、Unicode、更新和换肤组合用例通过 |
| M5 流式更新与阅读保持 | 2–3 人周 | 合并输入、后台解析、快照差异、乱序结果拒绝、跨线程会话、最终等待、底部跟随与逻辑锚点 | 依赖 M1/M3/M4；任意分块最终等价；无旧结果复现、丢尾块或上滚抢位 |
| M6 长文与质量完善 | 2–3 人周 | 视口呈现与回收、超长单块、布局/图片/高亮缓存、完整主题、资源释放、自动化/性能守卫 | 依赖 M2–M5；固定预算和回归通过；功能包自定义模板、服务共享不破坏状态 |
| M7 平台与发布验收 | 1–2 人周 | Windows/可用 Linux 真机、麒麟记录、独立 NuGet 消费、公共 API/迁移文档、既有控件回归 | 依赖 M6及相应设备；MD01–MD19 有结果；仅按实测平台发布支持声明 |

所有阶段持续补测试和样例。M3/M4 可以在模型和 Presenter 契约稳定后并行；M5 的输入调度可提前实现，但完整验收依赖 M4 的选区迁移；M6 的虚拟化原型从 M0 开始，最终回收必须通过选择/流式组合验收。

并行工作以模块契约稳定为前提：模型与解析、呈现与交互、主题与样例、质量与平台可以分工。文档位置、节点身份、变更应用顺序、服务接口及模板契约指定统一维护者；契约变更同步使用方和测试。多个开发者不能直接用人数平均分摊工期。

**统一 Demo、验证脚本与包消费**

- 在 `samples/NT.AvaloniaControl.Demo/Pages/Markdown/` 实现 `Navigation/IDemoPage`，在 `DemoPageCatalog` 注册唯一标识 `markdown`；沿用 `--page markdown` 的页面选择机制，Demo 引用功能/主题及可选高亮项目。
- 展示基础文档、扩展语法、复杂表格、中文/RTL/Emoji、图片加载失败、自定义 loader、高亮切换、跨块选择、流式对话、长文、样式覆盖及完整文本绑定。流式按钮使用本地固定语料和受控输入速率，不依赖外部 AI 服务。
- `Deactivate()` 停止 Demo 拥有的流式生产者和持续性能任务，取消当前捕获；`Activate()` 恢复页面功能但不重播旧追加；`Dispose()` 释放生产者、订阅和服务资源。页面状态与控件卸载状态分别测试。
- 控件卸载取消在途呈现工作和资源请求，并拒绝旧结果。重新挂载按保留的当前源文本和配置生成有效文档；重新挂载不恢复已失效的显式会话。只隐藏但仍挂载时可以继续接收和合并源文本，不强求视觉布局；再次显示按最新文档布局。Demo 页面停用仍主动停止其生产者，不能仅依赖 detach 事件。
- 提供 `samples/Markdown.Usage`：真实 AXAML、Compiled Binding、显式主题和 loader、MVVM 绑定以及可等待的流式示例。样例引用本库命名空间，代码标注新增 API 的使用前提。
- 把基础项目、可选适配、样例、测试和基准加入 `NT.AvaloniaControl.slnx`。扩展 `scripts/Verify.ps1` 的显式 xUnit v3 测试清单；沿用 locked restore、Release build、包打包及消费流程。
- 扩展 `scripts/Test-PackageConsumer.ps1` 的 `Control` 选项和消费者夹具以支持 Markdown，验证功能包自定义模板、主题资源 URI、Text 绑定、选择复制模型、流式完成和可选高亮包。
- 消费者沿用 `artifacts/consumer/<guid>`、独立构建/中央版本文件与全新包缓存，不引用源码项目或开发目录资源。真实平台剪贴板与联网图片不能由 Headless 假服务结果替代。
- Windows/Ubuntu CI 继续覆盖现有两个控件及新控件。平台探针按页面分发，保留 TreeDataGrid/VirtualKeyboard 的既有入口，不把所有页面强转为 Markdown 页。

**测试语料与验收矩阵**

测试数据使用可复现的本地语料、假图片/高亮服务、可控时钟和可控制完成顺序的调度器；少量集成测试再验证真实服务/平台。结构与逻辑文本快照负责语义回归，视觉截图负责排版，不以单一证据替代其它能力。

| 编号 | 场景 | 必须满足的结果 | 证据 |
| --- | --- | --- | --- |
| MT01 | 常规语法、转义、实体、空/null、CRLF | 归一化结构、文本和样式类别符合固定语料；源 span 与复制投影分别正确 | Core / Headless |
| MT02 | 嵌套列表、引用、松散列表、代码空白 | 层级、编号与间距正确；复制次序不随呈现顺序变化 | Core / 原生截图 |
| MT03 | pipe/grid 表格、宽表、单元格内联格式 | 尺寸/对齐/关系正确；横向滚动与跨单元格选择正确 | Core / Headless / 原生 |
| MT04 | 任务、五类提示、脚注及重复引用 | 只读状态、编号、定位和复制契约一致 | Core / Headless |
| MT05 | 段落→代码→表格→离屏段落选择 | 选区连续、Ctrl+A/C 与 `SelectedText` 完整；回收不丢文本 | Core / Headless / 真实剪贴板 |
| MT06 | 中文、代理对、组合字符、Emoji ZWJ、RTL | 命中和键盘移动在合法边界；无损坏字符，视觉顺序正确 | Core / Headless / 原生 |
| MT07 | 同一输入完整赋值与分块追加 | 小型对抗语料逐个分割点、随机多分块的最终文档、链接及文本投影等价 | Core / Headless |
| MT08 | 未闭合 fence、表格分隔线、列表续行、文末引用/脚注定义 | 允许中间状态变化，最终修正所有受影响块；前文链接不残留错误目标 | Core / 流式样例 |
| MT09 | CRLF、代理对或实体跨 chunk；空 chunk | 合并后与完整字符串等价；中间显示不永久丢字符 | Core / Headless |
| MT10 | 旧解析/图片/高亮晚于新文本完成 | 旧结果不提交，不触发错误的更新事件或重新挂载资源 | 可控调度 / Headless |
| MT11 | 并发/持续追加、解析慢于输入、重复会话、完成/取消、Dispose | 顺序明确，连续输入仍定期显示已完成前缀；最后一批提交，已结束会话拒绝输入，无死锁/无限 Task | Core / Headless |
| MT12 | 重复相同段落前后插入、选区锚点删除 | 身份迁移正确，选区不串到另一份相同文本；删除按契约回退 | Core / Headless |
| MT13 | 用户上滚、回到底部、图片在视口上方加载 | 跟随暂停/恢复正确；阅读锚点不被增长内容抢走 | Headless / 原生 |
| MT14 | 字体、窗口宽度、DPI、主题和高亮主题变化 | 缓存失效正确，布局和命中一致，文本与逻辑选区保留 | Themes / 原生截图 |
| MT15 | loader 后设置、单元素覆盖、BaseUri 改变、失败重试 | 服务优先级、重新加载、占位和尺寸正确；旧 URL 结果不复现 | 假服务 / 原生样例 |
| MT16 | 未知语言、无高亮器、失败和失效通知 | 纯文本降级、原文代码复制、主题重新高亮正确 | Core / 适配包 / Themes |
| MT17 | 卸载/重新挂载、隐藏、页面停用、模板更换 | 无旧会话复活、泄漏捕获或订阅；共享服务未被误释放 | Headless / 生命周期探针 |
| MT18 | 自定义 pipeline 与未知渲染节点 | 支持节点有 renderer；不支持节点按固定降级并提供诊断 | Core / 消费者 |
| MT19 | 功能包独用、主题包、TextMate 包独立消费 | 包内 AXAML/资源及传递依赖正确，隔离仓库配置仍可运行 | 独立 NuGet 消费者 |
| MT20 | 链接键盘激活、只读任务、文档/选区无障碍 | 可读名称、角色与文本范围符合发布的支持级别 | Automation / 真实辅助工具 |
| MT21 | 反复导航、替换长文、服务共享、持续流式 | 订阅、队列、资源和存活控件不持续增长 | 资源探针 / 基准 |
| MT22 | Windows/Linux/麒麟、100%/125%/150%/200% 缩放 | 字体、布局、命中、剪贴板和链接启动正确；不具备的组合明确待验证 | 平台记录 / 截图 |
| MT23 | 流式 `Text` 内部同步、TwoWay 同值回写、宿主外部替换 | 绑定不丢失、不重启自身代次、不误清选区；外部新文本优先，旧输入不能拼入 | 真实 AXAML / Headless |
| MT24 | 完成时隐藏、未挂载、缺失模板、解析失败、图片延迟 | 按契约正常完成/取消/错误；不无限等待布局，也不默认等待远程资源 | 可控调度 / Headless |

本库转换层使用 Markdig 的输入结果，但需要自己的渲染与投影断言，不能仅重跑 parser 测试就认为 Markdown 控件通过。平台快捷键、原生剪贴板、屏幕阅读器和中文字体必须保留真实系统证据；Headless 不作为麒麟真机结果。

**性能方法与初始验收预算**

M0 固定 Release、无调试器、SDK/框架版本、CPU/内存/显示后端、字体、缩放、视口、语料 hash、图片/高亮配置及预热规则。以至少五次独立运行比较中位数；需要 p50/p95 的场景每轮至少收集 100 个有效样本并报告样本数，不能从五个数据点推断稳定的 p95。冷启动、解析、快照转换、UI 提交、布局与实际首屏分开计时。累计分配、峰值内存、GC 和未释放资源分别记录，主动等待不能计为渲染速度。

首轮采用下列工程预算，**不是实测结果或产品承诺**。M0 报告若需要调整预算，须同时说明硬件、场景和调整原因，不在失败后静默放宽。Windows 与 Linux 分别建立基线。

| 场景 | 初始预算/约束 | 测量要求 |
| --- | --- | --- |
| 100 KiB 混合文档，1200×700 DIP、缩放 1 | 预热后的解析至首屏 p95 ≤ 300 ms | 本地数据，无远程图片等待；纯文本与启用高亮分别测量 |
| 1 MiB 混合文档、固定 100 个代码块 | 解析至首屏 p95 ≤ 1500 ms；UI 单批提交/布局 p95 ≤ 50 ms | 报告完整模型与可见呈现的成本，不只测 AST |
| 100 KiB 最终文档、20 个 chunk/s | 被合并批次从接收到可见的 p95 ≤ 150 ms；完成至最终应用 ≤ 500 ms | 延迟从批次最早尚未呈现的追加计起，不能重置为最后一个 chunk；另测 1 MiB 长会话退化 |
| 10,000 个普通块滚动 | 已呈现普通块 ≤ 可见块 + 两端各 8 块；空闲回收池 ≤ 16 块 | 超高块/表格单列测量；不以总视觉后代数代替块数量 |
| 100 次文档替换/导航/服务切换 | 活动解析 ≤ 1，最新待处理请求 ≤ 1；稳定后旧代次订阅/捕获为 0 | 控制图片、高亮完成顺序；检查重复回调和缓存持有 |
| 单个 100 KiB 代码块/段落、宽表 | 专门记录布局、滚动、选择和分配，M0/M2 固定退出预算 | 不用“块虚拟化已开启”跳过长单块验收 |

2026-10-14 补齐长单块退出预算（此前该范围仅有诊断记录，以下是首次固定的工程门槛）：100KiB单代码/单段落的接收至合成线程完成P95≤300ms、布局P95≤150ms；30×30宽表对应≤1500ms/≤500ms。三类均要求选择/滚动至合成线程完成P95≤150ms，每次有效文档更新的进程累计managed分配≤128MiB；分配不是存活/峰值内存。沿用五独立进程、每场景≥100有效样本及预热规则。保持原有混合文档UI≤50ms、100KiB流式150/500ms预算。

1MiB持续会话单独记录最早未呈现批次、源码增长四分区、解析/UI/布局、累计分配及峰值；最终Complete至提交P95≤1500ms，提交UI+布局P95≤50ms，完整投影/选区/代次/队列有界均须通过。该范围不套用100KiB的批次150ms阈值，亦不替代独立100KiB场景；四分区比值是退化诊断，不是无阈值的通过判定。

解析及归一化快照可以按文档长度增长，呈现控件与后台队列必须受视口/调度约束；不要把两种复杂度混写。若完整解析使长会话预算失败，M6 增加解析缓存/依赖失效方案并用 MT07–MT11 验证，不能以牺牲最终正确性换取更新速度。

自身回归调查阈值初定为：同机同配置耗时中位数同时增加 >20% 且 >5 ms，或累计分配增加 >20%；峰值内存/存活对象持续增长独立调查。阈值不能替代首屏、输入和稳定性预算。基准结果保存到 `artifacts/benchmarks/markdown/`，方法和汇总写入 `markdown-performance.md`，沿用现有 JSON 中文字段和真实数值/布尔值约定。

**主要风险与处理任务**

| 风险 | 应对和决策时点 |
| --- | --- |
| 跨块选择到后期才发现坐标体系不合适 | M0 先做复杂连续选区，M1 固定投影和位置；M4 完善交互 |
| StyledElement 样式、行内范围与 TextLayout 无法正确联动 | M0 验证附着/继承/选择器与失效；M1 固定支持边界，不靠默认主题掩盖缺口 |
| grid 表格、嵌套列表和长单块布局成本偏高 | M0/M2 使用对抗语料；M3 验收结构，M6 加缓存与必要的更细布局策略 |
| 长会话反复全文解析导致累计耗时增长 | M0 测量，M5 合并/复用，M6 按证据优化解析失效范围 |
| 节点重复及源位置变化造成错误复用 | M1 引入身份匹配及保守重建；MT10/MT12 验证，不只比较字符串 hash |
| 异步图片/高亮带来锚点跳动和旧内容回流 | 服务版本、文档代次与节点身份校验；完成后只重排相关区域并保持锚点 |
| 回收只顾性能，破坏全文复制和无障碍 | 选区/文本/无障碍模型独立于视口，M6 对照未回收结果 |
| 动态官方资料扩大对标范围 | 固定 12.3.0 清单；新增功能进入后续版本，现有差异持续公开 |
| Linux/麒麟字体、剪贴板和辅助工具不可用 | M0 调查、设备到位立即验证；继续其它开发，平台状态保持待验证 |

**完成标准与启动清单**

正式版本发布前，MD01–MD19 均有实现、样例和验收证据；标记为主要阅读能力的缺口不能以“后续优化”隐去。未来文档能力与未验证平台分别写入已知差异和支持列表。存在错误文本/链接复用、选区复制损坏、丢失流式尾部、过期任务回流、崩溃或持续资源增长时不得发布。

基础包、主题包及首版高亮适配的独立消费通过；公共 API、模板/样式资源与版本策略有快照；现有 TreeDataGrid 和 VirtualKeyboard 的构建、测试、Demo 及包消费回归通过。目标平台验收清单有结果，缺少麒麟设备时不宣称麒麟已支持。

- [x] M0 固定官方版本、MD01–MD19 对照清单、语法/渲染支持表和未明确的 symbol/HTML 边界。
- [x] 核验 Markdig 与高亮依赖，固定中央版本、许可通知和锁文件策略。
- [x] 验证复杂跨块选区、复制投影、样式节点、流式不完整结构和图片滚动锚点。
- [x] 建立两个基础项目、测试语料、最小消费入口和性能方法；输出 M0 结果并重估。
- [x] M1 固定文档快照/身份/位置/线程、服务归属和 API/模板契约。
- [x] M2/M3 完成常规与扩展呈现、主题和至少一种可选高亮适配。
- [x] M4 完成跨块选择、复制、键盘、链接及已公布支持级别的无障碍接口；真实辅助工具仍待平台验收。
- [x] M5 完成流式、最终等待、乱序拒绝及阅读保持，全部分块等价用例通过。
- [x] M6 完成本轮Windows缩放1与WSLg/X11已测软件组合的长文、回收、资源与主题质量验收，固定性能结果及差异；其它设备组合不据此通过。
- [ ] M7 可执行的软件验收、独立包消费、已有控件回归、当前两平台smoke及归档已完成；整体平台设备矩阵仍待验证，保留未勾选。

2026-10-14 按 [实施状态](markdown-status.md) 和 [功能/API 对照](markdown-official-alignment.md) 更新已有证据的勾选项。M6/M7 继续执行，宿主异常结果和资源清理的错误恢复也在进一步补测；发布、性能及缺设备平台不随功能阶段自动标记通过。

2026-10-15此前F832 / A50F里程碑进度：Windows[d2冻结Verify](../artifacts/verification/d2f591e51482493c9b62b4f642af789e/results.json)和Linux[6451实际SDK回归](../artifacts/linux-build/6451a248de3948f8baab4ee5f6ba0e0c/regression-summary.json)各907/907，387共同软件源逐条同SHA、无例外；Windows[F832原50ms五轮](../artifacts/benchmarks/markdown/windows-formal-f8324d-fcba9d4c612443c0bb682cd462532d5/guard.json)与Linux[A50F显式51ms五轮](../artifacts/linux-probe/dfc8c8cdb68349dda6f37615eea4c319/guard-policy-51.json)均实际通过。51ms仅适用用户明确修订的长会话全部实际提交/已绘制子集UI P95，原R4在50ms下失败及原raw布尔值保留。Windows[本轮43项smoke](../artifacts/markdown-m7-preparation/08ba3007b2fd4d09bf73a98cc62d4763/windows-smoke-ed4fe48322da4558b2b239c671918809/supervisor-result.json)与Linux[本轮43项smoke](../artifacts/platform/linux-markdown-smoke-a50f-00ca24d5178d454eb29e563d8887c157/evidence-summary.json)各Exit0，真实文本剪贴板复制及恢复均true。[134项修订守卫合同](../artifacts/markdown-budget-revision/guard-contract-20261015-715d222931f642219117de5749ec413f/results.json)实际134/134、Exit0；M7[软件归档](../artifacts/markdown-release-evidence/13c8c03113e64064ba54b7580cb620fb/manifest.json)及[51政策附录](../artifacts/markdown-release-evidence/13c8c03113e64064ba54b7580cb620fb/policy-support-eb118238cf2b46b2817b815849dd62a7/policy-support-manifest.json)均完成。M6按已测软件/性能范围通过，M7软件退出完成但整体设备矩阵保留未勾选；Published=false、VersionChanged=false；实体Linux、原生Wayland、物理输入、辅助工具、完整DPI及麒麟继续保留待验证范围。详见[性能记录](markdown-performance.md)与[平台验收](markdown-platform-acceptance.md)。

**参考与关联文档**

- [现有控件库说明](../README.md)、[TreeDataGrid 开发计划](treedatagrid-development-plan.md)、[VirtualKeyboard 开发计划](virtualkeyboard-development-plan.md)：功能/主题分离、独立包、Demo 生命周期及阶段验收约定。
- [现有性能方法](performance-baseline.md)：固定场景、多轮诊断、回归阈值与平台证据边界。
- [官方 Markdown 总览][official-markdown]、[控件 API][official-api]、[StreamingSession API][official-streaming]：主要阅读、选择和流式能力。
- [官方样式说明][official-styling]、[图片加载][official-images]、[代码高亮][official-highlighting]：主题与服务扩展基线。
- [官方 12.3.0 包与发行说明][official-package]：固定版本能力和后续 serializer 差异。
- [Markdig 项目][markdig]及[许可][markdig-license]：解析、扩展、源位置和发行通知。
- [Avalonia TextLayout API][textlayout]与[固定 Avalonia 12.0.3 源码][avalonia-source]：排版、命中及基础平台能力。

[official-markdown]: https://docs.avaloniaui.net/controls/data-display/text-display/markdown
[official-api]: https://docs.avaloniaui.net/api/avalonia/controls/markdown
[official-streaming]: https://docs.avaloniaui.net/api/avalonia/controls/markdownstreamingsession
[official-styling]: https://docs.avaloniaui.net/controls/data-display/text-display/markdown/markdown-styling
[official-images]: https://docs.avaloniaui.net/controls/data-display/text-display/markdown/imageloader
[official-highlighting]: https://docs.avaloniaui.net/controls/data-display/text-display/markdown/codehighlighter
[official-package]: https://www.nuget.org/packages/Avalonia.Controls.Markdown/12.3.0
[markdig]: https://github.com/xoofx/markdig
[markdig-license]: https://github.com/xoofx/markdig/blob/main/license.txt
[textlayout]: https://docs.avaloniaui.net/api/avalonia/media/textformatting/textlayout
[avalonia-source]: https://github.com/AvaloniaUI/Avalonia/tree/12.0.3
