using System.Text;
using Avalonia;
using NT.AvaloniaControl.PdfViewer.Interop;

namespace NT.AvaloniaControl.PdfViewer;

public sealed partial class PdfDocument
{
    /// <summary>
    /// The document's /PageLabels strings in page order. An entry is empty when the document
    /// defines no label for that page; viewers fall back to the page number.
    /// </summary>
    public IReadOnlyList<string> PageLabels { get; private set; } = Array.Empty<string>();

    /// <summary>
    /// Extracts every link annotation on one page with its bounds and resolved target. Internal
    /// destinations carry page, /XYZ position and zoom, and the /Fit-family view parameters (/FitR
    /// rectangle, /FitH / /FitBV edge coordinates) where defined; URI and file links only report
    /// their text. Nothing external is ever launched, and unsupported actions are returned as such.
    /// </summary>
    public Task<IReadOnlyList<PdfLink>> GetLinksAsync(int pageIndex, CancellationToken cancellationToken = default)
    {
        ValidatePage(pageIndex);
        return RunDocumentAsync(token => ReadLinks(pageIndex, token), cancellationToken);
    }

    private IReadOnlyList<PdfLink> ReadLinks(int pageIndex, CancellationToken token)
    {
        var page = LoadPage(pageIndex);
        try
        {
            var coordinates = new PageCoordinates(page, Pages[pageIndex]);
            var result = new List<PdfLink>();
            var position = 0;
            while (PdfiumNative.FPDFLink_Enumerate(page, ref position, out var link) != 0)
            {
                token.ThrowIfCancellationRequested();
                if (PdfiumNative.FPDFLink_GetAnnotRect(link, out var rect) == 0) continue;
                var bounds = coordinates.GetBounds(rect.Left, rect.Top, rect.Right, rect.Bottom);
                if (bounds.Width <= 0 || bounds.Height <= 0) continue;
                result.Add(ReadLinkTarget(pageIndex, bounds, link, token));
            }
            return result.AsReadOnly();
        }
        finally { PdfiumNative.FPDF_ClosePage(page); }
    }

    private PdfLink ReadLinkTarget(int pageIndex, Rect bounds, IntPtr link, CancellationToken token)
    {
        var destination = PdfiumNative.FPDFLink_GetDest(_handle, link);
        if (destination != IntPtr.Zero)
            return ResolveInternalLink(pageIndex, bounds, destination, token);
        var action = PdfiumNative.FPDFLink_GetAction(link);
        if (action == IntPtr.Zero)
            return new PdfLink(pageIndex, bounds, PdfLinkKind.Unsupported, null, null);
        switch (PdfiumNative.FPDFAction_GetType(action))
        {
            case PdfiumNative.ActionGoto:
                destination = PdfiumNative.FPDFAction_GetDest(_handle, action);
                return destination == IntPtr.Zero
                    ? new PdfLink(pageIndex, bounds, PdfLinkKind.Unsupported, null, null)
                    : ResolveInternalLink(pageIndex, bounds, destination, token);
            case PdfiumNative.ActionUri:
                return new PdfLink(pageIndex, bounds, PdfLinkKind.Uri, null,
                    ReadActionText((buffer, length) => PdfiumNative.FPDFAction_GetURIPath(_handle, action, buffer, length)));
            case PdfiumNative.ActionLaunch:
            case PdfiumNative.ActionRemoteGoto:
                return new PdfLink(pageIndex, bounds, PdfLinkKind.File, null,
                    ReadActionText((buffer, length) => PdfiumNative.FPDFAction_GetFilePath(action, buffer, length)));
            default:
                return new PdfLink(pageIndex, bounds, PdfLinkKind.Unsupported, null, null);
        }
    }

    private PdfLink ResolveInternalLink(int pageIndex, Rect bounds, IntPtr destination, CancellationToken token)
    {
        var resolved = ResolveDestinationCore(_handle, Pages, destination, token);
        // A link to an unresolved or external destination is not actionable inside this document.
        var kind = resolved.PageIndex >= 0 ? PdfLinkKind.Internal : PdfLinkKind.Unsupported;
        return new PdfLink(pageIndex, bounds, kind, resolved, null);
    }

    private static string ReadActionText(Func<byte[]?, uint, uint> read)
    {
        var length = read(null, 0);
        if (length == 0 || length > 1024 * 1024) return "";
        var buffer = new byte[length];
        var actual = read(buffer, (uint)buffer.Length);
        if (actual == 0) return "";
        // UTF-8 including a trailing NUL; the PDFium docs allow malformed input that we must sanitize.
        var text = Encoding.UTF8.GetString(buffer, 0, (int)Math.Min(actual, length));
        return text.TrimEnd('\0');
    }

    // Runs inside the scheduler with a live handle. Position mapping loads the target page so the
    // result matches PdfTextCharacter.Bounds (displayed top-left origin, built-in rotation and crop).
    private static PdfLinkDestination ResolveDestinationCore(IntPtr handle, IReadOnlyList<PdfPageInfo> pages,
        IntPtr destination, CancellationToken token)
    {
        var pageIndex = PdfiumNative.FPDFDest_GetDestPageIndex(handle, destination);
        if (pageIndex < 0 || pageIndex >= pages.Count)
            return new PdfLinkDestination(-1, null, null, PdfDestinationFit.Unknown);
        token.ThrowIfCancellationRequested();
        var parameters = new float[4];
        var fit = ToFit(PdfiumNative.FPDFDest_GetView(destination, out var numParams, parameters));
        // PDFium fills only the slots the fit type defines; cap a nonsensical count at the array size.
        var count = numParams <= 4 ? (int)numParams : 4;
        Point? position = null;
        double? zoom = null;
        Rect? rectangle = null;
        double? left = null;
        double? top = null;
        var page = IntPtr.Zero;
        try
        {
            Rect Map(double x1, double y1, double x2, double y2)
            {
                if (page == IntPtr.Zero)
                {
                    page = PdfiumNative.FPDF_LoadPage(handle, pageIndex);
                    if (page == IntPtr.Zero)
                        throw new PdfDocumentException(string.Format(PdfViewerStrings.ErrorPageLoadFormat, pageIndex + 1), 6);
                }
                return new PageCoordinates(page, pages[pageIndex]).GetBounds(x1, y1, x2, y2);
            }

            if (PdfiumNative.FPDFDest_GetLocationInPage(destination, out var hasX, out var hasY, out var hasZoom,
                    out var x, out var y, out var z) != 0)
            {
                if (fit == PdfDestinationFit.Unknown) fit = PdfDestinationFit.Xyz;
                if (hasX != 0 && hasY != 0 && float.IsFinite(x) && float.IsFinite(y))
                {
                    // A degenerate rectangle maps the single point; GetBounds clamps it to the page.
                    position = Map(x, y, x, y).Position;
                }
                else if (fit == PdfDestinationFit.Xyz)
                {
                    // /XYZ with a single coordinate keeps the defined axis; the other axis stays
                    // where it is. Which display axis a user-space axis controls follows the page's
                    // built-in rotation, so probe the mapping and file the coordinate under Left
                    // (display X) or Top (display Y) accordingly.
                    if (hasX != 0 && float.IsFinite(x))
                    {
                        var a = Map(x, 0, x, 0);
                        var b = Map(x + 1, 0, x + 1, 0);
                        if (Math.Abs(a.X - b.X) >= Math.Abs(a.Y - b.Y)) left = a.X;
                        else top = a.Y;
                    }
                    if (hasY != 0 && float.IsFinite(y))
                    {
                        var a = Map(0, y, 0, y);
                        var b = Map(0, y + 1, 0, y + 1);
                        if (Math.Abs(a.Y - b.Y) >= Math.Abs(a.X - b.X)) top = a.Y;
                        else left = a.X;
                    }
                }
                if (hasZoom != 0 && float.IsFinite(z) && z > 0) zoom = z;
            }
            if (fit == PdfDestinationFit.FitR && count >= 4 &&
                float.IsFinite(parameters[0]) && float.IsFinite(parameters[1]) &&
                float.IsFinite(parameters[2]) && float.IsFinite(parameters[3]))
            {
                var mapped = Map(parameters[0], parameters[1], parameters[2], parameters[3]);
                if (mapped.Width > 0 && mapped.Height > 0) rectangle = mapped;
            }
            else if (fit is PdfDestinationFit.FitH or PdfDestinationFit.FitBH && count >= 1 && float.IsFinite(parameters[0]))
            {
                // The single edge coordinate maps like an /XYZ axis value (evaluated at the other
                // axis' page-space origin); the displayed Y is what the viewer pins to the window top.
                top = Map(0, parameters[0], 0, parameters[0]).Y;
            }
            else if (fit is PdfDestinationFit.FitV or PdfDestinationFit.FitBV && count >= 1 && float.IsFinite(parameters[0]))
            {
                left = Map(parameters[0], 0, parameters[0], 0).X;
            }
        }
        finally
        {
            if (page != IntPtr.Zero) PdfiumNative.FPDF_ClosePage(page);
        }
        return new PdfLinkDestination(pageIndex, position, zoom, fit, rectangle, left, top);
    }

    private static PdfDestinationFit ToFit(uint view) => view switch
    {
        PdfiumNative.DestViewXyz => PdfDestinationFit.Xyz,
        PdfiumNative.DestViewFit => PdfDestinationFit.Fit,
        PdfiumNative.DestViewFitH => PdfDestinationFit.FitH,
        PdfiumNative.DestViewFitV => PdfDestinationFit.FitV,
        PdfiumNative.DestViewFitR => PdfDestinationFit.FitR,
        PdfiumNative.DestViewFitB => PdfDestinationFit.FitB,
        PdfiumNative.DestViewFitBH => PdfDestinationFit.FitBH,
        PdfiumNative.DestViewFitBV => PdfDestinationFit.FitBV,
        _ => PdfDestinationFit.Unknown,
    };

    // Called from FinishLoad inside the scheduler, before the instance exists.
    private static string[] ReadPageLabels(IntPtr handle, int count, CancellationToken token)
    {
        var labels = new string[count];
        for (var index = 0; index < count; index++)
        {
            if ((index & 63) == 0) token.ThrowIfCancellationRequested();
            labels[index] = ReadPageLabel(handle, index);
        }
        return labels;
    }

    private static string ReadPageLabel(IntPtr handle, int pageIndex)
    {
        var length = PdfiumNative.FPDF_GetPageLabel(handle, pageIndex, null, 0);
        if (length <= 2) return "";
        if (length > 2 * 1024 * 1024 || (length & 1) != 0)
            throw new PdfDocumentException(PdfViewerStrings.ErrorPageLabelInvalid, 3);
        var buffer = new byte[length];
        var actual = PdfiumNative.FPDF_GetPageLabel(handle, pageIndex, buffer, (uint)buffer.Length);
        if (actual == 0) return "";
        var bytes = (int)Math.Min(actual, length) / 2 * 2;
        return Encoding.Unicode.GetString(buffer, 0, bytes).TrimEnd('\0');
    }
}
