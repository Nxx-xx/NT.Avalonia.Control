# TreeDataGrid 无障碍接入与当前验收边界

2026-10-13：Q1 已接入专用自动化 peer 和 Headless 契约测试；实际 Windows 屏幕阅读器及目标麒麟桌面仍待验证。测试通过只证明本地公开 API 行为，不能替代真实读屏验收。

## 固定 Avalonia 版本的公开能力

当前依赖 Avalonia `12.0.3`。已通过本地引用程序集 XML 和公开类型元数据核对其能力，不使用 Avalonia 私有反射。

| 对象 | 控件类型 | 已接入的公开 provider / 信息 |
| --- | --- | --- |
| TreeDataGrid | `DataGrid` | `ISelectionProvider`、`IScrollProvider`、当前可见行列数量、应用配置的自动化名称 |
| 列头 | `HeaderItem` | 标题、ColumnId、可见显示索引、排序状态、`IInvokeProvider` 排序动作 |
| 行 | `DataItem` | NodeId、显示行索引、深度、展开状态、`ISelectionItemProvider`、`IExpandCollapseProvider` |
| 单元格 | `DataItem` | 行列身份和显示索引、名称及值、只读、选择、当前 / 焦点、验证错误、`ISelectionItemProvider`、`IValueProvider` |
| 活动编辑器 | 编辑器原有类型 | 保留 Avalonia TextBox、CheckBox 或应用模板控件的自动化子树 |

该版本未公开 `IGridProvider` / `ITableProvider` 接口。因此 `DataGrid` 控件类型与身份定位入口不能等同于原生 UIA Grid / Table pattern；原生平台桥接与屏幕阅读器的最终呈现尚待真实环境确认。HeaderItem 和 DataItem 的自动化状态包含当前显示索引，不把源 IndexPath 当成显示行号。

## 应用提供名称与定位目标

应用可通过 Avalonia 的 `AutomationProperties.Name` 为表格提供场景名称，例如“订单明细”。专用 peer 优先使用应用提供的名称。

```csharp
AutomationProperties.SetName(grid, "订单明细");
var peer = (TreeDataGridAutomationPeer)
    ControlAutomationPeer.CreatePeerForElement(grid)!;

var cellPeer = peer.GetCellPeer(nodeId, columnId);
cellPeer?.BringIntoView(); // 实现可见行列；保持选择不变。
cellPeer?.SetFocus();      // 按身份设置当前单元格和控件焦点。
```

`GetRowPeer(NodeId)`、`GetCellPeer(NodeId, ColumnId)` 和 `GetColumnHeaderPeer(ColumnId)` 返回逻辑身份 peer。不存在的身份返回 `null`。已有但被折叠、过滤或隐藏的身份可以读取元数据；选择、聚焦、写入和 BringIntoView 要求目标当前可见，不能自动选择其它显示位置。

逻辑行 / 单元格 peer 捕获当时的 Source。滚动导致容器回收后，该 peer 继续指向原 NodeId / ColumnId；Source 替换、节点删除或列隐藏后，相关动作拒绝过期目标。实际 presenter peer 描述该容器当前实现的内容，应用不应把回收容器当作长期业务身份。完整虚拟化数据不一次性加入自动化树；树中提供当前实现的行、单元格及列头，逻辑入口用于按身份定位离屏目标。

## 选择、写入和树操作

provider 动作仍受创建线程、控件启用状态和现有编辑策略约束。改变选择或焦点前，已有其它单元格草稿须成功提交；校验失败会保留草稿并拒绝导航。

`IValueProvider.SetValue` 通过 `BeginEdit`、转换、验证、`CommitEdit` 实现，采用既有 setter 的一次提交契约；转换或校验失败不调用 writer，错误可从 HelpText 与活动编辑器读取。任意业务 setter 已产生的副作用依旧由应用负责，不承诺自动回滚。

行选择模式下，行 peer 支持添加和移除单个身份；单元格选择模式下由单元格 peer 提供选择动作。单元格选择遵循现有矩形选区：`AddToSelection` 扩展矩形；从多格矩形中单独移除一格会造成孔洞，因此该动作明确拒绝。应用可改用 `Select` 重新建立合法选区。隐藏行的既有选择继续保留，选择 provider 可以报告该身份的选中状态。

表格的滚动 provider 委托必需模板部件 `PART_ScrollViewer` 的 Avalonia 公开 provider。自动化客户端可以通过滚动模式更新视口，再读取新实现的行；自定义模板仍需提供可自动化的 ScrollViewer。

行展开 / 折叠使用 Source 的现有受控路径，叶节点报告 `LeafNode`。应用的同步按需加载回调、版本检查和换源规则仍然适用。

## 尚须真实环境完成的验收

- Windows 真实屏幕阅读器识别表格、列头、行和单元格；正确读出选择、只读、层级、展开状态与验证错误。
- 键盘浏览与编辑器焦点、读屏导航、滚动回收及换肤后的公告和位置一致。
- 确认自动化事件经实际 Windows 平台桥接后得到客户端认可；Headless 属性查询及事件不代表原生公告已经验证。
- 目标麒麟的桌面无障碍桥接、实际读屏软件和输入法组合。

Q1 完整完成标准包含上述真实验证。缺少真实屏幕阅读器或目标系统时，保持相应项目“待验证”。
