# Charts 官方功能覆盖矩阵

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


核验日期：2026-10-15。适用对象：本仓库 `NT.AvaloniaControl.Charts`，以及其独立 Themes 包和统一 Demo。此文件是后续开发的范围清单；阶段顺序、依赖、验收与排期由 [完整开发计划](charts-development-plan.md) 统一维护。

## 核验口径与当前基线

官方事实来自公开产品页、Charts 总览、各分类侧栏及相关正文。下面的“差距与交付项”和 C0–C7 阶段是本仓库的自主实现计划，不是官方实现细节或官方性能承诺。访问日期均为 **2026-10-15**；网站会继续变动，发布前须重新核验。

公开目录包含独立控件、宿主中的系列、算法变体和组合示例，不能全部当作独立图表类型相加。尤其 Charts 总览正文并未列全 Cartesian 侧栏；侧栏还包含 Dot plot、Lollipop、Range bar、Spline area、Stacked 100% bar、Variance 等项。Scatter 正文另有 Scatter line 变体。官网“70+”是产品描述，本计划不以凑满该数字验收。[官方产品页](https://avaloniaui.net/charts)、[官方总览](https://docs.avaloniaui.net/controls/data-display/charts)、[Cartesian 分类完整侧栏](https://docs.avaloniaui.net/controls/data-display/charts/cartesian/area-chart)。

本仓库实现更新：2026-10-18；官方目录仍沿用 2026-10-15 冻结范围。

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

C2-09 历史批在原三系列上已实现当前三系列的点级角色/名称/只读值/选择与命令、按需分页节点、独立于选择的键盘浏览、可绑定数据摘要及显式亮暗高对比配色，并接入 Demo/Usage。默认每页 64 个源序号，图表自身 LRU 最多强引用 256 个点节点（客户端仍可持有已逐出节点）；保留原始 Item/StableId/SourceIndex，选择继续遵守取消与重入契约。

外部 Windows UIA 已实查 3 个系列、9 个点、Value/SelectionItem/Invoke 的点操作子集及分页；Ubuntu 26.04 WSLg 的独立 AT-SPI 总线已实查 9 个点、边界、click/grab_focus 和分页。完整原生桥接门槛仍未通过：Windows 根 SelectionPattern.GetSelection 返回空集合（托管列表为 1，自定义 selector 的真正附着 ControlPeer 对照也复现）；Linux 预设 canonical 点选择后点 Selected 状态为 true、get_n_selected_children 为 1，但 get_selected_child(0) 返回 null，完整已选子节点枚举未过，嵌套点选择写入仍未验。根 select_child(0) 指向直属系列，不能当作点选择写入验证。Windows 原生焦点仍是图表，当前点经图表 Value 变化报告；Linux grab_focus 返回 true 及 CurrentPoint 可读不等于原生点焦点合格，点级原生焦点与实际读屏播报继续待验。

C2-09 历史冻结源码 v3 曾在 Windows 与 Ubuntu 26.04 WSLg 分别通过 946/946 项测试、578/578 项 Demo 检查、163/163 项 Usage 检查，以及功能包/主题包两类全新缓存 NuGet 消费者各 213/213 项；Release 构建零警告/错误，基础、包消费与 API 三阶段退出码均为 0。两平台 589 个声明输入逐项一致。已验证软件子集通过，完整原生桥接门槛仍为 false；正式身份与原始限制见[C2-09 历史报告](charts-c2-accessibility.md)、[验证清单](charts-c2-accessibility-validation.json)与[API](charts-c2-accessibility-api.txt)。

C2-08 的双平台 892/504/147/183、C2-07 的 860/420/131/163、C2-06 的 821/366/119/147 与更早结果保留原批历史身份。本批未测正式性能/内存，百万点命中尾延迟继续留 C4；填充图案、系统高对比偏好接入、Windows 原生选中集合、Linux 已选子节点枚举与嵌套点选择写入、原生点焦点与实际读屏器、系统保存选择器真实交互、物理设备、present 与官方许可运行参考仍开放。

历史 v15 的固定 controlled 100/100、297 项增加值/75 个描述性标记与 1896 对像素对照保留[原始身份](charts-tail-latency.md)。本批没有重新测量性能或内存；独立百万点 hit p95 `0.7649→1.1235 ms` 按用户决定后置 C4。本文 200 个 ID、顺序及原有来源 URL 保持不变。

状态定义：

- **已实现**：该行明确限定的基础行为已有代码与回归证据；不自动表示相关官方属性全部覆盖。
- **部分**：已有可运行子集，但缺少这一官方功能的关键数据契约、模式或视觉行为。
- **未实现**：本仓库尚无对应能力，需开发和验收。
- **需核验**：公开文档不足以确认精确语义、存在与否或性能；C0 先补证据，不能当作完成。

| 阶段 | 本矩阵中的含义 |
| --- | --- |
| C0 | 固定版本和功能基线，澄清文档空白，建立官方对照数据与场景 |
| C1 | 公共契约、数据适配、轴系统和 MVVM |
| C2 | 交互、图例/标签/标记、模板、分析叠加层以外的共用视觉、导出与可访问性基础 |
| C3 | 常用 Cartesian、圆形、极坐标、气泡、仪表和小图 |
| C4 | 可见区裁剪、增量管线、抽样、命中索引、缓存与性能证据 |
| C5 | 统计、金融、工程与进阶比较图，以及相关数值算法 |
| C6 | 层级关系、地图、运营排程与分析组件 |
| C7 | Windows/Linux 实机兼容、性能对照、可访问性和发布验收 |

## 图表、系列与组合目录

每个表覆盖对应官方分类侧栏中的图表条目；表内再展开已确认的系列或模式。只有正文明确给出类型名时才列 API 名称，其他条目使用官方图表名称，不推测其内部继承关系。所有系列最终还须通过后面的共用能力矩阵和 C4、C7 门槛。

### Cartesian

来源：[完整分类侧栏及 Area](https://docs.avaloniaui.net/controls/data-display/charts/cartesian/area-chart)、[Bar](https://docs.avaloniaui.net/controls/data-display/charts/cartesian/bar-chart)、[Line](https://docs.avaloniaui.net/controls/data-display/charts/cartesian/line-chart)、[Scatter 与 Scatter line](https://docs.avaloniaui.net/controls/data-display/charts/cartesian/scatter-chart)、[Combo](https://docs.avaloniaui.net/controls/data-display/charts/cartesian/combo-chart)。访问：2026-10-15。

| ID | 官方图表 / 系列 / 模式 | 状态 | 差距与自主交付项 | 阶段 |
| --- | --- | --- | --- | --- |
| CT01 | Line / `LineSeries` | 部分 | C3-01 已增 Linear/Smooth/StepBefore/StepAfter/StepCenter、正负堆叠/百分比、原始身份与几何读数；逐点样式及完整官方 API 覆盖仍待补 | C1、C2、C3 |
| CT02 | Bar / `BarSeries`，单系列及多系列并列 | 部分 | C3-02 已增横竖分组、正负/基线、相对与固定DIP宽度、自动类别值排序、RawPoint身份及原始值标签；圆角、独立柱描边、完整逐点颜色/样式和官方API覆盖仍开放 | C1、C2、C3 |
| CT03 | Scatter / `ScatterSeries` | 部分 | C3-03 已补SizePath/ColorPath、面积/直径DIP尺度、固定/自动输入域、数值渐变/直接颜色和绘制顺序，真实XY/源身份及十种marker底座保留；大小/颜色图例、完整官方API/性能与设备资格仍开放 | C1、C2、C3 |
| CT04 | Scatter line / `ScatterLineSeries` | 部分 | C3-03 本库独立ScatterLineSeries固定linear源序连接非单调/重复X，ShowLine可关，XY缺失断线而无效大小只隐藏marker，参与segment/Trackball及大小/颜色编码；完整官方API/性能和物理资格仍开放 | C3 |
| CT05 | Area / `AreaSeries` | 部分 | C3-01 已增可配 Baseline 的五种路径填充、正负/缺失段、多轴/断轴与导出；默认零基线支持 log 原点回退，非零域外下界留 Gap，完整官方属性与设备资格仍开放 | C3 |
| CT06 | Spline / `SplineSeries` | 部分 | 本库以 LineSeries.Interpolation=Smooth 提供投影空间保形 Hermite、端点/原始身份及真曲线命中/裁剪；未声明独立同名系列或完整官方 API 兼容，性能待 C4 | C3 |
| CT07 | Spline area / `SplineAreaSeries` | 部分 | 本库 AreaSeries.Interpolation=Smooth 与 RangeAreaSeries 复用曲线填充；顶点共同切线限幅保持 C1 和同符号厚度，兼容堆叠共边；异构拓扑与完整官方属性待补 | C3 |
| CT08 | Step line / `StepLineSeries` | 部分 | 本库 LineSeries.Interpolation 三种阶梯已实现源起点/末点/屏幕X中点垂直变化、缺失段及同路径读数；以自有 API 提供，不声明独立同名系列兼容 | C3 |
| CT09 | Stacked bar / `StackedBarSeries` | 部分 | C3-02 以BarSeries.StackMode=Stacked提供横竖正负独立累计、组/轴/方向/基线隔离、显隐重算与原贡献；不声明官方独立同名系列或完整API兼容，性能/设备资格仍开放 | C3 |
| CT10 | Stacked 100% bar / `Stacked100PercentBarSeries` | 部分 | C3-02 以BarSeries.StackMode=Percent按正负分别归一±100，零量为0、零基线约束、组隔离及显示/原始份额分离；正式双平台软件证据已记录，完整官方API/性能资格仍开放 | C3 |
| CT11 | Stacked area / `StackedAreaSeries` | 部分 | C3-01 线/面积支持正负独立累计，非零正/负总量各归一到±100、零量为0，组/轴/模式隔离及显隐重算；兼容X/插值/有效拓扑共边与厚度已验，异构连续共边仍开放 | C3 |
| CT12 | Area range / `AreaRangeSeries` | 部分 | 本库 RangeAreaSeries 使用 HighPath/LowPath、规范化显示上下界而保留源字段、逐界空点策略与五种路径；样本以上边界命中/浏览并带 BaseValue，下界不单独建点，拒绝 CategoryY | C1、C3 |
| CT13 | Range bar / `RangeBarSeries` | 部分 | C3-02 本库RangeBarSeries绑定High/Low无需ValuePath，横竖显示规范化两界并保留原值/身份、区间命中和标签/提示底座；专属双界模板及完整官方属性仍开放 | C1、C3 |
| CT14 | Waterfall / `WaterfallSeries` | 部分 | C3-02 已有横竖源序增减累计、缺失断连、布尔TotalPath总计/重置、正负/总计颜色及可配连接线；不支持显示排序，完整官方API及物理/性能资格仍开放 | C3 |
| CT15 | Histogram / `HistogramSeries` | 基础已实现 | 固定箱数/宽度/显式非等宽边界、末端右闭、Count/Probability/Density及原始bin命中；双平台独立数值/像素通过，规模及官方等价仍开放 | C5 |
| CT16 | Pareto | 部分 | C3-02 本库ParetoSeries按非负有限值稳定降序，源身份与显示rank分开，正总量末项累计100%、全零累计0，使用自动或指定独立线性轴；负/缺失Gap、累计线无第二业务身份，完整官方属性/性能仍开放 | C3 |
| CT17 | Dot plot / `DotPlotSeries` | 部分 | C3-02 本库DotSeries为独立类别点系列，支持横竖、marker大小/形状、显示排序与源身份；以自有API提供，完整官方DotPlotSeries属性/逐点编码未覆盖 | C3 |
| CT18 | Lollipop / `LollipopSeries` | 部分 | C3-02 已有横竖点与基线细杆、宽度/排序、点/杆命中、原身份与标签/导出底座；逐点样式及完整官方API与性能资格仍开放 | C3 |
| CT19 | Variance / `VarianceSeries` | 部分 | C3-02 以High/Low有向差的双端点与细杆表示横竖比较，正负填充可配置、原始上下界/身份保留；官方参考基线/柱体变体及完整逐点样式仍有兼容差距 | C3 |
| CT20 | Combo：混合系列共用主轴 | 部分 | C3-01扩展line/bar/scatter/area/range，C3-02加入比较系列，C3-03加入编码Scatter/ScatterLine/XY Bubble及共用身份、选择/标签/动画/导出；混合renderer保留各自命中距离协议，后续图族与完整组合目录继续验收 | C1、C3 |
| CT21 | Combo：次 Y 轴及系列轴归属 | 部分 | 独立轴范围/Key、Near/Far、读数/命中及视图同步已有；C3-02 Pareto新增独立线性累计百分比轴，比较系列沿用显式轴/断轴；后续图族及完整组合目录仍开放 | C1、C2、C3 |

堆叠模式的直接依据：[Stacked bar](https://docs.avaloniaui.net/controls/data-display/charts/cartesian/stacked-bar-chart)、[Stacked 100% bar](https://docs.avaloniaui.net/controls/data-display/charts/cartesian/stacked-100-percent-bar-chart)、[Stacked area](https://docs.avaloniaui.net/controls/data-display/charts/cartesian/stacked-area-chart)。进阶项依据：[Range bar](https://docs.avaloniaui.net/controls/data-display/charts/cartesian/range-bar-chart)、[Area range](https://docs.avaloniaui.net/controls/data-display/charts/cartesian/range-area-chart)、[Dot plot](https://docs.avaloniaui.net/controls/data-display/charts/cartesian/dot-plot-chart)、[Lollipop](https://docs.avaloniaui.net/controls/data-display/charts/cartesian/lollipop-chart)、[Variance](https://docs.avaloniaui.net/controls/data-display/charts/cartesian/variance-chart)、[Waterfall](https://docs.avaloniaui.net/controls/data-display/charts/cartesian/waterfall-chart)、[Histogram](https://docs.avaloniaui.net/controls/data-display/charts/cartesian/histogram-chart)、[Pareto](https://docs.avaloniaui.net/controls/data-display/charts/cartesian/pareto-chart)。访问：2026-10-15。

### Bubble、Circular 与 Radial

来源：[Bubble 分类侧栏](https://docs.avaloniaui.net/controls/data-display/charts/bubble/bubble-chart)、[Circular 分类侧栏](https://docs.avaloniaui.net/controls/data-display/charts/circular/pie-chart)、[Radial 分类侧栏](https://docs.avaloniaui.net/controls/data-display/charts/radial/nightingale-rose-chart)、[Polar 系列](https://docs.avaloniaui.net/controls/data-display/charts/radial/polar-chart)、[Radar 系列](https://docs.avaloniaui.net/controls/data-display/charts/radial/radar-chart)。访问：2026-10-15。

| ID | 官方图表 / 系列 / 模式 | 状态 | 差距与自主交付项 | 阶段 |
| --- | --- | --- | --- | --- |
| BC01 | Bubble：XY + 大小编码 | 部分 | C3-03 本库BubbleSeries以SizePath优先/WeightPath兼容映射DIP直径，Area/Diameter尺度、零/负/缺失规则、颜色编码和重叠绘制/可见形状命中已实现；大小/颜色图例、官方完整API及性能/物理资格仍开放 | C1、C3 |
| BC02 | Bubble cloud | 基础已实现 | 有界源序气泡/标签/命中/原始面积比，双平台软件验证；完整专业扩展/正式性能/官方等价开放 | C6 |
| BC03 | Packed bubble | 基础已实现 | 稳定降序打包/面积/来源/类别与相对大小图例；vendor尺寸默认开放，双平台软件验证；完整专业扩展/正式性能/官方等价开放 | C6 |
| CC01 | Pie | 部分 | C3-04 本库CircularChart/PieSeries已补份额、零/负值Ignore/Absolute/Throw、间隔、小扇区Keep/Hide、源身份选择与过渡；完整官方API、进阶图例/模板、辅助语义及性能/物理资格仍开放 | C3 |
| CC02 | Donut | 部分 | C3-04 DonutSeries已补比例内外半径、中心文字/内容、环段命中、分离、部分弧和独立多环；完整官方API、图例/模板及设备资格仍开放 | C3 |
| CC03 | Semi-donut | 部分 | C3-04 SemiDonutSeries默认180°上半环，支持起始/总角度、方向、外标签连线及布局；拥挤标签按有限空间省略，完整目录变体、辅助语义/性能及物理资格仍开放 | C3 |
| RD01 | Polar / `PolarLineSeries` | 部分 | 已补数值角度/半径、PolarLine/Scatter/Filled、度/弧度/方向/起点/部分周期、闭合/缺失、圆形/多边形网格和标记；正式双平台软件验证通过。完整轴、官方全部属性/组合、性能与设备资格待补 | C1、C3 |
| RD02 | Radar / `RadarSeries` | 部分 | 已补 RadarChart、多系列首见类别并集、重排同名射线、轮廓/填充、网格、MaxValue 和源身份选择；正式双平台软件验证通过。进阶模板/布局、完整官方契约和性能/设备资格待补 | C3 |
| RD03 | Polar area | 部分 | 已补等角扇区、环形面积正比数值、内半径/角间隙、零负缺失策略、基本标签/选择/导出；完整标签布局/模板、官方全部属性与性能待补 | C3 |
| RD04 | Nightingale rose | 部分 | 已补线性半径编码并与 PolarArea 面积语义分开验收，支持内半径/角间隙与源身份；方向分箱/堆叠和高级布局仍待后续 | C3 |
| RD05 | Radial bar | 部分 | 已补共享类别同心轨道、弧长比例、内孔/间隙、身份选择和导出；完整官方轨道/标签/模板组合、性能与设备资格待补 | C3 |
| RD06 | Radial line | 部分 | 已补共享类别径向折线、源序断点/闭合、圆形网格、周边类别文字、标记/选择/导出；完整文字布局/轴/C2 适配与性能待补 | C3 |

### Comparison

来源：[Comparison 完整分类侧栏](https://docs.avaloniaui.net/controls/data-display/charts/comparison/diverging-bar-chart)。访问：2026-10-15。以下是逐项范围，不从现有正负竖柱推断其横向布局已经完成。

| ID | 官方图表 / 模式 | 状态 | 差距与自主交付项 | 阶段 |
| --- | --- | --- | --- | --- |
| CP01 | Diverging bar | 基础已实现 | 真正中心基线/共享幅值、正负/标签与双方向、原始身份，双平台软件验证；官方等价/专业扩展/规模资格开放 | C5 |
| CP02 | Dumbbell | 基础已实现 | 低高双端点/连接、逆序/同点顶层角色和分别选择，双平台软件验证；官方等价/专业扩展/规模资格开放 | C5 |
| CP03 | Mekko | 基础已实现 | 列宽×段高归一化、gap0面积份额、父子来源/嵌套更新，双平台软件验证；官方等价/专业扩展/规模资格开放 | C5 |
| CP04 | Mirror bar | 基础已实现 | 共享非负双侧尺度、中心空隙、双侧原值刻度，双平台软件验证；官方等价/专业扩展/规模资格开放 | C5 |
| CP05 | Parliament | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7 | C6 |
| CP06 | Population pyramid | 基础已实现 | 输入年龄顺序、非负左右组、共享尺度/独立BarGap，双平台软件验证；官方等价/专业扩展/规模资格开放 | C5 |
| CP07 | Tornado | 基础已实现 | 左右总影响稳定降序、共享尺度/来源选择，双平台软件验证；官方等价/专业扩展/规模资格开放 | C5 |
| CP08 | Venn diagram | 基础已实现 | inclusive mask契约、exclusive汇总依赖、2圆解析/3圆近似及公开误差，双平台软件验证；官方等价/专业扩展/规模资格开放 | C5 |

### Statistical 与 Engineering

来源：[Statistical 完整分类侧栏](https://docs.avaloniaui.net/controls/data-display/charts/statistical/beeswarm-plot-chart)、[Engineering 完整分类侧栏](https://docs.avaloniaui.net/controls/data-display/charts/engineering/carpet-plot-chart)。访问：2026-10-15。算法选型、误差界及确定性是本仓库待制定的验收要求，官方目录名称本身不能提供这些保证。

| ID | 官方图表 / 系列 | 状态 | 差距与自主交付项 | 阶段 |
| --- | --- | --- | --- | --- |
| ST01 | Beeswarm | 基础已实现 | 分类原始点、空间碰撞检查、有界工作量及拥挤回退诊断；双平台软件验证与原始来源通过，专业/官方资格留C7，见[C5补齐报告](charts-c5-completion.md) | C5 |
| ST02 | Box plot | 基础已实现 | 原始/预聚合、Type7、IQR须/原始离群序号、均值及组命中；横竖/缩放/导出双平台验证，其他约定/官方等价开放 | C5 |
| ST03 | Contour plot | 基础已实现 | 完整矩形标量网格、等值线/填色、鞍点拓扑及NaN掩膜；双平台软件验证与原始来源通过，专业/官方资格留C7，见[C5补齐报告](charts-c5-completion.md) | C5 |
| ST04 | Density plot | 基础已实现 | Gaussian KDE显式/自动Silverman带宽、有限域/梯形积分、不强制重归一；二维/加权/边界校正与规模资格开放 | C5 |
| ST05 | Error bar / `ErrorBarSeries` | 基础已实现 | 对称/非对称/单侧误差、横竖DIP端帽、原始源点命中及normal-z辅助；多轴/导出双平台验证，官方等价开放 | C1、C5 |
| ST06 | Mosaic | 基础已实现 | 双分类列联比例、重复单元聚合及全部原始成员；双平台软件验证与原始来源通过，专业/官方资格留C7，见[C5补齐报告](charts-c5-completion.md) | C5 |
| ST07 | Parallel coordinates | 基础已实现 | 逐维尺度/反向/clamp/缺失断线、业务轴绑定及精确点线命中；双平台软件验证与原始来源通过，专业/官方资格留C7，见[C5补齐报告](charts-c5-completion.md) | C5 |
| ST08 | Ridgeline | 基础已实现 | 原始样本KDE与预计算曲线、共同数值/绝对密度尺度及重叠；双平台软件验证与原始来源通过，专业/官方资格留C7，见[C5补齐报告](charts-c5-completion.md) | C5 |
| ST09 | Strip plot | 基础已实现 | 分类原始点、固定seed/index散列jitter及均值；双平台软件验证与原始来源通过，专业/官方资格留C7，见[C5补齐报告](charts-c5-completion.md) | C5 |
| ST10 | Violin plot | 基础已实现 | 对称密度轮廓、Type7分位/箱体、小样本规则及原始样本序号；双平台软件验证与原始来源通过，专业/官方资格留C7，见[C5补齐报告](charts-c5-completion.md) | C5 |
| EN01 | Carpet plot | 基础已实现 | 完整矩形A/B网格、双线性Y、X=A+OffsetB、Jacobian唯一逆解；fold可显示/逆解拒绝，双平台验证；官方运行时等价/扩展资格开放 | C5 |
| EN02 | Hexbin | 基础已实现 | pointy axial/cube rounding、DIP半径、Count/Sum/Mean及全部原始成员；resize重建/viewport保留分箱，双平台验证；官方运行时等价/扩展资格开放 | C4、C5 |
| EN03 | Smith | 基础已实现 | 物理Ω/S、Z₀、被动阻抗/导纳反射、实虚网格与逆解；极点/圆外/不可表示拒绝，双平台验证；官方运行时等价/扩展资格开放 | C5 |
| EN04 | Ternary | 基础已实现 | max缩放三分量归一化、等边三角正逆投影、原始Item与比例区分，双平台验证；官方运行时等价/扩展资格开放 | C5 |
| EN05 | Wind rose | 基础已实现 | north-centered半开方向/速度档、frequency权重/百分比与原始Count、线性频数半径叠加，双平台验证；官方运行时等价/扩展资格开放 | C5 |

### Financial 与派生分析

来源：[Financial 分类侧栏](https://docs.avaloniaui.net/controls/data-display/charts/financial/candlestick-chart)、[Financial 宿主与叠加层](https://docs.avaloniaui.net/controls/data-display/charts/financial/financial-chart)、[Technical indicators](https://docs.avaloniaui.net/controls/data-display/charts/financial/technical-indicators-chart)、[Trendline 与均线系列](https://docs.avaloniaui.net/controls/data-display/charts/shared-elements/trendline-chart)。访问：2026-10-15。

官方正文明确 FinancialChart 可承载金融系列和兼容叠加系列；交易周期横向布局采用等槽距，不等同于普通日期轴的自然时间距离。该差异需要独立的数据和缩放验收。

| ID | 官方图表 / 系列 / 算法 | 状态 | 差距与自主交付项 | 阶段 |
| --- | --- | --- | --- | --- |
| FN01 | Financial chart 宿主 | 基础已实现 | FinancialChart共享显式UTC交易槽、价格/成交量多轴、精确日期overlay与未知日期gap；交易所时段/节假日及官方等价开放 | C1、C5 |
| FN02 | Candlestick / `CandlestickSeries` | 基础已实现 | 本库CandlestickSeries涨跌/平价蜡烛、有限OHLC校验、原始源项/RawPoint和导出；官方API/运行时等价开放 | C5 |
| FN03 | OHLC / `OhlcSeries` | 基础已实现 | 高低线、左开右收tick、UTC交易槽和原始源项提示；双平台数值/像素/包消费通过，官方等价开放 | C5 |
| FN04 | Heikin-Ashi | 基础已实现 | 明确首开/递推OHLC/gap重置与合成/原始值区分，100k全序列SHA独立核对；增量/正式性能开放 | C5 |
| FN05 | Hilo | 基础已实现 | HiloSeries原始High/Low线、完整价格bounds、日期槽/命中/多轴导出；高级布局与官方等价开放 | C5 |
| FN06 | Kagi | 基础已实现 | close-only绝对反转、shoulder/waist粗细/阈值拆段、来源范围和非时间槽；ATR/百分比/逐笔及正式性能开放 | C5 |
| FN07 | Renko | 基础已实现 | close-only固定箱宽、可配反转、同源多砖/原始区间、非时间槽及资源拒绝；ATR/百分比/wick/逐笔开放 | C5 |
| FN08 | Point and figure | 基础已实现 | 固定箱/反转数、X/O列延伸/反转、来源范围及累计字母预算；高级箱规则/逐笔/正式性能开放 | C5 |
| AN01 | 附加 Trendline 与独立 `ChartTrendlineSeries` 两种接入 | 基础已实现 | 附加Trendline与独立ChartTrendlineSeries，源关联/自身映射、轴继承/显式覆盖、预测范围、图例与生命周期均有回归；官方API等价开放 | C1、C5 |
| AN02 | Linear 趋势线 | 基础已实现 | Linear最小二乘、完整历史拟合与显式取样/forecast；Count/Omitted/R²和合成身份明确 | C1、C5 |
| AN03 | Polynomial 趋势线 | 基础已实现 | degree1..10多项式，中心缩放HouseholderQR；大offset和秩亏回归，degree/采样预算有界 | C1、C5 |
| AN04 | Exponential 趋势线 | 基础已实现 | Exponential exp(a+bX)，logY最小二乘，Y≤0省略；R²原Y域，预测溢出拒绝 | C1、C5 |
| AN05 | Logarithmic 趋势线 | 基础已实现 | Logarithmic a+b lnX，X≤0省略且预测/采样域须正；原Y域R² | C1、C5 |
| AN06 | Power 趋势线 | 基础已实现 | Power exp(a)X^b，X/Y须正、双log拟合、原Y域R²和域/不足拒绝 | C1、C5 |
| AN07 | Moving average 趋势线 | 基础已实现 | MovingAverage趋势=源对齐尾随SMA，预热/gap重置，不接受任意X预测/forecast | C1、C5 |
| AN08 | `MovingAverageSeries`：Simple / SMA | 基础已实现 | MovingAverageSeries Simple/SMA，完整历史尾随窗口、源端点身份、通知和viewport外历史 | C1、C5 |
| AN09 | `MovingAverageSeries`：Exponential / EMA | 基础已实现 | MovingAverageSeries EMA，period点SMA种子与alpha2/(p+1)，gap重新预热 | C1、C5 |
| AN10 | `MovingAverageSeries`：Weighted / WMA | 基础已实现 | MovingAverageSeries WMA，最旧至最新权重1..period，源长度/顺序保持 | C1、C5 |
| AN11 | `MovingAverageSeries`：Triangular / TMA | 基础已实现 | MovingAverageSeries TMA，ceil(p/2)和floor(p/2)+1两SMA，奇偶均预热p−1 | C1、C5 |
| AN12 | `SMAIndicator` | 基础已实现 | 独立SMAIndicator的SourceSeries/参数/生命周期及源更新回归，非由均线系列自动通过 | C1、C5 |
| AN13 | `EMAIndicator` | 基础已实现 | 独立EMAIndicator，SMA种子/递推、SourceSeries更新/源身份和图例 | C1、C5 |
| AN14 | `WMAIndicator` | 基础已实现 | 独立WMAIndicator，权重/预热、SourceSeries关联/通知与图例 | C1、C5 |
| AN15 | `BollingerBandsIndicator` | 基础已实现 | BollingerBandsIndicator中轨SMA±总体SD倍数，上/下/中轨和带组件独立样式/图例/可见性、bounds和gap | C1、C5 |
| AN16 | RSI、MACD 等未在当前技术指标正文列出的算法 | 待专项核验 | 本轮检查的公开API目录未匹配RSI/MACD，不能据此证明授权runtime不存在；参考版本运行时/API专项核验或单列自主扩展继续开放，见[C5补齐报告](charts-c5-completion.md) | C0、C5 |

### Gauges

来源：[Gauges 完整分类侧栏](https://docs.avaloniaui.net/controls/data-display/charts/gauges/circular-gauge-chart)。访问：2026-10-15。

| ID | 官方图表 / 模式 | 状态 | 差距与自主交付项 | 阶段 |
| --- | --- | --- | --- | --- |
| GG01 | Circular gauge | 部分 | 已补圆弧轨道、线性业务刻度/针/目标/区间和导出，双平台软件验证通过；复杂刻度布局、完整官方 API/性能与物理资格待补 | C3 |
| GG02 | Gauge chart | 部分 | 已补独立半圆布局、指针/弧/目标/区间和实时更新；高级布局、全 C2/官方 API/性能资格待补 | C3 |
| GG03 | Gradient ring | 部分 | 已补共享域同心独立进度环、业务标签/梯度画刷、环间距与更新；复杂标签、完整官方组合/性能待补 | C3 |
| GG04 | Linear gauge | 部分 | 已补横竖轨道、反向、填充/指针/目标/阈值和刻度；复杂文字/区间布局、完整官方 API/性能待补 | C3 |
| GG05 | Liquid fill gauge | 部分 | 已补圆/矩形裁剪、液位、静态波形/显式动画/暂停/减少动态效果与当前帧导出；数值过渡/完整模板/性能待补 | C3 |
| GG06 | Progress donut | 部分 | 已补进度弧、上下限、目标、百分比/自定义文本和导出；复杂布局、完整官方 API/性能待补 | C3 |

### Analytics 与 KPI

来源：[Analytics 完整分类侧栏](https://docs.avaloniaui.net/controls/data-display/charts/analytics/bullet-chart)、[Theme river 组合](https://docs.avaloniaui.net/controls/data-display/charts/analytics/theme-river-chart)、[Pictorial bar](https://docs.avaloniaui.net/controls/data-display/charts/analytics/pictorial-bar-chart)、[官网 Stream graph 描述](https://avaloniaui.net/charts)。访问：2026-10-15。

| ID | 官方图表 / 系列 / 模式 | 状态 | 差距与自主交付项 | 阶段 |
| --- | --- | --- | --- | --- |
| AK01 | Bullet | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7 | C6 |
| AK02 | Bump | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7 | C6 |
| AK03 | Calendar heatmap | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7 | C1、C6 |
| AK04 | Funnel | 部分 | 已补源序正值阶段、颈宽/分段间隔、业务身份命中/选择/键盘/提示与导出，双平台软件验证通过；复杂标签/模板、完整 C2/官方 API/性能资格待补 | C3 |
| AK05 | Heatmap | 部分 | 已补类型化离散行列、颜色域/实际色标、缺失与重复末项规则、源项交互和导出；高级颜色轴/大矩阵虚拟化、完整 C2/性能资格待补 | C3、C4 |
| AK06 | KPI cards | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7 | C6 |
| AK07 | Matrix | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7 | C6 |
| AK08 | Pictorial bar / `PictorialBarSeries` | 部分 | 已补 Geometry 符号 Stretch、Clip、Repeat 三模式、方向/间距、真实形状与孔洞命中，接入 Cartesian 数据/轴/选择/导出；完整组合/官方 API/性能资格待补 | C3 |
| AK09 | Pyramid | 部分 | 已补有限正值按面积比例分层、源序/间隔、身份交互/标签和导出；复杂标签/模板、完整 C2/官方 API/性能资格待补 | C3 |
| AK10 | Slope | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7 | C6 |
| AK11 | Table chart | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7 | C6 |
| AK12 | Theme river（独立控件及堆叠组合） | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7；Centered/Zero，无wiggle | C3、C6 |
| AK13 | Waffle | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7 | C6 |
| AK14 | Word cloud | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7 | C6、C7 |
| AK15 | 官网 Stream graph 与 Theme river 的精确对应 | 待专项核验 | 已交付ThemeRiver控件和Centered/Zero；精确官方独立系列/API与额外算法对应仍在C7核验，不由AK12自动声明等价 | C0、C6 |

### Hierarchy、Flow 与 Network

来源：[完整分类侧栏](https://docs.avaloniaui.net/controls/data-display/charts/hierarchy/alluvial-chart)、[Flow chart](https://docs.avaloniaui.net/controls/data-display/charts/hierarchy/flow-chart)、[Mindmap 组合](https://docs.avaloniaui.net/controls/data-display/charts/hierarchy/mindmap-chart)、[Process flow 组合](https://docs.avaloniaui.net/controls/data-display/charts/hierarchy/process-flow-chart)。访问：2026-10-15。Mindmap 和 Process flow 的官方正文使用 FlowChart 组合，不能再计成两个独立基础引擎。

| ID | 官方图表 / 组合模式 | 状态 | 差距与自主交付项 | 阶段 |
| --- | --- | --- | --- | --- |
| HF01 | Alluvial | 基础已实现 | 显式Stage列、共享流量带/原始端点，双平台软件验证；完整专业扩展/正式性能/官方等价开放 | C6 |
| HF02 | Arc diagram | 基础已实现 | 原始线序/弧线/平行边自环/命中，双平台软件验证；完整专业扩展/正式性能/官方等价开放 | C6 |
| HF03 | Chord diagram | 基础已实现 | 有向端口/incident质量扇区/原始边，双平台软件验证；完整专业扩展/正式性能/官方等价开放 | C6 |
| HF04 | Circle packing | 基础已实现 | 子圆嵌套/原始叶面积/包络空隙/钻取，双平台软件验证；完整专业扩展/正式性能/官方等价开放 | C6 |
| HF05 | Dendrogram | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7 | C6 |
| HF06 | Flame graph | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7 | C6 |
| HF07 | Flow chart | 基础已实现 | 显式位置尺寸/五种形状/箭头/命名分组；任意组样式与节点模板开放，双平台软件验证；完整专业扩展/正式性能/官方等价开放 | C6 |
| HF08 | Force-directed graph | 基础已实现 | 确定性种子/有界迭代/固定节点/碰撞回退/取消，双平台软件验证；完整专业扩展/正式性能/官方等价开放 | C4、C6 |
| HF09 | Icicle | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7 | C6 |
| HF10 | Indented tree | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7 | C6 |
| HF11 | Mindmap / brainstorming：FlowChart 组合 | 基础已实现 | 双侧生成森林/中心主题/原始交叉边，双平台软件验证；完整专业扩展/正式性能/官方等价开放 | C6 |
| HF12 | Network | 基础已实现 | 圆周/固定位置/节点边身份与命中，双平台软件验证；完整专业扩展/正式性能/官方等价开放 | C6 |
| HF13 | Organization | 基础已实现 | 组织树/连接/双方向/折叠；任意节点模板开放，双平台软件验证；完整专业扩展/正式性能/官方等价开放 | C6 |
| HF14 | Process flow：FlowChart 组合 | 基础已实现 | 自动有向层级/步骤判断/返回边/箭头标签，双平台软件验证；完整专业扩展/正式性能/官方等价开放 | C6 |
| HF15 | Radial tree | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7 | C6 |
| HF16 | Sankey | 基础已实现 | 共享流量宽度/内部守恒/循环返回/重心排序，双平台软件验证；完整专业扩展/正式性能/官方等价开放 | C6 |
| HF17 | Sunburst | 基础已实现 | 叶合计扇环/内孔扫角/钻取祖先路径，双平台软件验证；完整专业扩展/正式性能/官方等价开放 | C6 |
| HF18 | Treemap | 基础已实现 | Squarify/Slice面积/极端比例拒绝/标签/钻取，双平台软件验证；完整专业扩展/正式性能/官方等价开放 | C6 |

### Maps

来源：[Maps 完整分类侧栏与 Bubble map](https://docs.avaloniaui.net/controls/data-display/charts/maps/bubble-map-chart)。访问：2026-10-15。空间数据加载、坐标转换和图层生命周期须作为独立契约；已有 XY 散点不表示地图已覆盖。

| ID | 官方图表 / 图层场景 | 状态 | 差距与自主交付项 | 阶段 |
| --- | --- | --- | --- | --- |
| MP01 | Bubble map | 基础已实现 | 原地图基础与Marker/Line/Vector/Pie、分类色、多座位选择通过双平台；fit/旧订阅问题关闭；瓦片/标签对比/模板/正式性能及官方等价开放，见[C6报告](charts-c6-main-completion.md) | C6 |
| MP02 | Choropleth map | 基础已实现 | 原地图基础与Marker/Line/Vector/Pie、分类色、多座位选择通过双平台；fit/旧订阅问题关闭；瓦片/标签对比/模板/正式性能及官方等价开放，见[C6报告](charts-c6-main-completion.md) | C6 |
| MP03 | Heatmap map | 基础已实现 | 原地图基础与Marker/Line/Vector/Pie、分类色、多座位选择通过双平台；fit/旧订阅问题关闭；瓦片/标签对比/模板/正式性能及官方等价开放，见[C6报告](charts-c6-main-completion.md) | C4、C6 |
| MP04 | Non-geographic / Seat map | 基础已实现 | 原地图基础与Marker/Line/Vector/Pie、分类色、多座位选择通过双平台；fit/旧订阅问题关闭；瓦片/标签对比/模板/正式性能及官方等价开放，见[C6报告](charts-c6-main-completion.md) | C6 |
| MP05 | Shape map | 基础已实现 | 原地图基础与Marker/Line/Vector/Pie、分类色、多座位选择通过双平台；fit/旧订阅问题关闭；瓦片/标签对比/模板/正式性能及官方等价开放，见[C6报告](charts-c6-main-completion.md) | C6 |

### Scheduling 与小图

来源：[Scheduling 完整分类侧栏](https://docs.avaloniaui.net/controls/data-display/charts/scheduling/timeline-chart)、[Sparkline 四种模式](https://docs.avaloniaui.net/controls/data-display/charts/scheduling/sparkline-chart)。访问：2026-10-15。

| ID | 官方图表 / 模式 | 状态 | 差距与自主交付项 | 阶段 |
| --- | --- | --- | --- | --- |
| SC01 | Gantt | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7 | C1、C6 |
| SC02 | Sparkline：Line | 部分 | 已补无轴源序微图、缺口、极值/首尾/标记、命中/业务身份、实时更新和尺寸约束；完整 C2/辅助语义/性能待补 | C3 |
| SC03 | Sparkline：Area | 部分 | 已补基线填充、正负值与缺口，不跨缺口连面；完整模板/官方 API/性能待补 | C3 |
| SC04 | Sparkline：Bar | 部分 | 已补源槽柱、正负基线/间距、缺失槽与极值强调；完整样式/官方 API/性能待补 | C3 |
| SC05 | Sparkline：WinLoss | 部分 | 已补相对基线的正/负/零固定符号格、原始幅值/业务身份与颜色；完整模板/辅助语义/性能待补 | C3 |
| SC06 | Spiral timeline | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7 | C6 |
| SC07 | Swimlane | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7 | C6 |
| SC08 | Event timeline | 基础已实现 | C6主项数值/原始身份/交互及Demo通过双平台软件验证；具体契约与限制见[C6报告](charts-c6-main-completion.md)，高级样式/完整官方等价留C7 | C1、C6 |

## 共用功能矩阵

### 契约、数据与扩展

来源：[官方总览：数据、公共属性、动画及扩展](https://docs.avaloniaui.net/controls/data-display/charts)、[Financial 扩展上下文](https://docs.avaloniaui.net/controls/data-display/charts/financial/financial-chart)。访问：2026-10-15。

| ID | 功能范围 | 状态 | 当前边界与自主交付项 | 阶段 |
| --- | --- | --- | --- | --- |
| DS01 | `ItemsSource` 绑定和集合更新 | 部分 | 已有非泛型源、INCC/嵌套INPC、批量通知和卸载释放；真正增量计算/实时预算待 C4 | C1、C4 |
| DS02 | 对象数据的 CategoryPath / ValuePath | 已实现 | 类型缓存访问器、嵌套路径、DataError/Issues，真实 compiled AXAML / MVVM 与业务记录示例通过 | C1 |
| DS03 | 直接数值列表和 Point 列表 | 已实现 | 数值列表按源索引给 X；ChartPoint/Avalonia Point 保留 XY，非有限/缺失值由统一空点策略处理 | C1 |
| DS04 | 范围、OHLC、角度半径、层级、图和空间数据 | 部分 | 范围/OHLC/角度及C6层级/图/空间/排程/运营typed契约与原始来源已接入；完整统一专用视图和官方适配继续开放，见[C6报告](charts-c6-main-completion.md) | C1、C5、C6 |
| DS05 | 非有限值过滤和折线断点 | 已实现 | 当前固定 Gap；已有空数据、常量和极端有限值回归 | C0 |
| DS06 | EmptyPointMode：Zero | 已实现 | 现有三系列统一策略；本库默认 Gap 与官方默认不同，迁移说明保留 | C1 |
| DS07 | EmptyPointMode：Gap | 已实现 | 统一模式属性与 WasEmpty/IsEmpty，断线/忽略无效点通过回归 | C1 |
| DS08 | EmptyPointMode：Average / Interpolate | 部分 | 三系列已实现邻有效值平均/按X插值（重复逆序X按源序比例），边界留Gap；区间专属规则待对应系列 | C1 |
| DS09 | 逐数据项画刷和调色板 | 部分 | 有系列画刷及按实例稳定的公共palette；逐项颜色和路径映射待补 | C1、C2 |
| DS10 | 完整 MVVM 状态、命令与通知 | 部分 | 已有逻辑继承、编译绑定、资源/源替换、共享悬停、点/系列选择TwoWay与取消回写、原子Range及主轴范围联动；C2-04当前三系列功能验证通过，完整前置事件与未来图族仍待补 | C1、C2 |
| DS11 | 自定义 Cartesian 系列绘制协议 | 已实现 | IChartSeriesRenderer + IChartProjection，三系列统一使用；外部派生系列 bounds/render/hit/legend 回归通过 | C1、C4 |
| DS12 | 自定义图例、边界及交互数据提供者 | 部分 | renderer可控制bounds、命中、投影与图例符号；图例内容控件模板与有界滚动已验；复杂叠加仍待C2 | C1、C2 |
| DS13 | 金融叠加层与 bounds provider | 部分 | FinancialChart日期索引/MapTime/ProjectOverlay、价格/量独立轴和金融完整High/Low bounds已交付；可见区专属优化/复杂外部bounds provider仍开放 | C1、C5 |
| DS14 | PlotAreaContent / 覆盖内容 | 未实现 | 布局对齐、裁剪、输入路由及尺寸变更 | C2 |
| DS15 | 官方公开消费 API 与迁移映射 | 部分 | 已固定12.3.0实际API与本地C0/C1快照，提供Usage/两类隔离包消费者和迁移说明；无二进制替换承诺 | C0、C1、C7 |

### 坐标轴

来源：[Axis customization](https://docs.avaloniaui.net/controls/data-display/charts/shared-elements/axis-customization-chart)、[Combo 次轴](https://docs.avaloniaui.net/controls/data-display/charts/cartesian/combo-chart)。访问：2026-10-15。

| ID | 功能范围 | 状态 | 当前边界与自主交付项 | 阶段 |
| --- | --- | --- | --- | --- |
| AX01 | 数值轴自动范围、漂亮刻度、显式上下限 | 已实现 | 当前线性数值轴；极端值保护不代表所有轴模式完成 | C0 |
| AX02 | CategoryAxis | 已实现 | X/Y业务类别共享映射、显式标签与数字槽位；OnTicks/BetweenTicks、Gap、单类别范围和默认逐类别刻度已回归 | C1 |
| AX03 | DateTimeAxis | 已实现 | Unixms UTC转换、毫秒至年/日历刻度、格式与逆投影；任意时区/交易日历不在本行已实现范围 | C1 |
| AX04 | LogarithmicAxis | 已实现 | 有效正数、可配底/指数间隔、反向、投影/逆投影及锚点缩放回归通过 | C1 |
| AX05 | 主/次 Y 轴、系列轴归属 | 已实现 | Key映射、同侧/异侧多轴布局、独立视图或主轴归一化联动、逐轴缩放限制；共享叠加层属于C2 | C1 |
| AX06 | 坐标轴标题、基本数值格式 | 部分 | 基础标题/标签样式与原始域格式已有；C2-06 Demo暴露顶部X终点100与右Y标题的角落重叠，标题/端点占位避让仍需补；UTC契约见C1文档 | C0、C1、C2 |
| AX07 | 固定主步长、次步长、次网格 | 已实现 | MinorInterval优先，或每主间隔插MinorTickCount；numeric/date原始单位、log指数单位，主次网格独立 | C1 |
| AX08 | 标签适配：隐藏、换行、多行、45/90度、自定义旋转及 Auto | 已实现 | 测量文本后选错行/换行/旋转并抽稀；缩放重算密度、稳定留白、Near/Far边缘回归；复杂模板标签仍属C2 | C1、C2 |
| AX09 | 轴/标题/标签/刻度/主次网格独立样式 | 已实现 | 各级开关/画刷/线宽、字体/字号、刻度长度；基线及主次网格dash/cap/join；隐藏轴保留数据投影 | C1、C2 |
| AX10 | Scale breaks | 已实现 | inline/source绑定、相邻交叠合并、压缩映射/逆映射、端点有效、内部空点/不可命中、穿越线裁断、标记样式；压缩规则属本库契约 | C1、C2 |
| AX11 | Plot bands：文字、重复、系列前后层级 | 已实现 | inline/source、NaN视图端点、分类槽/原始域、透明度/文字对齐/显隐、repeat count/until及图层，至多10000次 | C1、C2 |
| AX12 | 连续水平轴与系列兼容/退回分类布局规则 | 部分 | 数值/日期/对数连续X及混合三系列可用；本库显式选择轴，无效项留Gap，不提供官方“任一系列不兼容时整图自动退回类别”的策略；属于已记录的兼容差异 | C1 |

### 共用视觉与交互

来源：[Legend](https://docs.avaloniaui.net/controls/data-display/charts/shared-elements/legend-chart)、[Markers](https://docs.avaloniaui.net/controls/data-display/charts/shared-elements/markers-chart)、[Data labels](https://docs.avaloniaui.net/controls/data-display/charts/shared-elements/data-labels-chart)、[Tooltip](https://docs.avaloniaui.net/controls/data-display/charts/shared-elements/tooltip-chart)、[Interactions](https://docs.avaloniaui.net/controls/data-display/charts/shared-elements/interactions-chart)、[Crosshairs](https://docs.avaloniaui.net/controls/data-display/charts/shared-elements/crosshairs-chart)。访问：2026-10-15。

| ID | 功能范围 | 状态 | 当前边界与自主交付项 | 阶段 |
| --- | --- | --- | --- | --- |
| VI01 | 标题、网格和基础静态图例 | 已实现 | 已有当前控件范围内的基本展示 | C0 |
| VI02 | 图例位置、对齐、浮动、可复用图例模型 | 部分 | 四边/横竖、边缘对齐、绘图区内浮动、稳定颜色和模型/绘制回调已有；被动控件模板、有界纵向滚动、浮动输入阻挡与混合标签层级已验；类别图例及虚拟化待补 | C2 |
| VI03 | 图例控制系列/分类显隐 | 部分 | 系列显隐同步范围/柱分组/命中及选择，恢复保持配色/身份；C3-01线/面积与C3-02柱的堆叠/百分比显隐重算已接入，类别图例及其余图族继续待补 | C2、C3 |
| VI04 | 基础点标记 | 已实现 | 当前三系列提供十种形状、大小和独立marker填充/描边；柱marker默认关闭，其余形状使用保守外接半径命中 | C2 |
| VI05 | 数据标签 | 部分 | 格式/原始项formatter、偏移/字体/画刷/背景、位置/裁剪/有限候选及贪心避让已有；原业务项UI模板、预算及混合层级已验；连接线和高级排版待补 | C2 |
| IN01 | 点/柱 Tooltip 基础命中与主题 | 已实现 | 原三系列名称、X/Y、分类标签和轴格式可用；C3-01线/面积/范围与C3-02比较系列提供显示值及RawPoint/BaseValue/StackValue/CumulativeValue，示例明确原值/源索引；样本身份仍为原始项 | C0、C3 |
| IN02 | Tooltip 模板与数据项 TooltipText、系列级开关 | 部分 | TooltipFormatter及真实IDataTemplate内容、typed上下文/原始Item、复用/资源继承/重入清理和PNG已有；TooltipText与系列级独立开关待补 | C1、C2 |
| IN03 | 按轴鼠标滚轮缩放、拖动平移、复位 | 已实现 | 独立 Viewport、边界、最小跨度与捕获释放已有；C2-05 增加 Zoom/Pan/Auto、连续双轴 delta、修饰键及实际范围变化后的父滚动交接，当前功能验证通过；未宣称完整官方交互契约 | C0、C2 |
| IN04 | 捏合等触控手势 | 部分 | C2-05 已实现当前三系列触点阈值/延迟tap、累计双指捏合/移动质心、单指平移、原生Pinch入口去重、滚动容器竞争、捕获/失焦/Escape及重入取消；当前功能验证通过，物理触屏/触控板、惯性/手掌拒绝及DPI设备矩阵仍开放 | C2、C7 |
| IN05 | 缩放灵敏度、历史及返回 | 部分 | WheelZoomSensitivity/WheelPanSensitivity及zoom修饰键、重置和程序范围状态已有，C2-05当前功能验证通过；缩放历史栈、容量和返回/前进仍未实现 | C2 |
| IN06 | 内嵌/独立 Range selector | 部分 | 横竖独立控件、双拖柄/窗口、原子TwoWay、键盘步长、主轴Chart连接及log/date/inverse/断轴比例转换已实现；C2-04当前三系列功能验证通过，内嵌数据概览与未来图族待补 | C2 |
| IN07 | Crosshair：横/竖/双向与轴坐标标签 | 已实现 | 当前三系列提供Horizontal/Vertical/Both、各参与轴逆投影读数/标签与可观察AxisReadings；线/标签显隐独立，默认关闭标签，更多图族随其阶段验证 | C2 |
| IN08 | Trackball：横/竖与逐点/聚合浮层 | 部分 | C2-03 横竖/逐点/聚合、模板与原始身份保留；C3-01 新增平滑/阶梯/面积/范围/堆叠的同路径插值和 BaseValue，范围读数给上下界；LOD、自定义几何扩展及查询性能留 C4 | C2、C3、C4 |
| IN09 | Hover highlighting | 部分 | 当前提示环；补系列/单元/扇区高亮及其余数据淡化 | C2 |
| IN10 | Selection：Single、SingleDeselect、Multiple | 部分 | 当前三系列点选择/矩形与独立系列选择、Ctrl切换、TwoWay不可变列表、系列淡化及前置取消已实现；源/配置清理不可取消，C2-04当前三系列功能验证通过，未来图族选择语义仍待补 | C1、C2 |
| IN11 | 点击、悬停、选择、缩放和系列变化事件 | 部分 | HoveredPoints、点/系列SelectionChanging/Changed、RangeChanged与可取消LegendItemInvoked已有；前置选择/范围反馈处理重入及TwoWay回写，C2-04当前三系列功能验证通过，完整前置事件体系仍待补 | C1、C2 |
| IN12 | 稳定布局下的交互锚点和视觉命中 | 部分 | 当前已有大标记/裁剪边缘命中和标签留白稳定的回归；仍需覆盖多轴、断轴和全部新增系列的一致几何 | C2、C4 |

### 注释、动画、导出、主题与可访问性

来源：[Annotations](https://docs.avaloniaui.net/controls/data-display/charts/shared-elements/annotations-chart)、[Export](https://docs.avaloniaui.net/controls/data-display/charts/shared-elements/export-chart)、[总览动画契约](https://docs.avaloniaui.net/controls/data-display/charts)、[产品页主题及可访问性描述](https://avaloniaui.net/charts)。访问：2026-10-15。

| ID | 功能范围 | 状态 | 当前边界与自主交付项 | 阶段 |
| --- | --- | --- | --- | --- |
| OV01 | Line annotation | 已实现 | 横竖线、标签、数据/Pixel、前后层/裁剪；当前 C2-06 支持标注专用轴，错误/重复/冲突维度跳过；本批当前功能验证通过 | C2 |
| OV02 | Band annotation | 已实现 | 区间带/标签/方向、层/断轴裁剪；当前 C2-06 专用轴/独立视图与 PNG 一致性本批当前功能验证通过 | C2 |
| OV03 | Text annotation | 已实现 | XY/样式/Pixel、层/裁剪；当前 C2-06 标注专用轴无需占位系列，本批当前功能验证通过 | C2 |
| OV04 | Rectangle annotation | 已实现 | 两角点、DIP 圆角短边收敛、标签/位置/偏移、多轴/缩放/导出；C2-06本批当前功能验证通过，不宣称官方 API 同名同参 | C2 |
| OV05 | Ellipse annotation | 已实现 | 默认两角点，新增中心±数据半径投影包围框、标签/位置/偏移；非线性下仍为屏幕椭圆；C2-06本批当前功能验证通过 | C2 |
| OV06 | Arrow line annotation | 已实现 | 端点/四种头部/固定DIP/短线收敛、虚实线/标签/偏移与裁剪；C2-06本批当前功能验证通过 | C2 |
| OV07 | 自定义叠加内容 | 已实现 | CustomAnnotation回调与被动 PlotAreaContent/IDataTemplate、plot-local多轴投影/上下文、继承/重入清理/PNG；C2-06本批当前功能验证通过，交互编辑不在当前范围 | C1、C2 |
| EX01 | PNG / JPEG 文件导出 | 已实现 | C2-08当前三图支持同步/异步、固定/推导像素、背景/透明度、DPI、JPEG质量、预算、同目录临时替换、取消与UI失败事件，图例/标签/标注/模板/缩放及暂停帧共用捕获；Windows/WSLg v2当前功能验证通过；压力/跨图全局预算留C4，系统保存选择器实操待验 | C2、C7 |
| EX02 | PNG stream 导出与保存对话框 | 部分 | C2-08已实现PNG/JPEG流与文件同步/异步、流不关闭及部分写出契约、队列/阶段边界取消、单图在途忙拒绝；Demo/Usage接StorageProvider并清理选择器对象及关闭回调，双平台picker/portal实际交互仍待验 | C2、C7 |
| EX03 | SVG / PDF 等额外格式 | 需核验 | 当前官方导出正文仅列 PNG/JPEG；如自行扩展，独立列范围 | C0 |
| AM01 | 入场动画开关、时长和 easing | 已实现 | C2-07 入场/数据/视图、duration/easing、暂停/取消、显式减少动态与点数预算保留；C3-01 新形状及上下界使用显示帧、命中和导出，系统偏好/其余图族与正式性能开放 | C2、C3 |
| AM02 | 系列 delay / progress | 部分 | C2-07提供图表统一delay/eased progress、同版本数据发布和latest-wins；逐系列独立delay/progress、离场/错峰及性能资格仍待补 | C2、C4 |
| TH01 | 亮暗主题与自定义基础样式 | 部分 | Themes 与 Tooltip 亮暗配色已有；C2-09 新增显式 Light/Dark 高对比画布、文字/系列/选择/读数预设和至少 7:1 的默认系列色，保留本地自定义样式优先；OS 偏好自动接入、完整目录和模板仍待补 | C2、C3、C7 |
| AC01 | 图案等非颜色编码 | 未实现 | C2-09 的高对比配色、已有标记形状与浏览焦点轮廓不代表纹理/填充图案已实现；按原范围补图案和识别测试 | C2、C7 |
| AC02 | 图表键盘、读屏、数据摘要与可访问性树的官方精确覆盖 | 部分 | C2-09 当前三图已有原始点角色/名称/值/选择/命令、有界页与缓存、独立键盘及数据/轴摘要；Windows UIA 的树/点操作、Linux 独立 AT-SPI 总线的 9 点/边界/click/grab_focus/分页已有实查。完整桥接门槛未过：Windows 原生 GetSelection 列表为空而托管为 1，自定义 selector 的附着 ControlPeer 对照也复现；Linux 已选子节点枚举未过、嵌套点选择写入未验。原生点焦点、真实读屏、未来图族及官方精确覆盖继续开放 | C0、C2、C7 |

## 性能及 Windows / Linux 对照边界

来源：[官方产品页性能、实时数据和平台描述](https://avaloniaui.net/charts)、[官方数据源与更新契约](https://docs.avaloniaui.net/controls/data-display/charts)。访问：2026-10-15。公开材料提及渲染优化、实时数据和跨平台，但本轮没有获得可用于同机复测的官方点数/FPS/延迟/内存基准。下面的工作项均为本仓库计划，不能写成官方已经达到的数值。

| ID | 对照项 | 状态 | 必须补齐的证据 | 阶段 |
| --- | --- | --- | --- | --- |
| PF01 | 完整数据、可见数据和绘制数据分层 | 部分 | 当前有场景缓存，但失效后仍全量快照/遍历；补版本化数据管线和分层失效 | C4 |
| PF02 | 大数据裁剪、抽样与 LOD | 未实现 | 线峰值/缺口保持、分布不失真、原始点可追溯 | C4 |
| PF03 | 实时追加/替换/删除与窗口滚动 | 部分 | 集合变更/DeferUpdates通知合并已有；增量范围、分段数据、环形缓冲与持续刷新预算待补 | C4 |
| PF04 | 命中索引和交互缓存 | 部分 | 三系列已有精确X索引/候选裁剪、非单调稳定radix及原始项身份；二维/同X/SharedX退化、更多图族和持续交互预算待补 | C4 |
| PF05 | 与官方同等性能 | 需核验 | 固定两库版本、硬件、后端、数据集、视觉设置，比较首帧、P95/P99、内存和持续更新 | C0、C4、C7 |
| PL01 | Windows 实机功能回归 | 部分 | C3-04 c3-circular-source-v3通过1214测试/890Demo/250Usage/两类隔离包各354，Release零警告/错误，三阶段退出码0；物理资格仍开放；C3-03 c3-scatter-bubble-source-v2通过1145测试/804Demo/229Usage/两类隔离包各321，Release零警告/错误，三阶段退出码0；C3-02历史c3-bar-comparison-source-v1通过1083测试/744Demo/206Usage/两类隔离包各284，Release零警告/错误，三阶段退出码0；C3-01历史c3-line-area-source-v1通过1013测试/652Demo/181Usage/两包各245及零警告/错误、三阶段退出码0。C2-09 UIA实查保留历史身份，选中枚举留A11Y-NATIVE-01；物理设备/DPI/present/读屏/选择器开放 | C0、C7 |
| PL02 | Linux 功能兼容 | 部分 | C3-04 Ubuntu26.04/WSLg c3-circular-source-v3通过1214测试/890Demo/250Usage/两类隔离包各354，Release零警告/错误，三阶段退出码0；物理资格仍开放；C3-03 Ubuntu26.04/WSLg c3-scatter-bubble-source-v2通过1145测试/804Demo/229Usage/两类隔离包各321，Release零警告/错误，三阶段退出码0；C3-02历史Ubuntu26.04/WSLg c3-bar-comparison-source-v1通过1083测试/744Demo/206Usage/两类隔离包各284，Release零警告/错误，三阶段退出码0；C3-01历史Ubuntu26.04/WSLg c3-line-area-source-v1通过1013测试/652Demo/181Usage/两包各245及零警告/错误、三阶段退出码0。C2-09 AT-SPI实查保留历史身份，已选节点枚举未过；22.04/24.04物理桌面/读屏/portal等开放 | C0、C7 |
| PL03 | Linux 性能与官方对照 | 需核验 | 须有 Linux 独立原始测量；WSL、CI headless 或能编译均不代表桌面性能完成 | C4、C7 |
| PL04 | Windows/Linux 可访问性与发布 | 部分 | C2-09 点级软件语义、键盘、摘要和显式高对比已有；Windows 外部 UIA 树/点操作与 Linux 独立 AT-SPI 树/边界/click/grab_focus/分页已有实查。Windows 原生选中列表和 Linux 已选子节点枚举未过、嵌套点选择写入未验，完整原生门槛未过；真实读屏器、原生点焦点、OS 偏好/图案、完整发布及设备矩阵继续开放 | C2、C7 |

## 完整性与更新规则

1. C0 保存目标版本和本清单日期；逐项核对各分类侧栏、正文中的系列变体及示例组合。对官方新增、重命名和下架保留差异记录。
2. 每项“未实现/部分”须绑定开发计划中的阶段、场景数据、自动化测试、Demo 入口和 Windows/Linux 验收记录；复杂算法另存数值参考与容差。
3. 公共基础设施完成不自动给新图表打勾；新图表完成也不自动表示模板、导出、选择、读屏或高密度性能完成。
4. “需核验”转为其他状态必须附公开文档或目标版本的可复现证据。未公开的官方行为可以先自主设计，但要保留“自主扩展”标识。
5. API 名称、默认值和渲染语义分别核验。当前已有 ChartSeries、CartesianChart 等名称，不等于兼容官方命名空间、XAML、数据契约或二进制。
6. 性能结论必须链接原始测量、环境和对照设置。Linux 按发行版/会话/设备逐项回填；WSLg 合成通过仅覆盖已执行范围，其余目标保持待核验。
