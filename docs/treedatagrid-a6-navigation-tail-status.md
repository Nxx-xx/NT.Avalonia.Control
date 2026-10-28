# TreeDataGrid A6 导航编辑长尾修复

2026-10-24 自定义工厂长尾修复：自动行高的完整拆挂路径按当前视口预算保留已清理备用行，减少宿主/模板重建。557 项控件回归、90 项校验、4512 条几何快照及本构建 600.56 秒/301 轮原生耐久通过，1204728 个退休对象零保留。正式同状态四进程的自定义工厂 UI P95/分配下降 18.87%/15.31%，渲染完成 P99 下降 19.32%，默认复用收益保留；本次协议内长尾调查收敛，见[本轮记录](treedatagrid-a6-auto-factory-tail-status.md)。历史遥测警告、真实平台/CI/麒麟和业务预算继续单列，整体 A6 未关闭；以下为先前轮次。

2026-10-24 自动行高修复：默认宿主复用已启用，模板测量链缓存与宿主退休边界通过验证。546 项控件回归、68 项原生记录校验、9 项几何校验及 4512 条旧新几何快照通过；增强原生耐久 1800.44 秒/901 轮、3620276 个退休对象最终零保留。首轮宿主自动化缓存失败及性能自动化状态差异均保留，修正后的证据与限制见[本轮记录](treedatagrid-a6-auto-height-status.md)。2 次原始 Any 警告、真实 Windows/CI/麒麟和业务预算继续单列，整体 A6 未关闭；以下是先前轮次。

日期：2026-10-24（香港时间）。承接[托管堆遥测与滚动宿主复用](treedatagrid-a6-telemetry-scroll-status.md)，本轮针对导航编辑的尾延迟回退，保留默认滚动路径和既有公开 API。

结论：同源刷新默认宿主复用已交付，本轮同机双向对照的导航回退收敛，默认滚动收益保留。535 项控件测试、57 项记录校验、逐帧几何/离屏图及增强 30 分钟耐久通过。原始 Any 堆快照不一致、当前堆负读数和真实平台/业务预算仍单列，整体 A6 未关闭。

## 定位与实现

原生源码/配置与上一轮冻结输入完全一致。本轮先用同一个独立场景宿主，对滚动优化前 `51B02CE...`、上轮 `D04DD11...` 做 EventPipe 配对采样，再对修复构建采样；这些记录只用于归因，不替代无采样验收。

确认的瓶颈是：编辑提交引发同源投影刷新时，`SourceChanged → RowsPresenter.Refresh` 清理并脱离所有可见行；下一次布局再挂接模板、合成对象、绑定和文字。上轮复用只覆盖滚动，这条刷新路径没有获得收益。两版每 300 个测量帧仍分配约 5.6 GiB；采样栈中的 `SetLayout`/`SetLayoutCore` 单组各约 1 GiB，另有 `CellPresenter.Clear/Prepare`、`Panel.ChildrenChanged` 和 Source 图/投影重建。不能把所有版本间耗时差都归为一个新方法或 GC 次数差。

本轮让默认工厂、`AutoRowHeight=false` 的同源投影刷新保留已清理宿主到下一次布局。旧模型绑定、内容、编辑器和 A2/行寿命仍同步清理；新 Source 版本和逻辑身份重新准备。未使用的备用宿主在布局结束前脱离；换源、换工厂、换模板及卸载会清理尚未重新准备的宿主。自定义工厂、自动行高及其它刷新入口沿用完整脱离路径。没有跳过 Source 投影发布、降低刷新次数或删除编辑/事件检查。

新增 12 项[刷新契约测试](../tests/TreeDataGrid.Headless.Tests/DefaultRefreshReuseTests.cs)，覆盖同步解绑及原父级复用、导航取消/草稿刷新/单次提交、刷新到布局之间换源/工厂/模板/关闭、清理回调重入和抛错恢复、过滤为空时无残留子控件。早期两项失败源于测试把仍在使用的同源新容器订阅也要求为零；修正为退休容器立即清空、关闭后订阅恢复原值，原失败记录保留。

[完整本地验收](../artifacts/treedatagrid-alignment/a6-navigation-tail-20261024/regression-1/results.json)输入快照前后一致，Core 153、Headless 351、Themes 31，共 **535 项**控件测试通过；包隔离消费、数据/UI/40 轮回收、Usage、Demo 发布及原生程序化 161/29/36 项、图库通过。原生记录校验另有 **57 项**。

## 采样边界

原始记录位于 [profile-pre-reuse](../artifacts/treedatagrid-alignment/a6-navigation-tail-20261024/profile-pre-reuse/attribution.json)、[profile-current](../artifacts/treedatagrid-alignment/a6-navigation-tail-20261024/profile-current/attribution.json)、[profile-fixed](../artifacts/treedatagrid-alignment/a6-navigation-tail-20261024/profile-fixed/attribution.json)。三次均无 EventPipe 丢失；修复后有 1 条无栈事件，其余两次为零，不将采样字节当精确对象数或 CPU 毫秒。

GC 时间线只纳入 `SuspendForGC`/`SuspendForGCPrep`，按 SuspendEEStart/RestartEEStop 的嵌套通知并集，与原始 UI 窗口对齐；`SuspendOther` 排除在 GC 项外。早期未细分原因的草稿记录保留，不作 GC 暂停结论。这是含挂起协调过程的包络，不是精确的逐线程暂停。剩余墙钟时间仍含计算和其它挂起/等待；当前采集器未开启 monitor-contention 关键字，因此不能据事件缺失排除等待。

上轮构建→修复后的采样 UI P95 为 80.6381→46.2434 ms，GC 相关包络与 UI 窗口重叠的 P95 为 30.4941→21.5521 ms；UI 墙钟减去该重叠部分的 P95 为 52.8469→31.8995 ms。它们分别排序，不能相加复原总体 P95，也不能把剩余墙钟称为 CPU 时间。这些观察支持减少刷新重挂接/分配，而不是用“全是 GC/调度噪声”排除回退。

## 耐久负载与遥测口径

本轮新增显式 `--refresh-source` 配置：在默认/自定义工厂每轮第二个滚动请求中执行同源刷新，事先跟踪退休对象；不增加每轮 8 帧数量。独立校验逐条核对请求、周期、工厂、UI 时间窗口和渲染后的 Source 版本。性能模式的六场景与计时边界保持不变。

首次 20 秒试跑的背景 GC 记录出现总碎片 `14024016 B`、分区汇总 `14024496 B` 的 480 B 差异，严格校验使[该试跑保持失败](../artifacts/treedatagrid-alignment/a6-navigation-tail-20261024/refresh-soak-pilot/report.json)。未把它归为已确认的产品或运行时缺陷。后续原 Any 口径试跑单独归档，不能覆盖该失败。

正式耐久使用显式 `--blocking-gc-reference`：`gcSnapshotKind=FullBlocking` 是必需交叉参考，原始 `lastAnyGc` 及其一致性错误仍独立保留、独立重算并警告。没有修改原值、强制额外 GC、隐藏重采或放宽阻塞参考的汇总一致性规则。该 API 按 GC kind 选取记录，见 [.NET GC 源码](https://github.com/dotnet/runtime/blob/v10.0.0/src/coreclr/gc/gc.cpp#L47863)；FullBlocking 数据可能早于当前背景/短代 GC，须连同 kind/index 读取，不能声称是当前堆峰值。

原始 `GetTotalMemory(false)` 的负值仍为可选当前估计不可用，不能用阻塞参考替换它。新增的类型、原始 Any 保留、一致性状态和必需参考校验均有反例测试。

本轮 A6 状态由正式无采样性能、几何和耐久结果决定；真实 Windows 交互、物理呈现/输入延迟、实际 CI、业务预算和麒麟仍分别验收。

## 正式增强耐久

[原生完整运行](../artifacts/treedatagrid-alignment/a6-navigation-tail-20261024/after-native-30m/results.json)通过。独立进程计时 **1800.4467896 秒、901 轮**，每轮 8 帧，另有计时前 10 轮预热；同源刷新覆盖默认工厂 451 轮、自定义工厂 450 轮。计时内没有强制 GC，最终排空/12 秒等待/GC 在计时段外。9 类共 **3,954,234 个**已跟踪退休对象全部回收，保留数为零。

[独立记录校验](../artifacts/treedatagrid-alignment/a6-navigation-tail-20261024/after-native-30m/soak/validated.json)通过，116 份必需 `FullBlocking` 参考均满足堆/碎片/提交量和各区段汇总。原始 Any 样本 101 的总碎片为 25041920 B、分区汇总为 25042328 B，相差 408 B；原值、并发标记、GC index 和不一致警告均保留，其 Any 堆结论仍不合格。它没有被阻塞参考值覆盖，也没有被计成原 Any 口径通过。

稳态首/末三个检查点的私有内存中位数变化 **−8437760 B（约 −8.05 MiB）**，句柄 **−11**，未触发 +256 MiB/+128 句柄的调查护栏；护栏不是业务 SLA。`GetTotalMemory(false)` 的两次负读数继续标为当前估计不可用。新版参考模式只是显式选择已完成 GC 的观测口径，没有改变产品 GC 配置，也没有证明当前堆峰值、物理帧率或多小时耐久预算。

## 正式性能复测

最终功能 DLL 为 `5C48D9D8023683118D9C5CF9C77429489E99E04CED6586C2DB1ECF4F77F7DA50`，对应上述本地回归、包消费及发布目录。比较对象是上轮 `D04DD11D76A03C1EEF0344FB79542AC3B09A0C444C79726970488C14205D8393`。同一新宿主 `B23C88D1D00F127615FAAE5893CA93249D59B1CF4AF356F2D53DCE7F991E3EB8`、主题、依赖完全相同，两个应用只替换功能 DLL/PDB。运行使用 [85 份冻结输入](../artifacts/treedatagrid-alignment/a6-navigation-tail-20261024/native-snapshot-manifest.json)；相对脚本路径以其中 `snapshotRoot` 为根解释。

[完整编排](../artifacts/treedatagrid-alignment/a6-navigation-tail-20261024/orchestration-results.json)及[反向对照](../artifacts/treedatagrid-alignment/a6-navigation-tail-20261024/reverse-pair/results.json)通过。基线→修复→修复→基线四个无采样进程，共 **10800 请求、7200 正式测量帧**；每个场景/版本 600 帧，分属两个进程、10 个 fixture，每个 fixture 先预热 30 帧再测量 60 帧。[QPC 汇总](../artifacts/treedatagrid-alignment/a6-navigation-tail-20261024/abba-analysis.json)逐帧几何/可见身份一致，六张离屏图在四个进程中逐字节一致。帧和 fixture 并非独立随机样本；合并分位数采用 nearest rank，不能解释为产品 SLA。

| 场景 | UI P95，D04→修复（ms） | UI P99（ms） | 同步 UI 分配变化 |
| --- | ---: | ---: | ---: |
| observer | 0.0139 → 0.0145 | 0.0252 → 0.0279 | 0 B → 0 B |
| flat-100k | 20.8248 → 20.4915 | 24.6470 → 26.3410 | +0.0008% |
| auto-default | 40.1562 → 38.8783 | 48.7938 → 49.0708 | +0.0041% |
| auto-factory | 42.0160 → 44.9559 | 52.4786 → 57.9394 | +0.0028% |
| wide-tree | 16.0959 → 18.1882 | 19.3917 → 20.7052 | +0.0010% |
| navigation-edit | 54.4495 → 41.9148 | 63.2739 → 46.0031 | −24.08% |

导航编辑 P95 下降 **23.02%**、P99 下降 **27.30%**、同步分配下降 **24.08%**，渲染完成 P95 下降约 **19.80%**。两组 UI P95 分别为 54.2405→43.0559、54.6545→40.5236 ms，方向一致。该同机同协议有限测量中，导航回退调查项收敛；没有通过减少数据量、容器数量、编辑/事件检查或修正原始时间戳取得收益。

默认滚动相对 D04 的分配量基本不变。宽树 P95 上升 2.0923 ms，自定义工厂 P95 上升 2.9399 ms，保留原读数；六场景均未达到“UI P95 同时增加超过 20% 且超过 5 ms”的调查规则。自动行高仍保留旧路径，自定义工厂不计本轮收益；此前自定义工厂混合负载回退未在本次同机对照中达到相同调查幅度，不等于任意业务负载都已验收。

## 保留默认滚动收益与归档

另外使用同一宿主/依赖，对前轮滚动复用前的 `51B02CE...` 做[单进程参考](../artifacts/treedatagrid-alignment/a6-navigation-tail-20261024/pre-reuse-performance/report.json)，额外 2700 请求、1800 测量帧；与主对照分开计数，不将它声称为又一组双向试验。其全部请求的逻辑几何也与主基线一致。

| 默认滚动场景 | 51B 参考 UI P95（ms） | 修复两进程 UI P95（ms） | 修复相对 51B 的每帧同步分配 |
| --- | ---: | ---: | ---: |
| flat-100k | 32.9646 | 20.7509 / 18.8925 | −55.29% |
| wide-tree | 30.8741 | 18.1641 / 18.3008 | −55.35% |

因此本轮在修复导航编辑刷新路径时，保留了已测默认滚动的分配与尾延迟收益。没有据一次参考进程宣布普遍百分比或业务性能目标已冻结。

[验证汇总](../artifacts/treedatagrid-alignment/a6-navigation-tail-20261024/validation-summary.json)、[当前/冻结输入复核](../artifacts/treedatagrid-alignment/a6-navigation-tail-20261024/current-input-verification.json)、[证据清单](../artifacts/treedatagrid-alignment/a6-navigation-tail-20261024/evidence-manifest.json)记录同一功能 DLL、85 份输入和结果哈希。[部分平台矩阵](../artifacts/treedatagrid-alignment/a6-navigation-tail-20261024/partial-platform-matrix.json)经独立校验保持本地通过、Windows 8 类/完整平台 9 类待验。未创建提交、调整产品版本或发布公共包。
