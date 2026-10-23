# Charts C3-01：折线、面积、范围与堆叠

执行日期：2026-10-18。平滑、三种阶梯、面积/平滑面积、范围面积，以及折线/面积的堆叠和百分比变体已实现，并接入统一 Demo、编译绑定 Usage 及两类独立 NuGet 消费者。最终候选 `c3-line-area-source-v1` 已在 Windows 和 Ubuntu 26.04 WSL2/WSLg 实际运行通过。此结论限本轮软件功能，不代表完整 C3、官方运行时兼容、物理桌面或性能资格。

## 使用与契约

`LineSeries.Interpolation` 支持 `Linear`（默认）、`Smooth`、`StepBefore`、`StepAfter`、`StepCenter`；新增 `AreaSeries` 和 `RangeAreaSeries` 也支持全部五种模式。前阶梯在源起点转折，后阶梯在源末点转折，中阶梯在屏幕 X 中点转折。Smooth 使用投影空间的保形 Hermite 曲线，保留源顺序和端点；重复 X 使用直线，反向 X 可绘制。绘制、线段插值和 Trackball 共用几何。

`AreaSeries.Baseline` 默认 0，必须有限；自动范围包含有效基线。默认填充为调色板颜色的 25% 不透明度，显式 `Fill` 按调用者值绘制。范围面积使用 `CategoryPath`、`HighPath`、`LowPath`，无需 `ValuePath`；每行有限高低值按 min/max 规范为上下轨，原有效 High/Low、Item、StableId 和源序号保留。Gap 模式缺任一界即断段；Zero/Average/Interpolate 只补缺失界，保留原始 WasEmpty。已发布快照显式 IsEmpty 不会复活。

```csharp
chart.Series.Add(new LineSeries { Interpolation = ChartLineInterpolation.Smooth,
    ItemsSource = points });
chart.Series.Add(new AreaSeries { Interpolation = ChartLineInterpolation.StepCenter,
    Baseline = 0, ItemsSource = points });
chart.Series.Add(new RangeAreaSeries { ItemsSource = records, CategoryPath = "X",
    HighPath = "High", LowPath = "Low", IdPath = "Id" });
// 同组的折线或面积：StackMode=Stacked / Percent，StackGroup="sales"。
```

堆叠按实际 X/Y 轴对象、StackGroup（null 与空串同组）、StackMode 隔离；同 X 的第 N 次出现按源顺序对齐，保留缺失占位，仅可见系列参加。正、负贡献分别累计；Percent 正侧到 +100、负侧到 -100，零总量为 0。分母先缩放避免有限原值相加溢出，以累计权重求上界、相邻上界之差求贡献，末层精确到 ±100；普通堆叠溢出时断段。堆叠忽略 Area.Baseline，以累计下界为基线。

同组 Smooth 系列在插值、X/源序号、有效边和符号链拓扑一致时共享切线收缩，防止上下轨厚度反转并保留连续共边与 C1 连续；不同拓扑只保证实际 X 采样点累计，不能推定连续共边。允许数据穿越自身基线。

显示快照不修改原源快照。点/选择的 `Point` 是累计或范围上轨，`RawPoint` 是映射源坐标，`BaseValue` 是显示下轨，`StackValue` 是原始贡献或带符号百分比；范围原始 High/Low 仍可读取。点命中、选择、键盘及线段/Trackball 以同一原始项的上轨为目标，线段 BaseValue 按同参数求下轨，默认范围读数显示下界至上界。填充内部与独立下轨不是新的选择目标。

动画同时插值上/下轨的当前帧，RawPoint/StackValue 保持目标源数据；瞬时读数不保证等于业务对象目标属性。pending 场景可暂读到 canonical 选择，发布场景后获得派生元数据。范围及堆叠显式拒绝 Category 值轴，可用 Numeric/Log/DateTime 等非 Category 值轴；普通面积的 CategoryY 组合未单独资格验证，当前示例使用数值值轴。非零基线或范围下轨处于轴域外时断段，log 轴默认 0 基线回退至绘图区 origin。

Demo → Charts →「折线与面积：平滑、阶梯、范围和堆叠」包含四图、插值选择、基线、缺失值开关及堆叠模式。初始展示平滑堆叠，切换后展示阶梯百分比及精确 ±100% 自动范围；范围示例还包含独立右轴，可使用现有悬停、选择、缩放和平移。

## Windows 与 Linux 验证

| 最终 v1 | Windows 原生合成窗口 | Ubuntu 26.04 WSL2/WSLg |
| --- | --- | --- |
| Charts 测试 | 1013/1013 | 1013/1013 |
| 原生 Demo | 652/652 | 652/652 |
| 编译绑定 Usage | 181/181 | 181/181 |
| 仅功能包消费者 | 245/245 | 245/245 |
| 功能与 Themes 消费者 | 245/245 | 245/245 |
| Release 警告/错误 | 0/0 | 0/0 |
| 基础/消费者/API launcher 退出码 | 0/0/0 | 0/0/0 |

相较 C2-09 增加 67 项产品测试、74 项 Demo、18 项 Usage 和每类消费者 32 项检查。两种消费者各用全新 NuGet 缓存，无源码项目引用；仅功能包自行供样式，可选 Themes 单独验证。14 个非产品依赖归档按旧 SHA 复用，产品在两平台分别构建打包，未变更 audit/警告策略。

冻结 1291 个文件；两平台各 600 个声明输入逐项同 SHA，并验证运行中源码/程序集稳定。Capture-ChartsBaseline.ps1 本批补入声明输入。两个本地产品程序集的完整公开 API 从 98 到 102 个类型（包括导出的生成资源类；Charts 命名空间单独计数为 96→100），新增 4 类型、既有类型新增 15 项成员/接口声明，无旧类型、既有声明或成员删除；这是本地元数据比较，不是二进制或官方 API 兼容认证。29 个历史报告/API/基线当前文件及 1 个旧渲染器冻结副本保留原 SHA；当前渲染器已按新系列接入修订，并进入本批源清单。

独立 stdlib PNG 解码/CRC/滤波审阅 174/174 项，实际解码 112 张图：五种线形数学坐标、正负对照、面积基线/缺失空白、范围内外、正负堆叠原值/下界/贡献、两主题自动百分比范围，以及每主题 6 个 PNG 导出的绘图区逐像素 0 差与 96/144/192 DPI 密度。像素条件在正式运行前登记：6×6 DIP 区域、alpha≥250、RGB 容差 24，线至少 4 像素、填充至少 10、拒绝区域 0。两平台各四张正式页面另经实际图像视检；图像来自原生窗口 RenderTargetBitmap，不是 compositor 桌面截图。

另有不导入产品代码的固定 seed 9383 代数验证：2000 组、48000 个 band、4848000 个采样点，经共同切线收缩后无负厚度/过冲/不连续切线反例；不计为产品测试或平台性能结果。完整数值、原始报告与 SHA 见 [验证清单](charts-c3-line-area-validation.json)，公开声明见 [API](charts-c3-line-area-api.txt)。

## 已修复问题与保留记录

开发阶段修复了范围显式空点被派生转换复活、log 面积默认零基线吞掉正值自动范围、平滑堆叠上下轨交叉及曲线边缘裁剪命中问题，均有回归覆盖。最早 focused 49/50 的选择断言读取了尚未发布的 pending 场景，改为发布后验证；原失败运行保留。

独立来源审阅首轮 3779/3783 原件保留：本批正常修订了产品渲染器的形状接入，历史 renderer pin 改从 C2-09 原冻结文件验证，29 个其他历史文件仍在当前路径核对；另外 Windows 三份原始中文构建日志发生乱码，补充英文环境下三项目 `--no-incremental` 显式重编译，全部 0 警告/0 错误。前后 600 个声明源文件与所有已运行程序集 SHA 均与正式运行一致，因此无需重跑相同二进制测试。最终来源/平台审阅 3784/3784，完整本地程序集 API 追加审阅 5/5；旧失败 JSON、旧乱码日志及旧 renderer 原件均未覆盖。

`development-native-v1` 原始 646/650、JSON 和图片保留：四项失败来自派生贡献 ±60.00000000000001 与数学 ±60 的 exact-double 断言，正式前仅 Base/Stack 改用绝对 1e-9，Raw/身份/Point 仍精确比较。另一个独立的真实产品缺陷是百分比层累加漂移至 100.00000000000003，导致 nice 自动轴扩到 ±150；已改累计权重算法、补三层与七等分正负回归，并新增 native 自动轴精确 ±100 检查。这两件事分开记录，没有用容差掩盖自动轴缺陷。正式前预注册 v1～v4、开发失败及原图均保留；像素、DPI 与 PNG 0 差门槛未放宽。

## 剩余范围

- 不同插值、X/source ordinal拓扑、缺失段或符号链的堆叠仅保证实际X采样点累计，不保证连续曲线共边。
- 点命中/选择/键盘及线段/Trackball锚定上边界；本轮不提供填充内部命中和独立下边界选择目标。
- 新形状几何构建/命中为O(N)，百万点曲线、带宽内存/LOD/增量及尾延迟归C4，未建立本轮性能资格。
- 面积/范围及堆叠值轴要求数值轴；范围High/Low需有效数值。非零Baseline落在数值轴域外时断段；log默认0基线回退绘图区origin。
- Windows UIA与Linux AT-SPI原生选中项枚举保留原失败，按用户要求进入A11Y-NATIVE-01，未在本批重验。
- 物理Windows/Linux、多屏DPI、实际GPU/backend/compositor、Ubuntu22/24物理桌面、原生Wayland/ARM及官方许可运行时对照继续待验。
- C3-02柱条、C3-03气泡及后续常用图族未在本批完成；完整C2/C3/C4及C7出口仍开放。

百万点命中 p95 历史回退 0.7649→1.1235 ms 仍按用户决定留到 C4。Windows UIA / Linux AT-SPI 原生选中项枚举保留原未通过资格，按用户决定由 A11Y-NATIVE-01 专项处理；本批未重跑，也未转成通过。既有 AX06 和四个历史 PixelProbe DLL 缺口继续保留，不以新构建替代旧证据。
