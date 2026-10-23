# Markdown 依赖与发行通知

核验日期：2026 年 10 月 13 日。实施采用中央版本与 `packages.lock.json`；产品版本沿仓库 `0.2.0-preview.2`。功能包仅依赖 Avalonia 和 Markdig；TextMate 放在独立可选包，基础功能/主题包不传递依赖高亮引擎。

| 组件 | 固定版本 / 目标资产 | 依赖与许可依据 |
| --- | --- | --- |
| .NET SDK | 10.0.401；本库 net10.0 / C# 14 | `global.json` 禁止滚动，重新读取仓库构建属性确认 |
| Avalonia | 12.0.3 | 既有中央版本；Markdown 使用框架公开原生文本排版 API |
| Markdig | 0.42.0；net9.0 / net8.0 可由 net10.0 消费 | 两种现代 .NET 资产无额外包依赖；BSD-2-Clause。[固定包](https://www.nuget.org/packages/Markdig/0.42.0)、[固定许可](https://github.com/xoofx/markdig/blob/0.42.0/license.txt) |
| TextMateSharp | 2.0.4；netstandard2.0 | 依赖 Onigwrap ≥ 1.0.11；MIT。[固定包](https://www.nuget.org/packages/TextMateSharp/2.0.4)、[项目许可](https://github.com/danipen/TextMateSharp/blob/master/LICENSE.md) |
| TextMateSharp.Grammars | 2.0.4；netstandard2.0 | 依赖 TextMateSharp ≥ 2.0.4；NuGet 标示 MIT，实际包内 typst/tinymist 元数据另标 Apache-2.0。[固定包](https://www.nuget.org/packages/TextMateSharp.Grammars/2.0.4)、[固定源码](https://github.com/danipen/TextMateSharp/tree/622d1b240a5be474939857c0b2780249022f14a3) |
| Onigwrap | 已还原锁定 1.0.11 | MIT；依赖 System.Memory ≥ 4.6.0，net10.0 还原图由 SDK 裁剪该兼容包；随包带原生 Oniguruma 库及第三方通知。[固定包](https://www.nuget.org/packages/Onigwrap/1.0.11)、[固定源码](https://github.com/aikawayataro/Onigwrap/tree/dd8a1cbbe3190c18033b96f2922b95c1761ae793) |
| 官方 Markdown | 12.3.0，仅参考 | 依赖 Avalonia ≥ 12.0.3；本库不引入商业运行时、许可 key 或官方文档程序集。[固定包](https://www.nuget.org/packages/Avalonia.Controls.Markdown/12.3.0) |

Markdig 1.4.0 已发布，直接提供 net10.0 资产；本轮选择已实施转换层的 0.42.0，升级需重新运行语法、投影和任意分块等价用例，不能只改版本。固定旧版本不代表它是最新。[候选包](https://www.nuget.org/packages/Markdig/1.4.0)

## 解析与渲染登记

默认 pipeline 仅应启用已实现 renderer 的扩展：Alert、AutoLink、Emoji/Smiley、EmphasisExtras、Footnote、GridTable、PipeTable 与 TaskList。`UseAdvancedExtensions()` 同时启用数学、图表、媒体、definition list 等，不能直接用它表示全部支持。HTML 原文保留及 symbol 边界见 [功能对照](markdown-official-alignment.md)。[固定扩展配置源码](https://github.com/xoofx/markdig/blob/0.42.0/src/Markdig/MarkdownExtensions.cs)

Markdig 自身 parser 或 HTML renderer 的通过结果不作为本库原生 renderer、逻辑文本投影或选择的证据。未知 AST 节点应降级并诊断；配置变化重新建立文档代次。

## 可选高亮与平台边界

适配直接使用 TextMateSharp 的语法 token 与主题颜色，转换为本库代码 UTF-16 样式范围，不引用 AvaloniaEdit，也不生成编辑器控件。逐行保留 rule stack 以处理多行注释；未知语言返回空样式范围，原文继续显示。TextMateSharp 不支持跨语法 injection，不能宣称与 VS Code 的语义高亮完全一致。[引擎用法](https://github.com/danipen/TextMateSharp)

已检查 Onigwrap 1.0.11 实际包：包含 `win-x64` / `win-x86` / `win-arm64`、`linux-x64` / `linux-arm64`、对应 musl / bionic、`osx-x64` / `osx-arm64`、`maccatalyst-x64` / `maccatalyst-arm64` 原生资产，以及 WASM 构建资产。资产存在不等于已在目标设备加载成功。Windows、可用 Linux 与麒麟分别记录 tokenizer 实跑；缺少设备只能记录“待验证”。缺少 native library 时由控件诊断并退回纯文本，核心阅读功能不依赖 TextMate。

## 通知、锁文件和包验收

每项目锁文件固定完整还原图与 content hash；集成使用 `dotnet restore --locked-mode`。新增依赖首次普通还原生成锁文件，再进行 locked restore、Release 构建与隔离 NuGet 消费。实际 nuspec 与 RID 文件列表须在包验收时核对，NuGet 的 computed TFM 不能替代原生库加载。

TextMate 的首次还原已生成并读取功能/测试项目锁文件：TextMateSharp / Grammars 为 2.0.4，Onigwrap 为 1.0.11。实际 nuspec 将两种 TextMate 包绑定到源码 commit `622d1b240a5be474939857c0b2780249022f14a3`，Onigwrap 为 `dd8a1cbbe3190c18033b96f2922b95c1761ae793`。

2026-10-13 在 Windows x64 / .NET 10.0.12 实跑 TextMate 专属测试，13/13 通过、零失败/跳过。固定语料验证真实 native tokenizer、语言别名、中文/Emoji 的 UTF-16 范围、CRLF、多行状态、未知语言降级、取消、共享并发及主题失效；Release 构建零警告/错误。TextMate 项目与测试 locked restore 成功（含 NuGet 元数据检查）。2026-10-14 在实际Ubuntu26.04/WSL2 x64 runtime运行Windows IL及真正LinuxSDK10.0.401编译的隔离产物，TextMate均13/13通过，native Onigwrap加载已有证据；[Linux结果](../artifacts/linux-build/68350f908ae04c17a94f8f295c665a8a/retest-result.json)及[平台边界](markdown-platform-acceptance.md)保留。麒麟、其它架构/RID仍待实跑，不能由资产存在推断成功。

BSD-2-Clause / MIT 发行应携带原版权、许可与免责声明。TextMate 可选包带 [通知](../src/NT.AvaloniaControl.Markdown.TextMate/THIRD-PARTY-NOTICES.md)和固定源码版 MIT、Onigwrap 全通知、Tinymist v0.12.18 Apache-2.0 原文。实际 Grammars DLL 的 51 个语言包元数据已读取：50 个 MIT、1 个 Apache-2.0；其 nupkg 只有程序集而未附单独语法 NOTICE，故额外保留元数据和 Tinymist 许可。此记录不声称完成每份语法/主题的历史来源审计。打包时保留依赖原包与通知，不能笼统重新许可全部资源。

TextMate 本地打包已实查：`NT.AvaloniaControl.Markdown.TextMate.0.2.0-preview.2.nupkg` 包含 `README.md`、`THIRD-PARTY-NOTICES.md` 和 `Licenses/` 下五份通知/元数据文件；nuspec 依赖同版本功能包及 TextMateSharp / Grammars 2.0.4。本次 nupkg SHA256 为 `94401B2B3B762BD35FFA6506077875B0FCFE650206F7F03DD6B2EEDA082DE980`。这不替代 MT19 的隔离消费或最终发行包复核。

MT19 隔离 NuGet 消费已实跑通过：FunctionOnly 15/15，DefaultTheme + TextMate 17/17；两者在 `artifacts/consumer/<guid>` 使用独立构建/中央版本边界和各自全新 `.packages`。普通与锁定还原、编译AXAML TwoWay绑定、离屏全文选择、跨线程流式最终提交及后续宿主替换通过；可选包实际 native tokenizer 加载通过。测试使用复制前取消事件捕获模型文本，不宣称真实平台剪贴板通过。三包消费哈希与目录见 [汇总](../artifacts/markdown-package-consumer-results.json)，与早期本地TextMate单包hash分别保留。

功能包 [通知](../src/NT.AvaloniaControl.Markdown/THIRD-PARTY-NOTICES.md)和 `Licenses/Markdig-BSD-2-Clause.txt` 已建立，固定原文 SHA256 为 `AE543293657B6782E3D4A76257DC4157210CE88A352EF8392065D3F04DCD88D1`。2026-10-13 再次直接检查 `artifacts/packages` 中三产品的实际ZIP：功能包已包含该BSD原文、README和通知；TextMate包已包含README、通知及五份许可/元数据；主题包的AXAML资源编译在主题程序集，真实资源URI消费已有证据。

实际 nuspec 同时核对：功能包仅列 Avalonia 12.0.3 / Markdig 0.42.0；Themes仅列同版本功能包；TextMate列同版本功能包和TextMateSharp / Grammars 2.0.4。许可入包和单向依赖已经有实际证据。后续同版本开发包仍可能重新构建，最终SHA与消费者结果以统一 [消费汇总](../artifacts/markdown-package-consumer-results.json)为准，不使用早期包hash作为最终发布标识。
