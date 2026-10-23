param([switch]$NoPack, [string]$PackageFeed)
$ErrorActionPreference = 'Stop'
$markdownRepoRoot = Split-Path $PSScriptRoot -Parent
$markdownFeed = if ([string]::IsNullOrWhiteSpace($PackageFeed)) { Join-Path $markdownRepoRoot 'artifacts/packages' } else { [IO.Path]::GetFullPath($PackageFeed) }
$markdownProperties = [xml](Get-Content -LiteralPath (Join-Path $markdownRepoRoot 'Directory.Build.props') -Raw)
$markdownPackageVersion = [string]$markdownProperties.Project.PropertyGroup.Version
$markdownVersions = [xml](Get-Content -LiteralPath (Join-Path $markdownRepoRoot 'Directory.Packages.props') -Raw)
$markdownAvaloniaVersion = [string]($markdownVersions.Project.ItemGroup.PackageVersion | Where-Object { $_.Include -eq 'Avalonia' }).Version
if ([string]::IsNullOrWhiteSpace($markdownPackageVersion) -or [string]::IsNullOrWhiteSpace($markdownAvaloniaVersion)) { throw 'Markdown package or Avalonia version is missing.' }
$markdownResults = @()
Push-Location $markdownRepoRoot
try {
    if (-not $NoPack) {
        foreach ($markdownProduct in @('NT.AvaloniaControl.Markdown', 'NT.AvaloniaControl.Markdown.Themes', 'NT.AvaloniaControl.Markdown.TextMate')) {
            dotnet pack "src/$markdownProduct/$markdownProduct.csproj" -c Release --no-build --no-restore -m:1 -o $markdownFeed
            if ($LASTEXITCODE -ne 0) { throw "Markdown package creation failed: $markdownProduct; first build Release." }
        }
    }
    foreach ($markdownProduct in @('NT.AvaloniaControl.Markdown', 'NT.AvaloniaControl.Markdown.Themes', 'NT.AvaloniaControl.Markdown.TextMate')) {
        if (-not (Test-Path -LiteralPath (Join-Path $markdownFeed "$markdownProduct.$markdownPackageVersion.nupkg"))) { throw "Markdown package missing: $markdownProduct" }
    }
    foreach ($markdownMode in @('FunctionOnly', 'DefaultTheme')) {
        $markdownConsumer = Join-Path $markdownRepoRoot ('artifacts/consumer/' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $markdownConsumer -Force | Out-Null
        foreach ($markdownBoundary in @('Directory.Build.props', 'Directory.Packages.props')) {
            Set-Content -LiteralPath (Join-Path $markdownConsumer $markdownBoundary) -Value '<Project />' -Encoding utf8
        }
        $markdownProductReferences = if ($markdownMode -eq 'FunctionOnly') {
            '<PackageReference Include="NT.AvaloniaControl.Markdown" Version="__PRODUCT_VERSION__" />'
        } else {
            '<PackageReference Include="NT.AvaloniaControl.Markdown.Themes" Version="__PRODUCT_VERSION__" /><PackageReference Include="NT.AvaloniaControl.Markdown.TextMate" Version="__PRODUCT_VERSION__" />'
        }
        $markdownDefine = if ($markdownMode -eq 'FunctionOnly') { 'FUNCTION_ONLY' } else { 'DEFAULT_THEME' }
        $markdownProjectText = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
    <DefineConstants>__DEFINE__</DefineConstants>
  </PropertyGroup>
  <ItemGroup>
    __PRODUCT_REFERENCES__
    <PackageReference Include="Avalonia.Headless" Version="__AVALONIA_VERSION__" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="__AVALONIA_VERSION__" />
  </ItemGroup>
</Project>
'@
        $markdownProjectText.Replace('__PRODUCT_REFERENCES__', $markdownProductReferences).Replace('__DEFINE__', $markdownDefine).
            Replace('__PRODUCT_VERSION__', $markdownPackageVersion).Replace('__AVALONIA_VERSION__', $markdownAvaloniaVersion) |
            Set-Content -LiteralPath (Join-Path $markdownConsumer 'Consumer.csproj') -Encoding utf8
        @'
<Window xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:md="clr-namespace:NT.AvaloniaControl.Markdown;assembly=NT.AvaloniaControl.Markdown"
        xmlns:local="clr-namespace:MarkdownPackageConsumer" x:Class="MarkdownPackageConsumer.ConsumerWindow"
        x:DataType="local:ConsumerViewModel" Width="800" Height="400">
  <md:Markdown Name="Reader" Text="{Binding Text, Mode=TwoWay}" />
</Window>
'@ | Set-Content -LiteralPath (Join-Path $markdownConsumer 'Window.axaml') -Encoding utf8
        $markdownInlineAxaml = @'
<Window xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:md="clr-namespace:NT.AvaloniaControl.Markdown;assembly=NT.AvaloniaControl.Markdown"
        x:Class="MarkdownPackageConsumer.__INLINE_CLASS__" Width="800" Height="400">
  <md:Markdown Name="InlineReader" xml:space="preserve"># 内嵌包标题

正文 **强调** 😀
续行  
硬换行

- 第一项
- 第二项</md:Markdown>
</Window>
'@
        # XML normalizes physical CRLF to LF before compiled AXAML assigns [Content] Text.
        # Write both file encodings deliberately; the runtime checks assert that boundary.
        $markdownInlineLf = $markdownInlineAxaml.Replace("`r`n", "`n").Replace("`r", "`n")
        $markdownInlineUtf8 = [Text.UTF8Encoding]::new($false)
        [IO.File]::WriteAllText((Join-Path $markdownConsumer 'InlineLfWindow.axaml'),
            $markdownInlineLf.Replace('__INLINE_CLASS__', 'InlineLfWindow'), $markdownInlineUtf8)
        [IO.File]::WriteAllText((Join-Path $markdownConsumer 'InlineCrlfWindow.axaml'),
            $markdownInlineLf.Replace('__INLINE_CLASS__', 'InlineCrlfWindow').Replace("`n", "`r`n"), $markdownInlineUtf8)
        $markdownStyles = if ($markdownMode -eq 'FunctionOnly') { '' } else { '<StyleInclude Source="avares://NT.AvaloniaControl.Markdown.Themes/Themes/Generic.axaml" />' }
        @'
<Application xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="MarkdownPackageConsumer.ConsumerApp">
  <Application.Styles><FluentTheme />__MARKDOWN_STYLES__</Application.Styles>
</Application>
'@.Replace('__MARKDOWN_STYLES__', $markdownStyles) | Set-Content -LiteralPath (Join-Path $markdownConsumer 'App.axaml') -Encoding utf8
        @'
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using NT.AvaloniaControl.Markdown;
using ReaderControl = NT.AvaloniaControl.Markdown.Markdown;
#if DEFAULT_THEME
using NT.AvaloniaControl.Markdown.TextMate;
using TextMateSharp.Grammars;
#endif

namespace MarkdownPackageConsumer;

internal static class Program
{
    private static readonly Dictionary<string, bool> Checks = [];
    internal static void Check(bool condition, string name)
    { Checks[name] = condition; if (!condition) throw new InvalidOperationException(name); }

    public static void Main(string[] args)
    {
        var passed = false;
        string? failure = null;
        ConsumerWindow? window = null;
        try
        {
            AppBuilder.Configure<ConsumerApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
            var original = "# 包标题\n\n包正文 **强调** 😀\n\n```cs\nvar value = 1;\n  // 包代码\n```\n\n| 包表头 | B |\n|---|---|\n| 包单元格 | C |\n\n" +
                string.Concat(Enumerable.Range(0, 120).Select(index => $"离屏段落 {index:D3}\n\n")) + "包尾部";
            var viewModel = new ConsumerViewModel { Text = original };
            window = new ConsumerWindow { DataContext = viewModel };
            var reader = window.FindControl<ReaderControl>("Reader") ?? throw new InvalidOperationException("AXAML reader missing.");
            var errors = new List<string>();
            reader.Error += (_, error) => errors.Add(error.Category + ": " + error.Error.Message);
#if FUNCTION_ONLY
            reader.Template = new FuncControlTemplate<ReaderControl>((_, scope) =>
            {
                var presenter = new MarkdownDocumentPresenter { Name = "PART_DocumentPresenter" };
                scope.Register("PART_DocumentPresenter", presenter);
                var scroll = new ScrollViewer { Name = "PART_ScrollViewer", Content = presenter };
                scope.Register("PART_ScrollViewer", scroll);
                return scroll;
            });
            Check(!Directory.EnumerateFiles(AppContext.BaseDirectory, "NT.AvaloniaControl.Markdown.Themes*.dll").Any() &&
                !Directory.EnumerateFiles(AppContext.BaseDirectory, "TextMateSharp*.dll").Any(), "功能包独用无主题和高亮程序集");
#else
            var highlighter = new TextMateCodeHighlighter(ThemeName.DarkPlus);
            reader.CodeHighlighter = highlighter;
            var highlights = Pump(highlighter.HighlightAsync("var value = 1;", "cs", CancellationToken.None).AsTask(), window);
            Check(highlights.Count > 0 && highlights.Any(span => span.Foreground is not null), "可选包真实Tokenizer和原生资产加载");
            Check(Directory.EnumerateFiles(AppContext.BaseDirectory, "NT.AvaloniaControl.Markdown.Themes*.dll").Any(), "主题程序集由NuGet加载");
#endif
            window.Show();
            window.UpdateLayout();
            var first = Pump(reader.WhenUpdatedAsync(), window);
            Check(first.Status == MarkdownUpdateStatus.Completed && reader.Text == original && reader.Document.Source == original, "真实AXAML编译绑定和初始提交");
            Check(reader.HasPresentationEnvironment && reader.DocumentPresenter is { RealizedBlockCount: > 0 }, "包内模板资源和呈现部件挂载");
            Check(reader.Document.Text.Contains("包标题", StringComparison.Ordinal) &&
                reader.Document.Text.Contains("  // 包代码", StringComparison.Ordinal) &&
                reader.Document.Text.Contains("包表头\tB", StringComparison.Ordinal), "标题代码空白表格复制投影");
            Check(reader.Document.Blocks.Length > reader.DocumentPresenter!.RealizedBlockCount, "长文包含未呈现块");
            reader.SelectAll();
            Check(reader.CanCopy && reader.SelectedText == reader.Document.Text && reader.SelectedText.EndsWith("包尾部", StringComparison.Ordinal), "全局选区复制含离屏尾部");
            string? copied = null;
            reader.CopyingToClipboard += (_, copying) => { copied = copying.Text; copying.Cancel = true; };
            Check(Pump(reader.CopyAsync(), window) == MarkdownCopyResult.Cancelled && copied == reader.SelectedText, "复制前取消事件携带完整模型文本");
            reader.ClearSelection();
            Check(!reader.CanCopy && reader.SelectedText.Length == 0, "清除选区");
            viewModel.Text = "宿主替换 **成功**";
            Check(Pump(reader.WhenUpdatedAsync(), window).Status == MarkdownUpdateStatus.Completed && reader.Document.Text.Contains("宿主替换 成功", StringComparison.Ordinal), "ViewModel完整替换仍驱动绑定");
            viewModel.Text = string.Empty;
            Pump(reader.WhenUpdatedAsync(), window);
            const string finalSource = "# 流式包消费\n\n正文😀\n\n```cs\nvar final = 1;\n```\n\n[引用][id]\n\n[id]: https://example.test/final\n";
            using var session = reader.BeginStreaming();
            Pump(Task.Run(() =>
            {
                for (var offset = 0; offset < finalSource.Length; offset += 3)
                    session.Append(finalSource.AsMemory(offset, Math.Min(3, finalSource.Length - offset)));
            }), window);
            var completed = Pump(session.CompleteAsync(), window);
            var expected = new MarkdownParser().Parse(finalSource);
            Check(completed.Status == MarkdownUpdateStatus.Completed && !completed.LayoutPending && reader.Text == finalSource && reader.Document.Source == finalSource, "显式跨线程会话等待最终提交和布局");
            Check(reader.Document.Text == expected.Text, "任意分块最终复制投影与完整赋值等价");
            Check(viewModel.Text == finalSource, "流式内部同步保留TwoWay绑定");
            viewModel.Text = "流式后宿主替换";
            Check(Pump(reader.WhenUpdatedAsync(), window).Status == MarkdownUpdateStatus.Completed && reader.Document.Text == "流式后宿主替换", "流式后绑定可继续完整替换");
            reader.SelectAll();
#if DEFAULT_THEME
            var selected = reader.SelectedText;
            highlighter.SetTheme(ThemeName.LightPlus);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Check(reader.SelectedText == selected && reader.Document.Text == selected, "高亮主题失效保留逻辑选择");
#endif
            NewApiChecks.Run(reader, viewModel, window);
            Check(errors.Count == 0, "无模板解析高亮诊断错误");
            Check(!typeof(ReaderControl).Assembly.GetReferencedAssemblies().Any(assembly =>
                assembly.Name is "Avalonia.Controls.Markdown" or "Avalonia.Controls.Documents" or "Avalonia.Controls.RichTextEditor"), "独立实现无商业文档依赖");
            InlineAxamlChecks.Run();
            passed = true;
            Console.WriteLine("PASS: Markdown __MODE__ isolated NuGet consumer, compiled AXAML binding, selection geometry, streaming, element services, custom renderers and automation ranges.");
        }
        catch (Exception error) { failure = error.ToString(); Console.Error.WriteLine(failure); Environment.ExitCode = 1; }
        finally
        {
            window?.Close();
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "consumer-result.json"), JsonSerializer.Serialize(new
            {
                创建时间UTC = DateTimeOffset.UtcNow,
                模式 = "__MODE__",
                包版本 = "__PRODUCT_VERSION__",
                全部通过 = passed,
                检查 = Checks,
                内嵌AXAML检查数 = Checks.Keys.Count(name => name.StartsWith("MD01 内嵌AXAML ", StringComparison.Ordinal)),
                功能程序集 = typeof(ReaderControl).Assembly.FullName,
                错误 = failure,
                说明 = "真实隔离NuGet包/编译AXAML与Headless布局；未验证原生剪贴板、屏幕阅读器或麒麟设备。"
            }, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        }
    }

    internal static T Pump<T>(Task<T> task, Window window)
    {
        Pump((Task)task, window);
        return task.GetAwaiter().GetResult();
    }
    internal static void Pump(Task task, Window window)
    {
        var timeout = Stopwatch.StartNew();
        while (!task.IsCompleted && timeout.Elapsed < TimeSpan.FromSeconds(20))
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Thread.Sleep(1);
        }
        if (!task.IsCompleted) throw new TimeoutException("包消费等待超过20秒。");
        task.GetAwaiter().GetResult();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }
}
public sealed partial class ConsumerApp : Application
{ public override void Initialize() => AvaloniaXamlLoader.Load(this); }
public sealed partial class ConsumerWindow : Window
{ public ConsumerWindow() => AvaloniaXamlLoader.Load(this); }
public sealed partial class InlineLfWindow : Window
{ public InlineLfWindow() => AvaloniaXamlLoader.Load(this); }
public sealed partial class InlineCrlfWindow : Window
{ public InlineCrlfWindow() => AvaloniaXamlLoader.Load(this); }
internal static class InlineAxamlChecks
{
    private const string Source = "# 内嵌包标题\n\n正文 **强调** 😀\n续行  \n硬换行\n\n- 第一项\n- 第二项";
    private const string Projection = "内嵌包标题\n正文 强调 😀 续行\n硬换行\n- 第一项\n- 第二项";

    internal static void Run()
    {
        Verify(new InlineLfWindow(), "LF文件");
        Verify(new InlineCrlfWindow(), "CRLF文件经XML归LF");
    }

    private static void Verify(Window window, string fixture)
    {
        void Check(bool condition, string detail) => Program.Check(condition, "MD01 内嵌AXAML " + fixture + " " + detail);
        try
        {
            var reader = window.FindControl<ReaderControl>("InlineReader") ?? throw new InvalidOperationException("Compiled inline AXAML reader missing.");
            Check(reader.Text is { } rawText && rawText == Source && !rawText.Contains('\r'), "xml:space保留精确内容及LF边界");
            var errors = new List<string>();
            reader.Error += (_, error) => errors.Add(error.Category + ": " + error.Error.Message);
#if FUNCTION_ONLY
            reader.Template = new FuncControlTemplate<ReaderControl>((_, scope) =>
            {
                var presenter = new MarkdownDocumentPresenter { Name = "PART_DocumentPresenter" };
                scope.Register("PART_DocumentPresenter", presenter);
                var scroll = new ScrollViewer { Name = "PART_ScrollViewer", Content = presenter };
                scope.Register("PART_ScrollViewer", scroll);
                return scroll;
            });
#endif
            window.Show();
            window.UpdateLayout();
            var completed = Program.Pump(reader.WhenUpdatedAsync(), window);
            Check(completed.Status == MarkdownUpdateStatus.Completed && !completed.LayoutPending &&
                reader.HasPresentationEnvironment && reader.DocumentPresenter is { RealizedBlockCount: > 0 }, "最终提交和实际呈现环境");
            Check(reader.Document.Source == Source && reader.Document.Source == reader.Text, "ContentText传入完整Source");
            Check(reader.Document.Text == Projection, "完整逻辑投影和换行");
            var blocks = reader.Document.Blocks;
            Check(blocks.Length == 3 && blocks[0].Kind == MarkdownBlockKind.Heading &&
                blocks[0].Metadata.GetValueOrDefault("level") == "1" && blocks[0].Text == "内嵌包标题", "标题结构和文本");
            Check(blocks.Length == 3 && blocks[2].Kind == MarkdownBlockKind.List && blocks[2].Children.Length == 2 &&
                blocks[2].Children.All(item => item.Kind == MarkdownBlockKind.ListItem) &&
                blocks[2].Text == "- 第一项\n- 第二项", "列表结构和复制标记");
            Check(blocks.Length == 3 && blocks[1].Kind == MarkdownBlockKind.Paragraph &&
                blocks[1].Inlines.Where(inline => inline.Kind == MarkdownInlineKind.LineBreak)
                    .Select(inline => inline.Metadata.GetValueOrDefault("hard")).SequenceEqual(new[] { "false", "true" }), "软换行和两个尾空格硬换行");
            reader.SelectAll();
            Check(reader.CanCopy && reader.SelectedText == Projection && !reader.SelectedText.Contains('\r'), "全文选择采用逻辑投影");
            Check(errors.Count == 0 && reader.Document.Diagnostics.IsEmpty && reader.RenderingDiagnostics.Count == 0, "无解析或呈现诊断");
        }
        finally { window.Close(); }
    }
}
public sealed class ConsumerViewModel : INotifyPropertyChanged
{
    private string? _text;
    public string? Text
    {
        get => _text;
        set { if (_text == value) return; _text = value; PropertyChanged?.Invoke(this, new(nameof(Text))); }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}
'@.Replace('__MODE__', $markdownMode).Replace('__PRODUCT_VERSION__', $markdownPackageVersion) |
            Set-Content -LiteralPath (Join-Path $markdownConsumer 'Program.cs') -Encoding utf8
        @'
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using NT.AvaloniaControl.Markdown;
using ReaderControl = NT.AvaloniaControl.Markdown.Markdown;
using static MarkdownPackageConsumer.Program;

namespace MarkdownPackageConsumer;

internal static class NewApiChecks
{
    public static void Run(ReaderControl reader, ConsumerViewModel model, Window window)
    {
        Check(typeof(MarkdownServiceOverrides).Assembly == typeof(ReaderControl).Assembly &&
            typeof(MarkdownAutomationTextRange).Assembly == typeof(ReaderControl).Assembly,
            "新增服务和范围API由功能NuGet程序集提供");
        ValidateServices(reader, model, window);
        ValidateConvertersAndAutomation(reader, model, window);
        ValidateLinksAndRelativeImages(reader, model, window);
    }

    private static void ValidateServices(ReaderControl reader, ConsumerViewModel model, Window window)
    {
        var rootImages = new Loader(); var localImages = new Loader(); var nextImages = new Loader();
        var rootCode = new Highlighter(); var localCode = new Highlighter();
        var originalCode = reader.CodeHighlighter;
        try
        {
            reader.ImageLoader = rootImages;
            reader.CodeHighlighter = rootCode;
            reader.ElementServices = descriptor => descriptor.Inline?.Text switch
            {
                "local" => new MarkdownServiceOverrides { OverrideImageLoader = true, ImageLoader = localImages },
                "disabled" => new MarkdownServiceOverrides { OverrideImageLoader = true, ImageLoader = null },
                _ => descriptor.Block.Metadata.GetValueOrDefault("language") switch
                {
                    "local" => new MarkdownServiceOverrides { OverrideCodeHighlighter = true, CodeHighlighter = localCode },
                    "disabled" => new MarkdownServiceOverrides { OverrideCodeHighlighter = true, CodeHighlighter = null },
                    _ => null
                }
            };
            model.Text = "![local](https://example.test/local) ![disabled](https://example.test/disabled) ![inherited](https://example.test/inherited)\n\n" +
                "```local\nlocal code\n```\n\n```disabled\ndisabled code\n```\n\n```inherited\ninherited code\n```";
            Check(Pump(reader.WhenUpdatedAsync(), window).Status == MarkdownUpdateStatus.Completed, "单元素服务语料正常提交");
            Wait(window, () => localImages.Count == 1 && rootImages.Count == 1 && localCode.Count == 1 && rootCode.Count == 1 && reader.ActiveResourceRequestCount == 0);
            var images = reader.GetVisualDescendants().OfType<MarkdownImage>().ToArray();
            var code = reader.GetVisualDescendants().OfType<MarkdownCodeBlock>().ToArray();
            Check(images.Length == 3 && code.Length == 3, "包内真实图片和代码语义控件已呈现");
            var disabledImage = images.Single(image => image.AltText == "disabled");
            var disabledCode = code.Single(node => node.Language == "disabled");
            Check(ReferenceEquals(images.Single(image => image.AltText == "local").ImageLoader, localImages) &&
                ReferenceEquals(code.Single(node => node.Language == "local").CodeHighlighter, localCode), "单元素服务覆盖控件继承值");
            Check(disabledImage.ImageLoader is null && !disabledImage.IsLoading && disabledCode.CodeHighlighter is null && !disabledCode.IsHighlighting,
                "显式本地null禁用图片和高亮");
            reader.SelectAll(); var projection = reader.SelectedText;
            disabledImage.ClearValue(MarkdownImage.ImageLoaderProperty);
            disabledCode.ClearValue(MarkdownCodeBlock.CodeHighlighterProperty);
            Wait(window, () => rootImages.Count == 2 && rootCode.Count == 2 && reader.ActiveResourceRequestCount == 0);
            Check(ReferenceEquals(disabledImage.ImageLoader, rootImages) && ReferenceEquals(disabledCode.CodeHighlighter, rootCode),
                "清除本地null恢复服务继承");
            reader.ImageLoader = nextImages;
            Wait(window, () => nextImages.Count == 2 && reader.ActiveResourceRequestCount == 0);
            Check(localImages.Count == 1 && ReferenceEquals(images.Single(image => image.AltText == "local").ImageLoader, localImages),
                "根loader切换保留显式本地loader和节点");
            Check(rootImages.Bitmaps.All(Disposed), "服务替换释放本库拥有的旧图片租约");
            Check(reader.SelectedText == projection && reader.SelectedText == reader.Document.Text,
                "单元素服务和继承失效保留全文选择投影");
            var count = localCode.Count;
            localCode.Invalidate();
            Wait(window, () => localCode.Count == count + 1 && reader.ActiveResourceRequestCount == 0);
            Check(reader.SelectedText == projection, "本地高亮失效仍保留逻辑选区");
        }
        finally
        {
            model.Text = string.Empty;
            Pump(reader.WhenUpdatedAsync(), window);
            reader.ElementServices = null;
            reader.ImageLoader = null;
            reader.CodeHighlighter = originalCode;
            Wait(window, () => reader.ActiveResourceRequestCount == 0);
            Check(rootImages.DisposeCount + localImages.DisposeCount + nextImages.DisposeCount + rootCode.DisposeCount + localCode.DisposeCount == 0,
                "替换卸载不释放宿主拥有的共享服务");
            Check(rootCode.Subscribers == 0 && localCode.Subscribers == 0, "语义代码卸载解除服务失效订阅");
            Check(rootImages.Bitmaps.Concat(localImages.Bitmaps).Concat(nextImages.Bitmaps).All(Disposed), "文档移除释放全部返回图片租约");
            rootImages.Dispose(); localImages.Dispose(); nextImages.Dispose(); rootCode.Dispose(); localCode.Dispose();
        }
    }

    private static void ValidateConvertersAndAutomation(ReaderControl reader, ConsumerViewModel model, Window window)
    {
        var resources = new List<Resource>();
        MarkdownBlockRenderContext? firstContext = null;
        var inlineCalls = 0;
        var options = new MarkdownParserOptions
        {
            ConfigurePipeline = _ => { },
            Converters =
            [
                new MarkdownNodeConverterRegistration<ParagraphBlock>(context => context.SourceText.StartsWith("Custom block", StringComparison.Ordinal)
                    ? new MarkdownCustomNode("consumer-card") { StyleClasses = ["consumer-card"], Metadata = ImmutableDictionary<string, string>.Empty.Add("consumer-kind", "block") } : null),
                new MarkdownNodeConverterRegistration<HeadingBlock>(_ => new MarkdownCustomNode("consumer-heading")),
                new MarkdownNodeConverterRegistration<EmphasisInline>(_ => new MarkdownCustomNode("consumer-emphasis")
                    { StyleClasses = ["consumer-emphasis"], Metadata = ImmutableDictionary<string, string>.Empty.Add("consumer-kind", "inline") }),
                new MarkdownNodeConverterRegistration<LinkInline>(_ => new MarkdownCustomNode("consumer-link"))
            ]
        };
        reader.ConfigureParser(options);
        reader.NodeRenderers = new MarkdownRendererRegistry(
            blocks:
            [
                new MarkdownBlockRendererRegistration("consumer-card", context =>
                {
                    firstContext ??= context;
                    var resource = new Resource(); resources.Add(resource); context.RegisterDisposable(resource);
                    return new Border { Padding = new Thickness(8), Child = context.CreateText(), Background = Brushes.AliceBlue };
                }),
                new MarkdownBlockRendererRegistration("consumer-heading", context => context.CreateDefault())
            ],
            inlines:
            [
                new MarkdownInlineRendererRegistration("consumer-emphasis", context =>
                { inlineCalls++; return new Span { FontWeight = FontWeight.Bold, Inlines = { context.CreateText() } }; }),
                new MarkdownInlineRendererRegistration("consumer-link", context => context.CreateDefault())
            ]);
        const string source = "## semantic heading\n\nCustom block 中😀 e\u0301\n\nA *custom inline* and [consumer link](https://example.test/consumer)";
        try
        {
            model.Text = source;
            Check(Pump(reader.WhenUpdatedAsync(), window).Status == MarkdownUpdateStatus.Completed, "ConfigureParser与自定义呈现器可独立包编译运行");
            Check(reader.Document.Text == new MarkdownParser().Parse(source).Text && inlineCalls > 0 && firstContext is not null,
                "自定义block和inline保留原生复制投影");
            var card = reader.Document.Blocks.Single(block => block.Metadata.GetValueOrDefault(MarkdownCustomNodeMetadata.RendererKey) == "consumer-card");
            Check(card.Kind == MarkdownBlockKind.Custom && card.SemanticKind == MarkdownBlockKind.Paragraph &&
                card.Metadata[MarkdownCustomNodeMetadata.NativeKind] == nameof(MarkdownBlockKind.Paragraph) && card.Metadata["consumer-kind"] == "block",
                "nullText自定义节点保留SemanticKind与保留元数据");
            Check(reader.Document.Descendants().SelectMany(block => Flatten(block.Inlines)).Any(inline =>
                inline.Kind == MarkdownInlineKind.Custom && inline.SemanticKind == MarkdownInlineKind.Emphasis && inline.Metadata.GetValueOrDefault("consumer-kind") == "inline"),
                "自定义inline原生语义与宿主元数据进入不可变快照");
            var text = reader.GetVisualDescendants().OfType<MarkdownTextPresenter>().Single(text => text.TextStart == card.TextStart && text.TextLength == card.TextLength);
            Check(text.TextLayout.Height > 0 && text.Bounds.Height > 0, "CreateText原生布局helper具有实际几何");
            var start = PointAt(text, window, 0); var end = PointAt(text, window, text.TextLength);
            window.MouseDown(start, MouseButton.Left); window.MouseMove(end, RawInputModifiers.LeftMouseButton); window.MouseUp(end, MouseButton.Left);
            Check(reader.SelectedText == card.Text && reader.Selection.Start == card.TextStart && reader.Selection.End == card.TextStart + card.TextLength,
                "自定义helper真实指针几何映射到全局选区");
            var peer = (MarkdownAutomationPeer)ControlAutomationPeer.CreatePeerForElement(reader)!;
            var value = peer.GetProvider<IValueProvider>()!;
            Check(value.IsReadOnly && value.Value == reader.Document.Text && Invalid(() => value.SetValue("write")),
                "包内automation全文readonlyValue拒绝写入");
            Check(peer.GetChildren()[0].GetHeadingLevel() == 2, "自定义Heading保留automation语义");
            var emojiStart = card.TextStart + card.Text.IndexOf("😀", StringComparison.Ordinal);
            var range = peer.GetTextRange(emojiStart + 1, 1);
            range.Select();
            Check(range.Text == "😀" && reader.SelectedText == "😀" && peer.GetSelectionRange().Text == "😀",
                "CSharp范围使用UTF16并保留完整字素和选区");
            Check(typeof(IValueProvider).Assembly.GetType("Avalonia.Automation.Provider.ITextProvider") is null && peer.GetProvider<ISelectionProvider>() is null,
                "未把自有范围冒充平台TextPattern或列表SelectionPattern");
            var links = Descendants(peer).Where(node => node.GetAutomationControlType() == AutomationControlType.Hyperlink).ToArray();
            string? activated = null;
            EventHandler<MarkdownLinkEventArgs> handler = (_, args) => { activated = args.Url; args.Handled = true; };
            reader.LinkClicked += handler;
            try { links.Single().GetProvider<IInvokeProvider>()!.Invoke(); }
            finally { reader.LinkClicked -= handler; }
            Check(activated == "https://example.test/consumer", "自定义Link原生语义保留宿主Invoke");
            using var session = reader.BeginStreaming(); session.Append("\n\naccepted tail");
            reader.ConfigureParser(options);
            Check(Pump(session.Completion, window).Status == MarkdownUpdateStatus.Cancelled, "重新配置取消旧显式会话");
            Pump(reader.WhenUpdatedAsync(), window);
            Check(reader.Text == source + "\n\naccepted tail" && reader.Document.Source == reader.Text && model.Text == reader.Text,
                "重新配置保留未应用流式尾部和TwoWay绑定");
            Check(!range.IsCurrent && Invalid(range.Select), "文档代次变化拒绝旧CSharp范围");
            Check(reader.RenderingDiagnostics.Count == 0, "自定义呈现器已注册且无降级诊断");
        }
        finally
        {
            model.Text = string.Empty;
            Pump(reader.WhenUpdatedAsync(), window);
            Check(resources.Count > 0 && resources.All(resource => resource.DisposeCount == 1), "自定义呈现生命周期资源恰好释放一次");
            Check(firstContext is not null && DisposedAction(() => firstContext.CreateText()), "过期rendererContext不能继续创建视觉helper");
            reader.NodeRenderers = MarkdownRendererRegistry.Empty;
            reader.ConfigureParser(new MarkdownParserOptions());
            Pump(reader.WhenUpdatedAsync(), window);
        }
    }

    private static Point PointAt(MarkdownTextPresenter text, Window window, int position)
    {
        var rectangle = text.TextLayout.HitTestTextPosition(position);
        return text.TranslatePoint(new Point(rectangle.X + text.Padding.Left + .1, rectangle.Y + rectangle.Height / 2 + text.Padding.Top), window)!.Value;
    }

    private static void ValidateLinksAndRelativeImages(ReaderControl reader, ConsumerViewModel model, Window window)
    {
        const string relative = "guide.md#part";
        const string absolute = "https://example.test/one/guide.md#part";
        var activated = new List<string>();
        var changeBaseInHandler = false;
        var handleActivation = false;
        EventHandler<MarkdownLinkEventArgs> handler = (_, args) =>
        {
            activated.Add(args.Url); args.Handled = handleActivation;
            if (changeBaseInHandler) reader.BaseUri = new Uri("https://example.test/reentrant-new/");
        };
        reader.LinkClicked += handler;
        var images = new Loader();
        try
        {
            reader.BaseUri = new Uri("https://example.test/one/");
            model.Text = $"[relative]({relative}) [absolute]({absolute}) [root](/root/readme.md) [authority](//cdn.example.test/readme.md)";
            Pump(reader.WhenUpdatedAsync(), window);
            Check(LinkSpans(reader).Length == 4 && LinkSpans(reader).All(span => !span.Classes.Contains("visited")), "新链接的visited初始状态与原生span存在");
            reader.SetSelection(0, 5); var selection = reader.Selection;
            handleActivation = true;
            Invoke(reader, "relative");
            Check(activated.Single() == absolute && reader.Selection == selection &&
                LinkSpans(reader).All(span => !span.Classes.Contains("visited")), "Handled为true仍传递解析目标但不新增visited或改变选区");
            activated.Clear(); handleActivation = false;
            Invoke(reader, "relative");
            Check(activated.Single() == absolute && reader.Selection == selection, "BaseUri解析相对链接且Invoke不改变选区");
            Check(LinkSpans(reader).Where(span => span.Url is relative or absolute).All(span => span.Classes.Contains("visited")), "相对和等价绝对URL共享visited");
            handleActivation = true;
            Invoke(reader, "relative");
            Check(activated[^1] == absolute && reader.Selection == selection &&
                LinkSpans(reader).Where(span => span.Url is relative or absolute).All(span => span.Classes.Contains("visited")), "Handled为true不会清除相对和等价绝对URL已有visited");
            handleActivation = false;
            reader.BaseUri = new Uri("https://example.test/two/");
            Check(!LinkSpans(reader).Single(span => span.Url == relative).Classes.Contains("visited") &&
                LinkSpans(reader).Single(span => span.Url == absolute).Classes.Contains("visited"), "BaseUri改变只刷新实际目标改变的visited状态");
            Invoke(reader, "relative");
            Check(activated[^1] == "https://example.test/two/guide.md#part", "链接在新BaseUri下激活新绝对目标");
            reader.BaseUri = new Uri("https://example.test/one/");
            Check(LinkSpans(reader).Single(span => span.Url == relative).Classes.Contains("visited"), "BaseUri切回恢复历史visited");
            Invoke(reader, "root"); Invoke(reader, "authority");
            Check(activated[^2] == "https://example.test/root/readme.md" && activated[^1] == "https://cdn.example.test/readme.md", "网页root和authority链接按基址解析而非隐式fileURI");
            reader.BaseUri = new Uri("https://example.test/reentrant-old/");
            changeBaseInHandler = true; Invoke(reader, "relative"); changeBaseInHandler = false;
            Check(activated[^1] == "https://example.test/reentrant-old/guide.md#part" &&
                !LinkSpans(reader).Single(span => span.Url == relative).Classes.Contains("visited"), "宿主事件重入换基址不会误标新目标visited");
            reader.BaseUri = new Uri("https://example.test/reentrant-old/");
            Check(LinkSpans(reader).Single(span => span.Url == relative).Classes.Contains("visited"), "visited记录事件前捕获的实际目标");
            reader.BaseUri = new Uri("https://example.test/one/");
            using (var session = reader.BeginStreaming())
            {
                session.Append("\n\nappended after links");
                Pump(session.CompleteAsync(), window);
            }
            Check(LinkSpans(reader).Where(span => span.Url is relative or absolute).All(span => span.Classes.Contains("visited")), "追加后的原生链接保留resolved目标历史");

            reader.ImageLoader = images;
            model.Text = "![root](/root/picture.png) ![authority](//cdn.example.test/picture.png)";
            Pump(reader.WhenUpdatedAsync(), window);
            Wait(window, () => images.Count == 2 && reader.ActiveResourceRequestCount == 0);
            Check(images.Uris.Select(uri => uri.AbsoluteUri).Order().SequenceEqual(new[]
                { "https://cdn.example.test/picture.png", "https://example.test/root/picture.png" }.Order()) && images.Uris.All(uri => !uri.IsFile),
                "原生图片节点继承网页BaseUri的root和authority解析");
            var nodes = reader.GetVisualDescendants().OfType<MarkdownImage>().ToArray();
            reader.SelectAll(); var projection = reader.SelectedText;
            reader.BaseUri = new Uri("http://next.test/other/");
            Wait(window, () => images.Count == 4 && reader.ActiveResourceRequestCount == 0);
            Check(images.Uris.Select(uri => uri.AbsoluteUri).Contains("http://next.test/root/picture.png") &&
                images.Uris.Select(uri => uri.AbsoluteUri).Contains("http://cdn.example.test/picture.png") &&
                reader.GetVisualDescendants().OfType<MarkdownImage>().All(node => nodes.Contains(node)) && reader.SelectedText == projection,
                "图片在BaseUri切换后保留节点及选区并重载当前目标");
        }
        finally
        {
            reader.LinkClicked -= handler;
            model.Text = string.Empty; Pump(reader.WhenUpdatedAsync(), window);
            reader.ImageLoader = null; reader.BaseUri = null;
            Wait(window, () => reader.ActiveResourceRequestCount == 0);
            Check(images.DisposeCount == 0 && images.Bitmaps.All(Disposed), "相对图片服务仍归宿主且全部图像租约已释放");
            images.Dispose();
        }
    }

    private static MarkdownSpan[] LinkSpans(ReaderControl reader) => reader.GetVisualDescendants().OfType<MarkdownTextPresenter>()
        .SelectMany(text => text.GetLogicalDescendants().OfType<MarkdownSpan>()).Where(span => span.Url is not null).ToArray();
    private static void Invoke(ReaderControl reader, string name) => Descendants(ControlAutomationPeer.CreatePeerForElement(reader)!)
        .Single(peer => peer.GetName() == name && peer.GetProvider<IInvokeProvider>() is not null).GetProvider<IInvokeProvider>()!.Invoke();
    private static void Wait(Window window, Func<bool> condition)
    {
        var timeout = Stopwatch.StartNew();
        do
        {
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            if (condition()) return;
            Thread.Sleep(1);
        } while (timeout.Elapsed < TimeSpan.FromSeconds(20));
        throw new TimeoutException("新增包消费验证等待超过20秒。");
    }
    private static bool Invalid(Action action) { try { action(); return false; } catch (InvalidOperationException) { return true; } }
    private static bool DisposedAction(Action action) { try { action(); return false; } catch (ObjectDisposedException) { return true; } }
    private static bool Disposed(WriteableBitmap bitmap) { try { using var locked = bitmap.Lock(); return false; } catch (Exception) { return true; } }
    private static IEnumerable<MarkdownInline> Flatten(IEnumerable<MarkdownInline> inlines)
    { foreach (var inline in inlines) { yield return inline; foreach (var nested in Flatten(inline.Children)) yield return nested; } }
    private static IEnumerable<AutomationPeer> Descendants(AutomationPeer peer)
    { foreach (var child in peer.GetChildren()) { yield return child; foreach (var nested in Descendants(child)) yield return nested; } }

    private sealed class Resource : IDisposable { public int DisposeCount { get; private set; } public void Dispose() => DisposeCount++; }
    private sealed class Loader : IMarkdownImageLoader, IDisposable
    {
        private readonly ConcurrentQueue<WriteableBitmap> _bitmaps = new();
        public ConcurrentQueue<Uri> Uris { get; } = new();
        public int Count => _bitmaps.Count;
        public IEnumerable<WriteableBitmap> Bitmaps => _bitmaps.ToArray();
        public int DisposeCount { get; private set; }
        public ValueTask<MarkdownImageLease> LoadAsync(Uri uri, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Uris.Enqueue(uri);
            // Keep this service fixture inside the consumer viewport after image Stretch;
            // all three code nodes must remain realized for local/null/inherited service checks.
            var bitmap = new WriteableBitmap(new PixelSize(64, 4), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            _bitmaps.Enqueue(bitmap);
            return ValueTask.FromResult(new MarkdownImageLease(bitmap));
        }
        public void Dispose() => DisposeCount++;
    }
    private sealed class Highlighter : IMarkdownCodeHighlighter, IDisposable
    {
        private int _count;
        private EventHandler? _invalidated;
        public int Count => Volatile.Read(ref _count);
        public int Subscribers { get; private set; }
        public int DisposeCount { get; private set; }
        public event EventHandler? Invalidated { add { _invalidated += value; Subscribers++; } remove { _invalidated -= value; Subscribers--; } }
        public ValueTask<IReadOnlyList<MarkdownHighlightSpan>> HighlightAsync(string code, string? language, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); Interlocked.Increment(ref _count);
            return ValueTask.FromResult<IReadOnlyList<MarkdownHighlightSpan>>([new(0, code.Length, "#008000")]);
        }
        public void Invalidate() => _invalidated?.Invoke(this, EventArgs.Empty);
        public void Dispose() => DisposeCount++;
    }
}
'@ | Set-Content -LiteralPath (Join-Path $markdownConsumer 'NewApiChecks.cs') -Encoding utf8
        $markdownCache = Join-Path $markdownConsumer '.packages'
        $markdownConfig = Join-Path $markdownConsumer 'NuGet.Config'
        $markdownEscapedFeed = [System.Security.SecurityElement]::Escape($markdownFeed)
        "<configuration><packageSources><clear /><add key=`"local-preview`" value=`"$markdownEscapedFeed`" /><add key=`"nuget.org`" value=`"https://api.nuget.org/v3/index.json`" /></packageSources></configuration>" |
            Set-Content -LiteralPath $markdownConfig -Encoding utf8
        $markdownProject = Join-Path $markdownConsumer 'Consumer.csproj'
        dotnet restore $markdownProject --packages $markdownCache --configfile $markdownConfig --disable-parallel
        if ($LASTEXITCODE -ne 0) { throw "Markdown $markdownMode independent restore failed." }
        dotnet restore $markdownProject --packages $markdownCache --configfile $markdownConfig --locked-mode
        if ($LASTEXITCODE -ne 0) { throw "Markdown $markdownMode independent locked restore failed." }
        $markdownAssets = Get-Content -LiteralPath (Join-Path $markdownConsumer 'obj/project.assets.json') -Raw | ConvertFrom-Json
        $markdownAssetNames = @($markdownAssets.libraries.PSObject.Properties.Name)
        if ($markdownMode -eq 'FunctionOnly' -and @($markdownAssetNames | Where-Object { $_ -match '^NT\.AvaloniaControl\.Markdown\.(Themes|TextMate)/|^TextMateSharp' }).Count -gt 0) {
            throw 'Function-only Markdown consumer unexpectedly restored theme or highlighter packages.'
        }
        if (@($markdownAssets.libraries.PSObject.Properties | Where-Object { $_.Value.type -eq 'project' }).Count -gt 0) {
            throw 'Independent Markdown consumer resolved a source project reference.'
        }
        dotnet build $markdownProject -c Release --no-restore -m:1
        if ($LASTEXITCODE -ne 0) { throw "Markdown $markdownMode independent consumer build failed." }
        # The in-process Pump keeps its 20-second contract, but cannot interrupt
        # a single non-returning RunJobs/UpdateLayout. Bound the runtime process
        # separately from restore/build and preserve its original result/streams.
        $markdownConsumerOutput = Join-Path $markdownConsumer 'bin/Release/net10.0'
        $markdownConsumerAssembly = [IO.Path]::GetFullPath((Join-Path $markdownConsumerOutput 'Consumer.dll'))
        $markdownConsumerDirectory = [IO.Path]::GetFullPath($markdownConsumer).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
        $markdownPathComparison = if ([OperatingSystem]::IsWindows()) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
        if (-not $markdownConsumerAssembly.StartsWith($markdownConsumerDirectory + [IO.Path]::DirectorySeparatorChar, $markdownPathComparison) -or
            -not (Test-Path -LiteralPath $markdownConsumerAssembly)) { throw 'Compiled consumer is outside its isolated directory or missing.' }
        $markdownHostCommand = Get-Command dotnet -CommandType Application -ErrorAction Stop | Select-Object -First 1
        $markdownHostFile = [IO.FileInfo]::new([IO.Path]::GetFullPath($markdownHostCommand.Source))
        $markdownHostTarget = $markdownHostFile.ResolveLinkTarget($true)
        $markdownExpectedHost = if ($markdownHostTarget) { $markdownHostTarget.FullName } else { $markdownHostFile.FullName }
        $markdownRuntimeStart = [DateTimeOffset]::UtcNow
        $markdownRuntimeTimedOut = $false
        $markdownRuntimeError = $null
        $markdownConsumerExit = $null
        $markdownRuntimePid = $null
        $markdownProcessStartUtc = $null
        $markdownRuntimeStopped = $false
        $markdownCapturedOutput = ''
        $markdownCapturedError = ''
        $markdownOutputTask = $null
        $markdownErrorTask = $null
        $markdownSupervisorPath = Join-Path $markdownConsumer 'supervisor-result.json'
        $markdownStdoutPath = Join-Path $markdownConsumer 'runtime-stdout.log'
        $markdownStderrPath = Join-Path $markdownConsumer 'runtime-stderr.log'
        $markdownRuntimeHashes = foreach ($markdownProduct in @('NT.AvaloniaControl.Markdown', 'NT.AvaloniaControl.Markdown.Themes', 'NT.AvaloniaControl.Markdown.TextMate')) {
            $markdownRuntimeDll = Join-Path $markdownConsumerOutput "$markdownProduct.dll"
            [ordered]@{
                package = $markdownProduct
                packageSha256 = (Get-FileHash -LiteralPath (Join-Path $markdownFeed "$markdownProduct.$markdownPackageVersion.nupkg") -Algorithm SHA256).Hash
                runtimeDllSha256 = if (Test-Path -LiteralPath $markdownRuntimeDll) { (Get-FileHash -LiteralPath $markdownRuntimeDll -Algorithm SHA256).Hash } else { $null }
            }
        }
        $markdownRuntimeSources = foreach ($markdownRuntimeSource in @('Consumer.csproj', 'Program.cs', 'NewApiChecks.cs', 'App.axaml', 'Window.axaml', 'InlineLfWindow.axaml', 'InlineCrlfWindow.axaml', 'Directory.Build.props', 'Directory.Packages.props', 'NuGet.Config', 'packages.lock.json')) {
            [ordered]@{ file = $markdownRuntimeSource; sha256 = (Get-FileHash -LiteralPath (Join-Path $markdownConsumer $markdownRuntimeSource) -Algorithm SHA256).Hash }
        }
        $markdownRuntimeInfo = [Diagnostics.ProcessStartInfo]::new()
        $markdownRuntimeInfo.FileName = $markdownHostFile.FullName
        $markdownRuntimeInfo.ArgumentList.Add($markdownConsumerAssembly)
        $markdownRuntimeInfo.WorkingDirectory = $markdownConsumerOutput
        $markdownRuntimeInfo.UseShellExecute = $false
        $markdownRuntimeInfo.CreateNoWindow = $true
        $markdownRuntimeInfo.RedirectStandardOutput = $true
        $markdownRuntimeInfo.RedirectStandardError = $true
        $markdownRuntimeInfo.StandardOutputEncoding = [Text.UTF8Encoding]::new($false)
        $markdownRuntimeInfo.StandardErrorEncoding = [Text.UTF8Encoding]::new($false)
        $markdownRuntimeProcess = [Diagnostics.Process]::new()
        $markdownRuntimeProcess.StartInfo = $markdownRuntimeInfo
        try {
            if (-not $markdownRuntimeProcess.Start()) { throw 'Isolated consumer process did not start.' }
            $markdownRuntimePid = $markdownRuntimeProcess.Id
            $markdownOutputTask = $markdownRuntimeProcess.StandardOutput.ReadToEndAsync()
            $markdownErrorTask = $markdownRuntimeProcess.StandardError.ReadToEndAsync()
            $markdownProcessStartUtc = $markdownRuntimeProcess.StartTime.ToUniversalTime()
            if (-not $markdownRuntimeProcess.WaitForExit(60000)) {
                $markdownRuntimeTimedOut = $true
                $markdownRuntimeProcess.Refresh()
                if (-not $markdownRuntimeProcess.HasExited) {
                    $markdownActualHostFile = [IO.FileInfo]::new([IO.Path]::GetFullPath($markdownRuntimeProcess.MainModule.FileName))
                    $markdownActualHostTarget = $markdownActualHostFile.ResolveLinkTarget($true)
                    $markdownActualHost = if ($markdownActualHostTarget) { $markdownActualHostTarget.FullName } else { $markdownActualHostFile.FullName }
                    if ($markdownRuntimeProcess.StartTime.ToUniversalTime() -ne $markdownProcessStartUtc -or
                        -not [string]::Equals($markdownActualHost, $markdownExpectedHost, $markdownPathComparison)) {
                        throw 'Refusing to stop a process whose start time or executable differs from this isolated consumer.'
                    }
                    # Kill only the held process created above; never enumerate or
                    # terminate other dotnet processes, and never retry this sample.
                    $markdownRuntimeProcess.Kill()
                    $markdownRuntimeStopped = $true
                    if (-not $markdownRuntimeProcess.WaitForExit(5000)) { throw 'Timed-out isolated consumer did not exit after termination.' }
                }
            }
            $markdownConsumerExit = $markdownRuntimeProcess.ExitCode
        }
        catch { $markdownRuntimeError = $_.Exception.ToString() }
        finally {
            try {
                if ($markdownOutputTask -and $markdownErrorTask) {
                    $markdownStreamTasks = [Threading.Tasks.Task[]]@($markdownOutputTask, $markdownErrorTask)
                    if (-not [Threading.Tasks.Task]::WhenAll($markdownStreamTasks).Wait(5000)) {
                        throw 'Isolated consumer output streams did not close after process completion.'
                    }
                    $markdownCapturedOutput = $markdownOutputTask.GetAwaiter().GetResult()
                    $markdownCapturedError = $markdownErrorTask.GetAwaiter().GetResult()
                }
            }
            catch {
                $markdownRuntimeError = if ($markdownRuntimeError) { $markdownRuntimeError + [Environment]::NewLine + $_.Exception.ToString() } else { $_.Exception.ToString() }
            }
            $markdownRuntimeUtf8 = [Text.UTF8Encoding]::new($false)
            [IO.File]::WriteAllText($markdownStdoutPath, $markdownCapturedOutput, $markdownRuntimeUtf8)
            [IO.File]::WriteAllText($markdownStderrPath, $markdownCapturedError, $markdownRuntimeUtf8)
            [ordered]@{
                mode = $markdownMode; startedUtc = $markdownRuntimeStart; completedUtc = [DateTimeOffset]::UtcNow
                runtimeTimeoutSeconds = 60; timedOut = $markdownRuntimeTimedOut; stoppedOwnedProcess = $markdownRuntimeStopped
                processId = $markdownRuntimePid; processStartUtc = $markdownProcessStartUtc
                executable = $markdownHostFile.FullName; resolvedExecutable = $markdownExpectedHost; consumerAssembly = $markdownConsumerAssembly
                consumerAssemblySha256 = (Get-FileHash -LiteralPath $markdownConsumerAssembly -Algorithm SHA256).Hash
                exitCode = $markdownConsumerExit; error = $markdownRuntimeError
                consumerResultPresent = Test-Path -LiteralPath (Join-Path $markdownConsumerOutput 'consumer-result.json')
                stdout = $markdownStdoutPath; stderr = $markdownStderrPath
                packagesAndRuntimeDlls = @($markdownRuntimeHashes); sourceFiles = @($markdownRuntimeSources)
                nativePerformanceIncluded = $false
            } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $markdownSupervisorPath -Encoding utf8
            $markdownRuntimeProcess.Dispose()
        }
        if ($markdownCapturedOutput) { Write-Output $markdownCapturedOutput.TrimEnd() }
        if ($markdownRuntimeTimedOut -or $markdownRuntimeError) {
            throw "Markdown $markdownMode isolated consumer failed; timeout=$markdownRuntimeTimedOut. Evidence: $markdownSupervisorPath. $markdownRuntimeError"
        }
        $markdownResultPath = Join-Path $markdownConsumer 'bin/Release/net10.0/consumer-result.json'
        if (-not (Test-Path -LiteralPath $markdownResultPath)) { throw "Markdown $markdownMode produced no result; exit=$markdownConsumerExit. Evidence: $markdownSupervisorPath" }
        Copy-Item -LiteralPath $markdownResultPath -Destination (Join-Path $markdownConsumer 'result.json')
        $markdownResult = Get-Content -LiteralPath $markdownResultPath -Raw | ConvertFrom-Json
        $markdownFailedChecks = @($markdownResult.检查.PSObject.Properties | Where-Object { $_.Value -ne $true })
        if ($markdownConsumerExit -ne 0 -or $markdownResult.全部通过 -ne $true -or $markdownFailedChecks.Count -gt 0 -or $markdownResult.错误) {
            throw "Markdown $markdownMode failed; exit=$markdownConsumerExit, checks=$($markdownFailedChecks.Name -join ', '), error=$($markdownResult.错误). Evidence: $markdownSupervisorPath"
        }
        $markdownResults += [PSCustomObject]@{ 模式 = $markdownMode; 全部通过 = $true; 消费目录 = $markdownConsumer; 检查数 = @($markdownResult.检查.PSObject.Properties).Count; 内嵌AXAML检查数 = $markdownResult.内嵌AXAML检查数 }
        Write-Output "Markdown $markdownMode consumer artifacts: $markdownConsumer"
    }
    $markdownPackageHashes = foreach ($markdownProduct in @('NT.AvaloniaControl.Markdown', 'NT.AvaloniaControl.Markdown.Themes', 'NT.AvaloniaControl.Markdown.TextMate')) {
        $markdownPackagePath = Join-Path $markdownFeed "$markdownProduct.$markdownPackageVersion.nupkg"
        [PSCustomObject]@{ 包 = $markdownProduct; 版本 = $markdownPackageVersion; SHA256 = (Get-FileHash -LiteralPath $markdownPackagePath -Algorithm SHA256).Hash }
    }
    [PSCustomObject]@{ 创建时间UTC = [DateTimeOffset]::UtcNow; 全部通过 = $true; 消费者 = $markdownResults; 包 = @($markdownPackageHashes) } |
        ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $markdownRepoRoot 'artifacts/markdown-package-consumer-results.json') -Encoding utf8
}
finally { Pop-Location }
