# PDF Viewer 性能开发与验收附件

本文是 PDF Viewer 剩余开发计划的性能附件，范围为 Windows 与 Linux 桌面。验收阈值仍为待校准方案。2026-10-15 的 P0 已新增最小后端基线：两个合成样本、各 7 次、1200×1600 栅格，记录加载 / 渲染 / 搜索 / 释放及进程内存采样，见 [P0 验证记录](pdfviewer-p0-validation.md)。该数据没有 UI 首次呈现 / 帧、峰值内存、真实生产语料、Linux 或官方同机 A/B，不能宣称达到本附件 SLA 或官方性能对齐；其余未标明执行的任务仍属计划。

按主计划本轮收口范围，P0 交付指标定义、同机同画质协议、最小后端工具与已有基线、初始真实语料和逐操作兼容边界。新增的 6 份 / 314 页真实出版语料用于验证接受 / 拒绝范围；未对它们执行完整性能场景，不能计入性能达标样本。P1 实现调度 / 生命周期和 Ubuntu 原生底座，P3 实现完整 present harness、UI / 峰值 / 分位数及官方对照，P9 完成全功能发布验收。上述后续工作不再作为新增生产化任务回灌 P0，最终门槛保持不变。[主计划收口范围](pdfviewer-development-plan.md#p0-当前收口范围与阶段迁移) · [兼容性边界](pdfviewer-compatibility-boundaries.md)

稳定任务编号为 **PERF01～PERF20**。主计划阶段为：P0 基线与关键 PoC、P1 公共契约/调度生命周期/平台底座、P2 完整阅读、P3 性能质量集中优化、P4 保存与 undo、P5 批注、P6 表单、P7 redact、P8 平台服务、P9 发布。性能工作估算已经分摊到这些阶段，不能再作为独立阶段与主计划重复加总。

2026-10-16 P1 已验证全进程串行、三队列 4:2:1 调度、按页搜索、排队取消及文档租约。真实队列繁忙时，控件 Close / Dispose 能让 UI 心跳继续，DisposeAsync 等待原生工作安全退出；非协作流工厂也只延迟等待完成，不同步占住 UI。Windows 与 Ubuntu 原生窗口分别产生缓存页和 CompositionBatch.Rendered 证据，详见[P1 记录](pdfviewer-p1-validation.md)。这些是正确性与生命周期结论，没有形成延迟分位数、FPS、峰值内存或官方同画质 A/B；P3 的预算和验收条件保持不变。

P0 已落地受限 FreeText 生产写入和静态 TrueType 自动子集。每次修改会在原生门外从完整源字体生成字形闭包子集，创建候选字节、结构重写并重开验证；不是字节增量编辑。三套真实完整 CJK 字体另有 Full 对照、重开更新与独立质量检查；NoSubsetting 保持全字体。仍需实测多批注 / 多次修改、源字体常驻、临时候选、解析与保存峰值；每次单进程耗时 / 总托管分配差值不是峰值、p95 或保存 SLA，文件体积降低也不能推导为 CPU / 内存同等降低。

## 1. 对标依据与结果表述

截至本计划编写时，官方公开了视口附近页面解码、缩略图按范围保留、预取与保留窗口、位图尺寸上限、HiDPI 渲染、诊断和 AOT 等机制；性能页没有公开首屏时间、FPS、p95、峰值内存或取消延迟 SLA。因此必须区分三类结论：公开机制是否覆盖、自身工程门槛是否达到、与指定官方版本的实测是否对齐。

官方机制基线：`PageRenderBuffer` 默认 2、范围 0～10；`PageRetentionBuffer` 默认 4、范围 0～20；页面位图最多 8192 像素/边且不超过 30MP；`MaxRenderScale` 上限 4。上述数字用于设计和画质对齐，不是官方速度保证。[官方：平台与性能](https://docs.avaloniaui.net/controls/data-display/pdfviewer/platforms-and-performance)

同一进程内 PDFium 调用应维持串行调度；不能为提高吞吐量直接让多个文档并发调用原生 API。[官方：PdfViewer 与线程约束](https://docs.avaloniaui.net/controls/data-display/pdfviewer/)

如没有可合法运行的官方控件授权、对应 runtime、版本固定的参考宿主或同场景测试条件，只能发布“自身性能结果”和“公开机制差距”。对应对照项标为 `blocked-reference`，不得写“性能已对标”“比官方快”或使用模拟值填补基线。

## 2. 当前实现的审计起点

以下是静态代码审计发现，不是实测瓶颈排名。P0 记录已有测量、风险和验证方案；P1 / P3 结合实际调度与桌面测量确定实现优先级。其中 UI 同步等待原生释放属于 P1 应优先消除的架构风险。

| 项目 | 当前实现 | 影响与计划 |
| --- | --- | --- |
| 输入数据 | 路径先 `ReadAllBytesAsync`；后端再次克隆并长期 pin；流通过 `MemoryStream.ToArray` | 大文件峰值可能有多份输入副本；实施 PERF04 |
| 原生调度 | P1 为进程内唯一串行队列，正文 / 搜索 / 缩略图 4:2:1，同级 FIFO | 已验证任务类公平与取消；按 viewer 轮转、合并、硬队列上限及观测仍归 P3 / PERF05 |
| progressive render | pause callback 主要检查取消 | 一次复杂页可能长占队列；验证安全暂停与抢占边界；PERF05 |
| 关闭/切换 | P1 同步入口立即失效会话并请求释放，租约结束后异步 close / unpin | 忙队列 UI 心跳及重入测试通过；DisposeAsync / CloseDocumentAsync 等真实释放，完整延迟分位数仍未测；PERF06 |
| 页面缓存 | 固定 96MiB，仅统计最终页面位图 | 未覆盖输入、暂存、上传副本、文字与缩略图；PERF07、PERF16 |
| 画质 | P3-03 起上限与官方同口径且可配置（`MaxRenderDimension` 8192/边、`MaxRenderPixels` 30MP、DPI clamp 1～`MaxRenderScale`），分层渲染（预览 / 全质量）目标光栅不降级取胜 | 旧 4096/4MP/DPI 1～2 状态已关闭；比较速度仍须先记录实际 raster 尺寸；PERF08 阅读侧已交付，同机对照归 PERF03 |
| 布局 | 缩放、旋转、宽度变化时生成全部页面 Rect | 大页数与连续 resize 有 O(N) 分配；PERF09 |
| 搜索绘制 | 每个可见页扫描全部搜索结果 | 命中量增加时重复扫描；搜索全部完成才返回；PERF10 |
| 文本选择 | 按 24 页缓存；指针更新时生成完整跨页选中文本 | 长选区可能重复提取与淘汰；PERF11 |
| 缩略图 | 最多 12 个 pending、64 张缓存，P1 已进入低优先级队列 | 按字节预算、方向预取与规模观测仍归 PERF12 |
| 多 viewer | 每实例有自己的缓存，共用串行队列和任务类公平规则 | 进程总预算、按实例轮转和完整可见性优先策略仍归 PERF13 |

审计涉及 `src/NT.AvaloniaControl.PdfViewer/PdfDocument.cs`、`Interop/PdfiumRuntime.cs`、`Controls/PdfDocumentPresenter.cs`、`Controls/PdfPageLayout.cs`、`Controls/PdfViewer.Document.cs` 与 `Controls/PdfViewer.Interaction.cs`。实施前应重新确认对应代码，避免按本表误改已经演进的实现。

## 3. 工作项、依赖与交付

| 编号 | 工作项与完成定义 | 主阶段 / 依赖 |
| --- | --- | --- |
| PERF01 | 指标契约与埋点：输入读取、打开解析、队列等待、原生执行、解码完成、位图上传、首次真实呈现、布局、搜索、取消、释放分别计时；记录 monotonic 时间与关联 ID，不把 `LoadDocumentAsync` 返回当作首屏 | P0 定义协议并保留已有后端计时；P1 / P3 补完整埋点；各项测量前固定对应定义 |
| PERF02 | 建立分层语料 manifest，记录 SHA-256、来源/许可、页数、大小、内容标签、预期行为、golden 版本；公开与私有脱敏语料分开 | P0 初始真实 / 合成语料与操作边界；P3 / P9 补生产规模、压力和异常样本；依赖 PERF01 场景定义 |
| PERF03 | 固定官方版本、合法参考宿主和同机同画质协议；输出可运行/被阻塞场景列表；记录自身初始目标、对标倍率和例外，取得有效参考条件后冻结可执行对照配置 | P0 协议与参考可用性清单；P3 执行对照及校准，P9 最终复验；依赖 PERF01～02 |
| PERF04 | 文档数据源：文件随机读或文件映射 PoC、可寻址流、不可寻址流临时文件策略；明确源所有权、生命周期、取消、临时文件回收；保留 byte[] 独立副本语义并避免路径加载重复复制 | P0 记录当前副本风险；P1 数据源 PoC / 底座，P3 调优；依赖 PERF06 |
| PERF05 | 全局串行 scheduler：可见页/交互优先，搜索/缩略图后台，任务去重、过期淘汰、viewer 公平轮转、队列长度上限；验证 progressive 安全点与重入限制；先支持取消过期低优先级工作，再考虑安全的时间片暂停 | P0 固定串行与取消约束；P1 方案验证和实现，P3 调优；依赖 PERF01 |
| PERF06 | 异步关闭与句柄 lease：UI 状态清理不等待 gate；取消后后台安全完成原生资源回收；打开/关闭/切换/卸载模板/detach/dispose 都禁止迟到结果覆盖新状态；保持向后兼容的 Dispose 契约 | P1；依赖 PERF05 的队列生命周期 |
| PERF07 | 按字节与总预算管理缓存：正文、预取、缩略图、文本、暂存、编辑历史分开记账；预取与保留窗口形成迟滞；提供预算/窗口配置与只读诊断；明确预算是硬上限、软上限还是含在途余量 | P1 契约，P3 实现；依赖 PERF05～06 |
| PERF08 | 画质与 HiDPI：在预算允许时覆盖官方 8192/30MP 上限与可配置 MaxRenderScale；可见页高清、预取低清、停滚升级、缩放复用旧图；高倍率降级显式记录；跨屏 DPI 后失效正确 | P2 基础，P3 优化（P3-03 已交付阅读侧：8192/30MP 可配置硬顶、可见高清 / 预取低清 / 停滚升级 / 缩放复用旧图、`DegradedPageCount` 显式记账、跨屏 DPI 失效重光栅；同机对照与指标记录归 PERF03 / P3-05）；依赖 PERF07、PERF17 |
| PERF09 | 大文档布局：预计算页尺寸/前缀高度/最大宽度，滚动定位保持 O(log N)，避免无变化时重建；连续 resize/缩放合并；降低 O(N) 分配并保持滚动锚点、混合页尺寸、旋转坐标正确 | P2，P3；依赖 PERF01、完整阅读布局契约 |
| PERF10 | 搜索：按页索引命中与高亮，仅绘制可见命中；增量发布首个结果/进度，支持取消和结果截断提示；背景搜索不饿死正文；可复用文本索引须计入 budget 并在编辑后失效 | P2，P3；依赖 PERF05、PERF07、PERF09 |
| PERF11 | 文本与跨页选择：字节预算/LRU、命中空间索引、拖动只更新区间与可见高亮，节流生成预览，复制时完整提取；验证超过 24 页、100 页、1000 页选区的取消、反向选择与文本正确性 | P2，P3；依赖 PERF05、PERF07、PERF09 |
| PERF12 | 缩略图统一调度：侧栏隐藏立即取消，按滚动方向预取，字节预算淘汰，旧代结果不可回写；高页数列表保持虚拟化，批注/旋转变化正确失效 | P2，P3；依赖 PERF05、PERF07 |
| PERF13 | 多 viewer：2/4 实例公平性、总缓存预算、前台/后台优先级、隐藏暂停、一个 viewer dispose 不阻塞另一个；记录各 viewer 队列等待和最长无进展时段 | P1 契约，P3 验收；依赖 PERF05～07 |
| PERF14 | 文档修改与缓存版本：页/文档 revision 纳入渲染和文本索引键；批注、表单、redact、undo/redo、保存重载采用局部或明确全量失效；事件批量合并，避免每笔输入全页重渲染 | P4～P7；依赖 PERF07～12 与编辑模型 |
| PERF15 | 保存与历史预算：记录序列化时间、保存峰值、原生/托管副本；undo 记录数与字节同时受控；redact 全文快照独立预算，超过阈值有明确策略；表单输入可合并撤销，保存不得长期占用 UI | P4，P6，P7；依赖 PERF04～07 |
| PERF16 | 资源审计：managed heap、native/private memory、RSS/working set、缓存、暂存、句柄、临时文件与可获取的 GPU 数据同时记录；分析“缓存稳定但进程持续增长”；不以强制 GC 隐藏正常运行压力 | P0 保留已有进程采样及未测清单；P3 实现完整审计，P9 全功能验收；依赖 PERF01、PERF06～07 |
| PERF17 | 真实桌面呈现与图像质量 harness：独立记录解码完成、UI 提交、compositor/render 完成、可验证的实际 present；用截图/平台 profiler/必要的视频核验区分；输出质量对齐证据 | P0 固定测量边界和方案；P3 实现 / 验收，P9 全功能复验；依赖 PERF01～03 |
| PERF18 | Windows/Linux 真机矩阵：相同语料、同质量、各平台独立统计；Windows DPI/跨屏，Linux X11 与 Wayland 会话/XWayland、字体、输入、剪贴板、GPU/软件渲染与原生依赖；原生 Wayland 仅另行升级试验；WSL 结果只作补充 | P1 底座，P3 调优，P8 平台服务，P9 发布；依赖 PERF17 |
| PERF19 | 可复现 benchmark 工具链：runner、场景脚本、raw JSON schema、聚合器、baseline compare、CI 轻量回归、发布报告；阈值版本化，失败保留原始记录/截图/trace | P0 骨架，P3 自动化，P9 发布；依赖 PERF01～03 |
| PERF20 | 8 小时长稳、100 轮开关、交替取消与 2/4 viewer 混合负载；保存/undo/表单/redact 完成后全量重跑；发布阻断项清零，例外必须有语料 ID、风险、原因与负责人 | P3 早期压力，P4～P8 增量，P9 最终；依赖 PERF06、PERF13～19 |

性能专项估算为 **25～41 人日**，分摊建议如下。包含正常的实现、审查、指标迭代与性能专项验证，不包含全部阅读/批注/表单/redact 功能本身的实现。

| 主阶段 | 已包含的性能工作 | 人日范围 |
| --- | --- | --- |
| P0 | PERF01～03 的协议 / 初始语料 / 可用性，04/05/17 的风险与验证方案，19 最小后端骨架 | 3～5 |
| P1 | 调度、异步生命周期、数据源与预算契约、平台底座 | 5～8 |
| P2 | 布局/搜索/文字/缩略图的性能结构 | 4～6 |
| P3 | 数据源、缓存、画质、调度、多 viewer 集中优化与真机回归 | 6～10 |
| P4～P7 | 修改失效、保存峰值、undo/redact 历史预算增量验收 | 2～4 |
| P8 | 原生平台服务集成后性能与资源复核 | 1～2 |
| P9 | 发布样本、长稳、质量门槛、最终报告 | 4～6 |
| 合计 | 已分摊，禁止与主阶段重复加总 | **25～41** |

## 4. 基准语料与场景

| 层级 | 样本内容与规模 | 验证重点 |
| --- | --- | --- |
| S0 正确性 | 1～20 页；文本、图片、crop box、固有/附加旋转、混合页尺寸、CJK、RTL、双栏、无文字、加密 | 坐标、文字、画质、错误处理；为性能测试提供正确性门禁 |
| S1 日常 | 20～200 页、1～30MB；手册、合同、图文报告，至少 20 份 | 首屏、跳页、正常滚动、缩放、搜索、复制 |
| S2 大文档 | 500～2000 页、100～500MB；扫描、混合内容、密集书签/链接、10K 搜索命中 | 分页调度、内存、长搜索、布局和高亮 |
| S3 压力 | 5000～10000 页、最大 1GB；超密矢量/CAD、透明度、字体复杂页、超大页面 | 峰值、退化曲线、取消、降级、错误终止；不并入 S1 统计 |
| S4 编辑 | 大量批注、表单、撤销重做、保存重开、redact | revision 失效、历史预算、保存峰值、结果可用性 |
| S5 生命周期 | 开关/切换/取消、detach/reattach、隐藏、模板变化、2/4 viewer | 资源释放、公平性、迟到结果、8 小时稳定性 |
| S6 异常输入 | 截断、损坏、错误密码、不支持文件、不可读数据源 | 有界失败、取消、临时文件清理和 UI 可恢复 |

一个样本可以带多个内容标签，但必须有唯一 corpus ID。每个样本至少记录打开、跳至指定未缓存页、往返滚动、快速滚动后停止、100%/200%/500%缩放、搜索首个命中/完整搜索、跨页复制、关闭。没有文字或命中的文档采用 `not-applicable`，不可记为 0ms。

本节是 P3 / P9 的完整性能语料和场景完成定义。P0 初始语料登记及有限正确性检查不等于 S0～S6 全覆盖；未执行的交互、规模、压力与异常场景仍标为未测。

跨页选择是当前实现应保留和加固的能力；官方文档列为现有限制的场景不能伪造官方对照值。Linux 内建打印/分享也不是官方现成能力，若本项目提供应单列平台增强。[官方限制说明](https://docs.avaloniaui.net/controls/data-display/pdfviewer/platforms-and-performance)

## 5. 同机同画质对照协议

1. 固定 CPU、内存、SSD、显卡、驱动、电源模式、OS、.NET、Avalonia、控件和 PDFium 版本，记录后台负载。固定 Release 发布方式、渲染 backend、窗口/视口尺寸、DPI、侧栏、视图模式、预取参数、搜索选项和源文件位置。
2. 默认参考视口建议 1440×900 DIPs，60Hz；测试 DPI 为 100%/150%/200%，HiDPI 单独统计。P0 冻结已有机器并登记缺失参考设备；P3 / P9 每次正式测量前冻结实际机器配置，不以规格相近的两台机器进行直接性能结论。
3. 官方与自研使用同一份语料、相同目标像素尺寸与可比较的质量设置。实际 raster 尺寸、缩放上限、DPI clamp 和质量降级都写入记录。P3-03 起默认上限与官方同口径（8192/30MP，`MaxRenderScale` 可配置；官方机制 `MaxRenderScale` 上限 4 可经配置设为同条件），未记录实际 raster 尺寸前仍不能直接比较速度。
4. 区分新进程首次打开、同进程重复打开、已解码缓存命中、未缓存页和已建立文本索引。新进程并不等于冷 OS 文件缓存。若确实测冷文件缓存，记录操作方法并控制对照顺序。
5. 采用固定且交替/随机化的实现运行顺序，预热另记。**每个语料、每个场景、每个 OS/桌面/backend/质量配置独立至少 30 次**；不得把不同语料混合得到一个总 p95。发布关键场景至少 100 次才报告相对更可靠的 p99；100 次仍需展示样本数和最大值，不能表示尾延迟已完全稳定。
6. 固定分位数算法，例如 nearest-rank，并记录版本；取消、错误、超时、缺失 present 不得静默删除。失败率与延迟同时报告，超时用明确 status 保留，不伪造完成时间。
7. 初始对照目标：S1 关键交互 p95 和同画质峰值进程私有内存分别不超过指定官方基线的 1.25 倍，同时达到自身绝对门槛。**1.25 是 P0 协议记录的初始工程目标，不是官方承诺；实际参考版本、配置和可执行阈值须在 P3 正式对照前冻结。** 每份文档逐项判断，不能靠其他简单文档平均抵消。
8. 不同平台分别形成结果。Windows 通过不代表 Linux 通过，Ubuntu X11 通过不代表 Wayland 会话/XWayland 通过，headless/WSL 通过不代表真实桌面通过。当前版本不纳入原生 Wayland 支持；升级后的试验单列。

## 6. 工程初始门槛

以下全部为 **P0 协议记录、P3 待测量校准并在正式验收前冻结的初始工程目标**，适用于约定参考机器上的 S1 普通文档。P0 不以达到这些发布性能为退出条件。S2/S3 按每份语料建立独立预算、曲线与降级策略，不混入 S1 p95。修改冻结门槛必须记录原因与新旧值，不允许在结果不达标后无说明地放宽。

| 指标 | 初始目标 | 测量边界 |
| --- | --- | --- |
| 首屏 | p50 ≤600ms，p95 ≤1500ms | 用户打开命令提交至首个可见页以目标清晰度实际呈现；分别报告输入读取/解析/排队/渲染/上传/present |
| 缓存命中跳页 | p95 ≤100ms | 输入到目标页呈现，不把 offset 设置完成作为终点 |
| 未缓存普通页跳转 | p95 ≤600ms | 目标页以对应质量呈现 |
| 连续滚动 | 60Hz 目标；帧间隔 p95 ≤20ms，p99 ≤33.3ms | 真实桌面 present；发布场景 p99 至少 100 个独立迭代，单次场景内也保留完整 frame 时间序列 |
| UI 长任务 | 组件自身单次 UI 占用不超过 50ms | 记录最大值和 >50ms 次数；OS/采集干扰须有 trace，不能直接删除样本 |
| 停滚后高清 | p95 ≤500ms | 最后滚动输入至可见页目标质量呈现 |
| 搜索首个命中 | p95 ≤500ms | 排除可配置 debounce 与包含 debounce 两种都报告，不能混用 |
| 1000 页纯文字完整搜索 | 初始单次预算 ≤10s | 对选定 S2 文档冻结；不适用于任意扫描或矢量内容；同时满足前台交互门槛 |
| 排队取消 | p95 ≤50ms | 取消请求至任务出队且不会执行 |
| 原生渲染取消 | 普通页 p95 ≤250ms；复杂页初始 p95 ≤1000ms | PDFium 安全点受文档影响；逐样本报告，不对任意 PDF 宣称硬实时上限 |
| 关闭/切换 UI 响应 | p95 ≤100ms | UI 清理和新状态可交互；后台原生释放另外计时，不同步等其他 viewer |
| 缓存预算 | 正文默认 128MiB、缩略图 16MiB、文本 32MiB，可配置 | 是各池初始目标；输入/native/解码暂存/上传副本/undo/GPU另计，预算允许在途余量须明确 |
| 8 小时长稳 | 无崩溃、死锁、迟到结果覆盖或无进展；错误可恢复 | 正常 GC 下记录完整趋势，不仅比首尾 |
| 100 轮开关 | 回到稳定空闲并完成后台释放后，私有内存残留增量 ≤20MiB 或基线 5% 中较大者；随轮数不持续上升 | Windows private bytes / Linux private RSS 与 managed heap 分别报告；统一冷却与诊断 GC 条件，正常运行曲线也必须保留 |
| 多 viewer | 2/4 viewer 不发生永久饥饿 | 分别冻结队列等待 p95 与最大无进展预算；前台延迟与单 viewer 对照，不仅记录合计吞吐量 |

峰值进程内存的绝对门槛须由 P3 根据 P1 数据源 / 生命周期方案、native 开销、GPU backend 和实际语料，在正式验收前校准冻结，P9 全功能复验；P0 记录缺失指标及测量方案。不能仅把缓存池相加作为进程 SLA。任何 30MP 页面都可能暂时同时存在解码像素、Avalonia 位图与上传副本；实际是否保留多份必须实测。

滚动统计采用固定时长、速度和路径的场景。每次迭代分别计算其有效呈现帧间隔的 p95/p99；门槛作用于该次迭代，发布阶段至少 100 次迭代中 ≥95% 同时满足两项门槛，其他迭代和全部 >100ms 卡顿仍逐条报告。再汇总各次迭代分位数的分布；禁止把所有迭代的帧合并后伪装成独立重复实验。无可靠 present、超时或采集失败的迭代不能充当通过样本。P0 固定这些统计原则，具体有效帧、暖场与采集精度由 P3 的真实桌面 harness 验证后、正式测量前冻结。

## 7. 图像质量门禁

性能对照先过画质门禁。不得通过减少像素、漏画复杂对象、缺字、跳过透明层或改变注释/表单绘制获得更低时间。

| 质量项 | 方案与初始验收标准 |
| --- | --- |
| 几何与分辨率 | 比较同页、同 viewport、同缩放/旋转/crop box、同输出像素尺寸；记录 raster、DPI、backend 和渲染 flags；页面边界和关键标记位移初始不超过 1 个目标像素 |
| 缺字与文字 | CJK/RTL/嵌入字体/字体替换样本有人工批准的预期文本与局部 golden；不得出现参考中存在而被测中缺失的字形、方框替代或明显错误字形；文字提取另验，不用提取成功替代视觉正确 |
| 透明度与混合 | 含软蒙版、渐变、透明叠加、图片 alpha、裁切与复杂矢量样本逐区域对照；背景色一致；不得有整层丢失、透明变黑、裁切错误和可见色块 |
| 像素差异 | 相同 PDFium/flags/backend 可采用逐像素/差分；不同抗锯齿或平台采用注册后的 SSIM/局部差分，初始建议 SSIM ≥0.995，P0 记录规则，P3 用目标平台样本校准并在正式验收前冻结阈值；不能仅靠全页平均分掩盖小块缺字 |
| 局部检查 | 对文字密集、透明、表单、细线、注释区域建立 ROI；同时保留原图、差分图与人工复核状态；自动分数和语义缺陷都必须通过 |
| 降级 | 超过像素/预算限制的降级另设场景，记录原因和实际质量；不能与未降级官方结果直接评判速度 |

参考图应包括官方运行截图与人工批准的独立样本预期。两者同用 PDFium 时可能共享缺陷，因此“与官方像素相似”不能代替已知正确性检查。平台字体差异必须作为独立配置记录；不能只通过放宽整页阈值掩盖缺字。

## 8. 真实 present、Headless 与测量工具边界

- Headless 测试可验证布局、调度、取消、revision、预算计数、生命周期和 deterministic 逻辑；可用于非 GPU 的回归趋势。它不能证明屏幕 FPS、GPU 上传成本、真实 present、DPI 跨屏或桌面输入延迟。
- `RenderAsync` 完成只表示原生像素准备好；UI 绘制提交或某个 render 回调也不必然表示屏幕已经显示。PERF17 必须分别命名这些指标，禁止用前者填入 `firstPagePresentedMs`。
- Windows 按实际图形 backend 选择可捕获的系统 graphics trace；Linux 按 X11、Wayland 会话/XWayland 及 compositor 选择 frame/presentation tracing，原生 Wayland 试验另记。工具无法覆盖时，采用可复现截图/高帧率视频验证和明确精度说明，而不是伪造 present 事件。
- 无法得到可靠 present 的记录标为 `present-unavailable`，仍保留 decode/submit 等数据，但真实呈现验收项未通过。图形采集本身的开销应在无采集对照中量化。
- UI 长任务、排队等待和原生占用使用应用内 monotonic spans；OS profiler 用于定位 native、GC、GPU/合成器问题。计时不得混用 wall clock 和 monotonic clock 相减。

## 9. 结果数据与目录交付建议

以下为 P3 / P9 完整工具链的计划目录，不表示已经存在。P0 已有最小宿主与初始语料位于 `benchmarks/PdfViewer.P0/`，由验证记录关联；其未实现的桌面采集、场景执行和聚合工作沿用 PERF 编号迁入后续阶段。正式性能目录在 P3 固定并由主计划链接，不要求为关闭 P0 提前创建全部产物。

```text
benchmarks/PdfViewer.Benchmarks/         # 可运行桌面宿主、官方对照适配层、场景执行器
benchmarks/pdfviewer/corpus/manifest.json
benchmarks/pdfviewer/scenarios/         # 场景输入、页码、滚动、缩放、查询与预期
benchmarks/pdfviewer/schema/            # raw-run.schema.json、corpus.schema.json
scripts/pdfviewer/benchmark.ps1         # Windows 构建与运行
scripts/pdfviewer/benchmark.sh          # Linux 构建与运行
scripts/pdfviewer/compare-baseline.*    # 固定 quantile、质量门禁和差异判定
artifacts/pdfviewer/<run-id>/performance/ # raw、trace、截图、diff、summary
docs/validation/pdfviewer-performance/ # 版本化摘要与可发布基线，不提交私有 PDF
```

`scripts/Test-PdfViewerPerformance.ps1` 是主计划规定的统一编排入口，可调用上述 `scripts/pdfviewer/` 中的平台 runner 和聚合器；不维护两套判定实现。建议 raw JSON 为“每个语料 × 每个场景 × 每次迭代一条记录”，采用 schema version；frame/native spans 可外置 JSONL/trace 并由 SHA-256 关联。聚合结果必须能从 raw 重建。

| 字段组 | 建议必需字段 |
| --- | --- |
| 身份 | `schemaVersion`、`runId`、`iteration`、`implementation`、可空的 `commit`、`isDirty`、`sourceManifestSha256`、`productAssemblySha256`、`nativeLibrarySha256`、`scenarioId`、`corpusId`、`corpusSha256`；未提交工作区必须用真实源清单标识，不能仅靠 commit |
| 版本与机器 | OS/发行版/内核/桌面会话、CPU/内存/GPU/驱动、.NET/Avalonia/PDFium/控件版本、backend、电源模式 |
| 条件 | 进程/文件缓存类型、viewport DIPs/px、DPI、目标 raster、render flags、预取/保留/预算、viewer 数、语料标签 |
| 状态 | `status`、错误/超时/取消阶段、`qualityStatus`、`referenceAvailability`；不适用字段为 null 并给原因 |
| 时间 | 读取/解析/队列/原生/解码/上传/提交/present/搜索/取消/释放等 ms，测量方法及其精度 |
| 资源 | 各缓存 bytes、input bytes、暂存、managed heap、private bytes/private RSS、working set/RSS、native handles、可获取的 GPU memory；单位写入字段名 |
| 质量与证据 | 实际 raster、降级原因、SSIM/ROI 结果、golden ID、screenshot/diff/trace 的路径和 hash、人工复核状态 |

结构示例仅说明字段和缺失值语义，不是测量结果；真实 schema 应限制枚举、数值单位、非负范围和必填关系：

```json
{
  "schemaVersion": "1.0",
  "runId": "<unique-id>",
  "iteration": 1,
  "implementation": "rt",
  "commit": "<sha>",
  "isDirty": false,
  "sourceManifestSha256": "<sha256>",
  "productAssemblySha256": "<sha256>",
  "nativeLibrarySha256": "<sha256>",
  "corpusId": "S1-manual-001",
  "corpusSha256": "<sha256>",
  "scenarioId": "open-new-process",
  "status": "not-run",
  "qualityStatus": "not-reviewed",
  "referenceAvailability": "blocked-reference",
  "environment": {
    "os": "<os-and-version>",
    "desktopSession": "<windows-x11-wayland>",
    "graphicsBackend": "<backend>",
    "machineProfileId": "<frozen-profile>",
    "runtimeVersions": {}
  },
  "conditions": {
    "processCache": "new-process",
    "fileCache": "uncontrolled",
    "viewerCount": 1,
    "viewportDip": [1440, 900],
    "renderScale": 1,
    "targetRasterPx": null,
    "qualityProfileId": "<frozen-quality-profile>"
  },
  "metrics": {
    "firstPageDecodedMs": null,
    "firstPageSubmittedMs": null,
    "firstPagePresentedMs": null,
    "peakPrivateMemoryBytes": null,
    "peakManagedHeapBytes": null,
    "queueWaitMs": null,
    "cancelObservedMs": null
  },
  "measurement": {
    "clock": "monotonic",
    "presentMethod": null,
    "presentUnavailableReason": "not-run"
  },
  "quality": { "actualRasterPx": null, "degradationReason": null, "roiResults": [] },
  "evidence": []
}
```

聚合器至少输出：每语料/每场景/每 OS 配置的样本数、成功率、p50/p95、符合采样条件时的 p99、最大值、质量门禁、取消/错误/超时数、原始数据链接；不生成掩盖文档差异的跨语料总 p95。普通 CI 使用小语料与宽松噪声门槛抓明显回退，真实发布门槛由固定机器运行完成。

## 10. 发布完成条件

1. PERF01～20 都有对应交付或带负责人/原因/风险的明确阻塞项；发布范围内不得保留严重度为 P0/P1 的性能与质量阻断缺陷。
2. Windows 与目标 Linux 真机矩阵各自完成质量、延迟、资源、取消、2/4 viewer、8 小时与 100 轮验证；WSL/headless 结果作为补充附件。
3. 保存、批注、表单、redact、平台服务完成后重新运行受影响场景和最终回归，确认 undo/快照/缓存失效没有引入内存与调度退化。
4. raw JSON、语料 manifest、脚本、版本、配置、截图/差分、trace 与报告可追溯；报告清楚标注未测、不可对照、降级和已知限制。
5. 只有合法官方 runtime、同机同质量对照、质量门禁和各语料门槛全部满足的范围，才可声明该指定版本与场景“完成对标”；其余范围保留准确状态。
P0 清理 / 删除后端已采用多次候选字节和新目录完整重写，输入上限 256 MiB、单解码流 32 MiB、累计解码 256 MiB、资源深度 64；这些是拒绝阈值，不是进程峰值内存或抗解压炸弹保证。PDFsharp 解码分配先于长度检查，完整快照、原生文档、字体和暂存副本须在 PERF 编辑 / 保存场景单独记账。本轮小型合成删除验证没有测量生产规模峰值或官方相对性能，指标保持未测。

P0 表单字体实验保留完整静态 TTF、128 KiB 的 BMP CIDToGID 和全覆盖 ToUnicode，以允许保存后输入新字；不能套用当前值子集的文件大小结果。实际 DLL 的 CJK 自动 AP 使用系统 fallback，受控方案须在每次提交后托管重建外观。P6 / PERF 的表单场景需分别计量首次字体准备、每次提交 / 桥接、字段数增长、资源去重、原生与托管文档并存及完整重写的峰值；同时检查输入期间字体 / 光标、保存 AP 和可见选中行一致。当前仅有合成语料的可行性与字节清单，没有这些性能预算的通过数据。[方案与限制](pdfviewer-acroform-font-scheme.md)

P0 字符簇 / 部分图像原型使用少量字形和 32×24 像素图片，验证的是内容与位置正确性。图像目前逐像素做仿射多边形相交，单图维度 / 32 MiB 样本限制只是拒绝阈值；解压前后副本、保留字体、候选文档和完整新目录重写会同时占用内存。P7 / PERF 应补大图、重复实例、大量文本对象 / CMap、取消、预算和峰值实测，再考虑逆映射选区包围盒等优化，优化后仍必须通过相同独立像素与簇边界门禁。当前不报告该原型的吞吐量、峰值通过或官方性能对齐。[方案与边界](pdfviewer-partial-redaction-scheme.md)
