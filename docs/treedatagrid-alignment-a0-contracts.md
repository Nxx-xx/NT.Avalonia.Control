# TreeDataGrid A0 差异矩阵与兼容契约

日期：2026-10-23。适用计划：[功能补齐开发计划](treedatagrid-feature-alignment-plan.md)。本文保留 A0 的初始差异与后续实现规则；当前 A1、A2、A4 和 A5 本地范围已交付，最新见 [A5 验收记录](treedatagrid-alignment-a5-status.md)与[元素工厂契约](treedatagrid-element-factory.md)。A3 受控范围和通用扩展缺项单列。矩阵描述 A0 起点；后续 API 以各阶段实现记录为准，不能据本契约推断已经支持。

## A0 的六项交付

| 编号 | 交付 | 本轮结果 |
| --- | --- | --- |
| A0.1 | 逐项差异矩阵 | 见下表；区分缺失、入口差异、范围限制和环境待验 |
| A0.2 | 属性、默认值与继承 | 固定 A1 的类型、默认值、展开器优先级和运行时行为 |
| A0.3 | 旧 API 兼容 | 原枚举、成员、默认行为和已发布模板入口保留 |
| A0.4 | 事件、提交、取消、异常顺序 | 固定选择、单元格生命周期、写回和拖放规则 |
| A0.5 | 当次验证基线与环境清单 | 具体运行及结果见 [A0 验证记录](treedatagrid-alignment-a0-status.md) |
| A0.6 | API 快照与兼容检查规则 | 沿用精确快照及 P1/P2-P3/P4-P5 历史成员检查；本轮不增加产品 API |

## 当前差异矩阵

本表依据当前源码及上一轮已查阅的官方 v12 [列文档](https://docs.avaloniaui.net/controls/data-display/structured-data/treedatagrid/column-types)和[控件 API](https://docs.avaloniaui.net/api/avalonia/controls/treedatagrid)。官方参考版本为 12.0.3；未通过官方许可参考程序进行运行时行为对照，不声明逐项默认值和行为完全一致。

| 编号 | 能力 | 当前源码证据 | 差异类别 | 进入阶段 |
| --- | --- | --- | --- | --- |
| D01 | TextAlignment/TextTrimming 列配置 | TreeDataGridColumn.cs 文本列只有 Binding/StringFormat/TextWrapping；CellPresenter 固定 CharacterEllipsis | 配置缺失，已有裁剪显示 | A1.1 |
| D02 | 列搜索开关和模板搜索绑定 | Interaction.SearchText 按可见文本列/委托搜索，没有列开关或 TextSearchBinding | 配置缺失，搜索已实现 | A1.2 |
| D03 | 每列编辑手势/WhenSelected | Interaction.cs 只有全局 EditTriggers，枚举为 None/F2/SingleClick/DoubleClick | 配置与手势缺失 | A1.3 |
| D04 | 每列三态排序、方向比较器 | 全局 IsThreeStateSort，列 Comparison，由 Source 按方向比较 | 配置缺失，单列排序已实现 | A1.4 |
| D05 | ShowColumnHeaders | 默认主题始终包含列头 Border；HeadersPresenter 固定最小高度 28 | 控件开关缺失 | A1.5 |
| D06 | 可取消选择 | State 和控件只有 SelectionChanged；导航有提交编辑和重新验证逻辑 | 事务前事件缺失 | A2.1 |
| D07 | 单元格准备/清理/值变化事件 | CellPresenter 的 Prepare/Clear 为内部操作；有行准备/清理事件 | 公开事件缺失 | A2.2 |
| D08 | compiled/converter 自动写回 | Column.Editing.GetWriter 自动支持受限 ReflectionBinding，compiled 用显式 writer | 自动写回范围限制 | A3 |
| D09 | compiled/复杂层级绑定 | ItemsSource 适配支持反射点分路径及订阅，不支持任意绑定表达式 | 绑定范围限制 | A3 |
| D10 | 拖放生命周期 | RowMoving 使用行号列指针拖动和 RowMoveRequested/Completed | 已有同表移动；事件入口不同 | A4.1 |
| D11 | ItemsSource 默认移动 | 自动适配擦除集合类型，默认移动不支持；应用可处理请求 | 数据接入范围限制 | A4.2 |
| D12 | 多选节点一起移动 | TryMoveRow 和在途状态只捕获一个节点 | 本轮增强；官方同等行为未实测 | A4.3 |
| D13 | ElementFactory | presenters 直接创建、准备和回收容器 | 工厂扩展入口缺失 | A5 |
| D14 | 自动化/真实输入/平台性能 | 已有专用 peer/provider 及 Headless 回归；真实读屏、IME、DPI、麒麟等待验 | 环境待验和平台接口限制 | A6 |
| D15 | SelectionMode 等官方形态 | 本项目用 SelectionUnit 与 Single/Multiple，Columns 在控件上，State 为独立实现 | 有意的 API 差异，保持兼容 | 文档与迁移 |
| D16 | 内置异步加载、跨控件拖放、导出等 | 异步仅设计原型，其余见后续增强计划 | 独立增强，不计作已核实官方缺口 | E1–E4 |

Fluent API 不进入实施清单。当前冻结、显隐、列重排、单节点同表拖动、嵌套反射绑定等已实现能力继续回归。

## A1 属性与默认值

以下名称为本项目新增契约，允许与官方类型/签名不同；正式加入公开快照前须使用实际 XAML 编译确认可用。

| 所属类型 | 新增成员/类型 | 默认值 | 规则 |
| --- | --- | --- | --- |
| TreeDataGridTextColumn | TextAlignment: Avalonia.Media.TextAlignment | Left | 文本列直接控制内部 TextBlock，不修改应用模板中的任意控件 |
| TreeDataGridTextColumn | TextTrimming: Avalonia.Media.TextTrimming | CharacterEllipsis | 保持现有裁剪表现；允许 None；与换行共同参与测量 |
| TreeDataGridColumn | IsTextSearchEnabled: bool | true | 外层和有效内列任一 false 都排除该列；无搜索值提供者的非文本列仍不自动参与 |
| TreeDataGridTemplateColumn | TextSearchBinding: BindingBase?，AssignBinding 元数据 | null | 仅用于读取搜索文本；不承担编辑写回 |
| TreeDataGridColumn | BeginEditGestures: TreeDataGridBeginEditGestures? | null | 外层显式值 > 内列显式值 > 控件 EditTriggers 映射 |
| TreeDataGridColumn | AllowTriStateSorting: bool? | null | 外层 > 内列 > 控件 IsThreeStateSort；false 可覆盖全局 true |
| TreeDataGridColumn | CompareAscending/CompareDescending: Comparison<object>? | null | 非 null 委托按模型比较；方向语义见下文 |
| TreeDataGrid | ShowColumnHeaders: StyledProperty<bool> 与 CLR 属性 | true | 默认主题隐藏整条列头区域；presenter 清理已实现列头，自动化树随之更新 |

TreeDataGridBeginEditGestures 为新 Flags 枚举：None=0、F2=1、Tap=2、DoubleTap=4、WhenSelected=8。旧 TreeDataGridEditTriggers 不改名、不改数值、不增加 WhenSelected 值；全局旧枚举映射到对应新手势。null 表示继承，显式 None 禁用该列用户手势，仍允许通过 BeginEdit 主动编辑可写列。

### 展开器、搜索与运行时更新

- 文本外观属于 InnerColumn，树包装列控制缩进/展开按钮；不把文字格式参数复制到包装列。
- IsTextSearchEnabled 使用外层与有效内列的逻辑 AND；搜索值按有效内列显式 TextSearchValueSelector > 模板 TextSearchBinding > 文本列原始值获取。旧 selector 返回 null 时，保留文本列旧行为的原值回退；模板列有显式 provider 时不意外回退到模型 ToString。
- 用户搜索命中后仍须通过选择事务。A2 完成后，取消选择或旧编辑失败时 SearchText 返回 null，不滚动、不误报已定位。
- WhenSelected 只修饰鼠标点击：点击前该单元格已是当前项且已选中，点击后仍是同一目标，才允许进入编辑。单独 WhenSelected 代表已选中项再次单击编辑；与 Tap/DoubleTap 组合时约束相应点击。F2 独立工作；程序选中和焦点变化不会自动开启编辑。
- 文本属性运行时变更通过列通知重新准备可见内容，重置相应列宽/行高测量，保持 NodeId/ColumnId、选择和草稿身份。回收时所有文字参数都须重设，避免沿用上一列配置。
- 搜索/手势/三态开关下一次输入即时使用；活动草稿不因改手势而被静默提交或取消。替换排序委托在下一次显式排序时生效，不在属性 setter 内隐式提交草稿；已发布 Source 排序继续使用当时的比较器，直到再次指定排序。
- ShowColumnHeaders=false 取消列头重排和调宽会话，清理列头，释放高度；列集合、宽度与排序状态保留。Auto 宽度在显隐间保留已有宽度基线，避免列头显隐引起列宽跳动。自定义模板需将列头外层区域显隐绑定到该属性。
- 带验证的枚举属性遇到非法值应在赋值时诊断；列本身仍为 INotifyPropertyChanged 对象，不宣称其新 CLR 属性天然支持 Avalonia StyledProperty 样式 setter。

### 排序比较器的方向与优先级

1. 升序优先使用外层 CompareAscending，再内列 CompareAscending，再外层旧 Comparison，再内列旧 Comparison，最后沿用默认值比较。
2. 降序优先使用外层 CompareDescending，再内列 CompareDescending。存在时直接代表最终降序顺序；不存在时反转上述升序比较。
3. Source.Sort 现有“比较器加排序方向”的公开契约保持。控件桥接方向专用比较器时使用参数交换等方式消除 Source 的再次反转，不使用可能溢出的结果取负值。
4. 同值保持现有稳定同级排序。比较回调中的换源、列移除和内列替换继续触发版本/目标守卫；失败不能发布半完成投影。

## 旧 API、默认值与模板兼容

- 保留 EditTriggers、IsThreeStateSort、Comparison、TextSearchValueSelector，以及 Source/ItemsSource/Columns 的现有入口。新增设置为 null/默认值时继续旧行为。
- RowMoveRequested/Completed 保留；新增拖放事件负责拖动生命周期，不能再次提交已由应用处理的模型移动。
- 选择单位与选择模式继续分开，IndexPath/NodeId/ColumnId 保持本项目类型和语义，不以替换枚举实现官方字符串兼容。
- 默认宽度 120、最小宽度 24、网格线 All 等已发布默认值保持；这些与官方默认值的差异在支持表说明，不强制更改。
- 必需 PART_ScrollViewer、PART_RowsPresenter 和可选 PART_ColumnHeadersPresenter 保持；引入新能力须同时维护默认主题和纯功能包的应用模板示例。
- 功能程序集不引用主题程序集；新工厂和事件不得把默认主题设为必需依赖。

## 选择事务、编辑与取消

执行顺序固定为：验证入口和目标 → 计算旧/拟议状态 → SelectionChanging → 取消则结束 → 提交必要旧草稿 → 重新验证 Source、版本、身份与列 → 一次应用选择/锚点/当前项 → 同步 MVVM/provider → SelectionChanged → 请求滚动。

| 情形 | 必须行为 |
| --- | --- |
| 前事件取消或抛异常 | 不提交草稿、不写模型、不应用拟议选择、不滚动；异常向调用者报告 |
| 旧草稿转换/验证失败 | 保留原选择、当前项和错误草稿；不执行后续选择 |
| 编辑成功后回调改变源/目标 | 保留已经成功的业务写入，拒绝旧拟议选择，不恢复旧源或写入新目标 |
| 前事件内主动调用选择 | 拒绝选择重入并诊断；模型集合通知可受控排队，回调后重新验证，不递归应用半状态 |
| MVVM 赋值被取消 | SelectedItem/SelectedIndex 等公开状态恢复到实际选择，保留绑定表达式，避免反复回写 |
| 行删除、换源、隐藏列导致强制清理 | 不能取消无效身份的清理；完成通知包含系统清理原因，不伪装成可撤销用户请求 |
| 批量选择/矩形范围 | 一次逻辑事务，一次前/后通知；取消保持旧锚点和选区，不物化全源单元格 |
| 已提交后的观察者异常 | 状态已提交，不能按“未成功”再次应用；必要清理和其它完成观察者仍执行，按既有异常聚合策略报告 |

所有主动选择入口共享事务：指针、键盘、搜索、SelectedItem/SelectedIndex、State 程序选择与自动化 provider。直接改变模型后进行的强制清理单独处理。

## 单元格生命周期与值变更

- CellClearing 在旧身份、DataContext 和绑定解除前发送；随后清理编辑器/绑定并释放旧身份。CellPrepared 在新身份、绑定、内容和交互状态准备完毕后发送。同一容器不能把清理中的旧身份替换为新目标后再通知。
- 回调内换源/删行/换模板时依赖 generation 守卫；旧准备任务停止，必要清理执行一次。清理通知抛错也要解除订阅；已经发出的 Prepared 必须有对应的最终清理。
- CellValueChanged 由数据/编辑变更产生，准备或回收本身不算值变化。成功编辑提交必须通知一次，即便已同步触发 INPC 也不重复。
- 通知包含原因与旧值可用标记；没有旧值时不能用 null 冒充“旧值就是 null”。离屏已知编辑可按身份通知，外部模型变更只在能建立旧/新值的已追踪范围报告；不为事件扫描全表所有绑定。
- 无 INPC 的外部赋值只有显式 Refresh 后才可能被观测。支持表必须列明委托值、反射绑定、编译绑定、离屏目标和 Refresh 的通知边界。

## 写回、拖放与回调异常

- 绑定保持显示与草稿分离。提交顺序为转换/ConvertBack → 验证 → 校验原编辑目标仍有效 → 调用一次 writer → 更新已提交状态 → 通知。取消、目标变化、转换/验证失败均不得调用 writer。
- ConvertBack 的 UnsetValue/DoNothing 是不写入结果，不将该对象赋给模型；保留草稿并给出明确的未提交状态。StringFormat 只用于显示，不推断逆转换。
- getter、converter、validator、writer、集合适配器和应用处理器的任意副作用不作通用回滚承诺；不得把异常后已提交的移动报告为未提交。
- 拖动顺序为 RowDragStarted → 零次或多次 RowDragOver → RowDrop → 取消/Handled 时结束默认行为 → 提交旧草稿 → 校验快照 → RowMoveRequested → 再校验 → 事务移动 → 身份/选择恢复 → RowMoveCompleted。
- RowDrop.Handled 只表示应用接管，默认路径不得再次写集合；RowMoveRequested 的现有 Handled/Result 协议保留。悬停不修改集合，跨控件与 Copy/Link 不因事件存在就自动支持。
- ItemsSource 移动须通过明确的集合能力协议回到真实集合；跨父及多节点要求原子事务/已验证补偿。不能仅凭集合可写就启用 Remove+Insert 批操作。
- 多节点先规范化源顺序、排除已选祖先覆盖的后代，再整批验证和提交；同父保留出现位置身份，跨父通过返回映射恢复，不借用旧显示索引。
- 默认排序/过滤下自动移动继续拒绝；应用处理源位置映射。删除、换源、列/模板版本变更和权限变更使在途快照失效则取消。

## API 快照与测试落点

本轮 A0 只增加文档和验证材料；docs/public-api.txt 及历史快照不修改。后续新增 API 的顺序为：实现实际签名 → 审查导出差异 → 更新当前精确快照 → 运行当前及历史兼容测试。不得重写历史快照、删除历史断言或把精确比较改为宽松比较。

| 用例组 | 自动验证落点 | 必查行为 |
| --- | --- | --- |
| A1 文本与列头 | Headless/ Themes/编译 XAML 消费 | 树内列、冻结/横滚、回收、运行时属性、自动行高、列头高度与自动化树 |
| A1 搜索/编辑/排序 | Headless + Core 排序不变量 | 列配置优先级、旧全局兼容、None/false、选中再次点击、方向比较器不反转两次 |
| A2 状态与生命周期 | Core + Headless + 自动化 | 取消零写回、锚点保持、MVVM 状态一致、重入/换源、成对清理和值通知去重 |
| A3 写回 | compiled AXAML + Headless + 包消费 | 成功一次 writer；取消/ConvertBack/目标更换零写回；旧对象退订 |
| A4 移动 | Core 事务 + Headless 指针 + 原生 | 不部分提交、重复模型、祖先/后代、过期快照、事件不双重写入 |
| A5 工厂 | Headless/ Themes/纯功能包消费 | 旧工厂退役、池不混用、草稿身份、自动化和实现数受视口限制 |
| A6 平台 | 真实系统 + 实际 CI | IME、物理拖动、读屏、DPI、原生耐久和目标麒麟，不以离屏图替代 |

## 平台准备与交付边界

当前可执行项目：Windows 上的 Release 构建、Core/Headless/Themes、独立包消费。实际运行和源码哈希保存在 A0 验证目录。

仍需记录或提供：Windows 实际中文 IME/版本、读屏软件/版本、100%–200% 实际缩放与多显示器配置、物理拖动和系统剪贴板操作、干净部署和长期原生耐久；实际 CI runner 的桌面/窗口可用性；目标麒麟发行版/版本/CPU 架构、桌面会话、字体、输入法、运行时及渲染依赖。既有“环境待验”不会阻止进入 A1，但不能算对应平台验收通过。

A0 完成以当次源码矩阵、本文契约、当前 API 兼容测试、完整的 TreeDataGrid 范围测试与双包消费者结果为依据。涉及其它控件的全 solution 验证留到集成阶段；并行工作区其它模块的变化不混入本轮 TreeDataGrid 基线结论。
