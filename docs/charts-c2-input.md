# Charts C2-05：触摸、触控板与嵌套滚动输入

更新日期：2026-10-17。本批实现当前 Line/Bar/Scatter 的触摸平移、双指捏合、连续双轴滚轮/触控板输入，以及滚动容器嵌套仲裁，已接入 Demo 与 Usage。Windows 与 Ubuntu 26.04 WSLg 已通过当前三系列输入功能验证，数量来自本批实际运行。C2 整阶段、物理设备和正式性能验收继续开放。

## 滚轮与精密触控板

Avalonia 12.0.3 将滚轮与精密滚动都交给 `PointerWheelChanged`；本库保留连续的小数 X/Y delta，由显式配置决定操作，不根据 delta 大小猜测输入硬件。

| API | 默认值及当前契约 |
| --- | --- |
| `WheelMode` | `ChartWheelMode.Zoom`，保留原有 opt-in 滚轮缩放；其他值为 `Pan/Auto/Disabled`。缩放和平移仍分别要求 `IsZoomEnabled/IsPanEnabled`。 |
| `WheelZoomModifiers` | `None`。Zoom 模式不要求修饰键；Auto 模式的 None 表示 Control 或 Meta 缩放，其余滚动尝试平移。设置非 None 后，Zoom/Auto 都要求该 mask 的全部位，允许附加修饰键；Pan 忽略此项。 |
| `WheelPanSensitivity` | `48` DIP/Delta 单位，必须为有限正数。连续双轴输入分别保留；Shift 将只有 Y 的滚动改为水平平移，已有 X/Y 的双轴 packet 不重映射。 |
| `WheelZoomSensitivity` | `1.2`，必须有限且大于 1。正 Y delta 放大；指数输入限制在 ±20，极端有限设置饱和为可用变换。 |
| `InputRoutingMode` | `ChartInputRoutingMode.Auto`；另有 `Chart/ScrollViewer`。默认 WheelMode 仍为 Zoom，不会因新增 Auto 路由而把原有滚轮缩放改成页面滚动。 |

`Chart` 路由优先尝试图表：只有主轴或显式附加轴的有效范围实际改变时才消费事件；范围/缩放极限或锁轴导致的 no-op 会继续交给父容器。`ScrollViewer` 路由将导航交给父控件。`Auto` 配合 `WheelMode.Auto` 时，普通滚动先检查祖先 ScrollViewer 的实际可滚动轴、Extent/Viewport 与当前 Offset 边界；存在能沿该 packet 滚动的祖先时交给页面，配置的缩放修饰键（默认 Control/Meta）仍由图表尝试。在父容器相应边界且图表还能改变范围时，图表可以接手。对角 packet 整体仲裁，不把同一个 packet 分给图表和父容器。

绘图区外、空图、浮动图例遮挡、交互关闭、不可见/禁用或无有效轴的输入不会启动图表导航；图例自身滚动继续优先处理。有效导航切换会结束之前的触摸/鼠标基线，避免旧手势随后覆盖新范围。无效 delta 不产生范围更新。

## 触摸与捏合

`IsTouchEnabled` 默认 `true`，`TouchPanThreshold` 默认 `8 DIP` 且必须有限、非负；原有缩放/平移开关继续决定相应操作是否启用。按下时记录触点，超过累计位移或触点距离变化阈值、并能改变范围后才认领手势并请求捕获。触摸点/系列选择与原记录命令延迟到释放时执行；拖动、捏合及多触点序列不会被误当成点击。ScrollViewer 路由只委托导航，轻触仍可选择。

单指平移以手势开始时的视图为基线，不累计每个 move 的误差。双指使用开始距离与当前距离之比缩放，保持原数据锚点在移动后的质心之下；只开缩放时固定原锚点，只开平移时保留跨度。两指变一指或触点数改变后重建基线，避免跳变；经历多触点的手势不会在最后一个触点释放时产生单点选择。

`Auto` 的单指输入按主要移动方向检查祖先 ScrollViewer：父容器能滚动时让出这一整条触点序列，后续不在同一拖动中抢回；例如仅有纵向滚动的父容器可处理纵向拖动，而图表仍可争取横向平移。双指缩放在 Chart/Auto 路由下由图表尝试，ScrollViewer 路由保持放行。已经声明捕获的手势保持所有权直到结束或取消。

宿主/平台已提供的 `PinchEvent/PinchEndedEvent` 可走累计 Scale 的原生手势入口；图表没有另装 pinch recognizer，自己的双触点流存在时忽略该入口，避免重复缩放。这证明接受相应路由事件，不证明 Windows/Linux 的每种驱动都会生成该手势。

触点取消、捕获丢失、业务侧改源/改轴/改 Viewport、相关输入配置变化、隐藏/禁用/detach、布局变化、窗口失焦、Escape 和重置会结束当前输入并释放捕获。窗口级 Escape 在图表未取得键盘焦点时也能取消活动手势；窗口及祖先订阅在 detach 时释放。应用回调的新状态优先，旧事务剩余写入停止。

## 坐标与业务状态

鼠标平移、滚轮与触摸共用输入事务：先准备所有参与轴的结果，再发布 Viewport 和轴视图；每次业务回调后检查版本，较新的状态可中止旧输入。未选择的轴保持原值。每根轴在自己的完整变换范围与当前窗口上计算屏幕锚点、反向、跨度和 min/max zoom，覆盖线性、对数、日期、断轴及额外轴；显式附加轴按自己的窗口独立平移/缩放，主轴已到极限仍可由该附加轴产生有效改变。无位移的轴原样保留，避免无意义的浮点回写。

此行为不重新映射源记录、不改 StableId/SourceIndex，也不把插值坐标当成原始项。已有点/系列选择、Trackball、范围选择器与多图同步继续回归。输入事务不是缩放历史栈；返回/前进历史仍未实现。

## Demo 与消费方式

运行统一 Demo，进入 Charts → **“输入完整性：触摸捏合、平移、触控板与嵌套滚动”**。示例初始使用 WheelMode=Auto、InputRoutingMode=Auto；库默认 WheelMode 仍为 Zoom。示例提供真实嵌套 ScrollViewer、当前范围与容器 Offset、触摸开关、操作轴、三种路由、四种滚轮模式、灵敏度、阈值、取消/恢复和全范围边界交接。

```powershell
dotnet run --project samples/NT.AvaloniaControl.Demo -c Release -- --page charts
dotnet run --project samples/Charts.Usage -c Release
```

编译 AXAML/绑定与两种隔离包消费者使用公开 Pointer 事件及 PinchEvent 验证接入。Raw touch 注入仅在测试适配器中使用反射：Avalonia 12 的运行程序集有相应入口，编译引用程序集有意隐藏它们；测试仍经过真实 TouchDevice、命中、指针路由与父 ScrollGestureRecognizer，但这条路径不是产品公开 API 或物理触屏证据。产品包不依赖该反射适配器。

## Windows 与 Linux 验证

| 检查 | Windows | Ubuntu 26.04 WSLg / XWayland |
| --- | --- | --- |
| 当前 Charts 测试 | 通过：748 | 通过：748 |
| Demo 程序化检查 | 通过：322 | 通过：322 |
| Usage 编译绑定与运行 | 通过：108 | 通过：108 |
| 功能独用 / 默认主题包消费者 | 通过：各 132 | 通过：各 132 |
| Release 警告/错误、源码/程序集/API 身份 | 零警告/零错误，身份一致 | 零警告/零错误，身份一致 |

本批[验证清单](charts-c2-input-validation.json)和[API 快照](charts-c2-input-api.txt)已关联实际运行结果。本批覆盖小数双轴、修饰键、锁轴/反向、独立附加轴、对数/日期/断轴锚点、累计捏合/移动质心、多触点与 tap 分离、真实父 Offset、捕获/失焦/取消、重入回调，以及原有功能回归。结果、数量和平台身份只从各平台实际产物回填，不从旧版本推算。

原生窗口程序化输入、RawTouch 合成注入、RenderTargetBitmap/PNG、物理设备和 compositor present 分开记录。Windows 与 WSLg 的功能结果即使通过，也不替代 Ubuntu 22.04/24.04 物理桌面或触屏/精密触控板设备验收。本批不运行正式性能时序池或内存回归，不继承历史源码的性能资格。


冻结源码 1242 个文件，每平台 564 个声明输入；两平台与当前声明源码、冻结副本哈希一致。当前 API 只增加声明，未删除或改变已有声明。独立包消费者各使用全新缓存，验证锁定恢复、编译绑定及当前 nupkg 的程序集身份。

本批同时修复父手势识别器继续处理已认领触摸包的问题（使用公开 PreventGestureRecognition）、合法轴范围重复变换的浮点漂移、焦点回调移动控件后的过期坐标，以及捕获释放/命令 CanExecute 重入。Raw 测试先用 Headless 自带鼠标入口提交命中场景，再逐次断言 TouchBegin 实际命中图表；未用直接 RaiseEvent 替代原始触摸链。原始失败 TRX 和 Demo 布局检查保存在 development，最终报告不以它们作为通过证据。亮暗捏合前后、恢复图片以及三种 DPI 导出已独立解码复核，均属 RenderTargetBitmap/PNG。

独立复核共 262 项检查通过，核验 2119 份文件哈希；12 组 DPI 正负/恢复图像的恢复差异均为 0。Core API 从 82 增至 84 个类型，新增 33 个声明，既有声明无删除或改变；Themes 的 3 个类型不变。首轮两平台基础验证也通过，但包消费者错误地要求已在底部的父容器继续向下滚动；最终 v2 选择实际可滚动方向，两个冻结版本间仅消费者测试文件改变，产品代码不变，原始 v1 证据保留。

## 剩余问题与下一步

- 设备输入：物理触屏/触控板、系统级惯性与手掌拒绝、驱动修饰键映射、DPI/跨屏、窗口切换和目标 Linux 输入后端仍待独立验证。当前可连续接收系统传来的惯性 delta，没有新增自己的惯性模拟器或 palm rejection。
- 平台与性能：Ubuntu 22.04/24.04 实机、实际 GPU/backend/present、UIA/AT-SPI、原生 Wayland/ARM 及官方许可运行参考继续开放；本批没有正式性能或内存资格。百万点 hit p95 `0.7649→1.1235 ms` 与全部历史性能残余继续留 C4。
- 历史证据：C2-04 已记录的 4 个原始 `BarCache.PixelProbe.dll` 路径缺失（8 处历史引用）继续开放，见[上一批剩余问题](charts-c2-selection-range.md#剩余问题与下一步)。不恢复或改写旧 C0/C2-03/C2-04/v15 报告、JSON、API 或哈希，也不用新编译文件代替旧产物。
- 功能后续按原计划：核对 C2-06 标注/参考元素剩余契约，再推进 C2-07 动画、C2-08 JPEG/异步导出及 C2-09 点级辅助语义与真实读屏。类别图例/高级标签、范围内嵌概览、跨图游标与未来图族适配仍开放；本批输入实现不代表 C2 整阶段完成。

阶段顺序与出口继续由[开发计划](charts-development-plan.md)、[功能矩阵](charts-official-alignment.md)和[性能与平台计划](charts-performance-platform-plan.md)约束。
