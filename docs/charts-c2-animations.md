# Charts C2-07：入场、数据与视图变化动画

更新日期：2026-10-17。本批针对当前折线、柱状、散点图增加可选动画，默认保持即时更新。Windows 与 Ubuntu 26.04 WSLg 已通过本批当前三系列功能验证；C2-06 的 821/366/119/147 保留历史身份。

## 配置与时间线

`AnimationTriggers` 为 `ChartAnimationTriggers` 标志：None（默认）、Entrance、Data、Viewport 和 All。`AnimationDuration` 默认 450 ms，`AnimationDelay` 默认 0，均拒绝负值。`AnimationEasing` 使用 Avalonia `IEasing`，默认 CubicEaseOut；null 为线性，异常或非有限结果回退线性，有限输出限制到 0–1。

`IsAnimationPaused` 冻结当前帧及已消耗的延迟，恢复后继续剩余时间。只读 `AnimationState` 为 Idle、Delayed、Running、Paused，`AnimationProgress` 为缓动后进度，完成时为 1。`ReplayEntranceAnimation()` 重播已启用的入场，`CancelAnimations()` 显示最新目标，不修改源数据或请求视图。时长为 0 时直接完成并忽略延迟。修改动画配置结束旧过渡，下一次变化使用新配置。

生产时间线使用 `TimeProvider.System` 和窗口 `RequestAnimationFrame`。读取状态、命中、绘制或导出只消费已发布帧，不自行推进时钟；取消、暂停和新业务更新使旧调度失效。内部可注入时钟的单测与原生真实 RAF 验证分开留证。

## 数据、坐标与一致性

入场从有效 Y 基线开始：线性域允许 0 时使用 0，否则使用当前有效下界；零高度柱不绘制。数据过渡优先匹配 IdPath 的值身份，无 IdPath 时使用引用身份及源索引。新增点从基线进入，移除点立即消失；连续更新从当前画面过渡到最新目标。业务 Id 应保持唯一。

每一帧使用同一组插值后的快照、尺度和投影，供系列、命中、选择、读数、标注、标签、绘图区内容与 PNG 使用。帧中 Point 是画面插值值，Item/StableId/SourceIndex 指向当前目标的真实业务项；`series.CreateSnapshot()` 保留最新源数据，不被动画修改。非有限或数据域无效点保留缺口，穿过既有断轴内部的中间点暂时作为缺口。

视图属性保留请求目标，VisibleRange 反映当前动画帧。数值、反向、对数与独立多轴使用各自变换空间插值；自动数据范围由真实目标数据确定，不受入场基线污染。绘图区使用目标留白，当前尺度和刻度逐帧更新。联动图交换目标意图，各自播放时间线，避免中间帧互相反馈。

直接输入导航抢占动画并即时应用。隐藏、祖先隐藏、卸载、禁用、尺寸变化，以及轴配置或其它不支持平滑过渡的样式变化结束旧过渡；旧回调不再恢复。当前帧导出包含相同标注与控件内容，并保留原 PNG 尺寸/DPI/背景/流所有权契约。

## 减少动态效果与大数据

`ReduceMotion=true` 立即结束活动过渡，后续更新直接显示最终状态。Avalonia 12.0.3 的公开 IPlatformSettings 没有统一的减少动态效果属性，本批通过显式属性供应用绑定用户或系统偏好；不宣称已验证 Windows/Linux 系统设置自动接入。

`AnimationPointLimit` 默认 10000，为当前可见系列源点总数上限；目标或旧画面任一超过上限时整次过渡直接完成。0 禁用过渡。默认 None 不创建动画帧、旧快照或祖先观察列表。此策略限制逐帧插值工作量，不能视为已经通过大数据 FPS、内存或真实呈现性能验收；C4 预算保持不变。

## Demo 与接入

在统一 Demo 的 Charts 页展开“动画：入场、数据与视图变化”，启用后查看三类系列的重播、更新数据、缩放/恢复、时长/缓动/延迟、暂停/恢复、取消、减少动态效果、点数预算与 PNG 导出。亮暗原生验证另建固定坐标窗口，不使用内部测试时钟。

```powershell
dotnet run --project samples/NT.AvaloniaControl.Demo -c Release -- --page charts
dotnet run --project samples/Charts.Usage -c Release
```

Usage 提供相同配置的 compiled AXAML/MVVM 绑定，源数据和视图更新、暂停 TwoWay、减少动态效果及预算策略也用于两个隔离 NuGet 消费者。Headless 包消费只证明接入与结果契约；真实 RAF、PNG 像素和当前帧导出来自原生 Demo。

## Windows 与 Linux 验证

| 检查 | Windows 原生窗口/程序化 | Ubuntu 26.04 WSLg（请求 X11/XWayland） |
| --- | --- | --- |
| 全部 Charts 测试 | 860 通过 | 860 通过 |
| Demo | 420 通过 | 420 通过 |
| Usage 编译绑定 | 131 通过 | 131 通过 |
| 功能 / 主题包消费者 | 各 163 通过 | 各 163 通过 |
| Release | 零警告、零错误 | 零警告、零错误 |

[验证清单](charts-c2-animations-validation.json)和[API 快照](charts-c2-animations-api.txt)关联原始结果、PNG、源码及程序集身份。冻结副本 1259 文件，每平台 575 个声明输入；两平台与当前声明源码哈希一致，不宣称后续一般文档编辑后的全仓库逐文件相同。21 个历史/渲染器固定记录保持不变，官方矩阵 200 个 ID 及既有来源链接/顺序不变。Linux 的 X11/XWayland 是请求及环境范围，实际图形后端、GPU 与 present 未探测。

动画新增 39 项可控时钟单测、54 项原生亮暗检查、12 项 Usage 绑定和每个消费者 4 项独立动画检查。原生真实 RAF 的线性/CubicEaseOut 时间进度、橙色散点像素中心、三系列墨迹、delay/pause/cancel/reduce/连续更新、当前帧及三种 DPI PNG 均有独立数值/像素复核。最终独立复核通过 763 项检查；API 仅新增声明，未删除既有声明。Headless 导出只检查明确结果/NotSupportedException零输出契约，真实 PNG 像素来自原生 Demo。

首轮 v1 的运行结果完整保留，但没有覆盖独立探针新发现的同版本快照发布缺口，因此不作为最终候选。v2 用内部发布修订计数区分数据更新和样式事件，保持 SourceVersion/快照版本及公共 API 契约，并新增 sync/async、连续重定向及拒绝发布检查。系列匹配另改为引用字典，避免逐帧系列数平方扫描，重复实例保留既有首项语义。v3 再补齐取消、自然完成和跳过时的会话缓动引用清理，在状态通知前释放以保留回调重入语义，并用 WeakReference 验证公开缓动配置替换后旧应用对象可回收。最终双平台从 v3 冻结源码重新构建、消费和验证，v1/v2 原始结果均完整保留。

开发失败亦保留：测试初版直接实现 Avalonia12 IEasing、误用 Point.Length 导致编译失败，改用支持的 Easing 基类和数值距离；首轮动画测试的零高柱/尚未布局起始时刻假设错误已修正；Demo 固定像素探针改用不透明柱填充，用户示例保留半透明样式；快照发布独立探针第一次缺本机原生依赖，补齐后复现并推动修复。未通过降低测试阈值替代修复。动画独立审查前两轮的消费者包装层级读取与 PowerShell 数值重载错误均保留；按原始 JSON 的 consumers.rows 和显式 double 缓动公式修正审查脚本后，763 项全部通过，未放宽容差或改候选及原始结果。

## 剩余问题

- 当前动画帧会清理旧 Tooltip/Trackball 读数，新的指针事件或命中查询使用当前画面。静止指针下持续跟随动态数据的读数尚未实现。
- C2-08 JPEG/异步导出与 C2-09 点级辅助语义尚待推进；未来图族、High/Low/Weight 等额外几何、离场淡出、逐系列独立时序/进度、逐点错峰和任意样式/轴拓扑动画不在当前三系列过渡范围。
- 系统减少动态效果自动映射、Windows/Linux 物理输入、跨屏/DPI、GPU/backend/present 和真实帧预算仍需独立验收；Ubuntu 26.04 WSLg 合成窗口不替代 Ubuntu 22.04/24.04 物理桌面、Wayland/ARM 或官方许可运行对照。
- C2-06 的 AX06 轴角落标题/末端刻度轻微重叠继续开放；高级标签避让、类别图例、范围概览、跨图游标和四界 Viewport 内独立 nullable-auto 轴的既有边界保留。
- 本批不关闭 C4：百万点命中 p95 的 0.7649→1.1235 ms、历史残余、增量/LOD、内存与长稳仍按用户决定后期处理。
- 继承的 4 个原始 v15 BarCache.PixelProbe.dll 缺失（8 处历史引用）继续保留，不用重建产物替代；C0、v15、C2-03 至 C2-06 的原报告/API 不改写。

总计划见[开发计划](charts-development-plan.md)、[官方矩阵](charts-official-alignment.md)与[性能/平台附件](charts-performance-platform-plan.md)。
