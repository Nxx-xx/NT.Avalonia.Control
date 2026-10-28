# TreeDataGrid A6 集成与验收记录

2026-10-27 本地预览收尾：当前 0.2.0-preview.2 的 Windows 本地自动验收与交付准备完成。产品、主题、宿主/客户端执行代码保持冻结，默认宿主复用保留；最终 565 项控件测试、11 项收尾工具测试及完整本地流程通过，独立 TreeDataGrid.Usage 发布与原生窗口启动通过。验证入口新增磁盘预检、失败摘要和证据索引；同产品构建的正式 UIA/30 分钟耐久原始证据重新校验后复用。本地候选包、示例、文档与 SHA256/ZIP 校验由交付工具输出。真实读屏/物理交互/DPI/干净部署、实际 CI、麒麟与业务预算继续待验，整体平台 A6 保持开放；当前本地开发进入维护，新增工作另行排期。详见[本地预览收尾](treedatagrid-local-preview-closeout.md)。以下为历史轮次。

2026-10-27 后续：已修复受控 UIA 客户端的接口引用寿命，默认使用显式 COM 作用域释放；产品 DLL 与默认宿主复用保持字节一致。30 分钟 / 899 轮增强活动 UIA 耐久通过，私有内存稳态增量 5.66 MiB；同一诊断宿主的活动堆由旧后端 14 个表格降至 1 个当前活动表格。565 项控件测试、150 项独立校验、7 项所有权自检和完整本地流程通过，四次同状态无客户端复测无调查项。该结论限定本轮受控客户端；旧后端增长和历史失败保留，真实读屏/平台/CI/业务预算仍待验，A6 保持开放。详见[退休寿命修复与验证](treedatagrid-a6-uia-retirement-status.md)。以下为历史轮次。

2026-10-27：活动 UIA 分组归因与三处热点修复已落地，565 项控件测试、135 项独立校验及完整本地流程通过；无客户端 ABBA 的 6000 请求快照/图像一致，默认宿主复用收益保留。修复后正式 UIA 读取/事件批次通过，但 600.95 秒/194 轮增强混合耐久私有内存仍增长约 3.54 GiB，继续失败；活动堆观察到 COM/peer/模板/合成保留链，关闭 provider 后零保留不能覆盖增长。旧版 UIA 性能基线超时，尚不声明活动负载的正式前后收益。详见[本轮归因与验证](treedatagrid-a6-uia-attribution-status.md)。A6 与真实平台/业务验收保持开放，以下为历史轮次。

2026-10-26 本轮验证已执行完毕，整体未通过：560 项控件回归、123 项独立校验测试及当前功能 DLL 的 1800.68 秒/900 轮增强耐久通过，3608454 个跟踪对象回收；同状态四进程未触发 P95 版本回退规则，默认复用收益保留。正式外部 UIA 读取/事件批次通过，但 603.85 秒/139 轮混合耐久私有内存稳态增长约 3.20 GiB，违反原护栏；provider 关闭后零保留不能覆盖此失败。活动 UIA 内存/延迟、持续连接缓存及真实平台/业务预算继续开放，详见[本轮记录](treedatagrid-a6-automation-soak-status.md)。以下为历史轮次。

2026-10-24 自定义工厂长尾修复：自动行高的完整拆挂路径按当前视口预算保留已清理备用行，减少宿主/模板重建。557 项控件回归、90 项校验、4512 条几何快照及本构建 600.56 秒/301 轮原生耐久通过，1204728 个退休对象零保留。正式同状态四进程的自定义工厂 UI P95/分配下降 18.87%/15.31%，渲染完成 P99 下降 19.32%，默认复用收益保留；本次协议内长尾调查收敛，见[本轮记录](treedatagrid-a6-auto-factory-tail-status.md)。历史遥测警告、真实平台/CI/麒麟和业务预算继续单列，整体 A6 未关闭；以下为先前轮次。

2026-10-24 自动行高修复：默认宿主复用已启用，模板测量链缓存与宿主退休边界通过验证。546 项控件回归、68 项原生记录校验、9 项几何校验及 4512 条旧新几何快照通过；增强原生耐久 1800.44 秒/901 轮、3620276 个退休对象最终零保留。首轮宿主自动化缓存失败及性能自动化状态差异均保留，修正后的证据与限制见[本轮记录](treedatagrid-a6-auto-height-status.md)。2 次原始 Any 警告、真实 Windows/CI/麒麟和业务预算继续单列，整体 A6 未关闭；以下是先前轮次。

2026-10-24 导航修复：同源投影刷新复用默认工厂的已清理宿主，535 项控件测试、57 项原生记录校验及增强 30 分钟/901 轮耐久通过。正式四进程对照中导航 UI P95/P99 下降 23.02%/27.30%，分配下降 24.08%，本次有限协议下导航回退调查收敛；默认滚动收益复核与原始 Any 遥测警告见[本轮记录](treedatagrid-a6-navigation-tail-status.md)。整体 A6、物理平台和业务预算保持开放，以下为历史记录。

2026-10-24 后续：已补充托管堆遥测 v2、原始记录/终态交叉校验，以及默认工厂在 `AutoRowHeight=false` 时的滚动宿主复用。最终本地 [regression-6](../artifacts/treedatagrid-alignment/a6-telemetry-scroll-20261024/regression-6/results.json) 输入一致，523 项控件回归及包消费、数据/UI/回收和发布目录检查通过；另有 43 项原生记录校验测试。自动行高候选的原生几何差异被拒绝，该配置沿用原回收路径。新对照、耐久和遥测有效性结论见[本轮记录](treedatagrid-a6-telemetry-scroll-status.md)。以下 513/509 项及旧 DLL 是历史证据；整体 A6 和真实平台项目继续开放。

2026-10-24 后续：首轮 Source 分配优化完成，当前 **513 项控件回归**通过（Core153 / Headless329 / Themes31），独立包和发布目录检查复跑通过；同宿主/主题/依赖的原生对照及 **1800.50 秒/901 轮**耐久完成，4577132 个跟踪退休对象全部回收，私有内存/句柄护栏通过。末次排空后托管堆 API 读数无效，已保留原值并排除该指标结论；滚动长尾与其余平台验收仍开放。见[首轮原生优化记录](treedatagrid-native-optimization-a6.md)。下文 509 项和旧 DLL/时间属于初轮 A6 历史证据。

日期：2026-10-23（香港时间）。本轮推进 A6 的本机自动集成、无障碍组合、性能/生命周期、独立包、发布目录运行与 CI 接入。A6 的真实 Windows 交互、实际 CI runner 和目标麒麟尚待执行，因此 A6 总项保持开放。

后续原生补测已完成：6 个原生场景、30 轮/1800 次正式测量（另 900 次预热），独立进程持续 600.50 秒/301 轮原生耐久，1529048 个已跟踪退休对象全部回收，私有内存与句柄趋势未触发调查阈值。17 项原生记录校验测试通过。同步 UI 与渲染线程完成时间、35.05 GiB 累计分配/GC 负担及限制见[Windows 原生补测](treedatagrid-native-performance-a6.md)。以下保留初轮 A6 本地自动验收的时间和结果。

版本保持 0.2.0-preview.2 / Avalonia 12.0.3 / net10.0 / SDK 10.0.402，不增加产品依赖，不修改当前/历史公开 API，不发布公共包。A3.1 通用原生不透明 CompiledBinding 的自动安全写回继续开放。平台步骤与复跑见[验收说明](treedatagrid-platform-acceptance.md)。

## 本轮交付

| 范围 | 结果 |
| --- | --- |
| 组合无障碍测试 | 6 项：取消 provider 导航保留草稿；错误/逻辑草稿经换工厂/模板保留与一次恢复写回；同父批量后旧 peer 的显示位置/选择；跨父后代旧 peer 失效与新身份恢复；列头显隐/重排/只读/禁用；离屏目标经筛选/显隐/工厂更换后的身份 |
| 滚动与生命周期 | 2 项：三组配置各 5 样本的自动行高双轴滚动；工厂/事件/绑定/批量/换源模板组合的退休对象回收。普通回归使用小规模样本，独立扩展运行输出 10000 行 × 100 列及 40 轮结果 |
| 包消费 | 两个现有脚本新增可选 DependencyFeed，仅允许依赖包；本轮产品包从当前构建产生。新缓存独立消费及还原方式、包哈希有记录 |
| 本机发布运行 | 原生检查支持 ApplicationPath，记录实际输入 DLL 的路径和哈希，可验证独立发布目录，而不是始终使用项目 bin |
| 验收矩阵 | 固定 local / windows / windows-kylin 三种范围；缺少人工/CI 证据时保持未完成，不能以程序化结果替代 |
| CI | Windows job 使用同一 A6 脚本、配置 Python 3.12、支持 workflow_dispatch 并归档结果；只有实际 Actions 环境才生成 CiRun 和实际 URL，本地不伪造远端结果 |

新增 **8 项控件测试**，合计 **509 项**（Core149 / Headless329 / Themes31）。验收记录校验器另有 **8 项 Python 测试**，验证未完成范围、缺项、文件/哈希、错误证明类型等，不混入控件数量。扩展生命周期的 2 项复跑也不重复计数。

## 最终运行

当次目录：`artifacts/treedatagrid-alignment/a6-completion-20261023/run-3/`。香港时间 **22:22:12–22:24:40**，[results.json](../artifacts/treedatagrid-alignment/a6-completion-20261023/run-3/results.json) passed=true。[自动证据](../artifacts/treedatagrid-alignment/a6-completion-20261023/run-3/automated-evidence.json)覆盖 236 份输入（223 份范围源码/配置/锁文件及 13 个依赖 nupkg），运行前后 SHA256 一致。入口：[Test-TreeDataGridAlignment.ps1](../scripts/Test-TreeDataGridAlignment.ps1)。

| 检查 | 证据 |
| --- | --- |
| Core149 / Headless329 / Themes31 | [Core XML](../artifacts/treedatagrid-alignment/a6-completion-20261023/run-3/TreeDataGrid.Core.Tests.xml)、[Headless XML](../artifacts/treedatagrid-alignment/a6-completion-20261023/run-3/TreeDataGrid.Headless.Tests.xml)、[Themes XML](../artifacts/treedatagrid-alignment/a6-completion-20261023/run-3/TreeDataGrid.Themes.Tests.xml)，失败/错误/跳过/未运行均为 0 |
| 验收记录校验器 8 项 | [日志](../artifacts/treedatagrid-alignment/a6-completion-20261023/run-3/acceptance-validator-tests.log) |
| 数据/几何 12×5 样本 | [data-profile.json](../artifacts/treedatagrid-alignment/a6-completion-20261023/run-3/data-profile.json)，60 个样本不变量通过 |
| UI 3×5 样本及 40 轮回收 | [UI profile](../artifacts/treedatagrid-alignment/a6-completion-20261023/run-3/profiles/ui-profile.json)、[durability.json](../artifacts/treedatagrid-alignment/a6-completion-20261023/run-3/profiles/durability.json)、[扩展复跑 XML](../artifacts/treedatagrid-alignment/a6-completion-20261023/run-3/expanded-lifecycle.xml) |
| 纯功能包 50 项 | [result.json](../artifacts/consumer/treedatagrid-function-3571dadbc6a44653b25c0c4c1482409c/result.json)、[还原方式/包哈希](../artifacts/consumer/treedatagrid-function-3571dadbc6a44653b25c0c4c1482409c/package-consumption.json) |
| 主题包独立消费 | [日志](../artifacts/treedatagrid-alignment/a6-completion-20261023/run-3/theme-package-consumer.log)、[还原方式/包哈希](../artifacts/consumer/282c6fd25b554915b048d1780dc7cdad/package-consumption.json) |
| 拒绝依赖源混入旧产品包 | [两个脚本负向验证](../artifacts/treedatagrid-alignment/a6-completion-20261023/consumer-feed-negative-checks.json)，均在打包/创建消费者之前返回诊断 |
| 发布目录原生程序化检查 | [实际输入 DLL](../artifacts/treedatagrid-alignment/a6-completion-20261023/run-3/native/application-input.json)、[Demo161](../artifacts/treedatagrid-alignment/a6-completion-20261023/run-3/native/demo.json)、[MVVM29](../artifacts/treedatagrid-alignment/a6-completion-20261023/run-3/native/mvvm.json)、[回归36](../artifacts/treedatagrid-alignment/a6-completion-20261023/run-3/native/regression.json) |
| 图库 TreeDataGrid 场景导航 | [XML](../artifacts/treedatagrid-alignment/a6-completion-20261023/run-3/tree-gallery.xml)，1 项通过 |
| 程序集一致性 | [binary-hashes.json](../artifacts/treedatagrid-alignment/a6-completion-20261023/run-3/binary-hashes.json)，功能 DLL 12 份、主题 DLL 8 份、Demo DLL 3 份分别一致，包含测试/示例/包/消费者及发布目录 |

锁定还原、Release 构建、MSBuild Publish 均通过，构建 0 警告/0 错误。两个消费者本次均为显式离线依赖 feed、新建空缓存；当前产品包不会从依赖 feed 读取，不代表在线还原或干净操作系统已验收。已查看[发布目录工厂场景图像](../artifacts/treedatagrid-alignment/a6-completion-20261023/run-3/native/demo-a5-element-factory.png)，计数、操作、模板和滚动表格完整，图像仍为离屏渲染。

功能 DLL SHA256 与 A5 一致：`7A5CDD48C718C40A3E7123E23E8FDF62E479A50B684FBF46FBC6B15D2D9C24C3`。当前 API 文件仍为 `DAF8E041FE01F557450F4DE1D393840B1C87B890E0CE3F4173D1C70AD8AFE53B`，三份历史快照未修改。本轮增加验证和工具，没有改动功能行为来适配测试。

## 性能与保留对象

数据/几何沿用既有 P6 的 12 个场景，各重复 5 次：10 万行、100 列混合行高、2 万次几何命中、10 万子节点宽树、10 万节点/100 层深树，以及普通通知、DeferRefresh、事务集合 Add/AddRange 配对。结果中的通过表示不变量成立，不能解释为原生帧率或已决定产品预算。

新增 UI 测量在 640×360 DIP 下使用 10000 行 × 100 列、冻结 1 列、换行自动行高。三组为 reflection-default、controlled-events-default、controlled-events-factory。每组 5 个新表格，各 100 次预热后测 80 次滚动/布局。样例工厂额外管理每个实现单元格的模型订阅，测试自身包含寿命断言，组间差值不是框架净开销；初轮仍可能受 JIT/缓存影响。

| 配置 | 每 80 次布局中位耗时 | UI 线程中位分配 | 峰值保留行 / 实现单元格 |
| --- | ---: | ---: | ---: |
| reflection-default | 464.4571 ms | 226526248 B | 14 / 84 |
| controlled-events-default | 426.7755 ms | 223151872 B | 14 / 84 |
| controlled-events-factory | 441.3999 ms | 225497240 B | 14 / 84 |

40 轮耐久包含 Source/模型替换、工厂准备与退订、A2 成对事件、受控编译绑定、每轮一次提交、同父原子批量、主题/模板/列头切换及双轴滚动；取消零写回由每个 UI 样本覆盖。完整 GC 在不内联操作栈返回并排空 dispatcher/layout/headless render 之后执行。只对已跟踪退休对象作出结论，不把强制 GC 后的零保留解释为长时间原生内存预算。

最终跟踪 39800 个退休对象全部零保留：context22240、模型12000、单元格4480、行640、列头280，以及 Source/工厂/presenter/逻辑 peer 各40。每轮一次 EditCommit，模型零残余订阅，A2 准备/清理成对。原始数据保留每 5 轮的内存和计数趋势。

## 环境待验与发行状态

本轮[本地自动结果](../artifacts/treedatagrid-alignment/a6-completion-20261023/run-3/acceptance-local.json) passed=true，pending/errors 均为空；[Windows 发行范围](../artifacts/treedatagrid-alignment/a6-completion-20261023/run-3/acceptance-windows.json)仍有 8 类 pending；[Windows＋麒麟范围](../artifacts/treedatagrid-alignment/a6-completion-20261023/run-3/acceptance-windows-kylin.json)有 9 类，均 errors=[]，校验进程按契约退出 2 表示未完成。完整状态见[当次矩阵](../artifacts/treedatagrid-alignment/a6-completion-20261023/run-3/platform-matrix.json)。

正式 Windows 范围尚缺真实 IME、物理拖动、系统剪贴板/焦点、字体/多 DPI/多显示器、读屏、干净系统部署/在线还原和实际 CI。原生批次渲染与十分钟耐久已由[后续补测](treedatagrid-native-performance-a6.md)覆盖，屏幕呈现/物理输入延迟、数小时负载与业务预算仍待验；[更新矩阵](../artifacts/treedatagrid-alignment/a6-native-20261023/final-1/platform-matrix.json)记录部分证据而不关闭该发行项目。完整范围再缺目标麒麟桌面矩阵；目前未提供对应环境。

当前 Avalonia 公开 API 仍未接入原生 Grid/Table pattern，专用逻辑 provider 的通过不补足该能力。真实平台资料须附设备/版本、时间、预期/实际及桌面图像/录像，再更新矩阵；脚本校验只保证结构和哈希一致，不能证明人工记录内容属实。

## 开发中的诊断

首次组合测试有 3 项预期错误：多选夹具未显式配置 Multiple；逻辑 IValueProvider 在活动编辑中按既有契约返回草稿而非模型值。修正夹具与预期后 8 项通过，没有修改产品行为。验收校验器最初使用系统临时目录被沙箱写权限阻止，改为仓库 artifacts 下临时目录并验证清理路径后 8 项通过。

首轮完整运行完成 509 项测试后，基准还原的 NuGet 漏洞数据请求被网络权限阻止；显式离线依赖源解决了该轮还原范围问题，不关闭告警、不更改锁文件。第二轮的 60 个数据样本、15 个 UI 样本、40 轮回收和独立包均通过，dotnet publish CLI 退出 1 且未给出诊断，原始失败保留。通过 SDK 的 MSBuild Publish 目标完成相同的无重建发布后，以该方式复跑最终流程。发布仍依赖本机 .NET 运行时，不等于干净机器部署。

其它控件存在并行改动，未执行全仓库 Verify.ps1，也未提交/推送或触发远端工作流。图库测试使用当次已构建的 Demo，--no-dependencies 避免重建其它模块。
