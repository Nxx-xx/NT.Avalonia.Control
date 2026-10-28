using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace NT.AvaloniaControl.PdfViewer;

public partial class PdfViewer
{
    public static readonly StyledProperty<IPdfFileDialogService?> FileDialogServiceProperty =
        AvaloniaProperty.Register<PdfViewer, IPdfFileDialogService?>(nameof(FileDialogService));
    public static readonly StyledProperty<IPdfPrintService?> PrintServiceProperty =
        AvaloniaProperty.Register<PdfViewer, IPdfPrintService?>(nameof(PrintService));
    public static readonly StyledProperty<IPdfShareService?> ShareServiceProperty =
        AvaloniaProperty.Register<PdfViewer, IPdfShareService?>(nameof(ShareService));

    /// <summary>Optional file picker. Null uses the attached top level's Avalonia storage provider.</summary>
    public IPdfFileDialogService? FileDialogService { get => GetValue(FileDialogServiceProperty); set => SetValue(FileDialogServiceProperty, value); }
    /// <summary>Adapter boundary for P8. Viewer printing remains unimplemented in P1.</summary>
    public IPdfPrintService? PrintService { get => GetValue(PrintServiceProperty); set => SetValue(PrintServiceProperty, value); }
    /// <summary>Adapter boundary for P8. Viewer sharing remains unimplemented in P1.</summary>
    public IPdfShareService? ShareService { get => GetValue(ShareServiceProperty); set => SetValue(ShareServiceProperty, value); }

    private async Task<PdfDocumentSource?> PickDocumentAsync(CancellationToken token)
    {
        if (FileDialogService is { } service)
            return service.CanOpen ? await service.OpenAsync(token) : null;
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage?.CanOpen != true) return null;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = PdfViewerStrings.FileDialogOpenTitle, AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(PdfViewerStrings.FileDialogPdfFileType) { Patterns = ["*.pdf"], MimeTypes = ["application/pdf"] }],
        });
        if (files.Count == 0) return null;
        // Fully capture storage-provider streams before disposing their file handles.
        using var file = files[0];
        token.ThrowIfCancellationRequested();
        if (file.TryGetLocalPath() is { } path) return PdfDocumentSource.FromFile(path);
        await using var stream = await file.OpenReadAsync();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, token);
        return PdfDocumentSource.FromBytes(buffer.ToArray(), file.Name);
    }

    private async Task<string?> PickSavePathAsync(CancellationToken token)
    {
        var suggested = string.IsNullOrWhiteSpace(DocumentName) ? "Document.pdf" : DocumentName;
        if (FileDialogService is { } service)
            return service.CanSave ? await service.GetSavePathAsync(suggested, token) : null;
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage?.CanSave != true) return null;
        using var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = PdfViewerStrings.SaveAsText, SuggestedFileName = suggested, DefaultExtension = "pdf", ShowOverwritePrompt = true,
            FileTypeChoices = [new FilePickerFileType(PdfViewerStrings.FileDialogPdfFileType) { Patterns = ["*.pdf"] }],
        });
        token.ThrowIfCancellationRequested();
        // Structural saves require a local path for same-directory atomic replacement.
        if (file is null) return null;
        return file.TryGetLocalPath() ?? throw new NotSupportedException("A local file target is required. Use SaveAsync(Stream) for non-local storage.");
    }
}
