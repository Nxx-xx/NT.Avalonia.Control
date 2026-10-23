# P1 呈现与布局契约

此模块是 P1 技术原型。固定依赖为 Avalonia 12.0.3 / .NET 10，公开类型位于 `NT.AvaloniaControl.TreeDataGrid`。功能程序集没有样式资源 URI 或对样式程序集的引用。

## 列与主控件

```csharp
var grid = new NT.AvaloniaControl.TreeDataGrid.TreeDataGrid { Source = source, FrozenColumnCount = 1 };
grid.Columns.Add(new TreeDataGridTextColumn
{
    Header = "名称",
    Width = new Avalonia.Controls.GridLength(180),
    ValueGetter = model => ((Item)model).Name
});
// 或 Binding = new Avalonia.Data.ReflectionBinding("Name")。
// Avalonia 12 的绑定公共基类为 BindingBase，不使用 Avalonia 11 的 IBinding。
```

每个列实例自动分配稳定 `ColumnId`；改变宽度、列位置不改变身份。`Columns` 是控件自己的可观察集合。`Source` 由应用所有，控件替换源或卸载时只解除自己的订阅，不调用 `Dispose`。应用在不再使用源时负责释放其集合和模型订阅。

`RowHeight` 默认 28，必须为有限正数。`RowHeightSelector` 可提供基于行快照的可变高度；返回 NaN、无穷或非正数时回退为默认行高。调用方改变选择器的外部状态后可调用 `Source.Refresh()` 请求重新计算。

像素宽、Auto 估算和 Star 宽度均纳入同一几何快照。Auto 在 P1 明确采用 `EstimatedAutoWidth`（默认 120）；不枚举全部单元格测量内容。Star 按剩余视口和权重分配后限制 Min/Max，限制后的剩余空间不再二次分配。完整 Auto 测量与受限 Star 分配归 P2。

## 必需和可选模板部件

| 名称 | 类型 | 要求 |
| --- | --- | --- |
| PART_ScrollViewer | Avalonia.Controls.ScrollViewer | 必需，Content 必须承载行 Presenter；横纵滚动条建议 Auto |
| PART_RowsPresenter | TreeDataGridRowsPresenter | 必需；实现 ILogicalScrollable，以 DIP 像素为滚动单位 |
| PART_ColumnHeadersPresenter | TreeDataGridColumnHeadersPresenter | 可选；由主控件注入列并共用行几何和横向偏移 |

缺失必需部件或类型错误时，应用模板抛出包含部件名和预期类型的 `InvalidOperationException`。主控件提供 `:empty` 伪类。列、数据源、冻结数和高度参数由主控件注入 Presenter，不应在模板中重复创建独立状态。

## 几何与回收

`TreeDataGridLayout` 是共享几何快照，包含列位置、冻结区宽度和行高前缀和；可见首行定位为 O(log n)。`GetCellBounds`、列头、行排列和 `HitTest` 使用相同的横向偏移规则。滚动单元格在冻结区边界裁剪；冻结区命中具有优先级。返回的单元格矩形相对行 Presenter 的视口，可能部分位于裁剪区外。

`TreeDataGridRowsPresenter` 只实例化可见行加上下各一行预取，另保留最多两个空闲容器。滚动回收已有行容器；当前行内部文本单元格在重新准备时重建，P2 将进一步优化单元格回收。P1 不实现横向单元格虚拟化。100 列的每个可见行仍包含 100 个文本控件。

可通过 `RealizedRowCount`、`RetainedRowCount`、`CreatedRowCount`、`RecycledRowCount` 检查实际回收。`RowPrepared` 在行完成数据设置后触发，`RowClearing` 在旧 DataContext 和绑定清理前触发。回收时释放 BindingBase 创建的绑定并清除 DataContext；容器不得保存选择或编辑状态。源投影、列集合或行高参数变化后原型重建几何并重新准备可见行。

控件卸载会断开源/列事件，清理全部可视容器。重新挂载时以源最新快照重建。修改模板清理旧 Presenter，再接入新 Presenter。`ScrollIntoView(NodeId)` 按稳定节点身份定位可见行，隐藏/不存在的节点返回 false，不隐式展开祖先。有限视口是虚拟化前提；无限测量约束不会触发创建所有数据行。

## 验证与后续范围

Headless 测试使用 Fluent 的 ScrollViewer 和应用自定义 TreeDataGrid 模板，仅引用功能程序集。覆盖十万行视口数量上界、长距离滚动复用、真实 ScrollViewer 偏移、可变行高、100 列冻结裁剪与命中、宽度约束、绑定重新准备、源替换/卸载/重挂、列移动身份和缺失部件诊断。独立测试程序集还编译真实 AXAML 列定义，验证 BindingBase 适配与代码 ValueGetter 对中文、英文、null 的显示一致。

投影/列变化后、下一次布局执行前调用 `HitTestCell` 或 `ScrollIntoView` 会同步更新脏几何，避免用旧行号访问新投影。`RowClearing` 回调可替换 Source：先从回收索引移除旧容器，再通知和清理；呈现循环检测版本并停止使用旧源。

P2 尚需完整单元格/行模板、内容 Auto 测量、受限 Star 分配、横向虚拟化评估、稳定滚动锚点及更细粒度的增量失效。P3 接入树展开器；P4 接入选择编辑键盘；P6 补齐调宽、冻结交互、重排拖放和无障碍。当前几何与数据接口不代表这些交互已经实现。
