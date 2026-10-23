namespace NT.AvaloniaControl.PdfViewer.Editing;

/// <summary>A deliberate, destructive policy for document data outside retained page painting.</summary>
public enum PdfHiddenInformationPolicy
{
    /// <summary>
    /// Rebuild only supported page painting and geometry. Remove all annotations (including their
    /// visible appearances), widgets, form fields, outlines, attachments, actions, document information,
    /// original metadata, logical structure, thumbnails, private dictionaries and unused resources.
    /// Forms and annotations are removed, not flattened. Unsupported painting resources are refused.
    /// </summary>
    RemoveAllNonPageData = 1
}

/// <summary>
/// A deletion region in PDF user-space points, with a bottom-left origin, before CropBox and page
/// rotation. Every intersecting page object must be wholly covered by an individual region.
/// Partial text, image, path and Form edits are refused before the session snapshot changes.
/// </summary>
public sealed record PdfRedactionRegion(int PageIndex, PdfEditRectangle Bounds);

/// <summary>Evidence identifying a committed, fully rewritten session snapshot.</summary>
/// <remarks>
/// The hashes identify bytes; they are not a general proof against arbitrary covert data in retained
/// fonts or painted images. Independent extraction, structural and render checks remain appropriate
/// for the caller's corpus. Save the session after this transaction to persist the result to disk.
/// </remarks>
public sealed record PdfSanitizationResult(long Revision, int PageCount, int RedactionRegionCount,
    PdfHiddenInformationPolicy Policy, string InputSha256, string OutputSha256);
