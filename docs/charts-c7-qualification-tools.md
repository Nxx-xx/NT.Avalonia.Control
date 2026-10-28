# C7-03 内存采样、CI 入口与散点提交试验

更新日期：2026-10-27。

本轮补齐独立的匹配空宿主内存采样工具与手动 C7 原协议 CI 入口。图表产品绘制代码、Render 调用、原协议、数据量、画质和门槛均未改变。新散点顺序提交候选未达到双平台稳定收益条件，未接入产品。C7-03 仍未完成。

最新完整原协议测量仍是[记录分配与柱形批次](charts-c7-record-allocations.md)：两端各107进程，散点排空后剩余 Windows 161 / Linux 1040，柱形均0。本轮不重跑未变化的产品来宣称新的吞吐收益。

## 匹配 empty/full 内存工具

新增 `benchmarks/Charts.MemoryProbe`、`scripts/Invoke-ChartsMemoryProbe.ps1`、外部 Python sampler/oracle 和固定 JSON 契约，并将项目加入 solution。每端三对 AB/BA/AB、六个新宿主与六个采样器；empty/full 保持同窗口、字体、软件 Skia、1200×650 / 96 DPI、同依赖及固定坐标轴。full 使用原 S1M-L 百万点生成器、对象行与 MinMax；保留完整输入，不从进程占用中扣除原始数据。固定坐标轴与测量阶段构成独立诊断，不冒充原 C7 像素/吞吐轨道。

宿主先就绪；采样器精确核对进程身份并记录基线后才允许生成源数据。工作依次覆盖生成、源绑定、准备、公开录制、受控栅格和原始点命中；工作结束后宿主保活，采样器取得末次读数才释放退出。没有强制 GC，也不以 private−managed 推算 native。

| 平台与口径 | 第1对增量 MiB | 第2对 | 第3对 | 512 MiB 名义诊断 |
| --- | ---: | ---: | ---: | --- |
| Windows 采样私有提交量 | 234.945 | 234.078 | 234.570 | 三对均通过 |
| Linux 采样私有驻留量 | 226.359 | 229.531 | 229.141 | 三对均通过 |

增量逐对计算为同口径 maxObserved(full)−maxObserved(empty)，不跨进程池化、不将负差截为零。Windows working set、Linux RSS/PSS/swap 与 OS lifetime high-water 另列机器记录，不能把两平台上表数值当同一种内存直接比较。

请求采样间隔为 process 20ms、Linux smaps 100ms；实测最大起点间隔分别为 Windows 36.793ms、Linux process 39.911ms、smaps 112.517ms，全部读耗时、间隔及错误保留。采样最大值不等于瞬时精确峰值。Windows commit/lifetime peak commit 的含义见[微软计数器文档](https://learn.microsoft.com/en-us/windows/desktop/api/psapi/ns-psapi-process_memory_counters_ex)；Linux resident/high-water 与 smaps 口径见[内核 proc 文档](https://docs.kernel.org/filesystems/proc.html)。

Windows v1 已通过；Linux v1 在采样开始前被父子 UTC 换算差异拦截，未生成有效采样。实际偏差为 −660.2 至 +2438.7 微秒且方向不固定。v2 用 PID、内核启动 tick 与 boot ID 精确核验，保留父 UTC 原文并另记子进程握手 UTC，不用容差/舍入；Windows FILETIME 严格检查不变。v2 双端12个宿主与12个采样器真实退出0，独立重算266条读数、18条流的 maxima/gap/配对增量及全部握手/指纹通过。旧失败不回溯接纳。

## C7 手动 CI 入口

新增 `.github/workflows/charts-c7.yml`，现有 `charts.yml` 的 PR/Push/C4 行为及文件字节不变。默认手动启动只跑轻量契约；显式 `run_formal=true` 才在 Windows hosted software 与 Ubuntu 24.04 Xvfb 执行原107进程正式脚本。原 warmup、30秒负载、排空、数据、oracle 和数字门槛均不覆写；原非零退出、超时和负载失败保持失败。

失败分支配置 always 上传 raw、requests、datasets、PNG、收据、源/程序集指纹、缓存配置和日志；源文件缺失也写入检测收据。原生资产文件另存指纹，资产存在不等于实际加载模块。此自研CI不使用官方密钥；官方对照当前无可用许可，保持 blocked-reference。新内存 live CLI 独立运行，尚未接入该工作流的 live job。

本地新 CI 证据契约24项、内存契约37项、原 oracle33项，共94项通过；YAML、Python与PowerShell语法/结构检查通过。另对错误平台调用做本地负例实测：入口在启动正式活动前失败退出，并保留 wrapper/证据收据。远程 workflow 未触发，因此 remote pass=null，不能登记 CI 实机验收。使用步骤见项目 `scripts/README.ChartsC7Ci.md` 与 MemoryProbe README。

## 未接纳的原生顺序提交

小 C 模块把同画布、同填充/轮廓路径的原序20,000次 DrawPath 放进一次普通 GC 过渡；不合并路径、不读取目标像素、不推断活动图层、不并行。双端123项严格像素对照通过，共492张PNG；包含抗锯齿开关、透明度、裁剪、图层、整数/分数平移及1.25/1.5倍矩阵、真实10000点与活画刷。

每端三对新进程 AB/BA/AB、各10秒；N10模型每组重建句柄表，记录准备、固定、10次绘制及释放成本，冷路径准备另列。以下为每进程各自的核算成本 ms/draw；仅 owned native 诊断，不是原 C7 请求、生产 lease 或 present。

| 平台 / 对 | direct | batch | 变化 |
| --- | ---: | ---: | ---: |
| windows / 1 | 18.923700 | 18.513144 | -2.170% |
| windows / 2 | 19.006983 | 18.496145 | -2.688% |
| windows / 3 | 19.125273 | 18.617450 | -2.655% |
| linux / 1 | 26.115739 | 25.792100 | -1.239% |
| linux / 2 | 25.897783 | 26.515741 | +2.386% |
| linux / 3 | 26.066104 | 26.541642 | +1.824% |

Windows三对较快，Linux两对退化；稳定双平台接入条件不成立，产品未接入。本轮原型结果序列化失败、审计 PascalCase 字段失配及其修复均保留；新版本重新验证，不改原raw或门槛。公开 Geometry.Clone 的浅克隆能力也重新核实，但手工树交回 SDK recorder 仍逐项重录，无法省掉当前主要分配。

## 剩余项

- 自研散点固定负载积压与尾部波动，继续按未变原协议处理。
- 官方许可缺失，对照 measurements/ratios=null。
- 真实 render/present 及正式匹配峰值 SLA 仍未取得；本轮 sampled 工具不翻转原 oracle 的 null 资格。
- Ubuntu22.04/24.04实机、硬件GPU、物理触控、百万点命中尾延迟和原生 UIA/AT-SPI 选中项桥接保持用户后置决定。

完整输入指纹、独立审计、逐对原值、失败历史和外部证据索引见[机器记录](charts-c7-qualification-tools-validation.json)。
