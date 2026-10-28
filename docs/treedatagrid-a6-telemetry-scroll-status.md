# TreeDataGrid A6 托管堆交叉遥测与滚动宿主复用

2026-10-24 自定义工厂长尾修复：自动行高的完整拆挂路径按当前视口预算保留已清理备用行，减少宿主/模板重建。557 项控件回归、90 项校验、4512 条几何快照及本构建 600.56 秒/301 轮原生耐久通过，1204728 个退休对象零保留。正式同状态四进程的自定义工厂 UI P95/分配下降 18.87%/15.31%，渲染完成 P99 下降 19.32%，默认复用收益保留；本次协议内长尾调查收敛，见[本轮记录](treedatagrid-a6-auto-factory-tail-status.md)。历史遥测警告、真实平台/CI/麒麟和业务预算继续单列，整体 A6 未关闭；以下为先前轮次。

2026-10-24 自动行高修复：默认宿主复用已启用，模板测量链缓存与宿主退休边界通过验证。546 项控件回归、68 项原生记录校验、9 项几何校验及 4512 条旧新几何快照通过；增强原生耐久 1800.44 秒/901 轮、3620276 个退休对象最终零保留。首轮宿主自动化缓存失败及性能自动化状态差异均保留，修正后的证据与限制见[本轮记录](treedatagrid-a6-auto-height-status.md)。2 次原始 Any 警告、真实 Windows/CI/麒麟和业务预算继续单列，整体 A6 未关闭；以下是先前轮次。

后续：导航编辑刷新路径已修复并重新验证，535 项控件回归、57 项原生记录校验、四进程对照和增强 30 分钟耐久见[导航修复记录](treedatagrid-a6-navigation-tail-status.md)。本文保留 `D04DD11...` 轮次的原始结论，不用新结果覆盖其回退、失败试验或遥测负值。

日期：2026-10-24（香港时间）。承接[首轮 Source 分配优化](treedatagrid-native-optimization-a6.md)，本轮先增加托管堆交叉指标和独立有效性校验，再处理默认工厂滚动中的宿主反复挂接。公开 API、依赖版本、暂存编辑和自定义工厂回收边界保持不变。

当前结论：托管堆交叉指标和原始记录校验已补齐；默认工厂未启用自动行高时，两个滚动场景的 UI P95 及分配取得稳定收益。523 项控件测试、原生记录和 30 分钟生命周期验证通过，但自定义工厂与导航编辑的正式混合负载 P95 出现回退，仍需调查，不能据本轮记录宣称全场景性能验收通过或关闭 A6。

## 托管堆遥测 v2

[原生宿主](../benchmarks/TreeDataGrid.NativeBenchmarks/README.md)升级为 `treedatagrid-native-render-v2`。六个性能场景、UI/批次渲染计时边界不变。耐久的五次滚动观察按轮交替使用默认工厂和订阅工厂，因此 v2 耐久累计分配不与旧 v1 当作完全相同工作负载比较。

每份资源记录包含：

- `rawManagedBytes`：保留 `GC.GetTotalMemory(false)` 原始值。负数对应 `managedBytes=null` 和明确的不可用状态，不钳为零、不用其它指标替换。
- 同一个 `GC.GetGCMemoryInfo()` 快照的 GC 索引、代数、并发/压缩状态、堆大小、碎片、计算出的存活字节、GC 提交字节，以及 5 个 `GenerationInfo` 区段的 GC 前/后大小与碎片。
- 采样开始/结束单调时间戳、前后各代 GC 次数及期间是否发生 GC。
- 独立的进程私有内存、工作集、句柄、累计分配和 CPU 时间。

当前估计、上次 GC 的存活字节和进程私有内存含义不同，不要求相等。上次 GC 的堆/碎片/提交值必须非负，碎片不能超过堆，提交量不能小于堆，各 `GenerationInfo` 区段汇总须与该快照一致。基线、稳态和最终资源都追加到原始 JSONL，终态记录逐条对应；缺失/重复/篡改、阶段越界、计数倒退和伪造有效标记都会失败。可选当前估计不可用时，独立校验器输出警告；必需交叉指标错误仍使运行失败。

[独立记录校验器](../scripts/verify_treedatagrid_native.py)另核对 v2 初始/终态协议，禁止降级协议绕过遥测检查，并从单调时间戳重算 UI/渲染耗时。历史 v1 原记录仍保留。[记录校验测试](../scripts/Tests/test_treedatagrid_native.py)目前共 43 项，与控件测试分开计数。

早期遥测试跑在 20 秒诊断中复现了负读数，见[报告](../artifacts/treedatagrid-alignment/a6-telemetry-scroll-20261024/telemetry-pilot-1/report.json)：基线原值 -2071808 B、末次 -1502368 B，对应上次 GC 的交叉指标有效，末次 last-GC live 为 4464960 B。该试跑先于默认/自定义工厂交替负载，不作本轮正式资格证据。负值原因未被归为已确认的产品或运行时缺陷；新的不可用状态也不表示该当前估计已修复。

## 滚动实现

默认工厂、固定行高、同一 Source/工厂会话的滚动可在原父级中重新准备行和单元格宿主，减少整个宿主模板和合成对象反复挂接。旧模型绑定、编辑器、内容仍清理，旧 A2/行 Clearing 与新 Prepared 配对；暂存草稿归逻辑身份所有。布局结束或失效/异常时，未使用的清空宿主脱离父级，备用池和保留数仍受视口限制。

自定义工厂继续执行原来的核心清理、脱离父级、Recycle/Release 顺序。换源、换工厂、换模板和抛错后会复验代数及会话，清理未映射的在途宿主，防止流入新会话或留下不可见的已清空子控件。没有增加公开开关或扩展任意 Control 工厂类型。

保留宿主后需显式失效单元格测量，不能依赖脱离视觉树来使旧 DesiredSize 失效。Headless 附加测试曾捕获短行沿用 373 高度而非原路径 28 的问题，显式失效后该测试通过。但首组原生逐帧交叉核对仍发现 `auto-default` 的 450 帧中有 445 帧几何/可见身份不同，证明 Headless 单项结果不足以放开原生自动行高复用。因此最终实现让 `AutoRowHeight=true` 沿用完整脱离路径，固定行高才启用宿主复用。自动行高优化继续开放。

该候选组的[拒绝记录](../artifacts/treedatagrid-alignment/a6-telemetry-scroll-20261024/candidate-rejection.json)、原始性能记录和主动中断的耐久记录全部保留；即使单次记录协议通过，也不将其计为最终性能/耐久资格证据。末次正式对照必须另用新目录、新功能 DLL，并核对相同请求的几何和可见身份。

新增 10 项[复用契约测试](../tests/TreeDataGrid.Headless.Tests/DefaultScrollReuseTests.cs)：垂直/水平宿主保持挂接、冻结列寿命、宽度变化与窗口缩小后无备用宿主残影、清理中切换工厂、清理中换源、嵌套滚动、抛错后配对和清理、离屏草稿只向原模型写一次、自动行高与原路径对照，以及自动行高保持脱离并在切回固定行高后恢复复用。

完整集成首轮在统一 Demo 的 Charts smoke 文件缺少 `Avalonia.Controls.Templates` 引用处失败；仅补齐该 using，未修改 Charts 行为。失败原记录保留在 [regression-1](../artifacts/treedatagrid-alignment/a6-telemetry-scroll-20261024/regression-1/results.json)，后续结果独立归档，不覆盖失败记录。

## 最终本地回归

[regression-6](../artifacts/treedatagrid-alignment/a6-telemetry-scroll-20261024/regression-6/results.json) 完整通过，源码/配置快照前后一致。Core 153、Headless 339、Themes 31，共 523 项控件测试通过；另有 8 项验收记录测试和 43 项原生记录测试。数据 12 场景各 5 轮、Headless UI 与 40 轮回收、功能/主题双包的全新缓存隔离消费、Usage、统一 Demo 构建/发布、原生程序化 161/29/36 项和图库导航均通过。

较早的 `regression-4` 遇到 Charts 并行改动的构建错误；`regression-5` 的各执行项通过，但检测到运行期间修改了工厂说明文档，整轮仍标为失败。修正后的 `regression-6` 未放宽输入一致性规则，失败记录继续保留。实际 Windows IME、物理拖动/焦点/剪贴板、字体/DPI、读屏、干净系统、实际 CI 和麒麟项目不由这些程序化结果代替。

## 最终原生输入与耐久

最终功能 DLL 为 `D04DD11D76A03C1EEF0344FB79542AC3B09A0C444C79726970488C14205D8393`，与 `regression-6` 的测试、消费者、包和发布目录一致。比较基线为前轮 Source 优化后的 `51B02CE611530F9FAEB3AC5221C0E77C91698F3DE259DE8C0ED20E68DA369D9B`；同一 v2 宿主 `2360929D138F05C9A19FCAFD7CC96CD25FB9A9D64AD25B9C12221169F1F4B9D3`、主题及依赖全部相同。两个隔离应用仅功能 DLL/PDB 不同，见[应用比较](../artifacts/treedatagrid-alignment/a6-telemetry-scroll-20261024/final/application-comparison.json)。

原生运行使用[85 份冻结输入](../artifacts/treedatagrid-alignment/a6-telemetry-scroll-20261024/final/native-snapshot-manifest.json)；冻结脚本清单中的相对路径以该文件的 `snapshotRoot` 为根解析，应用路径为绝对路径。共享 Charts 改动不改变该实验输入。正式性能场景仍为六场景、每场景五轮，每轮 30 帧预热和 60 帧测量；没有附加 EventPipe/采样分析器。

[最终原生完整运行](../artifacts/treedatagrid-alignment/a6-telemetry-scroll-20261024/final/after-native-30m/results.json)通过。独立进程耐久计时 **1800.6054538 秒、901 轮、7288 个请求全部完成**；计时段内无强制 GC，10 轮预热和最终排空/12 秒等待/GC 均在计时段外。默认工厂 451 轮、自定义工厂 450 轮；换肤、换模板、换源、双轴滚动、A2 配对和单次编辑提交持续检查。9 类共 **3,945,774 个**已跟踪退休对象全部回收，保留数为零。

[独立原始记录校验](../artifacts/treedatagrid-alignment/a6-telemetry-scroll-20261024/final/after-native-30m/soak/validated.json)通过：116 份资源样本中，基线和最终估计原值分别为 **−2077984 B / −758760 B**，可用值均为 null，明确输出两条警告；114 个稳态样本均非负。所有必需交叉指标、代区段汇总、记录对应、采样阶段及计数单调性通过。本次采样前后 GC 次数均未变化；运行期间的正常 GC 仍持续发生，不能据此把 last-GC 数据解释为任意时刻的当前堆峰值。

| 指标 | 预热后基线 GC | 排空后最终 GC |
| --- | ---: | ---: |
| 原始当前堆估计 | −2077984 B，不可用 | −758760 B，不可用 |
| last-GC 堆大小 | 9993480 B | 12201440 B |
| last-GC 碎片 | 6094768 B | 6975360 B |
| last-GC 堆减碎片 | 3898712 B | 5226080 B |
| last-GC 提交字节 | 37453824 B | 13086720 B |
| 进程私有内存 | 149389312 B | 120541184 B |
| 进程工作集 | 199065600 B | 176918528 B |
| 句柄 | 696 | 689 |

稳态私有内存样本范围为 153202688–186011648 B，句柄 681–704；首/末三个稳态样本的中位数变化为私有内存 **+905216 B（约 +0.86 MiB）**、句柄 **−18**，未触发原有调查护栏。护栏是 +256 MiB / +128 句柄，不是业务预算。基线至最终的累计分配约 98.86 GiB，含宿主/追踪/原生渲染缓存等，不能当作瞬时占用，也不与旧 v1 的不同工厂负载作直接收益比较。

## 正式双向性能对照

[四进程原始 QPC 汇总](../artifacts/treedatagrid-alignment/a6-telemetry-scroll-20261024/final/abba-analysis.json)按基线→优化→优化→基线执行，中间包含上述独立耐久进程；合计 **10800 个请求、7200 个正式测量帧**，每场景每版本 600 帧，分属两个进程、10 个 fixture。几何/可见身份逐帧一致，六张离屏图在四个进程中也逐字节一致；离屏图不属于桌面截图。分位数从原始单调时间戳重算，采用 nearest rank；帧和 fixture 存在相关性，这不是随机独立试验或业务 SLA。

| 场景 | 同步 UI P95，基线→优化（ms） | 同步 UI P99（ms） | 同步 UI 分配变化 |
| --- | ---: | ---: | ---: |
| observer | 0.0140 → 0.0149 | 0.0220 → 0.0218 | 0 B → 0 B |
| flat-100k | 35.3409 → 18.7422 | 49.0902 → 25.1832 | −55.29% |
| auto-default | 55.3854 → 52.6473 | 72.7067 → 74.4481 | 约 0% |
| auto-factory | 53.0869 → 65.8990 | 68.9919 → 83.5817 | 约 0% |
| wide-tree | 41.4991 → 23.1230 | 53.0502 → 26.5821 | −55.35% |
| navigation-edit | 59.1941 → 75.9971 | 77.9156 → 88.6565 | 约 0% |

默认工厂两个非自动行高滚动场景的 P95 分别下降 **46.97% / 44.28%**，P99 下降 **48.70% / 49.89%**。两个执行顺序均有收益：`flat-100k` 的两组 P95 分别为 29.7658→18.7712、37.3171→18.6433 ms；`wide-tree` 为 44.1819→24.2686、35.0399→17.3786 ms。对应渲染完成 P95 合并值为 80.7683→50.4665、67.6447→51.4450 ms，包含批次/调度开销，不能换算成物理屏幕 FPS。observer 的渲染完成 P95 本身也从 49.1965 变为 34.7149 ms，说明这部分存在进程/调度波动。

`auto-factory`、`navigation-edit` 的合并 UI P95 分别增加 **24.13%（+12.8121 ms）/28.39%（+16.8030 ms）**。本轮按“超过 20% 且超过 5 ms”的规则标记 `performanceInvestigationRequired=true`，不把分配近似相同当作排除回退的理由。自动行高保持旧路径，P99 未改善；这三个场景均不计优化收益。协议、几何和生命周期检查通过，与“所有尾延迟均通过性能验收”是不同结论。

外层编排器首次向按仓库相对路径解析的比较脚本传入绝对路径，反向阶段未启动，其[失败结果](../artifacts/treedatagrid-alignment/a6-telemetry-scroll-20261024/final/orchestration-results.json)保持失败。随后用同一个冻结脚本、相对参数单独完成[反向对照](../artifacts/treedatagrid-alignment/a6-telemetry-scroll-20261024/final/reverse-pair/results.json)，未改变应用和测量输入。[完成汇总](../artifacts/treedatagrid-alignment/a6-telemetry-scroll-20261024/final/completion-results.json)分别记录各执行项和恢复原因，不覆盖原失败记录。

## 追加独立场景诊断与未关闭项

为区分串行场景历史和实现路径，本轮另复制宿主，采用明确分开的 `NativeDiagnostic` / `treedatagrid-native-isolated-diagnostic-v1` 协议。每个场景从新进程开始，每进程 5 个 fixture、各 30 帧预热/60 帧测量；两个场景分别按基线→优化→优化→基线运行。UI 计时窗口外记录 GC 次数，应用输入仍仅功能 DLL/PDB 不同。诊断共 3600 请求/2400 测量帧，关联与几何检查通过；**不计入六场景正式验收的 10800 请求**，不通过改写协议或放宽正式校验器将其升级为资格证据。

见[独立场景诊断](../artifacts/treedatagrid-alignment/a6-telemetry-scroll-20261024/final/isolated-diagnostic/diagnostic-summary.json)：

| 独立场景 | 基线两进程 UI P95（ms） | 优化两进程 UI P95（ms） | 合并 P95，基线→优化（ms） |
| --- | ---: | ---: | ---: |
| navigation-edit | 70.7936 / 60.2313 | 88.6217 / 58.1863 | 62.8278 → 81.6824 |
| auto-factory | 54.8693 / 55.2713 | 56.1939 / 47.5557 | 55.1934 → 51.7311 |

自定义工厂在独立场景中未取得与正式混合负载相同方向的变化，说明顺序/运行状态需要进一步控制。导航编辑的两个优化进程差异很大，但合并 P95 仍增加约 30%；**独立运行没有排除该回退**。UI 窗口内 Gen2 次数变化合计，导航基线为 154/125、优化为 122/134；这些次数不能单独归因暂停、CPU 热点或线程等待，也不足以证明回退原因。当前不把其解释成已确认的 GC 或系统噪声。

本轮通过的是执行、协议、几何、已跟踪生命周期和遥测交叉校验；[验证汇总](../artifacts/treedatagrid-alignment/a6-telemetry-scroll-20261024/final/validation-summary.json)显式保留 `performanceInvestigationRequired=true`。导航编辑需继续定位 UI 调用栈、GC 暂停及渲染线程等待，自定义工厂需复核混合负载的顺序/状态影响；原因和修复未经证据确认前，这两项不关闭。自动行高继续完整脱离，原生测量缓存差异未解决前不启用宿主复用。

[部分平台矩阵](../artifacts/treedatagrid-alignment/a6-telemetry-scroll-20261024/final/partial-platform-matrix.json)及三个范围的复核记录保持本地通过、Windows 8 类/完整平台 9 类待验。物理呈现/输入延迟、多小时业务负载和预算、真实平台交互/CI/麒麟及 A3.1 原生不透明 CompiledBinding 安全写回继续开放；未调整版本、发布公共包或关闭 A6。
