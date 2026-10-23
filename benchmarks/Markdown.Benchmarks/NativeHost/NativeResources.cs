using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using NT.AvaloniaControl.Markdown;
using Reader = NT.AvaloniaControl.Markdown.Markdown;

namespace MarkdownNativeBenchmarks;

internal sealed partial class NativeBenchmarkRunner
{
    private int _resourcePeakParse;
    private int _resourcePeakPending;
    private int _resourcePeakExecuting;
    private int _resourcePeakResourcePending;
    private int _resourceLateUpdates;
    private async Task ResourcesAsync()
    {
        var service = new ResourceServices();
        var iterations = _report.正式采样 ? 100 : _samples;
        var retired = await RunSharedResourceIterationsAsync(service, iterations);
        await _witness.ObserveAsync();
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        var settled = Stopwatch.StartNew();
        while (service.Active > 0 && settled.Elapsed < TimeSpan.FromSeconds(10)) await Task.Delay(5);
        // Forced collection is only the lifecycle probe, never inside timed rendering samples.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            await Task.Delay(20);
            await _witness.ObserveAsync();
        }
        var alive = retired.Count(reference => reference.IsAlive);
        _report.资源["替换卸载次数"] = iterations;
        _report.资源["累计服务请求"] = service.Requests;
        _report.资源["活动服务请求"] = service.Active;
        _report.资源["已取消请求"] = service.Cancelled;
        _report.资源["当前失效订阅"] = service.Subscribers;
        _report.资源["已卸载Reader存活数"] = alive;
        _report.资源["旧Lease存活数"] = service.Leases.Count(reference => reference.IsAlive);
        _report.资源["观察ActiveParse峰值"] = _resourcePeakParse;
        _report.资源["观察Pending峰值"] = _resourcePeakPending;
        _report.资源["观察ExecutingResource峰值"] = _resourcePeakExecuting;
        _report.资源["观察PendingResource峰值"] = _resourcePeakResourcePending;
        _report.资源["当前Reader活动资源请求"] = _reader.ActiveResourceRequestCount;
        _report.资源["卸载后旧更新回调"] = _resourceLateUpdates;
        _report.检查["资源队列有界"] = _resourcePeakParse <= 1 && _resourcePeakPending <= 1 && _resourcePeakExecuting <= 4 && _resourcePeakResourcePending <= 256;
        _report.检查["共享服务未被控件释放"] = !service.Disposed;
        _report.检查["旧资源请求和订阅已结束"] = service.Active == 0 && service.Subscribers == 0;
        _report.检查["旧Reader可回收"] = alive == 0;
        _report.检查["旧Lease可回收"] = service.Leases.All(reference => !reference.IsAlive);
        _report.检查["卸载后旧更新不提交"] = _resourceLateUpdates == 0;
        service.Dispose();
    }
    private async Task<List<WeakReference>> RunSharedResourceIterationsAsync(ResourceServices service, int iterations)
    {
        // Two simultaneously attached readers share the externally owned services.
        // Removing each transient reader must retain the companion's subscriptions.
        var companion = new Reader { ImageLoader = service, CodeHighlighter = service };
        var panel = (Grid)_window.Content!;
        panel.Children.Insert(0, companion);
        companion.Text = "![shared](https://example.test/shared)\n\n```cs\nvar shared = true;\n```";
        await companion.WhenUpdatedAsync().WaitAsync(TimeSpan.FromSeconds(30));
        var retired = new List<WeakReference>();
        var sharedValid = true;
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            retired.Add(await ReplaceAndRetireAsync(service, iteration));
            ObserveResourceCounts(companion);
            sharedValid &= !service.Disposed && service.Subscribers > 0 && companion.HasPresentationEnvironment;
            service.Invalidate();
        }
        _report.检查["并发共享服务卸载隔离"] = sharedValid;
        panel.Children.Remove(companion);
        _window.UpdateLayout();
        retired.Add(new WeakReference(companion));
        return retired;
    }
    private async Task<WeakReference> ReplaceAndRetireAsync(ResourceServices service, int iteration)
    {
        var reader = new Reader { ImageLoader = service, CodeHighlighter = service };
        var retired = false;
        void Count() => ObserveResourceCounts(reader);
        reader.UpdateMeasured += (_, _) => { Count(); if (retired) _resourceLateUpdates++; };
        var panel = (Grid)_window.Content!;
        panel.Children.Insert(0, reader);
        reader.Text = $"![alt](https://example.test/image-{iteration})\n\n```cs\nvar id = {iteration};\n```";
        Count();
        await reader.WhenUpdatedAsync().WaitAsync(TimeSpan.FromSeconds(30));
        reader.Text = $"![alt](https://example.test/replaced-{iteration})\n\n```cs\nvar changed = {iteration};\n```";
        Count();
        await reader.WhenUpdatedAsync().WaitAsync(TimeSpan.FromSeconds(30));
        await _witness.ObserveAsync();
        reader.SelectAll();
        service.Invalidate();
        panel.Children.Remove(reader);
        retired = true;
        _window.UpdateLayout();
        return new WeakReference(reader);
    }
    private void ObserveResourceCounts(Reader reader)
    {
        _resourcePeakParse = Math.Max(_resourcePeakParse, reader.ActiveParseCount);
        _resourcePeakPending = Math.Max(_resourcePeakPending, reader.PendingRequestCount);
        _resourcePeakExecuting = Math.Max(_resourcePeakExecuting, reader.ExecutingResourceRequestCount);
        _resourcePeakResourcePending = Math.Max(_resourcePeakResourcePending, reader.PendingResourceRequestCount);
    }

    private sealed class ResourceServices : IMarkdownImageLoader, IMarkdownCodeHighlighter, IDisposable
    {
        private EventHandler? _invalidated;
        private int _requests, _active, _cancelled, _subscribers;
        public int Requests => Volatile.Read(ref _requests);
        public int Active => Volatile.Read(ref _active);
        public int Cancelled => Volatile.Read(ref _cancelled);
        public int Subscribers => Volatile.Read(ref _subscribers);
        public bool Disposed { get; private set; }
        public List<WeakReference> Leases { get; } = [];
        private readonly object _eventGate = new();
        public event EventHandler? Invalidated
        {
            add { lock (_eventGate) { _invalidated += value; _subscribers = _invalidated?.GetInvocationList().Length ?? 0; } }
            remove { lock (_eventGate) { _invalidated -= value; _subscribers = _invalidated?.GetInvocationList().Length ?? 0; } }
        }
        public void Invalidate() => _invalidated?.Invoke(this, EventArgs.Empty);
        public async ValueTask<MarkdownImageLease> LoadAsync(Uri uri, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requests); Interlocked.Increment(ref _active);
            try
            {
                await Task.Delay(10);
                if (cancellationToken.IsCancellationRequested) Interlocked.Increment(ref _cancelled);
                var bitmap = new WriteableBitmap(new PixelSize(8, 8), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
                var lease = new MarkdownImageLease(bitmap);
                lock (Leases) Leases.Add(new WeakReference(lease));
                return lease;
            }
            catch (OperationCanceledException) { Interlocked.Increment(ref _cancelled); throw; }
            finally { Interlocked.Decrement(ref _active); }
        }
        public async ValueTask<IReadOnlyList<MarkdownHighlightSpan>> HighlightAsync(string code, string? language, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requests); Interlocked.Increment(ref _active);
            try { await Task.Delay(10); if (cancellationToken.IsCancellationRequested) Interlocked.Increment(ref _cancelled); return [new MarkdownHighlightSpan(0, code.Length, "#556677")]; }
            catch (OperationCanceledException) { Interlocked.Increment(ref _cancelled); throw; }
            finally { Interlocked.Decrement(ref _active); }
        }
        public void Dispose() { Disposed = true; }
    }
}
