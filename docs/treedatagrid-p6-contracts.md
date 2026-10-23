# TreeDataGrid P6 公共契约

日期：2026-10-13。执行依据为[后续完善计划](treedatagrid-next-development-plan.md)。本文件固定 P6-0 的共用规则；实现与验收状态见[阶段记录](treedatagrid-p6-status.md)，不能由这里的 API 清单推断功能已经通过验收。

## C1：列身份、集合与显示索引

- `Columns` 保留全部列；隐藏不会移除列，不创建新的 `ColumnId`，也不丢弃宽度或集合顺序。
- `TreeDataGridColumn.IsVisible` 默认 `true`。控件公开 `VisibleColumns`，类型为 `IReadOnlyList<TreeDataGridColumn>`；顺序是 `Columns` 中可见列的顺序。
- 列头、单元格、布局、命中、键盘、搜索、滚动定位与无障碍共用该映射。显示入口的 `ColumnIndex` 是可见列索引，不能直接当作 `Columns` 的集合索引。
- 源 `IndexPath`、投影行索引、列集合索引和可见列索引分别表示不同位置；持久身份由 `NodeId` / `ColumnId` 表示。
- `FrozenColumnCount` 表示前 N 个可见列；实际冻结数为请求数与可见列数的较小值。隐藏冻结列后由后续可见列补入冻结区域。
- 列重排事件必须包含 `ColumnId`、旧/新集合索引和旧/新可见列索引；隐藏列没有有效显示位置，不可假借其它列的索引。

## C2：编辑、隐藏与移动

- 列重排保留同一编辑会话，草稿和当前单元格跟随 `ColumnId`；不因索引变化重新写入模型。
- 隐藏活动编辑列取消草稿，取消路径不调用 writer。隐藏当前列先寻找右侧邻近可见列，再寻找左侧可见列；行身份与行选择保持。
- 单元格选区按可见列收敛。全部列隐藏时清空当前单元格；内部保留当前行焦点意图，恢复列后定位到该行的首个可见列。若行已失效则不重定向到另一行。
- 行移动先尝试提交活动编辑；校验失败时拒绝移动。提交、请求回调返回后重新校验 `Source`、操作版本、节点身份和目标位置，然后才修改集合。
- 验证与取消应发生在写入前。外部 writer 与应用移动处理器负责“成功提交，或失败前不改模型”的契约；控件不承诺回滚任意业务 setter 已产生的副作用。

## C3：行移动范围、身份与事务

- 首版覆盖同一控件的同父重排及跨父移动，跨控件移动另列增强。
- 同父受支持集合的 `Move` 保留出现位置的 `NodeId`，重复模型对象的不同出现位置仍为不同节点。
- 跨父移动不承诺旧 `NodeId` 保持不变。实现必须按实际产生的身份清理或恢复选择和当前项，不能把旧源路径或旧投影行号当作新身份。
- before / after / inside 落点均以节点身份和源位置验证；不能移动到自身或后代，不能形成树循环。
- 排序或过滤存在时默认自动移动拒绝执行；应用可通过请求事件自行映射与处理。
- 跨父默认写入只开放给已验证的事务/补偿适配器。仅仅可写的集合不构成事务保证，其它集合通过请求交由应用处理。
- 默认适配器分别报告未提交失败、补偿结果与提交后观察者异常；删除/换源、集合变更或模板变更使在途操作失效时取消，禁止使用过期索引移动其它节点。
- 保持创建线程访问、数据源所有权、回调重入与退订规则，不引入跨线程访问源模型。

## P6-A API 与默认值

| 范围 | API | 默认值或规则 |
| --- | --- | --- |
| A1 | `TreeDataGridColumn.IsVisible` | `true` |
| A1 | `TreeDataGrid.VisibleColumns` | 只读可见列映射 |
| A2 | `TreeDataGridGridLinesVisibility` | `None` / `Horizontal` / `Vertical` / `All` |
| A2 | `TreeDataGrid.GridLinesVisibility` | `All`，保持当前水平及垂直边框 |
| A2 | `TreeDataGrid.EmptyContent` | `"暂无数据"` |
| A2 | `TreeDataGrid.EmptyContentTemplate` | `null` |
| A4 | `CanUserReorderColumns` | `false` |
| A4 | `TreeDataGridColumn.CanUserReorder` | `true` |
| A4 | `TryReorderColumn(ColumnId, int targetDisplayIndex)` | 同区且许可有效，使用原 Columns.Move |
| A4 | `ColumnReordering` / `ColumnReordered` | 请求可取消；包含 ColumnId、旧/新集合索引、旧/新显示索引 |

A2 的内容由主题/模板绑定呈现；功能侧不硬编码外观，也不以强制本地值覆盖应用 `CellTheme`。空源与过滤为空均可呈现空内容；已有同步加载不因该入口自动产生异步加载状态。

A3 在层级列且没有编辑/IME 预编辑时处理普通左右键；模板内控件保有自己的输入。PageUp/PageDown 根据实际行坐标和视口高度定位，保留可见列并夹紧到边界；单行超高时仍至少推进一行。

A4 首版允许冻结区内部和滚动区内部重排，跨冻结边界落点无效。提交使用原 `Columns.Move`，拖动阈值、排序点击、调宽、插入标记、边缘滚动与取消须分别验收。

## P6-B 与质量入口

- `TryScrollIntoView(NodeId/IndexPath, expandAncestors = false, select = false)` 按身份或源路径定位；不会借用显示行号。
- `SetValueAccessors<TModel,TValue>` 为 compiled 显示提供显式暂存写回。自动 ReflectionBinding 写回捕获开始编辑时的目标引用；中间对象或绑定更换后拒绝旧草稿写入，取消后可以重新开始。
- `CanUserMoveRows = false`；启用后 `TryMoveRow` 返回明确结果。同父精确 ObservableCollection 的 Move 或受控集合保留 NodeId；跨父只有 `TreeDataGridRowMoveCollection<T>` 的双存储原子提交适配，返回新身份并恢复合理的选择。
- `RowMoveRequested` 按注册顺序路由，在 Cancel 或 Handled 后停止，应用处理器须提供 Result。`RowMoveCompleted` 通知全部观察者并聚合错误；回调期间拒绝重入。提交后观察者失败仍报告已提交，不能据错误再次盲目移动。
- ItemsSource 的集合擦除适配器没有默认移动路径，使用请求处理器；默认 typed 适配通过显式 Source 接入。
- `AddRange` 仅新增于受控集合。枚举/分配/验证完成后替换存储，再发一次范围 Add；准备失败不写入。存储通知期间 IsRefreshDeferred 为 true，Source.Refresh 排队并在通知完成后收敛，避免重复应用索引变更。
- Source、列集合、冻结边界与模板在请求期间变化会使默认移动失效；模板属性一经更换即失效，不等待下一次 ApplyTemplate。
- 专用自动化 peer 提供固定版本公开的选择、值、展开、滚动与逻辑身份定位；原生 Grid/Table pattern 和真实读屏支持仍未完成。默认值与模板部件由契约测试及源码声明验证，签名快照不能代替行为冻结。

## 契约验证场景

| 编号 | 场景 |
| --- | --- |
| C1/A1 | 隐藏/恢复冻结列、隐藏列前后的显示索引、百列横向虚拟化、Source 与 ItemsSource 两入口 |
| C2/A1 | 隐藏当前/编辑列、零写回、右再左迁移、全部隐藏与恢复、选区收敛、行被删除后恢复 |
| A2 | 四种网格线、亮暗主题、冻结分界、空源与过滤为空、重新出现数据、自定义 CellTheme/功能包消费者 |
| A3 | 根/叶/展开/折叠/过滤节点、混合和自动行高、超高行、模板输入与预编辑路由 |
| C1/C2/A4 | 隐藏列参与集合映射、同区重排、当前项/草稿跟随身份、途中换源/集合变化/模板变化与取消 |
| C2/C3/B4 | 提交失败拒绝移动、提交回调换源、重复模型、只读集合、循环目标、事务失败与补偿 |

Headless 或程序化原生输入只覆盖相应路由行为；真实中文输入法、物理拖动、多 DPI、屏幕阅读器与麒麟分别记录待验证。
