# PDF Viewer：Ubuntu 原生验证与 CI

P1 将 PDFium 后端验证与真实 Avalonia 桌面窗口验证拆开记录。Linux 程序使用 `Avalonia.Desktop` 和 X11 后端，不引用 `Avalonia.Headless`；Core 与 Headless 回归仍分别执行。WSLg、Xvfb 和物理桌面具有不同的验收含义，报告不会将前两者记为麒麟或 Ubuntu 实机桌面验收。

## 可复现入口

Ubuntu x64 / ARM64 的入口如下。输出目录必须不存在；失败时保留原始日志、来源、ELF 审计和已生成的结果。

```bash
bash scripts/Verify-PdfViewer-Linux.sh \
  --output artifacts/pdfviewer-native/ubuntu-local \
  --corpus-cache artifacts/pdfviewer/real-corpus-cache
```

没有图形会话时，可以用 `--backend-only` 只验证 PDFium，或通过 `xvfb-run -a bash scripts/Verify-PdfViewer-Linux.sh ...` 创建 CI X11 显示。默认要求现有 `DISPLAY` 并创建真实原生窗口。脚本不安装依赖、不更改系统字体和桌面设置、不启动 Docker，也不管理 WSL 分发版。

`--no-build` 复用现有 P1 / Core / Headless 输出，报告会记为没有执行编译；不能据此声称已在 Linux 还原或构建。`--skip-tests` 明确跳过两个回归套件。两者都不会将未执行的工作记为通过。

Windows 使用相同的独立主机：

```powershell
./scripts/Test-PdfViewerNative.ps1 -OutputDirectory artifacts/pdfviewer-native/windows-local
```

它会锁定还原、构建，执行后端、原生窗口及 Core / Headless 回归，并为每个拥有的子进程记录超时和退出码。超时只停止本次启动的进程树。

## 当前验证边界

- 自有两页 PDF：页面和书签、字符文本、跨页搜索、真实栅格渲染。
- 真实 GNU make 4.4.1 手册：固定 SHA256 `A4BC06026984382815E392159E60A4BC356CAC6C5B81E5AE84FA4F58F621F4D3`，229 页，首／中页渲染以及跨页搜索。每次复制或下载都核对来源清单中的长度和哈希；上游版本改变时拒绝继续。来源、GFDL 条件沿用 [P0 真实语料清单](../benchmarks/PdfViewer.P0/Fixtures/real-corpus-sources.json)，不进入产品包。
- 原生窗口：非零平台句柄，系统字体匹配器能为“中／文／页”找到字体并记录实际字体族，控件完成加载、搜索和导航，页面栅格进入缓存，布局成立，等待 `CompositionBatch.Rendered`，保存窗口视觉树的 `RenderTargetBitmap` PNG。该字体前置条件只验证三字的匹配，不代表完整 CJK 排版或表单 IME 已验收。
- 原生依赖：PDFium、SkiaSharp、HarfBuzzSharp 的 `readelf` ELF 头、动态依赖、版本符号和 `ldd` 原文；任何 `not found` 均失败。报告记录所审计资产的 SHA256，并要求运行进程 `/proc/self/maps` 中实际加载的 `libpdfium.so`、`libSkiaSharp.so`、`libHarfBuzzSharp.so` 路径和 SHA256 与审计一致。
- 源码与证据：运行前后源码哈希必须一致，结果中绑定产品／主机程序集哈希，保留失败阶段和全部已有证据。`--no-build` 只记录当前源码及实际二进制，不作二者的独立构建来源保证。

这些检查没有覆盖物理键鼠、IME、剪贴板桌面集成、屏幕扫描输出、DPI 多屏、打印和性能门槛。真实 PDF 的首／中页渲染也不代表逐页视觉验收。所有路径仍需遵守 [兼容性边界](pdfviewer-compatibility-boundaries.md)。

## Ubuntu CI

[pdfviewer-native.yml](../.github/workflows/pdfviewer-native.yml) 在 push / pull request 时使用 Windows hosted runner 和固定 `ubuntu-24.04` x64 runner。它没有发布、上传 NuGet 或远程手动触发步骤。

两个平台均执行锁定还原和构建、Core / Headless 回归、原生后端与窗口 smoke，以及使用独立空包缓存的功能包／Themes 包消费者。Ubuntu 在 Xvfb 中运行真实 X11 窗口。NuGet audit 保持默认开启；系统依赖通过显式 apt 包清单安装。无论成功或失败，CI 都上传日志、XML、来源／程序集／原生资产身份和 PNG；外部手册 PDF 本身不进入该 artifact。

工作流落地不等于远端执行成功。只有 GitHub 的实际 run 记录才能补上对应证据；本地 Windows 或 WSL 结果不替代 `ubuntu-24.04` 的 hosted run。

## 本地环境与待补项

本轮已发现已有 Ubuntu 26.04 LTS（WSL2 x64、内核 `6.6.114.1-microsoft-standard-WSL2`），WSLg 提供 X11 `DISPLAY=:0`。环境包含 X11、fontconfig、freetype 和 GL/EGL 库，系统 glibc 为 2.43。它与 CI 的 Ubuntu 24.04 以及计划中的麒麟 x64 / ARM64 实机均分开记账。

对固定 PDFium 155.0.8044 Linux x64 资产的预审计得到 ELF64 x86-64，最高导入版本 `GLIBC_2.16`，没有导入 `GLIBCXX_*` 或 `CXXABI_*` 版本符号；`ldd` 的依赖在该 Ubuntu 环境中均能解析。这个结果描述这一份资产的 ABI 导入，不据此宣布所有 glibc 2.16 及以上发行版都已支持，也不推广到 ARM64。

本轮正式运行的目录、产品哈希、测试计数和 Linux 实际运行状态由 [P1 验证记录](pdfviewer-p1-validation.md) 与其机器记录给出。Ubuntu 24.04 CI 首次远端执行、麒麟 x64 / ARM64、物理桌面、未覆盖的发行版和 ARM64 ABI 仍需要各自的证据。

### P2 收尾批（2026-10-22，第十批）的 WSL 运行

该轮 WSL 环境与 P1 时不同：WSLg 不可用（无 `DISPLAY` / `/mnt/wslg`），故 Linux 原生窗口验证按 CI 显示语义经 `xvfb-run -a` 执行，并记为 Xvfb 证据，不是 Ubuntu 实机桌面。环境为本轮自建：用户级 .NET SDK 10.0.402（`~/.dotnet`），root apt 安装 xvfb、X11 / GL / EGL、freetype、libicu 与 **fonts-noto-cjk**。

首轮运行 `artifacts/pdfviewer-native/linux-20261022T022038Z` 因环境缺 CJK 字体在 `native-cjk-font-4E2D` 前置失败（原生窗口句柄已建立；`ClassicDesktopStyleApplicationLifetime` 在缺字体路径上抛 `NullReferenceException` 退出 1），按规则保留为失败证据。装入 Noto CJK（fc-list 确认 Noto Serif CJK）后完整复跑 `linux-20261022T022442Z` 通过：后端 11/11、原生窗口 22/22（含实际中文字族匹配记录）、Core 111/111、Headless 82/82，`run.json` 记录 `Passed True / Exit 0 / Stage complete` 与运行前后 95 份源文件哈希一致。同日 Windows 运行 `windows-20261022T021311Z` 同数通过。详见[阅读版状态](pdfviewer-status.md)第十批。
