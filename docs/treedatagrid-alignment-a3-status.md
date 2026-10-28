# TreeDataGrid A3 受控绑定交付与验证记录

日期：2026-10-23（香港时间）。本轮交付受控编译绑定适配器、ConvertBack、编译层级适配和复杂路径支持表。**A3.1 的任意原生不透明 CompiledBinding 自动安全写回仍未完成**；其公开 API 限制已形成可复现原型，不将受控适配器或显式 setter 记为任意绑定自动写回。

版本保持 0.2.0-preview.2；Avalonia 12.0.3 / net10.0 / SDK 10.0.402，没有升级依赖、使用绑定私有反射或上传公共包。

## 本轮实现

| 任务 | 交付与状态 |
| --- | --- |
| A3.1 原型与受控产品接入 | TreeDataGridBinding.Create 从表达式保留公开成员和常量索引；Attach / XAML TreeDataGridBindingAdapter 声明权威模型路径；自动捕获 writer，无需业务 setter |
| A3.1 通用扩展 | 未注册的原生编译路径继续显示，但不能自动推断安全写入目标；保留未完成，不关闭 A3 总项 |
| A3.2 ConvertBack | 使用实际模型类型、参数、文化；原始 OriginalValue 与格式化草稿分开；转换后验证，一次 writer；错误/无更新标记零写回 |
| A3.3 层级扩展 | 实际编译 XAML 与代码工厂适配 Children/HasChildren/IsExpanded；处理对象和集合替换、null、展开写回、折叠/过滤节点的通知与退订 |
| A3.4 支持矩阵 | 区分自动、显式 writer、未支持；自动纳入引用对象链、显式 Source、常量索引到引用对象；视觉源、值类型中间节点、索引槽/动态表达式明确诊断 |

新增 `TreeDataGridColumn.GetEditBindingError(model)` 提供编辑诊断。原有 ValueSetter、SetValueAccessors 和 getter-only 只读行为保留；显式 accessor 不自动叠加绑定 ConvertBack，应用仍可配置 ValueConverter。

编辑过程捕获完整引用链、公共成员、绑定对象/路径注册、外部源、converter/参数/文化等配置；转换前后和最终 writer 前重新验证。源、节点、列、内部列、配置或目标变更时拒绝旧草稿。工厂核对公开成员的声明类型与元数据身份，避免把编译基类成员误写到运行时同名隐藏属性；继承的同一真实成员按元数据身份匹配。

转换回调也进入 State 的回调保护。UnsetValue、DoNothing、BindingNotification、转换异常、错误类型与非 nullable 值属性的 null 不进入 validator/writer，保留草稿和 ValidationError；相反，nullable 模型允许实际 null。ValueConverter 优先于绑定 ConvertBack。StringFormat 仅作显示格式。成功提交继续按 A2 发送一次 CellValueChanged，模型 INPC 不重复；回收编辑器不改变已捕获会话目标。

权威支持矩阵、示例和转换/层级契约见[绑定支持表](treedatagrid-binding-support.md)及[使用说明](treedatagrid-usage.md#受控编译绑定与-convertback)。XAML adapter 的 Path 是作者声明的模型目标，不是从不透明编译节点证明出的等价路径；作者须与业务意图对应。

## 可复现的原生绑定边界

[原型源码](../artifacts/treedatagrid-alignment/a3-completion-20261023/prototype/Program.cs)只检查公开成员并使用公开 Bind / GetBindingExpressionBase / UpdateSource：

1. CompiledBindingPath 的公开面没有目标枚举器。[API 清单](../artifacts/treedatagrid-alignment/a3-completion-20261023/prototype/public-api.txt)记录随包实际类型。
2. Details[0].Name 与 Details[1].Name 的 ToString 都为 Details.Item.Name，不能据此恢复索引。
3. Explicit 源更新虽然暂存值，但替换中间对象后 UpdateSource 跟随新目标；旧目标写 0 次，新目标收到一次旧草稿。

[行为 JSON](../artifacts/treedatagrid-alignment/a3-completion-20261023/prototype/public-binding-proof.json)对上述预期有断言。该结果表明，仅把原生绑定接到临时槽并在最后调用 UpdateSource，不满足本项目“捕获原目标，替换后零写回”的约束。

因此，本轮采用计划允许的正式受控适配入口，并保留通用原生能力的扩展项。后续需要上游可捕获目标的公开接口或进一步正式适配契约；不将该技术限制转写成已完成，也不为完成状态而升级依赖或读取内部节点。

## 测试与示例

相对 A2 的 405 项，新增 **37 项**，最终 **442 项**：Core138、Headless276、Themes28。

| 新增组 | 用例数 | 验收重点 |
| --- | ---: | --- |
| Headless / BindingWritebackTests | 33 | 编译 XAML 无业务 setter 暂存；取消/提交；ConvertBack 类型/参数/文化/nullable；无更新标记与错误类型；validator；原始值/格式分离；完整链/绑定/外部源/索引替换；回收；重入/换源；隐藏成员；重注册路径 |
| Headless / CompiledHierarchyAdapterTests | 3 | 实际编译 XAML 层级三种绑定；对象/集合替换与 null；折叠/过滤；旧图退订；无适配路径/错误模式不替换有效源 |
| Themes / BindingWritebackThemeTests | 1 | 默认主题亮暗切换、列头显隐保留 converter 草稿；自动化 provider 共用受控写回 |

既有“converter 一律不能自动编辑”的限制测试已更新为新契约：单向绑定仍只读；缺少 ConvertBack 的 converter 可创建草稿，但提交明确失败且零写回；显式 typed writer 路径继续验证。其它 A1/A2 与此前回归保留。

统一 Demo 新增“数据绑定 → 受控编译绑定与转换”，普通模式为 17 个场景。数量输入“数量 42”后转换成整数再校验；同时展示编译层级与嵌套状态替换保护。7 项 A3 原生检查和新场景使 Demo 达到 144 项。纯功能包消费者增加实际编译 XAML adapter、ConvertBack 类型/参数/文化、DoNothing、嵌套目标保护和原生不透明路径诊断，达到 35 项。

## 最终验证证据

当次目录：`artifacts/treedatagrid-alignment/a3-completion-20261023/run-dfc8740a5ab64c22acfef07ea1e759c7/`。重跑入口：[Run-Verification.ps1](../artifacts/treedatagrid-alignment/a3-completion-20261023/Run-Verification.ps1)。

最终运行于香港时间 **20:19:54–20:21:04**。总结果 passed=true，验证期间范围文件一致；442 项控件测试的失败、错误、跳过和未运行均为 0。下表各项全部通过，图库、原生检查与包消费单列，不混加为控件测试数量。

| 检查 | 证据 |
| --- | --- |
| 锁定还原、Release 构建、范围文件一致性 | [总结果](../artifacts/treedatagrid-alignment/a3-completion-20261023/run-dfc8740a5ab64c22acfef07ea1e759c7/results.json) |
| Core | [138 项 XML](../artifacts/treedatagrid-alignment/a3-completion-20261023/run-dfc8740a5ab64c22acfef07ea1e759c7/TreeDataGrid.Core.Tests.xml) |
| Headless | [276 项 XML](../artifacts/treedatagrid-alignment/a3-completion-20261023/run-dfc8740a5ab64c22acfef07ea1e759c7/TreeDataGrid.Headless.Tests.xml) |
| Themes | [28 项 XML](../artifacts/treedatagrid-alignment/a3-completion-20261023/run-dfc8740a5ab64c22acfef07ea1e759c7/TreeDataGrid.Themes.Tests.xml) |
| 当前/历史 API | Core PublicApiTests；当前快照 [public-api.txt](public-api.txt) |
| 主题包独立消费 | [新缓存项目](../artifacts/consumer/f8e4f790e0c94971b5bd3845930b5d31/Consumer.csproj) |
| 纯功能包消费 | [35 项 result.json](../artifacts/consumer/treedatagrid-function-338f87af32894eb680396cbf25d1b8f2/result.json) |
| 原生程序化检查 | [Demo 144 项](../artifacts/treedatagrid-alignment/a3-completion-20261023/run-dfc8740a5ab64c22acfef07ea1e759c7/native/demo.json)、[MVVM 29 项](../artifacts/treedatagrid-alignment/a3-completion-20261023/run-dfc8740a5ab64c22acfef07ea1e759c7/native/mvvm.json)、[既有回归 36 项](../artifacts/treedatagrid-alignment/a3-completion-20261023/run-dfc8740a5ab64c22acfef07ea1e759c7/native/regression.json) |
| 图库导航 | [TreeDataGrid 全场景进出测试](../artifacts/treedatagrid-alignment/a3-completion-20261023/run-dfc8740a5ab64c22acfef07ea1e759c7/tree-gallery-navigation.xml) |
| 程序集一致性 | 功能 DLL 11 份、主题 DLL 7 份分别一致，包含包内文件和消费者；原生 Demo 与图库测试使用的 Demo DLL 一致；[binary-hashes.json](../artifacts/treedatagrid-alignment/a3-completion-20261023/run-dfc8740a5ab64c22acfef07ea1e759c7/binary-hashes.json) |

纯功能包消费者 35 项全部 true，Passed=true、Error=null。当前 API SHA256 为 `3D00345675EED1400510F9F180A0AD0E45E09D859E6D988EB9B670E76B7347A3`，历史快照不修改。已查看 [A3 示例图像](../artifacts/treedatagrid-alignment/a3-completion-20261023/run-dfc8740a5ab64c22acfef07ea1e759c7/native/demo-a3-binding-writeback.png)，受控绑定场景、转换后的数量、嵌套层级和目标替换后的状态正常显示。

重复预览曾显示空白，但两次运行的图像 SHA256 均为 `A237892E3212EC47C83D656A9182694BF99139999EE99A760FDA84583D058734`，与前次已正常查看的图像完全一致；最终文件的[像素检查](../artifacts/treedatagrid-alignment/a3-completion-20261023/run-dfc8740a5ab64c22acfef07ea1e759c7/image-pixels.json)采样得到 493 种颜色，排除文件为空白。未为预览显示异常更改产品或烟雾检查。

验证范围为 TreeDataGrid、相关测试/示例、消费者脚本、API、绑定矩阵和公开 API 原型，共 194 份文件。图库测试使用当次已构建并原生检查的 Demo，以 --no-dependencies 编译图库测试；不把其它控件的并行源码纳入验收。本轮不执行全仓库 Verify.ps1。原生检查为程序化输入和窗口内离屏 RenderTargetBitmap，不代表真实物理输入、IME、多 DPI、读屏或麒麟；这些仍按 A6 验证。

## 实施中处理的问题

新缓存还原的 NuGet 网络/审核请求在沙箱内失败，使用已授权的依赖网络范围后继续，没有关闭审核或放宽警告。XAML 的字符串 ConverterCulture=fr-FR 在当前 Avalonia 编译器触发 CultureInfoIetfLanguageTagConverter 构造错误；实际 XAML 用 x:Static 返回 CultureInfo 的公开属性，保留文化传参验证，没有把上游编译器问题当作控件支持。

工厂首次成员核对对继承索引器的 ReflectedType 过于严格，改为公开成员的 Module/MetadataToken/DeclaringType 身份；新增基类隐藏成员与路径重注册测试。开发期失败 XML 和初次消费者保留在同阶段 artifacts，最终只以上述最终运行结果为准。

A3.2–A3.4 和 A3.1 的原型/受控适配器已交付；A3.1 通用原生编译绑定扩展继续保持未完成。

后续已基于此受控范围完成 A4 拖放、ItemsSource 集合适配和多节点原子移动，见 [A4 验收记录](treedatagrid-alignment-a4-status.md)。本记录数字保留为 A3 历史结果，不关闭上述 A3.1 未完成项。
