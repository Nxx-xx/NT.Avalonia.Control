# P4 / P5 选择、编辑与排序过滤样例

在仓库根目录执行 `dotnet run --project samples/NT.AvaloniaControl.Demo`，默认打开统一 Demo 的 TreeDataGrid 页面。普通模式默认场景提供 2,000 条可编辑记录；“选择示例”下拉框保留树、按需加载、列宽换行、大数据、横向滚动和四种自定义列模板，共十个场景。冻结列和行高设置位于“布局选项”，亮暗主题由主窗口统一切换。TreeDataGrid 页面源文件位于 `Pages/TreeDataGrid/`。

## 交互场景

| 入口 | 可观察行为 |
| --- | --- |
| 编辑 / 验证样例 | 名称必填且最多 32 字，数量须为 0–100 整数，可空布尔支持三态，说明列使用应用编辑模板 |
| F2 / 双击 / 编辑当前 | 建立草稿；Enter 提交、Esc 取消；提交前模型保持原值 |
| 提交非法值 | 单元格显示红色边框和错误背景；错误文本由功能控件提供；保留草稿，不写模型 |
| 矩形选区 3 × 3 | 选择第 2–4 行的名称、数量、布尔列；单个矩形，Shift 点击或方向键可扩展 |
| 行多选 | 切换为行选择；程序先选前两行，Ctrl 点击切换，Shift 扩展连续行 |
| 点击列头 | 单列升序 / 降序；“二 / 三态排序”增加清除状态；箭头独立于列头内容 |
| 名称包含 / 数量下限 | 同时满足谓词的记录可见；清除筛选恢复；树场景不匹配父节点时整个分支隐藏 |
| 编辑排序列 | 写回正确节点后重排；活动编辑的原节点身份不会被显示行索引替换 |
| 编辑使记录不再匹配 | 模型完成写回后从可见视图隐藏；外部过滤主动隐藏编辑目标则取消草稿 |
| 亮 / 暗主题、应用模板 | 保留当前源、选择和编辑会话；切换数据源取消旧编辑并释放旧状态 |

排序前会尝试提交活动编辑；验证失败时拒绝排序。隐藏选择按节点身份保留；矩形选择遇到重排而不再连续时会收缩为当前单元格，避免显示不连续的多区域选择。行号列关闭用户排序；应用显示模板若返回模型对象，应明确提供 `Comparison` 或关闭排序。

## 编辑契约

```csharp
grid.Columns.Add(new TreeDataGridTextColumn
{
    Header = "数量", ValueType = typeof(int),
    ValueGetter = model => ((Item)model).Quantity,
    ValueSetter = (model, value) => ((Item)model).Quantity = (int)value!,
    ValueValidator = value => value is int count && count is >= 0 and <= 100
        ? null : "数量必须为 0 到 100 的整数。",
});
```

`ValueType` 或 `ValueConverter` 定义草稿转换，`ValueValidator` 接收转换后的值；成功后 `ValueSetter` 执行一次写回。`CellEditingTemplate` 的数据上下文是 `TreeDataGridEditSession`，编辑器应双向绑定 `Value`。`OriginalValue`、`Cell` 与 `ValidationError` 可用于应用模板；草稿位于状态中，编辑控件回收后可重新建立。

控件持有并管理 `grid.State`。应用可使用 `SelectCellRange(anchor, end, columnIds)` 创建单个矩形，或在 `DeferSelectionNotifications()` 作用域中批量选行。替换主题或模板不替换状态；替换 Source 会销毁旧状态。P1 的应用自建 `TreeDataGridState` 仍可独立使用，控件交互不会自动连接另一个外部状态实例。

## 排序过滤契约

```csharp
grid.SortByColumn(quantityColumn.Id, TreeDataGridSortDirection.Ascending);
source.SetFilter(model => ((Item)model).Quantity >= 40);
source.SetFilter(null); // 清除
source.ClearSort();
```

直接 `source.Sort(columnId, comparison, direction)` 支持程序自定义比较器；活动草稿随稳定身份保留。树仅在同级排序，`NodeId` 和源 `IndexPath` 不变；过滤不保留不匹配的祖先。改变谓词捕获变量后调用 `Refresh()`。排序、过滤与写回后的投影通知共享源的更新边界，嵌套 `DeferRefresh()` 最终发布一次。

## 主题模板

公开主题入口仍为 `avares://NT.AvaloniaControl.TreeDataGrid.Themes/Themes/Generic.axaml`。主题增加 `:selected`、`:current`、`:editing`、`:invalid` 外观，亮暗各自使用选择、焦点、编辑及错误色资源。当前单元格边框是不可命中的叠层，不挤占编辑器内容。

列头模板使用只读 `SortIndicator` 单独显示箭头，保留任意 `Content` 和 `PART_ResizeThumb`。`:sortable`、`:sort-ascending`、`:sort-descending` 可用于应用样式。主题不执行写回、排序或选择操作；自定义模板继续只需提供 `PART_ScrollViewer` 与 `PART_RowsPresenter`。

## 自动验证

以下使用统一 Demo 的当前名称重跑验证；后面的通过项数、时间和图像描述保留当时记录。

```powershell
dotnet build tests/TreeDataGrid.Themes.Tests --no-restore -m:1 -p:UseSharedCompilation=false
dotnet tests/TreeDataGrid.Themes.Tests/bin/Debug/net10.0/TreeDataGrid.Themes.Tests.dll
dotnet build samples/NT.AvaloniaControl.Demo --no-restore -m:1 -p:UseSharedCompilation=false
dotnet samples/NT.AvaloniaControl.Demo/bin/Debug/net10.0/NT.AvaloniaControl.Demo.dll --smoke --smoke-output artifacts/platform/windows-smoke-p4-p5-debug.json --smoke-image artifacts/platform/windows-smoke-p4-p5.png
```

Windows 探针保留原有 21 项布局 / 树 / 回收检查，增加 15 项选择、编辑、验证、排序过滤与模板联动检查。它创建真实原生窗口，向真实控件路由 F2 / Enter / Esc 并改变编辑器值，检查最终模型、状态和投影；不模拟操作系统键盘或输入法。JSON 记录每项结果和场景容器数量。

图像参数保存原有主图、`-tree`、`-columns`，以及新增的 `-selection`、`-editing`、`-sort-filter`。这些是 Avalonia `RenderTargetBitmap` 的离屏控件渲染，用于检查排版，**不是桌面截图**。

真实中文输入法的预编辑、候选窗、提交 / 取消仍需人工验收；TextBox 中文赋值不代表 IME 已通过。麒麟设备未提供，麒麟与跨平台输入兼容性仍未验证。

## 本机集成证据

2026-10-12 12:36（香港时间），Debug 主题及跨模块测试 **18 / 18** 通过，保留原有 12 项并新增 6 项。Windows 原生探针 **36 / 36** 通过，退出码 0。原始 JSON 为 `artifacts/platform/windows-smoke-p4-p5-debug.json`，UTC 时间戳 `2026-10-12T04:36:22`。

窗口 1180 × 780 DIP，缩放 100%，Windows 10.0.26200 x64，.NET 10.0.12，Avalonia 12.0.3；表格字号 14 DIP / Inter，平台默认字体 Segoe UI。字体属性不代表逐字形回退已单独验证。

当前工具栏占用更多高度，10 万行 × 100 列滚动场景实现 16 行、128 单元格、8 列头。默认编辑场景实现 14 行、70 单元格；“名称包含 项目 01 且数量 ≥ 40”的排序过滤场景保留 65 个可见记录。

已查看新增三张离屏图：3 × 3 选区和当前单元格轮廓清楚，数量 200 的无效草稿保留且显示红框，暗色过滤视图显示升序箭头。视觉检查同时发现 Fluent 内部复选框模板按 32 DIP 排列导致 28 DIP 行裁切；主题现使用紧凑三态模板，并通过指示器在单元格内不越界的断言验证。
