# Markdown 使用说明

包版本沿用仓库的 `0.2.0-preview.2`；开发继续按M1–M7验收推进，版本号不代表阶段停止。公共线程/坐标/服务/扩展/模板与辅助工具边界见 [公共契约](markdown-contracts.md)，实际证据见 [状态记录](markdown-status.md)；功能差异见 [官方能力对照](markdown-official-alignment.md)。平台声明只依据实际已验证的组合。

## 项目与主题

功能包为 `NT.AvaloniaControl.Markdown`，默认主题为独立的 `NT.AvaloniaControl.Markdown.Themes`，可选高亮为 `NT.AvaloniaControl.Markdown.TextMate`。功能包不会引用默认主题包或官方商业 Markdown 包。

```csharp
using NT.AvaloniaControl.Markdown.Themes;

// 先加载应用自身的 Avalonia 主题。
Styles.Add(new MarkdownTheme());
```

也可用 `StyleInclude` 加载 `avares://NT.AvaloniaControl.Markdown.Themes/Themes/Generic.axaml`。只引用功能包时，应用可以提供自己的模板；必需部件是名为 `PART_ScrollViewer` 的 `ScrollViewer` 和名为 `PART_DocumentPresenter` 的 `MarkdownDocumentPresenter`。没有有效模板或尚未挂载时，流式完成返回错误，不无限等待布局。

## AXAML 与绑定

```xml
<md:Markdown xmlns:md="clr-namespace:NT.AvaloniaControl.Markdown;assembly=NT.AvaloniaControl.Markdown"
             Text="{Binding Text, Mode=TwoWay}" />
```

正文只读。`Text` 默认为 null，null/空字符串显示空文档。完整替换 `Text` 会使旧会话失效并清除旧选区。流式输入内部通过 `SetCurrentValue` 同步完整源文本，保留既有绑定；TwoWay 同值回写不会取消自身会话。宿主的新完整文本优先，不与旧追加内容拼接。

```xml
<md:Markdown xml:space="preserve"># 标题

内嵌 **Markdown** 正文。
</md:Markdown>
```

内嵌文本通过 `Content` 属性转换映射到 `Text`。一个控件只选择内嵌文本或绑定作为输入来源。最小样例 [Markdown.Usage](../samples/Markdown.Usage/Markdown.Usage.csproj) 提供真实 compiled binding、TwoWay 回写、显式主题、显式 loader、可等待的流式操作与内嵌文本两个页面。

## 选择与复制

```csharp
reader.SelectAll();
string selected = reader.SelectedText;
MarkdownCopyResult result = await reader.CopyAsync();
reader.ClearSelection();
```

`Document` 是不可变纯数据快照；`Document.Source` 为原始 Markdown，`Document.Text` 为阅读顺序的逻辑复制文本。全局选择由 `MarkdownSelection(Anchor, Active)` 表示，UTF-16 范围可跨段落、代码、表格及未呈现区域；选区边界按字素归一化，不拆开代理对、组合字符或 Emoji ZWJ。

`SetSelection(anchor, active)` 可程序设置选区。`SelectionEnabled` 默认 true；关闭会清除选区。鼠标拖选、双击、Shift 扩展、方向/Home/End/PageUp/PageDown 和 Ctrl+A/C 使用同一模型。`CanCopy` 只代表存在有效文本选区；剪贴板可用性及失败通过 `MarkdownCopyResult` 区分。复制前 `CopyingToClipboard` 提供此次捕获的文本并可取消；事件在 UI 线程触发。

复制投影固定为：块间单 LF，软换行为空格、硬换行为 LF，视觉折行不进入文本；表格单元格用 Tab、行用 LF，跨列合并后续列和跨行合并续行均保留空槽，包括行首、行中及行末；有序列表保留起始编号，无序项使用 `- `，任务保留 `[x]`/`[ ]`，提示标题使用 NOTE/TIP/IMPORTANT/WARNING/CAUTION，脚注保留 `[n]`，图片复制 alt 文本，链接复制标签。脚注按首次引用编号并复用重复引用编号；未引用定义、空默认脚注组、反向导航链接及仅含反向链接的生成段落不进入复制文本或产生额外换行。代码保留空白与末尾 LF，CRLF 规范为 LF；代码末尾 LF 加上块分隔 LF 可能形成两次换行。代码复制按钮复制完整代码原文投影。

## 流式会话

```csharp
// 在 UI 线程创建。一个控件最多一个活动会话。
var session = reader.BeginStreaming();

// 可由后台生产者调用。业务上的先后次序由调用方保证。
session.Append("\n\n## 新内容\n");
session.Append("正文。");

var result = await session.CompleteAsync();
if (result.Status == MarkdownUpdateStatus.Error)
    HandleError(result.Error);
```

`AppendText(string, TimeSpan?)` 为 UI 线程便利入口，复用隐式会话；显式会话活动时不混用。`CompleteStreamingAsync()` 等待隐式追加的最终完成。显式会话的 `Append(ReadOnlyMemory<char>)` 在返回前复制输入，调用方可以复用原内存。

输入被合并后后台完整解析，并通过快照身份匹配复用未变化的呈现。活动解析最多一个、最新待处理请求最多一个；持续输入允许已完成前缀单调提交。此策略是增量呈现，没有承诺增量解析或每次追加恒定成本。

`CompleteAsync()` 冻结接收并等待最终提交；可见的有效控件执行最终布局，已挂载但隐藏或合法自定义尾节点尚未形成可见布局时完成并标记 `LayoutPending=true`，完成不等待全部远程图片或高亮。普通 Text/配置更新的 `WhenUpdatedAsync()` 可先完成模型；未挂载、缺模板或隐藏时同样标记 LayoutPending。显式流式结束在未挂载或缺模板时返回错误。取消、卸载或完整替换返回取消结果。`session.Cancel()` / `reader.CancelStreaming()` 保留已接收源文本并停止待提交工作；`Dispose()` 非阻塞请求正常结束，`Completion` 提供可观察结果，`DisposeAsync()` 等待结束。终止后的会话拒绝新输入。

`AutoScrollToEnd` 默认为 false。开启后只有更新前已接近底部时跟随；用户上滚暂停，回到底部或 `ScrollToEnd()` 恢复。字体、宽度及图片高度变化期间，控件优先保存可见文字的逻辑位置和视口偏移；没有可用文字几何时使用块身份与块内偏移回退。

## 图片、高亮与链接

```csharp
// 宿主拥有 loader；使用完控件后由宿主释放。
var loader = new MarkdownImageLoader();
reader.ImageLoader = loader;
reader.BaseUri = new Uri("file:///D:/Help/");

// 可选适配包。
reader.CodeHighlighter = new TextMateCodeHighlighter();

reader.LinkClicked += (_, args) =>
{
    // 外链始终由宿主打开。设为true只是不让控件自动标记visited。
    args.Handled = true;
    OpenInHost(args.Url, args.Title);
};
```

如果需要控件自动记录链接访问状态，让 `Handled` 保持默认 false；即使没有订阅者，控件也只会标记 `visited`，不会打开目标。同步事件返回时的最终 `Handled=true` 阻止本次默认标记，但不会清除以前的访问记录，也不会跳过其它事件处理器。宿主在事件内完整替换文本、重配置或卸载后，旧激活不会把默认访问状态写入后续文档。事件处理器若抛错，异常按同步事件语义传播，本次不标记；需要由宿主在打开器调用周围处理异常。脚注引用保留控件内部定位，不触发外部链接事件。

迁移已有处理器时，如果过去只用 `Handled=true` 表示宿主已打开链接，同时仍需要默认 `visited` 样式，应移除该赋值或改为 false；宿主打开链接不依赖此标志。

主控件默认 `ImageLoader=null`，不自动联网。显式的 `MarkdownImageLoader` 支持 HTTP/HTTPS、文件和 avares，包含超时、取消，默认限制编码输入 16 MiB 和解码后 32 Mi 像素。可明确识别的标准PNG / 8bit JPEG头先按原始尺寸预检，超限时在解码前拒绝；不自动缩图，未知或不完整头仍走原生解码。解码后保留像素复核，因而不保证所有格式的分配峰值。自定义 `IMarkdownImageLoader.LoadAsync` 返回 `MarkdownImageLease`，调用方拥有租约，loader 仍由宿主拥有；`ownsBitmap=false` 的租约保留宿主图像的所有权。缺少 loader 或加载失败保留 alt 文本；`BaseUri` 解析相对链接/图片。服务更换会使相关呈现重新加载，旧结果不能回流。

默认 loader 的原生探针使用 3×2 PNG，验证 file、宿主 avares 及回环 HTTP 的实际解码、租约释放、编码输入限额和解码后像素限额。HTTP 只连接 `127.0.0.1` 动态端口，使用真实无代理的宿主客户端；公网 HTTP/HTTPS 图片与网络环境仍待验证，回环结果不作为公网验收。

`IMarkdownCodeHighlighter` 返回未修改代码的 UTF-16 样式范围。未知语言、缺少服务或失败时显示等宽原文；`Invalidated` 通知重新高亮，选区保持。TextMate 适配支持 `SetTheme(ThemeName)`；统一 Demo 的高亮主题跟随窗口亮暗主题，也提供独立切换按钮。控件不会释放宿主注入的 loader、高亮器或 HTTP 客户端。

单元素覆盖使用持久的 `ElementServices` 规则。规则收到纯数据节点描述，在 UI 线程实现节点时执行；回收后再次实现也会重新应用。以下示例让指定图片使用宿主的局部 loader，并关闭 `private` 代码块的高亮：

```csharp
reader.ElementServices = node =>
{
    if (node.Inline is { SemanticKind: MarkdownInlineKind.Image } image &&
        image.Metadata.GetValueOrDefault("url") == "assets/local.png")
        return new MarkdownServiceOverrides
        {
            OverrideImageLoader = true,
            ImageLoader = localImageLoader // 宿主拥有此服务。
        };

    if (node.Block.SemanticKind == MarkdownBlockKind.Code &&
        node.Block.Metadata.GetValueOrDefault("language") == "private")
        return new MarkdownServiceOverrides
        {
            OverrideCodeHighlighter = true,
            CodeHighlighter = null
        };

    return null;
};
```

只有对应 `Override...` 为 true 才设置局部服务，因此可区分不覆盖和明确覆盖为 null。根服务可继承到 `MarkdownImage` / `MarkdownCodeBlock`，Avalonia 样式也可设置服务；局部值优先，`ClearValue` 恢复样式或继承。`ElementPrepared` 目前覆盖实现的块根与独立图片节点，可修改本次实现的元素 UI 属性；同一元素每次实现只执行一次，不对每个行内 Span/Run 分别触发。需要跨回收保留的策略写入规则或样式。服务实际调用在后台资源队列执行，宿主服务操作 UI 时须自行调度。`MarkdownImage.Reload()` / `MarkdownCodeBlock.Reload()` 可主动重试。

空图片地址（如 `![alt]()`）保留 alt 文本且不调用 loader；设置 BaseUri 也不会把空图片地址加载为基址。外部链接由宿主处理 `LinkClicked`；Demo 明确接入平台 `Launcher`，只自动打开 HTTP/HTTPS。脚注引用在控件内定位。任务状态只读，不回写 Markdown。

## 样式与扩展边界

`MarkdownBlockPresenter` 是实际 Avalonia `Border`，带有 heading/h1–h6、quote、alert/note/tip/important/warning/caution、code、table、tablecell/header、list/listitem 等类。`MarkdownSpan` / `MarkdownRun` 是真实的 `Span` / `Run`，支持 strong/emphasis/strike/insert/mark/subscript/superscript/code/link 与链接 pointerover/visited 类。全局应用样式可用层级选择器调整真实语义节点：

```xml
<Style Selector="md|MarkdownBlockPresenter.quote md|MarkdownSpan.strong">
  <Setter Property="Foreground" Value="Purple" />
</Style>
```

默认主题资源统一使用 `Markdown` 前缀，例如 `MarkdownFontSize`、`MarkdownH1FontSize`、`MarkdownCodeFontFamily`、`MarkdownBlockMargin`、`MarkdownTableCellPadding`、`MarkdownLinkBrush`、`MarkdownSelectionBrush`。覆盖字体或间距会影响布局；换肤与视觉回收不会更改源文本或逻辑选区。节点只为已呈现区域存在，逻辑文本、身份和选区独立于节点存活。已验证预先注册的应用级语义选择器，以及运行后向 reader 的 `Styles` 添加/删除嵌套行内选择器；局部样式变化会重新应用已呈现逻辑节点的样式，字体变化会更新原生文本布局，并保留逻辑选区。其他选择器组合仍须按实际节点层级验证。

`ConfigurePipeline(Action<MarkdownPipelineBuilder>)` 可配置 parser。默认启用常规语法、pipe/grid 表格、任务、提示、脚注、自动链接、Emoji 和强调扩展。启用没有内置转换器的扩展会保留源码文本并产生 `MDNODE` 诊断，不能将 parser 支持直接当作 native renderer 已支持。HTML 默认保留原文并报告 `MDHTML`，不支持任意 HTML/CSS 排版。数学、Mermaid、任意 HTML 布局、编辑/Markdown 写回、分页及打印/PDF/Word 导出留作后续。

## 自定义纯数据节点与原生呈现

转换器与 UI 呈现器分别注册。`MarkdownParserOptions` 捕获 pipeline 配置与不可变的转换器列表，`MarkdownNodeConverterRegistration<TNode>` 按注册顺序匹配 Markdig block/inline 类型；返回 null 表示让下一项或内置转换器处理。源码定义、脚注反向链接和文档根不作为自定义呈现节点。

没有已引用脚注时，默认空 `FootnoteGroup` 被省略；显式匹配该组的转换器仍可声明 renderer 或替代文本，返回 null 则继续省略。该边界不会重复调用转换回调。

```csharp
using System.Collections.Immutable;
using Markdig;
using Markdig.Extensions.CustomContainers;
using NT.AvaloniaControl.Markdown;

var options = new MarkdownParserOptions
{
    ConfigurePipeline = builder => builder.UseCustomContainers(),
    Converters =
    [
        new MarkdownNodeConverterRegistration<CustomContainer>(context =>
            new MarkdownCustomNode("callout")
            {
                StyleClasses = ["callout"],
                Metadata = ImmutableDictionary<string, string>.Empty
                    .Add("caption", context.Node.Info ?? string.Empty)
            })
    ]
};
reader.ConfigureParser(options);
```

转换回调只处理纯数据。`MarkdownNodeConversionContext<TNode>` 提供此次 Markdig AST `Node`、完整 `Source`、精确的 `SourceText` 和 UTF-16 `SourceStart/SourceLength`。AST/context 短命，不能保留或修改；控件在后台解析时执行回调，直接调用 `MarkdownParser.Parse` 则在调用线程同步执行。转换回调不能读取 Avalonia 控件、Brush 或可变 UI 状态。回调捕获的业务状态由宿主负责线程安全和确定性；向 parser 构造函数传入的可变注册列表会被复制，不会在创建之后改变生效列表。

未闭合 fenced code block 的 Markdig AST span 可能在首行 info string 中提前结束。此时 context 的源范围覆盖完整首行及该代码块实际拥有的物理代码行（含原始 CR/LF），`SourceText.Length == SourceLength`；转换器失败也保留这段完整源码，不吞入后续 quote/list 兄弟节点。不可变快照节点和诊断仍保留原始 AST 源范围，因此该边界下它们的 `SourceLength` 可小于 context 的 `SourceLength`。已闭合 fence 和其他节点继续使用 AST 源范围。

表格单元格的源码可能分散在多行的局部列中，无法用一个连续范围恢复且排除相邻单元格。该边界下 fenced code 的 context 保留 AST 范围；转换器失败或输出无效仍报告 `MDNODECONV`，但回退到原生代码行投影，保留代码内容且不复制相邻单元格。此类 fence 的 `info` 元数据使用 Markdig 的 Info 和 Arguments 全部 token 重建，二者之间的空白可规范为一个空格。

`MarkdownCustomNode.Text` 非 null 时作为节点内容的逻辑复制文本，并使该节点成为叶子；父结构仍可添加列表编号、表格 Tab 或块分隔符。例如 `ListItemBlock` 的替代内容为 `summary` 时，列表仍投影为 `- summary`；`TableCell` 的替代内容不会移除父行的 Tab。空字符串是合法的零长度内容投影。Text 为 null 时，内置节点保留原生复制与子节点规则，例如列表编号、表格 Tab、代码空白；未知容器递归转换已有 block/inline 子节点，未知且没有子节点则保留源码文本。快照仍使用 Custom kind，保留的内置类型写入 `native-kind`，值为枚举原名，例如 `List`、`Table`、`Code` 或 `Strong`；`MarkdownBlock.SemanticKind` / `MarkdownInline.SemanticKind` 提供该语义类型。替代文本没有 `native-kind`。内置语义元数据优先于同名自定义 payload，例如列位置、跨列/跨行数、编号与列表 marker。库统一分配子节点、身份、源范围和全局逻辑范围，转换器不能注入任意坐标或 AST 对象。CRLF/CR 在投影中规范为 LF。

返回值只包含 renderer key、字符串文本、样式类与 `ImmutableDictionary<string,string>` 元数据。空元数据值合法，null 值、空/未修剪 key、带空白/控制字符的 renderer key 或单个样式类均拒绝。样式类重复项按顺序去重。`renderer-key`、`style-classes`、`ast-type`、`native-kind` 是本库保留元数据 key，转换器不能自行设置，包括不同大小写形式。

异常或无效输出在写入投影前处理：该节点降级为源码原文（表格内 fence 使用上述原生代码行边界），并报告 `MDNODECONV`；不会保留半份替代文本。诊断不输出回调异常消息，避免回调把正文写入错误文字。没有匹配转换器的未知 AST 继续使用 `MDNODE`。默认内置数学/图表呈现仍未提供；宿主显式注册相应转换与呈现才构成自定义支持。

UI 侧 `MarkdownRendererRegistry` 按同一 renderer key 查找原生 renderer。`MarkdownBlockRenderContext` 提供当前 `Document`、`Block`、`Owner`、`CreateDefault()`、`CreateText()` / 文档范围重载；行内 context 提供 `Inline` 及相同的默认/文本入口。使用这些文本入口保持全局选择几何和原生排版，不在 renderer 中重新定义复制文字或偏移。自定义 inline 必须保留节点的精确逻辑文本；`InlineUIContainer` 不能代替文本映射。本次呈现拥有的可释放资源用 block context 的 `RegisterDisposable` 登记，宿主服务不因此交给控件释放。

`context.CreateText()` 默认保留 Wrap；纯文本的前导、连续和尾部空行由原生行结束 run 适配，保持逻辑文本与选区映射。自定义 renderer 可以直接保留该默认值，见 [Wrap 前导空行修复及测试](markdown-wrap-leading-lf-20261015.md)。

失败块 renderer 的 context 与已释放块实现的 context 拒绝继续创建文字或登记资源。已登记资源在降级或回收前先从实现中移除，再逐项释放；某项 `Dispose` 抛错不跳过后续资源，也不阻止原生降级。原呈现错误保留在 `RenderingDiagnostics`，资源清理错误另由 `Error` 的 `RendererResource` 分类报告。

`TableRow` 可注册原生呈现器：非 null 替代文字显示为跨整行的内容；null 文字配合 `CreateDefault()` 保留单元格、列对齐和跨行/跨列关系，原生行与自定义行可混排。父表格统一协调默认单元格的列宽与行高，替换 `TableCell` 内容也保留 AST 中的物理跨度及列位置。转换器 payload 中错误的跨度值不会覆盖表格结构。模板包装器可使用 `ContentControl` 包装 `CreateText()`；宿主仍须实际呈现该 helper，隐藏文字或使用丢弃 Content 的自定义模板不能获得有效的指针映射。

```csharp
reader.NodeRenderers = new MarkdownRendererRegistry(blocks:
[
    new MarkdownBlockRendererRegistration("callout", context =>
        context.CreateDefault())
]);
```

缺少 UI renderer 时保留本库默认子节点/文本呈现并给出诊断；renderer 异常或非法文本输出也降级。修改 parser/options 或 renderer 注册表时重新解析并使旧配置结果失效，已接收源文本仍由控件调度缓冲管理。转换器、renderer key、样式类及 payload 变化都参与快照比较；同一份文字不意味着旧布局/链接/资源可无条件复用。

新增接口的实际转换/呈现与分块测试结果见 [实施状态](markdown-status.md) 和 [验收矩阵](markdown-acceptance-matrix.md)。单个示例 renderer 的存在不代表任意第三方扩展、数学或 Mermaid 已完成原生验收。

## 示例与生命周期

```powershell
dotnet run --project samples/NT.AvaloniaControl.Demo -c Release -- --page markdown
dotnet run --project samples/Markdown.Usage -c Release
```

统一 Demo 包含基础语法、五类提示、脚注、复杂与宽表、中文/RTL/Emoji、自定义延迟图片与失败、TextMate 切换、跨块选择、每秒 20 chunk 的本地流式输入、10,000 块长文和资源覆盖。页面 `Deactivate()` 停止其生产者、取消拖动及活动流式；`Activate()` 不恢复旧追加；`Dispose()` 释放页面拥有的 loader 与订阅。真实剪贴板、辅助工具、DPI 及 Linux/麒麟仍按平台记录分别验收，Headless 测试不代表这些平台结论。
