# NT.AvaloniaControl.Charts

## Linux 原生字体的早期接入

使用当前锁定的 Avalonia Desktop / HarfBuzzSharp `8.3.1.3`，且需要规避 glibc Linux 上系统 HarfBuzz 与打包资产的符号抢占时，在正常应用的 `BuildAvaloniaApp` 中显式接入：

```csharp
using Avalonia;
using NT.AvaloniaControl.Charts;

public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
    .WithChartsNativeTextIsolation()
    .UsePlatformDetect();
```

扩展立即执行，必须位于 Avalonia setup、应用实例创建和任何 HarfBuzzSharp 原生调用之前；也可在入口最早处直接调用 `ChartNativeTextBinding.Initialize()`。不要放到 `App.Initialize()`、窗口构造或字体回调中。重复及并发初始化共享同一个结果。未调用 API 时没有自动初始化；Windows、macOS 等不适用系统为 `NotApplicable`，不加载 Linux 原生资产或 HarfBuzzSharp 托管程序集。Core Charts 不因此增加对 Avalonia.Skia / HarfBuzzSharp 的包依赖，桌面应用仍需自己的正常锁定依赖。

glibc Linux x64 / arm64 分别核对该版本的已知原生资产大小和 SHA-256；从 `AppContext.BaseDirectory/runtimes/<rid>/native/libHarfBuzzSharp.so` 或 RID 发布后的 `AppContext.BaseDirectory/libHarfBuzzSharp.so` 读取绝对路径，不搜索当前工作目录、系统库、NuGet 缓存或环境库路径。两个位置同时存在时两者均需匹配，避免静默忽略旧部署文件。当前架构的资产不存在或不匹配时明确失败；不会从另一架构回退。arm64 有独立资产指纹和路径支持，不能据此推断已完成 arm64 实机验收。

初始化使用 glibc 的 `RTLD_NOW | RTLD_LOCAL | RTLD_DEEPBIND`，核对实际 `hb_font_create` / `hb_version_string` 符号提供者及原生版本，并仅为当前 HarfBuzzSharp 托管程序集设置 resolver。其绑定会影响同一程序集在本进程中的 Avalonia 文字使用，不局限于某个图表。没有 module initializer、全局 `LD_PRELOAD`、依赖升级或系统设置修改；已生效的原生句柄保留至进程结束。

`ChartNativeTextBinding.Diagnostics` 返回不可变快照，可公开读取 `Status`、`PackageVersion`、`RuntimeIdentifier`、`GlibcVersion`、资产路径/字节数/SHA、`NativeVersion`、`SymbolProviderPath`、`ResolverRegistered`、`ResolverObserved`、`ResolverInvocationCount`、`ResolvedLibraryName`、`DeepBindingEnabled` 和错误代码/消息。读取本身不启动字体或加载原生库。`Initialized` 表示初始化检查完成；只有实际 HarfBuzzSharp import 后 `ResolverObserved` 才为 true，仍需真实多语文字渲染验证。诊断含本地资产路径，由应用决定日志披露范围。

```csharp
var state = ChartNativeTextBinding.Diagnostics;
Console.WriteLine($"{state.Status}: {state.RuntimeIdentifier}, " +
    $"provider={state.SymbolProviderPath}, imports={state.ResolverInvocationCount}");
```

已创建 Avalonia 应用或已映射 `libHarfBuzzSharp.so` 会以 `TooLate` 拒绝；无法读取 `/proc/self/maps` 时也不猜测早期资格。musl、其他 Linux 架构、非支持托管版本及缺失资产均失败。已有外部 resolver 时报告 `ResolverConflict`，不替换或串接未知 resolver；应用需选定一个最早期的绑定责任方。适用 Linux 上的失败抛出 `InvalidOperationException`，`Diagnostics.Status=Failed` 且保留原因；失败结果不可重试，已注册的 resolver 会拒绝默认回退。修正部署或初始化顺序后重启，不能捕获异常后继续渲染。应用仍需保证初始化前没有其他线程并行使用字体；公开 .NET API 无法枚举或撤销已缓存的 P/Invoke 绑定。

支持范围集中在 `NativeText/` 中的版本和双架构资产映射。未来依赖升级必须同时重新检查托管版本、两个原生资产指纹、符号提供者、冲突/过晚场景及真实冷启动，不能删掉检查来接受未知二进制。本入口不代表已认证单文件、裁剪、NativeAOT、其他 libc、所有发行版或物理设备；最终 Windows/Linux 验证及仍开放的环境问题以本轮报告为准。

C7-01 已接入共用高对比主题、专业 Tooltip、共享图例/数据标签及专业 PlotAreaContent/Trackball，并继续补齐专业范围选择与业务 ID 联动。当前实现、Windows/Linux 验证与剩余问题见[范围与联动报告](../../docs/charts-c7-range-linking.md)；历史证据保留在[专业绘图区/Trackball 报告](../../docs/charts-c7-plotarea-trackball.md)、[共享呈现报告](../../docs/charts-c7-shared-presentations.md)、[集成报告](../../docs/charts-c7-integration.md)和[Tooltip/导出报告](../../docs/charts-c7-tooltips-export-stability.md)。
## 专业图族 Tooltip 模板

`EngineeringChartBase` 及其层级、关系、地图、排程、比较和运营图族，以及 `AnalysisChartBase` 的漏斗、金字塔、Heatmap，可设置 `TooltipContentTemplate : IDataTemplate`。模板控件的 DataContext 为 `ChartDataTooltipContext`：`Text` 是当前图族的标准读数，`Anchor` 使用图表局部 DIP；`Members` 保留该标记贡献的全部原始 `Item`、`StableId`、`SourceIndex` 与数值元数据。`EngineeringHit` 提供聚合标记，分析图使用 `PointHit`，不会把聚合值伪装为一个原始观测。`BusinessDataContext` 单独保留读取时的业务 VM。

```csharp
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using NT.AvaloniaControl.Charts;

var chart = new MosaicChart
{
    ItemsSource = new[]
    {
        new { Id = "a", Label = "业务 A", Column = "线上", Row = "完成", Value = 3d },
        new { Id = "b", Label = "业务 B", Column = "线上", Row = "完成", Value = 5d },
    },
};
chart.TooltipContentTemplate = new FuncDataTemplate<ChartDataTooltipContext>((_, _) =>
{
    var text = new TextBlock();
    text.Bind(TextBlock.TextProperty, new Binding(nameof(ChartDataTooltipContext.Text)));
    return text;
});
```

控件在读取更新时复用，应通过 DataContext 绑定更新文字。XAML 使用 `DataTemplate x:DataType="charts:ChartDataTooltipContext"`；业务 VM 和原始 `Members.Item` 可交给各自具备业务类型的 `ContentControl.ContentTemplate`。模板加入图表逻辑/视觉树并继承资源、字体和主题；外框使用 `TooltipBackground`、`TooltipForeground`、`TooltipBorderBrush`，局部值和绑定优先。控件限制在图表边界内，禁用命中和 Tab 导航。

未设置模板时保留实时默认文本；模板 Build 返回 null 时隐藏当前提示。Build 或布局错误清理已创建的子树后传播，该失败模板停止重试，替换模板可恢复。源通知、换源、隐藏、禁用和卸载立即清除旧控件的读取上下文；恢复显示或启用后可恢复仍有效的读取。模板不得返回已有父级的控件，也不能在 Build/布局回调中重入导出。

这些专业图族的 PNG/JPEG 同步和异步导出统一排除所有瞬态 Tooltip，包括默认文本与模板控件。Cartesian 保留既有 `ChartTooltipContext`、模板和导出契约。`Members` 直接引用当前帧的原始成员列表；模板自行展开大聚合的全部成员时，需控制所创建的 UI 数量。

## 专业图族共享图例与数据标签模板

`EngineeringChartBase`、`AnalysisChartBase` 及其全部派生控件共用 `LegendItemTemplate` 和 `DataLabelTemplate`。图例模板接收 `ChartDataLegendItemContext`；数据标签接收独立的 `ChartDataLabelItemContext`，与 Cartesian 系列使用的 `ChartDataLabelContext` 保持各自契约。

```csharp
chart.LegendItemTemplate = new FuncDataTemplate<ChartDataLegendItemContext>((_, _) =>
{
    var text = new TextBlock();
    text.Bind(TextBlock.TextProperty, new Binding(nameof(ChartDataLegendItemContext.Text)));
    return text;
});
chart.DataLabelTemplate = new FuncDataTemplate<ChartDataLabelItemContext>((_, _) =>
{
    var text = new TextBlock();
    text.Bind(TextBlock.TextProperty, new Binding(nameof(ChartDataLabelItemContext.Text)));
    return text;
});
chart.MaxLegendItems = 8;
chart.MaxDataLabels = 40;
```

两种上下文均提供 `Text`、`Anchor`、`Bounds`、`Brush`、`Value`、`BusinessDataContext` 和完整原始 `Members`。工程标记使用 `EngineeringHit`，单点分析图使用 `PointHit`；Mosaic 等聚合标记直接保留当前标记的完整成员列表，不将聚合值替换成一条虚构观测。XAML 可为这两个上下文设置 `x:DataType`，业务 VM 通过 `BusinessDataContext` 进入自己的类型化模板。

`GetLegendItems()` 与 `GetDataLabels()` 提供当前有界候选，独立于模板是否启用。`MaxLegendItems` 默认32、范围0～1024；`MaxDataLabels` 默认200、范围0～4096。候选按图族顺序截取，0关闭该共享呈现。它们限制创建的模板根控件数量，不限制业务模板自行展开 `Members` 的子控件数；大聚合需由应用控制展示数量。页脚和图表边界会裁切超出的内容，模板应保持紧凑；当前未提供共享图例滚动或通用标签避让。

图例的 `Kind` 区分真实标记、分类、连续色标和相对大小示意。层级图保留可见终端分类与9:4:1大小键；地图按实际图层保留分类键、色标渐变、`Minimum/Maximum` 和原始成员。装饰性大小键没有业务 `Members`。关系图的节点与边使用各自原始身份域；表格、排程和比较图的数据标签沿用自己的单元、事件碰撞、端点和复合标记语义，轴标题、组标题及无数据地图标签仍是装饰。现有 `ShowLabels`、`ShowValues`、节点/边标签和图例开关继续生效。共享图例和标签为被动控件，不接收指针或 Tab；原有图形选择与键盘浏览仍返回原始数据。

模板控件加入真实逻辑/视觉树，继承图表资源、字体、`Foreground` 和主题。稳定帧复用控件，换源、属性通知、业务 DataContext 改变后清除旧读取；替换模板、隐藏或卸载释放子树，恢复显示后重建。控件禁用时永久图例和标签仍显示。模板 Build 返回 null 隐藏该候选，新读取可重新判断；Build/布局异常清理后传播，失败模板停止重试，替换模板恢复。模板必须返回无父级控件，且不得在模板更新或绘制过程中重入导出。

永久图例和数据标签同时进入 PNG/JPEG 同步与异步导出，并遵循既有像素尺寸、DPI、背景、取消和失败契约；瞬态 Tooltip 继续排除。导出前完成模板布局，捕获期间的数据或模板重入变更会拒绝此次导出，避免成功输出混合帧。Demo 的“专业图族共享图例与数据标签模板”及 Charts.Usage 包含 Funnel、聚合 Mosaic、StripPlot、SeatMap 实例，平台结果和剩余边界见仓库 C7 文档。

## C5 统计目录补齐

新增 `BeeswarmChart` / `BeeswarmPlotChart`、`StripPlot` / `StripPlotChart`、`ContourPlot`、`MosaicChart`、`ParallelCoordinatesChart`、`RidgelineChart`、`ViolinPlot`。它们继承现有原始来源选择、键盘、Tooltip、缩放/平移和 PNG/JPEG 同步/异步导出；没有通知的输入调用 Refresh()，可观察输入在 UI 线程更新。验证与限制见 `docs/charts-c5-completion.md`。

分类样本为 `CategoricalSample(Id,Category,Value,Label)`，业务路径为 CategoryPath/ValuePath；全部观察保留原始序号，Strip 使用固定seed/index的确定性jitter。Beeswarm优先放置无重叠点，过密或工作耗尽时保留全部点并公开 OverflowCount/CollisionCount/WorkBudgetExceeded；HitTestAll返回所有命中的原始点。PointRadius、Fill/Stroke/StrokeThickness、ShowAxes/ShowCategoryLabels及ShowMeanLine/JitterAmount可配置。点数最多20000、工作最多2000000；预算不构成性能SLA。

Contour 的 `ScalarGridPoint(Id,X,Y,Value)` 必须组成完整、唯一坐标的矩形网格；NaN掩蔽邻接单元，无穷值/缺坐标拒绝。显式Levels优先，否则ContourLevels默认8；ShowFill/ShowLines可分别切换。双线性内部鞍点定位后共用三角表面生成线和填色，几何为分段线性近似，不是任意散点插值或精确双曲线。每个线/面保留原始单元成员；共享阈值边合并相邻单元来源。最多65536点、32levels、2000000工作及262144图元。

Mosaic 用 `MosaicObservation(Id,Column,Row,Value,Label)`，或 GroupPath/ColumnPath、SubGroupPath/RowPath、ValuePath。同分类对聚合，保留所有原始行；列宽为列权重/总权重，格高为格权重/列权重，CellGap=0时面积对应全局份额。非负有限权重，0保留数值结果但不产生选择面积；同格求和不可表示时明确失败。分类按值类型和首次出现次序，最多20000行。

Parallel 用可绑定的 `Axes : ObservableCollection<ParallelAxis>` 配合普通DTO的ValuePath，或 `ParallelRecord(Id,Values,Label)` 配合无ValuePath轴。每维独立自动/显式范围、反向和常值轴；NaN表示断点，不跨缺失轴连线；显示钳制保留原值。命中用真实线段距离，选择返回原始记录。最多64维、20000行、200000值，使用直线连接，完整曲线/图例共享集成留后续。

Violin/Ridgeline 接收 `DistributionSamples(Id,Label,Samples)` 或 SamplesPath/LabelPath（Violin另有ValuesPath/CategoryPath别名）。Gaussian KDE 使用Silverman带宽，默认每组样本极值±3带宽的共享域，各组共用绝对密度尺度；支持显式Bandwidth/Minimum/Maximum/SampleCount。有限样本及原始样本索引保留，缺失/非有限样本省略并计数；空组不绘制。小提琴显示Type7四分位与中位数，少于5项不绘箱体。Ridgeline的Overlap=0保留独立分布，正值增加相对行距的高度；ProfilePath="Points"可接 `DistributionCurve` 预计算曲线，不虚构样本/四分位。预计算采用折线，官方Spline/固定DIP SeriesHeight等差异继续记录。上限256组、100000实际输入、131072密度点、4000000核计算。

所有新图默认 IdPath="Id"，要求非空唯一ID；无业务ID时设 IdPath=null 使用原始对象身份。纯数值入口为 `ChartCategoricalDistribution`、`ChartContours`、`ChartMultivariate`、`ChartDistributionProfiles`，支持取消和独立数学验证。正式性能、物理平台、官方API/运行时等价与专业跨族集成继续在C7验收；百万点尾延迟和原生辅助语义仍按用户决定后置。


## C6-04～C6-06 排程、运营分析与目录补充

这一批提供自有公开数据契约与控件，不声明与 Avalonia Pro 的 API、二进制或运行时完全相同。Demo 与 Usage 均包含排程4、运营分析11、层级5、地图扩展6，共26种示例；本轮正式平台结果另行封存，开发期检查不能替代正式性能或物理设备资格。

`GanttChart`、`SwimlaneChart`、`EventTimelineChart`、`SpiralTimelineChart` 使用 `ScheduleChartBase`。甘特任务通过 `StartPath` / `EndPath` / `ProgressPath` / `DependenciesPath` / `LanePath` 读取普通业务 DTO，或直接使用 `ScheduleTask`；进度单位为0～100，开始等于结束表示里程碑。依赖保留 FinishStart、StartStart、FinishFinish、StartFinish 和 lag，缺少前驱、循环与约束违反通过诊断暴露，不自动重新排程。`ScheduleSpan` 是调用方明确单位的有限数值区间，不把日期自动转换为资源数值轴。

```csharp
var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
var gantt = new GanttChart
{
    ItemsSource = new[]
    {
        new ScheduleTask("design", start, start.AddDays(2), 60, Label: "Design"),
        new ScheduleTask("build", start.AddDays(2), start.AddDays(4), 20,
            Label: "Build", Dependencies: new[] { new ScheduleDependency("design") }),
    },
};
var snapshot = ChartSchedule.Snapshot((ScheduleTask[])gantt.ItemsSource!);
var crossingTasks = snapshot.QueryVisible(start.AddHours(12), start.AddHours(13));
```

`DateTimeOffset` 和 UTC `DateTime` 作为绝对瞬间归一化；无 Kind 的 `DateTime` 需通过 `InputTimeZone` 显式解释，机器本地 Kind 拒绝。夏令时跳空拒绝，歧义时间由 `AmbiguousTime` 的 Reject/Earlier/Later 明确处理。`VisibleStart/VisibleEnd` 和 `LaneFilter` 只改变可见项，区间索引保留跨越窗口边界的任务和原始源序号；事件按时间稳定排序后仍保留原始源序号。数值快照和查询支持取消及数量预算，不代表指定时延或实际内存 SLA。

运营控件继承 `OperationalChartBase`，共享 `ItemsSource`、`IdPath`、`LabelPath`、TwoWay `SelectedIndex`、原始 `Members`、键盘、Tooltip、`Zoom/PanX/PanY` 和 PNG/JPEG 同步/异步导出。通用映射路径包括 `ValuePath`、`SecondaryPath`、`DatePath`、`RowPath/ColumnPath`、`EntityPath/PeriodPath`、`TextPath`、`RangesPath`、`TrendPath`，默认名称见公开属性。可观察集合和对象在 UI 线程更新；不通知的输入需显式 `Refresh()`。

| 控件 | 数据入口及数值约定 |
| --- | --- |
| `BulletChart` | `BulletPoint` 或标量 Value/Target/MinValue/MaxValue/Ranges；原始越界值保留，显示位置钳制，极小条体保留最小命中宽度。边界必须严格递增且位于量程内。 |
| `KpiCardChart` / `KpiCard` | `KpiPoint` 或 Value/Previous/Delta/DeltaType/SparklineData；另有 Unit、Subtitle、刷子和 ShowSparkline。Previous派生变化用 `(Value-Previous)/abs(Previous)`，Previous为0时比例为null。显式 Percentage Delta用百分数点，12.5显示12.5%；布局 DisplayDelta为.125，IsExplicitDelta区分，独立 Change/RelativeChange仍保留Previous计算。 |
| `CalendarHeatmapChart` | `CalendarPoint` 或 DatePath/ValuePath；FirstDayOfWeek和包含首尾的StartDate/EndDate，缺失日只是背景，重复日期要求调用方先聚合。DateOnly直接使用，DateTime/Offset取其自身日历日期，不隐式转时区。 |
| `MatrixChart` | 平铺 `MatrixPoint` 的类型化行列键，或普通 DTO 的行列和值路径；bool按0/1编码，0值仍是真实单元，重复键对拒绝，不补造缺失组合。 |
| `TableChart` | Columns非null时映射真实业务行及 `OperationalTableColumn`；null时兼容平铺MatrixPoint。列有 Id/Header/ValuePath/Format/Minimum/Maximum/UseColorScale，支持文本、bool和有限数值，Cells保留RawValue、可空NumericValue及原始行。 |
| `WaffleChart` | `WafflePoint` 集合按稳定最大余数分配；标量Value/MaxValue先钳制比例，再对总格数中点远离0取整，空格不是业务点。FilledBrush/EmptyBrush区分显示。 |
| `WordCloudChart` | `WordPoint` 或TextPath/ValuePath，实际字体测量、有padding的确定性shelf矩形排布；零权重或放不下的原始索引由OmittedIndices公开，不缩短词或合并业务行。 |
| `BumpChart` / `SlopeChart` | 平铺 `RankingPoint` 实体/时期；时期按首次出现排序，Bump默认较大值排名靠前，并列为1、1、3；RankFromValues=false直接使用正排名。Slope需要两个时期；缺失期不跨连，端点标签避让/省略由OmittedLabelCount公开，点不省略。 |
| `ParliamentChart` | `ParliamentParty` 或整数值路径；总席位由各行Seats之和确定，精确分配到半圆同心行。每席保留原始党派行，不用比例舍入冒充席位输入。 |
| `ThemeRiverChart` | 对齐且非负的 `ThemeRiverPoint` 流/槽；Centered与Zero两种底线，每个带保留全部原始槽。仍可用已有 `ThemeRiverStack` 加堆叠面积组合；不声称wiggle或独立官方原生系列。 |

Matrix/Table 的单元内文字按实际实色填充与背景的 alpha 合成亮度选择黑色或白色，以取得较高对比度；轴外文字沿用 Foreground。非实色背景使用 Foreground 推断明暗底色，完整高对比适配仍需专项验收。

KPI的 `PositiveBrush` / `NegativeBrush` 只控制Delta文字，`SparklineBrush` 控制小图；标题、主值和副标题沿用Foreground。卡片底色保持不透明中性背景：Foreground为实色时按字节亮度 `(299R+587G+114B)/1000 >= 128` 选深底 `#1E293B`，否则浅底 `#F5F7FA`；非实色/未设置按Gray128处理。深底Delta默认正/负为 `#86EFAC` / `#FCA5A5`，浅底为 `#166534` / `#991B1B`，默认alpha为255；调用方刷子的alpha和Opacity保留。这是本库的明暗可读性规则，不是完整系统高对比认证。

标量入口仅在 `ItemsSource == null` 且 Value非null时生效；非null空集合也优先。标量 `Members.Item` 是实际控件自身，不伪造业务记录；集合模式保留业务 DTO。`KpiCard` 默认显示标量0，`KpiCardChart` 默认Value为null。大多数运营输入上限20000，矩阵/席位/waffle单元上限65536；词云最多256词，每词最多128个UTF-16字符。嵌套趋势和阈值同样检查实际枚举预算，不仅信任Count。

```csharp
var bullet = new BulletChart { Value = 72, Target = 85, Ranges = new[] { 30d, 60, 100 } };
var kpi = new KpiCard { Value = 124500, Delta = 12.5, Unit = "$",
    Subtitle = "vs last month", SparklineData = new[] { 10d, 12, 11, 15 } };
var progress = new WaffleChart { Value = 72, MaxValue = 100, Rows = 10, Columns = 10 };
var table = new TableChart
{
    ItemsSource = new[] { new OperationalTableRow("apple",
        new Dictionary<string, object?> { ["sales"] = 20d, ["status"] = "Open" }, "Apple") },
    Columns = new[]
    {
        new OperationalTableColumn("Sales", "sales") { Id = "sales", Format = "0.0", Minimum = 0, Maximum = 30 },
        new OperationalTableColumn("Status", "status") { Id = "status", UseColorScale = false },
    },
};
```

`OperationalTableRow.Values` 对列ValuePath使用精确字典键；普通业务 DTO 使用嵌套属性路径。Columns及列属性变化会刷新；选择按原行ID/列ID跟踪，文本或缺失字段不会伪造数值来源。表格是绘制型只读条件表，编辑、虚拟化属于独立表格组件。

新增层级控件为 `IcicleChart`、`FlameGraph`、`IndentedTreeChart`、`RadialTreeChart`、`DendrogramChart`，复用 `HierarchyChartBase` 的 ChildrenPath/ValuePath、展开、钻取、原始路径、通知、缩放和动画。Icicle可用LeafTotal或Inclusive；Flame默认Inclusive，父成本必须覆盖子成本，差额是自身成本。Dendrogram展示调用方给定的聚类树，DistancePath非null时校验非负且父高不小于子高，不自行计算聚类。

地图新增 `MarkerLayer`、`LineLayer`、`VectorLayer`、`PieLayer`、ShapeLayer的Categorical颜色和SeatMap多选。Marker是固定DIP符号，业务值可为有限负数；Line按业务连接拆日期线，曲线是有界分段的屏幕二次曲线，不是测地线。Vector接受Line/Polyline/Polygon含空洞/Circle/Arc类型，圆/弧半径为屏幕DIP。Pie按每个位置的非负分量分配角度，零分量不绘制，位置行和原始slice来源分别保留。分类色通过CategoryPath/CategoryColors绑定；`SeatMap.IsMultiSelectionEnabled` 配合TwoWay `SelectedItems`、`ToggleSeat(idOrSource)`、`ClearSeatSelection()`，集合内容是原始区域业务行。

这些入口覆盖本轮软件主项；完整官方嵌套RankingsPath/布尔ValuesPath适配、词云旋转/更紧密布局、Bullet竖向、更多专用样式/模板/共享C2交互及正式性能/物理平台资格仍开放。C5七类统计基础缺项现已补齐；完整高级能力、既有LOD原生AV、百万点命中尾延迟和A11Y-NATIVE-01不因这一批接入而关闭。目录状态见[开发计划](../../docs/charts-development-plan.md)与[官方对照](../../docs/charts-official-alignment.md)。

## C6-03 离线地理图与分层地图

`ShapeMap` 按 `Layers` 顺序组合 `ShapeLayer`、`BubbleLayer` 和 `HeatmapLayer`，覆盖形状地图、区域着色、地理气泡和地理热力；`SeatMap` 使用平面坐标显示座位、楼层和自定义区域。`GeoJson` 接受 Polygon/MultiPolygon 字符串，`FeaturesSource` 接受类型化 `MapFeature`；polygon 可带空洞，同一 feature 可含多个离散 polygon。区域按唯一业务键关联，缺失/null 值只绘制不可选择的无数据形状，未匹配业务行保留在 `UnknownRegions`。Tooltip、键盘和选择返回原始业务行；热力格保留全部原始贡献成员。

```csharp
using NT.AvaloniaControl.Charts;

var map = new ShapeMap
{
    Projection = MapProjection.Equirectangular,
    SourceUri = new Uri(Path.GetFullPath("Resources/local-areas.geojson")),
    ShowLegend = true,
};
var regions = new ShapeLayer
{
    ItemsSource = new[] { new MapValue("area-a", 64), new MapValue("area-b", 28) },
};
map.Layers.Add(regions);
map.Layers.Add(new BubbleLayer
{
    ItemsSource = new[] { new MapObservation("site-a", new MapCoordinate(12, 34), 16) },
    MinimumRadius = 0, MaximumRadius = .06,
});
// 在 UI 线程调用并异步等待；SourceUri 不会自动下载或隐式加载。
bool accepted = await map.LoadSourceAsync(cancellationToken);
map.SelectItem(regions, "area-a");
```

`SourceUri` 只支持显式本地 file / avares URI；网络 scheme 拒绝。来源优先级为 `FeaturesSource`、`GeoJson`、已加载的 `SourceUri`，切换到文件来源前需清空更高优先级属性。`CancelSourceLoad()`、取消令牌、较新的加载和卸载阻止旧结果发布；失败清空旧地图并公开 `SourceState` / `SourceError`，修复来源后可重新加载。数据集合与属性仍应在 UI 线程更新，几何投影和绘图并未整体移入后台。

`Equirectangular`、`WebMercator` 使用经度/纬度度数和北向正 Y，Mercator 在 ±85.0511287798066° 截止并报告夹取数量；`Identity` 使用任意有限平面 X/Y。`SeatMap` 使用非地理 Identity 设置。所有可见图层共享等比例 fit 和 `Zoom` / `PanX` / `PanY`。地理环按相邻点最短经度弧处理日期变更线，刻意超过180°的边需预切分。`TopologyRule` 默认 `StrictSimple`，拒绝自交；已知自交数据可显式选 `EvenOdd` 并检查 `ProjectedGeometry.SelfIntersectionCount`。这不会使零有向面积、无效空洞或预算超限变为有效。Demo 自带完整177 feature的 Natural Earth 离线资源，公开来源、许可、转换和SHA位于 `samples/NT.AvaloniaControl.Demo/Assets/Maps/provenance.json`；该资源显式采用 EvenOdd。

气泡 `MinimumRadius` / `MaximumRadius` 是拟合区域短边的归一化半径；最小半径为0时面积与非负值成比例，0值不绘制。热力采用有界格网和可选离散 Gaussian 核，边界核重新归一化，原始贡献身份不随格网聚合丢失。默认上限为5000 feature、200000原始顶点、每环4096顶点、20000观测，GeoJSON16MiB / 深度32。整图在任何热力布局前按每个可见热力层 `rawCount × (2×radius+1)²` 累计检查 `ChartMaps.MaximumRetainedHeatmapContributions = 1_000_000`，零值也计费；该保守准入不是实际字节预算。隐藏或Opacity0图层保留 `LayerSnapshots` 的层序号，三个数值结果为null且不执行布局，不参与共享fit。详见[开发报告](../../docs/charts-c6-geographic-maps.md)。Marker/Line/Vector/Pie基础叠加已在上方C6-06接入；完整官方样式/模板/API、地图瓦片、物理平台和正式性能资格仍开放。

## C6-01 / C6-02 层级与关系图

层级控件为 `TreemapChart`、`SunburstChart`、`OrganizationChart`、`BubbleCloudChart`、`PackedBubbleChart` 和 `CirclePackingChart`，数值入口为 `ChartHierarchy`。关系控件为 `SankeyChart`、`NetworkChart`、`ChordDiagramChart` 和 `FlowChart`；通过模式覆盖 Sankey、Alluvial、Network、Force-directed、Arc、Chord、FlowChart、Process 和 Mindmap，数值入口为 `ChartRelations`。节点和边分别保留原始数据身份，后台布局支持取消及过期结果抑制。具体预算、循环/多边和力导向边界见[层级报告](../../docs/charts-c6-hierarchy-packing.md)与[关系报告](../../docs/charts-c6-flow-relations.md)。

## C5-03 技术指标、趋势与移动平均

`MovingAverageSeries` 支持 Simple、Exponential、Weighted 和 Triangular；`ChartTrendlineSeries` 支持 Linear、Polynomial、Exponential、Logarithmic、Power 和 MovingAverage。设置 `SourceSeries` 可关联已有系列，也可通过自身 `ItemsSource` 和映射路径独立输入；两者同时设置时只观察和计算 `SourceSeries`。附加趋势放入源系列的 `Trendlines`，SMA/EMA/WMA/Bollinger 指标放入图表的 `TechnicalIndicators`。关联系列默认继承来源坐标轴，并在完整历史映射后计算，缩放不以可见数据重新初始化窗口。

```csharp
var price = new LineSeries { ItemsSource = observations };
chart.Series.Add(price);
chart.Series.Add(new MovingAverageSeries
{
    SourceSeries = price, Type = ChartMovingAverageType.Exponential, Period = 14,
});
price.Trendlines.Add(new Trendline { Type = ChartTrendlineType.Linear, ForwardForecast = 3 });
chart.TechnicalIndicators.Add(new BollingerBandsIndicator
{
    SourceSeries = price, Period = 20, StandardDeviationMultiplier = 2,
});
```

`ChartAnalytics` 提供可取消的独立数学入口。均线保持输入顺序、X 和长度，完整窗口之前输出 NaN，非有限 X/Y 重置预热；EMA 以窗口 SMA 为种子，alpha 为 `2/(period+1)`；WMA 的最新值权重最大；TMA 为两次 SMA，周期依次 `ceil(period/2)` 与 `floor(period/2)+1`，奇偶周期均需 period 个原始观测。布林带使用 SMA 和总体标准差（除数为 period），上下轨为中轨加减可配置倍数；`UpperSeries`、`LowerSeries` 和 `BandSeries` 可独立配置画刷、图例和可见性，中轨为指标本身。

回归对全部适用观测进行一次全局拟合，使用中心缩放和 QR 求解；多项式阶数为 1–10。非有限观测被略去，指数拟合还要求 Y>0，对数要求 X>0，幂拟合要求 X>0 且 Y>0，遗漏量可从 `FitResult.OmittedCount` 查询。指数/幂在 log(Y) 域拟合，R² 在原始 Y 域计算，常量 Y 的 R² 为 null。采样区间默认为适用 X 范围；`BackwardForecast`/`ForwardForecast` 按映射后 X 单位扩展，显式 `Minimum`/`Maximum` 优先。移动平均趋势不提供预测。静态 API 对样本不足、秩亏、非法参数、不能表示的结果或取消明确失败；系列趋势通过 DataError 报告无效拟合并清空旧结果。

均线命中保留窗口末端的真实来源、原始 `RawPoint` 和 `Analysis.CalculatedPoint`；拟合采样点的 `Analysis.IsSynthetic` 为 true，没有冒充某条原始观测。数据替换、集合追加、业务属性通知和参数变化会更新关联结果，分析消费者移除或卸载且没有剩余依赖时释放观察；仍被分析系列引用的来源继续保留绑定上下文和观察；当前追加仍重新计算完整历史，未承诺增量常数时间或金融性能 SLA。静态输入最多一百万行，周期最多65536，回归采样2–4096；同步映射/数学工作、实际内存、长期稳定性及官方 API/运行时等价仍需后续验收。金融叠加可直接引用 `FinancialChart.PriceSeries`，使用其交易槽及价格轴；高级交易所日历仍遵循 C5-02 边界。Demo“技术指标与趋势”和 Usage 展示接入方式，验证见 `docs/charts-c5-technical-trends.md`。

## C5-02 金融价格与交易槽

`FinancialChart` 组合一个公开 `Chart: CartesianChart`，共享交易槽、日期标签、价格/成交量双轴和默认系列；通过 `SetData(IEnumerable<ChartFinancialPoint>)` 提交有序快照，使用 `ApplyTransform` 重新计算 Heikin-Ashi、Renko、Kagi 或 Point & Figure。`Chart` 提供既有 Tooltip、选择、缩放、平移、标注和导出接口。宿主不自动观察业务集合，也不自动合并不同资产的日期；改动后再次调用 `SetData`。`MapTime` 精确映射已有交易日期，`ProjectOverlay` 将整数 Unix 毫秒 X 映射到同一交易槽，未知日期形成缺口。周末不凭自然时间长度拉开间距，交易所节假日/盘中会话需由调用方提供。

`CandlestickSeries`、`OhlcSeries`、`HiloSeries`、`HeikinAshiSeries`、`RenkoSeries`、`KagiSeries`、`PointAndFigureSeries` 可直接放进 Cartesian 图；它们使用交易槽坐标，不能把 X 当作 DateTimeAxis 的自然毫秒值。`FinancialSeries` 可配置路径、样式、转换、涨跌/中性颜色、箱宽、反转阈值和输出预算。`FinancialVolumeSeries` 使用独立数值轴。日期路径支持 `DateTimeOffset` 及 Kind 明确的 `DateTime`；未指定时区的 DateTime 和字符串明确拒绝，跨平台推荐 DateTimeOffset。

```csharp
var chart = new FinancialChart();
chart.SetData(new[]
{
    new ChartFinancialPoint(new DateTimeOffset(2026, 10, 2, 9, 30, 0, TimeSpan.Zero), 10, 14, 8, 12, 100),
    new ChartFinancialPoint(new DateTimeOffset(2026, 10, 5, 9, 30, 0, TimeSpan.Zero), 12, 15, 9, 10, 200),
});
chart.ApplyTransform(ChartFinancialTransformKind.HeikinAshi);
```

`ChartFinancialTransforms` 提供独立不可变数值结果。UTC 时刻必须严格递增；原始 OHLC 必须有限且满足 Low ≤ Open/Close ≤ High，成交量可空或非负，允许负价格。显式 gap 重置递推；系列映射的无效价格报告 DataError 并形成缺口，不补零或静默排序。HA 每段以原始开收均值作为初始 open，后续 open 用前一 HA 开收均值；close 是当期 OHLC 均值。Renko、Kagi、P&F 使用明确的收盘价/固定阈值规则，不是 ATR、百分比或盘中高低价路径推断。

`ChartPointHit.Financial` 同时保留显示 OHLC、真实 SourceEnd、来源起止序号和关联 Sources。Raw/HA 的 Index 保留原始行序号；非时间图的 Index 为显示图元序号。HA 的关联输入是当期行，不声称列出递推的完整因果历史。派生 volume 元数据为来源末行值，宿主隐藏非 Raw/HA 的成交量条，避免把重复值当作聚合成交量。输入最多一百万行，默认最多十万生成项，可显式提高到一百万；P&F 按 X/O 总数计费。超预算、不可表示的价格推进或溢出明确失败，不修改箱宽或截断结果。Demo 的“金融价格”节展示七图及日期对齐叠加，详细规则和验证见 `docs/charts-c5-financial-prices.md`。

## C5-01 统计与分布

`BoxPlotSeries` 按 `SamplesPath` 读取每组原始数值，或用 `SummaryPath` / 直接 `ChartBoxPlotSummary` 读取预聚合摘要；支持水平/垂直方向、均值、离群点和原始组命中。`ErrorBarSeries` 支持 `ErrorPath` 对称误差、`LowErrorPath` / `HighErrorPath` 非对称误差、`IntervalPath` 绝对区间、`ErrorMode` 双侧/正向/负向及 DIP 端帽。`HistogramSeries` 使用真实分箱边界，`DensitySeries` 绘制估计密度样本。四种系列复用 Cartesian 坐标轴、多轴、缩放、选择和导出。

```csharp
var bins = ChartStatistics.Histogram(samples, 12, ChartHistogramNormalization.Density);
chart.Series.Add(new HistogramSeries { ItemsSource = bins.Bins });
var density = ChartStatistics.KernelDensity(samples, 129, bandwidth: 1.2);
chart.Series.Add(new DensitySeries { ItemsSource = density.Points, FillUnderCurve = false });
```

`ChartStatistics` 提供 Type 7 分位数、Tukey 箱线、描述统计、误差区间、均值正态近似区间、直方图、Gaussian KDE、ECDF 和 Pearson 相关。直方图内部区间左闭右开，末箱包含右端点；支持计数、概率和单位面积密度。非有限观测被略去，空样本、非法参数和不可表示的计算结果明确拒绝。KDE 的自动带宽采用 Silverman 规则及明确的常量后备，有限显示域积分保留实际值，不强制为 1；计算量为样本数乘采样点数。均值区间估算样本标准差，使用正态近似，不提供小样本 Student-t 覆盖保证。

箱线命中返回原始组，离群点的组内原始序号位于 `ChartPointHit.BoxPlot.Outliers`；直方图命中返回原始 bin，密度命中返回原始密度采样点。聚合输出不能冒充某一条原始观测。嵌套样本集合原地更改后，应通知组属性变更或调用 `Refresh()`；数据映射错误通过 `DataError` 报告并保留缺口。Demo 的“统计与分布”节和 `samples/Charts.Usage` 展示这些功能与绑定用法。

Avalonia 12 的独立二维图表：折线、面积、范围面积、柱/横条、范围柱、瀑布、Pareto、Dot、Lollipop、Variance、散点、散点连线和 XY 气泡、圆形图、极坐标/雷达、六类仪表与四模式微图、漏斗/金字塔/Heatmap 和图形柱，支持大小/颜色编码、平滑/阶梯插值、堆叠与百分比、对象映射、多类轴/多轴、图例/标签/标记、共享读数、选择、标注、动画、PNG/JPEG 导出及缩放平移。

### 自动化性能门禁（C4-08）

CI 入口为 `scripts/Invoke-ChartsPerformanceGate.ps1`，包括七组 C4 机制、固定 Headless benchmark 与真实原生窗口的视图/数据/键盘驱动。PR 在独立 base checkout 中用相同候选驱动构建旧产品，检查同环境的计时、分配及像素差异；其他事件使用标明用途的同版本双进程对照。策略固定在 `scripts/charts-ci-performance-policy.json`，运行前保存副本，不自动接受候选结果作为新基线。失败/超时、原始请求与数据、PNG及差异图、JSONL、日志和SHA清单都保留并在CI上传。

原生窗口记录的是受控栅格捕获，Headless 记录的是 DrawingGroup，两者均不代表屏幕呈现或 GPU 完成。托管 runner 的回归门槛与计划中的固定硬件正式 SLA 分开记录；实际进程内存、长期稳定性、物理设备和官方运行时对照仍需单独验收。

原生 benchmark 显式关闭分层 JIT 编译，并核对实际 runtimeconfig、AppContext 与环境覆盖值；此设置仅作用于测量驱动，不改变控件使用方应用的配置。默认 JIT 下已观测的同源码双进程差异失败保留；新协议不调整数值门槛，也不据此认证默认应用的 JIT 行为或宣称原因已定位。

### 多图调度与后台数值布局（C4-07）

`RequestRender()` 将刷新请求合并到进程共享队列，已排队图表按 FIFO 各取得一次失效通知机会；可从后台线程调用，数据与属性仍应在 UI 线程更新。`GetWorkSchedulingInfo()` 报告本图请求、合并和派发次数。它们是刷新派发计数，不是实际呈现帧数，也不保证任意复杂图表的 UI 工作耗时相等。自身或祖先隐藏、卸载时取消自动工作并清理本地资源；重新显示后按最新数据准备。调用方显式查询或导出仍可能执行同步准备。

`UseBackgroundLayout = true`（默认关闭）允许内置平滑/阶梯折线、面积、范围面积、散点连线和平滑堆叠/百分比的数值路径准备使用共享后台 worker。`PrepareBackgroundLayoutAsync()` 等待当前版本，返回 true 表示已发布；false 表示取消或同步回退。`CancelBackgroundLayout()` 主动取消，`GetBackgroundLayoutInfo()` 提供版本、捕获/发布、取消、失败和回退诊断。一个等待者的 CancellationToken 会取消本图当前共享请求。源、视图、样式失效或隐藏/卸载都会取消，旧版本不能发布到新场景。等待期间支持的路径暂不绘制；导出强制同步准备完整路径。

后台只运行捕获好的数值算法；业务映射、可见数据查询、投影、坐标轴/文字和原生图形仍在 UI 线程。不支持的系列、自定义投影、动画、脱离视觉树或预算不足采用同步路径。单场景最多128个候选系列、131072个原始顶点，且在捕获前按保守模型预留容量。

`ChartWorkScheduler` 的默认共享预算为本地五层资源估算512 MiB、在途数值载荷估算64 MiB、最多16个任务预留，同时最多2个 worker。`ResourceCacheBudgetBytes`、`BackgroundPayloadBudgetBytes` 和 `MaximumBackgroundJobs` 可在 UI 线程配置；`GetBudgetInfo()` 提供进程诊断，`TrimResourceCaches()` 清理已登记的本地缓存。降低在途预算会取消任务，尚未退出的数值工作继续计费并阻止新准入，因此下降瞬间可能仍超过新值。进程资源账本实际参与五层缓存准入与淘汰，但不包括既有共享索引/路径缓存、业务对象、原生/GPU或整个进程实际内存。

### 绘图复用与资源预算（C4-06）

`CartesianChart.ResourceCacheBudgetBytes` 为本图表的本地绘图、轴文字、交互文字、轴画笔与线段缓存提供共享 LRU 准入预算，默认 128 MiB，设为 0 禁止保留这些缓存。超过预算的项目仍正常绘制，本次结果不进入缓存；降低预算会立即淘汰旧项。`GetResourceCacheInfo()` 返回各层的条目、逻辑估算值与命中、旁路、淘汰次数，`TrimResourceCaches()` 主动清理可重建的本地资源，保留数据、视图和选择。卸载时释放本地资源。画笔保留原始可变 Brush/DashStyle 引用，修改同一对象仍反映在后续绘图中。

预算采用公开诊断中的模型版本，以保留数组、图元、文字和条目的固定权重计费，五层估算之和受预算约束；它不是托管堆、进程或原生/GPU资源的实际字节硬上限。共享源/系列/上下文缓存（含命中、LOD、预备路径和柱形几何）、调用者业务对象、字体缓存和绘制时临时工作另行统计。线性线段的屏幕投影备忘最多保留每系列4096个端点，淘汰只影响重复查询成本，原始端点与命中规则不变。预算不足或关闭缓存会增加重建与分配成本。

`ChartDataBuffer<T>.StoragePageCount` 报告当前窗口实际持有的底层页数，`Compact()` 在 UI 线程显式压实碎片。压实按当前顺序复制保留数据及已有映射，不重读业务 getter，保持 Item、StableId、当前坐标及旧快照；有效压实发布一次版本和通知，不计作删除或容量淘汰，已紧凑时返回 false。压实有 O(N) 复制与临时内存成本，旧快照仍可持有旧页，因此不在每次追加时自动执行。

### 增量数据与实时调度（C4-05）

`ChartDataBuffer<T>` 提供 `AppendRange`、`InsertRange`、`RemoveRange`、`ReplaceRange` 和 `Clear`。构造时设置容量，超过容量淘汰最早的逻辑行；`EvictedCount` 与 `RemovedCount` 分别累计容量淘汰和显式移除。源在 UI 线程修改，快照共享不可变数据页，旧快照可继续读取；保留项的 `Item` / `StableId` 不变，`SourceIndex` 和隐式 X 随当前窗口重定位。对象本身仍由调用方拥有，保留快照也会保留其旧页和对象引用。

```csharp
// 在 UI 线程创建；后台生产者逐次 await AppendAsync，接受后再复用输入数组。
var source = new ChartDataBuffer<ChartPoint>(capacity: 250_000);
chart.Series.Add(new LineSeries { ItemsSource = source });
chart.FollowLatestWindow = 512; // 主 X 轴的数据单位，DateTimeAxis 使用 Unix 毫秒。
chart.FollowLatestData = true;
using var session = new ChartStreamingSession<ChartPoint>(batch =>
{
    using (chart.DeferDataUpdates()) source.AppendRange(batch);
}, maximumBatchSize: 500, maximumPendingBatches: 2);
await session.AppendAsync(new[] { new ChartPoint(0, 12), new ChartPoint(1, 15) });
await session.FlushAsync(); // UI 线程应异步等待，保持 Dispatcher 运行。
```

`ChartStreamingSession<T>` 以 FIFO 接收复制后的批次，最多保留两个已接受批次（包含正在执行的批次），最多一个生产者等待容量。`TryAppend` 满载或关闭时返回 false 并计入拒收；`AppendAsync` 等待容量，额外等待者明确拒收。接收确认与 UI 提交不同，`FlushAsync` 等待调用前已接受的批次提交，`CompleteAsync` 关闭接收并恢复、排空待提交数据。`Pause` / `Resume` 控制提交，暂停期间仍接收到容量上限；`Cancel` / `Dispose` 取消队列中尚未执行的数据，正在执行的回调可能完成。等待令牌只取消等待，已接受数据由会话生命周期管理。

`Statistics` 分别报告接受、提交、取消、失败、拒收和待处理数量，以及合并批次、队列峰值和等待者数量；提交表示回调成功返回，不表示画面已呈现。拒收按调用尝试的样本数计数，同一数据重试可重复计入拒收，唯一生产数量由生产者另行记录。回调异常可通过 `Error`、`Completion` 和等待任务观察，整个失败合并批次计入失败且不重试；回调可能已产生部分业务修改，因此不是事务。批次数组只浅复制，业务对象的线程约束不变。

`DeferDataUpdates()` 可嵌套，将多系列源变化合并为一次选择核对和刷新请求，离开最外层作用域提交；作用域内查询仍能读取最新数据。它不延迟任意轴或属性写入。`FollowLatestData` 独立控制主 X 轴尾随窗口，跟随主轴有效数据的最大 X；数值、日期和对数轴遵循各自的域和视图约束，分类和自定义轴沿用原视图策略。关闭跟随冻结当前窗口，显式视图设置、平移或缩放暂停跟随；恢复后追上最新数据。Demo → Charts →“增量数据与实时调度”展示四路有界数据、暂停恢复、删除替换和独立跟随开关。

图表挂接并保持活跃订阅、映射配置不变时，新增页的复制、默认空值策略下的映射和数值摘要可复用旧页；未订阅的对象系列仍在读取快照时重新映射，以观察无通知的对象修改。目录仍需 O(P) 工作（P 为页／切片数），频繁小批次或中间编辑会增加碎片。普通 `ObservableCollection`、邻点空值修复、可变对象及映射属性变化保留保守重建；堆叠、复杂投影、LOD 和二维命中索引也未全部增量化。页整理与本地缓存估算预算现由 Compact 和 ResourceCacheBudgetBytes 提供；自动压实、共享缓存及进程/原生/GPU字节预算和长期内存验证继续留后续性能门禁；短时加速回归不代表实时吞吐、帧呈现或长期运行资格。

### 高效命中与原始身份（C4-04）

稳定的内置笛卡尔投影下，普通点的短 X 候选窗口沿用二分路径，密集窗口复用原始坐标二维树；相同 X／Y 的密集共享读数按屏幕主轴距离、另一轴距离和原有并列规则查询同一棵树。编码散点和气泡复用当前视图的真实标记轮廓，并在密集候选中使用二维树；同距保留 `DrawOrder` 的最后绘制项。普通点和柱形同距保留最后物理行，Trackball 原始点及线段仍保留原来的先后规则；这些规则不按业务 `SourceIndex` 大小重新排序。

线性 `LineSeries` 与 `ScatterLineSeries` 的线段命中和 X／Y 插值查询原始相邻线段树，按需投影候选端点，保留断点和视口裁剪。MinMax 显示抽样不参与此索引，Tooltip、选择和插值端点继续返回原始 `Item`、`Id`、`SourceIndex` 和 `RawPoint`。每个系列使用其绑定轴的投影，因此多轴读数仍在同一屏幕坐标中比较。

普通柱和比较系列共用当前视图的几何布局，绘制、精确命中和空间候选使用相同矩形、茎线及标记位置。索引为内部实现，不增加公开 API。Demo → Charts →“高效命中”展示重叠点、相同 X、多轴、抽样遗漏点、原始线段和分组柱，并显示真实业务身份。

索引首次构建仍有源规模相关成本，暖查询的加速取决于候选分布。大量跨越视口的线段、无效投影域、复杂重叠或共享读数的裁剪组可能退化；自定义可变投影及平滑、阶梯、面积等派生线段保持保守路径。缓存总字节预算、增量更新、长期内存和百万点尾延迟 SLA 留后续阶段验证；本阶段的工作量门禁不代表所有输入均为 O(log N)。

```csharp
using System.Collections.ObjectModel;
using NT.AvaloniaControl.Charts;

var values = new ObservableCollection<ChartPoint>
{
    new(0, 18), new(1, 32), new(2, 25),
};
var chart = new CartesianChart
{
    Title = "每月销售额",
    Height = 320,
    IsTooltipEnabled = true,
    IsZoomEnabled = true,
    IsPanEnabled = true,
    InteractionAxes = ChartInteractionAxes.Both,
    XAxis = new ChartAxis { Title = "月份", Labels = new[] { "一月", "二月", "三月" } },
    YAxis = new ChartAxis { Title = "万元" },
};
chart.Series.Add(new LineSeries { Title = "销售额", ItemsSource = values });
// BarSeries / ScatterSeries 使用相同的数据接口。
```

### 散点、源序连线与 XY 气泡

`ScatterSeries` 支持逐点 `SizePath` / `ColorPath`；`ScatterLineSeries : PointSeries` 在同样的编码底座上提供 `ShowLine`（默认 true），按真实源顺序用直线连接相邻有效 XY，不按 X 重排，重复 X 仍为真实路径。无效 XY 形成断段，缺失/负大小只隐藏标记，不切断仍有效的轨迹。`BubbleSeries : PointSeries` 保持真实 XY，将第三值映射为标记大小；`SizePath` 优先，未设置时兼容既有 `WeightPath`，两者均未设置则使用固定 `MarkerSize`。无轴气泡云和紧凑打包属于后续 C6。

```csharp
chart.Series.Add(new BubbleSeries
{
    ItemsSource = new[] { new { Id = "a", X = 2d, Y = 25d, Size = 0d, Heat = 0d },
                         new { Id = "b", X = 5d, Y = 55d, Size = 50d, Heat = 50d },
                         new { Id = "c", X = 8d, Y = 75d, Size = 100d, Heat = 100d } },
    CategoryPath = "X", ValuePath = "Y", IdPath = "Id", SizePath = "Size", ColorPath = "Heat",
    MinimumSize = 20, MaximumSize = 60, SizeMinimum = 0, SizeMaximum = 100,
    SizeScale = ChartSizeScale.Area,
    ColorMinimum = 0, ColorMaximum = 100, MinimumColor = Colors.Blue, MaximumColor = Colors.Red,
});
```

尺寸是 DIP 直径，缩放 XY 视图不改变直径。`MinimumSize` / `MaximumSize` 默认 4 / 40，Bubble 默认 0 / 60；`SizeMinimum` / `SizeMaximum` 可显式固定输入域，否则使用 0 和有效源的最大大小值，映射域不随视口变化。`ChartSizeScale.Area`（默认）在直径平方之间插值再开方，`Diameter` 在线性直径之间插值，域外值限制到端点。零映射到最小直径；缺失、NaN、负大小不绘标记，但数据的 XY、浏览和原始身份仍保留。默认 Bubble 的零直径不可见。

`ColorPath` 支持数值、Avalonia `Color`、`IBrush` 或可解析颜色字符串。数值按 `ColorMinimum` / `ColorMaximum`（省略时用有效源最小/最大值）在 `MinimumColor` / `MaximumColor` 的 sRGBA 通道之间线性插值并限制到端点；无效颜色回退到系列填充。有效 `ColorPath` 编码优先，未提供或无效时回退到 `MarkerFill`、`Fill` 或系列默认画刷。编码基于完整目标数据，不因缩放和可见范围改变颜色或尺寸含义。

`DrawOrder` 为 `Source`（默认）、`SizeDescending` 或 `SizeAscending`；相同大小保持源序。它只改变绘制/重叠顺序，不改变业务集合、源索引、标记中心或 ScatterLine 连线顺序。点命中检查可见标记形状，形状内部距离为 0，同距时后绘制者优先，外部使用到标记轮廓的距离；绘图区外中心的形状仍可在进入绘图区的部分命中。范围选择和键盘浏览继续按真实点中心及源身份工作。

`ChartDataPoint` 与 `ChartPointHit` 的 `SizeValue` / `ColorValue` 为原始映射值，`MarkerDiameter` / `PointFill` 为显示派生值，可在 Tooltip、数据标签和辅助语义模板中区分两者。`Point` 保持真实 XY。ScatterLine 参与 `HitTestSegment`、插值 Tooltip 和 Trackball，估算读数保留真实两端项；Bubble/Scatter 保持真实点读数。默认 Scatter 未设置编码/绘制排序时保留既有绘制路径。Demo → Charts →“散点与气泡”提供六图、面积/直径尺度、重叠排序、标签与连线开关；`samples/Charts.Usage` 展示真实编译绑定及属性变化。新编码、排序和命中成本需在 C4 单独验证，不代表百万点性能资格。

### 圆形图

`CircularChart` 与笛卡尔坐标宿主独立，`Series` 可添加 `PieSeries`、`DonutSeries` 或 `SemiDonutSeries`。圆形系列复用 `ItemsSource` / `CategoryPath` / `ValuePath` / `IdPath` 投影，`ColorPath` 可提供逐项 `Color`、画刷或颜色字符串。`GetSlices()` 返回当前帧 `CircularSliceInfo`；过渡期间 `Data`、`Fraction` 和原始分配角度取最新目标，绘制角度、中心、半径和实色填充取当前帧。原始业务项、源索引、稳定 ID 与带符号数值保留；退场项带 `IsExiting`，不参与命中或选择。`HitTest()`、`SelectSlice()` 与 `SelectPoint()` 沿同一当前扇区几何工作。

零值和非有限值不分配角度；负值由 `ChartCircularNegativeValueMode.Ignore` / `Absolute` / `Throw` 显式决定。Absolute 只改变份额计算，原始负值仍保留。`StartAngle` 从右向射线起算，顺时针为正；`SweepAngle` 控制完整或部分弧，`Clockwise` 改变方向。`Radius` 是可用布局半径的比例，`InnerRadius` 是本系列外半径的比例，多系列可组合为独立归一化的多环。`SliceGap` 只收缩绘制角度；小于 `MinimumSliceAngle` 的扇区可 Keep 或 Hide，隐藏不重新分配其份额。`ExplodedIndex` 使用原始源索引，`ExplodeOffset` 使用 DIP。

`LabelPosition` 支持内标签、带连线的外标签及关闭；`CenterText` 可用于环图中心。选择复用 `ChartSelectionMode` 与可取消的选择事件，过渡使用既有动画时长、缓动、延迟、暂停、取消和减少动态效果契约。PNG/JPEG 同步和异步导出使用 `ChartExportOptions`。Demo → Charts →“圆形图”展示饼、环、半环、逆时针部分环、多环和小扇区，可操作数据变化、负值策略、标签、选择分离与动画；Usage 和隔离包消费者提供编译绑定。圆形图的完整无障碍、极端标签密度、性能、真实输入设备与物理平台资格仍需专项验收。

### 极坐标与雷达

`PolarChart` 提供独立的角度/半径投影；`RadarChart` 默认采用多边形网格。`Series` 或可绑定 `SeriesSource` 接受八类系列：数值角度的 `PolarLineSeries`、`PolarScatterSeries`、`PolarFilledSeries`，分类角度的 `RadarSeries`、`RadialLineSeries`、`PolarAreaSeries`、`NightingaleRoseSeries` 和 `RadialBarSeries`。`AnglePath` / `RadiusPath` 分别是共享 `CategoryPath` / `ValuePath` 的别名；`IdPath` 保留业务身份，`ColorPath` 接受逐项颜色、画刷或可解析颜色字符串。`GetPoints()`、`GetSectors()` 与 `HitTest()` 返回同一场景中的原始业务数据和显示位置。

`AngularAxis`、`RadialAxis` 当前支持线性数值范围、反向、主要间隔、文字格式和标题；`AngleUnit` 支持 Degrees / Radians，数值角度以轴最小/最大值归一化，再由 `StartAngle`、`SweepAngle` 与 `Clockwise` 映射为画面角度。起点默认 -90°，默认顺时针完整周期；`StartAngle` 和 `SweepAngle` 始终以显示度数表示。`WrapAngles` 控制超出数值角度域的周期折返。负半径和非有限值不参与绘制；线按源顺序连接，缺失项形成断点，`IsClosed` 控制闭合。

分类系列按全部可见分类系列的首见类别并集分配等角槽，同名类别跨系列共用射线；源顺序、源索引和业务对象不随类别重排而改写。雷达 `MaxValue` 默认 100，设为 null 使用共享半径轴范围。`GridShape` 可选 Circle / Polygon，`GridLevels` 或半径轴 `Interval` 控制环层级；`GridLineBrush` / `GridLineStrokeThickness` 配置绘制样式，`ShowAxisLabels` 与 `ShowGridLines` 分别控制文字和网格。

`PolarAreaSeries` 使用等角扇区，半径按 `sqrt(inner² + (outer²-inner²)×归一化值)` 计算，使有效环形面积与数值比例一致；`NightingaleRoseSeries` 使用线性半径。`RadialBarSeries` 为共享类别分别分配同心轨道，弧长比例表示各项数值。三者的零、负和无效值不绘制，`InnerRadius` 是可用半径比例，`SectorGap` 以度数收缩绘制角度且不改写原始值。

同构极面积或玫瑰系列的半径刻度使用相应面积/半径公式，含 `InnerRadius`，使刻度值与扇区外沿对应；分类名称位于扇区中角。径向条将类别文字放在各自同心轨道，数值刻度沿角度表示进度。不同半径编码、不同内孔或不兼容最大值混合时，网格采用明确的物理半径参考 `r/R` 与显示角 `θ`，并显示“归一半径参考”；不把一条参考圈标成彼此不同的业务数值。Demo 和 Usage 提供极面积叠加数值折线开关，展示这种参考轴组合。

`GridShape.Polygon` 用于点系列、雷达和径向线的顶点参考网格。极面积/玫瑰的数值圈、径向条轨道及混合编码的物理 `r/R` 参考始终使用真圆或部分圆弧，使参考半径在所有角度保持一致；这些图族不将 `GridShape.Polygon` 请求解释为数值等值多边形。

`SelectionMode`、双向 `SelectedPoint` / `SelectedPoints` 与可取消选择事件使用真实源身份；基本 Tooltip、键盘浏览、标签和 PNG/JPEG 同步/异步导出沿本投影实现。Demo → Charts →“极坐标与雷达”包含八图、度/弧度转换、方向、网格、闭合、数据更新与雷达导出；Usage 和两类隔离包消费者覆盖编译绑定。本批未将笛卡尔坐标的全部 C2 模板、动画、缩放、Trackball、范围联动、标注和点级无障碍自动扩展到极坐标。非线性轴、断轴、完整官方 API/图族组合、复杂标签布局、C4 性能和物理平台资格仍待专项。

### 仪表与微图

`CircularGaugeChart`（圆弧）、`GaugeChart`（半圆）、`GradientRingChart`（多环）、`LinearGaugeChart`（横向 / 纵向）、`LiquidFillGaugeChart`（圆形 / 矩形液体）和 `ProgressDonutChart`（进度环）继承 `GaugeBase`。共享 `Minimum` / `Maximum` / `Value`、可空 `TargetValue`、画刷和当前值属性。`Ranges` / `RangesSource` 区间仅由圆形、半圆和线性仪表绘制；其他图族继承该集合 API，但不绘制区间，刻度显示也按具体图族提供。有效数值域要求有限值且 `Maximum > Minimum`；`NormalizedValue` / `NormalizedTarget` 是可空、限制在 0–1 的显示派生值，原始输入不被改写。无效域或非有限值不绘数值，轨道仍可显示。范围 `GaugeRange.Start` / `End` 使用同一业务数值域。

多环通过 `GaugeValue` 的 `Value` / `Label` / `Brush` 与 `Values` / `ValuesSource` 设置独立环，所有环共享宿主数值域。`RingSpacing` 使用 DIP；画刷可使用 Avalonia 梯度。线性仪表支持 `Orientation`，`Clockwise` 反转进度方向。液体 `Shape` 支持 Circle / Rectangle，`WaveAmplitude` / `WaveFrequency` / `WavePhase` 配置波形；默认 `IsWaveAnimationEnabled=false`，显式启用后可用 `IsPaused` 暂停，`ReduceMotion` 停止动态波动。静态波形和当前帧导出使用相同绘制路径。

`SparklineChart.SparklineType` 提供 Line / Area / Bar / WinLoss 四模式。`ItemsSource` 可直接使用数值集合，或通过 `ValuePath` / `CategoryPath` / `IdPath` 映射业务对象；只读 `Series` 是实际命中来源。`GetPoints()` 返回业务身份、源槽位、原始值及派生显示位置，`HitTest()` 保留同一原始项。默认缺失策略下，缺失与非有限值保留源槽位并切断折线/面积；调用方也可通过 `Series.EmptyPointMode` 使用共享填补策略。集合和 INPC 更新可直接刷新。`Minimum` / `Maximum` 可固定域；面积和柱形自动范围包含 `Baseline`，WinLoss 依相对基线的正、负、零绘制固定符号格，不按幅度改变格高。`ShowMinMax`、`ShowFirstLast`、`ShowMarkers` 和对应画刷用于紧凑重点标记。

六类仪表和微图提供 PNG/JPEG 同步与异步导出，使用 `ChartExportOptions` 的尺寸、DPI、背景和取消契约。Demo → Charts →“仪表与微图”展示十卡、值与目标、区间、多环更新、方向、液体动态选项及 PNG 导出；Usage 展示真实编译 AXAML/MVVM 与卸载后的业务监听释放。完整 C2 模板 / 选择 / 联动 / 缩放、仪表数据过渡、复杂标签、原生无障碍、性能及物理桌面资格仍需专项验收，本批软件检查不代替这些资格。

### 柱条与比较

`BarSeries.Orientation` 默认 Vertical，Horizontal 时显示坐标为 `(value, category)`，X 为数值轴、Y 为类别维。`Baseline` 默认 0；`BarWidth` 为可空的固定 DIP 宽度，null 时按相邻类别间距和 `WidthRatio`（默认 .86）计算，分组系列共享类别槽。`StackMode` / `StackGroup` 沿用 None、Stacked、Percent，按相同轴、方向、组和类别对齐；正负分别累加或归一到 ±100。

`RangeBarSeries`、`WaterfallSeries`、`ParetoSeries`、`DotSeries`、`LollipopSeries`、`VarianceSeries` 继承 `ComparisonSeries`，共享方向、基线、宽度及排序配置。范围柱使用 `HighPath` / `LowPath`，无需 `ValuePath`，以两界 min/max 填充；Variance 保留 High/Low 的有向差，以两端点和连接线表示。Dot 只显示点，Lollipop 从基线连接到真实点。

瀑布按源顺序累计增减；`TotalPath` 指向布尔总计属性，总计行从 Baseline 到该行源值，并将累计重置到源值。缺失行保持断开且不改变累计；`PositiveFill`、`NegativeFill`、`TotalFill` 提供三类颜色，`ShowConnectors` / `ConnectorStroke` / `ConnectorThickness` 控制连接线。业务对象的 TotalPath 属性变化与其他映射属性一样订阅并更新。

`SortMode` 支持 Source、XAscending、ValueAscending、ValueDescending；XAscending 依据原始映射 X。值排序要求类别维使用自动 Labels 的 `CategoryAxis`，不支持在数值轴或手写 Labels 上仅改变绘制顺序，冲突配置明确抛异常。同轴首先声明的可见值排序系列决定类别槽，其他系列按类别和重复出现次序对齐。排序只改变显示槽，保留源集合、索引与业务身份。Pareto 默认 ValueDescending，非负有限项计算累计百分比，负值/缺失保留缺口，最后一个有效累计值为 100；累计线使用独立百分比轴，`CumulativeAxisKey` 可指定相容附加轴，省略时自动提供 Far 轴。

这些系列的 `Point` 为显示坐标，`RawPoint` 保留原始类别和值；`BaseValue` 是数值维基线，`StackValue` 为堆叠贡献或百分比份额，Pareto 还提供 `CumulativeValue`。横向、排序或累计后，请从 RawPoint 或原始 Item 读取业务值。标签可用 `DataLabelFormatter = context => (context.Data.RawPoint ?? context.Data.Point).Y.ToString("0.##")`，提示模板同样应明确区分源值和显示值。累计线与连接线没有独立业务身份；选择、键盘和辅助点沿用真实源项。Demo → Charts →“柱条与比较”提供六图及方向、排序、宽度、标签和堆叠开关。

### 折线、面积与范围面积

`LineSeries.Interpolation` 和 `AreaSeries.Interpolation` 支持 `ChartLineInterpolation.Linear`（默认）、`Smooth`、`StepBefore`、`StepAfter`、`StepCenter`。Before 在相邻源点的区段起点先垂直转换，After 在末点转换，Center 在中点转换。Smooth 在所属轴投影后的空间使用保形 Hermite 曲线，极值处限制切线以避免超调；保留输入顺序，重复 X 的区段使用直线。它提供平滑外观，不改变真实样本的坐标或身份。

```csharp
chart.Series.Add(new AreaSeries
{
    Title = "渠道 A", ItemsSource = new ChartPoint[] { new(1, 20), new(3, -20), new(5, 30) },
    Interpolation = ChartLineInterpolation.Smooth,
    StackMode = ChartStackMode.Percent, StackGroup = "channels",
});
chart.Series.Add(new AreaSeries
{
    Title = "渠道 B", ItemsSource = new ChartPoint[] { new(1, 30), new(3, -30), new(5, 70) },
    Interpolation = ChartLineInterpolation.Smooth,
    StackMode = ChartStackMode.Percent, StackGroup = "channels",
});
```

`AreaSeries` 在上界曲线与 `Baseline`（默认 0）之间填充，显式 `Fill` 优先；未设置时以系列画刷的 25% 不透明度填充。非堆叠的 `Baseline` 必须有限，可位于源数值上方或下方，自动范围包含有效上下界。基线为 0 且 0 不属于轴域时，绘图使用该轴的有效下边界；其他域外基线不会被自动改写，相关面积区段无法投影时形成缺口。例如对数轴应使用 0 的回退规则或显式正基线，不能使用负基线。入场动画沿用图表的有效 Y 起点（域内 0，否则轴下界），上下轨同时移动至目标；非零面积基线在入场中也参与插值。

`LineSeries.StackMode` / `AreaSeries.StackMode` 支持 `None`（默认）、`Stacked`、`Percent`。堆叠键由解析后的实际横轴实例、纵轴实例、`StackGroup` 和 `StackMode` 共同组成；null 与空组名为同一组，Line 与 Area 可以混合累计。按实际 X 值对齐，重复 X 的第 N 次出现相互配对，保留各系列源顺序；不为未采样的 X 生成业务项。只有当前可见系列参与计算，显隐、换源、集合和业务项更新会重算同组结果。类别横轴先映射到共享槽位；类别纵轴不支持堆叠并明确抛出异常。

正负贡献分别从 0 累计，堆叠忽略 `AreaSeries.Baseline`。`Percent` 在每个 X/出现次序上分别把正贡献和负贡献归一到 +100 / -100，某一侧总量为 0 时其贡献为 0；分母先缩放再求和，避免有限原值的直接求和溢出。每层边界由累计缩放权重除以同一分母计算，最后一层精确到达 ±100，避免微小累计误差把自动坐标轴扩到 ±150；份额取相邻边界之差，中间份额仍受浮点表示精度限制，业务比较应使用适当的数值容差。普通堆叠无法表示的累计结果形成缺口，源值仍保留。不同组、轴和堆叠方式互不累计。平滑堆叠对同插值、相同连续拓扑的上下边界共享切线限制，避免非负贡献的填充带反转；不同采样、空缺或插值方式仍沿各自源段绘制。

`RangeAreaSeries` 使用 `HighPath` / `LowPath`，无需提供 `ValuePath`，并支持相同五种插值：

```csharp
chart.Series.Add(new RangeAreaSeries
{
    ItemsSource = new[] { new { Id = "a", X = 1d, Low = 10d, High = 40d },
                          new { Id = "b", X = 3d, Low = 20d, High = 70d } },
    CategoryPath = "X", IdPath = "Id", LowPath = "Low", HighPath = "High",
    Interpolation = ChartLineInterpolation.Smooth,
});
```

显示时使用两界的 min/max，原 `High` / `Low` 字段保留，倒置输入不会倒置填充带。范围面积及折线/面积堆叠可使用数值、对数或日期型纵轴，坐标须符合各自有效域；`DateTimeAxis` 使用 Unix 毫秒，对数轴只接受正数。它们显式拒绝 `CategoryAxis` 纵轴并抛出异常，类别横轴仍受支持。`EmptyPointMode.Gap` 保留缺口；Zero 只把缺失界填 0，Average / Interpolate 从左右原本完整的范围样本补缺失界，已有的有效界保持原值，无效 X 和无法补齐的边界仍保留缺口。`WasEmpty` 保留原始缺失状态。公开快照显式设置的 `IsEmpty` 同样受到尊重。范围面积不提供堆叠属性。

显示快照的 `ChartDataPoint` 与命中的 `ChartPointHit` 同时提供 `RawPoint`、`BaseValue`、`StackValue`：`Point` 为当前显示的上界，`RawPoint` 为范围/堆叠变换前的映射坐标，`BaseValue` 为当前显示下界，`StackValue` 为原贡献或带正负号的百分比份额。普通未变换点不提供这些可选值，非堆叠面积/范围不提供 `StackValue`。`Item`、源索引和 `StableId` 始终指向源项；`series.CreateSnapshot()` 保持源投影，不被图表的堆叠变换改写。动画期间 `Point` / `BaseValue` 属于显示帧，`RawPoint` / `StackValue` 和原对象属于目标数据，不能用两者推导瞬时贡献差。

点选择、标记、键盘浏览和 `HitTestPoint` 仍指向真实上界采样点。`HitTestSegment`、插值 Tooltip 和 Trackball 查询折线或面积的上界路径，范围面积的下边曲线及整个填充内部不构成独立命中区域。`ChartSegmentHit.BaseValue` / `ChartTrackballReading.BaseValue` 提供同参数处的下轨读数；默认范围 Trackball 显示上下界。曲线查询先以 0.02 DIP 平直度或深度 16 终止自适应细分，再在曲线上细化查询，并保留断轴/绘图区裁剪与真实两端点身份；不承诺任意极端投影下的解析精确最近点。当前使用完整路径，线段查询成本、稀疏堆叠的连续视觉连接及大量曲线/面积的性能验收留在后续优化。

默认样式使用独立的 `NT.AvaloniaControl.Charts.Themes` 包，应用加载 FluentTheme 后添加 `new ChartsTheme()`。
也可直接设置控件的 `Foreground`、`AxisBrush`、`GridLineBrush`、`FontSize` 与 `Padding`。

`XAxis` / `YAxis` 可用 `NumericAxis`、`CategoryAxis`、`DateTimeAxis`、`LogarithmicAxis`；原 `new ChartAxis()` 保留线性行为。
它们支持 `Minimum`、`Maximum`、`DesiredTickCount`、`Interval`、`IsInversed`、`Position`、`LabelFormat`，以及标签旋转/换行/最大宽度。
附加轴放入 `Axes`，系列通过 `XAxisKey` / `YAxisKey` 对应唯一的 `Key`。缩放按主轴的归一化比例同步到附加轴。
日期原始坐标为 Unix 毫秒，`DateTimeAxis.ToUnixMilliseconds` 提供转换；未指定 Kind 的 DateTime 按 UTC 解释。
对数轴只接收正数，`LogBase` 默认 10；其 `Interval` 表示指数间隔。

`ItemsSource` 接收非泛型 `IEnumerable`：数值列表隐式使用源索引作为 X，`ChartPoint` / Avalonia `Point` 保留 XY，对象使用 `CategoryPath` / `ValuePath`，可选 `IdPath` / `HighPath` / `LowPath` / `WeightPath`。
属性路径访问器按类型缓存，支持嵌套 `INotifyPropertyChanged`；错误通过 `DataError` 与快照 `Issues` 提供。
`CategoryAxis` 对业务类别进行共享映射，显式 `Labels` 按类别名称匹配；不存在的业务类别形成空点。直接 XY 数据仍可使用显式数字槽位。
普通数值 X 按真实距离绘制，折线保留输入顺序与重复 X。
集合和业务项在 UI 线程更新；观察期间响应 `INotifyCollectionChanged` / `INotifyPropertyChanged`，普通列表更改后调用 `series.Refresh()`。
`using (series.DeferUpdates())` 合并通知。数据仍归应用所有，图表卸载后释放外部订阅。
缺失值策略 `EmptyPointMode` 支持 Gap（本库默认）、Zero、Average、Interpolate；边界无法补齐的缺失值保留 Gap，无效 X 不补值。
柱图按系列分组，共享零基线并支持负值。

悬停到数据点或柱体附近显示系列名称、X / Y 值；分类轴使用标签，数值优先使用 `LabelFormat`。
Tooltip 默认启用，滚轮缩放和平移默认关闭；启用后在绘图区滚轮缩放、按住左键拖动平移、双击恢复完整视图。
`InteractionAxes` 可设为 `X`、`Y`、`Both` 或 `None`，同时控制缩放和平移的方向。
绘图区以外的滚轮仍传递给页面滚动容器。

触摸采用 `IsTouchEnabled=true`，配合 `IsPanEnabled` / `IsZoomEnabled` 启用单指平移和双指捏合；`TouchPanThreshold` 默认 8 DIP，轻触在释放时选择。捏合围绕两指中心缩放，中心移动同时平移；手指数切换会重新建立基线。外部视图/数据变化、失去捕获、窗口失活、隐藏、禁用和 Escape 会取消当前操作。
`WheelMode` 默认 `Zoom`，保留原滚轮行为；`Pan` 接收连续 XY 增量，`Auto` 用普通滚动平移、Ctrl/Meta 滚动缩放，`Disabled` 将滚轮交给父级。`WheelPanSensitivity=48` 表示每单位增量的 DIP 位移，`WheelZoomSensitivity=1.2` 表示每单位缩放倍率；非空 `WheelZoomModifiers` 可指定缩放修饰键。Shift 把单独的纵向平移增量转为横向，原生对角增量保留两个分量。
嵌套滚动页面建议 `WheelMode=Auto`、`InputRoutingMode=Auto`（默认路由模式）：父 `ScrollViewer` 可沿输入方向继续滚动时优先滚动页面，图表边界处未发生有效变化的输入继续冒泡。`Chart` 优先操作图表，`ScrollViewer` 将导航交给父级、仍允许轻触选择。图例面板保留自己的滚动优先级。触控板验证覆盖连续增量和原生 Pinch 事件，真实设备及驱动手势映射仍需现场验证。

交互使用 `Viewport`（`ChartViewport?`），也支持各轴的 TwoWay `ViewMinimum` / `ViewMaximum`；保留 `Minimum` / `Maximum` 数据域边界。
`ResetViewport()` 同时清除图表和轴视图；`ViewportChanged` 通知图表请求变化。
布局完成后，`VisibleRange` 提供实际可见范围，`PlotArea` 提供控件内绘图区矩形。
视图限制在原始自动范围或显式坐标轴边界内；每轴 `MinimumZoom` / `MaximumZoom` 默认 1 / 1,000,000，可独立配置。附加轴可单独设置视图，也可跟随主轴比例。
更换轴或修改轴范围/变换会重置视图；标题、格式和画刷更改保留视图。数据更新清除过期提示并按新数据范围约束视图。
`HitTestPoint(position, tolerance)` 支持控件坐标的点 / 柱命中；`HoveredPoint` 包含系列、映射点、原始 `Item`、源索引、`StableId` 和屏幕位置。
`VisibleRange`、`PlotArea`、`HoveredPoint` 为可观察只读属性；`Viewport` / `SelectedPoint` 支持双向绑定，`PointInvokedCommand` 与 `ResetViewportCommand` 提供命令入口。
系列与轴作为图表逻辑子项继承 DataContext 和资源；同一个系列或轴实例只能属于一个图表。`SeriesSource` 支持绑定并替换系列集合。
派生 `ChartSeries` 并重写 `Renderer` 可提供范围、绘图、命中和图例符号，使用共享 `IChartProjection` 投影/逆投影。
异步计算只能发布独立只读快照；`PublishSnapshotAsync` 检查取消、源版本、索引和身份，并在 UI 线程发布。仅活跃观察期间接受发布，旧结果返回 false。
`TooltipBackground`、`TooltipForeground` 和 `TooltipBorderBrush` 可自定义，独立主题包提供亮暗配色。

迁移注意：`ItemsSource` 已从泛型集合拓宽为 `IEnumerable`，集合表达式请写成 `new ChartPoint[] { new(0, 1) }`；读取时按实际源类型转换。`ChartAxis.Changed` 的载荷改为 `ChartAxisChangedEventArgs`。
轴还支持 X/Y 类别映射、类别槽位/间隙、主次刻度和网格、Auto/隐藏/换行/错行/旋转标签、独立字体/画刷/线宽及虚线。`GetAxisLayout` 提供有效标签策略和留白诊断。
`ScaleBreaks` / `PlotBands` 或对应 `Source` 可从 AXAML/VM 配置压缩断轴和绘图区间，支持集合/子项通知；区间可设文字、对齐、透明度、重复和系列前后图层。
typed 数值/点源采用不可变值缓冲和按需身份，内置命中使用快照索引；折线仍保留完整顶点和缺失断点。全量公开 `Points` 读取有对应 metadata 成本，集合编辑尚为 O(n) 复制。

`IsVisible`（TwoWay）/ `ShowInLegend` 控制系列和图例。`ColorPalette` 保持同一系列实例的配色身份，支持四边 `LegendPosition`、横竖 `LegendOrientation`、带取消的 `LegendItemInvoked` 和自定义绘制。默认绘制模式在空间不足时截断条目；`IsLegendScrollable=true` 启用有界的纵向滚动，横向内容按排列换行或堆叠。`LegendMaxWidth` / `LegendMaxHeight` 是可选的 DIP 上限，仍受停靠区域或浮动绘图区约束。
`LegendItemTemplate` 接受 `IDataTemplate`，优先于 `LegendRenderer`，上下文为 `ChartLegendItem`，保留原 `Series`、文字、配色、符号、可见状态及图表局部坐标的可见裁剪 `Bounds`。同一系列及模板复用内容控件；返回 null 隐藏该项。内容是被动展示，条目点击仍经过可取消的 `LegendItemInvoked`；滚动条可操作，整个图例面板的滚轮不会传给图表缩放。`LegendItems` 只返回当前可见条目，滚动后的点击使用实际裁剪边界。未启用控件模板、滚动或尺寸限制时保留原绘制路径；单独设置最大尺寸会启用有界的默认条目控件。浮动图例与控件标签组合时也采用该宿主，保证图例位于标签上方；默认绘制 Tooltip 和模板 Tooltip 都位于这两层之上。
`LegendHorizontalAlignment` 控制上/下边图例的 Left/Center/Right，`LegendVerticalAlignment` 控制左/右边的 Top/Center/Bottom；Stretch 使用起点位置。默认 Left/Top 保持原布局。`IsLegendFloating` 将图例放入最终绘图区，不预留轴/系列空间；`FloatingLegendMargin` 默认 8 DIP，要求有限且非负。浮动图例在数据/标注/读数之上、Tooltip 之下绘制，背景取 `FloatingLegendBackground` 或图表 Background。整个浮动面板阻挡下层点选择、悬停与滚轮缩放，系列开关仍只命中实际条目；已捕获的拖动可以跨越面板继续。
`MarkerShape` 提供十种形状及独立 marker 样式；柱标记默认关闭。启用 `ShowDataLabels` 后可设置格式、位置、重叠策略和拿到原始项的 `DataLabelFormatter`；每系列默认最多 500 个可见候选，不改变数据几何。
`ChartSeries.DataLabelTemplate` 接受 `IDataTemplate`，上下文为 `ChartDataLabelContext(Series, Data, Position, FormattedX, FormattedY)`，其 `Text` 为格式化后的标签文字。`Data.Item` / `SourceIndex` / `StableId` / `Point` 保留原始业务数据身份；`Position` 是图表局部 DIP 锚点。Formatter 返回空文字时模板仍可展示原数据，Build 返回 null 隐藏该候选。模板沿用 `MaxDataLabels` 候选预算、位置、重叠与断轴/绘图区裁剪，并继承字体、前景色和资源；外层边框提供 `DataLabelBackground` 与 3×2 DIP 内边距。同系列、模板和源索引复用控件，业务更新替换上下文；不为视口外或无效锚点创建标签。关闭标签、替换模板、删除系列和卸载会释放旧内容及上下文。数据标签不接受指针或 Tab 焦点；模板、图例滚动位置一同进入 PNG 导出。
两类模板均要求返回未被其他父控件拥有的控件；构建异常在清理后传播，同一失败模板不会在每次重绘中反复构建，替换模板后可以恢复。复用控件通过绑定响应新的 DataContext，Build 中直接写入的固定文字不会自动重建。模板是有预算的 UI 控件展示，尚无图例条目虚拟化；大量业务控件的成本需要按实际模板另外测量。
`TooltipMode=SharedX` / `SharedY` 分别按每个系列最近的可见屏幕 X / Y 提供 `HoveredPoints`，保留各自实际原始值、源索引与身份，不插值或伪造共同坐标。另一屏幕维度距离用于平局比较，完全相同时取源物理顺序靠后的点。标准笛卡尔 SharedY 按需创建独立 Y 索引；未使用该模式不构建此索引，自定义耦合投影保留完整扫描。
`ShowCrosshair` 控制十字线，`CrosshairDirection` 支持 Both（默认）、Horizontal、Vertical。`ShowCrosshairLabels` 默认关闭，独立控制轴读数标签；Horizontal 对应 Y 轴，Vertical 对应 X 轴。只读 `AxisReadings` 保留有效悬停时各参与轴的 `Axis`、`IsHorizontal`、原始域 `Value` 与格式化 `Text`，不受线或标签显隐控制；各轴由同一屏幕位置按自身变换逆算，附加对数轴不会复制主轴数值。Trackball 启用时使用其主读数位置逆算；离开、所有读数禁用、拖动、换源和卸载清除读数。
`TooltipContentTemplate` 接受 `IDataTemplate`，其 DataContext 为 `ChartTooltipContext(Primary, Hits, Text, Anchor)`。`Hits` 中每个 `ChartPointHit.Item` 是原业务对象，`Anchor` 使用图表局部 DIP。模板实际创建 Avalonia 内容控件，同一模板在移动中复用；宿主不接受指针或键盘焦点。模板继承图表资源与提示前景色，背景/边框使用 `TooltipBackground` / `TooltipBorderBrush`，并参与 PNG 导出。模板返回 null 表示不显示内容；Build 异常清理临时引用后沿调用链传播，已属于其他父控件的内容不能复用。
`TooltipFormatter` 可提供模板的 `Text`；返回 null 时模板仍可用 `Hits` 显示数据，未设置模板时保持原来的空文本隐藏行为。模板、源和生命周期变化会释放旧上下文和内容；不悬停或不设置模板时不创建提示控件。

`TooltipMode=NearestSegment` 查询绘图区内最近的可见路径；`InterpolateX` / `InterpolateY` 按指针的屏幕 X / Y 读取各系列的路径交点，柱和散点仍读取真实点。`HitTestSegment(position, tolerance)` 提供独立查询，坐标和距离使用图表局部 DIP。内置 `LineSeries`、`AreaSeries`、`RangeAreaSeries` 连接的物理相邻有效点参与插值；不把散点、柱或自定义 renderer 的点擅自连成线。面积只查询上界。NaN/无效投影断开路径，绘图区及断轴裁剪后的可见部分才参与查询；两端点均在视口外的穿越路径仍可查询。

`ChartSegmentHit` 将 `Start` / `End` 两个真实 `ChartPointHit` 与派生的 `Point` / `Position` / `Fraction` 分开。先在线段实际屏幕几何上插值，再经所属轴逆投影，因此对数轴的显示中点通常不是原始数值的算术平均；`Fraction` 相对未裁剪原段，精确端点返回原始坐标。新插值模式的平局按最早物理段确定，`Original` 取比例不超过一半的起点或其后的终点。`HoveredPoint` / `HoveredPoints` 始终保留真实源项；估算值通过 `HoveredSegments` / `PrimarySegment` 和 `TrackballReadings` 暴露。`ChartTooltipContext` 保留原构造参数，并新增 `Readings`、`Segments`、`PrimarySegment`，业务模板可以同时展示估算值及两个原始记录，不能将估算值当作新的业务项。

`TrackballMode=None` 保持现有 Tooltip 行为；`Vertical` / `Horizontal` 分别按屏幕 X / Y 跟踪各可见系列，启用后优先于 `TooltipMode`，但不会改写其绑定值。Trackball 独立于 `IsTooltipEnabled`，可以关闭普通提示后单独启用。`TrackballSnapMode=NearestPoint` 读取真实采样点，`LinearInterpolation` 使用上述可见线段规则；柱和散点始终保持原始数据语义。孤立的有效折线采样仅在共享坐标精确相同时返回原点，不生成线段或跨缺口外推。方向参考线穿过指针 X / Y，标记在各读数的真实/派生位置；最近点模式不强迫不同系列具有共同采样坐标。`TrackballDisplayMode=GroupAllPoints` 为全部读数显示一个提示卡，`FloatAllPoints` 在各系列读数旁分别显示提示卡。

`TrackballContentTemplate` 使用同一 `ChartTooltipContext` 类型；聚合卡收到全部 `Readings`，逐点卡收到自身一条读数。`TrackballFormatter` 接收当前卡的 `IReadOnlyList<ChartTrackballReading>`，不改变原 Tooltip formatter 的真实点契约。模板内容是被动控件并继承图表资源和提示样式。`ShowTrackballLine` / `TrackballLineStroke` / `TrackballLineStrokeThickness` 配置方向线；`ShowTrackballMarkers` / `TrackballMarkerFill` / `TrackballMarkerStroke` / `TrackballMarkerStrokeThickness` 配置圆形标记，`TrackballMarkerSize` 是 DIP 直径，默认 14。切换模式、源失效、拖动、退出和卸载清理对应状态与卡片，当前追踪内容参与 PNG 导出。

```xml
<charts:CartesianChart TrackballMode="Vertical"
                       TrackballDisplayMode="GroupAllPoints"
                       TrackballSnapMode="LinearInterpolation"
                       IsTooltipEnabled="False" />
```
`SelectionMode` 支持 None、Single、SingleDeselect、Multiple；`SelectedPoints` 与 `SelectedPoint` TwoWay 同步。Ctrl 点击切换、Shift 拖动框选，或调用 `SelectRectangle` / `ClearSelection`；超出 `MaxSelectionCount` 会返回截断信息。隐藏或删除后清理选择。

`SeriesSelectionMode` 使用同一枚举，默认 None。`SelectedSeries` 为不可变系列引用列表，支持 TwoWay；`SelectSeries(series, additive)` 幂等选择，`ClearSeriesSelection()` 清除系列状态。点击折线点/直线段、柱体或散点，Ctrl 切换多选；点选择独立保留。启用键盘导航时，点导航同步其系列，关闭点选择后方向键按系列导航。`SeriesSelectionChanging` 可取消用户和外部设值，`SeriesSelectionChanged` 带变更原因；隐藏、删除及模式限制的强制清理不可取消。替换源容器仍按系列实例保留有效选择；重新显示不会恢复已清理选择。`UnselectedSeriesOpacity` 默认 .25，仅在存在系列选择时淡化其余系列几何；不扫描点或重建数据索引。程序调用返回 false 表示取消或被回调中的更新覆盖。

### 入场、数据与视图动画

`AnimationTriggers` 默认 `None`，保持数据和视图即时更新；显式选择 `Entrance`、`Data`、`Viewport` 或其组合（`All`）。入场在首次有效挂载布局时启动，也可用 `ReplayEntranceAnimation()` 重播。`AnimationDuration` 默认 450 毫秒，`AnimationDelay` 默认 0，均要求非负；时长 0 直接完成并忽略延迟。`AnimationEasing` 接受 Avalonia `IEasing`，默认 `CubicEaseOut`，null 为线性；异常或非有限返回值回退线性，有限结果限制到 0～1。只读 `AnimationProgress` 是当前帧的缓动后进度，静止时为 1；`AnimationState` 为 Idle、Delayed、Running 或 Paused。

```csharp
chart.AnimationTriggers = ChartAnimationTriggers.All;
chart.AnimationDuration = TimeSpan.FromMilliseconds(700);
chart.AnimationDelay = TimeSpan.FromMilliseconds(100);
chart.AnimationEasing = new Avalonia.Animation.Easings.CubicEaseInOut();
chart.ReplayEntranceAnimation();
// chart.IsAnimationPaused = true;  // 暂停当前画面及延迟计时
// chart.IsAnimationPaused = false; // 从暂停位置继续
// chart.CancelAnimations();        // 直接显示最新目标
```

入场从有效 Y 基线（域内的 0，否则当前轴下界）向目标生长，X 保持目标坐标。数据更新按同一系列的稳定身份匹配：设置 `IdPath` 时按值匹配且不回退错误序号；未设置时先匹配引用身份，再回退源索引。新增点从基线生长，删除点立即去除。连续更新以当前显示帧为新起点，最终到达最新目标。同一 SourceVersion 的成功 TryPublishSnapshot / PublishSnapshotAsync 发布也触发数据动画；SourceVersion 和快照 Version 仍保留原有版本契约。有限坐标和视图边界均在所属轴的转换空间插值，因此对数轴使用对数空间；已有断轴内的中间点作为缺口处理。自动数据范围始终取真实目标数据，动画基线不会扩展目标范围。

动画期间，`Viewport` 和轴级 `ViewMinimum/ViewMaximum` 保留业务目标，`VisibleRange`、`PlotAreaContent` 的上下文、系列、标签、标注和命中位置使用同一显示帧。命中/标签/Trackball 中的 `Point` 此时是画面上的瞬时插值坐标；`Item`、`StableId`、`SourceIndex` 仍指向真实目标项，原 `ItemsSource` 和 `series.CreateSnapshot()` 不被改写。业务需要最终值时从原始项或源快照读取。绘图区使用目标布局的留白，各帧重算刻度；多图联动只交换目标视图，各图按自身动画配置播放，不把中间帧反复回传。

`IsAnimationPaused` 同时暂停延迟和运动，恢复不计入暂停期间的时间。`CancelAnimations()`、隐藏图表或祖先、禁用、卸载会结束时间线并显示目标；重新显示不恢复旧帧，重新挂载且启用 Entrance 时可开始新的入场。动画配置、轴配置/变换/方向/断轴、会重建场景的样式和布局边界变化采用直接完成策略。滚轮、触摸和平移产生的有效视图写入立即抢占程序动画，以实际导航结果为准。

`ReduceMotion=true` 立即完成当前动画，并让后续更新直接显示结果。该属性由应用提供，可绑定用户偏好或宿主获取的系统减少动态效果设置；当前 Avalonia 12 公共平台设置接口未提供统一的系统减少动态效果偏好，本库不自动探测 Windows/Linux 桌面设置。

`AnimationPointLimit` 默认 10,000，按全部可见系列的源点数合计；起点或目标任一场景超限，整个变化直接完成，0 禁止动画。默认 None 不创建动画坐标叠加或动画索引。点数限制是显式成本策略，百万点和多图动画的帧率/分配/尾延迟仍需 C4 正式性能验收。生产时钟使用原生帧调度；普通查询、绘制和 PNG 导出读取已经提交的当前帧，不推进时间，暂停与运行中同步导出的画面均与同一帧一致。

### 范围选择器与多图范围同步

`ChartRangeSelector` 可独立放置或组合在图表旁边。`Range` 是 TwoWay 的 `ChartRange(Lower, Upper)`，以完整轴转换后的递增数据域表示 0～1 范围。支持 `Orientation` 横/竖、`IsDirectionReversed`、双端点拖动、窗口平移及 `MinimumSpan` / `SmallChange` / `LargeChange`；Tab 可依次聚焦窗口和两个端点，方向键、PageUp/Down、Home/End 调整，Escape 取消捕获。竖直默认向上递增，反向只改变呈现，不改变数据顺序。

设置 `Chart` 和单一 `Axis`（X 或 Y）可双向耦合主轴。`GetVisibleFraction` / `SetVisibleFraction`、`GetNormalizedViewport` / `SetNormalizedViewport` 使用各轴真实转换，覆盖数值、日期、对数、断轴与反向轴；不应对原始对数值直接线性换算。`MinimumSpan` 约束选择器发起的请求；图表自身缩放限制与实际范围为最终依据，接收外部较小窗口时如实显示，窗口平移保持该跨度。Chart 耦合与输入捕获在卸载时释放。

```csharp
var selector = new ChartRangeSelector { Chart = first, Axis = ChartInteractionAxes.X };
var group = new ChartSynchronizationGroup { Axes = ChartInteractionAxes.X };
group.Add(first);
group.Add(second);
group.SynchronizeFrom(first); // Add 与修改配置本身不选择同步源
first.SetVisibleFraction(new ChartRange(.2, .7));
// 完成使用后 group.Remove(chart) 或 group.Dispose()
```

`ChartSynchronizationGroup` 支持 X/Y/Both/None，`CoordinateMode` 默认为 Normalized（各自完整范围的比例）；DataCoordinates 使用原始坐标，每个目标按自身范围和缩放限制约束。不同目标约束后的结果不反推源；后续目标主动操作可作为新同步源。成员为弱引用，隐藏或卸载的图表暂停视图订阅和参与，恢复后显式 `SynchronizeFrom` 可选择权威源。重入时最新业务状态优先；64 轮仍不收敛的矛盾回调抛出 `InvalidOperationException`，不无限排队。UI 线程操作，Dispose 幂等。

完整范围有效无变化时保留自动范围；重置至完整双轴且没有轴级显式视图时恢复自动范围。单轴恢复全域、另一轴仍为手动窗口时，四界 `Viewport` 仍为显式范围，后续数据扩容需再次 reset/设值。同步源使用主 X/Y 范围；目标同维度的额外轴按自身完整范围和转换跟随相同比例，已有显式 `ViewMinimum/ViewMaximum` 也会更新，未选维度保持原视图。本期不含跨图游标、Trackball 或选中业务项同步。
`SelectionChanging` 提供 OldSelection/NewSelection、Reason、CanCancel 与 Cancel。用户选择、框选、清除和外部属性赋值可取消；源删除/隐藏/失效及配置收缩的清理不可取消。取消 TwoWay 外部赋值会用旧规范状态回写，Avalonia 绑定可能已发出瞬态通知。回调内的新选择优先于外层请求，嵌套变更不递归触发 Changing；取消选择不吞掉实际点击的 PointInvokedCommand。`ChartSelectionResult.IsCanceled` 表示框选请求被取消或被回调中的新请求替代。
`IsKeyboardNavigationEnabled` 保留方向键选择、Home 复位和 Esc 取消操作的原有行为；设置 `IsPointBrowsingEnabled=true` 可启用独立于选择的点浏览，具体按键见下述辅助功能契约。

### 点级辅助语义与数据摘要

`AccessibilitySummary` 是只读、可绑定的不可变摘要，`AccessibilitySummaryText` 提供文字。首次读取或绑定摘要、或者请求 `ChartAutomationPeer` 后才维护计数；没有这些用户时，绘图不会为辅助功能扫描源数据。摘要复用已排版系列的快照，不枚举 `ItemsSource`，按源版本和已发布快照修订缓存计数。每个系列包含 `TotalCount`、`ValidCount`、`MissingCount`、`InvalidCount`、`SelectedCount`：Missing 表示填补前的原始缺失，Valid 包含 Zero 等策略已填补的有效值，因此 Missing 与 Valid 可能重叠。轴摘要列出当前主轴及附加轴的可见范围。

首次布局前 `IsLayoutPending=true`，尚未展示或换源后的隐藏系列标记 `IsDataAvailable=false`，总数只合计已知系列，`IsDataComplete=false`；隐藏系列的旧计数仅在同一源版本与快照修订内复用。摘要不把未知数伪装为已经测得的零。浏览中的真实源项也写入 `Text` 的 current 段，并触发图表 `IValueProvider.Value` 变化。

Valid/Invalid 依据标准映射快照的有限坐标及缺失填补状态分类；Missing 可与 Invalid 重叠，Valid 不代表轴域、断轴或当前视口内可操作的节点数量。动画期间点节点和浏览读数使用当前显示帧坐标，业务 `Item` 仍是目标源对象，因此瞬时 Point/Value 可与 Item 的目标值不同，完整规则同上述动画契约。

系列辅助节点保留可取消的可见性切换；其子节点默认只展开当前 64 个快照序号内的有效源点，前后分页按钮通过 `IInvokeProvider` 切换页面。无效点在该页内跳过，不为填满页面继续扫描后面的行。`ChartAutomationPeer.GetPointPage(series,startIndex,count)` 显式请求 1～256 行；`GetPointPeer(series,sourceIndex)` 请求原始源索引。图表最多强引用 256 个最近访问的点节点；不创建全源节点表，客户端仍可持有已经取出的节点。`ISelectionProvider.GetSelection()` 返回完整的显式选中集合（受 `MaxSelectionCount` 约束），其结果数组大小随选中项数量变化，不截断为当前页。

有效新场景中源缩短导致当前页越界时，页起点自动归到最后一个有效对齐页；空源回到 0，后续增量恢复可直接读到点。布局尚待发布或系列隐藏时不把临时零计数当作空源。仅系列成员/顺序变化通知根结构改变，数据页和分页变化由系列节点通知，浏览和值更新不会重复重建根树。

`ChartPointAutomationPeer.CurrentPoint` 保留原始 `Item`、`StableId`、`Index` 和当前画面的投影位置，不把线段插值读数或抽样代表当作新源项。名称和值使用对应轴的格式化；提供只读值、选中状态、可用状态、相对于顶层的 DIP 边界和是否在绘图区外。点节点实现 `ISelectionItemProvider` 与 `IInvokeProvider`：选择沿用 `SelectionMode`、上限、`SelectionChanging` 取消和回调中新状态优先规则；调用命令独立于选择，执行原有 `PointInvokedCommand`，命令不存在、不能执行、数据失效或回调替换请求时明确抛出 `InvalidOperationException`，禁用节点沿用 Avalonia 的禁用异常。值和名称变化分别发出相应模式的属性事件。

稳定身份优先匹配原源索引，再复用已有身份索引，最后只搜索最多 256 个邻近快照序号；辅助功能不会因此为百万点新建完整身份或源索引字典。小于等于 256 项的重排可完整迁移；更大源的远距离迁移、非顺序已发布快照的远索引随机查找，在没有既有索引时可能返回不可用，旧节点不会错误绑定到新项。客户端可请求当前页面/源索引取得新节点。该限制及大数据全域虚拟化继续留待 C4；失效、移除和隐藏节点无法执行选择或命令，正常恢复显示可以复用仍有效的身份。

256 是图表自身 LRU 的强引用上限，不约束外部客户端或平台桥接缓存。逐出节点不再由图表主动更新事件；外部持有的节点仍会在查询/操作时按当前场景校验，失效时不能读取为有效点或执行命令，但可继续持有原业务项、系列和图表引用，直到客户端释放。根缓存中已发现失效的节点会主动退休并清除其点引用；不保证所有客户端持有节点的即时回收或全进程内存上限。

虚拟点 `SetFocus` 将 `CurrentPoint` 移至绘图区内的真实点并聚焦图表；浏览要求显式开启。点的 `HasKeyboardFocus` 反映此语义，但 Avalonia 12 的公开外部扩展接口不能替换原生根节点的焦点返回，原生焦点仍可能是图表。当前提供图表值变化通知及点状态，不能据此宣称读屏器自动播报、Windows UIA 原生点焦点或 Linux AT-SPI2 已全部验收。

原生完整选中列表另有桥接限制：本批 Windows UIA 夹具中，托管 `ChartAutomationPeer.GetSelection()` 有一项时，原生 SelectionPattern.GetSelection 返回空；各点的 SelectionItem 状态与 Select/Add/Remove 动作分别验证，不能替代该容器列表门禁。Linux AT-SPI 的直属子节点是系列，`select_child(0)` 并不选择其嵌套数据点；原生点选择写入口与实际读屏流程需另验。应用可展示摘要 SelectedCount、当前点和点状态，完整平台范围见仓库本批验证报告。

同时开启 `IsKeyboardNavigationEnabled` 与 `IsPointBrowsingEnabled` 后，Left/Right 在当前系列中浏览有效且位于绘图区内的点，Up/Down 切换系列并尽量保留快照序号；Home/End 到该系列首/末可用点，PageUp/PageDown 跨 64 个源序号，Ctrl+Home 复位视图。Space 选择当前点，Multiple 模式下 Ctrl+Space 切换其选中；Enter 独立执行命令，Escape 清除浏览/读数并取消输入，保留选择。失焦、隐藏、卸载、禁用和源失效清除浏览状态。每个候选系列每个搜索方向最多检查 4096 行，长串无效/视口外数据可能使按键保留当前点；该预算不宣称任意大源单次按键必达。`NavigateToPoint(series,sourceIndex)` 可显式移动浏览状态，要求图表已附着、启用、显示且目标位于当前绘图区；不会自动选择、执行命令或取焦点。

`HighContrastMode=None/Light/Dark` 是显式应用配置。安装 `ChartsTheme` 时 Light/Dark 样式提供白/黑画布、前景、坐标轴、网格、系列、选择、浮动图例与读数颜色；默认 `ChartContrastPalettes.Light/Dark` 的不透明系列色在对应白/黑画布上均至少 7:1 sRGB 对比度。不使用 Themes 时可自行应用这些只读调色板。图表/系列的本地自定义背景、笔刷和样式仍有优先级，其实际对比度由应用负责。此模式不自动侦测操作系统高对比设置，也未新增纹理/填充图案；这两项继续留待后续。

### 键盘浏览与高对比配色

同时设置 `IsKeyboardNavigationEnabled=true` 与 `IsPointBrowsingEnabled=true` 后，方向键浏览真实点：左右按当前系列源顺序，向上/下移至前/后系列的相近源序号；Home/End 到当前系列首/末点，PageUp/PageDown 跳过 64 行，Ctrl+Home 重置视图。无效、缺失及视口外的点跳过，每个系列的单次搜索最多 4096 行，不自动缩放或无限扫描。只读 `CurrentPoint` 可绑定，浏览独立于 `SelectedPoint`；公开 `NavigateToPoint(series,sourceIndex)` 允许程序显式移动至当前可见真实点，本身不选择、不执行命令、不抢焦点。Space 通过可取消的选择契约选择，Ctrl+Space 在 Multiple 模式切换，Enter 调用点命令；Esc 清除浏览读数并保留选择。焦点离开、隐藏、禁用、移除和卸载清理当前点。当前点的虚线方框和读数随同一显示帧参与导出。

`HighContrastMode=None` 为默认；`Light` / `Dark` 是应用显式选择的明/暗高对比预设。添加 `ChartsTheme` 后同时应用画布、文字、轴、网格、提示及调色板；仅引用功能包的应用需要提供相应样式，可直接使用不可变的 `ChartContrastPalettes.Light` / `.Dark`。六色均为不透明色，分别对白/黑画布达到至少 7:1 的 sRGB 对比度。显式系列 Stroke/Fill、图表局部值与业务绑定优先，切换预设不会改写它们或数据；自定义低对比颜色需由应用调整。预设不会自动读取操作系统高对比偏好，也不保证系列之间都有 7:1 差异；建议同时使用 marker 形状和文字。Demo 展示圆形、方形与菱形，避免只靠颜色辨认系列。

`Annotations` / `AnnotationsSource` 支持线、带、文字、自定义绘制，提供数据/相对绘图区像素坐标、图层和裁剪。标注 XAxisKey/YAxisKey 可以激活没有系列的附加轴，系列声明（含隐藏系列）固定已有维度；未知、重复、错误或冲突维度 Key 跳过，不回退主轴。Pixel 不激活数据轴；标注坐标不扩展系列自动范围。
`RectangleAnnotation`、`EllipseAnnotation`、`ArrowAnnotation` 使用 X1/Y1/X2/Y2 两端点，支持上述坐标、轴、资源继承与裁剪。椭圆是投影后包围框内的屏幕椭圆，在对数轴上不表示数据空间圆。箭头 `ArrowHeads` 支持 None/Start/End/Both，HeadLength/HeadWidth 默认 10/6 DIP、允许 0..10,000；缩放时尺寸保持 DIP，短线按长度缩小。DashStyle 只作用于箭杆，箭头边线保持实线；断轴裁剪不会在断口生成箭头。非有限、无法投影及退化几何跳过。`ClipToPlotArea=false` 沿用图表外框裁剪，允许跨断轴显示。
矩形 `CornerRadius` 为 DIP，默认 0，实际限制到短边的一半。三形状共用 `Label/LabelPosition/LabelOffset`（默认 Center、偏移 DIP）。椭圆 `GeometryMode=EndpointBounds` 保留两角点默认；`CenterRadius` 使用 X/Y/RadiusX/RadiusY 投影中心±轴单位半径的包围框，非线性轴视觉中心不一定等于中心数据点投影。
`PlotAreaContent` 直接接收未被父级使用的 Control（继承图表 VM），业务对象配合 `PlotAreaContentTemplate`，上下文为 `ChartPlotAreaContext`。它提供 Content、业务 DataContext、PlotArea、主轴 Projection、VisibleRange/Extent，以及 `TryGetProjection/TryGetVisibleRange`。投影为绘图区局部 DIP；断裂区投影为非有限值，模板应跳过该锚点。内容矩形裁剪 PlotArea，位于自绘图形之上、控件标签/图例/读数之下，被动展示不抢输入。替换、隐藏/卸载和离线导出结束释放旧内容；PNG 包括同一控件子树。
`TryExportPng(Stream|string, ChartExportOptions?)` 和 `TryExportJpeg` 导出当前完整图表，包括图例、标注、选择、读数和控件模板；按当前已排版尺寸等比缩放并居中，不改图表大小或视图。`PixelSize` 固定输出像素，省略时按 `Bounds × Dpi / 96` 向上取整。`Dpi` 接受 0.1～9600；固定像素时只影响分辨率元数据。PNG 写入 pHYs；JPEG 同时写 JFIF 整数 DPI 和 EXIF 有理数 DPI，后者保留五位小数。读取器是否采用该元数据取决于读取器。

`Background` 是额外画布背景，仍会绘制图表自己的 `Background`。PNG 可保留透明；JPEG 先铺白色再绘制这两层背景，将半透明画布混合为不透明像素。`JpegQuality` 默认 90，只接受 1～100，PNG 忽略该值。每次输出默认最多 16,777,216 像素，`MaximumPixelCount` 上限 67,108,864，单边上限 32,768；编码输出也有与像素预算成比例的上限。JPEG 和异步 PNG 使用无额外本机依赖的 StbImageWriteSharp 编码器。同步 PNG 保留原平台编码路径；异步 PNG 的 RGBA 读取在半透明边缘可能与平台编码存在每 RGB 通道 1 级的取整差异，透明度和几何保持一致。

`TryExportPngAsync` / `TryExportJpegAsync` 提供同样的流和文件重载，返回 `Task<bool>`，可从工作线程发起。图表、背景和模板在 UI 线程一次性捕获为独占 RGBA 像素，随后在工作线程编码和分块写入；捕获不会推进动画时钟，后续 UI 变化不影响这次快照。平台捕获和单次编码调用不能抢占，取消在捕获前后、编码前后及每段写入观测；显式开启抽样时，捕获中的抽样查询也在检查点响应取消。取消抛出 `OperationCanceledException`，不触发 `ExportFailed`；普通失败返回 false，并在 UI 线程触发一次该事件。事件处理程序自己抛出的异常会传回调用者。

同一图表从捕获至写出完成只接受一个导出；并发或重入请求返回 false 并报告失败，不排队累积大图。同步 API 必须在 UI 线程调用。调用者持有流：导出不会关闭或主动 Flush，失败/取消时流可能已有部分输出。文件先写同目录临时文件、关闭后替换；失败和最后一次令牌检查时已观测到的取消保留原文件，并尽力清理临时文件。外部权限或句柄变化可能阻止清理。随后启动的替换是提交点，提交后到达的取消不能撤回已成功输出。后端若不能读取像素或渲染，则显式报告失败；Headless 的失败契约不能作为真实图片验证。

现有图族已提供系列选择、横竖范围选择器与多图范围联动；后续图族的选择适配、内嵌概览、跨图游标/业务选择同步、物理触摸设备验收、系统减少动态效果适配、实时长稳及 Windows/Linux 完整设备矩阵仍在后续阶段。当前 Trackball/插值已覆盖折线、面积、范围面积及其平滑/阶梯/堆叠路径，柱与散点读取真实点。有序线性折线现可显式启用下述 MinMax 抽样；其他图族的 LOD/密度聚合和自定义线段扩展仍待补齐。线段查询成本及百万点命中尾延迟留到 C4 优化和正式性能验收，历史实测保留。不宣称商业 Chart API 或性能兼容。

## 保真抽样与层级细节

`LineSeries.SamplingMode` 默认是 `None`，保留原绘制策略。对于有序升/降 X（允许重复 X 和缺失断点）的线性折线，可显式开启 `MinMax`：

```csharp
var signal = new LineSeries
{
    ItemsSource = samples,
    ShowMarkers = false,
    SamplingMode = ChartSamplingMode.MinMax,
    SamplingPixelWidth = 1,
};
chart.Series.Add(signal);
// 在图表布局完成后、UI 线程上查询当前显示计划。
ChartSamplingInfo info = chart.GetSamplingInfo(signal);
```

每个物理像素桶保留原始首点、尾点及最高/最低点，按原顺序连接；缺失数据保持分段。`SamplingPixelWidth` 为 1～64 个物理像素，增大它会合并更宽的显示范围。单点峰谷按所在桶的极值保留，不保证保留桶内所有局部转折。放大后桶内数据变少，逐步恢复原始细节。桶范围包含描边接合与抗锯齿余量，因此 `PixelBucketCount` 可以略大于绘图区的像素宽度。

数值层级索引按不可变坐标版本复用；首次准备仍有 O(N) 成本。绘制只取原始物理序号，`ItemsSource`、原始快照、Tooltip、点/矩形选择和线段端点身份不变。被省略的顶点仍可命中和选择；插值读数继续使用原始相邻线段。`GetSamplingInfo` 可传取消令牌，取消不会发布半成品计划；`VisibleCount` 包含边界邻点，`RenderedCount` 是折线计划顶点数，不是新数据源的记录数。

当前抽样只用于线性、非堆叠、无可见标记/标签的内置 `LineSeries`，且要求稳定轴映射、无坐标轴断裂和实色描边。平滑/阶梯、堆叠、非有序 X、空间画刷、未知投影及不能证明安全的域沿原始绘制路径；`FallbackReason` 说明原因。柱状、散点、面积及其他图族不会静默聚合。多处缺口和真实可见数据仍有相应成本，本功能不提供所有场景固定顶点数或正式 FPS/内存承诺。

PNG/JPEG 导出默认采用原始线形。显式设置 `ChartExportOptions.UseSampling = true` 后，按输出像素尺寸重新计算抽样，不复用屏幕密度；导出不会覆盖屏幕显示计划。独立 renderer 使用 `ChartSeriesRenderContext.RenderScaling` 指定物理像素/DIP，默认 1。Demo 的“保真抽样与层级细节”提供开关、桶宽、10 万/100 万点、峰谷/断点放大及原始记录读数。
不依赖官方收费 Charts 包。编码依赖的固定版本与来源见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。

运行统一 Demo：`dotnet run --project samples/NT.AvaloniaControl.Demo -- --page charts`。
独立编译绑定样例：`dotnet run --project samples/Charts.Usage`。


## 基础分析图（C3-07）

`FunnelChart`、`PyramidChart` 与 `HeatmapChart` 是可绑定的独立控件，继承 `AnalysisChartBase` 的 `ItemsSource`、`ValuePath`、`CategoryPath`（`LabelPath` 别名）、`IdPath`、基础选择/键盘/Tooltip 与 PNG/JPEG 同步、异步导出。公开 `Series` 复用源观察与空值策略；`GetMarks()` 保留原始业务项目、源索引与稳定 ID。

```csharp
var funnel = new FunnelChart
{
    Height = 300, ItemsSource = new double[] { 100, 60, 25 },
    NeckWidth = .2, SegmentGap = 2,
};
var pyramid = new PyramidChart { Height = 300, ItemsSource = new double[] { 1, 3, 5 } };
var heatmap = new HeatmapChart
{
    ItemsSource = matrixRows, RowPath = "Row", ColumnPath = "Column", ValuePath = "Value",
    IdPath = "Id", Minimum = 0, Maximum = 100,
};
var symbols = new PictorialBarSeries
{
    ItemsSource = new ChartPoint[] { new(0, 75), new(1, -50) },
    RenderMode = PictorialBarRenderMode.Repeat, SymbolSize = 20, SymbolSpacing = 5,
    Geometry = Avalonia.Media.Geometry.Parse("M0,10 L5,0 L10,10 Z"),
};
chart.Series.Add(symbols);
```

漏斗按源顺序使用等高槽：宽度为 `W * (NeckWidth + (1-NeckWidth)*value/max)`，末边界为颈宽。金字塔以累计平方根边界分配正值面积，零间距时面积占比对应业务值；两者跳过非正和无效值。Heatmap 按类型和值保留首次出现的行列键，重复单元最后一项胜出；未提供的组合保持空白，`MissingBrush` 只作用于明确给出的无效单元。`LowColor`/`HighColor` 是线性 ARGB 色标，`ShowAxes` 控制行列文字，`ShowValues` 控制单元值。

`PictorialBarSeries` 使用 Cartesian 的轴、分组、方向、基线、业务身份和导出。Stretch 拉伸一个符号到数值柱，Clip 在基线放一个固定 DIP 符号并裁剪，Repeat 从基线重复并裁剪最后一个符号；孔洞和符号间距不产生填充命中。`Geometry` 原地修改后调用 `Refresh()`，或替换为新几何；暂不提供逐点 GeometryPath。

`ThemeRiverStack.CreateBaseline(streams, center)` 为同槽、有限非负流计算 `center-total/2` 的透明底座；空值由调用方明确处理。将底座置于同组 `AreaSeries { StackMode = ChartStackMode.Stacked }` 的首位，后接业务流，形成围绕中心的组合。它不实现 wiggle/streamgraph 算法。Demo 中底座不入图例，河流组合关闭点选和 Tooltip，避免将透明辅助项作为业务项。

本批是基础软件范围；独立分析图尚未统一适配完整 C2 模板/多轴/缩放/范围联动/标注/标准动画和原生点级辅助语义。高级标签避让、大矩阵虚拟化、密集符号/长稳性能、正式物理平台与官方 API 等价仍单独验收。验证结果见仓库 `docs/charts-c3-analysis.md`。

## 工程图与专业坐标（C5-04）

`CarpetPlot`、`HexbinChart`、`SmithChart`、`TernaryChart`、`WindRoseChart` 继承 `EngineeringChartBase`，支持 typed `ItemsSource` 或业务路径映射、`IdPath` 稳定身份与嵌套属性通知。`GetMarks()`、`HitTest()`、`SelectedPoint` 的 `Members` 保留原始 `SourceIndex`、`StableId`、`Item`；Hexbin/Wind 的标记可包含多个成员，`Index` 是标记序号。

Smith 阻抗为 Ω，导纳为 S，参考阻抗始终为 Ω。`Project`/`Unproject` 换算物理量与控件局部 DIP，拒绝奇点。Ternary 的 A 在顶、B 左、C 右；非负分量归一化，逆解返回比例，绝对总量留在原始成员中。

Carpet 明确采用 `X=A+Offset*B, Y=观测值`；完整矩形参数网格支持双线性插值和有界逆解。折叠/奇异网格可显示但拒绝含糊逆解，缺失或重复节点通过 `MeshError` 说明。Hexbin 使用 pointy-top 六边形，`BinRadius` 是 Zoom=1 时的 DIP 外接半径，支持 Count/Sum/Mean。缩放平移变换现有分组；改变布局或半径重新分箱。

Wind Rose 北向起、顺时针，支持度/弧度及业务字符串的 16 方位标签。方向扇区以标签为中心、左闭右开，速度分档 `[lower,upper)`，末档无上界。频数权重须为正，百分比以全部接受的频数为分母。无效观测跳过而保留原始索引。

`Zoom` 为 1–32，`PanX/PanY` 为绘图区宽高的平移比例。滚轮缩放、中键拖动、方向键/Home/End 浏览、Enter/Space 选择和 Esc 复位共用投影；PNG/JPEG 同步、异步导出沿用尺寸、背景、DPI、失败与取消契约。纯数值 API 为 `ChartEngineering`，有取消与输入/生成几何预算；预算不代表百万点交互性能。高级轴格式、多轴、完整 C2 模板/联动/动画、原生辅助语义及官方 API/性能等价仍需专项验收。用法示例在 Demo 与 Charts.Usage，验证及边界见 `docs/charts-c5-engineering-projections.md`。

自动布局、参数、数据变化后，选择按旧 `Members` 顺序寻找第一个仍存在的原始 `StableId` 并定位新标记；所有原成员删除则清除选择。设置 `IdPath` 时按 ID 值比较，否则按源身份引用比较。重分箱不会凭旧标记序号或旧格子选择无关数据。

只读 `DataError : Exception?` 报告源/几何预算、不可表示数值等 `ArgumentException` / `OverflowException` 数据错误：显示错误文字、清空图元/网格与旧交互状态；有效数据恢复后清除错误。状态通知在绘制后发布。纯数值 API 继续抛出明确异常；其他编程异常不会被静默吞掉。Mean 使用稳定在线均值，避免合法有限均值被中间总和溢出拒绝；Sum 和 Wind 的不可表示总频数仍明确失败。

## 进阶比较（C5-05）

`DivergingBarChart`、`DumbbellChart`、`MekkoChart`、`MirrorBarChart`、`PopulationPyramidChart`、`TornadoChart` 和 `VennDiagramChart` 继承 `ComparisonChartBase`，复用源观察、键盘、选择、缩放平移和 PNG/JPEG 导出。业务路径和 typed 模型均可用；纯数值布局在 `ChartAdvancedComparison`，输出保留原始序号，支持取消和输入/生成预算。基础示例：

```csharp
var diverging = new DivergingBarChart
{
    ItemsSource = new DivergingValue[] { new(-6, "A"), new(3, "B"), new(9, "C") },
};
var mirror = new MirrorBarChart
{
    ItemsSource = new PairedComparisonValue[] { new(6, 4, "North"), new(3, 8, "South") },
};
var venn = new VennDiagramChart
{
    SetCount = 2,
    ItemsSource = new VennItem[] { new(1, 60), new(2, 40), new(3, 20) },
};
```

发散条固定中心基线，以最大绝对差值统一两侧尺度，`Baseline` 是原始数值的参考值。哑铃保留输入的先后端点，不自动把反向变化排序成正差；连接线为装饰，端点独立命中。镜像条、人口金字塔和龙卷风使用相同的非负幅度尺度；`CenterGap` 为中心空缝 DIP，人口 `BarGap` 为相邻行的纵向留白 DIP。人口按输入年龄段顺序显示；龙卷风默认按双侧总影响降序，排序平局保持输入顺序。`ComparisonSort` 排序不重写源集合或原始索引。

Mekko 的列宽权重在全部列间归一化，各列段高独立归一化，零权重不产生可选择面积，负数或非有限值明确拒绝。`ColumnGap=0` 时面积为宽度份额乘以段高份额；非零 DIP 间距会缩小绘图面积。业务模式使用 `WidthPath/SegmentsPath/SegmentValuePath/SegmentLabelPath/SegmentIdPath`。每个子段的 `Members[0]` 是原始父列，`Members[1]` 是原始子段，两个 `SourceIndex` 分别是父源序号和该列内子序号；不能将两者当作一个扁平源索引。支持子集合及子属性通知、重排、换源和卸载释放。

Venn 接受两个或三个集合的包含式总量：Mask 1=A、2=B、4=C，3=A∩B，7=A∩B∩C；未给出的交集为0，每个单集合总量必须提供。也可用 `SetsPath` 绑定集合名列表（默认A/B/C，`SetNames`可配置），或用 `MaskPath/ValuePath` 映射业务数据。重复、负数、不一致交集拒绝。显示和选择的是由包含排除法得到的独占区域，`Members` 是参与该值计算的包含式汇总记录依赖，不代表实际集合元素。

两圆交集使用解析面积反解；三圆无法表达任意集合总量，布局明确标记 `IsApproximate`。`Layout.Regions` 同时给出目标 `Value` 与几何 `EstimatedValue`，`MaximumRelativeAreaError` 是最大独占区域绝对偏差除以最大单集合总量；`TripleIntersectionErrorEstimate` 是两种积分步长的差值估计，并非严格误差上界。`UnrepresentedRegionMasks` 列出正值但无可见内部的区域。可见区域按真实排他几何命中，不用重叠圆的包围框代替交集。

排序、重排和布局变化会按源身份与侧别/子段/区域共同追踪选择；删除所选数据则清除。`DataError` 显示数据、预算或不可表示数值错误，清空旧图元并允许同源修正后恢复。高级标签避让、完整 C2 轴/模板/联动/动画、四集合以上 Venn、三集合精确面积拟合、后台/增量布局和正式性能/物理设备资格仍需后续验收；不声明官方 API 或运行时等价。验证、截图和剩余问题见 `docs/charts-c5-advanced-comparison.md`。

## 层级和打包图

`TreemapChart`、`SunburstChart`、`OrganizationChart`、`BubbleCloudChart`、`PackedBubbleChart` 和 `CirclePackingChart` 共享 `HierarchyChartBase`。可以直接提供不可变 `HierarchyNode`，或通过 `ChildrenPath/ValuePath/LabelPath/IdPath` 映射业务节点。`IdPath` 对业务源建议显式指定；没有ID时按原始对象引用保持身份。分支的 `AggregateValue` 是后代叶值之和，分支自身 `Value` 保留为原始业务值，不重复累计。

```csharp
var roots = new HierarchyNode[]
{
    new(999, [new(6, label: "API", id: "api"), new(3, label: "Data", id: "data")],
        label: "Engineering", id: "engineering"),
    new(3, label: "Operations", id: "operations"),
};
var chart = new TreemapChart
{
    ItemsSource = roots,
    LayoutMode = TreemapLayoutMode.Squarify,
    Gap = 0,
    AnimationTriggers = ChartAnimationTriggers.All,
    AnimationDuration = TimeSpan.FromMilliseconds(450),
};
// 在控件已加入窗口并完成布局后操作导航。
chart.DrillDown("engineering");
chart.Collapse("engineering");
chart.Expand("engineering");
chart.DrillUp();
```

`Snapshot.Nodes` 保留原始preorder `Index`、父索引、兄弟 `SourceIndex` 和完整 `Path`；控件的 `Members[0]` 返回原始节点、稳定ID、自身值和叶合计 `Weight`，标记 `Value` 是聚合值。显示序号可随折叠或钻取变化，不能将 `SelectedIndex` 当作原始preorder索引。Treemap和圆打包的父形状可能被后代覆盖，指针命中返回顶层可见节点；父节点仍可显式选择及导航。

Treemap 支持 `Squarify/Slice`；`Gap=0` 时面积对应叶合计份额，非零间距减少实际着色面积。Sunburst 使用等厚深度环、叶合计角度份额，可配置归一化 `InnerRadius`（0至小于0.5）、`StartAngle` 和 `SweepAngle`。Organization 支持横/纵方向和真实父子连接；`MaximumVisibleDepth` 将更深分支作为聚合节点显示。双击或 Enter 钻取，Backspace 返回，+ / - 展开折叠，`ResetDrill()` 返回森林。

Bubble cloud 与 Packed bubble 对可见终端节点绘制统一面积比例的圆，分别采用源序旋转方向及稳定降序布局。Circle packing 在父圆内嵌套子圆，终端面积共用比例；父圆包含包络空隙和6%径向留白，其面积不表示聚合值。算法为16方向的确定性启发式，未声明最优密度、官方内部算法或其气泡像素尺寸默认值。

`GroupPath` 可将原始节点映射到类别，类别颜色在钻取和折叠时沿用全源叶节点的顺序。`ShowLegend` 默认关闭，开启后在底部预留不超过100DIP、且不超过原绘图区三分之一的图例区；最多显示20项，溢出项用数量提示。只读 `LegendItems` 仍提供全部当前可见终端类别、合计和原始 `Nodes`。气泡云与打包气泡可用 `ShowSizeLegend` 显示9:4:1相对面积示意，它不是业务单位的绝对半径刻度。

纯数值 `ChartHierarchy` 接受 `Snapshot` 并提供可取消布局，返回只读归一化结果。源最多10000节点、根深度0至64，打包子树最多2000输入节点且折叠后代仍计数。非有限/负值、循环/重复源、汇总溢出、正比例数值归零及无法区分的打包圆等条件明确失败；控件公开 `DataError` 并支持同源修复恢复。数据和布局可能同步全量处理，尚未声明后台、增量或持续帧率资格。

有效的极小正值仍可低于屏幕分辨率。极小扇区可能出现 `StartAngle + SweepAngle == StartAngle` 的坐标舍入，矩形或圆也可能小于一个像素；数值快照保留正值和原始身份，但不保证形成可见、可命中或可放置标签的区域。当前没有为所有小项强制最小像素尺寸，不能将这种可见性限制写作源数据无效。

动画支持入场、数据和视图触发、`AnimationDuration/AnimationDelay/AnimationEasing`、暂停/继续、`CancelAnimations()`、`ReplayEntranceAnimation()`、`ReduceMotion` 与点数限制。`AnimationTriggers` 默认关闭，Demo显式启用；点数限制计算旧帧和目标总数，减少动态效果由应用显式设置。没有几何变化的更新直接稳定，不强制造帧；退出节点只作非交互装饰，不进入公开标记或键盘选择。导出使用当前帧及统一PNG/JPEG尺寸、DPI、背景和异步接口。使用示例见 `samples/Charts.Usage`，平台验证及边界见 `docs/charts-c6-hierarchy-packing.md`。

## 专业图族 PlotAreaContent 与 Trackball

`EngineeringChartBase` 和 `AnalysisChartBase` 的派生控件共用 `PlotAreaContent`、`PlotAreaContentTemplate` 与 Trackball 模式及外观属性。对应 StyledProperty 复用 Cartesian 的属性身份，但专业图族的模板上下文使用下面的独立类型；不能将 Cartesian 的 `ChartPlotAreaContext` / `ChartTooltipContext` 模板直接作为专业上下文使用。当前平台证据、Demo 截图和剩余问题见 [C7 PlotAreaContent / Trackball 报告](../../docs/charts-c7-plotarea-trackball.md)。

```csharp
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using NT.AvaloniaControl.Charts;

var professional = new FunnelChart
{
    ItemsSource = new double[] { 100, 60, 25 },
    PlotAreaContent = "目标区说明",
    TrackballMode = ChartTrackballMode.Vertical,
    TrackballDisplayMode = ChartTrackballDisplayMode.GroupAllPoints,
    MaxTrackballReadings = 32,
};
professional.PlotAreaContentTemplate = new FuncDataTemplate<ChartDataPlotAreaContext>((_, _) =>
{
    var caption = new TextBlock();
    caption.Bind(TextBlock.TextProperty, new Binding(nameof(ChartDataPlotAreaContext.Content)));
    return caption;
});
professional.TrackballContentTemplate = new FuncDataTemplate<ChartDataTrackballContext>((_, _) =>
{
    var text = new TextBlock();
    text.Bind(TextBlock.TextProperty, new Binding(nameof(ChartDataTrackballContext.Text)));
    return text;
});
// 完成窗口布局后，也可显式读取；真实指针和键盘浏览会更新读数。
professional.UpdateTrackball(professional.PlotArea.Center);
var retained = professional.TrackballReadings;
var totalBeforeBudget = professional.TrackballTotalReadings;
// 每个读数各自呈现一张卡片；切回 GroupAllPoints 则合并为一张。
professional.TrackballDisplayMode = ChartTrackballDisplayMode.FloatAllPoints;
professional.ClearTrackball();
```

`PlotAreaContentTemplate` 接收 `ChartDataPlotAreaContext`，其中 `Content` 是设置的内容，`DataContext` 是当前业务 VM；`EngineeringMarks` 或 `AnalysisMarks` 直接引用对应根的不可变实际帧，不重新枚举 `ItemsSource`。内容在真实 `PlotArea` 内布局、裁剪，位于已绘制图元上方、永久标签和图例下方，继承图表资源、字体、前景与主题。它是被动呈现，不接收指针和 Tab；不带模板的 `Control` 内容直接继承业务 DataContext，已有父级的外部控件明确拒绝。

专业图族没有统一的数值轴，`ProjectLayout(xFraction, yFraction)` / `UnprojectLayout(plotLocal)` 因此使用未变换绘图区的视觉布局比例：左上为 `(0,0)`，右下为 `(1,1)`，Y 向下；工程图纳入实际 `Zoom/PanX/PanY`，分析图保持单位布局。`VisibleLayoutBounds` 给出可见的未变换比例范围；`ToLocal(chartLocal)` / `ToChart(plotLocal)` 仅在图表与绘图区局部 DIP 间换算。地图经纬度、排程时间、层级权重与分类值仍使用各图族自身的数据和投影接口，不能当作上述布局比例。

Trackball 默认 `None`，保留既有 Tooltip 行为；启用 `Vertical` 或 `Horizontal` 后优先使用 Trackball，即使 `IsTooltipEnabled=false` 也可读数。它在当前可见图元的包围区间上沿显示 X 或 Y 寻找切片：保留与该坐标相交的全部图元，没有相交时保留最近区间的并列候选；候选顺序沿用图族绘制顺序，不把地理、树形、排程或聚合几何伪造成 Cartesian 线段插值。读数以当前实际图元为准，包含重叠或重复坐标图元，超出绘图区及不可见图元不参与。

`ChartDataTrackballReading` 提供 `Members`、`Text`、`Position`、`Bounds`、`Brush`、`Value`、`BusinessDataContext`，以及对应的 `EngineeringHit` 或 `PointHit`。每个聚合读数保留完整原始成员、原始 `Item`、`StableId`、`SourceIndex`；限制读数数量不会截断一个聚合标记的成员。`ChartDataTrackballContext.Readings` 在 Group 模式包含全部保留读数，在 Float 模式只包含当前卡片的一个读数；`BusinessDataContext` 单独保存业务 VM，`Anchor` 为图表局部 DIP。`TotalReadings` 说明整个切片在预算前的候选数，`IsTruncated` 说明是否省略了其他图元；这不是单个聚合的原始成员总数。

`MaxTrackballReadings` 默认32，允许0～1024；0不创建读数卡片，仍可通过 `TrackballTotalReadings` 看到切片候选总数。查询只扫描现有图元，不复制或重新枚举全部业务源，临时候选和卡片数量受预算约束；模板自行展开 `Members` 的控件数仍由应用负责。Float 卡片按实际测量尺寸尝试邻近的无重叠位置，并限制在图表范围内；可用空间不足时仍可能重叠或裁剪，密集上千卡片不构成交互性能承诺。`TrackballFormatter` 接收专业读数列表，Group 调用一次，Float 每张卡片调用一次；默认格式组合图族标准文本。返回 null 隐藏默认卡片，自定义模板仍可收到读数。

`ShowTrackballLine`、`TrackballLineStroke/TrackballLineStrokeThickness`、`ShowTrackballMarkers` 与 `TrackballMarkerSize/TrackballMarkerStroke/TrackballMarkerFill/TrackballMarkerStrokeThickness` 控制线与标记。线和标记裁剪在绘图区，并显示在 PlotAreaContent 上方；卡片继承 `TooltipBackground/TooltipForeground/TooltipBorderBrush` 和现有主题，高对比及局部外观值保持优先。模板为真实被动控件，稳定帧和指针更新复用子树，应通过绑定读取新的上下文。Build 返回 null 隐藏该卡片；模板或 Formatter 异常清理旧读取并传播，失败项停止重试，替换对应模板或 Formatter 恢复。换源、业务上下文变化、隐藏、禁用和卸载清除旧读数上下文；移出绘图区、复位和 Escape 清除 Trackball。

PlotAreaContent 是永久内容，进入同步、异步 PNG/JPEG 导出，并沿用尺寸、DPI、背景、取消和失败契约；Trackball 的线、标记和卡片均为瞬态内容，与 Tooltip 一起排除。模板更新、布局或绘制期间的重入导出会明确拒绝，避免输出混合帧。Demo 与 Charts.Usage 提供两种根、真实聚合和地图数据、布局与业务绑定、Group/Float、纵向/横向模式及导出对照示例。默认专业线和标记使用相反明暗色的轮廓改善局部同色填充上的可见性；显式 Stroke 保留调用方外观。

## 专业范围选择与业务联动

EngineeringChartBase 与 AnalysisChartBase 共享 `SelectedDataKeys` / `DataSelectionRange` 的 TwoWay 属性身份。`SelectedData` / `GetSelectedData()` 返回当前可见图元中匹配的原始 ChartDataPoint；重复绘制同一原始观察不会重复返回。范围选择或点击聚合图元时包含其全部原始成员；按 `SelectedDataKeys` 请求部分成员时只返回匹配项，整个聚合图元仍会高亮。例如 Mosaic 聚合包含 a、c，只请求 a 时公开选择仅包含 a。

`SelectedDataKeys` 使用各图自身的原生身份：显式 ID 按值匹配，隐式来源按对象引用匹配。地图键使用带图层作用域的 MapGlyphKey，关系图键使用区分节点与边的 RelationGlyphKey。直接请求中暂时缺失的原生键会保留；公开选中数据在隐藏、禁用、卸载时释放旧来源引用。设置新的键请求会清除本地范围；范围自身产生的派生键保持该范围。点击和 Enter/Space 接入数据选择，ResetViewport 清除范围和多选。`ClearDataSelection()` 清除独立多选与范围，既有 SelectedIndex 仍需按其单点契约单独清理；直接设置 SelectedIndex 也不隐式改写独立多选绑定。

`SelectDataRange(new Rect(x,y,width,height))` 使用当前绘图区从左上到右下的 0～1 布局比例，宽高为正且整个矩形位于此区间内。匹配采用可见图元的轴对齐几何包围盒相交；圆弧、孔洞或斜线的包围盒空白也可能匹配，零宽或零高线段的包围盒按 0.5 DIP 膨胀。它保留匹配图元的完整原始成员，并保持坐标轴和视口。当前提供矩形 API 和范围条输入，图内拖拽框选、套索与任意多边形选择仍属于扩展范围。

`ChartDataRangeSelector` 只设置 EngineeringChart 或 AnalysisChart 中的一个目标，通过 Axis 指定 X/Y，复用已有双手柄指针、键盘和辅助语义。横纵范围条分别保留另一个方向区间。挂载时既有图表范围优先；卸载时提出的非默认 Range 仅在目标没有范围时初始化它。MinimumSpan 约束本地请求，外部设置到图表上的较窄范围仍保持其原值。永久选中高亮进入 PNG/JPEG 同步和异步导出，临时范围框排除。

```csharp
// 两图的 ItemsSource 和其他业务路径已经配置，窗口打开时两图可见。
funnel.IdPath = nameof(Order.Id);
mosaic.IdPath = nameof(Order.Id);
var links = new ChartDataSynchronizationGroup();
links.Add(funnel, static point => ((Order)point.Item!).Id);
links.Add(mosaic, static point => ((Order)point.Item!).Id);
ownerWindow.Opened += (_, _) =>
{
    // SelectedDataKeys 是源图原生键；此处 IdPath 使其成为 Order.Id。
    funnel.SelectedDataKeys = new object[] { "order-a" };
    // 注册本身不选定初始来源；已挂载、可见且启用后显式复制。
    links.SynchronizeFrom(funnel);
};
ownerWindow.Closed += (_, _) => links.Dispose();
```

组的 keySelector 只负责跨图业务键映射，保持各图原生身份契约。未配置显式 ID 的普通来源使用原始 Order 对象引用请求选择；仅在 Add 中返回 Order.Id，并不会让源图接受字符串原生键。Add、Remove、SynchronizeFrom、Dispose 和选择操作均在 Avalonia UI 线程执行。

成员使用弱引用，只有挂载、可见且启用的成员发送和接收变化。组会强持有显式 keySelector 委托和 layoutDomain 值；它们捕获的图表或业务 VM 仍由调用方管理，Remove/Dispose 会释放这些引用。来源排序和聚合方式可以不同，业务键负责匹配；默认只联动选择。联动读取当前真实绘图帧保留的成员，包括被本地视口裁剪的图元，避免不同平移意外清除业务选择；公开 SelectedData 仍只报告可见选择。目标没有生成相应图元时，组无法为该业务键构造原生选择；应用仍可在目标 SelectedDataKeys 中保留自己的未知原生键请求。

范围或工程 Zoom/Pan 联动需分别开启 SynchronizeRange / SynchronizeViewport，并在 Add 时给出相同且非空的 `layoutDomain`，表示应用确认这些布局可比较。Analysis 没有对应工程视口。隐藏成员重新出现时不会自动广播旧状态，应用在需要重放时显式同步。业务回调提出的新状态优先；矛盾循环在有限次数后明确失败。组联动选择、高亮、范围与可比较的工程视口；交叉筛选和查询由应用依据事件与业务键更新查询或 ItemsSource。

Demo 与 Usage 的异构看板展示业务选择联动和各图局部范围，Strip 缩放也保持本地视图。同一 layoutDomain 的范围/视口联动由测试覆盖，四图看板没有把这些不同布局注册到共同域。范围、监听生命周期和代表性动画导出验证见[本批报告](../../docs/charts-c7-range-linking.md)。
