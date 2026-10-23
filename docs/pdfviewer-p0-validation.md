# PdfViewer P0 基线与可行性验证

验证日期：2026-10-15。本文保留初轮基线与四项原型结果，并在后文记录[生产写入层续轮](#生产写入层与内容删除续轮)。初轮没有修改产品源码；续轮已在功能包实现受限 FreeText 后端，Themes / Demo 的阅读 UI 未增加编辑工具。**G0 尚未关闭**。

当前结论以[真实语料续轮](#真实出版语料与兼容性边界续轮)及[支持 / 拒绝边界](pdfviewer-compatibility-boundaries.md)为准。P0 的技术可行性清单已有证据，官方可运行参考版本及目标镜像 / 设备入口继续待办；生产化任务按主计划归回后续阶段，历史章节的“P0 尚缺”不得用来无限追加生产工作。

## 冻结范围

初始源码基于 `2a8199b65157952b8bbb52220f467efbc8e4e273`，于 `2026-10-15T07:24:29Z` 捕获 257 个文件。快照覆盖 PDF 两包、测试、消费者、Usage、Demo PDF 页面 / 接入和共享构建配置。工作区同时存在其他控件的修改，它们不属于本轮修改范围。

| 项目 | 冻结值 / 状态 |
| --- | --- |
| 源码 manifest SHA-256 | `67256AB841AB546686A1551571E5DF9D491F30985F73BA9370BC2A5A3C31485B` |
| 源码 ZIP SHA-256 | `BDE9B89246C3D5111BEEF06B4778217242DB343626BCAA81218C3CAD1D322684` |
| 框架 | SDK 10.0.401、net10.0、Avalonia 12.0.3、包版本 0.2.0-preview.2；未升级产品依赖 |
| PDFium | 155.0.8044；12 RID 与既有发行许可校验清单匹配；8 个配套 public header 固定 SHA-256 |
| 运行绑定 | 每组报告记录实际加载的 native 模块路径 / 哈希及托管验证程序集哈希 |
| 官方资料 | 7 页公开文档的 HTML、抓取 UTC 和 SHA-256；不是商业控件运行版本 |
| Windows | 当前 x64 主机，OS 构建 `10.0.26200`，16 逻辑处理器；本轮原生后端运行 |
| Linux | Ubuntu 24.04 LTS x64、X11 / XWayland 为目标；本轮未在目标系统运行 |
| 麒麟 | x64、ARM64 均为必需实机验收目标；精确发行版 / 补丁 / 设备仍待登记 |
| 官方运行宿主 | 尚无可合法运行且固定版本的参考宿主；未做性能 A/B |

manifest 与 ZIP 的每个 entry 均核对内容哈希和条目唯一性。冻结脚本复用已有快照时会验证哈希；原始源码、原生元数据、目标清单和已抓取的官方资料不因重放而静默替换。新增原型源码及编译产物另有执行 manifest，与初始快照分开记录。

## 四项原型结果

| 原型 | 实测范围 | 结论与边界 |
| --- | --- | --- |
| POC01 保存 | 新增真实页面内容；全量 / 增量内存写出、重开；同目录临时文件、刷新、验证、原子替换；I/O 失败、取消、Windows 文件占用；密码和真实 CMS 签名样本 | 有限可行。R2 加密保留；AES、权限角色、磁盘满、断网挂载、崩溃恢复和产品 Dirty / Undo 尚未验证 |
| POC02 批注字体 | 合法 OFL 衍生 TrueType 子集；CJK Stamp 创建、AP 嵌入、保存重开、修改后再保存；FreeText 字体资源负对照和结构写入桥接 | Stamp 原生可行；FreeText 需要资源写入层。4 个正例经 pypdf / Poppler 和目检；Linux、字体回退、复杂排版与 UI 编辑未验 |
| POC03 表单 | 真实 AcroForm 文本、复选框、不可编辑组合框；原生焦点、鼠标、OnChar、键盘选择、提交、保存重开；canonical 值与 AP | 有限可行。3 个 widget 保持交互，未 flatten；不代表 Avalonia 输入法、真实 UI 定时器、单选 / 多选 / 多行 / CJK 表单已通过 |
| POC04 内容删除 | 整文字、整图像、完整覆盖独占嵌套 Form；隐藏信息和旧修订；两页共享嵌套 Form 子对象；局部字符 / 图像拒绝；当前 xref 对象 / 解压流独立审计 | 整对象 primitive 仅在限定样本通过；隐藏和共享 Form 子对象的 native-only 方案不可作为通用删除方案。窄域结构重写可清理隐藏样本；生产清理器尚未实现 |

### 保存与签名

`FPDF_SaveAsCopy` 的原生 BOOL 不能独自决定成功。本版本在故障注入中可出现回调异常后原生仍返回成功，验证工程将捕获的异常作为失败，并保留原文件哈希。临时文件使用唯一名称，只清理由本次调用创建的文件。Windows 占用目标和 POSIX 打开文件描述符下的替换语义采用不同分支；本轮只实测 Windows。

签名夹具使用真实自签证书和 detached CMS，没有持久化私钥。OpenSSL 独立确认：输入校验成功；增量输出保留原文件前缀，原签名覆盖的旧修订校验成功，但新增页面不在签名覆盖范围；完整重写输出的原签名校验失败。证书信任链、DocMDP / FieldMDP 未验。内容删除必须完整重写，不能同时承诺保留旧字节和移除旧敏感内容。

### 批注字体与外观

Stamp 的 CJK 外观和 canonical `/Contents` 均保存成功；重开后修改 AP 文字并再次保存成功。初次和修改后的 Stamp / FreeText 对应 Poppler 图像逐像素一致，四个正例均无渲染诊断。FreeText 桥接修改的是已保存的批注字典、AP 和 Contents，页面正文为空，没有用正文文本或 flatten 代替批注。

固定版本的 FreeText `AppendObject` 返回失败；`CreateAnnot` / `SetAP` 成功也不能补出所引用的字体资源，负对照由 Poppler 检出未解析字体并渲染为空白。原型使用 pypdf 保留 AP 的字体 Resources、FontFile2、ToUnicode 和 `/DA` 的实际资源名，证明结构写入路线可行。Python 不进入产品依赖；生产仍需选择受限写入器和字体子集实现。

另有一次 OpenType/CFF 经 Type1 路径写入的试验，PDFium 能显示，但 Poppler 报字体格式不匹配；该路线已拒绝，证据保留在本地 `fonts/experiments/`。当前夹具使用真实 TrueType，含字体来源哈希、衍生名称与完整 OFL 通知。

### 表单生命周期

输入经 `FORM_OnFocus`、鼠标事件和 `FORM_OnChar` 完成，未用直接修改字典冒充交互输入。保存后 `/AcroForm/Fields` 保留三个字段，文本 `/V`、复选 `/V` 和 `/AS`、组合框 `/V` 与 `/I` 均被独立解析确认，每个 widget 保留 AP。

实测组合框 Down 可以改变预览文字，Return 的 `OnKeyDown` 单独不提交 canonical 值，继续发送 `FORM_OnChar('\r')` 才提交。P6 必须统一处理键盘、字符、IME 和失焦的提交顺序。原型只模拟定时器标识；真实 dispatcher 定时器、页面虚拟化、物理 Tab / IME、剪贴板和撤销仍属后续工作。Poppler 对英文表单有 Symbol / ArialUnicode 系统回退字体提示，英文页面目检正常，不能据此推断跨平台 CJK 表单可用。

### 删除反例与结构清理

独立审计同时检查原始字节、当前 xref 可解析对象、解压流、解析后的文字操作数、图片资源、字典字符串和独立文字提取，避免压缩或十六进制编码掩盖残留。旧修订采用已知 marker、Prev 和 EOF 检查；没有实现任意损坏 PDF 的完整取证。

- 整文字 / 图像对象移除并 GenerateContent、完整保存后，两个限定夹具的目标内容不存在，控制文字保留。
- 完整覆盖的独占 Form 夹具包含一层独占嵌套 Form；整顶层对象移除后，秘密文字、嵌套 Form 流和页 XObject 资源均被独立审计确认移除，控制文字保留。原型按已知对象身份选择，尚未实现区域命中、共享资源和路径的完整覆盖判断。
- 隐藏样本中，native-only 重写仍保留 Info、XMP、页面缩略图、结构树、目录标题和附件中的秘密内容。旧增量修订被移除，不等于隐藏数据已清理。
- 共享嵌套 Form 的内层 `FPDFFormObj_RemoveObject` 与外层 `FPDFPage_GenerateContent` 都返回成功，但保存重开后两页仍可提取秘密文字；native-candidate 输出 PDF 的未解压字节中搜不到 marker，解压流仍能定位它。这个反例针对本次调用序列，不证明所有 Form 写入方案都不可行。官方对部分覆盖 Form 本身也不执行删除；本项目先明确拒绝，完整覆盖的删除和资源清理另设验收。
- 局部字符和局部图像没有实现，原型直接拒绝，输入哈希不变，不生成所谓已删除的输出。

额外窄域实验用全新 catalog 和结构重写清理隐藏样本，独立审计确认秘密内容、旧修订、孤立对象和隐藏图片均消失，控制内容保留。该桥接的清理范围比官方默认范围更广，主动丢弃整个目录和附件，**不能作为通用文档转换器**。正式实现需单独决定目录 / 附件保留策略、共享资源处理、字符级改写及保留区质量。

## 性能与回归

最小后端宿主对两页样本及 1000 页合成纯文本样本各运行 7 次，记录 14 条加载、1200×1600 栅格、全文搜索、同步释放、分配量和进程内存采样。每类的后 6 次用于描述性中位数；数据见机器记录及 `baseline/timing-summary.json`。

1000 页文件仅约 272 KB，不能代表生产大文件。内存数字是采样时读数，不是峰值；没有 UI 首屏 / 帧 p95、扫描 / 矢量语料、官方 A/B 或 Linux 性能结论。P1 的异步释放 / 调度工作不因该简单样本速度较快而取消。

本轮 Release 验证工程构建零警告 / 零错误；既有 Core 18 项、Headless 19 项通过。49 项独立检查涵盖输入 / 输出内容、签名、字段、字体、AP、实际加载模块与快照一致性；删除另有 7 类案例审计，基线保护 6 项验证通过。拒绝样本和预期反例计为“正确识别限制”，不计为完整功能通过。

## 技术决定及剩余门槛

| 决定 | 后续落实 |
| --- | --- |
| 保留 PDFium 读取 / 渲染 / 已支持 primitive，增加受限结构写入边界 | P0-C01 选型；P4 管理修订、事务、对象图及保存；不直接引入原型 Python 工具 |
| 回调错误优先于原生成功；全量保存先验证再替换 | P4 完成 Dirty、取消、权限、失败和崩溃恢复 |
| 非拉丁批注需字体资源、子集、外观和 canonical 文本一致 | P0-C01 / P5 验证 FreeText 与外部再次编辑，补 Linux / 排版 |
| 表单宿主持有环境和页面租约，焦点提交先于保存 / 释放 | P0-C03 / P6 补字段矩阵、真实定时器和 Win/Linux IME |
| 内容删除拒绝未经验证的范围，完整重写并审计隐藏 / 孤立内容 | P0-C02 / P7 完成局部文字、共享资源、签名策略和可恢复数据清理 |
| 官方运行版本、真实语料与目标设备尚未齐备 | P0-C04 补资料；保持 Ubuntu 与麒麟 x64 / ARM64 验收门槛 |

主计划据此将 P4 / P5 / P7 的预算共增加 8–12 工程日，总预算调整为 100–154 工程日，仍需补充 PoC 后校准。公共契约、调度 / 异步释放与 Ubuntu CI 骨架可以并行推进；完整批注 / 删除日期暂不冻结。

## 生产写入层与内容删除续轮

续轮证据目录：`artifacts/pdfviewer/20261015T083100Z-p0-writer/`。初轮快照与记录保持原样；本轮生产源码、依赖和验证程序集另做快照与哈希绑定。可提交的[续轮机器记录](validation/pdfviewer-p0-writer-20261015.json)保留当前结果，原始 PDF、图像和日志仍按仓库惯例放在 `artifacts/`。

### 生产写入和字体资源

功能包新增 `Editing.PdfEditSession`，每次修改从当前自有字节创建临时结构文档，生成完整候选并经 PDFium 重开后才提交字节与修订。失败或取消保持原字节和 Dirty 状态。提供创建 / 更新 / 读取 FreeText、独立字节导出、流保存与唯一同目录临时文件原子替换；稳定 ID 写入 `/NM`。这是一条已实现的 C# 后端，不使用原型 Python writer。

PDFium 生成带嵌入字体的临时 AP，PDFsharp Core 6.2.4 导入资源引用闭包，写标准 `/FreeText`、Unicode `/Contents`、`/AP/N` 和 `/DA`。每条批注使用独立字体别名，在 AP、页面资源与 AcroForm DR 中引用同一字体对象。重开后按同一 ID 更新外观与内容；原页面内容、字段 V/DV/AP、目录、附件字节和自定义字典都有独立保持检查。继承页面资源另有核心回归。

当前仅支持明确提供的静态 glyf TrueType、单行黑字，字体完整嵌入。缺字、限制嵌入、非法 UTF-16、复杂 shaping / RTL / 组合文字、换行或超框直接拒绝；自动子集、CFF/TTC/可变字体与外部编辑器重排仍开放。字体许可由宿主提供；测试夹具保留 OFL 原文，不把系统字体复制进产品。编辑输入上限 256 MiB，字体上限 32 MiB。

结构会话拒绝加密和已签名输入，避免清除加密或破坏签名；已有只读加载行为不变。PDFsharp 完整保存会改写 Producer / XMP，当前保持检查不能扩展为任意 PDF 结构无损保证。Undo/Redo、AutoSave、控件编辑 UI 与全字段表单留在 P4–P6。

依赖采用精确版本范围 `[6.2.4]`，配套保存官方 MIT 许可、包 / DLL 哈希与传递依赖材料。真实 NuGet 审计发现默认 Pkcs 8.0.1 命中两项公告，已显式覆盖为 10.0.13，再执行审计与回归；公告及被拒绝版本记录在许可来源清单。[微软公告一](https://github.com/dotnet/announcements/issues/454)、[微软公告二](https://github.com/dotnet/announcements/issues/468)

### 真正内容删除的新增验证

| 案例 | 独立结果 | 生产边界 |
| --- | --- | --- |
| 同一个 ASCII text 对象直接 SetText | 秘密文字消失，但右邻文字左移约 179.732 pt | 明确拒绝这个保留区不正确的方案 |
| ASCII/Helvetica 字符位置表改写 | 目标字符消失，左右邻字原点与 Poppler 保留区像素一致 | 仅固定字体 / 编码夹具；未实现通用字符簇编辑 |
| 共享 Form 完整调用的单页删除 | 页 0 删除该调用，页 1 文字和像素保持；共享资源仍供页 1 使用 | 不以全文件 marker 消失误判页面作用域；局部 Form 按既定范围拒绝 |
| 整红色路径删除 | 目标绘画命令和红色像素消失，保留蓝路径不变 | 尚未实现任意区域完整覆盖判定与裁剪 / 透明度规则 |
| 连字的单字符、部分路径 | 输入哈希不变，不生成已删除输出 | 明确不支持 |
| 批注 / 表单 / 动作隐藏数据 | native 删页文字后，Annot Contents/AP、字段 V/DV/AP、OpenAction JS 仍存在 | 反例确认：生产删除必须增加结构清理器，当前没有通用删除 API |

独立审计解析当前 xref 对象和流，结合页面范围、文字坐标与 Poppler 图像检查。不能用“文字提取已空”“原始字节搜不到”或原生成功返回代替通过。下一步重点是隐藏表面策略和资源图清理，再补复杂编码 / CJK 字符簇、局部图像以及不可解析内容的拒绝策略。

复现续轮：

```powershell
pwsh -NoProfile -File scripts/Run-PdfViewerP0.ps1 `
  -PythonPath python -OpenSslPath openssl -PdfToPpmPath pdftoppm -IncludeProductionWriter
```

本轮仍没有 Ubuntu / 麒麟实机、官方可运行宿主、自动字体子集或完整生产删除的通过结论。预算保持主计划的条件范围，不把受限后端完成计作完整 P4 / P5 / P7 完成。

续轮最终验证：Core **34/34**（含 16 项新增写入回归）、Headless **19/19**；生产 C# 路径 **11 项**检查、独立生产结构 / 渲染检查 **49 项**及 **4 个**负对照通过；高级删除 **7 类 / 27 项**原生检查、独立审计和 **5 个**失败拒绝自测通过。两个独立 NuGet 消费者分别 **31/31、34/34**，从全新缓存验证创建、重开修改及保存，合计 **65 项**。最终依赖图的 NuGet 审计通过，未抑制已发现的漏洞告警。

功能包、Themes 和独立 Usage 构建通过。统一 Demo 的额外构建检查被当前并行修改的 Charts 编译错误阻挡（例如 `ChartAxis.ActiveScaleBreaks` / `IsVisible` / `ActivePlotBands`），本轮没有计为 Demo 通过；详细日志为 `demo-build.log`。这不改变已独立通过的 PDF 包与 Usage 结果。

## 记录与复现

可提交的机器记录：[pdfviewer-p0-20261015.json](validation/pdfviewer-p0-20261015.json)。验证入口：[Run-PdfViewerP0.ps1](../scripts/Run-PdfViewerP0.ps1)，工程及工具要求见 [P0 README](../benchmarks/PdfViewer.P0/README.md)。

本机原始证据位于 `artifacts/pdfviewer/20261015T072111Z-p0/`，包括初始快照、官方 HTML、原型 PDF、渲染图、许可、原始计时、独立审计、XML 回归和日志。`artifacts/` 按仓库规则不提交；机器记录保留证据路径与哈希。不要将其中标记 unsafe 的 native candidate 当作可对外披露的删除结果。

```powershell
pwsh -NoProfile -File scripts/Run-PdfViewerP0.ps1 `
  -PythonPath python -OpenSslPath openssl -PdfToPpmPath pdftoppm
```

新运行会创建新证据目录并抓取官方资料；复用本地快照可显式指定已有 `-OutputDirectory`。运行只验证该机器；Linux 与物理输入仍须在目标系统执行。官方公开对照范围来自 [保存](https://docs.avaloniaui.net/controls/data-display/pdfviewer/loading-and-saving)、[批注 / 表单 / 内容删除](https://docs.avaloniaui.net/controls/data-display/pdfviewer/annotations)及[平台 / 性能](https://docs.avaloniaui.net/controls/data-display/pdfviewer/platforms-and-performance)，原型结论来自本次输出和独立检查。
## 隐藏信息清理与完整流水线续轮

证据目录：`artifacts/pdfviewer/20261015T091230Z-p0-redaction/`；[本轮机器记录](validation/pdfviewer-p0-redaction-20261015.json)串联前两轮记录，历史快照不改写。冻结时间 `2026-10-15T09:17:01Z`，305 份源文件；manifest SHA-256 `D0CF5FD4819D02E3127E929E0EFA1AE546D428F068F3BBEDD5039A8DCD394EEE`，ZIP SHA-256 `E10ABF906715ECDFBE50AC609FEE4A08ACC89819F295D28ABDB3B983E94F7C51`。源码清单排除 bin / obj / Python 字节码等生成文件，实际 PDFium / PDFsharp / Pkcs 模块与两包消费使用同一实现的哈希绑定。

本轮提供功能包中的 C# 生产实现：`SanitizeAsync`、`ApplyRedactionsAsync` 和输入 / 输出哈希报告，复用既有保存事务。调用必须显式传入 `RemoveAllNonPageData`。策略会移除全部批注（含可见 AP）、字段 V / DV / AP、AcroForm / XFA、目录 / 附件、动作 / JS、原始 Info / XMP、结构 / ActualText、缩略图、PieceInfo 和私有字典；只重建支持的页面绘制及几何。它不扁平化表单 / 批注，与官方默认保留目录 / 附件的策略不同。

生产流水线：严格绘制结构预检及非页面数据清理 → 按区域预检全部对象 → 删除完整对象 / GenerateContent → 递归修剪页面和 Form 的活资源 → 新目录完整重写 → 结构重开审计 → PDFium 重开 / 页数 / 目标区域无对象审计 → 单修订提交。原生完整保存后额外核对保留对象类型、顺序和位置；任何阶段失败或取消均不提交候选，原会话字节、修订和 Dirty 保持。所有 PDFium 调用仍受全进程串行门控制。

支持完整覆盖的文字、图像、路径和整次 Form 调用。共享 Form 只移除指定页面的调用，不修改共享子对象；其他页面保留的文字仍应存在。部分对象、空区域、受影响页面的旋转 / 透明 / 自定义裁剪均拒绝。清理器对继承资源 / MediaBox / CropBox / Rotate、递归资源使用、操作符参数、绘制字典值类型、技术 XMP、图像 / 调色板有效字节范围逐项处理；拒绝可选内容、带属性的 marked content、Type3、Pattern / Shading、ICC 等颜色空间 / 默认覆盖、JPEG / JPX / JBIG2、未知过滤器和非默认 UserUnit。PDFsharp 新生成的 Producer / 日期 / UUID 等技术元数据经 XML 白名单验证，没有复制原始元数据。

独立验证仅消费公共 API 的输出，不用 Python 生成正例结果。pypdf 遍历所有当前 xref（包括不可达对象）、解压流和内容操作数，检查隐藏副本、旧修订和资源引用；Poppler 比较清理前后全部页面像素、删除后保留区域及共享实例。负对照包含缺失输出、损坏 PDF、字节篡改、伪造哈希后在不可达 Form 的 hex 字符串中注入隐藏内容、伪造哈希后移动保留内容，以及缺失清单项；必须返回失败并覆盖旧的成功报告。

这是一条完成限定范围验证的后端流水线。它不保证任意恶意字体 / CMap 名称或保留像素中不存在隐写信息，不移除未选中的不可见 / 裁剪外页面文字，不擦除调用方原数组、原文件、文件系统历史或进程内存。字符簇 / 部分图像 / 任意路径切分、嵌套 Form 内容编辑、完整 UI / Undo、真实复杂语料、内存峰值、Linux / 麒麟和官方性能对照仍需实施，G0 / G7 保持开放。

完整冻结复跑：Core **53/53**（较前轮增加 19 项清理 / 删除 / 几何 / 恶意参数 / 继承与保留 CJK 字体回归）、Headless **19/19**；本轮生产公共入口 **15/15**，独立清理 / 删除 **64/64** 和 **6/6** 负对照通过。原保存 / 字体 / 表单 / 删除反例、FreeText 生产与高级删除验证均在同一冻结实现重新执行通过。独立验证同时要求清理后两页像素完全一致、删除后保留区和第二页完全一致、四类目标区域空白、隐藏字符串 / 解码流 / 孤立对象无残留、资源集合等于实际使用集合，以及仅保留一份完整修订。共享 Form 标记保留在未选页属于正确结果。

两个实际 NuGet 包从全新缓存消费，功能包 **36/36**、默认 Themes **39/39**（合计 75 项）通过，新增清理 / 删除 / 保存调用随包交付，包内功能 DLL 与原生探针的 DLL 哈希相同。真实 NuGet 漏洞审计通过，PDFsharp `[6.2.4]` / Pkcs `10.0.13` 及许可保持。功能包、Themes、Usage 和统一 Demo Release 构建均为 **0 警告 / 0 错误**；本轮 Demo 是编译集成验证，没有新增编辑 UI 或声称原生窗口 / 实机验收。前轮 Charts 编译阻挡是历史结果，本轮独立输出目录重建已通过。

复现：

```powershell
pwsh -NoProfile -File scripts/Run-PdfViewerP0.ps1 `
  -PythonPath python -OpenSslPath openssl -PdfToPpmPath pdftoppm -IncludeProductionRedaction
```
## FreeText 自动子集与 CJK 字体续轮

最终证据目录：`artifacts/pdfviewer/20261015T111230Z-p0-font-subset/`；[本轮机器记录](validation/pdfviewer-p0-font-subset-20261015.json)以前一轮清理记录为父记录，不覆盖历史结论。源码冻结时间 `2026-10-15T11:14:18Z`，共 **322 份**；manifest SHA-256 `D95D048D4D6B89B0B35FEBFEC2097058422CFA319D9EB51C02F9986ABC14776C`，ZIP SHA-256 `B44B6A43F33B74D64FA5B7AFA986EFD98419530CB1DDF6821014F883E607FE3E`。

生产写入默认改为 `PdfFontEmbeddingMode.AutomaticSubset`，保留原单参数构造入口，并新增显式 `Full` 重载。C# 子集器在 PDFium 全局串行门外计算所需字形及 `.notdef` / 递归复合依赖，压缩 GID，重建 glyf / loca / hmtx / hhea / maxp / cmap / head / post、名称和校验和；保留必要提示程序，省略当前无整形水平文本契约不使用的布局 / 竖排等表。NoSubsetting 返回完整原字体，禁止可编辑嵌入的许可继续拒绝。每次更新都从宿主持有的原字体重新生成子集，并替换同一批注 ID 的 AP / 页面 / AcroForm 字体资源，输出不残留旧子集引用。字体上限保持 32 MiB。

匹配 PDFium 的实际 DLL 对照发现：保存标志 `FPDF_SUBSET_NEW_FONTS=8` 可缩小页面文本字体，但不处理批注 AP；同一 7,092 B 字体的 AP 在 flags 2 / 10 下都保持完整，不能据此声称自动子集已生效。生产选择字体预子集，而不启用这个实验标志。CJK 扩展区另复现 `SetText` 的 UTF-16 代理项问题，canonical Contents 正确而 AP 提取为 NUL。现改用 `LoadCidType2Font` 和 `SetCharcodes`：Unicode 标量分配独立 CID、映射准备后 GID，并提供显式 UTF-16BE ToUnicode；扩展汉字与不同 Unicode 共用轮廓都保持准确文本。变宽字的 CID 宽度与原路径实测一致。

独立语料是三套完整 OFL 静态 TrueType：LXGW WenKai v1.522、固定 Google Fonts 提交的 Zen Kaku Gothic New 和 Gowun Dodum，官方来源、许可、完整字节及 SHA-256 随运行登记。完整源字体放 `artifacts/`，产品不捆绑默认字体。10 个场景包含简体、繁体、日文、预组韩文、Ext-B、cmap4 / cmap12、复合轮廓、NoSubsetting 和 Unicode 共用字形；合成边界字体明确另标，不当作新增真实字体兼容。每例比较 Auto / Full 创建和重开更新，共 41 份 PDF。pypdf 检查实际 AP、ToUnicode、CIDToGID、资源闭包和旧字体回收；独立 fontTools 检查请求字形轮廓 / 度量 / 复合闭包、GID 和 sfnt 完整性；Poppler 比较模式像素与保留区域。伪造输出哈希后的损坏字体、错误 ToUnicode、错误复合依赖、几何移动及缺失 PDF 必须拒绝，并覆盖陈旧成功记录。

完整 CJK 字体可显著缩小：LXGW 简体修改样例的字体程序由 25,575,676 B / 46,867 字形变为 7,172 B / 17 字形；这是一条指定字体 / 文字的结果，不是通用比例。已接近最小字形集的合成字体可能因规范 cmap / 名称开销略增；NoSubsetting 样本必须字节完全保持。记录每次耗时 / 总托管分配差值 / 当前工作集，未测峰值、p95、UI 或官方性能对照。

当前仍限静态 glyf TrueType、水平单行、预组字符；CFF / TTC / 可变字体、字体回退、复杂排版 / RTL、竖排、变体序列、换行、外部编辑器再次输入及 Linux / 麒麟实机仍未通过。保存的子集不含未来未用字形，重开修改仍需提供原始字体。G0 和完整 F38 保持开放。

冻结复跑通过：Core **67/67**（新增 14 项子集 / 权限 / 复合 / 校验和 / 资源更新 / Ext-B / 别名 / 异常事务回归），Headless **19/19**；新生产探针 **50/50**，独立验证 **465/465**，负对照 **5/5**。字体矩阵的 20 对创建 / 更新 Auto-Full 页面像素完全一致，原页面保留像素一致，实际 AP 提取与 canonical Contents 一致；所有输出字体表严格校验，40 份输出 PDF 的 CID `/W` 字宽另与 fontTools `hmtx / unitsPerEm` 比较。既有保存 / FreeText Full / 表单 / 高级删除 / 严格清理和独立负对照均在同一冻结程序集重跑通过。

两个真实 NuGet 包从全新缓存消费：功能包 **41/41**、默认 Themes **44/44**（合计 85 项），覆盖默认 Auto、显式 Full、实际 FontFile2 缩小、原字节完整嵌入和 NoSubsetting，以及已有阅读 / 写入 / 清理 / 删除流程。包中功能 DLL 与探针实际加载 DLL 的哈希一致。功能包、Themes、Usage、统一 Demo Release 构建均 **0 警告 / 0 错误**；最终锁定图的 NuGet 审计（含传递依赖）通过，未添加生产依赖。Demo 此轮为编译集成，字体与物理输入 / Linux 验收分别记录。

复现（两个 Python 也可指向同时含相应依赖的同一环境）：

```powershell
pwsh -NoProfile -File scripts/Run-PdfViewerP0.ps1 `
  -PythonPath python -FontToolsPythonPath python `
  -OpenSslPath openssl -PdfToPpmPath pdftoppm -IncludeFontSubsetting
```

## AcroForm 高级字段与 CJK 表单字体续轮

最终证据目录：`artifacts/pdfviewer/20261015T114530Z-p0-forms/`；[本轮机器记录](validation/pdfviewer-p0-forms-20261015.json)以前一轮字体记录为父记录，保留历史证据。本轮只增加 P0 原型、独立验证和计划，没有新增功能包表单 API 或 Demo 表单编辑 UI。

源码冻结于 `2026-10-15T11:47:38Z`，共 **330 份**；manifest SHA-256 `AD59B4F1BC1AB1F4151BF1106811E32AD529618B9ACE1EDC8A5A4307E6FE62C8`，ZIP SHA-256 `D68F1E197491AE96B2A9CDA30950FC749DBC948D1BA2564812B3837C1FA98A97`。所有探针使用同一冻结构建的程序集及已核对的 PDFium DLL。功能包 / Themes 源文件集合与前轮相同，实际功能 DLL SHA 也相同，因此前轮双包新缓存消费者及构建 / 审计作为有哈希父证据引用，未冒充本轮重新执行或新增表单覆盖。

| 实验 | 原生检查 | 独立检查 | 负对照 | PDF |
| --- | --- | --- | --- | --- |
| 单选、多行、单 / 多选列表、只读 / 必填 | 26 | 69 | 7 | 3 |
| 三套本族 CJK 字体及受控 AP 桥 | 30 | 229 | 9 | 15，其中 6 份为原生失败反例 |

高级字段通过鼠标焦点、点击、方向键、Ctrl 多选、translated Return / 字符输入、失焦、完整保存和重开执行两轮编辑。独立 pypdf 验证 `/Fields` / `/Kids` / `/Parent` 双向关系、单选 `/V` 与各 Widget `/AS` 的互斥、列表 `/Opt` 的 export / display 区别、数组 `/V` 与 `/I`、多行 canonical CRLF 及 AP 两行位置。Poppler 对照单选可见圆点、逐行高亮和选择索引、只读文字和页面保留区域；第二轮必须可见更新。必填标记保留，但 PDFium 允许清空必填字段，宿主仍需单独实现提交校验。默认值重置、任意字段校验动作和脚本未在本轮新增覆盖。

CJK 先导复现了值正确但字体错误的反例：仅向 AcroForm `/DR` 注册完整内嵌字体仍不够，当前 DLL 原生重建 AP 会为中 / 日文添加未嵌入 SimSun，为韩文添加未嵌入 Ebrima。原生值 / 重开检查都可能通过，韩文渲染却为方框；中 / 日文在当前机器看似正确也不能当作可移植字体通过。六份原生输出保留并由独立检查确认实际 `Tf` 使用未嵌入替代字体。8044 分支官方源码调查随记录保存，分支提交尚未映射为二进制精确构建提交，结论以实际输出为准。

本轮可行方案是**完整静态 TrueType BMP identity CID 默认资源 + 原生提交 canonical 值 + 每次提交后托管重建 AP**。桥只从已提交字段读值，保留原生字段与选择状态；保存后从桥接结果重开、输入前轮未使用的新字，再次提交 / 桥接 / 保存。三套 OFL 字体来源与全字节 SHA 固定，缺字拒绝；LXGW 用中文，Zen 用日文 / 繁体，Gowun 用韩文，不假定每套都覆盖中日韩全部字符。完整字体保留未来输入字形，不沿用仅含当前值的 FreeText 子集；不向产品包附送默认字体。

独立 fontTools 核验源 cmap 与请求字形、完整字体字节 / 表校验和；pypdf 检查实际 AP 所有 `Tf` 都解析到同一个 AcroForm 内嵌字体，核验 ToUnicode、CIDToGID、CID 字宽与源 `hmtx / unitsPerEm`、实际 AP 文本 / 多行位置与保存值一致。Poppler 检查中 / 日 / 韩可见外观、重开新字、逐行选中高亮及字段外像素保留。负对照在伪造输出哈希后修改父子结构、单选状态、列表索引 / 高亮、AP、ToUnicode、CID/GID、字宽，或移除字体 / PDF，均须返回非零并覆盖旧成功记录。

完整冻结复跑还覆盖已有保存、FreeText 生产写入、字体子集和严格清理 / 删除探针及各独立负对照；Core **67/67**、Headless **19/19**，基准工程 Release 构建 **0 警告 / 0 错误**。本轮新增独立检查合计 **298/298**，负对照 **16/16**。最终人工审图的报告及图像 SHA 随机器记录绑定。

这是同步单页原生宿主与受限字体桥的可行性结论。桥仅覆盖固定字号、左对齐单行、显式换行和全部选项可见的单选列表；不包含生产字段会话、自动换行 / 滚动 / 适配字号、任意现有表单风格、编辑期间字体与光标一致性、dispatcher 定时器循环、虚拟化 / 跨页焦点、Undo 或 IME。补充平面 Unicode、复杂整形 / RTL、竖排、CFF / TTC / 可变字体、外部交互编辑器、Windows 物理输入以及 Ubuntu / 麒麟均未完成验收。完整字体及映射有体积 / 内存成本，未作 UI 性能或官方对照结论。

方案与 P6 落地点见 [AcroForm 字体决定](pdfviewer-acroform-font-scheme.md)。P6 因外观桥和输入期间字体策略增加 2–4 工程日，条件预算从 8–12 调整为 10–16，主表合计 102–158；这不是交付日期承诺。P0-C03 的字段 / 字体受限可行性已补齐，生产宿主、真实语料、两平台 IME 仍开放，G0 / F40–F42 / G6 均未关闭。

复现：

```powershell
pwsh -NoProfile -File scripts/Run-PdfViewerP0.ps1 `
  -PythonPath python -FontToolsPythonPath python `
  -OpenSslPath openssl -PdfToPpmPath pdftoppm -IncludeAdvancedForms
```

## CJK 字符簇与部分图像真正删除续轮

最终证据目录：`artifacts/pdfviewer/20261015T121500Z-p0-partial-redaction/`；[本轮机器记录](validation/pdfviewer-p0-partial-redaction-20261015.json)以前轮表单记录为父证据，不覆盖历史结果。新增两个 C# 原型和独立验证器；功能包 / Themes 源码及实际 DLL 与父记录相同，公共 `ApplyRedactionsAsync` 仍只支持整对象删除。

源码冻结时间 `2026-10-15T12:17:45Z`，共 **338 份**；manifest SHA-256 `B8BDBE1B4DDF60C2B2233BD77C11EC0A550EEEA21BFA11609B5FC70303EFEFB5`，ZIP SHA-256 `9FB5CD2F9E8DB90A2DBB2B848C4C9CB8FF4EB1FA4D2CF505614DCE46733053D7`。所有本轮探针使用同一冻结构建及已核对的 PDFium DLL。保留全部先导材料，负对照用硬链接共享不修改的夹具，变异文件写入新临时文件后替换，不就地改写硬链接源。

| 原型 | 正例 | 拒绝 / 反例 | 原型检查 | PDF |
| --- | --- | --- | --- | --- |
| CJK 字符簇 | 7 | 每例严格部分命中拒绝；另 4 个不支持输入 | 67 | 18 |
| 部分图像 | 7 | 6 个不支持输入、2 个可恢复原图的覆盖 / 裁剪反例 | 50 | 24 |

CJK 每个正例的原始内容都是一个 packed `Tj`，PDFium 确认只有一个 text pageobject；删除发生在对象内部的 CID 范围，而非移除已分离的整字对象。算法读取实际 ToUnicode / 字宽和源字形边界，按 Unicode 字素簇与不可拆 CID 合并，几何部分命中时扩张到完整簇，严格模式则拒绝。保留字形输出绝对文本矩阵，邻字位置不移动；不用的 ToUnicode 项删除，已删 CIDToGID 项清零，再调用公共 `SanitizeAsync(RemoveAllNonPageData)` 清理非页面数据并完整新目录保存。

文字样本覆盖简体、繁体、日文组合浊点、韩文 Jamo、Ext-B、汉字加补充平面变体选择符、单字形对应多个汉字。使用固定来源的完整 LXGW WenKai / Gowun Dodum；独立检查源字节 SHA 及真实 cmap / GID。Jamo、变体和单字形多汉字是明确合成的 ToUnicode 边界样本，不代表 OpenType 整形或真实 IVS 字形通过。另核验旋转页、未实施的 `TJ`、缺失 ToUnicode、孤立 UTF-16 高代理映射的实际输入，并确认拒绝而没有成功输出。

图像通过真实读取调用矩阵和无损样本，对选区命中的像素单元写入不透明白色，创建选中调用私有的图像资源，然后经公共清理器完整保存。7 个正例为 RGB、灰度、90 度旋转、镜像、仿射、同页共享和跨页共享。所有目标外样本保持字节一致，未选共享调用保持原图；原图允许仅为这些未选实例继续存在，不声称整个文档不再含重复图像。仿射样例命中 **71 个像素单元**，会保守删除伸出选区的同一像素部分，尚未实施精确子像素重采样。

JPEG、软遮罩、裁剪、奇异矩阵、4 位图像和嵌套 Form 六类输入明确拒绝。JPEG 输入另由独立解码器确认是合法 JPEG，避免把无效夹具当作格式拒绝证据。白框 / 裁剪反例可直接从 XObject 恢复完整原始图案及隐藏尾随数据，说明视觉隐藏不是内容删除。

独立 pypdf 遍历全部当前 xref、资源闭包和解码流，解析实际文本操作数、ToUnicode、CIDToGID 与字宽；fontTools 从实际内嵌字体取得 cmap、GID、轮廓与 hmtx，重算字形边界、部分命中和完整簇扩张，核验保留坐标与字体不变。图像检查从实际矩阵 / 输入样本独立计算像素掩码，逐字节核验目标、非目标、共享实例和图像尾随数据，拒绝未用资源或原始图像流残留。所有新正例仅由 C# 产出。

Poppler 渲染 **30 页**，检查目标字形实际区域纯白且原先确有墨迹、保留区域像素一致、图像保守删除区域和未选页面一致。先导中繁体“刪”的空白 ROI 曾错误复用了 2px 抗锯齿扩边，碰到保留邻字“體”的两个像素；诊断确认它们位于邻字轮廓内。现将空白检查改为独立轮廓紧框，邻区差分继续使用抗锯齿扩边，未改变纯白阈值、源样本或算法。原失败报告与精确像素诊断一并保留在最终目录的 `pilot-diagnostics`。

最终独立检查 **325/325**，**15/15** 语义负对照通过。负对照伪造外层 PDF 哈希后，恢复原文 / 原图、加入孤立 Form、移动邻字、恢复已删映射、注入孤立代理或原始隐藏 Title、漏清目标像素、改坏外部 / 未选共享像素，以及以覆盖 / 裁剪保留原图；另覆盖缺失、损坏、字节篡改与陈旧成功报告。每例必须非零退出、明确识别失败原因并覆盖陈旧结果。

本轮重跑基础 P0、生产 FreeText 写入、整对象删除 / 严格清理及相关独立负对照；Core **67/67**、Headless **19/19**，基准 Release 构建 **0 警告 / 0 错误**。前轮完整字体子集 / 高级表单矩阵、包消费者、Demo / Usage 构建和依赖审计按相同产品源 / DLL 的父记录引用，没有算成本轮新执行。最终 **30 张完整分辨率图像**逐张人工审阅，另看 1 张缩略总览；实际 PNG / PDF / 报告与 **843 份证据**的 SHA 随机器记录绑定。

本轮结论是限定语法与合成语料的可行性。字体程序保留通用 cmap / 字形；不声称擦除字体或像素的任意隐写信息、源数组 / 原文件 / 文件系统历史。通用编码、`TJ` / 复杂排版、透明 / ICC / 压缩图像、裁剪 / 嵌套资源、生产选区 / 事务 / Undo、峰值 / 取消、真实语料及 Ubuntu / 麒麟仍开放；G0 / F43 / F44 / G7 未关闭。P7 保持 14–24 工程日条件预算，主表仍为 102–158，生产兼容范围扩大后再按实测校准。[方案及生产化任务](pdfviewer-partial-redaction-scheme.md)

复现（本开关运行相关基础 / 写入 / 删除回归与新原型；可另组合 `-IncludeFontSubsetting` / `-IncludeAdvancedForms` 执行完整历史矩阵）：

```powershell
pwsh -NoProfile -File scripts/Run-PdfViewerP0.ps1 `
  -PythonPath python -FontToolsPythonPath python `
  -OpenSslPath openssl -PdfToPpmPath pdftoppm -IncludePartialRedaction
```

## 真实出版语料与兼容性边界续轮

本轮新增真实语料准备器、公共 API 探针、独立验证器和记录导出器，未修改功能包 / Themes 源码、生产 API 或 Demo UI。[机器记录](validation/pdfviewer-p0-real-corpus-20261015.json)以部分删除轮为父证据，并给出绝对 `artifactRoot`。本轮证据目录位于：

`<USER_HOME>/.codex/visualizations/2026/10/15/01a13d45-c96e-78a0-81df-3fc930f9db6f/pdfviewer/20261015T130500Z-p0-real-corpus/`

运行目录名仅为标识，实际冻结时间为 **2026-10-15T12:53:27Z**。冻结 **345 份**文件，manifest SHA-256 `7E1BD9023BD993CFF6E253FDB3CB72B407D1EA11329E90A716B2C14A3800B5DD`，ZIP SHA-256 `E5564F42E7516DFAED8D7497EB96AF5DA8BB1B4E48EEA1BCA8C0628C60060A4D`。执行清单内的验证源码未在冻结后修改；本节及计划 / 状态含运行后的结论补记。产品源文件集合及实际功能 DLL 与父记录相同：`82AA7459A0FECAB2C8340EF6707A528BF2EC4A1BE9103FCA420D4B6574AEB895`；实际 PDFium DLL SHA 为 `04100C03E41CAC1F979E36E5E26FB860BCB5A7461F53830D3C098716624A27A9`。

### 来源与实测结果

来源清单固定 **6 份 / 314 页 / 6,154,810 字节**：GNU make v4.4.1 手册、IRS W-4 2026 英文与简中表单、日本数字厅 PDL、PLOS CC BY 论文、NIST 1959 扫描件。逐份保存官方 PDF / 落地页地址、文件 SHA、页数、内容标签、许可条件和对应官方许可 HTML，并记录清单冻结时间；没有将准备时间冒充各原件获取时间。完整 PDF 及字体不随产品包分发。GNU 的 GFDL 封面要求、IRS 的署名与不背书条件、PDL / CC BY 署名及 NIST 的政府作品 / 第三方标注边界均在清单保留，不将“公开下载”当作无条件再分发许可。无法取得的 LOC 来源没有换用未知镜像。

准备器默认离线，从已固定哈希的缓存复制 / 链接；缺失文件、变更字节或许可快照不匹配立即失败。显式 `--download` 才允许从登记官方地址取回精确原件，线上版本更新也不能静默替换。最终来源清单 SHA 为 `696EF0410AE2CE616C357059B5C71E990B6D6F6FB263F0AFCD977E19A0AF1D7E`。[逐操作矩阵](pdfviewer-compatibility-boundaries.md)是本轮支持与拒绝结论入口。

| 检查 | 最终结果 | 解释 |
| --- | --- | --- |
| 原生公共 API | 117 / 117 | 37 项操作为 26 接受、11 预期拒绝，另 6 份读取成功 |
| 独立 pypdf / Poppler | 645 / 645 | 全部原正文、原嵌入资源 / 字段 / AP / XFA 与代表页像素分别检查 |
| 语义负对照 | 12 / 12 | 伪造外层哈希仍须检出实际语义损坏及陈旧成功记录 |
| Core / Headless | 67 / 67、19 / 19 | 同一冻结产品的回归，XML 记录无失败 / 错误 / 跳过 |
| 基准 Release 构建 | 0 警告 / 0 错误 | 全部 20 个 runner 步骤 exit 0，日志哈希随记录保存 |

6 份均通过读取、未修改的字节 Export / 流 Save 和新增 `中文批注 ABC 123` 的 FreeText 重写 / 重开。原样字节保存不算结构写入。GNU 严格清理通过，整首页内容真正删除后页面保留且无绘制内容，其余 228 页文本 / 绘制操作及抽样像素保持；1pt 小区因只覆盖部分文字对象而明确拒绝。

IRS 英 / 中及日本文档的严格清理因有效 BDC 内联属性字典触发 PDFsharp 当前解析边界而拒绝。异常中的 `Dictionary` 是内部表示，并非非法源 PDF 运算符。PLOS 的实际资源含 ICCBased，当前严格清理仅允许 Device / Indexed；NIST 实际图像含 JBIG2 / JPX，被不透明图像容器边界拒绝，不能登记成 JPEG 实测。这五份的小区域删除同样在清理预检先停止，未执行下游部分删除；整页删除未尝试。全部拒绝都检查明确类型 / 原因以及字节、Revision、Dirty 不变，没有 unexpected failure。

### 独立检查与图像审阅

独立检查从固定来源清单而非原生“通过”标志出发，绑定原件 / 许可 / 预期、准备器、探针程序集、实际 PDFium、验证脚本与输出字节。逐页检查 FreeText 写入前后原正文流保持，清点原字体程序、页几何、书签、字段 / Widget 和所有 AP 状态；IRS 每份有 **48 末级字段、48 Widget**，`get_fields()` 的 54 节点包含父层。XFA 各数据包解码字节保持，未测试 XFA 交互或表单新值输入。Info / Producer 的规范化变化单独登记，不声称任意结构字节无损。

GNU 严格输出另核验完整 xref / 资源闭包，无孤立流；整页删除不能只靠空白截图，必须无目标绘制内容，并逐页核验其余 228 页的文字及归一化绘制操作。Poppler 共 **30 张**代表页图，对源 / FreeText / GNU 清理 / 删除结果做像素对照；PDFium 抽样首 / 中页，GNU 删除另看中 / 末页，不能称全部 314 页均渲染过。

12 个负对照覆盖伪造来源、伪造原件 SHA、漏样本、意外接受、错误拒绝原因、把阅读成功抬高为可写、拒绝后状态改变、缺失输出、原正文损失、陈旧 CJK AP、假整页删除及损坏 XFA。每例非零退出并命中预期原因，覆盖旧成功报告。最终人工实际查看 **30 张 Poppler、15 张 PDFium** 原分辨率图，报告记录逐图观察和 SHA；机器记录共绑定 **264 份证据**。固定测试批注覆盖 IRS 页脚部分表格，NIST 深色封面对黑色文字对比度有限，均明确记录，不把本实验当作自动选位 / 避让 / UI 可读性验收。

旧 CFF / CID 字体在真实 PDF 重写中保留，不等于允许新增 CFF 字体。GNU 本身含 TJ 且严格清理成功，历史字符簇原型拒绝 TJ 不能泛化到阅读或所有清理操作。NIST 文字来自原有不可见 OCR，非本控件新增 OCR，也不保证历史 OCR 准确。

### 阶段收口与未验项

本轮没有重新执行完整字体子集 / 高级表单 / 部分删除大矩阵、双包新缓存消费者、Usage / Demo 构建或漏洞审计；它们通过相同产品源和 DLL 的父记录链保留，未计入本轮新增覆盖。当前仅新增 Windows 后端和独立工具验证，没有 Linux / 麒麟、物理键盘 / IME、外部交互阅读器、峰值 / UI 或官方性能通过结论。

P0 的四类原型及补足技术方案、初始真实语料和支持 / 拒绝边界已有可复验证据；**G0 仍开放**，剩余外部基线是合法可运行的官方参考包 / 版本及 Ubuntu / 麒麟精确镜像与参考设备入口。P1 可开始公共契约、生命周期 / 串行调度、Ubuntu native / CI。生产事务 / Undo 归 P4；完整字体 / 批注归 P5；表单宿主 / IME 归 P6；生产部分删除和绘制兼容归 P7；打印归 P8；大规模语料、完整性能及目标平台终验归 P3 / P9。迁移不删减最终对标要求，102–158 工程日条件预算不重复增加。[更新后的主计划](pdfviewer-development-plan.md#p0-当前收口范围与阶段迁移)

复现（输出可为任一有足够空间的目录；Python 需 pypdf / Pillow，Poppler 和 OpenSSL 为独立校验工具）：

```powershell
pwsh -NoProfile -File scripts/Run-PdfViewerP0.ps1 `
  -OutputDirectory C:/validation/pdfviewer-p0-real-corpus `
  -PythonPath python -OpenSslPath openssl -PdfToPpmPath pdftoppm `
  -RealCorpusSourceCache artifacts/pdfviewer/real-corpus-cache -IncludeRealCorpus
```

需要联网首次准备精确原件时，先运行 `prepare_real_corpus.py --output <目录> --source-cache <缓存> --download`；离线 runner 不会自动回退到未固定的其他文件。人工审图后，执行 `export_real_corpus_record.py --run <目录> --output docs/validation/pdfviewer-p0-real-corpus-20261015.json`，导出器对冻结清单、父证据、执行源码、日志、独立检查、负对照、图像和 XML 再作绑定检查。空间处理保留了全部旧证据：工作盘满时将完整 `partial-text-research` 目录迁到本轮 C 盘根下 `preserved-research/`，原路径留有位置说明；正式历史运行目录未迁移。
