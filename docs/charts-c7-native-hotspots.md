# C7-03 原生散点热点与绘制路径诊断

更新日期：2026-10-26。状态：`diagnostic-complete-no-product-optimization-accepted`；C7-03 仍未完成。

本批把 Linux 散点成本定位推进到实际加载的 Skia 原生函数，并完成 Windows / Linux 的显式初始化交替配对。没有把诊断结果登记为吞吐通过，也没有改变数据、画质、性能门槛或 B8 产品绘制代码。B9 批量解析、B10 完整向量复用的拒绝结论保留。

本批证据根目录：

`C:/Users/a4472/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c7-native-hotspots-20261026`

[机器核验记录](charts-c7-native-hotspots-validation.json)登记输入、过程、图片、原始采样及校验脚本指纹。历史正式结果仍见[B8 积压优化报告](charts-c7-backlog-optimization.md)及其[验证 JSON](charts-c7-backlog-optimization-validation.json)；本批没有新增完整 formal 复测。

## 实际依赖与采样能力

实际依赖是 **SkiaSharp 3.119.4-preview.1.1**，原生 getter 返回 **119.0**。包版本与原生版本不是同一字段，不能用稳定版 3.119.4 的源码代替本次二进制证据。

- Linux 实际加载的 `libSkiaSharp.so` 为 11,170,296 bytes，SHA256 `77924699617FD9C8E04127C4CA797279DB041E6161AFCBDC44B371D9D81B1675`，ELF Build-ID `76dd495c578396098614d9ebb9a69d9b0b5690d8`。
- Windows 实际加载的原生库为 11,628,576 bytes，SHA256 `6D6ECA64EC333DAED78858A637B54C545AA01558204B8E2AE8870692ACADA127`。
- Linux 库保留 25,021 个已定义符号及展开信息，但没有 `.debug_*` 源码调试节。本批支持函数级定位，没有取得源代码行和 inline 归因资格。
- 每个 producer 保存自己的映射、PID、完整启动时间、程序集及原生库指纹；profiler supervisor PID 与 producer PID 分开登记。B10 未保存的历史原生映射没有被补造。

Linux 环境为 Ubuntu 26.04 / WSL2 / WSLg 的软件诊断。私有解包的 perf 7.0.14 与 WSL 内核 6.6.114.1 存在版本差异；用户态 `cpu-clock:u`、DWARF smoke 与实际捕获均成功。没有安装系统工具、调整 sysctl 或取得硬件 GPU / 实机资格。

## 独立探针及坐标边界

探针使用冻结的 C0 `S10-S` revision0 全部 10,000 个点，源顺序、点数及源数据哈希固定，1200×650 BGRA8888 / Premul 独立软件 bitmap，半径 2、描边 2，实际布局的 plot clip，ClipRect AA=false。每批按源顺序发出 20,000 次原生调用：fill[i]、outline[i]。全部路径、paint 和 fixture 在计时外准备；Clear、读回、hash、JSON 和 PNG 写入不进入 draw 窗口。

draw 窗口仍包含 managed loop、SkiaSharp wrapper、P/Invoke 与原生绘制，不能称为纯 Skia CPU 时间。计划测量秒数指包含 Clear 和批间开销的整个循环，最后一批可越过截止时间；计时期间不逐帧读回图片。

`paths` 使用 AddOval + GetFillPath 预展开轮廓；`ovals` 使用 DrawOval Fill + Stroke。它们是独立 native-only fixture，**没有调用产品的 ChartNativeCircleOperation.Render**，不覆盖公开 DrawingGroup 记录、缓存解析、请求排队、窗口 present 或 GPU，也不替代 232 幅产品保真回归。

v1/v2 使用普通乘加投影；审查发现生产 ChartLinearMapping.Map 使用 FMA 和端点规则。因此 v1/v2 只具有自身 fixture 的像素一致性资格，不能声称每个屏幕坐标 bit 与生产相同。v3 单独链接冻结的生产映射实现，并与实际 chart 记录后 RetainedPoints 的所有坐标 bit 对照，结果在验证 JSON 独立登记。旧记录不改名、不补写资格。

每个 v2 进程保留 24 组初始化前后 paths 比较及 24 组 paths/ovals 比较，共 72 张 PNG，覆盖透明度、重叠、边缘、分数半径/描边及 none / integer / fractional clip。前后比较中的 post-stage 重新构建 Scene，因此没有验证主 timed Scene 中既存路径对象跨 Init 的像素生命周期。没有据此提出生产初始化改动。

### v3 生产坐标和路径结构复核

v3 语义探针的 Windows / Linux 成功构建均归档为 build-v4；两端使用相同冻结的五个探针文件。Windows 首次 build-v3 在受限环境的 initial-restore 返回 exit 1，只有 37-byte stdout、无 stderr；失败收据保留。相同源码在允许本机缓存访问的独立新目录完成初始还原、锁定还原和构建，未关闭审计、修改源码或把失败改记成功。失败的确切原因没有从原始输出中取得。

两端各一个未采样的 2 秒 smoke 均完成，生产映射源码 SHA 为 `534B3B53AFEB39DAE9C3F45FF816F86085835643F05C003366AADC945BF8A852`。准备阶段正常调用 chart.Render 产生真实 retained centers，再与链接的原实现逐位、逐序比较；比较 sidecar 保留全部源点和两套中心位值，可独立重算哈希。

| 平台 | 生产坐标比较 / 差异 | 旧 v2 中心位差异 | 实际填充及轮廓路径比较 / 结构差异 | 自身 fixture 严格像素检查 |
| --- | --- | --- | --- | --- |
| Windows | 10,000 / 0 | 3,886 | 20,000 / 0 | 通过 |
| Linux | 10,000 / 0 | 3,963 | 20,000 / 0 | 通过 |

路径结构比较借用实际模块 GetFastShapes 的 paths，核对 FillType、RawIterator verb、有效 point 的 float bit 和 ConicWeight；Close 不读取 point payload。生产 owner 保活，借用 paths 不释放。timed paths 仍来自独立本地 fixture，未比较 Skia 私有 shape hints / metadata；结构一致不能扩写为产品像素、渲染路径或正式性能资格。所选 Windows / Linux 分数裁剪 PNG 已人工查看，图像包含完整散点与裁剪边界。

## Windows / Linux 显式初始化交替配对

两端各三对新进程，顺序 false/true、true/false、false/true；每进程 10 批预热，测量循环计划 10 秒。false 不调用 SKGraphics.Init，true 在 NativeScene 建立后显式调用；调用前已有自动初始化的状态未知。每个进程独立计算 nearest-rank 分位数，不混样，不作为 30 秒固定负载倍率。

表中数值均为 **未显式调用 → 显式调用**，单位 ms；P50/P95 仅描述各自完成的 draw 批次，批次数不同。

| 平台/对 | 批次数 | draw P50 | draw P95 |
| --- | --- | --- | --- |
| Windows 1 | 534 → 534 | 18.4682 → 18.4333 | 19.3940 → 19.5607 |
| Windows 2 | 519 → 522 | 18.7966 → 18.5679 | 21.1242 → 21.5937 |
| Windows 3 | 512 → 535 | 18.7630 → 18.4501 | 23.8812 → 19.3750 |
| Linux 1 | 386 → 392 | 25.656395 → 25.283277 | 26.725965 → 26.452368 |
| Linux 2 | 389 → 389 | 25.392843 → 25.477160 | 27.089287 → 26.556863 |
| Linux 3 | 390 → 388 | 25.333245 → 25.363138 | 26.711273 → 27.102452 |

12 个进程均完成，原生身份稳定，所有自身 fixture 的严格 BGRA 比较通过。Windows P50 小幅下降而 P95 混合，Linux 两对 P50 上升；没有稳定的跨平台收益，**不接受显式初始化作为性能修复**。静态扫描仅确认公开 Init 调用链，不能证明原来未初始化。

Windows 另有 2 秒开发 smoke：paths P50 18.3034 / P95 18.8590 ms，ovals P50 38.5308 / P95 42.2940 ms；各 24 组表示比较通过。它们不是交替配对或正式倍率，但已表明直接 Oval 不能凭调用形式假定更快，本批不采用这一替换。

## Linux 原生热点

v1 首个测量循环计划 20 秒的 traced 捕获收到 13,639 个叶样本，全部解析；按 producer 原生 TID、CLOCK_MONOTONIC 纳秒、case 与半开 draw 区间精确分类后：

| 分类 | 叶样本 |
| --- | --- |
| measurement draw | 9,892 |
| measurement Clear | 63 |
| warmup draw | 129 |
| warmup Clear | 0 |
| 窗口外 | 3,353 |
| 其他线程 | 202 |
| 其他 case | 0 |

draw 中有 9,355 个 Skia 命名叶样本，333 个未知符号，未知 DSO 为 0；未知项保留 IP 和库名。主要命名叶样本为 MaskSuperBlitter::blitH 2,463、SkQuadraticEdge::updateQuadratic 866、sk_fill_path 669、setQuadraticWithoutUpdate 557、sse2::blit_mask_d32_a8 494、SkBasicEdgeBuilder::addQuad 457。实际命名 SSE2 样本已采到；未采到 AVX/HSW 命名叶样本不能证明未分派，也不排除内联 SIMD。

这些结果支持下一步优先研究抗锯齿覆盖与曲线边缘生成的重复成本。leaf count 不是精确 CPU 百分比，不能当成纯 P/Invoke 时间，也不能从各个阶段 P95 相加得到端到端耗时。

原始统计保留了 Aggregated LOST events=2、Aggregated LOST_SAMPLES events=1，以及 cpu-clock:u 区段 LOST_SAMPLES events=26。它们的计数含义不同，不能相加；没有足够时间/TID 信息把失采分配给 draw，不能修正 draw 分母或宣称零失采。原始 perf.data、stack/leaf 导出、stats、build IDs、stdout/stderr 和 process receipts 全部保留。

### v3 重新采样

通过上述生产坐标 / 路径结构复核的 v3，在 Linux 重新执行 20 秒测量循环并采集 499 Hz 用户态软件事件。778 个 draw 批次完成；draw P50 25.456924 / P95 26.614216 ms 为 **traced 独立 fixture 诊断**，不与未采样配对计时混样或计算产品倍率。

收到 15,151 个叶样本，未解析行 0；measurement draw 9,924、measurement Clear 57、warmup draw 131、warmup Clear 0、窗口外 4,853、其他线程 186、其他 case 0。draw 中 Skia 命名叶样本 9,364，未知符号 360，未知 DSO 0，全部保留。

| v3 主要 draw 叶函数 | 样本 |
| --- | --- |
| MaskSuperBlitter::blitH | 2,410 |
| SkQuadraticEdge::updateQuadratic | 826 |
| sk_fill_path | 710 |
| setQuadraticWithoutUpdate | 565 |
| sse2::blit_mask_d32_a8 | 494 |
| SkBasicEdgeBuilder::addQuad | 438 |

v3 原始统计未列出 LOST / LOST_SAMPLES 行，仍不宣称零失采。perf 报告头的 first/last/sample duration 为 0，未用来计算时长；分类依据保留的 leaf 单调时间和 producer 的 CLOCK_MONOTONIC 窗口。v3 与 v1 分别登记，不合并样本；新结果仍指向 AA coverage 与曲线边缘工作，未得到初始化修复的依据。

## 仓库工具与验证

新增[解析器](../scripts/charts_c7_native_profile.py)、[合成测试](../scripts/test_charts_c7_native_profile.py)和[使用说明](../scripts/README.md)。解析器核对 producer 身份、TID、case、时钟、窗口不重叠、终态批数，并保留全部未知符号、未解析行和失采证据。解析通过与产品性能通过分别记录。

Charts CI 增加这组纯 Python 合约测试，不在 CI 自动开启 perf 或变更性能门槛。本批只验证本地执行，未触发远程 GitHub Actions，也没有应用 B10 的性能 CI 草案。

接入时的 v1 真实捕获重放与原验证版进行 22 项比较全部一致，逐样本及未解析行 JSONL 逐字节一致。最终仓库解析器的 12 项合成测试通过，并独立重算 v3 的全部坐标 sidecar、源数据与中心哈希后完成真实采样解析；v1/v2 明确列为 legacy-unqualified，不回溯补写生产资格。本批未重新运行产品全量测试或完整正式性能活动；没有产品绘制改动需要登记为通过。B8/B10 的历史全量和保真结果保持原有候选、版本及适用范围。

## 剩余问题与下一步

1. 双平台 exact 散点仍未通过固定负载。正式记录继续使用 B8：Windows pending 235 / 9000，Linux pending 1028 / 9000；不能用本批短诊断替换。
2. 原生 AA 和曲线边缘开销已经有函数级证据；下一项候选应优先减少边缘生成重复计算或逐点记录分配，并同时观察完整请求管线，先经严格产品像素验证，再运行原协议交替配对及完整门禁。不得重新引入已拒绝的 A8 mask、圆形合并、单位环平移或完整向量 Memo。
3. 公开 DrawingGroup 记录仍有逐点包装和分配。记录与原生 raster 是不同成本，后者变快也不能直接宣称记录成本消失；独立可变导出语义须保留。
4. 百万点首次可用仍观察 B8 的 Windows 1189.5808 / Linux 1335.665949 ms P95，未用本批探针复测；峰值内存和正式 render/present 资格仍未取得。
5. 官方许可仍缺失，`blocked-reference`；官方测量与公平倍率保持 null。Ubuntu 22.04/24.04 实机、硬件 GPU、物理触控及用户已后置的无障碍专项继续待验。

已保留一个后续可证伪方向：在严格受控的变换、裁剪和误差选择条件下，预生成与当前 Skia 内部完全相同的 conic→quadratic 序列，减少重复转换。当前只取得静态调用链证据；自动细分等级、字节预算、私有路径 metadata 和严格产品像素一致尚未证明，本批没有实现或接受该方案，也不预计它能消除主要 coverage 混合成本。

