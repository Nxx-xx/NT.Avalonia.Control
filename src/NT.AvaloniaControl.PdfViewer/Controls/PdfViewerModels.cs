using System.ComponentModel;
using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.PdfViewer;

public enum PdfZoomMode { Manual, FitWidth, FitPage, FitHeight }

/// <summary>
/// How pages are arranged while reading. Continuous flows vertically through every page;
/// SinglePage and TwoPage show only the active page or spread. In two-page modes ShowCoverPage
/// keeps page 1 alone as the cover (spreads 2-3, 4-5…); otherwise spreads pair odd pages with
/// the following even page (1-2, 3-4…).
/// </summary>
public enum PdfPageDisplayMode { Continuous, SinglePage, TwoPageContinuous, TwoPage }

/// <summary>Which side of the document the sidebar occupies. The default template honors this
/// through its PART_BodyGrid / PART_Sidebar parts; custom templates may ignore it.</summary>
public enum PdfSidebarPlacement { Left, Right }

/// <summary>
/// How the sidebar meets the document: <see cref="Offset"/> gives it its own column and the
/// document reflows beside it; <see cref="Overlay"/> floats it above the document, which keeps
/// the full width (the narrow-window presentation). The default template honors this through its
/// PART_BodyGrid / PART_Sidebar / PART_CanvasGrid parts; custom templates may ignore it.
/// </summary>
public enum PdfSidebarPresentation { Offset, Overlay }

/// <summary>
/// A saved reading position: the anchor point that sat at the viewport's top-left corner plus the
/// zoom and rotation state that defined the view. PagePosition uses the same coordinate space as
/// <see cref="PdfLink.Bounds"/>: page-top-left origin, PDF points, including built-in page rotation
/// but not the control's <see cref="PdfViewer.Rotation"/>. Capture with
/// <see cref="PdfViewer.CaptureReadingPosition"/>, persist the values in the host, rebuild with the
/// public constructor, and reapply with <see cref="PdfViewer.RestoreReadingPosition"/>.
/// </summary>
public sealed record PdfReadingPosition(int PageIndex, Point PagePosition, double ZoomLevel, PdfZoomMode ZoomMode, int Rotation);

/// <summary>
/// Payload for <see cref="PdfViewer.SearchCompleted"/>: the final result count and whether the
/// walk stopped at <see cref="PdfViewer.SearchResultLimit"/>. Raised only when a search finishes
/// scanning; canceled, superseded and empty-query searches never complete.
/// </summary>
public sealed class PdfSearchCompletedEventArgs(int resultCount, bool truncated) : EventArgs
{
    public int ResultCount { get; } = resultCount;
    public bool Truncated { get; } = truncated;
}

/// <summary>A lazily decoded thumbnail. Images are owned by the viewer.</summary>
public sealed class PdfThumbnail : INotifyPropertyChanged
{
    private IImage? _image;
    internal PdfThumbnail(int pageNumber) => PageNumber = pageNumber;
    public int PageNumber { get; }
    public IImage? Image
    {
        get => _image;
        internal set
        {
            if (ReferenceEquals(_image, value)) return;
            _image = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Image)));
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}
