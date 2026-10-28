# Charts C7-01：专业 Tooltip 模板与原生导出稳定性

日期：2026-10-23（香港时间）。本批完成专业 Tooltip 模板及生命周期接入、普通/高对比主题和导出约定，并修复折线原生绘图资源生命周期。**受控 A/B 与 Windows、Linux 当前组合候选的完整回归和压力均已通过；原生问题已在指定受控协议下修复并通过双平台完整原生回归，不是所有原生路径的永久担保。C7-01 仍在进行，C7 整阶段未完成。** 前一批主题与目录结果保留在[集成报告](charts-c7-integration.md)，本批使用独立冻结候选和当前验证证据。

完整数据见[验证汇总](charts-c7-tooltips-export-stability-validation.json)与[本批 API 快照](charts-c7-tooltips-export-stability-api.txt)。

## 专业图族 Tooltip API

`EngineeringChartBase` 和 `AnalysisChartBase` 新增 `TooltipContentTemplate : IDataTemplate`、`TooltipBackground`、`TooltipForeground`、`TooltipBorderBrush`。两根使用相同 styled property 身份；Cartesian 的既有属性、`ChartTooltipContext` 和呈现实现保持独立。适用范围包含继承这两根的工程、统计、比较、层级、关系、地图、排程、运营及基础分析图族；尚未把该模板接口推广到 Polar、Circular、Gauge 或 Sparkline。

模板控件的 DataContext 为 `ChartDataTooltipContext`：

| 成员 | 语义 |
| --- | --- |
| `Members` | 当前标记贡献的全部原始 `ChartDataPoint`，保留 `Item`、`StableId`、`SourceIndex` 和数值元数据。直接使用当前帧成员列表，不在鼠标移动时复制整个聚合。 |
| `EngineeringHit` | 工程图族当前 `EngineeringPointHit`，包含真实聚合标记及其值；不制造代表整个聚合的假原始点。 |
| `PointHit` | 分析图族的真实 `ChartPointHit`；该读取的 `Members` 为对应单个原始点。 |
| `Text` / `Anchor` | 当前图族的标准读数及图表局部 DIP 锚点。 |
| `BusinessDataContext` | 读取时的业务 VM，与提示控件使用的读取 DataContext 分开。 |

模板作为真实控件加入图表逻辑/视觉树，继承资源、字体和主题。控件可复用，读取变化通过 DataContext 更新，模板应使用绑定。XAML 的 `x:DataType` 为 `charts:ChartDataTooltipContext`；业务 VM 和原始 `Members.Item` 可通过各自具有业务类型的 `ContentControl.ContentTemplate` 展示。[功能包 README](../src/NT.AvaloniaControl.Charts/README.md#专业图族-tooltip-模板)提供实际绑定示例。

提示层限制在图表边界内并裁剪，禁用命中和 Tab 导航。未设置模板时保留实时默认文本；Build 返回 null 时隐藏当前提示。Build 和布局异常在清理子树后传播，失败模板停止重试，替换模板可恢复。旧控件布局时重入替换新模板后再抛错，不会把新模板误标为失败。

源属性通知、源替换、禁用、隐藏、卸载及专业图族 Dispose 会清除旧提示控件的读取上下文和父级关系。可见性观察覆盖祖先的公开 `IsVisible` 变更，并在卸载时解除观察；恢复显示或启用后可恢复仍有效的读取。版本守卫覆盖 Build、继承 DataContext、逻辑/视觉附加和布局回调中的重入；不在 Render 内构建模板。已有父级的控件会被拒绝，原应用父级和 DataContext 保留。

默认文本呈现也覆盖祖先启用状态变化：祖先禁用或恢复会显式使图表重绘，不需要再次移动指针。导出同时拒绝图表正在构建数据、正在绘制或提示模板正在更新/布局时的重入，避免自定义绘图或数据构建回调把空白捕获误报为成功。两根各有默认绘制重入、数据构建重入、祖先禁用恢复重绘三个回归用例，共新增六项。

## 普通主题、高对比与导出约定

可选 `ChartsTheme` 的普通浅/深主题为两根提供动态 `ChartTooltipBackgroundBrush`、`ChartTooltipForegroundBrush`、`ChartTooltipBorderBrush`；显式 `HighContrastMode.Light / Dark` 提供匹配的白底黑字或黑底白字。`None` 恢复当前普通主题资源，局部笔刷值和绑定优先。模板子控件仍可显式覆盖自己的颜色。仅包含 Generic.axaml 提供普通主题，条件高对比样式由 `new ChartsTheme()` 提供。详见[主题包说明](../src/NT.AvaloniaControl.Charts.Themes/README.md)。

**行为变化：Engineering/Analysis 专业图族的 PNG/JPEG 同步及异步导出统一排除全部瞬态 Tooltip，包括默认实时文本和模板控件。** 永久数据标签、标注和图族图形沿现有导出路径处理。两个根的本地导出 render wrapper 执行此规则，不更改共享编码、取消、文件替换或流所有权管线。数据构建、绘制和模板更新/布局期间的重入捕获会被拒绝。Cartesian 保留既有模板与导出契约。

## 原生访问冲突：独立的两处生命周期修复 A/B

历史问题 `C7-NATIVE-LOD-AV-01` 为 Windows 原生折线导出访问冲突。本轮先用旧产品和原有独立宿主复现，再从旧冻结源码只加入两处修复：在普通和抽样折线的 `DrawGeometry` 返回后调用 `GC.KeepAlive(geometry)`，保持拥有原生路径的托管几何在该调用结束前存活。[Microsoft 的 GC.KeepAlive 文档](https://learn.microsoft.com/en-us/dotnet/api/system.gc.keepalive?view=net-10.0)说明了该方法维持对象到调用点的生命周期，以及托管引用结束而非托管代码仍在使用对象的场景。这是修复依据；它本身不能证明本次崩溃的完整原生因果链。

辅助核查与安装版本保持对应：Avalonia 12.0.3 的 [`DrawingContext.DrawGeometry`](https://github.com/AvaloniaUI/Avalonia/blob/12.0.3/src/Avalonia.Base/Media/DrawingContext.cs#L85-L89) 把 `geometry.PlatformImpl` 交给后端，[Skia 后端](https://github.com/AvaloniaUI/Avalonia/blob/12.0.3/src/Skia/Avalonia.Skia/DrawingContextImpl.cs#L264-L286) 再把填充/描边路径传给 `SKCanvas.DrawPath`。实际安装的 SkiaSharp 3.119.0.0 的该方法 IL 取得画布、路径和画笔 Handle，调用 `sk_canvas_draw_path` 后立即返回，没有调用后的托管生命周期屏障；原始 IL、程序集路径和 SHA 保留在 `installed-skia-il-v1.json`。这些代码与二进制观察支持保留路径所有者的修复方向，未据此推断已独立证明旧崩溃的具体终结器或指令级原因。

| 检查 | 旧产品控制组 | 仅两处 KeepAlive 的候选 |
| --- | --- | --- |
| 场景 | 同一已编译独立宿主、10 万原始点、默认运行时 JIT 配置、并发强制 GC | 同一宿主、场景、断言和运行时配置，16 轮 × 17 次尝试 |
| 结果 | 已完成前 83 次且通过；第 **84 次开始**后退出 `0xC0000005`，没有该次结束记录 | **272 / 272** 次尝试完成，通过 **3200 / 3200** 个 guard |
| 失败/图片证据 | 最后开始为 `id=83`、第 4 轮 `async-overview-exact-opaque-jpeg`；未产生该次图片或 producer 最终汇总 | 保存 **256 张** PNG/JPEG/窗口捕获图片；其余为协议中的预取消尝试 |
| GC 压力记录 | 失败开始时强制 GC 工作线程仍活动；原始 JSONL 保留 | **12017** 次强制回收开始，其中 **2573** 次开始于托管捕获区间；正常停止工作线程 |
| 替换范围 | 旧宿主和旧产品字节保持不变 | 宿主模块清单 **76 项仅核心 Charts DLL 1 项改变**，其余宿主和原生模块字节相同 |
| 源码范围 | 原 C7 集成 source-v1 | 复制 253 个产品构建输入，只改两个 renderer 文件；不包含新 Tooltip/主题修改，运行前后 source manifest 保持一致 |

协议保留原始选中项身份、源数量、峰谷、指定断点区域、图片格式/尺寸/透明度和导出不改变屏幕抽样计划等 guard。精确导出仍使用原始几何，没有通过隐式启用抽样、减少顶点或放宽断言规避问题。压力下记录的耗时只是诊断数据，不发布为性能资格。

证据根目录为 `<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c7-tooltips-export-stability-20261023/`：

- 控制组：`windows/baseline-warm-run-v1/`、`baseline-warm-failure-pins-v1.json`。
- 两处修复源码证明：`windows/fixed-fence-run-v2/source-provenance-before.json`。
- 宿主、程序集和原生模块对照：`windows/fixed-fence-run-v2/AB-provenance.json`。
- 272 次结果及 guard：`windows/fixed-fence-run-v2/producer/results.json`、逐次 `events.jsonl` 和 `attempts/`。
- 图片：`windows/fixed-fence-run-v2/producer/images/`；进程收据与运行前后源码核对：同级 `results.json`。
- 初始 restore 运行器的多源参数错误保留在 `fixed-fence-run-v1`；v2 只改为外置 NuGet.Config，产品源不变，正常 NuGet 审计仍启用。

这些结果支持所述生命周期修复在该受控协议中的有效性。运行未覆写 JIT 选项，相关 DOTNET/COMPlus 环境值为空；**未用 ETW/反汇编独立证明 Tier1 晋升**。2573 只表示回收开始于托管捕获区间，没有声称精确重叠原生 `sk_canvas_draw_path` 指令。旧失败 PID 没有独立采集原生模块清单或崩溃 dump；同宿主先前成功进程的模块记录不替代失败 PID 证据。该受控 A/B 与下面的当前组合候选回归分别记录，均不等于所有原生路径永久无访问冲突，也不替代目标实机或长稳验收。

## 当前组合候选验证状态

当前候选为 `source-snapshot-v2.json`，固定 **1881 个文件**，manifest SHA256 为 `D9FAC82F5D193CA3297DF13B1469A84229806A3BCB41CA72B3AC2741C580269E`。Windows 与 Linux 各六个有序阶段（目录、基础回归、包消费者、专业 Tooltip、导出压力、API）均已全部通过，运行前后全部 1881 个输入与该快照一致。最终 `validation-final.json` 的 `batchPassed`、`fullNativeRegressionPassed` 为 true，平台失败检查为空；`milestoneComplete` 为 false。

| 项目 | Windows | Linux |
| --- | --- | --- |
| Headless 全量契约 | **2928 / 2928**，零失败/跳过 | **2928 / 2928**，零失败/跳过 |
| 其中 `SharedTooltipPresentationTests` | **59 / 59** | **59 / 59** |
| 完整原生 Demo 合成事件回归 | **4585 / 4585** | **4585 / 4585** |
| 原生 Usage 编译绑定与业务上下文 | **865 / 865** | **865 / 865** |
| 新隔离缓存功能包消费者 | **1047 / 1047**，无源码项目引用 | **1047 / 1047**，无源码项目引用 |
| 新隔离缓存主题包消费者 | **1081 / 1081**，无源码项目引用 | **1081 / 1081**，无源码项目引用 |
| 专业 Tooltip 原生专项 | **150 / 150**，7 个 probe | **150 / 150**，7 个 probe |
| 普通浅/深与高对比浅/深截图 | **4 张**，均为 **1440 × 900** | **4 张**，均为 **1440 × 900** |
| 两根禁用/默认/模板 × 同步 PNG/异步 PNG/JPEG | **18 个导出产物** | **18 个导出产物** |
| 当前候选原生导出压力 | **272 / 272** 次，producer **3200 / 3200**，独立 oracle **3232 / 3232** | **272 / 272** 次，producer **3200 / 3200**，独立 oracle **3232 / 3232** |
| API 元数据快照 | **500 个类型**（功能包 497 + 主题包 3），未与官方授权 runtime 比较 | **500 个类型**；规范化 API 与 Windows 相同 |
| 静态目录追踪 | **68 个控件**，**22 项变异自检**通过 | **68 个控件**，**22 项变异自检**通过 |

每平台 Tooltip 专项保留四主题下真实控件、原始数据/聚合成员、业务 VM 和动态资源、控件复用/布局边界、INPC/换源/祖先禁用隐藏/卸载恢复，以及全部 18 个导出状态的证据。导出 PNG 经独立解码核对默认/模板与禁用状态的瞬态排除；JPEG 记录格式、尺寸及 producer 契约，不声称已完成独立 JPEG 视觉质量比较。窗口使用原生 backend 与合成事件，截图为渲染捕获；这些结果没有物理输入或合成器实际 present 的资格含义。

当前组合候选的压力另记录 Windows **12171** 次强制回收开始，其中 **2587** 次开始于托管捕获区间；Linux 分别为 **14006 / 2278**。每平台保存 **256 张**图片，**240 个** PNG 文件经独立解码校验，工作线程正常停止，默认 JIT 配置与执行输入保持不变。这些数字与前面的两处修复 A/B 是不同运行，不互相代填。独立 oracle 的 **17 / 17** 个变异测试通过，包含缺失、null、错误对象、空、截断、超量等汇总数组的拒绝；门禁不会将不完整 producer 清单当作通过。

正式证据分别在 `windows/run-v2/`、`linux/run-v2/`：基础回归 `verification/functional-v1/results.json` 与测试 XML；目录 `catalog.json`；包消费者 `consumers/*/results.json` 和各消费者报告；专业 Tooltip `professional-tooltip/smoke.json` 与同目录截图/导出；压力 `export-stability/` 的 producer、独立 oracle、输入前后 manifest 和进程收据；API `api/`。证据根的最终汇总为 `validation-final.json`，SHA256 `E129DAE9689DAE61C1FD9FE838C66801E274793784D4CA87233E5B78FB71A144`；双平台规范化 API SHA 为 `10E9500CE8D5569BB2AC2424D66FEA2A9E88BBED61EE41D2C9DEC85E90C8A6E3`。仓库根目录上同期无关 PDF/TreeDataGrid 的后续编辑不作为冻结候选的运行输入，最终文档与报告改动单独记录。

本批运行环境为 Windows x64 build 26200，以及 Ubuntu 26.04 x64 WSLg；均为 .NET 10.0.13、Avalonia 12.0.3。Linux 结果是该 WSLg 原生窗口/backend 的软件验证，不能外推为计划中的 Ubuntu 22.04/24.04 物理桌面、其他发行版、GPU 或真实输入资格。

首次正式 `windows/run-v1/` 因同时修改中的 PDF Demo 缺少 `Avalonia.Rendering.Composition` import，在完整测试运行前以 `CS0103 ElementComposition` 编译失败；失败结果与日志保留，未记为 Charts 运行通过。v2 的显式修订清单记录八个文件：两个 Tooltip 根和新增六项回归所在测试文件；oracle 的汇总完整性守卫及变异测试；Usage README、solution 注册，以及 PDF 所有者的一行 import 修正。其父快照 v1 SHA 为 `EF40338F29F03B78501EAC8F1067A2676287C977F76A202A68129753A5E3F9C9`，修订前后 SHA 保留在 v2 manifest。

开发期历史证据继续保留：最初 Windows Release 新 Tooltip **49 / 49**，连同旧 C7 相关用例 **92 / 92**，另外九类相关生命周期 **228 / 228**；随后加入两根主题资源/绑定四项及上述六项边界回归，`windows/tooltip-edge-development-v1/` 的定向编译零警告/错误、测试 **59 / 59**。当前验收以表内冻结候选全量结果为准。开发期 restore 的沙箱 NU1900 与两次不可用 Avalonia API 编译失败均保留，修正后正常网络审计还原通过；未关闭审计或降级断言。

## 自动化门禁与最终源码核对

[导出稳定性门禁](../scripts/Test-ChartsExportStability.ps1)使用提交的依赖锁，保留默认 JIT、并发强制 GC 和固定 16 轮场景；每次尝试开始即落盘。原生进程异常退出后仍执行[独立 PNG/协议校验器](../scripts/charts_export_stability_oracle.py)，保存未完成记录及失败图片。CI 配置增加 Windows 2025 / Ubuntu 24.04 两个作业，Linux 使用 Xvfb，成功和失败都上传证据；本轮执行的是本地 Windows/WSLg，远端 CI 尚未执行。YAML 解析与配置静态记录为证据根 `ci-static-validation-v1.json`。

最终整理仅将 `AnalysisChartBase.cs` 和 `AnalysisChartBase.Export.cs` 恢复为原有 CRLF，减少无意义的整文件差异。584 项当前 Charts 构建/测试/Usage/Demo/门禁输入中，582 项与已测快照字节相同，另外两项规范化换行后与已测源码逐字节相同，未改变源码 token。该格式核对收据 `post-validation-formatting-v1.json` 保留两种 SHA；原始已测快照和 `validation-final.json` 未改写。仓库[验证汇总](charts-c7-tooltips-export-stability-validation.json)附加该收据，SHA 为 `B0D9A2C9730A60FBACE10E0897AB9E5698BAF59901C17416A91315BA8AD78A2C`；[API 快照](charts-c7-tooltips-export-stability-api.txt)记录本批公共契约，尚未作最终发布冻结。

## 剩余工作

| 编号 | 状态与下一步 |
| --- | --- |
| C7-01 | 专业 Tooltip 及本批共享集成已通过双平台完整回归和压力。图例/数据标签模板、PlotAreaContent、专业 Trackball、范围选择、多图同步、完整动画/导出组合仍需逐图族推进。 |
| C7-NATIVE-LOD-AV-01 | 已在指定受控协议下修复：受控 A/B、Windows/Linux 当前完整原生回归及压力通过。这是指定候选和协议的证据，不是所有原生路径的永久担保，目标实机、长稳及新场景门禁继续保留。 |
| C7-CONTRAST-COMPOSITE-01 | 渐变、多层透明叠加与连续色阶的最终像素对比继续专项；公共调色板不自动保证任意自定义颜色。 |
| C7-02 | 指定 Windows/Linux 发行版与实际桌面/backend、GPU/软件、DPI/跨屏、字体/文化、物理输入及读屏矩阵待验。WSLg 和 RenderTargetBitmap 证据不等于实际屏幕 present 或目标 Linux 实机资格。 |
| C7-03 | 官方授权 runtime 同条件性能对照、绝对门槛、C4 正式性能门禁及远端 CI 仍待完成。当前压力耗时不构成 SLA。 |
| C7-04 | 8 小时持续更新、多图负载，至少 100 轮创建/导航/换源/主题/关闭，以及内存/句柄趋势和异常恢复待验。272 次导出尝试不能换算为该阶段已通过。 |
| C7-05 | 本批双平台新隔离缓存功能包/主题包消费者已通过；自包含、裁剪/AOT、升级消费者、大小写/资源及最终支持的发布方式仍需验收。 |
| C7-06 | 最终 API/主题快照、迁移、默认值/线程/所有权、性能条件、真实平台支持和维护策略尚未冻结。 |
| 目录与专业差异 | AK15 Stream graph/Theme river、AN16 授权 runtime 核验、Contour 抗锯齿细缝、专业 API/算法和历史平台差异继续保留。 |

百万点命中尾延迟与 `A11Y-NATIVE-01` 原生选中项枚举按用户决定后置，本批未将其标为解决。后续顺序继续依据[总开发计划](charts-development-plan.md)：C7-01 集成 → 双平台实机与辅助工具 → 同质量性能对照和长期稳定性 → API、文档和发行收口。
