# Charts.Usage

“平台字体与启动诊断”折叠面板同时显示中文、`é`、😀 和阿拉伯文字的真实 TextBlock、图表标题、图例及默认数据标签。正常 `Program.Main` 在 `UsePlatformDetect` 前无条件调用生产 `WithChartsNativeTextIsolation()`，保存创建界面前的诊断快照；不依赖 `--isolate-native-text`。Linux/glibc 的实际文本库路径、SHA256、版本、符号提供者与 resolver 调用通过 `ChartNativeTextBinding.Diagnostics` 公开，Windows 为 `NotApplicable`。初始化失败终止启动，不继续绘制界面。

```powershell
dotnet run --project samples/Charts.Usage -- --smoke --charts-font-startup-only --smoke-output <新绝对目录>/usage.json --smoke-image <新绝对目录>/usage.png
```

去掉 `--charts-font-startup-only` 保留完整绑定冒烟并追加字体检查。主 JSON 格式不变；`usage.font-startup.json`（`charts-sample-font-startup/v1`）记录 before-UI/真实文字绘制后的原生绑定、各脚本 shaped runs 和字体 glyph ID/哈希、原始 A/C 命中、七张 PNG 的尺寸和哈希。四张逐脚本隐藏对照图必须分别产生实际文字像素差，恢复图必须精确一致；缺少中文等脚本的 glyph 不会当作通过。初始化失败写 `usage.startup-failure.json` 并返回非零退出码。每次运行使用新目录，PNG 不覆盖。

冷字体缓存验证需要每个进程使用独立且开始为空的缓存目录；证据记录的环境变量不能单独证明冷缓存。窗口 PNG 是原生窗口视觉树的 RTB 读回，另有图表导出，不冒充 compositor/GPU 输出、物理输入或完整塑形认证。Headless 包消费者复制 `FontStartupScene.cs` 和 `MainWindow.FontStartup.cs`，自定义 Program 提供 `internal static object? NativeTextBeforeUi { get; } = null;`；仅构造诊断面板不会初始化原生库或启动原生冒烟窗口。

“C7 集成”通过一个编译绑定的 ViewModel 属性同步七类主题根，包含 BeeswarmPlotChart/StripPlotChart 别名；切换普通/高对比主题保留原始选择。两平台原生 Usage 各848项、功能包/主题包隔离消费者各1032/1064项，包含32项明确的主题颜色断言；仅功能包不自动安装 ChartsTheme。范围、完整 Demo 失败和剩余专业集成见 [C7-01 报告](../../docs/charts-c7-integration.md)。

展开“C7 专业图族 · 共享业务 Tooltip 控件模板”（`ProfessionalTooltipExamples`），可查看 Funnel 单项和 Mosaic 重复分类聚合。`MainWindow.axaml` 的 `DataTemplate` 使用 `x:DataType="charts:ChartDataTooltipContext"` 编译绑定；`Members` 保留标记的全部原始记录、稳定 ID 和源索引，示例聚合对应 `[0, 2]`。模板只读取成员数量和首项身份，避免为大聚合创建完整控件列表。`BusinessDataContext` 交给具有 `ChartUsageViewModel` 类型的嵌套模板，直接读取业务标题。

按钮可切换模板与默认读数、更新 DTO/业务标题或替换数据源；复选框控制 Tooltip。普通明暗主题及“C7 集成”的 `HighContrastMode` 选择器共同作用于模板继承的字体、资源与提示外框。`MainWindow.ProfessionalTooltips.cs` 验证编译绑定、控件复用、原始身份、双向选择及旧上下文/订阅释放，并将范围写入 `professionalTooltipDiagnostics`。这份 Usage 检查不声明导出像素验证；专业图族的默认和模板 Tooltip 均从 PNG/JPEG 同步、异步导出排除，由 Demo 的 `--charts-professional-tooltips-only` 原生窗口检查提供同格式像素对照。API 契约见 [Charts README](../../src/NT.AvaloniaControl.Charts/README.md#专业图族-tooltip-模板)。

C6-04～C6-06 的“C6 主项”面板提供26个真实AXAML编译绑定示例：Gantt/Swimlane/Event/Spiral四类排程，Bullet/KpiCard/Calendar/Matrix/Table/Waffle/Word/Bump/Slope/Parliament/ThemeRiver十一类运营分析，Icicle/Flame/Indented/Radial/Dendrogram五类层级，以及地图Marker/Line/Vector/Pie/分类色/Seat多选六种扩展。数据与断言分别位于 `ChartUsageViewModel.C6Completion.cs`、`MainWindow.C6Completion.cs`，它们和同一份AXAML都进入功能包、主题包两类隔离消费者。

示例使用真实业务路径 Open/Close/Complete/Links、Amount/Prior/Goal、Day、Entity/Period、Branches/Cost/Distance、East/North/Metric/Parts；Table通过可观察Columns读取业务行及格式、数值域和文本。Bullet、KPI、Waffle展示独立标量绑定，标量选择的Item是实际控件自身；集合模式的Item是原始业务行，非null空集合也覆盖标量。KPI显式Percentage Delta用百分数点，12.5显示12.5%，Previous派生变化独立保留；Previous为0时派生比例未定义。Map Seat的TwoWay SelectedItems包含原始区域对象，不是复制投影。

`VerifyC6CompletionBindings` 追加到已有完整验证，覆盖所有面板、TwoWay选择、重排后的身份、可见任务跨界查询、INPC/INCC、换源释放、条件表列变更、子节点展开钻取、地图slice通知、座位多选、缩放裁剪和卸载/重挂载。视口检查使用内部可见点，不要求合法裁剪后的边界事件继续绘制。控制器/数据快照在读取最新帧后核对订阅释放；不以程序化检查替代物理输入或GPU呈现。

```powershell
dotnet run --project samples/Charts.Usage -- --smoke --smoke-output artifacts/c6-usage.json
```

本轮正式Windows/Linux结果和证据由阶段报告封存；这里不提前发布正式计数。完整官方嵌套排名/矩阵适配、词云旋转/更紧密排布、专业模板/共享交互、正式性能、C7物理平台/长稳仍开放；C5统计缺项、C7-NATIVE-LOD-AV-01和按用户决定后置的百万点命中尾延迟、A11Y-NATIVE-01保持原状态。API和数值规则见[Charts README](../../src/NT.AvaloniaControl.Charts/README.md)。

C6-03 的“地理图”提供 Shape、Choropleth、Bubble、Heatmap 和 Seat 五个编译绑定面板。图层继承地图 DataContext，自定义区域/经纬度/业务值/标签路径映射普通业务 DTO；值、位置、集合和图层可观察更新，选择以原始区域或观测身份跟踪。示例展示空洞、离散区域、未知区域诊断、共享视图、逐项配色和座位业务状态。`MainWindow.Maps.cs` 同两类独立 NuGet 消费者共享验证：双向选择、INPC/INCC、无效值后的清空/恢复及卸载后观察释放。地图默认严格拓扑；自带世界资源的来源及显式 EvenOdd 规则见 Demo 资源清单。完整 Windows/Linux 软件验证和未覆盖范围见[地理图报告](../../docs/charts-c6-geographic-maps.md)。

C6-01/C6-02 的层级与关系面板分别演示六类层级/打包图和九种关系模式，包括业务对象路径、循环/自环/平行边、独立节点/边身份与选择、后台重排及取消。示例与包消费者共享同一绑定代码。

独立 AXAML / MVVM 示例，使用 compiled bindings、业务对象 `INotifyPropertyChanged`、可替换 `SeriesSource`、系列/轴的 DataContext 与动态资源继承，以及显式 TwoWay `Viewport` 和重置命令。

C5-03 的“技术指标与趋势”展示 `MovingAverageSeries`、`ChartTrendlineSeries`、`SMAIndicator`、`EMAIndicator`、`WMAIndicator` 和 `BollingerBandsIndicator`。周期、四种均线和六种趋势使用双向编译绑定；修改业务值、追加记录及替换整个可观察数据源会重算。派生系列通过 `SourceSeries` 读取完整映射历史，缩放继续使用同一计算结果；移除可见原系列后，仍被引用的分析依赖继续观察数据。卸载窗口会释放原始对象观察，重挂载后恢复。

均线按源索引对齐，缺失 X/Y 重启预热；EMA 以周期窗口 SMA 为种子，alpha 为 `2/(period+1)`，WMA 新值权重大，TMA 为 `SMA(ceil(p/2))` 再 `SMA(floor(p/2)+1)`，总窗口宽度恰好为 p。Bollinger 使用总体标准差。五种回归使用中心缩放与 Householder QR；非有限和模型正域外观察被省略并计数，拟合曲线保留合成身份，R² 在原始 Y 域计算且常数 Y 为 null。`MainWindow.TechnicalTrends.cs` 与两个独立 NuGet 消费者共享绑定、数值、原始身份、缓存、INPC、追加/替换和卸载验证；程序化检查不代表物理输入、GPU 或专项性能资格。

C5-02 的“金融价格”展示 `CandlestickSeries`、`OhlcSeries`、`HeikinAshiSeries` 和 `PointAndFigureSeries` 的编译绑定，以及组合宿主 `FinancialChart.SetData` 的显式不可变快照。真实交易时间通过 `TimePath` 映射到等距交易槽，价格与成交量使用不同数值轴；缺失日期不会自动创建交易。业务 INPC 会更新直接绑定的系列，宿主在点击刷新后重新读取数据；箱大小双向绑定 X/O 列。`MainWindow.Financial.cs` 核对原始业务身份、真实 OHLC 与合成显示价、旧快照、跨周末日期、原始范围、宿主日期查询及非时间变换隐藏成交量；相同源码与检查进入两种独立 NuGet 消费者。Renko、Kagi、Point & Figure 本轮采用明确的 close-only 固定阈值规则，尚无 ATR、百分比、实时未封闭投影或交易撮合语义。Headless/程序化窗口检查不代表物理输入、GPU 呈现或正式性能资格。

C5-01 的“统计与分布”展示 `BoxPlotSeries`、`ErrorBarSeries`、`HistogramSeries`、`DensitySeries` 的编译绑定。切换数据会更新组内样本、误差中心和分布；离群点开关、单侧误差、KDE 带宽及 Count/Probability/Density 均可调整。箱线命中和选择保留原始业务组，直方图/密度命中保留其实际 bin/采样项。`MainWindow.Statistics.cs` 验证双向控件、原始身份、INPC 更新、旧快照和不等宽归一化；相同 AXAML/ViewModel/验证代码进入两类隔离包消费者。数值 API 使用 Type 7 分位数和 Gaussian KDE，有限域 KDE 不重新归一化，均值置信区间为正态近似；这些检查不代表物理输入、读屏器、GPU 呈现或正式性能资格。

```powershell
dotnet run --project samples/Charts.Usage
```

窗口支持修改业务对象数值、追加数据、替换整个系列集合及切换主题；两张图共享 ViewModel 视图状态。`--smoke --smoke-output <path>` 执行原生窗口中的程序化绑定和解绑检查；它不验证物理输入或实际呈现性能。

展开“增量与实时调度”可启动后台批数据，通过 `ChartStreamingSession<T>` 的有限队列在 UI 提交；`ChartDataBuffer<T>` 保留最新256条原始业务对象。暂停提交、取消生产者与 `FollowLatestData` 跟随视口分别控制，`FollowLatestWindow` 使用原始X单位。拖动/缩放停止跟随，重新勾选后回到最新。旧的 `ChartDataSnapshot` 保持原有顺序、原始对象和索引；当前窗口的索引随头部淘汰重新编号，业务 ID 继续标识同一记录。

`MainWindow.IncrementalStreaming.cs` 在编译绑定与两类独立包消费者中检查追加/滑动窗口/删除/替换、旧快照、原始命中和选择、跟随与历史视口；异步检查用显式暂停与 `FlushAsync` 验证队列上限、等待写入取消、恢复提交和完成，不以计时猜测提交了多少批次。队列容量约束待提交批次，输入枚举、旧快照持有及应用回调的内存和耗时仍由调用方负责。

展开“散点与气泡”可查看 `ScatterSeries`、`ScatterLineSeries` 和 `BubbleSeries` 的编译绑定。业务大小与颜色经 `SizePath` / `ColorPath` 读取，气泡示例兼容 `WeightPath`；尺度、绘制顺序、标签和连线开关双向绑定 ViewModel。固定输入域 0–100 映射到 20–60 DIP 直径，Area 在直径平方间插值，Diameter 直接插值，缩放 XY 不改变大小。源序轨迹包含非单调 X 和 NaN，缺失 XY 断线。

提示模板展示 `SizeValue` / `ColorValue` 原始编码及 `MarkerDiameter` 显示值，真实 XY、源索引和业务对象保留。`MainWindow.ScatterBubble.cs` 验证两种尺寸公式、SizePath 优先于 WeightPath、大小/颜色 INPC、负大小隐藏标记但保留数据节点、线段与缺口、选择和卸载/重挂载；两类隔离包消费者使用相同 AXAML 和 ViewModel 源码，并验证公共枚举、参数拒绝和真实包类型。实际大小边缘、渐变颜色、重叠、绘图区裁剪和三种 DPI PNG 的像素证据由统一 Demo 原生检查提供。

展开“圆形图”可查看 `CircularChart` 的饼图、环/部分环和半环编译绑定。业务对象通过 `CategoryPath` / `ValuePath` / `IdPath` / `ColorPath` 投影；内半径、总角度、负值策略、外标签、暂停和减少动态效果双向绑定 ViewModel。零值不分配角度，默认忽略负值，Absolute 模式保留原始负数并用绝对值计算份额。当前扇区保留原始对象、源索引和稳定 ID。

`MainWindow.Circular.cs` 验证份额、类型默认值、颜色、选择、INPC 值/颜色更新与卸载/重挂载；两类隔离包消费者使用相同源文件并补验公共类型、枚举和非法参数。当前帧的饼/环/半环、方向、缺口、小扇区、外标签连线、程序化指针选择、暂停过渡和 96/144/192 DPI 导出由统一 Demo 原生检查提供。Headless 与程序化窗口结果不代表物理输入或读屏器、GPU/桌面呈现及正式性能资格。

展开“柱条与比较”可查看编译绑定的横竖方向、分组/堆叠/百分比、显示排序、可空固定 DIP 宽度和原始值标签；下方展示范围柱、Dot、Lollipop、Variance、瀑布与 Pareto。方向切换同时交换数值轴与 `CategoryAxis`。提示卡明确显示 `RawPoint` 和原始源索引，排序不会改写源集合。范围柱/Variance 只绑定 High/Low，不重复提供 ValuePath；瀑布的 `TotalPath="IsTotal"` 订阅业务通知。`MainWindow.BarComparison.cs` 验证真实业务身份、正负百分比份额、排序槽、累计端点及卸载/重挂载订阅；这些相同源码被复制到两类隔离 NuGet 消费者。

右侧图的“浮动图例”使用 compiled binding，矩形、椭圆和箭头由 AXAML 声明并继承动态资源。绑定检查同时验证浮动布局不占绘图区、形状属性/资源，以及 `SelectionChanging` 取消外部 TwoWay 赋值后回写旧状态并保留绑定。这些相同的消费文件也用于两种真实 NuGet 消费者。

展开“轴配置绑定”可查看次网格、断轴和带文字的重复区间；右侧成功率轴使用独立的 TwoWay `ViewMinimum` / `ViewMaximum`。轴内区间和断轴继承 ViewModel 与动态资源，重置命令会同时清除独立次轴范围。轴放大倍数由各轴的 `MinimumZoom` / `MaximumZoom` 限制；日期区间和重复步长使用原始 Unix 毫秒，对数次步长使用指数单位。

横轴使用 compiled AXAML 的 `ScaleBreaksSource` 绑定可观察集合；程序化检查会追加、修改和移除断轴，核对集合通知与子项通知都生效。非 null 的来源集合优先于内联集合，空来源集合表示不使用断轴。

独立 NuGet 消费验证由 `scripts/Test-ChartsPackageConsumer.ps1` 执行：在调用方已构建 Release 产品程序集后打包，分别使用功能包自定义样式和功能+主题包创建两个隔离目录与全新缓存。消费的 AXAML/ViewModel 是本示例源码的物理副本，无源码项目引用，结果属于 Headless 包接入证据。平台入口使用 `-IncludeC1` 扩展验证本示例和消费者。

展开“标注和参考元素”可查看没有占位系列的专用附加轴、圆角矩形/标签、中心数据半径椭圆和被动 PlotAreaContent 模板。编译绑定及两种包消费者验证多轴缩放、源替换/解绑、动态资源、滚轮穿透和 PNG 导出。

展开“动画”可启用入场、数据与视图变化，配置时长、延迟、暂停和减少动态效果，或更新数据、重播、取消。`AnimationEasing`、`AnimationPointLimit` 也由 ViewModel 编译绑定；取消显示最新目标，减少动态效果立即更新。默认关闭动画，超过点数上限跳过过渡。`ReduceMotion` 由应用接入自身的用户或系统偏好。

本示例与隔离包消费者检查导出结果契约；Headless 后端不产生 PNG 时要求明确的 `NotSupportedException` 和零输出。真实 PNG 像素、真实 RAF 和不同 DPI 的当前帧一致性由原生 Demo 单独验证。

展开“导出”可通过编译绑定配置 PNG / JPEG、像素宽高、按布局推导尺寸、DPI、背景与 JPEG 质量，并观察异步保存、取消及失败状态。显式像素尺寸不会随 DPI 改变；JPEG 将透明区域合成白底，PNG 保留透明。图表本身的背景仍会参与绘制。

本地保存使用路径重载及同目录临时文件替换；其他存储提供方使用调用方流，失败或取消可能保留部分输出。异步方法在 UI 线程捕获画面，后台编码和写入。取消在阶段边界和写入块之间观察；当前编码器调用不支持中途打断。取消抛出 `OperationCanceledException`，不发布 `ExportFailed`；界面捕获它并恢复按钮状态。普通失败返回 `false` 并在 UI 线程发布一次事件。

`VerifyExportBindingsAsync` 由原生 `--smoke` 与两个独立 NuGet 消费者共同执行，覆盖选项双向绑定、无效参数、只读流、预取消、状态反馈及真实格式包络或明确的后端不支持结果。消费者以有界 dispatcher pump 等待异步任务，避免同步堵住 UI。Headless 成功或格式包络不代表真实像素验证，JPEG/PNG 像素、背景、DPI、暂停帧和写入响应由原生 Demo 另行检查。

展开“辅助功能”可切换独立点浏览和高对比模式，并读取由同一图表生成的数据摘要与当前点。`IsKeyboardNavigationEnabled`、`IsPointBrowsingEnabled`、`HighContrastMode` 通过 ViewModel 编译绑定；摘要及当前 Y 值直接绑定图表的只读属性。三个系列共享主横轴，压力散点使用 1000–2000 的独立右轴；业务记录包含一个缺失值，辅助点仍保留原始源索引、业务对象和稳定编号。

点浏览开启时，左右键浏览同一系列的有效可见点，上下键切换系列，Home/End 到首末点，PageUp/PageDown 跳转，Ctrl+Home 复位视图。浏览与选择分开：Space 选择当前点，Ctrl+Space 在多选模式追加或取消，Enter 调用 `PointInvokedCommand`，Esc 清除当前读数并保留选择。关闭点浏览后保留原有键盘行为。

`ChartAutomationPeer.GetPointPeer` 和 `GetPointPage` 提供按需、受限的点级辅助节点。节点可读取数值、调用业务命令及请求选择，选择仍遵循 `SelectionChanging` 取消语义。示例的编译绑定验证和两种 NuGet 消费者同时检查缺失值跳过、原始身份、源替换、解绑及只读值。图表不将全部点展开为辅助节点。

高对比模式是显式应用偏好；功能+主题包提供黑白背景和对比调色板，功能包由应用样式决定外观。自定义系列颜色仍优先，因此此示例使用默认系列画刷，并以圆形标记、柱形与菱形散点区分系列。应用负责把系统高对比偏好接入此设置。原生窗口内的程序化节点、键盘路由和导出像素检查不代表实际 UIA/AT-SPI 桥接、读屏器播报或物理键盘验收。

展开“折线与面积”可通过编译绑定切换 `ChartLineInterpolation.Linear`、`Smooth`、`StepBefore`、`StepAfter`、`StepCenter`，观察 `LineSeries`、`AreaSeries` 和 `RangeAreaSeries` 使用同一选择值更新。StepBefore 在区段起点垂直转换，StepAfter 在末点转换，StepCenter 在中点转换。`AreaSeries.Baseline` 在非堆叠模式生效，示例可选择 0、-20 或 10。

`LineSeries` 和 `AreaSeries` 的 `StackMode` 支持 `None`、`Stacked`、`Percent`；相同实际横纵轴、`StackGroup` 与堆叠方式的系列可共同累计。按实际 X 值对齐，重复 X 按出现次序配对，正负分别累计；Percent 正负分别归一到 +100 / -100。堆叠时使用累计下界并忽略面积的 Baseline。示例的两组面积共享 `usage-area`，折线使用 `usage-line`，缺失值开关用于观察断开和数据替换。

`RangeAreaSeries` 用 `HighPath` / `LowPath` 直接映射业务上下界，无需提供 `ValuePath`；示例保留原始业务对象和稳定编号，并通过 INPC 检查两界更新及卸载解绑。`ChartPointHit.Point` 为当前显示坐标，堆叠后的 `RawPoint` 为源贡献，`BaseValue` 为累计下界，`StackValue` 为当前贡献或归一化百分比；选择、提示和业务处理可据此区分源值与显示值。

原生 Usage 的程序化检查与两类隔离 NuGet 消费者覆盖插值、堆叠、基线的双向编译绑定，缺失值替换，正负百分比的源贡献保留、选择、上下界 INPC 和解绑恢复。包消费者另验混合 Line/Area 堆叠、分组变化、枚举及非有限基线拒绝。真实路径、填充、缺失空白与三种 DPI PNG 的像素一致性由 Demo 的“折线与面积”原生检查提供，绑定检查本身不代表实际呈现或性能验收。


C4-06 的 "RenderResourceExamples" 展示 "ResourceCacheBudgetBytes" 双向编译绑定、公开 "GetResourceCacheInfo()"/"TrimResourceCaches()"、可变画刷与 "ChartDataBuffer<T>.Compact()"/"StoragePageCount"。预算为五层本地缓存的估算模型，"NativeMemoryIncluded" 与 "SharedIndexCachesIncluded" 均为 false；0 禁用受控缓存，图形和原始记录仍应保持。显式压实会复制保留行，旧快照和调用者业务对象的内存另计。

C4-07 的 `MultiChartExamples` 展示两图 `UseBackgroundLayout` 双向编译绑定、`PrepareBackgroundLayoutAsync()`、`RequestRender()`、最新数据替换及公开后台/公平调度统计。展开后点“刷新两图与状态”，可以观察发布、取消和进程数字任务/五层缓存估算预算；折叠会停止当前后台工作。数字路径构建采用共享有限队列，数据映射、投影、轴、原生几何和绘制仍在 UI 线程，当前不代表整图布局全部离开 UI 线程。

C5-04 的“工程图与专业坐标”展示 Carpet、Hexbin、Smith、Ternary 和 Wind Rose 五类控件。业务路径、原始 ID、可观察源和参数使用编译绑定，验证真实成员、选择、单位/逆解、追加、换源、嵌套 INPC 与卸载重挂载。两种隔离 NuGet 消费者使用同一示例源码。原生像素、DPI 和主题验证由统一 Demo 提供；结果和边界见 `docs/charts-c5-engineering-projections.md`。

C5-05 的“进阶比较”使用七种控件展示发散、哑铃、Mekko、镜像、人口金字塔、龙卷风和 Venn。排序、方向、基线、间隙、标签和 Venn 映射路径由 compiled AXAML/MVVM 绑定。发散图以居中的基线共享正负幅值尺度；龙卷风默认按两侧总跨度稳定降序。Mekko 的列宽归一化权重、内部高度归一化分段值，负数拒绝，零值产生空几何；每个标记保留父列和真实子段。Venn 输入为包含交集的集合总量，显示独占区域，`Members` 是参与包含排除计算的原始汇总行，不是集合元素。双圆使用解析交叠面积，三圆近似通过 `MaximumRelativeAreaError`、各区域 `Value`/`EstimatedValue` 和 `UnrepresentedRegionMasks` 明示误差或无法表示的区域。

绑定验证覆盖嵌套子段 INPC、子集合追加/替换/重排、同一错误子项的恢复、`SetsPath`/`MaskPath` 等价、端点与左右侧选择身份、连续 `SelectedIndex` 更新、卸载和重挂载。两个 NuGet 消费者复制并执行相同绑定源码，再独立检查公开类型、属性、归一化与集合语义。绑定检查不代表原生像素、物理输入或性能验收；这些结果由统一 Demo 和本阶段报告记录。

C6-01 的“层级和打包”提供 Treemap、Sunburst、Organization、Bubble cloud、Packed bubble 和 Circle packing 六个控件。业务节点以编译绑定映射 `Nodes/Weight/Name/Id` 到子源、原始值、标签和身份，展示展开/折叠、钻取/返回、可见深度、标签和减少动态效果。分支自身Weight999保留为原始值，叶合计9作为布局权重；`SelectedIndex` 双向绑定保留重排后的原始节点，不把兄弟序号或显示序号混成一列索引。

验证覆盖六类真实场景、路径绑定、原始ID/对象、叶合计、嵌套INPC/INCC、新增/删除节点订阅、错误值清屏与同源修复、完整卸载释放和重新挂载。功能包与主题包消费者复制同一Usage源码并独立执行。圆打包父圆含布局空隙，终端圆面积才共用数值比例；GroupPath/ShowLegend提供类别配色与有界图例，气泡大小图例为9:4:1相对面积；任意组织节点模板、完整交互图例和官方特定气泡尺寸规则不在本轮声明内。动画、主题、原生窗口PNG/导出/DPI与Windows/Linux结果见 `docs/charts-c6-hierarchy-packing.md`。


C5CompletionExamples 包含七类统计图的编译绑定、原始身份、嵌套Samples、Levels/Axes通知和双向选择验证。


C7-01 的“专业图族 · 共享图例与数据标签模板”提供 Funnel 与 Mosaic 的实际编译绑定。`LegendItemTemplate`、`DataLabelTemplate` 和 `MaxLegendItems` / `MaxDataLabels` 均由业务 VM 控制；开关模板、更新数值/标题及替换原始集合可以直接操作。`ChartDataLegendItemContext` 与 `ChartDataLabelItemContext` 保留完整 `Members` 和 `BusinessDataContext`，重复的北区线上两项仍以 `[0, 2]` 原始成员表示，界面仅显示有界计数与首项 ID。专业标签上下文与原有 Cartesian 的 `ChartDataLabelContext` 分开，不创建假系列。

Usage 及两种隔离 NuGet 消费者检查真实模板控件、编译绑定、成员身份、预算、业务/源通知、主题、视图、原始选择和换源释放。永久图例与数据标签的同步/异步 PNG/JPEG 像素验证由原生 Demo 专项负责；Usage 的绑定检查不代替原生图片或物理输入验收。本批平台结果及剩余工作见[共享图例与标签报告](../../docs/charts-c7-shared-presentations.md)。

专业图族的绘图区和 Trackball 示例使用 `ChartDataPlotAreaContext` / `ChartDataTrackballContext` 的编译模板绑定，覆盖 Funnel 与 Mosaic 两个共享根。`PlotAreaContent` 是永久业务内容，投影的 0..1 坐标表示图族布局，Y 向下；它不是数值轴或经纬度。Trackball 读取显示空间的垂直 / 水平切片，分组和逐读数浮动都保留真实原始成员，Mosaic 的聚合仍包含源项 `[0, 2]`。

`ProfessionalPlotTrackballExamples` 可切换永久内容、Trackball 和读数布局并更新业务数据。绑定检查同时验证来源替换、上下文释放、视口变化、高对比和显示预算；栅格永久内容与临时读数排除的像素证据由 Demo 的 `--charts-professional-plot-trackball-only` 原生窗口专项生成。

专业绘图区与 Trackball 的同名折叠区展示 Funnel、Mosaic。`PlotAreaContentTemplate` 使用 `ChartDataPlotAreaContext` 编译绑定永久内容与业务 VM；`TrackballContentTemplate` 使用 `ChartDataTrackballContext`，展示完整原始成员、读数预算和原始 ID。内容、横纵模式、Group/Float、模板、预算、换源命令和选择都绑定到可观察 VM。布局比例从左上 `(0,0)` 到右下 `(1,1)`，不是地图、排程或分类的原始数据轴。

PlotAreaContent 进入同步/异步 PNG、JPEG 导出；Trackball 线、标记和卡片从导出排除。Usage 核对实际编译绑定、来源身份与释放行为；原生图片由 Demo 专项验证。平台结果、截图目录和剩余工作见[专业绘图区与 Trackball 报告](../../docs/charts-c7-plotarea-trackball.md)。

“专业图族 · 范围选择和业务 ID 联动”展示独立的 `SelectedDataKeys` / `DataSelectionRange` TwoWay 编译绑定。`ChartDataRangeSelector` 的 `EngineeringChart` 或 `AnalysisChart` 只绑定一种目标，范围是该家族绘图区从左上角开始的比例；它按可见图元的轴对齐包围盒相交选择完整原始成员，圆弧或多边形孔洞内的包围盒空白也可能匹配。范围条保持数据坐标与视口；图内拖拽框选和套索仍为扩展范围。

示例的 Funnel/Mosaic 显式配置 `IdPath="Id"`，所以各自的 `SelectedDataKeys` 可以使用字符串业务 ID；未设显式 ID 的普通来源需要传原始对象引用。组的 keySelector 负责跨图映射，保留源图的原生键要求。地图仍使用带图层作用域的 MapGlyphKey。按键请求 a 时，Mosaic 聚合 a、c 的公开选择仅包含 a，整块仍高亮；用范围条选择这块才包含其全部原始成员。

`MainWindow.ProfessionalLinking.cs` 使用 `ChartDataSynchronizationGroup` 将两个根按原始业务 ID 映射；显式清除、换源保留 ID、反向选择和高对比切换都由真实 VM 属性驱动。不同家族默认只联动业务选择。只有应用明确提供相同的 `layoutDomain` 并开启相应选项时，才适合联动范围或工程图视口；这类可比较布局由测试覆盖，当前异构看板展示业务 ID 联动与局部范围、局部缩放。交叉筛选由应用依据选择事件和业务键更新查询或 ItemsSource。

挂载范围条时，图表已有的范围优先；卸载期间设置的非默认 Range 只在目标没有范围时初始化它。MinimumSpan 约束本地输入，外部图表范围保持原值。组的操作在 UI 线程执行，窗口关闭时释放组。成员虽使用弱引用，显式 keySelector 和 layoutDomain 仍被组持有；委托捕获的 VM/图表由应用管理。隐藏、禁用或卸载的成员停止同步，重新显示后需要主动重放时调用 SynchronizeFrom。

Demo 的“专业范围与多图联动”还展示地图层身份、聚合来源、范围条指针和键盘输入，以及永久模板 / 暂态读数的导出组合。Usage 的编译绑定检查覆盖业务状态和来源身份；同一布局域联动、实际导出图片及代表性动画组合由测试与原生 Demo 专项各自验证，物理输入和实机验收仍见[范围与联动报告](../../docs/charts-c7-range-linking.md)。
