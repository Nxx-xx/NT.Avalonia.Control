# C7-03 散点加速候选与原协议复测

更新日期：2026-10-26。状态：`same-protocol-retest-complete-workloads-failed-no-new-product-optimization`；C7-03 仍未完成。

本批保持现有 Render 调用方式、原始点数、顺序、画质、协议及门槛。并行原型按用户决定暂不接入，不增加新的 owned-render 公共入口，也不替换 benchmark 入口。产品绘制实现仍为 B8；本批复测不能作为新产品优化的因果证据。

证据目录：`C:/Users/a4472/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c7-scatter-followup-20261026`。

[精简机器记录](charts-c7-scatter-followup-validation.json)包含逐进程统计和完整审计的路径、大小及 SHA；完整审计留在证据目录的 `validation-v1.json`，未把 13 MB 证据重复嵌入仓库。上一批定位见[原生热点诊断](charts-c7-native-hotspots.md)，已接受产品实现的历史结果见[B8 报告](charts-c7-backlog-optimization.md)。

## 本批候选及取舍

| 方向 | 本批证据 | 处理 |
| --- | --- | --- |
| Conic 预展开 | 双平台 native-only 短测像素一致，但没有稳定提速 | 不接入 |
| 两个独立 worker 的分块栅格 | 双平台自有 bitmap 各 24/24 例真正激活且像素一致；没有性能计时资格 | 保留实验，暂不接入 |
| 稳定版 Skia 119 patch | 实际包的关键热点比较未发现性能改善依据 | 不为性能升级依赖 |
| 不透明 Src 画笔 | 当前原生路径已归一化到相同内环 | 不增加重复规范化 |
| 自建 DrawPath C thunk | 当前封装已直接 P/Invoke，无逐次 delegate / 符号查找层 | 不实施 |
| recorder 画笔预快照 | 实际记录链没有逐点 ToImmutable，提前快照会改变旧记录的动态画刷语义 | 不实施 |
| 同一 plan 的延迟 SKPicture | 仅有摊销窗口和设计审查，仍需资源预算及退休机制 | 后续窄试验候选，未实施 |

Conic 短测每进程计划 2 秒，不是原协议固定负载。路径、paint 与 fixture 在计时外准备；draw 包含 managed loop、SkiaSharp 包装与原生绘制。不能把这些数值登记为完整请求延迟、纯 Skia CPU 时间或正式门禁。

| 平台 | 原路径 P50 / P95 ms | 预展开 P50 / P95 ms |
| --- | --- | --- |
| Windows | 18.797 / 19.2731 | 20.2783 / 24.589 |
| Linux | 26.721571 / 28.206113 | 26.822211 / 28.845011 |

每端核对 10,000 个实际生产中心的坐标 bit、20,000 条路径结构。实际转换 9,875 条 fill path、39,500 个 conic，观察到的 POW2 全为 0；125 条靠近 clip 的路径回退，10,000 条 outline 已无 conic。额外准备约 67–72 ms，没有显示可接受的绘制收益。

## 并行原型的资格与边界

最终 v3 的 Windows / Linux 各 24/24 例真正激活，全部 changedBytes、changedPixels、maxDelta 为 0。两端各两组 Src-copy 回环也通过，包括 rowBytes=2112 的带 padding 情况。实际合法容量为 540,608 bytes，不能用完整 stride×height 的 540,672 bytes 代替。

所有成功 case 的主目标均由原型明确拥有，SaveCount=1；worker 拥有各自的 path、paint、bitmap 和 canvas。实际 Avalonia 普通产品上下文的 SaveCount 为 6，OpacityMask 情况为 7 且 CurrentOpacity 仍可为 1。当前公开 API 返回的 root Surface 不能证明它就是活动绘制层，因此自有目标的资格不能推广到任意 Render 上下文。

v1 进程真实以 0xC0000374 退出且缺少终态；静态确认 padding 复制多访问 64 bytes，与 heap corruption 一致，但没有 dump 证明唯一崩溃位置。v2 修复容量后没有 native fatal，但只有 20/24 例激活，整体失败；v3 支持合法的零命令 worker，没有降低激活门槛。旧失败、源码、日志和图片全部保留。

另一个活动层识别实验在 F16 颜色回环后改变了 130,890 bytes，已拒绝。没有把这一路径用于产品探测。Windows 首次构建的 NU1900 审计源失败也保留，后续网络恢复后在新目录成功构建，未关闭 NuGet 审计。

B12/B13 独立文件审计共 639 项核查通过，记录 639 份证据 SHA；384 个不同 PNG 构成 240 对编码字节一致的比较。它们属于各原型的局部保真资格，不替代 232 幅产品回归。B13 duration=0，没有吞吐或加速倍率结论。

## 原协议复测

本批在 Windows 11 x64 与 Ubuntu 26.04 / WSL2 / WSLg 各运行原协议 107 个独立进程：5 个交互 producer、2 个首次可用校准、100 个正式首次可用 producer。交互每 track 10 秒预热、30 秒测量、60 Hz、最多额外排空 15 秒；没有修改入口、数据、AA、画笔、DPI、字体设定、软件后端或门槛。两端 locked restore / build 完成，原 oracle 33 项测试各通过。

双平台独立 oracle 均核验 107/107、failures=[]、证据完整性和命中门禁通过；执行器与整体性能门禁仍失败。每端冻结源码 2,135 文件，两个副本及 formal 的 318 项前后输入均核验不变。四个产品/宿主 pin 与五个协议/oracle/测试/预算/策略 pin 均未变。

下表统计每项 5 次测量，共 9,000 个计划槽。“30 秒终点未完成”由已记录 end ticks 推导；“排空后剩余”是原协议 pending，含未尝试的槽。门槛仍沿用原 oracle，没有把新增诊断边界当作新门槛。

| 平台 / track | 完成 / 计划 | 30 秒终点未完成 | 排空后剩余 | 测量轮通过 |
| --- | ---: | ---: | ---: | ---: |
| Windows / S10-L-exact | 9000 / 9000 | 0 | 0 | 5 / 5 |
| Windows / S10-S-exact | 8122 / 9000 | 3071 | 878 | 4 / 5 |
| Windows / S10-B-exact | 8777 / 9000 | 950 | 223 | 4 / 5 |
| Windows / S100-L-minmax | 9000 / 9000 | 217 | 0 | 5 / 5 |
| Linux / S10-L-exact | 9000 / 9000 | 0 | 0 | 5 / 5 |
| Linux / S10-S-exact | 7580 / 9000 | 3840 | 1420 | 0 / 5 |
| Linux / S10-B-exact | 9000 / 9000 | 739 | 0 | 5 / 5 |
| Linux / S100-L-minmax | 9000 / 9000 | 0 | 0 | 5 / 5 |

Windows 第 5 轮散点和柱形同时超过有界排空；Linux 散点 5 轮均失败。所有失败、未尝试槽和 exit1 producer 保留。B8 历史散点 pending 为 Windows 235 / Linux 1028，柱形均为 0；本批为 878 / 1420，Windows 柱形为 223。产品未变化，不能将差异归因于新优化或删除异常轮改记成功。完成负载也不能宣称真实 60 FPS / compositor present。

每个进程单独计算 nearest-rank P95，下面不合并样本，也不把阶段 P95 相加。失败轮只描述已完成样本，其余槽仍计入上表。单位 ms。

| 平台 / 散点轮次 | 已完成样本 | record P95 | raster P95 | request-to-raster P95 |
| --- | ---: | ---: | ---: | ---: |
| Windows / 1 | 1800 | 27.362300 | 21.489700 | 47.603800 |
| Windows / 2 | 1800 | 27.195300 | 21.383300 | 47.885300 |
| Windows / 3 | 1800 | 26.778500 | 21.592400 | 47.908700 |
| Windows / 4 | 1800 | 27.611000 | 21.217400 | 48.167200 |
| Windows / 5 | 922 | 46.616800 | 48.864800 | 97.218300 |
| Linux / 1 | 1434 | 25.972154 | 33.583930 | 58.536130 |
| Linux / 2 | 1364 | 28.246250 | 30.790383 | 57.975907 |
| Linux / 3 | 1589 | 27.284767 | 29.975254 | 56.087254 |
| Linux / 4 | 1591 | 26.752284 | 29.493175 | 55.537612 |
| Linux / 5 | 1602 | 25.397812 | 29.550449 | 54.521750 |

100 个正式独立百万点首次可用样本的受控软件诊断 P95：Windows **1357.368 ms**、Linux **1303.189201 ms**；2 个校准均排除。两端仍满足 3000 ms 名义软件诊断目标。B8 对应值为 1189.5808 / 1335.665949 ms，变化没有新产品优化的因果资格。正式 render/present 和匹配的峰值私有内存仍未测得。

两端各比较全部 142 张正式活动首末图（含校准），共 284 对：Windows 142 对、Linux 140 对编码字节一致。279 对首图/相同记录状态的图像全部一致；5 个末态不同的比较单独保留，Linux 其中 2 对确有像素差异，未当作相同状态视觉回归或删掉图片。B12/B13 局部探针与这些正式活动图片分别统计。

归档每端 SkiaSharp.dll、Avalonia.Skia.dll 及对应 native asset，并核验 managed identity。原生资产 SHA 与 B11 相同；这是活动结束后的 **asset-only** 归档，正式宿主没有逐进程原生模块映射，不能将文件归档冒称实际加载映射。

本批没有产品绘制改动，未重跑产品 3256 项全量、232 幅专项、Demo/Usage 或独立包消费者。它们的 B8/B10 历史资格保持原有候选与范围，不搬用为本批通过。

## Windows 第 5 轮波动

独立诊断保留全部 107 个进程。散点第 4/5 轮共同的前 922 个 measured index，其 action、revision、输入/可见/绘制计数和 range 全部匹配：record P95 27.6782 → 46.6168 ms，raster P95 21.4029 → 48.8648 ms；record/raster 分配 P95 分别仍为 10,393,120 / 87,408 bytes。这支持继续检查整条管线的延迟波动，不能仅凭分配量相同断定 GC 没有影响。

仅有进程 CPU/wall 与内存端点；没有 GC 次数/暂停、温度、频率、功耗、调度或外部进程遥测，因此原因保留为 `unresolved-from-recorded-data`。不能推断为热降频、外部负载或某次 GC。原始诊断见证据目录 `variance-v1.json`，上述完整证据汇总与 Windows 波动诊断均在两端计时结束后运行。

## 剩余问题

1. 双平台 exact 散点仍未通过，Windows 本批柱形也有失败轮；优先处理原生栅格与公开 DrawingGroup 逐点记录成本及整条请求波动，不能用短测、排空后完成或局部像素一致登记积压修复。
2. 并行方案暂不接入。后续若继续研究自动加速，必须在保持现有调用方式的条件下取得可靠活动目标与图层资格，不能依赖私有字段、SaveCount 猜测或破坏性探测。
3. 同一 plan 的延迟 SKPicture 仅是后续候选。跨源替换不天然复用，不能重新引入已拒绝的完整向量 Memo；须预留预算并验证失效、驱逐、旧帧和在途租用的释放行为后，才值得做严格保真与端到端配对。

   B8 旧 picture 对照使用 DrawEllipse，不能当成当前 fast DrawPath 的收益证据；Windows / Linux 冷记录分别约 2.85 / 4.62 ms，单份 ApproximateBytesUsed 为 2,080,144 bytes。下一步先做限定同一 plan 的小试验验证净收益，再决定是否投入缓存生命周期工程。
4. 官方仍无可用许可，状态 blocked-reference，测量与倍率为 null。正式 frame/present 和峰值内存资格仍未取得。
5. Ubuntu 22.04/24.04 实机、硬件 GPU、物理触控及已后置的百万点命中尾延迟、原生选中项桥接继续待专项验证。


## 实验证据索引

- B12-B13：[记录](C:/Users/a4472/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c7-current-evidence-20261026/audit-v1.json) — Frozen external diagnostic evidence; no product optimization accepted.
- B14：[记录](C:/Users/a4472/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c7-skia119-patch-20261026/native-inspection/findings.md) — Static findings, no product integration.
- B15：[记录](C:/Users/a4472/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c7-opaque-blend-20261026/native-inspection/findings.md) — Static findings, no product integration.
- B18：[记录](C:/Users/a4472/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c7-picture-plan-review-20261026/design-review.md) — Design/historical evidence review only, no product integration.
- recorder-paint：[记录](C:/Users/a4472/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c7-recorder-paint-20261026/native-inspection/findings.md) — Rejected paint snapshot direction, no product integration.
