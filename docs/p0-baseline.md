# P0 技术与功能基线

记录日期：2026-10-12。以下是本项目冻结的开发目标，不是上游兼容认证。

| 项目 | 固定值 | 理由/证据 |
| --- | --- | --- |
| SDK | 10.0.401，global.json 禁止自动滚动 | 本机可用 SDK，构建验证以此版本执行 |
| TFM | net10.0 | 先验证计划首选组合；不推断麒麟环境运行时兼容性 |
| Avalonia/桌面/Fluent/Headless | 12.0.3 | 中央版本 + 每项目 packages.lock.json；已实际 NuGet 还原 |
| 上游参考控件 | Avalonia.Controls.TreeDataGrid 12.0.3 | [固定包页](https://www.nuget.org/packages/Avalonia.Controls.TreeDataGrid/12.0.3)，仅参考版本，产品无此依赖 |
| 文档语义 | 官方 v12 文档，2026-10-12 查阅 | 文档会更新，未对照具体包执行的行为均标记未对照 |
| 测试 | xunit.v3 3.2.2 / VS runner 3.1.4 / Test SDK 18.0.1 | Avalonia Headless 12 使用 xUnit v3，统一避免双重 Fact 类型 |
| 产品程序集 | NT.AvaloniaControl.TreeDataGrid / NT.AvaloniaControl.TreeDataGrid.Themes | 功能无主题反向引用，主题单向引用功能，测试验证 |
| 发布版本 | 0.2.0-preview.2 | MVVM 绑定与编辑树表预览；未上传公共包源 |

[v12 迁移文档](https://docs.avaloniaui.net/controls/data-display/structured-data/treedatagrid/breaking-changes-v12)确认上游改为非泛型列及抽象数据源，本项目采用独立命名空间下的对应名称。列位于 `grid.Columns`；MVVM 版本已补 ItemsSource 及 XAML 内容列，尚未提供上游全部 fluent 方法，不宣称源码直接兼容。公开 `Binding` 使用已实际编译的 Avalonia 12 `BindingBase`。具体支持和差异见 [MVVM 对照](mvvm-official-alignment.md)。

**功能与 API 执行矩阵**

所有“参考语义”均为文档依据，**未使用许可参考程序进行行为对照**。测试列中未来编号是验收任务，只有列明现有测试类的项已经有代码。

| 条目 | 参考语义/目标 API | 本项目当前实现及默认值 | 验收与后续阶段 |
| --- | --- | --- | --- |
| F01a | 平面 Source | FlatTreeDataGridSource<T>，null 模型拒绝 | SourceTests / StateTests，P1 |
| F01b | 树 Source | HierarchicalTreeDataGridSource<T>，默认折叠或读取模型展开位，深度≤256，提前索引已加载节点 | SourceTests / ExpansionTests，P1/P3；支持事件加载新子节点 |
| F01c | XAML 列和 ItemsSource 入口 | 非泛型 XAML 内容列、ItemsSource 自动平面/层级适配、层级属性 ReflectionBinding；保留显式 Source | ItemsSourceBindingTests、compiled MVVM NuGet 消费；fluent API 尚未提供 |
| F02a | 文本绑定、格式、换行 | BindingBase / ValueGetter；Binding 优先；StringFormat、TextWrapping 与自动行高 | Headless 文本与布局测试，P2 |
| F02b | 复选框三态、模板、展开器、行头 | 五种列；三态暂存编辑及写回；CellEditingTemplate 接收编辑会话；树缩进和展开按钮 | P2/P3 显示测试、P4 编辑与主题组合测试 |
| F03a | 固定/Auto/Star 列宽与限制 | Width=120，MinWidth=24，MaxWidth=Infinity；Auto 测列头和已实现单元格；Star 按最小最大宽度重分配 | P2 几何和布局测试；不扫描全量数据求 Auto 宽度 |
| F03b | 用户调宽开关/边界 | CanUserResizeColumns=true；列 CanUserResize=true；拖动按 MinWidth/MaxWidth 限制 | P2 调宽测试；项目默认值，非官方行为实测 |
| F04a | 身份选择、当前单元格 | TreeDataGridState，初始无选择/当前；隐藏选择保留，删除清理 | StateTests，P1基础 |
| F04b | 单多行、单矩形单元格、批量事件 | 控件默认 Row/Single；独立 State 默认 Multiple 保持旧契约；Shift 范围、Ctrl 行切换、程序选区和合并通知 | P4 State/Headless/主题测试；重排使原选区不连续时收敛当前单格 |
| F04c | MVVM 选择和当前项 | SelectedItem、SelectedIndex、SelectedItems、CurrentCell 默认 TwoWay；CurrentCell 为本项目扩展 | MvvmBindingTests / MvvmIntegrationTests / MvvmReviewTests，生命周期与重入专项 |
| F05a | 编辑提交/取消/验证 | BeginEdit/CommitEdit/CancelEdit 基础；验证先于写回；取消零写入；回调禁止重入 | StateTests，P1基础 |
| F05b | F2/点击触发、转换、编辑模板、只读、IME | 默认 F2/双击，可开单击；Enter/Tab 提交，Esc 取消；类型转换/验证/只读/模板，编辑草稿跨回收保留 | P4 交互及主题组合测试；预编辑代理防误提交，真实 IME 另验 |
| F05c | MVVM 编辑扩展 | 非视觉 TreeDataGridEditCommands、ICommand 开始/提交/取消、IsEditing/EditSession 状态和草稿绑定 | 完整 MVVM Demo 原生 21 项检查；公开 API 增量兼容测试 |
| F06a | 单节点展开、投影更新 | SetExpanded；叶节点也保存展开位，后续添加子节点可见 | SourceTests，P1 |
| F06b | 全部/条件递归、前后事件、绑定、按需加载 | ExpandAll/CollapseAll/SetExpandedRecursive/SetExpandedAt；RowExpanding/Collapsing 可取消；完成事件在投影提交后；可选模型展开绑定 | ExpansionTests / TreeStateIntegrationTests，P3；同步事件加载，异步结果由应用调度回创建线程 |
| F07 | 单列排序、自定义比较、三态 | Sort/ClearSort、SortByColumn；列头升降序，可选三态；兄弟内稳定排序，清除恢复源序 | ProjectionTests/SortingInteractionTests；保留身份和源路径，校验失败阻止列头排序 |
| F08 | UI线程谓词过滤、刷新清除 | SetFilter/Refresh/SetFilter(null)；所有已加载节点求值；父节点拒绝隐藏整支，不自动保留祖先 | ProjectionTests；动态更新、异常恢复、批处理及选择编辑联动 |
| F09a | 冻结、网格线 | FrozenColumnCount=0；冻结裁剪/命中共享几何；单元格和列头主题提供边框资源 | P2 组合测试；完整网格线模式与交互验收 P6-F09 |
| F09b | 列重排 | 程序修改 Columns 可以更新；用户拖动重排未实现 | P6-F09；禁止跨冻结边界 |
| F10 | 自动行拖放及事件 | 尚未实现 | P6-F10；只读/循环/过滤排序下合法性 |
| F11a | 定位及容器生命周期 | ScrollIntoView(NodeId)、RowPrepared/RowClearing、卸载释放 | VirtualizationTests，P1原型 |
| F11b | 文本搜索/源路径定位/输入事件 | SearchText 前缀搜索可见行；文本绑定/模板选择器；键盘搜索、身份滚动 | P4 搜索测试；源路径滚动与事件补齐仍属 P6-F11 |
| F12a | 行虚拟化/回收 | 容器数量按视口+overscan约束；行高默认28 | 十万行 Headless+原生探针，P1 |
| F12b | 横向虚拟化 | 仅实现视口相交列及冻结列；单元格和列头回收；不为百列创建全部可视单元格 | P2 百列与冻结组合测试；P7 补持续滚动统计，见性能文档 |

官方来源：[总览](https://docs.avaloniaui.net/controls/data-display/structured-data/treedatagrid/)、[列类型](https://docs.avaloniaui.net/controls/data-display/structured-data/treedatagrid/column-types)、[选择](https://docs.avaloniaui.net/controls/data-display/structured-data/treedatagrid/selection-modes)、[排序](https://docs.avaloniaui.net/controls/data-display/structured-data/treedatagrid/sorting)、[过滤](https://docs.avaloniaui.net/controls/data-display/structured-data/treedatagrid/filtering)、[展开](https://docs.avaloniaui.net/controls/data-display/structured-data/treedatagrid/expand-and-collapse)、[API](https://docs.avaloniaui.net/api/avalonia/controls/treedatagrid)。

`CanUserResizeColumns` 在原计划引用的总览与 API 页面分别描述为 false/true。P2 选择默认值 true，并用本项目调宽测试固定语义；**不是官方实测结论**。没有许可参考程序时仍保持“行为未对照”。

平台范围：本机 Windows x64；通用 Linux/麒麟 x64/ARM64 均不能由编译成功推断运行通过。麒麟发行版/版本/CPU 尚缺，用户同意先完成 Windows 和核心模型。

