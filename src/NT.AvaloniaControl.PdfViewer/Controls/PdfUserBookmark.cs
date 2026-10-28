using System.ComponentModel;
using Avalonia.Media;

namespace NT.AvaloniaControl.PdfViewer;

[Flags]
public enum PdfUserBookmarkFlags { None = 0, Bold = 1, Italic = 2 }

/// <summary>
/// A session-scoped bookmark created by the user, modeled separately from the document outline.
/// Bookmarks live until the document closes or the viewer is disposed; PDF persistence is a later
/// editing stage. PageIndex is the zero-based page captured when the bookmark was created.
/// </summary>
public sealed class PdfUserBookmark : INotifyPropertyChanged
{
    private string _title;
    private PdfUserBookmarkFlags _flags;

    internal PdfUserBookmark(string title, int pageIndex, PdfUserBookmarkFlags flags, Guid? id = null)
    {
        Id = id ?? Guid.NewGuid();
        _title = title;
        PageIndex = pageIndex;
        _flags = flags;
    }

    public Guid Id { get; } = Guid.NewGuid();
    public int PageIndex { get; }
    /// <summary>The one-based page number for display.</summary>
    public int PageNumber => PageIndex + 1;

    public string Title
    {
        get => _title;
        internal set
        {
            if (_title == value) return;
            _title = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
        }
    }

    public PdfUserBookmarkFlags Flags
    {
        get => _flags;
        internal set
        {
            if (_flags == value) return;
            _flags = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Flags)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBold)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsItalic)));
            // The theme binds FontWeight / FontStyle through Weight / Style, so those
            // computed properties must notify as well or bold / italic never refresh.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Weight)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Style)));
        }
    }

    public bool IsBold => _flags.HasFlag(PdfUserBookmarkFlags.Bold);
    public bool IsItalic => _flags.HasFlag(PdfUserBookmarkFlags.Italic);
    public FontWeight Weight => IsBold ? FontWeight.Bold : FontWeight.Normal;
    public FontStyle Style => IsItalic ? FontStyle.Italic : FontStyle.Normal;

    public event PropertyChangedEventHandler? PropertyChanged;
}
