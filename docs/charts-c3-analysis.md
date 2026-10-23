# Charts C3-07：漏斗、金字塔、Heatmap、Pictorial bar 与 Theme river 堆叠底座

执行日期：2026-10-18。最终候选 `c3-analysis-source-v1` 已实现基础分析图软件范围及 Demo。Windows 与 Ubuntu 26.04 WSL2/WSLg 验证通过。下一主项为 C4-01 数据与视图分层失效；本批不启动 C4，也不据此关闭完整 C2/C3/C4/C7、官方等价或物理平台门槛。

## 功能与 Demo

`FunnelChart` 提供按源顺序的漏斗、颈宽及分段间隔；`PyramidChart` 提供按正数面积比例分层的金字塔。`HeatmapChart` 提供离散行列、颜色域与实际色标、缺失单元和最后源项胜出的重复单元规则。新图族共享真实源项命中、Tooltip、键盘浏览、基础选择与 PNG/JPEG 同步/异步导出。`PictorialBarSeries` 接入现有 Cartesian 管线，提供符号拉伸、裁剪和重复模式、方向、间隔、真实轮廓命中及现有轴/选择/导出。`ThemeRiverStack.CreateBaseline` 为相同有序槽位、有限非负贡献创建居中透明底座，组合既有堆叠面积系列；底座采用居中 silhouette 规则；wiggle、优化流顺序等未实现，也不等于完整官方 Theme river 控件。

Demo → Charts →「基础分析图」展示这批图族与可交互场景；编译绑定 Usage 和仅功能包/功能与 Themes 两类隔离 NuGet 消费者均已接入。

## Windows 与 Linux 验证

| 最终 v1 | Windows 原生合成窗口 | Ubuntu 26.04 WSL2/WSLg |
| --- | --- | --- |
| Charts 测试 | 1500/1500 | 1500/1500 |
| 原生 Demo | 1188/1188 | 1188/1188 |
| 编译绑定 Usage | 318/318 | 318/318 |
| 仅功能包消费者 | 470/470 | 470/470 |
| 功能与 Themes 消费者 | 470/470 | 470/470 |
| Release 警告/错误 | 0/0 | 0/0 |
| 基础/消费者/API 三阶段退出码 | 0/0/0 | 0/0/0 |

相比 C3-06，新增 96 项测试、140 项 Demo、23 项 Usage 和每类消费者 38 项检查。消费者均使用全新 NuGet 缓存且没有源码项目引用。包消费使用 Avalonia Headless；PNG 契约按实际 PNG signature/IHDR，或后端明确 NotSupported、零输出和一次实际图表/UI 线程失败事件分别验证，不把无像素分支计为像素成功。真实图片与像素由两平台原生窗口和独立解码另行验证。14 个非产品依赖按原 SHA 复用；产品按本批各平台源码重新构建打包，未改 NuGet audit 或警告策略。独立包载荷审阅 14/14，核对新 nupkg、原生运行记录与消费者实际 Core/Themes DLL 的来源关联。

冻结 1400 个文件，两平台各 691 个声明运行输入同 SHA，运行前后声明源与程序集稳定。后续文档更新不等于整个工作区仍与冻结副本一致。独立来源/平台审阅 4280/4280；47 个历史文档/API pin（含 C3-06 三份）及 7 版历史 renderer 保持原 SHA。当前状态和平台矩阵深拷贝此前 C3-06 当前证据、计数及问题，再追加本批，历史记录与延期优先级保持原义。

完整本地 API 按 assembly+type 比较，含生成资源类型：154→163 类型，Charts 命名空间 152→161；新增 9 类型、既有类型增加 0 项声明，无旧类型、声明或成员删除。此为本地元数据比较，未认证二进制或官方 API 兼容。

独立 PNG CRC/deflate/滤波解码审阅 981/981，实际解码 168 张图。每平台两主题各 18 个预注册固定业务场景，覆盖各图族正向填充、负向空白与洞/间隔、颜色、缺失和方向规则。控制显式 420×280；预期由原始业务数值和公开布局公式独立计算并在对应首张图像前封存，不读取生产映射点、命中或 geometry 输出。门槛未放宽；真实 PNG 尺寸、捕获/导出一致和代表性 DPI 由审阅清单逐项记录。线条像素审阅不替代文字语义、绑定、生命周期或物理 present 证明。

两平台两主题原始页及改变状态后的四张整板均已实际查看。图片为已创建原生窗口的 RenderTargetBitmap。WSLg 合成窗口和程序化输入不代表物理桌面、实际 backend/GPU/compositor present 或真实读屏器资格。完整路径和 SHA 见[验证清单](charts-c3-analysis-validation.json)，公开声明见[API](charts-c3-analysis-api.txt)。

开发期原始失败与修复结果独立封存：首次 Core 缺少 Avalonia.Controls using 产生 2 个编译错误，其记录为工具转录而非原始日志；测试编译曾因生命周期夹具宿主类型不匹配失败。初始 Pictorial 投影元数据断言在延迟场景发布前读取，后续保留 Item/ID/RawPoint 断言并在绘制后有限排空 UI 任务。tests-v2 是编译失败后误用旧测试 DLL 的 77/79，明确排除为新候选验证；tests-v3 的 88/90、修复后 tests-v4 的 90/90 和 full-tests-v1 的 1494/1494 均保留原结果。首张对应图片前另修正 Heatmap 固定 gap/键、Pictorial 两极性输入、初始标签开关与只读 river 图例夹具，预注册 oracle 和像素阈值未改变。native-v1 的 Usage317/317 通过，但 Demo 在1052项检查后因生产控件 AnalysisChartBase.Render→GetMarks→SetSelected 对外通知 TextBlock 绑定，触发 compositor 的 Visual was invalidated during the render pass，进程失败；这不是夹具问题。该失败实际使用的8个DLL已在后续重建前保留并按原manifest核验。本轮随后修复生产控件，使几何构建与状态发布分离，由绘制后的单个UI任务按最新revision发布，并增加回归及默认TwoWay选择绑定。修复后 tests-v5 96/96 与 full-tests-v2 1500/1500、native-v2 Demo1188/1188、Usage318/318、两进程退出0及8DLL运行稳定按实际证据保留。全部开发失败及修复均不替代后续冻结正式验证。

## 官方公开范围对照

按官方公开图族资料核对场景范围；未运行许可官方程序集，未验证属性、默认值、视觉或二进制逐项等价。旧矩阵的 200 个目录身份及 59 个 URL 原顺序保持不变。

- [Funnel](https://docs.avaloniaui.net/controls/data-display/charts/analytics/funnel-chart)
- [Pyramid](https://docs.avaloniaui.net/controls/data-display/charts/analytics/pyramid-chart)
- [Heatmap](https://docs.avaloniaui.net/controls/data-display/charts/analytics/heatmap-chart)
- [Pictorial bar](https://docs.avaloniaui.net/controls/data-display/charts/analytics/pictorial-bar-chart)
- [Theme river](https://docs.avaloniaui.net/controls/data-display/charts/analytics/theme-river-chart)

## 实现契约

- 本批新增 FunnelChart、PyramidChart、HeatmapChart，AnalysisChartBase/AnalysisSeries/AnalysisMarkInfo 公共底座，PictorialBarSeries/PictorialBarRenderMode，以及 ThemeRiverStack 居中堆叠辅助算法。这是本库公开 API 与组合契约，不是官方同名程序集、属性、默认值或二进制替代。
- AnalysisChartBase 的单一 Series 复用 ItemsSource、CategoryPath（LabelPath 为 CLR 别名）、ValuePath、IdPath 与共享 EmptyPointMode；快照保留真实 Item、SourceIndex、StableId、原始值和缺失元数据。数据源与嵌套属性观察在视觉挂载时订阅、换源和卸载时解除；Heatmap 的 RowPath/ColumnPath 也参加嵌套观察。临时继承父级、挂载修订和同步收敛处理绑定回调中立即卸载、重挂载与换源，未挂载查询不长期持有外部观察者。源变更应发生在 UI 线程。
- FunnelChart 仅将有限正值生成阶段，保留源顺序与源序号，不自动按值排序；每个有效阶段使用相等高度槽。边界宽度为 W×(NeckWidth+(1-NeckWidth)×value/max)，末边界使用 NeckWidth；默认 NeckWidth=0.3，范围 0～1。SegmentGap 默认 2 DIP，只裁掉相邻阶段内部边缘，间隔按阶段高度限制，不改变业务值。非正、缺失、非有限或无可表示面积的阶段不制造可命中几何。
- PyramidChart 是向下展开的直立三角形。无间隔时，边界高度为 H×sqrt(累计正数贡献/总贡献)，宽度随三角形高度线性增长，因此每层面积按有效正值成比例。先按最大值缩放并补偿累加，避免直接求和溢出；保留源顺序，默认 SegmentGap=2 DIP。间隔删除实际绘制面积，不再宣称带间隔层面积严格相等于原始比例；小到没有可表示面积的正数不人为膨胀。
- HeatmapChart 的 RowPath/ColumnPath 必须解析到非 null 键；缺路径或 null 键只跳过该源项。行列保持首次出现的顺序，并以 CLR 类型加值相等区分类别，数值 1、字符串 1 与 double 1 分开。NaN 源项仍保留有效行列键，未出现的矩阵组合不会生成业务项。重复单元最后源项胜出，包括最后源项无效时默认留空；颜色域只统计实际胜出的有效数值。共享 EmptyPointMode 的显式填补仍可改变显示投影，原始缺失元数据继续保留。MissingBrush 仅为明确提供的无效源单元提供可选填充，不填满缺失组合。
- Heatmap 自动颜色域来自胜出有限值，零和负数有效；自动相等域使用颜色中点，色标也显示单一中点色。可选 Minimum/Maximum 必须有限；显式相等或反向域令 IsColorDomainValid=false 并清空有限值单元，绑定恢复可重绘。LowColor/HighColor 按 ARGB 通道线性插值，越界颜色 clamp 而不改原值；支持相反 MaxDouble 及相邻巨大有限端点。CellGap 默认 2 DIP，以半间隔内缩槽位并限制到槽尺寸，无正面积则无绘制和命中。
- Heatmap 的 ShowAxes/ShowColorScale 为行列文字与真实颜色标尺预留空间；单元文字由 ShowValues 控制，ShowLabels 单独开启不制造源 ordinal 标签。有效单元按填充亮度选黑/白标签，缺失占位没有伪造数值文字。Tooltip 保留行/列上下文和真实原值，命中仍返回原始 Item 与 SourceIndex。标题、文字和色标只实现单行省略及边界约束，没有高级碰撞避让或复杂模板。
- 分析图的 GetMarks 返回与绘制一致的形状和原始数据，HitTest 只接受真实填充区域；漏斗斜边外、阶段间隔、Heatmap 缺失与槽间空白不回退为矩形或近点命中。Left/Right/Up/Down/Home/End 按实际可见源项浏览，Enter/Space 选择，Escape 清除浏览与选择。SelectedIndex 默认 TwoWay，SelectedPoint 在源变更后按稳定身份重查。几何构建与对外状态发布分离：Render 冻结当前数组并只计算本帧显示状态，不从绘制调用对外 SetAndRaise；最多一个待执行 UI 任务在绘制后按最新 revision 汇合 ActivePoint/SelectedPoint/跟随后的索引。回调中新源或显式选择优先，dirty 几何等下一版重建后才发布，避免旧快照回写；绘制中的 Reset/Active 变更也排到安全发布点。卸载解除观察和清除 ActivePoint，完整 C2 选择/取消事件体系仍未统一适配。
- PictorialBarSeries 继承 ComparisonSeries，复用 Cartesian 分组、方向、宽度/间隔、轴投影、源顺序/排序、标签、选择及导出。正负值从真实基线分别向两侧绘制；零值没有符号主体。Stretch 将一个轮廓缩放至完整数值柱；Clip 将一个 SymbolSize DIP 轮廓放在基线并按数值柱裁剪；Repeat 从基线按 SymbolSize+SymbolSpacing 重复，最后一个轮廓只显示实际剩余范围。SymbolSize 默认 20 DIP（1～4096），SymbolSpacing 默认 4 DIP（0～4096），两者相对当前像素布局而非业务数值单位。 Demo/Usage 的类别轴显式使用 CategoryPlotMode.BetweenTicks，为首尾符号留出至少半个类别槽；OnTicks 保留端点并按 plot 边缘裁剪是既有轴契约，不靠扩张生产几何遮蔽。
- Pictorial 几何按含用户变换的 bounds 归一，克隆几何并组合变换，不改调用方对象；Geometry=null 使用单位矩形，空或非有限 bounds 不绘制。所有模式裁剪到真实数值柱与 plot；命中按符号实际填充、洞、重复间隔和裁剪判断，较大 tolerance 不填平空白。矩形/椭圆/线段多边形/布尔组合提供可在 Headless 使用的几何判断，曲线路径仍依赖原生填充规则。替换 Geometry 或原地修改后调用 Refresh；独立符号描边与点标记属性未用于此主体。Cartesian 源通知先恢复规范源坐标，投影后的 RawPoint/选择元数据在后续场景/UI 任务发布。
- ThemeRiverStack.CreateBaseline 接受相同有序槽位、有限非负贡献。默认 Center 为最大总量的一半，底座为 Center-当前总量/2，故可见堆叠总量围绕 Center；显式 Center 至少为最大总量的一半且必须有限非负。总量和最终端点必须可用有限 double 表示，否则明确抛 OverflowException；空流或空槽返回空/对应槽位，不自动补缺失、排序或对齐源。结果是供既有堆叠 AreaSeries 组合的透明正向 spacer；Demo/Usage 的图例只读，避免隐藏某条贡献后沿用旧底座。该辅助算法不订阅源变化，调用方更新时重新计算。
- AnalysisChartBase 与现有 Cartesian 图共用有界 PNG/JPEG 同步/异步导出管线，支持流/路径、尺寸、DPI、背景、预算、取消与实际图表发送者失败事件，绘制入口直接变换至目标像素。包消费者的 Headless 后端若不产 PNG，只验证精确 NotSupportedException、零输出与一次正确 owner/UI 线程失败事件；不将此分支记为像素成功。原生窗口导出和独立 PNG 解码另行核对尺寸、画面与 DPI。

## 剩余问题

- C3-01～C3-07 的当前软件子集均不等于完整 C3 结束；下一主项为 C4-01 数据与视图分层失效。本批不自动开始 C4，也不关闭完整 C2/C3/C4/C7、C5 高级统计比较与 C6 无轴云/紧凑打包，旧图族已记录问题保持有效。
- 未使用官方许可运行时作逐项参考，完整官方 API、默认值、视觉、组合及二进制等价仍未验收。漏斗本库源顺序/颈宽公式、面积比例金字塔、类型敏感 Heatmap 键和 ThemeRiverStack 辅助接口均为本库明确契约。
- Funnel/Pyramid/Heatmap 新分析控件尚未完整适配 Cartesian 的多系列图例、Trackball、范围选择器/联动、触摸缩放平移、多轴/标注、可取消选择事件和标准入场/数据/视图动画时间线；目前为基本形状命中、源项选择、键盘、Tooltip 及导出。
- 分析图密集/极小尺寸标签、漏斗外标签/连接线、复杂色标布局、任意控件模板、RTL/国际化布局以及高级碰撞避让仍待专项。Heatmap 目前为离散矩阵，不含连续二维 binning/插值热力、复杂颜色空间/分段尺度或矩阵虚拟化。
- Pictorial 目前为 Geometry 轮廓和本库 Stretch/Clip/Repeat 语义，不含图片/任意控件符号、独立符号边框、完整逐点样式或所有官方排列组合。大量重复几何、曲线/布尔组合、复杂轴断裂和多种交互组合仍需更广专项；原地修改几何必须 Refresh。
- ThemeRiverStack 只是居中非负堆叠底座；居中底座采用 Center-总量/2 的 silhouette 式居中规则；没有完整 Theme river 控件，wiggle、优化流顺序及更多基线算法未验收，也没有正负流混合、自动稀疏对齐/缺失策略及隐藏系列后自动重算。透明底座是辅助系列，调用方应从业务命中和可交互图例排除；Demo 使用受控组合。
- 本批未建立新的正式性能或内存回归 SLA。大 Heatmap、重复符号、动态堆叠、复杂几何的吞吐/尾延迟/分配仍归 C4；用户延期的百万点命中 p95 历史回退 0.7649→1.1235 ms 保持开放，不因功能通过而关闭。
- 新图族尚未完成点级辅助语义/原生焦点及系统偏好整合；Windows UIA/Linux AT-SPI 原生选中项枚举继续由用户延期到 A11Y-NATIVE-01，完整原生桥接门禁仍为 false。物理 Windows/Linux、真实触摸/触控板/读屏器、实际 backend/GPU/compositor present 未由合成窗口认证。AX06 标题/末刻度布局与四个历史 PixelProbe DLL 缺口继续开放。
