# P3 树数据与展开契约

P3 在 P1 的身份、源路径和不可变投影上增加展开命令、可取消前事件、完成事件、同步按需加载和模型展开绑定。排序、过滤、异步远程树不在本阶段实现。

## 使用入口

原有两个参数的层级数据源构造保持可用；默认各节点折叠，同一个业务对象出现多次时，各 occurrence 独立保存展开状态。

```csharp
var source = new HierarchicalTreeDataGridSource<Item>(items, x => x.Children);
source.SetExpanded(source.Rows[0].Id, true);
source.SetExpandedAt(new IndexPath(0, 1), true);
source.ExpandAll();
source.CollapseAll();
source.SetExpandedRecursive(true, row => row.Depth < 2);
```

`SetExpanded` / `SetExpandedAt` 返回是否实际改变了目标；目标不存在、状态未变、前事件取消，或前事件移除/替换了原身份时返回 false。路径入口在调用时解析已发布的源路径，再使用 NodeId 操作，因此移动不会令完成事件指向另一个对象。保留 `SetExpandedAt` 独立名称，避免新增重载使 P1 的 `SetExpanded(default, true)` 调用产生歧义。

`ExpandAll` / `CollapseAll` / `SetExpandedRecursive` 返回成功变化的节点 occurrence 数。展开采用先父后子的顺序，只对 `HasChildren` 为 true 的节点发起请求；折叠采用先子后父顺序，也会清除单节点操作预先保留的叶节点展开位。谓词返回 false 时，整个子树不再访问；父节点展开被取消时，该分支也不再访问。调用开始时取得根节点身份；在每个分支前事件中同步加载的子节点会进入本次递归。其他回调新插入到已遍历位置或新的根节点，交给下一次调用处理。

单节点操作仍允许将当前叶节点设为展开。以后添加子节点时，子节点会立即进入可见投影。

## 按需加载与模型绑定

```csharp
var source = new HierarchicalTreeDataGridSource<Item>(
    items,
    x => x.Children,
    new HierarchicalTreeDataGridSourceOptions<Item>
    {
        HasChildrenSelector = x => x.MayHaveChildren,
        IsExpandedSelector = x => x.IsExpanded,
        IsExpandedWriter = (x, value) => x.IsExpanded = value,
    });

source.RowExpanding += (_, e) =>
{
    var item = (Item)e.Row.Model;
    if (!item.ChildrenLoaded)
    {
        foreach (var child in LoadChildrenSynchronously(item))
            item.Children.Add(child);
        item.ChildrenLoaded = true;
    }
};
```

`HasChildrenSelector` 可以独立配置。行的 `HasChildren` 为“实际子集合非空 **或** 预告为 true”；预告 false 不会隐藏已经存在的子节点。空子集合配合 true 预告可显示展开器，前事件再同步填充子集合。前事件可以替换子集合；应通过 `INotifyPropertyChanged` 通知，或者调用 `Refresh()`。没有这些通知的普通集合修改，需要显式 `Refresh()` 才会进入索引。

`IsExpandedSelector` 与 `IsExpandedWriter` 必须成对提供，否则构造抛出 `ArgumentException`。委托在构造时捕获。模型的初始状态在首个投影中生效；之后的属性通知或 `Refresh()` 读取模型状态。模型直接变更是权威状态同步，发送 `ProjectionChanged(ModelChanged)`，不再次触发可取消的展开命令事件。selector 应无副作用，writer 必须成功写入请求值，或在不修改模型时抛错；不支持任意用户 setter 的自动回滚。

未绑定时，重复对象各 occurrence 独立。绑定时，同一个对象的所有 occurrence 共用模型上的展开属性：对任一 occurrence 发起命令，会对本次请求开始时所有待变化 occurrence 发前事件，其中任一取消则整个组不调用 writer；成功时 writer 只调用一次，各存活 occurrence 分别收到完成事件。条件递归选中其中一个 occurrence 也会影响它的共享组。请求回调中新建的 occurrence 根据模型权威状态初始化，不补发旧命令的事件。

## 事件、提交与异常

四个模型事件位于 `TreeDataGridSource`，与可视行容器生命周期事件相互独立：

| 事件 | 参数 | 读取到的行状态 |
| --- | --- | --- |
| `RowExpanding` | `TreeDataGridRowCancelEventArgs` | 变更前快照，可设 `Cancel = true` |
| `RowCollapsing` | `TreeDataGridRowCancelEventArgs` | 变更前快照，可设 `Cancel = true` |
| `RowExpanded` | `TreeDataGridRowModelEventArgs` | 已提交投影中的行 |
| `RowCollapsed` | `TreeDataGridRowModelEventArgs` | 已提交投影中的行 |

每个参数通过 `Row` 提供模型、NodeId、源路径、深度和展开状态。前事件取消具有粘性，后一个监听者不能恢复已经取消的请求。前事件可读旧 `Rows`，其 `e.Row` 是当前被请求节点的快照；加载过程维护内部图和订阅，但不逐次发布中间投影。

普通命令的顺序是：**全部前事件 → 写入状态 → 一次 `ProjectionChanged(ExpansionChanged)` → 完成事件**。无变化且回调未加载数据时不增加 Version。前事件取消不会回滚它已加载的模型数据；加载数据本身仍会提交一次投影。批量正常展开只提交一次投影，避免逐节点投影导致 O(n²)。

若调用者已有外层 `DeferRefresh()`，命令合并进入该批次，完成事件等待最外层 scope 释放。相同 NodeId 在一个发布批次中只保留最后一次成功命令的完成事件；例如 expand/collapse/expand 只完成一次 Expanded。最终状态回到最初值时，仍会完成最后一次成功命令。中间命令的前事件仍逐次发送。完成事件发送前再次核对节点身份和请求状态：目标被删除、Replace/Reset 失效、或模型权威状态改变时，跳过该完成事件。不能把方法返回的变化数当作最终可见行数或完成事件数。

监听者可在前后事件中修改集合、模型属性、调用 `Refresh()`。直接从展开前事件、后事件、递归谓词或投影通知中再次调用展开命令会抛 `InvalidOperationException`，防止递归请求和旧目标写回。`IsRefreshDeferred` 在 scope 或排队变更存在时为 true；编辑模型继续遵守 P1 的规则，不能在此时开始或提交编辑。

前事件抛错会终止当前命令，当前节点不执行状态写入；之前已经成功的节点和已加载的数据正常提交。完成事件或投影监听者抛错时，已提交状态保留，其他通知和排队集合变更继续处理，然后重新抛出原异常；多个失败以 `AggregateException` 报告。异常路径会释放操作和刷新标志，修正模型后可以继续操作。Dispose 会解除订阅并取消尚未发送的完成事件。循环和超过 256 层的模型图继续按 P1 规则拒绝，保留最后的有效图；模型实际修改需由调用者修正后再刷新。

## 性能与验证边界

正常批量展开直接改变节点展开位并一次生成投影，复杂度与已索引节点数成线性关系。绑定 writer 引起的属性通知合并为一次图维护，模型 occurrence 索引只在启用展开绑定时建立。源路径在建图时生成并复用于投影，不在展开时重新分配整套路径。

同步 loader 的每次结构通知仍使用 P1 的全树维护策略；高频逐条加载大子集合可能需要后续增量适配优化。`DeferRefresh` 合并发布但持续维护订阅，所以不能把它视为完全免除结构维护成本。所有操作和通知继续要求创建线程。

核心回归覆盖前后事件与版本、取消、同步加载、同对象重复 occurrence、外部绑定变更、前事件移动/删除/替换、后通知删除、重入拒绝、scope 合并、通知异常、Dispose、256 层边界、批量 selector 调用次数，以及固定种子的树命令和集合变化随机序列。平台输入、真实中文输入法、屏幕阅读器和麒麟兼容性不由这些核心测试替代。

本轮 Debug 专项验证：`ExpansionTests` 26 项、`SourceTests` 22 项、`StateTests` 9 项、`TreeStateIntegrationTests` 3 项，共 60 项全部通过；构建零警告、零错误。两组树随机测试分别以有绑定和无绑定方式执行 300 步命令/集合混合操作。2,500 个绑定分支的全部展开只提交一次投影，子集合 selector 总读取不超过该树的 5,000 个节点。测试使用 xUnit v3 直接运行器，最终 Release、API 快照与呈现/主题组合结果由统一集成验收记录。
