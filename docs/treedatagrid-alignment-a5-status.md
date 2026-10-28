# TreeDataGrid A5 实现与验收记录

后续进展：A6 本地自动范围已完成，509 项控件测试和扩展测量/包消费/发布目录原生检查通过；真实平台仍待验，见 [A6 记录](treedatagrid-alignment-a6-status.md)。下文保留 A5 当次结果。

日期：2026-10-23（香港时间）。本轮范围为可替换元素工厂、回收所有权、模板兼容与独立消费。A3.1 任意原生不透明 CompiledBinding 的自动安全写回继续开放，A6 的真实输入、读屏、多 DPI 和目标麒麟验收未由本轮替代。

版本保持 0.2.0-preview.2，Avalonia 12.0.3 / net10.0 / SDK 10.0.402。本轮没有升级依赖或发布公共包；其它控件的并行改动保持原状。

## 本轮交付

| 范围 | 实现 |
| --- | --- |
| 公开入口 | 可空 ElementFactory 属性、TreeDataGridElementFactory、Row/Cell/ColumnHeader 三角色、不可变准备上下文；实际编译 XAML 资源可用 |
| 生命周期 | Create / Prepare / Clear / Recycle / Release；明确与 A2 CellPrepared/CellClearing 和原 RowPrepared/RowClearing 的顺序 |
| 池与所有权 | 表格、每次工厂赋值会话和角色隔离；旧会话退役、池裁剪、卸载和故障永久释放；行退出时释放其备用单元格 |
| 动态切换 | A/B/默认工厂切换重新实现可见宿主，保留有效选择、当前地址、同一草稿及滚动；切换不写模型 |
| 重入与故障 | 拒绝错误角色/null/已挂载或已发放对象；回调换源/工厂复验，Prepare 返回后清理迟安装的订阅；异常后继续必要清理并传播错误 |
| 主题与自动化 | 标准宿主保留旧输入和逻辑身份 peer；亮暗主题、列头显隐、局部模板和应用自有模板组合验证 |

原 presenter/header 的 sealed 类型和历史公开 API 快照保留。工厂返回标准宿主，在其上定制模板、属性与寿命；本轮不支持任意 Control/宿主子类替换。功能包仍不依赖主题包。详细类型、时序、应用订阅责任和重入例外见[元素工厂契约](treedatagrid-element-factory.md)。

统一 Demo 在“自定义与模板”增加“元素工厂与容器回收”，普通模式共 19 个场景。A/B 工厂分别提供青色/紫色单元格装饰，展示创建、准备、清理、回收、释放、活动及订阅计数；提供开始编辑、切换、提交/取消按钮。该场景收起重复的公共编辑/筛选工具，让工厂计数与表格有足够显示空间。

## 测试与消费覆盖

相对 A4 的 476 项，新增 **25 项**：Headless 的 ElementFactoryTests 23 项，Themes 的 ElementFactoryThemeTests 2 项。最终控件测试 **501 项**：Core149、Headless321、Themes31，全部通过，失败/错误/跳过/未运行均为 0。

新增用例覆盖三角色寿命与 A2 顺序，A/B/默认切换保留同一草稿/选择/偏移和一次 writer，2000 行 × 60 列双向滚动与保留容器上界，源/模板更换，三角色错误返回，三角色 Prepare 内换源/工厂后安装订阅，Prepare/Clear/Recycle/Release 抛错后的其它容器清理与恢复，拒绝回收，共享工厂的跨表隔离，已发放/父级对象拒绝，A2 Prepared 重入以及实际编译 XAML。

主题测试验证默认亮暗主题、列头显隐、局部单元格模板、草稿提交、逻辑 IValueProvider 写入及列头 IInvokeProvider 排序。旧 Core、Headless、Themes 回归和当前/三份历史 API 检查全部保留。当前公开 API SHA256：`DAF8E041FE01F557450F4DE1D393840B1C87B890E0CE3F4173D1C70AD8AFE53B`。

纯功能包消费者实际编译 XAML 中的工厂资源，保留应用自有表格、单元格与列头模板，从初始加载起使用自定义工厂。原 42 项回归加 8 项工厂检查，共 50 项，验证三角色/池钩子、换源、草稿零写回、旧订阅及容器释放、主题/列头、一次提交和逻辑自动化、恢复默认工厂。项目只引用功能 NuGet，不引用源码或主题包。

## 默认路径性能比较

使用 A4 验收消费者保留的真实功能 DLL（SHA256 `A25B0964DFE7E3A377ACE3E46CBEB308F89B64FC3121B2A146AE11AFE2EE6CAE`）与 A5 功能 DLL（`7A5CDD48C718C40A3E7123E23E8FDF62E479A50B684FBF46FBC6B15D2D9C24C3`），运行相同公开 API 源码和自定义模板，不在基准中调用新增工厂 API。

同机 Windows 10.0.26200 / SDK 10.0.402 / 16 逻辑处理器，10000 行 × 64 列、500×320 视口、冻结 1 列，双方均预热 500 次滚动布局，随后 5 轮各 100 次双轴滚动/布局。分配为 UI 线程累计字节；耗时包含该轮布局。内置默认工厂跳过无自定义钩子的逐寿命 context 分配。

| 指标 | A4 | A5 |
| --- | ---: | ---: |
| 每轮中位耗时 | 158.3828 ms | 158.2264 ms |
| 每轮中位分配 | 105450416 B | 105478528 B |
| 峰值保留行 | 14 | 14 |
| 峰值实现单元格 | 84 | 84 |

耗时比约 0.9990，分配比约 1.00027（+0.027%），未见该场景下明显退化。初始仅预热 30 次的结果受 JIT 等启动开销影响，逐轮明显下降，未作为稳态判断；双方相同增至 500 次预热后重新测量，原始结果保留在 warmup30 文件。本测量不是原生帧率、输入延迟、长期耐久或所有模板的性能承诺，这些继续按 A6 扩展。

基准[源码](../artifacts/treedatagrid-alignment/a5-completion-20261023/performance/Program.cs)、[运行脚本](../artifacts/treedatagrid-alignment/a5-completion-20261023/performance/Run-Comparison.ps1)、[A4 原始结果](../artifacts/treedatagrid-alignment/a5-completion-20261023/performance/baseline.json)、[A5 原始结果](../artifacts/treedatagrid-alignment/a5-completion-20261023/performance/current.json)和[比较及哈希](../artifacts/treedatagrid-alignment/a5-completion-20261023/performance/comparison.json)可复核。

## 最终验证证据

当次目录：`artifacts/treedatagrid-alignment/a5-completion-20261023/run-b3073a6ec63e408da37dbf1d700d639e/`。香港时间 **21:52:59–21:58:32**；[汇总 final-validation.json](../artifacts/treedatagrid-alignment/a5-completion-20261023/run-b3073a6ec63e408da37dbf1d700d639e/final-validation.json) passed=true，220 份范围文件自测试开始至续跑完成及汇总时 SHA256 保持一致。包消费、原生和图库不混入 501 项控件测试数量。

| 检查 | 证据 |
| --- | --- |
| Core149 | [XML](../artifacts/treedatagrid-alignment/a5-completion-20261023/run-b3073a6ec63e408da37dbf1d700d639e/TreeDataGrid.Core.Tests.xml) |
| Headless321 | [XML](../artifacts/treedatagrid-alignment/a5-completion-20261023/run-b3073a6ec63e408da37dbf1d700d639e/TreeDataGrid.Headless.Tests.xml) |
| Themes31 | [XML](../artifacts/treedatagrid-alignment/a5-completion-20261023/run-b3073a6ec63e408da37dbf1d700d639e/TreeDataGrid.Themes.Tests.xml) |
| Usage/Demo 锁定还原与 Release 构建、原生和图库续跑 | [local-completion.json](../artifacts/treedatagrid-alignment/a5-completion-20261023/run-b3073a6ec63e408da37dbf1d700d639e/local-completion.json)；构建 0 警告/0 错误 |
| 纯功能独立包 50 项 | [result.json](../artifacts/consumer/treedatagrid-function-e3e8440dfc7e4c3eb22d6bd96b7ef2c7/result.json)、[最终执行日志](../artifacts/treedatagrid-alignment/a5-completion-20261023/run-b3073a6ec63e408da37dbf1d700d639e/function-package-consumer-final.log) |
| 主题独立包离线隔离消费 | [结果](../artifacts/treedatagrid-alignment/a5-completion-20261023/offline-theme-consumer-result.json)、[日志](../artifacts/treedatagrid-alignment/a5-completion-20261023/offline-theme-consumer.log)、[本地 feed 来源和哈希](../artifacts/treedatagrid-alignment/a5-completion-20261023/offline-feed-manifest.json) |
| 原生程序化检查 | [Demo161](../artifacts/treedatagrid-alignment/a5-completion-20261023/run-b3073a6ec63e408da37dbf1d700d639e/native/demo.json)、[MVVM29](../artifacts/treedatagrid-alignment/a5-completion-20261023/run-b3073a6ec63e408da37dbf1d700d639e/native/mvvm.json)、[回归36](../artifacts/treedatagrid-alignment/a5-completion-20261023/run-b3073a6ec63e408da37dbf1d700d639e/native/regression.json) |
| 图库全部 TreeDataGrid 场景进出 | [XML](../artifacts/treedatagrid-alignment/a5-completion-20261023/run-b3073a6ec63e408da37dbf1d700d639e/tree-gallery-navigation.xml)，1 项通过 |
| 程序集一致性 | [binary-hashes.json](../artifacts/treedatagrid-alignment/a5-completion-20261023/run-b3073a6ec63e408da37dbf1d700d639e/binary-hashes.json)：功能 DLL 11 份、主题 DLL 7 份分别一致，含包内文件、测试、示例与消费者；图库所用 Demo DLL 与原生所用两份一致 |

已查看[最终工厂示例图像](../artifacts/treedatagrid-alignment/a5-completion-20261023/run-b3073a6ec63e408da37dbf1d700d639e/native/demo-a5-element-factory.png)：工厂操作按钮、A/B 计数与表格完整显示，退役 B 的活动和订阅为 0，A 的青色模板与滚动后的行可见。

完整网络流程的重跑入口为 [Run-Verification.ps1](../artifacts/treedatagrid-alignment/a5-completion-20261023/Run-Verification.ps1)。本次自动审批两次超时没有启动该脚本；沙箱内运行完成 501 项测试后，在主题消费者联网还原处被套接字权限阻止。[原始 results.json](../artifacts/treedatagrid-alignment/a5-completion-20261023/run-b3073a6ec63e408da37dbf1d700d639e/results.json)保留 passed=false 与错误，没有改写为成功。

随后以[本地续跑脚本](../artifacts/treedatagrid-alignment/a5-completion-20261023/Complete-LocalVerification.ps1)完成示例/原生/图库，以[离线主题脚本](../artifacts/treedatagrid-alignment/a5-completion-20261023/Verify-OfflineThemeConsumer.ps1)将本轮联网成功功能消费者缓存中的 13 个依赖 nupkg 和本轮两个产品包组成仅本地 feed，向新的空缓存还原并执行原主题消费者。未复用 A4 产品包，未使用源码引用，未放宽 NuGet 校验。此结果验证包可独立消费，不代表主题包在线干净环境还原已验证。[汇总脚本](../artifacts/treedatagrid-alignment/a5-completion-20261023/Summarize-Verification.ps1)重新执行功能消费者并逐项检查全部结果、源文件和程序集哈希后生成 final-validation.json。

其它控件存在并行改动，本轮未执行全仓库 Verify.ps1。图库测试使用当次原生检查的 Demo，编译时 --no-dependencies。性能比较的 A5 DLL 哈希与最终测试/消费 DLL 一致。

## 实施中处理的问题与边界

初次寿命测试发现控件卸载后列头仍处于活动状态，已补列头脱离清理和重新挂载设置；最初失败 XML 保留，不作为最终通过证据。Prepare 内换源/工厂时，核心可能先清空容器，但工厂回调随后仍可能安装订阅，已增加准备回调结束后的延迟工厂清理与丢弃，测试故意在变更之后订阅以验证无残留。

独立功能消费者第一次还原遇到 NuGet 签名端点 SSL 中断，没有结果文件；重跑的新缓存生成 50 项全部通过结果。之后验证中的联网警告/重试保留在日志，不修改还原源或放宽校验。中间测试、原生材料和消费者仍保留。

工厂只管理视觉宿主寿命，不取得 Source/模型所有权，不对任意应用订阅自动推断退订，也不保证脱离有效源/列的旧草稿继续有效。A3 通用编译绑定扩展、A6 真机、实际 CI、原生 Grid/Table pattern 和发行验收仍分别开放。
