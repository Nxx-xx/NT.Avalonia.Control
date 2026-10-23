# Markdown 平台验收记录

当前M7可执行软件范围已通过，见[本次M7收束记录](markdown-m7-completion-20261015.md)及[新M7执行记录](../artifacts/markdown-m7-completion/c63fab7542ba4b22b9a63d9cbb736f1b/m7-exit.json)。Windows43项与Linux私有Xvfb/X11新43项真实复制、读回及恢复通过，实际producer/Main/外层均退出0；原WSLg共享显示的安全Pending保持历史记录，两个环境分别记账。

Windows50ms、Linux51ms及身份、引用、脚注语义不变；本次增补按同一冻结源码和产物SHA复用五轮、544×2、416×2、包/API及85/134工具合同，不声称再次执行。远端原生CI由用户暂缓（DeferredByUser）；麒麟、实体Linux/原生Wayland、完整DPI/多屏、物理输入和真实辅助工具继续待验证。未发布、未改版本。

## 此前冻结候选与阶段记录

以下保留当时结论及原始来源，当前结果以上方revision2记录为准。

当前冻结源码：Windows [d2f591最终Verify](../artifacts/verification/d2f591e51482493c9b62b4f642af789e/results.json)907/907、七包及全部隔离消费通过，387软件源前后相同；实际Linux SDK [6451回归](../artifacts/linux-build/6451a248de3948f8baab4ee5f6ba0e0c/regression-summary.json)同为907/907，[387共同软件源](../artifacts/linux-build/6451a248de3948f8baab4ee5f6ba0e0c/windows-source-comparison.json)逐条相同，无例外。Windows F832[原50ms五轮守卫](../artifacts/benchmarks/markdown/windows-formal-f8324d-fcba9d4c612443c0bb682cd462532d5/guard.json)与Linux A50F[显式51ms五轮守卫](../artifacts/linux-probe/dfc8c8cdb68349dda6f37615eea4c319/guard-policy-51.json)均实际通过；两平台当前Demo各43项通过、Exit0，真实文本剪贴板复制及恢复均true。134项修订守卫合同已实际通过，M6在本轮软件及已测原生组合范围内通过；M7软件验收与归档完成，整体设备矩阵仍有待验证项，版本未发布。

用户在2026-10-15明确授权仅将长会话UI+同步布局P95的ALL实际提交与Rendered子集两处改为51ms，其余预算与Same容差不变。Linux[第4轮原始报告](../artifacts/linux-probe/dfc8c8cdb68349dda6f37615eea4c319/report-4.json)在原50ms下仍为false，ALL与Rendered UI P95均50.6962ms；新的显式51守卫保留并核验原producer50字段和raw SHA，再独立复评五份实际报告。Windows五轮保持原50ms通过。EDB5/7B29旧896项、旧Windows52.5111ms失败及Linux剪贴板不可读时的安全跳过均按历史产物保留，未转签为新版本证据。

记录日期：2026-10-15（保留2026-10-13/14历史证据）。技术基线为 .NET SDK 10.0.401、net10.0、Avalonia 12.0.3、Markdig 0.42.0。平台支持声明只覆盖已取得相应证据的组合；当前M7可执行的软件验收与归档已完成，整体平台设备矩阵尚未完成；完整DPI、物理输入、辅助工具和麒麟均待验证。Published=false、VersionChanged=false。

本轮[134项守卫合同](../artifacts/markdown-budget-revision/guard-contract-20261015-715d222931f642219117de5749ec413f/results.json)实际134/134、Exit0，默认50ms的109项原合同与显式51ms的25项新增边界/伪造拒绝用例全部通过；[源码落地](../artifacts/markdown-budget-revision/landed-20261015-6f46fd660c214d09a14ab14a2a719bfe/source-transition.json)仅修改validation脚本。M7[软件证据归档](../artifacts/markdown-release-evidence/13c8c03113e64064ba54b7580cb620fb/manifest.json)实际SoftwareEvidencePassed=true、RequiredSoftwareHashChainPassed=true；[51政策独立附录](../artifacts/markdown-release-evidence/13c8c03113e64064ba54b7580cb620fb/policy-support-eb118238cf2b46b2817b815849dd62a7/policy-support-manifest.json)完成并保留原50ms false，未改旧387/B5证明或产品/宿主DLL。以上不等于发布或缺设备项目通过。

## 平台清单

| 平台与显示组合 | 构建与逻辑测试 | 原生阅读、布局与复制 | 缩放与输入 | 状态 |
| --- | --- | --- | --- | --- |
| 本机 Windows x64，缩放1 | F832 Release0warning/error、11项目907/907、七包及76/78 Markdown隔离消费通过，387软件源前后核验一致 | F832五独立进程按原50ms守卫通过；08ba冻结Demo组合43/43、Exit0/noTimeout，真实文本复制及恢复true | 实际窗口与文档区域见各原始报告；物理输入/辅助工具另验 | 当前软件、五轮性能、原生smoke及134合同通过，M7软件归档完成；未测设备组合保留待验证 |
| Windows 125% / 150% / 200% | 不以缩放 1 的测试推断 | 待逐组合运行 | 中文/Emoji/RTL、选区命中、键盘、剪贴板分别验证 | 待验证 |
| Ubuntu26.04 / WSL2 / WSLg，Avalonia.X11，x64，缩放1 | 6451实际SDK整solution Release0warning/error，11项目907/907（Core221、Headless187）；387软件文件与Windows完全相同 | A50F/Host2905五独立进程按显式51ms长流UI政策通过；真实Linux编译Demo43/43、Exit0，真实文本复制及原文本恢复true；R4原50ms失败保留 | Linux6.6.114.1、.NET10.0.12、Noto CJK/fontconfig环境已记录；WSLg/XID不代表实体X11或Wayland | 当前回归、五轮性能和原生smoke已有证据；实体设备与未测组合仍待验证 |
| 独立Ubuntu实体X11桌面 | 不以WSLg替代实体桌面 | 未取得本控件原生平台smoke及剪贴板/系统链接证据 | CPU、字体、桌面、多屏/DPI须逐设备记录 | 待验证 |
| Linux原生Wayland后端 | WSLg有WAYLAND_DISPLAY不等于应用使用Wayland后端 | 当前探针backend为Avalonia.X11；Wayland另验 | 不能把XID/X11结果标为原生Wayland | 待验证 |
| 麒麟目标设备 | 未提供设备；发行版、版本、CPU 和 .NET 运行时条件未确认 | 不复用 Windows/Headless/其它 Linux 结果 | 显示后端、中文字体、剪贴板和辅助工具须真机记录 | 待验证，未声明支持 |

## 已取得的自动化证据

| 验收范围 | 结果与证据 | 不能据此推断的能力 |
| --- | --- | --- |
| 本轮两平台冻结软件回归 | Windows[d2 Verify](../artifacts/verification/d2f591e51482493c9b62b4f642af789e/results.json)及Linux[6451实际日志](../artifacts/linux-build/6451a248de3948f8baab4ee5f6ba0e0c/regression-summary.json)各907/907、failed/errors/skipped/notRun均0；387共同软件输入逐文件无例外同SHA，Linux完整485项来源独立保存 | 不宣称并行PDF修改后的整个live工作区仍等于冻结Verify；不代替设备验收 |
| 本轮两平台五轮原生性能 | Windows[F832原50ms五轮](../artifacts/benchmarks/markdown/windows-formal-f8324d-fcba9d4c612443c0bb682cd462532d5/guard.json)通过；Linux[A50F显式51ms五轮](../artifacts/linux-probe/dfc8c8cdb68349dda6f37615eea4c319/guard-policy-51.json)通过，原R4 false保留；每平台同四DLL、五独立进程、100样本/100会话和10预热 | Linux不是原50ms五轮通过；原生Window计时不认证scan-out、实体Linux、麒麟或物理输入 |
| 本轮两平台原生smoke与剪贴板 | Windows[08ba/ed4fe报告](../artifacts/markdown-m7-preparation/08ba3007b2fd4d09bf73a98cc62d4763/windows-smoke-ed4fe48322da4558b2b239c671918809/report.json)和Linux[A50F证据](../artifacts/platform/linux-markdown-smoke-a50f-00ca24d5178d454eb29e563d8887c157/evidence-summary.json)各43/43、Exit0、复制及原文本恢复true | 程序路由输入不是物理键鼠；PNG为原生Window的RenderTargetBitmap，不是桌面截图或全部中间状态截图 |
| 历史048源码完整软件验收 | [Verify19b1](../artifacts/verification/19b1d0d43b0e472aac15d0fdd2d4d48d/results.json)退出0：870/870、0failed/errors/skipped/notRun，七包与原控件隔离消费均通过；Markdown76/78含各18项内嵌LF/CRLF AXAML，379文件启动/完成哈希一致；同源同测试DLL的[硬换行5/5](../artifacts/markdown-hard-break-regression/729e01e7ea0e47fb8ec2701eb3167494/after/result.json)另存 | Windows软件回归不替代当前Linux SDK、原生五轮、设备、DPI、物理输入或辅助工具证据 |
| MT07–MT11，流式纯数据与有界调度 | `Markdown.Streaming.Tests` Release xUnit v3：41 项通过，0 跳过；9 份对抗语料逐个两块分割点，12 个种子随机多块；受控慢解析、过期解析/UI 提交、尾批、并发追加、完成/取消/错误与生命周期 | 真机布局、DPI、剪贴板、字体外观和辅助工具 |
| MT23，实际 Avalonia TwoWay 绑定 | `StreamingBindingTests`：同值回写保留会话/选区/绑定；VM setter 转换为外部新文本及属性事件重入均取消旧会话，旧 `Updated` 不发布；后续 VM 仍可更新 | 这是 Headless 的实际 Binding 集成；不是独立 AXAML 消费者、真机字体或剪贴板证据 |
| MT24，宿主完成边界 | Headless：可见完成后原生 `TextLayout` 有有效高度；隐藏完成返回布局待显示；未挂载/缺模板明确错误；延迟本地图片不阻塞最终文档完成 | 远程图片已下载、真实帧呈现时延或所有平台布局均已验证 |
| MT10/MT13，图片与阅读组合 | `ReadingAnchorTests` 4 项通过：视口上方图片增高保留首可见块和偏移；上滚暂停跟随、回底恢复；未变图片在其它块追加时保留同一请求；最终提交不等图片 | 联网图片、SVG、所有字体/DPI/多屏组合 |
| Markdown Headless 集成汇总 | 集中 Release 运行：35 项通过，0 跳过；包含上述8项以及15项坐标拖选/字素/链接/代码按钮/表格对齐/空文导航 | 不把模拟剪贴板或 Headless 当作 Windows/Linux/麒麟真机结果 |
| Linux真实SDK构建与全套回归，2026-10-14旧快照 | SDK10.0.401实际locked restore、全slnx Release编译0warning/error；补齐历史API夹具后11项目670/672通过。两失败为空FootnoteGroup及非合作高亮服务未完成时旧Reader回收；[结构化结果](../artifacts/linux-build/68350f908ae04c17a94f8f295c665a8a/retest-result.json) | 此旧snapshot的失败不能用后续源码/Windows测试覆盖；最终Linux同步复验待完成 |
| Linux真实SDK最终冻结产品与完整夹具 | SDK10.0.401全slnx Release0warning/error，最终完整夹具11项目805/805；Headless145同DLL三次独立复验通过，Themes8两个独立进程通过，全部Errors0/Failed0/Skipped0；[最终11项目](../artifacts/linux-build/7b37264619ea4225bbdbd84fdde263bc/retest-final-themes-7d76ab4032f84ce6a0b037b2a823347d/result.json)、[Headless三次](../artifacts/linux-build/7b37264619ea4225bbdbd84fdde263bc/retest-final-fixture-9b3dc692a8bb45a8bf97e054052d8d28/result.json) | 回归不是性能首屏、物理输入、剪贴板或实体Linux/麒麟证明 |
| 最新Linux真实SDK与Viewport同步修复 | SDK10.0.401 locked restore、全slnx Release0warning/error、4分00秒；11项目808/808，Errors0/Failed0/Skipped0，含3项单次布局跳转用例；[最新完整结果](../artifacts/linux-build/7cc589e63c24417881d1fe614f5da928/result.json) | 同上；新原生性能仍须独立五轮 |
| TextMate native tokenizer Linux加载 | Windows IL在Linux runtime及真实LinuxSDK编译产物各运行13/13通过；语言别名、中文/Emoji UTF16、CRLF、多行状态、取消与主题失效有实际测试 | 不代表所有RID、麒麟或语义高亮与VS Code一致 |
| 原生性能/回收入口 | Windows与WSLg/X11都实际取得native handle/render-thread、分项parse/UI/layout、10k滚动及共享服务回收数据；[性能记录](markdown-performance.md) | 9场景历史pilot、reading100单轮不能替代新增11场景host的五轮守卫；Linux预算和旧流式预算有失败，未宣布M6性能通过 |
| M6同步跟随提交与margin布局收敛修复 | 同一测试程序集下71E0产品11项2通过/9失败，A174产品11/11通过；完整Headless159/159，Errors0/Failed0/Skipped0；[实际before/after](../artifacts/markdown-following-probe/6471905ac7644cd5bfb1372c86b3e21b/results.json) | 两个before用例在append前触发margin循环，只证明该布局缺陷；软件回归不替代新hash原生预算、Linux同步复验或五轮守卫 |
| 最新DA11对应源码的Linux完整回归 | 真实SDK全slnx Release0warning/error、3分31.17秒；[11项目840/840](../artifacts/linux-build/8df8090ad9b844a096f573b154f4a91b/regression-summary.json)，Errors0/Failed0/Skipped0/NotRun0，含最终following与未闭合EOF错误恢复 | 真实Linux编译回归不是原生性能、剪贴板或麒麟证明；所有旧失败原样保留 |

表内2026-10-13的41/35等测试数是历史集中运行；新增用例的最终数量以最新Verify/回归日志为准，不追填旧输出。命令与完整输出由集中验收记录保存。本文的测试运行不是性能基准，不能将测试总耗时当作首屏或输入延迟。

## Windows 原生探针

运行入口保留既有 TreeDataGrid/VirtualKeyboard 的页面分发，同时支持 Markdown：

```powershell
dotnet run --project samples/NT.AvaloniaControl.Demo -c Release --no-build --no-restore -- --markdown-smoke --smoke-output artifacts/platform/windows-markdown-smoke.json --smoke-image artifacts/platform/windows-markdown-smoke.png
```

也可使用 `--page markdown --smoke`。探针在真实 Window 内验证模板、跨段落/代码/表格全选、跨线程流式完成与 MVVM 同步、外部替换、换肤、链接 Enter 交给宿主、图片阅读锚点、用户上滚/回底跟随、页面停用取消及重新激活。它移除 Demo 的自动外链启动处理器，只验证宿主链接事件。键盘事件通过应用路由模拟，物理键鼠和系统链接启动仍待验证。

JSON 使用中文字段及数值/布尔值，记录 OS、进程架构、运行时/Avalonia、配置字体、字号、渲染缩放、窗口和文档视口、每项结果、限制及错误。PNG 由可见原生 Window 的 `RenderTargetBitmap` 生成；它可以用于版面检查，但不是桌面截图或物理输入证据。配置字体名称不能单独证明实际中文字形回退字体。

真实剪贴板测试先读取原文本；若无文本，再读取格式清单以区分空状态和非文本内容。原状态为文本或为空时，复制并读回逻辑投影，最后分别恢复原文本或ClearAsync恢复原空状态并验证。原剪贴板为非文本或服务不可用时保留内容，JSON中 `剪贴板测试已执行=false`，该次不能作为完整通过。Clipboard/解析/完成等待有超时，失败写入JSON后关闭探针应用。

| 本机原生项目 | 状态 | 证据位置 |
| --- | --- | --- |
| 本轮F832 Window / 布局 / MVVM / 流式 / 锚点 / 局部服务 / 图标 / 图片协议 / 剪贴板 | 43/43 true、错误null、实际Exit0/noTimeout；真实剪贴板复制及原文本恢复true，Demo与正式产品组合的来源独立记录 | [报告](../artifacts/markdown-m7-preparation/08ba3007b2fd4d09bf73a98cc62d4763/windows-smoke-ed4fe48322da4558b2b239c671918809/report.json)、[监督](../artifacts/markdown-m7-preparation/08ba3007b2fd4d09bf73a98cc62d4763/windows-smoke-ed4fe48322da4558b2b239c671918809/supervisor-result.json)、[来源](../artifacts/markdown-m7-preparation/08ba3007b2fd4d09bf73a98cc62d4763/windows-smoke-ed4fe48322da4558b2b239c671918809/source.json)、[PNG](../artifacts/markdown-m7-preparation/08ba3007b2fd4d09bf73a98cc62d4763/windows-smoke-ed4fe48322da4558b2b239c671918809/preview.png) |
| 历史048 Window / 布局 / MVVM / 流式 / 锚点 / 局部服务 / 提示图标 / 图片协议 | 40功能/协议true，错误null；该次剪贴板无可读文本而保持原内容，测试已执行false，整体退出1；原PNG保留 | [历史JSON](../artifacts/platform/windows-markdown-smoke-cache-7bee07f6bf6f455ab2c77b2d9163d271.json)、[实际程序集](../artifacts/platform/windows-markdown-smoke-cache-7bee07f6bf6f455ab2c77b2d9163d271.source.json)、[历史PNG](../artifacts/platform/windows-markdown-smoke-cache-7bee07f6bf6f455ab2c77b2d9163d271.png) |
| DA11历史 Window / 布局 / MVVM / 流式 / 锚点 / 局部服务 / 提示图标 | following/EOF错误恢复DA11版本43项全部true、退出0；此前Viewport和增强28项记录保持 | [历史43项JSON](../artifacts/platform/windows-markdown-smoke-20261014-following-recovery.json)、[程序集与退出码](../artifacts/platform/windows-markdown-smoke-20261014-following-recovery.source.json)、[历史28项JSON](../artifacts/platform/windows-markdown-smoke-20261014-services-final.json) |
| 默认图片loader真实原生输入与释放 | file/宿主avares/127.0.0.1动态端口HTTP均解码实际3×2 PNG；验证独立租约/重复释放/loader释放保留调用方租约/宿主client归属/编码上限及解码后像素上限，回环4次请求、取消并关闭 | 同最新43项JSON；公网HTTP/HTTPS另验 |
| 原生版面 PNG | 最新PNG已通过工具图像目视：中文、标题、列表、引用、链接、高亮代码与复制按钮可见，无异常重叠或跨视口绘制；图像最终回基础页，不覆盖43个动态中间状态外观 | [最新PNG](../artifacts/platform/windows-markdown-smoke-20261014-following-recovery.png)；[历史28项PNG](../artifacts/platform/windows-markdown-smoke-20261014-services-final.png) |
| 真实剪贴板复制及恢复 | 最新43项复制/读回与恢复true；28项报告另证原空剪贴板ClearAsync恢复，早期文本恢复true另存elevated报告 | 最新43项、历史28项及早期报告剪贴板字段 |
| 真实键鼠拖选、中文/RTL 命中 | 待验证 | 人工步骤及平台记录 |
| 屏幕阅读器/文本范围 | 待验证 | 工具名称、版本、实际操作记录 |
| 多 DPI、多屏、远程桌面 | 待验证 | 每组合独立 JSON/截图及交互结果 |

本轮环境：Windows 10.0.26200、X64、.NET 10.0.12、Avalonia 12.0.3，渲染缩放1。配置字体为 Segoe UI/Noto Sans CJK SC/Microsoft YaHei/sans-serif；此名称是配置值，未鉴别每个字形的实际字体。锚点使用固定本地loader，最新协议探针另有实际默认file/avares及真实回环HTTP输入；未进行公网网络环境验收。PNG底部代码在605DIP滚动视口边界裁切，属于视口范围；不是屏幕scan-out或物理输入截图。

保留失败证据：首次沙盒 `dotnet <Demo.dll>` 输出完成JSON但运行时退出断言，退出1；沙盒apphost复验遇Windows Clipboard UnauthorizedAccessException，helper被清理。随后在实际桌面上下文运行相同Demo.dll成功退出0并生成上述18项结果。沙盒失败不被改写成通过，也不据这些环境错误宣称控件已完成全平台退出稳定性验收。

最终Core迁移优化后的首次复验报告 `windows-markdown-smoke-final-verified.json` 因剪贴板无文本而保守跳过、退出1，其余检查正常。探针增加空状态/非文本区分后，`windows-markdown-smoke-latest.json` 在原空剪贴板环境验证真实复制并恢复空状态，18项true、退出0；早期 `windows-markdown-smoke-elevated.json` 已验证原文本的恢复。两个状态的证据分别保留。

2026-10-14增强smoke途中两个失败报告继续保留：一个筛选未限定自定义节点导致命中其它视觉节点，另一个错误把`Geometry.ToString()`当作path内容。它们属于探针判定缺陷，修正筛选与几何判定后28项通过，后续扩充默认图片协议后43项通过；不能将这两项写成产品功能失败或改写历史输出。最新PNG的基础阅读视口已工具目视，DPI、物理交互与辅助工具审核仍待完成。

## Linux实际SDK与原生运行证据

本轮[6451实际SDK构建](../artifacts/linux-build/6451a248de3948f8baab4ee5f6ba0e0c/result.json)及[11份原始日志审计](../artifacts/linux-build/6451a248de3948f8baab4ee5f6ba0e0c/regression-summary.json)为907/907、0failed/errors/skipped/notRun，四个实际输出DLL与结果声明一致，485项源清单及与Windows387项的全量比较独立保存。A50F/Host2905[五轮守卫](../artifacts/linux-probe/dfc8c8cdb68349dda6f37615eea4c319/guard-policy-51.json)在用户明示51ms长流UI政策下实际通过，原R4在50ms下失败的报告未改写。

该构建的真实Linux编译Demo已取得新的[43项报告](../artifacts/platform/linux-markdown-smoke-a50f-00ca24d5178d454eb29e563d8887c157/report.json)、[退出记录](../artifacts/platform/linux-markdown-smoke-a50f-00ca24d5178d454eb29e563d8887c157/result.json)、[产物与证据SHA汇总](../artifacts/platform/linux-markdown-smoke-a50f-00ca24d5178d454eb29e563d8887c157/evidence-summary.json)及[PNG](../artifacts/platform/linux-markdown-smoke-a50f-00ca24d5178d454eb29e563d8887c157/preview.png)：43/43 true、error=null、Exit0，真实文本复制、读回与原文本恢复均true。新PNG已经工具目视，中文、嵌套列表、引用、高亮代码及阅读界面正常。驱动仅在独立副本修正冻结源码目录的运行库根路径，并增加120s/10s监督超时；[原/派生驱动SHA](../artifacts/platform/linux-markdown-smoke-a50f-00ca24d5178d454eb29e563d8887c157.driver-provenance.json)保留，无构建或产品修改。以下805/808/840/870/896及旧剪贴板跳过记录按历史版本保留，不覆盖为新通过。

Ubuntu26.04 LTS / WSL2 / WSLg，kernel6.6.114.1-microsoft-standard-WSL2 x86_64；DISPLAY=:0、WAYLAND_DISPLAY=wayland-0。已运行的原生宿主实际使用Avalonia.X11，句柄XID有效；Wayland环境变量只说明WSLg合成环境，不能作为应用原生Wayland后端证据。

原有工作区runtime为10.0.12。随后官方固定linux-x64 SDK10.0.401 archive经完整SHA512核验，离线解压到全新`artifacts/tools/dotnet-linux-sdk`；[实际SDK信息](../artifacts/tools/dotnet-linux-sdk-metadata/sdk-info.txt)记录MSBuild18.9.11、Host10.0.12和linux-x64，[固定官方URL/hash](../artifacts/tools/dotnet-linux-sdk-metadata/10.0.401-linux-x64.json)保留。没有安装系统包、改变PATH或覆盖既有runtime。

后续ED745B产品源码快照实际LinuxSDK再次locked restore/全slnx Release0warning/error，3分18秒；[11项目结果](../artifacts/linux-build/a8ed9053714549f8b6172a9f601aba8b/result.json)797/798通过，唯Headless138拖选scope用例在Flush遇框架已释放TestContext。Linux功能DLL `7B94190C…` 与Windows ED不同编译环境hash分别保存；源文件清单当时全Core0diff。仅更新测试harness后，[同产品/host四个hash不变的两轮](../artifacts/linux-build/a8ed9053714549f8b6172a9f601aba8b/retest-context-26e78530641d42be8ffa6ad76e62bc6c/result.json)首轮Errors100/Total12，第二轮138/138、Errors0。两次失败与成功均保留，仍不宣称最终Linux整套通过；当前错误恢复与中性UI接收修复后还需同步必要源码、复用隔离SDK/cache编译全套。

Windows最终错误恢复与中性UI接收修复版本功能SHA `1B6523A929E71E79F77EEAB684485107E6B24429A740798F2ED73F0E568071F9` 的43项native报告通过、退出0、原剪贴板文本恢复true，最新PNG已工具目视。此前ED的final报告、EC50的loader报告、28项services报告及旧18项保持原文件。

随后Viewport同步修复版本功能SHA `71E042D902BE079B86823A4290A30E7FE0297C91B5D9B479E02FA829D53EFC7A` 再次完成43项native探针、退出0，PNG已工具目视；viewport报告保持原文件。全仓[808项Verify](../artifacts/verification/d0c2ce2b3ea241aaa7a215c288dbf66e/results.json)及包消费者通过，不能据此覆盖1B65原生性能失败或宣布五轮预算通过。

同一产品的原生性能另行保留两次正式尝试：2370宿主首次第1轮在请求窗口Minimized时Render超时，未完成该轮；351D宿主仅固定窗口创建时不能最小化、初态Normal，未改变预算或重试。新尝试第1轮完整通过，第2轮完整测得短流式延迟P95169.8169ms、长流全部提交UI+布局P9570.4623ms、长流Complete P951759.3211ms/最大3210.7820ms，分别超150/50/每次1500ms门槛，实际退出1并停止第3–5轮。第2轮100次虚拟化和100次替换/释放检查通过；[原始报告与分项证据](markdown-performance.md)记录全部失败。当前M6五轮仍未通过。Linux最新808的宿主99B9是原2370源的真实Linux编译结果；后续宿主派生编译须单列，不能回写成旧808已覆盖新宿主。

后续A174软件修复将实际tail realization、ScrollToEnd及全部同步following布局纳入同一dispatch和layout metric，并修复margin边缘反复创建/清除块导致的永久布局失效；同程序集before/after及159项Headless实际证据如上。旧71E0/351D指标只包围首次ApplyDocument布局，不能据首轮40.4013ms宣称包含完整follow过程。A174尚无新冻结完整原生性能轮次；旧Windows43项smoke与Linux808也不追填成新产品证据。

最终following与未闭合EOF错误恢复冻结产品功能SHA `DA1108CC82DA79A21433163E488A9BD783D7E5EC36EBD7F2685064F527D14B71` 的[集中840项Verify](../artifacts/verification/7b338e894adb4d51a425a0c3b6134138/results.json)、七包及消费者通过；最新Windows43项following-recovery native报告退出0、真实剪贴板复制/恢复true，PNG已工具目视。新产品完整pilot和四组reading100/10通过；正式第1轮全部场景完成后长会话完整UI+布局P9587.2475ms、Complete P952175.3424ms/最大2612.5187ms失败，实际退出1，第2–5轮未启动。短流式、100次虚拟化及100次共享服务/替换/卸载检查通过；[原始报告和分项证据](markdown-performance.md)全部保留。840项测试或43项smoke不能替代五轮预算验收。

最新冻结产品源码在新GUID `7b37264619ea4225bbdbd84fdde263bc` 实际locked restore与全slnx Release0warning/error，2分55.80秒。最终Headless完整夹具仅重编测试10.16秒、0warning/error，11项目805/805；同DLL另两个独立whole145/145，三次均Errors0/Failed0/Skipped0，约9.266/9.059/8.880秒。产品源与Windows冻结源码0diff，Linux功能SHA `D6022156031E461CF9C159307F222F2053B2DC5EC890D9B654C12BF977199571`、宿主 `E5CA80FD57C8D5C1FB893B8CABFD682847A410BFFDFC39D3561B8F2E5B150DF1`；测试重编未变更三个产品及宿主hash。[完整结果、源码备份与日志](../artifacts/linux-build/7b37264619ea4225bbdbd84fdde263bc/retest-final-fixture-9b3dc692a8bb45a8bf97e054052d8d28/result.json)保留，原compile-only报告仍明确测试跳过。随后Themes针对Windows框架adapter错误改用相同公开Session夹具，仅重编测试6.90秒、0warning/error，[最终全11再次805/805、Themes两个独立8/8](../artifacts/linux-build/7b37264619ea4225bbdbd84fdde263bc/retest-final-themes-7d76ab4032f84ce6a0b037b2a823347d/result.json)全部Errors0/Failed0/Skipped0，产品/host四个hash仍未变。

真实Linux编译Demo已经运行原生smoke：[JSON](../artifacts/platform/linux-markdown-smoke-dc47816134814f6b822737ba0b6f5ad7/report.json)、[PNG](../artifacts/platform/linux-markdown-smoke-dc47816134814f6b822737ba0b6f5ad7/preview.png)、[退出码与来源](../artifacts/platform/linux-markdown-smoke-dc47816134814f6b822737ba0b6f5ad7/result.json)。40项页面、服务、图标和file/avares/回环HTTP检查true，错误字段null；剪贴板无可读取文本而格式非空，安全保留原内容、`剪贴板测试已执行=false`，实际退出1。该次不计完整43项通过，也没有复制/恢复证明。PNG已工具目视：中文、嵌套列表、引用、高亮代码及复制按钮正常，601DIP视口底部代码正常裁切。入口 `NativeHost/Run-LinuxMarkdownSmoke.ps1`核对通过的全11项目build报告与产品hash，使用进程范围ICU/库/字体，不用perf报告替代。

随后[只读X11目标清单](../artifacts/platform/linux-markdown-smoke-dc47816134814f6b822737ba0b6f5ad7/clipboard-targets.json)记录CLIPBOARD owner未变，目标为TIMESTAMP、TARGETS、UTF8_STRING、TEXT；PRIMARY没有owner。仅查询TARGETS，不取得用户payload、不改写剪贴板。固定NuGet commit的[Avalonia.X11映射源码](https://github.com/AvaloniaUI/Avalonia/blob/21e816874efa142bf9903b51fd323d1f35e56ab3/src/Avalonia.X11/Clipboard/ClipboardDataFormatHelper.cs)过滤TARGETS/MULTIPLE/SAVE_TARGETS，但TIMESTAMP仍会映射为普通格式，UTF8_STRING识别为文本。因此“格式非空且文本读取null”尚不能证明真实用户非文本内容；原生文本请求/WSLg桥状态还需诊断，当前安全跳过与剪贴板待验结论保持。

最新完整源码快照[7cc构建与11项目结果](../artifacts/linux-build/7cc589e63c24417881d1fe614f5da928/result.json)实际Linux SDK全slnx Release0warning/error、4分00秒，808/808且Errors0/Failed0/Skipped0。包含Viewport同步修复3项新用例，Headless148；功能SHA `CDB73D8C0F3F7E8299689DB7A1914FE23A9E5A8EA3E9E294D53DA8E2FF641732`、宿主 `99B9B5B680496FC32A74E1F79F8B7DB6263CD8C86579174EA8894116C7AC0712`，源manifest SHA `CCE84CA66EA983B29E196FF1CAFE22A9BF78CA2DEADAE75FAA95FF7F29D5E708`。该新快照[Demo原生重测](../artifacts/platform/linux-markdown-smoke-e70d5b21251d4c7eb6ea705793575954/result.json)仍40项功能/协议true、错误null，但剪贴板TryGetText为空而安全跳过，退出1；JSON/PNG继续保留。只读[文本转换响应metadata](../artifacts/platform/linux-markdown-smoke-dc47816134814f6b822737ba0b6f5ad7/clipboard-conversion-metadata.json)显示UTF8_STRING/TEXT的响应type=UTF8_STRING、format8、65字节待读取、owner保持；通过length0查询，没有取payload。广告格式/单次响应不能代替Demo实际文本复制和恢复证明，原生剪贴板仍待验。

DA11最终源码另用全新GUID[8df构建结果](../artifacts/linux-build/8df8090ad9b844a096f573b154f4a91b/result.json)实际Linux SDK locked restore、全slnx Release0warning/error、3分31.17秒，再跑11项目[840/840](../artifacts/linux-build/8df8090ad9b844a096f573b154f4a91b/regression-summary.json)，Errors0/Failed0/Skipped0/NotRun0。Core184、Streaming60、Headless159、Themes8、TextMate13及既有416均通过；源manifest SHA `418323DE43A1CA038B0697C0944E4EE5D033FE9394316A4DAB7F6EB182654556`，真实Linux功能SHA `6C30CB601097599FEBD4E4E2CFF11C0AA4E6D8795996B8C5FC92CEA94E09D2C1`、宿主 `29FD4C13132085927CE7E10F475F3EB14A4855E9466C287DFD063AD1DB979B85`。没有把旧7cc808复用成新源码通过证据。

同一Linux实际编译Demo[最新原生报告](../artifacts/platform/linux-markdown-smoke-11a1a9e386d248f3b4aa8bc1351f097f/report.json)、[退出码与来源](../artifacts/platform/linux-markdown-smoke-11a1a9e386d248f3b4aa8bc1351f097f/result.json)及[PNG](../artifacts/platform/linux-markdown-smoke-11a1a9e386d248f3b4aa8bc1351f097f/preview.png)完整保存：40项页面/服务/图片协议true、错误null；原剪贴板TryGetText没有可读文本，安全跳过，未取得复制/恢复证明，实际退出1。PNG已工具目视，中文/标题/列表/引用/高亮代码正常，底部代码在601DIP视口裁切。此次未重查用户payload或改写未知剪贴板；完整43项和M7剪贴板仍待验证。

`NativeHost/Run-LinuxBuild.ps1`以独立源文件/hash清单、cache、CLI home、temp及每项目obj/bin执行真实Linux SDK locked restore、全slnx Release和11项目测试。初次snapshot中缺历史API夹具导致5项误失败，补docs/scripts/.github后保留原编译程序集完整重跑；既有6项目416项、Streaming60、Themes7、TextMate13全通过，Core87/88和Headless87/88。功能SHA `73F8B16DD587286AD0E08A38DEB427F60BB11AD77363F8C12D14B39EA3DBAACD`。后续空脚注组和弱continuation修复已在Windows得到证据，仍须同步最终源码在Linux复验，不宣称旧snapshot已通过。

进程范围复用既有app-local ICU72.1.0.3、libICE/libSM、Noto CJK/fontconfig；ICU模式非invariant。原生固定1200×700/scale1，名义Segoe UI字体及FontManager字符匹配结果另存报告（不证明每个绘制glyph来源）。第四版Windows与Linux均用同一隔离Windows IL副本，Linux真实SDK编译数据另按hash分开；[Linux reading100/10单轮](../artifacts/linux-probe/4f4f36f09033406bb521082eb110bb57/reading-100-round-1.json)的1MiB UI P95为70.525/71.129ms，均超过50ms，不能用Windows33ms替代。最新优化及新增1MiB长流入口的正式五轮尚未取得。

本环境已经取得当前冻结产品的实际LinuxSDK907回归、WSLg/X11五轮原生性能及43项Demo真实文本剪贴板复制/恢复证据。系统链接、物理键鼠、辅助工具、完整DPI、多屏、原生Wayland、实体Linux和麒麟仍需相应设备与原生操作证据。

## 真机退出清单

- 记录发行版/OS 构建号、CPU 架构、.NET SDK/运行时、显示后端、桌面/合成器、实际字体环境、缩放和视口。
- 检查中文、代理对、组合字符、Emoji ZWJ、RTL 视觉顺序及真实命中；鼠标拖选跨段落、代码、表格和离屏区域，全选/复制读回完整逻辑文本。
- 在持续追加、未闭合 fence、表格分隔线、晚引用/脚注定义中验证进度、尾批和旧结果拒绝；真实 TwoWay 外部替换后旧生产者不能拼入。
- 验证上滚/回底跟随、视口上方图片增高、宽度/字体/换肤、隐藏/显示、模板替换、卸载/挂载和页面停用；记录不能稳定保持阅读位置的组合。
- 用实际辅助工具检查只读文档、链接、任务状态和全文文本范围；未实现或未验证的级别在功能差异中公开。
- 在全新 NuGet 缓存中运行功能包自定义模板、主题资源和可选高亮的独立消费者；回归既有 TreeDataGrid/VirtualKeyboard 页面及包消费。
- 缺少麒麟设备时继续其它验收，麒麟状态始终保留待验证，不发布麒麟已支持的声明。

最新048同源Linux实际SDK构建另存[5a681结果](../artifacts/linux-build/5a681321a91349dda6bace76a80eb43d/result.json)和[逐项目回归汇总](../artifacts/linux-build/5a681321a91349dda6bace76a80eb43d/regression-summary.json)：SDK10.0.401、Release零警告/错误，11项目870/870、failed/errors/skipped/notRun均0；477文件来源清单独立保存，不将Windows程序集冒充Linux编译。当前Linux实际功能SHA为7E7816CE，与Windows04856FD6分别追溯。

同版本Linux[原生报告](../artifacts/platform/linux-markdown-smoke-2c50e76225fd4e839a978f3a785e6433/report.json)、[来源与退出记录](../artifacts/platform/linux-markdown-smoke-2c50e76225fd4e839a978f3a785e6433/result.json)及[PNG](../artifacts/platform/linux-markdown-smoke-2c50e76225fd4e839a978f3a785e6433/preview.png)已保存并目视：40项功能/协议true、错误null；原剪贴板无可读文本而安全保留，整体退出1，复制/恢复仍待验证。新完整pilot和正式首轮均已复现1MiB UI与短流预算失败，见[性能记录](markdown-performance.md)，不以软件870项或旧DA11记录替代性能通过。麒麟、物理设备、多DPI与真辅助工具继续待验证。
