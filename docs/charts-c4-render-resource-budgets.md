# Charts C4-06：绘图与本地资源预算

执行日期：2026-10-20。候选 `c4-render-resource-budgets-source-v1` 已通过 Windows 原生合成与 Ubuntu26.04 WSL2/WSLg 软件验证，新增资源预算、缓存统计/清理和显式分页压实，并保留原始身份和图形一致性。下一项 C4-07 多图与后台布局；完整 C4、实际内存与后端性能资格继续开放。

## 实现范围

- CartesianChart提供ResourceCacheBudgetBytes（默认128MiB，0禁本地保留）、GetResourceCacheInfo与TrimResourceCaches。系列绘图、轴文字、交互文字、轴画笔和线段几何共享LRU准入与淘汰；超预算项本次正常绘制但不保留，降低预算立即约束模型计费；卸载释放可重建本地资源。 全局计费预算采用LRU；轴文字256、交互文字32、画笔64的内部条目上限另以FIFO淘汰，不把所有内部策略统称纯LRU。
- Info的ModelVersion=1报告各层条目、固定权重估算之和以及累计命中/未命中/淘汰/旁路/清理；NativeMemoryIncluded和SharedIndexCachesIncluded为false。这不是托管堆、进程、原生或GPU实际字节硬上限，调用者业务对象、字体缓存、共享source/series/context缓存（含命中/LOD、PreparedStacks、预备路径与BarGeometry等CWT持有结果）和绘制临时工作不在五层账本。
- 轴Pen保留可变Brush/DashStyle引用；绘图重放读取当前画刷。原生两主题检查同一画刷变色，清理、降低预算、禁用缓存及显式Compact后原始数据、命中、选择和图形保持一致。
- 线性线段按原始数据查询；每系列屏幕投影memo最多4096端点，内部memo淘汰另报，不与公共LRU EvictionCount混同。固定3000独立查询重算原始端点，30个每100查询的Info里程碑报告观测最大；不称全部查询的全程内存峰值。
- ChartDataBuffer<T>.StoragePageCount统计当前buffer不同底层页；Compact()在UI线程显式按当前顺序复制全部保留行并迁移已有有效映射缓存，Compact本身不读取业务getter，保持Item、StableId、当前序号/坐标及旧快照。有效压实一次Reset/Version++，已紧凑或空源false不发通知；不计作删除或容量淘汰。 无live owner的系列后续读取仍沿既有轮询规则，不能据此承诺任意后续CreateSnapshot都不重映射。
- Compact有O(N)复制、临时双份数据和版本失效成本；不自动执行，旧快照仍可持有旧页，任意T对象图未获字节预算。普通追加仍保留C4-05原500新页payload/映射门槛，自动压实、共享索引和进程级资源策略另留后续。
- 固定Headless诊断使用20000原始线性点、无marker，cached与budget0各5次预热20个样本。缓存暖样本的数据/视图/绘图/文字/画笔重建和投影六计数均0；每帧当前线程分配最多2MiB，cached总分配至多disabled一半；40次长文字/窄窗churn检查五层估算预算。
- 20帧测量区间仅包含PlotArea访问、新DrawingGroup及Render，前后计数和Info/命中/oracle在外；线程分配不代表进程保留或native/GPU峰值，Headless DrawingGroup不含栅格化或compositor present。原始源构造不在暖区间，不证明首次可用。
- 独立interaction文字夹具调用private缓存/绘制helper和固定literal，oversized4096字符检查旁路；不将该helper等同实际routed Tooltip路线。完整Demo继承已有routed Tooltip检查，专门资源Native小图关闭Tooltip以维持固定图形。
- 2048单点页压实到8页的诊断完整保存新旧2048行、真实版本及前后metrics：buffer payload复制2048、映射迁移2048、新映射0、getter0。独立reader按原公式重算全部序号/ID/IEEE754坐标；运行时对象引用比较保留，JSON不能重建实际引用。
- 有限primitive-leaf指纹只比较有序叶子、变换/clip/opacity/paint/glyph与Geometry.ToString/Bounds；未独立解码opaque pathcommands，不等同完整矢量或原生画质证明。资源Native另由预登记14图协议、12图PNG独立解码/每图3业务ROI和每主题4组全画布0差检查。
- 四个短时MemorySamples按app setup、drawing、compaction、interaction后观测，未强制GC。managedLiveEstimate为估计，GC堆/碎片/提交值对应最新一次GC；累计分配与回收次数跨整个进程，OSpeak是进程生命周期而非夹具精确峰值，缺失OS计数和Native/GPU为null。没有将private减managed当原生，也没有为短时快照设保留/峰值/长期内存门槛。

## Windows 与 Linux 验证

| 项目 | Windows 原生合成 | Ubuntu26.04 WSL2/WSLg |
| --- | --- | --- |
| Charts 测试 | 1893/1893 | 1893/1893 |
| 完整原生 Demo | 1384/1384 | 1384/1384 |
| 编译绑定 Usage | 380/380 | 380/380 |
| 仅功能包消费者 | 538/538 | 538/538 |
| 功能与 Themes 消费者 | 538/538 | 538/538 |
| Release 警告/错误 | 0/0 | 0/0 |
| 基础/消费者/API/缓存/裁剪/LOD/命中/增量/资源阶段退出码 | 0/0/0/0/0/0/0/0/0 | 0/0/0/0/0/0/0/0/0 |

两个独立冻结副本包含1501个同SHA源文件；冻结前后780个声明运行/benchmark输入稳定，基础验证每平台实际765个输入一致。两平台独立bin/obj和新项目缓存，两类消费者另用全新NuGet缓存和本轮产品包，不含源码项目引用。14个只读非产品消费者依赖与63个项目归档按SHA复用，audit和警告策略未改。65份历史文档/API和13版renderer保留副本核对通过。资格绑定冻结副本，不宣称并行PdfViewer活动工作区等于冻结整仓。

Windows首轮v1/source-v1的1893项测试通过，在原长验证路径下原生Demo加载libHarfBuzzSharp时报OS 0x800700CE路径过长，未产生smokeJSON或图表断言。原失败完整保留；v2仍使用source-v1，将1501文件逐SHA复制到<REPO_ROOT>/artifacts/c406/windows-source-v1，配合全新短缓存重跑，snapshot与原wrapper未改。实际源码根由authoritative launcher绑定，短路径三方SHA证明独立归档。短源和短缓存同时改变，具体失败的加载器探测路径未定位；这是验证环境绕行，不称加载器已修复。v2原生加载成功后，旧动画viewport等待中间帧时出现Idle/progress1/threshold0.2异常，382项已执行布尔检查全true但整段未完成；failed base的稳定门禁尚未评估，结果字段保留false。外部事后1501源SHA与16个捕获载荷SHA相等，原载荷已另存副本。最终Windows v3/source-v1使用全新缓存重跑；通过也不视为旧动画时序稳健性修复。

Linux实际为Ubuntu26.04 WSL2/WSLg，复用已校验SDK10.0.402、私有PowerShell7.6.5/RG14.1.1/ICU72.1.0.3、X11库和字体；未改系统包。实际renderer/backend、GPU、物理桌面、原生Wayland和present未取得资格。

完整本地API类型168→169，Charts命名空间166→167；新增公开类型1，既有类型新增声明6，无旧声明删除。这是本地声明兼容核对，不认证官方API或二进制等价。C4-01～C4-05机制与旧像素门槛保持原值重跑。

## 固定诊断结果与边界

| 20k点诊断 | Windows | Linux WSLg |
| --- | --- | --- |
| 缓存每帧线程分配最大 bytes | 112528 | 112528 |
| 禁用每帧线程分配最大 bytes | 1198720 | 1198264 |
| 缓存/禁用20帧分配比 | 0.09441 | 0.09435 |
| 缓存绘制中位数/p95/最大 ms | 0.170 / 0.239 / 0.251 | 0.298 / 0.409 / 0.414 |
| 禁用绘制中位数/p95/最大 ms | 1.241 / 1.513 / 1.563 | 1.316 / 1.918 / 1.996 |
| 30里程碑观测memo最大/实际memo淘汰 | 4096 / 4909 | 4096 / 4909 |

缓存每帧≤2MiB、20帧总分配≤禁用一半、六项暖重建/投影计数为0，以及40轮预算churn门槛均未修改。固定3000个查询独立重算原始相邻端点，30里程碑仅为每100查询一次的观测；每系列memo4096端点限制和内部淘汰分别验证，公共LRU计数不混同。压实2048单点页→8页，buffer payload复制2048、现有映射迁移2048、新映射0、getter0；完整新旧2048行逐IEEE754/ID/当前序号核对，实际对象引用由运行时直接验证。

四个短时进程快照记录未强制GC的managed估计、最新GC堆与碎片/已提交、累计分配和回收次数，以及支持的OS私有/工作集/进程生命周期峰值；不支持为null，Native/GPU均null。没有将private减managed当原生内存，没有取短观测代替保留或本夹具精确峰值，也没有新增或放宽内存/时间门槛。完整数值见验证JSON。

毫秒和线程分配仅描述固定Headless DrawingGroup区间，不包括栅格化、GPU/compositor/present；源构造、缓存预热、Info与oracle不在暖区间。primitive-leaf指纹比较未独立解码opaque几何命令。资源原生夹具每平台两主题14图，独立解码12张chart-only PNG、每图3个业务ROI及每主题4组整图0差，另2张overview仅SHA；alpha≥250、RGB±24、至少10像素的预登记阈值未改。每平台另人工查看3张代表图，只作布局检查。

## 开发与工具记录

首轮1891项测试1889通过、2个新host夹具失败：重挂时Chart仍属于Border；Brushes记录器只收直接IBrush，MarkerSize0时画笔Brush封装在IPen内，因此只记录Transparent不能判定业务绘制为空。夹具先解除Border父级，并增加MarkerSize4、Fill使用同一画刷，保留Equal与Contains断言，另在Render外访问PlotArea完成准备。成本模型审查另修Pareto与ScatterLine估算并新增2项回归，最终1893项全过。Demo开发v1/v2资源64项和Usage380项均过，第二轮14图与第一轮逐SHA相同。benchmark-v1三夹具通过；benchmark preregister-v4（产品冻结source-v1）仅增加四个观测内存时点，三个原检查字典与固定gates保持一致，benchmark-v2和独立reader通过。

外部reader曾漏原有axisPens精确key，40项schema检查失败，其余数值通过；保留原报告，新脚本按六项精确schema修正后通过。无效review-id在参数解析阶段终止（wrapper退出1，argparse2仅惯例推断，无检查），注册工具regex双转义断言中止及开发summary属性比较错误均保留失败/修正收据。另继承LOD读图工具默认找Windows run-v1，在登记检查后因缺smokeJSON结束，未解码像素；新脚本从authoritative alias绑定实际Win run-v3/Linux run-v1与source-v1，两平台重新独立读图，原像素函数AST与阈值不变。它们未触发产品重跑或门槛调整，旧工具与原始证据仍可追溯。

## Demo 与截图

运行 `dotnet run --project samples/NT.AvaloniaControl.Demo -- --page charts`，展开 **绘图与资源预算：本地缓存、清理、可变画刷与分页压实**。可切换128KiB/4KiB/禁用缓存、清理重绘、修改同一画刷和显式压实，查看五层估算/条目、累计统计、页数及原始选中项。Usage提供编译绑定预算/状态和同类按钮。

- Windows：[截图目录](<<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-render-resource-budgets-20261020/windows/windows-final-v3>)，共725张，本批资源14张。 [画板](<<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-render-resource-budgets-20261020/windows/windows-final-v3/charts-light-resource-light-overview.png>) [4KiB预算](<<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-render-resource-budgets-20261020/windows/windows-final-v3/charts-light-resource-light-small-budget.png>) [暗色可变画刷](<<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-render-resource-budgets-20261020/windows/windows-final-v3/charts-light-resource-dark-mutable-brush.png>)。

- Linux WSLg：[截图目录](<<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-render-resource-budgets-20261020/linux/run-v1/verification/wslg-final-v1>)，共725张，本批资源14张。 [画板](<<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-render-resource-budgets-20261020/linux/run-v1/verification/wslg-final-v1/charts-light-resource-light-overview.png>) [4KiB预算](<<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-render-resource-budgets-20261020/linux/run-v1/verification/wslg-final-v1/charts-light-resource-light-small-budget.png>) [暗色可变画刷](<<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-render-resource-budgets-20261020/linux/run-v1/verification/wslg-final-v1/charts-light-resource-dark-mutable-brush.png>)。

## 剩余问题

- 下一项C4-07多图与后台布局；公平多图调度、隐藏/卸载的端到端工作预算、可取消复杂布局与旧结果版本隔离仍需后续执行，完整C2/C3/C4/C7及官方许可运行时/API/二进制对照继续开放。
- Model1只约束五层逻辑估算；真实托管/进程私有/原生/GPU保留和峰值、字体/共享索引/调用者对象图、在途临时分配和30分钟/8小时长稳未取得资格。
- 显式Compact解决可主动恢复的页碎片，不承诺自动分页压实或固定总字节；AppendRange仍暂存整批后才裁剪，旧快照保留旧页；目录复制与部分摘要/slice工作仍O(P)，CopiedItemCount非完整数组/对象复制统计。
- C4-05增量路径、日期/对数/断轴/自定义域和类别/排序/堆叠/点编码/柱布局、新版本二维/共享读数/线段/LOD索引与动画几何的已记录N路径仍开放；本轮未认证所有图族更新O(delta)。
- 固定20k无marker绘图分配门槛及原C4-01..05机制不是正式CPU/FPS/尾延迟或端到端显示SLA；百万点hit p95历史0.7649→1.1235ms按用户决定后置。
- Windows原生合成、Ubuntu26.04 WSL2/WSLg和Headless不代表物理Windows/Linux、原生Wayland、国产Linux、实际GPU/backend、多屏DPI或触控板/触摸资格；无GPU/软件后端性能对等结论，未引入替换Skia路径。
- A11Y-NATIVE-01原生UIA/AT-SPI选中项枚举按用户延期；AX06标题/末刻度与四份历史PixelProbe DLL缺口保留。C4-03 Linux旧导出动画三项部分暂停前置时序失败仍保留，同源码预热重试通过不视为修复。
- Windows在原长验证路径下加载libHarfBuzzSharp报OS0x800700CE；同字节短源和全新短缓存共同规避，具体失败探测路径未定位，不称加载器已修复，也未更改系统长路径策略。超长安装/运行路径仍待平台打包专项处理。
- C4-06 Windows run-v2旧动画viewport WaitProgress错过中间帧（Idle/progress1/threshold0.2），382个已执行布尔检查全true但整段提前exception。原failed base的最终source/assembly稳定flags未评估而为false，外部事后源/载荷SHA核对另列；后续同源码通过不视为时序稳健性已修复。

详细SHA、短时内存原值和失败收据见[验证清单](charts-c4-render-resource-budgets-validation.json)，公开声明见[API](charts-c4-render-resource-budgets-api.txt)。
