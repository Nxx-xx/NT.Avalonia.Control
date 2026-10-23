# Charts 百万点与小场景尾延迟

更新：2026-10-16。最终产品候选为 v15。本轮固定对照池的 **100/100 项目标门禁通过**：FULL8 的 64 项实质回退检查、百万点两项 p95 改善目标，以及 34 项散点保留检查均通过。Windows 与 Linux 各自完成 504/504 Headless、134 项 Demo、51 项 Usage，以及两个全新缓存包消费者各 55 项验证。独立柱图/圆形标记像素对照四组均通过，共 **1896 对**。全部当前证据见[验证记录](charts-tail-latency-validation.json)，没有沿用上一轮 087 的成绩。

这是 `controlled-prototype` 结果。`renderRecord` 是 DrawingGroup 命令记录；`firstUsableToRecord` 包含宿主复制数据、赋源、准备场景、记录及首次命中查询，不包含完整进程启动、初次窗口/layout 或屏幕 present。`formalPerformancePassed=null`、`formalSlaEvaluated=false`、`completeRegressionPassed=null` 继续保留，C4、真实呈现、物理目标桌面和官方许可对照尚未验收。

## 改动与定位证据

- 数值坐标投影保留原 double 除法、端点快捷返回、反向轴及 FMA 次序；只在有限值、轴类型、break 和范围证明成立时使用专用循环。折线保留每个点与 figure/gap 顺序，散点保留逐点 DrawEllipse，未加入 LOD、删点或更换命中 oracle。
- 小型折线及坐标轴使用局部优化入口；大折线保留默认分层编译。无 break 时减少空裁剪迭代，Y 轴标签列布局在同一轴内复用。
- 固色柱图在 exact DrawingGroup recorder 中最多缓存 8192 个私有 RectangleGeometry。每次重算全部原矩形，四项 Rect bit pattern 相同才复用；直接原生绘制、其他 context、paint、marker、超限或不满足投影条件的路径保留原实现。隐藏、移除和卸载图表时清理缓存，旧 DrawingGroup 仍有效。数组元数据上限约 320 KiB/series，另有托管与原生 geometry 成本，不能把它称为总字节预算。
- 未知长度数值源的 4096 元素临时块使用一个跨全部 T 的池，最多 16 块、1 MiB **元素载荷**；每次采集也受同一租借数量/载荷上限约束。超预算块普通分配，不返池或清零；最终 owned 数组、已知长度与 ICollection 路径不入池。归还前清零，pool 锁不在逐点循环中。数组头、登记表、in-flight 块、最终数组及原生内存不属于该载荷预算。
- 将异常归还 wrapper 与热枚举 Core 分开，仍单次枚举、先 Dispose 后归还。实际 v12 CollectOwned 只有 Tier0-FullOpts；v15 Core 重新捕获到 Instrumented Tier0 与 Tier1-OSR，正常逐点路径没有池查询，租借只在扩块分支执行。所有权、异常、重入、并发、混合类型及超过租借预算的数据均有回归。
- 折线/散点圆形 marker 使用内置 CartesianProjection、exact recorder 且画笔笔刷为 exact ImmutableSolidColorBrush 时，每次绘制在循环外生成一份 ImmutablePen。独立的具体类型 helper 避免逐点读取可变 Pen 样式属性；其他路径保留原 Pen helper。已捕获的 in-range 线性圆图循环 OSR 从 2268 B 变为 1153 B，仍有两次 scalar divide、两次 FMA 和一次 DrawEllipseCore 调用，捕获的 metadata/Dispatcher 调用从各 2 次变为 0 次。**代码长度不是性能或唯一因果证据**，最终判断使用下述独立对照池。

公开 IPen 的笔刷、线宽、端帽、连接和 miter/dash 值保持一致；受限 recorder 输出中的具体 Pen 类型发生变化，不能继续假设它一定能强转为可变 Pen。可变笔刷保留原行为，包括修改同一笔刷实例时旧绘图记录可观察到变化。未证实记录路径逐点调用 Pen.ToImmutable；本次优化不以“消除了逐点 immutable pen 分配”作为解释。

机器码与阶段诊断：[最终 native 原件](../artifacts/charts-performance/charts-tail-20261016/native-8372-v1/capture.json)、[来源适配与闭包](../artifacts/charts-performance/charts-tail-20261016/native-8372-v1/native-provenance.json)、[逐 body 核验](../artifacts/charts-performance/charts-tail-20261016/v15-native-body-review-v1/analysis.json)、[087/v12/v13 采集循环复核](../artifacts/charts-performance/charts-tail-20261016/test-review/compact-native-087-v12-v13-review-v2/manual-review-zh.md)。这些计时均为 diagnostic-only，不能替换验收样本，也不能证明某个测量样本执行了哪个 tier。

独立阶段计数诊断保留 3246 行及 288 组 memory，观测到万点折线第 4 个 measured 样本的宿主复制窗口出现 GC，散点 30/30 个 measured record 窗口也有 GC。嵌套 first 与内部窗口不双计 GC/pause；这些是另一组进程，不能为原 pilot 样本确定因果、扣除暂停或豁免回退。见[完整阶段记录](../artifacts/charts-performance/charts-tail-20261016/stage-counter-diagnostic-v4-v13/analysis-v1/assessment-zh.md)。

## 固定协议与结果

宿主保持 `34055360A2D39F711175FC0191E6EE0DC57A0279DC15527AEDDF4C859289B45E`，只替换冻结产品 DLL/PDB；未重建或修改计时应用。保持 FULL8 原顺序、1200×650 DIP、原数据、点数、markers、绘制原语、查询、默认 tiering/PGO、预热与计时边界，无 affinity/运行时设置调整。

[原策略](../artifacts/charts-performance/charts-tail-20261016/tail-regression-policy-v1.json) SHA `F6D2796A7299427E8204BB726A058D700BD4BE2BEFF7D0EDA3AF09453A772302` 在候选测量前冻结。实质回退要求同时满足 ratio > 1.05 且增加至少 0.05 ms；百万点 record/first p95 对 fresh 087 均须改善至少 10%。散点保留原严格中位数改善及四侧回退条件，不用其他场景收益抵销失败。

最终池包含 80 个计时进程、12 组比较与三侧各 20 次换源的内存检查。FULL8/scatter 各五侧×五进程，million/nonmonotonic 各三侧×五进程；前三池每进程 1 warmup/5 measured/5 queries，nonmonotonic 为 3/10/25。分池统计，全部暖身、测量样本与逐进程分布保留，未删极值或合并不同场景池。

以下为同一最终 FULL8 池的 pooled p95，单位 ms，各侧每场景 25 个 measured 样本。stage 的分位数来自各自分布，不能相加解释 first。

| 场景 | fresh 087 record | v15 record | fresh 087 first | v15 first |
|---|---:|---:|---:|---:|
| 万点折线 | 0.9661 | 0.6416 | 4.5524 | 4.2783 |
| 万点散点 | 30.4419 | 29.1878 | 30.9527 | 29.6777 |
| 万点柱图 | 27.8096 | 22.6701 | 32.6145 | 29.3657 |
| 十万点折线 | 7.7650 | 5.9816 | 9.5339 | 8.3073 |
| 百万点折线 | 51.7989 | 26.2218 | 76.9736 | 47.9239 |
| 非单调折线 | 1.0851 | 0.5704 | 1.7917 | 1.2704 |
| 含 gap 折线 | 2.3480 | 1.7973 | 2.8091 | 2.2885 |
| 小型实时 PoC | 0.4472 | 0.3774 | 0.7114 | 0.6575 |

百万点 record/first p95 分别改善 **49.38% / 37.74%**。百万点 sourceApply p50/p95 为 10.8685/16.0030→9.8430/12.9155 ms，分配 p50 为 32,066,744→31,017,968 B；不是百万点整体仅分配这些字节，宿主复制与绘图记录另计。其五个 warmup first 为 42.9517、45.8869、44.9265、46.2721、48.5436 ms，原始暖身成本没有移入或扣除 measured 指标。

FULL8/独立 scatter record p50 对 fresh CDB6 分别改善 **2.35% / 7.05%**。100 项通过不表示每个数值都降低：FULL8 scatter record p50 对 fresh 087 为 19.8114→20.5791 ms，增加约 3.88%，仍在原门禁容许范围；独立 scatter record p95 为 28.0836→28.6304 ms，亦完整保留。最终共有 **297 项增加值、75 个描述性实质标记**，非目标指标仍单独审阅，`nonTargetRegressionReviewPassed=null`，不能将目标通过扩写成完整性能回归通过。

其中特别保留：独立百万点 hit p95 对 087 为 0.7649→1.1235 ms；FULL8 十万点 sourceApply p95 为 1.4716→1.9999 ms、prepare p95 为 0.7238→0.8986 ms；FULL8 百万点 prepare p95 为 4.8472→5.3829 ms。这些仍是开放的回退。首个观测 warmup 的 first 也存在成本增加：柱图 p50 对 087 为 38.7958→59.4373 ms，gap 为 2.5584→3.8236 ms，实时 PoC 为 0.9074→1.7445 ms。warmup 不是完整应用冷启动，但不能用 measured 通过掩盖这些首次路径成本；它们继续纳入 C4 和完整回归工作。

原件：[最终 summary](../artifacts/charts-performance/charts-tail-20261016/candidate-v15-formal-v1/reports-v1/summary.json)（SHA `D784F9A222DD24EA2A0547AB0357FD3036753DF3A9130C1669444E665995380E`）、[100 项 gate](../artifacts/charts-performance/charts-tail-20261016/candidate-v15-formal-v1/reports-v1/prototype-tail-gate.json)（SHA `992D8D163550EAB810C5F0CC78832A8024FD12252F7A4F5D2A62BE3B4D413BA2`）、[精简数值提取](../artifacts/charts-performance/charts-tail-20261016/v15-formal-accepted-target-summary-v1.json)、[非目标指标审阅](../artifacts/charts-performance/charts-tail-20261016/v15-formal-nontarget-review-v1/review-zh.md)。独立复核逐一验证 80 份 raw SHA、255 组场景样本序列及 2601 项 pooled 统计；保留 285 个 warmup、1350 个 measured 样本和 12075 次查询。

## 内存与功能验证

独立内存宿主保持 `5DA2E701C96F2CE04BDD94419F2C0F040F83C992D9263BE3BED3DD30FF36503A`。每侧 20 次百万点换源，强制 GC 是保留量诊断，不是计时池的运行策略。plateau/clear residual 各 16 MiB 的原门槛不变。

| 侧 | plateau growth B | cleared residual B | source/snapshot survivors |
|---|---:|---:|---:|
| CDB6 | 4,632 | 1,824 | 0/0 |
| 087 | 29,176 | 26,368 | 0/0 |
| v15 | 4,632 | 1,050,960 | 0/0 |

三侧均通过。v15 清源残余比 CDB6 多 **1,049,136 B**，是需要公开的有界保留代价；不是“零新增内存”。FULL8 柱图最高采样 private 对 087 为 62,185,472→72,257,536 B（约 +9.61 MiB / +16.20%），十万点为 71,733,248→92,012,544 B（约 +19.34 MiB / +28.27%）；十万点 prepare 分配 p95/max 为 12,840→16,360 B。这些是实际残余，不等于图表独占内存、OS peak、原生保留或泄漏量；原始报告保留其全部 checkpoint。该 fixture 不覆盖柱图 geometry 的完整原生字节预算、长文本/分类、任意自定义系列和 8 小时长稳。

| 验证范围 | Windows | Linux |
|---|---|---|
| 本轮候选 Headless | 504/504，Release 0 warning/error | 504/504，Release 0 warning/error |
| Demo / Usage / 全新缓存包消费者 | 134 / 51 / 两个各 55 | 134 / 51 / 两个各 55 |
| 本轮柱图/圆形标记 Direct / Recorded 像素对照 | Light 474/474、Dark 474/474 | Light 474/474、Dark 474/474 |
| 物理输入、桌面 present、正式平台性能 | 未验收 | 未验收 |

Windows 与 Linux 各执行三个 Charts 项目 Release 构建，零编译 warning/error；各自 521 个验证输入和 8 个输出程序集保持稳定。两个包消费者分别从当前构建重新打包，使用独立全新 package cache，正常 restore 与 locked restore，且没有 ProjectReference。Linux 使用 Ubuntu 26.04 / WSL2 / WSLg，其 1186 份源码与冻结 Windows 输入逐 SHA 对齐。实际窗口的 RenderTargetBitmap 已逐图检查，不能当作 compositor 截图。当前证据：[Windows](../artifacts/charts-performance/charts-tail-20261016/platform-final-v15/windows-final-summary.json)、[Linux](../artifacts/charts-performance/charts-tail-20261016/platform-final-v15/linux/final-v1/linux-final-summary.json)。两平台实际 backend/renderer 字段仍为 null，未据此推断 GPU 或 present。

独立像素 fixture v4 的首轮 Windows Light 在 restore/locked restore 成功后因新增 CirclePenProbe 的四项 nullable Dashes 编译错误终止，未执行 native；[失败原件](../artifacts/charts-performance/charts-tail-20261016/bar-cache-pixel-v4-runs/windows-final-light-v1/results.json)完整保留。新 v5 仅修复 null/array 读取与比较，保留 null/empty 区分、全部 IPen/geometry/pixel 断言，以及每主题 348 对柱图和 126 对圆形 marker 的计划。[v5 Windows Light 实际结果](../artifacts/charts-performance/charts-tail-20261016/bar-cache-pixel-v5-runs/windows-final-light-v1/results.json) SHA `162F2E393D9ADB41CCD67B0EC6A799D9BAEFD7AB39C8A1827DB7219040C411FB`，474/474 对通过。Windows Dark 首轮因磁盘满在 build/native 前中断，旧 `windows-final-dark-v1/results.json` 为 0 字节，不能作有效报告；保留目录及日志，恢复后在全新 `windows-final-dark-v2` 完成验证。

v5 的 Windows/WSLg 两主题实际各通过 948 对，总计 1392 对柱图、504 对圆形标记，生成 3792 张 actual/expected PNG。每组分别在本平台以 Direct 和 Recorded 模式对照原路径，覆盖 96/144/192 DPI，全部像素差为 0；每组另有 54 项新记录 pen 值/笔刷/共享身份检查和 9 项旧 pen 保持检查。旧 DrawingGroup 的样式、变换及后续新帧均纳入回归。四组正常 restore、locked restore、Release build、native 执行均成功，构建零 warning/error；独立 package cache 使用逐 SHA 复制的 HTTP seed，来源与复制记录保留，不将其称为新网络下载。实际窗口 RTBM、bitmap DPI 与图内 transform 的检查范围仍不能替代桌面 present、OS DPI/跨屏和实体输入。

四组全部 PNG 文件的 SHA 已逐一核验，8 张两平台/两主题的柱图与圆形标记代表图也分别由执行者和主线程实际查看。原件见[像素总表](../artifacts/charts-performance/charts-tail-20261016/pixel-v5-final-evidence/pixel-final-summary-v1.json)（SHA `DC287568441837908AAFAF8A86BF00FCD1483DADF203FCBDDA066A48C1ED3E62`）、[完整证据索引](../artifacts/charts-performance/charts-tail-20261016/pixel-v5-final-evidence/pixel-evidence-index.json)、[主线程结果复核](../artifacts/charts-performance/charts-tail-20261016/test-review/pixel-v5-four-run-review-root-v1.json)及[主线程目视记录](../artifacts/charts-performance/charts-tail-20261016/test-review/pixel-v5-visual-review-root-v1.json)。最终[源码与程序集闭包](../artifacts/charts-performance/charts-tail-20261016/pixel-v5-final-evidence/source-assembly-final.json)确认 Windows/Linux 共 16 份发布输出未变；[独立源码守卫](../artifacts/charts-performance/charts-tail-20261016/test-review/v15-source-after-final-validation-v1.json)再次验证 1186/9/135 的原范围。所有自有重进程均已退出，平台基础报告的独立像素 scope=false 原字段保留，以本次追加报告记录新范围。

磁盘维护发生在最终性能池结束之后，没有重跑计时池或改写其结果。维护清理了全局下载缓存与 live 项目的可重建 bin/obj，对本任务原件仅做 NTFS 无损压缩；冻结源码、私有运行输出和 HTTP seed 保留。维护后[源码守卫](../artifacts/maintenance/disk-cleanup-20261016/charts-source-after-cleanup.json)重新核验 1186 份冻结文件、9 overlay、135 个 live Charts 输入及精确文件集合，全部匹配；六份关键性能/平台/像素来源报告的 SHA 也保持不变。后续验证继续使用对应平台的实际冻结 DLL。

此前 087 的 Windows/Ubuntu WSLg 378/134/51/两消费者各55 是[历史版本结果](charts-c2-templates-readings.md)。v11 曾取得 Windows 两主题 696 对 bar 像素一致；它也不能替最终 v15 或 Linux 作结论。本表只按当前候选的真实新 receipt 更新。

最终冻结源码为 **1186 文件、9 overlay、1177 项父快照不变**；135 个声明的 live Charts 输入分别守卫。其余 live Markdown/PDF 修改没有并入本次完整冻结源码身份声明。源码 snapshot SHA `2ADDFFE5458E49CBEF041C9DEE94E5F52C7649DA19BEBFDE9C93311A7A3FB36B`；Windows Core `83720ED1D4AD828AA5B8A6E4CB50A9CE503101B25EA143B138CFAAFA46AE2911`，Themes `D0BF6A3A8AA372A1E08605FB16DE2A76112B1006F4C4224516293F82223B41AC`；独立 Linux 构建 Core `80667916E70BA56ABDA625A0A569D86908ED76DAD056D9EDEDA9902C20BE7846`，Themes `84D8A6BB1343FB31474BF89D077CED210287AA47B51858E9B3E10A44D720D964`。

[本轮公开 API](charts-tail-api.txt) 的 Windows 实际捕获 SHA 为 `75E69DDB03A2058AAE062AF52E2D3294E58E86797D046110D1D8EC50A217FEE3`；Linux 也对自身实际 DLL 另行导出，完整文件 SHA 为 `AD8AC73D443801EFC6CBBF2F3D684303FF07FBE61508469D43ED08AA802070B0`。两平台均为 Core 69 / Themes 3 个导出类型；仅排除 RunId 和实际 assembly SHA 头行后，所有公开/受保护声明和值文本与旧 1F49 完全一致。官方 282 类型仅作 metadata 读取，未执行许可受阻的官方运行时。公共 API 与三项 C0 冻结文件另由最终文档完整性校验确认。

## 失败历史与剩余工作

| 候选 | 实际结果 |
|---|---|
| v1/v3 | 测试编译失败，未运行；保留原日志。 |
| v2/v4 | 403/432 项测试通过，但 pilot 分别有 9/6 项失败。 |
| v5/v6/v7 | v5 无独立通过 receipt；v6 451 项测试、pilot 7 fail；v7 451 项测试、pilot 1 fail。 |
| v8 | 451 项测试；正式 99/100，柱图中位数失败。 |
| v9 | 454 项测试；pilot 4 项柱图失败。 |
| v10 | 469 项测试通过，但 unrestricted geometry cache 的 Windows native pixel 对照 144/222 失败，候选拒绝；不得隐去。 |
| v11 | 469 项测试、Windows bar pixel 696 对通过；正式 97/100，十万点与 gap first p95 共 3 fail。 |
| v12 | 493 项测试；pilot 83 pass/1 fail/16 pending，百万点 first p95 仅改善 9.8307%，不满足 10%。没有运行正式池。 |
| v13 | 496 项测试；恢复 OSR 和有界租借后百万点目标通过，pilot 仍有 15 fail/16 pending。 |
| v14 | 产品构建零警告/错误，503/504；新增 Diamond 测试漏读变换子组，未进入性能池。 |
| v15 | 同一运行时修复该测试读取，504/504；pilot 仍 5 fail/16 pending。随后按预定更大独立池执行，正式 **100 pass/0 fail/0 pending**；未重复同池择优。 |

pilot 缺 fresh C1/048，整体 `pass` 与完整 scatter 结论为 null；正式池不撤销先前 pilot 的失败。NuGet 网络环境失败、编译失败、native 差异、暖身及全部原始样本均保留。详细经过与来源见[完整历史草稿](../artifacts/charts-performance/charts-tail-20261016/tail-report-draft-v5.md)、[v13 全样本复核](../artifacts/charts-performance/charts-tail-20261016/v13-pilot-review-v1/assessment-zh.md)、[Diamond 测试修复输入](../artifacts/charts-performance/charts-tail-20261016/test-review/v14-diamond-oracle-repair-v1/inputs.json)。早期草稿是其写入时状态，最终结论以本报告和最终 receipt 为准。

剩余性能工作优先处理独立百万点 hit p95，其次是柱图/gap/实时 PoC 的首个 warmup 成本，以及十万点 apply/prepare 的尾部与分配尖峰；保留固定对照、全部样本和独立诊断，不用其他阶段收益抵销回退。柱图缓存还需完整托管/原生字节预算与 backend 重绑定验收；Series 仍存活而仅切换 Series.IsVisible 或空源的所有组合，不保证立即释放缓存，不能扩大本轮图表隐藏/移除/detach 的清理结论。增量/LOD/SharedY 大数据首索引、长稳、多图及真实 native frame/present 继续开放。Linux 本轮使用 WSLg 的功能结果也不能替代物理 Ubuntu 22.04/24.04、原生 Wayland、多显示器、触屏和读屏认证。官方固定参考仍受 AVLIC0001 阻塞，不能宣称性能已与商业 Charts 对齐。

C2 的图例/标签模板、线段插值和完整 Trackball、系列/范围选择、触摸、动画、JPEG 与点级辅助功能继续按[开发计划](charts-development-plan.md)推进；本轮不因目标尾延迟门禁通过关闭 C2/C4 全阶段。
