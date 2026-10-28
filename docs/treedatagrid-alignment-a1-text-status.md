# TreeDataGrid A1.1 文本列对齐与裁剪验收

日期：2026-10-23（香港时间）。本文件保留 A1.1 首次交付时的实现与验证记录。后续 A1.2–A1.5 已完成，当前结果见 [A1 完成验收](treedatagrid-alignment-a1-status.md)。

## 实现与使用

`TreeDataGridTextColumn` 新增两个公开属性：

| 属性 | 类型 | 默认值 | 校验与作用 |
| --- | --- | --- | --- |
| TextAlignment | Avalonia.Media.TextAlignment | Left | 非法枚举值在赋值前拒绝；设置显示文本在单元格中的对齐方式 |
| TextTrimming | Avalonia.Media.TextTrimming | CharacterEllipsis | 策略类型，拒绝 null；支持 None、WordEllipsis 等 Avalonia 公开策略 |

默认值保持既有左对齐及字符省略号。树形列配置其 InnerColumn 的文本列；CellPresenter 每次准备时重设文字参数，支持行列回收。运行时修改通过现有列订阅重置测量并刷新可见内容，保留节点、列、选择和暂存会话的身份。

两个属性作用于显示 TextBlock。列头、编辑器和应用模板内的控件沿用各自配置；None 关闭省略号，单元格边界裁剪仍生效。使用方式见[文本列说明](treedatagrid-usage.md#文本列对齐与裁剪)。

统一 Demo 的列类型场景展示居中与按词省略，树场景编号展示右对齐；最小 Usage 的数量列通过 XAML 配置右对齐与 None。纯功能包消费者增加实际 compiled XAML 与文字位置检查。

## 新增回归场景

| 范围 | 用例 | 检查的可观察行为 |
| --- | --- | --- |
| Headless | Alignment_changes_the_actual_text_line_position_and_retains_existing_defaults | 左/中/右切换后实际 TextLine.Start 改变，默认字符省略号保持 |
| Headless | Trimming_changes_actual_collapsing_and_rejects_invalid_configuration_before_notification | 窄列的 CharacterEllipsis/WordEllipsis 真实折叠，None 不折叠；无效赋值不发布通知 |
| Headless | Frozen_and_recycled_cells_reapply_their_own_column_configuration_in_both_scroll_directions | 确认同一容器被复用；冻结列及横纵滚动后不串用上一列配置 |
| Headless | Tree_inner_column_updates_and_replacement_release_the_previous_column_subscription | 树内列动态更新、替换后恢复默认，旧内列不再触发刷新；节点身份保持 |
| Headless | Runtime_changes_preserve_selection_and_draft_without_modifying_application_template_text | 草稿与选区保留、取消零写回，应用模板文本不被列配置覆盖 |
| Headless | Wrapping_and_auto_row_height_are_remeasured_after_runtime_changes | 换行后高度增加，切换配置正确重测，恢复不换行后高度收敛 |
| Headless | Compiled_xaml_consumes_alignment_trimming_and_wrapping_with_nested_bindings | 实际编译 AXAML 配置生效，嵌套模型通知继续更新文本 |
| Themes | Text_layout_survives_light_dark_theme_changes_frozen_columns_and_horizontal_scrolling | 确认默认模板实际应用；亮暗切换、冻结和横向回收后文字折叠及配置正确 |

这八项是 A0 基线之外的新增用例，没有把属性赋值次数当成功能覆盖。初次主题定向测试错误地以 Theme 非 null 判断隐式主题是否应用；已改为检查实际模板中的 PART_ContentPresenter，并保留初次结果在定向验证目录。产品默认主题通过实际文本布局验证。

## 最终验证结果

成功运行时间：2026-10-23 17:01:42–17:02:50（香港时间）。证据目录为 `artifacts/treedatagrid-alignment/a1-text-20261023/run-1384a3ef9c374fff8494ffa124cc52bc/`。

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| 锁定还原与 Release 构建 | 三个测试项目、Usage、Demo 均通过；构建 0 警告/0 错误 | [总结果](../artifacts/treedatagrid-alignment/a1-text-20261023/run-1384a3ef9c374fff8494ffa124cc52bc/results.json)与各 restore/build 日志 |
| Core | 127/127 通过 | [Core XML](../artifacts/treedatagrid-alignment/a1-text-20261023/run-1384a3ef9c374fff8494ffa124cc52bc/TreeDataGrid.Core.Tests.xml) |
| Headless | 184/184 通过 | [Headless XML](../artifacts/treedatagrid-alignment/a1-text-20261023/run-1384a3ef9c374fff8494ffa124cc52bc/TreeDataGrid.Headless.Tests.xml) |
| Themes | 25/25 通过 | [Themes XML](../artifacts/treedatagrid-alignment/a1-text-20261023/run-1384a3ef9c374fff8494ffa124cc52bc/TreeDataGrid.Themes.Tests.xml) |
| API | 精确快照只增加 TextAlignment/TextTrimming 两个属性；P1/P2-P3/P4-P5 历史成员检查全部通过 | [当前 API](public-api.txt)及 Core XML |
| 主题包独立消费者 | 新缓存、PackageReference、无源码引用，通过 | [Consumer.csproj](../artifacts/consumer/4aa757ad345f4c69a3452259938cceec/Consumer.csproj) |
| 纯功能包独立消费者 | 19 项全部 true，包含新的 compiled XAML 文字布局检查 | [result.json](../artifacts/consumer/treedatagrid-function-6dd90e0c1f804a24a3f435855df6227d/result.json) |
| 原生 Demo | 退出 0，Passed=true，120 项；textColumnAlignmentAndTrimming=true | [demo.json](../artifacts/treedatagrid-alignment/a1-text-20261023/run-1384a3ef9c374fff8494ffa124cc52bc/native/demo.json) |
| 原生 MVVM | 退出 0，Passed=true，29 项 | [mvvm.json](../artifacts/treedatagrid-alignment/a1-text-20261023/run-1384a3ef9c374fff8494ffa124cc52bc/native/mvvm.json) |
| 原生既有回归 | 退出 0，Passed=true，36 项 | [regression.json](../artifacts/treedatagrid-alignment/a1-text-20261023/run-1384a3ef9c374fff8494ffa124cc52bc/native/regression.json) |
| 源码一致性 | 范围内 152 份文件运行前后 SHA256 一致 | [初始清单](../artifacts/treedatagrid-alignment/a1-text-20261023/run-1384a3ef9c374fff8494ffa124cc52bc/source-manifest.json)、[最终清单](../artifacts/treedatagrid-alignment/a1-text-20261023/run-1384a3ef9c374fff8494ffa124cc52bc/source-manifest-final.json) |
| 二进制一致性 | 功能 DLL 在构建、三个测试、两个示例、两个消费者及包内共十份副本哈希相同 | [binary-hashes.json](../artifacts/treedatagrid-alignment/a1-text-20261023/run-1384a3ef9c374fff8494ffa124cc52bc/binary-hashes.json) |

合计 **336 项 TreeDataGrid 测试全部通过**，失败、错误、跳过和未运行均为 0。原生 120/29/36 为独立的程序化检查数量，不与测试数相加。版本保持 `0.2.0-preview.2`；仅本地打包，没有发布公共包。

当前 API 快照 SHA256：`87F3DFD9AD8EA4812D754B53BCCAACD5C8452EF45259A753AE53FA89F454678F`。历史快照没有修改；正式验证期间不启用 API 自动更新。

## 原生检查修复与图像复核

首次全流程的构建、336 项测试与两个消费者通过，但原生入口的包装脚本错误地传入绝对输出路径；改为仓库相对路径后，又定位到旧 TreeDataGrid 探针查找已移除的 ControlNavigation 侧栏。首次失败结果保存在 [initial results](../artifacts/treedatagrid-alignment/a1-text-20261023/run-37431639d4d74d2ab6103d84db2a5922/results.json)及该目录的 native/demo.json。

本轮仅适配 TreeDataGrid 探针：检查当前 Breadcrumbs，使用实际返回按钮离开页面，再通过 smoke 模式的 SelectPage 恢复保留页面；确认图库存在 TreeDataGrid 入口，继续验证页面源、草稿、计时器和主题。该探针不把图库入口存在等同于实际逐级点击，目录导航由原有独立测试负责。共享 MainWindow 和图库产品代码未因本轮修改。

修复后从新的源清单完整复跑，所有结果见上表。重跑入口：[Run-Verification.ps1](../artifacts/treedatagrid-alignment/a1-text-20261023/Run-Verification.ps1)。

已查看 [列配置图像](../artifacts/treedatagrid-alignment/a1-text-20261023/run-1384a3ef9c374fff8494ffa124cc52bc/native/demo-columns.png)：短名称居中、长说明在列边界呈现、列头/冻结区域对齐。该图是原生窗口内的 Avalonia RenderTargetBitmap 离屏图，不是桌面截图；实际省略行为同时由 TextLine.HasCollapsed 断言证明。

## 范围与后续

本轮执行 TreeDataGrid 范围验证，没有执行整个仓库的 Verify.ps1 或其它控件的全套测试。152 份清单包括 TreeDataGrid 功能/主题、三个测试项目、TreeDataGrid 示例、相关消费者/原生脚本和 API/中央配置，不表示并行修改中的整个仓库冻结。原生检查使用当前 Demo 二进制，其哈希另行保存。

真实 IME、物理拖动、多 DPI、读屏、长期原生耐久及目标麒麟依旧按 A6 平台清单验收。本轮能力已经通过所列本地测试，不将这些环境项目标为完成。

A1.1 交接时的下一项为 A1.2；后续的搜索、每列编辑/排序和列头显隐已由 [A1 完成验收](treedatagrid-alignment-a1-status.md)记录，本文的 336 项等数量仍描述首次交付。
