# Charts C4-07：多图与后台布局

执行日期：2026-10-20。冻结候选 `c4-multichart-background-layout-source-v2` 的 Windows 原生合成与 Ubuntu26.04 WSL2/WSLg 软件验证已通过。Demo 新增六图面板，Usage 与两类隔离包消费者覆盖公开 API。下一项为 **C4-08 自动化性能门禁**；本轮不关闭完整 C4 或实际平台/性能资格。

## 实现与使用

- `RequestRender()` 将图表刷新请求交给进程共享 FIFO 队列；重复请求合并，每次 dispatcher 回调派发一张图的失效请求。`GetWorkSchedulingInfo()` 报告是否活动、待派发及请求/合并/派发计数。它保证的是请求轮次，不是均分整图 CPU 或每帧完成。
- 图表跟踪自身及祖先可见性。隐藏/卸载后取消刷新与后台任务、释放可重建本地缓存；显示/挂回后根据最新数据重建，旧结果不能覆盖当前版本。显式查询和导出仍允许准备数据。
- `UseBackgroundLayout` 默认 false；开启后，支持的内置非线性折线/面积/范围面积/ScatterLine与兼容平滑堆叠使用数字快照构建路径部件。业务 getter、数据映射、投影、轴文字、原生几何和画刷留在 UI 线程；worker 不携带业务对象或 Avalonia 绘图资源。
- `PrepareBackgroundLayoutAsync(CancellationToken)` 仅在当前 worker 结果已发布时返回 true；取消、失效或同步回退返回 false，预先取消的 token 抛取消异常。`CancelBackgroundLayout()` 取消当前准备并恢复同步路径；`GetBackgroundLayoutInfo()` 提供版本、提交/发布/取消/过期/失败/回退及 worker 线程诊断。支持路径待发布期间暂缺，依赖完整结果时先 await；导出始终同步准备完整图形。
- `ChartWorkScheduler` 提供跨图五层本地缓存预算、数字载荷预算、最大预约数及统计/清理。默认缓存512MiB、数字载荷64MiB、16项预约，最多2个 worker；捕获前预留权重，拒绝准入时同步回退。后台捕获另限128个支持系列和131072个源顶点。预算都是模型估算，不是进程、原生或 GPU 内存硬上限。
- 数字任务正在执行时降低预算会合作取消，载荷在实际结束前继续计费，短时间可能高于新限额。发布后的共享预备路径及其他 CWT 缓存仍在该在途账本之外。配置/统计需 UI 线程；RequestRender 允许跨线程并合并到 UI，统计以 UI 接纳的请求为准。

Demo 的“多图与后台布局”展示六张512点平滑折线、同时刷新、三次连续换源、隐藏/显示、卸载/挂回、进程预算与状态；两个主题各6张截图；恢复按钮或面板释放会还原原进程配置，折叠仅停工作。Usage 展示两张 `UseBackgroundLayout` 双向编译绑定图、换源命令与公开统计；包消费者只引用本轮产品包，没有源码项目引用。

## Windows 与 Linux 验证

| 项目 | Windows 原生合成 | Ubuntu26.04 WSL2/WSLg |
| --- | --- | --- |
| Charts 测试 | 1939/1939 | 1939/1939 |
| 完整原生 Demo | 1414/1414 | 1414/1414 |
| 编译绑定 Usage | 393/393 | 393/393 |
| 仅功能包消费者 | 553/553 | 553/553 |
| 功能与 Themes 消费者 | 553/553 | 553/553 |
| Release 警告/错误 | 0/0 | 0/0 |
| 基础/消费者/API/缓存/裁剪/LOD/命中/增量/资源/多图阶段退出码 | 全部0 | 全部0 |

冻结的两个平台副本和 Windows 短路径副本共 1514 个同 SHA 文件；790 个声明运行输入在成功冻结前后稳定，实际基础验证每平台 774 个输入与候选相同。项目缓存和两类消费者缓存均为新建，14个非产品消费者依赖及63个项目依赖归档按 SHA 复用，未改 audit/警告策略。Windows 与 Linux 顺序运行，完整原生 Demo 保留旧门槛；C4-01～C4-06 机制阶段按原门槛重跑。本轮仅对新的多图机制/像素增加独立审阅，不把历史独立 reader 当作本轮重新执行。

本轮完整本地公开 API 类型 169→173，新增 `ChartBackgroundLayoutInfo`, `ChartSchedulingInfo`, `ChartWorkBudgetInfo`, `ChartWorkScheduler`；无旧类型/声明删除，两平台声明一致。API快照是本地兼容核对，不证明官方 API 或二进制等价。

固定多图 benchmark 使用4张每系列2048点的图（2张平滑折线、2张双层平滑堆叠面积）、1000次单图请求风暴、其余图刷新、版本替换、隐藏/挂回、预算拒绝与缩减。17项机制门槛要求所有图获得公平派发、后台数字结果与同步实际路径相等、命中仍返回原始 ID/序号/坐标、旧版本不可覆盖新数据、队列/估算预算及取消/释放符合契约。独立 reader 从原始标量重算每边界断点分段2015、双层堆叠面积图8060个上下边界部件及序号200的原始公式、调度和预算条件；路径 hash 为实际同步/worker结果一致性核对，并非独立 opaque 几何 golden。不比较跨平台路径hash；JSON无法重建对象引用，编译绑定的运行时快照断言另检查Item引用一致性。独立审阅 Windows通过147项、Linux通过159项谓词，数量随实际预算样本数变化，17项生产门槛相同。记录捕获线程分配/耗时与状态样本作为诊断；不设正式帧时限或内存 SLA。

新增原生检查每主题15项，每平台30项。独立 PNG reader 解码12张新图，核对每主题2组全 RGBA 画布零差、初始/最新版本的蓝色业务线 ROI，并重算6×512行最新原始数据公式；两平台均通过 38 项独立谓词。窗口截图由原生窗口的 RenderTargetBitmap 生成，未验证 compositor present 或物理输入。

## 失败、环境与证据

- 开发 Demo v1因RequestRender尚为private而编译3错；v2在新源已公开但 BuildProjectReferences=false 仍使用旧核心 DLL 时同样3错。未执行原生场景。核心重编后 v3通过30项新原生与393项Usage，两次失败及真实日志保留。
- 最后聚焦核心验证曾因夹具把既有属性观察器维护 getter 计入隐藏刷新准备而70/71通过；调整为隐藏通知后的严格 snapshot/build/prepare计数基线后71/71通过，没有放宽产品门槛。后台布局开发期还修复了外部取消落在数值完成与UI发布之间时busy状态未清理的问题；另一次失败为测试把ChartPoint装箱对象误作引用身份，改为值与StableId核对。最终完整测试结果见上表。
- Linux首次工具探测在脚本入口因新 shell 文件CRLF导致 `pipefail\r` 失败，尚未执行产品；仅将新脚本改LF，后续环境探测通过。沿用现有 SHA 校验的SDK10.0.402、PowerShell/RG/ICU/X11库和字体，未安装系统包；明确记录SDK为只读复用。
- 首次 source-v1 冻结中，唯一发生变化的是并行任务的 PdfViewer.Interaction.cs；冻结稳定门禁为false，副本与复制结果保留，未正式执行。最初外层两条命令缺少失败短路导致仍生成了短路径副本，该副本未获资格。改为逐步检查退出码，并在正式 Windows launcher 加入冻结稳定强制门禁。source-v2冻结前后稳定，正式两平台均明确绑定source-v2；不改动并行PdfViewer源码，也不宣称当前整仓等于冻结副本。
- 采集器首版仅解析命名空间导出类型（167→171），后补齐两程序集的生成XAML loader类型并以程序集/类型作键（169→173）；两份收据及范围修正说明保留，未改变产品或门槛。
- Windows从本轮开始使用 `<REPO_ROOT>/artifacts/c407/windows-source-v2` 与全新短缓存，沿用已知长路径环境规避。历史 C4-06 的 libHarfBuzzSharp 0x800700CE 探测路径尚未定位，当前通过不算加载器修复。旧动画时序失败继续保留。

证据根目录：`<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-multichart-background-layout-20261020`。

- [Windows完整结果](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-multichart-background-layout-20261020/windows/windows-final-v1/results.json)；[Linux完整结果](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-multichart-background-layout-20261020/linux/run-v1/verification/wslg-final-v1/results.json)。
- [Windows亮色六图](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-multichart-background-layout-20261020/windows/windows-final-v1/charts-light-multichart-light-overview.png)；[Windows暗色六图](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-multichart-background-layout-20261020/windows/windows-final-v1/charts-light-multichart-dark-overview.png)。
- [Linux亮色六图](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-multichart-background-layout-20261020/linux/run-v1/verification/wslg-final-v1/charts-light-multichart-light-overview.png)；[Linux暗色六图](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-multichart-background-layout-20261020/linux/run-v1/verification/wslg-final-v1/charts-light-multichart-dark-overview.png)。
- [验证清单](charts-c4-multichart-background-layout-validation.json)；[公开API](charts-c4-multichart-background-layout-api.txt)。每平台共737张PNG，其中新增多图12张；旧截图与历史失败证据不覆盖。

## 剩余问题

- 下一项 C4-08 自动化性能门禁；完整 C2/C3/C4/C7、C5/C6 图族及官方许可运行时/API/二进制对照仍开放。
- 公平性约束 RequestRender 失效请求的 FIFO 合并与调度轮次，不切分整图 CPU 时间；数据映射、可见查询、极值/索引、投影、坐标轴、原生几何、绘制和 compositor present 仍未取得全后台或帧时限资格。
- 后台默认关闭；仅对内置稳定 Cartesian 的非线性折线、面积、范围面积、ScatterLine 和兼容平滑堆叠路径生成纯数字路径部件。普通线性 LineSeries 及其 MinMax 路径、自定义、无支持路径、动画及超预算情形同步回退；支持路径待发布时会暂缺，需 await PrepareBackgroundLayoutAsync。导出强制同步以避免输出缺图。
- 隐藏/卸载按自身及祖先 IsVisible/视觉树连接判断，不检测滚动视口外或被其他窗口遮挡；停止自动调度和正在准备的后台工作，并释放可重建本地缓存。显式查询、CreateSnapshot、Prepare 请求的同步回退或导出仍可能主动准备数据，不承诺隐藏对象任意调用都零 CPU。
- 进程预算只覆盖注册的五层本地缓存估算和在途数字任务的固定权重估算，不覆盖共享 source/index/LOD/PreparedPaths 等 CWT、已发布路径、调用方业务对象/旧快照、临时工作、原生/GPU或实际进程堆。未建立实际内存硬上限。
- 最多两 worker、默认16项预约、默认64MiB数字载荷预算；硬上限128个支持系列/131072原始顶点，另按2304B/源顶点加固定量保守准入。降低任务预算会取消工作，但正在运行的载荷继续计费，合作取消完成前可能临时超过新预算；不抹去运行中的账目。
- 本轮固定四图2048点与六图512点验证是机制/正确性检查，不代表百万点首次可用、完整 UI 帧公平性、CPU/FPS/尾延迟、实际峰值、持续吞吐或30分钟/8小时长稳资格。
- Windows 原生合成与 Ubuntu26.04 WSL2/WSLg 的功能验证不代表物理 Windows/Linux 桌面、原生 Wayland、ARM64、国产 Linux、GPU/backend、DPI/触摸设备、读屏器或 compositor present 验收。
- 百万点 hit p95 历史 0.7649→1.1235 ms 按用户决定后置；A11Y-NATIVE-01 原生 UIA/AT-SPI 选中枚举专项、AX06及四份历史 PixelProbe DLL 缺口仍开放。
- C4-03 Linux 历史导出/动画暂停前置状态的三项时序失败、C4-06 Windows旧动画 viewport错过中间帧及原长路径 libHarfBuzzSharp OS0x800700CE 问题保留；当前短路径与通过结果不视为修复加载器或动画时序稳健性。
