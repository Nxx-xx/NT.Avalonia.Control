# Wrap 修复后完整五轮与 M7（2026-10-15）

后续同冻结版本的Linux私有Xvfb真实43项已完成，当前M7软件结论见[本次收束记录](markdown-m7-completion-20261015.md)及[新执行记录](../artifacts/markdown-m7-completion/c63fab7542ba4b22b9a63d9cbb736f1b/m7-exit.json)。本文保留完整五轮及原WSLg Pending、旧M7未退出时的历史证据，不把旧记录改签为新显示环境的43项。远端原生CI由用户暂缓，缺设备矩阵继续Pending；未发布、未改版本。

本轮以[Wrap修复后的冻结revision2](../artifacts/markdown-wrap-fix/4e635a31e1de441bbfa4cc4b78abca88/revision2/revision-source-manifest.json)重跑Windows和Linux完整五轮。Windows Core为1AEF42AB，Linux独立SDK Core为A8A4FA5A；不复用旧五轮作为新版本通过依据。[本轮起点](../artifacts/markdown-formal-m7/5fc570ff6e9940b7bb728346ba4c75b2/start.json)记录来源、预算与执行范围。

每平台要求5个独立进程，每轮11组场景、每组100样本、短流和长流各100会话、10次预热。平台串行采样；原始producer报告、退出码及进程监督完整保留，再运行独立五报告守卫。完整预算失败仍收齐后续轮；缺报告、运行异常和不完整轮不计通过。

Windows长会话全部实际提交UI及Rendered子集P95仍为50ms，Linux同两项为51ms，其它时间、分配、队列、资源和来源门槛不变。重复节点身份、引用与脚注继续绑定同冻结产物的[两平台544项回归和专属Wrap差分](markdown-wrap-leading-lf-20261015.md)，五轮本身不扩大为这些语义的独立全面验证。

## 当前状态

Windows完整五轮及独立守卫已实际通过，进程退出0、891源码与冻结运行文件校验未变。Linux同源独立SDK NativeHost已实际locked restore / build成功，0警告0错误；完整五轮、独立守卫及入口实际退出0，891源码和冻结运行文件校验未变。两平台各六套既有控件兼容回归均416/416通过。后续在私有Xvfb中补齐Linux43项、真实复制及恢复后，同冻结产物的M7已测软件范围通过；原WSLg共享显示的40功能/41项Pending仍是其环境下的历史事实。Windows宿主来自revision2已实际成功的NativeHost构建；NoBuild运行的来源绑定与实际构建记录分别保存，不把NoBuild声称为本轮重新编译。

| 平台 / 轮次 | ALL实际提交UI P95（ms） | 判定 |
| --- | ---: | --- |
| [Windows 1](../artifacts/markdown-formal-m7/5fc570ff6e9940b7bb728346ba4c75b2/windows/report-1.summary.json) | 25.4916 / 50 | 11组完整，producer退出0、分项通过 |
| [Windows 2](../artifacts/markdown-formal-m7/5fc570ff6e9940b7bb728346ba4c75b2/windows/report-2.summary.json) | 26.7044 / 50 | 11组完整，producer退出0、分项通过 |
| [Windows 3](../artifacts/markdown-formal-m7/5fc570ff6e9940b7bb728346ba4c75b2/windows/report-3.summary.json) | 25.7980 / 50 | 11组完整，producer退出0、分项通过 |
| [Windows 4](../artifacts/markdown-formal-m7/5fc570ff6e9940b7bb728346ba4c75b2/windows/report-4.summary.json) | 25.5205 / 50 | 11组完整，producer退出0、分项通过 |
| [Windows 5](../artifacts/markdown-formal-m7/5fc570ff6e9940b7bb728346ba4c75b2/windows/report-5.summary.json) | 25.7770 / 50 | 11组完整，producer退出0、分项通过 |
| [Linux 1](../artifacts/markdown-formal-wrap-linux/ed73715b0a2d4a61a2f05b734bf05ac0/formal-after-storage-87202a04751a4a85b8446b7894f160eb/report-1.summary.json) | 48.9534 / 51 | 11组完整，原producer50也通过、退出0 |
| [Linux 2](../artifacts/markdown-formal-wrap-linux/ed73715b0a2d4a61a2f05b734bf05ac0/formal-after-storage-87202a04751a4a85b8446b7894f160eb/report-2.summary.json) | 48.4256 / 51 | 11组完整，原producer50也通过、退出0 |
| [Linux 3](../artifacts/markdown-formal-wrap-linux/ed73715b0a2d4a61a2f05b734bf05ac0/formal-after-storage-87202a04751a4a85b8446b7894f160eb/report-3.summary.json) | 50.9018 / 51 | 11组完整，按现有51ms预算通过 |
| [Linux 4](../artifacts/markdown-formal-wrap-linux/ed73715b0a2d4a61a2f05b734bf05ac0/formal-after-storage-87202a04751a4a85b8446b7894f160eb/report-4.summary.json) | 50.4646 / 51 | 11组完整，按现有51ms预算通过 |
| [Linux 5](../artifacts/markdown-formal-wrap-linux/ed73715b0a2d4a61a2f05b734bf05ac0/formal-after-storage-87202a04751a4a85b8446b7894f160eb/report-5.summary.json) | 49.2685 / 51 | 11组完整，按现有51ms预算通过 |

Windows首轮短Complete P95为45.4641ms，长Complete为567.3257ms；原始报告与自有进程监督均保存，[Windows独立五报告守卫](../artifacts/markdown-formal-m7/5fc570ff6e9940b7bb728346ba4c75b2/windows/guard.json)已实际通过；[入口最终结果](../artifacts/markdown-formal-m7/5fc570ff6e9940b7bb728346ba4c75b2/windows/result.json)保留真实退出及来源核验。Linux[入口结果](../artifacts/markdown-formal-wrap-linux/ed73715b0a2d4a61a2f05b734bf05ac0/formal-after-storage-87202a04751a4a85b8446b7894f160eb/result.json)和[独立五轮守卫](../artifacts/markdown-formal-wrap-linux/ed73715b0a2d4a61a2f05b734bf05ac0/formal-after-storage-87202a04751a4a85b8446b7894f160eb/guard.json)均通过。Linux构建来源见[独立构建绑定](../artifacts/markdown-formal-wrap-linux/ed73715b0a2d4a61a2f05b734bf05ac0/build/build-binding.json)。

第3、4轮按现有51ms预算计为通过，不列入当前失败项。原始测量报告供独立五轮守卫校验，来源字段保持实测内容。两平台原生已全部通过，直接进入M7软件归档、包/API/回归证据绑定及最终组合smoke。若发生当前预算失败，先保留全部原件并定位；若修改产品，生成新冻结候选并重新验收，保持原预算和语义。

## M7软件与归档

| 项目 | 实际结果与来源 |
| --- | --- |
| Windows既有控件六套兼容回归 | [416/416，18阶段实际退出0](../artifacts/markdown-formal-m7/2fe1e682c532434ebcb566890ec68ec4/windows-compatibility-preparation-7b7578d761724543b2c3f79b692d113b/execution/execution-summary.json)，891冻结输入未变、六次构建0警告0错误 |
| Linux既有控件六套兼容回归 | [416/416，18阶段实际退出0](../artifacts/markdown-formal-wrap-linux/ed73715b0a2d4a61a2f05b734bf05ac0/compatibility-m7-ae4b3550b1f7443a9889f8d734412c70/execution-summary.json)，891冻结输入未变 |
| Windows最终组合smoke | [43/43及真实剪贴板复制、恢复通过](../artifacts/markdown-formal-m7/5fc570ff6e9940b7bb728346ba4c75b2/windows-smoke-desktop-42ecaaf62d5e4cde8828521ba891dfb7/independent-verification.json)，实际退出0；首次沙盒dotnet预检权限失败发生在原生启动前，原记录保留 |
| Linux原WSLg组合smoke（历史） | [40项功能通过、真实剪贴板Pending](../artifacts/markdown-formal-wrap-linux/ed73715b0a2d4a61a2f05b734bf05ac0/smoke-m7-13c8ea8fe32a476489a4b4d54a1c118a/execution-summary.json)，Main状态Pending，原生/外层实际退出1；未执行复制，原剪贴板保持。PNG已目视，源码及冻结运行文件未变；新私有Xvfb43项另见[收束记录](markdown-m7-completion-20261015.md) |
| 既有Markdown回归、包/API | [56项重新核验通过](../artifacts/markdown-formal-m7/5fc570ff6e9940b7bb728346ba4c75b2/software-reuse-verification-after-compatibility.json)，SHA绑定同冻结版本既有544×2、包消费78/80及实际API，不声称本次重跑这些测试 |
| 当前专属源码与CI工具 | [160项Markdown专属输入逐项SHA相同](../artifacts/markdown-formal-m7/5fc570ff6e9940b7bb728346ba4c75b2/current-live-markdown-source-binding.json)，各85工具合同与134政策合同为同源证明复用；公共配置与并行PDF不扩大为整仓相同 |

本阶段smoke使用旧D9D14C Demo宿主加各平台当前三库的独立NoBuild组合。Windows43项含真实文本剪贴板复制与原文本恢复；原WSLg环境Linux40项功能通过，当时真实复制未执行。PNG为RenderTargetBitmap截图，不替代物理输入、辅助工具或屏幕scan-out。原WSLg原文不可读且后续只读观察有非文本私有格式，保持历史Pending、不填入临时文本、不清空原内容；后续私有Xvfb的真实43项独立记录，不能改写本阶段事实。

本阶段基础设施失败、处理结果和当时待验证项见[失败与待验索引](../artifacts/markdown-formal-m7/5fc570ff6e9940b7bb728346ba4c75b2/failure-and-pending-index.json)。旧归档的SoftwareM7Passed=false保持原样；后续真实Linux43的独立增补据实完成M7软件范围，见[新收束记录](markdown-m7-completion-20261015.md)。整体设备矩阵仍待后续验收，不把原环境Pending改为通过。

## 磁盘不足与清理

Linux首次准备在D盘无可用空间时于runtime复制阶段退出1，未创建round-1或任何原始采样报告；后续C盘恢复在开始采样前被用户中断，当次未启动采样进程。两次失败/中断元数据均保留，不计为产品性能失败或已执行轮次，也不覆盖旧记录。

按用户要求核对后，仅清理顶层独立consumer的可重建包展开缓存：137处`.packages`先原位改名为`cache-archives`，再删除4,225个展开目录，共28,708,835,324逻辑字节。保留全部2,145份`.nupkg`、签名/nuspec/元数据等14,741个顶层文件及其SHA，保留license/image附属文件、consumer源码/bin/obj/所有结果。原`.packages`路径消失，未来restore会完整重建缓存，不误认残缺展开目录。

[清理结果](../artifacts/markdown-formal-m7/5fc570ff6e9940b7bb728346ba4c75b2/storage-cleanup-caf24c97462c439cbd990380170ba031/cache-cleanup-result.json)记录实得可用空间增加28,768,919,552B（约26.8GiB），D盘恢复至29,700,902,912B可用（约27.7GiB）。[逐项计划](../artifacts/markdown-formal-m7/5fc570ff6e9940b7bb728346ba4c75b2/storage-cleanup-caf24c97462c439cbd990380170ba031/cache-cleanup-plan.json)及操作journal保留精确原路径/归档路径和全部保留文件SHA。未清理任何冻结版本、原始报告、失败记录、消费者程序或源文件；旧NativeHost runtime去重候选未执行。

[清理后保护核验](../artifacts/markdown-formal-m7/5fc570ff6e9940b7bb728346ba4c75b2/storage-cleanup-caf24c97462c439cbd990380170ba031/post-cleanup-formal-preservation.json)确认Windows五份原始报告SHA仍匹配独立guard，96个冻结运行文件全部未变。Wrap冻结软件的56项来源核验通过；本次受清理的历史消费者在独立副本NoBuild复核76/76和78/78通过，实际退出0且原bin/旧结果未变；这用于清理兼容性验证，不代签最新包78/80验收。Linux已在新独立目录恢复完整五轮，五轮及独立守卫按现有51ms预算全部通过，版本、预算、身份/引用/脚注契约保持原样。

## 剩余与平台边界

当前五轮及守卫没有未通过项；原WSLg环境Linux剪贴板的历史Pending不计为43，新私有Xvfb真实43按独立环境完成并另存增补。第3轮50.9018ms和第4轮50.4646ms均在现有51ms预算内，不列为当前失败。重复节点身份、引用与脚注的断言保持冻结544项回归原样，本轮未修改产品、预算或语义。

| 范围 | 状态与后续 |
| --- | --- |
| 当前冻结版本的M7软件退出 | [新执行与增补入口](../artifacts/markdown-m7-completion/c63fab7542ba4b22b9a63d9cbb736f1b/m7-exit.json)记录后续同源软件范围结论；[旧Pending退出记录](../artifacts/markdown-formal-m7/5fc570ff6e9940b7bb728346ba4c75b2/m7-exit.json)保持历史原件 |
| 远端原生CI | 用户已明确暂缓，状态DeferredByUser；没有实际远端运行，不作为本次本地软件退出门槛 |
| 设备矩阵 | 麒麟、实体Linux、原生Wayland、物理键鼠/触屏、真实辅助工具、系统外链、完整DPI与多屏继续待验证；当前Linux为WSLg/X11 |
| 全Demo / solution重新构建 | 冻结底本曾受PDF依赖Pkcs8.0.1审计阻断；未关闭审计或修改并行PDF工作。Markdown三库和NativeHost独立构建、组合Demo明确NoBuild，不声称整仓重新构建通过 |
| 超长块继续优化 | 当前正式预算已通过；块内行级排版虚拟化尚未实现，完整冷塑形和文字绘制仍使用原生全布局 |
| 通用图片资源上限 | 已知格式尺寸预检和已测PNG峰值有证据；未知/不完整格式仍可能先解码后检查，多图并发的通用native峰值硬上限尚无保证 |

时间预算今后可依据完整、稳定实测评估；身份错误、引用/脚注变化、布局几何差异、取消/租约失效、队列越界、来源不一致和不完整报告属于应修问题，不能用放宽时间预算接受。本轮无需再放宽预算。

设备缺口不阻塞可执行的软件M7工作。未发布、未变更版本。
