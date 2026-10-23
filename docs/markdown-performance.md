# Markdown 性能方法与结果

当前M7可执行软件范围已通过，见[本次M7收束记录](markdown-m7-completion-20261015.md)及[新M7执行记录](../artifacts/markdown-m7-completion/c63fab7542ba4b22b9a63d9cbb736f1b/m7-exit.json)。Windows43项与Linux私有Xvfb/X11新43项真实复制、读回及恢复通过，实际producer/Main/外层均退出0；原WSLg共享显示的安全Pending保持历史记录，两个环境分别记账。

Windows50ms、Linux51ms及身份、引用、脚注语义不变；本次增补按同一冻结源码和产物SHA复用五轮、544×2、416×2、包/API及85/134工具合同，不声称再次执行。远端原生CI由用户暂缓（DeferredByUser）；麒麟、实体Linux/原生Wayland、完整DPI/多屏、物理输入和真实辅助工具继续待验证。未发布、未改版本。

## 此前冻结候选与阶段记录

以下保留当时结论及原始来源，当前结果以上方revision2记录为准。

基线日期：2026 年 10 月 13 日。工程预算来自 [开发计划](markdown-development-plan.md)，不是测量结果。M0 五轮基线只测后台完整解析与本库归一化快照/逻辑复制投影。2026-10-14 增加模型身份更新及原生呈现入口，并首次冻结长单块与1MiB长会话工程门槛。2026-10-15当前Windows与Linux各五轮原生守卫已通过，分别适用原50ms与用户明确修订的长会话UI51ms政策；历史原生失败保持原样。134项修订守卫合同已实际通过；M6在本轮已测软件与原生组合范围内通过，M7软件验收及归档已完成，缺设备矩阵仍待验证，版本未发布。

2026-10-15当前冻结软件证据已更新：[Windows完整Verify d2](../artifacts/verification/d2f591e51482493c9b62b4f642af789e/results.json)实际passed=true、**907/907**、387文件启动/结束相同，manifest SHA `B5DD8FEDBE203A7CBF84D2841CC674331B700E70B51373244A52B9E156AB3D69`；[实际Linux SDK6451](../artifacts/linux-build/6451a248de3948f8baab4ee5f6ba0e0c/result.json)11项目通过，[直接日志审计](../artifacts/linux-build/6451a248de3948f8baab4ee5f6ba0e0c/regression-summary.json)同为907/907、0 failed/errors/skipped/notRun。[最终软件/API比较](../artifacts/markdown-candidate-landings/664238a5d9754cf6af315cde811636d3/final-source-and-api-comparison.json)逐文件387全部相同、无例外，Linux完整输入485文件；API SHA `2EC61F47FDCD9B33C637967BF86902722343254D6B1C9A9CCC32D51953890D7D`、公开成员签名差异0。新[FunctionOnly76](../artifacts/consumer/c3d55208a91d405cb0223057d50c676c/result.json)/[Theme+TextMate78](../artifacts/consumer/af58e3a2f38b443c92c659e9e78bd346/result.json)各18项内嵌AXAML全部true。三包/API/消费证明已[独立冻结08ba](../artifacts/markdown-m7-preparation/08ba3007b2fd4d09bf73a98cc62d4763/small-freeze-manifest.json)，保持实际GUID与SHA，不使用私有84A6替代正式产品。

Windows正式F832版本的[五独立进程守卫](../artifacts/benchmarks/markdown/windows-formal-f8324d-fcba9d4c612443c0bb682cd462532d5/guard.json)实际全部通过，保留原**50ms**门槛及各100样本/100短长会话、10预热；[五轮派生摘要](../artifacts/benchmarks/markdown/windows-formal-f8324d-fcba9d4c612443c0bb682cd462532d5/final-five-round-summary.json)全部实际提交UI+同步布局P95为42.3460/25.9897/24.8804/24.6516/24.1691ms，长Complete P95为989.5465/598.5500/781.3140/523.0250/546.3653ms、五轮最大1486.1136ms。它是正式Windows该四DLL的通过证据，不转签不同Linux DLL。并行PDF工作在Verify后修改了[九份live源码](../artifacts/markdown-candidate-landings/664238a5d9754cf6af315cde811636d3/concurrent-workspace-changes-after-verify.json)，全部保留；d2证明对应其冻结版本，不宣称现在整个工作区仍等于d2。M7另存[08ba冻结源码与产物清单](../artifacts/markdown-m7-preparation/08ba3007b2fd4d09bf73a98cc62d4763/final-freeze-manifest.json)和[Demo来源](../artifacts/markdown-m7-preparation/08ba3007b2fd4d09bf73a98cc62d4763/demo-provenance.json)，使用原兼容Demo与正式验证的三产品组合，未使用并行PDF版Demo；[本轮软件证据归档](../artifacts/markdown-release-evidence/13c8c03113e64064ba54b7580cb620fb/manifest.json)已实际完成，Published=false、VersionChanged=false。

新Linux A50F/Host2905版本的[正式轮次来源](../artifacts/linux-probe/dfc8c8cdb68349dda6f37615eea4c319/source-snapshot.json)未完成原50ms五轮：前三份raw全部true，ALL实际UI P95为47.8740/47.3905/48.5603ms；[第四轮raw](../artifacts/linux-probe/dfc8c8cdb68349dda6f37615eea4c319/report-4.json)error=null但overall=false，3669实际提交P95 **50.6962ms超过50ms**，原检查“长会话提交UI预算”及长流至帧场景false。驱动实际退出1并停在第四轮；generic“pilot failed”文字不改变raw的正式100样本/100会话、10预热性质。原始四份raw与原判定不改写，[离线派生摘要](../artifacts/linux-probe/dfc8c8cdb68349dda6f37615eea4c319/derived-08a1d80ad2f34108a1468bb4097fa435/summary.json)及[逐提交关联CSV](../artifacts/linux-probe/dfc8c8cdb68349dda6f37615eea4c319/derived-08a1d80ad2f34108a1468bb4097fa435/all-actual-commits-joined.csv)保留各源版本、长度、Parse/UI应用/Layout和Rendered关联。四轮每轮9200接收记录与全部实际提交均无重键、缺失、UTF16/UTF8长度错配；Rendered阶段数值精确0差，资源100次替换后必需计数全0，队列无1/1/4/256违例，短/长Complete全部100项分别在500/1500ms内。

最新用户明确授权仅将**长会话UI+同步布局P95的ALL实际提交及Rendered子集两口径，从50ms调整为51ms（+1ms，+2%）**；[原始用户授权策略](../artifacts/markdown-budget-revision/user-authorized-policy-20261015.json)及[修订记录](../artifacts/linux-probe/dfc8c8cdb68349dda6f37615eea4c319/derived-08a1d80ad2f34108a1468bb4097fa435/budget-amendment-record.json)保留原producer50判定与R4失败。独立守卫默认仍50，51必须显式选择；其余预算、100/10采样、重复节点身份/引用/脚注和全部语义断言不变。随后同四DLL的[第五独立进程](../artifacts/linux-probe/dfc8c8cdb68349dda6f37615eea4c319/report-5.json)完成，Linux[显式51五轮独立守卫](../artifacts/linux-probe/dfc8c8cdb68349dda6f37615eea4c319/guard-policy-51.json)实际全部通过，五轮ALL实际及Rendered UI P95均分别为47.8740/47.3905/48.5603/**50.6962**/49.3257ms。守卫逐份记录raw SHA、原producer预算/两处判定/overall，并核对它们与raw重算一致；R4原overall=false仍保留，不能写成原50ms五轮通过。Windows五轮继续引用其原50ms通过结果。新守卫保留原109用例与新增25例，[134项合同实际结果](../artifacts/markdown-budget-revision/guard-contract-20261015-715d222931f642219117de5749ec413f/results.json)全部通过，[完成执行绑定](../artifacts/markdown-budget-revision/guard-contract-20261015-715d222931f642219117de5749ec413f/completed-execution-binding.json)记录Exit0/noTimeout及源码前后SHA不变。Linux[最终五轮派生摘要](../artifacts/linux-probe/dfc8c8cdb68349dda6f37615eea4c319/final-five-round-policy-51-summary.json)实际完成；其权威仍为对应五轮独立守卫。

守卫与测试已按[经测试SHA落地清单](../artifacts/markdown-budget-revision/landed-20261015-6f46fd660c214d09a14ab14a2a719bfe/source-transition.json)更新，守卫SHA为`B5160B634338CAF6ED40AF702DA12CE7155BB61E151F6E919D0A50165453010A`。此变动仅为validation政策，默认50/显式51；907回归仍绑定原387/B5冻结源码，不倒签为新脚本已在旧Verify运行，产品/NativeHost四DLL不变。[软件归档](../artifacts/markdown-release-evidence/13c8c03113e64064ba54b7580cb620fb/manifest.json)与[独立51政策附录](../artifacts/markdown-release-evidence/13c8c03113e64064ba54b7580cb620fb/policy-support-eb118238cf2b46b2817b815849dd62a7/policy-support-manifest.json)实际完成，保留387来源、134合同、原50失败与新51判定；Published=false、VersionChanged=false。

本轮原生功能另有独立证据：Windows F832[43项报告](../artifacts/markdown-m7-preparation/08ba3007b2fd4d09bf73a98cc62d4763/windows-smoke-ed4fe48322da4558b2b239c671918809/report.json)及[监督](../artifacts/markdown-m7-preparation/08ba3007b2fd4d09bf73a98cc62d4763/windows-smoke-ed4fe48322da4558b2b239c671918809/supervisor-result.json)实际43/43、Exit0/noTimeout；Linux A50F真实SDK Demo的[43项证据](../artifacts/platform/linux-markdown-smoke-a50f-00ca24d5178d454eb29e563d8887c157/evidence-summary.json)同为43/43、Exit0。两平台真实剪贴板复制、读回与原文本恢复均true；Linux新PNG已目视，旧7B29不可读剪贴板的安全跳过失败仍保留。该smoke验证原生Window与程序路由输入，其PNG是RenderTargetBitmap，不是桌面截图。实体Linux、原生Wayland、物理输入、系统外链、辅助工具、完整DPI及麒麟仍Pending，不由本轮性能或smoke代替。

下文保留各历史版本当时的失败、通过与未完成状态；本轮当前产物与验收进度以上述F832/A50F记录为准，不将历史版本中的“待取得”或旧剪贴板Pending转用于当前产物。

最新联合候选已由[6642落地清单](../artifacts/markdown-candidate-landings/664238a5d9754cf6af315cde811636d3/source-transition.json)将5个产品文件及3个测试文件逐字节转入当前源码，包含纯身份索引预建与单次CreateTextLayout内属性共享。[私有冻结整仓回归](../artifacts/markdown-combined-candidate/c3dfbd83f87e4676b546f5daa0feb53e/regressions/434946cbf7a24188822778aa78694259/results.json)11项目**907/907**、0 failed/errors/skipped/notRun：Core221、Streaming60、Headless187、Themes10、TextMate13共491，加既有416；它不含包消费、guard合同或正式原生性能。新增[身份预建7项](../tests/Markdown.Core.Tests/MarkdownIdentityPrewarmTests.cs)保留重复旧ID首次前序解析、重复当前ID的原有异常、零位置重叠最短范围与同长首项，并验证预建后的迁移/changes及GC；重复节点身份、引用和脚注语义的既有断言与预算均未放宽。

[同一测试DLL字体差分](../artifacts/markdown-combined-candidate/c3dfbd83f87e4676b546f5daa0feb53e/build-and-font-differential-result.json)使用公开同键替换字体集合：旧EDB5为4项中3失败，[原失败XML](../artifacts/markdown-combined-candidate/c3dfbd83f87e4676b546f5daa0feb53e/font-before.xml)保留；新私有84A6为[4/4通过](../artifacts/markdown-combined-candidate/c3dfbd83f87e4676b546f5daa0feb53e/font-after-attempt-1.xml)。修复后重新Measure使用新字体，Measure后直接Arrange保留原生inline旧face、重新创建默认属性；硬换行适配后记最终run列表身份，非零LineSpacing、派生/custom类型绕过新增属性合并，既有硬换行适配仍先执行。原编译失败及失败源码清单亦保留。此差分覆盖这些明确操作，不宣称全部自定义字体provider均已验证。

同候选[Windows Full探索](../artifacts/markdown-combined-candidate/c3dfbd83f87e4676b546f5daa0feb53e/windows-full-runs/d297509cae0f4d12b31886473e80eaab/report.json)为**一个非正式进程**，七个阅读/长块组各10样本，短/长流各10会话；11组全部true、error=null，[监督](../artifacts/markdown-combined-candidate/c3dfbd83f87e4676b546f5daa0feb53e/windows-full-runs/d297509cae0f4d12b31886473e80eaab/supervisor-result.json)实际Exit0/noTimeout。460次长流全部实际提交UI+同步布局P95为**23.8619ms**，短流至帧P95114.5508ms；资源替换/卸载10次后必需计数均0，队列峰值1/1/4/0。[来源](../artifacts/markdown-combined-candidate/c3dfbd83f87e4676b546f5daa0feb53e/windows-full-runs/d297509cae0f4d12b31886473e80eaab/source.json)固定485文件、manifest SHA `FD497D1B1777757A86F4CBC81C6C6908A5DBDD10BBB20C5804C2FF3072F1B81C`、私有Core `84A63529C4A8C0029D5DD74C397539603A36CED7F8F01270631A05CEF51C0F0B`及另外三DLL。原wrapper传入的diagnostic-warmup参数仅探针host识别，当前host忽略；本轮raw各组预热实际为1，按raw登记，保留原wrapper/监督，不将其Warmups=3元数据当观测。它不能代替100样本/100会话、10预热、五独立进程的正式结果，也不能将私有DLL签给落地后的官方构建。

此前[85ce完整Verify尝试](../artifacts/verification/85ce8ecfa4fc4e93ae0620dd5aba37c6/results.json)及[Linux a747隔离还原](../artifacts/linux-build/a747737d78cf4e2b90a8b45bbf494e7c/result.json)因NuGet审计源HTTPS/TLS握手失败而未通过，原失败保留；85ce测试未开始、testTotal=null。随后官方连接恢复，1520完成907测试但consumer签名下载失败，最终d2与实际Linux6451已取得上面记录的软件通过。审计与警告未关闭，不把早期失败记录改成通过。EDB5/7B29的896项、smoke及Linux五轮仍是旧产物历史，正式F832 Windows五轮及A50F Linux显式51五轮另列；本轮134合同与M7软件归档均已完成，缺设备项Pending。

前次EDB5产品冻结构建已完成[整仓Verify](../artifacts/verification/f2013bf2f6764fe283856d0878ea1026/results.json)：passed=true、实际退出0，11项目**896/896**，Core214/Streaming60/Headless183/Themes10/TextMate13共480，加既有TreeDataGrid/VirtualKeyboard416，0 failed/errors/skipped/notRun；七包及全部独立消费者均完成。[385文件源码清单](../artifacts/verification/f2013bf2f6764fe283856d0878ea1026/source-manifest.json)SHA256 `3C38D269278E0B94019A8D08F07AC0DA7368B2822BC071E04752BBCD3CDC4140`，sourcesUnchangedDuringVerification/sourceComparisonCompleted均true。六产品及首四测试的[落地记录](../artifacts/markdown-candidate-landings/0581031e556d49e394cf377fbc92efb5/source-transition.json)另保留，最后增加[缓存文档生命周期Fact](../tests/Markdown.Headless.Tests/TextPropertyCacheLifecycleTests.cs)。该Fact仅反射本库helper、使用实际FontManager，验证旧run属性在换文档或Clear后重建、baseline/culture/装饰/brush/features不变，不操作框架内部或字体表。98项未变guard合同按Verify记录复用，不称本轮重跑。

同一新包的[FunctionOnly76](../artifacts/consumer/cfb8502b20bb4ea0870c5add1e213816/result.json)及[Theme+TextMate78](../artifacts/consumer/f092fb9ab058410492b9f2509d6dc60b/result.json)全部true，各18项内嵌AXAML LF/CRLF检查，监督均Exit0/noTimeout。功能/主题/TextMate包SHA前缀`472C711B…`/`4067FEF3…`/`0A635330…`；Windows四DLL功能/主题/TextMate/Host分别`EDB5A830376611A21DA035FCD8D55EF57997EBEBB8CB73305FA29AA7D021D87C`/`2BD47A8EDAE56907360A1595C8390916E923181F9CB9185CDAF486BDFECA1FE1`/`9CBE1284EE52ADCBC04C058CFFD6E1F20C09E42E2696E0AFCED50F1F0578019B`/`B9AADF26A6054DB1CCD6C2AA0A7CE83D6C7281573E135BD9A479744A3C271428`。最新[公共API快照](validation/markdown-public-api.txt)经[当前Release helper](../artifacts/markdown-api-helper/07031d79ad4d4042b466b4c82e24f6f0/Program.cs)生成，SHA `C8BF2413152E8263DD141CD9698F0D707647906C40C723EEE2181588235248C3`，与旧0485归档仅三DLL SHA变化、公开成员签名无差异。此软件与包/API通过不等于原生性能通过。

前次EDB5/7B29同源[真实Linux SDK完整结果][active-linux-build]已实际Exit0、全部11项目true：SDK10.0.401、locked restore、全solution Release构建3分06.30秒、0 warning/error；[直接测试日志汇总][active-linux-regression]为**896/896**，Errors/Failed/Skipped/NotRun均0，项目数量与Windows一致。[483文件Linux输入清单][active-linux-source]SHA256为`352546E1C8F380A6C66052ECCBDED1A9DF88C37226FF557EC09DAD31280EC4C2`；[脚本修正前跨平台软件/API比较][active-source-api-proof]记录当时385文件相同与公开签名无差异；后续仅两个validation PowerShell脚本修改，当前跨平台软件口径为383文件相同、两脚本差异，不能沿用旧385全等结论。产品与NativeHost的C#和四DLL未变，Linux896来自本次独立SDK实际执行；前次[385文件完整Verify][final-timespan-verification]已实际896通过，来源由[最终383+2比较][final-compiled-source-proof]补齐。

Linux功能/主题/TextMate/Host四DLL分别为`7B29D53342A03C888FE170A46F8BBDCA74F8A91C75420EBFFBA7F98E02E25048`/`CFB302DDC3290562B5038D027894B86179416345510CABE082060AE712349558`/`D30D3AE71EB754A8CF5F003BAA89C01BFE754909CC947939E7676710B0FC7D77`/`1ED252BA27771B8310BD74B21A5556E96B5BB89B31CEEE0161035AAB2EEBA9AD`，不复用Windows EDB5程序集。[新Linux正式五轮冻结来源][active-linux-formal-source]核对上述真实SDK四DLL，五原始报告已全部完成；原守卫因时间换算错误拒绝，经实际BCL核验和109合同用例后，[修正守卫的新结果][active-linux-five-guard]实际全部true、独立进程5/5。原始报告与失败守卫均保留，详见下段失败链；前次[Windows EDB5候选五轮][final-windows-five-source]首轮已完整失败，后续轮次停止，无候选五轮通过结论。环境为可用Ubuntu26.04 WSL2/WSLg、Avalonia.X11/XID，不能代替实体Linux、原生Wayland或麒麟。当前不可变软件归档与M6/M7整体退出仍待验证；旧0485的870、f7归档和Windows五轮、旧Linux97e失败均保留历史，不拼轮或转移版本。Linux剪贴板、物理输入、辅助工具、多DPI及缺设备项继续Pending。

2026-10-15时间换算核验：Linux a245五原始报告各11组全部完成，但[原独立守卫][active-linux-old-failed-guard]五轮均拒绝，错误为`stream elapsed differs from raw samples.`。问题是守卫直接按频率换算毫秒，生产者使用Stopwatch.GetElapsedTime先转换为100ns的TimeSpan；不是会话排序、样本映射或预算变更。[实际Linux BCL独立对照][active-timespan-bcl]对五轮短/长batch和Complete共25,588条逐条比较，原始值与GetElapsedTime及相同截断公式均0差异。[109合同用例][active-timespan-tests]实际全部通过，含1GHz的18/99ns余量接受、10MHz原路径接受、四种时间值漂移100ns及倒退拒绝；Same容差和全部预算原样保留。新守卫SHA为`71094D15D88B888972590879207923FB9EE6C9EACEE7ED14A3B56FB623D97205`，[独立重验结果][active-linux-five-guard]五进程全部true；[输入不变记录][active-timespan-validation]确认五raw重验前后SHA不变，原false守卫未改写。

首个差值来自a245第1轮短流batch数组第0项、session0/ordinal0：start `588932077230`、end `589012694248`、频率1,000,000,000Hz、差80,617,018 ticks。原始batch与对应NativeSample均为80.617ms，原守卫却计算80.617018ms；差0.000018ms超过原Same容差0.000000080617018ms。[.NET 10.0.12固定源码](https://raw.githubusercontent.com/dotnet/runtime/v10.0.12/src/libraries/System.Private.CoreLib/src/System/Diagnostics/Stopwatch.cs)将tick差乘TimeSpan.TicksPerSecond/Frequency后向零截断，得到806,170个100ns tick，即80.617ms。修正只让四处短/长batch与Complete预期换算遵循同一BCL步骤；PowerShell先Math.Truncate再转long，不能用会四舍五入的直接long转换。没有扩大浮点容差、减去暂停、重写raw或放宽50/150/500/1500ms预算。

前次EDB5应用两处validation PowerShell修正后的[完整Verify][final-timespan-verification]已实际passed=true、896/896，11项目0 failed/errors/skipped/notRun，locked restore/Release零warning/error、七包及全部独立消费者完成。385文件[最终源清单][final-timespan-manifest]SHA为`A43CD74D3C354F7FB7A3B22A43627020946BAF05BFEE56D9F0669FE9B8047514`，启动/结束一致且比较完成；109合同证据e8按该轮结果记录复用。新[FunctionOnly76][active-function-consumer]/[Theme+TextMate78][active-theme-consumer]各18项内嵌AXAML全部true，两监督Exit0/noTimeout；产品四DLL和包hash与前次EDB5相同。[最终跨平台来源比较][final-compiled-source-proof]明确383相同+两个validation PS差异、CrossPlatformAll385Equal=false、AllCompileInputsUnchanged=true，不能用前次385全等记录代签当前来源。

前次7B29版本[Linux五轮派生摘要][final-linux-five-summary]已读取原始五报告并保留独立守卫为验收权威：每轮七个阅读/长块组100样本/10预热，短/长流各100会话/10预热；短流最早未显示至帧P95115.7539–126.5725ms，长流全部实际提交UI+同步布局P9542.5399–45.6744ms，长流Complete P95539.7656–595.3001ms、五轮每次Complete最大712.9927ms，均在原预算内。全部实际提交与Rendered批次分别保留（该五轮两者数量恰相同）；资源必需归零和1/1/4/256队列界限通过。[Windows候选五轮来源][final-windows-five-source]的正式首轮已完整失败，后轮停止，旧0485结果不转签；新不可变归档仍待收束，整体M6/M7未提前退出。

| Linux完整原始轮次 | 短流最早未显示至帧P95 ms | 长流Complete P95 / 最大 ms | 长流全部实际提交UI+同步布局P95 ms | 全部实际提交数 | Rendered批次数 |
| --- | ---: | ---: | ---: | ---: | ---: |
| [第1轮](../artifacts/linux-probe/a24551de85354cdb8b6fdcd244dcb5f3/report-1.json) | 120.5141 | 576.1724 / 666.4005 | 43.7414 | 4061 | 4061 |
| [第2轮](../artifacts/linux-probe/a24551de85354cdb8b6fdcd244dcb5f3/report-2.json) | 120.2473 | 580.9969 / 676.7174 | 43.3091 | 4053 | 4053 |
| [第3轮](../artifacts/linux-probe/a24551de85354cdb8b6fdcd244dcb5f3/report-3.json) | 122.5655 | 595.3001 / 712.9927 | 45.0150 | 3946 | 3946 |
| [第4轮](../artifacts/linux-probe/a24551de85354cdb8b6fdcd244dcb5f3/report-4.json) | 126.5725 | 558.0691 / 621.9958 | 45.6744 | 3973 | 3973 |
| [第5轮](../artifacts/linux-probe/a24551de85354cdb8b6fdcd244dcb5f3/report-5.json) | 115.7539 | 539.7656 / 627.5000 | 42.5399 | 4147 | 4147 |

前次[Windows EDB5原生smoke][final-windows-smoke]为**43/43 true**、error=null，[监督][final-windows-smoke-supervisor]实际Exit0/noTimeout；包含真实文本剪贴板复制、读回及原文本恢复均true，[来源][final-windows-smoke-source]固定产品/宿主副本。前次[Linux7B29原生smoke][final-linux-smoke]为40项功能/协议true、error=null，但原剪贴板无可读文本而保留，Clipboard执行=false、overall=false、[退出记录][final-linux-smoke-result]为1，Linux真实复制/恢复保持Pending。两PNG已目视；程序路由输入、Avalonia窗口渲染图与回环HTTP不等于物理输入、桌面截图、公网HTTPS、辅助工具、多DPI或麒麟验收。

后续Windows EDB5[正式首轮原始报告][failed-edb5-windows-round]已收齐11组且实际退出1：短流最早未显示至帧P95121.704ms通过，长流Complete P951051.317ms在1500ms内，但**2969次全部实际提交UI+同步布局P9552.5111ms超过原50ms预算**，raw error=null、overall=false。100次资源替换/卸载后必需计数均0，队列峰值1/1/4/0；这些通过项不抵消UI失败。驱动自动停止后轮，[监督补充记录][failed-edb5-windows-supervision]OwnedControllerStopped=false，没有终止任何进程；其中FirstRoundExitCode=null只是GetProcess监督元数据，不替代实际原启动进程已经取得的Exit1。旧失败与完整原始数据保留。

EDB5跨CreateTextLayout保留文字属性的字体语义缺口已由上面的同DLL差分确认，并在联合候选的固定用例中修复：同一个FontManager可经公开AddFontCollection替换，或让已有Embedded集合追加字体，manager/document引用相同不足以保证字体仍有效。新路径改为单次Create复用、fresh Measure列表检查及native Arrange列表保留；四项公开替换字体差分已通过，但新官方产物的完整Verify、消费/API及正式五轮尚未取得。EDB5原DLL的Linux五轮、896回归、包消费和Windows43项smoke保留为历史分项，不能签发修复版本M6/M7退出。

### Windows Full/Only分项（EDB5私有诊断）

正式Windows首轮失败后，使用[479文件冻结来源](../artifacts/markdown-ui-profiles/f44b0bef1be64941ab425328c1c61028/preparation.json)构建的同四DLL分别跑[完整顺序](../artifacts/markdown-ui-profiles/f44b0bef1be64941ab425328c1c61028/runs/full-20261015T022138Z-ecab979f76544ef4889fd14c474b8247/native.json)及[OnlyLong](../artifacts/markdown-ui-profiles/f44b0bef1be64941ab425328c1c61028/runs/streaming-long-20261015T022401Z-6e821ff7535f483fb53ac8d1f8596cba/native.json)。Core为带探针的私有`1EAED441…`，不是官方EDB5 DLL；各10样本/10短会话/10长会话、3预热，OnlyLong不运行阅读或短流，均formal=false。两运行实际Exit0/noTimeout，Full短流P9596.8122ms；Full/Only的长Complete P95及最大分别565.5659/490.3346ms，长流454/464次全部实际UI P9530.4146/27.0446ms。**这个较小样本没有复现正式100会话的52.5111ms失败，不能撤回正式失败或宣布修复。**

[Full分项](../artifacts/markdown-ui-profiles/f44b0bef1be64941ab425328c1c61028/analysis-full/summary.json)记录210,153行、956上下文，[Only分项](../artifacts/markdown-ui-profiles/f44b0bef1be64941ab425328c1c61028/analysis-only/summary.json)记录101,337行、584上下文。454/464个长流实际提交均按SourceVersion+SourceLength完整关联，missing、length mismatch及duplicate context均0。下表的后台Parse取原生全部实际提交，其余使用对应Ui.CommitTotal窗口内阶段；[紧凑比较](../artifacts/markdown-ui-profiles/f44b0bef1be64941ab425328c1c61028/stage-comparison.json)保留原摘要来源与hash。P95各自计算，父子阶段与不同时刻不能相加。

| 分项 | Full P95 ms | OnlyLong P95 ms |
| --- | ---: | ---: |
| 后台Parse整体 | 232.9193 | 230.0275 |
| 原生UI应用整体 | 10.8733 | 8.9763 |
| 原生完整同步布局整体 | 24.5828 | 23.2027 |
| Presenter.SetDocument | 2.4833 | 2.6339 |
| Presenter.CreateBlock（递归创建） | 8.8573 | 6.1327 |
| Presenter.Measure.Total | 23.6525 | 21.7500 |
| TextPresenter.Measure | 1.2817 | 1.4154 |
| CreateTextLayout.Measure | 1.2130 | 1.1800 |
| TextPresenter.Arrange | 0.6944 | 0.6689 |
| CreateTextLayout.Arrange | 0.6346 | 0.6274 |
| Presenter.BuildTops | 0.0731 | 0.0705 |
| Ui.ConditionalReconcile | 0.0007 | 0.0008 |

`Parse`的watch包住parser.Parse(source, previous)，其中包含完整Markdig解析、DTO归一化与后台MarkdownDocumentMatcher.Reconcile；本次没有它们各自的计时。`Ui.ConditionalReconcile`只包UI需要重新匹配时的分支，其后ApplyDocument中的选区MigrateSelection、身份索引建表、链接焦点迁移没有独立stage，仍计入UI总量；不能用上表该分支接近0推断全部身份处理近似免费。SetDocument与递归创建在本次小样本中的成本比TextLayout调用更高，BuildTops则较小，但尚未取得正式52.5111ms样本对应的细分记录，不能确认整轮唯一瓶颈。完整Measure/Ui.CommitTotal中观察到GC完成暂停，探针分配及暂停均保留；没有从墙钟中扣减暂停来宣称CPU通过。

[同形文字元数据比较](../artifacts/markdown-ui-profiles/f44b0bef1be64941ab425328c1c61028/full-only-text-metadata-comparison.json)包含Full22,557/Only23,114次Ui.CommitTotal窗口内TextLayout调用，0长度错配、0窗口外混入。字号26/字重600、两字符标题、1150DIP宽度的同形Measure单次P50为0.0367/0.0364ms，两边UI自身分配P50均4,024B；Arrange为0.0212/0.0218ms、均3,616B。这次Windows样本未出现此前Linux上下文中的同形百KB差异；不据此推断所有run字体相同或字体热更新安全。探针不记录源文本、不解析glyph face，也没有测试公开字体集合在运行中改变；已确认的跨Create字体语义缺口仍须独立修复和差分验证。

### Linux A50F前四轮原50ms失败分项与后续51ms复评

[四轮离线摘要](../artifacts/linux-probe/dfc8c8cdb68349dda6f37615eea4c319/derived-08a1d80ad2f34108a1468bb4097fa435/summary.json)使用最近秩P95，逐行保留源会话、SourceVersion及SourceLength。全部实际提交与Rendered在本次四轮数量恰相同；按会话+版本+长度关联后，Parse/UI应用/Layout数值精确相同，故此处不以Rendered筛选丢掉慢提交。下表P95分别统计，不能相加各列P95。

| Linux正式raw轮次 | ALL实际/Rendered数 | 后台Parse P95 ms | UI应用P95 ms | 同步Layout P95 ms | ALL实际UI+Layout P95 ms | 原50ms producer结果 | 长Complete P95 / max ms |
| --- | ---: | ---: | ---: | ---: | ---: | --- | ---: |
| [R1](../artifacts/linux-probe/dfc8c8cdb68349dda6f37615eea4c319/report-1.json) | 3843/3843 | 240.4643 | 2.3270 | 46.3053 | 47.8740 | true | 556.0486 / 618.1135 |
| [R2](../artifacts/linux-probe/dfc8c8cdb68349dda6f37615eea4c319/report-2.json) | 3848/3848 | 240.1137 | 2.3770 | 45.5856 | 47.3905 | true | 572.0404 / 595.2562 |
| [R3](../artifacts/linux-probe/dfc8c8cdb68349dda6f37615eea4c319/report-3.json) | 3751/3751 | 246.3196 | 2.4764 | 46.7717 | 48.5603 | true | 530.0269 / 604.6546 |
| [R4](../artifacts/linux-probe/dfc8c8cdb68349dda6f37615eea4c319/report-4.json) | 3669/3669 | 258.1182 | 2.8431 | 48.9090 | **50.6962** | **false** | 597.0674 / 740.2466 |

R4的215条UI+布局>50ms，占实际提交5.8599%；这些慢提交的合计墙钟中Layout占93.5027%，因此当前可确认主要成本在同步布局，不能由后台Parse较大推成UI预算的解析失败。原生报告没有TextLayout/Measure/Arrange内部探针，不能进一步断言是塑形、身份建表或某个布局阶段的唯一原因，也未扣除GC或调度暂停。最慢项为会话12/序号18/SourceVersion4833、SourceLength294912（最终748588字符的39.4%）：UI应用3.0832ms+Layout115.4890ms=118.5722ms；对应Parse208.8170ms另计。

| R4按已提交UTF16 SourceLength/最终748588分段 | 提交数 | >50ms条数 | UI应用P95 ms | Layout P95 ms | UI+Layout P95 ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| 0–25% | 1520 | 26 | 1.3909 | 41.8586 | 42.8973 |
| 25–50% | 982 | 87 | 2.8412 | 51.5523 | 52.7914 |
| 50–75% | 620 | 63 | 3.7239 | 51.5466 | 53.4596 |
| 75–100% | 547 | 39 | 4.0867 | 48.7895 | 51.5816 |

慢提交的150/215条位于文档长度25–75%，不是只有最后尾部；按会话顺序四段的UI P95为53.0687/50.2112/48.9305/51.9178ms，也不能据此声称持续单调退化。四轮短流至帧P95为130.3739/133.5771/133.3176/134.5431ms，短Complete最大100.0599ms；各轮短/长Complete均100项且没有500/1500ms越界，资源必需0及队列界限通过。R4唯一false检查为长会话提交UI预算，长流至帧组原50下UI P9550.6962、false；最后长Complete组的UI诊断P9550.3239不用于其总耗时1500ms判定，该组true。

51ms修订只用于上述两个长流UI口径的独立验收，原四轮50ms结果和原布尔值继续保留。随后同四DLL第五进程完成，ALL/Rendered UI P95为49.3257ms；[新守卫](../artifacts/linux-probe/dfc8c8cdb68349dda6f37615eea4c319/guard-policy-51.json)实际验证五独立进程全部通过显式51政策，原R4失败仍在原报告与新守卫中分别可见。产品优化未继续，未改写raw或拼接不同产物；134合同与M7软件归档均已实际完成，未测设备矩阵另行验收。

## 固定测量方法

`benchmarks/Markdown.Benchmarks` 使用 Release、无调试器、同一进程内复用 parser/pipeline；每场景先验证固定语料结构及投影，再预热10次，采样100次。通过 `Run-Baseline.ps1` 启动五个独立进程，共每场景500样本。各进程保存原始耗时与累计分配、最近秩 P50/P95、GC次数及进程工作集；汇总使用五轮 P50/P95 的中位数，保留每轮文件而不把五个点当作稳定 P95。

语料包括精确 UTF-8 大小的100 KiB / 1 MiB混合文档（固定100代码块）、100 KiB单长行代码块、100 KiB单段落及10,000普通段落。每份报告记录语料 SHA256、UTF-16长度、顶层/递归块数、代码块数和复制投影长度。每次采样使用 `Parse(source)`，不传入 previous，故身份匹配和差异提交另测。

耗时仅包围 `MarkdownParser.Parse`；语料创建、哈希、基线验证、预热和结果校验在计时之外。分配使用同一采样线程的 `GC.GetAllocatedBytesForCurrentThread` 差值，是累计分配，不是峰值内存。Gen0/1/2统计覆盖整组采样，工作集为整个进程，不能视为某个文档的存活大小。报告不强制 GC，也不将主动等待计为渲染速度。

环境来自实际 `dotnet --version`、运行时、OS、架构、`PROCESSOR_IDENTIFIER`、逻辑CPU数和GC可用内存；无法读取的字段为null。GC可用内存不是物理内存。基准同时记录功能程序集版本和 SHA256（首次M0报告建立后增加，原始报告未追填）。此后台基准不使用显示后端、字体、缩放或视口；这些字段在真实呈现基准时必须另行固定。

```powershell
dotnet restore benchmarks/Markdown.Benchmarks/Markdown.Benchmarks.csproj --locked-mode
./benchmarks/Markdown.Benchmarks/Run-Baseline.ps1
```

报告保存到 `artifacts/benchmarks/markdown/<id>/round-1.json` 至 `round-5.json` 和 `summary.json`。JSON中文字段中的耗时/字节/样本数使用数值，`是否通过` / `全部通过` 使用布尔值。这里“通过”只指固定结构、文本和采样条件通过，`性能预算已验收=false` 明确保留 UI、流式和呈现预算。

## 本轮结果

首轮 M0 五个独立进程、每场景共500个采样已通过语料和采样条件；[汇总](../artifacts/benchmarks/markdown/m0-20261013/summary.json)及同目录五轮原始报告保留。环境为 SDK 10.0.401 / .NET 10.0.12、Windows 10.0.26200、X64、Intel64 Family 6 Model 183 Stepping 1、16逻辑处理器；GC报告可用内存为34,010,132,480字节。本轮开发/集成工作仍在同机进行，报告可作为初次成本调查，正式回归须在构建与测试停止后重跑同一语料和二进制。

| 场景 | 顶层块 / 代码块 | 五轮P50中位数 ms | 五轮P95中位数 ms | 累计分配P50中位数字节 |
| --- | ---: | ---: | ---: | ---: |
| 100 KiB混合文档 | 2,772 / 100 | 22.2174 | 41.8282 | 15,275,248 |
| 1 MiB混合文档固定100代码块 | 28,907 / 100 | 348.9621 | 413.9607 | 164,480,528 |
| 100 KiB单代码块单长行 | 1 / 1 | 0.3134 | 0.4444 | 1,232,704 |
| 100 KiB单段落 | 1 / 0 | 0.3380 | 0.4343 | 466,624 |
| 10,000普通段落块（510,000 UTF-8字节） | 10,000 / 0 | 35.9306 | 42.2949 | 25,383,848 |

1 MiB混合文档的累计分配约164 MB/次，与密集表格、层级节点和复制投影一起统计；必须在M6分析增长来源与长会话成本，不能将其描述为峰值内存。单长行代码的最大样本44.2574 ms被原始报告保留，不能只引用P50而抹去GC/调度极值。后台长块解析很快也不表示原生字体塑形、排版或选择很快。

不得以纯解析 P95 套用“解析至首屏”300 ms / 1500 ms预算；也不得据此宣称达到或超过官方性能。当前所有原生首屏、流式和布局预算保持未验收。

尚需真实原生平台测量：UI单批提交/布局、首屏、20chunk/s至可见延迟及最终等待、图片/高亮、10,000块滚动、单长块选择/滚动、资源稳定性。Windows与Linux分别建立基线，麒麟仍待设备。

## M6 原生测量入口与计时边界

2026-10-14 建立独立 `NativeHost/Markdown.NativeBenchmarks.csproj`，不使用 Demo、Headless 或开发窗口计数冒充原生呈现。固定窗口内容1200×700 DIP、缩放1、默认主题、已记录字体、语料SHA与产品程序集SHA；Windows与Linux分别采样。首轮pilot可减少样本以发现错误和预算失败，但标为非验收；正式报告须五独立进程，每场景每轮至少100个有效样本。

控制侧 `UpdateMeasured` 诊断分别计完整后台parse/convert、Dispatcher进入后的文档/身份/选择应用及同步布局；不能把后台或队列等待算作UI应用。原生宿主在Markdown上方放置不拦截输入的最后绘制见证，在窗口已原生挂载、文档长度/内容匹配及布局有效后，由见证 `Render` 捕获实际文档长度，再请求公开的原生 `CompositionBatch.Rendered`，在 render thread 完成时记录时间戳。这是包含后续确认批次的保守边界，未把固定等待当作渲染时间；**不证明GPU呈现、显示器scan-out或物理输入端到端延迟**。每报告记录生产端 `StopwatchFrequencyHz`，跨Windows/Linux校验用该频率，不用验证脚本进程的时钟频率。

**旧报告诊断范围：截至71E0产品/351D宿主的原始报告，layout仅包围`ApplyDocument`中的首次同步`UpdateLayout`。随后Loaded回调的跟随滚动、anchor恢复及新尾部布局不在这项metric内，但仍包含在接收至原生帧的真实端到端延迟中。** 因此首轮40.4013ms只证明所记录的首次UI应用+布局P95，不能据此宣称完整follow全过程满足50ms。A174修复已将同步following路径中的尾部实现、ScrollToEnd与全部同步布局包含在同dispatch的layout metric内；旧数据保持原值与范围，扩大后的诊断须用新产品hash单独重测，端到端Render边界和预算保持不变。非following的后续anchor恢复不能因此推断为已纳入，无法同步确认尾部的有界回退须明确LayoutPending。

原生首屏和长块每组预热10次、采样100次，流式预热10个完整会话后采样100次完成及至少100个覆盖批次。高亮首屏允许正常纯文本回退，同时另存可见高亮活动请求清零后的原生帧时间。累计分配来自 `GC.GetTotalAllocatedBytes` 的进程差额，包含同时进行的后台解析、渲染和资源工作，不代表峰值或存活大小；流式批次从最早覆盖chunk开始计算。原始pilot中的流式分配字段0未测量，不能引用为零分配证据，新入口已改为实际采集。

| 场景 | 原始观察与预算 |
| --- | --- |
| 100 KiB / 1 MiB混合文档，纯文本与TextMate分别测量 | 接收至原生绘制见证P95分别≤300 / 1500 ms；1 MiB单批UI应用+布局P95≤50 ms；另存parse、UI应用、布局和可见高亮全部完成后的绘制时间 |
| 100 KiB最终文档、20chunk/s | 每个chunk保存不可重写的接收时间与累计UTF-16结束位置；绘制见证覆盖的一批从最早尚未呈现chunk计延迟，P95≤150 ms；停止接收至最终提交≤500 ms，不用最后chunk时间重置累计等待 |
| 1 MiB最终文档、20chunk/s长会话 | 最早未显示chunk至Render的真实延迟全部保留；每次停止接收至最终提交≤1500 ms；全部实际UpdateMeasured提交的UI应用+布局P95≤50 ms，另核对被绘制批次；最终源码/全文选择完整。四个文档大小区间及首末退化比例仅诊断 |
| 10,000普通块滚动 | 顶层实际Bounds与ScrollViewer.Offset/Viewport独立算visible；每步realized≤visible+16、retained≤16；同时全选投影完整 |
| 100次替换/共享服务切换/卸载 | active parse≤1、pending request≤1；可控服务记录活动/取消请求、订阅/退订；稳态旧lease/presenter/reader弱引用及旧回调验证，不把累计请求数当作泄漏数 |
| 单100 KiB代码、段落 | 首屏P95≤300 ms、同步布局P95≤150 ms；选择/滚动至Rendered分别P95≤150 ms；每份有效文档加载和两次交互的进程累计managed分配最大值≤128 MiB |
| 30×30宽表 | 首屏P95≤1500 ms、同步布局P95≤500 ms；选择/滚动至Rendered分别P95≤150 ms；每份有效文档累计managed分配最大值≤128 MiB |
| 模型身份更新 | Parse(source, previous)、Reconcile及删除锚点MigrateSelection分开记录；仅语义/采样守卫，UI性能预算仍由原生报告判断 |

长单块与长会话门槛于2026-10-14首次冻结，补齐原计划尚未给数值的范围；此前3样本pilot缺少这些阈值，保留原始结果而不追改预算或宣称已验收。单文档128MiB是从接收前至选择、滚动两次见证后 `GC.GetTotalAllocatedBytes` 的累计差值最大值，任何有效文档超限都会拒绝，不能用P95掩盖单份超限；该差值含并发工作，不能称为存活堆。进程峰值工作集与非强制GC的托管堆估算另存，不承诺未经实测的512MiB上限。

1MiB长会话以固定8192 UTF-16单位、实际不可重写时间戳输入20chunk/s。观察按累计已绘制UTF-8大小落入最终源文档的 `(0,25%]`、`(25%,50%]`、`(50%,75%]`、`(75%,100%]`；各区保留样本数、parse P95、UI应用+布局P50/P95、累计分配/当批源字节P95及 `GC.GetTotalMemory(false)` 估算。累计分配从当批最早覆盖chunk至该原生确认帧，可能包含多次合并解析；归一值不是峰值堆或存活字节比。首/末比例无冻结预算，缺区或零分母时明确null，不能据比例宣称通过。前缀UTF-8长度在计时外预计算，观察器不逐批扫描全文。100KiB独立流式≤150ms预算继续单独执行，长文档1500ms最终提交门槛不能代替它。

每个正式会话另存完整原始receipt序列：重置后的BaseSourceVersion、每chunk连续版本、UTF-16/UTF-8累积结束位置、chunk长度与接收ticks。守卫依据原生Render的SourceVersion/end位置处理尚未覆盖的接收prefix，独立计算最早接收、覆盖数量与字节，拒绝漏receipt、最新时间替最早时间、旧chunk重复覆盖及未来receipt；不仅核对自报耗时。停止接收时间在producer最后Append返回后立即捕获，包含回到UI continuation的真实排队时间；Complete结束记录在应用返回处，不加入资源settle、选择、主动等待或后续帧确认。receipt整理发生在该计时后。此前旧宿主尚未保存全部receipt、且stop时间在UI continuation捕获的历史报告保留，不能作为此新守卫格式通过证据。

资源观测分别验证每个Reader的 `ActiveParseCount≤1`、`PendingRequestCount≤1`、`ExecutingResourceRequestCount≤4`、`PendingResourceRequestCount≤256`。`ActiveResourceRequestCount` 包括等待UI接收或释放的完整异步请求，不设260的虚构峰值；稳态须为0，并检验旧lease、Reader、回调和订阅释放。非合作服务不因收到Cancel就伪报已结束。

### 原生 pilot 结果（非正式验收）

2026-10-14 两平台使用同一完整隔离程序集副本：功能SHA `326C6F9CF0510A3B5C9E5F33CCF9386CE4EF22770A480D05524397EFB9789FD3`，NativeHost SHA `B9721DE06299958E31B8C28B662261CD887F5D82F42B561FCAAEFA9E6F5C69C0`。固定1200×700 DIP、scale1、Release / .NET10.0.12，原生句柄HWND/XID均有效。每阅读/长块组仅预热1次、采样3次，流式仅1个会话且未预热；开发集成仍在同机进行。下表P95为这3个/7个/4个点的最近秩值，仅用于定位失败，不能描述为稳定P95或正式平台支持。

| 原生场景 | Windows 总耗时 / UI应用+布局 ms | WSLg/X11 总耗时 / UI应用+布局 ms | 结果 |
| --- | ---: | ---: | --- |
| 100 KiB纯文本 | 144.813 / 57.987 | 253.738 / 137.968 | 首屏pilot≤300；未设100KiB UI预算 |
| 1 MiB纯文本 | 800.014 / 66.074 | 953.691 / 126.580 | 两平台UI单批>50，失败 |
| 100 KiB TextMate | 117.056 / 29.843 | 169.349 / 79.383 | 首屏pilot≤300 |
| 1 MiB TextMate | 732.893 / 40.773 | 480.288 / 88.064 | Linux UI单批>50，失败 |
| 20chunk/s最早未显示chunk至原生帧 | 159.847（7批） | 481.581（4批） | 两平台>150，失败 |
| Complete至最终UI应用 | 71.962（1次） | 326.281（1次） | pilot≤500，最终全文/绘制长度一致 |

Windows 1MiB纯文本的UI应用分项10.01–17.49ms，布局35.13–56.06ms；Linux对应15.65–18.23ms和89.51–108.35ms。此副本主要超时来自布局。Windows 10k块3个位置visible均21，realized29/37/30；Linux中段visible20、realized37（多17个）失败，另外两个位置28/29。两个平台各3次替换卸载的旧Reader/lease存活0、服务活动/订阅0、活动解析及pending峰值1。新入口另加两个同时挂载的共享服务控件、卸载后旧回调、长块选择/滚动采样，尚需新二进制重跑。

证据：[Windows原始报告](../artifacts/benchmarks/markdown/windows-native-pilot-ae64e005f4a04842bc46339e6758143f/report.json)、[Linux原始报告](../artifacts/linux-probe/d8413c0e163e4b5a88319f1557ab8b55/report.json)。产品后续修改不能倒推为这份报告已通过，也不改变已冻结预算。

第二轮隔离副本功能SHA `0D5C5E7435BE543B3FE97A9FC33BACE5AD22FB13D0C3B4D4AB9FC000BF74A513`，NativeHost SHA `A92C94CADE7B3A2AEB985B649F321FE6E3465348891EC18267C85CB4E217E302`。仍仅每阅读场景3样本、1次预热、1个未预热流式会话；[Windows报告](../artifacts/benchmarks/markdown/windows-native-pilot-2-a745043a41f44c668dee923558c4e431/report.json)、[Linux报告](../artifacts/linux-probe/c9fbff9529894700adcb20c1097f26d0/report-1.json)保留。Windows 1MiB纯文本首屏816.090ms、UI批72.407ms，高亮540.104/41.621ms；Linux对应792.426/108.324ms及439.451/76.726ms。UI应用已降至约1–2ms，主要失败仍来自布局。流式Windows197.094ms、Linux527.518ms均>150；最终完成96.606/127.836ms。Windows10k块三个位置通过，Linux中段realized36、visible19仍多17个；各平台两个同时挂载的Reader共享服务、卸载后旧回调检查通过，旧Reader/lease及活动请求/订阅均0。长块已测全选与滚动至render thread：Windows长代码选择32.71–33.47ms、滚动66.34–67.12ms；单段落32.33–34.19/32.47–33.82ms；宽表33.16–51.32/29.32–30.59ms。这些值包含帧确认，未制定单长块耗时阈值。

新版宿主补入流式10次完整预热、Render时捕获的metrics与SourceVersion/接收长度验证、全进程GC/工作集统计。后续正式采样须使用新宿主固定hash。全进程峰值字段来自OS `Process.PeakWorkingSet64`，包括所有场景、预热和资源探针，不能作为单份文档受控堆的峰值；不可读时明确标不可用。1MiB流式长会话退化仍待独立采样，不能以100KiB流式报告覆盖。

模型更新入口首次五轮在真实Linux SDK编译的隔离功能SHA `73F8B16DD587286AD0E08A38DEB427F60BB11AD77363F8C12D14B39EA3DBAACD`运行。固定10,000个唯一普通段落，追加尾部段落与删除除首段外所有旧锚点；预解析数据在计时外准备。每组每进程预热10次、100样本，五进程各500样本，追加后首尾旧身份保持、删除选区明确落在唯一存活前邻的末端。分配为测量线程累计差值。原始分位数/语料/hash/独立进程数守卫通过，但开发同机活动仍存在，且没有UI或官方测量：

| 模型更新场景 | 五轮P50 / P95中位数 ms | 五轮分配P50中位数字节 |
| --- | ---: | ---: |
| Parse(source, previous)，含解析归一与身份匹配 | 48.5381 / 62.4986 | 31,792,120 |
| 预解析文档Reconcile | 15.6719 / 26.7751 | 13,018,144 |
| 删除锚点MigrateSelection | 1.7081 / 2.3477 | 1,103,728 |

[模型汇总及五轮原始报告](../artifacts/benchmarks/markdown/model-updates-linux-ce75b5f5a50f49428b3bd813ce7cba41/summary.json)保留，`性能预算已验收=false`。后续parser/metadata/匹配器优化需新hash重测，不能将这版和原始M0不同语料/平台的数值直接当作改善百分比。

第三版副本功能SHA `6371EFBFB26375A5BCA4D877A14EC85517D2D5343B7508AA9C02E29D9DA29335` 在启动空文档原生布局时出现 `MeasureOverride` 下标越界，进程在场景前退出，没有性能报告。该失败已推动空文档护栏修复，不能将没有报告的第三版计为性能通过。

第四版功能SHA `52CB846B1BF061D64F564FB0B6B402C19D01303D0D0133F76C08831A5DEDB9B2` 的Windows完整pilot首次阅读UI均在预算内；[完整pilot](../artifacts/benchmarks/markdown/windows-native-pilot-4-da305a2b89114fd789bdfe6e0f441d72/report.json)的流式9批P95167.005ms仍>150，Complete114.942ms，虚拟化与资源检查均通过。同副本随后运行**单进程、每组10次预热+100个阅读样本**，[原始报告](../artifacts/benchmarks/markdown/windows-native-pilot-4-da305a2b89114fd789bdfe6e0f441d72/reading-100-round-1.json)阅读固定预算全部通过：

| Windows阅读单轮 | P50 / P95 ms | UI应用+布局P95 ms |
| --- | ---: | ---: |
| 100KiB纯文本 | 68.307 / 116.400 | 29.052 |
| 1MiB纯文本 | 366.702 / 412.652 | 33.178 |
| 100KiB TextMate | 67.040 / 83.907 | 18.732 |
| 1MiB TextMate | 380.747 / 409.720 | 33.398 |

该阅读单轮累计全进程分配43,006,699,592字节，OS历史工作集峰值374,448,128字节；仅解释整个宿主400阅读样本+40预热，不能当作单文档堆峰值或泄漏。四组单轮通过仍不替代五独立进程、流式与其他场景的整套守卫。第四版同hash [Linux完整pilot](../artifacts/linux-probe/4f4f36f09033406bb521082eb110bb57/report-1.json)仍失败：1MiB纯文本UI92.240ms、高亮UI66.353ms、流式487.000ms；Complete127.077ms。Linux须另运行100样本/10次预热的阅读单轮定位。

第四版同hash [Linux阅读100/10单轮](../artifacts/linux-probe/4f4f36f09033406bb521082eb110bb57/reading-100-round-1.json)已完成400样本，仍失败，不能用Windows结果代替：

| WSLg/X11阅读单轮 | P50 / P95 ms | UI应用+布局P95 ms |
| --- | ---: | ---: |
| 100KiB纯文本 | 113.319 / 162.477 | 69.113 |
| 1MiB纯文本 | 410.943 / 439.837 | 70.525（>50） |
| 100KiB TextMate | 109.065 / 129.008 | 60.047 |
| 1MiB TextMate | 413.729 / 440.240 | 71.129（>50） |

该Linux阅读单轮累计进程分配46,263,523,136字节、历史工作集峰值478,502,912字节，范围同Windows单轮。两平台fontconfig/字体匹配候选分别固定；`TryMatchCharacter`记录候选family，不认证每个已绘制glyph实际使用的字体。这是平台独立的自有预算判断，没有官方性能参考。

后续功能SHA `1FBCC0234F1B8CCFBEE44870094E950DC7D253A9060B98CC2CD7873B70D138BF` 的[Windows完整pilot](../artifacts/benchmarks/markdown/windows-native-pilot-final-635bcf32181d4453aa5000acfa2e955e/report.json)仍只有3样本/1预热：1MiB纯文本UI批54.428ms失败，高亮42.370ms；100KiB流式8批132.018ms、Complete40.180ms。这版未执行1MiB长流，不能覆盖新增门槛。

功能SHA `81842BC79AED1DCB3067BEB58BF21F194FDA5E66BC123972113E202A12C493E8`、宿主SHA `5B04E488287CCC53B1B2E971CB82A55B89CA3424B9956F92632C36A04BC05DDC` 的[Windows阅读100/10单轮](../artifacts/benchmarks/markdown/windows-reading-final-9f90aca6140f426294d6e5a69c42f3ec/reading-100-round-1.json)四组固定阅读预算全部通过：

| Windows阅读单轮（81842版本） | P50 / P95 ms | UI应用+布局P95 ms |
| --- | ---: | ---: |
| 100KiB纯文本 | 67.928 / 118.618 | 27.350 |
| 1MiB纯文本 | 400.351 / 679.386 | 32.136 |
| 100KiB TextMate | 82.294 / 115.314 | 27.171 |
| 1MiB TextMate | 400.213 / 450.383 | 32.048 |

此单进程只测四个阅读场景，不含五轮、长单块新门槛、1MiB长会话或最终平台回归，也未记录后续新增的主题程序集hash。CustomRow等后续修改必须在新二进制重新验证；该结果不能拼接为新的完整五轮。

最终冻结Windows功能 `1B6523A929E71E79F77EEAB684485107E6B24429A740798F2ED73F0E568071F9`、宿主 `51D018E80AF745CA52CA201B265A2BC9A1ADCB7352BCD6EB584C22482E68C918` 的[完整11场景pilot](../artifacts/benchmarks/markdown/windows-final-pilot-79961ad12f0a42078abdbbdadf31049d/report.json)实际退出1。每阅读/长块3样本、1预热，短/长流各1次完整预热和1个采样会话，未并行build或tests。1MiB纯文本总682.245ms、UI批50.820ms超过50门槛：第一个样本UI应用2.206/布局48.614ms，另外两批41.797/20.956ms。10k块首次顶部Offset0、独立Bounds计visible0/realized24，超过visible+16；中部20/24、末端19/21通过。[新进程只跑虚拟化](../artifacts/benchmarks/markdown/windows-final-pilot-79961ad12f0a42078abdbbdadf31049d/virtualization-fresh.json)顶部20/22、中部20/24通过，底部Offset399060时0/24再次失败。两个报告保持原样；读取独立100样本定位与视口同步问题修复仍需完成，不能以3个点宣布稳定P95。

这一pilot的100KiB流式8批最早未显示至帧117.816ms、Complete54.192ms；1MiB长会话33批真实延迟P95863.917ms仅诊断，Complete688.571ms、全部实际提交UI批P9535.437ms，最终源码/选择和1/1/4/256资源边界检查true。长会话累计进程分配2,009,273,472字节，托管堆估算起176,156,528/末176,843,296字节；不能以这一单会话差值证明无退化或泄漏。首/末quarter UI批P95为35.034/35.338ms，四区Parse P95随源大小为92.674/195.908/325.565/408.372ms，原始批次/receipts/commits全部保留。长代码/段落/宽表首屏84.116/133.297/283.203ms，布局38.093/81.195/217.395ms；选择与滚动至帧均≤150ms，每文档累计分配最大5,433,936/14,810,192/74,189,240字节。三次替换/共享服务probe的旧Reader/lease、请求、订阅和迟回调为0，但不替代正式100次测量。

同一冻结副本随后[阅读100样本/10预热诊断](../artifacts/benchmarks/markdown/windows-final-pilot-79961ad12f0a42078abdbbdadf31049d/reading-100-round-1.json)前三组分别完成100样本：100KiB纯文本总/UI P9599.809/23.551ms，1MiB纯文本400.235/28.984ms，100KiB高亮83.605/19.607ms，预算通过。第四组1MiB高亮仅14样本（总/UI P95465.761/37.788ms）后，`FrameWitness.ObserveAsync`等待UI `Render`超过30秒，报告记录TimeoutException并退出1。它尚未进入原生CompositionBatch等待；该旧入口没有超时现场可见性/Render计数，不能猜测为产品、窗口遮挡或平台原因，也不能记为四组100样本完整通过。该过程未并行build/tests，全部原始样本和失败保留。

随后同步ScrollViewer.Offset时立即更新Presenter视口，新增一次UpdateLayout的3跳转回归，原生[fresh虚拟化](../artifacts/benchmarks/markdown/windows-viewport-pilot-d2fc473d2c3b45f1968870f03ce7a4e8/virtualization-fresh.json)和[完整pilot](../artifacts/benchmarks/markdown/windows-viewport-pilot-d2fc473d2c3b45f1968870f03ce7a4e8/report.json)顶部/中部/底部visible20/20/19、realized22/24/21全部通过。新Windows功能SHA `71E042D902BE079B86823A4290A30E7FE0297C91B5D9B479E02FA829D53EFC7A`、宿主 `237008AC9441F7126F1796F795C3DF5C0788808AEA39BF6E72B04952712755FA`，FrameWitness另记录请求/最后实际Render的不可变可见性、布局和版本快照；超时依旧30秒、无重试/插等待/跳样本，快照轻分配计入实际测量。新完整pilot仅1MiB高亮UI批3点P9561.262ms超50，布局58.732/UI应用2.530ms；其余场景与语义/回收通过，退出1。短流144.819/Complete85.934ms，长流820.736ms仅诊断、Complete549.730ms、all-commit UI43.338ms。该失败仍保留。

同一新hash随后[完整阅读100/10单轮](../artifacts/benchmarks/markdown/windows-viewport-pilot-d2fc473d2c3b45f1968870f03ce7a4e8/reading-100-round-1.json)四组各100样本通过、退出0、无绘制超时：总/UI批P95依次为100KiB纯文本133.932/30.209、1MiB纯文本515.202/33.712、100KiB高亮99.097/22.272、1MiB高亮466.530/36.616ms。不能用100/10覆盖1预热/3点的历史pilot，也不替代全11场景五进程。

随后首次正式[2370宿主第1轮](../artifacts/benchmarks/markdown/windows-formal-71e042-bd1f0d0093064986aa15dfe134f54df8/native-round-1.json)退出1：100KiB纯文本100样本通过；1MiB纯文本仅90样本后超过原30秒Render等待。不可变快照明确记录最后实际Render时窗口Normal，下一次请求和超时现场为Minimized、WindowActive=false，布局和文档版本仍有效，Render计数422不再前进。[固定Avalonia源码](https://github.com/AvaloniaUI/Avalonia/blob/21e816874efa142bf9903b51fd323d1f35e56ab3/src/Avalonia.Controls/Window.cs)在Minimized时停止渲染；这轮未满足可见原生窗口条件，不能作为完整预算轮次，所有90个样本和失败均保留。旧1B65超时没有该现场快照，不能据这次证据替旧报告归因。

宿主仅在窗口创建时固定`CanMinimize=false`、`WindowState=Normal`，保留`ShowActivated=false`，产生新Host SHA `351DAFBBCFFE402E316A9234D82018CEF6384C1E33E20C9C722D707CB4921616`；产品71E0和所有预算、30秒等待、采样流程不变，没有采样中恢复窗口或重试。[新正式目录](../artifacts/benchmarks/markdown/windows-formal-351daf-afea65b94b2f42be9686551ad0ca6926/source-snapshot.json)使用同一完整副本启动五独立进程计划。实际[第1轮](../artifacts/benchmarks/markdown/windows-formal-351daf-afea65b94b2f42be9686551ad0ca6926/native-round-1.json)11场景及100次虚拟化/资源检查通过；[第2轮](../artifacts/benchmarks/markdown/windows-formal-351daf-afea65b94b2f42be9686551ad0ca6926/native-round-2.json)全部场景完成后预算失败，launcher退出1，第3–5轮未启动，五报告守卫未执行。不能将首轮通过写成M6验收通过，也不因同机后台进程存在而撤回第2轮的真实失败。

| 相同71E0产品/351D宿主的实际正式轮次 | 第1轮 | 第2轮 | 固定门槛 |
| --- | ---: | ---: | --- |
| 100KiB短流式可见批次数 | 971 | 813 | 各100会话、10会话预热 |
| 短流式最早未显示chunk至帧P95 ms | 119.0714 | 169.8169 | ≤150，第2轮失败 |
| 短流式Complete P95 / 最大 ms | 101.260 / 140.1963 | 146.556 / 178.2172 | 每次≤500 |
| 1MiB长会话实际提交次数 | 3321 | 2552 | 完整100会话及原始receipts/commits |
| 长会话全部提交UI+布局P95 ms | 40.4013 | 70.4623 | ≤50，第2轮失败 |
| 长会话Complete P95 / 最大 ms | 933.2563 / 1128.1991 | 1759.3211 / 3210.7820 | 每次≤1500，第2轮失败 |
| 10k块滚动失败数 / 样本数 | 0 / 100 | 0 / 100 | realized≤visible+16、retained≤16 |
| 替换/卸载次数 | 100 | 100 | 旧Reader/lease、订阅/活动请求/迟回调均0 |

两轮各阅读与长块组都完成100样本/10预热且通过；第2轮真实峰值parse1、pending1、执行资源4、排队资源0，稳态活动资源0。单轮[紧凑汇总](../artifacts/benchmarks/markdown/windows-formal-351daf-afea65b94b2f42be9686551ad0ca6926/native-round-2.summary.json)不替代独立五报告守卫。

进程结束后只读原始报告一次，按生产端频率及各会话未覆盖receipt独立重算earliest、覆盖数量/字节和延迟，两轮错误数均0。[原始分项对比](../artifacts/benchmarks/markdown/windows-formal-351daf-afea65b94b2f42be9686551ad0ca6926/streaming-round-comparison.json)显示短流parse/UI应用/布局P95从36.7294/4.1519/26.4383增加至59.2011/7.2934/42.5838ms；实际接收间隔P50/P95从50.1046/58.5957至50.3324/65.6811ms，覆盖chunk数P95从1至2。短流超过150ms的批次数从4/971至105/813。长会话最终parse/UI应用/布局P95从578.9969/27.2178/26.3979至902.8232/66.0881/47.6276ms。parse、UI、layout均变慢，不能只归因帧确认；延迟减最新三项的残差仍包含此前提交、合并、队列与确认，且各项P95不能相加。原因待进一步隔离profile，不能改变receipt、排除慢批或放宽门槛。

### 跟随提交与布局收敛的已确认修复

后续隔离已证实两个库内问题。旧following路径先在旧视口布局，再由Loaded回调滚动到新尾部；新路径在私有临时following scope内按新总高度选择尾部，并随实际测量高度修正视口，在同一UI dispatch中ScrollToEnd并确认最终节点的实际几何。合法的框架offset收敛不再误增用户阅读revision，真实用户上滚或重入仍使scope失效。成功路径的全部同步布局进入layout metric；不能同步确认的情况保留有界回退与LayoutPending，不以逻辑文档尾部代替实际可见尾部。

另一个确定问题是Presenter在视口落在块margin时，初始realization范围32..39与测量后的保留范围33..39不一致，每次Measure重新创建块32又立即清除，反复使布局失效。初始与最终范围现在使用同一个`TrimVisibleMargins`判定，消除了该创建/清除循环；ordinary/code overscan、图片邻域规则及预算不变。

[同一测试程序集的before/after证据](../artifacts/markdown-following-probe/6471905ac7644cd5bfb1372c86b3e21b/results.json)记录产品71E0下11项2通过/9失败，产品`A174E083B6BF2960707D8814B64E693A5E8AA3D8ADC64203905EDB236922AFAA`下11/11通过；完整Headless159/159，Errors0/Failed0/Skipped0。测试程序集SHA相同，断言观察同步Updated时的真实尾部几何、用户重入和LayoutPending。两个before用例在目标append前的setup就触发margin循环，只能证明该布局缺陷，不能单独作为append时序证据；早期非法零尺寸renderer和缓存旧source的探针仍排除。该结果证明软件行为修复，不证明短流150ms、长流50/1500ms或五轮性能通过，也不能据此断言第2轮全部变慢都由这两个问题造成。最终源码仍需集中回归、包消费与原生smoke后冻结，再用新hash完整采样；旧失败保持原样。

最终following和未闭合EOF错误恢复产品在[840项集中Verify](../artifacts/verification/7b338e894adb4d51a425a0c3b6134138/results.json)、七包/独立消费者和Windows43项native smoke通过后重新冻结。Windows功能SHA `DA1108CC82DA79A21433163E488A9BD783D7E5EC36EBD7F2685064F527D14B71`、宿主 `58DDD2B499EC3F232A9295D66348C6224A2FB597BBE25E2951F98916C206326E`；[新完整pilot](../artifacts/benchmarks/markdown/windows-final-da1108-92fc6c27c39d4276b4373b99f8312a70/report.json)11场景、虚拟化与资源检查通过，实际退出0。四阅读组总/UI P95依次为131.729/35.286、799.416/33.514、82.862/23.102、417.743/35.777ms；仅各3样本/1预热。短流1会话10批最早至帧99.645ms/Complete77.486ms，长流1会话37批780.784ms仅诊断/Complete507.311ms，全部实际提交UI+布局41.4105ms；长块与回收检查也通过，但均为pilot，不能替代正式100会话或五轮。

同副本[四组阅读100/10](../artifacts/benchmarks/markdown/windows-final-da1108-92fc6c27c39d4276b4373b99f8312a70/reading-100-round-1.json)完整通过、退出0，总/UI P95依次为117.544/24.307、433.798/32.980、83.348/18.409、846.356/49.412ms；1MiB高亮接近50ms，仍按实值保留。随后五进程新尝试[第1轮完整报告](../artifacts/benchmarks/markdown/windows-formal-da1108-c5c63aa9cd6c47e981b4651958152aad/native-round-1.json)在100次长会话完成后预算失败、launcher退出1，第2–5轮未启动，五报告守卫未执行，不能拼旧71E0轮次。

DA11第1轮阅读/长块七组均完成100样本/10预热且通过；短流100会话986批最早至帧P95106.5753ms/最大135.3554ms，Complete P9568.346ms/最大95.3298ms通过。**长会话100完成/2077实际提交的完整UI+同步布局P9587.2475ms超过50ms；Complete P952175.3424ms、最大2612.5187ms超过每次1500ms门槛。** 最早未显示至帧P951680.1047ms仅为长会话诊断。虚拟化100失败0；100次替换/共享服务/卸载的旧Reader/lease、活动请求、订阅和迟回调均0，parse/pending/执行资源/排队资源峰值1/1/4/0。[紧凑汇总](../artifacts/benchmarks/markdown/windows-formal-da1108-c5c63aa9cd6c47e981b4651958152aad/native-round-1.summary.json)保留逐次Complete最大值，不因P95或pilot通过而忽略单例失败。

进程结束后[一次原始分析](../artifacts/benchmarks/markdown/windows-formal-da1108-c5c63aa9cd6c47e981b4651958152aad/streaming-round-comparison.json)独立核对short/long全部receipt覆盖、earliest与延迟，错误数0。长会话全部实际提交parse/UI应用/完整同步布局P95为831.1192/51.2881/57.3496ms；最终提交对应P95为1164.9761/90.0866/56.7417ms，Complete减最终三项的残差P95926.7419ms。最慢会话76在停止接收后728.6344ms仍呈现较旧prefix（UTF-16长度638976、parse1242.8893ms）；最终全文parse1868.8958ms，Complete2612.5187ms。会话90的前一prefix只比最终源少一个chunk，却在stop后1131.9122ms呈现。原始receipt不是parse开始/结束时间，commit没有起止ticks，不能据这些数值精确断言解析重叠或把残差全归为排队；它们支持进一步对完整解析、归一化与串行尾部成本作隔离profile。新报告UI/layout已包含完整同步follow，不能用旧首次布局范围解释为通过；全部失败保留，下一版本须重新冻结并采完整五轮。

### DA11私有UI分阶段定位（非正式性能证明）

为区分身份迁移、提交和布局，在保存的DA11源码完整私有GUID加入internal计时探针，没有修改共享产品、公共API、DTO/parser/matcher、语料、字体、FrameWitness、receipt或预算。私有Release编译0warning/error，原生Win32仅3完整预热+10个1MiB长会话，数据先缓冲、ProcessExit一次写出，无逐批文件/控制台输出。[来源、四DLL SHA及退出码](../artifacts/markdown-ui-profiles/0dd7b0443f754429a4df5b30948081da/runs/efb8a6ab4233405e986007593af3f04b/results.json)记录私有功能SHA `73D8E013FF8A71CCBCA1EE8DAEF9BE2385BD68746671D8D98FCAEEC8C8CB1172`、宿主 `8A9C38F53F99F393E2B29E570557C75ABDA642C3CB1C7AEA867BA1B2FA4CFF8E`。实际359提交、Complete P95818.875ms、完整UI+布局P9537.0392ms仅为探索，不能覆盖DA11正式100会话失败或拼入五轮。

[UI阶段汇总](../artifacts/markdown-ui-profiles/0dd7b0443f754429a4df5b30948081da/runs/efb8a6ab4233405e986007593af3f04b/ui-stage-summary.json)按实际SourceVersion/SourceLength关联，共13641匹配记录，长度不一致、非UI线程、跨线程均0；各阶段先对同一版本多次调用求和再算最近秩。近1MiB区间为UTF-8源大小超过最终75%的44个实际提交，全文最终长度另有10提交。首次分析的分组条件漏括号导致all/last-quarter误取final10；原错误文件保留，聚合版本2从已保存per-commit数据修正为359/44/10，未重读详细UI原始数据或新增采样。

| 近1MiB实际44提交的阶段 | wall P50 / P95 ms | 当前UI线程分配P95 B | 进程分配差P95 B | 完成GC累计暂停差P95 ms |
| --- | ---: | ---: | ---: | ---: |
| 选择迁移 / 链接焦点 | 12.7565 / 19.0095 | 8,460,872 | 8,469,736 | 1.7180 |
| Presenter.SetDocument | 1.5184 / 2.3315 | 1,471,480 | 1,466,624 | 0 |
| 完整同步following / 布局 | 5.1910 / 25.3982 | 2,585,808 | 2,584,248 | 20.0190 |
| Presenter.Measure（嵌套于上一项） | 4.2851 / 24.8650 | 2,278,736 | 2,279,600 | 20.0190 |
| BuildTops（同版本调用聚合） | 0.1095 / 0.1302 | 0 | 0 | 0 |

UI线程分配来自`GC.GetAllocatedBytesForCurrentThread`；进程差包含后台与render工作，采用近似累计计数，不能据与own的微小差反推其它线程零分配。暂停差来自进程[GC.GetTotalPauseDuration](https://learn.microsoft.com/en-us/dotnet/api/system.gc.gettotalpauseduration?view=net-10.0)，完成暂停的统计可能跨阶段边界；不能直接减去该值并把余数叫CPU或render成本，也不能断言该阶段触发了GC。嵌套父/子阶段及不同阶段P95不能相加；探针本身stamp/行开销计入外层。

具体重复成本：44次SetDocument新建heights/tops数组与ID字典，UI自身累计52,875,056B、每次P951,310,904B；原height+IDindex同一loop P951.3255ms，未为拆阶段改变算法。Measure累计88次pass，创建738块、调用child Measure1271次（738次进入前invalid）、prune移除205块；创建阶段UI分配累计71,148,288B、P951,711,384B，child Measure累计21,809,232B、P95502,352B。BuildTops累计处理2,279,982个height元素，却未出现同量级时间/自身分配；这份数据优先支持调查选择迁移及新视图/索引重复分配，不能单凭扫描次数把BuildTops定为主因。此次359提交条件UI身份重做为0，不代表正式100会话或其它文档永远不会触发。缓存/reuse后仍须保护IDs、范围、引用、脚注、定制converter语义，并重新跑差分和正式预算。

### 归一化复用的后端分阶段与差分证据（非正式预算验收）

DA11长会话正式失败后，在三个独立GUID副本中分开记录完整Markdig AST解析、归一化转换、身份Reconcile；计时使用`Stopwatch`，分配使用当前线程`GC.GetAllocatedBytesForCurrentThread`。语料直接使用NativeHost现有`CorpusFactory.Mixed`的精确100KiB/1MiB源，分别测初次解析、8192 UTF-16追加的10/92个prefix，以及原生末尾代表性的737280→748588两个prefix。每场景预热2次、采样10次；prefix创建、DTO校验与记录输出在阶段计时外，不做逐样本强制GC。转换阶段包括缓存资格、当前AST比较、递归fresh IDs、描述符计数/捕获和弱缓存登记，不能把这些成本排除后宣称节省。

[DA11阶段原始数据](../artifacts/markdown-parser-phase-profile/7b623c6dbe6b458e865290625d836847/phase-profile.json)保留旧完整转换成本；[首版缓存数据](../artifacts/markdown-normalization-phase-profile/3bf536fd85e8447c80908d977b16f359/phase-profile.json)同时保留缺陷：1MiB初次累计分配137.776640→211.803448MB，密集最后745472→748588未复用、转换分配139.787400MB。首版private build[两次CS8602及对应源hash](../artifacts/markdown-normalization-phase-profile/3bf536fd85e8447c80908d977b16f359/compile-failures.json)保留，后续编译修复不能抹去首次失败，它们不属于DTO断言或原生预算失败。[尾部类别诊断](../artifacts/markdown-normalization-veto-profile/2fdc9aa319f248e78fccee249814b880/veto-results.json)实际退出0：712704、729088、745472仅最后一个Paragraph含`Markdig.Extensions.Tables.PipeTableDelimiterInline`而关闭缓存；745472时root span745468..745471、delimiter span745468..745468。该诊断只记录类型、位置和flags，没有输出文档payload；[探针/语料/被检查DLL源记录](../artifacts/markdown-normalization-veto-profile/2fdc9aa319f248e78fccee249814b880/veto-source.json)保存Program SHA `FB9BF465FFC3217F250A5E15FEC4A66B533E57A335AC7D64F23C24314EC047B5`和被检查首版DLL `A6EA91DD36BE092D2EC960C32729EF4B072BAF73899108D2CF7D1867C8220CB3`，原始结果未覆盖。

最终内部方案保持每次完整Markdig AST解析，仅默认pipeline、无任何注册converter、同parser弱来源且Source严格追加时尝试复用。Entry只保存最后可见root之前已完成块的纯归一化内容和primitive shape；当前AST的结构、投影内容、元数据、源坐标和逻辑起点必须逐项匹配，首次不匹配即停止prefix复用。比较覆盖当前默认converter依赖的语义字段，**不声明所有raw AST trivia字段相同**。引用定义/脚注、reference/dynamic/shortcut link、HTML、其他未知节点以及非默认pipeline均全局禁用；直接链接还比较URL、title、image/autolink等字段。已知局部pipe-table delimiter只可出现在整个必重建的最后可见root内，仍递归检查其children；它移到非尾部也关闭缓存。尾部诊断只在既有`MDNODE`代码/完整message、尾部范围与实际delimiter AST source span精确对应时允许登记，诊断始终由当前尾块重新生成，不复制旧诊断。

描述符先无分配计数，再精确容量捕获并`MoveToImmutable`，消除扩容与最后复制。静态弱表的value不持snapshot、AST、UI、`StringSlice`、整个旧Source buffer或前entry链，origin为parser弱引用；仅不可变内容和primitive描述符可共享。所有复用块先递归生成fresh block IDs，再完整运行原身份matcher的重复计数/范围/父上下文规则，不能直接复用旧ID子树。以下数据还包含Matcher不可变inline数组/元数据自身相同比较快路径；它们不是缓存单项独立A/B。

[最终阶段报告](../artifacts/markdown-normalization-phase-profile/7ec7999fd1754dccb24f7494d1dd82bc/phase-profile.json)与[三版对照及源冻结](../artifacts/markdown-normalization-phase-profile/7ec7999fd1754dccb24f7494d1dd82bc/comparison-summary.json)实际退出0。下表为各独立探索运行的P50，MB按1,000,000字节；92prefix行先对每次完整序列求和再取分位数，各阶段P50不能相加当总P50。

| 场景 | DA11 / 最终总P50 ms | DA11 / 最终转换P50 ms | DA11 / 最终累计分配P50 MB |
| --- | ---: | ---: | ---: |
| 100KiB初次解析 | 36.1571 / 48.3650 | 13.7755 / 25.2127 | 12.755112 / 14.700000 |
| 1MiB初次解析 | 315.2335 / 308.3114 | 192.2454 / 191.6008 | 137.776640 / 158.496848 |
| 1MiB全部92次追加 | 17508.3240 / 10488.0995 | 8168.9426 / 2059.6455 | 7252.855040 / 4908.528232 |
| 密集最后745472→748588 | 371.8207 / 235.7455 | 197.0871 / 43.4169 | 155.059960 / 103.426432 |
| 原生代表末尾737280→748588 | 365.5622 / 207.5382 | 182.5882 / 46.7550 | 154.960784 / 104.123888 |

初次1MiB分配仍增加约15.0%，100KiB初次总P50增加约12.2ms，成本来自资格检查与描述符记录，不能称所有场景加速。最终92prefix完整AST累计P506764.9026ms、转换2059.6455ms、Reconcile1710.2561ms；每次仍全量解析，后端探索时间不是首屏、UI50ms、短流150/500ms或长流每次Complete1500ms通过证据。旧DA11正式失败保持不变，新产品还须集中验证与新的原生五轮。

[完整DTO/持久身份差分](../artifacts/markdown-normalization-phase-profile/7ec7999fd1754dccb24f7494d1dd82bc/dto-equivalence.json)比较106快照、0差异：每版独立对每个first-seen ID建立跨序列ordinal，保留身份保持/重建关系，其余源文本、复制投影、元数据、树、source/logical坐标、diagnostics与SemanticKind完整逐字节相同。[锚点/选区差分](../artifacts/markdown-normalization-phase-profile/7ec7999fd1754dccb24f7494d1dd82bc/navigation-equivalence.json)覆盖125快照、750个FromPosition/Resolve、684次MigrateSelection，0差异；包含头尾caret、正反向选择、删除/重复、晚引用与脚注、Unicode/CRLF、围栏闭合。基准产品DLL是DA11原始`DA1108CC82DA79A21433163E488A9BD783D7E5EC36EBD7F2685064F527D14B71`，最终instrumented DLL为`E1B817670A1C08F1DD76A17E036012430FCB44391272F754450519780F1E7E6B`；production parser/cache/Matcher源SHA及instrumentation源SHA均在对照JSON中分别记录，不能将私有instrumented程序集当最终包hash。

[未加计时探针的隔离Core](../artifacts/markdown-normalization-reuse/8d9958b2366743c785a6da10bb0e6c64/core-cache-after.xml)实际207/207、Errors0/Failed0/Skipped0/NotRun0，41.646s；[编译日志](../artifacts/markdown-normalization-reuse/8d9958b2366743c785a6da10bb0e6c64/build.log)0warning/error。新增19个cache用例含Native全部92prefix、直接链接、late refs/footnotes、重复与结构改变、非默认pipeline/wholeSource converter、同parser并发、弱parser寿命，以及旧snapshot/动态Source在保留parser+新snapshot时可GC、nested quote/grid fence及右格完整且只出现一次、pipe尾部/非尾部、dense最终边界共享与完整DTO/身份关系。原184个Core及新Matcher4个migration用例都在207中；此前[203项副本](../artifacts/markdown-normalization-reuse/9bf0c1c8ea844d85aa40b568b9e3bb19/core-cache-after.xml)保留，未包含后续快路径与四个新增用例，不替代207或最终集中回归。

### 硬换行软件修复与独立夹具排除（不计原生预算）

M7编译AXAML消费者在Markdown `a  \nb`的真实段落测量中阻塞，后端完整投影仍为`a\nb`。固定Avalonia12.0.3的原生inline路径在Wrap/无限高度下可重现；NoWrap或有限高度返回不能证明全文完整，有限高度可能只触发原布局的高度截断。依据包nuspec commit `21e816874efa142bf9903b51fd323d1f35e56ab3`核查[TextFormatterImpl](https://github.com/AvaloniaUI/Avalonia/blob/21e816874efa142bf9903b51fd323d1f35e56ab3/src/Avalonia.Base/Media/TextFormatting/TextFormatterImpl.cs)与[TextLayout](https://github.com/AvaloniaUI/Avalonia/blob/21e816874efa142bf9903b51fd323d1f35e56ab3/src/Avalonia.Base/Media/TextFormatting/TextLayout.cs)：Wrap的empty-shaped分支能返回零源长度行，外层仅按line.Length推进且无限高度没有高度退出；静态路径本身不证明所有平台都因newline零glyph触发。[TextBlock](https://github.com/AvaloniaUI/Avalonia/blob/21e816874efa142bf9903b51fd323d1f35e56ab3/src/Avalonia.Controls/TextBlock.cs)提供protected `_textRuns`与virtual `CreateTextLayout`，可在保留逻辑inline树的情况下改用公开`TextEndOfLine(1)`，再调用原base布局。原`TextCharacters.Properties`对象保留，CRLF只有与原Run/Span/LineBreak逻辑投影精确一致时归LF；未知run、无换行和已适配marker保留原路径。base继续保留原LineSpacing、LineHeight、flow/bidi、字体、宽高约束、trimming等参数，没有任意有限高度、MaxLines限行或新增隐形字符。末尾仍由原source产生virtual长度1的EOP，不计入Markdown逻辑文本。

**私有prototype。** [完整记录](../artifacts/markdown-hard-break-source/d495b2d14a0e41a6b39e3a0269d52496/proof-summary.json)的功能DLL为`FAAACAAEA184CAAF92EECBF6F3AF98989A500E1010AFB684602B619CB26401C2`；[原消费者76项](../artifacts/markdown-hard-break-source/d495b2d14a0e41a6b39e3a0269d52496/consumer-runtime-result.json)全部true、实际退出0，原compiled AXAML LF/CRLF、绑定、选区、流式、服务、renderer与automation断言保留。[tiny结果](../artifacts/markdown-hard-break-source/d495b2d14a0e41a6b39e3a0269d52496/tiny-hard-break-results.json)7场景/45检查全部通过：首/连续/尾LF、custom LineBreak的Environment CRLF归LF、UTF-16尾部位置、完整自然高度与再次Arrange、nested live properties、LineHeight31+LineSpacing7、运行时字号/颜色及窄宽重排；[实际运行](../artifacts/markdown-hard-break-source/d495b2d14a0e41a6b39e3a0269d52496/tiny-runtime-result.json)退出0、无timeout。它们是Headless软件和私有DLL替换证据，不能当最终NuGet包验收或Win32/原生性能通过。

prototype的失败也保留：直接构造paragraph properties首次编译因LineSpacing为内部API而CS0117；随后通过run-list+base保全该属性。首个tiny的[20秒timeout](../artifacts/markdown-hard-break-source/d495b2d14a0e41a6b39e3a0269d52496/tiny-first-timeout-result.json)不能判断适配失败：[IL记录](../artifacts/markdown-hard-break-source/d495b2d14a0e41a6b39e3a0269d52496/tiny-first-il-calls.json)证实诊断派生控件按旧NuGet元数据把basecall绑定到Avalonia.TextBlock，绕过新override；改为private DLL HintPath编译后才用于判断。随后[高度相等断言失败](../artifacts/markdown-hard-break-source/d495b2d14a0e41a6b39e3a0269d52496/tiny-rounding-assertion-failure.json)来自默认layout rounding与raw TextLayout高度比较；最终tiny显式固定`UseLayoutRounding=false`验证完整原始高度，产品和原76断言未改。

**正式五个回归fact。** 清理后的官方功能SHA `04856FD6EAF3796F9D400CAC656CCEE90D2F420D1197152A13721F71B4709AD0`保持上述语义，去掉了prototype无用Segment/Start/position。首个[290D before](../artifacts/markdown-hard-break-regression/2c61ae2e88f540ca91d1255ca9bc4bfe/before/result.json)与[048 after](../artifacts/markdown-hard-break-regression/2c61ae2e88f540ca91d1255ca9bc4bfe/after/result.json)均30秒timeout、结束前按实际PID/路径核验停止，计数为null，不记成5项断言失败或成功。[阶段日志](../artifacts/markdown-hard-break-regression/764855cf92434ddb9ca19d7d6fff5411/after/host/hard-layout-stages.log)明确记录适配MarkdownTextPresenter的`\nfirst` Measure与Arrange均返回，随后未经适配的plain TextBlock参考在同源Measure进入后不返。因此该after不能作为产品仍挂的结论，也不能抹掉其真实timeout；诊断运行还暴露custom重用点击被识别为双击而得到选区，原失败输出保留。

正式oracle改为每段无LF文本的独立原生Wrap/无限高度布局，再按源起点组合所有line ranges、逐行自然高度与总高度；每段terminal EOP长度1对应中间实际LF长度1，末段仍保原terminal。所有范围、完整行数、自然高度、重排、style、全局选区/copy/link与custom复用断言保留。custom cycle用`Application.Current.PlatformSettings.GetDoubleTapSize`改变真实Margin/点击位置，避免把单击前提误设为同位置连续双击，没有增加等待或放宽断言。首个[CS0104编译日志](../artifacts/markdown-hard-break-regression/2c61ae2e88f540ca91d1255ca9bc4bfe/compile-failure-1.log)与后续[CS1061日志](../artifacts/markdown-hard-break-regression/03e0e9e183b74a1ab87ab3041cefa091/compile-failure.log)保留；03e0编译失败后误启动cached test DLL的[排除记录](../artifacts/markdown-hard-break-regression/03e0e9e183b74a1ab87ab3041cefa091/exclusion.json)明确其runtime与所报source hash不对应，不能使用。

修正夹具的[最终before](../artifacts/markdown-hard-break-regression/729e01e7ea0e47fb8ec2701eb3167494/before/result.json)与[最终after](../artifacts/markdown-hard-break-regression/729e01e7ea0e47fb8ec2701eb3167494/after/result.json)使用**相同test source SHA** `5CF348F6B04FFE085735E20CD4E3AA8D920B8FF89C490B2711BBBA4DDECAD53E`、**相同test DLL SHA** `982235EECD35678C780B5EA39F7FC0D0FB9F1601DFB3EFC3EAA7A8A00F454181`，只替换功能DLL：290D仍30秒有界timeout并核验停止，048实际退出0、5/5、Errors0/Failed0/Skipped0/NotRun0，测试0.815s。该对照证明软件修复，未计任何原生预算。旧DA11长会话预算失败全部保留，预算未改变。

### 048冻结版本的集中回归与当前原生采样

2026-10-14的[集中Verify](../artifacts/verification/19b1d0d43b0e472aac15d0fdd2d4d48d/results.json)已实际完成：11项目870/870，各项目退出0、Errors0/Failed0/Skipped0/NotRun0。Markdown为Core207、Streaming60、Headless164、Themes10、TextMate13，共454；既有六项目416。运行前后[379文件源码清单](../artifacts/verification/19b1d0d43b0e472aac15d0fdd2d4d48d/source-manifest.json)相同，manifest SHA `C104EA4750B93BB43D245E2798D539B669C2662206F2B7727C6BD6BA7B91CF54`，功能DLL为上述048。该报告明确`nativePerformanceIncluded=false`，不能把集中回归计入原生预算通过。

最终隔离NuGet的[FunctionOnly消费者](../artifacts/consumer/b85e8af78af34dfc9feee6da05c4597f/result.json)76项及[DefaultTheme消费者](../artifacts/consumer/731d211645724312bb82b795fad57951/result.json)78项全部true，各含18项真实编译内嵌AXAML LF/CRLF检查；两份[FunctionOnly运行记录](../artifacts/consumer/b85e8af78af34dfc9feee6da05c4597f/supervisor-result.json)/[DefaultTheme运行记录](../artifacts/consumer/731d211645724312bb82b795fad57951/supervisor-result.json)均退出0、无timeout、错误null。两者使用相同三包，功能/主题/TextMate包SHA依次为`10467E72EE7173F449FCD12BEC27014FBE5F49FF7BFC2219189B4EE63C9A3EC6`、`6B15E789E1607E54EA724245C0F57A64D71B4222D4FAFCEB15A8C99B818AFBC3`、`261015835E47F6512122115BB1958474A42934C91EA365F99D1C3AD27930B284`；FunctionOnly没有加载主题或高亮DLL。实际048 Release程序集的[API快照](validation/markdown-public-api.txt)由[本次导出helper](../artifacts/markdown-api-helper/cfa345c0edc04f388fc8f6337c64b252/Program.cs)生成。这些是软件/包合同证据，native smoke、Linux及物理设备仍按各自版本报告判断。

同版本的[新Windows完整pilot](../artifacts/benchmarks/markdown/windows-cache-04856f-pilot-ddf9dfc6ddf6465ab2a75c14b23d5073/report.json)11场景及资源/虚拟化检查通过、实际退出0；[汇总](../artifacts/benchmarks/markdown/windows-cache-04856f-pilot-ddf9dfc6ddf6465ab2a75c14b23d5073/summary.json)明确`formal=false`。阅读/长块各3样本，短流1完整会话，长流3完整会话，各1预热；长会话Complete P95471.5879ms、全部实际提交UI+同步布局P9528.2789ms。长会话最早未显示至帧P95499.8425ms仅为诊断，未改固定预算，也不将pilot代签正式100会话或五轮。

[新Windows正式五进程副本](../artifacts/benchmarks/markdown/windows-formal-04856f-9414e79e2b684ba08992741f7fa0ab86/source-snapshot.json)的五轮已完整完成、launcher退出0：功能048、主题`9D4C5D0A4908BDED99C7910738A46334E89C12D5400AAA0E60D5451E342B5081`、TextMate`69F2CB47B00D6A75DA0E509C5818F8D864017DFB93B596B2A013EDCC81E0E311`、宿主`24CA4D491E3FBD09E51E56AB38BA67DF6C4FCF187DB8D1ECD19DCE80F4194A93`，五轮使用同一完整隔离输出。[五报告独立守卫](../artifacts/benchmarks/markdown/windows-formal-04856f-9414e79e2b684ba08992741f7fa0ab86/guard.json)实际全部通过，独立进程数5/要求5，从五份原始数据重新核算全部固定预算与数据一致性；此处可以判定Windows平台预算通过。

五轮均正式采样为true、错误null、11场景和全部检查通过；各七个阅读/长块组100样本/10预热，短/长流各100完整会话/10预热。各轮短流最早未显示至帧P95为93.3415–99.3291ms，Complete P95为45.1491–49.7420ms，原150ms及每次500ms门槛均由守卫通过。下表分别列出长会话全部实际提交与已观察到的可见批次，Complete最大值仍用于每次1500ms门槛，不能以P95代替。

| Windows正式轮次（完整原始报告） | Complete P95 ms | Complete最大 ms | 全部实际提交UI+同步布局P95 ms | 实际提交数 | 可见批次数 |
| --- | ---: | ---: | ---: | ---: | ---: |
| [第1轮](../artifacts/benchmarks/markdown/windows-formal-04856f-9414e79e2b684ba08992741f7fa0ab86/native-round-1.json) | 510.0244 | 578.2785 | 32.5640 | 4577 | 4577 |
| [第2轮](../artifacts/benchmarks/markdown/windows-formal-04856f-9414e79e2b684ba08992741f7fa0ab86/native-round-2.json) | 548.9444 | 659.4792 | 31.8011 | 4595 | 4595 |
| [第3轮](../artifacts/benchmarks/markdown/windows-formal-04856f-9414e79e2b684ba08992741f7fa0ab86/native-round-3.json) | 527.0191 | 562.3000 | 32.2399 | 4595 | 4595 |
| [第4轮](../artifacts/benchmarks/markdown/windows-formal-04856f-9414e79e2b684ba08992741f7fa0ab86/native-round-4.json) | 525.0116 | 541.7689 | 32.3166 | 4602 | 4602 |
| [第5轮](../artifacts/benchmarks/markdown/windows-formal-04856f-9414e79e2b684ba08992741f7fa0ab86/native-round-5.json) | 547.8068 | 569.5124 | 31.5532 | 4593 | 3895 |

第5轮Render合并后3895个可见批次少于4593次实际提交，不能把前者当全部UI样本。[第5轮汇总](../artifacts/benchmarks/markdown/windows-formal-04856f-9414e79e2b684ba08992741f7fa0ab86/native-round-5.summary.json)保留该差异；50ms守卫使用包含未单独形成可见批次的全部实际提交UI+同步布局P9531.5532ms。可见批次的UI P9532.2553ms是另一统计，不能替换全部提交值。该轮长会话最早未显示至帧P95968.5786ms也按实值保留，仅作长会话诊断，不能套短流150ms门槛或把合并记录排除后重算。

各轮资源与虚拟化检查均由五报告守卫通过。第1轮[资源/范围明细](../artifacts/benchmarks/markdown/windows-formal-04856f-9414e79e2b684ba08992741f7fa0ab86/native-round-1.summary.json)中100次替换/卸载后，旧Reader/lease、当前服务请求、失效订阅及旧更新迟回调均0，实际parse/pending峰值1/1、资源执行峰值4、资源排队峰值0；100个虚拟化样本全部满足realized≤visible+16与retained≤16。Windows通过不覆盖旧DA11及71E0正式失败，也没有拼轮。同源Linux真实SDK的870/870回归已完成，但新原生pilot与正式首轮均完整失败，正式第2–5轮停止；**整体M6保持未退出**，M7软件验收继续。守卫所证范围是原生render-thread与完整软件预算，不认证GPU scan-out、麒麟或物理输入。

当前[Windows原生smoke](../artifacts/platform/windows-markdown-smoke-cache-7bee07f6bf6f455ab2c77b2d9163d271.json)及[新Linux原生smoke](../artifacts/platform/linux-markdown-smoke-2c50e76225fd4e839a978f3a785e6433/report.json)分别40项功能/协议检查true、错误null，但原剪贴板没有可读文本而保留，剪贴板测试执行=false、整体false，真实复制/读回/恢复保持Pending。Linux[退出记录](../artifacts/platform/linux-markdown-smoke-2c50e76225fd4e839a978f3a785e6433/result.json)为1。它们不是43项通过，也不由Windows性能五轮或Linux软件回归代签；历史DA11 Windows43项的真实剪贴板证据仍只属于该旧版本。

### Linux失败后的范围收敛私有探索（按当时版本记录）

Linux97e完整正式失败后，[私有reading分项](../artifacts/markdown-ui-profiles/2b890c1109e04612801becd51570f7a5/runs/reading-20261014T131427Z-jEKHlY/analysis-window/summary.json)记录1MiB首屏先按估计范围创建17块，再删除10块、最终只留7块。纯文本/高亮合并的26个非空提交含预热，创建/删除/保留数的P50与P95均为17/10/7；同步窗口内TextPresenter.Measure P9537.5720ms、CreateTextLayout.Measure P9537.5384ms、Ui.CommitTotal P9567.0324ms。[私有long分项](../artifacts/markdown-ui-profiles/2b890c1109e04612801becd51570f7a5/runs/streaming-long-20261014T131529Z-4sNRn5/analysis-window/summary.json)按原生SourceVersion+SourceLength关联314次实际提交：同步窗口内原生TextPresenter.Measure P9549.8528ms、CreateTextLayout.Measure P9549.3597ms、CreateTextLayout.Arrange P9518.2884ms；Ui.CommitTotal P9572.2264ms，BuildTops P950.1245ms且UI自身分配0。这些数据支持优先减少不必要的原生测量，不能把嵌套阶段或不同阶段P95相加。reading实际分组只在计数/固定长度匹配后作顺序推断，不冒充原生receipt join；含预热分组、窗口内阶段及窗口外异步高亮/Loaded工作分别保留，探针开销也未扣除，均不是正式预算证明。

私有a102候选在实际高度变化后重新求同一overscan、margin与image band需要的范围，跳过已经不再需要的块、补测新增缺口，最终仍使用原生Arrange。高度前缀用dirtyMinIndex从最早改变处更新，复用此前未变部分；缺失旧阅读文字时走既有TopBlockId/BlockOffset粗定位，再重新实现并精确恢复字素。没有跳过自然Arrange、限制TextLayout.MaxHeight或共享原glyph缓冲区。候选还需检查实际节点的局部服务，不能从reader根CodeHighlighter为null推断所有节点没有样式高亮回调。**该阶段NativeRange、dirtyMin与anchor改动只在私有副本；后续落入EDB5并通过896软件整轮，前后证据仍按各自产品哈希保留。**

夹具和失败链分开记账。原始source SHA `FC348579FE5FDA36C30153535047A785EDEADA355A3B719CA391F993401A3098`、测试DLL `DBD4227FCDFA7AC857BC6A92DAFA573C53A571ACC6BFF874DCCD01B4F170A905`的[before](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/before/result.json)为11/13、首版[after](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/after/result.json)为12/13；[anchor-after](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/anchor-after/result.json)仍12/13。该夹具捕获时用累计line.Height的Y、断言却用HitTestTextPosition.Y，并跨两次已经独立完成的窄/宽重排要求最初历史字素不变；后者强于每次变化前保当前可见字素的契约。统一原生矩形但尚未按transition重新捕获的[tops-after失败](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/tops-after/result.json)也保留，不能删除或改为通过。

最终夹具在每次独立宽度变化前捕获当前阅读字素，捕获与断言均用同一公开HitTestTextPosition坐标，**1px门槛、第一次变窄的原字素断言、覆盖/全文投影断言均保留**。同一source SHA `DF0DBD1849889A1AD3AD82611D1B6F2194E54A275972E75CE37C06B25202A874`、同一测试DLL `56BA1E88DF5E21D176403B99F2913E53343A5740BE88D6CD90205AA62763CF37`仅替换产品：[旧048 before](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/transition-before/result.json)11/13、退出1，[私有AE after](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/transition-after/result.json)13/13、退出0。旧版严格冷code创建9、删除8、保留1，新版只创建1；修正oracle后旧版仍在每次当前字素恢复时找不到其实现文字，候选通过，因此missing-text恢复有独立有效对照，不依赖历史字素额外断言。候选功能SHA `AE365F1F1819A6CEADEBB7D398EAD913DA139059A19D4AD7065A2E66D5D55F5E`属于私有副本，不能替换官方048的哈希或验收状态。

局部样式高亮的[首次1/2失败](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/styled-after/result.json)也保留：全局IndexOf选到重复段落中的emoji，追加使重复数量变化，matcher按原有保守身份规则回退，不能为该夹具改产品身份策略。selector改为唯一seed代码的全局TextStart加代码内UTF-16位置后，其余选区/字素、following同步尾部、实际节点服务、请求取消、卸载订阅、迟到结果0可观察格式/文档回调及宿主服务不释放断言保持。相同source `BCD2597C57025179D6546B15549AB35EA80F87B45F34C86873E364BA19E92012`、测试DLL `E9E7453EC78DF17F5365F2EEB008A2D41DDEC7998D8C2427CF269248D5324F7F`的[旧048](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/styled-unique-before/result.json)/[私有AE](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/styled-unique-after/result.json)均2/2、退出0。这证明该固定边界未回归，不证明任意宿主回调重入安全。[私有完整Headless](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/full-styled-after/result.json)179/179、Errors0/Failed0/Skipped0/NotRun0、退出0，包含原164及新13+2；不是最终整仓/包/API或原生预算验收。

以下Linux运行均为**Windows构建的私有功能Core IL在WSLg/X11运行**；[AE OnlyLong来源](../artifacts/linux-probe/e2a993e2cc99401f9bf6aa4f7cc5197d/source-snapshot.json)、[AE完整顺序来源](../artifacts/linux-probe/711121a804fa48ff81142036278443fe/source-snapshot.json)和[首版174BD来源](../artifacts/linux-probe/acf017537226466ea709375d97f9021d/source-snapshot.json)明确`LinuxSDK编译=false`，不能称候选已真实Linux SDK编译或正式平台通过。完整探索各阅读/长块10样本、短/长流各10会话、1预热；OnlyLong仅长流10会话/1预热，均`formal=false`。

| 私有Core及探索上下文 | 短流最早未显示至帧P95 ms | 长流全部实际提交UI+同步布局P95 ms | 长流Complete P95 ms | 长流实际提交/可见批次 | 结果 |
| --- | ---: | ---: | ---: | ---: | --- |
| [174BD完整11组](../artifacts/linux-probe/acf017537226466ea709375d97f9021d/report-1.json) | 147.6107 | 59.3403 | 553.9415 | 348/348 | 整体失败，长UI超50 |
| [AE OnlyLong](../artifacts/linux-probe/e2a993e2cc99401f9bf6aa4f7cc5197d/report-1.json) | 未运行 | 35.3019 | 425.7709 | 438/438 | 此独立单组探索通过 |
| [AE完整11组](../artifacts/linux-probe/711121a804fa48ff81142036278443fe/report-1.json) | 167.7280 | 60.2002 | 707.0296 | 348/348 | 整体失败，短流超150、长UI超50 |

OnlyLong的10次Complete最大值也是425.7709ms；完整顺序仍有真实失败，不能以单组通过代替全部场景，或把不同上下文与不同产品拼成正式轮次。上下文差异仍待新的Full/Only分项profile定位，当前没有据此确认GC、服务、JIT或render的唯一原因。官方048的Windows9414五轮通过、真实Linux7E7816的97e正式首轮失败各自保留；候选须在修改通过后重新集中验证、真实Linux SDK编译及新冻结原生采样。预算不变，整体M6未过。

### 本轮身份复用与字体布局定位（私有证据）

解析、身份匹配和布局分别登记，不能把一次完整会话的改善全部归因给解析缓存。默认parser仍每次执行完整Markdig解析，只复用已完成且安全的归一化DTO；引用/脚注等全局依赖保持保守失效，重复节点的匹配决定与fresh ID分配次序不变。私有matcher仅在原匹配成功之后，确认kind、源码/文本范围、文本、metadata引用、inline backing及递归子节点均相同，才复用旧不可变DTO；其它情况按原结果生成新DTO，不增加旧snapshot或AST强引用。

[同DLL七项before](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/matcher-seven-before/result.json)为5/7，[after](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/matcher-seven-after/result.json)为7/7；测试DLL `C0DB621A9FC60003548D6B9DC4B647F9267C74337F7AD6B6DCC6DA7827921B72`固定。覆盖singleton/general、list/table、重复节点数量变化、source/text位置漂移及迟到引用/脚注全DTO与ID关系。私有Core `123DC43E…`的[完整214项](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/core-final-after/result.json)0 failed/errors/skipped/notRun、退出0；[完整179项Headless](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/full-matcher-after/result.json)亦通过。它们是私有功能证据，尚未计入官方870或最终包消费。

失败按原次序保留：[Core首次](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/core-after/result.json)误用了小组探针30秒监督期限，timeout且未生成完整测试计数；完整Core既有运行约41秒，后续120秒是整组功能运行的外层监督期限，未改任何原生性能预算。[第二次](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/core-120-after/result.json)213/214，新夹具把原始脚注label `^n`误期望为`n`；只修正夹具后取得214/214，旧结果及原夹具均保留。[matcher完整Linux探索](../artifacts/linux-probe/fab651b81ee642f98d8ce7f7da05354b/report-1.json)仍失败：短流P95151.391ms、长流全部实际提交UI P9559.0149ms，Complete P95529.125ms通过。该Core来自Windows IL，LinuxSDKProductCompiled=false，不能算真实Linux候选编译或正式五轮。

随后使用同一真实Linux SDK构建的四DLL分别跑完整顺序与OnlyLong分项，追加纯计数/样式元数据，未记录源文本、未改变glyph解析。[精确元数据对比](../artifacts/markdown-ui-profiles/0bbd58641a3c43499085150ebe9ff840/text-layout-metadata-comparison.json)按SourceVersion+SourceLength关联每个实际UI窗口，无错配或窗口外调用。完整顺序345次实际提交的UI P9559.1420ms；同形两字符标题、字号26、字重600、同字体/首run属性与1150DIP约束，Measure单次P50由OnlyLong的0.0167ms/2,512B变为完整顺序的2.2623ms/228,536B；Arrange对应0.0053ms/2,224B与1.1144ms/115,888B。差异集中在字重600，不能据此改默认字重、字体或选择较快场景替代完整顺序。探针开销保留，阶段时间不相加，GC暂停不从预算扣除。

[公开字体缓存入口微探针](../artifacts/markdown-ui-profiles/a47a2d3c817c4653a4a093232e467a80/runs/full-20261014T145055Z-85u8FC/font-cache-summary.json)观察512行：请求为600，而真实已shape字体报告700，且family是composite来源；ExactKeyMatches=0、RegistrationAttempted=0、Errors=0。该方向因此没有登记别名，也没有把观察到的700字体写入600请求键。后续私有方案在单个TextPresenter内复用同一公开样式的GenericTextRunProperties，让框架自身延迟glyph缓存继续生效：最多32键，包含baseline、字体完整来源、大小、前后景/装饰/features/culture引用；每次请求删除未使用项，Owner/文档/FontManager变化及卸载清空，文档/manager仅弱引用；换上下文重新复制公开样式，避免旧properties的内部缓存跨上下文。非零LineSpacing继续原生base路径；自定义run/property与换行marker原样保留，没有复用TextLayout、shaped buffer或字形几何。

该properties首版[编译失败](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/font-property-compile-failure-1.json)为不可公开设置LineSpacing，RuntimeExecuted=false、CachedDllUsed=false，未冒用缓存程序集。修正后的[182项before](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/font-properties-before/result.json)、[182项after](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/font-properties-after/result.json)和[上下文失效后182项](../artifacts/markdown-range-prototype/a102078266ca437a9aeb1826c55d561e/font-properties-context-after/result.json)均退出0，后者功能SHA `B0A9DADFC5EA9EEF1F3BAEE56BDB66E73DB028159BE1E74E418999435182E60D`。新增三项使用公开原生TextBlock做几何/命中/样式差分；before/after测试程序集不同，因此不称“同DLL”证据。这仍是私有功能回归，不是最终软件整轮、实际Linux SDK候选或正式原生预算通过。整体M6继续，正式五轮只在最终同一产物上重新运行。

[properties候选完整11组探索](../artifacts/linux-probe/028f5d059c97437a9bb4d2fa8560b173/report-1.json)随后全部通过、error=null，保留原预算：阅读/长块各10样本、短/长流各10会话及1预热；短流最早未显示至帧P95129.773ms，长流402次全部实际提交UI+同步布局P9545.8342ms、Complete P95553.445ms。其[来源](../artifacts/linux-probe/028f5d059c97437a9bb4d2fa8560b173/source-snapshot.json)明确功能为B0A9DADF私有Windows IL、LinuxSDK编译=false，formal=false。该结果支持把候选送入官方全量验证，不代替最终真实Linux SDK编译、包/API重验或任一平台的五轮；旧97e及所有私有失败继续保留。正式通过后立即推进M7，其设备/剪贴板缺项按实保持待验证。

### 执行与守卫

先由集中构建产生Release输出，然后隔离完整输出，避免共享构建替换运行中的程序集。Windows正式五轮入口与Linux入口均保存原始报告；遇预算失败保留报告并停止后续正式轮次，先分析原因。Linux `-Formal` 要求五进程且完整场景；pilot默认仅3样本。

最终验证按以下顺序执行，同一平台的五轮必须使用同一个完整二进制副本、主题/高亮程序集hash、语料与字体环境，不能拼不同源码版本的轮次：

1. 集中回归、独立包消费通过后冻结源码；复制完整输出，保存三个产品包及宿主hash。真实Linux SDK另复制完整最终源码、locked restore/Release编译、跑11项目，再用该编译输出验证原生X11。Windows构建IL在WSLg的pilot单独标记。
2. 两平台各跑完整pilot，包含1MiB长流预热和完成、长单块交互、10k块与资源。发现语义错误或门槛失败时先修复并产生新hash，保留旧报告。
3. 必要时单独执行每阅读组100/10和流式100会话/10预热定位。读取每批parse/UI/layout/接收至帧、Complete与四区数据；这些局部场景不能当整套验收。
4. 平台分别运行11个原生场景、五独立进程，每轮阅读/长块至少100样本、流式各100完成会话及至少100批；每场景10次完整预热。1MiB长会话每轮包含110次完整会话，可能约10分钟，宿主每10会话输出真实进度；结果文件和完整守卫在轮次结束生成。

正式采样期间暂停同机共享build、测试、额外原生窗口和profile探针。Windows与WSLg分别判断预算；实体Linux/麒麟和物理辅助工具仍须独立设备证据。

```powershell
./benchmarks/Markdown.Benchmarks/NativeHost/Run-NativeBaseline.ps1
./benchmarks/Markdown.Benchmarks/NativeHost/Run-LinuxNative.ps1
./benchmarks/Markdown.Benchmarks/NativeHost/Run-LinuxNative.ps1 -Formal
./benchmarks/Markdown.Benchmarks/Run-ModelUpdates.ps1
./scripts/Tests/Test-MarkdownPerformance.Tests.ps1
```

`Test-MarkdownPerformance.ps1` 从五份中文数值/布尔原始报告重新计算11个场景的最近秩P50/P95和UI批P95，并核对固定预算、独立进程、相同功能/主题/高亮/宿主程序集与语料/字体匹配候选、采样/预热数、流式不可重写的最早接收时间/最终完成时间与原始样本一致、所有长会话实际提交与四区汇总、长块交互及逐文档分配、虚拟化范围、共享服务/旧回调/释放计数。不能只信报告`全部通过`。脚本合同测试的合成报告只用于验证拒绝行为，保存在`artifacts/guard-tests`，不能用作渲染测量证据。

本轮用户51ms政策使用[独立修订守卫](../artifacts/markdown-budget-revision/898ee48bc3f547679aecb9893e876024/source/scripts/Test-MarkdownPerformance.ps1)，需显式传入`-LongActualUiP95BudgetMilliseconds 51 -ProducerLongActualUiP95BudgetMilliseconds 50`；默认路径仍按50ms执行。它仅对长会话ALL实际及Rendered UI两处重判政策，不改producer原50ms声明、原旗标、原raw或其它预算；原producer整体与两处旗标必须与raw重算结果一致，才能继续完整receipt、资源及其它验证。其[134项合同实际执行](../artifacts/markdown-budget-revision/guard-contract-20261015-715d222931f642219117de5749ec413f/results.json)为134/134、Exit0；准备记录与实际执行证明分别保存。

2026-10-14该阶段[98合同用例](../artifacts/guard-tests/markdown-2f0cd047fc4d4b98b110ed35e4171afc/results.json)全部通过，guard SHA `BC9244105AF7F0F41E33722D89151D5287A71C0C6E58549B2EC4EA3D8596ED00`对应当时源文件；后续TimeSpan精度修正已由[109合同用例][active-timespan-tests]核验，当前守卫SHA为71094D15，旧98记录按历史保留。包括一致伪造latest替earliest、漏receipt、旧/未来chunk覆盖、覆盖数量/字节/版本/结束位置、Frame metric缺实际提交，以及长块每项门槛、all-commit UI50、资源4/256、空四区/零分母和三个产品/宿主hash。历史[44](../artifacts/guard-tests/markdown-f6d06987de3240f1b8a911d5c7f05ef7/results.json)、[50](../artifacts/guard-tests/markdown-958b14a281e140aea80663d5d681cbaa/results.json)、[68](../artifacts/guard-tests/markdown-93428ca5e76a4ecebe2b4a84153ca554/results.json)、[84](../artifacts/guard-tests/markdown-6407089d8ab7474fb8e1cfe139ee0935/results.json)记录保留。96格式首轮的covered-bytes伪造同时超过已绘制bytes，守卫正确拒绝但测试期待了更后面的错误字符串；夹具改为仅伪造覆盖差值后98通过，原失败目录保留。合同数据不能作为性能测量或预算通过证据。

## 可用 Linux 环境

2026-10-14 初次只读核验：Ubuntu26.04 LTS、WSL2 / WSLg、Linux6.6.114.1-microsoft-standard-WSL2 x86_64，DISPLAY=:0、WAYLAND_DISPLAY=wayland-0。最初仅有工作区 `.NET 10.0.12` runtime，没有Linux SDK；上述四版原生pilot及首个11项目回归均运行Windows构建的net10.0 IL，不能据此报告“Linux编译通过”。

后续按已授权的必要开发依赖安装，在**全新工作区**`artifacts/tools/dotnet-linux-sdk`离线解压官方固定10.0.401 linux-x64 archive；下载HTTP200、240,059,572字节，SHA512完整匹配[官方发布metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json)。[本地固定URL/hash](../artifacts/tools/dotnet-linux-sdk-metadata/10.0.401-linux-x64.json)和[实际SDK信息](../artifacts/tools/dotnet-linux-sdk-metadata/sdk-info.txt)记录SDK10.0.401、MSBuild18.9.11、Host10.0.12和linux-x64。没有修改系统、PATH或现有runtime。SDK已可运行，但真正Linux编译仍须独立build结果确认；入口`Run-LinuxBuild.ps1`复制完整源文件和hash清单、集中配置/锁文件，使用独立NuGet cache、CLI home、temp及每项目obj/bin，执行locked restore、Release全slnx build，再运行11项目。app-local ICU通过[官方支持的进程环境变量](https://learn.microsoft.com/en-us/dotnet/core/extensions/globalization-icu#app-local-icu)加载；不以invariant模式冒充中文/Unicode测试。

首次真实Linux SDK全slnx Release已完成，**0warning/error**、locked restore通过，build耗时3分21秒。证据位于`artifacts/linux-build/68350f908ae04c17a94f8f295c665a8a`的`build.log`、`restore.log`、源文件hash清单；Linux功能程序集SHA `73F8B16DD587286AD0E08A38DEB427F60BB11AD77363F8C12D14B39EA3DBAACD`。初次副本漏历史API文档导致5项夹具失败；入口已补docs/scripts/.github，保留原编译程序集和日志后全11重跑，[结果](../artifacts/linux-build/68350f908ae04c17a94f8f295c665a8a/retest-result.json)为**670/672通过，2失败**：Markdown Core88中定义only生成空FootnoteGroup；Headless88中非合作高亮服务仍运行时旧Reader不可回收。Streaming60、Themes7、TextMate13及既有6项目416均通过。根分支后续修复不自动改变这个隔离快照的结果，需要新版本重测；当前不宣称Linux整套测试通过。

后续完整源码快照 `artifacts/linux-build/a8ed9053714549f8b6172a9f601aba8b` 再次实际Linux SDK locked restore/全slnx Release，0warning/error、3分18秒；产品源码hash清单与当时Windows ED745B冻结源码相同，Linux功能DLL SHA `7B94190CE694B6F46A2893E571BC1376D6E1F5445B38763BD721052E18BEAE7E`、宿主 `F67C67FFC61DCB3DF3C10767A1F8341312468205CEF9AE5FC8A98689F413E124`。平台编译产物hash分别记录，不要求与Windows DLL相同。[全11项目结果](../artifacts/linux-build/a8ed9053714549f8b6172a9f601aba8b/result.json)797/798通过，唯Headless138的拖选timer scope用例在Flush遇框架已释放TestContext，Core163、Streaming60、Themes8、TextMate13及既有416全部通过。

仅同步两个Headless harness源文件、`BuildProjectReferences=false`复编测试0warning/error后，[两次复验](../artifacts/linux-build/a8ed9053714549f8b6172a9f601aba8b/retest-context-26e78530641d42be8ffa6ad76e62bc6c/result.json)产品/宿主四个hash未改变：首轮Total12/Errors100/Failed0，框架cleanup idle TestContext；第二轮138/138、Errors0。同DLL的成功不能抹去首次失败。复用该已核验SDK与隔离cache，不另安装SDK。

错误恢复与neutral接收修复后的最新冻结产品源（Windows功能1B6523、宿主51D018）已复制新GUID `7b37264619ea4225bbdbd84fdde263bc`，与当前产品源0diff。真实SDK locked restore/全slnx Release0warning/error、2分55.80秒；完整Headless夹具仅重编测试0warning/error、10.16秒。[11项目805/805及两次独立145复验](../artifacts/linux-build/7b37264619ea4225bbdbd84fdde263bc/retest-final-fixture-9b3dc692a8bb45a8bf97e054052d8d28/result.json)全部Errors0/Failed0/Skipped0，产品/宿主四个hash不变。Themes也改为公开Session最终夹具后，test-only编译0warning/error、6.90秒，[全11再次805/805及Themes两个独立8/8](../artifacts/linux-build/7b37264619ea4225bbdbd84fdde263bc/retest-final-themes-7d76ab4032f84ce6a0b037b2a823347d/result.json)全部Errors0/Failed0/Skipped0，四个产品/宿主hash仍不变。Linux功能SHA `D6022156031E461CF9C159307F222F2053B2DC5EC890D9B654C12BF977199571`、宿主 `E5CA80FD57C8D5C1FB893B8CABFD682847A410BFFDFC39D3561B8F2E5B150DF1`分别记录，不能与Windows DLL hash拼轮。此结果是回归测试，不是原生预算验收；旧670/672、797/798和harness错误原样保留。

真实Linux SDK全套通过后执行 `NativeHost/Run-LinuxMarkdownSmoke.ps1 -SourceBuildReportPath <passed-build-result.json>`，核对Demo产品hash并隔离完整输出；未通过全套的旧build报告会被拒绝。首次最新[Linux编译Demo原生报告](../artifacts/platform/linux-markdown-smoke-dc47816134814f6b822737ba0b6f5ad7/result.json)已保存JSON/PNG/ExitCode：40项页面/服务/图标/file/avares/HTTP检查true、错误null，原剪贴板无文本而格式非空，安全保留并跳过复制，退出1，不能记为完整43项或真实剪贴板恢复通过。该平台smoke与原生性能分开，不能用perf替代Clipboard/生命周期证明。

Viewport同步修复/FrameWitness诊断版本当时的真实Linux[完整源码与808回归](../artifacts/linux-build/7cc589e63c24417881d1fe614f5da928/result.json)locked restore/全slnx Release0warning/error、4分00秒，11项目808/808、Errors0/Failed0/Skipped0。Linux四个hash：功能`CDB73D8C0F3F7E8299689DB7A1914FE23A9E5A8EA3E9E294D53DA8E2FF641732`、主题`CA8A98BBB368C5D71CA90CBAC5E4055A5CFCD953BEF2090ACE40FA9E83529A80`、TextMate`57978E5F2AAE8C53982F011EFF558F110DFF161DD51B349EAF5E82968484A590`、宿主`99B9B5B680496FC32A74E1F79F8B7DB6263CD8C86579174EA8894116C7AC0712`；该版不能与后续产品或Windows IL拼轮。该版[Demo原生报告](../artifacts/platform/linux-markdown-smoke-e70d5b21251d4c7eb6ea705793575954/result.json)再次40功能/协议true、剪贴板安全跳过、退出1，仍不记为完整43项通过。

最终DA11源码另行[完整Linux SDK编译与840回归](../artifacts/linux-build/8df8090ad9b844a096f573b154f4a91b/result.json)locked restore、全slnx Release0warning/error、3分31.17秒，11项目840/840且Errors0/Failed0/Skipped0/NotRun0；[实际日志计数](../artifacts/linux-build/8df8090ad9b844a096f573b154f4a91b/regression-summary.json)与469文件manifest保留。Linux功能/主题/TextMate/宿主SHA分别为`6C30CB601097599FEBD4E4E2CFF11C0AA4E6D8795996B8C5FC92CEA94E09D2C1`、`E98394C6672871469691AE9C2E4D300FC46DA8B6B3665AD7F546D4E88CA0973F`、`7A226EDB486C33A58D192A29A4B167954953A6909119C388F5D294DFEAEE703C`、`29FD4C13132085927CE7E10F475F3EB14A4855E9466C287DFD063AD1DB979B85`，后续最终Linux性能使用这份真实输出。旧7cc808及旧预算失败保持原样。该产物[最新Demo原生smoke](../artifacts/platform/linux-markdown-smoke-11a1a9e386d248f3b4aa8bc1351f097f/result.json)40功能/协议true、错误null，剪贴板无可读文本而安全跳过，退出1；PNG已工具目视，不能宣称完整43项或真实复制/恢复通过。

后续048同源的新快照[Linux SDK编译结果](../artifacts/linux-build/5a681321a91349dda6bace76a80eb43d/result.json)已实际通过：SDK10.0.401、locked restore、全slnx Release0warning/error，build3分18.06秒；[11项目日志汇总](../artifacts/linux-build/5a681321a91349dda6bace76a80eb43d/regression-summary.json)870/870、Errors0/Failed0/Skipped0/NotRun0。Markdown为207/60/164/10/13共454，既有六项目416。[477文件源清单](../artifacts/linux-build/5a681321a91349dda6bace76a80eb43d/source-files.json)SHA `B84D71BEC4FE7E263DC95273ACE0B5044CF034AD5921FA8359B90B3860C3183E`；Linux功能/主题/TextMate/宿主SHA分别为`7E7816CE7936C1131AB1B79C67F4615FCEA09FE001E020A86853C405E1F77574`、`9C8ED9675986239C0F945F7CB0A10173FF227A6123E9D62FDD88F9B7F547AA2D`、`DE4738D44A4D8364715AF4F6CF3DA9E3FC84DE99EB06F3F36844AD327057DFC1`、`E7DD2A87097EBFF05F12950A4853129AC592F4B7203BA98B57DF8DF1093A1690`。这些是新的真实Linux输出，不能复用Windows048程序集hash或旧DA11/840平台结果。

该输出的[Linux完整pilot原始报告](../artifacts/linux-probe/b25b8a2fdf9441f3bc2502a5f7dd365d/report-1.json)收齐11场景后整体失败、错误null；[汇总](../artifacts/linux-probe/b25b8a2fdf9441f3bc2502a5f7dd365d/summary.json)明确`formal=false`。各阅读/长块3样本/1预热、短流1完整会话、长流3完整会话/1预热：1MiB纯文本/TextMate UI P9556.1440/57.2149ms超过50ms；短流最早未显示至帧P95173.2480ms超过150ms，Complete P9584.0173ms通过；长流全部实际提交UI+同步布局P9575.5239ms超过50ms，Complete P95548.1482ms通过。长会话最早未显示至帧P95574.8616ms仍仅为诊断。失败完整保留，不能因Windows五轮通过或pilot样本少而略去，也不更改预算。

新Linux[正式首轮冻结来源](../artifacts/linux-probe/97e74dcd972349b3b28b216f3b97d388/source-snapshot.json)核对上述四个真实SDK产物，在WSLg/X11运行；[完整原始报告](../artifacts/linux-probe/97e74dcd972349b3b28b216f3b97d388/report-1.json)已收齐11场景、错误null、整体false，实际退出1。[首轮汇总](../artifacts/linux-probe/97e74dcd972349b3b28b216f3b97d388/report-1.summary.json)与[完整父日志](../artifacts/linux-formal-7e7816-20261014.log)保留：七个阅读/长块组各100样本/10预热；1MiB纯文本/TextMate UI P9552.8128/56.1590ms超过50ms。短流100会话/625个可见批次，最早未显示至帧P95177.6524ms超过150ms；Complete P95116.6806ms、最大147.9714ms，Complete每次500ms指标通过，短流组仍因最早至帧超预算而失败。

长流100完整会话/10预热、3119次实际提交与3119个可见批次均已收齐；Complete P95610.2483ms、最大651.5183ms，Complete每次1500ms指标通过，但**全部实际提交UI+同步布局P9574.9159ms超过50ms**，完整首轮失败。长会话最早未显示至帧P95579.3360ms仅作诊断，不套短流150ms门槛；实际提交没有用已绘制子集代替。100个虚拟化样本与100次替换/卸载资源检查全部通过，旧Reader/lease、活动服务请求、当前失效订阅、当前Reader活动资源及旧更新迟回调均0，实际parse/pending峰值1/1、资源执行/排队峰值4/0。失败后停止第2–5轮，没有五报告通过守卫结果。Linux固定预算未通过，预算未改，整体M6继续；软件回归与资源检查通过不能抵消性能失败。

复用既有 `artifacts/tools/dotnet-linux` 和 `artifacts/linux-probe/99146fc3e4e34c9eac9c3dafb84a2984` 的进程范围依赖：app-local ICU72.1.0.3、libICE/libSM、本地Noto CJK/fontconfig。各新probe复制完整bin输出（含deps/runtimeconfig与runtimes），只修改副本的AppLocalIcu配置；通过进程DOTNET_ROOT、LD_LIBRARY_PATH、FONTCONFIG_FILE、XDG_CACHE_HOME加载，不安装系统包或修改用户字体设置。来源/哈希沿用 [既有环境证据](validation/ubuntu-wsl-keyboard-environment.json)，新运行仍需记录实际产品hash、字体、native handle和backend。WSLg不替代实体Linux或麒麟。

首次独立11项目回归probe为`artifacts/linux-probe/0b031acb062342c68007214eff5ba259`：既有6项目416/416通过；Markdown Core31/31、Streaming41/41、Headless46/48、Themes7/7、TextMate13/13。两个失败为图片上方高度改变后的滚动锚点、离屏链接BringIntoView。该结果属于复制时二进制，后续修复后需要重新同步再运行全套；不能用当前源码状态覆盖失败证据。`Run-LinuxRegression.ps1` 已确保失败后继续所有11项目并保留日志。

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
