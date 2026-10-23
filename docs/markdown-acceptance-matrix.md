# Markdown 验收矩阵

## 当前结论（2026-10-15）

当前M7可执行软件范围已通过，见[本次M7收束记录](markdown-m7-completion-20261015.md)及[新M7执行记录](../artifacts/markdown-m7-completion/c63fab7542ba4b22b9a63d9cbb736f1b/m7-exit.json)。Windows43项与Linux私有Xvfb/X11新43项真实复制、读回及恢复通过，实际producer/Main/外层均退出0；原WSLg共享显示的安全Pending保持历史记录，两个环境分别记账。

Windows50ms、Linux51ms及身份、引用、脚注语义不变；本次增补按同一冻结源码和产物SHA复用五轮、544×2、416×2、包/API及85/134工具合同，不声称再次执行。远端原生CI由用户暂缓（DeferredByUser）；麒麟、实体Linux/原生Wayland、完整DPI/多屏、物理输入和真实辅助工具继续待验证。未发布、未改版本。

## 以下为历史阶段证据与验收矩阵

以下保留当时的“四轮”“51 待复评”“M7 准备”和旧 Linux 剪贴板 Pending 等记录，当前结论以上节为准；不同版本和环境的证据不合并为当前验收。

记录日期：2026-10-15。依据 [开发计划](markdown-development-plan.md) 中 MT01–MT24，登记已经取得的证据及仍缺的验收范围。官方参考控件未执行行为、视觉或同机性能对照；本库自动测试通过不代表官方兼容。线程、服务归属、扩展、模板与辅助工具的支持级别见 [公共契约](markdown-contracts.md)。

2026-10-15当前冻结软件证据已更新：[Windows完整Verify d2](../artifacts/verification/d2f591e51482493c9b62b4f642af789e/results.json)实际passed=true、**907/907**、387文件启动/结束相同，manifest SHA `B5DD8FEDBE203A7CBF84D2841CC674331B700E70B51373244A52B9E156AB3D69`；[实际Linux SDK6451](../artifacts/linux-build/6451a248de3948f8baab4ee5f6ba0e0c/result.json)11项目通过，[直接日志审计](../artifacts/linux-build/6451a248de3948f8baab4ee5f6ba0e0c/regression-summary.json)同为907/907、0 failed/errors/skipped/notRun。[最终软件/API比较](../artifacts/markdown-candidate-landings/664238a5d9754cf6af315cde811636d3/final-source-and-api-comparison.json)逐文件387全部相同、无例外，Linux完整输入485文件；API SHA `2EC61F47FDCD9B33C637967BF86902722343254D6B1C9A9CCC32D51953890D7D`、公开成员签名差异0。新[FunctionOnly76](../artifacts/consumer/c3d55208a91d405cb0223057d50c676c/result.json)/[Theme+TextMate78](../artifacts/consumer/af58e3a2f38b443c92c659e9e78bd346/result.json)各18项内嵌AXAML全部true。三包/API/消费证明已[独立冻结08ba](../artifacts/markdown-m7-preparation/08ba3007b2fd4d09bf73a98cc62d4763/small-freeze-manifest.json)，保持实际GUID与SHA，不使用私有84A6替代正式产品。

Windows正式F832版本的[五独立进程守卫](../artifacts/benchmarks/markdown/windows-formal-f8324d-fcba9d4c612443c0bb682cd462532d5/guard.json)实际全部通过，保留原**50ms**门槛及各100样本/100短长会话、10预热；[五轮派生摘要](../artifacts/benchmarks/markdown/windows-formal-f8324d-fcba9d4c612443c0bb682cd462532d5/final-five-round-summary.json)全部实际提交UI+同步布局P95为42.3460/25.9897/24.8804/24.6516/24.1691ms，长Complete P95为989.5465/598.5500/781.3140/523.0250/546.3653ms、五轮最大1486.1136ms。它是正式Windows该四DLL的通过证据，不转签不同Linux DLL。并行PDF工作在Verify后修改了[九份live源码](../artifacts/markdown-candidate-landings/664238a5d9754cf6af315cde811636d3/concurrent-workspace-changes-after-verify.json)，全部保留；d2证明对应其冻结版本，不宣称现在整个工作区仍等于d2。M7最终源码/Demo复制及smoke/归档仅[准备](../artifacts/markdown-m7-preparation/08ba3007b2fd4d09bf73a98cc62d4763/README.md)，未执行大复制，不使用并行PDF版Demo。

新Linux A50F/Host2905版本的[正式轮次来源](../artifacts/linux-probe/dfc8c8cdb68349dda6f37615eea4c319/source-snapshot.json)未完成原50ms五轮：前三份raw全部true，ALL实际UI P95为47.8740/47.3905/48.5603ms；[第四轮raw](../artifacts/linux-probe/dfc8c8cdb68349dda6f37615eea4c319/report-4.json)error=null但overall=false，3669实际提交P95 **50.6962ms超过50ms**，原检查“长会话提交UI预算”及长流至帧场景false。驱动实际退出1并停在第四轮；generic“pilot failed”文字不改变raw的正式100样本/100会话、10预热性质。原始四份raw与原判定不改写，[离线派生摘要](../artifacts/linux-probe/dfc8c8cdb68349dda6f37615eea4c319/derived-08a1d80ad2f34108a1468bb4097fa435/summary.json)及[逐提交关联CSV](../artifacts/linux-probe/dfc8c8cdb68349dda6f37615eea4c319/derived-08a1d80ad2f34108a1468bb4097fa435/all-actual-commits-joined.csv)保留各源版本、长度、Parse/UI应用/Layout和Rendered关联。四轮每轮9200接收记录与全部实际提交均无重键、缺失、UTF16/UTF8长度错配；Rendered阶段数值精确0差，资源100次替换后必需计数全0，队列无1/1/4/256违例，短/长Complete全部100项分别在500/1500ms内。

R4分项显示主要成本为同步Layout：UI应用P952.8431ms、Layout P9548.9090ms；215条>50ms提交的合计墙钟中Layout占93.5027%，150条位于最终UTF16长度25–75%，不能归结为只有末尾变慢。后台Parse P95258.1182ms另计，不计入UI预算；各阶段P95不能相加，原生raw没有Measure/Arrange内部探针，详见[四轮分项与长度分段](markdown-performance.md)。

最新用户明确授权仅将**长会话UI+同步布局P95的ALL实际提交及Rendered子集两口径，从50ms调整为51ms（+1ms，+2%）**；[原始用户授权策略](../artifacts/markdown-budget-revision/user-authorized-policy-20261015.json)及[修订记录](../artifacts/linux-probe/dfc8c8cdb68349dda6f37615eea4c319/derived-08a1d80ad2f34108a1468bb4097fa435/budget-amendment-record.json)保留原producer50判定与R4失败。独立守卫默认仍50，51必须显式选择；其余预算、100/10采样、重复节点身份/引用/脚注和全部语义断言不变。Windows五轮已在更严格50下通过，无需重写或重跑；Linux须补同四DLL的第五独立进程并由明示51策略独立重验完整五轮。现在只有四轮调查数据，不能据数值小于51提前登记新守卫或M6/M7通过。缺设备、剪贴板、物理输入、辅助工具、DPI和麒麟项目继续按既有范围Pending。

最新联合候选已由[6642落地清单](../artifacts/markdown-candidate-landings/664238a5d9754cf6af315cde811636d3/source-transition.json)将5个产品文件及3个测试文件逐字节转入当前源码，包含纯身份索引预建与单次CreateTextLayout内属性共享。[私有冻结整仓回归](../artifacts/markdown-combined-candidate/c3dfbd83f87e4676b546f5daa0feb53e/regressions/434946cbf7a24188822778aa78694259/results.json)11项目**907/907**、0 failed/errors/skipped/notRun：Core221、Streaming60、Headless187、Themes10、TextMate13共491，加既有416；它不含包消费、guard合同或正式原生性能。新增[身份预建7项](../tests/Markdown.Core.Tests/MarkdownIdentityPrewarmTests.cs)保留重复旧ID首次前序解析、重复当前ID的原有异常、零位置重叠最短范围与同长首项，并验证预建后的迁移/changes及GC；重复节点身份、引用和脚注语义的既有断言与预算均未放宽。

[同一测试DLL字体差分](../artifacts/markdown-combined-candidate/c3dfbd83f87e4676b546f5daa0feb53e/build-and-font-differential-result.json)使用公开同键替换字体集合：旧EDB5为4项中3失败，[原失败XML](../artifacts/markdown-combined-candidate/c3dfbd83f87e4676b546f5daa0feb53e/font-before.xml)保留；新私有84A6为[4/4通过](../artifacts/markdown-combined-candidate/c3dfbd83f87e4676b546f5daa0feb53e/font-after-attempt-1.xml)。修复后重新Measure使用新字体，Measure后直接Arrange保留原生inline旧face、重新创建默认属性；硬换行适配后记最终run列表身份，非零LineSpacing、派生/custom类型绕过新增属性合并，既有硬换行适配仍先执行。原编译失败及失败源码清单亦保留。此差分覆盖这些明确操作，不宣称全部自定义字体provider均已验证。

同候选[Windows Full探索](../artifacts/markdown-combined-candidate/c3dfbd83f87e4676b546f5daa0feb53e/windows-full-runs/d297509cae0f4d12b31886473e80eaab/report.json)为**一个非正式进程**，七个阅读/长块组各10样本，短/长流各10会话；11组全部true、error=null，[监督](../artifacts/markdown-combined-candidate/c3dfbd83f87e4676b546f5daa0feb53e/windows-full-runs/d297509cae0f4d12b31886473e80eaab/supervisor-result.json)实际Exit0/noTimeout。460次长流全部实际提交UI+同步布局P95为**23.8619ms**，短流至帧P95114.5508ms；资源替换/卸载10次后必需计数均0，队列峰值1/1/4/0。[来源](../artifacts/markdown-combined-candidate/c3dfbd83f87e4676b546f5daa0feb53e/windows-full-runs/d297509cae0f4d12b31886473e80eaab/source.json)固定485文件、manifest SHA `FD497D1B1777757A86F4CBC81C6C6908A5DBDD10BBB20C5804C2FF3072F1B81C`、私有Core `84A63529C4A8C0029D5DD74C397539603A36CED7F8F01270631A05CEF51C0F0B`及另外三DLL。原wrapper传入的diagnostic-warmup参数仅探针host识别，当前host忽略；本轮raw各组预热实际为1，按raw登记，保留原wrapper/监督，不将其Warmups=3元数据当观测。它不能代替100样本/100会话、10预热、五独立进程的正式结果，也不能将私有DLL签给落地后的官方构建。

此前[85ce完整Verify尝试](../artifacts/verification/85ce8ecfa4fc4e93ae0620dd5aba37c6/results.json)及[Linux a747隔离还原](../artifacts/linux-build/a747737d78cf4e2b90a8b45bbf494e7c/result.json)因NuGet审计源HTTPS/TLS握手失败而未通过，原失败保留；85ce测试未开始、testTotal=null。随后官方连接恢复，1520完成907测试但consumer签名下载失败，最终d2与实际Linux6451已取得上面记录的软件通过。审计与警告未关闭，不把早期失败记录改成通过。EDB5/7B29的896项、smoke及Linux五轮仍是旧产物历史，正式F832 Windows五轮及本轮Linux四轮另列；整体M6/M7继续，缺设备项Pending。

后续Windows EDB5[正式首轮原始报告][failed-edb5-windows-round]已收齐11组且实际退出1：短流最早未显示至帧P95121.704ms通过，长流Complete P951051.317ms在1500ms内，但**2969次全部实际提交UI+同步布局P9552.5111ms超过原50ms预算**，raw error=null、overall=false。100次资源替换/卸载后必需计数均0，队列峰值1/1/4/0；这些通过项不抵消UI失败。驱动自动停止后轮，[监督补充记录][failed-edb5-windows-supervision]OwnedControllerStopped=false，没有终止任何进程；其中FirstRoundExitCode=null只是GetProcess监督元数据，不替代实际原启动进程已经取得的Exit1。旧失败与完整原始数据保留。

后续Windows [Full/Only私有分项](../artifacts/markdown-ui-profiles/f44b0bef1be64941ab425328c1c61028/stage-comparison.json)各10长会话/3预热，同四个带探针DLL；454/464次实际提交均完整关联，UI P9530.4146/27.0446ms，小样本没有复现正式52.5111ms失败，不能覆盖正式结果。后台Parse P95232.9193/230.0275ms包含Markdig、DTO及后台Matcher，未分别计时；UI ConditionalReconcile仅分支，ApplyDocument的选区迁移/身份索引/链接迁移没有独立stage。原生Text Measure P951.2817/1.4154ms、Arrange0.6944/0.6689ms，递归CreateBlock8.8573/6.1327ms；具体元数据与计时边界见[性能分项](markdown-performance.md)，不能相加父子P95、扣除GC或据此宣布M6通过。

EDB5跨CreateTextLayout保留文字属性的字体语义缺口已由上面的同DLL差分确认，并在联合候选的固定用例中修复：同一个FontManager可经公开AddFontCollection替换，或让已有Embedded集合追加字体，manager/document引用相同不足以保证字体仍有效。新路径改为单次Create复用、fresh Measure列表检查及native Arrange列表保留；四项公开替换字体差分已通过，但新官方产物的完整Verify、消费/API及正式五轮尚未取得。EDB5原DLL的Linux五轮、896回归、包消费和Windows43项smoke保留为历史分项，不能签发修复版本M6/M7退出。

2026-10-15时间换算核验：Linux a245五原始报告各11组全部完成，但[原独立守卫][active-linux-old-failed-guard]五轮均拒绝，错误为`stream elapsed differs from raw samples.`。问题是守卫直接按频率换算毫秒，生产者使用Stopwatch.GetElapsedTime先转换为100ns的TimeSpan；不是会话排序、样本映射或预算变更。[实际Linux BCL独立对照][active-timespan-bcl]对五轮短/长batch和Complete共25,588条逐条比较，原始值与GetElapsedTime及相同截断公式均0差异。[109合同用例][active-timespan-tests]实际全部通过，含1GHz的18/99ns余量接受、10MHz原路径接受、四种时间值漂移100ns及倒退拒绝；Same容差和全部预算原样保留。新守卫SHA为`71094D15D88B888972590879207923FB9EE6C9EACEE7ED14A3B56FB623D97205`，[独立重验结果][active-linux-five-guard]五进程全部true；[输入不变记录][active-timespan-validation]确认五raw重验前后SHA不变，原false守卫未改写。

前次EDB5应用两处validation PowerShell修正后的[完整Verify][final-timespan-verification]已实际passed=true、896/896，11项目0 failed/errors/skipped/notRun，locked restore/Release零warning/error、七包及全部独立消费者完成。385文件[最终源清单][final-timespan-manifest]SHA为`A43CD74D3C354F7FB7A3B22A43627020946BAF05BFEE56D9F0669FE9B8047514`，启动/结束一致且比较完成；109合同证据e8按该轮结果记录复用。新[FunctionOnly76][active-function-consumer]/[Theme+TextMate78][active-theme-consumer]各18项内嵌AXAML全部true，两监督Exit0/noTimeout；产品四DLL和包hash与前次EDB5相同。[最终跨平台来源比较][final-compiled-source-proof]明确383相同+两个validation PS差异、CrossPlatformAll385Equal=false、AllCompileInputsUnchanged=true，不能用前次385全等记录代签当前来源。

前次7B29版本[Linux五轮派生摘要][final-linux-five-summary]已读取原始五报告并保留独立守卫为验收权威：每轮七个阅读/长块组100样本/10预热，短/长流各100会话/10预热；短流最早未显示至帧P95115.7539–126.5725ms，长流全部实际提交UI+同步布局P9542.5399–45.6744ms，长流Complete P95539.7656–595.3001ms、五轮每次Complete最大712.9927ms，均在原预算内。全部实际提交与Rendered批次分别保留（该五轮两者数量恰相同）；资源必需归零和1/1/4/256队列界限通过。[Windows候选五轮来源][final-windows-five-source]的正式首轮已完整失败，后轮停止，旧0485结果不转签；新不可变归档仍待收束，整体M6/M7未提前退出。

前次[Windows EDB5原生smoke][final-windows-smoke]为**43/43 true**、error=null，[监督][final-windows-smoke-supervisor]实际Exit0/noTimeout；包含真实文本剪贴板复制、读回及原文本恢复均true，[来源][final-windows-smoke-source]固定产品/宿主副本。前次[Linux7B29原生smoke][final-linux-smoke]为40项功能/协议true、error=null，但原剪贴板无可读文本而保留，Clipboard执行=false、overall=false、[退出记录][final-linux-smoke-result]为1，Linux真实复制/恢复保持Pending。两PNG已目视；程序路由输入、Avalonia窗口渲染图与回环HTTP不等于物理输入、桌面截图、公网HTTPS、辅助工具、多DPI或麒麟验收。

前次EDB5产品冻结构建的[完整Verify](../artifacts/verification/f2013bf2f6764fe283856d0878ea1026/results.json)已实际passed=true、退出0：Core214/214、Streaming60/60、Headless183/183、Themes10/10、TextMate13/13，Markdown480项，连同既有六项目416项共**896/896**，11项目0 failed/errors/skipped/notRun，七包与全部独立消费者完成。六产品及首四测试的[落地源文件转换记录](../artifacts/markdown-candidate-landings/0581031e556d49e394cf377fbc92efb5/source-transition.json)与最后新增的[缓存生命周期Fact](../tests/Markdown.Headless.Tests/TextPropertyCacheLifecycleTests.cs)分别保留；Verify的385文件[源清单](../artifacts/verification/f2013bf2f6764fe283856d0878ea1026/source-manifest.json)SHA256为`3C38D269278E0B94019A8D08F07AC0DA7368B2822BC071E04752BBCD3CDC4140`，启动/结束源码一致、比较已完成。未改变的98项guard合同证据按Verify记录复用，不声明重新运行，也不计896或原生预算。

前次EDB5软件轮的独立NuGet[FunctionOnly76项](../artifacts/consumer/cfb8502b20bb4ea0870c5add1e213816/result.json)/[主题+TextMate78项](../artifacts/consumer/f092fb9ab058410492b9f2509d6dc60b/result.json)全部true，各18项compiled AXAML LF/CRLF检查；[功能监督](../artifacts/consumer/cfb8502b20bb4ea0870c5add1e213816/supervisor-result.json)/[主题监督](../artifacts/consumer/f092fb9ab058410492b9f2509d6dc60b/supervisor-result.json)均Exit0、无timeout、error=null。当前[消费汇总](../artifacts/markdown-package-consumer-results.json)三包SHA前缀为`472C711B…`/`4067FEF3…`/`0A635330…`，版本仍0.2.0-preview.2；实际功能/主题/TextMate DLL为`EDB5A830376611A21DA035FCD8D55EF57997EBEBB8CB73305FA29AA7D021D87C`/`2BD47A8EDAE56907360A1595C8390916E923181F9CB9185CDAF486BDFECA1FE1`/`9CBE1284EE52ADCBC04C058CFFD6E1F20C09E42E2696E0AFCED50F1F0578019B`。最新[公共API快照](validation/markdown-public-api.txt)由[实际Release导出helper](../artifacts/markdown-api-helper/07031d79ad4d4042b466b4c82e24f6f0/Program.cs)生成，快照SHA256为`C8BF2413152E8263DD141CD9698F0D707647906C40C723EEE2181588235248C3`；与0485归档相比仅三处程序集SHA变化，公开成员签名无差异。

前次EDB5/7B29同源[真实Linux SDK完整结果][active-linux-build]已实际Exit0、全部11项目true：SDK10.0.401、locked restore、全solution Release构建3分06.30秒、0 warning/error；[直接测试日志汇总][active-linux-regression]为**896/896**，Errors/Failed/Skipped/NotRun均0，项目数量与Windows一致。[483文件Linux输入清单][active-linux-source]SHA256为`352546E1C8F380A6C66052ECCBDED1A9DF88C37226FF557EC09DAD31280EC4C2`；[脚本修正前跨平台软件/API比较][active-source-api-proof]记录当时385文件相同与公开签名无差异；后续仅两个validation PowerShell脚本修改，当前跨平台软件口径为383文件相同、两脚本差异，不能沿用旧385全等结论。产品与NativeHost的C#和四DLL未变，Linux896来自本次独立SDK实际执行；前次[385文件完整Verify][final-timespan-verification]已实际896通过，来源由[最终383+2比较][final-compiled-source-proof]补齐。

Linux功能/主题/TextMate/Host四DLL分别为`7B29D53342A03C888FE170A46F8BBDCA74F8A91C75420EBFFBA7F98E02E25048`/`CFB302DDC3290562B5038D027894B86179416345510CABE082060AE712349558`/`D30D3AE71EB754A8CF5F003BAA89C01BFE754909CC947939E7676710B0FC7D77`/`1ED252BA27771B8310BD74B21A5556E96B5BB89B31CEEE0161035AAB2EEBA9AD`，不复用Windows EDB5程序集。[新Linux正式五轮冻结来源][active-linux-formal-source]核对上述真实SDK四DLL，五原始报告已全部完成；原守卫因时间换算错误拒绝，经实际BCL核验和109合同用例后，[修正守卫的新结果][active-linux-five-guard]实际全部true、独立进程5/5。原始报告与失败守卫均保留，详见下段失败链；前次[Windows EDB5候选五轮][final-windows-five-source]首轮已完整失败，后续轮次停止，无候选五轮通过结论。环境为可用Ubuntu26.04 WSL2/WSLg、Avalonia.X11/XID，不能代替实体Linux、原生Wayland或麒麟。当前不可变软件归档与M6/M7整体退出仍待验证；旧0485的870、f7归档和Windows五轮、旧Linux97e失败均保留历史，不拼轮或转移版本。Linux剪贴板、物理输入、辅助工具、多DPI及缺设备项继续Pending。

此前0485冻结构建的 [完整Verify][current-final-verification] 已实际退出0、passed=true：Core **207/207**、Streaming **60/60**、Headless **164/164**、Themes **10/10**、TextMate **13/13**，Markdown **454项**，连同既有416项共 **870/870**，0 errors/failed/skipped/notRun；七包及全部独立消费者完成。来源清单SHA256为`C104EA4750B93BB43D245E2798D539B669C2662206F2B7727C6BD6BA7B91CF54`，Verify明确记录sourcesUnchangedDuringVerification=true、sourceComparisonCompleted=true。[当前Headless XML][current-headless-final] 与 [当前Themes XML][current-theme-final] 对应本轮，未包含原生性能预算。

此前0485新消费者 [FunctionOnly结果][current-function-consumer] **76/76**、[Theme+TextMate结果][current-theme-consumer] **78/78**，各含LF/CRLF两个compiled AXAML内嵌夹具的 **18项**，全部true；各 [功能监督记录][current-function-supervisor] / [主题监督记录][current-theme-supervisor] 均noTimeout、Exit0。此前0485的 [不可变消费汇总](../artifacts/markdown-release-evidence/f7c55c3a385a49fbb3ab7da96dc064bc/consumers/summary.json)，三包SHA前缀依次`10467E72…`、`6B15E789…`、`26101583…`；[0485不可变软件归档][current-software-archive]已核验包/DLL、API、76/78消费与870回归的完整必需hash链，Published=false、VersionChanged=false。旧[DA11消费归档][consumer-results]的58/60项继续按旧版本保留，不能当作新76/78证据，也不与870重复计数。

此前0485的 [API归档](../artifacts/markdown-release-evidence/f7c55c3a385a49fbb3ab7da96dc064bc/api/markdown-public-api.txt) 已由 [实际导出helper][current-api-export-helper] 从Release生成，功能/主题/TextMate SHA前缀分别为`04856FD6…`、`9D4C5D0A…`、`69F2CB47…`；[旧DA11 API归档][prior-da11-api]保持历史。本轮 [Windows原生报告][current-windows-native] 的40项功能/协议检查为true，但剪贴板未执行、整体false，来源见 [哈希记录][current-windows-native-source]；原剪贴板保留、真实复制/恢复待验证，不能记为43项通过。DA11历史Windows43及Linux840/原生40结果不迁为0485的平台通过；此前0485的Windows正式五轮预算已另通过；Linux最终五轮及设备项仍待验收。

此前0485/Host24CA的 [Windows正式五轮独立守卫][windows-final-five-guard] 已实际 **全部通过=true**、独立进程数5；[冻结来源][windows-final-five-source] 固定功能/主题/TextMate/Host四DLL为0485/9D4C/69F2/24CA，五轮各11场景均通过，保持原预算、每轮阅读/长块各100样本、短/长流各100会话及10次预热，不与旧失败轮次拼接。1MiB长会话五轮Complete P95为510.0244–548.9444ms、各轮最大值总体659.4792ms，全部实际提交UI+同步布局P95为31.5532–32.5640ms；固定首屏、长块选择/滚动、10,000块回收与资源稳定性门槛同由独立守卫通过。这是Windows平台性能通过，不改变7bee功能探针40项/ClipboardPending，也不证明物理扫描、读屏、DPI或麒麟。同源[真实Linux SDK结果][linux-048-build]已完成locked restore/Release0 warning/error，11项目[870/870回归][linux-048-regression]通过，四DLL与Windows分别登记；[477文件输入清单][linux-final-source-files]固定该副本来源。但该Linux输出的[正式首轮][linux-048-failed-native]收齐11组后失败：1MiB纯文本/TextMate UI P9552.8128/56.1590ms、短流最早未显示至帧P95177.6524ms、长流全部实际提交UI+同步布局P9574.9159ms均超原预算。第二至第五轮停止；整体M6尚未退出，M7包/API/软件归档材料继续收集。后续私有优化及分项通过不继承0485 Windows五轮，也不代替最终版本的两平台五轮。

后续私有a102已补范围收敛、dirty前缀、当前阅读锚点恢复、严格匹配后的不可变DTO复用及单Presenter文字属性复用。[Core214完整回归](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/core-final-after/result.json)、[Headless182完整回归](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/font-properties-context-after/result.json)均通过，包含重复节点、迟到引用/脚注、原生几何/命中及动态样式差分。[完整11组探索](../artifacts/linux-probe/028f5d059c97437a9bb4d2fa8560b173/report-1.json)也全部通过，短流P95129.773ms、长流402次全部实际提交UI P9545.8342ms；但[来源](../artifacts/linux-probe/028f5d059c97437a9bb4d2fa8560b173/source-snapshot.json)为私有B0A9DADF Windows IL、LinuxSDK编译=false，且10样本/10会话、1预热、formal=false。它们不计入0485的870、不继承旧Windows五轮，不提前宣布M6/M7退出；后续EDB5已完成896软件整轮与新包/API验证；真实Linux SDK与两平台各五轮仍须新证据。失败链与拒绝更改600字体别名的原因见[性能记录](markdown-performance.md)。

此前DA11 Release构建的 [完整Verify][final-verification] 已实际退出0：Core **184/184**、Streaming **60/60**、Headless **159/159**、Themes **8/8**、TextMate **13/13**，Markdown **424项**；连同既有控件416项，11项目共 **840/840**，0 errors/failed/skipped/notRun。七包及全部消费者完成，未跳过消费验证。该DA11 Headless结果见 [159项XML][current-headless]。同构建Windows原生探针独立记录 **43项true、退出0**，含file/avares/回环HTTP实际PNG解码、限额及租约释放、真实文本剪贴板复制/读回/恢复，PNG已目视通过；物理输入/辅助工具/DPI/麒麟仍未验证。[最终Markdown消费者][consumer-results] 为功能独用58/58、主题+TextMate60/60，共118项包检查；三包SHA256前缀依次为`50FCA779B15D…`、`070E8ED561B02…`、`376490E7D443…`。原生43项和消费者118项分别记账，不加入840；本次Verify未包含原生性能预算。

此前71E0构建 [808/808记录][viewport-verification]（Core163/Headless148，Markdown392）、ED构建 [798/798记录][ed-verification]、1B65构建 [805/805记录][prior-verification] 与2026-10-13的543项记录保留为历史，不与当前840项相加。71E0原生43项的 [原报告][viewport-native-results] / [来源][viewport-native-source] 及其消费者包SHA前缀`0FAD8AD698F5…`、`96FBF610B07F…`、`D0751E53AC11…`保留为该阶段记录；1B65的Headless两独立进程145/145，见 [第一轮][headless-round-1]、[第二轮][headless-round-2]，其 [原生来源][prior-native-source] 亦单独保留。最终Verify复用未改变的guard98契约证据，复用路径与状态列在结果中；不将其声明为本轮重新执行。

后续290D版本的 [失败Verify记录][post-cache-failed-verification] 中，11测试项目已完成 **865/865**，其中 [Themes XML][post-cache-theme-xml] 为 **10/10**、0 failed/errors/skipped/notRun；新增 [标题与合并格几何两项][heading-table-geometry-tests] 均实际Pass。它们采用原Headless会话入口、默认主题、原生TextLayout和真实布局对象，不采用自定义空视觉替代。该次Verify因新增内嵌AXAML消费者运行不返回而最终退出1，不能记为整轮通过或76/78项新消费者通过；DA11的840整轮成功仍与此后续失败分别保留。

290D内嵌LF夹具精确Content Text后，InlineReader段落Measure阻塞的 [原始定位][inline-ui-stage-trace]、[最小对照][inline-wrap-infinite-cases] 和 [未成功的LineBreak私有分支][inline-hard-branch-proof] 继续保留；旧865 Verify仍退出1。后续0485已修复硬换行inline在Wrap/无限高度Measure中的非返回，并保持LF投影、Span样式、全局UTF-16范围和原生文字几何。[硬换行五Fact][hard-break-layout-tests] 使用同一测试DLL `982235EE…` 的 [290D对照][hard-break-regression-before] 在30秒外层期限超时，而 [0485修复后][hard-break-regression-after] 为5/5、Exit0、0错误/失败/跳过；具体 [XML][hard-break-regression-xml] 覆盖连续/首尾LF、宽窄回排、嵌套样式、Unicode/指针键盘链接、CRLF与reader复用。该五项已包含在此前0485的Headless164中，完整870与新76/78消费者另已复验；不回写旧失败，不把诊断或模型完成称作原生性能通过。

[ExtendedPresentationTests][extended-tests]8、[ReadingLayoutAnchorTests][layout-anchor-tests]8、[LinkFocusPreservationTests][link-focus-tests]3、[ResourceLifetimeTests][resource-lifetime-tests]2、[ImageLoaderContractTests][loader-contract-tests]6、[LinkResolutionTests][link-resolution-tests]6、[CustomContainerRendererTests][container-renderer-tests]12、[PageNavigationGeometryTests][page-geometry-tests]5、[UiDispatchLifetimeTests][ui-dispatch-tests]10、[ParserLifetimeTests][parser-lifetime-tests]1及 [ExtensionErrorRecoveryTests][error-recovery-tests]6均包含在此前0485的Headless164项中。高亮null/异常枚举保持原代码、registered resource.Dispose异常不打断其它清理/降级，专组由 [修正前5失败/1通过][error-recovery-before] 变为 [修正后6/6通过][error-recovery-after]。UI收件污染回归由 [旧实现失败][receiver-before] 变为 [中性作用域实现通过][receiver-after]，并严格保留宿主后台服务AsyncLocal继承断言。此前0485的 [API归档](../artifacts/markdown-release-evidence/f7c55c3a385a49fbb3ab7da96dc064bc/api/markdown-public-api.txt) 由 [0485实际导出helper][current-api-export-helper] 从Release生成，功能/主题/TextMate SHA分别为`04856FD6…`、`9D4C5D0A…`、`69F2CB47…`；[旧DA11 API归档][prior-da11-api]与旧包证据保持历史。Headless快照不代替真实剪贴板、麒麟或正式原生性能。

新增 [EOF转换器恢复8项][fence-recovery-tests] 已含在当前Core207项并全部通过：[隔离Core XML][fence-recovery-core] 与 [实际前后诊断][fence-recovery-proof] 记录header-only EOF、body、CRLF、两空格缩进、quote/list相邻兄弟和closed fence边界。普通未闭合fence的context/失败回退覆盖完整首行和其拥有的物理代码行，context仍是原始UTF-16子串；快照和诊断保留原AST范围，不吞入后续quote/list兄弟。grid cell源码不连续，context保留AST范围，异常/无效输出保留MDNODECONV但使用原生代码行投影；info由Markdig Info+Arguments全部token重建，其间空白可规范为一个空格，相邻单元格只复制一次。八项是固定恢复用例，不代表所有第三方扩展的源码无损写回。

M6软件几何修正的 [独立11项前后与159项整套记录][following-proof] 使用同一Headless测试DLL：旧71E042为2通过/9失败，A174E083为11/11及159/159；这份A174阶段proof不改记为DA11原生性能结果。它覆盖同步Updated尾部几何、用户重入与LayoutPending，也确认默认块边距导致的创建/裁剪布局循环。旧版两个Updated-tail用例在目标追加前就触发布局循环，不能单独证明追加时序缺陷；早期非法zero-renderer和编译失败后缓存DLL输出继续排除。最终DA11整套159项已另由840 Verify复测；DA11正式第一轮100短/长会话已完整结束且退出1：短流通过，长流全部实际提交UI+同步布局P9587.2475ms、Complete P952175.3424ms/最大2612.5187ms超原预算。第二至第五轮未启动，原始失败记录保留；归一化/身份索引缓存与语义差分正在修复验证，M6保持进行中，见[性能记录](markdown-performance.md)。

测试入口使用普通xUnit Fact/Theory与 [公开Headless会话包装][headless-entry]，真实调用者ExecutionContext.Run包住原完整测试体，显式Dispatch<int>等待异步完成；保留PerTest隔离、原输入/布局/断言和真实取消token，UI清理中单异常保原stack、双异常保两者。框架12.0.3的旧适配器初始Dispatch不捕获调用者上下文，已有 [官方同类问题](https://github.com/AvaloniaUI/Avalonia/issues/21332) 与 [上游修正](https://github.com/AvaloniaUI/Avalonia/pull/21357)；本库没有升级或fork框架。[输入边界][headless-input]、[原始timer/队列对照][context-proof] 与 [入口/收件对照][final-context-proof] 分别记录证据。早期public-session-1/2的24项“通过”因同步Dispatch<Task>形成Task<Task>而未等待完整测试体，已撤回、排除；有效记录只采用显式异步入口后的整套结果。

M6原生pilot发现直接改变滚动偏移后仍使用旧呈现范围而短暂空白。71E0仅在Offset属性变更时立即同步实际视口，保留原阅读修订条件与overscan。[ImmediateViewportTests][immediate-viewport-tests]3项在10,000块文档中从远端跳到顶部/中部/末尾，设Offset后只调用一次UpdateLayout；按实际Bounds计算可见块。其 [修正前后记录][immediate-viewport-proof] 为前3项全部visible=0失败，后3项通过、visible=10/9/9、realized=12/13/11，满足realized≤visible+16。三项亦包含在当前164项中；这份功能回归不代替原生帧或正式性能预算。

此前71E0阶段首套Verify在 [Themes XML][theme-adapter-failure] 记录6项通过、2项旧适配器cleanup errors、0产品断言失败，实际退出1，保留该失败。Themes的7项UI测试随后应用同一 [公开入口包装][theme-headless-entry]，1项纯程序集依赖测试保持原样；该DA11 Windows Verify已8/8通过，这次历史修正前失败仍保留。

此前DA11同源版本 [Linux SDK完整记录][linux-current-build] / [回归汇总][linux-current-summary] 已实际locked restore/全slnx Release为0 warning/error，11项目 **840/840**、0 errors/failed/skipped/notRun，含Core184和Headless159。源清单SHA前缀`418323DE…`对应 [Windows DA11冻结来源][linux-da11-source]；Linux功能/主题/TextMate/原生宿主SHA前缀依次为`6C30CB60…`、`E98394C6…`、`7A226EDB…`、`29FD4C13…`，由真实Linux SDK编译，不复用Windows assets/程序集或旧71E0 Linux808结果。此前 [71E0 Linux808][linux-viewport-build]、[1B65 Linux805][linux-final-fixture] 与 [旧Linux原生记录][linux-viewport-native] 保留历史。[当前Linux原生Demo][linux-current-native] 的40项页面/服务/图片协议检查为true，PNG已目视；原剪贴板没有可读取文本，保留并安全跳过复制，clipboard测试执行=false、整体false、退出1、error=null，未证明真实复制/恢复，不能记为43项通过。物理输入、辅助工具、完整DPI与麒麟仍待验。

“固定用例通过”只覆盖列出的语料与环境；“子集通过”表示该行仍有未取得的证据；“功能缺口”表示所需接口/能力尚未实现；“待平台验收”不能以 Headless 或其它平台推断。状态与缺口是整行的共同结论，不将一项测试名称中的 MT 编号当作该 MT 全项完成。

## MT01–MT24

| 编号 | 计划验收范围 | 现有证据 | 缺口与范围限制 | 当前状态 |
| --- | --- | --- | --- | --- |
| MT01 | 常规语法、转义、实体、空/null、CRLF；结构/文本/源码 span | [P][parser-tests] 的 EmptyInput、VisibleText、SoftHard、SourceSpans 黄金断言；[C][chunk-tests] CRLF/实体分块；[T][theme-tests] h1/代码/行内字体；[HT][heading-table-geometry-tests] h1–h6同文的默认32/26/22/19/17/16字号、SemiBold、TextLayout与实际Bounds高度递减、顺序不重叠、Source/全文投影保留，见 [实际Themes10 XML][post-cache-theme-xml]；[N][native-results] 原生阅读与投影 | 新标题证据属于后续290D阶段Headless固定几何，不改记为DA11原生探针；全部语法/字体/DPI组合及源span映射矩阵仍缺。硬换行阻塞已由[五Fact差分][hard-break-regression-after]及当前[76/78新消费者][current-consumer-results]修复复验；已有span断言不代表源码无损写回 | 固定语料/六级标题几何/内嵌硬换行通过；完整视觉矩阵未完成 |
| MT02 | 嵌套列表、引用、松紧列表、代码空白；层级与复制顺序 | [P][parser-tests]/[SP][structure-tests] 嵌套结构/编号/代码次序；[EP][extended-tests] 实际紧松列表间距，嵌套代码/marker投影不变；[C][chunk-tests] 续行；[I][interaction-tests] 原文复制 | 代码尾部 LF保留；紧松布局固定用例通过，深层引用与所有列表内代码组合的原生截图仍缺 | 固定结构/复制/间距子集通过 |
| MT03 | pipe/grid、宽表、内联单元格；跨度/对齐/滚动/选择 | [P][parser-tests] pipe/grid 多段落/colspan；[SP][structure-tests] rowspan 首/中/尾空槽、ragged/空 pipe 行与越宽列投影；[FR][fence-recovery-tests] grid fence原生/失败/无效输出不复制邻格；[R][reader-tests] 原生 Grid/横滚；[I][interaction-tests] 表格对齐与跨块拖选；[HT][heading-table-geometry-tests] 同一三列表的中列rowspan2与末行colspan3实际Grid边界/合并宽高、280DIP窄窗口横滚、右下单元格局部指针选区与全局UTF-16/原生范围几何、复制空槽，见 [实际Themes10 XML][post-cache-theme-xml] | 新组合通过Headless默认主题固定几何/模拟指针验收，不证明原生桌面截图、物理命中、DPI或任意合并格组合。grid fence context保AST范围，失败用原生代码行，info间空白可规范；复杂合并格实际平台视觉矩阵仍待验 | 模型与固定合并格布局/局部选择通过；完整平台视觉待验 |
| MT04 | 只读任务、五类提示、脚注重复引用；定位/复制 | [P][parser-tests]/[SP][structure-tests] 复制/排序/嵌套定义；[EP][extended-tests] 五类实际矢量图标且标题/正文可选；[IN][navigation-tests] 实际脚注点击定位；[AT][automation-tests] 只读任务状态，无 Toggle/Invoke；[T][theme-tests] TIP资源 | 固定图标/定位/只读provider通过；全部嵌套视觉及真实辅助工具读取仍待验 | 固定结构/原生布局/定位子集通过 |
| MT05 | 段落→代码→表格→离屏块；Ctrl+A/C、全文复制与回收 | [R][reader-tests] 千块离屏全选；[I][interaction-tests] 坐标跨块拖选；[IN][navigation-tests] PageUp/Down、Shift 锚点、Home/End、Ctrl+A/C、Escape 与越界拖选自动滚/释放停止；[IV][immediate-viewport-tests] 10,000块跳转的一次布局可见范围；[N][native-results] 真实文本剪贴板复制、读回和恢复 | Headless 输入为模拟；物理鼠标跨离屏区域、触屏与全部快捷键/平台组合仍待验收 | 逻辑全文/模拟输入与真实剪贴板子集通过 |
| MT06 | 中文、代理对、组合字符、ZWJ、RTL；命中与移动合法 | [S][selection-tests] 字素范围；[I][interaction-tests] Unicode 双击/Shift 左右；[AT][automation-tests] 组合字符、ZWJ、RTL 的 C# 范围；[R][reader-tests] 中文/希伯来文规范；[N][native-results] 中文/Emoji 正文复制 | 已证明逻辑字素完整性；RTL 视觉次序、逐字字体回退、物理命中、多 DPI 与真实辅助工具仍待验 | 字素/模拟输入子集通过；平台待验 |
| MT07 | 完整赋值与任意分块最终结构、链接、文本等价 | [P][parser-tests] 单切分点与随机分块；[C][chunk-tests] 9 份对抗语料所有两块切分点、12 种随机种子，Canonical 比较忽略身份而保留结构/span/目标；[R][reader-tests] 实际控件每切分点最终投影；[U][consumer-results] 编译 AXAML 消费者等价 | 范围为固定对抗语料；不宣称对所有第三方 pipeline/未知扩展渲染器均完成等价验收 | 固定对抗语料通过 |
| MT08 | 未闭合 fence、表格分隔线、列表续行、尾部引用/脚注 | [P][parser-tests] LateReferenceDefinitions；[C][chunk-tests] 未闭合代码、表格头、续行、引用/脚注，并验证此前无链接的前缀在尾部定义后改正 URL/title；[FR][fence-recovery-tests] EOF转换器恢复8项、quote/list兄弟边界及grid例外 | 固定转换失败恢复保留完整拥有源码，grid用原生代码行；context与快照/诊断范围边界见契约；所有中间状态的原生视觉变化、复杂引用依赖的长会话压力尚未全面覆盖 | 固定模型/调度/失败恢复用例通过 |
| MT09 | CRLF、代理对、实体跨 chunk、空 chunk、借用内存 | [C][chunk-tests] ActuallyPresentedIncompleteChunk：CRLF/半代理对/实体/ZWJ 前缀提交后最终修正；[Q][scheduler-tests] BorrowedMemory 被复制、空 chunk 不增加版本；[P][parser-tests] 规范化断言 | 中间已提交模型的证据来自可控 scheduler；每一种残缺字符的真实平台绘制/命中没有截图矩阵 | 模型/输入契约通过；中间绘制待验 |
| MT10 | 旧解析、图片、高亮晚到，不回流旧代次或事件 | [Q][scheduler-tests] 不可取消旧解析/UI 提交与失效失败；[R][reader-tests] 旧图片拒绝、旧高亮 Run 清除；[ES][service-tests] BaseUri/服务切换取消旧请求，迟到结果释放；[IN][navigation-tests] 完整替换清除旧链接焦点；[RS][resource-tests] epoch/取消与非合作服务迟到租约；[A][anchor-tests] 未变资源复用 | 受控顺序通过；持续高频切换与所有完成排列未穷尽，长期资源压力及真实远程协议仍需另验 | 过期结果子集通过；压力/协议另验 |
| MT11 | 持续/并发追加、慢解析、重复会话、结束/取消/Dispose | [Q][scheduler-tests] 慢解析前缀单调、单解析/单最新请求、并发无丢块、最终 apply、Dispose 与限额；[SL][lifecycle-tests] 重配置保留尾部、取消后继承源、已结束会话不取消新会话；[RS][resource-tests] 19 个资源并发/排队/取消边界；[N][native-results] 跨线程完成 | Windows 20chunk/s短流及1MiB长会话固定五轮预算由[独立守卫][windows-final-five-guard]通过；Linux最终五轮待验，持续更长周期及全部宿主组合不由固定样本推断 | 调度/结束与Windows五轮通过；Linux性能待验 |
| MT12 | 重复段落插入、身份迁移、删除锚点回退 | [S][selection-tests] 重复段落前插/中插、数量变化时保守重建、删除回退、追加保留身份/选区、纯逻辑阅读锚点；[LargeMigration][large-migration-tests] 2,000 个被删除旧叶/2,000 个新叶和唯一存活前缀的两个端点回退 | 当前迁移一次构建新文档身份字典，避免逐旧叶全树查找；该新用例没有性能预算断言。拖选捕获中同时改写复杂表格/链接或删除阅读块的视觉迁移尚缺组合证据 | 固定身份/选区用例通过；交互组合待验 |
| MT13 | 上滚暂停、回底恢复、视口上方图片增高保持锚点 | [A][anchor-tests] 块锚点/跟随；[LA][layout-anchor-tests] 同一长段落的实际字素Y、同block图片增高、排队重排/用户导航优先；[FC][following-proof] A174同步尾部几何/重入/LayoutPending 11项前后；[N][native-results] DA11原生固定子集 | 重排保留捕获字素的Y/像素偏移，新首行包含该字素，行首文字可自然改变；Windows五轮预算已由[守卫][windows-final-five-guard]通过，M6整体仍等Linux，复杂表格/所有输入竞争、远程图片及新逻辑锚点的真机组合仍待验 | Headless逻辑/几何与Windows原生固定子集通过 |
| MT14 | 字体、视口、DPI、主题、高亮主题；布局/命中与选区 | [T][theme-tests] 8项含挂载后reader.Styles层级Span规则增删、字体/颜色/布局变化且选区保留；[LA][layout-anchor-tests] 字体/动态资源/窄视口与custom helper的字素Y；[N][native-results] 原生换肤 | 固定动态局部/全局样式与阅读用例通过，不据一个选择器宣称任意规则/组合均已验收；全部重排/表格/命中及多DPI仍待验 | 样式/原生文字几何子集通过；DPI/组合待验 |
| MT15 | loader 后设置、单元素覆盖、BaseUri 改变、失败重试 | [ES][service-tests] 继承/样式/局部null/Reload/租约；[LC][loader-contract-tests] 6输入/错误/取消/限额实例；[LR][link-resolution-tests] 网页目标与基址切换；[N][native-results] 默认file/avares/回环HTTP实际PNG解码、编码/解码后像素限额、原生租约释放与局部服务 | DA11固定协议及服务通过；回环HTTP不证明公网，解码后限额不保证峰值内存，长期缓存/平台组合仍待验 | DA11协议/服务子集通过；公网/平台/压力待验 |
| MT16 | 无/未知/失败高亮与失效通知；原文复制、主题更新 | [X][textmate-tests] 13 项真实 tokenizer/主题/未知语言/取消/共享；[ES][service-tests] 代码局部高亮/null、共享失效/卸载订阅与迟到格式拒绝；[I][interaction-tests] 原文复制；[R][reader-tests] 无服务纯文本 | [ER][error-recovery-tests]已验证null/异常枚举保纯代码与迟到结果拒绝；任意宿主返回类型、更多语言视觉和取消时换主题仍需专项；ColorCode 留作后续 | 适配/元素服务/复制子集通过 |
| MT17 | 卸载重挂、隐藏、页面停用、模板更换、共享服务 | [IN][navigation-tests] 运行中模板替换/卸载释放 pointer capture、旧 drag 不恢复、逻辑 peer 保留；[AT][automation-tests] 12 次重挂单通知与 reader/peer GC；[RL][resource-lifetime-tests] 非合作图片/高亮仍运行时卸载reader可回收、订阅解除及迟到租约释放；[ES][service-tests] 共享服务关闭一方不影响另一方；[R][reader-tests]/[B][binding-tests]/[N][native-results] 完成/页面边界 | 固定本地及非合作服务生命周期通过；祖先隐藏与所有宿主重入/远程服务排列及长期稳定性仍待验 | 生命周期/捕获/局部回收子集通过 |
| MT18 | 自定义 pipeline、未知渲染节点、固定降级诊断 | [NC][converter-tests]/[NP][native-converter-tests] 类型安全转换、nullText保留结构/语义；[FR][fence-recovery-tests] EOF异常恢复8项含grid边界；[NR][renderer-tests]11实例原生helper/拖选/降级/资源；[CR][container-renderer-tests]12实例表格/代码/行CreateDefault、同列协调/跨度/顺序/实际几何；[SL][lifecycle-tests] 配置尾部；[U][consumer-results] 新API包消费通过 | [ER][error-recovery-tests]已验证抛错资源逐个清理、重入失效与原降级诊断保留；grid fence按原生代码行回退，context保AST范围；更复杂宿主异常排列尚未穷尽；数学/Mermaid/HTML不因parser可扩展就获得renderer | 公开扩展/默认容器固定用例通过；异常降级固定用例通过 |
| MT19 | 功能独用、主题、TextMate的真正独立NuGet消费 | 当前EDB5独立配置/全新缓存/编译AXAML：[FunctionOnly76][active-function-consumer] / [Theme+TextMate78][active-theme-consumer]全部true，各18项内嵌文本通过；[功能监督][active-function-supervisor]/[主题监督][active-theme-supervisor] noTimeout、Exit0，无ProjectReference；包SHA在[当前汇总][active-consumer-summary]。0485和DA11各原始记录保留 | 当前公共API签名相对0485无变化，896整仓/七包及原控件消费通过；新不可变软件归档待收束。Headless不证明真实剪贴板/辅助工具/麒麟或原生预算 | 当前76/78包消费及软件整轮通过；完整平台/性能另验 |
| MT20 | 链接键盘激活、只读任务、文档/选择无障碍 | [AT][automation-tests] 12 项：readonly IValueProvider 全文、延迟离屏逻辑树、HeadingLevel、Link Invoke/Bring/focus、只读任务状态、字素范围/selection、旧 generation 拒绝、事件与回收；[I][interaction-tests]/[N][native-results] 宿主链接事件 | Avalonia 12.0.3 无公开 ITextProvider/ITextRangeProvider，本库**不支持原生 UIA TextPattern**；C# MarkdownAutomationTextRange 不是平台文本范围。真实辅助工具与原生桥接体验待验，详见 [支持级别][automation-contract] | 公布 provider/C# 范围的 Headless 用例通过；平台辅助工具待验 |
| MT21 | 反复导航/替换/共享服务/持续流式无持续增长 | [AT][automation-tests] 20 次替换裁剪旧 peers、弱引用回收、12 次重挂单通知、无服务 reader/peer GC；[RL][resource-lifetime-tests] 非合作loader/highlighter仍执行时卸载reader可回收的2实例；[ES][service-tests] 共享订阅与 owned/borrowed lease；[RS][resource-tests] 实际执行 ≤4、等待 ≤256，取消未返回不伪造释放槽；[Q][scheduler-tests] 单解析/单最新请求 | 固定本地及非合作服务GC通过，不代表所有宿主外部引用均能解除；Windows五轮每轮100次资源/虚拟化门槛已有[守卫通过][windows-final-five-guard]；Linux最终原生及更长周期稳定性待验，峰值与任意宿主外部引用不从固定用例推断，累计分配不是泄漏判定 | 局部回收/有界资源证据通过；长期稳定性待验 |
| MT22 | Windows/Linux/麒麟及100%/125%/150%/200%缩放 | [当前Windows EDB5原生smoke][final-windows-smoke] 43/43 true、scale1、1440×900窗口/1184×605区域，真实文本复制/读回/恢复通过、监督Exit0；[当前Linux7B29原生smoke][final-linux-smoke] 40项功能/协议true、error=null，剪贴板未执行、overall=false/退出1；两PNG已目视，来源与四DLL各自固定。Linux原生五轮预算另由[守卫][active-linux-five-guard]通过 | Linux剪贴板保持Pending；WSLg/X11不代替实体Linux、Wayland或麒麟。程序输入、窗口渲染图及回环HTTP不证明物理输入、公网HTTPS、多DPI、系统外链或辅助工具 | Windows scale1当前候选功能/剪贴板通过；Linux功能和五轮性能通过、剪贴板待验；完整设备矩阵另验 |
| MT23 | 内部 Text 同步、TwoWay 同值回写、宿主外部替换 | [B][binding-tests] 同值 echo 保留会话/选区/绑定、VM setter 转换与属性事件重入使外部新文本优先且旧 Updated 不发布；[Q][scheduler-tests] 代次保持；[U][consumer-results] 真实编译 AXAML 的流式后仍可完整替换；[N][native-results] 原生绑定/选区 | 范围为固定绑定模型/事件重入及两种消费者；其它自定义宿主回调和平台交互组合不从这些测试自动推断 | 约定绑定用例与隔离 AXAML 范围通过 |
| MT24 | 完成时隐藏/未挂载/缺模板/失败/延迟图片 | [Q][scheduler-tests] Applied/LayoutPending/NoPresentationEnvironment、解析与呈现失败终止等待并可恢复；[B][binding-tests] 有效原生 TextLayout/缺模板；[R][reader-tests] 隐藏与未挂载；[A][anchor-tests] 最终文档不等图片；[N][native-results] 可见最终提交 | 本轮覆盖受控完成边界；实际远程协议、所有模板/平台故障及显示帧时延仍待验；不将任务完成当作远程图片全部下载或首屏预算通过 | 约定完成边界子集通过 |

## 证据入口

| 代号 | 文件 / 报告 | 范围 |
| --- | --- | --- |
| P / S / SP / TR | [Parser][parser-tests] / [Selection][selection-tests] / [LargeMigration][large-migration-tests] / [StructureProjection][structure-tests] / [Traversal][traversal-tests] | 模型/投影/身份/字素与深树遍历；与NC/NP等合计Core207项 |
| NC / NP | [NodeConverter][converter-tests] / [NativeConverterProjection][native-converter-tests] | 自定义扩展、metadata/SemanticKind、内置结构投影保留与降级 |
| FR | [FenceConverterRecovery][fence-recovery-tests] / [前后诊断][fence-recovery-proof] / [隔离Core XML][fence-recovery-core] | 新增8项已含在当前Core207：EOF首行/正文/CRLF/缩进、quote/list兄弟、closed fence与grid原生/失败/无效输出例外 |
| Q / C / RS | [Scheduler][scheduler-tests] / [Chunks][chunk-tests] / [ResourceScheduler][resource-tests] | 解析调度/分块41项与资源调度19实例，合计 Streaming60 项 |
| R / I / B / A | [Reader][reader-tests] / [Interaction][interaction-tests] / [Binding][binding-tests] / [Anchor][anchor-tests] | 原控件集成/交互/绑定/资源锚点，包含在Headless159项内 |
| ES / NR / AT / IN / SL | [ElementService][service-tests] / [NodeRenderer][renderer-tests] / [Automation][automation-tests] / [InputNavigation][navigation-tests] / [StreamingLifecycle][lifecycle-tests] | 服务8/renderer11/automation12/导航7/生命周期5实例，包含在Headless159项内 |
| EP / LA / LF / RL / LC | [ExtendedPresentation][extended-tests] / [ReadingLayoutAnchor][layout-anchor-tests] / [LinkFocus][link-focus-tests] / [ResourceLifetime][resource-lifetime-tests] / [ImageLoaderContract][loader-contract-tests] | 扩展布局8/字素阅读8/链接焦点3/非合作服务GC2/loader契约6，包含在Headless159项内 |
| LR | [LinkResolution][link-resolution-tests] | 6实例实际通过：resolved visited别名/基址/宿主重入、回收/追加和图片root/authority |
| CR / PG / UD / PL / ER | [CustomContainerRenderer][container-renderer-tests] / [PageGeometry][page-geometry-tests] / [UiDispatchLifetime][ui-dispatch-tests] / [ParserLifetime][parser-lifetime-tests] / [ErrorRecovery][error-recovery-tests] | 容器默认呈现12/实际双向页距5/UI上下文与旧错误10/非合作parser GC1/错误恢复6，包含在此前0485的Headless164项完整Verify中 |
| IV | [ImmediateViewport][immediate-viewport-tests] / [修正前后][immediate-viewport-proof] | 10,000块顶部/中部/末尾一次布局3项，before全部visible0、after3/3通过；包含在此前0485的Headless164项内 |
| FC | [FollowingDocumentCommit][following-tests] / [A174阶段proof][following-proof] | 同一测试DLL11项：71E042为2通过/9失败，A174为11/11和整套159；DA11当前Verify另复测159，软件helper几何不等于M6原生预算 |
| T / X | [ThemeContractTests][theme-tests] / [TextMateCodeHighlighterTests][textmate-tests] | Themes8项、TextMate13项本轮Verify实际通过 |
| N | [当前Windows EDB5原生结果][final-windows-smoke] / [监督][final-windows-smoke-supervisor] / [来源][final-windows-smoke-source] | 2026-10-15当前候选43项true、Exit0/noTimeout、error=null，包含真实文本复制/读回/原文本恢复、原生几何与局部服务、五图标及file/avares/回环HTTP限额/租约，PNG已目视；旧DA11/0485各自保留。物理输入/辅助工具/DPI/麒麟仍未验证 |
| LN | [当前真实Linux SDK][active-linux-build] / [896汇总][active-linux-regression] / [483源清单][active-linux-source] / [跨平台软件及API比较][active-source-api-proof] / [新正式五轮来源][active-linux-formal-source] | 真实SDK locked restore/全solution Release0 warning/error、11项目896通过；仅两个validation脚本修正后与Linux输入分别记383相同+2差异。Linux新五轮已由[修正守卫][active-linux-five-guard]实际通过，旧失败守卫保留，当前Windows新五轮首轮预算失败、后轮停止；旧0485的870/40项smoke与97e正式失败、DA11各历史结果保留。WSLg/X11不代替麒麟、设备或剪贴板验收 |
| U | [当前消费汇总][active-consumer-summary] / [Function76][active-function-consumer] / [Theme78][active-theme-consumer] / [增强脚本][consumer-script] | EDB5本轮76/78全部通过，各18项compiled AXAML内嵌文本、服务/扩展/geometry/automation/URI；两监督noTimeout、Exit0。新不可变归档待收束；[0485软件归档][current-software-archive]及[旧DA11 58/60][consumer-results]保持历史 |

性能退出预算另见 [性能方法与结果](markdown-performance.md)。纯模型五轮500样本与历史Windows原生pilot均按各自源码/程序集哈希登记，历史记录不覆盖此前0485构建。旧71E0/Host2370首个正式轮次在100KiB纯文本100样本通过后，于1MiB纯文本等待实际Render超时；[原报告][minimized-formal-round]保留overallfalse/退出1。诊断显示请求与超时快照WindowState=Minimized，Win32停止绘制，属环境无效，不能算正式一轮。此前Host351D加最小化守卫后的正式预算失败也保留；DA11正式首轮长会话失败已保留；此前0485 Windows五轮阅读/流式/长块/资源已由[独立守卫][windows-final-five-guard]通过，Linux最终五轮及完整设备矩阵仍待验收。本阶段构建/测试/包通过不代替M6，不将pilot或单轮通过提升为正式退出。没有同机官方测量，不宣称达到或超过官方性能。

整体阶段由 [实施状态](markdown-status.md)维护，公开能力和 API/样式差异见 [官方功能对照](markdown-official-alignment.md)、[公共契约](markdown-contracts.md)与 [使用说明](markdown-usage.md)。本库新功能的固定自动化子集通过，不等于所有视觉、性能、包与平台退出条件完成。

[parser-tests]: ../tests/Markdown.Core.Tests/MarkdownParserTests.cs
[selection-tests]: ../tests/Markdown.Core.Tests/MarkdownSelectionTests.cs
[large-migration-tests]: ../tests/Markdown.Core.Tests/MarkdownLargeMigrationTests.cs
[structure-tests]: ../tests/Markdown.Core.Tests/MarkdownStructureProjectionTests.cs
[converter-tests]: ../tests/Markdown.Core.Tests/MarkdownNodeConverterTests.cs
[native-converter-tests]: ../tests/Markdown.Core.Tests/MarkdownNativeConverterProjectionTests.cs
[traversal-tests]: ../tests/Markdown.Core.Tests/MarkdownTraversalTests.cs
[scheduler-tests]: ../tests/Markdown.Streaming.Tests/MarkdownUpdateSchedulerTests.cs
[chunk-tests]: ../tests/Markdown.Streaming.Tests/MarkdownChunkEquivalenceTests.cs
[resource-tests]: ../tests/Markdown.Streaming.Tests/MarkdownResourceSchedulerTests.cs
[reader-tests]: ../tests/Markdown.Headless.Tests/ReaderTests.cs
[interaction-tests]: ../tests/Markdown.Headless.Tests/InteractionTests.cs
[binding-tests]: ../tests/Markdown.Headless.Tests/StreamingBindingTests.cs
[anchor-tests]: ../tests/Markdown.Headless.Tests/ReadingAnchorTests.cs
[service-tests]: ../tests/Markdown.Headless.Tests/ElementServiceTests.cs
[renderer-tests]: ../tests/Markdown.Headless.Tests/NodeRendererTests.cs
[automation-tests]: ../tests/Markdown.Headless.Tests/AutomationTests.cs
[navigation-tests]: ../tests/Markdown.Headless.Tests/InputNavigationTests.cs
[lifecycle-tests]: ../tests/Markdown.Headless.Tests/StreamingLifecycleTests.cs
[extended-tests]: ../tests/Markdown.Headless.Tests/ExtendedPresentationTests.cs
[layout-anchor-tests]: ../tests/Markdown.Headless.Tests/ReadingLayoutAnchorTests.cs
[link-focus-tests]: ../tests/Markdown.Headless.Tests/LinkFocusPreservationTests.cs
[resource-lifetime-tests]: ../tests/Markdown.Headless.Tests/ResourceLifetimeTests.cs
[loader-contract-tests]: ../tests/Markdown.Headless.Tests/ImageLoaderContractTests.cs
[link-resolution-tests]: ../tests/Markdown.Headless.Tests/LinkResolutionTests.cs
[automation-contract]: ../src/NT.AvaloniaControl.Markdown/Automation/README.md
[theme-tests]: ../tests/Markdown.Themes.Tests/ThemeContractTests.cs
[textmate-tests]: ../tests/Markdown.TextMate.Tests/TextMateCodeHighlighterTests.cs
[native-results]: ../artifacts/platform/windows-markdown-smoke-20261014-following-recovery.json
[native-image]: ../artifacts/platform/windows-markdown-smoke-20261014-following-recovery.png
[container-renderer-tests]: ../tests/Markdown.Headless.Tests/CustomContainerRendererTests.cs
[page-geometry-tests]: ../tests/Markdown.Headless.Tests/PageNavigationGeometryTests.cs
[ui-dispatch-tests]: ../tests/Markdown.Headless.Tests/UiDispatchLifetimeTests.cs
[parser-lifetime-tests]: ../tests/Markdown.Headless.Tests/ParserLifetimeTests.cs
[headless-input]: ../tests/Markdown.Headless.Tests/ScopedHeadlessInput.cs
[context-proof]: ../artifacts/consumer/whole-headless-isolation/context-boundary-proof.json
[headless-round-1]: ../artifacts/verification/headless-context-final-round-1.xml
[headless-round-2]: ../artifacts/verification/headless-context-final-round-2.xml
[consumer-results]: ../artifacts/markdown-release-evidence/43c553bb86024ca3bc665123086ac784/consumers/summary.json
[consumer-script]: ../scripts/Test-MarkdownPackageConsumer.ps1



[ed-verification]: ../artifacts/verification/86c2bb4bb03d415698deea43da3faa8a/results.json
[native-source]: ../artifacts/platform/windows-markdown-smoke-20261014-following-recovery.source.json
[error-recovery-tests]: ../tests/Markdown.Headless.Tests/ExtensionErrorRecoveryTests.cs
[error-recovery-before]: ../artifacts/markdown-error-recovery-probe/6ece8cb7f2e3425b871a116f41c088eb/before-fix.xml
[error-recovery-after]: ../artifacts/markdown-error-recovery-probe/6ece8cb7f2e3425b871a116f41c088eb/after-fix.xml
[receiver-before]: ../artifacts/consumer/whole-headless-isolation/receiver-before.log
[receiver-after]: ../artifacts/consumer/whole-headless-isolation/receiver-after.log
[headless-entry]: ../tests/Markdown.Headless.Tests/HeadlessTestScope.cs
[final-context-proof]: ../artifacts/consumer/whole-headless-isolation/final-context-proof.json


[theme-adapter-failure]: ../artifacts/verification/e64a4fd99b2243a79b21d6f4dee69599/Markdown.Themes.Tests.xml
[theme-headless-entry]: ../tests/Markdown.Themes.Tests/HeadlessTestScope.cs

[linux-final-fixture]: ../artifacts/linux-build/7b37264619ea4225bbdbd84fdde263bc/retest-final-themes-7d76ab4032f84ce6a0b037b2a823347d/result.json

[final-verification]: ../artifacts/verification/7b338e894adb4d51a425a0c3b6134138/results.json


[prior-verification]: ../artifacts/verification/7d37c1159f174f5c83944f91f1b21e18/results.json
[prior-native-source]: ../artifacts/platform/windows-markdown-smoke-20261014-error-recovery.source.json
[current-headless]: ../artifacts/verification/7b338e894adb4d51a425a0c3b6134138/Markdown.Headless.Tests.xml
[immediate-viewport-tests]: ../tests/Markdown.Headless.Tests/ImmediateViewportTests.cs
[immediate-viewport-proof]: ../artifacts/consumer/whole-headless-isolation/immediate-viewport-proof.json



[linux-current-build]: ../artifacts/linux-build/8df8090ad9b844a096f573b154f4a91b/result.json
[linux-current-native]: ../artifacts/platform/linux-markdown-smoke-11a1a9e386d248f3b4aa8bc1351f097f/report.json
[linux-current-native-result]: ../artifacts/platform/linux-markdown-smoke-11a1a9e386d248f3b4aa8bc1351f097f/result.json

[minimized-formal-round]: ../artifacts/benchmarks/markdown/windows-formal-71e042-bd1f0d0093064986aa15dfe134f54df8/native-round-1.json

[viewport-verification]: ../artifacts/verification/d0c2ce2b3ea241aaa7a215c288dbf66e/results.json
[viewport-native-results]: ../artifacts/platform/windows-markdown-smoke-20261014-viewport.json
[viewport-native-source]: ../artifacts/platform/windows-markdown-smoke-20261014-viewport.source.json
[api-export-helper]: ../artifacts/markdown-api-helper/214e9818405e42698daaa7bcd070e200/Program.cs
[fence-recovery-tests]: ../tests/Markdown.Core.Tests/MarkdownFenceConverterRecoveryTests.cs
[fence-recovery-core]: ../artifacts/markdown-fence-recovery/740420b0a3794bffb96199726d2125dc/core-fence-after.xml
[fence-recovery-proof]: ../artifacts/markdown-fence-recovery/740420b0a3794bffb96199726d2125dc/fence-failure-summary.json
[following-tests]: ../tests/Markdown.Headless.Tests/FollowingDocumentCommitTests.cs
[following-proof]: ../artifacts/markdown-following-probe/6471905ac7644cd5bfb1372c86b3e21b/results.json
[linux-da11-source]: ../artifacts/linux-build/8df8090ad9b844a096f573b154f4a91b/frozen-source.json
[linux-current-summary]: ../artifacts/linux-build/8df8090ad9b844a096f573b154f4a91b/regression-summary.json
[linux-current-image]: ../artifacts/platform/linux-markdown-smoke-11a1a9e386d248f3b4aa8bc1351f097f/preview.png
[linux-viewport-build]: ../artifacts/linux-build/7cc589e63c24417881d1fe614f5da928/result.json
[linux-viewport-native]: ../artifacts/platform/linux-markdown-smoke-e70d5b21251d4c7eb6ea705793575954/report.json

[heading-table-geometry-tests]: ../tests/Markdown.Themes.Tests/MarkdownHeadingAndTableGeometryTests.cs
[post-cache-theme-xml]: ../artifacts/verification/0247dca7a9204c59a3fe26c71bacb068/Markdown.Themes.Tests.xml
[post-cache-failed-verification]: ../artifacts/verification/0247dca7a9204c59a3fe26c71bacb068/results.json
[inline-ui-stage-trace]: ../artifacts/markdown-consumer-diagnosis/f4418e5d59f24ab9b1128e0e615ba659/runtime.log
[inline-wrap-infinite-cases]: ../artifacts/markdown-consumer-diagnosis/585d9907d80843a7a4756f7be149d5a3/case-results.json
[inline-hard-branch-proof]: ../artifacts/markdown-consumer-diagnosis/918f072abe4f4ee89751668c0f5ae516/runtime.log

[current-final-verification]: ../artifacts/verification/19b1d0d43b0e472aac15d0fdd2d4d48d/results.json
[current-headless-final]: ../artifacts/verification/19b1d0d43b0e472aac15d0fdd2d4d48d/Markdown.Headless.Tests.xml
[current-theme-final]: ../artifacts/verification/19b1d0d43b0e472aac15d0fdd2d4d48d/Markdown.Themes.Tests.xml
[current-consumer-results]: ../artifacts/markdown-package-consumer-results.json
[current-function-consumer]: ../artifacts/consumer/b85e8af78af34dfc9feee6da05c4597f/result.json
[current-theme-consumer]: ../artifacts/consumer/731d211645724312bb82b795fad57951/result.json
[current-function-supervisor]: ../artifacts/consumer/b85e8af78af34dfc9feee6da05c4597f/supervisor-result.json
[current-theme-supervisor]: ../artifacts/consumer/731d211645724312bb82b795fad57951/supervisor-result.json
[current-api-export-helper]: ../artifacts/markdown-api-helper/cfa345c0edc04f388fc8f6337c64b252/Program.cs
[prior-da11-api]: ../artifacts/markdown-release-evidence/43c553bb86024ca3bc665123086ac784/api/markdown-public-api.txt
[current-windows-native]: ../artifacts/platform/windows-markdown-smoke-cache-7bee07f6bf6f455ab2c77b2d9163d271.json
[current-windows-native-source]: ../artifacts/platform/windows-markdown-smoke-cache-7bee07f6bf6f455ab2c77b2d9163d271.source.json
[hard-break-layout-tests]: ../tests/Markdown.Headless.Tests/MarkdownHardLineBreakLayoutTests.cs
[hard-break-regression-before]: ../artifacts/markdown-hard-break-regression/729e01e7ea0e47fb8ec2701eb3167494/before/result.json
[hard-break-regression-after]: ../artifacts/markdown-hard-break-regression/729e01e7ea0e47fb8ec2701eb3167494/after/result.json
[hard-break-regression-xml]: ../artifacts/markdown-hard-break-regression/729e01e7ea0e47fb8ec2701eb3167494/after/hard-break.xml


[windows-final-five-guard]: ../artifacts/benchmarks/markdown/windows-formal-04856f-9414e79e2b684ba08992741f7fa0ab86/guard.json
[windows-final-five-source]: ../artifacts/benchmarks/markdown/windows-formal-04856f-9414e79e2b684ba08992741f7fa0ab86/source-snapshot.json
[linux-final-source-files]: ../artifacts/linux-build/5a681321a91349dda6bace76a80eb43d/source-files.json

[current-software-archive]: ../artifacts/markdown-release-evidence/f7c55c3a385a49fbb3ab7da96dc064bc/manifest.json
[linux-048-build]: ../artifacts/linux-build/5a681321a91349dda6bace76a80eb43d/result.json
[linux-048-regression]: ../artifacts/linux-build/5a681321a91349dda6bace76a80eb43d/regression-summary.json
[linux-048-failed-native]: ../artifacts/linux-probe/97e74dcd972349b3b28b216f3b97d388/report-1.json
[linux-048-smoke]: ../artifacts/platform/linux-markdown-smoke-2c50e76225fd4e839a978f3a785e6433/report.json
[linux-048-smoke-result]: ../artifacts/platform/linux-markdown-smoke-2c50e76225fd4e839a978f3a785e6433/result.json

[active-verification]: ../artifacts/verification/85ea048786ea4b6288c23ccacd65b4d2/results.json
[active-consumer-summary]: ../artifacts/markdown-package-consumer-results.json
[active-function-consumer]: ../artifacts/consumer/cfb8502b20bb4ea0870c5add1e213816/result.json
[active-theme-consumer]: ../artifacts/consumer/f092fb9ab058410492b9f2509d6dc60b/result.json
[active-function-supervisor]: ../artifacts/consumer/cfb8502b20bb4ea0870c5add1e213816/supervisor-result.json
[active-theme-supervisor]: ../artifacts/consumer/f092fb9ab058410492b9f2509d6dc60b/supervisor-result.json

[active-linux-build]: ../artifacts/linux-build/1fcc230d5ebf437c876a92ef96dd4900/result.json
[active-linux-regression]: ../artifacts/linux-build/1fcc230d5ebf437c876a92ef96dd4900/regression-summary.json
[active-linux-source]: ../artifacts/linux-build/1fcc230d5ebf437c876a92ef96dd4900/source-files.json
[active-source-api-proof]: ../artifacts/markdown-candidate-landings/0581031e556d49e394cf377fbc92efb5/cross-platform-software-and-api.json
[active-linux-formal-source]: ../artifacts/linux-probe/a24551de85354cdb8b6fdcd244dcb5f3/source-snapshot.json

[active-linux-five-guard]: ../artifacts/linux-probe/a24551de85354cdb8b6fdcd244dcb5f3/guard-timespan.json
[active-linux-old-failed-guard]: ../artifacts/linux-probe/a24551de85354cdb8b6fdcd244dcb5f3/guard.json
[active-timespan-bcl]: ../artifacts/markdown-time-precision/a3d4df624c3b49fa8c28ed7e0310534f/comparison.json
[active-timespan-tests]: ../artifacts/guard-tests/markdown-e8e4e5e0715f4988800f8b7207ef4eb8/results.json
[active-timespan-validation]: ../artifacts/linux-probe/a24551de85354cdb8b6fdcd244dcb5f3/timespan-validation-result.json

[final-timespan-verification]: ../artifacts/verification/85ea048786ea4b6288c23ccacd65b4d2/results.json
[final-timespan-manifest]: ../artifacts/verification/85ea048786ea4b6288c23ccacd65b4d2/source-manifest.json
[final-compiled-source-proof]: ../artifacts/markdown-candidate-landings/0581031e556d49e394cf377fbc92efb5/final-source-comparison.json
[final-linux-five-summary]: ../artifacts/linux-probe/a24551de85354cdb8b6fdcd244dcb5f3/final-five-round-summary.json
[final-windows-five-source]: ../artifacts/benchmarks/markdown/windows-formal-edb5-4603a6b51c1b4d38a245779e14362583/source-snapshot.json
[final-windows-smoke]: ../artifacts/markdown-m7-preparation/98f7cbfafb9244baa11e1051c04df609/windows-smoke-ec44d5ad0a594acd869f697b311dddb8/report.json
[final-windows-smoke-supervisor]: ../artifacts/markdown-m7-preparation/98f7cbfafb9244baa11e1051c04df609/windows-smoke-ec44d5ad0a594acd869f697b311dddb8/supervisor-result.json
[final-windows-smoke-source]: ../artifacts/markdown-m7-preparation/98f7cbfafb9244baa11e1051c04df609/windows-smoke-ec44d5ad0a594acd869f697b311dddb8/source.json
[final-linux-smoke]: ../artifacts/platform/linux-markdown-smoke-7b29d5-085db6f4b0204d55a5d60d93feae883f/report.json
[final-linux-smoke-result]: ../artifacts/platform/linux-markdown-smoke-7b29d5-085db6f4b0204d55a5d60d93feae883f/result.json

[failed-edb5-windows-round]: ../artifacts/benchmarks/markdown/windows-formal-edb5-4603a6b51c1b4d38a245779e14362583/native-round-1.json
[failed-edb5-windows-supervision]: ../artifacts/markdown-m7-preparation/98f7cbfafb9244baa11e1051c04df609/superseded-edb5-windows-result.json
