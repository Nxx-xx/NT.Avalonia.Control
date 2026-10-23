# Markdown 后续完善与本轮验收（2026-10-15）

当前M7可执行软件范围已通过，见[本次M7收束记录](markdown-m7-completion-20261015.md)及[新M7执行记录](../artifacts/markdown-m7-completion/c63fab7542ba4b22b9a63d9cbb736f1b/m7-exit.json)。Windows43项与Linux私有Xvfb/X11新43项真实复制、读回及恢复通过，实际producer/Main/外层均退出0；原WSLg共享显示的安全Pending保持历史记录，两个环境分别记账。

Windows50ms、Linux51ms及身份、引用、脚注语义不变；本次增补按同一冻结源码和产物SHA复用五轮、544×2、416×2、包/API及85/134工具合同，不声称再次执行。远端原生CI由用户暂缓（DeferredByUser）；麒麟、实体Linux/原生Wayland、完整DPI/多屏、物理输入和真实辅助工具继续待验证。未发布、未改版本。

## 前期定点优化与当时验收记录

后续专项已修复自定义Wrap前导空行挂起；其独立复现及新代码测试见[Wrap修复记录](markdown-wrap-leading-lf-20261015.md)。本文下面的DD330 / 57C68通过记录保持原候选绑定。

本轮按顺序完成原生 CI、链接 Handled 契约和文档一致性，再推进长块布局与图片资源峰值优化。已验证的上一候选为 Windows F832 / Linux A50F；其 907 项回归、五轮及 43 项 smoke 不自动签给本轮新代码。

修订候选已完成本轮实现和定点验收：[最终证据索引](../artifacts/markdown-followup/09e1dae233b34ce8869fbe5768a5f01a/phase-b/revision-4c573ee828e347cfbb95772a98c60fd2/final-results.json)。Windows / 实际Linux SDK各538项软件回归、各43项NoBuild组合原生功能、各85项工具合同通过；三包消费78 / 80项通过。长块三场景各100样本 / 10预热按原门槛通过，初版原生回归及所有首测失败保留。本轮没有再放宽预算；后续Wrap修复版完整新五轮已通过；远端CI及其它剩余范围见文末和上方最新入口。

## 范围与固定条件

- 长会话 UI+同步布局 P95 先维持显式 51ms；用户允许依据新实测评估小幅放宽，本轮尚未实施进一步修订。其他预算保持原值。
- 重复节点身份、晚到引用、脚注编号与复制投影、选区和资源归属保持原契约。
- 麒麟、实体 Linux / 原生 Wayland、完整 DPI / 多屏、物理输入和真实辅助工具仍按具体环境待验证，不阻塞可执行工作。
- 旧报告、旧失败、已冻结软件与政策归档不改写。新结果使用新的目录和实际源 / 产物哈希。

## 本轮阶段

| 阶段 | 工作 | 当前状态 |
| --- | --- | --- |
| A1 | 本机 Windows / Linux 原生 smoke、正式五轮入口及 CI 工作流；完整保留失败日志 | 已落地；两平台各85项工具合同、最终各43项NoBuild组合原生功能通过；早期剪贴板Pending保留，远端workflow尚未执行 |
| A2 | Handled 的默认 visited 行为及事件重入失效契约 | 已实现；完整Headless198/198、独立包消费78/78及80/80通过 |
| A3 | 当前文档入口与历史阶段证据分开，统一当前验收来源 | 对照/矩阵/状态已统一入口，历史证据保留；原生CI调用和证据边界已补文档 |
| B1 | 长块布局优化及原生几何 / 命中 / 选择差分 | 已完成本轮定点验收；初版布局回归修订后，三组各100样本 / 10预热全部按原门槛通过；独立Wrap空行挂起已由后续专项修复，见[修复验收](markdown-wrap-leading-lf-20261015.md) |
| B2 | 图片解码前尺寸预检与峰值边界，保留原生解码 / 租约 / 取消 | 已实现；含DHP / EXP回退的24项loader测试通过，两平台最终DLL各17项真实解码兼容及单次峰值通过 |

## 证据与剩余问题

本轮起点与文档备份见 [开始记录](../artifacts/markdown-followup/09e1dae233b34ce8869fbe5768a5f01a/start.json)。测试、失败及阶段退出结果随实际执行补入；远端 GitHub 工作流只有取得真实运行结果后才记录通过，本地验证不代替远端执行。

- [Handled完整Headless结果](../artifacts/markdown-followup/09e1dae233b34ce8869fbe5768a5f01a/phase-a/handled-headless-result.json)：198/198，构建零警告/错误。
- [独立包消费者](../artifacts/markdown-followup/09e1dae233b34ce8869fbe5768a5f01a/phase-a/package-consumer-results.json)：功能独用78项、主题及TextMate80项，各18项内嵌AXAML。新Handled断言保留全部既有URL别名/BaseUri/事件重入检查。
- [阶段A汇总](../artifacts/markdown-followup/09e1dae233b34ce8869fbe5768a5f01a/phase-a/final-results.json)记录当时74项助手合同及原生执行；[复核后补充](../artifacts/markdown-followup/09e1dae233b34ce8869fbe5768a5f01a/phase-a/postreview-results.json)记录当时两平台各82项工具合同、实际退出码和文件哈希。工具合同不作为原生性能结果，早期失败及59 / 65 / 72 / 74项记录均保留。
- [最终路径大小写修复](../artifacts/markdown-followup/09e1dae233b34ce8869fbe5768a5f01a/phase-a/pathcase-results.json)继续补齐两平台各85项工具合同。Windows可执行路径按OrdinalIgnoreCase、Linux按Ordinal核验；真实Windows大小写别名进程的启动与超时清理已验证，SHA与源码比较仍严格。
- [Windows v3原生运行](../artifacts/markdown-followup/09e1dae233b34ce8869fbe5768a5f01a/phase-a/windows-native-smoke-v3/result.json)及[Linux v3原生运行](../artifacts/markdown-followup/09e1dae233b34ce8869fbe5768a5f01a/phase-a/linux-native-smoke-v3/result.json)：各40项非剪贴板功能通过，41条检查含剪贴板未执行；原状态无法安全读回，保持Pending/producer退出1、runner退出2。隔离源码及运行文件执行中未变化，PNG已目视；不冒充43项完整通过。NoBuild单独冻结源码和既有产物，不证明二者构建对应；Linux为Windows生成的IL在Linux / WSLg X11运行，不是Linux SDK重新编译。
- [首次运行的真实并行源码变化](../artifacts/markdown-followup/09e1dae233b34ce8869fbe5768a5f01a/phase-a/source-diagnosis-b5f173407a104b16b0f9107250d9b939/source-diff.json)发生在Charts/PDF其它任务内；检查正确拒绝，native尚未启动，后续改用隔离源码。
- [Linux入口首测失败](../artifacts/markdown-followup/09e1dae233b34ce8869fbe5768a5f01a/phase-a/linux-native-smoke/result.json)发生于启动窗口前：读取隐藏.editorconfig缺少Force。后续还定位并修复短生命周期子进程的元数据观察竞态，以及Linux重复fc-list命令路径解析。失败原件保留，最终74项合同及两平台v3重测已验证修复。
- [十份旧正式raw元数据重放](../artifacts/markdown-followup/09e1dae233b34ce8869fbe5768a5f01a/phase-a/formal-metadata-replay-895d94d84db046969d5266bf2544ffe5/results.json)通过、原raw哈希未变；仅证明新入口能读取真实报告schema，不作为本轮产品新五轮或预算通过证据。

本轮还原首次遇沙箱代理连接NU1301而未开始消费者执行，原 [失败日志](../artifacts/markdown-followup/09e1dae233b34ce8869fbe5768a5f01a/phase-a/consumer.log) 保留；恢复网络权限后使用同一批新包、全新消费目录重跑通过，未关闭审计/签名检查。

Windows图片峰值使用同一冻结probe DLL、同一261,755字节的8192×8192 PNG，优化前后各独立进程加载一次。[旧路径](../artifacts/markdown-image-peak/02c857a92bc8493b9e3ee361728e9628/before-peak.json)进程PeakWorkingSet64从82,063,360到352,292,864字节；[预检路径](../artifacts/markdown-image-peak/02c857a92bc8493b9e3ee361728e9628/after-peak.json)从81,960,960到82,661,376字节。均实际按原32Mi像素限额抛IOException、没有逃逸租约、文件已关闭；不是以工作集当前值或托管GC代替native峰值。[前](../artifacts/markdown-image-peak/02c857a92bc8493b9e3ee361728e9628/before-compatibility.json) / [后](../artifacts/markdown-image-peak/02c857a92bc8493b9e3ee361728e9628/after-compatibility.json)17项兼容检查均通过，PNG / JPEG / BMP的原生重编码SHA逐个相同。此为单fixture诊断，不是所有格式的峰值保证或新增资源预算。

Linux同probe的[四个独立进程](../artifacts/markdown-image-peak/02c857a92bc8493b9e3ee361728e9628/linux-probe-execution-summary.json)也实际退出0：大图进程峰值427,687,936降至159,182,848字节，两侧兼容检查各17/17、对应重编码SHA相同。此处是冻结Windows IL在Linux实际native解码器运行，未称Linux SDK编译；最终联合源码的Linux SDK回归另行记录。

长块原生[优化前局部诊断](../artifacts/markdown-followup/09e1dae233b34ce8869fbe5768a5f01a/phase-b/long-block-before-summary.json)完成三个场景各100样本、10预热、实际退出0。它只测long-block组，不是完整11组正式轮次，也不能作为本轮完整五轮守卫通过证据；优化后使用同一宿主对照。

联合初版[Windows五项目](../artifacts/markdown-followup/09e1dae233b34ce8869fbe5768a5f01a/phase-b/integration-9cc0d5934e7a4e71be599c684788231f/windows-regressions/results.json)534/534通过，三包[独立消费](../artifacts/markdown-followup/09e1dae233b34ce8869fbe5768a5f01a/phase-b/integration-9cc0d5934e7a4e71be599c684788231f/windows-markdown-hosts/package-consumer-results.json)78/78及80/80通过；[公共API和默认值导出](../artifacts/markdown-followup/09e1dae233b34ce8869fbe5768a5f01a/phase-b/integration-9cc0d5934e7a4e71be599c684788231f/public-api-comparison.json)与既有签名一致。此为Core312C初版，不能覆盖下面已发现的性能失败，也不签给修订代码。

布局订阅修订后的候选采用[稳定底本及明确覆入清单](../artifacts/markdown-followup/09e1dae233b34ce8869fbe5768a5f01a/phase-b/revision-4c573ee828e347cfbb95772a98c60fd2/revision-source-manifest.json)，未混入后续并行PDF / Charts源码。[Windows](../artifacts/markdown-followup/09e1dae233b34ce8869fbe5768a5f01a/phase-b/revision-4c573ee828e347cfbb95772a98c60fd2/windows-regressions/results.json)和[实际Linux SDK](../artifacts/markdown-followup/09e1dae233b34ce8869fbe5768a5f01a/phase-b/revision-4c573ee828e347cfbb95772a98c60fd2/linux-sdk/evidence/regressions/results.json)五项目各538/538：Core221、Streaming60、Headless234、Themes10、TextMate13，failed / errors / skipped / not-run全0、源码执行中不变。各自编译的Core为DD330 / 57C68，不能混称同DLL；新三包[独立消费](../artifacts/markdown-followup/09e1dae233b34ce8869fbe5768a5f01a/phase-b/revision-4c573ee828e347cfbb95772a98c60fd2/windows-markdown-hosts/package-consumer-results.json)78/78及80/80通过，实际[公共API和默认值](../artifacts/markdown-followup/09e1dae233b34ce8869fbe5768a5f01a/phase-b/revision-4c573ee828e347cfbb95772a98c60fd2/public-api-comparison.json)无签名差异，当前文档快照已更新。原生长块重测仍单独判定。

修订候选的[Windows最终原生检查](../artifacts/markdown-followup/09e1dae233b34ce8869fbe5768a5f01a/phase-b/revision-4c573ee828e347cfbb95772a98c60fd2/windows-native-smoke-after-linux-text-restore/result.json)与[Linux最终汇总](../artifacts/markdown-followup/09e1dae233b34ce8869fbe5768a5f01a/phase-b/revision-4c573ee828e347cfbb95772a98c60fd2/linux-sdk/final-linux-evidence-summary.json)均43/43、producer / runner实际退出0，包含真实文本剪贴板复制及原文本恢复。Windows此前Pending原件不改；Linux恢复真实文本后的新Windows运行才完成43项。两端均为冻结Demo加最终三控件DLL的NoBuild组合，不称当前Demo源码新编；图像已核对，属于Window的RenderTargetBitmap而非桌面scan-out。[新包内嵌DLL](../artifacts/markdown-followup/09e1dae233b34ce8869fbe5768a5f01a/phase-b/revision-4c573ee828e347cfbb95772a98c60fd2/package-assembly-binding.json)也与测试程序集逐项同SHA。

最终DLL的[Windows大图](../artifacts/markdown-followup/09e1dae233b34ce8869fbe5768a5f01a/phase-b/revision-4c573ee828e347cfbb95772a98c60fd2/windows-image-final/peak.json)和[Linux大图](../artifacts/markdown-followup/09e1dae233b34ce8869fbe5768a5f01a/phase-b/revision-4c573ee828e347cfbb95772a98c60fd2/linux-sdk/image-final/peak.json)再次使用同一6527探针、各一个新进程 / 一次输入，PeakWorkingSet64分别85,553,152和159,346,688字节，均正确拒绝同一超限PNG、无租约逃逸、文件关闭；各17项兼容检查通过。旧831E基线分别352,292,864和427,687,936字节，BB8中间版本峰值报告按原产物保留，不转签。

同一宿主、仅替换Core的[初版原生失败](../artifacts/markdown-followup/09e1dae233b34ce8869fbe5768a5f01a/phase-b/integration-9cc0d5934e7a4e71be599c684788231f/long-block-after.json)已完整收齐：每组三场景各100样本 / 10预热，实际producer退出1、监督完整。段落总P95为709.8908ms / layout653.2521ms，宽表为1032.1248ms / layout867.4401ms；原门槛失败。对照原布局75.5469 / 197.4284ms，主要增量在布局而非解析、选择或巨额分配。此项按实现回归定位，不放宽预算或归咎并行负载，保留原raw后修复重跑。

修订将视口监听限制到32–65,536行的合格原生布局，短块 / 单行及时取消订阅，空选区不再因视口通知创建无意义重绘；订阅发生在原生Measure后且不触发新的Measure。新增4项长短 / fallback / 卸载重挂回归，并纳入最终两平台538项。[修订原生100 / 10](../artifacts/markdown-followup/09e1dae233b34ce8869fbe5768a5f01a/phase-b/revision-4c573ee828e347cfbb95772a98c60fd2/long-block.json)实际退出0、三组全部通过，门槛逐字段与原报告相同。

| 原生长块场景 | 原版总P95 / 布局P95（ms） | 初版失败总P95 / 布局P95（ms） | 最终总P95 / 布局P95（ms） |
| --- | --- | --- | --- |
| 100KiB单代码长行 | 116.5725 / 53.4067 | 149.9481 / 86.7551 | 116.5460 / 54.3383 |
| 100KiB单段落 | 116.6077 / 75.5469 | 709.8908 / 653.2521 | 116.7732 / 67.7232 |
| 30×30宽表 | 250.5130 / 197.4284 | 1032.1248 / 867.4401 | 267.1893 / 218.0920 |

三版本使用同一冻结NativeHost，仅替换Core。最终选择 / 滚动 / 单文档分配守卫也通过；宽表仍略慢于原版，不能宣称每一项都更快。[原冻结测试DLL对最终DD330差分](../artifacts/markdown-followup/09e1dae233b34ce8869fbe5768a5f01a/phase-b/revision-4c573ee828e347cfbb95772a98c60fd2/same-fixture-binding.json)5/5通过，旧831E同DLL为4/5且唯一失败是视口外工作量断言；选区绘制命令280→12。该诊断与原生局部100采样都不替代新的完整11组五轮。

上一轮完整交接见 [测试交接](markdown-test-handoff-20261015.md)，已验证的平台边界见 [平台验收](markdown-platform-acceptance.md)。

## 失败分类与预算判断

| 项目 | 性质 | 当前处理 |
| --- | --- | --- |
| Handled=true仍自动写visited，事件重入写入后续文档 | 公共行为契约bug | 已修复，11个新增实例及完整198项Headless、独立包消费验证 |
| Linux隐藏文件读取、重复fc-list路径、短进程元数据观察竞态 | 原生入口脚本bug | 失败原件保留，修复后两平台各74项合同及v3实际原生运行完成 |
| Windows启动期间MainModule暂返回ntdll.dll导致错误身份拒绝 | 新合同捕获的原生入口脚本bug | 原失败保留，改用OS查询自有进程的实际image路径；修复后两平台各82项工具合同通过 |
| 连续空LF代码块全选时原生范围查询传入零长度或漏覆盖 | 既有文字几何bug，优化前后均能复现 | 已修复为确认空LF布局的原生逐行正交集；绘制 / 自动化 / 反向 / 仅LF / 边界范围及全套回归通过，失败原件保留 |
| 新版段落 / 宽表原生布局P95大幅回退 | 本轮优化引入的实现回归 | 已修订视口订阅 / 重绘条件；原失败保留，最终100 / 10按原门槛通过，未提高时间门槛 |
| 自定义代码helper默认Wrap且含前导空LF时无限高度测量挂起 | 公开可达的既有独立bug | [复现及取证边界](../artifacts/markdown-b1/68d60e26f49741b2ba939af30e1f57d9/known-wrapped-leading-lf-boundary.json)保留；默认代码NoWrap不受该路径影响；后续已独立复现并修复，当前结果见[Wrap专项](markdown-wrap-leading-lf-20261015.md)，不再列为待修或预算问题 |
| Demo全源码locked restore因PDF的Pkcs8.0.1报NU1902 / NU1903 | 非Markdown依赖审计阻断 | 原日志保留；不关闭审计，不修改并行PDF工作。继续纯Markdown宿主 / 包测试，组合smoke按NoBuild明确来源 |
| 并行Charts / PDF源码在采样间变化 | 正确的来源拒绝，不是控件bug | 使用完整隔离源码和冻结运行文件，不忽略变化来通过 |
| 大段全选生成大量视口外矩形 | 选区渲染工作量优化项 | B1做相同测试DLL的前后差分，保留原生几何与复制结果 |
| 超限图片在完整native解码后才被拒绝 | 资源峰值限制不足 | B2用真实大PNG测峰值，增加保守尺寸预检，未知格式继续原生解码和后置限额 |
| 无法安全保存原剪贴板、缺设备或未执行远端CI | 待验证 | 不标记通过，不阻塞其它可执行工作 |

51ms目前仅用于已授权的Linux长会话ALL实际提交及Rendered子集UI P95。只有新的完整采样显示小幅、稳定超限且语义与资源守卫均通过，才有依据考虑进一步调整；本轮尚未修改该值。身份错误、引用 / 脚注结果变化、布局几何差异、取消 / 租约失效、队列越界、来源不一致及不完整报告均应修复，不能通过放宽时间预算接受。

## 剩余问题与未测试项

1. **已修专项**：自定义代码helper默认Wrap前导空LF挂起已独立复现并修复；精确冻结测试DLL差分及新两平台544项结果见[Wrap修复验收](markdown-wrap-leading-lf-20261015.md)。下面旧DD330 / 57C68的538项及43项不转签新候选。
2. **未执行验收**：后续Wrap修复冻结版已取得完整11组、五个独立进程的新正式性能验收通过；旧F832 / A50F及本文修复前538候选不转签。远端GitHub hosted smoke及自托管正式runner配置 / 工作流也尚无实际运行结果。当前三组100 / 10只是本轮定点验收。
3. **构建阻断**：冻结底本的全Demo / solution验收受PDF依赖Pkcs8.0.1审计失败影响；未关闭审计，未更改并行PDF工作。当前三Markdown库和NativeHost均独立构建，Demo组合检查明确为旧宿主加新库的NoBuild。
4. **缺设备待验证**：麒麟、实体Linux / 原生Wayland、物理键鼠、真实辅助工具、完整DPI及多屏矩阵继续Pending。当前Linux是WSLg / X11；不由43项程序路由输入及RenderTargetBitmap代替设备验收。
5. **继续优化**：本次仅减少长块选区几何工作，完整冷塑形和文字绘制仍走native全布局；块内行级排版虚拟化未实现。未知 / 不完整图片格式仍可能先解码后检查像素，通用或多图并发的native峰值硬上限尚无保证。本轮单PNG进程峰值诊断不替代这些压力场景。
