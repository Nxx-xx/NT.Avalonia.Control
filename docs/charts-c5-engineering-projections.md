# Charts C5-04：工程图与专业坐标

执行日期：2026-10-21（香港时间）。同一冻结候选 `c5-engineering-projections-source-v1` 在 Windows 本地原生合成窗口与 Ubuntu26.04 WSL2/WSLg 完成软件验证。交付 EN01–05：`CarpetPlot`、`HexbinChart`、`SmithChart`、`TernaryChart`、`WindRoseChart`；下一主项为 **C5-05 进阶比较**。其余统计目录、完整 C5、官方运行时/API/二进制等价与物理平台资格继续开放。

## 用法与本地数学契约

- 五图均继承 `EngineeringChartBase`，支持 `ItemsSource`、属性路径映射、`IdPath`、可观察集合更新、标题/网格/调色板、命中/选择、键盘、缩放平移/恢复及 PNG/JPEG 导出。`GetMarks` 的绘图序号与 `Members` 的源索引分开；聚合标记保留全部真实 `ChartDataPoint`、原始 `Item`、`StableId` 和原始值，不将中心/频数伪装成单条业务记录。
- `ChartEngineering` 是独立静态数学入口。Smith 接受物理阻抗 Ω 或导纳 S，正参考阻抗 Z₀（默认50Ω）；Γ=(Z−Z₀)/(Z+Z₀)，导纳 Γ=(1−Z₀Y)/(1+Z₀Y)。归一化实/虚部网格与外圆使用同一反射平面；只接受非负实部的被动源，拒绝非有限量、逆解圆外值、极点和不可表示结果。导纳不是把西门子当作欧姆。
- Ternary 对非负三分量先除最大分量再求和，防止大数归一化溢出；A在上、B在左下、C在右下，x=A/2+C、y=√3·A/2，均使用归一化比例。零总量、负分量、非有限量和三角形外逆解拒绝；屏幕逆解返回比例，不能恢复未知的原始总量。
- Hexbin 采用 pointy-top 轴坐标与 cube rounding；最大误差平局优先Q、cubeY、R，舍入使用 AwayFromZero。静态API以输入坐标和给定半径/原点分箱；控件先把有效源XY映射到未缩放DIP，用 `BinRadius`（默认18DIP）和plot中心聚合，再对整组几何应用视口变换。Count/Sum/Mean 可选，密度配色保留完整源成员；非有限观察省略但源索引不重排。缩放不隐式重分箱，resize会按新DIP布局重建。
- Carpet 要求至少2×2、无重复且完整的矩形A/B参数网格。X=A+Offset×B，Y在cell内双线性插值；网格同时沿A与B方向连接，逆解逐cell核验有效根、残差和Jacobian唯一性。折叠/奇异网格可显示，但 `IsInvertible=false`，逆解明确失败；缺节点不自动补造观测。
- WindRose 以北为0°，顺时针、扇区以主方向为中心，边界左闭右开；方向可使用度、弧度与16方位字符串。速度档同样左闭右开，末档至+∞。Frequency为正权重，Count另计真实条数，百分比以全部有效Frequency为分母；同扇区按速度叠加，控件半径按频数线性映射，不声称面积代表频率。负速度、非正权重或非有限观察省略，保留原始源索引。
- 静态源输入上限一百万，生成几何也受一百万预算限制，均提供有界拒绝与取消；Hex六顶点/格、Carpet两组路径和Wind档数分别计费。静态API不可表示聚合值明确抛异常；控件捕获可报告数据失败，发布只读 `DataError`、清空旧scene并显示错误，源恢复后可重建。当前映射、聚合、网格和源更新可能同步全量重算，未声明后台/增量或持续吞吐资格。

Demo入口 **Charts → 工程图与专业坐标** 包含Carpet、Hexbin、Smith阻抗/导纳、Ternary与Wind Rose六面板，可调半径、偏移、参考阻抗和方向数，并追加源与导航。编译绑定Usage和功能/主题两类隔离包消费者覆盖投影、映射更新和原始身份。

![Windows六图面板](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-engineering-projections-20261021/windows/run-v1/verification/functional-v1/charts-light-engineering-light-board-overview.png)

[Linux暗色六图面板](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-engineering-projections-20261021/linux/run-v2/verification/functional-v2/charts-light-engineering-dark-board-overview.png)。面板总览用于布局审查，四张最终亮暗面板已实际查看并保留[SHA绑定审查记录](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-engineering-projections-20261021/final-board-visual-review-v2.json)；数学像素门槛作用于固定22张专图。

## 双平台验证

| 项目 | Windows本地原生合成 | Ubuntu26.04 WSL2/WSLg |
| --- | --- | --- |
| Charts全套测试 | 2236/2236 | 2236/2236 |
| 完整原生Demo / 本轮工程检查 | 1860 / 146 | 1860 / 146 |
| 编译绑定Usage | 462 | 462 |
| 两类新cache消费者，各 | 639 | 639 |
| 独立固定数值 / 100k全量分箱 | 63 / 2 | 63 / 2 |
| 数值叶级谓词 | 333340 | 333340 |
| 像素谓词 / 原始来源元数据谓词 | 233 / 2726 | 233 / 2726 |
| 新工程PNG / 完整Demo PNG | 24 / 865 | 24 / 865 |
| Release警告/错误 | 0/0 | 0/0 |

叶级谓词不是独立产品测试，不能与测试数相加。Windows后Linux串行各九个监督阶段：完整Verify-Charts IncludeC1、两类独立包消费者、API快照、benchmark restore/build/run、数值/像素/元数据oracle；保留进程ID、退出码、超时和完整日志。1598个冻结源文件在四份副本逐SHA一致，916个声明runtime输入在冻结及运行前后稳定。各平台项目cache初始为空，两类消费者各用新cache；仅按SHA只读复用63个非产品项目依赖与14个非产品消费者依赖，不复用历史Charts包/DLL，不改变审计或警告策略。Linux使用已有私有SDK/库/字体，未安装系统包。

首产品观察前封存63组源输入/异常及两个100k生成公式，所有聚合成员与数值完整重算，little-endian f64输入SHA绑定真实源。Python独立使用复数除法、缩放后math.fsum归一化、轴坐标cube rounding、显式权重分档和双线性公式；没有加载产品程序集或几何作为期望。计算容差abs1e−9/rel1e−10，原始输入、参数、源payload、序号、计数、形状及非有限标记精确检查。

像素协议在首图前固定420×280DIP、plot、原始源和调色板：五类×亮暗×native/export共20图，另Smith144DPI和Ternary192DPI，共22专图；10对96DPI逐RGBA完全相同并保留diff。6×6DIP ROI至少6个指定色像素，DPI按面积缩放，颜色容差24，独立空白控制为0。原始元数据oracle独立重建10图全部mark、分箱成员、频数、原始Item所有字段、StableId、Point/Value与固定目标命中/选择；没有把聚合中心、归一化比例或频数当原始Item。另有18项数学/像素与7项元数据合成正负guard，不冒充真实窗口或产品测试。

本地公开类型 219→243，两平台逐声明一致，全部219个既有类型及公开/受保护成员保留；这是本地源码/API兼容快照，不是官方或二进制等价认证。

## 规模诊断与失败保留

| 固定负载 | Windows elapsed / 当前线程分配 | Linux elapsed / 当前线程分配 |
| --- | --- | --- |
| perf-hexbin-100k | 9.097 ms / 2031312 B | 9.383 ms / 2031312 B |
| perf-wind-100k | 8.896 ms / 1472680 B | 9.424 ms / 1472680 B |

计时为小场景之后的一次API调用，包含产品快照/算法分配；结果投影、序列化和SHA在计时之外。没有p50/p95、持续UI吞吐、实际进程/原生/GPU内存或长稳资格。本轮未重跑C4-08性能门禁、C5-01/02/03独立oracle；完整功能测试/Demo包含既有图族回归，旧诊断不能认证新工程图SLA。

首次独立100k Hexbin核对发现6条X=0的对称边界观察被分入错误邻格。原产品用不同浮点求值顺序计算−y/3与(2/3)y，舍入误差打破数学上的Q/cubeY平局，违反已声明的tie优先规则；最终共享yThird中间值计算Q/R，并增加固定边界回归。原63+2观察、378项失败谓词和六条源记录调查均保留，随后使用原始协议与精确成员预期重验；没有接受“邻格也算通过”，也没有改输入避开边界。

冻结前独立控件审查另发现真实问题：Hex resize/rebin后旧绘图序号可能指向无关源，最终改为追随原始身份；原生v2进一步暴露SelectedIndex赋值后未读SelectedPoint便立即resize的时序缺口，保留亮暗各三项失败并修复，测试没有靠增加getter读取绕过。有限1e308源可在Sum/Wind累加时溢出并从Render/dispatcher泄漏异常，最终保留静态API严格拒绝，同时由控件发布DataError/空scene并支持修复源后恢复。Hex Mean原先借助Sum也会误拒绝可表示的大数平均值，最终改用稳定在线平均；Sum不可表示时继续拒绝。增加对应回归后才冻结候选，没有把早期仅“有非空选择/有可绘图点”作为身份和失败隔离证明。

Windows正式run-v1九阶段通过后，Linux run-v1的前八阶段全部通过，第九元数据工具将本机/mnt/c路径错误转换为Windows C:/路径，导致24项文件存在性误报，正式run仍保留为失败。metadata-v2只修复OS-aware路径解析，10项平台路径正负guard通过；Windows用同一报告补核v2，2726个谓词名称和值与v1完全相同，没有重复已通过的.NET全套。Linux使用新源副本和全新cache/run-v2完整重跑九阶段通过。原source-v1/数值/ROI/容差不变，保留plan-v1与plan-v2、原工具及失败run；恢复说明见[工具差异与Windows补核](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-engineering-projections-20261021/metadata-tool-recovery-v2.json)和[Linux首次工具失败](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-engineering-projections-20261021/linux/run-v1/formal-run-v1.json)。

开发期另保留编译缺using、Smith导纳逆解夹具将零值误写在Γ=−1极点（正确零值在Γ=+1）、网格16/17条计数夹具及Demo截图helper类型不匹配等失败原日志。早期子集通过不替代冻结候选验收；本轮所有开发记录见[完整开发证据索引](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-engineering-projections-20261021/development-evidence-index-v1.json)。数值容差、输入、ROI和颜色门槛没有根据观察结果放宽，既有C4时序/长路径/默认JIT和百万点hit尾延迟问题未标记修复。

## 证据与剩余范围

- [冻结源](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-engineering-projections-20261021/source-snapshot-v1.json)、[Windows原正式计划](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-engineering-projections-20261021/formal-plan-v1.json)、[Linux恢复计划](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-engineering-projections-20261021/formal-plan-v2.json)、[验证清单](charts-c5-engineering-projections-validation.json)、[API](charts-c5-engineering-projections-api.txt)。
- [Windows九阶段](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-engineering-projections-20261021/windows/run-v1/formal-run-v1.json)、[Linux九阶段](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-engineering-projections-20261021/linux/run-v2/formal-run-v2.json)；[Windows完整功能](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-engineering-projections-20261021/windows/run-v1/verification/functional-v1/results.json)、[Linux完整功能](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-engineering-projections-20261021/linux/run-v2/verification/functional-v2/results.json)。
- [Windows原始数学观察](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-engineering-projections-20261021/windows/run-v1/engineering-projections/raw.json)、[Linux原始数学观察](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-engineering-projections-20261021/linux/run-v2/engineering-projections/raw.json)；[Windows独立数值](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-engineering-projections-20261021/windows/run-v1/engineering-projections/independent-numeric-result.json)、[Linux独立数值](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-engineering-projections-20261021/linux/run-v2/engineering-projections/independent-numeric-result.json)；[Windows像素](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-engineering-projections-20261021/windows/run-v1/engineering-projections/independent-pixel-result.json)、[Linux像素](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-engineering-projections-20261021/linux/run-v2/engineering-projections/independent-pixel-result.json)；[Windows来源元数据](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-engineering-projections-20261021/windows/run-v1/engineering-projections/independent-metadata-result-v2.json)、[Linux来源元数据](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-engineering-projections-20261021/linux/run-v2/engineering-projections/independent-metadata-result.json)。
- 官方目录依据：[Carpet](https://docs.avaloniaui.net/controls/data-display/charts/engineering/carpet-plot-chart)、[Hexbin](https://docs.avaloniaui.net/controls/data-display/charts/engineering/hexbin-chart)、[Smith API](https://docs.avaloniaui.net/api/avalonia/controls/charts/smithchart)、[Ternary](https://docs.avaloniaui.net/controls/data-display/charts/engineering/ternary-chart)、[Wind Rose](https://docs.avaloniaui.net/controls/data-display/charts/engineering/wind-rose-chart)。Smith正文cache miss，使用官方API页确认目录与阻抗/导纳范围；本地公式、初值和边界约定以上述契约为准，没有声称复制官方运行时默认行为。
- 下一主项C5-05进阶比较；ST01/03/06–10等剩余统计、其他目录及完整C5仍开放。非矩形Carpet/任意折叠面的唯一逆解、主动/有源Smith圆外模型、Hex其他布局/重分箱策略、Wind面积频率变体与复杂专业标签仍需明确范围后扩展。
- 所有PNG为原生窗口RenderTargetBitmap或导出，不认证物理输入、compositor present、GPU fence、原生Wayland、ARM、国产Linux、多屏DPI或实机读屏器；远端GitHub Actions run未执行。DPI为两类抽样，非所有模型×参数×主题×格式全排列。
- 百万点hit p95与A11Y-NATIVE-01按用户决定后置；AX06、四份历史PixelProbe DLL和旧动画/长路径问题保留。大型首次可用、尾延迟、长期内存、后台/增量工程计算、固定硬件及30分钟/8小时长稳资格继续开放。
