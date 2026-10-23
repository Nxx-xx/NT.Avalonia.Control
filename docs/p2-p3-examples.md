# P2 / P3 树表、主题与样例

P2/P3 阶段原始 Demo 的窗口标题为 `NT TreeDataGrid · v0.1 树表与虚拟化样例`，初始客户区 1180 × 780 DIP。本文保留该阶段的场景与验收记录；当前场景由统一 `NT.AvaloniaControl.Demo` 的 TreeDataGrid 页面承载，源文件位于 `Pages/TreeDataGrid/`。单元格选择、编辑手势及 IME 编辑会话随后在 P4 接入。

## 可操作场景

在仓库根目录执行 `dotnet run --project samples/NT.AvaloniaControl.Demo`，默认进入 TreeDataGrid 页面。在普通模式的“选择示例”下拉框中选择树结构、按需加载、列宽换行、大数据或横向滚动；“布局选项”提供冻结列与行高设置，对应场景按钮执行树操作。该阶段的操作与观察如下：

| 入口 | 操作与观察 |
| --- | --- |
| 10 万平面行 / 100 列 | 横纵滚动，观察状态栏的实现行与单元格数；控件只实现视口内列和冻结列 |
| 冻结前 2 列 | 横向滚动后，前两列维持位置；滚动区列在冻结边界处裁剪 |
| 可变行高 | 每 7 行使用 52 DIP，其余 28 DIP；切换时观察顶部节点锚点 |
| 树投影 | 100 个根、每根 8 个子节点；首次展开前 3 个根，可见 124 行；点击展开器可改变状态 |
| 全部展开 / 全部折叠 | 可见行分别为 900 / 100；操作使用同一个树数据源 |
| 条件展开：偶数根 | 先折叠，再按根节点编号展开偶数根；可见 500 行；不匹配的子树被剪枝 |
| 首根增加 / 删除末子节点 | 修改首根的 ObservableCollection；投影和路径同步更新；按钮目标明确为首根，尚无行选择 UI |
| 按需加载样例 | 在 RowExpanding 前事件同步加入 3 个子节点，只加载一次；随后折叠 / 展开复用已加载数据 |
| 列类型 / Auto / Star | 行号、文本、可空三态复选框、应用显示模板；固定、Auto、Star 宽度共存；拖动列头右边缘调宽 |
| 自动行高 / 换行 | 长中文说明使用换行列，按已实现内容测量行高 |
| 亮 / 暗主题 | 更新独立样式包的色彩资源，保持源与树展开状态 |
| 应用模板 | 用应用提供的主控件模板替换默认模板，保留列头和滚动部件；再次点击恢复默认模板 |

P2/P3 阶段的复选框只展示布尔 / null 值，不提供写回；模板列使用 `CellTemplate`，`CellEditingTemplate` 当时仅是后续编辑器接入的契约。当前编辑能力见 [P4/P5 样例](p4-p5-examples.md)。按需加载样例是同步回调，不宣称内置异步远程树。

## 样式和模板边界

主题 URI 保持 `avares://NT.AvaloniaControl.TreeDataGrid.Themes/Themes/Generic.axaml`，代码入口保持 `new TreeDataGridTheme()`。默认主题在独立程序集定义这些 ControlTheme：

- `TreeDataGrid`：组合列头与滚动视口，保留必需的 `PART_ScrollViewer` 和 `PART_RowsPresenter`。
- `TreeDataGridCellPresenter`：背景、边框、内边距及内容呈现；内部文本、复选框、模板和展开器内容由功能程序集建立。
- `TreeDataGridColumnHeader`：列头内容和可选 `PART_ResizeThumb`。保留手柄可启用指针调宽，删除手柄则只保留列头显示。

`CellTheme` 与 `ColumnHeaderTheme` 允许应用替换单元格和列头外观。展开按钮使用 `tree-data-grid-expander` 样式类；它的点击行为仍由功能程序集调用 Source。主题不修改模型、展开状态或选择状态。应用需加载基本控件主题（Demo 使用 FluentTheme），用于 Button、CheckBox 和 ScrollViewer。

模型的 IsExpanded 读写通过 `HierarchicalTreeDataGridSourceOptions<TModel>` 配对提供，展开器、程序递归操作和外部属性变化共享该状态。模型继续由应用拥有，源负责订阅释放。

## 自动验证

以下使用统一 Demo 的当前名称重跑验证；后面的通过项数、时间和截图描述保留当时记录。

```powershell
dotnet build tests/TreeDataGrid.Themes.Tests --no-restore -m:1 -p:UseSharedCompilation=false
dotnet tests/TreeDataGrid.Themes.Tests/bin/Debug/net10.0/TreeDataGrid.Themes.Tests.dll
dotnet build samples/NT.AvaloniaControl.Demo --no-restore -m:1 -p:UseSharedCompilation=false
dotnet samples/NT.AvaloniaControl.Demo/bin/Debug/net10.0/NT.AvaloniaControl.Demo.dll --smoke --smoke-output artifacts/platform/windows-smoke-p2-p3-debug.json
```

可追加 `--smoke-image artifacts/platform/windows-smoke-p2-p3.png`，通过 `RenderTargetBitmap` 保存最终 10 万行 × 100 列场景，以及同基名 `-tree.png`、`-columns.png` 的树与换行列场景。这些文件是 Avalonia 控件的离屏渲染，用于排版检查，**不是桌面截图**。

Headless 跨模块用例覆盖：默认主题下各显示列、真实指针拖动列头手柄、展开器触发按需加载、外部 IsExpanded 绑定、换肤 / 自定义模板后的动态树更新、100 列的容器上界与回收值正确性，以及 Auto / Star / 换行行高与应用 CellTheme 的组合。

Windows `--smoke` 保留原有 9 项检查，增加行高锚点、树批量操作、动态子集合、上方插入时的锚点、按需展开、双向展开绑定、应用模板保留树状态、显示列类型、列宽 / 行高测量及 10 万行 × 100 列的水平容器界限与复用。结果 JSON 使用固定场景顺序，记录窗口、字体、源行数、实现行 / 单元格 / 列头及视觉控件数；初始等待仍不计作性能基准。

原生窗口探针中的展开按钮通过路由 Click 事件激活，用于验证同一 UI 处理链。它不替代物理鼠标、真实中文输入法、系统剪贴板、无障碍或多 DPI 的人工回归。麒麟设备仍未提供，兼容状态保持未验证。

## 当前集成证据

2026-10-12 11:56（香港时间），Debug 专项验证通过：**主题 / 跨模块测试 12 / 12；Windows 原生探针 21 / 21，退出码 0**。原始记录为 `artifacts/platform/windows-smoke-p2-p3-debug.json`。

| 场景 | 本机记录 |
| --- | --- |
| 运行环境 | Windows 10.0.26200 x64、.NET 10.0.12、Avalonia 12.0.3；1180 × 780 DIP、100% |
| 字体属性 | 表格 14 DIP / Inter，平台默认 Segoe UI；中文逐字形回退仍未单独验证 |
| 10 万行 × 20 列首屏 | 16 行、112 单元格、7 列头 |
| 冻结后双向滚动 | ScrollViewer 与 Presenter 均为 (600, 8000)，18 行、144 单元格 |
| 可变行高 | 最大行高 52 DIP，顶部节点保持；调整后的纵向偏移 8984 |
| 树上方增加节点 | 501 可见行，纵向偏移 1200 → 1228，顶部节点身份保持 |
| 按需加载 + 应用模板 | 4 可见行、只加载一次；模型展开绑定保持 |
| 三态 / 模板 / 换行列 | 10 实现行、50 单元格；最大行高 64 DIP |
| 10 万行 × 100 列滚动 | 18 行、144 单元格、8 列头；累计单元格复用 192 次 |

本轮跨模块测试先发现并促成修复：调宽过程中替换列头导致鼠标捕获丢失、自动行高被固定高度测量截断、待恢复锚点覆盖随后显式设置的滚动位置。回归使用连续两次指针移动、真实主题和原生 ScrollViewer 路径验证，未放宽断言。
