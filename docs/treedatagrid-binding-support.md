# TreeDataGrid 受控绑定与暂存写回

更新日期：2026-10-13。固定验证依赖为 Avalonia 12.0.3 / .NET 10。以下描述 B2 / B3 当前实现；自动写回始终先转换、再校验，最后调用一次 writer。取消与转换 / 校验失败不调用 writer。

| 接入方式 | 显示与通知 | 暂存写回 |
| --- | --- | --- |
| `ValueGetter` / `ValueSetter` | getter 读取模型；Source 负责模型刷新 | 显式 setter，应用承担一致性契约 |
| `SetValueAccessors<TModel,TValue>` | 类型化 getter；自动设置 `ValueType` | 有 setter 时可写；省略 setter 时只读 |
| 简单或嵌套 `ReflectionBinding` | Avalonia 显示绑定跟踪各层 INPC 与对象替换 | Default / TwoWay、公共 getter / setter、非 null 引用对象路径支持自动写回 |
| OneWay `ReflectionBinding` | 支持显示 | 自动写回关闭；可显式提供 setter / 类型化 accessors |
| 带 converter、相对源或元素源的绑定 | 由 Avalonia 提供显示 | 需要显式 setter；不自动调用 converter 的 ConvertBack |
| `CompiledBinding` / compiled XAML | 支持显示和嵌套通知；有编译 XAML 回归样例 | 需要显式 setter / 类型化 accessors，不直接更新编译绑定表达式的源 |
| 层级 Children / HasChildren / IsExpanded binding | 点分公共引用对象属性路径，跟踪每层 INPC | 展开布尔叶属性必须有公共 setter；空中间对象时写入给出诊断 |

`SetValueAccessors` 用于绑定显示与业务写入分开的场景。显示绑定继续生效，编辑读取类型化 getter 的原始值，避免把格式化显示值当作原始值。getter-only accessors 明确保持只读。

```csharp
var column = new TreeDataGridTextColumn { Binding = compiledNameBinding };
column.SetValueAccessors<MyRow, string>(row => row.Detail.Name,
    (row, value) => row.Detail.Name = value);
```

层级绑定可使用 `ChildrenBinding="{ReflectionBinding Container.State.Children}"`，对应 HasChildren 与 IsExpanded 使用相同对象路径。子集合为 null 或中间对象为 null 时视为空层级；HasChildren / IsExpanded 读取为 false。中间对象恢复、任意相关层级的 PropertyChanged、子集合替换后，适配器更新源并立即解除旧对象与集合订阅。移除模型和清空 ItemsSource 也解除嵌套订阅。根模型仍由 Source 订阅，中间对象由适配器跟踪，通知必须发生在 Source 创建线程。

层级路径不支持索引器、converter、StringFormat、fallback、相对源、元素源或值类型中间节点；Children / HasChildren 使用 Default / OneWay，IsExpanded 使用 Default / TwoWay。无效成员、非布尔状态、非集合 Children 或不可写展开属性提供明确异常，失败的新 ItemsSource 不替换既有有效源。

嵌套路径变化沿用当前 Source 的完整图刷新。路径订阅根据已提交的源路径图维护，包含折叠和筛选节点；内部展开 writer 期间的中间对象通知延后到投影发布，不重新进入展开事务。这是功能接入能力，不将其声明为增量更新性能优化。

暂存期间引用对象若替换，自动 ReflectionBinding writer 拒绝把旧草稿写入新对象，且零写回；目标为 null 或变为只读也拒绝提交。取消旧草稿后可针对当前对象开始新编辑。对象属性 getter 应无副作用。任意业务 setter、converter、validator 和自定义 writer 应遵守成功提交或失败前不修改模型的契约；显式 writer 应自行检查自己捕获的业务目标是否仍有效，已经发生的业务副作用不能由 TreeDataGrid 通用回滚。

已核实 Avalonia 12.0.3 随包公开 XML：`CompiledBinding` 支持显示绑定表达式和公开的 `Create<TModel,TValue>` 工厂，但本实现不依赖其内部表达式节点，也不尝试把 DataContext 绑定直接作为草稿编辑器的双向绑定。编译 XAML 直接消费、取消、转换、验证和显式 typed writer 均由回归场景验证；最终通过结果见阶段验证记录。
