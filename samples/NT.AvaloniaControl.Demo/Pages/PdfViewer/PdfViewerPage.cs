using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using PdfViewerSamples;
using NT.AvaloniaControl.Demo.Navigation;
using NT.AvaloniaControl.PdfViewer;
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
    private bool _closeAccepted, _closePending;

    internal PdfViewerPage(Window hostWindow)
    {
        _hostWindow = hostWindow;
        _reader.UnsavedChangesService = new DemoUnsavedChangesService(hostWindow);
        _hostWindow.Closing += ConfirmWindowClose;
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
        var allowEditing = new CheckBox { Content = "启用保存", IsChecked = false };
        allowEditing.IsCheckedChanged += async (_, _) =>
        {
            _reader.IsReadOnly = allowEditing.IsChecked != true;
            if (!_reader.IsReadOnly && _reader.HasDocument)
            {
                try { await _reader.EnableEditingAsync(); }
                catch (Exception error) { _message.Text = error.Message; }
            }
        };
        actions.Children.Add(allowEditing);
        var autoSave = new CheckBox { Content = "自动保存", IsChecked = false };
        autoSave.IsCheckedChanged += (_, _) => _reader.AutoSave = autoSave.IsChecked == true;
        actions.Children.Add(autoSave);
        var grid = new Grid { Margin = new Thickness(20), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*"), RowSpacing = 8 };
        grid.Children.Add(new TextBlock { Text = "PDF Viewer · 桌面阅读", FontSize = 24, FontWeight = FontWeight.SemiBold });
        Grid.SetRow(actions, 1); grid.Children.Add(actions);
        var help = new TextBlock
        {
            Text = "启用保存后，用户书签计入未保存状态并随 PDF 持久化。字节示例需先另存为，AutoSave 才会启用；工具栏提供保存、另存为与内容撤销 / 重做。",
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
        _hostWindow.Closing -= ConfirmWindowClose;
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
            using var file = files[0];
            if (file.TryGetLocalPath() is { } path) await _reader.LoadDocumentAsync(path, _password.Text, cancellation);
            else
            {
                await using var stream = await file.OpenReadAsync();
                await _reader.LoadDocumentAsync(stream, _password.Text, cancellation);
            }
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

    private async void ConfirmWindowClose(object? sender, WindowClosingEventArgs args)
    {
        if (_closeAccepted || _disposed) return;
        args.Cancel = true;
        if (_closePending) return;
        _closePending = true;
        try
        {
            if (await _reader.RequestCloseDocumentAsync()) { _closeAccepted = true; Avalonia.Threading.Dispatcher.UIThread.Post(_hostWindow.Close); }
        }
        catch (OperationCanceledException) { }
        finally { _closePending = false; }
    }

    private sealed class DemoUnsavedChangesService(Window owner) : IPdfUnsavedChangesService
    {
        public async Task<PdfUnsavedChangesDecision> ConfirmAsync(PdfUnsavedChangesContext context, CancellationToken cancellationToken = default)
        {
            var dialog = new Window { Title = PdfViewerStrings.UnsavedChangesTitle, Width = 460, SizeToContent = SizeToContent.Height, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
            foreach (var (label, decision) in new[] { (PdfViewerStrings.SaveText, PdfUnsavedChangesDecision.Save), (PdfViewerStrings.DiscardChangesText, PdfUnsavedChangesDecision.Discard), (PdfViewerStrings.CancelChangesText, PdfUnsavedChangesDecision.Cancel) })
            {
                var button = new Button { Content = label };
                button.Click += (_, _) => dialog.Close(decision);
                actions.Children.Add(button);
            }
            dialog.Content = new StackPanel { Margin = new Thickness(24), Spacing = 16, Children = { new TextBlock { Text = context.DocumentName, FontWeight = FontWeight.SemiBold }, new TextBlock { Text = PdfViewerStrings.UnsavedChangesMessage, TextWrapping = TextWrapping.Wrap }, actions } };
            var pending = dialog.ShowDialog<PdfUnsavedChangesDecision>(owner);
            using var registration = cancellationToken.Register(() => Avalonia.Threading.Dispatcher.UIThread.Post(() => dialog.Close(PdfUnsavedChangesDecision.Cancel)));
            return await pending.WaitAsync(cancellationToken);
        }
    }
}
