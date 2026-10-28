# C7-02 桌面平台验收

更新日期：2026-10-24。本批按冻结候选 v6 复验；Linux 的显式宿主隔离条件和未取得的设备资格分开记录。C7-01 的冻结软件结果继续保留在[范围与联动报告](charts-c7-range-linking.md)。

后续普通消费者字体启动修正、Demo/Usage 正常入口与独立 nupkg 验证见[字体启动报告](charts-c7-consumer-font-startup.md)。该后续批次不覆盖以下 v6 历史记录，不改变 Linux GPU 失败或目标实机未验状态；C7-02 整项仍未完成。

本批建立可复跑的独立桌面宿主与运行器，直接观察原生窗口后端、当前绘制上下文、屏幕缩放、字体和输入事件，再按实际可用设备取得证据。它不承担 C7-03 的官方性能对照或 C7-04 的八小时长稳，也不自动关闭 C7-01 的高级图例、密集读数及标签布局差距。

## 验收口径

| 维度 | 本批可执行范围 | 不能据此取得的资格 |
| --- | --- | --- |
| 窗口后端 | 原生 Win32 / X11 窗口类型、句柄描述、屏幕和实际缩放 | 请求参数不等于实际后端；XWayland 不等于原生 Wayland |
| 渲染器 | 默认、强制软件与显式 GPU API 请求，窗口真实绘制时观察 Skia/GL 上下文和设备字符串 | 加载 Skia DLL 或存在 GRContext 不单独证明物理 GPU；Mesa llvmpipe/WARP 与硬件路径分开记录 |
| DPI | 当前屏幕实际 RenderScaling；Linux 进程级缩放；96/120/144/192 导出 DPI | 导出 DPI 不证明桌面 DPI；进程级缩放不等于异 DPI 物理跨屏 |
| 布局 | 原生窗口 resize、小窗口、主题、长标签、原始点命中及永久内容导出 | 一张代表性图片不证明所有专业图族无标签碰撞 |
| 文字 | en-US / zh-CN / ar-SA、CJK/组合字符/emoji/RTL、实际字体匹配和可取得的 glyph 信息 | 非缺失 glyph 不证明全部语言整形、双向排版、IME 或读屏顺序正确 |
| 输入 | 控件路由注入与系统输入分开；本次拥有窗口上的鼠标、键盘、滚轮、拖动、焦点和重置 | 系统注入不等于真人鼠标、精密触控板、触屏捏合或手掌拒绝 |
| 图像 | 独立 PNG 结构/像素检查；实际窗口人工查看；可取得时单独归档窗口捕获 | RenderTargetBitmap 不是 compositor 截图，窗口截图也不产生 present/FPS 性能数据 |
| 可追溯性 | 冻结源码、程序集哈希、运行命令、环境与原始状态、独立退出码、失败/超时 | CI 配置不等于远端执行，WSLg 结果不等于 Ubuntu 22.04/24.04 实机认证 |

## 当前设备与目标矩阵

初始 Windows 只读硬件记录位于本批原始证据 `windows-hardware-inventory-v1.json`：内核 26200.9711、注册表 DisplayVersion 25H2；注册表 ProductName 仍为旧命名，不能只凭该字符串确认 Windows 营销版本。CPU 为 i5-14400F，约 32 GiB 内存；列出 RTX 4060 与 Oray 虚拟显示设备，必须再核实窗口实际所在显示器及绘制设备。检测到 HID 鼠标，触屏或精密触控板未据此取得资格。

| 目标 | 初始条件 | 本批状态 |
| --- | --- | --- |
| 当前 Windows x64 桌面 | Win32、原生 DPI 96/RenderScaling 1；两个逻辑区域均 1920×1080、缩放 1 | 六个正式正交用例通过；默认/显式 ANGLE D3D11 观察到 RTX 4060，强制软件为 software-raster |
| 当前 Ubuntu 26.04 / WSL2 / WSLg | X11 / XWayland，两个逻辑区域；固定现有工具链 | 显式宿主字体隔离条件下 9/10 原生用例通过；GLX GPU 请求被 llvmpipe 黑名单拒绝，完整矩阵仍失败 |
| Ubuntu 22.04 / 24.04 x64 实机 | 用户表示另有拟测实机，随后表示尚未测试；具体版本、访问方式和桌面会话尚未确认 | 本批尚未执行实机，不登记通过；提供本地复跑源码包 |
| Windows 125%/150%/200% 与异 DPI 跨屏 | 未改变全局显示设置；以当前拓扑为准 | 没有实际配置的组合保持待验 |
| 物理触摸/精密触控板 | 未确认设备 | 待设备与真人操作 |
| 指定麒麟/UOS、ARM64、原生 Wayland | 未新增目标或升级依赖 | 保留独立分支，不默认支持 |

## 验证结果

默认用例是明确的正交组合，不宣称穷举所有渲染器、文化、主题、DPI 与图族的笛卡尔积。入口为 `scripts/Invoke-ChartsPlatformAcceptance.ps1` 和 Linux 包装器；每个用例单独进程，强制同轮锁定还原、正常 NuGet 审计和非增量构建，保留超时及失败原文。独立 Python 检查器核验图像结构、像素差异、后端/比例和原始数据身份，并用反例测试防止矛盾证据被接受。

最终候选 v6 包含 2,036 个输入文件，manifest SHA256 为 `0BBC3FC8FBC9A70A651429409C754F2E5F81294D585D5F4C800F443A16ED8181`。Windows 与 Linux 使用各自的冻结副本；本批仅修改 Charts 产品的 RTL 绘制策略，新增宿主、运行器、校验器和回归测试。原生字体绑定隔离仅属于验收宿主的显式选项，不注入 Charts、Demo 或 Usage。最终候选重新构建并验收，不直接沿用旧二进制通过。既有其他控件的并行变更单列，不计入本批验证资格。

| 检查 | Windows v6 | Linux WSLg v6（宿主显式隔离） |
| --- | --- | --- |
| .NET SDK / Avalonia | 10.0.402 / 12.0.3 | 10.0.402 / 12.0.3，runtime 均 10.0.13 |
| Headless | 3,171 / 3,171，含新增八项方向回归 | 3,171 / 3,171；按同平台历史身份核对，原 3,163 项全部保留 |
| 独立校验器自测 | 106 / 106，无跳过 | 106 / 106，无跳过 |
| 原生矩阵 | 6 / 6 用例；48 个宿主 + 762 个场景检查全部通过 | 9 / 10 条件通过；72 个宿主 + 1,143 个场景检查通过；显式 GPU 启动失败，无场景覆盖，完整门禁仍失败 |
| 独立用例检查 | 每项按实际图片、身份、后端、比例和进程重新计算，详见验证 JSON | 同左；隔离开关、解析器真实调用及包内原生库路径/哈希另作强绑定 |
| 图像产物 | 396 PNG + 24 JPEG，共 420；12 个模板字形证明、18 个 F 方向证明 | 594 PNG + 36 JPEG，共 630；18 个模板字形证明、27 个 F 方向证明；失败 GPU 没有图像资格 |
| 完整 Demo / Usage 冒烟 | 5,186 / 5,186；931 / 931 | 5,186 / 5,186；931 / 931 |
| 系统输入 | 12 个实际外部动作；8 个宿主 + 135 个场景检查通过，正常 Finish 退出 0，独立关联进程/句柄/事件/图片 | 未执行系统输入，不以自动注入替代 |

Linux 本轮原生宿主的所有用例显式启用 `-LinuxNativeTextIsolation`，属于条件软件证据。Demo 和 Usage 没有注入该启动逻辑，使用本轮已建立的共享字体缓存完成既有冒烟；这不证明普通消费者的全新缓存冷启动已经修复。完整 Linux 矩阵因 GPU 失败，最终 `batchPassed`、`selectedAutomaticSoftwareMatrixPassed` 和 C7-02 整项验收均不能登记为通过。

Windows 的默认及显式 GPU 请求实际取得 ANGLE NVIDIA RTX 4060 D3D11 字符串；强制软件请求没有 GRContext。Win32 原生客户端尺寸、96 DPI 和 per-monitor-aware-v2 与框架信息交叉一致。六次逻辑屏幕迁移均为程序设置位置，两个区域都是缩放 1，其中存在虚拟显示驱动，不能认证物理异 DPI 跨屏。

Windows 原生交互另使用 computer-use 的系统输入接口操作本批返回的窗口，记录点击原始 B、Home/End 浏览 A/C、图表焦点下 Space 选择 C、滚轮缩放、图内拖动平移、范围条用户变更、重置以及拥有的辅助窗口 Escape 焦点返回。窗口最大化通过原生标题栏的辅助接口执行，实际客户区从 1280×800 变为 1920×1009；角落拖动没有触发原生 resize，不能写成边框拖拽通过。平移空白处按下会清除选择，原始点身份历史仍保留。系统注入和辅助接口动作均与物理设备操作分开。

历史交互首轮在 Finish 前超过 30 分钟被监督器终止，没有生成最终 raw，保留为 `system-input-v1` 失败；相同 v2 冻结程序集的 `system-input-v2` 正常 Finish 并退出 0。最终 `system-input-v6` 使用 v6 程序集另行完成 12 个外部动作并正常退出。14 份窗口观察中的最大化和辅助开窗两帧过渡动画排除，12 份用于人工查看；原始 JPEG 均保留。首轮受遮挡/裁切的帧同样保留但排除视觉资格，不推断未单独报告的捕获后端，也不据此声称 compositor present/FPS。

Windows 的实际图片人工复核覆盖 RTL/高对比/小窗口及模板隐藏恢复。640×360 代表场景的自定义不透明 PlotAreaContent 标题会覆盖 A 的标签区域，A 点和命中仍在；此项继续归高级叠加布局/标签避让限制，不能声明所有标签都无遮挡。

最终人工复核直接查看 Windows 14 张、Linux 20 张本轮新图，并将每张 SHA/尺寸与原始 artifact 关联；Linux 记录完整宿主字体隔离条件。汇总证据见[独立验证 JSON](charts-c7-platform-acceptance-validation.json)。该 JSON 的批次失败与里程碑未完成状态保留，不能从通过的代表图或软件子集推导整项通过。

## 复跑与证据位置

本批本地归档根目录为 `C:/Users/a4472/.codex/visualizations/2026/10/15/01a13d4a-978f-77e1-8173-9dc5ed8f6a20/charts-c7-platform-acceptance-20261024/`。正式截图在各平台 `run-v6/native/cases/<case>/`；原始状态和导出图片同目录，`raw.json`、进程 stdout/stderr、独立 oracle 与 source/assembly manifest 一并保留。人工查看记录位于各平台 `run-v6/manual-visual-review.json`。最终系统输入在 `windows/system-input-v6/sky/` 和相邻 `host/`；v1 超时、v2 通过、v3/v4 原生失败、全部开发对照和 posthoc 校验分别保留，v5 为仅冻结未运行的中间候选。

可在仓库根使用以下入口，输出目录须全新；默认先正常还原依赖再非增量构建：

```powershell
pwsh -NoProfile -File scripts/Invoke-ChartsPlatformAcceptance.ps1 `
  -NativeMode DesktopSynthetic -OutputDirectory artifacts/platform-windows-new-run
```

```bash
bash scripts/Invoke-ChartsPlatformAcceptance-Linux.sh --mode desktop \
  -PythonExecutable python3 -OutputDirectory artifacts/platform-linux-new-run
```

Ubuntu 实机准备包为同归档根的 `charts-c7-02-ubuntu-source-v6.zip`（864,497 bytes，SHA256 `D174034810F88D2CB04B2D862AB2BDB5B5A31595857A7E641BAAADE3EA8890B5`），旁有 `.zip.manifest.json`。包内 302 个冻结源码/资源输入和三份生成说明/校验文件，共 305 个成员；ZIP 校验及全新目录解压后的 304 项 payload 核验均通过。`README-UBUNTU.md` 分别提供未隔离基线和显式宿主隔离命令，`VERIFY-PACKAGE.py` 只读核对完整性。包不含依赖二进制，需要已有 SDK 10.0.402、PowerShell 7、Python、字体和原生库；它没有连接目标设备或上传动作，不代表离线依赖包、自包含发布或实机通过。

## 已发现的问题与修正

### C702-RTL-MIRROR-01：自绘文字随 RTL 几何反射

首个冻结候选 v1 在阿拉伯语/高对比亮色场景中显示标题、轴文字和默认数据标签水平反射，模板 TextBlock 则保持正常。ChartBase 的自绘内容原先进入 Avalonia 的默认 FlowDirection 镜像策略，将数据几何和字形一起反射。

修正使用框架提供的 BypassFlowDirectionPolicies 扩展点，让图表的坐标方向继续由各轴与投影的显式配置决定；保留 FlowDirection 的继承和绑定，子文字控件继续采用自身双向文字布局。新增八项回归覆盖三种根的继承/动态绑定、离中心原始点的窗口方向，以及显式轴反向。原生场景另以真实默认数据标签 F 与同窗 TextBlock F 对照，覆盖显式 RTL、继承 RTL 容器和运行时切换，不能仅凭设置了 RTL 属性就算通过。

八项回归与最终双平台实际 F 方向、模板字形及原始点坐标检查均已验证；Linux 结论带有本报告列明的宿主隔离条件。此修正不宣称完整阿拉伯语排版、整形、IME 或读屏顺序已经认证。

### C702-FIXTURE-CONTRAST-01：验收模板黑字落在黑色图元上

v1 原始记录还真实失败于 engineering-permanent-template-real-raster-difference。原因是样例直接将黑色前景的模板文字放到高对比亮色 Mosaic 的黑色首格上，文字不可见。验收模板现在提供独立且常驻的对比背景容器，正文前景继续绑定图表；像素证明只将子 TextBlock 的 Opacity 置零并恢复，保持背景、布局和其余图元不动，避免把背景变化误当文字证据。原始失败及人工记录都保留，不降低像素差异门槛。

### 证据工具修正

独立审查同时补强了实际 OS/架构与后端/进程/句柄的绑定、原生 Win32 客户区和 DPI 交叉核验、同轮源码还原与非增量构建来源，以及外部键盘事件当场的图表焦点和事件来源。旧二进制目录跳过构建的入口已移除。GPU 探针也已修正底层 graphics 租约有效期间访问 SkCanvas 的错误；这类开发失败保留为工具失败，不误记为 GPU 不支持。

自动审批拒绝了新增 GitHub Actions 自动上传截图和诊断文件的操作，理由是没有明确的该出站目的地授权。该 CI 改动未落地，本批使用本地证据归档，未执行远端 CI。

### C702-RUNNER-HIDDEN-01：Linux 显式隐藏输入读取

Linux v2 的 Headless 3,171 项和校验器自测已通过，但原生运行器在任何 restore/宿主进程之前失败：`Test-Path` 能看到显式 `.editorconfig`，随后的 `Get-Item` 未传 `-Force`，在 Linux 返回找不到项目。独立 oracle 同时正确拒绝了缺少程序集/用例的未完成证据。此轮没有取得 Linux 原生矩阵或后续 Demo/Usage 资格，保留 `linux/run-v2/` 原始失败；修订运行器后另冻结候选和运行批次，不覆盖或补写旧结果。

修正给源目录递归、显式输入读取和程序集枚举增加 `-Force`，排序保留大小写差异。新增四项回归实际运行提取后的 PowerShell 函数，验证隐藏根配置、隐藏源/目录、隐藏模块及大小写不同的路径名不会丢失。正式前的 Windows/Linux PlanOnly 均得到同一组 298 项路径/大小/哈希，含 `.editorconfig`，且严格保留为未执行原生进程的预演。开发自测 Windows 86 通过/1 项既有 symlink 权限跳过、Linux 87 项全部通过；正式监督轮次另记录其实际跳过数。

### C702-WM-RESTORE-01：WSLg 的窗口请求与实际框架坐标不同

Linux v3 检测到两个逻辑屏幕。迁移请求 `(1932,12)` 后实际位置为 `(1900,-20)`，直接将原 getter 值 `(-22,-22)` 写回并未恢复原位置。宿主改为先恢复尺寸，等待原生配置稳定，再使用已接受请求的实际偏移作初始校准，最多六轮根据真实反馈修正；精确的原位置、屏幕、原始点身份和视图恢复门槛保持不变。补记迁移当场的屏幕、客户端 origin/center、尺寸和缩放，以及原生 PositionChanged 事件/有界采样轨迹，独立校验器核对这些几何关系，不再用较早的窗口位置猜迁移起点。新增缺起点、错屏、错恢复位置、客户端几何和轨迹矛盾的反例测试。开发首轮还记录了未限定扩展方法接收者的编译错误，补齐 `this.PointToScreen` 后另轮实测。

### C702-LINUX-SCALE-01：环境字符串没有实际改变窗口缩放

v3 的 1.25/1.5/2 请求虽然在环境中可见，但窗口启动和结束的实际比例均为 1，宿主和独立校验器均判失败。对固定 Avalonia 12.0.3 本地程序集的只读 IL 检查说明该单值按索引配置没有命中当前屏幕。运行器改为仅对本次子进程设置 `AVALONIA_GLOBAL_SCALE_FACTOR`，清除本次子进程的 indexed screen override，结束后精确恢复两项环境变量；诊断同时保留 global 与 screen 原始值。全局变量名在此指进程继承的框架设置，没有更改系统显示设置。

该值是底层屏幕比例的**乘数**。本机两屏基准实际为 1，所以默认四项预期为 1/1.25/1.5/2；其他基准比例应使用显式用例计划，分别指定乘数 `processScreenScale` 与预期实际 `expectedScale`。初始和结束窗口都须满足实际比例，写入环境本身不能取得通过。

### C702-CAPTURE-DPI-01：高 DPI 的窗口离屏读回丢失字形

真实比例变为 2 后，旧的窗口 RTB 初始 DPI=192 路径里三种 F 探针都缺失默认标签，红 marker 和 TextBlock F 仍在，独立图表导出的 F 也正确，因而实际门槛失败。产品原有导出已规避初始 DPI 与嵌套绘制 transform/clip 叠加的问题；验收窗口现在也采用初始 DPI=96 的画布，以完整 live Window VisualBrush 将 client DIP 区域显式映射到实际比例的像素区，保持原物理像素尺寸。没有改用图表导出来替代窗口树，也没有修改产品绘制或字形方向/恢复门槛。

对照开发轮次中 1 倍与 2 倍宿主及全部场景检查均通过，2 倍默认字形重新出现。新增捕获元数据检查最初把 tuple 与 list 直接比较，导致该开发轮次的原独立 oracle 判失败；修正后另存 posthoc 校验，不覆盖原失败。正式候选仍重新冻结、构建和执行，离屏读回不据此晋升为真实 compositor 字体或呈现认证。

### v6 调查记录：Linux GPU 和原生异常退出

- **C702-LINUX-GPU-01**：当前 WSLg 的 GLX renderer 为 `llvmpipe (LLVM 21.1.8, 256 bits)`，被 Avalonia 默认黑名单拒绝。显式 GPU 请求保留启动失败，不清黑名单、不用软件回退冒充 GPU；默认路径允许框架自身的软件回退，并记录实际结果。目标 Ubuntu 的硬件 GPU 验收仍待实机。
- **C702-LINUX-STARTUP-01**：v3 与 v4 的首个默认用例均以 134 退出，stderr 为 `free(): invalid pointer`，缺少最终 raw 和图像。另一次全新缓存诊断真实复现，并取得 229,214 字节的 native crash report：系统 HarfBuzz 12.3.2 与包内 HarfBuzzSharp 8.3.1 混入同一字体初始化栈。包内 ELF 有 59 个可被抢占的 `hb_*` 重定位，与[上游问题 3038](https://github.com/mono/SkiaSharp/issues/3038)及[原生链接修复 5352](https://github.com/mono/SkiaSharp/pull/5352)相符。没有据合并状态假定当前 NuGet 8.3.1.3 已含该修复。

验收宿主新增默认关闭的 `--isolate-native-text`，运行器须显式传 `-LinuxNativeTextIsolation`；仅在 Linux/glibc、Avalonia 初始化之前对包内绝对路径使用 `NOW|LOCAL|DEEPBIND`，保留句柄并注册仅匹配 HarfBuzzSharp 的解析器。独立检查核对请求、实际解析器调用、原生模块、相对 RID 路径、哈希和标志位。全新缓存的默认路径对照已正常退出并通过全部宿主、场景和独立像素检查。该模式没有改依赖、系统库或黑名单，也没有为普通应用消费者自动安装启动修正；C702-LINUX-STARTUP-01 在普通消费者范围仍开放。最终 Linux 条件验证须明确注明此开关，不覆盖未隔离的历史失败。

后续普通消费者批次已将这一绑定机制提供为明确的生产 API，接入正常 Demo/Usage 并验证独立 nupkg 消费者；当前锁定版本和实际环境中的启动修正结果见[字体启动报告](charts-c7-consumer-font-startup.md)。以上仍为 v6 当时的范围与原始状态，不能将新批次结果回填为旧批次通过。

## 剩余工作

- 按实测结果修复可重现的平台缺陷；没有目标设备的条目保留明确待验条件。
- 完成目标实机的异 DPI/刷新率跨屏、物理鼠标与触控、IME、系统保存选择器/portal 和辅助工具矩阵。
- 百万点命中尾延迟和 Windows UIA / Linux AT-SPI 原生选中项枚举继续按用户决定后置。
- 完整官方对照、长期资源/句柄趋势、发布方式/裁剪/AOT及 API 冻结仍归 C7-03～06。
