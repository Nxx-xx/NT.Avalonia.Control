# Charts C7-01：专业图族共享图例与数据标签模板

日期：2026-10-23。本轮为 Engineering / Analysis 两根及其专业图族补齐共享图例和数据标签模板，加入 Demo、Usage 和真实包消费者，延续[业务 Tooltip 与导出稳定性](charts-c7-tooltips-export-stability.md)的原始身份、主题和生命周期约定。Windows 与 Linux / WSLg 的同一最终冻结候选均完成六个正式软件验证阶段，本轮共享图例和数据标签范围通过。C7-01 其余集成仍在进行。

## API 与语义

两根新增 `LegendItemTemplate`、`DataLabelTemplate`、`MaxLegendItems`、`MaxDataLabels`，并提供 `GetLegendItems()` / `GetDataLabels()` 读取当前有界上下文。两个根对应属性共享 styled property 身份；原有 Cartesian 图例、系列标签和 `ChartDataLabelContext` 保留原契约。

| 属性 / 上下文 | 约定 |
| --- | --- |
| `LegendItemTemplate` | `IDataTemplate?`，使用 `ChartDataLegendItemContext`。null 恢复原图族默认图例；Build 返回 null 隐藏对应项，新读取会重新判断。 |
| `DataLabelTemplate` | `IDataTemplate?`，使用新的 `ChartDataLabelItemContext`。替换真实数据标签，保留轴文字、标题、分组标题、连线等装饰。 |
| `MaxLegendItems` | 默认 32，范围 0～1024，限制共享图例候选与模板根控件数量。 |
| `MaxDataLabels` | 默认 200，范围 0～4096，限制当前帧共享标签候选与模板根控件数量。 |
| `Members` | 当前项贡献的完整原始 `ChartDataPoint` 列表，包含真实 Item、StableId、SourceIndex；聚合项直接保留当前标记的全部成员。 |
| `BusinessDataContext` | 图表的业务 VM；模板控件自己的 DataContext 为当前读取上下文。 |
| `EngineeringHit` / `PointHit` | 对应真实工程标记或原始分析点，类型互斥。分类、色阶和尺寸键可以没有单个命中，不伪造系列或聚合观测。 |
| `Text`、`Value`、`Brush`、`Anchor`、`Bounds` | 图族的当前文本、值、颜色与图表局部 DIP 位置和边界。 |
| 图例 `Kind` / `Key` / `Minimum` / `Maximum` | 区分标记、分类、连续色阶和尺寸键；装饰性尺寸刻度没有业务成员。 |

`GetLegendItems()` / `GetDataLabels()` 独立于模板是否启用，候选按图族顺序截取，预算为 0 时该共享呈现为空。预算不限制业务模板自行展开 `Members` 的子控件数。模板保持被动，不接收指针或 Tab；原图形命中、键盘浏览和选择继续返回原始数据。

### 图族适配

- Hierarchy 保留当前终端分类、合计和 9:4:1 相对面积键，钻取、折叠与当前动画帧共同参与。几何间距使极小节点没有可见图元时，分类键仍保留决定合计的真实原始节点。
- ShapeMap / SeatMap 按真实图层保留分类键、连续色阶、渐变透明度、范围和原始关联成员；无数据 feature 的文字仍是装饰。影响色阶范围但因投影收缩而没有图元的真实成员也保留在图例中。
- 关系图按节点分组提供图例，节点与边的标签维持各自身份域和开关，组标题继续绘制。表格、排程、比较与运营图保留单元、事件碰撞、端点、复合标记及 KPI 主值 / Delta 等实际标签语义，不按邻近位置猜测来源。
- 既有 `ShowLegend`、`ShowLabels`、`ShowValues`、节点 / 边标签开关继续生效。比较图标签元数据为空时，仅 `CategoricalDistributionChartBase`（Strip / Beeswarm）、CalendarHeatmap、ParallelCoordinates 明确允许新增逐点模板；类别和值分别遵循对应开关，Parallel 使用真实原始维度值。Mosaic、分布轮廓等不会因空元数据回退而绕过原标签开关。

### 所有权、更新与布局

控件加入图表真实逻辑和视觉树，继承字体、Foreground、主题及动态资源；业务读取通过 DataContext 更新，稳定帧复用控件。源通知、换源和业务 DataContext 改变会清除旧读取；模板替换、隐藏、卸载和释放会清理子树。恢复显示后可重建，禁用控件时永久图例与标签继续显示。Build / 布局异常清理后传播，失败模板停止重试，替换模板可恢复；已有父级的返回控件被拒绝。

上下文生成包含独立重入保护。虚拟图族生成器或业务 Category.ToString 再次读取 `GetLegendItems()` / `GetDataLabels()` 时，只获得此前完整批次或空结果，不递归生成。图例与标签先在局部生成，再共同验证源 revision、当前标记帧及业务 DataContext 后发布；生成期间换源不会提交混合批次。上下文生成、模板构建、布局和绘图过程中重入导出会被拒绝。

标签保持原始 Anchor，实际模板位置受图表、图族几何和绘图区裁剪；启用有效共享图例时，标签布局范围避开图例页脚。完整图表大小不变而标签位置变化时，内部承载层会主动失效 Arrange，保证复用控件跟随缩放、平移或图例开关重新定位。默认绘制 Tooltip 使用前景层，避免被永久模板遮住。页脚空间有界，超出内容会裁切；当前没有共享图例滚动或通用标签避让。

## 导出与 Demo

图例和数据标签是永久内容，进入同步 / 异步 PNG、JPEG 导出；瞬态 Tooltip 继续排除。模板构建与布局在捕获前的 UI 阶段完成，绘图回调不构建模板子树。导出沿用尺寸、DPI、背景、取消、失败和文件替换管线，并拒绝捕获期间改变源或模板的重入更新。

Demo 的“专业图族 · 共享图例与数据标签模板”展示 Funnel、Mosaic 聚合、Strip 和 SeatMap，提供独立模板开关、预算、数据 / VM 更新、换源、主题与视图操作。Usage 的同名折叠区使用真实 AXAML 编译绑定；功能包和主题包消费者复制并执行同一绑定示例。

## 最终冻结候选与验证结果

冻结源码为 `source-snapshot-v3.json`，1929 个文件，SHA-256：`E1506D980F78035E304AD7F08F0C064F3E214BB86A38CE9924EA16E7A1F6FB60`。双平台正式结果来自最终独立收集器 `validation-final.json`，`batchPassed` 与 `fullNativeRegressionPassed` 均为 true，`milestoneComplete` 为 false。两个平台各自验证前后 1929 个冻结文件未变化，工作区 600 项图表构建、测试、示例及验证输入与候选一致；Demo、Usage、导出宿主与隔离消费者实际加载的 Charts / Themes 程序集均核对哈希。最终归档共 169928 字节，SHA-256：`5D9984256F7FD6BA118921B0D824AF09FD7877BB166AE68335EC0DEA636BF89D`。

| 验证项目 | Windows 原生合成窗口 | Linux / WSLg |
| --- | --- | --- |
| 六阶段正式软件验证 | 6 / 6 通过 | 6 / 6 通过 |
| Headless 全量 | 2986 / 2986 | 2986 / 2986 |
| 新共享呈现 / 图族适配 / 原 Tooltip 回归 | 38 / 20 / 59，全部通过 | 38 / 20 / 59，全部通过 |
| 完整原生 Demo | 4824 / 4824 | 4824 / 4824 |
| 原生 Usage 编译绑定 | 885 / 885 | 885 / 885 |
| 新隔离缓存功能包 / 主题包消费者 | 1067 / 1067；1101 / 1101 | 1067 / 1067；1101 / 1101 |
| 共享呈现专项 | 239 / 239，7 组主题、导出与生命周期探针 | 239 / 239，7 组主题、导出与生命周期探针 |
| 专项图片与导出文件 | 44：4 张主题图、32 个导出、8 张字形隐藏 / 恢复图 | 44：4 张主题图、32 个导出、8 张字形隐藏 / 恢复图 |
| API 元数据快照 | 503 个公开类型：功能包 500、主题包 3 | 503 个公开类型：功能包 500、主题包 3 |
| 强制 GC 原生导出稳定性 | 272 次尝试、3200 项生产端守卫、3232 项独立检查，全部通过 | 272 次尝试、3200 项生产端守卫、3232 项独立检查，全部通过 |

Windows 为 x64 Windows 10.0.26200 原生合成窗口；Linux 为 Ubuntu 26.04 LTS x64、WSL2 / WSLg XWayland 原生 X11 合成窗口。两端 SDK 均固定并核对为 10.0.402，实际运行时 .NET 10.0.13，Avalonia 12.0.3.0。两个平台的功能包与主题包消费者均使用新缓存、无源码 ProjectReference，并核对实际 SDK 与所加载包。全量 XML 按用例显示名逐项比较，上一批 2928 项全部保留，本轮新增 58 项，没有删除旧测试。双平台归一化 API 表面一致，SHA-256 为 `51E31BC54B1047CD2F36A1515F889C468B947CB6EF031ABE1699E25C6EA039DA`。

专项覆盖普通浅色、普通深色、高对比浅色和高对比深色；两个根分别检查默认、仅标签、仅图例、两者同时启用的四种状态，以及 PNG / JPEG 同步、异步四条路径。独立 PNG 解码检查永久内容像素变化、同编码同步 / 异步一致性，以及真实 TextBlock 字形所在 ROI 的隐藏差异和恢复一致性。图片收集器另保留开发证据上的 184 项自检与 3 个负向变体：隐藏图替换成恢复图、永久内容替换成默认内容、JPEG 文件替换为 PNG，均正确拒绝；这些开发自检不冒充最终冻结候选结果。JPEG 验证包含生产端解码比较及独立文件结构 / 尺寸检查，未声称独立视觉质量认证。

原始证据根目录：`C:/Users/a4472/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c7-shared-presentations-20261023`。正式记录分别在 `windows/run-v3`、`linux/run-v3`；专项截图目录为 `windows/run-v3/professional-presentation` 与 `linux/run-v3/professional-presentation`。人工复核记录 `final-v3-manual-qa-v1.json` 仅列出三张实际查看的最终图片：Windows 普通浅色、Linux 普通浅色和 Linux 高对比深色，人工复核范围限于上述三张最终图片，记录未见 CJK 缺字或卡片交叠。最终聚合证据见[验证 JSON](charts-c7-shared-presentations-validation.json)，公开表面见[API 快照](charts-c7-shared-presentations-api.txt)。

## 已发现的问题与修正

开发原生验证首先发现图例页脚裁切业务标题、边界标签进入图例区及紧凑示例卡片相压，保留首次 219 / 223 结果和截图。修正模板紧凑布局、卡片高度和标签 / 图例布局区域后，开发专项达到 239 / 239，Usage 达到 885 / 885；最终 V3 再次执行相同专项。

正式 V1 在目录阶段发现四个排程控件改为 partial 后的声明锚点过期，仅修正目录锚点，没有修改产品代码。正式 V2 全量为 2979 项，2977 通过，两个新用例发现标签承载层位置变化后未触发内部 Arrange；该轮在原生 Demo 前停止。两轮失败的冻结源码、日志和 XML 均保留。

对 V2 的实测定位是：Analysis Anchor 从 `(320,324)` 变到 `(320,388)`，标签仍停在 `(270,234,100,90)`；Engineering Anchor 从 `(320,312)` 变到 `(320,376)`，标签仍停在 `(270,222,100,90)`。修正承载层 Placement 的 Arrange 失效后，两根标签均恢复到 `(270,310,100,90)`、底部 400。另加工程图视图回归，原始 Anchor `(320,168)` 变到 `(379.2,139.2)` 时，同一标签控件的实际边界从 `(280,158,80,20)` 移到 `(339,129,80,20)`，原始来源与控件身份保留。

随后补齐上述上下文生成重入保护。修正仅涉及六个明确的 C# 文件，冻结 V3 记录其父清单和允许变更列表；开发定向 117 / 117（共享呈现 38、图族 20、旧 Tooltip 59）通过，六个文件哈希与最终候选一致。最终 Windows 与 Linux 各自全量 2986 / 2986 均包含这些回归，未放宽原有断言。

## 剩余工作与本轮边界

- C7-01：专业 PlotAreaContent、Trackball、范围选择、多图同步，以及完整动画 / 导出交叉组合继续逐图族接入。
- 共享图例为被动呈现；分类显隐命令、超长图例滚动和复杂标签避让仍开放。渐变、透明叠层及业务自定义模板配色继续按最终像素检查。
- C7-02～06：指定 Windows / Linux 实机与物理输入、官方授权 runtime 同质量性能对照、8 小时和 100 轮生命周期长稳、自包含 / 裁剪 / AOT / 升级消费、最终 API 和发布材料仍待验收。WSLg 与离屏导出不作为实际屏幕 present、GPU 或发行版认证，272 次压力导出不换算为性能 SLA 或长稳完成。
- 百万点命中尾延迟与 UIA / AT-SPI 原生选中项桥接按用户决定后置。
