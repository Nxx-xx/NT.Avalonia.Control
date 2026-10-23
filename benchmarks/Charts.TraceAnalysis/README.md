# Charts EventPipe 分配与线程栈诊断

这是独立的诊断工具，不加入正常解决方案，不引用或构建图表项目。它解析 `dotnet-trace` 产生的真实 `.nettrace`，用固定 NuGet 依赖 `Microsoft.Diagnostics.Tracing.TraceEvent [3.2.6]` 转换为 ETLX，再归纳分配类型、分配调用栈与托管线程样本。依赖锁定在 `packages.lock.json`。

## 已采集的 before

2026-10-15 使用 `dotnet-trace 10.0.750501+ad724c4b85e7ae775e42f393c6e95a7557a57954`，联合 `gc-verbose,dotnet-sampled-thread-time`，512 MB Drop buffer，采集冻结宿主：

`artifacts/charts-performance/C1-axes-20261015-before/frozen-host`

结果位于 [before 诊断目录](../../artifacts/charts-traces/C1-axes-S1M-before-20261015T084437Z/)，其 [capture.json](../../artifacts/charts-traces/C1-axes-S1M-before-20261015T084437Z/capture.json) 保存完整可执行文件与参数数组、工具版本及 SHA、前后宿主全文件 manifest、基线对应关系、环境和退出码；[analysis.json](../../artifacts/charts-traces/C1-axes-S1M-before-20261015T084437Z/analysis.json) 与 [analysis.md](../../artifacts/charts-traces/C1-axes-S1M-before-20261015T084437Z/analysis.md) 保存分析。

宿主使用同一固定数据集 `S1M-L`，2 次计量样本、1 次 warmup、每样本 5 次命中查询；raw 因保留 warmup 共记录 3 个样本。`raw.json` 的 `producerPassed=true`。这次诊断覆盖整个子进程，包括启动、数据集生成、warmup、样本和退出，不是某一个内部阶段的独立测量。原始无 trace benchmark 结果没有被覆盖。

| 证据 | 结果 |
| --- | --- |
| trace SHA256 | `4FB1F4214B91A3DBF53FA4FB0CB91F7CB3DF9CD0EC5BE07FE101B8BE9BAB004F` |
| trace 大小 / 会话时间 | 4,663,142 bytes / 约 3.204 秒 |
| harness DLL SHA256 | `2EC2B6DAD7E8217F463B7FCB80F692B0B722BC1E8408C6F2094EA994EB576EBC` |
| charts DLL SHA256 | `FA690CDA35E8C612B912E4C362C8F6D20121B3EEE42E9D975CDA08CCAFDCB50A` |
| themes DLL SHA256 | `1544C206F5CA9F2B7B2F4CDC91626CD657BCCC5690CCE791E2C95A6B29E77B23` |
| 冻结匹配 | 上述三项与原 baseline `raw.sources` 匹配；采集前后全部宿主文件 SHA 不变。 |
| 收到事件 / 已报告丢失事件 | 32,240 / 0；转换丢失或截断回调 0 次。 |
| GCAllocationTick | 3,255 个，均为 v4；分配区间报告量合计约 1.170 GB。 |
| 线程样本 | Managed 1,076，External 356；分配/托管线程事件缺失栈均为 0。 |

样本归因表明优先调查：`ChartDataPoint[]`（Large）约 576 MB / 6 ticks；`Avalonia.Point[]`（Large）约 100.3 MB；`ChartPoint[]`（Large）约 96.6 MB；装箱 `ChartPoint` 约 95.9 MB；`Entry` 约 94.8 MB；`System.Object` 约 73.2 MB；`System.Double` 约 72.2 MB。这些是 tick 代表的近似分配区间按采样类型归因，不能当作各类型精确分配总量或对象数量。

`GetScene` / `CreateSnapshot` 的栈内累计归因约 672 MB；托管线程栈中 `HitTestPoint` 出现于 450 个样本、内置 `HitTest` 421 个、`GetScene` 199 个。它们是 inclusive 栈计数，同一事件会出现在多个方法行中，不能相加。完整叶到根栈保存在 JSON；保留两个未解析地址，并在转换日志与分析元数据中登记。由此可提出快照复制/装箱、几何数组和全量命中扫描的优化假设，优化收益仍需单独无 trace benchmark 和 after 诊断验证。

环境限制：沙箱下 `dotnet --info` 的工作负载探测尝试访问 Windows Service Control Manager 并返回拒绝访问。原采集成功且退出码为 0；`dotnet-info-supplement.txt` 保留后补相同命令的 stdout/stderr 与退出码 1，`dotnet-runtimes.txt` / `dotnet-sdks.txt` 另行记录运行时与 SDK。该缺项没有被写成环境信息完整。首次依赖还原受沙箱代理套接字限制，随后获准从公开 NuGet 还原成功。

## 重复采集与 after

采集脚本拒绝复用已存在的输出目录。它不会 restore/build 或写入宿主目录；每次会检查宿主文件哈希前后不变。指定 `-BaselineReport` 后还会强制匹配其 `sources.harness/charts/themes.sha256`，不匹配即停止。

```powershell
# Before 的可重复命令；选择新的输出目录，保留已有结果。
& benchmarks/Charts.TraceAnalysis/Capture.ps1 `
  -HostDirectory artifacts/charts-performance/C1-axes-20261015-before/frozen-host `
  -OutputDirectory artifacts/charts-traces/<new-before-run-id> `
  -Phase allocation-diagnostic-before `
  -BaselineReport artifacts/charts-performance/C1-axes-20261015-before/raw.json

# After 模板：等待主线程提供明确的 axis-complete/优化后 frozen-host 及其报告路径。
# 不使用会被后续 build 覆盖的 bin/Release 路径，也不复用 before 输出。
& benchmarks/Charts.TraceAnalysis/Capture.ps1 `
  -HostDirectory <root-provided-after-frozen-host> `
  -OutputDirectory artifacts/charts-traces/<new-after-run-id> `
  -Phase allocation-diagnostic-after `
  -BaselineReport <matching-after-raw.json>
```

参数默认值为 `-Samples 2 -Warmups 1 -HitQueries 5`，固定场景为 S1M-L。内部已按本机 `collect --help` 核对的命令为：

```text
dotnet-trace collect --profile gc-verbose,dotnet-sampled-thread-time --buffersize 512
  --show-child-io --output <trace.nettrace> -- dotnet <frozen-host>/Charts.Benchmarks.dll
  --quick --scenarios S1M-L --samples 2 --warmups 1 --hit-queries 5
  --phase <phase> --run-id <run-id> --output <raw.json>
```

准备分析器只需独立项目；已经还原后可以使用 `--no-restore`。不要把主项目构建插入采集脚本，也不要将诊断与无 trace 计时并行运行。

```powershell
dotnet restore benchmarks/Charts.TraceAnalysis/Charts.TraceAnalysis.csproj --locked-mode
dotnet build benchmarks/Charts.TraceAnalysis/Charts.TraceAnalysis.csproj -c Release --no-restore -m:1
dotnet benchmarks/Charts.TraceAnalysis/bin/Release/net10.0/Charts.TraceAnalysis.dll `
  --input artifacts/charts-traces/<run-id>/trace.nettrace `
  --output artifacts/charts-traces/<run-id>/analysis.json --top 50
```

分析输出包括 `.json`、`.md`、派生 `.etlx` 和 `.conversion.txt`。JSON 保存输入 SHA、分析器与 TraceEvent 程序集 SHA/版本、源码/锁文件 SHA、命令、丢失回调、分配事件版本和位宽、未解析地址、截断栈及热点。重新分析会重写派生结果；已封存的证据目录应选择新分析文件名。`evidence-manifest.json` 是本次 before 证据封存时的文件哈希清单。

## 解释边界

- `GCAllocationTick` 是采样。事件次数不是对象总数，`AllocationAmount64` / `AllocationAmount` 是近似分配区间；将区间字节归到这一次 tick 的类型和栈会有统计误差。
- `ObjectSize` 只描述被采样对象；其合计不等于总分配、保留内存或 GC 后存活内存。Native、GPU 分配也不在该字段中。
- 线程样本保留 Managed / External 分类。它们可揭示托管热点，但不能称为内核 CPU 采样、精确 CPU 时间、GPU 时间或实际窗口呈现延迟。
- `TraceLog.SampleProfileInterval` 为转换器元数据，尚未验证为该 EventPipe 会话实际采样周期；工具不以样本数乘这个值推算 CPU 耗时。本机 CLI 的 profile 描述为近似采样频率，也不能当作每个线程的严格计时器。
- Inclusive 方法行互相重叠；分配和线程计数均不能跨行相加。完整栈按事件分组，缺失和未解析栈会显式保留。
- 零已报告丢失不等于记录了每次对象分配或每条 CPU 指令。转换失败会返回非零，丢失/截断则在分析状态和回调中记录。
- 不下载远程 native symbols；托管方法名依赖 trace 的 rundown。冻结宿主没有内部阶段 EventSource marker，因此不宣称 tick 能精确划分至某次 scene/hit 阶段。
- 诊断 run 的 wall time 和 `raw.json` timings 不能进入无 trace 的性能提升百分比。

API 与采样解释依据：[TraceEvent 3.2.6 NuGet](https://www.nuget.org/packages/Microsoft.Diagnostics.Tracing.TraceEvent/3.2.6)、[Microsoft TraceEvent programmer guide](https://github.com/microsoft/perfview/blob/v3.2.6/documentation/TraceEvent/TraceEventProgrammersGuide.md)、[TraceLog / loss callback 实现](https://github.com/microsoft/perfview/blob/v3.2.6/src/TraceEvent/TraceLog.cs)、[CLR allocation event parser](https://github.com/microsoft/perfview/blob/v3.2.6/src/TraceEvent/Parsers/ClrTraceEventParser.cs)，核验日期 2026-10-15。实际 CLI 选项和版本以每次采集保存的本机 help 为准。
