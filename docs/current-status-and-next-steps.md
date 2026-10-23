# 当前功能状态与下一步计划

C5补齐当前进度（2026-10-23）：ST01/03/06/07/08/09/10七类统计基础缺项已完成，Demo七面板、编译绑定Usage和两种隔离包消费已接入。C5-01～05与C6-01～06基础图族主项已交付，下一主项C7-01全功能集成。

同一冻结source-v2在Windows和Ubuntu26.04 WSL2/WSLg通过Charts测试2830/2830、完整Demo4380/4380、Usage752/752；独立数值各820755，原生图像/来源19191/19191，每平台32张本轮PNG、44行metadata及14对RGBA零差。旧检查身份和459种类型声明保留；实际API499种、双平台一致。见[C5补齐报告](charts-c5-completion.md)和[验证清单](charts-c5-completion-validation.json)。

Parallel无效维数空帧的typed集合订阅丢失已修复并有双平台恢复回归。完整专业集成、官方API/二进制/性能等价、物理平台与长稳留C7；AN16 RSI/MACD仅公开目录核验未匹配，不能证明授权runtime不存在。C7-NATIVE-LOD-AV-01及地图暗色标签等旧问题保留；百万点尾延迟及A11Y-NATIVE-01按用户决定后置。下方历史“下一项”和旧缺项以本段为准，旧报告数据不重写。

C6 主项当前进度（2026-10-23）：C6-04排程四图、C6-05运营分析十一图、C6-06层级五图与地图叠加/分类色/多选已接入，Demo五组26种示例。C6-01～06基础功能主项已交付，下一主线按用户决定返回C5补缺。

同一冻结候选在Windows与Ubuntu26.04 WSL2/WSLg各通过2753项Charts测试、4046项完整Demo、688项Usage、两类隔离包消费者各904项；独立数值各1927023、原生图像/来源Windows50412及Linux50416谓词通过，每平台116张本轮PNG。旧测试/检查身份保留，API350→459且旧声明保留。见[C6主项完成报告](charts-c6-main-completion.md)和[验证清单](charts-c6-main-completion-validation.json)。

C6-MAP-FIT-01和C6-MAP-OBSERVER-01已修复并通过双平台验证；C7-NATIVE-LOD-AV-01未定位/未修复。C5七类统计缺项、专业共享交互、官方API/二进制/性能等价、物理设备与长稳、地图暗色标签对比继续开放；AK15精确官方映射留C7核验。百万点命中尾延迟及A11Y-NATIVE-01按用户决定后置。主项完成不等同所有专业能力和平台资格完成，下方历史“下一项”和旧问题状态以本段为准。

C6-03 当前进度（2026-10-23）：ShapeMap、SeatMap与三种图层覆盖气泡、区域着色、热力、座位和自定义形状五目录模式，支持离线GeoJSON/类型化多边形、空洞/日期线、三种投影、共享fit/缩放平移、原始业务身份、异步来源加载取消，Demo和Usage已接入。Natural Earth离线资源完整177 feature、10654顶点，来源/许可/SHA随资源保存。

同一冻结`c6-geographic-maps-source-v2`在Windows与Ubuntu26.04 WSL2/WSLg每平台通过2617项Charts测试（2515既有+98地图+4交互动画）、3286项完整原生Demo（本轮242）、584项Usage，两类新cache包消费者各790项，Release零警告/错误；独立数值2034945、像素156、来源4004谓词通过。22张固定图、10对native/export逐RGBA零差异，另两张总览。见[C6-03报告](charts-c6-geographic-maps.md)和[验证清单](charts-c6-geographic-maps-validation.json)。数量仅为Charts专项。

原source-v1的Windows九阶段通过，Linux首轮却在亮色既有动画出现4项暂停/导出失败，地图未执行；单次独立trace定位到IsPointerOver取消运行中动画。4项真实MouseMove用例旧核心红、修复绿，修复后Linux单次两主题trace通过；旧动画测试只追加39行且原有行保留，不声称文件SHA未变。首轮失败、源码差异和诊断完整保留，新候选重新完整验收；合成验证只覆盖此复现，不解决旧C7原生AV或全部物理/时序风险。

开放项：C6-MAP-FIT-01（无几何SeatMap极小单点fit可能不可见）、C6-MAP-OBSERVER-01（换源/移除图层的旧成员订阅到下次GetMarks/render才释放），以及C7-NATIVE-LOD-AV-01（此前既有LOD原生AV根因未定位、未修复）。本轮通过不等同完整native可靠性资格。完整Marker/Line/Vector/Pie叠加、瓦片/分类色/多选/模板、专业共享集成、完整C5/C6和官方等价继续开放。

下一主项C6-04排程和运营。数据/工作/贡献预算是准入上限，不是实际内存或性能SLA；正式性能、物理Windows/Linux/GPU/present和长稳待专项。百万点hit尾延迟及A11Y-NATIVE-01按用户决定后置，历史AX06/PixelProbe/动画/长路径/default-JIT等问题保留；旧独立oracle与C4-08未重跑。下方历史“下一项”以本段为准，历史验证数不重写。

C6-02 当前进度（2026-10-22）：四个数值引擎覆盖Sankey、Alluvial、Chord、Network、Force-directed、Arc、FlowChart、Process和Mindmap九目录模式，包含原始节点/边身份、循环/平行边/自环、流量守恒诊断、布局取消、选择、缩放及Demo九面板。

同一冻结`c6-flow-relations-source-v3`在Windows与Ubuntu26.04 WSL2/WSLg每平台通过2515项Charts测试、3044项完整原生Demo（本轮400）、557项Usage，两类新cache包消费者各756项，Release零警告/错误；独立数值134745、像素247、来源15912谓词通过。38张固定数学专图、18对native/export逐RGBA零差异，另两张总览。见[C6-02报告](charts-c6-flow-relations.md)和[验证清单](charts-c6-flow-relations-validation.json)。数量仅为Charts专项。

独立开放项 **C7-NATIVE-LOD-AV-01**：此前既有LOD原生导出曾fatal0xC0000005，根因未定位、未修复；44项专项及32轮GC未复现不能排除race。本轮软件运行通过不等同完整release/native可靠性资格。

下一主项C6-03地理图。图预算2000节点/10000边、力导向400节点/300迭代，布局路由及回退为有界启发式；完整Flow专业样式/编辑、完整C5/C6、官方等价、共享集成、正式性能和物理平台资格继续开放。百万点hit尾延迟、A11Y-NATIVE-01依用户决定后置；旧独立oracle未重跑，历史时序/长路径/AX06等问题保留。下方历史“下一项”以本段为准，历史验证数不重写。

C6-01 当前进度（2026-10-21）：完成Treemap、Sunburst、Organization、Bubble cloud、Packed bubble和Circle packing六图族，包含叶合计、稳定原始节点、展开/折叠、钻取/祖先路径、标签裁剪及入场/数据/视图动画，Demo六面板、Usage和两类包消费者已接入。

同一冻结`c6-hierarchy-packing-source-v1`在Windows与Ubuntu26.04 WSL2/WSLg每平台通过2393项Charts测试、2644项完整原生Demo（本轮344）、535项Usage，两类新cache包消费者各726项，Release零警告/错误；独立数值202108、像素346、来源13963谓词通过。本轮28张PNG中26张固定数学专图，12对native/export逐RGBA零差异。见[C6-01报告](charts-c6-hierarchy-packing.md)和[验证清单](charts-c6-hierarchy-packing-validation.json)。数量仅为Charts专项。

下一主项C6-02流向和关系。打包为确定性有界启发式，终端面积表示值而父圆为含空隙包络；额外层级目录、完整C5/C6、官方等价、专业共享集成、正式性能和物理平台资格仍开放。百万点hit尾延迟、A11Y-NATIVE-01依用户决定后置；C4-08及C5-01～05独立oracle未重跑，历史时序/长路径/AX06等问题保留。下方历史“下一项”以本段为准，历史验证数不重写。

C5-05 当前进度（2026-10-21）：完成Diverging、Dumbbell、Mekko、Mirror、Population pyramid、Tornado与双/三集合Venn，Demo八面板，Usage与两类包消费具备编译绑定。共享尺度、稳定排序、宽高/面积份额、inclusive→exclusive汇总依赖、三圆误差、嵌套更新与原始选择已验证。

同一冻结`c5-advanced-comparison-source-v1`在Windows与Ubuntu26.04 WSL2/WSLg每平台通过2288项Charts测试、2300项完整原生Demo（本轮440）、491项Usage，两类全新cache包消费者各676项，Release零警告/错误；独立数值1493、像素562、来源11992谓词通过。本轮40张PNG中38张固定公式专图，18对native/export逐RGBA零差异。见[C5-05报告](charts-c5-advanced-comparison.md)及[验证清单](charts-c5-advanced-comparison-validation.json)。上述数量仅为Charts专项。

下一主项C6-01层级和打包；C5-05是C5计划的第五子项，但剩余统计目录、完整C5、官方等价、完整C2专业交互集成和正式性能/物理平台验收仍开放。CP05 Parliament保留C6；百万点hit尾延迟、A11Y-NATIVE-01按用户决定后置。C4-08和C5-01/02/03/04独立oracle未重跑，历史时序/长路径/AX06等问题保留；下方历史“下一项”以本段为准，其历史验证数不重写。

C5-04 当前进度（2026-10-21）：完成 Carpet、Hexbin、Smith 阻抗/导纳、Ternary 和 Wind Rose 五类工程图与专业坐标，Demo 提供六个面板，Usage/包消费者提供编译绑定。单位、正逆投影、参数网格插值、聚合成员、选择与导出均有验证；已修复分箱浮点边界、稳定均值、重分箱选择身份和超大数据错误恢复问题。

同一冻结 source-v1 在 Windows 与 Ubuntu26.04 WSL2/WSLg 每平台通过2236项 Charts测试、1860项完整原生Demo、462项Usage，两类新缓存包消费者各639项，Release零警告/错误。独立数值、像素、原始成员及DPI检查通过；Linux首次验证工具的路径误报和完整重跑记录保留。结果与剩余问题见[C5-04报告](charts-c5-engineering-projections.md)和[验证清单](charts-c5-engineering-projections-validation.json)，上述数字属于Charts专项，不是全库当前总数。

下一主项为C5-05进阶比较。完整C5与统计缺项、专业标签/轴和C2集成、正式性能、长期稳定性及物理平台验收仍开放；百万点hit尾延迟及A11Y-NATIVE-01按用户决定后置。下方各历史阶段的“下一项”以本段为准，其原始验证数量保持历史身份。

C4-08 当前执行（2026-10-20）：benchmark、真实原生窗口驱动、原始数据/trace、独立差异守卫与失败证据已连接CI；保留既有七阶段门槛，增加计时/分配/失败率/原始身份及像素检查。PR配置独立base产品，native测量协议显式核验tiered compilation关闭；不修改Charts应用默认JIT策略。

C4-08 双平台验证：同一冻结source-v1在Windows与Ubuntu26.04 WSL2/WSLg每平台通过1939项Charts测试、1414项原生Demo、393项Usage，两类新cache消费者各553项；39项Python、30项PowerShell及15项重叠主guard入口通过，CI9个producer及同候选独立paired control通过，Release零警告/错误。实际GitHub远端run尚未执行；本轮不是完整C4 SLA/present/GPU/物理设备/长稳认证。见[C4-08报告](charts-c4-ci-performance-gates.md)、[验证清单](charts-c4-ci-performance-gates-validation.json)和[API](charts-c4-ci-performance-gates-api.txt)。

C4-08 后续：下一主项C5-01统计与分布；完整C2/C3/C4/C7及官方运行时/API/二进制对照仍开放。保留dev-v1真实p50拒绝，v2通过不证明JIT因果或产品回退已修复；百万点hit p95、A11Y-NATIVE-01、AX06、四份PixelProbe DLL、旧动画时序与长路径问题继续保留。下方历史“下一项”以本段为准。

C4-07 当前执行（2026-10-20）：完成多图 FIFO 合并刷新、隐藏/卸载生命周期、可取消且隔离旧版本的后台数字路径准备、进程五层缓存/在途任务估算预算及公开统计；Demo新增六图面板，Usage与包消费者完成编译绑定接入。后台默认关闭，数据映射/投影/轴/原生绘图仍在UI线程；公平的是失效请求轮次，不承诺整图CPU均分。

C4-07 双平台验证：同一 source-v2 冻结候选在 Windows 与 Ubuntu26.04 WSL2/WSLg 每平台通过 1939项Charts测试、1414项完整原生Demo、393项Usage，两类全新缓存包消费者各553项，Release零警告/错误；十阶段通过。固定四图机制17项门槛及新增两主题12张PNG的同步/后台、原始数据与业务ROI独立审查通过，不认证物理桌面、GPU/present、完整UI帧公平、长期内存或正式SLA。见[C4-07报告](charts-c4-multichart-background-layout.md)、[验证清单](charts-c4-multichart-background-layout-validation.json)和[API](charts-c4-multichart-background-layout-api.txt)。

C4-07 后续：下一项 C4-08 自动化性能门禁。共享索引/CWT/已发布预备路径、旧快照/调用方对象、原生/GPU和实际进程内存预算、未覆盖图族/域的同步N路径与30分钟/8小时长稳仍开放；降低预算时运行任务继续计费直到合作取消完成。百万点hit p95按用户决定后置，A11Y-NATIVE-01、AX06、四份PixelProbe DLL及旧动画/长路径问题保留。完整C2/C3/C4/C7及官方对照仍开放，下方历史阶段“下一项”以本段为准。

C4-06 当前执行（2026-10-20）：完成系列绘图、轴/交互文字、轴画笔与线段几何的本地共享预算，公开 ResourceCacheBudgetBytes/GetResourceCacheInfo/TrimResourceCaches，支持清理、禁用和可变画刷；ChartDataBuffer 提供 StoragePageCount 与显式 Compact 压实，保留原始记录、选择与旧快照。预算是Model1逻辑估算，不是进程/原生/GPU实际字节硬上限；共享数据/命中/LOD与source/series/context缓存、调用者对象和临时工作仍在五层账本之外。

C4-06 双平台验证：同一冻结候选的两物理副本在 Windows 与 Ubuntu26.04 WSL2/WSLg 每平台通过 1893/1893 项 Charts 测试、1384/1384 项原生 Demo、380/380 项 Usage，两类全新缓存包消费者各 538/538，Release 零警告/错误。九阶段完成，C4-01～C4-05继承原门槛重跑；新增20k点分配诊断、3000原始线段对、五层预算及2048→8页压实 oracle通过。每平台资源14图中12图独立解码并核对业务ROI/整图一致性。短时进程快照和线程分配不认证长期内存、原生/GPU、FPS或present。见[C4-06报告](charts-c4-render-resource-budgets.md)、[验证清单](charts-c4-render-resource-budgets-validation.json)和[API](charts-c4-render-resource-budgets-api.txt)。

C4-06 后续：下一项 C4-07 多图与后台布局。进程/共享索引/旧快照和任意业务对象预算、自动压实、复杂图族N路径、公平调度、30分钟/8小时长稳及真实backend/GPU/present继续开放。百万点hit p95历史0.7649→1.1235ms仍按用户决定后置；A11Y-NATIVE-01、AX06、四份PixelProbe DLL缺口与C4-03旧导出动画时序失败未关闭。完整C2/C3/C4/C7及官方对照仍开放，下方历史阶段“下一项”以本段为准。

C4-05 当前执行（2026-10-20）：完成 `ChartDataBuffer<T>` 分页有限窗口和追加/插入/删除/替换、`ChartStreamingSession<T>` 后台到 UI 的两批有界队列、背压、合并、暂停恢复、取消与统计；`DeferDataUpdates` 合并多源提交，`FollowLatestData/FollowLatestWindow` 支持暂停跟随历史窗口与恢复追到最新。Demo 新增四路实时画板，Usage 和两类仅包消费者使用编译绑定公开 API，原始 StableId/Item 保持，SourceIndex 随当前窗口重定位。

C4-05 双平台验证：相同冻结候选的两个物理副本在 Windows 与 Ubuntu26.04 WSL2/WSLg 每平台通过 1868/1868 项 Charts 测试、1320/1320 项原生 Demo、361/361 项 Usage，两类全新缓存包消费者各 517/517，Release 零警告/错误。四路共百万行的120轮源更新及12轮真实 Headless 附图、原始身份/快照/队列/跟随 oracle 与两主题原生像素检查通过；C4-01～C4-04机制保持原门槛重跑。源更新每轮复制/映射500行的结论仅适用已测路径；目录仍有 O(P)、多种派生索引仍有 N 级工作。虚拟节奏、线程分配和 DrawingGroup 不认证持续吞吐、总内存、首次可用、尾延迟、FPS 或 present。见[C4-05报告](charts-c4-incremental-streaming.md)、[验证清单](charts-c4-incremental-streaming-validation.json)和[API](charts-c4-incremental-streaming-api.txt)。

C4-05 后续：下一项 C4-06 绘图与资源预算。页碎片/压实、旧快照和任意业务对象的字节预算、日期/对数/断轴/自定义域与未覆盖图族的全量路径、多图公平调度、30分钟/8小时长稳和物理平台/backend/present继续开放。百万点 hit p95 历史回退0.7649→1.1235ms仍按用户决定后置；A11Y-NATIVE-01、AX06、四份PixelProbe DLL缺口与C4-03旧导出动画时序失败未关闭。完整C2/C3/C4/C7及官方对照仍开放，下方历史阶段“下一项”以本段为准。

C4-04 当前执行（2026-10-20）：完成原始点二维索引、原始相邻线段索引、密集共享 X/Y 读数与共享柱布局命中。稳定内置笛卡尔投影保留短 X 候选窗口，密集/非有序候选使用层级查询并执行原有精确几何规则；标记、重叠点及并列按实际绘制/物理顺序确定。各系列使用各自坐标轴，MinMax 显示与交互索引分离，Tooltip、选择和线段端点继续返回原始源索引、StableId、Item 与既有 RawPoint。Demo 展示重叠、同 X、独立右轴、抽样遗漏原始点、相邻线段、换源和两组柱体；无新增公开 API。

C4-04 双平台验证：候选 `c4-efficient-hit-source-v1` 在 Windows 与 Ubuntu26.04 WSL2/WSLg 每平台通过 1817/1817 项 Charts 测试、1266/1266 项原生 Demo、344/344 项编译绑定 Usage；两类全新缓存隔离包消费者各 498/498 项，Release 零警告/错误。12个固定命中诊断场景各10次预热查询，独立完整原始数据 oracle 核对点、Tooltip、选择和线段；C4-01～C4-03机制与既有业务像素门槛重跑通过。计时/分配仅作诊断，工作量门禁不代表全部输入的复杂度或正式尾延迟/FPS SLA。见[C4-04 报告](charts-c4-efficient-hit.md)、[验证清单](charts-c4-efficient-hit-validation.json)和[API](charts-c4-efficient-hit-api.txt)。

C4-04 后续：下一项为 C4-05 增量与实时调度。首次构建、极端重叠/跨视口长段、未知变换和复杂形状仍可能访问大量数据；平滑/阶梯、面积/范围段沿用正确保守路径。百万点 hit p95 历史回退0.7649→1.1235 ms按用户决定留后续专项，资源预算、长期内存、物理平台/backend/present和官方运行时对照继续开放。Windows UIA/Linux AT-SPI选中枚举保留延期 A11Y-NATIVE-01，C4-03旧导出动画夹具的暂停前置状态时序问题、AX06和四份PixelProbe DLL缺口未关闭。下方历史阶段文字保留原记录，其中“下一项”以本段为准。


C4-03 历史执行（2026-10-19）：完成显式线性折线 MinMax 保真抽样和层级细节。按不可变坐标版本复用分块极值层级，按 TopLevel.RenderScaling 或显式导出比例的像素桶保留首末点和峰谷，映射/补值后仍非有限的断点独立，保留边界邻点；已有 EmptyPointMode 补值连续语义不变。LineSeries.SamplingMode 默认 None，沿用 C4-02 原始细节/可见裁剪路径；显式 MinMax 适用于单调 X、线性且未堆叠、无标记/标签及稳定有效内置投影的实色折线，不适用场景保守回退并给出原因。Tooltip、点/矩形选择仍查询原始数据。Demo 提供10万/100万点、模式/物理桶宽及峰谷/断点缩放；导出默认原始细节，可显式按输出像素抽样。

C4-03 双平台验证：候选 `c4-level-of-detail-source-v1` 在 Windows 与 Ubuntu26.04 WSL2/WSLg 每平台通过 1730/1730 项 Charts 测试、1232/1232 项原生 Demo、332/332 项编译绑定 Usage；两类全新缓存隔离包消费者各 486/486 项，Release 零警告/错误。10个抽样诊断场景各10次视口变化，7个适用稠密夹具精确通过独立原始桶峰谷/断点/身份 oracle，预热源访问和层级重建为0；3个保守场景明确回退。C4-01 缓存、C4-02 裁剪及既有业务像素门槛重跑通过。计时/分配只作诊断，不代表正式 SLA。见[C4-03 报告](charts-c4-level-of-detail.md)、[验证清单](charts-c4-level-of-detail-validation.json)和[API](charts-c4-level-of-detail-api.txt)。

C4-03 后续：下一项为 C4-04 高效命中。非有序/平滑/阶梯/堆叠/面积/范围等仍使用原路径；柱状/散点密度聚合尚未定义语义，极多断点的保真输出仍可接近 N。任意祖先 RenderTransform/Viewbox 组合变换的密度资格、百万点 hit p95 历史回退 0.7649→1.1235 ms、增量/实时调度、缓存字节预算、长期内存和物理平台/backend/present 资格继续开放。Windows UIA/Linux AT-SPI 原生选中枚举保留用户延期的 A11Y-NATIVE-01；完整 C2/C3/C4–C7、官方等价和历史 AX06/四份 PixelProbe DLL 缺口未关闭。下方此前阶段文字保留历史状态，其中“下一项”以本段当前进度为准。


C4-02 历史执行（2026-10-19）：完成 CartesianChart 内置系列的可见数据裁剪，覆盖几何、保留绘图和矩形选择；小型线性折线、空间画刷和未知投影采用保守路径。在稳定的内置 Cartesian 投影下，有序 X 使用范围查询，非有序与二维数据使用按不可变坐标版本复用的空间索引；在既有渲染器最终可见性规则内保留穿越视口的线段端点、曲线所需邻点、带状区域和标记覆盖边缘。裁剪只筛选原始序号，不复制成新的数据源，Tooltip 与点/矩形选择继续返回原始 Item、SourceIndex、StableId 和 RawPoint。未知自定义投影保持保守路径。

C4-02 双平台验证：候选 `c4-visible-clipping-source-v1` 在 Windows 与 Ubuntu 26.04/WSL2/WSLg 每平台通过 1694/1694 项 Charts 测试、1188/1188 项原生 Demo、318/318 项编译绑定 Usage；两类全新缓存隔离包消费者各 470/470 项，Release 零警告/错误。9 个裁剪诊断场景各10次窄视口变化，源访问和索引所有者重建为0；这些稀疏夹具的实际投影/遍历量均低于源规模1/4，包含百万点有序折线、非有序及二维散点。C4-01 的9个 Tooltip 缓存场景也重新验证。计时/分配只作诊断，不代表正式 SLA。见[C4-02 报告](charts-c4-visible-clipping.md)、[验证清单](charts-c4-visible-clipping-validation.json)和[API](charts-c4-visible-clipping-api.txt)。

C4-02 后续：下一项为 C4-03 保真抽样和层级细节；大量线段同时跨越视口时，真实候选仍可能达到 O(N)，裁剪不能替代 LOD。增量/实时调度、缓存字节预算、百万点 hit p95 历史回退 0.7649→1.1235 ms、长稳内存和物理平台/backend/present 资格继续开放。Windows UIA/Linux AT-SPI 原生选中项枚举仍留 A11Y-NATIVE-01；完整 C2/C3/C4–C7、官方等价和历史 AX06/四份 PixelProbe DLL 缺口未关闭。下方此前阶段文字保留历史状态，其中“下一项”以本段当前进度为准。


C4-01 历史执行（2026-10-18）：完成 CartesianChart 主数据管线与内置系列；独立图族保留各自现有缓存 的数据/视图分层失效；数据快照与类别/堆叠/范围准备、极值/基础尺度、视图投影、轴文字、系列保留绘图与交互叠加工作分开复用。移动 Tooltip 的已预热场景不重新枚举或访问整个源，不重建数据、极值、视图、轴文字、排序/命中索引或系列几何；视口只重建相关视图，源变更按版本重新准备。自定义 renderer/投影保留保守实时路径，独立非 Cartesian 图族仍沿用各自缓存。

C4-01 双平台验证：最终候选 `c4-layered-invalidation-source-v1` 在 Windows 与 Ubuntu 26.04/WSL2/WSLg 每平台通过 1622/1622 项 Charts 测试、1188/1188 项原生 Demo、318/318 项编译绑定 Usage；两类全新缓存隔离包消费者各 470/470 项，Release 零警告/错误。9 个缓存机制场景各 30 次预热、100 次记录，源访问与各层重建增量均为 0，并验证视口/源版本改变的失效。计时与线程分配只作诊断，未评估正式 SLA、实际 present 或长稳内存。见[C4-01 报告](charts-c4-layered-invalidation.md)、[验证清单](charts-c4-layered-invalidation-validation.json)和[API](charts-c4-layered-invalidation-api.txt)。

C4-01 后续：下一项为 C4-02 可见数据裁剪；保留命中语义的范围索引、LOD、增量调度、缓存字节预算、百万点 hit p95 历史回退 0.7649→1.1235 ms 及物理设备/后端资格仍开放。保留绘图 replay 仍随绘图操作数变化，不是 O(1) present。Windows UIA/Linux AT-SPI 原生选中项枚举继续留 A11Y-NATIVE-01；完整 C2/C3/C4–C7、官方等价与历史 AX06/四份 PixelProbe DLL 缺口未自动关闭。下方 C3-07 及更早段落保留对应历史批次状态，其中“下一项”以本段当前进度为准。


Charts 更新日期：2026-10-18；其他控件保留各自已记录批次。本文按源码、测试和各控件状态文档汇总，用作仓库进度入口。版本仍为 `0.2.0-preview.2`，技术基线为 .NET SDK `10.0.401`、`net10.0`、Avalonia `12.0.3`。2026-10-15 的提交推送属于历史开发基线，本批 C4-04 未提交或推送；正式 NuGet 发布和版本升级尚未完成。

Charts 排期决定（2026-10-17）：按用户要求，将 Windows UIA / Linux AT-SPI 原生选中项枚举桥接问题后置到[无障碍专项 A11Y-NATIVE-01](charts-development-plan.md#a11y-native-01)，不阻塞当前功能及下一阶段 C3；原生门槛继续为未通过，C2 全阶段不因此自动完成。

## 状态说明

- **已实现**：当前功能包或示例中已有实现。
- **待完善功能**：尚未产品化、尚未实现，或明确作为后续扩展；不能用“待测试”代替开发。
- **待平台验收**：已有实现，但对应真实设备、显示后端、字体、输入或辅助工具证据不足。
- **历史验证**：只对记录中的源码、程序集和环境有效。Headless、合成输入和窗口可视树渲染图不能替代物理输入、桌面呈现和真实读屏验收。

Charts 当前执行更新：2026-10-18。

C3-07 历史实现（2026-10-18）：新增 FunnelChart、PyramidChart、HeatmapChart，提供真实业务数据映射、源项命中、Tooltip、基础选择、键盘浏览及 PNG/JPEG 同步/异步导出。PictorialBarSeries 接入现有 Cartesian 管线，支持符号 Stretch、Clip、Repeat、方向/间隔与实际形状及孔洞命中；ThemeRiverStack.CreateBaseline 提供堆叠面积组合所需的居中透明底座。Demo「基础分析图」的七张图卡、真实编译绑定 Usage 和两类隔离包消费者均接入；此为本库自有软件契约，不代表官方完整 API 或运行时等价。

C3-07 数值契约：漏斗与金字塔仅对有限正数创建阶段，保留源顺序和原始数值；漏斗按值/最大值与 NeckWidth 计算连续边界，金字塔在无间隔时按正数面积比例分层；间隔删除实际绘制面积而不改业务值，不宣称带间隔面积仍严格等比。Heatmap 行列按类型化键首见顺序建立，重复单元最后源项胜出（包括无效末项），不存在的组合不制造业务点；颜色域、缺失填充和实际色标分别展示。Pictorial 三模式裁剪到数值柱体与 PlotArea，不用包围盒填补孔洞；零值无柱体。Theme river 输入必须槽位对齐、有限且非负，透明正向 spacer 与已有 AreaSeries 同组堆叠；该底座已实现 silhouette 式居中规则；wiggle、优化流顺序及更多基线算法尚未验收，也不虚列独立官方 Theme river 系列。

C3-07 正式验证：最终候选 `c3-analysis-source-v1` 在 Windows 与 Ubuntu 26.04/WSL2/WSLg 每平台通过 1500/1500 项 Charts 测试、1188/1188 项原生 Demo 检查、318/318 项编译绑定 Usage 检查，功能包/主题包两类全新缓存隔离消费者各 470/470 项；Release 构建零警告、零错误，基础/消费者/API 三阶段退出码均为 0。 结果见[C3-07 报告](charts-c3-analysis.md)、[验证清单](charts-c3-analysis-validation.json)与[API](charts-c3-analysis-api.txt)。

C3-07 验证口径：冻结 1400 文件，两平台各 691 个声明运行输入逐项同 SHA；每平台运行前后声明源与程序集稳定，文档更新后不声称整个工作区与冻结副本相同。独立来源/平台审阅 4280/4280，PNG 审阅 981/981、实际解码 168 图，独立包载荷核对 14/14；47 个历史报告/API pin（含 C3-06 三文件）与 7 版历史 renderer 保持原 SHA。检查重叠，不合并为产品测试数。完整本地 API 类型 154→163，Charts 命名空间 152→161，新增 9 类型、既有类型增加 0 项声明；无旧类型、声明或成员删除，不代表官方或二进制兼容。 各图族原始业务数值和公开布局公式在对应首图前封存，独立解码真实 PNG，不读取生产几何、投影或命中输出作为像素预期。两主题原始与改变状态后的原生整板另行实际查看。Headless 包消费者区分真实 PNG 文件头与明确后端 NotSupported、零输出及一次 owner/UI 线程失败事件；后者是失败反馈契约通过，不是 PNG 成功。原生窗口图片、导出/DPI、源码/程序集及包消费检查均不代表物理输入、实际 backend/GPU/compositor present 或性能资格。

C3-07 修复记录：开发期原始失败与修复结果独立封存：首次 Core 缺少 Avalonia.Controls using 产生 2 个编译错误，其记录为工具转录而非原始日志；测试编译曾因生命周期夹具宿主类型不匹配失败。初始 Pictorial 投影元数据断言在延迟场景发布前读取，后续保留 Item/ID/RawPoint 断言并在绘制后有限排空 UI 任务。tests-v2 是编译失败后误用旧测试 DLL 的 77/79，明确排除为新候选验证；tests-v3 的 88/90、修复后 tests-v4 的 90/90 和 full-tests-v1 的 1494/1494 均保留原结果。首张对应图片前另修正 Heatmap 固定 gap/键、Pictorial 两极性输入、初始标签开关与只读 river 图例夹具，预注册 oracle 和像素阈值未改变。native-v1 的 Usage317/317 通过，但 Demo 在1052项检查后因生产控件 AnalysisChartBase.Render→GetMarks→SetSelected 对外通知 TextBlock 绑定，触发 compositor 的 Visual was invalidated during the render pass，进程失败；这不是夹具问题。该失败实际使用的8个DLL已在后续重建前保留并按原manifest核验。本轮随后修复生产控件，使几何构建与状态发布分离，由绘制后的单个UI任务按最新revision发布，并增加回归及默认TwoWay选择绑定。修复后 tests-v5 96/96 与 full-tests-v2 1500/1500、native-v2 Demo1188/1188、Usage318/318、两进程退出0及8DLL运行稳定按实际证据保留。全部开发失败及修复均不替代后续冻结正式验证。

C3-07 剩余：复杂漏斗/金字塔标签避让与全模板、Heatmap 非线性色阶/高级颜色轴与大矩阵虚拟化、Pictorial 全组合和高密度性能、完整 Theme river 控件、wiggle/优化流顺序/更多基线算法及 C6 分析图组合继续开放。新独立宿主的完整 C2 标注/联动/缩放/范围/Trackball/辅助语义、标准入场/数据/视图动画仍需逐族适配；既有 C3-01～C3-06 的异构共边、逐点样式、图例/布局/动画及完整 C2 残余不自动关闭。百万点命中 p95 历史回退 0.7649→1.1235 ms 继续留 C4，Windows UIA/Linux AT-SPI 原生选中项枚举继续留 A11Y-NATIVE-01；AX06 标题/末刻度布局与四个历史 PixelProbe DLL 证据缺口保留。本批完成 C3-07 指定软件范围，下一主项为 C4-01 数据与视图分层失效；完整 C2/C3/C4–C7、官方等价及物理平台门槛仍开放。

C3-06 历史实现（2026-10-18）：新增 CircularGaugeChart、GaugeChart、GradientRingChart、LinearGaugeChart、LiquidFillGaugeChart、ProgressDonutChart 六类仪表，以及 SparklineChart 的 Line/Area/Bar/WinLoss 四模式。共享数值域和目标，圆形/半圆/线性仪表的区间、多环独立数值和梯度画刷、线性横竖/方向、圆/矩形液体静态波形和显式动画/暂停/减少动态效果、微图极值/首尾/标记/缺口、业务身份与实时更新、PNG/JPEG 同步/异步导出接入 Demo「仪表与微图」、真实编译绑定 Usage 和两类隔离包消费者。此为本库自有软件契约，不代表官方完整 API 或运行时等价。

C3-06 数值契约：有效仪表域要求有限上下限且 Maximum>Minimum；Normalize 结果限制在 0–1，原始 Value/TargetValue 不改写，无效域或非有限值不绘数值。圆形/半圆/线性仪表的范围按业务值投影，多环共用域并分别显示进度。微图按原始源槽位排布，不按类别排序；默认 Gap 下非有限和缺失保留槽位并切断路径；可通过 Series.EmptyPointMode 显式填补并保留 WasEmpty 元数据。面积和柱形自动域包含 Baseline，WinLoss 按相对基线的正/负/零显示固定符号格并保留原始值。附着时观察集合与业务项，卸载释放；液体波动默认关闭，静态与导出共享绘制路径。

C3-06 正式验证：最终候选 `c3-gauge-sparkline-source-v2` 在 Windows 与 Ubuntu 26.04/WSL2/WSLg 每平台通过 1404/1404 项 Charts 测试、1048/1048 项原生 Demo 检查、295/295 项编译绑定 Usage 检查，功能包/主题包两类全新缓存隔离消费者各 432/432 项；Release 构建零警告、零错误，基础/消费者/API 三阶段退出码均为 0。 结果见[C3-06 报告](charts-c3-gauge-sparkline.md)、[验证清单](charts-c3-gauge-sparkline-validation.json)与[API](charts-c3-gauge-sparkline-api.txt)。

C3-06 验证口径：冻结 1380 文件，两平台各 674 个声明运行输入逐项同 SHA；每平台运行前后声明源和程序集稳定，文档更新后不声称整个工作区与冻结副本相同。独立来源/平台审阅 4188/4188，PNG 审阅 869/869、实际解码 168 图，独立包载荷核对 14/14；44 个历史报告/API pin（含 C3-05 三文件）与 6 版历史 renderer 保持原 SHA。检查重叠，不合并为产品测试数。完整本地 API 类型 140→154，Charts 命名空间 138→152，新增 14 类型，既有类型声明和成员无新增、删除或改写；不代表官方或二进制兼容。 每平台两主题各 18 个首图前封存业务像素场景，使用固定 420×280 控件、原始业务值和公开布局公式独立复算，不使用生产几何/命中/GetLayout 输出当预期。6 DIP 方框、RGB 容差 24、alpha 至少 240、正像素至少 6、负像素 0；每场景 96 DPI 导出与原生捕获逐像素零差，仪表和微图代表图追加 144/192 DPI 密度与逐像素核对。十卡独立 Viewbox board 在程序化窗口内完整显示；Headless 包消费者只核对真实 PNG signature/IHDR，或唯一明确后端 NotSupported、零输出与 owner/UI 线程失败事件；后者是结果契约通过，不是 PNG 成功。原生像素资格使用上述独立原生窗口场景。这些检查不代表物理输入、桌面呈现或性能资格。

C3-06 修复记录：开发及正式 source-v1 失败证据原样保留。Usage 输入 Slider 曾通过 TwoWay 将请求 180 反写为输入上限 120；扩大示例固定输入域后保留原始值/归一值断言。Headless 不支持真实 PNG 的两项检查改为严格的成功文件头或明确受控后端失败契约，未改原生像素门槛。独立审查还定位并修复了生产生命周期问题：DataContext/挂载回调中卸载、重挂载或换父级时的仪表观察器泄漏、Sparkline 内部 Series 继承/逻辑父级和 Changed 订阅状态失配；按最新挂载状态协调拥有关系并补真实回调回归。超大有限 StartAngle 在加进度前归一化，避免浮点精度吞掉角度增量导致路径退化；超大液体相位先归一化再加已过时间。上述为生产修复，最终候选重新冻结并完整重跑，未用夹具调整替代。

C3-06 剩余：仪表数值过渡 timeline、完整 C2 模板/选择/标注/联动/缩放适配、复杂标签和区间布局、完整官方 API/组合、点级及原生辅助语义、正式 C4 性能与物理平台资格仍开放。保留 C3-01～C3-05 的异构共边、逐点样式、大小/颜色图例、圆形高级标签/图例/模板、极坐标轴/布局/动画及全 C2 适配残余。百万点命中 p95 历史回退 0.7649→1.1235 ms 继续留 C4；Windows UIA/Linux AT-SPI 原生选中项枚举继续留 A11Y-NATIVE-01，未因本批软件验证关闭。AX06 标题/末刻度布局与四个历史 PixelProbe DLL 证据缺口继续开放。该历史批后续主项为 C3-07 基础分析图；当前进度以本文 C3-07 段落为准，完整 C2/C3/C4–C7 仍开放。

C3-05 历史实现（2026-10-18）：新增独立 PolarChart 与默认多边形网格的 RadarChart，以及 PolarLine/PolarScatter/PolarFilled、Radar、RadialLine、PolarArea、NightingaleRose、RadialBar 八种系列。数值角度与分类角度分别支持角度/半径轴、度/弧度、起点/方向/部分周期、源序断点与闭合、圆形/多边形网格、轮廓/填充/标记；选择、Tooltip、基本标签及 PNG/JPEG 同步/异步导出接入 Demo「极坐标与雷达」、编译绑定 Usage 和两类隔离包消费者。这是本库自有软件契约，不代表官方完整 API 或运行时等价。

C3-05 数值契约：AnglePath/RadiusPath 分别是 CategoryPath/ValuePath 的别名，原始 Item/StableId/SourceIndex 和角度/数值保留。数值角度按线性轴范围归一化，再应用显示度数的 StartAngle/SweepAngle、方向及 WrapAngles；分类系列共享可见系列首见类别并集，重排系列仍沿同名射线。Radar.MaxValue 默认 100、null 使用共享半径域；负半径、非有限值和缺失形成缺口。等角 PolarArea 以环形面积编码，NightingaleRose 以线性半径编码，RadialBar 以同心轨道弧长编码；零值扇区不画，InnerRadius 和 SectorGap 不改写业务值。同构极面积/玫瑰的刻度使用同一数值映射，径向条类别沿轨道、值沿角度显示；异构编码组合明确标为 r/R 和 θ 参考。扇区数值圈、轨道及物理 r/R 参考固定使用真圆/部分弧，Polygon 仅作用于点/雷达/径向线的顶点参考。标签与图例为有限空间绘制式基础实现。

C3-05 正式验证：最终候选 `c3-polar-radar-source-v2` 在 Windows 与 Ubuntu 26.04/WSLg 每平台通过 1292/1292 项 Charts 测试、954/954 项原生 Demo 检查、275/275 项编译绑定 Usage 检查，功能包/主题包两类全新缓存隔离消费者各 396/396 项；Release 构建零警告、零错误，基础/消费者/API 三阶段退出码均为 0。 结果见[C3-05 报告](charts-c3-polar-radar.md)、[验证清单](charts-c3-polar-radar-validation.json)与[API](charts-c3-polar-radar-api.txt)。

C3-05 验证口径：冻结 1356 文件，两平台各 653 个声明运行输入逐项同 SHA；每平台运行前后声明源和程序集稳定，文档更新后不声称整个工作区与冻结副本相同。独立来源/平台审阅 4076/4076，PNG 审阅 864/864、实际解码 96 图，API 元数据审阅 4/4，独立包载荷核对 14/14；41 个历史报告/API pin（含 C3-04 三文件）与 5 版历史 renderer 保持原 SHA，检查重叠，不合并为产品测试数。完整本地 API 类型 125→140，Charts 命名空间 123→138，新增 15 类型，旧类型与成员无删除或改写；这不等于官方或二进制兼容。 每平台两主题各 15 个首图前预注册业务像素场景和 3 个轴修复前预注册补充，用固定角度/半径/类别值和公开布局公式独立复算，不使用生产投影/命中输出当像素预期；业务控件显式且实际为 700×520。网格沿原 pin 使用正像素 6、线/填充 10、负像素 0，96/144/192 DPI 导出逐像素零差。首轮轴默认值注册启动失败、过度收紧网格阈值、角度归一预期和桌面高度裁切等开发证据保留。正式 source-v1 验证期间，人工审阅仍发现内孔极面积/玫瑰刻度未按编码映射及径向条类别/数值轴混淆；保留原候选，修正轴展示、增加能区分原错误的独立探针，重新冻结 source-v2 并完整重跑，未替换原 15 场景。旧 Trackball 的 40 边界事件实际回读 39.948757…，保留实际边界检查；双系列插值/双卡业务输入改为共同内域 45，原数学、端点身份、数量与容差门槛不变，生产不在数据域外外推。附加 Trackball 诊断中的 cards 字段延迟序列化，不能作为断言时的卡片上下文证据；实际布尔门禁即时检查上下文，rawY/routed/PlotArea 为值快照。

C3-05 剩余：非线性轴、断轴和完整轴布局、严格标签碰撞/扇区内包含、进阶模板和交互图例、极坐标全 C2 缩放/平移/Trackball/范围联动/标注/点级辅助语义、官方完整 API/目录组合、正式 C4 性能与物理平台资格仍开放。Polar/Radar 当前没有入场、数据或视图动画 timeline。完整极坐标风玫瑰方向分箱/堆叠留 C5，云/打包留 C6；历史散点大小图例/连续色标、圆形高级标签/图例/模板等残余保留。百万点命中 p95 历史回退 0.7649→1.1235 ms 继续留 C4；原生选中项枚举继续留 A11Y-NATIVE-01，未由本批软件验证关闭。AX06 标题/末刻度布局与四个历史 PixelProbe DLL 证据缺口继续开放，未用本批新证据替换。该历史批后续主项为 C3-06 仪表和微图；当前进度以本文 C3-07 段落为准，完整 C2/C3/C4–C7 仍开放。

C3-04 历史实现（2026-10-18）：已新增独立 CircularChart、PieSeries、DonutSeries 和 SemiDonutSeries，覆盖饼、环、半环、部分弧、顺逆时针、多环及分离变体；零/负值、小扇区、内外标签与连线、中心文字/内容、业务身份选择和当前帧过渡接入 Demo「圆形图」、编译绑定 Usage 及两类隔离包消费者。每个系列独立归一化，SeriesSource 可绑定业务系列集合；这些是本库自有契约，不代表官方完整 API 或运行时等价。

C3-04 数值契约：默认映射快照中的零值、非有限值和显式缺失不分配角度；显式 EmptyPointMode 可先填充缺失并保留 WasEmpty 元数据。负值可 Ignore、Absolute 或 Throw；Absolute 用绝对值计算份额并保留带符号原值。SliceGap 收缩绘制角度，小扇区 Hide 保留原份额和角度空缺，不合并、不重分配、不放大；过渡期间 Data/Fraction 和 RawStartAngle/RawSweepAngle 保留最新目标的业务数据、份额与分配角度，StartAngle/SweepAngle、中心、半径和实色填充为当前帧；退场 IsExiting 项不参与命中或选择。Radius 是布局半径比例，InnerRadius 是本系列外半径比例，多环分别计算总量。当前扇区、命中与选择保留 Item/StableId/SourceIndex，选择可前置取消并按业务 ID 更新；外标签在有限空间内避让、截断或省略，省略标签不改写数据。

C3-04 动画支持入场、数据和几何配置变化，包含时长、缓动、延迟、暂停、取消与显式减少动态效果；导出捕获当前帧并支持 PNG/JPEG、尺寸、背景、DPI、异步、取消与失败反馈。Demo 同时提供六图、负值策略、小扇区和标签开关、数据更新、选择分离与动画操作；Usage 以真实编译绑定检查属性、INPC、选择身份及卸载/重挂载。当前圆形宿主的基本提示、键盘和选择功能不等于笛卡尔宿主全部 C2 模板、标注或完整无障碍功能。

C3-04 正式验证：最终候选 `c3-circular-source-v3` 在 Windows 与 Ubuntu 26.04/WSLg 每平台通过 1214/1214 项 Charts 测试、890/890 项原生 Demo 检查、250/250 项编译绑定 Usage 检查，功能包/主题包两类全新缓存隔离消费者各 354/354 项；Release 构建零警告、零错误，基础/消费者/API 三阶段退出码均为 0。结果见[C3-04 报告](charts-c3-circular.md)、[验证清单](charts-c3-circular-validation.json)与[API](charts-c3-circular-api.txt)。

C3-04 冻结 1337 文件，两平台各 637 个声明运行输入逐项同 SHA；后续文档更新不表示整个工作区与冻结副本相同。独立来源/平台审阅 3989/3989，PNG 审阅 712/712、实际解码 120 张图，API 元数据审阅 4/4；38 个历史文档/API pin 与 4 版 renderer 保持原 SHA，检查重叠，不合并为产品测试数。完整本地 API 类型 115→125，Charts 命名空间 113→123，新增 10 类型，旧类型和成员声明保持不变，不代表官方或二进制兼容。两平台两主题各 12 项固定极坐标/标签探针、原请求布局的 2 项中心内容补充及显式 1160×1160 的 2 项中心内容回归、暂停帧和 96/144/192 DPI 导出通过；正式 v1/v2 中心内容分数边缘及大布局局部坐标的导出差异修复后重新冻结并完整重跑，原失败/中断证据保留。

C3-04 剩余：内标签文字矩形严格包含于扇区、与中心文字/中心控件严格避让、极端标签密度/连线交叉、进阶图例/模板、圆形图完整辅助语义和 Cartesian 全 C2 功能适配仍开放。中心控件复杂 Effect、平台材质/缓存和自定义可视效果未获完整导出等价资格；本库完整官方 API/目录组合、正式性能与物理设备资格仍开放。百万点命中 p95 历史回退 0.7649→1.1235 ms 继续留 C4；Windows UIA/Linux AT-SPI 原生选中项枚举继续留 A11Y-NATIVE-01，未因圆形图软件验证关闭。该历史批后续主项为 C3-05 极坐标与雷达；当前进度以本文 C3-07 段落为准，C2/C3 整阶段与 C4–C7 仍开放。

C3-03 历史实现（2026-10-18）：已补散点逐点大小/颜色编码、独立 ScatterLineSeries 和真实 XY BubbleSeries；面积/直径尺度、数值域/渐变端点、源序/大小绘制顺序、形状内部及轮廓命中、视口裁剪与原始业务身份接入 Demo「散点与气泡」、编译绑定 Usage 和两类隔离包消费者。SizeValue/ColorValue 为原始目标映射，MarkerDiameter/PointFill 为当前显示帧派生，ShowMarkers 另行控制可见性；Point 为当前帧显示 XY，RawPoint 保留源坐标。默认数据标签为当前数值端点，Demo/Usage 主动用 formatter 展示业务大小。

C3-03 使用本库自有 PointSeries 公共契约，既有 ScatterSeries 保留类型及默认快速路径。尺寸是 DIP 直径，Area 在直径平方之间插值，Diameter 直接插值；Bubble 的 SizePath 优先于兼容 WeightPath。缺失/负大小隐藏标记，仍保留有效 XY 浏览，缺失 XY 才断 ScatterLine 源序轨迹；绘制排序不重排业务源或连线。颜色支持数值渐变及直接 Color/IBrush/颜色字符串。编码域基于轴域有效的目标 XY，与视口独立；动画中非实色画刷直接切到目标。新编码点系列按可见形状及轮廓命中，普通 Scatter 默认快速路径保留中心距离规则，不将此度量泛化到全部混合 renderer。范围选择和键盘沿真实点中心；散点连线参与线段插值与 Trackball，真实两端身份不改写。

C3-03 正式验证：最终候选 `c3-scatter-bubble-source-v2` 在 Windows 与 Ubuntu 26.04/WSLg 每平台通过 1145/1145 项 Charts 测试、804/804 项原生 Demo 检查、229/229 项编译绑定 Usage 检查，功能包/主题包两类全新缓存隔离消费者各 321/321 项；Release 构建零警告、零错误，基础/消费者/API 三阶段退出码均为 0。结果见[C3-03 报告](charts-c3-scatter-bubble.md)、[验证清单](charts-c3-scatter-bubble-validation.json)与[API](charts-c3-scatter-bubble-api.txt)。

C3-03 冻结 1320 文件，两平台各 623 个声明运行输入逐项同 SHA；后续文档更新不表示整个工作区与冻结副本相同。独立来源/平台审阅 3912/3912，PNG 审阅 640/640、实际解码 80 张图，API 元数据审阅 4/4；这些检查重叠，不合并为产品测试数。完整本地 API 类型 110→115、Charts 命名空间 108→113，新增 5 类型及既有类型 33 项成员/接口声明，无旧类型或成员删除，不代表官方或二进制兼容。两平台两主题各 14 项预注册独立业务代数正负像素探针和 96/144/192 DPI 导出通过。开发布局增高后保留既定门槛；正式 v1 厚描边/断轴裁剪问题修复并补 16 项回归后重新冻结 v2、完整重跑，原失败与中断证据保留。

C3-03 剩余：专属大小图例/连续色标、无轴云布局与紧凑打包、官方完整 API/运行时等价、性能和物理平台资格仍开放；云/打包留 C6，百万点命中 p95 历史回退 0.7649→1.1235 ms 留 C4，原生选中项枚举留 A11Y-NATIVE-01。本批 PNG 与程序化窗口通过不代表物理输入、实际 backend/GPU/present 或 Ubuntu 22.04/24.04 实机资格。该历史批后续主项为 C3-04 圆形图；当前进度以本文 C3-07 段落为准。

C3-02 历史实现（2026-10-18）：已补齐柱/横条的分组、正负堆叠和百分比，以及范围柱、瀑布、Pareto、Dot、Lollipop、Variance；方向、基线、相对/固定 DIP 宽度、显示排序和原始值标签接入 Demo「柱条与比较」、编译绑定 Usage 与两类隔离包消费者。`Point` 为显示坐标，`RawPoint`、`Item`、`StableId`、`SourceIndex` 保留原业务数据；数值维基线、堆叠份额及 Pareto 累计值分别由 `BaseValue`、`StackValue`、`CumulativeValue` 提供。

C3-02 使用本库自有 `ComparisonSeries` 公共契约，BarSeries 保留既有类型并扩展相同配置。默认数据标签读取显示端点（横向 X、纵向 Y），本批 Demo/Usage 主动以 DataLabelFormatter 读取 RawPoint 显示原值；值排序要求自动 Labels 的 CategoryAxis，显示槽改变不改写源顺序，瀑布只支持源序，Pareto 固定非负有效项降序并用独立线性百分比轴展示累计。范围柱规范化上下界，Variance 保留有向 High/Low；瀑布总计行从基线到源值并重置累计，缺失保留缺口。本库 API、范围/排序/Gap 策略与官方全部属性不等价；未来图族、逐点样式、圆角/独立描边、直方分箱与进阶比较仍有后续范围。

C3-02 正式验证：冻结候选 `c3-bar-comparison-source-v1` 在 Windows 与 Ubuntu 26.04/WSLg 分别通过 1083/1083 项 Charts 测试、744/744 项 Demo 检查、206/206 项 Usage 检查，以及功能包/主题包两类全新缓存隔离消费者各 284/284 项；Release 构建零警告、零错误，基础/消费者/API 三阶段退出码均为 0。正式结果见[C3-02 报告](charts-c3-bar-comparison.md)、[验证清单](charts-c3-bar-comparison-validation.json)与[API](charts-c3-bar-comparison-api.txt)。

C3-02 冻结 1306 文件，两平台各 612 个声明运行输入逐项同 SHA；后续文档更新不表示整个工作区与冻结副本相同。来源/平台审阅 3850/3850，独立 PNG 审阅 853/853、实际解码 100 张图；这些检查重叠，不合并为产品测试数。完整本地 API 类型 102→110，Charts 命名空间 100→108，新增 8 类型及既有类型 21 项成员/接口声明，无删除或旧声明改变；元数据比较不等于二进制或官方 API 兼容。本批未取得 C4 性能/内存、真实设备、实际 present 或官方许可参考资格；原生选中项枚举继续后置 A11Y-NATIVE-01。该历史批的后续工作为 C3-03；当前进度以本文 C3-07 段落为准，C2/C3 整阶段及 C4–C7 仍开放。

C3-01 历史批已实现普通/平滑/三种阶梯折线、面积与范围面积，以及线/面积的正负堆叠和百分比变体，接入 Demo「折线与面积：平滑、阶梯、范围和堆叠」、编译绑定 Usage 和两类包消费者。共享原始 Item/StableId/SourceIndex，显示点另带 RawPoint/BaseValue/StackValue；插值读数、范围下界、坐标轴/断轴、选择与键盘、显示帧动画及 PNG/JPEG 导出复用共用底座。

C3-01 历史冻结源码 c3-line-area-source-v1 在 Windows 与 Ubuntu 26.04 WSLg 分别通过 1013/1013 项 Charts 测试、652/652 项 Demo 检查、181/181 项 Usage 检查，以及功能包/主题包两类全新缓存 NuGet 消费者各 245/245 项；Release 构建零警告/错误，基础、包消费与 API 三阶段退出码均为 0。 该历史批冻结清单包含 1291 个文件，两平台各声明 600 个运行输入；独立证据复核状态以该历史批验证清单为准。 该历史批范围与正式证据见[历史报告](charts-c3-line-area.md)、[验证清单](charts-c3-line-area-validation.json)与[API](charts-c3-line-area-api.txt)。

C3-01 历史批的连续共边保证限于相同插值、源顺序 X 和有效段拓扑的兼容堆叠；Smooth 组共同缩短顶点切线，保持 C1 与同符号层厚度，异构拓扑仅保证实际 X 样本的累计。范围命中/浏览以上边界代表一项，下界通过 BaseValue 提供；范围及堆叠拒绝 CategoryY。对数轴上面积默认零基线回退有效轴原点，非零域外下界保留缺口。新形状构建/命中仍为 O(N)，未取得 C4 性能/内存、真实设备、实际 present 或官方许可参考资格；原有 Linear 快速路径保留，不能据此推断新变体的百万点性能。

C2-09 历史批已实现当前三系列的点级角色/名称/只读值/选择与命令、按需分页节点、独立于选择的键盘浏览、可绑定数据摘要及显式亮暗高对比配色，并接入 Demo/Usage。默认每页 64 个源序号，图表自身 LRU 最多强引用 256 个点节点（客户端仍可持有已逐出节点）；保留原始 Item/StableId/SourceIndex，选择继续遵守取消与重入契约。

外部 Windows UIA 已实查 3 个系列、9 个点、Value/SelectionItem/Invoke 的点操作子集及分页；Ubuntu 26.04 WSLg 的独立 AT-SPI 总线已实查 9 个点、边界、click/grab_focus 和分页。完整原生桥接门槛仍未通过：Windows 根 SelectionPattern.GetSelection 返回空集合（托管列表为 1，自定义 selector 的真正附着 ControlPeer 对照也复现）；Linux 预设 canonical 点选择后点 Selected 状态为 true、get_n_selected_children 为 1，但 get_selected_child(0) 返回 null，完整已选子节点枚举未过，嵌套点选择写入仍未验。根 select_child(0) 指向直属系列，不能当作点选择写入验证。Windows 原生焦点仍是图表，当前点经图表 Value 变化报告；Linux grab_focus 返回 true 及 CurrentPoint 可读不等于原生点焦点合格，点级原生焦点与实际读屏播报继续待验。

C2-09 历史冻结源码 v3 曾在 Windows 与 Ubuntu 26.04 WSLg 分别通过 946/946 项测试、578/578 项 Demo 检查、163/163 项 Usage 检查，以及功能包/主题包两类全新缓存 NuGet 消费者各 213/213 项；Release 构建零警告/错误，基础、包消费与 API 三阶段退出码均为 0。两平台 589 个声明输入逐项一致。已验证软件子集通过，完整原生桥接门槛仍为 false；正式身份与原始限制见[C2-09 历史报告](charts-c2-accessibility.md)、[验证清单](charts-c2-accessibility-validation.json)与[API](charts-c2-accessibility-api.txt)。

C2-08 的双平台 892/504/147/183、C2-07 的 860/420/131/163、C2-06 的 821/366/119/147 与更早结果保留原批历史身份。本批未测正式性能/内存，百万点命中尾延迟继续留 C4；填充图案、系统高对比偏好接入、Windows 原生选中集合、Linux 已选子节点枚举与嵌套点选择写入、原生点焦点与实际读屏器、系统保存选择器真实交互、物理设备、present 与官方许可运行参考仍开放。 全库 997 项仍为历史数量，不推算当前总数。

## 共用基础已完成

- 五个控件各自提供功能程序集与独立默认主题程序集，功能包不反向依赖主题包；Markdown 另有可选 TextMate 适配包。
- 统一 Demo 提供概览与五个控件页面、亮暗切换及页面生命周期管理；五个控件均提供独立 Usage 应用。
- 中央依赖版本、锁文件、Release 构建、分控件测试、打包与隔离包消费脚本已建立；Windows/Ubuntu 验证和 TreeDataGrid Windows 原生探针的 CI 配置已提供，实际 CI runner 结果仍需执行确认。
- 独立 NuGet 消费脚本覆盖五个控件；Charts 两种全新缓存、无源码引用的专属消费者已在 Windows 与 Ubuntu WSLg 对应 Linux 环境通过。

## 五个控件的完成情况

各节附对应 Demo 场景图，可点击查看原图。图片采集日期、主题及原始报告见 [截图说明](screenshots/README.md)；图片展示采集时的界面，当前功能边界以各节文字为准。

### TreeDataGrid

[![TreeDataGrid Demo：自定义列中的按钮、颜色、时钟、开关和进度条](screenshots/treedatagrid.png)](screenshots/treedatagrid.png)

暗色主题，自定义列模板中的控件组合；采集于 2026-10-13。

已完成：

- 平面/层级 `Source`、`ItemsSource`、XAML 列声明、MVVM 选择/当前单元格/编辑命令；文本、复选框、模板、行号和树展开五种列。
- 树展开与同步按需加载，行/单元格选择、暂存编辑、转换与校验、排序和过滤。
- 冻结列、行列虚拟化、固定/可变/自动行高；列显隐、网格线、空内容、键盘树导航与按实际高度翻页。
- 同冻结区列拖动重排、源路径定位、嵌套层级绑定、类型化读写、受控同表行移动；亮暗主题、模板示例和自动化 peer/provider。

待完善功能：

- 异步加载目前只有设计与 11 项私有原型测试；产品 loader API、取消与生命周期、加载/错误反馈和远端示例仍待实现。
- 选区复制、列布局保存/恢复、多列排序、保留过滤祖先、跨控件拖放，以及服务端分页、分组汇总、导出和批量粘贴属于后续增强。
- compiled binding 的显示能力不代表任意自动写回；写回需使用受支持的 ReflectionBinding、`ValueSetter` 或类型化访问器。默认行移动受集合类型及排序/过滤状态约束。

待平台验收：真实中文 IME、多 DPI/多屏、物理拖放、焦点/剪贴板、屏幕阅读器和自动化桥、原生帧率/输入延迟、长期耐久、干净部署与目标麒麟。已有自动化 peer 不等于完整原生 Grid/Table pattern。

依据：[P6 状态](treedatagrid-p6-status.md)、[使用说明](treedatagrid-usage.md)、[无障碍边界](treedatagrid-accessibility.md)、[后续执行计划](treedatagrid-next-development-plan.md)。

### VirtualKeyboard

[![VirtualKeyboard Demo：英文键盘及多种 TextBox 输入目标](screenshots/virtualkeyboard.png)](screenshots/virtualkeyboard.png)

暗色主题，英文键盘、双向绑定输入框及数字选项；采集于 2026-10-13。

已完成：

- 数字键盘与美式英文 printable ASCII、Shift/Caps、小数点/负号选项；显式指定同窗口 `TextBox` 目标。
- 字符/选区输入、删除、光标/Home/End、全选、原生 Undo/Redo、TwoWay 与 MaxLength；Enter/Tab 按 `AcceptsReturn` / `AcceptsTab` 在目标换行/插入与宿主请求之间单路径处理。
- 单活动指针、移出/捕获丢失取消、可控长按重复；目标、窗口、祖先、布局、模板和主题失效时停止旧输入。
- 独立亮暗主题、资源覆盖、自定义模板、Button 名称/角色/Invoke；双包、Demo、Usage 与真实包消费者。

待完善功能：中文拼音/词库/候选等可选输入引擎，自动焦点发现、自动显隐/避让的 `VirtualKeyboardScope`，第三方编辑器/TreeDataGrid 编辑器适配、更多布局和完整剪贴板工具栏。系统 IME 活动组合期间与虚拟键盘并发输入目前不在支持范围。

待平台验收：物理触屏与多触点、硬件/虚拟输入交替、真实系统 IME 提交/取消后的输入、100%/125%/150%/200% DPI、完整读屏和麒麟。已有 Windows 与 Ubuntu/WSLg 原生窗口程序化验证，不能外推全部设备。

依据：[实施状态](virtualkeyboard-status.md)、[开发计划](virtualkeyboard-development-plan.md)、[平台清单](virtualkeyboard-platform-acceptance.md)。

### Markdown

[![Markdown Demo：原生阅读、列表、引用和代码高亮](screenshots/markdown.png)](screenshots/markdown.png)

亮色主题，基础阅读场景及选择、复制、流式追加入口；采集于 2026-10-15。

已完成：

- 原生只读阅读：标题、段落、强调、列表、引用、代码、pipe/grid 表格、任务列表、提示块、脚注和 Emoji 等已有呈现器的语法。
- 全文字素选区、跨块/离屏选择与复制，链接及代码复制；流式追加、跨线程会话、有界调度、旧结果拒绝、底部跟随与阅读锚点。
- 图片 loader、高亮接口及可选 TextMate、语义样式、自定义 AST 转换/原生 renderer、视口回收与资源释放；三包、Demo、Usage 和独立包消费。
- M6 软件与已测原生组合验收通过，M7 可执行软件验收、包消费及证据归档完成；整体设备矩阵仍未完成，尚未正式发布。

待完善功能：任意 HTML/CSS、数学、Mermaid、symbol 完整支持，ColorCode 第二高亮适配，以及完整 SVG/认证图片/缓存适配。Markdown 写回、编辑、撤销、分页、打印及 PDF/Word 导出仍是后续工程；原生 UIA TextPattern 当前不支持。

待平台验收：实体 Linux X11、原生 Wayland、麒麟、物理键鼠/触屏、完整读屏、多 DPI/多屏与中文/Emoji/RTL/字体组合，公网 HTTPS 图片、长期运行/峰值内存/低端设备，以及官方参考控件同机对照。

历史性能结论须保留原口径：Windows 五轮按原 50ms 门槛通过；Linux 五轮按明确选择的 51ms 长会话 UI 政策通过。Linux 第四轮原值 50.6962ms 超过原 50ms，原失败仍保留；默认门槛仍为 50ms。两平台已有各 43 项当前冻结版本 smoke 通过，真实文本复制及恢复通过，这些结果不代表本次重新执行原生性能或全部设备验收。

依据：[实施状态](markdown-status.md)、[公共契约](markdown-contracts.md)、[平台记录](markdown-platform-acceptance.md)、[测试交接](markdown-test-handoff-20261015.md)。

### Charts

[![Charts Demo：折线图、分组柱状图、散点图与交互选项](screenshots/charts.png)](screenshots/charts.png)

亮色主题，三类图表、双组数据、图例和缩放/平移选项；采集于 2026-10-15。

已完成：

- 普通/平滑/三种阶梯折线、面积/范围面积、线/面积/柱的正负堆叠与百分比、横竖分组柱、范围柱/瀑布/Pareto/Dot/Lollipop/Variance、基线/宽度/显示排序及原始值标签、散点大小/颜色编码、ScatterLine/XY Bubble、面积/直径尺度及绘制顺序和基础混合绘制，数值/类别轴、刻度/标题/格式/网格及基础图例；C3-04 饼/环/半环/部分弧/多环、标签连线/选择与过渡已实现，正式双平台冻结软件验证已通过；C3-01/C3-02/C3-03 保留原批证据身份。
- 可观察集合更新、点/柱悬停提示、按横轴/纵轴/双轴滚轮缩放、鼠标平移、重置与视口状态。
- 数据映射/INPC/批处理/稳定身份快照、逻辑树与资源继承、compiled AXAML/SeriesSource/状态与命令；日期/对数/多轴及 renderer/projection 扩展。
- 次刻度/网格、Auto 标签、断轴/区间、独立轴样式和视图；typed 不可变值缓冲、稳定身份与精确候选索引。数值投影、轴文字、有限 recorder geometry 和 unknown-source 块复用已有受限优化，原始分布和内存代价见尾延迟报告。
- 浮动/对齐图例、可取消 SelectionChanging、矩形/椭圆/箭头标注及独立 Demo；绑定取消保留规范状态，源/配置清理不可取消。
- Tooltip 内容模板、SharedY、方向十字线及多轴读数；原始 Item/SourceIndex/StableId 与 MVVM 状态保留。
- 线段及真曲线/阶梯插值、范围下界和横竖/逐点/聚合 Trackball；现有线/面积/范围保留原始身份和 RawPoint/BaseValue/StackValue，独立系列选择、前置取消、不可变 TwoWay 状态和未选系列淡化。
- 横竖独立范围条及主轴双向连接、键盘/拖柄/窗口移动，多图主轴 Normalized/DataCoordinates 与按轴同步；跨图游标和未来图族不计入当前范围。
- 触摸单指平移、累计双指捏合/移动质心、延迟tap与多触点取消、连续双轴小数滚动、模式/修饰键/灵敏度、滚动容器实际Offset仲裁及窗口失焦/Escape/重入清理；C2-05 已有历史功能验证，本批继续回归。
- 圆角矩形/专属形状标签、中心数据半径椭圆、无占位系列的标注专用轴及被动 PlotAreaContent；源码、绑定与 Demo 已接入，本批当前功能验证通过。
- 入场/数据/视图动画，时长/缓动/延迟、暂停/取消/减少动态效果、旧/新点数预算、同版本快照发布、重入与目标视图联动；当前帧用于命中、模板、标注及 PNG。
- PNG/JPEG 同步与异步导出，固定/推导像素尺寸、DPI、背景/透明度、JPEG 质量与预算；UI 捕获当前视图/暂停帧，后台编码写入，单图在途租约拒绝重复操作。流保留调用方所有权，文件失败/取消保留原件；取消不发布失败事件，普通失败在 UI 线程报告。Demo、编译绑定 Usage 与两类包消费者已接入。
- 点级辅助节点保留原始 Item/StableId/SourceIndex，名称和值使用对应轴格式化，提供选中、可用、绘图区外状态、DIP 边界、可取消选择和独立命令；默认 64 行分页、256 点缓存及分页操作。独立 CurrentPoint 键盘浏览、数据/轴范围摘要与显式亮暗高对比已加入 Demo/Usage。Windows UIA 树/点模式操作及 Linux 独立 AT-SPI 总线的 9 点/边界/click/grab_focus/分页已实查；这些是已验证子集，Windows 原生根选中列表和 Linux 已选子节点枚举未过、嵌套点选择写入未验，完整原生门槛未过。Windows 原生焦点仍为图表，当前点通过图表 Value 变化报告；Linux grab_focus 调用不等于原生点焦点资格。

待完善功能：

- C3-01 异构插值/X/有效段拓扑堆叠的连续共边、范围下界独立点身份与更完整官方 API 尚未提供；同符号厚度与共边只在明确兼容范围保证。线/面积范围及堆叠拒绝 CategoryY；非零对数域外下界保留 Gap。C3-02 已实现的比较系列仍缺圆角/独立柱描边和完整逐点样式；值排序限自动类别轴，瀑布限源顺序，Pareto 限非负有效项降序及独立线性累计轴。C3-03 大小/颜色图例与完整官方 API/性能仍有差距，C3-04 进阶标签/图例/模板及完整圆形无障碍仍有差距，C3-05 极坐标进阶轴/布局/模板和全 C2 适配仍有差距，C3-06 仪表进阶数值过渡/布局/模板和全 C2 适配仍有差距，C3-07 完整交互/模板/复杂布局及更多图族与专属数值规则继续待实现；官方混合系列自动类别回退与本库显式轴/Gap 策略仍有兼容差异。
- 类别图例/标签高级布局、未来图族 Trackball/选择/范围/辅助语义适配、内嵌范围概览/每轴独立自动范围、跨图游标与选择同步、未来图族标注、系列独立时序动画、静止指针读数随动画刷新及系统减少动态效果自动接入；辅助功能的Windows 原生选中集合、Linux 已选子节点枚举与嵌套点选择写入、独立系列选择辅助模式、原生点焦点/实际读屏播报、系统高对比偏好与填充图案仍开放。导出的跨图全局预算及大源无既有索引时的远距离身份迁移/全域虚拟化留 C4；系统保存选择器与物理触摸/触控板仍待设备验收。
- LOD/分层几何缓存、真正增量更新和持续实时；按用户决定后置到 C4 的历史独立百万点 hit p95 `0.7649→1.1235 ms`、柱图 warmup first `38.7958→59.4373 ms` 等仍需处理。clear residual **1,050,960 B** 是真实保留代价；Windows/WSLg 两主题的柱图/圆形标记 Direct/Recorded 像素对照各 948 对通过。目标通过不表示非目标指标或完整性能回归通过，Headless 结果也不等同于正式 native 性能门槛。

待平台验收：Windows 真实输入与呈现性能、Ubuntu 22.04/24.04 物理桌面/麒麟、触屏/精密触控板、系统惯性/手掌拒绝、DPI/多屏、真实读屏器、Windows 原生选中集合/Linux 已选子节点枚举与嵌套点选择写入及原生点焦点，以及保存选择器/portal 的实际操作；Windows UIA 外部软件探针和 Ubuntu 26.04 WSLg 的合成结果不能替代这些目标。官方参考宿主仍缺可执行许可条件。AX12 兼容差异、C2/C3 整阶段、C4–C7/正式性能继续开放。C2-04 记录的 4 个原始探针 DLL 缺口（8 处历史引用）继续开放，不用新编译文件替代。见[C3-07 当前报告](charts-c3-analysis.md)、[C3-06 历史报告](charts-c3-gauge-sparkline.md)、[C3-05 历史报告](charts-c3-polar-radar.md)、[C3-04 历史报告](charts-c3-circular.md)、[C3-03 历史报告](charts-c3-scatter-bubble.md)、[C3-02 历史报告](charts-c3-bar-comparison.md)、[C3-01 历史验证](charts-c3-line-area.md)、[C2-09 历史验证](charts-c2-accessibility.md)及[历史尾延迟记录](charts-tail-latency.md)。

依据：[开发计划](charts-development-plan.md)、[功能对照](charts-official-alignment.md)、[性能与平台计划](charts-performance-platform-plan.md)。

### PdfViewer

[![PdfViewer Demo：缩略图、搜索结果和 PDF 阅读工具栏](screenshots/pdfviewer.png)](screenshots/pdfviewer.png)

亮色主题，内置三页 PDF、缩略图与全文搜索结果；采集于 2026-10-15。

已完成：

- PDFium 路径/字节/流/密码加载（含默认密码提示重试/取消）、真实页面渲染、Unicode 字符坐标、全文搜索与分层目录。
- 完整阅读面（P2 收口，2026-10-22）：单页/连续/双页/双页连续及封面规则、页标签、适配宽/整页/高与 ActualSize、可配置缩放上下限/步长、阅读位置保存接口；链接与完整内部目的地（含 Fit 族视图参数、FitB 内容盒）宿主可拦截；用户书签模型与会话内操作（与目录分开）；渐进搜索/进度上限/清除、页与全文提取、PNG 页面导出（独立解码逐像素比对）；细粒度工具开关、侧栏布局与字符串本地化、明暗主题与可选 Fluent 映射；快捷键契约、触摸捏合与触控板缩放。
- 可见页及邻页调度、缓存、旧请求取消与文档生命周期管理。当前页面预算为 96 MiB、单边 4096 像素及单页约 4MP 上限，缩略图最多 64 张。
- 功能/主题双包、统一 Demo、内置三页样例、Usage 与隔离包消费者；Windows x64 与 Ubuntu 26.04 WSL2 x64 各有原生后端 11/11、原生窗口 22/22、Core 111/111、Headless 82/82 证据，双包全新缓存消费者 153/153（含 27 项 `p2*`）。

待完善功能：

- 用户书签 PDF 持久化（P4）、保存/另存为/撤销重做、批注编辑、表单、真正内容删除、Windows 打印分享与 Linux CUPS；OCR、移动端与浏览器也未实现。
- 大文件数据源、全局优先级/公平调度、可配置总预算、高清渲染与大选区性能指标（P3）。

待平台验收：Ubuntu 24.04 CI 远端 run（工作流已配置未执行）、麒麟 x64/ARM64、Windows ARM64/x86、macOS、DPI/多屏、真实触摸屏/触控板、Windows IME、Linux IBus/Fcitx5 与读屏辅助技术。WSL Xvfb 证据是 CI 显示语义，不是实机桌面；打包原生 RID 资产不代表对应平台已通过。G2 自动化门槛已具备证据，待上述外部验收闭合后终判（见[开发计划](pdfviewer-development-plan.md)）。

依据：[阅读版状态](pdfviewer-status.md)、[开发计划](pdfviewer-development-plan.md)、[性能计划](pdfviewer-performance-plan.md)、[平台计划](pdfviewer-platform-plan.md)。

## 下一步执行顺序

| 顺序 | 范围 | 具体工作 | 完成标准 |
| --- | --- | --- | --- |
| 1 | 全仓基线 | 完成当前提交的锁定还原、Release 构建、全部测试、产品打包及独立包消费；触发配置好的 CI，补 Charts 专属消费者 | 记录源码/产物对应关系、实际退出结果和测试数量；Windows/Ubuntu runner 均有可追溯报告 |
| 2 | 真实平台 | 按控件清单补 Windows 物理输入、IME、DPI/多屏与读屏；准备 Ubuntu 实体/Wayland 和麒麟 x64/ARM64 | 每个组合登记系统、后端、字体、缩放、设备和结果；缺环境项继续待验 |
| 3 | 功能开发 | 按下面的控件里程碑推进，每次产品改动补对应回归/包消费/API 与原生 smoke | 新功能可在消费项目与示例复现，错误/取消/释放边界有验证，文档同步更新 |
| 4 | 性能与耐久 | 完善 Charts/PdfViewer 测量，补 TreeDataGrid 原生长期耐久和 Markdown 目标设备/长期内存 | 使用目标机器、固定语料和可追溯产物；新产物重新取得适用性能证据 |
| 5 | 预发行收口 | 依据验收结果冻结 API/模板，完善迁移、许可、支持矩阵和发行说明 | 满足相应控件发布门槛后，再单独决定版本升级和包发布 |

各控件功能里程碑：

| 控件 | 首个里程碑 | 后续顺序 |
| --- | --- | --- |
| TreeDataGrid | 按现有 E1 设计完成异步 loader 的取消、生命周期与反馈，替换仅有私有原型的状态 | 远端示例及包消费 → 复制/列布局等增强 → 根据验收结果冻结 API/模板 |
| VirtualKeyboard | 中文 C0 原型：引擎/许可/RID、预编辑/候选/提交取消和系统 IME 互斥 | 固定可选插件 API → 完整中文链路；Scope 可独立推进，再适配其他编辑器 |
| Markdown | 补当前只读/流式功能的目标设备、真实读屏、DPI 和长期运行证据 | 自定义 renderer/图片组合与超长单块专项 → 按需求单独规划扩展和编辑导出 |
| Charts | C3-07 漏斗、面积比例金字塔、Heatmap/色标/缺失、Pictorial 三模式及 Theme river 堆叠底座软件范围已实现，Demo/Usage/包消费及正式双平台冻结验证通过；C3-01～C3-06 保留原批证据，完整适配/无障碍/物理/性能出口保留 | C4-01 数据与视图分层失效；C3 各族残余继续追踪；C2 收尾与平台专项并行 → C4 性能/增量/LOD → C5–C7 |
| PdfViewer | P2 完整阅读已收口（G2 自动化门槛具备证据，待设备验收终判） | P3 性能画质 → P4 保存/书签持久化 → P5–P9 编辑及发布；并行补 Ubuntu 24.04 CI 远端 run 与设备输入验收 |

下节保留 2026-10-15 文档、验证及提交工作的历史记录；本批 C3-07 未执行全库提交或推送，计划项按各批证据更新。

## 2026-10-15 提交前验证（历史）

本节保留 997 项全库验证的原始身份，不与各批 Charts 专项数量合并。

本次已实际完成锁定还原与 Release 构建（0 警告、0 错误），14 个测试项目合计 **997/997** 通过，失败、错误、跳过和未运行均为 0。

| 控件 | 本轮测试项目 | 通过数量 |
| --- | --- | --- |
| TreeDataGrid | Core 127 + Headless 177 + Themes 24 | 328 |
| VirtualKeyboard | Core 25 + Headless 52 + Themes 11 | 88 |
| Markdown | Core 221 + Streaming 60 + Headless 187 + Themes 10 + TextMate 13 | 491 |
| Charts | Headless 53 | 53 |
| PdfViewer | Core 18 + Headless 19 | 37 |
| 合计 | 14 个项目 | **997** |

本次 Windows 本地 `scripts/Verify.ps1` 已完整执行，实际退出 **0**、`passed=true`、`consumerSkipped=false`；本轮 Markdown 守卫合同为实际重新运行，未复用旧结果。对应源码首次提交为 `63d4541`，后续仅补充本文档和 README 的验证结论。

| 验证步骤 | 本轮实际结果 |
| --- | --- |
| 锁定还原与 Release 构建 | 通过，构建 0 警告、0 错误 |
| 产品自动化回归 | 上表 997/997，14 项目，零失败/错误/跳过/未运行 |
| VirtualKeyboard 守卫 | 三份既有原生报告校验通过；41 项守卫合同通过 |
| Markdown 守卫合同 | 134/134 通过，默认 50ms 与显式 51ms 的接受/拒绝规则均覆盖 |
| 产品打包 | 当前 11 个功能/主题/高亮产品项目打包通过，版本均为 0.2.0-preview.2 |
| TreeDataGrid 包消费 | 默认主题及功能独用/自定义模板两条路径通过 |
| VirtualKeyboard 包消费 | FunctionOnly / DefaultTheme 两种全新缓存消费者通过 |
| Markdown 包消费 | 功能独用 76 项、主题及 TextMate 78 项通过；每种各含 18 项内嵌 AXAML 检查 |
| PdfViewer 包消费 | 功能独用 21 项、默认主题 24 项通过，包含真实 PDFium |
| 验证期间软件源一致性 | 656 份清单文件前后相同，比较已完成 |

本地运行编号为 `1ffe3467af7b4a08ad9976ae835d6cd8`，结果文件为 `artifacts/verification/1ffe3467af7b4a08ad9976ae835d6cd8/results.json`；源清单 SHA-256 为 `BB55C1F0BA52414D8C05CD6DEA56EA05F5DA8EB8017FE13BA83275A67E8E6184`。运行中首次隔离消费者依赖下载曾超时，NuGet 自动重试后还原成功，最终完整验证通过。

守卫合同不计入上述历史 997 项产品回归，也不作为原生性能证据；后续 Charts 已完成专属隔离消费者的 Windows 与 Ubuntu26.04/WSLg 本地运行，详见专项记录。远端 CI runner 与物理 Linux 目标设备仍未运行。

原生设备验收和正式原生性能不包含在本轮验证中。

历史文档中的 416、896、907、935 等整仓数量均对应各自当时的源码和项目集合，不能套用为当前总数。完整本地日志和证据位于被忽略的 `artifacts/`，未随源码推送；仓库保留本节摘要及 `docs/validation/` 中已有的阶段材料。
