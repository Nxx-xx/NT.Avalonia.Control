# Charts C5-02：金融价格图

执行日期：2026-10-21（香港时间）。冻结候选 `c5-financial-prices-source-v1` 完成 Windows 本地原生与 Ubuntu26.04 WSL2/WSLg 的软件验证。本轮交付蜡烛、OHLC、HiLo、Heikin-Ashi、固定箱 Renko、Kagi、Point & Figure，以及日期/价格/成交量组合宿主；下一主项为 **C5-03 技术指标与趋势**。完整 C5、交易所日历与官方运行时/API 等价对照继续开放。

## 使用与数学契约

- 公开 `FinancialSeries` 与 `CandlestickSeries`、`OhlcSeries`、`HiloSeries`、`HeikinAshiSeries`、`RenkoSeries`、`KagiSeries`、`PointAndFigureSeries`、`FinancialVolumeSeries`；可传 `ChartFinancialPoint`/预转换 `ChartFinancialSample` 或通过 Time/Date、Open/High/Low/Close/Volume/Gap 路径映射业务记录。涨、跌、平价画刷、宽度比例、多轴、原始源项命中、选择及导出复用 Cartesian 入口。
- Hilo使用同一完整有效OHLC输入，仅绘制High/Low，不提供独立HL-only数据契约。Time/DatePath接受DateTimeOffset或显式带时区的DateTime，日期字符串和Unspecified DateTime不属于本轮兼容范围。
- `ChartFinancialTransforms.Validate` 对原始OHLC/业务输入取防御快照，按 UTC 瞬间严格递增并保留原 offset；空输入合法。预计算样本可合法重复时间，例如同源多砖与Kagi同Slot拆段，不对这些样本施加raw时间唯一性。有效原始记录必须满足有限 `Low ≤ Open/Close ≤ High`，可选成交量有限且非负，负价格合法。显式 gap 忽略价格与成交量并重置各转换。静态 API 对非法 OHLC/时间/成交量抛异常；默认系列映射把非法价格记录报告为 DataError 和断点，重复或逆序原始时间仍拒绝。
- HA 收盘为当前 OHLC 均值，开盘为前一根 HA 开收中点；每段初始开盘取原始开收中点，上下沿包含原始 High/Low 与 HA 开收。Raw/HA 保留原始输入 Slot，gap 无 glyph；HA 当前样本的直接来源为当前原始记录，其递推依赖不冒充一段聚合成交数据。
- Renko 按每段首个 Close 锚定固定绝对箱宽；同向满一箱生成砖，反向达到 `ReversalBoxes` 箱才生成，首反向砖跨过前 `r−1` 个价位。Point & Figure 按 Close 形成交替 X/O 列，同向追加字母并更新末列，反向需 r 箱；字母中心为 `Open+direction×(1..BoxCount)×BoxSize`。Kagi 初始非零变动开始细线，反转采用固定绝对金额；严格越过前 shoulder 变粗、严格跌破前 waist 变细，刚好触及不切换；切换在阈值拆成同 Slot 的两段。
- `ChartFinancialSample` 区分显示槽、合成 OHLC、原始 SourceStart/EndIndex、端点与连续只读 Sources。Renko 同一记录可触发多个砖；Point & Figure、Kagi 延伸可更新现有列/段。转换结果的 Volume 是最后来源记录的原成交量，没有相加或复制出合成成交额。`FinancialChart` 对非时间变换隐藏成交量层。
- 输入最多一百万条，输出默认十万、可配置一到一百万；PF 预算按累计字母，其他转换按输出样本/段。超预算、无法形成有限且不同的价格或取消均拒绝并保留失败，不截断、不自动放大箱宽。公开取消支持静态算法；系列绑定和宿主映射仍可能同步在 UI 线程执行。
- 系列同样对预计算样本执行MaximumGeneratedSamples准入，Point & Figure累计字母不能用少量列绕过预算；宿主价格/成交量共享样本和对应预算。

公式参照 [TradingView HA](https://www.tradingview.com/support/solutions/43000619436-understanding-heikin-ashi-charts/)、[Renko 反转](https://www.tradingview.com/support/solutions/43000481040-what-do-renko-wicks-mean/)、[Point & Figure](https://www.tradingview.com/support/solutions/43000502276-learn-using-point-and-figure-charts/) 与 [Kagi](https://www.tradingview.com/support/solutions/43000502272-learn-to-use-kagi-charts/)。本项目选取 close-only、固定绝对阈值和明确的初值/gap/来源规则；这些约定没有覆盖 ATR、百分比或逐笔/intrabar 变体。合成图形值与原始观测分开保存，见[非标准图数据说明](https://www.tradingview.com/pine-script-docs/concepts/non-standard-charts-data/)。

`FinancialChart` 通过显式 SetData 单一日历压缩周末/未提供日期，提供共享交易槽、独立价格/成交量轴、精确 `MapTime`/`TryGetTime` 和 Unix 毫秒 `ProjectOverlay`；MapTime只覆盖最终Samples.Time，同时间多 glyph 映射到最后一个槽。未知日期与未保留最终输出glyph的源日期（包括PF/Kagi延伸覆盖的早期源日期）均映射为gap。宿主取数据快照，编辑后须再次 SetData；它没有自动多源calendar union，也没有内置交易所节假日、盘中休市或时区会话数据库。独立使用FinancialVolumeSeries时须由调用方配置合适的Y轴；宿主才自动配价格/成交量双轴。

Demo 入口：**Charts → 金融价格**，七图板展示蜡烛/成交量与日期叠加、OHLC、HiLo、HA、Renko、Kagi、Point & Figure，可调整转换参数和视图。Usage/两类隔离包消费者包含 compiled AXAML/MVVM 映射与通知、原始身份及宿主调用。

密集日期面板显式采用Stagger标签，Demo的Point & Figure宽度比例为.2；这是Demo视觉调整，没有修复或关闭既有AX06。固定数学专图仍使用协议中的.6宽度和隐藏坐标轴。

![Windows金融七图](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-financial-prices-20261021/windows/run-v1/verification/functional-v1/charts-light-financial-light-board-overview.png)

[Linux暗色七图](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-financial-prices-20261021/linux/run-v1/verification/functional-v1/charts-light-financial-dark-board-overview.png)。面板总览用于人工布局查看，独立业务像素门槛针对44张固定数学专图。

## Windows 与 Linux 验证

| 项目 | Windows本地原生合成 | Ubuntu26.04 WSL2/WSLg |
| --- | --- | --- |
| Charts 全套测试 | 2109/2109 | 2109/2109 |
| 完整原生 Demo | 1584/1584 | 1584/1584 |
| 其中金融 Demo | 114/114 | 114/114 |
| 编译绑定 Usage | 426/426 | 426/426 |
| 仅功能包消费者 | 593/593 | 593/593 |
| 功能与 Themes 消费者 | 593/593 | 593/593 |
| 独立数值固定场景 / 规模重算 | 41 / 5 | 41 / 5 |
| 数值叶级谓词 | 490103 | 490103 |
| 独立像素谓词 | 537/537 | 537/537 |
| 新金融 PNG / 其中专用 ROI 图 | 46 / 44 | 46 / 44 |
| 完整 Demo PNG | 813 | 813 |
| Release警告/错误 | 0/0 | 0/0 |

叶级谓词不是同等数量的独立测试，不能与产品测试相加。两平台各八个监督阶段的完整结果与失败记录见下方证据；没有自动重试。1557 个源文件在 Windows证据目录、Linux证据目录与短Windows构建目录三份副本逐 SHA 一致，883 个声明 runtime 输入在冻结及运行前后稳定。每平台项目 cache 首次为空，两类消费者各自使用新 cache；只读复用63个已按 SHA 检查的非产品项目依赖和14个非产品消费者依赖，没有复用历史产品包或二进制，没有修改 audit/警告策略。Linux 只读使用已有 SDK10.0.402、PowerShell/RG/ICU/X11库和字体，未安装系统包。

独立 Python 数值 oracle 在首次数值前封存41组输入/规则/异常及容差，重算5项规模输出。100k原始输入与HA全序列以 little-endian 原始字段 SHA 加首尾观测绑定，2048派生结果保留完整值及连续原始来源；数值绝对/相对容差1e−10，来源索引和时间 ticks 精确检查。44张业务 PNG 的420×280 DIP、plot=(40,24,354,226)、数据/轴/颜色/ROI在首图前封存；基础8种×亮暗的16对96DPI native/export逐 RGBA完全相同并保留差异 PNG，另覆盖蜡烛缩放/方向配色/次轴、Renko144/192DPI和日期/价格/量/叠加宿主。6×6 DIP ROI指定色像素至少6，DPI按面积缩放，空白控制为0；预期不来自生产几何。原生元数据另核对20条固定 probe/host/board、全部合成 sample/原始区间/命中与日历叠加。18项数值、8项像素、10项元数据正负用例另存合成证据，不冒充真实窗口或产品测试。

本地公开 API 188→203；Windows/Linux逐类型/声明一致，全部188个既有类型及成员保留，仅增加金融类型与元数据。这是本地源码/API快照兼容检查，不是官方或二进制等价认证。

## 规模诊断

| 固定工作负载 | Windows elapsed / 当前线程分配 | Linux elapsed / 当前线程分配 |
| --- | --- | --- |
| perf-validate-100k | 6.026 ms / 26074576 B | 11.277 ms / 26074576 B |
| perf-ha-100k | 44.322 ms / 60172160 B | 49.216 ms / 60172416 B |
| perf-renko-2048 | 0.793 ms / 2778640 B | 0.834 ms / 2778640 B |
| perf-pf-2048 | 0.676 ms / 963704 B | 3.017 ms / 970328 B |
| perf-kagi-2048 | 0.603 ms / 1038160 B | 0.689 ms / 1038160 B |

计时范围为小场景调用之后的单次产品算法调用，观测投影和输出 SHA 在计时之外；输入快照与算法自身分配在范围内。没有 p50/p95、固定硬件/功耗、持续 UI 吞吐或长期内存资格。本轮未重跑 C4-08 九阶段性能门禁，也未复跑 C5-01 独立统计数值/像素 oracle；其既有证据完整保留，旧普通图/统计数字不能认证本候选金融 SLA。全套功能测试与原生 Demo 已包含既有统计功能回归。

## 失败记录与证据

- 开发test-v1因错误使用不存在的SelectPoints入口编译失败，v2被xUnit1051取消令牌分析器拒绝；两次均没有执行产品测试。调用修正并遵循TestContext令牌后v3为75/75，随后边界与准入预算回归补齐，最终v5为82/82；原编译日志与较早测试结果保留，不把早期通过冒充最终候选。
- 冻结前审查发现预计算ChartFinancialSample分支可绕过系列MaximumGeneratedSamples。已对预计算Renko/Kagi/Raw等样本计数、PF累计字母进行同一预算准入，并让宿主价格/成交量预算同步；补两项拒绝/不截断回归后进入最终82项测试，未采用文档豁免或提高上限。
- 首个native-v1由金融专用入口执行114项检查，但验证范围字段误写complete-charts-demo；原JSON字节保留，后续修正为financial-prices-only。正式验证执行完整Demo；专用开发图不作为完整Demo通过证据。
- 密集36槽日期面板暴露既有旋转标签预留导致绘图区过扁，Demo改用Stagger标签并把PF宽度比例设为.2；未改或关闭公共AX06。固定数学专图仍为.6与隐藏坐标轴，原始v1/v2/v3/v4图片保留，最终44专图537项像素、433项原始身份/日历及41+5数值490103叶级谓词均通过。
- Windows与Ubuntu26.04 WSL2/WSLg的同一冻结source-v1分别八个监督阶段首次退出0，无自动重试；协议、数值容差、ROI及门槛未因结果放宽。此前C4/C5历史失败及本轮全部开发记录保持原文件，正式成功不视为修复旧时序/长路径或默认JIT问题。

独立数值/像素固定协议及门槛没有因结果放宽。历史动画前置时序、长路径及C4-08 dev-v1相对门禁拒绝继续保存，没有在本轮标记修复。

证据根：`<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-financial-prices-20261021`。

- [冻结源清单](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-financial-prices-20261021/source-snapshot-v1.json)、[验证清单](charts-c5-financial-prices-validation.json)、[完整公开API](charts-c5-financial-prices-api.txt)。
- [Windows八阶段](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-financial-prices-20261021/windows/run-v1/formal-run-v1.json)、[Linux八阶段](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-financial-prices-20261021/linux/run-v1/formal-run-v1.json)；[Windows功能结果](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-financial-prices-20261021/windows/run-v1/verification/functional-v1/results.json)、[Linux功能结果](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-financial-prices-20261021/linux/run-v1/verification/functional-v1/results.json)。
- [Windows数值原始数据](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-financial-prices-20261021/windows/run-v1/financial-prices/raw.json)、[Linux数值原始数据](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-financial-prices-20261021/linux/run-v1/financial-prices/raw.json)；[Windows独立数值](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-financial-prices-20261021/windows/run-v1/financial-prices/independent-numeric-result.json)、[Linux独立数值](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-financial-prices-20261021/linux/run-v1/financial-prices/independent-numeric-result.json)；[Windows独立像素](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-financial-prices-20261021/windows/run-v1/financial-prices/independent-pixel-result.json)、[Linux独立像素](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-financial-prices-20261021/linux/run-v1/financial-prices/independent-pixel-result.json)。

## 剩余问题

- 下一主项 C5-03 技术指标与趋势。金融高级目录、其他统计/工程目录、完整C5及官方可运行版本/API/二进制对照保持开放。
- 交易所时区、节假日/休市、盘中周期聚合、复权、实时行情接入、ATR/百分比箱宽、逐笔/intrabar重建、Renko wick及额外厂商变体不在本轮。宿主采用明确提供的数据日历，未知日期不插值；非时间转换多 glyph 同日期采用最后槽。
- 静态计算快照、系列映射和部分布局仍可能是 O(N) UI 同步工作；金融专用可见区LOD/命中索引、异步/增量变换、大场景首次可用/尾延迟、实际进程/原生/GPU内存和30分钟/8小时长稳仍开放。PF/砖/样本硬上限用于拒绝无界展开，不是内存或延迟 SLA。
- 本轮为 Windows 原生窗口及 Ubuntu26.04 WSL2/WSLg，输入/选择/视图为程序或合成路由。RenderTargetBitmap/导出PNG不认证物理输入、compositor present、GPU fence、原生Wayland、ARM、国产Linux、多屏DPI或实机读屏器。GitHub Actions远端run未执行。
- 导出矩阵覆盖八类96DPI、Renko144/192DPI；蜡烛缩放/配色/次轴和组合宿主为原生probe，并非所有金融类型×轴×主题×DPI×导出格式的全排列资格。
- Point & Figure的X/O符号宽高分别跟随交易槽和价格轴投影，当前不保证正方形箱；复杂数据标签/模板和交易终端专属布局仍需后续扩展。
- C5-01加权/二维/边界校正密度、小样本Student-t/bootstrap及其他统计图族仍开放。百万点hit p95历史0.7649→1.1235ms和A11Y-NATIVE-01仍按用户决定后置；AX06、四份历史PixelProbe DLL与旧C4-03/C4-06动画/长路径问题保留。
