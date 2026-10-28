# TreeDataGrid A6 原生性能热点首轮优化

后续实现和新证据见[托管堆交叉遥测与滚动宿主复用](treedatagrid-a6-telemetry-scroll-status.md)：遥测 v2、默认工厂非自动行高滚动复用及 523 项控件回归。本文保留首轮 Source 优化及 v1 测量原记录，不用后续结果改写历史读数。

日期：2026-10-23–24（香港时间）。

本轮承接[原生性能与十分钟耐久基线](treedatagrid-native-performance-a6.md)，先用独立 EventPipe 采样定位，再优化确认的 Source 分配热点，最后用相同原生协议做无采样对照。没有修改公开 API、依赖版本、重建发布语义、选择/草稿契约或工厂生命周期。

## 调用栈证据

新增独立诊断工具 [TreeDataGrid.ProfileAnalysis](../benchmarks/TreeDataGrid.ProfileAnalysis/README.md)及[采集驱动](../scripts/Trace-TreeDataGridNative.ps1)。诊断依赖只属于该工具，不进入控件包或测量宿主。启用 GC/分配、JIT/loader/stack 与托管采样，停止时保留 rundown；TraceEvent 解析后核对无丢失事件。性能样本仍覆盖六场景、五轮、每轮 30 次预热和 60 次正式请求；诊断耐久仅为 60 秒 pilot，不作正式耐久证据。

| 诊断捕获 | 托管采样事件 | 分配 tick | 丢失事件 / 无调用栈事件 |
| --- | ---: | ---: | ---: |
| 优化前性能 | 581318 | 255009 | 0 / 0 |
| 优化前短耐久 | 229705 | 37580 | 0 / 0 |
| 优化后性能 | 576169 | 236464 | 0 / 0 |

原始 nettrace、宿主 JSONL、独立校验和归因分别保存在 `artifacts/treedatagrid-alignment/a6-profile-20261023/before-performance/`、`before-soak/`、`after-performance/`。可读报告：[前测归因](../artifacts/treedatagrid-alignment/a6-profile-20261023/before-performance/attribution.json)、[短耐久归因](../artifacts/treedatagrid-alignment/a6-profile-20261023/before-soak/attribution.json)、[后测归因](../artifacts/treedatagrid-alignment/a6-profile-20261023/after-performance/attribution.json)、[采样比较](../artifacts/treedatagrid-alignment/a6-profile-20261023/profiling-comparison.json)。

归因按最近的实际产品方法分组，并区分同步 UI 区间、区间外 UI 工作和后台线程。JSONL 的 UTC/单调时钟锚点用于映射区间边界，边缘采样有误差。采样数量不是精确 CPU 毫秒；分配 tick 是采样估计，不是每种对象的精确计数。inclusive 栈会重叠，不能相加。无调用栈事件为零不表示原生符号全部解析；本轮未下载原生符号。

这些分组描述调用者归属，包含其调用的 Avalonia/运行时工作，不是组件净成本。例如 SetLayout 下包含框架合成对象创建；Program.Main 下可能是框架稍后调度的 UI 工作，不能全部解释为验证程序开销。验证快照、日志等有具体方法栈可查，未用类别总数宣称它们的精确比例。

确认的热点包括：滚动时行/单元格重新挂接引发合成对象、主题继承与文字布局重建；编辑提交时全量图重建创建身份匹配队列，并为投影映射多次扩容。短耐久的主要分配仍来自容器/模板重建，而非单纯测量日志。该负载有意重复换源、换模板和工厂，不能将累计分配解释为存活泄漏。

## 首轮实现与验证

本轮只优化 Source 重建的临时分配：

- 在没有适用索引通知、且每个位置的模型引用都相同时，直接复制原节点候选，避免为每个模型新建身份队列。静默重排继续使用引用队列；Move/Reset 等索引通知继续使用原来的精确路径。
- 在已验证图上统计节点与未过滤的可见节点数量，预分配新的事务投影映射。仍创建新快照并原子发布；不复用、修改已发布的映射。统计不调用过滤器；有过滤器时，可见映射沿用动态增长，过滤器仍每次只调用一次。

未改动容器挂接和工厂回收行为；这部分热点继续保留，避免在本轮同时改变生命周期边界。

新增 4 项契约测试，覆盖引用相同但身份不同的重复模型、相同引用序列下的索引 Move/Reset、非通知集合的同长度静默重排/替换、隐藏映射/过滤器次数/历史快照。完整 **513 项控件测试**通过（Core153 / Headless329 / Themes31），失败、错误、跳过和未运行均为零。

完整 A6 自动验证于香港时间 **2026-10-23 23:42:03–23:44:56** 完成，237 份输入前后哈希一致。12×5 数据/几何、3×5 Headless 组合、40 轮生命周期、独立功能/主题包消费、发布目录原生 161/29/36 项及 Gallery 检查通过。见[完整结果](../artifacts/treedatagrid-alignment/a6-profile-20261023/regression-1/results.json)、[测试和自动证据](../artifacts/treedatagrid-alignment/a6-profile-20261023/regression-1/automated-evidence.json)。8 项验收校验器测试另计。

| 数据/几何协议，5 次样本的中位数 | 前测分配 | 后测分配 | 前测耗时 | 后测耗时 |
| --- | ---: | ---: | ---: | ---: |
| 十万行创建 | 62.48 MiB | 40.60 MiB | 51.29 ms | 45.91 ms |
| 十万行 Refresh | 76.64 MiB | 37.55 MiB | 70.03 ms | 33.72 ms |
| 十万子节点宽树创建（折叠） | 54.70 MiB | 39.22 MiB | 42.34 ms | 40.27 ms |
| 宽树展开十万子节点 | 41.88 MiB | 20.00 MiB | 12.55 ms | 9.34 ms |

前测为先前 A6 自动验证同协议的数据层结果，不涉及渲染器；[前测](../artifacts/treedatagrid-alignment/a6-completion-20261023/run-3/data-profile.json)和[后测](../artifacts/treedatagrid-alignment/a6-profile-20261023/regression-1/data-profile.json)均保留。耗时受环境变化影响，不能换算成桌面 FPS。

导航编辑同步 UI 的采样分配归因，从约 7316.39 MiB 降到 5504.58 MiB。其中 ReconcileCandidates 约 1127.75 → 20.03 MiB，PrepareProjection 约 1222.26 → 439.12 MiB，BuildNodes 约 1405.12 → 1422.72 MiB（未优化该方法）。这仅验证热点方向；正式比较使用关闭诊断采样的独立进程。

## 原生对照约束

对照应用由原基线发布目录复制，仅替换当前已通过 513 项回归的功能 DLL 及其 PDB，其他文件逐一哈希相同。宿主 SHA256 为 `828B11B19862BADA9DEB2E3AF3210723BEB02407D112720053900FBAD73AE217`，主题仍为原基线的 `B7A2313901531341E0D5A045690E4EAC6B3347C27DEECB1AE7BF68EA8899A410`。优化后功能 DLL 为 `51B02CE611530F9FAEB3AC5221C0E77C91698F3DE259DE8C0ED20E68DA369D9B`。见[应用文件差异](../artifacts/treedatagrid-alignment/a6-profile-20261023/application-comparison.json)。

[原生驱动](../scripts/Test-TreeDataGridNativePerformance.ps1)新增可选 `ApplicationDirectory`，复制已有隔离应用并哈希全部输入，支持保持宿主/主题/依赖字节一致的功能 DLL 对照；默认仍执行锁定还原、Release 构建和发布。输入应用和输出目录禁止嵌套；不覆盖历史证据。正式测量仍使用原 `treedatagrid-native-render-v1` 协议，物理呈现/GPU/物理输入字段保持空。

## 首组无采样原生对照

先运行旧版本，再运行优化版本，每版本六场景、各五个新 fixture，每个 fixture 30 次预热和 60 次正式测量。两个进程各 2700 个请求全部完成并通过独立原始记录校验。固定同一个 1000×650 DIP、RenderScaling=1 窗口协议。以下分配为每场景合计 300 次正式同步 UI 工作的分配，不是单次值、进程总量或存活内存。

| 场景 | UI P50 前 / 后 ms | UI P95 前 / 后 ms | UI 合计分配前 / 后 MiB |
| --- | ---: | ---: | ---: |
| 空操作观察器 | 0.0034 / 0.0036 | 0.0154 / 0.0138 | 0 / 0 |
| 十万行百列 | 22.6477 / 23.5151 | 30.0278 / 39.6449 | 2390.60 / 2390.59 |
| 自动行高/受控绑定 | 20.2360 / 22.7773 | 40.6430 / 46.0540 | 2235.50 / 2235.49 |
| 自动行高/A2/订阅工厂 | 21.1191 / 25.3811 | 40.7806 / 61.5691 | 2253.54 / 2253.52 |
| 十万子节点宽树 | 23.2818 / 26.6274 | 37.4563 / 46.1577 | 2388.20 / 2388.20 |
| 导航编辑 | 47.8103 / 43.3211 | 60.3349 / 58.7895 | 7600.55 / 5705.67 |

导航编辑 UI 分配减少约 24.93%，P50 降约 9.39%。其他四个滚动场景没有触发 Source 重建，UI 分配基本相同；部分 P95 上升。首组不是普遍帧延迟改善的证据，也不能直接认定波动来自环境。十万行后测的第一轮 P50/P95 为 34.87/46.35 ms，其余四轮 P50 为 22.23–24.38 ms；自动行高/工厂组也有轮间差异。后续反向顺序复跑会单列保留，不替换或删除首组结果。

见[前测](../artifacts/treedatagrid-alignment/a6-profile-20261023/fresh-before-performance/report.json)、[后测](../artifacts/treedatagrid-alignment/a6-profile-20261023/after-native-30m/performance/report.json)和[完整分位数/分配比较](../artifacts/treedatagrid-alignment/a6-profile-20261023/unprofiled-performance-comparison.json)。批次渲染完成、UI 分配与物理呈现的区别沿用原协议说明；仍不声明 60 FPS、物理输入延迟或业务预算通过。

## 三十分钟原生耐久

独立进程于香港时间 **2026-10-23 23:54:56–2026-10-24 00:25:20** 运行，正式持续 **1800.4970162 秒**、**901 轮**，另有十轮预热。**7288/7288** 请求匹配，其中正式 7208 次、预热 80 次。完整驱动 passed=true、pilot=false，17 项记录校验测试通过，160 份源码/配置/供应应用输入前后哈希一致。驱动协议没有为本轮调低断言或调查阈值。

每轮仍验证取消零写回、跨工厂/模板/主题保留同一草稿、一次提交、批量移动、选择/当前身份、列显隐/重排、活动 Source 替换与五次双轴滚动。计时内没有宿主显式完整 GC；基线前和结束后各一次排空 GC 阶段。最终空渲染、12 秒释放等待和完整 GC 后，**4577132 个**跟踪退休对象全部回收：contexts2261004、模型1802000、Source1802、Grid901、工厂1802、presenter1802、行44339、单元格445383、列头18099。

| 114 个自然资源检查点 | 结果 |
| --- | ---: |
| 首/末稳态三点私有内存中位数 | 157.75 / 158.84 MiB，增量 +1.09 MiB |
| 计时区间私有内存范围 | 141.05–176.51 MiB |
| 计时区间工作集范围 | 191.18–228.19 MiB |
| 首/末稳态三点句柄中位数 | 694 / 690，增量 -4 |
| 计时区间句柄范围 | 681–697 |
| 计时区间托管堆 API 读数范围 | 18.05–45.35 MiB（114 点均非负） |
| 首/末资源检查点间累计分配 | 约 103.80 GiB |
| 相邻检查点覆盖的每轮平均累计分配 | 约 118.11 MiB；旧十分钟基线约 119.64 MiB |
| 对应 GC.CollectionCount(0/1/2) 增量 | 13283 / 8907 / 2804，代际计数不相加 |

本轮主要收益在大 Source 重建，耐久负载的每轮总分配仅小幅降低，容器/模板/文字布局开销仍存在。按轮比较仅是相同协议的描述统计；不是三十分钟进程内的精确产品净分配，也不能将时长不同的累计 GiB 直接比较。私有内存/句柄没有超过原来的 256 MiB / +128 调查阈值；这仍是有限时长调查护栏，不是业务 SLA 或数小时耐久认证。

**遥测限制：末次托管堆读数无效。** 测量后排空/完整 GC 阶段，运行时 `.NET 10.0.13` 的直接 `GC.GetTotalMemory(false)` 读数为 **-984152 B**。该 API 应表达排除碎片的堆大小，见[官方说明](https://learn.microsoft.com/dotnet/api/system.gc.gettotalmemory)。原始值未修改、未钳成零；114 个持续检查点没有该问题。负值的原因尚未定位，不归因为产品泄漏或已确认的运行时缺陷，也不用于声明最终托管堆大小。已单列[遥测质量记录](../artifacts/treedatagrid-alignment/a6-profile-20261023/telemetry-quality.json)。

现有协议通过条件针对请求/身份/编辑/生命周期、弱引用回收、时长和私有内存/句柄调查护栏；其 passed=true 不表示末次托管堆字段已获有效认证。最终进程私有内存约 114.72 MiB，弱对象回收是独立证据。下一轮需增加托管堆交叉指标与有效性校验，再对该指标下结论。

见[完整结果](../artifacts/treedatagrid-alignment/a6-profile-20261023/after-native-30m/results.json)、[耐久报告](../artifacts/treedatagrid-alignment/a6-profile-20261023/after-native-30m/soak/report.json)、[原始事件](../artifacts/treedatagrid-alignment/a6-profile-20261023/after-native-30m/soak/events.jsonl)、[独立校验](../artifacts/treedatagrid-alignment/a6-profile-20261023/after-native-30m/soak/validated.json)、[进程收据](../artifacts/treedatagrid-alignment/a6-profile-20261023/after-native-30m/soak-process.json)和[输入清单](../artifacts/treedatagrid-alignment/a6-profile-20261023/after-native-30m/source-manifest-final.json)。

## 反向顺序复跑与汇总结论

为检查首组长尾波动，三十分钟耐久完整通过后，再运行优化版→旧版两个独立性能进程。两个进程各 2700 请求均匹配，6 场景/各 5 新 fixture/30 预热/60 正式更新不变。输入文件列表/哈希前后一致，并核对两套应用仅功能 DLL/PDB 不同。见[反向复跑结果](../artifacts/treedatagrid-alignment/a6-profile-20261023/reverse-pair/results.json)和[驱动](../scripts/Compare-TreeDataGridNativePerformance.ps1)。

四个无采样性能进程按旧→新→新→旧顺序完成，共 **10800/10800 请求**；每版本两个进程、每场景十个新 fixture、600 次正式观测，合计 7200 次正式观测。两组之间存在三十分钟耐久间隔，不能视作随机化、独占机器的严格回归实验。下表从原始 completed 事件汇总，帧在 fixture/进程内有相关性。

| 场景 | 汇总 UI P50 前 / 后 ms | 汇总 UI P95 前 / 后 ms | 正式 UI 分配变化 |
| --- | ---: | ---: | ---: |
| 空操作观察器 | 0.0038 / 0.0036 | 0.0145 / 0.0140 | 均为零 |
| 十万行百列 | 22.5622 / 23.0720 | 30.7941 / 38.0372 | 约 0% |
| 自动行高/受控绑定 | 20.6171 / 21.0089 | 41.7414 / 41.2108 | +0.014% |
| 自动行高/A2/订阅工厂 | 21.5941 / 22.0561 | 41.4997 / 51.3868 | +0.014% |
| 十万子节点宽树 | 23.0981 / 23.0232 | 33.8853 / 43.1501 | 约 0% |
| 导航编辑 | 48.9862 / 43.3007 | 61.6992 / 61.9169 | **-24.9319%** |

Source 分配收益在两次新版本运行中重复出现；导航编辑 P50 约降 11.61%，P95 基本持平，不能把中位数改善解释为长尾问题已解决。第一组优化版多个滚动长尾上升，反向复跑优化版自动行高/工厂 P95 为 35.19 ms（旧版 43.04 ms）、宽树为 25.02 ms（旧版 29.82 ms）；上升没有在反向组一致复现，汇总仍包含首组较慢样本。没有删除离群样本、择优替换首组或确认环境为唯一原因。滚动 P95 及其成因继续保留为未解决项。

完整每进程分位数、600 次汇总与原始输入哈希见[正反向比较](../artifacts/treedatagrid-alignment/a6-profile-20261023/counterbalanced-performance-comparison.json)。本轮结论收敛为：大 Source 创建/Refresh 和导航编辑的分配减少，典型编辑耗时改善；有限原生耐久/身份/回收护栏通过。没有获得普遍帧延迟改善、业务预算或物理呈现达标的结论。

## 后续边界

[补充验收矩阵](../artifacts/treedatagrid-alignment/a6-profile-20261023/platform-matrix.json)保留当前 513 项本地自动证据，并给原生性能添加含遥测限制的 NativeMeasured 部分证据；物理条件没有放宽。[Windows](../artifacts/treedatagrid-alignment/a6-profile-20261023/acceptance-windows.json)仍有 8 类待验，[含麒麟](../artifacts/treedatagrid-alignment/a6-profile-20261023/acceptance-windows-kylin.json)仍有 9 类待验，均无记录结构错误，完整 A6 不关闭。

下一轮先增加末次托管堆的交叉指标/有效性检查，再处理已确认的容器挂接、合成对象与文字布局热点，并重复保留全部前后样本。Windows IME/物理交互/DPI/读屏、干净部署、实际 CI、屏幕呈现/物理延迟、多小时负载/业务预算、麒麟及 A3.1 任意不透明原生 CompiledBinding 的自动安全写回继续开放。没有提交、推送或发布公共包。
