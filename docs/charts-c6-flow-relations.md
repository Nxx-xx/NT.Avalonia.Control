# Charts C6-02：流向和关系

执行日期：2026-10-22（香港时间）。同一冻结候选 `c6-flow-relations-source-v3` 在 Windows 本地原生合成窗口与 Ubuntu26.04 WSL2/WSLg 完成软件验证。四个数值布局引擎覆盖九个公开目录条目：Sankey、Alluvial、Chord、Network、Force-directed、Arc、FlowChart，以及 Process 和 Mindmap 两种 FlowChart 组合。Demo 九面板、编译绑定 Usage、两类独立包消费者均已接入。下一主项 **C6-03 地理图**；完整 C6、官方默认/API/二进制与许可运行时等价仍开放。

## 数据、布局与交互契约

`GraphNode`、`GraphEdge` 与 `ChartRelations.Snapshot` 拥有不可变数值输入，节点与边分别保留原始 `Index`/`SourceIndex`、ID、原始 Source、端点和权重。节点ID必须非null且唯一，显式边ID在边域内唯一；无ID边用原始来源引用跟踪，平行边、自环和反向边不被聚合成一条。`RelationGlyphKey` 区分 Node/Edge，避免相同业务ID跨域冲突。重复/无效ID、缺失端点、负值/非有限权重、汇总溢出、无效归一化位置/尺寸及预算超限显式拒绝；控件公开错误并在数据修复后恢复。

| 目录 | 本地实现与数值规则 |
| --- | --- |
| HF16 Sankey | SCC 压缩后分层，有限次加权重心排序；所有正流量带共享同一个宽度/原始权重比例。存在正流量时节点容量为 max(输入,输出)；全图边权重均为0时按 GraphNode.Weight 比例回退，节点权重也全0则用单位容量均分。循环/自环/回边走外侧返回路径。 |
| HF01 Alluvial | 复用流量引擎，显式非负 Stage 排序压缩为列；缺失 Stage 使用自动 SCC 层级，原始节点与边身份不因列排序改变。 |
| HF03 Chord | 扇区质量为输入+输出，自环计两端；原始边分别分配源/目标端口。全部边为0时用节点权重分配扇区，节点权重也全0时均分；有效间隔受节点数约束。 |
| HF12 Network | 共享原始图数据，稳定圆周布局；显式归一化中心固定，尺寸受节点数约束，边宽为可读性尺度。 |
| HF08 Force-directed | 固定种子和有界迭代，保留显式固定节点；进行有界矩形碰撞分离，必要时对全部未固定节点使用确定性网格回退，公开迭代、碰撞、位移、回退及收敛结果。 |
| HF02 Arc | 原始节点顺序构成线性轴，显式位置仍固定；平行、反向和自环分别保留连接路径及原始身份。 |
| HF07 FlowChart | 显式归一化中心及尺寸，未指定的位置可自动排布；矩形、圆角矩形、椭圆、菱形、六边形，带标签/箭头和命名节点分组。 |
| HF14 Process | FlowChart 自动有向层级模式，支持判断/步骤形状、返回边、边标签和分组；与基础 FlowChart 使用同一契约。 |
| HF11 Mindmap | FlowChart 确定性双侧生成森林，根主题居中、子主题分散；循环和交叉边仍按原始边保留，不把任意图当严格树拒绝。 |

源/布局上限为2000节点、10000边；力导向最多400节点、300次迭代，交叉排序最多32轮。内部同时有正输入/输出且相对差大于1e−10的节点进入流量不平衡列表；源/汇为外部边界，严格守恒选项可拒绝内部不平衡。节点 Mark.Value 和 Member.Weight 表示衍生流量，Member.Value/Point.Y 保留原始节点权重；边的 Mark/Member 值均为原始边权重。

四个控件支持属性路径和普通INPC/INCC更新、独立节点/边选择、键盘浏览、滚轮缩放、中键平移和 PNG/JPEG 同步/异步导出。分组标题按缩放平移后的可见组范围测量并限宽，触及绘图区顶部时向内排布；完全离屏或碎片不足完整行高时省略，避免裁掉半行。`LayoutAsync` 在UI线程捕获输入及设置后将不可变数值布局交给后台；取消、换源、较新的布局代数和卸载防止过期结果发布。UI数据映射、快照捕获及原生几何仍可能同步执行；后台布局不是整条渲染管线的后台资格。

Sankey前向带在每个采样点保持归一化纵向流量截面，回流使用预留的完整走廊；节点宽/间距也是归一化单位，按上述流量或零流量回退规则计算后容量仍为0的节点保留最小显示桩。所有回流共用同一容量比例，最大回流厚度0.3、转向和节点侧面至少留0.025，防止粗自环折叠。极小正数权重的公开WeightScale溢出时饱和为double.MaxValue并报告IsWeightScaleSaturated，几何仍使用原始归一化比例；Network/Flow先求权重比再乘宽度。Flow按矩形分离间隙确定水平或垂直端口，回边使用面向外侧的短引线、六点走廊，前向平行边中段限定在端口间隙内。Network/Flow 使用至少2DIP的可读边宽，不将其像素宽度宣称为精确业务面积。Chord用有限分段多边形表示扇环与关系带。0权重边保留数值/快照身份但不绘制；存在正流量时，无关联质量节点的Chord扇区为0，仍保留在快照中。极小正比例可发生数值归零或低于屏幕分辨率，原值/身份仍保留。

Demo入口 **Charts → 流向和关系** 展示九个面板，可修改权重、追加节点、切换标签、缩放/平移、后台重排和取消；`--charts-relations-only` 提供专项入口。命名分组为节点包络，当前不等同任意 `FlowGroup` 显式范围/样式、节点图标/模板或每边独立箭头等完整官方扩展。

公开依据：[Sankey](https://docs.avaloniaui.net/controls/data-display/charts/hierarchy/sankey-chart)、[Alluvial](https://docs.avaloniaui.net/controls/data-display/charts/hierarchy/alluvial-chart)、[Chord](https://docs.avaloniaui.net/controls/data-display/charts/hierarchy/chord-diagram)、[Network](https://docs.avaloniaui.net/controls/data-display/charts/hierarchy/network-chart)、[Force-directed](https://docs.avaloniaui.net/controls/data-display/charts/hierarchy/force-directed-graph)、[Arc](https://docs.avaloniaui.net/controls/data-display/charts/hierarchy/arc-diagram-chart)、[FlowChart](https://docs.avaloniaui.net/controls/data-display/charts/hierarchy/flow-chart)、[Process](https://docs.avaloniaui.net/controls/data-display/charts/hierarchy/process-flow-chart)、[Mindmap](https://docs.avaloniaui.net/controls/data-display/charts/hierarchy/mindmap-chart)。本地公式、API和边界以上述契约为准，未宣称复制官方内部算法或默认行为。

![Windows流向和关系面板](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-flow-relations-20261022/windows/run-v4/verification/functional-v4/charts-light-relations-light-board-overview.png)

[Linux暗色面板](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-flow-relations-20261022/linux/run-v4/verification/functional-v4/charts-light-relations-dark-board-overview.png)。Windows/Linux浅暗四张正式总览已逐图检查，见[视觉复核](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-flow-relations-20261022/final-board-visual-review-v4.json)；PNG来自原生窗口RenderTargetBitmap或导出，不认证物理设备、compositor present或GPU完成。

## Windows 与 Linux 验证

| 项目 | Windows本地原生合成 | Ubuntu26.04 WSL2/WSLg |
| --- | --- | --- |
| Charts全套测试 | 2515/2515 | 2515/2515 |
| 完整原生Demo / 本轮关系检查 | 3044 / 400 | 3044 / 400 |
| 编译绑定Usage | 557 | 557 |
| 两类全新cache包消费者，各 | 756 | 756 |
| 独立固定数值 / 规模诊断 | 121 / 2 | 121 / 2 |
| 数值 / 像素 / 来源谓词 | 134745 / 247 / 15912 | 134745 / 247 / 15912 |
| 本轮PNG / 完整Demo PNG | 40 / 973 | 40 / 973 |
| Release警告 / 错误 | 0 / 0 | 0 / 0 |

两平台按完整测试名称保留全部2393项既有测试，新增122项；见[测试源文件与用例清单](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-flow-relations-20261022/development-test-inventory-v11.json)。两平台使用同一冻结的1680源文件、979声明runtime输入；最终Windows/Linux运行源及四份候选源副本逐SHA一致，运行前后源与已加载程序集保持。每平台九个监督阶段串行：完整Verify-Charts IncludeC1、两类包消费、API、benchmark restore/build/run、独立数值、像素与来源检查，保留PID、退出码、超时及完整日志。不拼接不同候选的平台结果。

项目cache初始为空，两类包消费者各使用新cache；只按SHA复用63个非产品项目依赖和14个非产品消费者依赖，没有复用历史Charts包/DLL，没有关闭NuGet审计或降低警告。Linux使用已有私有SDK、原生库和字体，没有安装系统包。环境及边界见[Windows](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-flow-relations-20261022/windows/run-v4/verification/functional-v4/environment.json)、[Linux](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-flow-relations-20261022/linux/run-v4/verification/functional-v4/environment.json)、[WSLg](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-flow-relations-20261022/linux/run-v4/wslg-environment.json)。请求X11/XWayland不等同实际应用backend/renderer/GPU已记录；物理Linux、原生Wayland及多发行版仍开放。

首轮专项benchmark和native图像前封存固定输入、数学约束、abs1e−8/rel1e−9容差及像素协议。静态审查期间数值协议保留v1～v6及每次修订哈希：原始67项加粗自环、极小正数、竖排、平行边、竖排循环和矩形间隙反例，最终121项，未删除既有输入。54项独立合成guard验证正常值与旧越界、粗自环折叠、下溢、错误端口及方向等反例；这些guard不加载产品程序集，不计作产品测试。46项工具/协议的首次观察前封存见[协议SHA清单](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-flow-relations-20261022/pre-observation-protocol-pins-v3.json)。独立Python从原始节点/边重算输入输出汇总、源/目标索引、共享流量宽度、Chord入出端口份额与不重叠、显式位置/阶段、分组包含和碰撞数量；规模场景完整检查输入和输出。固定规模调用记录时间和当前线程分配，未建立p50/p95、持续帧率、实际进程/原生/GPU内存或长期资格。

像素固定420×280DIP、plot(24,24,372,232)、隐藏标签/分组及两色源序调色板；九模式×两主题×native/export96为36张专图，另Chord144DPI与Sankey192DPI，共38张。18对native/export逐RGBA零差异并保存diff。6×6DIP数学ROI至少6个指定色像素，颜色容差24、alpha≥250；空白为0调色板像素。期望点由二节点解析布局、固定业务中心和Chord扇环推导，不读取产品glyph坐标作为期望。

来源检查覆盖56组按主题/模式/阶段排列的基线、视图、更新和总览记录；每个基线绘制项显式选择，两个独立业务目标命中，空白无命中。另18组后台布局记录逐项检查预取消、旧版本不发布、新版本与原始业务数据一致、显式取消及卸载后不发布和重新挂载恢复。节点原始业务权重与衍生流量分别检查，完整原始DTO逐字段比对。公共API290→316，旧290类型及全部公开/受保护声明保留，两平台声明一致；这是本地源码API快照，不是官方API/二进制等价资格。

## 规模诊断、保留失败与剩余问题

| 固定负载 | Windows elapsed / 当前线程分配 | Linux elapsed / 当前线程分配 |
| --- | --- | --- |
| perf-sankey-1000-1997 | 15.071 ms / 4532816 B | 15.368 ms / 4532816 B |
| perf-force-300-597 | 37.421 ms / 596912 B | 35.612 ms / 596912 B |

独立开放项 **C7-NATIVE-LOD-AV-01**：source-v3的Windows完整native运行曾在既有LOD原始数据导出中发生0xC0000005；44项专项回放与固定32轮GC实验均未复现，未证明根因、未添加未经证实的KeepAlive修复。最后一次Windows v4→Linux v4完整软件验证在运行前固定计划、保持同一source-v3和全部阈值，使用新cache与新输出；成功结果不能证明该AV已解决，保留为C7/native专项开放项。固定32轮GC实验不能排除race或证明实际native路径存活；探针启动时DllNotFoundException产生的dump只属于零轮次启动失败，不等同正式AV的dump。

计时包含一次Snapshot+数值布局；输入构造、JSON、哈希和独立检查在计时之外。本轮没有重跑C4-08正式性能门禁以及旧阶段独立数值/像素oracle；完整功能回归包含旧图族，旧性能结论不能认证新布局。力导向只发布有界求解终态，尚无持续物理仿真或逐迭代帧动画。规模输入未固定节点，包含真实有界迭代、碰撞和回退诊断；固定像素夹具使用显式中心，不能冒充自由力模拟像素验收。

首次定向控件运行的无效ID夹具暴露了共享Engineering映射在排队刷新中抛出异常的问题；关系图改用专用节点/边捕获入口，修正失败状态恢复与事件观察生命周期。原始[control-v1日志](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-flow-relations-20261022/development/control-v1/test.log)保留；后续同时加入旧Hexbin/Treemap与新Network源访问器重入回归，避免只覆盖新图族。

首次产品benchmark/native图像前的静态数学审查发现循环流量带越界、粗自环折叠、密集陡坡前向带横向溢出、极小权重的比例下溢及Flow竖排端口方向问题。产品修复与独立协议逐版扩充，所有旧输入和协议SHA保留，未按观测值回填期望或放宽容差。最终guard明确拒绝旧错误实现，见[数值协议修订](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-flow-relations-20261022/numeric-protocol-amendment-v6.json)与[54项合成guard](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-flow-relations-20261022/oracle-unit-v6/result.json)。

control-v6的111/112通过源于新增箭翼探针与后绘制反向边重叠：原命中遵循topmost规则，返回反向边是正确行为。夹具保留该点的反向原始边断言，并在未覆盖的箭翼增加正向边命中/选择检查；未为该失败修改产品命中顺序。[失败TRX](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-flow-relations-20261022/development/control-v6/relations.trx)与[control-v7的112/112](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-flow-relations-20261022/development/control-v7/relations.trx)均保留。

首个Usage构建请求在进程启动前遇到自动审批超时，原[超时记录](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-flow-relations-20261022/development/usage-build-v1-approval-timeout.txt)保留；单次重试构建成功且零警告/错误，不记作产品构建失败或未经执行的测试。

source-v1的Windows九阶段软件检查全部通过，但正式总览人工复核发现Process/Mindmap顶部组标题被plot裁去半行。该显示缺陷未以“功能通过”掩盖：保留Windows v1全部成功证据，并仅向已验证的Linux v1子进程PID911发送SIGTERM；监督链正常记录中断失败及源SHA前后证明，没有终止Linux发行版或无关进程。见[1292项旧证据及37工具SHA](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-flow-relations-20261022/superseded-candidate-v1-pins.json)和[Linux中断记录](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-flow-relations-20261022/linux/run-v1/operator-interruption-v1.json)。source-v2修复标题后重新执行；该候选后续Linux隐藏调度失败保留如下。最终接受使用新source-v3重新串行执行两个平台全部九阶段，没有混用候选结果。

组标题修复按可见交集和真实文字行高限宽/定位，碎片不足完整行则省略；新增7项直接检查实际GlyphRun绘制边界。control-v8的新夹具首次编译误用了Bounds属性，改为GetBounds()后[control-v9的119/119](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-flow-relations-20261022/development/control-v9/relations.trx)通过，包含118项新测试与1项旧测试。首次v2运行前的只读工具审查还修正了新Linux启动模板的两个默认版本，改为v2；未修改v1工具、验收协议或任何阈值。

Windows source-v2的九阶段全部通过，Linux source-v2在2511项完整测试中2509通过、2项既有MultiChartScheduler隐藏工作断言失败，尚未进入native/consumer/oracle。[失败运行](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-flow-relations-20261022/linux/run-v2/formal-run-v2.json)和[1299项v2证据及39工具SHA](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-flow-relations-20261022/superseded-candidate-v2-pins.json)完整保留。相关42个CartesianChart/调度器/原测试文件与C601字节一致，但受控测试在改产品前稳定复现真实缺口：MediaContext.Render→CompositingRenderer.UpdateCore→CartesianChart.Render→GetScene→BuildScene→BuildPreparedData→Getter，在活动状态为false时仍使准备次数1→2、getter6→8。[受控修复前TRX](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-flow-relations-20261022/development/hidden-admission-before-v1/hidden-admission.trx)保留。仅在attached且inactive的普通Render增加准入保护，排除_exportInProgress与_samplingExport作用域，保留detached显式绘制、隐藏图表主动导出和GetScene公开诊断语义。原断言不变，修复后受控62/62、最终组合181/181通过；新增4项覆盖直接hiddenRender、实际compositorFrame、hidden主动导出与其后无粘连、detached绘制。该项是产品修复，不将失败称为偶发或以重试代替。

最终组合TRX包含20项既有BackgroundLayout字符串参数用例。TRX使用原始引号，而既有xUnit XML名称含转义引号；原始逐字比较草案误计为新用例，已保留该草案，并仅在转义后的名称精确命中既有2393身份集合时适配日志格式。[最终身份清单](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-flow-relations-20261022/development-test-inventory-v11.json)记录20项一一映射、122项真实新身份和59项既有身份；完整正式运行仍逐名保留全部2393旧用例并要求恰好新增122项，未以数量替代身份检查。

source-v3的Windows2515项完整headless全部通过，但完整native在既有LOD首次overview原始数据导出发生fatal0xC0000005，最后完整图为gap sampled2x；该原始失败的[782项证据及42工具SHA](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-flow-relations-20261022/candidate-v3-failure-pins-v1.json)保留，Linux v3未启动。C601与C602-v1/v2此前全部34张LOD PNG及8项overview导出均完成，相关6个renderer/cache/export/sampling/smoke源与4个Skia/Avalonia DLL库存签名相同；库存不证明实际加载路径或根因。[诊断结论](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-flow-relations-20261022/development/lod-native-diagnostic-conclusion-v1.json)记录同candidate专项44/44通过、固定32轮100000顶点GC实验无AV，但不能排除race、不能证明native路径存活；没有采用未经证实的KeepAlive修复，没有改产品。探针startup DllNotFoundException发生在0轮实验，startup dump不是正式AV dump。[最后有界计划](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-flow-relations-20261022/formal-plan-v4.json)在运行前固定同candidate3仅一次Windows v4→Linux v4完整验证，新cache、新输出、Windows新source clone无旧bin/obj，任一失败即停止且不再重试。最终单次软件通过不视为AV已修，C7-NATIVE-LOD-AV-01作为独立开放项保留。

双平台正式产品九阶段完成后，只读collector-v4错误地将Linux相对source root的XML路径按当前目录解析；原sealed工具和异常记录保留，collector-v5仅修正解析并强制最终XML精确位置。该次collector随后因xUnit将Windows参数显示为∞而Linux显示为Infinity，记录false身份检查；旧false结果保留。[路径修订receipt](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-flow-relations-20261022/collector-path-amendment-v5.json)和[31项显示名逐项对照](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-flow-relations-20261022/collector-test-identity-amendment-v6.json)记录30项既有+1项新用例的一一映射，核对type/method和相同冻结源SHA；15项XML行号逐字段相等，16项两平台均未输出源行号，另从同SHA测试文件唯一定位声明行并明确标为派生。规范后仍要求完整2393+122身份精确并集、零重名、全部Pass；[6项独立反例guard](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-flow-relations-20261022/collector-identity-guards-v6.json)拒绝错误方法/行号、非Pass、重复和未批准拼写。该修订不改产品、输入、oracle或阈值，没有因此重跑任何产品阶段，原formal-plan-v4的49个SHA工具封存保持。

元数据最初草案在首次产品图像前按已确认公共契约修订为v2：图布局状态替代不存在的动画字段，并明确节点Member.Value保留原始权重、Member.Weight保留流量。原v1、修订说明及哈希保留；未改变输入、像素ROI、容差或性能门槛。开发失败和恢复见[开发证据索引](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-flow-relations-20261022/development-evidence-index-v4.json)；正式接受结果不抹去开发失败。

- 九目录条目基础功能已交付，四引擎+模式复用不计为九个独立基础引擎；C6-03～06及完整官方目录继续推进。
- 图规模、力迭代、交叉排序有明确上限；SCC返回路由、重心排序、有限分段Chord和力网格回退是本地有界策略，不是最优路由、最优能量或vendor算法等价。
- 任意FlowGroup显式范围/样式、节点图标/模板/逐项画刷、每边箭头开关、复杂标签避让、图编辑、完整专业共享交互仍开放；普通事件源及本轮重入夹具不代表任意自定义事件访问器均通过。
- 极小正比例和0质量的可绘制性按上述规则处理；回边可能重叠，不保证全图无边交叉。原始业务值和ID不被可视几何精度替代。
- 物理Windows/Linux、Wayland、ARM/国产Linux、多屏DPI、设备输入、读屏器、GPU/present、30分钟/8小时长稳与新图族正式性能待后续；远端GitHub Actions run未执行。
- 百万点hit p95和A11Y-NATIVE-01按用户决定后置；历史AX06、PixelProbe、动画时序、长路径/default-JIT及剩余统计/完整C5问题保留。

证据：[冻结源](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-flow-relations-20261022/source-snapshot-v3.json)、[正式计划](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-flow-relations-20261022/formal-plan-v4.json)、[验证清单](charts-c6-flow-relations-validation.json)、[API](charts-c6-flow-relations-api.txt)、[Windows九阶段](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-flow-relations-20261022/windows/run-v4/formal-run-v4.json)、[Linux九阶段](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-flow-relations-20261022/linux/run-v4/formal-run-v4.json)；[Windows数值](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-flow-relations-20261022/windows/run-v4/relations/independent-numeric-result.json)/[Linux数值](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-flow-relations-20261022/linux/run-v4/relations/independent-numeric-result.json)、[Windows像素](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-flow-relations-20261022/windows/run-v4/relations/independent-pixel-result.json)/[Linux像素](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-flow-relations-20261022/linux/run-v4/relations/independent-pixel-result.json)、[Windows来源](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-flow-relations-20261022/windows/run-v4/relations/independent-metadata-result.json)/[Linux来源](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-flow-relations-20261022/linux/run-v4/relations/independent-metadata-result.json)。
