# P4-03 真实存储、发布竞争与耐久验证（2026-10-25）

本轮推进保存生产协议和真实故障验证。最终证据入口为 `artifacts/pdfviewer/p4-03-storage-20261025/final/`，Windows 原生窗口证据为同父目录的 `windows-native/`；[机器记录](validation/pdfviewer-p4-03-storage-20261025.json)冻结执行程序集、源码、包、断连 / 终止动作、恢复及独立解析结果。[首轮记录](pdfviewer-p4-03-validation.md)保持历史内容，不用本轮结论覆盖旧证据。G4 保持开放。

## 生产实现

- 事务 schema 升为 v2，兼容读取 v1。`output.pdf` 是独立文件快照，`candidate.pdf` 是另一个 inode 的发布候选；journal 记录原目标和输出 SHA。Windows 使用 ReplaceFile backup，Linux 使用 `renameat2(RENAME_EXCHANGE)`，将实际被置换文件保留为 `displaced.pdf`。Linux 交换后改名尚未完成时，旧文件仍留在 candidate 位置，恢复器能识别。
- 提交前最终比对后仍可能有外部进程写入。现在对实际被置换版本及发布目标再次核对，发现竞争则抛 `PdfSaveConflictException`，`WasPublished=true`，给出恢复目录、输出快照、被置换版本路径；Dirty 和历史保留，AutoSave 暂停。目标可能已经发布，不能声称“完全没有改过目标”。不盲目回滚，以免覆盖第三个写入者。
- 新路径采用 Windows no-overwrite move / Linux link no-replace：最终窗口出现同名新文件时拒绝覆盖。已有目标的 Linux 文件系统不支持原子交换时明确拒绝，不降级为可能丢失外部版本的覆盖。NFS 的新路径与已有目标属于不同支持范围。
- Linux 对 journal、独立输出、候选、事务目录、目标父目录、发布目标和 commit 确认记录执行相应 fsync。`CommitFlushed` 位于确认记录及目录刷新后；只有完成确认才清 Dirty。Windows 记录目标和 commit Flush，不声称已经执行 POSIX 目录 fsync；ReplaceFile 的 WRITE_THROUGH 标志并不受支持。
- 发布或刷新结果不确定时抛 `PdfSaveDurabilityException`，映射 `SaveDurabilityUncertain`，控件公开 `LastSaveDurabilityFailure` 并暂停后续编辑触发的 AutoSave。成功文件另存 / 重载解除。清理失败发生在确认以后，仍返回保存成功及清理警告。
- `DurabilityPending` 表示目标已是完整输出但尚无有效确认记录；重新校验旧版本及目标后可补写确认，包含满盘造成的空 / 部分 commit JSON。`PublishedConflict` 禁止自动恢复和丢弃；`ReadVersionAsync` 可读取显式选择的保留版本（单份 ≤256 MiB），更大文件检查保留路径。枚举追加，原数值不改。未知成员、链接及伪造路径仍拒绝。

这仍是乐观并发协议，不是文件系统 CAS。不能阻止调用返回后的外部写入，也不承诺防御任意恶意进程、预先持有 inode 的后续改写、路径别名 / 硬链接竞争。事务可保留多份完整 PDF，保存磁盘需求相应增加；大文件时间与峰值归 P4-02 专项，不用本轮小样本关闭。

## 真实环境与故障

| 场景 | 环境及实际操作 | 验收范围 |
| --- | --- | --- |
| Windows 满盘 | 新建并核对磁盘身份的 128 MiB VHDX / NTFS，填充仅该测试卷；额外写入得到真实 HRESULT 112 | 保存开始前、journal 后、发布准备后 3 点；目标始终为完整原件或完整输出，失败不清 Dirty；释放填充后新进程核对 / 恢复 |
| Linux 满盘 | 私有 mount namespace 中的 64 MiB loop ext4、普通 UID 1000 保存；内核写入及额外 witness 均返回 ENOSPC 28 | 同样 3 点；包括替换已完成但确认 JSON 因满盘不完整的修复 |
| Windows 断连 | 本用户专用临时 SMB share，分别在 BeforeFlush / CommitPrepared / Replaced 移除该 share，观察失败后重新创建 | 真实 SMB tree 不可用，3 点失败均保持 Dirty；重连后完整目标及恢复验证。没有禁用宿主网卡 / 全局 SMB 服务；不是远端服务器掉电或跨网链路认证验收 |
| Linux 断连 | 私有 network / mount namespaces 中 Ganesha 6.5 NFSv4.1 TCP，loop ext4 export；仅关闭私有 lo 3 秒后恢复 | 新路径的 BeforeFlush / CommitPrepared 两点中断期间无提前成功，重连后完整输出、Dirty 与结果一致；已有目标交换不支持，明确拒绝并保持原件 |
| Linux 虚拟机断电 | QEMU 10.2.1 + KVM、独立 WSL 6.6.114.1 内核 / RAM / virtio ext4；writeback、flush 开启、无 snapshot / unsafe cache | FileFlushed / Replaced / CommitFlushed 三点直接终止自己启动的 QEMU，丢弃客体 RAM；共 6 次启动，新客体挂载重放并恢复完整输出。保留重放前磁盘压缩镜像、SHA、串口日志和恢复结果 |
| Windows 虚拟机断电 | 无可丢弃的 Windows 镜像，用户明确允许跳过 | **延期，未执行，不计通过** |

Linux NFSv4 永久失联时，即使 soft mount 也可能在状态恢复或同步 I/O 中等待；开发期永久断连尝试超过驱动 60 秒等待期，恢复链路后终止所拥有的探针。最终通过的是三秒短暂中断 / 重连，不能改写成永久断网取消通过。后续需独立保存进程、超时和用户恢复策略，不能承诺 CancellationToken 立即打断内核等待。网络 fsync 返回成功也不等于验证服务器物理介质耐久。

掉电模型是客体虚拟机失去 RAM / 缓存并重新启动，区别于只杀 .NET 保存进程；仍未执行真实电源拔除、控制器易失缓存丢失、磁盘故障固件或 Windows VM 断电。每点 1 次专项验证，不是长期统计耐久认证。

## 回归和独立验证

最终统一功能 DLL 为 `D3A92734CCA1A46EE63F96602523263183FC5E188CD35D3AD8F95FB46E45300E`。Windows / Ubuntu 26.04 WSL x64 使用同一 Windows 编译 IL，分别加载各自 RID 的原生库；Linux 客体也使用该 DLL，不冒充 Linux 源码独立编译或 Ubuntu 24.04 hosted run。

| 验证 | Windows | Linux |
| --- | --- | --- |
| Core / Headless | 195 / 140 | 195 / 140 |
| 确定性故障及进程退出 supervisor | 76 检查、90 子进程 | 80 检查、95 子进程 |
| 非协作真实进程最终窗口 | 24 检查、独立 Python 写入进程 | 24 检查、独立 Python 写入进程 |
| 实际存储专项 | 18 检查（NTFS + SMB） | ext4 12、NFS 10、VM 6 |
| 原生 PDFium / 窗口 | 11 / 40 | 11 / 40（Xvfb） |

新增正式回归 8 项（Core 6、Headless 2）：最终窗口改写 / 替换 / 新建目标、发布后外部变化、无确认及部分确认恢复、控件冲突 / 耐久不确定状态下 AutoSave 停止重复提交。真实竞争使用独立进程，不取得库的协作锁；内部 hook 只提供精确调度屏障，没有替换真实文件系统结果。发布冲突禁止自动恢复 / 丢弃，并验证独立输出、实际旧版本及当前目标。

独立脚本使用 pypdf strict 重开 **318 份**实际目标和保留版本，按独立期望 SHA 识别原件 / 外部件 / 保存输出，验证页面、批注实际 AP 操作数、ToUnicode、嵌入字体及私有书签；Poppler 渲染 **86 种不同字节内容**，代表输出跨平台像素一致。另有 **6 份**损坏候选 / PDF 由独立解析器拒绝。文件及截图路径见 `final/independent.json`，副本解析数不充当不同语料数。实际查看 Linux 客体恢复页，AAAA 和 B 字形完整；另查看 Linux 原生窗口渲染。双包消费者实际 nupkg / 全新依赖缓存 **129 + 132 = 261 项**通过，新增公共耐久和恢复契约检查；包档案与退出代码见机器记录。

功能、Themes、验证工程构建零错误；统一 Demo 构建成功，保留工作区 Charts.Skia 已有 AVA3001 提示，没有为本轮修改该项目。专用 Windows / Ubuntu CI 增加独立进程竞争，Ubuntu 接入有界 ext4 真实 ENOSPC；远端 run 尚未观察，VM / NFS 实验仍为专用本机脚本。

## 验证基础设施与剩余工作

开发期镜像重复保留导致工作区 D 盘耗尽，部分证据写入失败；这批尝试保留失败日志，不计最终通过。清理仅本轮已卸载的重复镜像，新增 512 MiB 空间前置门槛；最终镜像卸载后无损压缩并回读比对原 SHA，再删除未压缩副本。最终记录只绑定 `final/` 的重跑结果。Windows 测试卷、临时 share 及 Linux 私有 mounts / VMs 均由所拥有的句柄或 PID 清理，不处理其他卷、会话或进程。

仍开放：永久失联网络保存的进程隔离 / 超时，完整所有权 / 扩展 ACL / xattrs / SELinux 策略，真实服务端 / 物理设备掉电，Windows VM 延期项，长时间压力与目标 Ubuntu 24.04 / 麒麟 / ARM64 实机。P4-04 加密 / 签名策略、P4-05 外部兼容及 P4-02 大文件保存性能继续按计划推进。

依据：[Linux rename / RENAME_EXCHANGE](https://man7.org/linux/man-pages/man2/rename.2.html)、[Windows ReplaceFile](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilew)、[QEMU cache 与 flush](https://www.qemu.org/docs/master/system/qemu-manpage.html)、[Ganesha VFS](https://github.com/nfs-ganesha/nfs-ganesha/wiki/VFS)。上述资料限定机制语义，实际通过范围取自本轮测试。
