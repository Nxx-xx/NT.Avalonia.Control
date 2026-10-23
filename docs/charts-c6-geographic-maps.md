# Charts C6-03：离线地理图与分层地图

执行日期：2026-10-23（香港时间）。`ShapeMap`、`SeatMap` 与 `ShapeLayer`、`BubbleLayer`、`HeatmapLayer` 覆盖 MP01～MP05 五个目录模式：气泡地图、区域着色、热力地图、平面座位图和自定义形状图。Demo **Charts → 地理图**、编译绑定 Usage 与两类包消费者均已接入；下一主项 **C6-04 排程和运营**。本轮是离线地图基础功能的软件验收，完整 C6、官方 API/二进制/默认行为等价继续开放。

## 数据与显示契约

GeoJSON 支持 Polygon/MultiPolygon、空洞和多块区域，类型化入口为 `MapFeature`。区域按唯一业务键关联；缺失/null 只显示不可选的无数据形状，未知业务区域保留在 `UnknownRegions`。气泡最小半径为0时面积按非负值分配，0值不绘制；热力格保留全部原始贡献成员，边界核重新归一化。Tooltip、键盘、选择与导出沿用原始业务来源。图层按集合顺序绘制，共享等比例 fit、缩放和平移；隐藏/Opacity0层保留层序快照，数值结果为null，不计算布局或参与fit。

`Equirectangular` 使用物理宽高比2；`WebMercator` 在 ±85.0511287798066° 截止并报告夹取，二者都使用北向正Y。`Identity` 使用原始平面X/Y，供SeatMap等非地理场景使用。地理环按最短经度弧处理日期变更线；刻意超过180°的边需预切分。默认 `StrictSimple` 拒绝自交；明确选择 `EvenOdd` 可显示已知自交并查询交点计数，但不放宽零面积、无效空洞或预算规则。

`FeaturesSource > GeoJson > 已加载SourceUri`。URI只接受显式本地file/avares，调用 `LoadSourceAsync` 才加载；取消、换源、较新请求和卸载防止过期结果发布，失败清空旧地图并公开状态/错误。INPC/INCC更新保留业务身份。地图投影、业务映射和原生几何仍有同步工作。

| 资源约束 | 当前上限 |
| --- | --- |
| feature / 原始顶点 / 单环顶点 / 观测 | 5000 / 200000 / 4096 / 20000 |
| GeoJSON / 嵌套深度 | 16MiB / 32 |
| 热力网格 / 核半径 / 工作量 | 256 / 8 / 8000000 |
| 整图可见热力层保留贡献准入 | 1000000；逐层累计 rawCount×(2r+1)²，含0值 |

贡献准入为保守计数，不是实际字节或进程内存资格。离线世界资源保留 Natural Earth 110m 全部177 feature、10654顶点，未筛选或简化；固定commit、原文件/嵌入文件SHA、Public domain许可与转换见[资源来源](../samples/NT.AvaloniaControl.Demo/Assets/Maps/provenance.json)。两处真实自交采用显式EvenOdd，并保留Strict拒绝用例。

公开范围依据：[Bubble](https://docs.avaloniaui.net/controls/data-display/charts/maps/bubble-map-chart)、[Choropleth](https://docs.avaloniaui.net/controls/data-display/charts/maps/choropleth-map-chart)、[Heatmap](https://docs.avaloniaui.net/controls/data-display/charts/maps/heatmap-map-chart)、[Seat](https://docs.avaloniaui.net/controls/data-display/charts/maps/seat-map-chart)、[Shape](https://docs.avaloniaui.net/controls/data-display/charts/maps/shape-map-chart)、[GeoJSON RFC7946](https://www.rfc-editor.org/rfc/rfc7946)。本地公式和边界以上述契约为准。

![Windows地理图面板](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-geographic-maps-20261023/windows/run-v2/verification/functional-v2/charts-light-maps-light-board-overview.png)

[Linux暗色面板](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-geographic-maps-20261023/linux/run-v2/verification/functional-v2/charts-light-maps-dark-board-overview.png)；Windows/Linux浅暗四张正式面板逐图复核，见[视觉复核](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-geographic-maps-20261023/final-board-visual-review-v2.json)。图像来自原生合成窗口RenderTargetBitmap/导出，不认证物理设备、GPU完成或compositor present。

## Windows 与 Linux 验证

| 项目 | Windows本地原生合成 | Ubuntu26.04 WSL2/WSLg |
| --- | --- | --- |
| Charts完整测试（既有+地图+交互动画） | 2617（2515+98+4） | 2617（2515+98+4） |
| 完整Demo / 本轮地图检查 | 3286 / 242 | 3286 / 242 |
| 编译绑定Usage / 两类新cache消费者各 | 584 / 790 | 584 / 790 |
| 数值固定 / 规模 / 世界附录 | 88 / 2 / 3 | 88 / 2 / 3 |
| 数值 / 像素 / 来源谓词 | 2034945 / 156 / 4004 | 2034945 / 156 / 4004 |
| 本轮 / 完整Demo PNG | 24 / 997 | 24 / 997 |
| Release警告 / 错误 | 0 / 0 | 0 / 0 |

同一冻结候选 `c6-geographic-maps-source-v2`：1703源文件、999声明runtime输入，源快照SHA `69762602770CEC15F005C3F076E587389A92AED78CAE816C259D32C3ADAE2978`。两平台各九个受监督阶段串行，核对全套测试身份、源/程序集SHA及运行前后稳定性；新项目/消费cache只按SHA复用63/14个非产品依赖包，未复用历史Charts包，保留正常NuGet审计与警告。API316→350，旧类型及成员保留，两平台声明一致。

独立Python从原始几何/业务输入重算投影、边界、空洞命中、共享fit、气泡面积、热力总量及来源身份；规模和世界附录也检查完整输出。22张固定PNG含五模式×两主题×native/export96、两张高DPI图；10对逐RGBA零差异，另两张总览。来源检查包含32组探针及10组取消/最新版本/卸载记录。数值abs1e−8/rel1e−9，Skia公开几何边界abs1e−4在首次native前固定；其余协议阈值未降低。

| 固定规模诊断 | Windows elapsed / 当前线程分配 | Linux elapsed / 当前线程分配 |
| --- | --- | --- |
| perf-geometry-2000-10000 | 9.791 ms / 5037144 B | 9.880 ms / 5037144 B |
| perf-heat-10000-64x64-r2 | 14.982 ms / 11069400 B | 16.136 ms / 11069400 B |

这些是固定负载单次调用诊断；未建立p95、持续帧率、进程/原生/GPU内存或长稳资格，也未重跑C4-08正式性能门禁及旧阶段独立oracle。

## 保留失败与剩余问题

首次正式source-v1：Windows九阶段通过；Linux在既有**亮色**动画的暂停/96、144、192DPI导出阶段出现4项真实失败，377项检查后WaitProgress见Idle/progress1而抛错，地图阶段未执行，不能计作Linux地图通过。该次218文件、155PNG和所有原始日志完整封存；没有自动重试。随后独立诊断保持原核心源码、assert、时钟、时长和flush，只增加trace；单次exit1在4项检查通过后仍于入场动画报错。调用栈明确显示IsPointerOver属性变化经CartesianChart.OnPropertyChanged→PrepareAnimationChange(None)→CancelAnimations，将Running变为Idle。修复只为该交互属性请求重绘并返回；4项真实MouseMove用例在旧核心全部失败，修复后相关测试60/60通过；Linux单次诊断补齐亮暗主题trace并通过。旧ChartAnimationTests追加39行，原有行均保留，不声称旧文件SHA不变。完整4文件差异和前后SHA见[源码修订](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-geographic-maps-20261023/candidate-v2-source-amendment-v1.json)；最终新增98项地图与4项交互动画。新冻结源完整重验及合成诊断只覆盖本次复现，不能证明所有动画时序、物理输入或旧C7原生AV已解决。见[首轮正式失败](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-geographic-maps-20261023/candidate-v1-linux-failure-pins-v1.json)、[独立诊断](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-geographic-maps-20261023/animation-diagnostic-v1-pins.json)、[修复后单次诊断](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-geographic-maps-20261023/linux/animation-diagnostic-v2/diagnostic-run-v2.json)。

开发native-v1虽242项内置检查通过，独立来源检查发现24项负热力边界错误；已改为正尺寸Rect。Usage-v1发现删除业务点后仍订阅，现INCC删除即时裁剪。首次数值6项失败中，两项Eqc边界命中是产品缺陷；另四项是oracle将Mercator查询放在未投影平面、全局shoelace抵消小三角面积的错误。v4工具改用独立投影和原点平移面积，保留全部输入与容差，并用30项独立guard验证；对原始raw仍有两项真实失败。随后只修复Contains的ULP边界判定、保留严格拓扑OnSegment并加5项测试，最终数值通过。原协议、失败raw、回执和哈希均保留；审批超时发生于进程启动前，另记为工具失败。

- **C6-MAP-FIT-01**：无几何SeatMap单个double.Epsilon坐标的扩展可能舍入成相同边界，fit宽度/气泡半径为0而不可见；未承诺任意有限坐标均可显示。
- **C6-MAP-OBSERVER-01**：换ItemsSource或移除图层的旧成员订阅在下一次GetMarks/render释放；本轮只修复INCC逐行删除的即时释放。
- **C7-NATIVE-LOD-AV-01**：C6-02既有LOD原生导出0xC0000005根因未定位、未修复。本轮完整软件通过不能证明该故障已解决。
- Marker/Line/Vector/Pie叠加、瓦片、分类色映射、多座位选择、标签避让/模板、完整专业共享交互、C5/C6目录及官方等价仍开放。
- 物理Windows/Linux、Wayland/ARM/多发行版/多屏DPI、设备与读屏器、GPU/present、远端CI、正式性能和长稳待专项；百万点hit p95与A11Y-NATIVE-01按用户决定后置，历史问题保留。

证据：[冻结源](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-geographic-maps-20261023/source-snapshot-v2.json)、[正式计划](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-geographic-maps-20261023/formal-plan-v2.json)、[验证清单](charts-c6-geographic-maps-validation.json)、[API](charts-c6-geographic-maps-api.txt)、[Windows九阶段](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-geographic-maps-20261023/windows/run-v2/formal-run-v2.json)/[Linux九阶段](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-geographic-maps-20261023/linux/run-v2/formal-run-v2.json)；[Windows数值](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-geographic-maps-20261023/windows/run-v2/maps/independent-numeric-result.json)/[Linux数值](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-geographic-maps-20261023/linux/run-v2/maps/independent-numeric-result.json)、[Windows像素](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-geographic-maps-20261023/windows/run-v2/maps/independent-pixel-result.json)/[Linux像素](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-geographic-maps-20261023/linux/run-v2/maps/independent-pixel-result.json)、[Windows来源](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-geographic-maps-20261023/windows/run-v2/maps/independent-metadata-result.json)/[Linux来源](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-geographic-maps-20261023/linux/run-v2/maps/independent-metadata-result.json)、[开发失败索引](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-geographic-maps-20261023/development-evidence-index-v2.json)。
