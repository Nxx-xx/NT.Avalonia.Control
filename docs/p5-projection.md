# P5 排序与过滤契约

`TreeDataGridSource` 提供增量的 `virtual` 接口；已有自定义 Source 子类不需要增加 override。内置平面和树形泛型 Source 实现完整排序过滤。自定义 Source 的默认 `Sort`、非空 `SetFilter` 抛出 `NotSupportedException`，`ClearSort` 和 `SetFilter(null)` 是空操作。

```csharp
source.Sort(nameColumn.Id,
    (left, right) => StringComparer.Ordinal.Compare(
        ((Item)left).Name, ((Item)right).Name),
    TreeDataGridSortDirection.Ascending);
source.Sort(nameColumn.Id, comparison, TreeDataGridSortDirection.Descending);
source.ClearSort();

source.SetFilter(model => ((Item)model).Name.Contains(searchText));
source.Refresh(); // 捕获变量或不通知的模型属性改变后重新计算
source.SetFilter(null);
```

`SortColumn`、`SortDirection`、`Filter` 为最近一次成功发布的投影配置。未排序时列为 `null`，方向为 `None`。`Sort` 只接受正数 ColumnId、非空比较器以及 Ascending/Descending；清除通过 `ClearSort()` 完成。Source 不拥有列集合，程序传入的 ColumnId 由调用者负责对应实际列。未改变的清除操作、重复设置同一个过滤委托不产生通知；闭包条件改变请调用 `Refresh()`。

## 排序与身份

排序仅改变投影遍历顺序，不修改用户集合。树只在同一父节点的子集合内排序，子树仍连续排列。比较结果为零时维持当前源集合的 occurrence 顺序，升降序均稳定；降序交换比较器的左右参数，支持比较结果为 `int.MinValue`。比较器必须满足一致的排序关系，异常直接传播。清除排序恢复当前源集合顺序，包括排序期间发生的 Move。

`NodeId` 和 `ModelIndex` 继续表示源中的节点 occurrence 和路径；视图位置仅由 `GetViewIndex` 表示。重复模型在不同位置有不同 NodeId。Move 保留身份，Replace/Reset 沿 P1 规则产生新身份。列排序不会改变展开位、模型绑定或共享模型展开事件的源顺序。

## 过滤与树

谓词按 occurrence 计算，包含折叠节点及被父节点拒绝的后代；同一对象出现两次会计算两次。每次投影对每个节点计算一次。拒绝父节点时，其整支不显示，即使后代自身匹配；不会自动保留匹配节点的祖先。保留的父节点也仍须展开才能显示匹配的孩子。

过滤隐藏的节点仍能通过 `TryGetRow` / `TryGetNodeId` 查询，`GetViewIndex` 返回 `-1`。隐藏节点不会出现在 `RemovedNodes`；只有模型图实际删除、替换或重置的旧身份会出现。清除过滤恢复节点原有展开状态。`HasChildren` 描述源子节点或懒加载提示，不随孩子被过滤而变化。

## 提交、事件和异常

排序/过滤先生成完整候选映射，再原子替换所有公开映射和配置、增加 Version，最后通知 `ProjectionChanged`。直接排序和清除使用 `SortChanged`，直接设置或清除过滤使用 `FilterChanged`；这两个枚举成员追加在旧成员之后。集合和属性变化自动重投影，保持 `CollectionChanged` / `ModelChanged` 原因；显式 Refresh 使用 `Refresh`。

嵌套 `DeferRefresh` 中可以多次调整配置、修改模型，公开视图和公开配置保持旧值，最外层关闭时只发布最终配置一次，外部混合批次原因为 `Refresh`。配置调用会先评估候选以尽早发现错误，scope 结束会针对最终模型再次评估。新增集合订阅继续即时维护。

参数、比较器或谓词失败时，最近成功的公开配置、映射、Version 和事件历史保持不变。defer 最终评估失败会丢弃未发布配置，保留先前成功配置；修正数据后调用 Refresh，或清除导致失败的排序/过滤，再恢复投影。

实际集合或模型更改不能回滚。自动投影失败时，内置 Source 仍保存已协调的内部节点身份和订阅，公开结构保留上一成功快照；`IsRefreshDeferred` 为 true，直到恢复投影成功。因此旧行可用于显示，但不能开始或提交编辑。行快照持有原模型引用，模型属性本身并非历史快照。最终恢复通知会汇总公开快照以来实际移除的身份。

比较器和谓词必须为无副作用的同步函数，执行时禁止 Source 修改（排序、过滤、刷新、展开、开始/结束 defer、Dispose）和模型/集合通知。尝试重入会抛出 `InvalidOperationException`；回调吞掉该异常也不能提交候选投影。ObservableCollection 通常在通知前已完成修改，这种实际副作用会暂存并在后续恢复时协调；无法检测不通知的模型或集合副作用，调用方仍须遵守纯函数约定。所有计算和通知保持创建线程约束。

ProjectionChanged 监听器失败发生在成功提交之后，不回滚已发布配置。其他监听器和通知期间排队的模型更改仍按 P1 约定处理，再重新抛出监听器异常。通知回调中不可重入调整排序过滤配置；可以继续沿 P1 约定更改模型集合。

## 选择和编辑联动

程序排序可在活动编辑期间调用。只要节点仍可见，编辑和当前单元格按 NodeId 保留；提交仍写入原模型对象。提交顺序为验证 → 在 defer 内写回 → 结束编辑 → 发布重排/过滤投影。新值改变排序位置或使行不再匹配时，不会按旧视图索引写入另一行。

过滤使编辑目标隐藏时取消尚未提交的编辑。隐藏选择保持身份，过滤清除后恢复可见；当前单元格按现有状态模型规则寻找可见祖先，无祖先则清空。列头发起排序由控件层先尝试提交编辑；校验失败时不会排序。列头三态、列级开关和比较器元数据见控件实现及 P4/P5 集成测试。

## 列头交互

`grid.SortByColumn(column.Id, direction)` 使用列的 `Comparison` 比较模型；展开列也读取内层的比较器。未提供比较器时按列绑定或 `ValueGetter` 读取值，再用 `Comparer.Default` 比较。复杂值须提供一致的比较器。程序清除可传 `TreeDataGridSortDirection.None`，或者调用 `source.ClearSort()`。

`CanUserSortColumns` 和列的 `CanUserSort` 控制列头鼠标/键盘操作，不限制显式程序排序。普通点按、Enter 或空格在升降序间切换；`IsThreeStateSort=true` 时按升序 → 降序 → 清除循环。拖动调宽手柄不触发排序。列头的 `SortDirection` 和 `SortIndicator` 为只读 Avalonia 属性，默认主题展示 ▲/▼，自定义模板可以绑定它们或使用 `:sort-ascending` / `:sort-descending` / `:sortable` 伪类。

列头排序前提交活动编辑，转换/验证失败返回 false、保留编辑草稿和原排序。编辑回调若替换数据源或移除目标列，本次排序终止。Source 与列集合相互独立，移除正在排序的列不会自动清除 Source 排序；需要恢复源顺序时显式 ClearSort。

## 边界与验证

本阶段实现单列排序和同步谓词过滤；多字段排序可以放入一个自定义比较器。没有多列排序描述集合、自动保留祖先、后台过滤或服务端分页。投影为 O(n)，排序额外为各同级集合的 O(k log k)，稳定归并排序使用 O(k) 缓冲。所有节点仍按 P1/P3 急切索引，上限 256 层。每次模型通知仍可能完整重建图；批量调用应使用 DeferRefresh 合并发布，未宣称增量排序过滤。

`ProjectionTests` 的 21 项测试覆盖稳定升降序、极值比较器、树同级排序、源路径和重复模型身份、逐节点过滤、属性/集合/闭包刷新、失败保留及恢复、嵌套批处理、回调重入、移除身份、线程/Dispose、256 层边界、600 步随机树操作和编辑写回联动。2026-10-12 Debug 构建为 0 警告、0 错误；直接 xUnit runner 合并运行这些测试与既有 Source/Expansion/State/TreeStateIntegration 专项共 81 项，全部通过（0.306 秒）。统一 Release 和 UI 集成结果由 P4/P5 验收记录汇总。
