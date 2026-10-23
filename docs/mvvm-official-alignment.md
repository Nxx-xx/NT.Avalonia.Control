# MVVM 入口与 Avalonia TreeDataGrid v12 对照

核对日期：2026-10-12。本项目为 `NT.AvaloniaControl.TreeDataGrid` 的独立实现；下表依据官方公开文档核对入口形态，不代表商业 TreeDataGrid 包的实现、兼容性或全部行为已经复现。

## 官方依据

官方 v12 迁移文档给出了 `ItemsSource` 搭配 XAML 内容列，以及层级列三个绑定属性的用法；非泛型列、常规 Avalonia 绑定是 v12 的公开设计方向。该页标示最后更新为 2026-04-07。[v12 breaking changes](https://docs.avaloniaui.net/controls/data-display/structured-data/treedatagrid/breaking-changes-v12)

官方 API 将 `ItemsSource` 定义为 `IEnumerable`，说明其自动创建并设置 Source，与直接指定 Source 互斥；另公开 SelectedItem、SelectedIndex、Selection 和 SelectionMode 等入口。[TreeDataGrid API](https://docs.avaloniaui.net/api/avalonia/controls/treedatagrid)

## 本项目支持及差异

| 入口 | 当前实现 | 差异或限制 |
| --- | --- | --- |
| ItemsSource | `IEnumerable?`，支持 ViewModel 属性绑定及集合替换，自动创建 `FlatTreeDataGridSource<object>` 或层级适配源 | 为事务校验使用 DirectProperty；官方 API 将其列为 StyledProperty。本项目保留原 Source StyledProperty，不改变已发布字段类型。 |
| Source | 仍接受应用创建的 Source；自动模式下返回控件拥有的适配 Source | 显式 Source 由应用负责 Dispose。控件不会静默用 ItemsSource 覆盖显式源。 |
| XAML 列内容 | Columns 标记为 Content；支持直接子列，也保留显式 `<TreeDataGrid.Columns>` | Columns 仍是控件的 ObservableCollection；不复制官方 Source 内列集合及 fluent API。 |
| 层级列内容 | InnerColumn 标记为 Content，支持嵌套文本/复选框/模板列 | 不允许多层 expander 嵌套；至多一列定义自动数据入口的层级绑定。 |
| ChildrenBinding / HasChildrenBinding / IsExpandedBinding | 使用 `[AssignBinding] BindingBase?` 元数据，支持简单模型属性 ReflectionBinding | 本轮拒绝 compiled、嵌套路径、转换器、外部 Source、RelativeSource、ElementName；IsExpanded 属性须为可读写 bool，Mode 为 Default/TwoWay。 |
| 单元格 Binding | 文本/复选框绑定显示；简单 ReflectionBinding 支持已有编辑写回 | compiled 单元格绑定可以显示；自动推导写回仍需受支持的 ReflectionBinding 或显式 ValueSetter，不宣称任意绑定均可写回。 |
| SelectedItem / SelectedIndex | 控件层增加可绑定选择入口，保留基于身份的 State | 本项目 IndexPath、选择状态类型和默认值属于自身契约；不是官方类型的二进制替代。 |
| 选择模式 | 保留 `SelectionUnit=Row/Cell`、`SelectionMode=Single/Multiple` | 与官方示例合并标志 `SelectionMode="Row,Multiple"` 不同，避免破坏已有枚举。 |
| 当前单元格与编辑命令 | CurrentCell、TreeDataGridEditCommands 及编辑状态绑定由本项目扩展提供 | 这些是项目扩展，不归为上述官方 API 的逐项复刻。 |

官方条目依据为上述两份文档；本项目实现状态依据当前源码和专项测试。排序、过滤、编辑事件等既有契约分别见 `p5-projection.md` 和 P4/P5 验收记录。

## XAML 用法

```xml
<tg:TreeDataGrid ItemsSource="{Binding Records}"
                 SelectionUnit="Row" SelectionMode="Multiple">
    <tg:TreeDataGridTextColumn Header="名称"
                              Binding="{ReflectionBinding Name}" />
    <tg:TreeDataGridCheckBoxColumn Header="启用"
                                  Binding="{ReflectionBinding Enabled}" />
</tg:TreeDataGrid>
```

```xml
<tg:TreeDataGrid ItemsSource="{Binding Roots}">
    <tg:TreeDataGridHierarchicalExpanderColumn Header="名称"
            ChildrenBinding="{ReflectionBinding Children}"
            HasChildrenBinding="{ReflectionBinding HasChildren}"
            IsExpandedBinding="{ReflectionBinding Expanded, Mode=TwoWay}">
        <tg:TreeDataGridTextColumn Binding="{ReflectionBinding Name}" />
    </tg:TreeDataGridHierarchicalExpanderColumn>
</tg:TreeDataGrid>
```

外围 ViewModel 绑定可以使用 compiled binding；上述直接层级属性要显式选择 ReflectionBinding。Children 属性必须返回 IEnumerable 或 null。HasChildren 可选，实际非空孩子与提示值按 OR 判断。IsExpanded 可选；未绑定时沿 Source 的独立 occurrence 展开状态，有绑定时共享对象的 occurrence 同步。INotifyPropertyChanged 驱动属性替换和展开变化；INotifyCollectionChanged 驱动集合增删移动。非通知集合或模型改动后调用自动 `Source.Refresh()`。

## 所有权、身份和错误

ItemsSource 非空时，自动 Source 由控件管理；禁止再通过 Source CLR setter 指定外部源。使用 Avalonia SetValue 或绑定提供非空外部 Source 同样拒绝，保留有效源；属性系统重算默认 null 时继续使用自动源。切换入口时先清空当前入口：`ItemsSource = null` 后设置 Source，或 `Source = null` 后设置 ItemsSource。

自动 Source 替换通过 SetCurrentValue 更新 Source，不移除原 Avalonia 绑定表达式。ItemsSource 新值先完成图校验和适配，再替换公开属性；无效新集合（如含 null、祖先循环或过深图）不会替换原有效集合和 Source。新的层级绑定配置会创建新的 Source，因而重新分配身份；普通显示列增删、调整顺序不会重建 Source。

控件视觉卸载沿既有约定解除 UI/State 的活动订阅，保留自动 Source 及其模型订阅，使重挂载时的 NodeId、选择和编辑草稿继续有效。模型在卸载期间仍可更新自动 Source。替换或清空 ItemsSource 会释放旧源、集合转发和模型订阅；永久弃用一个仍绑定长寿命集合的控件时，应清空 ItemsSource。外部 Source 不受此释放策略影响，由其应用所有者管理。

若 ViewModel 在编辑 reader、validator 或 writer 回调中替换 ItemsSource，旧适配源会等该回调和刷新 scope 完整退出后释放，不依赖 Dispatcher 推迟。旧编辑停止继续访问或刷新旧源并返回失败；已经执行的用户 writer 副作用不能回滚，新集合中的模型不会被旧编辑写入。

非泛型集合适配保留原模型引用和集合通知索引；重复对象仍具有独立 occurrence 身份。装箱值使用稳定 occurrence 缓存，使普通刷新、索引 Move 和插入不无故替换存活节点。线程约束、循环/256 层限制、Replace/Reset 身份规则、延迟刷新和异常恢复继续沿 Core 契约。

## 验证

独立 `ItemsSourceBindingTests` 覆盖 ViewModel 属性替换、集合 INCC 和模型 INPC、重复模型与装箱值的身份、Source/ItemsSource 冲突、无效替换、已有 Source 绑定表达式保留、普通列变化不重建、层级绑定写回、子集合替换退订、共享模型展开，以及卸载/重挂载保留身份选择草稿。AXAML 实际编译与 Demo 加载由独立 MVVM Demo 线验证；最终测试数和统一 Release 结果以根任务验收为准。

2026-10-12 的独立 Debug 构建为 0 警告、0 错误；18 项 ItemsSourceBindingTests 全部通过（直接 xUnit runner，0.505 秒），包括 compiled ViewModel ItemsSource 绑定与编辑 reader/validator/writer 内部换源的定向回归。层级 compiled binding 会明确拒绝。
