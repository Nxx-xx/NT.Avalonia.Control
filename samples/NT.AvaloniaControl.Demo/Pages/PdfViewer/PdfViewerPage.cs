using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using PdfViewerSamples;
using NT.AvaloniaControl.Demo.Navigation;
using PdfControl = NT.AvaloniaControl.PdfViewer.PdfViewer;

namespace NT.AvaloniaControl.Demo.Pages.PdfViewer;

internal sealed partial class PdfViewerPage : UserControl, IDemoPage
{
    private readonly Window _hostWindow;
    private readonly PdfControl _reader = new() { Name = "PdfReader" };
    private readonly TextBox _password = new() { PlaceholderText = "加密 PDF 密码（可选）", PasswordChar = '●', Width = 190 };
    private readonly TextBlock _message = new() { TextWrapping = TextWrapping.Wrap };
    private CancellationTokenSource? _load;
    private bool _initialized;
    private bool _disposed;

    internal PdfViewerPage(Window hostWindow)
    {
        _hostWindow = hostWindow;
        Name = "PdfViewerPage";
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        actions.Children.Add(ActionButton("加载内置三页示例", LoadSampleAsync));
        actions.Children.Add(ActionButton("打开本地 PDF", OpenAsync));
        actions.Children.Add(_password);
        var allowCopy = new CheckBox { Content = "允许复制", IsChecked = true };
        allowCopy.IsCheckedChanged += (_, _) => _reader.AllowCopy = allowCopy.IsChecked == true;
        actions.Children.Add(allowCopy);
        var allowSearch = new CheckBox { Content = "允许搜索", IsChecked = true };
        allowSearch.IsCheckedChanged += (_, _) => _reader.AllowSearch = allowSearch.IsChecked == true;
        actions.Children.Add(allowSearch);
        var grid = new Grid { Margin = new Thickness(20), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*"), RowSpacing = 8 };
        grid.Children.Add(new TextBlock { Text = "PDF Viewer · 桌面阅读", FontSize = 24, FontWeight = FontWeight.SemiBold });
        Grid.SetRow(actions, 1); grid.Children.Add(actions);
        var help = new TextBlock
        {
            Text = "连续滚动阅读；点击缩略图或目录跳转；在工具栏搜索并逐个定位结果；拖动选择文字后复制。内置文档包含目录、可搜索文字和三种页面尺寸。",
            TextWrapping = TextWrapping.Wrap,
        };
        Grid.SetRow(help, 2); grid.Children.Add(help);
        Grid.SetRow(_message, 3); grid.Children.Add(_message);
        Grid.SetRow(_reader, 4); grid.Children.Add(_reader);
        Content = grid;
    }

    public Control View => this;
    public void Activate()
    {
        if (_disposed || _initialized) return;
        _initialized = true;
        if (Program.Arguments.Contains("--pdf-smoke", StringComparer.Ordinal)) return;
        _ = LoadSampleAsync();
    }
    public void Deactivate()
    {
        _load?.Cancel();
        if (!_reader.HasDocument) _initialized = false;
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _load?.Cancel(); _load?.Dispose();
        _reader.Dispose();
        DataContext = null;
    }

    private async Task LoadSampleAsync()
    {
        await LoadAsync(async cancellation => await _reader.LoadDocumentAsync(SamplePdf.Create(), cancellationToken: cancellation));
        if (!_disposed && _reader.HasDocument)
            _message.Text = "内置示例已加载。搜索 Avalonia 可测试跨页定位；切换侧栏“目录”可查看三个书签。";
    }

    private async Task OpenAsync()
    {
        var files = await _hostWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "打开 PDF 文档", AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("PDF 文档") { Patterns = ["*.pdf"], MimeTypes = ["application/pdf"] }],
        });
        if (_disposed || files.Count == 0) return;
        await LoadAsync(async cancellation =>
        {
            await using var stream = await files[0].OpenReadAsync();
            await _reader.LoadDocumentAsync(stream, _password.Text, cancellation);
        });
        if (!_disposed && _reader.HasDocument) _message.Text = $"已打开：{files[0].Name}";
    }

    private async Task LoadAsync(Func<CancellationToken, Task> operation)
    {
        if (_disposed) return;
        _load?.Cancel(); _load?.Dispose();
        _load = new CancellationTokenSource();
        try { await operation(_load.Token); }
        catch (OperationCanceledException) { }
        catch (Exception error) { if (!_disposed) _message.Text = $"加载失败：{error.Message}"; }
    }

    private static Button ActionButton(string content, Func<Task> action)
    {
        var button = new Button { Content = content };
        button.Click += async (_, _) => await action();
        return button;
    }
}
