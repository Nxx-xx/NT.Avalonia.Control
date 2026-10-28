# TreeDataGrid A6 Windows 原生性能与耐久补测

后续 2026-10-24 已完成调用栈定位、首轮 Source 分配优化、513 项回归及三十分钟/901轮耐久，见[首轮优化记录](treedatagrid-native-optimization-a6.md)。本文保留 2026-10-23 原始功能 DLL 的六场景/十分钟基线，不将两次 DLL 或时长的数据混为一次测量。

日期：2026-10-23（香港时间）。本轮使用单独的 Windows 原生测量宿主，补充先前 Headless 验证之外的渲染线程完成与有限时长耐久证据。功能/主题 API 与产品行为保持不变。真实屏幕呈现 FPS、GPU 执行时间和物理输入延迟未由此测量。

## 方法与复跑

入口：[Test-TreeDataGridNativePerformance.ps1](../scripts/Test-TreeDataGridNativePerformance.ps1)。协议和边界见[宿主 README](../benchmarks/TreeDataGrid.NativeBenchmarks/README.md)。在仓库根目录使用新的输出目录运行：

```powershell
./scripts/Test-TreeDataGridNativePerformance.ps1 `
  -OutputDirectory artifacts/treedatagrid-native/new-run
```

支持 DependencyFeed（本地依赖 nupkg）和 DurationSeconds（正式耐久不少于 600 秒）。Pilot 是缩短的诊断模式，不能作为正式验收。驱动执行锁定还原、Release 构建、隔离目录发布和 17 项记录校验测试，再依次启动两个独立进程，避免性能场景的大数据和峰值污染耐久进程。

窗口为 1000×650 DIP、RenderScaling=1，正常显示在显示器工作区，禁止最小化/调宽且不抢激活。每次更新检查窗口状态和视口；如果缩放、窗口状态或渲染回调失效，该次失败，不恢复或重试已失效样本。外层驱动限制进程总时长，保留 stdout/stderr、退出码、超时和输入 DLL 哈希。

每个请求记录唯一 id、场景/轮次/步骤、源版本、变更版本、同步布局结束和原生 Control.Render 快照，再通过当前 Avalonia 12.0.3 公开的 CompositionBatch.Processed / Rendered 取得渲染线程完成时间。快照检查行/列/模型身份、实际显示文本、A2 与工厂寿命，以及视口容器上界。请求日志在计时前落盘；applied/completed 日志在渲染计时后落盘。原始 JSONL 与终态 JSON 由独立 Python 校验器核对，不能用自报 passed 隐藏缺失/重复/过期回调、缺预热或不足的耐久时长。

P50/P95/P99 是“UI 调用前至该协议观察的原生批次渲染完成”，包含见证控件、额外合成提交和异步派发开销。空操作观察器本身的 P50 为 33.1503 ms；不能据这些数字反推显示 FPS，也不能简单扣除空操作得到控件净耗时。同步 UI 部分单独列出。PNG 是计时之外对原生窗口视觉树的 RenderTargetBitmap，仅用于布局复核，不是桌面截图。

## 正式性能结果

目录：`artifacts/treedatagrid-alignment/a6-native-20261023/final-1/`。性能进程于香港时间 **22:58:58–23:01:25** 完成，2700 个请求全部匹配（900 次预热 + 1800 次正式观测）。每场景 5 个新实例，每实例 30 次预热、60 次正式更新，使用真实产品主题和两个冻结列。

| 场景 | 同步 UI P50 / P95 | 批次渲染完成 P50 / P95 / P99 | 峰值保留行 / 实现单元格 |
| --- | ---: | ---: | ---: |
| 空操作观察器 | 0.0033 / 0.0140 ms | 33.1503 / 34.6118 / 36.6546 ms | 24 / 240 |
| 十万行 × 百列，固定行高 | 22.9755 / 30.6850 ms | 50.0000 / 64.8208 / 67.8188 ms | 26 / 286 |
| 一万行 × 百列，自动行高/受控绑定 | 21.0881 / 39.8483 ms | 49.4770 / 66.8879 / 83.6678 ms | 16 / 154 |
| 同配置加 A2 事件和订阅工厂 | 22.8879 / 47.3181 ms | 49.7517 / 82.2816 / 83.9402 ms | 16 / 154 |
| 十万子节点宽树 × 二十列 | 23.5742 / 30.8521 ms | 50.0567 / 66.4657 / 68.6336 ms | 26 / 286 |
| 一万行 × 二十列，程序导航/取消/提交 | 49.9582 / 61.2144 ms | 82.6397 / 84.6835 / 99.1870 ms | 26 / 260 |

同步 UI 长尾已经量化，不能据功能检查通过声明达到 60 FPS。导航场景每次包含选择、开始编辑、取消零写回、再次编辑、一次提交和定位，不能把其组合耗时当成单个键盘事件的延迟。工厂场景包含应用每个单元格的额外订阅和寿命核对，不作为框架净开销比较。本轮没有相同原生协议的旧版对照，因此不宣称性能提升或回退百分比。

原始[性能报告](../artifacts/treedatagrid-alignment/a6-native-20261023/final-1/performance/report.json)、[JSONL](../artifacts/treedatagrid-alignment/a6-native-20261023/final-1/performance/events.jsonl)、[独立校验结果](../artifacts/treedatagrid-alignment/a6-native-20261023/final-1/performance/validated.json)和[进程收据](../artifacts/treedatagrid-alignment/a6-native-20261023/final-1/performance-process.json)可复核。目录中保存六个场景的图像。

## 十分钟原生耐久

正式耐久进程于香港时间 **23:01:26–23:11:49** 完成，其中持续测量 **600.5048645 秒**、**301 轮**，另有十轮预热。2488 次请求全部匹配（预热 80 次 + 计时区间 2408 次），无崩溃、超时、身份/文本错误、草稿提前写回、重复提交或寿命配对失败。两个测量进程和整体驱动均退出 0，正式结果 pilot=false。

该进程先做十轮预热并在基线前完整 GC；计时区间内测量代码不主动完整 GC。每轮都保留草稿跨工厂/模板/主题变化，取消零写回、提交一次、原子移动两行、保留选择/当前身份、显隐/重排、替换活动 Source，再完成五次双轴滚动。每轮八次更新都须匹配原生渲染完成。按两秒或实际工作时长较大者节流，每约十五秒记录托管堆、私有内存、工作集、句柄、CPU、累计分配和 GC 计数。

弱跟踪覆盖每轮两个 Source、全部两千模型、两个工厂、上下文、Grid、模板 presenter、行、单元格、列头，按弱对象身份去重。已收集的弱引用从列表移除，只保留累计计数及尚存弱引用，避免跟踪器本身无限持有图。最终等待操作栈退出、空原生渲染、12 秒合成传输/释放池等待和完整 GC，再核对零保留。该最终 GC 不计入十分钟持续测量。

事先固定调查阈值：跳过最初两个检查点后，首/末各三个检查点的私有内存中位数增量不超过 256 MiB，句柄中位数增量不超过 128。超出即失败并调查。这是本轮泄漏调查护栏，不是业务内存预算、数小时耐久认证或“永不泄漏”的结论。

本轮 39 个资源检查点结果如下，未触发调查阈值：

| 指标 | 结果 |
| --- | ---: |
| 首/末稳态三点私有内存中位数 | 167.63 / 157.63 MiB，增量 -10.00 MiB |
| 计时区间私有内存范围 | 147.73–172.34 MiB |
| 计时区间工作集范围 | 197.98–224.60 MiB |
| 首/末稳态三点句柄中位数 | 703 / 687，增量 -16 |
| 计时区间句柄范围 | 681–703 |
| 第一/最后资源检查点间累计分配增量 | 37634840880 B，约 35.05 GiB |
| 对应 GC.CollectionCount(0/1/2) 增量 | 4477 / 2992 / 931（代际计数不相加） |

累计分配与 GC 负担需要单列关注：本负载每两秒重复创建/替换千行四十列的绑定和事件观察、两套工厂与模板及测量对象，包含驱动观察开销。分配增量不是存活内存，也不能以最终零保留掩盖该负担；本轮建立可对照基线，没有进行优化或冻结业务分配预算。后续优化应使用相同原生协议和工作负载重复比较。

最终跟踪 **1529048 个**退休对象全部已回收：context755286、模型602000、Source602、Grid301、工厂602、presenter602、行14814、单元格148795、列头6046。每轮旧模型退订、两个工厂释放、A2 Prepared/Clearing 配对及一次 EditCommit 均通过。JSONL 记录的两次宿主显式完整 GC 分别位于测量前基线和测量后排空阶段，独立校验器核对计时区间没有宿主显式完整 GC。

[耐久报告与资源序列](../artifacts/treedatagrid-alignment/a6-native-20261023/final-1/soak/report.json)、[原始事件](../artifacts/treedatagrid-alignment/a6-native-20261023/final-1/soak/events.jsonl)、[校验结果](../artifacts/treedatagrid-alignment/a6-native-20261023/final-1/soak/validated.json)、[进程收据](../artifacts/treedatagrid-alignment/a6-native-20261023/final-1/soak-process.json)和[整体结果](../artifacts/treedatagrid-alignment/a6-native-20261023/final-1/results.json)均保留。试跑单列在 pilot/driver-pilot 目录，不充作正式样本。

## 输入一致性与验收边界

整体运行于香港时间 **22:58:51–23:11:50**，96 份范围输入运行前后 SHA256 一致，两个隔离进程使用相同宿主/功能/主题 DLL，构建 0 警告/0 错误。见[输入清单](../artifacts/treedatagrid-alignment/a6-native-20261023/final-1/source-manifest.json)、[最终清单](../artifacts/treedatagrid-alignment/a6-native-20261023/final-1/source-manifest-final.json)、[隔离发布程序集清单](../artifacts/treedatagrid-alignment/a6-native-20261023/final-1/application-manifest.json)和[环境](../artifacts/treedatagrid-alignment/a6-native-20261023/final-1/environment.json)。

产品功能 DLL SHA256 与此前 A5/A6 相同：`7A5CDD48C718C40A3E7123E23E8FDF62E479A50B684FBF46FBC6B15D2D9C24C3`。原生宿主 DLL：`828B11B19862BADA9DEB2E3AF3210723BEB02407D112720053900FBAD73AE217`。本轮只增加测量宿主、驱动与记录校验，沿用已有 Avalonia 依赖版本，没有改动控件行为或公开 API。已有 509 项控件回归对应同一个功能 DLL，本轮新增执行 17 项原生记录校验测试，不重复计入控件数量，见[测试日志](../artifacts/treedatagrid-alignment/a6-native-20261023/final-1/guard-tests.log)。

报告保留已加载的 Skia、HarfBuzz、GLES、DXGI、D3D 模块和版本；模块存在不证明使用了特定 GPU。系统环境与输入哈希由驱动单独保存。操作系统仍为当前 Windows x64，不能外推到其它 Windows 配置或麒麟。

本轮覆盖的是 Windows 本机原生自动测量。[补充矩阵](../artifacts/treedatagrid-alignment/a6-native-20261023/final-1/platform-matrix.json)给 windows-native-performance 添加 NativeMeasured 部分证据，保留原验收条件；[Windows](../artifacts/treedatagrid-alignment/a6-native-20261023/final-1/acceptance-windows.json)和[完整平台](../artifacts/treedatagrid-alignment/a6-native-20261023/final-1/acceptance-windows-kylin.json)仍有 8/9 类待验，记录无结构错误，未将本机测量替代物理项目。

真实 IME、物理拖动/剪贴板/焦点、读屏、多 DPI/多显示器、干净系统部署、实际 CI、屏幕呈现/物理输入延迟、数小时负载与业务预算及麒麟仍保持待验。A3.1 任意不透明原生 CompiledBinding 的自动安全写回也继续开放。整体 A6 状态见 [A6 记录](treedatagrid-alignment-a6-status.md)。
