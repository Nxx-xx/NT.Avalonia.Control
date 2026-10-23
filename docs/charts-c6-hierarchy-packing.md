# Charts C6-01：层级和打包

执行日期：2026-10-21（香港时间）。同一冻结候选 `c6-hierarchy-packing-source-v1` 在 Windows 本地原生合成窗口与 Ubuntu26.04 WSL2/WSLg 完成软件验证。交付 Treemap、旭日、组织树、气泡云、打包气泡和圆打包六个图族，包含展开/折叠、钻取/祖先路径、标签裁剪、原始节点选择、数据/视图/入场变更动画、Demo 与编译绑定 Usage/包消费者。下一主项 **C6-02 流向和关系**；完整 C6 和官方运行时/API/二进制等价仍开放。

## 数据和布局契约

- `HierarchyNode`、`HierarchySnapshot` 与 `ChartHierarchy` 提供有界、可取消的不可变数值布局。原始森林按 preorder 建立 `Index`，同时保留兄弟 `SourceIndex`、父索引、完整整数路径、深度、原始 `Source`、ID、标签和自身 `Value`。布局、折叠与钻取保留原始索引，不把显示序号当业务身份。
- **只有叶节点值参与聚合**，分支自身 Value 不再加到子项之和上，但仍必须有限且非负。叶值0保留数值条目并占0面积；组织树仍绘制正常节点。重复节点/循环、非有限/负值、汇总溢出和数值归零的正比例等条件明确拒绝；控件公开 `DataError`，清空过期画面，并可在源修复后恢复。
- Treemap 提供稳定源序的 Squarify 与交替方向 Slice。`Gap=0` 时每层子矩形完整分割父矩形，面积对应叶合计份额；祖先矩形与后代重叠，命中按实际顶层顺序。默认视觉间距会减少着色面积，不把有间距像素面积宣称为精确业务份额。
- Sunburst 每个相对深度使用等厚环；角度按子叶合计份额分配，支持内孔、起角、扫角。钻取将原始分支作为新的显示根，其原始路径与祖先导航保持。
- Organization 按终端节点的源序分配横向 lane，父节点位于后代终端范围中间，连接真实父子关系；支持横/纵方向、折叠、标签与导航。当前为内置节点绘制，不宣称已具备任意节点模板的完整官方功能。
- Bubble cloud 对可见终端按源序使用黄金角旋转方向，Packed bubble 按半径稳定降序；两者均保留原始输出顺序和统一的面积/值比例。Circle packing 自下而上嵌套，**终端圆面积共享值比例；祖先圆是包含子圆与6%径向留白的包络，其面积不代表聚合总值**。打包为最多16方向的确定性切线包络启发式，保证已验约束，不是最优密度或官方力模拟算法。
- `GroupPath` 与默认关闭的 `ShowLegend` 提供类别配色和有界页脚，颜色按完整源的叶类别顺序分配，钻取/折叠沿用。只读 `LegendItems` 保留全部可见终端类别、聚合值及原始 `Nodes`；视觉页脚最多100DIP/原plot三分之一、最多20项并提示溢出数。气泡的9:4:1大小图例为相对面积示意，不是绝对业务单位标尺。
- 源上限10000节点，根深度为0、最大深度64；打包子树最多2000输入节点，折叠隐藏的后代也计入预算。映射、快照、布局与更新可能在UI线程同步执行；本轮不声明后台/增量布局、无限规模或持续吞吐资格。

控件支持 `ItemsSource` 与属性路径、显式 `IdPath`、普通 INPC/INCC 嵌套更新、鼠标/键盘选择、滚轮缩放/中键平移及 PNG/JPEG 导出。双击或 Enter 钻取，Backspace 返回，+ / - 展开折叠；程序可调用 DrillDown/DrillUp/ResetDrill/Expand/Collapse。变更动画匹配稳定节点身份，支持时长、缓动、延迟、暂停、取消、重播及减少动态效果。父节点被子图完全覆盖时可显式选择/导航，不将其中心命中强行解释为父节点。

共享导出支持同步与异步；本轮新图族没有取得完整笛卡尔Trackball、多轴、范围选择、触摸捏合、滚动容器嵌套或原生无障碍桥接资格，这些专业共享交互与C7集成继续开放。基础类别/大小图例也不代表官方交互图例、像素、API或默认行为等价。

Demo入口 **Charts → 层级和打包** 展示六个面板，可展开/折叠、钻取、返回/恢复、更新叶值、追加团队、暂停/继续、取消/重播及切换标签/减少动态效果。`--charts-hierarchy-only` 提供专项验证入口。

公开依据：[Treemap](https://docs.avaloniaui.net/controls/data-display/charts/hierarchy/treemap-chart)、[Sunburst](https://docs.avaloniaui.net/controls/data-display/charts/hierarchy/sunburst-chart)、[Organization](https://docs.avaloniaui.net/controls/data-display/charts/hierarchy/organization-chart)、[Circle packing](https://docs.avaloniaui.net/controls/data-display/charts/hierarchy/circle-packing-chart)、[Bubble cloud](https://docs.avaloniaui.net/controls/data-display/charts/bubble/bubble-cloud-chart)、[Packed bubble](https://docs.avaloniaui.net/controls/data-display/charts/bubble/packed-bubble-chart)。本地布局公式、属性名称、默认值和边界以上述契约为准；不声明逐项复刻官方内部实现。

![Windows层级和打包面板](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-hierarchy-packing-20261021/windows/run-v1/verification/functional-v1/charts-light-hierarchy-light-board-overview.png)

[Linux暗色面板](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-hierarchy-packing-20261021/linux/run-v1/verification/functional-v1/charts-light-hierarchy-dark-board-overview.png)。PNG是原生窗口 RenderTargetBitmap 或导出，不能代表物理设备、compositor present 或 GPU 完成。

Windows与Linux正式浅色/暗色四张总览已逐图检查：六面板、祖先边界、组织标签、气泡类别/相对大小图例及嵌套关系完整，小区域标签按节点裁剪。见[最终四图人工视觉复核](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-hierarchy-packing-20261021/final-board-visual-review-v1.json)；该检查不等同物理设备或读屏器验收。

## Windows与Linux验证

| 项目 | Windows本地原生合成 | Ubuntu26.04 WSL2/WSLg |
| --- | --- | --- |
| Charts全套测试 | 2393/2393 | 2393/2393 |
| 完整原生Demo / 本轮层级检查 | 2644 / 344 | 2644 / 344 |
| 编译绑定Usage | 535 | 535 |
| 两类全新cache包消费者，各 | 726 | 726 |
| 独立固定数值 / 规模诊断 | 57 / 2 | 57 / 2 |
| 数值 / 像素 / 来源谓词 | 202108 / 346 / 13963 | 202108 / 346 / 13963 |
| 本轮PNG / 完整Demo PNG | 28 / 933 | 28 / 933 |
| Release警告 / 错误 | 0 / 0 | 0 / 0 |

两平台使用同一冻结的1650源文件、954声明runtime输入；Linux/Windows长路径与Windows短路径三份源逐SHA一致，运行前后源与已加载程序集保持。每平台九个监督阶段串行：完整Verify-Charts IncludeC1、两类包消费、API、benchmark restore/build/run、独立数值、像素与元数据。每阶段保留PID、退出码、超时和完整日志；不拼接不同候选结果。

实际SDK两平台均为10.0.402，PowerShell7.6.5、x64/16逻辑处理器。Windows系统内核10.0.26200（26200.9711），注册表产品名不足以确认营销发行名称；Linux为Ubuntu26.04、WSL2内核6.6.114.1、WSLg1.0.73，请求X11/XWayland。应用未记录实际backend/renderer/GPU，不据环境库存推断。见[Windows环境](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-hierarchy-packing-20261021/windows/run-v1/verification/functional-v1/environment.json)、[Linux环境](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-hierarchy-packing-20261021/linux/run-v1/verification/functional-v1/environment.json)和[WSLg边界](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-hierarchy-packing-20261021/linux/run-v1/wslg-environment.json)。

项目cache初始为空，两类消费者各用新cache；按SHA只读复用63个非产品项目依赖与14个非产品消费者依赖，没有复用历史Charts包/DLL，没有关闭NuGet审计或降低警告。Linux使用已有私有SDK、库和字体，没有安装系统包。该结果适用WSL2/WSLg合成输入的软件路径，不是物理Linux全平台认证。

首轮专项benchmark/native观察前封存数值输入、数学约束与容差(abs1e−8/rel1e−9)、10k/1k源公式和独立像素协议。Python直接从原始叶值重算面积/角度、原始父子/兄弟/路径、Slice、组织lane及连接；圆使用面积比例、全部兄弟对非重叠、祖先包含与有限边界约束。10000个Treemap源/矩形完整检查；1000气泡检查全部499500对；这些聚合约束和叶级谓词不冒充独立产品测试数。

像素固定420×280DIP、plot(24,24,372,232)、隐藏标签值、Gap0和七色原始preorder配色。六族×两主题×native/export96共24专图，另圆打包144DPI与Treemap192DPI共26；12对96DPI逐RGBA零差异并保留diff。6×6DIP数学ROI至少6个指定色像素，颜色容差24、alpha≥250，DPI按面积缩放；每张空白控制为0调色板像素。两气泡的ROI由9:4业务面积比、解析外切与圆包络推导；没有读取产品glyph位置作为期望。

元数据独立比较原始嵌套DTO、preorder/兄弟/父索引/路径、自身值与叶合计，核对每个可见节点的显式选择、独立业务ROI命中和空白。原始来源与聚合值分开，不把父圆面积当业务合计。公共API268→290，两平台逐声明一致，全部268个既有类型及公开/受保护成员保留；这是本地源码快照，不是官方API/二进制等价认证。

54组原始/视图/钻取/折叠/数据变更元数据之外，六族×两主题的12组真实窗口动画记录使用250ms时长、100ms延迟、60ms暂停保持和450ms完成等待，核对暂停、恢复、实际边界变化、完成、取消及减少动态效果。Organization的数值修改本身不改变节点布局，专项通过实际视图或结构变化触发动画；时间等待和RAF状态不认证物理呈现时刻、帧率或视觉流畅度。合成工具guard为数值17、像素12、来源15、动画8，不能与产品测试数相加。

## 规模诊断、保留失败与剩余问题

| 固定负载 | Windows elapsed / 当前线程分配 | Linux elapsed / 当前线程分配 |
| --- | --- | --- |
| perf-treemap-10000 | 12.681 ms / 5863960 B | 16.519 ms / 5864200 B |
| perf-packed-1000 | 25.652 ms / 700144 B | 21.742 ms / 700144 B |

计时为一次Snapshot+数值布局调用，源构造、JSON、输入SHA和独立检查在计时之外。没有p50/p95、UI持续帧率、实际进程/原生/GPU内存或长期资格。本轮未重跑C4-08正式性能入口及C5-01～05独立oracle；完整功能回归包含旧图族，但旧性能结论不能认证新布局。

开发审查修复了：重入getter/Refresh/换源后丢弃旧版本候选、事件订阅add回调换源/卸载后的意图重同步、退出节点不再进入公开命中/选择、深层扇环间隙上限、组织分支标签、已删除折叠身份清理、按最后发布状态合并动画通知、非有限分支值严格拒绝及无ID记录按原始引用追踪。根枚举和批量集合更新在进入通用映射前受10k限制；Treemap层级间距显示祖先边界，父圆间距受径向余量限制。各已测重入夹具不代表任意自定义事件访问器都已取得兼容资格。

开发失败的性质分开记录：首次Demo编译的CS9124捕获诊断已修正；v2组织树动画夹具只改值、不改节点几何却断言Running，后来改用实际视图变化触发，未把无变化动画伪装为运行。v4窄整环严格测试期望65个节点、实际39，是产品将backend圆弧Geometry.Bounds当成可绘制门槛导致小环丢失；修复为整圆/整环使用EllipseGeometry及差集，所有扇区按正的原始尺寸/环厚/扫角判断，严格65节点及逐节点原始身份命中断言保持。v4两条图例失败则来自夹具假定DrawEllipse一定记录为特定GeometryDrawing及未应用变换的Bounds；改为捕获实际DrawEllipse的Rect与Transform，仍严格要求3个页脚圆、9:4:1面积比和不参与选择，产品图例未据此修改。原始失败TRX及后续111/111开发通过记录均保留；正式两平台2393/2393完整测试在这些修改后运行。

开发原始失败与恢复保留于[开发证据索引](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-hierarchy-packing-20261021/development-evidence-index-v1.json)。独立数值工具的初版合成夹具按分区插入序输出而非原始preorder，两个positive guard失败；在首次专项benchmark/native观察前仅修正夹具枚举，原工具/失败保留，oracle及数学门槛不变。后续通过不抹去开发期失败；见[合成夹具恢复记录](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-hierarchy-packing-20261021/tool-fixture-recovery-v1.json)。

- 本轮六个用户指定图族已交付；公开目录中的Icicle、Dendrogram、Flame、Indented/Radial tree、Mindmap等仍需C6-06核验补齐，不能把六族通过写作完整层级目录或完整C6完成。
- 打包是有界启发式，父圆含空隙；正比例数值归零或打包圆无法区分时拒绝。有效极小正值仍可低于坐标端点或屏幕分辨率，例如StartAngle+SweepAngle与StartAngle舍入相等；Snapshot保留原值和身份，但可见区域、命中和标签不保证。后台/增量布局、最优密度、完整交互图例/任意组织节点模板和vendor特定尺寸钳制/默认值仍开放。
- 已测C601重入夹具不等于任意自定义事件add/remove访问器均获得资格，历史C505问题也未据此批量关闭。扇区标签为未旋转单行裁剪；动画点数限制计算旧帧与目标总数，减少动态效果由应用显式设置。完整C2共享专业交互集成、剩余统计/完整C5和官方许可运行时对照继续开放。
- 物理Windows/Linux、原生Wayland、ARM/国产Linux、多屏DPI、设备输入、GPU/present、读屏器和30分钟/8小时长稳待C7；远端GitHub Actions run未执行。
- 百万点hit p95与A11Y-NATIVE-01按用户决定后置；AX06、四份历史PixelProbe DLL、旧动画时序/长路径/default-JIT等历史问题保留。

证据：[冻结源](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-hierarchy-packing-20261021/source-snapshot-v1.json)、[正式计划](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-hierarchy-packing-20261021/formal-plan-v1.json)、[验证清单](charts-c6-hierarchy-packing-validation.json)、[API](charts-c6-hierarchy-packing-api.txt)、[Windows九阶段](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-hierarchy-packing-20261021/windows/run-v1/formal-run-v1.json)、[Linux九阶段](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-hierarchy-packing-20261021/linux/run-v1/formal-run-v1.json)；[Windows数值](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-hierarchy-packing-20261021/windows/run-v1/hierarchy/independent-numeric-result.json)/[Linux数值](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-hierarchy-packing-20261021/linux/run-v1/hierarchy/independent-numeric-result.json)、[Windows像素](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-hierarchy-packing-20261021/windows/run-v1/hierarchy/independent-pixel-result.json)/[Linux像素](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-hierarchy-packing-20261021/linux/run-v1/hierarchy/independent-pixel-result.json)、[Windows原始来源](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-hierarchy-packing-20261021/windows/run-v1/hierarchy/independent-metadata-result.json)/[Linux原始来源](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c6-hierarchy-packing-20261021/linux/run-v1/hierarchy/independent-metadata-result.json)。
