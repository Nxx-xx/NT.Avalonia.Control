# Charts C0 / C1 执行与验证记录

执行日期：2026-10-15。基于 .NET SDK 10.0.401、net10.0、Avalonia 12.0.3。按 [开发计划](charts-development-plan.md) 先封存 C0，再修改运行库。源码、程序集和数据集均使用 SHA-256 关联；旧报告没有被覆盖或作为新实现的通过证据。

本文件保留首轮 C0/C1 的历史执行结果。后续坐标轴补齐、百万点优化见 [C1 坐标轴与性能续作](charts-c1-axes-performance.md)；最新非单调索引、C2、Windows/Ubuntu26.04 WSLg 的 258/66/30 与两类包消费见 [C2 续作](charts-c2-common.md)。下文的 96/37/14、当时 Linux 不可用和性能回归属于首轮程序集，不能用作当前通过/未通过证据。物理平台、官方对照和正式性能仍有开放项。

## C0 交付

| 工作 | 结果与证据 | 保留条件 |
| --- | --- | --- |
| C0-01 参考范围 | 固定官方 `Avalonia.Controls.Charts 12.3.0`，200 项矩阵、54 个来源 URL/访问日期、包与实际 API 哈希；见 [冻结清单](charts-c0-baseline.json)、[API](charts-c0-api.txt)。 | URL 记录多数为 listed-not-downloaded、正文 hash=null，并非 54 份完整网页快照；元数据也不等同于官方运行验收。 |
| C0-02 本地现状 | 重构前 core DLL `B3989A070EFAAF48BC0D283501BB472DF9CF6C082E3C39C37801C73291A1C27A`，Themes DLL `B607697E804EE44BCA777E00C1C24FCCC1E6EA9B6ABFC24D269BA09641FE15FE`；源码、锁文件、程序集/API 已封存。 | 工作区有其他控件的并行修改，因此以实际输入 manifest 为准，不能仅凭 Git HEAD 重现。 |
| C0-03 测量定义 | [固定工程目标 v1](charts-performance-budgets.json)、Headless harness、不可覆盖的 run 目录、独立 raw guard。区分 source apply、scene prepare、DrawingGroup record、query 与 present。 | UI submit、渲染完成、真实 present 暂无测量；其值为 null，状态 pending / blocked。 |
| C0-04 架构原型 | 8 个数据场景运行通过；旧 DLL 实例探针确认系列/轴无 StyledElement 继承及 Renderer 契约，C1 新 MVVM/扩展回归覆盖这些缺口。 | Linux 字体/输入原型没有执行。实时原型仅小规模批量替换，不是 10k 点/s 或长稳测试。 |
| C0-05 平台入口 | Windows / Linux 共享验证器、Headless / Xvfb / 桌面合成输入分层、GitHub Actions 配置、[平台矩阵](charts-platform-matrix.json)。重构前 53 项测试、31 项原生程序化检查通过。 | CI 仅已配置，尚未远程运行。Linux 当前无可执行环境；Windows 10 独立设备和国产 Linux 目标仍待明确。 |
| C0-06 官方参考 | 独立 opt-in 宿主链接相同数据生成器；官方包实际还原成功，正常构建触发 `AVLIC0001`。见 [宿主说明](../benchmarks/Charts.Reference/README.md) 及 [失败记录](../artifacts/charts-reference/C0-20261015/result.json)。 | `blocked-reference`：缺少有效许可；没有跳过许可门禁，宿主未通过编译及运行，不能输出性能倍率。 |

重构前可追溯报告：[Windows](../artifacts/charts-platform/C0-20261015-platform-before/results.json)、[原型 raw](../artifacts/charts-performance/abe88085e7f045aaa1f40ccf99c09c33/raw.json)、[guard](../artifacts/charts-performance/abe88085e7f045aaa1f40ccf99c09c33/guard.json)。原型目录保留 `frozen-host`、旧 DLL 架构探针和冻结目标关联；6 个伪造/缺失报告变体均被 guard 拒绝。

## C1 实施状态

| 工作 | 本次实现 | 剩余范围 |
| --- | --- | --- |
| C1-01 扩展契约 | `ChartBase`、`IChartProjection`、renderer 的 bounds/render/hit/legend；现有三系列使用相同管线，外部系列测试无需修改中心分支。 | 新投影图族、任意图例模板及共享叠加层按后续阶段扩展。 |
| C1-02 数据映射 | 非泛型 `IEnumerable` 接收数值、ChartPoint、Avalonia Point、对象；嵌套路径、类型缓存访问器、Category/Value/Id/High/Low/Weight；错误事件与快照 issues。 | OHLC、图/层级/空间专用数据视图及逐项颜色尚待相应系列。 |
| C1-03 身份与变化 | 原始项、源索引、稳定 ID、版本快照；Add/Remove/Replace/Move/Reset、嵌套 INPC、批处理合并；命中映射回源项。 | C4 的可见索引、抽样代表点、真正增量计算及滑动窗口尚未实现。 |
| C1-04 MVVM | 轴/系列逻辑树继承；真实 compiled AXAML、动态资源、DataContext 替换、SeriesSource 替换；Viewport / SelectedPoint TwoWay、可观察布局/悬停、点击与重置命令。 | 多选、取消选择模式、选中样式和完整选择事件在 C2；本轮为单点状态/命令底座。 |
| C1-05 轴 | 数值、分类、UTC 日期、对数；显式范围/主间隔、日历刻度、反向、Near/Far、多轴与源键；主轴归一化视图同步；旋转/最多三行换行/密度抽稀/稳定留白。 | 次刻度/次网格、完整 Auto 标签策略、断轴、Plot bands、轴级独立样式、可配置的逐轴缩放限制仍开放，不能称完整官方轴系统。 |
| C1-06 数值语义 | 空源/常量/极端值、重复 X/不排序、无效坐标；Gap / Zero / Average / Interpolate；缺失原值 WasEmpty 保留。 | 堆叠的正负累加、百分比零总量与各范围/统计图专属规则随 C3/C5 实现，当前三系列没有堆叠模式。 |
| C1-07 所有权与异步 | 应用拥有源；UI 线程修改；只读快照、版本/取消/身份校验、UI 发布、映射及通知重入保护；卸载订阅释放；标题/格式/画刷不重置用户视图。 | 发布要求活跃 owner；未观察源不承诺跟踪版本。有限队列、背压与后台复杂布局调度仍属 C4。 |
| C1-08 消费者 | 独立 Charts.Usage；真实打包后以两个全新缓存测试“功能包+自定义主题”与“功能+Themes”，无源码 ProjectReference，核对 nupkg 内 DLL 与执行 DLL。 | Linux 真正包消费执行、裁剪/AOT/各发布方式和长期兼容仍未验收。 |

准确的 API、数据语义、线程/所有权及迁移示例见 [C1 公共契约](charts-c1-contracts.md)、[C1 API 快照](charts-c1-api.txt) 和 [包 README](../src/NT.AvaloniaControl.Charts/README.md)。本次没有升级仓库 Avalonia，也没有引入官方商业包作为自研运行依赖。

已修复并加入回归的关键边界包括：继承属性通知意外取消拖动；InteractionAxes.None 误禁选择；点击命令换源/卸载后继续捕获；显式类别标签与源顺序不一致；选择随类别/索引/范围/尺寸更新；自定义 bounds 被原始点扩大；旋转标签留白不足；无效 renderer 命中位置；渲染过程中发布状态引发再次失效。

## 实际验证

最终本机证据入口为 [C1 Windows 报告](../artifacts/charts-platform/C1-20261015-platform-final-v2/results.json)，其输入源码与程序集首尾一致性需为 true。第一次 C1 通过报告仍保留，最终版本另建 v2 目录；后者补齐次轴标题的留白。

| 检查 | 结果 | 证据边界 |
| --- | --- | --- |
| 整个 slnx 的 Release 构建 | 通过，0 warning / 0 error | 包含新增 Usage / benchmark；官方 opt-in 参考项目不在 slnx 内。 |
| Charts Headless | 96 passed，0 failed / skipped | 含历史 53 项及数据/轴/MVVM/扩展/交互回归；输入为 Headless 注入。 |
| Windows 原生 Demo | 37 项程序化检查通过 | 31 项原检查 + 6 项 C1 场景；原生窗口 + RenderTargetBitmap + 合成路由，非物理设备/系统桌面截图。 |
| Windows 原生 Usage | 14 项检查通过 | 编译绑定、INPC、资源、替换源、视图绑定、卸载/重挂；无性能结论。 |
| 独立 nupkg 消费 | 两种均通过 | 每种从新包缓存还原并运行，locked restore、无源码引用，Headless 消费契约。 |
| C0 / C1 原型 | 各 8 场景，producer / guard 通过 | 每场景 1 次预热 + 2 次记录，每样本 5 次 query；正式门槛 pending-C4。 |
| Linux 脚本 | PowerShell/JSON/YAML 静态检查；Bash `-n` 通过 | Bash 语法检查在 Windows Git Bash 执行，不能算 Linux 运行。 |
| Linux 实际构建/窗口/包消费 | 未运行 | WSL 查询返回 E_ACCESSDENIED；Docker CLI 存在但 daemon 未运行。 |
| 官方性能对照 | blocked-reference | 包已固定/还原；许可门禁阻止构建及初始化。 |

当前主机为 Windows x64，内核 10.0.26200，registry displayVersion=25H2、edition=CoreCountrySpecific；registry ProductName 与内核名称不能单独确认 Windows 市场发行名。SDK 10.0.401、运行时 10.0.12、Avalonia 12.0.3；GPU/字体清单可见报告 `environment.json`，但实际图形后端没有被独立确认。DPI 1.0 的程序化检查不能代替 100/150/200%、跨屏、触控板、触摸、读屏器及真实 present。

Demo 的 Charts 页面增加可展开的“更多示例：业务对象、日期轴、对数轴与多坐标轴”。运行：

```powershell
dotnet run --project samples/NT.AvaloniaControl.Demo -- --page charts
dotnet run --project samples/Charts.Usage
pwsh -NoProfile -File scripts/Verify-Charts.ps1 -Mode NativeSynthetic -IncludeC1
# 在具备依赖的 Linux 环境执行，Xvfb 和桌面分别留证：
bash scripts/Verify-Charts-Linux.sh --mode headless -IncludeC1
bash scripts/Verify-Charts-Linux.sh --mode xvfb -IncludeC1
bash scripts/Verify-Charts-Linux.sh --mode desktop -IncludeC1
```

运行器需要 .NET 10.0.401、PowerShell 7、rg；Linux 原生依赖及 CJK 字体见 charts.yml。每次使用新的 RunId，失败证据也保留。若本机 NuGet 漏洞查询不可达，可单次还原时显式 `-p:NuGetAudit=false` 后使用 no-restore build 与 benchmark `-SkipBuild`；不修改仓库默认审计配置。本轮一次 benchmark 构建的 NU1900 失败保留在 `artifacts/charts-performance/C1-20261015-after`，修复环境后的结果另存 `C1-20261015-after-v2`。

## 性能原型与已知回归

详见 [机器可读比较](charts-c0-c1-performance-results.json)；C0 raw 与 C1 raw 均保留，数据种子及 SHA 相同。以下为本次少量样本的 p95 诊断，单位 ms：

| 场景 | scene prepare，C0 → C1 | render record，C0 → C1 | hit query，C0 → C1 |
| --- | --- | --- | --- |
| 10k 折线 | 1.07 → 5.07 | 1.32 → 2.08 | 0.50 → 0.93 |
| 10k 柱图 | 2.34 → 7.81 | 20.02 → 19.98 | 10.55 → 2.13 |
| 100k 折线 | 6.81 → 20.75 | 6.12 → 14.18 | 3.17 → 7.37 |
| 1m 折线 | 24.25 → 154.03 | 28.44 → 44.85 | 20.00 → 45.45 |

这些数字包含 JIT/GC、机器负载与同进程顺序执行影响，正式倍率门禁尚不能据此判定；实际绘图区高度还由 C0 的 588 DIP 变为 C1 的 582 DIP，画质未做像素等同确认。DrawingGroup record 不包含 GPU/合成器呈现，也不能换算成 FPS。

C1 为业务项/ID/token/映射快照增加了分配。百万点样本后的进程 PrivateMemory 读数约从 316 MiB 到 1026 MiB；这是整个顺序运行进程的当前私有内存，**不是该数据集独占增量、GPU/native 分项或泄漏判定**。本轮未采 allocation trace，不能把全部差值归因于某个字段。性能与资源目标仍为未验收，禁止通过降低 v1 门槛将本轮诊断改标达标。

## 剩余问题与下一步

| 优先级 / 编号 | 问题 | 后续处理与退出条件 |
| --- | --- | --- |
| P1 / C1-05 | 次刻度/次网格、断轴/Plot bands、完整 Auto 标签和逐轴缩放限制未完成。 | 保留 C1/C2 交叉项；统一轴布局/投影/命中后补契约、Demo、数学与视觉回归。 |
| P1 / C4-01、04、06 | 大数据 scene/hit 与内存有回归，仍是全量快照与线性命中。 | 对快照/token/点拷贝做 trace；专用数值/ChartPoint 快路径、版本化范围/几何缓存、共享柱布局和查询索引；保留当前 raw 再复测。 |
| P1 / OS01～OS13 | Linux 没有实际执行；Windows 设备/DPI/输入及读屏矩阵也未完成。 | 在 Ubuntu 22.04/24.04 独立运行脚本与包消费者，继而真桌面/字体/输入；国产 Linux 需冻结确切设备和系统。 |
| P1 / C0-06、PERF11 | 官方宿主被许可门禁阻塞。 | 获得有效运行条件后正常构建并对齐画质、场景、硬件；补同机 raw，不能以 docs 推断倍率。 |
| P2 / C2 | 单点选择状态已有；完整选择模式、共享/模板 Tooltip、Trackball、触摸/键盘、图例模板和导出仍待实现。 | 逐项按功能矩阵推进；增加取消、卸载和多轴交互测试。 |
| P2 / C3、C5 | High/Low/Weight 可映射，但范围/堆叠/金融/统计图及零总量等算法尚未实现。 | 实现对应系列和数值参照，不把存在字段当成图型完成。 |
| P2 / C4、C7 | 无真实帧呈现、正式 5 进程采样、30 分钟/8 小时更新和资源稳定性证据。 | 建原生采样/质量守卫/长稳与平台设备矩阵，再判断冻结门槛。 |
| P2 / C7 | API 有预发布迁移变化，完整官方 API 与 AOT/裁剪未承诺。 | 使用 C0/C1 API 快照、隔离消费者和迁移说明跟踪；逐发布方式留证。 |

当前可以继续在这个底座上开发 C2/C3，并提前推进 C4 的数据缓存与命中原型；以上开放项继续保留在矩阵中。
