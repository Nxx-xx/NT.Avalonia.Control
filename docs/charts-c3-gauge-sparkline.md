# Charts C3-06：仪表、微图与相关变体

执行日期：2026-10-18。最终候选 `c3-gauge-sparkline-source-v2` 实现六种仪表与四种微图模式；Windows 与 Ubuntu 26.04 WSL2/WSLg 软件功能验证通过。完整 C3、官方运行时等价、性能和物理平台资格继续开放。

## 功能与 Demo

`CircularGaugeChart`、`GaugeChart`、`LinearGaugeChart`、`ProgressDonutChart`、`GradientRingChart` 与 `LiquidFillGaugeChart` 提供线性域、当前值/目标值、方向、格式化、绑定和基础布局；阈值区间由圆形/半圆/线性仪表绘制。`SparklineChart` 的 `Line`、`Area`、`Bar`、`WinLoss` 支持真实源项身份、缺失断线、基线、胜负/零颜色、首尾/极值标记、命中、Tooltip 与键盘浏览。两图族共享 PNG/JPEG 同步及异步导出。Demo → Charts →「仪表与微图」、编译绑定 Usage 和两类隔离 NuGet 消费者均已展示本批图族。

## Windows 与 Linux 验证

| 最终 v2 | Windows 原生合成窗口 | Ubuntu 26.04 WSL2/WSLg |
| --- | --- | --- |
| Charts 测试 | 1404/1404 | 1404/1404 |
| 原生 Demo | 1048/1048 | 1048/1048 |
| 编译绑定 Usage | 295/295 | 295/295 |
| 仅功能包消费者 | 432/432 | 432/432 |
| 功能与 Themes 消费者 | 432/432 | 432/432 |
| Release 警告/错误 | 0/0 | 0/0 |
| 基础/消费者/API 三阶段退出码 | 0/0/0 | 0/0/0 |

相比 C3-05 增加 112 项测试、94 项 Demo、20 项 Usage 和每类消费者 36 项检查。消费者各用全新 NuGet 缓存，无源码项目引用。消费者采用 Avalonia Headless，新仪表/微图的 PNG 检查接受实际 PNG 尺寸，或明确 NotSupported、零输出与一次实际图表/UI 线程失败事件；真实图片和像素门禁由两平台原生场景及独立解码承担。14 个非产品依赖按既存 SHA 复用，产品按各平台本批源码重新构建打包，未改 NuGet audit 或项目/还原警告门禁。独立包载荷核对 14/14 通过，将两平台新 nupkg 的 Core/Themes DLL 与原生运行记录及隔离消费者实际程序集逐项关联。

冻结 1380 个文件；两平台各 674 个声明运行输入逐项同 SHA，运行前后声明源和程序集稳定。文档更新后不声称整个工作区仍等于冻结副本。独立来源/平台审阅 4188/4188；44 个历史文档/API pin（含 C3-05 三份）和 6 版历史 renderer 保持原 SHA。

完整本地 API 按 assembly+type 比较，含生成资源类型：140→154 类型；Charts 命名空间 138→152。新增 14 类型、既有类型增加 0 项声明，无旧类型、声明或成员删除。此为本地元数据比较，未认证二进制或官方 API 兼容。

独立 Python stdlib PNG CRC/deflate/滤波解码审阅 869/869，实际解码 168 张图。每平台两主题各 18 个固定业务场景覆盖仪表弧/指针/阈值/目标、半圆、方向、水平/竖直、同心环、水位/波形/裁剪、进度空满与越界，以及微图四模式、缺失断线和零带。控制显式 420×280；探针在首张图像前封存，由原始业务数值和公开布局、弧角/线性比例/正弦公式独立计算，不用生产映射点、命中或 geometry 输出作为预期。6 DIP 方框、RGB 容差 24、alpha≥240、正向至少 6 像素、负向零像素的门槛保持不变。18 个原生捕获与 96 DPI 导出逐像素一致；两项代表场景额外核对 144/192 DPI 的固定像素尺寸、密度与同一当前画面一致。像素线条不替代文字语义、绑定、生命周期或物理 present 证明。

两平台两主题原始页及改变值/模式后的整板均已实际查看。图像为已创建原生窗口的 RenderTargetBitmap；WSLg 合成窗口与程序化输入通过不代表物理桌面、实际 backend/GPU/compositor present 或真实读屏器资格。完整路径和 SHA 见[验证清单](charts-c3-gauge-sparkline-validation.json)，公开声明见[API](charts-c3-gauge-sparkline-api.txt)。

正式 source-v1 的两平台基础软件阶段均通过（1379 项测试、1048 项 Demo、295 项 Usage），但每平台首个隔离功能消费者仅 430/432，通过失败门禁明确阻止候选完成；两项失败是把 Avalonia Headless 的无真实 PNG 编码后端当作原生图片验证。Themes 消费者与 API 捕获按失败退出路径未执行，不记为通过；消费者脚本未走到末尾源比较，其 sourcesUnchangedDuringRun=false 原样保留，另按已有源清单逐项核对封存当时输入 SHA。已保留当时 671 个声明输入、1377 文件冻结副本、两平台各八份实际程序集副本、包、原始失败结果和日志。后续只修消费者对成功图片/明确 NotSupported+零输出+精确失败事件的后端分支断言，并修准 Demo 关于哪些仪表支持区间的文字；原生真实像素门槛和产品绘制算法不变。

source-v1 封存时摘要中的“后续只修夹具和文案、产品绘制不变”是当时的范围预期。随后独立复查另发现 Gauge/Sparkline 挂载与源同步的生命周期重入问题，以及极大有限 StartAngle / WavePhase 先相加导致增量丢失和晚启动 timer 的边缘状态；最终候选已修复这些产品边缘缺陷并增加回归，旧失败结果及原预期原样留存，不将其当作最终变更范围。

## 官方公开范围对照

本批按以下公开图族核对场景范围；未运行许可官方程序集，未验证完整 API、默认值、视觉或二进制等价。自有 typed GaugeValue、半圆默认与静止波形契约及未实现项见下文。旧矩阵 200 个目录身份及 59 个 URL 的原顺序保持不变。

- [Circular gauge](https://docs.avaloniaui.net/controls/data-display/charts/gauges/circular-gauge-chart)
- [Gauge](https://docs.avaloniaui.net/controls/data-display/charts/gauges/gauge-chart)
- [Gradient ring](https://docs.avaloniaui.net/controls/data-display/charts/gauges/gradient-ring-chart)
- [Linear gauge](https://docs.avaloniaui.net/controls/data-display/charts/gauges/linear-gauge-chart)
- [Liquid fill gauge](https://docs.avaloniaui.net/controls/data-display/charts/gauges/liquid-fill-gauge)
- [Progress donut](https://docs.avaloniaui.net/controls/data-display/charts/gauges/progress-donut-chart)
- [Sparkline](https://docs.avaloniaui.net/controls/data-display/charts/scheduling/sparkline-chart)

## 实现契约

- 本批新增 CircularGaugeChart、GaugeChart、LinearGaugeChart、ProgressDonutChart、GradientRingChart 与 LiquidFillGaugeChart 六种仪表，以及 SparklineChart 的 Line、Area、Bar、WinLoss 四种模式；GaugeBase、GaugeRange、GaugeValue 和 SparklineSeries 为本库公共 API，不将其视为官方同名属性或二进制替代。
- GaugeBase 使用共用线性 Minimum/Maximum（默认 0/100）、Value 和 TargetValue。有限且 Maximum>Minimum 才是有效域；越界有限值投影 clamp 到 0～1，原始 Value 不改写，非有限值不绘制有效进度并以破折号显示。绑定短暂相等、反向或非有限域为空进度，不抛绑定异常。NormalizedValue/NormalizedTarget 的属性变化按同一修订发布，回调中的新状态优先。
- CircularGaugeChart 默认起始 -225°、顺时针扫角 270°；GaugeChart 本库默认起始 -180°、扫角 180°，半圆按方向适配上下布局，修改为其他扫角则采用通用圆形布局。阈值区间按业务值 clamp、仅绘制终点大于起点的有限区间；刻度、指针和 target 使用同一线性映射，360° 刻度不重复末端。 Ranges 仅由 CircularGaugeChart、GaugeChart 和 LinearGaugeChart 绘制；ProgressDonutChart、GradientRingChart、LiquidFillGaugeChart 继承区间集合 API 但不渲染这些区间，target 均按各自几何绘制。 所有显示角先对原始 StartAngle 做 %360，再加扫角与比例，避免超大有限起始角吞掉半圆/整圆增量；原始公开 StartAngle 不改写。整环用两段半弧和反向内轮廓构造真实孔洞。
- LinearGaugeChart 支持横向/竖向；Clockwise=true 分别向右/向上递增，false 反向。track、进度、区间、刻度与当前/target 指针使用同一轨道矩形和映射。ProgressDonutChart 支持起始角、部分圆、方向、中心文字/百分比、域边界和 target；百分比来自 clamp 后比例，CurrentText 保留原值格式。
- GradientRingChart 的 Values/ValuesSource 是 typed GaugeValue 项（不是任意 ItemsSource+路径映射）；各环共享宿主域，按 TrackThickness+RingSpacing 向内递减，无正半径的后续项不画。每项可配置 Label 与任意 IBrush（含 Avalonia 渐变）；中心平均百分比只合计实际绘出的有效环。这里的 gradient 表示画刷支持，不表示实现专用角向渐变算法。
- GaugeRange/GaugeValue 项及可观察集合在挂载时订阅，在卸载/换源时解除；自身未有逻辑父级的项继承宿主 DataContext。RangesSource/ValuesSource 优先于内建集合；格式错误回退 G4，标题、刻度、标签采用单行省略与边界约束，不是高级标签避让或任意控件模板。 逻辑继承绑定同步触发换源、清集合或卸载时，观察集合与子项按最新期望状态收敛，避免旧快照复活外部订阅。
- LiquidFillGaugeChart 支持 Circle/Rectangle、业务域液位、正弦波振幅/频率/弧度相位以及裁剪内 target 线。0/100% 对应空/满；振幅限制为液体高度的 10%，128 段折线构造波面，容器外不绘制液体。IsWaveAnimationEnabled 默认 false，33ms UI timer 为可选相位推进；IsPaused、ReduceMotion 保留相位并停止 timer，卸载解除 timer，恢复从当前相位继续。ReduceMotion 由应用提供，不自动读取系统偏好。 相位先按 2π 归一后再加时间增量；挂载修订号与真实视觉树状态共同阻止旧 attach 返回后晚启 timer。
- SparklineChart 的源顺序决定等距 X 槽位，CategoryPath 是显示元数据，不将日期/类别按数值排序或按时间间隔拉开。单点 line/area 位于中央；bar/winloss 使用槽中心。默认缺失策略下，无效/缺失值保留槽并断开路径，不跨缺口连线；Area 每个有效段分别闭合到 Baseline，Bar 从真实 baseline 向正负方向绘制。 公开 Series.EmptyPointMode 可使用共享填补策略，并保留共享映射元数据。
- Sparkline Line 默认按原始有限值自动域，Area/Bar 自动域包含 Baseline；Minimum/Maximum 可覆盖。自动相等域扩展，双端显式相等或反向域标记 IsDomainValid=false 并清空几何，后续绑定恢复即可重绘。投影值 clamp 在有效域，数据 Point.Y 仍是原始值；极端有限范围用缩放后计算避免差值溢出。WinLoss 忽略数值域覆盖，采用相对 Baseline 的符号和固定 [-1,1] 显示域，不改变原数据。
- Sparkline 零值 Bar/WinLoss 在 baseline 用有界 1 DIP（不足 1 DIP 时用实际高度）条带表示，避免零数据不可见，不伪造非零业务值。Line/Area 标记画刷优先级为 Last、First、Maximum、Minimum、普通；Bar/WinLoss 主体颜色保留数值/胜负/零语义，高亮用描边。所有并列极值均可高亮。 普通 Bar 以统一 BarBrush 着色，零值可用 ZeroBrush；WinBrush/LossBrush 仅用于 WinLoss，普通 Bar 不自动按正负双色。
- Sparkline 的公开 Series 复用现有 ChartSeries 映射与源观察，ItemsSource/ValuePath/CategoryPath/IdPath 转发到该系列。命中、ActivePoint、键盘 Left/Right/Home/End/Escape 与 Tooltip 返回真实源项、源序号和稳定身份；段命中只选择最近真实端点，不制造插值业务项。卸载清除 ActivePoint、解除订阅；换源后按相同身份重查。 挂卸以修订号、订阅状态和 pending 循环收敛；先稳定临时继承绑定，再发布逻辑子项，防止绑定回调内立即卸载、换源及重挂载造成父级半完成异常或旧源观察遗留。
- GaugeBase 与 SparklineChart 通过共享内部 ChartRasterExporter 提供 PNG/JPEG 同步/异步、流/路径、像素尺寸、DPI、背景、预算、取消及失败事件；同一绘制入口直接按 DIP→目标像素变换渲染，不用二次位图中转。失败事件 sender 为实际图表；运行中的液体导出读取当前相位，不主动推进时间。 包消费的 Headless 默认绘制后端不提供 PNG 时，只验证精确 NotSupportedException 消息、零输出以及一次发送者正确的 UI 线程失败事件；不将此分支记为像素成功。成功分支读取真正 PNG signature/IHDR；原生窗口与独立 PNG 解码另行验证真实尺寸、像素及 DPI。

## 剩余问题

- C3-07 基础分析图为下一项；完整 C2/C3/C4/C7、C5 高级统计比较与 C6 无轴云/紧凑打包继续开放，旧 C3-03/C3-04/C3-05 已记录限制保持有效。
- 官方许可运行时、属性/默认值、二进制与视觉逐项等价未验收；本批 typed GaugeValue、GaugeChart 半圆默认、LiquidFillGaugeChart 名称与默认静止波形是本库显式契约。官方 GradientRingChart 的任意对象路径映射、进度 glow、任意模板和完整样式选项仍未实现。
- 仪表当前值/目标值的可交互编辑、系列/范围选择器/多图联动、Trackball、触摸缩放平移、完整标注、标准入场/数据/视图动画时间线等 C2 能力未统一适配本批 Gauge/Sparkline 图族；液体可选波相位 timer 不等于完成标准动画适配。Polar 图族也没有因此新增动画时间线。
- Gauge 的阈值可能被后绘制的进度覆盖；密集/极小尺寸刻度、标题、目标线和环底部标签的高级碰撞避让、换行、模板及布局策略仍待专项。GradientRing 无正半径的项不绘制、不计中心平均值；专用角向渐变及大量环虚拟化未实现。
- LiquidFill 使用 128 段静态波面与 33ms UI timer；隐藏祖先/禁用时的自动省电策略、真实帧同步、系统动态效果偏好和复杂波形仍待完善。物理帧率、长时 timer/订阅内存、批量仪表与密集微图虚拟化未作性能资格认证。
- Sparkline CategoryPath 是序号槽元数据，暂不支持真实日期间隔/排序、平滑阶梯/范围/堆叠微图、多系列微图、完整选择状态与任意 Tooltip 控件模板；默认布局和绘图为基础紧凑展示。
- 本批未测百万点首次可用、命中尾延迟、内存和吞吐回归。用户已将百万点命中 p95 的 .7649→1.1235ms 历史回退后置至 C4；不得将功能测试通过改写为性能门禁通过。
- Windows UIA 与 Linux AT-SPI 原生选中项枚举及读屏器工作流继续留到用户后置的 A11Y-NATIVE-01，completeNativeBridgeGatePassed=false，不在本批重验；新增 Gauge/Sparkline 也未认证原生点级辅助语义或键盘读屏播报。
- Windows 原生合成窗口与 Ubuntu 26.04 WSL2/WSLg 的程序化输入/RenderTargetBitmap 通过，仅覆盖软件功能；物理键盘/触摸/触控板、物理 Linux 桌面、实际渲染 backend/GPU/compositor present 和读屏器未验收。
- AX-06 以及旧四份历史 DLL 缺失造成的运行时参考证据缺口保留；既存历史 renderer 与文档/API pin 继续按原 SHA 保存，不据本批新程序集补签旧阶段。
- 可变 IBrush 就地修改不是本批专门的样式版本协议，完整 in-place 画刷变更/资源失效组合与高级颜色图例需继续跟踪；本批公共声明对比不构成二进制兼容认证。
