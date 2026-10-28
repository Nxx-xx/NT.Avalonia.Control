# TreeDataGrid A6 平台验收与复跑

2026-10-27 本地预览收尾：当前 0.2.0-preview.2 的 Windows 本地自动验收与交付准备完成。产品、主题、宿主/客户端执行代码保持冻结，默认宿主复用保留；最终 565 项控件测试、11 项收尾工具测试及完整本地流程通过，独立 TreeDataGrid.Usage 发布与原生窗口启动通过。验证入口新增磁盘预检、失败摘要和证据索引；同产品构建的正式 UIA/30 分钟耐久原始证据重新校验后复用。本地候选包、示例、文档与 SHA256/ZIP 校验由交付工具输出。真实读屏/物理交互/DPI/干净部署、实际 CI、麒麟与业务预算继续待验，整体平台 A6 保持开放；当前本地开发进入维护，新增工作另行排期。详见[本地预览收尾](treedatagrid-local-preview-closeout.md)。以下为历史轮次。

2026-10-27 后续：已修复受控 UIA 客户端的接口引用寿命，默认使用显式 COM 作用域释放；产品 DLL 与默认宿主复用保持字节一致。30 分钟 / 899 轮增强活动 UIA 耐久通过，私有内存稳态增量 5.66 MiB；同一诊断宿主的活动堆由旧后端 14 个表格降至 1 个当前活动表格。565 项控件测试、150 项独立校验、7 项所有权自检和完整本地流程通过，四次同状态无客户端复测无调查项。该结论限定本轮受控客户端；旧后端增长和历史失败保留，真实读屏/平台/CI/业务预算仍待验，A6 保持开放。详见[退休寿命修复与验证](treedatagrid-a6-uia-retirement-status.md)。以下为历史轮次。

2026-10-27：活动 UIA 分组归因与三处热点修复已落地，565 项控件测试、135 项独立校验及完整本地流程通过；无客户端 ABBA 的 6000 请求快照/图像一致，默认宿主复用收益保留。修复后正式 UIA 读取/事件批次通过，但 600.95 秒/194 轮增强混合耐久私有内存仍增长约 3.54 GiB，继续失败；活动堆观察到 COM/peer/模板/合成保留链，关闭 provider 后零保留不能覆盖增长。旧版 UIA 性能基线超时，尚不声明活动负载的正式前后收益。详见[本轮归因与验证](treedatagrid-a6-uia-attribution-status.md)。A6 与真实平台/业务验收保持开放，以下为历史轮次。

2026-10-26 本轮验证已执行完毕，整体未通过：560 项控件回归、123 项独立校验测试及当前功能 DLL 的 1800.68 秒/900 轮增强耐久通过，3608454 个跟踪对象回收；同状态四进程未触发 P95 版本回退规则，默认复用收益保留。正式外部 UIA 读取/事件批次通过，但 603.85 秒/139 轮混合耐久私有内存稳态增长约 3.20 GiB，违反原护栏；provider 关闭后零保留不能覆盖此失败。活动 UIA 内存/延迟、持续连接缓存及真实平台/业务预算继续开放，详见[本轮记录](treedatagrid-a6-automation-soak-status.md)。以下为历史轮次。

2026-10-24 自定义工厂长尾修复：自动行高的完整拆挂路径按当前视口预算保留已清理备用行，减少宿主/模板重建。557 项控件回归、90 项校验、4512 条几何快照及本构建 600.56 秒/301 轮原生耐久通过，1204728 个退休对象零保留。正式同状态四进程的自定义工厂 UI P95/分配下降 18.87%/15.31%，渲染完成 P99 下降 19.32%，默认复用收益保留；本次协议内长尾调查收敛，见[本轮记录](treedatagrid-a6-auto-factory-tail-status.md)。历史遥测警告、真实平台/CI/麒麟和业务预算继续单列，整体 A6 未关闭；以下为先前轮次。

2026-10-24 自动行高修复：默认宿主复用已启用，模板测量链缓存与宿主退休边界通过验证。546 项控件回归、68 项原生记录校验、9 项几何校验及 4512 条旧新几何快照通过；增强原生耐久 1800.44 秒/901 轮、3620276 个退休对象最终零保留。首轮宿主自动化缓存失败及性能自动化状态差异均保留，修正后的证据与限制见[本轮记录](treedatagrid-a6-auto-height-status.md)。2 次原始 Any 警告、真实 Windows/CI/麒麟和业务预算继续单列，整体 A6 未关闭；以下是先前轮次。

2026-10-24 导航修复复跑：最新控件回归为 535 项，原生记录测试 57 项。增强耐久使用 `-RefreshSourceDuringSoak -BlockingGcReference`，显式覆盖同源刷新并保留 Any 原始快照及警告，详见[修复与复跑证据](treedatagrid-a6-navigation-tail-status.md)。本次协议内导航性能回退已收敛，Windows 8 类和完整平台 9 类仍分别待验；没有用阻塞 GC 参考替代不可用的当前堆估计或物理验收。

2026-10-24 后续：最新本地完整验收为 523 项控件测试，见 [regression-6](../artifacts/treedatagrid-alignment/a6-telemetry-scroll-20261024/regression-6/results.json)。原生宿主现使用 v2 托管堆交叉遥测；`GC.GetTotalMemory(false)` 原始负值标为可选估计不可用，必需 last-GC/代汇总不一致仍失败。默认/自定义工厂交替耐久、原生几何一致性、正式对照和输入快照解释见[本轮记录](treedatagrid-a6-telemetry-scroll-status.md)。本地范围通过继续与 Windows 8 类、完整平台 9 类待验分开，不能用新遥测或批次渲染完成记录代替物理呈现、输入延迟和业务预算。

日期：2026-10-23。版本仍为 0.2.0-preview.2。A6 分为本地自动验收、Windows 发行验收、Windows + 麒麟完整验收三种范围；它们分别输出状态，不能用本机自动测试关闭后两项。当前结果由 [A6 执行记录](treedatagrid-alignment-a6-status.md)维护。

## 本地自动验证入口

在仓库根目录执行：

```powershell
./scripts/Test-TreeDataGridAlignment.ps1 -OutputDirectory artifacts/treedatagrid-alignment/a6-new-run
```

每次使用新的输出目录。脚本执行 TreeDataGrid Core/Headless/Themes 的锁定还原和 Release 测试、验收校验器测试、12 场景各 5 次数据/几何测量、三种组合各 5 轮自动行高滚动、40 轮生命周期回收、两个无源码引用的 NuGet 消费者、Usage/Demo 构建、独立发布目录的原生检查与图库导航。运行前后比较范围文件，核对测试/示例/消费者/包内/发布目录的程序集哈希。

没有网络的环境可显式提供依赖 nupkg 目录：

```powershell
./scripts/Test-TreeDataGridAlignment.ps1 `
  -OutputDirectory artifacts/treedatagrid-alignment/a6-offline-run `
  -DependencyFeed D:/packages/treedatagrid-dependencies
```

该目录只能含依赖，不能含 NT.AvaloniaControl.TreeDataGrid 的功能/主题包，防止同版本旧包混入。本轮产品包始终由当前构建打入 artifacts/packages。两个独立消费者每次建立自己的空包缓存和构建配置边界；离线源不证明在线还原或干净操作系统部署已通过。仓库项目的离线还原可以使用本机已有的全局缓存；缺包时正常失败，不关闭告警或改变锁文件。

`profiles/ui-profile.json` 记录相同视口、换行自动行高下的 reflection-default、controlled-events-default、controlled-events-factory 三组。每组 5 个新表格样本，各先 100 次预热，再 80 次双轴滚动/布局；测量 UI 线程分配、布局耗时、创建数与容器上界。它是 Headless 测量，初轮仍可能含 JIT/缓存效应；不表示原生 FPS 或输入延迟。

`profiles/durability.json` 在换源、模板、主题、列头、受控绑定、一次编辑写回与批量移动组合中跟踪退休模型、Source、工厂、context、行、单元格、列头、presenter 和逻辑 peer。断言在不内联的操作栈返回、dispatcher/layout/render 排空及完整 GC 后检查。40 轮属于有限生命周期检查，不表示数小时原生耐久预算。

发布目录使用本机已安装的 .NET 10 运行时；从该目录执行 Demo 的 161/29/36 项原生程序化检查，记录 application-input.json 和环境。它验证本机发布目录运行，不代表干净机器、自包含部署、物理输入或桌面截图。渲染图仍是 RenderTargetBitmap。

## 验收矩阵与校验

原生性能和耐久使用单独的 [Windows 测量宿主](../benchmarks/TreeDataGrid.NativeBenchmarks/README.md)，复跑入口为 `scripts/Test-TreeDataGridNativePerformance.ps1`。当前 6 场景/30 轮原生渲染完成测量和 600.50 秒/301 轮耐久已完成，结果见[原生补测记录](treedatagrid-native-performance-a6.md)。该证据区别于先前 Headless profile，也不替代屏幕呈现 FPS、物理输入延迟、数小时负载和业务预算。

后续热点分析及首轮 Source 优化见[原生优化记录](treedatagrid-native-optimization-a6.md)。诊断使用独立的 `scripts/Trace-TreeDataGridNative.ps1`，采样结果只用于定位；正式测量关闭采样。原生驱动的 `-ApplicationDirectory` 可复制已有隔离应用，便于固定宿主/主题/依赖字节，仅更换功能 DLL 做对照；输入应用和新证据目录不能嵌套。`-DurationSeconds 1800` 运行三十分钟正式耐久。`scripts/Compare-TreeDataGridNativePerformance.ps1` 另行按优化后→优化前顺序运行两个性能进程，可用 `-WaitForResults` 等待前一轮完整通过，避免并行原生负载。不覆盖首轮数据，也不把反向复跑当成对失效样本的重试。

[矩阵模板](treedatagrid-platform-matrix.template.json)固定必需项目。每次运行生成 platform-matrix.json，填入本地结果的路径和 SHA256；模板本身不是执行证据。校验器可分别检查三个范围：

```powershell
python scripts/verify_treedatagrid_acceptance.py <运行目录>/platform-matrix.json --scope local
python scripts/verify_treedatagrid_acceptance.py <运行目录>/platform-matrix.json --scope windows
python scripts/verify_treedatagrid_acceptance.py <运行目录>/platform-matrix.json --scope windows-kylin
```

退出 0 表示该范围记录通过，2 表示仍有未完成项目，1 表示记录/哈希无效。删除必需行、重复行、错误 kind、缺少文件、哈希变化或把 Headless 结果写入物理验收会被拒绝。本地自动通过时 Windows/麒麟仍应输出未完成。校验器只检查证据结构和一致性，不能代替人工确认记录内容属实。

物理记录采用单独 JSON，`evidenceKind=PhysicalInteraction`，`requirement` 对应下表 id，包含 passed、recordedBy、executedUtc、environment、expected、observed、`captureKind=Desktop` 和 attachments（实际桌面图像/录像的 path、sha256）。路径相对于仓库根目录。完成操作并保留材料后再将矩阵对应行设为 Passed；不能预先生成通过记录。

| 项目 id | 必须记录的操作与预期 |
| --- | --- |
| windows-ime | 真实中文预编辑、候选翻页/选词、Enter/Esc/Tab；换主题/工厂、滚动回收和错误修复时不抢选词、不重复或错目标写回；记录 IME 名称和版本 |
| windows-drag | 物理列排序/调宽/重排区分、冻结边界、行 Before/After/Inside、批量拖放取消、悬停展开、自动滚动、Esc/捕获丢失和换源；核对选择/身份与一次提交 |
| windows-clipboard-focus | 实际编辑器复制/粘贴、窗口切换与焦点恢复；模板内控件保留输入，草稿和当前单元格一致；此项不宣称支持网格选区导出 |
| windows-font-dpi | 中文/生僻字实际回退；100%/125%/150%/200% 及跨不同 DPI 显示器，核对列头/冻结边界/编辑器/拖放命中/候选窗；记录实际 RenderScaling 与字体 |
| windows-screenreader | 真实读屏软件和版本；表格名、标题、深度/展开、位置、选择取消、错误、批量移动及换工厂后的焦点/公告；检查平台桥接实际收到事件 |
| windows-clean-deployment | 干净 Windows 环境中的安装、启动、在线包还原、运行时/原生依赖诊断和自定义模板；记录具体部署方式，本机发布目录运行不能替代 |
| windows-native-performance | 按明确硬件/视口/数据/主题重复测量原生帧率、输入延迟和长时间内存趋势，依据业务预算判定；记录工具、采样时间与负载，不借用 Headless 毫秒数 |
| windows-ci | 实际 Windows runner job 链接、运行器环境、窗口创建、测试/包/发布探针输出；配置文件存在不算通过 |
| kylin-desktop | 先明确发行版/版本/架构、X11/Wayland、运行时、原生依赖、字体、输入法、读屏与部署方式，再完成同一功能、输入/DPI/无障碍、部署和性能矩阵；普通 Linux 构建不能代替 |

更详细的人工步骤沿用[发行准备](treedatagrid-release-readiness.md#windows-待验证的手工步骤)和[无障碍说明](treedatagrid-accessibility.md)。建议按每台设备/配置独立归档，不把多台设备的结果拼成一次运行。资料缺失保持 NotRun 或 EnvironmentUnavailable。

## CI 与公开能力边界

[Windows workflow](../.github/workflows/treedatagrid-native.yml)使用同一脚本并支持 workflow_dispatch，保存日志、XML、JSON、profile 和渲染图。只有实际 GitHub Actions 环境且前置步骤通过时才生成 CiRun 记录及实际 run URL；本地不会产生该证据。本轮未推送仓库或触发远端 CI，实际 job 验收仍开放。

Avalonia 12.0.3 的公开 provider 仍没有原生 Grid/Table pattern；A6 组合测试验证现有 Selection/Value/Expand/Scroll 与逻辑身份入口，不将 DataGrid 控件类型解释为已实现这些 pattern。A3.1 任意不透明原生 CompiledBinding 的自动安全写回继续开放。版本冻结、公共包上传和发布决定另行处理。
