using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace NT.AvaloniaControl.Markdown;

/// <summary>A styleable image node with inherited or explicit local loader ownership.</summary>
public class MarkdownImage : Border
{
    public static readonly StyledProperty<IMarkdownImageLoader?> ImageLoaderProperty = Markdown.ImageLoaderProperty.AddOwner<MarkdownImage>();
    public static readonly StyledProperty<Uri?> BaseUriProperty = Markdown.BaseUriProperty.AddOwner<MarkdownImage>();
    public static readonly StyledProperty<string?> SourceProperty = AvaloniaProperty.Register<MarkdownImage, string?>(nameof(Source));
    public static readonly DirectProperty<MarkdownImage, bool> IsLoadingProperty = AvaloniaProperty.RegisterDirect<MarkdownImage, bool>(nameof(IsLoading), node => node.IsLoading);
    public static readonly DirectProperty<MarkdownImage, string?> LoadErrorProperty = AvaloniaProperty.RegisterDirect<MarkdownImage, string?>(nameof(LoadError), node => node.LoadError);
    private readonly Image _image = new() { MaxHeight = 600, Stretch = Stretch.Uniform, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
    private CancellationTokenSource? _request;
    private MarkdownImageLease? _lease;
    private long _version;
    private bool _mounted;
    private bool _loading;
    private string? _error;

    public MarkdownImage() { Classes.Add("image"); Child = _image; }
    internal Markdown? Owner { get; set; }
    public long NodeId { get; internal set; }
    public int TextStart { get; internal set; }
    public string AltText { get; internal set; } = string.Empty;
    public IMarkdownImageLoader? ImageLoader { get => GetValue(ImageLoaderProperty); set => SetValue(ImageLoaderProperty, value); }
    public Uri? BaseUri { get => GetValue(BaseUriProperty); set => SetValue(BaseUriProperty, value); }
    public string? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }
    public bool IsLoading => _loading;
    public string? LoadError => _error;

    public void Reload()
    {
        Dispatcher.UIThread.VerifyAccess();
        var anchor = Owner?.CaptureReadingAnchor();
        var removedImage = _lease is not null;
        CancelRequest();
        _image.Source = null;
        _lease?.Dispose(); _lease = null;
        SetAndRaise(LoadErrorProperty, ref _error, null);
        if (!_mounted || ImageLoader is not { } loader || Source is not { Length: > 0 } source) { if (removedImage) Owner?.RestoreReadingAnchor(anchor); return; }
        var uri = MarkdownUriResolver.Resolve(source, BaseUri);
        if (uri is null) { SetAndRaise(LoadErrorProperty, ref _error, "A relative image requires BaseUri."); return; }
        _request = new CancellationTokenSource();
        SetAndRaise(IsLoadingProperty, ref _loading, true);
        Owner?.ResourceRequestStarted();
        var ownerReference = Owner is { } owner ? new WeakReference<Markdown>(owner) : null;
        _ = LoadAsync(new(this), ownerReference, StartLoad(Owner, loader, uri, _request.Token), _version, _request.Token);
        if (removedImage) Owner?.RestoreReadingAnchor(anchor);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) { base.OnAttachedToVisualTree(e); _mounted = true; Reload(); }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _mounted = false; CancelRequest(); _image.Source = null; _lease?.Dispose(); _lease = null;
        base.OnDetachedFromVisualTree(e);
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (_mounted && (change.Property == SourceProperty || change.Property == BaseUriProperty || change.Property == ImageLoaderProperty)) Reload();
    }

    private static Task<MarkdownImageLease> StartLoad(Markdown? owner, IMarkdownImageLoader loader, Uri uri, CancellationToken token) =>
        owner?.RunResourceAsync(cancellation => loader.LoadAsync(uri, cancellation), token) ?? Task.Run(async () => await loader.LoadAsync(uri, token).ConfigureAwait(false), token);

    private static async Task LoadAsync(WeakReference<MarkdownImage> nodeReference, WeakReference<Markdown>? ownerReference, Task<MarkdownImageLease> request, long version, CancellationToken token)
    {
        MarkdownImageLease? lease = null;
        try
        {
            lease = await request.ConfigureAwait(false);
            await MarkdownUiDispatcher.InvokeAsync(() =>
            {
                if (!nodeReference.TryGetTarget(out var node) || !node._mounted || token.IsCancellationRequested || version != node._version) return;
                var anchor = node.Owner?.CaptureReadingAnchor();
                node._lease = lease; node._image.Source = lease.Bitmap; lease = null;
                node.SetAndRaise(IsLoadingProperty, ref node._loading, false);
                node.Owner?.RestoreReadingAnchor(anchor);
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            await MarkdownUiDispatcher.InvokeAsync(() =>
            {
                if (!nodeReference.TryGetTarget(out var node) || !node._mounted || token.IsCancellationRequested || version != node._version) return;
                node.SetAndRaise(IsLoadingProperty, ref node._loading, false); node.SetAndRaise(LoadErrorProperty, ref node._error, error.Message);
                node.Owner?.ReportError("Image", error);
            });
        }
        finally { lease?.Dispose(); if (ownerReference?.TryGetTarget(out var currentOwner) == true) currentOwner.ResourceRequestFinished(); }
    }
    private void CancelRequest()
    {
        _version++;
        if (_request is { } request) _ = MarkdownServiceCancellation.CancelAsync(request);
        _request = null;
        SetAndRaise(IsLoadingProperty, ref _loading, false);
    }
}
