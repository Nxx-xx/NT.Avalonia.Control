# Charts C5-05：进阶比较

执行日期：2026-10-21（香港时间）。同一冻结候选 `c5-advanced-comparison-source-v1` 在 Windows 本地原生合成窗口与 Ubuntu26.04 WSL2/WSLg 完成软件验证。本轮交付 CP01/02/03/04/06/07/08：发散条、哑铃、Mekko、镜像条、人口金字塔、龙卷风与 Venn；CP05 Parliament 仍属于 C6，没有加入本轮。下一主项 **C6-01 层级和打包**，完整 C5、剩余统计目录及官方运行时/API/二进制等价继续开放。

## 功能与数学契约

- `ChartAdvancedComparison` 提供可取消、有界、不可变的纯数值布局；七种控件继承 `ComparisonChartBase` / `EngineeringChartBase`，支持类型数据与属性路径、`IdPath`、可观察集合、嵌套段更新、独立原始来源、鼠标/键盘选择、缩放平移与 PNG/JPEG 导出。绘图序号与源索引分开；排序、resize、换源与多标记来源不会把选中的右端点/子段静默换成同源的第一个标记。
- Diverging 的基线始终在中间，`Zero=.5`，端点 `.5+.5×(Value−Baseline)/maxAbs`；正负共用幅值尺度，同号数据的一侧留空。水平/垂直方向、基线、条高、正负画刷、标签及排序可配。非有限值或不可表示的基线差有界失败；全零保留空场景。
- Dumbbell 的低/高端点共用包含零的数值域，允许端点逆序与相等；连接线为装饰，不冒充第三条数据。两个端点分别保留原始行与角色；同坐标的高端点按绘制顺序优先命中，键盘或 `SelectedIndex` 仍可分别选择两个端点。
- Mirror、Population、Tornado 使用同一非负幅值尺度和可配置中心空隙，两侧均返回正的原始值。人口图保持输入年龄顺序并有独立垂直 `BarGap`；龙卷风默认按左右总影响稳定降序，平局保持原始顺序。双侧刻度显示原始幅值，不把左侧几何位置当成负业务量。
- Mekko 按所有列宽权重归一化 X、在每列内独立归一化段高并自下向上堆叠；0权重产生空几何，负值拒绝。`ColumnGap=0` 时矩形面积代表列宽份额×段内份额；默认4DIP间距用于可读性，会减少实际着色面积。每个单元保留原始列和嵌套段两个来源，段身份可通过 `SegmentIdPath` 绑定，不把计算出的比例当原始值。
- Venn 接受 **包含交集的汇总量**：mask1/2/4是单集合，3/5/6是两两交集，7是三集合交集；缺省高阶交集为0，非空输入必须提供全部单集合，重复mask、非法mask、负/非有限或包含关系矛盾拒绝。独占区域按 Möbius 扣除更高阶交集精确计算；区域 `Members` 是参与该计算的 inclusive 汇总来源，**不是集合的实际元素**。
- 两集合按解析圆相交面积求距离，单集合圆面积保持比例；三集合按两两目标距离放置第三圆，任意汇总量一般无法由三个圆精确表示，因此始终 `IsApproximate=true`。显示 `EstimatedValue`、最大独占面积偏差/最大单集合总量和2048/4096中点积分差。该积分差是估计，不能当严格误差界；若正区域没有可用内部则列入 `UnrepresentedRegionMasks`，不伪造可命中区域。极端相对量级不可表示时明确失败。
- 静态源与生成标记预算各一百万；控件把数据失败隔离为 `DataError`、清空旧scene并支持原集合修复后恢复。当前更新/布局可能同步全量映射与复制；没有声明后台/增量算法或持续吞吐资格。

Demo入口 **Charts → 进阶比较** 提供八面板（Venn双/三集合分别展示），可改排序、中心/列间距、标签、数据源和视图；选中读数展示原始来源，三集合近似误差有独立说明。Usage与功能/主题包消费者提供编译绑定、嵌套生命周期及公共API验证。

官方目录依据：[Diverging](https://docs.avaloniaui.net/controls/data-display/charts/comparison/diverging-bar-chart)、[Dumbbell](https://docs.avaloniaui.net/controls/data-display/charts/comparison/dumbbell-chart)、[Mekko](https://docs.avaloniaui.net/controls/data-display/charts/comparison/mekko-chart)、[Mirror](https://docs.avaloniaui.net/controls/data-display/charts/comparison/mirror-bar-chart)、[Population](https://docs.avaloniaui.net/controls/data-display/charts/comparison/population-pyramid-chart)、[Tornado](https://docs.avaloniaui.net/controls/data-display/charts/comparison/tornado-chart)、[Venn](https://docs.avaloniaui.net/controls/data-display/charts/comparison/venn-diagram-chart)。采用公开目录与属性入口对齐；本地 inclusive Venn、布局公式、边界和初值以上述契约为准，没有声明复制官方内部算法/默认行为。

![Windows进阶比较面板](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-advanced-comparison-20261021/windows/run-v1/verification/functional-v1/charts-light-comparison-light-board-overview.png)

[Linux暗色面板](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-advanced-comparison-20261021/linux/run-v1/verification/functional-v1/charts-light-comparison-dark-board-overview.png)。四张最终亮暗总览已实际查看并保留[SHA绑定布局审查](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-advanced-comparison-20261021/final-board-visual-review-v1.json)，像素数学门槛使用下述38张固定专图。

## Windows与Linux验证

| 项目 | Windows本地原生合成 | Ubuntu26.04 WSL2/WSLg |
| --- | --- | --- |
| Charts全套测试 | 2288/2288 | 2288/2288 |
| 完整原生Demo / 本轮比较检查 | 2300 / 440 | 2300 / 440 |
| 编译绑定Usage | 491 | 491 |
| 两类独立新cache包消费者，各 | 676 | 676 |
| 独立固定数值 / 全量规模诊断 | 45 / 2 | 45 / 2 |
| 数值谓词 / 像素谓词 / 来源谓词 | 1493 / 562 / 11992 | 1493 / 562 / 11992 |
| 本轮PNG / 完整Demo PNG | 40 / 905 | 40 / 905 |
| Release警告 / 错误 | 0 / 0 | 0 / 0 |

谓词是叶级校验，不与产品测试数相加。1624个冻结源文件的双平台副本与Windows短路径副本逐SHA一致，934个声明runtime输入在冻结与运行前后稳定；每平台九个监督阶段串行执行：完整Verify-Charts IncludeC1、两类包消费者、API、benchmark restore/build/run、数值/像素/元数据oracle。各阶段保留PID、退出码、超时、标准输出及错误；Windows后Linux执行，未将不同候选拼成通过结果。

项目cache初始为空，功能/主题消费者分别使用新cache；仅按SHA只读复用63个非产品项目依赖与14个非产品消费者依赖。未复用历史Charts包/DLL，未禁用NuGet审计或降低警告策略；Linux复用现有私有SDK、库和字体，没有安装系统包。

首产品观察前封存45组输入/异常、100k Diverging和100k Mekko源公式与f64输入SHA。Python独立重算中心基线、排序、共享尺度、列宽/段高与完整源序号；两圆使用解析lens，三圆使用独立adaptive Simpson积分核对面积，另独立重算中点积分差。普通连续量容差abs1e−8/rel1e−9；三圆面积abs.002、最大相对误差abs.00003在观察前固定。原始输入、索引、枚举顺序和计数精确检查；规模场景完整核对100k行与200k单元，聚合记录谓词不冒充30万独立产品测试。

像素协议固定420×280DIP、plot(24,24,372,232)、七色、隐藏标签值与原始输入。九个fixture×两主题×native/export共36图，另Venn2 144DPI与无间距Mekko192DPI共38；18对96DPI逐RGBA零差异并保留diff。6×6DIP数学ROI至少6个指定色像素，颜色容差24、alpha至少250，DPI按面积缩放；每张独立非填充空白控制为0色像素。Venn ROI由原始汇总量推圆并用独立160网格找exclusive内部，没有取产品几何边界/标记坐标当期望。

元数据oracle严格比较所有原始DTO字段、父列/子段、StableId、源索引、原始Point/Value及段份额；逐标记检查选择、顶层重叠端点命中、每个Venn独占区域与空白未命中，另核对圆、独占量和公开误差。工具合成正负guard数为数值21、像素15、元数据/路径16；这些不是产品或真实窗口测试。

本地公开类型243→268，两平台逐声明一致，全部243个既有类型与公开/受保护成员保留。这是本地源码兼容快照，不是官方API或二进制等价认证。

## 规模诊断与保留失败

| 固定负载 | Windows elapsed / 当前线程分配 | Linux elapsed / 当前线程分配 |
| --- | --- | --- |
| perf-diverging-100k | 9.568 ms / 14201448 B | 11.541 ms / 14195256 B |
| perf-mekko-100k | 39.955 ms / 30097744 B | 46.880 ms / 30097744 B |

计时为一次静态API调用，含产品快照/布局分配；源构造、JSON和输入SHA在计时之外。不包含p50/p95、持续UI渲染、实际进程/原生/GPU内存或长稳资格。本轮未重跑C4-08正式性能入口和C5-01/02/03/04独立oracle；完整功能测试与Demo包含旧图族，但历史诊断不能认证新图SLA。

开发期保留Headless下Venn组合几何命中错误及修复、Usage缺using与排序选择时序夹具、首次编译/窗口验证原日志。数值oracle-v2另有5项工具误报：连续解析面积20/40的Python整数literal误走离散精确比较，而产品仅有正常浮点roundoff。v3只对该expected面积显式float，使用已封abs1e−8/rel1e−9；原工具、原失败、同raw重验与四项连续/离散guard均保留，计数/SourceIndex仍精确。未修改输入、期望数学值或门槛，见[工具恢复说明](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-advanced-comparison-20261021/numeric-tool-recovery-v1.json)。

基线从extent改为真正中心、元数据按源码适配rawRows/config/weight均发生在首次对应观察之前，v1/v2协议和时间/hash保留于[首观察前契约历史](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-advanced-comparison-20261021/pre-observation-contract-history-v1.json)；不能把开发子集通过代替冻结候选验收。完整开发证据见[索引](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-advanced-comparison-20261021/development-evidence-index-v1.json)。

## 证据与剩余问题

- [冻结源](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-advanced-comparison-20261021/source-snapshot-v1.json)、[正式计划](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-advanced-comparison-20261021/formal-plan-v1.json)、[验证清单](charts-c5-advanced-comparison-validation.json)、[API](charts-c5-advanced-comparison-api.txt)。[Windows九阶段](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-advanced-comparison-20261021/windows/run-v1/formal-run-v1.json)、[Linux九阶段](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-advanced-comparison-20261021/linux/run-v1/formal-run-v1.json)；[Windows完整功能](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-advanced-comparison-20261021/windows/run-v1/verification/functional-v1/results.json)、[Linux完整功能](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-advanced-comparison-20261021/linux/run-v1/verification/functional-v1/results.json)。
- [Windows原始数值](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-advanced-comparison-20261021/windows/run-v1/advanced-comparison/raw.json)、[Linux原始数值](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-advanced-comparison-20261021/linux/run-v1/advanced-comparison/raw.json)；[Windows独立数值](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-advanced-comparison-20261021/windows/run-v1/advanced-comparison/independent-numeric-result.json)、[Linux独立数值](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-advanced-comparison-20261021/linux/run-v1/advanced-comparison/independent-numeric-result.json)；[Windows像素](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-advanced-comparison-20261021/windows/run-v1/advanced-comparison/independent-pixel-result.json)、[Linux像素](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-advanced-comparison-20261021/linux/run-v1/advanced-comparison/independent-pixel-result.json)；[Windows原始来源](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-advanced-comparison-20261021/windows/run-v1/advanced-comparison/independent-metadata-result.json)、[Linux原始来源](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-advanced-comparison-20261021/linux/run-v1/advanced-comparison/independent-metadata-result.json)。
- 三集合圆近似、不可表示区域、非圆集合几何、任意集合数和真实元素集合输入仍需另定范围；Mekko间距会减少着色面积；人口排序按调用方输入年龄序。统一Cartesian多轴/Trackball/标注/动画的完整专业集成仍开放，不把基础继承交互扩大为完整C2资格。
- 已验证普通ObservableCollection/INPC更新与正常卸载/重挂；自定义INPC/INCC事件add/remove访问器若在订阅操作中主动重入换源或卸载，该极端生命周期边界尚未硬化和验证，不宣称任意事件访问器均完全兼容。
- 剩余ST01/03/06–10等统计、完整C5及官方运行时/API/二进制对照继续开放。下一主项C6-01，Parliament仍在C6-05。
- 所有PNG为原生窗口RenderTargetBitmap或导出，不认证物理输入、compositor present、GPU fence、原生Wayland、ARM、国产Linux、多屏DPI或实机读屏器；远端GitHub Actions run未执行。两类DPI抽样不是所有参数/主题/格式全排列。
- 百万点hit p95、A11Y-NATIVE-01按用户决定后置；AX06、四份历史PixelProbe DLL、旧动画时序/长路径/default-JIT问题保留。后台/增量比较布局、正式性能和30分钟/8小时长稳资格仍开放。
