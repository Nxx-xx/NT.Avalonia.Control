# PdfViewer performance harness data (PERF19)

本目录是 PdfViewer 性能基准的数据契约：场景矩阵、语料定义、记录 schema。实现位于
`benchmarks/PdfViewer.Benchmarks/`，统一入口 `scripts/Test-PdfViewerPerformance.ps1`。

**诚实性**：所有产出为自身性能结果。同机官方 A/B 对照需要合法参考宿主，当前不可得，
对照项一律 `blocked-reference`；present 仅测到 `composition-batch-rendered`
（CompositionBatch.Rendered 通知，`scanOutVerified=false`），不是扫描输出/物理呈现。
缺失测量一律为 `null` 并在 `notes` 给出原因，绝不以模拟值填充。

## 目录

| 路径 | 内容 |
|---|---|
| `corpus/manifest.json` | 语料定义（fixture 名、页数、搜索词）。运行时按它生成确定性 PDF 到 `<output>/corpus/`，并在生成清单里记录真实 sha256 |
| `scenarios/scenarios.json` | 场景矩阵（scenarioId、corpora、ui、primaryMetric、iterations）。runners 只读这一份，不双写 |
| `schema/perf-record.schema.json` | 原始记录 JSON Schema 1.0（单次 = 语料 × 场景 × 迭代） |

## 场景与指标

计时全部使用单调时钟（Stopwatch ticks → ms）。`first*` 里程碑相对 `loadStart` mark，
绝不相对 `LoadDocumentAsync` 返回。stage span 求和窗口见括号内说明。

| scenarioId | 指标 | 含义 |
|---|---|---|
| `open-first-screen` | `firstScreenMs` | 首屏：有 compositor present 时 = `firstPagePresentedMs`，否则 = `firstPageSubmittedMs`（basis 记录在 `notes.firstScreenBasis`） |
| | `firstPageDecodedMs` / `firstPageSubmittedMs` / `firstPagePresentedMs` | loadStart → 首页解码完成 / UI 提交 / compositor 渲染（仅 harness 记录 present） |
| | `inputReadMs` / `openParseMs` / `queueWaitMs` / `queueWaitMaxMs` / `nativeExecMs` / `bitmapUploadMs` / `uiSubmitMs` | 首屏阶段分解（各 stage span 求和至 firstPageSubmitted；DecodeComplete 有意不入和，它包含 queue+native） |
| | `decodeCompleteMs` | 首页那一次 display 渲染调用的整体时长（enqueue → 像素返回） |
| | `loadReturnMs` | loadStart → loadReturn（开文档返回，不是首屏） |
| `jump-page-cached` | `jumpMs` | 已缓存页跳转至 idle（warmup：目标页已渲染过一次） |
| `jump-page-uncached` | `jumpMs` | 未缓存页跳转至 idle（目标页从未渲染） |
| `scroll-continuous` | `frameIntervalMs`（samples） | 连续滚动中相邻 CompositionBatch.Rendered 通知间隔 |
| | `frameCommitToRenderedMs`（samples） | 每步 commit → rendered 延迟 |
| | `settleMs` | 停止滚动 → 高清 settle 完成（WaitForIdleAsync 含 upgrade pass） |
| `search-first-hit` | `searchFirstHitMs` / `searchTotalMs` | 搜索至首个命中页 / 全量扫描 |
| `search-full-document` | `searchTotalMs` / `searchMatchCount` | 1000 页全文扫描 |
| `cancel-queued` | `cancelObservedMs` | 排队中渲染被取消：cancel 请求 → 观察到 OCE |
| `cancel-native-render` | `cancelObservedMs` | 原生渲染中途取消；若先完成则 status=`completed-before-cancel`，指标为 null |
| `close-release` | `closeUiMs` | 关闭的 UI 可见延迟（产品 Release span，同步段） |
| | `closeReleaseTotalMs` | 关闭请求 → 异步原生释放完成 |
| `open-close-cycles` | `privateBytesFirst` / `privateBytesLast` / `privateBytesDeltaBytes` | 100 轮开关（前 20 轮 warmup 排除 JIT/惰性初始化，GC 后取样）后 private bytes 增量；门槛 ≤ max(20MiB, 5%) |

## 聚合规则

- 分位数 nearest-rank：rank = ceil(q·n)，取 rank 处观测值，不插值。
- p99 仅在合并样本池 ≥ 20 时报告，否则 null。
- 门槛判定来源 `docs/pdfviewer-performance-plan.md §6 工程初始门槛（待校准）`，informational。
- 每次迭代独立进程（`processCache=new-process`）；`--in-process` 仅供调试并在记录中如实标注。

## 运行参数（脚本与 harness 一致）

- `--rendering-mode default|software`（`-RenderingMode`）：`software` 在 Win32 / X11 强制 `RenderingMode=[Software]`，用于 G3 软件 / 硬件渲染拆分；请求的模式记入 `run-env.json` 与记录 `environment.graphicsBackend`，**Avalonia 公开 API 无法查询协商后的实际后端**，不得把 default 组写成「硬件渲染」。
- `--scenarios a,b,c`（`-Scenarios`，也接受空格分隔）与 `--filter`：子集运行；`compare-baseline.ps1` 只做自身历史基线比较，不做官方对照。
- 原生库 sha256 按**当前 RID** 匹配记录（`runtimes/<rid>/native/`）；曾按文件名全局首匹配会误指其它平台二进制，已在 P3-05 修复。

## 运行输出（`--output`，通常为 `artifacts/pdfviewer/<run-id>/performance/`）

| 文件 | 内容 |
|---|---|
| `run-env.json` | runId、commit、dirty、产品/原生库 sha256、命令行 |
| `corpus/` | 生成的语料 PDF + 含真实 sha256 的 manifest |
| `raw/<corpusId>__<scenarioId>__<iteration>.json` | 原始记录（schema 1.0） |
| `aggregates.json` | 分组聚合 + 门槛判定 |
| `report.md` | 人类可读报告（含诚实性声明） |
