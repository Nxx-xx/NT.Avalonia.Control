using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using NT.AvaloniaControl.Markdown;
using Reader = NT.AvaloniaControl.Markdown.Markdown;

namespace Markdown.Headless.Tests;

public sealed class ResourceLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task DetachedReaderIsCollectableWhileAnUncooperativeHostServiceStillRuns(bool code) => HeadlessTestScope.Run(() => DetachedReaderIsCollectableWhileAnUncooperativeHostServiceStillRunsCore(code));

    private async Task DetachedReaderIsCollectableWhileAnUncooperativeHostServiceStillRunsCore(bool code)
    {
        var service = new StalledService();
        var weak = await MountThenDetach(service, code);
        try
        {
            for (var i = 0; i < 8 && weak.IsAlive; i++)
            {
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                await Task.Delay(20, TestContext.Current.CancellationToken);
                Collect();
            }
            Assert.True(service.Token.IsCancellationRequested); Assert.False(service.Finished);
            Assert.False(weak.IsAlive);
            Assert.Equal(0, service.Subscribers);
        }
        finally
        {
            service.Finish(code);
            for (var i = 0; i < 5; i++) { await Task.Delay(10, TestContext.Current.CancellationToken); await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background); }
        }
        if (service.Bitmap is { } bitmap) Assert.ThrowsAny<Exception>(() => bitmap.Lock());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> MountThenDetach(StalledService service, bool code)
    {
        var reader = new Reader { Text = code ? "```text\npending code\n```" : "![pending](https://example.test/image)", ImageLoader = service, CodeHighlighter = service };
        var window = new Window { Width = 500, Height = 260, Content = reader }; window.Show();
        await reader.WhenUpdatedAsync();
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var weak = new WeakReference(reader);
        window.Content = null; window.Close();
        return weak;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Collect() { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }

    private sealed class StalledService : IMarkdownImageLoader, IMarkdownCodeHighlighter
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<MarkdownImageLease> _image = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<IReadOnlyList<MarkdownHighlightSpan>> _code = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken Token { get; private set; } public bool Finished { get; private set; }
        public int Subscribers { get; private set; } public WriteableBitmap? Bitmap { get; private set; }
        public event EventHandler? Invalidated { add => Subscribers++; remove => Subscribers--; }
        public ValueTask<MarkdownImageLease> LoadAsync(Uri uri, CancellationToken token) { Token = token; Started.TrySetResult(); return new(_image.Task); }
        public ValueTask<IReadOnlyList<MarkdownHighlightSpan>> HighlightAsync(string code, string? language, CancellationToken token) { Token = token; Started.TrySetResult(); return new(_code.Task); }
        public void Finish(bool code)
        {
            Finished = true;
            if (code) _code.TrySetResult([]);
            else { Bitmap = new WriteableBitmap(new PixelSize(8, 8), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul); _image.TrySetResult(new(Bitmap)); }
        }
    }
}

