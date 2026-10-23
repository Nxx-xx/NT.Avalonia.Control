# Charts C4-08：自动化性能门禁

执行日期：2026-10-20。冻结候选 `c4-ci-performance-gates-source-v1` 的 Windows 本地原生与 Ubuntu26.04 WSL2/WSLg 验证已通过。benchmark、真实原生窗口驱动、原始数据/trace、差异守卫与失败证据已接入 `.github/workflows/charts.yml`。本轮验证了实际本地 CI 入口，**没有执行 GitHub 远端 run**。下一主项为 **C5-01 统计与分布**；完整 C4 性能及平台资格仍开放。

## 执行入口与 CI

- `scripts/Invoke-ChartsPerformanceGate.ps1` 在独立子进程中 Release 构建两个 host，顺序执行 C4-01～C4-07 七个既有机制、三个10k通用benchmark场景及四个native窗口场景，之后构建/运行独立源码基线并调用严格守卫。Linux CI 包装入口使用 Xvfb；本轮 WSLg 直接调用 PowerShell 入口，明确记录环境。
- CI 性能matrix配置 `windows-2025`、`ubuntu-22.04`、`ubuntu-24.04`，覆盖 push、pull_request、定时及手动入口；PR 取独立 base SHA 产品，其他事件取同 SHA 重复控制。native harness 在基线独立目录中核对/归档后叠加，保留产品源不变。两平台性能任务均先执行 Python oracle/guard 与 PowerShell 监督契约。
- 子进程保存准确命令、stdout/stderr、退出码、超时和部分输出；不自动重试。策略在测量前复制并记 SHA，守卫前封存输入证据清单，结束后生成完整 checksum。缺原始数据、空/假检查、SHA、环境、原始身份、业务像素、计时/分配或失败率不满足条件时拒绝。
- `upload-artifact` 使用 `if: always()`、独立 run/attempt 名称和30天保留期，上传成功及失败的 raw JSON、原始数据、JSONL trace、PNG、进程/源/载荷/策略收据。这里只确认工作流文件已连接及本地命令通过，远端 runner 尚未取得通过资格。

Windows本地入口使用 `-NativeMode DesktopSynthetic`，WSLg使用 `-NativeMode NativeSynthetic`；CI Linux包装器固定 `XvfbSynthetic`。均必须提供独立 `-BaselineSourceDirectory` 并使用唯一 `-OutputDirectory`。具体命令及先决条件见 [benchmark说明](../benchmarks/Charts.Benchmarks/README.md)、[native host说明](../benchmarks/Charts.NativeBenchmarks/README.md)与[工作流](../.github/workflows/charts.yml)。

## 门槛与测量口径

既有七阶段的 flags、机制与数值门槛未放宽。通用benchmark固定 `S10-L/S10-S/S10-B`、5次记录/1次预热；native固定折线/散点/柱状各10000点与四图各512点、每场景12次记录/2次预热。守卫从 raw 样本重算 p50/p95/线程分配，检查数据公式、原始序号/ID/坐标、预定查询与投影、版本、关联trace和原生窗口生命周期；独立解码PNG并核对业务ROI、同状态冷树及基线像素。

策略见 [charts-ci-performance-policy.json](../scripts/charts-ci-performance-policy.json)。native相对限额使用 `max(基线×比例, 基线+噪声量)`：p50为1.5倍/5ms，p95为2倍/20ms，p95分配为1.25倍/1MiB；还有固定绝对上限、零失败和零不同像素。它们是 hosted 回归守卫，不是完整 C4 正式 SLA。9个producer的失败率与native逐样本失败率分别计算；基线、构建或守卫失败也会拒绝总run。

native通过真实desktop backend创建可见且有非零handle的窗口，禁止加载Headless。记录 viewport、数据更换与合成Ctrl+Home，从request、UI apply/layout、RAF callback到受控capture完成，并单独记录原始命中。capture区间包括RenderTargetBitmap.Render、PNG写出、CopyPixels/逐行复制、像素统计与SHA；冷树重建/对照在测量区间外。UI apply/layout、capture和hit的线程分配各自在无await的区间读取，不能当作全进程或render线程总分配。RAF调度和离屏像素均不代表compositor present/GPU fence。

Native protocol v2 显式声明并核对 `System.Runtime.TieredCompilation=false`、存在且为false的AppContext switch和允许的环境覆盖；策略同时比较该配置和OS、架构、runtime、CPU数、字体、DPI/缩放、主题、culture及native handle描述。数值、比例、样本数和预热数与策略v1相同，原因及旧SHA见[协议变更收据](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-ci-performance-gates-20261020/policy-protocol-v2-change-receipt.json)。该配置只属于benchmark host，未修改Charts库或应用默认JIT策略。

## Windows 与 Linux 验证

| 项目 | Windows本地原生合成 | Ubuntu26.04 WSL2/WSLg |
| --- | --- | --- |
| Charts测试 | 1939/1939 | 1939/1939 |
| 完整原生Demo | 1414/1414 | 1414/1414 |
| 编译绑定Usage | 393/393 | 393/393 |
| 仅功能包消费者 | 553/553 | 553/553 |
| 功能与Themes消费者 | 553/553 | 553/553 |
| Release警告/错误 | 0/0 | 0/0 |
| CI 9个producer失败率 | 0/9 | 0/9 |
| 严格守卫谓词 | 4204/4204 | 4204/4204 |
| native当前/独立基线样本 | 56/56 | 56/56 |
| native PNG/原始dataset文件 | 224/8 | 224/8 |

每平台另通过39项Python契约、30项PowerShell监督/CI谓词及PowerShell入口的15项主guard契约。后15项与39项中的主guard用例重叠，不累加为独立产品测试。两套合成正负用例各保留27份case收据（其中25份预期拒绝），逐项核对CAS内容和所有文件绑定；这些是假数据的guard测试，与真实native样本分开。

每份副本均有 1524 个源文件，五份副本的对应文件SHA逐项一致，857个声明输入冻结前后稳定；基础验证每平台774个输入与冻结候选相同。Windows随后Linux顺序运行；各平台项目cache首次为空，两类消费者各用独立新cache，14个非产品消费者依赖和63个项目依赖归档按SHA复用。CI和独立基线构建复用本次新建的依赖cache，不复用历史产品二进制；audit/警告策略未改。Windows使用短路径；Linux只读复用既有SDK10.0.402、PowerShell/RG/ICU/X11库及字体，未安装系统包。Windows Python3.12.6与Linux既有Python3.14.4分别运行相同stdlib守卫。

本地正式baseline是同候选独立源码/新进程，Git SHA缺失如实为空；不能据此声称相对历史产品提速。整仓未提交的C4历史改动及并行PdfViewer工作保持，未声称活动整个仓库等于冻结副本。完整本地公开API 173→173，无类型/声明增删，两平台一致；这不是官方或二进制等价认证。

## 失败及诊断记录

- native开发v1因10000个最小宽度柱体重叠而原始点oracle选点不适合，改为确定的最大高度柱体后保持严格原始索引/ID/坐标检查，没有修改产品命中算法。v3的Rect编译失败后误用旧binary及原子rename失败、v4的rename UnauthorizedAccess均保留；v5改为初始raw、不可变状态快照及末次一次性raw，56样本通过。未把失败开发载荷作为正式候选。
- PowerShell监督开发v1捕获异步GetResult输出混入返回值的问题，修正后原有检查保留；早期默认TEMP被sandbox拒绝PNG写入/清理属于工具夹具。guard-native-v1为1132/1164，32项false来自reader把实际routed-key-reset写成keyboard；初始主guard13项测试有2个错误，暴露异常处理重复completed predicate，均在正式前修正并保留日志。
- **实际CI入口dev-v1的9个producer与baseline均通过，严格守卫仍真实拒绝4195/4196**：scatter layout p50为11.2758ms，基线5.1135ms，限额10.1135ms。143个native/product输入SHA及比较环境相同，双方慢到快切换位置不同（候选10、基线7）；具体原因未证明。旧raw/策略/载荷/日志未覆盖，不能把它写作产品已修复或无失败。
- 后续将native测量配置显式固定并可核验，策略升级v2且保留原数值/比例/样本/预热。完整dev-v2的9阶段及paired control、4204/4204守卫通过；这是测量协议稳定化尝试的验证，不证明JIT因果。之后仅增加合成unit证据留档钩子，15项入口再次通过；实际窗口、门槛和产品代码不变。
- Windows正式launcher-v1的base、两消费者和API均通过，但external unithelper未创建自定义evidence父目录，使15项主guard在setUpClass前失败（24项模块已通过），原launcher退出1及失败helper字节保持。仅修外部mkdir/证据路径，使用独立短路径units-v2执行39/30/15，并首次执行CIentry-v1；已通过的产品/API不重复。qualified continuation核对同一1524源文件及8份已完成阶段载荷前后SHA，项目cache仍是同次正式候选的新cache。Linux使用修正helper完成五阶段；不把Windows首launcher改写为全0或首次通过。
- 独立collector首轮82/83因继承历史清单把滚动charts-official-alignment.md仍pin在更旧版本而失败；任务初始metadata与source-v1已含C407最终SHA。首次发布后的文档proof229/230又发现同一清单把滚动platform-matrix也当不可变，误报本轮预期更新。两个失败、旧pin、首次发布文件与分类修正收据均保留；最终95份真正历史逐SHA不变，两个滚动索引的初始完整字节另存冻结副本/before文件。最终只读collector84/84通过，不涉及产品、原始测量或数值门槛修改。
- 本轮正式失败与阶段终态以原始launcher/results/CI收据为准，旧C4-03/C4-06时序和长路径问题继续保留。失败不通过过滤样本、改阈值或自动重试隐藏。

证据根：`<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-ci-performance-gates-20261020`。

- [Windows完整功能结果](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-ci-performance-gates-20261020/windows/windows-final-v1/results.json)、[Linux完整功能结果](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-ci-performance-gates-20261020/linux/run-v1/verification/wslg-final-v1/results.json)。
- [Windows CI入口](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-ci-performance-gates-20261020/windows/ci-performance-v1/orchestration.json)、[Linux CI入口](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-ci-performance-gates-20261020/linux/run-v1/ci-performance-v1/orchestration.json)；[Windows严格守卫](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-ci-performance-gates-20261020/windows/ci-performance-v1/guard/result.json)、[Linux严格守卫](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-ci-performance-gates-20261020/linux/run-v1/ci-performance-v1/guard/result.json)。
- [Windows四图窗口](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-ci-performance-gates-20261020/windows/ci-performance-v1/native-window/png/multi-4x512-013.png)、[Windows散点](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-ci-performance-gates-20261020/windows/ci-performance-v1/native-window/png/scatter-10k-013.png)；[Linux四图窗口](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-ci-performance-gates-20261020/linux/run-v1/ci-performance-v1/native-window/png/multi-4x512-013.png)、[Linux散点](<USER_HOME>/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c4-ci-performance-gates-20261020/linux/run-v1/ci-performance-v1/native-window/png/scatter-10k-013.png)。像素守卫在各自平台内部比较，不要求Windows/Linux跨平台SHA相同。
- [保留的dev-v1失败](<REPO_ROOT>/artifacts/c408/development/ci-entry-v1/guard/result.md)；[验证清单](charts-c4-ci-performance-gates-validation.json)、[公开API](charts-c4-ci-performance-gates-api.txt)。每平台旧Demo仍有737张PNG，新native当前/基线图像另存各自CI目录。

## 剩余问题

- 下一主项 C5-01 统计与分布；C4-01～C4-08 指定软件交付已推进，完整 C2/C3/C4/C7、剩余图族与官方许可运行时/API/二进制对照仍开放。
- 本轮实际执行的是 Windows 本地原生窗口与 Ubuntu26.04 WSL2/WSLg。GitHub Actions 的 Windows2025、Ubuntu22.04/24.04 Xvfb 工作流已连接，但尚未提交/推送或执行远端 run；配置不能记作远端通过。
- 门槛是固定版本的 hosted CI 回归守卫，绝对阈值较宽，并同时使用同环境基线、失败率、原始身份和画质；不认证完整 C4 固定硬件 FPS、首次可用、尾延迟、吞吐或30分钟/8小时长稳 SLA。
- Native protocol v2 显式关闭 tiered compilation，核对 runtimeconfig、AppContext switch 与环境覆盖。结果仅适用这个测量协议，未认证应用默认 JIT 策略下的性能。旧 paired control 慢到快切换的具体原因未证实，后续通过不能证明 JIT 是唯一原因或产品性能回退已修复。
- 本地正式基线是同一冻结候选的独立源码副本与新进程，仅验证 paired control 和门禁接线；不能当作相对历史版本的提升。PR 工作流配置为独立 base SHA 产品与同一候选 native harness，实际远端 PR 比较仍待执行。
- Native request-to-capture 包含 UI 应用/布局、RAF调度等待与受控离屏绘制/PNG写出/像素复制；RAF callback 和 RenderTargetBitmap 不证明 compositor present、GPU完成、真实输入或物理桌面帧率。
- 全进程/实际托管、原生/GPU峰值和长期保留预算、共享索引/CWT/已发布路径、调用方对象与旧快照、复杂图族和域的 N 级工作及完整多图 UI 帧公平性仍待专项。
- 百万点 hit p95 历史0.7649→1.1235ms按用户决定后置；新10k hosted场景不能关闭该问题。
- A11Y-NATIVE-01 的 UIA/AT-SPI 原生选中项枚举按用户决定延期；AX06与四份历史 PixelProbe DLL 缺口保留。
- C4-03 Linux旧导出/动画暂停前置时序失败、C4-06 Windows旧viewport中间帧时序失败及libHarfBuzzSharp OS0x800700CE长路径问题保留；短路径和本轮通过不构成修复证明。
- 物理Windows/Linux、原生Wayland、ARM、国产Linux、多屏DPI、设备输入、实际backend/GPU和读屏器工作流仍待平台资格验证；原有C3图族/模板/标签/交互适配残余见对应历史报告。
- 远端setup-python之后，workflow的python与guard自动选择的python3是否指向同一解释器尚待实际runner核验；本轮本地两平台均显式指定解释器。
- CI job限50分钟，多个子进程分别可达600秒。极端累计超时、runner强杀/取消时，always上传无法保证执行完；逐阶段snapshot和流式日志只证明本地已写，不能当作远端已持久化。
- 本地同版本control未证明真实PR base的依赖、锁文件、SDK及API兼容；跨版本baseline构建或契约不兼容会fail closed，尚未取得真实PR base运行资格。
