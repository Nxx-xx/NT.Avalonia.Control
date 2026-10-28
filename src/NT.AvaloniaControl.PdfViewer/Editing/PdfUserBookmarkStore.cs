using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;

namespace NT.AvaloniaControl.PdfViewer.Editing;

/// <summary>
/// Persists session user bookmarks in a private catalog array (/NTUserBookmarks). The block
/// belongs to this package's namespace; other readers ignore unknown catalog entries. The edit
/// session strips the block into overlay state at open and injects it back at the output
/// boundary, so content snapshots and undo history never contain it.
/// </summary>
internal static class PdfUserBookmarkStore
{
    internal const string CatalogKey = "/NTUserBookmarks";
    internal const int MaximumTitleLength = 512;

    /// <summary>Parses the private block; returns an empty list when the key is absent. Malformed entries are rejected.</summary>
    internal static (bool Present, IReadOnlyList<PdfEditUserBookmark> Bookmarks) Read(byte[] input, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(input, writable: false);
        using var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
        var catalog = document.Internals.Catalog;
        var stored = Dereference(catalog.Elements[CatalogKey]);
        if (stored is null) return (false, []);
        if (stored is not PdfArray array)
            throw new InvalidDataException("The persisted NT user bookmark block is not an array.");
        var result = new List<PdfEditUserBookmark>();
        var ids = new HashSet<Guid>();
        foreach (var element in array.Elements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Dereference(element) is not PdfDictionary entry)
                throw new InvalidDataException("The persisted NT user bookmark block contains a non-dictionary entry.");
            var idText = entry.Elements.GetString("/Id");
            if (string.IsNullOrEmpty(idText) || !Guid.TryParseExact(idText, "D", out var id) || !ids.Add(id))
                throw new InvalidDataException("The persisted NT user bookmark block contains an invalid or duplicate ID.");
            var title = entry.Elements.GetString("/Title");
            if (title is null || title.Length is 0 or > MaximumTitleLength || title.Contains('\0'))
                throw new InvalidDataException("The persisted NT user bookmark block contains an invalid title.");
            if (Dereference(entry.Elements["/Page"]) is not PdfInteger page || page.Value < 0)
                throw new InvalidDataException("The persisted NT user bookmark block contains an invalid page.");
            if (Dereference(entry.Elements["/Flags"]) is not PdfInteger flags)
                throw new InvalidDataException("The persisted NT user bookmark block contains an invalid flag value.");
            result.Add(new PdfEditUserBookmark(id, title, page.Value, (PdfUserBookmarkFlags)flags.Value));
        }
        return (true, result);
    }

    /// <summary>Removes the private block from the content bytes when present; otherwise returns the input unchanged.</summary>
    internal static (byte[] Bytes, IReadOnlyList<PdfEditUserBookmark> Bookmarks) Strip(byte[] input, CancellationToken cancellationToken)
    {
        var (present, bookmarks) = Read(input, cancellationToken);
        if (!present) return (input, bookmarks);
        using var inputStream = new MemoryStream(input, writable: false);
        using var target = PdfReader.Open(inputStream, PdfDocumentOpenMode.Modify);
        target.Internals.Catalog.Elements.Remove(CatalogKey);
        cancellationToken.ThrowIfCancellationRequested();
        using var output = new MemoryStream();
        target.Save(output, closeStream: false);
        cancellationToken.ThrowIfCancellationRequested();
        if (output.Length > PdfStructureWriter.MaximumDocumentBytes)
            throw new InvalidDataException("The edited PDF exceeds the 256 MiB structural writer limit.");
        return (output.ToArray(), bookmarks);
    }

    /// <summary>Writes the private block into a fresh byte copy; an empty set returns the input unchanged.</summary>
    internal static byte[] Inject(byte[] input, IReadOnlyList<PdfEditUserBookmark> bookmarks, CancellationToken cancellationToken)
    {
        if (bookmarks.Count == 0) return input;
        using var inputStream = new MemoryStream(input, writable: false);
        using var target = PdfReader.Open(inputStream, PdfDocumentOpenMode.Modify);
        var array = new PdfArray(target);
        foreach (var bookmark in bookmarks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = new PdfDictionary(target);
            entry.Elements.SetString("/Id", bookmark.Id.ToString("D"));
            entry.Elements["/Title"] = new PdfString(bookmark.Title, PdfStringEncoding.Unicode);
            entry.Elements.SetInteger("/Page", bookmark.PageIndex);
            entry.Elements.SetInteger("/Flags", (int)bookmark.Flags);
            array.Elements.Add(entry);
        }
        target.Internals.Catalog.Elements[CatalogKey] = array;
        cancellationToken.ThrowIfCancellationRequested();
        using var output = new MemoryStream();
        target.Save(output, closeStream: false);
        cancellationToken.ThrowIfCancellationRequested();
        if (output.Length > PdfStructureWriter.MaximumDocumentBytes)
            throw new InvalidDataException("The edited PDF exceeds the 256 MiB structural writer limit.");
        return output.ToArray();
    }

    private static PdfItem? Dereference(PdfItem? item) => item is PdfReference reference ? reference.Value : item;
}
