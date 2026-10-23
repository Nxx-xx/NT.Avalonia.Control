# Charts 性能与 Windows / Linux 验收附件

## 当前进度与下一步

更新日期：2026-10-23。

**当前进度**：常用图表和 C4 性能引擎已有实现；C5 统计、金融、工程与进阶比较，以及 C6 层级、关系、地图、排程与运营分析的基础图族主项已交付。七类统计缺项已补齐，并接入 Demo、Usage 和独立包消费者。

**下一步**：推进 C7-01 全功能集成，完善各图族共享主题、交互、模板与生命周期；随后完成 Windows/Linux 实机、性能对照、长期稳定性和发布验收。

## 1. 已证事实与当前边界

官方 Charts 产品页公开了高效绘制、智能重绘和实时增量更新的方向；官方 API 文档还说明，关闭 `IsRenderDiagnosticsEnabled` 会跳过诊断分配、计数、Stopwatch 采样和诊断覆盖层。这些可以作为机制对标项，不能转换为特定数据量下的 FPS 或 p95 保证。当前核验的资料没有给出可复现硬件、点数、帧延迟、峰值内存 SLA，也没有足够证据确定其内部降采样算法。[官方产品说明](https://avaloniaui.net/charts)、[CartesianChart API](https://docs.avaloniaui.net/api/avalonia/controls/charts/cartesianchart)

官方说明 Charts 采用 Avalonia 的渲染、布局、文字和主题体系。自研可参考框架对自定义渲染的公开建议：减少 UI 线程绘制工作和分配，按输入变化复用绘图对象；不能据此推断商业 Charts 已采用本附件指定的缓存或索引算法。[官方 Charts 发布说明](https://avaloniaui.net/blog/charts)、[自定义渲染](https://docs.avaloniaui.net/docs/graphics-animation/custom-rendering)

当前已具备裁剪、抽样、命中索引、增量调度、资源预算和自动化性能门禁。下一步在完整图族集成后，验证目标设备的实际呈现、同画质性能、输入延迟和长期内存。

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

C7 只有在最终同一候选产物的适用绝对预算、双平台真实桌面/后端、质量、生命周期、长稳及包消费完成后才能宣布对应发布范围通过。官方对照缺失只能发布自身成绩和公开机制覆盖情况；Linux/麒麟缺设备只能保留待验收状态。
