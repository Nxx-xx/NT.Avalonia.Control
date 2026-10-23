# Charts 性能回退修复与 C2 续作

历史报告：本文正文对应 CDB6 冻结版本的 338/100/35 与两类消费者各 37 项结果。最新散点修复、Tooltip 模板、SharedY 与轴读数续作见[新执行记录](charts-c2-templates-readings.md)。添加本导航前的[原始文档字节](../artifacts/charts-platform/charts-c2-templates-20261016/historical-document-bytes/charts-c2-followup.md)另行保留，原始性能/平台证据不变。

更新日期：2026-10-15。本记录接续 [C2 首轮](charts-c2-common.md)，旧报告只对应其冻结源码。本轮按“先性能定位和修复、通过开发门槛，再推进 C2，最后对最终程序集复测”执行；C2 整阶段仍未完成，正式 native 性能及官方参考对照也未结案。

## 性能定位与修复

用完全相同的冻结 benchmark 应用、数据、画质和查询 oracle，重新执行 C1 `6C30F456…694F33`、上一轮 C2 `048EED53…74CD0C` 与候选版的交错进程对照。保留默认 tiering/PGO；FULL8 与独立百万点为一轮 warmup、五轮计量，独立非单调保留三轮 warmup、十轮计量。窗口 1200×650 DIP、字体 12 DIP、ShowLegend=false，折线/柱 MarkerSize=0、散点为 4；没有降采样、减少顶点或放宽命中身份检查。

复现确认上一轮 C2 有真实的百万点绘制和小场景准备回退。相同投影/数据缓冲的源码及 portable PDB/IL 对照表明，不能把单调百万点或含缺口场景的问题归因于非单调 radix 索引：这些场景没有进入该排序路径。

本轮修复包括：

- 将折线逐点循环从 marker 配置及几何资源生命周期中提取出来。标准投影使用强类型 adapter，在保留一份数学算法的同时消除热点接口投影调用；自定义投影继续使用原接口路径，保留完整的耦合投影契约。
- 将缓存场景读取与场景重建分开。标准折线按数据规模选择入口：短循环局部优化编译，长循环保留默认 tiering/PGO 与 OSR；两者共用同一逐点算法，默认运行环境没有全局关闭 tiering 或 PGO。
- 未启用的图例、标签、标注、选择和读数使用小型入口，避免默认路径进入大型功能主体。圆标记的简单路径与其他形状的变换/清理分开。
- 每个控件最多缓存 256 份轴文字结果，区分测量与换行约束，键包含已解析字体、字号、画刷身份及 culture。只有纯系列数据 revision 保留缓存；轴、样式、资源、布局和视图变化仍清除，未挂载控件绕过缓存。256 是条目上限，不是任意长度标签的固定字节上限。系列 palette 查询不再为已有身份反复创建 factory delegate。
- 缓存这些文字的原始 glyph drawing，保留局部原点、变换及原画刷引用，每次重放读取当前 Color/Opacity/Transform/渐变。原点变化时重录；非纯 glyph、未挂载或容量外的文字仍调用原 DrawText。两级缓存同生命周期，不持有系列或业务数据对象。
- 标准投影且 marker 关闭的柱图提取独立循环、提前计算分组宽度与原点，保留原浮点运算顺序。柱命中复用已计算的矩形，保留裁剪柱体和完整 marker 锚点、严格距离与原始顺序的平局规则。
- 内置折线/散点命中按实际候选窗口数量选择短循环或长循环入口，8192 为分界，两入口共用同一算法；不减少候选，也不按场景名分流。柱与自定义投影保留原命中路径，距离算序、物理 ordinal 平局规则和原始 SourceIndex 不变。

EventPipe 与 native JIT 证据区分了短循环的 FullOpts 与长循环的 Tier1-OSR。长循环中的坐标变换、有效性检查已实际内联；不能用 wrapper 的编译属性代替循环主体的 native 证据。小场景测量阶段没有观测到 GC pause 重叠或 JIT start；文字测量采样落在字体匹配、ConcurrentDictionary.Values/AcquireAllLocks 等路径上，但没有据此量化“锁等待时间”。在 v9 与旧 C2 的相同 FULL8 诊断中仍出现重复文字排版，因此继续收窄纯数据更新时的缓存失效；诊断运行不计入性能成绩。

开发候选 `2A59220D…3FF39D` 已通过 264 项测试和两组三版本对照，才放行 C2 编辑。[开发门槛](../artifacts/charts-performance/C2-regression-fix-20261015/performance-fix-development-gate-v6.json)保留逐进程样本、首轮 warmup、四份严格比较与全部失败候选。独立百万点首次可用中位数为 C1 88.031 / 上轮 C2 87.5295 → 候选 57.2375 ms，绘制记录 66.7368 / 67.5616 → 32.9811 ms。八场景连续运行对上轮 C2 的百万点中位数仍有 +8.87% 的单轮历史差异，已由下述 CDB6 最终五组对照复核；它没有被独立场景的改善抵消或混池。

“首次可用”沿用冻结协议，指数据物化/应用、场景准备、记录绘图至第一次命中返回，窗口已经创建，结果是绘图命令记录，未包含 compositor present。开发候选 v6 的诊断中，场景重建和折线入口一次性编译约 8.706 + 2.581 ms，发生在初始窗口/布局期间，位于该计时范围外；这组历史数字不代表最终 CDB6 的冷启动测量。不能用这些数字宣称应用冷启动或屏幕首次呈现已达标。

## 最终性能原型验收（CDB6）

本轮保留 C1 `6C30F456`、此前 C2 `048EED53`、新版本 `CDB6C56A` 三侧独立对照；新 Themes 为 `204BE1FA`。固定基准程序 `34055360`，数据、全精度点、标记、原语及命中身份检查不变。45 个独立计时进程、6 组严格比较全部完成。下表均为 **p50 / p95，单位 ms**，包含全部测量样本；逐进程分布及原始预热轮另行保留。

FULL8 每侧五进程，每进程 **1 轮预热、5 轮测量、每轮5次查询**，每侧每场景25个测量样本。

| FULL8 首次可用至绘制记录 | C1 | 048 | 新版本 |
|---|---:|---:|---:|
| S10-L：万点折线 | 2.7079 / 5.8441 | 2.8711 / 5.8789 | 1.2753 / 4.1109 |
| S10-S：万点散点 | 21.3745 / 31.6601 | 21.8308 / 31.2051 | 21.9847 / 30.7961 |
| S10-B：万点分组柱图 | 25.9058 / 33.3152 | 26.7611 / 31.0805 | 22.6862 / 26.8168 |
| S100-L：十万点折线 | 11.2900 / 14.3286 | 14.2558 / 19.9602 | 6.0138 / 9.0801 |
| S1M-L：百万点折线 | 93.0279 / 118.7406 | 65.2603 / 136.3392 | 49.2628 / 60.3616 |
| S10-L-NONMONOTONIC：非单调 | 16.9793 / 20.3100 | 2.5292 / 3.7469 | 1.3497 / 1.5975 |
| S10-L-GAP：缺口折线 | 3.4198 / 4.9131 | 3.3983 / 4.3531 | 2.1813 / 2.3547 |
| S-RT-POC：四系列小图 | 1.7620 / 2.8906 | 1.7958 / 3.2105 | 0.5455 / 0.6721 |

独立百万点维持 **五进程/侧、1预热/5测量/5查询**；独立非单调维持此前 **五进程/侧、3预热/10测量/25查询**，每侧50个测量样本。它们与 FULL8 分池，不能混合后挑选较好结果。

| 独立场景首次可用 | C1 | 048 | 新版本 |
|---|---:|---:|---:|
| 百万点 | 60.6324 / 92.6431 | 93.5309 / 107.8242 | 55.5645 / 62.2938 |
| 非单调万点 | 9.9466 / 13.2411 | 2.8939 / 6.1731 | 1.5174 / 4.6977 |

| 新版本分阶段 | 准备 | 绘制记录 | 命中查询 |
|---|---:|---:|---:|
| FULL8 百万点 | 4.8698 / 5.7071 | 30.2519 / 43.0297 | 0.5812 / 0.7382 |
| FULL8 缺口 | 0.2172 / 0.3965 | 1.7095 / 1.8822 | 0.0155 / 0.0275 |
| FULL8 四系列小图 | 0.1463 / 0.1862 | 0.2658 / 0.3958 | 0.0126 / 0.0202 |
| 独立百万点 | 5.1170 / 5.8405 | 38.0196 / 43.2281 | 0.6547 / 0.8567 |
| 独立非单调 | 0.5547 / 0.6406 | 0.7852 / 3.1187 | 0.0164 / 0.0243 |

FULL8 百万点准备分配 p95 从048的71,272降至12,888B，记录33,694,512→33,674,856B，命中616→528B。文字缓存消除了重复排版/字形工作；小循环使用实际优化入口，大循环保留运行时剖析与 OSR。采样证明经过字体匹配及锁相关路径，未量化锁等待，也未将全部版本差异归于单一原因。

三侧各20轮强制GC检查均通过原16MiB门槛，所有过期源/快照及最终存活计数为0。C1和048的稳定段增长/清空残留均为29,240/26,432B，新版本为4,600/1,792B。范围限于此单进程、单调数值数据的托管回收；不代表原生/GPU、非单调临时空间或长时运行。缓存上限是256条，非字节上限，长类别文本另有持留风险。

真实冷编译成本没有消失：当前诊断中 BuildScene、MeasureAxis、AxisText、小折线入口的 JIT开始至装载墙间隔分别为10.3133、2.5766、3.0413、2.7122ms，均在初始窗口/布局阶段，处于现有样本计时域外；小命中入口3.659ms、大命中 OSR 3.8645ms落在预热轮。这不是完整进程冷启动指标。独立诊断报告事件丢失0；其时长不混入正式计时池，分配事件仅为抽样估算。

三项残余明确保留：① FULL8散点记录中位数 C1/048/新为19.9816/20.5377/21.4006ms，新版分别增加7.10%/4.20%；首次可用中位数增加2.85%/0.70%，但p95下降。② 独立百万点命中中位数相对C1由0.5976升至0.6547ms（+9.55%，+0.0571ms），p95由1.5036降至0.8567ms。③ FULL8柱图命中p95相对C1由0.1500升至0.1604ms（+0.0104ms）。不以“噪声”删除这些结果，列入C4监测。

独立门禁 `pass=true` 只验收本轮已定位的首次可用、轴文字及大小命中回退；不等于所有指标无回退。驱动执行成功与此门禁分开，正式性能资格仍为 `pending-C4`，`formalPerformancePassed=null`，呈现与官方许可参考测量仍阻塞。双平台与包消费者的产物关联验证另见下文；原型门禁不替代这些检查。

完整证据：

- [三侧各阶段、逐进程及原始预热轮汇总](../artifacts/charts-performance/C2-regression-fix-20261015/final-controlled-cdb6c56a-v1/summary.json)
- [45进程、比较与内存原始报告索引](../artifacts/charts-performance/C2-regression-fix-20261015/final-controlled-cdb6c56a-v1/driver-result.json)
- [独立原型门禁与残余](../artifacts/charts-performance/C2-regression-fix-20261015/final-controlled-cdb6c56a-v1/prototype-regression-gate.json)
- [673份证据文件清单及SHA](../artifacts/charts-performance/C2-regression-fix-20261015/final-controlled-cdb6c56a-v1/evidence-manifest.json)
- [程序集及40份源码/31份测试/8份基准输入封存](../artifacts/charts-performance/C2-regression-fix-20261015/final-c2-cdb6c56a/frozen-host.json)
- [当前版本原生代码、EventPipe及真实冷编译诊断索引](../artifacts/charts-performance/C2-regression-fix-20261015/final-controlled-cdb6c56a-v1/diagnostics.json)


## 新增 C2 契约

### 图例布局

`LegendHorizontalAlignment` 对齐上/下边图例，`LegendVerticalAlignment` 对齐左/右边图例。默认 Left/Top 保留原来的停靠位置；Stretch 使用起点。侧边图例继续竖排，空间不足仍会截断，没有新增滚动。

`IsLegendFloating=true` 使用最终 PlotArea 布局，不预留坐标轴或系列空间。`FloatingLegendMargin` 默认 8 DIP，有限非负并在小绘图区内收敛；`FloatingLegendBackground` 为 null 时使用图表 Background。浮动图例绘制在数据、标注、选择和读数之上、Tooltip 之下。

整个浮动面板，包括长短标题/换行留下的背景空白，都阻挡下层选择、悬停和滚轮缩放；切换显隐只命中实际图例条目，非交互图例也会阻挡底层输入。已经捕获的平移/框选可以跨越面板继续。隐藏全部系列后仍可通过图例恢复，颜色身份不变。

### 可取消选择

`SelectionChanging` 提供 OldSelection/NewSelection、Reason、CanCancel、Cancel。Pointer、Keyboard、Rectangle、Clear、ExternalValue 可以取消；Reconcile 与 Configuration 是强制清理，忽略 Cancel，不允许保留删除、隐藏、失效或超限的身份。

取消外部属性赋值会恢复旧规范列表并回写 TwoWay 源，不移除绑定；绑定传输可能先产生瞬态通知。回调内的新选择优先，嵌套变更不递归触发 Changing，外层用 revision 检测被替代的请求。回调修改数据后再次验证身份，异常会恢复规范状态再传播。取消选择仍执行实际点击的 PointInvokedCommand。

`ChartSelectionResult.IsCanceled` 表示框选被取消或被 SelectionChanging 回调中的新请求替代，SelectedCount 返回实际保留数量。没有语义变化时不触发 Changing/Changed；源替换为 Equals 相等的新业务对象，仍刷新公开 Hit.Item 的真实引用。

### 矩形、椭圆与箭头

`RectangleAnnotation`、`EllipseAnnotation`、`ArrowAnnotation` 使用 X1/Y1/X2/Y2，继承原有坐标、轴 Key、层级、资源、所有权和裁剪契约。Pixel 为相对绘图区的 DIP。椭圆取投影后两端点包围框，不把对数轴上的屏幕椭圆称作数据空间圆。

箭头 ArrowHeads 支持 None/Start/End/Both；HeadLength/HeadWidth 默认 10/6 DIP，允许 0..10,000，短线按长度缩小。DashStyle 只影响箭杆，头部边线实线，头部仅在原始端点生成。默认绘图区/断轴裁剪会裁掉越界头部，断口不会生成新箭头。非有限、非法对数、无法投影、退化或溢出的几何跳过。`ClipToPlotArea=false` 沿用图表外框裁剪，允许跨断轴绘制。

Demo 的共用能力区域增加浮动/对齐及选择拦截开关，并增加独立形状标注区域。Usage 和两类真实 nupkg 消费者共享 compiled AXAML 的形状、资源、浮动绑定及选择取消/绑定保持检查。

## 最终双平台与包验证

Windows 最终候选：338 项测试、100 项原生 Demo 检查、35 项 Usage 检查、两类独立包消费者各 37 项通过。三个专项项目 Release 构建均零警告/错误，测试失败、错误、跳过和未执行均为 0。[Windows base 原件](../artifacts/charts-platform/C2-followup-20261015-final-v3/results.json)明确使用 `-SkipPackageConsumers`，包验收来自随后单独执行的[原消费者脚本结果](../artifacts/charts-platform/C2-followup-20261015-final-v3/package-consumers/20261015T152321953Z-ad8247c6d9e64942b19527dd6e96777b/results.json)，没有把 skip 改写为 passed。

Linux 最终候选：338 项测试、100 项原生 Demo 检查、35 项 Usage 检查、两类独立包消费者各 37 项通过。[Ubuntu 26.04 / WSL2 / WSLg 最终结果](../artifacts/charts-linux/20261015T141841885Z-perf-candidate-dcdd71d35d7248f5a5e254a0ea6885e9/linux-final-summary.json)保留五个 Release 构建日志（主验证三个及消费者两个），均零警告/错误。还原没有 diagnostic warning，漏洞审计和警告策略没有放宽。

两平台均从新构建产物重新打包；功能包和主题包使用独立的全新消费者目录与包缓存，无 ProjectReference。仅第三方依赖采用锁定版本、内容哈希及原始包哈希均校验的本地 feed；Windows/Linux 的产品 feed 分开，避免同名产品包相互覆盖。

Windows 的 498 份受验证构建输入与 Linux 逐 SHA256 一致。Linux 复制的 1,143 份文件、498 份受验证输入、两平台运行程序集及只读工具/驱动在相应验证范围内保持稳定；[185 份 Linux 证据清单](../artifacts/charts-linux/20261015T141841885Z-perf-candidate-dcdd71d35d7248f5a5e254a0ea6885e9/evidence-index.json)全部复核通过。随后更新的一般 docs 不属于运行代码输入；文档最终关联检查单列在[验证汇总](charts-c2-followup-validation.json)，不会重写平台原件。

| 冻结对象 | SHA256 |
| --- | --- |
| Windows Core，与最终性能候选相同 | `CDB6C56A759233CFBF48FA8B41369FA89CB614F1908E831A803DC4CB5DF04B8D` |
| Windows Themes | `204BE1FA4B8EDFC0C7BBA486CBD689C10BAD7FF5C661BBDFB820F5E24D6BB6DC` |
| Linux Core | `E657BD20E648BD4174A4243F6FBE32A63D42203AC56BD3CF18E60FFB215E5B45` |
| Linux Themes | `F8B803D7DAEC299546EE199930FE2348E095B47494DA945EAE7B1BC876493E7E` |
| Windows base results.json | `99503B9094F14B896B30907859271709258EC85DD539C5DBA10F9D2CE03E9D8B` |
| Windows 独立消费者 results.json | `3426AC23B3316A7DA3CBD5F14F2A947C0C9F4E7AA6569FBEB371B57B1A4BC9F7` |
| Linux summary | `F2A3B0753E3C479ABD57B63215895D5514136EFFDD156198EA39B6937B158085` |
| [当前 API](charts-c2-followup-api.txt) | `83D963D0F721ECBBC10945F7668B76A3A87B26C0336EDC2109B8F6185D8F182F` |

当前 API 捕获只读取 metadata，没有构建、实例化官方控件或执行官方代码。C0 三份冻结文件、200 项矩阵 ID 顺序及 54 个来源 URL 保持不变。

两平台继续通过亮暗主题、六端点/三图例、96/144/192 DPI 的 PNG oracle，并新增浮动图例三符号、三种形状×两种断轴方向的六个组合、椭圆角落及双端箭头检查。Windows 的[六张目视记录](../artifacts/charts-platform/C2-followup-20261015-final-v3/visual-review.json)与 Linux 的九张目视记录均未发现新的布局/文字阻断。[亮色形状 Demo](../artifacts/charts-platform/C2-followup-20261015-final-v3/charts-light-c2-shapes.png)、[暗色形状 Demo](../artifacts/charts-platform/C2-followup-20261015-final-v3/charts-light-c2-shapes-dark.png)、[独立断轴像素图](../artifacts/charts-platform/C2-followup-20261015-final-v3/charts-light-shape-pixels.png)可直接查看。

这些结果属于真实窗口中的合成输入、程序化绑定与 RenderTargetBitmap；透明浮动 PNG 的黑色预览底不代表导出带黑背景。Linux 请求 X11/XWayland，实际应用 backend/renderer/GPU 仍未采集，不能据此关闭物理桌面、输入、OS DPI、多屏、辅助工具或 native present/FPS 验收。

## 功能冻结 B202 的历史验证

Windows 的[B202 功能结果](../artifacts/charts-platform/C2-followup-20261015-final-v2/results.json)通过：317 项测试（失败/跳过/未执行均为 0）、100 项原生窗口 Demo 合成检查、35 项 Usage 绑定检查，以及两类全新隔离缓存、无 ProjectReference 的 nupkg 消费者各 37 项。三个专项项目 Release 构建零警告/错误。497 份构建输入、8 份产物（六份 DLL 和两份配置 JSON）以及完整仓库 manifest 在该运行期间均保持一致。这份结果只对应 B202；后续 CDB6 已完成上方新的产物关联验证。

| 冻结对象 | SHA256 |
| --- | --- |
| Windows Core | `B2022B6448FA14FA0CF364E4E28CFF2D8BDABB77A7ABC8EBD8CA6A2552FABA49` |
| Windows Themes | `D9281C8906228A50F30D88663D0B73AFA596374E724AE529EED4BFB381747D5C` |
| Windows results.json | `1D44AC4F56F2207E11FFEDED53636672CD999A96DF307A25649CCC233F518105` |
| [B202 API 原件](../artifacts/charts-api/C2-followup-final-v1/api.txt) | `45AEE654F839E05164FC5BC45FE225C0BB6E053C2F6501AA4769E73BC39FBA56` |

新的像素检查保留原固定 PNG 的六端点/三图例与三档 DPI oracle；另加浮动图例的三符号、六张独立形状图的 X/Y 断轴两侧/缝内墨迹、椭圆角落及双端箭头侧翼检查。图形和文字在亮暗主题均经过实际窗口 bitmap 及导出检查，[目视记录](../artifacts/charts-platform/C2-followup-20261015-final-v2/visual-review.json)保留图像哈希。透明 PNG 在黑色预览底上显示暗色文字，不等于导出含不透明黑背景。

[形状 Demo](../artifacts/charts-platform/C2-followup-20261015-final-v2/charts-light-c2-shapes.png)、[暗色 Demo](../artifacts/charts-platform/C2-followup-20261015-final-v2/charts-light-c2-shapes-dark.png)、[独立断轴像素图](../artifacts/charts-platform/C2-followup-20261015-final-v2/charts-light-shape-pixels.png)。这些属于合成输入/程序化 API 与 RenderTargetBitmap 证据，没有真实物理输入或 compositor present 结论。

相同 B202 源码的 [Ubuntu 26.04 / WSL2 / WSLg 结果](../artifacts/charts-linux/20261015T134835967Z-c2-shapes-843b5358dff14044abad044dcdfd4d9e/linux-final-summary.json)也通过 317/100/35 项及两类消费者各 37 项。497 份受验证输入与 Windows 完全一致，1,142 份复制源码及运行程序集保持稳定。Linux Core 为 `20BD3DAAE057CA8577AE9F1644406A64F6469ED8D6D084E10F57958712C6A093`，summary SHA256 为 `7D4102EE511152020668A10C4D1E5D1D59B50462459462B6666B7BD648BC7387`。它使用进程私有 SDK、库和字体，通过 WSLg 创建真实窗口；请求 X11/XWayland，实际应用 backend/renderer/GPU 尚未采集。

## 保留的失败与剩余问题

首次 316 项回归有六项失败：四项图例 oracle 没有固定 Padding，错误假定主题的 16 DIP 为 8 DIP；两项选择取消在 TwoWay 回写时重建了等值列表。图例测试显式设置 8 DIP，选择恢复原 canonical 列表，并另加 Equals 相等业务对象替换时仍更新原始 Item 引用的回归。原失败与后续结果均保留，不覆盖旧文件。

首次完整 Windows 运行因受限网络无法读取 NuGet 漏洞索引，统一 Demo 的依赖还原报 NU1900；该运行没有标为功能失败或通过。取得允许的网络执行权限后重跑最终 v2，锁定还原及全部检查通过，未关闭漏洞审计或放宽警告。此前性能候选只看 p95 曾过早记录 development-pass，补看 p50 后明确撤销并保留 superseded 记录，最终仅 v6 开发门槛放行功能工作。

C2 功能完成后的 B202 五组完整对照再次暴露回退：[原始结果与逐进程分布](../artifacts/charts-performance/C2-regression-fix-20261015/final-controlled-b2022b64-v1/results.md)。FULL8 百万点首次可用中位数为上轮 C2 64.3212 → B202 80.4642 ms，柱图为 27.5279 → 30.3524 ms；独立百万点却改善为 88.9244 → 66.3173 ms。两种运行历史没有混池，没有只选择较好的独立结果。driver 的 passed 仅指执行、正确性和哈希 guard 完整通过，性能状态记录为 blocked-regression，因此继续修复后再验收。

之后的 056F 候选也保留了完整 45 进程及内存结果；它改善首次可用，但独立百万点命中中位数仍从约 0.60 增至 1.18 ms，门禁明确为 false 并标记 superseded-with-hit-regression。最终 CDB6 针对真实短/长命中循环分别修复后，才取得上文的窄范围原型通过；没有覆写失败原件。

本轮依赖 feed 加速脚本初版错误地把签名 NuGet 包的 content hash 当成原始 zip SHA512，严格 guard 因而失败。原脚本、空 feed 与失败记录保留；v2 使用锁定 SDK 的 NuGet `PackageArchiveReader.GetContentHash` 对 lock、原始 SHA512 对 cache `.sha512`、复制 SHA256 三类分别校验，13 个第三方包全部通过。未认定为包损坏，未改变仓库还原/漏洞审计策略，未复制历史 NT 产品包。

本轮仍未完成完整控件模板、系列选择、范围选择器、线段/竖向 Trackball 与轴读数标签、触摸仲裁、动画、JPEG、点级虚拟辅助语义/图案与真实读屏。C4 的增量数据、分层绘图缓存、同 X/二维退化查询、多图预算和长稳仍需继续。未参与场景的附加轴标注也仍跳过。

未挂载控件不订阅轴变化；已有缓存场景后在未挂载状态修改轴数据域，不属于本轮新增的轴刷新保证。文字缓存对此状态选择绕过，避免新增字体/画刷陈旧问题，但没有扩展原有轴订阅生命周期。

Windows/Linux 的物理输入、指定物理 Ubuntu 22.04/24.04、原生 Wayland、ARM/麒麟/UOS、真实 renderer/present 和官方授权参考不由 Headless 或 WSLg 合成检查替代。AX12 显式类别兼容差异与早期并行测试 UI 队列挂起的根因继续保留。
