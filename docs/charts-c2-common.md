# Charts：非单调索引续作与 C2 共用能力

历史报告：下文对应 `048EED53…74CD0C`，258/66/30 为当时结果；后续 CDB6 的 338/100/35 见[上一轮记录](charts-c2-followup.md)。最新散点修复及模板/读数见[新执行记录](charts-c2-templates-readings.md)。本文件中的旧性能开放项和源码关联均按当时版本保留，更新本导航前的[原始文档字节](../artifacts/charts-platform/charts-c2-templates-20261016/historical-document-bytes/charts-c2-common.md)另存。

本轮在 C1 基础上推进 C2，面向现有 Line / Bar / Scatter 的组合、绑定和交互。C2 尚未整体结项；下文分别记录已实现契约、验证及继续工作的边界。官方基线继续固定为 12.3.0，冻结的 C0 API、200 行矩阵 ID 和性能预算不改写。

## 非单调索引

此前构建索引时，比较排序反复通过 `GetValue` 读取完整数据项。[第一轮纯索引改动](../artifacts/charts-performance/C1-index-nonmonotonic-20261015-results.md)改为直接坐标比较/预取键值，独立冻结运行时的 5 对进程验证准备阶段 p95 从 9.8117 ms 降至 3.9333 ms，首次可用 p95 从 13.7522 ms 降至 8.3697 ms；查询与首查询均改善，未延迟建索引。

随后用独立、无 Avalonia 依赖的索引微基准比较间接比较器、双数组排序、8 位和 16 位 radix。60 个独立进程均逐个 ordinal 对照完整排序 oracle。最终紧凑坐标路径采用稳定 radix：小于 65,536 个有限点使用 8 位桶，较大数据使用 16 位桶；常量位跳过，原始浮点位序保持完整精度，正负零归为同一键，相同 X 保持物理顺序。映射/覆盖快照一次缓存 X，再排序并恢复等 X 的物理顺序。无抽样、无数据重排、无首次查询延迟构建。

索引保留一个 `int[finiteCount]`；radix 的另一份 ordinal 数组及较大数据的 256 KiB 桶数组仅在构建期间存活。单调完整数据仍不分配排序索引。微基准结果不能替代最终图表基准；最终 C2 默认配置运行时的对比与归因边界见下方验证记录。

## 最终性能及内存复测

所有最终计时使用同一冻结 benchmark 程序（SHA256 `34055360A2D39F711175FC0191E6EE0DC57A0279DC15527AEDDF4C859289B45E`），替换为下文 Windows Core/Themes；原始 guard、数据/视图/PlotArea/primitive 指纹及 supervisor 均通过，没有减少原始点、抽样或更改质量。比较脚本判定各组配置可比；这表示统计口径匹配，不表示性能门槛通过，也没有执行官方运行时对照。原始数据、进程与报告哈希保存在各 JSON 及相邻 `.evidence` 目录，[性能总简报](../artifacts/charts-performance/C2-common-20261015-final-v3/results.md)和[证据清单](../artifacts/charts-performance/C2-common-20261015-final-v3/evidence-manifest.json)一并保留。

### 非单调 10,000 点

[最终非单调比较](charts-c2-nonmonotonic-final.json)在每侧 5 个独立进程、每进程 3 次预热和 10 个计入样本中完成；每侧 50 个样本、1,250 次命中查询。

| 指标 | C1 优化前 | 最终 C2 | 变化 |
| --- | ---: | ---: | --- |
| scenePrepare p50 | 8.2699 ms | 1.1795 ms | -85.7% |
| scenePrepare p95 | 9.8117 ms | 1.3663 ms | -86.1% |
| RenderRecord p95 | 3.7939 ms | 3.9856 ms | +5.1% |
| 首次可用至记录 p95 | 13.7522 ms | 5.9732 ms | -56.6% |
| 命中查询 p95 | 0.0755 ms | 0.0657 ms | -13.0% |
| 每样本首查询 p95 | 0.0956 ms | 0.0807 ms | -15.6% |
| 各阶段及全部 25 次查询之和 p95 | 14.9250 ms | 7.1216 ms | -52.3% |
| scenePrepare 分配 p95 | 111,104 B | 151,256 B | +40,152 B |
| 首次可用分配 p95 | 1,294,576 B | 1,334,656 B | +40,080 B |

增加的构建分配主要来自临时 radix scratch；它不是常驻第二份数据副本，但本轮 20 次内存夹具是单调数据，不能据此宣称已经覆盖非单调 scratch 的长期保留。第一阶段纯索引改动与最终 C2 合并改动分别保留证据，不能把最终全部差值只归因于某一个方法。

阶段和不重复累加嵌套的首次可用计时。另从源快照开始到末次查询结束的独立 wall span p95 为 14.9621 → 7.1514 ms，包含未单独插桩的间隔；未从该 wall span 推导分配总量。没有观察到把构建成本转移到首查询的惩罚。

### 百万点与八场景回归

[百万点独立比较](charts-c2-millionpoint-final.json)每侧 5 个进程、25 个样本，结果包含回退：

| 指标 p95 | C1 | 最终 C2 |
| --- | ---: | ---: |
| SourceApply | 13.2266 ms | 14.2909 ms |
| scenePrepare | 12.9527 ms | 12.2935 ms |
| RenderRecord | 65.4314 ms | 81.1806 ms |
| 首次可用至记录 | 85.8193 ms | 105.9789 ms |
| 命中查询 | 1.7650 ms | 1.8418 ms |
| scenePrepare 分配 | 70,816 B | 71,008 B |
| 首次可用分配 | 81,840,184 B | 81,840,304 B |
| 命中分配 | 640 B | 616 B |

首次可用 p95 增加 23.5%，RenderRecord 增加 24.1%；不能以“索引优化”概括为全部性能改善。RenderRecord p50 也从 44.3753 增为 66.6186 ms，首次可用 p50 为 72.0842 → 86.8512 ms，命中 p50 为 0.5538 → 1.2600 ms；多个独立进程都出现记录变慢，不能解释为单个尾部离群样本。[八场景比较](charts-c2-full8-final.json)为每侧一个进程、每场景五样本，其 p95 就是该组最大值；其中百万点首次可用反而从 110.7023 降为 66.4203 ms，不能替代上面多进程的回退结论。全精度几何记录仍是主要成本，raw 没有 GC pause/JIT 计数或阶段 CPU 栈，具体因果需在 C4 取得阶段采样/控制变量证据后确认。

| 八场景 p95 | scenePrepare：C1 → C2（ms） | 首次可用：C1 → C2（ms） |
| --- | ---: | ---: |
| S-RT-POC | 0.4591 → 2.2013 | 1.2243 → 3.1262 |
| S10-B | 5.8310 → 7.2806 | 30.5819 → 34.6967 |
| S10-L | 0.8910 → 0.9916 | 5.6417 → 6.0653 |
| S10-L-GAP | 0.6597 → 0.9954 | 3.2695 → 3.5627 |
| S10-L-NONMONOTONIC | 13.7594 → 1.9155 | 15.4505 → 3.5568 |
| S10-S | 0.9492 → 1.1731 | 27.4258 → 37.4218 |
| S100-L | 1.2881 → 1.9742 | 13.0040 → 17.0021 |
| S1M-L | 11.0870 → 6.3450 | 110.7023 → 66.4203 |

小场景还包括 S100-L 记录 p95 7.3308 → 11.5499 ms（+57.6%）、S10-S 记录 26.3343 → 36.0090 ms（+36.7%），以及 RT 准备 4.79 倍。完整时间/分配增量和每进程分布见[回退分析](../artifacts/charts-performance/C2-common-20261015-final-v3/regressions-and-distribution.md)，不因绝对值较小而免记。

这些小场景与百万点的时间回退保留为 C4 问题；没有调整被冻结的性能预算或删去较慢的样本。另对[更早 DB866、尚未引入索引的同程序八场景基线](../artifacts/charts-performance/C2-common-20261015-final-v3/preindex-full8-comparison.json)严格比较：非单调准备 p95 仍为 1.5066 → 1.9155 ms（+27.1%，+0.4089 ms），该阶段分配则从 2,465,168 降至 151,256 B。此前回退的大部分已解决，但相对更早基线仍有残余准备成本；该五样本池和上面的 50 样本池不能混用，变化也包含更早的数据布局改动。

### 20 次百万点换源内存

[内存比较](charts-c2-memory-final.json)沿用同一程序 SHA256 `5DA2E701C96F2CE04BDD94419F2C0F040F83C992D9263BE3BED3DD30FF36503A` 和原门槛：每次全量换源、记录/查询后强制完整 GC，清空源时 chart 仍存活，最后再卸载。最终 source/snapshot 存活数均为 0，清空源后的 managed 残余为 26,464 → 26,432 B，平台期增长为 29,272 → 29,240 B；微小差值不作为内存改善结论，保留诊断通过。此为每侧一次 20 周期的单调百万点保留诊断，不是长期 soak、OS 峰值、Linux 内存或非单调 scratch 压力验收。

独立审阅重算 22 份 raw/guard/程序集关联及 380 个百分位/分配字段，均吻合。[结构化验证汇总](charts-c2-validation.json)保留平台、性能、内存与开放项，`formalPerformancePassed` 仍为 null。

## 已实现的公共契约

### 图例与稳定配色

`ColorPalette` 按系列实例分配颜色身份。隐藏、恢复、重排、移出再加入同一个实例不会改变其颜色；显式 `Stroke` / `Fill` 优先。颜色身份使用弱键保存。替换调色板列表会重新取色；不监听列表原地修改。

`ChartSeries.IsVisible` 支持 TwoWay，隐藏系列不参与绘制、范围、柱分组、命中及选择，仍可通过图例恢复。隐藏时清除其选择，恢复时不自动选回。`ShowInLegend` 独立控制图例条目。支持四边位置、横向/纵向排列、`LegendItems` 模型、带取消的 `LegendItemInvoked`、`IsLegendInteractive` 和自定义绘制回调 `LegendRenderer`。图例为绘图条目；完整控件模板、浮动/对齐及分类图例仍待实现。

图例超过可用空间时截断条目，并为绘图区保留空间，避免大量竖排条目让整图消失；当前不提供图例滚动。

### 数据标签与标记

系列支持十种标记形状、显示开关及独立填充/描边/粗细。圆标记保留 C1 命中规则；其余形状使用保守外接半径及查询容差。柱的标记默认关闭，开启后位于分组柱端点。定制系列通过复用 renderer 时，应显式选择适合自己的标记配置。

`ShowDataLabels` 默认关闭。支持格式、原始项/稳定 ID 上下文 formatter、位置、偏移、字体、画刷、背景及重叠隐藏。Auto 会尝试备选位置并限制在绘图区，正负柱按屏幕方向处理。标签遵守绘图区和断轴裁剪。`MaxDataLabels` 默认每系列 500 个候选，上限 10,000；密集数据确定性选择候选，不改变系列几何或提示返回的原始值。完整 UI 模板、连接线和更复杂避让仍待实现。

### 提示、共享读数与十字线

默认 `NearestPoint` 保留 C1 点/柱命中。`TooltipMode=SharedX` 对每个可见系列独立查找最近屏幕 X，等 X 时选较近 Y、再按物理顺序确定身份；多轴读数保留各系列实际 X/Y，不伪装为共同原始 X。数据源不排序，返回值包含原始 `Item` / `StableId` / `Index`。公开 `HoveredPoints`，`TooltipFormatter` 可生成完整文本或返回 null 隐藏文本框。`ShowCrosshair` 在当前主读数位置绘制横纵线，读数环与绘图区一致裁剪。

共享模式查找中心在当前绘图区内的有效点，最近 X 被 Y 裁剪时继续向两侧搜索；等距 X 一并比较 Y 和物理顺序。指针位于对应轴断轴内部时跳过该系列。大量点同时被 Y 裁剪或集中在同 X 时仍可能退化为线性查询，这是 C4 的二维索引工作。它尚不提供线段插值、竖向模式、轴标签、堆叠/LOD 聚合或 Avalonia 内容模板。关闭提示同时关闭读数和十字线。

### 选择、键盘与生命周期

`SelectionMode` 提供 None / Single / SingleDeselect / Multiple。`SelectedPoint` 与 TwoWay 的 `SelectedPoints` 同步，前者为列表最后一项；外部更新列表应替换列表实例。Ctrl 点击切换单点，普通点击替换选择，Shift 拖动框选，Ctrl+Shift 拖动追加。`SelectRectangle` 使用 chart-local DIP，选择点中心或分组柱端点；返回匹配数、最终选择数及截断标志。默认 `MaxSelectionCount=10,000`，计数遍历全部匹配点，超过上限时不继续物化元数据。`SelectionChanged` 提供前后状态，`ClearSelection` 显式清除。

选择按稳定身份迁移，集合 Move 更新原始索引，删除/隐藏/移出系列会移除对应选择。选择环不影响轴范围。`IsKeyboardNavigationEnabled` 启用焦点和按系列/物理顺序浏览的方向键，Home 复位，Esc 取消当前手势及读数并保留已有选择。捕获丢失、失焦、卸载、视图外部变更及操作开关变化会释放相关拖动状态。矩形选择和普通平移互斥。

无 IdPath 的重复同一对象引用视为同一实体，保留仍匹配的原始索引代表项；跨 Move 的重复引用 occurrence 无法单靠公开 StableId 区分。需要独立选择时使用不同业务实例及唯一业务 ID。外部赋值会先过滤/去重再应用数量上限，即使语义未改变也回写规范的只读列表。

尚无系列选择、前置选择取消事件、范围选择器、视图历史、触摸捏合仲裁和完整窗口失活/物理输入验证。跨图视图同步仍可通过已有的 Viewport TwoWay 绑定实现，调用者须匹配坐标单位和轴域。

### 标注

支持 `LineAnnotation` / `BandAnnotation` / `TextAnnotation` / `CustomAnnotation`，以及 inline `Annotations` 和非 null 优先的 `AnnotationsSource`。它们继承 DataContext，集合与属性变化可更新，卸载后退订外部源。系列、轴与标注均要求单一逻辑所有者。

支持层级、透明度、画刷/文字、数据或像素坐标及绘图区裁剪。像素坐标以绘图区左上角为原点、单位为 DIP。数据坐标使用对应轴的缩放/反向/对数/断轴映射。Key 必须属于场景中已参与相应维度的轴；未知、维度错误或仅声明而未参与场景的额外轴会跳过该标注，不回退主轴。数据标注裁剪同时处理断轴，图表外框仍始终裁剪。矩形/椭圆/箭头专用类型尚待实现。

### PNG 导出与辅助语义

`TryExportPng(Stream|string, ChartExportOptions?)` 在 UI 线程导出当前已布局的图表，等比缩放、居中放入目标尺寸，不重新排列标签。包括图例、标注、标签、当前选择及读数。配置像素尺寸、DPI、画布背景和像素预算；透明背景不覆盖图表自身 Background。PNG 写入 pHYs 分辨率元数据，不能用 Avalonia 解码器固定返回的 96 DPI 验证它。默认预算 16,777,216 像素，允许上限 67,108,864，每边最多 32,768；编码缓冲也有上限。

流由调用者所有，失败可能留下部分写入；文件经同目录临时文件后替换，失败保留旧文件，`ExportFailed` 提供异常和请求路径。Demo 使用 StorageProvider 保存对话框。当前只支持 PNG，JPEG、异步取消及 Linux picker 实机仍待验证。

图表的 AutomationPeer 提供只读摘要（系列/可见/行数、当前范围、选择数），系列节点按请求创建并可切换显隐，遵守图例开关和取消事件。摘要只读缓存场景，不枚举或物化百万个数据点。移除系列后回收节点缓存，旧节点不能再操作；没有辅助客户端时不创建这些 peer。此为托管语义和合成验证的基础，尚无点级虚拟节点、原生 Chart/Text pattern、图案或真实 UIA/AT-SPI2 结论。

当前已有 peer 的场景读取仍会发起重复 children 失效通知；按状态 revision 减少通知属于后续辅助功能/性能工作，不代表已经测试活跃读屏下的性能。

## Windows 最终验证

[完整结果](../artifacts/charts-platform/C2-common-20261015-final-v6/results.json)通过：258 项测试（失败/跳过/未执行均为 0），66 项真实窗口 Demo 合成检查，30 项 Usage 原生绑定检查，以及两个独立缓存、无 ProjectReference 的 nupkg 消费者。整个 slnx 的 Release 构建零警告、零错误，见 [构建日志](../artifacts/charts-build/C2-common-final-v6/Release.log)。

| 冻结对象 | SHA256 |
| --- | --- |
| Core | `048EED53A4EF91AB83A6334DF170497628B4DC54570A5E0D33EA5E8D6074CD0C` |
| Themes | `6743ACD7E3841F3874628EB1AF1CD08EE0D0412FABB62BF03A0BEFBD4FEB5DD9` |
| Windows results.json | `0C564095BFD05AA700B58E1B4C2C792FB108225B39B46F4F5C655C060DC4A084` |
| [C2 API 快照](charts-c2-api.txt) | `DEC5C1CA08FA59034D93434A1314AAE8A9E6FBFB12F75E8B401C5FEBA8AE4C0E` |

构建/测试输入与程序集在运行期间哈希不变。完整仓库 manifest 的稳定标志为 false：期间仅有 `benchmarks/PdfViewer.P0/verify_cjk_forms.py`、`docs/pdfviewer-p0-validation.md`、`docs/pdfviewer-status.md` 三项非 Charts 构建输入变化，见 [差异补充](../artifacts/charts-platform/C2-common-20261015-final-v6/source-changes-supplement.json)。没有用这些变化掩盖图表输入或 DLL 漂移。

PNG 检查覆盖亮暗主题，显式 960×540 下 96/144/192 DPI 解码像素逐字节相同，三档 pHYs 分别匹配各自 DPI。独立依据 Bounds、PlotArea、当前范围验证六个首末数据点、三组图例符号/文字、底轴、左边界和外侧 Y 刻度有墨迹；不复用导出内部布局计算。该像素 oracle 限当前线性、非反向、无分组柱的固定示例，不能扩大为全部轴/图族的像素验收。[亮色导出](../artifacts/charts-platform/C2-common-20261015-final-v6/charts-light-c2-export.png)、[暗色导出](../artifacts/charts-platform/C2-common-20261015-final-v6/charts-light-c2-export-dark.png)、[Demo 图](../artifacts/charts-platform/C2-common-20261015-final-v6/charts-light-c2-common.png)。

真实窗口和 bitmap 绘制已经执行；键鼠输入仍为合成路由，图片不是系统桌面截图，也未测 compositor present/FPS。

## Linux 最终验证

在 Ubuntu 26.04 x64 / WSL2（kernel 6.6.114.1）/ WSLg 1.0.73 实际执行，见 [最终汇总](../artifacts/charts-linux/20261015T114700134Z-final-v4-3c7b0fb20bbd4defa25071c4d94b8067/linux-final-summary.json)。其 SHA256 为 `5E4A4C4FB66954132355ACB52497E208D1AE4262804F1FFC54E640DA9A688F93`。

- Charts.Tests / Demo / Charts.Usage 三项目 Release 构建零警告、零错误。
- Headless 258 项通过，失败/跳过/未执行均为 0；WSLg 真实窗口 Demo 66 项、Usage 30 项通过。
- 两类全新独立缓存、无 ProjectReference 的包消费者各 32 项通过；消费者为 Headless 编译 AXAML/MVVM 契约，没有将其宣称为原生窗口验证。
- 亮暗 PNG 的三档 DPI、pHYs、六端点/三图例、底轴/边界/外侧刻度完整性通过，CJK 文本使用进程私有 Noto 字体。图片和目视检查见汇总中的 `visual-review.json`。
- 复制的源文件与所有验证后程序集保持稳定，live Charts 构建输入仍匹配复制时快照；Linux Core SHA256 为 `D9F1A5C2C348FB784B3C2ECBF415696C3F70E35A72882EF0E6280D5417DA6BEC`。Linux DLL 为独立平台构建，哈希分别记录，不以 Windows DLL 代替执行。

使用工作区私有 SDK、PowerShell、ICU/ICE/SM、字体和隔离缓存，未全局安装 SDK 或修改系统服务。最初 WSL 普通权限访问失败；后来经允许的提升调用取得发行版并完成独立执行。启动器仍报告用户 systemd session 启动警告，实际应用窗口检查独立通过。缺少私有 ICU/ICE 的早期失败、旧像素 oracle 的 v3 失败及所有中间快照保留，最终只采纳 final-v4。

请求的是 WSLg/XWayland 的 X11 路径；程序未直接采集实际 backend/renderer/GPU，字段保留 null。没有真实物理输入、系统桌面截图、native present、Linux 性能或官方参考测量；Ubuntu 22.04/24.04 物理桌面、原生 Wayland、ARM/麒麟/UOS 不能由本次 WSLg 结果替代。[平台矩阵](charts-platform-matrix.json)继续逐目标追踪。

## 保留的失败与修复

- 首次 216 项中两项旧 bar oracle 失败：复用 renderer 的测试代理没有复制 BarSeries 默认关闭 marker 的配置。修正代理配置，并另外覆盖显式开启柱顶标记的行为，没有放宽旧几何 oracle。
- 新视觉测试加入后的 seed 314159 并行运行出现 UI 队列挂起；采样连接未成功，不能给出已确认的阻塞栈。保留 [挂起记录](../artifacts/charts-tests/C2-visuals/hang.json)。同一 DLL 单方法隔离及 236 项串行均通过，当前验证入口显式 `-parallel none` 并采用 PerTest 隔离；并行测试调度的具体根因尚未结案。
- 首次导出结构测试错误地把 DrawingGroup.Bounds 当成裁剪后的边界，改为验证 target clip、背景、fit 矩阵和 chart-local clip，并保留原布局/视图不变断言。
- Windows v2 的尺寸和 pHYs 检查虽通过，目视发现 144 DPI 内容放大裁切。改为固定 96 DPI 像素域绘制，DPI 控制默认像素尺寸与 PNG 元数据，再补三档解码像素和完整内容检查。原 v2 保留 [范围更正](../artifacts/charts-platform/C2-common-20261015-final-v2/export-content-followup.json)，不作为最终导出通过证据。
- v3 纯 PNG 测试先运行时暴露静态 PanCursor 提前索取 ICursorFactory，导致 147 项级联失败。游标改为实际拖动时才创建；[无 UI 平台的两项独立进程测试](../artifacts/charts-tests/C2-pure-static/Charts.xml)及最终完整回归均通过。
- v4/v5 的完整内容 oracle 把左边界被主网格覆盖/抗锯齿混色、半透明 band 覆盖的像素错认为纯轴色。改查无遮挡的边界与绘图区外 Y 刻度，保留颜色/数量约束；最终 v6 通过，原失败目录不覆盖。

独立复核还修复了 SharedX 的分组柱中心偏移、最近点被裁剪时遗漏可见点及左右等距候选、外部 SelectedPoints 归一化/限额顺序、重复引用代表索引、图例溢出空图，以及 resize/隐藏期间捕获释放，均纳入当前回归。

## C2 继续工作

| 计划项 | 本轮范围 | 尚未完成 |
| --- | --- | --- |
| C2-01 | 稳定 palette、系列显隐、四边/排列、模型/绘制回调 | 浮动/对齐、控件模板、分类图例、未来堆叠重算 |
| C2-02 | 十种 marker、有限标签、格式/布局/避让/原始项 | 完整模板、逐点样式和新图族专属布局 |
| C2-03 | 自定义文本、SharedX、读数环、双向十字线 | 线段/竖向/轴标签、堆叠/LOD、完整内容模板 |
| C2-04 | 三种点选择模式、禁用、多选/矩形、绑定/事件 | 系列选择、前置取消事件、Range selector |
| C2-05 | 键盘浏览/复位/取消、捕获/失焦/卸载处理 | 触摸、精密触控板、窗口失活、DPI/手势冲突实机证据 |
| C2-06 | 线/带/文字/自定义、坐标/层/裁剪 | 矩形/椭圆/箭头专用类型、未参与场景轴的标注 |
| C2-07 | 保持当前静态行为 | 动画 duration/easing/delay、暂停/取消、减少动态效果及大数据策略 |
| C2-08 | PNG 实现与验证记录见上文 | JPEG、异步取消、双平台 picker 实机验证 |
| C2-09 | 图表摘要及系列操作语义见上文 | 点级虚拟语义、图案、高对比及真实 UIA/AT-SPI2 验证 |

后续先补 C2 模板/交互与上述各项缺口，并在 C4 处理百万点记录、小场景准备回退、同 X/二维退化查询、增量更新和绘图缓存。WSLg 的构建/窗口/包消费已经通过；物理 Linux 目标桌面、官方授权宿主、真实输入和 compositor present/FPS 仍待独立验证。C0 冻结物、200 行矩阵身份及本次证据关联校验见 [当前结构化状态](charts-c0-c1-status.json)。

文档收尾后的[关联校验](../artifacts/charts-platform/C2-common-20261015-final-v6/post-document-checks.json)确认：当前 484 份构建输入同时匹配 Windows 与 Linux 快照、两平台各 8 份程序集匹配；238 份性能证据、43 份外部输入、98 份冻结宿主文件及 39 份源码匹配。C0 三个冻结文件与 200 个矩阵 ID/顺序保持，15 份 Charts JSON 和本地文档链接通过检查。
