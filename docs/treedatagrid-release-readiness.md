# TreeDataGrid 本地发行准备与平台验收矩阵

2026-10-27 本地预览收尾：当前 0.2.0-preview.2 的 Windows 本地自动验收与交付准备完成。产品、主题、宿主/客户端执行代码保持冻结，默认宿主复用保留；最终 565 项控件测试、11 项收尾工具测试及完整本地流程通过，独立 TreeDataGrid.Usage 发布与原生窗口启动通过。验证入口新增磁盘预检、失败摘要和证据索引；同产品构建的正式 UIA/30 分钟耐久原始证据重新校验后复用。本地候选包、示例、文档与 SHA256/ZIP 校验由交付工具输出。真实读屏/物理交互/DPI/干净部署、实际 CI、麒麟与业务预算继续待验，整体平台 A6 保持开放；当前本地开发进入维护，新增工作另行排期。详见[本地预览收尾](treedatagrid-local-preview-closeout.md)。以下为历史轮次。

2026-10-27 后续：已修复受控 UIA 客户端的接口引用寿命，默认使用显式 COM 作用域释放；产品 DLL 与默认宿主复用保持字节一致。30 分钟 / 899 轮增强活动 UIA 耐久通过，私有内存稳态增量 5.66 MiB；同一诊断宿主的活动堆由旧后端 14 个表格降至 1 个当前活动表格。565 项控件测试、150 项独立校验、7 项所有权自检和完整本地流程通过，四次同状态无客户端复测无调查项。该结论限定本轮受控客户端；旧后端增长和历史失败保留，真实读屏/平台/CI/业务预算仍待验，A6 保持开放。详见[退休寿命修复与验证](treedatagrid-a6-uia-retirement-status.md)。以下为历史轮次。

2026-10-27：活动 UIA 分组归因与三处热点修复已落地，565 项控件测试、135 项独立校验及完整本地流程通过；无客户端 ABBA 的 6000 请求快照/图像一致，默认宿主复用收益保留。修复后正式 UIA 读取/事件批次通过，但 600.95 秒/194 轮增强混合耐久私有内存仍增长约 3.54 GiB，继续失败；活动堆观察到 COM/peer/模板/合成保留链，关闭 provider 后零保留不能覆盖增长。旧版 UIA 性能基线超时，尚不声明活动负载的正式前后收益。详见[本轮归因与验证](treedatagrid-a6-uia-attribution-status.md)。A6 与真实平台/业务验收保持开放，以下为历史轮次。

2026-10-26 本轮验证已执行完毕，整体未通过：560 项控件回归、123 项独立校验测试及当前功能 DLL 的 1800.68 秒/900 轮增强耐久通过，3608454 个跟踪对象回收；同状态四进程未触发 P95 版本回退规则，默认复用收益保留。正式外部 UIA 读取/事件批次通过，但 603.85 秒/139 轮混合耐久私有内存稳态增长约 3.20 GiB，违反原护栏；provider 关闭后零保留不能覆盖此失败。活动 UIA 内存/延迟、持续连接缓存及真实平台/业务预算继续开放，详见[本轮记录](treedatagrid-a6-automation-soak-status.md)。以下为历史轮次。

2026-10-24 自定义工厂长尾修复：自动行高的完整拆挂路径按当前视口预算保留已清理备用行，减少宿主/模板重建。557 项控件回归、90 项校验、4512 条几何快照及本构建 600.56 秒/301 轮原生耐久通过，1204728 个退休对象零保留。正式同状态四进程的自定义工厂 UI P95/分配下降 18.87%/15.31%，渲染完成 P99 下降 19.32%，默认复用收益保留；本次协议内长尾调查收敛，见[本轮记录](treedatagrid-a6-auto-factory-tail-status.md)。历史遥测警告、真实平台/CI/麒麟和业务预算继续单列，整体 A6 未关闭；以下为先前轮次。

2026-10-24 自动行高修复：默认宿主复用已启用，模板测量链缓存与宿主退休边界通过验证。546 项控件回归、68 项原生记录校验、9 项几何校验及 4512 条旧新几何快照通过；增强原生耐久 1800.44 秒/901 轮、3620276 个退休对象最终零保留。首轮宿主自动化缓存失败及性能自动化状态差异均保留，修正后的证据与限制见[本轮记录](treedatagrid-a6-auto-height-status.md)。2 次原始 Any 警告、真实 Windows/CI/麒麟和业务预算继续单列，整体 A6 未关闭；以下是先前轮次。

2026-10-24 导航修复：535 项控件回归、57 项原生记录测试及增强 30 分钟耐久通过，导航 P95/P99/分配下降 23.02%/27.30%/24.08%，本次协议下无场景触发 UI P95 调查阈值。默认滚动、生命周期与独立遥测口径见[本轮记录](treedatagrid-a6-navigation-tail-status.md)。这些有限证据仍不构成全平台/物理呈现、业务预算或正式发行验收；原始 Any 不一致警告没有被改成通过。

2026-10-24 后续：当前 523 项控件回归、双包消费和发布目录检查通过。托管堆遥测已分开处理“可选当前估计不可用”和“必需 last-GC/代汇总交叉指标错误”，滚动复用限定为默认工厂且未启用自动行高。原生同协议/反向对照、几何一致性与 30 分钟耐久通过，两个默认滚动场景取得收益；自定义工厂/导航编辑的混合负载 P95 回退仍需调查，未通过全场景性能或发行验收。详情见[本轮记录](treedatagrid-a6-telemetry-scroll-status.md)。物理呈现/输入延迟、业务预算、真实平台和发行门槛继续开放；下面数量保留其原始时间范围。

2026-10-24：当前 Source 优化后的 513 项控件测试及完整 A6 本地检查已通过；新增同程序集宿主/主题的原生对照和 1800.50 秒/901 轮耐久，退休弱对象零保留、私有内存/句柄护栏通过。末次托管堆遥测负值不作有效证据，原生长尾和真实平台/预算验收继续开放。见[首轮原生优化](treedatagrid-native-optimization-a6.md)；下文 509 项/十分钟等为先前材料，不代表本轮已经完成发行或全部平台验收。

2026-10-23 原生性能补测：同一功能 DLL 已完成 6 场景/30 轮、1800 次正式原生渲染完成观测及 600.50 秒/301 轮原生耐久，1529048 个退休对象全部回收、私有内存/句柄趋势未超调查阈值。实际同步 UI/渲染耗时、累计分配/GC 和部分验收证据见[原生记录](treedatagrid-native-performance-a6.md)。该有限测量不关闭屏幕呈现/物理输入、更长耐久和业务预算项目。

2026-10-23 更新：A6 本地自动范围通过，最新为 509 项控件测试、两个离线隔离包消费者、数据/UI/40 轮回收、发布目录原生 161/29/36 项和图库；见 [A6 记录](treedatagrid-alignment-a6-status.md)。[三范围验收说明](treedatagrid-platform-acceptance.md)及当次矩阵将本地通过与 Windows 8 类、完整平台 9 类待验分开；A6 与发行验收保持开放。以下 P6 的 328/118 等数量是历史材料，不代表当前结果。

更新日期：2026-10-13（香港时间）。当前交付为可审阅的本地实现、包、测试与验收材料，版本仍为 `0.2.0-preview.2`。本文不声明 `0.9.0-rc`、`1.0.0`、最终 API / 模板冻结或公共包发布完成。实施状态由[阶段记录](treedatagrid-p6-status.md)维护，迁移事项见[P6 迁移说明](treedatagrid-p6-migration.md)。

## 当前支持证据与缺口

| 范围 | 当前材料 / 状态 | 尚须完成 |
| --- | --- | --- |
| 本地 Windows x64 | Windows `10.0.26200`、.NET 10、Avalonia 12.0.3；最终 DLL 原生复跑118 / 29 / 36项通过，窗口能力可用 | 其它拟支持 Windows 版本、真实系统交互与干净部署环境分别验证 |
| Source / ItemsSource、编辑与 P6 交互 | 最终 Release 0警告/0错误，Core127 / Headless177 / Themes24共328项通过；功能/主题独立消费者通过 | 11项为私有E1设计原型；测试数量不作固定门槛，继续改实现须重新验证 |
| Windows 中文 IME | 程序化中文 TextBox round-trip 不属于真实 IME 验收 | 真实预编辑、候选窗口、选词、提交 / 取消与回收组合待验证 |
| 物理鼠标拖动与焦点 / 剪贴板 | 已有程序化输入与路由回归 | 实际列头 / 行号拖动、捕获丢失、系统焦点及剪贴板交互待验证 |
| 中文字体与多 DPI / 多显示器 | 环境记录列出本机中文字体文件；离屏渲染材料可检查布局 | 实际字形回退、100% / 125% / 150% / 200% 及跨显示器切换待验证 |
| 屏幕阅读器与平台自动化桥接 | 专用 peer、公开 provider 及 Headless 自动化查询 / 定位回归已接入 | 真实读屏软件、公告、导航与平台桥接待验证；Avalonia 当前未公开原生 Grid / Table provider 接口 |
| Windows 原生 CI | 已新增独立 [Windows job](../.github/workflows/treedatagrid-native.yml) 和运行脚本 | 尚未用实际 CI runner 证明可用桌面、原生窗口和三项结果；本地成功不能替代该证据 |
| 目标麒麟 | 当前未取得对应发行版、版本、架构和桌面环境，保持未验证 | 补齐环境清单后执行相同功能、手工输入、无障碍、部署与性能矩阵 |
| 通用 Linux 构建 | 常规 CI 配置有 `ubuntu-latest` job | 配置存在或通用 Linux 编译不构成麒麟桌面支持证据 |
| 性能与耐久 | 基线10×5、后测12×5、10个共同场景比较和AddRange配对完成；20轮最终已跟踪旧对象零保留 | 材料保留各自测量时间，非最后通知修复DLL的重新benchmark；产品预算、原生帧率/延迟与长期趋势待实际测量 |
| 安装与独立包消费 | 两个本地 NuGet 包和无源码引用的独立消费者材料 | 实际发布产物的干净系统启动、部署方式和缺依赖诊断待验证；公共包上传另行处理 |

目标麒麟未验证时，原定 Windows + 麒麟的完整 1.0 验收不能勾选。若以后调整发布范围，应先在支持矩阵和发布决定中明确处理。

## 本地材料入口与复核规则

本轮最终完整 Verify、三个原生复跑和四种独立消费者已经通过，当前统计与材料见下表及[阶段记录](treedatagrid-p6-status.md)。性能 / 耐久材料保留各自较早的测量时间；最终 DLL 的功能验收与这些测量不是同一个时间范围。

| 材料 | 路径 | 当前可证明的范围 |
| --- | --- | --- |
| 构建 / 测试 / 打包 / 消费日志 | [verify-final.log](../artifacts/platform/treedatagrid-p6/verify-final.log) | 最终退出0、Release0警告/0错误、全部测试及四种消费者通过；verify.log保留较早历史 |
| 源 / 程序集 / 包 / 材料哈希 | [evidence-manifest.json](../artifacts/platform/treedatagrid-p6/evidence-manifest.json) | 152份源码/配置快照；各功能/主题 Build、Demo、对应消费者与包内DLL哈希一致。未跟踪仓库不能据此宣称Git基线 |
| 主题包独立消费者 | [Consumer.csproj](../artifacts/consumer/2b8c5015b484480bb2e51cdc35dd127f/Consumer.csproj) | 新缓存、PackageReference、无源码引用；公开主题URI及P6接入通过 |
| 纯功能包独立消费者 | [result.json](../artifacts/consumer/treedatagrid-function-da2b731c694941d28f1608eb9ed2191e/result.json) | 18项Checks全true、Passed=true、Error空；无TreeDataGrid主题包依赖，自定义模板实际消费外观入口 |
| 本机环境 | [environment.json](../artifacts/platform/treedatagrid-p6/environment.json) | 已采字段与查询失败字段；CIM / GPU 等不可用信息不能凭猜测填充 |
| 原生窗口能力 | [native-capabilities.json](../artifacts/platform/treedatagrid-p6/native-capabilities.json) | 本地 NativeWindowAvailable，不说明云 CI runner 已具备同样能力 |
| 统一 Demo 界面检查 | [demo.json](../artifacts/platform/treedatagrid-p6/demo.json)、[demo.png](../artifacts/platform/treedatagrid-p6/demo.png) | 最终DLL118项程序化检查通过和离屏布局复核；非物理交互 |
| MVVM 检查 | [mvvm.json](../artifacts/platform/treedatagrid-p6/mvvm.json)、[mvvm.png](../artifacts/platform/treedatagrid-p6/mvvm.png) | 最终DLL29项脚本化MVVM检查通过 |
| 既有行为回归 | [regression.json](../artifacts/platform/treedatagrid-p6/regression.json)、[regression.png](../artifacts/platform/treedatagrid-p6/regression.png) | 最终DLL36项脚本化回归检查通过 |
| 数据与几何测量 | [p6-baseline.json](../artifacts/benchmarks/p6-baseline.json)、[p6-after.json](../artifacts/benchmarks/p6-after.json)、[comparison](../artifacts/benchmarks/p6-after.comparison.json) | 固定配置重复样本、不变量与AddRange配对；10个共同场景无调查阈值超出，不证明产品预算 |
| Headless UI 测量 | [p6-ui-profile.json](../artifacts/benchmarks/p6-ui-profile.json) | 持续程序化滚动、自动行高与布局；不证明原生帧率 |
| Headless 最终耐久 | [p6-durability-final.json](../artifacts/benchmarks/p6-durability-final.json) | RunJobs/layout排空及NoInlining外帧full-GC后，已跟踪退休模型/源/视觉对象零保留；非原生长期内存预算 |
| API / 迁移 | [public-api.txt](public-api.txt)、[迁移说明](treedatagrid-p6-migration.md) | 本轮精确成员与行为迁移，不作最终冻结声明 |
| 本地包 | [功能包](../artifacts/packages/NT.AvaloniaControl.TreeDataGrid.0.2.0-preview.2.nupkg)、[主题包](../artifacts/packages/NT.AvaloniaControl.TreeDataGrid.Themes.0.2.0-preview.2.nupkg) | 当前本地包，不代表已上传公共源 |

主题消费者由 `scripts/Test-PackageConsumer.ps1`、纯功能包消费者由 `scripts/Test-TreeDataGridFunctionConsumer.ps1` 每次生成 `artifacts/consumer/<唯一目录>`，使用新缓存、本地 feed 和 PackageReference，隔离仓库构建设置。两个TreeDataGrid消费者及VirtualKeyboard功能/主题消费者都通过最终Verify；不能用历史 `TreeDataGrid.Usage` 输出代替最新包消费结果。新增AddRange后绑定按钮通知回归由177项Headless中的针对场景证明，不能把它计作新增原生检查。

三个原生检查须同时满足进程退出码 0、当次 JSON 存在且 TimestampUtc 新于开始运行、`Passed=true`、Checks 非空且全部为 true、Error 为空及图像文件存在。当前 JSON 的 ImageCaptureKind 明确为 Avalonia RenderTargetBitmap 离屏渲染，不能作为桌面截图、真实输入、IME 或多 DPI 证据。

重新运行时由统一构建负责人安排输出目录，避免与同目录 VirtualKeyboard 工作线争用产物：

```powershell
./scripts/Verify.ps1
./scripts/Test-TreeDataGridNative.ps1 -NoBuild -OutputDirectory artifacts/platform/treedatagrid-p6
```

如果原生环境不可用、退出码非 0、结果缺失或有失败项，应保留诊断并修复；不能将该次运行标为通过。机器采集脚本里失败的字段继续如实保留。性能方法和限制见[测量说明](treedatagrid-performance-p6.md)。

## Windows 待验证的手工步骤

每次记录应用构建 / 包版本、运行时间、OS 和进程架构、.NET、Avalonia、主题、视口、RenderScaling、字体、输入法及读屏软件版本；保存实际桌面截图或录像及预期 / 实际结果。手工记录建议置于 `artifacts/platform/treedatagrid-manual/windows/<日期与配置>/`，该路径是后续归档位置，当前不代表已有对应证据。

### 真实中文输入法

1. 在统一 Demo 的可编辑单元格中启用实际中文输入法，输入拼音并保持预编辑，观察候选窗口位置与编辑器锚点。
2. 使用方向键、候选翻页和数字选词；确认树导航、网格翻页和编辑提交不会抢走选词输入。Enter 完成选词时核对仍是原单元格、原模型目标。
3. 取消预编辑后分别验证 Enter / Tab 的正常提交与导航、Esc 的草稿取消；使用记录 writer 调用的场景核对取消路径零写回。
4. 在预编辑 / 候选阶段尝试鼠标切换单元格、纵横滚动回收、换肤及模板切换；确认没有错行写入、候选窗口失位或重复提交。
5. 制造转换或校验错误，再输入中文、选词和修复；确认错误提示、焦点与提交策略一致。

上述步骤需由实际输入法完成。直接调用 TextBox.Text、程序化 Chinese TextInput 或离屏图均不能代替候选窗口与系统预编辑验证。

### 物理拖动、键盘和系统交互

1. 实际拖动列头超过阈值，检查插入标记；分别在冻结区、滚动区内完成重排，跨边界拒绝。与排序点击及调宽 Thumb 分别核对，拖动不能附带触发它们。
2. 配合隐藏列、横向边缘滚动和活动草稿，确认 ColumnId、宽度及编辑会话跟随原列；Esc、捕获丢失、模板 / Source 替换取消在途拖动。
3. 从行号列实际拖动同父节点、跨父事务节点，覆盖 Before / After / Inside、悬停展开与自动滚动。拒绝自身 / 后代、只读或不支持的默认集合、排序 / 过滤默认移动及校验失败。
4. 查看重复模型不同出现位置的实际结果身份；提交后观察者异常不得导致用户按未提交失败重复移动。
5. 使用真实系统剪贴板完成单元格编辑器内复制 / 粘贴、快捷键与窗口切换；核对编辑器和模板内控件自己的输入路由。此项不意味着新增“复制网格选区”增强。

### 中文字体、DPI 与多显示器

1. 使用业务需要的中文、标点、长文本和生僻字检查实际字形与回退，记录生效字体；文件存在不等于每个目标字形已验证。
2. 在 100% / 125% / 150% / 200% 实际系统缩放下复验列头、冻结边界、网格线、自动行高、空内容、编辑器、验证提示和拖动命中。
3. 将窗口在不同 DPI 显示器之间移动，包含预编辑、拖动和编辑器活动状态；记录布局、候选窗口、指针落点与实际 RenderScaling。

### 屏幕阅读器

1. 使用本机实际可用读屏软件记录版本，读取表格名称、列头、显示顺序、行 / 单元格值、选择、只读及当前焦点。
2. 展开和折叠树节点，核对深度与状态；滚动到新实现行后复验位置及读屏导航。
3. 编辑并制造验证错误，确认编辑器自动化子树可用、错误能读出且可返回修复；换肤、换源和回收后不指向另一业务目标。
4. 记录平台自动化桥接的实际结果。当前 Avalonia 12.0.3 未公开 IGridProvider / ITableProvider，DataGrid 控件类型和公开逻辑定位不能被当作原生 Grid / Table pattern 已验收。

具体公开能力和限制见[无障碍说明](treedatagrid-accessibility.md)。

## 麒麟环境和实际 CI 运行器准备

取得目标麒麟环境前须记录：发行版与完整版本、CPU 架构、内核、.NET SDK / Runtime、Avalonia 依赖、桌面会话与显示服务、中文字体、输入法及候选系统、渲染后端 / 图形依赖、读屏软件、显示缩放及部署方式。不能预设麒麟等同于现有 Ubuntu CI 环境，也不能未经目标版本确认就承诺依赖组合可用。

目标设备可用后，先运行干净应用与包消费，再执行源 / 选择 / 展开 / 编辑 / 排序过滤 / 冻结 / 显隐 / 行列移动、原生检查、真实输入法 / DPI / 无障碍和性能耐久矩阵。归档建议为 `artifacts/platform/treedatagrid-manual/kylin/<发行版-版本-架构>/`；目前该环境及结果仍为待提供 / 未验证。

CI 的 [treedatagrid-native.yml](../.github/workflows/treedatagrid-native.yml) 已接入独立 Windows job，运行 `Test-TreeDataGridNative.ps1` 并归档 JSON / PNG / 环境。首次实际 runner 运行须留存 job 链接、运行器环境、NativeWindowCreated 与三项结果；若桌面不可用明确失败并保留 capability JSON。作业配置存在不等于运行器已通过，本地 UserInteractive / 窗口创建也不能推断远端环境。

## 已完成的本地准备与正式发布待办

- [x] 本轮最终实现完整构建、328项TreeDataGrid测试、双包与四种独立消费者，失败项完成修复。
- [x] 最终DLL的118 / 29 / 36项原生程序化复跑、JSON/图像复核与程序集/包哈希一致性。
- [x] AddRange成对测量、共同场景比较、Headless滚动及外帧GC的20轮耐久。
- [ ] Windows 真实 IME、物理系统交互、字形 / 多 DPI / 多显示器和屏幕阅读器记录。
- [ ] 实际 Windows CI runner 的原生窗口与三项检查记录。
- [ ] 明确目标麒麟配置并完成相应矩阵，或先正式调整拟发布平台范围。
- [ ] 干净系统安装 / 启动、实际部署、缺依赖诊断及长期原生耐久。
- [ ] 按已测数据决定性能预算；优化收益有同机同配置对照，不能由测试数量或单次耗时推定。
- [ ] 候选版本时复核并最终冻结公开 API、默认值、身份 / 编辑 / 移动语义和必要模板部件，完成迁移与发行说明。
- [ ] 实际产品版本调整和公共包上传按单独发布任务执行。

E1 目前只有[异步加载设计与测试私有原型](treedatagrid-async-loading-design.md)，没有新增产品 loader / coordinator API、默认异步展开或远端服务交付。受控原型结果不改变当前同步加载契约，也不自动扩大 P6 / P7 的产品发布门槛。
