# Charts C2-03：线段插值与 Trackball

更新日期：2026-10-17。本批完成现有 Line/Bar/Scatter 范围的线段读数、横竖 Trackball、逐点/聚合内容及 Demo/Usage 接入。百万点命中尾延迟继续按用户决定后置 C4；本批不运行性能时序池或内存回归，也不沿用上一版本的性能资格。

## 已实现

| 能力 | 当前契约 |
| --- | --- |
| 最近线段 | `HitTestSegment` 和 `TooltipMode=NearestSegment` 查询内置折线的可见线段，按 DIP 距离和容差选取。 |
| 按方向插值 | `InterpolateX` / `InterpolateY` 读取各系列与当前屏幕 X / Y 的交点；非单调、多交点、垂直/水平段采用确定性的选择规则。 |
| 数据身份 | `ChartSegmentHit.Start/End` 保留原业务对象、索引和 StableId；派生 `Point/Position/Fraction` 单独暴露。原 `HoveredPoint/Points` 不伪装成新增业务记录。 |
| 数值与裁剪 | 在实际屏幕直线几何上插值，再按所属轴逆投影；支持对数/逆轴/多轴、物理邻接、缺口和断轴裁剪、穿越视口的屏外端点。 |
| Trackball | `None/Vertical/Horizontal`、`NearestPoint/LinearInterpolation`、`FloatAllPoints/GroupAllPoints`，独立于普通 Tooltip 开关；启用时优先使用 Trackball 配置，保留 TooltipMode 绑定。 |
| 内容与样式 | 被动 `IDataTemplate`、formatter、独立线/圆形标记配置、图表资源继承、实际控件布局及 PNG。`TrackballMarkerSize` 为 DIP 直径。 |
| 生命周期 | 可观察派生状态与端点身份，模式/源/可见性变更、拖动、退出、卸载和回调重入时清理；旧提示模式与默认无追踪路径继续回归。 |

插值只适用于当前内置直线 `LineSeries`。柱和散点读取真实原始点，自定义 renderer 不凭图例符号推断线段。对数轴的屏幕中点是逆投影结果，不能用原始值的算术平均替代。新线段查询完全等距时选最早系列及最早物理段；原 SharedX/SharedY 的平局规则保持兼容。精确端点仍取原始坐标和身份。

`ChartTooltipContext` 保留原构造参数，新增 `Readings/Segments/PrimarySegment`；业务模板可分别显示估算坐标与两个真实端点。聚合 Trackball 卡得到全部读数，逐点卡得到自身读数。完整接入契约见 [Charts README](../src/NT.AvaloniaControl.Charts/README.md)。

Demo 的 Charts 页新增“Trackball：线段插值、端点身份与共享追踪”，提供方向、吸附和显示模式切换、端点更新及视图重置。[Charts.Usage](../samples/Charts.Usage/MainWindow.axaml) 同时提供编译绑定示例。

## 验证

| 最终运行 | Charts 测试 | 原生程序化 Demo | 独立 Usage | 真实 nupkg 消费者 |
| --- | ---: | ---: | ---: | --- |
| Windows 本机 | 587/587 | 206/206 | 84/84 | 功能包、功能+主题包各 100/100 |
| Ubuntu 26.04 x64 / WSL2、WSLg XWayland | 587/587 | 206/206 | 84/84 | 功能包、功能+主题包各 100/100 |

两平台 Release 编译零警告、零错误，使用同一份 1215 文件冻结副本、各 543 个声明验证输入。消费者使用各自全新缓存与实际 nupkg，未引用源码项目。基础运行显式跳过包消费者，独立包消费报告另行关联。

测试与原生取样覆盖独立数值预期、原始端点、缺口/断轴、模式切换、绑定/资源、回调重入、模板生命周期及单次导出。图片是原生窗口/图表的 RenderTargetBitmap 与 PNG 导出，不代表物理输入、桌面截图或实际 compositor present。具体像素取样与导出 DPI 数据以原报告为准。

源码、程序集、命令、平台环境、每项结果及文件 SHA 见 [验证清单](charts-c2-trackball-validation.json)；公开声明见 [API 快照](charts-c2-trackball-api.txt)，运行证据见 [本批目录](../artifacts/charts-c2-trackball-20261017)。C0、v15 及上一批控件模板验收文件保持历史原值。

本批还修复两项实际生命周期问题：切换 Trackball 配置时，业务回调中较新的输入不能被外层旧锚点覆盖；managed 图例脏状态不再在 Render 内构建/测量或拆除控件树，而是排队到合法布局阶段更新。开发首轮原生崩溃、后续通过运行及独立渲染安全回归均保留。

开发阶段失败日志不覆盖：Usage 的生成控件名冲突已修；数学测试改用真实 VisualRoot 的精确 DIP 路由，真实包消费者仍使用窗口输入并按实际送达坐标计算独立预期；非 ordinal 快照遵循合法原始索引与身份。重合标记按位置核对数量，业务 INPC 导致源失效后先验证旧卡释放，再重新读取并验证新名称；相应数值、端点及内容断言保持。

## 剩余问题

- C2-03 后续适配：平滑/阶梯和堆叠系列、LOD 聚合的读数语义、自定义 renderer 线段扩展，随 C3/C4 对应能力接入；本批不宣称这些尚未实现的图族已通过。
- 性能：线段查询和大量浮层的成本、SharedY 首索引规模、百万点命中尾延迟及全部历史残余留到 C4；本批性能/内存资格未测，正式 SLA 未验收。
- C2 下一批：系列选择、范围选择器、多图同步；类别图例、图例虚拟化、标签连接线/高级避让、触摸、动画、JPEG 和点级原生辅助功能仍开放。
- 平台：目标 Ubuntu 22.04/24.04 实机、Windows/Linux 物理输入、DPI/多屏、UIA/AT-SPI、实际 GPU/backend/present、原生 Wayland/ARM 与官方许可运行参考仍待独立验收。
