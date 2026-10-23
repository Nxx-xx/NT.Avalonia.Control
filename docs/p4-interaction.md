# P4 选择、导航与事务编辑

P4 的交互建立在稳定的 `NodeId` 和 `ColumnId` 上。行与单元格容器只负责显示；选择、当前单元格、范围锚点和编辑草稿保存在 `TreeDataGridState`，滚动回收不会成为提交行为。本文描述本项目已经实现的契约，不表示已逐项复刻许可参考包的默认行为。

## 选择和身份

主控件的 `SelectionUnit` 默认为 `Row`，可以切换为 `Cell`；`SelectionMode` 默认为 `Single`，设置 `Multiple` 后启用范围和多选。切换选择单位或模式会清空原选择。单独创建的 `TreeDataGridState` 为保留已有 `Select(node, additive: true)` 行为，模式默认 `Multiple`；控件创建的 State 使用控件的模式。

`grid.State` 是随 Source 创建的只读状态入口，可调用 `Select`、`ToggleSelection`、`SelectRowRange`、`SelectCell`、`SelectCellRange`、`ClearSelection` 和 `SetCurrentCell`。独立使用 State 时，用 `SetColumns` 同步列顺序；列身份必须为正数且唯一。`CurrentCell`、`SelectionAnchor` 均为 `CellAddress`，不会记录旧视图索引。单元格矩形存储原 NodeId 集合与 ColumnId 集合的乘积。

排序后保留原身份。如果这些可见身份不再构成一个连续矩形，则收敛到仍可见的 CurrentCell 单格；不会按照新端点索引加入未选择的模型。过滤或折叠隐藏的选择可保留，真正删除的节点从选择中移除；隐藏的 CurrentCell 向上寻找最近可见祖先，找不到则清空。列移除会清理对应的选择、锚点和编辑。

`SelectionChanged` 在 State 和控件上均可订阅；`Changed` 还包含当前单元格和编辑状态变化。使用 `using (state.DeferSelectionNotifications())` 将多次选择修改合成一次通知。选择结果对外提供快照，不能通过修改返回的集合绕过通知。

## 输入行为

| 输入 | 行为 |
| --- | --- |
| 鼠标左键 | 选择目标；活动编辑需先成功提交 |
| Ctrl + 左键（多行） | 切换目标行的选择 |
| Shift + 左键、Shift + 方向键（多选） | 从锚点扩展行范围或单矩形单元格范围 |
| 方向键、Home/End、PageUp/PageDown | 移动当前单元格并滚入视口；Ctrl + Home/End 到首末行 |
| Ctrl + A（多选） | 选择全部可见行或全部可见单元格矩形 |
| F2、双击 | 默认开始编辑；`EditTriggers` 可以关闭或配置单击 |
| Enter、Tab / Shift + Tab | 提交活动编辑，成功后向下或跨列导航；错误时留在原编辑 |
| Esc | 取消活动编辑，保留模型原值 |
| Space（复选框单元格） | 按二态或三态循环暂存值，执行相同的转换、验证、提交事务 |

文本编辑器内的方向键保留光标编辑行为。列头的 Enter/Space 交给排序处理，展开按钮调用 Source 的展开命令。`SearchText(prefix)` 从当前行之后循环搜索可见行，使用当前区域规则忽略大小写匹配前缀；文本列默认使用取值结果，其他列设置 `TextSearchValueSelector` 后参与。非编辑状态键入文本也会搜索，一秒内输入合成前缀。搜索会遍历投影中的模型值，不承诺大数据搜索索引性能。

## 列编辑配置

```csharp
var amount = new TreeDataGridTextColumn
{
    Header = "数量",
    ValueGetter = model => ((Item)model).Amount,
    ValueSetter = (model, value) => ((Item)model).Amount = (int)value!,
    ValueType = typeof(int),
    ValueValidator = value => (int)value! >= 0 ? null : "数量不能为负数",
};
grid.Columns.Add(amount);
grid.BeginEdit(new CellAddress(source.Rows[0].Id, amount.Id));
grid.State!.EditSession!.Value = "12"; // 草稿；尚未写入 Item
bool committed = grid.CommitEdit();
```

`ValueSetter` 是明确的模型写回入口；缺少写入能力的列不能开始编辑。`IsReadOnly` 可在控件或列上设置，切到只读会取消正在进行的编辑。树列使用 InnerColumn 的取值、转换、验证和写入配置，同时尊重包装列的只读限制。

显示 Binding 的读取优先于 ValueGetter。无转换器、显式源、ElementName 或 RelativeSource 的简单 DataContext `ReflectionBinding.Path` 支持公共属性链读取和公共 setter 延迟写入；不会把编辑器 TwoWay 绑定到模型。复杂 Binding 的显示仍由 Avalonia 处理，编辑需要显式 `ValueSetter`；建议同时显式提供 `ValueType` 或 `ValueConverter`。索引器、私有 setter 等不作为自动写入路径。

提交首先调用 `ValueConverter`（若配置）；否则按 `ValueType`、绑定属性类型、复选框二/三态类型或原值类型推断转换。标准转换使用当前区域，支持可空类型、枚举；空字符串可转为 nullable 的 null。转换成功后运行 `ValueValidator`，返回 null 表示通过，其他字符串成为错误说明。`ValueConverter` 和验证器都不应写模型。

`TreeDataGridTemplateColumn.CellEditingTemplate` 的 Content/DataContext 为 `TreeDataGridEditSession`，模板应将编辑值绑定到其 `Value`，而非模型属性，例如 `{Binding Value, Mode=TwoWay}`。编辑会话还提供 Cell、OriginalValue、ValidationError 和 PropertyChanged。内置文本/复选框编辑器及自定义模板均会接收程序对草稿 Value 的更新。

## 提交、回收与生命周期

成功提交执行一次模型写回，再在 Source 的内部 `DeferRefresh` 范围内合并刷新，使排序、过滤和投影通知观察已写入的模型。验证失败时保留会话和草稿。提交前会再次校验源、节点、模型引用和可见性，防止验证器或错误通知回调删除节点、更换 Source、移除列后仍写旧对象。调用者打开的外层 `DeferRefresh` 必须先结束才能 BeginEdit/CommitEdit，避免根据过期投影写入。

ValueSetter 必须成功完成，或者在抛异常前保持模型未变；控件无法回滚用户 setter 已执行的外部副作用。控件捕获常见转换/验证/属性写入异常，将可报告的错误保留在 ValidationError；任意业务异常仍应由应用按自己的事务约定处理。

滚动、亮暗主题和模板替换会释放旧编辑器的绑定和事件，再将同一会话重挂到新容器。卸载解除控件与 State 的 Source 订阅，保留草稿；重新挂载按最新投影清理期间失效的身份。真正更换 Source 会释放旧 State 并取消旧编辑。应用仍拥有 Source，控件不会 Dispose 它。

排序后的目标仍可见时，程序 `Source.Sort` 保留原编辑身份与草稿；过滤或折叠令目标不可见则取消编辑。列头触发排序前先提交，验证失败会阻止排序。提交导致最后一行被过滤、writer 更换 Source 或卸载控件都有对应安全处理。

## 主题与输入法

单元格提供 `:selected`、`:current`、`:editing`、`:invalid` 伪类；控件提供 `:editing`。`TreeDataGridCellPresenter.ValidationError` 和工具提示可显示错误说明。默认编辑器名称为 `PART_Editor`，可由应用主题设置文本框/复选框视觉；CellTheme 的主体模板仍需显示 Content。主题仅负责视觉，功能程序集不引用资源 URI。

编辑器代理 Avalonia `TextInputMethodClient` 的预编辑通知：组合期间 Enter/Esc/Tab 不被网格拦截成提交、取消或导航；组合结束后恢复正常行为。自动测试覆盖预编辑路由及中文结果提交，Windows 演示覆盖普通键盘编辑。尚未用真实中文输入法进行原生组合、候选窗和焦点切换验收；也没有可用麒麟机器，不能据此声明麒麟或真实 IME 已验证。

## 验证范围

Core 专项覆盖选择批量通知、单选、排序后的矩形身份收敛、无效列顺序原子拒绝、草稿与单次写回、验证回调删除目标、提交中选择变化通知和移除列。Headless 覆盖真实指针 Ctrl/Shift 选择、键盘编辑与校验、简单 TwoWay Binding 延迟写入、编辑回收/模板/卸载恢复、隐藏末行后的 Enter/Tab、预编辑保护、搜索、程序草稿同步以及回调中更换 Source/清列/卸载。

主题及 Windows 演示另覆盖三态写回、自定义编辑模板、状态视觉、列头键盘/鼠标排序、校验拒绝排序和投影后编辑身份。列重排、行拖放、完整无障碍及剪贴板行为属于后续阶段；本阶段没有完成这些功能。
