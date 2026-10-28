# Charts C7-01：跨图族主题与集成追踪

执行日期：2026-10-23（香港时间）。本轮交付 C7-01 的第一批集成：完整具体控件目录、跨图族主题、实色内标签可读性，以及选择、换源、卸载重挂载的组合回归。**C7-01 仍在进行，C7 整阶段尚未完成。**

## 本轮变化

`ChartBase.HighContrastMode` 统一提供 `None / Light / Dark`。可选 `ChartsTheme` 的条件样式覆盖 Cartesian、Polar、Circular、Gauge、Sparkline、Analysis、Engineering 七个主题根及 68 个具体派生控件。Cartesian 原有静态属性字段和 CLR 访问器保留，并指向同一个属性；注册所有者扩展为 ChartBase。局部值、业务绑定和显式系列笔刷仍优先，`None` 恢复普通明暗主题。仅功能包不自动安装这些条件样式。

普通主题下同样改进实色内标签：Circular 的 Inside 标签、Funnel/Pyramid 标签，以及 Mosaic 等 Comparison 中心标签，根据实际不透明实色填充选择黑/白文字。标题、外标签及明确的 `AnalysisMarkInfo.LabelBrush` 保留原有设置。Comparison 的标签背景映射按当前帧线性建立，缓存键用弱引用，换源后无需第二次绘制也能释放旧业务项。

地图标签根据实色区域、填充 alpha、笔刷/图层 opacity 与实色图表/祖先背景合成后选择黑/白文字；无数据区域及气泡标签也使用相同规则。标题、图例仍使用 Foreground。

这是共享主题属性与指定标签场景的集成。连续色阶、局部显式颜色、渐变和多个半透明图层的最终合成，不由公共调色板自动保证对比度；不声明所有图形达到 7:1，也不代表原生无障碍验收。

## Demo、AXAML 与追踪入口

Demo 导航：**Charts → 交互与呈现 → 跨图族集成**。八个面板使用同一组可观察业务记录，演示统一预设、更新数据、复位、原始选择保持及浅色地图标签。独立入口：

```powershell
dotnet run --project samples/NT.AvaloniaControl.Demo -- --charts-smoke --charts-c7-integration-only --smoke-output artifacts/c7-native.json --smoke-image artifacts/c7.png
dotnet run --project samples/Charts.Usage -- --smoke --smoke-output artifacts/c7-usage.json
python scripts/verify_charts_integration_matrix.py --self-test --output artifacts/c7-catalog.json
```

Usage 的“C7 集成”使用真实编译绑定，将一个 ViewModel 属性绑定七个主题根；包含 `BeeswarmPlotChart`、`StripPlotChart` 别名。验证通过源类型的明确配置区分功能包和主题包，不以 Window 资源查询推断主题是否安装。这些源文件也由独立包消费者复制执行。

[机器矩阵](charts-c7-integration-matrix.json)逐项关联 68 个具体 ChartBase 控件的源码、测试、Demo、Usage、官方功能族编号和独立包消费调用链。FinancialChart、ChartRangeSelector 单独登记，系列、模式和别名不混为官方类型数。68 个控件有直接测试与 Usage 引用；Demo 为 65 个直接引用、3 个别名通过父类样例覆盖，已明确标记。静态链接不等于运行验收或官方 API 等价。

检查器验证继承目录、精确源码锚点、消费者复制文件与执行调用链，识别限定名及 `global::` 基类，附带 22 项错误注入自检。CI 新增独立目录检查任务并保留失败产物；本地运行不代表远端 CI 已执行。

## Windows / Linux 验证

本轮定向软件检查通过，完整原生回归仍有失败。[结构化结果](charts-c7-integration-validation.json)保留逐项状态、源码/程序集/包哈希、命令、原始报告及失败记录；[API 快照](charts-c7-integration-api.txt)为元数据采集，不执行官方授权 runtime。

| 检查 | Windows 本地 | Ubuntu 26.04 / WSL2 / WSLg |
| --- | --- | --- |
| Charts 全量测试 | 2869 / 2869 | 2869 / 2869 |
| C7 原生八图族看板 | 51 / 51 | 51 / 51 |
| 修正后的原生 Usage | 848 / 848，含 32 项跨族颜色断言 | 848 / 848，含同样 32 项断言 |
| 功能包独立消费者 | 1032 / 1032 | 1032 / 1032 |
| 主题包独立消费者 | 1064 / 1064 | 1064 / 1064 |
| 窄窗 / 恢复宽窗专项 | 6 / 6 | 6 / 6 |
| 静态目录 / 错误注入自检 | 68 控件 / 22 项 | 68 控件 / 22 项 |
| 公开类型 API 元数据 | 核心 496 + 主题 3 | 核心 496 + 主题 3 |
| C7 四种主题 PNG | 4 张，1440×800 | 4 张，1440×800 |
| 完整 Demo | **失败**：旧 LOD 导出访问冲突 | **4430 / 4431**：旧窄窗假设失败；定向修复通过，未重标整批通过 |

产品使用冻结的 **source-v1，1809 文件**（SHA256 `BBADE495346AE407E6B69F5BBB54DB706FE2D51ED0FF37F162EC5531BE06816B`）。验证样例单独冻结为 source-v3（1809 文件，`5156FA207019355F7A80A8CCE0B25C567C8B04C9C313C21B8A52201302F23701`）：唯一运行代码变化是 Usage 检查不再通过 Window 资源查找跳过颜色断言；Charts 功能/主题/测试源码保持一致。修正后的原生 Usage 及两类消费者从新缓存使用已测产品的真实 nupkg，执行 DLL 哈希与原测试产品一致，没有产品源码 ProjectReference。

响应式 Demo 测试采用 **source-v4，1811 文件**（`6C6983C288E208A6966399094CC5EDA88707F8DFD50C96AE08E1FEBDE7D4D442`），产品 Charts 源码仍一致。原检查以 1080 DIP 窗口隐含扣除旧侧栏宽度，改版后的整页宽度实际为 1080，合法处于 1050 阈值之上。现明确设为 960、等待原生尺寸生效，保留图宽 >250、卡片纵排及恢复三列断言；三阶段实际尺寸写入 `responsiveLayoutProbes`。两平台均为 1440 → 960 → 1440，三列 → 一列 → 三列。原失败报告完整保留，未重跑并宣称整批通过。

证据根目录：`<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c7-integration-20261023/`。

- Windows 截图：`windows/focused-v1/c7-c7-integration-{normal-light,normal-dark,hc-light,hc-dark}.png`。
- Linux 截图：`linux/run-v1/verification/functional-v1/charts-light-c7-integration-{normal-light,normal-dark,hc-light,hc-dark}.png`。
- 最新包 / 原生 Usage：`{windows,linux}/amended-v1/`；响应式专项：`{windows,linux}/responsive-v1/`。
- 完整原生失败：`{windows,linux}/run-v1/verification/functional-v1/`；Windows `charts-native.stderr.log` 保存 `0xC0000005` 与 Skia 路径栈。

Windows 全量 Demo 的旧 LOD 崩溃未修复。Linux 完整 Demo 仅旧响应式断言失败，单独修复已通过。开发及证据入口的其他失败也未删除：资源检测漏断言、Windows 汇总误要求 6 个而实际计划 5 个阶段、Linux API 输出与进程日志目录冲突，以及 source-v2 因并行 PDF 文档变更而未接受的冻结；修复和前后来源均单独登记。没有关闭 NuGet 审计或降低测试断言。

图片来自已显示原生窗口的 RenderTargetBitmap，已检查尺寸、文件哈希、明暗及高对比画面。字体平台差异允许；没有将两平台像素完全相等作为条件。这些证据不认证实际屏幕 present、GPU、触摸/触控板、DPI 跨屏、UIA/AT-SPI、目标 Linux 实机或 8 小时长稳；本轮也未执行官方性能对照、C4 正式性能门禁或远端 CI。

## 剩余工作

| 编号 | 状态与下一步 |
| --- | --- |
| C7-01 | 继续逐图族补齐共享图例/数据标签/提示模板、PlotAreaContent、专业 Trackball、范围选择、多图同步及完整导出/动画组合；当前各基础图族的单独交付不代表这些交叉能力已统一。 |
| C7-NATIVE-LOD-AV-01 | Windows 完整 Demo 在旧 LOD PNG 导出再次出现 `0xC0000005`；必须保留失败记录，不以本轮定向通过关闭该问题。 |
| C7-CONTRAST-COMPOSITE-01 | 渐变、透明重叠及连续色阶的最终像素对比仍需专项，当前仅保证上文明确的实色场景。 |
| C7-02 | 指定 Windows / Ubuntu 22.04、24.04 等实际桌面、GPU/软件、DPI/跨屏、物理输入和读屏资格待验；WSLg 不是目标发行版实机认证。 |
| C7-03 | 官方授权 runtime 同条件性能对照与 C4 正式性能门禁待完成；本轮不发布延迟、吞吐或资源 SLA。 |
| C7-04 | 8 小时持续更新及至少 100 轮完整生命周期长稳、句柄/内存趋势待验。 |
| C7-05 / C7-06 | 本轮仅验证新隔离缓存的常规 Release 包消费；裁剪、AOT、自包含、升级、最终 API/主题冻结和发布验收仍待完成。 |
| 官方目录映射 | AK15 Stream graph/Theme river、AN16 参考 runtime 专项及历史专业差异继续按官方矩阵跟踪，不以目录链接关闭。 |

百万点命中尾延迟和 `A11Y-NATIVE-01` 原生选中项枚举按用户决定后置；本轮未将它们标为解决。Contour 抗锯齿细缝、历史平台和专业 API 差异维持原有记录。

文档发布后已按原文件格式恢复少数文件的换行，避免引入整文件格式差异；最终检查确认运行代码清单及规范化文本与对应验证快照逐字一致，仅记录中的换行字节不同。C# 多行原始/逐字字符串不纳入这一格式修复。结构化结果的 `finalWorkingTreeAudit` 保留逐组核对和格式记录哈希。

## 后续批次

专业业务 Tooltip、原生绘图资源生存期修复及导出稳定性门禁的后续实现与对应验证，见 [C7 第二批报告](charts-c7-tooltips-export-stability.md)。现行源码在这些范围内的资格判断以该报告为准；本报告当时的失败、平台条件和未覆盖项继续保留为历史证据。
