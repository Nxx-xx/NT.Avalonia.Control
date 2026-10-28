# C7-03 性能测量与官方对照

后续优化与同协议复测见[2026-10-25 优化验证报告](charts-c7-performance-optimization.md)。本文及原验证 JSON 保留为前后对照基线。

执行日期：2026-10-24。用户明确将实机、硬件 GPU 和物理触控留到后续，并确认暂无官方 Charts 许可；本轮继续自研测量，官方对照保持 `blocked-reference`，参考值和倍率均为 null。缺失项目不转换为通过。

## 本轮范围与计时

当前 Windows x64 / Win32 与 Ubuntu 26.04 WSL2/WSLg / X11 使用同源候选、显式软件渲染、1200×650 DIP、96 DPI 的受控栅格；通过真实 native Skia lease 核验没有 GRContext。该结论不是目标 Ubuntu 22.04/24.04 实机、硬件 GPU、原生 Wayland 或 compositor 呈现认证。

新入口为 `scripts/Invoke-ChartsC7Performance.ps1`，正式模式显式 `-Phase formal`，内部正常锁定还原和重新构建 `-p:EnableC7Performance=true`。默认关闭该构建开关时仍编译原 C4 驱动；两种构建的程序集分开记录。被测程序集、runtimeconfig、依赖文件和输入源码清单另行归档。

协议为 `scripts/charts-c7-performance-protocol.json`，原 `charts-engineering-targets-v1` 和 C4 CI 门槛没有改动。正式协议 SHA-256 为 `7E633452EA83F4A9342C68093FE597DFB0CF4B18491D21660DE70EBE73EC5463`；原生候选 v2 共 2,060 文件，manifest SHA-256 为 `88669F223D20FCFAD57D113EAA0EC098B3243422693C423D07B8D8DA80370317`。

| 项目 | 口径 |
| --- | --- |
| 四条交互轨道 | S10-L/S10-S/S10-B exact，S100-L MinMax；每平台五个独立进程，每轨十秒预热、三十秒计划轨迹 |
| 固定请求 | 60 次/秒，即 600 次暖场和 1,800 次正式请求；保留计划时间、实际时间、迟到、已完成/失败/pending，最多排空十五秒 |
| 独立命中 | 每轨每进程 100 次预热、1,000 次正式查询，至少 10% 在源维护之后；原始系列/索引/ID/坐标及柱形几何独立验证 |
| 百万点首次使用 | S1M-L MinMax，每平台 100 个新进程；另两次校准明确排除。源已物化，从新图表绑定源前至完整受控栅格与原始点查询结束 |
| 分层时间 | sourceApply、prepare、DrawingGroup record、RenderTargetBitmap.Render、hit 单独保存原始 ticks；PNG 编码、文件写入、像素复制和哈希在 raster 区间之外 |
| 首次使用外部成本 | 数据生成、对象适配和边界准备另记；进程 UTC 首次完成时间只是带仪器初始化/取证 IO 的 wallclock 观测，不替代 Stopwatch 或普通应用首屏 |
| 分位数 | nearest-rank `ceil(p×N)`，各进程和 pooled 各算 p50/p95/p99；不平均多个 p95，不用其他进程掩盖失败，p99 为观察值 |
| 内存/CPU | 进程和 GC 端点、线程分配与 CPU 计数注明窗口；没有真实峰值 private/native/GPU 预算资格，不以 working-set lifetime peak 冒充 private 峰值 |

`controlledRaster` 是同步受控 CPU 栅格，`record` 是绘图记录；二者都不是 composition/framePipeline/present。3000 ms 的既有软件首次 render 目标只给名义对照，不能因为受控栅格较快就宣布正式 render 门槛通过。present、framePipeline 与官方倍率维持独立的未测/阻塞状态。

原始数据使用冻结的 C0 整数 XorShift 生成器及逐系列 IEEE754 小端二进制/hash。Native 适配保留原始整数 ID，替换源在原 Y 与 Y+0.25 间切换；exact 与 MinMax 分轨。公开 `ChartSamplingInfo` 的 rendered/candidate count 不等于 Skia draw-call 数，DrawingGroup primitive count 还包括轴和网格。只有相同最终 revision/viewport 的图片才要求精确复原，不用跨 OS 字体差异做逐像素比较。

## Windows 正式结果

107 个预定进程的证据全部通过独立完整性核验：五个交互进程、两次排除校准、一百次正式首次使用。四轨 100+1,000 查询及原始数据/几何守卫通过，8 ms 命中 P95 工程门槛逐进程通过；固定请求负载没有全部通过。

| Windows 轨道 | 完整进程 | 已完成正式请求 / 计划 | pending | 受控 raster P95 ms | 接受请求→raster P95 ms |
| --- | --- | --- | --- | --- | --- |
| S10-L exact | 5/5 | 9,000 / 9,000 | 0 | 7.9055 | 28.3159 |
| S10-S exact | 0/5 | 4,873 / 9,000 | 4,127 | 44.4997 | 81.7298 |
| S10-B exact | 0/5 | 7,453 / 9,000 | 1,547 | 4.6364 | 43.8446 |
| S100-L MinMax | 0/5 | 8,110 / 9,000 | 890 | 7.9321 | 147.6492 |

后三轨仅是已完成的部分样本分位数，不代表完整三十秒负载通过。所有暖场请求均完成；失败来自正式三十秒轨迹达到十五秒排空上限，实际已尝试请求没有错误命中，剩余请求明确记 pending。即使完整请求在排空窗口内完成，也不等价于持续 60 FPS 或没有迟到。

百万点 MinMax 一百个新进程：首次受控栅格 P50 `2346.3684 ms`、P95 `2437.3361 ms`、P99 `2491.5970 ms`、max `2666.6471 ms`。三秒名义诊断目标满足，正式首次 render/present 门槛仍未测；两次校准未入统计。

Windows 明确热点：S100 sourceApply P95 `81.5955 ms`、prepare P95 `52.3788 ms`；柱形 record P95 `28.5459 ms`，与受控 raster P95 `4.6364 ms` 是不同区间。它们应作为后续 profiler 输入，不能把多个 P95 相加或把绘图记录开销隐藏到 raster 数字中。其他进程的快样本没有抵消这些失败。

## Linux 正式结果

同一协议在 Ubuntu 26.04 WSLg 完成 107 个预定进程，独立证据完整性与四轨 8 ms 命中 P95 门槛通过，固定负载门禁仍失败。

| Linux 轨道 | 完整进程 | 已完成正式请求 / 计划 | pending | 受控 raster P95 ms | 接受请求→raster P95 ms |
| --- | --- | --- | --- | --- | --- |
| S10-L exact | 5/5 | 9,000 / 9,000 | 0 | 8.7476 | 22.9001 |
| S10-S exact | 0/5 | 5,435 / 9,000 | 3,565 | 50.0371 | 85.8161 |
| S10-B exact | 5/5 | 9,000 / 9,000 | 0 | 8.4540 | 45.4260 |
| S100-L MinMax | 0/5 | 8,823 / 9,000 | 177 | 12.1117 | 162.7869 |

散点、S100 的分位数仅描述已完成的部分样本。柱形虽在排空期限内完成全部请求，实际每进程正式阶段约 `39.378～40.285 s`，使用了约九至十秒排空，不能写成三十秒准时完成或稳定 60 FPS。所有暖场完成；排空失败保留 pending 和最后在途同步操作的实际结束时间。

百万点 MinMax 一百个新进程：首次可用受控诊断 P50 `2991.4932 ms`、P95 `3150.8334 ms`、P99 `3219.1394 ms`、max `3226.2886 ms`。Linux 的三秒名义诊断线未满足；正式首次 render/present 仍未测，没有以更宽松门槛或删去慢样本改成通过。

Linux 热点包括 S100 sourceApply P95 `100.4555 ms`、prepare P95 `47.2243 ms`，以及散点 record P95 `32.6362 ms` / raster P95 `50.0371 ms`。这些是当前软件与仪器协议下的定位线索，需要 profiler 拆分数据适配、复制、索引、GC、记录和绘制，不能预断全部来自同一产品缺陷，也不以跨 OS 数值直接评硬件优劣。

## 验证汇总与结论

两平台各 `33/33` 校验器测试通过；Windows 早期开发版的 31 项与后续补强版测试分开保留，正式使用 33 项版本。Native 开发轮与两次首次使用校准排除，正式共 214 个进程。独立校验器逐点核对原始二进制、版本/几何、图像、计时和进程身份；两端均 `evidenceIntegrityPassed=true`、`absoluteHitGatesPassed=true`、`workloadSchedulesPassed=false`。

本轮完成的是当前条件下的自研测量和真实失败记录，未声明性能全部达标或与官方性能对齐。后续优先剖析固定源替换的分配/准备、散点绘制、柱形记录和排队，再使用相同数据、画质、请求数量及排空限制重测。不要只看 raster 数字而忽略 scheduled→raster 的秒级积压；也不要将几个区间的 P95 相加。

汇总见[独立验证 JSON](charts-c7-performance-comparison-validation.json)，788,435 bytes，SHA-256 `D05C0F4D22EC12E9DE315602A5141E58D8A582F5737137617943676690135E2A`。其中自研证据完成为 true、全部负载通过为 false、官方倍率及正式 frame/present/峰值内存资格为 null、`milestoneComplete=false`。总计划和平台矩阵同步这些边界，历史 C7-02、C4 及失败结果未被覆盖。

## 官方参考

固定 `Avalonia.Controls.Charts 12.3.0` 与 Avalonia `12.0.3`；两平台正常还原后均由官方许可检查报 `AVLIC0001`，没有成功编译/运行，不产生计时或倍率。可执行项目已提供显式 opt-in 的环境变量到 `AvaloniaUILicenseKey` 接入，未保存密钥、改依赖或绕过许可。

用户暂无许可，因此本轮不采购或替换参考库。官方明确需要有效许可及可执行项目中的许可项，环境变量/共享 props 可以安全接入。[官方安装与许可说明](https://docs.avaloniaui.net/tools/installing-avalonia-pro)

现有 Reference 仍是 C0 数据/初始化骨架；有许可之后还需要成功编译、同样本循环、实际点数/质量、相同后端和 JIT 的验证。恢复许可本身不等于已具备正式公平对照。

## 夹具修正与历史保留

- Legacy v1 冻结漏带 `docs/charts-performance-budgets.json`，宿主未产生测量样本；原始退出码与异常保留，后续新建候选补齐输入。
- Legacy v2 的 S-RT-POC 在 revision 1 查询 `series 3/index 259` 时 Y 为 `42.16015625`，超过旧固定轴上限 `41.828125`。独立整数公式与原 hash 确认这是夹具视口错误；v3 在计时前单独预扫描有限计划 revision，固定 Y 范围为 `[-1.4296875,191.15234375]`，原 X 范围不变。预扫描时间、分配、数据 hash 和轴策略写入 `AxisPreparation`，不缓存未来数组，也没有放宽原索引命中判据。
- 初版短原生驱动发现 Task.Delay 可能早醒，产生少量负 queue ticks；后续候选使用 monotonic 尾段等待，正式请求不得早于计划时间。开发轮次与补充 posthoc 验证单独留存，不混入正式采样。
- Linux 正式包装器首轮因开发/正式单元测试输出目录重名而在采样前被拒绝；修正外部目录命名后以新监督目录执行，旧拒绝覆盖记录仍保留。这不是 native 性能样本，也没有覆盖开发数据。

Legacy Headless 在两平台各五个新进程运行八个原场景，每进程 15 个正式样本、三个计数预热、每样本 1,000 查询，原始守卫均通过。它保留原 C0 prototype schema、默认 JIT 与 render-record 边界；不与 native 禁用 tiered-JIT 的轨道混池，也不声称三十秒实时输入、10,000 点/秒、长期内存或 present 资格。

## 证据与复跑

本地根目录为 `C:/Users/a4472/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c7-performance-comparison-20261024/`。官方阻塞在 `<OS>/reference-v1/`，修正后的 Headless 原始结果在 `<OS>/legacy-v3/`，正式原生结果在 `<OS>/formal-v2/`；其中 `runs/<id>/png/` 为取证图、`datasets/` 为原始 XY，父目录含 raw、requests、进程日志、源码及被测程序集归档和 oracle。开发轮次、失败输入与正式结果分开，计时不依赖 CI 上传，本轮没有执行远端 CI。

```powershell
pwsh -NoProfile -File scripts/Invoke-ChartsC7Performance.ps1 `
  -Phase formal -OutputDirectory <全新绝对输出目录>
```

Linux 使用同一 PowerShell 入口及实际字体/原生依赖；可用 `-FontDirectory` 指定测试字体。每个子进程使用新私有缓存，系统缓存没有清空。短调试使用默认 development，不能替代正式采样。

## 剩余范围

官方运行与公平倍率保留 `blocked-reference`；framePipeline、present、真实峰值 private/native/GPU、指定目标硬件及完整质量黄金的资格分别保留。本轮四条 native 轨道和八个 legacy 场景不覆盖全部专业图族的原生性能，C5/C6 分族数量模型与专门门槛仍须按 PERF11 独立验收。当前排队失败和 Linux 首次可用名义诊断超线均保持开放，不能因证据完整而标成性能通过。

百万点命中尾延迟优化与原生 UIA/AT-SPI 选中项桥接继续按用户决定后置。C7-04 的三十分钟/八小时长稳、生命周期压力和后续发布/API 冻结不由本轮短轨迹自动结项。
