# Charts C1 坐标轴与百万点续作

本页保留 C1 里程碑的原始结果和当时开放项。其后非单调索引与 C2、Windows/WSLg 验证继续推进，最新结论见 [C2 续作](charts-c2-common.md)，不要将本页的 198/51/24 或旧 DLL 哈希当作当前候选产物。

执行日期：2026-10-15。继续 [首轮 C0/C1](charts-c0-c1-execution.md)，先验收坐标轴，再修改数据和性能路径。依赖仍为 .NET SDK 10.0.401 / net10.0 / Avalonia 12.0.3，版本仍为 0.2.0-preview.2。C0 文件和 [工程目标 v1](charts-performance-budgets.json) 保持冻结。

## 交付范围

| 工作 | 当前实现 |
| --- | --- |
| 分类与次刻度 | X/Y 业务类别、显式标签、OnTicks/BetweenTicks 与 Gap；数值/日期/log 次间隔、次刻度和次网格。 |
| 标签和多轴布局 | Auto/None/Hide/Wrap/Stagger/Rotate，测量文本、抽稀、最多三行、任意角度；缩放重算密度并保留绘图区；同侧和异侧轴独立留白。 |
| 独立样式 | 总显隐、标题/标签/基线/刻度/主次网格的显隐、字体、画刷、线宽、长度；基线和网格虚线、线帽、连接样式。 |
| 断轴 | inline 或绑定 source、逻辑继承和 INCC/子项变化、区间合并、压缩映射和逆映射、边缘裁剪、断线/命中及标记样式。 |
| 区间 | NaN 视图端点、原始域/分类槽、文字对齐、透明度、显隐、重复数量/上界、系列前后层级。 |
| 轴视图 | 各轴 min/max zoom、TwoWay 轴视图、独立附加轴或归一化联动、滚轮/平移回写和统一重置；外部改视图结束过期拖动。 |
| 数据存储 | 无映射路径的 typed ChartPoint/Point/数值及 nullable 源采用自有不可变值缓冲，包括任意泛型迭代器。公开 metadata 延迟展开，稳定身份按需生成；值源 Move 保留出现次数身份。 |
| 场景与命中 | 轻量坐标读取、坐标 overlay、快照范围缓存；X 搜索索引缩小候选后精确比较；以物理源序保留相等距离的胜者；只对最终命中展开 Item/StableId。 |

Demo 的“坐标轴进阶”展开项提供次网格、自适应标签、断轴、带文字的重复区间、同侧/异侧多轴和独立成功率视图。独立 Usage 与两种 NuGet 消费者包含编译 AXAML 的轴集合绑定、INCC 增改删和卸载重挂。详细配置与边界见 [C1 契约](charts-c1-contracts.md)。

本库仍是独立 API。自动标签默认 Auto、次刻度默认 0、区间透明度默认 1；日期使用 UTC Unix 毫秒，log 间隔使用指数单位。与官方的属性名、默认值和混合系列自动退回类别的策略存在差异。AX12 保留“部分”状态；明确轴上的不兼容值留 Gap，未声称官方程序集可以直接替换。对应 [官方轴文档](https://docs.avaloniaui.net/controls/data-display/charts/shared-elements/axis-customization-chart) 与 [200 项矩阵](charts-official-alignment.md)。

## 性能方法与证据边界

先封存预轴宿主，再封存轴完成后的旧数据实现。最终前后使用**同一 benchmark DLL**，分别装载轴完成旧 core 与最终 core；两端源代码和程序集另留哈希。匹配基线使用新目录，不覆盖最初报告。全套 8 场景各 1 次预热、5 次记录；百万点另用每端 5 个新启动宿主、每进程 1 次预热及 5 次记录，每样本 5 个命中查询。

比较器重新运行独立 raw guard，并核对 harness/目标 SHA、环境/配置、数据集和种子、系列数量、标记大小、无抽样设置、绘图区、视口、primitive 数量及查询的原始值/源索引。统计只合并非预热样本。绘制仍遍历所有输入，Gap 保持原拓扑，无降采样或关闭标记的质量替换。

阶段分配为线程实际累计分配差值；真实 EventPipe trace 单独运行，使用固定 dotnet-trace 10.0.750501 和 gc-verbose / dotnet-sampled-thread-time。类型归因为 GCAllocationTick **采样区间估计**，不是逐类型精确总量；inclusive stack 不可相加。诊断器不混入无 trace 的计时。

内存专项反复换入百万点、绘制和命中，再清空源并卸载；每次推进 Headless render clock 并使用完整 GC 检查点，记录旧源/旧快照弱引用、managed heap、committed、private 和 working set。它检查有限次数的释放和稳态增长，不能代替 30 分钟/8 小时实时长稳，也不能把进程当前 private 读数当成峰值私有增量。

DrawingGroup record 是 Headless 指令记录，**不是 native render completion、present 或 FPS**。正式工程门槛仍为 pending-C4；官方许可参考仍 blocked-reference。没有改低冻结目标来取得通过结论。

## 最终验证

最终 core SHA-256：`6C30F456DC038192D260B7F84F7786A29B7CE633F5335619B35DA1FFC9694F33`。API 见 [当前快照](charts-c1-api.txt)，机器状态见 [状态 JSON](charts-c0-c1-status.json)。

| 检查 | 结果与证据 |
| --- | --- |
| slnx Release | 0 warning / 0 error；[构建日志](../artifacts/charts-build/C1-axes-compact-final-v3/Release.log)。 |
| Charts 自动回归 | **198/198**，0 skipped/not-run；[最终 Windows 验证](../artifacts/charts-platform/C1-axes-compact-20261015-final-v3/results.json)。新增覆盖轴数学/布局/绑定、快照/身份/重入、typed scalar 类型、独立逐点命中 oracle、极端映射及小数据分配预算。 |
| Windows Demo / Usage | **51 / 24** 项通过；亮暗 × X/Y 断轴真实 bitmap 像素均保留两侧、缝内 ink=0；[Demo](../artifacts/charts-platform/C1-axes-compact-20261015-final-v3/charts-smoke.json)、[Usage](../artifacts/charts-platform/C1-axes-compact-20261015-final-v3/charts-usage.json)。 |
| 独立 NuGet 消费 | 功能包与功能+主题两种全新缓存均通过；locked restore、无 ProjectReference，包内/执行 DLL 相符。入口在最终 Windows 报告的 usageAndPackageConsumer。 |
| 输入稳定性 | 最终验证的 validated inputs、完整 source manifest 和程序集首尾一致性均 true。之后只更新仓库说明及结果索引，运行源码未改变。 |
| 外观核查 | 人工检查原生窗口渲染的[亮色](../artifacts/charts-platform/C1-axes-compact-20261015-final-v3/charts-light-c1-axes.png)、[暗色](../artifacts/charts-platform/C1-axes-compact-20261015-final-v3/charts-light-c1-axes-dark.png)；多轴/区间/长标签无明显重叠。非桌面呈现或物理输入验收。 |

文档更新后重新比对 466 个实际构建输入、冻结的 200 个矩阵编号、JSON 和本地文档链接，均通过；[最终复核 v2](../artifacts/charts-platform/C1-axes-compact-20261015-final-v3/post-document-checks-v2.json)。初版文档复核记录因 manifest 相对路径错误作废，旁附 invalid 说明，不能作为通过证据。

### 百万点结果

以下来自 [5+5 独立进程比较](charts-c1-millionpoint-final.json)，每侧 25 个非预热样本、125 次命中。前后 harness SHA 相同，数据 SHA / viewport / plot / marker / primitive / source-index oracle 全部匹配；每次仍绘制完整百万点。分配单位 B：

| 阶段 | p95 ms，前 → 后 | p95 分配 B，前 → 后 |
| --- | --- | --- |
| Source apply | 225.06 → **13.23** | 96,000,520 → **32,072,872** |
| Scene prepare | 194.65 → **12.95** | 240,070,368 → **70,816** |
| Render record | 114.66 → **65.43** | 33,694,488 → 33,694,512 |
| First usable to record | 506.75 → **85.82** | 385,771,928 → **81,840,184** |
| Hit query | 52.62 → **1.77** | 5,320 → **640** |

Render record p50 为 57.96 → 44.38 ms。观察到的进程 current-private 最大读数为 1,119,240,192 → 326,705,152 B（约 1067 → 312 MiB），**不当作 OS 峰值私有内存门槛**。这些是本机诊断统计，没有硬件隔离或正式 SLA 结论。

[全 8 场景比较](charts-c1-axes-performance-final.json) 也通过质量/索引 guard，但并非每个阶段都变快：非单调 10k 的 scene p95 为 1.51 → 13.76 ms（建立有序索引）；小规模 RT-POC source .0739 → .1001 ms、record .3541 → .7231 ms；10k 柱 record 25.6043 → 25.7695 ms。此组只有单进程 5 个样本，p95 等于最大值；相应原始回退没有删除。RT-POC 摄取分配已从 145,896 降至 57,928 B；cached getter 常量分配增加 112 B，百万点 record 增加 24 B。首次可用结果改善不等同于 C4 全场景达标。

### 内存与分配诊断

[独立内存复核](charts-c1-memory-results.json) 重新计算 raw 中的增长、清空残留和引用数。前后使用相同内存宿主；各换入 20 次百万点：

| 检查点 | 旧轴完成版 → 最终版 |
| --- | --- |
| 最后一轮完整 GC 后 managed | 238.36 → **32.00 MiB** |
| 清空并退休旧帧后的 managed 残留 | 8.01 → **0.025 MiB** |
| 第一至第二十轮 managed 增长 | 28.30 → 28.59 KiB |
| 清空后的旧 source / snapshot 存活 | **0 / 0**，两端均为 0 |
| 检查点观察到的最大 private | 649.27 → 153.25 MiB；非 OS 峰值 |

16 MiB 的增长/清空残留限额未改变。未知长度源改为从 32 元素逐步增长至 4096 的自有分块，再形成精确数组；已知 ICollection 仍精确复制。单次泛型枚举，没有为每点建立 Entry/装箱/ID。公开 metadata 仍按需可读，旧快照和 Move 身份保持。

[旧 trace](../artifacts/charts-traces/C1-axes-S1M-before-20261015T084437Z/analysis.json) 与 [最终 trace](../artifacts/charts-traces/C1-axes-S1M-after-final-20261015T093312Z/analysis.md) 均保留。最终收到 446 个 allocation tick、426 个 managed 样本，报告 event loss=0、缺栈=0，2 个未解析地址；约 297.5 MB 的采样区间主要归于 ChartPoint 数组与 Headless Point 几何记录。热点已集中于完整绘图；早期的 Entry/值装箱/完整 metadata 数组不再支配该路径。旧 trace 为预轴/不同 harness，仅辅助定位，严格前后数值采用上面的同 harness 无 trace 结果。

### 保留的失败与修正

早期断轴测试误用 Dummy Headless 的 Contains 判断像素，已改为公开绘图结构 oracle，并另加原生 RTBitmap 像素验证。日期断轴超域、隐藏刻度长偏移、缩放后 Auto 密度、外部轴视图覆盖拖动等真实边界均有回归。

一次 158 项随机顺序测试发生等待；该进程两秒 CPU 增量为 0、线程均等待，EventPipe 未能连接，不能确认具体根因。[诊断证据](../artifacts/charts-traces/test-hang-elevated-20261015T085658Z/cpu-state.json) 保留。测试不再在 Avalonia UI 线程 WaitForPendingFinalizers；随后 197 项多次及最终 198 项（含 seed 314159 和平台运行）通过，未再观察到等待。

初版优化的[内存失败](../artifacts/charts-performance/C1-axes-compact-final/retention.json) 和[中间失败](../artifacts/charts-performance/C1-axes-compact-final-v2/retention.json) 保留：清空残留分别约 32 MiB、16 MiB。先移除未知长度 ToArray 的池化暂存，再修正仅 Drain dispatcher、不推进 Headless render clock 的诊断遗漏；最终在同一内存宿主重测两端均通过。固定大分块还曾增加小实时场景分配，现改为渐增并新增千点分配上限测试。旧 v2 比较 JSON 和 trace 均保留，**当前结论只使用文件名含 final 的 v3 比较及上述最终平台报告**。

## 保留的问题

| 编号 | 剩余问题及退出条件 |
| --- | --- |
| AX12 | 未提供官方的整图兼容性自动退回类别策略；保留显式轴/Gap 契约和迁移差异，不计官方完全等同行为。 |
| C4 增量 | typed 集合编辑目前 copy-on-write O(n)，Move 后身份位置表按需建立；尚无环形窗口、背压、10k 点/s 持续输入与真正增量范围维护。 |
| C4 最坏查询 | 所有点同 X 时仍可能扫描全部候选；外部/自定义非单调投影及屏幕饱和边缘按完整扫描保持正确性。二维空间索引待后续实测。 |
| C4 绘制 | 仍完整记录百万顶点，几何与逐视图变换尚无完整 LOD/分层缓存；对象映射、全量公开 Points 枚举和复杂类别布局仍有相应 metadata 成本。 |
| PERF/OS | native submit/render/present、GPU/软件渲染、HiDPI、多图预算、物理输入和真实长稳尚未验收；不能由本轮 Headless 数字推算。 |
| Linux | 本机 WSL 权限失败，Docker daemon 未运行；已有 Linux/CI 入口尚无实际执行。需 Ubuntu 22.04/24.04 独立构建、窗口、字体、输入和包消费证据。 |
| 官方参考 | 固定 12.3.0 包正常还原，但构建遇 AVLIC0001；获得合法运行条件后再做同机、同数据、同画质运行对照。 |
| C2～C7 | 完整图例/提示/选择、十字线、触摸/键盘/无障碍、更多图族、统计金融、地图层级和发布矩阵仍按原计划推进。 |

首轮 C0 的 54 个来源记录多数只存 URL 元数据，状态 listed-not-downloaded、正文 hash=null；不能称为 54 份网页正文快照。本轮没有重写冻结 C0。后续若需完整网页归档，应新建有正文和 hash 的证据目录。

## 复核命令

```powershell
dotnet build NT.AvaloniaControl.slnx -c Release --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false
pwsh -NoProfile -File scripts/Verify-Charts.ps1 -Mode NativeSynthetic -IncludeC1
pwsh -NoProfile -File scripts/Run-ChartsBenchmarks.ps1 -Quick -Samples 5 -OutputDirectory artifacts/charts-performance/<new-run>
# frozen-host 必须使用 -SkipBuild；每次报告目录必须全新
pwsh -NoProfile -File scripts/Run-ChartsBenchmarks.ps1 -Quick -SkipBuild -HostDirectory <frozen-host> -Scenarios S1M-L -Samples 5 -OutputDirectory <new-run>
dotnet <frozen-host>/Charts.Benchmarks.dll --memory-regression --points 1000000 --cycles 20 --output <new-json>
```

Trace 的安装、采集、分析、event loss 和版本记录见 [独立诊断工具](../benchmarks/Charts.TraceAnalysis/README.md)。官方参考项目不加入普通解决方案构建，不绕过许可检查。
