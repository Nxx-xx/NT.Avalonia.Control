using System.Buffers.Binary;
using System.Net;
using System.Reflection;
using NT.AvaloniaControl.Markdown;

namespace Markdown.Headless.Tests;

public sealed class ImageLoaderPreflightTests
{
    private delegate bool ReadPixelCount(ReadOnlySpan<byte> encoded, out long pixels);
    private static readonly ReadPixelCount Read = typeof(MarkdownImageLoader).Assembly
        .GetType("NT.AvaloniaControl.Markdown.MarkdownImageDimensions", throwOnError: true)!
        .GetMethod("TryReadPixelCount", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<ReadPixelCount>();
    private static readonly byte[] SmallPng = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAMAAAACCAYAAACddGYaAAAAAklEQVR4nGKkkSsAAAAJSURBVGNgwAUAABoAAe5TtIEAAAAASUVORK5CYII=");
    private static readonly byte[] LargePngHeader = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAIAAAACAACAYAAAByqspZ");
    private static readonly byte[] NativeEncodedJpeg = Convert.FromBase64String("/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAMCAgMCAgMDAwMEAwMEBQgFBQQEBQoHBwYIDAoMDAsKCwsNDhIQDQ4RDgsLEBYQERMUFRUVDA8XGBYUGBIUFRT/2wBDAQMEBAUEBQkFBQkUDQsNFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBT/wAARCAACAAMDASIAAhEBAxEB/8QAFQABAQAAAAAAAAAAAAAAAAAAAAf/xAAUEAEAAAAAAAAAAAAAAAAAAAAA/8QAFQEBAQAAAAAAAAAAAAAAAAAABgj/xAAUEQEAAAAAAAAAAAAAAAAAAAAA/9oADAMBAAIRAxEAPwCNAHaVH//Z");

    [Fact]
    public void KnownNativePngAndJpegDimensionsAreReadWithoutResizing()
    {
        Assert.True(Read(SmallPng, out var png)); Assert.Equal(6, png);
        Assert.True(Read(NativeEncodedJpeg, out var jpeg)); Assert.Equal(6, jpeg);
        Assert.True(Read(LargePngHeader, out var large)); Assert.Equal(67_108_864, large);
    }

    [Fact]
    public void EveryIncompletePngHeaderAndBadCrcUseNativeFallback()
    {
        for (var length = 0; length < 33; length++) AssertUnknown(SmallPng.AsSpan(0, length));
        var badCrc = SmallPng.ToArray(); badCrc[32] ^= 1; AssertUnknown(badCrc);
        var badLength = SmallPng.ToArray(); badLength[11] = 12; AssertUnknown(badLength);
        var badKind = SmallPng.ToArray(); badKind[12] = (byte)'J'; AssertUnknown(badKind);
    }

    [Fact]
    public void PngPixelArithmeticIsLongAndInvalidNativeDimensionsRemainUnknown()
    {
        // Complete IHDRs with independently generated zlib CRCs, not complete image payloads.
        var maximum = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUn////9/////CAYAAAAUyQtm");
        Assert.True(Read(maximum, out var pixels)); Assert.Equal((long)int.MaxValue * int.MaxValue, pixels);
        AssertUnknown(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAAAAAABCAYAAADw16+3"));
        AssertUnknown(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUoAAAAAAAAABCAYAAABQvYqg"));
    }

    [Fact]
    public void DimensionProbeHonorsTheSuppliedSpanAndDoesNotValidatePayload()
    {
        var padded = new byte[SmallPng.Length + 17]; SmallPng.CopyTo(padded, 9);
        Assert.True(Read(padded.AsSpan(9, SmallPng.Length), out var pixels)); Assert.Equal(6, pixels);
        AssertUnknown(padded);
        // Only metadata is known here. The loader must still call Bitmap for in-budget images.
        Assert.True(Read(SmallPng.AsSpan(0, 33), out pixels)); Assert.Equal(6, pixels);
    }

    [Theory]
    [InlineData(0xc0, 1)]
    [InlineData(0xc0, 3)]
    [InlineData(0xc0, 4)]
    [InlineData(0xc1, 3)]
    [InlineData(0xc2, 3)]
    public void StandardJpegFrameHeadersHaveUnscaledDimensions(int marker, int components)
    {
        Assert.True(Read(JpegHeader((byte)marker, 3, 2, components: components), out var pixels));
        Assert.Equal(6, pixels);
    }

    [Fact]
    public void JpegSkipsLengthDelimitedApplicationDataIncludingFalseFrameMarkers()
    {
        byte[] application = [0xff, 0xd8, 0xff, 0xe1, 0, 8, 0xff, 0xc0, 0xff, 0xda, 0xff, 0xd9];
        var encoded = application.Concat(JpegHeader()[2..]).ToArray();
        Assert.True(Read(encoded, out var pixels)); Assert.Equal(6, pixels);
        byte[] filled = [0xff, 0xd8, 0xff, 0xff];
        Assert.True(Read(filled.Concat(JpegHeader()[3..]).ToArray(), out pixels)); Assert.Equal(6, pixels);
    }

    [Fact]
    public void EveryIncompleteJpegFrameHeaderUsesNativeFallback()
    {
        var frame = JpegHeader();
        for (var length = 0; length < frame.Length; length++) AssertUnknown(frame.AsSpan(0, length));
        Assert.True(Read(frame, out _));
        var badLength = frame.ToArray(); badLength[5] = 1; AssertUnknown(badLength);
        var missingComponent = frame.ToArray(); missingComponent[11] = 4; AssertUnknown(missingComponent);
    }

    [Fact]
    public void JpegNeverSearchesEntropyDataOrALaterImageForAFrame()
    {
        foreach (var marker in new byte[] { 0xda, 0xd9, 0xd8, 0 })
        {
            byte[] prefix = [0xff, 0xd8, 0xff, marker, 0, 2];
            AssertUnknown(prefix.Concat(JpegHeader()).ToArray());
        }
    }

    [Fact]
    public void JpegDnlOtherPrecisionsComponentsAndFrameProcessesRemainNative()
    {
        AssertUnknown(JpegHeader(height: 0)); AssertUnknown(JpegHeader(width: 0));
        AssertUnknown(JpegHeader(precision: 12)); AssertUnknown(JpegHeader(components: 2));
        foreach (var marker in new byte[] { 0xc3, 0xc5, 0xc6, 0xc7, 0xc9, 0xca, 0xcb, 0xcd, 0xce, 0xcf }) AssertUnknown(JpegHeader(marker));
        Assert.True(Read(JpegHeader(width: ushort.MaxValue, height: ushort.MaxValue), out var maximum));
        Assert.Equal((long)ushort.MaxValue * ushort.MaxValue, maximum);
    }

    [Theory]
    [InlineData(0xde)]
    [InlineData(0xdf)]
    public void JpegHierarchicalProgressionAndExpansionDoNotBorrowALaterFrameSize(int marker)
    {
        byte[] prefix = [0xff, 0xd8, 0xff, (byte)marker, 0, 2];
        AssertUnknown(prefix.Concat(JpegHeader()[2..]).ToArray());
    }

    [Fact]
    public void UnknownFormatsAndMalformedMarkersRemainNativeDecoderResponsibilities()
    {
        foreach (var encoded in new byte[][] { "BM-bitmap"u8.ToArray(), "GIF89a"u8.ToArray(), "RIFF-webp"u8.ToArray(), [0, 0, 1, 0], [0xff, 0xd8, 0xff], [0xff, 0xd8, 0x12], [0xff, 0xd8, 0xff, 0xe1, 0xff, 0xff], [0xff, 0xd8, 0xff, 0xe1, 0, 0] })
            AssertUnknown(encoded);
    }

    [Fact]
    public async Task KnownOversizedHeaderRejectsBeforeRequiringABitmapAndClosesFileInput()
    {
        var path = Path.Combine(Path.GetTempPath(), "markdown-image-preflight-" + Guid.NewGuid().ToString("N"));
        try
        {
            await File.WriteAllBytesAsync(path, LargePngHeader, TestContext.Current.CancellationToken);
            using var loader = new MarkdownImageLoader();
            var error = await Assert.ThrowsAsync<IOException>(async () => await loader.LoadAsync(new Uri(path), TestContext.Current.CancellationToken));
            Assert.Contains("configured pixel limit", error.Message, StringComparison.Ordinal);
            using var reopened = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.Equal(LargePngHeader.Length, reopened.Length);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task PreflightFailureClosesHttpInputAndDoesNotOwnTheBorrowedClient()
    {
        using var stream = new MemoryStream(LargePngHeader);
        using var handler = new Handler(stream);
        using var client = new HttpClient(handler);
        using var loader = new MarkdownImageLoader(client);
        await Assert.ThrowsAsync<IOException>(async () => await loader.LoadAsync(new Uri("https://fixture.test/large.png"), TestContext.Current.CancellationToken));
        Assert.False(stream.CanRead);
        loader.Dispose();
        using var response = await client.GetAsync("https://fixture.test/after", TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode); Assert.False(handler.Disposed); Assert.Equal(2, handler.Calls);
    }

    private static void AssertUnknown(ReadOnlySpan<byte> encoded) { Assert.False(Read(encoded, out var pixels)); Assert.Equal(0, pixels); }
    private static byte[] JpegHeader(byte marker = 0xc0, ushort width = 3, ushort height = 2, byte precision = 8, int components = 3)
    {
        var length = 8 + 3 * components;
        var bytes = new byte[4 + length]; bytes[0] = 0xff; bytes[1] = 0xd8; bytes[2] = 0xff; bytes[3] = marker;
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(4), (ushort)length);
        bytes[6] = precision; BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(7), height); BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(9), width);
        bytes[11] = (byte)components;
        for (var i = 0; i < components; i++) { bytes[12 + 3 * i] = (byte)(i + 1); bytes[13 + 3 * i] = 0x11; }
        return bytes;
    }
    private sealed class Handler(Stream first) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public bool Disposed { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); Calls++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = Calls == 1 ? new StreamContent(first) : new ByteArrayContent([]) }); }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
}
