# Charts C3-05：极坐标、雷达与径向变体

执行日期：2026-10-18。最终候选 `c3-polar-radar-source-v2` 完成独立极坐标与雷达投影及八种系列，Windows 与 Ubuntu 26.04 WSL2/WSLg 软件验证通过。完整 C3、官方运行时等价、性能和物理平台资格继续开放。

## 功能与 Demo

`PolarChart`、`RadarChart` 提供角度/半径线性数值轴、度/弧度、起始角、角度范围、方向、角域循环/反转、环形和多边形网格。`PolarLineSeries`、`PolarScatterSeries`、`PolarFilledSeries`、`RadarSeries`、`RadialLineSeries`、`PolarAreaSeries`、`NightingaleRoseSeries` 与 `RadialBarSeries` 支持绑定、原始项身份、缺失断线、闭合、选择、键盘、Tooltip 和共享 PNG/JPEG 导出。Demo → Charts →「极坐标与雷达」、编译绑定 Usage 和两类隔离 NuGet 消费者均可运行。

## Windows 与 Linux 验证

| 最终 v2 | Windows 原生合成窗口 | Ubuntu 26.04 WSL2/WSLg |
| --- | --- | --- |
| Charts 测试 | 1292/1292 | 1292/1292 |
| 原生 Demo | 954/954 | 954/954 |
| 编译绑定 Usage | 275/275 | 275/275 |
| 仅功能包消费者 | 396/396 | 396/396 |
| 功能与 Themes 消费者 | 396/396 | 396/396 |
| Release 警告/错误 | 0/0 | 0/0 |
| 基础/消费者/API 三阶段退出码 | 0/0/0 | 0/0/0 |

相比 C3-04 增加 78 项测试、64 项 Demo、25 项 Usage 和每类消费者 42 项检查。消费者各用全新 NuGet 缓存，无源码项目引用；14 个非产品依赖按既存 SHA 复用，产品按各平台本批源码重新构建打包，未改 NuGet audit 或项目/还原警告门禁。独立包载荷核对 14/14 通过，将两平台新 nupkg 的 Core/Themes DLL 与原生运行记录及隔离消费者实际程序集逐项关联。

冻结 1356 个文件；两平台各 653 个声明运行输入逐项同 SHA，运行前后声明源和程序集稳定。文档更新后不声称整个工作区仍等于冻结副本。独立来源/平台审阅 4076/4076；41 个历史文档/API pin（含 C3-04 三份）和 5 版历史 renderer 保持原 SHA。

完整本地 API 按 assembly+type 比较，含生成资源类型：125→140 类型；Charts 命名空间 123→138。新增 15 类型、既有类型增加 0 项声明，无旧类型、声明或成员删除。此为本地元数据比较，未认证二进制或官方 API 兼容。

独立 Python stdlib PNG CRC/deflate/滤波解码审阅 864/864，实际解码 96 张图。每平台两主题各 15 个固定业务场景：线段闭合/开口/缺失断线、散点、填充、雷达、径向线、弧度、逆时针、部分弧、面积平方根半径与玫瑰线性半径、径向柱分轨/内洞、环形与多边形网格。全部控制显式 700×520；探针在首张相应图像前封存，按业务数值及公开布局、角度、半径公式复算，不用生产 GetPoints/GetSectors、命中或 geometry 输出作为预期。6 DIP 方框、RGB 容差 24、alpha≥240、正向至少 10 像素（网格 6）、负向零像素的门槛保持不变。新增三项轴回归分别按原始业务刻度及闭合公式核对 Area/Rose 内半径变换后的刻度圈、RadialBar 进度值角轴；新增探针在 source-v1 人工发现问题后、首张修复图前封存，同样采用 6 DIP、RGB 容差 24、alpha≥240、正向至少 6、负向零像素。原 15 项逐值和门槛保持不变。每主题合计 18 项业务与轴回归；混合投影的 r/R、θ 参考文字及类别标签位置另由录制布局单测与实际整板审阅确认，不将像素线条计数视为文本语义证明。96/144/192 DPI 密度和固定像素尺寸、原生捕获与导出逐像素一致均通过。

两平台两主题的原始页和控制变更页均已实际查看。图像为已创建原生窗口的 RenderTargetBitmap；WSLg 合成窗口与程序化输入通过不代表物理桌面、实际 backend/GPU/compositor present 或真实读屏器资格。完整路径和 SHA 见[验证清单](charts-c3-polar-radar-validation.json)，公开声明见[API](charts-c3-polar-radar-api.txt)。

开发过程原始证据完整保留：初次几何定向测试 27/29，两项分别是 270° 手算预期和 Headless 空几何 Bounds 夹具错误，修正期望并改用同一生产几何写入接口的独立指令录制，未放宽像素门槛。native-v1 两个客户端因 AngularAxis 的无效 null 默认值在类型初始化时失败，无图像或 JSON，不能记为通过；修复默认值后用新进程重跑。native-v2 Demo 为 945/946：暗主题 circle-grid 实际正向 9、负向 0 像素，独立解码符合首图前封存的网格至少 6 门槛，生产端夹具误将所有场景统一收紧到 10，后续按初始预注册还原网格阈值，未更改预注册或旧门禁；独立 PNG 为 377/377、解码 42 图。Usage 为 269/272，135°/495° 为相同方向的非规范角预期问题，另外两个历史 Trackball 条件经原始窗口坐标诊断，计划 rawY=40 实际由整像素路由得到 39.94875736612862，严格位于第二轴最小值 40 外；生产域判断正确，不应外插。后续保留原 Hover40 并新增按实际路由 rawY 是否在第二轴域内决定读数数量的边界检查，原双 reading 数量、数学值、真实端点身份和浮动双卡断言完整保留，主输入夹具改为共同域内 45；未扩大 Cartesian 生产域或放宽容差。该批整板底行受实际窗口高度裁切，后续修正展示布局，15 个独立 700×520 业务场景与像素阈值保持不变。后续测试、原生运行与独立 PNG 审阅逐批保留，正式结果以冻结候选双平台全三阶段为准。

轴语义修复开发证据另批保留：新增轴展示录制测试首轮 13/18，五个失败来自 Avalonia 将实际输入几何包装成 PlatformGeometry 后，夹具仍按 EllipseGeometry/LineGeometry 等具体子类型筛选；已用安装 DLL 的实际 IL 与 SHA 留证，只修测试录制识别，不改生产投影或放宽门槛。后续 18/18 轴测试与完整 1292/1292 测试通过。native-v5 为 954/954 Demo、275/275 Usage、两进程 0、8 个 DLL SHA 稳定；独立 PNG 为 435/435、解码 48 图，Area/Rose/Bar 新轴正向像素 10/10/9，负向均 0。原 15 项业务与门槛保持不变，三项新增轴预期在修复首图前封存；正式结果仍以 source-v2 冻结后双平台完整重跑为准。

正式 source-v1 两平台既有门禁均通过：每平台 1274 项测试、946 项 Demo、273 项 Usage、两类隔离消费者各 393 项，基础/消费者/API 全为 0；独立 PNG 为 750/750、解码 84 图，完整本地 API 125→140 类型且无旧声明删除。但人工审阅发现真实轴语义缺口：Area/Rose/InnerRadius 的原值刻度圈未采用对应半径变换，RadialBar 类别与数值轴方向不符，异构组合缺少明确的归一参考说明。原 15 像素场景未覆盖该轴展示条件，因此该候选不作为最终交付；保留所有通过证据，新增轴回归在修复首图前独立封存，修复后 source-v2 重新冻结并完整重跑双平台。

附加 TrackballBindingDiagnostics.cards 是延迟枚举，可能在卸载后序列化，其 readings=null 不能作为断言当时卡片上下文证据；卡片宽度与 ChartTooltipContext.Readings.Count 的 bool 门禁在当时即时求值，路由坐标、rawY 与 PlotArea 是值快照。此附加诊断局限已留档，未为其改动正式候选源码或放宽生产域。

## 官方公开范围对照

本批按下列公开图族范围核对命名与场景；未运行许可官方程序集，也未验证完整 API、默认值或二进制兼容。官方 PolarArea/NightingaleRose/RadialBar 使用专用 chart API，本库以 PolarChart 下的系列组合实现，面积平方根半径与玫瑰线性半径为本库明确契约，不据名称推断官方算法。

- [Polar](https://docs.avaloniaui.net/controls/data-display/charts/radial/polar-chart)
- [Radar](https://docs.avaloniaui.net/controls/data-display/charts/radial/radar-chart)
- [Polar area](https://docs.avaloniaui.net/controls/data-display/charts/radial/polar-area-chart)
- [Nightingale rose](https://docs.avaloniaui.net/controls/data-display/charts/radial/nightingale-rose-chart)
- [Radial bar](https://docs.avaloniaui.net/controls/data-display/charts/radial/radial-bar-chart)
- [Radial line](https://docs.avaloniaui.net/controls/data-display/charts/radial/radial-line-chart)

官方 Radial line 页面复用 PolarChart/PolarLineSeries；本库额外提供类别 RadialLineSeries，不将其称为官方独立系列 API。旧功能矩阵的官方链接和目录身份保持原序。

## 实现契约

- PolarChart 是独立角度/半径投影，RadarChart 默认多边形网格并复用同一实现。八个公开系列为 PolarLine、PolarScatter、PolarFilled、Radar、RadialLine、PolarArea、NightingaleRose 与 RadialBar，复用 ChartSeries 的 ItemsSource、Category/Value/Id/EmptyPoint 数据映射、原始对象与生命周期。AnglePath 和 RadiusPath 分别是 CategoryPath 与 ValuePath 的别名。
- AngularAxis、RadialAxis 接受 ChartAxis 或 NumericAxis 的线性数值域；类别/日期/对数轴和 scale breaks 明确拒绝。角域默认 0..360° 或 0..2π 弧度，径向默认 Minimum=0；自动共享 Maximum 收集有限非负源值与 RadarSeries 显式 MaxValue。RadarSeries.MaxValue 默认 100，设 null 时使用图表共享径向域；非 null 时覆盖该系列的有效径向最大值。同构兼容投影的原值网格使用同一有效域；不同有效域或映射混合时改为明确的显示位置参考，不把共享原值刻度套到异构数据上。图表径向 Minimum 必须非负，显式 Maximum 必须大于 Minimum。
- 先按角域归一化，再以 StartAngle、SweepAngle、Clockwise 映射显示角。StartAngle 是显示度数，默认 -90；SweepAngle 为 0<值≤360。AngleUnit 只改变数值角域及原始角度解释，不改 StartAngle 的显示度单位。WrapAngles 只循环域外值，恰好在 Maximum 的值仍位于 Sweep 终点；两个轴可分别反转。 几何先将 StartAngle 归一到 0..360 再加显示 sweep；返回的 PolarPointInfo.Angle 不承诺规范区间（例如 495° 与 135° 表示同一方向），Position 使用等价方向。
- Radar、RadialLine、PolarArea、NightingaleRose、RadialBar 共用本图各类别系列 first-seen Equals 类别并集，每个类别位置为 i/N；原始项不排序或重写。数值角系列按原始源序连接，不隐式按角度排序。缺失类别、负/非有限/空半径、超出径向有效域的点，以及 WrapAngles=false 时超出角域的点形成断线；零半径点仍可位于内基线，未设置 InnerRadius 时为中心。共享 EmptyPointMode 先填充的值保留 WasEmpty 元数据。
- PolarLine 支持开口或闭合轮廓；PolarScatter 只绘制圆形标记；PolarFilled 与 Radar 填充完整闭合顶点多边形，有断点或非闭合时不跨空白补填。InnerRadius 是点半径映射的内界，填充多边形不自动形成内洞。RadialLine 使用类别位置连接，图表网格形状独立于系列可选。
- PolarArea 与 NightingaleRose 为等角类别扇区，SectorGap 从两侧对称收缩绘制角度。t 是按半径域归一化的贡献，inner=R*InnerRadius：PolarArea outer=sqrt(inner²+(R²-inner²)*t)，使环带面积成比例；Rose outer=inner+(R-inner)*t，使半径长度成比例。零、负、非有限或空贡献不绘制扇区；反转径向域不把原始零值变成有面积贡献。
- RadialBar 按类别分配同心轨道，laneWidth=(R-inner)/N；第 i 轨内外界为 inner+(i+.04)*laneWidth 与 inner+(i+.96)*laneWidth，保留轨道间隙。所有轨从同一起点按 t 占用 SweepAngle*t 的角度，SectorGap 同样仅影响绘制，不改原始业务值。
- 数据裁剪到外圆；命中依当前投影检查扇区角度/内外半径、圆形标记或线段，重叠按后绘制系列优先。扇区系列只枚举可见扇区进行键盘/选择，不重复枚举点，也不命中不可见零扇区、轨道洞或间隙。线段命中返回最近真实端点身份，不制造插值业务项。
- GetPoints 与 GetSectors 分别返回点图族或扇区图族快照。Data、Point、SourceIndex、StableId、Item 与 Category 保留原始业务数据；Position、Angle、Radius、Center、Inner/OuterRadius 和扇区角度为显示投影。ColorPath 支持直接 Color、IBrush 或可解析颜色字符串，缺省回退系列和调色板。FillOpacity 应用于 polygon/sector fill；公共默认值为 1，PolarFilled 为 .25，Radar 为 .3；轮廓与选择强调独立绘制，不随填充透明度衰减。
- 轴展示按实际投影选择：同构兼容点/Rose 的径向刻度使用 inner+(1-inner)*t，Area 使用 sqrt(inner²+(1-inner²)*t)；同心 RadialBar 类别标签放在相应 lane 的起始位置，业务值刻度按进度角分布。混合不同映射、InnerRadius、有效最大值或类别语义时显示 r/R=百分比、物理 θ=度数，并外显“归一半径参考”，不伪装成原值轴或类别轴。Area/Rose 的值网格、Bar 的 lane 网格及归一 r/R 网格始终为真圆或部分圆弧；GridShape.Polygon 仅用于点图族的指定角位参考顶点，不能作为任意角度的连续等值线。GridLevels 或显式 Interval 提供基础刻度（显式间隔最多 100 个中间刻度）；轴标签单行截断并钳制到图表边界，不提供全局碰撞消解；数据标签超出或相撞时省略。ShowAxisLabels=false、ShowLegend=false、无标题与 Padding=0 时 PlotArea=(8,8,w-16,h-16)，中心为图表中点，外半径 min(w-16,h-16)/2。
- None/Single/SingleDeselect/Multiple 选择支持双向属性、前置可取消事件、源更新按稳定身份跟随、失效项清理与重入保护。键盘支持左右、Home/End、Space/Enter 与 Escape；Tooltip 和标签使用自己的 formatter。
- PNG/JPEG、同步/异步、尺寸、背景、DPI、编码预算、取消和失败事件使用共享导出管线。背景参数是 canvas backdrop，图表自身 Background 仍绘制；显式 PixelSize 保持相同像素外观，DPI 写入密度元数据。导出为当前静态投影，正式原生窗口像素、96/144/192 DPI 和异步编码由本批双平台检查。

## 剩余问题

- 本批采用本库公共 API 和自有图族组合；官方许可参考运行时、完整官方属性/样式/模板/行为和二进制兼容尚未认证，不以相同图名宣布官方等价。
- 本批未加入 Polar/Radar 入场、数据或视图变化动画 timeline；C2 的完整动画、Trackball、缩放平移、范围选择、多图同步、标注、交互式图例与模板契约需逐图族适配。旧 Cartesian/Circular 已有功能不自动构成新投影的完整 C2 等价。
- 极坐标轴目前为非负径向线性域与数值角域；对数/日期/类别轴对象、scale breaks、逐轴复杂样式/标签布局、所有 ChartAxis 选项的完整适配仍开放。类别径向系列使用自身共享类别并集，不能替代 CategoryAxis 的完整语义。GridShape 是点图族的参考网格偏好；Area/Rose、Bar 和异构参考轴为保证真实半径语义强制使用圆弧，不宣称完整 C1 任意网格/投影组合等价。
- 圆形标记以外的逐点形状、边框/模板、填充纹理、平滑或阶梯极坐标线、range/stack/percent 极坐标扩展、逐类独立径向域、复杂重叠及高密度标签路由仍开放。Filled/Radar 的多边形填充不会自动切出 InnerRadius 内洞，数据缺失时不补闭合面。
- 绘制式单行图例可能省略超宽内容，轴标签仅单行截断与边界钳制，数据标签采用基础边界/碰撞策略；可交互/滚动/虚拟化图例、完整格式模板、复杂轴标题/刻度布局、轴标签全局碰撞消解和高密度可读性尚未获完整资格。
- 极坐标点级辅助语义与原生 automation、真实读屏器、系统高对比偏好以及跨图族无障碍完整适配仍在后续专项。Windows UIA/Linux AT-SPI 原生选中集合枚举、Linux 嵌套点选择写入、点系统焦点与真实读屏器按用户决定留 A11Y-NATIVE-01；原失败门禁未改为通过，本批未重验。
- 新投影的类别并集/身份查找、布局、绘制、命中、标签与多图的规模、增量、分配、内存、吞吐及尾延迟尚未建立 C4 性能资格，本批未实测百万点性能或内存回归。百万点命中 p95 历史回退 0.7649→1.1235 ms 按用户决定留 C4。
- C3-06 仪表和微图为下一项；C3-06～C3-07、C5/C6 其余图族和完整 C2/C3/C4/C7 仍开放。此前散点大小图例/连续色标、C6 无轴云/紧凑打包、圆形高级标签/模板与中心内容复杂视觉导出边界继续保留。
- 物理 Windows/Linux、实际 backend/GPU/compositor present、多屏 DPI、Ubuntu 22.04/24.04、原生 Wayland、ARM、目标国产 Linux及真实设备输入/保存对话框/读屏器工作流仍待独立验证。
- AX06 标题/末刻度布局及四个历史 PixelProbe DLL 证据缺口保持开放，未用本批新证据替换。
- ColorPath 与系列画刷实例由调用方持有；原地修改同一可变 IBrush 不保证触发源数据版本更新，应用可替换画刷或显式更新数据。本批不提供颜色过渡历史或极坐标动画 timeline。
