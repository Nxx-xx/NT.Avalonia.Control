# TreeDataGrid A1 完成与验收记录

本文保留 A1 当时的交付和证据；当前 A2 选择事务与单元格事件也已完成，见 [A2 验收](treedatagrid-alignment-a2-status.md)。下文“A2 待实现”等表述属于 A1 的历史边界。

日期：2026-10-23（香港时间）。状态：A1.1–A1.5 的实现、本地自动验证、独立包消费和原生程序化检查完成。Fluent 构建 API 继续排除；版本保持 0.2.0-preview.2，没有上传公共包。

A1.1 的初次交付见[文本列验收](treedatagrid-alignment-a1-text-status.md)。本记录覆盖其余 A1.2–A1.5 的交付，以及包含 A1.1 的最终回归。

## 交付能力

| 任务 | 当前交付 | 兼容和边界 |
| --- | --- | --- |
| A1.1 文本对齐/裁剪 | TextAlignment、TextTrimming、动态更新、树形内列和回收 | 默认 Left/CharacterEllipsis；原有默认行为保持 |
| A1.2 搜索配置 | 列 IsTextSearchEnabled、模板 TextSearchBinding；反射/编译绑定与嵌套值读取 | 外层与内层开关取 AND；selector 优先；临时绑定及时释放；以模型为 DataContext，不承诺视觉树相对目标 |
| A1.3 编辑手势 | 新 Flags 类型 TreeDataGridBeginEditGestures，列级可空 BeginEditGestures | 外层 > 内层 > 旧 EditTriggers；F2 独立，WhenSelected 检查点击前状态；显式 None 禁用用户触发 |
| A1.4 排序配置 | 可空 AllowTriStateSorting、CompareAscending/CompareDescending | 外层优先；false 可覆盖全局 true；降序比较器不重复反转；旧 Comparison 保留 |
| A1.5 列头显隐 | StyledProperty ShowColumnHeaders、默认主题、应用模板及自动化接入 | 默认 true；隐藏释放高度、取消排序点击/重排/调宽、保持 Auto 宽度基线和草稿 |

使用方式见[使用说明](treedatagrid-usage.md#每列搜索编辑与排序配置)和[列头显隐](treedatagrid-usage.md#列头显隐)。新增成员已进入[当前 API 快照](public-api.txt)，历史 API 快照未修改。

搜索中遇到已有其它单元格草稿时，没有命中不提交；命中后校验失败返回 null 且不导航。成功提交后重新读取视图一次，避免写回改变匹配目标。读取或选择回调使源/列/投影失效时不继续用旧快照；搜索直接重入会诊断且可恢复。可取消的 SelectionChanging 仍属于 A2，本轮不声明具备选择回滚事务。

旧全局 EditTriggers.None 的复选框 Space 行为保留；列级显式 BeginEditGestures.None 同时禁止该列 Space 用户切换。程序 BeginEdit 不受用户手势开关禁止，仍须满足只读、writer 和编辑事务检查。修改搜索、手势、方向比较器等元数据不重建活动编辑器或隐式提交草稿。

## 新增自动回归

相比 A1.1 完成时的 336 项，本轮新增 **25 项 Headless 和 1 项 Themes**，最终为 362 项 TreeDataGrid 测试。

| 组别 | 新增用例数 | 验收重点 |
| --- | ---: | --- |
| ColumnSearchOptionsTests | 8 | 禁用/隐藏列不调用 provider；selector 优先级；编译绑定嵌套替换与退订；展开/过滤；换源/隐藏列；搜索重入恢复；编辑校验；编译 XAML |
| ColumnEditGestureTests | 8 | 列与全局覆盖、None、行/单元格 WhenSelected、DoubleTap 组合、树内外层优先级、活动编辑器不重建、校验零写回、选择回调换内列、模板控件输入、复选框 Space 兼容 |
| ColumnSortOptionsTests | 5 | 降序最终顺序及 int.MinValue；稳定同级排序；旧比较器回退；下一次显式排序采用新配置；双向覆盖三态；树内外层优先级和源路径；回调改配置拒绝并恢复 |
| ColumnHeaderVisibilityTests / Headless | 4 | 释放高度、Auto 宽度与草稿；自动化树与缓存逻辑 peer；取消排序点击/重排；取消调宽并可重新开始 |
| ColumnHeaderVisibilityTests / Themes | 1 | 默认主题隐藏整个 Border；亮暗切换；冻结/横滚对齐；恢复布局 |

列级配置的实际 XAML 编译由 A1ColumnOptionsView 验证。其余既有选择、编辑、数据身份、排序过滤、虚拟化、绑定和耐久测试完整回归。

## Demo 与独立包消费

统一 Demo 新增“每列交互与搜索”，普通模式现为 15 个场景，也可从图库“基础与编辑”进入。场景展示：名称选中后单击或 F2 编辑并允许三态排序；数量单击编辑、只切换升降序且不参与搜索；说明模板列通过绑定参与前缀定位。新增公共“定位前缀”操作和“显示列头”布局选项，示例自定义模板同时接入显隐。

最小 Usage 使用 XAML 配置每列手势、搜索和排序覆盖。纯功能包消费者扩展到 25 项检查，增加 compiled XAML 新配置、模板搜索、关闭搜索、方向比较器，以及隐藏列头时保留草稿和释放高度；仍使用应用自定义主题，不依赖 TreeDataGrid.Themes。

## 最终验证结果

最终运行于 2026-10-23 **18:43:17–18:46:51**（香港时间），包含图库测试受其它模块构建影响后的受控续跑。环境为 SDK 10.0.402、Avalonia 12.0.3、net10.0、Windows X64。

证据目录：`artifacts/treedatagrid-alignment/a1-completion-20261023/run-d95c02685afb46729f04026f741c6004/`。以下是当次实测结果，不使用旧记录的数量代替本轮验收。

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| 锁定还原与 Release 构建 | 控件/三个测试/Usage/Demo 通过，0 警告/0 错误 | [总结果](../artifacts/treedatagrid-alignment/a1-completion-20261023/run-d95c02685afb46729f04026f741c6004/results.json)及同目录日志 |
| Core | 127/127 | [Core XML](../artifacts/treedatagrid-alignment/a1-completion-20261023/run-d95c02685afb46729f04026f741c6004/TreeDataGrid.Core.Tests.xml) |
| Headless | 209/209 | [Headless XML](../artifacts/treedatagrid-alignment/a1-completion-20261023/run-d95c02685afb46729f04026f741c6004/TreeDataGrid.Headless.Tests.xml) |
| Themes | 26/26 | [Themes XML](../artifacts/treedatagrid-alignment/a1-completion-20261023/run-d95c02685afb46729f04026f741c6004/TreeDataGrid.Themes.Tests.xml) |
| API 精确/历史检查 | 全部通过；新 API 为增量，旧枚举数值与成员保留 | Core XML 中 PublicApiTests；当前 API SHA256 见下文 |
| 主题包消费者 | 新目录/缓存、PackageReference、无源码引用，通过 | [Consumer.csproj](../artifacts/consumer/044d2c25c53c41409bdd51bd87c67527/Consumer.csproj) |
| 纯功能包消费者 | 25 项全部 true，Passed=true，Error 空 | [result.json](../artifacts/consumer/treedatagrid-function-d9a59ac1e7c548a38856332a6c7d12fe/result.json) |
| 原生 Demo | 退出 0，Passed=true，129 项 | [demo.json](../artifacts/treedatagrid-alignment/a1-completion-20261023/run-d95c02685afb46729f04026f741c6004/native/demo.json) |
| 原生 MVVM | 退出 0，Passed=true，29 项 | [mvvm.json](../artifacts/treedatagrid-alignment/a1-completion-20261023/run-d95c02685afb46729f04026f741c6004/native/mvvm.json) |
| 原生既有回归 | 退出 0，Passed=true，36 项 | [regression.json](../artifacts/treedatagrid-alignment/a1-completion-20261023/run-d95c02685afb46729f04026f741c6004/native/regression.json) |
| 图库导航 | Tree_scenarios_and_mvvm_can_switch_without_reparenting_failures，1/1，通过所有 TreeDataGrid 示例及 MVVM 的进出 | [gallery XML](../artifacts/treedatagrid-alignment/a1-completion-20261023/run-d95c02685afb46729f04026f741c6004/tree-gallery-navigation.xml) |
| 源码一致性 | 170 份范围文件初始/最终 SHA256 一致 | [初始清单](../artifacts/treedatagrid-alignment/a1-completion-20261023/run-d95c02685afb46729f04026f741c6004/source-manifest.json)、[最终清单](../artifacts/treedatagrid-alignment/a1-completion-20261023/run-d95c02685afb46729f04026f741c6004/source-manifest-final.json) |
| 程序集一致性 | 功能 DLL 十份、主题 DLL 六份副本分别一致；Demo 与图库测试输入哈希一致 | [binary-hashes.json](../artifacts/treedatagrid-alignment/a1-completion-20261023/run-d95c02685afb46729f04026f741c6004/binary-hashes.json) |

三个 TreeDataGrid 测试程序集合计 **362 项**，失败/错误/跳过/未运行均为 0；图库测试单列。原生检查数量不与测试数量混加。当前 API SHA256：`F3955FF0CF0DCCC2108E97B793065982FE4F12CEDA6276668D0B884893939C9A`。

原生 Demo 新增的 A1 检查覆盖模板绑定定位、禁用搜索、F2 暂存编辑、隐藏/恢复列头保留草稿、列级三态/两态覆盖全局，以及方向专用降序比较器。已查看[显示列头图像](../artifacts/treedatagrid-alignment/a1-completion-20261023/run-d95c02685afb46729f04026f741c6004/native/demo-a1-column-options.png)及[隐藏列头保留草稿图像](../artifacts/treedatagrid-alignment/a1-completion-20261023/run-d95c02685afb46729f04026f741c6004/native/demo-a1-headers-hidden.png)。这些是原生窗口内 RenderTargetBitmap 离屏图，不作为桌面截图或真实物理输入证据。

## 验证过程中的修正与范围

首次控件测试和主题消费者通过，纯功能包消费者新增代码触发 CS8602 可空警告并被 TreatWarningsAsErrors 拦下。已将经过验证的自动化 peer 声明为明确的非空类型，未放宽警告、锁定还原或测试门槛；重新生成的新缓存消费者 25 项通过。首次记录保留在 [run-8520867ed99343a89c71e713708c87fb](../artifacts/treedatagrid-alignment/a1-completion-20261023/run-8520867ed99343a89c71e713708c87fb/results.json)。

最终运行的 362 项控件测试、两个消费者、Usage/Demo 构建和三组原生检查通过后，图库测试的普通传递构建遇到并行 PdfViewer 改动：PdfDocument.cs 中 BeginLoad/CompleteLoadAsync 尚未可用，导致 CS0103。保留[当时结果](../artifacts/treedatagrid-alignment/a1-completion-20261023/run-d95c02685afb46729f04026f741c6004/results-initial.json)和[构建日志](../artifacts/treedatagrid-alignment/a1-completion-20261023/run-d95c02685afb46729f04026f741c6004/tree-gallery-navigation-initial.log)。

图库测试随后使用 `--no-restore --no-dependencies`，针对前面刚构建且原生验证通过的 Demo 产物编译；该测试 1/1 通过。续跑前后检查全部 170 份范围文件和 Demo 二进制哈希，未修改 PdfViewer 来完成本轮任务，也不把该结果当作其它控件当前源码构建通过。续跑记录见 [Resume-Gallery.ps1](../artifacts/treedatagrid-alignment/a1-completion-20261023/Resume-Gallery.ps1)。

本轮没有执行整个仓库 Verify.ps1。范围覆盖 TreeDataGrid 功能/主题、三个测试、相关示例和脚本、API/中央配置，以及图库测试涉及的入口；不表示并行工作区整个仓库冻结。重跑入口：[Run-Verification.ps1](../artifacts/treedatagrid-alignment/a1-completion-20261023/Run-Verification.ps1)。

## 后续阶段

A1 按本地实现与所列证据关闭，下一阶段是 A2：可取消选择事务与单元格生命周期/值变更事件。真实 IME、物理拖动、多 DPI、多显示器、读屏、长期原生耐久和目标麒麟仍按 A6/平台计划验证，不因 A1 测试通过而勾选。
