# Markdown 官方功能对照

## 当前结论（2026-10-15）

当前M7可执行软件范围已通过，见[本次M7收束记录](markdown-m7-completion-20261015.md)及[新M7执行记录](../artifacts/markdown-m7-completion/c63fab7542ba4b22b9a63d9cbb736f1b/m7-exit.json)。Windows43项与Linux私有Xvfb/X11新43项真实复制、读回及恢复通过，实际producer/Main/外层均退出0；原WSLg共享显示的安全Pending保持历史记录，两个环境分别记账。

Windows50ms、Linux51ms及身份、引用、脚注语义不变；本次增补按同一冻结源码和产物SHA复用五轮、544×2、416×2、包/API及85/134工具合同，不声称再次执行。远端原生CI由用户暂缓（DeferredByUser）；麒麟、实体Linux/原生Wayland、完整DPI/多屏、物理输入和真实辅助工具继续待验证。未发布、未改版本。

## 以下为历史阶段证据与对照记录

以下段落保留采集当时的口径，包括“四轮”“尚待复评”“M7 仅准备”和旧 Linux 剪贴板 Pending；这些是历史状态，当前结论以上节及其链接为准。旧失败、源码和平台边界不回写或转签。

参考资料核验日期：2026 年 10 月 13 日；本库证据更新：2026 年 10 月 15 日。参考包固定为 `Avalonia.Controls.Markdown 12.3.0`（2026-09-11 发布，依赖 Avalonia ≥ 12.0.3）。本库为独立实现，不引用官方 Markdown、Documents 或 RichTextEditor 程序集。公开文档可能随版本更新；下表保留已核验资料，尚未运行商业参考控件进行行为或视觉对照。[固定包](https://www.nuget.org/packages/Avalonia.Controls.Markdown/12.3.0)

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

“公开资料确认”只说明参考方描述了该能力；“本库验收”按实际运行证据登记。MD/MT编号来自 [开发计划](markdown-development-plan.md)，不能因子集通过就将整项完成。此前0485/Host24CA的 [Windows正式五轮独立守卫][windows-final-five-guard] 已实际 **全部通过=true**、独立进程数5；[冻结来源][windows-final-five-source] 固定功能/主题/TextMate/Host四DLL为0485/9D4C/69F2/24CA，五轮各11场景均通过，保持原预算、每轮阅读/长块各100样本、短/长流各100会话及10次预热，不与旧失败轮次拼接。1MiB长会话五轮Complete P95为510.0244–548.9444ms、各轮最大值总体659.4792ms，全部实际提交UI+同步布局P95为31.5532–32.5640ms；固定首屏、长块选择/滚动、10,000块回收与资源稳定性门槛同由独立守卫通过。这是Windows平台性能通过，不改变7bee功能探针40项/ClipboardPending，也不证明物理扫描、读屏、DPI或麒麟。同源[真实Linux SDK结果][linux-048-build]已完成locked restore/Release0 warning/error，11项目[870/870回归][linux-048-regression]通过，四DLL与Windows分别登记；[477文件输入清单][linux-final-source-files]固定该副本来源。但该Linux输出的[正式首轮][linux-048-failed-native]收齐11组后失败：1MiB纯文本/TextMate UI P9552.8128/56.1590ms、短流最早未显示至帧P95177.6524ms、长流全部实际提交UI+同步布局P9574.9159ms均超原预算。第二至第五轮停止；整体M6尚未退出，M7包/API/软件归档材料继续收集。后续私有优化及分项通过不继承0485 Windows五轮，也不代替最终版本的两平台五轮。

后续私有a102已补范围收敛、dirty前缀、当前阅读锚点恢复、严格匹配后的不可变DTO复用及单Presenter文字属性复用。[Core214完整回归](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/core-final-after/result.json)、[Headless182完整回归](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/font-properties-context-after/result.json)均通过，包含重复节点、迟到引用/脚注、原生几何/命中及动态样式差分。[完整11组探索](../artifacts/linux-probe/028f5d059c97437a9bb4d2fa8560b173/report-1.json)也全部通过，短流P95129.773ms、长流402次全部实际提交UI P9545.8342ms；但[来源](../artifacts/linux-probe/028f5d059c97437a9bb4d2fa8560b173/source-snapshot.json)为私有B0A9DADF Windows IL、LinuxSDK编译=false，且10样本/10会话、1预热、formal=false。它们不计入0485的870、不继承旧Windows五轮，不提前宣布M6/M7退出；后续EDB5已完成896软件整轮与新包/API验证；真实Linux SDK与两平台各五轮仍须新证据。失败链与拒绝更改600字体别名的原因见[性能记录](markdown-performance.md)。

此前DA11 Release构建 [完整Verify][final-verification] 已实际退出0：Core184/184、Streaming60/60、Headless159/159、Themes8/8、TextMate13/13，共 **424项**；连同既有416项，11项目为 **840/840**，0 errors/failed/skipped/notRun，七包及全部消费者完成。该DA11 Headless证据见 [159项XML][current-headless]。同构建Windows原生43项true、退出0且PNG目视通过，真实文本剪贴板已复制/读回/恢复，来源/程序集/报告哈希见 [来源记录][native-source]。最终Markdown消费者58/58、60/60共118项对应 [汇总][consumer-results] 三包SHA256前缀`50FCA779B15D…`、`070E8ED561B02…`、`376490E7D443…`；原生43和消费118分别记账，不加入840。此前71E0构建 [808/808][viewport-verification]（Core163/Headless148、Markdown392）、ED构建 [798/798][ed-verification]、1B65构建 [805/805][prior-verification] 及其Headless两独立145/145 [第一轮][headless-round-1]/[第二轮][headless-round-2] 保留为历史，不与本轮相加；[71E0原生来源][viewport-native-source] 和 [1B65原生来源][prior-native-source] 另留。71E0阶段消费者包前缀`0FAD8AD698F5…`、`96FBF610B07F…`、`D0751E53AC11…`保留为历史。未改guard98证据的复用与原生性能未包含状态均列在Verify结果中。整体见 [状态记录](markdown-status.md)、[验收矩阵](markdown-acceptance-matrix.md)与 [公共契约](markdown-contracts.md)。

新增 [错误恢复专组][error-recovery-tests] 修正高亮null/异常枚举清空代码、registered resource.Dispose打断降级/剩余清理，由 [修正前5失败/1通过][error-recovery-before] 到 [修正后6/6通过][error-recovery-after]；新UI收件污染回归由 [旧实现失败][receiver-before] 到 [中性作用域实现通过][receiver-after]，后台宿主服务仍继承其调用上下文。容器renderer12、页距5、UI上下文10、parser GC1及错误恢复6均含在当前164项中。夹具通过 [公开Headless会话入口][headless-entry] 将真实xUnit调用者ExecutionContext带入原完整测试体，显式Dispatch<int>等待异步完成，保PerTest、真实取消token及全部原断言；原12.0.3适配器的上下文丢失有 [官方问题](https://github.com/AvaloniaUI/Avalonia/issues/21332) 和 [上游修正](https://github.com/AvaloniaUI/Avalonia/pull/21357)，本库未升级或fork框架。[独立入口/收件对照][final-context-proof] 记录原始失败和修正结果；早期24项public-session-1/2记录因误用同步Dispatch<Task>而未等待body，已撤回，不能作为通过证据。

[EOF转换器恢复8项][fence-recovery-tests] 均含在当前Core207项；[隔离Core XML][fence-recovery-core] 与 [实际前后诊断][fence-recovery-proof] 记录header-only EOF、body、CRLF、缩进、quote/list相邻兄弟及closed fence边界。普通未闭合fence的context和失败回退使用完整首行/拥有的物理代码行，context仍为精确原始子串，快照/诊断保原AST范围。grid cell源码不连续，context保留AST范围，抛错/无效转换保持MDNODECONV并回退到原生代码行；info由Info+Arguments全部token重建，其间空白可规范为一个空格，不复制邻格内容。此固定恢复子集不等于任意扩展的源码无损写回。

M6软件几何的 [独立前后proof][following-proof] 使用同一Headless测试DLL，在71E042为11项2通过/9失败，在A174E083为11/11、整套159/159。它覆盖同步Updated尾部几何、用户重入与LayoutPending，含默认块边距创建/裁剪循环修正；A174 proof不改记为DA11原生性能。旧版两个Updated-tail用例在追加前就触发布局循环，不能单独证明追加时序问题；早期非法zero-renderer和编译失败后缓存DLL结果继续排除。DA11整套159项另由840 Verify复测；DA11正式第一轮已完整失败，长流UI+同步布局P9587.2475ms、Complete最大2612.5187ms超原预算，第二至第五轮未启动，M6仍进行中，不将MD/MT整行写为全部通过。

此前71E0阶段首套Verify在 [Themes XML][theme-adapter-failure] 记录6项通过、2项旧适配器cleanup errors、0产品断言失败，实际退出1并保留原结果。Themes随后仅迁移7个UI入口到同一 [公开会话包装][theme-headless-entry]，纯程序集依赖Fact与所有原断言保持不变，该DA11 Windows Verify已8/8通过。

M6原生pilot揭示直接跳转偏移后旧呈现范围导致空白，71E0仅补Offset变更时立即同步实际视口，原阅读修订条件/overscan不变。[ImmediateViewportTests][immediate-viewport-tests]的10,000块顶部/中部/末尾跳转只执行一次目标UpdateLayout，按真实Bounds计数；[修正前后][immediate-viewport-proof]前3项visible=0全失败，后3项全通过（visible10/9/9，realized12/13/11≤visible+16），亦含在当前164项。它不证明原生帧时延、五轮预算或官方性能相当。

此前DA11同源版本 [Linux SDK完整记录][linux-current-build] / [回归汇总][linux-current-summary] 已实际locked restore/全slnx Release0 warning/error、11项目840/840、0 errors/failed/skipped/notRun，含Core184/Headless159。源清单`418323DE…`对应 [Windows DA11冻结来源][linux-da11-source]；Linux功能/主题/TextMate/原生宿主哈希前缀为`6C30CB60…`、`E98394C6…`、`7A226EDB…`、`29FD4C13…`，由真实Linux SDK重新编译，不复用Windows assets/程序集或旧Linux808。此前 [71E0 Linux808][linux-viewport-build]、[1B65 Linux805][linux-final-fixture] 和 [旧Linux原生记录][linux-viewport-native] 保留历史。[当前Linux原生Demo][linux-current-native] 40项页面/服务/图片协议true、PNG已目视；原剪贴板没有可读取文本，因此保留并跳过复制，clipboard测试执行=false、整体false、退出1、error=null。它不是43项通过，也未证明Linux真实复制/恢复；辅助工具、物理输入、DPI与麒麟仍待验。

旧71E0/Host2370首个正式性能轮次在100KiB纯文本100样本通过后，1MiB纯文本等待实际Render超时。[原报告][minimized-formal-round]的请求与超时快照均为WindowState=Minimized，Win32已停止绘制；overallfalse/退出1保留为环境无效记录，不计正式一轮。此前Host351D修正最小化守卫后仍有正式预算失败，历史报告保留；DA11正式第一轮100长会话已因UI+布局及Complete超原预算退出1，完整失败记录保留；此前Windows0485五轮已由[独立守卫][windows-final-five-guard]通过，Linux最终五轮仍待验收；不能从阶段测试、包或历史43项功能探针提前宣布M6整体通过。

后续290D版本的 [Verify结果][post-cache-failed-verification] 已完成11项目 **865/865**，其中 [Themes10项XML][post-cache-theme-xml] 为10/10、0 failed/errors/skipped/notRun；新增 [标题与合并格几何两项][heading-table-geometry-tests] 均实际Pass。该次Verify的新增内嵌AXAML消费者不返回，最终退出1；不能用这份独立测试XML把整体Verify或76/78项消费者改记为通过。新几何记录为Headless默认主题与原生TextLayout的固定用例，不代替实际平台截图、物理输入或官方对照。

290D内嵌LF夹具精确Content Text后，InlineReader段落Measure阻塞的 [原始定位][inline-ui-stage-trace]、[最小对照][inline-wrap-infinite-cases] 和 [未成功的LineBreak私有分支][inline-hard-branch-proof] 继续保留；旧865 Verify仍退出1。后续0485已修复硬换行inline在Wrap/无限高度Measure中的非返回，并保持LF投影、Span样式、全局UTF-16范围和原生文字几何。[硬换行五Fact][hard-break-layout-tests] 使用同一测试DLL `982235EE…` 的 [290D对照][hard-break-regression-before] 在30秒外层期限超时，而 [0485修复后][hard-break-regression-after] 为5/5、Exit0、0错误/失败/跳过；具体 [XML][hard-break-regression-xml] 覆盖连续/首尾LF、宽窄回排、嵌套样式、Unicode/指针键盘链接、CRLF与reader复用。该五项已包含在此前0485的Headless164中，完整870与新76/78消费者另已复验；不回写旧失败，不把诊断或模型完成称作原生性能通过。

## MD01–MD19 对照登记

| 编号 | 公开资料确认的参考能力 | 本库实际实现 | 本库证据与尚未通过的边界 | 官方对照 |
| --- | --- | --- | --- | --- |
| MD01 | `Text`、AXAML内嵌文本、绑定；`xml:space="preserve"`。[总览](https://docs.avaloniaui.net/controls/data-display/text-display/markdown) | `[Content] Text`、null/空文档、完整替换、MVVM绑定；Usage含编译绑定和内嵌文本两个Tab | [P][parser-tests]/[B][binding-tests] 模型与绑定；前次EDB5 [Function76][active-function-consumer] / [Theme78][active-theme-consumer] 全部通过，各18项验证LF/CRLF compiled AXAML内嵌文本、XML归LF、Text→Source→逻辑投影、heading/list/软硬换行/全文选择。290D硬inline阻塞及旧865失败保留；[五Fact同DLL前后][hard-break-regression-after] 与后续896整轮已复验修复，监督记录noTimeout；此范围不替代全部平台或官方行为对照 | 本库固定接入与内嵌换行通过；官方行为未对照 |
| MD02 | 标题、强调、分隔线、行内代码。[总览](https://docs.avaloniaui.net/controls/data-display/text-display/markdown) | 自有块/行内 DTO 与原生 TextBlock/Span；实体/转义、软/硬换行分别投影，h1–h6 有语义类和默认字体资源 | [P][parser-tests] 常规语法/源span黄金用例、[T][theme-tests] h1/代码及行内字体、[HT][heading-table-geometry-tests] 六级同文默认字号32/26/22/19/17/16、SemiBold、原生TextLayout与实际Bounds高度递减、布局无重叠、Source/全文复制保留，见 [Themes10 XML][post-cache-theme-xml]；[N][native-results] DA11阅读/布局/复制子集通过。新六级标题是290D阶段Headless固定几何；全部语法/字体/DPI组合与实际平台视觉仍待验，硬换行修复已由[五Fact差分][hard-break-regression-after]及MD01新消费者通过复验 | 官方行为未对照 |
| MD03 | 有序/无序列表、嵌套引用。[总览](https://docs.avaloniaui.net/controls/data-display/text-display/markdown) | 保存起始编号、松紧元数据和层级，列表marker与正文并列呈现，引用有独立边框/资源 | [P][parser-tests]/[SP][structure-tests] 结构与复制次序；[EP][extended-tests] 真实紧松间距、嵌套代码/marker投影通过，完整深层视觉组合待验 | 官方行为未对照 |
| MD04 | 代码语言与 fence 后续参数保存。[12.3.0](https://www.nuget.org/packages/Avalonia.Controls.Markdown/12.3.0) | fenced/缩进代码、language/info、等宽原文、横向滚动、独立代码复制按钮；保留代码末尾 LF，CRLF 规范为 LF | [P][parser-tests] 空白/fence info、[FR][fence-recovery-tests] EOF转换器恢复8项/closed fence不变；grid context保AST范围、失败回原生代码行、info以Info+Arguments重建且不包含邻格，其间空白可规范；[I][interaction-tests] 代码按钮复制及输入隔离、[X][textmate-tests] 未知语言降级通过；Windows固定单长代码块布局/选择预算由[五轮守卫][windows-final-five-guard]通过，Linux最终五轮待验 | 官方行为未对照 |
| MD05 | 链接、自动链接、title，脚注定位与外部 fragment 分开。[12.3.0](https://www.nuget.org/packages/Avalonia.Controls.Markdown/12.3.0) | URL/title、BaseUri、宿主 LinkClicked、Tab/Enter、hover/visited；脚注内部定位；离屏逻辑链接 Invoke/Bring/focus | [P][parser-tests]/[I][interaction-tests] 链接语义/导航，[IN][navigation-tests] 实际脚注坐标跳转及替换清除旧链接焦点，[AT][automation-tests] 离屏 Invoke/可滚达，[LR][link-resolution-tests] 相对/绝对别名visited、换基址/宿主重入/追加通过，[N][native-results] 宿主事件通过；系统外链打开器和全部 fragment/BaseUri 组合仍待验 | 官方行为未对照 |
| MD06 | 默认 pipe/grid table 解析。[总览](https://docs.avaloniaui.net/controls/data-display/text-display/markdown) | 表格行/单元格、列对齐、跨度元数据、原生 Grid/横向 ScrollViewer；复制用 Tab/LF，跨列/跨行空槽保留 | [P][parser-tests]/[SP][structure-tests] grid rowspan首/中/尾、ragged/空pipe行、越宽列投影，[R][reader-tests]/[I][interaction-tests] 横滚/对齐/跨块拖选通过；[HT][heading-table-geometry-tests] 同一三列grid的中列rowspan2与末行colspan3，默认主题实际Grid宽高/边界、280DIP窄窗口横滚、右下单元格局部模拟指针选择、全局UTF-16/原生范围几何与复制空槽，见 [Themes10 XML][post-cache-theme-xml]。该290D阶段固定Headless用例不代替原生桌面截图、物理命中、DPI或任意合并格组合，完整平台视觉矩阵仍待验 | 官方行为未对照 |
| MD07 | 任务列表和五类提示；装饰为参考方呈现内容。[12.3.0](https://www.nuget.org/packages/Avalonia.Controls.Markdown/12.3.0) | 只读任务CheckBox role/value，无Toggle；五提示标题/彩边/矢量图标，不写回源码 | [P][parser-tests]/[T][theme-tests] 投影/资源，[AT][automation-tests] 只读provider，[EP][extended-tests] 五类实际图标且标题/正文可选通过；真辅助工具与完整嵌套视觉待验 | 官方行为未对照 |
| MD08 | 脚注重复引用、Emoji 与扩展强调。[序列化说明](https://docs.avaloniaui.net/controls/input/text-input/richtexteditor/markdown-serialization) | 脚注编号/label、Emoji、strike/insert/mark/sub/sup；insert 下划线，mark 默认前景色 | [P][parser-tests]/[SP][structure-tests] 首次引用次序/排除 backlink 空段/嵌套定义，[C][chunk-tests] 分块等价，[IN][navigation-tests] 脚注定位通过；全部扩展视觉仍待验，symbol 未确认/未实现 | 官方行为未对照 |
| MD09 | 默认 loader 未设，控件级继承、单元素覆盖、后设置重载。[图片加载](https://docs.avaloniaui.net/controls/data-display/text-display/markdown/imageloader) | 默认null，HTTP(S)/文件/avares；继承/ElementServices/样式/局部null、Reload/BaseUri/租约/失败事件；统一相对地址resolver | [ES][service-tests]/[LC][loader-contract-tests]/[LR][link-resolution-tests] 服务/输入/基址固定实例通过；[N][native-results] DA11实际file/avares/回环HTTP PNG、编码/解码后像素限额、原生租约释放与局部null通过；公网/长期压力仍待验，解码后限额不保证峰值内存 | 官方行为未对照 |
| MD10 | 独立高亮适配，参考方提供 TextMate/ColorCode。[高亮说明](https://docs.avaloniaui.net/controls/data-display/text-display/markdown/codehighlighter) | IMarkdownCodeHighlighter、可选 TextMate、元素局部/继承/null、失效通知及纯文本降级；Demo跟随主题 | [X][textmate-tests] 13项真实 tokenizer/主题/取消/共享/未知语言，[ES][service-tests] 局部覆盖/迟到格式/共享失效/卸载订阅通过；[ER][error-recovery-tests]null/异常枚举保纯代码与迟到拒绝通过，任意宿主返回与更多语言视觉待扩测，ColorCode属后续 | 官方行为未对照 |
| MD11 | 全选/清除、选择及复制事件；参考 `CopyAsync(): Task<bool>`。[控件 API](https://docs.avaloniaui.net/api/avalonia/controls/markdown) | 统一字素逻辑选区、SelectedText/CanCopy、鼠标/键盘/越界拖选、可取消复制；本库 MarkdownCopyResult 区分失败 | [S][selection-tests]/[R][reader-tests]/[I][interaction-tests] 迁移/离屏/跨块，[IN][navigation-tests] Page/Shift/Home/End/Ctrl+A/C/Escape/越界自动滚及释放，[N][native-results] 真实剪贴板恢复通过；模拟输入不代替物理输入/触屏/全部平台组合 | 官方行为未对照 |
| MD12 | 跨线程会话、内存追加、最终布局等待、UI 事件。[会话 API](https://docs.avaloniaui.net/api/avalonia/controls/markdownstreamingsession) | 完整后台解析/差异呈现、合并输入、单解析/单最新请求、有效前缀单调提交、明确完成/取消/隐藏/未挂载 | [Q][scheduler-tests]/[C][chunk-tests] 41项，[SL][lifecycle-tests] 5项重配置/取消/结束保留尾部，[B][binding-tests]/[U][consumer-results]/[N][native-results] 绑定/原生子集；非增量解析，Windows固定短流/1MiB长会话已[正式五轮通过][windows-final-five-guard]；Linux最终五轮待验 | 官方行为未对照 |
| MD13 | 接近底部跟随，上滚暂停、回底恢复。[控件 API](https://docs.avaloniaui.net/api/avalonia/controls/markdown) | 默认false、24DIP阈值；逻辑字素/像素锚点和块fallback；恢复单排队/单最新、weak owner及代次/用户阅读守卫 | [A][anchor-tests]/[N][native-results] DA11跟随/图片固定子集；[LA][layout-anchor-tests] 长段落字体/窄视口/动态资源、同block图片、direct helper、排队重排和用户导航8实例通过；[FC][following-proof] A174同步尾部几何/重入/LayoutPending 11项前后，DA11整套159另复测；新行包含旧字素且Y保持，行首可自然变化。Windows正式五轮预算已[通过][windows-final-five-guard]；M6整体仍等Linux最终五轮，复杂表格/全部输入/新设备组合另验 | 官方行为未对照 |
| MD14 | 可样式化块/行内、层级选择器、命名资源。[样式说明](https://docs.avaloniaui.net/controls/data-display/text-display/markdown/markdown-styling) | 真实MarkdownBlockPresenter/Span/Run、独立主题/命名资源，应用级及reader.Styles局部语义选择器 | [T][theme-tests] 8项含挂载后层级Span规则增删、字体/颜色/布局变化与选区保留，[LA][layout-anchor-tests] 字体/资源/宽度变化字素阅读保持通过；固定选择器不证明任意规则均已验收，官方选择器不能直接复用，完整DPI/视觉矩阵待验 | 官方行为未对照 |
| MD15 | 实例 pipeline 配置入口。[总览](https://docs.avaloniaui.net/controls/data-display/text-display/markdown) | ConfigurePipeline、ConfigureParser(MarkdownParserOptions)、类型安全 Converters、不可变 NodeRenderers；metadata/SemanticKind、原生helper与固定降级 | [NC][converter-tests]/[NP][native-converter-tests] 语义/结构/分块；[FR][fence-recovery-tests] EOF转换失败恢复8项及grid连续源范围例外；[NR][renderer-tests]11几何/资源/降级，[CR][container-renderer-tests]12默认表格/代码/行组合，[SL][lifecycle-tests] 尾部，[U][consumer-results] 最终新API包消费通过；[ER][error-recovery-tests]资源抛错逐个清理/重入失效/降级诊断已验，不据parser扩展声明数学/Mermaid/HTML已有renderer | 官方行为未对照 |
| MD16 | 资料提及延迟节点实现/差异更新。[会话 API](https://docs.avaloniaui.net/api/avalonia/controls/markdownstreamingsession) | 顶层高度索引、普通边界前后各2块预取/代码边界不预取、离屏移除、空回收池；统一模型选择不依赖 Presenter，未变内容复用 | [R][reader-tests] 千块离屏全文/已呈现上限观察、[A][anchor-tests] 未变图片复用、[IV][immediate-viewport-tests] 10,000块跳转一次布局3实例（实际可见>0、呈现≤可见+16）、[性能方法](markdown-performance.md) 10,000 块纯模型测量；**长块行级布局/缓存未实现**，Windows10,000块回收/滚动与固定UI预算已有[五轮守卫通过][windows-final-five-guard]；Linux最终五轮待验 | 官方行为及性能未对照 |
| MD17 | 后台 parse/diff/snapshot，UI 最终应用。[会话 API](https://docs.avaloniaui.net/api/avalonia/controls/markdownstreamingsession) | 文档/配置/会话守卫、UI apply、卸载/模板失效；宿主服务不被Dispose，owned租约释放；资源实际执行4/等待256有界 | [Q][scheduler-tests]/[RS][resource-tests] 调度与非合作取消边界，[ES][service-tests]/[IN][navigation-tests] 共享服务/模板与卸载捕获，[AT][automation-tests] 20次peer裁剪、12次重挂单通知与本地队列后reader/peer GC，[RL][resource-lifetime-tests] 非合作图片/高亮仍运行时卸载reader可回收、迟到租约释放通过；Windows五轮每轮100次资源与虚拟化固定门槛已有[守卫通过][windows-final-five-guard]；Linux最终原生、长期峰值及更长周期存活对象仍待验 | 官方行为未对照 |
| MD18 | 原生 Avalonia 文本排版。[总览](https://docs.avaloniaui.net/controls/data-display/text-display/markdown) | 原生 TextLayout/字体/字素输入；Document只读全文 IValueProvider、lazy离屏逻辑角色、Link Invoke、只读Task状态；C#范围绑定快照/代次 | [AT][automation-tests] 12项全文/角色/离屏/范围/失效/GC与原生几何，[S][selection-tests]/[I][interaction-tests]/[N][native-results] 字素及Windows scale1子集通过；Avalonia12.0.3无公开ITextProvider/ITextRangeProvider，**不支持原生UIA TextPattern**，C#范围不替代它；真辅助工具/RTL视觉/DPI/麒麟待验，见 [支持级别][automation-contract] | 官方行为未对照；未声明完整平台支持 |
| MD19 | 官方入口/主题打包于官方包。[固定包](https://www.nuget.org/packages/Avalonia.Controls.Markdown/12.3.0) | 功能/主题/可选TextMate三包，统一Demo/真实AXAML Usage、专属测试/原生基准/公共契约/API快照 | 前次EDB5 [Function76][active-function-consumer] / [Theme78][active-theme-consumer] 全新缓存/无源码引用，服务/扩展/geometry/automation/URI及18项内嵌AXAML全部通过；[完整Verify896][active-verification]含既有416回归、七包与所有消费者，当前API公开签名相对0485无变化。旧0485/DA11证据保留，新不可变软件归档、正式原生预算与设备另验 | 本库当前独立消费与软件整轮通过；官方行为未对照 |

## 本库证据索引

下列字母只定位本库用例。前次EDB5的Markdown完整Verify为480项（整仓896项）；此前0485的454项（整仓870项）保留历史；DA11的424/840及290D的865失败分别保留。固定子集不能替代每行视觉、性能或平台验收。

| 代号 | 源码或报告 | 范围 |
| --- | --- | --- |
| P / S / SP / NC / NP / TR | [Parser][parser-tests] / [Selection][selection-tests] / [LargeMigration][large-migration-tests] / [Structure][structure-tests] / [Converter][converter-tests] / [NativeProjection][native-converter-tests] / [Traversal][traversal-tests] | Core207项：模型/投影/身份/字素、扩展/内置语义及深树遍历等 |
| FR | [FenceConverterRecovery][fence-recovery-tests] / [前后诊断][fence-recovery-proof] / [隔离Core XML][fence-recovery-core] | EOF首行/正文/CRLF/缩进、quote/list兄弟、closed fence与grid例外8项，已含当前Core207 |
| Q / C / RS | [Scheduler][scheduler-tests] / [Chunks][chunk-tests] / [ResourceScheduler][resource-tests] | Streaming60项：原解析/分块41项与资源调度19实例 |
| R / I / B / A | [Reader][reader-tests] / [Interaction][interaction-tests] / [Binding][binding-tests] / [Anchor][anchor-tests] | 原Headless35项的布局/样式/绑定/模拟输入 |
| ES / NR / AT / IN / SL | [ElementService][service-tests] / [NodeRenderer][renderer-tests] / [Automation][automation-tests] / [Navigation][navigation-tests] / [Lifecycle][lifecycle-tests] | 服务8/renderer11/automation12/导航7/生命周期5实例，包含在此前0485的Headless164项 |
| EP / LA / LF / RL / LC | [ExtendedPresentation][extended-tests] / [ReadingLayoutAnchor][layout-anchor-tests] / [LinkFocus][link-focus-tests] / [ResourceLifetime][resource-lifetime-tests] / [ImageLoaderContract][loader-contract-tests] | 扩展布局8/字素阅读8/链接焦点3/非合作服务GC2/loader契约6实例，已含在Headless159项中 |
| LR | [LinkResolution][link-resolution-tests] | 6实例实际通过：resolved visited别名/基址/宿主重入、回收/追加及图片root/authority |
| CR / PG / UD / PL / ER | [CustomContainerRenderer][container-renderer-tests] / [PageGeometry][page-geometry-tests] / [UiDispatchLifetime][ui-dispatch-tests] / [ParserLifetime][parser-lifetime-tests] / [ErrorRecovery][error-recovery-tests] | 容器默认呈现12/页距5/UI范围与旧错误10/parser阻塞期间GC1/错误恢复6，包含在Headless159项中；测试输入范围以公开ExecutionContext.Run平衡，产品sentinel保留 |
| FC | [FollowingDocumentCommit][following-tests] / [A174阶段proof][following-proof] | 软件helper几何11项：71E042为2通过/9失败，A174为11/11和整套159；DA11当前Verify另复测159，独立前后proof不改记为DA11原生预算 |
| T | [Theme][theme-tests] | Themes 10 项；真实资源/节点与模板，含挂载后局部层级规则增删 | 当前10项见[本轮XML][current-theme-final]。
| X | [TextMate][textmate-tests] | TextMate 13 项；真实 tokenizer 与主题 |
| N | [当前Windows EDB5原生结果][final-windows-smoke] / [监督][final-windows-smoke-supervisor] / [来源][final-windows-smoke-source] | 2026-10-15当前候选43项true、Exit0/noTimeout、error=null，包含真实文本复制/读回/原文本恢复、原生几何与局部服务、五图标及file/avares/回环HTTP限额/租约，PNG已目视；旧DA11/0485各自保留。物理输入/辅助工具/DPI/麒麟仍未验证 |
| LN | [当前真实Linux SDK][active-linux-build] / [896汇总][active-linux-regression] / [483源清单][active-linux-source] / [跨平台软件及API比较][active-source-api-proof] / [新正式五轮来源][active-linux-formal-source] | 真实SDK locked restore/全solution Release0 warning/error、11项目896通过；仅两个validation脚本修正后与Linux输入分别记383相同+2差异。Linux新五轮已由[修正守卫][active-linux-five-guard]实际通过，旧失败守卫保留，当前Windows新五轮首轮预算失败、后轮停止；旧0485的870/40项smoke与97e正式失败、DA11各历史结果保留。WSLg/X11不代替麒麟、设备或剪贴板验收 |
| U | [当前消费汇总][active-consumer-summary] / [Function76][active-function-consumer] / [Theme78][active-theme-consumer] / [增强脚本][consumer-script] | EDB5本轮76/78全部通过，各18项compiled AXAML内嵌文本、服务/扩展/geometry/automation/URI；两监督noTimeout、Exit0。新不可变归档待收束；[0485软件归档][current-software-archive]及[旧DA11 58/60][consumer-results]保持历史 |

## M0 固定的 symbol 与 HTML 边界

官方主页面称“built-in symbol extension”，未列语法。后续序列化专项页称 Markdig 自有 symbol 扩展，但没有公开具体映射、解析器版本或可运行结果。Markdig 0.42.0 的公开 `MarkdownExtensions` 包含 Emoji/Smiley 与 SmartyPants，未提供名为 `UseSymbols` 的入口；因此不能从这句话推断 symbol 是 Emoji、SmartyPants 或同一组替换。首版 symbol 保持**未确认 / 未实现**；保留输入文字，不自行用一组推测映射替代。[专项说明](https://docs.avaloniaui.net/controls/input/text-input/richtexteditor/markdown-serialization)、[固定 Markdig 扩展源码](https://github.com/xoofx/markdig/blob/0.42.0/src/Markdig/MarkdownExtensions.cs)

官方 12.3.0 使用专门 HTML 节点保留原文，可将 HTML 转为文档元素，不认识的行内标签仍被保留；未闭合标签不应丢掉后续段落。本库首版默认将 HTML 作为文字保留，不执行 HTML/CSS 布局；M0 不启用隐含标签白名单。任何后续 `<br>` / `<sub>` 支持都须有独立呈现与复制用例后再登记。该差异不影响常规 Markdown，但意味着 **HTML 未完全对齐**。[HTML 说明](https://docs.avaloniaui.net/controls/input/text-input/richtexteditor/markdown-serialization)

## API 与样式迁移边界

本库使用自己的不可变快照、选区和服务接口；`Document` 不是官方可编辑 `FlowDocument`，不能直接调用官方节点/组件扩展。编辑、Markdown 写回、分页、打印与 PDF/Word 导出留作后续；保存 `Text` 不构成模型序列化。[官方文档集成](https://docs.avaloniaui.net/controls/input/text-input/richtexteditor/markdown-serialization)

官方 typed heading/alert/task 节点继续使用基类型样式键，官方保留 MarkdownCodeBlock/MarkdownImage 自身样式键，旧 alert-header 规则在12.3.0删除。[固定发行说明](https://www.nuget.org/packages/Avalonia.Controls.Markdown/12.3.0) 本库块使用MarkdownBlockPresenter及语义类（heading/h1–h6、alert/note等），行内使用MarkdownSpan/MarkdownRun，代码/图片使用本库MarkdownCodeBlock/MarkdownImage；资源均有Markdown前缀。官方Paragraph.h1、Section.alertBlock、ListItem选择器不能仅换命名空间，需按 [公共契约](markdown-contracts.md) 和 [样式说明](markdown-usage.md) 迁移。

主页面将 `CanCopy` / `SelectedText` 写为只读，自动 API 页面生成的部分签名带 `set`；本库发布只读选择派生属性，未宣称复制此 setter。官方主页面 API 摘要没有列出 `CopyAsync` 和控件级服务，基线采用专项页与固定包的补充内容。[总览](https://docs.avaloniaui.net/controls/data-display/text-display/markdown)、[API](https://docs.avaloniaui.net/api/avalonia/controls/markdown)

M0 尚无同机官方执行环境，不能声明二进制、源码、视觉或性能兼容。后续对照必须同时记录版本、语料、配置和具体观察；本库自动化通过也不改写该列为“官方行为一致”。

此前0485的 [API归档](../artifacts/markdown-release-evidence/f7c55c3a385a49fbb3ab7da96dc064bc/api/markdown-public-api.txt) 由 [0485实际导出helper][current-api-export-helper] 从Release生成，功能/主题/TextMate SHA分别为`04856FD6…`、`9D4C5D0A…`、`69F2CB47…`；[旧DA11 API归档][prior-da11-api]与旧包证据保持历史。Headless快照不代替真实剪贴板、麒麟或正式原生性能。

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
[container-renderer-tests]: ../tests/Markdown.Headless.Tests/CustomContainerRendererTests.cs
[page-geometry-tests]: ../tests/Markdown.Headless.Tests/PageNavigationGeometryTests.cs
[ui-dispatch-tests]: ../tests/Markdown.Headless.Tests/UiDispatchLifetimeTests.cs
[parser-lifetime-tests]: ../tests/Markdown.Headless.Tests/ParserLifetimeTests.cs
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
[native-image]: ../artifacts/platform/windows-markdown-smoke-20261014-following-recovery.png

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
