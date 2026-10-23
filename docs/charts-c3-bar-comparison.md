# Charts C3-02：柱条与比较

执行日期：2026-10-18。横向、分组、堆叠及百分比柱条，以及范围柱、瀑布、Pareto、Dot、Lollipop 和 Variance 已接入共享数据与几何底座、Demo、编译绑定 Usage 和独立 NuGet 消费者。候选 c3-bar-comparison-source-v1 的 Windows 和 Ubuntu 26.04 WSL2/WSLg 软件验证通过；完整 C3、物理平台、官方运行时兼容和性能资格仍开放。

## 公开 API 与使用

BarSeries 保持原默认纵向柱行为，新增 Orientation、Baseline、BarWidth、WidthRatio、SortMode、StackMode、StackGroup。横向展示将源分类映射到 Y、值映射到 X，原始 Item、StableId、源索引和 RawPoint 保留。正负值分别累计，百分比各符号侧归一至 ±100；分组及堆叠按解析后的轴和组配置分配槽。

新增 ComparisonSeries 公共基类，以及 RangeBarSeries、WaterfallSeries、DotSeries、LollipopSeries、VarianceSeries、ParetoSeries。范围柱和 Variance 使用 LowPath/HighPath；瀑布普通行作为增减量，TotalPath 为 true 的行作为显式总计并重置累计。Pareto 默认降序并保留原身份，累计线使用独立百分比轴；CumulativeAxisKey 可指定百分比轴。排序、宽度、标签、选择/读数、坐标轴、缩放和导出沿用正式公开 API。

Demo → Charts →「柱条与比较」展示六图及方向、宽度、排序、标签和堆叠模式切换。截图来自已创建的原生窗口 RenderTargetBitmap。

## Windows 与 Linux 验证

| 最终 v1 | Windows 原生合成窗口 | Ubuntu 26.04 WSL2/WSLg |
| --- | --- | --- |
| Charts 测试 | 1083/1083 | 1083/1083 |
| 原生 Demo | 744/744 | 744/744 |
| 编译绑定 Usage | 206/206 | 206/206 |
| 仅功能包消费者 | 284/284 | 284/284 |
| 功能与 Themes 消费者 | 284/284 | 284/284 |
| Release 警告/错误 | 0/0 | 0/0 |
| 基础/消费者/API launcher 退出码 | 0/0/0 | 0/0/0 |

相比 C3-01 增加 70 项产品测试、92 项 Demo、25 项 Usage 和每类消费者 39 项检查。消费者各用全新 NuGet 缓存，无源码项目引用；14 个非产品依赖归档按历史 SHA 复用，两平台分别构建打包产品。未更改 audit 或警告策略，正式构建初始即设英文输出。

冻结 1306 文件；两平台各 612 个声明输入逐项同 SHA，运行前后源码与程序集稳定。后续文档更新不据此声称整个工作区与冻结副本相同。来源/平台审阅 3850/3850 项；32 个历史文档/API/基线及两版冻结 renderer 保留原 SHA，当前 renderer 按新系列接入修订。

完整本地 API 按 assembly+type 比较，含导出生成资源类：102→110 类型；Charts 命名空间单独计为 100→108。新增 8 类型，既有类型新增 21 项成员/接口声明，无旧类型、既有类型声明或成员删除。这是元数据比较，未认证二进制或官方 API 兼容。

独立 stdlib PNG CRC/deflate/滤波解码审阅 853/853 项，实际解码 100 张图。预注册固定业务数据和公开轴范围，在两平台两主题复算19个横/纵向、固定宽度、分组、正负堆叠/百分比、范围、瀑布、Dot/Lollipop/Variance 及 Pareto 数学探针：6×6 DIP、alpha≥250、RGB容差24，填充至少10像素/细线至少4像素，拒绝区域0像素。未采用生产 hit/矩形/renderer路径作为预期；分组窄柱独立按固定±30DIP中心复算。96/144/192DPI导出保持固定像素尺寸与密度，绘图区逐像素0差。

开发版 native v1 保留原始742/744：亮暗两主题的六图布局未达到既定PlotArea.Height>100门禁。已将每图高度245→285、独立smoke窗口1000→1140，未降门槛；native v2及正式版744/744，最小绘图区高约108.037。原失败JSON与图像保留，没有覆盖。

完整退出码、原始报告、声明源/程序集清单和 SHA 见[验证清单](charts-c3-bar-comparison-validation.json)，公开声明见[API](charts-c3-bar-comparison-api.txt)。

## 剩余问题

- 本批比较形状的点选择依托原数据项；区间两端和Pareto辅助累计线没有单独的第二项身份。进阶比较交互与更多标签布局仍按后续计划扩展。
- 值排序当前要求自动标签CategoryAxis，不支持在自定义固定标签轴上任意重排；共享分类轴采用首排序系列驱动规则。
- 新比较几何与命中、排序/派生数据和堆叠的规模、分配、增量、内存及尾延迟回归留到 C4；本批未建立性能资格。
- 官方许可运行时行为/API 对照、物理 Windows/Linux、实际 GPU/backend/compositor、多屏 DPI、Ubuntu 22/24、原生 Wayland 和 ARM 继续待验。
- 原生 UIA/AT-SPI 选中项枚举、Linux 嵌套点写入、点系统焦点及真实读屏器按用户要求留到 A11Y-NATIVE-01；保留原失败，本批未重验。
- 直方图分箱和进阶比较仍在 C5；C3-03 及其余图族继续按计划开发，C2/C3/C4 和 C7 完整阶段未关闭。
- 既有 AX06 标题/末刻度布局问题和四个历史 PixelProbe DLL 缺口保留，未用新文件替代原证据。

## 实现契约

- BarWidth 为非空时是显式 DIP 宽度（至少0.5），可大于分组槽；为空则按槽和 WidthRatio（(0,1]，默认0.86）计算。未设置新增配置的纵向Bar仍走既有绘制路径。
- 数值升/降序要求自动标签 CategoryAxis；同一分类轴由第一个排序系列确定共享顺序，其余系列按标签和源序第N次出现对齐。XAscending按原始数值X稳定排序，非按标签字母排序；分类轴仍应用自身标签映射；Waterfall只支持Source排序，Pareto只支持ValueDescending。
- RangeBar 对有限 High/Low 规范化绘制 min/max 上下界并保留原始 High/Low；Variance 保留 Low→High 的方向及正负颜色。它们无需 ValuePath。
- Waterfall 普通行从当前累计值增加有符号原值；TotalPath=true 行从Baseline绘制显式总计并重置累计。累计溢出后有限delta保持gap，直到有限total重置；缺失行不产生额外业务值。
- Bar堆叠按解析X/Y轴、方向、StackGroup、StackMode和Baseline隔离，正负分别累计；Percent要求Baseline=0，各符号侧末层为±100。RawPoint保留原始源坐标；StackValue在Stacked中是有符号原贡献，在Percent中是有符号百分比份额。
- Pareto跳过负值，稳定降序并保留源索引、Item、StableId；累计使用最大值缩放分母以避免有限原值求和溢出，正总量时最后有效点为100，全零总量的累计值为0。默认私有百分比轴0..100，CumulativeAxisKey需唯一、独立线性数值轴。
- Pareto累计线使用独立投影和断轴裁剪；普通Tooltip、Trackball与辅助点语义返回本项累计值，未为辅助累计线建立第二套独立点/线段选择身份。
- 本库数据标签默认显示Point端点值（横向取X、纵向取Y）。Demo的“显示原始值标签”主动使用RawPoint格式化，所以百分比堆叠可显示原贡献20、30；应用可按公共label context自定义格式。范围/Variance/Waterfall的业务字段与显示端点分开。

百万点命中 p95 历史回退 0.7649→1.1235ms 仍按用户决定留到 C4；Windows UIA/Linux AT-SPI 原生选中项枚举原失败保留到 A11Y-NATIVE-01，本批未重验或转为通过。
