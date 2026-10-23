using Avalonia;

namespace NT.AvaloniaControl.PdfViewer;

/// <summary>The destination view type of an internal PDF destination (the /Fit family plus /XYZ).</summary>
public enum PdfDestinationFit { Unknown, Xyz, Fit, FitH, FitV, FitR, FitB, FitBH, FitBV }

/// <summary>
/// An internal navigation destination. PageIndex is zero-based and -1 when no valid page target exists.
/// Position is the destination point in the page's displayed top-left origin PDF points (the same space as
/// PdfTextCharacter.Bounds), present only for /XYZ destinations with both coordinates. Zoom is the /XYZ
/// zoom factor where 1 means 100%; null means the destination leaves the current zoom unchanged.
/// Rectangle is the /FitR rectangle and Left / Top the single axis coordinates: the /FitV / /FitBV and
/// /FitH / /FitBH edge parameters, and an /XYZ that defines only one coordinate (the other axis then
/// keeps its current scroll position). All are in the same displayed top-left origin space: Left is a
/// displayed X, Top a displayed Y. Pages with built-in rotation map these through the same page transform
/// as Position. The optional parameters are absent when the destination does not define them.
/// </summary>
public sealed record PdfLinkDestination(int PageIndex, Point? Position, double? Zoom, PdfDestinationFit Fit,
    Rect? Rectangle = null, double? Left = null, double? Top = null);

public enum PdfLinkKind
{
    /// <summary>An in-document destination. The viewer navigates to it unless the host handles the activation.</summary>
    Internal,
    /// <summary>A URI action. The control never launches it; hosts intercept it through LinkActivated.</summary>
    Uri,
    /// <summary>A launch or remote-go-to action naming another file. Never opened by the control.</summary>
    File,
    /// <summary>Any action this reader does not interpret (JavaScript, embedded go-to, unknown).</summary>
    Unsupported,
}

/// <summary>
/// A link annotation on one page. Bounds use the page's displayed top-left origin PDF points, matching
/// PdfTextCharacter.Bounds. Destination is set for internal links; Target carries the URI string or file
/// path for Uri and File links. External and unsupported links are never executed by the control.
/// </summary>
public sealed record PdfLink(int PageIndex, Rect Bounds, PdfLinkKind Kind, PdfLinkDestination? Destination, string? Target);

/// <summary>Raised when a link annotation is activated. Handle the event to suppress default behavior.</summary>
public sealed class PdfLinkActivatedEventArgs(PdfLink link) : EventArgs
{
    public PdfLink Link { get; } = link;

    /// <summary>
    /// Set to true to suppress the viewer's default handling. The default only navigates internal
    /// destinations; URI, file and unsupported links do nothing unless the host acts on this event.
    /// </summary>
    public bool Handled { get; set; }
}
