# Charts 0.2.0-preview.3 发布说明

状态：本地/私有试用候选；以[实际验收记录](charts-preview-release-validation.json)为准。未执行远程发布，项目分发许可尚待确定。

## 本版交付

- NT.AvaloniaControl.Charts：现有坐标图、统计/金融、专业坐标、层级/关系、地图和运营图族的基础能力。
- NT.AvaloniaControl.Charts.Themes：默认明暗主题和显式高对比预设。
- NT.AvaloniaControl.Charts.Skia：可选适配层，精确依赖Avalonia/Avalonia.Skia12.0.3；不支持的绘制状态回退到原路径。
- 本轮冻结现有功能与依赖，补齐包说明、真实nupkg消费者、普通JIT目录部署、生命周期与长稳验收工具。没有接纳新的散点加速算法或SDK扩展。

三个包的版本一致，交付采用同一套规范包及SHA256清单。Linux构建另外保留，但两端消费者均需消费规范包，避免分别测试不同字节的同名版本。

## 接入与兼容范围

使用.NET10和Avalonia12.0.3，按普通JIT、依赖运行时的目录方式部署；应用需要安装对应运行时并部署平台原生资产。Windows x64是主要预览环境，Linux x64只按实际记录的WSLg/X11软件环境提供预览验证结果。未取得Ubuntu22.04/24.04实机、硬件GPU、物理触控或ARM64资格。

Linux/glibc应用在Avalonia setup或任何HarfBuzzSharp调用之前接入WithChartsNativeTextIsolation()。使用所需语言字体，保留匹配的原生库；初始化失败应修复配置并重启。Skia适配层用WithChartsSkiaRendering()显式启用。

图表和ChartDataBuffer更新遵守UI线程契约。异步导出先在UI线程捕获，再后台编码；文件导出取消/失败保留原文件，调用方流可能已有部分写入，仍由调用方管理生命周期。历史数据快照保持独立；换源、取消、卸载和旧结果处理遵循现有版本/所有权规则。

## 已知限制与后续

- 密集散点高频刷新尚未通过原固定负载门禁；保留现有功能，不承诺真实60FPS、百万点实时交互或与官方性能等价。
- 官方功能对照按用户决定跳过本版，官方性能对照、真实present和正式峰值内存资格后置。
- 专业类别图例命令、通用标签避让/滚动布局、密集独立Trackball和精确套索仍有高级差距。暗色地图浅色区域的标签对比仍需业务显式配色。
- 单文件、裁剪、NativeAOT和其他部署组合未认证；可选Skia桥接不应据此声称这些模式已通过。
- 实机、GPU、物理触控、百万点命中尾延迟和原生UIA/AT-SPI选中项桥接按既有决定后置。
- 本版长稳采用[预览可靠性工作负载](charts-preview-validation-protocol.json)，与原C7极限性能、S-RT万点/秒百万保留窗口及完整官方等价分别判断。

API/主题快照、包清单、校验和和实际验收记录随本地交付目录保存；原完整C7里程碑仍开放。
