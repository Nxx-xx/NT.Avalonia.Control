# PDF Viewer P1 验证记录

验证日期：2026-10-16（Asia/Hong_Kong；原始 UTC 日志日期为 2026-10-15）。本轮实现阅读公共契约、权限与能力状态、文档租约、异步释放和全进程串行调度，接入 Ubuntu 原生验证与独立 CI。代码行为及迁移见[公共契约](pdfviewer-p1-contracts.md)，平台入口见[Ubuntu 原生说明](pdfviewer-linux-native.md)，阶段归属见[开发计划](pdfviewer-development-plan.md)。

最终源快照、程序集、原生模块、逐项检查、XML、包消费者和人工看图的绑定由[机器记录](validation/pdfviewer-p1-20261016.json)给出。记录导出器会拒绝缺项、哈希不同、过期结果、失败或跳过的回归，以及源码项目混入包消费者依赖图。旧 P0 记录保持原样；它们提供技术来源，不把旧二进制的大矩阵通过结果计入本轮。

## 实现和验证对象

| 工作包 | 已落地行为 | 验证依据 |
| --- | --- | --- |
| 数据源 | `PdfDocumentSource` 文件、捕获字节、可重开的流工厂；保留旧路径 / 流 / 字节入口 | 所有权、工厂流释放、取消、密码重试、旧请求不能覆盖新文档、真实包消费 |
| 文档契约 | 标准 DocInfo、文件版本、原始权限与密码访问身份、签名对象数量、结构化错误 | Core 原生读取与边界、加密夹具、独立 ctypes / pypdf 密码角色检查 |
| 控件契约 | `DocumentSource`、状态 / 会话 / 错误、实现与允许分开的能力、Allow 开关、可绑定命令 | Core 权限真值表、Headless 控件 / 命令 / 公共 API 一致性、消费者 |
| 调度 | 全进程一个 native worker；前台 / 搜索 / 缩略图 4:2:1 加权轮转，各队列 FIFO，搜索逐页让出 | 排序、公平性、取消、异常隔离、全局串行和后续工作继续执行 |
| 释放 | 文档与控件 `IAsyncDisposable`；同步 Dispose 关闭准入并开始取消，异步等待 native close / 固定输入释放 | 排队取消、运行租约、重复释放、多个文档、UI 心跳、非协作流工厂和重入加载 |
| 平台接口 | 可注入文件选择器及 Avalonia 默认打开；打印 / 分享接口固定 | 延迟选择器结果隔离、宿主字节快照所有权；没有打印服务也可阅读 |
| 集成 | Usage 与统一 Demo 接入状态 / 宿主开关，双包各自新缓存消费 | Release 编译、包内 / 已还原 / 实际执行 DLL 哈希一致 |

元数据按每字段 1 MiB 上限拒绝异常长度；日期保留原文。签名数量不验证签名信任或修改许可。受限 user 密码下 `GetTextAsync` 现在拒绝未经允许的全文提取，搜索仍作为阅读操作；这些权限接口不提供 DRM 隔离。`IsReadOnly` 不禁止复制或搜索。

控件当前实现 Read / Search / Copy；Save / Annotations / Forms / Print / Share 仍明确 `IsImplemented=false`。受限生产写入 API 仍在独立 `PdfEditSession` 中，不能从宿主服务存在或能力开关为 true 推导控件已经具备编辑、保存或打印 UI。

## 执行矩阵

| 执行环境 | 编译来源 | 后端检查 | 原生窗口检查 | Core | Headless |
| --- | --- | --- | --- | --- | --- |
| Windows x64，系统构建 10.0.26200 | Windows 锁定还原及 Release 构建 | 11 / 11 | 22 / 22 | 92 / 92 | 33 / 33 |
| Ubuntu 26.04 LTS x64，WSL2 / WSLg X11 | 同一份 Windows 编译的功能 IL，明确 `BuildExecuted=false` | 11 / 11 | 22 / 22 | 92 / 92 | 33 / 33 |
| 同一 Ubuntu，独立 Linux SDK 源码构建 | 独立源码副本、锁定还原及 Release 构建，`BuildExecuted=true` | 11 / 11 | 22 / 22 | 92 / 92 | 33 / 33 |

所有列出的最终回归均零失败、零跳过、零错误；窗口 22 项包含后端的 11 项，不能相加当作独立覆盖数量。同一 IL 运行用于隔离平台行为，独立 Linux 构建用于证明 Linux 还原 / 编译链路，两者分别保留源码、产物及执行证据。Linux 独立构建的绝对源码路径不同，程序集 SHA 不要求与 Windows 相同；每份报告、测试输出和实际执行的功能程序集必须在本组内一致。

功能包独立消费者 **48 / 48**，默认 Themes 消费者 **51 / 51**，共 **99** 项。两个消费者使用不同的初始空包缓存；依赖图没有源码 ProjectReference，功能包模式没有 Themes 依赖。真实恢复的 nupkg、包内 DLL、缓存 DLL 和运行输出 DLL 均绑定。Usage 与统一 Demo 的 Release 编译均为零警告 / 零错误。

测试覆盖了加载 Loading 事件中释放、PageCount 通知中关闭、Empty 状态通知中重新打开、旧错误回调中启动新加载、异步选择器迟到、非协作工厂、祖先隐藏、真实后台队列繁忙时的关闭与释放。旧错误事件必须关联失败时的数据源，不能读取重入后的新来源；旧任务收尾也不能清除新文档状态。

为使统一 Demo 编译通过，修复了 Charts 冒烟辅助代码一处坐标距离表达式：显式构造 `Vector(deltaX, deltaY).Length`，保持原比较含义。此项单独登记，不作为 Charts 功能修改或 Charts 回归通过证据。

## 原生与视觉证据

真实出版输入沿用 P0 的 GNU make 4.4.1 手册，SHA256 为 `A4BC06026984382815E392159E60A4BC356CAC6C5B81E5AE84FA4F58F621F4D3`，229 页。验证首 / 中页真实渲染、页面数量和跨页搜索 **2,105** 个结果；没有声称逐页视觉验收或性能达标。来源及 GFDL 条件随运行保留，手册不进入产品包。

原生窗口使用自有两页夹具，验证实际 HWND / XID、控件加载、搜索、导航、布局、页面栅格缓存和 `CompositionBatch.Rendered`。系统字体匹配器分别确认“中 / 文 / 页”可找到字体；它只覆盖三字前置条件。窗口保存的 PNG 来自视觉树 `RenderTargetBitmap`，人工已查看最终 Windows 和 Linux 全图：中文工具栏与状态可读，页码 / 125% / 旋转内容 / 蓝色矩形 / 五处搜索结果与高亮正常。两平台主题按本机配置分别为暗色和亮色。

PNG 与原生窗口报告分别绑定。它们不代表桌面合成器 scan-out、物理显示、物理键鼠、剪贴板桌面集成、IME、多屏 DPI 或 FPS 的验收；Headless 也不替代原生窗口。

Ubuntu 环境为内核 `6.6.114.1-microsoft-standard-WSL2`、glibc 2.43、X11 `DISPLAY=:0`，不是目标 Ubuntu 24.04 的 hosted CI 或实体设备。本轮通过官方 SHA512 校验取得任务专属 .NET runtime / SDK，并按 apt 元数据哈希取得 ICU、ICE / SM、Noto CJK，解包到任务目录；仅设置该进程的库路径、fontconfig 和缓存，没有修改发行版或系统字体设置，没有启用 invariant globalization 绕过 ICU。

PDFium、SkiaSharp、HarfBuzzSharp 三份实际加载 ELF 与审计路径 / 哈希一致，`ldd` 无缺失；最高 GLIBC 导入版本分别为 2.16、2.27、2.14，没有 GLIBCXX / CXXABI 版本导入。此结论仅描述固定 x64 资产及当前环境，不能推导整个产品的最低 Linux 版本，也不能推广到 ARM64。

## 独立检查、失败证据和可复验性

固定 PDFium 头文件记载 owner 权限为 `FFFFFFFF`，但 Windows x64 实际 restricted-owner 返回 `FFFFFFFC`。独立 Python ctypes 调用与 pypdf 解密角色核对四组 R2 输入后，生产模型比较 effective / user 的有效权限位：受限 user 为 `User`，权限提升的 owner 为 `Owner`，全许可 user / owner 均为 `UserOrOwner`。模型保留 API 无法区分的身份，不把全许可 user 误标为 owner。原始 PDF、探针和结果随证据保存；生产包不依赖 Python。

Linux runner 的两个负对照均被拒绝：手册缓存哈希错误在 native / build / tests 前退出；复用已有输出目录退出且原报告哈希不变。工作流静态检查通过，不能代替远端执行。预跑的 ICU 缺失、ICE 依赖缺失、中文缺字图、Windows 沙箱 `dotnet --info` 读取受限、网络失败及 Demo 初次编译阻断均保留，修复后使用新的输出目录执行。补入隐藏许可文件及测试共享 props 的冻结修正后也重新运行，旧候选记录没有覆盖或删除。

最终双包消费者通过经过哈希核对的本地离线 feed 恢复：43 个既有依赖归档及两份本轮新产品包。归档 SHA512 与各自归档 sidecar 比较，NuGet contentHash 与 assets 元数据单独比较；还核对 ZIP 内 DLL / SO / DYLIB 与缓存载荷。两种哈希语义没有混用。

实时漏洞源访问出现 NU1900 / NU1301；显式 `--vulnerable --include-transitive --no-restore` 查询退出零且未列漏洞，但没有证据证明本轮实时 advisory 刷新成功。最终本地锁定还原 / 包验证使用仅作用于任务进程的 `NuGetAudit=false`，未改项目或 CI 的审计设置。因此本轮不宣布新的依赖漏洞审计通过；CI 保留默认审计，并将成功刷新列为待补记录。

## CI 与剩余门槛

[pdfviewer-native.yml](../.github/workflows/pdfviewer-native.yml) 已接入 push / pull request 的 Windows 与 `ubuntu-24.04` x64，执行锁定还原、Release 构建、Core / Headless、原生后端 / X11 窗口和双包新缓存消费者，失败也保留日志、XML、程序集 / native 身份与 PNG。Ubuntu 使用 Xvfb 及显式系统依赖，没有发布或远程手动执行步骤。本次未触发远端作业，`configuredOnly=true`。

**G1 保持开放。** 公共契约和调度 / 释放底座已有双平台执行证据；仍需取得 Ubuntu 24.04 CI 的实际 run 与默认 audit 成功结果，落实精确部署镜像及设备依赖。麒麟 x64 / ARM64、目标实体桌面及系统输入 / 服务验收继续按平台计划推进，不把 WSLg 或 ELF 资产存在计为通过。P3 的大文件 / 有界队列 / 任务合并 / 延迟分位数 / UI 峰值及官方同机性能对照仍未完成。

复现入口：

```powershell
./scripts/Test-PdfViewerNative.ps1 -OutputDirectory artifacts/pdfviewer-native/windows-fresh
./scripts/Test-PdfViewerPackageConsumer.ps1 -PackageFeed artifacts/pdfviewer-native/packages-fresh
```

```bash
bash scripts/Verify-PdfViewer-Linux.sh --output artifacts/pdfviewer-native/ubuntu-fresh \
  --corpus-cache artifacts/pdfviewer/real-corpus-cache
```

输出目录须为新的目录，保留失败证据。机器记录从相应运行目录导出；未来执行应先完成新的源码冻结，不能把修改后的源码套用旧结果。P0 字体 / 表单 / 部分内容删除的大矩阵及完整真实语料操作矩阵未在本轮新二进制上全部复跑，仍需在对应生产阶段与 P9 重新验收。
