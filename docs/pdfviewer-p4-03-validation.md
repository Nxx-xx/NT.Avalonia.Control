# P4-03 原路径冲突、保存故障与进程崩溃恢复（2026-10-25）

本批交付完整内容指纹的原路径冲突检测、可恢复的文件保存事务，以及 Windows / Linux 的故障注入与受控进程终止验证。原文件发生外部改写、替换或删除时，控件拒绝覆盖并保留 Dirty；AutoSave 暂停，另存到新路径或重新加载后才解除冲突。G4 保持开放。

最终机器记录：[pdfviewer-p4-03-20261025.json](validation/pdfviewer-p4-03-20261025.json)。证据入口：`artifacts/pdfviewer/p4-03-20261025/release/final/`；父目录保留开发期尝试，不计为最终版本通过。

## 内容身份与控件行为

- `PdfFileSnapshot` 记录存在性、长度和完整 SHA-256，不依赖 mtime。可写会话初始化时从实际已打开的输入句柄取得原始字节并冻结基线，成功文件保存后更新为输出身份；切换 `IsReadOnly=false` 会排队初始化，宿主可等待 `EnableEditingAsync()`。默认只读加载仍保持随机访问，不做整文件指纹扫描；在启用编辑前的只读期间不承诺同长度原地改写检测。
- 原路径保存、AutoSave 和“另存为到同一原路径”复用该基线。真正的新目标在该次保存开始时捕获目标身份。提交前再次读取完整指纹；同长度、恢复原 mtime 的改写仍会拒绝。只改时间等元数据而内容不变时不视为内容冲突。
- `PdfSaveConflictException` 携带目标、预期和实际身份，映射 `PdfErrorKind.SaveConflict`；控件提供 `LastSaveConflict` direct property、`SaveConflict` 事件及本地化错误文字。失败不清 Dirty / 历史，不改变 SavePath，不因关闭时选择保存而丢失当前文档；AutoSave 在冲突后不会因下一笔编辑反复尝试覆盖。
- 冲突解决采用明确的新路径另存或重新加载，当前没有“强制覆盖外部版本”后门。成功文件保存清冲突；流导出 / 保存不将原路径重新认定为安全。
- 独立 `PdfEditSession.SaveAsync(path, PdfFileSaveOptions)` 可传 `ExpectedTarget`。未提供时，只检测该次保存开始后目标的变化；字节编辑会话本身不知道原始文件路径，不能自动推断更早的基线。返回 `PdfFileSaveResult`；控件 `DocumentSaved.FileResult` 可观察恢复目录和清理警告。

## 保存事务与恢复协议

1. 取得目标路径的跨进程协作锁，检查预期内容。空 `.nt-pdf-save-<hash>.lock` 文件不存文档数据，刻意保留以避免 Unix unlink 后出现两个锁 inode；不算未清理的暂存 PDF。
2. 在目标同目录创建唯一 `.nt-pdf-txn-<guid>`。Windows 使用仅当前用户可继承的保护 ACL；Unix 要求目录实际为 0700，候选及 journal 创建为 0600。无法执行私有权限的文件系统明确拒绝。新 Unix 输出保持私有；替换已有目标时保留其 Unix mode。
3. 写入并 `Flush(true)` 持久化 `transaction.json`，记录 schema、受限目标文件名、原内容身份、候选身份和页数。候选分块写入、Flush、重新读回比对 SHA，再由 PDFium 检查页数和未加密 / 未签名写入边界。
4. 再次比较目标，最后一次检查取消 / 会话存活后执行同文件系统原子替换。提交前异常保留原文件与 Dirty，清理本次拥有的暂存；替换 I/O 的结果不确定时保留 journal 供核对。提交后清理异常不假装撤销已完成的保存，返回成功及清理警告。
5. 正常结束只移除已识别、无链接且无未知成员的本次事务文件；不会递归清理任意目录。稳定协作锁保留。临时目录未完成 / 异常退出时由显式恢复流程处理。

宿主在重启后显式调用 `PdfSaveRecovery.ScanAsync(directory)`，不会扫描整盘或自动覆盖文档。扫描按条目给出状态；超过 4096 个目录明确报错，不静默丢弃后续项。

| 状态 | 含义与允许动作 |
| --- | --- |
| Active | 所属进程仍持有 owner lock，不能恢复 / 清理 |
| Incomplete | 候选缺失或与 journal 的长度 / SHA 不符；不允许提交，宿主可选择丢弃已识别事务 |
| Prepared | 候选身份和 PDF 语义验证通过、原目标仍匹配；用户选择后 `RecoverAsync` 再校验并提交 |
| AlreadyCommitted | 目标已是记录的输出；重新验证实际目标 PDF，随后清理，不再次覆盖 |
| Conflict | 外部目标已变化；拒绝恢复覆盖，保留候选及 journal |
| Invalid | journal、结构、候选 PDF、路径 / 链接或未知文件不符合协议；不自动重放 / 删除 |

`RecoverAsync(transactionDirectory)` 不信任扫描结果缓存，重新获取 owner / 目标锁，重新读取 journal、指纹和 PDF。`DiscardAsync` 只删除 inactive、有有效 journal、无未知文件 / 链接的事务；损坏 journal 需宿主人工查看后处理，产品不猜测所有权。恢复对象是**已经进入保存流程的候选快照**，不包含从未触发保存的编辑或内存 Undo 栈。

该实现是内容指纹和协作锁组成的乐观冲突检测，不是任意外部程序都参与的文件系统 compare-and-swap。非协作 POSIX 写入仍可能发生在最后检查与 rename 之间；路径别名 / 硬链接、多用户协作、所有权 / 扩展 ACL / xattrs / SELinux 标签未完成完整兼容验证。符号链接 / Windows reparse point 保存及恢复明确拒绝。本批不能据此宣称任意并发写入、网络文件系统或掉电场景已安全覆盖。

## 最终执行

最终功能 DLL SHA-256：`26F63ED45F18589E39D8FC9B4DE488421E6B6FFD83DB4026378EF9E70E01312E`。Windows 与 Ubuntu 使用同一 Windows 编译 IL，原生 PDFium / Skia 使用各自 RID；本批没有 Linux 源码独立编译。

| 验证 | Windows x64 | Ubuntu 26.04 WSL2 x64 |
| --- | --- | --- |
| Core | **189/189** | **189/189** |
| Headless | **138/138** | **138/138** |
| 原生后端 / 窗口 | **11/11、40/40** | **11/11、40/40**（Xvfb） |
| 故障 / 崩溃 supervisor | **68/68**，80 个子进程 | **72/72**，85 个子进程（普通 UID 1000） |

正式新增 **16** 项：Core 13、Headless 3。包括同长度同时间戳改写 / 替换 / 删除、迟到改写、写 / Flush / 挂载故障、候选损坏、取消、提交后清理、活跃事务 / 二次写入、伪造目录与未知文件拒绝、私有暂存权限、Unix mode 保留、原路径 / 同路径另存冲突、关闭保存失败保留文档、AutoSave 停止重复冲突，以及上次成功文件输出的基线更新。已有 P4-02 和六处问题修复随全套复跑。

故障矩阵实际包含写入后的 ENOSPC 错误码注入、Flush 错误、替换前挂载不可用错误、取消、已落盘候选损坏，以及外部改写 / 替换 / 删除。真实文件系统检查包括 Windows / Linux 的 .NET 文件共享锁、Windows 只读目标拒绝、Linux 只读父目录拒绝；Linux 目标文件仅只读但父目录可写时允许 rename 的差异作为预期验证，而非强行照搬 Windows 行为。

受控崩溃在 journal Flush 后、第一块写入后、候选 Flush 前、候选 Flush 后、PDF 验证后、替换前及替换后共 **7 个阶段**，由 supervisor 只终止自己启动的子进程，再启动新进程扫描 / 恢复 / 丢弃。大输入保证第一块写入阶段候选确实不完整；Flush 前允许根据实际完整性判定，不能预设一定可恢复。两平台共终止 **29** 个受控子进程（Windows 14、Linux 15，含恢复篡改和协作锁场景），没有终止其他工作进程。

恢复负向矩阵另外覆盖：恢复前改写目标、损坏候选、路径穿越、未知文件、伪造正确哈希的无效 PDF、伪造“已提交”的无效 PDF，以及 Linux 符号链接。第二个真实进程不能穿过同一目标协作锁；首进程被终止后，新进程仍能恢复第一份完整候选。

Linux 的主轮使用本地 `/tmp` tmpfs，确保权限检查和真实拒写生效。另验 WSL `/mnt/d` v9fs：该挂载不能执行请求的私有 Unix 目录权限，生产入口返回 `NotSupportedException`、Dirty 保留、目标 SHA 不变且无残留事务。该边界有单独记录，不能把 WSL Windows 挂载当成普通 Linux 本地文件系统。

补充结果：双包全新缓存消费者 **125/125 + 128/128 = 253/253**，各新增 5 项公共文件指纹、控件冲突、保留外部文件、另存解除及恢复扫描。实际 nupkg，无源码项目引用；功能包不依赖 Themes。功能包 / Themes / Usage / 验证工程构建零警告、零错误；统一 Demo 构建成功，带工作区 `Charts.Skia` 的 1 条 AVA3001 私有 API 提示，未改动该项目。Demo 原生阅读 smoke **68/68** 通过。

包验证仅任务命令使用 43 个已校验缓存依赖组成的离线源和 `NuGetAudit=false`，没有修改仓库 / CI 审计策略，未新增漏洞审计结论。专用 Windows / Ubuntu 24.04 CI 已接入锁定还原及 fault supervisor，远端 run 尚未观察到。

## 独立验证与可视检查

pypdf 严格重开 **60** 份实际目标 / 恢复输出，验证页数、批注 Contents、实际 AP 字符操作数和 ToUnicode、嵌入字体及私有书签；**6** 份损坏 / 无效 PDF 由独立解析器拒绝。Poppler 对全部 60 页渲染成功；4 组 Windows / Linux 代表恢复输出的像素一致。人工查看两平台恢复页面和 Windows 原生窗口截图，B 字形、原页面文字和工具栏可见。原生图是 RenderTargetBitmap，不是桌面 scan-out。Poppler 的系统显示字体告警保存在独立日志，未用其代替实际 AP / 嵌入字体检查。

机器记录冻结源码、实际执行程序集、原生模块、包档案和全部关键证据。进程退出代码、supervisor 的 owned PID 和 kill 动作、各阶段 scan / recover JSON、原 / 外部 / 目标 SHA、独立解析和渲染结果分别可追溯。内部注入 hook 仅 friend 测试工程能访问，PDF 或环境变量不能开启。

## 开发期发现及剩余门槛

开发期记录保留，包含：InvalidDataException 未被扫描器分类而中止扫描（已修复为逐项 Invalid）、更早文件指纹 I/O 失败没有触发控件 SaveFailed（已将完整保存准备移入失败报告范围）、Flush 前候选仍可能留在缓冲区（按真实完整性分类）、旧单测错误把持久空锁当作临时文件（已增加无文档数据 / 无残留事务验证）。早期一次 Windows ReplaceFile 返回 I/O 失败，原文件保留；同逻辑后续完整矩阵通过，未以直接覆盖或复制删除替换降级绕过。

本批没有真实填满磁盘、切断 SMB / NFS、模拟断电 / 内核崩溃或执行设备掉电。`Flush(true)` 和原子 rename 不等价于任意存储设备的掉电耐久；本批未加入并验收完整目录 fsync / 断电协议。P4-03 对这些项、非协作写入最终窗口和完整元数据仍保留开放验收；P4-02 大文件保存峰值 / 时延、P4-04 安全策略、P4-05 完整外部兼容矩阵继续按原计划推进。G4、Ubuntu 24.04 hosted run、麒麟 / ARM64 实机、物理输入与官方性能对照均未关闭。

实现依据采用 [.NET File.Replace](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.replace?view=net-10.0)、[Unix 替换异常契约](https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/6.0/file-replace-exceptions-on-unix)及 [Linux rename 原子性](https://www.man7.org/linux/man-pages/man2/rename.2.html)。文档中的原子性是名称替换语义；本轮可恢复性结论来自上述受控进程退出实测，不延伸为物理存储耐久保证。
