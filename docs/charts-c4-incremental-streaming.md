# Charts C4-05：增量数据与实时调度

执行日期：2026-10-20。候选 `c4-incremental-streaming-source-v1` 已通过 Windows 与 Ubuntu26.04 WSL2/WSLg 软件验证。提供分页有限窗口数据源、有界后台到 UI 调度、多源批量提交、跟随最新数据及独立暂停恢复。下一项为 C4-06 绘图与资源预算，完整性能、内存和物理平台资格继续开放。

## 实现范围

- 新增 ChartDataBuffer<T> 有限容量数据源，支持追加、插入、删除、批量替换与清空；超容量保留最后 Capacity 条，显式删除与容量淘汰分别统计。每次有效批量修改只发布一次集合通知和源版本，输入枚举失败不发布部分状态；修改必须在 UI 线程执行。 AppendRange 为保证失败迭代器不修改源，先枚举整个输入批并创建新页后才按容量裁剪，瞬时内存可随输入批大小超过保留容量。
- 数据源、映射记录与极值摘要采用不可变分页状态；存在 live Changed owner/附图订阅且映射配置未变时，适用的 Gap/Zero 普通对象映射在追加与滑动窗口中复用保留页，仅复制和映射新行。旧快照保留旧顺序、坐标、元数据和业务引用；由 buffer 映射创建的快照中 SourceIndex 是该快照的当前序号；公开发布/重排快照仍可保留显式 SourceIndex，跨滑动窗口使用显式 X 与 StableId 标识绝对业务记录。旧快照不深拷贝调用者 Item 对象。
- 新增 ChartStreamingSession<T> 将后台生产者的 TryAppend/AppendAsync 调度到 UI 回调，队列最多两批并包含执行中的批次；一次调度可以合并待提交批次。最多一个异步等待生产者，其他等待明确抛错；TryAppend 满时返回 false，没有静默丢弃。AppendAsync 在接受并复制输入后完成，调用者在接受前保留输入内存。 两批加一名等待者只保证批次与样本计数上限，不能界定任意 T 对象图的实际字节。
- 支持暂停、恢复、取消、完成、释放、Flush 和统计。暂停允许接收到容量；Flush 等待此前已接受数据完成 UI 回调，Complete 停止接受并恢复排空。取消清理未执行批次但不能打断已进入回调。CommittedSamples 表示成功 UI 源回调，不表示已显示帧；回调失败不自动重试，不保证任意用户修改之间的事务回滚。
- CartesianChart.DeferDataUpdates 支持嵌套，合并多数据源变更后的选择核对与刷新请求，数据缓存仍随每次变更立即失效；作用域内查询仍须看到当前数据。FollowLatestData/FollowLatestWindow 控制主轴最新窗口，用户拖动、缩放、显式视口或重置暂停跟随，重新启用追到最新；暂停提交与暂停跟随独立。运行时配置或替换坐标轴不误停内部跟随策略。
- Demo 展示四路有限窗口、后台生产、暂停恢复、停止、手动追加、查看完整窗口、删除与替换、跟随开关、原始业务 ID 和队列统计。Usage 使用编译绑定公开 API，两类仅包消费者复用 Usage 夹具且禁止源码项目引用。
- 结构诊断为四路共一百万保留行、120次每批500新行；初始用户对象与每批输入构造位于计数区间之前，完整原始 oracle 位于区间之后。每批 buffer 新页 payload 复制500行、对象新行映射500行，CopiedItemCount不包含Session接受ToArray与合并数组拷贝，映射阶段X/Y getter各500，整体getter各不超过1500，分页摘要访问不超过2548。新状态目录条目数另报；该计数不包含全部临时 slice 遍历或整页摘要合并工作，后两者仍为 O(P)，没有完整 P 工作门禁；虚拟50ms不证明实际定时10000点/秒吞吐。
- 独立附图诊断为四路共一百万行的真实 Headless CartesianChart，Numeric/SamplingNone、512单位窄窗，共12次每批500行，验证跟随、历史窗口暂停与恢复、淘汰与存活选择和原始命中。暖区间包含源、UI回调、选择维护、布局、场景、DrawingGroup绘制和RunJobs；初始 Row/Id、App、系列、源赋值与初始命中在 cold 区间之外，cold 不表示首次可用；首次业务ID缓存启动另报，随后命中、导航和完整oracle在暖区间之外；未测 framebuffer/GPU/compositor/present。
- 运行时直接比较原始 Item 引用；独立 reader 从完整公式重新计算新旧快照当前序号、业务ID、IEEE754坐标SHA，以及小型修改夹具全部原始行和队列顺序。JSON不能重建实际对象引用，不将布尔引用声明等同于独立引用证明。
- 两主题原生夹具验证后台线程到UI、暂停背压、合并取消、滑动删除身份、实际routed Tooltip与业务图形。独立PNG reader用预登记颜色、业务坐标、轴/plot和96DPI重算四区域，阈值alpha≥250、RGB±24、至少10像素不变；每平台仅两张chart-only图取得像素核对，另四张只记录SHA。

## Windows 与 Linux 验证

| 项目 | Windows 原生合成 | Ubuntu26.04 WSL2/WSLg |
| --- | --- | --- |
| Charts 测试 | 1868/1868 | 1868/1868 |
| 原生 Demo | 1320/1320 | 1320/1320 |
| 编译绑定 Usage | 361/361 | 361/361 |
| 仅功能包消费者 | 517/517 | 517/517 |
| 功能与 Themes 消费者 | 517/517 | 517/517 |
| Release 警告/错误 | 0/0 | 0/0 |
| 基础/消费者/API/缓存/裁剪/LOD/命中/增量阶段退出码 | 0/0/0/0/0/0/0/0 | 0/0/0/0/0/0/0/0 |

两份独立物理冻结副本共 1490 个源文件，内容逐 SHA 相同；两平台各 758 个声明运行输入同 SHA。Windows 与 Linux 分别使用独立 bin/obj 和从空目录开始的项目缓存；两种消费者另用全新 NuGet 缓存和重新构建的产品包，没有源码项目引用。14个只读非产品消费者依赖归档与63个项目依赖归档按 SHA 复用，没有关闭 audit 或更改警告策略。62份历史文档/API与12版renderer保留副本通过核对，元数据深拷贝 C4-04 后追加新状态。活动工作区另有并行 PdfViewer 修改，资格绑定冻结副本，不宣称活动整仓等于冻结副本。

Linux 复用此前按官方 SHA512 校验的 SDK10.0.402，以及保留的私有 PowerShell、RG、ICU、X11库与字体；实际发行版名为 Ubuntu，环境为 Ubuntu26.04 WSL2/WSLg。实际 GPU/backend、物理桌面、原生 Wayland 与 compositor present 未取得资格。

完整本地 API 类型 165→168，Charts命名空间 163→166；新增公开类型3、既有类型新增声明5，无旧类型/声明删除。这是本地声明兼容核对，不认证官方API或二进制等价。C4-01～C4-04机制和旧业务像素门槛在本候选重新执行，原阈值不变。

## 增量工作量与测量边界

4×250000条的源夹具执行120轮，每轮4×125=500条新数据；独立 reader 完整重建初始和最终每条原始记录的序号、ID、坐标位模式摘要，另验证小型插入/替换/删除/清空夹具全部行、保留旧快照及调度队列顺序。适用域中每轮 buffer 新页 payload 复制与对象新行映射恰为500行；CopiedItemCount不覆盖Session接收ToArray及合并拷贝，映射X/Y getter各500，含新增/移除观察的整体getter各不超过1500，摘要访问不超过2548；页目录条目另列，临时slice遍历和整页摘要合并仍有未完整计数的O(P)成本。

| 诊断结果 | Windows | Linux WSLg |
| --- | --- | --- |
| 源更新中位数 / p95 / 最大 ms | 3.700 / 10.686 / 24.199 | 4.053 / 10.055 / 33.626 |
| 每轮源分配最大 bytes | 1682776 | 1682776 |
| 每轮新目录条目范围 | [3912, 4156] | [3912, 4156] |
| 附图每轮投影点范围 | [2080, 2108] | [2080, 2108] |
| 附图每轮视图构建范围 | [1, 1] | [1, 1] |
| 附图每轮数据索引构建范围 | [0, 0] | [0, 0] |
| 附图每轮摘要访问范围 | [524, 1144] | [524, 1144] |

真实附图是 Headless Numeric/SamplingNone 窄窗绘图夹具，12轮、每轮500新行。每轮 buffer payload复制/对象映射500、摘要≤2548、投影1..8192、视图构建≤8、defer提交与UI批次各1，验证历史视口暂停与恢复、淘汰选择移除、存活选择重定位及实际原始命中。冷区间排除用户对象/ID、App/系列构造、源赋值与初始命中，不代表首次可用；首次ID缓存启动另列。暖区间包含源、UI回调、选择维护、布局、场景、DrawingGroup与RunJobs，随后命中、导航与完整oracle在区间外。没有栅格化、显示延迟、GPU或present资格。

上述毫秒和线程分配仅是本机固定夹具诊断；虚拟50ms节奏没有实际等待，不证明定时10000点/秒、正式尾延迟、FPS、内存预算或30分钟/8小时稳定性。新状态目录条目不是完整P工作计数。

新原生检查每平台54项、两主题6张PNG；独立reader逐行核对实际序列化的新旧窗口原始行和队列统计、UI线程路线、routed Tooltip，按冻结前预登记公式从已知业务坐标独立解码两张chart-only图的四路像素区域，alpha≥250、RGB±24、至少10像素阈值未改。其他4张新PNG只记录SHA；每平台人工查看3张代表图，范围见单独收据，未逐张人工验收全部截图。

开发记录保留：headless-v1在产品编译遇两项CS9124，v2为新测试误用ChartPointHit.Id的CS1061，均未执行测试。v3执行1867项、1856通过和11失败：两项标量装箱类型真实问题、一项重复摘要partial扫描、一项新插入/替换夹具算术错误，以及七项内部重置错误暂停跟随。修复标量分支、快照级摘要缓存和内部/用户重置分离，修正新夹具算术并新增运行时换轴回归后，v4为1868/1868。Demo开发v1为54/54且Usage361/361，benchmark开发四夹具通过；最终资格仍只来自同一冻结候选双平台完整执行。外部tooling-v1因contracts文件未落盘而归档中止，partial目录和失败记录保留；补齐外部文件后归档v2，不涉及产品或门槛。继承hit reader在首执行前以新脚本更新历史文件数量1467→本批1490并补齐原SHA的pr3依赖，数值公式和工作门槛未改。

独立包payload reader首跑沿用review版本v2推导Linux source-v2，发生缺目录的外部绑定错误；失败v2收据和原脚本保留。另建reader从authoritative.sourceDirectory绑定同一冻结source-v1，14/14通过，产品包与阈值未改。正式结束后仅两处README措辞补充未观察源的poll全映射和目录计数范围，冻结原字节保留且未重编。

## Demo 与截图

运行 `dotnet run --project samples/NT.AvaloniaControl.Demo -- --page charts`，展开 **Charts → 增量与实时调度：有限窗口、背压、暂停与跟随**。开始后台四路数据后，可暂停提交、拖动查看历史、切换跟随、手动追加/删除/替换，并查看窗口容量、原始ID和队列统计。停止后再次开始会创建新会话。

- Windows：[截图目录](<<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-incremental-streaming-20261020/windows/windows-final-v1>)，共711张PNG，本批6张。[暂停队列](<<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-incremental-streaming-20261020/windows/windows-final-v1/charts-light-stream-light-paused-queue.png>)、[恢复与Tooltip](<<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-incremental-streaming-20261020/windows/windows-final-v1/charts-light-stream-light-resumed-follow.png>)、[暗色四路图](<<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-incremental-streaming-20261020/windows/windows-final-v1/charts-light-stream-dark-four-stream-chart.png>)。
- Linux WSLg：[截图目录](<<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-incremental-streaming-20261020/linux/run-v1/verification/wslg-final-v1>)，共711张PNG，本批6张。[暂停队列](<<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-incremental-streaming-20261020/linux/run-v1/verification/wslg-final-v1/charts-light-stream-light-paused-queue.png>)、[恢复与Tooltip](<<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-incremental-streaming-20261020/linux/run-v1/verification/wslg-final-v1/charts-light-stream-light-resumed-follow.png>)、[暗色四路图](<<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-incremental-streaming-20261020/linux/run-v1/verification/wslg-final-v1/charts-light-stream-dark-four-stream-chart.png>)。

## 剩余问题

- 下一项 C4-06 绘图与资源预算；完整 C4、C2/C3/C7、官方许可运行时对照与 API/二进制等价继续开放。
- 分页目录复制为O(P)，独立小批次不压实旧尾页，长期单点批次或中间编辑页数可能接近行数；压实、缓存字节预算、旧快照持有成本与公平多图调度仍需验证。 DirectoryEntryCount 仅统计新 state 的目录条目数，不能当作所有 P 相关工作量或内存分配的完整计数。 Buffer retained Capacity 不是总字节硬上限，AppendRange 的暂存输入可超过 Capacity；Session maximumBatchSize 约束接收数组，任意 T 引用对象与等待输入仍由调用者管理。
- 增量快速路径只对 ChartDataBuffer 与已记录映射策略成立；普通 ObservableCollection、INPC可变业务项或映射属性变化、Average/Interpolate与范围Zero修补等仍有全量路径。 无 live owner 的 Series.CreateSnapshot 沿用轮询语义，会主动全量重映射，即使用 Buffer 也不属于本轮增量快速路径。
- 日期/对数/断轴/自定义域过滤仍可能每数据版本扫描全源，非法值还可能构造N级overlay；类别/排序/堆叠/点编码/柱布局、新版本二维/共享读数/线段/LOD索引和动画几何仍有N级工作。本轮百万行附图仅验证Numeric/SamplingNone窄窗，不承诺全部图族或更新O(delta)。
- 队列批次与样本有上限，回调、布局、首次索引与用户对象分配仍可能耗时；取消不能打断执行中的UI回调。UI提交与DrawingGroup计数不是输入到最终显示延迟、GPU/present或稳定FPS资格。
- 30分钟/8小时长稳、进程私有/原生/GPU内存、缓存取消回收、实际定时持续吞吐和多图公平尚未执行，短诊断线程分配不代表完整内存回归。
- 百万点hit p95历史回退0.7649→1.1235ms按用户决定留后续专项；数据更新和身份正确性门槛通过不等于该尾延迟问题取得正式资格。
- Windows原生合成、Ubuntu26.04 WSL2/WSLg和Headless检查不代表物理Windows/Linux、原生Wayland、国产Linux、实际GPU/backend、真实触摸/触控板、多屏DPI与读屏器资格。
- Windows UIA/Linux AT-SPI原生选中项枚举保留用户延期A11Y-NATIVE-01；AX06标题/末刻度和四份历史PixelProbe DLL缺口仍开放。
- C4-03 Linux run-v2旧导出动画夹具三项部分暂停前置状态时序失败仍保留，同源码预热重试通过未视为修复。历史原始目录清理后仅核对保留副本和报告/API SHA，不声称被删除路径仍存在。

详细SHA与边界见[验证清单](charts-c4-incremental-streaming-validation.json)，公开声明见[API](charts-c4-incremental-streaming-api.txt)。
