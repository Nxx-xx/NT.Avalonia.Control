# P2 呈现与 P3 展开器 UI

P2 在固定的 Avalonia 12.0.3 / .NET 10 契约上实现可组合的列、受视口约束的行与单元格回收、测量列宽、可变行高、滚动锚点及列头调宽。P3 展开器统一调用 Source 的展开命令，事件、取消和按需加载语义由 Core 负责。功能程序集没有内置主题 URI；可单独使用应用的模板和主题。

## 列与主题入口

| 列 | 显示/配置 |
| --- | --- |
| TreeDataGridTextColumn | ValueGetter 或 BindingBase；StringFormat 支持复合格式及标准格式字符串；TextWrapping 控制换行 |
| TreeDataGridCheckBoxColumn | ValueGetter 或 BindingBase；IsThreeState 支持 nullable bool；显示控件不接收点击、不写回模型 |
| TreeDataGridTemplateColumn | CellTemplate 以值为 DataContext（缺省值为模型）；CellEditingTemplate 预留编辑模板入口 |
| TreeDataGridRowHeaderColumn | 默认显示当前视图行号，从 1 开始；可用 ValueGetter 覆盖 |
| TreeDataGridHierarchicalExpanderColumn | InnerColumn 包装上述任一列；Indent 默认 16；按行 Depth 缩进，使用 HasChildren/IsExpanded 展示展开状态 |

展开按钮具有 `tree-data-grid-expander` class；点击按稳定 NodeId 调用 `Source.SetExpanded`，支持取消、懒加载、模型展开状态绑定。叶子保留缩进占位但不接受展开输入。当前未实现编辑手势、编辑器焦点管理、提交/取消、键盘选择等 P4 功能，编辑模板存在不代表编辑已实现。

公开的 `TreeDataGridCellPresenter` 和 `TreeDataGridColumnHeader` 继承 ContentControl；内置最小默认模板仅组合功能部件，应用可以用 ControlTheme 改变模板、边框、字体和间距。主控件 `CellTheme`、`ColumnHeaderTheme` 为可选的覆盖入口。单元格 Content 是列对应的显示控件，Row、Column、ColumnIndex、Value 可用于诊断。列头模板中的可选 `PART_ResizeThumb` 启用指针调宽；省略该部件即无拖动把手。主控件的三个 P1 模板部件约定继续有效。

`CanUserResizeColumns` 在本项目默认 true，并与列的 `CanUserResize` 共同控制拖动把手。调宽将 Auto/Star 转换为像素宽并限制 MinWidth/MaxWidth；同一布局帧内的多个指针变化累计，避免快拖丢失增量。官方公开文档默认值存在差异，尚未运行许可参考包逐项对照，因此此默认是本项目明确约定。

Columns 中同一列实例只允许出现一次；插入或替换重复 ColumnId 在修改集合前拒绝。移动列保持 ColumnId。包装列 InnerColumn 的变更由主控件统一订阅；卸载解除订阅。

## 测量和虚拟化

像素列直接应用 Min/Max。Star 列求满足 `sum(clamp(weight × multiplier, Min, Max))` 的单调倍率，限制后的剩余空间自动重新分配且与列序无关。视口小于最小宽之和时允许水平溢出；所有 Star 到达 Max 后保留多余空白。

Auto 从列头估测起步，实际已实现的列头和单元格回报测量宽度，按 ColumnId 保存该测量周期内最大值。它只评估可见行，绝不为量宽枚举全部模型。尚未滚动到的长值可令列在首次显示时增长。列配置、字体、显式单元格/列头主题变化开始新测量周期；普通滚动和数据投影变化保留最大宽度，避免反复缩窄。

纵向只实现可见行加上下各一行；横向只实现冻结区内可见列与滚动区相交列，完全遮住或在视口外的列不创建单元格。两轴均复用 CellPresenter，文本/复选框显示控件在列种类相同时也复用。每行另保留最多两个空闲单元格；行池保留最多两个空闲行。复杂 DataTemplate 的内部控件是否复用由模板/ContentPresenter 行为决定，不能仅以 CellPresenter 统计推断所有后代控件均未重建。

行列头、单元格排布、冻结裁剪和命中测试共享 TreeDataGridLayout。主 Presenter 提供 RealizedRowCount、CreatedRowCount、RecycledRowCount、RealizedCellCount、CreatedCellCount、RecycledCellCount。行提供 RealizedCells 列表，使用其中 ColumnIndex 找列，不能把 Children 的数组位置当源列索引。

## 可变行高与锚点

`AutoRowHeight=true` 时，测量当前已实现单元格的实际期望高度，行高取 RowHeight/RowHeightSelector 与内容高度的较大值。按 NodeId 缓存已测高度，在同列宽周期内只增长，横向滚走长文本时不会缩回默认高度。新出现的更高单元格仍可能使行增长；这是横向虚拟化下的渐进测量策略。列宽、字体、模板、数据投影变化清理相关高度测量；不预创建隐藏列来预知所有高度。

投影变化以视口顶部 NodeId 和行内像素偏移为锚点，插入、移动后恢复到该节点。若锚点被折叠隐藏，回退到之前最近的仍可见节点；若被删除，回退到原索引附近。显式 Offset 设置优先于尚未恢复的锚点。命中与按 NodeId 定位在下一次布局前也会同步更新脏几何，不使用旧投影索引访问新源。

几何当前仍使用全行前缀数组；首次布局和投影/测量高度变化重建是 O(n)，并未宣称百万行或稳定 60 FPS。行高/宽度渐进测量在每个测量循环中限制迭代次数，防止用户内容造成无限布局。

## 生命周期和验证

回收、卸载、源或模板替换都会清除 DataContext、Value、列模板内容及绑定。应用拥有 Source，控件不会 Dispose 它。呈现、几何和单元格各自检测回调期间的版本变化；RowHeightSelector、ValueGetter、Binding 初始化以及行清理回调中发生 Source/Columns 变化时停止旧工作，初始化期间创建的失效绑定立即释放。

Headless 验证包括 P1 原14项、父线程4项重入回归以及 P2 的混合 Min/Max Star 顺序、可见数据 Auto、格式/三态/模板/行号、二维控件复用与数量界限、换行高度缓存、NodeId 锚点、无中间布局快速调宽、重复列拒绝及模板绑定释放。主题线另验真实主题、用户 CellTheme、指针拖宽、懒加载展开和 Windows 原生演示。中文输入法、编辑交互、无障碍、列重排和行拖放不在本次完成声明中。
