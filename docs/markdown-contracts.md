# Markdown 公共契约

核验日期：2026-10-14。适用于本仓库 `net10.0`、Avalonia 12.0.3、Markdig 0.42.0 实现。成员、属性默认值及资源名另见 [实际程序集快照](validation/markdown-public-api.txt)。本契约描述本库可依赖的行为；平台支持结果见 [平台验收](markdown-platform-acceptance.md)。

## 文档、坐标与选择

`MarkdownDocumentSnapshot`、`MarkdownBlock`、`MarkdownInline` 是不可变纯数据，不保存 Markdig AST、控件、图像、Brush 或 TextLayout。`Source` 是接收的原始 Markdown；`Text` 是规范化的全文复制投影。所有位置、长度及源码 span 使用 UTF-16；源码位置与逻辑文本位置不得混用。换行在投影中规范化为 LF。

段落之间使用 LF 分隔，列表保留编号/任务标记，表格以 Tab 分隔列、LF 分隔行。跨列/跨行占据的空槽仍参与表格复制；图片投影为 alt 文本；代码保留制表符、行末空格及实际尾部 LF。HTML 默认原文保留并产生 MDHTML 诊断。未知节点原文保留并产生 MDNODE 诊断。

`Selection` 是与已实现视觉对象无关的全文连续范围。任意端点均规范到扩展字素边界，避免拆开代理对、组合符和 Emoji ZWJ。完整 `Text` 替换清除选区；追加、配置更新、换肤、服务切换和节点回收保留或迁移逻辑位置。重复节点身份缺乏可靠证据时保守重建；删除端点优先回退到存活前驱，再尝试后继。

`CopyAsync()` 在 UI 线程捕获投影、触发可取消的 `CopyingToClipboard`，然后调用平台剪贴板。结果区分 `Copied`、`EmptySelection`、`Cancelled`、`ClipboardUnavailable`、`Failed`。`Copy()` 只发起异步操作；需要结果的宿主应等待 `CopyAsync()`。代码复制按钮使用代码原文，不改变全文选区。

## 输入、线程与结束

| 入口 | 固定行为 |
| --- | --- |
| 控件属性、配置、选择、模板与 UI 回调 | UI 线程。转换器只生成纯数据；呈现器与事件在 UI 线程执行 |
| `Text = ...` | 完整替换；立即使旧输入代次失效；外部赋值优先于排队的追加/配置同步 |
| `WhenUpdatedAsync()` | 等待当前登记的更新。普通 Text/配置更新可在未挂载或缺模板时完成模型；未挂载、缺模板、隐藏或跟随尾部布局尚未稳定时返回 `Status=Completed, LayoutPending=true`。显式会话仍等待该会话结束 |
| `BeginStreaming()` | UI 线程创建显式会话；已有活动会话时报错 |
| `session.Append(string/ReadOnlyMemory<char>)` | 可跨线程；按输入锁接收顺序排列，宿主负责业务排序；借用内存在返回前复制；结束后拒绝追加 |
| `AppendText(string, TimeSpan?)` | UI 线程便利入口；与显式会话不得混用；默认合并 33 ms，允许 0–1 s |
| `CompleteAsync()` / `CompleteStreamingAsync()` | 冻结输入并等待最终快照；可见有效模板执行布局，隐藏或合法自定义尾节点尚未形成可见布局时完成且 `LayoutPending=true`。未挂载/缺模板时公开结果为 `Status=Error` 和 `InvalidOperationException`，内部 apply 边界为 NoPresentationEnvironment；不等待远程图片或高亮 |
| `Cancel()` / `CancelStreaming()` | 停止旧工作并保留已经接收的源码；旧快照不再提交，下一会话从保留源码继续；完整替换仍优先 |
| `Dispose()` / `DisposeAsync()` | 请求正常结束；前者不阻塞等待，后者等待相同结束结果 |
| pipeline/解析选项/renderer registry 改变 | 取消旧会话与配置任务，以完整已接收源码重解析，保留逻辑选区；配置构造失败不替换旧解析器 |

调度器最多一个实际解析和一个最新合并请求。不可取消的 Markdig 解析占据解析槽直到返回，结果通过代次检查拒绝。默认源码上限 16 Mi UTF-16 字符；超限明确失败。内部流式同步使用 `SetCurrentValue`，保留绑定；TwoWay 同值回写保持当前会话，宿主重入给出不同完整文本时以宿主赋值为准。

## 图片与高亮服务

根 `ImageLoader`、`CodeHighlighter`、`BaseUri` 是可继承属性。BaseUri 必须为绝对 URI。默认 loader/highlighter 均为 null，由宿主显式注入。实际图片节点为 `MarkdownImage`，实际代码块为 `MarkdownCodeBlock`，可用 Avalonia 样式设置对应服务属性。局部值优先于样式和继承；明确设置 null 会关闭该元素的服务，`ClearValue` 恢复样式/继承。

`ElementServices` 是持久规则，以不可变 `MarkdownElementDescriptor` 决定局部服务。只有 `OverrideImageLoader` / `OverrideCodeHighlighter` 为 true 才写入局部值，因此能够区分“没有覆盖”与“覆盖为 null”。规则在节点重新实现时再次执行。`ElementPrepared` 对实现的块根及独立图片节点，在挂载前供宿主设置其它 UI 属性；不对每个 Span/Run 分别触发。直接改变某个当前视觉对象的值仅作用于该次实现，需要跨回收保留的策略应写在规则或样式里。

loader/highlighter 及其捕获状态归宿主所有；关闭控件不会 Dispose 共享服务。控件只拥有 loader 返回的 `MarkdownImageLease`，并在替换/回收/卸载时释放；迟到租约也释放。`ownsBitmap=false` 表示图像仍由宿主拥有，租约释放不释放图像。局部服务切换取消旧请求，旧成功/失败均不得覆盖新服务；highlighter 的 Invalidated 只通知当前有效订阅，卸载会移除订阅。

`MarkdownImage.Reload()` 可在失败后重试；相对地址需要 BaseUri。`MarkdownCodeBlock.Reload()` 可主动重新着色。无服务、未知语言、失败或不合法高亮 span 均保留代码原文；高亮偏移针对未经修改的 UTF-16 代码。服务错误通过 Error 分类报告，不使文档最终完成等待图片。

非空图片地址与链接使用相同 URI 解析规则：`/path`、`//host/path`、fragment 和相对路径按 BaseUri 解析；显式 URI scheme、Windows 绝对 drive 路径及反斜杠 UNC 保留其绝对含义。没有有效基址的相对地址不推断为本机文件。空图片地址（如 `![alt]()`）保留 alt 文本且不调用 loader，即使设置 BaseUri 也不将空图片地址加载为基址。链接事件先捕获实际目标，未被处理且仍有效的激活按该目标记录已访问状态；切换基址或重新实现后按当前目标计算视觉状态。

外部链接始终由宿主处理 `LinkClicked`，控件不会自动启动浏览器或系统打开器。`MarkdownLinkEventArgs.Handled` 默认为 false；同步事件正常返回后，最终值为 true 会阻止本次控件默认的 `visited` 标记，为 false（包括没有订阅者）则记录捕获的目标。该标记只表示链接已激活，不证明宿主打开成功；true 不撤销先前的访问记录，也不阻止其它已注册事件处理器执行。事件内完整替换文本、重配置或卸载会使旧激活失效，不再向后续文档写入该次默认访问状态。事件处理器异常沿同步调用传播，该次不执行默认标记，宿主应自行处理打开器异常。脚注引用只在控件内定位，不触发 `LinkClicked`，不受该 `Handled` 属性控制。

控件通过后台资源队列调用服务，最多同时执行4个真实调用、等待256个请求；溢出以错误明确失败。宿主若要操作UI须自己调度。替换/配置/卸载递增资源代次并拒绝队列及迟到结果；非合作的已运行服务继续占槽直到真正返回，控件无法强制结束宿主任务。`ExecutingResourceRequestCount` 表示实际占槽，`PendingResourceRequestCount` 表示排队，`ActiveResourceRequestCount` 包含尚未完成UI接收/释放的完整异步请求。取消回调异步执行，不在UI线程等待。

显式 `MarkdownImageLoader` 默认限制编码输入16MiB及解码后32Mi像素。完整且CRC有效的标准PNG IHDR，以及明确的8bit JPEG SOF0 / SOF1 / SOF2帧头，可在原生解码前按未缩放尺寸拒绝超限输入；预检不验证完整图片内容，不自动缩图。未知格式、不完整头、DNL及不明确的帧布局继续交给原生解码，解码后的像素复核与超限bitmap释放仍保留。因此像素限制不是所有格式的解码器分配峰值保证。HTTP(S)、file和avares均使用有界读取；宿主提供的HttpClient归宿主所有。

## 自定义扩展

后台扩展使用 `MarkdownParserOptions.Converters` 中的类型安全 AST 注册。按注册顺序尝试，null 表示交给下一个注册或内置转换。回调应确定、可在后台执行，不保留或修改短命 AST。输出 `MarkdownCustomNode` 仅包含 key、文本、类和不可变 metadata；span、身份和逻辑偏移由本库生成。

转换 context 的 SourceText 是精确源码片段，尚未进行复制投影的 LF 规范化，其长度等于 SourceLength。普通未闭合 fence 的 Markdig span 可能停在 info 首字符；context 会按完整开头行及实际所属代码行补全物理片段，快照与诊断的 span 仍保留 AST 坐标。grid 表格单元格的源码分散在多条物理行、与相邻列交错，无法由一个连续 SourceStart/SourceLength 表示；其中 fence context 保留 AST 范围，不扩展整条表格行。单元格 fence 的 info 使用 Markdig 的语言与参数，内部空白可能规范化；不带入其它列。

`Text=null` 保留内置节点的复制规则、子节点和语义 metadata；未知容器递归转换，未知空节点保留源码。非 null 文本替代该节点内容并成为叶子，空字符串有效；父结构仍可加入列表编号/任务标记、单元格分隔符或块间换行。`Kind=Custom` 表示扩展节点；`SemanticKind` 读取合法 `native-kind`，用于保留内置表格/列表/链接/图片行为。保留 metadata key 不得由输出覆盖，结构属性以内置值为准。转换异常或非法输出产生 MDNODECONV，以该节点原文整体降级。

上述 grid 单元格 fence 在转换失败时保留 MDNODECONV 并使用内置代码投影，保留其所属代码行及其它单元格的独立复制次序；不把短 AST 片段或含相邻列的整条物理行当作完整原文降级。原始完整 Markdown 始终保留在快照 Source。

UI 扩展使用不可变 `MarkdownRendererRegistry`。呈现器返回新的未挂载 Control 或 Inline；块应通过 `CreateDefault()` 或 `CreateText()` 使用原生全局文字几何。块文本助手必须按顺序覆盖节点投影（可省略纯 LF/Tab 分隔），不能改写文字；行内必须保留精确文本，不接受 InlineUIContainer 作为文字映射。缺注册、抛错、返回 null、文字映射不合规均产生 MDRENDER 并使用原生降级。

构造时验证可检查实际视觉子树和尚未套用模板的逻辑Content，支持普通ContentControl/ScrollViewer包装。宿主提供的自定义模板须真正呈现这些文字助手；隐藏或丢弃Content会失去该区域的命中几何，构造时检查不能认证任意宿主模板的最终视觉。`ElementPrepared` 对同一实现对象执行一次，直接返回`CreateDefault()`不会重复触发该对象的服务规则。

块 context 的文字工厂在所属实现释放后拒绝使用。`RegisterDisposable` 只注册当前实现拥有的资源；失败呈现立即释放，正常呈现在回收/卸载时释放。清理前先使 context 失效并摘除资源批次，逐项释放；单项异常以 `Error.Category=RendererResource` 报告，仍清理其余项并保留原呈现失败的 MDRENDER 与原生降级。失败/释放后的 context 拒绝新登记，并立即释放该次传入资源。禁止登记共享 loader/highlighter。快照可安全保存；UI context、视觉节点和 AST 不能作为全局解析缓存。解析诊断在 `Document.Diagnostics`，呈现诊断在 `RenderingDiagnostics`。

## 模板、样式与辅助工具

必需部件是 `PART_ScrollViewer`（ScrollViewer）与 `PART_DocumentPresenter`（MarkdownDocumentPresenter）。默认主题 URI 为 `avares://NT.AvaloniaControl.Markdown.Themes/Themes/Generic.axaml`。功能包独用可提供自有模板；缺部件时普通更新保留模型并标记 LayoutPending，显式流式完成为 Error，内部 apply 边界为 NoPresentationEnvironment。模板外层承担 padding，滚动内容应避免重复减去 padding，确保文档尾部可滚入视口。

底部跟随在同一次提交内按新文档尾部实现范围、校正实际高度并滚到真实底部；相关同步布局均计入 UpdateMeasured。用户滚动、导航或宿主重入改变偏移后，不重放旧跟随。零高度自定义尾节点或尚未稳定的尾部布局不能作为可见尾部证据，返回 LayoutPending，后续恢复仍服从原代次与用户操作优先权。

公共语义节点包括 MarkdownBlockPresenter、MarkdownCodeBlock、MarkdownImage、MarkdownTextPresenter、MarkdownSpan、MarkdownRun。块类/行内类和命名资源可覆盖；派生块主题使用 `:is(...)`。逻辑文档独立于样式，颜色、字体和主题不改写复制文本。布局预算、DPI、字体回退、局部层级样式和阅读锚点按相应验收证据判断。

硬换行在行内布局、命中和选区中占一个 LF 的 UTF-16 位置，包括合法自定义呈现器返回的原生 `LineBreak`；Windows 的 CRLF 不额外占据逻辑位置。首部、连续和尾部硬换行保留空行，重新测量与窄宽重排不截断全文。嵌套 Span/Run 的字体、颜色及运行时样式变化使用当前原生属性；行高、行间距、换行、对齐和裁剪仍遵循控件的布局设置。[硬换行回归](../tests/Markdown.Headless.Tests/MarkdownHardLineBreakLayoutTests.cs)覆盖这些固定边界及 Unicode 的全局位置，不替代设备字体、DPI和物理输入验收。

`MarkdownAutomationPeer` 提供 Document 角色、只读全文 Value、延迟生成的离屏逻辑块/链接/任务节点及链接 Invoke。任务提供只读 checked 值，不能切换源码。C# `MarkdownAutomationTextRange` 绑定快照和输入代次，规范字素范围，旧代次不能再 Select/BringIntoView。Avalonia 12.0.3 没有公开 ITextProvider/ITextRangeProvider；本库不将这个 C# 范围称作原生 UIA TextPattern。真实屏幕阅读器支持按平台记录，不以 Headless provider 测试代替。

`UpdateMeasured` 分别报告后台解析/匹配及纯 DTO 身份索引准备、UI 应用和当前同步布局耗时；不包含物理屏幕扫描。索引预建计入 `ParseElapsedMilliseconds`，不从端到端延迟中扣除；公开 `MarkdownParser.Parse` 的语义不变。弱索引不持有快照或 UI，重复 ID 的异常与旧节点首个匹配规则保留；取消和旧结果仍由原调度器拒绝，UI 重匹配生成新快照时仍走原有索引回退。性能工具使用 CompositionBatch.Rendered 测量合成线程完成，仍与实际显示帧不同。诊断计数、模型测试和任务结束不替代原生性能预算。

已匹配节点的内部复用不增加公共引用相等保证。只有完整身份匹配成功且节点的 source/text 位置、内容、metadata、inline 与递归子节点均相同时，才可复用旧不可变 DTO；重复节点的保守新身份、晚到引用和脚注依赖失效规则保持不变。

文字属性复用保持完整 Typeface、基线对齐和当前样式引用，不修改请求字体、字重、原生回退或逻辑 Run 树。最多32项共享限定于单次布局创建，并在 finally 清空；仅新建、已知原生行内树及精确文字属性类型参与。Arrange 复用已有行内列表时保留原生字体缓存行为，默认属性重新创建；非零行距、派生或自定义类型绕过新增属性合并，既有硬换行适配仍先执行。重新测量、Arrange、UTF-16 命中和选区使用当前原生 TextLayout。[动态字体差分](../tests/Markdown.Headless.Tests/DynamicFontCollectionLayoutTests.cs)通过同键公开替换字体集合对照原生 TextBlock，覆盖重新 Measure 与 Measure 后直接 Arrange；[几何差分](../tests/Markdown.Headless.Tests/TextPropertyReuseDifferentialTests.cs)和[生命周期差分](../tests/Markdown.Headless.Tests/TextPropertyCacheLifecycleTests.cs)另覆盖样式与单次请求边界。EDB5/7B29旧跨布局缓存的失败保留，新产物正式性能仍须独立验收。

纯文本 `MarkdownTextPresenter` 在 Wrap / WrapWithOverflow 下含 CR 或 LF 时，按原始 UTF-16 长度临时生成原生行结束 run，再由 base 创建布局；finally 恢复原状态。`Text`、Inline 树和默认 Wrap 保持原样，CRLF 的源位置仍占2。每次创建重新解析当前字体，不保留到后续 Arrange；LineSpacing、截断和原生宽高约束继续生效。见 [专属换行差分](../tests/Markdown.Headless.Tests/PlainWrappedLineBreakLayoutTests.cs)及[修复验收](markdown-wrap-leading-lf-20261015.md)。

长块全选可按有效视口所交的完整原生文字行缩小选区几何查询，保留边界行的原生矩形并由既有祖先裁剪处理像素。该优化不改变完整TextLayout的塑形、普通文字绘制、命中、光标或复制投影，不等同于块内冷排版虚拟化。每个presenter仅保留弱关联当前TextLayout的数字行索引，最多65,536行；重新测量、换owner或卸载时清理。非零LineSpacing、截断、MaxLines、自定义呈现/未知行内、变换或无法确认的坐标继续完整原生选区路径；几何与回退用例见[视口差分](../tests/Markdown.Headless.Tests/ViewportSelectionGeometryTests.cs)和[回退差分](../tests/Markdown.Headless.Tests/ViewportSelectionFallbackTests.cs)。

布局中存在投影为单个LF、原生宽度为零的空行时，绘制和自动化文字边界改用同一TextLayout的逐行非空逻辑交集及原生GetTextBounds，保留原生x / height / bidi与逐行Y累加，空LF不生成字形矩形。该窄路径修复Avalonia 12.0.3跨行范围累计导致的零长度异常及漏覆盖；复制投影中的空行仍完整保留。判定缓存弱关联当前layout及投影字符串，并核对文字起点和长度，不跨文档持有文字或字体对象。
