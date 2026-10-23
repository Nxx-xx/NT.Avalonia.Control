using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using NT.AvaloniaControl.Markdown;

namespace Markdown.Usage;

public sealed partial class MainWindow : Window
{
    private readonly MarkdownViewModel _model = new();
    private readonly MarkdownImageLoader _images = new();
    private CancellationTokenSource? _producer;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        DataContext = _model;
        Reader.ImageLoader = _images;
        Reader.LinkClicked += async (_, args) =>
        {
            if (Uri.TryCreate(args.Url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
            {
                args.Handled = true;
                if (GetTopLevel(this)?.Launcher is { } launcher) await launcher.LaunchUriAsync(uri);
            }
        };
        Closed += (_, _) => { _producer?.Cancel(); Reader.CancelInteraction(); Reader.CancelStreaming(); _producer?.Dispose(); _images.Dispose(); };
    }

    private async void StreamClick(object? sender, RoutedEventArgs args)
    {
        _producer?.Cancel(); Reader.CancelStreaming(); _producer?.Dispose();
        _producer = new CancellationTokenSource(); var cancellation = _producer.Token;
        try
        {
            // BeginStreaming is UI-thread-only. Append may also be called by background producers.
            var session = Reader.BeginStreaming();
            const string source = "\n\n## 流式结果\n\n每一批输入合并后在后台解析。\n\n```cs\nawait session.CompleteAsync();\n```";
            for (var offset = 0; offset < source.Length; offset += 6)
            {
                cancellation.ThrowIfCancellationRequested();
                session.Append(source.Substring(offset, Math.Min(6, source.Length - offset)));
                await Task.Delay(50, cancellation);
            }
            var result = await session.CompleteAsync();
            _model.Status = $"完成：{result.Status}；Text 绑定长度 {_model.Text.Length}。";
        }
        catch (OperationCanceledException) { _model.Status = "追加已取消。"; }
        catch (Exception error) { _model.Status = error.Message; }
    }
    private void ReplaceClick(object? sender, RoutedEventArgs args) { _producer?.Cancel(); Reader.CancelStreaming(); _model.Text = MarkdownViewModel.InitialText; }
    private void SelectAllClick(object? sender, RoutedEventArgs args) => Reader.SelectAll();
    private async void CopyClick(object? sender, RoutedEventArgs args) => _model.Status = $"复制：{await Reader.CopyAsync()}";
}
