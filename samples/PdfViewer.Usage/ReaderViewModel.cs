using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfViewerSamples;
using PdfControl = NT.AvaloniaControl.PdfViewer.PdfViewer;

namespace PdfViewer.Usage;

/// <summary>
/// MVVM surface for the reading sample: document binding plus host-side commands that drive the
/// P2 contract (reading position capture / restore, page image export). The reader arrives as a
/// CommandParameter, so the view model never holds a control reference. Layout, user bookmarks,
/// progressive search results and search progress bind to the reader directly from AXAML.
/// </summary>
public sealed partial class ReaderViewModel : ObservableObject, IDisposable
{
    private readonly string? _samplePath;

    [ObservableProperty] private string? _documentPath;
    [ObservableProperty] private string? _password;
    [ObservableProperty] private int _page = 1;
    [ObservableProperty] private bool _sidebarVisible = true;
    [ObservableProperty] private string _positionStatus = "尚未保存阅读位置";
    [ObservableProperty] private string _exportStatus = "尚未导出页面图片";

    /// <summary>The last captured reading position; the host owns persistence across sessions.</summary>
    public NT.AvaloniaControl.PdfViewer.PdfReadingPosition? SavedPosition { get; private set; }

    /// <summary>Layout mode list for the sample's ComboBox (the reader binds the selected value).</summary>
    public static NT.AvaloniaControl.PdfViewer.PdfPageDisplayMode[] DisplayModes { get; } =
        Enum.GetValues<NT.AvaloniaControl.PdfViewer.PdfPageDisplayMode>();

    /// <summary>Sidebar presentation list for the sample's ComboBox (offset / overlay).</summary>
    public static NT.AvaloniaControl.PdfViewer.PdfSidebarPresentation[] SidebarPresentations { get; } =
        Enum.GetValues<NT.AvaloniaControl.PdfViewer.PdfSidebarPresentation>();

    public ReaderViewModel(string? documentPath = null)
    {
        if (!string.IsNullOrWhiteSpace(documentPath)) DocumentPath = Path.GetFullPath(documentPath);
        else
        {
            _samplePath = Path.Combine(Path.GetTempPath(), $"nt-pdfviewer-sample-{Guid.NewGuid():N}.pdf");
            File.WriteAllBytes(_samplePath, SamplePdf.Create());
            DocumentPath = _samplePath;
        }
    }

    [RelayCommand]
    private void CapturePosition(PdfControl? reader)
    {
        if (reader is null) return;
        SavedPosition = reader.CaptureReadingPosition();
        PositionStatus = SavedPosition is { } position
            ? $"已保存：第 {position.PageIndex + 1} 页 {position.PagePosition.X:0},{position.PagePosition.Y:0} · {position.ZoomLevel:0.##}× · {position.Rotation}°"
            : "无文档，无法保存阅读位置";
    }

    [RelayCommand]
    private void RestorePosition(PdfControl? reader)
    {
        if (reader is null || SavedPosition is not { } position)
        {
            PositionStatus = "尚未保存阅读位置";
            return;
        }
        reader.RestoreReadingPosition(position);
        PositionStatus = $"已恢复：第 {position.PageIndex + 1} 页";
    }

    [RelayCommand]
    private async Task ExportPageAsync(PdfControl? reader)
    {
        if (reader is null || reader.CurrentPage < 1)
        {
            ExportStatus = "无文档，无法导出页面图片";
            return;
        }
        try
        {
            var png = await reader.ExportPageImageAsync(reader.CurrentPage - 1, dpi: 144);
            var path = Path.Combine(Path.GetTempPath(), $"nt-pdfviewer-page-{reader.CurrentPage}.png");
            await File.WriteAllBytesAsync(path, png);
            ExportStatus = $"已导出第 {reader.CurrentPage} 页 → {path}（{png.Length} bytes）";
        }
        catch (Exception exception)
        {
            ExportStatus = $"导出失败：{exception.Message}";
        }
    }

    public void Dispose()
    {
        if (_samplePath is null) return;
        try { File.Delete(_samplePath); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
