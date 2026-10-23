using Avalonia;
using Avalonia.Threading;
using Avalonia.Media;

namespace NT.AvaloniaControl.Markdown;

/// <summary>A code node whose local highlighter can override or disable the inherited service.</summary>
public class MarkdownCodeBlock : MarkdownBlockPresenter
{
    public static readonly StyledProperty<IMarkdownCodeHighlighter?> CodeHighlighterProperty = Markdown.CodeHighlighterProperty.AddOwner<MarkdownCodeBlock>();
    public static readonly StyledProperty<string?> LanguageProperty = AvaloniaProperty.Register<MarkdownCodeBlock, string?>(nameof(Language));
    public static readonly DirectProperty<MarkdownCodeBlock, bool> IsHighlightingProperty = AvaloniaProperty.RegisterDirect<MarkdownCodeBlock, bool>(nameof(IsHighlighting), node => node.IsHighlighting);
    private CancellationTokenSource? _request;
    private IMarkdownCodeHighlighter? _observed;
    private EventHandler? _invalidated;
    private long _version;
    private bool _mounted;
    private bool _highlighting;
    internal Markdown? Owner { get; set; }
    internal MarkdownTextPresenter? TextPresenter { get; set; }
    public string Code { get; internal set; } = string.Empty;
    public string? Language { get => GetValue(LanguageProperty); set => SetValue(LanguageProperty, value); }
    public IMarkdownCodeHighlighter? CodeHighlighter { get => GetValue(CodeHighlighterProperty); set => SetValue(CodeHighlighterProperty, value); }
    public bool IsHighlighting => _highlighting;

    public void Reload()
    {
        Dispatcher.UIThread.VerifyAccess();
        CancelRequest();
        if (TextPresenter is not { } text) return;
        text.Inlines!.Clear(); text.Text = Code;
        if (!_mounted || CodeHighlighter is not { } highlighter) return;
        _request = new CancellationTokenSource();
        SetAndRaise(IsHighlightingProperty, ref _highlighting, true);
        Owner?.ResourceRequestStarted();
        var ownerReference = Owner is { } owner ? new WeakReference<Markdown>(owner) : null;
        _ = HighlightAsync(new(this), ownerReference, StartHighlight(Owner, highlighter, Code, Language, _request.Token), _version, _request.Token);
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) { base.OnAttachedToVisualTree(e); _mounted = true; Subscribe(); Reload(); }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) { _mounted = false; Unsubscribe(); CancelRequest(); base.OnDetachedFromVisualTree(e); }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (!_mounted) return;
        if (change.Property == CodeHighlighterProperty) { Subscribe(); Reload(); }
        else if (change.Property == LanguageProperty) Reload();
    }
    private void Subscribe()
    {
        Unsubscribe();
        if (CodeHighlighter is not { } service) return;
        _observed = service;
        var nodeReference = new WeakReference<MarkdownCodeBlock>(this);
        var serviceReference = new WeakReference<IMarkdownCodeHighlighter>(service);
        _invalidated = (_, _) =>
        {
            if (!nodeReference.TryGetTarget(out var node)) return;
            var version = Volatile.Read(ref node._version);
            MarkdownUiDispatcher.Post(() =>
            {
                if (nodeReference.TryGetTarget(out var current) && serviceReference.TryGetTarget(out var observed) &&
                    current._mounted && version == current._version && ReferenceEquals(current._observed, observed) &&
                    ReferenceEquals(current.CodeHighlighter, observed)) current.Reload();
            });
        };
        service.Invalidated += _invalidated;
    }
    private void Unsubscribe() { if (_observed is { } service && _invalidated is { } handler) service.Invalidated -= handler; _observed = null; _invalidated = null; }
    private static Task<IReadOnlyList<MarkdownHighlightSpan>> StartHighlight(Markdown? owner, IMarkdownCodeHighlighter highlighter, string code, string? language, CancellationToken token) =>
        owner?.RunResourceAsync(cancellation => highlighter.HighlightAsync(code, language, cancellation), token) ?? Task.Run(async () => await highlighter.HighlightAsync(code, language, token).ConfigureAwait(false), token);

    private static async Task HighlightAsync(WeakReference<MarkdownCodeBlock> nodeReference, WeakReference<Markdown>? ownerReference, Task<IReadOnlyList<MarkdownHighlightSpan>> request, long version, CancellationToken token)
    {
        try
        {
            var ranges = await request.ConfigureAwait(false);
            if (ranges is null) throw new InvalidOperationException("The code highlighter returned a null result.");
            // A host list can throw during enumeration. Materialize its pure values before
            // touching the visible code so malformed results never clear the plain fallback.
            var ordered = ranges.OrderBy(range => range.Start).ToArray();
            await MarkdownUiDispatcher.InvokeAsync(() =>
            {
                if (!nodeReference.TryGetTarget(out var node) || !node._mounted || token.IsCancellationRequested || version != node._version || node.TextPresenter is not { } text) return;
                var code = node.Code;
                var anchor = node.Owner?.CaptureReadingAnchor();
                var runs = new List<MarkdownRun>();
                var offset = 0;
                foreach (var range in ordered)
                {
                    if (range.Start < offset || range.Length < 0 || range.Start > code.Length - range.Length) continue;
                    if (range.Start > offset) runs.Add(new MarkdownRun(code[offset..range.Start]));
                    var run = new MarkdownRun(code.Substring(range.Start, range.Length));
                    if (range.Foreground is { } color && Color.TryParse(color, out var parsed)) run.Foreground = new SolidColorBrush(parsed);
                    if (range.Bold) run.FontWeight = FontWeight.Bold;
                    if (range.Italic) run.FontStyle = FontStyle.Italic;
                    runs.Add(run); offset = range.Start + range.Length;
                }
                if (offset < code.Length) runs.Add(new MarkdownRun(code[offset..]));
                text.Text = null; text.Inlines!.Clear();
                foreach (var run in runs) text.Inlines.Add(run);
                node.SetAndRaise(IsHighlightingProperty, ref node._highlighting, false);
                node.Owner?.RestoreReadingAnchor(anchor);
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            await MarkdownUiDispatcher.InvokeAsync(() =>
            {
                if (!nodeReference.TryGetTarget(out var node) || !node._mounted || token.IsCancellationRequested || version != node._version) return;
                var anchor = node.Owner?.CaptureReadingAnchor();
                if (node.TextPresenter is { } text) { text.Inlines!.Clear(); text.Text = node.Code; }
                node.SetAndRaise(IsHighlightingProperty, ref node._highlighting, false);
                node.Owner?.RestoreReadingAnchor(anchor);
                node.Owner?.ReportError("Highlight", error);
            });
        }
        finally { if (ownerReference?.TryGetTarget(out var currentOwner) == true) currentOwner.ResourceRequestFinished(); }
    }
    private void CancelRequest() { _version++; if (_request is { } request) _ = MarkdownServiceCancellation.CancelAsync(request); _request = null; SetAndRaise(IsHighlightingProperty, ref _highlighting, false); }
}
