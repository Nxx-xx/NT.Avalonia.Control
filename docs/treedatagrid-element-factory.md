# TreeDataGrid 元素工厂契约

日期：2026-10-23。适用版本：0.2.0-preview.2 / Avalonia 12.0.3。A5 增加公开 `TreeDataGrid.ElementFactory`，默认 `null`，表示使用内置工厂。功能包不依赖可选主题包；工厂与 `CellTheme`、`ColumnHeaderTheme` 和应用模板可以组合使用。

## 入口与兼容类型

继承 `TreeDataGridElementFactory`，按需覆盖以下方法：

| 方法 | 职责 |
| --- | --- |
| `CreateElement(owner, kind)` | 创建新的行、单元格或列头容器；调用基类即可取得标准类型 |
| `PrepareElement(context)` | 核心身份、绑定、内容和容器连接完成后，安装本次寿命的属性或订阅 |
| `ClearElement(context)` | 移除本次寿命的应用订阅和属性；通常在核心解绑之前调用 |
| `RecycleElement(context)` | 核心清理、解绑与脱离父级后调用；返回 `true` 允许进入当前池，`false` 丢弃 |
| `ReleaseElement(owner, kind, element)` | 容器永久退出控件的池时调用一次，释放创建阶段持有的资源 |

`TreeDataGridElementKind` 为 `Row`、`Cell`、`ColumnHeader`。对应返回类型必须分别是 `TreeDataGridRowPresenter`、`TreeDataGridCellPresenter`、`TreeDataGridColumnHeader`。这些既有类型保持 sealed；本轮支持标准宿主上的模板、属性与生命周期扩展，不支持任意 Control 或 presenter 子类替换内部协议。

`CreateElement` 必须返回全新、无逻辑/视觉父级、`DataContext == null` 的容器。返回 null、错误角色、已挂载控件或任何已发放过的控件都会抛出 `InvalidOperationException`。即使旧容器已经 Release，也不能由工厂再次返回。检查失败不会把其它控件正在使用的容器取走；无效返回值的资源仍由创建者负责。

`TreeDataGridElementContext` 是不可变的本次准备快照，包含 `Owner`、`Element`、`Kind`、`Source`、`Row`、`RowIndex`、`Column`、`ColumnIndex`。行没有 Column，列头没有 Row，对应索引为 -1。索引是准备时的显示位置，不是持续更新的定位器；持久业务状态使用 Source、NodeId/ColumnId。保留 context 会保留其中的模型和控件引用，Clear 后应从应用集合移除。

## 寿命与所有权

正常单元格顺序为：

1. 创建或从当前池取出 → 核心准备和连接 → 工厂 `PrepareElement` → A2 `CellPrepared`。
2. A2 `CellClearing` → 工厂 `ClearElement` → 核心解绑、编辑器与内容清理、脱离父级。
3. 工厂 `RecycleElement` → 放入兼容池，或 `ReleaseElement` 永久丢弃。

行同样先完成工厂准备再发送既有 `RowPrepared`，清理时先 `RowClearing` 再工厂 Clear。列头没有对应 A2 单元格事件。普通状态或排序更新不构成一次新准备；换列身份、换源、换模板或重新实现才结束相关寿命。

默认工厂没有覆盖钩子时，同源滚动和投影刷新可在原父级内重新准备标准宿主，以减少反复挂接。自动行高复用会在重新绑定后，使模板和内容的整个测量链失效，避免上一条数据的临时尺寸进入行高缓存。刷新仍同步清理模型绑定、内容、编辑器及旧 A2/行寿命；清空宿主只保留到下一次布局，未使用的备用宿主在布局结束前脱离。换源、换工厂、换模板或卸载会释放尚未重新准备的宿主。自定义工厂的 Recycle/Release 仍在清理、脱离后调用。一次准备不等同于创建或挂接，容器引用不能替代 Source 和 NodeId/ColumnId 身份。实现和验证见[自动行高缓存修复记录](treedatagrid-a6-auto-height-status.md)、[遥测与滚动记录](treedatagrid-a6-telemetry-scroll-status.md)及[导航编辑修复记录](treedatagrid-a6-navigation-tail-status.md)。

控件拥有池。池按控件、工厂赋值会话及角色隔离，单元格池还随所属行管理，容量沿用既有视口与小型备用池约束。一个工厂对象可以供多个表格使用，但不共享容器。不要在工厂中建立另一套返回旧控件的池。`RecycleElement=true` 是允许回收，不保证保留：池满、工厂退役、模板/控件卸载或失败时仍会 Release。

自动行高的自定义工厂路径会在当前视口预算内保留已清理、已脱离的备用行，减少短行/长行切换时反复创建模板。可见行与备用行共享同一个有界预算；视口缩窄时释放旧宽度的备用行，缩高、筛选为空、换源/工厂/模板及卸载继续收缩或释放池。固定行高和默认宿主复用路径仍使用原有的小型备用池，`RecycleElement=false` 仍立即丢弃。释放回调重入创建的新寿命不会并入旧池的释放批次。验证见[自定义工厂长尾记录](treedatagrid-a6-auto-factory-tail-status.md)。

换工厂先退役旧会话，旧容器不会进入新会话。旧行永久释放时也会释放其备用单元格。控件从视觉树卸载时释放行、单元格和列头；重新挂载可创建新容器。自动数据源临时卸载的所有权仍按既有数据源契约处理。

## 回调重入与异常

回调可以触发换源或换工厂，控件会复验会话及容器版本。特别地，在 `PrepareElement` 内触发这些操作后，回调可能继续安装订阅，因此旧容器的工厂 Clear/Recycle/Release 延迟到该 Prepare 返回之后执行，且该容器不再入池。此时核心字段可能已经清空，Clear 应使用传入的不可变 context 和自己的订阅记录清理，不能依赖 `cell.Row` 等当前字段仍有效。A2 Prepared 尚未发生时不会补发 Prepared/Clearing。

`CreateElement` 回调也可能使创建请求失效：返回的有效容器可以直接 Release，从未执行 Prepare/Clear/Recycle。Release 必须能处理这种情况。

准备或生命周期回调抛错会继续必要的核心清理、其它容器清理和永久释放；不能安全回池的容器丢弃，错误向调用者传播，多项错误聚合。工厂仍须自行移除其安装的订阅；控件无法推断应用事件处理器。清理方法应能完成自己的资源释放后再抛错，不要依赖异常后仍重用旧容器。

## 运行时切换与模板

`grid.ElementFactory = otherFactory` 或 `null` 重新实现可见容器，保留逻辑选择、当前地址、滚动位置和同一个编辑会话。切换本身不提交、不取消、不写模型；新容器重新显示已有草稿。源或列同时失效时仍执行既有身份保护，不保证保留已无效的编辑。赋回同一个工厂实例不会新建会话；切换到其它实例后再切回来会建立新会话。

自定义工厂不能替代默认输入、焦点、编辑和自动化协议。修改单元格模板时保留 Content/ContentTemplate 的 TemplateBinding，编辑模板继续绑定暂存会话；保留需要的状态显示及命中区域。保留列头排序、调宽和重排输入。使用标准宿主可以继续获得逻辑身份自动化 peer，但不等于已验证真实读屏或原生 Grid/Table pattern。

工厂负责撤销自己设置的临时属性（例如 Tag、局部样式和事件）。核心负责清理其模型绑定、内容和编辑器。局部 Template/Background 等值优先于主题，是否保留由工厂自行决定；恢复默认工厂会创建标准容器，不沿用旧工厂的局部值。

## 最小使用示例

```csharp
public sealed class AppElementFactory : TreeDataGridElementFactory
{
    public override Control CreateElement(TreeDataGrid owner, TreeDataGridElementKind kind)
        => base.CreateElement(owner, kind);

    public override void PrepareElement(TreeDataGridElementContext context)
        => context.Element.Tag = context.Kind.ToString();

    public override void ClearElement(TreeDataGridElementContext context)
        => context.Element.Tag = null;
}
```

工厂类型须可被 XAML 创建。将命名空间分别映射到功能程序集和应用后，可以在资源中实例化并赋值：

```xml
<Window.Resources>
  <local:AppElementFactory x:Key="AppFactory" />
</Window.Resources>
<nt:TreeDataGrid ElementFactory="{StaticResource AppFactory}" />
```

完整可运行的订阅清理、模板定制与 A/B/默认切换见统一 Demo 的“自定义与模板 → 元素工厂与容器回收”。[独立功能包消费者](../scripts/Test-TreeDataGridFunctionConsumer.ps1)包含实际编译 XAML、应用自有模板、三角色工厂、源更换和草稿提交验证。[A5 验收记录](treedatagrid-alignment-a5-status.md)单独记录测试、包消费与性能边界。
