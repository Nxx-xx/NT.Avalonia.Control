# Charts C3-03：散点与气泡

执行日期：2026-10-18。散点大小/颜色编码、源序 Scatter line 与 XY 气泡已接入共享底座、Demo、编译绑定 Usage 和隔离 NuGet 消费者。最终候选 `c3-scatter-bubble-source-v2` 的 Windows 与 Ubuntu 26.04 WSL2/WSLg 软件验证通过。完整 C3、官方运行时兼容、性能和物理平台资格继续开放。

## 功能与 Demo

既有 `ScatterSeries` 增加逐点大小、数值渐变及直接颜色；新增 `ScatterLineSeries`、`BubbleSeries` 和公共配置基类 `PointSeries`。支持面积/直径尺度、自动/显式域、clamp、无效值、稳定绘制顺序、十种marker的几何与裁剪命中，保留原始项身份和真实XY。Scatter line 保持非单调X的物理源顺序和缺失段，接入线段插值与完整Trackball。标签、Tooltip、选择、键盘、辅助语义、动画和导出复用公共管线。

Demo → Charts →「散点与气泡：大小、颜色和源序连线」包含六图，可切换面积/直径尺度、重叠顺序、数据标签及连线，并使用缩放平移。两主题的原始页与控制变更页均已实际查看；图像是已创建原生窗口的RenderTargetBitmap。

## Windows 与 Linux 验证

| 最终 source-v2 | Windows 原生合成窗口 | Ubuntu 26.04 WSL2/WSLg |
| --- | --- | --- |
| Charts 测试 | 1145/1145 | 1145/1145 |
| 原生 Demo | 804/804 | 804/804 |
| 编译绑定 Usage | 229/229 | 229/229 |
| 仅功能包消费者 | 321/321 | 321/321 |
| 功能与 Themes 消费者 | 321/321 | 321/321 |
| Release 警告/错误 | 0/0 | 0/0 |
| 基础/消费者/API 三阶段退出码 | 0/0/0 | 0/0/0 |

相比 C3-02 增加 62 项测试、60 项 Demo、23 项 Usage 和每类消费者 37 项检查。两类消费者均使用各自全新NuGet缓存，无源码项目引用；14个非产品依赖按已保存SHA复用，产品由各平台本批源码重新构建打包。未更改NuGet audit或警告策略。

冻结 1320 个文件；两平台各 623 个声明运行输入同SHA，运行前后声明源和程序集稳定。后续文档更新不声称整个工作区与冻结副本相同。独立来源/平台审阅 3912/3912；35 个历史文档/API pin及 3 版历史renderer原SHA保留。

完整本地API按assembly+type比较，含导出生成资源类：110→115类型；Charts命名空间108→113。新增5类型及既有类型33项成员/接口声明，无旧类型、声明或成员删除。此为元数据比较，未认证二进制或官方API兼容。

独立Python stdlib PNG CRC/deflate/滤波解码审阅 640/640，实际解码 80 张图。两平台两主题各14项预注册固定业务探针，按公开0..10/0..100轴与大小/颜色公式复算面积/直径边界、数值颜色、源序连线/缺失段、重叠顶层与中心在plot外的裁剪。6×6DIP区域、alpha≥240、RGB容差24，填充至少10像素/线段至少4像素，拒绝区域0像素；未将生产hit/shape/marker尺寸输出作为预期。96/144/192DPI导出像素尺寸固定，pHYs密度符合公式，绘图区逐像素0差。初次reader在47项通过后因未识别Avalonia命名色Blue/Purple/Red中止；仅补等价RGB解析后另存复核结果，原失败保留，业务预期和像素阈值未变。

开发版native-v1保留原802/804：两主题六图绘图区约98DIP，未通过既定Height>100门禁。实际图高275→295后native-v2为804/804，绘图区约118DIP；门槛未降低。正式source-v1在独立复核发现厚描边非圆marker完全位于plot外仍可容差命中后中断；原证据保留且不作为最终通过。补精确裁剪描边和回归案例后重新冻结source-v2并完整重跑两平台。Linux原中断launcher的exitCode0伴随子阶段退出码null，明确按未完成处理。

完整报告、退出码、声明输入/程序集清单及SHA见[验证清单](charts-c3-scatter-bubble-validation.json)，公开声明见[API](charts-c3-scatter-bubble-api.txt)。

## 实现契约

- 公开新增 PointSeries、ScatterLineSeries、BubbleSeries、ChartSizeScale 和 ChartPointDrawOrder；既有 ScatterSeries 通过新增属性接入编码。未配置 SizePath/ColorPath 且 DrawOrder=Source 的普通 Scatter 保留原渲染路径和原命中距离规则。
- SizePath 优先；仅 Bubble 在没有 SizePath 时回退 WeightPath。两者均未配置则使用 MarkerSize。SizeValue/ColorValue 保留源映射原值，MarkerDiameter/PointFill 是当前显示场景的派生值，源快照不被改写。
- MinimumSize/MaximumSize 为 DIP 直径，范围0..1024且下限不大于上限。有限非负业务值经域内clamp得到t；Diameter为Dmin+(Dmax-Dmin)t，Area为sqrt(Dmin²+(Dmax²-Dmin²)t)。默认Area；Bubble默认Dmin=0、Dmax=60，PointSeries/Scatter为4、40。
- 大小自动域下界为0，上界为全部轴域有效XY样本中的最大有限非负大小；颜色自动域取同一批有效XY样本的有限数值min/max，均不随Viewport改变。自动退化域取t=0；用户显式边界解析为max≤min时抛InvalidOperationException。
- 负值、缺失和非有限大小只隐藏marker，XY仍可用于键盘、共享读数和线段；0映射到MinimumSize，因此默认Bubble零直径不可见，显式非零MinimumSize可显示零值。发布快照中的有限IsEmpty仍在显示场景保持gap，发布源本身保留。
- ColorPath 接受有限数值、Color、IBrush和可解析颜色字符串；数值按MinimumColor/MaximumColor的sRGBA通道线性插值并量化。无效颜色回退MarkerFill、Fill及系列调色板；原ColorValue仍可供模板读取。
- DrawOrder按源序或当前显示直径稳定升/降序，不改Item/StableId/SourceIndex。新/encoded点renderer按可见marker几何外缘及描边计算距离，内部为0，等距选后绘制项；透明fill保留几何命中。混合其他renderer继续遵循各自DistanceSquared协议，不承诺跨全部系列恒选全局最顶图形。
- 新marker按十种形状、round描边、plot外框及本系列实际X/Y轴断轴可见区计算裁剪命中，中心在可见区外而部分可见时仍可命中；完全不可见形状不产生容差命中，plot外或断轴空白查询不返回点。共享范围选择仍按XY中心语义。
- ScatterLine按原快照物理源顺序作直线连接，保留非单调X、相邻真实源端点和插值元数据；XY gap断开，大小缺失不把线段断开。ShowLine与ShowMarkers独立，ShowLine=false或StrokeThickness=0时不提供线段命中/插值读数。
- 动画按当前显示直径、坐标和solid颜色/Opacity插值；SizeValue/ColorValue继续指向目标源值，MarkerDiameter/PointFill与当前绘制帧一致。非solid IBrush在动画期间直接取目标brush。
- 标签、Tooltip、Trackball、选择、键盘和辅助语义复用共享底座并携带大小/颜色/当前样式；基础标签默认仍显示XY值，Demo主动格式化业务大小。缩放保持DIP尺寸；PNG/JPEG复用既有导出管线，本批新增PNG三DPI精确像素检查。

## 剩余问题

- 本批沿用系列级图例，未增加独立尺寸图例或连续色标控件；点编码、排序及几何命中以本库API提供，尚未认证完整官方API/运行时等价。
- 共享范围选择仍按XY中心，默认旧Scatter与混合renderer保留原距离协议；这些边界不等同于新encoded marker内部的paint-order命中规则。
- 新点样式派生、稳定大小排序、全量绘制/命中、动画和多图的规模、分配、增量、内存与尾延迟回归仍在C4；本批未建立性能资格。
- 无轴Bubble cloud、Packed bubble、Circle packing与碰撞布局仍在C6；C3-04圆形及其余常用图族继续按计划推进，完整C2/C3/C4/C7仍开放。
- 官方许可参考宿主、物理Windows/Linux、实际backend/GPU/compositor呈现、多屏DPI、Ubuntu22/24、原生Wayland、ARM及目标国产Linux仍待独立验证。
- Windows UIA/Linux AT-SPI原生选中项枚举、Linux嵌套点选择写入、点系统焦点和真实读屏器按用户决定保留到A11Y-NATIVE-01；本批未重验或改为通过。
- 百万点命中p95历史回退0.7649→1.1235ms按用户决定保留到C4；既有AX06标题/末刻度布局和4个历史PixelProbe DLL缺口保持开放，未用新证据替换。
