# Markdown 测试交接（2026-10-15）

当前M7可执行软件范围已通过，见[本次M7收束记录](markdown-m7-completion-20261015.md)及[新M7执行记录](../artifacts/markdown-m7-completion/c63fab7542ba4b22b9a63d9cbb736f1b/m7-exit.json)。Windows43项与Linux私有Xvfb/X11新43项真实复制、读回及恢复通过，实际producer/Main/外层均退出0；原WSLg共享显示的安全Pending保持历史记录，两个环境分别记账。

Windows50ms、Linux51ms及身份、引用、脚注语义不变；本次增补按同一冻结源码和产物SHA复用五轮、544×2、416×2、包/API及85/134工具合同，不声称再次执行。远端原生CI由用户暂缓（DeferredByUser）；麒麟、实体Linux/原生Wayland、完整DPI/多屏、物理输入和真实辅助工具继续待验证。未发布、未改版本。

## 此前F832 / A50F测试交接（历史记录）

**本轮测试已结束：M6软件验收通过；M7可执行软件、包消费、回归、两平台smoke及不可变证据归档完成。** Windows原50ms与Linux显式51ms五轮独立守卫均通过，两平台当前产品smoke各43/43，政策守卫合同134/134。麒麟等缺设备及下文外部范围继续待验证；本轮未发布、未改版本。本文交接失败、未测范围与预算/bug边界，不再启动优化或压力测试。

本轮产品以 Windows F832/实际 Linux A50F 为准：[Windows d2 Verify](../artifacts/verification/d2f591e51482493c9b62b4f642af789e/results.json)与[Linux6451回归](../artifacts/linux-build/6451a248de3948f8baab4ee5f6ba0e0c/regression-summary.json)各907/907通过，软件源387项与Linux485输入逐项相同。并行 PDF 工作后来修改了[九份工作区源码](../artifacts/markdown-candidate-landings/664238a5d9754cf6af315cde811636d3/concurrent-workspace-changes-after-verify.json)；这些修改保留，但不在本轮冻结387份软件源及其测试结论内，不以当前整个工作区冒充 d2 来源。

134项合同通过后，[两个验证脚本已落地](../artifacts/markdown-budget-revision/landed-20261015-6f46fd660c214d09a14ab14a2a719bfe/source-transition.json)：默认50ms不变，显式政策51ms；没有产品/NativeHost程序集变更。冻结387份软件源仍保留d2原字节，策略工具由独立134项证据覆盖，不追记为原907项软件回归的一部分。

## 本轮结果与保留的失败

| 项目 | 已取得的结果 | 分类与下一步 |
| --- | --- | --- |
| Linux 长会话原50ms门槛 | [R1–R3](../artifacts/linux-probe/dfc8c8cdb68349dda6f37615eea4c319/derived-08a1d80ad2f34108a1468bb4097fa435/summary.json)全部true；[R4](../artifacts/linux-probe/dfc8c8cdb68349dda6f37615eea4c319/report-4.json)3669次ALL实际UI+同步布局P95 **50.6962ms>50ms**、producer=false，首次驱动退出1。R4长会话至帧组亦为false | **原门槛性能失败，不是已证实的语义bug。** 原raw/false保留；已按用户51ms政策独立复评通过，不能称原50ms五轮通过 |
| 显式51ms复评与第五轮 | [R5](../artifacts/linux-probe/dfc8c8cdb68349dda6f37615eea4c319/report-5.json)原50ms下ALL UI P9549.3257ms、producer通过；[监督](../artifacts/linux-probe/dfc8c8cdb68349dda6f37615eea4c319/f1fec2d3b6004ddda2d6a05abf62f37e/execution/supervisor-result.json)ProducerExitCode=0、同四DLL及前四raw/metadata未变。[显式51守卫](../artifacts/linux-probe/dfc8c8cdb68349dda6f37615eea4c319/guard-policy-51.json)实际退出0、五独立进程全部true，另存R4原producer=false/政策true | **五轮政策复评与134项合同均已通过。** [最终五轮摘要](../artifacts/linux-probe/dfc8c8cdb68349dda6f37615eea4c319/final-five-round-policy-51-summary.json)保留原判定及政策判定；[授权](../artifacts/markdown-budget-revision/user-authorized-policy-20261015.json)仅改长会话ALL实际及至帧组UI P95；原50结果不改写 |
| Windows 正式五轮 | F832 [独立守卫](../artifacts/benchmarks/markdown/windows-formal-f8324d-fcba9d4c612443c0bb682cd462532d5/guard.json)已在更严格原50ms下全部通过；[摘要](../artifacts/benchmarks/markdown/windows-formal-f8324d-fcba9d4c612443c0bb682cd462532d5/final-five-round-summary.json)ALL UI P95为24.1691–42.3460ms | **已通过，保留独立证据。** 不必因Linux政策调整重跑，也不转签不同平台DLL |
| 当前产品最终冻结与M7归档 | [08ba最终冻结](../artifacts/markdown-m7-preparation/08ba3007b2fd4d09bf73a98cc62d4763/final-freeze-manifest.json)已实际退出0，387软件源完全匹配；[软件归档](../artifacts/markdown-release-evidence/13c8c03113e64064ba54b7580cb620fb/manifest.json)实际Exit0、SoftwareEvidencePassed=true、RequiredSoftwareHashChainPassed=true；[政策独立附录](../artifacts/markdown-release-evidence/13c8c03113e64064ba54b7580cb620fb/policy-support-eb118238cf2b46b2817b815849dd62a7/policy-support-manifest.json)完整绑定387/B5、134合同及51ms授权，保留原false | **M6软件验收通过，M7软件证据归档完成。** Published=false、VersionChanged=false；缺设备不判通过。不使用并行PDF版Demo，也不迁用旧版本smoke |
| 当前Windows smoke | [新报告](../artifacts/markdown-m7-preparation/08ba3007b2fd4d09bf73a98cc62d4763/windows-smoke-ed4fe48322da4558b2b239c671918809/report.json)43/43 true、overall=true、错误null；[v2监督](../artifacts/markdown-m7-preparation/08ba3007b2fd4d09bf73a98cc62d4763/windows-smoke-ed4fe48322da4558b2b239c671918809/supervisor-result.json)Exit0/noTimeout、stdout/stderr完整，真实回环HTTP4请求，剪贴板真实复制并恢复本轮原空状态；Root已目视PNG | **当前组合通过。** 旧37项中断与Socket10013失败保留为历史，见工具/环境表 |
| 当前Linux smoke | [新A50F证据汇总](../artifacts/platform/linux-markdown-smoke-a50f-00ca24d5178d454eb29e563d8887c157/evidence-summary.json)为43/43 true、Exit0、error=null；实际6451 SDK产品与运行副本SHA相同，真实剪贴板复制/执行/原文本恢复均true，PNG已目视 | **当前WSLg/X11组合通过，当前剪贴板不再Pending。** 其他设备/bridge、物理输入、辅助工具及DPI不由此推定 |

R4成本主要在**同步布局**：Layout P95 **48.9090ms**，UI应用P95 **2.8431ms**；215条UI+布局>50ms提交的合计墙钟中，Layout占93.5027%，其中150条位于文档UTF16长度25%–75%，并非只在最终尾部。后台Parse P95258.1182ms另计，不属于UI50/51ms预算；原生raw没有Measure/Arrange内部探针，不能进一步确认某个布局函数或GC是唯一原因。各阶段P95不能相加。[分项、长度分段与版本关联](markdown-performance.md)、[逐提交CSV](../artifacts/linux-probe/dfc8c8cdb68349dda6f37615eea4c319/derived-08a1d80ad2f34108a1468bb4097fa435/all-actual-commits-joined.csv)

以下离线分项仅统计此前四轮：每轮9200条接收记录与全部实际提交按会话/SourceVersion/长度关联，无缺失、重键、UTF16/UTF8错配；Rendered阶段数值精确0差。短流至帧P95130.3739–134.5431ms，短Complete最大100.0599ms，长Complete最大740.2466ms；资源100次替换/卸载后必需计数全0、队列界限无违例。**长流batch至帧约550ms是诊断值，其固定延迟预算未适用，不能当作短流150ms失败。**

这里的51ms只对应ALL实际提交及长会话至帧组的同一UI P95。R4最后长Complete组的50.3239ms是UI诊断字段，该组不另设UI门槛；其总完成耗时P95597.0674ms、最大740.2466ms按1500ms验收，不能因UI诊断>50另造一个失败。

## 已确认且已修的 bug

| 问题 | 失败与修复证据 | 目前边界 |
| --- | --- | --- |
| 产品：字体属性跨Create缓存旧face | 同FontManager公开替换字体集合后，旧EDB5在[同fixture前测](../artifacts/markdown-combined-candidate/c3dfbd83f87e4676b546f5daa0feb53e/font-before.xml)4项中3失败；[修复后](../artifacts/markdown-combined-candidate/c3dfbd83f87e4676b546f5daa0feb53e/font-after-attempt-1.xml)4/4通过，[构建/差分记录](../artifacts/markdown-combined-candidate/c3dfbd83f87e4676b546f5daa0feb53e/build-and-font-differential-result.json)保留两版本及同测试DLL来源 | 已改单次Create内共享；fresh Measure响应新字体，Arrange保留原生已测inline旧face。不能用放宽性能预算解决字体、命中或选区语义 |
| 产品：硬换行inline在Wrap/无限高度Measure不返回（历史） | [旧版本同测试DLL前测](../artifacts/markdown-hard-break-regression/729e01e7ea0e47fb8ec2701eb3167494/before/result.json)超时，[修复后](../artifacts/markdown-hard-break-regression/729e01e7ea0e47fb8ec2701eb3167494/after/result.json)5/5通过，保留LF/CRLF投影与原生几何 | 已含后续完整回归；不是“等得更久就算通过”的预算问题 |

旧EDB5 Windows [正式首轮52.5111ms失败](../artifacts/benchmarks/markdown/windows-formal-edb5-4603a6b51c1b4d38a245779e14362583/native-round-1.json)与字体bug属于旧产物历史，不能被新F832五轮通过回写成成功，也不需要继续验收旧DLL。

## 测试工具和基础设施失败

| 项目 | 证据与处理 | 是否产品bug |
| --- | --- | --- |
| 守卫时间量化错误 | Linux纳秒Stopwatch频率下，raw80.617ms与旧守卫80.617018ms不一致；[原失败guard](../artifacts/linux-probe/a24551de85354cdb8b6fdcd244dcb5f3/guard.json)保留。[实际BCL逐条对照](../artifacts/markdown-time-precision/a3d4df624c3b49fa8c28ed7e0310534f/comparison.json)25588条0差，按TimeSpan100ns截断后[109项合同](../artifacts/guard-tests/markdown-e8e4e5e0715f4988800f8b7207ef4eb8/results.json)通过；Same容差未放宽 | **测试工具bug，已修。** 不改变任何产品计时或预算 |
| 50/51政策守卫合同与工具落地 | [134项实际结果](../artifacts/markdown-budget-revision/guard-contract-20261015-715d222931f642219117de5749ec413f/results.json)全部通过（109项默认兼容+25项政策修订）；[执行绑定](../artifacts/markdown-budget-revision/guard-contract-20261015-715d222931f642219117de5749ec413f/completed-execution-binding.json)记录session67404实际Exit0、预冻结与后SHA一致，守卫B5160B63…/测试FECE2C2E…。[两文件落地记录](../artifacts/markdown-budget-revision/landed-20261015-6f46fd660c214d09a14ab14a2a719bfe/source-transition.json)保留旧文件及前后SHA | **工具验证已完成。** 134是合成拒绝/边界合同，不是134次原生渲染或对907的追加运行 |
| 字体测试初稿编译失败 | [c3df首次构建](../artifacts/markdown-combined-candidate/c3dfbd83f87e4676b546f5daa0feb53e/build-attempt-1-result.json)退出1：测试中局部变量重名CS0136、对EmbeddedFontCollection直接调用Dispose导致CS1061；失败源码/日志保留，cached旧测试DLL未执行。修正测试代码后才执行前后4项差分 | **测试代码构建错误，非产品功能失败。** 不把这次未运行计成4项运行失败 |
| 私有隔离配置复制不全 | [a102 locked restore原失败](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/restore-failure-missing-tests-props.log)为NU1004，隔离副本缺父级测试配置导致依赖集合不完整；补齐配置后[重新locked restore](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/restore-2.log)成功 | **实验目录配置错误，非产品bug。** 没有靠关闭锁定模式处理 |
| 属性缓存私有初稿编译失败 | [CS0117记录](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/font-property-compile-failure-1.json)：初稿误用框架不可访问的LineSpacing；RuntimeExecuted=false、CachedDllUsed=false，后改为非零行距走原生base | **开发试稿API使用错误，非最终产品运行失败。** 不冒用旧DLL声称新代码测过 |
| 样式服务差分夹具选错位置 | [初稿2项中1失败](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/styled-after/result.json)：全文IndexOf取到首个重复emoji，而非目标seed代码块。仅修正夹具为seedText.TextStart+seed.Code.IndexOf；修正后的同一测试DLL在[旧库](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/styled-unique-before/result.json)/[新库](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/styled-unique-after/result.json)均2/2 | **测试夹具定位错误，非产品回归。** 未放宽选区断言，原失败保留 |
| 私有交接摘要字段适配错误 | [schema修正记录](../artifacts/markdown-m7-preparation/7a0be9ee03f44825a7d43bbc3ecfdb23/SCHEMA-FIX-AND-FREEZE-PLAN.md)：helper原以为Linux摘要是数组，实际是含projects的对象；首次拒绝后按真实字段修正，仍严格核11项目/907/零失败 | **交接工具错误，非产品bug。** 不修改原Linux证明或放宽数量 |
| NU1900审计源TLS失败 | [Windows日志](../artifacts/verification-font-index-final-20261015.log)、[Linux a747 restore](../artifacts/linux-build/a747737d78cf4e2b90a8b45bbf494e7c/restore.log)及原失败结果保留；没有关闭审计或放宽warning-as-error | **基础设施失败。** 官方连接恢复后d2/6451已通过；未运行的测试不算测试失败或通过 |
| NU1301签名端点TLS失败 | [1520原Verify](../artifacts/verification/1520d3370e374203bcd806531d7f852c/results.json)虽907项测试通过，隔离consumer还原失败，overall仍false；[原日志](../artifacts/verification-font-index-recovered-20261015.log)保留。恢复官方签名元数据后d2及76/78消费者通过 | **基础设施失败。** 不将1520追记为完整Verify通过 |
| 驱动/私有wrapper文字与元数据 | Linux正式raw是真100/10，驱动generic“pilot failed”不把它降为pilot；私有c3df wrapper写Warmups3但当前host不识别该扩展参数，raw实际1，原记录保留 | **取证工具边界。** 按真实raw登记；私有10样本探索从未作为正式五轮 |
| 当前Windows首次smoke权限失败 | [原报告](../artifacts/markdown-m7-preparation/08ba3007b2fd4d09bf73a98cc62d4763/windows-smoke-4b704e00b26841ccaf387e3f2ec74072/report.json)已登记37项均true（含当时真实复制及原文本恢复），但127.0.0.1默认图片HTTP遇Socket10013拒绝，后续检查/截图未完成、overall=false；恢复权限scope后的新运行才43/43通过 | **环境权限失败，已重跑解决；非产品语义bug。** 不把旧37项改写成完整smoke通过 |
| Windows smoke监督退出竞态 | 旧运行在120秒期限处理时GetProcessById(47340)遇进程已退出竞态，未写supervisor结果；[清理记录](../artifacts/markdown-m7-preparation/08ba3007b2fd4d09bf73a98cc62d4763/windows-smoke-4b704e00b26841ccaf387e3f2ec74072/wrapper-cleanup.json)确认原Demo已退出、其退出码未知（null）。owner Ctrl+C清理的是wrapper会话97312，wrapper Exit1不等于producer Exit1。v2保留Process句柄及流读取5秒期限，新运行Exit0/noTimeout、流完整 | **监督工具bug，已修并实跑。** 保留原失败与清理边界，不伪造旧Demo退出码 |

阶段实验还保留a102视口差分[旧库13项中2失败](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/before/result.json)、[首版候选13项中1失败](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/after/result.json)及[中间锚点候选同样1失败](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/anchor-after/result.json)；其中创建数量优化目标与TextFor辅助定位异常需按原测试源码SHA区分，不能统一称为当前产品bug。后续[177项通过记录](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/full-transition-after/result.json)与最终907各自另存，不回写早期实验，也不把不同测试源码/版本拼成同fixture修复证明。

## 尚未验证，交给后续环境/设备验收

| 范围 | 当前证据缺口 | 可执行的后续动作 |
| --- | --- | --- |
| 麒麟、实体Linux X11、原生Wayland | 只有可用Ubuntu/WSL2/WSLg/X11环境；不等于这些组合已通过 | 在对应设备记录发行版/显示后端/字体/缩放，运行相同已冻结产品的构建、smoke及适用性能门槛 |
| Windows/Linux多DPI、中文/Emoji/RTL完整视觉组合 | 缩放1及固定几何用例不能覆盖125%/150%/200%及全部字体组合 | 按组合检查布局、选择命中、复制和阅读锚点；保持真实截图与输入记录 |
| 物理鼠标/键盘、真实读屏和桌面显示 | 程序路由输入、Avalonia图像及角色断言不等于物理输入、读屏或scan-out | 使用真实设备/辅助工具验收现有只读角色、链接及选择能力 |
| 其他Linux桌面/clipboard bridge组合 | 当前A50F/WSLg真实复制及原文本恢复已通过；麒麟、原生Wayland或其他bridge尚无相同证据 | 对具体设备/bridge继续验收并保留原状态，不把本轮通过外推全部桌面 |
| 公网HTTPS/长期存活与峰值内存/低端设备 | 回环HTTP、固定100次资源循环和当前机器不能覆盖所有外部服务、长周期或设备档位 | 在明确目标环境另定实测计划；本轮不额外启动压力测试 |
| 商业官方参考控件对照 | 尚无同机行为/视觉/性能对照 | 需对应版本与可运行环境后再登记，不宣称官方兼容 |

两平台新PNG均为实际可见原生Window的Avalonia RenderTargetBitmap，不是桌面截图或scan-out测量。完整平台范围见[平台记录](markdown-platform-acceptance.md)与[公共契约](markdown-contracts.md)。原生UIA TextPattern、编辑/写回、完整HTML/数学/Mermaid/symbol不属于当前已支持承诺，不能把“没有实现”伪装为“测过”。

## 预算可以怎样调整

| 项目 | 建议 | 生效状态 |
| --- | --- | --- |
| 长会话ALL实际提交及至帧组UI+同步布局P95 | **50→51ms**只增加1ms/2%，R4两口径均50.6962ms，五轮已按明确政策独立复评；原50失败继续保留。Complete组不单独增加UI门槛 | 用户已授权、五轮政策守卫及134项合同均通过；工具已落地，默认50不变、显式51 |
| 短流150ms、短Complete500ms、长Complete1500ms | 当前均已通过，**没有再放宽的必要** | 不变 |
| 未来低端设备分档 | 可在真实目标设备测试后提出独立档位，明确硬件、后端、字体、缩放及测量方法 | 仅未来建议，未生效，不替缺设备判通过 |
| 重复节点身份、引用/脚注、选择/命中、字体热更新、旧Reader/Lease归零与队列有界 | **不能预算化或放宽语义。** 不通过时应按bug定位；不能少测次数、跳过失败样本或忽略旧资源 | 原断言与数量保持 |

## 本轮完成记录

| 项目 | 完成结果 |
| --- | --- |
| 第五独立进程raw路径、实际退出与同四DLL核对 | 已完成：R5原50下ALL UI P9549.3257ms、ProducerExitCode=0；监督确认同四DLL及前四raw/metadata不变 |
| 134项守卫合同实际结果、guard源码/测试源码SHA与默认50兼容/51边界证据 | 已完成：134/134、109默认兼容+25政策修订，session67404实际Exit0；B516/FECE源预冻结与后SHA一致，根目录两工具文件已落地 |
| 显式51策略五轮独立守卫、原producer=false与policy verdict分别登记 | 已完成：guard-policy-51五轮全部true；R4原producer=false/50.6962保留，51政策为true |
| 当前产品smoke、冻结与M6/M7软件退出 | 两平台当前smoke各43/43、907项回归、76/78项隔离包消费、最终冻结及不可变归档均完成；M6软件验收通过，M7可执行软件与证据归档完成。设备/外部范围仍待验证，未发布、未改版本 |
