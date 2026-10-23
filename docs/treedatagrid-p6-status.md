# TreeDataGrid P6 执行与验证记录

日期：2026-10-13（香港时间）。本轮可在当前环境完成的 P6-0、P6-A、P6-B 与 P6-C 本地实现、质量验证和 P7 发行准备材料已交付；执行依据为[后续完善计划](treedatagrid-next-development-plan.md)，公共规则见[P6 契约](treedatagrid-p6-contracts.md)。产品版本仍为 `0.2.0-preview.2`，没有发布 RC / 1.0 或上传公共包。真实平台项目、实际 CI 运行器与最终 API / 模板冻结仍有单独门槛。

## 状态与职责

| 范围 | 状态 | 本轮工作 |
| --- | --- | --- |
| C1–C3 | 本地契约与回归已完成 | 可见列、显示索引、冻结边界、隐藏/移动编辑冲突、事务范围和身份规则 |
| C4 | 本轮全流程与最终原生复跑通过 | 最终完整 Verify 与三个本地原生检查均退出 0，保留同批产物和材料 |
| A1 / A2 | 本地实现与独立包验证完成 | 可见列、冻结补位、选区/草稿、网格线四模式、空模板；功能包与可选主题两种独立消费者 |
| A3 / A4 | 已实现并通过自动检查 | 树键盘、真实坐标翻页、列拖动/提示/边缘滚动/取消；物理拖动与真实 IME 待验 |
| P6-B | 本地实现与最终自动回归通过 | 源路径定位、嵌套层级退订/目标守卫、compiled 显示加 typed writer、受控同父/跨父移动 |
| P6-C | 当前环境质量工作完成，真实验收保留 | 自动化 provider、重复测量及 AddRange 对照、最终耐久回归、独立 Windows job；真实读屏、native Grid/Table 与实际 CI runner 仍未完成 |
| P7 | 本地发行材料已交付，平台门槛未满足 | 迁移/支持矩阵/手工步骤与本地双包；Windows 人工项目、干净部署和麒麟待真实环境 |
| E1 | 设计与 11 项私有 prototype 测试通过 | 仅测试辅助；异步 loader、主题/Demo、生命周期集成和真实远端仍未产品化 |

主线程统一公共 API 与核心集成；子代理分别负责主题/测试、只读复核及本文件/契约/TreeDataGrid 专属 Demo。共享 Demo 外壳、solution、Directory.*、脚本与 README 的改动须先重新读取并采用最小补丁。当前虚拟键盘工作线仍在同目录实施，避免同时构建相同输出目录；本记录不覆盖其文件与验证证据。

## 交接历史证据

交接材料记录 Release 0 警告/0 错误，Core 93、Headless 93、Themes 18，共 204 项；原生界面/MVVM/既有回归为 66/29/36 项。`artifacts/platform/unified-demo/` 的旧 JSON/PNG 修改时间为 2026-10-13 10:06–10:10。这些是本轮实施前的证据，不能作为本轮通过结果或固定测试数量门槛。

## C4 环境准备结果

| 字段 | 本轮只读查询结果 |
| --- | --- |
| OS / 进程架构 | `Microsoft Windows 10.0.26200` / X64 |
| CPU / 可用逻辑处理器 | Intel Core i5-14400F / 16（CPU 名取注册表，逻辑处理器由 .NET 查询） |
| SDK / 运行时 | 仓库锁定 SDK 10.0.401；已安装 .NET 10.0.12 win-x64 |
| Avalonia / TFM | 中央包 Avalonia 12.0.3；`net10.0` |
| 中文字体文件 | `msyh.ttc`、`msyhbd.ttc`、`simsun.ttc`、`simhei.ttf` 存在；不能据文件存在声称逐字形回退已验收 |
| GPU / 物理内存 / CIM OS 详情 | 当前受限 shell 的 CIM 查询不可用，待目标环境补采 |
| `dotnet --info` | 返回码 1：Service Control Manager 访问被拒绝；部分 SDK/Host 信息仍可读，完整诊断没有通过 |
| 当前窗口/视口/字体/缩放 | 最近 Demo 为 1440×900 DIP、RenderScaling=1；实际场景见原生 JSON。Headless UI probe 为 640×360、14 DIP，字体记录在测量 JSON，不代表真实字形回退验收 |
| 真实 IME、多 DPI/多显示器、屏幕阅读器、麒麟 | 待真实环境验证 |

环境采集脚本 `Collect-Platform.ps1` 已具备 CIM 容错与 CPU 注册表回退。基线运行时须保留其 JSON 的不可用字段与 `DotnetInfoExitCode`，不能用历史成功信息填补本轮查询缺口。

## 当前验证入口与验收口径

下列入口已在本轮执行。`Verify.ps1` 包括 TreeDataGrid 与 VirtualKeyboard 各三个测试程序集、本地打包及两种控件的功能包/主题消费者；TreeDataGrid 的纯功能包消费者也已加入默认流程。`Test-TreeDataGridNative.ps1` 检查退出码、JSON Passed/Checks/Error、时间戳和图像存在，保留环境与原生窗口能力；Windows CI job 已接入，尚未在实际 CI 运行器执行。

```powershell
./scripts/Collect-Platform.ps1 -OutputPath artifacts/platform/treedatagrid-p6/environment.json
./scripts/Verify.ps1
# 若需要单独复验已打包的 TreeDataGrid，消费者不引用仓库源项目：
./scripts/Test-PackageConsumer.ps1 -NoPack -Control TreeDataGrid
./scripts/Test-TreeDataGridFunctionConsumer.ps1 -NoPack
./scripts/Test-TreeDataGridNative.ps1 -NoBuild -OutputDirectory artifacts/platform/treedatagrid-p6

dotnet samples/NT.AvaloniaControl.Demo/bin/Release/net10.0/NT.AvaloniaControl.Demo.dll --page treedatagrid --demo-smoke --smoke-output artifacts/platform/treedatagrid-p6/demo.json --smoke-image artifacts/platform/treedatagrid-p6/demo.png
dotnet samples/NT.AvaloniaControl.Demo/bin/Release/net10.0/NT.AvaloniaControl.Demo.dll --page treedatagrid --mvvm-smoke --smoke-output artifacts/platform/treedatagrid-p6/mvvm.json --smoke-image artifacts/platform/treedatagrid-p6/mvvm.png
dotnet samples/NT.AvaloniaControl.Demo/bin/Release/net10.0/NT.AvaloniaControl.Demo.dll --page treedatagrid --smoke --smoke-output artifacts/platform/treedatagrid-p6/regression.json --smoke-image artifacts/platform/treedatagrid-p6/regression.png
```

原生验收必须同时满足进程退出码 0、目标 JSON 存在、`Passed=true`、所有 `Checks` 为 true 且 `Error` 为空。缺失结果、旧时间戳、退出非 0 或失败项均不算通过。数量仅随当次实际场景记录；不固定为交接数量。图像是 Avalonia `RenderTargetBitmap` 离屏窗口渲染，不能替代桌面截图、帧率、物理输入或输入法证据。

独立消费者使用 `artifacts/consumer/<唯一目录>`，隔离仓库 `Directory.Build.props` / `Directory.Packages.props`，采用新 `.packages` 缓存与本地 feed，没有源码 ProjectReference。主题消费者覆盖公开主题 URI、五种列、MVVM/compiled XAML、同步加载、暂存写回、选区、排序过滤及 P6 API。功能包消费者只引用功能包与 Avalonia Headless/Fluent，通过应用自定义模板消费 EmptyContent/EmptyContentTemplate，并验证四模式、自定义 CellTheme、显隐零写回、草稿重排、移动、定位及自动化 peer；它检查输出目录、程序集依赖和恢复图中均无 TreeDataGrid 主题包。

## 性能与耐久实际结果

`Measure-TreeDataGrid.ps1` / `--p6` 在同一进程内为每个场景创建五个独立 fixture，固定 Release、种子 1729、视口 1200×700，记录原始分布、分配、GC 与累计进程峰值；这不是五次独立进程测量。[基线](../artifacts/benchmarks/p6-baseline.json)为 10 场景 × 5 次，[后测](../artifacts/benchmarks/p6-after.json)为 12 场景 × 5 次，全部不变量通过。[比较结果](../artifacts/benchmarks/p6-after.comparison.json)中 10 个共同场景均未同时超过 20% 且 5 ms 的耗时调查阈值；两个新增场景标为 NewScenario，不计算不存在的历史比值。

同一事务集合、一万行基底追加 200 项的配对测量：逐条 Add 的中位数为 1210.6002 ms / 1,388,576,920 bytes 累计分配，AddRange 为 1.9491 ms / 7,058,000 bytes，投影通知从 200 次降至 1 次。只对这一受控批导入场景报告收益，原 ObservableCollection 与 DeferRefresh 算法未改，不推导一般增量更新或 UI 帧率。完整分布和解释见[性能说明](treedatagrid-performance-p6.md)。

[Headless UI probe](../artifacts/benchmarks/p6-ui-profile.json)已完成五轮各一百次滚动（10000×100，640×360）。首轮未排空工作队列的内存增长保留为历史诊断；[最终耐久](../artifacts/benchmarks/p6-durability-final.json)执行 20 轮换源/模板/主题/显隐/关闭，排空 RunJobs 与 layout，并在 NoInlining 测试外帧返回后 full-GC。Passed=true，退休模型 20000/保留0、Source 20/0、RowsPresenter 20/0、RowPresenter 156/0、CellPresenter 1384/0、ScrollViewer 20/0。排空后轮次记录的托管内存为 6,253,584–6,877,496 bytes（十进制约6.25–6.88 MB）；这是观测值，不是原生内存预算或无遗漏泄漏证明。真实帧率、输入延迟和原生长期耐久仍待测。

## 本轮实际执行结果

最终完整 `Verify.ps1` 退出 0：锁定还原、Release 构建 0 警告/0 错误，本地打包与四种独立消费者全部通过。[verify-final.log](../artifacts/platform/treedatagrid-p6/verify-final.log)为本轮最终全流程记录；[verify.log](../artifacts/platform/treedatagrid-p6/verify.log)保留首次集成历史，不能据其旧数量代替最新结果。`core.trx` / `headless.trx` / `themes.trx` 已重写为这一轮的 127 / 177 / 24 项、零失败记录。

| 测试程序集 | 通过 | 失败 / 错误 / 跳过 |
| --- | ---: | --- |
| TreeDataGrid Core | 127 | 0 / 0 / 0 |
| TreeDataGrid Headless | 177 | 0 / 0 / 0 |
| TreeDataGrid Themes | 24 | 0 / 0 / 0 |
| VirtualKeyboard Core | 20 | 0 / 0 / 0 |
| VirtualKeyboard Headless | 48 | 0 / 0 / 0 |
| VirtualKeyboard Themes | 11 | 0 / 0 / 0 |

TreeDataGrid 共 328 项，其中 11 项为 E1 测试私有设计原型，不代表产品具有异步加载 API。测试数量是这次结果记录，不是之后的固定验收门槛，也不据此宣称其它控件开发计划全部完成。

最终主题消费者为 [2b8c5015b484480bb2e51cdc35dd127f](../artifacts/consumer/2b8c5015b484480bb2e51cdc35dd127f/Consumer.csproj)；纯功能包消费者为 [treedatagrid-function-da2b731c694941d28f1608eb9ed2191e](../artifacts/consumer/treedatagrid-function-da2b731c694941d28f1608eb9ed2191e/result.json)，18 Checks 全 true、Passed=true、Error 空。两者均为实际新缓存 NuGet 消费，不引用仓库源项目。

独立复核已修正源替换后的移动状态、重复对象的同父/跨父最终 NodeId、回调禁用移动许可、嵌套 binding 目标替换/最后 getter 换源、提交后焦点观察者错误、网格线自然测量缓存，并补齐受控 AddRange 与外帧 GC 的生命周期证据。这些修复已进入上述完整 Verify。

一次实际回归发现：AddRange 完成后 `BeginEditCommand.CanExecute` 已为 true，但已绑定 Button.IsEnabled 仍为 false。新增 Headless 回归先复现失败，再通过受控刷新恢复稳定时重新发送命令通知修复；最后完整 Verify 的 177 项 Headless 包含该回归。批导入后的编辑命令查询与实际绑定按钮状态均已验证同步。

三个原生检查已使用最终 DLL **完成本地最后一轮复跑**：Demo 118 / MVVM 29 / 回归 36 项，进程均退出 0，`Passed=true`、失败项 0、Error 空；JSON/PNG/环境及 `native-capabilities.json` 在 `artifacts/platform/treedatagrid-p6/`，覆盖 P6 显隐/外观、行列移动、定位和嵌套绑定 Demo。新增批导入后按钮同步回归由上述 Headless 测试覆盖，没有据此增加原生场景声明。已有离屏图布局复核；图像不替代物理桌面交互，本地结果也不代表 CI runner 已运行。交付材料的源 / 最终 DLL / 包哈希索引见 [evidence-manifest.json](../artifacts/platform/treedatagrid-p6/evidence-manifest.json)。

清单记录当前152份源码/配置哈希，功能与主题程序集分别在 Build、Demo、对应消费者及包内 DLL 的 SHA256 一致。当前仓库文件尚未跟踪，清单固定的是工作区快照而不是可追溯 Git commit / 基线。基准及耐久 JSON 保留各自较早的测量时间，哈希索引不说明它们已经使用最后命令通知修复后的 DLL 重跑；最终功能测试、原生复跑和消费者结果另按这一轮记录。

受限 shell 锁定还原曾返回 1 且无诊断，随后在正常 .NET/NuGet 缓存权限下执行锁定还原与完整 Verify 成功。没有放宽锁文件、API 历史比较或测试通过条件。

## 待验证事项

- [x] C4 最近完整 Release 构建、测试、双包与功能/主题独立消费者。
- [x] 三个原生检查最近一轮退出码、JSON、失败项与离屏图复核。
- [x] 最终 DLL 的三个原生检查复跑与同批材料确认。
- [x] A1–A4、B1–B4 本地实现、Demo、使用/迁移说明与当次 API 精确快照回归。
- [x] 十场景基线、十二场景后测及十个共同场景比较；新增 AddRange 配对收益。
- [x] Headless 持续滚动、最终20轮耐久与已跟踪旧 Source/模型/视觉对象零保留。
- [x] Windows 原生 CI job 和失败/归档条件接入；尚无实际 runner 运行结果。
- [x] P7 本地迁移、支持矩阵、手工验收步骤和发行准备材料。
- [x] E1 设计与11项私有原型测试；没有扩入产品 API。
- [ ] 实际 Windows CI runner 桌面/窗口与三项结果。
- [ ] 真实中文 IME、字形回退、多 DPI/多显示器、屏幕阅读器、物理拖动、目标麒麟。
- [ ] 原生长期耐久、干净系统部署、产品性能预算与最终 API/模板冻结。
- [ ] 异步加载产品化的受控发布、生命周期、主题/Demo、无障碍及真实远端服务验证。

缺设备时继续其余可完成工作；完整 Windows + 麒麟 1.0 仍待平台证据，不据 Headless、通用 Linux 编译或测试数量宣称完成。
