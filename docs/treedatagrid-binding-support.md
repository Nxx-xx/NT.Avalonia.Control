# TreeDataGrid 受控绑定与暂存写回

更新日期：2026-10-23。验证依赖为 Avalonia 12.0.3 / .NET 10，SDK 10.0.402。本文描述 A3 的受控实现；完整证据见 [A3 记录](treedatagrid-alignment-a3-status.md)。

## 当前支持矩阵

“自动”表示控件捕获公共模型属性目标，不需要业务 setter；不表示任意 Avalonia 绑定都可逆。“显式 writer”可用 ValueSetter 或 SetValueAccessors 接入，应用负责业务目标一致性。

| 接入方式 | 显示/通知 | 暂存写回状态 |
| --- | --- | --- |
| ValueGetter / ValueSetter | getter 读取；Source 负责模型通知 | 显式 writer |
| SetValueAccessors<TModel,TValue> | 类型化原始值 getter；显示绑定继续运行 | 显式 writer；省略 setter 明确只读 |
| 普通/嵌套 ReflectionBinding，公共引用属性链 | Avalonia 跟踪通知与中间对象替换 | **自动**；Default / TwoWay，公共叶 getter/setter，中间对象非 null |
| 带 Converter 的上述反射绑定 | 显示转换独立运行 | **自动**；草稿用正向转换初始化，ConvertBack 后校验，最后一次写入 |
| TreeDataGridBinding.Create 的 CompiledBinding | 真正的 Avalonia 编译显示绑定 | **受控适配器自动**；从原始 LINQ 表达式保留公共成员与常量索引，不读取绑定私有节点 |
| 编译 XAML + TreeDataGridBindingAdapter | 真正的编译绑定显示与通知 | **受控适配器自动**；Path 是作者明确声明的权威模型目标 |
| 原生 CompiledBinding / 编译 XAML，未注册适配器 | 支持显示与嵌套通知 | **不支持通用自动定位**；使用适配器或显式 writer；GetEditBindingError 给出诊断 |
| OneWay / OneTime / OneWayToSource | 按 Avalonia 的显示语义 | 自动编辑关闭；可使用显式 writer |
| 反射绑定显式 Source | 显示外部对象 | **自动**；捕获外部根对象与整条目标链，替换 Source 后拒绝旧草稿 |
| 工厂/适配器编译绑定显式 Source | 显示外部对象 | 与受控路径相同；更换 Source 或配置后拒绝提交 |
| ElementName / RelativeSource | 可由 Avalonia 提供显示，取决于实际视觉锚点 | **显式 writer**；列元数据没有稳定视觉定位契约 |
| 常量整数/字符串索引经过引用对象，再访问公共叶属性 | 按绑定提供显示；已测试列表、字典和一维数组 | **自动**；索引处引用对象更换后拒绝旧草稿；索引参数不从诊断字符串推断 |
| 索引槽本身写入，例如 Items[0] | 可显示 | **显式 writer**；不推断集合槽的持久身份 |
| 值类型根模型/中间节点、字段、计算表达式、方法、动态索引 | 按原绑定可提供显示 | **显式 writer**；工厂对计算/动态路径给出明确异常 |
| 工厂表达式成员被运行时隐藏/覆盖 | 编译绑定自身可显示 | 不猜测同名成员；自动定位拒绝，使用明确声明的适配器路径或显式 writer |

Binding 的 StringFormat 仅用于显示；没有 converter 时编辑原始值，不尝试解析格式前缀。FallbackValue / TargetNullValue 不会变成自动编辑目标；中间目标为 null 时不能开始自动编辑。

## 两种受控编译绑定入口

代码入口保留表达式中的确切成员和常量索引信息，再创建 Avalonia 的 CompiledBinding：

```csharp
var binding = TreeDataGridBinding.Create<MyRow, string>(row => row.Detail.Name);
var column = new TreeDataGridTextColumn { Binding = binding };
// 不需要 ValueSetter。
var indexed = TreeDataGridBinding.Create<MyRow, string>(row => row.Items[1].Name);
```

实际编译 XAML 可使用适配器包装绑定：

```xml
<tg:TreeDataGridTextColumn
    Binding="{tg:TreeDataGridBindingAdapter Path=Detail.Name, Binding={CompiledBinding Detail.Name}}" />
```

TreeDataGridBinding.Attach(binding, "Detail.Name") 提供等价的显式注册方式。注册以绑定对象为弱键，不为所有行创建编辑订阅。Path 是明确声明的编辑/层级路径，不是对原生表达式节点等价性的推断；作者负责让它与绑定的业务意图对应。它始终以行模型或受支持的显式 Source 为根，不解析视觉祖先/元素名。注册路径、原绑定对象、CompiledBinding.Path 或转换配置在编辑期间改变会使旧会话失效。

工厂保留公共 PropertyInfo，定位时核对声明成员与运行时成员，避免把基类编译属性误认成派生类同名新属性。允许的路径是公共属性和常量整数/简单字符串索引；不编译执行任意索引表达式来“猜”目标。模型成员访问使用公共反射进行安全捕获；编译显示绑定仍由 Avalonia 执行，不声明捕获过程完全无反射或具有增量性能优势。

未注册的原生编译绑定保留显示支持，自动编辑返回 false，可调用 column.GetEditBindingError(model) 查看原因。原有显式写回依然可用：

```csharp
column.SetValueAccessors<MyRow, string>(row => row.Detail.Name,
    (row, value) => row.Detail.Name = value);
```

显式 accessor 的 setter 由应用提供；绑定的 ConvertBack 不会隐式叠加到该业务映射上。需要自定义转换时配置 ValueConverter。getter-only accessors 仍为只读。

## 转换、草稿与提交

自动绑定编辑会捕获原始模型值作为 OriginalValue；有绑定 converter 时，用 Convert 初始化独立草稿。显示绑定仍留在单元格上，草稿从未作为 TwoWay 业务绑定目标。

提交顺序是：校验捕获配置/目标 → 转换草稿 → 再校验 → validator → 再校验原目标/源/节点/列 → 一次 writer。默认类型转换使用实际叶属性类型；有 ConvertBack 时传入实际模型类型（含 nullable）、ConverterParameter 和 ConverterCulture，未指定文化时使用提交时的 CurrentCulture。ValueConverter 优先于绑定 ConvertBack。

UnsetValue、BindingOperations.DoNothing、BindingNotification、转换异常和错误返回类型都表示未提交：不调用 validator/writer，保留草稿和 ValidationError。非 nullable 值属性拒绝 null；不会让反射 setter 把 null 静默变成 0。validator 拒绝时保留原草稿，允许修改后重试。取消不调用 ConvertBack 或 writer。

转换回调也进入 State 回调保护，直接递归选择/编辑会被拒绝；回调换源、删节点、隐藏列或替换目标会使旧操作失效。已发生的任意业务 converter/setter 副作用不作通用回滚承诺。成功 writer 后观察者异常仍遵循 A2：模型已经提交，不重试写入，不伪装为验证失败。

自动 writer 在转换前后和最终写入前验证完整引用链、公共成员和索引位置处的对象；不仅比较叶属性名。编辑器被横纵虚拟化回收不改变会话目标。新引用恢复后须取消旧草稿并重新编辑。

## 层级绑定

Children / HasChildren / IsExpanded 支持点分公共引用属性路径，反射绑定与受控编译适配器共用层级图契约：

```xml
<tg:TreeDataGridHierarchicalExpanderColumn
    ChildrenBinding="{tg:TreeDataGridBindingAdapter Path=State.Children, Binding={CompiledBinding State.Children}}"
    HasChildrenBinding="{tg:TreeDataGridBindingAdapter Path=State.HasChildren, Binding={CompiledBinding State.HasChildren}}"
    IsExpandedBinding="{tg:TreeDataGridBindingAdapter Path=State.Expanded, Binding={CompiledBinding State.Expanded, Mode=TwoWay}}" />
```

Children / HasChildren 使用 Default / OneWay，IsExpanded 使用 Default / TwoWay。子集合或中间对象为 null 时视为空层级，布尔状态读取为 false。对象恢复、嵌套 INPC、子集合替换后更新投影并解除旧对象/集合订阅；折叠和过滤节点同样维护路径订阅。移除模型和清空 ItemsSource 后解除订阅。

层级路径不支持索引器、converter、StringFormat、fallback、外部/元素/相对源和值类型中间节点。非布尔状态、非集合 Children、不可写 Expanded 和未注册编译路径提供诊断，失败配置不替换既有有效源。嵌套变化仍使用完整图刷新；内部展开 writer 的通知延后处理，不声明为增量更新优化。

## 原生编译绑定的技术边界

Avalonia 12.0.3 的 CompiledBindingPath 公开面没有目标枚举器；ToString 是诊断文本。实测 Items[0].Name 和 Items[1].Name 均输出 Items.Item.Name。公开 UpdateSource 会跟随当前表达式目标：原型在替换中间对象后调用它，旧对象写入 0 次，新对象却收到一次旧草稿。仅将 UpdateSourceTrigger 设为 Explicit 不提供目标身份锁。

可复现的[公开 API 清单](../artifacts/treedatagrid-alignment/a3-completion-20261023/prototype/public-api.txt)、[行为结果](../artifacts/treedatagrid-alignment/a3-completion-20261023/prototype/public-binding-proof.json)和[原型源码](../artifacts/treedatagrid-alignment/a3-completion-20261023/prototype/Program.cs)保留在 artifacts。原型和产品都没有读取绑定私有字段/内部节点，也没有升级依赖。

因此，受控适配器路径的交付与“任意原生编译绑定自动安全写回”分开记录。后者仍是 A3.1 的未完成扩展项，需要上游提供可捕获目标的公开能力或进一步正式适配契约；不能把显式 setter 或适配器声明记为任意绑定自动写回完成。
