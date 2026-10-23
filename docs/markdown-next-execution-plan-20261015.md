# Markdown 后续开发执行计划（2026-10-15）

状态：**计划已交付，尚未开始后续产品实现或新一轮验收。** 本文是后续逐项执行入口，不将计划项计为已实现能力。本次只读取说明、源码、测试和证据，并新增本文；未构建、运行长时间验收、修改产品、版本或发布，未启用、配置或触发远端 CI。

## 1. 优先级、路线与范围

执行路线为 **B0 核定执行底本 → L0 / I0 分项诊断 → L1 长块小范围优化与 I1 / I2 图片预检、累计资源控制 → L2 行级可行性决策 → C1 解析与匹配分配优化 → H1 独立验收宿主 → G1 联合冻结、复核及文档收束**。L0 / I0 的开发和分析可并行；同机性能采样串行。L2 原型只在决策通过后实施，不阻塞图片或 P2 工作。

| 优先级 | 阶段 | 目标 | 依赖 | 工作量估计（开发人日） |
| --- | --- | --- | --- | --- |
| 前置 | B0 | 明确本次输入、隔离目录、合同和证据来源 | 本计划 | 0.5–1 |
| P1 | L0 | 分解长块冷布局、重排和交互成本 | B0 | 1–2 |
| P1 | L1 | 依据热点减少重复布局准备及瞬时分配 | L0 | 2–4 |
| P1，有条件 | L2 | 判断块内行级虚拟化是否可行、值得采用 | L0，参考 L1 结果 | 评估 2–3；通过后原型另估 5–10 |
| P1 | I0 | 建立未知/不完整格式与多图真实峰值基线 | B0 | 2–3 |
| P1 | I1 | 扩大可信预检范围，保持原生兼容与后验复核 | I0 | 2–4 |
| P1 | I2 | 分别控制解码在途量和存活图片租约累计量 | I0；采用 I1 的可信尺寸结果 | 3–6，含设计决策 |
| P2 | C1 | 减少重复工作、身份匹配和临时分配 | P1 诊断完成；已有通过的候选稳定 | 诊断 1–2，实现与差分 3–6 |
| P2 | H1 | 完善 Markdown 独立演示/验收入口，减少跨控件依赖 | B0；最终合同跟随联合候选 | 3–5 |
| 集成退出 | G1 | 同一待交付快照的软件、原生与文档证据闭合 | 所有拟采用的候选及宿主就绪 | 准备/整理 2–3，另计实际运行时间 |

以上均为估计，不是承诺；不含设备获取、网络故障、需求决策等待和正式采样占机时间。L2 原型和 I2 的通用硬上限均没有预先保证的收益或可行性。各阶段诊断结束后先修订剩余估计；不将本表换算为确定完成日期。正式五轮按最近真实日志估计占机窗口，每轮现有长会话本身包含 110 个完整会话，可能约十分钟或更长；不得为赶时间缩减样本。

本计划必做的是测量、候选验证、明确采用/不采用的决策和证据收束。以下不纳入必做实现：原生 UIA TextPattern、任意 HTML/CSS 布局、数学、Mermaid、视频、编辑/导出等可选扩展。它们维持公开能力边界，新增需求另立范围。麒麟、实体 Linux、原生 Wayland、完整 DPI/多屏、物理键鼠/触屏、真实辅助工具及系统外链继续逐设备待验证；现有软件证据不代替设备证据。

远端原生 CI 为用户明确暂缓（`DeferredByUser`），不作为本计划各阶段的执行门槛，也不安排远端启用、配置、触发或以本地执行冒充远端通过。

## 2. 已核对的起点与不可变条件

### 2.1 当前通过基线

当前结论以 [M7 收束](markdown-m7-completion-20261015.md)、[最新软件退出记录](../artifacts/markdown-m7-completion/c63fab7542ba4b22b9a63d9cbb736f1b/m7-exit.json)及其[增补清单](../artifacts/markdown-m7-completion/c63fab7542ba4b22b9a63d9cbb736f1b/evidence-addendum/manifest.json)为准。M1–M7 首版可执行软件范围完成，M7 软件验收通过；该记录的 `CurrentProductFailures`、`CurrentPerformanceFailures` 均为空。后续优化不能被描述为修复当前已确认不合格版本。

| 范围 | 当前结论与来源边界 |
| --- | --- |
| 冻结源码 | [revision2 清单](../artifacts/markdown-wrap-fix/4e635a31e1de441bbfa4cc4b78abca88/revision2/revision-source-manifest.json)，Windows `revision2/source` 与实际 Linux SDK `revision2/linux-sdk/source` |
| Markdown 五项目 | 两平台各 544 项；不是当前整仓总数，也不沿用历史 907 项 |
| 既有兼容六项目 | Windows / Linux 各 416 项；不含当前并行 PDF/Charts 的新全仓结论 |
| 三包消费、API | 功能独用 78 项、主题/TextMate 80 项，各 18 项内嵌 AXAML；API/默认值已核验 |
| 正式性能 | 两平台各完整五轮、独立守卫通过；Windows 长会话两项 50ms，Linux 两项 51ms |
| Windows 原生 | 43 项通过，真实复制、读回及原文本恢复通过 |
| 新 Linux 私有 Xvfb/X11 | 43 项通过，producer / Main / 外层实际退出均 0；与 WSLg 性能环境分别记账 |
| 原 WSLg 共享剪贴板 | 未知非文本原内容无法安全保存，旧 Pending/失败退出保持历史事实；不称该环境已通过同一项 |
| 证据执行方式 | 五轮、544×2、416×2、包/API、Windows43、85/134 工具合同主要按同源 SHA 复用；最新新增执行是私有 Linux X11 剪贴板43。不能称本次全部重跑 |

参考产品 SHA：Windows Core `1AEF42AB16F6133750047AD4708A4ECB8D8110A5806722F6421D21FD64478F4A`；Linux SDK Core `A8A4FA5A295606E52925922EA0857E713B0172633A70A95733136A9E019FC8A7`。两平台分别编译、分别绑定，不能要求或假装两端 DLL 相同。

本次计划编制做了只读 SHA 核对：冻结 Windows 与 Linux 源码各 891 项均匹配 revision2 清单；[最新专属输入绑定](../artifacts/markdown-m7-completion/c63fab7542ba4b22b9a63d9cbb736f1b/live-markdown-source-verification.json)中的当前 160 项均匹配冻结 SHA；增补清单的 43 份复制证据均匹配其记录 SHA。此核对只证明这些文件一致，不是重新运行 544 项、五轮或全部增补检查。

计划读取时的记录 SHA256：

| 记录 | SHA256 |
| --- | --- |
| `m7-exit.json` | `E191C2FEA216FB91331382EFCE73124310D8D579E850858AABF835A95C900815` |
| `evidence-addendum/manifest.json` | `04940995B8B35E0941D84710430FBAC670898B8E6C925F740486F270DBE57FA4` |
| `revision-source-manifest.json` | `71667BED60BACD2B09F7FE5B92E7331DE2D9D7AACBB027AF411EA4ED60A1ECD4` |

已阅读 [公共契约](markdown-contracts.md)、[当前状态](markdown-status.md)、[后续历史记录](markdown-followup-20261015.md)、[性能方法](markdown-performance.md)、根 README、构建/包配置及 `.editorconfig`；仓库适用目录未发现 `AGENTS.md`。历史段落的候选、失败、旧五轮和总数各自保持原绑定，不能覆盖上述最新结论。

### 2.2 固定合同与预算

语义以 [markdown-contracts.md](markdown-contracts.md)为 oracle：重复节点身份保守性、晚到引用、脚注编号和复制投影、UTF-16/字素位置、字体更新、原生布局几何、命中/选择/阅读锚点、取消/旧代次拒绝、资源租约所有权均不得改变。API/默认值无意外变化；内部 DTO 复用不增加公共引用相等保证。

预算的完整权威是 [性能守卫](../scripts/Test-MarkdownPerformance.ps1)和既有正式原始报告，本表仅列便于执行的主要项，未列项也保持原值：

| 项目 | 固定要求 |
| --- | --- |
| 长会话 ALL 实际提交及 Rendered 子集 UI+同步布局 P95 | Windows 各 ≤50ms；Linux 各 ≤51ms，仅这两项适用 51 |
| 1MiB 混合首屏两模式 UI P95 | 两平台仍 ≤50ms，不能扩展 Linux51 政策到阅读场景 |
| 100KiB / 1MiB 混合首屏至 Rendered P95 | ≤300 / 1500ms，纯文本/TextMate分别判断 |
| 100KiB 短流最早未显示 chunk 至帧 P95 / 每次 Complete | ≤150ms / ≤500ms |
| 1MiB 长流每次 Complete | ≤1500ms；长流最早至帧与四区退化比保留诊断，不添加虚构预算 |
| 100KiB 单代码长行、单段落 | 首屏 P95≤300ms、布局 P95≤150ms、选择/滚动至帧各 P95≤150ms |
| 30×30 宽表 | 首屏 P95≤1500ms、布局 P95≤500ms、选择/滚动各 P95≤150ms |
| 三组长块每有效文档累计 managed 分配 | 加载与两次交互合计最大值 ≤128MiB，不能用 P95 掩盖单份超限 |
| 调度与虚拟化 | parse/pending≤1/1，资源执行/排队≤4/256；realized≤visible+16、retained≤16；原稳态释放计数归零 |
| 默认图片输入限制 | 编码16MiB、解码32Mi像素；不改成自动缩图，不等同于 native 峰值保证 |
| 正式采样 | 每平台五独立进程、完整11组；每组阅读/长块100有效样本、短/长流各100完整会话及至少100批、10完整预热；保留原守卫容差 |

保留 receipt、最早接收时间、SourceVersion、ALL 与 Rendered 分别统计、Complete 计时边界及 .NET TimeSpan 量化规则。Linux 使用显式51政策独立重算，producer 的原50声明/raw/旗标保留；不重写历史原报告，不通过扣除 GC/调度暂停、扩大容差或预算接受差分错误。

## 3. B0：执行底本、隔离和诊断准备

目标：让后续每个候选具有可追溯输入，并避免并行工作改变运行中的文件。非目标：重新验收当前已通过版本、整理或提交他人改动。

1. 开始实施时重新读取本计划、最新状态与 `git status --short`，检查是否出现更细目录说明。记录本阶段许可范围、时间、工具版本、当前可用空间及工作区变化清单。
2. 从保留的 revision2 建立**新的开发快照**，采用明确覆入清单纳入本阶段 Markdown 改动。当前160项专属输入一致不代表当前公共配置/整个解决方案与冻结版一致；中央 props、锁文件、slnx、宿主/脚本和并行模块分别列差异与来源。
3. 保留两种 manifest：Markdown 构建闭包（含公共配置、锁文件、资源、测试/宿主/工具）与待交付整仓集成快照。缩小前者必须列出排除项及理由，不可冒称整仓冻结。当前原生入口会扫描整个 `src/samples/tests/scripts/benchmarks/docs/.github`，并行变化被拒绝是正确结果，不能关闭来源守卫。
4. Windows / Linux 各自 obj/bin、运行目录和进程环境隔离；复用已核验 SDK/ICU/fontconfig 等工具，记录实际路径和 SHA，不另安装同版本 SDK。Linux 编译、Windows IL 在 Linux 运行、NoBuild 宿主组合分别标识。
5. 每阶段先写诊断协议：语料/fixture SHA、主指标、输入规模、有效样本条件、对照顺序、环境、扰动检查、拟验证风险；新增指标先标诊断，不偷偷变更正式预算。

交付：`start.json`、输入与覆入 manifest、合同/预算快照引用、磁盘预检、阶段诊断协议。退出：输入闭包可复现、并行变更未混入、输出/回退路径明确。发现来源变化则停止本次运行，保留记录，重新生成候选；不从共享工作区强行继续。

## 4. P1 长块：L0 → L1 → L2

### 4.1 现有实现与测量缺口

涉及 [MarkdownTextPresenter](../src/NT.AvaloniaControl.Markdown/Presentation/MarkdownTextPresenter.cs) 的 `CreateTextLayout`、[MarkdownDocumentPresenter](../src/NT.AvaloniaControl.Markdown/Presentation/MarkdownDocumentPresenter.cs) 的实现范围/测量/命中、[MarkdownCodeBlock](../src/NT.AvaloniaControl.Markdown/Documents/MarkdownCodeBlock.cs) 的高亮 Run 重建、[文字属性缓存](../src/NT.AvaloniaControl.Markdown/Presentation/MarkdownTextRunPropertiesCache.cs)、[行索引](../src/NT.AvaloniaControl.Markdown/Presentation/MarkdownSelectionLineIndex.cs)和[范围几何](../src/NT.AvaloniaControl.Markdown/Presentation/MarkdownTextRangeGeometry.cs)。

当前是块级回收；一个已实现长块仍创建完整原生 TextLayout。视口行索引仅缩小选区几何查询，不减少冷塑形或完整文字绘制。代码默认 NoWrap 并有横向滚动；[正式语料](../benchmarks/Markdown.Benchmarks/NativeHost/CorpusFactory.cs)的100KiB代码是**单条长行**，单做垂直行级虚拟化不会降低这条行的塑形工作。

### 4.2 L0：分项诊断，先不改布局策略

实施顺序：

1. 在独立诊断副本记录 `CreateTextLayout`、Run构造/硬换行适配、Measure/Arrange、文档范围细化、选区几何/绘制、高亮落地的次数、耗时和分配；用 SourceVersion、块ID、长度、原生行数及宽高约束关联，标清嵌套项，不能直接把嵌套耗时求和。
2. 保持正式语料原样；另加100KiB/1MiB单段落、单超长行/同字节多行代码、纯文本/高亮、少/密集Run、中英混排、bidi、组合符/ZWJ、首中尾空LF诊断组。
3. 分开记录新 presenter 的首次布局、进程首样本、固定预热后的样本、同宽重复测量、宽窄往返、字体/主题变更、Measure后直接Arrange、高亮更新、流式追加、全选与横纵滚动。不得把暖路径收益称作进程冷启动收益；不人为清系统字体缓存或强制GC制造对照。
4. 用可关闭的轻量探针；避免逐字符日志和逐行分配，量化开关扰动。带探针DLL独立SHA、仅作诊断；正式候选移除探针或证明默认关闭，并绑定实际产物。

交付：两平台同源原始对照、分项/规模增长表、探针扰动说明、排序后的候选及预期验证指标。退出：足以区分完整塑形、重复准备、范围收敛与交互几何的成本；证据不足时明确继续测量，不先宣称唯一瓶颈或确定收益。

### 4.3 L1：一次一个成本点，保留原生完整路径

先按 L0 选择收益最大的局部候选：同一次同步提交内重复扫描/临时列表；相同有效状态下重复初始化；高亮 span 校验/Run 构造分配；范围细化中的重复工作。先做一个候选、相关差分和局部原生对照，再决定下一个，避免多因子同时变化。

文字属性共享继续最多32项、限单次布局创建、finally清空。禁止把 FontManager/文档/样式引用未变当作字体未变证据；同键字体集合可能公开替换。不得直接删除第二次原生 Arrange 布局、跨布局保存已解析字体/字形，或借减少原生查询改变几何。

每个新增复用结构须写明 key、容量、owner、精确输入和失效条件；优先单次调用的纯数字/短命数据。重新测量、宽度/文字/行内属性变更、字体更新、换owner/文档、卸载、未知自定义呈现均有失效或完整原生回退。比较同源前后相同场景，报告全部有效样本、P50/P95及分配，不从墙钟扣除暂停。

交付：单候选改动说明、差分结果、局部原生报告、收益/代价与采用决定。退出：合同和资源无差异，重复对照支持改善且未新增未解释回退；无稳定收益或差分失败则撤回候选，保留原实现和失败证据。进入联合版本仍须 G1，局部100/10不等于完整五轮。

### 4.4 L2：块内行级虚拟化决策门

非目标：按固定字符数拆成多个TextBlock、自动改变段落结构，或以大致相同的外观替代原生几何。先审查固定版本可用的原生 formatter/行对象能力及其释放规则，不假定 TextLayout 已支持惰性布局。

依次评估：多行NoWrap代码的逻辑行局部实现；软换行段落的上下文和换行依赖；单超长NoWrap行的单行成本。后两者分别取证，不能转签多行代码收益。设计说明必须回答：全文UTF-16与字素映射、可见/预读行容量、离屏精确位置、总高度与水平extent、前后跳转/选择/复制/链接脚注、锚点、高亮、字体变更、卸载释放如何保持。

通过条件：原生塑形上下文、ligature/bidi/回退字体、可变行高和换行结果均有可验证等价路径；滚动总几何和锚点不依赖会跳变的近似高度；缓存与资源可有界且失效明确。非零行距、截断/MaxLines、未知行内、自定义渲染、变换等未证明路径继续整块原生回退。

交付首先是 `line-virtualization-decision.md`（采用/继续研究/不采用、证据、适用范围和成本）。只有通过决策门才制作可关闭的私有原型并扩展行窗口边界测试；原型通过后才纳入联合候选。无法证明等价或没有实测收益时，以保留 L1 和公开当前边界退出，不强行上线原型。所有原型、失败及决策保留。

## 5. P1 图片：I0 → I1 → I2

### 5.1 现状与边界

[MarkdownImageDimensions](../src/NT.AvaloniaControl.Markdown/Services/MarkdownImageDimensions.cs)仅信任完整CRC有效PNG IHDR及明确8bit JPEG SOF0/1/2；未知、截断、DNL/DHP/EXP和有歧义布局保守回原生解码。[MarkdownImageLoader](../src/NT.AvaloniaControl.Markdown/Services/MarkdownImageLoader.cs)先有界读取，再预检、Bitmap解码、像素后验复核与超限释放。不能将当前单PNG峰值改善称作所有格式保证。

[资源调度器](../src/NT.AvaloniaControl.Markdown/Services/MarkdownResourceScheduler.cs)的4/256是每Reader实际调用/排队界限，成功交付lease后归还执行槽；它不限制已交付存活位图或多个Reader的累计内存。[MarkdownImageLease](../src/NT.AvaloniaControl.Markdown/Services/MarkdownServices.cs)释放幂等且尊重`ownsBitmap=false`；[MarkdownImage](../src/NT.AvaloniaControl.Markdown/Documents/MarkdownImage.cs)以代次、挂载和取消检查拒绝旧结果，关闭控件不Dispose宿主服务，Complete不等待图片。

### 5.2 I0：先补真实格式与累计峰值基线

使用独立图片probe，按fixture SHA记录编码长度、声明/实际尺寸、预检分类、有界读取/编码缓冲、metadata/native decode耗时、提交、lease存活/释放、结果/异常分类和重编码像素SHA。PNG/JPEG/BMP为已知对照；GIF/WebP/ICO、多帧、方向元数据等只在实际后端支持时纳入，不承诺任意格式支持。

覆盖小图、接近/超过编码或像素限额、每个必要头截断点、坏CRC/长度冲突/整数溢出/伪魔数、含歧义帧的输入；file/avares/HTTP声明与未知长度全部保留。每个压力场景用独立新进程：单超限图、4个同步解码、超过执行/排队边界、顺序交付但持续持有多lease、两个Reader共享/不同loader、取消/替换/卸载循环。

保留OS lifetime `PeakWorkingSet64`、调用前后当前工作集、managed累计分配/非强制GC堆估算、在途/预留量、存活lease数。固定初始化与预热，保留原峰值和起点；峰值工作集包含全进程，差额不是精确native分配。`width×height×4`仅是调度估计，不是decoder临时量、多帧或进程硬上限。旧图片probe只复用方法，不能将旧候选结果签给新DLL。

交付：fixture索引、真实兼容/峰值矩阵、三类占用（编码、解码临时、存活位图）分项和候选优先级。退出：实际decoder行为与峰值来源明确；未实测的格式和指标标为未知。

### 5.3 I1：可靠预检，按格式逐项扩展

1. 先研究固定Avalonia后端是否有与实际decoder同源、无需完整像素分配的metadata能力。当前功能包仅引用Avalonia/Markdig，不假设可直接调用SkiaSharp；如需新依赖，先提交必要性、许可/锁文件和平台成本设计，不顺带升级依赖。
2. 内部诊断区分可信尺寸、未知、不完整、有歧义。手写解析仅在必要头、长度/偏移、overflow、容器画布/帧意义与native一致性均可验证时采用；识别魔数不等于可靠预检。
3. 每格式单独差分：预算内原可解码图片继续真实Bitmap解码和后验复核，尺寸/重编码像素/错误分类保持；未知或不足以判断的头继续原生兼容路径。无自动缩图，无默认静默拒绝未知格式。

交付：每格式适用条件、实现及差分、真实native峰值前后报告、剩余未知矩阵。误拒绝、尺寸分歧或不能可靠识别时撤回该分支，返回原生路径；不能把猜测尺寸写成安全保证。

### 5.4 I2：累计资源控制，分别处理两种生命周期

先出设计，再写候选：

| 层次 | 预期方向 | 必须证明的事项 |
| --- | --- | --- |
| 在途解码及编码缓冲 | 评估loader范围已知尺寸加权admission、未知格式独立解码闸门、有界编码输入保留量 | 等待取消/失败归还、同步native不可抢占、跨Reader公平性、与原4/256及高亮共享槽无饥饿/互锁 |
| 已交付且存活的位图 | 评估内部reservation随最终lease Dispose归还，覆盖同loader跨Reader | 幂等、迟到lease、`ownsBitmap=false`、loader释放后已交付lease可用、所有权与错误分类不变；满额全部被存活lease持有时有明确可推进/失败策略 |

不能把解码串行化当作存活位图总量控制，也不能把像素估计当作native临时分配上限。未知metadata仍可能先解码后超额；降低叠加不证明单图或任意格式硬上限。只对明确范围/前提给可证明边界。

reservation的可证明作用域仅覆盖参与计账、且能够观察最终资源释放的资源。`ownsBitmap=false`的lease Dispose不代表宿主bitmap已释放；自定义/不同loader及宿主另持有的位图也不自动受该账本覆盖。lease计数归零不能等同于进程native/GPU内存归零或通用硬上限。

默认可接受格式、有效lease生命周期、资源执行/排队规则均保持原合同。需要新增公开策略、拒绝默认可加载输入、改变等待/错误行为或驱逐有效lease才能成立的方案，先交具体API/行为与需求决策，不自动实施。无法在当前合同内实现通用硬上限时，交付“测得改善范围＋不采用方案理由＋保留边界”，不宣称已经解决通用硬上限。

实现前补取消点：读取前/中、预检前/后、admission等待/已预留、native解码期间及返回后、UI提交前、交付后卸载。检查stream、permit、reservation和lease各释放一次；旧epoch成功/失败不覆盖新服务，非合作服务真正返回前仍占槽。用满额持续持有、共享loader、局部null/继承切换、Reload、虚拟回收、阻塞/抛错取消回调验证无互锁。

交付：设计决定、容量/作用域/估计误差说明、状态机和失败策略、资源差分与多图峰值记录。退出：相关合同全过、释放必需项归零、没有新增等待死锁/饥饿，测量支持所声明范围；否则回退最后已验证的I1/基线。最终产品改动统一进入G1。

## 6. P2 C1：解析、身份匹配与分配

现有 [Parser](../src/NT.AvaloniaControl.Markdown/Parsing/MarkdownParser.cs)仍每次完整Markdig Parse；[NormalizationCache](../src/NT.AvaloniaControl.Markdown/Parsing/MarkdownNormalizationCache.cs)复用归一化结果，不能称已经跳过AST解析。[Matcher](../src/NT.AvaloniaControl.Markdown/Core/Documents/MarkdownDocumentMatcher.cs)已有空/单/≤4同胞特化及严格不可变DTO复用；[IdentityIndex](../src/NT.AvaloniaControl.Markdown/Core/Documents/MarkdownSnapshotIdentityIndex.cs)已有弱快照索引。[更新入口](../src/NT.AvaloniaControl.Markdown/Controls/Markdown.Updates.cs)后台Parse含匹配/索引预热，UI previous变化时仍可能二次Reconcile。

实施：先扩展[模型更新基准](../benchmarks/Markdown.Benchmarks/ModelUpdateBenchmarks.cs)现有Parse(previous)/Reconcile/MigrateSelection三场景，覆盖替换/前插/中删/追加、1/4/5/大量同胞、重复节点增删、引用/脚注晚到、未闭合结构、CRLF/Unicode、配置/转换器和并发previous。分项记录AST parse、缓存判定/失效原因、DTO转换、匹配、索引、UI二次匹配与选区迁移；线程累计分配和进程分配分开，模型10预热/100样本/五进程仍不代表UI预算。

按热点尝试一次遍历/定容索引、减少LINQ/候选数组、复用完整验证后的immutable结果。完整Markdig增量化、跨parser缓存、全局字符串驻留不预先列为确定方案。不缓存AST、UI context、callback、Brush、native资源或会保活大源码缓冲的slice。一次只改一个成本点，比较oracle后再纳入候选。

每项缓存/池化提交须附以下表项，不齐则不扩展复用：

| 对象 | 有效期与失效规则 |
| --- | --- |
| 归一化结果 | 保持同parser弱来源、显式previous、严格追加前缀、受支持默认配置；尾根重算；引用/脚注/动态URL/未知节点依赖继续veto；重新核验shape、source/text位置及投影，不只比较hash |
| 匹配临时索引/数组 | 优先单次Reconcile；重复数量变化、歧义候选与fresh ID规则保持；若池化，finally清引用归还、限制保留容量，异常/取消路径同样验证 |
| 快照身份索引 | 不可变快照弱键；重复ID异常、旧节点首个匹配、零点查询语义保持；并发发布完整，不持有snapshot/UI/AST |
| 字体/布局派生信息 | 沿用单次创建或当前精确layout弱关联；不扩大为跨布局已解析字体缓存 |

交付：成本/分配基线、缓存有效期表、单项候选差分/回收/并发报告、采用或放弃决定。退出：完整DTO、diagnostics、GetChanges、身份关系、正反向selection migration和基线oracle一致；没有新保活、计数越界或未解释性能退化。没有稳定收益则不采用；不得改期望值消掉身份/投影差异。

## 7. P2 H1：独立宿主与整仓入口

已有 [Markdown.Usage](../samples/Markdown.Usage/Markdown.Usage.csproj)提供独立AXAML/MVVM示例；[NativeHost](../benchmarks/Markdown.Benchmarks/NativeHost/Markdown.NativeBenchmarks.csproj)已只依赖Markdown三库。主要耦合是 [Test-MarkdownNative](../scripts/Test-MarkdownNative.ps1) 的Smoke仍选择综合Demo，而[综合Demo](../samples/NT.AvaloniaControl.Demo/NT.AvaloniaControl.Demo.csproj)引用其它控件。无需另写一套性能宿主。

1. 盘点[页面smoke](../samples/NT.AvaloniaControl.Demo/Pages/Markdown/MarkdownPage.Smoke.cs)和[图片smoke](../samples/NT.AvaloniaControl.Demo/Pages/Markdown/MarkdownPage.ImageLoader.Smoke.cs)的43项、fixture、协议/服务/图标、报告schema、剪贴板安全与恢复。优先设计独立`Markdown.Acceptance`宿主，Usage保留简明公开示例；新增项目/目录在设计评审后创建。
2. 将Markdown-only fixture、smoke逻辑与report writer抽取到非产品测试支持层，综合Demo通过适配调用；依赖保持产品→无测试宿主依赖，验收宿主仅引用Markdown三库/Avalonia。保留实际窗口、真实服务/剪贴板、截图与producer/Main/外层退出证据。
3. 新旧宿主对同一fixture证明43检查集合、限制字段、错误/退出语义一致；原剪贴板未知非文本时仍安全Pending，不强行覆盖。切换入口时同步扩展[原生工具合同](../scripts/Tests/Test-MarkdownNative.Tests.ps1)：host选择、必需DLL、source/runtime清单、NoBuild来源、超时及自有进程清理、严格报告校验。
4. 区分小闭包构建与整仓复核。[旧Linux入口](../benchmarks/Markdown.Benchmarks/NativeHost/Run-LinuxNative.ps1)Formal仍限定11回归项目；[当前Verify](../scripts/Verify.ps1)列14项目。H1先明确各入口实际覆盖和schema，再决定兼容适配，不能拿11项目报告冒充当前全仓。保留旧报告消费者的验证合同。

当前中央包配置已含Pkcs10.0.13、PDFsharp[6.2.4]及更新的PDFium；历史冻结底本的Pkcs8.0.1审计失败保留，但**当前是否仍阻断须用当前输入重新locked restore/审计确认**。不预判同一错误存在，不关闭审计/签名/TLS或顺带修改PDF包来通过Markdown验收。

交付：宿主依赖/fixture清单、等价对照、入口合同、独立构建/运行记录和Usage说明。退出：能从当前明确输入新编译Markdown验收宿主、通过已声明软件范围，来源不靠旧Demo NoBuild组合代替新构建；综合Demo集成另在G1验证。若迁移不等价，保留旧默认入口和新宿主试验，修复后再切换。

## 8. 各阶段共用的语义差分和资源测试

先补对应候选尚缺的边界断言，再实现；沿用既有oracle和原生对照，新增测试须验证风险，避免只镜像代码。每阶段先跑相关小闭包，再跑五个Markdown项目；产品进入联合冻结前核对原544项全部保留，新总数按实际XML逐项目报告。

| 范围 | 优先复用/扩展的测试与差分 |
| --- | --- |
| 原生文字/换行/字体 | [HardLineBreak](../tests/Markdown.Headless.Tests/MarkdownHardLineBreakLayoutTests.cs)、[PlainWrapped](../tests/Markdown.Headless.Tests/PlainWrappedLineBreakLayoutTests.cs)、[CustomWrapped](../tests/Markdown.Headless.Tests/CustomWrappedPlainTextTests.cs)、[文字属性差分](../tests/Markdown.Headless.Tests/TextPropertyReuseDifferentialTests.cs)、[缓存生命周期](../tests/Markdown.Headless.Tests/TextPropertyCacheLifecycleTests.cs)、[动态字体](../tests/Markdown.Headless.Tests/DynamicFontCollectionLayoutTests.cs)：原生行start/length、宽度/尾空格、baseline/height、逐位置命中与矩形；含同键字体集合替换和Measure后直接Arrange的原生行为 |
| 选区/锚点/生命周期 | [视口几何](../tests/Markdown.Headless.Tests/ViewportSelectionGeometryTests.cs)、[回退](../tests/Markdown.Headless.Tests/ViewportSelectionFallbackTests.cs)、[订阅生命周期](../tests/Markdown.Headless.Tests/ViewportSubscriptionLifecycleTests.cs)、[阅读重排](../tests/Markdown.Headless.Tests/ReadingLayoutAnchorTests.cs)：正反/部分/离屏/全选、空LF、RTL、局部样式、变换、自定义降级、图片增高及用户导航优先 |
| 行级原型新增风险 | 行窗口边界内外箭头/Page/Home/End/拖选，横纵滚动组合，离屏BringIntoView，跨窗口复制、末尾LF、ligature/bidi/Emoji跨切分、字体更新后的extent/命中；同时检查阅读锚点和Automation范围消费者 |
| 图片预检/协议 | [Preflight](../tests/Markdown.Headless.Tests/ImageLoaderPreflightTests.cs)、[Loader合同](../tests/Markdown.Headless.Tests/ImageLoaderContractTests.cs)：截断每个必要头、坏CRC/伪帧/溢出/DNL/DHP/EXP、协议有界读取、流关闭、借用HttpClient保留、预算内真实解码像素与分类一致 |
| 服务/取消/租约 | [资源调度](../tests/Markdown.Streaming.Tests/MarkdownResourceSchedulerTests.cs)、[元素服务](../tests/Markdown.Headless.Tests/ElementServiceTests.cs)、[资源生命周期](../tests/Markdown.Headless.Tests/ResourceLifetimeTests.cs)：不合作调用占槽、迟到lease、局部null/继承、双Reader共享、取消回调阻塞/抛错、卸载后订阅/旧回调/弱引用释放；I2新增所有reservation取消点和满额持有压力 |
| 解析/身份/投影 | [归一化复用](../tests/Markdown.Core.Tests/MarkdownNormalizationReuseTests.cs)、[小同胞](../tests/Markdown.Core.Tests/MarkdownSmallSiblingMatchTests.cs)、[单同胞](../tests/Markdown.Core.Tests/MarkdownSingleSiblingMatchTests.cs)、[身份预热](../tests/Markdown.Core.Tests/MarkdownIdentityPrewarmTests.cs)、[大迁移](../tests/Markdown.Core.Tests/MarkdownLargeMigrationTests.cs)、[迁移复用](../tests/Markdown.Core.Tests/MarkdownMigrationReuseTests.cs)：重复歧义、4/5边界、晚到引用/脚注、配置与converter调用、并发previous、parser/source buffer不保活、重复ID原异常/首个匹配 |
| 分块与公开消费 | [流式分块等价](../tests/Markdown.Streaming.Tests/MarkdownChunkEquivalenceTests.cs)、绑定/完成/自动化既有测试、[包消费入口](../scripts/Test-MarkdownPackageConsumer.ps1)及[API导出](../scripts/Export-MarkdownPublicApi.ps1)：全部复制投影、代次、TwoWay、Handled重入、默认值、内嵌AXAML与三包内DLL绑定 |

Headless差分不替代两平台原生窗口/字体几何与资源压力。新增图片/布局对象要记录创建、存活、释放/取消和容量；原100次替换卸载及1/1/4/256界限继续保留。分清累计managed分配、非强制GC存活估算、OS工作集和native相关峰值，不互相代用。

## 9. G1：重新冻结、完整五轮与整仓复核

### 9.1 重验判定

| 变化 | 所需验证 |
| --- | --- |
| 仅本文/说明、独立诊断probe；产品、正式测量链未变 | 文档/来源或probe校验；可按SHA明确复用既有证据，不启动产品五轮 |
| 仅新增/修改测试，产品与正式宿主/语料/守卫不变 | 跑相关及声明范围软件回归，记录新计数；旧性能证据可按SHA复用，不能称重跑 |
| 产品布局/Run/字体/高亮、parser/matcher/cache/index、图片生产预检/读取/解码、admission/scheduler/lease或有效默认配置变化 | 针对性差分通过后重新冻结；联合候选在两平台跑全软件、包/API、完整pilot及完整五轮与独立守卫 |
| NativeHost、语料、receipt/Rendered/Complete计时、formal参数、影响正式结果的schema/guard变化 | 测量链重新冻结、合同重验并完整新五轮；禁止与旧链轮次拼接 |
| 仅独立Smoke宿主/包装入口变化 | 新宿主等价、工具合同与两平台新43项；若证明产品三DLL及formal宿主/语料/计时/守卫不变，可明确复用五轮。无法证明隔离影响则完整重跑 |
| 公共配置/锁文件/依赖/工具链变化 | 分析实际依赖闭包及新编译SHA；影响Markdown或测量环境则按新候选完整重验，不能仅凭源码文字未变复用性能 |

实验候选先做定点诊断，失败及时停止；不要求每次小改动都立即跑五轮。可以把逐项已验证的小改动纳入一个联合候选，最终冻结后只验该确切组合；任何通过声明仅属于实际完成验证的SHA。已冻结运行目录不再增量覆写，尤其不能对通过的revision2调用增量Linux更新脚本。

### 9.2 最终执行顺序

1. 核定拟交付的完整输入、并行模块版本、环境与容量，停止共享构建。当前整仓以新快照做locked restore、Release零警告/错误构建和实际项目清单测试；先复核脚本覆盖，再报告总数。Markdown、历史Tree/Keyboard兼容、当前PDF/Charts分别列结果，不预先合并旧总数。
2. Windows、真实Linux SDK分别编译与回归；五Markdown项目和既有兼容原用例全部保留，failed/errors/skipped/not-run为0；新增项目按实际记录。完成三包打包、独立新消费者78/80基线用例及新增用例、18内嵌AXAML、实际API/默认值比较与包内DLL SHA绑定。无意外API差异。
3. 冻结源码与完整运行输出，保存产品三库、性能宿主/验收宿主、deps/runtimeconfig/native资源、包、语料、字体/后端SHA。两平台完整pilot用于发现语义/预算失败，探索报告明确非正式；必要时只跑对应100/10场景定位。
4. 在独占测量窗口按各平台同一完整副本执行11组×五独立进程，原样本/会话/预热、receipt和全部资源检查不变。Windows与WSLg分开；不得在采样同时构建、跑测试、profile或开额外原生探针。性能进度沿用真实进度日志，不制造固定等待收益。
5. 由独立守卫读五份raw重新计算。Windows默认50；Linux仅显式`-LongActualUiP95BudgetMilliseconds 51 -ProducerLongActualUiP95BudgetMilliseconds 50`。工具合同、raw/schema/来源、两种UI集合与资源全部通过才计性能退出；合成合同数据仅证明守卫拒绝行为。
6. 对最终SHA执行两平台原生43项。Windows原生与Linux私有X11真实剪贴板分别记录，实际producer/Main/外层退出0；安全无法保存的用户剪贴板继续Pending。恢复区分Demo进程内读回、外部observer和退出后Harness恢复，不能互相冒称。图像为Window RenderTargetBitmap时不称scan-out。
7. 独立核对源/运行文件前后SHA、报告与实际退出、包/API/消费关联、缺项和失败；生成新的阶段退出记录并更新当前文档入口。全程不改版本、不发布、不执行远端CI。

建议沿用的入口：`scripts/Verify.ps1`（当前整仓软件，先核实项目覆盖）、`scripts/Test-MarkdownPackageConsumer.ps1`、`scripts/Export-MarkdownPublicApi.ps1`、`scripts/Tests/Test-MarkdownNative.Tests.ps1`、`scripts/Tests/Test-MarkdownPerformance.Tests.ps1`、`scripts/Test-MarkdownNative.ps1 -Mode Smoke/Formal`及NativeHost现有平台入口。所有命令在**新隔离快照**工作目录执行，传入新输出/包目录；API先导出到阶段证据目录审阅，避免覆盖当前快照。入口细节经H1适配后记录实际命令，不将本节视为本轮立即运行指令。

### 9.3 失败与回退

来源变化、语义/几何差分、预算/分配/队列/释放失败均不放行。pilot失败停止扩大；正式任一轮失败保留raw/log/实际退出并停止后续轮，分析后产生新候选SHA，完整五轮从第1轮重来。不能挑选五个成功轮、混不同二进制或以小样本撤销正式失败。

候选回退只撤销本阶段登记的修改或回到其开发快照，不执行共享工作区reset/clean，不覆盖并行改动。旧版本/current revision2、候选失败、早期启动失败和环境Pending都保留。基础设施故障与产品失败分开登记，修复基础设施后另开attempt，不把原失败改为成功。整仓审计/构建若失败，先以当前日志定位责任范围，仍保留Markdown独立结论；没有全仓实际通过就不签全仓退出。

## 10. 测试成本与临时产物控制

计划读取时D盘空闲约23.37GB（21.76GiB），仅为容量观察，不承诺实施时仍有同量空间。本轮不清理任何现存目录。

后续每阶段采用以下规则：

1. 证据建议放`artifacts/markdown-next/<run-id>/<phase>/attempt-<id>`，临时构建在该run明确登记的`scratch`内；已有脚本固定输出到其它artifact根时保留其边界，以绝对路径和SHA在本run索引引用，不为统一外观改写旧目录。
2. 开始前量取源码闭包、Windows/Linux完整输出、消费者cache和预计日志/图片体积；计算`预计新增占用 + 10GiB空闲储备`，容量不足先停止新构建/长跑。10GiB是本计划的磁盘操作预留，不是控件性能/内存预算；根据实测更新预计占用，不能靠清理未知artifacts凑空间。
3. 常规开发只保留一份活动候选的Windows/Linux构建输出与一个对照输出；五轮复用各平台同一冻结runtime，不每轮复制源码、SDK、cache。正式不可变产物与新开发输出分开，试验不覆写已冻结副本。
4. 普通locked restore可复用核验过的受控包cache，Windows/Linux obj与assets分开；包消费者仍用全新隔离目录/cache，验证包的独立闭包，不能为省空间变成项目引用或复用旧构建。不禁用审计或改锁文件解容量问题。
5. 先差分/定点测试→小探索→软件联合冻结→完整pilot→正式五轮，避免为注定失败候选重复长跑。每次只运行一个原生性能进程，同机其它阶段分析可继续，禁止并行构建/采样干扰。
6. 运行时核对新增目录实际占用/空闲，达到预留线停止安排后续进程，保留已产生证据。大型逐行trace只在明确诊断attempt生成；原始数据不裁掉失败/慢样本，派生汇总可重建并标为临时。
7. 临时删除仅限本任务新建、登记为可再生、确认无进程占用且必要来源已归档的绝对路径。Windows删除前检查解析后路径仍在本run scratch白名单内、不是link/junction指向外部，使用单一PowerShell原生命令及`-LiteralPath`；先保存清理清单与释放量。不得枚举整个artifacts递归清理、跨shell拼接删除或删除当前通过版本、原始报告、失败记录、最终产物和冻结清单。

## 11. 交付物、记录更新与阶段台账

每个阶段独立交付：诊断协议/设计或决策、输入与产物manifest、实际命令和环境/工具版本、原始报告/XML/log/图片、对照与语义/资源结果、失败/Pending及回退说明，以及`phase-exit.json`。尚无实现或执行的字段为null/Planned，不填通过。

`phase-exit.json`至少包含：phase/attempt/status、目标与非目标、开始/结束时间、源manifest及SHA、覆入清单、各平台SDK/后端/字体、产品/宿主/包/API SHA、命令/实际PID与退出码/超时、逐项目计数、预算政策与独立guard、资源/峰值口径、原始证据路径、`NewExecution`与`ReusedEvidence`及复用理由、已知边界、失败归属/回退、下一阶段条件。`Passed`、`PassedWithinDeclaredScope`、`Pending`、`Failed`、`NotAdopted`分别表达，不用一个总true掩盖未实现能力或设备缺项。

文档职责：本计划维护任务状态与下一步；[status](markdown-status.md)只在实际阶段退出后更新当前入口/结论；[contracts](markdown-contracts.md)维护已核验语义与资源边界；[performance](markdown-performance.md)追加本轮方法/实测与新证据，原预算保留；[平台验收](markdown-platform-acceptance.md)/[矩阵](markdown-acceptance-matrix.md)按实际环境更新；[usage](markdown-usage.md)、[handoff](markdown-test-handoff-20261015.md)在宿主/API/操作方式实际改变后同步。M7收束与历史followup保持历史绑定，新增入口引用新记录，不改写旧raw/失败。README和全仓状态只在整仓事实可核对后收束，不把设备Pending写成全部完成。

| 阶段 | 当前状态 | 退出交付物 / 下一步 |
| --- | --- | --- |
| 计划编制 | 已完成 | 本文；只读源/证据核对，无产品或验收执行 |
| B0 | Planned | 新执行底本、清单、容量与诊断协议 |
| L0 / I0 | Planned | 分项/格式/累计峰值基线与候选排序 |
| L1 / I1 / I2 | Planned | 单项候选、差分、资源与采用决定 |
| L2 | Planned，原型有条件 | 行级go/no-go与适用范围；通过才实施原型 |
| C1 | Planned | 模型成本、缓存失效表与候选差分 |
| H1 | Planned | 独立宿主等价、工具合同与覆盖清单 |
| G1 | Planned | 新冻结、实际全软件/包/API/两平台五轮/原生记录与文档索引 |

本次停在计划交付。后续开始执行时先进入B0，逐阶段记录和验收，不因本文存在而自动启动全部优化。
