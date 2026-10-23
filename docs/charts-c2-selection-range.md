# Charts C2-04：系列选择、范围选择器与多图联动

更新日期：2026-10-17。本批已实现当前 Line/Bar/Scatter 的系列选择、可独立使用或连接图表的横竖范围选择器，以及多图主轴视图同步，已接入 Demo 和 Usage。Windows 与 Ubuntu 26.04 WSLg 均完成当前功能范围的正式验证，结果如下。百万点命中尾延迟继续按用户决定后置 C4，本批不测性能时序或内存门禁。

## 系列选择

| 能力 | 当前契约 |
| --- | --- |
| 模式 | `SeriesSelectionMode` 使用现有 `ChartSelectionMode` 的 `None/Single/SingleDeselect/Multiple`，默认关闭，与点选择的 `SelectionMode` 独立。 |
| 输入与 API | 点击命中系列；Multiple 下 Ctrl 点击切换，SingleDeselect 下再次点击取消。`SelectSeries(series, additive)` 的重复调用幂等，`ClearSeriesSelection()` 清除系列状态；沿用键盘导航开关。 |
| 身份与绑定 | `SelectedSeries` 是 TwoWay 的系列引用列表，规范化为不可变快照，过滤外部、不属于当前图表、重复或隐藏的系列；选择系列不会制造或枚举整组 `SelectedPoints`。 |
| 前后事件 | `SeriesSelectionChanging/Changed` 提供旧/新快照和原因。用户/外部请求可以取消，隐藏、删除及模式变化导致的强制清理不可取消；业务回调中的较新选择优先，取消后保留绑定并回写规范状态。 |
| 显示 | 选中任意系列后，以 `UnselectedSeriesOpacity` 淡化未选系列，默认 `.25`；没有系列选中时保留正常透明度。此状态不改变原数据、轴范围或图例显隐。 |

当前验证范围是已有三类笛卡尔系列。源替换后按系列实例引用协调状态，同一业务对象出现在不同系列中不等于同一系列；后续扇区、单元格、堆叠与聚合图族仍需各自定义和验证。

## 范围选择器与图表连接

`ChartRange(Lower, Upper)` 表示完整轴范围变换后的递增比例，严格有效范围为 `0 <= Lower < Upper <= 1`，`Span` 为两端差值。`ChartRangeSelector.Range` 是原子 TwoWay 值；本地请求被约束到 `[0,1]` 和 `MinimumSpan`，两个端点不会交换或合并。

| 能力 | 当前契约 |
| --- | --- |
| 独立使用 | `Orientation` 控制横竖布局，水平默认向右递增、垂直默认向上递增；`IsDirectionReversed` 只改变呈现和方向键方向。 |
| 指针 | 两端拖柄调整端点，窗口内部拖动保持跨度；可观察 `ActivePart/IsDragging`，通过 `RangeChanged` 提供旧/新范围及 `IsUserInitiated`。 |
| 键盘 | 选择器窗口和两端柄可聚焦；方向键使用 `SmallChange`，PageUp/PageDown 使用 `LargeChange`，Home/End 移到相应边界，拖动中 Escape 请求恢复开始范围。 |
| Chart 连接 | `Chart` 可选，`Axis` 只接受主 X 或主 Y，与显示方向独立；附着时读取图表实际范围，图表是权威来源，范围条写入后再读取轴约束后的实际结果。 |
| 轴与跨度 | 对数、日期、逆轴和断轴使用完整轴的实际变换比例，尊重各轴最小/最大 zoom。外部图表缩放可以小于选择器的本地 `MinimumSpan`；此时窗口拖动保留实际跨度，移动端柄重新请求本地最小跨度。 |
| 生命周期 | 外部范围/配置变更、禁用、隐藏、尺寸变化、失去捕获及卸载会结束相应拖动；连接的图表订阅在选择器附着期间存在，卸载时解除，旧回调不得覆盖较新 Range/Chart/Axis。 |

`CartesianChart` 新增 `GetNormalizedViewport()/SetNormalizedViewport(viewport, axes)` 和 `GetVisibleFraction(axis)/SetVisibleFraction(range, axis)`。分数按数据递增方向定义，`IsInversed` 不反转 API 的 Lower/Upper；显示方向与轴归属是不同配置。完整变换包含断轴压缩，对数轴的半范围按指数位置计算。

```csharp
var horizontal = new ChartRangeSelector
{
    Chart = firstChart,
    Axis = ChartInteractionAxes.X,
    MinimumSpan = .05,
};
firstChart.SetVisibleFraction(new ChartRange(.2, .8));
```

当前交付的是独立范围控件及图表连接，可以在应用布局中放到图表旁边；内嵌数据概览、缩略预览和任意图族专属范围模板不计入已完成范围。

## 多图主轴范围联动

显式创建 `ChartSynchronizationGroup`，使用 `Add/Remove/SynchronizeFrom/Dispose` 管理成员。默认 `Axes=X`、`CoordinateMode=Normalized`；可选择 `None/X/Y/Both`。添加成员或修改模式不会主动选择源图，初始化及切换模式后可调用 `SynchronizeFrom`。这组 API 在 UI 线程使用。

```csharp
using var group = new ChartSynchronizationGroup
{
    Axes = ChartInteractionAxes.X,
    CoordinateMode = ChartSynchronizationCoordinateMode.Normalized,
};
group.Add(firstChart);
group.Add(secondChart);
group.SynchronizeFrom(firstChart);
```

- `Normalized`：将源图的主轴比例窗口映射到各目标自己的完整轴范围，适用于不同 extent、对数/日期/逆轴/断轴；目标各自执行 zoom 限制。
- `DataCoordinates`：复制主轴原始数值，再按目标轴域约束，不进行单位换算、类别语义匹配或时区转换。
- 未选中的轴保留当前实际窗口。目标同维度的额外轴按自身完整范围和变换跟随相同比例，已有显式 ViewMinimum/ViewMaximum 也会更新；不共享不同图表的轴实例。
- 成员以弱引用注册。图表或祖先隐藏、图表卸载时解除 group 的视图订阅并暂停参与；附着期间保留必要的祖先可见性观察以恢复参与，卸载时解除这些观察。重新激活不会主动选择源；`Dispose` 注销全部成员。
- 自己的同步写入不回环；业务绑定或回调重入时采用较新窗口，删除成员、切换配置或 Dispose 后不继续提交旧请求。矛盾回调连续 64 次仍不收敛会抛出 `InvalidOperationException`，不无限投递 dispatcher 任务。

无变化的全域同步保留 `Viewport=null`。所有维度回到完整 extent，且主轴没有显式 `ViewMinimum/ViewMaximum` 时恢复自动视图，后续数据扩容继续自动取全域。若只同步 X 全域而 Y 仍为手动窗口，现有四界 `ChartViewport` 仍须保留显式窗口，不能表达每轴独立 nullable 自动范围；该 X 窗口随后不会自动随 extent 扩张。主轴显式 View 配置保留，此时完整比例请求可以由显式 Viewport 覆盖其显示范围；同维度的额外轴显式 View 会随同步更新。

本期多图联动仅指主轴视图范围，不提供跨图游标、共享 Trackball、系列选择同步或全进程刷新调度。范围变更、源 extent 变化和 axis 配置变更可以触发同步，不代表多图性能预算已验收。

## Demo、Usage 与验证

统一 Demo → Charts → **“选择与范围：系列选择、双向范围条与多图联动”**，展示三系列选择和取消、横竖范围条、两张不同 X extent 的图、同步坐标模式和按轴切换、视图重置。旧 Trackball、图例/标签及坐标轴示例继续保留。[Charts.Usage](../samples/Charts.Usage/MainWindow.axaml) 提供编译绑定接入，并由真实包消费者复用对应样例。

正式验收状态：**当前三系列功能范围通过**。两平台 Release 构建均为零警告、零错误；原始报告分别关联冻结输入、程序集与包消费者。

| 最终运行 | Charts 测试 | 原生程序化 Demo | 独立 Usage | 真实 nupkg 消费者 |
| --- | ---: | ---: | ---: | --- |
| Windows 本机 | 676 | 256 | 98 | 功能包、功能+主题包各 118 |
| Ubuntu 26.04 x64 / WSL2、WSLg XWayland | 676 | 256 | 98 | 功能包、功能+主题包各 118 |

最终源码输入、程序集、构建警告/错误、命令、逐项结果、全新消费者缓存和文件 SHA 由[本批验证清单](charts-c2-selection-range-validation.json)关联；公开声明由[本批 API 快照](charts-c2-selection-range-api.txt)记录。两平台使用同一声明输入独立构建、测试和消费当前 nupkg，不继承 C2-03 的通过数量或性能资格。

回归场景包括系列引用与业务点身份分离、三类系列淡化、选择取消/TwoWay 回写、源隐藏/移除、范围原子更新和限制反馈、方向/键盘/指针捕获、主/额外轴变换、不同 extent 的 normalized/raw 同步、按轴重置、自动范围扩容、可见性/卸载/Dispose、回调重入及不收敛保护。Windows 与 WSLg 已分别运行相同契约；原生窗口程序化输入、RenderTargetBitmap/PNG 与物理设备、系统桌面截图和 compositor present 分开记录。

历史 [C2-03 Trackball](charts-c2-trackball.md)、C0 和 [v15 尾延迟](charts-tail-latency.md)及其 JSON/API 保留原值。本批不运行性能时序池或内存回归，不把历史 100/100 controlled、旧像素对照或旧内存样本当成当前代码资格。


冻结源码 **1231 文件**，每平台 **556 个声明输入**；两平台、当前运行输入与冻结副本哈希一致。API 为只增加声明，既有声明未删除或改变。消费者使用各自新的隔离缓存，功能包与主题包各自执行锁定恢复、编译绑定和真实包运行。原生检查使用合成路由输入；图像为 RenderTargetBitmap/PNG，未宣称物理输入、桌面截图或 compositor present。

保留前次 v1 报告：两平台基础验证均通过，隔离消费者的一条精确浮点范围断言失败；最终 v2 保留独立期望值，使用明确的 1e-8 数值精度并保存实际读数，产品 C# 没有因此改动。包内 README 的额外轴说明与残留待实现文字已修正。开发阶段的失败 TRX、受限测试宿主启动中止及早期透明背景图片保留，不作为最终通过证据。

## 剩余问题与下一步

- 历史证据：本轮核对发现 v15 引用的 **4 个原始 BarCache.PixelProbe.dll 路径缺失（8 处历史引用）**，原报告文件和记录的哈希保留。见[缺失清单](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c2-selection-range-20261017/historical-reference-gaps-v2.json)。该缺口保持开放，不用重新编译的文件冒充原始产物；本轮 C2-04 的当前证据完整，不受这 4 个旧探针缺失影响。


- 下一批 C2-05：精密触控板、触摸捏合/平移、滚动容器嵌套、手势仲裁、捕获/失焦取消及真实设备输入；当前键盘和合成指针结果不代表输入完整性阶段完成。
- 范围与同步：每轴独立自动范围、内嵌数据概览/缩略预览、跨图游标/Trackball、选择同步，以及未来曲线/堆叠/圆形/LOD 图族的选择与范围语义仍需单独设计和验证。
- C2 开放项：类别图例/虚拟化、标签连接线与高级避让、完整前置事件、动画、JPEG/异步导出、点级辅助语义及真实读屏。
- C4：百万点命中尾延迟、全部历史性能残余、线段查询/多浮层、大量系列选择和多图范围更新的成本、增量/LOD、内存及长期运行；本轮均未取得新的正式性能资格。
- 平台：Ubuntu 22.04/24.04 实机、Windows/Linux 物理输入、DPI/跨屏、UIA/AT-SPI、实际 GPU/backend/present、原生 Wayland/ARM 与官方许可运行参考仍待独立验收。Ubuntu 26.04 WSLg 不能替代物理 Linux 目标。

本轮源码完成不表示 C2/C4 整阶段或完整官方功能对标已关闭。后续顺序和出口继续由[开发计划](charts-development-plan.md)、[功能矩阵](charts-official-alignment.md)及[性能与平台计划](charts-performance-platform-plan.md)约束。
