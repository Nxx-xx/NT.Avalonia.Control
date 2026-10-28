# C7-03 Windows 运行时诊断与 SKPicture 试验

更新日期：2026-10-26。结论：**完成本轮诊断和双平台小试验，未达到产品接入条件；C7-03 仍未完成。**

保持现有 Render 调用、原始点数/顺序、画质、协议及门槛。并行原型继续不接入。本轮没有改变 affinity、QoS、线程优先级、电源策略或产品绘制代码。

[机器记录](charts-c7-runtime-picture-validation.json)保存本轮统计、固定指纹及完整证据文件的路径、大小、SHA。证据根目录为：

`C:/Users/a4472/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c7-runtime-picture-20261026`

Windows ETL/raw/调度证据位于 `D:/Procedure/AvaloniaControl/artifacts/c7-runtime-picture/windows/trace-campaign-v1`。最近一次原协议完整 formal 仍是 [B17](charts-c7-scatter-followup.md)：Windows/Linux 各107进程；散点排空后878/1420，Windows柱形223个未完成槽。本轮带观察器诊断和 native-only 小试验不替代这些结果。

## 执行顺序与停止条件

1. 先运行 Windows CPU/GC/UI调度诊断并复核相同请求。
2. 再对当前 B8 fast DrawPath 做 same-plan SKPicture 保真和有限寿命净成本对照。
3. 两端有稳定净收益才投入预算、退休、旧帧租用和产品接入。本轮 Windows 配对方向不一致，条件未满足，因此停止在试验阶段，没有强行增加缓存。

## Windows 诊断

沿用冻结 B12 原始宿主，原四个 track，每轮10秒预热、30秒测量、60Hz和15秒最大排空，连续5个独立进程。增加唯一命名的 owned WPR 会话和1秒 PDH 旁路；5个 Start/Stop 均成功，所有会话正常停止，没有取消其他录制。

进程/UI线程由存活 HWND、PID和完整创建时刻交叉确认。ETW header 的公开 PerfFreq、原QPC、原raw频率/身份和WPR命令时间括号均匹配；五轮阶段数据全部取得时钟、GC配对和UI调度观察资格，报告 lost=0、QPC顺序违规=0。**这不证明绝对无丢失。** GC collection lifetime、EE suspension envelope、fully-suspended window 分别统计，不混同为暂停。

| 诊断轮 | producer exit | 散点完成 / 1800 | 排空后剩余 | 柱形排空后剩余 |
| --- | ---: | ---: | ---: | ---: |
| rep-1 | 0 | 1800 | 0 | 0 |
| rep-2 | 1 | 1726 | 74 | 0 |
| rep-3 | 0 | 1800 | 0 | 0 |
| rep-4 | 0 | 1800 | 0 | 0 |
| rep-5 | 1 | 1618 | 182 | 0 |

所有失败轮保留。带观察器的新重复不能作为产品吞吐通过，也不能据此宣称 B17 柱形问题已修复。

第4/5轮散点共有1618个 measured 请求，其 index/action/revision/input/visible/rendered/range 全部匹配；参考轮额外182个已观察请求及第5轮182个未尝试槽单独保留：

| 同请求指标 | 第4轮 | 第5轮 |
| --- | ---: | ---: |
| record P95 ms | 28.5485 | 30.2244 |
| raster P95 ms | 22.3123 | 32.3928 |
| raster sum(onCPU) / sum(wall) | 99.6141% | 99.4065% |
| raster GC envelope / wall | 0.3613% | 0.4133% |
| raster Ready overlap / wall | 0.0093% | 0.0068% |

百分比使用同一组实际请求的时间求和，未对阶段P95加减。第5轮自身P95慢 raster 的81条实际行，onCPU占98.5771%、GC envelope占0.8695%、Ready占0.0141%。新慢轮主要表现为线程运行成本增加；GC窗口、offCPU和Ready分别保留，不能由窗口重叠直接断言GC造成延迟，native preemptive PInvoke可在EE暂停期间继续运行。

### 执行位置关联

只读 GetSystemCpuSetInformation 及处理器组查询返回当前单一Group0的16个逻辑CPU。ETW Processor编号与当前拓扑的一一映射通过；每CPU分桶时间总和与已有 onCPU 精确吻合。没有修改进程或线程设置。

| 相同散点请求集合的 Class0/0 运行时间占比（逻辑12–15） | 第1轮 | 第4轮 | 第5轮 |
| --- | ---: | ---: | ---: |
| 1618个匹配请求 | 0.0104% | 0.0074% | 7.5174% |
| 固定取第5轮P95慢尾的81个相同index | 0.0124% | 0.0109% | 31.4823% |

这表明慢尾伴随执行位置/CPU类分布变化。拓扑在采样后读取，不是历史快照；不推断全部延迟由此造成，也不将类编号直接当作P/E、每核真实频率或热限证据。PDH只有1秒系统级粗观测，原生FILETIME的UTC解释未取得资格，使用QPC括号关联；没有温度和校准的每请求核心频率。**B17历史第5轮的唯一原因仍未确定。**

## SKPicture 保真与计时范围

直接借用当前 B8 实际 GetFastShapes 的绝对 FillPath/OutlinePath，保活原 owner；每点按 fill、outline 原顺序发出20000条DrawPath。没有使用旧 DrawEllipse 对照、合并、采样、conic改写、sprite或并行栅格。

v3 两端各73例严格像素检查通过，各218张PNG；覆盖颜色/透明度、重叠、边缘、裁剪、opacity layer、整数平移及同owner画笔变更。实际picture激活计数与直接路径资格通过。连同v2历史构建，独立文件审计2165项通过、1117份文件指纹；580对同run编码文件及436对同平台v2/v3对应文件全部相同。没有把producer的premultiplied BGRA hash冒充Python独立解码，也没有宣称232幅产品回归或全部原生path序列已独立重算。

性能采用同一冻结v3构建，独立进程跳过图片重验并显式登记 parity=null，关联前面的同build保真。Windows的每draw起止CPU观察放在计时窗外；Linux为null/not-supported，不猜宿主CPU类。端点只是端点，不证明整段没有迁移，全部样本保留，不按CPU类过滤或调整净值。

N10模型：同一 prepared path owner 每组绘制10次，第1次direct，第2次record+replay，后续replay，组末Dispose。净值为全部draw窗口加最终退休成本除以绘制次数，录制已包含于第2次，不重复相加。Clear、读回、hash、PNG及记账在draw窗外。每次10组预热、计划10秒测量，最后完整组可以越过终点。

## 三对新进程结果

每端AB/BA/AB；以下均为单进程有限寿命净值ms/draw，负变化代表候选更快。不合并请求P95，不剔除任何慢轮或CPU迁移。

| 平台 / 对 | 原 DrawPath | 延迟 Picture | 候选变化 |
| --- | ---: | ---: | ---: |
| windows / 1 | 19.891468 | 19.703927 | -0.943% |
| windows / 2 | 20.234333 | 20.602387 | +1.819% |
| windows / 3 | 20.119013 | 19.786564 | -1.652% |
| linux / 1 | 27.920271 | 27.171288 | -2.683% |
| linux / 2 | 27.602247 | 27.476471 | -0.456% |
| linux / 3 | 27.729346 | 27.294008 | -1.570% |

12个进程全部正常完成，所有原窗口、记录/退休/分配、净值和分位数重算一致。独立复核保留6400条draw、其中5200条measurement，以及3610个Windows绘制窗的CPU双端点。旧汇总器曾把同一进程时刻的UTC与+08:00字符串当成12项不一致；v4只对该时间字段使用精确整数纳秒比较，保留100ns/1ns差异，PID/其他字段/数值仍严格相等。原raw/campaign未重跑或改写，旧误报与修复审计均保留。

Windows两对略快、一对退化约1.82%，该对draw P95也从21.5986升至24.1538ms；Linux三对略快约0.46–2.68%。**没有建立稳定的跨平台有限寿命净收益，不接受产品接入。** 不用热重放上界代替录制/退休净成本，也不从小试验推算原请求吞吐或60FPS。

每份picture的ApproximateBytesUsed为2080144 bytes、20000 operations，是Skia估算，不是匹配的原生/进程峰值。N1/N2/N9、长期热态以及真实生产plan成功绘制次数没有实测；N10第一轮配对未满足接入条件后，没有扩项或先做跨层缓存工程。

## 资源与后续问题

当前记录器/native RTB交替静态上共享plan，WithPaint仅StrokeKey变化时新建；但真实成功重用次数仍缺直接计数。普通源/视图失效、资源准入失败、驱逐、Trim、隐藏、detach、导出和旧帧并发都必须有退休/租用设计。Frame.Dispose只清本帧引用，不能退休共享plan；不能靠GC解释缓存资源释放，也不能新增Series.Changed观察者而改变CanShareRenderingGeometryHistory。未达收益条件，本轮没有修改这些契约。

后续优先围绕公开DrawingGroup逐点记录分配和原生栅格运行成本寻找安全候选，保留CPU执行位置观察及独立可变导出语义。历史B17散点积压和Windows柱形失败仍开放。官方无许可保持blocked-reference，测量/倍率为null；正式present、匹配峰值内存及目标实机/GPU/触控资格未取得，已后置的百万点命中尾延迟和原生选中项桥接不变。

## 验证和保留的失败

- 五轮诊断的UI身份、QPC、报告失采、GC配对及全部record/raster阶段关联已核验；8个手算区间/资格契约通过。
- 同时刻比较6个精度/负例契约通过；独立12进程时间和窗口审计47737项通过。
- helper旧编译错误、smoke-v1时间身份误拒、PDH原时间解释及旧pair汇总误报均保留，新版本分别修复后验证；没有改写旧失败。
- 没有新产品绘制代码，因此未重跑3256项产品全量、232幅专项、Demo/Usage、打包消费者或完整107进程活动，也未运行远程CI。历史资格保持原候选/版本范围，不搬用为本轮通过。
