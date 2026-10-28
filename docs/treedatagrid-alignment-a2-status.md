# TreeDataGrid A2 完成与验收记录

日期：2026-10-23（香港时间）。状态：A2.1–A2.2 实现、本地自动验证、独立包消费、原生程序化检查和图库导航验收完成。版本保持 0.2.0-preview.2，Fluent 构建 API 继续排除，没有上传公共包。

## 交付能力

| 任务 | 已实现内容 |
| --- | --- |
| A2.1 选择事务 | State 和控件 SelectionChanging；不可变旧/拟议快照；按节点集合与矩形轴保存选择；统一取消、提交、复验、应用和完成通知 |
| 全部主动入口 | 指针、导航键、搜索、选择定位、四类 MVVM 选择/当前项属性、State 程序接口、自动化选择/移除/焦点，以及 BeginEdit 的当前项移动 |
| 取消与重入 | 取消在 converter/validator/writer 之前；保留选集、锚点、当前项、草稿和视口；前事件直接选择重入拒绝，MVVM 输出重定向输入受控排队 |
| 系统清理 | 删除、换源、列显隐/移除和模式/单位变化清理不可取消；SelectionChangeCompleted 包含 Reason、IsForced、OldSelection、NewSelection |
| 兼容入口 | 原 SelectionChanged 的 EventHandler 签名保留，针对选集变化；新强类型完成事件同时覆盖当前项移动；DeferSelectionNotifications 最外层提交一个可取消批次 |
| A2.2 单元格生命周期 | CellPrepared 在身份、绑定、内容与交互建立后；CellClearing 在旧内容/视觉关系、绑定和 DataContext 解除前；成对使用同一份稳定身份快照 |
| 单元格值变化 | CellValueChanged 按 EditCommit、ModelNotification、Refresh 区分；成功提交恰好一次，同步 INPC 去重；外部更新按已跟踪范围比较旧/新读取值 |
| 异常与失效 | 回调换源、删除、隐藏列、换模板停止旧任务；清理异常仍解绑其它容器；已成功 writer 的通知异常不伪装成验证失败或导致重复写回 |

使用入口和完整支持表见[使用说明](treedatagrid-usage.md#选择事务与单元格事件)。新增公开类型/成员已进入[当前 API 快照](public-api.txt)，历史 API 快照不修改。

选择顺序为：验证目标 → 计算提议 → SelectionChanging → 提交必要旧草稿 → 复验源、版本、身份、列与矩形 → 应用 → 同步输出 → 完成事件 → 滚动。成功 writer 若改变投影，不能回滚业务写入；失效的提议不应用。搜索可在成功提交后重新搜索一次并发出新提议，只有接受的新提议发送完成通知。

## 值事件支持边界

| 情形 | 本轮支持 |
| --- | --- |
| 已实现单元格，委托/typed accessor，行模型 INPC | 比较原观察值与列的新读取值，变化时发送 ModelNotification |
| 已实现单元格，反射/编译绑定 | 行通知、绑定值更新、嵌套属性和中间对象替换；清理后释放旧订阅 |
| 无通知模型外部赋值 | 已跟踪容器保留旧基线时，Source.Refresh 可检测并发送 Refresh |
| 未跟踪的离屏外部赋值 | 不自动订阅或扫描全表；重新 Prepared 不伪造值变化 |
| 已知离屏编辑 | 编辑器回收后仍从会话按源/节点/列身份发送一次 EditCommit |
| 草稿、取消、验证/转换失败、首次实现、回收、纯布局变化 | 不产生值事件；成功的相等值写回仍有一次 EditCommit |

HasOldValue/HasNewValue 区分未知与已知 null；当前可报告的观察有两端值。EditCommit 的旧值为 OriginalValue，新值为成功交给 writer 的转换后载荷；不猜测业务 setter 的额外规范化。生命周期位置属于容器寿命快照；值事件使用当前路径/显示位置，目标移除时保留写入前路径。Source 明确区分回调换源前后的身份空间。

这些是可观察范围，不宣称对所有离屏绑定或任意内存赋值提供全量变更流，也没有为事件实现全表订阅。

## 新增自动回归

相对 A1 的 362 项，本轮新增 **43 项**，最终为 **405 项**：Core 138、Headless 240、Themes 27。

| 测试组 | 新增 | 验收重点 |
| --- | ---: | --- |
| Core / SelectionTransactionTests | 11 | 取消零校验/零写回；嵌套批次；重入恢复；前/校验/写/后顺序；强制删除/列清理；成功写后目标删除与矩形失效；观察者异常；批次内外部移除 |
| Headless / SelectionTransactionTests | 14 | 指针/键盘/搜索/定位/自动化共用取消；双向属性/集合恢复；换源/隐藏列/模式变化；搜索重新定位；converter 重入；成功 writer 隐藏目标；隐藏选区保留；完成观察者读取所有选择/编辑镜像 |
| Headless / CellEventTests | 17 | 行列双向回收、排序过滤、换模板配对；清理前旧视觉/绑定；准备/清理异常释放；委托/无通知 Refresh/反射/编译嵌套观察；离屏编辑；相等值提交；失败/取消零通知；回调变更与提交通知异常 |
| Themes / CellEventThemeTests | 1 | 默认主题亮暗切换、横纵滚动、冻结、列头显隐及网格线刷新中寿命配对且零假值通知 |

既有选择、MVVM、绑定、编辑验证、排序过滤、身份、虚拟化、自动化、主题和耐久测试一并回归。

## 示例与包消费

统一 Demo 新增“基础与编辑 → 选择事务与单元格事件”，普通模式现为 16 个场景。提供“阻止离开当前选择”开关、外部模型更新按钮，以及请求/完成、准备/清理和真实值变化计数。新增 6 项 A2 原生程序化检查，连同新场景检查使 Demo 达到 136 项；MVVM 和既有回归仍分别为 29、36 项。

最小 Usage 使用 XAML 事件接入取消开关和值变化输出。纯功能包独立消费者新增事务取消、接受后单次提交、可观察值更新和寿命配对，达到 30 项；仍只引用功能 NuGet，使用应用自定义主题。默认主题另由主题包消费者和 Themes 测试验证。

## 最终验证结果

最终验收目录：`artifacts/treedatagrid-alignment/a2-completion-20261023/run-0e7a90be3e2e48b39f59b554a6f97a41/`。重跑入口：[Run-Verification.ps1](../artifacts/treedatagrid-alignment/a2-completion-20261023/Run-Verification.ps1)。

当次运行于香港时间 **19:29:08–19:30:21**，SDK 10.0.402 / Avalonia 12.0.3 / net10.0 / Windows X64。总结果 passed=true，验证期间 179 份范围文件 SHA256 一致。

| 检查 | 当次结果/证据 |
| --- | --- |
| 锁定还原、Release 构建与源码范围一致性 | 通过，构建 0 警告/0 错误；[results.json](../artifacts/treedatagrid-alignment/a2-completion-20261023/run-0e7a90be3e2e48b39f59b554a6f97a41/results.json) |
| Core | 138/138；[XML](../artifacts/treedatagrid-alignment/a2-completion-20261023/run-0e7a90be3e2e48b39f59b554a6f97a41/TreeDataGrid.Core.Tests.xml) |
| Headless | 240/240；[XML](../artifacts/treedatagrid-alignment/a2-completion-20261023/run-0e7a90be3e2e48b39f59b554a6f97a41/TreeDataGrid.Headless.Tests.xml) |
| Themes | 27/27；[XML](../artifacts/treedatagrid-alignment/a2-completion-20261023/run-0e7a90be3e2e48b39f59b554a6f97a41/TreeDataGrid.Themes.Tests.xml) |
| API 精确及历史检查 | Core / PublicApiTests；历史快照保持兼容 |
| 主题包消费者 | 通过；[独立项目](../artifacts/consumer/1c9fbeb8620747b59216814febfb952c/Consumer.csproj) |
| 纯功能包消费者 | 30 项全部 true，Passed=true，Error 空；[result.json](../artifacts/consumer/treedatagrid-function-b4eba05e6d3f4cd485b73495a78a5ceb/result.json) |
| 原生程序化检查 | 全部退出 0、Passed=true；[Demo 136 项](../artifacts/treedatagrid-alignment/a2-completion-20261023/run-0e7a90be3e2e48b39f59b554a6f97a41/native/demo.json)、[MVVM 29 项](../artifacts/treedatagrid-alignment/a2-completion-20261023/run-0e7a90be3e2e48b39f59b554a6f97a41/native/mvvm.json)、[既有回归 36 项](../artifacts/treedatagrid-alignment/a2-completion-20261023/run-0e7a90be3e2e48b39f59b554a6f97a41/native/regression.json) |
| 图库导航 | 1/1，通过 TreeDataGrid 场景及 MVVM 的进出；[XML](../artifacts/treedatagrid-alignment/a2-completion-20261023/run-0e7a90be3e2e48b39f59b554a6f97a41/tree-gallery-navigation.xml) |
| 源码一致性 | 179 份文件初始/最终一致；[初始清单](../artifacts/treedatagrid-alignment/a2-completion-20261023/run-0e7a90be3e2e48b39f59b554a6f97a41/source-manifest.json)、[最终清单](../artifacts/treedatagrid-alignment/a2-completion-20261023/run-0e7a90be3e2e48b39f59b554a6f97a41/source-manifest-final.json) |
| 程序集一致性 | 功能 DLL 11 份、主题 DLL 7 份副本分别一致，含包内 DLL、测试及消费者；图库测试的 Demo DLL 与原生输入一致；[binary-hashes.json](../artifacts/treedatagrid-alignment/a2-completion-20261023/run-0e7a90be3e2e48b39f59b554a6f97a41/binary-hashes.json) |

405 项控件测试的失败/错误/跳过/未运行均为 0。图库测试、原生检查与包消费者单列，不混加为控件测试数量。当前 API SHA256 为 `DEEC42E1DCDB8767B14484820359F2CCE7FE4C06C7AF9BB06D8FCDBC9C48BFAE`。已查看 [A2 原生示例图像](../artifacts/treedatagrid-alignment/a2-completion-20261023/run-0e7a90be3e2e48b39f59b554a6f97a41/native/demo-a2-selection-events.png)，取消开关、计数和表格布局可见。

验收限于 TreeDataGrid 及相关示例。图库测试针对同轮已构建并原生验证的 Demo 使用 `--no-dependencies` 编译，避免把其它控件的并行工作纳入此阶段验收。本轮没有执行全仓库 Verify.ps1；真实 IME、物理输入、多 DPI、读屏、目标麒麟等仍按 A6/平台计划验证。原生图像是窗口内 RenderTargetBitmap 离屏图，不作为桌面截图或真实输入证据。

## 实施期间修正

补回了校验回调隐藏/移除列、旧编辑成功提交后继续单击编辑、MVVM 输出重定向选择、末行 Enter/Tab 提交，以及保留隐藏选区时仅移动当前项的兼容路径。清理通知提前到视觉关系解除前后，调整模板清空次序，避免旧应用模板被 null 模型重新构造。

第一次独立消费者还原被沙箱网络限制阻断；在已授权的依赖还原范围内使用网络权限后，新缓存消费者通过，没有放宽警告或更换依赖。开发中失败 XML、第一次 404 项完整验收及原生回归失败记录均保留在同阶段 artifacts 目录。

第一次完整验收通过后，复核增加了 MVVM 完成顺序测试，复现 SelectedItem 输入已应用而 SelectedIndex 在完成观察者中仍为旧值的问题。已在状态完成之前发布选择与编辑镜像，并重新执行全部验证；[复现 XML](../artifacts/treedatagrid-alignment/a2-completion-20261023/mvvm-completion-order-before.xml)与[前次验收](../artifacts/treedatagrid-alignment/a2-completion-20261023/run-3cc240b826784105b6c38b54b2a447ad/results.json)单独保留，不代替本次最终结果。

A2 验收后的下一阶段为 A3。后续已交付受控编译绑定、ConvertBack、编译层级绑定和支持矩阵，442 项控件测试及范围集成验证通过；任意原生不透明编译绑定的自动安全写回仍开放，见 [A3 交付记录](treedatagrid-alignment-a3-status.md)。本文数字保留为 A2 历史结果。
