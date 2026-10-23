# 自定义 Wrap 前导空行修复与测试（2026-10-15）

当前M7可执行软件范围已通过，见[本次M7收束记录](markdown-m7-completion-20261015.md)及[新M7执行记录](../artifacts/markdown-m7-completion/c63fab7542ba4b22b9a63d9cbb736f1b/m7-exit.json)。Windows43项与Linux私有Xvfb/X11新43项真实复制、读回及恢复通过，实际producer/Main/外层均退出0；原WSLg共享显示的安全Pending保持历史记录，两个环境分别记账。

Windows50ms、Linux51ms及身份、引用、脚注语义不变；本次增补按同一冻结源码和产物SHA复用五轮、544×2、416×2、包/API及85/134工具合同，不声称再次执行。远端原生CI由用户暂缓（DeferredByUser）；麒麟、实体Linux/原生Wayland、完整DPI/多屏、物理输入和真实辅助工具继续待验证。未发布、未改版本。

## Wrap专项修复与当时定点验收

本轮只修复自定义代码呈现器调用`context.CreateText()`、保留默认Wrap且文本从空LF开始时的挂起。保留Wrap、完整UTF-16投影、原生字体与布局属性、身份 / 引用 / 脚注语义，不修改预算。此前DD330 / 57C68的538项回归及原生43项属于修复前候选，不签给新代码。

## 已取得的独立复现

[单例冻结信息](../artifacts/markdown-wrapped-lf/6754ff698fdb40dca46b04d2d7a7beda/before-frozen.json)记录真实旧Core与测试DLL；[测量入口](../artifacts/markdown-wrapped-lf/6754ff698fdb40dca46b04d2d7a7beda/before-constraints.jsonl)记录宽470 DIP、高度正无穷、Wrap、27字符纯文本和InlineCount=0，未返回Measure。[外部进程监督](../artifacts/markdown-wrapped-lf/6754ff698fdb40dca46b04d2d7a7beda/before-process/supervisor-result.json)实际30秒超时，并验证自有PID身份及终止；[线程栈](../artifacts/markdown-wrapped-lf/6754ff698fdb40dca46b04d2d7a7beda/before-stack/runtime-stdout.log)停在HarfBuzzTextShaper、TextFormatter及TextLayout创建路径。

该证据证明公开入口的独立挂起，未单凭栈断言具体内部循环算法。旧分组挂起和未执行的最小源码仍保留历史属性，不改写成已执行证据。

## 修复与差分

修复在每次纯文本Wrap布局创建时，将原文拆为原生TextCharacters和显式TextEndOfLine，临时交给base.CreateTextLayout，finally恢复原状态。LF / CR各占1个UTF-16位置，CRLF仍占2个；不修改Text、Inline树或字体声明，保留原生LineSpacing、约束和截断参数。

[同一测试DLL差分](../artifacts/markdown-wrapped-lf/6754ff698fdb40dca46b04d2d7a7beda/final-revision2-evidence-binding.json)已完成：86BBEC82测试DLL在旧DD330下30秒超时；177个其它依赖保持相同，只换最终1AEF42 Core后1/1通过、实际退出0。两次都是470 DIP × 正无穷、27个UTF-16字符、Wrap和InlineCount=0，新布局生成8行并完成测量及绘制。[最初修复候选的差分](../artifacts/markdown-wrapped-lf/6754ff698fdb40dca46b04d2d7a7beda/same-test-dll-differential.json)另保留，不转签最终DLL。

[独立原生窗口旧版结果](../artifacts/markdown-leading-lf/ae0209a3c6c641b2b11982ba02f8d1f7/native-before-summary.json)在Windows和Linux各31项通过；其实际约束为590 DIP × 正无穷、文本25个UTF-16字符，与Headless案例不同。该原生案例没有复现挂起，不将其转写为修复前失败；两端取栈观察器失败也不算产品挂起。

新增6个回归实例的首次专属运行5通过1失败，[完整Headless首测](../artifacts/markdown-wrap-fix/4e635a31e1de441bbfa4cc4b78abca88/windows-regressions/Markdown.Headless.Tests.tests.log)239/240。唯一失败是新增字体测试把raw-NoWrap的NewLineLength与显式EOL的Wrap路径直接比较；原生实现分别记录这两种表示。仅该新增字体测试改用无换行原生正文验证当前字形、完整正文行指标及每UTF-16位置的光标几何，旧共享断言和产品代码未为此放宽；[专属重测](../artifacts/markdown-wrapped-lf/6754ff698fdb40dca46b04d2d7a7beda/expanded-targets-attempt2.xml)6/6通过，原失败不删除。另4个新增几何实例独立覆盖LF / CRLF / CR全部源位置、WrapOverflow、行距、宽度重排、样式和有限高度截断。

## 最终回归与原生检查

冻结[revision2源清单](../artifacts/markdown-wrap-fix/4e635a31e1de441bbfa4cc4b78abca88/revision2/revision-source-manifest.json)采用上一轮稳定底本并明确覆入当前Markdown；并行PDF / Charts不混入。实际[Windows](../artifacts/markdown-wrap-fix/4e635a31e1de441bbfa4cc4b78abca88/revision2/windows-regressions/results.json)和[Linux SDK](../artifacts/markdown-wrap-fix/4e635a31e1de441bbfa4cc4b78abca88/revision2/linux-sdk/evidence/regressions/results.json)各544/544：Core221、Streaming60、Headless240、Themes10、TextMate13，失败 / 错误 / 跳过 / 未执行均0、源码执行中不变。Windows Core为1AEF42，Linux独立编译Core为A8A4FA；不称同一个DLL。

新三包[独立消费](../artifacts/markdown-wrap-fix/4e635a31e1de441bbfa4cc4b78abca88/revision2/windows-hosts/package-consumer-results.json)78/78、80/80通过，内嵌AXAML各18项；[包内DLL绑定](../artifacts/markdown-wrap-fix/4e635a31e1de441bbfa4cc4b78abca88/revision2/package-assembly-binding.json)与最终测试产物逐项同SHA。[实际公共API及默认值对比](../artifacts/markdown-wrap-fix/4e635a31e1de441bbfa4cc4b78abca88/revision2/public-api-comparison.json)无差异，文档快照更新。

[Windows原生后测](../artifacts/markdown-leading-lf/ae0209a3c6c641b2b11982ba02f8d1f7/windows-after-summary.json)和[Linux原生后测](../artifacts/markdown-leading-lf/ae0209a3c6c641b2b11982ba02f8d1f7/linux-after-summary.json)使用同一C766探针、仅替各平台最终Core：两端均短例31/31、长例42/42，真实Window句柄、退出0、运行文件哈希不变。覆盖默认Wrap、空行、光标 / 自动化边界、590↔280重排、脚注、流式完成 / 全文替换、选区复制文本及资源释放。复制通过Copying事件截获文本验证，不将此声称为新OS剪贴板复制 / 恢复验收。

Linux首次后测在探针启动前遇到WSL VM创建超时0x800705b4；[原监督](../artifacts/markdown-leading-lf/ae0209a3c6c641b2b11982ba02f8d1f7/linux-after-launch-execution/supervisor-result.json)保留，归为基础设施启动失败，不是控件超时。一次独立正常恢复重试后才得到上述实际后测结果；没有关闭发行版、修改系统配置或重跑before以寻求失败。

同一冻结26834原生宿主、仅替最终Core的[长块100样本 / 10预热](../artifacts/markdown-wrap-fix/4e635a31e1de441bbfa4cc4b78abca88/long-block.json)实际退出0、三组预算和分配守卫均通过，未改变门槛。

| 场景 | 总P95（ms） | 布局P95（ms） |
| --- | ---: | ---: |
| 100KiB单代码长行 | 116.7903 | 57.0814 |
| 100KiB单段落 | 142.4981 | 93.3569 |
| 30×30宽表 | 433.1185 | 361.8042 |

后两组高于前轮定点值，本次仅确认原预算通过，不声称修复带来性能提升或凭单轮认定因果。后续同revision2完整11组五轮已通过，见上方新验收入口。

本轮起点及源哈希见[开始记录](../artifacts/markdown-wrap-fix/4e635a31e1de441bbfa4cc4b78abca88/start.json)，[最终验收汇总](../artifacts/markdown-wrap-fix/4e635a31e1de441bbfa4cc4b78abca88/revision2/final-results.json)核对16份证据哈希、最终源码、样本和预算。Linux长会话UI P95仍51ms、Windows仍50ms；身份、引用、脚注语义保持原契约。完整新五轮已由后续验收通过；远端CI、麒麟 / 实体Linux / 原生Wayland、完整DPI / 多屏、真实辅助工具和物理输入继续待验证；当前Linux为WSLg / X11。未发布、未改版本。
