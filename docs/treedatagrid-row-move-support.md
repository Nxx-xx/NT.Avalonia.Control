# TreeDataGrid 拖放与批量移动契约

更新日期：2026-10-23。适用 Avalonia 12.0.3 / net10.0，功能补齐计划 A4。版本保持 0.2.0-preview.2，不包含跨控件、外部载荷、Copy 或 Link。验收见 [A4 记录](treedatagrid-alignment-a4-status.md)。

## 拖放事件与输入

CanUserMoveRows 默认 false。RowDragHandle 默认 RowHeader，WholeRow 可从整行非交互区域启动，但整行模式仅用于行选择。Ctrl/Shift 等修饰键保留选择手势；按钮、文本框、Thumb 和嵌套可聚焦控件不启动拖动。自定义单元格模板若希望空白区域可拖动，应为命中区域设置透明背景，不能只绘制文字或边框。

```xml
<tg:TreeDataGrid CanUserMoveRows="True" RowDragHandle="WholeRow"
                 SelectionUnit="Row" SelectionMode="Multiple" />
```

RowDragStarted、RowDragOver、RowDrop 都是 Bubble 路由事件，可在控件或父级处理。参数提供 DataSource、规范化后的只读 Rows 快照、SourceVersion、Target 和 Before/After/Inside Position。允许效果使用 Avalonia DragDropEffects；默认行为只检查 Move 位，不执行 Copy/Link。

| 阶段 | Cancel / Handled / 不含 Move 时的行为 |
| --- | --- |
| RowDragStarted | 超过拖动阈值后通知；拒绝则结束本次手势并释放捕获 |
| RowDragOver | 当前落点无效，清除指示器，不进行该落点的悬停展开；后续悬停可重新允许 |
| RowDrop | 结束默认行为，不提交旧草稿、不进入移动请求；Handled 表示应用拥有此次操作 |

顺序为开始 → 零次或多次悬停 → 放下 → 提交旧草稿 → 复验 → 移动请求 → 复验 → 集合事务 → 身份与选择恢复 → 完成。候选拖动的普通单击延迟到释放时处理，避免按下行号即提交编辑或收缩多选。未达到阈值仍执行原单击选择/编辑逻辑；被回收的旧单元格不会按新身份执行延迟单击。

```csharp
grid.RowDrop += (_, e) =>
{
    if (!CanDrop(e.Rows, e.Target, e.Position)) e.Cancel = true;
    // 自行执行事务时先设置 e.Handled = true，控件不会再自动移动。
};
```

Esc、捕获丢失、源/列/模板/选择配置或移动许可变化使手势失效。边缘滚动和悬停展开继续使用已捕获的节点身份，回收起始行不更换移动对象。悬停不默认修改集合；展开绑定和应用展开回调仍按原契约执行。生命周期处理器抛错时清理捕获和计时器并传播异常，不重试默认写入。

## 集合能力与 ItemsSource

ItemsSource 擦除适配器通过内部明确能力协议访问原集合，不复制集合后冒充已移动，也不根据 IList.IsReadOnly 或某个同名 Move 方法推断事务性。

| 集合/操作 | 同父单节点 | 同父批量 | 跨父单个/批量 |
| --- | --- | --- | --- |
| 精确 ObservableCollection<T>，T 为引用类型 | 支持精确 Move，重复对象按出现位置保留身份 | 规范化后仍有多个节点则 Unsupported | Unsupported |
| sealed TreeDataGridRowMoveCollection<T> | 支持 | 支持一次存储事务 | 所有参与集合使用相同 T 时支持；可合并多个父集合 |
| List/IList、只读集合、ObservableCollection 子类、值类型的 ObservableCollection<T> | Unsupported | Unsupported | Unsupported |

该矩阵同时适用于显式 Source 和 ItemsSource；ItemsSource 的三种层级绑定仍按[绑定支持表](treedatagrid-binding-support.md)。共享原集合却具有不同父出现位置的写入目标拒绝默认跨父/批量移动，避免一个物理槽被当成两个独立槽。

默认排序/过滤期间返回 Rejected。应用可通过原 RowMoveRequested 或新 RowsMoveRequested 接管，使用 ModelIndex/模型引用映射原集合位置，不使用显示行号。Handled 必须提供 Result；已提交结果优先于后续取消或观察者异常，不再次执行默认事务。普通可写集合不能用未经验证的 Remove+Insert 承诺批量原子性。

## 批量规范化、事务与身份

```csharp
TreeDataGridRowsMoveResult result = grid.TryMoveRows(
    grid.State!.SelectedNodes.ToArray(), targetId, TreeDataGridRowDropPosition.Inside);
if (result.IsCommitted)
    foreach (var pair in result.IdentityMap)
        UseMovedIdentity(pair.Key, pair.Value);
```

输入先去重并按源 IndexPath 排序；祖先已选时排除冗余后代。同一模型的不同出现位置是不同节点，不能用模型相等性去重。整批验证节点、落点、真实索引、集合能力、树循环和最大深度后才准备存储。无变化、自身、后代及无效身份拒绝；某一集合不支持时整批不写。

事务集合先准备所有受影响集合的存储和通知，再统一替换存储，最后发送带精确索引的 Remove/Add/Move 通知。任何通知观察者看到的都是整批提交后的存储；通知期间禁止再次修改这些参与集合。通知处理器异常后继续其它处理器，Source 合并为一次投影。批量同父重排只生成所选出现位置的 Move 通知，保留未移动节点及重复对象的身份。

同父保留 NodeId；跨父分配新身份，IdentityMap 包含存活的移动根与后代。映射在首次提交投影中、用户投影观察者之前捕获，不通过重复模型引用猜测；后续删除的映射被移除，Source 已释放时返回空映射。未配置模型展开绑定时保留被移动子树的展开状态；已配置时模型选择器仍为权威值。

控件按映射恢复行选择、当前项和锚点，属于提交后的强制身份修复；当前项隐藏时回到最近可见祖先，单元格矩形按现有规则收敛。应用在提交期间更换源、列或选择配置时，不向旧控件状态恢复。应用可在完成事件中进一步改选。

TryMoveRows 使用独立的 RowsMoveRequested / RowsMoveCompleted，每批一次，不为每个成员重复发送单节点事件。原 TryMoveRow 与 RowMoveRequested / Completed 继续保留；直接程序调用不会合成拖放事件。Source.MoveRows 只处理模型事务，不处理控件草稿、焦点或选择。

TreeDataGridRowMoveCollection.MoveMany(indices, insertionIndex) 是集合级入口：索引唯一，按原集合顺序移动，insertionIndex 指移除这些槽之后的位置。它不进行树合法性、排序过滤和控件状态检查；树表应用通常应调用 TryMoveRows。

## 提交与错误边界

RowDrop 取消在编辑转换/验证/writer 之前。原有移动请求取消保持旧时序，可能已成功提交草稿。转换/验证失败不移动集合；writer 的任意副作用不承诺回滚。

结果沿用 Rejected、Cancelled、Stale、Unsupported、ValidationFailed、HandlerFailed、Committed、CommittedWithObserverError。存储已经提交后，集合通知、投影、身份恢复或完成观察者异常均报告已提交并携带 Error，不能当作未提交重试。完成观察者继续逐个执行；任意业务回调额外修改其它数据不在存储事务的回滚范围。

自动验证覆盖模型事务、真实 Headless 指针路由、默认/应用模板、编译 XAML 和独立包消费。原生程序化检查及离屏图像不等同于物理拖动、IME、多 DPI、真实读屏或麒麟验收；这些继续按 A6 执行。
