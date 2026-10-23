using Avalonia;
using Avalonia.Media.Imaging;

namespace NT.AvaloniaControl.Markdown;

public readonly record struct MarkdownHighlightSpan(int Start, int Length, string? Foreground = null, bool Bold = false, bool Italic = false);

/// <summary>A host-owned highlighter. Offsets refer to the unmodified UTF-16 code string.</summary>
public interface IMarkdownCodeHighlighter
{
    event EventHandler? Invalidated;
    ValueTask<IReadOnlyList<MarkdownHighlightSpan>> HighlightAsync(string code, string? language, CancellationToken cancellationToken);
}

/// <summary>The caller owns the returned lease, but never owns the loader itself.</summary>
public interface IMarkdownImageLoader
{
    ValueTask<MarkdownImageLease> LoadAsync(Uri uri, CancellationToken cancellationToken);
}

public sealed class MarkdownImageLease(Bitmap bitmap, bool ownsBitmap = true) : IDisposable
{
    private int _disposed;
    public Bitmap Bitmap { get; } = bitmap ?? throw new ArgumentNullException(nameof(bitmap));
    public void Dispose() { if (Interlocked.Exchange(ref _disposed, 1) == 0 && ownsBitmap) Bitmap.Dispose(); }
}

/// <summary>Overrides are explicit: a specified null disables that element's service.</summary>
public sealed record MarkdownServiceOverrides
{
    public bool OverrideImageLoader { get; init; }
    public IMarkdownImageLoader? ImageLoader { get; init; }
    public bool OverrideCodeHighlighter { get; init; }
    public IMarkdownCodeHighlighter? CodeHighlighter { get; init; }
}

/// <summary>Pure node identity and source metadata supplied to an element service rule.</summary>
public sealed record MarkdownElementDescriptor(MarkdownBlock Block, MarkdownInline? Inline);

public sealed class MarkdownElementPreparedEventArgs(StyledElement element, MarkdownElementDescriptor descriptor) : EventArgs
{
    public StyledElement Element { get; } = element;
    public MarkdownElementDescriptor Descriptor { get; } = descriptor;
}

public readonly record struct MarkdownUpdateMetrics(long SourceVersion, int SourceLength, double ParseElapsedMilliseconds, double UiApplyElapsedMilliseconds, double LayoutElapsedMilliseconds);

public enum MarkdownCopyResult { Copied, EmptySelection, Cancelled, ClipboardUnavailable, Failed }

public sealed class MarkdownCopyEventArgs(string text) : EventArgs
{
    public string Text { get; } = text;
    public bool Cancel { get; set; }
}

/// <summary>An external link activation; opening its target is always the host's responsibility.</summary>
public sealed class MarkdownLinkEventArgs(string url, string? title) : EventArgs
{
    public string Url { get; } = url;
    public string? Title { get; } = title;
    /// <summary>
    /// Suppresses the control's default visited marking for this activation when true.
    /// It does not clear an earlier visit or control host navigation. The default is false.
    /// Footnote references navigate internally and do not raise this event.
    /// </summary>
    public bool Handled { get; set; }
}

public sealed class MarkdownErrorEventArgs(string category, Exception error) : EventArgs
{
    public string Category { get; } = category;
    public Exception Error { get; } = error;
}
