# Charts C6 主项完成：排程、运营分析和目录补充

执行日期：2026-10-23（香港时间）。本轮完成 **C6-04、C6-05、C6-06 的基础功能主项**，接续已交付的 C6-01～03。Demo → Charts → **“C6 主项：排程、分析排名、层级与地图扩展”** 提供五组、26 个示例，包含数据更新、缩放平移、标量/集合切换、表格列切换、层级导航和多座位选择。下一主线按用户决定返回 **C5 补缺**。

## 交付范围

| 阶段 | 本轮交付 |
| --- | --- |
| C6-04 | Gantt、Swimlane、Event timeline、Spiral timeline；进度/里程碑、四类依赖及约束诊断、UTC ticks 和显式时区/DST规则、泳道重叠分轨、可见区间索引、标签碰撞处理、原始来源选择 |
| C6-05 | Bullet、KPI、Calendar heatmap、Matrix、Table、Waffle、Word cloud、Bump、Slope、Parliament、Theme river；数值/缺失/零值规则、标量及集合入口、真实 Table.Columns 业务行适配、排名端点标签避让、KPI 与单元文字明暗对比 |
| C6-06 | Flame graph、Icicle、Dendrogram、Indented tree、Radial tree；展开/折叠、钻取和既有变更动画；Marker/Line/Vector/Pie 地图叠加、分类色图例与 SeatMap 原始业务行多选；审计42条带C6标记的目录映射 |

42 是目录映射条目数，不是42个独立引擎。**C6基础图族主项已交付、C6-06目录审计已完成；原C6-06出口中的完整专业交叉集成与官方映射仍开放，不能视作全部出口验收完成。** AK15 的 Stream graph/Theme river 精确官方 API 映射仍明确保留 C7 核验，不能由 AK12 自动推断等价。详细入口、单位、边界、类型和代码示例见[库 README](../src/NT.AvaloniaControl.Charts/README.md)、[Usage](../samples/Charts.Usage/README.md)、[API 快照](charts-c6-main-completion-api.txt)。

排程最多100000条任务、200000依赖、2048标签；事件碰撞最多16个候选位置，螺旋最多64圈。运营输入通常最多20000条，单元/席位上限65536，词云256词、每词128个UTF-16字符及有界布局工作量。层级沿用原预算，地图叠加采用逻辑图元准入上限。以上是拒绝失控输入的资源约束，不是实际内存或交互性能承诺。

## Windows 和 Linux 验证

同一冻结 `c6-main-completion-source-v1` 共 **1743 个文件**，清单 SHA256 `9AF95ABB9E9AB30160415970B37EF7F101EC3F988BCBADAB6142F4803F3F16BD`。Windows 本地原生合成与 Ubuntu 26.04 WSL2/WSLg 使用相同源文件，各执行7个受监督阶段：基础构建/测试/完整 Demo/Usage、两类包消费者、API、benchmark 构建、数值记录、独立数值校验、独立原生图像/来源校验。

| 验证项目 | Windows | Linux / WSLg |
| --- | --- | --- |
| Charts 全套测试 | 2753 / 2753 | 2753 / 2753 |
| 完整 Demo / 本轮新增检查 | 4046 / 760 | 4046 / 760 |
| Usage / 两类隔离包消费者各 | 688 / 904 | 688 / 904 |
| 固定数值 / 规模场景 | 84 / 2 | 84 / 2 |
| 独立数值谓词 | 1927023 / 1927023 | 1927023 / 1927023 |
| 独立图像/来源谓词 | 50412 / 50412 | 50416 / 50416 |
| 本轮 PNG / 来源元数据行 | 116 / 166 | 116 / 166 |
| 同平台 native/export 96 DPI 完全相同的 RGBA 对 | 52 | 52 |

完整旧2617个测试身份、3286条Demo检查、584条Usage检查、各790条包消费者检查均保留且通过；新增136个测试、760条Demo、104条Usage、各114条消费者检查。API公共/保护类型快照由350增至459（含两个生成的XAML类型），旧声明保留，两平台类型/成员声明一致。元数据一致不构成官方API、二进制或默认行为等价证明。运行前后源与程序集稳定；隔离消费者重新打包当前Release产品并使用新cache，仅复用非产品依赖归档，未关闭NuGet审计或压制警告。

独立数值Python由原始输入重算，固定绝对/相对容差均2e-9；覆盖84个固定场景和100k排程/20k矩阵完整输出。原生校验核对26模式×两主题、固定布局点、三阶段元数据、原始来源/命中、尺寸/DPI、非空像素及52对RGBA一致性；来源谓词只对非空命中格执行，词云字体测量的跨平台差异使Linux净增4项，断言与阈值未改。它不是对全部图形的完整像素几何证明，也没有跨操作系统逐像素一致性的承诺。

| 单次规模诊断 | Windows elapsed / 线程分配 | Linux elapsed / 线程分配 |
| --- | --- | --- |
| schedule-100000 | 131.800 ms / 77227240 B | 150.163 ms / 77227272 B |
| matrix-20000 | 13.325 ms / 4849616 B | 11.029 ms / 4848696 B |

数字仅属于Charts专项；不等同整个仓库测试、物理设备、正式性能或长期可靠性认证。机器可读结果和全部日志/原始数值/证据SHA见[验证清单](charts-c6-main-completion-validation.json)。

## Demo 和截图

启动：`dotnet run --project samples/NT.AvaloniaControl.Demo`，选择 Charts 后展开 C6 主项。本批窗口检查参数为 `-- --charts-smoke --charts-c6-only --smoke-output <json> --smoke-image <png>`，完整运行入口为 `scripts/Verify-Charts.ps1`；独立新数值入口为 `Charts.Benchmarks --c6-completion-regression --output <file>`。完整受监督命令保存在本轮证据根的 `Run-C6Platform-v1.ps1`。

![Windows 暗色运营面板](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-main-completion-20261023/windows/run-v1/verification/functional-v1/charts-light-c6-dark-operations-a-overview.png)

截图目录：[Windows](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-main-completion-20261023/windows/run-v1/verification/functional-v1)、[Linux](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-main-completion-20261023/linux/run-v1/verification/functional-v1)。文件名前缀 `charts-light-c6-`；两主题、26模式各native/export，加2张高DPI及10张五组总览，共116张/平台。这些是原生合成窗口的 RenderTargetBitmap/导出图，不认证真实桌面 compositor present。

## 修复、保留失败和剩余问题

关闭 **C6-MAP-FIT-01**：无几何地图极小单点坐标现在获得实际可表示fit跨度；验证可见大小、投影和原始命中。关闭 **C6-MAP-OBSERVER-01**：换源/移除图层即时释放旧成员订阅，不再等下一次 GetMarks/render。对应极端值与生命周期测试包含在双平台完整测试中。

开发失败保留在证据根 `development/`：native-v1的隐藏事件标签Rect序列化产生NaN，改为明确导出有限bounds字段；native-v2有两条测试误把从子节点返回父节点当作直接回森林，修正为两步并增加父级检查。数值v1因局部变量重名编译失败；v2独立oracle有一项误把公开快照规范化后的空Ranges视为null，保留raw与初版失败，仅修正输入规范化解释，未改输入、产品算法或放宽容差。跨平台原生oracle-v2改用标准库PNG解码并比较RGBA，开发图与旧解码器结果一致，未降低像素要求。正式冻结候选两平台各只运行一次且全部通过。

- C6-01～06 主项基础功能已交付；完整官方 API、二进制、默认行为和性能等价未认证。AK15 Stream graph/Theme river 的精确官方 API 对应继续在 C7 核验。
- C5 七类统计缺项 Beeswarm、Contour、Mosaic、Parallel coordinates、Ridgeline、Strip、Violin，以及金融/技术指标等专业扩展仍开放；下一主线先补 C5。
- C7-NATIVE-LOD-AV-01：此前 LOD 原生导出 0xC0000005 根因未定位、未修复。本轮通过不能关闭此问题。
- 百万点命中尾延迟和 A11Y-NATIVE-01 按用户决定后置；物理 Windows/Linux、GPU/compositor present、触摸设备、读屏和长稳未认证。
- 完整专业模板、PlotAreaContent、Trackball/范围联动/高级标注等跨图族集成仍需 C7 专项；未重跑 C4-08 正式性能门禁或旧阶段独立 oracle，未执行远端 CI。
- 排程显示约束诊断而不自动重排；泳道采用明确单位的数值区间。Dendrogram 展示已有树，不自动聚类。
- 运营图未覆盖官方全部嵌套 RankingsPath/布尔 ValuesPath 适配、Bullet 竖向、词云旋转/紧密排布；词云和密集端点标签可明确省略。Table 为只读绘制表，无编辑和虚拟化。Theme river 为 centered/zero 基线，无 wiggle。
- 地图曲线/弧采用最多 128 段近似，圆弧半径为 DIP 而非测地距离；瓦片、标签避让、专业样式/模板仍开放。暗色地图部分白色标签落在浅色区域，对比不足，列入 C7 主题改进。
- 100000 排程/20000 矩阵的计时与线程分配是单次诊断，不是 p95、持续帧率、进程/原生/GPU 内存或 SLA。

历史保护：本轮核对 122 个历史文档及 135 个上一轮根证据文件，SHA均未改变。仅新增本报告/验证/API，并更新六份滚动计划和状态文档；历史“下一项”与开放项文本代表当时状态，以最新摘要为准。
