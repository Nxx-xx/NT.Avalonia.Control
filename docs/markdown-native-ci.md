# Markdown 原生 CI 与本机验收

入口是 [Test-MarkdownNative.ps1](../scripts/Test-MarkdownNative.ps1)，监督与核验逻辑见 [Helpers](../scripts/Test-MarkdownNative.Helpers.ps1)，轻量合同由 [Test-MarkdownNative.Tests.ps1](../scripts/Tests/Test-MarkdownNative.Tests.ps1) 执行。普通软件CI先跑这些合同，再执行原有Verify。

## 工作流

[markdown-native.yml](../.github/workflows/markdown-native.yml) 在push/PR的Windows和Ubuntu运行原生smoke。Ubuntu使用明确的Xvfb/X11环境，不能由此宣称实体桌面或Wayland通过。

正式性能由workflow_dispatch启动，使用带有`self-hosted`、`Windows`或`Linux`、`X64`、`markdown-native-desktop`标签的稳定桌面runner。每个平台串行采样，不自动创建WSL/Xvfb，不修改桌面缩放、不清空剪贴板，也不因失败重启样本。正式运行保持窗口可见、缩放1、1200×700视口及稳定负载；Linux需要已有X11 DISPLAY和可用字体。

远端workflow是否已执行、对应runner是否已配置，必须另有真实运行记录；本地测试脚本通过不等于GitHub任务已通过。

## 本机调用

在仓库根目录使用PowerShell7：

```powershell
pwsh -NoProfile -File scripts/Tests/Test-MarkdownNative.Tests.ps1
pwsh -NoProfile -File scripts/Test-MarkdownNative.ps1 -Mode Smoke
pwsh -NoProfile -File scripts/Test-MarkdownNative.ps1 -Mode Formal
```

默认会locked restore并构建对应Demo或NativeHost。`SourceRoot`可指定稳定的隔离源码。`SourceHostPath`在构建模式下必须是该源码的默认构建输出，不能用无关外部DLL冒充本次构建。

`SourceBuildRequested`记录是否请求构建。`BuildStarted`和`SourceBuildPerformed`仅在自有构建命令实际启动后为true，失败的构建尝试也可能为true；`BuildSucceeded`仅在该命令完整成功后为true。源码归因`BuiltFromCapturedSource`还要求构建后源码核对通过。缺少显示环境、还原失败或命令未启动不会被记录为已经构建。

已有产物可显式使用`-NoBuild -SourceHostPath <目录>`；该模式冻结并记录传入运行文件，同时单独核验源码，但**不证明产物由这些源码构建**。需要源码归因时，应从同一个隔离SourceRoot构建和测试。跨平台运行Windows生成的可移植IL也必须登记为NoBuild运行，不能称为Linux SDK重新编译。

每次使用全新的OutputDirectory；已有目录会拒绝覆盖。证据默认写入`artifacts/markdown-native`，包含源码前后清单、环境、四DLL及完整运行文件哈希、各子进程监督、双流有界日志、原始报告、独立guard和图像。

## 判定及预算

- 完整smoke要求43项真实通过、剪贴板复制和恢复、producer退出0及完整监督。无法预先确认原剪贴板可安全替换时不写入它，以`剪贴板测试已执行=false`保留Pending，producer原退出1与runner退出2分别记录。实际读取异常或超时、执行复制后复制/恢复失败均为Failed，不能当作安全跳过。40项功能通过不能写成43项完整通过。
- 正式入口固定五个独立进程；每轮11组、100样本、100短会话、100长会话及10次预热。没有缩减采样、仅选组或pilot入口。
- producer保留原50ms长会话UI门槛。Windows继续使用更严格50ms；Linux显式使用已授权51ms，独立guard核验原raw及原布尔判定后复评。所有其他预算、接收记录、资源/队列及语义检查保持原值。
- producer退出1时只要报告完整且来源/退出/元数据一致，仍收齐五份，再由独立guard判定；退出1不自动变成功，缺报告、错误、身份不符、超时或日志监督不完整均失败。
- 源码或冻结运行文件在执行期间变化会拒绝验收。并行开发时使用隔离快照，不忽略变化文件来凑通过。

## 当前证据

本轮本地执行、发现的脚本问题、修复后结果及仍Pending范围见 [本轮验收](markdown-followup-20261015.md)。历史性能基线与政策授权见 [性能记录](markdown-performance.md)。麒麟、真实辅助工具、物理输入及完整DPI/多屏继续按 [平台矩阵](markdown-platform-acceptance.md) 验证。
