# Markdown 实施状态

当前M7可执行软件范围已通过，见[本次M7收束记录](markdown-m7-completion-20261015.md)及[新M7执行记录](../artifacts/markdown-m7-completion/c63fab7542ba4b22b9a63d9cbb736f1b/m7-exit.json)。Windows43项与Linux私有Xvfb/X11新43项真实复制、读回及恢复通过，实际producer/Main/外层均退出0；原WSLg共享显示的安全Pending保持历史记录，两个环境分别记账。

Windows50ms、Linux51ms及身份、引用、脚注语义不变；本次增补按同一冻结源码和产物SHA复用五轮、544×2、416×2、包/API及85/134工具合同，不声称再次执行。远端原生CI由用户暂缓（DeferredByUser）；麒麟、实体Linux/原生Wayland、完整DPI/多屏、物理输入和真实辅助工具继续待验证。未发布、未改版本。

## 此前冻结候选与阶段记录

以下保留当时结论及原始来源，当前结果以上方revision2记录为准。

记录日期：2026-10-15。本轮M6软件与已测原生组合验收通过；M7可执行的软件验收、包消费、已有控件回归、当前两平台smoke和证据归档已完成。整体平台设备矩阵仍有待验证项，M7不标记所有平台通过；Published=false、VersionChanged=false，版本未发布。

当前冻结F832 Windows[d2 Verify](../artifacts/verification/d2f591e51482493c9b62b4f642af789e/results.json)与A50F Linux[6451实际SDK回归](../artifacts/linux-build/6451a248de3948f8baab4ee5f6ba0e0c/regression-summary.json)各907/907、0失败/错误/跳过/未运行，387共同软件源逐条一致，无例外。七包、Markdown功能独用76/主题及TextMate78项、各18项内嵌AXAML及现有控件消费通过，实际公共API成员签名无差异。Windows[五独立进程守卫](../artifacts/benchmarks/markdown/windows-formal-f8324d-fcba9d4c612443c0bb682cd462532d5/guard.json)按原50ms通过；Linux[五独立进程守卫](../artifacts/linux-probe/dfc8c8cdb68349dda6f37615eea4c319/guard-policy-51.json)及[最终派生摘要](../artifacts/linux-probe/dfc8c8cdb68349dda6f37615eea4c319/final-five-round-policy-51-summary.json)按用户明示的长流UI51ms政策通过。Linux R4原50ms下ALL/Rendered UI P95=50.6962ms及原false完整保留，不改写为原50ms五轮通过。

[134项守卫合同](../artifacts/markdown-budget-revision/guard-contract-20261015-715d222931f642219117de5749ec413f/results.json)实际134/134、Exit0，含默认50的109项和显式51的25项；[经测试SHA落地](../artifacts/markdown-budget-revision/landed-20261015-6f46fd660c214d09a14ab14a2a719bfe/source-transition.json)只变更validation脚本，未改变产品或NativeHost四DLL。原907证明仍绑定原387/B5冻结输入；新政策合同有独立执行证明，不倒签到旧Verify。

Windows[本轮43项原生smoke](../artifacts/markdown-m7-preparation/08ba3007b2fd4d09bf73a98cc62d4763/windows-smoke-ed4fe48322da4558b2b239c671918809/supervisor-result.json)与Linux[本轮43项原生smoke](../artifacts/platform/linux-markdown-smoke-a50f-00ca24d5178d454eb29e563d8887c157/evidence-summary.json)均43/43、Exit0，真实文本剪贴板复制、读回及原文本恢复通过。M7[软件归档](../artifacts/markdown-release-evidence/13c8c03113e64064ba54b7580cb620fb/manifest.json)实际SoftwareEvidencePassed=true、RequiredSoftwareHashChainPassed=true；[51政策独立附录](../artifacts/markdown-release-evidence/13c8c03113e64064ba54b7580cb620fb/policy-support-eb118238cf2b46b2817b815849dd62a7/policy-support-manifest.json)完成，保留原50失败与新51结果，不修改原软件来源证明。实体Linux、原生Wayland、物理键鼠/触屏、系统外链、辅助工具、完整DPI、多屏和麒麟继续Pending。两平台当前smoke通过不代替这些设备验收。

以下保留各历史版本与开发过程当时的状态；本轮当前结论以本节和阶段表为准，旧产物的失败、待验与通过均不转签新版本。

此前冻结版本04856FD6已完成[Windows整仓Verify](../artifacts/verification/19b1d0d43b0e472aac15d0fdd2d4d48d/results.json)：870项、七包、全部隔离消费者通过，Markdown两模式76/78项且内嵌AXAML各18项通过，379文件源码前后核验一致。公共API实际快照已更新。[Windows原生正式五轮守卫](../artifacts/benchmarks/markdown/windows-formal-04856f-9414e79e2b684ba08992741f7fa0ab86/guard.json)现已实际通过，全部原预算不变。当前Windows原生40项功能/协议通过，原剪贴板无可读文本而安全保留，复制/恢复待验证。此前048对应源码[Linux SDK整仓回归](../artifacts/linux-build/5a681321a91349dda6bace76a80eb43d/regression-summary.json)已完成870/870，0失败/错误/跳过；[Linux正式首轮](../artifacts/linux-probe/97e74dcd972349b3b28b216f3b97d388/report-1.summary.json)已完整失败：1MiB两模式UI、短流延迟及全部长会话提交UI均超原预算；100处虚拟化和100次资源替换卸载通过；DA11/840历史与当前源码分开记录，所有旧失败保留，M6整体尚未退出。

此前冻结产品EDB5/7B29已完成Windows与实际Linux SDK各896项回归、包消费76/78、公开API签名零差异。Linux新[正式五份原始报告](../artifacts/linux-probe/a24551de85354cdb8b6fdcd244dcb5f3/source-snapshot.json)原生预算/语义均true：全部提交UI P95=43.7414/43.3091/45.0150/45.6744/42.5399ms；原独立[guard失败](../artifacts/linux-probe/a24551de85354cdb8b6fdcd244dcb5f3/guard.json)保留，原因是纳秒Stopwatch ticks直接换算与producer的100ns TimeSpan量化不一致。[实际Linux BCL逐条核验](../artifacts/markdown-time-precision/a3d4df624c3b49fa8c28ed7e0310534f/comparison.json)25,588条全部精确相等；守卫换算已按实际BCL修正，新增量化/篡改拒绝用例后109/109通过；[新独立守卫](../artifacts/linux-probe/a24551de85354cdb8b6fdcd244dcb5f3/guard-timespan.json)通过，五份raw哈希不变，不改预算或Same容差。该五轮只证明旧EDB5/7B29性能，后续字体语义修复需要新产物重新五轮；最终M7归档仍待完成。
后续复核发现EDB5/7B29属性缓存跨CreateTextLayout保留字形，无法响应同FontManager公开替换字体集合、或同EmbeddedFontCollection追加优先字体后的原生重排；当前候选因此不能作为M6/M7完整验收通过。新[Windows首轮](../artifacts/benchmarks/markdown/windows-formal-edb5-4603a6b51c1b4d38a245779e14362583/native-round-1.json)已完整收齐11组并实际退出1：2969全部实际提交UI P9552.5111ms超过50，短P95121.704ms与长Complete P951051.317ms通过，资源100次释放及队列有界通过；驱动自行停止后续轮，无五轮守卫。监督[记录](../artifacts/markdown-m7-preparation/98f7cbfafb9244baa11e1051c04df609/superseded-edb5-windows-result.json)OwnedControllerStopped=false，没有终止任何进程，其GetProcess式FirstRoundExitCode=null不替代原执行会话真实Exit1。正在准备不跨原生布局创建的文字属性复用、运行时字体更新差分与[私有Windows分项诊断](../artifacts/markdown-ui-profiles/f44b0bef1be64941ab425328c1c61028/preparation.json)，所有原预算、重复节点身份/引用/脚注规则保持原样。之前896软件通过、Linux性能五轮通过和smoke结果保留为该候选的分项证据，不代签尚未修正的字体语义。
字体单次布局共享及纯身份索引预建已通过[6642转移清单](../artifacts/markdown-candidate-landings/664238a5d9754cf6af315cde811636d3/source-transition.json)逐字节落入当前源码。私有冻结候选[整仓回归](../artifacts/markdown-combined-candidate/c3dfbd83f87e4676b546f5daa0feb53e/regressions/434946cbf7a24188822778aa78694259/results.json)907/907通过；[同测试DLL字体差分](../artifacts/markdown-combined-candidate/c3dfbd83f87e4676b546f5daa0feb53e/build-and-font-differential-result.json)旧EDB5为4项中3失败、新84A6为4/4通过。旧编译错误及原失败XML均保留。Windows非正式[全场景10样本探索](../artifacts/markdown-combined-candidate/c3dfbd83f87e4676b546f5daa0feb53e/windows-full-runs/d297509cae0f4d12b31886473e80eaab/report.json)全部通过，460次实际长提交UI P95=23.8619ms；它不替代正式五轮。当前完整Verify及新Linux隔离还原遭NuGet审计源HTTPS握手失败，已保留[Windows](../artifacts/verification/85ce8ecfa4fc4e93ae0620dd5aba37c6/results.json)与[Linux](../artifacts/linux-build/a747737d78cf4e2b90a8b45bbf494e7c/result.json)失败，未关闭审计、改变警告或性能预算；正在恢复官方元数据连接后继续新产物验收。

恢复官方元数据后，当前Windows F832/BB15/B57F 已完成[最终Verify](../artifacts/verification/d2f591e51482493c9b62b4f642af789e/results.json)：907/907、七包、所有隔离消费者通过，387软件源前后相同（B5DD8FED…）；Markdown功能独用76/78主题消费、各18项内嵌AXAML通过。[实际API](validation/markdown-public-api.txt)与EDB5仅三个DLL SHA行不同，公共成员签名零差异。实际Linux SDK [6451回归](../artifacts/linux-build/6451a248de3948f8baab4ee5f6ba0e0c/regression-summary.json)907/907，全部失败/错误/跳过/未运行0；[共同源码387项](../artifacts/linux-build/6451a248de3948f8baab4ee5f6ba0e0c/windows-source-comparison.json)逐条完全一致，无例外。网络恢复使用[5份官方HTTPS真实响应](../artifacts/nuget-http-recovery/55d9e265e4824c8491747988ea5a84bf/result.json)与签名信息，未禁用TLS/审计/签名校验；之前Verify1520的907/打包通过但消费签名下载失败仍保留。Windows新[正式五轮](../artifacts/benchmarks/markdown/windows-formal-f8324d-fcba9d4c612443c0bb682cd462532d5/source-snapshot.json)已按原50ms通过，Linux新五轮亦按显式51ms通过；本轮M6软件与已测原生范围通过，M7软件退出及归档完成，未测设备另列。

本轮[Windows五轮独立守卫](../artifacts/benchmarks/markdown/windows-formal-f8324d-fcba9d4c612443c0bb682cd462532d5/guard.json)已全部通过，[汇总](../artifacts/benchmarks/markdown/windows-formal-f8324d-fcba9d4c612443c0bb682cd462532d5/final-five-round-summary.json)核验各自原始报告、实际四DLL与387源。Linux A50F同四DLL的[dfc8五轮报告集](../artifacts/linux-probe/dfc8c8cdb68349dda6f37615eea4c319/source-snapshot.json)已完成并通过显式51ms独立守卫，原R4在50ms下失败仍保留。采样期间工作区另有[9份并行PDF改动](../artifacts/markdown-candidate-landings/664238a5d9754cf6af315cde811636d3/concurrent-workspace-changes-after-verify.json)，全部保留；当前本轮交付绑定与d2验收387文件完全相同的c3df冻结源码，不把旧清单转签当前PDF工作区。三包/API/消费证据已保存在[08ba小文件冻结清单](../artifacts/markdown-m7-preparation/08ba3007b2fd4d09bf73a98cc62d4763/small-freeze-manifest.json)，原版兼容Demo与正式F832产品的组合已完成当前43项smoke，并纳入本轮软件证据归档。

已读取完整开发计划、README、构建配置及现有控件的开发与验收约定。保留工作区全部既有未跟踪文件与用户改动；不自动提交或清理。

## 基线与分工

- 配置及执行核验：`dotnet --version` 实际输出 SDK 10.0.401；net10.0、C# 14、Avalonia 12.0.3、CommunityToolkit.Mvvm 8.4.2。运行时 .NET 10.0.12、Windows x64。
- 参考版本固定 Avalonia.Controls.Markdown 12.3.0。运行时不引用商业 Markdown/Documents/RichTextEditor 包。
- Markdig 固定 0.42.0，TextMateSharp/Grammars 2.0.4、Onigwrap 1.0.11；产品和测试锁文件已生成。依赖及许可见 markdown-dependencies.md。
- 模型/解析负责人：markdown_core；调度/会话：markdown_streaming；官方对照/依赖：markdown_alignment；公共控件、服务/模板、集成和验收：root。

## 阶段

| 阶段 | 状态 | 退出证据与缺口 |
| --- | --- | --- |
| M0 | 关键架构原型通过 | 基线、复制/字素、真实语义样式、复杂grid结构/宽表、流式不完整结构、图片锚点及原生Windows子集均有证据；见M0结果。UI预算与全平台验收仍是后续门槛 |
| M1 | 公共契约与代表性包消费通过；持续同步 | markdown-contracts.md明确单元素继承/样式/局部null、服务归属、转换器/renderer registry、SemanticKind、模板必需部件和支持级别；最新API隔离NuGet消费者76/78项通过，LF/CRLF内嵌AXAML各18项通过，实际程序集成员/默认值/init/模板快照已更新。后续实现修正同步新包与快照 |
| M2 | 基础原生呈现集成通过 | 常规原生呈现、主题、真实AXAML/MVVM Usage和Demo已Release构建并有Windows探针；复杂视觉与平台组合继续专项验收 |
| M3 | 扩展/服务自动化及Windows子集通过 | 单元素服务覆盖、自定义纯数据转换/原生呈现、原生语义保留、嵌套扩展失败降级与资源归属已专测；修rowspan尾空槽和脚注backlink空行。五类提示矢量图标/紧松列表/运行时局部嵌套样式已有通过证据 |
| M4 | 已公布支持级别的自动测试通过 | 新增Page/Shift/快捷键、越界拖选、运行时模板/卸载捕获、脚注实际坐标跳转、全文Value/逻辑peer/字素范围/代次/GC；真辅助工具待平台验证。原生UIA TextPattern明确不支持，C#范围不冒充其实现 |
| M5 | 自动化及Windows阅读保持子集通过 | 分块等价、持续输入前缀、旧结果拒绝、真实TwoWay/外部替换、隐藏/无模板完成、图片锚点/跟随；字体/宽度/自定义文本glyph锚点与排队期间用户导航优先已通过，原生压力预算继续执行 |
| M6 | 本轮软件与已测原生组合验收通过 | 两平台907回归、387共同冻结输入无例外一致；Windows F832原50ms及Linux A50F显式长流UI51ms各五独立进程/100样本与会话/10预热守卫通过，134项合同通过。Linux R4原50ms false与全部历史失败保留；其它设备组合不由此通过 |
| M7 | 可执行软件验收与证据归档完成，整体设备矩阵待验 | 七包、76/78消费、旧控件回归、API及当前两平台43smoke/文本剪贴板恢复通过，13c8软件归档与51政策附录实际完成。Published=false、VersionChanged=false；麒麟、实体Linux/原生Wayland、多DPI、物理输入与真辅助工具保留Pending，不标记整体平台全通过 |

## 当前风险

1. 运行时局部嵌套行内样式增删、全局语义样式及命名资源已验证；更广泛的宿主主题/字体/DPI组合仍需按平台执行。
2. grid复杂合并格视觉与超长单块仍需专项验证；原生TextPattern是明确的框架能力边界，支持级别见公共契约，不把C#范围或Value当其替代验收。
3. 五独立进程×每场景100样本的纯模型历史基线已采集；分层调查已降低转换和身份匹配分配，本轮两平台正式五轮已采集并通过各自明确的预算政策。原生各轮首屏/流式预算逐项记录，无同机官方测量。
4. 可用Linux已取得实际SDK回归、WSLg/X11五轮原生性能及43项smoke证据；麒麟、完整DPI、真实辅助工具和物理触屏保持待验证。

## 2026-10-14 持续验收

DA1108CC最终集中`Verify`已实际退出0：Core184/184、Streaming60/60、Headless159/159、Themes8/8、TextMate13/13，共Markdown424项；既有两控件416项同轮通过，总840项，0失败/错误/跳过。`artifacts/verification/7b338e894adb4d51a425a0c3b6134138/results.json`另记录七包、全部消费者、41项键盘守卫及98项未变Markdown合同证据的哈希核验复用；未包含原生性能。Markdown包功能独用58/58、主题+TextMate60/60，新三包SHA前缀50FCA779/070E8ED5/376490E7分别登记在消费者汇总。

同版本Windows原生43项true、退出0，真实复制及原剪贴板文本恢复通过；`artifacts/platform/windows-markdown-smoke-20261014-following-recovery.json`与PNG已核对，source.json记录Demo/三产品和报告哈希。实际Release公共API、默认值与资源快照由`artifacts/markdown-api-helper/214e9818405e42698daaa7bcd070e200`成功导出。原71E/1B65/ED各版证据保持原文件，不并为当前840结果。

同源码Linux8df809已实际退出0：SDK10.0.401 locked restore、全sln Release0警告/错误，用时3分31.17秒；11项目840/840，Headless159/159用时10.043秒，所有失败/错误/跳过/未运行0。`artifacts/linux-build/8df8090ad9b844a096f573b154f4a91b/result.json`、`regression-summary.json`及逐文件manifest保留；manifest SHA418323DE，Linux Core6C30CB60、ThemesE98394C6、TextMate7A226EDB、宿主29FD4C13分别记录。与Windows哈希不同是各平台真实编译结果，不拼合性能轮次；此回归不替代Linux原生性能或剪贴板证明。

自定义fence转换失败的原文恢复新增8例，隔离Core184全通过，并已含当前840整轮；`artifacts/markdown-fence-recovery/740420b0a3794bffb96199726d2125dc`保存前后诊断和XML。普通EOF、正文、CRLF、缩进和引用/列表恢复完整所属物理片段，不吞相邻节点；闭合fence不变。grid单元格来源跨行交错，context保留AST范围，info只取语言/参数，失败保留MDNODECONV并用其所属代码行投影，避免复制右列或截断内容。快照/诊断保留原AST span，context范围区别已公开。VSTest连接超时的0执行基础设施结果保留，不作为断言失败或通过；有效证据来自直接xUnit184与集中Verify。

71E042版本Windows整仓Verify已退出0：Markdown Core163/163、Streaming60/60、Headless148/148、Themes8/8、TextMate13/13，合计392项；既有两控件416项同轮通过，总808项，0失败/错误/跳过。证据为`artifacts/verification/d0c2ce2b3ea241aaa7a215c288dbf66e/results.json`及各项目XML。七包打包、全部隔离消费者及41项键盘守卫合同通过；98项Markdown合同使用哈希一致且测试脚本未更新的已通过记录，JSON明确登记复用路径。Markdown功能独用58/58、主题+TextMate60/60，新包SHA另记汇总。正式原生性能未包含在Verify内。1B652整轮805项（`7d37c1159f174f5c83944f91f1b21e18`）与ED745B整轮798项（`86c2bb4bb03d415698deea43da3faa8a`）的通过记录分别保留，不与新版本拼接。

整仓这次`Verify`测试与七包打包完成，Markdown原消费者图片语料放大导致末尾代码离屏，已通过调整固定测试图像宽高比修复，数量/继承/null/所有权断言未放宽。随后两模式新消费者分别58/58、60/60通过，summary记录包SHA。`Verify`该次整体退出1及测试XML/计数仍保留在`artifacts/verification/f2e955bd3ad84e5cb83db60ff3fbd843`，不能把独立修复复验改写成该历史整轮退出0。

进一步扩展审查修复了未套模板的`CreateDefault()`表格/代码逻辑内容漏检和同一根对象重复ElementPrepared，并补自定义TableRow呈现路径。共享行列尺寸的布局循环已改为一次协调自然尺寸测量，12个容器组合实例随Headless138项全跑通过，涵盖自定义整行、原生/自定义混合行、跨行跨列、文字包装及真实拖选。父表格的结构跨度不能被转换器metadata覆盖。

后续契约复查新增错误恢复用例：null/异常枚举高亮使代码视觉变空，注册资源Dispose异常中断失败降级或卸载。独立修复前6实例为5失败/1通过，修复后6/6通过，`artifacts/markdown-error-recovery-probe/6ece8cb7f2e3425b871a116f41c088eb`保留实际前后XML。框架排队任务污染UI接收上下文也有确定性前失败/后通过；内部发送不捕获生产者范围，接收在一次异步捕获的中性ExecutionContext中平衡执行，宿主后台服务调用仍保留自身调用范围。

Avalonia12.0.3的xUnit adapter入口丢失真实调用者范围，导致偶发idle TestContext清理错误。Markdown Headless/Theme测试改用公开HeadlessUnitTestSession及显式Dispatch<int>等待原完整测试体，保留真实取消令牌、所有数据/断言与UI清理异常；不是升级或修改框架。Themes旧夹具的一轮6通过/2清理错误仍保留在`e64a4fd99b2243a79b21d6f4dee69599`，XML计数命名冲突另有派生修正记录，原结果未覆盖。

最新同步视口修复源码的真实Linux SDK编译0警告/错误；全11项目808/808、Headless148通过，证据为`artifacts/linux-build/7cc589e63c24417881d1fe614f5da928/result.json`、各项目日志及源文件manifest，Linux Core为CDB73D8C、宿主99B9B5B6。前1B652源码805项及Headless145多进程/Themes8两进程的证据仍保留在`7b37264619ea4225bbdbd84fdde263bc/retest-final-themes-7d76ab4032f84ce6a0b037b2a823347d/result.json`。两个平台分别保留实际编译DLL哈希，不拼接跨平台或不同版本性能轮次。

服务切换保持现有节点及选区；显式局部null禁用，ClearValue恢复继承；回收重新应用持久规则；共享服务不被控件释放。旧请求真正返回前仍占执行槽，取消不能伪造并发归零。卸载时切断视觉节点与Owner引用，旧租约经代次/局部版本检查丢弃并释放。

模板修复把padding移至外层Border，解决底部40DIP不可滚达、离屏链接和脚注定位问题。新增输入测试同时覆盖卸载/模板替换结束拖动；完整Text替换清除独立链接焦点，配置/追加迁移并检查目标语义，旧hover节点引用被释放。

阅读保持基于首可见原生文字cluster和稳定glyph坐标，支持自定义CreateText直接或包装布局；只排队一个弱owner恢复动作。用户键盘/滚轮/滚动条/显式Offset更新使旧恢复失效，Ctrl+Home按完整caret矩形保留首行。URI解析覆盖root-relative、authority-relative、fragment、avares及Windowsdrive/UNC，已访问链接按解析后实际目标记录。

Windows71E042版本原生探针43项true、进程退出0，真实复制和剪贴板恢复通过：`artifacts/platform/windows-markdown-smoke-20261014-viewport.json`，对应PNG已目视检查，`.source.json`另记录Demo/三产品和报告哈希。前1B652的error-recovery报告保留。包括单元素服务、原生自定义节点、五类提示及等色图标栅格形状摘要；默认loader有15项实际file、host avares、回环HTTP解码、租约、大小限制和网络服务结束检查。公网HTTP/HTTPS另待验证。这是缩放1的应用原生探针，物理输入/辅助工具/多DPI仍待验证。

1B652/宿主51D018原生完整pilot已进入M6预算验收。发现10k块跳转后Visible=0、Realized=24，现已在Offset属性改变时同步SetViewport；新增三个一次布局测试修复前全部可见0、修复后可见10/9/9、实现12/13/11并通过≤visible+16，已含新808整轮。71E042/宿主237008新原生10k探针三个位置可见20/20/19、实现22/24/21，均通过；同一完整pilot仍因1MiB高亮3样本UI P95=61.262ms退出1，失败记录保留，不倒推旧报告通过。

71E042/宿主237008随后独立进程的四组阅读均完成10次预热、100个样本并退出0：100KiB纯文本总/UI P95为133.932/30.209ms、1MiB纯文本515.202/33.712ms、100KiB高亮99.097/22.272ms、1MiB高亮466.530/36.616ms。这是400个样本的一进程阅读证据，不替代11场景五轮完整验收。

该版本首次五轮入口在第一轮1MiB纯文本阶段超时，完整五轮未完成；`artifacts/benchmarks/markdown/windows-formal-71e042-bd1f0d0093064986aa15dfe134f54df8`保留退出1和报告。新增不可变UI快照明确记录上一实际Render时窗口Normal，等待请求及超时现场均为Minimized、WindowActive=false，reader和布局仍有效。Avalonia最小化时停止渲染，故该轮不满足可见原生窗口前提，不能当作预算通过或据此认定产品死锁。新宿主351DAF只在新窗口构造时设置CanMinimize=false和WindowState.Normal，原有ShowActivated=false保持；无样本重试、自动恢复、Topmost或加长等待。产品71E042未改变，五轮使用同一新宿主从头采集，当前入口为`artifacts/benchmarks/markdown/windows-formal-351daf-afea65b94b2f42be9686551ad0ca6926`，未完成前不标记正式预算通过。

351DAF第一轮已实际完成，`native-round-1.json`为11场景/全部通过true/错误null；`native-round-1.summary.json`保留紧凑派生摘要。首屏与三类长块各100样本、短/长流各100会话及10次预热。短流971覆盖批次P95119.071ms、Complete P95101.260ms；长流3321覆盖批次仅诊断、Complete P95933.256ms、全部实际UI提交P9540.4013ms。100个滚动位置均通过，100次替换卸载后旧Reader/lease、活动服务/订阅/旧回调均0，真实执行峰值4、解析/待处理峰值1。第二轮已自动进入；五报告独立守卫尚未执行，单轮不能代替平台五轮验收。

351DAF第二轮短流100会话的813批次可见延迟P95169.817ms，超过固定150ms门槛；Complete P95146.556ms通过。继续收齐该轮长会话、虚拟化与资源记录后，五轮脚本将按失败停止，不跳过该组或把首轮通过作为平台通过。根据真实parse/UI/layout、接收间隔与帧分项调查后再决定优化；后续产品或宿主变化须重新冻结哈希并完整采集五轮。

第二轮已完整退出1，第三至第五轮没有启动。长会话实际提交UI+布局P9570.4623ms、Complete P951759.3211ms/最大3210.782ms，亦超固定预算；100处虚拟化与100次替换卸载通过。两轮receipt独立复算无误，分项对照见同目录`streaming-round-comparison.json`。历史UpdateMeasured只包含ApplyDocument首次同步布局，Loaded跟随的后续布局属于原生帧延迟、未计入该UI字段；第一轮40.4013ms不能宣称包括跟随全过程。原值和失败均保留。

针对后台分配与重复尾部布局，新源码已集中Release构建0警告/错误，功能SHA为55C2756A、宿主D5762F5C；尚未完成该版本整仓Verify或原生预算，不复用71E042的平台通过。解析改为只取fence首行、直接追加StringSlice、仅在投影范围完全一致时共享不可变字符串；新增13项Core边界用例，隔离176/176通过，1,548份完整快照无非预期差异。未闭合fence的完整info修正单列为预期差异，原source span保留。固定混合语料的分配降低约4.4%–4.7%，模型计时未证明总体性能门槛通过。

新跟随提交在首次Measure直接定位新文档尾部，随实际高度校正；同一提交内滚到真实底部并计入全部相关同步布局，成功时不再排Loaded恢复。用户偏移/键盘重入使该scope立即失效并回到实际视口，finally清理临时订阅与委托。普通模型更新缺模板/隐藏及合法零高度尾节点明确LayoutPending，显式流式缺模板仍Error。11项新UI/metadata用例正做同夹具前后验证，初始两个setup未稳定及非法无文字映射zero-renderer的结果不作为产品通过/失败证明，修正夹具后继续全套验收。

后续精确源码trace确证默认块的布局循环：每次Measure都创建index32/id73，初始范围32–39，按真实边距裁剪后33–39并删除index32；下次Measure重复创建/删除，使Presenter持续无效。现在初始实现范围与最终裁剪使用同一TrimVisibleMargins规则，overscan和回收门槛不变。合法零高度尾节点还暴露自动校正到新真实底部时修订号增加的问题，scope内先区分布局产生的上/下底部校正，用户内部偏移与键盘既有修订仍优先；可见尾部只按最后节点ID的实际根控件确认，不借相邻块边距。

A174E083修复版本集中Headless构建0警告/错误；相同测试DLL在71E042运行11项为2通过/9失败，在A174E083为11/11，完整Headless159/159、0错误/失败/跳过。实际DLL哈希及三个XML见`artifacts/markdown-following-probe/6471905ac7644cd5bfb1372c86b3e21b/results.json`。两个旧版用例在追加前就触发布局循环，不独立证明追加时序缺陷；独立trace已确认其产品原因。早期非法zero-renderer和编译失败后缓存DLL输出仍排除。该新版本原生性能与整仓Verify尚未执行，M6保持进行中。

后续DA11/58DDD2B4新完整pilot实际退出0，目录`artifacts/benchmarks/markdown/windows-final-da1108-92fc6c27c39d4276b4373b99f8312a70`：11场景均通过，短流1会话10批P9599.645ms/Complete77.486ms；长流1会话37批Complete507.311ms、全部实际提交UI+同步跟随布局P9541.4105ms。各阅读/长块仅3样本，不能计正式验收。随后独立reading100/10四组全部完成并退出0；1MiB纯文本总/UI P95433.798/32.980ms、TextMate846.356/49.412ms，后者接近50ms门槛，不掩盖边界。

五独立进程完整正式采样已实际启动于`artifacts/benchmarks/markdown/windows-formal-da1108-c5c63aa9cd6c47e981b4651958152aad`，同目录快照固定DA11/58DDD2B4四程序集副本。每轮阅读/长块各100样本、短/长流各100会话且10次预热；全部记录均完成后才运行独立五报告守卫。失败整轮保留并停止后续轮次，不补挑通过样本，不与71E旧轮拼接；M6仍进行中。

DA11上述正式第一轮已完整收齐并退出1，第二至第五轮没有启动：短流100会话986批可见延迟P95106.5753ms/最大135.3554ms，Complete最大95.3298ms，通过；长流100会话2077批的全部实际提交UI+同步跟随布局P9587.2475ms，Complete P952175.3424ms/最大2612.5187ms，均失败。100处虚拟化与100次替换卸载通过，所有旧Reader/lease/服务活动/订阅/迟到回调归0。小summary与一次原始receipt/commit分项复算另存，覆盖/最早接收/延迟复算无误；后端最终parse P951164.9761ms，并观察到较早prefix在停止输入后仍实际呈现。commit没有parse起止ticks，不能将receipt代替parse开始，残差也不能直接称渲染成本。

按最新继续指令开展分层定位：私有后台探针`artifacts/markdown-parser-phase-profile/7b623c6dbe6b458e865290625d836847`完成warm2/sample10及106份完整DTO/跨快照身份关系差分，未预期差异0；1060条分项保留。1MiB92前缀整轮P50总17.508秒，AST7.093秒/累计线程分配3.343GB、快照转换8.169秒/3.062GB、身份匹配2.295秒/0.848GB；最终两次前缀的完整快照P50分别139.66/182.59/30.85ms。该探针仅插入私有计时，不是当前产品或原生预算验收。优先研究归一化纯内容复用，保留完整AST与matcher保守重复身份规则；引用/脚注、非默认pipeline和宿主转换器需保守失效，未放宽任何预算。

UI私有探针`artifacts/markdown-ui-profiles/0dd7b0443f754429a4df5b30948081da/runs/efb8a6ab4233405e986007593af3f04b`已完成3预热/10长会话，359次可见提交。near1MiB的44次提交中，选区/焦点迁移P95为19.0095ms、UI线程分配约8.46MB；Presenter.SetDocument为2.3315ms/1.47MB，完整跟随布局25.3982ms/2.59MB，BuildTops仅0.1302ms且无分配。布局期间完成的GC暂停累计差P95为20.019ms，不能据此认定是该阶段触发GC或直接从wall耗时相减。探针本身完成延迟P95818.875ms仅属探索，不覆盖正式100会话失败。

新源码开始实现默认parser的纯归一化前缀缓存：保留完整当前AST，严格追加、同parser弱来源、字段/结构/源范围/实际投影核对，旧末根块始终重新转换；引用/脚注/HTML/未知扩展和非默认配置保守失效。复用内容先生成全新候选块ID，再运行原matcher，避免重复数量变化时带入旧身份。身份索引改用快照弱缓存与预分配；空光标只迁移一次，删除回退改为单次遍历，保持公开手工旧重复ID的首preorder语义。新增四项迁移边界测试及缓存差分正在隔离构建验证，尚未记为通过。另新增独立包消费者内嵌AXAML的LF/CRLF两夹具各9项，明确XML归LF契约；待实际Verify，不提前更新58/60的已通过计数。

首份隔离缓存版`artifacts/markdown-normalization-reuse/9bf0c1c8ea844d85aa40b568b9e3bb19/core-cache-after.xml`为Core203/203、0失败/错误/跳过，含缓存15与迁移4项；后补的旧snapshot/source回收及嵌套grid2项尚待集中验证。最新私有phase`artifacts/markdown-normalization-phase-profile/3bf536fd85e8447c80908d977b16f359`保留两次CS8602编译失败，修复后0警告/错误并退出0，1060分项、106完整DTO/持续身份关系、125导航快照、750anchors/684selections均无差异。所有缓存判断、验证、fresh IDs和登记计入转换成本。92前缀总P50为17.508→11.649秒、转换8.169→3.274秒；原生常见737280→748588的最终快照365.562→213.908ms。但首次赋值累计分配137.777→211.803MB，密集745472→748588末次转换206.032ms/139.787MB表明复用失效，继续优化shape捕获与尾部失效，不能据此宣布缓存或原生预算全部通过。

旧DA11三包/API/两消费者/840 XML/四DLL已复制到不可变归档`artifacts/markdown-release-evidence/43c553bb86024ca3bc665123086ac784/manifest.json`，包内DLL、实际消费者DLL及API标记哈希链核验一致。原Verify尚无运行前后源码清单，因此明确HistoricalSourceUnproven、SoftwareEvidencePassed=false，不倒签当前缓存源码或M7发布通过。后续Verify已增加源码前后哈希一致性门槛，最终新版本将另存新GUID归档。

进一步修正shape临时扩容/复制及已知pipe尾占位后，隔离Core207/207通过；新7ec7999fd1754dccb24f7494d1dd82bc完整1060分项/106DTO、125导航快照750anchors/684selection无差异。初次1MiB累计分配158.497MB（较DA11+15.0%），92前缀总P5010.488秒/转换2.060秒/累计分配4.909GB，密集745472→748588最终转换43.417ms；短100KiB首次P50增加约12.2ms，成本及探索限制保留。完整AST、依赖字段/源范围核对、default-only、引用/脚注/未知节点全局失效继续保留；只有最后必重建root内已知PipeTableDelimiterInline及对应现有MDNODE诊断允许不阻断前文，尾部diagnostic重新生成、不缓存。

该源码290D8FF6的新整仓`artifacts/verification/0247dca7a9204c59a3fe26c71bacb068/results.json`locked restore/Release为0警告/错误，11项目865/865（Markdown449+既有416，含Core207/Themes10）通过，七包和旧控件消费者完成。但FunctionOnly Markdown消费`artifacts/consumer/1d147891eebf419885475600027fbdf0`已编译后持续占满一个CPU核，超过十分钟无结果；核对路径后仅停止该受控进程，整轮退出1，不能称Verify或76/78消费通过。`hang-evidence.json`保PID/时间/CPU/可执行文件SHA；dotnet-stack未产生有效栈，不能据此归因布局。原58和新增18检查的阻塞阶段正由私有有上限日志定位。失败后的377文件与启动source-manifest复核差异0，另存post-failure-source-reconciliation.json；结果中的sourcesUnchanged=false因最终门槛未运行，不表示已发现源码变更。正式五轮尚未启动，不复用DA11失败或私有探索结果冒充通过。

该消费阻塞已用私有阶段日志定位到新增InlineReader第二块的原生文字Measure，旧58项均返回；移除新增18项的诊断分支仅58/58通过，不计76验收。最小[Wrap/无限高度对照](../artifacts/markdown-consumer-diagnosis/585d9907d80843a7a4756f7be149d5a3/case-results.json)中TextBlock.Text含LF正常，原生Inline LineBreak和MarkdownSpan/Run含LF均在20秒外部诊断上限内不返回；NoWrap或有限可用高度不复现。有限高度可能提前停止行生成，不能据此确认全文正确。把内建硬换行换成LineBreak的私有[分支与DLL核验](../artifacts/markdown-consumer-diagnosis/918f072abe4f4ee89751668c0f5ae516/runtime-result.json)仍阻塞，未采纳该无效补丁。正验证公共ITextSource显式TextEndOfLine路径，保留原生样式属性、逻辑inline树和全局UTF16映射，不限制高度、行数或改变硬换行投影。当前修复仍待验证，未进入新的正式五轮。

硬换行最小适配现已落入产品：原生文字段中的LF改为长度1的TextEndOfLine，再交回原TextBlock排版；保留同一原生Properties对象、逻辑Span/Run树、所有布局设置和内部LineSpacing。仅当原生CRLF归LF恰与当前合法inline投影一致时归一化；无换行、未知run和已适配标记保持原路径。官方Release功能SHA为04856FD6；[同源同测试DLL差分](../artifacts/markdown-hard-break-regression/729e01e7ea0e47fb8ec2701eb3167494/after/result.json)五项为5/5、0失败/错误/跳过，原290D仅替换产品DLL的[before](../artifacts/markdown-hard-break-regression/729e01e7ea0e47fb8ec2701eb3167494/before/result.json)在30秒诊断上限内不返回，核对自有PID/路径后终止。测试使用相同5CF348F6源码、982235EE程序集，保全部行范围/自然高度、动态样式、Unicode全局命中、复制、custom LineBreak与同Reader四轮清空复用断言。

私有d495探针的原76消费者及7场景45检查另有实际退出0记录，不当作官方包消费。正式夹具初期原生TextBlock.Text="\nfirst"参考也会阻塞，阶段日志确认适配控件已Measure/Arrange返回；参考现以无LF各段的独立原生布局组合范围与行高，不限制可用高度。原生双击聚合引起的第二轮整词选择，通过公开平台双击距离移动真实Reader位置，保精确光标断言；测试编译的CS0104/CS1061及缓存旧DLL误启动均保留，03e0旧DLL输出已明确exclusion，不计验收。整仓新Verify正在运行，正式五轮仍待此软件门槛通过。

官方048版本新整仓[19b1记录](../artifacts/verification/19b1d0d43b0e472aac15d0fdd2d4d48d/results.json)已完整退出0：870/870（Markdown454+既有416）、七包、全部隔离消费者通过；FunctionOnly76、DefaultTheme78各含18项内嵌AXAML，外部60秒supervisor均约1秒内退出0、无timeout。379软件文件前后核验一致，manifest SHA C104EA4750B93BB43D245E2798D539B669C2662206F2B7727C6BD6BA7B91CF54，sourceComparisonCompleted=true。API由cfa345helper从实际功能048/主题9D4C5D/高亮69F2CB程序集导出；旧失败不倒签。

新Windows完整[ddf9探索](../artifacts/benchmarks/markdown/windows-cache-04856f-pilot-ddf9dfc6ddf6465ab2a75c14b23d5073/summary.json)退出0但仍仅3阅读样本、1短流/3长会话且1次预热；Complete P95471.5879ms、全部实际UI+同步布局28.2789ms，不计正式通过。随后[9414正式第一轮](../artifacts/benchmarks/markdown/windows-formal-04856f-9414e79e2b684ba08992741f7fa0ab86/native-round-1.summary.json)实际11组全部通过、错误null，阅读/长块各100、短/长流各100会话与10预热；短999批P9595.761ms/Complete47.6209ms，长4577批及4577实际提交，Complete P95510.0244ms/最大578.2785ms、全部实际UI32.564ms。100处虚拟化和100次替换卸载通过，旧Reader/lease/服务活动/订阅/迟到回调均0。此为第一轮完成时记录。其后五轮均已完整结束，[Windows五报告守卫](../artifacts/benchmarks/markdown/windows-formal-04856f-9414e79e2b684ba08992741f7fa0ab86/guard.json)通过；Linux正式轮仍有预算失败，整体M6继续。

旧pilot的1MiB纯文本3样本UI P95=50.8199ms略超50ms；同版本100样本/10预热的前三组通过，1MiB TextMate第四组14样本后UI Render等待30秒超时，整轮退出1并保报告。宿主现补UI请求/最后Render快照的超时诊断，预算与30秒阈值保持不变。旧pilot100KiB流式117.816ms/Complete54.192ms、1MiB长流Complete688.571ms/all-commit UI35.4367ms等不能当五轮正式通过。

原生性能报告保留失败值及实际DLL SHA：早期Windows 1MiB UI72.407ms、Linux108.324ms，流式Windows197.094ms/Linux527.518ms；减少代码区域过量实现和UI字素索引成本后第四Windows pilot 1MiB UI38.671ms/19.788ms、流式167.005ms。pilot只有3个阅读样本，不作为正式P95验收；正在执行100样本/10次预热的独立轮次，预算没有放宽。完整方法及证据入口由markdown-performance.md持续记录。

## 2026-10-13 历史验证记录

- `dotnet restore NT.AvaloniaControl.slnx --disable-parallel -m:1`：24项目成功；受沙盒网络限制的首次还原走授权路径，没有修改仓库漏洞检查设置。
- `dotnet build NT.AvaloniaControl.slnx -c Release --no-restore -m:1`：整解决方案成功，0警告/0错误。
- Markdown Core31/31、Streaming41/41、Headless35/35、Themes7/7、TextMate13/13，新增127项全部通过；0失败/跳过。
- 既有回归：TreeDataGrid Core127/Headless177/Themes24，VirtualKeyboard Core25/Headless52/Themes11，合计416项全部通过。
- `dotnet pack ... --no-build --no-restore -m:1 -o artifacts/packages`：三个Markdown及四个既有产品包成功；未发布到外部源。
- 最终 `scripts/Verify.ps1` 退出0：locked restore、Release零警告构建、543项xUnit、VirtualKeyboard性能守卫及41个守卫拒绝用例、三控件独立NuGet消费均通过。最后增强了旧高亮实际Run/纯文本断言后再跑Headless35/35通过。
- Markdown最终消费者：FunctionOnly15/15（`artifacts/consumer/056901906835425b8d2f76ffc717cba2`）、DefaultTheme+TextMate17/17（`artifacts/consumer/82981928578b434c91e54e8ca09e9df0`）；汇总及最终包SHA见 `artifacts/markdown-package-consumer-results.json`。
- Windows最终原生探针：`artifacts/platform/windows-markdown-smoke-latest.json` 18项true，真实桌面上下文进程退出0，剪贴板原状态恢复true；对应PNG已目视检查。沙盒退出/Clipboard失败与清理另见平台记录。
- 最终公共API：`scripts/Export-MarkdownPublicApi.ps1` 成功，从实际Release DLL/Headless主题生成 `docs/validation/markdown-public-api.txt`；该成员快照不替代完整C#源码兼容认证。

## 后续阶段退出门槛

单元素服务、自定义节点及已公布自动化支持级别已经通过当前自动化与Windows子集，继续长文/平台工作。剩余门槛为更广泛的复杂视觉组合、长单块固定预算、正式五轮原生首屏/持续流式/1MiB长会话、最终源码Linux稳定回归及最终包消费。真辅助工具、完整DPI和麒麟按设备证据保持待验证；原生UIA TextPattern能力边界持续公开。

### 原型修正

修正了表格列索引/脚注编号、代码末尾LF丢失、内部Text同步吞外部回写、卸载尾部保留、浅比较导致未变图片重载、旧锚点回流及ScrollChanged延迟导致阅读漂移。对应Core/Binding/ReadingAnchor用例实际通过。

最终复核还修正删除锚点回退时反复扫描当前整棵文档的最坏二次成本，改为一次身份索引供两个端点和邻居回退复用；新增2000旧叶删除/2000新叶/唯一前驱存活用例通过。该语义用例不是耗时预算验收，Reconcile/MigrateSelection仍需专项测量。


最新Linux实际7E7816/E7DD2A正式首轮[97e报告](../artifacts/linux-probe/97e74dcd972349b3b28b216f3b97d388/report-1.summary.json)已完整结束、退出1、错误null；11组全部收齐，每组100样本/会话、10预热。1MiB plain/TextMate UI P95=52.813/56.159ms，短625批最早未显示至绘制P95=177.652ms，全部3119长会话实际提交UI+同步布局P95=74.9159ms，分别超50/150/50ms原门槛。长100会话Complete P95=610.248ms通过，长批延迟579.336ms为诊断项；不能套短流150ms门槛。100处实际虚拟化与100次资源替换卸载全部通过，旧Reader/lease、服务活动、订阅和迟到回调均0。首轮失败停止第2–5轮，无五报告通过守卫。私有分项诊断继续，未改变预算、语义或正式产物。

048版本的软件证据已核验并复制到新[不可变归档](../artifacts/markdown-release-evidence/f7c55c3a385a49fbb3ab7da96dc064bc/manifest.json)：SoftwareEvidencePassed=true、RequiredSoftwareHashChainPassed=true，实际870 XML、76/78消费者、三包内DLL/实际程序集/API与379软件源前后清单完整相符；Windows五轮守卫为已声明通过的独立原生引用，当前Windows剪贴板失败保留、Linux原生守卫Pending。归档不改变版本、不发布，不代签Linux预算失败或缺设备项；归档时根README/docs来源与Verify379文件来源分别记录。

Linux97e正式失败后，私有分项[reading](../artifacts/markdown-ui-profiles/2b890c1109e04612801becd51570f7a5/runs/reading-20261014T131427Z-jEKHlY/analysis-window/summary.json)与[long](../artifacts/markdown-ui-profiles/2b890c1109e04612801becd51570f7a5/runs/streaming-long-20261014T131529Z-4sNRn5/analysis-window/summary.json)确认原生文本测量为主要UI成本：1MiB首屏每提交先创建17块、后丢10块仅留7，估计范围过大导致无效测量。a102私有候选按实高收敛必要范围，使用同overscan与原生Arrange，另以dirtyMinIndex精确复用未变高度前缀。未采用改变TextLayout.MaxHeight的Arrange跳过或共享原glyph缓冲区捷径。

私有a102对照中原始FC348夹具跨两次完成重排仍断言同一历史字素，及捕获/断言采用不同Y计算，均原样留存；修正为每次独立变化捕获当前字素、同原生HitTestTextPosition坐标且1px门槛不变后，同一56BA1E88测试DLL/DF0DBD源码在048为11/13，在AE365F为13/13：[before](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/transition-before/result.json)、[after](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/transition-after/result.json)。旧版严格冷code创建9/丢8，以及每次当前阅读字素丢失，修复后均通过。局部样式高亮2项通过，原selector误选重复段落导致保守身份迁移的失败保留；改选唯一seed代码内emoji后不改产品Matcher。[完整Headless](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/full-styled-after/result.json)179/179、0失败/错误/跳过。

私有Linux探索使用明确标记的Windows托管Core在Linux运行，不能代替真实LinuxSDK产物或正式五轮。AE365F[单独long探索](../artifacts/linux-probe/e2a993e2cc99401f9bf6aa4f7cc5197d/report-1.json)10会话/438实际提交UI P9535.3019ms，但[完整11组顺序](../artifacts/linux-probe/711121a804fa48ff81142036278443fe/report-1.json)10样本/会话仍短流167.728ms、长全部提交UI60.2002ms失败；所有失败保留，正在分项定位上下文差异，不以单组通过代替整体。官方源仍为870已验版本，候选尚未落入正式产物。

后续a102私有Matcher只在完整身份匹配成功后复用完全相同的旧不可变节点：source/text全部标量、Kind/Text、同Metadata引用、同Inline backing和逐项同matched child引用全部相同才命中；CloneFreshIds/NewId顺序与singleton/small/general匹配及duplicate计数原样。Core SHA123DC43E，[新7项同DLL对照](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/matcher-seven-after/result.json)为旧0485/7、新7/7；[完整Core](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/core-final-after/result.json)214/214、0失败/错误/跳过，Headless179也通过。新增fixture第一次标签误期望n造成213/214失败，已仅按既有^n契约修正测试；30秒小用例supervisor误用于旧207实际41秒的整套Core时超时，无XML/计数null，保留并排除，不改任何原生性能预算。

同候选[完整Linux探索](../artifacts/linux-probe/fab651b81ee642f98d8ce7f7da05354b/report-1.json)仍短流151.391ms、长全部实际提交UI59.0149ms失败。随后[同Linux SDK诊断四DLL的Full](../artifacts/markdown-ui-profiles/14f1a35cdb264b28b2f8573504e60801/runs/full-20261014T140101Z-wG1fuP/context-analysis/summary.json)351实际提交UI P9561.6408ms，[OnlyLong](../artifacts/markdown-ui-profiles/14f1a35cdb264b28b2f8573504e60801/runs/streaming-long-20261014T140432Z-vqkVwv/context-analysis/summary.json)455实际提交33.8828ms；两者refine成功提交分别344/445，分别取自各自完整原始报告，不拼轮。Full成功refine344/351，legacy仅7；原生单run小文本的测量分配约228KB对Only2.4KB，正在核真实长度、字体与整形路径，不将相同constraint/line/run数量直接当相同语料。[调用对比](../artifacts/markdown-ui-profiles/14f1a35cdb264b28b2f8573504e60801/text-layout-call-comparison.json)保留原分项。框架ArrayBuilder容量整段清零假设已由固定12.0.3源码反证，不据此修改框架或全局运行时。所有新探索均非正式五轮，M6未退出。

同一真实LinuxSDK诊断四DLL的0bbd完整元数据取证已结束：[Full](../artifacts/markdown-ui-profiles/0bbd58641a3c43499085150ebe9ff840/runs/full-20261014T143025Z-fSSch4/native.json) 345实际提交UI P95=59.142ms仍失败；OnlyLong446实际提交，单组通过不能代签正式整体。调用按实际提交时间窗与SourceVersion/Length关联，0不匹配；[114组完整输入对比](../artifacts/markdown-ui-profiles/0bbd58641a3c43499085150ebe9ff840/text-layout-metadata-comparison.json)确认差异集中于字重600：两字符标题字号26、width1150、相同实际长度及行源范围，Full每次Measure约228536B/2.2623ms，Only约2512B/0.0167ms，Arrange约115888B对2224B。字号/文本长度/布局约束相同，但原counter的FontFamily.Name只显示composite首名，仍需核完整FontFamily及真实resolved face；当前字体缓存优化尚未落地，所有预算与默认字体声明保持原样。
[真实字体取证a47a](../artifacts/markdown-ui-profiles/a47a2d3c817c4653a4a093232e467a80/runs/full-20261014T145055Z-85u8FC/font-cache-summary.json)512条全部requested key=600与实际face weight=700不匹配；composite完整请求与Noto CJK JP实际族名不符，公开登记方案未执行，不能改变字重或伪造alias。取证额外resolver调用影响耗时，明确非性能通过证据。改验证每Presenter有界原生properties复用；第一次private编译误用internal LineSpacing而失败，未运行cachedDLL，原失败保存。修正版仅LineSpacing==0走等参数原生构造，其他行距回原base，FontManager/文档/Owner与卸载边界清理，不改global字体配置。新增3项public TextBlock几何/逐UTF16命中/inline基线与live样式对照，旧123DC完整[before182/182](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/font-properties-before/result.json)、privateBC9F [after182/182](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/font-properties-after/result.json)；后续审计还在补cache miss复制公开style值，避免旧_textRuns在context变化后携带旧私有glyph缓存。尚无新的正式五轮通过结论。
局部属性缓存fresh-clone版本B0A9的[完整Linux探索028f](../artifacts/linux-probe/028f5d059c97437a9bb4d2fa8560b173/report-1.summary.json)11组全true、error null：10读取/短/长会话+1预热，短P95129.773ms、长402全部实际提交UI P9545.8342ms、Complete P95553.445ms；资源/虚拟化10次通过。它明确LinuxSDKProductCompiled=false、Formal=false，不能代签五轮。相同857960测试DLL在[before123DC](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/font-properties-same-dll-before/result.json)及[afterB0A9](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/font-properties-context-after/result.json)均完整182/182通过。六产品文件及四新增测试已通过[0581源码转移清单](../artifacts/markdown-candidate-landings/0581031e556d49e394cf377fbc92efb5/source-transition.json)落入工作区正式源码；仅清理private注释及HostFreeAtCreation改名RangeEligibleAtCreation。当前软件/包/平台证据仍对应旧048，正冻结新候选后重跑全部门槛，未复用旧五轮给新DLL。
新正式EDB5候选[Verify f2013](../artifacts/verification/f2013bf2f6764fe283856d0878ea1026/results.json)已退出0：实际896/896，Core214、Streaming60、Headless183、Themes10、TextMate13及既有416，失败/错误/跳过/未运行均0；385软件源启动/结束一致，manifest SHA3C38D269278E0B94019A8D08F07AC0DA7368B2822BC071E04752BBCD3CDC4140。七包和旧控件独立消费通过；新Markdown消费者76/78含各18内嵌AXAML通过，见[汇总](../artifacts/markdown-package-consumer-results.json)。新[实际API](validation/markdown-public-api.txt) SHA C8BF2413152E8263DD141CD9698F0D707647906C40C723EEE2181588235248C3，与048冻结API逐行对比仅三个程序集SHA变化，公开成员签名无差异。Linux [1fcc快照](../artifacts/linux-build/1fcc230d5ebf437c876a92ef96dd4900/prepared.json)483文件实际SDK编译/回归执行中，尚不记通过；新两平台正式五轮仍待执行，旧048五轮未转签EDB5。
