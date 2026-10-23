namespace NT.AvaloniaControl.PdfViewer.Editing;

/// <summary>A rectangle in PDF points with a bottom-left origin, before crop/rotation.</summary>
public readonly record struct PdfEditRectangle(double Left, double Bottom, double Width, double Height)
{
    internal void Validate()
    {
        if (!double.IsFinite(Left) || !double.IsFinite(Bottom) || !double.IsFinite(Width) || !double.IsFinite(Height) ||
            Width <= 0 || Height <= 0 || Width > 14400 || Height > 14400 || Math.Abs(Left) > 14400 || Math.Abs(Bottom) > 14400)
            throw new ArgumentOutOfRangeException(nameof(Width), "Use finite PDF coordinates and positive dimensions no larger than 14400 points.");
    }
}

/// <summary>
/// An owned copy of an explicitly supplied static TrueType font. The caller must
/// have permission to embed it. No system font lookup or fallback is performed.
/// </summary>
public sealed class PdfEmbeddedTrueTypeFont
{
    private readonly byte[] _data;
    /// <summary>Copies the font and automatically subsets each annotation's requested glyphs.</summary>
    public PdfEmbeddedTrueTypeFont(byte[] data) : this(data, PdfFontEmbeddingMode.AutomaticSubset) { }

    /// <summary>Copies a font and selects automatic subsetting or explicit complete embedding.</summary>
    public PdfEmbeddedTrueTypeFont(byte[] data, PdfFontEmbeddingMode embeddingMode)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length is < 12 or > 32 * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(data), "Font data must be between 12 bytes and 32 MiB.");
        if (embeddingMode is not (PdfFontEmbeddingMode.AutomaticSubset or PdfFontEmbeddingMode.Full))
            throw new ArgumentOutOfRangeException(nameof(embeddingMode));
        _data = (byte[])data.Clone();
        EmbeddingMode = embeddingMode;
    }
    /// <summary>Automatic subsetting honors the font's NoSubsetting flag by embedding it in full.</summary>
    public PdfFontEmbeddingMode EmbeddingMode { get; }
    internal byte[] Data => _data;
}

/// <summary>How an explicitly supplied font is embedded in a FreeText appearance.</summary>
public enum PdfFontEmbeddingMode
{
    /// <summary>Keep requested glyphs and composite dependencies; NoSubsetting fonts remain complete.</summary>
    AutomaticSubset = 0,
    /// <summary>Embed the complete supplied font; all other embedding and text restrictions still apply.</summary>
    Full = 1
}

/// <summary>A session-owned FreeText annotation identified by its persisted PDF /NM.</summary>
public sealed record PdfFreeTextAnnotation(Guid Id, int PageIndex, PdfEditRectangle Bounds, string Text);
