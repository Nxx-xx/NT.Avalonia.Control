# Charts 性能与 Windows / Linux 验收附件

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

C5-04 当前执行（2026-10-21）：完成EN01–05 CarpetPlot/Hexbin/Smith/Ternary/WindRose及物理单位、正逆投影、双线性网格、分箱/加权频数、原始身份和有界失败；六面板Demo、编译绑定Usage与隔离包消费接入。

C5-04 双平台验证：同一c5-engineering-projections-source-v1冻结候选，Windows与Ubuntu26.04 WSL2/WSLg每平台2236项全套测试、1860项原生Demo、462项Usage，两类新cache消费者各639项；63固定数值+两项100k全量重算、22数学专图/10对96DPI零差及原始来源元数据通过，Release零警告/错误。见[C5-04报告](charts-c5-engineering-projections.md)、[验证](charts-c5-engineering-projections-validation.json)、[API](charts-c5-engineering-projections-api.txt)。

C5-04 后续：下一主项C5-05进阶比较；ST01/03/06–10等剩余统计、完整C5及官方运行时/API/二进制等价继续开放。C4-08和C5-01/02/03独立oracle未重跑；物理平台/GPU/present/长稳、百万点hit p95、A11Y-NATIVE-01及历史时序/长路径/AX06保留。下方历史“下一项”以本段为准。

C5-03 当前执行（2026-10-21）：完成附加Trendline/独立ChartTrendlineSeries的六趋势、MovingAverageSeries四均线、SMA/EMA/WMA/Bollinger四指标对象，明确全源历史、预热/gap/正域/初值/来源及失败规则；Demo、编译绑定Usage和隔离包消费者接入。

C5-03 双平台验证：同一source-v2冻结候选在Windows与Ubuntu26.04 WSL2/WSLg每平台通过2173项全套测试、1714项原生Demo、446项Usage，两类新cache消费者各617项；60固定数值及4项100k完整重算、26数学专图和2对96DPI零差通过，Release零警告/错误。见[C5-03报告](charts-c5-technical-trends.md)、[验证清单](charts-c5-technical-trends-validation.json)和[API](charts-c5-technical-trends-api.txt)。

C5-03 后续：下一主项C5-04工程图与专业坐标。全源追加仍重算，增量/后台指标、正式性能/长稳、完整C5与官方对照继续开放；RSI/MACD未添加。C4-08及C5-01/C5-02独立oracle未重跑，百万点hit p95和A11Y-NATIVE-01仍后置，历史时序/长路径/AX06保留。下方历史“下一项”以本段为准。

C5-02 当前执行（2026-10-21）：完成蜡烛/OHLC/HiLo/HA/Renko/Kagi/Point&Figure及金融日期/价格/成交量/叠加宿主，明确UTC顺序、OHLC有效性、gap、固定阈值、源身份和输出硬上限；七图Demo、编译绑定Usage和隔离包消费者已接入。

C5-02 双平台验证：同一source-v1冻结候选在Windows与Ubuntu26.04 WSL2/WSLg每平台通过2109项全套测试、1584项原生Demo、426项Usage，两类新cache消费者各593项；41固定数值场景及5规模完整重算、44数学专图和16对96DPI零差通过，每平台46张新金融PNG，Release零警告/错误。见[C5-02报告](charts-c5-financial-prices.md)、[验证清单](charts-c5-financial-prices-validation.json)和[API](charts-c5-financial-prices-api.txt)。

C5-02 后续：下一主项C5-03技术指标与趋势；交易所日历/盘中会话、ATR/百分比/逐笔变体、完整C5及官方对照仍开放。100k/2048计时仅单次诊断，本轮未重跑C4-08九阶段或C5-01独立统计oracle；物理平台/present/GPU/长稳、百万点hit p95、A11Y-NATIVE-01及旧时序/长路径问题保留。下方历史“下一项”以本段为准。

C5-01 当前执行（2026-10-21）：完成BoxPlot/ErrorBar/Histogram/Density及Type7分位数、IQR须/离群、normal-z区间、分箱/核密度和相关统计；保留原始组/bin/密度sample身份，补分类首末slot留白、四图Demo和编译绑定/隔离包。

C5-01 双平台验证：同一source-v1冻结候选在Windows与Ubuntu26.04 WSL2/WSLg每平台通过2027项全套测试、1470项原生Demo、410项Usage，两类新cache消费者各574项；独立数值1660、像素247谓词通过（不与产品测试相加），每平台30张新增统计PNG，Release零警告/错误。见[C5-01报告](charts-c5-statistics-distributions.md)、[验证清单](charts-c5-statistics-distributions-validation.json)和[API](charts-c5-statistics-distributions-api.txt)。

C5-01 后续：下一主项C5-02金融价格图；其他统计/工程目录、完整C5及官方运行时/API对照仍开放。100k/2048统计计时仅单次诊断，本轮未重跑C4-08九阶段，不沿用旧普通图数字认证统计SLA。物理平台/present/GPU/长稳、百万点hit p95、A11Y-NATIVE-01及旧时序/长路径问题继续保留；下方历史“下一项”以本段为准。

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


计划及执行更新日期：2026-10-18。

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

C2-08 的双平台 892/504/147/183、C2-07 的 860/420/131/163、C2-06 的 821/366/119/147 与更早结果保留原批历史身份。本批未测正式性能/内存，百万点命中尾延迟继续留 C4；填充图案、系统高对比偏好接入、Windows 原生选中集合、Linux 已选子节点枚举与嵌套点选择写入、原生点焦点与实际读屏器、系统保存选择器真实交互、物理设备、present 与官方许可运行参考仍开放。 本附件保留 C0 固定预算。

任务使用稳定编号 `PERF01` 起和 `OS01` 起。本文的开发和验收工时已分摊到主阶段，不得再与主计划重复相加。C0 的 Headless harness、平台工作流和 raw guard 已实现；Ubuntu 26.04 WSLg 已取得 Linux 执行证据，正式帧/呈现及目标设备仍待取得。下文历史起点和后续目标不自动转为已验收状态。

历史 v15 的固定 controlled 100/100、297 项增加值/75 个描述性标记、20 轮内存与 1896 对像素数据保留[原始报告](charts-tail-latency.md)。C3-02 软件功能及既有回归保留正式双平台冻结历史验证，C3-03 source-v2 保留正式双平台软件验证；C3-04 圆形图保留正式历史证据；C3-05 极坐标与雷达正式双平台冻结软件验证已通过，性能/内存门禁未运行；百万点 hit 尾延迟按用户决定后置 C4，所有历史残余继续开放。

C3-02 的冻结源码、程序集/API 与包消费者身份已由其原批正式验证清单关联；C3-01、C2-09 及辅助技术探针保留原清单历史身份。C2-08 v2 的 1267 个冻结文件、580 个声明输入，以及 C2-07 与更早源码身份继续保留原始记录，包括 C2-06 的 1252/571、C2-05 的 1242/564 和 C2-04 的 1231/556。按需 64 行分页、256 点缓存、不额外枚举源和复用既有身份索引属于功能与成本边界；百万点请求的测试通过不等于延迟、分配或全域虚拟化资格。未建索引的大源远移仅作最多 256 行身份搜索，键盘每个候选系列的每个搜索方向最多检查 4096 行，更完整虚拟化与资源预算留 C4。后台导出 UI 心跳、动画与当前辅助技术软件检查均不能替代吞吐、内存、GPU/backend/present 或真实读屏器验收。

C3-01 的 Smooth/Step/Area/Range/stack 仍按源点构建显示覆盖和几何；Smooth 兼容组共用切线限幅并以场景 Context 弱键缓存，采样堆叠和命中没有 LOD/空间索引资格。原 Linear 快速路径未被新曲线替代，不能把旧三图预算外推到新变体。共同切线的独立代数审计及功能像素通过只证明数值/几何契约，不测 UI 延迟、分配、内存或 present；相关预算留 PERF04～PERF10/C4，未新增或降低 C0 门槛。

C3-02 比较系列的源映射、显示坐标/堆叠覆盖和几何仍按源点准备；值排序与 Pareto 有排序成本，独立累计百分比轴会参与布局和绘制，命中仍需检查各源项。普通旧竖向柱的默认快速路径保留；功能回归及独立数学像素探针不证明新变体的首帧、缩放/命中p95、分配或长期内存预算。新柱/比较形状、横向布局、多轴累计线和动画/导出组合须在 C4 用相同数据、硬件和官方许可参考单独测量，不降低已有性能门槛。

C3-07 新增漏斗/金字塔分段几何、Heatmap 行列/末项归并及颜色扫描、Pictorial 符号几何/重复布局与实际轮廓命中、Theme river 总量扫描和透明底座。复杂矩阵、密集 Repeat、高频源替换与多图资源预算需在 C4 按画质/语义一致条件独立测量；本批功能及像素门禁没有给出新性能、长稳内存或百万点尾延迟结论。下一主项 C4-01 数据与视图分层失效。

C3-06 仪表新增区间/多环枚举、刻度文字与液体波形逐帧几何；微图新增源槽投影、极值扫描、路径/柱和身份命中。实时高频更新、多实例、极端小尺寸、长稳内存、动画/GPU/backend/present 开销均需在 C4 独立测量，本批未重测百万点尾延迟。

C3-05 极坐标场景新增类别并集查找、逐项投影、源序路径、扇区与轨道构建、网格/标签和源身份选择，其源规模/类别规模/标签规模成本需在 C4 单独测量。八图与固定 700×520 业务像素、导出和程序化窗口不代表大源吞吐、首次可用、分配/内存或 GPU/backend/present 性能；本批未重测百万点尾延迟。

C3-04 圆形图新增逐项投影、稳健份额归一化、弧形几何、按身份匹配的过渡以及标签排序/避让，均有源规模或标签规模成本。十二项固定极坐标探针、程序化窗口、暂停帧及 DPI 导出验证软件行为；不测量首帧、尾延迟、分配、长期内存或 GPU/backend/present。C4 继续独立度量，不以圆形图较小的 Demo 数据代替历史百万点回归或正式性能门槛。

C3-03 编码域按轴域有效的完整目标 XY 数据计算，逐点覆盖与可选大小排序有源规模成本；可见形状命中需要检查标记轮廓，ScatterLine 连接源序路径并参与线段读数。默认无编码 Scatter 快速路径保留，但不将旧散点基准外推到新编码、排序、大尺寸边缘裁剪或气泡重叠。固定业务代数和 PNG 正负探针仅验证功能；首帧、p95、分配、长期内存、GPU/backend/present 和目标设备资格继续在 C4/C7 独立测量，百万点历史尾延迟与 C0 预算未改变。

## 1. 已证事实与当前边界

官方 Charts 产品页公开了高效绘制、智能重绘和实时增量更新的方向；官方 API 文档还说明，关闭 `IsRenderDiagnosticsEnabled` 会跳过诊断分配、计数、Stopwatch 采样和诊断覆盖层。这些可以作为机制对标项，不能转换为特定数据量下的 FPS 或 p95 保证。当前核验的资料没有给出可复现硬件、点数、帧延迟、峰值内存 SLA，也没有足够证据确定其内部降采样算法。[官方产品说明](https://avaloniaui.net/charts)、[CartesianChart API](https://docs.avaloniaui.net/api/avalonia/controls/charts/cartesianchart)

官方说明 Charts 采用 Avalonia 的渲染、布局、文字和主题体系。自研可参考框架对自定义渲染的公开建议：减少 UI 线程绘制工作和分配，按输入变化复用绘图对象；不能据此推断商业 Charts 已采用本附件指定的缓存或索引算法。[官方 Charts 发布说明](https://avaloniaui.net/blog/charts)、[自定义渲染](https://docs.avaloniaui.net/docs/graphics-animation/custom-rendering)

计划起点锁定 `.NET SDK 10.0.401`、`net10.0` 和 Avalonia `12.0.3`，已有 [53 项 Charts 自动测试](../artifacts/platform/charts-interaction-tests.xml)及 [31 项 Windows 原生程序化检查](../artifacts/platform/charts-interaction.json)。这些是历史起点证据：视图由程序设置，Tooltip 通过合成路由验证；图片是 `RenderTargetBitmap`，并非系统桌面截图。当时仅取得 Windows 结果；后续 C2 首轮为 258/66/30，CDB6 为 338/100/35，087 为 378/134/51；历史 v15 Windows/Ubuntu 26.04 WSLg 为 504/134/51 及两类独立消费者各 55 项，详见[最新记录](charts-tail-latency.md)。真实物理输入、目标 Linux 22.04/24.04 桌面及正式性能门槛仍未验收。默认无悬停基准也不代表 SharedY 首索引或内容模板的百万点交互资格。

制定计划时的静态审计起点（历史）：`GetScene()` 为系列创建数据快照并汇总范围；失效后会再次准备；折线仍遍历原始点，命中仍遍历数据，柱命中还需要计算分组间距。当时的场景缓存和订阅释放是可保留的基础，尚未等同于视口裁剪索引、降采样、增量数据流水线或正式性能诊断。该段不是当前实现或实测瓶颈排名，实施前须重新检查源码。

## 2. 平台目标与版本交集

官方当前 Avalonia 12 支持表将 Windows 11 24H2 x64/ARM64 列为 Tier 1，Windows 11 22H2、Windows 10 22H2 列为 Tier 2；Ubuntu 16.04～24.x 为 Tier 2。**本项目选择 Ubuntu 22.04/24.04 作为自己的验收目标，不将其描述为官方 Tier 1。** 精确版本和依赖支持须在冻结及发布时复核。[Avalonia 支持矩阵](https://docs.avaloniaui.net/docs/supported-platforms)

Linux 默认路径仍是 X11；Wayland 桌面默认经 XWayland 运行。原生 `Avalonia.Wayland` 从 12.1.0 起需要显式启用，仍被官方描述为早期/试验后端，`UsePlatformDetect()` 不自动选择它，`UseWayland()` 也不自动回退。当前 12.0.3 不能据新版本文档宣称已有原生 Wayland。原生 Wayland 的 EGL、输入等具体能力按实际升级版本和 compositor 单独验收。[Linux 集成指南](https://docs.avaloniaui.net/docs/platform-specific-guides/linux)、[12.1 发布说明](https://avaloniaui.net/blog/release-12-1)

还需检查 `.NET + Avalonia + Skia/HarfBuzz + OS` 的交集。当前 .NET 10 支持表包含 Ubuntu 22.04/24.04；Windows 10 列表侧重特定 Enterprise/IoT 长期服务版本，不能从 Avalonia 的 Windows 10 22H2 条目直接推出普通 Windows 10 22H2 仍获得 .NET 10 厂商维护。Windows 10 的实际 edition、构建、补丁和运行时部署策略是 OS01 的冻结项。[.NET 10 支持系统，核验时更新于 2026-09-28](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md)

| 目标组合 | 计划地位 | 退出要求 |
| --- | --- | --- |
| Windows 11 24H2 或冻结的受支持后继版本，x64 / Win32 | 主支持基线 | 原生功能、真实输入、GPU/软件路径、DPI、正式性能与包消费全部完成 |
| Windows 10 x64，精确 edition/build 待 OS01 冻结 | 兼容验收目标 | 保留 Windows 10 需求；确认运行时支持交集并真机验证后才写入对应发行承诺，未确认时标 `pending-runtime-policy` |
| Ubuntu 22.04 LTS x64 / X11 | Linux 支持基线 | 独立 Linux 构建、原生窗口、系统依赖、真实桌面和正式性能 |
| Ubuntu 24.04 LTS x64 / X11 | Linux 主基线 | 同上；不能用 22.04 或 WSL 的结果替代 |
| 上述 Ubuntu 的 GNOME Wayland 会话 / XWayland 客户端 | 兼容会话路径 | 记录实际 X11 后端和 XWayland；焦点、捕获、滚轮、DPI、导出等专项通过；与原生 Wayland 分开 |
| 原生 Wayland / 明确的 GNOME、KDE compositor | 升级后的试验轨道 | OS13：固定至少 12.1.0 的候选版本、显式后端、EGL/驱动与缺项清单，不默认升格为主支持 |
| 麒麟发行版、版本/补丁、桌面及 CPU 架构 | 显式兼容工作包，待确认目标 | OS11；x64、ARM64、LoongArch 等分别确认，不能由 Ubuntu 或其他架构结果推断 |
| Windows/Linux ARM64 及其他发行版 | 按用户目标增加 | 原生设备、RID 和依赖审计形成独立矩阵；交叉编译成功只证明产物生成 |

Xvfb、Headless、WSLg、虚拟机、真实桌面及真实 GPU 分别记录。Xvfb 通过不代表桌面输入或 GPU 性能通过，Windows 通过不代表 Linux 通过，XWayland 通过不代表原生 Wayland 通过。

## 3. 固定基准配置与数据集

### 3.1 C0 必须冻结的配置

主性能配置建议使用一台 Windows 和一台 Linux 固定 x64 设备，至少四个物理 CPU 核心、16 GiB RAM、SSD、明确型号/驱动的集成或独立 GPU、60 Hz 显示器。C0 将建议替换为具体设备 ID 和配置文件；不存在“任意满足最低规格的机器均通过”的承诺。同机官方对照在各自 OS 上执行，不拿 Windows 的官方结果对比 Linux 的自研结果。

- Release、相同 .NET/Avalonia 依赖和 JIT/GC 设置、相同电源策略；禁用调试器和诊断覆盖层。诊断打开与关闭另做开销差分，正式成绩使用冻结设置。
- 默认窗口客户区 `1280×800 DIP`，单图表 `1200×650 DIP`，DPI `1.0`，60 Hz，记录实际 `PlotArea` 和物理像素大小。DPI `1.5/2.0`、小尺寸 `640×360 DIP` 另成场景，不混入默认统计。
- 数值轴各约 6 个刻度、网格开启、字体 12 DIP、线宽 2 DIP、固定亮/暗主题、相同图例/Tooltip/标签配置；动画默认关闭后单测开启模式。文字采用固定版本的 Latin 字体和 `Noto Sans CJK SC` 回退，记录字体文件哈希及最终回退字体。
- 密集折线基准关闭 marker；有 marker 的基准明确开启数量和大小。散点不得套用“关闭所有 marker 的折线”成绩，柱图不得使用折线降采样成绩。
- 锁定实际窗口后端与渲染路径；GPU 和强制软件渲染各自统计。记录请求配置和实际选择，发生回退必须可见，不能仍以 GPU 场景名出成绩。
- 比较缓存、降采样和图元数量时同时保存 `inputPointCount / validPointCount / visiblePointCount / renderedPointCount / primitiveCount`，禁止仅报告最终绘制的少量点。

### 3.2 数据集与固定动作

所有数据集使用固定 seed 和 SHA-256 manifest，保存生成器版本、点数、分布、排序/重复 X、NaN/gap、峰值位置及预期命中原始索引。生成与载入耗时单列；不能每次更换随机数据而仍沿用同一 corpus ID。

| 场景 ID | 数据与质量设置 | 固定操作 |
| --- | --- | --- |
| S10-L | 单系列 10,000 点折线，单调 X，正弦+噪声+孤立尖峰，marker 关闭，精确绘制轨道 | 10 秒预热后 30 秒轮缩放/往返平移，60 次输入/秒；全范围、1/10 视口分别运行 |
| S10-S | 单系列 10,000 散点，均匀、聚类、重合三份数据，marker 4 DIP | 相同输入轨迹及 1,000 次空间查询；不能以散点抽样隐藏点而不披露 |
| S10-B | 两系列合计 10,000 条柱数据，每系列 5,000 条，共 5,000 个共享分类；另含正负与重复类别子集，标签有避让 | 同样的视口、命中及数据替换；验证零基线和重叠柱选择 |
| S100-L | 单调 X 的 100,000 点折线，含尖峰、缺失段，marker 关闭 | 开启版本化降采样，全范围及局部视口交互；另测关闭降采样的退化曲线 |
| S1M-L | 单调 X 的 1,000,000 点折线，marker 关闭，降采样开启 | 新图表首次可用、首个有效输入、缩放进局部数据；覆盖单调/非单调源分开的压力轨道 |
| S-RT | 4 条实时折线，每条 2,500 点/秒，总 10,000 点/秒；每 50 ms 批次共 500 点；总保留 1,000,000 点的环形窗口 | 跟随末尾、暂停跟随、恢复、同步 hover/pan；30 分钟日常压力和 8 小时发布长稳 |
| S-LIFE | 2/4 图表、主题/DPI/resize、100 次 attach/detach/换源/移除系列 | 释放订阅、缓存、索引、后台任务与迟到结果；隐藏图表策略单测 |
| S-ANIM | 当前三图各 100/1,000/10,000 点与多系列；None/Entrance/Data/Viewport、Linear/Cubic、delay/pause/reduce，另含 10,001 点及旧大新小预算跳过 | C4 待测：入场、替换/同版本发布、连续 retarget、视图/多图、暂停/恢复/取消、隐藏/卸载；记录 UI/渲染/present、分配/残留及质量，默认关闭基准与启用模式分报，本批功能 smoke 不计为性能通过 |
| S-EXPORT | PNG/JPEG，同步/异步、固定/推导尺寸、96/144/192 DPI、透明/背景、JPEG 质量及同图/多图并发 | C4 待测：UI 捕获、不可分编码段、写入与取消延迟分报，记录峰值/残留、文件失败恢复及全局在途预算；原生受控写入时 UI 心跳不代替性能门禁。旧原生 PNG 与新异步编码路径的透明 RGB 量化边界单列，不更换同路径精确像素门槛 |
| S-FAMILY | 后续热图、金融、层次、地图、时间轴等分族 manifest | 以单元格、蜡烛、节点/边、地图顶点、任务条数计规模，单独冻结各族门槛 |

S10 三种类型独立报告；C0 可按柱图真实可读性调整 S10-B 的显示密度，但不能降低输入规模后仍声称同一基准。非单调 X、逐点样式、复杂标签、阴影、动画、对数轴、时间轴、选区和导出是单独配置；不将单调无 marker 折线的承诺扩展为所有 70+ 类型的承诺。

## 4. 计时边界、拟定预算与对标判定

### 4.1 必须分开的时间线

每次数据 revision 或输入都带 `requestId / revision / frameId`，用同一个 monotonic 时钟记录原始 ticks 及频率。数据生成、文件 IO、队列等待、UI、后台准备和渲染分别记录，重叠区间不相加为“总时间”。

| 指标 | 开始 → 结束 | 不允许的替代 |
| --- | --- | --- |
| `prepareMs` | 控件接受源 revision → 快照、范围、裁剪/LOD、命中索引准备完成 | 只计 geometry 构建，漏掉复制、索引或排队 |
| `uiBuildMs` | UI 接受待应用 revision/输入 → 更新场景并完成本次 UI 布局/绘图记录 | `InvalidateVisual()` 返回当作绘制完成 |
| `submitMs` | UI 场景就绪 → 对应合成批次提交 | 后台计算结束当作提交 |
| `framePipelineMs` | 本帧有效输入或 revision 被 UI 接受 → 对应 composition/render 完成 | 只用 `Render()` 函数计时声称完整帧耗时 |
| `requestToPresentMs` | 输入/源被宿主接受 → 平台证实对应帧 present | composition 的 Rendered 回调或离屏位图当作物理呈现 |
| `firstUsableToRenderMs` | 已物化源交给新图表前 → 相同 revision 首个完整图形 render 完成，且命中索引可查询 | 先画空坐标轴或旧图再宣布首次可用 |
| `firstUsableToPresentMs` | 相同开始 → 可验证的首次可用帧 present | 上项加上猜测的 16.7 ms |
| `hitQueryMs` | 实际查询入口 → 原始点/柱命中结果返回 | 只测已选中点的数组索引读取 |

“首次可用”包含控件内部快照/数据构建、范围/LOD/索引、布局、提交和相应渲染，另报告外部数据生成/加载及进程启动到可用的端到端时间。计时不得在昂贵内部准备后才开始。新进程不等于冷 OS 文件缓存；冷/热缓存条件须写明。

Windows 的系统呈现追踪和 Linux compositor/呈现协议探针由 PERF01 做 PoC，并在报告标出工具、事件类型及关联精度；不能取得对应帧的呈现证据时 `present=null / status=blocked-present`。仍可报告 UI/submit/render 的准确分层结果，但不得据此宣布 present 门槛通过。屏幕视频可辅助核验，不用于伪造精确到毫秒的 present 时间。

### 4.2 拟定工程门槛

下表先用于 C0 验证测量可行性和设备适配，随后冻结 `budgetVersion`；变更预算必须说明原因并保留旧版本失败，不得在看见 C4 结果后无记录放宽。

| 场景 / 指标 | 拟定绝对门槛 | 同机官方对照门槛 |
| --- | --- | --- |
| S10-L / S10-S / S10-B 交互 | `framePipelineMs p95 ≤16.7 ms`；60 Hz 下 `requestToPresentMs p95 ≤33.3 ms` | 各类型、质量、OS 分别 `p95 ≤1.25× reference p95`，同时满足绝对门槛 |
| S100-L，单调线降采样 | `framePipelineMs p95 ≤33.3 ms`；`requestToPresentMs p95 ≤66.7 ms` | 相同质量轨道满足上述 1.25 倍规则 |
| S1M-L 首次可用 | `firstUsableToRenderMs p95 ≤1500 ms`；有可靠 present 时另要求 `firstUsableToPresentMs p95 ≤1600 ms` | 两层分别比较，不能将官方 render 和自研 present 混比 |
| S10/S100/S1M 命中 | `hitQueryMs p95 ≤8 ms`；pointer→Tooltip present `p95 ≤50 ms` | 查询及呈现分别 ≤1.25 倍；原始数据索引/Tooltip 值必须正确 |
| S-RT | 稳态 `framePipelineMs p95 ≤33.3 ms`，输入→present p95 ≤66.7 ms；固定速率全量接收，丢样 0 | 同一窗口、批次与质量下分别 ≤1.25 倍；不得通过悄悄丢数据达标 |
| 内存 | S1M 控件场景相对空宿主的峰值 private 增量拟定 ≤512 MiB；原始输入另记但不从总进程报告抹除 | 同机、同口径峰值 private ≤1.25 倍，并满足自身预算 |
| S-RT 30 分钟 / 8 小时 | 第 10 分钟后，最后稳定窗口的 managed 与 private 各自增量 ≤`max(32 MiB, 稳态基线5%)`；无持续增长趋势 | 两实现分开比较完整曲线；不得只比较结束时一帧 |
| S-LIFE | 100 次循环后 active observer/task/pointer capture 归零；无迟到回写；资源在约定生命周期内释放 | 行为正确性为硬门槛，不能用参考实现也有问题来豁免 |

上表是场景门槛，不等价于“稳定 60 FPS”。同时记录 p50/p95/p99/max、实际帧间隔、超预算帧占比、丢帧、错误/超时和 CPU；不能用平均 FPS 隐藏停顿。单一进程或少量样本不足以作最终 p99 声明。

软件渲染保留相同功能和质量校验，先拟定 S10 `framePipeline p95≤33.3 ms`、S100≤66.7 ms、S1M 首次 render≤3000 ms 的独立档；C0 需冻结明确最低 CPU。不得把软件档通过填入 GPU 档成绩。DPI 1.5/2.0 的预算及图元密度需在 C0 按实际像素面积测定后单列，不默认复用 DPI 1.0 数字。

### 4.3 官方参考可运行性的硬条件

官方与自研使用同一台物理主机、相同 OS/后端/依赖/窗口/DPI/数据、相同 marker/线宽/动画/标签与可验证质量；官方控件包、许可证和参考宿主版本必须可追溯。实现运行顺序使用固定 seed 随机化或 ABBA，避免一方独占冷启动、另一方全是热缓存。

比较精确绘制与降采样时分别标记质量轨道。官方算法不可配置或输出无法达到同一正确性/质量条件时，记 `not-comparable`，保留双方独立结果，不硬算倍率。官方未公开的内部机制不作猜测，第三方库的分数也不能替代官方参考。

**未取得合法官方运行权限、可运行固定版本宿主或同等环境时，对照项为 `blocked-reference`，保留 reference 值为空。** 自研绝对门槛可继续验收，但不得发布“与官方性能对齐”的结论。缺硬件是 `blocked-hardware`；缺 present 是 `blocked-present`；这些状态不能按 0 ms 或通过处理。

## 5. 正确性、降采样与内存契约

降采样只改变显示准备，不能改写用户原始数据或静默改变选区、统计和导出语义。单调 X 折线优先验证按视口物理像素列保留首/尾/最小/最大点的方案；是否再用 LTTB 等算法属于 C4 PoC 决策，不能预先假定其自动保留全部极值。非单调/重复 X、gap、对数无效域、堆叠、金融和逐点样式必须有明确回退或专门算法。

每个显示点/桶记录原始 series revision、源索引或源索引区间；Tooltip、trackball 和选择通过原始数据精查，返回稳定原始索引及真实数值。桶代表点不能冒充未采样原始点。插入/删除/Move/批量 Reset、环形窗口淘汰、重复点、数据换源和取消的旧 revision 均须校验映射；选中已淘汰点按公开契约清除或保留键，不能指向另一点。

质量门禁先于速度：逐桶尖峰和缺失段不被抹掉，端点/连线裁剪正确，固定 DPI 下屏幕包络误差目标不超过 1 物理像素；对不适用包络判据的图族另外定义。文字、抗锯齿和颜色比较使用固定 OS/backend/font 版本的 golden 及几何容差，不能要求不同字体回退的跨平台图片逐像素相同。保留关闭降采样的真值、代表性截图和差异图；错误不能靠更高容差或更低质量换通过。

缓存必须按数据、轴/viewport、布局/字体、主题/brush、DPI 和画质版本区分失效。区分可复用原始快照、视口索引、LOD、geometry、文字、命中和 overlay，避免每次鼠标移动重建全量快照，也不能缓存已失效的公开样式。可变集合仅在约定的 UI 线程访问；后台准备使用稳定不可变快照和 revision，取消/换源后禁止旧结果覆盖新源。

S-RT 明确保留窗口为 4×250,000 点，总 1,000,000 点，不能无限保存 8 小时所有输入再声称有界内存。数据接收与屏幕刷新可合并，但每个样本有顺序号；记录 produced/accepted/retained/evicted/rendered/dropped。拟定至多两个待提交 UI revision；积压达到冻结上限时采用显式背压/错误策略。基准正常负载要求 dropped=0，不允许默默保留最后一个批次。非有界全历史模式另测，不共享环形窗口预算。

内存按字节跟踪源快照、LOD、索引、geometry、文字缓存、在途 revision 和 GPU 资源；单项预算之外保留整个进程峰值。managed heap 下降不等于 native/GPU 资源释放。不得在正式交互/长稳期间强制 GC、清空缓存或重启进程美化曲线；诊断性强制 GC 测试另列。

## 6. PERF 工作项与退出条件

| 编号 | 可执行交付与完成定义 | 主阶段 / 依赖 |
| --- | --- | --- |
| PERF01 | 定义原始 schema、monotonic 计时和 revision/frame 关联；实现 prepare/UI/submit/render/present 分层 PoC；明确各 OS 的 present 能力与 unavailable 原因 | C0；所有性能任务前置 |
| PERF02 | 生成并冻结 S10/S100/S1M/RT/LIFE manifest、轨迹、原始索引 oracle、字体和质量 golden；加入尖峰/缺失/极值/重复 X | C0；依赖 PERF01 |
| PERF03 | 建立合法固定版本官方宿主，冻结绝对门槛、倍率、设备和质量轨道；不满足条件输出 blocked-reference 清单 | C0；依赖 PERF01～02 |
| PERF04 | 数据 revision、批量通知和不可变快照底座；INCC/换源/单点更新正确失效；避免纯 hover/viewport 变化时复制全部源 | C1～C3 定契约，C4 优化；依赖数据绑定契约 |
| PERF05 | 单调 X 可见区间索引、二分定位和边界相邻点；非单调源保持正确回退；度量扫描量、准备时间及额外内存 | C4；依赖 PERF04 |
| PERF06 | 按物理像素预算的 LOD/降采样与原始索引映射；实现质量 oracle、取消、缓存失效及关闭开关；不对其他图族直接套用 | C4；依赖 PERF02/05 |
| PERF07 | 单调点查询、散点空间索引、柱矩形命中，Tooltip/trackball 精查原始源；更新/换轴/DPI 后无旧命中；索引构建计入首次可用 | C3 契约、C4 引擎；依赖 PERF04～06 |
| PERF08 | 区分轴/数据/overlay，复用稳定 geometry、Pen/Brush/FormattedText，控制裁剪与图元数；数据无变化的 hover 不全图重建 | C4；依赖 PERF04～07 |
| PERF09 | 批量实时接收、帧合并、环形窗口、明确背压和样本计数；线程切换与旧代结果有界；证明 10,000 点/秒无静默丢样 | C1 契约、C4 实现；依赖 PERF04/08 |
| PERF10 | 有界缓存和进程资源计数；2/4 图表公平性、隐藏暂停、detach/换源释放与迟到结果取消；记录每图表最长无进展时段 | C4；依赖 PERF04～09 |
| PERF11 | 每个后续图族增加自己的数量模型、正确性与热点基准，如热图纹理、金融可见蜡烛、层次布局、地图顶点；不能只看通用折线分数 | C5～C6 增量；依赖相应图族功能 |
| PERF12 | 固定宿主 benchmark、五进程 runner、原始样本、独立聚合/守卫、baseline compare；UI 工具和 trace 采集不开在另一方单独计费 | C0 骨架、C4 正式；依赖 PERF01～03、OS07 |
| PERF13 | S-RT 30 分钟与 8 小时、100 次生命周期、实时+交互+多实例混合负载；检查资源斜率、积压、丢样和取消 | C4 早期、C7 最终；依赖 PERF09～12 |
| PERF14 | 新增导出单独测高分辨率、峰值内存、字体和源 revision 一致性；导出不得长期阻塞实时接收和输入 | C2 基础、C6 完整压力；依赖导出功能、PERF10 |
| PERF15 | 发布候选冻结代码/依赖/包/数据/harness 哈希；Windows/Linux 各自正式回归；同画质参考倍率和自身绝对门槛逐项裁决 | C7；依赖全部适用 PERF/OS 项 |

## 7. OS 工作项与平台验收

| 编号 | 可执行交付与完成定义 | 主阶段 / 依赖 |
| --- | --- | --- |
| OS01 | 冻结每个目标 OS/edition/build/patch、架构、桌面、.NET/Avalonia 及后端支持交集；Windows 10 运行时策略和麒麟目标缺项显式登记 | C0 |
| OS02 | 审计 win-x64/linux-x64 的 Skia、HarfBuzz 及传递原生依赖/字体；Linux 用 readelf/ldd 检查干净 22.04/24.04 镜像；native 缺失或 ABI 错误可诊断 | C1；依赖 OS01 |
| OS03 | GitHub Windows/Linux locked restore、Release build、Core/Headless/XAML/包消费；固定 SDK/runner 镜像记录和哈希；必需作业缺失/超时不得通过 | C1 起持续；依赖 OS02 |
| OS04 | 增加 Charts 原生 CI：Windows 原生进程、Ubuntu 22.04/24.04 Xvfb 窗口 smoke；独立退出码/监督/图像/原生依赖报告；不冒充实机 GPU | C1 起、C7 归档；依赖 OS03 |
| OS05 | 每个真实桌面测试鼠标滚轮、精细/惯性触摸板、焦点与快捷键、双击重置、拖动出窗、捕获丢失/禁用/detach、窗口切换/最小化；触摸/pinch 按承诺配真设备 | C3～C6；依赖交互功能 |
| OS06 | DPI 100/125/150/200%、跨屏 DPI/刷新率、窗口 resize、小窗口和最大化；CJK/emoji/组合字符/RTL、字体缺失/回退、长标签、culture/日期时区正确；记录实际缩放与字体 | C2 起、C6 完整；依赖字体/轴契约 |
| OS07 | Windows 默认实际渲染器与强制软件、Linux X11 GLX 与强制软件分别验收；EGL/Vulkan 仅选用且版本支持时增加；记录实际 GPU/驱动、回退和显示器/合成器 | C4；依赖 OS02、PERF01 |
| OS08 | PNG/JPEG 及后续矢量/数据导出按最终承诺核验尺寸/DPI、透明度、CJK字体、当前视图/全数据选项；系统保存选择器取消、中文路径、只读/被占用文件和 Linux portal 情形 | C2 起、C6 完整；依赖导出/平台服务 |
| OS09 | X11、XWayland 独立验证焦点/捕获、触摸板 delta、窗口关闭和多屏；Xvfb/WSL 只作补充；必要的键盘/辅助技术语义在 Windows 和 Linux 实际服务下验证 | C3 起、C6 完整；依赖 OS05～06 |
| OS10 | Windows 11、Ubuntu 22.04/24.04 固定硬件正式性能与长稳；适用兼容组合复跑功能；核查 source/build/execution OS、后端和实际进程架构 | C4 早期、C7 最终；依赖 PERF12～13 |
| OS11 | 麒麟兼容任务：确认具体发行版、版本/补丁、桌面、glibc、CPU/RID、驱动与可用 .NET；建立原生包/字体/输入/导出/性能适配和真机报告 | C0 定目标、C6 适配；无目标或设备标 blocked-target/blocked-hardware，不默认支持 |
| OS12 | 在干净目标系统消费正式 nupkg，验证独立功能包/主题包、离线依赖、许可证、卸载释放与升级；发布支持表逐格链接证据，缺项保留明确状态 | C7；依赖 OS01～10 与适用 OS11 |
| OS13 | 原生 Wayland 试验：单独升级候选依赖，记录 Avalonia.Wayland 版本、UseWayland、GNOME/KDE、EGL/dma-buf 路径及功能缺项；无 compositor 时验证明确失败/宿主选择策略 | C0 排期、C6 试验；与当前 12.0.3 主轨分开，不自动升级 |

OS05/06/09 的验收结果必须区分：Headless 注入、原生合成路由、系统注入和真人设备。程序调用 `Viewport` 正确不证明滚轮/pinch 正确；RenderTargetBitmap 正确也不证明窗口被 compositor 实际呈现。

## 8. CI、基准 harness 与报告规则

### 8.1 后续工作流结构

保留现有 `.github/workflows/ci.yml`，新增 Charts 专属工作流作为 OS03/04 交付，而非本次直接修改。

| 作业层 | 建议运行入口 | 能形成的证据 |
| --- | --- | --- |
| PR contracts | 固定 `windows-2025`、`ubuntu-22.04`、`ubuntu-24.04`；SDK 读 global.json，locked restore、Release build、直接 xUnit runner/包消费 | 模型、Headless、AXAML 与包依赖回归；记录镜像版本，不当作 Win11/物理 GPU 认证 |
| PR native smoke | Windows 原生窗口；Ubuntu `xvfb-run` 原生窗口、固定字体和 lib 依赖、需要时 D-Bus session | 实际平台 runtime/窗口/渲染冒烟；Xvfb 标软件或实际测得后端 |
| Nightly desktop | 自托管 Win11 和 Ubuntu22.04/24.04，固定已登录桌面、渲染器和显示器 | 真实系统输入、跨窗捕获、CJK、导出、DPI、30 分钟实时 |
| Release performance | 冻结硬件自托管 runner，固定后台负载、GPU/软件分轨，官方与自研 ABBA | 正式各场景五进程统计、present、倍率、8 小时长稳 |
| Compatibility/experimental | 明确的 Win10、麒麟或原生 Wayland 设备标签 | 对应 OS/架构/backend 的独立证据，不与主轨混算 |

这些 runner 标签可在当前 GitHub 文档核对；其可用性和镜像内容仍应随 C0/发布复核。托管 VM 成绩用于发现回归，不用来签发固定硬件 p95。自托管真实桌面 runner 必须检查会话状态，锁屏/远程会话改变渲染环境时作业应报告不可验收。[GitHub runner 参考](https://docs.github.com/en/actions/reference/runners/github-hosted-runners)

现有测试可沿用项目已经验证的 Release 构建和 xUnit v3 直接 runner 路线；Windows 此环境 VSTest IPC 曾受限。新原生/性能 harness 须增加进程超时、退出码校验、仅监督本次启动的 PID、来源哈希和 `always` 上传失败产物。正式结果不能靠忽略超时或缺报告变成通过。

### 8.2 样本与统计协议

1. 每个 `OS + 后端 + 渲染器 + DPI + 质量 + 数据集 + 操作` 独立统计。五个独立进程，各做 10 秒预热和 30 秒固定输入；预热、实际输入、提交、render 和 present 的计数分别保存。每个进程都必须满足门槛，不能靠合并其他进程的好结果掩盖失败。
2. 首次可用独立跑新进程，C0 至少 30 次，C7 关键场景至少 100 次；预先两次探针校准不进入正式样本。命中每进程 100 次预热后 1,000 次固定查询，至少 10% 发生在数据/viewport 更新后；索引维护成本另归入 prepare/frame，不能被遗漏。
3. 分位数用冻结的 nearest-rank：排序后取 `ceil(p×N)` 位，保存原始值、样本数和算法版本。分别报告每进程和全量派生摘要；p95 不能相加、不能取多个 p95 的平均冒充总体 p95。若提供置信区间，保存估计方法和 seed。
4. 错误、超时、无匹配 frame/present、取消和失效结果以明确状态保留，列出失败率；不得静默删掉慢样本、GC/系统调度停顿或缺失关联。外部干扰可让整个 run 无效，但须保留证据并重新跑整组。
5. S-RT 至少完成各主平台 30 分钟早期压力及一次完整 8 小时发布长稳；每秒资源采样，每分钟/全程各报告尾延迟、积压和丢样。过程包含滚轮/pan/hover、主题/resize、停止/恢复及换源；干净纯流和混合负载分别运行。
6. 最终独立守卫从原始 ticks 和计数重算时间、分位数、预算、内存变化及 data revision/frame 关联；报告 summary=true 不能替代守卫通过。保存未修改 raw、manifest、工具、stdout/stderr、trace、截图/差异图与 SHA-256。

### 8.3 原始报告最小字段

| 字段组 | 必需内容 |
| --- | --- |
| 来源 | runId、commit/source manifest、产品/宿主/原生 DLL 与 nupkg 哈希、数据集/生成器/轨迹哈希、schemaVersion、budgetVersion、官方包/授权是否可运行；不保存许可证密钥 |
| 环境 | build OS/execution OS、准确 edition/build/patch、CPU/核心数/架构、RAM、.NET/Avalonia、GPU/driver、后端/renderer 请求与实际、桌面/compositor、字体哈希、DPI/DIP/物理像素/刷新率、功耗/会话状态 |
| 时间 | Stopwatch 原始 ticks/frequency、request/revision/frameId、队列/prepare/UI/submit/render/present 开始结束、超时/取消/关联缺失原因 |
| 负载 | raw/valid/visible/rendered 点与图元数、LOD方式/预算、marker、标签/动画开关、series数、采样率、批次、保留窗口、produced/accepted/evicted/dropped 与积压峰值 |
| CPU/GC | 进程 CPU 时间增量、`coreEquivalent=CPU秒/墙钟秒`、按逻辑核归一化 CPU%、UI/render 线程可测时间、分配速率/累计分配、GC次数/暂停；不以不同核数机器的单个CPU%直接比较 |
| 内存 | managed heap/live/committed、进程 RSS/working set、private、PSS（可用时）、峰值及时间序列、各缓存/在途字节、线程/句柄/订阅数量 |
| native/GPU | native 分配/保留的可测值与工具、GPU dedicated/shared/resident/显存预算及采样来源；不可获取填 null+原因；`private-managed`只能标估计剩余，不能命名为准确native占用 |
| 结论 | 每场景 absolute/reference/quality/correctness 独立状态、数量、失败/跳过原因、超时、参考倍率、证据路径；汇总不覆盖细项失败 |

Windows private commit 与 Linux private resident/PSS 的来源语义不同，字段须带 `measurementKind`；同机倍率只能用同一口径，跨 OS 不直接比较数值。Linux 可采 `/proc/<pid>/status` 与 `smaps_rollup`，Windows 可采进程计数及平台 profiler；GPU 计数不可用不填 0。

拟定归档目录为 `artifacts/charts-performance/<runId>/` 和 `artifacts/charts-platform/<runId>/`，每次新建不可变目录；包括 source/config/corpus、raw、summary、guard、平台信息和质量图。现有 `artifacts/platform/charts-interaction*` 保留历史身份，不能覆盖成未来性能数据。

## 9. 工时分摊与阶段退出

本附件专项暂估 **34～54 人日**，包含性能测量/引擎优化与双平台专项工具、验收和正常修复，不包括所有图族功能本身、商业参考授权采购等待、目标设备等待或未知麒麟移植。C0 量测后收敛，不按代理数量压缩为日历天。

| 主阶段 | 本附件内含工作 | 暂估人日 |
| --- | --- | --- |
| C0 | 指标/present PoC、设备/版本/语料/参考宿主、门槛冻结 | 4～6 |
| C1～C3 | 数据与交互性能契约、原生依赖/CI、真实输入和平台基础 | 5～8 |
| C4 | 视口/LOD/命中/增量/缓存引擎、正式 harness、双平台性能调优 | 12～20 |
| C5～C6 | 新图族专项、导出、DPI/字体/桌面集成的性能增量验收 | 5～8 |
| C7 | 两平台正式五进程、长稳、官方对比、包消费和不可变报告 | 8～12 |
| 合计 | 已计入主计划对应阶段，禁止再次相加 | **34～54** |

该分摊按人日计工作，不把夜间 8 小时 run 全额计为 8 小时人工；运行时占用、并行设备和日历等待单列。OS11 在具体目标确认后估算，OS13 原生 Wayland 升级与重大后端适配另列候选增量，不隐含在当前版本的支持承诺中。

C0 退出必须有可执行测量契约、设备/版本 manifest、预算版本及 reference 状态；缺 reference 可以继续开发，但保持阻塞项。C4 退出需正确性/质量先通过，主平台 S10/S100/S1M、命中、增量与早期长稳各有原始证据。C5/C6 每新增图族和平台能力即时补场景，不等 C7 才发现成本。

C7 只有在最终同一候选产物的适用绝对预算、双平台真实桌面/后端、质量、生命周期、长稳及包消费完成后才能宣布对应发布范围通过。官方对照缺失只能发布自身成绩和公开机制覆盖情况；Linux/麒麟缺设备只能保留待验收状态，不能由 Windows 的 53/31 项历史结果填补。
