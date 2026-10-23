# Charts 剩余开发计划

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


更新日期：2026-10-18。适用起点：仓库 `0.2.0-preview.2`、.NET SDK `10.0.401`、`net10.0`、Avalonia `12.0.3`。

排期决定（2026-10-17）：按用户要求，Windows UIA / Linux AT-SPI 原生选中项枚举桥接问题移至[后续无障碍专项 A11Y-NATIVE-01](#a11y-native-01)，不再作为当前功能推进或进入 C3 的前置阻塞；现有失败结果与完整原生桥接门槛仍保留未通过状态。

执行进展：2026-10-18。

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

C2-09 历史实现（2026-10-17）：已实现当前三系列的点级角色/名称/只读值/选择与命令、按需分页节点、独立于选择的键盘浏览、可绑定数据摘要及显式亮暗高对比配色，并接入 Demo/Usage。默认每页 64 个源序号，图表自身 LRU 最多强引用 256 个点节点（客户端仍可持有已逐出节点）；保留原始 Item/StableId/SourceIndex，选择继续遵守取消与重入契约。

外部 Windows UIA 已实查 3 个系列、9 个点、Value/SelectionItem/Invoke 的点操作子集及分页；Ubuntu 26.04 WSLg 的独立 AT-SPI 总线已实查 9 个点、边界、click/grab_focus 和分页。完整原生桥接门槛仍未通过：Windows 根 SelectionPattern.GetSelection 返回空集合（托管列表为 1，自定义 selector 的真正附着 ControlPeer 对照也复现）；Linux 预设 canonical 点选择后点 Selected 状态为 true、get_n_selected_children 为 1，但 get_selected_child(0) 返回 null，完整已选子节点枚举未过，嵌套点选择写入仍未验。根 select_child(0) 指向直属系列，不能当作点选择写入验证。Windows 原生焦点仍是图表，当前点经图表 Value 变化报告；Linux grab_focus 返回 true 及 CurrentPoint 可读不等于原生点焦点合格，点级原生焦点与实际读屏播报继续待验。

C2-09 历史冻结源码 v3 曾在 Windows 与 Ubuntu 26.04 WSLg 分别通过 946/946 项测试、578/578 项 Demo 检查、163/163 项 Usage 检查，以及功能包/主题包两类全新缓存 NuGet 消费者各 213/213 项；Release 构建零警告/错误，基础、包消费与 API 三阶段退出码均为 0。两平台 589 个声明输入逐项一致。已验证软件子集通过，完整原生桥接门槛仍为 false；正式身份与原始限制见[C2-09 历史报告](charts-c2-accessibility.md)、[验证清单](charts-c2-accessibility-validation.json)与[API](charts-c2-accessibility-api.txt)。

C2-08 的双平台 892/504/147/183、C2-07 的 860/420/131/163、C2-06 的 821/366/119/147 与更早结果保留原批历史身份。本批未测正式性能/内存，百万点命中尾延迟继续留 C4；填充图案、系统高对比偏好接入、Windows 原生选中集合、Linux 已选子节点枚举与嵌套点选择写入、原生点焦点与实际读屏器、系统保存选择器真实交互、物理设备、present 与官方许可运行参考仍开放。 下文历史起点保留。

上一轮 v15 的固定 controlled 目标门禁 100/100、1896 对 Direct/Recorded 像素及全部 297 项增加值/75 个描述性标记继续保留[历史身份](charts-tail-latency.md)。百万点 hit p95 `0.7649→1.1235 ms` 未被修复或重新测量。当前功能版本的性能资格留到 C4；AX12、未来图族 Trackball/选择/范围适配、跨图游标、物理触摸/触控板、未来图族动画、系统保存选择器、物理平台和官方许可参考仍开放。

目标是把目前的三种基础图表扩展为可维护的图表库，覆盖官方 Charts 的公开图表目录、共用功能和交互，并在 Windows 与 Linux 桌面完成独立验收。功能以逐项矩阵验收，性能同时检查自身工程门槛和指定官方版本的同条件实测；构建成功、图表类型数量或单次截图均不能代替这些验收。

完整交付范围包括下列三个文件，它们使用同一组 C0 至 C7 阶段编号：

- 本计划：架构、任务顺序、阶段交付、依赖、工作量与发布条件。
- [官方功能对照](charts-official-alignment.md)：官方图表、系列、组合模式与共用能力的逐项差距，以及对应实施阶段。
- [性能与平台验收](charts-performance-platform-plan.md)：PERF 和 OS 工作项、场景、指标、官方对照协议、Windows/Linux 环境矩阵和结果格式。

## 对标范围与完成含义

官方产品目前列出 70 多种图表，并提供坐标轴、图例、数据标签、Tooltip、缩放平移、十字线、Trackball、选择、动画、标注、趋势线、实时数据和图片导出。文档还包含多种组合方式和系列变体，不能用营销类型数代替完整目录。[官方产品](https://avaloniaui.net/charts)、[控件目录与公共契约](https://docs.avaloniaui.net/controls/data-display/charts)

对标采用 2026-10-15 核验的公开目录；C0 固定官方包版本、文档快照与 API 清单。后续官方变化进入差距清单，经过范围更新后安排，避免验收目标随网页变化漂移。某个文档条目是独立控件、系列还是组合模式，在矩阵中分别登记。

| 维度 | 完成条件 |
| --- | --- |
| 功能覆盖 | 矩阵中的运行时控件及共用能力逐项有实现、样例、必要测试和 Windows/Linux 证据；组合模式需要真实组合验收。 |
| 行为与数值 | 数据含义、轴范围、缺失点、堆叠、分箱、统计、选择与交互符合记录的契约，不能只有近似外观。 |
| API 与接入 | 公开 API 对照和差异明确，AXAML、MVVM、可观察集合、独立包消费者可用；提供迁移示例。当前不宣称二进制替换官方程序集。 |
| 性能 | 自身绝对门槛通过，具有合法可运行参考条件的场景完成同机同质量对照；缺少参考结果的条目标为待对照。 |
| 平台 | Windows 与 Linux 各自有构建、包消费、原生窗口、真实输入、字体/DPI、导出、辅助功能与性能报告。 |
| 发布 | API、默认值、主题资源、资源所有权、兼容行为和已支持环境有明确版本记录，发布阻断项清零。 |

官方 Charts MCP 等开发工具属于生态能力，不纳入本次桌面运行时控件的发布门槛。移动端、浏览器端和桌面以外的部署不在本轮平台范围内。高级图族属于完整目标，C0 至 C4 的中间交付不能称为完整官方对标。

## 当前起点

| 已有部分 | 当前状态 | 剩余工作 |
| --- | --- | --- |
| 工程结构 | `NT.AvaloniaControl.Charts` 与 `.Charts.Themes` 独立，接入统一 Demo、解决方案和 Verify 测试列表。 | 独立 Usage、真实 NuGet 包消费者、API 快照、专用 benchmark、平台验证和发行材料。 |
| 图表 | 折线、垂直分组柱状、散点；支持负值柱和基础混合绘制。 | 完整图族、系列专属数据模型、组合规则与各自的标签/交互/分析契约。 |
| 坐标轴 | 线性数值范围、整数索引分类标签、刻度、标题、格式、网格线。 | 独立轴类型、日期时间、对数、多轴、反向与布局、标签避让、区域/带状配置。 |
| 数据入口 | `IEnumerable<ChartPoint>`、可观察集合增删替换、按生命周期释放订阅。 | 对象数据映射、属性变化、批量增量、异步生产者、完整 XAML/MVVM 继承和源身份规则。 |
| 交互 | 数据点/柱体提示、鼠标锚点缩放、左键平移、按轴控制、双击/按钮重置。 | 自定义提示、共享提示、Trackball、十字线、选择、范围选择器、触摸、键盘和辅助功能。 |
| 视觉 | 独立亮暗配色、基础图例、标记、空数据提示。 | 可交互图例、调色板规则、数据标签、模板/扩展点、动画、标注、图案及导出。 |
| 测试证据 | 最近已有 53 项图表测试与 31 项 Windows 程序化原生窗口检查通过的报告。 | 报告关联源码和程序集哈希，补齐完整功能、真实性能和 Linux 验收。 |

已有结果位于 `artifacts/platform/charts-interaction-tests.xml` 与 `artifacts/platform/charts-interaction.json`，图像包括亮暗主题、局部视图和合成路由 Tooltip。报告中的 Windows 内核版本为 `10.0.26200`，不能据此标注为 Windows 10 发行版。原生检查使用程序视图和合成输入，窗口图由 `RenderTargetBitmap` 生成；它们不证明物理鼠标、触屏、桌面实际呈现耗时或 Linux 已通过。本次制定计划不产生新的运行结果。

静态代码审计已经发现影响扩展顺序的约束：

1. `ChartAxis` 和系列缺少宿主 `DataContext` 的继承通路，当前 XAML 测试只有常量；不能把存在 StyledProperty 等同为完整 MVVM 接入。
2. 渲染器按三种具体系列分支，第三方派生 `ChartSeries` 缺少绘制、范围和命中扩展契约。
3. 数据快照、极值和柱间距计算随视图失效重做；命中扫描全部数据，柱图命中还计算间距。尚无可见段索引、抽样和批量更新管线。
4. 图形和文字的缓存粒度、动态主题和交互层失效仍需拆分；目前的 CLR 只读诊断状态也不能直接作为 MVVM 可观察状态使用。

这些是设计风险，具体耗时排序由 C0 的测量确定。

## 平台基线

本计划先按 Windows x64 与 Ubuntu x64 规划。Linux 发行版和 CPU 架构补充后，更新 OS 矩阵及相关工作量；麒麟/UOS 的版本、桌面环境、CPU 和图形驱动必须作为明确目标登记。

| 环境 | 计划地位 | 必须处理的问题 |
| --- | --- | --- |
| Windows 11 24H2 及后续指定测试版本 x64 | 首要桌面基线 | GPU/软件渲染、100%/150%/200% DPI、跨屏、鼠标/精密触控板、Narrator/UIA、打包消费。 |
| Windows 10 指定版本/版本类别 x64 | 兼容分支 | C0 确认实际 edition、补丁与 .NET 10 支持交集，单独留证；不能只凭 Avalonia 平台表宣称完整厂商支持。 |
| Ubuntu 22.04 与 24.04 x64 | 拟承担的 Linux 验收基线 | GNOME/X11，以及 Wayland 会话下的 XWayland；CJK 字体、区域文化、捕获、导出路径、GPU/软件后端。 |
| 指定麒麟/UOS 或 ARM64 | 目标环境分支 | 明确版本、glibc、.NET、原生图形依赖、字体、窗口系统及实际设备，完成相同质量门禁后列入支持表。 |
| 原生 Wayland | 需要框架升级评估的支线 | 当前仓库是 12.0.3；官方文档将原生后端放在 12.1.0 起的显式选择路径，且仍有局限。先评估升级对仓库其他控件的影响，再建立专门验收。 |

官方文档说明 Linux 默认走 X11，Wayland 桌面默认通过 XWayland；原生 Wayland 需要单独启用。Ubuntu 22.04/24.04 是本项目拟验证的环境，不能称为当前官方 Tier 1。[Linux 后端说明](https://docs.avaloniaui.net/docs/platform-specific-guides/linux)、[官方支持矩阵](https://docs.avaloniaui.net/docs/supported-platforms)

Linux 工作从 C0 的运行器和字体基线开始，每阶段都运行共享测试及对应原生场景。C7 承担最终平台复核与发布验收，不能把全部 Linux 工作留到最后。

## 阶段与工作量

以下为一名熟悉 Avalonia 图形和数据可视化的工程师的粗略工程工作量，包含设计、实现、审查、必要测试、Demo 和文档。它用于安排工作包，不是 Codex 持续运行时间或承诺日期。每阶段结束后按完成证据重新估算。

| 阶段 | 工作量 | 交付重点 | 主要前置条件 |
| --- | --- | --- | --- |
| C0 基线与关键原型 | 8～12 人日 | 固定官方/本地基线、支持环境、数值数据集、性能仪表和双平台冒烟原型。 | 当前实现与可获取的环境。 |
| C1 公共契约与底座 | 12～18 人日 | 数据/系列/轴/视图/生命周期契约、MVVM、扩展管线、Usage 和包消费。 | C0 的范围与原型结论。 |
| C2 共用视觉与交互 | 15～22 人日 | 图例/标签、完整提示与选择、十字线/Trackball、触摸/键盘、标注、导出、动画基础。 | C1；可与 C3 独立图族并行。 |
| C3 常用图族 | 18～28 人日 | 完整笛卡尔变体、圆形、极坐标、雷达、气泡、基础仪表与 Sparkline。 | C1 的投影/轴/系列契约，C2 的共用元素。 |
| C4 性能引擎 | 18～30 人日 | 增量数据、分层缓存、裁剪/抽样、索引命中、实时调度、多图预算和测量门禁。 | C0 仪表、C1 数据管线；原型从 C1 提前开展。 |
| C5 统计金融与工程 | 24～38 人日 | 统计/分布、金融图与指标、趋势分析及工程图族的数值和交互。 | C1/C2；性能验收依赖 C4。 |
| C6 层级关系地图与运营分析 | 35～55 人日 | 层级/流向/网络/地图/排程/运营分析的全部矩阵条目及复杂布局。 | C1/C2，C4 的布局调度与资源预算；部分统计依赖 C5。 |
| C7 双平台完整验收与发布 | 15～24 人日 | 全矩阵回归、官方实测对照、实机/长稳、包发布验证、迁移和维护资料。 | 前述功能与性能门禁，目标设备和参考运行条件齐备。 |
| 基础合计 | **145～227 人日** | 完整公开功能目录及上述 Windows/Linux 范围。 | 性能/平台附件的专项工作已包含在各阶段中。 |

建议保留约 20%～25% 的复杂图布局、参考差异和平台问题余量；单人全职的规模约为 8～13 个月，设备等待另记。并行工作可缩短日历时间，但 C1 公共契约、C4 性能验证和 C7 发布条件仍是共同依赖。目标发行版或官方对照范围改变后重新估算。

关键路径为 C0 → C1 → C2/C3 → C4 集成门禁 → C5/C6 完整集成 → C7。Windows/Linux 自动化、性能测量和平台缺陷修复贯穿所有阶段。

## C0 基线与关键原型

| 编号 | 工作 | 完成条件 |
| --- | --- | --- |
| C0-01 | 固定参考范围 | 保存官方目录、文档访问日期、参考包版本、API 与默认值清单；每条矩阵有阶段、差距及验证场景。 |
| C0-02 | 建立可追溯现状 | 生成本地 API 快照、源码 manifest、程序集 SHA-256、依赖锁文件标识；重新取得所需基线并标明未具备的平台。 |
| C0-03 | 冻结性能测量口径 | 区分数据摄取、预处理、布局、UI 提交、渲染完成和实际呈现；固定场景、数据种子、画质、硬件及 raw JSON schema。 |
| C0-04 | 验证关键架构 | 对百万点折线、非单调散点、增量追加、真实 AXAML/MVVM 继承、可扩展系列和 Linux 字体/输入做小规模原型，确定 C1 接口。 |
| C0-05 | 建立双平台验证入口 | Windows 与 Linux 构建/测试/包消费入口，Xvfb 集成场景与真实桌面任务分开；确定 Ubuntu、Windows 和目标国产 Linux 的具体环境。 |
| C0-06 | 建立官方参考宿主 | 相同数据/窗口/质量的官方与自研独立宿主。参考不可运行的项标记 `blocked-reference`，自身功能与优化继续推进。 |

出口：范围和数据集可追溯、关键原型有结果、每个平台有可执行验证入口。性能数值在此阶段评估并版本化冻结，不能在后续失败时仅通过降低门槛宣告完成。

## C1 公共契约与底座

| 编号 | 工作 | 完成条件 |
| --- | --- | --- |
| C1-01 | 图表与系列扩展契约 | 拆出 ChartBase、投影/布局、范围、绘图、命中和图例接口；新增系列无需修改中心类型判断。保留当前公开入口或提供明确迁移。 |
| C1-02 | 数据映射 | 支持数值/点列表与对象集合，分类/值/范围/权重等字段映射、类型转换及错误事件；访问器按模型类型缓存，反射不进入逐点热路径。 |
| C1-03 | 数据身份与增量 | 定义原始索引、稳定 ID、可见索引与抽样代表点映射；覆盖 Add/Remove/Replace/Move/Reset、属性通知及批量更新。 |
| C1-04 | XAML 与 MVVM | 系列和轴具有可验证的 DataContext/资源继承方案；支持编译绑定、集合替换、视图状态/选择双向绑定和命令。用真实 ViewModel 消费者验收。 |
| C1-05 | 完整坐标轴 | 按矩阵实现数值、分类、日期时间、对数、多轴；明确范围/间隔、反向、对齐、缩放限制、类别槽位及格式；标签避让/旋转/换行与稳定留白统一处理。 |
| C1-06 | 数值语义 | null/NaN/Infinity、缺失点 Zero/Gap/Average/Interpolate、空源、常量、极端量级、不排序输入、重复 X、正负堆叠和零总量均有明确结果。 |
| C1-07 | 所有权与异步生产 | 数据源归应用所有；定义 UI 发布、后台只读快照、版本/取消/重入规则。轴标题或颜色修改不应无条件丢失用户视图。 |
| C1-08 | 消费者与兼容 | 新建 `samples/Charts.Usage`，扩展 NuGet 消费验证覆盖仅功能包与功能+主题包，验证打包资源、真实 AXAML/MVVM、卸载订阅和公开 API 差异。 |

出口：接入、数据身份和扩展接口先稳定；后续图族不得自行建立不兼容的数据与命中体系。

## C2 共用视觉与交互

执行状态：现有三系列的图例/标签模板、Trackball、系列选择/范围/多图、触摸/嵌套输入、标注、动画及 PNG/JPEG 导出保留原批功能验证身份。C2-09 已补点级辅助语义、有界分页、独立键盘浏览、数据摘要和显式高对比预设，其原批正式验证见历史报告。C2-01～C2-09 的本轮主要软件实现已有落地；类别图例/标签高级布局、范围概览、跨图游标、逐系列动画、填充图案、系统偏好与读屏/物理平台验收、未来图族和 C2 整阶段出口仍开放。下一主阶段可推进 C3，未完成的收尾继续保留编号追踪。

| 编号 | 工作 | 完成条件 |
| --- | --- | --- |
| C2-01 | 调色板与图例 | 稳定系列配色、可见性切换、位置/排列、图例标记与模板；隐藏/恢复保持颜色、身份和选择规则。 |
| C2-02 | 数据标签和标记 | 标签定位/格式/模板、重叠处理、正负值和边缘裁剪；不同图族共用配置并保留专属布局。 |
| C2-03 | 提示与读数 | 自定义/共享提示、最近点/线段规则、十字线和 Trackball；密集、重叠、抽样、堆叠和多轴数据都映射回真实原始值。 |
| C2-04 | 选择与范围 | 点/系列/多选和矩形选择、状态绑定/事件、范围选择器、按轴控制及多图同步；修改源或隐藏系列后的状态迁移一致。 |
| C2-05 | 输入完整性 | 鼠标、精密触控板、触摸捏合/平移、键盘导航和重置；处理滚动容器嵌套、捕获丢失、窗口失焦、DPI、手势冲突和操作取消。 |
| C2-06 | 标注和参考元素 | 线/带/文本/自定义形状、轴坐标与像素坐标、图层顺序和裁剪；多轴/缩放/导出一致。 |
| C2-07 | 动画 | 入场、数据和视图变化的 duration/easing/delay、暂停/取消及减少动态效果；大数据默认策略显式配置并纳入性能场景。 |
| C2-08 | 导出 | 当前三图已实现 PNG/JPEG 同步与异步、固定/推导像素尺寸、背景/透明度、DPI 与 JPEG 质量；调用方流保留所有权，文件临时替换，取消不发失败事件，普通失败返回 false 并在 UI 线程报告，单图在途任务拒绝重入。图例/标签/标注/模板与当前视图和暂停帧一并捕获；物理选择器、压力与 C4 资源资格仍待验。 |
| C2-09 | 辅助功能 | 当前三图已提供原始点角色、格式化名称/只读值、选择/命令及生命周期，64 行默认页/256 点 LRU、独立键盘 CurrentPoint、源计数/轴范围摘要和显式亮暗高对比。Windows UIA 点操作与 Linux 独立 AT-SPI 总线的树/边界/click/grab_focus/分页已有实查；完整桥接门槛未过，Windows 原生根选中列表、Linux 已选子节点枚举与嵌套点选择写入继续追踪。独立系列选择辅助模式、原生点焦点/真实读屏器、OS 偏好/填充图案、大源无索引远迁移及设备矩阵仍开放。 |

出口：共用能力可在现有三种图表与后续图族复用；每项交互包含开关、事件、状态绑定和生命周期测试。

<a id="a11y-native-01"></a>
### 后续无障碍专项：A11Y-NATIVE-01

状态：用户明确后置，尚未解决。决策日期：2026-10-17；用户指示：“留到后续无障碍专项处理”。关联功能矩阵 AC02、PL04。

范围为 Windows UIA `SelectionPattern.GetSelection` 的原生选中集合返回，以及 Linux AT-SPI `get_selected_child` 的节点映射与原生选中项枚举。Linux 嵌套点选择写入仍保持未验证，可在专项中关联复验；本次延期不自动扩大至全部无障碍工作。

专项验收时使用外部原生客户端核对托管选择与原生返回项的数量、身份及状态，覆盖单选、多选、取消选择、分页和数据变化，并复验 Linux 嵌套点选择写入。原始失败记录和 C2-09 冻结证据继续保留，修复后另建验证批次；延期不等于通过完整原生桥接或 C2 全阶段验收。下一主阶段仍按计划推进 C3。

## C3 常用图族

逐项范围见[功能矩阵](charts-official-alignment.md)。C3-01 的线与面积、C3-02 柱条与比较保留原批双平台软件证据；C3-03 散点和气泡软件范围已实现，正式双平台冻结软件验证已通过。C3-04 圆形图软件范围已实现，正式双平台冻结软件验证已通过；C3-05 极坐标与雷达软件范围已实现，正式双平台冻结软件验证已通过；C3-06 仪表和微图软件范围已实现，正式双平台冻结软件验证已通过；C3-07 基础分析图软件范围已实现，正式双平台冻结软件验证已通过；下一工作项为 C4-01 数据与视图分层失效，C3 各族完整适配、完整官方 API、性能和设备门槛仍开放。此阶段按共同几何基础实现，不能把变体只做成静态 Demo 绘图。

| 编号 | 图族 | 实施与验收重点 |
| --- | --- | --- |
| C3-01 | 笛卡尔线与面积 | 历史批普通/平滑/三种阶梯、面积/范围、堆叠与百分比及 Demo/Usage/包消费已实现；兼容拓扑共边、同符号厚度、原始身份/下界、轴/动画/导出保留原批验收。异构连续共边、新形状正式性能与完整设备/官方 API 对照继续开放。 |
| C3-02 | 柱条与比较 | 本轮已实现横竖分组、正负堆叠/百分比、范围柱、瀑布、Pareto、Dot/Lollipop/Variance、基线、相对/固定 DIP 宽度、显示排序及原始值标签，接入 Demo/Usage/隔离包消费；正式双平台冻结软件验证已通过。值排序要求自动类别轴，Pareto 独立累计百分比轴；圆角/独立描边、逐点样式、性能/物理及完整官方 API 资格继续开放，直方分箱与进阶比较留 C5。 |
| C3-03 | 散点和气泡 | 已补 ScatterSeries 逐点大小/颜色编码、ScatterLineSeries 和 XY BubbleSeries、面积/直径 DIP 尺度、固定/自动域、数值渐变/直接颜色、源序/大小绘制顺序及可见形状命中；Demo 六图、编译绑定 Usage 和两类隔离包消费已接入，正式双平台冻结软件验证已通过。专属大小/颜色图例、完整官方 API/性能和物理平台资格仍开放；无轴云布局与紧凑打包在 C6 验收。 |
| C3-04 | 圆形 | 已补 CircularChart 与 Pie/Donut/SemiDonut 系列、部分弧/方向/多环/分离、零负值策略与小扇区保留或隐藏、内外标签连线及中心内容、身份选择和过渡控制；Demo 六图、编译绑定 Usage 及两类包消费已接入，正式双平台冻结软件验证已通过。完整官方 API、进阶标签/图例/模板、完整圆形辅助语义、性能与物理平台资格仍开放。 |
| C3-05 | 极坐标与雷达 | 已补 PolarChart/RadarChart、八类公开系列、角度/半径轴、度/弧度/方向/部分周期、断点/闭合、圆形/多边形网格、面积/半径/弧长数值编码、源身份选择与导出；Demo 八图、编译绑定 Usage 和两类包消费已接入，正式双平台冻结软件验证已通过。非线性/完整轴、进阶布局/图例/模板、全 C2 适配、官方完整 API、C4 性能与物理资格仍开放。 |
| C3-06 | 仪表和微图 | 已补六类独立仪表及 Sparkline 四模式、数值域/目标/区间、多环/梯度、横竖/方向、液体静态和显式动画/暂停/减少动态效果、缺口/极值/首尾、实时更新/卸载释放及导出；Demo 十图、编译绑定 Usage 和两类包消费者已接入并通过正式双平台冻结软件验证。仪表数值过渡、全 C2 适配、复杂标签/官方完整 API、原生语义、C4 性能与物理资格仍开放。 |
| C3-07 | 基础分析图 | 已补漏斗、面积比例金字塔、离散 Heatmap/颜色域/实际色标/缺失与重复单元、Pictorial Stretch/Clip/Repeat 的真实符号/孔洞命中、Theme river 居中透明堆叠底座；Demo/编译绑定 Usage/两类隔离包消费者通过正式双平台冻结软件验证。全 C2/复杂布局、wiggle/优化流顺序/更多基线算法、官方 API/性能与物理资格仍开放；下一主项 C4-01。 |

出口：常用业务场景可以真实绑定与组合，C2 共用功能逐图型适配；此时仍是阶段性交付。

## C4 性能引擎

PERF 工作项和量化条件由[性能与平台附件](charts-performance-platform-plan.md)统一定义，避免不同阶段使用不一致的点数/FPS口径。

| 编号 | 工作 | 完成条件 |
| --- | --- | --- |
| C4-01 | 数据与视图分层失效 | 已完成 Cartesian 数据/类别/堆叠/范围准备、极值/基础尺度、视图、轴文字、系列保留绘图和叠加层失效；排序/命中索引与线段投影按数据/视图复用。双平台9场景预热 Tooltip 无全源访问或各层重建，源/视图变更门禁通过；计时/分配仅诊断，完整 C4/内存预算与独立图族资格仍开放，下一项 C4-02。 |
| C4-02 | 可见数据裁剪 | 已完成稳定 Cartesian 投影内置系列的有序 X 范围与非有序/二维空间查询，保留穿越边界端点、曲线邻点和形状覆盖边缘；原始 Tooltip/选择身份不变。双平台9场景窄视口的源访问/索引重建为0，稀疏夹具投影与遍历均低于源规模1/4；密集真实候选与自定义投影保守路径、正式 C4 SLA 仍开放，下一项 C4-03。 |
| C4-03 | 保真抽样和层级细节 | 已完成显式单调线性折线 MinMax 和可复用极值层级，按 TopLevel/导出比例的像素桶保留首末峰谷、边界邻点及映射/补值后仍非有限的断点；原始 Tooltip/选择身份不变，Demo 和可选导出已验证。双平台10场景覆盖7个稠密抽样与3个明确回退，源/层级复用通过；不适用图形保守绘制，柱状/散点密度聚合尚未定义，正式 C4 SLA、任意祖先变换密度和可比较官方画质仍开放，下一项 C4-04。 |
| C4-04 | 高效命中 | 已完成稳定内置 Cartesian 原始点二维索引、线性原始相邻段索引、密集共享 X/Y 读数、标记足迹及共享柱布局；重叠/相同 X、多轴、MinMax省略点和换源均保留原始身份。双平台12个固定场景预热查询与原始数据 oracle 通过，既有并列/裁剪规则不变；极端退化、复杂派生段和正式尾延迟/内存/物理平台资格继续开放，下一项 C4-05。 |
| C4-05 | 增量与实时调度 | 已完成分页有限窗口增量数据源、追加/插入/滑动/删除/替换、合并提交、两批有限队列、显式背压、暂停恢复/取消/统计和跟随最新数据；Demo/编译绑定/隔离消费者与双平台百万行源及Numeric/SamplingNone窄窗附图诊断通过。目录O(P)、其他图族与派生索引N路径、字节预算、持续吞吐及物理/长期SLA仍开放，下一项C4-06。 |
| C4-06 | 绘图与资源预算 | 已完成五层本地共享Model1预算、绘图/文字/画笔/线段复用与4096端点memo，公开预算/统计/清理及显式Compact；两主题Demo、编译绑定、隔离包消费者、双平台20k点分配/五层/线段/分页身份与Native像素检查通过。真实进程/原生/GPU、共享缓存、自动压实、长期内存与后端/present资格仍开放，下一项C4-07。 |
| C4-07 | 多图与后台布局 | 已完成多图FIFO失效请求合并/派发、可见性/卸载生命周期、内置形状路径数字后台准备与取消/版本隔离，以及进程五层缓存/在途任务估算预算；六图Demo、编译绑定、隔离包和双平台固定机制/像素验证通过。数据映射/投影/原生绘制仍在UI线程，完整帧公平/共享CWT/实际内存/长期平台资格开放，下一项C4-08。 |
| C4-08 | 自动化性能门禁 | 已接入九阶段benchmark/真实native窗口、原始数据/trace、独立数值/像素/身份差异守卫与失败留档；本地Windows与WSLg及同候选独立paired control通过，远端CI尚未执行。native测量显式关闭tiered compilation，原数值门槛保留；完整固定硬件SLA、实际内存/present/长期平台资格仍开放，下一主项C5-01。 |

出口：规定规模的自身性能门槛通过，官方可运行场景有对照结果，长稳和资源释放通过。尚缺参考条件的对照项继续保留，不能用自身速度推断官方性能。

## C5 统计金融与工程

| 编号 | 图族 | 实施与验收重点 |
| --- | --- | --- |
| C5-01 | 统计与分布 | 箱线/误差/直方图/密度基础及本轮七类统计目录已补齐；原始身份、数值/命中/生命周期及Demo/Usage/包消费双平台软件验证通过。专业交叉集成、规模/长稳和官方资格继续由C7验收。 |
| C5-02 | 金融价格图 | 金融七图、日期/价格/成交量与overlay宿主基础已交付，旧验证保留；交易所会话、ATR/百分比/逐笔变体及完整官方/性能资格留C7专业专项。 |
| C5-03 | 技术指标与趋势 | 六趋势、四均线及SMA/EMA/WMA/Bollinger基础已交付；增量/后台算法和正式资格留C7。AN16公开目录未匹配不代表授权runtime不存在，专项核验继续开放。 |
| C5-04 | 工程图与专业坐标 | EN01–05工程图/单位/正逆投影/网格/分箱基础已交付；原始来源及软件验证保留，专业扩展、物理平台、正式性能与官方资格继续开放。 |
| C5-05 | 进阶比较 | CP01/02/03/04/06/07/08基础及共享尺度/比例/集合近似已交付，CP05在C6交付；C5基础图族主项完成，下一C7-01专业共享集成及平台/官方资格。 |

出口：数值算法先有独立参照和误差容限，再检查视觉。金融与统计场景同时进入 PERF 数据集；不能只复用普通折线性能结论。

## C6 层级关系地图与运营分析

| 编号 | 图族 | 实施与验收重点 |
| --- | --- | --- |
| C6-01 | 层级和打包 | 已交付六种层级/打包基础图族，原始身份、叶合计、展开钻取/动画通过双平台；额外五层级已于C6-06补充，专业集成与正式资格继续开放。 |
| C6-02 | 流向和关系 | 已交付九种流向/关系目录模式、原始节点边、循环平行边/守恒诊断/取消；双平台软件验证通过，专业样式/编辑和完整共享集成继续开放。 |
| C6-03 | 地理图 | 五模式原有基础加叠加层/分类色/多选已通过双平台；极小坐标fit和换源订阅问题已关闭。瓦片/高级样式/物理平台/官方等价留专项。 |
| C6-04 | 排程和运营 | 已完成四图、依赖/进度、时区/DST、泳道重叠、可见区间和原始选择；双平台软件验证通过，自动排程及专业模板留后续。 |
| C6-05 | 分析和排名 | 已完成十一种运营图、标量/集合、Table.Columns、排名标签和数值规则；双平台软件验证通过。完整官方适配、编辑/虚拟化、wiggle等专业扩展仍开放。 |
| C6-06 | 全目录补齐 | 已补齐五种层级、四叠加层/分类色/多选，审计42条C6目录映射；主项完成。AK15精确官方映射及跨图族专业集成/C7资格保留，下一主线C5补缺。 |

出口：完整运行时目录没有未分配条目；不适用项需要具体的产品范围理由，不能因为实现困难改为可选。数据规模、复杂布局及资源上限纳入性能附件。

## C7 双平台验收与发布

| 编号 | 工作 | 完成条件 |
| --- | --- | --- |
| C7-01 | 全功能集成 | 功能矩阵关联源码、测试、Demo、Usage 和真实包消费者；检查不同图族共享主题、交互和生命周期的组合。 |
| C7-02 | 双平台实机 | 完成指定 Windows/Linux 发行版、桌面/backend、GPU/软件、DPI/跨屏、字体/文化、物理输入及辅助工具矩阵。 |
| C7-03 | 性能与官方对照 | 输出指定参考版本的同条件报告、绝对门槛、失败/超时/降级记录；在仍有待对照项时只声明已取得的结论。 |
| C7-04 | 长稳和异常恢复 | 8 小时持续更新、多图负载、至少 100 轮创建/导航/切换源/主题/关闭；取消、异常数据和内存/句柄趋势符合门槛。 |
| C7-05 | 发布消费者 | 从打包产物、新隔离缓存安装，两平台运行；验证资源、路径大小写、支持的发布方式/裁剪/AOT和升级消费者。 |
| C7-06 | API 与文档冻结 | API/主题快照、迁移映射、默认值/线程/所有权、性能条件和真实支持平台齐备；写明已知限制和维护策略。 |

## 统一测试和证据要求

每个阶段交付都包含功能实现、必要测试、统一 Demo 场景、接入文档和 Windows/Linux 状态。测试按风险组织，不以数量达标：

- 纯数学：轴/投影、插值/堆叠、分箱/统计、金融指标、几何布局和极端输入，使用独立预期值和误差范围。
- Headless：绑定、输入路由、选择、视图、捕获、取消、可见性、源替换、重入和卸载/释放。
- 主题/视觉：每平台分别维护字体和后端基线，包含 CJK、暗色、高对比、RTL和缩放；跨系统比较共享几何及数据语义，不要求不同字体栅格逐像素相等。
- 包消费者：真实 `.nupkg`、新隔离 NuGet 缓存、无源码项目引用，包含编译绑定、独立功能包/主题包和发布产物。
- 原生窗口：平台事件链、输入和导出。合成路由、Xvfb、桌面截图、真实辅助工具和实际呈现测量分别记录，不能相互替代。
- 性能和耐久：固定场景、原始样本、正确性/画质门禁、尾延迟、资源和失败率；按附件协议比较。

每份结果至少关联源码 manifest、程序集/数据集哈希、依赖版本、OS/发行版、CPU/GPU/驱动、backend、字体、窗口/DPI、渲染配置、执行命令、时间、样本数、退出码和失败原因。结果状态统一区分通过、失败、待运行、被环境阻塞和不适用。历史报告只作为历史基线，源代码变化后不得沿用为当前验收。

`scripts/Verify.ps1` 继续承担锁定还原、构建、测试与包消费；Charts 专项性能/原生运行器及 Linux 真实桌面任务通过独立入口接入。附件中列出的新 benchmark、脚本和输出路径是待实现交付，不代表这些工具已经存在。

## 性能目标的使用

详细场景与门槛见[性能与平台附件](charts-performance-platform-plan.md)。拟定关注点包括万点交互、十万点可视化、百万点首次可用、命中尾延迟、四系列持续追加、缓存预算和 8 小时稳定性。GPU与软件后端分别建立预算。

这些数字属于本项目工程目标，C0 固定测量条件后冻结。官方产品说明中的高效绘制与实时增量能力不能转换为其未公开的点数、FPS 或时延承诺；在具备可运行参考宿主时，逐场景同时检查自身绝对门槛与拟定的官方 p95/资源对照倍率。[官方产品说明](https://avaloniaui.net/charts)

## 风险处理与执行顺序

| 风险或依赖 | 处理方式 |
| --- | --- |
| 官方目录/API 在开发中变化 | C0 固定版本和快照，新增差异单独登记，防止无声扩大范围。 |
| 功能扩展先于公共契约 | 先完成 C1 的数据/轴/扩展/绑定；每个新图族共用同一数据身份、命中与选择规则。 |
| 以降画质获得表面性能收益 | 抽样、动画、标记、输出尺寸和质量变化写入结果；对照模式一致，正确性先通过。 |
| 官方参考或 Linux 设备尚不可用 | 单独记录具体缺失条件；继续自身实现与可运行检查，相应对照/平台发布条目保留待验。 |
| 国产 Linux、ARM64 或原生 Wayland 扩大平台范围 | C0 登记版本与架构、完成框架/原生依赖原型，更新矩阵、工期及支持声明。 |
| 库重构影响当前消费者 | 使用现有 53 项测试作为历史回归入口，加入 API/真实消费者测试；迁移变化公开记录，不通过放宽测试掩盖差异。 |
| 复杂图布局和实时数据阻塞 UI | 版本化快照、可取消布局、有限队列和资源预算先落实，再增加大规模场景。 |

原定顺序为 C0 基线/原型 → C1 数据与接入/坐标/生命周期/包消费。当前已完成底座、非单调索引及主要首次可用/文字/命中回退修复，并追加 C2 图例布局、选择取消、形状标注、Tooltip 内容模板、SharedY 与方向/多轴读数。v15 的 record/first 目标门禁已 100/100 通过，其证据继续保留历史身份。2026-10-16 按用户决定，将独立百万点 hit p95 的回退移到 C4 后期优化，不再作为 C2 功能推进的前置阻塞；`0.7649→1.1235 ms` 的实测未被关闭或改写。C2-01/C2-02 控件模板、C2-03 Trackball/插值、C2-04 选择/范围/多图同步均保留原批验证身份。C2-05 已实现触摸/触控板连续输入、嵌套滚动和取消仲裁，其 Windows/WSLg 功能验证保留原批身份；物理输入仍需独立验证。C2-06 补齐当前标注范围并保留原批身份。C2-07 当前三系列动画与时间线控制保留原批身份。C2-08 已补齐 PNG/JPEG 与异步导出软件契约并保留原批验证。C2-09 历史批补点级辅助语义/分页、独立键盘、数据摘要与显式高对比；Windows 原生 UIA 已检出系列/点和 Value/SelectionItem/Invoke，Windows 原生选中集合、Linux 已选子节点枚举与嵌套点选择写入、原生点焦点和真实读屏器、系统偏好/填充图案及 C2 收尾仍开放。C3-01 历史批已补普通/平滑/三种阶梯折线、面积/范围面积及正负堆叠/百分比；兼容组共边、同符号厚度、插值下界、动画/导出和消费者已验证，异构连续共边及新形状性能继续保留。C3-02 本轮已补横竖分组/正负堆叠/百分比柱、范围柱/瀑布/Pareto/Dot/Lollipop/Variance、宽度/显示排序/原始值标签及 Demo/Usage/隔离包接入，正式双平台冻结软件验证已通过。C3-03 已补散点大小/颜色、ScatterLine 与 XY Bubble、尺度/重叠/命中及 Demo/Usage/包消费，正式双平台冻结软件验证已通过。C3-04 已补圆形图软件范围及 Demo/Usage/包接入，正式双平台冻结软件验证已通过；C3-05 极坐标与雷达软件范围及 Demo/Usage/包消费已通过正式双平台冻结软件验证；C3-06 仪表和微图软件范围及 Demo/Usage/包消费已通过正式双平台冻结软件验证；C3-07 基础分析图软件范围及 Demo/Usage/包消费已通过正式双平台冻结软件验证；下一工作项为 C4-01，各 C3 图族残余和完整阶段出口未自动完成；跨图游标、内嵌范围概览和未来图族语义继续追踪。C4 继续跟踪柱图/gap/实时 PoC 的首次 warmup、十万点 apply/prepare 尾部与分配、全部历史残余、SharedY 首索引规模、增量/LOD、多图预算与内存字节预算。C2-04 已记录的 4 个历史探针 DLL 缺口继续开放，不用新文件替代旧证据。AX12、物理平台及官方许可参考按条件补独立验收。每阶段按实际证据回填矩阵，不因实现可运行而自动关闭平台或正式性能门槛。
