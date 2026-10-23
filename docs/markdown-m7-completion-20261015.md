# Markdown M7 软件验收收束（2026-10-15）

本次补齐同一冻结版本的Linux隔离X11真实剪贴板验收，M7可执行软件范围已通过；整体设备矩阵仍待验证。最新状态与增补证据由[本次M7执行记录](../artifacts/markdown-m7-completion/c63fab7542ba4b22b9a63d9cbb736f1b/m7-exit.json)维护。远端原生CI已[由用户暂缓](../artifacts/markdown-m7-completion/c63fab7542ba4b22b9a63d9cbb736f1b/remote-ci-deferred-by-user.json)，不作为本次软件退出门槛；未执行远端任务，不标记远端CI通过。未发布、未改版本。

软件来源仍为[Wrap修复后的冻结revision2](../artifacts/markdown-wrap-fix/4e635a31e1de441bbfa4cc4b78abca88/revision2/revision-source-manifest.json)：Windows Core `1AEF42AB16F6133750047AD4708A4ECB8D8110A5806722F6421D21FD64478F4A`，实际Linux SDK Core `A8A4FA5A295606E52925922EA0857E713B0172633A70A95733136A9E019FC8A7`。Windows长流ALL实际提交与Rendered UI P95维持50ms，Linux同两处维持51ms；其它预算、重复节点身份、引用和脚注语义不变。Linux第3、4轮按现有51ms政策计为通过，不列当前失败。

## 本次补测与证据复用

| 范围 | 实际结论 | 本次增补的来源方式 |
| --- | --- | --- |
| Windows完整五轮 | [五报告独立守卫](../artifacts/markdown-formal-m7/5fc570ff6e9940b7bb728346ba4c75b2/windows/guard.json)通过；每轮11组及原样本/会话/预热数 | 已有实际执行按冻结源码与产物SHA复用，未再跑五轮 |
| Linux / WSLg X11完整五轮 | [独立守卫](../artifacts/markdown-formal-wrap-linux/ed73715b0a2d4a61a2f05b734bf05ac0/formal-after-storage-87202a04751a4a85b8446b7894f160eb/guard.json)按51ms通过；原producer50字段与报告不改 | 同源证据复用，性能环境与新私有Xvfb分别记录 |
| Markdown五项目、包与API | 两平台各544项；三包独立消费78/80项、各18项内嵌AXAML；API和默认值无签名差异 | [56项来源复核](../artifacts/markdown-formal-m7/5fc570ff6e9940b7bb728346ba4c75b2/software-reuse-verification-after-compatibility.json)绑定既有实际XML与包，不声称本次重新执行 |
| 既有控件六项目 | Windows / Linux各416/416，18阶段实际退出均0，891冻结输入前后未变 | [Windows执行](../artifacts/markdown-formal-m7/2fe1e682c532434ebcb566890ec68ec4/windows-compatibility-preparation-7b7578d761724543b2c3f79b692d113b/execution/execution-summary.json)与[Linux执行](../artifacts/markdown-formal-wrap-linux/ed73715b0a2d4a61a2f05b734bf05ac0/compatibility-m7-ae4b3550b1f7443a9889f8d734412c70/execution-summary.json)在本增补中按SHA复用 |
| 原生工具与性能政策合同 | 两平台各85项工具合同、134项政策合同 | [160项Markdown专属输入核对](../artifacts/markdown-formal-m7/5fc570ff6e9940b7bb728346ba4c75b2/current-live-markdown-source-binding.json)一致，明确复用；不扩大为当前公共配置或并行PDF/Charts完全匹配 |
| Windows最终原生smoke | 43/43，真实复制与原文本恢复，producer / Main / 外层实际退出0 | [已有独立核验](../artifacts/markdown-formal-m7/5fc570ff6e9940b7bb728346ba4c75b2/windows-smoke-desktop-42ecaaf62d5e4cde8828521ba891dfb7/independent-verification.json)复用 |
| 原WSLg共享显示 | 40项功能通过、41项报告；剪贴板安全Pending，producer原退出1，未覆盖未知内容 | [历史环境记录](../artifacts/markdown-formal-wrap-linux/ed73715b0a2d4a61a2f05b734bf05ac0/smoke-m7-13c8ea8fe32a476489a4b4d54a1c118a/execution-summary.json)原样保留，不转签为43 |
| 新Linux私有Xvfb / X11 | 43/43，真实复制/读回与原文本恢复/读回；producer、Main和外层实际退出0 | 本次新增：[原始报告](../artifacts/markdown-private-x11-b3359488b5304232a4910e7a6c20d6b7/attempt-aa3efb6c77de4fa988d07546773e8c5d/native/report.json)、[Main结果](../artifacts/markdown-private-x11-b3359488b5304232a4910e7a6c20d6b7/attempt-aa3efb6c77de4fa988d07546773e8c5d/native/result.json)、[隔离补测汇总](../artifacts/markdown-private-x11-b3359488b5304232a4910e7a6c20d6b7/isolated-clipboard-acceptance.json) |

本次没有修改产品、原主入口或当前workflow。原[五轮与M7阶段记录](markdown-formal-wrap-m7-20261015.md)、原WSLg Pending、[上轮M7退出记录](../artifacts/markdown-formal-m7/5fc570ff6e9940b7bb728346ba4c75b2/m7-exit.json)及旧归档保持其历史结论；新软件范围的通过由本次增补另行绑定，不改写旧`SoftwareM7Passed=false`。

## 私有Linux剪贴板与清理边界

实际显示为`:2766`，1440×900、96 DPI，使用独立Xauthority（0600）、`-nolisten tcp`及整棵新进程树的私有socket目录映射。[外层监督](../artifacts/markdown-private-x11-b3359488b5304232a4910e7a6c20d6b7/outer-44d4f1fc94b448af8e2265a0cdb49a2d/execution-summary.json)确认真实用户`:0`的socket身份和父环境前后未变，未读写用户剪贴板；[attempt监督](../artifacts/markdown-private-x11-b3359488b5304232a4910e7a6c20d6b7/attempt-aa3efb6c77de4fa988d07546773e8c5d/execution-summary.json)记录原生producer PID547实际退出0、无超时，Main源码与全部冻结运行文件稳定。

holder PID465在原生运行前后均存活。已知原文本与初始实际读回SHA均为`F4716F7D4ABDD55D229C182D7C92DAF97E684A4153F7AEB2D78C92DE713E110B`。冻结Demo在进程内真实验证复制内容和恢复原文本后的精确读回；外部observer未捕获Demo短暂恢复窗口，其`DemoOriginalReadbackSHA256`仍为null。Demo退出后，存活holder执行的是独立**Harness恢复**，其外部实际读回SHA与上述原值相同，不能把这次Harness操作冒称Demo的外部恢复证据。

Xvfb、holder和native-main三个私有进程组清理后均无残余，私有cookie、临时socket映射目录已清理。三库SHA分别为Linux Core `A8A4FA5A…`、Themes `9FA5CF7A…`、TextMate `6A2B371A…`，与此前同源验收一致；Demo为旧`D9D14C87…`宿主加当前三库的明确NoBuild组合，不声称Demo源码新编译。图像为原生Window的RenderTargetBitmap，不代表屏幕scan-out或实体Linux操作证据。

## 已保留的问题记录与剩余范围

原共享显示因原内容不可安全保存而Pending属于环境安全边界，不是放宽预算或产品修复。隔离工具准备、早期启动失败、旧归档脚本的StrictMode调用兼容失败及其修正记录均保留在各自证据目录；旧失败和退出不改成成功。本次补测未发现新的预算或身份、引用、脚注语义回归，不以加大时间门槛接受语义错误。

远端原生CI状态为`DeferredByUser`，没有执行或发布。整体平台矩阵继续Pending：麒麟、实体Linux、原生Wayland、完整DPI/多屏、物理键鼠/触屏、真实辅助工具与系统外链仍须对应设备验证。私有Xvfb的软件剪贴板通过与WSLg性能通过分别记账，不合并环境字段。

现有首版边界继续保留：超长块尚未实现块内行级排版虚拟化；未知或不完整图片格式仍可能先解码后检查，多图并发的通用native峰值硬上限尚无保证。当前正式预算已通过，这些后续优化不改本次已测范围的结论。
