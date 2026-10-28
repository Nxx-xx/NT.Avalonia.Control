# Charts 官方功能覆盖矩阵

## 当前进度与下一步

更新日期：2026-10-28。

**当前进度**：0.2.0-preview.3首版范围已冻结。Windows/Linux各3267项回归、三包干净原生消费者、目录部署、字体启动、Usage/Demo和各100轮生命周期均已通过。8小时长稳状态：Windows passed / Linux running。尚未登记发布就绪。官方功能对照跳过，官方性能对照、散点深度优化和SDK扩展后置。见[首版发布收尾](charts-preview-release.md)。

**前一批路径调查（历史记录）**：C7-03 完成三种减少散点原生绘制次数的组件试验；Windows/Linux均在首例出现像素差异，全部拒绝，未接入产品、未运行失败候选的性能测试。独立图片/身份审计394项通过，294个产品输入保持B18。另完成透明目标代理核查，双端均被SDK内部位图契约拒绝。散点积压仍未解决，完整性能基线不变。见[路径绘制试验](charts-c7-scatter-path-work.md)和[验证记录](charts-c7-scatter-path-work-validation.json)。

**下一步**：等待Linux实际跑满8小时并完成独立判定；失败则保留证据、修复后重新验收，未完成前不登记通过。原C7完整对标状态及历史门禁保留，暂不远程发布。

**前一批下一步（历史记录）**：目标接管路线需要先隔离验证保留原生位图身份、可靠暴露活动目标状态的SDK扩展；普通公开代理及三种路径合并均已有拒绝证据。继续保持Render、画质和原门槛，未证明安全与净收益前不接入。官方、present和用户后置专项状态保持。

**前一批小视口调查（历史记录）**：C7-03 小视口候选已拒绝并恢复基线。双端各3277项测试及232组保真通过；局部创建较快、分配较少，但未形成稳定端到端收益，散点积压仍未解决。原画质、Render、保守账本和门槛保持，完整性能基线仍为B18。见[小场景原生计划记录](charts-c7-small-native-plan.md)和[机器记录](charts-c7-small-native-plan-validation.json)。

**前一批下一步（历史记录）**：按本轮最终实测处理尚未通过的散点固定负载与尾部波动；phase2/3先解决真实SDK图层/合成语义可行性。官方许可、present与用户后置专项继续保留状态。

**前一批散点几何调查（历史记录）**：C7-03 完成 B18 散点成本归因、共享原生几何候选双平台各3275项测试/232组保真及各三对30秒原协议诊断。候选没有稳定净收益，已拒绝并恢复B18产品源码；本轮不登记新的提速或完整formal资格。Oval fill双端净成本也未显示稳定收益，保持独立探针、不接入产品。详见[散点几何调查](charts-c7-scatter-geometry.md)和[机器记录](charts-c7-scatter-geometry-validation.json)。

**前一批下一步（历史记录）**：先调查小于2048点场景中尚未使用的native计划预备（Bounds/Pen），处理前需保持资源预留、共享历史与退休契约；该候选尚未实现或接纳。继续按完整净成本筛选积压优化，保持画质、Render与原门槛。B18性能基线、官方许可/present和后置硬件及辅助专项状态不变。

**前一批内存与CI工具（历史记录）**：C7-03 新增匹配 empty/full 的双平台进程内存采样、独立 oracle 和手动原协议 CI 入口；双端三对内存诊断及本地94项契约通过。新散点顺序提交的 Linux 配对出现退化，未接入产品。产品绘制、原协议和门槛保持不变，C7-03 仍开放。见[内存与CI记录](charts-c7-qualification-tools.md)和[机器记录](charts-c7-qualification-tools-validation.json)。

**前一批记录分配与柱形优化（历史记录）**：C7-03 本轮定位公开 DrawingGroup 记录分配，拒绝3个不保持语义/画质的输入几何替换；已按明确审核决定接纳柱形源替换/布局优化，完成双平台各107进程同协议正式复测。按原协议预热后，柱形源替换请求至受控栅格完成的P95稳定降低，全体请求P95表现混合，不能扩展为所有请求或图族统一提速。原 Render、画质、协议和门槛保持，C7-03 不结项。见[记录分配与柱形复测](charts-c7-record-allocations.md)和[验证记录](charts-c7-record-allocations-validation.json)。

**最近一次完整原协议复测（记录分配与柱形批次）**：Windows 散点4/5、pending 161；柱形5/5、pending 0；Linux 散点0/5、pending 1040；柱形5/5、pending 0。完整性、命中、图片与负载门禁分别保留，未取得真实present、匹配峰值内存或官方倍率资格。

**前一批运行时诊断（历史记录）**：C7-03 完成 Windows 五轮 CPU/GC/UI 调度旁路诊断与相同请求/CPU位置关联，以及同源 SKPicture 双平台各73例保真、各三对N10净成本测试。新慢轮伴随线程运行成本和CPU位置分布变化，不能据此回推B17历史唯一原因；Windows配对一轮退化，未建立跨平台稳定净收益，未接入产品。Render、原画质、协议和门槛保持不变。见[运行时诊断与Picture试验](charts-c7-runtime-picture.md)和[机器记录](charts-c7-runtime-picture-validation.json)。

**前一批调查（B17，历史记录）**：C7-03 保持现有 Render 调用方式，并行原型按用户决定暂不接入。本轮排除无稳定收益或不满足语义的加速方案，未接受新的产品绘制优化；按未修改协议完成 Windows / Ubuntu 26.04 WSLg 各 107 进程复测。两端证据完整性、命中及可比状态图片通过，但固定负载仍失败：散点排空后 pending 为 Windows 878 / Linux 1420（各计划 9000），Windows 柱形另有 223，C7-03 不结项。完整结果及剩余问题见[散点候选与复测报告](charts-c7-scatter-followup.md)和[验证 JSON](charts-c7-scatter-followup-validation.json)。

**前一批完整正式复测（B17，历史记录）**：5 个交互、2 个校准和 100 个正式首次可用进程/平台全部保留，原 oracle 均核验 107/107。散点测量轮 Windows 4/5、Linux 0/5；柱形 Windows 4/5、Linux 5/5。279 对首图/相同状态图片全部一致；不同末态的 5 个比较单独保留。百万点首次可用软件诊断 P95 为 Windows 1357.368 / Linux 1303.189201 ms，仍在 3000 ms 名义诊断目标内。没有真实 present、峰值内存或官方倍率资格；产品未变化，轮次差异不登记为优化收益。

**前一批诊断（B11，历史记录）**：C7-03 本轮完成 Linux 原生热点定位、生产坐标 / 路径结构复核和 Windows / Linux 初始化交替配对，未显示稳定的跨平台收益，未接受新的产品优化。新增采样解析器及 CI 单元测试步骤，本地 12 项合成测试通过，远程 CI 未运行。画质、协议与门槛保留，C7-03 仍未完成。结果及剩余问题见[原生散点热点诊断](charts-c7-native-hotspots.md)与[验证 JSON](charts-c7-native-hotspots-validation.json)。

**已接受优化与复测（B8，历史记录）**：按原协议、数据、画质与门槛完成 Windows / Ubuntu 26.04 WSLg 各 107 进程复测。Windows 柱形固定负载积压由 563 降至 0；Windows / Linux exact 散点由 3786 / 3455 降至 235 / 1028，整体固定负载仍未通过，C7-03 不结项。双平台证据完整性与命中门禁通过，各 3256 项全量测试及 232 幅专项图片通过；加上 240 幅正式首图，共 704 组同平台新旧图片字节一致。本批冻结 v3 双副本各 2121 个输入文件，核验 214 个进程的可选后端身份。详见[积压优化报告](charts-c7-backlog-optimization.md)及[验证记录](charts-c7-backlog-optimization-validation.json)；B6/B7 历史证据保留。

B8 历史百万点首次可用受控诊断 P95 为 Windows 1189.5808 / Linux 1335.665949 ms，较 B7 的 1130.7928 / 1292.219544 ms 上升；3000 ms 软件诊断名义目标仍满足。此诊断不能代替正式 render/present 或峰值内存资格；官方无许可仍为 `blocked-reference`，正式 frame/present、峰值内存及目标硬件资格保持未取得。

**前一批下一步（历史记录）**：优先解决原协议散点积压与尾部波动；在具备运行条件时手动执行新CI并保留失败证据。采样内存最大值与真实峰值SLA分开判断，官方许可、present及用户后置项目继续保留状态。

**前一批下一步（历史记录）**：按实测表继续处理开放轨道、尾部波动和未完成门槛，已通过的软件范围推进长稳与发布准备，保留公开DrawingGroup独立可变输出与活画刷语义。官方许可、实机/GPU/物理触控和已后置无障碍专项保持开放；以本轮实际门禁结果为准。

**前一批下一步（历史记录）**：继续优先解决原生散点栅格成本和公开DrawingGroup逐点记录分配，保留执行CPU观察及独立可变导出语义；新的候选须先证明稳定净收益，再投入缓存所有权/退休工程并运行产品保真和原协议配对。B17仍是最近完整formal，散点与Windows柱形问题开放；官方许可和已后置硬件/辅助功能专项状态不变。

后续完整原生向量复用完成双平台 30 秒配对及软件回归：密集源替换栅格分配约少 80 KB，但 Windows 三组总时长均上升，Linux 积压变化混合。该方案已拒绝并恢复 B8；本批只新增诊断证据，不改变功能覆盖或正式性能资格。独立记录/Skia 成本定位与剩余工作见[完整向量复用试验](charts-c7-vector-reuse.md)。

## 核验口径与当前基线

官方事实来自公开产品页、Charts 总览、各分类侧栏及相关正文。下面的“差距与交付项”和 C0–C7 阶段是本仓库的自主实现计划，不是官方实现细节或官方性能承诺。访问日期均为 **2026-10-15**；网站会继续变动，发布前须重新核验。

公开目录包含独立控件、宿主中的系列、算法变体和组合示例，不能全部当作独立图表类型相加。尤其 Charts 总览正文并未列全 Cartesian 侧栏；侧栏还包含 Dot plot、Lollipop、Range bar、Spline area、Stacked 100% bar、Variance 等项。Scatter 正文另有 Scatter line 变体。官网“70+”是产品描述，本计划不以凑满该数字验收。[官方产品页](https://avaloniaui.net/charts)、[官方总览](https://docs.avaloniaui.net/controls/data-display/charts)、[Cartesian 分类完整侧栏](https://docs.avaloniaui.net/controls/data-display/charts/cartesian/area-chart)。

状态定义：

- **已实现**：该行明确限定的基础行为已有代码与回归证据；不自动表示相关官方属性全部覆盖。
- **部分**：已有可运行子集，但缺少这一官方功能的关键数据契约、模式或视觉行为。
- **未实现**：本仓库尚无对应能力，需开发和验收。
- **需核验**：公开文档不足以确认精确语义、存在与否或性能；C0 先补证据，不能当作完成。

| 阶段 | 本矩阵中的含义 |
| --- | --- |
| C0 | 固定版本和功能基线，澄清文档空白，建立官方对照数据与场景 |
| C1 | 公共契约、数据适配、轴系统和 MVVM |
| C2 | 交互、图例/标签/标记、模板、分析叠加层以外的共用视觉、导出与可访问性基础 |
| C3 | 常用 Cartesian、圆形、极坐标、气泡、仪表和小图 |
| C4 | 可见区裁剪、增量管线、抽样、命中索引、缓存与性能证据 |
| C5 | 统计、金融、工程与进阶比较图，以及相关数值算法 |
| C6 | 层级关系、地图、运营排程与分析组件 |
| C7 | Windows/Linux 实机兼容、性能对照、可访问性和发布验收 |

## 图表、系列与组合目录

每个表覆盖对应官方分类侧栏中的图表条目；表内再展开已确认的系列或模式。只有正文明确给出类型名时才列 API 名称，其他条目使用官方图表名称，不推测其内部继承关系。所有系列最终还须通过后面的共用能力矩阵和 C4、C7 门槛。

### Cartesian

来源：[完整分类侧栏及 Area](https://docs.avaloniaui.net/controls/data-display/charts/cartesian/area-chart)、[Bar](https://docs.avaloniaui.net/controls/data-display/charts/cartesian/bar-chart)、[Line](https://docs.avaloniaui.net/controls/data-display/charts/cartesian/line-chart)、[Scatter 与 Scatter line](https://docs.avaloniaui.net/controls/data-display/charts/cartesian/scatter-chart)、[Combo](https://docs.avaloniaui.net/controls/data-display/charts/cartesian/combo-chart)。访问：2026-10-15。

| ID | 官方图表 / 系列 / 模式 | 状态 | 差距与自主交付项 | 阶段 |
| --- | --- | --- | --- | --- |
| CT01 | Line / `LineSeries` | 部分 | C3-01 已增 Linear/Smooth/StepBefore/StepAfter/StepCenter、正负堆叠/百分比、原始身份与几何读数；逐点样式及完整官方 API 覆盖仍待补 | C1、C2、C3 |
| CT02 | Bar / `BarSeries`，单系列及多系列并列 | 部分 | C3-02 已增横竖分组、正负/基线、相对与固定DIP宽度、自动类别值排序、RawPoint身份及原始值标签；圆角、独立柱描边、完整逐点颜色/样式和官方API覆盖仍开放 | C1、C2、C3 |
| CT03 | Scatter / `ScatterSeries` | 部分 | C3-03 已补SizePath/ColorPath、面积/直径DIP尺度、固定/自动输入域、数值渐变/直接颜色和绘制顺序，真实XY/源身份及十种marker底座保留；大小/颜色图例、完整官方API/性能与设备资格仍开放 | C1、C2、C3 |
| CT04 | Scatter line / `ScatterLineSeries` | 部分 | C3-03 本库独立ScatterLineSeries固定linear源序连接非单调/重复X，ShowLine可关，XY缺失断线而无效大小只隐藏marker，参与segment/Trackball及大小/颜色编码；完整官方API/性能和物理资格仍开放 | C3 |
| CT05 | Area / `AreaSeries` | 部分 | C3-01 已增可配 Baseline 的五种路径填充、正负/缺失段、多轴/断轴与导出；默认零基线支持 log 原点回退，非零域外下界留 Gap，完整官方属性与设备资格仍开放 | C3 |
| CT06 | Spline / `SplineSeries` | 部分 | 本库以 LineSeries.Interpolation=Smooth 提供投影空间保形 Hermite、端点/原始身份及真曲线命中/裁剪；未声明独立同名系列或完整官方 API 兼容，性能待 C4 | C3 |
| CT07 | Spline area / `SplineAreaSeries` | 部分 | 本库 AreaSeries.Interpolation=Smooth 与 RangeAreaSeries 复用曲线填充；顶点共同切线限幅保持 C1 和同符号厚度，兼容堆叠共边；异构拓扑与完整官方属性待补 | C3 |
| CT08 | Step line / `StepLineSeries` | 部分 | 本库 LineSeries.Interpolation 三种阶梯已实现源起点/末点/屏幕X中点垂直变化、缺失段及同路径读数；以自有 API 提供，不声明独立同名系列兼容 | C3 |
| CT09 | Stacked bar / `StackedBarSeries` | 部分 | C3-02 以BarSeries.StackMode=Stacked提供横竖正负独立累计、组/轴/方向/基线隔离、显隐重算与原贡献；不声明官方独立同名系列或完整API兼容，性能/设备资格仍开放 | C3 |
| CT10 | Stacked 100% bar / `Stacked100PercentBarSeries` | 部分 | C3-02 以BarSeries.StackMode=Percent按正负分别归一±100，零量为0、零基线约束、组隔离及显示/原始份额分离；正式双平台软件证据已记录，完整官方API/性能资格仍开放 | C3 |
| CT11 | Stacked area / `StackedAreaSeries` | 部分 | C3-01 线/面积支持正负独立累计，非零正/负总量各归一到±100、零量为0，组/轴/模式隔离及显隐重算；兼容X/插值/有效拓扑共边与厚度已验，异构连续共边仍开放 | C3 |
| CT12 | Area range / `AreaRangeSeries` | 部分 | 本库 RangeAreaSeries 使用 HighPath/LowPath、规范化显示上下界而保留源字段、逐界空点策略与五种路径；样本以上边界命中/浏览并带 BaseValue，下界不单独建点，拒绝 CategoryY | C1、C3 |
| CT13 | Range bar / `RangeBarSeries` | 部分 | C3-02 本库RangeBarSeries绑定High/Low无需ValuePath，横竖显示规范化两界并保留原值/身份、区间命中和标签/提示底座；专属双界模板及完整官方属性仍开放 | C1、C3 |
| CT14 | Waterfall / `WaterfallSeries` | 部分 | C3-02 已有横竖源序增减累计、缺失断连、布尔TotalPath总计/重置、正负/总计颜色及可配连接线；不支持显示排序，完整官方API及物理/性能资格仍开放 | C3 |
| CT15 | Histogram / `HistogramSeries` | 基础已实现 | 固定箱数/宽度/显式非等宽边界、末端右闭、Count/Probability/Density及原始bin命中；双平台独立数值/像素通过，规模及官方等价仍开放 | C5 |
| CT16 | Pareto | 部分 | C3-02 本库ParetoSeries按非负有限值稳定降序，源身份与显示rank分开，正总量末项累计100%、全零累计0，使用自动或指定独立线性轴；负/缺失Gap、累计线无第二业务身份，完整官方属性/性能仍开放 | C3 |
| CT17 | Dot plot / `DotPlotSeries` | 部分 | C3-02 本库DotSeries为独立类别点系列，支持横竖、marker大小/形状、显示排序与源身份；以自有API提供，完整官方DotPlotSeries属性/逐点编码未覆盖 | C3 |
| CT18 | Lollipop / `LollipopSeries` | 部分 | C3-02 已有横竖点与基线细杆、宽度/排序、点/杆命中、原身份与标签/导出底座；逐点样式及完整官方API与性能资格仍开放 | C3 |
| CT19 | Variance / `VarianceSeries` | 部分 | C3-02 以High/Low有向差的双端点与细杆表示横竖比较，正负填充可配置、原始上下界/身份保留；官方参考基线/柱体变体及完整逐点样式仍有兼容差距 | C3 |
| CT20 | Combo：混合系列共用主轴 | 部分 | C3-01扩展line/bar/scatter/area/range，C3-02加入比较系列，C3-03加入编码Scatter/ScatterLine/XY Bubble及共用身份、选择/标签/动画/导出；混合renderer保留各自命中距离协议，后续图族与完整组合目录继续验收 | C1、C3 |
| CT21 | Combo：次 Y 轴及系列轴归属 | 部分 | 独立轴范围/Key、Near/Far、读数/命中及视图同步已有；C3-02 Pareto新增独立线性累计百分比轴，比较系列沿用显式轴/断轴；后续图族及完整组合目录仍开放 | C1、C2、C3 |

堆叠模式的直接依据：[Stacked bar](https://docs.avaloniaui.net/controls/data-display/charts/cartesian/stacked-bar-chart)、[Stacked 100% bar](https://docs.avaloniaui.net/controls/data-display/charts/cartesian/stacked-100-percent-bar-chart)、[Stacked area](https://docs.avaloniaui.net/controls/data-display/charts/cartesian/stacked-area-chart)。进阶项依据：[Range bar](https://docs.avaloniaui.net/controls/data-display/charts/cartesian/range-bar-chart)、[Area range](https://docs.avaloniaui.net/controls/data-display/charts/cartesian/range-area-chart)、[Dot plot](https://docs.avaloniaui.net/controls/data-display/charts/cartesian/dot-plot-chart)、[Lollipop](https://docs.avaloniaui.net/controls/data-display/charts/cartesian/lollipop-chart)、[Variance](https://docs.avaloniaui.net/controls/data-display/charts/cartesian/variance-chart)、[Waterfall](https://docs.avaloniaui.net/controls/data-display/charts/cartesian/waterfall-chart)、[Histogram](https://docs.avaloniaui.net/controls/data-display/charts/cartesian/histogram-chart)、[Pareto](https://docs.avaloniaui.net/controls/data-display/charts/cartesian/pareto-chart)。访问：2026-10-15。

### Bubble、Circular 与 Radial

来源：[Bubble 分类侧栏](https://docs.avaloniaui.net/controls/data-display/charts/bubble/bubble-chart)、[Circular 分类侧栏](https://docs.avaloniaui.net/controls/data-display/charts/circular/pie-chart)、[Radial 分类侧栏](https://docs.avaloniaui.net/controls/data-display/charts/radial/nightingale-rose-chart)、[Polar 系列](https://docs.avaloniaui.net/controls/data-display/charts/radial/polar-chart)、[Radar 系列](https://docs.avaloniaui.net/controls/data-display/charts/radial/radar-chart)。访问：2026-10-15。

| ID | 官方图表 / 系列 / 模式 | 状态 | 差距与自主交付项 | 阶段 |
| --- | --- | --- | --- | --- |
| BC01 | Bubble：XY + 大小编码 | 部分 | C3-03 本库BubbleSeries以SizePath优先/WeightPath兼容映射DIP直径，Area/Diameter尺度、零/负/缺失规则、颜色编码和重叠绘制/可见形状命中已实现；大小/颜色图例、官方完整API及性能/物理资格仍开放 | C1、C3 |
| BC02 | Bubble cloud | 基础已实现 | 有界源序气泡/标签/命中/原始面积比，双平台软件验证；完整专业扩展/正式性能/官方等价开放 | C6 |
| BC03 | Packed bubble | 基础已实现 | 稳定降序打包/面积/来源/类别与相对大小图例；vendor尺寸默认开放，双平台软件验证；完整专业扩展/正式性能/官方等价开放 | C6 |
| CC01 | Pie | 部分 | C3-04 本库CircularChart/PieSeries已补份额、零/负值Ignore/Absolute/Throw、间隔、小扇区Keep/Hide、源身份选择与过渡；完整官方API、进阶图例/模板、辅助语义及性能/物理资格仍开放 | C3 |
| CC02 | Donut | 部分 | C3-04 DonutSeries已补比例内外半径、中心文字/内容、环段命中、分离、部分弧和独立多环；完整官方API、图例/模板及设备资格仍开放 | C3 |
| CC03 | Semi-donut | 部分 | C3-04 SemiDonutSeries默认180°上半环，支持起始/总角度、方向、外标签连线及布局；拥挤标签按有限空间省略，完整目录变体、辅助语义/性能及物理资格仍开放 | C3 |
| RD01 | Polar / `PolarLineSeries` | 部分 | 已补数值角度/半径、PolarLine/Scatter/Filled、度/弧度/方向/起点/部分周期、闭合/缺失、圆形/多边形网格和标记；正式双平台软件验证通过。完整轴、官方全部属性/组合、性能与设备资格待补 | C1、C3 |
| RD02 | Radar / `RadarSeries` | 部分 | 已补 RadarChart、多系列首见类别并集、重排同名射线、轮廓/填充、网格、MaxValue 和源身份选择；正式双平台软件验证通过。进阶模板/布局、完整官方契约和性能/设备资格待补 | C3 |
| RD03 | Polar area | 部分 | 已补等角扇区、环形面积正比数值、内半径/角间隙、零负缺失策略、基本标签/选择/导出；完整标签布局/模板、官方全部属性与性能待补 | C3 |
| RD04 | Nightingale rose | 部分 | 已补线性半径编码并与 PolarArea 面积语义分开验收，支持内半径/角间隙与源身份；方向分箱/堆叠和高级布局仍待后续 | C3 |
| RD05 | Radial bar | 部分 | 已补共享类别同心轨道、弧长比例、内孔/间隙、身份选择和导出；完整官方轨道/标签/模板组合、性能与设备资格待补 | C3 |
| RD06 | Radial line | 部分 | 已补共享类别径向折线、源序断点/闭合、圆形网格、周边类别文字、标记/选择/导出；完整文字布局/轴/C2 适配与性能待补 | C3 |

### Comparison

来源：[Comparison 完整分类侧栏](https://docs.avaloniaui.net/controls/data-display/charts/comparison/diverging-bar-chart)。访问：2026-10-15。以下是逐项范围，不从现有正负竖柱推断其横向布局已经完成。

| ID | 官方图表 / 模式 | 状态 | 差距与自主交付项 | 阶段 |
| --- | --- | --- | --- | --- |
| CP01 | Diverging bar | 基础已实现 | 真正中心基线/共享幅值、正负/标签与双方向、原始身份，双平台软件验证；官方等价/专业扩展/规模资格开放 | C5 |
| CP02 | Dumbbell | 基础已实现 | 低高双端点/连接、逆序/同点顶层角色和分别选择，双平台软件验证；官方等价/专业扩展/规模资格开放 | C5 |
| CP03 | Mekko | 基础已实现 | 列宽×段高归一化、gap0面积份额、父子来源/嵌套更新，双平台软件验证；官方等价/专业扩展/规模资格开放 | C5 |
| CP04 | Mirror bar | 基础已实现 | 共享非负双侧尺度、中心空隙、双侧原值刻度，双平台软件验证；官方等价/专业扩展/规模资格开放 | C5 |
| CP05 | Parliament | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7 | C6 |
| CP06 | Population pyramid | 基础已实现 | 输入年龄顺序、非负左右组、共享尺度/独立BarGap，双平台软件验证；官方等价/专业扩展/规模资格开放 | C5 |
| CP07 | Tornado | 基础已实现 | 左右总影响稳定降序、共享尺度/来源选择，双平台软件验证；官方等价/专业扩展/规模资格开放 | C5 |
| CP08 | Venn diagram | 基础已实现 | inclusive mask契约、exclusive汇总依赖、2圆解析/3圆近似及公开误差，双平台软件验证；官方等价/专业扩展/规模资格开放 | C5 |

### Statistical 与 Engineering

来源：[Statistical 完整分类侧栏](https://docs.avaloniaui.net/controls/data-display/charts/statistical/beeswarm-plot-chart)、[Engineering 完整分类侧栏](https://docs.avaloniaui.net/controls/data-display/charts/engineering/carpet-plot-chart)。访问：2026-10-15。算法选型、误差界及确定性是本仓库待制定的验收要求，官方目录名称本身不能提供这些保证。

| ID | 官方图表 / 系列 | 状态 | 差距与自主交付项 | 阶段 |
| --- | --- | --- | --- | --- |
| ST01 | Beeswarm | 基础已实现 | 分类原始点、空间碰撞检查、有界工作量及拥挤回退诊断；双平台软件验证与原始来源通过，专业/官方资格留C7，见[C5补齐报告](charts-c5-completion.md) | C5 |
| ST02 | Box plot | 基础已实现 | 原始/预聚合、Type7、IQR须/原始离群序号、均值及组命中；横竖/缩放/导出双平台验证，其他约定/官方等价开放 | C5 |
| ST03 | Contour plot | 基础已实现 | 完整矩形标量网格、等值线/填色、鞍点拓扑及NaN掩膜；双平台软件验证与原始来源通过，专业/官方资格留C7，见[C5补齐报告](charts-c5-completion.md) | C5 |
| ST04 | Density plot | 基础已实现 | Gaussian KDE显式/自动Silverman带宽、有限域/梯形积分、不强制重归一；二维/加权/边界校正与规模资格开放 | C5 |
| ST05 | Error bar / `ErrorBarSeries` | 基础已实现 | 对称/非对称/单侧误差、横竖DIP端帽、原始源点命中及normal-z辅助；多轴/导出双平台验证，官方等价开放 | C1、C5 |
| ST06 | Mosaic | 基础已实现 | 双分类列联比例、重复单元聚合及全部原始成员；双平台软件验证与原始来源通过，专业/官方资格留C7，见[C5补齐报告](charts-c5-completion.md) | C5 |
| ST07 | Parallel coordinates | 基础已实现 | 逐维尺度/反向/clamp/缺失断线、业务轴绑定及精确点线命中；双平台软件验证与原始来源通过，专业/官方资格留C7，见[C5补齐报告](charts-c5-completion.md) | C5 |
| ST08 | Ridgeline | 基础已实现 | 原始样本KDE与预计算曲线、共同数值/绝对密度尺度及重叠；双平台软件验证与原始来源通过，专业/官方资格留C7，见[C5补齐报告](charts-c5-completion.md) | C5 |
| ST09 | Strip plot | 基础已实现 | 分类原始点、固定seed/index散列jitter及均值；双平台软件验证与原始来源通过，专业/官方资格留C7，见[C5补齐报告](charts-c5-completion.md) | C5 |
| ST10 | Violin plot | 基础已实现 | 对称密度轮廓、Type7分位/箱体、小样本规则及原始样本序号；双平台软件验证与原始来源通过，专业/官方资格留C7，见[C5补齐报告](charts-c5-completion.md) | C5 |
| EN01 | Carpet plot | 基础已实现 | 完整矩形A/B网格、双线性Y、X=A+OffsetB、Jacobian唯一逆解；fold可显示/逆解拒绝，双平台验证；官方运行时等价/扩展资格开放 | C5 |
| EN02 | Hexbin | 基础已实现 | pointy axial/cube rounding、DIP半径、Count/Sum/Mean及全部原始成员；resize重建/viewport保留分箱，双平台验证；官方运行时等价/扩展资格开放 | C4、C5 |
| EN03 | Smith | 基础已实现 | 物理Ω/S、Z₀、被动阻抗/导纳反射、实虚网格与逆解；极点/圆外/不可表示拒绝，双平台验证；官方运行时等价/扩展资格开放 | C5 |
| EN04 | Ternary | 基础已实现 | max缩放三分量归一化、等边三角正逆投影、原始Item与比例区分，双平台验证；官方运行时等价/扩展资格开放 | C5 |
| EN05 | Wind rose | 基础已实现 | north-centered半开方向/速度档、frequency权重/百分比与原始Count、线性频数半径叠加，双平台验证；官方运行时等价/扩展资格开放 | C5 |

### Financial 与派生分析

来源：[Financial 分类侧栏](https://docs.avaloniaui.net/controls/data-display/charts/financial/candlestick-chart)、[Financial 宿主与叠加层](https://docs.avaloniaui.net/controls/data-display/charts/financial/financial-chart)、[Technical indicators](https://docs.avaloniaui.net/controls/data-display/charts/financial/technical-indicators-chart)、[Trendline 与均线系列](https://docs.avaloniaui.net/controls/data-display/charts/shared-elements/trendline-chart)。访问：2026-10-15。

官方正文明确 FinancialChart 可承载金融系列和兼容叠加系列；交易周期横向布局采用等槽距，不等同于普通日期轴的自然时间距离。该差异需要独立的数据和缩放验收。

| ID | 官方图表 / 系列 / 算法 | 状态 | 差距与自主交付项 | 阶段 |
| --- | --- | --- | --- | --- |
| FN01 | Financial chart 宿主 | 基础已实现 | FinancialChart共享显式UTC交易槽、价格/成交量多轴、精确日期overlay与未知日期gap；交易所时段/节假日及官方等价开放 | C1、C5 |
| FN02 | Candlestick / `CandlestickSeries` | 基础已实现 | 本库CandlestickSeries涨跌/平价蜡烛、有限OHLC校验、原始源项/RawPoint和导出；官方API/运行时等价开放 | C5 |
| FN03 | OHLC / `OhlcSeries` | 基础已实现 | 高低线、左开右收tick、UTC交易槽和原始源项提示；双平台数值/像素/包消费通过，官方等价开放 | C5 |
| FN04 | Heikin-Ashi | 基础已实现 | 明确首开/递推OHLC/gap重置与合成/原始值区分，100k全序列SHA独立核对；增量/正式性能开放 | C5 |
| FN05 | Hilo | 基础已实现 | HiloSeries原始High/Low线、完整价格bounds、日期槽/命中/多轴导出；高级布局与官方等价开放 | C5 |
| FN06 | Kagi | 基础已实现 | close-only绝对反转、shoulder/waist粗细/阈值拆段、来源范围和非时间槽；ATR/百分比/逐笔及正式性能开放 | C5 |
| FN07 | Renko | 基础已实现 | close-only固定箱宽、可配反转、同源多砖/原始区间、非时间槽及资源拒绝；ATR/百分比/wick/逐笔开放 | C5 |
| FN08 | Point and figure | 基础已实现 | 固定箱/反转数、X/O列延伸/反转、来源范围及累计字母预算；高级箱规则/逐笔/正式性能开放 | C5 |
| AN01 | 附加 Trendline 与独立 `ChartTrendlineSeries` 两种接入 | 基础已实现 | 附加Trendline与独立ChartTrendlineSeries，源关联/自身映射、轴继承/显式覆盖、预测范围、图例与生命周期均有回归；官方API等价开放 | C1、C5 |
| AN02 | Linear 趋势线 | 基础已实现 | Linear最小二乘、完整历史拟合与显式取样/forecast；Count/Omitted/R²和合成身份明确 | C1、C5 |
| AN03 | Polynomial 趋势线 | 基础已实现 | degree1..10多项式，中心缩放HouseholderQR；大offset和秩亏回归，degree/采样预算有界 | C1、C5 |
| AN04 | Exponential 趋势线 | 基础已实现 | Exponential exp(a+bX)，logY最小二乘，Y≤0省略；R²原Y域，预测溢出拒绝 | C1、C5 |
| AN05 | Logarithmic 趋势线 | 基础已实现 | Logarithmic a+b lnX，X≤0省略且预测/采样域须正；原Y域R² | C1、C5 |
| AN06 | Power 趋势线 | 基础已实现 | Power exp(a)X^b，X/Y须正、双log拟合、原Y域R²和域/不足拒绝 | C1、C5 |
| AN07 | Moving average 趋势线 | 基础已实现 | MovingAverage趋势=源对齐尾随SMA，预热/gap重置，不接受任意X预测/forecast | C1、C5 |
| AN08 | `MovingAverageSeries`：Simple / SMA | 基础已实现 | MovingAverageSeries Simple/SMA，完整历史尾随窗口、源端点身份、通知和viewport外历史 | C1、C5 |
| AN09 | `MovingAverageSeries`：Exponential / EMA | 基础已实现 | MovingAverageSeries EMA，period点SMA种子与alpha2/(p+1)，gap重新预热 | C1、C5 |
| AN10 | `MovingAverageSeries`：Weighted / WMA | 基础已实现 | MovingAverageSeries WMA，最旧至最新权重1..period，源长度/顺序保持 | C1、C5 |
| AN11 | `MovingAverageSeries`：Triangular / TMA | 基础已实现 | MovingAverageSeries TMA，ceil(p/2)和floor(p/2)+1两SMA，奇偶均预热p−1 | C1、C5 |
| AN12 | `SMAIndicator` | 基础已实现 | 独立SMAIndicator的SourceSeries/参数/生命周期及源更新回归，非由均线系列自动通过 | C1、C5 |
| AN13 | `EMAIndicator` | 基础已实现 | 独立EMAIndicator，SMA种子/递推、SourceSeries更新/源身份和图例 | C1、C5 |
| AN14 | `WMAIndicator` | 基础已实现 | 独立WMAIndicator，权重/预热、SourceSeries关联/通知与图例 | C1、C5 |
| AN15 | `BollingerBandsIndicator` | 基础已实现 | BollingerBandsIndicator中轨SMA±总体SD倍数，上/下/中轨和带组件独立样式/图例/可见性、bounds和gap | C1、C5 |
| AN16 | RSI、MACD 等未在当前技术指标正文列出的算法 | 待专项核验 | 已核验的公开 API 目录未匹配RSI/MACD，不能据此证明授权runtime不存在；参考版本运行时/API专项核验或单列自主扩展继续开放，见[C5补齐报告](charts-c5-completion.md) | C0、C5 |

### Gauges

来源：[Gauges 完整分类侧栏](https://docs.avaloniaui.net/controls/data-display/charts/gauges/circular-gauge-chart)。访问：2026-10-15。

| ID | 官方图表 / 模式 | 状态 | 差距与自主交付项 | 阶段 |
| --- | --- | --- | --- | --- |
| GG01 | Circular gauge | 部分 | 已补圆弧轨道、线性业务刻度/针/目标/区间和导出，双平台软件验证通过；复杂刻度布局、完整官方 API/性能与物理资格待补 | C3 |
| GG02 | Gauge chart | 部分 | 已补独立半圆布局、指针/弧/目标/区间和实时更新；高级布局、全 C2/官方 API/性能资格待补 | C3 |
| GG03 | Gradient ring | 部分 | 已补共享域同心独立进度环、业务标签/梯度画刷、环间距与更新；复杂标签、完整官方组合/性能待补 | C3 |
| GG04 | Linear gauge | 部分 | 已补横竖轨道、反向、填充/指针/目标/阈值和刻度；复杂文字/区间布局、完整官方 API/性能待补 | C3 |
| GG05 | Liquid fill gauge | 部分 | 已补圆/矩形裁剪、液位、静态波形/显式动画/暂停/减少动态效果与当前帧导出；数值过渡/完整模板/性能待补 | C3 |
| GG06 | Progress donut | 部分 | 已补进度弧、上下限、目标、百分比/自定义文本和导出；复杂布局、完整官方 API/性能待补 | C3 |

### Analytics 与 KPI

来源：[Analytics 完整分类侧栏](https://docs.avaloniaui.net/controls/data-display/charts/analytics/bullet-chart)、[Theme river 组合](https://docs.avaloniaui.net/controls/data-display/charts/analytics/theme-river-chart)、[Pictorial bar](https://docs.avaloniaui.net/controls/data-display/charts/analytics/pictorial-bar-chart)、[官网 Stream graph 描述](https://avaloniaui.net/charts)。访问：2026-10-15。

| ID | 官方图表 / 系列 / 模式 | 状态 | 差距与自主交付项 | 阶段 |
| --- | --- | --- | --- | --- |
| AK01 | Bullet | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7 | C6 |
| AK02 | Bump | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7 | C6 |
| AK03 | Calendar heatmap | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7 | C1、C6 |
| AK04 | Funnel | 部分 | 已补源序正值阶段、颈宽/分段间隔、业务身份命中/选择/键盘/提示与导出，双平台软件验证通过；复杂标签/模板、完整 C2/官方 API/性能资格待补 | C3 |
| AK05 | Heatmap | 部分 | 已补类型化离散行列、颜色域/实际色标、缺失与重复末项规则、源项交互和导出；高级颜色轴/大矩阵虚拟化、完整 C2/性能资格待补 | C3、C4 |
| AK06 | KPI cards | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7 | C6 |
| AK07 | Matrix | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7 | C6 |
| AK08 | Pictorial bar / `PictorialBarSeries` | 部分 | 已补 Geometry 符号 Stretch、Clip、Repeat 三模式、方向/间距、真实形状与孔洞命中，接入 Cartesian 数据/轴/选择/导出；完整组合/官方 API/性能资格待补 | C3 |
| AK09 | Pyramid | 部分 | 已补有限正值按面积比例分层、源序/间隔、身份交互/标签和导出；复杂标签/模板、完整 C2/官方 API/性能资格待补 | C3 |
| AK10 | Slope | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7 | C6 |
| AK11 | Table chart | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7 | C6 |
| AK12 | Theme river（独立控件及堆叠组合） | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7；Centered/Zero，无wiggle | C3、C6 |
| AK13 | Waffle | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7 | C6 |
| AK14 | Word cloud | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7 | C6、C7 |
| AK15 | 官网 Stream graph 与 Theme river 的精确对应 | 待专项核验 | 已交付ThemeRiver控件和Centered/Zero；精确官方独立系列/API与额外算法对应仍在C7核验，不由AK12自动声明等价 | C0、C6 |

### Hierarchy、Flow 与 Network

来源：[完整分类侧栏](https://docs.avaloniaui.net/controls/data-display/charts/hierarchy/alluvial-chart)、[Flow chart](https://docs.avaloniaui.net/controls/data-display/charts/hierarchy/flow-chart)、[Mindmap 组合](https://docs.avaloniaui.net/controls/data-display/charts/hierarchy/mindmap-chart)、[Process flow 组合](https://docs.avaloniaui.net/controls/data-display/charts/hierarchy/process-flow-chart)。访问：2026-10-15。Mindmap 和 Process flow 的官方正文使用 FlowChart 组合，不能再计成两个独立基础引擎。

| ID | 官方图表 / 组合模式 | 状态 | 差距与自主交付项 | 阶段 |
| --- | --- | --- | --- | --- |
| HF01 | Alluvial | 基础已实现 | 显式Stage列、共享流量带/原始端点，双平台软件验证；完整专业扩展/正式性能/官方等价开放 | C6 |
| HF02 | Arc diagram | 基础已实现 | 原始线序/弧线/平行边自环/命中，双平台软件验证；完整专业扩展/正式性能/官方等价开放 | C6 |
| HF03 | Chord diagram | 基础已实现 | 有向端口/incident质量扇区/原始边，双平台软件验证；完整专业扩展/正式性能/官方等价开放 | C6 |
| HF04 | Circle packing | 基础已实现 | 子圆嵌套/原始叶面积/包络空隙/钻取，双平台软件验证；完整专业扩展/正式性能/官方等价开放 | C6 |
| HF05 | Dendrogram | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7 | C6 |
| HF06 | Flame graph | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7 | C6 |
| HF07 | Flow chart | 基础已实现 | 显式位置尺寸/五种形状/箭头/命名分组；任意组样式与节点模板开放，双平台软件验证；完整专业扩展/正式性能/官方等价开放 | C6 |
| HF08 | Force-directed graph | 基础已实现 | 确定性种子/有界迭代/固定节点/碰撞回退/取消，双平台软件验证；完整专业扩展/正式性能/官方等价开放 | C4、C6 |
| HF09 | Icicle | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7 | C6 |
| HF10 | Indented tree | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7 | C6 |
| HF11 | Mindmap / brainstorming：FlowChart 组合 | 基础已实现 | 双侧生成森林/中心主题/原始交叉边，双平台软件验证；完整专业扩展/正式性能/官方等价开放 | C6 |
| HF12 | Network | 基础已实现 | 圆周/固定位置/节点边身份与命中，双平台软件验证；完整专业扩展/正式性能/官方等价开放 | C6 |
| HF13 | Organization | 基础已实现 | 组织树/连接/双方向/折叠；任意节点模板开放，双平台软件验证；完整专业扩展/正式性能/官方等价开放 | C6 |
| HF14 | Process flow：FlowChart 组合 | 基础已实现 | 自动有向层级/步骤判断/返回边/箭头标签，双平台软件验证；完整专业扩展/正式性能/官方等价开放 | C6 |
| HF15 | Radial tree | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7 | C6 |
| HF16 | Sankey | 基础已实现 | 共享流量宽度/内部守恒/循环返回/重心排序，双平台软件验证；完整专业扩展/正式性能/官方等价开放 | C6 |
| HF17 | Sunburst | 基础已实现 | 叶合计扇环/内孔扫角/钻取祖先路径，双平台软件验证；完整专业扩展/正式性能/官方等价开放 | C6 |
| HF18 | Treemap | 基础已实现 | Squarify/Slice面积/极端比例拒绝/标签/钻取，双平台软件验证；完整专业扩展/正式性能/官方等价开放 | C6 |

### Maps

来源：[Maps 完整分类侧栏与 Bubble map](https://docs.avaloniaui.net/controls/data-display/charts/maps/bubble-map-chart)。访问：2026-10-15。空间数据加载、坐标转换和图层生命周期须作为独立契约；已有 XY 散点不表示地图已覆盖。

| ID | 官方图表 / 图层场景 | 状态 | 差距与自主交付项 | 阶段 |
| --- | --- | --- | --- | --- |
| MP01 | Bubble map | 基础已实现 | 原地图基础与Marker/Line/Vector/Pie、分类色、多座位选择通过双平台；fit/旧订阅问题关闭；瓦片/标签对比/模板/正式性能及官方等价开放，见[C6报告](charts-c6-main-completion.md) | C6 |
| MP02 | Choropleth map | 基础已实现 | 原地图基础与Marker/Line/Vector/Pie、分类色、多座位选择通过双平台；fit/旧订阅问题关闭；瓦片/标签对比/模板/正式性能及官方等价开放，见[C6报告](charts-c6-main-completion.md) | C6 |
| MP03 | Heatmap map | 基础已实现 | 原地图基础与Marker/Line/Vector/Pie、分类色、多座位选择通过双平台；fit/旧订阅问题关闭；瓦片/标签对比/模板/正式性能及官方等价开放，见[C6报告](charts-c6-main-completion.md) | C4、C6 |
| MP04 | Non-geographic / Seat map | 基础已实现 | 原地图基础与Marker/Line/Vector/Pie、分类色、多座位选择通过双平台；fit/旧订阅问题关闭；瓦片/标签对比/模板/正式性能及官方等价开放，见[C6报告](charts-c6-main-completion.md) | C6 |
| MP05 | Shape map | 基础已实现 | 原地图基础与Marker/Line/Vector/Pie、分类色、多座位选择通过双平台；fit/旧订阅问题关闭；瓦片/标签对比/模板/正式性能及官方等价开放，见[C6报告](charts-c6-main-completion.md) | C6 |

### Scheduling 与小图

来源：[Scheduling 完整分类侧栏](https://docs.avaloniaui.net/controls/data-display/charts/scheduling/timeline-chart)、[Sparkline 四种模式](https://docs.avaloniaui.net/controls/data-display/charts/scheduling/sparkline-chart)。访问：2026-10-15。

| ID | 官方图表 / 模式 | 状态 | 差距与自主交付项 | 阶段 |
| --- | --- | --- | --- | --- |
| SC01 | Gantt | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7 | C1、C6 |
| SC02 | Sparkline：Line | 部分 | 已补无轴源序微图、缺口、极值/首尾/标记、命中/业务身份、实时更新和尺寸约束；完整 C2/辅助语义/性能待补 | C3 |
| SC03 | Sparkline：Area | 部分 | 已补基线填充、正负值与缺口，不跨缺口连面；完整模板/官方 API/性能待补 | C3 |
| SC04 | Sparkline：Bar | 部分 | 已补源槽柱、正负基线/间距、缺失槽与极值强调；完整样式/官方 API/性能待补 | C3 |
| SC05 | Sparkline：WinLoss | 部分 | 已补相对基线的正/负/零固定符号格、原始幅值/业务身份与颜色；完整模板/辅助语义/性能待补 | C3 |
| SC06 | Spiral timeline | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7 | C6 |
| SC07 | Swimlane | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7 | C6 |
| SC08 | Event timeline | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7 | C1、C6 |

## 共用功能矩阵

### 契约、数据与扩展

来源：[官方总览：数据、公共属性、动画及扩展](https://docs.avaloniaui.net/controls/data-display/charts)、[Financial 扩展上下文](https://docs.avaloniaui.net/controls/data-display/charts/financial/financial-chart)。访问：2026-10-15。

| ID | 功能范围 | 状态 | 当前边界与自主交付项 | 阶段 |
| --- | --- | --- | --- | --- |
| DS01 | `ItemsSource` 绑定和集合更新 | 部分 | 已有非泛型源、INCC/嵌套INPC、批量通知和卸载释放；真正增量计算/实时预算待 C4 | C1、C4 |
| DS02 | 对象数据的 CategoryPath / ValuePath | 已实现 | 类型缓存访问器、嵌套路径、DataError/Issues，真实 compiled AXAML / MVVM 与业务记录示例通过 | C1 |
| DS03 | 直接数值列表和 Point 列表 | 已实现 | 数值列表按源索引给 X；ChartPoint/Avalonia Point 保留 XY，非有限/缺失值由统一空点策略处理 | C1 |
| DS04 | 范围、OHLC、角度半径、层级、图和空间数据 | 部分 | 范围/OHLC/角度及C6层级/图/空间/排程/运营typed契约与原始来源已接入；完整统一专用视图和官方适配继续开放，见[C6报告](charts-c6-main-completion.md) | C1、C5、C6 |
| DS05 | 非有限值过滤和折线断点 | 已实现 | 当前固定 Gap；已有空数据、常量和极端有限值回归 | C0 |
| DS06 | EmptyPointMode：Zero | 已实现 | 现有三系列统一策略；本库默认 Gap 与官方默认不同，迁移说明保留 | C1 |
| DS07 | EmptyPointMode：Gap | 已实现 | 统一模式属性与 WasEmpty/IsEmpty，断线/忽略无效点通过回归 | C1 |
| DS08 | EmptyPointMode：Average / Interpolate | 部分 | 三系列已实现邻有效值平均/按X插值（重复逆序X按源序比例），边界留Gap；区间专属规则待对应系列 | C1 |
| DS09 | 逐数据项画刷和调色板 | 部分 | 有系列画刷及按实例稳定的公共palette；逐项颜色和路径映射待补 | C1、C2 |
| DS10 | 完整 MVVM 状态、命令与通知 | 部分 | 已有逻辑继承、编译绑定、资源/源替换、共享悬停、点/系列选择TwoWay与取消回写、原子Range及主轴范围联动；C7-01 补专业根 SelectedDataKeys/DataSelectionRange 编译绑定与跨图业务键联动，见[范围与联动报告](charts-c7-range-linking.md)；完整前置事件与其他独立根仍待补 | C1、C2、C7 |
| DS11 | 自定义 Cartesian 系列绘制协议 | 已实现 | IChartSeriesRenderer + IChartProjection，三系列统一使用；外部派生系列 bounds/render/hit/legend 回归通过 | C1、C4 |
| DS12 | 自定义图例、边界及交互数据提供者 | 部分 | renderer可控制bounds、命中、投影与图例符号；图例内容控件模板与有界滚动已验；复杂叠加仍待C2 | C1、C2 |
| DS13 | 金融叠加层与 bounds provider | 部分 | FinancialChart日期索引/MapTime/ProjectOverlay、价格/量独立轴和金融完整High/Low bounds已交付；可见区专属优化/复杂外部bounds provider仍开放 | C1、C5 |
| DS14 | PlotAreaContent / 覆盖内容 | 部分 | Cartesian 已有被动内容与真实轴投影；C7-01 接入 Engineering/Analysis 专业根的 Control/模板、实际帧、绘图区布局/裁剪、资源继承、重入清理与永久导出，使用明确的视觉布局比例而非伪造数据轴，见[专项报告](charts-c7-plotarea-trackball.md)；其余独立根及交互式编辑继续开放 | C2、C7 |
| DS15 | 官方公开消费 API 与迁移映射 | 部分 | 已固定12.3.0实际API与本地C0/C1快照，提供Usage/两类隔离包消费者和迁移说明；无二进制替换承诺 | C0、C1、C7 |

### 坐标轴

来源：[Axis customization](https://docs.avaloniaui.net/controls/data-display/charts/shared-elements/axis-customization-chart)、[Combo 次轴](https://docs.avaloniaui.net/controls/data-display/charts/cartesian/combo-chart)。访问：2026-10-15。

| ID | 功能范围 | 状态 | 当前边界与自主交付项 | 阶段 |
| --- | --- | --- | --- | --- |
| AX01 | 数值轴自动范围、漂亮刻度、显式上下限 | 已实现 | 当前线性数值轴；极端值保护不代表所有轴模式完成 | C0 |
| AX02 | CategoryAxis | 已实现 | X/Y业务类别共享映射、显式标签与数字槽位；OnTicks/BetweenTicks、Gap、单类别范围和默认逐类别刻度已回归 | C1 |
| AX03 | DateTimeAxis | 已实现 | Unixms UTC转换、毫秒至年/日历刻度、格式与逆投影；任意时区/交易日历不在本行已实现范围 | C1 |
| AX04 | LogarithmicAxis | 已实现 | 有效正数、可配底/指数间隔、反向、投影/逆投影及锚点缩放回归通过 | C1 |
| AX05 | 主/次 Y 轴、系列轴归属 | 已实现 | Key映射、同侧/异侧多轴布局、独立视图或主轴归一化联动、逐轴缩放限制；共享叠加层属于C2 | C1 |
| AX06 | 坐标轴标题、基本数值格式 | 部分 | 基础标题/标签样式与原始域格式已有；C2-06 Demo暴露顶部X终点100与右Y标题的角落重叠，标题/端点占位避让仍需补；UTC契约见C1文档 | C0、C1、C2 |
| AX07 | 固定主步长、次步长、次网格 | 已实现 | MinorInterval优先，或每主间隔插MinorTickCount；numeric/date原始单位、log指数单位，主次网格独立 | C1 |
| AX08 | 标签适配：隐藏、换行、多行、45/90度、自定义旋转及 Auto | 已实现 | 测量文本后选错行/换行/旋转并抽稀；缩放重算密度、稳定留白、Near/Far边缘回归；复杂模板标签仍属C2 | C1、C2 |
| AX09 | 轴/标题/标签/刻度/主次网格独立样式 | 已实现 | 各级开关/画刷/线宽、字体/字号、刻度长度；基线及主次网格dash/cap/join；隐藏轴保留数据投影 | C1、C2 |
| AX10 | Scale breaks | 已实现 | inline/source绑定、相邻交叠合并、压缩映射/逆映射、端点有效、内部空点/不可命中、穿越线裁断、标记样式；压缩规则属本库契约 | C1、C2 |
| AX11 | Plot bands：文字、重复、系列前后层级 | 已实现 | inline/source、NaN视图端点、分类槽/原始域、透明度/文字对齐/显隐、repeat count/until及图层，至多10000次 | C1、C2 |
| AX12 | 连续水平轴与系列兼容/退回分类布局规则 | 部分 | 数值/日期/对数连续X及混合三系列可用；本库显式选择轴，无效项留Gap，不提供官方“任一系列不兼容时整图自动退回类别”的策略；属于已记录的兼容差异 | C1 |

### 共用视觉与交互

来源：[Legend](https://docs.avaloniaui.net/controls/data-display/charts/shared-elements/legend-chart)、[Markers](https://docs.avaloniaui.net/controls/data-display/charts/shared-elements/markers-chart)、[Data labels](https://docs.avaloniaui.net/controls/data-display/charts/shared-elements/data-labels-chart)、[Tooltip](https://docs.avaloniaui.net/controls/data-display/charts/shared-elements/tooltip-chart)、[Interactions](https://docs.avaloniaui.net/controls/data-display/charts/shared-elements/interactions-chart)、[Crosshairs](https://docs.avaloniaui.net/controls/data-display/charts/shared-elements/crosshairs-chart)。访问：2026-10-15。

| ID | 功能范围 | 状态 | 当前边界与自主交付项 | 阶段 |
| --- | --- | --- | --- | --- |
| VI01 | 标题、网格和基础静态图例 | 已实现 | 已有当前控件范围内的基本展示 | C0 |
| VI02 | 图例位置、对齐、浮动、可复用图例模型 | 部分 | Cartesian 已有四边/横竖、对齐、浮动、稳定模型、被动控件模板与有界滚动；C7-01 补齐专业图族共享图例模板及标记/分类/色阶/尺寸语义、原始成员与预算，见[专项报告](charts-c7-shared-presentations.md)；专业图例滚动、分类显隐及虚拟化待补 | C2、C7 |
| VI03 | 图例控制系列/分类显隐 | 部分 | 系列显隐同步范围/柱分组/命中及选择，恢复保持配色/身份；C3-01线/面积与C3-02柱的堆叠/百分比显隐重算已接入，类别图例及其余图族继续待补 | C2、C3 |
| VI04 | 基础点标记 | 已实现 | 当前三系列提供十种形状、大小和独立marker填充/描边；柱marker默认关闭，其余形状使用保守外接半径命中 | C2 |
| VI05 | 数据标签 | 部分 | Cartesian 已有格式、原始项 formatter、样式、位置/裁剪、有限候选与避让；C7-01 补齐专业图族共享数据标签控件模板、完整原始成员、业务 VM、预算、视图定位与永久内容导出，见[专项报告](charts-c7-shared-presentations.md)；专业通用避让、连接线和高级排版待补 | C2、C7 |
| IN01 | 点/柱 Tooltip 基础命中与主题 | 已实现 | 原三系列名称、X/Y、分类标签和轴格式可用；C3-01线/面积/范围与C3-02比较系列提供显示值及RawPoint/BaseValue/StackValue/CumulativeValue，示例明确原值/源索引；样本身份仍为原始项 | C0、C3 |
| IN02 | Tooltip 模板与数据项 TooltipText、系列级开关 | 部分 | TooltipFormatter及真实IDataTemplate内容、typed上下文/原始Item、复用/资源继承/重入清理和PNG已有；TooltipText与系列级独立开关待补 | C1、C2 |
| IN03 | 按轴鼠标滚轮缩放、拖动平移、复位 | 已实现 | 独立 Viewport、边界、最小跨度与捕获释放已有；C2-05 增加 Zoom/Pan/Auto、连续双轴 delta、修饰键及实际范围变化后的父滚动交接，当前功能验证通过；未宣称完整官方交互契约 | C0、C2 |
| IN04 | 捏合等触控手势 | 部分 | C2-05 已实现当前三系列触点阈值/延迟tap、累计双指捏合/移动质心、单指平移、原生Pinch入口去重、滚动容器竞争、捕获/失焦/Escape及重入取消；当前功能验证通过，物理触屏/触控板、惯性/手掌拒绝及DPI设备矩阵仍开放 | C2、C7 |
| IN05 | 缩放灵敏度、历史及返回 | 部分 | WheelZoomSensitivity/WheelPanSensitivity及zoom修饰键、重置和程序范围状态已有，C2-05当前功能验证通过；缩放历史栈、容量和返回/前进仍未实现 | C2 |
| IN06 | 内嵌/独立 Range selector | 部分 | 横竖独立控件、双拖柄/窗口、原子TwoWay、键盘步长、主轴Chart连接及log/date/inverse/断轴比例转换已实现；C7-01 新增专业 ChartDataRangeSelector，以当前布局比例选择真实图元包围盒及全部原始成员，见[专项报告](charts-c7-range-linking.md)；内嵌数据概览、图内拖拽及精确路径框选继续开放 | C2、C7 |
| IN07 | Crosshair：横/竖/双向与轴坐标标签 | 已实现 | 当前三系列提供Horizontal/Vertical/Both、各参与轴逆投影读数/标签与可观察AxisReadings；线/标签显隐独立，默认关闭标签，更多图族随其阶段验证 | C2 |
| IN08 | Trackball：横/竖与逐点/聚合浮层 | 部分 | Cartesian 已有横竖、分组/独立卡片及线/面积同路径插值；C7-01 专业根按可见真实图元包围区间切片，保留完整聚合成员、原始命中与业务 VM，提供模板、预算/省略计数、键盘与瞬态导出排除，见[专项报告](charts-c7-plotarea-trackball.md)；不将非数值图族伪造成数据轴插值，完整官方等价与实机性能继续待验 | C2、C3、C4、C7 |
| IN09 | Hover highlighting | 部分 | 当前提示环；补系列/单元/扇区高亮及其余数据淡化 | C2 |
| IN10 | Selection：Single、SingleDeselect、Multiple | 部分 | 当前三系列点选择/矩形与独立系列选择、Ctrl切换、TwoWay不可变列表、系列淡化及前置取消已实现；C7-01 专业根增加独立原始键多选、布局范围选择、地图图层/关系节点边身份、生命周期和同业务键联动，见[专项报告](charts-c7-range-linking.md)；不将聚合图元高亮误作所有成员已选，完整官方选择模式及前置取消仍待补 | C1、C2、C7 |
| IN11 | 点击、悬停、选择、缩放和系列变化事件 | 部分 | HoveredPoints、点/系列SelectionChanging/Changed、RangeChanged与可取消LegendItemInvoked已有；C7-01 增加专业 DataSelectionChanged 与按当前原始帧发布的联动，较新业务意图优先，显式同布局域可同步范围/工程视口；完整前置事件体系仍待补 | C1、C2、C7 |
| IN12 | 稳定布局下的交互锚点和视觉命中 | 部分 | 当前已有大标记/裁剪边缘命中和标签留白稳定的回归；仍需覆盖多轴、断轴和全部新增系列的一致几何 | C2、C4 |

### 注释、动画、导出、主题与可访问性

来源：[Annotations](https://docs.avaloniaui.net/controls/data-display/charts/shared-elements/annotations-chart)、[Export](https://docs.avaloniaui.net/controls/data-display/charts/shared-elements/export-chart)、[总览动画契约](https://docs.avaloniaui.net/controls/data-display/charts)、[产品页主题及可访问性描述](https://avaloniaui.net/charts)。访问：2026-10-15。

| ID | 功能范围 | 状态 | 当前边界与自主交付项 | 阶段 |
| --- | --- | --- | --- | --- |
| OV01 | Line annotation | 已实现 | 横竖线、标签、数据/Pixel、前后层/裁剪；当前 C2-06 支持标注专用轴，错误/重复/冲突维度跳过；本批当前功能验证通过 | C2 |
| OV02 | Band annotation | 已实现 | 区间带/标签/方向、层/断轴裁剪；当前 C2-06 专用轴/独立视图与 PNG 一致性本批当前功能验证通过 | C2 |
| OV03 | Text annotation | 已实现 | XY/样式/Pixel、层/裁剪；当前 C2-06 标注专用轴无需占位系列，本批当前功能验证通过 | C2 |
| OV04 | Rectangle annotation | 已实现 | 两角点、DIP 圆角短边收敛、标签/位置/偏移、多轴/缩放/导出；C2-06本批当前功能验证通过，不宣称官方 API 同名同参 | C2 |
| OV05 | Ellipse annotation | 已实现 | 默认两角点，新增中心±数据半径投影包围框、标签/位置/偏移；非线性下仍为屏幕椭圆；C2-06本批当前功能验证通过 | C2 |
| OV06 | Arrow line annotation | 已实现 | 端点/四种头部/固定DIP/短线收敛、虚实线/标签/偏移与裁剪；C2-06本批当前功能验证通过 | C2 |
| OV07 | 自定义叠加内容 | 已实现 | CustomAnnotation回调与被动 PlotAreaContent/IDataTemplate、plot-local多轴投影/上下文、继承/重入清理/PNG；C2-06本批当前功能验证通过，交互编辑不在当前范围 | C1、C2 |
| EX01 | PNG / JPEG 文件导出 | 已实现 | C2-08当前三图支持同步/异步、固定/推导像素、背景/透明度、DPI、JPEG质量、预算、同目录临时替换、取消与UI失败事件，图例/标签/标注/模板/缩放及暂停帧共用捕获；Windows/WSLg v2当前功能验证通过；压力/跨图全局预算留C4，系统保存选择器实操待验 | C2、C7 |
| EX02 | PNG stream 导出与保存对话框 | 部分 | C2-08已实现PNG/JPEG流与文件同步/异步、流不关闭及部分写出契约、队列/阶段边界取消、单图在途忙拒绝；Demo/Usage接StorageProvider并清理选择器对象及关闭回调，双平台picker/portal实际交互仍待验 | C2、C7 |
| EX03 | SVG / PDF 等额外格式 | 需核验 | 当前官方导出正文仅列 PNG/JPEG；如自行扩展，独立列范围 | C0 |
| AM01 | 入场动画开关、时长和 easing | 已实现 | C2-07 入场/数据/视图、duration/easing、暂停/取消、显式减少动态与点数预算保留；C3-01 新形状及上下界使用显示帧、命中和导出；C7-01 验证真实层级图暂停、换主题/换源、永久模板与选择导出组合，修复主题通知重启动画及实际显示视口投影，见[专项报告](charts-c7-range-linking.md)；系统偏好/其他独立图族与正式性能开放 | C2、C3、C7 |
| AM02 | 系列 delay / progress | 部分 | C2-07提供图表统一delay/eased progress、同版本数据发布和latest-wins；逐系列独立delay/progress、离场/错峰及性能资格仍待补 | C2、C4 |
| TH01 | 亮暗主题与自定义基础样式 | 部分 | Themes 与 Tooltip 亮暗配色已有；C2-09 新增显式 Light/Dark 高对比画布、文字/系列/选择/读数预设和至少 7:1 的默认系列色，保留本地自定义样式优先；OS 偏好自动接入、完整目录和模板仍待补 | C2、C3、C7 |
| AC01 | 图案等非颜色编码 | 未实现 | C2-09 的高对比配色、已有标记形状与浏览焦点轮廓不代表纹理/填充图案已实现；按原范围补图案和识别测试 | C2、C7 |
| AC02 | 图表键盘、读屏、数据摘要与可访问性树的官方精确覆盖 | 部分 | C2-09 当前三图已有原始点角色/名称/值/选择/命令、有界页与缓存、独立键盘及数据/轴摘要；Windows UIA 的树/点操作、Linux 独立 AT-SPI 总线的 9 点/边界/click/grab_focus/分页已有实查。完整桥接门槛未过：Windows 原生 GetSelection 列表为空而托管为 1，自定义 selector 的附着 ControlPeer 对照也复现；Linux 已选子节点枚举未过、嵌套点选择写入未验。原生点焦点、真实读屏、未来图族及官方精确覆盖继续开放 | C0、C2、C7 |

## 性能及 Windows / Linux 对照边界

来源：[官方产品页性能、实时数据和平台描述](https://avaloniaui.net/charts)、[官方数据源与更新契约](https://docs.avaloniaui.net/controls/data-display/charts)。访问：2026-10-15。公开材料提及渲染优化、实时数据和跨平台，但本轮没有获得可用于同机复测的官方点数/FPS/延迟/内存基准。下面的工作项均为本仓库计划，不能写成官方已经达到的数值。

| ID | 对照项 | 状态 | 必须补齐的证据 | 阶段 |
| --- | --- | --- | --- | --- |
| PF01 | 完整数据、可见数据和绘制数据分层 | 部分 | 当前有场景缓存，但失效后仍全量快照/遍历；补版本化数据管线和分层失效 | C4 |
| PF02 | 大数据裁剪、抽样与 LOD | 未实现 | 线峰值/缺口保持、分布不失真、原始点可追溯 | C4 |
| PF03 | 实时追加/替换/删除与窗口滚动 | 部分 | 集合变更/DeferUpdates通知合并已有；增量范围、分段数据、环形缓冲与持续刷新预算待补 | C4 |
| PF04 | 命中索引和交互缓存 | 部分 | 三系列已有精确X索引/候选裁剪、非单调稳定radix及原始项身份；二维/同X/SharedX退化、更多图族和持续交互预算待补 | C4 |
| PF05 | 与官方同等性能 | 需核验 | 固定两库版本、硬件、后端、数据集、视觉设置，比较首帧、P95/P99、内存和持续更新 | C0、C4、C7 |
| PL01 | Windows 实机功能回归 | 部分 | 已有 Windows 软件与原生窗口自动化验证；物理输入、DPI、多屏、实际呈现及完整读屏验收待完成 | C0、C7 |
| PL02 | Linux 功能兼容 | 部分 | 已有 Ubuntu WSLg 软件与原生窗口自动化验证；目标 Linux 实机、输入设备、辅助功能和后端兼容验收待完成 | C0、C7 |
| PL03 | Linux 性能与官方对照 | 需核验 | 须有 Linux 独立原始测量；WSL、CI headless 或能编译均不代表桌面性能完成 | C4、C7 |
| PL04 | Windows/Linux 可访问性与发布 | 部分 | C2-09 点级软件语义、键盘、摘要和显式高对比已有；Windows 外部 UIA 树/点操作与 Linux 独立 AT-SPI 树/边界/click/grab_focus/分页已有实查。Windows 原生选中列表和 Linux 已选子节点枚举未过、嵌套点选择写入未验，完整原生门槛未过；真实读屏器、原生点焦点、OS 偏好/图案、完整发布及设备矩阵继续开放 | C2、C7 |

## 完整性与更新规则

1. C0 保存目标版本和本清单日期；逐项核对各分类侧栏、正文中的系列变体及示例组合。对官方新增、重命名和下架保留差异记录。
2. 每项“未实现/部分”须绑定开发计划中的阶段、场景数据、自动化测试、Demo 入口和 Windows/Linux 验收记录；复杂算法另存数值参考与容差。
3. 公共基础设施完成不自动给新图表打勾；新图表完成也不自动表示模板、导出、选择、读屏或高密度性能完成。
4. “需核验”转为其他状态必须附公开文档或目标版本的可复现证据。未公开的官方行为可以先自主设计，但要保留“自主扩展”标识。
5. API 名称、默认值和渲染语义分别核验。当前已有 ChartSeries、CartesianChart 等名称，不等于兼容官方命名空间、XAML、数据契约或二进制。
6. 性能结论必须链接原始测量、环境和对照设置。Linux 按发行版/会话/设备逐项回填；WSLg 合成通过仅覆盖已执行范围，其余目标保持待核验。
