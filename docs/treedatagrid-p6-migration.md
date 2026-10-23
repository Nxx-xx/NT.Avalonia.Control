# TreeDataGrid P6 本地实现迁移说明

更新日期：2026-10-13。本文说明本轮 P6 实现相对已有预览实现的接入变化。当前产品版本仍为 `0.2.0-preview.2`；本地新增能力不代表已发布 `0.3.0-preview`、`0.9.0-rc` 或 `1.0.0`。完整验收状态见[执行记录](treedatagrid-p6-status.md)与[发行准备](treedatagrid-release-readiness.md)。

## 保持的接入结构与所有权

功能包为 `NT.AvaloniaControl.TreeDataGrid`，可选主题包为 `NT.AvaloniaControl.TreeDataGrid.Themes`，主题单向引用功能。已有主题 URI 与功能包自定义模板路径继续保留。统一 Demo 的 TreeDataGrid 场景位于 `samples/NT.AvaloniaControl.Demo/Pages/TreeDataGrid/`；`TreeDataGrid.Usage` 继续作为最小接入样例。

显式 `Source` 仍由应用拥有和释放；`ItemsSource` 适配 Source 由控件拥有，两个入口互斥。源、状态和事务集合继续遵循创建线程访问规则。`NodeId` 标识一次模型出现位置，`ColumnId` 标识列；重复引用同一模型的节点仍不能合并身份。

## 显示列索引与冻结区域

`Columns` 保留所有列。新增 `TreeDataGridColumn.IsVisible` 默认 `true`，隐藏不删除列、不改变 ColumnId，也不丢弃宽度或集合顺序。`VisibleColumns` 提供按当前显示顺序排列的只读映射。

| 位置 | 含义 | 迁移注意事项 |
| --- | --- | --- |
| `Columns` 的集合索引 | 包含隐藏列的列位置 | 使用集合索引修改、插入或移动列 |
| `ColumnIndex`、`GetColumnIndex(ColumnId)`、布局列索引 | 可见列显示索引 | 不能再直接索引 `Columns`；隐藏或不存在的列由 GetColumnIndex 返回 `-1` |
| 显示行索引 | 当前展开、排序和过滤后的投影位置 | 不能代替源路径或 NodeId |
| `IndexPath` / 行 `ModelIndex` | 原始源集合中的零基路径 | 排序或过滤不把它变成显示行号 |

例如，命中结果或已实现单元格的 `ColumnIndex` 应映射到可见列：

```csharp
TreeDataGridColumn displayedColumn = grid.VisibleColumns[hit.ColumnIndex];
int collectionIndex = grid.Columns.IndexOf(displayedColumn);
ColumnId identity = displayedColumn.Id;
```

`FrozenColumnCount` 现在表示前 N 个**可见列**，实际冻结数不超过可见列数量。隐藏冻结列后，后续可见列补入冻结区域；恢复列后重新按可见顺序确定边界。冻结区域不是一组固定 ColumnId。持久化布局或处理交互时，应保存身份并重新解析当前索引。

隐藏活动编辑列取消草稿且零写回；隐藏当前列按右侧邻近可见列、再左侧的顺序迁移。行身份和行选择保持，单元格选区按可见列收敛。全部列隐藏时当前单元格清空；恢复后可回到原行的首个可见列，原行失效时不会误定位其它行。

## 网格线与空内容模板

`GridLinesVisibility` 默认 `All`，另有 `None`、`Horizontal`、`Vertical`。默认 CellTheme 根据 `:horizontal-grid-lines` / `:vertical-grid-lines` 呈现边框；功能侧不强制设置本地 BorderThickness。应用提供的 CellTheme 继续决定自己的边框，选择 / 当前 / 验证指示边框与网格线是不同的视觉状态。

`EmptyContent` 默认“暂无数据”，`EmptyContentTemplate` 默认 `null`。空源及过滤后无显示行均使用空内容；恢复数据后隐藏该内容。空状态不表示节点加载中，现有同步加载契约保持。

默认主题中 `PART_EmptyMessage` 已由 `TextBlock` 改为 `ContentPresenter`，部件名保留。依赖旧具体类型的模板查询、选择器和测试需更新；例如将 `TextBlock#PART_EmptyMessage` 改为 `ContentPresenter#PART_EmptyMessage`。该部件不是控件必需模板部件。

应用复制默认模板时，应把固定字符串改成模板绑定：

```xml
<ContentPresenter Name="PART_EmptyMessage"
                  Content="{TemplateBinding EmptyContent}"
                  ContentTemplate="{TemplateBinding EmptyContentTemplate}"
                  IsVisible="False"
                  HorizontalAlignment="Center" VerticalAlignment="Center" />
```

对应空状态选择器继续使用 `:empty` 显示 presenter。自定义 IDataTemplate 应能处理 ContentPresenter 在内容变为 `null`、模板尚未清除时传入的 `null`。

## 键盘导航与列重排

当前位于层级列、无编辑且无 IME 预编辑时，普通 Right 展开节点或进入首个可见子节点；普通 Left 折叠节点或进入父节点。其它列与修饰键选区保持网格导航。模板内输入控件继续接收自己的按键。PageUp / PageDown 使用当前行坐标与实际视口高度，不再以固定行数估算一页；超高行仍至少推进一行。真实输入法选词行为须另行在实际系统验证。

列重排全局 `CanUserReorderColumns` 默认 `false`，单列 `CanUserReorder` 默认 `true`。显式启用后支持列头拖动，也可调用 `TryReorderColumn(ColumnId, targetDisplayIndex)`；该方法同样遵循全局和单列许可，目标参数是可见列的最终显示位置，无变化或无效落点返回 `false`。

首版允许冻结区内部或滚动区内部重排，用户及 TryReorderColumn 跨冻结边界落点拒绝。提交使用原 `Columns.Move`，包含隐藏列时先解析集合位置。应用直接修改 `Columns` 仍是应用的集合操作，不能把该路径等同于用户拖动请求的许可与边界检查。

`ColumnReordering` 可取消；`ColumnReordered` 表示移动完成。两类参数都提供 `ColumnId`、`OldCollectionIndex` / `NewCollectionIndex` 和 `OldDisplayIndex` / `NewDisplayIndex`。请求回调返回后重新核对 Source、投影 / 操作版本、列顺序、可见映射、冻结边界与许可，避免使用旧索引移动其它列。拖动采用阈值、插入提示与滚动区域边缘自动滚动；Esc、捕获丢失或在途失效取消。

重排保留同一 EditSession、草稿、宽度和当前项，跟随 ColumnId；不自动提交草稿。若矩形选区因顺序变化不再连续，沿用既有选区收敛规则。

## 身份与源路径定位

控件新增 `TryScrollIntoView(NodeId, expandAncestors, select)` 与 `TryScrollIntoView(IndexPath, expandAncestors, select)`。默认只定位，不展开祖先，也不修改选择。需要展开祖先或选择时明确指定参数。

目标缺失、被过滤、默认保持折叠、源处于延迟刷新 / 过期状态、无有效已附着视口时返回 `false`。全部列隐藏时仍可只定位行，`select=true` 无可见单元格可选时返回 `false`。回调后重新检查身份和路径，不能用失败定位后的旧显示索引选其它行。已经成功的祖先展开不会因后续失败被通用回滚。

## 嵌套绑定与编译绑定写回

嵌套 ReflectionBinding 的显示继续交由 Avalonia 处理各层通知；层级 Children / HasChildren / IsExpanded 绑定新增点分公共引用对象路径支持，处理对象或子集合替换、null 中间节点及退订。具体模式和限制见[绑定支持表](treedatagrid-binding-support.md)。

自动 ReflectionBinding writer 在开始编辑时捕获写入目标与属性，提交时重新解析并检查目标仍为同一引用、绑定仍有效、叶属性仍可写以及控件拥有的编辑仍有效。目标对象替换后拒绝把旧草稿写入新对象，失败零写回；取消旧草稿后可重新开始编辑当前目标。null 或不可写路径同样拒绝写入。

`CompiledBinding` / compiled XAML 支持显示，不等同于任意编译绑定能安全自动写回。写入可配置显式 ValueSetter 或 `SetValueAccessors<TModel,TValue>`；getter-only accessors 明确只读。转换先于校验，校验成功后调用 writer。显式或类型化 writer 仍由应用检查业务目标有效性，并遵守成功提交或失败前无副作用的契约；任意业务 setter 已发生的副作用不能通用回滚。

## 行与树节点移动

`CanUserMoveRows` 默认 `false`。启用后可从行号列拖动，或调用 `TryMoveRow(NodeId, targetNodeId, position)`；落点为 Before / After / Inside。范围为同一控件，跨控件移动尚未纳入本轮。

默认适配范围只包括显式 Source 可识别的受控集合：

| 集合与场景 | 默认结果 / 适配 |
| --- | --- |
| 同父、精确 `ObservableCollection<T>` 类型 | 按精确源索引调用 Move；保留重复对象各出现位置的 NodeId |
| 同父、sealed `TreeDataGridRowMoveCollection<T>` | 受控 Move，保留身份 |
| 跨父、两边均为 `TreeDataGridRowMoveCollection<T>` | 先准备存储，再同时提交两边存储并发送 Remove / Add 通知；不承诺旧 NodeId 不变 |
| `ObservableCollection<T>` 子类、普通 IList、只读集合、跨父普通 ObservableCollection | `Unsupported`；默认不修改集合 |
| `ItemsSource` 入口 | 内部适配集合不提供默认可写适配；通过 RowMoveRequested 由应用处理事务 |

不能依据“集合可写”推断跨父 Remove + Insert 具有事务保证。事务集合在通知内禁止重新修改参与的两个集合；某个观察者抛错仍执行余下处理器。排序或过滤存在时拒绝默认移动，应用可在请求中自行映射源位置。自身、后代、循环模型目标、无变化落点和超出层级限制的移动也拒绝。

控件先尝试提交活动编辑；校验失败拒绝移动并保留草稿。成功提交及 RowMoveRequested 返回后，重新核对 Source、版本、许可、节点身份和落点。请求可取消；应用设 `Handled=true` 时须提供真实 `Result`，并承担自己的写入一致性。直接调用 `Source.MoveRow` 只执行模型适配，不处理控件的草稿、选择、焦点或用户许可。

| 结果 | 应用处理 |
| --- | --- |
| `Rejected` / `Unsupported` | 默认移动未执行；检查落点、投影与适配能力 |
| `Cancelled` | 请求取消，不执行默认移动 |
| `Stale` | 操作已过期，重新从当前 Source 和身份建立新请求 |
| `ValidationFailed` | 保留草稿，修复或取消编辑后再操作 |
| `HandlerFailed` | 检查 Error；应用自定义处理器的既有副作用仍由应用负责 |
| `Committed` / `CommittedWithObserverError` | 存储已提交；后者记录提交后观察者 / 恢复错误，不能当成未提交失败直接重试 |

以 `IsCommitted` 判断提交，跨父以结果 `NodeId` 的实际身份为准；观察者又修改了数据或删除了结果节点时，该身份可能为空。不要凭同一模型引用、计划落点或旧源路径推断是哪次出现位置。控件清理失效旧身份，并对可见有效结果按既有策略恢复选择与当前项。

## 精确快照与最终冻结状态

本轮公开类型及成员签名以[当前 public-api.txt](public-api.txt)复核；默认值和必要模板部件另外依据源码声明、公共契约与 Headless / Themes 测试核对，不能只凭成员快照推断行为一致。`PublicApiTests` 保留精确成员比较和历史 API 回归；新增成员应同步快照，不能通过放宽比较掩盖删除。

当前快照固定的是这次本地实现的可审阅接口，不是 `0.9.0-rc` / `1.0.0` 的最终 API 或模板冻结声明。实际平台验收、候选版本决策、版本调整与公共包上传仍分别处理。
