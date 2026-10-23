using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace NT.AvaloniaControl.Markdown;

/// <summary>Explicit opt-in loader for HTTP(S), file and avares. The host owns its lifetime.</summary>
public sealed class MarkdownImageLoader : IMarkdownImageLoader, IDisposable
{
    private readonly HttpClient _client;
    private readonly bool _ownsClient;
    public MarkdownImageLoader(HttpClient? client = null, int maximumBytes = 16 * 1024 * 1024, long maximumDecodedPixels = 32 * 1024 * 1024)
    {
        if (maximumBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        if (maximumDecodedPixels <= 0) throw new ArgumentOutOfRangeException(nameof(maximumDecodedPixels));
        MaximumBytes = maximumBytes;
        MaximumDecodedPixels = maximumDecodedPixels;
        _ownsClient = client is null;
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    }
    public int MaximumBytes { get; }
    public long MaximumDecodedPixels { get; }

    public async ValueTask<MarkdownImageLease> LoadAsync(Uri uri, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uri);
        cancellationToken.ThrowIfCancellationRequested();
        MemoryStream input;
        if (uri.Scheme is "http" or "https")
        {
            using var response = await _client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaximumBytes) throw new IOException("Image exceeds the configured byte limit.");
            await using var remote = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            input = await CopyBoundedAsync(remote, cancellationToken).ConfigureAwait(false);
        }
        else if (uri.IsFile)
        {
            await using var file = File.OpenRead(uri.LocalPath);
            input = await CopyBoundedAsync(file, cancellationToken).ConfigureAwait(false);
        }
        else if (uri.Scheme == "avares")
        {
            using var resource = AssetLoader.Open(uri);
            input = await CopyBoundedAsync(resource, cancellationToken).ConfigureAwait(false);
        }
        else throw new NotSupportedException($"Unsupported image URI scheme: {uri.Scheme}.");
        using (input)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // This is a best-effort size preflight, not image validation. Unknown,
            // incomplete or ambiguous headers retain the native decoder fallback.
            var exceedsKnownSize = input.TryGetBuffer(out var encoded) &&
                MarkdownImageDimensions.TryReadPixelCount(encoded.AsSpan(), out var pixels) && pixels > MaximumDecodedPixels;
            cancellationToken.ThrowIfCancellationRequested();
            if (exceedsKnownSize) throw new IOException("Decoded image exceeds the configured pixel limit.");
            var bitmap = new Bitmap(input);
            if ((long)bitmap.PixelSize.Width * bitmap.PixelSize.Height > MaximumDecodedPixels || cancellationToken.IsCancellationRequested)
            {
                bitmap.Dispose();
                cancellationToken.ThrowIfCancellationRequested();
                throw new IOException("Decoded image exceeds the configured pixel limit.");
            }
            return new MarkdownImageLease(bitmap);
        }
    }

    private async Task<MemoryStream> CopyBoundedAsync(Stream source, CancellationToken cancellationToken)
    {
        var output = new MemoryStream();
        try
        {
            var buffer = new byte[8192];
            int count;
            while ((count = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (output.Length + count > MaximumBytes) throw new IOException("Image exceeds the configured byte limit.");
                output.Write(buffer, 0, count);
            }
            output.Position = 0;
            return output;
        }
        catch { output.Dispose(); throw; }
    }
    public void Dispose() { if (_ownsClient) _client.Dispose(); }
}
