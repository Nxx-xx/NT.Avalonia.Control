using Avalonia;

namespace NT.AvaloniaControl.PdfViewer;

public partial class PdfViewer
{
    /// <summary>
    /// Adds a session bookmark at one page. The title defaults to the page's label ("第 N 页"
    /// when the document defines no label). Bookmarks are cleared when the document closes.
    /// </summary>
    public PdfUserBookmark AddUserBookmark(string? title = null, int? pageNumber = null, PdfUserBookmarkFlags flags = PdfUserBookmarkFlags.None)
    {
        VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!HasDocument) throw new InvalidOperationException(PdfViewerStrings.ErrorBookmarkRequiresDocument);
        var page = Math.Clamp(pageNumber ?? CurrentPage, 1, PageCount);
        var label = labelFor(page);
        var bookmark = new PdfUserBookmark(string.IsNullOrWhiteSpace(title) ? label : title, page - 1, flags);
        _userBookmarks.Add(bookmark);
        UpdateCommandParts();
        return bookmark;

        string labelFor(int number)
        {
            var raw = number <= _pageLabels.Count ? _pageLabels[number - 1] : "";
            return string.IsNullOrEmpty(raw) ? string.Format(PdfViewerStrings.BookmarkPageTitleFormat, number) : raw;
        }
    }

    /// <summary>Removes a bookmark by id. Returns false when the id is unknown.</summary>
    public bool RemoveUserBookmark(Guid id)
    {
        VerifyAccess();
        var index = _userBookmarks.ToList().FindIndex(bookmark => bookmark.Id == id);
        if (index < 0) return false;
        _userBookmarks.RemoveAt(index);
        UpdateCommandParts();
        return true;
    }

    /// <summary>Renames a bookmark. Returns false when the id is unknown.</summary>
    public bool RenameUserBookmark(Guid id, string title)
    {
        VerifyAccess();
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (FindUserBookmark(id) is not { } bookmark) return false;
        bookmark.Title = title;
        ViewerBookmarksChanged();
        return true;
    }

    /// <summary>Updates a bookmark's style flags. Returns false when the id is unknown.</summary>
    public bool SetUserBookmarkFlags(Guid id, PdfUserBookmarkFlags flags)
    {
        VerifyAccess();
        if (FindUserBookmark(id) is not { } bookmark) return false;
        bookmark.Flags = flags;
        ViewerBookmarksChanged();
        return true;
    }

    /// <summary>Removes every session bookmark. Called automatically when the document closes.</summary>
    public void ClearUserBookmarks()
    {
        VerifyAccess();
        if (_userBookmarks.Count == 0) return;
        _userBookmarks.Clear();
        UpdateCommandParts();
    }

    private PdfUserBookmark? FindUserBookmark(Guid id) => _userBookmarks.FirstOrDefault(bookmark => bookmark.Id == id);
}
