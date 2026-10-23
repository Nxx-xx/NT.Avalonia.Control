# Charts C4-04：高效命中与原始数据身份

执行完成日期：2026-10-20。最终候选 `c4-efficient-hit-source-v1` 已通过 Windows 与 Ubuntu26.04 WSL2/WSLg 软件验证。完成稳定内置笛卡尔投影下的原始点、相邻线性线段、密集共享读数和共享柱布局查询；重叠/相同 X、多轴、MinMax 显示省略点与同坐标换源均保持原始记录。下一项为 C4-05 增量与实时调度。完整 C4 性能、内存、官方对照和物理平台资格仍开放。

## 实现范围与身份

- 点、重复 X、完全重叠、二维非有序和独立坐标轴的命中始终返回原始源索引、StableId、Item 与既有 RawPoint 元数据；普通点 RawPoint 仍为 null，不把投影后或抽样点合成为业务记录。
- 普通点距离并列沿用既有后绘制系列和后物理序号的规则；源 SourceIndex 与不可变快照中物理序号分开处理，重新发布反向物理存储仍保持正确身份。
- 内置稳定 Cartesian 投影通过空间层级约束原始候选，再执行原有精确前向投影、屏幕距离、标记几何和最终可见性判定；无效域、饱和或自定义可变投影使用保守查询。
- 高级散点/气泡保留点大小、颜色、标记覆盖边缘和实际绘制顺序；点足迹层级按视图与样式签名复用，不以中心最近替代现有标记几何规则。
- 共享 X/Y 读数和 Trackball 在相同坐标密集数据中保留既有最近与并列语义；数值来自各自解析到的坐标轴，保持每个系列原始记录。
- 线性 LineSeries 与 ScatterLineSeries 按原始相邻有限数据建立线段层级，查询仍遵循视口裁剪、断点/轴断裂和既有最早系列/线段并列规则；返回原始两个端点以及单独的插值位置。
- 原始线段层级与 MinMax 显示几何分离；显示省略的点仍可命中与框选，插值端点不会换成跨度更大的抽样端点。平滑/阶梯、面积/范围与未知变换保留既有形状缓存或保守路径。
- 柱体和比较图族使用同一份按原始物理序号保存的视图布局进行绘制和命中；组偏移、基线、方向、宽度、标记及区间形状遵循原有精确几何规则。
- 视图、坐标轴、样式和源版本变化分别失效派生缓存；同坐标替换业务源必须返回新 Item/StableId，不能复用旧业务身份。
- Demo 提供重叠点/相同 X/独立右轴、20,000条 MinMax 原始记录、原始段放大、同坐标源替换及两组共享柱布局。原生和编译绑定夹具独立从完整业务数组生成期望。
- 新诊断的cold区间从构造源和设置ItemsSource之后开始，coldBefore已包含一次全源枚举；它记录Show/布局/绘制阶段，不是端到端首次可用。首次惰性查询另行记录；10个暖查询坐标均已预热，零暖线段投影只表示这些重复坐标的缓存命中，不能代表任意新位置或新视口。暖计数区间围绕一次Probe，完整原始扫描、随后Tooltip发布和选择检查位于区间之外，共享模式的Probe本身会更新读数。工作量门槛仅针对固定夹具，时间和线程分配不构成正式尾延迟、FPS、present或长期内存SLA。
- 序列化JSON不能重建实际对象引用；运行中的producer直接比较Tooltip、选择与线段端点的原始Item引用，独立reader从完整原始公式重算持久化的命中序号、ID、坐标、端点与几何。两种证据分开记录，不把JSON中的布尔声明当作独立引用证明。

## Windows 与 Linux 验证

| 项目 | Windows 原生合成窗口 | Ubuntu26.04 WSL2/WSLg |
| --- | --- | --- |
| Charts 测试 | 1817/1817 | 1817/1817 |
| 原生 Demo | 1266/1266 | 1266/1266 |
| 编译绑定 Usage | 344/344 | 344/344 |
| 仅功能包消费者 | 498/498 | 498/498 |
| 功能与 Themes 消费者 | 498/498 | 498/498 |
| Release 警告/错误 | 0/0 | 0/0 |
| 基础/消费者/API/缓存/裁剪/LOD/命中阶段退出码 | 0/0/0/0/0/0/0 | 0/0/0/0/0/0/0 |

冻结 1467 个文件，两平台各 742 个声明运行输入同 SHA，正式运行期间源与实际程序集保持稳定。两类消费者使用全新隔离 NuGet 缓存且无源码项目引用；14个非产品归档逐SHA复用，产品重新构建打包。59份历史报告/API和11版renderer副本保持初始化SHA；当前元数据先深拷贝历史C4-03状态再追加本批。该证明针对声明范围，不表示整个工作区与冻结副本相同，也不证明已删除的历史原始路径仍存在。

Windows初始v1的实际运行在还原阶段因NU1900（沙盒无法访问api.nuget.org）失败，尚未执行Charts测试或原生图表；原结果与日志保留。新的v2包装器对同一冻结source-v1重新执行全部七阶段，使用新输出、全新消费者缓存与独立product-feed，并在前后逐项核对1467个源文件、旧v1收据及14个只读非产品依赖归档。启动请求的自动权限审核超时不代表软件执行失败，相关未执行请求另行记录；没有改写v1失败为通过，也没有关闭audit或更改警告策略。

Linux使用官方固定SDK10.0.402，下载归档按官方release metadata SHA512校验；复用C4-03保留的PowerShell、RG、ICU、X11桌面库和字体。63个非产品项目依赖归档用于新的expanded项目缓存，消费者缓存另行全新创建。SDK10.0.401不满足当前global.json、初次新SDK探测缺少私有ICU环境变量的失败日志仍保留，补齐既有私有环境后运行；没有修改global.json、系统包、NuGet audit或警告策略。prepared-run中沿用旧脚本的缓存复用计划布尔值已由正式前环境readiness收据明确纠正，实际项目缓存从空目录开始。

完整本地API类型 165→165，Charts命名空间 163→163；新增公开类型0、既有类型新增声明0，无旧声明删除。这不认证官方API或二进制等价。旧C4-01/C4-02/C4-03机制和C3-07业务像素门槛均在本候选重跑，原阈值保持不变。

## 命中工作量与独立核对

12个固定场景，各10次预热查询，冷源准备与首次查询单独记录。普通有序百万点、重复X、完全重合、非有序散点、气泡绘制顺序、重复共享X/Y、原始线段/断点、多轴抽样和分组柱均从完整原始数据独立计算期望。多轴抽样场景交替查询两条轴的原始点，不能仅以配置了附加轴认定覆盖成功。

| 场景 | 每系列源规模 | Windows 候选/节点最大事件数 | Linux 候选/节点最大事件数 |
| --- | --- | --- | --- |
| ordered-line-1m | 1000000×1 | 31/31 | 31/31 |
| same-x-vertical-100k | 100000×1 | 23/25 | 23/25 |
| coincident-100k | 100000×1 | 1/1 | 1/1 |
| unordered-scatter-100k | 100000×1 | 25/25 | 25/25 |
| bubble-paint-order-100k | 100000×1 | 118/135 | 118/135 |
| shared-x-duplicates-100k | 100000×1 | 25/25 | 25/25 |
| shared-y-duplicates-100k | 100000×1 | 25/25 | 25/25 |
| segment-ordered-100k | 100000×1 | 13/27 | 13/27 |
| segment-same-x-100k | 100000×1 | 13/27 | 13/27 |
| segment-unordered-gaps-20k | 20000×1 | 10/23 | 10/23 |
| multi-axis-minmax-100k | 100000×2 | 24/28 | 24/28 |
| grouped-same-x-bars-100k | 100000×2 | 1/28 | 1/28 |

计数门槛仅针对上述固定夹具：每次候选事件≤4096、节点事件≤8192，预热源枚举/访问及相关索引重建为0，线段不重新投影整个数据源。事件数不是全部内存读取、距离计算或GPU工作；首次树/屏幕足迹/柱布局仍可能与N相关，极端重叠与长跨越段可能退化。计时和线程分配区间围绕一次Probe，完整oracle与随后Tooltip/选择验证位于计数区间外；共享模式的Probe本身会更新读数。没有正式尾延迟、FPS、present、首次可用或长期内存SLA资格。

新原生检查每平台34项、10张截图。每主题的点图通过独立CRC/deflate/滤波PNG解码，区域由已知业务坐标、轴/plot和96DPI的DIP→像素floor/ceil映射独立重算，检查原始重叠与右轴标记；像素门槛alpha≥250、RGB±24、至少20像素不变。共2张图取得像素核对，另外8张只记录SHA；Tooltip/框选/线段原始身份另由完整数组扫描、实际SelectedPoints序号、原生控件文本和Headless测试验证。两平台各人工查看至少3张代表图，人工查看范围见收据，不能声称全部截图逐张验收。

开发失败保持原收据：首个提权编译因自动审核超时未执行；随后Demo开发编译缺少9个非Charts引用输出，构建这些独立依赖后通过。新reader首版变量range遮蔽导致异常，原脚本/失败记录保留，修正版按同一原始oracle和像素门槛通过；框选序号收据随后改为序列化实际SelectedPoints，并独立记录期望。新柱oracle曾将历史裁剪/插值简化为Clamp造成3例浮点并列期望错误；原完整扫描与新产品结果相同，仅修新oracle并加3条固定查询，原失败TRX、诊断和实际程序集保持。root早期Headless开发build在普通沙盒中exit1但日志0warning/0error，未执行测试；新增benchmark早期CS1061诊断只保留编译修复摘要，缺少原完整日志，不声明该候选编译或运行成功。线段开发v1为88/89，其中新增发布夹具使用非法SourceIndex；修正新夹具后继续验证。完整Headless开发v2为1814项、1811通过与3项新柱oracle失败，新增3条固定回归后的v3为1817/1817；本批87个新增用例由10高级点、26柱、15原始点、16线段和20共享读数组成。正式继承LOD机制reader最初漏复制未改动的旧scenario-name登记文件，0/1失败后按原SHA恢复外部依赖再重审，失败记录和reader原字节仍保留，产品、冻结源及门槛未改。最终资格只来自同一冻结候选的双平台完整验证，开发相邻源码检查点不等于每个旧开发版本全部编译输入均可重建。

## Demo 与截图

运行 `dotnet run --project samples/NT.AvaloniaControl.Demo -- --page charts`，展开 **Charts → 高效命中：重叠、相同 X、多轴与抽样原始记录**。悬停/选择查看原始索引、业务ID和原始数值；可放大相邻段、回到稠密概览或替换同坐标业务源。

- Windows：[截图目录](<<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-efficient-hit-20261020/windows/windows-final-v2>)，共705张PNG，本批命中10张。[概览](<<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-efficient-hit-20261020/windows/windows-final-v2/charts-light-hit-light-overview.png>)、[原始Tooltip](<<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-efficient-hit-20261020/windows/windows-final-v2/charts-light-hit-dark-sampled-original-tooltip.png>)、[相邻段细节](<<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-efficient-hit-20261020/windows/windows-final-v2/charts-light-hit-light-segment-detail.png>)。
- Linux WSLg：[截图目录](<<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-efficient-hit-20261020/linux/run-v1/verification/wslg-final-v1>)，共705张PNG，本批命中10张。[概览](<<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-efficient-hit-20261020/linux/run-v1/verification/wslg-final-v1/charts-light-hit-light-overview.png>)、[原始Tooltip](<<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-efficient-hit-20261020/linux/run-v1/verification/wslg-final-v1/charts-light-hit-dark-sampled-original-tooltip.png>)、[相邻段细节](<<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-efficient-hit-20261020/linux/run-v1/verification/wslg-final-v1/charts-light-hit-light-segment-detail.png>)。

## 剩余问题

- 下一项 C4-05 增量与实时调度；追加/滑动/删除/批量替换、有限队列与背压、跟随开关和暂停恢复继续按计划推进。
- 百万点 hit p95 历史回退0.7649→1.1235 ms仍按用户决定留后续专项，本批索引工作量和正确性通过不等于该历史尾延迟问题取得正式资格。
- 首次映射、屏幕足迹和柱布局仍有源规模相关工作，空间树构建还包含分区或排序成本；极端重叠、超宽标记、长跨越线段、退化数据及自定义变换可能访问大量候选，未承诺所有输入 O(log N) 或 O(1)。原始线段按需投影缓存也可在长时间广域查询后接近全源大小，资源预算留后续验证。
- 完整内存预算、原生/GPU资源、30分钟/8小时长稳、取消与进程级缓存、公平多图调度继续留 C4-05～C4-08/C7。
- 平滑/阶梯、面积/范围及自定义几何的快速线段查询资格仍开放；本批保留其正确的既有路径，不以线性点/段结论概括全部图族。
- 类别组距的全局计算、编码排序及未覆盖图族仍可能具有 N/K 成本；MinMax适用域、极多断点输出、祖先RenderTransform密度与柱/散点密度聚合语义仍沿用C4-03限制。
- Windows原生合成、Headless与Ubuntu26.04 WSL2/WSLg软件检查不代表物理Windows/Linux、Wayland、国产Linux、GPU/compositor present、真实触摸/触控板、多屏DPI或读屏器资格。
- Windows UIA/Linux AT-SPI原生选中集合枚举保留用户延期A11Y-NATIVE-01；AX06标题/末刻度和四份历史PixelProbe DLL缺口保持开放。
- C4-03 Linux run-v2既有导出动画夹具的3项部分暂停前置状态时序失败仍保留；同候选预热重试通过未视为修复。
- 历史外部原始目录被清理后，本批只核对C403保留副本及当前历史报告/API的SHA；不声称被删除原始路径仍存在。
- 完整C2/C3/C4/C7、C5/C6图族、官方许可运行时对照与API/二进制等价继续开放。

完整收据与SHA见[验证清单](charts-c4-efficient-hit-validation.json)，公开声明见[API](charts-c4-efficient-hit-api.txt)。
