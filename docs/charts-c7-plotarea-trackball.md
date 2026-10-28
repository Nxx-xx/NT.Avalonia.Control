# Charts C7-01：专业图族 PlotAreaContent 与 Trackball

任务启动：2026-10-23；验证记录更新：2026-10-24（Asia/Hong_Kong）。本轮为 EngineeringChartBase / AnalysisChartBase 及其专业图族接入绘图区业务内容和 Trackball，延续[共享图例与数据标签](charts-c7-shared-presentations.md)的原始身份、模板所有权、主题和导出约定。同一冻结候选的 Windows 与 Linux 正式六阶段软件验证均已通过，最终独立收集完成。本批功能的软件验证已完成，C7-01 及 C7 整阶段仍在推进，正式实机、视觉边界和性能对照继续单独验收。

## PlotAreaContent API 与坐标语义

两个专业根的 `PlotAreaContentProperty`、`PlotAreaContentTemplateProperty` 通过 AddOwner 复用 Cartesian 对应 styled property 身份。专业模板使用独立的 `ChartDataPlotAreaContext`；Cartesian 原有 `ChartPlotAreaContext` 的实际轴、命名轴、对数轴和断轴投影契约保持原样。

| 属性 / 上下文 | 约定 |
| --- | --- |
| `PlotAreaContent` | `object?`。直接传 Control 且没有模板时，控件正常继承图表业务 DataContext；其他对象需要 `PlotAreaContentTemplate`。null 移除永久绘图区内容。 |
| `PlotAreaContentTemplate` | `IDataTemplate?`，Build 获得 `ChartDataPlotAreaContext`；返回一个尚无父级的 Control。null Build 隐藏当前读取，新实际绘图帧可再次判断。 |
| `Content` / `DataContext` | 分别保留传入的业务内容和图表当前业务 VM；模板子树自己的 DataContext 为上述上下文。 |
| `PlotArea` | 实际图表局部 DIP 绘图区，已经包含图族边距、标题及共享图例预留区域。模板控件以绘图区左上角为局部原点。 |
| `ProjectLayout(xFraction, yFraction)` | 将图族未变换布局的归一化位置映射到模板局部 DIP。左上 `(0,0)`、右下 `(1,1)`，Y 向下。Engineering 应用实际 Zoom / PanX / PanY；Analysis 使用当前布局的恒等映射。 |
| `UnprojectLayout(Point)` / `VisibleLayoutBounds` | 反算布局比例及当前可见比例范围。工程图缩放后可见比例范围缩小，平移后可超出原来的 0～1 范围。 |
| `ToLocal(Point)` / `ToChart(Point)` | 在已投影标记的图表局部 DIP 与模板局部 DIP 之间平移，不再次应用视口变换。 |
| `EngineeringMarks` / `AnalysisMarks` | 分别引用当前真实不可变绘图帧，另一个集合为空；保持原标记、完整 Members 或原始 Data，没有按悬停复制整个数据源。 |

布局比例表达家族当前的视觉布局，不将漏斗阶段、树节点、地图经纬度或排程时间伪装成共同数值轴。业务内容需要对齐真实图元时，读取对应标记的 Position / LabelPosition，再通过 ToLocal 转换；需要真实专业数据投影时继续使用该图族自身已有的投影 API。输入必须有限，无法表示的坐标溢出会明确抛出异常。

内容通过真实逻辑树和视觉树获得资源、动态资源、字体、Foreground 和业务绑定。承载层为被动控件，绘图区 Border 负责实际矩形裁剪，不拦截指针或 Tab。稳定帧复用同一上下文和子控件；源、VM、视口或布局变化后更新快照并复用已构建控件。业务内容自身的 INPC 绑定更新不需要重新枚举来源。

## Trackball API、读取与原始身份

专业根通过 AddOwner 复用 Cartesian 的 `TrackballMode`、`TrackballDisplayMode`、`TrackballContentTemplate`、线条和标记外观属性。专业 `TrackballFormatter` 接受 `IReadOnlyList<ChartDataTrackballReading>`，两个专业根共享这一属性身份；它与 Cartesian 接受 `ChartTrackballReading` 的格式器类型分别保留各自契约。

| 属性 / 方法 | 约定 |
| --- | --- |
| `TrackballMode` | None、Vertical、Horizontal；默认 None。垂直 / 水平读取当前可见专业图元的显示坐标切片。 |
| `TrackballDisplayMode` | 默认 GroupAllPoints，一张卡片包含当前保留读数；FloatAllPoints 每张卡片包含一个读数。 |
| `TrackballContentTemplate` | 可选 `IDataTemplate?`，使用 `ChartDataTrackballContext`。null 使用默认文本卡片；Build 返回 null 可隐藏对应卡片。 |
| `TrackballFormatter` | 可选 `Func<IReadOnlyList<ChartDataTrackballReading>, string?>`。默认组合图族提供的读数文本；返回 null 且未提供自定义模板时不生成文本卡片。 |
| `MaxTrackballReadings` | 默认 32，合法范围 0～1024；按稳定图族绘制顺序截取最接近切片的可见图元。0 保留总数语义但不生成读数或卡片。 |
| `TrackballReadings` / `TrackballTotalReadings` | 只读通知属性，分别是预算内读数和截取前完整切片图元数。总数统计图元，不把聚合 Members 个数当作独立读数。 |
| `UpdateTrackball(Point)` / `ClearTrackball()` | UI 线程上的图表局部 DIP 更新与清除接口。无效位置、绘图区外、None、隐藏或禁用时清空读数。 |
| `ChartDataTrackballReading` | Members、Text、Position、Bounds、Brush、Value、BusinessDataContext，以及互斥的 EngineeringHit / PointHit。 |
| `ChartDataTrackballContext` | Readings、Anchor、Text、BusinessDataContext、TotalReadings、IsTruncated；聚合卡片含全部预算内读数，浮动卡片含一个。 |

切片按当前可见图元边界到指针 X 或 Y 的距离寻找最近距离，保留同距离全部图元，再按预算截取；裁剪外、不可见或不可绘制图元不会参与。Mosaic 等聚合图元直接保留其完整真实 Members，包括 Item、StableId、SourceIndex；Analysis 保留原始点身份。没有制造 Cartesian 系列、聚合业务观测或图元间插值，也不把专业切片接入 Cartesian 的 LinearInterpolation 模式。

每次需要新切片时搜索复杂度为 O(当前标记数)，候选索引只保留预算内的图元，同时用独立计数器统计全部并列候选；临时索引、最终读数和根卡片数量均受 MaxTrackballReadings 约束。该预算不限制扫描工作或每个聚合 Members 数量。稳定来源与视口使用已有绘图帧，不因移动指针重新枚举业务源。浮动卡片在图表边缘约束内尝试局部避让；密集卡片没有足够空间时仍可能重叠，当前没有滚动容器或全局最优排版保证，业务应使用分组模式或较小预算。

指针移动、直接点击和既有键盘点浏览可更新读数；退出、Escape / 视口重置及中键开始工程平移会清理临时读数。图形命中和选择继续使用原有数据身份。启用专业 Trackball 时，旧 Tooltip 呈现让位于 Trackball；切回 None 恢复旧 Tooltip 契约。

## 层级、生命周期与错误处理

| 呈现层 | ZIndex / 行为 |
| --- | --- |
| 原图族绘制 | 保留原背景、轴、图元、标注和装饰绘制。 |
| PlotAreaContent | 10，实际绘图区裁剪，永久业务内容。 |
| 数据标签 / 共享图例 | 20 / 25，保留上一批模板布局和图族开关。 |
| 旧 Tooltip 前景层 | 30，未启用 Trackball 时保留可见次序，避免被永久模板覆盖。 |
| Trackball 线与标记 / 卡片 | 35 / 40，绘图区线条裁剪与图表边界内的被动卡片。 |

源或业务 DataContext 改变时先清除旧模板读取，再进入业务集合 / 绑定回调；换模板、隐藏、脱离视觉树会释放拥有的子树和旧上下文。恢复显示应能从当前仍有效的读取恢复；禁用保留永久绘图区、清理临时 Trackball。所有模板结果都必须无现有父级，拒绝结果时保留原父级与原上下文。

Build、Measure、Arrange 异常在清理后传播，失败模板隔离到替换后恢复。格式器失败只隔离实际抛错且仍是当前值的委托；模板失败不连带隔离正常格式器，格式器回调替换自身后抛错也不污染新委托。源投影失败遵循原图族数据恢复流程，不将其当成永久模板故障。

读数发布、模板构建、逻辑树挂接和布局包含版本与重入检查：回调中换源、换 VM、换模板、清除或脱离时，不提交过期子树。布局和绘制中的嵌套导出被拒绝。根控件也参照已有 Cartesian 行为过滤 IsPointerOver、IsFocused、IsKeyboardFocusWithin 纯输入状态的通用几何失效；样式实际改变字体、颜色或布局属性时，仍由对应属性通知处理，不能省略真实视图更新。

## 导出、Demo 与编译绑定示例

PlotAreaContent 为永久内容，进入同步 / 异步 PNG、JPEG；Trackball 卡片、线和标记与 Tooltip 一样从导出中排除，捕获不清空屏幕上的有效实时读数。模板与布局在捕获前的 UI 阶段准备，绘图阶段只使用已准备的视觉树；脱离窗口导出沿用同一准备帧，完成后释放临时拥有的呈现。沿用已有尺寸、DPI、背景、取消、失败通知和文件替换路径。

Demo 新增“专业图族 · 绘图区内容与 Trackball”，四张实际图表为 Funnel、Mosaic、Strip、SeatMap，覆盖两个专业根、聚合 Members 和地图区域身份。绘图区模板展示业务标题、永久内容与可见布局比例，十字使用 ProjectLayout 的归一化中心；Trackball 模板展示预算内 / 总读数、首读数原始成员数与原始 ID。提供永久模板、默认 / 业务读数模板、垂直 / 水平、分组 / 浮动、最多 2 项、普通 / 高对比、数值 / VM 更新、换源、工程缩放平移、视图重置和隐藏图组操作。窄布局单列，宽布局两列。

Usage 的同名 AXAML 折叠区提供 Funnel / Mosaic 两根的真实编译模板绑定；内容、模式、显示方式、预算、可观察源、业务标题、命令和双向选择都来自 VM。测试检查 Mosaic `[0,2]` 聚合成员、真实来源、换源后的观察者释放与高对比切换。Usage 仅验证绑定和控件行为；真实导出像素由 Demo 专项负责，不能将 Usage 的对象断言记成像素验证。

Demo 专项设计包含四种主题截图，以及两个根的默认、永久内容加活动 Trackball、永久内容清除 Trackball 三种状态，每种分别导出同步 / 异步 PNG、JPEG。比较同编码同步 / 异步像素、永久内容与默认像素差异，以及活动 / 清除 Trackball 的相同导出像素。另对实际永久模板 TextBlock 做 Opacity 隐藏 / 恢复，在布局不变的字形 ROI 内检查变化。Windows 与 Linux 各自冻结候选的正式执行均输出 7 个探针和 32 张图片，见下表。

## 验证结果：冻结候选双平台软件验证通过

| 验证项目 | Windows（run-v2） | Linux（run-v2） |
| --- | --- | --- |
| 正式阶段收据 | 6 / 6 通过；failedChecks 为空 | 6 / 6 通过；failedChecks 为空 |
| 最终源码冻结清单、前后哈希 | v2 全部 1976 文件运行前后完全一致 | v2 全部 1976 文件运行前后完全一致 |
| 全量 Headless 回归 | 3050 / 3050；0 失败、错误、跳过与未运行 | 3050 / 3050；0 失败、错误、跳过与未运行 |
| 上批用例保留 / 新回归 | 2986 项全部保留；新增 64 项，PlotAreaContent 29 / Trackball 35 | 2986 项全部保留；新增 64 项，PlotAreaContent 29 / Trackball 35 |
| 完整原生 Demo / Usage | 5067 / 5067；913 / 913，含 28 项新增编译绑定检查 | 5067 / 5067；913 / 913，含 28 项新增编译绑定检查 |
| 新缓存功能包 / 主题包消费者 | 1095 / 1095；1129 / 1129 | 1095 / 1095；1129 / 1129 |
| 绘图区与 Trackball 原生专项 | 243 / 243；7 个探针 | 243 / 243；7 个探针 |
| 四主题截图、永久内容 / 瞬态排除与字形证据 | 32 图：4 主题 + 24 导出 + 4 字形隐藏 / 恢复图 | 32 图：4 主题 + 24 导出 + 4 字形隐藏 / 恢复图 |
| 原生导出稳定性 | 272 次导出尝试；3200 项生产者保护；3232 项独立检查通过 | 272 次导出尝试；3200 项生产者保护；3232 项独立检查通过 |
| API 表面 | Core 503 + Themes 3 = 506 个导出类型 | Core 503 + Themes 3 = 506 个导出类型；归一化表面与 Windows 一致 |

当前候选为 `source-snapshot-v2.json`，2026-10-23 15:52:33 UTC 冻结，包含 1976 个非忽略仓库输入文件；清单 SHA-256 为 `6C20FDD91DA525E9FD097DCBFF2289923108AC4A8244C78F7ADE65D272B5839B`。Windows / Linux 分别使用 `artifacts/c7plottrack/windows-source-v2` 与 `artifacts/c7plottrack/linux-source-v2`，输出归档为对应平台的 `run-v2`。正式执行已核对两份全部输入的运行前后哈希。双平台独立收集已核对固定 SDK / 运行时、实际加载程序集、旧用例保留与新回归、原生结果及原始图片；当前工作区 615 个 Charts 构建、测试、Usage、Demo 和验证入口相关输入与冻结候选一致，文档及无关任务差异另外记录。最终收集的 batchPassed 与 fullNativeRegressionPassed 均为 true，pendingPlatforms、aggregationErrors 及两个平台的 failedChecks 均为空。Windows Headless 与合成窗口、Linux WSLg / XWayland 软件结果均不替代物理触控板、真实屏幕 present、GPU / 驱动、目标发行版实机或读屏验收。

原始证据根目录：`C:/Users/a4472/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c7-plotarea-trackball-20261023`。`collector-windows-complete-v1.json` 保留 Windows 先完成而 Linux 尚在执行时的中间结果；最终双平台收集为 `validation-final.json`，归档见[验证 JSON](charts-c7-plotarea-trackball-validation.json)，181998 字节，SHA-256 为 `484F19D1D9075309236DB22DDC2F5F06AFFE2FCCCBC7D737C9F4549A3C91FC40`。Windows 原始公开表面已经逐字节保存为[API 快照](charts-c7-plotarea-trackball-api.txt)。

Windows 环境为 Microsoft Windows 10.0.26200 x64，可见原生窗口加合成输入；Linux 为 Ubuntu 26.04 LTS x64、WSL2 内核 6.6.114.1，使用 WSLg / XWayland 原生 X11 窗口和合成输入。两平台实际 SDK 均为 10.0.402、运行时 .NET 10.0.13、Avalonia 12.0.3.0、PowerShell 7.6.5。每个平台的两个隔离包消费者均从独立新 NuGet 缓存恢复，无 ProjectReference，实际 SDK、运行时及 Avalonia 版本与主体一致；包内容、实际加载 DLL 和执行收据的哈希链已核对。旧共享呈现 38、图族适配 20、专业 Tooltip 59 项在两个平台仍全部通过，没有移除上批 2986 项测试中的任何用例。Ubuntu 26.04 WSLg 的软件验证不表示目标 Ubuntu 22.04 / 24.04 实机资格。

双平台专项截图及导出分别位于证据根下 `windows/run-v2/professional-plot-trackball/` 和 `linux/run-v2/professional-plot-trackball/`，主题为 normal-light、normal-dark、hc-light、hc-dark。PNG 独立校验结构、CRC、尺寸、生产者像素哈希链接、同编码同步 / 异步相等、永久内容变化、活动 / 清空 Trackball 相等及真实 TextBlock ROI 隐藏 / 恢复；JPEG 独立验证仅覆盖 SOI / SOF / 尺寸 / EOI，解码后的像素对比来自生产者 Avalonia，未宣称独立 JPEG 视觉质量或跨编码相等。此前开发图像检查的 4 种负例继续保留，不用成功结果覆盖。导出稳定性使用默认 JIT 和强制 GC，工作进程已停止，执行前后来源及主机输入保持一致；这些检查仍不等于长期实机性能验收。

双平台元数据 API 表面一致，归一化 SHA-256 均为 `5CF2AE9D8AD33EE1697F8A89A166033497BABAAF430BA85185D20AA2134D11E9`。此处是自有程序集公开类型与成员的元数据记录，已核对自有 Windows / Linux 表面一致，仍未与官方授权 runtime 作 API 或性能等价验证。

最终图像复核使用查看工具核对 3 张指定图片：Windows normal-light、Linux normal-light 与 Linux hc-dark，清单及图片哈希保存在 `final-v2-visual-review-v1.json`。在这些图片中，四图、永久内容、读数原始成员卡片、中文与控制区均可见，未观察到缺字或卡片 / 页面互相遮挡。复核同时发现剩余视觉边界：使用 Foreground 的 Trackball 线与标记描边，覆盖白色或同色图元填充时局部对比可能不足，例如高对比深色主题中的白色漏斗带。按实际底色自动选色或双重描边继续列入 C7 视觉验收，本轮不宣称所有线条 / 图元组合逐像素达到 7:1；也没有把这 3 张图像复核扩展为全部图片、物理屏幕或所有用户模板认证。

## 开发过程中的发现与修正

开发日志与 XML 保留首次失败，不用后续成功覆盖：

| 开发批次 | 原始结果 | 定位与状态 |
| --- | --- | --- |
| Windows core-development-v1 | 3 个编译错误，未进入测试 | Point 没有 SquaredLength，以及两个根没有对应的 ActualThemeVariantProperty；改为实际坐标平方距离与正确主题变更通知。 |
| Windows core-development-v2 | 构建 0 警告；167 项中 158 通过、9 失败 | 包含坐标往返浮点精度、初始空 Build 帧、首次指针进入的上下文身份及 Trackball 相关失败，原 XML 保留。 |
| Windows core-development-v3 | 构建 0 警告；171 项中 169 通过、2 失败 | 原始 XML 中 PlotAreaContent 29 / 29、上一批 117 / 117 通过，Trackball 25 项中 23 通过、2 失败；两根祖先恢复问题保留到 v4 继续诊断。 |
| Windows core-development-v4 | 构建 0 警告；Trackball 25 项中 23 通过、2 失败 | 只运行 Trackball，并增加祖先隐藏 / 恢复各时点的真实状态日志；原失败未覆盖。 |
| Windows core-development-v5 | 构建 0 警告、0 错误；Trackball 29 / 29 通过 | 保留祖先恢复严格断言，并验证空格式器下仍有线层、真实 Home / 方向键 / Escape、无先行 MouseMove 的直接点击与原始选择身份。 |
| Windows core-development-v6 | 构建 0 警告；Trackball 33 项中 29 通过、4 失败 | 使用实际一次性 PropertyChanged 监听器复现清空读数时同步重新读取、或切换模式 / 来源 / VM 后重新读取的重入；两根均出现已恢复读数但公开总数被外层清成 0。 |
| Windows core-development-v7 | 构建 0 警告；179 / 179 通过 | PlotAreaContent 29、Trackball 33、上一批 117 均通过；此时仅保护公开读数通知，尚未覆盖子树清理回调，不能当作最终完整修正。 |
| Windows core-development-v8 | 构建 0 警告；Trackball 35 项中 33 通过、2 失败 | 增加真实模板子控件 DataContextChanged 一次性监听器；两根重现清理后公开读数 / 总数为 0，但仍有 1 张卡片和有效缓存帧。 |
| Windows core-development-v9 | 构建 0 警告、0 错误；181 / 181 通过 | 将保护扩展到整个清理事务后，PlotAreaContent 29、Trackball 35、上一批 117 均通过；6 项新重入用例全部保留，源码与 XML 哈希单独归档。 |
| Windows demo-development-v1 | Demo 构建 3 个编译错误，未进入运行；静态检查发现 Usage 2 处同类写法 | 新样例把 Point 差值当成具有 Length 的向量；改为明确的 X / Y 距离计算，错误日志保留，未记为运行成功。 |
| Windows demo-development-v2 | 专项 Demo 243 / 243；编译绑定 Usage 913 / 913 | Usage 新增 28 项绑定检查；Demo 输出 7 个探针、32 张原始图片。外部图像收集器独立完成 152 项图像检查，并拒绝 4 种负例，包括重算正确哈希后仍含 1 个像素瞬态泄漏的有效图片。 |

浮点往返的 `123.2` 与 `123.19999999999999` 是正常数值舍入，测试改为逐分量 10 位小数比较。初次窗口 Measure / Arrange 可能产生连续实际绘图帧；条件空模板测试记录每次 Build 的真实不可变标记帧身份，确认没有在相同帧重复构建，再严格要求稳定悬停不增加构建、一次源变更仅增加一次构建。两项属于测试契约校正，没有通过减少稳定帧、来源身份或复用断言来掩盖产品问题。

首次指针进入引发根控件的通用属性处理重建上下文，属于产品失效粒度问题，修正上述纯输入属性过滤后保留严格的上下文引用相等与源枚举不变断言。另补齐格式器 / 模板错误隔离边界和直接点击更新。

祖先恢复的实际 v4 日志显示：隐藏时两根均立即从 2 条读数 / 1 张卡片清成 0 / 0；显示后源版本、不可变帧和 2 条读数已经恢复，但卡片保持 0。原因是祖先 / 卡片可见性通知顺序使单独卡片随后再次失效，而根的同源帧快速路径直接返回，没有同步呈现器。修正后，该快速路径只重新同步现有呈现器，正常卡片自行快速返回，不重新生成读数或枚举业务源。v5 同一严格用例显示 Loaded 工作完成后卡片恢复为 1，后续相同指针更新仍为 1，两根均通过；它已不是当前未解决问题。

另为过期 Build 返回的无父级子控件清除显式旧读取上下文，保留有外部父级的控件及其上下文；格式器抛错只隔离实际当前委托，正常格式器不因模板错误而失效。默认格式器结果为 null 时隐藏卡片，线与标记仍按真实读数呈现，自定义模板继续收到原始读数。

公开读数清除通知并不是唯一可重入位置：从图表移除旧模板子树时，子控件的 DataContextChanged 回调也能同步请求新的指针、模式、业务来源或 VM。仅在发布空读数时设置保护，仍会让旧子树清理与新读数发布交错。v8 保留了这一情况的真实失败证据。最终修正从进入 InvalidateProfessionalTrackball 起保护整个事务，包括旧子树和上下文清理；嵌套清理只作版本失效，最外层负责发布空读数与空总数。回调明确提出的新读取请求延迟到整个清理完成后统一处理，并重新检查最新来源、模式与业务 VM，避免外层清理覆盖新读数，或旧卡片在公开空状态下重新出现。导出忙碌保护也覆盖该清理事务。

当前冻结的新增回归为 29 个 PlotAreaContent 与 35 个 Trackball，用例共 64 个。v9 是同一次开发执行：64 个新增回归加上一批 117 个模板 / Tooltip 用例，共 181 / 181 通过；它是开发定向证据。两平台后续正式全量各 3050 / 3050、旧用例保留及程序集身份均已由上节收据和最终独立收集确认。

## 候选清单修订与正式首次启动

v1 清单包含 1973 文件，SHA-256 为 `F110A58C863ABCC413184FA50D9F461775A914D4BB8CB2AA52A5A693A7E02167`。Windows `run-v1` 在 `restore-0` 停止：受限环境中的 NuGet 审计网络访问产生 NU1900，另有 dotnet info 访问 SCM 被拒绝；尚未进入运行期测试，没有可用的正式运行 XML。该环境失败完整保留，不记作产品回归通过，也没有关闭 NuGet 审计。

v2 保留上述父清单 SHA 和逐文件 before / after 修订。Charts 生产改动仅为 `EngineeringChartBase.Trackball.cs`、`AnalysisChartBase.Trackball.cs` 的完整清理事务保护，另新增 `ProfessionalTrackballTests.cs` 中的 6 项回归；文档与并行 TreeDataGrid 工作的输入修订另行记录，不计为本批 Charts 功能。新增文档 / 脚本使整个冻结仓库输入从 1973 增为 1976。Windows `run-v2` 使用获准的完整执行环境并保留正常 NuGet 审计；Linux 使用同一 v2 候选，两个平台六阶段正式收据全部通过。最终独立收集确认本批软件验证通过，首次启动的环境失败及修订链继续保留。

## 剩余工作与验收边界

- 祖先恢复与清理事务重入回归均已修复，同一 v2 候选双平台正式六阶段和最终独立收集已通过；C7-01 及 C7 整阶段仍在进行，不能用本批软件结果关闭后续范围。
- C7-01：专业图族范围选择、多图联动和完整动画 / 导出交叉组合继续逐图族接入。本轮绘图区布局比例与真实图元切片不能代替各图族专有数据投影或有意义的插值算法。
- Trackball 线 / 标记在同色或亮色图元上仍需按实际填充自动对比或双重描边，尤其高对比深色主题的白色图元；本轮未取得所有组合的逐像素 7:1 描边保证。
- 浮动 Trackball 密集排版、超预算聚合成员模板自行展开、切片 O(标记数) 扫描的更大场景预算仍需按实际工作负载评估；本轮不宣称百万点 Trackball 延迟 SLA。
- C7-02～06：目标 Windows / Linux 实机与物理输入、官方授权 runtime 同机同质量性能对照、8 小时和 100 轮生命周期长稳、自包含 / 裁剪 / AOT / 升级消费、最终 API 和发布材料仍待验收。开发窗口、离屏导出和 WSLg 结果不能折算为上述完成。
- 百万点命中尾延迟与 Windows UIA / Linux AT-SPI 原生选中项枚举桥接继续按用户决定后置。
