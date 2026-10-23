# Charts 散点性能与 C2 模板/读数续作

> **历史报告（087 / v3，非当前 v15 结果）。** 本文下方的 378 项测试、四侧性能、227 项增加值和 36 项标记均保留原身份，不作为当前产物验收。最新结果见[尾延迟报告](charts-tail-latency.md)和[验证清单](charts-tail-latency-validation.json)：固定 controlled 目标 100/100，通过不代表所有指标改善；百万点 record/first p95 对 fresh 087 改善 49.38%/37.74%，FULL8/独立散点 record p50 对 CDB6 改善 2.35%/7.05%，仍有 297 项增加值和 75 个描述性标记。独立百万点 hit p95 `0.7649→1.1235 ms`、柱图 warmup first `38.7958→59.4373 ms` 等继续开放，完整回归和正式 SLA 为 null。
>
> 当前 v15 的 Windows 与 Ubuntu 26.04 WSLg 各通过 504/134/51 与两类包消费者各 55 项，Release 零警告/错误；同一份 1186 文件源码与 135 个声明的 live Charts 输入对应。Windows Core/Themes `8372…`/`D0BF…`，Linux `8066…`/`84D8…` 分别记录；公开 API 文本未变，新[元数据快照](charts-tail-api.txt)保留 Core 69 / Themes 3 个公开类型。当前 v15 的 Windows/WSLg 两主题柱图/圆形标记 Direct/Recorded 像素对照各 948 对通过，v11 像素成绩保留历史身份。C2/C4、物理 Linux、present 和官方许可性能对照仍开放。

更新日期：2026-10-16。接续[上一轮性能修复与 C2 续作](charts-c2-followup.md)，保留此前报告及所有失败/历史样本。本轮 C2 子集为 Tooltip 内容模板、SharedY、十字线方向和多轴读数；C2 整阶段仍有未完成项。

最终 v3 已通过 Windows/Ubuntu 26.04 WSLg 功能验证及目标散点性能门槛；FULL8/独立散点记录中位数相对 CDB6 下降 4.67%/6.95%。完整性能回归仍开放，尤其 FULL8 百万点及部分小场景尾延迟；下文区分开发候选、6C2 历史失败与最终 v3 结果。

## 散点定位与开发门槛

使用冻结的同一基准应用 `34055360…859B45E`，C1 `6C30F456`、早期 C2 `048EED53`、上一轮 `CDB6C56A` 和仅拆分圆标记循环的 `E559189D` 四侧交错运行。默认 tiering/PGO、完整点集、散点 MarkerSize=4、1200×650 DIP、字体 12 DIP、命中身份/几何 oracle 均未改变。FULL8 与独立散点各五个进程/侧，每进程一轮预热、五轮测量、每轮五次查询，共 40 个独立进程；两池分开统计，不挑样本。

| 开发候选散点绘制记录，p50 / p95，ms | C1 | 048 | CDB6 | E559 |
|---|---:|---:|---:|---:|
| FULL8 | 19.7043 / 30.1668 | 20.1993 / 29.7870 | 22.8681 / 31.5547 | 19.0090 / 28.8735 |
| 独立散点 | 20.2636 / 31.3557 | 18.8302 / 30.1738 | 18.4340 / 28.7540 | 17.3438 / 27.9530 |

通用 marker 循环包含非圆形几何/变换分支，标准投影的数学未充分内联。修复只为内置笛卡尔投影的 Circle 拆出独立循环，保留 Project、有限值/绘图区检查、运算与 DrawEllipse 次序；自定义投影与其他形状继续原路径。实际 Tier1-OSR 中已内联投影数学，不再调用独立 Normalize/Interpolate，也没有非圆形几何/变换分支。内联展开后的代码为 4668 字节，不能据此宣称原生代码体积减少。默认环境没有全局关闭 tiering 或 PGO。

四项新增独立几何/身份检查后 342 项测试通过。E559 相对 CDB6 的散点记录分配基本相同（FULL8 p95 仅 +8B），不能把约 10.7MB 的共同绝对分配量认定为此次回退的新增原因。初次复现池没有重现中位数回退，后续 pilot/controlled 池出现；全部保留，不将不利结果称为噪声。

这份开发门槛只接受散点修复。FULL8 的百万点首次可用 CDB6 56.1720 / 61.4066 → E559 61.5843 / 71.4232ms、记录中位数 35.4897 → 41.8611ms；柱图首次可用 22.3343 → 23.9273ms、记录 17.2752 → 19.6521ms，缺口/非单调还有描述性差异。百万点 RenderLineCore 的实际 OSR 指令逐行比较无差异，所有折线记录分配相同；这排除了新增点工作/分配，未完成进程生命周期及共享 callee PGO 的因果分解。独立 phase trace 中部分方向反转，不能用诊断时间替代 controlled 结果；GC/JIT 重叠只作相关证据。合入 C2 后仍需完整复核。

- [开发门槛与明确残余](../artifacts/charts-performance/charts-scatter-c2-20261015/controlled-v1/targeted-development-gate.json)
- [逐进程、中位数/尾延迟及首轮预热](../artifacts/charts-performance/charts-scatter-c2-20261015/controlled-v1/results-v2.md)
- [全部原始样本及各场景分阶段汇总](../artifacts/charts-performance/charts-scatter-c2-20261015/controlled-v1/summary-v2.json)
- [478 份开发证据及 SHA 清单](../artifacts/charts-performance/charts-scatter-c2-20261015/controlled-v1/evidence-manifest.json)

## 本轮 C2 契约

`TooltipContentTemplate : IDataTemplate?` 的上下文为 `ChartTooltipContext(Primary, Hits, Text, Anchor)`：Primary 非 null，Hits 保留每个真实原点及原始 Item/SourceIndex/StableId，Anchor 为图表局部 DIP。模板生成真实 Avalonia 控件并在鼠标移动时复用；被动宿主继承资源及提示样式，不参与指针或键盘导航。默认未启用模板或无有效悬停时不创建提示控件。

`TooltipFormatter` 为模板提供 Text。返回 null 时 Text 为空，模板仍可绑定 Hits/Item；没有模板时沿用空文本隐藏提示框。Build 返回 null 表示合法空内容，抑制默认文字提示；Build 异常清理后传播，禁止抢占已有父控件的内容。源、模板、交互取消和卸载清除旧内容及上下文。PNG 导出用公共 VisualBrush 绘制相同的宿主子树一次，不在 Render 中创建或测量模板。

`TooltipMode.SharedY` 为每个系列找最近的可见屏幕 Y 原点，再按 X 距离和靠后的物理 ordinal 解平局。它不插值、不把各系列值改成共同 Y。内置笛卡尔投影按需建立独立 Y 索引，不增加默认 X 索引构造工作；自定义耦合投影保留完整扫描。

`CrosshairDirection` 为 Both（默认）、Horizontal、Vertical。`ShowCrosshair` 控制线；`ShowCrosshairLabels` 默认 false，独立控制标签。Horizontal 绘制 Y 轴标签，Vertical 绘制 X 轴标签。只读 `AxisReadings` 包含 Axis、IsHorizontal、Value、Text；有有效悬停时保存所有参与轴的读数，方向和显隐不影响 MVVM 状态。各轴从同一屏幕位置按自己的变换反算，附加对数轴不复制主轴值。

SharedY 的非单调 Y 首次索引目前使用 O(n log n) 排序及临时 double 键数组。索引只在首次使用 SharedY 时建立，与快照同生命周期；既有 X 索引的 radix 优化及默认性能结果不代表这一新路径已完成百万点资格验收。

## 查看样例

运行 `dotnet run --project samples/NT.AvaloniaControl.Demo -- --page charts`，展开“业务提示卡：共享 Y、方向参考线与多轴读数”。左轴为响应时间，右轴为对数任务量；提示卡保留两条记录各自的时间、原始值和业务 ID。工具栏可切换最近点/共享 X/共享 Y、横线/竖线/双线、标签和模板，也可修改业务记录后重新悬停查看。

独立[Usage AXAML](../samples/Charts.Usage/MainWindow.axaml) 提供三层编译绑定：ChartTooltipContext → ChartPointHit → 原始 ChartUsageRecord，不需要将业务 Item 转成字符串后交给图表。[库接入说明](../src/NT.AvaloniaControl.Charts/README.md) 记录默认值、资源继承、异常与生命周期规则。

## 最终验证

为避开同工作区并行 PDF 开发对共享构建输入的修改，本轮最终使用独立的完整源码快照，而非分别从持续变化的工作区执行两个平台。最终 v3 保留 v2 的完整 1184 文件闭包，只显式覆盖 `ChartDataIndex.cs` 及其已有回归测试，其他 1182 份逐 SHA 相同；两份覆盖均核对来源复制前后、旧/新 SHA 和目标。Windows/Linux 对同一 v3 快照中的全部 519 份实际构建输入执行严格比对。另行验证当前工作区 133 份 Charts 源码、测试、Demo 图表页/宿主、Usage、共享配置与运行脚本仍匹配快照；不宣称工作区中持续开发的其他控件源码与快照相同。

首次工作区 Windows 运行与后续 Linux 准备之间，三份 PdfViewer 源码已变化，严格 guard 正确拦截了 Linux 准备，尚未进入 WSL/build。旧报告和失败准备全部保留，不恢复其他开发的文件，也没有把差异从原 guard 中删去。最终新快照的 Windows 执行期间，完整 manifest、全部 validated inputs 与程序集均未变化。快照不包含 `.git`，Git 查询不可用的原始诊断保留；本次版本身份以源码清单和程序集 SHA 确定。

Windows v3 冻结验证通过 **378 项测试、134 项 Demo、51 项 Usage，以及两类独立包消费者各 55 项**，无失败/跳过。三个专项 Release 项目与两类消费者构建为零警告、零错误。包消费者从同一快照重新 pack，使用独立依赖源及全新包/HTTP 缓存，无 ProjectReference；正常 NuGet 审计策略未改。Core `087ACE2F…C0AA5F5`，Themes `9FC4AE65…355C6F2`，pack/API 后八份程序集 SHA 仍相同。首轮 v3 在 sandbox 的 dotnet 信息查询和 NuGet 漏洞服务访问处失败，未进入构建/测试；原失败回执保留，新编号 `frozen-final-v3-r2` 以正常权限执行通过。这三个专项项目的结果不代表整份解决方案的全部测试已通过。

目视检查本轮六张亮暗模板图/原生窗口 RTBM/导出图及两张旧功能导出图；模板、原始业务 ID、三轴读数与文字均可见。程序化检查分别验证三处轴 gutter 的像素变化，以及限定在真实业务 ID 文本布局区域的文字墨迹；固定 960×540 输出在 96/144/192 DPI 下解码像素相同，pHYs 正确。虚线按开关前后同一区域的像素差验证方向，保留抗锯齿混色及正/负对照；Anchor 与同一 PointerEventArgs.GetPosition(chart) 严格匹配，原生坐标转换前的小数请求值另行记录，不以数据点位置冒充指针位置。

- [1184 文件 v3 源码快照及两项覆盖证明](../artifacts/charts-platform/charts-c2-templates-20261016/source-snapshot-v3.json)
- [Windows v3 冻结验收](../artifacts/charts-platform/charts-c2-templates-20261016/windows/frozen-final-v3-r2/results.json)
- [两类 Windows 独立包消费者](../artifacts/charts-platform/charts-c2-templates-20261016/windows-package-consumers-v3/20261015T174133675Z-88e83a4e33924e47b2da95c067a02019/results.json)
- [Windows 八张新图片的实际目视记录](../artifacts/charts-platform/charts-c2-templates-20261016/windows/frozen-final-v3-r2/visual-review.json)
- [当前 API 元数据清单](charts-c2-templates-readings-api.txt)

Linux v3 冻结验证通过 **378 项测试、134 项 Demo、51 项 Usage，以及两类独立包消费者各 55 项**，无失败/跳过。扫描 46 份执行日志，诊断警告数为 0；三个专项 Release 项目与两个消费者构建均零警告、零错误。Core `9C1B7ACB…BC3C5E9`、Themes `D249D969…940B31C`；跨 OS 的程序集 SHA 分别记录，519 份实际构建输入严格相同，冻结/复制的 1184 份源文件及 Windows/Linux 程序集保持不变。额外核验当前工作区 133 份 Charts 输入仍与快照一致。

环境为 Ubuntu 26.04 LTS x64、WSL2 内核 6.6.114.1-microsoft-standard-WSL2、WSLg 1.0.73，请求 X11/XWayland。实际 backend/renderer/GPU 未由应用确认，保持 null。十张新图片已实际目视；应用 stderr 未出现错误。宿主 WSL 的 localhost/NAT 代理警告、私有 SDK 首次欢迎文本及无 `.git` 查询诊断按原始内容保留。本轮采用已核验的汇总 helper；早期 v2 的空日志/null 和辅助求和失败保留在历史目录，未改写已通过的产品检查。

- [Linux v3 最终摘要](../artifacts/charts-linux/20261015T174300950Z-count-v3-5933c23092a14a808c7622b2b84ab67e/linux-final-summary.json)
- [Linux 全部证据索引](../artifacts/charts-linux/20261015T174300950Z-count-v3-5933c23092a14a808c7622b2b84ab67e/evidence-index.json)
- [Linux 两类独立包消费者](../artifacts/charts-linux/20261015T174300950Z-count-v3-5933c23092a14a808c7622b2b84ab67e/package-consumers/20261015T174736301Z-96268cf7991f490d95b825dd0e7bc3f0/results.json)
- [Linux 零诊断警告日志扫描](../artifacts/charts-linux/20261015T174300950Z-count-v3-5933c23092a14a808c7622b2b84ab67e/build-warning-summary-v3.json)
- [Linux 十张新图片的实际目视记录](../artifacts/charts-linux/20261015T174300950Z-count-v3-5933c23092a14a808c7622b2b84ab67e/visual-review.json)

两平台结果均限 Headless、合成路由、原生窗口程序化绑定和 RTBM/PNG；不替代物理输入、系统 DPI/跨屏、桌面 compositor present 或原生读屏验收。开发候选 E559 的 342 项测试及成绩不作为最终组合版本的验收结果。

## 6C2 首次组合性能复核：保留的失败

追加 Count 之前的组合版本 `6C2CDE80` 已完成四侧、80 个独立计时进程及四侧各 20 轮内存检查，执行和几何/身份检查均通过，**目标散点性能门槛失败**。FULL8 散点记录 p50/p95 为 18.5964/28.7235ms，对照 C1 为 20.1366/27.1027ms：中位数改善，p95 增加 5.98%（1.6208ms），超过事先冻结的 5% 且至少 0.05ms 门槛。独立散点池不代替 FULL8，两池完整样本分别保留。该版本对应的 v2 平台回执继续保留，不作为最新 Count 版本的验证。

同一 FULL8 的百万点 prepare p50 从 CDB6 的 4.8664 增至 11.0233ms，记录从 35.2416 增至 46.7883ms，首次可用从 54.4955 增至 66.8458ms；独立百万点池的首次可用中位数则改善。四侧内存检查的旧源和旧快照最终存活数均为 0，但该候选残留比 CDB6 增加，不能称为所有内存指标均无回退。失败候选、逐进程/分阶段数据及全部增加项已封存，后续有界诊断见下一节；此处不将失败门槛标为完成。

静态对照表明，CDB6 与 6C2 的 GetScene/BuildScene/数据失效及默认索引源码相同；新增模板与读数在未悬停时快速返回，没有额外的百万点扫描或重复布局。实际采样 IP 所在的索引构造代码两侧均为 Tier1-OSR，不能将双峰直接归因于不同 tier；两侧循环都仍逐点调用 IChartSnapshotData.Count。后续将不可变点数缓存到循环外的单项候选不改点数、排序、有限值、几何或命中规则。[静态路径排除及可复现比较](../artifacts/charts-platform/charts-c2-templates-20261016/integrity/static-default-path-review.md)

- [最终组合版本原始执行回执](../artifacts/charts-performance/charts-c2-readings-20261016/final-controlled-6c2cde80-v1/driver-result.json)
- [失败的目标散点门槛](../artifacts/charts-performance/charts-c2-readings-20261016/final-controlled-6c2cde80-v1/reports-v1/prototype-regression-gate.json)
- [全部场景的汇总及增加项](../artifacts/charts-performance/charts-c2-readings-20261016/final-controlled-6c2cde80-v1/reports-v1/summary.json)
- [6C2 当时的 Windows v2 功能回执](../artifacts/charts-platform/charts-c2-templates-20261016/windows/frozen-final-v2/results.json)
- [6C2 当时的 Linux v2 功能回执](../artifacts/charts-linux/20261015T163930025Z-frozen-source-62aabdb933dc45fc8737b3a354872032/linux-final-summary.json)

## 点数缓存及诊断边界

追加候选只将 `ChartDataIndex` 构造中的不可变 Count 缓存到局部，主扫描、终判与回退扫描复用同一值。原有两项索引测试增加 getter 读取计数预算，原几何、排序、身份断言保留，仍为 378 项测试。真实 native 显示旧 OSR 每点一次 Count 接口调用，新候选的入口读取一次、OSR 循环内零次；这项减少工作量的结论有实际代码证据。

三侧、两 pair 的独立 pilot 共 18 个进程，完整八场景、独立散点与独立百万点分别统计。FULL8 百万点 prepare 的 6C2 → Count 候选 p50/p95 为 4.8248/11.4927 → 4.3309/4.9561ms；GAP 为 0.3683/0.4291 → 0.1893/0.2430ms，非单调为 0.6944/1.3129 → 0.4986/0.5388ms。散点记录两池变化未越过原实质回退界限，但百万点 FULL8 record/first 仍有明显不利值。pilot 仅支持进入新功能候选的双平台复核，不替代最终四侧 80 进程验收。

Win32 实际枚举表明本机有两种 EfficiencyClass；处理器为 i5-14400F，官方规格列出 6 个性能核与 4 个能效核。[只读本机拓扑回执](../artifacts/charts-platform/charts-c2-templates-20261016/integrity/cpu-topology-v1.json)、[Intel 规格](https://www.intel.com/content/www/us/en/products/sku/236777/intel-core-i5-processor-14400f-20m-cache-up-to-4-70-ghz/specifications.html)、[Win32 CPU Set 字段说明](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-system_cpu_set_information)。仅约束诊断子进程的 P/E 组实验表明核心类能放大耗时，但同 P 类仍出现版本差异；随后同 P 类 phase trace 又未重现该慢值。既有 EventPipe 的 processorNumber 为会话常量 0/101，不可用于反推历史样本的核心归属。

因此，核心类、GC/JIT 重叠及相同源码/OSR 均不足以单独解释旧 controlled 差值。实际逐点 paint 路径也未出现假设中的 ToImmutable/clone；同类时序及生命周期贡献尚未分割。诊断池不并入正式统计，保留失败门槛，当前不继续加入无证据的 runtime 改动。

- [完整诊断、首次可用表与残余评审](../artifacts/charts-performance/charts-c2-readings-20261016/failed-6c2-review-zh.md)
- [逐项残余及其原始证据](../artifacts/charts-performance/charts-c2-readings-20261016/residual-review-v1.json)
- [1836 份原始、失败及诊断证据的 SHA 图谱](../artifacts/charts-performance/charts-c2-readings-20261016/final-controlled-6c2cde80-v1/evidence-manifest.json)

## 最终 v3 性能和内存复核

最终 Windows Core `087ACE2F` 与上述双平台功能候选关联，使用同一冻结 `34055360` 基准应用、默认 tiering/PGO、完整原始点集和原有几何/命中 oracle。C1、048、CDB6、当前四侧交错执行，共 80 个独立计时进程、12 组严格比较及四侧各 20 轮内存检查，执行与质量 guard 全部通过。FULL8、独立散点、独立百万点各侧五进程，每进程一轮预热、五轮测量、每轮五次查询；独立非单调为三轮预热、十轮测量、每轮 25 次查询。四池分开，预热完整保留。此版本仅执行这一次最终控制池，没有修改原 `91CBDCAD` 策略或重选样本。

**目标散点门槛通过**：FULL8 和独立散点的记录中位数均改善，记录/首次可用的 p50/p95 对全部三个控制侧均符合原门槛。以下单位均为 ms，单元格为 p50 / p95。

| 散点绘制记录 | C1 | 048 | CDB6 | 当前 v3 |
|---|---:|---:|---:|---:|
| FULL8 | 20.4052 / 30.1056 | 19.8211 / 29.0156 | 19.0894 / 29.2271 | 18.1971 / 28.3395 |
| 独立散点 | 17.9594 / 27.7057 | 18.7674 / 27.9207 | 18.5246 / 30.0299 | 17.2373 / 27.2514 |

相对 CDB6，两个散点池的记录中位数分别下降 4.67% 和 6.95%。首次可用 FULL8 为 19.6336/29.6961 → 18.7523/28.7930ms，独立池为 19.2077/30.6500 → 17.7009/27.8597ms。散点记录分配 p95 为 FULL8 10,717,240 → 10,713,280B，独立池两侧均为 10,712,400B；未通过减少点数或绘制对象的语义来换取时间。

当前精确 DLL 的 Circle Tier1-OSR 为 4618B，人工核对实际指令后确认已纳入投影/归一化/插值数学，无独立 Project/Normalize/Interpolate、通用 marker 形状 helper 或标记变换调用。轴变换、log、断轴和域有效性分支及冷调用仍保留；native 结构证据不解释或推翻其他场景的不利时间。

| 百万点池 / 阶段 | CDB6 p50 / p95 | 当前 v3 p50 / p95 |
|---|---:|---:|
| FULL8 prepare | 4.8229 / 10.6924 | 4.3286 / 4.7139 |
| FULL8 record | 35.9169 / 42.4125 | 33.6739 / 46.7521 |
| 独立百万点 prepare | 4.7375 / 6.1128 | 4.4227 / 5.0297 |
| 独立百万点 record | 36.6608 / 38.9612 | 36.0422 / 38.4797 |

Count 优化的 FULL8 百万点 prepare p95 下降 55.91%；但同池 record p95 增加 10.23%，首次可用 p95 增加 11.86%。独立百万点首次可用为 53.6820/58.1463 → 52.8772/56.6804ms，不能用这一改善冲销 FULL8 尾延迟。

| FULL8 首次可用 | CDB6 p50 / p95 | 当前 v3 p50 / p95 |
|---|---:|---:|
| S10-L | 1.3494 / 4.3162 | 1.3441 / 3.9056 |
| S10-S | 19.6336 / 29.6961 | 18.7523 / 28.7930 |
| S10-B | 23.0624 / 29.0021 | 23.3059 / 29.8695 |
| S100-L | 6.0839 / 8.8930 | 6.6648 / 8.3618 |
| S1M-L | 54.9435 / 60.3563 | 51.2355 / 67.5120 |
| 非单调 | 1.3464 / 1.4502 | 1.2244 / 1.7323 |
| 缺口 | 2.1523 / 2.3348 | 2.0988 / 2.6310 |
| RT-POC | 0.5494 / 0.6296 | 0.5734 / 0.8213 |

**完整性能回归尚未通过**。当前仍有 227 项增加值、36 项描述性实质标记；完整清单含时间、分配、最大值及所有基线，不只列改善项。除百万点尾延迟外，100k record 中位数 4.1490 → 4.5314ms（+9.22%），RT 首次可用 p95 0.6296 → 0.8213ms（+30.45%），非单调/缺口尾延迟及部分命中指标仍需处理。`nonTargetReviewRequired=true`，`nonTargetRegressionReviewPassed` 与 `completeRegressionPassed` 均为 null；目标散点通过不关闭这些剩余项。

| 四侧各 20 轮 numeric forced-GC | plateau growth，B | clear residual，B | 最终 source / snapshot 存活 |
|---|---:|---:|---:|
| C1 | 29,240 | 26,432 | 0 / 0 |
| 048 | 29,240 | 26,432 | 0 / 0 |
| CDB6 | 4,632 | 1,824 | 0 / 0 |
| 当前 v3 | 4,600 | 26,368 | 0 / 0 |

旧 source/snapshot 存活检查均通过，沿用原 16MiB 限制。当前 clear residual 比 CDB6 多 24,544B，不能宣称所有内存指标保持原水平；此检查也不代表任意长类别文本、native/GPU 峰值或长稳已验收。

first 仍是既有 harness 从源复制/应用、prepare、记录至首个 hit 的计时窗口，窗口创建及初始 layout/JIT 在计时域外，原始预热单列；它不是进程冷启动或 compositor present。Linux 本轮没有测性能。正式资格继续为 `pending-C4`、`blocked-present/reference`、`formalPerformancePassed=null`。

- [最终 80 进程及四侧内存执行回执](../artifacts/charts-performance/charts-c2-readings-20261016/final-controlled-087ace2f-v1/driver-result.json)
- [通过的目标散点门槛及完整非目标开放项](../artifacts/charts-performance/charts-c2-readings-20261016/final-controlled-087ace2f-v1/reports-v1/prototype-regression-gate.json)
- [最终全部原始池及增加指标](../artifacts/charts-performance/charts-c2-readings-20261016/final-controlled-087ace2f-v1/reports-v1/summary.json)
- [四侧全部场景分阶段表与预热说明](../artifacts/charts-performance/charts-c2-readings-20261016/final-controlled-087ace2f-v1/reports-v1/results-zh.md)
- [最终中文结论与完整残余评审](../artifacts/charts-performance/charts-c2-readings-20261016/final-controlled-087ace2f-v1/reports-v1/final-review-zh.md)
- [最终源码/程序集冻结身份](../artifacts/charts-performance/charts-c2-readings-20261016/final-c2-readings-087ace2f/freeze.json)
- [最终证据及 SHA 图谱](../artifacts/charts-performance/charts-c2-readings-20261016/final-controlled-087ace2f-v1/evidence-manifest.json)
- [双平台、性能和剩余项汇总 JSON](charts-c2-templates-readings-validation.json)
- [最终证据一致性复核回执](../artifacts/charts-platform/charts-c2-templates-20261016/integrity/final-evidence-v3-v2.json)

首次证据复核因 Windows 构建 stdout 中已保存的中文乱码而在摘要解析处停止；[失败回执](../artifacts/charts-platform/charts-c2-templates-20261016/integrity/final-evidence-v3-v1.json)及原始日志保留。复核工具仅增加由原中文标签精确派生的编码分支，仍要求成功摘要及警告、错误计数均符合原规则；产品构建、测试和性能结果未改写。

## 剩余工作

本轮散点目标门槛已通过。下一步先处理最终控制池的 FULL8 百万点记录/首次可用尾延迟、100k/RT 及非单调/缺口尾延迟，继续分割进程生命周期和同类核心内的时序贡献；旧 6C2 失败作为历史保留。Count 减少工作量和原始数据/几何一致性已有证据，正式帧/呈现、物理硬件资格及全部指标无回退仍未完成。

图例/数据标签内容模板、线段插值及其命中规则、完整 Trackball、系列选择/范围选择器、触摸、动画、JPEG 和点级辅助功能继续按 C2 计划推进。C4 仍包括增量/LOD、原生/GPU 内存、字节预算、长稳及实际呈现耗时。Windows/Linux 物理输入、桌面 DPI/跨屏、指定发行版/ARM、原生辅助工具及合法可运行的官方参考性能对照仍需各自证据；现有开发门槛不能替代。
