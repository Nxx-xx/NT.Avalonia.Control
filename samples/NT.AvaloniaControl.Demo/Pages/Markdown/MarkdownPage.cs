using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Markdig.Syntax;
using NT.AvaloniaControl.Demo.Navigation;
using NT.AvaloniaControl.Markdown;
using NT.AvaloniaControl.Markdown.TextMate;
using TextMateSharp.Grammars;
using MarkdownControl = NT.AvaloniaControl.Markdown.Markdown;

namespace NT.AvaloniaControl.Demo.Pages.Markdown;

internal sealed partial class MarkdownPage : UserControl, IDemoPage
{
    private readonly Window _hostWindow;
    private readonly MarkdownControl _reader = new() { Name = "MarkdownReader" };
    private readonly MarkdownDemoViewModel _model = new();
    private readonly DemoImageLoader _images = new();
    private readonly DemoImageLoader _localImages = new();
    private readonly TextMateCodeHighlighter _highlighter = new();
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ComboBox _scenarios = new() { ItemsSource = MarkdownCorpus.Names, SelectedIndex = 0, MinWidth = 220 };
    private CancellationTokenSource? _producer;
    private bool _active;
    private bool _disposed;
    private bool _customStyle;
    private bool _highlight = true;

    internal MarkdownPage(Window hostWindow)
    {
        _hostWindow = hostWindow;
        Name = "MarkdownPage";
        DataContext = _model;
        _reader.Bind(MarkdownControl.TextProperty, new Binding(nameof(MarkdownDemoViewModel.Text)) { Mode = BindingMode.TwoWay });
        _reader.ImageLoader = _images;
        _reader.CodeHighlighter = _highlighter;
        _reader.ElementServices = node => node.Inline?.Text switch
        {
            "局部加载器" => new() { OverrideImageLoader = true, ImageLoader = _localImages },
            "局部禁用图片" => new() { OverrideImageLoader = true },
            _ => node.Block.SemanticKind == MarkdownBlockKind.Code && node.Block.Metadata.GetValueOrDefault("language") == "plain" ? new() { OverrideCodeHighlighter = true } : null,
        };
        _reader.ConfigureParser(new() { Converters = [new MarkdownNodeConverterRegistration<ParagraphBlock>(node => node.SourceText.StartsWith("CARD: ", StringComparison.Ordinal) ? new("demo-card") { StyleClasses = ["demo-card"] } : null)] });
        _reader.NodeRenderers = new([new("demo-card", node => new Border { BorderBrush = Brushes.SteelBlue, BorderThickness = new Thickness(2), Padding = new Thickness(12), CornerRadius = new CornerRadius(8), Child = node.CreateDefault() })]);
        _reader.Updated += ReaderUpdated;
        _reader.SelectionChanged += ReaderUpdated;
        _reader.Error += ReaderError;
        _reader.LinkClicked += LinkClicked;
        _hostWindow.ActualThemeVariantChanged += HostThemeChanged;
        HostThemeChanged(null, EventArgs.Empty);
        Content = CreateContent();
    }

    public Control View => this;
    public void Activate() { if (_disposed) return; _active = true; UpdateStatus(); }
    public void Deactivate()
    {
        _active = false;
        _producer?.Cancel();
        _reader.CancelInteraction();
        _reader.CancelStreaming();
    }
    public void Dispose()
    {
        if (_disposed) return;
        Deactivate(); _disposed = true;
        _reader.Updated -= ReaderUpdated; _reader.SelectionChanged -= ReaderUpdated;
        _reader.Error -= ReaderError; _reader.LinkClicked -= LinkClicked;
        _hostWindow.ActualThemeVariantChanged -= HostThemeChanged;
        _reader.ImageLoader = null; _reader.CodeHighlighter = null;
        _images.Dispose(); _localImages.Dispose(); _producer?.Dispose(); DataContext = null;
    }

    private Control CreateContent()
    {
        var scenarios = _scenarios;
        scenarios.SelectionChanged += (_, _) => { _producer?.Cancel(); _reader.CancelStreaming(); _model.Text = MarkdownCorpus.Get(scenarios.SelectedIndex); };
        var toolbar = new WrapPanel { Orientation = Orientation.Horizontal };
        toolbar.Children.Add(scenarios);
        toolbar.Children.Add(Button("全选", () => _reader.SelectAll()));
        toolbar.Children.Add(AsyncButton("复制", async () => _status.Text = $"复制结果：{await _reader.CopyAsync()}"));
        toolbar.Children.Add(AsyncButton("流式追加", StreamAsync));
        toolbar.Children.Add(Button("停止追加", () => { _producer?.Cancel(); _reader.CancelStreaming(); }));
        toolbar.Children.Add(Button("滚到末尾", () => _reader.ScrollToEnd()));
        toolbar.Children.Add(Button("高亮开关", () => { _highlight = !_highlight; _reader.CodeHighlighter = _highlight ? _highlighter : null; }));
        toolbar.Children.Add(Button("高亮主题", () => _highlighter.SetTheme(_highlighter.Theme == ThemeName.DarkPlus ? ThemeName.LightPlus : ThemeName.DarkPlus)));
        toolbar.Children.Add(Button("样式覆盖", ToggleStyle));
        var follow = new CheckBox { Content = "在底部时跟随更新", Margin = new Thickness(0, 4) };
        follow.IsCheckedChanged += (_, _) => _reader.AutoScrollToEnd = follow.IsChecked == true;
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*"), Margin = new Thickness(20) };
        grid.Children.Add(new TextBlock { Text = "Markdown · 原生阅读与流式会话", FontSize = 24, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 12) });
        Grid.SetRow(toolbar, 1); grid.Children.Add(toolbar);
        Grid.SetRow(follow, 2); grid.Children.Add(follow);
        _status.Margin = new Thickness(0, 6, 0, 10); Grid.SetRow(_status, 3); grid.Children.Add(_status);
        Grid.SetRow(_reader, 4); grid.Children.Add(_reader);
        return grid;
    }

    private async Task StreamAsync()
    {
        if (!_active || _disposed) return;
        _producer?.Cancel(); _reader.CancelStreaming(); _producer?.Dispose();
        _producer = new CancellationTokenSource(); var cancellation = _producer.Token;
        try
        {
            var session = _reader.BeginStreaming();
            for (var offset = 0; offset < MarkdownCorpus.Stream.Length; offset += 8)
            {
                cancellation.ThrowIfCancellationRequested();
                session.Append(MarkdownCorpus.Stream.Substring(offset, Math.Min(8, MarkdownCorpus.Stream.Length - offset)));
                await Task.Delay(50, cancellation);
            }
            var result = await session.CompleteAsync();
            if (_active && !_disposed) _status.Text = $"流式完成：{result.Status}；源长度 {_model.Text.Length}；布局待显示 {result.LayoutPending}。";
        }
        catch (OperationCanceledException) { if (_active && !_disposed) _status.Text = "流式生产者已停止。"; }
        catch (Exception error) { if (_active && !_disposed) _status.Text = error.Message; }
    }

    private void ToggleStyle()
    {
        _customStyle = !_customStyle;
        if (_customStyle) { _reader.Resources["MarkdownFontSize"] = 18d; _reader.Resources["MarkdownH1FontSize"] = 38d; _reader.Resources["MarkdownLinkBrush"] = Brushes.MediumVioletRed; }
        else { _reader.Resources.Remove("MarkdownFontSize"); _reader.Resources.Remove("MarkdownH1FontSize"); _reader.Resources.Remove("MarkdownLinkBrush"); }
        UpdateStatus();
    }
    private void ReaderUpdated(object? sender, EventArgs args) => UpdateStatus();
    private void UpdateStatus()
    {
        if (!_active || _disposed) return;
        _status.Text = $"逻辑文本 {_reader.Document.Text.Length:N0} 字符 · 选区 {_reader.SelectedText.Length:N0} 字符 · 已呈现 {_reader.DocumentPresenter?.RealizedBlockCount ?? 0} 块。";
    }
    private void ReaderError(object? sender, MarkdownErrorEventArgs args) { if (_active) _status.Text = $"{args.Category}：{args.Error.Message}"; }
    private void HostThemeChanged(object? sender, EventArgs args) => _highlighter.SetTheme(_hostWindow.ActualThemeVariant == ThemeVariant.Dark ? ThemeName.DarkPlus : ThemeName.LightPlus);
    private async void LinkClicked(object? sender, MarkdownLinkEventArgs args)
    {
        if (Uri.TryCreate(args.Url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
        {
            args.Handled = true;
            if (TopLevel.GetTopLevel(_reader)?.Launcher is { } launcher) await launcher.LaunchUriAsync(uri);
        }
    }
    private static Button Button(string label, Action action) { var button = new Button { Content = label, Margin = new Thickness(6, 0, 0, 4) }; button.Click += (_, _) => action(); return button; }
    private static Button AsyncButton(string label, Func<Task> action) { var button = new Button { Content = label, Margin = new Thickness(6, 0, 0, 4) }; button.Click += async (_, _) => await action(); return button; }

    private sealed class DemoImageLoader : IMarkdownImageLoader, IDisposable
    {
        private readonly MarkdownImageLoader _default = new();
        public async ValueTask<MarkdownImageLease> LoadAsync(Uri uri, CancellationToken cancellationToken)
        {
            if (uri.Scheme != "demo") return await _default.LoadAsync(uri, cancellationToken);
            await Task.Delay(350, cancellationToken);
            if (uri.Host == "failure") throw new IOException("本地固定图片失败场景。");
            return await Dispatcher.UIThread.InvokeAsync(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var view = new Border
                {
                    Width = 640, Height = 160, Background = new SolidColorBrush(0xff1d5a7b), Padding = new Thickness(30),
                    Child = new TextBlock { Text = "自定义图片 loader\n延迟返回的本地预览", Foreground = Brushes.White, FontSize = 28, VerticalAlignment = VerticalAlignment.Center }
                };
                view.Measure(new Size(640, 160)); view.Arrange(new Rect(0, 0, 640, 160));
                var bitmap = new RenderTargetBitmap(new PixelSize(640, 160), new Vector(96, 96)); bitmap.Render(view);
                return new MarkdownImageLease(bitmap);
            });
        }
        public void Dispose() => _default.Dispose();
    }
}

internal sealed partial class MarkdownDemoViewModel : ObservableObject
{
    [ObservableProperty] private string _text = MarkdownCorpus.Basic;
}
