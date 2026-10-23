# Charts C5 补齐：统计与多维图族

执行日期：2026-10-23（香港时间）。本轮补齐 **ST01 Beeswarm、ST03 Contour、ST06 Mosaic、ST07 Parallel coordinates、ST08 Ridgeline、ST09 Strip、ST10 Violin**。结合已交付的C5-01统计、C5-02金融、C5-03趋势指标、C5-04工程坐标、C5-05进阶比较，C5五阶段的基础图族主项已交付；下一主项 **C7-01全功能集成**。完整专业能力和官方/平台资格仍按下方限制验收。

## 功能与公开契约

| 图族 | 本轮实现与边界 |
| --- | --- |
| Beeswarm / Strip | 原始分类观察、共同数值域、确定性碰撞/jitter、均值和拥挤诊断；不通过改动业务值或丢点解决拥挤 |
| Contour | 无序输入完整矩形网格，等值线与填色来自同一鞍点感知分片面；NaN mask、明确levels、单元原始来源、标量/路径适配 |
| Mosaic | 首见顺序保留类型的双分类、重复组合成员聚合、全局列宽及列内比例、零值/负值/大值规则 |
| Parallel | 可绑定Axes集合、每维独立/反向/显式clamp/常值/全缺失域，NaN不跨连、精确节点/线段命中；原始DTO及typed嵌套集合通知 |
| Ridgeline / Violin | 共享数值域和绝对密度尺度、带宽/采样点配置、原始样本序号和Type7摘要；预计算Ridgeline曲线、Violin中位线及小样本箱体规则 |

源选择返回真实业务组/记录，生成密度点和等值线不伪装成新业务项。Contour返回所属单元的原始网格成员；Mosaic返回该组合全部聚合输入；分布组metadata保留有效原始样本序号。重排、换源、卸载、键盘浏览、缩放平移和PNG/JPEG导出沿用现有工程/比较基础契约，完整专业跨族交互仍需C7。源码API见[本轮快照](charts-c5-completion-api.txt)，示例在[Demo](../samples/NT.AvaloniaControl.Demo/Pages/Charts/ChartsPage.C5Completion.cs)和[Usage](../samples/Charts.Usage)。

资源准入：Beeswarm/Strip最多20000观察、2000000工作计数；Contour最多65536网格点、32levels、262144图元和2000000保守cell/level工作量；Mosaic最多20000条；Parallel最多20000条、64轴及200000组合值；分布最多256组、100000实际采样项、131072生成密度点及4000000次核计算，缺失项同样计入枚举预算。纯API支持取消，控件数据错误清空旧帧并可从同源通知恢复。这些上限不是内存、帧率或延迟保证。

## Windows / Linux 验证

两平台使用冻结 **c5-completion-source-v2，1764文件**；清单SHA256 `9389F9190823FE89EB71CDD46651BF40EAA06899C17333E5528C2AA06BDE3BCC`。Windows本地原生合成窗口与Ubuntu26.04 WSL2/WSLg各通过7个受监督阶段：完整构建/Charts测试/Demo/Usage、两种隔离包消费、API、benchmark构建、数值记录、独立数值、独立原生图像/来源校验。

| 项目 | Windows | Linux / WSLg |
| --- | --- | --- |
| Charts测试 | 2830 | 2830 |
| 完整Demo / 新增检查 | 4380 / 334 | 4380 / 334 |
| Usage | 752 | 752 |
| 两种隔离包消费者各 | 968 / 968 | 968 / 968 |
| API公共/保护类型 | 499 | 499 |
| 固定数值 / 有限规模 | 57 / 7 | 57 / 7 |
| 独立数值谓词 | 820755 | 820755 |
| 独立原生图像/来源谓词 | 19191 | 19191 |
| 本轮PNG / metadata行 / RGBA完全相等对 | 32 / 44 / 14 | 32 / 44 / 14 |

旧C6基线的2753个测试身份、4046条Demo、688条Usage、两种消费者各904条检查全部保留并通过。当前API由459种增至499种，旧声明保留，两平台声明一致；不据此声明官方API或二进制等价。受监督运行前后源文件和程序集稳定；当前根目录的Charts运行输入SHA亦与正式候选相符。非Charts并行根变更另外记录在证据根 `root-working-tree-source-audit-v1.json`，不归功于本轮产品改动。

独立数值v3从完整原始input/config重算57固定场景与7规模输出，不加载产品DLL；覆盖碰撞/均值/jitter、Contour逐片交点/色带/原始成员、Mosaic质量比例、Parallel轴尺度/断线和KDE/Type7。37个手写正常/故意破坏的guard验证独立校验器，保留v2未通过18项guard的观察并由v3收紧完整性。正式数学容差及实现见协议，未通过调阈值或删场景使结果通过。

原生v2覆盖7模式×两主题×3阶段、44行metadata、原始引用/ID/索引、每场景100个固定命中格及独立锚点；32张PNG包含14对同平台96DPI native/export逐RGBA完全相同、Contour144DPI、Violin192DPI及两张总览，全部非空且尺寸/DPI合规。部分命中检验对生产者记录图元坐标作解析计算，数值层另行验证各族几何；它不是所有像素形状的完全几何证明，也未要求跨系统逐像素相同。

| 单次规模诊断 | Windows elapsed / 线程分配 | Linux elapsed / 线程分配 |
| --- | --- | --- |
| beeswarm-4000 | 15.716 ms / 1316240 B | 17.011 ms / 1316240 B |
| strip-4000 | 3.418 ms / 1285328 B | 4.148 ms / 1285328 B |
| contour-64x64 | 38.491 ms / 23947824 B | 42.098 ms / 23947824 B |
| ridgeline-10000 | 29.320 ms / 3409704 B | 27.179 ms / 3409704 B |
| violin-10000 | 16.840 ms / 3409704 B | 16.690 ms / 3409704 B |
| mosaic-20000 | 27.612 ms / 12784880 B | 30.233 ms / 12784560 B |
| parallel-20000x6 | 23.349 ms / 14330792 B | 24.309 ms / 14330792 B |

计时仅包含公开API调用，原始输入构造及JSON写出不计入线程分配；完整输入与输出保存在raw.json。上述数字只属于Charts专项，不能当作整个仓库、正式SLA或硬件平台验收。详细SHA/日志/原始结果见[机器可读验证](charts-c5-completion-validation.json)。

## Demo 与截图

启动 `dotnet run --project samples/NT.AvaloniaControl.Demo`，在Charts展开 **“C5 补齐：分布、等值线、列联比例与多维比较”**。七面板展示更新、缩放平移、选择、等值线填色、Strip抖动、小提琴摘要与Ridgeline重叠；Levels/Axes通知与集合切换在Usage中验证。独立smoke参数 `-- --charts-smoke --charts-c5-completion-only --smoke-output <json> --smoke-image <png>`；数值入口 `Charts.Benchmarks --c5-completion-regression --output <fresh-file>`。实际受监督命令记录于 `Run-C5Platform-v2.ps1`。

![Windows暗色C5总览](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-completion-20261023/windows/run-v2/verification/functional-v2/charts-light-c5-completion-dark-all-overview.png)

截图：[Windows](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-completion-20261023/windows/run-v2/verification/functional-v2)、[Linux](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-completion-20261023/linux/run-v2/verification/functional-v2)，文件名含 `c5-completion`。这是软件原生窗口RenderTargetBitmap/导出证据，不证明实际桌面present或物理输入。

## 失败保留及修复

开发证据全部保留：初版Contour误用Point.Length导致编译失败；第二次构建暴露测试未传TestContext取消令牌的xUnit分析器要求，按测试上下文修正而未压制分析器。target-v1两项夹具错误分别为Contour换源仍使用旧IdPath，以及单点带宽误期望1；前者修正夹具路径，后者按公开Silverman公式改为.9，产品带宽算法不变。

Usage先暴露Levels未严格升序和typed记录/轴维数未同步的夹具问题，修复夹具后又定位真实生命周期缺陷：Parallel构建首条无效记录抛出后，后续typed Values集合的旧订阅会被释放，导致集合INCC修复不能自动恢复。现在预先收集全部typed Values owners，失效空帧继续监听；新增回归覆盖3→4轴、逐条补值、4→3反向恢复与真实来源。双平台完整测试和Usage均覆盖该修复，关闭 **C5-PARALLEL-INVALID-FRAME-OBSERVERS-01**。

数值oracle-v1误读lowerQuartile/upperQuartile字段并误解默认KDE域，v2只修独立解释、保留原始input及产品；v2随后guard暴露18处漏检/误判，v3收紧来源/结果数量、类别均值、Mosaic排序和Contour完整交点/色带，未放宽数学容差。初版正式候选source-v1因并行PDF代码中PdfDocumentPresenter缺少RenderRequest声明而在Demo构建失败；不是C5故障。本轮没有编辑PDF源；source-v2新冻结含并行工作随后补齐的类型，旧候选、失败日志及根差异审计保留，未用新源覆盖旧证据。最终结论仅来自source-v2双平台run-v2。

## 与官方目录的对应及剩余资格

本轮核验以下七个公开页面，作为目录与行为入口的参考；没有授权官方运行时同机测试或二进制对照。具体API、默认布局、近似算法和资源预算以本库公开契约为准。

| 公开目录页 | 来源 |
| --- | --- |
| beeswarm-plot-chart | [官方页面](https://docs.avaloniaui.net/controls/data-display/charts/statistical/beeswarm-plot-chart) |
| strip-plot-chart | [官方页面](https://docs.avaloniaui.net/controls/data-display/charts/statistical/strip-plot-chart) |
| violin-plot-chart | [官方页面](https://docs.avaloniaui.net/controls/data-display/charts/statistical/violin-plot-chart) |
| ridgeline-chart | [官方页面](https://docs.avaloniaui.net/controls/data-display/charts/statistical/ridgeline-chart) |
| contour-plot-chart | [官方页面](https://docs.avaloniaui.net/controls/data-display/charts/statistical/contour-plot-chart) |
| mosaic-chart | [官方页面](https://docs.avaloniaui.net/controls/data-display/charts/statistical/mosaic-chart) |
| parallel-coordinates-chart | [官方页面](https://docs.avaloniaui.net/controls/data-display/charts/statistical/parallel-coordinates-chart) |

- C5-01～05的基础图族主项已交付，七类统计目录基础缺项已补齐；官方API/二进制/默认行为、完整专业交叉集成和平台性能资格继续由C7验收。
- Beeswarm为确定性有界碰撞启发式；密集输入可能明确标记Overflow/WorkBudgetExceeded并回退jitter，不承诺任意密度无重叠。Strip的jitter由seed和源序号决定，重排可改变横向偏移，业务值及源身份不变。
- Violin的ValuesPath/CategoryPath别名仅属于Violin，Ridgeline使用SamplesPath/LabelPath或ProfilePath；Violin缺少官方ViolinWidth和专用Fill/Stroke/StrokeThickness，中位线在少于5样本或ShowBoxPlot关闭时仍可由ShowMedian显示，与官方条件不同。
- Strip未提供PointOpacity，一些半径/jitter/描边默认值与官方不同；Parallel未提供CurveTension/LegendLabelPath/图例，自动轴范围规则也与官方默认0..100不同。
- Contour只接受完整矩形网格；NaN遮蔽相邻整单元，Infinity拒绝。采用鞍点感知三角片的分段线性等值线/色带，保留拓扑，不宣称任意散点插值或精确双线性曲线。总览中可见相邻填色三角片的抗锯齿细缝，专业渲染改进留C7。
- Mosaic为二维非负列联权重，不是任意维度递归mosaic；重复组合聚合并返回全部源成员，零权重无可选面积。不可表示的单元合计拒绝。
- Parallel按独立轴归一化并保留NaN断线；精确marker/线段命中与基本记录选择已实现，轴范围刷选、多图过滤、高级多轴格式/模板仍待专业集成。
- Violin/Ridgeline共享绝对密度尺度，Gaussian KDE为有界直接计算，无加权/自适应带宽/边界反射校正。命中返回真实组，个体原始样本索引保存在metadata，不提供每个隐藏样本的独立命中。
- Ridgeline预计算曲线为逐段线性，尚无官方CurveType样条和固定DIP SeriesHeight等价契约。Violin少于5个有效样本不画箱体；单点/常数按公开自动带宽fallback后仍乘Silverman系数。
- 历史金融/指标/工程/比较专业扩展仍开放：交易所日历及盘中会话、增量/后台指标、专业模板和完整共享Trackball/范围联动/标注/动画需C7逐项验证。AN16只核验公开目录，不能据此宣称授权runtime不存在RSI/MACD。
- Windows本地原生合成窗口及Ubuntu26.04 WSL2/WSLg是软件验证，不认证物理Windows/Linux桌面、GPU/compositor present、实际触摸/触控板、DPI跨屏、读屏或8小时长稳；未执行远端CI。
- 本轮七项规模计时/线程分配为单次诊断，不是p95/持续帧率/进程原生或GPU内存SLA；没有重跑C4-08正式门禁或全部旧阶段独立oracle。
- C7-NATIVE-LOD-AV-01仍未定位/未修复；地图暗色标签对比、历史AX06/PixelProbe/动画时序/长路径/default-JIT等问题保留。百万点命中尾延迟和A11Y-NATIVE-01按用户决定后置。

历史保护：125份旧阶段文档SHA未改变；只新增本轮报告/验证/API并更新六份滚动计划状态，保留前轮summary/evidence/checks。旧“下一项”代表当时状态，以最新C5摘要和C7-01下一主项为准。
