using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NT.AvaloniaControl.PdfViewer;
using PdfControl = NT.AvaloniaControl.PdfViewer.PdfViewer;

namespace PdfViewer.Usage;

public partial class MainWindow : Window
{
    private bool _closeAccepted, _closePending;
    public MainWindow()
    {
        InitializeComponent();
        Closed += (_, _) => this.FindControl<PdfControl>("Reader")?.Dispose();
        Closing += async (_, args) =>
        {
            if (_closeAccepted) return;
            args.Cancel = true;
            if (_closePending) return;
            _closePending = true;
            try
            {
                if (this.FindControl<PdfControl>("Reader") is not { } reader || await reader.RequestCloseDocumentAsync())
                { _closeAccepted = true; Avalonia.Threading.Dispatcher.UIThread.Post(Close); }
            }
            catch (OperationCanceledException) { }
            finally { _closePending = false; }
        };
    }

    private async void OpenPdf(object? sender, RoutedEventArgs args)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "打开 PDF 文档", AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("PDF 文档") { Patterns = ["*.pdf"], MimeTypes = ["application/pdf"] }],
        });
        if (files.Count > 0 && DataContext is ReaderViewModel model)
            model.DocumentPath = files[0].TryGetLocalPath();
    }

    private void JumpToBookmark(object? sender, TappedEventArgs args)
    {
        // UserBookmarks doubles as the sample's host-side bookmark list.
        if (sender is ListBox { SelectedItem: PdfUserBookmark bookmark })
            this.FindControl<PdfControl>("Reader")?.GoToPage(bookmark.PageNumber);
    }
}
