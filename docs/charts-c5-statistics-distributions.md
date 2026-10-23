# Charts C5-01：统计与分布

执行日期：2026-10-21（香港时间；证据根保留任务起始日期20261020）。冻结候选 `c5-statistics-distributions-source-v1` 的 Windows 本地原生与 Ubuntu26.04 WSL2/WSLg 八阶段验证通过。本轮交付箱线、误差、直方图和核密度展示，以及相关公开数值计算；下一主项为 **C5-02 金融价格图**。其他统计目录、完整 C5 和官方运行时/API 等价对照继续开放。

## 使用与数学约定

- `BoxPlotSeries` 支持原始分组样本与 `ChartBoxPlotSummary` 预聚合、Type 7 分位数、可配置 IQR 须、离群点、均值、横竖方向和原始组身份。默认须取包含在 Q1/Q3±1.5×IQR 内的最远观测；离群点保留未过滤输入中的原始序号及顺序。若配置非常窄的 fence，且相应四分位数外侧没有入域观测，则该侧须停在四分位数，遵循XML契约。预聚合只验证摘要有序、有限、计数和异常项约束，不推断调用方的分位数算法或原始样本。
- `ErrorBarSeries` 支持对称/非对称绝对误差、Both/Plus/Minus、横竖方向、DIP 端帽及直接区间输入；缺少或负误差形成 gap 并报告 DataError。`ChartStatistics.MeanConfidenceInterval` 使用样本标准差与双侧 normal-z 近似，至少两个有限值，不是 Student-t 或小样本覆盖保证。
- `HistogramSeries` 使用真实数值分箱边界，支持非等宽 bins。计算入口覆盖固定箱数、箱宽和显式边界；左闭右开、最后右闭，范围外有限值与非有限省略值分别计数。Count/Probability/Density 按 IncludedCount 归一化；有入域观测时 Density 的各 bin 高度乘宽之和为一，无入域观测则各 bin 为零。
- `DensitySeries` 展示非负核密度样本与可选填充。Gaussian KDE 支持显式带宽或 Silverman 自动带宽 `.9×min(sampleSD,IQR/1.34)×n^(-.2)`，零 IQR 退回标准差，再退回常量尺度；默认显示域为 min−3h 到 max+3h。报告梯形积分，有限显示域不强制缩放到面积一。另提供 Summary、ECDF 与 Pearson；非有限输入遵循公开过滤/拒绝约定，溢出拒绝，不悄悄重排误差端点。

算法参照：[R Type 7 quantile](https://stat.ethz.ch/R-manual/R-devel/library/stats/html/quantile.html)、[NIST 箱线与围栏](https://itl.nist.gov/div898/handbook/eda/section3/boxplot.htm)、[R bandwidth / bw.nrd0](https://stat.ethz.ch/R-manual/R-devel/library/stats/html/bandwidth.html)及[R density](https://stat.ethz.ch/R-manual/R-devel/library/stats/html/density.html)；本项目的常量回退、逐点求和及窄 fence 边界采用本节契约。

统计系列复用 Cartesian 坐标轴、次轴、缩放、选择和导出；自动分类范围保留首末 glyph 所需留白，空统计系列不扩大其他系列范围。箱线命中的是原始组，直方图命中原始 bin，密度命中生成的密度 sample；它们不冒充内层样本或估计曲线上的真实观测。更改嵌套样本集合需要组属性通知或 `Refresh()`。

Demo 入口：打开 **Charts → 统计与分布：箱线、误差、直方图与核密度**。四图面板展示离群/均值、误差方向、分箱数、带宽、原始/预聚合及恢复视图；Usage 提供 compiled AXAML/MVVM 双向绑定、数据通知、原始身份和两类隔离包消费者。

![Windows统计四图](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-statistics-distributions-20261020/windows/run-v1/verification/functional-v1/charts-light-statistics-light-board-overview.png)

[Linux暗色四图](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-statistics-distributions-20261020/linux/run-v1/verification/functional-v1/charts-light-statistics-dark-board-overview.png)。专用数学 probe 与面板总览分开保存；总览用于人工查看布局，业务 ROI 的自动判据针对专用固定图。

## Windows 与 Linux 验证

| 项目 | Windows本地原生合成 | Ubuntu26.04 WSL2/WSLg |
| --- | --- | --- |
| Charts 全套测试 | 2027/2027 | 2027/2027 |
| 完整原生 Demo | 1470/1470 | 1470/1470 |
| 其中统计 Demo | 56/56 | 56/56 |
| 编译绑定 Usage | 410/410 | 410/410 |
| 仅功能包消费者 | 574/574 | 574/574 |
| 功能与 Themes 消费者 | 574/574 | 574/574 |
| 独立数值 oracle | 1660/1660 | 1660/1660 |
| 独立像素 oracle | 247/247 | 247/247 |
| 新统计 PNG / 其中专用 ROI 图 | 30 / 28 | 30 / 28 |
| 完整 Demo PNG | 767 | 767 |
| Release警告/错误 | 0/0 | 0/0 |

上述 oracle 谓词与产品测试不是同一计数，不能相加。两平台各八个监督子进程首次正式退出0，没有自动重试；输入及载荷前后稳定。1538 个源文件在 Windows证据目录、Linux证据目录与短Windows构建目录的三份副本逐 SHA 一致，868 个声明 runtime 输入冻结前后稳定；项目 cache 首次为空，两类消费者各用独立新 cache。只读复用63个按 SHA 核对的非产品项目依赖与14个非产品消费者依赖，未复用历史产品包或构建二进制，未更改 audit/警告策略。Linux 只读复用既有 SDK10.0.402、PowerShell/RG/ICU/X11库及字体，未安装系统包。

数值 oracle 在首次实际数值前封存原始输入、公式、异常类型和容差，独立 Python 重算34个固定场景与3个规模诊断；不加载产品程序集。一般容差为绝对/相对1e−9，normal-z反函数区间绝对1e−7。100k与2048数据按精确二进制有理公式再生，并核对完整 little-endian double 字节 SHA。像素协议在首图前封存420×280 DIP、数学 plot=(40,24,354,226)、原始样本、固定轴、颜色和 ROI；不读取生产几何形成预期。亮暗共28图覆盖四类基础96DPI、箱须/异常、误差端帽、非等宽 bin 和5个 Gaussian knots；另有箱线缩放、误差横向/次轴及密度h2原生图，仅密度曲线追加144/192DPI导出。固定6×6 DIP业务 ROI 蓝色像素至少6，DPI按面积缩放，空白控制为0；8对基础96DPI native/export逐 RGBA完全相同并保留差异 PNG。12项数值与7项像素正负用例另存合成证据，不冒充真实窗口测试。

公开API 173→188；逐类型/声明保留全部既有成员，只增加统计类型与统计元数据，Windows/Linux 的完整本地公开API一致。这是本地源码兼容检查，不是官方或二进制等价认证。

## 规模诊断

| 固定工作负载 | Windows elapsed / 当前线程分配 | Linux elapsed / 当前线程分配 |
| --- | --- | --- |
| perf-box-100k | 24.359 ms / 7410928 B | 34.113 ms / 7403952 B |
| perf-histogram-100k | 17.875 ms / 6598088 B | 25.675 ms / 6604280 B |
| perf-density-2048 | 2.787 ms / 116232 B | 2.657 ms / 116232 B |

它们是同一新进程中完成小样本调用后的单次算法诊断，没有 p50/p95、固定硬件/功耗或长期内存资格。直接 KDE 为 O(n×sampleCount)，静态计算会取输入快照；数据源映射及这些统计计算仍可能在 UI 线程执行。本轮未重跑 C4-08 九阶段性能门禁，既有 C4 证据完整保留，不能用旧普通图场景认证新统计系列的 SLA。

## 失败记录与证据

- 开发初次筛选73项中72通过、1项 Mean 精确相等断言因约1ulp差异失败，改用12位精度后通过；首次 Demo 编译的6项 smoke API/using 错误与 Usage 初次编译的2项错误均保留，修正后编译零警告/错误。空路径或未执行 stub 不记作一次 benchmark run。
- 首次 native 开发图的独立数学 ROI 已通过，但面板首末分类 glyph 有半幅裁剪；修正分类 slot 留白并补单点、多点、方向与空系列回归后，新图首末箱体/端帽完整。最终统计新增88项，全套正式测试包含最后的空系列 Fact；开发87项的较早载荷不冒充包含该 Fact。
- 开发数值 v1 为1660/1660，native v1/v2均247/247；保留全部原始开发日志和 PNG。正式 Windows 与 Linux 的新目录各八阶段通过。数值/像素协议、原始样本及阈值未因结果放宽。
- 准备阶段保留初稿及修订：正确区分98份真正历史文件与5份滚动 metadata，并另外保存5份初始完整原始字节；早期文本 copy 的换行规范化文件没有冒充字节一致备份。这些准备收据不算产品性能故障。C4-08 dev-v1真实相对门禁拒绝、旧动画时序/长路径失败等历史不覆盖、不改写。

证据根：`<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-statistics-distributions-20261020`。

- [冻结源清单](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-statistics-distributions-20261020/source-snapshot-v1.json)、[验证清单](charts-c5-statistics-distributions-validation.json)、[完整公开API](charts-c5-statistics-distributions-api.txt)。
- [Windows完整功能结果](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-statistics-distributions-20261020/windows/run-v1/verification/functional-v1/results.json)、[Linux完整功能结果](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-statistics-distributions-20261020/linux/run-v1/verification/functional-v1/results.json)；[Windows八阶段](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-statistics-distributions-20261020/windows/run-v1/formal-run-v1.json)、[Linux八阶段](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-statistics-distributions-20261020/linux/run-v1/formal-run-v1.json)。
- [Windows数值原始数据](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-statistics-distributions-20261020/windows/run-v1/statistics/raw.json)、[Linux数值原始数据](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-statistics-distributions-20261020/linux/run-v1/statistics/raw.json)；[Windows独立数值](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-statistics-distributions-20261020/windows/run-v1/statistics/independent-numeric-result.json)、[Linux独立数值](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-statistics-distributions-20261020/linux/run-v1/statistics/independent-numeric-result.json)；[Windows独立像素](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-statistics-distributions-20261020/windows/run-v1/statistics/independent-pixel-result.json)、[Linux独立像素](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-statistics-distributions-20261020/linux/run-v1/statistics/independent-pixel-result.json)。

## 剩余问题

- 下一主项 C5-02 金融价格图。ST01/03/06～10 等 Beeswarm、Contour、Mosaic、Parallel coordinates、Ridgeline、Strip、Violin，以及其他统计/工程目录继续开放；本轮不代表完整C5或官方API等价完成。
- Weighted/二维或边界校正KDE、Student-t/bootstrap置信区间、异步/增量统计、大规模统计系列的分层细节/命中索引与后台算法专项不在本轮资格中；直接KDE的二重循环与输入快照仍需后续规模验证。
- 性能只有上述单次诊断；C4固定硬件FPS、首次可用/尾延迟、实际进程/原生/GPU内存、30分钟/8小时长稳及官方可运行版本对照仍开放。C4-08历史回归门禁未在本候选重跑，GitHub Actions远端run尚未执行。
- 本轮为本地 Windows 原生窗口及 Ubuntu26.04 WSL2/WSLg，输入/选择/视图经程序或合成路由执行。RenderTargetBitmap与控件PNG不证明物理输入、compositor present、GPU fence、原生Wayland、ARM、国产Linux、多屏DPI或实机读屏器工作流。
- 原始/预聚合箱线按组返回身份，分箱与密度按派生项返回；内层样本逐项选择、调用方自定义摘要真值、加权/多维分布和复杂模板仍需独立约定。嵌套集合原地更改须通知或Refresh。
- 百万点hit p95历史0.7649→1.1235ms按用户决定后置；A11Y-NATIVE-01原生UIA/AT-SPI选中项枚举按用户决定后续专项处理，AX06与四份历史PixelProbe DLL缺口保留。旧C4-03/C4-06动画前置时序及libHarfBuzzSharp长路径问题、本轮没有修复证明。
