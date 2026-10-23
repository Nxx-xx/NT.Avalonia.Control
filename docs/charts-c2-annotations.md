# Charts C2-06：标注与参考元素

更新日期：2026-10-17。本批补齐当前 Line/Bar/Scatter 的圆角矩形、形状标签、椭圆数据半径、标注专用轴和绘图区内容，加入 Demo、编译 AXAML/Usage 和隔离包消费者。Windows 与 Ubuntu 26.04 WSLg 均已通过本批当前三系列功能验证；旧 C2-05 报告保留原身份。

## 形状与标签

- `RectangleAnnotation.CornerRadius` 为有限非负 DIP，默认 0；实际圆角限制为投影后短边的一半。现有 X1/Y1/X2/Y2 两角点接口保留。
- 三种形状共用 `Label`、`LabelPosition` 和 `LabelOffset`。位置默认 Center；九宫位置在投影包围框内对齐，Start/End 以原始端点居中，偏移为 DIP。文字继承标注/图表字体、字号与颜色；矩形/椭圆无填充和描边时仍可单独显示标签。箭头的线条保留前景画刷回退。
- `EllipseAnnotation.GeometryMode=EndpointBounds` 保持两角点屏幕椭圆默认行为。`CenterRadius` 使用 X/Y/RadiusX/RadiusY（默认 0/0/1/1），Data 为轴单位，Pixel 为绘图区 DIP。RadiusX/Y 拒绝非有限或负值；半径为 0 时跳过几何与标签，X/Y 或投影无效、几何退化时也跳过。
- CenterRadius 投影 `(X±RadiusX,Y±RadiusY)` 得到屏幕包围框，再绘制其中的椭圆。非线性轴下视觉中心不一定等于 Project(X,Y)，不将屏幕椭圆称为非线性映射后的数据圆。此模式的标签 Start/End 使用视觉左上/右下角。
- 箭头仍保留原始端点、固定 DIP 头部、虚线杆/实线头、短线收敛和断轴裁剪；缩放不改变头部与标签偏移的逻辑尺寸。

## 多轴与参考元素

数据标注的 XAxisKey/YAxisKey 可以激活没有系列引用的附加轴，无需透明占位系列。Axes 中的轴由系列或标注确定维度；完全未引用的声明轴不参与场景。标注坐标不加入系列自动范围，专用轴使用自身显式范围或默认范围。

系列声明（包括隐藏系列）固定轴维度。未知、全局重复、错误维度或互相冲突的标注专用轴请求跳过，不回退主轴。Pixel 标注忽略 Key，不激活数据轴。源替换、Key/坐标单位/可见性变化更新布局并取消旧导航；样式和几何数值变化保留导航。

专用轴与系列轴使用同一场景尺度，支持独立视图、反向、对数/日期/类别与断轴。未显式配置 View 的附加轴跟随主轴归一化窗口；显式 ViewMinimum/ViewMaximum 保持自身窗口。线、带、文字、自定义、形状及该轴 PlotBands 一致；原前后层次和裁剪保持。默认 Data 裁剪去掉断轴区，Pixel 只按绘图区裁剪。

## 绘图区内容

PlotAreaContent 默认 null；直接放未被父级使用的 Control 时继承图表业务 DataContext。其它对象配合 PlotAreaContentTemplate（IDataTemplate），模板上下文为 ChartPlotAreaContext：Content 和 DataContext 为当前对象/业务上下文，PlotArea 为图表局部绘图区，Projection/Project 输出绘图区局部 DIP，VisibleRange/Extent 为主轴窗口/完整范围；TryGetProjection/TryGetVisibleRange 解析当前场景唯一且维度正确的具名轴，空 Key 使用主轴，失败不回退。

内容为被动层，位于自绘图形上方、控件标签/图例/读数下方，不抢指针、滚轮、触摸或 Tab。整体按 PlotArea 矩形裁剪，不用某一组轴的断轴带切掉整个 UI；模板应跳过非有限投影锚点。无模板的非 Control 对象、null 内容或 null Build 不显示。

布局/视图变化复用控件并更新上下文。源/模板替换、隐藏、卸载释放旧控件与上下文；Build/绑定/Measure 回调的新状态优先。模板错误清理后传播，源/模板变化可恢复。离线导出结束释放临时内容。局部禁用布局取整，内容边界与 PlotArea 精确一致。

## Demo、绑定与导出

运行统一 Demo 的 Charts 页，展开“标注与参考元素：独立坐标轴、圆角、标签与绘图区模板”，查看专用双轴、反向对数/断轴、圆角/标签、中心半径、固定 DIP 和内容模板，以及缩放、消息更新、恢复和 PNG 导出操作。验证程序另建独立原生窗口运行亮暗检查。

```powershell
dotnet run --project samples/NT.AvaloniaControl.Demo -c Release -- --page charts
dotnet run --project samples/Charts.Usage -c Release
```

Usage 新增“标注和参考元素 · 多轴、圆角、标签与绘图区内容”，编译绑定圆角、半径、标签、内容及动态资源。同一份源码用于功能独用与主题包消费者，无产品项目引用。Headless 消费者验证导出结果或明确 NotSupportedException/零输出失败契约，真实 PNG 像素证据取自原生 Demo，Usage 验证导出结果契约。PNG 使用当前布局与同一控件子树，包括标注/标签/绘图区内容，保持层次、裁剪与坐标；显式 PixelSize 时 DPI 修改元数据而不改变栅格外观，省略时按 DPI 推导像素尺寸。

## Windows 与 Linux 验证

| 检查 | Windows 原生合成 | Ubuntu 26.04 WSLg / XWayland |
| --- | --- | --- |
| 全部 Charts 测试 | 通过：821 | 通过：821 |
| Demo | 通过：366 | 通过：366 |
| Usage 编译绑定 | 通过：119 | 通过：119 |
| 功能 / 主题包消费者 | 通过：各 147 | 通过：各 147 |
| Release、输入/程序集/API 身份 | 零警告/零错误，身份一致 | 零警告/零错误，身份一致 |

原始结果已由[验证清单](charts-c2-annotations-validation.json)和[API 快照](charts-c2-annotations-api.txt)关联。已独立解码圆角切角、三个形状标签、全部元素正负恢复、96/144/192 DPI 导出，并独立计算多轴/反向对数/断轴/缩放投影。圆角单测仅在测试适配器中反射构造 Avalonia 12 内部绘图上下文，用 DispatchProxy 适配运行时绘图接口，记录实际 RoundedRect 参数（引用程序集未公开可实现的完整接口）；产品无此反射依赖，原生 RTB/PNG 另验视觉。


冻结源码 1252 文件，每平台 571 个声明输入；两平台、当前声明源码与冻结副本哈希一致。API 新增 60 项声明，无既有声明删除或改变。独立消费者各用新缓存，核对锁定恢复、compiled AXAML 与当前包 DLL 身份。最终独立复核通过 318 项检查、2251 个文件哈希，18 项历史与渲染器固定记录均未变化。

开发失败记录保留：圆角测试改用仅测试的运行时绘图适配器观测实际 RoundedRect，未为测试修改产品；Demo 修正固定提示遮挡及切角 ROI 采样；包消费者把屏外 pointer 放到真实可见窗口后继续原始注入，并明确无栅格后端的零输出错误契约。产品另修复布局取整、模板回调重入、离线导出清理与默认前景层次。

首轮独立审查使用了开发期旧 ROI，导致固定像素提示和圆角切角检查失败；按冻结 Demo 的实际区域修正审查脚本后通过，未修改原始运行结果或放宽容差。失败报告与最终 v2 报告一并保留。

## 剩余问题

- 多轴角落布局：当前 Demo 右上角的顶部 X 轴终点刻度 100 与右侧 Y 轴标题起始处存在轻微重叠；绘图区内标注、形状标签、固定提示和模板未互相遮挡。该项保留为轴标题/端点刻度的占位避让问题，当前功能及 PNG 投影通过不代表全部文字布局已完成。
- 未来图族、极坐标/3D、堆叠/LOD、交互式标注编辑/拖柄、高级标签避让需各自契约及回归；当前不宣称官方全部行为或 API 兼容。
- 按计划继续 C2-07 动画、C2-08 JPEG/异步导出、C2-09 点级辅助语义/真实读屏；类别图例、范围概览和跨图游标仍开放。
- Ubuntu 22.04/24.04 物理桌面、真实触屏/触控板、DPI/跨屏、UIA/AT-SPI、原生 Wayland/ARM、实际 GPU/backend/present 与官方许可运行参考仍待验收。Headless、原生程序化窗口和 RTB/PNG 不替代这些证据。
- 本批不测正式性能或内存；百万点命中尾延迟 0.7649→1.1235 ms、历史残余、增量/LOD 和长稳内存继续留 C4。
- 继承 C2-04 的 4 个原始 BarCache.PixelProbe.dll 缺失（8 处历史引用），原报告/哈希保留，不用新编译文件替代。C0/C2-03/C2-04/C2-05/v15 既有报告及 API 不变。

总计划见[开发计划](charts-development-plan.md)、[官方矩阵](charts-official-alignment.md)与[性能/平台附件](charts-performance-platform-plan.md)。
