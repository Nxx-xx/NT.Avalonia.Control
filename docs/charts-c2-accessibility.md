# Charts C2-09：点级辅助语义、键盘浏览、摘要与高对比

执行日期：2026-10-17。当前折线、柱状和散点的本轮软件功能已实现，统一 Demo、编译绑定 Usage 及两种包消费者已接入。同一最终 v3 冻结源码在 Windows 与 Ubuntu 26.04 WSL2/WSLg 完成软件验证；原生点树、状态/动作、边界和分页另有实测。**完整原生选择容器门禁未通过，完整 native bridge 资格为 false**；C2 全阶段、实际读屏器、物理桌面和 C4 性能出口继续开放。

排期补充（2026-10-17）：用户决定“留到后续无障碍专项处理”。Windows UIA / Linux AT-SPI 原生选中项枚举桥接问题已转入[专项 A11Y-NATIVE-01](charts-development-plan.md#a11y-native-01)，不阻塞当前功能和下一阶段 C3。以下失败结果、冻结证据和未验证范围保持原身份；本补充只调整后续排期。

## 使用与行为

同时启用 `IsKeyboardNavigationEnabled` 和 `IsPointBrowsingEnabled` 可独立浏览数据点，选择关闭时也可使用。只读 `CurrentPoint` 保留真实业务对象、StableId、源索引及当前显示帧坐标，不以插值或抽样代表替换原始项。`NavigateToPoint(series,sourceIndex)` 允许程序显式定位当前视口内的有效点；本身不选择、不调用命令、不获取焦点。

| 输入 | 行为 |
| --- | --- |
| 左 / 右 | 当前系列按源顺序浏览有效且可见的点 |
| 上 / 下 | 前 / 后系列的相近源序号 |
| Home / End | 当前系列首 / 末有效可见点 |
| PageUp / PageDown | 按源序号跳过 64 行 |
| Ctrl+Home | 重置视图 |
| Space | 沿可取消的选择契约选择当前点 |
| Ctrl+Space | Multiple 模式下切换该点 |
| Enter | 独立于选择执行 PointInvokedCommand |
| Esc | 清除当前浏览点和读数，保留选择 |

不启用点浏览时保留原方向键选择与 Home 复位行为。浏览使用已发布场景和快照，不重新枚举数据源；每系列、每个搜索方向最多检查 4096 行，上下切换系列时可以先向前再反向搜索，并可继续尝试其他系列，因此不是整次按键合计 4096 行。无效点、缺失点、视口外点跳过，不自动平移/缩放至不可见数据。焦点离开、隐藏、禁用、移除和卸载清理当前点；回调里发起的新定位优先。当前点的虚线方框与读数进入同一帧导出。

```csharp
chart.IsKeyboardNavigationEnabled = true;
chart.IsPointBrowsingEnabled = true;
chart.SelectionMode = ChartSelectionMode.Multiple;
// chart.CurrentPoint 与 chart.AccessibilitySummaryText 可供编译绑定读取。
chart.HighContrastMode = ChartHighContrastMode.Dark;
```

`AccessibilitySummary` 是只读不可变摘要，`AccessibilitySummaryText` 是可绑定文字。包含各系列的总数、有效数、原始缺失数、无效数、选中数，以及主轴和附加轴当前范围；文字含当前浏览点。Missing 是填补前的原始缺失，Zero 等填补后的有效值可能同时计入 Valid，因此不能简单相加。布局前及未取得当前快照的隐藏系列明确标记 pending/unavailable，不伪装为已知零。计数按 SourceVersion 和已发布快照修订缓存，复用标准快照；仅在摘要读取、绑定或辅助节点请求后维护，不为无辅助消费者的普通绘制额外扫描源。

Valid/Invalid 按标准映射快照的有限坐标和填补状态分类，Missing 也可能计入 Invalid；Valid 不是通过轴域、断轴及视口裁剪后可操作的点数。动画中 Point/Name/Value 表示当前显示帧坐标，Item/StableId 仍指向真实目标业务项，瞬时读数不保证等于 Item 的最终目标值。

`ChartAutomationPeer` 实现只读 Value 和 Selection，系列节点提供可取消的可见性切换。系列子树默认展开 64 个快照序号内的有效源点，提供前后分页按钮；无效行不为补满页面而越界扫描。`GetPointPage(series,startIndex,count)` 请求 1～256 个快照序号，`GetPointPeer(series,sourceIndex)` 请求原源索引。根最多强引用 256 个最近使用的点节点，客户端可持有已返回节点；`GetSelection()` 返回受 MaxSelectionCount 约束的完整显式选择，不因分页截断。

新场景发布后，数据缩短时越界页自动归到最后有效对齐页，空源回到 0，恢复数据可直接读取；pending/隐藏期间的临时零计数不会误重置页。仅成员/顺序变化触发根结构通知，浏览和数值变化不重复通知根树重建。

`ChartPointAutomationPeer` 提供 DataItem、轴格式化名称/Value、选中/可用/视口外状态、边界，以及 SelectionItem/Invoke。选择沿用上限、取消和重入规则；调用命令独立于选择，没有可执行命令或请求失效时明确失败。根缓存中已发现失效的节点退休并清除其点引用，不错误迁移至新对象。外部客户端或平台桥接持有的逐出节点不再接收主动更新，可继续保留旧业务项、系列和图表直至客户端释放；查询/操作仍验证当前有效性。256 是根缓存上限，不是全进程内存保证。身份匹配先检查原索引、复用已有索引，最后最多查 256 个邻近序号；无既有索引时，大源的远距离重排和非顺序快照远索引查找可能不可用，需要重新请求当前页面，未建立百万点辅助节点或完整新身份索引。

`HighContrastMode=None` 默认保留普通主题。安装 `ChartsTheme` 后，Light/Dark 显式选择白/黑画布、文字、轴、网格、提示、选择和六色调色板。`ChartContrastPalettes.Light/Dark` 均为不可变不透明色，每色分别对对应画布达到至少 7:1 的 sRGB 对比度；该条件不代表系列颜色相互都有 7:1。局部属性、绑定和显式系列 Stroke/Fill 优先且不被改写。仅功能包消费者自行提供样式，可使用公开调色板；直接包含 Generic.axaml 不包含 ChartsTheme 中的条件预设。当前不自动读取系统高对比偏好。

Demo → Charts 的辅助功能页展示真实业务项、缺失点、附加轴、摘要、当前点、选择与命令，以及普通/明/暗高对比。三系列分别使用圆形、方形和菱形，提供颜色之外的辨认线索。Usage 验证只读编译绑定和键盘行为，两种包消费者验证独立安装与样式优先级。

## 验证记录

| 最终 v3 软件验证 | Windows 原生合成窗口 | Ubuntu 26.04 WSL2 / WSLg |
| --- | --- | --- |
| Charts 测试 | 946/946 | 946/946 |
| 原生 Demo 检查 | 578/578 | 578/578 |
| 编译绑定 Usage | 163/163 | 163/163 |
| 仅功能包独立消费者 | 213/213 | 213/213 |
| 功能加主题包独立消费者 | 213/213 | 213/213 |
| Release 构建警告 / 错误 | 0 / 0 | 0 / 0 |
| 基础验证 / 独立消费者 / API launcher 退出码 | 0 / 0 / 0 | 0 / 0 / 0 |

清单见 [charts-c2-accessibility-validation.json](charts-c2-accessibility-validation.json)，API 见 [charts-c2-accessibility-api.txt](charts-c2-accessibility-api.txt)。本批较 C2-08 增加 54 个测试、74 项 Demo 检查、16 项 Usage 检查及每类消费者 30 项检查；不是以数量代替功能或原生门槛。两类消费者无源码项目引用，使用各自全新 NuGet 缓存，分别验证应用自供样式与可选 Themes；本批无新产品依赖，固定依赖源包含 14 个非产品归档，未改变 audit/警告策略。

最终源冻结 1279 个文件，每平台 589 个声明验证输入；两平台声明输入逐项一致，源、程序集、API 和包消费者身份由清单关联。27 个历史报告/API、基线与固定渲染器记录保留原哈希。后续一般文档更新不属于运行时声明输入，不声称当前全仓逐文件等于冻结副本；既有官方矩阵 200 个 ID、原有 59 次 URL / 50 个不同 URL 及顺序保持。

| 最终 v3 原生桥接项目 | Windows UIA | Linux 私有 AT-SPI 总线 |
| --- | --- | --- |
| 真实源点语义 | 9 个 DataItem；Value / SelectionItem / Invoke | 9 个 table-cell；真实名称/坐标/源索引 |
| 原生边界 | 非零窗口位置的 9 个屏幕矩形与 PointToScreen 复算一致 | 9 个屏幕矩形与数据中心一致 |
| 已测点操作 | Select / Add / Remove / Invoke；业务命令与状态确认 | click 执行业务命令；grab_focus 更新 CurrentPoint |
| 前后分页 | 0/1 → 2/3 → 0/1，通过 | 0/1 → 2/3 → 0/1，通过 |
| 已选点计数 / 状态 | 单点 IsSelected 正确 | 预置规范选择后 count=1、该点 Selected=true |
| 完整原生选中项枚举 | **失败：托管 1 项，原生空数组** | **失败：count=1，selected-child=null** |
| 嵌套点选择写入入口 | 上述单点 SelectionItem 动作已测 | 未合格；root select_child(0) 指向直属系列 |
| 原生系统点焦点 / 读屏器播报 | 系统 FocusedElement 仍是图表；实际播报未验 | 焦点请求与语义游标已测；系统点焦点及 Orca 播报未验 |

原生证据汇总为本批 `native-bridge-review-v3.json`。其中已测子集及已知失败的 19 项核对完成，`nativeBridgeSubsetPassed=true`；报告自身 `passed=false`、`completeNativeBridgeGatePassed=false`、`nativeRootSelectionGatePassed=false`，完整原生门槛没有被改判为通过。Windows 独立 .NET Framework C# 客户端排除了 PowerShell 枚举因素，自定义 selector 的真实附着 ControlPeer 对照也复现托管非空/原生空数组；该结果不进一步指认具体框架代码。Linux 同一私有 memory DBus 会话中有 GTK 正对照，分别记录 count、点状态、空 selected-child 和请求实际目标。

每个平台的亮暗辅助面板各记录 18 次路由键盘操作、13 个节点状态、8 种普通/高对比/PNG/JPEG 图像场景；两轴范围与原始业务身份独立复算。PNG 包含 96/144/192 DPI 固定尺寸的精确同路径比较，JPEG 使用有损误差检查；调色板对比公式、投影及有效墨迹由独立解码复核。像素尺寸/DPI 导出、RTB 页面视检、程序键盘事件与平台桥接分别记录，不能替代真实设备、跨屏、实际呈现或读屏器阅读。

本批独立证据复核为 2493/2493；后续身份复核确认实际 5388 个文件引用哈希均匹配。原 review 的计数字段写 5387，是在最后追加审阅脚本自身引用之前取值；原件保留，该差 1 的统计错误及全部实际引用由 `final-evidence-audit-v3-attempt3.json` 明确核实。C2-06 回归 318/318、C2-07 回归 746/746、C2-08 最终 attempt3 回归 1924/1924、本批语义/键盘/高对比及栅格 oracle 526/526，JPEG 几何的 32 个正例通过、160 个负例全部拒绝。各检查集存在证据交叉，不相加为新的测试总数。该复核通过表示软件子集、证据身份与失败范围记录一致，**不表示完整 native gate 通过**。局部 API 元数据新增 6 类型及 20 条既有类型的成员/接口声明，未移除或改写旧声明，不能等同于完整二进制兼容或商业 Chart API 认证。

最终文档/身份审计采用 `final-doc-audit-v3-attempt2.json`，2719/2719；独立身份/历史审计采用上述 attempt3，16/16。前一文档审计的空失败列表误写为 [null]，前一身份审计的引用数量断言失败，均保留原报告，最终选用明确命名的新 attempt，不按短文件名或通配符挑选结果。

开发首轮重点测试 39/41，两项分别暴露聚焦属性误使场景失效，以及测试渲染后尚未处理已排队场景发布的时序。产品针对焦点只发辅助通知，并让浏览方框按新场景解析；夹具处理正式发布队列后再检查游标清理。Usage 还发现浏览后切换 SelectionMode 使 scene pending，Space 未选中；现 Space/Enter 先发布场景并重新解析当前点，另有无显式布局的回归测试。没有通过跳过选择或降低有效性断言解决这些问题。

Windows 开发期原生树首轮只有图表、没有系列/点。查明 Avalonia 的虚拟节点默认 IsControlElement/IsContentElement 均为 false，显式声明后真实 UIA 才能读到节点与模式。随后修复普通读数反复触发根结构事件，保留值/选择事件，并仅对真实成员/顺序变化发根结构通知。

首版 v1 冻结后，独立审阅发现尾页数据从 2000 项缩至 10 项时，旧 PageStart=1920 会令客户端连续退页 30 次才能读到点。双平台自有验证进程被停止，v1 的冻结、部分日志和中断证据保留；Linux 外层实际退出 15，内层 TERM trap 记录的 0 伴随全部阶段 null，不能当作成功。修复为有效新场景中的页归一化，并补缩短、对齐尾页、空源恢复、隐藏恢复和 pending 不误清页五个测试，开发全量达到 942/942；随后 v2 两平台软件运行完整通过 942/578/163/每类消费者 213，仍不能取代原生桥接门禁。

v2 原生 Windows 复核中，点模式和单点动作通过，但完整 Selection 容器返回空数组，中间页只枚举到 Previous 按钮，两项门禁明确失败。开发 v3 探针的原始记录其实也有这两个 false；早期口头把几何检查通过误读成分页/完整选择都通过，已在独立纠正记录中说明，未改写原始结果。相同 DLL 的后续诊断证明实际目标/容器正确、9 点原生 RuntimeId 各异，托管选择集有 1 项而原生为 0。分页根因是 GetChildren 每次创建新的翻页节点，而 Win32 按旧节点引用定位下一个兄弟，因此在新 Previous 上中断；修复为每系列复用两个分页节点，另加引用稳定及真实寻邻模型回归测试。最终结果只采后续重新冻结与验证的候选。

Linux v2 首次启动脚本因 CRLF 在 bash 读取选项时退出 2，未进入产品构建；原脚本字节/哈希及诊断保留。修为 LF 并通过 bash -n 后另记 attempt2 及实际调用脚本身份，同一冻结产品的第二次启动完成全部阶段 0。脚本启动失败不被计为通过，也不需要改变产品源码身份。

Linux 辅助总线开发探针首轮错误地让私有总线激活的服务继承持久化 dconf 后端，短暂把用户 IsEnabled 偏好从 false 写成 true。已恢复原始 false，同时确认 ScreenReaderEnabled 仍为原始 false，纠正记录与前后检查保留。后续全部从私有 dbus-run-session 的父进程指定 GSETTINGS_BACKEND=memory 和隔离配置目录；GTK 正对照与 Avalonia 使用同一私有总线，结束后再次检查原偏好。该纠正不被描述为“从未改变过用户偏好”，也不作为真实读屏器使用通过证据。

Windows v3 页面人工视检记录的首版尺寸计算发生 byte 位移截断，仅记录了低 8 位；图片身份、字节数、SHA 和实际视检未变。原三份 JSON 保留，另生成 `*-v3-corrected.json`，通过完整 32 位大端 PNG 尺寸复算为动画 1160×730、导出 1160×800、辅助 1160×840，并关联原错误记录哈希；最终复核使用 corrected 记录，不覆盖原件。

C2-08 导出独立回归的早期 attempt 也保留失败身份。起初怀疑 JSON 序列化，后续 1e-7 数值预检证明是真实的 oracle 算术错误：PowerShell 的 Math.Max(1,doubleExpr) 选中整数重载，将图例剩余高度舍入。最终 attempt3 仅把两参明确为 double，恢复既定 ROI 公式；独立复核 80 对文件的 560 个 ROI，只有 80 个图例浮点高度改变，最大 0.05859375 px，其余字段/顺序/颜色/阈值未变，按实际 floor/ceil 裁剪后的整数区域全部相同。PNG 精确条件、JPEG MAE<8 及预先固定的有损几何正负准则没有放宽；原失败、纠正诊断和新脚本身份均保留。

C2-09 总审阅首 attempt 在 StrictMode 下读取空失败数组的 .name 属性时报错，原脚本与退出记录保留；改为显式遍历后按新的 attempt 完整执行，未改变产品输入或验证门槛。

Demo 开发期一次使用反序启动参数，进入普通窗口而未产生 smoke 记录，按启动错误保留；正确参数 `--charts-smoke` 重跑。Usage/消费者的两处摘要断言在修改配置后尚未发布新场景，按公开 pending 契约显式读取 PlotArea 发布后才检查轴，未用此改动掩盖上述 Space 产品缺陷。所有早期失败和后续结果均保留原身份。

## 剩余问题与边界

- **完整原生选择容器门禁未通过。** Windows 夹具中托管选择 1 项、UIA GetSelection 返回 0 项；自定义 selector 的真实附着 ControlPeer 对照也复现同样差异，不能据此指认具体框架代码或说只是图表虚拟节点问题。Linux 私有 AT-SPI 夹具中 selected-child count 可读为 1，但 get_selected_child(0) 返回 null；直属 series 的 select_child(0) 返回 false 不代表测试了嵌套点选择写入。单点状态/动作、摘要计数和已修复分页不替代该失败门禁；完整 native bridge 资格明确为 false。
- 原生点语义、原生系统焦点和实际读屏器播报属于不同资格。虚拟点 SetFocus 移动 CurrentPoint 并聚焦图表；Avalonia 12 公共外部接口不能替换原生根节点焦点返回，不据此宣称逐点系统焦点或完整读屏体验。
- 点节点 BringIntoView 目前把图表控件滚入外层容器，不自动平移图表视口；视口外点的 SetFocus 不会自动揭示该数据点。全域浏览、视口揭示与虚拟化需要后续适配。
- 系统高对比自动桥接、NVDA/Narrator/Orca 实际阅读流程、物理键盘/多屏 DPI、Ubuntu 22.04/24.04 物理桌面、原生 Wayland/ARM、GPU/backend/present 与官方许可运行对照仍需独立验证。
- 摘要结构词和分页按钮目前使用英文；轴读数沿用各轴格式化。应用可根据结构化摘要展示本地化文字，完整的库级辅助文案本地化仍待补。
- 分页节点的客户端持有成本、首次全源摘要计数、远距离身份索引与长无效区间的导航体验需要 C4 规模/内存/延迟验收。本批的有界查找与缓存规则不是百万点性能 SLA。
- C2 填充图案、类别图例、条目虚拟化、高级标签避让/连接线、内嵌概览、跨图游标/业务选择同步、独立 nullable-auto 轴、未来图族语义继续开放。父系列节点当前提供可见性 Toggle，独立 SelectedSeries 的选择模式尚未映射为系列 SelectionItem；摘要 SelectedCount 只计点选择。C2-07 逐系列时序、离场/错峰、系统 ReduceMotion 桥接等既有问题保留。
- AX06 多轴角落标题/末端刻度轻微重叠、4 个原始 v15 探针 DLL 缺口，以及 C4 百万点命中 p95 的 0.7649→1.1235 ms 和增量/LOD/内存/长稳问题均保留原身份，按用户决定后期处理。

总计划见 [开发计划](charts-development-plan.md)、[官方矩阵](charts-official-alignment.md)及 [性能/平台附件](charts-performance-platform-plan.md)。本库不依赖官方收费 Charts，不宣称商业 API 或性能兼容。
