using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using NT.AvaloniaControl.Markdown;

namespace NT.AvaloniaControl.Demo.Pages.Markdown;

internal sealed partial class MarkdownPage
{
    // Invoked by the main native smoke after its window and renderer have been initialized.
    // This tests the actual default implementation and native decoder, using loopback only.
    private static async Task SmokeDefaultImageLoaderAsync(IDictionary<string, bool> checks,
        IDictionary<string, double> observations, ICollection<string> limitations, string smokeOutput,
        CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var token = timeout.Token;
        var directory = Path.GetDirectoryName(Path.GetFullPath(smokeOutput))!;
        Directory.CreateDirectory(directory);
        var fixturePath = Path.Combine(directory, "markdown-loader-fixture-" + Guid.NewGuid().ToString("N") + ".png");
        byte[] png;
        using (var encoded = new RenderTargetBitmap(new PixelSize(3, 2), new Vector(96, 96)))
        {
            var tile = new Border { Width = 3, Height = 2, Background = Brushes.MediumSeaGreen };
            tile.Measure(new Size(3, 2)); tile.Arrange(new Rect(0, 0, 3, 2));
            encoded.Render(tile);
            using var output = new MemoryStream();
            encoded.Save(output);
            png = output.ToArray();
        }
        await File.WriteAllBytesAsync(fixturePath, png, token);
        var fileUri = new Uri(fixturePath);
        var assetUri = new Uri("avares://NT.AvaloniaControl.Demo/Assets/MarkdownSmoke.png");
        observations["默认图片PNG编码输入bytes"] = png.Length;
        observations["默认图片PNG宽pixel"] = 3;
        observations["默认图片PNG高pixel"] = 2;

        using (var loader = new MarkdownImageLoader())
        {
            using var first = await loader.LoadAsync(fileUri, token);
            using var second = await loader.LoadAsync(fileUri, token);
            checks["默认图片file原生PNG解码与尺寸"] = ImageSmokeSize(first.Bitmap) && ImageSmokeCanSave(first.Bitmap);
            loader.Dispose();
            checks["默认图片loader释放保留调用方租约"] = ImageSmokeCanSave(first.Bitmap) && ImageSmokeCanSave(second.Bitmap);
            first.Dispose(); first.Dispose();
            checks["默认图片独立租约与重复释放"] = !ReferenceEquals(first.Bitmap, second.Bitmap) &&
                !ImageSmokeCanSave(first.Bitmap) && ImageSmokeCanSave(second.Bitmap);
            second.Dispose();
            checks["默认图片file租约释放原生bitmap"] = !ImageSmokeCanSave(second.Bitmap);
        }
        using (var loader = new MarkdownImageLoader())
        {
            using var asset = await loader.LoadAsync(assetUri, token);
            checks["默认图片host avares原生PNG解码与尺寸"] = ImageSmokeSize(asset.Bitmap) && ImageSmokeCanSave(asset.Bitmap);
            asset.Dispose(); asset.Dispose();
            checks["默认图片avares租约释放原生bitmap"] = !ImageSmokeCanSave(asset.Bitmap);
        }
        using (var bytes = new MarkdownImageLoader(maximumBytes: 4))
        {
            var rejected = await ImageSmokeRejectsLimitAsync(bytes, fileUri, "configured byte limit", token);
            using var reopened = File.Open(fixturePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            checks["默认图片file编码超限拒绝并关闭输入"] = rejected && reopened.Length == png.Length;
            checks["默认图片avares编码超限拒绝"] = await ImageSmokeRejectsLimitAsync(bytes, assetUri, "configured byte limit", token);
        }
        using (var pixels = new MarkdownImageLoader(maximumDecodedPixels: 4))
            checks["默认图片解码后6pixel超过4pixel拒绝"] = await ImageSmokeRejectsLimitAsync(pixels, fileUri, "configured pixel limit", token);

        // The supplied client is real and host-owned. Disabling its proxy guarantees that this
        // protocol probe cannot leave the loopback interface through a machine proxy setting.
        using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(5) };
        var server = new ImageSmokeLoopbackServer(png, token);
        try
        {
            using (var loader = new MarkdownImageLoader(client))
            {
                using var remote = await loader.LoadAsync(server.ImageUri, token);
                checks["默认图片回环HTTP原生PNG解码与尺寸"] = ImageSmokeSize(remote.Bitmap) && ImageSmokeCanSave(remote.Bitmap);
                loader.Dispose();
                using var afterDispose = await client.GetAsync(server.ImageUri, token);
                var returned = await afterDispose.Content.ReadAsByteArrayAsync(token);
                checks["默认图片HTTP保留宿主client和已有租约"] = afterDispose.IsSuccessStatusCode && returned.AsSpan().SequenceEqual(png) && ImageSmokeCanSave(remote.Bitmap);
                remote.Dispose(); remote.Dispose();
                checks["默认图片HTTP租约释放原生bitmap"] = !ImageSmokeCanSave(remote.Bitmap);
            }
            using (var bytes = new MarkdownImageLoader(client, maximumBytes: 4))
            {
                checks["默认图片HTTP声明编码超限拒绝"] = await ImageSmokeRejectsLimitAsync(bytes, server.ImageUri, "configured byte limit", token);
                checks["默认图片HTTP分块编码超限拒绝"] = await ImageSmokeRejectsLimitAsync(bytes, server.ChunkedUri, "configured byte limit", token);
            }
            observations["默认图片真实回环HTTP请求次数"] = server.RequestCount;
        }
        finally { await server.DisposeAsync(); }
        checks["默认图片回环服务已取消并关闭"] = server.IsStopped;
        limitations.Add("默认图片协议探针使用原生编码小 PNG、file、宿主 avares 和 127.0.0.1 动态端口；真实公网 HTTP/HTTPS 图片与网络环境仍待验证。HTTP 客户端为真实无代理的宿主客户端。");
    }

    private static bool ImageSmokeSize(Bitmap bitmap) => bitmap.PixelSize == new PixelSize(3, 2);
    private static bool ImageSmokeCanSave(Bitmap bitmap)
    {
        try
        {
            using var output = new MemoryStream(); bitmap.Save(output);
            return output.Length >= 8 && output.GetBuffer().AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        }
        catch (Exception error) when (error is ObjectDisposedException or InvalidOperationException or NullReferenceException) { return false; }
    }
    private static async Task<bool> ImageSmokeRejectsLimitAsync(MarkdownImageLoader loader, Uri uri, string message, CancellationToken token)
    {
        try { using var unexpected = await loader.LoadAsync(uri, token); return false; }
        catch (IOException error) when (error.Message.Contains(message, StringComparison.Ordinal)) { return true; }
    }

    private sealed class ImageSmokeLoopbackServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _cancellation;
        private readonly Task _worker;
        private readonly byte[] _png;
        private int _requests;
        private int _stopped;
        public ImageSmokeLoopbackServer(byte[] png, CancellationToken cancellationToken)
        {
            _png = png;
            _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            try
            {
                _listener.Start(4);
                var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
                ImageUri = new Uri("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/image.png");
                ChunkedUri = new Uri(ImageUri, "/chunked.png");
                _worker = Task.Run(ServeAsync, CancellationToken.None);
            }
            catch { _listener.Stop(); _cancellation.Dispose(); throw; }
        }
        public Uri ImageUri { get; }
        public Uri ChunkedUri { get; }
        public int RequestCount => Volatile.Read(ref _requests);
        public bool IsStopped => Volatile.Read(ref _stopped) != 0 && _worker.IsCompleted;

        private async Task ServeAsync()
        {
            var token = _cancellation.Token;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    using var connection = await _listener.AcceptTcpClientAsync(token);
                    await using var stream = connection.GetStream();
                    var request = await ReadHeaderAsync(stream, token);
                    if (!request.StartsWith("GET /", StringComparison.Ordinal)) throw new InvalidDataException("The image smoke server accepts GET requests only.");
                    Interlocked.Increment(ref _requests);
                    var chunked = request.StartsWith("GET /chunked.png ", StringComparison.Ordinal);
                    using var response = new MemoryStream();
                    var header = "HTTP/1.1 200 OK\r\nContent-Type: image/png\r\nConnection: close\r\n" +
                        (chunked ? "Transfer-Encoding: chunked\r\n" : "Content-Length: " + _png.Length.ToString(CultureInfo.InvariantCulture) + "\r\n") + "\r\n";
                    response.Write(Encoding.ASCII.GetBytes(header));
                    if (chunked) response.Write(Encoding.ASCII.GetBytes(_png.Length.ToString("X", CultureInfo.InvariantCulture) + "\r\n"));
                    response.Write(_png);
                    if (chunked) response.Write(Encoding.ASCII.GetBytes("\r\n0\r\n\r\n"));
                    try { await stream.WriteAsync(response.GetBuffer().AsMemory(0, checked((int)response.Length)), token); }
                    catch (IOException) when (!token.IsCancellationRequested) { /* A limit-rejecting client may close after the header. */ }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (SocketException) when (token.IsCancellationRequested) { }
            finally { _listener.Stop(); Interlocked.Exchange(ref _stopped, 1); }
        }
        private static async Task<string> ReadHeaderAsync(NetworkStream stream, CancellationToken token)
        {
            var request = new StringBuilder(); var buffer = new byte[1]; uint tail = 0;
            while (request.Length < 8192)
            {
                if (await stream.ReadAsync(buffer, token) == 0) throw new EndOfStreamException("Image smoke request ended before its header.");
                request.Append((char)buffer[0]); tail = (tail << 8) | buffer[0];
                if (tail == 0x0D0A0D0A) return request.ToString();
            }
            throw new InvalidDataException("Image smoke request header exceeded 8 KiB.");
        }
        public async ValueTask DisposeAsync()
        {
            try
            {
                await _cancellation.CancelAsync();
                _listener.Stop();
                await _worker.WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally { _listener.Stop(); _cancellation.Dispose(); }
        }
    }
}
