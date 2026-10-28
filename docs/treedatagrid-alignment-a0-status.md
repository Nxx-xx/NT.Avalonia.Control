# TreeDataGrid A0 执行与验证记录

日期：2026-10-23（香港时间）。状态：A0 六项交付完成，可进入 A1。新增产品功能仍按各自阶段实施，不能把 A0 契约当作已经实现的 API。

后续进展：A1、A2、A4 和 A5 本地范围已完成；A6 本地自动验收通过，当前 509 项控件测试及扩展测量/独立包/发布目录原生检查见 [A6 记录](treedatagrid-alignment-a6-status.md)。A6 真实平台与 A3 通用编译绑定扩展继续开放。下文保留 A0 当时的基线与下一步记录。

## 本轮完成内容

- 逐项核实当前源码，形成 D01–D16 差异矩阵，区分能力缺失、API 形态差异、范围限制和平台待验。
- 固定 A1 属性的类型/默认值、展开器包装列与内列优先级、运行时变更和旧全局配置的继承规则。
- 固定选择取消、编辑提交、单元格生命周期、值变化和拖放事件的执行顺序、身份检查、异常与重复通知处理。
- 记录当前 Release 构建、328 项 TreeDataGrid 测试、双包打包和两类独立消费者的当次结果。
- 确认当前精确 API 快照及 P1/P2-P3/P4-P5 历史兼容检查通过；产品源码与 API 快照没有改动。
- 建立平台待验清单及后续测试落点，明确 A1 首批开发任务。

具体契约见 [A0 差异矩阵与兼容契约](treedatagrid-alignment-a0-contracts.md)，执行范围见[功能补齐计划](treedatagrid-feature-alignment-plan.md)。Fluent 构建 API 继续排除。

## 当次自动验证

成功运行时间：2026-10-23 16:40:31–16:41:21（香港时间）。以下结果直接读取本次 XML/JSON，不复用历史测试数量。

| 检查 | 结果 | 当次证据 |
| --- | --- | --- |
| 三个测试项目锁定还原 | 通过 | 验证目录内 *-restore.log |
| 三个测试项目及功能/主题依赖的 Release 构建 | 通过，0 警告/0 错误 | 验证目录内 *-build.log |
| Core | 127/127；失败/错误/跳过/未运行均 0 | [Core XML](../artifacts/treedatagrid-alignment/a0-20261023/run-ae8cadbf8dce4e0d8d732b7babd58797/TreeDataGrid.Core.Tests.xml) |
| Headless | 177/177；失败/错误/跳过/未运行均 0 | [Headless XML](../artifacts/treedatagrid-alignment/a0-20261023/run-ae8cadbf8dce4e0d8d732b7babd58797/TreeDataGrid.Headless.Tests.xml) |
| Themes | 24/24；失败/错误/跳过/未运行均 0 | [Themes XML](../artifacts/treedatagrid-alignment/a0-20261023/run-ae8cadbf8dce4e0d8d732b7babd58797/TreeDataGrid.Themes.Tests.xml) |
| 功能/主题双包 | 通过，当前预览版本本地打包 | [打包及主题消费者日志](../artifacts/treedatagrid-alignment/a0-20261023/run-ae8cadbf8dce4e0d8d732b7babd58797/theme-package-consumer.log) |
| 主题包独立消费者 | 通过，新目录和缓存、PackageReference、无源码引用 | [Consumer.csproj](../artifacts/consumer/5770ac1591bc41c1a21eb357678ba9df/Consumer.csproj) |
| 纯功能包独立消费者 | 通过，Passed=true，自定义模板实际消费 | [result.json](../artifacts/consumer/treedatagrid-function-6585fc749e924a5bbfd34dd69e33787c/result.json) |
| 精确及历史 API | PublicSurfaceMatchesReviewedContract、P1/P2P3/P4P5 历史成员检查全部通过 | Core XML 中 PublicApiTests |
| 范围源码一致性 | 113 份文件的运行前后 SHA256 清单一致 | [source-manifest.json](../artifacts/treedatagrid-alignment/a0-20261023/run-ae8cadbf8dce4e0d8d732b7babd58797/source-manifest.json)、[最终清单](../artifacts/treedatagrid-alignment/a0-20261023/run-ae8cadbf8dce4e0d8d732b7babd58797/source-manifest-final.json) |

总结果：[results.json](../artifacts/treedatagrid-alignment/a0-20261023/run-ae8cadbf8dce4e0d8d732b7babd58797/results.json)。重跑脚本：[Run-Baseline.ps1](../artifacts/treedatagrid-alignment/a0-20261023/Run-Baseline.ps1)，后续运行自动创建独立 run 目录，不覆盖本次证据。

当前 API 快照 SHA256：`21F713DB196D9C5C7B2D6BFE2ABD25320C3DE4DA7B204CA5CE4DB4DA7D777656`。本轮未设置 TREEGRID_UPDATE_API，也未重写历史快照。

首次受限网络执行时，NuGet 漏洞数据查询访问 api.nuget.org 失败并触发 NU1900/警告视为错误，锁定还原失败。保留[失败结果](../artifacts/treedatagrid-alignment/a0-20261023/results.json)及[日志](../artifacts/treedatagrid-alignment/a0-20261023/TreeDataGrid.Core.Tests-restore.log)；正常网络权限下完整重跑通过，没有关闭 NuGet audit、放宽锁文件或降低警告门槛。

## 验证范围与环境边界

本轮使用当前 SDK **10.0.402**（global.json 禁止滚动）、Avalonia 12.0.3 / net10.0；不沿用历史 SDK 10.0.401 的记录。环境见 [environment.json](../artifacts/treedatagrid-alignment/a0-20261023/run-ae8cadbf8dce4e0d8d732b7babd58797/environment.json)及[SDK/已安装运行时补充](../artifacts/treedatagrid-alignment/a0-20261023/run-ae8cadbf8dce4e0d8d732b7babd58797/environment-detail.json)。environment.json 的 runtime 字段描述 PowerShell 宿主，不能直接等同于 dotnet 测试进程的实际运行时。范围清单覆盖 TreeDataGrid 功能/主题源码、三个测试项目、中央构建配置、当前和历史 API 快照以及两个消费者脚本。

工作区另有 PdfViewer 正在修改。本轮运行 TreeDataGrid 范围的锁定还原/构建/测试/打包/消费者，不执行整个 solution 的 Verify.ps1，也不把其它模块变化计入 TreeDataGrid 基线。源码前后清单一致只能证明清单范围没有变化，不代表整个仓库冻结。

328 项中包含已有 11 项异步加载设计私有原型，不能据测试通过宣称产品已有异步 loader。没有新增产品 API，因此本轮不进行新增功能的原生交互检查。

以下均继续待验：真实 IME、物理拖动、系统剪贴板、字体/多 DPI/多显示器、真实屏幕阅读器与原生自动化桥接、实际 Windows CI runner、干净系统部署、原生长期耐久，以及尚未明确配置的目标麒麟。这些条件不会阻止 A1 源码开发，但对应平台验收仍未完成。

## A1 实施入口

按照 A1.1 → A1.2 → A1.3 → A1.4 → A1.5 顺序推进：文本对齐/裁剪、搜索配置、每列编辑手势、每列排序配置、列头显隐。每一项同时更新源码、必要测试、编译 XAML/主题消费示例和使用说明；只有存在实际新 API 时才更新当前精确快照。

A2 的可取消选择尚未实现，A1 搜索暂时沿用既有选择入口；A2 必须补上搜索取消后返回 null 且不滚动的集成用例。A1 不提前声明 A2 的事务能力。
