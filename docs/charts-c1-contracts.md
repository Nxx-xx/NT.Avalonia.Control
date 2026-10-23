# Charts C1 接入与公共契约

更新日期：2026-10-15。本文件按当前源码描述已经落地的契约及迁移事项，适用于 `NT.AvaloniaControl.Charts`。它补充 [开发计划](charts-development-plan.md) 的 C1 工作项，不替代执行结果、平台报告或 [官方覆盖矩阵](charts-official-alignment.md)。

C2 已扩展图例/标签/标记、共享读数、SelectedPoints 与选择模式、键盘、标注、PNG 和辅助语义；这些新增契约以 [C2 续作](charts-c2-common.md)及 [C2 API 快照](charts-c2-api.txt)为准，C1 API 文件保留历史。

当前渲染系列仍为 `LineSeries`、竖向分组 `BarSeries`、`ScatterSeries`。新增底座允许派生系列与独立渲染器接入；不表示其他图族、全部 C1 验收项或官方 API 兼容已经完成。原始 C0 API、源码及程序集基线保持冻结；本文件不会回写 `charts-c0-api.txt` 或 `charts-c0-baseline.json`。

## 宿主、绑定与所有权

[ChartBase](../src/NT.AvaloniaControl.Charts/ChartBase.cs) 继承 `TemplatedControl`，提供 `ResetViewportCommand : ICommand` 和抽象 `ResetViewport()`。`CartesianChart` 继承该基类，轴与系列分别继承 `StyledElement`。系列和轴是图表的逻辑子元素，可继承宿主的 `DataContext` 和资源，支持 AXAML 绑定与 `DynamicResource`。

| 入口 | 类型与行为 |
| --- | --- |
| `CartesianChart.Series` | 只读属性，内容为可修改的 `ObservableCollection<ChartSeries>`，适合 AXAML 声明与代码添加。 |
| `CartesianChart.SeriesSource` | 可绑定的 `IEnumerable<ChartSeries>?`。非空时成为活动系列源，覆盖 `Series` 的输入；空集合也表示不绘制系列。设为 `null` 才重新使用 `Series`。两者不合并。 |
| `CartesianChart.XAxis` / `YAxis` | 主轴。每个图表拥有独立的默认轴实例。 |
| `CartesianChart.Axes` | 附加轴集合，通过系列的轴键引用。附加轴成为逻辑子元素，但只有实际使用的附加轴参与布局。 |

活动系列、主轴及附加轴实例只能有一个逻辑宿主。把同一 `ChartSeries` 或 `ChartAxis` 同时放入两个图表会抛出 `InvalidOperationException`。多个图表可共享业务数据集合，但应各自创建系列和轴。转移实例时，先从旧图表的活动源或轴集合移除，再加入新宿主。

逻辑所有权与外部事件订阅是两件事：图表脱离视觉树时保留逻辑子元素和继承关系，同时释放对系列、轴以及外部 `SeriesSource` 集合的活动订阅；重新附加时重新建立订阅。仅从窗口移除旧图表，并不会把其系列实例释放给另一图表。`SeriesSource` 的集合原地变化需要 `INotifyCollectionChanged`；普通列表可通过替换 `SeriesSource` 重新同步。

业务集合、业务对象及其资源归应用所有，图表不会销毁或 `Dispose` 数据源。应用自行订阅 `Changed`、`DataError` 等事件时，也应自行退订。实现依据：[宿主与订阅](../src/NT.AvaloniaControl.Charts/CartesianChart.cs)、[系列生命周期](../src/NT.AvaloniaControl.Charts/ChartSeries.cs)。

真实 AXAML / MVVM 接入见 [Charts.Usage](../samples/Charts.Usage/MainWindow.axaml)，包含编译绑定、系列集合替换、轴标题继承、动态画刷资源、视图及单点选择双向绑定。核心写法如下，`Records` 为业务集合，`Month`、`Value`、`Id` 为对象公开属性：

```xml
<charts:CartesianChart IsZoomEnabled="True" IsPanEnabled="True"
                       Viewport="{Binding Viewport, Mode=TwoWay}"
                       SelectedPoint="{Binding SelectedPoint, Mode=TwoWay}"
                       PointInvokedCommand="{Binding PointInvokedCommand}">
  <charts:CartesianChart.XAxis>
    <charts:CategoryAxis Title="月份" />
  </charts:CartesianChart.XAxis>
  <charts:CartesianChart.YAxis>
    <charts:NumericAxis Title="{Binding AxisTitle}" />
  </charts:CartesianChart.YAxis>
  <charts:CartesianChart.Series>
    <charts:LineSeries ItemsSource="{Binding Records}"
                       CategoryPath="Month" ValuePath="Value" IdPath="Id"
                       Stroke="{DynamicResource UsageSeriesBrush}" />
  </charts:CartesianChart.Series>
</charts:CartesianChart>
```

## 数据入口、路径与错误

`ChartSeries.ItemsSource` 当前类型为非泛型 `System.Collections.IEnumerable?`，允许以下输入：

| 输入 | XY 解释 |
| --- | --- |
| `ChartPoint` 或 Avalonia `Point` 集合，未设置分类/值路径 | 直接保留 XY，包括源顺序和重复 X；不会自动排序。 |
| 数值集合，未设置路径 | X 为零基源索引，Y 为数值。 |
| 对象集合 | `CategoryPath` 指定 X 或类别，`ValuePath` 指定 Y；缺省分类路径时 X 为源索引。 |
| 日期分类属性 | `DateTime` / `DateTimeOffset` 转为 UTC Unix 毫秒，配合 `DateTimeAxis` 使用。 |
| 字符串类别 | 数字字符串先按 invariant culture 尝试转为数值；其他字符串在系列快照中先使用源索引，`CategoryAxis` 再按类别文本共享映射。 |

路径是区分大小写、以点分隔的公开实例属性，例如 `Reading.Temperature`。支持空中间对象；缺失属性、索引器或读取失败通过 `DataError` / 快照 `Issues` 报告。字段、字典键、索引表达式和任意查询语法不属于当前契约。访问器按运行时类型和属性名缓存，编译后复用；这不构成 NativeAOT / trimming 已验证声明。

数字转换接受可转换的有限数值及 invariant 数字字符串，拒绝 `null`、`bool`、`char`、NaN 和 Infinity。不支持的分类对象无法获得有效 X；空值与无效 Y 进入缺失点策略。`HighPath`、`LowPath`、`WeightPath` 可将可选有限值放入快照，当前三种渲染器不消费这些字段；它们不代表区间、OHLC 或气泡图已经实现。

`DataError` 的类型为 `EventHandler<ChartDataErrorEventArgs>`，载荷 `Issue` 包含 `SourceIndex`、`Path`、`Message`；单次映射最多收集 100 条问题。读取路径失败通常生成缺失点；显式 `IdPath` 产生空或重复标识则会报告并抛出 `InvalidOperationException`。映射或源枚举重入也会失败，不会悄悄接受不一致结果。实现依据：[映射](../src/NT.AvaloniaControl.Charts/ChartSeries.cs)、[属性访问器](../src/NT.AvaloniaControl.Charts/ChartDataAccessor.cs)。

## 可观察更新、缺失点与批处理

集合和业务项应在 Avalonia UI 线程更新。活跃订阅期间，系列响应 `INotifyCollectionChanged` 的 Add、Remove、Replace、Move、Reset，以及映射路径各层对象的 `INotifyPropertyChanged`；嵌套对象替换会重新建立观察链。普通列表原地修改，或对象不发送属性通知时，调用 `series.Refresh()` 显式刷新。后台直接修改受观察集合不会自动调度到 UI 线程。

| `EmptyPointMode` | 当前行为 |
| --- | --- |
| `Gap`，本库默认 | 保留缺失；折线在该点断开。 |
| `Zero` | 有限 X 的缺失 Y 置为 0。无效 X 不补值。 |
| `Average` | 使用源顺序中两侧最近的原始有效点 Y 的平均值。同一缺失段采用同一平均值；缺少任意一侧时仍为空。 |
| `Interpolate` | 两侧原始有效点 X 差为有限正数时按 X 插值，否则按源索引位置插值；缺少任意一侧时仍为空。输入不按 X 排序，因此非单调数据不保证位于两端 Y 之间。 |

补值不修改业务源。快照中 `WasEmpty` 保留原始缺失状态，补齐后 `IsEmpty` 可变为 `false`。轴有效域是后续检查：例如对数轴不能绘制 `Zero` 补出的 0，仍会作为无效点处理。

```csharp
using (series.DeferUpdates())
{
    records.Add(first);
    records.Add(second);
    // 此处的集合与业务对象仍由应用在 UI 线程修改。
}
```

`SuspendUpdates()` / `ResumeUpdates()` 支持嵌套，`DeferUpdates()` 是可释放作用域。最外层恢复时合并待发的 `Changed`；没有配对挂起时调用 `ResumeUpdates()` 会抛出异常。批处理合并通知，不暂停源变化、版本递增或所有内部观察工作，也不承诺大数据更新达到 O(1)。作用域应在 UI 线程结束。

## 快照、身份与异步发布

[ChartDataSnapshot](../src/NT.AvaloniaControl.Charts/ChartDataSnapshot.cs) 是一次源版本的投影，包含 `Version`、只读 `Points` 与 `Issues`。构造函数复制容器；`Item`、`Category`、`StableId` 仍可能引用应用可变对象，不是深复制，也不使对象跨线程访问自动安全。

每个 `ChartDataPoint` 保留 `SourceIndex`、`StableId`、`Item`、`Category`、XY、空点状态及可选 High/Low/Weight。`SourceIndex` 是原始源位置，不能用绘制后筛选索引或未来抽样索引替代。`CreateSnapshot()` 在 UI 线程调用；活跃订阅下复用匹配版本的快照，无订阅时重新枚举而不长期持有外部订阅。

无映射路径的 typed 点/数值源使用自有不可变值缓冲，包括仅支持泛型枚举的迭代器；源修改不会改写旧快照。公开 `Points` 的 Item/Category/StableId 按需展开，全量枚举仍有对应 metadata/装箱成本。`Value` 保存对象 ValuePath 的原始值供 Y 类别映射；直接点/数值源为 null。内置绘制、范围和查询使用轻量坐标通道，最终命中仍返回完整原始项和稳定身份。INCC 编辑当前为 O(n) copy-on-write，并非连续流性能保证。

| 标识方式 | 保持与失效规则 |
| --- | --- |
| 设置 `IdPath` | 读取非空且唯一的键，使用值相等判断。保留相同键可跨对象替换关联选择；重复或空键被拒绝。 |
| 无 `IdPath`，引用类型业务项 | 业务对象本身作为标识，按引用相等关联。同一对象重复出现不提供独立的出现次数标识；可靠区分应使用唯一 `IdPath`。 |
| 无 `IdPath`，值类型或空项 | 使用内部条目标识。Move 保留该标识；Reset、源替换、`Refresh()` 或无订阅重新枚举可重建条目，不承诺跨这些操作保持身份。 |

`SourceVersion` 是源或映射修订号，不是数据哈希或全局 ID。源、映射配置及受观察的数据变化推进版本，纯颜色/标题变化不作为数据修订。首次加入 `Changed` 活跃订阅也会重建观察并推进版本；无订阅时不能用它证明源从未变化。应在观察已经建立后获取用于后台结果的版本和标识。

发布入口为：

```csharp
bool TryPublishSnapshot(ChartDataSnapshot snapshot, long expectedSourceVersion,
    CancellationToken cancellationToken = default);
Task<bool> PublishSnapshotAsync(ChartDataSnapshot snapshot, long expectedSourceVersion,
    CancellationToken cancellationToken = default);
```

`TryPublishSnapshot` 必须在 UI 线程调用；`PublishSnapshotAsync` 将发布调度到 UI 线程。两者都要求仍有活跃 `Changed` 订阅者，通常是已附加的图表；仅存在逻辑宿主或仅订阅 `DataError` 不满足条件。应用也可以自行管理 `Changed` 订阅，但需负责退订。

发布时必须同时匹配 `SourceVersion`、`snapshot.Version` 和预期版本。还会检查条目数量、源索引唯一且在范围内、稳定标识唯一且与源一致、原始项匹配、非空坐标及可选数值有限。无活跃订阅、陈旧、取消或不合法的结果返回 `false`；线程访问、空参数等调用错误仍可能抛出异常。引用类型原始项按引用匹配，值类型按值匹配；不能通过重新构造虚假标识绕过校验。

推荐在 UI 线程获取一致的源版本、快照身份和可供后台计算的独立只读数值输入；后台只处理该副本，保留原始索引/标识/项关联，完成后提交同版本结果。不要在后台枚举仍由 UI 修改的 `ObservableCollection` 或读取可变业务属性。成功发布替换缓存投影并通知图表，不写回 `ItemsSource`，也不推进源版本；后续源变化使其失效。这是版本校验的快照发布，不是连续流接入、队列容量、背压或实时吞吐量保证。

## 轴、坐标域与多轴

[ChartAxis](../src/NT.AvaloniaControl.Charts/ChartAxis.cs) 本身保留可实例化的线性行为，方便原有 `new ChartAxis()` 代码迁移。具体轴类型为：

| 类型 | 原始数据域、间隔和默认行为 |
| --- | --- |
| `ChartAxis` / `NumericAxis` | 有限 double，线性；`Interval` 使用原始数据单位。 |
| `CategoryAxis` | 零基类别槽位；刻度间隔至少为整数 1。相同 X 轴的多个系列共享类别文本到槽位的映射。 |
| `DateTimeAxis` | UTC Unix 毫秒；`IntervalUnit=Auto` 时显式 `Interval` 表示毫秒，其他单位表示单位数量。Month/Year 按日历推进，数量取至少为 1 的整数。 |
| `LogarithmicAxis` | 只接受有限正数，`LogBase=10` 且必须有限并大于 1；`Interval` 表示指数间隔，例如 base 10、interval 1 为每个数量级。 |

分类映射由图表临时建立，不修改应用的 `axis.Labels`。X 方向读取 `CategoryPath`，Y 方向读取 `ValuePath` 保留的原始 `ChartDataPoint.Value`；相同轴的系列共享首次出现顺序。显式 `Labels` 按名称匹配，未知类别为空点；没有相应路径的直接 XY 数据可使用数字槽位。`PlotMode=OnTicks` 默认不增加类别半槽，`BetweenTicks` 两端各增加半槽；`Gap` 再增加非负槽数。显式上下限优先，单类别自动范围至少保留一个可绘制槽。

日期转换用 `DateTimeAxis.ToUnixMilliseconds(DateTimeOffset)` 或 `ToUnixMilliseconds(DateTime)`。`DateTimeOffset` 按其实际 UTC 时刻转换；`DateTimeKind.Local` 转 UTC，`Unspecified` 明确按 UTC 解释。原始数据、`Minimum` / `Maximum`、视口和逆投影均使用 Unix 毫秒，不是 OADate 或 .NET ticks。`FromUnixMilliseconds(double)` 将毫秒向下取整并返回 UTC `DateTimeOffset`；非有限或超出公开日期常量范围会抛出 `ArgumentOutOfRangeException`。未实现任意显示时区、交易日历或 DST 会话规则。

| 公共属性 | 当前契约 |
| --- | --- |
| `Minimum` / `Maximum : double?` | 原始数据域可选边界，默认 null 自动。空源、常量和不合法配置由内部尺度修复为可绘制范围；应用仍应传入对应轴有效且递增的边界。 |
| `DesiredTickCount : int` | 默认 6，是近似目标；计算时限制为 2～30，不保证正好输出该数量。 |
| `Interval : double?` | 默认 null 自动。非有限或非正值回退自动间隔；密集刻度会稀疏化，不能保证绘制每一个请求间隔。 |
| `IsInversed : bool` | 默认 false；反转投影方向，不交换原始范围的最小/最大含义。 |
| `Position : ChartAxisPosition` | 默认 `Near`。纵轴 Near 左、Far 右；横轴 Near 下、Far 上。 |
| `Key : string?` | 可被 `XAxisKey` / `YAxisKey` 引用。空系列键使用主轴；非空键必须唯一匹配主轴或 `Axes` 中的一根轴，否则建场景时抛出异常。 |
| `Labels : IReadOnlyList<string>?` | 显式类别标签；订阅期间还观察其可选的 `INotifyCollectionChanged`。普通列表原地变动应替换属性值以通知。 |
| `LabelFormat : string?` | 数值或日期格式；日期的 `DateFormat` 非空时优先。自动格式随间隔选择；格式化使用当前 culture。 |
| `LabelRotation` / `LabelWrap` / `LabelMaxWidth` | 默认 0 度 / false / 120。旋转按度设置；换行最多三行，超出部分裁剪。 |

### 次刻度、标签与独立样式

`MinorInterval` 为正的可选次间隔，优先于 `MinorTickCount`。数值/日期轴分别使用原始单位/Unix 毫秒，对数轴使用指数单位。没有显式次间隔时，`MinorTickCount` 在相邻主刻度之间加入指定数量的内部次刻度；默认 0，最大 100。次刻度不与主刻度重复，单轴数量上限 1000。`ShowMinorGridLines` 默认 false。

`LabelMode` 包含 `Auto`（默认）、`None`、`Hide`、`Wrap`、`Stagger`、`Rotate`。`Rotate` 配合 `LabelRotation`，未指定角度时用 45 度；90 度也可直接配置。`Auto` 根据当前尺寸和视图的实际文本测量选择原样、错行、换行或旋转，并在必要时抽稀标签。标签抽稀不删主刻度或网格。缩放后重新计算密度，保留全域预留的绘图区，避免图表跳动。`GetAxisLayout(axis)` 返回有效策略、角度、行数、stride 和图表局部预留矩形，适合诊断。

轴提供 `IsVisible` 总开关及 `ShowAxis`、`ShowTitle`、`ShowLabels`、`ShowTicks`、`ShowGridLines` 独立开关。隐藏整根轴隐藏其装饰、网格和区间，仍保留系列的坐标投影。`TitleBrush` / `LabelBrush`、两类字体/字号、基线/主次刻度/主次网格画刷、线宽、刻度长度可分别配置；基线及主次网格支持 `IDashStyle`、`PenLineCap` 和 `PenLineJoin`。空画刷/字体沿用图表设置或主题。隐藏刻度不再为其长度移动标签。

### 断轴与绘图区间

`ScaleBreaks`、`PlotBands` 为可修改的 inline 集合；非 null 的 `ScaleBreaksSource` / `PlotBandsSource` 优先，空集合也会覆盖 inline。支持真实 AXAML 绑定、INCC 增删替换、子项属性通知和 DataContext 继承。每个子项只能有一个轴宿主。最后一个活动 owner 卸载时释放外部订阅，重挂后重新同步；普通非通知集合原地修改应替换 source。

`ChartScaleBreak` 使用有限原始域 `Start` / `End`。`End <= Start` 忽略，交叠或相邻区间合并，采用最小 `CompressionRatio`（默认 .05，实际计算下限 1e-12）。严格内部不绘点、不命中，端点有效；穿越区间的线条在压缩带处裁断。压缩带保留连续逆投影，允许视图边界落在其中。超出可见/日期范围的端点先裁剪。合并后的断轴标记采用源中首个相交项的 `Stroke` / `StrokeThickness`。这些压缩和端点规则是本库明确选择的契约。

`ChartPlotBand` 的 Start/End 默认 NaN，分别表示当前轴起点/终点；数值/日期/对数轴使用原始域，分类轴使用槽位索引。支持 Fill/Stroke/线宽、Opacity（默认 1）、Text/Foreground/FontSize、水平及垂直 Start/Center/End 对齐、IsVisible 和 BehindSeries/AboveSeries 图层。区间及文字裁剪到绘图区。`RepeatInterval` 按原始单位平移区间，`RepeatCount` 默认 1；设 `RepeatUntil` 时重复到该原始域上界并截断末段。RepeatCount=0 始终不画，重复最多 10,000 次；无有效间隔只画至多一次，逆序区间忽略。

```xml
<charts:NumericAxis MinorInterval="5" ShowMinorGridLines="True"
                    ScaleBreaksSource="{Binding Breaks}" MaximumZoom="100">
  <charts:NumericAxis.PlotBands>
    <charts:ChartPlotBand Start="80" End="100" Text="高峰区" Fill="Orange"
                          Opacity="0.2" VerticalTextAlignment="End" />
  </charts:NumericAxis.PlotBands>
</charts:NumericAxis>
```

连续轴由应用显式选择。无法转换或超出该轴有效域的数据成为缺失点；本库不会因某个系列不兼容而自动把整个图表切回类别布局。官方默认值、属性命名和混合兼容回退规则存在差异，详见覆盖矩阵；这不是官方程序集的直接替换。

一根轴实例不能同时用于 X 和 Y。多个 Y 轴或 X 轴按各自数据和变换计算完整范围及留白，系列使用其指定投影。附加轴的可见切片按主轴在变换空间中的归一化范围同步；当前不是每根轴独立保存视口或独立缩放手势。

```csharp
var chart = new CartesianChart
{
    XAxis = new DateTimeAxis { Title = "UTC", DateFormat = "MM-dd HH:mm" },
    YAxis = new NumericAxis { Title = "温度" },
};
chart.Axes.Add(new LogarithmicAxis
{
    Key = "load", Position = ChartAxisPosition.Far, Title = "负载", LogBase = 10,
});
chart.Series.Add(new LineSeries
{
    ItemsSource = records, CategoryPath = "Timestamp", ValuePath = "Temperature",
});
chart.Series.Add(new ScatterSeries
{
    ItemsSource = records, CategoryPath = "Timestamp", ValuePath = "Load", YAxisKey = "load",
});
```

`IChartAxisTransform` 的 `IsValidValue`、`Transform`、`Inverse` 描述原始数据与轴空间转换，尚未包含视口和屏幕方向。`ChartAxis` 的方法可重写。内部 `ChartScale` 保持 internal，负责原始域边界/刻度、归一化、反向和逆归一化，不作为公共扩展入口。有效但越出显式范围的坐标保留外插比例后由绘图区裁剪，避免把图外散点夹到边缘，或扭曲穿越边界的折线。

## 渲染器与公共投影

自定义系列派生 `ChartSeries` 并重写 `Renderer : IChartSeriesRenderer`。默认渲染器为空，不会自动按派生类型猜测图形。加入系列不需要修改图表中心的类型分支；扩展必须共同提供范围、绘制、命中和图例符号。定义见 [ChartSeriesRenderer](../src/NT.AvaloniaControl.Charts/ChartSeriesRenderer.cs)。

| 成员 | 责任 |
| --- | --- |
| `GetBounds(series, data)` | 返回原始数据域 `ChartSeriesBounds?`；空数据可返回 null，有限且不倒置的常量边界有效。 |
| `IncludesZero` | 请求自动 Y 范围包含 0，但仅在该轴接受 0 时生效。 |
| `UsesCategorySlots` | 请求分组槽位；宿主按共同 X 轴安排组索引与组宽。 |
| `LegendSymbol` | 当前为 `Line`、`Rectangle`、`Circle` 三种静态图例符号。 |
| `Render(drawing, context)` | 使用该场景快照和投影绘制，图表裁剪绘图区。 |
| `HitTest(context, position, tolerance)` | 返回 `ChartSeriesHit?`，包含原始 `SourceIndex`、图表局部坐标和有限非负距离平方。 |

`ChartSeriesRenderContext` 提供 `Series`、`Data`、`Projection`、回退 `Brush`、`GroupIndex`、`GroupCount`、`GroupWidth` 与 `PlotArea`。快照先经过类别映射及轴有效域过滤；自定义渲染器仍应识别空点和非有限投影。图例符号不是任意控件模板；系列本身也不是独立的模板控件。

`IChartProjection` 提供 `PlotArea`、`Project(ChartPoint)`、`Unproject(Point)`、`MapX(double)`、`MapY(double)`、`YOrigin`。投影输入输出的数据部分均为原始域，包括日期毫秒和对数轴原值；屏幕坐标相对于图表。逆投影同时考虑实际绘图区、指定轴变换和反向。`YOrigin` 为零基线的屏幕坐标；轴不接受 0 时采用最低有效边界，适用于对数柱图等场景。

绘制和命中应使用同一几何及投影。宿主校验命中点的源索引、坐标和距离，再返回 `ChartPointHit`，附带原始 `Item` 与 `StableId`。不能把可见子集的数组下标当作 `SourceIndex`。内置柱图绘制和命中共用矩形计算；折线按源顺序连接，缺失点断线。

## 视图、选择与变更通知

`Viewport : ChartViewport?` 为可双向绑定的请求视口，四个字段为主轴原始域的有限且严格递增边界。null 表示自动视图。请求会按完整范围与内部最小跨度约束，实际范围通过只读可观察 `VisibleRange` 获取；`PlotArea` 和 `HoveredPoint` 也有只读 Avalonia 属性通知。场景在绘制中重建时，状态通知可延后到 UI 调度，以免在绘图期间修改保留内容；调用方不应要求每次源赋值都同步产生最终布局状态。

`ResetViewport()` / `ResetViewportCommand` 清除图表请求和各轴的 `ViewMinimum` / `ViewMaximum`，保留 `Minimum` / `Maximum` 数据域边界。每轴 `MinimumZoom` / `MaximumZoom` 默认 1 / 1,000,000，均须有限且 >=1；最大值小于最小值时按最小值处理。缩放限制相对于完整变换/压缩后范围，受浮点可分辨性约束，允许配置超过百万倍。

主轴优先使用非 null 的图表 Viewport，否则使用可双向绑定的轴 ViewMinimum/ViewMaximum；附加轴有显式轴视图时独立，否则跟随主轴归一化范围。滚轮和平移会回写实际轴边界。外部在拖动时修改轴视图会结束过期拖动。`ViewportChanged` 通知图表请求变化，不等同于每次轴布局通知。Tooltip 默认启用，缩放/平移默认关闭，`InteractionAxes` 默认 Both；鼠标支持滚轮、左拖及双击重置，对数、反向和断轴采用同一映射。

`SelectedPoint` 为双向的最后选中点，`PointInvokedCommand` 的参数为命中的 `ChartPointHit`。默认 Single 模式下，左键按下更新并调用命令，绘图区空白清空选择。C2 新增 SelectedPoints、SingleDeselect/Multiple、矩形选择和键盘；数据变化仍按稳定身份迁移，移除/失效/隐藏清理选择。完整选择模板、系列选择、前置取消及范围选择器仍待后续实现，详见 C2 契约。

轴的事件现在是：

```csharp
event EventHandler<ChartAxisChangedEventArgs>? Changed;
// e.RangeChanged: 是否影响范围或投影；e.Property: 对应 AvaloniaProperty，可为空。
```

`Minimum`、`Maximum`、`IsInversed`、`Interval`、`Key`、`Labels`、日期 `IntervalUnit`、对数 `LogBase` 触发 `RangeChanged=true`，图表重置请求视口。标题、格式、标签旋转/换行/宽度和位置变更为呈现变化，重绘时保留请求视口；画刷变化同样保留。源变化会中止过期拖动并按新范围约束视图，不等同于总是重置视图。

## 从 C0 迁移

`ItemsSource` 从 `IEnumerable<ChartPoint>?` 改为非泛型 `IEnumerable?`。已声明为 `ChartPoint[]`、`List<ChartPoint>` 或 `ObservableCollection<ChartPoint>` 的变量可继续赋值；依赖旧 `StyledProperty<IEnumerable<ChartPoint>?>` 类型的代码需更新。C# 集合表达式不能从非泛型 `IEnumerable` 推断元素目标类型，原有内联赋值应显式给出数组类型：

```csharp
// 使用显式元素类型，不再写 ItemsSource = [new(0, 1), new(1, 3)]。
var series = new LineSeries
{
    ItemsSource = new ChartPoint[] { new(0, 1), new(1, 3) },
};
```

`ChartAxis.Changed` 从 `EventHandler?` 改为 `EventHandler<ChartAxisChangedEventArgs>?`。以 `EventArgs` 参数声明的兼容方法组仍可接收派生载荷；保存为旧 `EventHandler` 委托变量的代码应改为新泛型委托，才能直接订阅和退订：

```csharp
EventHandler<ChartAxisChangedEventArgs> handler = (_, e) =>
{
    if (e.RangeChanged) { /* 更新依赖轴范围的应用状态。 */ }
};
axis.Changed += handler;
// 释放应用订阅时：axis.Changed -= handler;
```

类型继承、事件签名、StyledProperty 的泛型类型及新增 API 都应以 C0 冻结快照和新 API 差异审阅；不承诺旧二进制消费者无需重新编译。当前类型名为 `NumericAxis`，默认缺失策略为 Gap；不要从官方相似名称推断 API、默认值或 ABI 可替换。

## 仍待开发与验收的边界

下面是本轮底座与 C1/C2 交叉项的未完成部分，仍按主计划和矩阵追踪：

| 工作范围 | 当前缺项或限制 | 后续阶段 |
| --- | --- | --- |
| 轴兼容 | 次刻度/网格、断轴/区间、Auto/多轴布局和逐轴 min/max zoom/独立视口已实现；AX12 的官方整图自动类别回退仍与本库显式轴/Gap 契约不同。 | C1 |
| 复杂标签与交互 | 模板化标签/任意覆盖内容、极密多轴产品布局、范围选择器及完整触摸手势仍待后续实现。 | C2 |
| 身份与增量 | 已有源索引和稳定身份；未实现抽样代表点/可见索引的完整映射、大数据增量缓存、背压和连续流调度。 | C1、C4 |
| 数值图族 | High/Low/Weight 仅映射；堆叠、100% 堆叠、零总量、区间、金融、统计等需对应渲染和数值契约，不能以接口预留当作完成。 | C3、C5 |
| 共用视觉与交互 | C2 基础已经接入；完整模板、竖向/线段读数、系列选择/范围选择器、触摸、专用形状标注、动画/JPEG和点级辅助语义仍开放。 | C2 |
| 扩展兼容 | 当前为自主 `IChartSeriesRenderer` / `IChartProjection` 契约，没有实现官方自定义系列 ABI 或模板体系兼容。 | C1、C2、C7 |
| 消费与平台 | Windows 及 Ubuntu WSLg 已有构建/原生合成/包消费报告；Ubuntu22.04/24.04物理桌面、字体/DPI设备矩阵、物理输入、native present和长稳仍须验收。 | C1、C4、C7 |

官方参考宿主当前受到构建期许可校验阻断，其 API 尚未经过成功编译和运行验证，详见 [参考宿主说明](../benchmarks/Charts.Reference/README.md)。本文件不据此声明官方行为一致、官方性能相当或 Linux 性能达标；本轮运行结果与剩余门禁由执行结果文档记录。
