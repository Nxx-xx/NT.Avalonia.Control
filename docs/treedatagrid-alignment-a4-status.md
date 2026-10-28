# TreeDataGrid A4 实现与验收记录

后续进展：A5 元素工厂本地范围已交付，501 项控件测试和相关消费/原生检查通过，见 [A5 验收记录](treedatagrid-alignment-a5-status.md)。下文保留 A4 当次实施与验收证据。

日期：2026-10-23（香港时间）。本轮范围为 A4.1 拖放生命周期、A4.2 ItemsSource 集合适配、A4.3 多节点事务移动。基于 A3 已验收的受控绑定范围实施；A3.1 任意原生不透明 CompiledBinding 自动安全写回仍未完成，不因 A4 交付而关闭。

版本保持 0.2.0-preview.2，Avalonia 12.0.3 / net10.0 / SDK 10.0.402。本轮没有升级依赖、发布公共包或修改其它控件的并行开发内容。

## 本轮交付

| 子项 | 实现 |
| --- | --- |
| A4.1 生命周期 | Bubble 路由 RowDragStarted / RowDragOver / RowDrop；只读源/节点/目标/版本快照，Before/After/Inside 落点、Cancel、Handled、Move 允许效果 |
| A4.1 输入 | RowDragHandle 默认 RowHeader，可选 WholeRow；保留模板输入和修饰键选择，延迟候选手势的普通单击，放下取消在旧编辑提交之前 |
| A4.2 集合适配 | 内部明确移动能力协议，ItemsSource 访问真实集合；精确 ObservableCollection 同父单节点、相同 T 的事务集合单个/批量；不猜测任意 IList 或子类的原子性 |
| A4.3 批量 | TryMoveRows / Source.MoveRows、独立 RowsMoveRequested / Completed、不可变 RowsMoveResult.IdentityMap；源顺序、祖先覆盖后代、多个父集合合并、一次事务提交 |
| A4.3 身份与恢复 | 同父出现位置和子树身份保留；跨父捕获根/后代的新身份；保留源维护的展开状态，模型展开绑定仍为权威；恢复选择、当前项和锚点，矩形按既有规则收敛 |
| 一致性与诊断 | 全部存储和通知准备后统一提交；通知异常仍完成其它观察者；区分未提交与 CommittedWithObserverError；源/列/模板/权限/选择配置失效保护 |

原 TryMoveRow、RowMoveRequested / Completed 签名和单节点处理入口保留。批量操作每批一次请求/完成，不逐行执行旧事件。RowDrop.Handled 由应用接管，默认路径不再次提交；程序直接调用移动 API 不合成拖放事件。完整契约与支持表见[拖放与批量移动契约](treedatagrid-row-move-support.md)和[使用说明](treedatagrid-usage.md#受控行与树节点移动)。

## 测试与消费覆盖

相对 A3 的 442 项，新增 **34 项**，本轮控件测试 **476 项**：Core149、Headless298、Themes29。

| 新增组 | 数量 | 重点 |
| --- | ---: | --- |
| Core / BatchRowMoveTests | 11 | 上下移动与重复出现位置；祖先/后代；多个父集合；全部存储在通知前一致；原子前置失败；通知重入；小集合全部子集/插入位置的独立顺序预期；观察者重排重复对象；移回根后的展开；提交后释放 |
| Headless / A4RowMoveTests | 22 | 实际指针与路由冒泡；开始/悬停/放下取消、Handled、效果；旧草稿零写回；换源/列/许可/拖动区域；异常释放捕获和重入；多选拖动；跨父后代当前项；ItemsSource 原集合/抛错观察者；编译层级；整行与模板输入；批量验证/取消/过期/已提交异常 |
| Themes / BatchRowMoveThemeTests | 1 | 默认主题亮暗和列头显隐后，整行多选拖动保留身份和选择 |

原有同父/跨父单节点、精确重复模型、自动滚动和起始行回收、悬停展开、Esc/捕获丢失/换源等回归保留。当前与三份历史公开 API 检查均保留，历史快照不改写。当前 API SHA256：`D82E2FAE29257F155988E0E302B83B6A83265C04177C69CCA14E7BD133C70BBA`。

统一 Demo 增加“树结构与行操作 → 拖放事件与批量移动”，普通模式共有 18 个场景。使用 ItemsSource 事务集合和受控编译层级绑定，提供选中首组两项、整批跨组移动、RowDrop 取消开关和生命周期/批量计数。7 项 A4 检查连同新场景使原生 Demo 达到 152 项；MVVM29、既有回归36 单列。

纯功能包消费者增加实际编译 XAML 的 RowDragHandle、ItemsSource 原 ObservableCollection 移动、普通集合批量零写入、事务批量身份映射、一次取消请求、实际指针 RowDrop 取消保留草稿，合计 42 项。自定义模板为完整单元格命中区域设置透明背景；不依赖可选主题包。

## 最终验证证据

当次目录：`artifacts/treedatagrid-alignment/a4-completion-20261023/run-a3cfcc049f2e47528036a96e8fcad29f/`。重跑入口：[Run-Verification.ps1](../artifacts/treedatagrid-alignment/a4-completion-20261023/Run-Verification.ps1)。

最终运行于香港时间 **21:05:31–21:06:53**，passed=true，207 份范围文件运行前后 SHA256 一致。476 项测试失败/错误/跳过/未运行均为 0，下表全部通过。包消费、图库和原生检查单列，不混入控件测试数量。

| 检查 | 证据 |
| --- | --- |
| 锁定还原、Release 构建及运行前后范围哈希 | [总结果](../artifacts/treedatagrid-alignment/a4-completion-20261023/run-a3cfcc049f2e47528036a96e8fcad29f/results.json) |
| Core149 | [XML](../artifacts/treedatagrid-alignment/a4-completion-20261023/run-a3cfcc049f2e47528036a96e8fcad29f/TreeDataGrid.Core.Tests.xml) |
| Headless298 | [XML](../artifacts/treedatagrid-alignment/a4-completion-20261023/run-a3cfcc049f2e47528036a96e8fcad29f/TreeDataGrid.Headless.Tests.xml) |
| Themes29 | [XML](../artifacts/treedatagrid-alignment/a4-completion-20261023/run-a3cfcc049f2e47528036a96e8fcad29f/TreeDataGrid.Themes.Tests.xml) |
| 主题独立包消费 | [日志](../artifacts/treedatagrid-alignment/a4-completion-20261023/run-a3cfcc049f2e47528036a96e8fcad29f/theme-package-consumer.log) |
| 纯功能独立包消费 | [日志](../artifacts/treedatagrid-alignment/a4-completion-20261023/run-a3cfcc049f2e47528036a96e8fcad29f/function-package-consumer.log) |
| 原生程序化检查 | [Demo152](../artifacts/treedatagrid-alignment/a4-completion-20261023/run-a3cfcc049f2e47528036a96e8fcad29f/native/demo.json)、[MVVM29](../artifacts/treedatagrid-alignment/a4-completion-20261023/run-a3cfcc049f2e47528036a96e8fcad29f/native/mvvm.json)、[回归36](../artifacts/treedatagrid-alignment/a4-completion-20261023/run-a3cfcc049f2e47528036a96e8fcad29f/native/regression.json) |
| 图库全场景进出 | [XML](../artifacts/treedatagrid-alignment/a4-completion-20261023/run-a3cfcc049f2e47528036a96e8fcad29f/tree-gallery-navigation.xml) |
| 程序集一致性 | 功能 DLL 11 份、主题 DLL 7 份分别一致，含包内文件、测试和消费者；图库所用 Demo DLL 与原生输入一致；[binary-hashes.json](../artifacts/treedatagrid-alignment/a4-completion-20261023/run-a3cfcc049f2e47528036a96e8fcad29f/binary-hashes.json) |

最终纯功能消费者 [42 项 result.json](../artifacts/consumer/treedatagrid-function-fb3615b2b9af407fa1631c737d20ab36/result.json)全部 true，Passed=true、Error=null；[主题消费者项目](../artifacts/consumer/eacdc4f060e64790aee6a2030a6ba69b/Consumer.csproj)来自另一全新缓存。已查看 [A4 示例图像](../artifacts/treedatagrid-alignment/a4-completion-20261023/run-a3cfcc049f2e47528036a96e8fcad29f/native/demo-a4-batch-moving.png)，新场景、取消开关、事件计数、批量按钮和恢复后的多选可见。

本轮执行 TreeDataGrid 范围验证，不执行全仓库 Verify.ps1；其它控件存在并行变更。图库使用当次已构建并运行原生检查的 Demo，编译图库测试时使用 --no-dependencies。范围清单包含 TreeDataGrid 功能/主题、测试/示例、消费者脚本、中央配置、API 和支持矩阵；此前公开 API 原型作为既有绑定证据保留。

## 开发中处理的问题与边界

隧道指针处理器的调用顺序曾使选择处理先于拖动候选捕获，导致草稿过早提交或多选收缩。改由统一指针入口先捕获候选，再在普通单击释放时处理选择；拖动开始时取得焦点/捕获，取消或异常清理。跨父子树曾丢失由 Source 维护的展开状态，已在重建前按精确目标位置恢复；模型展开选择器优先级保持。

独立消费者第一次未刷新新源布局而缺少单元格；第二次自定义单元格内部空白区域没有背景命中，未启动拖动。已补布局刷新与透明背景，并验证同一草稿和模型在放下取消后保留。失败 XML、消费者和中间原生材料保留在同阶段目录，不替代最终验收。

批量仅对已确认事务集合默认执行；普通可写集合、跨父普通 ObservableCollection 仍需应用处理。跨控件、Copy/Link、异步外部载荷不在 A4 范围。物理拖动、真实 IME、多 DPI、读屏、麒麟及全仓库/实际 CI 验收继续按 A6 记录，程序化原生检查和窗口内离屏图不替代这些证据。
