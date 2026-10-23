# Charts C5-03：技术指标与趋势

执行日期：2026-10-21（香港时间）。冻结候选 `c5-technical-trends-source-v2` 在 Windows 本地原生窗口和 Ubuntu26.04 WSL2/WSLg 完成软件验证。交付六类趋势、四类均线和 SMA/EMA/WMA/Bollinger 指标对象；下一主项为 **C5-04 工程图与专业坐标**。完整 C5、官方运行时/API/二进制等价及物理平台资格仍开放。

## 使用与数学契约

- `ChartSeries.Trendlines` 附加 `Trendline`；独立 `ChartTrendlineSeries`、`MovingAverageSeries` 和 `ChartTechnicalIndicator` 支持 `SourceSeries` 引用及自身 `ItemsSource` 映射。源引用优先，默认继承源 X/Y 轴；显式轴键覆盖继承。图例、可见性、变更通知、导出及命中复用 Cartesian 入口。
- `ChartAnalytics.FitTrend` / `ChartTrendlineType` 支持 Linear、Polynomial、Exponential、Logarithmic、Power、MovingAverage。模型分别为 a+bX、degree 1..10 多项式、exp(a+bX)、a+b ln(X)、exp(a)X^b、尾随 SMA。指数/幂在 log(Y) 域最小二乘，R²在原始 Y 域计算；常量 Y 的 R²为 null，不自动声明完美拟合。
- 回归省略非有限 X/Y；指数另省略 Y≤0，对数另省略 X≤0，幂另省略 X≤0 或 Y≤0。`Count`/`OmittedCount`明确纳入和省略数量。回归把所有合格观察拟成一个模型，缺失点不切分模型；均线和 Bollinger 才按 gap 分段。不足 degree+1 条独立观察、秩亏或数值无法分辨的模型明确失败。
- 回归用中心/尺度归一化和 Householder QR，避免生产实现形成 AᵀA。默认在合格源 X 极值间输出128个均匀采样点，可配置2..4096点及显式 `Minimum`/`Maximum`；系列还可按映射 X 单位 `BackwardForecast`/`ForwardForecast` 扩展，显式范围优先。log/power 预测和取样要求 X>0。预测非有限坐标、非正域或不可表示的结果拒绝；外推属于模型计算，不代表未来市场数据。
- `ChartAnalytics.MovingAverage` / `MovingAverageSeries.Type` 支持 Simple、Exponential、Weighted、Triangular。保留输入长度、顺序、X和索引，不排序；period为1..65536。每个有限连续段前 period−1 个结果是 NaN。EMA用 period 点 SMA作种子，随后 α=2/(period+1)；WMA从最旧至最新权重1..period；TMA依次取 SMA(ceil(period/2))、SMA(floor(period/2)+1)，奇偶周期都总共预热 period−1 点。
- `SMAIndicator`、`EMAIndicator`、`WMAIndicator` 是独立指标对象，复用上述公式。`BollingerBandsIndicator` 默认period20、multiplier2，中轨SMA，上下轨为 SMA±multiplier×总体标准差（除以period）；multiplier有限且非负。中/上/下轨及带填充可独立设置样式和图例/可见性，带上下值参与范围计算；没有以样本标准差代替总体标准差。
- 均线/Bollinger对非有限 X或Y输出NaN并重置窗口；gap后的完整窗口从头预热。尾随计算使用完整映射源历史，再进行可见区裁剪，因此缩放后第一可见点可使用视口外的历史。MA趋势保留源对齐，不提供任意X预测、不接受显式范围或forecast，R²为null。
- `ChartAnalysisSample` 区分合成拟合点和尾随端点。MA/指标保留端点 `SourceIndex`/Item/StableId、原始RawPoint与源系列，命中结果另带计算点；拟合采样是合成数据，没有伪造原始业务Item/SourceIndex。附加趋势和指标组件会随源、参数、通知集合、增量buffer和轴更新。隐藏对象不绘制，也不自行触发数学计算；仍有Changed owner时保持SourceSeries观察，确保可见下游引用隐藏中间分析链时继续正确更新。移除最后owner或卸载后释放；循环SourceSeries拒绝。
- 静态API最多读取一百万条输入，支持取消并取防御快照；参数错误、不足/秩亏、不可表示结果分别抛明确异常。系列把可报告计算失败转为 DataError/Issues 和空结果，清除旧FitResult，不保留陈旧拟合图。追加/变更会使全源计算失效；当前没有声明O(1)增量算法或后台指标计算。

Demo入口为 **Charts → 技术指标与趋势**，六图面板可调整周期和趋势模型、追加/替换源及切换历史视图，展示均线/指标/附加与独立趋势/预测和金融叠加。编译绑定Usage和两类隔离包消费者覆盖SourceSeries、参数/源更新、Bollinger组件及合成/原始身份。

![Windows技术指标面板](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-technical-trends-20261021/windows/run-v3/verification/functional-v3/charts-light-technical-light-board-overview.png)

[Linux暗色面板](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-technical-trends-20261021/linux/run-v2/verification/functional-v2/charts-light-technical-dark-board-overview.png)。面板总览用于布局检查；数学像素门槛针对26张固定专图。

## Windows 与 Linux 验证

| 项目 | Windows 本地原生合成 | Ubuntu26.04 WSL2/WSLg |
| --- | --- | --- |
| Charts全套测试 | 2173/2173 | 2173/2173 |
| 完整原生Demo | 1714/1714 | 1714/1714 |
| 其中本轮技术指标/趋势Demo | 130/130 | 130/130 |
| 编译绑定Usage | 446/446 | 446/446 |
| 仅功能包消费者 | 617/617 | 617/617 |
| 功能与Themes消费者 | 617/617 | 617/617 |
| 独立数值固定场景 / 规模完整重算 | 60 / 4 | 60 / 4 |
| 数值叶级谓词 | 1103234 | 1103234 |
| 独立像素谓词 | 209/209 | 209/209 |
| 新技术PNG / 其中ROI专图 | 28 / 26 | 28 / 26 |
| 完整Demo PNG | 841 | 841 |
| Release警告/错误 | 0/0 | 0/0 |

叶级谓词不是独立产品测试，不能与Charts测试相加。成功候选source-v2按Windows run-v3、Linux run-v2串行各八个监督阶段，记录进程/退出码/超时和完整stdout/stderr。此前source-v1的Windows run-v1因sandbox阻止审计网络/SCM而在restore失败；run-v2通过全部测试/Demo/Usage后，隔离consumer因JSON默认拒绝预热NaN而失败。两个目录均保留。唯一运行源修正为consumer证据JSON允许命名浮点值，产品核心/数学/断言/协议与门槛未变；随后冻结source-v2、使用全新cache重新验证，没有自动重试，不声称首次成功。1572个冻结源文件在Windows证据、Linux证据和短Windows构建目录三份逐SHA一致；895个声明runtime输入在冻结及运行前后稳定。每平台项目cache初始为空，两类消费者各用新cache；只读复用63个按SHA核验的非产品项目依赖和14个非产品消费者依赖，不复用历史Charts包或产品DLL，不改变audit/警告策略。Linux只读使用已有SDK10.0.402、PowerShell/RG/ICU/X11库和字体，未安装系统包。

独立Python数值oracle在首次观察前封存60组输入/异常、4项100k规模和容差。均线/Bollinger用显式窗口与math.fsum，回归用60位Decimal正规方程及高斯消元，与生产QR实现不同；所有100k SMA/EMA/Bollinger结果逐点重算，linear重算129点及R²，输入以little-endian f64 X/Y SHA绑定。数值绝对容差1e−9、相对1e−10，非有限标记/形状/序号/计数精确检查，未使用生产程序集或几何作为预期。

像素协议在首图前封存420×280 DIP、plot=(40,24,354,226)、数据/轴/颜色/ROI。亮暗各11种native图覆盖4均线、Bollinger和6趋势，另有亮暗Bollinger导出及SMA144/192DPI；两对96DPI native/export逐RGBA完全相同并保留diff PNG。6×6 DIP ROI指定色像素至少6，DPI按面积缩放，颜色容差24，空白控制0。独立元数据oracle重算22个专图probe和2个history序列，核对全量计算点、预热、原始端点/合成身份、plot/axis、fit计数/R²及gap/卸载重附后的值。另保留8项数值、8项像素、6项元数据合成正负守卫，不冒充真实窗口或产品测试。

本地公开API 203→219；Windows/Linux逐类型/成员完全一致，203个既有公开类型与成员全部保留。该结果是本地源码/API快照兼容检查，不是官方或二进制等价认证。

## 规模诊断

| 固定工作负载 | Windows elapsed / 当前线程分配 | Linux elapsed / 当前线程分配 |
| --- | --- | --- |
| perf-sma-100k | 112.376 ms / 7400096 B | 150.601 ms / 7400096 B |
| perf-ema-100k | 2.306 ms / 7397352 B | 3.809 ms / 7397352 B |
| perf-bollinger-100k | 18.537 ms / 9002960 B | 22.064 ms / 9002960 B |
| perf-linear-100k | 42.208 ms / 13503112 B | 45.073 ms / 13494944 B |

计时为小场景后的单次产品API调用，含输入快照及算法自身分配；结果投影/序列化和SHA在计时之外。没有p50/p95、固定硬件/功耗、持续UI吞吐、实际进程/原生/GPU内存或长稳资格。本轮未重跑C4-08九阶段性能门禁，也未复跑C5-01/C5-02独立数值/像素oracle；旧证据保留，旧普通图/统计/金融数字不认证本候选技术指标SLA。完整功能测试和Demo包含既有图族回归。

## 失败记录与证据

- 开发tests-v1被xUnit1051取消令牌分析器拒绝，没有执行产品测试。数学测试helper改为显式传入TestContext.Current.CancellationToken，取消专测使用关联token；未抑制分析器。tests-v2的57项数学/系列测试通过，原失败日志和后续记录都保留，早期子集不作为最终全套通过。
- 开发native-v1已生成26张数学专图和2张面板，独立像素209项通过，但默认JSON序列化拒绝warmup/gap的NaN，进程退出1且没有有效完整报告。随后对Smoke显式配置JsonNumberHandling.AllowNamedFloatingPointLiterals并重建Demo；修复没有改变数学、输入、ROI或门槛。v1日志/PNG和v2报告保留，v1像素通过不冒充完整Demo成功。
- 冻结前只读API审查发现既有ChartSeries.CreateSnapshot从非virtual改成virtual，违反本轮203个既有公开类型/成员保持的约束。原声明/源码SHA和C5-02 API快照单独保存；最终候选恢复原公开wrapper并把分析扩展留在内部路径，Windows/Linux最终API逐声明检查无旧成员变化，没有为该差异增加豁免。
- 冻结前审查发现按IsVisible解绑分析源观察会切断隐藏中间链，导致可见下游读到旧缓存。最终与既有raw系列/整图隐藏策略统一：Changed owner存在即保持上游观察，隐藏不绘制/不自行计算；最后owner移除或卸载才释放。补隐藏中间链的源更新回归，不以隐藏豁免数据正确性，也不增加复杂lease或声称完成后台/增量算法。
- 开发Usage-v1的446项中，趋势切换后的FitResult与移除源系列后依赖更新两项断言失败，error为null。前者为懒计算夹具，改为先创建快照再核对模型/64个有限采样/R²；后者为真实产品问题：仍被分析系列引用的source移出显示集合后丢失逻辑DataContext，编译ItemsSource绑定被清空。产品修复依赖source的继承上下文/观察生命周期并补回归，保留原始失败和严格的手算SMA=8预期；最终完整Usage按冻结候选重新验证，没有放宽数值或订阅断言。
- source-v1的Windows run-v1因sandbox阻止正常NuGet审计网络产生NU1900并记录SCM拒绝，restore退出1且未执行产品测试；保留失败后授权宿主权限、新run-v2/cache。run-v2的2173测试/1714 Demo/446 Usage通过，随后function包消费者因证据JSON拒绝预热NaN而序列化失败，没有有效consumer-result。唯一source-v2改动为consumer JSON选项允许命名浮点值，产品核心/算法/断言均未改；新Windows run-v3与Linux run-v2使用新cache并串行各八阶段退出0，无自动重试；60+4数值及26张专图/原始来源元数据独立核对通过。数值容差/ROI/像素门槛未按结果放宽，开发失败和此前C4/C5历史原文件保留；本轮通过不视为修复旧时序、长路径、默认JIT或百万hit尾延迟。

数学容差、ROI与门槛没有根据结果放宽。历史动画时序、长路径和C4-08 dev-v1相对门禁拒绝原样保留，没有在本轮标记修复。

证据根：`<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-technical-trends-20261021`。

- [Windows首次环境失败](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-technical-trends-20261021/windows/run-v1/formal-run-v1.json)、[Windows消费端JSON失败](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-technical-trends-20261021/windows/run-v2/formal-run-v2.json)；[完整失败记录](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-technical-trends-20261021/development-and-formal-failures-v3.json)。
- [冻结源](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-technical-trends-20261021/source-snapshot-v2.json)、[验证清单](charts-c5-technical-trends-validation.json)、[完整API](charts-c5-technical-trends-api.txt)。
- [Windows八阶段](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-technical-trends-20261021/windows/run-v3/formal-run-v3.json)、[Linux八阶段](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-technical-trends-20261021/linux/run-v2/formal-run-v2.json)；[Windows完整功能](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-technical-trends-20261021/windows/run-v3/verification/functional-v3/results.json)、[Linux完整功能](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-technical-trends-20261021/linux/run-v2/verification/functional-v2/results.json)。
- [Windows数值观察](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-technical-trends-20261021/windows/run-v3/technical-trends/raw.json)、[Linux数值观察](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-technical-trends-20261021/linux/run-v2/technical-trends/raw.json)；[Windows独立数值](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-technical-trends-20261021/windows/run-v3/technical-trends/independent-numeric-result.json)、[Linux独立数值](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-technical-trends-20261021/linux/run-v2/technical-trends/independent-numeric-result.json)；[Windows独立像素](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-technical-trends-20261021/windows/run-v3/technical-trends/independent-pixel-result.json)、[Linux独立像素](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c5-technical-trends-20261021/linux/run-v2/technical-trends/independent-pixel-result.json)。

## 剩余问题

- 下一主项C5-04工程图与专业坐标；其他高级统计/金融/比较目录及完整C5仍开放。RSI/MACD等尚未核验官方范围，本轮不增加也不冒充已完成。
- 指标采用本报告明确的预热/EMA种子/TMA奇偶/总体SD/正域与缺失值约定；其他厂商初值、交易日历/时区会话、复权和逐笔变体不在本轮。专用范围外的官方运行时/默认值/API/二进制兼容需另作验证。
- 完整源快照/映射、追加重算、拟合与部分布局仍可能同步O(N)；大型首次可用/尾延迟、增量或后台指标算法、实际进程/原生/GPU内存及30分钟/8小时长稳仍开放。
- PNG来自原生窗口RenderTargetBitmap与导出，不认证物理输入、compositor present、GPU fence、原生Wayland、ARM、国产Linux、多屏DPI或实机读屏器；GitHub Actions远端run未执行。
- 导出/DPI矩阵为Bollinger亮暗96DPI和SMA144/192DPI抽样，并非所有模型×轴×主题×DPI×格式的全排列。复杂业务模板、特殊轴/断轴域的模型含义与高级产品布局仍需后续扩展。
- 百万点hit p95历史0.7649→1.1235ms和A11Y-NATIVE-01按用户决定后置；AX06、四份历史PixelProbe DLL与旧C4-03/C4-06动画/长路径问题保留。
