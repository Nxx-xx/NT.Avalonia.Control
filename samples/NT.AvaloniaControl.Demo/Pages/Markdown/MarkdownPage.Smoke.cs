using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.Markdown;

namespace NT.AvaloniaControl.Demo.Pages.Markdown;

internal sealed partial class MarkdownPage
{
    internal async Task RunMarkdownSmokeAsync(string[] args)
    {
        var output = SmokeArgument(args, "--smoke-output") ?? Path.Combine("artifacts", "platform", "windows-markdown-smoke.json");
        var imageOutput = SmokeArgument(args, "--smoke-image");
        var checks = new Dictionary<string, bool>();
        var observations = new Dictionary<string, double>();
        var limitations = new List<string>
        {
            "键盘事件通过应用路由模拟；物理键鼠、触屏、真实辅助工具和完整 DPI 矩阵另行验收。",
            "图像是可见原生 Window 的 Avalonia RenderTargetBitmap 渲染，不是桌面截图。",
            "本地固定图片服务用于锚点测试；未把远程网络下载当作流式完成边界。"
        };
        string? error = null;
        string? clipboardOriginal = null;
        var clipboardWasEmpty = false;
        var clipboardChanged = false;
        var clipboardRestored = false;
        _reader.LinkClicked -= LinkClicked;
        try
        {
            await _reader.WhenUpdatedAsync().WaitAsync(TimeSpan.FromSeconds(10));
            await SmokeFlushAsync();
            checks["原生窗口已创建"] = _hostWindow.TryGetPlatformHandle() is { Handle: not 0 };
            checks["Markdown页面已选择"] = _hostWindow is MainWindow main && main.SelectedPageId == "markdown";
            checks["模板部件可用"] = _reader.HasPresentationEnvironment;

            const string projectionSource = "# 阅读探针\n\n中文 **强调** 😀 e\u0301\n\n```cs\n  Console.WriteLine(1);\n```\n\n| A | B |\n|---|---|\n| c | d |\n\n[host link](https://example.test/markdown)";
            _model.Text = projectionSource;
            await _reader.WhenUpdatedAsync().WaitAsync(TimeSpan.FromSeconds(10));
            _reader.SelectAll();
            var copyProjection = _reader.SelectedText;
            checks["跨段落代码表格全文选择"] = copyProjection == _reader.Document.Text &&
                copyProjection.Contains("  Console.WriteLine(1);", StringComparison.Ordinal) &&
                copyProjection.Contains("A\tB\nc\td", StringComparison.Ordinal);
            if (_hostWindow.Clipboard is { } clipboard)
            {
                clipboardOriginal = await clipboard.TryGetTextAsync().WaitAsync(TimeSpan.FromSeconds(5));
                if (clipboardOriginal is null) clipboardWasEmpty = !(await clipboard.GetDataFormatsAsync().WaitAsync(TimeSpan.FromSeconds(5))).Any();
                if (clipboardOriginal is not null || clipboardWasEmpty)
                {
                    clipboardChanged = true;
                    var copied = await _reader.CopyAsync().WaitAsync(TimeSpan.FromSeconds(5));
                    checks["真实剪贴板文本复制"] = copied == MarkdownCopyResult.Copied && await clipboard.TryGetTextAsync().WaitAsync(TimeSpan.FromSeconds(5)) == copyProjection;
                    checks["剪贴板测试已执行"] = true;
                }
                else
                {
                    checks["剪贴板测试已执行"] = false;
                    limitations.Add("原剪贴板没有可读取的文本；保留原剪贴板，真实复制保持待验证。请在授权的文本剪贴板环境重跑。");
                }
            }
            else
            {
                checks["剪贴板测试已执行"] = false;
                limitations.Add("当前原生窗口未提供剪贴板服务；真实复制保持待验证。");
            }

            _reader.SetSelection(0, 4);
            var selection = _reader.SelectedText;
            var session = _reader.BeginStreaming();
            await Task.Run(() => { session.Append("\n\nstream **tail**"); session.Append("\n"); });
            var streaming = await session.CompleteAsync().WaitAsync(TimeSpan.FromSeconds(10));
            checks["跨线程流式最终提交"] = streaming.Status == MarkdownUpdateStatus.Completed && !streaming.LayoutPending && _reader.Document.Source == streaming.Source;
            checks["流式同步保留MVVM绑定和选区"] = _model.Text == streaming.Source && _reader.SelectedText == selection;
            _model.Text = projectionSource;
            await _reader.WhenUpdatedAsync().WaitAsync(TimeSpan.FromSeconds(10));
            checks["宿主外部绑定替换优先"] = _reader.Document.Source == projectionSource && _reader.SelectedText.Length == 0;

            string? activatedUrl = null;
            EventHandler<MarkdownLinkEventArgs> activated = (_, args) => { activatedUrl = args.Url; args.Handled = true; };
            _reader.LinkClicked += activated;
            try
            {
                var link = _reader.Document.Descendants().SelectMany(block => block.Inlines).First(inline => inline.Kind == MarkdownInlineKind.Link);
                _reader.SetSelection(link.TextStart, link.TextStart);
                _reader.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Source = _reader, Key = Key.Enter });
                checks["链接键盘激活交给宿主"] = activatedUrl == "https://example.test/markdown";
            }
            finally { _reader.LinkClicked -= activated; }

            _reader.SelectAll();
            var themeText = _reader.SelectedText;
            _hostWindow.RequestedThemeVariant = ThemeVariant.Dark;
            await SmokeFlushAsync();
            _hostWindow.RequestedThemeVariant = ThemeVariant.Light;
            await SmokeFlushAsync();
            checks["换肤保留全文和选区"] = _reader.SelectedText == themeText && _reader.Document.Source == projectionSource;

            _model.Text = "![本地延迟图片](demo://preview)\n\n" + SmokeParagraphs(40);
            await _reader.WhenUpdatedAsync().WaitAsync(TimeSpan.FromSeconds(10));
            await SmokeFlushAsync();
            var scroll = SmokeScroll();
            _reader.AutoScrollToEnd = false;
            scroll.Offset = new Vector(0, 180);
            await SmokeFlushAsync();
            var beforeImage = SmokeFirstVisible(scroll);
            checks["图片增长前占位可见"] = _reader.GetVisualDescendants().OfType<Image>().Any(image => image.Source is null);
            await WaitSmokeConditionAsync(() => _reader.GetVisualDescendants().OfType<Image>().Any(image => image.Source is not null));
            await SmokeFlushAsync();
            var afterImage = SmokeFirstVisible(scroll);
            checks["图片增高保持阅读锚点"] = beforeImage.Id == afterImage.Id && Math.Abs(beforeImage.Offset - afterImage.Offset) <= 2;

            _reader.AutoScrollToEnd = true;
            _reader.ScrollToEnd();
            await SmokeFlushAsync();
            scroll.Offset = new Vector(0, 240);
            await SmokeFlushAsync();
            var beforeAppend = SmokeFirstVisible(scroll);
            var readingSession = _reader.BeginStreaming();
            readingSession.Append("\n\nreading tail");
            await readingSession.CompleteAsync().WaitAsync(TimeSpan.FromSeconds(10));
            await SmokeFlushAsync();
            var afterAppend = SmokeFirstVisible(scroll);
            checks["用户上滚暂停底部跟随"] = beforeAppend.Id == afterAppend.Id && Math.Abs(beforeAppend.Offset - afterAppend.Offset) <= 2 &&
                scroll.Offset.Y < scroll.Extent.Height - scroll.Viewport.Height - 24;
            scroll.ScrollToEnd();
            await SmokeFlushAsync();
            var bottomSession = _reader.BeginStreaming();
            bottomSession.Append("\n\nnew bottom tail");
            await bottomSession.CompleteAsync().WaitAsync(TimeSpan.FromSeconds(10));
            await SmokeFlushAsync();
            checks["回到底部恢复跟随"] = scroll.Extent.Height - scroll.Viewport.Height - scroll.Offset.Y <= 2;
            observations["阅读测试滚动范围DIP"] = scroll.Extent.Height - scroll.Viewport.Height;

            var cancelled = _reader.BeginStreaming();
            cancelled.Append(" deactivated tail");
            Deactivate();
            checks["页面停用取消会话"] = (await cancelled.Completion.WaitAsync(TimeSpan.FromSeconds(10))).Status == MarkdownUpdateStatus.Cancelled && !_active;
            Activate();
            var acceptsStale = false;
            try { cancelled.Append("old producer"); acceptsStale = true; } catch (InvalidOperationException) { }
            checks["页面重新激活不恢复旧会话"] = _active && !acceptsStale;

            _reader.AutoScrollToEnd = false;
            _model.Text = "CARD: native **card**\n\n![局部加载器](demo://local) ![局部禁用图片](demo://failure)\n\n```plain\nplain code\n```\n\n```cs\nvar inherited = 1;\n```";
            await _reader.WhenUpdatedAsync().WaitAsync(TimeSpan.FromSeconds(10));
            SmokeScroll().Offset = default;
            await WaitSmokeConditionAsync(() => _reader.GetVisualDescendants().OfType<MarkdownImage>().Any(image => image.AltText == "局部加载器" && image.Child is Image { Source: not null }));
            await SmokeFlushAsync();
            var custom = _reader.DocumentPresenter!.Children.OfType<Border>().Single(border => border.Classes.Contains("demo-card") && border.Classes.Contains("custom"));
            checks["自定义节点使用原生文本几何"] = custom.GetVisualDescendants().OfType<MarkdownTextPresenter>().Any(text => text.TextLength > 0) && _reader.RenderingDiagnostics.Count == 0;
            var elementImages = _reader.GetVisualDescendants().OfType<MarkdownImage>().ToArray();
            checks["单图片服务局部覆盖与显式禁用"] = elementImages.Any(image => image.AltText == "局部加载器" && ReferenceEquals(image.ImageLoader, _localImages)) &&
                elementImages.Any(image => image.AltText == "局部禁用图片" && image.ImageLoader is null && !image.IsLoading && image.LoadError is null);
            _reader.ScrollToEnd(); await SmokeFlushAsync();
            var elementCodes = _reader.GetVisualDescendants().OfType<MarkdownCodeBlock>().ToArray();
            checks["单代码块高亮覆盖与根继承"] = elementCodes.Any(code => code.Language == "plain" && code.CodeHighlighter is null) &&
                elementCodes.Any(code => code.Language == "cs" && ReferenceEquals(code.CodeHighlighter, _highlighter));
            _reader.SelectAll();
            checks["自定义节点与局部服务保持全文投影"] = _reader.SelectedText == _reader.Document.Text && _reader.SelectedText.Contains("CARD: native card", StringComparison.Ordinal) &&
                _reader.SelectedText.Contains("plain code\n", StringComparison.Ordinal) && _reader.SelectedText.Contains("局部禁用图片", StringComparison.Ordinal);
            var alertGeometry = new HashSet<string>();
            foreach (var alert in new[] { "NOTE", "TIP", "IMPORTANT", "WARNING", "CAUTION" })
            {
                _model.Text = $"> [!{alert}]\n> native alert body";
                await _reader.WhenUpdatedAsync().WaitAsync(TimeSpan.FromSeconds(10));
                SmokeScroll().Offset = default; await SmokeFlushAsync();
                var icon = _reader.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Single(path => path.Classes.Contains("alerticon"));
                if (icon.Bounds.Width > 0 && icon.Fill is not null && icon.Data is not null)
                {
                    var fill = icon.Fill;
                    icon.SetCurrentValue(Avalonia.Controls.Shapes.Shape.FillProperty, Avalonia.Media.Brushes.Black);
                    using var iconBitmap = new RenderTargetBitmap(new PixelSize(24, 24), new Vector(96, 96));
                    iconBitmap.Render(icon);
                    using var iconStream = new MemoryStream(); iconBitmap.Save(iconStream);
                    alertGeometry.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(iconStream.ToArray())));
                    icon.SetCurrentValue(Avalonia.Controls.Shapes.Shape.FillProperty, fill);
                }
                _reader.SelectAll();
                checks[$"原生提示{alert}图标与复制"] = icon.Classes.Contains(alert.ToLowerInvariant()) && _reader.SelectedText == alert + "\nnative alert body";
            }
            checks["五类提示使用不同矢量图形"] = alertGeometry.Count == 5;
            await SmokeDefaultImageLoaderAsync(checks, observations, limitations, output);

            _model.Text = MarkdownCorpus.Basic;
            await _reader.WhenUpdatedAsync().WaitAsync(TimeSpan.FromSeconds(10));
            _reader.ClearSelection();
            SmokeScroll().Offset = default;
            await SmokeFlushAsync();
            if (imageOutput is not null) CaptureMarkdownSmokeImage(imageOutput);
        }
        catch (Exception exception) { error = exception.ToString(); }
        finally
        {
            if (clipboardChanged && _hostWindow.Clipboard is { } clipboard)
            {
                try
                {
                    if (clipboardWasEmpty) { await clipboard.ClearAsync().WaitAsync(TimeSpan.FromSeconds(5)); clipboardRestored = !(await clipboard.GetDataFormatsAsync().WaitAsync(TimeSpan.FromSeconds(5))).Any(); }
                    else { await clipboard.SetTextAsync(clipboardOriginal!).WaitAsync(TimeSpan.FromSeconds(5)); clipboardRestored = await clipboard.TryGetTextAsync().WaitAsync(TimeSpan.FromSeconds(5)) == clipboardOriginal; }
                }
                catch (Exception exception) { error ??= "恢复剪贴板原文本失败：" + exception; }
                checks[clipboardWasEmpty ? "空剪贴板状态已恢复" : "剪贴板原文本已恢复"] = clipboardRestored;
            }
            _reader.LinkClicked += LinkClicked;
        }
        var passed = error is null && checks.Count > 0 && checks.Values.All(value => value);
        try
        {
            var fullOutput = Path.GetFullPath(output);
            Directory.CreateDirectory(Path.GetDirectoryName(fullOutput)!);
            File.WriteAllText(fullOutput, JsonSerializer.Serialize(new
            {
                通过 = passed, 时间UTC = DateTimeOffset.UtcNow,
                平台 = RuntimeInformation.OSDescription, CPU架构 = RuntimeInformation.ProcessArchitecture.ToString(),
                运行时 = Environment.Version.ToString(), Avalonia版本 = typeof(Application).Assembly.GetName().Version?.ToString(),
                字体 = _reader.FontFamily.ToString(), 字号DIP = _reader.FontSize,
                缩放 = _hostWindow.RenderScaling, 窗口宽DIP = _hostWindow.ClientSize.Width, 窗口高DIP = _hostWindow.ClientSize.Height,
                文档视口宽DIP = _reader.Bounds.Width, 文档视口高DIP = _reader.Bounds.Height,
                检查 = checks, 观察 = observations, 错误 = error, 限制 = limitations,
                图片输出 = imageOutput is null ? null : Path.GetFullPath(imageOutput),
                真实物理输入已验证 = false, 辅助工具已验证 = false, DPI矩阵已验证 = false, 麒麟已验证 = false
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.Shutdown(passed ? 0 : 1);
            else _hostWindow.Close();
        }
    }

    private ScrollViewer SmokeScroll() => _reader.GetVisualDescendants().OfType<ScrollViewer>().Single(scroll => scroll.Name == "PART_ScrollViewer");
    private (long Id, double Offset) SmokeFirstVisible(ScrollViewer scroll)
    {
        var first = _reader.DocumentPresenter!.Children.OfType<MarkdownBlockPresenter>()
            .Where(block => block.Bounds.Bottom > scroll.Offset.Y + .1).OrderBy(block => block.Bounds.Y).First();
        return (first.NodeId, first.Bounds.Y - scroll.Offset.Y);
    }
    private async Task SmokeFlushAsync() { await Task.Delay(40); _hostWindow.UpdateLayout(); }
    private async Task WaitSmokeConditionAsync(Func<bool> predicate)
    {
        for (var i = 0; i < 100; i++) { _hostWindow.UpdateLayout(); if (predicate()) return; await Task.Delay(25); }
        throw new TimeoutException("本地原生探针条件未在 2.5 秒内满足。");
    }
    private void CaptureMarkdownSmokeImage(string path)
    {
        var size = _hostWindow.ClientSize;
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height)), new Vector(96, 96));
        bitmap.Render(_hostWindow);
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        bitmap.Save(fullPath);
    }
    private static string SmokeParagraphs(int count) => string.Join("\n\n", Enumerable.Range(0, count).Select(i => $"阅读段落 {i:D2} 中文 English marker text。"));
    private static string? SmokeArgument(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
