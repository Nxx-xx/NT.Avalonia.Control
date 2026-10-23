# P0 AcroForm 字段与 CJK 字体方案

本决定对应固定 PDFium `155.0.8044` 的原生宿主实验。代码位于 `benchmarks/PdfViewer.P0/AdvancedFormsProbe.cs` 与 `CjkFormsProbe.cs`，不是功能包新增的表单编辑 API。实际结果和未验项见 [P0 验证记录](pdfviewer-p0-validation.md)。

## 字段与提交

| 字段 | 原生输入与保存契约 | 独立检查 |
| --- | --- | --- |
| 单选 | 一个父字段、多个 Widget；鼠标选择 export name；父 `/V` 与各 Widget `/AS` 同步互斥 | `/Fields`、`/Kids`、`/Parent` 双向关系；恰好一个选中外观，第二次选择撤销第一次 |
| 多行文本 | Return 的 keydown 之后发送 translated character；CRLF 输入只翻译一次 Return；失焦提交 | canonical 值的 CRLF、两行 AP 文本和各行位置；重开后替换两行 |
| 单选列表 | 鼠标聚焦及方向键选择 | `/Opt` 的 export / display 区别，`/V`、`/I` 和可见选中行 |
| 多选列表 | 鼠标配合 Ctrl 切换选择；CJK 对照使用原生选择索引 API | 数组 `/V` 与多个 `/I` 对应且排序；AP 列出显示名称 |
| 只读 / 必填 | 只读拒绝输入；必填标记必须保留 | 必填不等于引擎禁止空值；本例实际允许清空，宿主须单独验证提交 |

原有单行、复选框、组合框实验继续保留。组合框 Return keydown 不能单独替代 translated character。后续宿主应统一处理换行、选择和失焦，不能用修改 PDF 字典模拟输入通过。

## 可编辑 CJK 字体

采用宿主显式提供、有可编辑嵌入许可的完整静态 glyf TrueType 字体作为默认编辑资源。在 BMP 范围内使用 Unicode 码点作为 CID，提供完整的 CIDToGID 和 ToUnicode。将同一字体注册到 AcroForm `/DR/Font`，字段 `/DA` 使用该资源名；原生输入负责提交 canonical 值，每次提交后由受控托管桥重建实际 `/AP/N`。保存后仍保留完整字体，供重开输入此前未用的字形。

只配置 `/DR` 的方案在本轮被否决：PDFium `155.0.8044` 原生重建 AP 时为中文 / 日文添加未嵌入的 SimSun，为韩文添加未嵌入的 Ebrima。值和原生重开检查均可能通过，中文在当前机器看似正常，韩文却呈方框。六份原生输出保留为反例；桥接输出必须独立证明实际每个 `Tf` 都解析到同一内嵌字体。桥从 PDF 的 canonical 值读取文字，不从预期答案写值；字段 `/V`、选择状态和用户输入仍由原生宿主生成。

辅助根因调查检查了 8044 分支的[官方 CPDF_BAFontMap 源码](https://pdfium.googlesource.com/pdfium/+/f91ca5a72358bb0b00b4da9481b21fe668157614/core/fpdfdoc/cpdf_bafontmap.cpp)：默认字体没有替代字体 charset 时按 ANSI 处理，CJK 选字路径可能转而查找替代字体。分支提交和源码 SHA 随记录保存；未证明该提交就是 NuGet 二进制的精确构建提交，因此结论以实际 DLL 的六份输出为准。不使用伪装成 Wingdings 等资源别名的实现细节规避。

FreeText 的按当前文本自动子集适用于由宿主重新提供原字体的批注更新。表单允许阅读器直接输入新字符，仅嵌入当前值的字形会丢失未来覆盖，所以此方案保留完整字体。不得依赖本机 CJK 字体或静默 fallback 来声称文档可移植；源字体缺字应明确拒绝或要求宿主选择另一字体。

实验使用带来源、许可和 SHA-256 的三套完整 OFL 字体：LXGW WenKai 中文、Zen Kaku Gothic New 日文 / 繁体、Gowun Dodum 韩文。每套只测试其实际 cmap 覆盖的文本；Gowun 不覆盖本轮中文，Zen 不覆盖简体“汉”，不能把三套都称为通用中日韩字体。第二次输入含初次值以外的新字符，独立检查源 cmap、完整 FontFile2 字节、CIDToGID、ToUnicode、实际 AP 文本和 Poppler 渲染。完整字体、128 KiB CIDToGID 与完整 BMP ToUnicode 都有体积成本，后续实现需记录去重及内存预算。

## P6 落地要求

1. 由生产文档会话管理唯一 FormFillEnvironment，所有原生操作仍走全局串行门；字段稳定身份基于字段树，不能持久化原生句柄或数组下标。
2. 回调 delegate、输入字节、页面句柄与 environment 同寿命；UI dispatcher 执行定时器、重绘和光标通知。虚拟页释放、文档切换、保存和打印前按明确顺序提交 / 取消焦点；关闭页面前调用对应通知。
3. 将字体提供器、嵌入许可、缺字策略、全字体大小预算和资源去重纳入生产契约；字段继承的 `/DA`、`/DR`、已有自定义 AP、列表行高与滚动必须在真实文档中检查。
4. 编辑候选使用已有完整重写、原生重开和事务提交底座，失焦提交后必须执行字体外观桥并审计，失败不改变原会话。字段内撤销与文档历史、Dirty / AutoSave、必填验证、读写权限共同实现。
5. 完成 Avalonia 指针 / Tab / Shift+Tab / Esc、选择和剪贴板仲裁、跨页生命周期、辅助技术与 Windows / Linux 实机 IME；候选窗、组合提交 / 取消分别验收。

P0 的同步单页宿主只提供 timer ID 和保持存活的回调，不运行 UI 定时器循环。字体外观桥当前限固定字号、左对齐单行、显式换行和全部选项可见的单选列表；自动换行、滚动、适配字号、旋转和任意现有字段风格尚未实现。原生 API 调用成功不代表物理键盘、IME、外部交互阅读器或页面虚拟化通过。补充平面 Unicode、整形 / RTL、竖排、CFF / TTC / 可变字体及 XFA / JavaScript 不在本轮通过范围。全字体可能明显增大文件，实验数据不能作为官方性能对齐或通用内存预算。
